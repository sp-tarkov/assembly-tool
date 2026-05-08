using System.Diagnostics;
using System.Runtime.InteropServices;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable]
public sealed class AssemblyWriter(
    ILogger<AssemblyWriter> logger,
    DataProvider dataProvider,
    SymbolGenerator symbolGenerator,
    DebuggableAttributeHelper debuggableAttributeHelper
)
{
    internal DeObfuscationResult Deobfuscate(ModuleDefinition? module, string assemblyPath)
    {
        var sw = Stopwatch.StartNew();
        var result = new DeObfuscationResult();

        if (module!.GetAllTypes().Any(t => t.Name?.Contains("GClass") ?? false))
        {
            logger.LogInformation("Assembly is not obfuscated.");

            result.Success = true;
            result.DeObfuscatedAssemblyPath = assemblyPath;
            result.DeObfuscatedModule = module;

            return result;
        }

        logger.LogInformation("Assembly is obfuscated, running de-obfuscation...");

        if (!Deobfuscate(assemblyPath))
        {
            result.Success = false;

            logger.LogError("Failed to deobfuscate assembly.");
            return result;
        }

        var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
        var fileExtension = Path.GetExtension(assemblyPath);
        var managedPath = Path.GetDirectoryName(assemblyPath);
        var cleanedPath = Path.Combine(managedPath!, $"{fileName}-cleaned{fileExtension}");

        result.Success = true;
        result.DeObfuscatedAssemblyPath = cleanedPath;
        result.DeObfuscatedModule = dataProvider.LoadModule(cleanedPath);

        logger.LogInformation(
            "Deobfuscation completed. Took {time:F2} seconds. Deobfuscated assembly written to: {assemblyPath}",
            sw.ElapsedMilliseconds / 1000,
            result.DeObfuscatedAssemblyPath
        );

        return result;
    }

    public void WriteAssembly(
        ModuleDefinition module,
        string targetAssemblyPath,
        string dllName = "-cleaned-direct-mapped-publicized.dll"
    )
    {
        var outPath = Path.Combine(
            Path.GetDirectoryName(targetAssemblyPath)
                ?? throw new NullReferenceException("Target assembly path is null"),
            module.Name?.Replace(".dll", dllName) ?? Utf8String.Empty
        );

        var deltaPath = Path.Combine(
            Path.GetDirectoryName(targetAssemblyPath)
                ?? throw new NullReferenceException("Target assembly path is null"),
            module.Name?.Replace(".dll", ".dll.delta") ?? Utf8String.Empty
        );

        try
        {
            //debuggableAttributeHelper.RemoveDebuggableAttribute(module);
            module.Assembly!.Write(outPath);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Failed to write assembly to: {outPath}", outPath);
            throw;
        }

        logger.LogInformation("Assembly written to: {outPath}", outPath);

        if (dllName != "-cleaned-direct-mapped-publicized.dll")
        {
            return;
        }

        var symbolResult = symbolGenerator.GenerateForAssembly(outPath, GetSymbolPath(outPath, module));

        _ = Task.Run(() => StartHollow(module.GetAllTypes()));

        var hollowedDir = Path.GetDirectoryName(outPath);
        var hollowedPath = Path.Combine(hollowedDir!, "Assembly-CSharp-hollowed.dll");

        try
        {
            module.Write(hollowedPath);
        }
        catch (Exception e)
        {
            logger.LogCritical("Exception during write hollow task:\n{Exception}", e.Message);
            return;
        }

        logger.LogInformation("Hollowed written to: {outPath}", hollowedPath);

        CreateDelta(targetAssemblyPath, outPath, deltaPath);
        CopyToDevelopmentEnvironment(outPath, hollowedPath, deltaPath, symbolResult);
    }

    private static string GetSymbolPath(string assemblyPath, ModuleDefinition module)
    {
        var symbolName = Path.ChangeExtension(module.Name?.ToString() ?? Path.GetFileName(assemblyPath), ".pdb");

        return Path.Combine(
            Path.GetDirectoryName(assemblyPath) ?? throw new NullReferenceException("Assembly path is null"),
            symbolName
        );
    }

    private void CopyToDevelopmentEnvironment(
        string asmPath,
        string hollowedPath,
        string deltaPath,
        SyntheticSymbolGenerationResult symbolResult
    )
    {
        if (
            dataProvider.Settings.CopyToGame
            && !string.IsNullOrEmpty(dataProvider.Settings.GamePath)
            && File.Exists(asmPath)
        )
        {
            var gameDest = Path.Combine(
                dataProvider.Settings.GamePath,
                "EscapeFromTarkov_Data",
                "Managed",
                "Assembly-CSharp.dll"
            );

            if (File.Exists(gameDest))
            {
                File.Copy(asmPath, gameDest, true);
                logger.LogInformation("Assembly has been installed to the game: {GameDest}", gameDest);
                CopySymbolsToGame(gameDest, symbolResult);
            }
        }

        if (
            dataProvider.Settings.CopyToModules
            && !string.IsNullOrEmpty(dataProvider.Settings.ModulesProjectPath)
            && Directory.Exists(dataProvider.Settings.ModulesProjectPath)
            && File.Exists(hollowedPath)
        )
        {
            var hollowedDest = Path.Combine(
                dataProvider.Settings.ModulesProjectPath,
                "project",
                "Shared",
                "Hollowed",
                "hollowed.dll"
            );

            File.Copy(hollowedPath, hollowedDest, true);

            logger.LogInformation("Hollowed has been copied to the modules project: {HollowedDest}", hollowedDest);
        }

        if (
            dataProvider.Settings.CopyToLauncher
            && !string.IsNullOrEmpty(dataProvider.Settings.LauncherProjectPath)
            && Directory.Exists(dataProvider.Settings.LauncherProjectPath)
            && File.Exists(hollowedPath)
        )
        {
            var deltaDest = Path.Combine(
                dataProvider.Settings.LauncherProjectPath,
                "project",
                "SPTarkov.Core",
                "SPT_Data",
                "Launcher",
                "Patches",
                "SPT-core",
                "EscapeFromTarkov_Data",
                "Managed",
                "Assembly-CSharp.dll.delta"
            );

            File.Copy(deltaPath, deltaDest, true);

            logger.LogInformation("Delta has been copied to the launcher project: {HollowedDest}", deltaDest);
        }
    }

    private void CopySymbolsToGame(string gameAssemblyPath, SyntheticSymbolGenerationResult symbolResult)
    {
        var gameDir =
            Path.GetDirectoryName(gameAssemblyPath) ?? throw new NullReferenceException("Game assembly path is null");

        var pdbDest = Path.Combine(gameDir, Path.GetFileName(symbolResult.PdbPath));
        File.Copy(symbolResult.PdbPath, pdbDest, true);

        var mdbDest = $"{gameAssemblyPath}.mdb";
        File.Copy(symbolResult.MdbPath, mdbDest, true);

        if (File.Exists(symbolResult.SourcePath))
        {
            var sourceDest = Path.Combine(gameDir, Path.GetFileName(symbolResult.SourcePath));
            File.Copy(symbolResult.SourcePath, sourceDest, true);
        }

        logger.LogInformation(
            "Synthetic symbols have been installed to the game: {PdbDest}, {MdbDest}",
            pdbDest,
            mdbDest
        );
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow(IEnumerable<TypeDefinition> types)
    {
        var tasks = new List<Task>(types.Count());

        foreach (var type in types)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        HollowType(type);
                    }
                    catch (Exception ex)
                    {
                        logger.LogCritical("Exception in hollow task:\n{ExMessage}", ex.Message);
                    }
                })
            );
        }

        await Task.WhenAll(tasks.ToArray());
    }

    private static void HollowType(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(m => m.HasMethodBody))
        {
            // Create a new empty CIL body
            var newBody = new CilMethodBody();

            // If the method returns something, return default value
            if (method.Signature?.ReturnType != null && method.Signature.ReturnType.ElementType != ElementType.Void)
            {
                // Push default value onto the stack
                newBody.Instructions.Add(CilOpCodes.Ldnull);
            }

            // Just return (for void methods)
            newBody.Instructions.Add(CilOpCodes.Ret);

            // Assign the new method body
            method.CilMethodBody = newBody;
        }
    }

    public bool Deobfuscate(string assemblyPath, bool isLauncher = false)
    {
        var module = ModuleDefinition.FromFile(assemblyPath);

        var potentialStringDelegates = new List<MethodDefinition>();

        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                if (
                    method.Signature!.ReturnType.FullName != "System.String"
                    || method.Parameters.Count != 1
                    || method.Parameters[0].ParameterType.FullName != "System.Int32"
                    || method.CilMethodBody is null
                    || !method.IsStatic
                )
                {
                    continue;
                }

                if (
                    !method.CilMethodBody.Instructions.Any(x =>
                        x.OpCode.Code == CilCode.Callvirt
                        && ((IMethodDefOrRef)x.Operand!).FullName
                            == "System.Object System.AppDomain::GetData(System.String)"
                    )
                )
                {
                    continue;
                }

                potentialStringDelegates.Add(method);
            }
        }

        if (potentialStringDelegates.Count != 1)
        {
            logger.LogError(
                "Expected to find 1 potential string delegate method; found {Count}. Candidates: {Join}",
                potentialStringDelegates.Count,
                string.Join("\r\n", potentialStringDelegates.Select(x => x.FullName))
            );

            return false;
        }

        var methodDef = potentialStringDelegates[0];
        var deobfRid = methodDef.MetadataToken;

        // Construct the token string (similar to Mono.Cecil's format)
        // Shift table index to the upper 8 bits
        var token = $"0x{((uint)deobfRid.Table << 24 | deobfRid.Rid):x4}";
        logger.LogInformation("Deobfuscated token: {Token}", token);

        var cmd = isLauncher
            ? $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtok \"{token}\""
            : $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtyp delegate --strtok \"{token}\"";

        var executablePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Binaries", "de4dot", "de4dot-x64.exe");
        var workingDir = Path.GetDirectoryName(executablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDir,
            Arguments = cmd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var proc = new Process();
        proc.StartInfo = startInfo;

        proc.Start();
        proc.WaitForExit();

        return true;
    }

    /// <summary>
    ///     Creates a delta patch for the provided paths
    /// </summary>
    /// <param name="originalFilePath">Original unpatched file path</param>
    /// <param name="patchedFilePath">Patched file path</param>
    /// <param name="deltaFilePath">Path to where the delta should be created</param>
    public void CreateDelta(string originalFilePath, string patchedFilePath, string deltaFilePath)
    {
        var hdiffExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "hdiffz.elf" : "hdiffz.exe";
        var hdiffPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Binaries", "Hdiffz", hdiffExecutable);

        if (File.Exists(deltaFilePath))
        {
            File.Delete(deltaFilePath);
        }

        var arguments = $"-s-64 -c-zstd-21-24 -d \"{originalFilePath}\" \"{patchedFilePath}\" \"{deltaFilePath}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = hdiffPath,
            WorkingDirectory = Path.GetDirectoryName(hdiffPath),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();
        //var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (error.Length > 0)
        {
            logger.LogError("Error: {Error}", error);
            return;
        }

        logger.LogInformation("Delta written to: {outPath}", deltaFilePath);
    }
}
