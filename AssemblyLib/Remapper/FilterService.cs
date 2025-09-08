using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Remapper.Filters;
using AssemblyLib.Shared;
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

        // Order here is very important do NOT change it, Otherwise cases such as static can be missed

        /* Type             IL designation
         * Interface:       .class interface public auto ansi abstract beforefieldinit
         * Class:           .class public auto ansi beforefieldinit
         * Abstract Class:  .class public auto ansi abstract beforefieldinit
         * Sealed Class:    .class public auto ansi sealed beforefieldinit
         * Static Class:    .class public auto ansi abstract sealed beforefieldinit
         * Structs:         .class public sequential ansi sealed beforefieldinit [Name] extends [System.Runtime]System.ValueType
         */

        // Abstract and sealed = static
        if (genericParams.IsStatic)
        {
            Log.Information("Static class cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.StaticClasses;
        }

        if (genericParams.IsStruct ?? false)
        {
            if (nestedParams.IsNested)
            {
                Log.Information("Nested struct cache chosen for remap: {newTypeName}", mapping.NewTypeName);
                return typeCache.NestedStructs;
            }

            Log.Information("Struct cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.Structs;
        }

        // Interface - considered abstract so check it before abstract
        if (genericParams.IsInterface)
        {
            Log.Information("Interface cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.Interfaces;
        }

        if (genericParams.IsAbstract)
        {
            Log.Information("Abstract class cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.AbstractClasses;
        }

        if (genericParams.IsSealed)
        {
            Log.Information("Sealed class cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.SealedClasses;
        }

        // Enums are never obfuscated but im putting this here anyway just in-case
        if (genericParams.IsEnum)
        {
            Log.Information("Enum cache chosen for remap: {newTypeName}", mapping.NewTypeName);
            return typeCache.Enums;
        }

        // Last thing to consider is if its nested or not
        switch (nestedParams.IsNested)
        {
            case true:
                Log.Information("Nested class cache chosen for remap: {newTypeName}", mapping.NewTypeName);
                return typeCache.NestedClasses;
            case false:
                Log.Information("Nested class cache chosen for remap: {newTypeName}", mapping.NewTypeName);
                return typeCache.Classes;
        }
    }
}
