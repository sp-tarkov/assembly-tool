using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public sealed class FieldTypeFilters : IRemapFilter
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
            remapModel.NoMatchReasons.Add(ENoMatchReason.FieldsCount);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByInclude(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.FieldsInclude);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByExclude(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.FieldsExclude);
            filteredTypes = null;
            return false;
        }

        filteredTypes = internFilteredTypes.ToList();
        return true;
    }

    /// <summary>
    /// Filters based on field name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Fields.IncludeFields.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Fields.IncludeFields.All(includeName => type.Fields.Any(field => field.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on field name
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Fields.ExcludeFields.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Fields.Where(field => parms.Fields.ExcludeFields.Contains(field.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on method count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Fields.FieldCount == -1)
        {
            return types;
        }

        if (parms.Fields.FieldCount >= 0)
        {
            types = types.Where(t => t.Fields.Count == parms.Fields.FieldCount);
        }

        return types;
    }
}
