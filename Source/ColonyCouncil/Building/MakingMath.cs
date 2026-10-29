using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What the colony could make (Building/Making.cs) as plain rules: whether a batch's ingredients are there, when a
    /// product is plentiful enough not to suggest, how many to set a bill for, the order kinds of product come in, and
    /// when a harvest counts as unused.
    /// </summary>
    public static class MakingMath
    {
        /// <summary>A product the colony has this many of already isn't suggested.</summary>
        public const int Plenty = 60;
        /// <summary>A harvest or find under this many isn't worth a bench.</summary>
        public const int LeastUnused = 20;
        /// <summary>An ingredient filter allowing this many kinds or fewer is a specific use (hops for wort), not "any raw food".</summary>
        public const int SpecificFilter = 10;
        /// <summary>How many recipes to show for one bench, and for clothes and weapons at one bench.</summary>
        public const int PerBench = 6, PerBenchWearable = 2;

        /// <summary>Every part of one batch is in stock.</summary>
        public static bool BatchPossible(IList<(float have, float need)> parts)
        {
            if (parts == null || parts.Count == 0) return false;
            foreach (var p in parts) if (p.have < p.need) return false;
            return true;
        }

        public static bool WorthShowing(int productStock, int plenty = Plenty) => productStock < plenty;

        /// <summary>How many to make before the bill rests: enough of a staple, a few of anything dear.</summary>
        public static int TargetFor(string kind)
        {
            switch (kind)
            {
                case "blocks": return 150;
                case "textiles": return 100;
                case "fuel": return 100;
                case "feed": return 100;
                case "meals": return 20;
                case "drugs": return 20;
                case "medicine": return 10;
                case "components": return 8;
                case "apparel": return 2;
                case "weapons": return 1;
                default: return 10;
            }
        }

        /// <summary>The order kinds of product are shown in: what feeds and heals first, what arms last.</summary>
        public static int KindRank(string kind)
        {
            switch (kind)
            {
                case "meals": return 0;
                case "medicine": return 1;
                case "components": return 2;
                case "blocks": return 3;
                case "textiles": return 4;
                case "fuel": return 5;
                case "feed": return 6;
                case "drugs": return 7;
                case "apparel": return 8;
                case "weapons": return 9;
                default: return 10;
            }
        }

        public static bool IsWearable(string kind) => kind == "apparel" || kind == "weapons";

        /// <summary>A harvest or find is unused when there's a fair amount and no bench here puts it to its own use.</summary>
        public static bool Unused(int stock, bool anyBenchUsesIt, int least = LeastUnused) => stock >= least && !anyBenchUsesIt;
    }
}
