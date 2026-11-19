using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S12, the lost founder's grave (StorylineGrave.cs; docs/quests-proposals.md). A people with a long past, the region's:
    /// 1. their elders say one of the people who founded them is buried on the colony's land, and ask the colony to find the
    /// grave in a marked area and mark it (not found in time, one of theirs comes and finds it). 2. On a set day mourners
    /// come to the grave, and the colony hosts them for the day. Endings: the grave kept, and they come each year and remember
    /// the colony well; built over, or the colony refused them, and they hold it against the colony.
    /// </summary>
    public sealed class GraveFlow : StorylineFlow
    {
        public const string Key = "grave";

        public override string Kind => Key;

        public override int Number => 12;

        public override string[] Parts => new[] { "search", "mourners" };

        public override string[] Endings => new[] { "declined", "kept", "refused", "builtover", "harmed", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "search": return new[] { "found", "theyfound", "builtover" };
                case "mourners": return new[] { "hosted", "poorly", "harmed", "builtover" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "builtover" || outcome == "harmed";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "search":
                    if (outcome == "found" || outcome == "theyfound") return To("mourners", 3f, 6f, outcome);
                    if (outcome == "builtover") return End("builtover");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "mourners":
                    if (outcome == "hosted" || outcome == "poorly") return End("kept", 0f, 0f, outcome);
                    if (outcome == "harmed") return End("harmed");
                    if (outcome == "builtover") return End("builtover");
                    if (outcome == StorylineMath.Declined) return End("refused");
                    return End(StorylineMath.Faded);
            }
            return End(StorylineMath.Faded);
        }

        // ================= The numbers =================

        /// <summary>The marked area's radius (cells, about 150 to search), a look's reach, a look's time, the grave's margin inside the edge: the lost keepsake's own.</summary>
        public const float AreaRadius = 7f, LookRadius = 2.9f, AreaMargin = 2f;

        public const int LookTicks = 2000;

        /// <summary>The area at least this far from the colony's middle.</summary>
        public const float AreaFrom = 22f;

        /// <summary>A look made <paramref name="dx"/>, <paramref name="dz"/> cells from the grave finds it.</summary>
        public static bool Covers(int dx, int dz) => dx * dx + dz * dz <= LookRadius * LookRadius;

        /// <summary>Days to find it, once agreed; then one of theirs comes and searches for this many hours.</summary>
        public const float SearchMinDays = 4f, SearchMaxDays = 6f, TheirSearchHours = 8f;

        /// <summary>The mourners: three to five, and the hours they stay at the grave on their day.</summary>
        public static int Mourners(float roll) => 3 + Clamp((int)Math.Floor(Safe(roll) * 3f), 0, 2);

        public const float MournHours = 10f;

        /// <summary>Kept: they come each year (a year of the game), this many years.</summary>
        public const float YearDays = 60f;

        public const int Years = 3;

        /// <summary>Goodwill: the grave found by the colony, the mourners hosted, each year's visit, built over, refused.</summary>
        public const int FoundGoodwill = 8, HostedGoodwill = 10, YearGoodwill = 4, BuiltOverGoodwill = -20, RefusedGoodwill = -12;
    }
}
