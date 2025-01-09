using dnlib.DotNet;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

public class AutoMatcher(List<RemapModel> mappings, string mappingPath)
{
	private ModuleDefMD? Module { get; set; }
	
	private List<TypeDef>? CandidateTypes { get; set; }

	private static List<string> _tokens = DataProvider.Settings!.TokensToMatch;
	
	public void AutoMatch(string assemblyPath, string oldTypeName, string newTypeName)
	{
		assemblyPath = AssemblyUtils.TryDeObfuscate(
			DataProvider.LoadModule(assemblyPath), 
			assemblyPath, 
			out var module);

		Module = module;
		CandidateTypes = Module.GetTypes()
			.Where(t => _tokens.Any(token => t.Name.StartsWith(token)))
			// .Where(t => t.Name != oldTypeName)
			.ToList();
		
		var targetTypeDef = FindTargetType(oldTypeName);

		if (targetTypeDef is null)
		{
			Logger.LogSync($"Could not target type: {oldTypeName}", ConsoleColor.Red);
			return;
		}
		
		Logger.LogSync($"Found target type: {targetTypeDef!.FullName}", ConsoleColor.Green);
		
		var remapModel = new RemapModel();
		remapModel.NewTypeName = newTypeName;
		
		StartFilter(targetTypeDef, remapModel, assemblyPath);
	}

	private TypeDef? FindTargetType(string oldTypeName)
	{
		return Module!.GetTypes().FirstOrDefault(t => t.FullName == oldTypeName);
	}
	
	private void StartFilter(TypeDef target, RemapModel remapModel, string assemblyPath)
	{
		Logger.LogSync($"Starting Candidates: {CandidateTypes!.Count}", ConsoleColor.Yellow);
		
		// Purpose of this pass is to eliminate any types that have no matching parameters
		foreach (var candidate in CandidateTypes!.ToList())
		{
			if (!PassesGeneralChecks(target, candidate, remapModel.SearchParams.GenericParams))
			{
				CandidateTypes!.Remove(candidate);
				continue;
			}
			
			if (!ContainsTargetMethods(target, candidate, remapModel.SearchParams.Methods))
			{
				CandidateTypes!.Remove(candidate);
				continue;
			}
			
			if (!ContainsTargetFields(target, candidate, remapModel.SearchParams.Fields))
			{
				CandidateTypes!.Remove(candidate);
				continue;
			}
			
			if (!ContainsTargetProperties(target, candidate, remapModel.SearchParams.Properties))
			{
				CandidateTypes!.Remove(candidate);
				continue;
			}

			if (!ContainsTargetNestedTypes(target, candidate, remapModel.SearchParams.NestedTypes))
			{
				CandidateTypes!.Remove(candidate);
				continue;
			}
			
			if (!ContainsTargetEvents(target, candidate, remapModel.SearchParams.Events))
			{
				CandidateTypes!.Remove(candidate);
			}
		}
		
		if (CandidateTypes!.Count == 1)
		{
			Logger.LogSync("Narrowed candidates down to one. Testing generated model...", ConsoleColor.Green);

			var tmpList = new List<RemapModel>()
			{
				remapModel
			};
			
			new ReMapper().InitializeRemap(tmpList, assemblyPath, validate: true);

			ProcessEndQuestions(remapModel, assemblyPath);
			return;
		}
		
		Logger.LogSync("Could not find a match... :(", ConsoleColor.Red);
	}

	private bool PassesGeneralChecks(TypeDef target, TypeDef candidate, GenericParams parms)
	{
		if (target.IsPublic != candidate.IsPublic) return false;
		if (target.IsAbstract != candidate.IsAbstract) return false;
		if (target.IsInterface != candidate.IsInterface) return false;
		if (target.IsEnum != candidate.IsEnum) return false;
		if (target.IsValueType != candidate.IsValueType) return false;
		if (target.HasGenericParameters != candidate.HasGenericParameters) return false;
		if (target.IsNested != candidate.IsNested) return false;
		if (target.IsSealed != candidate.IsSealed) return false;
		if (target.HasCustomAttributes != candidate.HasCustomAttributes) return false;
		
		parms.IsPublic = target.IsPublic;
		parms.IsAbstract = target.IsAbstract;
		parms.IsInterface = target.IsInterface;
		parms.IsEnum = target.IsEnum;
		parms.IsStruct = target.IsValueType && !target.IsEnum;
		parms.HasGenericParameters = target.HasGenericParameters;
		parms.IsSealed = target.IsSealed;
		parms.HasAttribute = target.HasCustomAttributes;
		parms.IsDerived = target.BaseType != null && target.BaseType.Name != "Object";

		if ((bool)parms.IsDerived)
		{
			parms.MatchBaseClass = target.BaseType?.Name.String;
		}
		
		return true;
	}
	
	private bool ContainsTargetMethods(TypeDef target, TypeDef candidate, MethodParams methods)
	{
		// Target has no methods and type has no methods
		if (!target.Methods.Any() && !candidate.Methods.Any())
		{
			methods.MethodCount = 0;
			return true;
		}
		
		// Target has no methods but type has methods
		if (!target.Methods.Any() && candidate.Methods.Any()) return false;
		
		// Target has methods but type has no methods
		if (target.Methods.Any() && !candidate.Methods.Any()) return false;
		
		// Target has a different number of methods
		if (target.Methods.Count != candidate.Methods.Count) return false;
		
		var commonMethods = target.Methods
			.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
			.Select(s => s.Name)
			.Intersect(candidate.Methods
				.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
				.Select(s => s.Name));
		
		// Methods in target that are not in candidate
		var includeMethods = target.Methods
			.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
			.Select(s => s.Name.ToString())
			.Except(candidate.Methods
				.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
				.Select(s => s.Name.ToString()));
		
		// Methods in candidate that are not in target
		var excludeMethods = candidate.Methods
			.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
			.Select(s => s.Name.ToString())
			.Except(target.Methods
				.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter)
				.Select(s => s.Name.ToString()));
		
		foreach (var include in includeMethods)
		{
			methods.IncludeMethods.Add(include);
		}

		foreach (var exclude in excludeMethods)
		{
			methods.ExcludeMethods.Add(exclude);
		}
		
		methods.MethodCount = target.Methods
			.Count(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter && !m.IsSpecialName);

		if (target.Methods.Any(m => m.IsConstructor && m.Parameters.Count > 0))
		{
			methods.ConstructorParameterCount = target.Methods.First(m => m.IsConstructor && m.Parameters.Count > 0).Parameters.Count - 1;
		}
		
		return commonMethods.Any();
	}
	
	private bool ContainsTargetFields(TypeDef target, TypeDef candidate, FieldParams fields)
	{
		// Target has no fields and type has no fields
		if (!target.Fields.Any() && !candidate.Fields.Any())
		{
			fields.FieldCount = 0;
			return true;
		}
		
		// Target has fields but type has no fields
		if (target.Fields.Any() && !candidate.Fields.Any()) return false;
		
		// Target has a different number of fields
		if (target.Fields.Count != candidate.Fields.Count) return false;

		var commonFields = target.Fields
			.Select(s => s.Name)
			.Intersect(candidate.Fields.Select(s => s.Name));
		
		// Methods in target that are not in candidate
		var includeFields = target.Fields
			.Select(s => s.Name.ToString())
			.Except(candidate.Fields.Select(s => s.Name.ToString()));
		
		// Methods in candidate that are not in target
		var excludeFields = candidate.Fields
			.Select(s => s.Name.ToString())
			.Except(target.Fields.Select(s => s.Name.ToString()));
		
		foreach (var include in includeFields)
		{
			fields.IncludeFields.Add(include);
		}

		foreach (var exclude in excludeFields)
		{
			fields.ExcludeFields.Add(exclude);
		}
		
		fields.FieldCount = target.Fields.Count;
		
		return commonFields.Any();
	}
	
	private bool ContainsTargetProperties(TypeDef target, TypeDef candidate, PropertyParams props)
	{
		// Both target and candidate don't have properties
		if (!target.Properties.Any() && !candidate.Properties.Any())
		{
			props.PropertyCount = 0;
			return true;
		}
		
		// Target has props but type has no props
		if (target.Properties.Any() && !candidate.Properties.Any()) return false;
		
		// Target has a different number of props
		if (target.Properties.Count != candidate.Properties.Count) return false;
		
		var commonProps = target.Properties
			.Select(s => s.Name)
			.Intersect(candidate.Properties.Select(s => s.Name));
		
		// Props in target that are not in candidate
		var includeProps = target.Properties
			.Select(s => s.Name.ToString())
			.Except(candidate.Properties.Select(s => s.Name.ToString()));
		
		// Props in candidate that are not in target
		var excludeProps = candidate.Properties
			.Select(s => s.Name.ToString())
			.Except(target.Properties.Select(s => s.Name.ToString()));
		
		foreach (var include in includeProps)
		{
			props.IncludeProperties.Add(include);
		}

		foreach (var exclude in excludeProps)
		{
			props.ExcludeProperties.Add(exclude);
		}
		
		props.PropertyCount = target.Properties.Count;
		
		return commonProps.Any();
	}

	private bool ContainsTargetNestedTypes(TypeDef target, TypeDef candidate, NestedTypeParams nt)
	{
		// Target has no nt's but type has nt's
		if (!target.NestedTypes.Any() && candidate.NestedTypes.Any())
		{
			nt.NestedTypeCount = 0;
			return false;
		}
		
		// Target has nt's but type has no nt's
		if (target.NestedTypes.Any() && !candidate.NestedTypes.Any()) return false;
		
		// Target has a different number of nt's
		if (target.NestedTypes.Count != candidate.NestedTypes.Count) return false;
		
		var commonNts = target.NestedTypes
			.Select(s => s.Name)
			.Intersect(candidate.NestedTypes.Select(s => s.Name));
		
		var includeNts = target.NestedTypes
			.Select(s => s.Name.ToString())
			.Except(candidate.NestedTypes.Select(s => s.Name.ToString()));
		
		var excludeNts = candidate.NestedTypes
			.Select(s => s.Name.ToString())
			.Except(target.NestedTypes.Select(s => s.Name.ToString()));
		
		foreach (var include in includeNts)
		{
			nt.IncludeNestedTypes.Add(include);
		}
		
		foreach (var exclude in excludeNts)
		{
			nt.ExcludeNestedTypes.Add(exclude);
		}
		
		nt.NestedTypeCount = target.NestedTypes.Count;
		nt.IsNested = target.IsNested;

		if (target.DeclaringType is not null)
		{
			nt.NestedTypeParentName = target.DeclaringType.Name.String;
		}
		
		return commonNts.Any() || !target.IsNested;
	}
	
	private bool ContainsTargetEvents(TypeDef target, TypeDef candidate, EventParams events)
	{
		// Target has no events but type has events
		if (!target.Events.Any() && candidate.Events.Any())
		{
			events.EventCount = 0;
			return false;
		}
		
		// Target has events but type has no events
		if (target.Events.Any() && !candidate.Events.Any()) return false;
		
		// Target has a different number of events
		if (target.Events.Count != candidate.Events.Count) return false;
		
		var commonEvents = target.Events
			.Select(s => s.Name)
			.Intersect(candidate.Events.Select(s => s.Name));
		
		var includeEvents = target.Events
			.Select(s => s.Name.ToString())
			.Except(candidate.Events.Select(s => s.Name.ToString()));
		
		var excludeEvents = candidate.Events
			.Select(s => s.Name.ToString())
			.Except(target.Events.Select(s => s.Name.ToString()));
		
		foreach (var include in includeEvents)
		{
			events.IncludeEvents.Add(include);
		}
		
		foreach (var exclude in excludeEvents)
		{
			events.ExcludeEvents.Add(exclude);
		}
		
		events.EventCount = target.NestedTypes.Count;
		
		return commonEvents.Any() || target.Events.Count == 0;
	}
	
	private void ProcessEndQuestions(RemapModel remapModel, string assemblyPath)
	{
		Thread.Sleep(1000);
		
		Logger.LogSync("Add remap to existing list?.. (y/n)", ConsoleColor.Yellow);
		var resp = Console.ReadLine();

		if (resp == "y" || resp == "yes" || resp == "Y")
		{
			if (mappings.Count == 0)
			{
				Logger.LogSync("No remaps loaded. Please restart with a provided mapping path.", ConsoleColor.Red);
				return;
			}

			if (mappings.Any(m => m.NewTypeName == remapModel.NewTypeName))
			{
				Logger.LogSync($"Ambiguous new type names found for {remapModel.NewTypeName}. Please pick a different name.", ConsoleColor.Red);
				return;
			}
			
			remapModel.AutoGenerated = true;
			
			mappings.Add(remapModel);
			DataProvider.UpdateMapping(mappingPath, mappings);
		}
		
		Logger.LogSync("Would you like to run the remap process?... (y/n)", ConsoleColor.Yellow);
		var resp2 = Console.ReadLine();
		
		if (resp2 == "y" || resp2 == "yes" || resp2 == "Y")
		{
			var outPath = Path.GetDirectoryName(assemblyPath);
			new ReMapper().InitializeRemap(mappings, assemblyPath, outPath);
		}
	}
}