using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.Filters;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Remapper;

[Injectable(InjectionType.Singleton)]
public sealed class FilterService(IEnumerable<IRemapFilter> filters, TypeCache typeCache)
{
    public void FilterRemap(RemapModel mapping)
    {
        // Don't run filters on forced names (Dynamic remaps)
        if (mapping.UseForceRename)
        {
            return;
        }

        var typesToFilter = GetCacheForMapping(mapping);
        if (typesToFilter is null || typesToFilter.Count == 0)
        {
            mapping.FailureReasons.Add("No cache available for remap");
            return;
        }

        var remainingTypePool = typesToFilter;
        foreach (var filter in filters)
        {
            if (!filter.Filter(remainingTypePool, mapping, out var filteredTypes))
            {
                return;
            }

            remainingTypePool = filteredTypes.ToList();
        }

        if (remainingTypePool.Count == 0)
        {
            return;
        }

        mapping.TypeCandidates.UnionWith(remainingTypePool);
    }

    private List<TypeDefinition>? GetCacheForMapping(RemapModel mapping)
    {
        var genericParams = mapping.SearchParams.GenericParams;
        var nestedParams = mapping.SearchParams.NestedTypes;

        if (genericParams.IsEnum)
        {
            return typeCache.Enums;
        }

        if (genericParams.IsInterface)
        {
            return typeCache.Interfaces;
        }

        if (genericParams.IsAbstract)
        {
            return typeCache.AbstractClasses;
        }

        if (genericParams.IsSealed)
        {
            return typeCache.SealedClasses;
        }

        // Not abstract or sealed
        if (!genericParams.IsAbstract && !genericParams.IsSealed)
        {
            // Additionally check for nested
            return nestedParams.IsNested ? typeCache.NestedClasses : typeCache.Classes;
        }

        Log.Error(
            "Could not find cache for remap: {remapName}. This is a code issue, not a remap issue, skipping remap",
            mapping.NewTypeName
        );
        return [];
    }
}
