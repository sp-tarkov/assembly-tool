using dnlib.DotNet;
using ReCodeItLib.Models;

namespace ReCodeItLib.ReMapper.Filters;

internal static class NestedTypeFilters
{
    /// <summary>
    /// Filters based on nested type includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByInclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.NestedTypes.IncludeNestedTypes.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.NestedTypes.IncludeNestedTypes
                .All(includeName => type.NestedTypes
                    .Any(nestedType => nestedType.Name.String == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on nested type excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByExclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.NestedTypes.ExcludeNestedTypes.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Fields
                .Where(field => parms.NestedTypes.ExcludeNestedTypes.Contains(field.Name.String));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on nested type count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByCount(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.NestedTypes.NestedTypeCount >= 0)
        {
            types = types.Where(t => t.NestedTypes.Count == parms.NestedTypes.NestedTypeCount);
        }

        return types;
    }
}