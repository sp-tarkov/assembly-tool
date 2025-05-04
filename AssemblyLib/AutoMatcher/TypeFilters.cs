using AsmResolver.DotNet;
using AssemblyLib.AutoMatcher.Filters;
using AssemblyLib.Models;
using AssemblyLib.Utils;

namespace AssemblyLib.AutoMatcher;

public class TypeFilters(List<TypeDefinition> types)
{
    private readonly GeneralFilters _generalFilters = new();
    private readonly MethodFilters _methodFilters = new();
    private readonly FieldFilters _fieldFilters = new();
    private readonly PropertyFilters _propertyFilters = new();
    private readonly NestedFilters _nestedFilters = new();
    private readonly EventFilters _eventFilters = new();
    
    public void Filter(TypeDefinition target, TypeDefinition candidate, RemapModel remapModel)
    {
        if (!_generalFilters.Filter(target, candidate, remapModel.SearchParams.GenericParams))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after general checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }

        if (!_methodFilters.Filter(target, candidate, remapModel.SearchParams.Methods))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after method checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!_fieldFilters.Filter(target, candidate, remapModel.SearchParams.Fields))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after field checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!_propertyFilters.Filter(target, candidate, remapModel.SearchParams.Properties))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after property checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }

        if (!_nestedFilters.Filter(target, candidate, remapModel.SearchParams.NestedTypes))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after nested checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
            return;
        }
			
        if (!_eventFilters.Filter(target, candidate, remapModel.SearchParams.Events))
        {
            Logger.Log($"Candidate: {candidate.Name} filtered out after event checks", ConsoleColor.Yellow, true);
            types.Remove(candidate);
        }
    }
}