using AssemblyLib.DirectMapper.Helpers;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Core;

[Injectable]
public class RemoveHwInfoPatch(PatchHelper patchHelper, DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.Namespace == "EFT" && t.Name == "TarkovApplication")
            ?.NestedTypes.FirstOrDefault(t => t.Name == "CG_Struct35")
            ?.Methods.FirstOrDefault(m => m.Name == "MoveNext");

        if (moveNextMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.TarkovApplication.CG_Struct35.MoveNext()` when patching"
            );
        }

        patchHelper.NopRange(moveNextMethod.CilMethodBody!.Instructions, 62, 94);

        var hwEchoType = dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.Name == "HWEcho");
        if (hwEchoType is null)
        {
            throw new FailedToFindTypeException("Could not find `HWEcho` when patching");
        }

        patchHelper.NukeType(hwEchoType);

        Log.Information("RemoveHwInfoPatch Successful");
    }
}
