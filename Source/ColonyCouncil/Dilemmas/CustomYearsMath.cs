using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How a custom stands when it comes round again, as tags for its letter and the lines of its answers (Dilemmas.cs),
    /// so a custom kept year after year reads like one: "again" once its custom answer has been chosen before, "years"
    /// once it has been chosen <see cref="Years"/> times or more, "tradition" once it is a tradition, and "missed" when it
    /// is a tradition the colony let go the last time it came round. A tradition that lapses starts again from nothing.
    /// Pure (no game types), for the tests.
    /// </summary>
    public static class CustomYearsMath
    {
        /// <summary>Kept this many times or more, a custom has been held for years.</summary>
        public const int Years = 3;

        public const string Again = "again", HeldYears = "years", Tradition = "tradition", Missed = "missed";

        /// <summary>Every tag <see cref="Tags"/> can give, for the tests.</summary>
        public static readonly string[] All = { Again, HeldYears, Tradition, Missed };

        /// <summary>
        /// The tags for a custom kept <paramref name="kept"/> times (since it last lapsed), let go <paramref name="breaks"/>
        /// times in a row as a tradition, and a tradition now or not.
        /// </summary>
        public static List<string> Tags(int kept, int breaks, bool tradition)
        {
            var tags = new List<string>(4);
            if (kept >= 1) tags.Add(Again);
            if (kept >= Years) tags.Add(HeldYears);
            if (tradition)
            {
                tags.Add(Tradition);
                if (breaks >= 1) tags.Add(Missed);
            }
            return tags;
        }
    }
}
