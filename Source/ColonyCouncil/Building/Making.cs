using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Something the colony could make now: at which bench, from what, with a bill ready to set up.</summary>
    public class MakingOption
    {
        public ThingDef benchDef;
        public Building_WorkTable bench;
        public RecipeDef recipe;
        public ThingDef product;
        public int productStock;
        public string kind;
        /// <summary>Per ingredient: what would be used (the best-stocked kind allowed), how much is there, how much a batch needs.</summary>
        public List<(ThingDef def, float have, float need)> parts = new List<(ThingDef, float, float)>();
        public int target;

        public string PartsLine => string.Join(", ", parts.Select(p => p.def.label + " " + ((int)p.have) + "/" + ((int)p.need)));
    }

    /// <summary>A harvest or find the colony has a fair amount of that no bench here puts to its own use, and what would.</summary>
    public class UnusedStock
    {
        public ThingDef def;
        public int stock;
        public ThingDef benchDef;
        public RecipeDef recipe;
        public ResearchProjectDef research;
        public bool benchBuilt;
    }

    /// <summary>A bench with nothing on its bill stack.</summary>
    public class IdleBench
    {
        public Building_WorkTable bench;
        public int options;
    }

    /// <summary>
    /// What the colony could make (the Making page), read from the recipes of the benches it has, so every mod's
    /// bench and recipe counts by itself: what could be made now from what's in stock with no bill for it, which
    /// harvests and finds no bench here puts to its own use and what would, and which benches stand without bills.
    /// Rules in <see cref="MakingMath"/>. Worked out once an hour, or when asked.
    /// </summary>
    public static class Making
    {
        public class Result
        {
            public int tick = -999999;
            public List<MakingOption> could = new List<MakingOption>();
            public List<UnusedStock> unused = new List<UnusedStock>();
            public List<IdleBench> idle = new List<IdleBench>();
        }

        private static readonly Dictionary<int, Result> cache = new Dictionary<int, Result>();

        public static Result For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new Result();
            if (!cache.TryGetValue(map.uniqueID, out var r)) cache[map.uniqueID] = r = new Result();
            int now = Find.TickManager.TicksGame;
            if (force || now - r.tick > Sched.HourTicks || now < r.tick)
            {
                r.tick = now;
                try
                {
                    Compute(map, r);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("making", e);
                }
            }
            return r;
        }

        // ---------------- Stock ----------------

        /// <summary>Everything lying about or stored, unforbidden, by kind.</summary>
        private static Dictionary<ThingDef, int> Stock(Map map)
        {
            var stock = new Dictionary<ThingDef, int>();
            var player = Faction.OfPlayerSilentFail;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (t == null || !t.Spawned || t.def.category != ThingCategory.Item || (player != null && t.IsForbidden(player))) continue;
                var inner = t.GetInnerIfMinified();
                if (inner != t) continue;
                stock.TryGetValue(t.def, out int n);
                stock[t.def] = n + t.stackCount;
            }
            return stock;
        }

        /// <summary>The kind of a product, for the order and the bill's count.</summary>
        public static string KindOf(ThingDef d)
        {
            if (d == null) return "other";
            if (d.IsMedicine) return "medicine";
            if (d.IsDrug) return "drugs";
            if (d.IsApparel) return "apparel";
            if (d.IsWeapon) return "weapons";
            if (d.IsIngestible && d.ingestible?.foodType != null && (d.ingestible.foodType & FoodTypeFlags.Meal) != 0) return "meals";
            if (d.IsIngestible && d.ingestible != null && d.ingestible.preferability <= FoodPreferability.RawTasty && d.defName.IndexOf("Kibble", StringComparison.OrdinalIgnoreCase) >= 0) return "feed";
            if (d.thingCategories != null)
            {
                foreach (var c in d.thingCategories)
                {
                    if (c == ThingCategoryDefOf.StoneBlocks) return "blocks";
                    if (c == ThingCategoryDefOf.Textiles || c == ThingCategoryDefOf.Leathers) return "textiles";
                }
            }
            if (d.defName.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0) return "components";
            if (d.defName.IndexOf("Chemfuel", StringComparison.OrdinalIgnoreCase) >= 0) return "fuel";
            return "other";
        }

        // ---------------- The review ----------------

        private static void Compute(Map map, Result r)
        {
            r.could.Clear();
            r.unused.Clear();
            r.idle.Clear();
            var stock = Stock(map);
            int Have(ThingDef d) => d != null && stock.TryGetValue(d, out int n) ? n : 0;

            // The benches the colony has, one per kind, with their bills.
            var benches = new Dictionary<ThingDef, List<Building_WorkTable>>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_WorkTable wt) || wt.def.AllRecipes == null) continue;
                if (!benches.TryGetValue(wt.def, out var list)) benches[wt.def] = list = new List<Building_WorkTable>();
                list.Add(wt);
            }
            var billed = new HashSet<RecipeDef>();
            foreach (var kv in benches)
                foreach (var wt in kv.Value)
                    if (wt.BillStack != null)
                        foreach (var bill in wt.BillStack)
                            if (bill?.recipe != null && !bill.suspended) billed.Add(bill.recipe);

            // What could be made now.
            var specificHere = new HashSet<ThingDef>();
            foreach (var kv in benches)
            {
                var def = kv.Key;
                var first = kv.Value[0];
                var options = new List<MakingOption>();
                foreach (var recipe in def.AllRecipes)
                {
                    if (recipe == null || !recipe.AvailableNow || recipe.products == null || recipe.products.Count == 0) continue;
                    try
                    {
                        if (!recipe.AvailableOnNow(first)) continue;
                    }
                    catch
                    {
                        // A modded recipe that can't say: count it as available.
                    }
                    // Specific uses this bench offers, for the unused list below.
                    if (recipe.ingredients != null)
                        foreach (var ing in recipe.ingredients)
                            if (ing?.filter != null && ing.filter.AllowedDefCount <= MakingMath.SpecificFilter)
                                foreach (var d in ing.filter.AllowedThingDefs)
                                    specificHere.Add(d);
                    var product = recipe.ProducedThingDef ?? recipe.products[0].thingDef;
                    if (product == null || !product.EverStorable(false)) continue;
                    if (billed.Contains(recipe)) continue;
                    int productStock = Have(product);
                    if (!MakingMath.WorthShowing(productStock)) continue;
                    var option = new MakingOption { benchDef = def, bench = first, recipe = recipe, product = product, productStock = productStock, kind = KindOf(product) };
                    var parts = new List<(float have, float need)>();
                    bool ok = true;
                    if (recipe.ingredients != null)
                        foreach (var ing in recipe.ingredients)
                        {
                            if (ing?.filter == null) continue;
                            float need = ing.GetBaseCount();
                            ThingDef best = null;
                            float have = 0f;
                            foreach (var d in ing.filter.AllowedThingDefs)
                            {
                                if (!recipe.IsIngredient(d)) continue;
                                int n = Have(d);
                                if (n <= 0) continue;
                                // The count a batch needs in units of this kind (cloth by the piece, meat by nutrition and so on).
                                float units = ing.CountRequiredOfFor(d, recipe);
                                if (best == null || n / Mathf.Max(units, 0.01f) > have / Mathf.Max(need, 0.01f))
                                {
                                    best = d;
                                    have = n;
                                    need = units;
                                }
                            }
                            if (best == null)
                            {
                                ok = false;
                                break;
                            }
                            option.parts.Add((best, have, need));
                            parts.Add((have, need));
                        }
                    if (!ok || !MakingMath.BatchPossible(parts)) continue;
                    option.target = MakingMath.TargetFor(option.kind);
                    options.Add(option);
                }
                // The most useful few: what feeds and heals first, and of clothes and weapons only the dearest.
                var ordered = options.OrderBy(o => MakingMath.KindRank(o.kind)).ThenByDescending(o => o.product.BaseMarketValue).ToList();
                var kept = new List<MakingOption>();
                int wearables = 0;
                foreach (var o in ordered)
                {
                    if (MakingMath.IsWearable(o.kind))
                    {
                        if (wearables >= MakingMath.PerBenchWearable) continue;
                        wearables++;
                    }
                    kept.Add(o);
                    if (kept.Count >= MakingMath.PerBench) break;
                }
                r.could.AddRange(kept);
                foreach (var wt in kv.Value)
                    if (wt.BillStack == null || wt.BillStack.Count == 0 || wt.BillStack.Bills.All(b => b.suspended))
                        r.idle.Add(new IdleBench { bench = wt, options = kept.Count });
            }
            r.could = r.could.OrderBy(o => MakingMath.KindRank(o.kind)).ThenBy(o => o.benchDef.label).ToList();

            // Harvests and finds with a specific use somewhere that no bench here offers.
            var hasFermenter = Avail.ColonyHas(map, Avail.IsFermenter);
            var candidates = new List<ThingDef>();
            foreach (var kv in stock)
            {
                var d = kv.Key;
                if (d == null || kv.Value < MakingMath.LeastUnused || !IsRawish(d) || specificHere.Contains(d)) continue;
                if (hasFermenter && d.defName.IndexOf("Wort", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                candidates.Add(d);
            }
            // The recipes anywhere that ask for one of them in particular, at a bench the colony could build: read once, not per kind.
            var usesOf = new Dictionary<ThingDef, List<(RecipeDef recipe, ThingDef bench)>>();
            if (candidates.Count > 0)
            {
                var wanted = new HashSet<ThingDef>(candidates);
                foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (recipe.ingredients == null || recipe.products == null || recipe.products.Count == 0) continue;
                    ThingDef user = null;
                    bool looked = false;
                    foreach (var ing in recipe.ingredients)
                    {
                        if (ing?.filter == null || ing.filter.AllowedDefCount > MakingMath.SpecificFilter) continue;
                        foreach (var d in ing.filter.AllowedThingDefs)
                        {
                            if (d == null || !wanted.Contains(d) || !recipe.IsIngredient(d)) continue;
                            if (!looked)
                            {
                                looked = true;
                                try
                                {
                                    user = recipe.AllRecipeUsers.FirstOrDefault(u => u != null && u.IsWorkTable && u.BuildableByPlayer);
                                }
                                catch
                                {
                                    // A recipe with odd users: not for this list.
                                }
                            }
                            if (user == null) break;
                            if (!usesOf.TryGetValue(d, out var list)) usesOf[d] = list = new List<(RecipeDef, ThingDef)>();
                            list.Add((recipe, user));
                        }
                    }
                }
            }
            foreach (var d in candidates)
            {
                stock.TryGetValue(d, out int count);
                if (!usesOf.TryGetValue(d, out var uses) || uses.Count == 0) continue;
                // One built here already (whose recipe isn't available yet) beats one still to build.
                var pick = uses.FirstOrDefault(u => benches.ContainsKey(u.bench));
                if (pick.recipe == null) pick = uses[0];
                var use = pick.recipe;
                var benchDef = pick.bench;
                var kv = new KeyValuePair<ThingDef, int>(d, count);
                if (!MakingMath.Unused(kv.Value, false)) continue;
                var research = use.researchPrerequisite != null && !use.researchPrerequisite.IsFinished ? use.researchPrerequisite
                    : use.researchPrerequisites?.FirstOrDefault(p => !p.IsFinished);
                if (research == null && benchDef.researchPrerequisites != null) research = benchDef.researchPrerequisites.FirstOrDefault(p => !p.IsFinished);
                r.unused.Add(new UnusedStock { def = d, stock = kv.Value, benchDef = benchDef, recipe = use, research = research, benchBuilt = benches.ContainsKey(benchDef) });
            }
            r.unused = r.unused.OrderByDescending(u => u.stock).Take(12).ToList();
        }

        /// <summary>Harvests, hides, wool, leaves, chunks and the like: things a bench turns into something else.</summary>
        private static bool IsRawish(ThingDef d)
        {
            if (d.thingCategories == null) return false;
            foreach (var c in d.thingCategories)
            {
                if (c == ThingCategoryDefOf.PlantFoodRaw || c == ThingCategoryDefOf.PlantMatter || c == ThingCategoryDefOf.Textiles || c == ThingCategoryDefOf.Leathers
                    || c == ThingCategoryDefOf.Wools || c == ThingCategoryDefOf.StoneChunks || c == ThingCategoryDefOf.MeatRaw) return true;
                if (c.defName == "AnimalProductRaw" || c.defName == "Chunks") return true;
            }
            return false;
        }

        // ---------------- Doing it ----------------

        /// <summary>Sets a bill on the bench: make until the colony has the usual number of the product.</summary>
        public static void SetUpBill(MakingOption o)
        {
            if (o?.bench == null || o.recipe == null || o.bench.Destroyed || o.bench.BillStack == null) return;
            var bill = BillUtility.MakeNewBill(o.recipe);
            if (bill is Bill_Production prod)
            {
                prod.repeatMode = BillRepeatModeDefOf.TargetCount;
                prod.targetCount = Mathf.Max(1, o.target);
            }
            o.bench.BillStack.AddBill(bill);
            Messages.Message("CC_Making_BillSet".Translate(o.product.label.Named("PRODUCT"), o.target.Named("N"), o.bench.LabelCap.ToString().Named("BENCH")).Resolve(),
                new LookTargets(o.bench), MessageTypeDefOf.TaskCompletion, false);
            if (o.bench.Map != null) For(o.bench.Map, true);
        }
    }
}
