using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

/// <summary>
/// Fixes Unity constantly spamming the console with "Setting linear velocity of a kinematic body is not supported" warnings after an airdrop crate spawns.
///
/// This is caused by Unity 2022, which added this warning because setting the velocity property of a kinematic RigidBody doesn't work; the error just
/// wasn't shown in previous versions. However, regularly setting the airdrop crate's velocity to zero doesn't seem necessary, so simply removing that
/// line will fix this.
/// </summary>
[Injectable]
public class FixUnityWarningSpamFromAirdropsPatch(ModuleMemberLookup lookup, PatchHelper patchHelper) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        // Use string based lookup because of weird unity struct layout things, the CLR doesn't like it.
        var manualUpdateMethod = lookup.Method("EFT.Airdrop", "ServerAirDrop", "ManualUpdate");
        if (manualUpdateMethod?.CilMethodBody?.Instructions is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.Airdrop.ServerAirDrop.ManualUpdate()` when patching"
            );
        }

        // Remove `this.Rigidbody_0.velocity = Vector3.zero;`
        patchHelper.NopRange(manualUpdateMethod.CilMethodBody.Instructions, 25, 28);
    }
}
