using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class MethodFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out List<TypeDefinition>? filteredTypes
    )
    {
        var internFilteredTypes = FilterByInclude(types, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.MethodsInclude);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByExclude(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.MethodsExclude);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByCtorParameterCount(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.ConstructorParameterCount);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterByCount(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.MethodsCount);
            filteredTypes = null;
            return false;
        }

        filteredTypes = internFilteredTypes.ToList();
        return true;
    }

    /// <summary>
    /// Filters based on method includes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByInclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.IncludeMethods.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            if (parms.Methods.IncludeMethods.All(includeName => type.Methods.Any(method => method.Name == includeName)))
            {
                filteredTypes.Add(type);
            }
        }

        return filteredTypes.Count != 0 ? filteredTypes : types;
    }

    /// <summary>
    /// Filters based on method excludes
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByExclude(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.ExcludeMethods.Count == 0)
        {
            return types;
        }

        List<TypeDefinition> filteredTypes = [];

        foreach (var type in types)
        {
            var match = type.Methods.Where(method => parms.Methods.ExcludeMethods.Contains(method.Name!));

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
    private static IEnumerable<TypeDefinition> FilterByCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        if (parms.Methods.MethodCount == -1)
        {
            return types;
        }

        if (parms.Methods.MethodCount >= 0)
        {
            types = types.Where(t => GetMethodCountExcludingConstructors(t) == parms.Methods.MethodCount);
        }

        return types;
    }

    /// <summary>
    /// Search for types with a constructor of a given length
    /// </summary>
    /// <param name="types">Types to filter</param>
    /// <param name="parms">Search params</param>
    /// <returns>Filtered list</returns>
    private static IEnumerable<TypeDefinition> FilterByCtorParameterCount(
        IEnumerable<TypeDefinition> types,
        SearchParams parms
    )
    {
        // Count disabled, bypass
        if (parms.Methods.ConstructorParameterCount == -1)
        {
            return types;
        }

        return types.Where(type =>
        {
            var constructors = type.Methods.Where(m => m.IsConstructor);
            return constructors.Any(ctor =>
            {
                // Ensure Parameters isn't null before checking Count
                var parameters = ctor.Parameters;
                // This +1 offset is needed for some reason, needs investigation
                return parameters.Count == parms.Methods.ConstructorParameterCount;
            });
        });
    }

    /// <summary>
    /// We don't want the constructors included in the count
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static int GetMethodCountExcludingConstructors(TypeDefinition type)
    {
        var count = 0;
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
