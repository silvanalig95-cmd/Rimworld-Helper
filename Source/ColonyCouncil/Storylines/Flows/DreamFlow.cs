using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S6, a dream of the gods (StorylineDream.cs; docs/quests-proposals.md). The colony's own faith, and the gods stay unseen:
    /// a devout colonist dreams of a place on the colony's ground and of something to build there for their god (named only as
    /// the colony knows it). Built and given to the god, the dreamer asks for a watch at the new place one night, and something
    /// happens there that the watcher sees. Then the dream comes back and asks for an offering of a kind at the place: given,
    /// it goes into the colony's book of the gods as the dreamer believes it; refused or let go, the dreamer is troubled for a
    /// while. Nothing of the god's mind is ever shown.
    /// </summary>
    public sealed class DreamFlow : StorylineFlow
    {
        public const string Key = "dream";

        public override string Kind => Key;

        public override int Number => 6;

        public override string[] Parts => new[] { "place", "watch", "offering" };

        public override string[] Endings => new[] { "declined", "unbuilt", "given", "refused", "notgiven", "lost", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "place": return new[] { "built", "notbuilt" };
                case "watch": return new[] { "seen", "unwatched" };
                case "offering": return new[] { "given", "notgiven" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "notbuilt" || outcome == "unwatched" || outcome == "notgiven";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "place":
                    if (outcome == "built") return To("watch", 1f, 3f);
                    if (outcome == "notbuilt") return End("unbuilt");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    // The dreamer's place came to nothing for another reason (the map left, the place gone).
                    return End("lost");
                case "watch":
                    if (outcome == "seen") return To("offering", 2f, 5f, "seen");
                    // Nobody kept the watch, or the colony would not: the dream comes back all the same.
                    if (outcome == "unwatched" || outcome == StorylineMath.Declined) return To("offering", 2f, 5f, "nowatch");
                    return End("lost");
                case "offering":
                    if (outcome == "given") return End("given");
                    if (outcome == "notgiven") return End("notgiven");
                    if (outcome == StorylineMath.Declined) return End("refused");
                    return End("lost");
            }
            return End(StorylineMath.Faded);
        }

        // ================= The numbers =================

        /// <summary>The place the dreamer saw: the middle of a marked area this wide (cells), at least this far from the colony's middle.</summary>
        public const float AreaRadius = 4f, PlaceFrom = 14f;

        /// <summary>Days the colony has to build a place for the god there and give it to the god.</summary>
        public const float PlaceMinDays = 6f, PlaceMaxDays = 9f;

        /// <summary>The watch: this many hours at the place in the night, within this many nights.</summary>
        public const float WatchHours = 2f;

        public const int WatchNights = 3;

        /// <summary>Night, for the watch: from nine in the evening to four in the morning.</summary>
        public const int NightFrom = 21, NightTo = 4;

        public static bool Night(int hour) => hour >= NightFrom || hour < NightTo;

        /// <summary>Someone this close to the place is keeping the watch.</summary>
        public const float WatchWithin = 5f;

        /// <summary>
        /// When in the watch it comes: after this share of the hours, by <paramref name="roll"/> (a half to the whole of it),
        /// so it never comes the moment someone arrives.
        /// </summary>
        public static float SignAt(float roll) => WatchHours * (0.5f + 0.5f * Clamp(Safe(roll), 0f, 1f));

        /// <summary>Days to bring the offering, as every dream's demand of the gods' worship (WorshipMath.DemandDays).</summary>
        public const float OfferingDays = 3f;

        /// <summary>The dreamer: the colony's priest likelier than anyone else devout.</summary>
        public static float DreamerWeight(bool priest, bool psychic) => (priest ? 3f : 1f) * (psychic ? 1.5f : 1f);

        /// <summary>How the dreamer feels about it afterwards, a mood of its own each: honoured, or troubled for a while.</summary>
        public static bool Honoured(string ending) => ending == "given";

        public static bool Troubled(string ending) => ending == "declined" || ending == "unbuilt" || ending == "refused" || ending == "notgiven";
    }
}
