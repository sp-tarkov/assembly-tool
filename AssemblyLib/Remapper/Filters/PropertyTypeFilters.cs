using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class PropertyTypeFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out List<TypeDefinition>? filteredTypes
    )
    {
        var internFilteredTypes = FilterByCount(types, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.PropertiesCount);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByInclude(types, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.PropertiesInclude);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByExclude(types, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.PropertiesExclude);
            filteredTypes = null;
            return false;
        }

        filteredTypes = internFilteredTypes.ToList();
        return true;
    }

    /// <summary>
    /// Filters based on property includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Properties.IncludeProperties.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (
                parms.Properties.IncludeProperties.All(includeName =>
                    type.Properties.Any(prop => prop.Name == includeName)
                )
            )
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on property excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Properties.ExcludeProperties.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Properties.Where(prop => parms.Properties.ExcludeProperties.Contains(prop.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on property count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        // Param is disabled
        if (parms.Properties.PropertyCount == -1)
        {
            return types;
        }

        if (parms.Properties.PropertyCount >= 0)
        {
            types = types.Where(t => t.Properties.Count == parms.Properties.PropertyCount);
        }

        return types;
    }
}
