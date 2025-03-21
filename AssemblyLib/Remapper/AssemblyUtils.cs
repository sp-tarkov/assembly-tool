using AsmResolver.DotNet;
using AssemblyLib.Utils;

namespace AssemblyLib.ReMapper;

internal static class AssemblyUtils
{
	public static (string, ModuleDefinition) TryDeObfuscate(ModuleDefinition? module, string assemblyPath)
	{
		if (!module!.GetAllTypes().Any(t => t.Name.Contains("GClass")))
		{
			Logger.Log("Assembly is obfuscated, running de-obfuscation...\n", ConsoleColor.Yellow);
			
			module = null;
            
			Deobfuscator.Deobfuscate(assemblyPath);
            
			var cleanedName = Path.GetFileNameWithoutExtension(assemblyPath);
			cleanedName = $"{cleanedName}-cleaned.dll";
            
			var newPath = Path.GetDirectoryName(assemblyPath);
			newPath = Path.Combine(newPath!, cleanedName);
			
			module = DataProvider.LoadModule(newPath);
		}
		
		return (assemblyPath, module);
	}
}