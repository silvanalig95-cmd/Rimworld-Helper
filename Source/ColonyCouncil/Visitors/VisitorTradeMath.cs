using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The kinds of goods a traveller looks for on a market stall's counter (Visitors/VisitorTrade.cs).</summary>
    [Flags]
    public enum TradeGoods
    {
        None = 0,
        /// <summary>Hay and kibble: what a herd eats on the road.</summary>
        Feed = 1,
        /// <summary>A cooked meal, to eat soon.</summary>
        Meal = 2,
        /// <summary>Food that keeps for the road: pemmican, packaged meals.</summary>
        RoadFood = 4,
        /// <summary>Raw food to cook at their own fire: vegetables, grain, meat, eggs, milk.</summary>
        RawFood = 8,
        Medicine = 16,
        /// <summary>Something to share of an evening: beer, smokeleaf.</summary>
        Drink = 32,
        Clothes = 64,
        Textiles = 128,
        /// <summary>Metal and components, for a smith or a tinker.</summary>
        Metal = 256,
        Art = 512
    }

    /// <summary>
    /// The arithmetic of visitors and campers buying at the colony's market stalls (Visitors/VisitorTrade.cs, with
    /// RimMarket): what each kind of traveller looks for, how well an article fits, how many they take, what their purse
    /// holds, and the price and the chance to buy as RimMarket reckons a shopper's sale. No game types, so Tools/Tests
    /// checks it.
    /// </summary>
    public static class VisitorTradeMath
    {
        /// <summary>Purchases a visit makes at most, and a camp over its whole stay (one a day).</summary>
        public const int VisitBuys = 2, CampBuys = 3;

        /// <summary>A market day's buyers (a world event's visit "buyers"): each of them shops, and between them they buy this many at most.</summary>
        public const int MarketBuys = 5;

        /// <summary>What a market day's buyers are called (the visit's <c>what</c>).</summary>
        public const string Buyers = "buyers";

        /// <summary>A market day's buyers look at a bit of everything, food first.</summary>
        public static readonly TradeGoods[] MarketNeeds =
        {
            TradeGoods.RawFood, TradeGoods.Meal, TradeGoods.Textiles, TradeGoods.Clothes, TradeGoods.Drink, TradeGoods.Medicine, TradeGoods.Metal,
            TradeGoods.Art, TradeGoods.RoadFood, TradeGoods.Feed
        };

        /// <summary>The market's hours when RimMarket doesn't say (its ShopHoursStart and ShopHoursEnd).</summary>
        public const int ShopFrom = 8, ShopTo = 18;

        /// <summary>Less silver than this and nobody goes to buy (RimMarket's own least for a customer).</summary>
        public const int LeastSilver = 5;

        /// <summary>What anyone on the road needs: food that keeps, a meal, something for a wound.</summary>
        public static readonly TradeGoods[] RoadNeeds = { TradeGoods.RoadFood, TradeGoods.Meal, TradeGoods.Medicine };

        private static readonly string[] Herders = { "drovers", "herders" };
        private static readonly string[] Poor = { "family", "families", "settlers", "exiles", "orphans", "orphan", "mother", "survivor", "captive", "fugitive", "deserter", "refugees" };
        private static readonly string[] Faithful = { "pilgrims", "mourners", "preacher", "penitents" };
        private static readonly string[] Diggers = { "prospectors", "prospector", "salvagers", "digger" };
        private static readonly string[] Makers = { "smith", "tinker" };
        private static readonly string[] Players = { "singer", "band", "stargazer" };
        private static readonly string[] Dealers = { "merchant", "agent", "smuggler" };

        /// <summary>What a visit's people look for first, by what they are (a visit's <c>what</c>): drovers feed for the herd, a smith metal, a family a meal and clothes.</summary>
        public static TradeGoods[] Wants(string who)
        {
            if (who == Buyers) return MarketNeeds;
            if (Is(who, Herders)) return new[] { TradeGoods.Feed, TradeGoods.RoadFood, TradeGoods.Medicine };
            if (Is(who, Poor)) return new[] { TradeGoods.Meal, TradeGoods.RoadFood, TradeGoods.Clothes, TradeGoods.Medicine };
            if (Is(who, Faithful)) return new[] { TradeGoods.Meal, TradeGoods.RawFood, TradeGoods.RoadFood };
            if (Is(who, Diggers)) return new[] { TradeGoods.RoadFood, TradeGoods.Meal, TradeGoods.Medicine, TradeGoods.Drink };
            if (Is(who, Makers)) return new[] { TradeGoods.Metal, TradeGoods.RoadFood };
            if (Is(who, Players)) return new[] { TradeGoods.Drink, TradeGoods.Meal };
            if (Is(who, Dealers)) return new[] { TradeGoods.Art, TradeGoods.Textiles, TradeGoods.Clothes };
            if (who == "gleaners") return new[] { TradeGoods.Meal, TradeGoods.RawFood };
            if (who == "hunters") return new[] { TradeGoods.RoadFood, TradeGoods.Medicine, TradeGoods.Drink };
            return RoadNeeds;
        }

        /// <summary>A camp kind's own list (CampKindDef.buys: "Feed", "RoadFood"...), in order; the road's needs when it has none that are known.</summary>
        public static TradeGoods[] Wants(IList<string> words)
        {
            var list = new List<TradeGoods>();
            if (words != null)
                foreach (var w in words)
                {
                    var g = Parse(w);
                    if (g != TradeGoods.None && !list.Contains(g)) list.Add(g);
                }
            return list.Count > 0 ? list.ToArray() : RoadNeeds;
        }

        /// <summary>One kind of goods by its name ("Feed", "roadfood"), or None.</summary>
        public static TradeGoods Parse(string word) =>
            !string.IsNullOrEmpty(word) && char.IsLetter(word.Trim()[0]) && Enum.TryParse(word.Trim(), true, out TradeGoods g) && g != TradeGoods.None && IsOne(g) ? g : TradeGoods.None;

        private static bool IsOne(TradeGoods g) => ((int)g & ((int)g - 1)) == 0;

        private static bool Is(string who, string[] words) => who != null && Array.IndexOf(words, who) >= 0;

        /// <summary>How well an article of these kinds fits: the first thing they look for scores highest, nothing they want scores 0.</summary>
        public static int Fit(TradeGoods article, IList<TradeGoods> wants)
        {
            if (wants == null) return 0;
            for (int i = 0; i < wants.Count; i++)
                if ((article & wants[i]) != 0) return wants.Count - i;
            return 0;
        }

        /// <summary>The first of the kinds they want that the article is (for what they say about it), or None.</summary>
        public static TradeGoods Kind(TradeGoods article, IList<TradeGoods> wants)
        {
            if (wants != null)
                foreach (var w in wants)
                    if ((article & w) != 0) return w;
            return TradeGoods.None;
        }

        /// <summary>How many of an article of this kind the group would take: food by the mouths (and the herd), one coat, a bolt of cloth.</summary>
        public static int Need(TradeGoods kind, int people, int animals)
        {
            people = Math.Max(1, people);
            animals = Math.Max(0, animals);
            switch (kind)
            {
                case TradeGoods.Feed: return Math.Min(60, 10 + 8 * animals);
                case TradeGoods.Meal: return Math.Min(4, people);
                case TradeGoods.RoadFood: return Math.Min(10, 2 * people);
                case TradeGoods.RawFood: return Math.Min(30, 8 * people);
                case TradeGoods.Medicine: return Math.Min(4, 1 + people / 3);
                case TradeGoods.Drink: return Math.Min(4, people);
                case TradeGoods.Textiles: return 20;
                case TradeGoods.Metal: return 15;
                default: return 1;
            }
        }

        /// <summary>How many they buy: what they need, no more than lies on the counter, no more than the purse pays for. 0 when they can't pay for one.</summary>
        public static int HowMany(int need, int onCounter, float unitPrice, int purse)
        {
            if (need <= 0 || onCounter <= 0 || purse <= 0) return 0;
            float unit = Math.Max(1f, unitPrice);
            int afford = (int)Math.Floor(purse / unit);
            // RimMarket rounds a sale up to the next silver: never more than the purse.
            while (afford > 0 && Total(unit, afford) > purse) afford--;
            return Math.Max(0, Math.Min(need, Math.Min(onCounter, afford)));
        }

        /// <summary>One of an article at the stall, as RimMarket prices a shopper's sale: its sell value times the line's price, the keeper's way with people and the market's demand, never under one silver.</summary>
        public static float UnitPrice(float sellValue, float priceFactor, float keeperFactor, float saleFactor) =>
            Math.Max(1f, sellValue * priceFactor * keeperFactor * saleFactor);

        /// <summary>What a count of it comes to, as RimMarket rounds it (up, at least one silver).</summary>
        public static int Total(float unitPrice, int count)
        {
            float v = unitPrice * Math.Max(0, count);
            return Math.Max(1, (int)Math.Ceiling(v));
        }

        /// <summary>The chance one who fancies an article buys it, as RimMarket reckons it: its chance at the line's price, a tenth more with a keeper at the stall, times the market's demand for it.</summary>
        public static float BuyChance(float chanceAtPrice, bool keeper, float demand)
        {
            float c = (chanceAtPrice + (keeper ? 0.1f : 0f)) * demand;
            return c < 0f ? 0f : c > 1f ? 1f : c;
        }

        /// <summary>The coin a group carries for the road, by what they are: none if robbed, a little for the poor and the faithful, more for those going to market or with a trade.</summary>
        public static (int min, int max) Purse(string who, bool robbed)
        {
            if (robbed) return (0, 0);
            if (who == Buyers) return (80, 220);
            if (Is(who, Poor) || Is(who, Faithful) || who == "gleaners") return (15, 50);
            if (Is(who, Herders) || Is(who, Dealers) || Is(who, Makers) || Is(who, Diggers) || who == "surveyors") return (60, 160);
            return (30, 100);
        }

        /// <summary>The hours before a buyer goes back to the stalls: soon on a market day, a good while otherwise.</summary>
        public static (int min, int max) NextTryHours(bool market) => market ? (1, 2) : (4, 8);

        /// <summary>Within the market's hours, with time to walk over and back before it closes.</summary>
        public static bool ShopHour(int hour, int from, int to) => hour >= from && hour < to - 1;

        /// <summary>The herd's milk that a beast gives now: none under half full, else its amount by how full it is (the game's own share at milking).</summary>
        public static int MilkGives(int amount, float fullness) =>
            amount <= 0 || fullness < 0.5f ? 0 : (int)Math.Round(amount * Math.Min(1f, fullness), MidpointRounding.AwayFromZero);

        /// <summary>What the colony pays for milk: its worth each times how many, at least one silver.</summary>
        public static int MilkPrice(int count, float worthEach) => count <= 0 ? 0 : Math.Max(1, (int)Math.Round(count * Math.Max(0f, worthEach), MidpointRounding.AwayFromZero));

        /// <summary>What the colony pays when it hasn't all of it: what it has, up to the price.</summary>
        public static int PayWith(int price, int have) => Math.Max(0, Math.Min(price, have));
    }
}
