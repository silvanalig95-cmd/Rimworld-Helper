using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// RimMarket (forti.rimmarket), met softly: a colony stall's sales table opens as Rimfolk's own window
    /// (UI/Window_StallCounter.cs) instead of RimMarket's, with the setting "RimMarket's sales table, Rimfolk's way" on.
    /// Everything is RimMarket's: its stall keeps its sale lines (article, how many to sell, the price as a share of the
    /// sell value, the quality range, how many sold), and the window reads and sets them through the stall's own
    /// SetOrder, its prices and the shoppers' pull through RimMarket's ColonyStalls. All of it is found by name; when
    /// anything isn't there (another RimMarket version), RimMarket's own window opens as before. And with a powered
    /// orbital trade beacon on the map, what lies on a colony stall's counter is offered to trade ships too
    /// (<see cref="Patch_StallGoodsForShips"/>), the setting "RimMarket's stall goods for trade ships".
    /// </summary>
    public static class RimMarketStall
    {
        public const string PackageId = "forti.rimmarket";

        private static bool looked, ready, shipsReady;
        private static Type dialogType, stallType;
        private static FieldInfo dialogStall, orderDef, orderToSell, orderPrice, orderSold, orderQuality, shoppersVisit;
        private static PropertyInfo colonyStall, orders, stock, settings;
        private static MethodInfo setOrder, stockDef, canSell, baseValueThing, baseValueDef, pull, interval, chanceFor, tradeable, notifySold;

        /// <summary>One sale line of a stall, as read from RimMarket.</summary>
        public struct Line
        {
            public object order;
            public ThingDef def;
            public int toSell, sold;
            public float price;
            public QualityRange quality;
        }

        /// <summary>RimMarket is loaded and everything the window needs from it was found.</summary>
        public static bool Ready
        {
            get
            {
                Look();
                return ready;
            }
        }

        /// <summary>Rimfolk's window takes the place of RimMarket's sales table now.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.marketStallWindow ?? true) && Ready;

        /// <summary>A colony stall's goods are offered to trade ships when there's a powered beacon (and what that needs was found).</summary>
        public static bool ShipsOn
        {
            get
            {
                if (!RimfolkPatches.On || !(ColonyCouncilMod.Settings?.marketStallShips ?? true)) return false;
                Look();
                return shipsReady;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(PackageId)) return;
                dialogType = AccessTools.TypeByName("RimMarket.UI.Dialog_ColonyStall");
                stallType = AccessTools.TypeByName("RimMarket.Buildings.Building_MarketStall");
                var orderType = AccessTools.TypeByName("RimMarket.Buildings.StallOrder");
                var stalls = AccessTools.TypeByName("RimMarket.Market.ColonyStalls");
                var utility = AccessTools.TypeByName("RimMarket.Market.MarketUtility");
                var mod = AccessTools.TypeByName("RimMarket.RimMarketMod");
                if (dialogType == null || stallType == null || orderType == null || stalls == null || utility == null || mod == null) return;
                dialogStall = AccessTools.Field(dialogType, "stall");
                orderDef = AccessTools.Field(orderType, "def");
                orderToSell = AccessTools.Field(orderType, "toSell");
                orderPrice = AccessTools.Field(orderType, "priceFactor");
                orderSold = AccessTools.Field(orderType, "sold");
                orderQuality = AccessTools.Field(orderType, "quality");
                colonyStall = AccessTools.Property(stallType, "ColonyStall");
                orders = AccessTools.Property(stallType, "Orders");
                stock = AccessTools.Property(stallType, "Stock");
                setOrder = AccessTools.Method(stallType, "SetOrder", new[] { typeof(ThingDef), typeof(int), typeof(float), typeof(QualityRange) });
                stockDef = AccessTools.Method(stalls, "StockDef", new[] { typeof(Thing) });
                canSell = AccessTools.Method(stalls, "CanSell", new[] { typeof(ThingDef) });
                baseValueThing = AccessTools.Method(stalls, "BaseValue", new[] { typeof(Thing) });
                baseValueDef = AccessTools.Method(stalls, "BaseValue", new[] { typeof(ThingDef) });
                pull = AccessTools.Method(stalls, "Pull", new[] { typeof(Map) });
                interval = AccessTools.Method(stalls, "ShopperIntervalTicks", new[] { typeof(float) });
                chanceFor = AccessTools.Method(stalls, "ChanceFor", new[] { typeof(float) });
                tradeable = AccessTools.Method(utility, "Tradeable", new[] { typeof(Thing) });
                notifySold = AccessTools.Method(stallType, "Notify_Sold", new[] { typeof(ThingDef), typeof(int) });
                settings = AccessTools.Property(mod, "Settings");
                shoppersVisit = settings != null ? AccessTools.Field(settings.PropertyType, "shoppersVisit") : null;
                ready = dialogStall != null && orderDef != null && orderToSell != null && orderPrice != null && orderSold != null && orderQuality != null
                        && orderQuality.FieldType == typeof(QualityRange) && colonyStall != null && orders != null && stock != null && setOrder != null
                        && stockDef != null && canSell != null && baseValueThing != null && baseValueDef != null && pull != null && interval != null
                        && chanceFor != null && tradeable != null;
                // Trade ships only need the stall, its counter and which article a thing is (its sale line, to count one sold).
                shipsReady = colonyStall != null && stock != null && stockDef != null;
                if (!ready) Log.Warning("[Rimfolk] RimMarket's sales table isn't the one Rimfolk knows, so RimMarket's own window stays.");
                if (!shipsReady) Log.Warning("[Rimfolk] RimMarket's stalls aren't the ones Rimfolk knows, so trade ships don't see their goods.");
            }
            catch (Exception e)
            {
                ready = shipsReady = false;
                CouncilLog.ErrorOnce("RimMarket", e);
            }
        }

        /// <summary>The window being opened is RimMarket's sales table.</summary>
        public static bool IsTheirTable(Window w) => w != null && Ready && w.GetType() == dialogType;

        public static Thing StallOf(Window w) => dialogStall.GetValue(w) as Thing;

        public static bool IsColonyStall(Thing stall) => stall != null && colonyStall.GetValue(stall) is bool b && b;

        /// <summary>The stall's sale lines, into the list.</summary>
        public static void Lines(Thing stall, List<Line> into)
        {
            into.Clear();
            if (!(orders.GetValue(stall) is System.Collections.IEnumerable list)) return;
            foreach (var o in list)
            {
                if (o == null || !(orderDef.GetValue(o) is ThingDef def)) continue;
                into.Add(new Line
                {
                    order = o,
                    def = def,
                    toSell = (int)orderToSell.GetValue(o),
                    sold = (int)orderSold.GetValue(o),
                    price = (float)orderPrice.GetValue(o),
                    quality = (QualityRange)orderQuality.GetValue(o)
                });
            }
        }

        /// <summary>What lies on the stall's counter now.</summary>
        public static IReadOnlyList<Thing> Stock(Thing stall) => stock.GetValue(stall) as IReadOnlyList<Thing> ?? new List<Thing>();

        /// <summary>Sets a line (RimMarket's SetOrder): none or fewer than one to sell takes it off the table.</summary>
        public static void SetLine(Thing stall, ThingDef def, int toSell, float price, QualityRange quality) =>
            setOrder.Invoke(stall, new object[] { def, toSell, price, quality });

        /// <summary>A new price on a line, as RimMarket's own window sets it (straight onto the line, nothing else moves).</summary>
        public static void SetPrice(object order, float price) => orderPrice.SetValue(order, price);

        public static ThingDef StockDef(Thing t) => stockDef.Invoke(null, new object[] { t }) as ThingDef;

        /// <summary>
        /// What lies on the counters of the colony's own stalls on the map that a trade ship may buy (never silver, which
        /// a stall doesn't sell). The beacon's rule for what can be sold holds for them as for anything else.
        /// </summary>
        public static List<Thing> ShipGoods(Map map, ITrader trader)
        {
            var list = new List<Thing>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!stallType.IsInstanceOfType(b) || !b.Spawned || !IsColonyStall(b)) continue;
                foreach (var t in Stock(b))
                    if (t != null && !t.Destroyed && t.def != ThingDefOf.Silver && TradeUtility.PlayerSellableNow(t, trader)) list.Add(t);
            }
            return list;
        }

        /// <summary>The colony stall on the map on whose counter this lies, or null.</summary>
        public static Thing CounterOf(Thing t) =>
            t?.ParentHolder is Thing stall && stallType != null && stallType.IsInstanceOfType(stall) && stall.Spawned && IsColonyStall(stall) ? stall : null;

        /// <summary>Something off a colony stall's counter was sold to a trade ship: it comes off the stall's line, as a sale at the stall does.</summary>
        public static void SoldToShip(Thing t, int count)
        {
            if (notifySold == null || !(t?.ParentHolder is Thing stall) || !stallType.IsInstanceOfType(stall) || !IsColonyStall(stall)) return;
            var def = StockDef(t);
            if (def != null) notifySold.Invoke(stall, new object[] { def, Math.Min(count, t.stackCount) });
        }

        public static bool CanSell(ThingDef def) => canSell.Invoke(null, new object[] { def }) is bool b && b;

        public static bool Tradeable(Thing t) => tradeable.Invoke(null, new object[] { t }) is bool b && b;

        /// <summary>An article's sell value, as RimMarket prices it (its market value times its sell price factor).</summary>
        public static float BaseValue(Thing t) => baseValueThing.Invoke(null, new object[] { t }) is float f ? f : 0f;

        public static float BaseValue(ThingDef def) => baseValueDef.Invoke(null, new object[] { def }) is float f ? f : 0f;

        /// <summary>The chance a customer who fancies an article buys it at this price.</summary>
        public static float ChanceFor(float price) => chanceFor.Invoke(null, new object[] { price }) is float f ? f : 0f;

        /// <summary>How often a group of shoppers comes at the colony's prices (ticks), or -1 with shoppers switched off.</summary>
        public static int ShopperTicks(Map map, out float draw)
        {
            draw = pull.Invoke(null, new object[] { map }) is float f ? f : 1f;
            object s = settings?.GetValue(null);
            if (s != null && shoppersVisit != null && shoppersVisit.GetValue(s) is bool on && !on) return -1;
            return interval.Invoke(null, new object[] { draw }) is int t ? t : -1;
        }
    }

    /// <summary>RimMarket's sales table opens as Rimfolk's (Integrations/RimMarketStall.cs).</summary>
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    public static class Patch_MarketStallWindow
    {
        public static void Prefix(ref Window window)
        {
            if (window == null || !RimMarketStall.On || !RimMarketStall.IsTheirTable(window)) return;
            try
            {
                var stall = RimMarketStall.StallOf(window);
                if (stall != null) window = new Window_StallCounter(stall);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket sales table", e);
            }
        }
    }

    /// <summary>
    /// With a powered orbital trade beacon on the map, what lies on a colony stall's counter is offered to a trade ship
    /// too, after what lies in the beacon's range, so a ship buys from the stockpile first (Integrations/RimMarketStall.cs).
    /// A stall is never a beacon of its own: with no powered beacon, it adds nothing.
    /// </summary>
    [HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.AllLaunchableThingsForTrade))]
    public static class Patch_StallGoodsForShips
    {
        public static bool Prepare() => ModsConfig.IsActive(RimMarketStall.PackageId);

        public static void Postfix(Map map, ITrader trader, ref IEnumerable<Thing> __result)
        {
            if (map == null || __result == null || !RimMarketStall.ShipsOn) return;
            __result = WithStalls(__result, map, trader);
        }

        private static IEnumerable<Thing> WithStalls(IEnumerable<Thing> inRange, Map map, ITrader trader)
        {
            foreach (var t in inRange) yield return t;
            List<Thing> stalls = null;
            try
            {
                if (Building_OrbitalTradeBeacon.AllPowered(map).Any()) stalls = RimMarketStall.ShipGoods(map, trader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stall goods for trade ships", e);
            }
            if (stalls == null) yield break;
            foreach (var t in stalls) yield return t;
        }
    }

    /// <summary>
    /// The game's trade deal keeps only what lies on the map, and what lies on a few shelves it knows (a gene bank, a
    /// bookcase, an outfit stand). A colony stall holds its counter goods itself, so the deal let them all go again after
    /// <see cref="Patch_StallGoodsForShips"/> offered them. For a trade ship they now stand where their stall stands:
    /// what keeps goods there from being sold (fog, a building that forbids selling near it) keeps them too.
    /// </summary>
    [HarmonyPatch(typeof(TradeDeal), "InSellablePosition")]
    public static class Patch_StallGoodsSellable
    {
        private delegate bool SellableAt(TradeDeal deal, Thing t, out string reason);

        private static SellableAt sellableAt;

        public static bool Prepare() => ModsConfig.IsActive(RimMarketStall.PackageId);

        public static void Postfix(TradeDeal __instance, Thing t, ref bool __result, ref string reason)
        {
            if (__result || t == null || t.Spawned || !(TradeSession.trader is TradeShip) || !RimMarketStall.ShipsOn) return;
            try
            {
                var stall = RimMarketStall.CounterOf(t);
                if (stall == null) return;
                sellableAt ??= AccessTools.MethodDelegate<SellableAt>(AccessTools.Method(typeof(TradeDeal), "InSellablePosition"));
                // The stall itself is on the map, so the game's own answer for its place is the one for its goods.
                __result = sellableAt(__instance, stall, out reason);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stall goods in the trade deal", e);
            }
        }
    }

    /// <summary>A colony stall's goods sold to a trade ship come off its sale line (Integrations/RimMarketStall.cs).</summary>
    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToTrader))]
    public static class Patch_StallGoodsSoldToShip
    {
        public static bool Prepare() => ModsConfig.IsActive(RimMarketStall.PackageId);

        public static void Prefix(Thing toGive, int countToGive)
        {
            if (toGive == null || countToGive <= 0 || !RimMarketStall.ShipsOn) return;
            try
            {
                RimMarketStall.SoldToShip(toGive, countToGive);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stall goods sold to a ship", e);
            }
        }
    }
}
