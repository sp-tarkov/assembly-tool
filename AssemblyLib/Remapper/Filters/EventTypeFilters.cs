﻿using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class EventTypeFilters
{
    /// <summary>
    /// Filters based on events name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Events.IncludeEvents.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Events.IncludeEvents
                .All(includeName => type.Events
                    .Any(ev => ev.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on events name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Events.ExcludeEvents.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Events
                .Where(prop => parms.Events.ExcludeEvents.Contains(prop.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }
}
