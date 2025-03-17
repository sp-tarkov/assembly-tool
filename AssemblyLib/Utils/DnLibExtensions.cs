using dnlib.DotNet;

namespace AssemblyLib.Utils;

public static class TypeResolveUtil
{
	public static AssemblyRef? GetCorlibAssembly(this ModuleDefMD module)
	{
		return module.GetAssemblyRefs()
			.FirstOrDefault(
				assembly => assembly.Name == "mscorlib" || 
				            assembly.Name == "System.Private.CoreLib");
	}

	public static TypeDef? GetGlobalType(this ModuleDefMD module, string fullName)
	{
		var assemblyRefs = module.GetAssemblyRefs();

		return assemblyRefs.SelectMany(assemblyRef => module.GetTypes())
			.FirstOrDefault(type => type.FullName == fullName);
	}
}