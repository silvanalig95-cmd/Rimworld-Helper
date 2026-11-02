using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning ahead, phase 3, worked out with the rest of the Ahead section (<see cref="Ahead.For"/>, hourly): Colony
    /// Manager's targets shown beside the stock rows, a target raised when the colony wants more of what a job brings
    /// in (meat for the raw food, wood for the blueprints and the fires, the minerals on the shopping list), a
    /// suspended job resumed when its goods are wanted, and a manager that can't work named (no desk, a desk that
    /// can't be used, nobody managing); the shopping list, what the colony wants and can't make here, walked up the
    /// chains (neutroamine for medicine, steel for components, components before Fabrication, the blueprints' and the
    /// bills' materials) less what is in store and what is marked to mine, with what a trader here has of it; rock of
    /// a needed mineral marked to mine at the nearest known deposit; and spare clothes: a bill when what is worn below
    /// half, what will be within the days ahead and what the record saw worn through outrun the spares and the bills.
    /// Every step goes through the shared suggestion core. Rules in <see cref="AheadMoreMath"/>.
    /// </summary>
    public static class AheadMore
    {
        /// <summary>What phase 3 adds to the Ahead section's view of a map.</summary>
        public class Extra
        {
            public List<ColonyManagerBridge.Job> jobs = new List<ColonyManagerBridge.Job>();
            public ManagerState state;
            public string stall;
            public List<ShopNeed> shop = new List<ShopNeed>();
            /// <summary>A line for each trader on the map or in orbit: which of the list they have.</summary>
            public List<string> traders = new List<string>();
            public List<ClothesKind> clothes = new List<ClothesKind>();
        }

        private static string N(float v) => v >= AheadMath.Far ? "-" : v.ToString(v < 10f ? "0.#" : "0");

        /// <summary>Works phase 3 out for the view and adds its steps to the view's suggestions.</summary>
        public static void Add(Ahead.View v)
        {
            var n = v.needs;
            if (n?.map == null) return;
            var x = v.more = new Extra();
            var map = n.map;
            try
            {
                x.jobs = ColonyManagerBridge.Jobs(map);
                var shop = new Dictionary<ThingDef, ShopNeed>();
                Clothes(v, x, shop);
                Shopping(v, x, shop);
                x.shop = AheadMoreMath.Order(shop.Values);
                Traders(map, x);
                Manager(v, x, shop);
                Mining(v, x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ahead more", e);
            }
        }

        private static string Plural(ThingDef d, int count) => count == 1 ? d.label : Find.ActiveLanguageWorker.Pluralize(d.label, count);

        // ---------------- Spare clothes ----------------

        private static void Clothes(Ahead.View v, Extra x, Dictionary<ThingDef, ShopNeed> shop)
        {
            var n = v.needs;
            var map = n.map;
            float days = v.reserveDays;
            var kinds = new Dictionary<ThingDef, ClothesKind>();
            foreach (var p in ColonyNeeds.Hands(map))
            {
                var worn = p?.apparel?.WornApparel;
                if (worn == null || p.Dead) continue;
                foreach (var ap in worn)
                {
                    if (ap?.def?.apparel == null || !ap.def.apparel.careIfDamaged) continue;
                    if (!kinds.TryGetValue(ap.def, out var c)) kinds[ap.def] = c = new ClothesKind { key = ap.def.defName, label = ap.def.label };
                    c.worn++;
                    if (!ap.def.useHitPoints) continue;
                    float toFray = AheadMoreMath.DaysToFray(ap.HitPoints, ap.MaxHitPoints, ap.def.apparel.wearPerDay);
                    if (toFray <= 0f) c.frayed++;
                    else if (toFray <= days) c.soon++;
                }
            }
            if (kinds.Count == 0) return;
            foreach (var g in n.goods)
                if (g.kind == AheadMath.Clothes && n.defs.TryGetValue(g.key, out var d) && kinds.TryGetValue(d, out var c))
                    c.spare += (int)g.count;
            if (n.measured)
                foreach (var kv in kinds)
                    if (n.record.used.TryGetValue(kv.Key, out float used))
                        kv.Value.wornOutADay = used;

            // The bills that make them, and a bench and recipe that could.
            var benches = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.Spawned && b.BillStack != null).ToList();
            var maker = new Dictionary<ThingDef, (Building_WorkTable bench, RecipeDef recipe)>();
            var until = new Dictionary<ThingDef, (Building_WorkTable bench, Bill_Production bill)>();
            foreach (var bench in benches)
            {
                foreach (var bill in bench.BillStack.Bills.OfType<Bill_Production>())
                {
                    var made = bill.recipe?.ProducedThingDef;
                    if (made == null || bill.DeletedOrDereferenced || !kinds.TryGetValue(made, out var c)) continue;
                    if (!BillMods.GameMode(bill)) c.managed = true;
                    else if (bill.suspended) continue;
                    else if (bill.repeatMode == BillRepeatModeDefOf.Forever) c.forever = true;
                    else if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount) c.billCover += bill.repeatCount * (int)Chains.PerBatch(bill.recipe, made);
                    else
                    {
                        c.billCover += Mathf.Max(0, bill.targetCount - c.spare);
                        if (!until.TryGetValue(made, out var had) || had.bill.targetCount < bill.targetCount) until[made] = (bench, bill);
                    }
                }
                foreach (var recipe in bench.def.AllRecipes ?? new List<RecipeDef>())
                {
                    var made = recipe?.ProducedThingDef;
                    if (made == null || !kinds.ContainsKey(made) || maker.ContainsKey(made) || !recipe.AvailableNow) continue;
                    bool ok;
                    try
                    {
                        ok = recipe.AvailableOnNow(bench, null);
                    }
                    catch
                    {
                        ok = true;
                    }
                    if (ok && Chains.BenchFactor(bench, recipe) > 0f) maker[made] = (bench, recipe);
                }
            }
            foreach (var kv in kinds)
            {
                if (!maker.TryGetValue(kv.Key, out var m)) continue;
                kv.Value.canMake = true;
                kv.Value.partsInStock = PartsInStock(m.recipe, n.stock);
            }
            x.clothes = kinds.Values.OrderByDescending(c => c.frayed + c.soon).ThenBy(c => c.key, StringComparer.Ordinal).ToList();

            foreach (var c in AheadMoreMath.ClothesSteps(kinds.Values, days))
            {
                var def = kinds.First(k => k.Value == c).Key;
                int shortBy = AheadMoreMath.ClothesShort(c, days), target = AheadMoreMath.ClothesTarget(c, days);
                string why = ClothesReason(c, days, def);
                if (!c.canMake)
                {
                    // None can be made here: on the shopping list.
                    var need = Need(shop, def, n.stock);
                    AheadMoreMath.AddReason(need, "CC_AheadMore_ShopWhy_Clothes".Translate(c.frayed + c.soon).Resolve(), AheadMoreMath.Gross(shortBy, need.have));
                    continue;
                }
                var (bench, recipe) = maker[def];
                var s = new Suggestion
                {
                    key = "ahead:clothes:" + def.defName, source = Ahead.Source, area = AheadMath.Clothes, map = map, level = c.frayed > c.spare ? 2 : 1,
                    when = "CC_AheadMore_When_Clothes".Translate(c.frayed).Resolve(), title = "CC_AheadMore_Clothes_Title".Translate(target.Named("N"), Plural(def, target).Named("WHAT")).Resolve(),
                    look = new LookTargets(bench), cells = () => bench.OccupiedRect().Cells.ToList()
                };
                if (!c.partsInStock)
                {
                    s.reason = why + " " + "CC_AheadMore_Clothes_NoParts".Translate(bench.LabelCap.ToString().Named("BENCH"), PartsWords(recipe).Named("PARTS")).Resolve();
                    v.all.Add(s);
                    continue;
                }
                if (until.TryGetValue(def, out var at) && !at.bill.DeletedOrDereferenced)
                {
                    var bill = at.bill;
                    int from = bill.targetCount, to = Mathf.Max(from + shortBy, target);
                    s.reason = why + " " + "CC_AheadMore_Clothes_Raise".Translate(at.bench.LabelCap.ToString().Named("BENCH"), from.Named("FROM"), to.Named("TO")).Resolve();
                    s.doLabel = "CC_Ahead_Do_Raise".Translate(to).Resolve();
                    s.doTip = "CC_Ahead_Do_TargetTip".Translate(at.bench.LabelCap.ToString().Named("BENCH"), def.label.Named("PRODUCT"), from.Named("FROM"), to.Named("TO")).Resolve();
                    s.look = new LookTargets(at.bench);
                    s.cells = () => at.bench.OccupiedRect().Cells.ToList();
                    int spare = c.spare;
                    s.apply = () =>
                    {
                        if (bill.DeletedOrDereferenced || !BillMods.GameMode(bill)) return null;
                        var undo = new UndoBillTarget();
                        foreach (var b in BillMods.Linked(bill))
                        {
                            undo.bills.Add(b);
                            undo.targets.Add(b.targetCount);
                            b.targetCount = to;
                        }
                        return new SuggestDone { undo = undo, check = new BillStockCheck { bench = at.bench, bill = bill, product = def, before = spare, target = to, waitTicks = 2 * SuggestMath.TicksPerDay } };
                    };
                }
                else
                {
                    s.reason = why + " " + "CC_AheadMore_Clothes_Add".Translate(bench.LabelCap.ToString().Named("BENCH"), PartsWords(recipe).Named("PARTS")).Resolve();
                    s.doLabel = "CC_Ahead_Do_Add".Translate().Resolve();
                    s.doTip = "CC_AheadMore_Clothes_AddTip".Translate(bench.LabelCap.ToString().Named("BENCH"), Plural(def, target).Named("WHAT"), target.Named("N")).Resolve();
                    int spare = c.spare;
                    s.apply = () =>
                    {
                        if (bench.Destroyed) return null;
                        var added = Making.AddBill(bench, recipe, def, target, true);
                        if (added == null) return null;
                        // Only whole ones count toward the count, so the worn ones in store don't stand in for spares.
                        added.hpRange = new FloatRange(AheadMoreMath.FrayedShare, 1f);
                        return new SuggestDone { undo = new UndoBillsAdded { bills = { added } }, check = new BillStockCheck { bench = bench, bill = added, product = def, before = spare, target = target, waitTicks = 2 * SuggestMath.TicksPerDay } };
                    };
                }
                v.all.Add(s);
            }
        }

        private static string ClothesReason(ClothesKind c, float days, ThingDef def)
        {
            var parts = new List<string>
            {
                "CC_AheadMore_Clothes_Worn".Translate(c.worn.Named("WORN"), Plural(def, c.worn).Named("WHAT"), c.frayed.Named("FRAYED"), c.soon.Named("SOON"), N(days).Named("DAYS")).Resolve()
            };
            if (c.wornOutADay > 0f) parts.Add("CC_AheadMore_Clothes_WornOut".Translate(N(c.wornOutADay).Named("ADAY"), N(c.wornOutADay * days).Named("IN"), N(days).Named("DAYS")).Resolve());
            parts.Add("CC_AheadMore_Clothes_Have".Translate(c.spare.Named("SPARE"), c.billCover.Named("BILLS"), AheadMoreMath.ClothesShort(c, days).Named("SHORT")).Resolve());
            return string.Join(" ", parts);
        }

        private static bool PartsInStock(RecipeDef recipe, Dictionary<ThingDef, int> stock)
        {
            if (recipe?.ingredients == null) return true;
            foreach (var ing in recipe.ingredients)
            {
                if (ing?.filter == null) continue;
                bool any = false;
                foreach (var d in ing.filter.AllowedThingDefs)
                    if (d != null && recipe.IsIngredient(d) && stock.TryGetValue(d, out int have) && have >= ing.CountRequiredOfFor(d, recipe, null))
                    {
                        any = true;
                        break;
                    }
                if (!any) return false;
            }
            return true;
        }

        private static string PartsWords(RecipeDef recipe)
        {
            if (recipe?.ingredients == null || recipe.ingredients.Count == 0) return "-";
            return string.Join(", ", recipe.ingredients.Where(i => i?.filter != null).Select(i => i.GetBaseCount() + " " + i.filter.Summary));
        }

        // ---------------- The shopping list ----------------

        private static ShopNeed Need(Dictionary<ThingDef, ShopNeed> shop, ThingDef d, Dictionary<ThingDef, int> stock)
        {
            if (shop.TryGetValue(d, out var s)) return s;
            stock.TryGetValue(d, out int have);
            shop[d] = s = new ShopNeed { key = d.defName, label = d.label, have = have, price = d.BaseMarketValue };
            return s;
        }

        /// <summary>What comes from the land rather than a bench: crops, meat, hides, wool, milk and eggs, chunks, and whatever a plant on the map or one the colony can sow yields.</summary>
        private static bool FromTheLand(ThingDef d, HashSet<ThingDef> plantYields)
        {
            if (d == null) return true;
            if (plantYields.Contains(d) || Sowing.PlantFor(d) != null || IsChunk(d)) return true;
            if (d.thingCategories == null) return false;
            foreach (var c in d.thingCategories)
                if (c == ThingCategoryDefOf.PlantFoodRaw || c == ThingCategoryDefOf.MeatRaw || c == ThingCategoryDefOf.Leathers || c == ThingCategoryDefOf.Wools
                    || c == ThingCategoryDefOf.PlantMatter || c.defName == "AnimalProductRaw") return true;
            return false;
        }

        /// <summary>A chunk of stone or slag (its category, or one above it, is the chunks').</summary>
        private static bool IsChunk(ThingDef d)
        {
            if (d?.thingCategories == null) return false;
            foreach (var c in d.thingCategories)
                for (var cat = c; cat != null; cat = cat.parent)
                    if (cat == ThingCategoryDefOf.StoneChunks || cat.defName == "Chunks") return true;
            return false;
        }

        /// <summary>
        /// A link a bench here makes only from chunks there are none of (steel smelted from slag): as good as not made
        /// here, so it goes on the list itself.
        /// </summary>
        private static bool OnlyFromMissingChunks(ChainNode node) =>
            node.recipe != null && !node.canMake && node.want > 0 && node.parts.Count > 0 && node.parts.All(p => p.want <= 0 || p.NoMaker && p.plant == null && IsChunk(p.thing));

        /// <summary>What the plants on the map yield when cut or harvested (wood from the trees, and the like).</summary>
        private static HashSet<ThingDef> PlantYields(Map map)
        {
            var set = new HashSet<ThingDef>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant))
                if (t?.def?.plant?.harvestedThingDef != null) set.Add(t.def.plant.harvestedThingDef);
            return set;
        }

        /// <summary>The rock that yields each mineral when mined.</summary>
        private static Dictionary<ThingDef, List<ThingDef>> rockFor;

        public static List<ThingDef> RocksFor(ThingDef mineral)
        {
            if (rockFor == null)
            {
                rockFor = new Dictionary<ThingDef, List<ThingDef>>();
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    var b = d?.building;
                    if (b?.mineableThing == null || b.mineableYield <= 0 || !d.mineable) continue;
                    if (!rockFor.TryGetValue(b.mineableThing, out var list)) rockFor[b.mineableThing] = list = new List<ThingDef>();
                    list.Add(d);
                }
            }
            return mineral != null && rockFor.TryGetValue(mineral, out var l) ? l : new List<ThingDef>();
        }

        /// <summary>What a cell of the rock gives when mined here: its yield, the storyteller's mining factor, and for what a poor miner wastes, the best miner's yield.</summary>
        public static float YieldPerCell(ThingDef rock, Map map)
        {
            var b = rock?.building;
            if (b == null) return 0f;
            float y = b.mineableYield * (Find.Storyteller?.difficulty?.mineYieldFactor ?? 1f);
            if (b.mineableYieldWasteable)
            {
                float best = 0f;
                foreach (var p in ColonyNeeds.Hands(map))
                {
                    if (p.Dead || p.workSettings == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Mining)) continue;
                    try
                    {
                        best = Mathf.Max(best, p.GetStatValue(StatDefOf.MiningYield));
                    }
                    catch
                    {
                        // A pawn in an odd state counts for nothing.
                    }
                }
                y *= best > 0f ? best : 0.8f;
            }
            return y;
        }

        private static void Shopping(Ahead.View v, Extra x, Dictionary<ThingDef, ShopNeed> shop)
        {
            var n = v.needs;
            var map = n.map;
            var stock = n.stock;
            var making = Making.For(map);
            HashSet<ThingDef> yields = null;
            bool Buy(ThingDef d)
            {
                yields ??= PlantYields(map);
                return d != null && !FromTheLand(d, yields);
            }

            // 1. Down the chains: a part no bench here makes and no field grows (steel for components, neutroamine for
            // medicine), or a link made only from chunks there are none of (steel smelted from slag).
            foreach (var root in making.chains)
                foreach (var node in root.All())
                {
                    if (node.cycle || node.tooDeep || node.want <= 0 || !Buy(node.thing)) continue;
                    bool unmade = node != root && node.NoMaker && node.plant == null;
                    if (!unmade && !OnlyFromMissingChunks(node)) continue;
                    var need = Need(shop, node.thing, stock);
                    string why = unmade ? "CC_AheadMore_ShopWhy_Chain".Translate(root.thing.label).Resolve()
                        : "CC_AheadMore_ShopWhy_Chunks".Translate(node.parts.First(p => p.want > 0).thing.label).Resolve();
                    AheadMoreMath.AddReason(need, why, AheadMoreMath.Gross(node.want, node.stock));
                }

            // 2. What the blueprints are short of and nothing here makes.
            var rooted = new HashSet<ThingDef>(making.chains.Select(c => c.thing));
            foreach (var g in n.goods)
            {
                if (g.kind != AheadMath.Material || g.shortForBuilding <= 0 || !n.defs.TryGetValue(g.key, out var d) || rooted.Contains(d) || !Buy(d)) continue;
                var need = Need(shop, d, stock);
                AheadMoreMath.AddReason(need, "CC_AheadMore_ShopWhy_Build".Translate().Resolve(), AheadMoreMath.Gross(g.shortForBuilding, need.have));
            }

            // 3. Bills waiting on something none of whose kinds is in stock, that nothing here makes.
            foreach (var sb in making.starved)
            {
                if (sb.fix != null || sb.bill == null || sb.bill.DeletedOrDereferenced) continue;
                var wants = sb.wants.Where(Buy).ToList();
                if (wants.Count != 1) continue;
                var d = wants[0];
                var ing = sb.bill.recipe.ingredients?.FirstOrDefault(i => i?.filter != null && i.filter.Allows(d));
                if (ing == null) continue;
                int batches = Batches(sb.bill, stock);
                int use = Mathf.CeilToInt(ing.CountRequiredOfFor(d, sb.bill.recipe, null) * batches);
                var need = Need(shop, d, stock);
                AheadMoreMath.AddReason(need, "CC_AheadMore_ShopWhy_Bill".Translate(sb.bench.LabelCap.ToString(), batches).Resolve(), AheadMoreMath.Gross(use, need.have));
            }

            // 4. Medicine short: what its making waits on, or the medicine itself when nothing here makes it.
            var med = v.plan.Row(AheadMath.Medicine);
            if (med != null && (med.status == AheadMath.Short || med.status == AheadMath.Low))
            {
                int shortBy = Mathf.CeilToInt(Mathf.Max(1f, med.target - med.have));
                // The medicine the colony makes (its bill's), else the one a bench here could.
                string made = n.bills.FirstOrDefault(b => b.kind == AheadMath.Medicine)?.product ?? n.canAdd.FirstOrDefault(a => a.kind == AheadMath.Medicine)?.product;
                var product = made != null ? DefDatabase<ThingDef>.GetNamedSilentFail(made) : null;
                if (product != null)
                {
                    var (root, _) = Making.ChainFor(map, product, shortBy, "CC_AheadMore_ShopWhy_Medicine".Translate().Resolve());
                    if (root != null)
                        foreach (var node in root.All())
                        {
                            if (node == root || !node.NoMaker || node.plant != null || node.cycle || node.tooDeep || node.want <= 0 || !Buy(node.thing)) continue;
                            var need = Need(shop, node.thing, stock);
                            AheadMoreMath.AddReason(need, "CC_AheadMore_ShopWhy_Chain".Translate(product.label).Resolve(), AheadMoreMath.Gross(node.want, node.stock));
                        }
                }
                else
                {
                    var used = n.goods.Where(g => g.kind == AheadMath.Medicine).OrderByDescending(g => g.count).Select(g => n.defs.TryGetValue(g.key, out var d) ? d : null).FirstOrDefault(d => d != null)
                               ?? ThingDefOf.MedicineIndustrial;
                    if (used != null && Buy(used))
                    {
                        var need = Need(shop, used, stock);
                        AheadMoreMath.AddReason(need, "CC_AheadMore_ShopWhy_MedicineItself".Translate(N(med.target)).Resolve(), AheadMoreMath.Gross(shortBy, (int)med.have));
                    }
                }
            }

            // What is already marked to mine, at its yield; and whether a deposit of it is known.
            var marked = new Dictionary<ThingDef, int>();
            foreach (var des in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine))
            {
                var cell = des.target.Cell;
                var rock = cell.IsValid && cell.InBounds(map) ? cell.GetEdifice(map)?.def : null;
                var mineral = rock?.building?.mineableThing;
                if (mineral == null) continue;
                marked.TryGetValue(mineral, out int had);
                marked[mineral] = had + Mathf.FloorToInt(YieldPerCell(rock, map));
            }
            var mining = x.jobs.Where(j => j.info.kind == AheadMoreMath.Mining && j.info.managed && !j.info.suspended).ToList();
            foreach (var kv in shop)
            {
                var need = kv.Value;
                if (marked.TryGetValue(kv.Key, out int pending)) need.pending = pending;
                var rocks = RocksFor(kv.Key);
                if (rocks.Count == 0) continue;
                need.deposit = rocks.Any(r => map.listerThings.ThingsOfDef(r).Any(t => t.Spawned && !t.Position.Fogged(map)));
                need.managerMines = mining.Any(j => j.goods.Contains(kv.Key) && (j.minerals.Count == 0 || rocks.Any(j.minerals.Contains)));
            }
        }

        /// <summary>The batches a bill still wants: so many left, or its count less the stock; a bill for ever, a few.</summary>
        private static int Batches(Bill_Production bill, Dictionary<ThingDef, int> stock)
        {
            if (bill.repeatMode == BillRepeatModeDefOf.Forever) return 3;
            if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount) return Mathf.Clamp(bill.repeatCount, 1, 10);
            var product = bill.recipe.ProducedThingDef;
            int have = product != null && stock.TryGetValue(product, out int h) ? h : 0;
            float per = Mathf.Max(1f, Chains.PerBatch(bill.recipe, product));
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, bill.targetCount - have) / per), 1, 10);
        }

        /// <summary>The traders on the map and in orbit, and what each has of the list.</summary>
        private static void Traders(Map map, Extra x)
        {
            var traders = new List<(string name, string where, IEnumerable<Thing> goods)>();
            try
            {
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                    if (p?.trader?.traderKind != null && !p.Dead && p.Faction != null && !p.Faction.HostileTo(Faction.OfPlayer))
                        traders.Add((p.trader.TraderName ?? p.LabelShort, "CC_AheadMore_Trader_OnMap".Translate().Resolve(), p.trader.Goods));
                foreach (var ship in map.passingShipManager?.passingShips?.OfType<TradeShip>() ?? Enumerable.Empty<TradeShip>())
                    traders.Add((ship.TraderName ?? ship.def?.label, "CC_AheadMore_Trader_Orbit".Translate().Resolve(), ship.Goods));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ahead traders", e);
            }
            foreach (var (name, where, goods) in traders)
            {
                Dictionary<ThingDef, int> has;
                try
                {
                    has = new Dictionary<ThingDef, int>();
                    foreach (var t in goods ?? Enumerable.Empty<Thing>())
                    {
                        if (t?.def == null) continue;
                        has.TryGetValue(t.def, out int c);
                        has[t.def] = c + t.stackCount;
                    }
                }
                catch
                {
                    has = null;
                }
                if (has == null)
                {
                    x.traders.Add("CC_AheadMore_Trader_Unread".Translate(name.Named("NAME"), where.Named("WHERE")).Resolve());
                    continue;
                }
                var lines = new List<string>();
                foreach (var need in x.shop)
                {
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(need.key);
                    if (d == null || !has.TryGetValue(d, out int c) || c <= 0) continue;
                    need.traderHas = Mathf.Max(need.traderHas, c);
                    lines.Add("CC_AheadMore_Trader_Item".Translate(d.label.Named("WHAT"), c.Named("HAS"), need.Short.Named("SHORT")).Resolve());
                }
                x.traders.Add(lines.Count == 0
                    ? "CC_AheadMore_Trader_None".Translate(name.Named("NAME"), where.Named("WHERE")).Resolve()
                    : "CC_AheadMore_Trader_Has".Translate(name.Named("NAME"), where.Named("WHERE"), string.Join(", ", lines).Named("LIST")).Resolve());
            }
        }

        // ---------------- Colony Manager ----------------

        private static void Manager(Ahead.View v, Extra x, Dictionary<ThingDef, ShopNeed> shop)
        {
            if (x.jobs.Count == 0) return;
            var n = v.needs;
            var map = n.map;
            float days = v.reserveDays;
            var making = Making.For(map);
            foreach (var j in x.jobs)
            {
                var info = j.info;
                if (!info.threshold || j.goods.Count == 0) continue;
                var why = new List<string>();
                int wanted = 0;
                if (info.kind == AheadMoreMath.Hunting)
                {
                    // Meat for the raw food, by the share of it the record saw come from meat.
                    var raw = v.plan.Row(AheadMath.Raw);
                    if (raw != null && n.measured && n.plantShare < 0.95f && (raw.status == AheadMath.Short || raw.status == AheadMath.Low))
                    {
                        float meatShare = 1f - n.plantShare;
                        var meats = j.goods.Where(d => d.ingestible != null).ToList();
                        float each = meats.Count > 0 ? meats.Average(d => d.GetStatValueAbstract(StatDefOf.Nutrition)) : 0.05f;
                        int meat = Mathf.CeilToInt(raw.target * meatShare / Mathf.Max(0.01f, each));
                        wanted += meat;
                        why.Add("CC_AheadMore_Want_Meat".Translate(N(raw.target).Named("TARGET"), Mathf.RoundToInt(meatShare * 100f).Named("SHARE"), meat.Named("MEAT")).Resolve());
                    }
                }
                if (info.kind == AheadMoreMath.Mining)
                {
                    // The minerals on the shopping list.
                    foreach (var kv in shop)
                        if (j.goods.Contains(kv.Key) && kv.Value.Short > 0)
                        {
                            wanted += kv.Value.want;
                            why.Add("CC_AheadMore_Want_Shop".Translate(kv.Key.label.Named("WHAT"), kv.Value.want.Named("WANT"), kv.Value.have.Named("HAVE")).Resolve());
                        }
                }
                else
                {
                    // The blueprints' materials, and the parts the chains want, of what the job brings in.
                    foreach (var g in n.goods)
                        if (g.kind == AheadMath.Material && g.shortForBuilding > 0 && n.defs.TryGetValue(g.key, out var d) && j.goods.Contains(d))
                        {
                            wanted += (int)g.count + g.shortForBuilding;
                            why.Add("CC_AheadMore_Want_Build".Translate(d.label.Named("WHAT"), g.shortForBuilding.Named("SHORT")).Resolve());
                        }
                    foreach (var root in making.chains)
                        foreach (var node in root.All())
                            if (node != root && node.want > 0 && j.goods.Contains(node.thing))
                            {
                                wanted += node.want + node.stock;
                                why.Add("CC_AheadMore_Want_Chain".Translate(node.thing.label.Named("WHAT"), node.want.Named("MORE"), root.thing.label.Named("FOR")).Resolve());
                            }
                }
                if (info.kind == AheadMoreMath.Forestry && n.measured)
                {
                    // What the record saw used (the stoves, the fires and the generators, and the bills), kept for the days set.
                    float burned = 0f;
                    foreach (var d in j.goods)
                        if (n.record.used.TryGetValue(d, out float u) && d.IsNutritionGivingIngestible == false) burned += u;
                    if (burned > 0f)
                    {
                        int keep = Mathf.CeilToInt(burned * days);
                        wanted += keep;
                        why.Add("CC_AheadMore_Want_Fuel".Translate(N(burned).Named("ADAY"), keep.Named("KEEP"), N(days).Named("DAYS")).Resolve());
                    }
                }
                info.wanted = wanted;
                info.wantedWhy = string.Join(" ", why);
            }

            // Whether the manager can work at all.
            var s = x.state = new ManagerState { jobs = x.jobs.Count(j => j.info.managed), active = x.jobs.Count(j => j.info.managed && !j.info.suspended && !j.info.completed) };
            var work = ColonyManagerBridge.ManagingWork;
            var hands = ColonyNeeds.Hands(map).Where(p => !p.Dead && p.workSettings != null && !p.DevelopmentalStage.Juvenile()).ToList();
            s.workType = work != null;
            if (work != null)
            {
                s.managersOn = hands.Count(p => !p.WorkTypeIsDisabled(work) && p.workSettings.WorkIsActive(work));
                s.managersAble = hands.Count(p => !p.WorkTypeIsDisabled(work));
            }
            var desks = ColonyManagerBridge.Desks(map);
            s.deskNeeded = ColonyManagerBridge.DeskNeeded;
            s.desks = desks.Count;
            s.desksWorking = desks.Count(ColonyManagerBridge.DeskWorks);
            // A working AI manager does the work by itself.
            if (desks.Any(b => ColonyManagerBridge.DeskWorks(b) && b.GetType().Name == "Building_AIManager")) s.managersOn = Mathf.Max(1, s.managersOn);
            x.stall = AheadMoreMath.Stall(s);
            if (x.stall != null) v.all.Add(StallStep(x, map, work, hands, desks));

            foreach (var j in x.jobs)
            {
                var info = j.info;
                if (AheadMoreMath.ShouldResume(info)) v.all.Add(ResumeStep(j, map));
                else if (AheadMoreMath.ShouldRaise(info)) v.all.Add(RaiseStep(j, map, x.stall));
            }
        }

        private static string JobName(ManagerJobInfo i) => ("CC_AheadMore_Job_" + i.kind).Translate(i.label).Resolve();

        private static Suggestion RaiseStep(ColonyManagerBridge.Job j, Map map, string stall)
        {
            var i = j.info;
            int from = i.target, to = AheadMoreMath.RaiseTo(i.target, i.wanted, i.max);
            string name = JobName(i);
            var s = new Suggestion
            {
                key = "ahead:manager:raise:" + i.key, source = Ahead.Source, area = "manager", map = map, level = 2, when = "CC_AheadMore_When_Manager".Translate().Resolve(),
                title = "CC_AheadMore_Raise_Title".Translate(name.Named("JOB"), from.Named("FROM"), to.Named("TO")).Resolve(),
                reason = "CC_AheadMore_Raise_Why".Translate(name.Named("JOB"), i.goods.Named("GOODS"), from.Named("TARGET"), i.current.Named("NOW")).Resolve() + " " + i.wantedWhy
                         + (to < AheadMoreMath.RoundTarget(i.wanted) ? " " + "CC_AheadMore_Raise_Capped".Translate(to).Resolve() : "")
                         + (stall != null ? " " + "CC_AheadMore_Raise_Stalled".Translate().Resolve() : ""),
                doLabel = "CC_AheadMore_Do_Raise".Translate(to).Resolve(),
                doTip = "CC_AheadMore_Do_RaiseTip".Translate(name.Named("JOB"), from.Named("FROM"), to.Named("TO")).Resolve()
            };
            string type = j.typeName;
            int index = j.index, now = i.current;
            s.apply = () =>
            {
                var job = ColonyManagerBridge.Find(map, type, index, from);
                if (job == null || job.info.target != from || !ColonyManagerBridge.SetTarget(job, to)) return null;
                return new SuggestDone
                {
                    undo = new UndoManagerTarget { typeName = type, index = job.index, from = from, to = to },
                    check = new ManagerCountCheck { typeName = type, index = job.index, before = now, target = to, waitTicks = 2 * SuggestMath.TicksPerDay }
                };
            };
            return s;
        }

        private static Suggestion ResumeStep(ColonyManagerBridge.Job j, Map map)
        {
            var i = j.info;
            string name = JobName(i);
            string type = j.typeName;
            int index = j.index, now = i.current, target = i.target;
            return new Suggestion
            {
                key = "ahead:manager:resume:" + i.key, source = Ahead.Source, area = "manager", map = map, level = 2, when = "CC_AheadMore_When_Manager".Translate().Resolve(),
                title = "CC_AheadMore_Resume_Title".Translate(name).Resolve(),
                reason = "CC_AheadMore_Resume_Why".Translate(name.Named("JOB"), i.goods.Named("GOODS"), i.current.Named("NOW")).Resolve() + " " + i.wantedWhy,
                doLabel = "CC_AheadMore_Do_Resume".Translate().Resolve(), doTip = "CC_AheadMore_Do_ResumeTip".Translate(name).Resolve(),
                apply = () =>
                {
                    var job = ColonyManagerBridge.Find(map, type, index, target);
                    if (job == null || !job.info.suspended || !ColonyManagerBridge.SetSuspended(job, false)) return null;
                    return new SuggestDone
                    {
                        undo = new UndoManagerSuspend { typeName = type, index = job.index, target = target },
                        check = new ManagerCountCheck { typeName = type, index = job.index, before = now, target = Mathf.Max(target, now + 1), waitTicks = 2 * SuggestMath.TicksPerDay }
                    };
                }
            };
        }

        private static Suggestion StallStep(Extra x, Map map, WorkTypeDef work, List<Pawn> hands, List<Building> desks)
        {
            var active = x.jobs.Where(j => j.info.managed && !j.info.suspended && !j.info.completed).Select(j => JobName(j.info)).Distinct().ToList();
            string jobs = "CC_AheadMore_Stall_Jobs".Translate(active.Count.Named("COUNT"), TextUtil.JoinAnd(active.Take(4).ToList()).Named("JOBS")).Resolve();
            var s = new Suggestion
            {
                key = "ahead:manager:" + x.stall, source = Ahead.Source, area = "manager", map = map, level = 3, when = "CC_AheadMore_When_Idle".Translate().Resolve(),
                title = ("CC_AheadMore_Stall_" + x.stall).Translate().Resolve()
            };
            switch (x.stall)
            {
                case AheadMoreMath.NoDesk:
                {
                    var desk = ColonyManagerBridge.BuildableDesk();
                    s.reason = jobs + " " + (desk != null ? "CC_AheadMore_Stall_NoDeskWhy".Translate(desk.label).Resolve() : "CC_AheadMore_Stall_NoDeskResearch".Translate().Resolve());
                    break;
                }
                case AheadMoreMath.DeskOff:
                {
                    var desk = desks.FirstOrDefault();
                    var player = Faction.OfPlayerSilentFail;
                    s.look = desk != null ? new LookTargets(desk) : null;
                    s.cells = desk != null ? () => desk.OccupiedRect().Cells.ToList() : (Func<List<IntVec3>>)null;
                    bool forbidden = desk != null && player != null && desk.IsForbidden(player);
                    s.reason = jobs + " " + (forbidden ? "CC_AheadMore_Stall_DeskForbidden" : "CC_AheadMore_Stall_DeskOffWhy").Translate(desk?.LabelShort ?? "-").Resolve();
                    if (forbidden)
                    {
                        s.doLabel = "CC_AheadMore_Do_Allow".Translate().Resolve();
                        s.doTip = "CC_AheadMore_Do_AllowTip".Translate(desk.LabelShort).Resolve();
                        s.apply = () =>
                        {
                            if (desk.Destroyed || !desk.Spawned) return null;
                            desk.SetForbidden(false, false);
                            return new SuggestDone { undo = new UndoForbid { thing = desk, wasForbidden = true }, check = new ManagerStallCheck { stall = AheadMoreMath.DeskOff, waitTicks = SuggestMath.TicksPerDay / 2 } };
                        };
                    }
                    break;
                }
                case AheadMoreMath.NoManager:
                {
                    var best = hands.Where(p => !p.WorkTypeIsDisabled(work)).OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0))
                        .ThenBy(p => p.thingIDNumber).FirstOrDefault();
                    string text = jobs + " " + "CC_AheadMore_Stall_NoManagerWhy".Translate(work.labelShort ?? work.label).Resolve();
                    if (best == null)
                    {
                        s.reason = text;
                        break;
                    }
                    var why = BillStuck.LetDo(best, work, text, "nomanager", null, null, () => new ManagerStallCheck { stall = AheadMoreMath.NoManager, waitTicks = SuggestMath.TicksPerDay / 2 });
                    s.reason = why.text;
                    s.apply = why.fix;
                    s.doLabel = why.fixLabel;
                    s.doTip = why.fixTip;
                    s.look = new LookTargets(best);
                    break;
                }
                default:
                    s.reason = jobs + " " + "CC_AheadMore_Stall_NobodyCanWhy".Translate(work?.labelShort ?? "-").Resolve();
                    break;
            }
            return s;
        }

        // ---------------- Where to mine ----------------

        private static void Mining(Ahead.View v, Extra x)
        {
            var map = v.needs.map;
            int steps = 0;
            foreach (var need in x.shop)
            {
                if (steps >= 2) break;
                if (!need.deposit || need.managerMines || need.Short <= 0) continue;
                var mineral = DefDatabase<ThingDef>.GetNamedSilentFail(need.key);
                var rocks = RocksFor(mineral);
                if (mineral == null || rocks.Count == 0) continue;
                string key = "ahead:mine:" + mineral.defName;
                var pick = MineSpot(map, rocks, need.Short, out var rock, out int lump, out float far, out float perCell);
                if (pick.Count == 0) continue;
                steps++;
                int cells = pick.Count, gives = Mathf.FloorToInt(cells * perCell), shortBy = need.Short;
                var s = new Suggestion
                {
                    key = key, source = Ahead.Source, area = "mine", map = map, level = 2, when = "CC_AheadMore_When_Short".Translate(shortBy).Resolve(),
                    title = "CC_AheadMore_Mine_Title".Translate(cells.Named("CELLS"), rock.label.Named("ROCK"), mineral.label.Named("WHAT")).Resolve(),
                    reason = ShopLine(need) + " " + "CC_AheadMore_Mine_Why".Translate(Mathf.RoundToInt(far).Named("FAR"), lump.Named("LUMP"), N(perCell).Named("EACH"), cells.Named("CELLS"),
                        gives.Named("GIVES"), mineral.label.Named("WHAT")).Resolve(),
                    doLabel = "CC_AheadMore_Do_Mine".Translate().Resolve(),
                    doTip = "CC_AheadMore_Do_MineTip".Translate(cells.Named("CELLS"), rock.label.Named("ROCK")).Resolve(),
                    look = new LookTargets(pick[0], map), cells = () => pick
                };
                var rockDefs = rocks;
                s.apply = () =>
                {
                    var fresh = MineSpot(map, rockDefs, shortBy, out _, out _, out _, out _);
                    if (fresh.Count == 0) return null;
                    var undo = new UndoOrders();
                    foreach (var c in fresh)
                    {
                        if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null) continue;
                        map.designationManager.AddDesignation(new Designation(c, DesignationDefOf.Mine));
                        undo.markedCells.Add(c);
                        undo.markedCellDefs.Add(DesignationDefOf.Mine.defName);
                    }
                    if (undo.Count == 0) return null;
                    return new SuggestDone { undo = undo, check = new AheadMineCheck { mineral = mineral, waitTicks = 2 * SuggestMath.TicksPerDay } };
                };
                v.all.Add(s);
            }
        }

        /// <summary>A shopping row in a sentence: what is wanted and why, what there is, what is marked to mine, what is short.</summary>
        public static string ShopLine(ShopNeed n)
        {
            string why = TextUtil.JoinAnd(n.reasons.Select(r => r.why).Distinct().Take(4).ToList());
            string s = "CC_AheadMore_ShopLine".Translate(n.label.CapitalizeFirst().Named("WHAT"), n.want.Named("WANT"), why.Named("WHY"), n.have.Named("HAVE"), n.Short.Named("SHORT")).Resolve();
            if (n.pending > 0) s += " " + "CC_AheadMore_ShopPending".Translate(n.pending).Resolve();
            return s;
        }

        /// <summary>
        /// The rock to mark for a mineral: the known deposit (seen, not marked yet) nearest the colony's middle that the
        /// colony can walk up to, its cells from the way in onward so each is reachable once the one before is mined, so
        /// many as the shortfall takes at the rock's yield; a roofed cell only where a support stays within reach, so no
        /// roof comes down.
        /// </summary>
        public static List<IntVec3> MineSpot(Map map, List<ThingDef> rocks, int shortBy, out ThingDef rock, out int lumpSize, out float distance, out float perCell)
        {
            rock = null;
            lumpSize = 0;
            distance = 0f;
            perCell = 0f;
            var result = new List<IntVec3>();
            if (map == null || rocks == null || rocks.Count == 0 || shortBy <= 0) return result;
            var center = TemplatePlanner.ColonyCenter(map);
            var dm = map.designationManager;
            var rockSet = new HashSet<ThingDef>(rocks);
            var seen = new HashSet<IntVec3>();
            List<IntVec3> best = null, bestDoors = null;
            ThingDef bestRock = null;
            float bestFar = float.MaxValue;
            bool Rock(IntVec3 c) => c.InBounds(map) && c.GetEdifice(map) is Building b && rockSet.Contains(b.def) && !c.Fogged(map);
            foreach (var r in rocks)
                foreach (var t in map.listerThings.ThingsOfDef(r))
                {
                    if (t == null || !t.Spawned || seen.Contains(t.Position) || !Rock(t.Position)) continue;
                    // One lump: the rock's cells joined side by side.
                    var lump = new List<IntVec3>();
                    var queue = new Queue<IntVec3>();
                    queue.Enqueue(t.Position);
                    seen.Add(t.Position);
                    while (queue.Count > 0 && lump.Count < 400)
                    {
                        var c = queue.Dequeue();
                        lump.Add(c);
                        foreach (var d in GenAdj.CardinalDirections)
                        {
                            var nb = c + d;
                            if (seen.Contains(nb) || !Rock(nb)) continue;
                            seen.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }
                    if (lump.Any(c => dm.DesignationAt(c, DesignationDefOf.Mine) != null)) continue;
                    // The way in: a cell of it beside open ground the colony reaches.
                    var doors = lump.Where(c => GenAdj.CardinalDirections.Any(d =>
                    {
                        var nb = c + d;
                        return nb.InBounds(map) && nb.Standable(map) && map.reachability.CanReachColony(nb);
                    })).ToList();
                    if (doors.Count == 0) continue;
                    float far = doors.Min(c => c.DistanceTo(center));
                    if (far >= bestFar) continue;
                    bestFar = far;
                    best = lump;
                    bestDoors = doors;
                    bestRock = r;
                }
            if (best == null) return result;
            rock = bestRock;
            lumpSize = best.Count;
            distance = bestFar;
            perCell = YieldPerCell(rock, map);
            int want = AheadMoreMath.CellsToMark(shortBy, perCell);
            var inLump = new HashSet<IntVec3>(best);
            var chosen = new HashSet<IntVec3>();
            var order = new Queue<IntVec3>(bestDoors.OrderBy(c => c.DistanceTo(center)));
            var visited = new HashSet<IntVec3>(bestDoors);
            while (order.Count > 0 && result.Count < want)
            {
                var c = order.Dequeue();
                if (!RoofHolds(map, c, chosen)) continue;
                chosen.Add(c);
                result.Add(c);
                foreach (var d in GenAdj.CardinalDirections)
                {
                    var nb = c + d;
                    if (inLump.Contains(nb) && visited.Add(nb)) order.Enqueue(nb);
                }
            }
            return result;
        }

        /// <summary>A cell can be mined without bringing a roof down: unroofed, or a roof holder other than the cells marked stays within the roof's reach of it.</summary>
        private static bool RoofHolds(Map map, IntVec3 c, HashSet<IntVec3> chosen)
        {
            if (!map.roofGrid.Roofed(c)) return true;
            foreach (var near in GenRadial.RadialCellsAround(c, RoofCollapseUtility.RoofMaxSupportDistance, false))
            {
                if (!near.InBounds(map) || chosen.Contains(near)) continue;
                var ed = near.GetEdifice(map);
                if (ed != null && ed.def.holdsRoof && !(ed.def.building?.mineableThing != null && map.designationManager.DesignationAt(near, DesignationDefOf.Mine) != null)) return true;
            }
            return false;
        }
    }

    // ---------------- Undos and checks ----------------

    /// <summary>A Colony Manager job's target put back, if it still has the one the step set.</summary>
    public class UndoManagerTarget : SuggestUndo
    {
        public string typeName;
        public int index, from, to;

        public override bool Possible(Map map) => ColonyManagerBridge.Find(map, typeName, index, to, true)?.info.target == to;

        public override void Undo(Map map)
        {
            var job = ColonyManagerBridge.Find(map, typeName, index, to);
            if (job != null && job.info.target == to) ColonyManagerBridge.SetTarget(job, from);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref typeName, "typeName");
            Scribe_Values.Look(ref index, "index");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref to, "to");
        }
    }

    /// <summary>A Colony Manager job a step resumed, suspended again.</summary>
    public class UndoManagerSuspend : SuggestUndo
    {
        public string typeName;
        public int index, target;

        public override bool Possible(Map map) => ColonyManagerBridge.Find(map, typeName, index, target, true) is ColonyManagerBridge.Job j && !j.info.suspended;

        public override void Undo(Map map)
        {
            var job = ColonyManagerBridge.Find(map, typeName, index, target);
            if (job != null && !job.info.suspended) ColonyManagerBridge.SetSuspended(job, true);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref typeName, "typeName");
            Scribe_Values.Look(ref index, "index");
            Scribe_Values.Look(ref target, "target");
        }
    }

    /// <summary>Whether a Colony Manager job brought more in after a step: its count rose a quarter of the way to its target; if not, why the manager is idle or that it found no more.</summary>
    public class ManagerCountCheck : SuggestCheck
    {
        public string typeName;
        public int index, before, target;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var job = ColonyManagerBridge.Find(done.map, typeName, index, target);
            if (job == null)
            {
                whyNot = "CC_AheadMore_NotYet_JobGone".Translate().Resolve();
                return false;
            }
            if (AheadMoreMath.RaiseWorked(before, job.info.current, Mathf.Max(target, job.info.target))) return true;
            var extra = Ahead.For(done.map).more;
            if (job.info.suspended) whyNot = "CC_AheadMore_NotYet_Suspended".Translate().Resolve();
            else if (extra?.stall != null) whyNot = ("CC_AheadMore_Stall_" + extra.stall).Translate().Resolve();
            else whyNot = "CC_AheadMore_NotYet_Manager".Translate(job.info.current.Named("NOW"), job.info.target.Named("TARGET"), job.info.goods.Named("GOODS")).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref typeName, "typeName");
            Scribe_Values.Look(ref index, "index");
            Scribe_Values.Look(ref before, "before");
            Scribe_Values.Look(ref target, "target");
        }
    }

    /// <summary>Whether what kept the manager idle is gone half a day after a step.</summary>
    public class ManagerStallCheck : SuggestCheck
    {
        public string stall;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var now = Ahead.For(done.map).more?.stall;
            if (now != stall) return true;
            whyNot = ("CC_AheadMore_Stall_" + stall).Translate().Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stall, "stall");
        }
    }

    /// <summary>Whether the rock a step marked was mined two days on (half of it at least); if not, what is in the way.</summary>
    public class AheadMineCheck : SuggestCheck
    {
        public ThingDef mineral;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var map = done.map;
            if (!(done.undo is UndoOrders orders) || map == null) return true;
            int marked = orders.markedCells.Count;
            int mined = orders.markedCells.Count(c => !c.InBounds(map) || c.GetEdifice(map)?.def?.building?.mineableThing != mineral);
            if (AheadMoreMath.MineWorked(marked, mined)) return true;
            int miners = ColonyNeeds.Hands(map).Count(p => !p.Dead && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining) && p.workSettings.WorkIsActive(WorkTypeDefOf.Mining));
            whyNot = miners == 0 ? "CC_AheadMore_NotYet_NoMiners".Translate().Resolve() : "CC_AheadMore_NotYet_Mine".Translate(mined.Named("MINED"), marked.Named("MARKED"), miners.Named("MINERS")).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            CouncilScribe.LookDef(ref mineral, "mineral");
        }
    }
}
