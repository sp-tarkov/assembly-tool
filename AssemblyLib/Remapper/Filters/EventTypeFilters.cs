using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable(TypePriority = 4)]
public sealed class EventTypeFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out IEnumerable<TypeDefinition>? filteredTypes
    )
    {
        types = FilterByInclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.EventsInclude);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = FilterByExclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.EventsExclude);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        filteredTypes = types;
        return true;
    }

    /// <summary>
    /// Filters based on events name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Events.IncludeEvents.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Events.IncludeEvents.All(includeName => type.Events.Any(ev => ev.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on events name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Events.ExcludeEvents.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Events.Where(prop => parms.Events.ExcludeEvents.Contains(prop.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }
}
