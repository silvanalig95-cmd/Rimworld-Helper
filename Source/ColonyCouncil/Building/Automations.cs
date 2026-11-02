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
        /// <summary>Its key in the shared suggestion core ("auto:bill:...", "base:stubs", "adv:..."): undone, it is set aside there, so it doesn't come back.</summary>
        public string key;
        /// <summary>How it is taken back, the same undo a step done by hand keeps (older actions have none: their kind says).</summary>
        public SuggestUndo undo;

        public bool CanUndo => !undone && map != null && (undo == null || SafePossible());

        private bool SafePossible()
        {
            try
            {
                return undo.Possible(map);
            }
            catch
            {
                return false;
            }
        }

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
            Scribe_Values.Look(ref key, "key");
            Scribe_Deep.Look(ref undo, "undo");
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

    /// <summary>
    /// Something an automation would do now: its switch, what it is in words with its numbers, and the doing of it. The
    /// same thing is a step the player can do by hand (<see cref="AutomationTracker.HandSteps"/>): <see cref="run"/> does
    /// it and returns its undo and check; <see cref="act"/> is the automation's own doing (the run, then its log).
    /// </summary>
    public class AutoPending
    {
        /// <summary>The automation's switch ("autoBills").</summary>
        public string key;
        /// <summary>Its key in the shared suggestion core: set aside there, neither the automation nor the page offers it.</summary>
        public string stepKey;
        public string text;
        /// <summary>Why, in the colony's numbers.</summary>
        public string reason;
        public string doLabel, doTip;
        public LookTargets look;
        public int level = 1;
        public Func<SuggestDone> run;
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

        // ---------------- The settings page ----------------

        private static readonly Dictionary<string, string> settingsLines = new Dictionary<string, string>();
        private static float settingsLinesAt = -999f;
        private static Map settingsLinesMap;

        /// <summary>
        /// The line under an automation's switch on the settings page: what it would do right now on the home map in view
        /// (else the colony's first home), from <see cref="Preview"/>, looked at again every few seconds while the page is
        /// open; or that no game is loaded. Null for a setting that isn't an automation's own switch.
        /// </summary>
        public static string SettingsLine(string key)
        {
            if (key == null || !key.StartsWith("auto", StringComparison.Ordinal)) return null;
            bool playing = Current.ProgramState == ProgramState.Playing && Current.Game != null;
            var map = !playing ? null : Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome ? Find.CurrentMap : Find.AnyPlayerHomeMap;
            float now = Time.realtimeSinceStartup;
            if (now - settingsLinesAt > 3f || now < settingsLinesAt || map != settingsLinesMap)
            {
                settingsLinesAt = now;
                settingsLinesMap = map;
                settingsLines.Clear();
                var tracker = playing ? CouncilGameComponent.Instance?.automations : null;
                var preview = tracker != null && map != null ? tracker.Preview(map) : null;
                foreach (var (k, _) in Switches())
                {
                    string line;
                    if (!playing) line = "CC_Auto_SetNoGame".Translate().Resolve();
                    else if (preview == null) line = "CC_Auto_SetNoMap".Translate().Resolve();
                    else
                    {
                        var texts = preview.FirstOrDefault(p => p.key == k).texts;
                        if (texts == null || texts.Count == 0) line = "CC_Auto_SetNowNothing".Translate().Resolve();
                        else
                        {
                            string body = string.Join(" ", texts.Take(2));
                            if (texts.Count > 2) body += " " + "CC_Auto_SetMore".Translate(texts.Count - 2).Resolve();
                            line = "CC_Auto_SetNow".Translate(body).Resolve();
                        }
                    }
                    settingsLines[k] = line;
                }
            }
            return settingsLines.TryGetValue(key, out var found) ? found : null;
        }

        /// <summary>Everything the automations would do now on this map (only those switched on, or all of them), each with its doing.</summary>
        private List<AutoPending> Candidates(Map map, bool onlyOn)
        {
            var list = new List<AutoPending>();
            var s = S;
            if (s == null) return list;
            var tracker = SuggestTracker.Instance;
            void Gather(string key, bool on, Func<Map, IEnumerable<AutoPending>> f)
            {
                if (onlyOn && !on) return;
                try
                {
                    foreach (var p in f(map))
                    {
                        // The shared "Not this": what the player set aside, or did by hand and is still to be checked, isn't
                        // done again; nor, by the automation, what the player undid the last time it was done.
                        if (p.stepKey != null && tracker != null && (!tracker.Offered(p.stepKey) || onlyOn && tracker.UndoneLast(p.stepKey))) continue;
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

        // ---------------- By hand ----------------

        /// <summary>The source a step's key belongs to ("auto", "base", "adv"), for the pages' lists.</summary>
        private static string SourceOf(string key)
        {
            int i = key?.IndexOf(':') ?? -1;
            return i > 0 ? key.Substring(0, i) : AutomationSource;
        }

        public const string AutomationSource = "auto";

        /// <summary>
        /// What each automation would do now (switched on or not) as steps the player does by hand, through the shared
        /// suggestion core: the same doing, kept in the log with its undo and checked later, "Not this" to set it aside
        /// (then the automation leaves it alone too). Grouped by switch, at most <paramref name="perSwitch"/> each.
        /// </summary>
        public List<(string key, bool on, List<Suggestion> steps)> HandSteps(Map map, int perSwitch = 2)
        {
            NewDay();
            var result = new List<(string, bool, List<Suggestion>)>();
            if (map == null) return result;
            List<AutoPending> all;
            try
            {
                all = Candidates(map, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("automations by hand", e);
                all = new List<AutoPending>();
            }
            foreach (var (key, on) in Switches())
            {
                var steps = new List<Suggestion>();
                foreach (var p in all.Where(x => x.key == key && x.run != null && x.stepKey != null).Take(perSwitch))
                {
                    var pending = p;
                    steps.Add(new Suggestion
                    {
                        key = p.stepKey, source = SourceOf(p.stepKey), area = key, title = p.text, reason = p.reason, level = p.level, map = map, look = p.look,
                        doLabel = p.doLabel, doTip = p.doTip, apply = () => pending.run()
                    });
                }
                result.Add((key, on, steps));
            }
            return result;
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

        /// <summary>Told quietly, kept for the check-myself page, and kept as an action to undo (with the step's undo and key when it has them).</summary>
        private AutoAction Record(string kind, Map map, string text, LookTargets look = null, SuggestDone done = null, string key = null)
        {
            var a = new AutoAction { kind = kind, map = map, tick = Now, text = text, undo = done?.undo, key = key };
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
                    if (c.billed || c.bench == null || Dropped(BillKey(c.bench, c.recipe)) || !Offered(BillStepKey(c.bench, c.recipe))) continue;
                    var link = c;
                    string numbers = "CC_Same_Auto_BillNumbers".Translate(link.stock.Named("HAVE"), link.target.Named("N")).Resolve();
                    string text = "CC_Auto_BillSet".Translate(link.product.label.Named("PRODUCT"), link.target.Named("N"), link.bench.LabelCap.ToString().Named("BENCH"), link.why.Named("WHY")).Resolve()
                                  + " " + numbers;
                    set++;
                    yield return BillPending(map, link.bench, link.recipe, link.product, link.target, link.stock, text, link.why + " " + numbers + (link.parts.NullOrEmpty() ? "" : " " + link.PartsLine));
                }
            foreach (var o in r.could.ToList())
            {
                if (set >= AutomationMath.BillsAnHour) yield break;
                if (o.bench == null || o.wantedBy != null || !KindOn(o.kind) || Dropped(BillKey(o.bench, o.recipe)) || !Offered(BillStepKey(o.bench, o.recipe))) continue;
                var option = o;
                string why = option.targetWhy ?? "CC_Auto_Why_Staple".Translate().Resolve();
                string text = "CC_Auto_BillSet".Translate(option.product.label.Named("PRODUCT"), option.target.Named("N"), option.bench.LabelCap.ToString().Named("BENCH"), why.Named("WHY")).Resolve()
                              + " " + "CC_Same_Auto_BillNumbers".Translate(option.productStock.Named("HAVE"), option.target.Named("N")).Resolve();
                set++;
                yield return BillPending(map, option.bench, option.recipe, option.product, option.target, option.productStock, text, why + " " + option.PartsLine);
            }
        }

        private static string BillStepKey(Building_WorkTable bench, RecipeDef recipe) => AutomationSource + ":bill:" + BillKey(bench, recipe);

        private static bool Offered(string key) => SuggestTracker.Instance?.Offered(key) ?? true;

        /// <summary>A "make until" bill as a pending action: the bill set (by hand or by the automation), its undo the bill taken off, its check whether the stock rose.</summary>
        private AutoPending BillPending(Map map, Building_WorkTable bench, RecipeDef recipe, ThingDef product, int target, int stock, string text, string reason)
        {
            string key = BillStepKey(bench, recipe);
            Func<SuggestDone> run = () =>
            {
                var bill = Making.AddBill(bench, recipe, product, target, true);
                if (bill == null) return null;
                return new SuggestDone
                {
                    undo = new UndoBillsAdded { bills = { bill } }, map = map,
                    check = new BillStockCheck { bench = bench, bill = bill, product = product, before = stock, target = target, waitTicks = SuggestMath.TicksPerDay }
                };
            };
            return new AutoPending
            {
                stepKey = key, text = text, reason = reason, look = new LookTargets(bench), run = run,
                doLabel = "CC_Making_SetBill".Translate(target).Resolve(), doTip = "CC_Making_SetBillTip".Translate(bench.LabelCap.ToString()).Resolve(),
                act = () =>
                {
                    var d = run();
                    if (d == null) return;
                    billKeys.Add(BillKey(bench, recipe));
                    var a = Record(AutoAction.Bill, map, text, new LookTargets(bench), d, key);
                    a.things.Add(bench);
                    a.recipe = recipe.defName;
                }
            };
        }

        // ---------------- The plan ----------------

        /// <summary>Proposed steps at a high level or above go straight into the plan.</summary>
        private IEnumerable<AutoPending> Plan(Map map)
        {
            foreach (var s in NextSteps.ProposedFor(map).ToList())
            {
                int level = NextStepsMath.LevelOf(s.score);
                if (level < AutomationMath.PlanLevel) continue;
                var step = s;
                string key = AutomationSource + ":plan:" + step.key;
                string text = "CC_Auto_Planned".Translate(step.title.Named("STEP")).Resolve() + " " + "CC_Same_Auto_PlanLevel".Translate(("CC_Level_" + (AdviceLevel)level).Translate().Named("LEVEL")).Resolve();
                Func<SuggestDone> run = () =>
                {
                    if (!step.Proposed) return null;
                    NextSteps.Take(step);
                    return new SuggestDone { undo = new UndoPlanTaken { stepKey = step.key }, map = map };
                };
                yield return new AutoPending
                {
                    stepKey = key, text = text, reason = step.detail, level = level, look = step.look.IsValid ? new LookTargets(step.look, map) : null, run = run,
                    doLabel = "CC_Plan_Take".Translate().Resolve(), doTip = "CC_Plan_TakeTip".Translate().Resolve(),
                    act = () =>
                    {
                        var d = run();
                        if (d == null) return;
                        var a = Record(AutoAction.Plan, map, text, step.look.IsValid ? new LookTargets(step.look, map) : null, d, key);
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
            string key = AutomationSource + ":research:" + next.defName;
            string text = "CC_Auto_Research".Translate(next.LabelCap.ToString().Named("PROJECT")).Resolve() + " "
                          + "CC_Same_Auto_ResearchCost".Translate(next.CostApparent.ToString("0").Named("COST")).Resolve();
            Func<SuggestDone> run = () =>
            {
                var prev = Find.ResearchManager.GetProject();
                if (prev != null || next.IsFinished || !next.CanStartNow) return null;
                Find.ResearchManager.SetCurrentProject(next);
                return new SuggestDone { undo = new UndoResearchPick { prev = prev, picked = next }, map = map };
            };
            yield return new AutoPending
            {
                stepKey = key, text = text, reason = "CC_Plan_Research_Detail".Translate(next.LabelCap.ToString().Named("PROJECT"), next.CostApparent.ToString("0").Named("COST")).Resolve(),
                run = run, doLabel = "CC_Plan_StartResearch".Translate().Resolve(), doTip = "CC_Plan_StartResearchTip".Translate(next.LabelCap).Resolve(),
                act = () =>
                {
                    var prev = Find.ResearchManager.GetProject();
                    var d = run();
                    if (d == null) return;
                    Done("research");
                    var a = Record(AutoAction.Research, map, text, null, d, key);
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
            string key = BaseReview.KeyOf(f);
            string text = "CC_Auto_Stockpile".Translate(f.title.Named("WHAT")).Resolve();
            var fix = f.fix;
            yield return new AutoPending
            {
                stepKey = key, text = text, reason = f.detail, level = (int)f.Level, look = f.look.IsValid ? new LookTargets(f.look, map) : null, run = fix,
                doLabel = f.fixLabel, doTip = f.fixTip,
                act = () =>
                {
                    var d = fix();
                    if (d == null) return;
                    Done("unstored");
                    var a = Record(AutoAction.Stockpile, map, text, f.look.IsValid ? new LookTargets(f.look, map) : null, d, key);
                    if (d.undo is UndoZone z) a.zoneId = z.zoneId;
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
                string key = BaseReview.KeyOf(finding);
                string text = "CC_Auto_TakeDown".Translate(finding.title.Named("WHAT"), things.Count.Named("N")).Resolve();
                Func<SuggestDone> run = () => BaseReview.Deconstruct(map, things);
                yield return new AutoPending
                {
                    stepKey = key, text = text, reason = finding.detail, level = (int)finding.Level, look = finding.look.IsValid ? new LookTargets(finding.look, map) : null, run = run,
                    doLabel = finding.fixLabel, doTip = finding.fixTip,
                    act = () =>
                    {
                        var d = run();
                        if (d == null) return;
                        takenDownToday += things.Count;
                        if (things.Count >= finding.things.Count) Done("takedown:" + finding.key);
                        var a = Record(AutoAction.TakeDown, map, text, finding.look.IsValid ? new LookTargets(finding.look, map) : null, d, key);
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
                string key = AdvisorSteps.KeyOf(signal);
                string text = "CC_Auto_Acted".Translate(signal.title.Named("WHAT")).Resolve();
                var step = AdvisorSteps.StepOf(signal, map);
                if (step.apply == null) continue;
                yield return new AutoPending
                {
                    stepKey = key, text = text, reason = signal.detail, level = (int)signal.Level, look = signal.look, run = step.apply, doLabel = step.doLabel, doTip = step.doTip,
                    act = () =>
                    {
                        var d = step.apply();
                        if (d == null) return;
                        Done(signal.def.defName);
                        var placed = (d.undo as UndoOrders)?.pieceCells.SelectMany(c => c.GetThingList(map).OfType<Blueprint_Build>()).Distinct().Cast<Thing>().ToList() ?? new List<Thing>();
                        float worth = placed.Sum(Worth);
                        spentToday += worth;
                        string line = text + " " + "CC_Same_Auto_Placed".Translate(placed.Count.Named("N"), Mathf.RoundToInt(worth).Named("WORTH")).Resolve();
                        var a = Record(AutoAction.Lamps, map, line, signal.look, d, key);
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
            const string key = AutomationSource + ":firewood";
            // The wood the trees give, against what the advice says the fires will burn.
            float wood = trees.Sum(p => p.YieldNow());
            string text = "CC_Auto_Firewood".Translate(trees.Count.Named("N")).Resolve() + " " + "CC_Same_Auto_FirewoodWood".Translate(Mathf.RoundToInt(wood).Named("WOOD")).Resolve();
            Func<SuggestDone> run = () =>
            {
                var dm = map.designationManager;
                var undo = new UndoOrders();
                foreach (var p in trees)
                {
                    if (p.Destroyed || !p.Spawned || dm.DesignationOn(p) != null) continue;
                    dm.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
                    undo.marked.Add(p);
                    undo.markedDefs.Add(DesignationDefOf.HarvestPlant.defName);
                }
                if (undo.Count == 0) return null;
                // Worked once the firewood advice has gone (enough wood for the fires till spring).
                return new SuggestDone { undo = undo, map = map, check = new SourceGoneCheck { key = AdvisorSteps.KeyOf(s), waitTicks = 2 * SuggestMath.TicksPerDay } };
            };
            yield return new AutoPending
            {
                stepKey = key, text = text, reason = s.detail, level = (int)s.Level, look = new LookTargets(trees[0]), run = run,
                doLabel = "CC_Same_Auto_FirewoodDo".Translate(trees.Count).Resolve(), doTip = "CC_Same_Auto_FirewoodDoTip".Translate(trees.Count).Resolve(),
                act = () =>
                {
                    var d = run();
                    if (d == null) return;
                    int marked = (d.undo as UndoOrders)?.marked.Count ?? 0;
                    choppedToday += marked;
                    Done("firewood");
                    var a = Record(AutoAction.Firewood, map, text, new LookTargets(trees[0]), d, key);
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
                    string key = AutomationSource + ":grow:" + node.thing.defName;
                    string text = "CC_Auto_Grow".Translate(node.cells.Named("N"), node.plant.label.Named("PLANT"), node.thing.label.Named("PRODUCT")).Resolve() + " "
                                  + "CC_Same_Auto_BillNumbers".Translate(node.stock.Named("HAVE"), node.want.Named("N")).Resolve();
                    Func<SuggestDone> run = () =>
                    {
                        var before = new HashSet<int>(map.zoneManager.AllZones.Select(z => z.ID));
                        var zone = Sowing.Sow(map, node.plant, node.cells);
                        if (zone == null) return null;
                        bool added = before.Contains(zone.ID);
                        var cells = added ? zone.Cells.TakeLast(node.cells).ToList() : zone.Cells.ToList();
                        Making.For(map, true);
                        return new SuggestDone
                        {
                            undo = new UndoZone { what = added ? UndoZone.Added : UndoZone.Made, zoneId = zone.ID, cells = cells }, map = map,
                            check = new AheadSownCheck { zoneId = zone.ID, plant = node.plant, cells = cells }
                        };
                    };
                    yield return new AutoPending
                    {
                        stepKey = key, text = text, reason = node.partsLine.NullOrEmpty() ? null : node.PartsLine,
                        run = run, doLabel = "CC_Same_Auto_GrowDo".Translate(node.cells).Resolve(), doTip = "CC_Same_Auto_GrowDoTip".Translate(node.cells.Named("N"), node.plant.label.Named("PLANT")).Resolve(),
                        act = () =>
                        {
                            var d = run();
                            if (d == null) return;
                            Done("grow:" + node.thing.defName);
                            var z = d.undo as UndoZone;
                            var a = Record(AutoAction.Grow, map, text, z != null && z.cells.Count > 0 ? new LookTargets(z.cells[0], map) : null, d, key);
                            a.zoneId = z?.zoneId ?? -1;
                            if (z != null && z.what == UndoZone.Added) a.cells.AddRange(z.cells);
                        }
                    };
                }
        }

        // ---------------- Undoing ----------------

        /// <summary>
        /// Takes an action back: the bill off the bench (and not set again), the step back to proposed, the research pick,
        /// the zone, the orders, the blueprints, the marks. Undone, it counts as "Not this" for its key in the shared
        /// suggestion core: after its cooldown it doesn't come back, by itself or as a step to do by hand, until the player
        /// brings it back.
        /// </summary>
        public void Undo(AutoAction a)
        {
            if (a == null || !a.CanUndo) return;
            var map = a.map;
            try
            {
                if (a.undo != null)
                {
                    // The same undo a step done by hand keeps.
                    a.undo.Undo(map);
                    if (a.kind == AutoAction.Bill)
                    {
                        var bench = a.things.OfType<Building_WorkTable>().FirstOrDefault();
                        if (bench != null && a.recipe != null)
                        {
                            string key = bench.thingIDNumber + ":" + a.recipe;
                            billKeys.Remove(key);
                            Drop(key);
                        }
                    }
                    if (map != null) Making.For(map, true);
                }
                else
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
            // An older action has no key: a bill's is its bench and recipe.
            string stepKey = a.key;
            if (stepKey == null && a.kind == AutoAction.Bill && a.recipe != null)
            {
                var bench = a.things.OfType<Building_WorkTable>().FirstOrDefault();
                if (bench != null) stepKey = AutomationSource + ":bill:" + bench.thingIDNumber + ":" + a.recipe;
            }
            if (stepKey != null) SuggestTracker.Instance?.Decline(stepKey, a.text);
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
