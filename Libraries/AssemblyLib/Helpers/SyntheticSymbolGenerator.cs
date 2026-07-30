using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using AsmResolver.PE;
using AsmResolver.PE.Builder;
using AsmResolver.PE.Debug;
using SPTarkov.DI.Annotations;
using MonoCecil = Mono.Cecil;
using MonoCecilCil = Mono.Cecil.Cil;
using MonoCecilMdb = Mono.Cecil.Mdb;

namespace AssemblyLib.Helpers;

[Injectable]
public sealed class SymbolGenerator(ILogger<SymbolGenerator> logger)
{
    private static readonly Guid CSharpLanguageGuid = new("3f5162f8-07c6-11d3-9053-00c04fa302a1");
    private static readonly Guid Sha256HashAlgorithmGuid = new("8829d00f-11b8-4213-878b-770e8597ac16");

    public SyntheticSymbolGenerationResult GenerateForAssembly(
        string assemblyPath,
        string? pdbPath = null,
        string? sourcePath = null
    )
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Assembly file does not exist.", assemblyPath);
        }

        pdbPath ??= Path.ChangeExtension(assemblyPath, ".pdb");
        sourcePath ??= Path.Combine(
            Path.GetDirectoryName(pdbPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(assemblyPath)}.synthetic-symbols.cs"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(pdbPath) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? ".");

        Dictionary<int, MethodSymbolInfo> methodSymbols;
        ImmutableArray<int> typeSystemRowCounts;

        using (var assemblyStream = File.OpenRead(assemblyPath))
        using (var peReader = new PEReader(assemblyStream))
        {
            var metadataReader = peReader.GetMetadataReader();
            var methodNames = BuildMethodNameMap(metadataReader);
            methodSymbols = WriteSyntheticSourceMap(peReader, metadataReader, methodNames, sourcePath);
            typeSystemRowCounts = GetTypeSystemRowCounts(metadataReader);
        }

        var sourceHash = SHA256.HashData(File.ReadAllBytes(sourcePath));

        var pdbMetadata = new MetadataBuilder();
        var document = pdbMetadata.AddDocument(
            pdbMetadata.GetOrAddDocumentName(sourcePath),
            pdbMetadata.GetOrAddGuid(Sha256HashAlgorithmGuid),
            pdbMetadata.GetOrAddBlob(sourceHash),
            pdbMetadata.GetOrAddGuid(CSharpLanguageGuid)
        );

        var sequencePointCount = 0;

        var methodDefinitionCount = typeSystemRowCounts[(int)TableIndex.MethodDef];

        for (var methodRow = 1; methodRow <= methodDefinitionCount; methodRow++)
        {
            if (methodSymbols.TryGetValue(methodRow, out var methodSymbol))
            {
                var sequencePoints = BuildSequencePointBlob(methodSymbol);
                pdbMetadata.AddMethodDebugInformation(document, pdbMetadata.GetOrAddBlob(sequencePoints));
                sequencePointCount += methodSymbol.InstructionOffsets.Length;
                continue;
            }

            pdbMetadata.AddMethodDebugInformation(default, default);
        }

        var pdbBuilder = new PortablePdbBuilder(
            pdbMetadata,
            typeSystemRowCounts,
            entryPoint: default,
            idProvider: CreateDeterministicContentId
        );

        var pdbBlob = new BlobBuilder();
        var pdbId = pdbBuilder.Serialize(pdbBlob);

        using (var pdbStream = File.Create(pdbPath))
        {
            pdbBlob.WriteContentTo(pdbStream);
        }

        var mdbPath = GenerateMonoSymbols(assemblyPath, sourcePath, methodSymbols);
        AttachCodeViewDebugDirectory(assemblyPath, pdbPath, pdbId);

        logger.LogInformation(
            "Synthetic symbols written to {PdbPath} and {MdbPath}. "
                + "Generated {SequencePointCount} sequence points for {MethodCount} methods.",
            pdbPath,
            mdbPath,
            sequencePointCount,
            methodSymbols.Count
        );

        return new SyntheticSymbolGenerationResult(
            pdbPath,
            mdbPath,
            sourcePath,
            pdbId.Guid,
            pdbId.Stamp,
            methodSymbols.Count,
            sequencePointCount
        );
    }

    private static void AttachCodeViewDebugDirectory(string assemblyPath, string pdbPath, BlobContentId pdbId)
    {
        var tempPath = $"{assemblyPath}.{Guid.NewGuid():N}.tmp";

        var image = PEImage.FromBytes(File.ReadAllBytes(assemblyPath));
        image.DebugData.Clear();
        image.DebugData.Add(
            new DebugDataEntry(
                new RsdsDataSegment
                {
                    Guid = pdbId.Guid,
                    Age = 1,
                    Path = Path.GetFileName(pdbPath),
                }
            )
            {
                TimeDateStamp = pdbId.Stamp,
                MajorVersion = 0x0100,
            }
        );

        var peFile = new ManagedPEFileBuilder().CreateFile(image);
        peFile.Write(tempPath);

        try
        {
            ReplaceFileWithRetry(tempPath, assemblyPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ReplaceFileWithRetry(string sourcePath, string destinationPath)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static Dictionary<int, string> BuildMethodNameMap(MetadataReader metadataReader)
    {
        var result = new Dictionary<int, string>();

        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var type = metadataReader.GetTypeDefinition(typeHandle);
            var typeName = GetFullTypeName(metadataReader, type);

            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadataReader.GetMethodDefinition(methodHandle);
                var methodName = metadataReader.GetString(method.Name);
                result[MetadataTokens.GetRowNumber(methodHandle)] = $"{typeName}::{methodName}";
            }
        }

        return result;
    }

    private static string GenerateMonoSymbols(
        string assemblyPath,
        string sourcePath,
        IReadOnlyDictionary<int, MethodSymbolInfo> methodSymbols
    )
    {
        var mdbPath = GetMonoSymbolPath(assemblyPath);

        using var module = MonoCecil.ModuleDefinition.ReadModule(
            assemblyPath,
            new MonoCecil.ReaderParameters { ReadSymbols = false }
        );

        var document = new MonoCecilCil.Document(sourcePath)
        {
            Type = MonoCecilCil.DocumentType.Text,
            Language = MonoCecilCil.DocumentLanguage.CSharp,
            LanguageVendor = MonoCecilCil.DocumentLanguageVendor.Microsoft,
            HashAlgorithm = MonoCecilCil.DocumentHashAlgorithm.SHA256,
            Hash = SHA256.HashData(File.ReadAllBytes(sourcePath)),
        };

        var methods = GetAllCecilMethods(module.Types).ToArray();

        foreach (var method in methods)
        {
            var methodRow = checked((int)method.MetadataToken.RID);

            if (!method.HasBody || !methodSymbols.TryGetValue(methodRow, out var methodSymbol))
            {
                continue;
            }

            method.DebugInformation.SequencePoints.Clear();

            foreach (var instruction in method.Body.Instructions)
            {
                var sequencePointIndex = methodSymbol.InstructionOffsets.IndexOf(instruction.Offset);

                if (sequencePointIndex < 0)
                {
                    continue;
                }

                var line = methodSymbol.StartLine + sequencePointIndex;
                method.DebugInformation.SequencePoints.Add(
                    new MonoCecilCil.SequencePoint(instruction, document)
                    {
                        StartLine = line,
                        StartColumn = 1,
                        EndLine = line,
                        EndColumn = 2,
                    }
                );
            }
        }

        if (File.Exists(mdbPath))
        {
            File.Delete(mdbPath);
        }

        using var symbolWriter = new MonoCecilMdb.MdbWriterProvider().GetSymbolWriter(module, assemblyPath);

        foreach (var method in methods.Where(method => method.DebugInformation.HasSequencePoints))
        {
            symbolWriter.Write(method.DebugInformation);
        }

        symbolWriter.Write();

        return mdbPath;
    }

    private static IEnumerable<MonoCecil.MethodDefinition> GetAllCecilMethods(
        IEnumerable<MonoCecil.TypeDefinition> types
    )
    {
        foreach (var type in types)
        {
            foreach (var method in type.Methods)
            {
                yield return method;
            }

            foreach (var nestedMethod in GetAllCecilMethods(type.NestedTypes))
            {
                yield return nestedMethod;
            }
        }
    }

    private static string GetMonoSymbolPath(string assemblyPath)
    {
        return $"{assemblyPath}.mdb";
    }

    private static string GetFullTypeName(MetadataReader metadataReader, TypeDefinition type)
    {
        var name = metadataReader.GetString(type.Name);
        var @namespace = metadataReader.GetString(type.Namespace);

        return string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";
    }

    private static Dictionary<int, MethodSymbolInfo> WriteSyntheticSourceMap(
        PEReader peReader,
        MetadataReader metadataReader,
        IReadOnlyDictionary<int, string> methodNames,
        string sourcePath
    )
    {
        var result = new Dictionary<int, MethodSymbolInfo>();
        var currentLine = 1;

        var sourceEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var writer = new StreamWriter(sourcePath, append: false, sourceEncoding);
        writer.WriteLine("// <auto-generated />");
        currentLine++;

        foreach (var methodHandle in metadataReader.MethodDefinitions)
        {
            var method = metadataReader.GetMethodDefinition(methodHandle);

            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var methodBody = peReader.GetMethodBody(method.RelativeVirtualAddress);
            var instructionOffsets = GetInstructionOffsets(methodBody.GetILBytes());

            if (instructionOffsets.Length == 0)
            {
                continue;
            }

            var methodRow = MetadataTokens.GetRowNumber(methodHandle);
            var methodName = methodNames.GetValueOrDefault(methodRow, $"method_{methodRow:x8}");
            var startLine = currentLine;

            foreach (var offset in instructionOffsets)
            {
                writer.WriteLine($"// {methodName} IL_{offset:x4}");
                currentLine++;
            }

            result[methodRow] = new MethodSymbolInfo(
                MetadataTokens.GetRowNumber(methodBody.LocalSignature),
                startLine,
                instructionOffsets
            );
        }

        return result;
    }

    private static ImmutableArray<int> GetTypeSystemRowCounts(MetadataReader metadataReader)
    {
        var rowCounts = new int[MetadataTokens.TableCount];

        for (var i = 0; i < rowCounts.Length; i++)
        {
            var table = (TableIndex)i;

            if (IsTypeSystemTable(table))
            {
                rowCounts[i] = metadataReader.GetTableRowCount(table);
            }
        }

        return rowCounts.ToImmutableArray();
    }

    private static bool IsTypeSystemTable(TableIndex table)
    {
        return table
            is not TableIndex.FieldPtr
                and not TableIndex.MethodPtr
                and not TableIndex.ParamPtr
                and not TableIndex.EventPtr
                and not TableIndex.PropertyPtr
                and not TableIndex.EncLog
                and not TableIndex.EncMap
                and not TableIndex.Document
                and not TableIndex.MethodDebugInformation
                and not TableIndex.LocalScope
                and not TableIndex.LocalVariable
                and not TableIndex.LocalConstant
                and not TableIndex.ImportScope
                and not TableIndex.StateMachineMethod
                and not TableIndex.CustomDebugInformation;
    }

    private static BlobBuilder BuildSequencePointBlob(MethodSymbolInfo methodSymbol)
    {
        var builder = new BlobBuilder();
        builder.WriteCompressedInteger(methodSymbol.LocalSignatureRowId);

        var previousOffset = 0;
        var previousLine = 0;
        var previousColumn = 0;

        for (var i = 0; i < methodSymbol.InstructionOffsets.Length; i++)
        {
            var offset = methodSymbol.InstructionOffsets[i];
            var line = methodSymbol.StartLine + i;
            const int column = 1;

            builder.WriteCompressedInteger(i == 0 ? offset : offset - previousOffset);
            builder.WriteCompressedInteger(0);
            builder.WriteCompressedInteger(1);

            if (i == 0)
            {
                builder.WriteCompressedInteger(line);
                builder.WriteCompressedInteger(column);
            }
            else
            {
                builder.WriteCompressedSignedInteger(line - previousLine);
                builder.WriteCompressedSignedInteger(column - previousColumn);
            }

            previousOffset = offset;
            previousLine = line;
            previousColumn = column;
        }

        return builder;
    }

    private static ImmutableArray<int> GetInstructionOffsets(byte[]? ilBytes)
    {
        if (ilBytes is null || ilBytes.Length == 0)
        {
            return [];
        }

        var offsets = ImmutableArray.CreateBuilder<int>();
        var offset = 0;

        while (offset < ilBytes.Length)
        {
            offsets.Add(offset);

            var instructionSize = GetInstructionSize(ilBytes, offset);

            if (instructionSize <= 0)
            {
                break;
            }

            offset += instructionSize;
        }

        return offsets.ToImmutable();
    }

    private static int GetInstructionSize(byte[] ilBytes, int offset)
    {
        var opcode = ilBytes[offset];

        if (opcode == 0xfe)
        {
            return offset + 1 >= ilBytes.Length ? 1 : 2 + GetTwoByteOperandSize(ilBytes[offset + 1]);
        }

        return 1 + GetSingleByteOperandSize(ilBytes, offset);
    }

    private static int GetSingleByteOperandSize(byte[] ilBytes, int offset)
    {
        var opcode = ilBytes[offset];

        return opcode switch
        {
            >= 0x09 and <= 0x13 => 1,
            0x1f => 1,
            0x20 => 4,
            0x21 => 8,
            0x22 => 4,
            0x23 => 8,
            0x27 => 4,
            0x28 => 4,
            0x29 => 4,
            >= 0x2b and <= 0x37 => 1,
            >= 0x38 and <= 0x44 => 4,
            0x45 => GetSwitchOperandSize(ilBytes, offset),
            >= 0x6f and <= 0x75 => 4,
            0x79 => 4,
            >= 0x7b and <= 0x81 => 4,
            >= 0x8c and <= 0x8d => 4,
            0x8f => 4,
            >= 0xa3 and <= 0xa5 => 4,
            0xc2 => 4,
            0xc6 => 4,
            0xd0 => 4,
            0xdd => 1,
            0xde => 4,
            _ => 0,
        };
    }

    private static int GetSwitchOperandSize(byte[] ilBytes, int offset)
    {
        if (offset + 4 >= ilBytes.Length)
        {
            return Math.Max(0, ilBytes.Length - offset - 1);
        }

        var targetCount = BitConverter.ToInt32(ilBytes, offset + 1);
        var operandSize = 4 + targetCount * 4;

        return operandSize < 4 ? 4 : Math.Min(operandSize, ilBytes.Length - offset - 1);
    }

    private static int GetTwoByteOperandSize(byte opcode)
    {
        return opcode switch
        {
            0x06 => 4,
            0x07 => 4,
            >= 0x09 and <= 0x0e => 2,
            0x12 => 1,
            0x15 => 4,
            0x16 => 4,
            0x19 => 1,
            0x1c => 4,
            _ => 0,
        };
    }

    private static BlobContentId CreateDeterministicContentId(IEnumerable<Blob> blobs)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var blob in blobs)
        {
            hash.AppendData(blob.GetBytes());
        }

        return BlobContentId.FromHash(hash.GetHashAndReset());
    }

    private sealed record MethodSymbolInfo(
        int LocalSignatureRowId,
        int StartLine,
        ImmutableArray<int> InstructionOffsets
    );
}

public sealed record SyntheticSymbolGenerationResult(
    string PdbPath,
    string MdbPath,
    string SourcePath,
    Guid PdbGuid,
    uint PdbStamp,
    int MethodCount,
    int SequencePointCount
);
