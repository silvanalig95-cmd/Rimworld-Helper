using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of council meetings, free of game types so the tests can cover it (Tools/Tests): how long a
    /// meeting runs, when it's set for, how many make a proper meeting, and who would arrive late or too late.
    /// </summary>
    public static class MeetingMath
    {
        /// <summary>How long the gathering runs once the chair has called everyone (1.8 in-game hours).</summary>
        public const int MeetingTicks = 4500;
        public const int TicksPerHour = 2500;
        /// <summary>Arriving after this share of the meeting is arriving late.</summary>
        public const float LateShare = 1f / 3f;
        /// <summary>Someone who'd arrive after this share of the meeting isn't called at all.</summary>
        public const float MissShare = 0.7f;
        /// <summary>Paths wind and doors slow people down: the walk takes this much longer than a straight line.</summary>
        public const float PathFactor = 1.35f;
        /// <summary>Putting down tools and setting off.</summary>
        public const int GetGoingTicks = 250;
        /// <summary>The usual hour for a meeting, and the latest full hour one is still set for that evening.</summary>
        public const float EveningHour = 18f, LatestHour = 20f;

        /// <summary>Fewer than this many present makes a thin meeting: half of those with a say, and never fewer than two.</summary>
        public static int Quorum(int eligible) => Math.Max(2, (int)Math.Ceiling(eligible / 2.0));

        public static bool Thin(int present, int eligible) => present < Quorum(eligible);

        /// <summary>
        /// Hours from <paramref name="hourNow"/> (0 to 24, local) to the next evening slot at least
        /// <paramref name="leadHours"/> away: six in the evening, or a later full hour up to eight; past that, six on a
        /// later evening.
        /// </summary>
        public static float HoursToEvening(float hourNow, float leadHours)
        {
            float earliest = hourNow + Math.Max(0f, leadHours);
            float target;
            if (earliest <= EveningHour) target = EveningHour;
            else if (earliest <= LatestHour) target = (float)Math.Ceiling(earliest);
            else
            {
                target = EveningHour + 24f;
                while (target < earliest) target += 24f;
            }
            return target - hourNow;
        }

        /// <summary>The walk to the meeting in ticks, from the straight-line distance and the colonist's ticks per cell.</summary>
        public static int EtaTicks(float distance, float ticksPerCell) =>
            GetGoingTicks + (int)Math.Round(Math.Max(0f, distance) * Math.Max(1f, ticksPerCell) * PathFactor);

        /// <summary>They'd get there after most of the meeting is over.</summary>
        public static bool TooFar(int etaTicks) => etaTicks > MissShare * MeetingTicks;

        /// <summary>Arriving this many ticks after the start counts as late.</summary>
        public static bool Late(int ticksAfterStart) => ticksAfterStart > MeetingTicks * LateShare;
    }
}
