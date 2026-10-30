using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's automations: what it would otherwise only suggest, done for the player each hour, each one its own
    /// switch in the settings (all off unless switched on), each action told in a quiet message and kept in a short log
    /// (the check-myself page). Never during a fight on the map, and never over the player's own orders: a bill the
    /// player removes isn't set again for a fortnight, and whatever the council does (a bill, a step in the plan, a
    /// research pick, a stockpile, a take-down order, a blueprint, a chopping mark) is undone the ordinary way. Rules in
    /// <see cref="AutomationMath"/>.
    /// </summary>
    public class AutomationTracker : IExposable
    {
        /// <summary>The bills the council set, as bench id and recipe: gone from the bench, the player removed it.</summary>
        public List<string> billKeys = new List<string>();
        /// <summary>Bills the player removed, and when: not set again for a fortnight.</summary>
        public List<string> droppedKeys = new List<string>();
        public List<int> droppedTicks = new List<int>();
        /// <summary>When each kind of thing was last done ("research", "unstored", "takedown:stubs", "Council_Adv_BedsDark", "firewood").</summary>
        public List<string> doneKeys = new List<string>();
        public List<int> doneTicks = new List<int>();
        /// <summary>What the council did, newest last, for the check-myself page.</summary>
        public List<string> log = new List<string>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;

        public static bool Any => S != null && (S.autoBills || S.autoPlan || S.autoResearch || S.autoStockpile || S.autoTakeDown || S.autoLamps || S.autoFirewood);

        /// <summary>Each switch, for the check-myself page: its setting key and whether it is on.</summary>
        public static IEnumerable<(string key, bool on)> Switches()
        {
            var s = S;
            if (s == null) yield break;
            yield return ("autoBills", s.autoBills);
            yield return ("autoPlan", s.autoPlan);
            yield return ("autoResearch", s.autoResearch);
            yield return ("autoStockpile", s.autoStockpile);
            yield return ("autoTakeDown", s.autoTakeDown);
            yield return ("autoLamps", s.autoLamps);
            yield return ("autoFirewood", s.autoFirewood);
        }

        public void Hourly(CouncilGameComponent comp)
        {
            if (!Any) return;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                // Never while there's fighting to be done.
                if (MapFacts.ThreatOn(map) || map.mapPawns.FreeColonistsSpawnedCount == 0) continue;
                if (S.autoBills) Try("bills", () => Bills(map));
                if (S.autoPlan && NextSteps.On) Try("plan", () => Plan(map));
                if (S.autoResearch) Try("research", () => Research(map));
                if (S.autoStockpile) Try("stockpile", () => Stockpile(map));
                if (S.autoTakeDown) Try("take down", () => TakeDown(map));
                if (S.autoLamps && S.enableAdvisor) Try("lamps", () => LampsAndChairs(map));
                if (S.autoFirewood && S.enableAdvisor) Try("firewood", () => Firewood(map));
            }
        }

        private static void Try(string what, Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("automation " + what, e);
            }
        }

        // ---------------- The memory ----------------

        private int LastDone(string key)
        {
            int i = doneKeys.IndexOf(key);
            return i >= 0 ? doneTicks[i] : -1;
        }

        private void Done(string key)
        {
            int i = doneKeys.IndexOf(key);
            if (i >= 0) doneTicks[i] = Now;
            else
            {
                doneKeys.Add(key);
                doneTicks.Add(Now);
            }
        }

        private bool Due(string key, int days) => AutomationMath.Due(LastDone(key), Now, days * AutomationMath.TicksPerDay);

        private bool Dropped(string key)
        {
            int i = droppedKeys.IndexOf(key);
            if (i < 0) return false;
            if (AutomationMath.StillDropped(droppedTicks[i], Now)) return true;
            droppedKeys.RemoveAt(i);
            droppedTicks.RemoveAt(i);
            return false;
        }

        /// <summary>Told quietly, and kept for the check-myself page.</summary>
        private void Note(string text, LookTargets look = null)
        {
            if (text.NullOrEmpty()) return;
            log.Add(GenDate.DaysPassed + ": " + text);
            while (log.Count > AutomationMath.LogLines) log.RemoveAt(0);
            Messages.Message(text, look ?? LookTargets.Invalid, MessageTypeDefOf.SilentInput, false);
        }

        // ---------------- Bills ----------------

        private static string BillKey(Building_WorkTable bench, RecipeDef recipe) => bench.thingIDNumber + ":" + recipe.defName;

        /// <summary>
        /// Bills for the staples the colony lacks and can make (meals, medicine, components), and for whatever the
        /// building and the bills want (Making.needs): a "make until" bill, a couple an hour at most. A bill of the
        /// council's that the player removed isn't set again for a fortnight.
        /// </summary>
        private void Bills(Map map)
        {
            var r = Making.For(map);
            // What the council set and the player has since taken off the bench.
            for (int i = billKeys.Count - 1; i >= 0; i--)
            {
                var parts = billKeys[i].Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int id)) { billKeys.RemoveAt(i); continue; }
                var bench = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b => b.thingIDNumber == id);
                if (bench == null) continue;
                if (bench.BillStack != null && bench.BillStack.Bills.Any(b => b.recipe?.defName == parts[1])) continue;
                droppedKeys.Add(billKeys[i]);
                droppedTicks.Add(Now);
                billKeys.RemoveAt(i);
            }
            int set = 0;
            // What the building and the bills want first, then the staples (a bill set refreshes the page's lists, so copies are walked).
            foreach (var c in r.needs.ToList())
            {
                if (set >= AutomationMath.BillsAnHour) return;
                if (!c.canMake || c.billed || c.bench == null || Dropped(BillKey(c.bench, c.recipe))) continue;
                Making.SetUpChain(c, true);
                billKeys.Add(BillKey(c.bench, c.recipe));
                Note("CC_Auto_BillSet".Translate(c.product.label.Named("PRODUCT"), c.target.Named("N"), c.bench.LabelCap.ToString().Named("BENCH"), c.why.Named("WHY")).Resolve(), new LookTargets(c.bench));
                set++;
            }
            foreach (var o in r.could.ToList())
            {
                if (set >= AutomationMath.BillsAnHour) return;
                if (o.bench == null || !AutomationMath.AutoKind(o.kind, o.wantedBy != null) || Dropped(BillKey(o.bench, o.recipe))) continue;
                if (o.kind == "feed" && RoomUtil.ColonyAnimalCount(map) == 0) continue;
                Making.SetUpBill(o, true);
                billKeys.Add(BillKey(o.bench, o.recipe));
                Note("CC_Auto_BillSet".Translate(o.product.label.Named("PRODUCT"), o.target.Named("N"), o.bench.LabelCap.ToString().Named("BENCH"),
                    (o.wantedBy ?? "CC_Auto_Why_Staple".Translate().Resolve()).Named("WHY")).Resolve(), new LookTargets(o.bench));
                set++;
            }
        }

        // ---------------- The plan ----------------

        /// <summary>Proposed steps at a high level or above go straight into the plan.</summary>
        private void Plan(Map map)
        {
            foreach (var s in NextSteps.ProposedFor(map).ToList())
            {
                if (NextStepsMath.LevelOf(s.score) < AutomationMath.PlanLevel) continue;
                NextSteps.Take(s);
                Note("CC_Auto_Planned".Translate(s.title.Named("STEP")).Resolve(), s.look.IsValid ? new LookTargets(s.look, map) : null);
            }
        }

        // ---------------- Research ----------------

        /// <summary>An idle research bench takes up the cheapest project of the colony's own era.</summary>
        private void Research(Map map)
        {
            if (Find.ResearchManager.GetProject() != null || !map.listerBuildings.ColonistsHaveResearchBench() || !Due("research", AutomationMath.ResearchDays)) return;
            var next = AdvisorWorker_Research.SuggestNext();
            if (next == null) return;
            Find.ResearchManager.SetCurrentProject(next);
            Done("research");
            Note("CC_Auto_Research".Translate(next.LabelCap.ToString().Named("PROJECT")).Resolve());
        }

        // ---------------- Stores and the base ----------------

        /// <summary>Goods nobody stores get the stockpile the stores review would lay out, every few days at most.</summary>
        private void Stockpile(Map map)
        {
            var f = BaseReview.Finding(map, "unstored");
            if (f?.fix == null || f.Level < AdviceLevel.Medium || !Due("unstored", AutomationMath.StockpileDays)) return;
            f.fix();
            Done("unstored");
            Note("CC_Auto_Stockpile".Translate(f.title.Named("WHAT")).Resolve(), f.look.IsValid ? new LookTargets(f.look, map) : null);
        }

        private static readonly string[] TakeDownKeys = { "stubs", "conduits", "orphanconduits", "doors" };

        /// <summary>What the base review would take down (wall stubs, dead conduit and pipe ends, doors between the same room) gets its deconstruct orders.</summary>
        private void TakeDown(Map map)
        {
            foreach (var f in BaseReview.For(map).ToList())
            {
                if (f.fix == null || f.things.NullOrEmpty()) continue;
                if (!TakeDownKeys.Contains(f.key) && !f.key.StartsWith("pipes:")) continue;
                if (!Due("takedown:" + f.key, AutomationMath.TakeDownDays)) continue;
                f.fix();
                Done("takedown:" + f.key);
                Note("CC_Auto_TakeDown".Translate(f.title.Named("WHAT"), f.things.Count.Named("N")).Resolve(), f.look.IsValid ? new LookTargets(f.look, map) : null);
            }
        }

        // ---------------- The advisor's own hands ----------------

        private static readonly string[] LampSignals = { "Council_Adv_BedsDark", "Council_Adv_NoSeat" };

        /// <summary>Dark bedrooms get their lamps and benches their chairs planned, as the advisor's button would, once a day at most.</summary>
        private void LampsAndChairs(Map map)
        {
            var signals = CouncilMapComponent.For(map)?.Signals;
            if (signals == null) return;
            foreach (var s in signals.ToList())
            {
                if (s.def == null || s.act == null || !LampSignals.Contains(s.def.defName) || !Due(s.def.defName, AutomationMath.LampsDays)) continue;
                s.act();
                Done(s.def.defName);
                Note("CC_Auto_Acted".Translate(s.title.Named("WHAT")).Resolve(), s.look);
            }
        }

        /// <summary>Firewood short before the cold: trees near the base are marked for chopping, as many as the advice is loud, once a day at most.</summary>
        private void Firewood(Map map)
        {
            var s = CouncilMapComponent.For(map)?.Signals?.FirstOrDefault(x => x.def?.defName == "Council_Adv_Firewood");
            if (s == null || !Due("firewood", AutomationMath.FirewoodDays)) return;
            int want = AutomationMath.TreesToMark((int)s.Level);
            var woods = new HashSet<ThingDef>(Goal_Woodpile.FirewoodDefs);
            var centre = TemplatePlanner.ColonyCenter(map);
            var player = Faction.OfPlayer;
            var dm = map.designationManager;
            var trees = new List<Plant>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant))
            {
                if (!(t is Plant p) || p.def.plant == null || !p.def.plant.IsTree || !p.HarvestableNow || p.def.plant.harvestedThingDef == null || !woods.Contains(p.def.plant.harvestedThingDef)) continue;
                if (p.IsForbidden(player) || p.Position.Fogged(map) || dm.DesignationOn(p) != null) continue;
                if (map.zoneManager.ZoneAt(p.Position) != null || !map.reachability.CanReachColony(p.Position)) continue;
                if (p.Position.DistanceTo(centre) > 70f) continue;
                trees.Add(p);
            }
            if (trees.Count == 0) return;
            int marked = 0;
            foreach (var p in trees.OrderBy(p => p.Position.DistanceToSquared(centre)).Take(want))
            {
                dm.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
                marked++;
            }
            if (marked == 0) return;
            Done("firewood");
            Note("CC_Auto_Firewood".Translate(marked.Named("N")).Resolve(), new LookTargets(trees[0]));
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref billKeys, "billKeys", LookMode.Value);
            Scribe_Collections.Look(ref droppedKeys, "droppedKeys", LookMode.Value);
            Scribe_Collections.Look(ref droppedTicks, "droppedTicks", LookMode.Value);
            Scribe_Collections.Look(ref doneKeys, "doneKeys", LookMode.Value);
            Scribe_Collections.Look(ref doneTicks, "doneTicks", LookMode.Value);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                billKeys ??= new List<string>();
                droppedKeys ??= new List<string>();
                droppedTicks ??= new List<int>();
                doneKeys ??= new List<string>();
                doneTicks ??= new List<int>();
                log ??= new List<string>();
                while (droppedTicks.Count < droppedKeys.Count) droppedTicks.Add(0);
                while (droppedTicks.Count > droppedKeys.Count) droppedTicks.RemoveAt(droppedTicks.Count - 1);
                while (doneTicks.Count < doneKeys.Count) doneTicks.Add(0);
                while (doneTicks.Count > doneKeys.Count) doneTicks.RemoveAt(doneTicks.Count - 1);
                while (log.Count > AutomationMath.LogLines) log.RemoveAt(0);
            }
        }
    }
}
