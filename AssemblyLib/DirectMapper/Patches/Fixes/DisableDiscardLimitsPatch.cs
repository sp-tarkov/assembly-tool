using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

/// <summary>
///     Disables anti-rmt shit
/// </summary>
[Injectable]
public class DisableDiscardLimitsPatch(ModuleMemberLookup lookup, PatchHelper patchHelper) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var discardLimitsGetter = lookup.Method<Player.PlayerOwnerInventoryController>("get_HasDiscardLimits");
        if (discardLimitsGetter?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.Player.PlayerOwnerInventoryController.HasDiscardLimits` when patching"
            );
        }

        patchHelper.NukeBoolBody(discardLimitsGetter.CilMethodBody);
    }
}
