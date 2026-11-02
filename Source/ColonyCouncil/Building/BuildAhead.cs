using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Building ahead (the Plan page's "Building ahead" section): what rooms the colony will be short of
    /// (<see cref="GrowthNeeds"/>, <see cref="GrowthMath"/>), each made a step of the shared suggestion core: the next
    /// one or two rooms to build, the most urgent first, at a spot the room planner's spot finder gives (beside the rooms
    /// it goes with, keeping a side free to grow), placed as blueprints at a click through the planner's own code and
    /// taken away again with Undo; never more than the builders do in a couple of days. A placed room (a step's, or one
    /// the player placed with the planner) is followed in stages: its current stage raised to high build priority, a
    /// stockpile for its materials beside the site, what holds it up with the fix where there is one. A blueprint put
    /// down where a room was to grow gets a warning. Nothing is done until the player clicks.
    /// </summary>
    public static class BuildAhead
    {
        public const string Source = "build";

        /// <summary>A room to build: which plan, at which spot, what it adds, how long it takes, and the side it keeps free.</summary>
        public class RoomPick
        {
            public GrowthIdea idea;
            public RoomTemplateDef template;
            public RoomPlanConfig cfg;
            public List<ResolvedThing> furniture;
            public TemplatePlanner.SpotCandidate spot;
            public PlacementPlan plan;
            public CellRect rect;
            public int capacity, growSide = -1;
            public (int x, int z, int w, int h) strip;
            public float hours;
            /// <summary>Why no plan of the kind can be made yet (research, materials), or that no spot was found.</summary>
            public string unavailable;
            public bool nowhere;

            public bool CanPlace => plan != null && spot != null && unavailable == null && !nowhere;
        }

        /// <summary>What the page draws for one map: the needs, the rooms picked, the projects and every suggestion (before "Not this" is taken out).</summary>
        public class View
        {
            public GrowthNeeds needs;
            public List<RoomPick> rooms = new List<RoomPick>();
            public List<Suggestion> all = new List<Suggestion>();
            /// <summary>Why the next room isn't suggested yet (the builders' couple of days), or null.</summary>
            public string heldBack;
            public int needsTick = -1, version = -1, projectsVersion = -1;
        }

        private static readonly Dictionary<int, View> cache = new Dictionary<int, View>();

        public static View For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new View();
            var needs = GrowthNeeds.For(map, force);
            int version = SuggestTracker.Instance?.Version ?? 0, projects = BuildAheadTracker.Instance?.Version ?? 0;
            if (!force && cache.TryGetValue(map.uniqueID, out var v) && v.needs == needs && v.needsTick == needs.tick && v.version == version && v.projectsVersion == projects) return v;
            v = new View { needs = needs, needsTick = needs.tick, version = version, projectsVersion = projects };
            try
            {
                Rooms(v, map);
                Projects(v, map);
                Growing(v, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("build ahead", e);
            }
            cache[map.uniqueID] = v;
            return v;
        }

        /// <summary>The suggestions still open, the most urgent first.</summary>
        public static List<Suggestion> Open(Map map)
        {
            var v = For(map);
            var tracker = SuggestTracker.Instance;
            return tracker != null ? tracker.Open(v.all) : v.all;
        }

        /// <summary>After a step: read the rooms again at the next look.</summary>
        public static void Refresh(Map map)
        {
            if (map == null) return;
            GrowthNeeds.Forget(map);
            cache.Remove(map.uniqueID);
        }

        // ---------------- The hour ----------------

        /// <summary>The hour (from Ahead.Hourly): the people laid up noted, the rooms followed looked at, finished ones marked, gone ones forgotten.</summary>
        public static void Hourly()
        {
            var tracker = BuildAheadTracker.Instance;
            if (tracker == null) return;
            int patients = 0;
            foreach (var map in Find.Maps)
                if (map.IsPlayerHome) patients += PatientsNow(map);
            tracker.NotePatients(patients, GenDate.DaysPassed);
            int now = Find.TickManager.TicksGame;
            foreach (var p in tracker.projects.ToList())
            {
                if (p.map == null || !Find.Maps.Contains(p.map))
                {
                    tracker.Forget(p.id);
                    continue;
                }
                var st = BuildProjects.Read(p);
                bool standing = st.walls > 0 && st.wallsStanding * 2 >= st.walls;
                if (!p.Done && !st.AnythingLeft && st.Finished)
                {
                    if (!standing)
                    {
                        // Cancelled before it stood: nothing left to follow.
                        tracker.Forget(p.id);
                        continue;
                    }
                    p.doneTick = now;
                    tracker.Changed();
                    Messages.Message("CC_Build_ProjectDone".Translate(p.Label).Resolve(), new LookTargets(p.Rect.CenterCell, p.map), MessageTypeDefOf.PositiveEvent, false);
                }
                else if (p.Done && !standing) tracker.Forget(p.id);
            }
        }

        /// <summary>The colony's people laid up now: in a medical bed, or who should be.</summary>
        public static int PatientsNow(Map map)
        {
            int n = 0;
            foreach (var p in ColonyNeeds.Hands(map))
            {
                try
                {
                    if (!p.Dead && (p.InBed() && p.CurrentBed() != null && p.CurrentBed().Medical || HealthAIUtility.ShouldSeekMedicalRest(p))) n++;
                }
                catch
                {
                    // A pawn in an odd state counts for nobody.
                }
            }
            return n;
        }

        // ---------------- The kinds of room ----------------

        private static string TypeOf(string kind)
        {
            switch (kind)
            {
                case GrowthMath.Beds: return "Council_RT_Bedroom";
                case GrowthMath.Cold: return "Council_RT_Freezer";
                case GrowthMath.Dining: return "Council_RT_DiningRoom";
                case GrowthMath.Hospital: return "Council_RT_Hospital";
                case GrowthMath.Workshop: return "Council_RT_Workshop";
                case GrowthMath.Storage: return "Council_RT_Storeroom";
            }
            return null;
        }

        /// <summary>The kind of room a plan is for (by its room type), or null.</summary>
        public static string KindOf(RoomTemplateDef t)
        {
            var type = t != null ? RoomTypeDef.Of(t) : null;
            if (type == null) return null;
            foreach (var k in GrowthMath.Kinds)
                if (TypeOf(k) == type.defName) return k;
            return null;
        }

        /// <summary>The rooms it goes with, beyond the plan's own: bedrooms together, the freezer and the dining room by the kitchen, workshop and store side by side.</summary>
        private static List<string> NearOf(string kind)
        {
            switch (kind)
            {
                case GrowthMath.Beds: return new List<string> { "Bedroom" };
                case GrowthMath.Cold:
                case GrowthMath.Dining: return new List<string> { "Kitchen" };
                case GrowthMath.Workshop: return new List<string> { "Storeroom" };
                case GrowthMath.Storage: return new List<string> { "Workshop" };
            }
            return null;
        }

        /// <summary>What one planned piece adds to a kind of room: sleeping places, seats, benches, or places for a stack.</summary>
        private static int PieceAdds(string kind, string role, ThingDef def)
        {
            if (def == null) return 0;
            switch (kind)
            {
                case GrowthMath.Beds:
                case GrowthMath.Hospital:
                    return (role == "bed" || role == "doublebed" || role == "medical") && def.IsBed ? BedUtility.GetSleepingSlotsCount(def.size) : 0;
                case GrowthMath.Dining: return role == "chair" ? 1 : 0;
                case GrowthMath.Workshop: return role == "craft" ? 1 : 0;
                case GrowthMath.Cold:
                case GrowthMath.Storage:
                    return role == "storage" && def.building != null && def.building.maxItemsInCell > 0 ? def.building.maxItemsInCell * def.size.Area : 0;
            }
            return 0;
        }

        private static string RoleOf(RoomTemplateDef t, int slot) => t != null && slot >= 0 && slot < t.things.Count ? t.things[slot].role : null;

        private static int Capacity(RoomTemplateDef t, List<ResolvedThing> furniture, string kind) =>
            furniture?.Sum(f => PieceAdds(kind, RoleOf(t, f.slot), f.def)) ?? 0;

        /// <summary>What a plan placed adds, by the pieces that go down.</summary>
        public static int PlanAdds(PlacementPlan plan, string kind) =>
            plan?.furniture.Where(f => f.state == PieceState.Ready || f.state == PieceState.Waiting).Sum(f => PieceAdds(kind, RoleOf(plan.template, f.slot), f.def)) ?? 0;

        /// <summary>The sizes of a kind of room the colony can plan now, the smallest first; bedrooms the standard and cozy ones, hospitals without the surgery.</summary>
        private static List<RoomTemplateDef> Sizes(string kind, Map map, out string unavailable)
        {
            unavailable = null;
            var type = DefDatabase<RoomTypeDef>.GetNamedSilentFail(TypeOf(kind) ?? "");
            if (type == null) return new List<RoomTemplateDef>();
            var all = type.Variants.Select(v => v.t).Where(t => t != null).ToList();
            if (kind == GrowthMath.Beds) all = all.Where(t => t.defName == "Council_Bedroom" || t.defName == "Council_SmallBedroom").OrderBy(t => t.defName == "Council_Bedroom" ? 0 : 1).ToList();
            if (kind == GrowthMath.Hospital) all = all.Where(t => t.defName != "Council_Surgery").ToList();
            var ok = new List<RoomTemplateDef>();
            foreach (var t in all)
            {
                string why = t.UnavailableReason(map);
                if (why == null) ok.Add(t);
                else unavailable ??= why;
            }
            return ok;
        }

        // ---------------- Where ----------------

        /// <summary>The ground kept for the rooms followed: each footprint with a cell round it, and the side each keeps free.</summary>
        private static HashSet<IntVec3> Taken(Map map, BuildProject except = null)
        {
            var taken = new HashSet<IntVec3>();
            var tracker = BuildAheadTracker.Instance;
            if (tracker == null) return taken;
            foreach (var p in tracker.On(map))
            {
                if (p == except) continue;
                if (!p.Done)
                    foreach (var (x, z) in GroundPlanMath.Kept(p.x, p.z, p.w, p.h)) taken.Add(new IntVec3(x, 0, z));
                if (p.KeepsSide)
                    foreach (var c in p.StripRect) taken.Add(c);
            }
            return taken;
        }

        private static void Keep(HashSet<IntVec3> taken, RoomPick pick)
        {
            foreach (var (x, z) in GroundPlanMath.Kept(pick.rect.minX, pick.rect.minZ, pick.rect.Width, pick.rect.Height)) taken.Add(new IntVec3(x, 0, z));
            if (pick.growSide >= 0)
                foreach (var c in new CellRect(pick.strip.x, pick.strip.z, pick.strip.w, pick.strip.h)) taken.Add(c);
        }

        private static int DoorSide(CellRect r, IntVec3 door)
        {
            if (!door.IsValid) return GrowthMath.South;
            if (door.z == r.minZ) return GrowthMath.South;
            if (door.z == r.maxZ) return GrowthMath.North;
            if (door.x == r.minX) return GrowthMath.West;
            return GrowthMath.East;
        }

        /// <summary>A cell beyond a room's wall free to grow into: seen, buildable, nothing standing, planned or zoned there, not kept for another room.</summary>
        private static bool FreeToGrow(Map map, IntVec3 c, HashSet<IntVec3> taken)
        {
            if (!c.InBounds(map) || c.Fogged(map) || taken != null && taken.Contains(c) || c.InNoBuildEdgeArea(map)) return false;
            if (c.GetEdifice(map) != null || map.zoneManager.ZoneAt(c) != null) return false;
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.passability == Traversability.Impassable || !TemplatePlanner.GroundHolds(ThingDefOf.Wall, c, map, Rot4.North, null)) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Blueprint || things[i] is Frame || things[i].def.category == ThingCategory.Building) return false;
            return true;
        }

        /// <summary>The side of a room kept free to grow into (GrowthMath.GrowSide on the free rows beyond each wall), with its strip; -1 none.</summary>
        public static int GrowSideFor(Map map, CellRect r, IntVec3 door, HashSet<IntVec3> taken, out (int x, int z, int w, int h) strip)
        {
            strip = default;
            var depth = new int[4];
            var keep = new int[4];
            for (int side = 0; side < 4; side++)
            {
                keep[side] = GrowthMath.KeepDepth(r.Width, r.Height, side);
                for (int d = 1; d <= keep[side]; d++)
                {
                    bool row = true;
                    if (side == GrowthMath.North || side == GrowthMath.South)
                    {
                        int z = side == GrowthMath.North ? r.maxZ + d : r.minZ - d;
                        for (int x = r.minX; x <= r.maxX && row; x++) row = FreeToGrow(map, new IntVec3(x, 0, z), taken);
                    }
                    else
                    {
                        int x = side == GrowthMath.East ? r.maxX + d : r.minX - d;
                        for (int z = r.minZ; z <= r.maxZ && row; z++) row = FreeToGrow(map, new IntVec3(x, 0, z), taken);
                    }
                    if (!row) break;
                    depth[side] = d;
                }
            }
            int pick = GrowthMath.GrowSide(depth, DoorSide(r, door), keep);
            if (pick >= 0) strip = GrowthMath.Strip(r.minX, r.minZ, r.Width, r.Height, pick, Mathf.Min(depth[pick], keep[pick]));
            return pick;
        }

        private static RoomPlanConfig Turned(RoomPlanConfig cfg, int rot)
        {
            var c = cfg.WithDoor(cfg.doorX);
            c.rotation = rot;
            return c;
        }

        private static CellRect RectOf(RoomTemplateDef t, TemplatePlanner.SpotCandidate s)
        {
            TemplateMath.RotatedSize(t.FullWidth, t.FullHeight, s.rot, out int rw, out int rh);
            return new CellRect(s.anchor.x, s.anchor.z, rw, rh);
        }

        /// <summary>Building work of a plan: walls, door, furniture and floor as they would go down.</summary>
        public static float PlanWork(PlacementPlan plan)
        {
            if (plan == null) return 0f;
            float w = (plan.wallNew.Count + plan.wallWaiting.Count + plan.wallFog.Count) * BuildProjects.WorkOf(ThingDefOf.Wall, plan.wallStuff);
            if (!plan.doorExists && plan.doorState != PieceState.Blocked) w += BuildProjects.WorkOf(plan.doorDef, plan.doorStuff);
            foreach (var f in plan.furniture)
                if (f.state == PieceState.Ready || f.state == PieceState.Waiting) w += BuildProjects.WorkOf(f.def, f.stuff);
            if (plan.floor != null && plan.floorCells > 0) w += plan.floorCells * BuildProjects.WorkOf(plan.floor, plan.floorStuff);
            return w;
        }

        /// <summary>
        /// The room for an idea: the smallest size that adds what is wanted (else the largest), at the best spot the
        /// planner finds near the colony beside the rooms it goes with, where all of it can be built and no field is
        /// under it, the first such spot with a side free to grow (else the best).
        /// </summary>
        public static RoomPick Pick(GrowthIdea idea, Map map, HashSet<IntVec3> taken)
        {
            var pick = new RoomPick { idea = idea };
            var sizes = Sizes(idea.kind, map, out string unavailable);
            if (sizes.Count == 0)
            {
                pick.unavailable = unavailable ?? "CC_Build_NoPlanKind".Translate().Resolve();
                return pick;
            }
            var withCap = new List<(RoomTemplateDef t, int cap)>();
            foreach (var t in sizes)
            {
                int cap = Capacity(t, t.ResolveCached(map, out _), idea.kind);
                if (cap > 0) withCap.Add((t, cap));
            }
            if (withCap.Count == 0)
            {
                pick.unavailable = "CC_Build_NoPlanKind".Translate().Resolve();
                return pick;
            }
            RoomTemplateDef chosen;
            if (idea.kind == GrowthMath.Beds) chosen = withCap[0].t;
            else
            {
                var enough = withCap.Where(c => c.cap >= idea.want).OrderBy(c => c.cap).ToList();
                chosen = enough.Count > 0 ? enough[0].t : withCap.OrderByDescending(c => c.cap).First().t;
            }
            pick.template = chosen;
            pick.cfg = RoomPlanConfig.For(chosen);
            pick.furniture = chosen.Resolve(map, out _, pick.cfg);
            pick.capacity = Capacity(chosen, pick.furniture, idea.kind);
            var spots = TemplatePlanner.FindSpots(chosen, map, pick.furniture, pick.cfg, 4, taken, NearOf(idea.kind));
            (TemplatePlanner.SpotCandidate spot, PlacementPlan plan, int side, (int, int, int, int) strip)? first = null, best = null;
            foreach (var s in spots)
            {
                var plan = TemplatePlanner.Plan(chosen, map, s.anchor, Turned(pick.cfg, s.rot), pick.furniture);
                if (!plan.Valid || plan.zoneCells.Count > 0) continue;
                int side = GrowSideFor(map, RectOf(chosen, s), plan.door, taken, out var strip);
                first ??= (s, plan, side, strip);
                if (side >= 0)
                {
                    best = (s, plan, side, strip);
                    break;
                }
            }
            var got = best ?? first;
            if (got == null)
            {
                pick.nowhere = true;
                return pick;
            }
            pick.spot = got.Value.spot;
            pick.plan = got.Value.plan;
            pick.growSide = got.Value.side;
            pick.strip = got.Value.strip;
            pick.rect = RectOf(chosen, pick.spot);
            int adds = PlanAdds(pick.plan, idea.kind);
            if (adds > 0) pick.capacity = adds;
            pick.hours = GrowthMath.BuildHours(PlanWork(pick.plan));
            return pick;
        }

        // ---------------- The rooms to build ----------------

        private static string N(float v) => v >= GrowthMath.NoRush ? "-" : v.ToString(v < 10f ? "0.#" : "0");

        private static string SideName(int side) => ("CC_Build_Side_" + Mathf.Clamp(side, 0, 3)).Translate().Resolve();

        /// <summary>"1 more bed", "8 more seats", "36 more places": what a room adds, in its kind's words.</summary>
        public static string AddsText(string kind, int n) => (n == 1 ? "CC_Build_Adds1_" + kind : "CC_Build_Adds_" + kind).Translate(n).Resolve();

        /// <summary>What the pill says when it isn't a matter of days.</summary>
        private static string WhenText(GrowthRow r) => r?.why == null || r.why == GrowthMath.WhyNow || r.why == GrowthMath.WhyWinter ? null : ("CC_Build_When_" + r.why).Translate().Resolve();

        /// <summary>A row in sentences: what is needed and why, what is built and planned, how it stands.</summary>
        public static string RowReason(GrowthRow r, GrowthNeeds n)
        {
            var x = n.input;
            var parts = new List<string>();
            switch (r.kind)
            {
                case GrowthMath.Beds:
                    parts.Add("CC_Build_Why_Beds".Translate(r.built.Named("BUILT"), r.need.Named("NEED")).Resolve());
                    if (x.joining > 0) parts.Add("CC_Build_Why_BedsJoining".Translate(x.joining).Resolve());
                    if (x.shared > 0) parts.Add("CC_Build_Why_BedsShared".Translate(x.shared).Resolve());
                    break;
                case GrowthMath.Cold:
                    parts.Add(r.built == 0 ? "CC_Build_Why_ColdNone".Translate().Resolve()
                        : "CC_Build_Why_ColdHolds".Translate(r.built.Named("SLOTS"), N(r.holdsDays).Named("HOLDS"), N(x.mealDays).Named("MEALDAYS")).Resolve());
                    parts.Add("CC_Build_Why_ColdWants".Translate(r.need.Named("NEED"), r.mealSlots.Named("MEALS"), N(x.mealDays).Named("MEALDAYS"), r.rawSlots.Named("RAW"),
                        N(x.reserveDays).Named("RESERVE"), N(x.rawUseADay).Named("USE"), x.rawPerSlot.ToString("0.##").Named("PER")).Resolve());
                    if (x.withoutDays > 0)
                        parts.Add("CC_Build_Why_ColdWinter".Translate(x.withoutDays.Named("WITHOUT"), r.soon.Named("SOON"), N(r.wantsDays).Named("WANTS")).Resolve());
                    break;
                case GrowthMath.Dining:
                    parts.Add("CC_Build_Why_Dining".Translate(r.built.Named("BUILT"), (x.colonists + x.slaves + x.guests).Named("EATERS"), Ahead.Who(n.food).Named("WHO"), r.need.Named("NEED")).Resolve());
                    if (x.joining > 0) parts.Add("CC_Build_Why_DiningJoining".Translate(x.joining).Resolve());
                    break;
                case GrowthMath.Hospital:
                    parts.Add("CC_Build_Why_Hospital".Translate(r.built.Named("BUILT"), (x.colonists + x.slaves).Named("PEOPLE"), GrowthMath.HospitalBedsFor(x.colonists + x.slaves).Named("SIZE")).Resolve());
                    if (x.patientDays > 0) parts.Add("CC_Build_Why_HospitalMost".Translate(x.patientsMost.Named("MOST"), x.patientDays.Named("DAYS"), x.patientsNow.Named("NOW")).Resolve());
                    break;
                case GrowthMath.Workshop:
                    parts.Add("CC_Build_Why_Workshop".Translate(x.benches.Named("BENCHES"), x.benchesInWorkshop.Named("IN"), x.workshopSpare.Named("SPARE")).Resolve());
                    if (n.busyBenches.Count > 0)
                        parts.Add("CC_Build_Why_WorkshopBusy".Translate(TextUtil.JoinAnd(n.busyBenches.Take(3)
                            .Select(b => "CC_Build_BusyOne".Translate(b.bench.LabelShort.Named("BENCH"), Mathf.RoundToInt(b.hours).Named("HOURS")).Resolve()).ToList())).Resolve());
                    break;
                case GrowthMath.Storage:
                    parts.Add("CC_Build_Why_Storage".Translate(x.lying.Named("LYING"), x.lyingOut.Named("OUT"), r.built.Named("FREE")).Resolve());
                    break;
            }
            if (r.planned > 0) parts.Add("CC_Build_Why_Planned".Translate(r.planned).Resolve());
            if (r.Short > 0) parts.Add("CC_Build_Why_ShortNow".Translate(r.Short).Resolve());
            else if (r.SoonShort > 0)
                parts.Add(r.why == GrowthMath.WhyWinter && r.days < GrowthMath.NoRush
                    ? "CC_Build_Why_ShortWinter".Translate(r.SoonShort.Named("SHORT"), N(r.days).Named("DAYS")).Resolve()
                    : "CC_Build_Why_ShortSoon".Translate(r.SoonShort).Resolve());
            else parts.Add("CC_Build_Why_Enough".Translate().Resolve());
            return string.Join(" ", parts);
        }

        /// <summary>A row's state in a few words, for the table.</summary>
        public static string RowState(GrowthRow r)
        {
            if (r.Short > 0) return "CC_Build_State_ShortNow".Translate(r.Short).Resolve();
            if (r.SoonShort > 0)
                return r.why == GrowthMath.WhyWinter && r.days < GrowthMath.NoRush ? "CC_Build_State_ShortIn".Translate(r.SoonShort.Named("SHORT"), N(r.days).Named("DAYS")).Resolve()
                    : "CC_Build_State_ShortSoon".Translate(r.SoonShort).Resolve();
            return r.need <= 0 && r.built <= 0 ? "CC_Build_State_None".Translate().Resolve() : "CC_Build_State_Enough".Translate().Resolve();
        }

        private static void Rooms(View v, Map map)
        {
            var n = v.needs;
            var x = n.input;
            var taken = Taken(map);
            float queued = x.queuedHours;
            int shown = 0;
            foreach (var idea in n.plan.ideas)
            {
                if (shown >= GrowthMath.MaxRoomSteps) break;
                var pick = Pick(idea, map, taken);
                if (pick.CanPlace)
                {
                    if (!GrowthMath.Fits(queued, pick.hours, x.builderHoursADay))
                    {
                        v.heldBack = x.builders == 0
                            ? "CC_Build_HeldBack_NoBuilders".Translate().Resolve()
                            : "CC_Build_HeldBack".Translate(Mathf.RoundToInt(queued).Named("QUEUED"), x.builders.Named("BUILDERS"), Mathf.RoundToInt(x.builderHoursADay).Named("ADAY"),
                                RoomGrowth.WithArticle(pick.template, true).CapitalizeFirst().Named("ROOM")).Resolve();
                        break;
                    }
                    queued += pick.hours;
                    Keep(taken, pick);
                }
                v.rooms.Add(pick);
                var s = RoomStep(pick, n, map);
                if (s != null) v.all.Add(s);
                shown++;
            }
            // Nobody here builds: say so, with the colonist who could.
            if (x.builders == 0 && (n.plan.ideas.Count > 0 || x.queuedHours > 0f))
            {
                var best = ColonyNeeds.Hands(map).Where(p => !p.Dead && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).ThenBy(p => p.thingIDNumber).FirstOrDefault();
                string text = "CC_Build_NoBuilders".Translate().Resolve();
                var why = best != null ? BillStuck.LetDo(best, WorkTypeDefOf.Construction, text, "buildoff", null, null) : new StuckReason { key = "nobuilder", text = text };
                v.all.Add(new Suggestion
                {
                    key = "build:nobuilder", source = Source, area = "builders", days = 0f, level = 3, map = map, title = "CC_Build_NoBuildersTitle".Translate().Resolve(), reason = why.text,
                    apply = why.fix, doLabel = why.fixLabel, doTip = why.fixTip, look = best != null ? new LookTargets(best) : null
                });
            }
        }

        private static Suggestion RoomStep(RoomPick pick, GrowthNeeds n, Map map)
        {
            var idea = pick.idea;
            var row = idea.row;
            string reason = RowReason(row, n);
            var s = new Suggestion { key = idea.key, source = Source, area = idea.kind, days = idea.days, level = idea.level, map = map, when = WhenText(row) };
            string kindName = ("CC_Build_Kind_" + idea.kind).Translate().Resolve();
            if (pick.unavailable != null || pick.template == null)
            {
                s.title = "CC_Build_Room_NoPlan".Translate(kindName).Resolve();
                s.reason = reason + " " + "CC_Build_Room_NoPlanWhy".Translate(pick.unavailable ?? "").Resolve();
                return s;
            }
            string room = RoomGrowth.WithArticle(pick.template, true);
            if (pick.nowhere)
            {
                s.title = "CC_Build_Room_Title".Translate(room.Named("ROOM"), AddsText(idea.kind, pick.capacity).Named("ADDS")).Resolve();
                s.reason = reason + " " + "CC_Build_Room_Nowhere".Translate(room).Resolve();
                return s;
            }
            var x = n.input;
            string where = SpotText.Line(pick.spot, 3);
            string grow = pick.growSide >= 0
                ? "CC_Build_Room_Grow".Translate(SideName(pick.growSide).Named("SIDE"), pick.strip.w.Named("W"), pick.strip.h.Named("H")).Resolve()
                : "CC_Build_Room_NoGrow".Translate().Resolve();
            string work = "CC_Build_Room_Work".Translate(N(pick.hours).Named("HOURS"), x.builders.Named("BUILDERS"), Mathf.RoundToInt(x.builderHoursADay).Named("ADAY")).Resolve();
            s.title = "CC_Build_Room_Title".Translate(room.Named("ROOM"), AddsText(idea.kind, pick.capacity).Named("ADDS")).Resolve();
            s.reason = reason + " " + "CC_Build_Room_Where".Translate(where).Resolve() + " " + grow + " " + work;
            s.doLabel = "CC_Build_Do_Place".Translate().Resolve();
            s.doTip = "CC_Build_Do_PlaceTip".Translate(room.Named("ROOM"), (pick.plan.wallStuff?.label ?? "-").Named("STUFF")).Resolve()
                      + (pick.growSide >= 0 ? " " + "CC_Build_Do_PlaceGrowTip".Translate(SideName(pick.growSide)).Resolve() : "");
            var rect = pick.rect;
            var strip = pick.strip;
            bool keeps = pick.growSide >= 0;
            s.cells = () =>
            {
                var cells = rect.EdgeCells.ToList();
                if (keeps) cells.AddRange(new CellRect(strip.x, strip.z, strip.w, strip.h).Cells.Where(c => c.InBounds(map)));
                return cells;
            };
            s.look = new LookTargets(rect.CenterCell, map);
            s.apply = () => PlaceRoom(pick, map, s.title);
            return s;
        }

        /// <summary>The room placed as blueprints, at the spot found if it is still clear (else the next good one), through the planner's own code.</summary>
        private static SuggestDone PlaceRoom(RoomPick pick, Map map, string title)
        {
            var t = pick.template;
            if (t == null || map == null || t.UnavailableReason(map) != null) return null;
            var plan = pick.spot != null ? TemplatePlanner.Plan(t, map, pick.spot.anchor, Turned(pick.cfg, pick.spot.rot), pick.furniture) : null;
            if (plan == null || !plan.CanPlace || plan.zoneCells.Count > 0)
            {
                var again = Pick(pick.idea, map, Taken(map));
                if (!again.CanPlace || again.template != t) return null;
                pick = again;
                plan = again.plan;
            }
            var rect = RectOf(t, pick.spot);
            var player = Faction.OfPlayer;
            var dm = map.designationManager;
            var queue = map.GetComponent<CouncilPlanQueue>();
            // What was there before, so Undo takes away only what this placed.
            var before = new HashSet<Thing>();
            var mineBefore = new HashSet<IntVec3>();
            var marked = new HashSet<Thing>();
            foreach (var c in rect.ClipInsideMap(map))
            {
                foreach (var th in c.GetThingList(map))
                {
                    if (th is Blueprint || th is Frame) before.Add(th);
                    if (dm.DesignationOn(th) != null) marked.Add(th);
                }
                if (dm.DesignationAt(c, DesignationDefOf.Mine) != null) mineBefore.Add(c);
            }
            int queuedBefore = queue?.Count ?? 0;
            string summary = TemplatePlanner.Execute(plan, map);
            var undo = new UndoRoomPlaced();
            foreach (var c in rect.ClipInsideMap(map))
            {
                foreach (var th in c.GetThingList(map))
                {
                    if ((th is Blueprint || th is Frame) && th.Faction == player && !before.Contains(th) && th.def.entityDefToBuild != null && !undo.pieceCells.Contains(th.Position))
                    {
                        undo.pieceCells.Add(th.Position);
                        undo.pieceDefs.Add(th.def.entityDefToBuild.defName);
                    }
                    if (!marked.Contains(th) && !(th is Blueprint) && !(th is Frame))
                    {
                        if (th is Plant && (dm.DesignationOn(th, DesignationDefOf.CutPlant) != null || dm.DesignationOn(th, DesignationDefOf.HarvestPlant) != null)) undo.cutCells.Add(c);
                        else if (dm.DesignationOn(th, DesignationDefOf.Deconstruct) != null) undo.removeCells.Add(c);
                    }
                }
                if (!mineBefore.Contains(c) && dm.DesignationAt(c, DesignationDefOf.Mine) != null) undo.mineCells.Add(c);
            }
            if (queue != null)
                foreach (var q in queue.queue.Skip(queuedBefore))
                    if (q?.Def != null)
                    {
                        undo.queueCells.Add(q.cell);
                        undo.queueDefs.Add(q.Def.defName);
                    }
            if (plan.LeavesOutGround) Messages.Message(summary, new LookTargets(rect.CenterCell, map), MessageTypeDefOf.CautionInput, false);
            var project = Placed(plan, map, pick.idea.kind);
            if (project == null) return new SuggestDone { undo = undo, map = map, text = title };
            undo.projectId = project.id;
            return new SuggestDone { undo = undo, check = new BuildRoomCheck { projectId = project.id }, map = map, text = title };
        }

        /// <summary>A room plan placed (by a step, or by the player with the room planner): followed in stages from now, keeping a side free to grow into.</summary>
        public static BuildProject Placed(PlacementPlan plan, Map map, string kind = null)
        {
            var tracker = BuildAheadTracker.Instance;
            var t = plan?.template;
            if (tracker == null || t == null || map == null || plan.upgrade) return null;
            TemplateMath.RotatedSize(t.FullWidth, t.FullHeight, plan.rotation, out int rw, out int rh);
            var p = new BuildProject
            {
                map = map, template = t, label = t.label, kind = kind ?? KindOf(t), x = plan.anchor.x, z = plan.anchor.z, w = rw, h = rh, placedTick = Find.TickManager.TicksGame
            };
            p.adds = p.kind != null ? PlanAdds(plan, p.kind) : 0;
            int side = GrowSideFor(map, p.Rect, plan.door, Taken(map), out var strip);
            if (side >= 0)
            {
                p.growSide = side;
                (p.growX, p.growZ, p.growW, p.growH) = strip;
            }
            tracker.Add(p);
            p.workAtStart = BuildProjects.Read(p).workLeft;
            Refresh(map);
            return p;
        }

        // ---------------- Rooms being built ----------------

        private static string StageName(int stage) => ("CC_Build_Stage_" + GrowthMath.Stages[Mathf.Clamp(stage, 0, GrowthMath.Finished)]).Translate().Resolve();

        /// <summary>A room's state in a sentence: its stage, what is left, the work.</summary>
        public static string ProjectLine(ProjectState st)
        {
            var p = st.project;
            if (st.Finished) return "CC_Build_Project_Built".Translate(p.Label.CapitalizeFirst()).Resolve();
            return "CC_Build_Project_Line".Translate(p.Label.CapitalizeFirst().Named("ROOM"), StageName(st.stage).Named("STAGE"), (st.stage + 1).Named("N"), st.PiecesLeft.Named("LEFT"),
                N(st.Hours).Named("HOURS")).Resolve();
        }

        private static void Projects(View v, Map map)
        {
            var n = v.needs;
            var x = n.input;
            foreach (var st in n.projects)
            {
                var p = st.project;
                if (p.Done || !st.AnythingLeft) continue;
                string room = p.Label;
                var look = new LookTargets(p.Rect.CenterCell, map);
                List<IntVec3> Footprint() => p.Rect.ClipInsideMap(map).Cells.ToList();

                // What holds it up comes first: raising its priority wouldn't help.
                var why = BuildProjects.Why(st, Mathf.Max(0f, x.queuedHours - st.Hours), x.builderHoursADay);
                if (why != null)
                {
                    v.all.Add(new Suggestion
                    {
                        key = "build:held:" + p.id + ":" + why.key, source = Source, area = "project", level = 2, map = map, when = "CC_Build_When_Held".Translate().Resolve(),
                        title = "CC_Build_Held_Title".Translate(room).Resolve(), reason = ProjectLine(st) + " " + why.text, apply = why.fix, doLabel = why.fixLabel, doTip = why.fixTip,
                        look = look, cells = Footprint
                    });
                    continue;
                }
                StageStep(v, st, map, look, Footprint);
                StockStep(v, st, map, look);
            }
        }

        /// <summary>The stage at hand set to high build priority, so the builders take it before other building (walls before furniture).</summary>
        private static void StageStep(View v, ProjectState st, Map map, LookTargets look, Func<List<IntVec3>> footprint)
        {
            var p = st.project;
            if (!BuildPriorities.On || !RimfolkPatches.On || st.stage < GrowthMath.Walls || st.stage >= GrowthMath.Finished || st.stagePieces.Count == 0) return;
            var prio = BuildPriorities.Of(map);
            if (prio == null) return;
            var low = st.stagePieces.Where(t => t.Spawned && prio.LevelOf(t) < BuildPriorityMath.High).ToList();
            // Only when something else waits for the builders too: the room's later stages or other building.
            bool others = st.pieces.Count > st.stagePieces.Count || v.needs.input.queuedHours > st.Hours + 0.5f;
            if (low.Count == 0 || !others) return;
            string stage = StageName(st.stage);
            int left = st.left[st.stage];
            v.all.Add(new Suggestion
            {
                key = "build:stage:" + p.id + ":" + st.stage, source = Source, area = "project", level = 2, map = map, when = "CC_Build_When_Building".Translate().Resolve(),
                title = "CC_Build_Stage_Title".Translate(p.Label.Named("ROOM"), stage.Named("STAGE")).Resolve(),
                reason = ProjectLine(st) + " " + "CC_Build_Stage_Why".Translate(low.Count.Named("COUNT"), stage.Named("STAGE")).Resolve(),
                doLabel = "CC_Build_Do_Stage".Translate().Resolve(), doTip = "CC_Build_Do_StageTip".Translate(low.Count.Named("COUNT"), p.Label.Named("ROOM"), stage.Named("STAGE")).Resolve(),
                look = look, cells = () => low.Where(t => t.Spawned).SelectMany(t => t.OccupiedRect().Cells).ToList(),
                apply = () =>
                {
                    var c = BuildPriorities.Of(map);
                    var things = low.Where(t => t.Spawned && !t.Destroyed).ToList();
                    if (c == null || things.Count == 0) return null;
                    var undo = new UndoBuildPriority();
                    foreach (var t in things)
                    {
                        undo.things.Add(t);
                        undo.levels.Add(c.LevelOf(t));
                        c.Set(t, BuildPriorityMath.High);
                    }
                    return new SuggestDone { undo = undo, check = new BuildStageCheck { projectId = p.id, stage = st.stage, leftBefore = left }, map = map };
                }
            });
        }

        /// <summary>A material the site needs a lot of, with no store near it that takes it: a stockpile made beside the site for it.</summary>
        private static void StockStep(View v, ProjectState st, Map map, LookTargets look)
        {
            var p = st.project;
            var stock = v.needs.food.stock;
            var site = p.Rect.CenterCell;
            foreach (var kv in st.needs.OrderByDescending(k => k.Value))
            {
                var def = kv.Key;
                if (kv.Value < MinStockpile || !stock.TryGetValue(def, out int have) || have <= 0) continue;
                float near = NearestStore(map, def, site);
                if (near <= StoreReach) continue;
                int cells = Mathf.Clamp(Mathf.CeilToInt(kv.Value / (float)Mathf.Max(1, def.stackLimit)), 1, 9);
                string room = p.Label;
                v.all.Add(new Suggestion
                {
                    key = "build:stock:" + p.id + ":" + def.defName, source = Source, area = "project", level = 1, map = map, when = "CC_Build_When_Building".Translate().Resolve(),
                    title = "CC_Build_Stock_Title".Translate(def.label.Named("MATERIAL"), room.Named("ROOM")).Resolve(),
                    reason = "CC_Build_Stock_Why".Translate(room.Named("ROOM"), kv.Value.Named("NEED"), def.label.Named("MATERIAL"), (near >= 999f ? "-" : Mathf.RoundToInt(near).ToString()).Named("FAR"),
                        have.Named("HAVE")).Resolve(),
                    doLabel = "CC_Build_Do_Stock".Translate().Resolve(), doTip = "CC_Build_Do_StockTip".Translate(cells.Named("CELLS"), def.label.Named("MATERIAL"), room.Named("ROOM")).Resolve(),
                    look = look, cells = () => StockCells(map, p, cells),
                    apply = () =>
                    {
                        var at = StockCells(map, p, cells);
                        if (at.Count == 0) return null;
                        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
                        map.zoneManager.RegisterZone(zone);
                        foreach (var c in at) zone.AddCell(c);
                        zone.settings.filter.SetDisallowAll();
                        zone.settings.filter.SetAllow(def, true);
                        zone.settings.Priority = StoragePriority.Preferred;
                        zone.label = "CC_Build_StockName".Translate(def.label, room).Resolve();
                        return new SuggestDone
                        {
                            undo = new UndoZone { what = UndoZone.Made, zoneId = zone.ID, cells = at }, check = new BuildStockCheck { projectId = p.id, zoneId = zone.ID, material = def, waitTicks = SuggestMath.TicksPerDay },
                            map = map
                        };
                    }
                });
                return;
            }
        }

        /// <summary>A site needs this much of a material before a stockpile beside it is worth making, and the nearest store taking it is further than this.</summary>
        private const int MinStockpile = 40;
        private const float StoreReach = 14f;

        /// <summary>How far the nearest store that takes a material is from a cell (999 for none).</summary>
        private static float NearestStore(Map map, ThingDef def, IntVec3 from)
        {
            float best = 999f;
            foreach (var g in map.haulDestinationManager.AllGroupsListForReading)
            {
                if (g?.Settings == null || g.CellsList.Count == 0 || !g.Settings.AllowedToAccept(def)) continue;
                foreach (var c in g.CellsList) best = Mathf.Min(best, c.DistanceTo(from));
            }
            return best;
        }

        /// <summary>Free cells for a stockpile just outside a site, by its door, off the side it keeps to grow: a block, as many cells as wanted.</summary>
        private static List<IntVec3> StockCells(Map map, BuildProject p, int want)
        {
            var result = new List<IntVec3>();
            var rect = p.Rect;
            var keep = Taken(map);
            bool Free((int x, int z) q)
            {
                var c = new IntVec3(q.x, 0, q.z);
                if (!c.InBounds(map) || c.Fogged(map) || rect.ExpandedBy(1).Contains(c) || keep.Contains(c) || c.InNoZoneEdgeArea(map)) return false;
                if (map.zoneManager.ZoneAt(c) != null) return false;
                var terrain = c.GetTerrain(map);
                if (terrain == null || terrain.passability == Traversability.Impassable) return false;
                foreach (var t in c.GetThingList(map))
                    if (!t.def.CanOverlapZones || t is Blueprint || t is Frame) return false;
                return true;
            }
            var near = rect.CenterCell;
            int size = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(want)), 1, 3);
            for (int s = Mathf.Max(size, 2); s >= 1; s--)
            {
                if (!StorageReviewMath.BestBlock(Free, (near.x, near.z), Mathf.Max(rect.Width, rect.Height) + 4, s, out var corner)) continue;
                for (int dx = 0; dx < s && result.Count < want; dx++)
                    for (int dz = 0; dz < s && result.Count < want; dz++)
                        result.Add(new IntVec3(corner.x + dx, 0, corner.z + dz));
                break;
            }
            return result;
        }

        // ---------------- Room to grow ----------------

        /// <summary>Blueprints and frames put down where a room was to grow: a warning, with the blueprints taken away at a click.</summary>
        private static void Growing(View v, Map map)
        {
            var tracker = BuildAheadTracker.Instance;
            if (tracker == null) return;
            var player = Faction.OfPlayerSilentFail;
            foreach (var p in tracker.On(map))
            {
                if (!p.KeepsSide) continue;
                var found = new HashSet<Thing>();
                foreach (var c in p.StripRect.ClipInsideMap(map))
                    foreach (var t in c.GetThingList(map))
                        if ((t is Blueprint || t is Frame) && t.Faction == player && !p.Rect.Contains(t.Position)) found.Add(t);
                if (found.Count == 0) continue;
                var blueprints = found.OfType<Blueprint_Build>().Cast<Thing>().ToList();
                var labels = found.Select(t => t.def.entityDefToBuild?.label ?? t.LabelShort).GroupBy(l => l).Select(g => g.Count() > 1 ? g.Count() + " " + Find.ActiveLanguageWorker.Pluralize(g.Key, g.Count()) : g.Key).ToList();
                var s = new Suggestion
                {
                    key = "build:grow:" + p.id, source = Source, area = "grow", level = 1, map = map, when = "CC_Build_When_Grow".Translate().Resolve(),
                    title = "CC_Build_Grow_Title".Translate(p.Label).Resolve(),
                    reason = "CC_Build_Grow_Why".Translate(found.Count.Named("COUNT"), TextUtil.JoinAnd(labels.Take(4).ToList()).Named("WHAT"), p.growW.Named("W"), p.growH.Named("H"),
                        p.Label.Named("ROOM"), SideName(p.growSide).Named("SIDE")).Resolve()
                             + (blueprints.Count < found.Count ? " " + "CC_Build_Grow_Frames".Translate(found.Count - blueprints.Count).Resolve() : ""),
                    look = new LookTargets(p.StripRect.CenterCell, map), cells = () => p.StripRect.ClipInsideMap(map).Cells.ToList()
                };
                if (blueprints.Count > 0)
                {
                    s.doLabel = "CC_Build_Do_Cancel".Translate().Resolve();
                    s.doTip = "CC_Build_Do_CancelTip".Translate(blueprints.Count, p.Label).Resolve();
                    s.apply = () =>
                    {
                        var undo = new UndoBlueprintsCancelled();
                        foreach (var t in blueprints.Where(b => b.Spawned && !b.Destroyed).ToList())
                        {
                            var def = t.def.entityDefToBuild;
                            if (def == null) continue;
                            undo.defs.Add(def.defName);
                            undo.stuffs.Add((t as Blueprint_Build)?.stuffToUse?.defName ?? "");
                            undo.cells.Add(t.Position);
                            undo.rots.Add(t.Rotation.AsInt);
                            t.Destroy(DestroyMode.Cancel);
                        }
                        return undo.cells.Count == 0 ? null : new SuggestDone { undo = undo, map = map };
                    };
                }
                v.all.Add(s);
            }
        }
    }

    // ---------------- Undoing a room placed ----------------

    /// <summary>
    /// A room a step placed, taken away again: its blueprints and frames still there cancelled (a frame gives back what
    /// was brought to it), the cutting, mining and taking down it marked unmarked, its queued pieces dropped, and the
    /// room no longer followed. What was built already stays.
    /// </summary>
    public class UndoRoomPlaced : SuggestUndo
    {
        public int projectId = -1;
        public List<IntVec3> pieceCells = new List<IntVec3>();
        public List<string> pieceDefs = new List<string>();
        public List<IntVec3> mineCells = new List<IntVec3>(), cutCells = new List<IntVec3>(), removeCells = new List<IntVec3>(), queueCells = new List<IntVec3>();
        public List<string> queueDefs = new List<string>();

        private IEnumerable<Thing> Pieces(Map map)
        {
            var player = Faction.OfPlayerSilentFail;
            for (int i = 0; i < pieceCells.Count && i < pieceDefs.Count; i++)
            {
                var c = pieceCells[i];
                if (!c.InBounds(map)) continue;
                foreach (var t in c.GetThingList(map).ToList())
                    if ((t is Blueprint || t is Frame) && t.Faction == player && t.def.entityDefToBuild?.defName == pieceDefs[i] && t.Position == c)
                        yield return t;
            }
        }

        public override bool Possible(Map map)
        {
            if (map == null) return false;
            if (Pieces(map).Any()) return true;
            var dm = map.designationManager;
            if (mineCells.Any(c => c.InBounds(map) && dm.DesignationAt(c, DesignationDefOf.Mine) != null)) return true;
            var queue = map.GetComponent<CouncilPlanQueue>();
            return queue != null && queue.queue.Any(q => q != null && queueCells.Contains(q.cell));
        }

        public override void Undo(Map map)
        {
            if (map == null) return;
            foreach (var t in Pieces(map).Distinct().ToList())
                if (!t.Destroyed) t.Destroy(DestroyMode.Cancel);
            var dm = map.designationManager;
            foreach (var c in mineCells)
            {
                var d = c.InBounds(map) ? dm.DesignationAt(c, DesignationDefOf.Mine) : null;
                if (d != null) dm.RemoveDesignation(d);
            }
            foreach (var c in cutCells)
            {
                var plant = c.InBounds(map) ? c.GetPlant(map) : null;
                if (plant == null) continue;
                foreach (var des in new[] { DesignationDefOf.CutPlant, DesignationDefOf.HarvestPlant })
                {
                    var d = dm.DesignationOn(plant, des);
                    if (d != null) dm.RemoveDesignation(d);
                }
            }
            foreach (var c in removeCells)
            {
                var ed = c.InBounds(map) ? c.GetEdifice(map) : null;
                var d = ed != null ? dm.DesignationOn(ed, DesignationDefOf.Deconstruct) : null;
                if (d != null) dm.RemoveDesignation(d);
            }
            var queue = map.GetComponent<CouncilPlanQueue>();
            if (queue != null)
                for (int i = 0; i < queueCells.Count && i < queueDefs.Count; i++)
                {
                    var c = queueCells[i];
                    string def = queueDefs[i];
                    int at = queue.queue.FindIndex(q => q != null && q.cell == c && q.Def?.defName == def);
                    if (at >= 0) queue.queue.RemoveAt(at);
                }
            if (projectId >= 0) BuildAheadTracker.Instance?.Forget(projectId);
            BuildAhead.Refresh(map);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref projectId, "projectId", -1);
            Scribe_Collections.Look(ref pieceCells, "pieceCells", LookMode.Value);
            Scribe_Collections.Look(ref pieceDefs, "pieceDefs", LookMode.Value);
            Scribe_Collections.Look(ref mineCells, "mineCells", LookMode.Value);
            Scribe_Collections.Look(ref cutCells, "cutCells", LookMode.Value);
            Scribe_Collections.Look(ref removeCells, "removeCells", LookMode.Value);
            Scribe_Collections.Look(ref queueCells, "queueCells", LookMode.Value);
            Scribe_Collections.Look(ref queueDefs, "queueDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pieceCells ??= new List<IntVec3>();
                pieceDefs ??= new List<string>();
                mineCells ??= new List<IntVec3>();
                cutCells ??= new List<IntVec3>();
                removeCells ??= new List<IntVec3>();
                queueCells ??= new List<IntVec3>();
                queueDefs ??= new List<string>();
            }
        }
    }

    /// <summary>Blueprints a step took away, placed again as they were (where the ground is still clear).</summary>
    public class UndoBlueprintsCancelled : SuggestUndo
    {
        public List<string> defs = new List<string>(), stuffs = new List<string>();
        public List<IntVec3> cells = new List<IntVec3>();
        public List<int> rots = new List<int>();

        private static BuildableDef DefNamed(string name) =>
            (BuildableDef)DefDatabase<ThingDef>.GetNamedSilentFail(name ?? "") ?? DefDatabase<TerrainDef>.GetNamedSilentFail(name ?? "");

        public override void Undo(Map map)
        {
            if (map == null) return;
            for (int i = 0; i < defs.Count && i < cells.Count; i++)
            {
                var def = DefNamed(defs[i]);
                if (def == null) continue;
                var stuff = i < stuffs.Count && !stuffs[i].NullOrEmpty() ? DefDatabase<ThingDef>.GetNamedSilentFail(stuffs[i]) : null;
                if (def.MadeFromStuff && stuff == null) stuff = GenStuff.DefaultStuffFor(def);
                TemplatePlanner.TryPlace(def, cells[i], new Rot4(i < rots.Count ? rots[i] : 0), stuff, map);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref defs, "defs", LookMode.Value);
            Scribe_Collections.Look(ref stuffs, "stuffs", LookMode.Value);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Collections.Look(ref rots, "rots", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                defs ??= new List<string>();
                stuffs ??= new List<string>();
                cells ??= new List<IntVec3>();
                rots ??= new List<int>();
            }
        }
    }

    /// <summary>While the player places a building, a zone or a room, the sides the colony's rooms keep free to grow into show on the map, each with its room's name.</summary>
    public class BuildAheadOverlay : MapComponent
    {
        private static readonly Color StripColor = new Color(0.55f, 0.85f, 1f, 0.55f);

        public BuildAheadOverlay(Map map) : base(map) { }

        private static bool Placing()
        {
            var des = Find.DesignatorManager?.SelectedDesignator;
            return des is Designator_Place || des is Designator_PlanRoom || des is Designator_ZoneAdd;
        }

        private IEnumerable<BuildProject> Kept() => BuildAheadTracker.Instance?.On(map).Where(p => p.KeepsSide) ?? Enumerable.Empty<BuildProject>();

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || !Placing()) return;
            try
            {
                foreach (var p in Kept()) GenDraw.DrawFieldEdges(p.StripRect.ClipInsideMap(map).Cells.ToList(), StripColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("build ahead overlay", e);
            }
        }

        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap != map || !Placing() || Find.CameraDriver.CurrentZoom > CameraZoomRange.Middle) return;
            foreach (var p in Kept())
            {
                var mid = p.StripRect.CenterCell;
                if (mid.InBounds(map)) GenMapUI.DrawThingLabel(mid.ToVector3Shifted().MapToUIPosition(), "CC_Build_KeptLabel".Translate(p.Label).Resolve(), StripColor);
            }
        }
    }
}
