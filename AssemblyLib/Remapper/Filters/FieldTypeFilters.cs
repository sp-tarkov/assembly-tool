using AssemblyLib.Models;
using dnlib.DotNet;

namespace AssemblyLib.ReMapper.Filters;

internal static class FieldTypeFilters
{
    /// <summary>
    /// Filters based on field name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByInclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Fields.IncludeFields.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Fields.IncludeFields
                .All(includeName => type.Fields
                    .Any(field => field.Name.String == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on field name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByExclude(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Fields.ExcludeFields.Count == 0) return types;

        List<TypeDef> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Fields
                .Where(field => parms.Fields.ExcludeFields.Contains(field.Name.String));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on method count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByCount(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.Fields.FieldCount == -1) return types;

        if (parms.Fields.FieldCount >= 0)
        {
            types = types.Where(t => t.Fields.Count == parms.Fields.FieldCount);
        }

        return types;
    }
}