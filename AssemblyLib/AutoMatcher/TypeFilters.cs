using AsmResolver.DotNet;
using AssemblyLib.AutoMatcher.Filters;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.Filters;
using AssemblyLib.Utils;
using Serilog;
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
            Log.Debug("Candidate: {CandidateName} filtered out after general checks", candidate.Name);
            types.Remove(candidate);
            return;
        }

        if (!methodFilters.Filter(target, candidate, remapModel.SearchParams.Methods))
        {
            Log.Debug("Candidate: {CandidateName} filtered out after method checks", candidate.Name);
            types.Remove(candidate);
            return;
        }
			
        if (!fieldFilters.Filter(target, candidate, remapModel.SearchParams.Fields))
        {
            Log.Debug("Candidate: {CandidateName} filtered out after field checks", candidate.Name);
            types.Remove(candidate);
            return;
        }
			
        if (!propertyFilters.Filter(target, candidate, remapModel.SearchParams.Properties))
        {
            Log.Debug("Candidate: {CandidateName} filtered out after property checks", candidate.Name);
            types.Remove(candidate);
            return;
        }

        if (!nestedFilters.Filter(target, candidate, remapModel.SearchParams.NestedTypes))
        {
            Log.Debug("Candidate: {CandidateName} filtered out after nested checks", candidate.Name);
            types.Remove(candidate);
            return;
        }
			
        if (!eventFilters.Filter(target, candidate, remapModel.SearchParams.Events))
        {
            Log.Debug("Candidate: {CandidateName} filtered out after event checks", candidate.Name);
            types.Remove(candidate);
        }
    }
}