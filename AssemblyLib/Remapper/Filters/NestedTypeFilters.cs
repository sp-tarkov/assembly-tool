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
    
    public static IEnumerable<TypeDef> FilterByNestedVisibility(IEnumerable<TypeDef> types, SearchParams parms)
    {
        types = FilterNestedByName(types, parms);

        var ntp = parms.NestedTypes;
        
        types = types.Where(t => 
            t.IsNestedAssembly == ntp.IsNestedAssembly
            && t.IsNestedFamily == ntp.IsNestedFamily
            && t.IsNestedPrivate == ntp.IsNestedPrivate
            && t.IsNestedPublic == ntp.IsNestedPublic
            && t.IsNestedFamilyAndAssembly == ntp.IsNestedFamilyAndAssembly
            && t.IsNestedFamilyOrAssembly == ntp.IsNestedFamilyOrAssembly
            );
        
        return types;
    }
    
    private static IEnumerable<TypeDef> FilterNestedByName(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.NestedTypes.NestedTypeParentName is not "")
        {
            types = types.Where(t => t.DeclaringType.Name.String == parms.NestedTypes.NestedTypeParentName);
        }

        return types;
    }
}