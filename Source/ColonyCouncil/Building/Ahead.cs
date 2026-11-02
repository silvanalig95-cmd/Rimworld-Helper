using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning ahead for production (the Making page's "Ahead" section): the colony's needs read once an hour
    /// (<see cref="ColonyNeeds"/>), planned by <see cref="AheadMath.Plan"/> into stock rows and ideas, and each idea
    /// made a step of the shared suggestion core (<see cref="Suggestion"/>) with its words, its doing, its undo and its
    /// check: a bill's "do until" raised or lowered (every bill Better Workbench Management links with it at once; a
    /// bill another mod sets is never changed), a bill added with what feeds it (Making's chains), a copy on a second
    /// bench, a field grown onto free ground next to it, cells switched from a crop the colony has far more of, a new
    /// field. Bills and fields that are held up get a step of their own saying why (BillStuck, FieldStuck), with the
    /// fix where there is one. Colony Manager's targets, the shopping list, rock to mine and spare clothes come from
    /// <see cref="AheadMore"/>. Nothing is done until the player clicks.
    /// </summary>
    public static class Ahead
    {
        public const string Source = "ahead";

        /// <summary>What the page draws for one map: the needs, the plan and every suggestion (before "Not this" is taken out).</summary>
        public class View
        {
            public ColonyNeeds needs;
            public AheadPlan plan = new AheadPlan();
            public List<Suggestion> all = new List<Suggestion>();
            public int needsTick = -1, version = -1;
            public float mealDays, reserveDays;
            /// <summary>Phase 3: Colony Manager's jobs, the shopping list, the traders here, the clothes worn (Building/AheadMore.cs).</summary>
            public AheadMore.Extra more = new AheadMore.Extra();
        }

        private static readonly Dictionary<int, View> cache = new Dictionary<int, View>();

        private static float MealDays => ColonyCouncilMod.Settings?.aheadMealDays ?? 2f;
        private static float ReserveDays => ColonyCouncilMod.Settings?.aheadReserveDays ?? 5f;

        /// <summary>The plan and its suggestions for a home map: worked out again when the needs are read again (hourly), when the player sets something aside or does a step, or when asked.</summary>
        public static View For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new View();
            var needs = ColonyNeeds.For(map, force);
            var tracker = SuggestTracker.Instance;
            int version = tracker?.Version ?? 0;
            if (!force && cache.TryGetValue(map.uniqueID, out var v) && v.needs == needs && v.needsTick == needs.tick && v.version == version && v.mealDays == MealDays
                && v.reserveDays == ReserveDays)
                return v;
            v = new View { needs = needs, needsTick = needs.tick, version = version, mealDays = MealDays, reserveDays = ReserveDays };
            try
            {
                v.plan = AheadMath.Plan(needs.Input(MealDays, ReserveDays, tracker?.DeclinedKeys()));
                foreach (var idea in v.plan.ideas)
                {
                    var s = FromIdea(idea, needs, v.plan);
                    if (s != null) v.all.Add(s);
                }
                Stuck(needs, v.plan, v.all);
                AheadMore.Add(v);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ahead", e);
            }
            cache[map.uniqueID] = v;
            return v;
        }

        /// <summary>The suggestions still open, the soonest first: those set aside and those waiting for their check taken out.</summary>
        public static List<Suggestion> Open(Map map)
        {
            var v = For(map);
            var tracker = SuggestTracker.Instance;
            return tracker != null ? tracker.Open(v.all) : v.all;
        }

        /// <summary>The hour (CouncilGameComponent.HourlyPeople): the steps done are checked, and each home map's plan worked out again from the live game.</summary>
        public static void Hourly()
        {
            SuggestTracker.Instance?.Hourly();
            foreach (var map in Find.Maps)
                if (map.IsPlayerHome) For(map);
            // Building ahead: the rooms being built followed, the people laid up noted (Building/BuildAhead.cs).
            BuildAhead.Hourly();
        }

        // ---------------- Words ----------------

        private static string N(float v) => v >= AheadMath.Far ? "-" : v.ToString(v < 10f ? "0.#" : "0");

        /// <summary>Who eats here: "9 colonists and 8 guests".</summary>
        public static string Who(ColonyNeeds n)
        {
            var parts = new List<string>();
            void Count(int c, string one, string many)
            {
                if (c > 0) parts.Add(c == 1 ? one.Translate().Resolve() : many.Translate(c).Resolve());
            }
            Count(n.colonists, "CC_Ahead_Who_Colonist", "CC_Ahead_Who_Colonists");
            Count(n.slaves, "CC_Ahead_Who_Slave", "CC_Ahead_Who_Slaves");
            Count(n.prisoners, "CC_Ahead_Who_Prisoner", "CC_Ahead_Who_Prisoners");
            Count(n.guests, "CC_Ahead_Who_Guest", "CC_Ahead_Who_Guests");
            return parts.Count == 0 ? "CC_Ahead_Who_Nobody".Translate().Resolve() : parts.ToCommaList(true);
        }

        /// <summary>A row's state in a sentence: what there is, who uses it and how fast, how long it lasts.</summary>
        public static string RowReason(AheadRow r, ColonyNeeds n)
        {
            switch (r.kind)
            {
                case AheadMath.Meal:
                    return "CC_Ahead_Why_Meals".Translate(((int)r.count).Named("COUNT"), N(r.have).Named("HAVE"), Who(n).Named("WHO"), N(r.useADay).Named("USE"),
                        N(r.daysLeft).Named("DAYS")).Resolve();
                case AheadMath.Raw:
                    return "CC_Ahead_Why_Raw".Translate(N(r.have).Named("HAVE"), N(r.useADay).Named("USE"), Who(n).Named("WHO"), N(r.daysLeft).Named("DAYS")).Resolve();
                case AheadMath.Feed:
                    return "CC_Ahead_Why_Feed".Translate(N(r.have).Named("HAVE"), n.feedAnimals.Named("N"), N(r.useADay).Named("USE"), N(r.daysLeft).Named("DAYS")).Resolve();
                default:
                    return "CC_Ahead_Why_Count".Translate(((int)r.have).Named("HAVE"), ("CC_Ahead_Noun_" + r.kind).Translate().Resolve().Named("KIND"), N(r.useADay).Named("USE"),
                        (r.useMeasured ? "CC_Ahead_Measured" : "CC_Ahead_Estimated").Translate().Resolve().Named("FROM"), N(r.daysLeft).Named("DAYS")).Resolve();
            }
        }

        /// <summary>What to keep and what limits it.</summary>
        public static string TargetReason(AheadRow r)
        {
            string s = "CC_Ahead_Why_Target".Translate(N(r.target).Named("TARGET"), N(r.targetDays).Named("DAYS")).Resolve();
            if (r.keepDays < AheadMath.Far && r.keepDays < r.targetDays + 0.01f) s += " " + "CC_Ahead_Why_Keeps".Translate(N(r.keepDays)).Resolve();
            return s;
        }

        private static string FieldReason(AheadPlan p) =>
            "CC_Ahead_Why_Fields".Translate(N(p.cropNeed).Named("NEED"), N(p.cropGive).Named("GIVE"), N(p.growingDays).Named("GROW"), p.cycleDays.Named("CYCLE"),
                p.withoutDays.Named("WITHOUT")).Resolve();

        // ---------------- The ideas made steps ----------------

        private static Suggestion Base(AheadIdea i, ColonyNeeds n) => new Suggestion { key = i.key, source = Source, area = i.area, days = i.days, map = n.map };

        private static Suggestion FromIdea(AheadIdea i, ColonyNeeds n, AheadPlan p)
        {
            var map = n.map;
            switch (i.kind)
            {
                case "raise":
                case "lower":
                {
                    if (!n.billOf.TryGetValue(i.bill.key, out var at)) return null;
                    var (bench, bill) = at;
                    bool up = i.kind == "raise";
                    var s = Base(i, n);
                    s.title = (up ? "CC_Ahead_Raise" : "CC_Ahead_Lower").Translate(i.bill.label.Named("PRODUCT"), i.bill.bench.Named("BENCH"), i.from.Named("FROM"), i.to.Named("TO")).Resolve();
                    s.reason = up ? RowReason(i.row, n) + " " + CoverReason(i.row, n) + " " + TargetReason(i.row)
                        : "CC_Ahead_Why_Lower".Translate(N(i.row.keepDays).Named("KEEP"), N(i.row.target).Named("TARGET")).Resolve() + " " + CoverReason(i.row, n);
                    if (i.bill.linked > 1) s.reason += " " + "CC_Ahead_Why_Linked".Translate(i.bill.linked - 1).Resolve();
                    s.doLabel = (up ? "CC_Ahead_Do_Raise" : "CC_Ahead_Do_Lower").Translate(i.to).Resolve();
                    s.doTip = "CC_Ahead_Do_TargetTip".Translate(i.bill.bench.Named("BENCH"), i.bill.label.Named("PRODUCT"), i.from.Named("FROM"), i.to.Named("TO")).Resolve()
                              + (i.bill.linked > 1 ? " " + "CC_Ahead_Do_LinkedTip".Translate(i.bill.linked - 1).Resolve() : "");
                    s.look = new LookTargets(bench);
                    s.cells = () => bench.OccupiedRect().Cells.ToList();
                    var row = i.row;
                    int to = i.to;
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
                        return new SuggestDone { undo = undo, check = up ? StockCheck(row, bench, bill) : null };
                    };
                    return s;
                }
                case "add":
                {
                    if (!n.addOf.TryGetValue(i.add.key, out var at)) return null;
                    var (bench, recipe, product) = at;
                    var s = Base(i, n);
                    s.title = "CC_Ahead_Add".Translate(i.add.label.Named("PRODUCT"), i.add.bench.Named("BENCH"), i.to.Named("TO")).Resolve();
                    s.reason = RowReason(i.row, n) + " " + "CC_Ahead_Why_NoBill".Translate().Resolve() + " " + TargetReason(i.row);
                    s.doLabel = "CC_Ahead_Do_Add".Translate().Resolve();
                    s.doTip = "CC_Ahead_Do_AddTip".Translate(i.add.bench.Named("BENCH"), i.add.label.Named("PRODUCT"), i.to.Named("TO")).Resolve();
                    s.look = new LookTargets(bench);
                    s.cells = () => bench.OccupiedRect().Cells.ToList();
                    var row = i.row;
                    int to = i.to;
                    s.apply = () =>
                    {
                        var added = new List<Bill_Production>();
                        // What feeds it first, the way the Making page sets a chain: the parts' bills, leaf first.
                        var (root, links) = Making.ChainFor(map, product, to, "CC_Ahead_ChainWhy".Translate(product.label).Resolve());
                        foreach (var link in links)
                        {
                            if (link.billed || link.node == root) continue;
                            var b = Making.AddBill(link.bench, link.recipe, link.product, link.target, true);
                            if (b != null) added.Add(b);
                        }
                        var main = Making.AddBill(bench, recipe, product, to, true);
                        if (main == null) return null;
                        added.Add(main);
                        return new SuggestDone
                        {
                            undo = new UndoBillsAdded { bills = added }, check = StockCheck(row, bench, main),
                            text = added.Count > 1 ? s.title + " " + "CC_Ahead_AddedParts".Translate(added.Count - 1).Resolve() : null
                        };
                    };
                    return s;
                }
                case "second":
                {
                    if (!n.billOf.TryGetValue(i.bill.key, out var at) || !n.freeBenchOf.TryGetValue(i.bill.key, out var free)) return null;
                    var (bench, bill) = at;
                    var s = Base(i, n);
                    s.title = "CC_Ahead_Second".Translate(i.bill.label.Named("PRODUCT"), i.bill.freeBench.Named("BENCH")).Resolve();
                    s.reason = RowReason(i.row, n) + " " + "CC_Ahead_Why_Second".Translate(N(i.gives).Named("CAN"), N(i.row.useADay).Named("USE")).Resolve();
                    s.doLabel = "CC_Ahead_Do_Second".Translate().Resolve();
                    s.doTip = "CC_Ahead_Do_SecondTip".Translate(i.bill.freeBench.Named("BENCH"), i.bill.label.Named("PRODUCT"), i.bill.bench.Named("FROM")).Resolve();
                    s.look = new LookTargets(free);
                    s.cells = () => free.OccupiedRect().Cells.ToList();
                    var row = i.row;
                    s.apply = () =>
                    {
                        if (bill.DeletedOrDereferenced || free.Destroyed) return null;
                        var product = bill.recipe.ProducedThingDef;
                        var copy = Making.AddBill(free, bill.recipe, product, bill.targetCount, true);
                        if (copy == null) return null;
                        copy.repeatMode = bill.repeatMode;
                        copy.repeatCount = bill.repeatCount;
                        copy.ingredientSearchRadius = bill.ingredientSearchRadius;
                        copy.allowedSkillRange = bill.allowedSkillRange;
                        if (bill.ingredientFilter != null) copy.ingredientFilter?.CopyAllowancesFrom(bill.ingredientFilter);
                        return new SuggestDone { undo = new UndoBillsAdded { bills = { copy } }, check = StockCheck(row, free, copy) };
                    };
                    return s;
                }
                case "grow":
                {
                    if (!n.zones.TryGetValue(i.field.key, out var zone)) return null;
                    var plant = zone.GetPlantDefToGrow();
                    if (plant == null) return null;
                    var s = Base(i, n);
                    s.title = "CC_Ahead_Grow".Translate(zone.label.Named("ZONE"), i.field.cropLabel.Named("CROP"), i.cells.Named("CELLS")).Resolve();
                    s.reason = FieldReason(p) + " " + "CC_Ahead_Why_Grow".Translate(i.cells.Named("CELLS"), i.field.cropLabel.Named("CROP"), N(i.gives).Named("ADDS")).Resolve();
                    s.doLabel = "CC_Ahead_Do_Grow".Translate(i.cells).Resolve();
                    s.doTip = "CC_Ahead_Do_GrowTip".Translate(zone.label.Named("ZONE"), i.cells.Named("CELLS"), i.field.cropLabel.Named("CROP")).Resolve();
                    int count = i.cells;
                    s.cells = () => Sowing.Around(map, plant, zone, count);
                    s.look = new LookTargets(zone.Cells[0], map);
                    s.apply = () =>
                    {
                        if (zone.Map == null || !map.zoneManager.AllZones.Contains(zone)) return null;
                        var cells = Sowing.Around(map, plant, zone, count).Where(c => map.zoneManager.ZoneAt(c) == null).ToList();
                        if (cells.Count == 0) return null;
                        foreach (var c in cells) zone.AddCell(c);
                        return new SuggestDone { undo = new UndoZone { what = UndoZone.Added, zoneId = zone.ID, cells = cells }, check = new AheadSownCheck { zoneId = zone.ID, plant = plant, cells = cells } };
                    };
                    return s;
                }
                case "switch":
                {
                    if (!n.zones.TryGetValue(i.field.key, out var zone) || !n.plants.TryGetValue(i.crop.key, out var to)) return null;
                    var from = zone.GetPlantDefToGrow();
                    var s = Base(i, n);
                    bool whole = i.cells >= zone.Cells.Count;
                    s.title = (whole ? "CC_Ahead_SwitchAll" : "CC_Ahead_Switch").Translate(zone.label.Named("ZONE"), i.cells.Named("CELLS"), i.crop.label.Named("CROP"),
                        i.field.cropLabel.Named("OLD")).Resolve();
                    string over = i.field.food
                        ? "CC_Ahead_Why_OverFood".Translate(i.field.productLabel.Named("PRODUCT"), ((int)i.field.stock).Named("STOCK"), N(i.field.stockNutrition).Named("NUTR"),
                            N(p.cropNeed > 0f ? i.field.stockNutrition / p.cropNeed : AheadMath.Far).Named("DAYS"), i.field.stockCells.Named("SCELLS")).Resolve()
                        : i.field.useADay > 0f
                            ? "CC_Ahead_Why_OverUse".Translate(i.field.productLabel.Named("PRODUCT"), ((int)i.field.stock).Named("STOCK"), N(i.field.useADay).Named("USE"),
                                N(i.field.stock / i.field.useADay).Named("DAYS")).Resolve()
                            : "CC_Ahead_Why_OverUnused".Translate(i.field.productLabel.Named("PRODUCT"), ((int)i.field.stock).Named("STOCK"), N(n.measuredDays).Named("DAYS")).Resolve();
                    s.reason = FieldReason(p) + " " + over + " " + "CC_Ahead_Why_Switch".Translate(i.cells.Named("CELLS"), i.crop.label.Named("CROP"), N(i.gives).Named("ADDS")).Resolve();
                    s.doLabel = "CC_Ahead_Do_Switch".Translate(i.crop.label).Resolve();
                    s.doTip = "CC_Ahead_Do_SwitchTip".Translate(zone.label.Named("ZONE"), i.cells.Named("CELLS"), i.crop.label.Named("CROP"), i.field.cropLabel.Named("OLD")).Resolve();
                    int count = i.cells;
                    s.cells = () => SwitchCells(map, zone, from, count);
                    s.look = new LookTargets(zone.Cells[0], map);
                    s.apply = () =>
                    {
                        if (zone.Map == null || !map.zoneManager.AllZones.Contains(zone)) return null;
                        var cells = SwitchCells(map, zone, from, count);
                        if (cells.Count == 0) return null;
                        if (cells.Count >= zone.Cells.Count)
                        {
                            zone.SetPlantDefToGrow(to);
                            return new SuggestDone { undo = new UndoZone { what = UndoZone.Plant, zoneId = zone.ID, plant = from }, check = new AheadSownCheck { zoneId = zone.ID, plant = to, cells = cells } };
                        }
                        foreach (var c in cells) zone.RemoveCell(c);
                        var made = Sowing.MakeZone(map, to, cells);
                        if (made == null)
                        {
                            foreach (var c in cells) zone.AddCell(c);
                            return null;
                        }
                        return new SuggestDone
                        {
                            undo = new UndoZone { what = UndoZone.Moved, zoneId = made.ID, fromId = zone.ID, cells = cells }, check = new AheadSownCheck { zoneId = made.ID, plant = to, cells = cells }
                        };
                    };
                    return s;
                }
                case "new":
                {
                    if (!n.plants.TryGetValue(i.crop.key, out var plant)) return null;
                    var s = Base(i, n);
                    s.title = "CC_Ahead_New".Translate(i.cells.Named("CELLS"), i.crop.label.Named("CROP")).Resolve();
                    s.reason = FieldReason(p) + " " + "CC_Ahead_Why_New".Translate(i.cells.Named("CELLS"), i.crop.label.Named("CROP"), N(i.gives).Named("ADDS")).Resolve();
                    s.doLabel = "CC_Ahead_Do_New".Translate().Resolve();
                    s.doTip = "CC_Ahead_Do_NewTip".Translate(i.cells.Named("CELLS"), i.crop.label.Named("CROP")).Resolve();
                    int count = i.cells;
                    s.cells = () => Sowing.NewGround(map, plant, count);
                    s.apply = () =>
                    {
                        var zone = Sowing.MakeZone(map, plant, Sowing.NewGround(map, plant, count));
                        if (zone == null) return null;
                        return new SuggestDone
                        {
                            undo = new UndoZone { what = UndoZone.Made, zoneId = zone.ID, cells = zone.Cells.ToList() },
                            check = new AheadSownCheck { zoneId = zone.ID, plant = plant, cells = zone.Cells.ToList() }
                        };
                    };
                    return s;
                }
            }
            return null;
        }

        /// <summary>What the bills of a row ask for together, in pieces.</summary>
        private static string CoverReason(AheadRow r, ColonyNeeds n)
        {
            var own = n.bills.Where(b => b.kind == r.kind && !b.managed && !b.suspended).ToList();
            if (own.Count == 0) return "CC_Ahead_Why_NoBill".Translate().Resolve();
            if (own.Any(b => b.mode == AheadMath.Forever)) return "CC_Ahead_Why_Forever".Translate().Resolve();
            string list = own.Select(b => "CC_Ahead_BillUntil".Translate(b.label.Named("PRODUCT"), b.target.Named("N")).Resolve()).ToCommaList(true);
            string s = "CC_Ahead_Why_Cover".Translate(list).Resolve();
            if (r.managedBills > 0) s += " " + "CC_Ahead_Why_Managed".Translate(r.managedBills).Resolve();
            return s;
        }

        /// <summary>The cells of a zone to sow with another crop: a band across it from one end, so the new field is one piece.</summary>
        private static List<IntVec3> SwitchCells(Map map, Zone_Growing zone, ThingDef from, int count)
        {
            if (zone == null || count <= 0 || zone.Cells.Count == 0) return new List<IntVec3>();
            int w = zone.Cells.Max(c => c.x) - zone.Cells.Min(c => c.x), h = zone.Cells.Max(c => c.z) - zone.Cells.Min(c => c.z);
            var ordered = w >= h ? zone.Cells.OrderBy(c => c.x).ThenBy(c => c.z) : zone.Cells.OrderBy(c => c.z).ThenBy(c => c.x);
            return ordered.Take(count).ToList();
        }

        private static AheadStockCheck StockCheck(AheadRow row, Building_WorkTable bench, Bill_Production bill) => new AheadStockCheck
        {
            kind = row.kind, before = row.have, target = row.target, useADay = row.useADay, bench = bench, bill = bill,
            product = bill?.recipe?.ProducedThingDef
        };

        // ---------------- What is held up ----------------

        /// <summary>
        /// The bills behind a short or low row that are held up, and the food fields mostly unsown in the growing
        /// season for a reason that can be named: each a step of its own, with its fix where there is one.
        /// </summary>
        private static void Stuck(ColonyNeeds n, AheadPlan p, List<Suggestion> into)
        {
            int Have(ThingDef d) => d != null && n.stock.TryGetValue(d, out int c) ? c : 0;
            foreach (var row in p.rows)
            {
                if (row.status != AheadMath.Short && row.status != AheadMath.Low) continue;
                foreach (var b in n.bills.Where(b => b.kind == row.kind && !b.managed))
                {
                    if (!n.billOf.TryGetValue(b.key, out var at)) continue;
                    var (bench, bill) = at;
                    if (!bill.suspended && !Making.StillWanted(bill, Have)) continue;
                    var why = BillStuck.Why(bench, bill);
                    if (why == null) continue;
                    into.Add(StuckStep("ahead:stuck:" + why.key + ":" + b.key, row.kind, row.daysLeft, n.map,
                        "CC_Ahead_StuckTitle".Translate(b.label.Named("PRODUCT"), b.bench.Named("BENCH")).Resolve(), why, new LookTargets(bench), () => bench.OccupiedRect().Cells.ToList()));
                }
            }
            foreach (var f in n.fields)
            {
                if (!f.food || f.cells < AheadMath.MinCellsAStep || f.sown >= f.cells * 0.3f || !n.zones.TryGetValue(f.key, out var zone)) continue;
                var plant = zone.GetPlantDefToGrow();
                var why = FieldStuck.Why(n.map, zone, plant);
                if (why == null || why.key == "season" || why.key == "zonegone") continue;
                into.Add(StuckStep("ahead:fieldstuck:" + why.key + ":" + f.key, "fields", p.runsShortIn, n.map,
                    "CC_Ahead_FieldStuckTitle".Translate(zone.label.Named("ZONE"), f.cropLabel.Named("CROP"), f.sown.Named("SOWN"), f.cells.Named("CELLS")).Resolve(), why,
                    new LookTargets(zone.Cells[0], n.map), () => zone.Cells.ToList()));
            }
        }

        private static Suggestion StuckStep(string key, string area, float days, Map map, string title, StuckReason why, LookTargets look, Func<List<IntVec3>> cells) => new Suggestion
        {
            key = key, source = Source, area = area, days = days, map = map, title = title, reason = why.text, apply = why.fix, doLabel = why.fixLabel, doTip = why.fixTip,
            look = look, cells = cells
        };
    }

    /// <summary>Whether a bill step made the stock rise; if not, what holds the bill up (or that what is made is eaten as fast).</summary>
    public class AheadStockCheck : SuggestCheck
    {
        public string kind;
        public float before, target, useADay;
        public Building_WorkTable bench;
        public Bill_Production bill;
        public ThingDef product;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var needs = ColonyNeeds.For(done.map);
            float now = needs.HaveOf(kind);
            // What the bench made since, by the record, in the row's measure.
            float made = 0f;
            if (bench != null && product != null && needs.record != null && needs.record.made.TryGetValue(bench.thingIDNumber, out var byBench) && byBench.TryGetValue(product, out float aDay))
            {
                float days = Mathf.Min((Find.TickManager.TicksGame - done.tick) / (float)SuggestMath.TicksPerDay, needs.record.days);
                float unit = kind == AheadMath.Medicine ? 1f : Mathf.Max(0.01f, product.GetStatValueAbstract(StatDefOf.Nutrition));
                made = aDay * days * unit;
            }
            if (AheadMath.StockStepWorked(before, now, target, made, useADay)) return true;
            bool gone = bill == null || bill.DeletedOrDereferenced;
            whyNot = gone ? "CC_Ahead_NotYet_BillGone".Translate().Resolve()
                : BillStuck.Why(bench, bill)?.text ?? (made > 0f ? "CC_Ahead_NotYet_Eaten".Translate().Resolve() : "CC_Ahead_NotYet_Nothing".Translate().Resolve());
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && bill != null && bill.DeletedOrDereferenced) bill = null;
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref before, "before");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref useADay, "useADay");
            Scribe_References.Look(ref bench, "bench");
            Scribe_References.Look(ref bill, "bill");
            CouncilScribe.LookDef(ref product, "product");
        }
    }

    /// <summary>Whether the cells a field step added are sown, two days on; if not, why (the season, the sowing switched off, nobody to sow).</summary>
    public class AheadSownCheck : SuggestCheck
    {
        public int zoneId = -1;
        public ThingDef plant;
        public List<IntVec3> cells = new List<IntVec3>();

        public AheadSownCheck()
        {
            waitTicks = 2 * SuggestMath.TicksPerDay;
        }

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var map = done.map;
            var zone = map?.zoneManager.AllZones.OfType<Zone_Growing>().FirstOrDefault(z => z.ID == zoneId);
            int sown = cells.Count(c => c.InBounds(map) && c.GetPlant(map)?.def == plant);
            if (AheadMath.SownWorked(cells.Count, sown)) return true;
            whyNot = FieldStuck.Why(map, zone, plant)?.text ?? "CC_Ahead_NotYet_Sown".Translate(sown, cells.Count).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            CouncilScribe.LookDef(ref plant, "plant");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }
    }

    /// <summary>Whether a fix cleared what held a bill up, half a day on; if not, what holds it up now.</summary>
    public class AheadGoneCheck : SuggestCheck
    {
        public Building_WorkTable bench;
        public Bill_Production bill;
        public string reason;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            if (bill == null || bill.DeletedOrDereferenced || bench == null || !bench.Spawned) return true;
            var why = BillStuck.Why(bench, bill);
            if (why == null || why.key != reason) return true;
            whyNot = why.text;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && bill != null && bill.DeletedOrDereferenced) bill = null;
            Scribe_References.Look(ref bench, "bench");
            Scribe_References.Look(ref bill, "bill");
            Scribe_Values.Look(ref reason, "reason");
        }
    }
}
