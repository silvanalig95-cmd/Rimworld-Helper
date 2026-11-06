using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the fourth round of life together (the player's request, 6 November 2026), without game types: which
    /// tags of a role are the moment's (the weather, the hour, what someone is doing) rather than who they are, how long a
    /// fine piece or a hurt stays fresh enough to be spoken of, which work two can do side by side, and what a close call
    /// on the map is called once it is a memory. Checked in Tools/Tests/TogetherFourTests.cs.
    /// </summary>
    public static class TogetherFourMath
    {
        /// <summary>
        /// Tags of the moment (Flavor.TagsFor), not of the person (PeopleTags.Identity): the weather, the season, the hour,
        /// the heat or cold, a fight just over, and what they are doing and where. A role's tags that name one of these are
        /// matched against the moment as well as the person; a role whose tags name none is matched as before.
        /// </summary>
        public static readonly HashSet<string> MomentTags = new HashSet<string>
        {
            "rain", "snow", "fog", "storm", "clear", "hot", "cold", "spring", "summer", "fall", "winter", "morning", "day", "evening", "night",
            "afterraid", "lowmood", "highmood", "hurt", "newcomer", "grieving", "inbed", "eating", "relaxing", "working", "outside", "indoors", "inownroom"
        };

        /// <summary>A prefix of a moment's tag: the work they are at (work:Mining), the weather by name (weather:Rain).</summary>
        public static readonly string[] MomentPrefixes = { "work:", "weather:" };

        private static readonly Dictionary<string, bool> needs = new Dictionary<string, bool>();

        /// <summary>
        /// Whether a tag expression ("rain|snow", "work:Mining,!lowmood") names a tag of the moment, so it is matched against
        /// the moment's tags; worked out once per expression.
        /// </summary>
        public static bool NeedsMoment(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            if (needs.TryGetValue(expr, out bool known)) return known;
            bool found = false;
            foreach (var part in expr.Split(',', '|'))
            {
                string t = part.Trim();
                if (t.StartsWith("!", StringComparison.Ordinal)) t = t.Substring(1).Trim();
                if (t.Length == 0) continue;
                if (MomentTags.Contains(t)) found = true;
                foreach (var pre in MomentPrefixes)
                    if (t.StartsWith(pre, StringComparison.Ordinal))
                        found = true;
                if (found) break;
            }
            if (needs.Count < 2000) needs[expr] = found;
            return found;
        }

        /// <summary>The least quality a piece must be to be shown round and spoken of: excellent (the game's QualityCategory 4).</summary>
        public const int FineQuality = 4;

        /// <summary>How long a fine piece is talked of, in days, unless an event asks for its own.</summary>
        public const float FineDays = 2f;

        /// <summary>Whether a piece made at <paramref name="madeTick"/> is still fresh at <paramref name="now"/> (within <paramref name="days"/>).</summary>
        public static bool FreshWithin(int madeTick, int now, float days, int ticksPerDay = 60000) =>
            madeTick >= 0 && now >= madeTick && now - madeTick <= days * ticksPerDay;

        /// <summary>Whether a quality (the game's QualityCategory as a number, 0 awful to 6 legendary) is fine enough to show round.</summary>
        public static bool Fine(int quality) => quality >= FineQuality;

        /// <summary>A hurt of everyday life <paramref name="daysSince"/> days ago is still spoken of within <paramref name="days"/> (and it was one at all).</summary>
        public static bool HurtLately(float daysSince, float days) => days > 0f && daysSince >= 0f && daysSince <= days;

        /// <summary>
        /// Work two can do side by side and race at, or keep each other company over: the field, the rock, the build, the
        /// hauling, the cleaning, the woodpile, the stove, the bench. Not the doctor's work (a patient isn't a race), research
        /// or art (each at their own bench, alone in it), hunting (apart by its nature) or the wardens' (with a captive).
        /// </summary>
        public static readonly HashSet<string> SideBySideWork = new HashSet<string>
        {
            "Growing", "PlantCutting", "Mining", "Construction", "Hauling", "Cleaning", "Cooking", "Crafting", "Smithing", "Tailoring", "Handling"
        };

        /// <summary>Whether two at work are at the same work, one of <paramref name="allowed"/> (empty: any of <see cref="SideBySideWork"/>).</summary>
        public static bool SameWork(string a, string b, ICollection<string> allowed)
        {
            if (string.IsNullOrEmpty(a) || a != b) return false;
            return allowed != null && allowed.Count > 0 ? allowed.Contains(a) : SideBySideWork.Contains(a);
        }

        /// <summary>
        /// The danger a close call on the map was (MapDangers.Wound's key), as the memory calls it: CC_Danger_Rockfall is
        /// "rock", the falling tree "tree", and so on. Null for a key that isn't a danger's.
        /// </summary>
        public static string CloseCallKind(string key)
        {
            switch (key)
            {
                case "CC_Danger_Rockfall": return "Rock";
                case "CC_Danger_TreeCrush": return "Tree";
                case "CC_Danger_StampBurn": return "Smoulder";
                case "CC_Danger_BlastStone": return "Shell";
                case "CC_Danger_ScrapBurn": return "Scrap";
                case "CC_Danger_GroundGave":
                case "CC_Danger_GroundCrack": return "Ground";
                default: return null;
            }
        }

        /// <summary>The kinds of close call on the map a memory may name (a Keyed CC_Times_Close_KIND for each).</summary>
        public static readonly string[] CloseCallKinds = { "Rock", "Tree", "Smoulder", "Shell", "Scrap", "Ground" };

        /// <summary>
        /// The doctor and the patient share the memory of an illness nursed only when the doctor tended it twice or more
        /// (HealthTracker's own rule for who pulled a patient through) and the illness was a dangerous one.
        /// </summary>
        public const int NursedTends = 2;

        /// <summary>How many of the founding party each shares the founding with (the closest of them), so a big party doesn't fill everyone's memories.</summary>
        public const int FoundingEach = 3;

        /// <summary>
        /// Each new social event is a share of the day's draw, never an extra one: the day's count comes from the colony's size
        /// (SocialEvents.Daily) and doesn't read the defs.
        /// </summary>
        public static float SocialEventsADay(int colonists, float frequency = 1f)
        {
            float x = colonists / 5f;
            if (x < 0.4f) x = 0.4f;
            if (x > 2f) x = 2f;
            return x * frequency;
        }
    }
}
