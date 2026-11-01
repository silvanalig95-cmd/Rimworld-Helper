using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What holds a bill or a field up, in words, with the step that clears it when there is one.</summary>
    public class StuckReason
    {
        /// <summary>suspended, forbidden, broken, power, fuel, worker, nobody, workoff, missing, radius, storage; nosow, season, nogrower, growoff.</summary>
        public string key;
        public string text;
        /// <summary>The step that clears it (with its undo and check), its button's words and what it does; null when there is none.</summary>
        public Func<SuggestDone> fix;
        public string fixLabel, fixTip;
    }

    /// <summary>
    /// Why a bill isn't moving, looked at in this order: it is suspended; its bench is forbidden, broken down, without
    /// power or out of fuel; the one it is kept for is gone; nobody can do it (the work, the skill it needs, the bill's
    /// own skill range); nobody who can has the work switched on; none of its ingredients is in stock; they are, but
    /// not within the bill's radius; the store it goes to has no room. Each with the step that clears it, where one
    /// exists: resume, allow, let someone do the work, the bill for an ingredient a bench here makes, the whole map's
    /// radius, anyone may do it. Used for the Ahead section's "why it's stuck", and to say why a step didn't work.
    /// </summary>
    public static class BillStuck
    {
        public static StuckReason Why(Building_WorkTable bench, Bill_Production bill)
        {
            if (bench == null || bill?.recipe == null || bill.DeletedOrDereferenced || !bench.Spawned) return null;
            var map = bench.Map;
            string product = (bill.recipe.ProducedThingDef ?? bill.recipe.products?.FirstOrDefault()?.thingDef)?.label ?? bill.recipe.label;
            string benchName = bench.LabelCap.ToString();
            if (bill.suspended)
                return new StuckReason
                {
                    key = "suspended", text = "CC_Ahead_Stuck_Suspended".Translate(product, benchName).Resolve(), fixLabel = "CC_Ahead_Fix_Resume".Translate().Resolve(),
                    fixTip = "CC_Ahead_Fix_ResumeTip".Translate(benchName).Resolve(),
                    fix = () =>
                    {
                        bill.suspended = false;
                        return Done(new UndoBillSetting { bill = bill, what = UndoBillSetting.Suspend }, bench, bill, "suspended");
                    }
                };
            var player = Faction.OfPlayerSilentFail;
            if (player != null && bench.IsForbidden(player))
                return new StuckReason
                {
                    key = "forbidden", text = "CC_Ahead_Stuck_Forbidden".Translate(benchName).Resolve(), fixLabel = "CC_Ahead_Fix_Allow".Translate().Resolve(),
                    fixTip = "CC_Ahead_Fix_AllowTip".Translate(benchName).Resolve(),
                    fix = () =>
                    {
                        bench.SetForbidden(false, false);
                        return Done(new UndoForbid { thing = bench, wasForbidden = true }, bench, bill, "forbidden");
                    }
                };
            var breakdown = bench.GetComp<CompBreakdownable>();
            if (breakdown != null && breakdown.BrokenDown) return new StuckReason { key = "broken", text = "CC_Ahead_Stuck_Broken".Translate(benchName).Resolve() };
            var power = bench.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return new StuckReason { key = "power", text = "CC_Ahead_Stuck_Power".Translate(benchName).Resolve() };
            var fuel = bench.GetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel)
            {
                string fuelName = fuel.Props.fuelFilter?.AnyAllowedDef?.label ?? "fuel";
                int fuelHave = fuel.Props.fuelFilter == null ? 0 : map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                    .Where(t => t.Spawned && fuel.Props.fuelFilter.Allows(t) && (player == null || !t.IsForbidden(player))).Sum(t => t.stackCount);
                return new StuckReason { key = "fuel", text = "CC_Ahead_Stuck_Fuel".Translate(benchName, fuelName, fuelHave).Resolve() };
            }

            // Who could do it.
            var work = ColonyNeeds.WorkTypeFor(bench, bill.recipe);
            string workName = work?.labelShort ?? work?.label ?? bill.recipe.label;
            var restricted = bill.PawnRestriction;
            if (restricted != null && (restricted.Dead || !restricted.Spawned || restricted.Map != map || !ColonyNeeds.Able(restricted, bill, work)))
                return new StuckReason
                {
                    key = "worker", text = "CC_Ahead_Stuck_Worker".Translate(restricted.LabelShort).Resolve(), fixLabel = "CC_Ahead_Fix_Anyone".Translate().Resolve(),
                    fixTip = "CC_Ahead_Fix_AnyoneTip".Translate(restricted.LabelShort).Resolve(),
                    fix = () =>
                    {
                        bill.SetAnyPawnRestriction();
                        return Done(new UndoBillSetting { bill = bill, what = UndoBillSetting.Worker, pawn = restricted }, bench, bill, "worker");
                    }
                };
            var people = ColonyNeeds.Hands(map).Where(p => !p.Dead && p.workSettings != null).ToList();
            var able = people.Where(p => ColonyNeeds.Able(p, bill, work)).ToList();
            if (able.Count == 0)
            {
                string needs = bill.recipe.skillRequirements != null && bill.recipe.skillRequirements.Count > 0
                    ? string.Join(", ", bill.recipe.skillRequirements.Where(r => r?.skill != null).Select(r => r.skill.label + " " + r.minLevel))
                    : bill.recipe.workSkill?.label ?? workName;
                return new StuckReason { key = "nobody", text = "CC_Ahead_Stuck_Nobody".Translate(workName, needs).Resolve() };
            }
            if (work != null && !able.Any(p => p.workSettings.WorkIsActive(work)))
            {
                var best = able.OrderByDescending(p => bill.recipe.workSkill != null ? p.skills?.GetSkill(bill.recipe.workSkill)?.Level ?? 0 : 0).ThenBy(p => p.thingIDNumber).First();
                return LetDo(best, work, "CC_Ahead_Stuck_WorkOff".Translate(workName).Resolve(), "workoff", bench, bill);
            }

            // What it is made from.
            var stock = Making.Stock(map);
            var missing = new List<string>();
            bool outOfReach = false;
            foreach (var ing in bill.recipe.ingredients ?? new List<IngredientCount>())
            {
                if (ing?.filter == null) continue;
                var allowed = ing.filter.AllowedThingDefs.Where(d => d != null && bill.recipe.IsIngredient(d) && (bill.ingredientFilter == null || bill.ingredientFilter.Allows(d))).ToList();
                if (allowed.Count == 0) continue;
                if (!allowed.Any(d => stock.TryGetValue(d, out int n) && n >= ing.CountRequiredOfFor(d, bill.recipe, null)))
                {
                    missing.Add(ing.filter.Summary);
                    continue;
                }
                if (bill.ingredientSearchRadius < Bill.MaxIngredientSearchRadius)
                {
                    float r2 = bill.ingredientSearchRadius * bill.ingredientSearchRadius;
                    var at = bench.InteractionCell.IsValid ? bench.InteractionCell : bench.Position;
                    bool near = allowed.Any(d => map.listerThings.ThingsOfDef(d).Where(t => t.Spawned && (at - t.Position).LengthHorizontalSquared <= r2 && (player == null || !t.IsForbidden(player)))
                                                    .Sum(t => t.stackCount) >= ing.CountRequiredOfFor(d, bill.recipe, null));
                    if (!near) outOfReach = true;
                }
            }
            if (missing.Count > 0)
            {
                var starved = Making.For(map).starved.FirstOrDefault(s => s.bill == bill);
                var fix = starved?.fix;
                var reason = new StuckReason { key = "missing", text = "CC_Ahead_Stuck_Missing".Translate(string.Join(", ", missing)).Resolve() };
                if (fix != null && !fix.billed && fix.bench != null)
                {
                    reason.text += " " + "CC_Ahead_Stuck_MissingFix".Translate(fix.product.label, fix.bench.LabelCap.ToString()).Resolve();
                    reason.fixLabel = "CC_Making_SetBillFor".Translate(fix.product.label).Resolve();
                    reason.fixTip = "CC_Ahead_Fix_FeedTip".Translate(fix.product.label, fix.bench.LabelCap.ToString(), fix.target).Resolve();
                    reason.fix = () =>
                    {
                        var added = Making.AddBill(fix.bench, fix.recipe, fix.product, fix.target, true);
                        if (added == null) return null;
                        return Done(new UndoBillsAdded { bills = { added } }, bench, bill, "missing");
                    };
                }
                else reason.text += " " + "CC_Making_StarvedNoFix".Translate().Resolve();
                return reason;
            }
            if (outOfReach)
            {
                float was = bill.ingredientSearchRadius;
                return new StuckReason
                {
                    key = "radius", text = "CC_Ahead_Stuck_Radius".Translate(Mathf.RoundToInt(was)).Resolve(), fixLabel = "CC_Ahead_Fix_Radius".Translate().Resolve(),
                    fixTip = "CC_Ahead_Fix_RadiusTip".Translate(Mathf.RoundToInt(was)).Resolve(),
                    fix = () =>
                    {
                        bill.ingredientSearchRadius = Bill.MaxIngredientSearchRadius;
                        return Done(new UndoBillSetting { bill = bill, what = UndoBillSetting.Radius, radius = was }, bench, bill, "radius");
                    }
                };
            }

            // Where it goes.
            var productDef = bill.recipe.ProducedThingDef;
            if (productDef != null && !RoomFor(map, bill, productDef))
                return new StuckReason { key = "storage", text = "CC_Ahead_Stuck_Storage".Translate(productDef.label).Resolve() };
            return null;
        }

        /// <summary>The step that lets a colonist do a kind of work (priority 3, or a tick without priorities); Better Pawn Control may set it again when its presets switch.</summary>
        internal static StuckReason LetDo(Pawn best, WorkTypeDef work, string text, string key, Building_WorkTable bench, Bill_Production bill, Func<SuggestCheck> check = null)
        {
            string workName = work.labelShort ?? work.label;
            return new StuckReason
            {
                key = key, text = text + " " + "CC_Ahead_Stuck_CouldDo".Translate(best.LabelShort, workName).Resolve(),
                fixLabel = "CC_Ahead_Fix_LetDo".Translate(best.LabelShort, workName).Resolve(), fixTip = "CC_Ahead_Fix_LetDoTip".Translate(best.LabelShort, workName).Resolve(),
                fix = () =>
                {
                    if (best.Dead || best.workSettings == null || best.WorkTypeIsDisabled(work)) return null;
                    int was = best.workSettings.GetPriority(work);
                    best.workSettings.SetPriority(work, 3);
                    var undo = new UndoWorkPriority { pawn = best, work = work, priority = was };
                    if (bench != null) return Done(undo, bench, bill, key);
                    return new SuggestDone { undo = undo, check = check?.Invoke(), map = best.Map };
                }
            };
        }

        /// <summary>A fix done: its undo, and a look in half a day whether the bill is still held up by the same thing.</summary>
        private static SuggestDone Done(SuggestUndo undo, Building_WorkTable bench, Bill_Production bill, string reason) =>
            new SuggestDone { undo = undo, map = bench.Map, check = new AheadGoneCheck { bench = bench, bill = bill, reason = reason, waitTicks = SuggestMath.TicksPerDay / 2 } };

        /// <summary>Whether the bill's product has a place to go: its own store with room, any store that takes it with room, or dropped by the bench.</summary>
        private static bool RoomFor(Map map, Bill_Production bill, ThingDef product)
        {
            var mode = bill.GetStoreMode();
            if (mode == BillStoreModeDefOf.DropOnFloor) return true;
            IEnumerable<SlotGroup> groups;
            if (mode == BillStoreModeDefOf.SpecificStockpile)
                groups = bill.GetSlotGroup() is SlotGroup g ? new[] { g } : new SlotGroup[0];
            else groups = map.haulDestinationManager.AllGroupsListForReading;
            int stack = Mathf.Max(1, product.stackLimit);
            foreach (var g in groups)
            {
                if (g?.Settings == null || !g.Settings.AllowedToAccept(product)) continue;
                foreach (var c in g.CellsList)
                {
                    if (c.GetItemCount(map) < c.GetMaxItemsAllowedInCell(map)) return true;
                    foreach (var t in c.GetThingList(map))
                        if (t.def == product && t.stackCount < stack) return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Why a field isn't sown: its sowing is switched off, it is out of the growing season, nobody can sow the crop
    /// (the growing work, the skill it needs), or nobody who can has the work switched on. Each with the step that
    /// clears it where one exists.
    /// </summary>
    public static class FieldStuck
    {
        public static StuckReason Why(Map map, Zone_Growing zone, ThingDef plant)
        {
            if (map == null || plant?.plant == null) return null;
            if (zone == null || zone.Cells.Count == 0) return new StuckReason { key = "zonegone", text = "CC_Ahead_Stuck_ZoneGone".Translate().Resolve() };
            string zoneName = zone.label;
            if (!zone.allowSow)
                return new StuckReason
                {
                    key = "nosow", text = "CC_Ahead_Stuck_NoSow".Translate(zoneName).Resolve(), fixLabel = "CC_Ahead_Fix_Sow".Translate().Resolve(),
                    fixTip = "CC_Ahead_Fix_SowTip".Translate(zoneName).Resolve(),
                    fix = () =>
                    {
                        zone.allowSow = true;
                        return new SuggestDone { map = map, undo = new UndoZone { what = UndoZone.Sowing, zoneId = zone.ID }, check = new AheadSownCheck { zoneId = zone.ID, plant = plant, cells = zone.Cells.Take(AheadMath.MaxCellsAStep).ToList() } };
                    }
                };
            bool season;
            try
            {
                season = PlantUtility.GrowthSeasonNow(zone.Cells[0], map, plant);
            }
            catch
            {
                season = true;
            }
            if (!season) return new StuckReason { key = "season", text = "CC_Ahead_Stuck_Season".Translate(plant.label).Resolve() };
            var work = WorkTypeDefOf.Growing;
            var people = ColonyNeeds.Hands(map).Where(p => !p.Dead && !p.Downed && p.workSettings != null).ToList();
            var able = people.Where(p => !p.WorkTypeIsDisabled(work) && (p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0) >= plant.plant.sowMinSkill).ToList();
            if (able.Count == 0) return new StuckReason { key = "nogrower", text = "CC_Ahead_Stuck_NoGrower".Translate(plant.label, plant.plant.sowMinSkill).Resolve() };
            if (!able.Any(p => p.workSettings.WorkIsActive(work)))
            {
                var best = able.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).ThenBy(p => p.thingIDNumber).First();
                return BillStuck.LetDo(best, work, "CC_Ahead_Stuck_GrowOff".Translate().Resolve(), "growoff", null, null,
                    () => new AheadSownCheck { zoneId = zone.ID, plant = plant, cells = zone.Cells.Take(AheadMath.MaxCellsAStep).ToList() });
            }
            return null;
        }
    }
}
