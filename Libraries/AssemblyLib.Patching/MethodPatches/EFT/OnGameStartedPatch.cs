using AssemblyLib.Patching.Tool;
using EFT;

namespace AssemblyLib.Patching.MethodPatches.EFT;

public class OnGameStartedPatch : GameWorld
{
    [MethodPatch(typeof(GameWorld), nameof(GameWorld.OnGameStarted), MethodPatchType.Prefix)]
    public override void OnGameStarted()
    {
        if (MainPlayer == null || MainPlayer.Profile.Side != EPlayerSide.Savage)
        {
            return;
        }

        MainPlayer.Profile.SetSpawnedInSession(true);
    }
}
