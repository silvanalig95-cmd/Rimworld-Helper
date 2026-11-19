using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S10, the town's well ran dry (StorylineWell.cs; docs/quests-proposals.md). The Deepwell Union's town, or any town near
    /// the colony: 1. its well ran dry, and it asks for water or food, collected by its carts. 2. It asks the colony to lend a
    /// builder or a miner to dig a new one: the colonist goes with their foreman for some days and comes back more skilled.
    /// 3. With the new well found, the town grows and remembers the help; without it, some of its people come to the colony as
    /// refugees, and the colony takes them in or turns them away.
    /// </summary>
    public sealed class WellFlow : StorylineFlow
    {
        public const string Key = "well";

        public override string Kind => Key;

        public override int Number => 10;

        public override string[] Parts => new[] { "water", "digger", "refugees" };

        public override string[] Endings => new[] { "declined", "nohelp", "grew", "theirown", "lostdigger", "takenin", "turnedaway", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "water": return new[] { "sent", "partly", "none" };
                case "digger": return new[] { "found", "notfound", "lost" };
                case "refugees": return new[] { "takenin" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "none" || outcome == "lost";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "water":
                    if (outcome == "sent" || outcome == "partly") return To("digger", 2f, 5f, outcome);
                    if (outcome == "none") return End("nohelp");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "digger":
                    if (outcome == "found") return End("grew", 1f, 3f);
                    if (outcome == "notfound") return To("refugees", 3f, 7f, "notfound");
                    if (outcome == "lost") return End("lostdigger");
                    // Nobody lent: they dig on alone, and now and then they find it themselves.
                    if (outcome == StorylineMath.Declined)
                        return Safe(roll) < AloneChance ? End("theirown", 2f, 5f) : To("refugees", 4f, 8f, "nohand");
                    return End(StorylineMath.Faded);
                case "refugees":
                    if (outcome == "takenin") return End("takenin");
                    return End("turnedaway");
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "grew" || ending == "theirown" || ending == StorylineMath.Gone;

        // ================= The numbers =================

        /// <summary>Meals asked for: twelve to thirty, by the colony's size.</summary>
        public static int Meals(int colonists) => Clamp(10 + colonists * 2, 12, 30);

        /// <summary>Water asked for, where the colony keeps it (Dubs Bad Hygiene's bottled water): twenty to forty.</summary>
        public static int Water(int colonists) => Clamp(16 + colonists * 3, 20, 40);

        /// <summary>Days to have it ready, once agreed.</summary>
        public const float WaterMinDays = 4f, WaterMaxDays = 6f;

        /// <summary>What they pay: its worth and a fifth more, rounded to tens; part of it, its plain worth.</summary>
        public static int Pay(int count, float unit) => Math.Max(40, StorylineMath.Round10(Math.Max(0, count) * Safe(unit) * 1.2f));

        public static int PartPay(int count, float unit) => Math.Max(10, StorylineMath.Round10(Math.Max(0, count) * Safe(unit)));

        /// <summary>"sent" (all of it), "partly" (half or more), "none" (less).</summary>
        public static string WaterOutcome(int have, int count) => have >= count ? "sent" : have * 2 >= count ? "partly" : "none";

        /// <summary>The one lent: Mining or Construction at least this good.</summary>
        public const int MinSkill = 6;

        /// <summary>Days they are away digging (with a day's walk each way in the words).</summary>
        public const float AwayMinDays = 4f, AwayMaxDays = 6f;

        /// <summary>What they learn while away, each day: in the skill they were lent for, and a little of the other.</summary>
        public const float XpPerDay = 1500f, OtherXpPerDay = 400f;

        /// <summary>The new well is found this often, by the lent colonist's skill: about two in five at 6, nine in ten at 16 and up.</summary>
        public static float FoundChance(int skill) => Clamp(0.4f + 0.05f * (skill - MinSkill), 0.4f, 0.9f);

        /// <summary>Their pay for the lent hand: a day's wage each day away, by the skill.</summary>
        public static int Wage(float days, int skill) => StorylineMath.Round10(Math.Max(0f, days) * (25f + 3f * Clamp(skill, 0, 20)));

        /// <summary>No hand lent: they find a new well themselves this often.</summary>
        public const float AloneChance = 0.3f;

        /// <summary>The refugees: two to four.</summary>
        public static int Refugees(float roll) => 2 + Clamp((int)Math.Floor(Safe(roll) * 3f), 0, 2);

        /// <summary>Goodwill with the town: the well found with the colony's hand, and refugees turned away.</summary>
        public const int GrewGoodwill = 15, TurnedAwayGoodwill = -8;
    }
}
