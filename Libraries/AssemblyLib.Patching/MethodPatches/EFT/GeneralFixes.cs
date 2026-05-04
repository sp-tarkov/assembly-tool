using System;
using AssemblyLib.Patching.Tool;
using EFT;
using EFT.Counters;
using EFT.UI.SessionEnd;

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

    /// <summary>
    ///     Disables RMT check for discard limits
    /// </summary>
    /// <returns>false, no limits</returns>
    [MethodPatch(
        typeof(SessionResultExitStatus),
        nameof(SessionResultExitStatus.Show),
        MethodPatchType.Prefix,
        typeof(Profile),
        typeof(PlayerVisualRepresentation),
        typeof(ESideType),
        typeof(ExitStatus),
        typeof(TimeSpan),
        typeof(IEftSession),
        typeof(bool)
    )]
    public void FixPostScavRaidXpShowingZeroPatch(Profile activeProfile, ESideType side)
    {
        if (activeProfile.Side == EPlayerSide.Savage)
        {
            side = ESideType.Savage; // Also set side to correct value (defaults to USEC/BEAR when playing as scav)
            var xpGainedInSession = activeProfile.Stats.Eft.SessionCounters.GetAllInt(CounterTag.Exp);
            activeProfile.Stats.Eft.TotalSessionExperience = (int)(
                xpGainedInSession
                * activeProfile.Stats.Eft.SessionExperienceMult
                * activeProfile.Stats.Eft.ExperienceBonusMult
            );
        }
    }
}
