using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Fixes;

/// <summary>
/// Fixes Unity constantly spamming the console with "Setting linear velocity of a kinematic body is not supported" warnings after an airdrop crate spawns.
///
/// This is caused by Unity 2022, which added this warning because setting the velocity property of a kinematic RigidBody doesn't work; the error just
/// wasn't shown in previous versions. However, regularly setting the airdrop crate's velocity to zero doesn't seem necessary, so simply removing that
/// line will fix this.
/// </summary>
[Injectable]
public class FixUnityWarningSpamFromAirdropsPatch(MemberLookup.MemberLookup lookup, MethodBodyNuker methodBodyNuker)
    : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        // Use string based lookup because of weird unity struct layout things, the CLR doesn't like it.
        var manualUpdateMethod = lookup.Eft.Method("EFT.Airdrop", "ServerAirDrop", "ManualUpdate");
        if (manualUpdateMethod?.CilMethodBody?.Instructions is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.Airdrop.ServerAirDrop.ManualUpdate()` when patching"
            );
        }

        // Remove `this.Rigidbody_0.velocity = Vector3.zero;`
        methodBodyNuker.NopRange(manualUpdateMethod.CilMethodBody.Instructions, 25, 28);
    }
}
