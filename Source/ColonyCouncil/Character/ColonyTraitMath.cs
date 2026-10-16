using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic behind what a colony grows into and the names that come with its years (no game types, so the
    /// tests can run it): the rolling average a trait is measured by, the calendar's standing count, and who counts as
    /// one of the colony's founders.
    /// </summary>
    public static class ColonyTraitMath
    {
        /// <summary>Today's amount folded into a rolling average (<paramref name="alpha"/>: today's share).</summary>
        public static float Fold(float rate, float amount, float alpha)
        {
            if (alpha <= 0f) return rate;
            if (alpha >= 1f) return amount;
            return rate * (1f - alpha) + amount * alpha;
        }

        /// <summary>Known at <paramref name="knownAt"/>; once known, kept down to <paramref name="loseShare"/> of it, so it doesn't flicker.</summary>
        public static bool Known(bool wasKnown, float rate, float knownAt, float loseShare) =>
            wasKnown ? rate >= knownAt * loseShare : rate >= knownAt;

        /// <summary>
        /// A camp the colony hosted to the end: it made camp, and left because its time was up, or the weather or a raid
        /// moved it on. Not one driven off, asked to go, crowded out, gone hungry, soured, harmed or fought.
        /// </summary>
        public static bool HostedWell(string ending, bool arrived)
        {
            if (!arrived) return false;
            switch (ending)
            {
                case null:
                case "time":
                case "weather":
                case "raid":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// A deed the region remembers (FactionMemory's kinds) that was the colony taking people in: visitors welcomed, a
        /// feast or a dignitary hosted, refugees or travellers sheltered, peace talks held at its table, wounded guests
        /// tended. Camps count on their own, when they leave.
        /// </summary>
        public static bool Hosting(string deedKind)
        {
            switch (deedKind)
            {
                case "welcomed":
                case "feast":
                case "dignitary":
                case "refugees":
                case "sheltered":
                case "peaceTalks":
                case "tended":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A calendar day kept this many years running counts toward a colony that keeps its days.</summary>
        public const int KeptYearsToCount = 2;

        /// <summary>
        /// The calendar's standing count: each day kept two years running or more (a birthday or an arrival, a person's
        /// own day, doesn't count), and each tradition once more.
        /// </summary>
        public static float CalendarLevel(IEnumerable<(bool personal, int keptYears, bool tradition)> days)
        {
            if (days == null) return 0f;
            float n = 0f;
            foreach (var d in days)
            {
                if (d.personal) continue;
                if (d.keptYears >= KeptYearsToCount) n += 1f;
                if (d.tradition) n += 1f;
            }
            return n;
        }

        /// <summary>Someone who came within the colony's first two days is one of its founders.</summary>
        public const int FounderDays = 2;

        /// <summary>Joined at this tick (the game's tick less their time as a colonist): one of the founders.</summary>
        public static bool Founder(int joinedTick, int ticksPerDay) => joinedTick <= FounderDays * ticksPerDay;

        /// <summary>
        /// The last of the first settlers: at least two founded the colony, only one of them is still here, and the colony
        /// has seen a year.
        /// </summary>
        public static bool LastOfTheFirst(int foundersEver, int foundersHere, float yearsSinceFounding) =>
            foundersEver >= 2 && foundersHere == 1 && yearsSinceFounding >= 1f;
    }
}
