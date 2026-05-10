using AssemblyLib.Patching.ToolTypes;
using EFT;

namespace AssemblyLib.Patching.Fixes;

public class ShowIncompatibleNotificationPatch(Player.FirearmController controller)
    : Player.FirearmController.Idling(controller)
{
    [Patch(typeof(Player.FirearmController.Idling), nameof(ShowIncompatibleNotification), PatchType.Prefix)]
    public bool Patch()
    {
        if (!player_0.IsYourPlayer)
        {
            return true;
        }

        return false;
    }
}
