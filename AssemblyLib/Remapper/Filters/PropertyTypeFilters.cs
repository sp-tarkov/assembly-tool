using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class PropertyTypeFilters
{
    /// <summary>
    /// Filters based on property includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Properties.IncludeProperties.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Properties.IncludeProperties
                .All(includeName => type.Properties
                    .Any(prop => prop.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on property excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Properties.ExcludeProperties.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Properties
                .Where(prop => parms.Properties.ExcludeProperties.Contains(prop.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on property count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Properties.PropertyCount == -1) return types;

        if (parms.Properties.PropertyCount >= 0)
        {
            types = types.Where(t => t.Properties.Count == parms.Properties.PropertyCount);
        }

        return types;
    }
}