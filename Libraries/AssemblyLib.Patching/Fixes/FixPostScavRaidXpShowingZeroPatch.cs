using System;
using AssemblyLib.Patching.ToolTypes;
using EFT;
using EFT.Counters;
using EFT.UI.SessionEnd;

namespace AssemblyLib.Patching.Fixes;

public class FixPostScavRaidXpShowingZeroPatch
{
    /// <summary>
    ///     Fixes post raid scav raid xp not showing
    /// </summary>
    /// <returns>false, no limits</returns>
    [Patch(
        typeof(SessionResultExitStatus),
        nameof(SessionResultExitStatus.Show),
        PatchType.Prefix,
        typeof(Profile),
        typeof(PlayerVisualRepresentation),
        typeof(ESideType),
        typeof(ExitStatus),
        typeof(TimeSpan),
        typeof(IEftSession),
        typeof(bool)
    )]
    public void Patch(Profile activeProfile, ESideType side)
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
