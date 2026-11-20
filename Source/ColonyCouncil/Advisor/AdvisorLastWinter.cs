using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The advisor remembers last winter (C20 of docs/content-update-proposals.md; the player's go of 20 November 2026): the
    /// notes on firewood, the winter stores, the herd's feed and the herd in the cold read the colony's own record of the last
    /// winter (Weather/WeatherBook.cs, WeatherMath.WinterBefore: how many days the cold lasted, too cold for anything to grow,
    /// how many stayed below freezing all day, how many the snow lay, the coldest night), and add one sentence in the colony's
    /// own terms: "Last winter the cold here lasted 19 days; at today's fires the wood in store lasts 11 such days." Nothing
    /// is said before the record has seen a winter, and nothing else in the notes changes.
    /// </summary>
    public static class AdvisorLastWinter
    {
        public static LastWinter Of(Map map) => map == null ? null : WeatherBook.Of(map)?.LastWinter;

        private static string Cold(float c) => c.ToStringTemperature("F0");

        /// <summary>The firewood note: last winter's cold against the days the wood in store lasts at today's fires.</summary>
        public static string Firewood(Map map, int have, float perDay)
        {
            var w = Of(map);
            if (w == null || perDay <= 0f) return null;
            return "CC_Adv_LastWinter_Firewood".Translate(w.coldDays, Cold(w.coldest), WeatherMath.DaysOf(have, perDay)).Resolve();
        }

        /// <summary>The winter stores' note: last winter's cold, and how few of its days froze.</summary>
        public static string Stores(Map map)
        {
            var w = Of(map);
            return w == null ? null : "CC_Adv_LastWinter_Stores".Translate(w.coldDays, w.frozenDays).Resolve();
        }

        /// <summary>The herd's feed: last winter's cold (and the snow), against the days the hay and kibble last.</summary>
        public static string Feed(Map map, float feedDays)
        {
            var w = Of(map);
            if (w == null) return null;
            int days = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.FloorToInt(feedDays));
            return w.coverDays > 0
                ? "CC_Adv_LastWinter_FeedSnow".Translate(w.coldDays, w.coverDays, days).Resolve()
                : "CC_Adv_LastWinter_Feed".Translate(w.coldDays, days).Resolve();
        }

        /// <summary>The herd in the cold: last winter's coldest night here.</summary>
        public static string HerdCold(Map map)
        {
            var w = Of(map);
            return w == null ? null : "CC_Adv_LastWinter_HerdCold".Translate(Cold(w.coldest), w.frozenDays).Resolve();
        }

        /// <summary>A note's detail with last winter's sentence after it, when there is one.</summary>
        public static string With(string detail, string lastWinter) => lastWinter.NullOrEmpty() ? detail : detail + " " + lastWinter;
    }
}
