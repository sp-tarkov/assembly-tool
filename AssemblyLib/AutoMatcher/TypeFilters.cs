using AsmResolver.DotNet;
using AssemblyLib.AutoMatcher.Filters;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.Filters;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher;

[Injectable]
public class TypeFilters(
    GeneralFilters generalFilters,
    MethodFilters methodFilters,
    FieldFilters fieldFilters,
    PropertyFilters propertyFilters,
    EventFilters eventFilters,
    NestedFilters nestedFilters,
    List<TypeDefinition> types
    )
{
    public void Filter(TypeDefinition target, TypeDefinition candidate, RemapModel remapModel)
    {
        if (!generalFilters.Filter(target, candidate, remapModel.SearchParams.GenericParams))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after general checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }

        if (!methodFilters.Filter(target, candidate, remapModel.SearchParams.Methods))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after method checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!fieldFilters.Filter(target, candidate, remapModel.SearchParams.Fields))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after field checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!propertyFilters.Filter(target, candidate, remapModel.SearchParams.Properties))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after property checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }

        if (!nestedFilters.Filter(target, candidate, remapModel.SearchParams.NestedTypes))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after nested checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!eventFilters.Filter(target, candidate, remapModel.SearchParams.Events))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after event checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
        }
    }
}