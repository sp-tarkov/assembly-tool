using AssemblyLib.Utils;
using dnlib.DotNet;

namespace AssemblyLib.ReMapper;

internal static class AssemblyUtils
{
	public static string TryDeObfuscate(ModuleDefMD module, string assemblyPath, out ModuleDefMD cleanedModule)
	{
		if (!module!.GetTypes().Any(t => t.Name.Contains("GClass")))
		{
			Logger.Log("Assembly is obfuscated, running de-obfuscation...\n", ConsoleColor.Yellow);
            
			module.Dispose();
			module = null;
            
			Deobfuscator.Deobfuscate(assemblyPath);
            
			var cleanedName = Path.GetFileNameWithoutExtension(assemblyPath);
			cleanedName = $"{cleanedName}-cleaned.dll";
            
			var newPath = Path.GetDirectoryName(assemblyPath);
			newPath = Path.Combine(newPath!, cleanedName);
            
			Logger.Log($"Cleaning assembly: {newPath}", ConsoleColor.Green);
            
			cleanedModule = DataProvider.LoadModule(newPath);
			return newPath;
		}

		cleanedModule = module;
		return assemblyPath;
	}
}