using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules of the hurt of other peoples the colony takes in (World/Region/TakenIn.cs): a captive who ran from a
    /// raiders' camp (B13) and a defence partner's wounded after their fight (B27). They lie in the colony's beds as its
    /// guests, tended as the game tends a guest, and walk home once nothing needs tending and they need no more bed rest (or
    /// after twenty days, if they can walk). Pure (no game types): Tools/Tests checks them.
    /// </summary>
    public static class TakenInMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        /// <summary>Why they are here: a runaway from a raiders' camp, or the wounded of a fight the colony was called to.</summary>
        public const string Runaway = "runaway", Wounded = "wounded";

        /// <summary>At most this many kept at once, and so many parties of wounded on their way.</summary>
        public const int MaxPatients = 8, MaxComing = 4;

        /// <summary>They go home by then, well or not, if they can walk.</summary>
        public const int StayDays = 20;

        /// <summary>The runaway's injuries from before (bruises, cuts, scratches), and the wounded's from their fight.</summary>
        public const int RunawayInjuriesMin = 2, RunawayInjuriesMax = 3, WoundedInjuriesMin = 2, WoundedInjuriesMax = 3;

        /// <summary>Each injury this bad, and never more than this share of the part's health (nothing lost, nothing that kills).</summary>
        public const float SeverityMin = 3f, SeverityMax = 6f, PartShare = 0.6f;

        /// <summary>No wound that would leave them bleeding to death sooner than this (days): there is time to tend them.</summary>
        public const float MinBleedDays = 1.5f;

        /// <summary>Two to four of their wounded come, one to three days after the colony offered its beds.</summary>
        public const int WoundedMin = 2, WoundedMax = 4;

        public const float ArriveDaysMin = 1f, ArriveDaysMax = 3f;

        /// <summary>How bad an injury is (<paramref name="roll"/> 0 to 1), on a part with this much health.</summary>
        public static float Severity(float roll, float partHealth)
        {
            float r = float.IsNaN(roll) ? 0f : Math.Max(0f, Math.Min(1f, roll));
            float s = SeverityMin + r * (SeverityMax - SeverityMin);
            return Math.Min(s, Math.Max(0f, partHealth) * PartShare);
        }

        /// <summary>Well again: nothing needs tending, no bed rest needed, up on their feet.</summary>
        public static bool Well(bool needsTending, bool needsRest, bool downed) => !needsTending && !needsRest && !downed;

        /// <summary>They walk home: well, or their days here are over, and never while they can't stand.</summary>
        public static bool GoesHome(bool well, bool downed, int since, int now) => !downed && (well || since >= 0 && now - since >= (long)StayDays * DayTicks);

        public static int ArriveTicks(float roll) => (int)Math.Round((ArriveDaysMin + Math.Max(0f, Math.Min(1f, float.IsNaN(roll) ? 0f : roll)) * (ArriveDaysMax - ArriveDaysMin)) * DayTicks);
    }
}
