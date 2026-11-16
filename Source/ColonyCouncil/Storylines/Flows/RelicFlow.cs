using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S5, the pilgrims' relic (StorylineRelic.cs; docs/quests-proposals.md). Pilgrims carrying a relic rest at the colony;
    /// in the night it is taken, by one of their own or a stranger, and the thief runs for the edge of the map. Caught, the
    /// pilgrims ask the colony to judge the thief (handed over, punished here, or let go). Got away, the pilgrims blame the
    /// colony: the colony may go to the thief's camp if it is within three tiles, else the pilgrims go after the thief
    /// themselves with the colony's food. With the relic back, they ask to leave it at the colony's shrine for a year.
    /// </summary>
    public sealed class RelicFlow : StorylineFlow
    {
        public const string Key = "relic";

        public override string Kind => Key;

        public override int Number => 5;

        public override string[] Parts => new[] { "rest", "trail", "shrine" };

        public override string[] Endings => new[] { "declined", "harmed", "lost", "home", "shrine", "gone", "faded" };

        /// <summary>How the thief's night ends with the relic back: handed over, punished here, let go, killed, or gone without it.</summary>
        public static readonly string[] Caught = { "handed", "punished", "letgo", "killed", "dropped" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "rest": return new[] { "handed", "punished", "letgo", "killed", "dropped", "escaped", "harmed" };
                case "trail": return new[] { "recovered", "notfound" };
                case "shrine": return new[] { "kept", "noshrine" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "escaped" || outcome == "harmed" || outcome == "notfound" || outcome == "noshrine";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "rest":
                    if (Array.IndexOf(Caught, outcome) >= 0) return To("shrine", 2f, 5f, outcome);
                    if (outcome == "escaped") return To("trail", 1f, 2f, "escaped");
                    if (outcome == "harmed") return End("harmed");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "trail":
                    if (outcome == "recovered") return To("shrine", 2f, 5f, "recovered");
                    // Not found, or the colony would not help: the relic is lost to them.
                    return End("lost");
                case "shrine":
                    if (outcome == "kept") return End("shrine");
                    return End("home");
            }
            return End(StorylineMath.Faded);
        }

        // ================= The numbers =================

        /// <summary>The pilgrims, besides the one who carries the relic: two to three.</summary>
        public static int Pilgrims(int colonists, float roll) => Clamp(2 + (Safe(roll) >= 0.5f ? 1 : 0), 2, 3);

        /// <summary>Days they rest at the colony.</summary>
        public const float RestMinDays = 2.4f, RestMaxDays = 3f;

        /// <summary>The thief is one of their own this often; else a stranger who asked to share their fire that evening.</summary>
        public const float OwnThiefChance = 0.5f;

        /// <summary>The night of the theft: the stay's second night, at this hour or after (everyone asleep).</summary>
        public const int TheftNight = 2, TheftHour = 1;

        /// <summary>A stranger comes to their fire in the evening before the theft, at this hour.</summary>
        public const int StrangerHour = 19;

        /// <summary>The thief's camp: on the world map this many tiles from the colony at most (the one away part, docs/quests-proposals.md rule 2).</summary>
        public const int CampMinTiles = 1, CampMaxTiles = 3;

        /// <summary>Days the thief's camp stays before they move on.</summary>
        public const float CampDays = 8f;

        /// <summary>Meals for the pilgrims' own search: four each for the road.</summary>
        public static int SearchMeals(int pilgrims) => Math.Max(6, Clamp(pilgrims, 1, 6) * 4);

        /// <summary>Days the pilgrims' own search takes before they send word.</summary>
        public const float SearchMinDays = 3f, SearchMaxDays = 5f;

        /// <summary>The pilgrims find the thief this often with the colony's food for the road, and without it.</summary>
        public static float FindChance(bool supplied) => supplied ? 0.7f : 0.35f;

        /// <summary>The relic stays at the colony's shrine for a year.</summary>
        public const float ShrineDays = 60f;

        /// <summary>Pilgrims come to see it once a season while it stays: three visits, then its keeper comes for it.</summary>
        public const int Visits = 3;

        public const float VisitEvery = 15f;

        /// <summary>The pilgrims who come each time: two to three.</summary>
        public static int VisitCount(float roll) => Safe(roll) >= 0.5f ? 3 : 2;

        /// <summary>Their offering at the shrine each visit, in silver before the colony's wealth.</summary>
        public const float OfferingMin = 40f, OfferingMax = 90f;

        /// <summary>Goodwill with the pilgrims' people for each judgement (the thief one of their own or not).</summary>
        public static int JudgeGoodwill(string judgement, bool ownThief)
        {
            switch (judgement)
            {
                case "handed": return 10;
                case "punished": return ownThief ? 2 : 6;
                case "letgo": return ownThief ? 4 : -4;
                case "killed": return ownThief ? -2 : 4;
                case "dropped": return 4;
            }
            return 0;
        }
    }
}
