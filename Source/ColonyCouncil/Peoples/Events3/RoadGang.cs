using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 4. The road gang (the road league): their camp =====================

    public class RoadState : CampWorkState
    {
        /// <summary>The road's cells in the order they are laid (two wide), and the spur to the colony's gate (one wide).</summary>
        public List<IntVec3> road = new List<IntVec3>(), spur = new List<IntVec3>();
        /// <summary>How many of each are done (laid, or passed over where the ground no longer takes it).</summary>
        public int laid, spurLaid;
        public string floor;
        /// <summary>Let through the colony's fields; laid round the colony's land (the line refused); the spur paid for; the band rolled; asked once each.</summary>
        public bool fields, round, spurPaid, spurPlanned, bandRolled, foodAsked, handAsked;
        public int hands;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref road, "road", LookMode.Value);
            Scribe_Collections.Look(ref spur, "spur", LookMode.Value);
            Scribe_Values.Look(ref laid, "laid");
            Scribe_Values.Look(ref spurLaid, "spurLaid");
            Scribe_Values.Look(ref floor, "floor");
            Scribe_Values.Look(ref fields, "fields");
            Scribe_Values.Look(ref round, "round");
            Scribe_Values.Look(ref spurPaid, "spurPaid");
            Scribe_Values.Look(ref spurPlanned, "spurPlanned");
            Scribe_Values.Look(ref bandRolled, "bandRolled");
            Scribe_Values.Look(ref foodAsked, "foodAsked");
            Scribe_Values.Look(ref handAsked, "handAsked");
            Scribe_Values.Look(ref hands, "hands");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                road ??= new List<IntVec3>();
                spur ??= new List<IntVec3>();
            }
        }
    }

    /// <summary>
    /// The road gang (item 4): while a new road is laid through the region (the world event, Council_WS_NewRoad), a waymaster
    /// of the league, labourers and two road wardens camp at the map's edge for 8 to 12 days (Rimfolk_Camp_RoadGang) and lay
    /// the road across the colony's land a stretch a day, so the road grows where the player can watch it. They ask leave at
    /// the gate where it crosses the colony's ground: through its fields, or round them (their way unless let through); refused,
    /// they lay it round the colony's land and think less of it. They offer a spur to the colony's gate for silver or stone,
    /// hire the colony's builder by the day, and buy food. The road stays when they go. It can turn: a robber band comes against
    /// the camp (the camps' bands, the colony may stand with them). Packed earth, flagstones of the land's stone, concrete, paved
    /// tile, by the league's age.
    /// </summary>
    public static class RoadGang
    {
        public const string HandKind = "pev:roadhand";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_RoadGang");

        public static JobDef LayJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_LayRoad");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static RoadState StateOf(Camp c) => c?.workState as RoadState;

        /// <summary>The new road through the region running now (its name), or null.</summary>
        public static string NewRoad()
        {
            var s = WorldSituations.Instance?.Running.FirstOrDefault(x => x?.def?.defName == "Council_WS_NewRoad");
            return s == null ? null : (!(s.ctx?.name).NullOrEmpty() ? s.ctx.name : s.def.label);
        }

        /// <summary>The road's floor by the league's age (PeopleEvents3Math.RoadFloor), flagstones of the land's own stone.</summary>
        public static TerrainDef FloorFor(Map map, Faction f)
        {
            string stone = null;
            try
            {
                stone = Find.World.NaturalRockTypesIn(map.Tile).FirstOrDefault()?.defName;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the land's stone", e);
            }
            string name = PeopleEvents3Math.RoadFloor(PeopleEvents.AgeOf(f), stone);
            return DefDatabase<TerrainDef>.GetNamedSilentFail(name) ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneGranite") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("PackedDirt");
        }

        /// <summary>The road's floor in a word ("flagstones"), for their words.</summary>
        public static string FloorWord(Faction f) => ("CC_PEv3_Floor_" + Mathf.Clamp(PeopleEvents.AgeOf(f), 0, 3)).Translate().Resolve();

        /// <summary>The ground takes the road here: open, walkable, no building on it, nothing that would sink it, not fogged.</summary>
        public static bool Takes(Map map, IntVec3 c, TerrainDef floor)
        {
            if (!c.InBounds(map) || c.Fogged(map) || !c.Walkable(map) || c.GetEdifice(map) != null) return false;
            var t = c.GetTerrain(map);
            if (t == null || t.IsWater) return false;
            return floor?.terrainAffordanceNeeded == null || c.SupportsStructureType(map, floor.terrainAffordanceNeeded);
        }

        /// <summary>
        /// The colony's land kept clear of when the line is refused: every cell within PeopleEvents3Math.AroundMargin of its home
        /// area or a building of its own (a walk outward from them all at once).
        /// </summary>
        private static bool[] Near(Map map)
        {
            int w = map.Size.x, h = map.Size.z;
            var near = new bool[w * h];
            var dist = new int[w * h];
            var queue = new Queue<int>();
            var home = map.areaManager.Home;
            foreach (var c in map.AllCells)
            {
                bool ours = (home != null && home[c]) || (c.GetEdifice(map)?.Faction == Faction.OfPlayer);
                if (!ours) continue;
                int i = c.z * w + c.x;
                near[i] = true;
                queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (dist[i] >= PeopleEvents3Math.AroundMargin) continue;
                int x = i % w, z = i / w;
                foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                {
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int j = nz * w + nx;
                    if (near[j]) continue;
                    near[j] = true;
                    dist[j] = dist[i] + 1;
                    queue.Enqueue(j);
                }
            }
            return near;
        }

        /// <summary>
        /// The road's way from one edge of the map to the other: across the colony's land past its gate (through its fields only
        /// with <paramref name="fields"/>), or with <paramref name="round"/> kept clear of it all. Its cells in laying order, two
        /// wide; empty when the land has no way.
        /// </summary>
        public static List<IntVec3> Line(Map map, TerrainDef floor, bool fields, bool round, out int fieldCells)
        {
            fieldCells = 0;
            var result = new List<IntVec3>();
            if (map == null || floor == null) return result;
            int w = map.Size.x, h = map.Size.z;
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            var near = round ? Near(map) : null;
            var zones = new bool[w * h];
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in zone.Cells)
                    zones[c.z * w + c.x] = true;
            // Each cell's cost worked out once, for the way's search and the road's cells.
            var costs = new float[w * h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    costs[i] = !Takes(map, new IntVec3(x, 0, z), floor) || (near != null && near[i]) ? -1f : zones[i] ? (fields ? 1.5f : PeopleEvents3Math.FieldCost) : 1f;
                }
            float Cost(int x, int z) => x < 0 || z < 0 || x >= w || z >= h ? -1f : costs[z * w + x];
            // West to east or south to north, whichever runs nearer the colony's middle; the line past its gate, a little aside.
            List<(int x, int z)> way = null;
            foreach (bool eastWest in Rand.Bool ? new[] { true, false } : new[] { false, true })
            {
                int across = eastWest ? centre.z : centre.x;
                int offset = round ? Rand.RangeInclusive(-40, 40) : Rand.RangeInclusive(-10, 10);
                int line = Mathf.Clamp(across + offset, 8, (eastWest ? h : w) - 9);
                if (!Ends(map, eastWest, line, Cost, out var a, out var b)) continue;
                way = PeopleEvents3Math.Route(w, h, Cost, a.x, a.z, b.x, b.z);
                if (way != null && way.Count > 10) break;
                way = null;
            }
            if (way == null) return result;
            foreach (var (x, z) in PeopleEvents3Math.Widen(way, PeopleEvents3Math.RoadWidth, w, h))
            {
                var c = new IntVec3(x, 0, z);
                if (Cost(x, z) < 0f) continue;
                if (zones[z * w + x]) fieldCells++;
                result.Add(c);
            }
            return result;
        }

        /// <summary>The two ends of the line on opposite edges, the nearest the road can start on.</summary>
        private static bool Ends(Map map, bool eastWest, int line, Func<int, int, float> cost, out IntVec3 a, out IntVec3 b)
        {
            a = b = IntVec3.Invalid;
            int w = map.Size.x, h = map.Size.z;
            for (int off = 0; off < 30 && (!a.IsValid || !b.IsValid); off++)
                foreach (int s in new[] { off, -off })
                {
                    int k = line + s;
                    if (eastWest ? (k < 2 || k >= h - 2) : (k < 2 || k >= w - 2)) continue;
                    var p = eastWest ? new IntVec3(1, 0, k) : new IntVec3(k, 0, 1);
                    var q = eastWest ? new IntVec3(w - 2, 0, k) : new IntVec3(k, 0, h - 2);
                    if (!a.IsValid && cost(p.x, p.z) >= 0f) a = p;
                    if (!b.IsValid && cost(q.x, q.z) >= 0f) b = q;
                }
            return a.IsValid && b.IsValid;
        }

        /// <summary>The spur from the colony's gate to the nearest cell of the road, one wide.</summary>
        public static List<IntVec3> Spur(Map map, RoadState st, TerrainDef floor)
        {
            var list = new List<IntVec3>();
            var gate = PlaySpots.GateCell(map, null);
            if (map == null || st == null || floor == null || !gate.IsValid || st.road.Count == 0) return list;
            var to = st.road.OrderBy(c => c.DistanceToSquared(gate)).First();
            int w = map.Size.x, h = map.Size.z;
            var way = PeopleEvents3Math.Route(w, h, (x, z) => Takes(map, new IntVec3(x, 0, z), floor) || new IntVec3(x, 0, z) == gate ? 1f : -1f, gate.x, gate.z, to.x, to.z);
            if (way == null) return list;
            foreach (var (x, z) in way)
            {
                var c = new IntVec3(x, 0, z);
                if (!st.road.Contains(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// The camp made and set going (DryWater's way): the waymaster, labourers and two road wardens walking in, camped by the
        /// edge where the road comes in, the line chosen by the colony's answer.
        /// </summary>
        public static Camp Start(HappeningContext ctx, bool fields, bool round)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            var f = ctx?.faction;
            if (map == null || tracker == null || kind == null || f == null || !CampTracker.Enabled) return null;
            try
            {
                var floor = FloorFor(map, f);
                var road = Line(map, floor, fields, round, out _);
                if (road.Count < 10) return null;
                var people = new List<Pawn>();
                int labourers = Rand.RangeInclusive(2, 3);
                foreach (var k in new[] { "Rimfolk_League_Master", "Rimfolk_League_Guard", "Rimfolk_League_Guard" }.Concat(Enumerable.Repeat("Rimfolk_League_Wayfarer", labourers)))
                {
                    var p = Arrivals.Make(PeoplesD.KindNamed(f, k), f, kind.ages);
                    if (p != null) people.Add(p);
                }
                if (people.Count < 3)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 10);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var start = road[0];
                var center = CampLand.TryFindNear(map, start, 6f, 16f, hot);
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, f, hot, null, null, people);
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
                c.workState = new RoadState { road = road, floor = floor.defName, fields = fields, round = round };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the road gang", e);
                return null;
            }
        }

        /// <summary>How many of the road's cells should be down by now: a stretch a day, the last a day before they go.</summary>
        public static int Target(Camp c, RoadState st)
        {
            float stay = Mathf.Max(2f, (c.untilTick == int.MaxValue ? c.stayTicks : c.untilTick - c.arrivedTick) / (float)GenDate.TicksPerDay);
            int days = Mathf.Max(1, Mathf.FloorToInt(stay) - 1);
            return PeopleEvents3Math.Stretch(st.road.Count, days, Mathf.FloorToInt(c.DaysHere));
        }

        /// <summary>The next cell to lay for <paramref name="p"/>: the road up to today's stretch, then the spur; none another has.</summary>
        public static IntVec3 Next(Camp c, RoadState st, Pawn p)
        {
            var map = c.map;
            var floor = DefDatabase<TerrainDef>.GetNamedSilentFail(st.floor ?? "");
            var job = LayJob;
            if (floor == null || job == null) return IntVec3.Invalid;
            var taken = new HashSet<IntVec3>(map.mapPawns.AllPawnsSpawned.Where(o => o != p && o.CurJobDef == job).Select(o => o.CurJob.targetA.Cell));
            int target = Target(c, st);
            while (st.laid < st.road.Count && Done(map, st.road[st.laid], floor)) st.laid++;
            for (int i = st.laid; i < Mathf.Min(target, st.road.Count); i++)
            {
                var cell = st.road[i];
                if (taken.Contains(cell) || Done(map, cell, floor)) continue;
                if (p.CanReach(cell, PathEndMode.Touch, Danger.Some)) return cell;
            }
            if (!st.spurPaid || st.laid < Mathf.Min(target, st.road.Count)) return IntVec3.Invalid;
            while (st.spurLaid < st.spur.Count && Done(map, st.spur[st.spurLaid], floor)) st.spurLaid++;
            for (int i = st.spurLaid; i < st.spur.Count; i++)
            {
                var cell = st.spur[i];
                if (taken.Contains(cell) || Done(map, cell, floor)) continue;
                if (p.CanReach(cell, PathEndMode.Touch, Danger.Some)) return cell;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A cell needs no more work: laid already, a floor of the colony's own on it, or the ground no longer takes it.</summary>
        private static bool Done(Map map, IntVec3 c, TerrainDef floor)
        {
            var t = c.GetTerrain(map);
            return t == floor || (t != null && t.layerable && t != floor) || !Takes(map, c, floor);
        }

        /// <summary>One of the road's cells laid (JobDriver_LayRoad): the plants on it cleared, the floor down; the colony's builder learns by it.</summary>
        public static void Lay(Pawn p, IntVec3 cell)
        {
            var map = p?.Map;
            var c = CampTracker.VisitorsAt(map).FirstOrDefault(x => x.kind == Kind);
            var st = StateOf(c);
            var floor = DefDatabase<TerrainDef>.GetNamedSilentFail(st?.floor ?? "");
            if (map == null || st == null || floor == null || Done(map, cell, floor)) return;
            try
            {
                cell.GetPlant(map)?.Destroy();
                map.terrainGrid.SetTerrain(cell, floor);
                FleckMaker.ThrowDustPuff(cell, map, 0.8f);
                if (p.IsColonist)
                {
                    p.skills?.Learn(SkillDefOf.Construction, PeopleEvents3Math.RoadCellXp, true);
                    // The colony's builder on their day with the gang goes straight on to the next cell.
                    var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == HandKind && !x.done && x.x == p);
                    var next = v != null && Now < v.until ? Next(c, st, p) : IntVec3.Invalid;
                    if (next.IsValid && LayJob != null)
                    {
                        var j = JobMaker.MakeJob(LayJob, next);
                        j.count = PeopleEvents3Math.RoadCellTicks;
                        p.jobs.jobQueue.EnqueueFirst(j);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: a cell of road", e);
            }
        }

        // ---------------- The colony's builder, hired by the day ----------------

        /// <summary>The colony's builder works beside the gang until evening (their visit), paid at the day's end.</summary>
        public static Visit Hire(Camp c, Pawn builder)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || c == null || builder == null || !builder.Spawned) return null;
            int hour = GenLocalDate.HourOfDay(c.map);
            var v = t.AddVisit(HandKind, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_RoadGang"), c.map, builder, null, null,
                Now + Mathf.Max(2, 18 - hour) * GenDate.TicksPerHour);
            v.stake = c.id;
            v.text = c.faction?.GetUniqueLoadID() ?? "";
            v.due = PeopleEvents3Math.BuilderWage(builder.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0);
            var st = StateOf(c);
            if (st != null) st.hands++;
            return v;
        }

        /// <summary>The builder's hour on the road: the next cell, beside the gang; the day done, their wage set down at the builder's feet.</summary>
        public static bool HandHour(Visit v, int now)
        {
            if (v.done) return false;
            var c = CampTracker.Instance?.ById(v.stake);
            var st = StateOf(c);
            var p = v.x;
            if (c == null || st == null || p == null || p.Dead || !c.Settled)
            {
                v.done = true;
                return false;
            }
            if (now >= v.until)
            {
                v.done = true;
                var payer = c.leader != null && c.leader.Spawned ? c.leader : null;
                Couriers.PaySilver(c.map, p.Spawned ? p.Position : c.center, v.due, c.faction, payer);
                c.Note(c.Text("CC_PEv3_HandPaid", null, p, v.due), MessageTypeDefOf.PositiveEvent, Importance.Notable, p);
                return false;
            }
            var job = LayJob;
            if (job == null || !p.Spawned || p.Map != c.map || p.Downed || p.Drafted || p.InMentalState || p.CurJobDef == job) return true;
            var cell = Next(c, st, p);
            if (!cell.IsValid) return true;
            var j = JobMaker.MakeJob(job, cell);
            j.count = PeopleEvents3Math.RoadCellTicks;
            p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
            return true;
        }

        /// <summary>The spur paid for (silver or stone, carried out by the ask's own effect): its way from the gate, laid after the day's stretch.</summary>
        public static void SpurPaid(Camp c)
        {
            var st = StateOf(c);
            if (st == null || st.spurPaid) return;
            st.spurPaid = true;
            if (st.spur.Count == 0) st.spur = Spur(c.map, st, DefDatabase<TerrainDef>.GetNamedSilentFail(st.floor ?? ""));
        }

        /// <summary>The colony's stone blocks it has most of (any stone's), for a spur paid in stone; null with none.</summary>
        public static ThingDef Blocks(Map map)
        {
            if (map == null) return null;
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff && d.stuffProps?.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Stony)
                                                                         && d.defName.StartsWith("Blocks", StringComparison.Ordinal))
                .OrderByDescending(d => map.resourceCounter.GetCount(d)).FirstOrDefault(d => map.resourceCounter.GetCount(d) > 0);
        }

        /// <summary>The spur's length if it were laid now (for the ask's price), from the gate to the road.</summary>
        public static int SpurLength(Camp c) => StateOf(c)?.spur.Count ?? 0;

        /// <summary>The spur's way from the gate worked out once, their first hour settled (the asks' price and the laying read it).</summary>
        public static void PlanSpur(Camp c, RoadState st)
        {
            if (st == null || st.spurPlanned || st.road.Count == 0) return;
            st.spurPlanned = true;
            st.spur = Spur(c.map, st, DefDatabase<TerrainDef>.GetNamedSilentFail(st.floor ?? ""));
        }
    }

    /// <summary>The road gang's days: the road a stretch a day, the spur, the colony's builder by the day, the food they buy, the band.</summary>
    public class CampWorker_RoadGang : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is RoadState)) c.workState = new RoadState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is RoadState st))
            {
                Started(c);
                st = RoadGang.StateOf(c);
            }
            if (c.Settled) RoadGang.PlanSpur(c, st);
            // Once a stay: a robber band comes against the camp, on top of the camps' own.
            if (!st.bandRolled && c.Settled && c.raidRolled)
            {
                st.bandRolled = true;
                if (c.raidAt < 0 && Rand.Chance(PeopleEvents3Math.GangBandChance)) c.raidAt = CampRaidMath.AttackTick(c.arrivedTick, c.untilTick, Rand.Value);
            }
            // They buy food on their second day, and ask for the colony's builder on their third.
            if (!st.foodAsked && c.DaysHere >= 1f && hour >= 8 && hour <= 12 && Tell.CanAsk)
            {
                var ask = c.AskByKey("food");
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.foodAsked = true;
            }
            else if (!st.handAsked && c.DaysHere >= 2f && hour >= 6 && hour <= 9 && Tell.CanAsk)
            {
                var ask = c.AskByKey("builder");
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.handAsked = true;
            }
            // The day's stretch told at dusk.
            if (hour == 18 && st.road.Count > 0)
            {
                int down = Mathf.Min(st.laid, st.road.Count);
                c.Note(c.Text(down >= st.road.Count ? "CC_PEv3_RoadDone" : "CC_PEv3_RoadDay", null, null, down * 100 / Mathf.Max(1, st.road.Count)), MessageTypeDefOf.NeutralEvent, Importance.Flavor,
                    null);
            }
        }

        /// <summary>By day the labourers (and the waymaster) lay the next cell of the road; the wardens keep their watch as a camp does.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = RoadGang.StateOf(c);
            var job = RoadGang.LayJob;
            if (st == null || job == null || !p.RaceProps.Humanlike || p.kindDef?.defName == "Rimfolk_League_Guard" || p == c.trader) return null;
            var cell = RoadGang.Next(c, st, p);
            if (!cell.IsValid) return null;
            if (Rand.Chance(0.05f)) c.SayMoment(p, "road");
            var j = JobMaker.MakeJob(job, cell);
            j.count = PeopleEvents3Math.RoadCellTicks;
            return j;
        }

        public override void Leaving(Camp c)
        {
            var st = RoadGang.StateOf(c);
            var f = c.faction;
            if (st == null || f == null) return;
            if (!st.round && !c.Hostile && c.tension < 40f && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.RoadGoodwill, false);
            News.Post("CC_PEv3_RoadStays".Translate(f.Name.Named("FACTION"), RoadGang.FloorWord(f).Named("FLOOR")).Resolve(), c.Look, NewsKind.World);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = RoadGang.StateOf(c);
            if (st == null) return;
            if (st.round) marks.Add("round");
            if (st.spurPaid) marks.Add("spur");
            if (st.hands > 0) marks.Add("hand");
            if (st.road.Count > 0 && st.laid >= st.road.Count) marks.Add("done");
        }

        public override string Fill(Camp c, string text) =>
            text == null || !text.Contains("{FLOOR}") ? text : text.Replace("{FLOOR}", RoadGang.FloorWord(c.faction));

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = RoadGang.StateOf(c);
            if (st == null) return null;
            if ((a?.key == "spur" || a?.key == "spurStone") && (st.spurPaid || RoadGang.SpurLength(c) == 0)) return "CC_CampAsk_Done".Translate().Resolve();
            var blocks = RoadGang.Blocks(c.map);
            if (a?.key == "spurStone" && (blocks == null || PlayCarry.Have(c.map, blocks.defName, blocks) < PeopleEvents3Math.SpurStone(RoadGang.SpurLength(c))))
                return "CC_Dilemma_NotEnough".Translate(PeopleEvents3Math.SpurStone(RoadGang.SpurLength(c)), blocks?.label ?? "CC_PEv3_StoneBlocks".Translate().Resolve()).ToString();
            return null;
        }
    }

    /// <summary>
    /// The road gang's asks answered: "spur" (paid in silver or stone, carried out by the ask's own effect beside this), "food"
    /// (a day's meals carried out, paid at their worth and a tenth), "builder" (the colony's best builder works the day beside
    /// them, paid at its end).
    /// </summary>
    public class DilemmaEffect_RoadAsk : DilemmaEffect
    {
        public string what;
        public bool stone;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RoadGang);
            if (c == null) return null;
            if (what == "food" && PlayCarry.Have(c.map, "meals", null) < PeopleEvents3Math.GangMeals)
                return "CC_Dilemma_NotEnough".Translate(PeopleEvents3Math.GangMeals, PlayCarry.Label("meals", null)).ToString();
            if (what == "spur" && !stone && PlayCarry.Have(c.map, "silver", ThingDefOf.Silver) < SpurPrice(c)) return "CC_Dilemma_NotEnough".Translate(SpurPrice(c), ThingDefOf.Silver.label).ToString();
            if (what == "builder" && Builder(c) == null) return "CC_PEv3_NoBuilder".Translate().Resolve();
            return null;
        }

        private static int SpurPrice(Camp c) => PeopleEvents3Math.SpurPrice(RoadGang.SpurLength(c), LivingEvents.WealthFactor(c.map));

        private static Pawn Builder(Camp c) => PeoplesD.Best(c.map, SkillDefOf.Construction, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));

        private static int MealsPay(Map map)
        {
            var meals = PlayCarry.Stock(map, "meals", null).Take(PeopleEvents3Math.GangMeals).ToList();
            float worth = meals.Sum(t => t.def.BaseMarketValue) / Mathf.Max(1, meals.Count) * PeopleEvents3Math.GangMeals;
            return PeopleEvents2Math.MealsPrice(worth);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RoadGang);
            if (c == null) return;
            switch (what)
            {
                case "spur":
                    int len = RoadGang.SpurLength(c);
                    sheet.Play("CC_PEv3_Impact_Spur".Translate(len).Resolve(), "CC_PEv3_Impact_SpurBrief".Translate().Resolve());
                    if (stone && RoadGang.Blocks(c.map) is ThingDef b0) sheet.Goods(-1, b0, PeopleEvents3Math.SpurStone(len));
                    else sheet.Silver(-SpurPrice(c));
                    break;
                case "food":
                    sheet.Goods(-1, PlayCarry.Label("meals", null), PeopleEvents3Math.GangMeals, "meals");
                    sheet.Silver(MealsPay(c.map));
                    break;
                case "builder":
                    var b = Builder(c);
                    if (b == null) return;
                    sheet.Play("CC_PEv3_Impact_Hand".Translate(b.LabelShort).Resolve(), "CC_PEv3_Impact_HandBrief".Translate().Resolve());
                    sheet.Silver(PeopleEvents3Math.BuilderWage(b.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0));
                    DilemmaImpact.Xp(sheet, b.LabelShort, b.ThingID, SkillDefOf.Construction, PeopleEvents3Math.RoadCellXp * 12f, false);
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RoadGang);
            if (c == null) return;
            switch (what)
            {
                case "spur":
                    if (stone)
                    {
                        var blocks = RoadGang.Blocks(c.map);
                        if (blocks == null) return;
                        CampAnswers.Carry(c, blocks.defName, PeopleEvents3Math.SpurStone(RoadGang.SpurLength(c)), null, "CC_PEv3_SpurStoneLabel".Translate(c.Label.Named("NAME")).Resolve());
                    }
                    else CampAnswers.Carry(c, "silver", SpurPrice(c), null, "CC_PEv3_SpurSilverLabel".Translate(c.Label.Named("NAME")).Resolve());
                    RoadGang.SpurPaid(c);
                    break;
                case "food":
                {
                    int pay = MealsPay(c.map);
                    CampAnswers.Carry(c, "meals", PeopleEvents3Math.GangMeals, null, "CC_PEv3_GangMealsLabel".Translate(c.Label.Named("NAME")).Resolve());
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = pay;
                    var at = c.share.IsValid ? c.share : c.center;
                    if (GenPlace.TryPlaceThing(silver, at, c.map, ThingPlaceMode.Near, out var placed)) placed?.SetForbidden(false, false);
                    c.Note(c.Text("CC_PEv3_GangMealsPaid", null, null, pay), MessageTypeDefOf.PositiveEvent, Importance.Notable, placed);
                    break;
                }
                case "builder":
                    RoadGang.Hire(c, Builder(c));
                    break;
            }
        }
    }

    /// <summary>
    /// A waymaster of {FACTION} at the gate: with {TALE} being laid through the region, the line crosses the colony's ground;
    /// leave to lay it through, fields and all, or round the fields, or not across its land. {COUNT} is the fields' cells in the
    /// line's way; {THING}... the floor is named by the texts' age tags.
    /// </summary>
    public class HappeningWorker_RoadGang : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            RoadGang.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Traders) != null && RoadGang.NewRoad() != null && RoadGang.On(map) == null
            && !MapFacts.InVacuum(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Traders);
            ctx.tale = RoadGang.NewRoad();
            if (ctx.faction == null || ctx.tale == null) return false;
            var floor = RoadGang.FloorFor(ctx.map, ctx.faction);
            var line = RoadGang.Line(ctx.map, floor, true, false, out int fields);
            if (line.Count < 10) return false;
            ctx.count = fields;
            ctx.beast = RoadGang.FloorWord(ctx.faction);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Master"), ctx.faction, ext.ages);
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.count > 0) tags.Add("fields");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            RoadGang.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "leave" : "round");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The road gang comes (RoadGang.Start): through the fields with <see cref="fields"/>, round the colony's land with <see cref="round"/> (they think less of it).</summary>
    public class DilemmaEffect_RoadGang : DilemmaEffect
    {
        public bool fields, round;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play((round ? "CC_PEv3_Impact_RoadRound" : "CC_PEv3_Impact_Road").Translate(hc.beast ?? "?").Resolve(), "CC_PEv3_Impact_RoadBrief".Translate().Resolve());
            if (!round && fields && hc.count > 0)
                sheet.Add(ImpactKind.Goods, -1, "CC_PEv3_Impact_RoadFields".Translate(hc.count).Resolve(), "CC_PEv3_Impact_RoadFieldsBrief".Translate().Resolve(), "pev3roadfields");
            sheet.Add(ImpactKind.People, 0, "CC_PEv3_Impact_RoadCamp".Translate().Resolve(), "CC_PEv3_Impact_RoadCampBrief".Translate().Resolve(), "pev3roadcamp");
            if (hc.faction != null) sheet.Goodwill(hc.faction, round ? PeopleEvents3Math.LineRefusedGoodwill : PeopleEvents3Math.RoadGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (round && hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents3Math.LineRefusedGoodwill, false);
            if (RoadGang.Start(hc, fields, round) == null && round)
                Tell.Message(NewsKind.World, Importance.Notable, hc.map, "CC_PEv3_RoadElsewhere".Translate((hc.faction?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
            // The waymaster goes back to bring the gang in.
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>One of the gang (or the colony's builder) lays a cell of the road: to it, a while at it, the floor down (RoadGang.Lay). TargetA the cell, count the ticks.</summary>
    public class JobDriver_LayRoad : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("LayRoad");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
                if (pawn.IsHashIntervalTick(200, delta)) FleckMaker.ThrowDustPuff(job.targetA.Cell, pawn.Map, 0.5f);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("RoadLaid");
            done.initAction = () => RoadGang.Lay(pawn, job.targetA.Cell);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
