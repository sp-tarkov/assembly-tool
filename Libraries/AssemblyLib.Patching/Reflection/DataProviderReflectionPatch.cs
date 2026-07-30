using System.Collections.Generic;
using System.Linq;
using AssemblyLib.Patching.ToolTypes;
using EFT.DataProviding;
using Type = System.Type;

namespace AssemblyLib.Patching.Reflection;

public class DataProviderReflectionPatch
{
    /// <summary>
    ///     Fixes reflection forcefully loading all assemblies into context
    /// </summary>
    [Patch(typeof(DataProvider), nameof(DataProvider.GetDataContainersTypes), PatchType.Replace)]
    public List<Type> Patch<T>()
        where T : IDataContainer
    {
        var types = typeof(T).Assembly.GetTypes();

        var result = new List<Type>();

        foreach (var type in types)
        {
            if (type.IsClass && !type.IsAbstract && type.GetInterfaces().Contains(typeof(T)))
            {
                result.Add(type);
            }
        }

        return result;
    }
}
