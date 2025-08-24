using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class NestedFilters : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams)
    {
        // Target has no nt's but type has nt's
        if (!target.NestedTypes.Any() && candidate.NestedTypes.Any())
        {
            searchParams.NestedTypes.NestedTypeCount = 0;
            return false;
        }
		
        // Target has nt's but type has no nt's
        if (target.NestedTypes.Any() && !candidate.NestedTypes.Any())
        {
            return LogFailure($"`{candidate.FullName}` filtered out during NestedFilters: Target has nested types but candidate does not");
        }
		
        // Target has a different number of nt's
        if (target.NestedTypes.Count != candidate.NestedTypes.Count)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during NestedFilters: Target has different number of nested types");
        }
		
        var commonNts = target.NestedTypes
            .Select(s => s.Name)
            .Intersect(candidate.NestedTypes.Select(s => s.Name));
		
        var includeNts = target.NestedTypes
            .Select(s => s.Name!.ToString())
            .Except(candidate.NestedTypes.Select(s => s.Name!.ToString()));
		
        var excludeNts = candidate.NestedTypes
            .Select(s => s.Name!.ToString())
            .Except(target.NestedTypes.Select(s => s.Name!.ToString()));
		
        searchParams.NestedTypes.IncludeNestedTypes.UnionWith(includeNts);
        searchParams.NestedTypes.ExcludeNestedTypes.UnionWith(excludeNts);
		
        searchParams.NestedTypes.NestedTypeCount = target.NestedTypes.Count;
        searchParams.NestedTypes.IsNested = target.IsNested;
        searchParams.NestedTypes.IsNestedAssembly = target.IsNestedAssembly;
        searchParams.NestedTypes.IsNestedFamily = target.IsNestedFamily;
        searchParams.NestedTypes.IsNestedPrivate = target.IsNestedPrivate;
        searchParams.NestedTypes.IsNestedPublic = target.IsNestedPublic;
        searchParams.NestedTypes.IsNestedFamilyAndAssembly = target.IsNestedFamilyAndAssembly;
        searchParams.NestedTypes.IsNestedFamilyOrAssembly = target.IsNestedFamilyOrAssembly;
		
        if (target.DeclaringType is not null)
        {
            searchParams.NestedTypes.NestedTypeParentName = target.DeclaringType.Name!;
        }
		
        return commonNts.Any() || target.NestedTypes.Count == 0;
    }
}