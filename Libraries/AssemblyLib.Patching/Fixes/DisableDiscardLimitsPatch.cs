using AssemblyLib.Patching.ToolTypes;
using EFT;

namespace AssemblyLib.Patching.Fixes;

public class DisableDiscardLimitsPatch
{
    /// <summary>
    ///     Disables RMT check for discard limits
    /// </summary>
    /// <returns>false, no limits</returns>
    [Patch(typeof(Player.PlayerOwnerInventoryController), "get_HasDiscardLimits", PatchType.Replace)]
    public bool Patch()
    {
        return false;
    }
}
