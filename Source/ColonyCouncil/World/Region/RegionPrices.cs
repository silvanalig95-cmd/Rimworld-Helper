using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The region's hard and good years in the traders' prices (docs/content-update-proposals.md, B5; the rules are
    /// <see cref="RegionPricesMath"/>'s): while a world event runs at a people's town, every trader of that people (their
    /// caravans, their visitors' traders, their towns) pays more for what the town lacks and asks more for it, and in a
    /// bumper year sells food cheaper. It rides on the moot's season prices (Peoples/Events10/, Patch_SeasonPrices): one
    /// factor on the price, the moot's and the region's together, so the two never pull against each other. The price's
    /// hover in the trade window says why (Patch_SeasonPriceWhy). With world events off, nothing moves.
    /// </summary>
    public static class RegionPrices
    {
        private struct Entry
        {
            public RegionGoods goods;
            public float factor;
            public string why;
        }

        private static readonly Dictionary<Faction, List<Entry>> byPeople = new Dictionary<Faction, List<Entry>>();
        private static readonly Dictionary<ThingDef, RegionGoods> kinds = new Dictionary<ThingDef, RegionGoods>();
        private static int stamp = int.MinValue;
        private static Game game;

        public static bool On => ColonyCouncilMod.Settings?.worldSituations ?? true;

        /// <summary>Any people's prices moved now (read by the trade price hook first, so kept cheap).</summary>
        public static bool Any
        {
            get
            {
                if (Current.Game == null) return false;
                Refresh();
                return byPeople.Count > 0;
            }
        }

        /// <summary>The running world events read again when one begins or ends, and once an hour.</summary>
        private static void Refresh()
        {
            var ws = WorldSituations.Instance;
            int st = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (ws != null)
                foreach (var s in ws.active)
                    st = unchecked(st * 31 + (s?.id ?? 0) * 2 + (s != null && s.ended ? 1 : 0));
            if (st == stamp && game == Current.Game) return;
            stamp = st;
            game = Current.Game;
            byPeople.Clear();
            if (ws == null || !On) return;
            foreach (var s in ws.Running)
            {
                string def = s?.def?.defName;
                var g = RegionPricesMath.Goods(def, out bool dear);
                if (g == RegionGoods.None || s.ctx == null) continue;
                string why = Why(s);
                Add(s.ctx.faction, g, dear, why);
                if (RegionPricesMath.BothSides(def)) Add(s.ctx.enemy, g, dear, why);
            }
        }

        private static void Add(Faction f, RegionGoods g, bool dear, string why)
        {
            if (f == null || f.IsPlayer) return;
            if (!byPeople.TryGetValue(f, out var list)) byPeople[f] = list = new List<Entry>();
            list.Add(new Entry { goods = g, factor = dear ? RegionPricesMath.Dear : RegionPricesMath.Cheap, why = why });
        }

        /// <summary>"famine, Ashford": the world event and where it is, for the price's hover.</summary>
        private static string Why(WorldSituation s)
        {
            string label = s.def?.label ?? "?";
            return s.ctx.placeName.NullOrEmpty() ? label : "CC_RegionPrice_Where".Translate(label.Named("EVENT"), s.ctx.placeName.Named("PLACE")).Resolve();
        }

        /// <summary>What kind of goods a thing is, for the region's years (worked out once a def).</summary>
        public static RegionGoods GoodsOf(ThingDef d)
        {
            if (d == null) return RegionGoods.None;
            if (kinds.TryGetValue(d, out var k)) return k;
            var cats = d.IsStuff ? d.stuffProps?.categories : null;
            var ing = d.ingestible;
            bool food = ing != null && d.IsNutritionGivingIngestible && ing.HumanEdible && !d.IsDrug && !d.IsCorpse && ing.preferability >= FoodPreferability.RawBad;
            k = RegionPricesMath.Kind(d.IsMedicine, food, d == ThingDefOf.Steel, cats != null && cats.Contains(StuffCategoryDefOf.Woody),
                cats != null && (cats.Contains(StuffCategoryDefOf.Fabric) || cats.Contains(StuffCategoryDefOf.Leathery)));
            kinds[d] = k;
            return k;
        }

        /// <summary>The people of the trader the colony is dealing with now, or null.</summary>
        public static Faction Trader => TradeSession.Active ? TradeSession.trader?.Faction : null;

        /// <summary>The region's factor on this thing's price with this people (1 for none).</summary>
        public static float Factor(ThingDef d, Faction f)
        {
            if (f == null || d == null || !Any || !byPeople.TryGetValue(f, out var list)) return 1f;
            var g = GoodsOf(d);
            if (g == RegionGoods.None) return 1f;
            float factor = 1f;
            foreach (var e in list)
                if (e.goods == g)
                    factor *= e.factor;
            return RegionPricesMath.Combined(1f, factor);
        }

        /// <summary>The lines for the price's hover: each world event that moves this thing's price with this people.</summary>
        public static IEnumerable<string> Lines(ThingDef d, Faction f)
        {
            if (f == null || d == null || !Any || !byPeople.TryGetValue(f, out var list)) yield break;
            var g = GoodsOf(d);
            if (g == RegionGoods.None) yield break;
            foreach (var e in list)
                if (e.goods == g)
                    yield return (e.factor >= 1f ? "CC_RegionPrice_Dear" : "CC_RegionPrice_Cheap").Translate(("CC_RegionPrice_Goods_" + g).Translate().Named("GOODS"),
                        RegionPricesMath.Percent(e.factor).Named("PCT"), f.Name.Named("PEOPLE"), e.why.Named("WHY")).Resolve();
        }

        /// <summary>The moot's season price on this thing (Peoples/Events10/), in words for the hover, or null.</summary>
        public static string MootLine(ThingDef d)
        {
            if (d == null || !PeopleEvents10Book.AnyPrice) return null;
            float f = PeopleEvents10Book.Factor(d);
            if (f == 1f) return null;
            var p = PeopleEvents10Book.Instance?.prices?.FirstOrDefault(x => x != null && x.thing == d.defName);
            return (f < 1f ? "CC_RegionPrice_MootCheap" : "CC_RegionPrice_MootDear").Translate(d.LabelCap.Named("GOODS"), RegionPricesMath.Percent(f).Named("PCT"),
                (p?.why ?? "?").Named("PEOPLE")).Resolve();
        }

        /// <summary>Dev mode: what moves now, people by people.</summary>
        public static string Describe()
        {
            if (!Any) return "no world event moves a price now.";
            return string.Join("; ", byPeople.Select(kv => kv.Key.Name + ": " + string.Join(", ", kv.Value.Select(e => e.goods + " x" + e.factor.ToString("0.00") + " (" + e.why + ")"))));
        }
    }

    /// <summary>
    /// The price's hover in the trade window (the game's and Rimfolk's own, which shows the game's): why this price is what
    /// it is this season, the moot's ruling and the region's years at the trader's people's towns.
    /// </summary>
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceTooltip))]
    public static class Patch_SeasonPriceWhy
    {
        public static void Postfix(Tradeable __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance == null || !TradeSession.Active) return;
            try
            {
                var def = __instance.ThingDef;
                if (def == null || __instance.IsCurrency) return;
                var lines = new List<string>();
                string moot = RegionPrices.MootLine(def);
                if (moot != null) lines.Add(moot);
                lines.AddRange(RegionPrices.Lines(def, RegionPrices.Trader));
                if (lines.Count > 0) __result = (__result ?? "") + "\n\n" + string.Join("\n", lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the region's years: the price's hover", e);
            }
        }
    }
}
