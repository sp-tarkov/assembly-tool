using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class MethodTypeFilters
{
    /// <summary>
    /// Filters based on method includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.IncludeMethods.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Methods.IncludeMethods
                .All(includeName => type.Methods
                    .Any(method => method.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Any() ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on method excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.ExcludeMethods.Count == 0) return types;

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Methods
                .Where(method => parms.Methods.ExcludeMethods.Contains(method.Name!));

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
    public IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.MethodCount == -1) return types;

        if (parms.Methods.MethodCount >= 0)
        {
            types = types.Where(t => GetMethodCountExcludingConstructors(t) == parms.Methods.MethodCount);
        }

        return types;
    }

    /// <summary>
    /// We don't want the constructors included in the count
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static int GetMethodCountExcludingConstructors(TypeDefinition type)
    {
        int count = 0;
        foreach (var method in type.Methods)
        {
            if (method is { IsConstructor: false, IsSpecialName: false, IsGetMethod: false, IsSetMethod: false })
            {
                count++;
            }
        }
        return count;
    }
}