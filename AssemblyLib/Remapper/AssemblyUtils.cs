using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper;

[Injectable]
public class AssemblyUtils(
	DataProvider dataProvider
	)
{
	public (string, ModuleDefinition) TryDeObfuscate(ModuleDefinition? module, string assemblyPath)
	{
		if (!module!.GetAllTypes().Any(t => t.Name.Contains("GClass")))
		{
			Log.Information("Assembly is obfuscated, running de-obfuscation...");
			
			module = null;
            
			Deobfuscate(assemblyPath);
            
			var cleanedName = Path.GetFileNameWithoutExtension(assemblyPath);
			cleanedName = $"{cleanedName}-cleaned.dll";
            
			var newPath = Path.GetDirectoryName(assemblyPath);
			newPath = Path.Combine(newPath!, cleanedName);
			
			module = dataProvider.LoadModule(newPath);
		}
		
		return (assemblyPath, module);
	}
	
	public void Deobfuscate(string assemblyPath, bool isLauncher = false)
    { 
        string token;
        
        var module = ModuleDefinition.FromFile(assemblyPath);

        var potentialStringDelegates = new List<MethodDefinition>();

        foreach (var type in module.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.Signature!.ReturnType.FullName != "System.String"
                    || method.Parameters.Count != 1
                    || method.Parameters[0].ParameterType.FullName != "System.Int32"
                    || method.CilMethodBody is null
                    || !method.IsStatic)
                {
                    continue;
                }

                if (!method.CilMethodBody.Instructions.Any(x =>
                        x.OpCode.Code == CilCode.Callvirt &&
                        ((IMethodDefOrRef)x.Operand!).FullName ==
                        "System.Object System.AppDomain::GetData(System.String)"))
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
        }

        var methodDef = potentialStringDelegates[0];
        var deobfRid = methodDef.MetadataToken;

        // Construct the token string (similar to Mono.Cecil's format)
        // Shift table index to the upper 8 bits
        token = $"0x{((uint)deobfRid.Table << 24 | deobfRid.Rid):x4}";
        Log.Information("Deobfuscated token: {Token}", token);
        
        var cmd = isLauncher
            ? $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtok \"{token}\""
            : $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{assemblyPath}\" --strtyp delegate --strtok \"{token}\"";
        
        var executablePath = Path.Combine(AppContext.BaseDirectory, "de4dot", "de4dot-x64.exe");
        var workingDir = Path.GetDirectoryName(executablePath);
        
        var startInfo = new ProcessStartInfo
        {
	        FileName = executablePath,
	        WorkingDirectory = workingDir,
	        Arguments = cmd,
	        UseShellExecute = false,
	        RedirectStandardOutput = true,
	        RedirectStandardError = true,
	        CreateNoWindow = true
        };
        
        var proc = new Process();
        proc.StartInfo = startInfo;
        
        proc.Start();
        proc.WaitForExit();
    }
	
	public void StartHDiffz(string outPath)
	{
		Log.Information("Creating Delta...");
        
		var hdiffPath = Path.Combine(AppContext.BaseDirectory, "Data", "hdiffz.exe");

		var outDir = Path.GetDirectoryName(outPath);
        
		var originalFile = Path.Combine(outDir!, "Assembly-CSharp.dll");
		var patchedFile = Path.Combine(outDir!, "Assembly-CSharp-cleaned-remapped-publicized.dll");
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
			CreateNoWindow = true
		};

		using var process = new Process();
		process.StartInfo = startInfo;

		process.Start();
		//var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (error.Length > 0)
		{
			Log.Error("Error: {Error}",error);
		}
	}
}