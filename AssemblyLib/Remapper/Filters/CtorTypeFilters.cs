using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public class CtorTypeFilters
{
    /// <summary>
    /// Search for types with a constructor of a given length
    /// </summary>
    /// <param name="types">Types to filter</param>
    /// <param name="parms">Search params</param>
    /// <returns>Filtered list</returns>
    public IEnumerable<TypeDefinition> FilterByParameterCount(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
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
}
