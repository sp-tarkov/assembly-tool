using AssemblyLib.Patching.Tool;
using EFT;

namespace AssemblyLib.Patching.MethodPatches.EFT;

public class ShowIncompatibleNotificationPatch(Player.FirearmController controller)
    : Player.FirearmController.Idling(controller)
{
    [MethodPatch(typeof(Player.FirearmController.Idling), nameof(ShowIncompatibleNotification), PatchType.Prefix)]
    public bool Patch()
    {
        if (!player_0.IsYourPlayer)
        {
            return true;
        }

        return false;
    }
}
