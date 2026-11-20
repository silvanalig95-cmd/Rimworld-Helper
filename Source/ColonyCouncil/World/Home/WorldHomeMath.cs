using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the world brought home (the content update's integrations, part B: B1, B2, B3, B23, B25, B26 and
    /// B32 of docs/content-update-proposals.md). No game types here: Tools/Tests checks them. The parts are in World/Home/.
    /// </summary>
    public static class WorldHomeMath
    {
        // ================= B1. World events in the peoples' own ways =================

        /// <summary>
        /// A line of a world event's own paragraph for its people (WorldSituationDef.theirs, theirEnds) names a calling of
        /// Rimfolk's peoples among its tags ([their:healers]), so it is never told of anyone else.
        /// </summary>
        public static bool ForAPeople(IEnumerable<string> tags) => tags != null && tags.Any(t => t != null && PeoplesMath.Tags.Contains(t.Trim()));

        // ================= B2. World events steer the quests and storylines offered =================

        /// <summary>The tags a steered event's first letter is picked by: the world event's kind and name, and whether it is at the asker's own people.</summary>
        public static IEnumerable<string> SteerTags(string kind, string defName, bool theirs)
        {
            if (!string.IsNullOrEmpty(kind)) yield return "ws:" + kind;
            if (!string.IsNullOrEmpty(defName)) yield return "wsdef:" + defName;
            yield return "ws:steered";
            if (theirs) yield return "ws:theirs";
        }

        /// <summary>A line written for a world event's season: one of its tags names a kind or a world event ([ws:sickness], [wsdef:Council_WS_Plague]).</summary>
        public static bool WorldTagged(IEnumerable<string> tags) =>
            tags != null && tags.Any(t => t != null && (t.StartsWith("ws:", StringComparison.Ordinal) || t.StartsWith("wsdef:", StringComparison.Ordinal)) && !t.StartsWith("ws:theirs", StringComparison.Ordinal));

        /// <summary>The world event's own people are this much likelier to be the ones who ask while it steers the asking (the fever's town asks for the medicine).</summary>
        public const float SteeredPeople = 4f;

        /// <summary>A people's weight for an event a world event steers: its own people's <see cref="SteeredPeople"/> times, any other's as it was.</summary>
        public static float SteerWeight(bool theirs) => theirs ? SteeredPeople : 1f;

        // ================= B3. Messengers tell the road's news =================

        /// <summary>A world event this close to the colony or to the messenger's town (world tiles) is news on their road.</summary>
        public const float NearEnd = 5f;

        /// <summary>
        /// A world event's place lies on the road between the colony and the messenger's town: near either end, or no more
        /// than a third out of the way (and two tiles) going past it. NaN distances (no place known) are never on it.
        /// </summary>
        public static bool OnRoad(float homeToEvent, float townToEvent, float homeToTown)
        {
            if (float.IsNaN(homeToEvent) || float.IsNaN(townToEvent) || homeToEvent < 0f || townToEvent < 0f) return false;
            if (homeToEvent <= NearEnd || townToEvent <= NearEnd) return true;
            if (float.IsNaN(homeToTown) || homeToTown < 0f) return false;
            return homeToEvent + townToEvent <= homeToTown * 1.33f + 2f;
        }

        /// <summary>How often a messenger with road news to tell tells it as they set the bag down (the rest of the time, their usual word).</summary>
        public const float RoadNewsChance = 0.75f;

        /// <summary>The tags a road's news is picked by: the moment, the event's kind and name, and whether it is at the messenger's own people.</summary>
        public static IEnumerable<string> RoadTags(string kind, string defName, bool theirs)
        {
            yield return "road";
            if (!string.IsNullOrEmpty(kind)) yield return "ws:" + kind;
            if (!string.IsNullOrEmpty(defName)) yield return "wsdef:" + defName;
            if (theirs) yield return "ws:theirs";
        }

        // ================= B25 and B26. Only in a world event's season =================

        /// <summary>
        /// Something that comes only while a world event of one of these kinds runs (an encounter's or a council question's
        /// <c>situations</c>): none given fits always; else one of the running kinds must be listed.
        /// </summary>
        public static bool SeasonFits(IList<string> kinds, IEnumerable<string> running)
        {
            if (kinds == null || kinds.Count == 0) return true;
            return running != null && running.Any(r => r != null && kinds.Contains(r));
        }

        // ================= B26. Council questions the world brings home =================

        /// <summary>Kept apart from travellers: a plague's carriers at the gate and couriers come this much less often.</summary>
        public const float ApartCarrierFactor = 0.5f;

        /// <summary>The chance a carrier's occasion comes, kept apart: half of what it was.</summary>
        public static float ApartChance(float chance, bool apart) => apart ? Clamp01(chance) * ApartCarrierFactor : Clamp01(chance);

        /// <summary>A group of visitors or traders kept at arm's length thinks this much less of the colony (once a group).</summary>
        public const int ApartGoodwill = -1;

        /// <summary>A vigil day is kept once a week while the holy year lasts: every this many days.</summary>
        public const int VigilEveryDays = 7;

        /// <summary>The first vigil day comes this many days after the vow (the next morning).</summary>
        public const int FirstVigilDays = 1;

        /// <summary>The vigil starts in the morning, between these hours, and lasts most of the working day.</summary>
        public const int VigilFrom = 7, VigilTo = 10;

        public const float VigilHoursMin = 7f, VigilHoursMax = 8f;

        /// <summary>The day after <paramref name="lastDay"/>'s vigil that the next one is due (game days).</summary>
        public static int NextVigilDay(int lastDay) => lastDay + VigilEveryDays;

        /// <summary>
        /// How strongly a colonist belongs with one side of a war between neighbours: family among them counts most, then the
        /// people's good memory of them by name, then a faith shared with them. 0: no tie to that side.
        /// </summary>
        public static int SideTie(bool family, bool remembered, bool sameFaith) => (family ? 3 : 0) + (remembered ? 2 : 0) + (sameFaith ? 1 : 0);

        /// <summary>Backing one side of a war: goodwill with it, and with the other.</summary>
        public const int BackedGoodwill = 5, OtherGoodwill = -4;

        /// <summary>The flags on the war a backing sets, so that only the backed side's asks come (the other side's beats forbid it).</summary>
        public static string SideFlag(bool first) => first ? "backFaction" : "backEnemy";

        /// <summary>Mourning colours: goodwill with the mourning people, once.</summary>
        public const int MourningGoodwill = 4;

        /// <summary>The mourning colours, dark grey: red, green, blue.</summary>
        public static readonly float[] MourningColour = { 0.22f, 0.22f, 0.24f };

        /// <summary>How many of the colony's clothes are dyed at most (each colonist's outermost body layer).</summary>
        public const int MaxDyed = 40;

        // ================= B32. The region's great events in the colony's book =================

        /// <summary>
        /// The gods the colony's people take a great world event for, by the world event (its defName): the fever for the Soft
        /// Mother's or the Hunger's, the comet for the sky's, a bumper year for the Rootmother's or the Giver's... Empty: not
        /// one of the great ones.
        /// </summary>
        public static string[] GreatGods(string defName)
        {
            switch (defName)
            {
                case "Council_WS_Plague": return new[] { "Morel", "Undertow" };
                case "Council_WS_Comet": return new[] { "Cirrus", "Selene" };
                case "Council_WS_Bumper": return new[] { "Hollin", "Helios" };
                case "Council_WS_GreatFire": return new[] { "Smoulder", "Helios" };
                case "Rimfolk_WS_ColdYear": return new[] { "Hush", "Helios" };
                case "Council_WS_Famine": return new[] { "Undertow", "Helios" };
                case "Council_WS_Flood": return new[] { "Murmur" };
                case "Council_WS_Drought": return new[] { "Helios", "Murmur" };
                case "Council_WS_HardWinter": return new[] { "Hush" };
                case "Council_WS_Murrain": return new[] { "Bellwether", "Morel" };
                // The content update's variety (World/Variety/): stars falling over the region, as the comet is the sky's.
                case "Rimfolk_WS_FallingStars": return new[] { "Cirrus", "Selene" };
                default: return new string[0];
            }
        }

        /// <summary>Every great world event the book may keep (for the tests and the dev action).</summary>
        public static readonly string[] Great =
        {
            "Council_WS_Plague", "Council_WS_Comet", "Council_WS_Bumper", "Council_WS_GreatFire", "Rimfolk_WS_ColdYear", "Council_WS_Famine",
            "Council_WS_Flood", "Council_WS_Drought", "Council_WS_HardWinter", "Council_WS_Murrain", "Rimfolk_WS_FallingStars"
        };

        /// <summary>A devout colonist sets a great world event down as a god's doing this often (once for each, when it is new).</summary>
        public const float GreatChance = 0.65f;

        /// <summary>It is new for this many days: past them, nobody sets it down any more.</summary>
        public const float GreatNewDays = 3f;

        /// <summary>Whether the moment for a great world event's word has passed: rolled once, or too old.</summary>
        public static bool GreatPast(bool rolled, float daysIn) => rolled || daysIn > GreatNewDays;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : float.IsNaN(v) ? 0f : v;
    }
}
