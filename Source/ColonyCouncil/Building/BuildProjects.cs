using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A room placed as blueprints (by a building-ahead step, or by the player with the room planner), followed in
    /// stages until it stands: its footprint, the kind of room it is for, what it adds while it is being built, and the
    /// side it keeps free to grow into (remembered as long as its walls stand).
    /// </summary>
    public class BuildProject : IExposable
    {
        public int id;
        public Map map;
        public RoomTemplateDef template;
        public string label;
        /// <summary>The kind of room it is (GrowthMath.Beds, Cold...), or null for another room.</summary>
        public string kind;
        /// <summary>The beds, seats, places or bench places it adds, counted as planned while it is being built.</summary>
        public int adds;
        /// <summary>Its footprint, walls included.</summary>
        public int x, z, w, h;
        /// <summary>The side kept free to grow into (GrowthMath.North...; -1 none) and the cells kept.</summary>
        public int growSide = -1, growX, growZ, growW, growH;
        public int placedTick, doneTick = -1;
        /// <summary>Its building work when it was placed, for the check whether it is going.</summary>
        public float workAtStart;

        public bool Done => doneTick >= 0;
        public CellRect Rect => new CellRect(x, z, w, h);
        public bool KeepsSide => growSide >= 0 && growW > 0 && growH > 0;
        public (int x, int z, int w, int h) Strip => (growX, growZ, growW, growH);
        public CellRect StripRect => new CellRect(growX, growZ, growW, growH);
        public string Label => label ?? template?.label ?? "?";

        public bool InStrip(IntVec3 c) => KeepsSide && GrowthMath.Inside(c.x, c.z, Strip);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref map, "map");
            CouncilScribe.LookDef(ref template, "template");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref adds, "adds");
            Scribe_Values.Look(ref x, "x");
            Scribe_Values.Look(ref z, "z");
            Scribe_Values.Look(ref w, "w");
            Scribe_Values.Look(ref h, "h");
            Scribe_Values.Look(ref growSide, "growSide", -1);
            Scribe_Values.Look(ref growX, "growX");
            Scribe_Values.Look(ref growZ, "growZ");
            Scribe_Values.Look(ref growW, "growW");
            Scribe_Values.Look(ref growH, "growH");
            Scribe_Values.Look(ref placedTick, "placedTick");
            Scribe_Values.Look(ref doneTick, "doneTick", -1);
            Scribe_Values.Look(ref workAtStart, "workAtStart");
        }
    }

    /// <summary>
    /// What building ahead remembers (CouncilGameComponent.buildAhead): the rooms placed and followed, and the most people
    /// laid up at once on each of the last days (for the hospital beds). Read and kept hourly through
    /// <see cref="BuildAhead.Hourly"/>.
    /// </summary>
    public class BuildAheadTracker : IExposable
    {
        public List<BuildProject> projects = new List<BuildProject>();
        public int nextId = 1;
        /// <summary>The most laid up at once, a day each, the newest last; and the day of the newest.</summary>
        public List<int> patients = new List<int>();
        public int patientsDay = -1;

        /// <summary>Rooms followed at most (the oldest finished go first), and days of patients kept.</summary>
        public const int MaxProjects = 16, PatientDays = 15;

        public static BuildAheadTracker Instance => CouncilGameComponent.Instance?.buildAhead;

        /// <summary>Goes up whenever a room is added or forgotten, so the page works its suggestions out again (not saved).</summary>
        public int Version { get; private set; }

        public BuildProject Find(int id) => projects.FirstOrDefault(p => p.id == id);

        public IEnumerable<BuildProject> On(Map map) => projects.Where(p => p.map == map);

        public BuildProject Add(BuildProject p)
        {
            p.id = nextId++;
            projects.Add(p);
            while (projects.Count > MaxProjects)
            {
                var old = projects.FirstOrDefault(q => q.Done) ?? projects[0];
                projects.Remove(old);
            }
            Version++;
            return p;
        }

        public void Forget(int id)
        {
            if (projects.RemoveAll(p => p.id == id) > 0) Version++;
        }

        public void Changed() => Version++;

        /// <summary>Notes how many are laid up now, as the day's most.</summary>
        public void NotePatients(int now, int day)
        {
            if (patientsDay != day || patients.Count == 0)
            {
                patients.Add(now);
                patientsDay = day;
            }
            else patients[patients.Count - 1] = Mathf.Max(patients[patients.Count - 1], now);
            SuggestMath.TrimOldest(patients, PatientDays);
        }

        public int MostPatients => patients.Count == 0 ? 0 : patients.Max();

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) projects.RemoveAll(p => p == null || p.map == null);
            Scribe_Collections.Look(ref projects, "projects", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref patients, "patients", LookMode.Value);
            Scribe_Values.Look(ref patientsDay, "patientsDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                projects ??= new List<BuildProject>();
                projects.RemoveAll(p => p == null || p.map == null);
                patients ??= new List<int>();
                SuggestMath.TrimOldest(patients, PatientDays);
            }
        }
    }

    /// <summary>A placed room as it stands now: the pieces left in each stage, the work left, what it still needs and what is in the way.</summary>
    public class ProjectState
    {
        public BuildProject project;
        /// <summary>Pieces left per stage: clearing, walls (doors and roof), climate and power, floors, furniture.</summary>
        public int[] left = new int[GrowthMath.Finished];
        public int stage = GrowthMath.Finished;
        /// <summary>Building work left in its blueprints and frames.</summary>
        public float workLeft;
        public List<Thing> pieces = new List<Thing>(), stagePieces = new List<Thing>();
        /// <summary>Materials its blueprints and frames still need, all of them and those of the stage at hand.</summary>
        public Dictionary<ThingDef, int> needs = new Dictionary<ThingDef, int>(), stageNeeds = new Dictionary<ThingDef, int>();
        /// <summary>Clearing marked: plants to cut, rock to mine, things to take down; queued pieces waiting for it.</summary>
        public int plants, rocks, removals, queued;
        /// <summary>Cells inside without a roof: marked for one, and not marked.</summary>
        public int roofMarked, roofless;
        public List<IntVec3> rooflessCells = new List<IntVec3>();
        /// <summary>The footprint's wall cells, and those with a wall or door standing.</summary>
        public int walls, wallsStanding;
        public List<BuildableDef> waitingResearch = new List<BuildableDef>();

        public bool Finished => stage == GrowthMath.Finished;
        public float Hours => GrowthMath.BuildHours(workLeft);
        public bool AnythingLeft => pieces.Count > 0 || left[GrowthMath.Clear] > 0 || queued > 0;
        public int PiecesLeft => left.Sum();
    }

    /// <summary>Reading a placed room's stages from the map, and what holds it up.</summary>
    public static class BuildProjects
    {
        /// <summary>The stage a piece belongs to: walls and doors; coolers, heaters, vents, conduits and power; floors; the rest is furniture.</summary>
        public static int StageOf(BuildableDef def)
        {
            if (def is TerrainDef) return GrowthMath.Floors;
            if (!(def is ThingDef td)) return GrowthMath.Furniture;
            if (td.IsWall || td.IsDoor || TemplatePlanner.IsWallLike(td)) return GrowthMath.Walls;
            if (Avail.IsCooler(td) || Avail.IsHeater(td) || td.thingClass != null && td.thingClass.Name.IndexOf("Vent", StringComparison.Ordinal) >= 0) return GrowthMath.Climate;
            if (td.EverTransmitsPower && !td.IsWall) return GrowthMath.Climate;
            if (td.comps != null)
                foreach (var c in td.comps)
                {
                    if (c is CompProperties_TempControl || c is CompProperties_Battery) return GrowthMath.Climate;
                    if (c?.compClass != null && typeof(CompPowerPlant).IsAssignableFrom(c.compClass)) return GrowthMath.Climate;
                }
            return GrowthMath.Furniture;
        }

        /// <summary>Building work left in a blueprint or frame.</summary>
        public static float WorkOf(Thing t)
        {
            try
            {
                if (t is Frame f) return Mathf.Max(0f, f.WorkLeft);
                if (t is Blueprint b && b.def.entityDefToBuild != null) return b.def.entityDefToBuild.GetStatValueAbstract(StatDefOf.WorkToBuild, b.EntityToBuildStuff());
            }
            catch
            {
                // A modded piece whose work can't be read counts for nothing rather than breaking the page.
            }
            return 0f;
        }

        /// <summary>Building work of a piece not yet placed.</summary>
        public static float WorkOf(BuildableDef def, ThingDef stuff)
        {
            if (def == null) return 0f;
            try
            {
                return def.GetStatValueAbstract(StatDefOf.WorkToBuild, def.MadeFromStuff ? stuff : null);
            }
            catch
            {
                return 0f;
            }
        }

        private static void AddNeed(Dictionary<ThingDef, int> into, ThingDef def, int n)
        {
            if (def == null || n <= 0) return;
            into.TryGetValue(def, out int had);
            into[def] = had + n;
        }

        public static ProjectState Read(BuildProject p)
        {
            var st = new ProjectState { project = p };
            var map = p?.map;
            if (map == null) return st;
            var player = Faction.OfPlayerSilentFail;
            var dm = map.designationManager;
            var rect = p.Rect.ClipInsideMap(map);
            var seen = new HashSet<Thing>();
            var stageOfPiece = new Dictionary<Thing, int>();
            foreach (var c in rect)
            {
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    var t = things[i];
                    if ((t is Blueprint || t is Frame) && t.Faction == player && t.def.entityDefToBuild != null && seen.Add(t))
                    {
                        int s = StageOf(t.def.entityDefToBuild);
                        st.left[s]++;
                        st.pieces.Add(t);
                        stageOfPiece[t] = s;
                        st.workLeft += WorkOf(t);
                    }
                    else if (t is Plant plant && (dm.DesignationOn(plant, DesignationDefOf.CutPlant) != null || dm.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null))
                        st.plants++;
                }
                if (dm.DesignationAt(c, DesignationDefOf.Mine) != null) st.rocks++;
                var ed = c.GetEdifice(map);
                if (ed != null && dm.DesignationOn(ed, DesignationDefOf.Deconstruct) != null) st.removals++;
                bool edge = c.x == p.x || c.z == p.z || c.x == p.x + p.w - 1 || c.z == p.z + p.h - 1;
                if (edge)
                {
                    st.walls++;
                    if (ed != null && (TemplatePlanner.IsWallLike(ed.def) || ed.def.IsDoor)) st.wallsStanding++;
                }
            }
            // Pieces queued until the ground is clear: they come in their own stage.
            var queue = map.GetComponent<CouncilPlanQueue>();
            if (queue != null)
                foreach (var q in queue.queue)
                {
                    if (q?.Def == null || !p.Rect.Contains(q.cell)) continue;
                    st.queued++;
                    st.left[StageOf(q.Def)]++;
                    if (!q.Def.IsResearchFinished) st.waitingResearch.Add(q.Def);
                }
            st.left[GrowthMath.Clear] += st.plants + st.rocks + st.removals;
            // The roof once the walls stand: the game marks an enclosed room's roof to build by itself in the home area.
            if (st.left[GrowthMath.Walls] == 0 && st.wallsStanding > 0)
            {
                var roofArea = map.areaManager.BuildRoof;
                foreach (var c in p.Rect.ContractedBy(1))
                {
                    if (!c.InBounds(map) || map.roofGrid.Roofed(c)) continue;
                    var room = c.GetRoom(map);
                    if (room == null || room.TouchesMapEdge || room.CellCount > p.w * p.h) continue;
                    if (roofArea != null && roofArea[c]) st.roofMarked++;
                    else
                    {
                        st.roofless++;
                        st.rooflessCells.Add(c);
                    }
                }
                st.left[GrowthMath.Walls] += st.roofMarked;
            }
            st.stage = GrowthMath.StageOf(st.left);
            foreach (var t in st.pieces)
            {
                bool inStage = stageOfPiece.TryGetValue(t, out int s) && s == st.stage;
                if (inStage) st.stagePieces.Add(t);
                if (!(t is IConstructible ic)) continue;
                List<ThingDefCountClass> cost;
                try
                {
                    cost = ic.TotalMaterialCost();
                }
                catch
                {
                    continue;
                }
                if (cost == null) continue;
                foreach (var need in cost)
                {
                    if (need?.thingDef == null) continue;
                    int n = ic.ThingCountNeeded(need.thingDef);
                    AddNeed(st.needs, need.thingDef, n);
                    if (inStage) AddNeed(st.stageNeeds, need.thingDef, n);
                }
            }
            return st;
        }

        /// <summary>The colony's builders: those here with Construction switched on, not down.</summary>
        public static List<Pawn> Builders(Map map) =>
            ColonyNeeds.Hands(map).Where(p => !p.Dead && !p.Downed && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                                              && p.workSettings.WorkIsActive(WorkTypeDefOf.Construction)).ToList();

        private static int Skill(Pawn p, SkillDef s) => p.skills?.GetSkill(s)?.Level ?? 0;

        /// <summary>Who would do a kind of work best, of those who can and don't have it switched on; null when nobody can.</summary>
        private static Pawn BestFor(Map map, WorkTypeDef work, SkillDef skill) =>
            ColonyNeeds.Hands(map).Where(p => !p.Dead && p.workSettings != null && !p.WorkTypeIsDisabled(work)).OrderByDescending(p => skill != null ? Skill(p, skill) : 0)
                .ThenBy(p => p.thingIDNumber).FirstOrDefault();

        private static bool AnyActive(Map map, WorkTypeDef work) =>
            ColonyNeeds.Hands(map).Any(p => !p.Dead && !p.Downed && p.workSettings != null && !p.WorkTypeIsDisabled(work) && p.workSettings.WorkIsActive(work));

        private static string StageName(int stage) => ("CC_Build_Stage_" + GrowthMath.Stages[Mathf.Clamp(stage, 0, GrowthMath.Finished)]).Translate().Resolve();

        /// <summary>
        /// What holds a placed room up, in this order: nobody here builds (or cuts the plants, or mines the rock, in its
        /// way); the roof isn't marked; its pieces need more skill than the builders have; it can't be reached; its
        /// pieces are forbidden; the materials the stage needs aren't here; queued pieces wait on research; the
        /// builders have more other building than they do in a couple of days. Each with its fix where one exists.
        /// Null when nothing holds it up.
        /// </summary>
        public static StuckReason Why(ProjectState st, float otherHours, float hoursADay)
        {
            var p = st?.project;
            var map = p?.map;
            if (map == null || st.Finished) return null;
            string room = p.Label;
            SuggestCheck Check() => new BuildStageCheck { projectId = p.id, stage = st.stage, leftBefore = st.left[Mathf.Min(st.stage, GrowthMath.Furniture)] };

            // Who builds.
            if (!AnyActive(map, WorkTypeDefOf.Construction))
            {
                var best = BestFor(map, WorkTypeDefOf.Construction, SkillDefOf.Construction);
                if (best == null) return new StuckReason { key = "nobuilder", text = "CC_Build_Stuck_NoBuilder".Translate(room).Resolve() };
                return BillStuck.LetDo(best, WorkTypeDefOf.Construction, "CC_Build_Stuck_BuildOff".Translate(room).Resolve(), "buildoff", null, null, Check);
            }
            if (st.stage == GrowthMath.Clear)
            {
                if (st.plants > 0 && !AnyActive(map, WorkTypeDefOf.PlantCutting))
                {
                    var best = BestFor(map, WorkTypeDefOf.PlantCutting, SkillDefOf.Plants);
                    if (best != null)
                        return BillStuck.LetDo(best, WorkTypeDefOf.PlantCutting, "CC_Build_Stuck_Plants".Translate(st.plants, room).Resolve(), "cutoff", null, null, Check);
                }
                if (st.rocks > 0 && !AnyActive(map, WorkTypeDefOf.Mining))
                {
                    var best = BestFor(map, WorkTypeDefOf.Mining, SkillDefOf.Mining);
                    if (best != null) return BillStuck.LetDo(best, WorkTypeDefOf.Mining, "CC_Build_Stuck_Rock".Translate(st.rocks, room).Resolve(), "mineoff", null, null, Check);
                }
            }
            // The roof nobody marked.
            if (st.roofless > 0 && st.left[GrowthMath.Walls] == 0)
            {
                var cells = st.rooflessCells.ToList();
                return new StuckReason
                {
                    key = "roof", text = "CC_Build_Stuck_Roof".Translate(st.roofless, room).Resolve(), fixLabel = "CC_Build_Fix_Roof".Translate().Resolve(),
                    fixTip = "CC_Build_Fix_RoofTip".Translate(cells.Count, room).Resolve(),
                    fix = () =>
                    {
                        var area = map.areaManager.BuildRoof;
                        if (area == null) return null;
                        var marked = new List<IntVec3>();
                        foreach (var c in cells)
                            if (c.InBounds(map) && !area[c] && !map.roofGrid.Roofed(c))
                            {
                                area[c] = true;
                                marked.Add(c);
                            }
                        if (marked.Count == 0) return null;
                        return new SuggestDone { undo = new UndoRoofMarked { cells = marked }, check = Check(), map = map };
                    }
                };
            }
            var pieces = st.stagePieces;
            int skill = Builders(map).Select(b => Skill(b, SkillDefOf.Construction)).DefaultIfEmpty(0).Max();
            var hard = pieces.Select(t => t.def.entityDefToBuild).Where(d => d != null && d.constructionSkillPrerequisite > skill).OrderByDescending(d => d.constructionSkillPrerequisite).FirstOrDefault();
            if (hard != null)
                return new StuckReason { key = "skill", text = "CC_Build_Stuck_Skill".Translate(hard.label.Named("PIECE"), hard.constructionSkillPrerequisite.Named("NEED"), skill.Named("HAVE")).Resolve() };
            if (pieces.Count > 0 && !pieces.Any(t => map.reachability.CanReachColony(t.Position)))
                return new StuckReason { key = "reach", text = "CC_Build_Stuck_Reach".Translate(room).Resolve() };
            var player = Faction.OfPlayerSilentFail;
            var forbidden = player == null ? new List<Thing>() : pieces.Where(t => t.IsForbidden(player)).ToList();
            if (forbidden.Count > 0)
                return new StuckReason
                {
                    key = "forbidden", text = "CC_Build_Stuck_Forbidden".Translate(forbidden.Count, room).Resolve(), fixLabel = "CC_Build_Fix_Allow".Translate().Resolve(),
                    fixTip = "CC_Build_Fix_AllowTip".Translate(forbidden.Count, room).Resolve(),
                    fix = () =>
                    {
                        var allowed = forbidden.Where(t => t.Spawned && t.IsForbidden(player)).ToList();
                        if (allowed.Count == 0) return null;
                        foreach (var t in allowed) t.SetForbidden(false, false);
                        return new SuggestDone { undo = new UndoAllowed { things = allowed }, check = Check(), map = map };
                    }
                };
            // The materials the stage at hand needs, against what is on the map and not forbidden.
            var stock = Making.Stock(map);
            var shortOf = new List<string>();
            foreach (var kv in st.stageNeeds.OrderByDescending(k => k.Value))
            {
                stock.TryGetValue(kv.Key, out int have);
                if (have >= kv.Value) continue;
                shortOf.Add("CC_Build_Stuck_ShortOne".Translate((kv.Value - have).Named("SHORT"), kv.Key.label.Named("MATERIAL"), kv.Value.Named("NEED"), have.Named("HAVE")).Resolve());
            }
            if (shortOf.Count > 0)
                return new StuckReason { key = "materials", text = "CC_Build_Stuck_Materials".Translate(StageName(st.stage), room, TextUtil.JoinAnd(shortOf.Take(3).ToList())).Resolve() };
            if (st.waitingResearch.Count > 0)
            {
                var d = st.waitingResearch[0];
                string research = d.researchPrerequisites?.FirstOrDefault(r => r != null && !r.IsFinished)?.LabelCap.ToString() ?? d.label;
                return new StuckReason { key = "research", text = "CC_Build_Stuck_Research".Translate(d.label.Named("PIECE"), research.Named("RESEARCH")).Resolve() };
            }
            if (hoursADay > 0f && otherHours > GrowthMath.BudgetDays * hoursADay)
                return new StuckReason
                {
                    key = "busy", text = "CC_Build_Stuck_Busy".Translate(Mathf.RoundToInt(otherHours).Named("HOURS"), GrowthMath.DaysOfWork(otherHours, hoursADay).ToString("0.#").Named("DAYS"), room.Named("ROOM")).Resolve()
                };
            return null;
        }
    }

    // ---------------- Undoing and checking a project's steps ----------------

    /// <summary>The roof marks a step added, taken off again.</summary>
    public class UndoRoofMarked : SuggestUndo
    {
        public List<IntVec3> cells = new List<IntVec3>();

        public override void Undo(Map map)
        {
            var area = map?.areaManager.BuildRoof;
            if (area == null) return;
            foreach (var c in cells)
                if (c.InBounds(map)) area[c] = false;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }
    }

    /// <summary>Blueprints and frames a step allowed, forbidden again.</summary>
    public class UndoAllowed : SuggestUndo
    {
        public List<Thing> things = new List<Thing>();

        public override bool Possible(Map map) => things.Any(t => t != null && t.Spawned);

        public override void Undo(Map map)
        {
            foreach (var t in things)
                if (t != null && t.Spawned) t.SetForbidden(true, false);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) things.RemoveAll(t => t == null || t.Destroyed);
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                things ??= new List<Thing>();
                things.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>Build priorities a step set, put back as they were.</summary>
    public class UndoBuildPriority : SuggestUndo
    {
        public List<Thing> things = new List<Thing>();
        public List<int> levels = new List<int>();

        public override bool Possible(Map map) => things.Any(t => t != null && t.Spawned);

        public override void Undo(Map map)
        {
            var c = BuildPriorities.Of(map);
            if (c == null) return;
            for (int i = 0; i < things.Count && i < levels.Count; i++)
                if (things[i] != null && things[i].Spawned) c.Set(things[i], levels[i]);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                for (int i = 0; i < things.Count; i++)
                    if (things[i] != null && things[i].Destroyed) things[i] = null;
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Collections.Look(ref levels, "levels", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                things ??= new List<Thing>();
                levels ??= new List<int>();
            }
        }
    }

    /// <summary>Whether a stage step got the room on, a day later: on to a later stage, or a quarter of the stage done; if not, what holds it up.</summary>
    public class BuildStageCheck : SuggestCheck
    {
        public int projectId, stage, leftBefore;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var p = BuildAheadTracker.Instance?.Find(projectId);
            if (p == null || p.Done) return true;
            var st = BuildProjects.Read(p);
            if (GrowthMath.StageMoved(stage, leftBefore, st.stage, st.left[Mathf.Min(stage, GrowthMath.Furniture)])) return true;
            var needs = GrowthNeeds.For(p.map);
            whyNot = BuildProjects.Why(st, Mathf.Max(0f, needs.input.queuedHours - st.Hours), needs.input.builderHoursADay)?.text
                     ?? "CC_Build_NotYet_Stage".Translate(st.left[Mathf.Min(st.stage, GrowthMath.Furniture)], p.Label).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref projectId, "projectId");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref leftBefore, "leftBefore");
        }
    }

    /// <summary>Whether a room placed is going, two days later: its walls stand, or a third of it is built; if not, what holds it up.</summary>
    public class BuildRoomCheck : SuggestCheck
    {
        public int projectId;

        public BuildRoomCheck()
        {
            waitTicks = 2 * SuggestMath.TicksPerDay;
        }

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var p = BuildAheadTracker.Instance?.Find(projectId);
            if (p == null || p.Done) return true;
            var st = BuildProjects.Read(p);
            if (GrowthMath.RoomGoing(st.stage, st.workLeft, p.workAtStart)) return true;
            var needs = GrowthNeeds.For(p.map);
            whyNot = BuildProjects.Why(st, Mathf.Max(0f, needs.input.queuedHours - st.Hours), needs.input.builderHoursADay)?.text
                     ?? "CC_Build_NotYet_Room".Translate(st.wallsStanding, st.walls, p.Label).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref projectId, "projectId");
        }
    }

    /// <summary>Whether the stockpile made beside a site took the materials, a day later: it holds some, or the site needs none of them any more.</summary>
    public class BuildStockCheck : SuggestCheck
    {
        public int projectId, zoneId = -1;
        public ThingDef material;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var map = done.map;
            var zone = map?.zoneManager.AllZones.OfType<Zone_Stockpile>().FirstOrDefault(z => z.ID == zoneId);
            if (zone == null) return true;
            if (zone.AllContainedThings.Any(t => t.def == material)) return true;
            var p = BuildAheadTracker.Instance?.Find(projectId);
            if (p == null || p.Done) return true;
            var st = BuildProjects.Read(p);
            if (material == null || !st.needs.ContainsKey(material)) return true;
            Making.Stock(map).TryGetValue(material, out int have);
            whyNot = have <= 0 ? "CC_Build_NotYet_StockNone".Translate(material.label).Resolve() : "CC_Build_NotYet_StockHaul".Translate(material.label).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref projectId, "projectId");
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            CouncilScribe.LookDef(ref material, "material");
        }
    }
}
