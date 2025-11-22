using System.Diagnostics;
using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public sealed class AssemblyWriter(DataProvider dataProvider)
{
    internal DeObfuscationResult Deobfuscate(ModuleDefinition? module, string assemblyPath)
    {
        var sw = Stopwatch.StartNew();
        var result = new DeObfuscationResult();

        if (module!.GetAllTypes().Any(t => t.Name?.Contains("GClass") ?? false))
        {
            Log.Information("Assembly is not obfuscated.");

            result.Success = true;
            result.DeObfuscatedAssemblyPath = assemblyPath;
            result.DeObfuscatedModule = module;

            return result;
        }

        Log.Information("Assembly is obfuscated, running de-obfuscation...");

        if (!Deobfuscate(assemblyPath))
        {
            result.Success = false;

            Log.Error("Failed to deobfuscate assembly.");
            return result;
        }

        var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
        var managedPath = Path.GetDirectoryName(assemblyPath);
        var cleanedPath = Path.Combine(managedPath!, $"{fileName}-cleaned.dll");

        result.Success = true;
        result.DeObfuscatedAssemblyPath = cleanedPath;
        result.DeObfuscatedModule = dataProvider.LoadModule(cleanedPath);

        Log.Information(
            "Deobfuscation completed. Took {time:F2} seconds. Deobfuscated assembly written to: {assemblyPath}",
            sw.ElapsedMilliseconds / 1000,
            result.DeObfuscatedAssemblyPath
        );

        return result;
    }

    public async Task WriteAssembly(ModuleDefinition module, string targetAssemblyPath)
    {
        const string dllName = "-cleaned-direct-mapped-publicized.dll";
        var outPath = Path.Combine(
            Path.GetDirectoryName(targetAssemblyPath)
                ?? throw new NullReferenceException("Target assembly path is null"),
            module.Name?.Replace(".dll", dllName) ?? Utf8String.Empty
        );

        try
        {
            module.Assembly!.Write(outPath);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Failed to write assembly to: {outPath}", outPath);
            throw;
        }

        Log.Information("Direct map completed. Assembly written to: {outPath}", outPath);

        await StartHollow(module.GetAllTypes());

        var hollowedDir = Path.GetDirectoryName(outPath);
        var hollowedPath = Path.Combine(hollowedDir!, "Assembly-CSharp-hollowed.dll");

        try
        {
            module.Write(hollowedPath);
        }
        catch (Exception e)
        {
            Log.Error("Exception during write hollow task:\n{Exception}", e.Message);
            return;
        }

        StartHDiffz(outPath);
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow(IEnumerable<TypeDefinition> types)
    {
        Log.Information("Creating Hollow...");

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
                        Log.Error("Exception in task:\n{ExMessage}", ex.Message);
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
            var newBody = new CilMethodBody(method);

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
            Log.Error(
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
        Log.Information("Deobfuscated token: {Token}", token);

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

    public void StartHDiffz(string outPath)
    {
        Log.Information("Creating Delta...");

        var hdiffPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Binaries", "HDiffz", "hdiffz.exe");

        var outDir = Path.GetDirectoryName(outPath);

        var originalFile = Path.Combine(outDir!, "Assembly-CSharp.dll");
        var patchedFile = Path.Combine(outDir!, "Assembly-CSharp-cleaned-direct-mapped-publicized.dll");
        var deltaFile = Path.Combine(outDir!, "Assembly-CSharp.dll.delta");

        if (File.Exists(deltaFile))
        {
            File.Delete(deltaFile);
        }

        var arguments = $"-s-64 -c-zstd-21-24 -d \"{originalFile}\" \"{patchedFile}\" \"{deltaFile}\"";

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
            Log.Error("Error: {Error}", error);
        }
    }
}
