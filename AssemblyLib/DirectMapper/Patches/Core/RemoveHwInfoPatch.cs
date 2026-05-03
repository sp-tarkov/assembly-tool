using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Core;

[Injectable]
public class RemoveHwInfoPatch(ModuleMemberLookup lookup, PatchHelper patchHelper, DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Method<TarkovApplication.CG_Struct35>("MoveNext");
        if (moveNextMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.TarkovApplication.CG_Struct35.MoveNext()` when patching"
            );
        }

        patchHelper.NopRange(moveNextMethod.CilMethodBody!.Instructions, 62, 94);

        // Use old way here because we delete it, can't resolve it with strong typed code.
        var hwEchoType = dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.Name == "HWEcho");
        if (hwEchoType is null)
        {
            throw new FailedToFindTypeException("Could not find `HWEcho` when patching");
        }

        patchHelper.NukeType(hwEchoType);

        var sendMetricsJsonMethod = lookup.Method<ClientBackendSession>("SendMetricsJson");
        if (sendMetricsJsonMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.ClientBackendSession.SendMetricsJson()` when patching"
            );
        }

        patchHelper.NukeTaskBody(sendMetricsJsonMethod.CilMethodBody);
    }
}
