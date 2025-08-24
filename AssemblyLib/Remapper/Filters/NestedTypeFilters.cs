using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class NestedTypeFilters
{
    /// <summary>
    /// Filters based on nested type includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByInclude(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
    {
        if (parms.NestedTypes.IncludeNestedTypes.Count == 0)
            return types;

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

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on nested type excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByExclude(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
    {
        if (parms.NestedTypes.ExcludeNestedTypes.Count == 0)
            return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Fields.Where(field =>
                parms.NestedTypes.ExcludeNestedTypes.Contains(field.Name!)
            );

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
    public IEnumerable<TypeDefinition> FilterByCount(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
    {
        if (parms.NestedTypes.NestedTypeCount >= 0)
        {
            types = types.Where(t => t.NestedTypes.Count == parms.NestedTypes.NestedTypeCount);
        }

        return types;
    }

    public IEnumerable<TypeDefinition> FilterByNestedVisibility(
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

    private IEnumerable<TypeDefinition> FilterNestedByName(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
    {
        if (parms.NestedTypes.NestedTypeParentName is not "")
        {
            types = types.Where(t =>
                t.DeclaringType!.Name == parms.NestedTypes.NestedTypeParentName
            );
        }

        return types;
    }
}
