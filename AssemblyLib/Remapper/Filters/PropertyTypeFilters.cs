using AssemblyLib.Models;
using dnlib.DotNet;

namespace AssemblyLib.ReMapper.Filters;

internal static class PropertyTypeFilters
{
    /// <summary>
    /// Filters based on property includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByInclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Properties.IncludeProperties.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Properties.IncludeProperties
                .All(includeName => type.Properties
                    .Any(prop => prop.Name.String == includeName)))
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
    public static IEnumerable<TypeDef> FilterByExclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Properties.ExcludeProperties.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Properties
                .Where(prop => parms.Properties.ExcludeProperties.Contains(prop.Name.String));

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
    public static IEnumerable<TypeDef> FilterByCount(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Properties.PropertyCount == -1) return types;

        if (parms.Properties.PropertyCount >= 0)
        {
            types = types.Where(t => t.Properties.Count == parms.Properties.PropertyCount);
        }

        return types;
    }
}