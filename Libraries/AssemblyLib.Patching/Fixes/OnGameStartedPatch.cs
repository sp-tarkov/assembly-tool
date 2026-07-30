using AssemblyLib.Patching.ToolTypes;
using EFT;

namespace AssemblyLib.Patching.Fixes;

public class OnGameStartedPatches : GameWorld
{
    /// <summary>
    ///     Sets the player scav's inventory as found in raid
    /// </summary>
    [Patch(typeof(GameWorld), nameof(OnGameStarted), PatchType.Prefix)]
    public void Patch()
    {
        if (MainPlayer == null || MainPlayer.Profile.Side != EPlayerSide.Savage)
        {
            return;
        }

        MainPlayer.Profile.SetSpawnedInSession(true);
    }
}
