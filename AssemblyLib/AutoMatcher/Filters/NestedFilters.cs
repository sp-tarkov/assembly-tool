using AsmResolver.DotNet;
using AssemblyLib.Models;

namespace AssemblyLib.AutoMatcher.Filters;

public class NestedFilters
{
    public bool Filter(TypeDefinition target, TypeDefinition candidate, NestedTypeParams nt)
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
            .Select(s => s.Name!.ToString())
            .Except(candidate.NestedTypes.Select(s => s.Name!.ToString()));
		
        var excludeNts = candidate.NestedTypes
            .Select(s => s.Name!.ToString())
            .Except(target.NestedTypes.Select(s => s.Name!.ToString()));
		
        nt.IncludeNestedTypes.UnionWith(includeNts);
        nt.ExcludeNestedTypes.UnionWith(excludeNts);
		
        nt.NestedTypeCount = target.NestedTypes.Count;
        nt.IsNested = target.IsNested;
        nt.IsNestedAssembly = target.IsNestedAssembly;
        nt.IsNestedFamily = target.IsNestedFamily;
        nt.IsNestedPrivate = target.IsNestedPrivate;
        nt.IsNestedPublic = target.IsNestedPublic;
        nt.IsNestedFamilyAndAssembly = target.IsNestedFamilyAndAssembly;
        nt.IsNestedFamilyOrAssembly = target.IsNestedFamilyOrAssembly;
		
        if (target.DeclaringType is not null)
        {
            nt.NestedTypeParentName = target.DeclaringType.Name!;
        }
		
        return commonNts.Any() || target.NestedTypes.Count == 0;
    }
}