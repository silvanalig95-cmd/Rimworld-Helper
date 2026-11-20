using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the content update's variety for the world (part B of docs/content-update-proposals.md: B6 the two
    /// scarcities, B7 the season of games, B8 the pardon, the six world events and six camps of this round). No game types
    /// here: Tools/Tests checks them. The parts are in World/Variety/.
    /// </summary>
    public static class VarietyMath
    {
        // ================= Their age, as a world event's lines are tagged =================

        /// <summary>
        /// The tag a world event of this round gives its people's own age ([theirTribal], [theirMedieval],
        /// [theirIndustrial], [theirSpacer]), so each age has its own form of the event: the gathering games, a tourney, an
        /// athletics meet, a league season. From an age's line tag (AgesMath.Tag); null for none.
        /// </summary>
        public static string TheirAgeTag(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return "theirTribal";
                case "medieval": return "theirMedieval";
                case "industrial": return "theirIndustrial";
                case "spacer": return "theirSpacer";
                default: return null;
            }
        }

        /// <summary>Every tag <see cref="TheirAgeTag"/> gives, for the tests that check each age has its own line.</summary>
        public static readonly string[] TheirAgeTags = { "theirTribal", "theirMedieval", "theirIndustrial", "theirSpacer" };

        // ================= Away content: near enough =================

        /// <summary>Away content only goes this far (docs/quests-proposals.md: within three tiles): a founding day the colony can send one of its own to.</summary>
        public const float NearTiles = 3f;

        public static bool Near(float tiles) => tiles >= 0f && tiles <= NearTiles;

        // ================= The new camps =================

        /// <summary>Mushroom pickers come in any season within this many days of the colony's own mushrooms after rain (else in the fall).</summary>
        public static bool AfterFlush(float daysAgo, float within) => daysAgo >= 0f && daysAgo <= within;

        /// <summary>One stay of the mushroom pickers in four, one of them eats a cap that looked right and wasn't (the game's food poisoning, on them).</summary>
        public const float BadCapChance = 0.25f;

        /// <summary>The star-watchers lie out under the sky these hours of the night (from dusk to one), and sleep after.</summary>
        public static bool StarHour(int hour) => hour >= 20 || hour < 1;

        // ================= B24. A stud visit =================

        /// <summary>Their male standing with the colony's females is the likelier way round (their bloodline is what they want spread).</summary>
        public const float StudTheirsWeight = 1.5f;

        /// <summary>How long their animal stands in the colony's pen: five to seven days.</summary>
        public static float StudDays(float roll) => 5f + Clamp01(roll) * 2f;

        /// <summary>The fee for a young one of his: about a seventh of his worth, in tens, from 30 to 200 silver.</summary>
        public static int StudFee(float marketValue) => Tens(Math.Max(30f, Math.Min(200f, marketValue / 7f)));

        /// <summary>The silver the colony asks for his keep instead (the second answer): half the fee, at least 20.</summary>
        public static int StudKeep(int fee) => Tens(Math.Max(20f, fee * 0.5f));

        /// <summary>Their goodwill for a stand kept to its end, and what losing their animal costs.</summary>
        public const int StudGoodwill = 8, StudPenalty = 10;

        /// <summary>When the young one comes: the rest of her gestation from where she is in it (0 to 1), and two to five days on the road.</summary>
        public static int YoungDueTicks(float progress, float gestationDays, float roll) =>
            (int)(Math.Max(0f, 1f - Clamp01(progress)) * Math.Max(1f, gestationDays) * DayTicks + (2f + Clamp01(roll) * 3f) * DayTicks);

        public const int DayTicks = 60000;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static int Tens(float v) => (int)Math.Round(v / 10f) * 10;

        // ================= B6. Cloth gone scarce =================

        /// <summary>Cloth goes scarce more readily this long after a sickness in the herds or markets closed ended.</summary>
        public const float ClothAfterDays = 40f;

        /// <summary>How much likelier cloth goes scarce in that while.</summary>
        public const float ClothAfterFactor = 3f;

        /// <summary>The world events after which cloth goes scarce more readily: the flocks lost before the shearing, the bales gone elsewhere.</summary>
        public static readonly string[] ClothAfter = { "Council_WS_Murrain", "Council_WS_Embargo" };

        /// <summary>The weight factor on cloth gone scarce: <see cref="ClothAfterFactor"/> when one of those ended within <see cref="ClothAfterDays"/> days and wasn't followed up yet.</summary>
        public static float ClothFactor(bool recentlyAfter) => recentlyAfter ? ClothAfterFactor : 1f;

        /// <summary>Whether a world event that ended <paramref name="daysAgo"/> days ago counts for cloth: one of <see cref="ClothAfter"/>, ended not quietly, within the while.</summary>
        public static bool CountsForCloth(string defName, bool quietly, float daysAgo) =>
            defName != null && Array.IndexOf(ClothAfter, defName) >= 0 && !quietly && daysAgo >= 0f && daysAgo <= ClothAfterDays;

        // ================= B7. A season of games =================

        /// <summary>The colony's games day: this many days' notice before the young people come.</summary>
        public const float GamesDayNoticeDays = 2f;

        /// <summary>
        /// How many young people of each of the two peoples come to the games day (each walks in with one or two friends,
        /// the feast's own retinue): two of the first and one of the second, or three of the first with no second people.
        /// The feast takes three guests at most (FeastMath.MaxGuests).
        /// </summary>
        public static void GamesGuests(bool second, out int first, out int other)
        {
            first = second ? 2 : 3;
            other = second ? 1 : 0;
        }

        /// <summary>The ages of the young people who come to a games day.</summary>
        public const float GamesYoungFrom = 16f, GamesYoungTo = 25f;

        /// <summary>The contests of a games day, in order: the bouts, the race, and the shooting match where the colony has targets.</summary>
        public static List<ContestKind> GamesContests(bool targets)
        {
            var list = new List<ContestKind> { ContestKind.Wrestling, ContestKind.Race };
            if (targets) list.Add(ContestKind.Shooting);
            return list;
        }

        /// <summary>The key a games day is hosted under (Feasts.Host's tag), so its end finds the world event that called it.</summary>
        public static string GamesTag(int situationId) => "games:" + situationId;

        public static bool ParseGamesTag(string tag, out int situationId)
        {
            situationId = -1;
            return tag != null && tag.StartsWith("games:", StringComparison.Ordinal) && int.TryParse(tag.Substring(6), out situationId);
        }

        // ================= B8. A pardon proclaimed =================

        /// <summary>
        /// A pardon can be proclaimed after a kind new head takes their seat (a succession ended with the flag "kind", within
        /// the follow-up's days), or while a holy year runs, by that year's own people.
        /// </summary>
        public static bool PardonPossible(bool afterKindHead, bool holyYearFriendly) => afterKindHead || holyYearFriendly;

        /// <summary>The prisoners a pardon covers: those of its own people only, never a hostile people's, never the colony's own.</summary>
        public static bool PardonCovers(bool ofTheirPeople, bool hostileToColony) => ofTheirPeople && !hostileToColony;
    }
}
