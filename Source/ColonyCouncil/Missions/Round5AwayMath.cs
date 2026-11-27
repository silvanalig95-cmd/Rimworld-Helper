using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of content round 5, the colony out in the world (docs/content-round-5-away.md; Missions/Round5Away.cs,
    /// Defs/MissionDefs/Rimfolk_Round5_Missions.xml), without game types so the tests can check them: what the colony keeps
    /// enough of for a neighbour's merchants to order it, how much they order, and what they pay.
    /// </summary>
    public static class Round5AwayMath
    {
        private static int Tens(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        /// <summary>A number as the missions round theirs (VarietyUtil.Round): tens from a hundred, fives from twenty.</summary>
        public static int Nice(int n) =>
            n >= 100 ? (int)Math.Round(n / 10f, MidpointRounding.AwayFromZero) * 10 : n >= 20 ? (int)Math.Round(n / 5f, MidpointRounding.AwayFromZero) * 5 : n;

        // ================= R5A-M3. An order from their merchants =================

        /// <summary>At least this many of a thing in the colony's stores for merchants to have heard of it.</summary>
        public const int OrderMinStock = 10;

        /// <summary>And worth at least this much in all.</summary>
        public const float OrderMinWorth = 120f;

        /// <summary>They order about as much as the colony keeps, give or take a third.</summary>
        public const float OrderLow = 0.7f, OrderHigh = 1.3f;

        /// <summary>Never more than this much silver's worth, at a colony of middling wealth (times its wealth factor).</summary>
        public const float OrderMaxWorth = 1200f;

        /// <summary>The fewest of a thing they order.</summary>
        public const int OrderMinCount = 5;

        /// <summary>The price: half again the goods' plain worth (the delivery's own pays on its terms when it comes short).</summary>
        public const float OrderPayFactor = 1.5f;

        /// <summary>Enough of it, worth enough, for an order.</summary>
        public static bool OrderWorth(int have, float unit) => have >= OrderMinStock && unit > 0f && have * unit >= OrderMinWorth;

        /// <summary>How likely each of the colony's goods is to be the one ordered: by its worth in the stores, softened.</summary>
        public static float OrderWeight(int have, float unit) => have <= 0 || unit <= 0f ? 0f : (float)Math.Sqrt(have * unit);

        /// <summary>
        /// How many they order of a thing the colony keeps <paramref name="have"/> of, each worth <paramref name="unit"/>:
        /// about as many as it keeps (<paramref name="roll"/> from 0 to 1 picks between a third less and a third more), never
        /// more than <see cref="OrderMaxWorth"/> at the colony's <paramref name="wealth"/> factor, rounded as the missions
        /// round, five at least. None when the colony keeps too little of it.
        /// </summary>
        public static int OrderCount(int have, float unit, float roll, float wealth)
        {
            if (!OrderWorth(have, unit)) return 0;
            float r = Math.Max(0f, Math.Min(1f, roll));
            float n = have * (OrderLow + (OrderHigh - OrderLow) * r);
            float cap = OrderMaxWorth * Math.Max(0.5f, wealth) / unit;
            n = Math.Min(n, cap);
            return Math.Max(OrderMinCount, Nice((int)Math.Round(n, MidpointRounding.AwayFromZero)));
        }

        /// <summary>What they pay for the whole order, in tens, twenty at least.</summary>
        public static int OrderPay(float worth) => Math.Max(20, Tens(worth * OrderPayFactor));

        /// <summary>The order's words by what is ordered: a goal's group, or "goods" for one thing a recipe makes.</summary>
        public static string OrderTag(string what)
        {
            switch (what)
            {
                case "meals": return "food";
                case "textiles": return "cloth";
                case "medicine": return "medicine";
                case "drinks": return "drink";
                case "blocks": return "stone";
                default: return "goods";
            }
        }

        /// <summary>The goal groups an order may be for, before any one thing the colony makes.</summary>
        public static readonly string[] OrderGroups = { "meals", "textiles", "medicine", "drinks", "blocks" };
    }
}
