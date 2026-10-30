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
        /// <summary>Why it is wanted beyond the usual: the blueprints are short of it, a bill waits on it (null: a plain suggestion).</summary>
        public string wantedBy;

        public string PartsLine => string.Join(", ", parts.Select(p => p.def.label + " " + ((int)p.have) + "/" + ((int)p.need)));
    }

    /// <summary>
    /// A link in a chain: a material the building (blueprints and frames) or a bill is waiting on, that a bench here
    /// makes, with the bill that would make it ("make until" the stock plus the want and a margin).
    /// </summary>
    public class ChainNeed
    {
        public ThingDef product;
        /// <summary>How much the blueprints are short of it (0: a bill waits on it).</summary>
        public int shortBy;
        public int stock;
        public RecipeDef recipe;
        public Building_WorkTable bench;
        public ThingDef benchDef;
        /// <summary>Its own parts are in stock for a batch.</summary>
        public bool canMake;
        /// <summary>A bill for it is set already.</summary>
        public bool billed;
        public int target;
        public string why;
        public List<(ThingDef def, float have, float need)> parts = new List<(ThingDef, float, float)>();

        public string PartsLine => string.Join(", ", parts.Select(p => p.def.label + " " + ((int)p.have) + "/" + ((int)p.need)));
    }

    /// <summary>A bill that can't go on: an ingredient none of whose kinds is in stock, and the link that would feed it, if a bench here can.</summary>
    public class StarvedBill
    {
        public Building_WorkTable bench;
        public Bill_Production bill;
        /// <summary>What it waits on, as the filter names it.</summary>
        public string missing;
        public List<ThingDef> wants = new List<ThingDef>();
        public ChainNeed fix;
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
    /// bench and recipe counts by itself: what could be made now from what's in stock with no bill for it (what the
    /// building and the bills wait on first), the chains (a material the blueprints are short of or a bill waits on,
    /// and the bench here that makes it, with a bill sized to the want), the bills starved of an ingredient, which
    /// harvests and finds no bench here puts to its own use and what would, and which benches stand without bills.
    /// Rules in <see cref="MakingMath"/>. Worked out once an hour, or when asked; the automations (Automations.cs) act on it.
    /// </summary>
    public static class Making
    {
        public class Result
        {
            public int tick = -999999;
            public List<MakingOption> could = new List<MakingOption>();
            public List<UnusedStock> unused = new List<UnusedStock>();
            public List<IdleBench> idle = new List<IdleBench>();
            /// <summary>The chains: what the building and the bills wait on that a bench here makes.</summary>
            public List<ChainNeed> needs = new List<ChainNeed>();
            /// <summary>Bills waiting on an ingredient the colony has none of.</summary>
            public List<StarvedBill> starved = new List<StarvedBill>();
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
            r.needs.Clear();
            r.starved.Clear();
            var stock = Stock(map);
            int Have(ThingDef d) => d != null && stock.TryGetValue(d, out int n) ? n : 0;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;

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

            // The chains' far end: what the blueprints and frames still need beyond the stock, and what the bills wait on.
            var shortWants = BuildingNeeds(map, Have);
            var wantedByBill = new Dictionary<ThingDef, string>();
            Starved(benches, Have, r, wantedByBill);

            // Every recipe here by its product, for the chains' near end.
            var makers = new Dictionary<ThingDef, (RecipeDef recipe, Building_WorkTable bench)>();

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
                    if (!makers.ContainsKey(product)) makers[product] = (recipe, first);
                    if (billed.Contains(recipe)) continue;
                    int productStock = Have(product);
                    bool wantedNow = shortWants.ContainsKey(product) || wantedByBill.ContainsKey(product);
                    if (!wantedNow && !MakingMath.WorthShowing(productStock)) continue;
                    var option = new MakingOption { benchDef = def, bench = first, recipe = recipe, product = product, productStock = productStock, kind = KindOf(product) };
                    if (!Parts(recipe, Have, option.parts)) continue;
                    option.target = MakingMath.TargetFor(option.kind, colonists);
                    if (shortWants.TryGetValue(product, out int shortBy))
                    {
                        option.target = MakingMath.DemandTarget(productStock, shortBy, option.target);
                        option.wantedBy = "CC_Making_WantedBuild".Translate(shortBy).Resolve();
                    }
                    else if (wantedByBill.TryGetValue(product, out var benchLabel)) option.wantedBy = "CC_Making_WantedBill".Translate(benchLabel).Resolve();
                    options.Add(option);
                }
                // The most useful few: what the building and the bills wait on, then what feeds and heals, and of clothes and weapons only the dearest.
                var ordered = options.OrderBy(o => MakingMath.WantedRank(o.kind, o.wantedBy != null)).ThenByDescending(o => o.product.BaseMarketValue).ToList();
                var kept = new List<MakingOption>();
                int wearables = 0;
                foreach (var o in ordered)
                {
                    if (MakingMath.IsWearable(o.kind) && o.wantedBy == null)
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
            r.could = r.could.OrderBy(o => MakingMath.WantedRank(o.kind, o.wantedBy != null)).ThenBy(o => o.benchDef.label).ToList();

            // The chains: each material wanted that a bench here makes, as a link with a bill of its own.
            var wants = new List<(ThingDef def, int shortBy)>();
            foreach (var kv in shortWants) wants.Add((kv.Key, kv.Value));
            foreach (var d in wantedByBill.Keys)
                if (!shortWants.ContainsKey(d)) wants.Add((d, 0));
            foreach (var (d, shortBy) in wants)
            {
                if (!makers.TryGetValue(d, out var maker)) continue;
                var link = new ChainNeed
                {
                    product = d, shortBy = shortBy, stock = Have(d), recipe = maker.recipe, bench = maker.bench, benchDef = maker.bench.def, billed = billed.Contains(maker.recipe),
                    why = shortBy > 0 ? "CC_Making_WantedBuild".Translate(shortBy).Resolve() : "CC_Making_WantedBill".Translate(wantedByBill[d]).Resolve()
                };
                link.canMake = Parts(maker.recipe, Have, link.parts);
                int batch = Mathf.Max(1, maker.recipe.products.Count > 0 ? maker.recipe.products[0].count : 1);
                link.target = MakingMath.DemandTarget(link.stock, Mathf.Max(shortBy, batch), MakingMath.TargetFor(KindOf(d), colonists));
                r.needs.Add(link);
            }
            r.needs = r.needs.OrderByDescending(c => c.shortBy).ThenBy(c => c.product.label).ToList();
            foreach (var sb in r.starved) sb.fix = r.needs.FirstOrDefault(c => sb.wants.Contains(c.product));

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

        /// <summary>
        /// The parts of one batch from the stock: for each ingredient the best-stocked kind allowed, how much there is
        /// and how much a batch needs. False when a part is missing or short.
        /// </summary>
        private static bool Parts(RecipeDef recipe, Func<ThingDef, int> have, List<(ThingDef def, float have, float need)> into)
        {
            into.Clear();
            var parts = new List<(float have, float need)>();
            if (recipe.ingredients != null)
                foreach (var ing in recipe.ingredients)
                {
                    if (ing?.filter == null) continue;
                    float need = ing.GetBaseCount();
                    ThingDef best = null;
                    float got = 0f;
                    foreach (var d in ing.filter.AllowedThingDefs)
                    {
                        if (!recipe.IsIngredient(d)) continue;
                        int n = have(d);
                        if (n <= 0) continue;
                        // The count a batch needs in units of this kind (cloth by the piece, meat by nutrition and so on).
                        float units = ing.CountRequiredOfFor(d, recipe);
                        if (best == null || n / Mathf.Max(units, 0.01f) > got / Mathf.Max(need, 0.01f))
                        {
                            best = d;
                            got = n;
                            need = units;
                        }
                    }
                    if (best == null) return false;
                    into.Add((best, got, need));
                    parts.Add((got, need));
                }
            return MakingMath.BatchPossible(parts);
        }

        /// <summary>What the colony's blueprints and frames still need beyond the stock, by material (the short amount).</summary>
        private static Dictionary<ThingDef, int> BuildingNeeds(Map map, Func<ThingDef, int> have)
        {
            var need = new Dictionary<ThingDef, int>();
            var player = Faction.OfPlayerSilentFail;
            void Add(ThingDef d, int n)
            {
                if (d == null || n <= 0) return;
                need.TryGetValue(d, out int had);
                need[d] = had + n;
            }
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (!(t is Blueprint_Build bb) || bb.Faction != player) continue;
                try
                {
                    foreach (var c in bb.TotalMaterialCost()) Add(c?.thingDef, c?.count ?? 0);
                }
                catch
                {
                    // A blueprint whose cost can't be read: not counted.
                }
            }
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (!(t is Frame f) || f.Faction != player) continue;
                try
                {
                    foreach (var c in f.TotalMaterialCost())
                        if (c?.thingDef != null) Add(c.thingDef, f.ThingCountNeeded(c.thingDef));
                }
                catch
                {
                    // A frame whose cost can't be read: not counted.
                }
            }
            var shortBy = new Dictionary<ThingDef, int>();
            foreach (var kv in need)
            {
                int s = MakingMath.Short(kv.Value, have(kv.Key));
                if (s > 0) shortBy[kv.Key] = s;
            }
            return shortBy;
        }

        /// <summary>
        /// The bills that can't go on: one whose work is still wanted (forever, so many left, or short of its count) with
        /// an ingredient none of whose allowed kinds is in stock. Each wants the kinds it would take, for the chains.
        /// </summary>
        private static void Starved(Dictionary<ThingDef, List<Building_WorkTable>> benches, Func<ThingDef, int> have, Result r, Dictionary<ThingDef, string> wantedByBill)
        {
            foreach (var kv in benches)
                foreach (var wt in kv.Value)
                {
                    if (wt.BillStack == null) continue;
                    foreach (var bill in wt.BillStack)
                    {
                        if (!(bill is Bill_Production bp) || bp.suspended || bp.recipe?.ingredients == null || !Active(bp, have)) continue;
                        var missing = new List<string>();
                        var wants = new List<ThingDef>();
                        foreach (var ing in bp.recipe.ingredients)
                        {
                            if (ing?.filter == null) continue;
                            bool any = false;
                            var allowed = new List<ThingDef>();
                            foreach (var d in ing.filter.AllowedThingDefs)
                            {
                                if (d == null || !bp.recipe.IsIngredient(d) || (bp.ingredientFilter != null && !bp.ingredientFilter.Allows(d))) continue;
                                allowed.Add(d);
                                if (have(d) >= ing.CountRequiredOfFor(d, bp.recipe))
                                {
                                    any = true;
                                    break;
                                }
                            }
                            if (any || allowed.Count == 0) continue;
                            missing.Add(ing.filter.Summary);
                            foreach (var d in allowed.Take(6))
                            {
                                wants.Add(d);
                                if (!wantedByBill.ContainsKey(d)) wantedByBill[d] = wt.LabelCap.ToString();
                            }
                        }
                        if (missing.Count == 0 || r.starved.Count >= 10) continue;
                        r.starved.Add(new StarvedBill { bench = wt, bill = bp, missing = string.Join(", ", missing), wants = wants });
                    }
                }
        }

        /// <summary>A bill whose work is still wanted: forever, so many still to make, or short of its count.</summary>
        private static bool Active(Bill_Production bp, Func<ThingDef, int> have)
        {
            if (bp.repeatMode == BillRepeatModeDefOf.Forever) return true;
            if (bp.repeatMode == BillRepeatModeDefOf.RepeatCount) return bp.repeatCount > 0;
            var product = bp.recipe.ProducedThingDef ?? (bp.recipe.products != null && bp.recipe.products.Count > 0 ? bp.recipe.products[0].thingDef : null);
            return product == null || have(product) < bp.targetCount;
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

        /// <summary>Sets a bill on the bench: make until the colony has the usual number of the product (quietly for the automations).</summary>
        public static void SetUpBill(MakingOption o, bool quiet = false)
        {
            if (o?.bench == null || o.recipe == null) return;
            SetBill(o.bench, o.recipe, o.product, o.target, quiet);
        }

        /// <summary>Sets a chain's bill: make until the colony has what the building or the bill waits for.</summary>
        public static void SetUpChain(ChainNeed c, bool quiet = false)
        {
            if (c?.bench == null || c.recipe == null) return;
            SetBill(c.bench, c.recipe, c.product, c.target, quiet);
        }

        private static void SetBill(Building_WorkTable bench, RecipeDef recipe, ThingDef product, int target, bool quiet)
        {
            if (bench.Destroyed || bench.BillStack == null) return;
            var bill = BillUtility.MakeNewBill(recipe);
            if (bill is Bill_Production prod)
            {
                prod.repeatMode = BillRepeatModeDefOf.TargetCount;
                prod.targetCount = Mathf.Max(1, target);
            }
            bench.BillStack.AddBill(bill);
            if (!quiet)
                Messages.Message("CC_Making_BillSet".Translate((product?.label ?? recipe.label).Named("PRODUCT"), target.Named("N"), bench.LabelCap.ToString().Named("BENCH")).Resolve(),
                    new LookTargets(bench), MessageTypeDefOf.TaskCompletion, false);
            if (bench.Map != null) For(bench.Map, true);
        }
    }
}
