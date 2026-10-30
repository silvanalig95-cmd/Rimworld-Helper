using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing the council did on its own, with what it touched, so it can be undone at a click.</summary>
    public class AutoAction : IExposable
    {
        public const string Bill = "bill", Plan = "plan", Research = "research", Stockpile = "stockpile", TakeDown = "takedown", Lamps = "lamps", Firewood = "firewood", Grow = "grow";

        public string kind;
        public int tick;
        public string text;
        public Map map;
        /// <summary>The bench of a bill, the things ordered down, the blueprints placed, the trees marked.</summary>
        public List<Thing> things = new List<Thing>();
        public string recipe;
        public string stepKey;
        public ResearchProjectDef prev;
        public int zoneId = -1;
        /// <summary>Cells added to a zone that was there already (grow), to take back.</summary>
        public List<IntVec3> cells = new List<IntVec3>();
        public bool undone;

        public bool CanUndo => !undone && map != null;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Values.Look(ref recipe, "recipe");
            Scribe_Values.Look(ref stepKey, "stepKey");
            CouncilScribe.LookDef(ref prev, "prev");
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Values.Look(ref undone, "undone");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                things ??= new List<Thing>();
                things.RemoveAll(t => t == null);
                cells ??= new List<IntVec3>();
                kind ??= Bill;
                text ??= "";
            }
        }
    }

    /// <summary>Something an automation would do now: its switch, what it is, and the doing of it.</summary>
    public class AutoPending
    {
        public string key;
        public string text;
        public Action act;
    }

    /// <summary>
    /// The council's automations: what it would otherwise only suggest, done for the player each hour, each one its own
    /// switch in the settings (all off unless switched on), each action told in a quiet message, kept in a short log and
    /// as an action that can be undone at a click (the Making page, the check-myself page). Never during a fight on the
    /// map, and never over the player's own orders: a bill the player removes isn't set again for a fortnight, and
    /// whatever the council does (a bill, a step in the plan, a research pick, a stockpile, a growing zone, a take-down
    /// order, a blueprint, a chopping mark) is undone the ordinary way as well. A materials budget bounds the blueprints
    /// placed in a day, daily caps bound the take-down orders and the chopping marks. Every automation can say what it
    /// would do now without doing it (<see cref="Preview"/>). Rules in <see cref="AutomationMath"/>.
    /// </summary>
    public class AutomationTracker : IExposable
    {
        /// <summary>The bills the council set, as bench id and recipe: gone from the bench, the player removed it.</summary>
        public List<string> billKeys = new List<string>();
        /// <summary>Bills the player removed, and when: not set again for a fortnight.</summary>
        public List<string> droppedKeys = new List<string>();
        public List<int> droppedTicks = new List<int>();
        /// <summary>When each kind of thing was last done ("research", "unstored", "takedown:stubs", "Council_Adv_BedsDark", "firewood", "grow:Cloth").</summary>
        public List<string> doneKeys = new List<string>();
        public List<int> doneTicks = new List<int>();
        /// <summary>What the council did, newest last, for the check-myself page.</summary>
        public List<string> log = new List<string>();
        /// <summary>The last actions, newest last, each undoable at a click.</summary>
        public List<AutoAction> actions = new List<AutoAction>();
        /// <summary>The day's spending and counts against the budget and the caps.</summary>
        public int spendDay = -1, takeDownDay = -1, chopDay = -1;
        public float spentToday;
        public int takenDownToday, choppedToday;

        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;

        public static bool Any => S != null && (S.autoBills || S.autoPlan || S.autoResearch || S.autoStockpile || S.autoTakeDown || S.autoLamps || S.autoFirewood || S.autoGrow);

        /// <summary>Each switch, for the pages: its setting key and whether it is on.</summary>
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
            yield return ("autoGrow", s.autoGrow);
        }

        public static bool On(string key)
        {
            foreach (var (k, on) in Switches())
                if (k == key) return on;
            return false;
        }

        public void Hourly(CouncilGameComponent comp)
        {
            if (!Any) return;
            NewDay();
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                // Never while there's fighting to be done.
                if (MapFacts.ThreatOn(map) || map.mapPawns.FreeColonistsSpawnedCount == 0) continue;
                foreach (var p in Candidates(map, true))
                {
                    try
                    {
                        p.act?.Invoke();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("automation " + p.key, e);
                    }
                }
            }
        }

        private void NewDay()
        {
            int today = GenDate.DaysPassed;
            if (spendDay != today)
            {
                spendDay = today;
                spentToday = 0f;
            }
            if (takeDownDay != today)
            {
                takeDownDay = today;
                takenDownToday = 0;
            }
            if (chopDay != today)
            {
                chopDay = today;
                choppedToday = 0;
            }
        }

        /// <summary>What each automation would do now, for the pages: its switch, whether it is on, and the lines.</summary>
        public List<(string key, bool on, List<string> texts)> Preview(Map map)
        {
            NewDay();
            var result = new List<(string, bool, List<string>)>();
            if (map == null) return result;
            List<AutoPending> all;
            try
            {
                all = Candidates(map, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("automations preview", e);
                all = new List<AutoPending>();
            }
            foreach (var (key, on) in Switches())
                result.Add((key, on, all.Where(p => p.key == key).Select(p => p.text).ToList()));
            return result;
        }

        /// <summary>Everything the automations would do now on this map (only those switched on, or all of them), each with its doing.</summary>
        private List<AutoPending> Candidates(Map map, bool onlyOn)
        {
            var list = new List<AutoPending>();
            var s = S;
            if (s == null) return list;
            void Gather(string key, bool on, Func<Map, IEnumerable<AutoPending>> f)
            {
                if (onlyOn && !on) return;
                try
                {
                    foreach (var p in f(map))
                    {
                        p.key = key;
                        list.Add(p);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("automation " + key, e);
                }
            }
            Gather("autoBills", s.autoBills, Bills);
            Gather("autoPlan", s.autoPlan && NextSteps.On, Plan);
            Gather("autoResearch", s.autoResearch, Research);
            Gather("autoStockpile", s.autoStockpile, Stockpile);
            Gather("autoTakeDown", s.autoTakeDown, TakeDown);
            Gather("autoLamps", s.autoLamps && s.enableAdvisor, LampsAndChairs);
            Gather("autoFirewood", s.autoFirewood && s.enableAdvisor, Firewood);
            Gather("autoGrow", s.autoGrow, Grow);
            return list;
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

        private void Drop(string key)
        {
            if (droppedKeys.Contains(key)) return;
            droppedKeys.Add(key);
            droppedTicks.Add(Now);
        }

        /// <summary>Told quietly, kept for the check-myself page, and kept as an action to undo.</summary>
        private AutoAction Record(string kind, Map map, string text, LookTargets look = null)
        {
            var a = new AutoAction { kind = kind, map = map, tick = Now, text = text };
            actions.Add(a);
            while (actions.Count > AutomationMath.ActionsKept) actions.RemoveAt(0);
            log.Add(GenDate.DaysPassed + ": " + text);
            while (log.Count > AutomationMath.LogLines) log.RemoveAt(0);
            Messages.Message(text, look ?? LookTargets.Invalid, MessageTypeDefOf.SilentInput, false);
            return a;
        }

        // ---------------- Bills ----------------

        private static string BillKey(Building_WorkTable bench, RecipeDef recipe) => bench.thingIDNumber + ":" + recipe.defName;

        /// <summary>The council's bills the player has since taken off the bench are remembered as dropped.</summary>
        private void NoteRemovedBills(Map map)
        {
            for (int i = billKeys.Count - 1; i >= 0; i--)
            {
                var parts = billKeys[i].Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int id))
                {
                    billKeys.RemoveAt(i);
                    continue;
                }
                var bench = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b => b.thingIDNumber == id);
                if (bench == null) continue;
                if (bench.BillStack != null && bench.BillStack.Bills.Any(b => b.recipe?.defName == parts[1])) continue;
                Drop(billKeys[i]);
                billKeys.RemoveAt(i);
            }
        }

        private static bool KindOn(string kind)
        {
            var s = S;
            switch (kind)
            {
                case "meals": return s.autoBillMeals;
                case "medicine": return s.autoBillMedicine;
                case "components": return s.autoBillComponents;
                default: return false;
            }
        }

        /// <summary>
        /// Bills for the staples the colony lacks and can make (each kind its own switch) and for the chains (what the
        /// building and the bills wait on, link by link, the parts' bills first): a "make until" bill, a couple an hour at
        /// most. A bill of the council's that the player removed isn't set again for a fortnight.
        /// </summary>
        private IEnumerable<AutoPending> Bills(Map map)
        {
            NoteRemovedBills(map);
            var r = Making.For(map);
            int set = 0;
            if (S.autoBillChains)
                foreach (var c in r.needs.ToList())
                {
                    if (set >= AutomationMath.BillsAnHour) yield break;
                    if (c.billed || c.bench == null || Dropped(BillKey(c.bench, c.recipe))) continue;
                    var link = c;
                    string text = "CC_Auto_BillSet".Translate(link.product.label.Named("PRODUCT"), link.target.Named("N"), link.bench.LabelCap.ToString().Named("BENCH"), link.why.Named("WHY")).Resolve();
                    set++;
                    yield return new AutoPending
                    {
                        text = text,
                        act = () =>
                        {
                            Making.SetUpChain(link, true);
                            billKeys.Add(BillKey(link.bench, link.recipe));
                            var a = Record(AutoAction.Bill, map, text, new LookTargets(link.bench));
                            a.things.Add(link.bench);
                            a.recipe = link.recipe.defName;
                        }
                    };
                }
            foreach (var o in r.could.ToList())
            {
                if (set >= AutomationMath.BillsAnHour) yield break;
                if (o.bench == null || o.wantedBy != null || !KindOn(o.kind) || Dropped(BillKey(o.bench, o.recipe))) continue;
                var option = o;
                string text = "CC_Auto_BillSet".Translate(option.product.label.Named("PRODUCT"), option.target.Named("N"), option.bench.LabelCap.ToString().Named("BENCH"),
                    "CC_Auto_Why_Staple".Translate().Resolve().Named("WHY")).Resolve();
                set++;
                yield return new AutoPending
                {
                    text = text,
                    act = () =>
                    {
                        Making.SetUpBill(option, true);
                        billKeys.Add(BillKey(option.bench, option.recipe));
                        var a = Record(AutoAction.Bill, map, text, new LookTargets(option.bench));
                        a.things.Add(option.bench);
                        a.recipe = option.recipe.defName;
                    }
                };
            }
        }

        // ---------------- The plan ----------------

        /// <summary>Proposed steps at a high level or above go straight into the plan.</summary>
        private IEnumerable<AutoPending> Plan(Map map)
        {
            foreach (var s in NextSteps.ProposedFor(map).ToList())
            {
                if (NextStepsMath.LevelOf(s.score) < AutomationMath.PlanLevel) continue;
                var step = s;
                string text = "CC_Auto_Planned".Translate(step.title.Named("STEP")).Resolve();
                yield return new AutoPending
                {
                    text = text,
                    act = () =>
                    {
                        NextSteps.Take(step);
                        var a = Record(AutoAction.Plan, map, text, step.look.IsValid ? new LookTargets(step.look, map) : null);
                        a.stepKey = step.key;
                    }
                };
            }
        }

        // ---------------- Research ----------------

        /// <summary>An idle research bench takes up the cheapest project of the colony's own era.</summary>
        private IEnumerable<AutoPending> Research(Map map)
        {
            if (Find.ResearchManager.GetProject() != null || !map.listerBuildings.ColonistsHaveResearchBench() || !Due("research", AutomationMath.ResearchDays)) yield break;
            var next = AdvisorWorker_Research.SuggestNext();
            if (next == null) yield break;
            string text = "CC_Auto_Research".Translate(next.LabelCap.ToString().Named("PROJECT")).Resolve();
            yield return new AutoPending
            {
                text = text,
                act = () =>
                {
                    var prev = Find.ResearchManager.GetProject();
                    Find.ResearchManager.SetCurrentProject(next);
                    Done("research");
                    var a = Record(AutoAction.Research, map, text);
                    a.prev = prev;
                }
            };
        }

        // ---------------- Stores and the base ----------------

        /// <summary>Goods nobody stores get the stockpile the stores review would lay out, every few days at most.</summary>
        private IEnumerable<AutoPending> Stockpile(Map map)
        {
            var f = BaseReview.Finding(map, "unstored");
            if (f?.fix == null || f.Level < AdviceLevel.Medium || !Due("unstored", AutomationMath.StockpileDays)) yield break;
            string text = "CC_Auto_Stockpile".Translate(f.title.Named("WHAT")).Resolve();
            yield return new AutoPending
            {
                text = text,
                act = () =>
                {
                    var before = new HashSet<int>(map.zoneManager.AllZones.Select(z => z.ID));
                    f.fix();
                    Done("unstored");
                    var a = Record(AutoAction.Stockpile, map, text, f.look.IsValid ? new LookTargets(f.look, map) : null);
                    var made = map.zoneManager.AllZones.FirstOrDefault(z => !before.Contains(z.ID));
                    if (made != null) a.zoneId = made.ID;
                }
            };
        }

        private static readonly string[] TakeDownKeys = { "stubs", "conduits", "orphanconduits", "doors" };

        /// <summary>What the base review would take down (wall stubs, dead conduit and pipe ends, doors between the same room) gets its deconstruct orders, so many a day at most.</summary>
        private IEnumerable<AutoPending> TakeDown(Map map)
        {
            int left = AutomationMath.Allowed(takenDownToday, S.autoTakeDownsADay, int.MaxValue);
            foreach (var f in BaseReview.For(map).ToList())
            {
                if (left <= 0) yield break;
                if (f.fix == null || f.things.NullOrEmpty()) continue;
                if (!TakeDownKeys.Contains(f.key) && !f.key.StartsWith("pipes:")) continue;
                if (!Due("takedown:" + f.key, AutomationMath.TakeDownDays)) continue;
                var finding = f;
                var things = f.things.Where(t => t != null && !t.Destroyed).Take(left).ToList();
                if (things.Count == 0) continue;
                left -= things.Count;
                string text = "CC_Auto_TakeDown".Translate(finding.title.Named("WHAT"), things.Count.Named("N")).Resolve();
                yield return new AutoPending
                {
                    text = text,
                    act = () =>
                    {
                        BaseReview.Deconstruct(map, things);
                        takenDownToday += things.Count;
                        if (things.Count >= finding.things.Count) Done("takedown:" + finding.key);
                        var a = Record(AutoAction.TakeDown, map, text, finding.look.IsValid ? new LookTargets(finding.look, map) : null);
                        a.things.AddRange(things);
                    }
                };
            }
        }

        // ---------------- The advisor's own hands ----------------

        private static readonly string[] LampSignals = { "Council_Adv_BedsDark", "Council_Adv_NoSeat" };

        /// <summary>Dark bedrooms get their lamps and benches their chairs planned, as the advisor's button would, once a day at most and within the day's materials budget.</summary>
        private IEnumerable<AutoPending> LampsAndChairs(Map map)
        {
            var signals = CouncilMapComponent.For(map)?.Signals;
            if (signals == null) yield break;
            foreach (var s in signals.ToList())
            {
                if (s.def == null || s.act == null || !LampSignals.Contains(s.def.defName) || !Due(s.def.defName, AutomationMath.LampsDays)) continue;
                if (!AutomationMath.BudgetLeft(spentToday, S.autoBudget))
                {
                    yield return new AutoPending { text = "CC_Auto_BudgetSpent".Translate(s.title.Named("WHAT")).Resolve() };
                    continue;
                }
                var signal = s;
                string text = "CC_Auto_Acted".Translate(signal.title.Named("WHAT")).Resolve();
                yield return new AutoPending
                {
                    text = text,
                    act = () =>
                    {
                        var before = new HashSet<Thing>(map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint));
                        signal.act();
                        Done(signal.def.defName);
                        var placed = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Where(t => !before.Contains(t)).ToList();
                        spentToday += placed.Sum(Worth);
                        var a = Record(AutoAction.Lamps, map, text, signal.look);
                        a.things.AddRange(placed);
                    }
                };
            }
        }

        /// <summary>What a blueprint is worth once built, against the day's budget.</summary>
        private static float Worth(Thing t)
        {
            try
            {
                if (t is Blueprint_Build bp && bp.def.entityDefToBuild is ThingDef built) return Mathf.Max(5f, built.GetStatValueAbstract(StatDefOf.MarketValue, bp.stuffToUse));
            }
            catch
            {
                // A blueprint whose worth can't be read: counted as a small one.
            }
            return 20f;
        }

        /// <summary>Firewood short before the cold: trees near the base are marked for chopping, as many as the advice is loud and the day's cap allows, once a day at most.</summary>
        private IEnumerable<AutoPending> Firewood(Map map)
        {
            var s = CouncilMapComponent.For(map)?.Signals?.FirstOrDefault(x => x.def?.defName == "Council_Adv_Firewood");
            if (s == null || !Due("firewood", AutomationMath.FirewoodDays)) yield break;
            int want = AutomationMath.Allowed(choppedToday, S.autoChopsADay, AutomationMath.TreesToMark((int)s.Level));
            if (want <= 0) yield break;
            var trees = Trees(map).Take(want).ToList();
            if (trees.Count == 0) yield break;
            string text = "CC_Auto_Firewood".Translate(trees.Count.Named("N")).Resolve();
            yield return new AutoPending
            {
                text = text,
                act = () =>
                {
                    var dm = map.designationManager;
                    int marked = 0;
                    foreach (var p in trees)
                    {
                        if (p.Destroyed || !p.Spawned || dm.DesignationOn(p) != null) continue;
                        dm.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
                        marked++;
                    }
                    if (marked == 0) return;
                    choppedToday += marked;
                    Done("firewood");
                    var a = Record(AutoAction.Firewood, map, text, new LookTargets(trees[0]));
                    a.things.AddRange(trees.Cast<Thing>());
                }
            };
        }

        /// <summary>Mature firewood trees near the base, nearest first: not forbidden, not marked, not in a zone, reachable.</summary>
        private static IEnumerable<Plant> Trees(Map map)
        {
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
            return trees.OrderBy(p => p.Position.DistanceToSquared(centre));
        }

        // ---------------- The growing side ----------------

        /// <summary>A link of a chain that no bench makes but a plant yields, with no zone growing it: so many cells are sown for it, once every so many days per thing.</summary>
        private IEnumerable<AutoPending> Grow(Map map)
        {
            var r = Making.For(map);
            var seen = new HashSet<ThingDef>();
            foreach (var root in r.chains)
                foreach (var n in root.All())
                {
                    if (n.plant == null || n.recipe != null || n.zone != null || n.cells <= 0 || !seen.Add(n.thing)) continue;
                    if (!Due("grow:" + n.thing.defName, AutomationMath.GrowDays)) continue;
                    var node = n;
                    string text = "CC_Auto_Grow".Translate(node.cells.Named("N"), node.plant.label.Named("PLANT"), node.thing.label.Named("PRODUCT")).Resolve();
                    yield return new AutoPending
                    {
                        text = text,
                        act = () =>
                        {
                            var before = new HashSet<int>(map.zoneManager.AllZones.Select(z => z.ID));
                            var zone = Sowing.Sow(map, node.plant, node.cells);
                            if (zone == null) return;
                            Done("grow:" + node.thing.defName);
                            var a = Record(AutoAction.Grow, map, text, zone.Cells.Count > 0 ? new LookTargets(zone.Cells[0], map) : null);
                            a.zoneId = zone.ID;
                            if (before.Contains(zone.ID)) a.cells.AddRange(zone.Cells.TakeLast(node.cells));
                            Making.For(map, true);
                        }
                    };
                }
        }

        // ---------------- Undoing ----------------

        /// <summary>Takes an action back: the bill off the bench (and not set again), the step back to proposed, the research pick, the zone, the orders, the blueprints, the marks.</summary>
        public void Undo(AutoAction a)
        {
            if (a == null || !a.CanUndo) return;
            var map = a.map;
            try
            {
                switch (a.kind)
                {
                    case AutoAction.Bill:
                    {
                        var bench = a.things.OfType<Building_WorkTable>().FirstOrDefault();
                        var bill = bench?.BillStack?.Bills.FirstOrDefault(b => b.recipe?.defName == a.recipe);
                        if (bench != null && bill != null) bench.BillStack.Delete(bill);
                        if (bench != null && a.recipe != null)
                        {
                            string key = bench.thingIDNumber + ":" + a.recipe;
                            billKeys.Remove(key);
                            Drop(key);
                        }
                        if (map != null) Making.For(map, true);
                        break;
                    }
                    case AutoAction.Plan:
                    {
                        var step = CouncilGameComponent.Instance?.nextSteps?.FirstOrDefault(s => s.key == a.stepKey && s.Planned);
                        if (step != null)
                        {
                            step.state = NextStepsMath.Proposed;
                            step.changedTick = Now;
                        }
                        break;
                    }
                    case AutoAction.Research:
                        Find.ResearchManager.SetCurrentProject(a.prev);
                        break;
                    case AutoAction.Stockpile:
                    case AutoAction.Grow:
                    {
                        var zone = map?.zoneManager.AllZones.FirstOrDefault(z => z.ID == a.zoneId);
                        if (zone == null) break;
                        if (a.cells.Count > 0 && zone.Cells.Count > a.cells.Count)
                        {
                            foreach (var c in a.cells)
                                if (zone.ContainsCell(c)) zone.RemoveCell(c);
                        }
                        else zone.Delete();
                        break;
                    }
                    case AutoAction.TakeDown:
                    {
                        var dm = map?.designationManager;
                        if (dm == null) break;
                        foreach (var t in a.things)
                        {
                            var d = dm.DesignationOn(t, DesignationDefOf.Deconstruct);
                            if (d != null) dm.RemoveDesignation(d);
                        }
                        break;
                    }
                    case AutoAction.Lamps:
                        foreach (var t in a.things)
                            if (t != null && !t.Destroyed && t.Spawned) t.Destroy(DestroyMode.Cancel);
                        break;
                    case AutoAction.Firewood:
                    {
                        var dm = map?.designationManager;
                        if (dm == null) break;
                        foreach (var t in a.things)
                        {
                            var d = dm.DesignationOn(t, DesignationDefOf.HarvestPlant);
                            if (d != null) dm.RemoveDesignation(d);
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("undo " + a.kind, e);
            }
            a.undone = true;
            log.Add(GenDate.DaysPassed + ": " + "CC_Auto_Undone".Translate(a.text).Resolve());
            while (log.Count > AutomationMath.LogLines) log.RemoveAt(0);
            Messages.Message("CC_Auto_Undone".Translate(a.text).Resolve(), MessageTypeDefOf.SilentInput, false);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref billKeys, "billKeys", LookMode.Value);
            Scribe_Collections.Look(ref droppedKeys, "droppedKeys", LookMode.Value);
            Scribe_Collections.Look(ref droppedTicks, "droppedTicks", LookMode.Value);
            Scribe_Collections.Look(ref doneKeys, "doneKeys", LookMode.Value);
            Scribe_Collections.Look(ref doneTicks, "doneTicks", LookMode.Value);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);
            Scribe_Collections.Look(ref actions, "actions", LookMode.Deep);
            Scribe_Values.Look(ref spendDay, "spendDay", -1);
            Scribe_Values.Look(ref spentToday, "spentToday");
            Scribe_Values.Look(ref takeDownDay, "takeDownDay", -1);
            Scribe_Values.Look(ref takenDownToday, "takenDownToday");
            Scribe_Values.Look(ref chopDay, "chopDay", -1);
            Scribe_Values.Look(ref choppedToday, "choppedToday");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                billKeys ??= new List<string>();
                droppedKeys ??= new List<string>();
                droppedTicks ??= new List<int>();
                doneKeys ??= new List<string>();
                doneTicks ??= new List<int>();
                log ??= new List<string>();
                actions ??= new List<AutoAction>();
                actions.RemoveAll(a => a == null);
                while (droppedTicks.Count < droppedKeys.Count) droppedTicks.Add(0);
                while (droppedTicks.Count > droppedKeys.Count) droppedTicks.RemoveAt(droppedTicks.Count - 1);
                while (doneTicks.Count < doneKeys.Count) doneTicks.Add(0);
                while (doneTicks.Count > doneKeys.Count) doneTicks.RemoveAt(doneTicks.Count - 1);
                while (log.Count > AutomationMath.LogLines) log.RemoveAt(0);
                while (actions.Count > AutomationMath.ActionsKept) actions.RemoveAt(0);
            }
        }
    }
}
