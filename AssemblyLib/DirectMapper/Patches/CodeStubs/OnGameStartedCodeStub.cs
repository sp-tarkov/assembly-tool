using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.CodeStubs;

[Injectable]
public class OnGameStartedCodeStub(MemberLookup lookup) : IPatch
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
    }
}
