using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.Patches.Core;

[Injectable]
public class RemoveHwInfoPatch(
    MemberLookup.MemberLookup lookup,
    MethodBodyNuker methodBodyNuker,
    DataProvider dataProvider
) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Eft.Method<TarkovApplication.CG_Struct35>("MoveNext");
        if (moveNextMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.TarkovApplication.CG_Struct35.MoveNext()` when patching"
            );
        }

        methodBodyNuker.NopRange(moveNextMethod.CilMethodBody!.Instructions, 62, 94);

        // Use old way here because we delete it, can't resolve it with strong typed code.
        var hwEchoType = dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.Name == "HWEcho");
        if (hwEchoType is null)
        {
            throw new FailedToFindTypeException("Could not find `HWEcho` when patching");
        }

        methodBodyNuker.NukeType(hwEchoType);

        var sendMetricsJsonMethod = lookup.Eft.Method<ClientBackendSession>("SendMetricsJson");
        if (sendMetricsJsonMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.ClientBackendSession.SendMetricsJson()` when patching"
            );
        }

        methodBodyNuker.NukeTaskBody(sendMetricsJsonMethod.CilMethodBody);
    }
}
