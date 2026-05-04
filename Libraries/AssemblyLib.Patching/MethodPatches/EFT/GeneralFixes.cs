using AssemblyLib.Patching.Tool;
using EFT;

namespace AssemblyLib.Patching.MethodPatches.EFT;

public class GeneralFixes
{
    /// <summary>
    ///     Disables RMT check for discard limits
    /// </summary>
    /// <returns>false, no limits</returns>
    [MethodPatch(typeof(Player.PlayerOwnerInventoryController), "get_HasDiscardLimits", MethodPatchType.Replace)]
    public bool DisableDiscardLimits()
    {
        return false;
    }
}
