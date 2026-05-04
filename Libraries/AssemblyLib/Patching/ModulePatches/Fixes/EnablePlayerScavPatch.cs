using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Fixes;

/// <summary>
/// This patch removes the check if the player is a scav, allowing for scav's in offline raids
/// </summary>
[Injectable]
public class EnablePlayerScavPatch(MemberLookup.ModuleMemberLookup lookup, MethodBodyNuker methodBodyNuker)
    : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Eft.Method<MainMenuShowOperation.CG_Struct14>("MoveNext");
        if (moveNextMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.MainMenuShowOperation.CG_Struct14.MoveNext()` when patching"
            );
        }
        methodBodyNuker.NopRange(moveNextMethod.CilMethodBody.Instructions, 142, 145);
    }
}
