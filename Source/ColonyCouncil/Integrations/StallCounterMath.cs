using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of Rimfolk's sales table for RimMarket's colony stalls (Integrations/RimMarketStall.cs,
    /// UI/Window_StallCounter.cs): prices kept as RimMarket keeps them (a share of the article's sell value, 50% to 300%
    /// in steps of 5%), how many of an article can go on the counter, the quick amounts offered for an article, and how
    /// well a price sells. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class StallCounterMath
    {
        /// <summary>RimMarket's limits and step for the asking price, and the price a new line starts at.</summary>
        public const float MinPrice = 0.5f, MaxPrice = 3f, PriceStep = 0.05f, DefaultPrice = 1.1f;

        /// <summary>The three prices offered for every line at once: a bargain, the usual, dear.</summary>
        public const float Bargain = 0.85f, Fair = 1.1f, Dear = 1.4f;

        /// <summary>A price as RimMarket keeps it: to the nearest 5%, within 50% and 300%.</summary>
        public static float SnapPrice(float price)
        {
            float p = (float)Math.Round(price / PriceStep) * PriceStep;
            return p < MinPrice ? MinPrice : p > MaxPrice ? MaxPrice : p;
        }

        /// <summary>How many of an article a line can sell: at least one, at most what the colony has of it (or one, when it has none left).</summary>
        public static int ClampCount(int count, int most) => Math.Max(1, Math.Min(count, Math.Max(1, most)));

        /// <summary>The amounts offered when an article goes on the counter: all of it, half, ten and one, largest first, without repeats or more than there is.</summary>
        public static List<int> QuickAmounts(int have)
        {
            var list = new List<int>();
            if (have <= 0) return list;
            foreach (int n in new[] { have, have / 2, 10, 1 })
                if (n >= 1 && n <= have && !list.Contains(n)) list.Add(n);
            return list;
        }

        /// <summary>
        /// How well a price sells, from the chance that a customer who fancies the article buys it at that price
        /// (RimMarket's own): 3 sells well (70% or more), 2 sells (45%), 1 sells slowly (20%), 0 hardly sells.
        /// </summary>
        public static int Demand(float chance) => chance >= 0.695f ? 3 : chance >= 0.45f ? 2 : chance >= 0.2f ? 1 : 0;

        /// <summary>A line's worth if it all goes: the price each, as shown, times the count.</summary>
        public static int LineWorth(int priceEach, int count) => Math.Max(0, priceEach) * Math.Max(0, count);
    }
}
