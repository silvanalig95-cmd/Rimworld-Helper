using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's fortune in what it sells (docs/design/stage1-region.md, item 2; decision 3): when the game restocks a
    /// town kept in detail, a struggling one has a quarter less food, medicine and silver, a thriving one a little more
    /// silver and a flourishing one more again (RegionMath.StockFactor), so the trade window matches its words. It only
    /// trims or adds by count, after the game and every other mod made the stock (a postfix that runs last), and never
    /// touches anything else; with the towns' fortunes, this setting or the deeper game hooks off it does nothing.
    /// </summary>
    public static class RegionStock
    {
        public static bool On => RimfolkPatches.On && RegionBook.FortunesOn && (ColonyCouncilMod.Settings?.regionStock ?? true);

        /// <summary>What a good is, for the fortune's say: food, medicine, silver, or anything else (left alone).</summary>
        public static RegionMath.StockKind KindOf(Thing t)
        {
            var d = t?.def;
            if (d == null || t is Pawn || t is MinifiedThing) return RegionMath.StockKind.Other;
            if (d == ThingDefOf.Silver) return RegionMath.StockKind.Silver;
            if (d.IsMedicine) return RegionMath.StockKind.Medicine;
            if (d.IsNutritionGivingIngestible && !d.IsCorpse && !d.IsDrug) return RegionMath.StockKind.Food;
            return RegionMath.StockKind.Other;
        }

        /// <summary>The restocked goods of a town in detail, by its fortune: each good's stacks together brought to their share.</summary>
        public static void Apply(Settlement town, ThingOwner stock)
        {
            if (town == null || stock == null || stock.Count == 0) return;
            var band = RegionFortunes.Of(town.ID);
            if (band == null || !RegionTowns.IsInDetail(town.ID)) return;
            var groups = new Dictionary<ThingDef, List<Thing>>();
            for (int i = 0; i < stock.Count; i++)
            {
                var t = stock[i];
                var kind = KindOf(t);
                if (RegionMath.StockFactor(band.Value, kind) == 1f) continue;
                if (!groups.TryGetValue(t.def, out var list)) groups[t.def] = list = new List<Thing>();
                list.Add(t);
            }
            foreach (var kv in groups)
            {
                var kind = KindOf(kv.Value[0]);
                int total = kv.Value.Sum(t => t.stackCount);
                int target = RegionMath.StockTarget(total, RegionMath.StockFactor(band.Value, kind));
                if (target < total) Trim(kv.Value, total - target);
                else if (target > total && kind == RegionMath.StockKind.Silver)
                {
                    var more = ThingMaker.MakeThing(ThingDefOf.Silver);
                    more.stackCount = target - total;
                    if (!stock.TryAdd(more)) more.Destroy();
                }
            }
        }

        /// <summary>Takes <paramref name="count"/> away from these stacks, the smallest first, destroying what goes.</summary>
        private static void Trim(List<Thing> stacks, int count)
        {
            foreach (var t in stacks.OrderBy(t => t.stackCount).ToList())
            {
                if (count <= 0) return;
                int take = Math.Min(count, t.stackCount);
                count -= take;
                var gone = t.SplitOff(take);
                if (!gone.Destroyed) gone.Destroy();
            }
        }
    }

    /// <summary>
    /// A town's fortune in its restocked trade goods (World/Region/RegionStock.cs). A postfix that runs after every other
    /// mod's, so their own changes to the stock still apply first; it only brings food, medicine and silver to a share.
    /// </summary>
    [HarmonyPatch(typeof(Settlement_TraderTracker), "RegenerateStock")]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_RegionStock
    {
        public static void Postfix(Settlement_TraderTracker __instance, Settlement ___settlement)
        {
            if (!RegionStock.On || __instance == null) return;
            try
            {
                RegionStock.Apply(___settlement, __instance.GetDirectlyHeldThings());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a town's fortune in its stock", e);
            }
        }
    }
}
