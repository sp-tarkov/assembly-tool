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

        var typesToFilter = typeCache.SelectCache(mapping);
        if (typesToFilter.Count == 0)
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
}
