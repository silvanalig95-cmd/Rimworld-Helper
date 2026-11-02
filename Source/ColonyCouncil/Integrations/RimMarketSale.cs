using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A sale off a colony stall's counter to someone who walked over to buy (Visitors/VisitorTrade.cs), done the way
    /// RimMarket's own shoppers buy (its ColonyStalls.Sell): the article taken off the counter and off its sale line, the
    /// market's demand for it noted, the silver taken from the buyer's purse and set down at the stall for someone to put
    /// away, the stall's takings and sales counted, the colony's sale recorded, the keeper's practice at talking; then the
    /// shopper's own touches (the coins' sound, what it cost over their head, a good mood). Every member is RimMarket's,
    /// found by name; when one the sale needs isn't there, nobody buys.
    /// </summary>
    public static partial class RimMarketStall
    {
        private static bool saleLooked, saleReady;
        private static PropertyInfo stallComp, stockOwner, managerInstance;
        private static FieldInfo stallTakings, stallSales, shopFrom, shopTo;
        private static MethodInfo orderFor, openForSale, keeperAt, keeperFactor, saleFactor, cashCell, takeAndPlace, browseCell, demandOf, demandRecord, vteSold, colonySale, hegemonyTrade;
        private static bool touchesLooked;
        private static SoundDef silverDrop;
        private static ThoughtDef boughtThought;

        /// <summary>RimMarket is loaded and its shoppers' sale was found: someone may buy off a colony stall's counter.</summary>
        public static bool SaleReady
        {
            get
            {
                LookSale();
                return saleReady;
            }
        }

        private static void LookSale()
        {
            if (saleLooked) return;
            saleLooked = true;
            Look();
            try
            {
                if (stallType == null || colonyStall == null || stock == null || stockDef == null || tradeable == null || baseValueThing == null || chanceFor == null
                    || notifySold == null || orderPrice == null) return;
                var stalls = AccessTools.TypeByName("RimMarket.Market.ColonyStalls");
                var utility = AccessTools.TypeByName("RimMarket.Market.MarketUtility");
                var demand = AccessTools.TypeByName("RimMarket.Market.MarketDemand");
                var manager = AccessTools.TypeByName("RimMarket.Market.MarketManager");
                var comp = AccessTools.TypeByName("RimMarket.Buildings.CompMarketStall");
                var vte = AccessTools.TypeByName("RimMarket.Compat.VteBridge");
                var hegemony = AccessTools.TypeByName("RimMarket.Compat.HegemonyBridge");
                if (stalls == null || utility == null || comp == null) return;
                stallComp = AccessTools.Property(stallType, "Stall");
                stockOwner = AccessTools.Property(stallType, "StockOwner");
                orderFor = AccessTools.Method(stallType, "OrderFor", new[] { typeof(ThingDef) });
                stallTakings = AccessTools.Field(stallType, "takings");
                stallSales = AccessTools.Field(stallType, "sales");
                if (stallTakings?.FieldType != typeof(int)) stallTakings = null;
                if (stallSales?.FieldType != typeof(int)) stallSales = null;
                openForSale = AccessTools.Method(stalls, "OpenForSale", new[] { stallType });
                keeperAt = AccessTools.Method(stalls, "KeeperAt", new[] { stallType });
                keeperFactor = AccessTools.Method(stalls, "KeeperFactor", new[] { stallType });
                saleFactor = AccessTools.Method(stalls, "SaleFactor", new[] { typeof(ThingDef) });
                cashCell = AccessTools.Method(stalls, "CashCell", new[] { stallType });
                shopFrom = AccessTools.Field(stalls, "ShopHoursStart");
                shopTo = AccessTools.Field(stalls, "ShopHoursEnd");
                takeAndPlace = AccessTools.Method(utility, "TakeAndPlacePayment", new[] { typeof(List<Pawn>), typeof(int), typeof(bool), typeof(IntVec3), typeof(Map) });
                browseCell = AccessTools.Method(utility, "FindBrowseCell", new[] { comp, typeof(Pawn) });
                if (demand != null)
                {
                    demandOf = AccessTools.Method(demand, "Demand", new[] { typeof(ThingDef) });
                    demandRecord = AccessTools.Method(demand, "Record", new[] { typeof(ThingDef), typeof(float) });
                }
                if (manager != null)
                {
                    managerInstance = AccessTools.Property(manager, "Instance");
                    colonySale = AccessTools.Method(manager, "RecordColonySale", new[] { typeof(int) });
                }
                if (vte != null) vteSold = AccessTools.Method(vte, "Notify_PlayerSold", new[] { typeof(Thing), typeof(int) });
                if (hegemony != null) hegemonyTrade = AccessTools.Method(hegemony, "RecordTrade", new[] { typeof(Faction), typeof(float) });
                saleReady = stallComp != null && stockOwner != null && orderFor != null && stallTakings != null && stallSales != null && openForSale != null
                            && takeAndPlace != null && takeAndPlace.ReturnType == typeof(int) && browseCell != null && browseCell.ReturnType == typeof(IntVec3);
                if (!saleReady) Log.Warning("[Rimfolk] RimMarket's stalls don't sell the way Rimfolk knows, so visitors and campers don't buy at them.");
            }
            catch (Exception e)
            {
                saleReady = false;
                CouncilLog.ErrorOnce("RimMarket sale", e);
            }
        }

        /// <summary>The market's hours, as RimMarket keeps them for its shoppers (8 to 18 when it doesn't say).</summary>
        public static int ShopFrom => Hour(shopFrom, VisitorTradeMath.ShopFrom);

        public static int ShopTo => Hour(shopTo, VisitorTradeMath.ShopTo);

        private static int Hour(FieldInfo f, int otherwise)
        {
            LookSale();
            try
            {
                object v = f == null ? null : f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
                return v is int h && h >= 0 && h <= 24 ? h : otherwise;
            }
            catch
            {
                return otherwise;
            }
        }

        /// <summary>The colony's stalls on the map open for sale now, as RimMarket decides it (in an open market, a keeper there if it asks for one, goods on the counter).</summary>
        public static List<Thing> OpenStalls(Map map)
        {
            var list = new List<Thing>();
            if (map == null || !SaleReady) return list;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (stallType.IsInstanceOfType(b) && b.Spawned && IsColonyStall(b) && OpenForSale(b)) list.Add(b);
            return list;
        }

        /// <summary>The colony's stalls on the map with something for sale on the counter, open now or not (market days ask for these).</summary>
        public static List<Thing> StockedStalls(Map map)
        {
            var list = new List<Thing>();
            if (map == null || !SaleReady) return list;
            try
            {
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                    if (stallType.IsInstanceOfType(b) && b.Spawned && IsColonyStall(b) && Stock(b).Any(t => ForSale(b, t))) list.Add(b);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stocked stalls", e);
            }
            return list;
        }

        public static bool OpenForSale(Thing stall) => stall != null && SaleReady && openForSale.Invoke(null, new object[] { stall }) is bool b && b;

        private static object OrderOf(Thing stall, ThingDef def) => def == null ? null : orderFor.Invoke(stall, new object[] { def });

        /// <summary>On the counter and for sale, by RimMarket's own test: not silver, a tradeable thing, on one of the stall's sale lines.</summary>
        public static bool ForSale(Thing stall, Thing t) =>
            t != null && !t.Destroyed && t.stackCount > 0 && t.def != ThingDefOf.Silver && t.ParentHolder == stall && Tradeable(t) && OrderOf(stall, StockDef(t)) != null;

        /// <summary>The line's asking price for an article, as a share of its sell value.</summary>
        public static float PriceFactor(Thing stall, ThingDef def) => OrderOf(stall, def) is object o && orderPrice.GetValue(o) is float f ? f : 0f;

        /// <summary>The colony's keeper at the stall, if one is there now (RimMarket's KeeperAt).</summary>
        public static Pawn Keeper(Thing stall) => keeperAt?.Invoke(null, new object[] { stall }) as Pawn;

        /// <summary>One of the article for a buyer at this stall, as RimMarket prices a shopper's sale.</summary>
        public static float UnitPrice(Thing stall, Thing t)
        {
            var def = StockDef(t);
            float keeper = keeperFactor?.Invoke(null, new object[] { stall }) is float k ? k : 1f;
            float sale = saleFactor?.Invoke(null, new object[] { def }) is float s ? s : 1f;
            return VisitorTradeMath.UnitPrice(BaseValue(t), PriceFactor(stall, def), keeper, sale);
        }

        /// <summary>The chance one who fancies the article buys it at its line's price, as RimMarket reckons it for a shopper.</summary>
        public static float BuyChance(Thing stall, ThingDef def)
        {
            float d = demandOf?.Invoke(null, new object[] { def }) is float x ? x : 1f;
            return VisitorTradeMath.BuyChance(ChanceFor(PriceFactor(stall, def)), Keeper(stall) != null, d);
        }

        /// <summary>A free place in front of the counter to stand and look over the goods (RimMarket's own, where its shoppers stand).</summary>
        public static IntVec3 BrowseCell(Thing stall, Pawn p)
        {
            if (stall == null || p == null || !SaleReady) return IntVec3.Invalid;
            var comp = stallComp.GetValue(stall);
            if (comp == null) return IntVec3.Invalid;
            return browseCell.Invoke(null, new object[] { comp, p }) is IntVec3 c ? c : IntVec3.Invalid;
        }

        /// <summary>Where RimMarket sets down what a shopper pays: the merchant's place behind the counter, else the stall.</summary>
        private static IntVec3 CashCell(Thing stall) => cashCell?.Invoke(null, new object[] { stall }) is IntVec3 c && c.IsValid ? c : stall.Position;

        /// <summary>Silver in someone's pack.</summary>
        public static int SilverIn(Pawn p) => p?.inventory?.innerContainer?.TotalStackCountOfDef(ThingDefOf.Silver) ?? 0;

        /// <summary>A sale made at a stall: what went (its kind, how many, its label as a stack and short) and what was paid.</summary>
        public struct StallSale
        {
            public ThingDef def;
            public int count, paid;
            public string label, labelShort;
        }

        /// <summary>
        /// Sells <paramref name="count"/> of the article on the stall's counter to the buyer, as RimMarket sells to its
        /// shoppers: off the counter into their pack, off its sale line, the silver from their pack set down at the stall.
        /// False (and nothing changed) when it can't be: the stall closed, the article gone, too little silver.
        /// </summary>
        public static bool SellTo(Pawn buyer, Thing stall, Thing article, int count, out StallSale sale)
        {
            sale = default;
            if (buyer?.inventory == null || stall == null || !stall.Spawned || count <= 0 || !OpenForSale(stall) || !ForSale(stall, article)) return false;
            var def = StockDef(article);
            count = Math.Min(count, article.stackCount);
            int price = VisitorTradeMath.Total(UnitPrice(stall, article), count);
            if (SilverIn(buyer) < price || !(stockOwner.GetValue(stall) is ThingOwner owner)) return false;
            var taken = owner.Take(article, count);
            if (taken == null) return false;
            int paid;
            try
            {
                paid = takeAndPlace.Invoke(null, new object[] { new List<Pawn> { buyer }, price, false, CashCell(stall), stall.Map }) is int n ? n : 0;
            }
            catch (Exception e)
            {
                // Not paid: the article goes back on the counter, and nothing was sold.
                owner.TryAdd(taken, true);
                CouncilLog.ErrorOnce("RimMarket sale payment", e);
                return false;
            }
            sale = new StallSale { def = def, count = taken.stackCount, paid = paid, label = taken.Label, labelShort = taken.LabelShort };
            notifySold.Invoke(stall, new object[] { def, taken.stackCount });
            Also(() => demandRecord?.Invoke(null, new object[] { def, (float)price }), "demand");
            Also(() => vteSold?.Invoke(null, new object[] { taken, taken.stackCount }), "trade economy");
            // Into their pack last: it may join a stack they already carry.
            buyer.inventory.TryAddItemNotForSale(taken);
            stallTakings.SetValue(stall, (int)stallTakings.GetValue(stall) + paid);
            stallSales.SetValue(stall, (int)stallSales.GetValue(stall) + 1);
            Also(() =>
            {
                var mgr = managerInstance?.GetValue(null);
                if (mgr != null) colonySale?.Invoke(mgr, new object[] { paid });
            }, "colony sale");
            Also(() => hegemonyTrade?.Invoke(null, new object[] { buyer.Faction, (float)paid }), "faction trade");
            Keeper(stall)?.skills?.Learn(SkillDefOf.Social, 30f);
            return true;
        }

        /// <summary>The shopper's own touches after a sale, as RimMarket gives them: the coins' sound at the stall, the article and its price over the buyer's head, a good mood for it.</summary>
        public static void BoughtTouches(Pawn buyer, Thing stall, StallSale sale)
        {
            if (!touchesLooked)
            {
                touchesLooked = true;
                silverDrop = DefDatabase<SoundDef>.GetNamedSilentFail("Silver_Drop");
                boughtThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimMarket_BoughtAtMarket");
            }
            try
            {
                if (silverDrop != null && stall != null && stall.Spawned) silverDrop.PlayOneShot(new TargetInfo(stall.Position, stall.Map));
                if (buyer != null && buyer.Spawned && !sale.labelShort.NullOrEmpty())
                {
                    string key = "RimMarket.MoteBought".CanTranslate() ? "RimMarket.MoteBought" : "CC_VisitTrade_Mote";
                    MoteMaker.ThrowText(buyer.DrawPos, buyer.Map, key.Translate(sale.labelShort, sale.paid).Resolve(), 3.5f);
                }
                if (boughtThought != null) buyer?.needs?.mood?.thoughts?.memories?.TryGainMemory(boughtThought);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket sale touches", e);
            }
        }

        /// <summary>A part of the sale that only keeps RimMarket's records: should it fail, the sale stands.</summary>
        private static void Also(Action a, string what)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket sale " + what, e);
            }
        }
    }
}
