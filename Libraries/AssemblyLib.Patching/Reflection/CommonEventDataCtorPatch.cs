using AssemblyLib.Patching.ToolTypes;
using EFT.GlobalEvents;

namespace AssemblyLib.Patching.Reflection;

public class CommonEventDataCtorPatch : CommonEventData
{
    /// <summary>
    ///     Fixes reflection forcefully loading all assemblies into context
    /// </summary>
    [Patch(typeof(CommonEventData), PatchTargetKind.Constructor, PatchType.Replace)]
    public void Patch()
    {
        // We have to initialize the fields since we're wiping the instance constructor
        _deserializeSentEventMap = [];
        _events = [];
        _eventsToApply = [];
        _nameEventMap = [];
        _pools = [];
        _serializeSentEventMap = [];

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
