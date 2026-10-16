using System;

namespace ColonyCouncil
{
    /// <summary>How well one of the heart's needs is met, as the tooltips name it.</summary>
    public enum NeedLevel
    {
        Lacking,
        Missed,
        Fair,
        Met
    }

    /// <summary>
    /// The numbers behind the heart's four needs, kept free of game types so the tests can check them: where a need
    /// starts to be missed and counts as well met, how much likelier it makes the wishes and life goals that answer it,
    /// how a low need deepens the feeling that goes with it, and when a long lack starts to wear on someone. None of it
    /// is mood: the needs only nudge, deepen and (after days) add a little strain within the heart's daily caps.
    /// </summary>
    public static class HeartNeedMath
    {
        /// <summary>From here a need is well met; with all four here, strain drains <see cref="AllMetRelief"/> times as fast.</summary>
        public const float WellMet = 60f;

        /// <summary>Below this a need is missed: the one they lack most makes what answers it likelier.</summary>
        public const float MissedBelow = 45f;

        /// <summary>Below this a need deepens the feeling that goes with it (lonely, restless, uneasy, frustrated).</summary>
        public const float FeelingBelow = 35f;

        /// <summary>Below this a need is badly lacking; kept up for days, it adds a little strain.</summary>
        public const float LackingBelow = 25f;

        public const float AllMetRelief = 1.2f;

        /// <summary>At most this much likelier, for a wish that answers the need they lack most (at 0).</summary>
        public const float MaxWishNudge = 0.35f;

        /// <summary>Life goals take the wish nudge times this, up to <see cref="MaxFactor"/>.</summary>
        public const float GoalExtra = 1.15f;

        public const float MaxFactor = 1.5f;

        /// <summary>Hours badly lacking (net of the hours it was better) before it adds strain: about three days.</summary>
        public const float HoursBeforeStrain = 72f;

        /// <summary>The count stops here: a lack of a week or more is made good in about three and a half days.</summary>
        public const float MaxLackHours = 168f;

        /// <summary>A better hour takes this many hours off the count.</summary>
        public const float RecoverySpeed = 2f;

        /// <summary>Strain an hour from one need badly lacking for days: from 0.04 just under 25 to 0.08 at 0 (1 to 2 a day).</summary>
        public const float StrainPerHourLeast = 0.04f;

        public const float StrainPerHourMost = 0.08f;

        public static NeedLevel LevelOf(float value) =>
            value < LackingBelow ? NeedLevel.Lacking : value < MissedBelow ? NeedLevel.Missed : value < WellMet ? NeedLevel.Fair : NeedLevel.Met;

        /// <summary>How much likelier a wish that answers the need they lack most is: 1 from 45 up, 1.35 at 0.</summary>
        public static float WishFactor(float value) => value >= MissedBelow ? 1f : 1f + MaxWishNudge * Clamp01((MissedBelow - value) / MissedBelow);

        /// <summary>The same for a life goal: a little more, at most half again.</summary>
        public static float GoalFactor(float value) => value >= MissedBelow ? 1f : Math.Min(MaxFactor, WishFactor(value) * GoalExtra);

        /// <summary>All four well met (60 or more): strain drains faster.</summary>
        public static bool AllWellMet(float a, float b, float c, float d) => a >= WellMet && b >= WellMet && c >= WellMet && d >= WellMet;

        /// <summary>
        /// The running count of hours a need has been badly lacking, after <paramref name="elapsed"/> more hours at
        /// <paramref name="value"/>: it grows while the need is below 25 and shrinks twice as fast while it isn't, so an
        /// hour's good company doesn't wipe out a lonely week, and a few good days do.
        /// </summary>
        public static float LackHours(float hours, float value, float elapsed)
        {
            if (elapsed <= 0f || float.IsNaN(hours)) return Math.Max(0f, float.IsNaN(hours) ? 0f : hours);
            if (value < LackingBelow) return Math.Min(MaxLackHours, Math.Max(0f, hours) + elapsed);
            return Math.Max(0f, hours - elapsed * RecoverySpeed);
        }

        /// <summary>Badly lacking for days and still below 25: it wears on them.</summary>
        public static bool Wearing(float lackHours, float value) => lackHours >= HoursBeforeStrain && value < LackingBelow;

        /// <summary>Strain an hour from one need that has been badly lacking for days (0 otherwise).</summary>
        public static float StrainPerHour(float lackHours, float value)
        {
            if (!Wearing(lackHours, value)) return 0f;
            return StrainPerHourLeast + (StrainPerHourMost - StrainPerHourLeast) * Clamp01((LackingBelow - value) / LackingBelow);
        }

        /// <summary>
        /// How much a low need adds to the feeling that goes with it this hour: nothing from 35 up, <paramref name="full"/>
        /// at 0. Small next to what feeds a feeling otherwise (a feeling is felt from about 5 or 6).
        /// </summary>
        public static float FeelingAmount(float value, float full)
        {
            if (full <= 0f || value >= FeelingBelow) return 0f;
            return full * Clamp01((FeelingBelow - value) / FeelingBelow);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
