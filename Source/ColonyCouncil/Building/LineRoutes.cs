using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Lines under the walls, from the Base page. Each power net and each pipe net of a home map (Dubs Bad Hygiene's
    /// plumbing, Vanilla Expanded's resource pipes) is read for the rules in LineRouteMath: its line (the conduit or
    /// pipe most of it is made of, in its usual material), its other pieces, the powered pieces wired to its lines and
    /// how near another net comes to each, and the cells a new line mustn't take because another net is beside them.
    /// Then it's planned again. Laying the plan out places the new lines as blueprints; the old ones get their
    /// deconstruct orders only once every new one stands, and only those the net can do without then
    /// (<see cref="LineRelayTracker"/>), so nothing goes dark or dry on the way.
    /// </summary>
    public static class LineRoutes
    {
        public const string Power = "power";

        /// <summary>A net planned again: the lines laid and taken up, and how many of its lines are in sight before and after.</summary>
        public class NetPlan
        {
            public string kind;
            public ThingDef line, stuff;
            public List<IntVec3> lay = new List<IntVec3>(), takeUp = new List<IntVec3>();
            public int inSightBefore, inSightAfter;
        }

        /// <summary>A net as the rules see it, and the way between their cells and the map's.</summary>
        private class Model
        {
            public string kind;
            public ThingDef line, stuff;
            public CellRect region;
            public LineRouteMath.Net net;

            public IntVec3 Cell(int i) => new IntVec3(region.minX + i % region.Width, 0, region.minZ + i / region.Width);

            public int Index(IntVec3 c) => region.Contains(c) ? (c.z - region.minZ) * region.Width + (c.x - region.minX) : -1;
        }

        /// <summary>How far round a net the rules may look for a better way, in cells.</summary>
        private const int Margin = 8;

        /// <summary>Fewer lines in sight than this, and a kind isn't worth a finding (nor a net its routing).</summary>
        private const int MinInSight = 3;

        private class Cache
        {
            public int tick = -999999;
            public List<NetPlan> plans = new List<NetPlan>();
        }

        private static readonly Dictionary<int, Cache> caches = new Dictionary<int, Cache>();

        /// <summary>What a kind of line is called: "conduits", "sewage pipes".</summary>
        public static string KindLabel(string kind) =>
            kind == Power ? "CC_Relay_Conduits".Translate().Resolve() : "CC_Relay_Pipes".Translate(BaseReview.PipeLabel(kind).Named("KIND")).Resolve();

        /// <summary>How long a map's plans are kept: the map's own placing checks are dear, and lines seldom move.</summary>
        private const int KeepTicks = Sched.HourTicks * 6;

        /// <summary>Every net of the map planned again (kept six hours, unless <paramref name="force"/>d), those with a few lines in sight at least.</summary>
        public static List<NetPlan> Plans(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new List<NetPlan>();
            if (!caches.TryGetValue(map.uniqueID, out var c)) caches[map.uniqueID] = c = new Cache();
            int now = Find.TickManager.TicksGame;
            if (force || now - c.tick > KeepTicks || now < c.tick)
            {
                c.tick = now;
                c.plans = new List<NetPlan>();
                try
                {
                    var planned = new Dictionary<string, HashSet<IntVec3>>();
                    foreach (var m in Models(map, planned))
                    {
                        // A net with hardly a line in sight has nothing worth the routing.
                        if (m.net.lines.Count(i => !LineRouteMath.Hidden(m.net.spots[i])) < MinInSight) continue;
                        var p = LineRouteMath.Route(m.net);
                        if (!p.ok) continue;
                        var plan = new NetPlan
                        {
                            kind = m.kind, line = m.line, stuff = m.stuff, inSightBefore = p.inSightBefore, inSightAfter = p.inSightAfter,
                            lay = p.lay.Select(m.Cell).ToList(), takeUp = p.takeUp.Select(m.Cell).ToList()
                        };
                        if (!planned.TryGetValue(m.kind, out var set)) planned[m.kind] = set = new HashSet<IntVec3>();
                        set.UnionWith(plan.lay);
                        c.plans.Add(plan);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("lines under the walls", e);
                }
            }
            return c.plans;
        }

        // ---------------- Reading the nets ----------------

        /// <summary>A piece of line: one cell, not standing in the way of anything (a conduit, a pipe).</summary>
        private static bool IsLine(Thing t) => t?.def?.building != null && !t.def.building.isEdifice && t.def.size == IntVec2.One;

        private static ThingDef LineOf(IEnumerable<Thing> members) =>
            members.Where(IsLine).GroupBy(t => t.def).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.defName).FirstOrDefault()?.Key;

        private static ThingDef StuffOf(IEnumerable<Thing> lines) =>
            lines.Where(t => t.Stuff != null).GroupBy(t => t.Stuff).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.defName).FirstOrDefault()?.Key;

        private static CellRect RegionOf(Map map, IEnumerable<Thing> members, IEnumerable<IntVec3> also)
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            void Add(IntVec3 c)
            {
                minX = Math.Min(minX, c.x);
                minZ = Math.Min(minZ, c.z);
                maxX = Math.Max(maxX, c.x);
                maxZ = Math.Max(maxZ, c.z);
            }
            foreach (var t in members)
                foreach (var c in t.OccupiedRect()) Add(c);
            foreach (var c in also) Add(c);
            if (minX > maxX) return CellRect.Empty;
            return CellRect.FromLimits(minX, minZ, maxX, maxZ).ExpandedBy(Margin).ClipInsideMap(map);
        }

        /// <summary>Where a cell stands for a line: under a wall or door, under furniture, on a floor (built, or under a roof), on open ground.</summary>
        private static LineRouteMath.Spot SpotOf(Map map, IntVec3 c)
        {
            var edifice = c.GetEdifice(map);
            if (edifice != null)
            {
                if (edifice.def.Fillage == FillCategory.Full || edifice.def.IsDoor) return LineRouteMath.Spot.Wall;
                return LineRouteMath.Spot.Furniture;
            }
            var terrain = map.terrainGrid.TerrainAt(c);
            if (terrain != null && terrain.layerable || c.Roofed(map)) return LineRouteMath.Spot.Floor;
            return LineRouteMath.Spot.Ground;
        }

        private static Model NewModel(Map map, string kind, ThingDef line, ThingDef stuff, CellRect region, List<Thing> lines, HashSet<IntVec3> shut)
        {
            var m = new Model { kind = kind, line = line, stuff = stuff, region = region };
            int size = region.Width * region.Height;
            var n = m.net = new LineRouteMath.Net { w = region.Width, h = region.Height, spots = new LineRouteMath.Spot[size] };
            for (int i = 0; i < size; i++) n.spots[i] = SpotOf(map, m.Cell(i));
            foreach (var t in lines)
            {
                int i = m.Index(t.Position);
                if (i >= 0) n.lines.Add(i);
            }
            var known = new Dictionary<int, bool>();
            n.canLayAt = i =>
            {
                if (known.TryGetValue(i, out bool ok)) return ok;
                var c = m.Cell(i);
                try
                {
                    ok = !shut.Contains(c) && c.InBounds(map) && !c.Fogged(map)
                         && GenConstruct.CanPlaceBlueprintAt(line, c, Rot4.North, map, stuffDef: stuff).Accepted;
                }
                catch
                {
                    ok = false;
                }
                known[i] = ok;
                return ok;
            };
            return m;
        }

        private static LineRouteMath.Anchor AnchorOf(Model m, Thing t, bool wires)
        {
            var a = new LineRouteMath.Anchor { wireAt = wires ? m.Index(t.Position) : -1 };
            foreach (var c in t.OccupiedRect())
            {
                int i = m.Index(c);
                if (i >= 0) a.cells.Add(i);
            }
            return a;
        }

        private static void ShutAround(HashSet<IntVec3> shut, IEnumerable<IntVec3> cells)
        {
            foreach (var c in cells)
            {
                shut.Add(c);
                for (int d = 0; d < 4; d++) shut.Add(c + GenAdj.CardinalDirections[d]);
            }
        }

        /// <summary>Every net of the map, read in turn; each later one stays clear of what the earlier ones planned (<paramref name="planned"/>, by kind).</summary>
        private static IEnumerable<Model> Models(Map map, Dictionary<string, HashSet<IntVec3>> planned)
        {
            foreach (var m in PowerModels(map, planned)) yield return m;
            foreach (var m in PipeModels(map, planned)) yield return m;
        }

        private static IEnumerable<Model> PowerModels(Map map, Dictionary<string, HashSet<IntVec3>> planned)
        {
            var nets = map.powerNetManager?.AllNetsListForReading;
            if (nets == null) yield break;
            int reach = (int)LineRouteMath.Reach;
            foreach (var net in nets.Where(x => x?.transmitters != null).OrderByDescending(x => x.transmitters.Count).ToList())
            {
                var members = net.transmitters.Where(c => c?.parent != null && c.parent.Spawned && c.parent.Map == map).Select(c => (Thing)c.parent).Distinct().ToList();
                var line = LineOf(members);
                if (line == null || !Avail.CanBuild(line, map)) continue;
                var lines = members.Where(t => t.def == line).ToList();
                var wired = net.connectors.Where(c => c?.parent != null && c.parent.Spawned && c.connectParent?.parent != null && c.connectParent.parent.def == line)
                    .Select(c => c.parent).ToList();
                var region = RegionOf(map, members, wired.Select(t => t.Position));
                if (region.Area == 0) continue;
                planned.TryGetValue(Power, out var earlier);
                var shut = new HashSet<IntVec3>();
                if (earlier != null) ShutAround(shut, earlier);
                // Other nets: not beside their pieces, and nowhere within a powered piece's reach of theirs.
                foreach (var c in region)
                {
                    foreach (var t in map.thingGrid.ThingsListAtFast(c))
                    {
                        var comp = (t as ThingWithComps)?.GetComp<CompPower>();
                        if (comp == null) continue;
                        if (comp.Props.transmitsPower)
                        {
                            if (comp.transNet != net) ShutAround(shut, t.OccupiedRect());
                        }
                        else if (comp.connectParent != null && comp.connectParent.transNet != net && t.Position == c)
                        {
                            foreach (var r in CellRect.CenteredOn(c, reach)) shut.Add(r);
                        }
                    }
                }
                var m = NewModel(map, Power, line, null, region, lines, shut);
                foreach (var a in members.Where(t => t.def != line))
                    m.net.anchors.Add(AnchorOf(m, a, a.def.building == null || a.def.building.allowWireConnection));
                foreach (var t in wired)
                {
                    var at = t.Position;
                    int other = int.MaxValue;
                    foreach (var c in CellRect.CenteredOn(at, reach).ClipInsideMap(map))
                    {
                        var tr = c.GetTransmitter(map);
                        var pc = tr?.PowerComp;
                        if (pc != null && pc.transNet != net && pc.TransmitsPowerNow && (tr.def.building == null || tr.def.building.allowWireConnection))
                            other = Math.Min(other, (tr.Position - at).LengthHorizontalSquared);
                        if (earlier != null && earlier.Contains(c)) other = Math.Min(other, (c - at).LengthHorizontalSquared);
                    }
                    int i = m.Index(at);
                    if (i >= 0) m.net.reachers.Add(new LineRouteMath.Reacher { x = i % region.Width, z = i / region.Width, otherSq = other });
                }
                yield return m;
            }
        }

        private static IEnumerable<Model> PipeModels(Map map, Dictionary<string, HashSet<IntVec3>> planned)
        {
            var byKey = new Dictionary<string, List<Building>>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                string key = BaseReview.PipeKey(b);
                if (key == null) continue;
                if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<Building>();
                list.Add(b);
            }
            foreach (var kv in byKey.OrderBy(k => k.Key))
            {
                // The pieces joined through each other, as a net: on the same cell or side by side.
                var at = new Dictionary<IntVec3, List<Building>>();
                foreach (var b in kv.Value)
                    foreach (var c in b.OccupiedRect())
                    {
                        if (!at.TryGetValue(c, out var l)) at[c] = l = new List<Building>();
                        l.Add(b);
                    }
                var seen = new HashSet<Building>();
                var nets = new List<List<Building>>();
                foreach (var b in kv.Value)
                {
                    if (!seen.Add(b)) continue;
                    var net = new List<Building>();
                    var stack = new Stack<Building>();
                    stack.Push(b);
                    while (stack.Count > 0)
                    {
                        var x = stack.Pop();
                        net.Add(x);
                        foreach (var c in x.OccupiedRect())
                        {
                            for (int d = -1; d < 4; d++)
                            {
                                var n = d < 0 ? c : c + GenAdj.CardinalDirections[d];
                                if (!at.TryGetValue(n, out var there)) continue;
                                foreach (var y in there)
                                    if (seen.Add(y)) stack.Push(y);
                            }
                        }
                    }
                    nets.Add(net);
                }
                foreach (var net in nets.OrderByDescending(x => x.Count))
                {
                    var members = net.Cast<Thing>().ToList();
                    var line = LineOf(members);
                    if (line == null || !Avail.CanBuild(line, map)) continue;
                    var lines = members.Where(t => t.def == line).ToList();
                    var region = RegionOf(map, members, Enumerable.Empty<IntVec3>());
                    if (region.Area == 0) continue;
                    var shut = new HashSet<IntVec3>();
                    if (planned.TryGetValue(kv.Key, out var earlier)) ShutAround(shut, earlier);
                    foreach (var other in nets)
                        if (other != net)
                            foreach (var b in other)
                                ShutAround(shut, b.OccupiedRect());
                    var m = NewModel(map, kv.Key, line, StuffOf(lines), region, lines, shut);
                    foreach (var a in members.Where(t => t.def != line)) m.net.anchors.Add(AnchorOf(m, a, false));
                    yield return m;
                }
            }
        }

        /// <summary>The model of the net of this kind that has a line on one of <paramref name="cells"/>, read as it stands.</summary>
        private static Model ModelAt(Map map, string kind, IEnumerable<IntVec3> cells)
        {
            var want = new HashSet<IntVec3>(cells);
            var models = kind == Power ? PowerModels(map, new Dictionary<string, HashSet<IntVec3>>()) : PipeModels(map, new Dictionary<string, HashSet<IntVec3>>());
            foreach (var m in models)
            {
                if (m.kind != kind) continue;
                foreach (int i in m.net.lines)
                    if (want.Contains(m.Cell(i))) return m;
            }
            return null;
        }

        // ---------------- Laying it out ----------------

        /// <summary>
        /// Plans the new lines of every net of this kind as blueprints, and leaves the old ones to the tracker: they come up
        /// once the new ones stand. A plan that only takes up lines nothing needs gives its orders at once.
        /// </summary>
        public static void Lay(Map map, string kind)
        {
            var comp = CouncilGameComponent.Instance;
            if (map == null || comp == null) return;
            int placed = 0, up = 0;
            foreach (var p in Plans(map, true).Where(x => x.kind == kind && x.inSightAfter < x.inSightBefore))
            {
                var relay = new LineRelay { mapId = map.uniqueID, kind = kind, line = p.line, stuff = p.stuff, started = Find.TickManager.TicksGame };
                foreach (var c in p.lay)
                {
                    if (!GenConstruct.CanPlaceBlueprintAt(p.line, c, Rot4.North, map, stuffDef: p.stuff).Accepted) continue;
                    GenConstruct.PlaceBlueprintForBuild(p.line, c, map, Rot4.North, Faction.OfPlayer, p.stuff);
                    relay.lay.Add(c);
                }
                relay.takeUp.AddRange(p.takeUp);
                placed += relay.lay.Count;
                up += p.takeUp.Count(c => !LineRouteMath.Hidden(SpotOf(map, c)));
                if (relay.lay.Count == 0) relay.Finish(map, true);
                else comp.relays.list.Add(relay);
            }
            if (placed > 0)
                Messages.Message("CC_Relay_Placed".Translate(placed.Named("N"), KindLabel(kind).Named("KIND"), up.Named("UP")).Resolve(), MessageTypeDefOf.TaskCompletion, false);
            caches.Remove(map.uniqueID);
            BaseReview.For(map, true);
        }

        /// <summary>For the tracker: of the old lines, the ones the net can do without as it stands now.</summary>
        internal static List<Thing> Removable(Map map, string kind, ThingDef line, List<IntVec3> lay, List<IntVec3> takeUp)
        {
            var result = new List<Thing>();
            var m = ModelAt(map, kind, lay.Count > 0 ? lay : takeUp);
            if (m == null) return result;
            var candidates = takeUp.Select(m.Index).Where(i => i >= 0).ToList();
            foreach (int i in LineRouteMath.Removable(m.net, candidates))
            {
                var t = m.Cell(i).GetThingList(map).FirstOrDefault(x => x.def == line);
                if (t != null) result.Add(t);
            }
            return result;
        }

        /// <summary>The lines still to stand of a relay, and those whose orders are gone without being built.</summary>
        internal static void Count(Map map, ThingDef line, List<IntVec3> lay, out int built, out int waiting, out int gone)
        {
            built = waiting = gone = 0;
            foreach (var c in lay)
            {
                var things = c.GetThingList(map);
                if (things.Any(t => t.def == line)) built++;
                else if (things.Any(t => t.def.entityDefToBuild == line && (t is Blueprint || t is Frame))) waiting++;
                else gone++;
            }
        }

        /// <summary>The Base page's findings: a kind of line with fewer in sight laid again, or a laying still under way.</summary>
        internal static void Findings(Map map, List<BaseFinding> findings)
        {
            var comp = CouncilGameComponent.Instance;
            var busy = new HashSet<string>();
            if (comp != null)
                foreach (var r in comp.relays.list.Where(r => r.mapId == map.uniqueID))
                {
                    if (!busy.Add(r.kind)) continue;
                    var all = comp.relays.list.Where(x => x.mapId == map.uniqueID && x.kind == r.kind).ToList();
                    int built = 0, total = 0;
                    foreach (var x in all)
                    {
                        Count(map, x.line, x.lay, out int b, out _, out _);
                        built += b;
                        total += x.lay.Count;
                    }
                    string kindLabel = KindLabel(r.kind);
                    string kind = r.kind;
                    findings.Add(new BaseFinding
                    {
                        key = "relaying:" + kind, group = "works", score = 0.05f,
                        title = "CC_Base_Relaying".Translate(kindLabel.Named("KIND"), built.Named("BUILT"), total.Named("ALL")).Resolve(),
                        detail = "CC_Base_Relaying_Detail".Translate().Resolve(),
                        look = all.SelectMany(x => x.lay).FirstOrDefault(), fixLabel = "CC_Base_Relay_Off".Translate().Resolve(),
                        fix = () => comp.relays.CallOff(map, kind),
                        cells = all.SelectMany(x => x.lay).ToList(), other = all.SelectMany(x => x.takeUp).ToList(),
                        mapLabel = "CC_Base_Map_Relay".Translate().Resolve()
                    });
                }
            foreach (var g in Plans(map).GroupBy(p => p.kind))
            {
                if (busy.Contains(g.Key)) continue;
                var plans = g.Where(p => p.inSightAfter < p.inSightBefore).ToList();
                int before = plans.Sum(p => p.inSightBefore), after = plans.Sum(p => p.inSightAfter);
                if (before - after < MinInSight) continue;
                string kindLabel = KindLabel(g.Key);
                string kind = g.Key;
                var lay = plans.SelectMany(p => p.lay).ToList();
                var up = plans.SelectMany(p => p.takeUp).ToList();
                findings.Add(new BaseFinding
                {
                    key = "relay:" + kind, group = "works", score = BaseReviewMath.TidyScore(before - after, 0.01f, 0.25f),
                    title = "CC_Base_Relay".Translate((before - after).Named("N"), kindLabel.Named("KIND")).Resolve(),
                    detail = "CC_Base_Relay_Detail".Translate(lay.Count.Named("LAY"), up.Count.Named("UP"), before.Named("BEFORE"), after.Named("AFTER"), kindLabel.Named("KIND")).Resolve(),
                    look = lay.Count > 0 ? lay[0] : up.FirstOrDefault(), fixLabel = "CC_Base_Relay_Fix".Translate().Resolve(), fix = () => Lay(map, kind),
                    cells = lay, other = up, mapLabel = "CC_Base_Map_Relay".Translate().Resolve()
                });
            }
        }
    }

    /// <summary>
    /// One net's lines being laid again: the new ones planned, and the old ones to take up once all the new ones stand.
    /// </summary>
    public class LineRelay : IExposable
    {
        public int mapId, started;
        public string kind;
        public ThingDef line, stuff;
        public List<IntVec3> lay = new List<IntVec3>(), takeUp = new List<IntVec3>();

        public Map Map => Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);

        /// <summary>Gives the old lines the net can do without their deconstruct orders, and says so.</summary>
        public void Finish(Map map, bool atOnce = false)
        {
            var things = LineRoutes.Removable(map, kind, line, lay, takeUp);
            var dm = map.designationManager;
            int n = 0;
            foreach (var t in things)
            {
                if (t.Destroyed || !t.Spawned || t.Faction != Faction.OfPlayer || dm.DesignationOn(t, DesignationDefOf.Deconstruct) != null) continue;
                dm.AddDesignation(new Designation(t, DesignationDefOf.Deconstruct));
                n++;
            }
            int stay = takeUp.Count(c => c.GetThingList(map).Any(t => t.def == line)) - things.Count;
            string text = (atOnce ? "CC_Relay_UpNow" : "CC_Relay_Done").Translate(LineRoutes.KindLabel(kind).Named("KIND"), n.Named("N"), lay.Count.Named("LAID")).Resolve();
            if (stay > 0) text += " " + "CC_Relay_Kept".Translate(stay.Named("N")).Resolve();
            Messages.Message(text, things.Count > 0 ? new LookTargets(things[0]) : null, MessageTypeDefOf.TaskCompletion, false);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Defs.Look(ref line, "line");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Collections.Look(ref lay, "lay", LookMode.Value);
            Scribe_Collections.Look(ref takeUp, "takeUp", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lay ??= new List<IntVec3>();
                takeUp ??= new List<IntVec3>();
            }
        }
    }

    /// <summary>The lines being laid again (on the game component as relays), looked at every hour: once a net's new lines all stand, its old ones come up.</summary>
    public class LineRelayTracker : IExposable
    {
        public List<LineRelay> list = new List<LineRelay>();

        public void Hourly(CouncilGameComponent comp)
        {
            foreach (var r in list.ToList())
            {
                var map = r.Map;
                if (map == null || r.line == null)
                {
                    list.Remove(r);
                    continue;
                }
                LineRoutes.Count(map, r.line, r.lay, out _, out int waiting, out int gone);
                if (gone > 0)
                {
                    // An order cancelled, or a new line lost before it stood: the old lines stay as they are.
                    list.Remove(r);
                    Messages.Message("CC_Relay_Dropped".Translate(LineRoutes.KindLabel(r.kind).Named("KIND")).Resolve(), MessageTypeDefOf.NeutralEvent, false);
                    continue;
                }
                if (waiting > 0) continue;
                list.Remove(r);
                r.Finish(map);
            }
        }

        /// <summary>Cancels the new lines' orders still standing and leaves the old lines as they are.</summary>
        public void CallOff(Map map, string kind)
        {
            foreach (var r in list.Where(x => x.mapId == map.uniqueID && x.kind == kind).ToList())
            {
                foreach (var c in r.lay)
                    foreach (var t in c.GetThingList(map).Where(t => t.def.entityDefToBuild == r.line && (t is Blueprint || t is Frame)).ToList())
                        t.Destroy(DestroyMode.Cancel);
                list.Remove(r);
            }
            Messages.Message("CC_Relay_CalledOff".Translate(LineRoutes.KindLabel(kind).Named("KIND")).Resolve(), MessageTypeDefOf.SilentInput, false);
            BaseReview.For(map, true);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref list, "list", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) list ??= new List<LineRelay>();
        }
    }
}
