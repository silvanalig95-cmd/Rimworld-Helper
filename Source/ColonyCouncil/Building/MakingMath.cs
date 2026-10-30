using System;
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

        /// <summary>
        /// The bill's count for a colony of so many: three days of meals (two a head a day), medicine two a head, within
        /// bounds; everything else as usual. Zero colonists: the usual.
        /// </summary>
        public static int TargetFor(string kind, int colonists)
        {
            if (colonists <= 0) return TargetFor(kind);
            switch (kind)
            {
                case "meals": return Math.Max(10, Math.Min(60, colonists * 6));
                case "medicine": return Math.Max(8, Math.Min(30, colonists * 2));
                default: return TargetFor(kind);
            }
        }

        /// <summary>
        /// The bill's count when something is wanted (the blueprints are short of it, a bill waits on it): the stock,
        /// what's short and a margin of a fifth (five at least), rounded up to fives; never under the usual.
        /// </summary>
        public static int DemandTarget(int stock, int shortBy, int usual)
        {
            if (shortBy <= 0) return usual;
            int margin = Math.Max(5, shortBy / 5);
            int t = Math.Max(0, stock) + shortBy + margin;
            t = (t + 4) / 5 * 5;
            return Math.Max(usual, t);
        }

        /// <summary>How much of a material the building still lacks beyond the stock.</summary>
        public static int Short(int needed, int stock) => Math.Max(0, needed - Math.Max(0, stock));

        /// <summary>The order products are shown in: what the building and the bills wait on first, then by kind.</summary>
        public static int WantedRank(string kind, bool wanted) => KindRank(kind) - (wanted ? 20 : 0);

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
