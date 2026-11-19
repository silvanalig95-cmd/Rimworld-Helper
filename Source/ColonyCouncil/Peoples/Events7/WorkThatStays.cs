using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 62. A work that stays (the Kiln Guild) =====================

    /// <summary>What the guild means to build here, where, and with what (WorkThatStays.Plan).</summary>
    public class WorkPlan
    {
        public string work;
        public BuildableDef def;
        public ThingDef material;
        public int count;
        public IntVec3 site = IntVec3.Invalid;
        public List<IntVec3> cells = new List<IntVec3>();
        public string where;
    }

    /// <summary>The work's days at the colony: what it is, its cells and its stone, the days at it, and how it turned.</summary>
    public class WorkState : CampWorkState
    {
        public string work, def, material, where;
        public IntVec3 site = IntVec3.Invalid;
        public List<IntVec3> cells = new List<IntVec3>();
        public int needed, received, workDays, daysWorked, lastWorkDay = -1, waitUntil = -1, waitDays, askedAt = -1;
        public Pawn student;
        public bool stood, shortRolled, shortNow, madeDo, tornRolled, torn;
        public Thing made;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref work, "work");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref material, "material");
            Scribe_Values.Look(ref where, "where");
            Scribe_Values.Look(ref site, "site", IntVec3.Invalid);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Values.Look(ref needed, "needed");
            Scribe_Values.Look(ref received, "received");
            Scribe_Values.Look(ref workDays, "workDays");
            Scribe_Values.Look(ref daysWorked, "daysWorked");
            Scribe_Values.Look(ref lastWorkDay, "lastWorkDay", -1);
            Scribe_Values.Look(ref waitUntil, "waitUntil", -1);
            Scribe_Values.Look(ref waitDays, "waitDays");
            Scribe_Values.Look(ref askedAt, "askedAt", -1);
            Scribe_References.Look(ref student, "student");
            Scribe_Values.Look(ref stood, "stood");
            Scribe_Values.Look(ref shortRolled, "shortRolled");
            Scribe_Values.Look(ref shortNow, "shortNow");
            Scribe_Values.Look(ref madeDo, "madeDo");
            Scribe_Values.Look(ref tornRolled, "tornRolled");
            Scribe_Values.Look(ref torn, "torn");
            Scribe_References.Look(ref made, "made");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }

        public ThingDef Material => material.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(material);
    }

    /// <summary>
    /// A work that stays (item 62): three journeymen of the Kiln Guild camp five to eight days by the colony and build a great work
    /// with its own stone or wood, by what the colony has: a carved bridge over the water nearest it first (Kell's bridge at
    /// Hardwater: Vanilla Furniture Expanded - Architect's stone bridge of the colony's stone, else the game's wooden one), else a
    /// grand sculpture in its hall (its largest dining or recreation room), else a fountain where it gathers (Alpha Props - Parks'
    /// fountains, Dubs Bad Hygiene's). Once they have camped, the stone is carried out to them from the stores (round 1's carry-out)
    /// and stacked at the work, they carve at it by day, and when their days of work are done it stands there, the colony's, of good
    /// quality with the guild's mark (a journeyman's name on it), and the guild remembers the work (the colony's renown reads it).
    /// The colony's best at the art may work beside them and learn. It can turn, once a stay each: a block cracks and they need a
    /// quarter more (carried out the same way; without it they wait a day or two, then make do and it stands a grade lower), or a
    /// journeyman's day is judged bad and torn down ("bad work is a lie told in stone"), which costs them a day.
    /// </summary>
    public static class WorkThatStays
    {
        public const string EventDef = "Rimfolk_Evt_WorkThatStays";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_WorkThatStays");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Vanilla Furniture Expanded - Architect's stone bridges, by the stone they are of.</summary>
        private static readonly string[] Stones = { "Granite", "Limestone", "Marble", "Sandstone", "Slate" };

        /// <summary>The fountains of the player's mods, first found first (Alpha Props - Parks', then Dubs Bad Hygiene's).</summary>
        private static readonly string[] FountainDefs =
        {
            "AP_ThreeTieredFountain", "AP_RoundFountain", "AP_HexagonalFountain", "AP_SquareFountain", "AP_ObeliskFountain", "AP_ArabicFountain", "Fountain"
        };

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static WorkState StateOf(Camp c) => c?.workState as WorkState;

        /// <summary>How much of a thing the colony has in its stores.</summary>
        private static int Stock(Map map, ThingDef def) => def == null || map == null ? 0 : VarietyUtil.InStores(map, def);

        /// <summary>The stony stuff the colony has most of in its stores that can make <paramref name="def"/>, else wood that can.</summary>
        private static ThingDef MaterialFor(Map map, ThingDef def, int need)
        {
            if (def == null || !def.MadeFromStuff) return null;
            var stones = DefDatabase<ThingDef>.AllDefsListForReading.Where(s => s.stuffProps != null && s.stuffProps.categories != null && s.stuffProps.categories.Contains(StuffCategoryDefOf.Stony)
                                                                                 && s.stuffProps.CanMake(def)).OrderByDescending(s => Stock(map, s)).ToList();
            var stone = stones.FirstOrDefault(s => Stock(map, s) >= need);
            if (stone != null) return stone;
            var wood = ThingDefOf.WoodLog;
            return wood.stuffProps != null && wood.stuffProps.CanMake(def) && Stock(map, wood) >= need ? wood : null;
        }

        /// <summary>
        /// What they would build here: the bridge over the nearest water first, else the sculpture in the hall, else the fountain where
        /// the colony gathers; each only of stone or wood the colony has in its stores, and only what the colony itself could build.
        /// </summary>
        public static WorkPlan Plan(Map map)
        {
            if (map == null) return null;
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid) return null;
            return BridgePlan(map, mid) ?? SculpturePlan(map, mid) ?? FountainPlan(map, mid);
        }

        // ---------------- The bridge ----------------

        private static IEnumerable<TerrainDef> Bridges(Map map)
        {
            foreach (var s in Stones.OrderByDescending(n => Stock(map, DefDatabase<ThingDef>.GetNamedSilentFail("Blocks" + n))))
            {
                var d = DefDatabase<TerrainDef>.GetNamedSilentFail("VFEArch_Bridge" + s);
                if (d != null) yield return d;
            }
            var wood = DefDatabase<TerrainDef>.GetNamedSilentFail("Bridge");
            if (wood != null) yield return wood;
        }

        /// <summary>What a bridge costs a cell, by its own cost (the first of it), else wood at the guild's rate.</summary>
        private static void BridgeCost(TerrainDef d, out ThingDef material, out int perCell)
        {
            var first = d?.CostList?.FirstOrDefault(c => c?.thingDef != null && c.count > 0);
            material = first?.thingDef ?? ThingDefOf.WoodLog;
            perCell = first?.count ?? PeopleEvents7Math.BridgePerCell;
        }

        private static bool Bridgeable(Map map, IntVec3 c, TerrainDef bridge)
        {
            if (!c.InBounds(map) || c.Fogged(map) || c.GetEdifice(map) != null) return false;
            var t = c.GetTerrain(map);
            if (t == null || !t.IsWater || t.bridge) return false;
            var need = bridge.terrainAffordanceNeeded;
            return need == null || t.affordances != null && t.affordances.Contains(need);
        }

        private static bool Bank(Map map, IntVec3 c) => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && !(c.GetTerrain(map)?.IsWater ?? true);

        /// <summary>
        /// The crossing nearest the colony: from a bank, straight across the water two cells wide to the far bank, two to twelve cells
        /// of water, every cell of it one the bridge can stand on, no bridge already within a few cells.
        /// </summary>
        private static List<IntVec3> Crossing(Map map, IntVec3 mid, TerrainDef bridge, out IntVec3 bank)
        {
            bank = IntVec3.Invalid;
            List<IntVec3> best = null;
            float bestScore = float.MaxValue;
            var dirs = new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West };
            foreach (var c in GenRadial.RadialCellsAround(mid, PeopleEvents7Math.WorkReach, true))
            {
                if (!Bank(map, c)) continue;
                float d = c.DistanceTo(mid);
                if (d >= bestScore) break;
                foreach (var dir in dirs)
                {
                    var side = new IntVec3(dir.z, 0, dir.x);
                    if (!Bank(map, c + side) || !(c + dir).InBounds(map) || !(c + dir).GetTerrain(map).IsWater) continue;
                    var cells = new List<IntVec3>();
                    int len = 0;
                    bool ok = false;
                    for (int k = 1; k <= PeopleEvents7Math.BridgeMax + 1; k++)
                    {
                        var a = c + dir * k;
                        var b = a + side;
                        if (!a.InBounds(map) || !b.InBounds(map)) break;
                        bool wa = a.GetTerrain(map).IsWater, wb = b.GetTerrain(map).IsWater;
                        if (!wa && !wb)
                        {
                            ok = Bank(map, a) && Bank(map, b) && len >= PeopleEvents7Math.BridgeMin;
                            break;
                        }
                        if (!wa || !wb || !Bridgeable(map, a, bridge) || !Bridgeable(map, b, bridge)) break;
                        cells.Add(a);
                        cells.Add(b);
                        len++;
                    }
                    if (!ok || cells.Any(x => GenRadial.RadialCellsAround(x, 4f, true).Any(n => n.InBounds(map) && n.GetTerrain(map).bridge))) continue;
                    float score = d + len;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = cells;
                    bank = c;
                }
            }
            return best;
        }

        private static WorkPlan BridgePlan(Map map, IntVec3 mid)
        {
            foreach (var bridge in Bridges(map))
            {
                if (!bridge.IsResearchFinished) continue;
                BridgeCost(bridge, out var material, out int perCell);
                var cells = Crossing(map, mid, bridge, out var bank);
                if (cells == null) return null;
                int need = PeopleEvents7Math.Material(PeopleEvents7Math.Bridge, cells.Count, perCell, 0);
                if (Stock(map, material) < need) continue;
                return new WorkPlan
                {
                    work = PeopleEvents7Math.Bridge, def = bridge, material = material, count = need, site = bank, cells = cells,
                    where = "CC_PEv7_WhereWater".Translate().Resolve()
                };
            }
            return null;
        }

        // ---------------- The sculpture ----------------

        /// <summary>The colony's hall: its largest dining or recreation room near its middle, roofed and its own.</summary>
        private static Room Hall(Map map, IntVec3 mid)
        {
            var roles = new[] { "DiningRoom", "RecRoom", "ThroneRoom" }.Select(n => DefDatabase<RoomRoleDef>.GetNamedSilentFail(n)).Where(r => r != null).ToList();
            return map.regionGrid.AllRooms.Where(r => r != null && !r.PsychologicallyOutdoors && !r.IsHuge && r.CellCount >= 25 && roles.Contains(r.Role)
                                                       && r.Cells.Any(c => c.InHorDistOf(mid, PeopleEvents7Math.WorkReach)))
                .OrderByDescending(r => r.CellCount).FirstOrDefault();
        }

        /// <summary>A place for a thing of this size: every cell free and standable, none by a door, the nearest a spot.</summary>
        private static IntVec3 Room(Map map, IEnumerable<IntVec3> within, IntVec3 near, IntVec2 size, bool roofed)
        {
            foreach (var c in within.OrderBy(x => x.DistanceToSquared(near)))
            {
                var rect = GenAdj.OccupiedRect(c, Rot4.North, size);
                if (rect.Cells.All(x => x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.Roofed(map) == roofed && x.GetEdifice(map) == null && x.GetFirstItem(map) == null
                                        && x.GetZone(map) == null && !(x.GetTerrain(map)?.IsWater ?? true) && !GenAdj.CellsAdjacent8Way(new TargetInfo(x, map)).Any(n => n.InBounds(map) && n.GetDoor(map) != null))
                    && rect.ExpandedBy(1).Cells.All(x => x.InBounds(map) && (x.GetEdifice(map) == null || rect.Contains(x) || x.GetEdifice(map).def.passability != Traversability.Impassable)))
                    return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// The great works of art for the hall: Vanilla Furniture Expanded - Art's grand statue and monument, and the game's grand
        /// sculpture; which comes first changes with the season, so the guild doesn't always build the same.
        /// </summary>
        private static readonly string[] Sculptures = { "VFE_StatueGrand", "VFE_Monument", "SculptureGrand" };

        private static WorkPlan SculpturePlan(Map map, IntVec3 mid)
        {
            var hall = Hall(map, mid);
            if (hall == null) return null;
            var middle = hall.Cells.Aggregate(IntVec3.Zero, (a, b) => a + b) / Math.Max(1, hall.CellCount);
            int season = GenDate.DaysPassed / 15;
            foreach (var name in Sculptures.OrderBy(n => (n.Sum(ch => ch) + season * 7) % Sculptures.Length))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null || def.category != ThingCategory.Building || !def.MadeFromStuff || !def.IsResearchFinished || def.size.x > 3 || def.size.z > 3) continue;
                int need = PeopleEvents7Math.Material(PeopleEvents7Math.Sculpture, 0, 0, def.CostStuffCount);
                var material = MaterialFor(map, def, need);
                if (material == null) continue;
                var spot = Room(map, hall.Cells, middle, def.size, true);
                if (!spot.IsValid) continue;
                return new WorkPlan
                {
                    work = PeopleEvents7Math.Sculpture, def = def, material = material, count = need, site = spot, cells = GenAdj.OccupiedRect(spot, Rot4.North, def.size).Cells.ToList(),
                    where = "CC_PEv7_WhereHall".Translate(hall.Role?.label ?? "hall").Resolve()
                };
            }
            return null;
        }

        // ---------------- The fountain ----------------

        private static WorkPlan FountainPlan(Map map, IntVec3 mid)
        {
            var spots = map.gatherSpotLister?.activeSpots?.Where(s => s?.parent != null && s.parent.Spawned).Select(s => s.parent.Position).ToList() ?? new List<IntVec3>();
            var at = spots.OrderBy(s => s.DistanceToSquared(mid)).Cast<IntVec3?>().FirstOrDefault() ?? mid;
            foreach (var name in FountainDefs)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null || def.category != ThingCategory.Building || !def.IsResearchFinished || def.size.x > 4 || def.size.z > 4) continue;
                int need = PeopleEvents7Math.Material(PeopleEvents7Math.Fountain, 0, 0, def.MadeFromStuff ? def.CostStuffCount : 0);
                var material = def.MadeFromStuff ? MaterialFor(map, def, need) : null;
                if (def.MadeFromStuff && material == null) continue;
                if (!def.MadeFromStuff) continue;
                var spot = Room(map, GenRadial.RadialCellsAround(at, 10f, true).Where(c => c.DistanceTo(at) >= 2f), at, def.size, false);
                if (!spot.IsValid) continue;
                return new WorkPlan
                {
                    work = PeopleEvents7Math.Fountain, def = def, material = material, count = need, site = spot, cells = GenAdj.OccupiedRect(spot, Rot4.North, def.size).Cells.ToList(),
                    where = (spots.Count > 0 ? "CC_PEv7_WhereGather" : "CC_PEv7_WhereMiddle").Translate().Resolve()
                };
            }
            return null;
        }

        // ---------------- The camp ----------------

        /// <summary>The camp made by the work, three journeymen walking in; <paramref name="student"/> works beside them.</summary>
        public static Camp Start(HappeningContext ctx, WorkPlan plan, Pawn student)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || plan == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                for (int i = 0; i < PeopleEvents7Math.Journeymen; i++)
                    people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_Journeyman"), ctx.faction, new FloatRange(20f, 40f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                foreach (var p in people)
                {
                    StorytellerEvents.Skilled(p, SkillDefOf.Artistic, new IntRange(9, 13));
                    StorytellerEvents.Skilled(p, SkillDefOf.Construction, new IntRange(9, 13));
                }
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents7Math.WorkMaxDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var near = plan.work == PeopleEvents7Math.Sculpture ? CampSite.ColonyCenter(map) : plan.site;
                var center = CampLand.TryFindNear(map, near, 8f, 20f, hot);
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new WorkState
                {
                    work = plan.work, def = plan.def.defName, material = plan.material?.defName, where = plan.where, site = plan.site, cells = plan.cells.ToList(), needed = plan.count,
                    student = student
                };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the work that stays", e);
                return null;
            }
        }

        /// <summary>The work's words for a camp's lines ({WORK}): what they are building, where.</summary>
        public static string Words(WorkState st) =>
            st == null ? "?" : ("CC_PEv7_Work_" + st.work).Translate(st.Material?.LabelAsStuff ?? "", st.where ?? "", LabelOf(st.def)).Resolve();

        /// <summary>The label of what is built (a building's or a bridge's own), for the words.</summary>
        public static string LabelOf(string def) =>
            (def.NullOrEmpty() ? null : (Def)DefDatabase<ThingDef>.GetNamedSilentFail(def) ?? DefDatabase<TerrainDef>.GetNamedSilentFail(def))?.label ?? "";

        /// <summary>The stone carried out to them from the stores (the carry-out's own errand), when the stores hold it; false: they wait.</summary>
        private static bool Fetch(Camp c, WorkState st, int count)
        {
            var mat = st.Material;
            if (mat == null || count <= 0) return false;
            var carry = new DilemmaEffect_CarryOut
            {
                what = mat.defName, count = count, steward = false, to = PlaySpot.Camp, who = PlayWho.None, label = "CC_PEv7_StoneFor".Translate(mat.label).Resolve()
            };
            var ctx = new HappeningContext { map = c.map, faction = c.faction, stranger = c.leader, thing = mat, count = count };
            if (carry.Unavailable(ctx) != null || Stock(c.map, mat) < count) return false;
            st.askedAt = Now;
            int before = Stock(c.map, mat);
            if (PlayOuts.On) PlayOuts.Run(new List<DilemmaEffect> { carry }, ctx, "a work that stays");
            // Nobody could carry it (the play-outs off, or no errand set going): taken from the stores now, as an answer's taking
            // always is, and set down at the work.
            if (!PlayOuts.On || before - Stock(c.map, mat) >= count)
            {
                if (PlayOuts.On || PlayCarry.Have(c.map, mat.defName, mat) >= count)
                {
                    if (!PlayOuts.On) PlayCarry.Take(c.map, mat.defName, mat, count);
                    foreach (var t in Couriers.Make(mat, count))
                        if (GenPlace.TryPlaceThing(t, st.site, c.map, ThingPlaceMode.Near, out var lying)) lying?.SetForbidden(true, false);
                    st.received += count;
                }
            }
            return true;
        }

        /// <summary>What they were handed, taken out of their packs and stacked at the work, theirs to build with.</summary>
        private static void Stack(Camp c, WorkState st)
        {
            var mat = st.Material;
            if (mat == null) return;
            foreach (var p in c.Here)
            {
                var pack = p.inventory?.innerContainer;
                if (pack == null) continue;
                foreach (var thing in pack.Where(x => x.def == mat).ToList())
                {
                    int n = thing.stackCount;
                    if (pack.TryDrop(thing, st.site, c.map, ThingPlaceMode.Near, out var dropped))
                    {
                        dropped.SetForbidden(true, false);
                        st.received += n;
                    }
                }
            }
        }

        /// <summary>Of the stone, what lies at the work.</summary>
        private static List<Thing> Pile(Camp c, WorkState st)
        {
            var mat = st.Material;
            if (mat == null || !st.site.IsValid) return new List<Thing>();
            return GenRadial.RadialDistinctThingsAround(st.site, c.map, 5f, true).Where(x => x.def == mat && x.IsForbidden(Faction.OfPlayer)).ToList();
        }

        private static bool Supplied(WorkState st) => st.received >= st.needed;

        /// <summary>Their hour: the stone fetched and stacked, the day's work counted, a day torn down, the stone short and the wait, the work standing.</summary>
        public static void Hour(Camp c, WorkState st, int hour)
        {
            if (st.stood) return;
            Stack(c, st);
            // The stone: carried out from the stores once they have camped (the errand given a day), and again for whatever is still
            // wanting; when the stores don't hold it, they wait for it a day or two (their stay the longer), then make do with what
            // they were given, or go if they were given none.
            if (!Supplied(st))
            {
                if (st.askedAt >= 0 && Now - st.askedAt < GenDate.TicksPerDay) return;
                if (Fetch(c, st, st.needed - st.received))
                {
                    if (st.waitUntil >= 0) c.Note(c.Text("CC_PEv7_WorkStoneCame", null, null, st.needed - st.received, st.Material?.label), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, false);
                    st.waitUntil = -1;
                    return;
                }
                if (st.waitUntil < 0)
                {
                    // The turn: the colony's stone runs short, and they wait for it.
                    st.waitUntil = Now + GenDate.TicksPerDay;
                    st.waitDays = 1;
                    if (c.untilTick != int.MaxValue) c.untilTick += GenDate.TicksPerDay;
                    if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "short");
                    c.Note(c.Text("CC_PEv7_WorkShort", c.leader, null, st.needed - st.received, st.Material?.label), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
                    return;
                }
                if (Now < st.waitUntil) return;
                if (st.waitDays < 2)
                {
                    st.waitUntil = Now + GenDate.TicksPerDay;
                    st.waitDays++;
                    if (c.untilTick != int.MaxValue) c.untilTick += GenDate.TicksPerDay;
                    return;
                }
                if (st.received <= 0)
                {
                    c.Leave("nostone");
                    return;
                }
                // Waited long enough: they make do with what they were given.
                st.madeDo = true;
                st.needed = st.received;
                st.shortNow = false;
                c.Note(c.Text("CC_PEv7_WorkMakeDo", c.leader), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
            }
            st.waitUntil = -1;
            // The turns, once a stay each, after their first full day at it.
            if (!st.shortRolled && st.daysWorked >= 1 && hour == 12)
            {
                st.shortRolled = true;
                if (Rand.Chance(PeopleEvents7Math.ShortChance))
                {
                    int more = PeopleEvents7Math.MoreNeeded(st.needed);
                    st.needed += more;
                    st.shortNow = true;
                    if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "crack");
                    c.Note(c.Text("CC_PEv7_WorkCracked", c.leader, null, more, st.Material?.label), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
                    return;
                }
            }
            if (!st.tornRolled && st.daysWorked >= 2 && hour == 17) Tear(c, st);
            if (st.daysWorked >= st.workDays && hour >= 15) Stand(c, st);
        }

        /// <summary>
        /// Once a stay, at the end of a day's work after their second: a journeyman's day is judged bad by the others and torn down (the
        /// day lost, a day longer at the colony); told, the oldest of them saying why.
        /// </summary>
        public static void Tear(Camp c, WorkState st)
        {
            st.tornRolled = true;
            if (!Rand.Chance(PeopleEvents7Math.TornChance)) return;
            var judge = c.Here.Where(p => p.RaceProps.Humanlike).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            var maker = c.Here.Where(p => p.RaceProps.Humanlike && p != judge).RandomElementWithFallback();
            if (judge == null || maker == null) return;
            st.torn = true;
            st.daysWorked = Mathf.Max(0, st.daysWorked - 1);
            if (c.untilTick != int.MaxValue) c.untilTick += GenDate.TicksPerDay;
            c.SayMoment(judge, "bad");
            PeopleEvents7.Send(maker, PeopleEvents7.TearJob, st.site, GenDate.TicksPerHour);
            c.Note(c.Text("CC_PEv7_WorkTorn", judge, null, 0, maker.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }

        /// <summary>A turn at the work done: the day counted once.</summary>
        public static void Carved(Pawn by, IntVec3 cell)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null) return;
            int day = GenDate.DaysPassed;
            if (st.lastWorkDay != day)
            {
                st.lastWorkDay = day;
                st.daysWorked++;
            }
            if (Rand.Chance(0.2f)) c.SayMoment(by, "carve");
        }

        /// <summary>A bad day's work torn down: rubble where it stood.</summary>
        public static void TornDown(Pawn by, IntVec3 cell)
        {
            if (by?.Map == null) return;
            FilthMaker.TryMakeFilth(cell, by.Map, ThingDefOf.Filth_RubbleRock, 3);
            FleckMaker.ThrowDustPuff(cell, by.Map, 1.2f);
        }

        /// <summary>The work done: the stone used up, and it stands there, the colony's, its quality the guild's, a journeyman's name on it; told.</summary>
        public static void Stand(Camp c, WorkState st)
        {
            if (st.stood) return;
            var map = c.map;
            var mat = st.Material;
            int quality = PeopleEvents7Math.WorkQuality(st.madeDo);
            var maker = c.Here.Where(p => p.RaceProps.Humanlike).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 0).FirstOrDefault() ?? c.leader;
            Thing made = null;
            try
            {
                if (st.work == PeopleEvents7Math.Bridge)
                {
                    var bridge = DefDatabase<TerrainDef>.GetNamedSilentFail(st.def);
                    if (bridge == null) return;
                    foreach (var cell in st.cells.Where(x => x.InBounds(map) && x.GetTerrain(map).IsWater && x.GetEdifice(map) == null))
                    {
                        if (bridge.isFoundation) map.terrainGrid.SetFoundation(cell, bridge);
                        else map.terrainGrid.SetTerrain(cell, bridge);
                        FleckMaker.ThrowDustPuff(cell, map, 0.8f);
                    }
                }
                else
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(st.def);
                    if (def == null) return;
                    var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? mat ?? GenStuff.DefaultStuffFor(def) : null);
                    thing.SetFaction(Faction.OfPlayer);
                    thing.TryGetComp<CompQuality>()?.SetQuality((QualityCategory)quality, ArtGenerationContext.Outsider);
                    var art = thing.TryGetComp<CompArt>();
                    if (art != null && maker != null) art.JustCreatedBy(maker);
                    var rect = GenAdj.OccupiedRect(st.site, Rot4.North, def.size);
                    foreach (var x in rect.Cells.Where(x => x.InBounds(map)))
                        foreach (var item in x.GetThingList(map).Where(t => t.def.category == ThingCategory.Item && t.def == mat).ToList())
                            item.Destroy();
                    if (!rect.Cells.All(x => x.InBounds(map) && x.GetEdifice(map) == null && x.Standable(map)))
                    {
                        var other = Room(map, GenRadial.RadialCellsAround(st.site, 8f, true), st.site, def.size, st.site.Roofed(map));
                        if (!other.IsValid) return;
                        st.site = other;
                    }
                    made = GenSpawn.Spawn(thing, st.site, map, Rot4.North);
                    FleckMaker.ThrowDustPuff(st.site, map, 1.5f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the work set up", e);
                return;
            }
            st.stood = true;
            st.made = made;
            // The stone used up, as much as the work took; anything over is left for the colony.
            int use = st.needed;
            foreach (var t in Pile(c, st).OrderBy(x => x.stackCount))
            {
                if (use <= 0)
                {
                    t.SetForbidden(false, false);
                    continue;
                }
                int take = Mathf.Min(use, t.stackCount);
                use -= take;
                if (take >= t.stackCount) t.Destroy();
                else
                {
                    t.SplitOff(take).Destroy();
                    t.SetForbidden(false, false);
                }
            }
            if (maker != null && maker.Spawned) c.SayMoment(maker, "done");
            var look = made != null ? new LookTargets(made) : new LookTargets(new TargetInfo(st.site, map));
            Tell.Message(NewsKind.Land, Importance.Important, map,
                "CC_PEv7_WorkStands".Translate(Words(st).Named("WORK"), (c.faction?.Name ?? "?").Named("FACTION"), (maker?.LabelShort ?? "?").Named("PAWN")).Resolve(), look,
                MessageTypeDefOf.PositiveEvent);
            if (c.faction != null) FactionMemory.Note(c.faction, "campHosted", Words(st));
            Renown.Dirty();
        }

        /// <summary>Going: the work's goodwill when it stands; unfinished, what is stacked there is left for the colony, told.</summary>
        public static void Leave(Camp c, WorkState st)
        {
            if (st.stood)
            {
                if (!c.Hostile && c.tension < 50f) PeopleEvents7.Goodwill(c.faction, PeopleEvents7Math.WorkGoodwill, GoodwillWhy.Helped);
                return;
            }
            var pile = Pile(c, st);
            foreach (var t in pile) t.SetForbidden(false, false);
            if (pile.Count > 0 || st.received > 0)
                Tell.Message(NewsKind.Land, Importance.Notable, c.map, "CC_PEv7_WorkUnfinished".Translate((c.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                    pile.Count > 0 ? new LookTargets(pile[0]) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Dev mode: the turn now (the stone short, or a day torn down).</summary>
        public static string Turn(Camp c, bool tear)
        {
            var st = StateOf(c);
            if (st == null) return "no work camp here.";
            if (tear)
            {
                st.tornRolled = false;
                st.daysWorked = Mathf.Max(st.daysWorked, 1);
                Tear(c, st);
                return st.torn ? "a day's work torn down." : "the others found nothing wrong this time (at its chance).";
            }
            st.shortRolled = true;
            int more = PeopleEvents7Math.MoreNeeded(st.needed);
            st.needed += more;
            st.shortNow = true;
            return "they need " + more + " more " + (st.Material?.label ?? "stone") + ".";
        }
    }

    /// <summary>The journeymen at the colony: the stone fetched, the work by day, the turns, the work standing.</summary>
    public class CampWorker_WorkThatStays : CampWorker
    {
        public override bool FitsMap(Map map) => WorkThatStays.Plan(map) != null;

        public override void Started(Camp c)
        {
            if (!(c.workState is WorkState))
            {
                var plan = WorkThatStays.Plan(c.map);
                c.workState = plan == null ? new WorkState { stood = true } : new WorkState
                {
                    work = plan.work, def = plan.def.defName, material = plan.material?.defName, where = plan.where, site = plan.site, cells = plan.cells.ToList(), needed = plan.count
                };
            }
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is WorkState st))
            {
                Started(c);
                st = WorkThatStays.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            if (st.workDays <= 0) st.workDays = PeopleEvents7Math.WorkDays(Mathf.RoundToInt(c.stayTicks / (float)GenDate.TicksPerDay));
            WorkThatStays.Hour(c, st, hour);
            // The one sent to work beside them, through the working hours, learning as they go.
            var s = st.student;
            if (!st.stood && s != null && PeoplesG.Up(s, c.map) && s.Faction == Faction.OfPlayer && PeopleEvents7Math.WorkHour(hour) && st.received > 0 && st.site.IsValid)
            {
                PeopleWorkOn.Bring(s, new LocalTargetInfo(st.site), "beside", GenDate.TicksPerHour + 600);
                s.skills?.Learn(SkillDefOf.Artistic, 120f, true);
            }
        }

        /// <summary>By day they carve at the work, once the stone is there.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = WorkThatStays.StateOf(c);
            if (st == null || st.stood || st.received <= 0 || !st.site.IsValid || !p.RaceProps.Humanlike || !PeopleEvents7Math.WorkHour(GenLocalDate.HourOfDay(c.map))) return null;
            var at = st.work == PeopleEvents7Math.Bridge ? st.site : st.cells.Where(x => x.InBounds(c.map)).RandomElementWithFallback(st.site);
            return PeopleEvents7.Make(PeopleEvents7.CarveJob, at, PeopleEvents7Math.CarveTicks);
        }

        public override void Leaving(Camp c)
        {
            var st = WorkThatStays.StateOf(c);
            if (st != null) WorkThatStays.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = WorkThatStays.StateOf(c);
            if (st == null) return;
            if (st.stood) marks.Add("stood");
            if (st.madeDo) marks.Add("madedo");
            if (st.torn) marks.Add("torn");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{WORK}") ? text : text.Replace("{WORK}", WorkThatStays.Words(WorkThatStays.StateOf(c)));

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = WorkThatStays.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "beside":
                    if (st.stood) return "CC_PEv7_WorkDone".Translate().Resolve();
                    return st.student != null ? "CC_PEv7_WorkBeside".Translate(st.student.LabelShort).Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A journeyman of {FACTION} ({X}) at the gate: three of them would camp by the colony and build {TALE} with {COUNT} of its {THING}.</summary>
    public class HappeningWorker_WorkThatStays : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Artisans) != null && WorkThatStays.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && WorkThatStays.On(map) == null
            && Colonists(map).Count > 0 && WorkThatStays.Plan(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            var plan = WorkThatStays.Plan(ctx.map);
            if (ctx.faction == null || plan == null) return false;
            ctx.thing = plan.material;
            ctx.count = plan.count;
            ctx.note = plan.work;
            ctx.tale = ("CC_PEv7_Work_" + plan.work).Translate(plan.material?.LabelAsStuff ?? "", plan.where ?? "", plan.def.label).Resolve();
            ctx.placeName = plan.where;
            ctx.spot = plan.site;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_Journeyman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Artistic, p => !p.WorkTagIsDisabled(WorkTags.Artistic)) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.thing == ThingDefOf.WoodLog) tags.Add("wood");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (WorkThatStays.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            if (option <= 1 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.count) return "CC_PEv7_WorkNoStone".Translate(ctx.thing.label).Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "build" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The journeymen's camp made (WorkThatStays.Start); with <see cref="beside"/>, the colony's best at the art works beside them.</summary>
    public class DilemmaEffect_WorkThatStays : DilemmaEffect
    {
        public bool beside;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.thing == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv7_Impact_Work".Translate(hc.faction?.Name ?? "?", hc.tale ?? "?").Resolve(), "CC_PEv7_Impact_WorkBrief".Translate().Resolve(), "pev7work");
            sheet.Goods(-1, hc.thing, hc.count);
            sheet.Add(ImpactKind.Other, -1, "CC_PEv7_Impact_WorkTurns".Translate(PeopleEvents7Math.MoreNeeded(hc.count), hc.thing.label).Resolve(), "CC_PEv7_Impact_WorkTurnsBrief".Translate().Resolve(),
                "pev7workturns");
            if (beside && hc.a != null)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv7_Impact_WorkBeside".Translate(hc.a.LabelShort).Resolve(), "CC_PEv7_Impact_WorkBesideBrief".Translate().Resolve(), "pev7workbeside");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.WorkGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            var plan = WorkThatStays.Plan(hc.map);
            WorkThatStays.Start(hc, plan, beside ? hc.a : null);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The colony's best at the art sent to work beside them (from the camp's page): remembered, at their side through the working hours.</summary>
    public class DilemmaEffect_WorkBeside : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv7_Impact_WorkBeside".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv7_Impact_WorkBesideBrief".Translate().Resolve(), "pev7workbeside");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_WorkThatStays);
            var st = WorkThatStays.StateOf(c);
            if (st != null && ctx?.a != null) st.student = ctx.a;
        }
    }
}
