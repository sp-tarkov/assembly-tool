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
            // This should NEVER be hit.
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

        // IMPORTANT: always return combined conditionals before we look for individual ones
        // Otherwise cases such as static can be missed

        /* Type             IL designation
         * Interface:       .class interface public auto ansi abstract beforefieldinit
         * Class:           .class public auto ansi beforefieldinit
         * Abstract Class:  .class public auto ansi abstract beforefieldinit
         * Sealed Class:    .class public auto ansi sealed beforefieldinit
         * Static Class:    .class public auto ansi abstract sealed beforefieldinit
         */

        switch (genericParams.IsAbstract)
        {
            // Abstract and sealed (static classes)
            case true when genericParams.IsSealed:
                return typeCache.StaticClasses;

            // Normal abstract classes
            // Interfaces are also considered abstract, ignore those.
            case true when !genericParams.IsInterface:
                return typeCache.AbstractClasses;

            // Not abstract or sealed (normal class)
            case false when !genericParams.IsSealed:
                // Additionally check for nested
                return nestedParams.IsNested ? typeCache.NestedClasses : typeCache.Classes;
        }

        if (genericParams.IsSealed)
        {
            return typeCache.SealedClasses;
        }

        if (genericParams.IsInterface)
        {
            return typeCache.Interfaces;
        }

        if (genericParams.IsEnum)
        {
            return typeCache.Enums;
        }

        Log.Error(
            "Could not find cache for remap: {remapName}. This is a code issue, not a remap issue, skipping remap",
            mapping.NewTypeName
        );
        return [];
    }
}
