using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A button on a line's card: plan this, mark that, go there.</summary>
    public class LineAction
    {
        public string label, tip;
        public Action act;
    }

    /// <summary>One thing the review says about a line, in the colony's own terms, with how it works and what to do.</summary>
    public class LineNote
    {
        public string key, text, how;
        /// <summary>Something done well, said so the player knows it's right.</summary>
        public bool good;
        public float weight;
        public AlmanacArticleDef article;
        public List<LineAction> actions = new List<LineAction>();
        /// <summary>Where it is, for the map overlay.</summary>
        public List<IntVec3> cells = new List<IntVec3>();
    }

    /// <summary>
    /// The helper's reading of one defense line: where each step of its ladder stands (a step built by hand counts),
    /// the step to do next, the checks a seasoned player would make (the busiest way in moving, a way round, who sees
    /// the gate and from how far, what raiders would shelter behind, turrets that would blow each other up or have no
    /// power, wood in the wall, a gate left shut and more than one door in the wall, darkness, upkeep), each with what
    /// to do about it, and a rough strength to set against the raids. Planning a step for the line goes through
    /// <see cref="PlanAt"/>: plans in front follow the way in, and walls that would turn raiders away are caught first.
    /// Worked out on demand, with the survey: when the survey or the line changes, or after a few seconds while the
    /// Defenses page is open. Never on a tick.
    /// </summary>
    public class DefenseLineReview
    {
        public DefenseLine line;
        public DefenseLineFacts f;
        public List<StepResult> steps = new List<StepResult>();
        public int current = -1;
        public List<LineNote> notes = new List<LineNote>();
        public DefenseLineMath.StrengthOut strength;
        public int points, verdict;
        public string where;
        public DefenseSurvey survey;
        public int version, madeTick;
        public float madeAt;

        public StepResult CurrentStep => current >= 0 && current < steps.Count ? steps[current] : null;

        private static readonly Dictionary<(Map, int), DefenseLineReview> cache = new Dictionary<(Map, int), DefenseLineReview>();
        private static Game cacheGame;

        public const float FreshSeconds = 5f;

        /// <summary>The review of a line: the one kept, unless the survey or the line has changed or it's a few seconds old (and the game has moved on).</summary>
        public static DefenseLineReview For(Map map, DefenseLine line, bool force = false)
        {
            if (map == null || line == null) return null;
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                cacheGame = Current.Game;
            }
            var s = DefenseSurvey.For(map);
            if (s == null) return null;
            cache.TryGetValue((map, line.id), out var kept);
            if (!force && kept != null && kept.survey == s && kept.version == line.version
                && (Time.realtimeSinceStartup - kept.madeAt < FreshSeconds || Find.TickManager.TicksGame == kept.madeTick))
                return kept;
            try
            {
                kept = Build(map, s, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense line review", e);
                if (kept == null) return null;
                kept.madeAt = Time.realtimeSinceStartup;
            }
            if (cache.Count > 60) cache.Clear();
            cache[(map, line.id)] = kept;
            return kept;
        }

        /// <summary>The review kept for a line, without working one out.</summary>
        public static DefenseLineReview Kept(Map map, DefenseLine line) =>
            map != null && line != null && cacheGame == Current.Game && cache.TryGetValue((map, line.id), out var r) ? r : null;

        public static void Forget(Map map, DefenseLine line)
        {
            if (map != null && line != null) cache.Remove((map, line.id));
        }

        private static DefenseLineReview Build(Map map, DefenseSurvey s, DefenseLine line)
        {
            var r = new DefenseLineReview
            {
                line = line, survey = s, version = line.version, madeAt = Time.realtimeSinceStartup, madeTick = Find.TickManager.TicksGame
            };
            var f = r.f = DefenseLineFacts.Read(map, s, line);
            int now = Find.TickManager.TicksGame;
            foreach (var def in DefenseStepDef.Ladder(line.aim))
            {
                StepResult step;
                if (line.Skipped(def.defName)) step = new StepResult { def = def, state = StepState.Skipped, template = def.FirstTemplate, look = f.gate };
                else
                {
                    try
                    {
                        step = def.Worker.Evaluate(f) ?? new StepResult { def = def };
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("defense step " + def.defName, e);
                        step = new StepResult { def = def };
                    }
                    step.def = def;
                    bool now0 = def.nowFor?.Contains(line.aim) ?? false;
                    if (def.minRaidPoints > 0f && f.raidPoints < def.minRaidPoints && !now0 && step.state != StepState.Done && step.state != StepState.NotApplicable)
                    {
                        step.state = StepState.Later;
                        step.left = "CC_DefLine_Later".Translate(f.raidPoints.ToString("0"), def.minRaidPoints.ToString("0")).Resolve();
                    }
                }
                if (step.state == StepState.Done) line.MarkDone(def.defName, now);
                r.steps.Add(step);
            }
            r.current = DefenseLineMath.Current(r.steps.Select(x => x.state).ToList());
            FromLastRaid(r, line);
            r.where = WhereOf(line, map, s);
            Checks(r);
            Strength(r);
            r.notes = r.notes.OrderBy(n => n.good ? 1 : 0).ThenByDescending(n => n.weight).ToList();
            return r;
        }

        /// <summary>The step the last raid showed missing comes first, whatever its number (for twenty days after it).</summary>
        private static void FromLastRaid(DefenseLineReview r, DefenseLine line)
        {
            var raid = line.LastRaid;
            if (raid == null || Find.TickManager.TicksGame - raid.tick > GenDate.TicksPerDay * 20) return;
            foreach (var lesson in raid.lessons)
            {
                int i = r.steps.FindIndex(s => Teaches(lesson, s.def.Worker));
                if (i < 0) continue;
                var st = r.steps[i].state;
                if (st != StepState.Open && st != StepState.Partly && st != StepState.NotYet) continue;
                r.current = i;
                r.steps[i].notes.Insert(0, "CC_DefLine_FromRaid".Translate());
                return;
            }
        }

        private static bool Teaches(string lesson, DefenseStepWorker w)
        {
            switch (lesson)
            {
                case nameof(DefenseLineMath.Lesson.Leak): return w is StepWorker_CloseGap;
                case nameof(DefenseLineMath.Lesson.Harden): return w is StepWorker_Harden;
                case nameof(DefenseLineMath.Lesson.CoverLeft):
                case nameof(DefenseLineMath.Lesson.CoverRight): return w is StepWorker_Cover;
                case nameof(DefenseLineMath.Lesson.Fallback): return w is StepWorker_Fallback;
                default: return false;
            }
        }

        private static AlmanacArticleDef A(params string[] names) =>
            names.Select(n => DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(n)).FirstOrDefault(a => a != null);

        // ---------------- Words ----------------

        /// <summary>"the north-east gate": the player's name for a line, else one from where it is and what it holds.</summary>
        public static string NameOf(DefenseLine line, Map map)
        {
            if (line == null) return "?";
            if (!line.name.NullOrEmpty()) return line.name;
            string auto = AutoName(line, map);
            var comp = DefenseLines.Of(map);
            int same = comp?.lines.Count(o => o.id < line.id && o.name.NullOrEmpty() && AutoName(o, map) == auto) ?? 0;
            return same > 0 ? "CC_DefLine_Name_Nth".Translate(auto, same + 1).Resolve() : auto;
        }

        private static string AutoName(DefenseLine line, Map map)
        {
            var s = DefenseSurvey.Kept(map);
            string dir = s != null ? DefenseAdvice.Where(s, line.gate) : DefenseSurvey.FacingWord(line.facing);
            switch (line.place)
            {
                case "pass": return "CC_DefLine_Name_Pass".Translate(dir).Resolve();
                case "bridge": return "CC_DefLine_Name_Bridge".Translate(dir).Resolve();
                case "open": return "CC_DefLine_Name_Line".Translate(dir).Resolve();
                default: return "CC_DefLine_Name_Gate".Translate(dir).Resolve();
            }
        }

        /// <summary>"To the north-east, between two rock faces, 5 cells wide; raiders come from the north."</summary>
        public static string WhereOf(DefenseLine line, Map map, DefenseSurvey s)
        {
            string dir = s != null ? DefenseAdvice.Where(s, line.gate) : DefenseSurvey.FacingWord(line.facing);
            string what = null;
            var choke = s?.chokes.FirstOrDefault(c => c.cell.DistanceTo(line.gate) <= 4f || c.c.span.Any(i => s.grid != null && new IntVec3(i % s.grid.w, 0, i / s.grid.w).DistanceTo(line.gate) <= 2f));
            if (choke != null) what = s.GapWords(choke);
            else
                switch (line.place)
                {
                    case "pass": what = "CC_Def_Gap_Pass".Translate(); break;
                    case "bridge": what = "CC_Def_Gap_Bridge".Translate(); break;
                    case "gate": what = "CC_Def_Gap_Gate".Translate(); break;
                    case "open": what = "CC_DefLine_Place_Open".Translate(); break;
                    default: what = "CC_Def_Gap_Other".Translate(); break;
                }
            return "CC_DefLine_Where".Translate(dir, what, DefenseSurvey.Cells(Mathf.Max(1, line.width)), DefenseSurvey.FacingWord(line.facing)).Resolve();
        }

        private static string EndWord(byte end)
        {
            switch (end)
            {
                case DefenseMath.Water: return "CC_DefLine_End_Water".Translate();
                case DefenseMath.Wall: return "CC_DefLine_End_Wall".Translate();
                case DefenseMath.Door: return "CC_DefLine_End_Door".Translate();
                default: return "CC_DefLine_End_Rock".Translate();
            }
        }

        /// <summary>Where raiders walk round the line with its gate shut, in words: "the wall stops 4 cells short of the rock to the west".</summary>
        public static string LeakWords(DefenseLineFacts f)
        {
            var leak = f.leak;
            if (leak == null || !leak.found) return null;
            int sign = leak.lateral < 0 ? -1 : 1;
            var row = sign < 0 ? f.rowLeft : f.rowRight;
            string side = f.SideWord(sign);
            string s;
            if (leak.viaDoor)
                s = "CC_DefLine_Leak_Door".Translate(DefenseSurvey.Direction(Compass(f.gate, f.Cell(leak.door)))).Resolve();
            else if (row.open > 0 && row.closed && Mathf.Abs(leak.lateral) <= row.built + row.open + 3)
                s = "CC_DefLine_Leak_Short".Translate(DefenseSurvey.Cells(row.open), EndWord(row.end), side).Resolve();
            else if (row.open > 0 && !row.closed && Mathf.Abs(leak.lateral) <= row.built + row.open + 3)
                s = "CC_DefLine_Leak_Open".Translate(side).Resolve();
            else
                s = "CC_DefLine_Leak_Far".Translate(DefenseSurvey.Cells(Mathf.Max(1, Mathf.Abs(leak.lateral))), side).Resolve();
            if (leak.evenOpen) s += " " + "CC_DefLine_Leak_EvenOpen".Translate().Resolve();
            return s;
        }

        private static int Compass(IntVec3 from, IntVec3 to)
        {
            var d = to - from;
            if (d.x == 0 && d.z == 0) return 0;
            return DefenseMath.Compass((Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f);
        }

        /// <summary>"4 trees, 2 chunks and a wall": what raiders would shelter behind.</summary>
        public static string ShelterWords(DefenseLineFacts f)
        {
            var parts = new List<string>();
            void Add(int n, string one, string many)
            {
                if (n == 1) parts.Add(one.Translate());
                else if (n > 1) parts.Add(many.Translate(n));
            }
            Add(f.trees, "CC_DefLine_Tree", "CC_DefLine_Trees");
            Add(f.plants, "CC_DefLine_Bush", "CC_DefLine_Bushes");
            Add(f.chunks, "CC_DefLine_Chunk", "CC_DefLine_Chunks");
            Add(f.rocks, "CC_DefLine_Rock", "CC_DefLine_Rocks");
            Add(f.walls, "CC_DefLine_OwnWall", "CC_DefLine_OwnWalls");
            Add(f.things, "CC_DefLine_Thing", "CC_DefLine_Things");
            return parts.Count == 0 ? "CC_Def_Nothing".Translate().ToString() : TextUtil.JoinAnd(parts);
        }

        /// <summary>"Complex furniture, Gun turrets": the research a building waits on, or its name.</summary>
        public static string ResearchFor(string defName)
        {
            var d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (d == null) return defName;
            var research = d.researchPrerequisites?.Where(x => x != null && !x.IsFinished).Select(x => x.label).ToList();
            return research != null && research.Count > 0 ? "CC_Def_MissingResearch".Translate(d.label, TextUtil.JoinAnd(research)).Resolve() : d.label;
        }

        // ---------------- Doing things ----------------

        /// <summary>Takes the camera to a place on the line and draws the line on the map.</summary>
        public static void Show(Map map, DefenseLine line, IntVec3 at)
        {
            if (map == null || line == null) return;
            DefenseLines.Select(map, line);
            DefenseOverlay.Show = true;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJump(at.IsValid && at.InBounds(map) ? at : line.gate, map);
        }

        /// <summary>Opens the defense planner with a step's plan placed and turned for the line; the player still clicks to place it.</summary>
        public static void PlanStep(Map map, DefenseLine line, StepResult step)
        {
            var t = step?.template;
            if (map == null || line == null || t == null) return;
            PlanAt(map, line, t, line.gate, line.facing, step.placeAt, step.distance, step.closeGap);
        }

        /// <summary>Opens the planner with one of a finished step's plans placed for the line again: the one it has, drawn for the colony as it is now, or another of the step's.</summary>
        public static void PlanAgain(DefenseLineFacts f, StepResult step, DefenseTemplateDef t)
        {
            if (f == null || step?.def == null || t == null) return;
            PlanAt(f.map, f.line, t, f.line.gate, f.line.facing, step.placeAt, step.def.Worker.DistanceFor(f, step, t), step.closeGap);
        }

        /// <summary>
        /// Opens the planner with a plan placed for the line. A plan in front of the line follows the way raiders walk
        /// up to the gate (<see cref="DefenseApproachPlanner"/>), and says why it stands where it does or why nothing fits.
        /// A plan that adds walls is checked first against the survey's ways in: when its walls would send most raiders
        /// another way, the player is told and chooses (<see cref="AskAboutWay"/>).
        /// </summary>
        public static void PlanAt(Map map, DefenseLine line, DefenseTemplateDef t, IntVec3 at, int facing, string placeAt, int distance, bool closeGap,
            ICollection<string> leaveOut = null, bool checkWay = true)
        {
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var cfg = DefensePlanConfig.For(t).Copy();
            cfg.closeGap = closeGap;
            cfg.leaveOut.Clear();
            if (leaveOut != null)
                foreach (var role in leaveOut) cfg.leaveOut.Add(role);
            var pieces = cfg.leaveOut.Count > 0 ? t.Resolve(map, out _, cfg) : t.ResolveCached(map, out _, cfg);
            IntVec3 anchor;
            int rot;
            bool mirror, fits;
            string why = null;
            if (DefenseApproachPlanner.Applies(t, placeAt))
            {
                var choice = DefenseApproachPlanner.Find(map, t, cfg, pieces, at, facing, distance, line?.span);
                if (!choice.fits)
                {
                    // Nothing on the way in fits: say why, rather than put it somewhere it does no good.
                    Find.WindowStack.Add(new Dialog_MessageBox(choice.why ?? "CC_Plan_Blocked".Translate().ToString(),
                        "CC_DefLine_PlaceMyself".Translate(), () => Designator_PlanDefense.BeginBlank(t, line, at, choice.why),
                        "CC_DefLine_NotNow".Translate(), null, "CC_DefLine_NoFit_Title".Translate(t.LabelCapNow).Resolve()));
                    return;
                }
                anchor = choice.anchor;
                rot = choice.rot;
                mirror = choice.mirror;
                fits = true;
                why = choice.why;
            }
            else fits = DefensePlanner.FindSpotAt(t, map, cfg, pieces, at, facing, placeAt, distance, true, out anchor, out rot, out mirror);
            if (fits && checkWay && line != null && DefenseWayCheck.Worth(t))
            {
                DefenseWayCheck.Result check = null;
                try
                {
                    var trial = cfg.Copy();
                    trial.rotation = rot;
                    trial.mirror = mirror;
                    check = DefenseWayCheck.For(map, line, DefensePlanner.Plan(t, map, anchor, trial, pieces, true));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("defense line: would its walls turn raiders away", e);
                }
                if (check != null && check.TurnedAway)
                {
                    AskAboutWay(map, line, t, at, facing, placeAt, distance, closeGap, cfg, anchor, rot, mirror, why, check);
                    return;
                }
            }
            Designator_PlanDefense.BeginAt(t, anchor, rot, mirror, closeGap, line?.id ?? -1, fits, why, null, cfg.leaveOut);
        }

        /// <summary>
        /// The plan's walls would send most raiders another way: says so, and offers what keeps them coming (the walls
        /// left out, where its sides are all they are and leaving them open keeps the way in), closing the other gap
        /// first, or the plan as it is with the warning shown in the planner.
        /// </summary>
        private static void AskAboutWay(Map map, DefenseLine line, DefenseTemplateDef t, IntVec3 at, int facing, string placeAt, int distance, bool closeGap,
            DefensePlanConfig cfg, IntVec3 anchor, int rot, bool mirror, string why, DefenseWayCheck.Result check)
        {
            string warn = DefenseWayCheck.Words(check, line, map);
            string text = warn;
            // Its sides left open: does that keep them coming?
            bool openKeeps = false;
            if (DefenseWayCheck.WallsAreSides(t) && !cfg.leaveOut.Contains("wall"))
            {
                try
                {
                    var open = cfg.Copy();
                    open.leaveOut.Add("wall");
                    open.rotation = rot;
                    open.mirror = mirror;
                    var openPieces = t.Resolve(map, out _, open);
                    var again = DefenseWayCheck.For(map, line, DefensePlanner.Plan(t, map, anchor, open, openPieces, true));
                    openKeeps = again != null && !again.TurnedAway;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("defense line: the open-sided version", e);
                }
            }
            var s = DefenseSurvey.Kept(map);
            bool closeOther = check.otherGap >= 0 && check.otherLine == null && s != null && check.otherGap < s.chokes.Count;
            if (openKeeps) text += "\n\n" + "CC_DefLine_Away_Open".Translate().Resolve();
            if (closeOther) text += "\n\n" + "CC_DefLine_Away_Close".Translate(check.otherWords).Resolve();
            if (check.otherLine != null) text += "\n\n" + "CC_DefLine_Away_OtherLine".Translate(check.otherWords).Resolve();
            text += "\n\n" + "CC_DefLine_Away_Anyway".Translate().Resolve();
            Action anyway = () => Designator_PlanDefense.BeginAt(t, anchor, rot, mirror, closeGap, line.id, true, why, warn, cfg.leaveOut);
            Action openSides = () => PlanAt(map, line, t, at, facing, placeAt, distance, closeGap, new List<string>(cfg.leaveOut) { "wall" }, false);
            var box = new Dialog_MessageBox(text,
                openKeeps ? "CC_DefLine_Away_OpenButton".Translate().ToString() : "CC_DefLine_Away_AnywayButton".Translate().ToString(),
                openKeeps ? openSides : anyway,
                openKeeps ? "CC_DefLine_Away_AnywayButton".Translate().ToString() : "CC_DefLine_NotNow".Translate().ToString(),
                openKeeps ? anyway : null,
                "CC_DefLine_Away_Title".Translate().Resolve());
            if (closeOther)
            {
                int gap = check.otherGap;
                box.buttonCText = "CC_DefLine_Away_CloseButton".Translate(check.otherWords).Resolve();
                box.buttonCAction = () => Designator_PlanDefense.Begin(DefenseAdvice.GateFor(s, s.chokes[gap]), true, gap);
                box.buttonCClose = true;
            }
            Find.WindowStack.Add(box);
        }

        /// <summary>Marks the field of fire's trees and bushes to be cut, its chunks to be hauled and its loose rock to be mined.</summary>
        public static void ClearField(DefenseLineFacts f)
        {
            var map = f.map;
            var dm = map.designationManager;
            int cut = 0, hauled = 0, mined = 0;
            foreach (var t in f.shelter)
            {
                if (t.Destroyed || !t.Spawned || StepWorker_ClearField.Marked(t, dm)) continue;
                if (t is Plant plant)
                {
                    dm.AddDesignation(new Designation(plant, plant.HarvestableNow && plant.def.plant?.harvestedThingDef != null ? DesignationDefOf.HarvestPlant : DesignationDefOf.CutPlant));
                    cut++;
                }
                else if (t.def.category == ThingCategory.Item && t.def.EverHaulable)
                {
                    dm.AddDesignation(new Designation(t, DesignationDefOf.Haul));
                    hauled++;
                }
                else if (t.def.building != null && t.def.mineable)
                {
                    dm.AddDesignation(new Designation(t.Position, DesignationDefOf.Mine));
                    mined++;
                }
            }
            var parts = new List<string>();
            if (cut > 0) parts.Add("CC_DefLine_Marked_Cut".Translate(cut).Resolve());
            if (hauled > 0) parts.Add("CC_DefLine_Marked_Haul".Translate(hauled).Resolve());
            if (mined > 0) parts.Add("CC_DefLine_Marked_Mine".Translate(mined).Resolve());
            Messages.Message(parts.Count == 0 ? "CC_DefLine_Marked_None".Translate().Resolve() : "CC_DefLine_Marked".Translate(TextUtil.JoinAnd(parts)).Resolve(),
                new LookTargets(f.gate, map), MessageTypeDefOf.TaskCompletion, false);
            f.line.Changed();
        }

        /// <summary>The open cells between the end of the line's wall and the rock on one side, if a few walls close them.</summary>
        public static List<IntVec3> GapCells(DefenseLineFacts f, int sign)
        {
            var row = sign < 0 ? f.rowLeft : f.rowRight;
            var list = new List<IntVec3>();
            if (!row.closed || row.open <= 0 || row.open > DefensePlanner.MaxCarry || row.firstOpen < 0) return list;
            var first = f.Cell(row.firstOpen);
            var step = f.right * sign;
            for (int k = 0; k < row.open; k++) list.Add(first + step * k);
            return list;
        }

        /// <summary>Plans walls in the open cells between the line's end and the rock, of the line's own wall and material.</summary>
        public static void WallOff(DefenseLineFacts f, List<IntVec3> cells)
        {
            var map = f.map;
            var own = f.parts.FirstOrDefault(p => !p.def.IsDoor && p.def.IsWall);
            var def = own?.def ?? ThingDefOf.Wall;
            var stuff = own?.Stuff ?? (def.MadeFromStuff ? MaterialPicker.Pick(def, map, cells.Count, MaterialUse.Wall, null) ?? GenStuff.DefaultStuffFor(def) : null);
            int placed = 0;
            foreach (var c in cells)
                if (TemplatePlanner.TryPlace(def, c, Rot4.North, stuff, map)) placed++;
            Messages.Message(placed > 0 ? "CC_DefLine_WalledOff".Translate(placed, def.label).Resolve() : "CC_DefLine_WalledOff_None".Translate().Resolve(),
                cells.Count > 0 ? new LookTargets(cells[0], map) : LookTargets.Invalid, placed > 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            f.line.Changed();
        }

        // ---------------- The checks ----------------

        private static LineNote Note(DefenseLineReview r, string key, string text, string how, float weight, AlmanacArticleDef article, bool good = false)
        {
            var n = new LineNote { key = key, text = text, how = how, weight = weight, article = article, good = good };
            r.notes.Add(n);
            return n;
        }

        private static LineAction ShowAction(DefenseLineReview r, IntVec3 at) => new LineAction
        {
            label = "CC_Show".Translate(),
            tip = "CC_DefLine_Show_Tip".Translate(),
            act = () => Show(r.f.map, r.line, at)
        };

        private static void Checks(DefenseLineReview r)
        {
            var f = r.f;
            WayMoved(r, f);
            Leak(r, f);
            Sight(r, f);
            Field(r, f);
            Distance(r, f);
            Turrets(r, f);
            Walls(r, f);
            Gate(r, f);
            Dark(r, f);
            Upkeep(r, f);
        }

        /// <summary>
        /// The busiest way in moved after a change (a wall built, a gap closed), within the last day: "Raiders now come by
        /// the north gap", on every line's card, with a look at where. Said once: it goes when the day is out.
        /// </summary>
        private static void WayMoved(DefenseLineReview r, DefenseLineFacts f)
        {
            var comp = DefenseLines.Of(f.map);
            if (comp == null || !comp.WayMovedLately) return;
            string to = comp.WayWords(f.s, comp.wayTo, comp.wayToLine), from = comp.WayWords(f.s, comp.wayFrom, comp.wayFromLine);
            bool here = comp.wayToLine == r.line.id;
            bool gone = comp.wayFromLine == r.line.id;
            string text = here ? "CC_DefLine_WayHere".Translate(from).Resolve()
                : gone ? "CC_DefLine_WayGone".Translate(to).Resolve()
                : "CC_DefLine_WayMoved".Translate(to, from).Resolve();
            var n = Note(r, "way", text, "CC_DefLine_How_Way".Translate(), here ? 2f : 8f, A("Rimfolk_AlmDef_RaiderWays", "Rimfolk_AlmDef_Lines"), here);
            if (!here && comp.wayTo.IsValid) n.actions.Add(ShowAction(r, comp.wayTo));
        }

        private static void Leak(DefenseLineReview r, DefenseLineFacts f)
        {
            if (!f.Closed) return;
            var leak = f.leak;
            var page = A("Rimfolk_AlmDef_Lines", "Rimfolk_AlmDef_ChokePoints");
            if (leak.found && !leak.viaDoor)
            {
                int sign = leak.lateral < 0 ? -1 : 1;
                var gap = GapCells(f, sign);
                // Walls planned there already: it's closed once they're built.
                bool planned = gap.Count > 0 && gap.All(c => DefenseLineFacts.PlannedWallOrDoor(c, f.map));
                var n = Note(r, "leak", LeakWords(f) + (planned ? " " + "CC_DefLine_Leak_Planned".Translate().Resolve() : ""), "CC_DefLine_How_Leak".Translate(), planned ? 4f : 10f, page);
                n.cells.AddRange(leak.path.Select(f.Cell));
                if (!planned && gap.Count > 0 && Mathf.Abs(leak.lateral) <= (sign < 0 ? f.rowLeft : f.rowRight).built + gap.Count + 3)
                    n.actions.Add(new LineAction
                    {
                        label = "CC_DefLine_WallOff".Translate(),
                        tip = "CC_DefLine_WallOff_Tip".Translate(gap.Count).Resolve(),
                        act = () => WallOff(f, gap)
                    });
                var wall = DefDatabase<DefenseTemplateDef>.GetNamedSilentFail("Rimfolk_Defense_Wall");
                var cross = f.Cell(leak.cross);
                if (wall != null && cross.IsValid)
                    n.actions.Add(new LineAction
                    {
                        label = "CC_DefLine_PlanWall".Translate(),
                        tip = "CC_DefLine_PlanWall_Tip".Translate(),
                        act = () => PlanAt(f.map, r.line, wall, cross, r.line.facing, "line", 0, true)
                    });
                n.actions.Add(ShowAction(r, cross.IsValid ? cross : f.gate));
            }
            else if (leak.found && leak.viaDoor)
            {
                var door = f.Cell(leak.door);
                var n = Note(r, "leak", LeakWords(f), "CC_DefLine_How_LeakDoor".Translate(), 4f, page);
                n.actions.Add(ShowAction(r, door));
            }
            else if (leak.longWay && !leak.viaDoor)
            {
                // Too long to matter while the gate is open; the way they take when it's shut.
                var cross = f.Cell(leak.cross);
                int extra = Mathf.Max(1, (leak.detour - Mathf.Max(0, leak.direct)) / DefenseMath.Ortho);
                var n = Note(r, "leak", "CC_DefLine_Leak_Long".Translate(extra, f.SideWord(leak.lateral < 0 ? -1 : 1)).Resolve(), "CC_DefLine_How_Leak".Translate(), 3f, page);
                n.actions.Add(ShowAction(r, cross.IsValid ? cross : f.gate));
            }
            else if (leak.sealedShut || leak.longWay) Note(r, "leak", "CC_DefLine_Leak_Sealed".Translate(), "CC_DefLine_How_Leak".Translate(), 1f, page, true);
            else if (leak.noWayOn) Note(r, "leak", "CC_DefLine_Leak_NoWayOn".Translate(), "CC_DefLine_How_NoWayOn".Translate(), 3f, A("Rimfolk_AlmDef_KillboxWhere", "Rimfolk_AlmDef_ChokePoints"));
        }

        private static void Sight(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.firing.Count == 0) return;
            var page = A("Rimfolk_AlmDef_FieldOfFire", "Rimfolk_AlmDef_Cover");
            var blind = f.firing.Where(p => !p.sees).ToList();
            if (blind.Count > 0)
            {
                var n = Note(r, "sight", "CC_DefLine_Sight_Blind".Translate(blind.Count, f.firing.Count).Resolve(), "CC_DefLine_How_Sight".Translate(), 5f + blind.Count, page);
                n.cells.AddRange(blind.Select(p => p.cell));
                n.actions.Add(ShowAction(r, blind[0].cell));
            }
            float reach = f.bestRange;
            int outOfReach = reach > 0f ? f.firing.Count(p => p.sees && p.dist > reach) : 0;
            if (outOfReach > 0)
                Note(r, "range", "CC_DefLine_Sight_Range".Translate(outOfReach, reach.ToString("0")).Resolve(), "CC_DefLine_How_Range".Translate(), 4f, page);
            if (f.seeing >= StepWorker_Cover.Need(f) && blind.Count == 0)
                Note(r, "sight", "CC_DefLine_Sight_Good".Translate(f.seeing, f.coverAt, reach > 0f ? reach.ToString("0") : "?").Resolve(), "CC_DefLine_How_Sight".Translate(), 1f, page, true);
        }

        private static void Field(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.shelter.Count == 0) return;
            var n = Note(r, "field", "CC_DefLine_Field".Translate(ShelterWords(f)).Resolve(), "CC_DefLine_How_Field".Translate(), 6f + Mathf.Min(4f, f.shelter.Count / 2f),
                A("Rimfolk_AlmDef_FieldOfFire", "Rimfolk_AlmDef_Cover"));
            n.cells.AddRange(f.shelter.Select(t => t.Position));
            var dm = f.map.designationManager;
            if (f.shelter.Any(t => !StepWorker_ClearField.Marked(t, dm) && (t is Plant || t.def.category == ThingCategory.Item || t.def.mineable)))
                n.actions.Add(new LineAction { label = "CC_DefLine_Field_Fix".Translate(), tip = "CC_DefLine_Field_Fix_Tip".Translate(), act = () => ClearField(f) });
            n.actions.Add(ShowAction(r, f.shelter[0].Position));
        }

        private static void Distance(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.seeing == 0 || f.coverAt <= 0) return;
            var page = A("Rimfolk_AlmDef_FieldOfFire", "Rimfolk_AlmDef_Cover");
            var reach = DefenseLineMath.ReachOf(f.coverAt, f.bestRange);
            var cover = r.steps.FirstOrDefault(s => s.def.Worker is StepWorker_Cover);
            // Where the cover plan would put its shooters: a drawn one where its layout holds, else the review's own distance.
            int better = StepWorker_Cover.Depth(f, cover?.template);
            LineNote n = null;
            switch (reach)
            {
                case DefenseLineMath.Reach.TooClose:
                    n = Note(r, "distance", "CC_DefLine_Dist_Close".Translate(f.coverAt, better).Resolve(), "CC_DefLine_How_Distance".Translate(), 5f, page);
                    break;
                case DefenseLineMath.Reach.Far:
                    n = Note(r, "distance", "CC_DefLine_Dist_Far".Translate(f.coverAt, better).Resolve(), "CC_DefLine_How_Distance".Translate(), 3f, page);
                    break;
                case DefenseLineMath.Reach.TooFar:
                    n = Note(r, "distance", "CC_DefLine_Dist_TooFar".Translate(f.coverAt, f.bestRange.ToString("0"), better).Resolve(), "CC_DefLine_How_Distance".Translate(), 6f, page);
                    break;
            }
            if (n != null && cover?.template != null)
                n.actions.Add(new LineAction
                {
                    label = "CC_DefLine_PlanCover".Translate(better).Resolve(),
                    tip = "CC_DefLine_PlanCover_Tip".Translate(),
                    act = () => PlanStep(f.map, r.line, cover)
                });
        }

        private static void Turrets(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.turrets.Count == 0) return;
            var parts = new List<string>();
            var cells = new List<IntVec3>();
            var said = new HashSet<Building_Turret>();
            foreach (var t in f.turrets)
            {
                string trouble = DefenseLineFacts.TurretTrouble(t);
                if (trouble != null)
                {
                    parts.Add(trouble);
                    cells.Add(t.Position);
                }
                float radius = t.def.GetCompProperties<CompProperties_Explosive>()?.explosiveRadius ?? 0f;
                if (radius <= 0f) continue;
                var other = f.turrets.FirstOrDefault(o => o != t && !said.Contains(o) && o.Position.DistanceTo(t.Position) <= radius);
                if (other != null && said.Add(t))
                {
                    parts.Add("CC_DefLine_Turret_Chain".Translate(t.LabelShort, other.LabelShort, radius.ToString("0.#")).Resolve());
                    cells.Add(t.Position);
                }
                int shooters = f.firing.Count(p => p.cell.DistanceTo(t.Position) <= radius);
                if (shooters > 0 && said.Add(t))
                {
                    parts.Add("CC_DefLine_Turret_Shooters".Translate(t.LabelShort, shooters, radius.ToString("0.#")).Resolve());
                    cells.Add(t.Position);
                }
            }
            if (parts.Count == 0) return;
            var n = Note(r, "turrets", string.Join(" ", parts.Distinct()), "CC_DefLine_How_Turrets".Translate(), 7f, A("Rimfolk_AlmDef_Turrets"));
            n.cells.AddRange(cells);
            n.actions.Add(ShowAction(r, cells[0]));
        }

        private static void Walls(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.parts.Count == 0 || f.wood * 10 < f.parts.Count * 3 || r.line.aim == "archers" && f.pre) return;
            Note(r, "wood", "CC_DefLine_Walls_Wood".Translate(f.wood, f.parts.Count).Resolve(), "CC_DefLine_How_Wood".Translate(), 4f, A("Rimfolk_AlmDef_WallsGates"));
        }

        /// <summary>
        /// The gate's state, from the door itself: held open, it brings raiders in; shut, it's still the line's way
        /// through (the survey counts it so), but raiders break it if there's no other way, so hold it open in a raid.
        /// More than one door in the line's wall: raiders count each as a way through.
        /// </summary>
        private static void Gate(DefenseLineReview r, DefenseLineFacts f)
        {
            if (f.door == null || !f.Closed) return;
            var page = A("Rimfolk_AlmDef_RaiderWays", "Rimfolk_AlmDef_Lines", "Rimfolk_AlmDef_Killbox");
            bool killbox = r.line.aim == "killbox" || StepWorker_Killbox.Built(f) || f.killboxMouth;
            if (f.doors > 1) Note(r, "doors", "CC_DefLine_Gate_Doors".Translate(f.doors).Resolve(), "CC_DefLine_How_Gate".Translate(), 1.5f, page);
            if (f.door.HoldOpen)
            {
                Note(r, "gate", "CC_DefLine_Gate_Open".Translate(f.door.LabelShort).Resolve(), "CC_DefLine_How_Gate".Translate(), 0.5f, page, true);
                return;
            }
            var n = Note(r, "gate", (killbox ? "CC_DefLine_Gate_ShutKillbox" : "CC_DefLine_Gate_Shut").Translate(f.door.LabelShort).Resolve(),
                "CC_DefLine_How_Gate".Translate(), 2f, page);
            var door = f.door;
            n.actions.Add(new LineAction
            {
                label = "CC_DefLine_SelectGate".Translate(),
                tip = "CC_DefLine_SelectGate_Tip".Translate(),
                act = () =>
                {
                    Find.MainTabsRoot.EscapeCurrentTab(false);
                    CameraJumper.TryJumpAndSelect(door);
                }
            });
        }

        /// <summary>
        /// Darkness spoils nobody's aim by itself in 1.6 (it slows movement and work); only a belief that prefers combat
        /// in darkness changes shooting, better at targets in the dark and worse at lit ones. So this speaks only when
        /// some of the colony's shooters hold it: whether the kill zone is dark at night, where they'd have the edge.
        /// </summary>
        private static void Dark(DefenseLineReview r, DefenseLineFacts f)
        {
            var page = A("Rimfolk_AlmDef_FieldOfFire", "Rimfolk_AlmDef_Cover");
            if (f.darkBetter <= 0)
            {
                // Nobody's aim depends on it; the dark still slows people at their posts and wears on them.
                if (f.firingDark > 0 && f.firingDark * 2 >= f.seeing)
                    Note(r, "dark", "CC_DefLine_Dark_Posts".Translate(f.firingDark, f.seeing).Resolve(), "CC_DefLine_How_Dark".Translate(), 2f, page);
                return;
            }
            if (f.darkShare < 0.5f)
                Note(r, "dark", "CC_DefLine_Dark_Lit".Translate(f.darkBetter, Mathf.RoundToInt((1f - f.darkShare) * 100f)).Resolve(), "CC_DefLine_How_Dark".Translate(), 3f, page);
            else
                Note(r, "dark", "CC_DefLine_Dark_Good".Translate(f.darkBetter).Resolve(), "CC_DefLine_How_Dark".Translate(), 0.5f, page, true);
        }

        private static void Upkeep(DefenseLineReview r, DefenseLineFacts f)
        {
            var parts = new List<string>();
            var home = f.map.areaManager.Home;
            var damaged = f.damaged.Where(t => t.Spawned).ToList();
            if (damaged.Count > 0) parts.Add("CC_DefLine_Upkeep_Damaged".Translate(damaged.Count).Resolve());
            var outside = damaged.Where(t => !home[t.Position]).ToList();
            if (outside.Count > 0) parts.Add("CC_DefLine_Upkeep_OutsideHome".Translate(outside.Count).Resolve());
            if (f.trapsWaiting > 0) parts.Add("CC_DefLine_Upkeep_Traps".Translate(f.trapsWaiting).Resolve());
            if (f.corpses.Count > 0) parts.Add("CC_DefLine_Upkeep_Bodies".Translate(f.corpses.Count).Resolve());
            if (parts.Count == 0) return;
            var n = Note(r, "upkeep", string.Join(" ", parts), "CC_DefLine_How_Upkeep".Translate(), 5f, A("Rimfolk_AlmDef_Upkeep", "Rimfolk_AlmDef_WallsGates"));
            n.cells.AddRange(damaged.Select(t => t.Position));
            if (outside.Count > 0)
                n.actions.Add(new LineAction
                {
                    label = "CC_DefLine_AddHome".Translate(),
                    tip = "CC_DefLine_AddHome_Tip".Translate(),
                    act = () =>
                    {
                        foreach (var t in outside)
                            foreach (var c in t.OccupiedRect())
                                if (c.InBounds(f.map)) home[c] = true;
                        Messages.Message("CC_DefLine_AddHome_Done".Translate(outside.Count).Resolve(), MessageTypeDefOf.TaskCompletion, false);
                        r.line.Changed();
                    }
                });
            var bodies = f.corpses.Where(c => c.Spawned && f.map.designationManager.DesignationOn(c) == null).ToList();
            if (bodies.Count > 0)
                n.actions.Add(new LineAction
                {
                    label = "CC_DefLine_HaulBodies".Translate(),
                    tip = "CC_DefLine_HaulBodies_Tip".Translate(),
                    act = () =>
                    {
                        foreach (var c in bodies)
                            if (c.Spawned && f.map.designationManager.DesignationOn(c) == null) f.map.designationManager.AddDesignation(new Designation(c, DesignationDefOf.Haul));
                        r.line.Changed();
                    }
                });
            n.actions.Add(ShowAction(r, damaged.FirstOrDefault()?.Position ?? f.gate));
        }

        // ---------------- Strength ----------------

        private static void Strength(DefenseLineReview r)
        {
            var f = r.f;
            var input = new DefenseLineMath.StrengthIn
            {
                fighters = f.fighters,
                positions = f.seeing,
                turrets = f.turretsWorking,
                turretWorth = f.turretWorth,
                traps = f.traps,
                killbox = StepWorker_Killbox.Built(f),
                closed = f.Closed && !f.Leaks,
                leak = f.Leaks,
                obstacles = f.shelter.Count,
                reach = f.seeing > 0 && f.coverAt > 0 ? DefenseLineMath.ReachOf(f.coverAt, f.bestRange) : DefenseLineMath.Reach.Good
            };
            r.strength = DefenseLineMath.Strength(input);
            r.points = DefenseLineMath.Rounded(r.strength.total);
            r.verdict = DefenseLineMath.Verdict(r.strength.total, f.raidPoints);
        }

        private string strengthText, strengthTip;

        public string StrengthText() => strengthText ??= MakeStrengthText();

        /// <summary>The strength's parts, for its tooltip: so the number can be checked.</summary>
        public string StrengthTip() => strengthTip ??= MakeStrengthTip();

        private string MakeStrengthText()
        {
            string raids = DefenseLineMath.Rounded(f.raidPoints).ToString("N0");
            string pts = points.ToString("N0");
            switch (verdict)
            {
                case 2: return "CC_DefLine_Strength_Well".Translate(pts, raids).Resolve();
                case 1: return "CC_DefLine_Strength_Holds".Translate(pts, raids).Resolve();
                case 0: return "CC_DefLine_Strength_Close".Translate(pts, raids).Resolve();
                default: return "CC_DefLine_Strength_Weak".Translate(pts, raids).Resolve();
            }
        }

        private string MakeStrengthTip()
        {
            var s = strength;
            if (s == null) return "";
            var lines = new List<string>
            {
                "CC_DefLine_StrTip_People".Translate(f.fighters.Count, s.atPosts, Mathf.RoundToInt(s.people)).Resolve()
            };
            if (s.turrets > 0f) lines.Add("CC_DefLine_StrTip_Turrets".Translate(f.turretsWorking, Mathf.RoundToInt(s.turrets)).Resolve());
            if (s.traps > 0f) lines.Add("CC_DefLine_StrTip_Traps".Translate(Mathf.Min(f.traps, DefenseLineMath.MaxTraps), Mathf.RoundToInt(s.traps)).Resolve());
            var why = new List<string>();
            if (StepWorker_Killbox.Built(f)) why.Add("CC_DefLine_StrTip_Killbox".Translate());
            if (f.Leaks) why.Add("CC_DefLine_StrTip_Leak".Translate());
            else if (f.Closed) why.Add("CC_DefLine_StrTip_Closed".Translate());
            if (f.shelter.Count > 0) why.Add("CC_DefLine_StrTip_Field".Translate(f.shelter.Count).Resolve());
            lines.Add("CC_DefLine_StrTip_Factor".Translate(s.factor.ToString("0.00"), why.Count > 0 ? TextUtil.JoinAnd(why) : "CC_DefLine_StrTip_Nothing".Translate().ToString()).Resolve());
            lines.Add("CC_DefLine_StrTip_Rough".Translate());
            return string.Join("\n", lines);
        }
    }
}
