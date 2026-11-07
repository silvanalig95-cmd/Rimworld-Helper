using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a good is, for what the colony keeps of it (Trade/TradeAdvice.cs sorts the game's things into these).</summary>
    public enum TradeGood
    {
        /// <summary>No rule of its own: art, body parts, books and the like are never called more than needed.</summary>
        Other,
        /// <summary>Meals and raw food people eat.</summary>
        Food,
        /// <summary>Food only animals eat (hay, kibble).</summary>
        Feed,
        Medicine,
        /// <summary>Wood, metal and stone blocks: what the colony builds and makes with.</summary>
        Material,
        /// <summary>Gold, jade and the like: a little is kept for art and fine things.</summary>
        Precious,
        /// <summary>Cloth, wool and leather.</summary>
        Textile,
        Component,
        /// <summary>A drug that isn't medicine.</summary>
        Drug,
        /// <summary>Weapons and clothes: kept by the spare, not by the count.</summary>
        Equipment
    }

    /// <summary>The mark a row of the trade screen carries.</summary>
    public enum TradeMark
    {
        None,
        Sell,
        Buy,
        Keep
    }

    /// <summary>One line of the suggested basket: a row of the trade screen, how many it can take and at what price.</summary>
    public class BasketLine
    {
        public int id;
        /// <summary>The most this line can move: the trader's stock to buy, the colony's surplus to sell.</summary>
        public int max;
        /// <summary>Silver a piece.</summary>
        public float price;
        /// <summary>The higher, the sooner it is taken (TradeAdviceMath's priorities).</summary>
        public float priority;
        /// <summary>The need a buy answers ("food", "medicine", a thing's own name), shared by every line that answers it; null for none.</summary>
        public string group;
        /// <summary>What a piece gives toward its group's need (nutrition for food, 1 for a piece).</summary>
        public float perUnit = 1f;
    }

    /// <summary>
    /// The trade screen's advice as plain rules (Trade/TradeAdvice.cs reads the colony and the trader into these): what
    /// the colony keeps of a good before anything of it is more than needed, how good a price is against the usual,
    /// which spare weapons and clothes are the worse of their kind, and the suggested basket filled within the silver
    /// both sides have. Nothing here changes a trade; the player clicks.
    /// <para>
    /// The reserve: the need is what the colony uses in a season (15 days) by its own record of use, or a floor for its
    /// kind by the colony's size, the larger; it keeps the need and as much again, and what is set aside for its plans
    /// on top (the blueprints, a journey's packing, a promise, a contract, the drug policies). Food goes by days of
    /// eating from the plan ahead (the days chosen to keep in store, and the days ahead nothing grows), at least ten.
    /// What is beyond the keep is more than needed.
    /// </para>
    /// </summary>
    public static class TradeAdviceMath
    {
        // ---------------- The reserve ----------------

        /// <summary>A season: what the colony uses in it is the need for a good with a record of use.</summary>
        public const float SeasonDays = 15f;
        /// <summary>The need and as much again as a reserve.</summary>
        public const int ReserveTimes = 2;
        /// <summary>Floors a person, and at least: wood, metal and stone blocks; precious stuff; cloth and leather; components; medicine.</summary>
        public const int MaterialEach = 50, MaterialAtLeast = 250;
        public const int PreciousEach = 5, PreciousAtLeast = 20;
        public const int TextileEach = 25, TextileAtLeast = 75;
        public const int ComponentEach = 3, ComponentAtLeast = 8;
        public const int MedicineEach = 3, MedicineAtLeast = 6;
        /// <summary>Food is kept by days of eating: never fewer than this many as the need.</summary>
        public const float FoodDaysAtLeast = 10f;
        /// <summary>One spare of each kind of weapon or garment for every this many people.</summary>
        public const int PeoplePerSpare = 3;
        /// <summary>A surplus worth a word: this share of what there is, and this much silver at the trader's price.</summary>
        public const float MinSurplusShare = 0.1f;
        public const float MinSurplusSilver = 40f;
        /// <summary>Raw food cooks into this much more meal nutrition (0.5 of ingredients make a 0.9 meal).</summary>
        public const float CookGain = 0.9f / 0.5f;

        /// <summary>The floor of a kind of good for a colony of so many people; 0 for a kind with none.</summary>
        public static int Floor(TradeGood kind, int people)
        {
            int p = Math.Max(1, people);
            switch (kind)
            {
                case TradeGood.Material: return Math.Max(MaterialAtLeast, MaterialEach * p);
                case TradeGood.Precious: return Math.Max(PreciousAtLeast, PreciousEach * p);
                case TradeGood.Textile: return Math.Max(TextileAtLeast, TextileEach * p);
                case TradeGood.Component: return Math.Max(ComponentAtLeast, ComponentEach * p);
                case TradeGood.Medicine: return Math.Max(MedicineAtLeast, MedicineEach * p);
                default: return 0;
            }
        }

        /// <summary>Whether the colony keeps a count of this good: a floor for its kind, or a record of using it. Food and feed go by days.</summary>
        public static bool Counted(TradeGood kind, float useADay) =>
            kind != TradeGood.Equipment && kind != TradeGood.Food && kind != TradeGood.Feed && (Floor(kind, 1) > 0 || useADay > 0f);

        /// <summary>The need: what a season uses by the record, or the floor, the larger; and for medicine the plan ahead's own stock target if that is more.</summary>
        public static int Need(TradeGood kind, int people, float useADay, float plannedTarget = 0f) =>
            Math.Max(Floor(kind, people), (int)Math.Ceiling(Math.Max(Math.Max(0f, useADay) * SeasonDays, Math.Max(0f, plannedTarget)) - 0.0001f));

        /// <summary>What is kept before anything is more than needed: the need twice over, and what is set aside on top. A good with no count keeps only what is set aside.</summary>
        public static int Keep(TradeGood kind, int people, float useADay, int setAside, float plannedTarget = 0f) =>
            (Counted(kind, useADay) || plannedTarget > 0f ? ReserveTimes * Need(kind, people, useADay, plannedTarget) : 0) + Math.Max(0, setAside);

        /// <summary>What is beyond the keep.</summary>
        public static int Surplus(int have, int keep) => Math.Max(0, have - Math.Max(0, keep));

        /// <summary>A surplus worth suggesting: a tenth of what there is or more, and worth at least a little silver.</summary>
        public static bool WorthSaying(int surplus, int have, float pricePerPiece) =>
            surplus > 0 && surplus >= MinSurplusShare * Math.Max(1, have) && surplus * Math.Max(0f, pricePerPiece) >= MinSurplusSilver;

        // ---------------- Food, by days of eating ----------------

        /// <summary>Days the colony eats from its food: meals as they are, raw food as the meals it cooks into when there is cooking.</summary>
        public static float FoodDays(float mealNutrition, float rawNutrition, float needADay, bool cooks)
        {
            if (needADay <= 0.0001f) return AheadMath.Far;
            return (Math.Max(0f, mealNutrition) + Math.Max(0f, rawNutrition) * (cooks ? CookGain : 1f)) / needADay;
        }

        /// <summary>The food need in days: the plan ahead's (days kept in store and the days ahead nothing grows), at least ten.</summary>
        public static float FoodNeedDays(float plannedDays) => Math.Max(FoodDaysAtLeast, plannedDays);

        /// <summary>Days of food short of the need (to buy), and days beyond twice the need (more than needed).</summary>
        public static float FoodShortDays(float haveDays, float needDays) => Math.Max(0f, needDays - Math.Max(0f, haveDays));

        public static float FoodSurplusDays(float haveDays, float needDays) => haveDays >= AheadMath.Far ? 0f : Math.Max(0f, haveDays - ReserveTimes * needDays);

        /// <summary>
        /// A surplus shared out over the colony's stocks of it, the biggest first: each stock gives what it has (in its
        /// own pieces, each worth <c>per</c> of the surplus) until the surplus is covered. Returns pieces per stock.
        /// </summary>
        public static int[] ShareOut(float surplus, IList<(int pieces, float per)> stocks)
        {
            var result = new int[stocks?.Count ?? 0];
            if (stocks == null || surplus <= 0f) return result;
            float left = surplus;
            foreach (int i in Enumerable.Range(0, stocks.Count).OrderByDescending(i => stocks[i].pieces * stocks[i].per).ThenBy(i => i))
            {
                if (left <= 0.0001f) break;
                var (pieces, per) = stocks[i];
                if (pieces <= 0 || per <= 0f) continue;
                int take = Math.Min(pieces, (int)Math.Floor(left / per));
                result[i] = take;
                left -= take * per;
            }
            return result;
        }

        // ---------------- Spare weapons and clothes ----------------

        /// <summary>Spares kept of each kind: one for every three people, at least one.</summary>
        public static int Spares(int people) => Math.Max(1, (Math.Max(1, people) + PeoplePerSpare - 1) / PeoplePerSpare);

        /// <summary>A copy's standing among its kind: its quality first, then its hit points by tenths, then whether the dead wore it.</summary>
        public static int Standing(int quality, float hitPoints, bool tainted) =>
            quality * 100 + (int)Math.Round(Math.Max(0f, Math.Min(1f, hitPoints)) * 10f) * 2 + (tainted ? 0 : 1);

        /// <summary>
        /// Of the colony's stored copies of one kind (each copy's standing), those worth selling: beyond the spares kept,
        /// the best first, and only copies worse than the best one kept. True for each such copy.
        /// </summary>
        public static bool[] WorseCopies(IList<int> standings, int spares)
        {
            var result = new bool[standings?.Count ?? 0];
            if (standings == null || standings.Count <= Math.Max(1, spares)) return result;
            var order = Enumerable.Range(0, standings.Count).OrderByDescending(i => standings[i]).ThenBy(i => i).ToList();
            int best = standings[order[0]];
            for (int k = Math.Max(1, spares); k < order.Count; k++)
                if (standings[order[k]] < best) result[order[k]] = true;
            return result;
        }

        // ---------------- The price against the usual ----------------

        /// <summary>The game's own share of a thing's worth a trader pays (selling) and asks (buying), before the negotiator.</summary>
        public const float SellBase = 0.6f, BuyBase = 1.4f;
        /// <summary>A sale a fifth above the usual is a good sale; a price a fifth under the usual (the deal 1.25) a bargain.</summary>
        public const float GoodSale = 1.2f, Bargain = 1.25f;
        /// <summary>A world price this far from the usual worth is named in the words (Vanilla Trading Expanded's market).</summary>
        public const float WorldShift = 0.1f;

        /// <summary>What a trader usually pays a piece for a thing worth so much, with the negotiator's gain and the difficulty's loss.</summary>
        public static float UsualSell(float worth, float gain, float loss) => Math.Max(0.01f, Math.Max(0f, worth) * SellBase * (1f - loss) * (1f + Math.Max(0f, gain)));

        /// <summary>What a trader usually asks a piece.</summary>
        public static float UsualBuy(float worth, float gain, float loss) => Math.Max(0.5f, Math.Max(0f, worth) * BuyBase * (1f + loss) * (1f - Math.Min(0.99f, Math.Max(0f, gain))));

        /// <summary>A sale's deal: the price against the usual (1 as usual; more is better).</summary>
        public static float SellDeal(float price, float usual) => usual <= 0f ? 1f : Math.Max(0f, price) / usual;

        /// <summary>A purchase's deal: the usual against the price (1 as usual; more is better, a cheaper price).</summary>
        public static float BuyDeal(float price, float usual) => price <= 0f ? 1f : Math.Max(0f, usual) / price;

        /// <summary>A deal as a share above (positive) or below the usual, rounded to fives: 1.23 is 25, 0.8 is -20.</summary>
        public static int PercentOff(float deal) => (int)Math.Round((deal - 1f) * 20f, MidpointRounding.AwayFromZero) * 5;

        // ---------------- Priorities and the sort ----------------

        /// <summary>What to buy first: food short, medicine, feed, what the plans and the shopping list want, then the rest; bargains last.</summary>
        public const float BuyFood = 90f, BuyMedicine = 85f, BuyFeed = 80f, BuyPlans = 70f, BuyComponents = 65f, BuyContract = 60f, BuyWish = 55f,
            BuyResearch = 50f, BuyBargain = 30f;

        /// <summary>What to sell first: what no one here can use, then the surplus, a good price on what is beyond the need, the worse spares.</summary>
        public const float SellUnused = 60f, SellSurplus = 50f, SellGoodPrice = 45f, SellWorse = 40f;

        /// <summary>"Suggested first": buys, then sales, then what is kept, each by its priority; the rest after.</summary>
        public static float SortScore(TradeMark mark, float priority)
        {
            float p = Math.Max(0f, Math.Min(99f, priority));
            switch (mark)
            {
                case TradeMark.Buy: return 300f + p;
                case TradeMark.Sell: return 200f + p;
                case TradeMark.Keep: return 100f + p;
                default: return 0f;
            }
        }

        // ---------------- The suggested basket ----------------

        /// <summary>Silver left aside on each side, so the game's own rounding of the total never tips a full basket over.</summary>
        public const int Slack = 2;

        /// <summary>
        /// The basket within the silver both sides have: buys first, by priority and then by the cheapest for what they
        /// give, each group's need answered once, paid for with the colony's silver and what its sales bring; then the
        /// sales, by priority and worth, as far as the trader's silver and what the colony buys pay for them. Returns how
        /// many each line takes (a line taking none is left out). The buys never cost more than the colony's silver and
        /// the sales made: either every sale is made, or the sales stop where the trader's silver and the buys run out,
        /// and then the colony is paid more than it spends.
        /// </summary>
        public static Dictionary<int, int> Fill(IList<BasketLine> buys, IDictionary<string, float> needs, IList<BasketLine> sales, int colonySilver, int traderSilver)
        {
            var buying = (buys ?? new List<BasketLine>()).Where(b => b != null && b.max > 0 && b.price > 0f)
                .OrderByDescending(b => b.priority).ThenBy(b => b.price / Math.Max(0.0001f, b.perUnit)).ThenBy(b => b.id).ToList();
            var selling = (sales ?? new List<BasketLine>()).Where(s => s != null && s.max > 0 && s.price > 0f)
                .OrderByDescending(s => s.priority).ThenByDescending(s => s.max * s.price).ThenBy(s => s.id).ToList();
            float colony = Math.Max(0, colonySilver - Slack), trader = Math.Max(0, traderSilver - Slack);
            // The buys count on every sale at first; when the trader's silver stops the sales short, they count on what
            // was sold, and are chosen again (a few rounds, each budget smaller); at the last, on the colony's silver alone.
            float budget = colony + selling.Sum(s => s.max * s.price);
            for (int round = 0; ; round++)
            {
                var take = new Dictionary<int, int>();
                float spent = Buys(buying, needs, round < 6 ? budget : colony, take);
                float earned = Sales(selling, trader + spent, take);
                if (spent <= colony + earned + 0.001f || round >= 6) return take;
                budget = colony + earned;
            }
        }

        private static float Buys(List<BasketLine> buying, IDictionary<string, float> needs, float budget, Dictionary<int, int> take)
        {
            var left = needs == null ? new Dictionary<string, float>() : new Dictionary<string, float>(needs);
            float spent = 0f;
            foreach (var b in buying)
            {
                int units = b.max;
                if (b.group != null)
                {
                    if (!left.TryGetValue(b.group, out float need) || need <= 0.0001f) continue;
                    units = Math.Min(units, (int)Math.Ceiling(need / Math.Max(0.0001f, b.perUnit) - 0.0001f));
                }
                units = Math.Min(units, (int)Math.Floor((budget - spent) / b.price + 0.0001f));
                if (units <= 0) continue;
                take[b.id] = units;
                spent += units * b.price;
                if (b.group != null) left[b.group] -= units * b.perUnit;
            }
            return spent;
        }

        private static float Sales(List<BasketLine> selling, float room, Dictionary<int, int> take)
        {
            float earned = 0f;
            foreach (var s in selling)
            {
                int units = Math.Min(s.max, (int)Math.Floor((room - earned) / s.price + 0.0001f));
                if (units <= 0) continue;
                take[s.id] = take.TryGetValue(s.id, out int had) ? had + units : units;
                earned += units * s.price;
            }
            return earned;
        }
    }
}
