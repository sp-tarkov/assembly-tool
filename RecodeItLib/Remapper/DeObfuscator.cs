using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ReCodeIt.Utils;
using System.Diagnostics;
using ReCodeIt.ReMapper;

namespace ReCodeItLib.Remapper;

public static class Deobfuscator
{
    public static void Deobfuscate(string assemblyPath, bool isLauncher = false)
    {
        var executablePath = Path.Combine(DataProvider.DataPath, "De4dot", "de4dot.exe");

        string token;

        ModuleContext modCtx = ModuleDef.CreateModuleContext();
        ModuleDefMD module = ModuleDefMD.Load(assemblyPath, modCtx);

        var potentialStringDelegates = new List<MethodDef>();

        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.ReturnType.FullName != "System.String"
                    || method.Parameters.Count != 1
                    || method.Parameters[0].Type.FullName != "System.Int32"
                    || method.Body == null
                    || !method.IsStatic)
                {
                    continue;
                }

                if (!method.Body.Instructions.Any(x =>
                        x.OpCode.Code == Code.Callvirt &&
                        ((IMethodDefOrRef)x.Operand).FullName == "System.Object System.AppDomain::GetData(System.String)"))
                {
                    continue;
                }

                potentialStringDelegates.Add(method);
            }
        }

        if (potentialStringDelegates.Count != 1)
        {
            Logger.Log($"Expected to find 1 potential string delegate method; found {potentialStringDelegates.Count}. Candidates: {string.Join("\r\n", potentialStringDelegates.Select(x => x.FullName))}");
        }

        var methodDef = potentialStringDelegates[0];
        MDToken deobfRid = methodDef.MDToken;

        // Construct the token string (similar to Mono.Cecil's format)
        token = $"0x{(deobfRid.Raw | deobfRid.Rid):x4}";
        Console.WriteLine($"Deobfuscation token: {token}");

        var cmd = isLauncher
            ? $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtok \"{token}\""
            : $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtyp delegate --strtok \"{token}\"";
        
        var process = Process.Start(executablePath, cmd);

        process.WaitForExit();

        var extName = isLauncher ? "-cleaned.exe" : "-cleaned.dll";
        
        // Fixes "ResolutionScope is null" by rewriting the assembly
        var cleanedDllPath = Path.Combine(Path.GetDirectoryName(assemblyPath), Path.GetFileNameWithoutExtension(assemblyPath) + extName);

        ModuleDefMD assemblyRewrite = null;

        using (var memoryStream = new MemoryStream(File.ReadAllBytes(cleanedDllPath)))
        {
            assemblyRewrite = ModuleDefMD.Load(memoryStream, modCtx);

            if (isLauncher)
            {
                SPTPublicizer.PublicizeClasses(assemblyRewrite, true);
            }
        }

        assemblyRewrite.Write(cleanedDllPath);
    }
}