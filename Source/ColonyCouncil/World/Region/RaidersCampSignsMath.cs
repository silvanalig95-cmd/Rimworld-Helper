using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules of a raiders' camp's signs on the map (docs/content-update-proposals.md, B13;
    /// World/Region/RaidersCampSigns.cs): before each of its raids two of its scouts come to the map's edge on the side the
    /// raid will come from, watch for an hour or so and go, and the raid follows two to three hours after they were first
    /// seen (held back while another fight is on, given up half a day late); once in its life a captive who ran from it
    /// reaches the gate, and taken in, its next raid comes sooner; at dusk someone on the walls may speak of its smoke.
    /// Pure (no game types): Tools/Tests checks them.
    /// </summary>
    public static class RaidersCampSignsMath
    {
        public const int HourTicks = 2500;
        public const int DayTicks = 60000;

        /// <summary>Two scouts come before a raid.</summary>
        public const int Scouts = 2;

        /// <summary>They watch from near the edge this long (hours), then walk off.</summary>
        public const float WatchHoursMin = 1f, WatchHoursMax = 1.5f;

        /// <summary>The raid comes this long (hours) after they were first seen.</summary>
        public const float LeadHoursMin = 2f, LeadHoursMax = 3f;

        /// <summary>How far in from the edge they stand to watch (cells).</summary>
        public const int WatchCells = 10;

        public static int WatchTicks(float roll) => (int)Math.Round((WatchHoursMin + Clamp01(roll) * (WatchHoursMax - WatchHoursMin)) * HourTicks);

        public static int LeadTicks(float roll) => (int)Math.Round((LeadHoursMin + Clamp01(roll) * (LeadHoursMax - LeadHoursMin)) * HourTicks);

        /// <summary>The scouts are always gone before the raid they come ahead of.</summary>
        public static bool GoneBefore(float watchRoll, float leadRoll) => WatchTicks(watchRoll) < LeadTicks(leadRoll);

        /// <summary>A raid held back by another fight on the map is given up this long (hours) after it was due.</summary>
        public const int LateHours = 12;

        public static bool Due(int due, int now) => due >= 0 && now >= due;

        public static bool TooLate(int due, int now) => due >= 0 && now - due > (long)LateHours * HourTicks;

        // ---------------- The runaway ----------------

        /// <summary>A day's chance of the runaway once the camp has stood found this many days, once in its life.</summary>
        public const float RunawayDaily = 0.05f;

        public const int RunawayAfterDays = 3;

        public static bool RunawayMay(bool already, CampStage stage, int foundAt, bool raidOpen, int now) =>
            !already && !raidOpen && stage == CampStage.Found && foundAt >= 0 && now - foundAt >= (long)RunawayAfterDays * DayTicks;

        /// <summary>Taken in and tended, the runaway's people think this much better of the colony (and remember it: the deed "rescued").</summary>
        public const int RunawayGoodwill = 6;

        /// <summary>Taken in, the camp's next raid comes sooner: its mean days between raids this much shorter, once.</summary>
        public const float SoonerFactor = 0.5f;

        public static float Mtb(float mtbDays, bool sooner) => sooner && !float.IsInfinity(mtbDays) ? mtbDays * SoonerFactor : mtbDays;

        // ---------------- The smoke at dusk ----------------

        /// <summary>At this hour of the evening, every few days at most, half the time.</summary>
        public const int DuskHour = 18;

        public const int SmokeGapDays = 3;

        public const float SmokeChance = 0.5f;

        public static bool SmokeDue(int hour, int lastAt, int now) => hour == DuskHour && (lastAt < 0 || now - lastAt >= (long)SmokeGapDays * DayTicks);

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
