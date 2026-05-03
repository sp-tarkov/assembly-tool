using EFT;

namespace EftCodeStub.EFT;

public class SptGameWorld : GameWorld
{
    public override void OnGameStarted()
    {
        if (MainPlayer == null || MainPlayer.Profile.Side != EPlayerSide.Savage)
        {
            return;
        }

        MainPlayer.Profile.SetSpawnedInSession(true);
    }
}
