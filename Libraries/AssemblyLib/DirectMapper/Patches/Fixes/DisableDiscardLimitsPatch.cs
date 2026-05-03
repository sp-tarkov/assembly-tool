using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

/// <summary>
///     Disables anti-rmt shit
/// </summary>
[Injectable]
public class DisableDiscardLimitsPatch(MemberLookup lookup, MethodBodyNuker methodBodyNuker) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var discardLimitsGetter = lookup.Eft.Method<Player.PlayerOwnerInventoryController>("get_HasDiscardLimits");
        if (discardLimitsGetter?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.Player.PlayerOwnerInventoryController.HasDiscardLimits` when patching"
            );
        }

        methodBodyNuker.NukeBoolBody(discardLimitsGetter.CilMethodBody);
    }
}
