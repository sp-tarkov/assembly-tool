using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable(TypePriority = 5)]
public sealed class NestedTypeFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out IEnumerable<TypeDefinition>? filteredTypes
    )
    {
        types = FilterByCount(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.NestedTypeCount);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = null;
            return false;
        }

        types = FilterByNestedVisibility(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.NestedVisibility);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = null;
            return false;
        }

        types = FilterByInclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.NestedTypeInclude);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = null;
            return false;
        }

        types = FilterByExclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.NestedTypeExclude);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = null;
            return false;
        }

        filteredTypes = types;
        return true;
    }

    /// <summary>
    /// Filters based on nested type includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.NestedTypes.IncludeNestedTypes.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (
                parms.NestedTypes.IncludeNestedTypes.All(includeName =>
                    type.NestedTypes.Any(nestedType => nestedType.Name == includeName)
                )
            )
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on nested type excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.NestedTypes.ExcludeNestedTypes.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Fields.Where(field => parms.NestedTypes.ExcludeNestedTypes.Contains(field.Name!));

            if (!match.Any())
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on nested type count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.NestedTypes.NestedTypeCount >= 0)
        {
            types = types.Where(t => t.NestedTypes.Count == parms.NestedTypes.NestedTypeCount);
        }

        return types;
    }

    private static IEnumerable<TypeDefinition> FilterByNestedVisibility(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
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

    private static IEnumerable<TypeDefinition> FilterNestedByName(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.NestedTypes.NestedTypeParentName is not "")
        {
            types = types.Where(t => t.DeclaringType!.Name == parms.NestedTypes.NestedTypeParentName);
        }

        return types;
    }
}
