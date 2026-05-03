using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using EftCodeStub.EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.CodeStubs;

[Injectable]
public class OnGameStartedCodeStub(MemberLookup lookup, MethodPatcher methodPatcher) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var onGameStartedMethod = lookup.Eft.Method<GameWorld>("OnGameStarted");
        if (onGameStartedMethod?.CilMethodBody?.Instructions is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.AnticheatValidationOperation.RunValidation()` when patching"
            );
        }

        var onGameStartedStub = lookup.Stub.Method<SptGameWorld>("OnGameStarted");
        if (onGameStartedStub?.CilMethodBody?.Instructions is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EftCodeStub.Eft.SptGameWorld.OnGameStarted()` when patching"
            );
        }

        methodPatcher.Patch(onGameStartedMethod, onGameStartedStub, MethodPatchType.Prefix);
    }
}
