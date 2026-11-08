using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One row's advice on the trade screen: its mark, how many, why in words, and how its prices stand.</summary>
    public sealed class TradeNote
    {
        public TradeMark mark;
        /// <summary>Pieces worth selling or buying (0 for a row only kept).</summary>
        public int count;
        public float priority;
        /// <summary>The need a buy answers, shared by every row that answers it, and what a piece gives toward it.</summary>
        public string group;
        public float perUnit = 1f;
        /// <summary>The price against the usual: a sale's and a purchase's (1 as usual; more is better), where the row has that side.</summary>
        public float sellDeal = 1f, buyDeal = 1f;
        /// <summary>The usual prices a piece for this negotiator and difficulty (TradeAdviceMath's usual, Vanilla Trading Expanded's market divided out); 0 outside a silver trade.</summary>
        public float usualSell, usualBuy;
        public bool canSell, canBuy;
        /// <summary>Of what the colony has of it in all, the pieces set aside for its plans (a sale that cuts into them is warned).</summary>
        public int have, setAside;
        public string head;
        public readonly List<string> lines = new List<string>();
        public readonly List<string> aside = new List<string>();
        public string price, world;
        /// <summary>With Vanilla Trading Expanded's history: the market's reason for the sale or buy (at its 60-day high or low, a fast move), or null.</summary>
        public string trend;

        /// <summary>The better of its two deals, for the "best deal" sort.</summary>
        public float Deal => Mathf.Max(canSell ? sellDeal : 0f, canBuy ? buyDeal : 0f);

        public bool Marked => mark != TradeMark.None;
    }

    /// <summary>
    /// The trade screen's advice, worked out once a trade opens (and again when its goods change): each row's mark (worth
    /// selling, worth buying, keep), how many and why, from what Rimfolk already knows of the colony. The plan ahead's
    /// stock rows and food days (Building/ColonyNeeds.cs, Ahead.cs), its shopping list (AheadMore.cs), the blueprints'
    /// materials (Making.cs), the colony's record of use (ColonyUse), the journey being packed, open and promised wishes
    /// and what was given for one (Wishes/), the drug policies, the research under way, and Vanilla Trading Expanded's
    /// market and contracts (Integrations/VanillaTrading.cs). Only what the colony has and can use counts as a need, so a
    /// tribe's advice is a tribe's. The rules are <see cref="TradeAdviceMath"/>'s. Nothing is changed by it; the basket
    /// is the player's click (Trade/TradeScreen.cs).
    /// </summary>
    public static class TradeAdvice
    {
        private static TradeDeal readDeal;
        private static int readCount = -1;
        private static Tradeable readFirst;
        private static bool readGift;
        private static readonly Dictionary<Tradeable, TradeNote> notes = new Dictionary<Tradeable, TradeNote>();
        /// <summary>Each shared need of the basket (food and feed in nutrition, medicine and the rest in pieces), from the last reading.</summary>
        private static readonly Dictionary<string, float> groupNeeds = new Dictionary<string, float>();

        /// <summary>The colony's food as the readout needs it: what it has, what it eats a day, whether it cooks.</summary>
        public static float mealNutrition, rawNutrition, eatADay;
        public static bool cooks, foodKnown;

        /// <summary>A row's advice, or null.</summary>
        public static TradeNote For(Tradeable t)
        {
            Refresh();
            return t != null && notes.TryGetValue(t, out var n) ? n : null;
        }

        /// <summary>Every row with its advice.</summary>
        public static Dictionary<Tradeable, TradeNote> All
        {
            get
            {
                Refresh();
                return notes;
            }
        }

        /// <summary>Worked out again when the trade's goods are not the ones read (a new trade, the game's Reset, gift mode).</summary>
        private static void Refresh()
        {
            var deal = TradeSession.Active ? TradeSession.deal : null;
            var list = deal?.AllTradeables;
            int count = list?.Count ?? -1;
            var first = count > 0 ? list[0] : null;
            if (deal == readDeal && count == readCount && first == readFirst && TradeSession.giftMode == readGift) return;
            readDeal = deal;
            readCount = count;
            readFirst = first;
            readGift = TradeSession.giftMode;
            notes.Clear();
            groupNeeds.Clear();
            foodKnown = false;
            if (deal == null || TradeSession.giftMode || TradeSession.playerNegotiator == null) return;
            try
            {
                new Reading().Run(list);
            }
            catch (Exception e)
            {
                notes.Clear();
                CouncilLog.ErrorOnce("trade advice", e);
            }
        }

        /// <summary>What the colony has of a good, kind by kind, for the reserve.</summary>
        public static TradeGood KindOf(ThingDef d)
        {
            if (d == null || d == ThingDefOf.Silver) return TradeGood.Other;
            if (d.IsMedicine) return TradeGood.Medicine;
            if (d.IsApparel) return TradeGood.Equipment;
            string food = ColonyNeeds.KindOf(d);
            if (food == AheadMath.Meal || food == AheadMath.Raw) return HumanMeat(d) ? TradeGood.Other : TradeGood.Food;
            if (food == AheadMath.Feed) return TradeGood.Feed;
            if (d.IsNonMedicalDrug) return TradeGood.Drug;
            if (d.IsWeapon) return TradeGood.Equipment;
            if (d == ThingDefOf.ComponentIndustrial || d == ThingDefOf.ComponentSpacer) return TradeGood.Component;
            if (d.category != ThingCategory.Item || !d.IsStuff || d.stuffProps?.categories == null) return TradeGood.Other;
            var cats = d.stuffProps.categories;
            if (cats.Contains(StuffCategoryDefOf.Fabric) || cats.Contains(StuffCategoryDefOf.Leathery)) return TradeGood.Textile;
            if (cats.Contains(StuffCategoryDefOf.Metallic) || cats.Contains(StuffCategoryDefOf.Woody) || cats.Contains(StuffCategoryDefOf.Stony))
                return MaterialPicker.IsPrecious(d) ? TradeGood.Precious : TradeGood.Material;
            return TradeGood.Other;
        }

        private static bool HumanMeat(ThingDef d) => d.ingestible?.sourceDef?.race?.Humanlike ?? false;

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>Between the shopping list's reasons in one line.</summary>
        private const string ListSeparator = "; ";

        private static string Money(float silver) => silver.ToStringMoney();

        private static string Days(float d) => d >= AheadMath.Far ? "-" : d.ToString(d < 10f ? "0.#" : "0");

        /// <summary>A price against the usual, in words: about the usual, or so much above or under it.</summary>
        private static string Usual(float deal, bool sale)
        {
            int pct = TradeAdviceMath.PercentOff(deal);
            if (Math.Abs(pct) < 10) return W("CC_Trade_Usual_Same");
            // A sale's deal above 1 is a higher price; a purchase's above 1 a lower one.
            bool higher = sale ? pct > 0 : pct < 0;
            int share = sale ? Math.Abs(pct) : Math.Abs(TradeAdviceMath.PercentOff(1f / Mathf.Max(0.01f, deal)));
            return W(higher ? "CC_Trade_Usual_Above" : "CC_Trade_Usual_Below", share.Named("PCT"));
        }

        /// <summary>
        /// The usual prices a piece of a thing for a negotiator's gain and the difficulty's loss: the game's 60% and 140% of
        /// its worth (TradeAdviceMath), its worth being its market value with Vanilla Trading Expanded's market change
        /// (<paramref name="factor"/>) divided out.
        /// </summary>
        public static void UsualPrices(Thing thing, float factor, float gain, float loss, out float sell, out float buy)
        {
            float worth = (thing?.MarketValue ?? 0f) / Mathf.Max(0.01f, factor);
            sell = TradeAdviceMath.UsualSell(worth, gain, loss);
            buy = TradeAdviceMath.UsualBuy(worth, gain, loss);
        }

        /// <summary>The open trade's negotiator gain (their price improvement and the trader's offset for the colony) and the difficulty's loss, as the game prices with them.</summary>
        public static void GainAndLoss(out float gain, out float loss)
        {
            var negotiator = TradeSession.playerNegotiator;
            gain = negotiator != null && TradeSession.Active ? negotiator.GetStatValue(StatDefOf.TradePriceImprovement) + TradeSession.trader.TradePriceImprovementOffsetForPlayer : 0f;
            loss = Find.Storyteller?.difficulty?.tradePriceFactorLoss ?? 0f;
        }

        /// <summary>The usual prices a piece of a row the advice doesn't read (people and animals), for the open trade.</summary>
        public static void UsualFor(Tradeable t, out float sell, out float buy)
        {
            sell = buy = 0f;
            if (t == null || !t.HasAnyThing || !TradeSession.Active || TradeSession.TradeCurrency != TradeCurrency.Silver) return;
            GainAndLoss(out float gain, out float loss);
            UsualPrices(t.AnyThing, VanillaTrading.WorldFactor(t.ThingDef), gain, loss, out sell, out buy);
        }

        /// <summary>One reading of the colony and the trader: builds every row's note.</summary>
        private sealed class Reading
        {
            private Pawn negotiator;
            private Caravan caravan;
            private Map home;
            private ColonyNeeds needs;
            private Ahead.View view;
            private List<Pawn> people = new List<Pawn>();
            private int headcount = 1;
            private readonly Dictionary<ThingDef, int> carried = new Dictionary<ThingDef, int>();
            private bool silver;
            private float gain, loss;

            private struct Aside
            {
                public ThingDef stuff;
                public int count;
                public string why;
            }

            private readonly Dictionary<ThingDef, List<Aside>> asides = new Dictionary<ThingDef, List<Aside>>();
            private readonly Dictionary<int, string> keptThings = new Dictionary<int, string>();
            private readonly Dictionary<ThingDef, float> world = new Dictionary<ThingDef, float>();
            private List<VanillaTrading.Contract> contracts = new List<VanillaTrading.Contract>();

            public void Run(List<Tradeable> list)
            {
                negotiator = TradeSession.playerNegotiator;
                caravan = negotiator.GetCaravan();
                home = negotiator.MapHeld != null && negotiator.MapHeld.IsPlayerHome ? negotiator.MapHeld : MainHome();
                needs = home != null ? ColonyNeeds.For(home) : null;
                view = home != null ? Ahead.For(home) : null;
                silver = TradeSession.TradeCurrency == TradeCurrency.Silver;
                GainAndLoss(out gain, out loss);
                if (home != null) people.AddRange(ColonyNeeds.Hands(home));
                if (caravan != null)
                {
                    people.AddRange(caravan.PawnsListForReading.Where(p => p.RaceProps.Humanlike && (p.IsColonist || p.IsSlaveOfColony)));
                    foreach (var t in CaravanInventoryUtility.AllInventoryItems(caravan))
                    {
                        carried.TryGetValue(t.def, out int n);
                        carried[t.def] = n + t.stackCount;
                    }
                }
                people = people.Distinct().ToList();
                headcount = Math.Max(1, needs != null && needs.colonists + needs.slaves > 0 ? needs.colonists + needs.slaves : people.Count);
                contracts = VanillaTrading.Contracts();
                SetAsides();
                Food();

                var rows = list.Where(t => t != null && !t.IsCurrency && !t.IsFavor && t.HasAnyThing && !(t is Tradeable_Pawn) && !(t.AnyThing is Pawn)).ToList();
                foreach (var t in rows) notes[t] = Base(t);
                FoodRows(rows);
                MedicineRows(rows);
                foreach (var t in rows) Row(t, notes[t]);
                foreach (var t in rows) Finish(t, notes[t]);
            }

            // ---------------- The colony ----------------

            private static Map MainHome() =>
                Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).ThenBy(m => m.uniqueID).FirstOrDefault();

            private int Stock(ThingDef d)
            {
                int n = 0;
                if (needs?.stock != null) needs.stock.TryGetValue(d, out n);
                carried.TryGetValue(d, out int c);
                return n + c;
            }

            private float UseADay(ThingDef d) => needs?.record != null && needs.measured && needs.record.used.TryGetValue(d, out float u) ? u : 0f;

            private void Add(ThingDef d, ThingDef stuff, int count, string why)
            {
                if (d == null || count <= 0) return;
                if (!asides.TryGetValue(d, out var l)) asides[d] = l = new List<Aside>();
                l.Add(new Aside { stuff = stuff, count = count, why = why });
            }

            /// <summary>What the colony has set aside for its plans: the journey's packing, the blueprints, the drug policies, a contract, a promise, a gift for a wish, the caravan's supplies.</summary>
            private void SetAsides()
            {
                var plan = home != null ? JourneyBook.PlanAt(home) : null;
                if (plan?.packing != null)
                    foreach (var line in plan.packing)
                        if (line != null && line.Taken)
                            Add(line.def, null, line.count, plan.destName.NullOrEmpty()
                                ? W("CC_Trade_Aside_JourneyNoPlace", line.count.Named("COUNT"))
                                : W("CC_Trade_Aside_Journey", line.count.Named("COUNT"), plan.destName.Named("PLACE")));
                if (home != null)
                    foreach (var kv in Making.BuildingWants(home))
                        Add(kv.Key, null, kv.Value, W("CC_Trade_Aside_Build", kv.Value.Named("COUNT")));
                // The drug policies: what each of the colony's people carries, and a season of what is scheduled.
                var drugs = new Dictionary<ThingDef, float>();
                foreach (var p in people)
                {
                    var policy = p.drugs?.CurrentPolicy;
                    if (policy == null) continue;
                    for (int i = 0; i < policy.Count; i++)
                    {
                        var e = policy[i];
                        if (e?.drug == null) continue;
                        float n = e.takeToInventory + (e.allowScheduled ? TradeAdviceMath.SeasonDays / Mathf.Max(0.25f, e.daysFrequency) : 0f);
                        if (n <= 0f) continue;
                        drugs.TryGetValue(e.drug, out float had);
                        drugs[e.drug] = had + n;
                    }
                }
                foreach (var kv in drugs)
                {
                    int n = Mathf.CeilToInt(kv.Value);
                    Add(kv.Key, null, n, W("CC_Trade_Aside_Drugs", n.Named("COUNT")));
                }
                foreach (var c in contracts)
                    if (c.theirs)
                        Add(c.item, c.stuff, c.amount, W("CC_Trade_Aside_Contract", c.amount.Named("COUNT"), c.reward.Named("REWARD")));
                var comp = CouncilGameComponent.Instance;
                if (comp != null && (ColonyCouncilMod.Settings?.enableWishes ?? true))
                {
                    foreach (var w in comp.OpenWishes)
                        if (w.promised && w.thingDef != null && w.Beneficiary != null)
                            Add(w.thingDef, null, 1, W("CC_Trade_Aside_Promise", w.Beneficiary.LabelShort.Named("PAWN")));
                    if (comp.kept?.watches != null)
                        foreach (var watch in comp.kept.watches)
                        {
                            if (watch?.things == null || watch.pawn == null || watch.phase != KeptPhase.Kept) continue;
                            foreach (var t in watch.things)
                                if (t != null && !keptThings.ContainsKey(t.thingIDNumber))
                                    keptThings[t.thingIDNumber] = W("CC_Trade_Aside_Kept", watch.pawn.LabelShort.Named("PAWN"));
                        }
                }
                // A caravan's own food and medicine are its supplies for the road home.
                foreach (var kv in carried)
                {
                    var kind = KindOf(kv.Key);
                    if (kind == TradeGood.Food || kind == TradeGood.Medicine || kind == TradeGood.Feed)
                        Add(kv.Key, null, kv.Value, W("CC_Trade_Aside_Caravan", kv.Value.Named("COUNT")));
                }
            }

            private int SetAside(ThingDef d, ThingDef stuff, out List<string> why)
            {
                why = new List<string>();
                if (d == null || !asides.TryGetValue(d, out var l)) return 0;
                int n = 0;
                foreach (var a in l)
                {
                    if (a.stuff != null && a.stuff != stuff) continue;
                    n += a.count;
                    why.Add(a.why);
                }
                return n;
            }

            // ---------------- Each row ----------------

            private float WorldOf(ThingDef d)
            {
                if (d == null) return 1f;
                if (!world.TryGetValue(d, out float f)) world[d] = f = VanillaTrading.WorldFactor(d);
                return f;
            }

            /// <summary>A row's counts and prices against the usual.</summary>
            private TradeNote Base(Tradeable t)
            {
                var n = new TradeNote();
                var thing = t.AnyThing;
                var d = t.ThingDef;
                n.canSell = t.CountHeldBy(Transactor.Colony) > 0 && t.TraderWillTrade;
                n.canBuy = t.CountHeldBy(Transactor.Trader) > 0 && t.TraderWillTrade;
                n.have = Mathf.Max(Stock(d), t.CountHeldBy(Transactor.Colony));
                n.setAside = SetAside(d, t.StuffDef, out var why);
                foreach (var t2 in t.thingsColony)
                    if (t2 != null && keptThings.TryGetValue(t2.GetInnerIfMinified().thingIDNumber, out string kw))
                    {
                        n.setAside++;
                        if (!why.Contains(kw)) why.Add(kw);
                    }
                n.aside.AddRange(why);
                if (!silver) return n;
                float factor = WorldOf(d);
                UsualPrices(thing, factor, gain, loss, out n.usualSell, out n.usualBuy);
                if (n.canSell) n.sellDeal = TradeAdviceMath.SellDeal(t.GetPriceFor(TradeAction.PlayerSells), n.usualSell);
                if (n.canBuy) n.buyDeal = TradeAdviceMath.BuyDeal(t.GetPriceFor(TradeAction.PlayerBuys), n.usualBuy);
                if (Mathf.Abs(factor - 1f) >= TradeAdviceMath.WorldShift)
                {
                    int pct = Mathf.Abs(TradeAdviceMath.PercentOff(factor));
                    n.world = W(factor > 1f ? "CC_Trade_World_Up" : "CC_Trade_World_Down", pct.Named("PCT"));
                }
                return n;
            }

            private void Mark(TradeNote n, TradeMark mark, int count, float priority, string why)
            {
                if (count <= 0) return;
                // A buy is never outdone; a sale by a buy; the higher priority wins between two of a kind.
                if (n.mark == TradeMark.Buy && mark != TradeMark.Buy) return;
                if (n.mark == mark && n.priority >= priority)
                {
                    if (why != null && !n.lines.Contains(why)) n.lines.Add(why);
                    return;
                }
                if (n.mark != mark) n.lines.Clear();
                n.mark = mark;
                n.count = count;
                n.priority = priority;
                if (why != null && !n.lines.Contains(why)) n.lines.Insert(0, why);
            }

            /// <summary>
            /// Worth buying: so many (no more than the trader has), toward a need shared by every row that answers it
            /// (<paramref name="group"/>, so much of it, each piece giving <paramref name="perUnit"/>); without a group,
            /// the row's own need of <paramref name="count"/> pieces.
            /// </summary>
            private void Buy(TradeNote n, Tradeable t, int count, float priority, string why, string group = null, float perUnit = 1f, float groupNeed = -1f)
            {
                if (!n.canBuy || count <= 0) return;
                bool takes = n.mark != TradeMark.Buy || n.priority < priority;
                Mark(n, TradeMark.Buy, Mathf.Min(t.CountHeldBy(Transactor.Trader), count), priority, why);
                if (!takes || n.mark != TradeMark.Buy) return;
                n.group = group ?? "row:" + t.GetHashCode();
                n.perUnit = perUnit;
                float need = groupNeed >= 0f ? groupNeed : count * perUnit;
                groupNeeds[n.group] = groupNeeds.TryGetValue(n.group, out float had) ? Mathf.Max(had, need) : need;
            }

            private void Sell(TradeNote n, Tradeable t, int count, float priority, string why)
            {
                if (!n.canSell) return;
                // A caravan's food and medicine are its supplies for the road: kept, never sold from it.
                var kind = KindOf(t.ThingDef);
                if (caravan != null && (kind == TradeGood.Food || kind == TradeGood.Feed || kind == TradeGood.Medicine)) return;
                // What is set aside stays: a sale takes only from the rest.
                int free = Mathf.Min(t.CountHeldBy(Transactor.Colony), n.have - n.setAside);
                Mark(n, TradeMark.Sell, Mathf.Min(free, count), priority, why);
            }

            /// <summary>The rules for one row, beyond food and medicine (FoodRows, MedicineRows).</summary>
            private void Row(Tradeable t, TradeNote n)
            {
                var d = t.ThingDef;
                var thing = t.AnyThing;
                var kind = KindOf(d);
                int colony = t.CountHeldBy(Transactor.Colony);
                float sellPrice = silver && n.canSell ? t.GetPriceFor(TradeAction.PlayerSells) : 0f;

                // No one here can use it.
                if (n.canSell && people.Count > 0)
                {
                    string unused = Unused(t, thing, d);
                    if (unused != null) Sell(n, t, colony, TradeAdviceMath.SellUnused, unused);
                }

                // Counted goods: the reserve, and a good price on what is beyond the need.
                float use = UseADay(d);
                if (kind != TradeGood.Food && kind != TradeGood.Feed && kind != TradeGood.Medicine && TradeAdviceMath.Counted(kind, use))
                {
                    int need = TradeAdviceMath.Need(kind, headcount, use);
                    int keep = TradeAdviceMath.Keep(kind, headcount, use, n.setAside);
                    int surplus = TradeAdviceMath.Surplus(n.have, keep);
                    string from = use > 0f && use * TradeAdviceMath.SeasonDays >= TradeAdviceMath.Floor(kind, headcount)
                        ? W("CC_Trade_From_Use", use.ToString(use < 10f ? "0.#" : "0").Named("USE"))
                        : W("CC_Trade_From_Floor", (TradeAdviceMath.Floor(kind, headcount) / headcount).Named("EACH"), headcount.Named("PEOPLE"));
                    if (n.canSell && TradeAdviceMath.WorthSaying(Mathf.Min(colony, surplus), n.have, sellPrice))
                        Sell(n, t, surplus, TradeAdviceMath.SellSurplus,
                            W("CC_Trade_Why_Surplus", n.have.Named("HAVE"), keep.Named("KEEP"), need.Named("NEED"), from.Named("FROM")));
                    else if (n.canSell && silver && n.sellDeal >= TradeAdviceMath.GoodSale)
                    {
                        int beyond = TradeAdviceMath.Surplus(n.have, need + n.setAside);
                        if (TradeAdviceMath.WorthSaying(Mathf.Min(colony, beyond), n.have, sellPrice))
                            Sell(n, t, beyond, TradeAdviceMath.SellGoodPrice,
                                W("CC_Trade_Why_GoodSale", TradeAdviceMath.PercentOff(n.sellDeal).Named("PCT"), n.have.Named("HAVE"), need.Named("NEED"), from.Named("FROM")));
                    }
                    // A bargain fills a stock the colony keeps and uses already (some in store, or a record of use), never a new one.
                    if (n.canBuy && silver && n.buyDeal >= TradeAdviceMath.Bargain && n.have < keep && (n.have > 0 || use > 0f))
                        Buy(n, t, keep - n.have, TradeAdviceMath.BuyBargain,
                            W("CC_Trade_Why_Bargain", Math.Abs(TradeAdviceMath.PercentOff(1f / Mathf.Max(0.01f, n.buyDeal))).Named("PCT"), keep.Named("KEEP"), n.have.Named("HAVE")));
                }

                // The worse of the spare weapons and clothes.
                if (kind == TradeGood.Equipment && n.canSell && home != null)
                {
                    int worse = WorseCopies(t, d, out int spares);
                    if (worse > 0)
                        Sell(n, t, worse, TradeAdviceMath.SellWorse, W("CC_Trade_Why_Worse", spares.Named("SPARES"), headcount.Named("PEOPLE")));
                }

                if (!n.canBuy) return;
                // What the plans need: the blueprints, the shopping list.
                var shop = view?.more?.shop?.FirstOrDefault(s => s.key == d.defName);
                int buildShort = needs?.goods?.FirstOrDefault(g => g.kind == AheadMath.Material && g.key == d.defName)?.shortForBuilding ?? 0;
                int shopShort = shop?.Short ?? 0;
                if (shopShort > 0 || buildShort > 0)
                {
                    var why = new List<string>();
                    if (buildShort > 0) why.Add(W("CC_Trade_Why_BuildShort", buildShort.Named("COUNT")));
                    if (shop != null && shopShort > 0)
                        why.Add(W("CC_Trade_Why_Shop", string.Join(ListSeparator, shop.reasons.Select(r => r.why).Distinct()).Named("LIST")));
                    int want = Mathf.Max(shopShort, buildShort);
                    Buy(n, t, want, TradeAdviceMath.BuyPlans, string.Join(" ", why), "def:" + d.defName, 1f, want);
                }

                // Components for repairs, where machines here break down.
                if (kind == TradeGood.Component && home != null)
                {
                    int machines = home.listerBuildings.allBuildingsColonist.Count(b => b.GetComp<CompBreakdownable>() != null);
                    int need = TradeAdviceMath.Need(kind, headcount, use);
                    int have = Stock(ThingDefOf.ComponentIndustrial) + Stock(ThingDefOf.ComponentSpacer);
                    if (machines > 0 && have < need)
                        Buy(n, t, need - have, TradeAdviceMath.BuyComponents,
                            W("CC_Trade_Why_Repairs", machines.Named("MACHINES"), have.Named("HAVE"), need.Named("NEED")), "components", 1f, need - have);
                }

                // Vanilla Trading Expanded's contracts.
                float buyPrice = silver ? t.GetPriceFor(TradeAction.PlayerBuys) : 0f;
                if (silver)
                    foreach (var c in contracts)
                    {
                        if (c.item != d || c.stuff != null && c.stuff != t.StuffDef || c.amount <= 0) continue;
                        if (c.theirs)
                        {
                            int have = Mathf.Min(n.have, c.amount);
                            int left = c.amount - have;
                            float cost = left * buyPrice;
                            if (left > 0 && cost <= c.reward * 0.8f)
                                Buy(n, t, left, TradeAdviceMath.BuyContract,
                                    W("CC_Trade_Why_Contract", c.amount.Named("AMOUNT"), c.reward.Named("REWARD"), have.Named("HAVE"), Money(cost).Named("COST")));
                        }
                        else if (c.amount * buyPrice < c.reward)
                            Buy(n, t, c.amount, TradeAdviceMath.BuyContract - 5f,
                                W("CC_Trade_Why_OwnContract", c.reward.Named("REWARD"), c.amount.Named("AMOUNT"), Money(c.amount * buyPrice).Named("COST")));
                    }

                // A colonist's open wish.
                var comp = CouncilGameComponent.Instance;
                if (comp != null && (ColonyCouncilMod.Settings?.enableWishes ?? true) && n.have == 0)
                    foreach (var w in comp.OpenWishes)
                    {
                        if (w.thingDef == null || w.Beneficiary == null || w.def?.Worker == null) continue;
                        bool about;
                        try
                        {
                            about = w.Worker.Concerns(w, d);
                        }
                        catch
                        {
                            about = w.thingDef == d;
                        }
                        if (!about) continue;
                        Buy(n, t, 1, TradeAdviceMath.BuyWish, W("CC_Trade_Why_Wish", w.Beneficiary.LabelShort.Named("PAWN"), w.LabelInline.Named("WISH")));
                        break;
                    }

                // The research under way: a techprint for it, or a book that helps it.
                var project = Find.ResearchManager?.GetProject();
                if (project != null && !project.IsFinished)
                {
                    var print = d.GetCompProperties<CompProperties_Techprint>();
                    if (print?.project == project && !project.TechprintRequirementMet)
                    {
                        int left = project.TechprintCount - project.TechprintsApplied - n.have;
                        if (left > 0)
                            Buy(n, t, left, TradeAdviceMath.BuyResearch, W("CC_Trade_Why_Techprint", project.LabelCap.ToString().Named("PROJECT"), left.Named("LEFT")));
                    }
                    else if (n.have == 0 && BookHelps(thing, project))
                        Buy(n, t, 1, TradeAdviceMath.BuyResearch, W("CC_Trade_Why_Book", project.LabelCap.ToString().Named("PROJECT")));
                }
            }

            /// <summary>Why no one here can use it (clothes no one can wear, a weapon biocoded to an outsider, a techprint learned, a drug for pleasure no policy allows), or null.</summary>
            private string Unused(Tradeable t, Thing thing, ThingDef d)
            {
                if (thing is Apparel ap)
                {
                    bool anyone = false;
                    foreach (var p in people)
                        if (ApparelUtility.HasPartsToWear(p, d) && ap.PawnCanWear(p))
                        {
                            anyone = true;
                            break;
                        }
                    if (!anyone) return W("CC_Trade_Why_CantWear");
                }
                var code = thing.TryGetComp<CompBiocodable>();
                if (code != null && code.Biocoded && code.CodedPawn != null && !people.Contains(code.CodedPawn))
                    return W("CC_Trade_Why_Biocoded", code.CodedPawn.LabelShort.Named("PAWN"));
                var print = d.GetCompProperties<CompProperties_Techprint>();
                if (print?.project != null && (print.project.IsFinished || print.project.TechprintRequirementMet))
                    return W("CC_Trade_Why_Learned");
                // A drug taken for pleasure that no policy allows; combat and work drugs are given by hand, so never.
                if (d.IsNonMedicalDrug && !d.IsMedicine && (d.ingestible?.joy ?? 0f) > 0f)
                {
                    bool allowed = false;
                    foreach (var p in people)
                    {
                        var e = EntryOf(p.drugs?.CurrentPolicy, d);
                        if (e != null && (e.allowedForAddiction || e.allowedForJoy || e.allowScheduled || e.takeToInventory > 0))
                        {
                            allowed = true;
                            break;
                        }
                    }
                    if (!allowed) return W("CC_Trade_Why_NoDrug");
                }
                return null;
            }

            /// <summary>A drug's line in a drug policy, or null (the game's own look-up throws for a drug it doesn't list).</summary>
            private static DrugPolicyEntry EntryOf(DrugPolicy policy, ThingDef drug)
            {
                if (policy == null) return null;
                for (int i = 0; i < policy.Count; i++)
                    if (policy[i]?.drug == drug)
                        return policy[i];
                return null;
            }

            /// <summary>Of this row's own things, those worse than the spares kept of their kind (the home's stored copies, and the caravan's).</summary>
            private int WorseCopies(Tradeable t, ThingDef d, out int spares)
            {
                spares = TradeAdviceMath.Spares(headcount);
                var copies = new List<Thing>();
                foreach (var c in home.listerThings.ThingsOfDef(d))
                    if (c != null && c.Spawned && !c.IsForbidden(Faction.OfPlayer) && c.IsInAnyStorage())
                        copies.Add(c);
                foreach (var c in t.thingsColony)
                    if (c != null && !copies.Contains(c))
                        copies.Add(c);
                if (copies.Count <= spares) return 0;
                var standings = copies.Select(c =>
                {
                    int q = c.TryGetQuality(out var qc) ? (int)qc : 2;
                    float hp = c.def.useHitPoints && c.MaxHitPoints > 0 ? (float)c.HitPoints / c.MaxHitPoints : 1f;
                    return TradeAdviceMath.Standing(q, hp, c is Apparel a && a.WornByCorpse);
                }).ToList();
                var worse = TradeAdviceMath.WorseCopies(standings, spares);
                int n = 0;
                for (int i = 0; i < copies.Count; i++)
                    if (worse[i] && t.thingsColony.Contains(copies[i]))
                        n += copies[i].stackCount;
                return n;
            }

            private static bool BookHelps(Thing thing, ResearchProjectDef project)
            {
                var book = thing.TryGetComp<CompBook>();
                if (book == null) return false;
                try
                {
                    foreach (var doer in book.Doers)
                        if (doer is ReadingOutcomeDoerGainResearch r && r.GetHyperlinks().Any(h => h.def == project))
                            return true;
                }
                catch
                {
                    // A book mod's own outcome that can't be read: not counted.
                }
                return false;
            }

            // ---------------- Food and medicine, as the colony's whole stock ----------------

            private float needDays, haveDays, feedHaveDays, reserveDays, withoutDays;

            private void Food()
            {
                if (needs == null) return;
                mealNutrition = needs.HaveOf(AheadMath.Meal);
                rawNutrition = needs.HaveOf(AheadMath.Raw);
                eatADay = needs.humanNeed;
                cooks = needs.bills.Any(b => b.kind == AheadMath.Meal) || needs.canAdd.Any(a => a.kind == AheadMath.Meal) || needs.dispenser;
                foodKnown = eatADay > 0.0001f;
                reserveDays = ColonyCouncilMod.Settings?.aheadReserveDays ?? 5f;
                withoutDays = view?.plan?.withoutDays ?? 0;
                needDays = TradeAdviceMath.FoodNeedDays(reserveDays + withoutDays);
                haveDays = TradeAdviceMath.FoodDays(mealNutrition, rawNutrition, eatADay, cooks);
                feedHaveDays = needs.feedNeed > 0.0001f ? needs.HaveOf(AheadMath.Feed) / needs.feedNeed : AheadMath.Far;
            }

            private float Nutrition(ThingDef d)
            {
                try
                {
                    return d.GetStatValueAbstract(StatDefOf.Nutrition);
                }
                catch
                {
                    return d.ingestible?.CachedNutrition ?? 0f;
                }
            }

            /// <summary>What a piece of food gives toward days of eating: a meal its nutrition, raw food the meals it cooks into.</summary>
            private float FoodWorth(ThingDef d) => Nutrition(d) * (ColonyNeeds.KindOf(d) == AheadMath.Raw && cooks ? TradeAdviceMath.CookGain : 1f);

            private void FoodRows(List<Tradeable> rows)
            {
                if (needs == null) return;
                FoodKind(rows, TradeGood.Food, eatADay, haveDays, TradeAdviceMath.BuyFood, "food", FoodWorth);
                if (needs.feedNeed > 0.0001f) FoodKind(rows, TradeGood.Feed, needs.feedNeed, feedHaveDays, TradeAdviceMath.BuyFeed, "feed", Nutrition);
            }

            private void FoodKind(List<Tradeable> rows, TradeGood kind, float aDay, float have, float buyPriority, string group, Func<ThingDef, float> worth)
            {
                if (aDay <= 0.0001f) return;
                var mine = rows.Where(t => KindOf(t.ThingDef) == kind).ToList();
                bool people = kind == TradeGood.Food;
                // More than needed: shared out over the colony's own stocks of it here, the biggest first.
                float surplusDays = TradeAdviceMath.FoodSurplusDays(have, needDays);
                if (surplusDays > 0f)
                {
                    var selling = mine.Where(t => notes[t].canSell).ToList();
                    var stocks = selling.Select(t => (Mathf.Min(t.CountHeldBy(Transactor.Colony), notes[t].have - notes[t].setAside), worth(t.ThingDef))).ToList();
                    var share = TradeAdviceMath.ShareOut(surplusDays * aDay, stocks);
                    string why = W(people ? "CC_Trade_Why_FoodSurplus" : "CC_Trade_Why_FeedSurplus", Days(have).Named("HAVE"),
                        Days(TradeAdviceMath.ReserveTimes * needDays).Named("KEEP"), Days(needDays).Named("NEED"));
                    for (int i = 0; i < selling.Count; i++)
                    {
                        var t = selling[i];
                        if (TradeAdviceMath.WorthSaying(share[i], t.CountHeldBy(Transactor.Colony), silver ? t.GetPriceFor(TradeAction.PlayerSells) : 0f))
                            Sell(notes[t], t, share[i], TradeAdviceMath.SellSurplus, why);
                    }
                }
                // Short: what the trader has of it, shared as one need in the basket, the cheapest for what it gives first.
                float shortDays = TradeAdviceMath.FoodShortDays(have, needDays);
                if (shortDays > 0f)
                {
                    float needNutrition = shortDays * aDay;
                    string why = people
                        ? W("CC_Trade_Why_FoodShort", Days(have).Named("HAVE"), Days(needDays).Named("NEED"), Days(reserveDays).Named("RESERVE"), Days(withoutDays).Named("WINTER"), Days(TradeAdviceMath.FoodDaysAtLeast).Named("LEAST"))
                        : W("CC_Trade_Why_FeedShort", Days(have).Named("HAVE"), Days(needDays).Named("NEED"));
                    foreach (var t in mine)
                    {
                        var n = notes[t];
                        float per = worth(t.ThingDef);
                        if (!n.canBuy || per <= 0f) continue;
                        // Food that would rot before it is eaten, with nothing to keep it cold, comes after the rest.
                        var rot = t.ThingDef.GetCompProperties<CompProperties_Rottable>();
                        bool rots = rot != null && rot.daysToRotStart > 0f && rot.daysToRotStart < Mathf.Min(needDays, shortDays) && !needs.freezer;
                        Buy(n, t, Mathf.CeilToInt(needNutrition / per), buyPriority - (rots ? 5f : 0f), why, group, per, needNutrition);
                    }
                }
            }

            private void MedicineRows(List<Tradeable> rows)
            {
                var row = view?.plan?.Row(AheadMath.Medicine);
                float use = row?.useADay ?? 0f;
                int need = TradeAdviceMath.Need(TradeGood.Medicine, headcount, use, row?.target ?? 0f);
                int have = needs != null ? Mathf.RoundToInt(needs.HaveOf(AheadMath.Medicine)) : 0;
                have += carried.Where(kv => kv.Key.IsMedicine).Sum(kv => kv.Value);
                var mine = rows.Where(t => KindOf(t.ThingDef) == TradeGood.Medicine).ToList();
                int keep = TradeAdviceMath.Keep(TradeGood.Medicine, headcount, use, 0, row?.target ?? 0f);
                if (have > keep)
                {
                    var selling = mine.Where(t => notes[t].canSell).ToList();
                    var share = TradeAdviceMath.ShareOut(have - keep, selling.Select(t => (Mathf.Min(t.CountHeldBy(Transactor.Colony), notes[t].have - notes[t].setAside), 1f)).ToList());
                    string why = W("CC_Trade_Why_MedicineSurplus", have.Named("HAVE"), keep.Named("KEEP"), need.Named("NEED"));
                    for (int i = 0; i < selling.Count; i++)
                        if (TradeAdviceMath.WorthSaying(share[i], have, silver ? selling[i].GetPriceFor(TradeAction.PlayerSells) : 0f))
                            Sell(notes[selling[i]], selling[i], share[i], TradeAdviceMath.SellSurplus, why);
                }
                else if (have < need)
                {
                    string why = W("CC_Trade_Why_MedicineShort", have.Named("HAVE"), need.Named("NEED"));
                    foreach (var t in mine) Buy(notes[t], t, need - have, TradeAdviceMath.BuyMedicine, why, "medicine", 1f, need - have);
                }
            }

            // ---------------- The words ----------------

            private void Finish(Tradeable t, TradeNote n)
            {
                if (n.mark == TradeMark.None && n.setAside > 0 && t.CountHeldBy(Transactor.Colony) > 0) n.mark = TradeMark.Keep;
                switch (n.mark)
                {
                    case TradeMark.Sell:
                        n.head = W("CC_Trade_Head_Sell", n.count.Named("COUNT"));
                        break;
                    case TradeMark.Buy:
                        n.head = W("CC_Trade_Head_Buy", n.count.Named("COUNT"));
                        break;
                    case TradeMark.Keep:
                        n.head = n.setAside >= n.have ? W("CC_Trade_Head_Keep") : W("CC_Trade_Head_KeepSome", n.setAside.Named("COUNT"), n.have.Named("HAVE"));
                        break;
                }
                if (!silver) return;
                if (n.mark == TradeMark.Sell || n.mark == TradeMark.Keep && n.canSell)
                    n.price = W("CC_Trade_Price_Sell", Usual(n.sellDeal, true).Named("HOW"));
                else if (n.mark == TradeMark.Buy)
                    n.price = W("CC_Trade_Price_Buy", Usual(n.buyDeal, false).Named("HOW"));
                // Where its market price stands and goes, as a reason more for the sale or the buy (never a mark of its own).
                if (n.mark == TradeMark.Sell || n.mark == TradeMark.Buy) n.trend = TradeMarket.HintLine(t.ThingDef, n.mark == TradeMark.Sell);
            }
        }

        /// <summary>Each shared need of the basket, as the last reading found it.</summary>
        public static Dictionary<string, float> GroupNeeds
        {
            get
            {
                Refresh();
                return groupNeeds;
            }
        }

        /// <summary>The whole tooltip of a row's mark.</summary>
        public static string Tip(TradeNote n, Tradeable t)
        {
            if (n == null) return null;
            var parts = new List<string>();
            if (!n.head.NullOrEmpty()) parts.Add(n.head);
            parts.AddRange(n.lines);
            if (n.aside.Count > 0) parts.Add(W("CC_Trade_Why_SetAside", string.Join(", ", n.aside).Named("LIST")));
            if (n.setAside > 0 && t != null && t.CountToTransfer < 0 && n.have + t.CountToTransfer < n.setAside) parts.Add(W("CC_Trade_KeepWarn"));
            if (!n.price.NullOrEmpty()) parts.Add(n.price);
            if (!n.world.NullOrEmpty()) parts.Add(n.world);
            if (!n.trend.NullOrEmpty()) parts.Add(n.trend);
            // What this trade is likely to do to its market price (Vanilla Trading Expanded's rule), as the counts stand now.
            if (t != null && t.CountToTransfer != 0 && TradeMarket.History)
            {
                string impact = TradeMarket.ImpactLine(t.ThingDef);
                if (!impact.NullOrEmpty()) parts.Add(impact);
            }
            return string.Join("\n\n", parts);
        }
    }
}
