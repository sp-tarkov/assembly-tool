using AsmResolver.DotNet;
using AssemblyLib.Utils;
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
			Logger.Log("Assembly is obfuscated, running de-obfuscation...\n", ConsoleColor.Yellow);
			
			module = null;
            
			Deobfuscator.Deobfuscate(assemblyPath);
            
			var cleanedName = Path.GetFileNameWithoutExtension(assemblyPath);
			cleanedName = $"{cleanedName}-cleaned.dll";
            
			var newPath = Path.GetDirectoryName(assemblyPath);
			newPath = Path.Combine(newPath!, cleanedName);
			
			module = dataProvider.LoadModule(newPath);
		}
		
		return (assemblyPath, module);
	}
}