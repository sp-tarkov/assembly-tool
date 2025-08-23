using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class NestedFilters : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParams)
    {
        if (filterParams is not NestedTypeParams nestedParams)
        {
            throw new FilterException("FilterParams in NestedFilters is not NestedTypeParams or is null");
        }
        
        // Target has no nt's but type has nt's
        if (!target.NestedTypes.Any() && candidate.NestedTypes.Any())
        {
            nestedParams.NestedTypeCount = 0;
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
            .Select(s => s.Name!.ToString())
            .Except(candidate.NestedTypes.Select(s => s.Name!.ToString()));
		
        var excludeNts = candidate.NestedTypes
            .Select(s => s.Name!.ToString())
            .Except(target.NestedTypes.Select(s => s.Name!.ToString()));
		
        nestedParams.IncludeNestedTypes.UnionWith(includeNts);
        nestedParams.ExcludeNestedTypes.UnionWith(excludeNts);
		
        nestedParams.NestedTypeCount = target.NestedTypes.Count;
        nestedParams.IsNested = target.IsNested;
        nestedParams.IsNestedAssembly = target.IsNestedAssembly;
        nestedParams.IsNestedFamily = target.IsNestedFamily;
        nestedParams.IsNestedPrivate = target.IsNestedPrivate;
        nestedParams.IsNestedPublic = target.IsNestedPublic;
        nestedParams.IsNestedFamilyAndAssembly = target.IsNestedFamilyAndAssembly;
        nestedParams.IsNestedFamilyOrAssembly = target.IsNestedFamilyOrAssembly;
		
        if (target.DeclaringType is not null)
        {
            nestedParams.NestedTypeParentName = target.DeclaringType.Name!;
        }
		
        return commonNts.Any() || target.NestedTypes.Count == 0;
    }
}