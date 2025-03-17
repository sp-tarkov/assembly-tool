using AssemblyLib.Application;
using dnlib.DotNet;
using AssemblyLib.Utils;

namespace AssemblyLib.ReMapper.MetaData;

internal sealed class DiffCompare(ModuleDefMD oldModule) 
	: IComponent
{
	public bool IsSame(TypeDef newType)
	{
		var oldTypes = oldModule.GetTypes();
		var typeDefs = oldTypes as TypeDef[] ?? oldTypes.ToArray();
		
		var oldType = typeDefs.FirstOrDefault(t => t.FullName == newType.FullName);
		
		// new type does not exist in the old assembly
		if (oldType is null)
		{
			return false;
		}

		// Do counts of members match?
		if (!IsSameCounts(newType, oldType))
		{
			//Logger.LogSync($"{newType.Name} has differences - member count differ", ConsoleColor.Yellow);
			return false;
		}

		foreach (var newMethod in newType.Methods)
		{
			var oldMethod = oldType.Methods.FirstOrDefault(m => m.FullName == newMethod.FullName);
			
			// Method does not exist in old assembly
			if (oldMethod is null)
			{
				//Logger.LogSync($"{newType.Name} has differences - new method(s)", ConsoleColor.Yellow);
				return false;
			}
			
			if (!IsMethodSame(newMethod, oldMethod))
			{
				//Logger.LogSync($"{newType.Name} has differences - method signature change(s)", ConsoleColor.Yellow);
				return false;
			}
		}

		//Logger.LogSync($"{newType.Name} is same", ConsoleColor.Green);
		return true;
	}

	private static bool IsSameCounts(TypeDef newType, TypeDef oldType)
	{
		// Field count differs
		if (newType.Fields.Count != oldType.Fields.Count)
		{
			return false;
		}

		// Property count differs
		if (newType.Properties.Count != oldType.Properties.Count)
		{
			return false;
		}

		// Method count differs
		if (newType.Methods.Count != oldType.Methods.Count)
		{
			return false;
		}
		
		// Event count differs
		if (newType.Events.Count != oldType.Events.Count)
		{
			return false;
		}
		
		// Nested type count differs
		if (newType.NestedTypes.Count != oldType.NestedTypes.Count)
		{
			return false;
		}
		
		return true;
	}

	private static bool IsMethodSame(MethodDef newMethod, MethodDef oldMethod)
	{
		if (newMethod.Parameters.Count != oldMethod.Parameters.Count)
		{
			return false;
		}

		if (newMethod.ReturnType != oldMethod.ReturnType)
		{
			return false;
		}

		if (newMethod.GenericParameters.Count != oldMethod.GenericParameters.Count)
		{
			return false;
		}

		if (newMethod.Body.Instructions.Count != oldMethod.Body.Instructions.Count)
		{
			return false;
		}
		
		return true;
	}
}