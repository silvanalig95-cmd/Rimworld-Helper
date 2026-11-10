using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One place the colony heard of, as the rules see it: its key, when it was heard, how long it is kept.</summary>
    public struct HeardPlaceFacts
    {
        public string key;
        public int tick;
        public float keepDays;
    }

    /// <summary>
    /// Places heard of (the player, 10 November 2026: "if other colonies tell me about things that are nearby on their map,
    /// will these also be revealed on my own map for reference?"): each place a piece of news names that is a real world
    /// object (a site) or a tile (a world event's place) is marked on the region map and the game's world map, its tooltip
    /// saying who told the colony, through whom and when, and fades after the news's own keep time. The rules: a place's key
    /// (a site by its world object, else its tile), the same place heard again is one mark brought up to date, how faint a
    /// mark is as its time runs out, and the cap. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class HeardPlaceMath
    {
        public const int TicksPerDay = 60000;

        /// <summary>Places kept at most: the oldest go first.</summary>
        public const int Max = 24;

        /// <summary>A mark is kept as long as the news is: heard lately's keep time (OutsiderTalkMath.HeardDays).</summary>
        public const float KeepDays = 60f;

        /// <summary>The last part of its time a mark fades over (a fifth).</summary>
        public const float FadeShare = 0.2f;

        /// <summary>The faintest a mark is drawn, just before it goes.</summary>
        public const float MinAlpha = 0.25f;

        /// <summary>A news fact that names a place (a site, a world event), as the facts service writes it ("site:40", "ws:7").</summary>
        public static bool NamesPlace(string fact) =>
            fact != null && (fact.StartsWith("site:", StringComparison.Ordinal) || fact.StartsWith("ws:", StringComparison.Ordinal));

        /// <summary>The site's world object id or the world event's id a fact names, and which; false for anything else.</summary>
        public static bool ParseFact(string fact, out bool site, out int id)
        {
            site = false;
            id = -1;
            if (!NamesPlace(fact)) return false;
            site = fact.StartsWith("site:", StringComparison.Ordinal);
            return int.TryParse(fact.Substring(site ? 5 : 3), out id) && id >= 0;
        }

        /// <summary>Past its keep time (or heard in a time that hasn't come: a save from elsewhere).</summary>
        public static bool Gone(int tick, int now, float keepDays) => tick > now || now - tick > Math.Max(1f, keepDays) * TicksPerDay;

        /// <summary>How strongly a mark is drawn: whole until the last fifth of its time, then fading to <see cref="MinAlpha"/>.</summary>
        public static float Alpha(int tick, int now, float keepDays)
        {
            float keep = Math.Max(1f, keepDays) * TicksPerDay;
            float age = Math.Max(0f, now - tick);
            float fadeFrom = keep * (1f - FadeShare);
            if (age <= fadeFrom) return 1f;
            float t = Math.Min(1f, (age - fadeFrom) / (keep * FadeShare));
            return 1f - t * (1f - MinAlpha);
        }

        /// <summary>
        /// Where a place heard of goes in the list: the index of the same place (its key: the place's card in text form, "s40" a
        /// site, "t55" a town, "w12345" a tile) to be brought up to date with the newest telling, or -1 for a new one.
        /// </summary>
        public static int Same(IList<HeardPlaceFacts> list, string key)
        {
            if (list == null || key == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == key)
                    return i;
            return -1;
        }

        /// <summary>How many of the oldest to let go so a new one fits under <see cref="Max"/>.</summary>
        public static int Over(int count) => Math.Max(0, count + 1 - Max);
    }
}
