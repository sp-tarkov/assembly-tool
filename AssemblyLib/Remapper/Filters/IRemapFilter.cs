using AsmResolver.DotNet;
using AssemblyLib.Models;

namespace AssemblyLib.ReMapper.Filters;

public interface IRemapFilter
{
    /// <summary>
    ///     Filter down a provided set of types based on remap model parameters
    /// </summary>
    /// <param name="types">Types to filter</param>
    /// <param name="remapModel">Model to check against</param>
    /// <param name="filteredTypes">Processed types, can be null if the filter failed</param>
    /// <returns>True if there are remaining types after filtering</returns>
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out IEnumerable<TypeDefinition>? filteredTypes
    );
}
