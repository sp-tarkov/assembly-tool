using AssemblyLib.Exceptions;
using AssemblyLib.Extensions;
using AssemblyLib.Helpers;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

/// <summary>
/// This patch removes the check if the player is a scav, allowing for scav's in offline raids
/// </summary>
[Injectable]
public class EnablePlayerScavPatch(ModuleMemberLookup lookup, PatchHelper patchHelper) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Method<MainMenuShowOperation.CG_Struct14>("MoveNext");
        if (moveNextMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.MainMenuShowOperation.CG_Struct14.MoveNext()` when patching"
            );
        }

        patchHelper.NopRange(moveNextMethod.CilMethodBody.Instructions, 144, 148);
    }
}
