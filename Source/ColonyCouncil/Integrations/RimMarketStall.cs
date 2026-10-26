using System;
using System.Collections.Generic;
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
    /// anything isn't there (another RimMarket version), RimMarket's own window opens as before.
    /// </summary>
    public static class RimMarketStall
    {
        public const string PackageId = "forti.rimmarket";

        private static bool looked, ready;
        private static Type dialogType;
        private static FieldInfo dialogStall, orderDef, orderToSell, orderPrice, orderSold, orderQuality, shoppersVisit;
        private static PropertyInfo colonyStall, orders, stock, settings;
        private static MethodInfo setOrder, stockDef, canSell, baseValueThing, baseValueDef, pull, interval, chanceFor, tradeable;

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

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(PackageId)) return;
                dialogType = AccessTools.TypeByName("RimMarket.UI.Dialog_ColonyStall");
                var stallType = AccessTools.TypeByName("RimMarket.Buildings.Building_MarketStall");
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
                settings = AccessTools.Property(mod, "Settings");
                shoppersVisit = settings != null ? AccessTools.Field(settings.PropertyType, "shoppersVisit") : null;
                ready = dialogStall != null && orderDef != null && orderToSell != null && orderPrice != null && orderSold != null && orderQuality != null
                        && orderQuality.FieldType == typeof(QualityRange) && colonyStall != null && orders != null && stock != null && setOrder != null
                        && stockDef != null && canSell != null && baseValueThing != null && baseValueDef != null && pull != null && interval != null
                        && chanceFor != null && tradeable != null;
                if (!ready) Log.Warning("[Rimfolk] RimMarket's sales table isn't the one Rimfolk knows, so RimMarket's own window stays.");
            }
            catch (Exception e)
            {
                ready = false;
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
}
