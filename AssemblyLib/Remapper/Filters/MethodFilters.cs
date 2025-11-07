using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Remapper.Filters;

[Injectable(TypePriority = 1)]
public sealed class MethodFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out IEnumerable<TypeDefinition> filteredTypes
    )
    {
        types = FilterByInclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by included methods");
            filteredTypes = types;
            return false;
        }

        types = FilterByExclude(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by excluded methods");
            filteredTypes = types;
            return false;
        }

        types = FilterByCtorParameterCount(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by constructor parameter count");
            filteredTypes = types;
            return false;
        }

        types = FilterByCount(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by method count");
            filteredTypes = types;
            return false;
        }

        filteredTypes = types;
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
        if (parms.Methods.MethodCount == -1)
        {
            return types;
        }

        if (parms.Methods.MethodCount >= 0)
        {
            types = types.Where(t => t.Methods.Count(m => !m.IsConstructor) == parms.Methods.MethodCount);
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
                return parameters.Count == parms.Methods.ConstructorParameterCount;
            });
        });
    }
}
