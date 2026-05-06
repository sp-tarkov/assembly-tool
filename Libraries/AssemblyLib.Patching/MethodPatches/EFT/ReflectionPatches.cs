using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyLib.Patching.Tool;
using EFT.DataProviding;
using EFT.GlobalEvents;

namespace AssemblyLib.Patching.MethodPatches.EFT;

public class GeneralReflectionPatches
{
    /// <summary>
    ///     Fixes reflection forcefully loading all assemblies into context
    /// </summary>
    [Patch(typeof(DataProvider), nameof(DataProvider.GetDataContainersTypes), PatchType.Replace)]
    public List<Type> FixDataProviderReflectionPatch<T>()
        where T : IDataContainer
    {
        var types = typeof(T).Assembly.GetTypes();

        var result = new List<Type>();

        foreach (var type in types)
        {
            if (type.IsClass && !type.IsAbstract && !type.GetInterfaces().Contains(typeof(T)))
            {
                result.Add(type);
            }
        }

        return result;
    }
}

public class CommonEventDataCtorPatch : CommonEventData
{
    /// <summary>
    ///     Fixes reflection forcefully loading all assemblies into context
    /// </summary>
    [Patch(typeof(CommonEventData), PatchTargetKind.Constructor, PatchType.Replace)]
    public void Patch()
    {
        var types = typeof(IEvent).Assembly.GetTypes();
        foreach (var type in types)
        {
            if (typeof(IEvent).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            {
                Add(type);
            }
        }
    }
}
