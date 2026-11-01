using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A link in a production chain followed to the end (<see cref="Chains"/>): a thing wanted, what there is of it,
    /// how fast it is used and made, the recipe and bench here that would make it (chosen by skill and distance), its
    /// parts as links of their own, or the plant that would yield it.
    /// </summary>
    public class ChainNode
    {
        public ThingDef thing;
        public int stock, want, target, depth;
        public float useADay, makeADay, hoursABatch, perBatch;
        public RecipeDef recipe;
        public Building_WorkTable bench;
        public bool billed, canMake, cycle, tooDeep;
        /// <summary>Who works the bench for it (the one the record saw there lately, else the best able hand), their hours a day there, and the bench's own speed.</summary>
        public Pawn hand;
        public bool handSeen;
        public float handHours, benchSpeed = 1f;
        /// <summary>The use and the make come from the colony's record of the last days rather than the estimate.</summary>
        public bool useMeasured, makeMeasured;
        public List<ChainNode> parts = new List<ChainNode>();
        /// <summary>A plant that yields it, the cells to sow for the want, and the zone that grows it already, if any.</summary>
        public ThingDef plant;
        public int cells;
        public Zone_Growing zone;
        /// <summary>At the root: why it is wanted.</summary>
        public string why;
        /// <summary>The parts' picks, for the lines: what a batch takes and how much there is.</summary>
        public List<(ThingDef def, float have, float need)> partsLine = new List<(ThingDef, float, float)>();

        public bool NoMaker => recipe == null;
        public float DaysLeft => ChainMath.DaysLeft(stock, useADay, makeADay);

        public IEnumerable<ChainNode> All()
        {
            yield return this;
            foreach (var p in parts)
                foreach (var n in p.All())
                    yield return n;
        }

        /// <summary>Leaf first: the order the chain's bills go up in, so the parts are made before what takes them.</summary>
        public IEnumerable<ChainNode> LeafFirst()
        {
            foreach (var p in parts)
                foreach (var n in p.LeafFirst())
                    yield return n;
            yield return this;
        }

        /// <summary>The chain's lead time from here: its longest path of batches, as days.</summary>
        public float LeadDays => ChainMath.LeadDays(LongestPath());

        private List<float> LongestPath()
        {
            var best = new List<float>();
            float bestSum = -1f;
            foreach (var p in parts)
            {
                var path = p.LongestPath();
                float sum = path.Sum();
                if (sum > bestSum)
                {
                    bestSum = sum;
                    best = path;
                }
            }
            int batches = perBatch > 0f ? Mathf.Max(1, Mathf.CeilToInt(want / perBatch)) : 1;
            best.Insert(0, recipe == null ? 0f : Mathf.Min(hoursABatch * batches, 24f * 30f));
            return best;
        }

        public string PartsLine => string.Join(", ", partsLine.Select(p => p.def.label + " " + ((int)p.have) + "/" + ((int)p.need)));
    }

    /// <summary>
    /// Production chains followed to the end: from a thing wanted (the blueprints short of it, a bill waiting on it) down
    /// through the recipes of the benches the colony has, each part a link of its own, four links at most and never
    /// round in a circle; with each link's rate of use and of make, how long its stock lasts, and the bench and recipe
    /// that would make it best. Use is what the colony actually ate, wore out and used up over the last few days
    /// (<see cref="ColonyUse"/>), or until that record covers a day an estimate (meals eaten, medicine spent, what the
    /// active bills take). Make is worked out bench by bench: what a bench made lately by its own record, else its
    /// active bills at its hand's speed times the bench's own, a hand who keeps several benches busy sharing the day
    /// among them; and what the growing zones yield. The bench that makes a link is the one a bill for it already
    /// stands on, else the best by its hand's skill, its speed, the distance from the materials' store and the product
    /// for the hour. A link no bench makes names the plant that yields it (<see cref="Sowing"/>). Rules in
    /// <see cref="ChainMath"/>.
    /// </summary>
    public static class Chains
    {
        public class Context
        {
            public Map map;
            public Func<ThingDef, int> have;
            public Dictionary<ThingDef, List<Building_WorkTable>> benches;
            public HashSet<RecipeDef> billed;
            /// <summary>Every recipe at a bench the colony has, by product.</summary>
            public Dictionary<ThingDef, List<(RecipeDef recipe, ThingDef benchDef)>> makers;
            public Dictionary<ThingDef, float> use = new Dictionary<ThingDef, float>(), make = new Dictionary<ThingDef, float>();
            public int colonists;
            public List<Pawn> workers = new List<Pawn>();
            /// <summary>The colony's record of the last days (what was used, what each bench made, who worked it); null without one.</summary>
            public ColonyUse.Read record;
            /// <summary>The use comes from the record rather than the estimate.</summary>
            public bool useMeasured;
            /// <summary>Products whose make came, at one bench or more, from that bench's own record.</summary>
            public HashSet<ThingDef> makeMeasured = new HashSet<ThingDef>();
            /// <summary>The benches an active bill of each recipe stands on: the ones that actually do it.</summary>
            public Dictionary<RecipeDef, List<Building_WorkTable>> doneAt = new Dictionary<RecipeDef, List<Building_WorkTable>>();
            /// <summary>The benches with active bills each hand works, for sharing their day.</summary>
            public Dictionary<Pawn, HashSet<Building_WorkTable>> busy = new Dictionary<Pawn, HashSet<Building_WorkTable>>();
            private readonly Dictionary<(Building_WorkTable, RecipeDef), (Pawn hand, bool seen)> hands = new Dictionary<(Building_WorkTable, RecipeDef), (Pawn, bool)>();
            private readonly Dictionary<ThingDef, (RecipeDef recipe, Building_WorkTable bench)> best = new Dictionary<ThingDef, (RecipeDef, Building_WorkTable)>();

            /// <summary>Who works the bench for the recipe, and whether the record saw them there (worked out once).</summary>
            public Pawn Hand(Building_WorkTable bench, RecipeDef recipe, out bool seen)
            {
                if (!hands.TryGetValue((bench, recipe), out var h)) hands[(bench, recipe)] = h = Chains.FindHand(this, bench, recipe);
                seen = h.seen;
                return h.hand;
            }

            public float Use(ThingDef d) => d != null && use.TryGetValue(d, out float v) ? v : 0f;
            public float Make(ThingDef d) => d != null && make.TryGetValue(d, out float v) ? v : 0f;

            public (RecipeDef recipe, Building_WorkTable bench) BestMaker(ThingDef product)
            {
                if (product == null) return (null, null);
                if (best.TryGetValue(product, out var b)) return b;
                b = Chains.BestMaker(this, product);
                best[product] = b;
                return b;
            }
        }

        public static Context Prepare(Map map, Func<ThingDef, int> have, Dictionary<ThingDef, List<Building_WorkTable>> benches, HashSet<RecipeDef> billed,
            Dictionary<ThingDef, List<(RecipeDef recipe, ThingDef benchDef)>> makers)
        {
            var ctx = new Context { map = map, have = have, benches = benches, billed = billed, makers = makers };
            ctx.workers = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && !p.Downed && p.workSettings != null && !p.DevelopmentalStage.Juvenile()).ToList();
            ctx.colonists = map.mapPawns.FreeColonistsSpawnedCount;
            try
            {
                ctx.record = ColonyUse.ReadFor(map);
                ctx.useMeasured = ctx.record != null && ctx.record.measured;
                Rates(ctx);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("chains: rates", e);
            }
            return ctx;
        }

        // ---------------- Rates ----------------

        private static void Bump(Dictionary<ThingDef, float> d, ThingDef key, float v)
        {
            if (key == null || v <= 0f) return;
            d.TryGetValue(key, out float had);
            d[key] = had + v;
        }

        /// <summary>
        /// Everyone's use and make in a day. Use: what the record says the colony used, or until it covers a day the
        /// estimate (meals eaten, medicine spent, what the active bills take). Make, bench by bench: what the bench made
        /// lately by its own record, else its active bills at its hand's speed there; and what the growing zones yield.
        /// </summary>
        private static void Rates(Context ctx)
        {
            var map = ctx.map;
            // The active bills, bench by bench, and the benches each hand keeps busy.
            var activeAt = new List<(Building_WorkTable bench, List<Bill_Production> bills)>();
            foreach (var kv in ctx.benches)
                foreach (var wt in kv.Value)
                {
                    if (wt.BillStack == null) continue;
                    var active = wt.BillStack.Bills.OfType<Bill_Production>().Where(b => !b.suspended && b.recipe != null && Making.StillWanted(b, ctx.have)).ToList();
                    if (active.Count == 0) continue;
                    activeAt.Add((wt, active));
                    foreach (var bill in active)
                    {
                        if (!ctx.doneAt.TryGetValue(bill.recipe, out var at)) ctx.doneAt[bill.recipe] = at = new List<Building_WorkTable>();
                        if (!at.Contains(wt)) at.Add(wt);
                    }
                    var hand = ctx.Hand(wt, active[0].recipe, out _);
                    if (hand == null) continue;
                    if (!ctx.busy.TryGetValue(hand, out var benches)) ctx.busy[hand] = benches = new HashSet<Building_WorkTable>();
                    benches.Add(wt);
                }
            if (ctx.useMeasured)
                foreach (var kv in ctx.record.used) Bump(ctx.use, kv.Key, kv.Value);
            else
            {
                // Meals: everyone eats, from the kind of meal there is most of (else a simple meal's worth).
                var meals = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => Making.KindOf(d) == "meals" && ctx.have(d) > 0).OrderByDescending(ctx.have).FirstOrDefault()
                            ?? ThingDefOf.MealSimple;
                if (meals != null) Bump(ctx.use, meals, ChainMath.MealsADay(ctx.colonists, Mathf.Max(0.3f, meals.ingestible?.CachedNutrition ?? 0.9f)));
                // Medicine: one a day for each who needs tending, and a tenth a head for the small hurts; from the kind there is most of.
                int tending = map.mapPawns.FreeColonistsSpawned.Count(p => p.health?.hediffSet != null && p.health.hediffSet.HasTendableHediff());
                var medicine = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMedicine && ctx.have(d) > 0).OrderByDescending(ctx.have).FirstOrDefault();
                if (medicine != null) Bump(ctx.use, medicine, tending + 0.1f * ctx.colonists);
            }
            // The active bills: what each gives in a day (and, without the record, takes), a bench's hours shared by its bills.
            foreach (var (wt, active) in activeAt)
            {
                Dictionary<ThingDef, float> made = null;
                if (ctx.useMeasured) ctx.record.made.TryGetValue(wt.thingIDNumber, out made);
                var counted = new HashSet<ThingDef>();
                foreach (var bill in active)
                {
                    var recipe = bill.recipe;
                    // The bench's own record of this bill's products, when it has one.
                    if (made != null && recipe.products != null && recipe.products.Any(p => p?.thingDef != null && made.ContainsKey(p.thingDef)))
                    {
                        foreach (var p in recipe.products)
                        {
                            if (p?.thingDef == null || !counted.Add(p.thingDef) || !made.TryGetValue(p.thingDef, out float v)) continue;
                            Bump(ctx.make, p.thingDef, v);
                            ctx.makeMeasured.Add(p.thingDef);
                        }
                        continue;
                    }
                    float batches = ChainMath.BatchesADay(Work(recipe), Speed(ctx, recipe, wt), Hours(ctx, wt, recipe)) / active.Count;
                    if (batches <= 0f) continue;
                    if (recipe.products != null)
                        foreach (var p in recipe.products)
                            Bump(ctx.make, p?.thingDef, batches * (p?.count ?? 0));
                    if (ctx.useMeasured || recipe.ingredients == null) continue;
                    foreach (var ing in recipe.ingredients)
                    {
                        var pick = Pick(ctx, recipe, ing, bill.ingredientFilter, out float units);
                        if (pick != null) Bump(ctx.use, pick, batches * units);
                    }
                }
            }
            // The growing zones: each cell's plant yields its harvest over its growing days.
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                var plant = zone.GetPlantDefToGrow();
                var pp = plant?.plant;
                if (pp?.harvestedThingDef == null) continue;
                Bump(ctx.make, pp.harvestedThingDef, zone.Cells.Count * ChainMath.YieldADay(pp.harvestYield, pp.growDays));
            }
        }

        /// <summary>The kind an ingredient would take: the best-stocked allowed kind, else the first allowed that a bench here makes or a plant yields, else the first allowed.</summary>
        private static ThingDef Pick(Context ctx, RecipeDef recipe, IngredientCount ing, ThingFilter billFilter, out float units)
        {
            units = 0f;
            if (ing?.filter == null) return null;
            ThingDef best = null, makeable = null, first = null;
            int bestStock = 0;
            foreach (var d in ing.filter.AllowedThingDefs)
            {
                if (d == null || !recipe.IsIngredient(d) || (billFilter != null && !billFilter.Allows(d))) continue;
                first ??= d;
                int s = ctx.have(d);
                if (s > bestStock)
                {
                    bestStock = s;
                    best = d;
                }
                if (makeable == null && (ctx.makers.ContainsKey(d) || Sowing.PlantFor(d) != null)) makeable = d;
            }
            var pick = best ?? makeable ?? first;
            if (pick != null) units = ing.CountRequiredOfFor(pick, recipe);
            return pick;
        }

        private static float Work(RecipeDef recipe)
        {
            try
            {
                return recipe.WorkAmountTotal(null);
            }
            catch
            {
                return recipe.workAmount;
            }
        }

        /// <summary>Whether this one could work the recipe at all: the skill not disabled, the work type open to them.</summary>
        private static bool Able(Pawn p, RecipeDef recipe)
        {
            if (recipe.workSkill != null && p.skills?.GetSkill(recipe.workSkill)?.TotallyDisabled == true) return false;
            var wt = recipe.requiredGiverWorkType;
            if (wt != null && (p.WorkTypeIsDisabled(wt) || !p.workSettings.WorkIsActive(wt))) return false;
            return true;
        }

        /// <summary>A hand's own work speed for the recipe, as the game reckons it at a bench (its speed stat; plain without one).</summary>
        private static float HandSpeed(Pawn p, RecipeDef recipe)
        {
            if (p == null) return 0f;
            if (recipe.workSpeedStat == null) return 1f;
            try
            {
                return p.GetStatValue(recipe.workSpeedStat);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>A hand's skill for the recipe (five when the recipe takes no skill).</summary>
        private static int SkillOf(Pawn p, RecipeDef recipe) => recipe.workSkill != null ? p.skills?.GetSkill(recipe.workSkill)?.Level ?? 0 : 5;

        /// <summary>The bench's own speed for the recipe (a hand tailoring bench works at half); 0 when it can't be worked now.</summary>
        public static float BenchFactor(Building_WorkTable bench, RecipeDef recipe)
        {
            if (bench == null) return 1f;
            try
            {
                if (!bench.UsableForBillsAfterFueling()) return 0f;
                return recipe?.workTableSpeedStat == null ? 1f : bench.GetStatValue(recipe.workTableSpeedStat);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Who works the bench for the recipe: the one who did the most batches there lately by the record, when they are
        /// still here and able; else the best able hand not busy at another bench yet, by skill, then speed; else the
        /// best busy one. Null when nobody can.
        /// </summary>
        internal static (Pawn hand, bool seen) FindHand(Context ctx, Building_WorkTable bench, RecipeDef recipe)
        {
            if (bench != null && ctx.record != null && ctx.record.doers.TryGetValue(bench.thingIDNumber, out var by))
            {
                Pawn seen = null;
                float most = 0f;
                foreach (var p in ctx.workers)
                    if (by.TryGetValue(p.thingIDNumber, out float n) && n > most && Able(p, recipe))
                    {
                        most = n;
                        seen = p;
                    }
                if (seen != null) return (seen, true);
            }
            Pawn pick = null;
            bool pickIdle = false;
            int bestSkill = -1;
            float bestSpeed = -1f;
            foreach (var p in ctx.workers)
            {
                if (!Able(p, recipe)) continue;
                bool idle = !ctx.busy.TryGetValue(p, out var at) || at.Count == 0 || at.Count == 1 && at.Contains(bench);
                int level = SkillOf(p, recipe);
                float speed = HandSpeed(p, recipe);
                if (pick != null && (pickIdle && !idle || pickIdle == idle && (level < bestSkill || level == bestSkill && speed <= bestSpeed))) continue;
                pick = p;
                pickIdle = idle;
                bestSkill = level;
                bestSpeed = speed;
            }
            return (pick, false);
        }

        /// <summary>The work speed at the bench: its hand's for the recipe times the bench's own; 0 when nobody can work it or it can't be worked.</summary>
        private static float Speed(Context ctx, RecipeDef recipe, Building_WorkTable bench)
        {
            var hand = ctx.Hand(bench, recipe, out _);
            return hand == null ? 0f : ChainMath.BenchSpeed(HandSpeed(hand, recipe), BenchFactor(bench, recipe));
        }

        /// <summary>The hours a day the bench's hand gives it: their day shared among the benches with bills they work (this one counted).</summary>
        private static float Hours(Context ctx, Building_WorkTable bench, RecipeDef recipe)
        {
            var hand = ctx.Hand(bench, recipe, out _);
            if (hand == null) return 0f;
            int n = ctx.busy.TryGetValue(hand, out var benches) ? benches.Count + (benches.Contains(bench) ? 0 : 1) : 1;
            return ChainMath.HandHours(n);
        }

        // ---------------- Choosing ----------------

        /// <summary>
        /// The recipe and bench that would make the product best: the bench an active bill for it already stands on (the
        /// one that does it), else among every recipe here that gives it and every bench of that kind, the bench's hand's
        /// skill, its own speed, less the distance from the bench to the nearest store of a batch's parts, plus a little
        /// for the product an hour of work gives (ChainMath.BenchScore, Efficiency).
        /// </summary>
        private static (RecipeDef recipe, Building_WorkTable bench) BestMaker(Context ctx, ThingDef product)
        {
            if (!ctx.makers.TryGetValue(product, out var list) || list.Count == 0) return (null, null);
            RecipeDef bestRecipe = null;
            Building_WorkTable bestBench = null;
            float bestScore = float.MinValue;
            foreach (var (recipe, benchDef) in list)
            {
                if (!ctx.benches.TryGetValue(benchDef, out var benches)) continue;
                float eff = ChainMath.Efficiency(PerBatch(recipe, product), Work(recipe));
                ctx.doneAt.TryGetValue(recipe, out var doing);
                foreach (var bench in benches)
                {
                    try
                    {
                        if (!recipe.AvailableOnNow(bench)) continue;
                    }
                    catch
                    {
                        // A modded recipe that can't say: count it as available.
                    }
                    var hand = ctx.Hand(bench, recipe, out _);
                    int skill = hand == null ? -1 : SkillOf(hand, recipe);
                    float score = ChainMath.BenchScore(Mathf.Max(0, skill), StoreDistance(ctx, bench, recipe), BenchFactor(bench, recipe)) + eff * 0.2f
                                  - (skill < 0 ? 100f : 0f) + (doing != null && doing.Contains(bench) ? ChainMath.DoingBonus : 0f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRecipe = recipe;
                        bestBench = bench;
                    }
                }
            }
            return (bestRecipe, bestBench);
        }

        /// <summary>The walk from the bench to the nearest stored stack of what a batch takes (0 when nothing is stored).</summary>
        private static float StoreDistance(Context ctx, Building_WorkTable bench, RecipeDef recipe)
        {
            if (recipe.ingredients == null) return 0f;
            float total = 0f;
            int counted = 0;
            foreach (var ing in recipe.ingredients)
            {
                var pick = Pick(ctx, recipe, ing, null, out _);
                if (pick == null) continue;
                float best = float.MaxValue;
                foreach (var t in ctx.map.listerThings.ThingsOfDef(pick))
                {
                    if (!t.Spawned || !t.IsInAnyStorage()) continue;
                    float d = t.Position.DistanceTo(bench.InteractionCell);
                    if (d < best) best = d;
                }
                if (best == float.MaxValue) continue;
                total += best;
                counted++;
            }
            return counted == 0 ? 0f : total / counted;
        }

        public static float PerBatch(RecipeDef recipe, ThingDef product)
        {
            if (recipe?.products == null) return 1f;
            foreach (var p in recipe.products)
                if (p?.thingDef == product) return Mathf.Max(1, p.count);
            return 1f;
        }

        // ---------------- Building the tree ----------------

        public static ChainNode Build(Context ctx, ThingDef thing, int want, string why) => Build(ctx, thing, want, 0, new HashSet<ThingDef>(), why);

        private static ChainNode Build(Context ctx, ThingDef thing, int want, int depth, HashSet<ThingDef> path, string why)
        {
            var node = new ChainNode
            {
                thing = thing, stock = ctx.have(thing), want = want, depth = depth, why = why, useADay = ctx.Use(thing), makeADay = ctx.Make(thing),
                useMeasured = ctx.useMeasured, makeMeasured = ctx.makeMeasured.Contains(thing)
            };
            if (path.Contains(thing))
            {
                node.cycle = true;
                return node;
            }
            if (ChainMath.TooDeep(depth))
            {
                node.tooDeep = true;
                return node;
            }
            Sowing.Fill(node, ctx.map);
            var (recipe, bench) = ctx.BestMaker(thing);
            if (recipe == null) return node;
            node.recipe = recipe;
            node.bench = bench;
            node.billed = ctx.billed.Contains(recipe);
            node.perBatch = PerBatch(recipe, thing);
            node.hand = ctx.Hand(bench, recipe, out node.handSeen);
            node.benchSpeed = BenchFactor(bench, recipe);
            node.handHours = Hours(ctx, bench, recipe);
            node.hoursABatch = ChainMath.HoursABatch(Work(recipe), Speed(ctx, recipe, bench));
            node.target = ChainMath.Target(node.stock, want, node.useADay, MakingMath.TargetFor(Making.KindOf(thing), ctx.colonists));
            node.canMake = true;
            path.Add(thing);
            if (recipe.ingredients != null)
                foreach (var ing in recipe.ingredients)
                {
                    var pick = Pick(ctx, recipe, ing, null, out float units);
                    if (pick == null) continue;
                    int stock = ctx.have(pick);
                    node.partsLine.Add((pick, stock, units));
                    if (stock < units) node.canMake = false;
                    int need = ChainMath.Need(want, node.perBatch, units, stock);
                    if (need <= 0) continue;
                    node.parts.Add(Build(ctx, pick, need, depth + 1, path, null));
                }
            path.Remove(thing);
            return node;
        }
    }

    /// <summary>
    /// The growing side of a chain: a thing a plant yields (cloth from cotton, herbal medicine from healroot, wood from a
    /// tree) can be sown for. The plant the colony can sow with the best yield in a day; the zone that grows it already;
    /// and sowing: so many cells added beside that zone, or a new zone laid out near the fields on fertile, open,
    /// reachable ground the plant can grow on.
    /// </summary>
    public static class Sowing
    {
        private static readonly Dictionary<ThingDef, ThingDef> plantFor = new Dictionary<ThingDef, ThingDef>();
        private static int plantForTick = -1;

        /// <summary>The plant the colony can sow that yields this thing, with the best yield in a day; null for none.</summary>
        public static ThingDef PlantFor(ThingDef material)
        {
            if (material == null) return null;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - plantForTick > GenDate.TicksPerDay || plantForTick < 0)
            {
                plantFor.Clear();
                plantForTick = now;
            }
            if (plantFor.TryGetValue(material, out var known)) return known;
            ThingDef best = null;
            float bestYield = 0f;
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var pp = d.plant;
                if (pp == null || pp.harvestedThingDef != material || !pp.Sowable || pp.harvestYield <= 0f) continue;
                if (pp.sowResearchPrerequisites != null && pp.sowResearchPrerequisites.Any(r => r != null && !r.IsFinished)) continue;
                float y = ChainMath.YieldADay(pp.harvestYield, pp.growDays);
                if (y > bestYield)
                {
                    bestYield = y;
                    best = d;
                }
            }
            plantFor[material] = best;
            return best;
        }

        public static Zone_Growing ZoneFor(Map map, ThingDef plant) =>
            plant == null || map == null ? null : map.zoneManager.AllZones.OfType<Zone_Growing>().FirstOrDefault(z => z.GetPlantDefToGrow() == plant);

        /// <summary>Gives a link its plant, the cells its want would take, and the zone growing it already.</summary>
        public static void Fill(ChainNode node, Map map)
        {
            var plant = PlantFor(node.thing);
            if (plant?.plant == null) return;
            node.plant = plant;
            node.cells = ChainMath.CellsToSow(node.want, plant.plant.harvestYield);
            node.zone = ZoneFor(map, plant);
        }

        /// <summary>Sows for it: so many cells added beside the zone that grows it, or a new zone near the fields. The zone, or null when no ground was found.</summary>
        public static Zone_Growing Sow(Map map, ThingDef plant, int cells)
        {
            if (map == null || plant == null || cells <= 0) return null;
            var zone = ZoneFor(map, plant);
            var picked = Blob(map, plant, zone, cells);
            if (picked.Count == 0) return null;
            bool made = false;
            if (zone == null)
            {
                zone = new Zone_Growing(map.zoneManager);
                map.zoneManager.RegisterZone(zone);
                made = true;
            }
            foreach (var c in picked) zone.AddCell(c);
            if (made) zone.SetPlantDefToGrow(plant);
            return zone;
        }

        /// <summary>So many cells the plant can grow on next to the zone, nearest first, without changing anything (the planning helper's look ahead).</summary>
        public static List<IntVec3> Around(Map map, ThingDef plant, Zone_Growing zone, int count) =>
            map == null || plant == null || zone == null || count <= 0 ? new List<IntVec3>() : Blob(map, plant, zone, count);

        /// <summary>So many cells the plant can grow on near the fields, for a new zone, without changing anything.</summary>
        public static List<IntVec3> NewGround(Map map, ThingDef plant, int count) =>
            map == null || plant == null || count <= 0 ? new List<IntVec3>() : Blob(map, plant, null, count);

        /// <summary>A new growing zone of these cells, sowing the plant; null when none of the cells is free.</summary>
        public static Zone_Growing MakeZone(Map map, ThingDef plant, IEnumerable<IntVec3> cells)
        {
            if (map == null || plant == null || cells == null) return null;
            var free = cells.Where(c => c.InBounds(map) && map.zoneManager.ZoneAt(c) == null).ToList();
            if (free.Count == 0) return null;
            var zone = new Zone_Growing(map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            foreach (var c in free) zone.AddCell(c);
            zone.SetPlantDefToGrow(plant);
            return zone;
        }

        /// <summary>Where new fields go: beside the biggest growing zone, else the colony's middle.</summary>
        private static IntVec3 NearFields(Map map)
        {
            var biggest = map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.Cells.Count).FirstOrDefault();
            return biggest != null && biggest.Cells.Count > 0 ? biggest.Cells[0] : TemplatePlanner.ColonyCenter(map);
        }

        private static bool Ok(Map map, ThingDef plant, IntVec3 c)
        {
            if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || map.zoneManager.ZoneAt(c) != null) return false;
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.fertility < 0.5f) return false;
            foreach (var t in c.GetThingList(map))
                if (t.def.category == ThingCategory.Building || t is Blueprint || t is Frame) return false;
            try
            {
                if (!plant.CanEverPlantAt(c, map)) return false;
            }
            catch
            {
                return false;
            }
            return map.reachability.CanReachColony(c);
        }

        /// <summary>So many cells the plant can grow on, growing out from the zone (or the seed) over the nearest good ground.</summary>
        private static List<IntVec3> Blob(Map map, ThingDef plant, Zone_Growing zone, int count)
        {
            var picked = new List<IntVec3>();
            var seen = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>();
            IntVec3 seed;
            if (zone != null && zone.Cells.Count > 0)
            {
                seed = zone.Cells[0];
                foreach (var c in zone.Cells)
                {
                    seen.Add(c);
                    queue.Enqueue(c);
                }
            }
            else
            {
                seed = NearFields(map);
                if (!seed.IsValid) return picked;
                seen.Add(seed);
                queue.Enqueue(seed);
                if (Ok(map, plant, seed)) picked.Add(seed);
            }
            int looked = 0;
            while (queue.Count > 0 && picked.Count < count && looked++ < 4000)
            {
                var c = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var n = c + GenAdj.CardinalDirections[i];
                    if (!seen.Add(n) || !n.InBounds(map) || n.DistanceTo(seed) > 45f) continue;
                    if (Ok(map, plant, n))
                    {
                        picked.Add(n);
                        if (picked.Count >= count) break;
                    }
                    // Walk on over any open ground, so a path or a rock in the way doesn't end the field.
                    if (n.Walkable(map) && n.GetEdifice(map) == null) queue.Enqueue(n);
                }
            }
            return picked;
        }
    }
}
