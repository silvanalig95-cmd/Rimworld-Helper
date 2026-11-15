using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of Rimfolk's sales table for RimMarket's colony stalls (Integrations/RimMarketStall.cs,
    /// UI/Window_StallCounter.cs): prices kept as RimMarket keeps them (a share of the article's sell value, 50% to 300%
    /// in steps of 5%), how many of an article can go on the counter, the quick amounts offered for an article, how well a
    /// price sells, and how worn the weapons and apparel are. No game types, so Tools/Tests checks it.
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

        // ---------------- How worn ----------------

        /// <summary>
        /// How worn the colony's weapons or apparel of one kind are (the player, 15 November 2026: "the market ui should show
        /// how much health that armor and weapons still have"): how many pieces, the lowest and highest share of their hit
        /// points left, and how many are tainted (taken from the dead: the game's own "tainted", which sells for a tenth).
        /// </summary>
        public struct Wear
        {
            public int count, tainted;
            public float low, high;

            public void Add(float health, bool taint)
            {
                float h = health < 0f ? 0f : health > 1f ? 1f : health;
                if (count == 0) low = high = h;
                else
                {
                    if (h < low) low = h;
                    if (h > high) high = h;
                }
                count++;
                if (taint) tainted++;
            }

            public void Add(Wear other)
            {
                if (other.count <= 0) return;
                if (count == 0)
                {
                    low = other.low;
                    high = other.high;
                }
                else
                {
                    if (other.low < low) low = other.low;
                    if (other.high > high) high = other.high;
                }
                count += other.count;
                tainted += other.tainted;
            }
        }

        /// <summary>A share of hit points as a whole percent: never 100 for a piece that has lost any (99.6% is "99%", as worn), never 0 for one that has some left.</summary>
        public static int Percent(float health)
        {
            if (health >= 1f) return 100;
            if (health <= 0f) return 0;
            return Math.Max(1, Math.Min(99, (int)Math.Round(health * 100f)));
        }

        /// <summary>The health of the pieces: "62%" for one (or several alike), "45-100%" for several; null for none.</summary>
        public static string HealthText(Wear w)
        {
            if (w.count <= 0) return null;
            int lo = Percent(w.low), hi = Percent(w.high);
            return lo == hi ? lo + "%" : lo + "-" + hi + "%";
        }

        /// <summary>How the health reads at a glance, by the most worn piece: 0 fine (80% or more), 1 worn (50%), 2 badly worn.</summary>
        public static int HealthTone(Wear w) => w.count <= 0 || w.low >= 0.8f ? 0 : w.low >= 0.5f ? 1 : 2;
    }
}
