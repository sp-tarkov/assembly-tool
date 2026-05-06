using AssemblyLib.Patching.Tool;
using EFT;

namespace AssemblyLib.Patching.MethodPatches.EFT;

public class OnGameStartedPatch : GameWorld
{
    /// <summary>
    ///     Sets the player scav's inventory as found in raid
    /// </summary>
    [MethodPatch(typeof(GameWorld), nameof(OnGameStarted), PatchType.Prefix)]
    public void Patch()
    {
        if (MainPlayer == null || MainPlayer.Profile.Side != EPlayerSide.Savage)
        {
            return;
        }

        MainPlayer.Profile.SetSpawnedInSession(true);
    }
}
