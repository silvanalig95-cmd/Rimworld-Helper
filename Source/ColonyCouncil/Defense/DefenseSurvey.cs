using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's situation for its defense, read from the map: where raiders can come from and the narrow places on
    /// their way (<see cref="DefenseMath"/>), what already defends it, what it can build, who fights, and the threats it
    /// faces (the raids its wealth buys, the hostile peoples around, the DLC threats that are on). Worked out on demand,
    /// never more than once a game hour unless asked, and kept until a day has passed, the colony's buildings change a
    /// lot, or a wall, door, turret, trap or cover is finished or taken away near a line or a gap (<see cref="Notify_Changed"/>,
    /// one look per building, from the game's own construction and despawning). The door rule: a defense line's gate and
    /// the doors in its wall are its way through, and a door held open is open, so a shut gate never sends the ways in
    /// elsewhere. A gap a line holds already is marked as that line's (<see cref="CoveringLine"/>). Nothing here ticks.
    /// </summary>
    public class DefenseSurvey
    {
        public Map map;
        public int tick;
        public int buildings;
        public DefenseMath.MapGrid grid;
        public DefenseMath.Result result;
        public IntVec3 center;
        public IntVec3 bedCenter = IntVec3.Invalid;

        // ---------- Threats ----------
        public float raidPoints;
        public List<Faction> hostiles = new List<Faction>();
        public bool tribal, medieval, industrial, empire, mechanoids, mechsYet, canDrop, canSiege, canBreach;
        public bool insects, anomaly, mechClusters;
        public int wastepacks;
        /// <summary>Open cells in the home area under overhead mountain, and those of them dark and warm (where insects like to nest).</summary>
        public int thickRoof, darkWarm;

        // ---------- Fighters ----------
        public DefenseUtil.Assessment assess;
        public int fighters, ranged, melee, unarmed;
        public Pawn bestShooter, bestMelee;
        public float bestRange;
        /// <summary>Most of the ranged weapons are bows, crossbows and the like.</summary>
        public bool bows;

        // ---------- What stands already ----------
        public int walls, doors, autoTurrets, mannedTurrets, mortars, traps, cover, embrasures, trapsInHome;
        public int weaponsNearBeds, medicine;
        public bool hospital, emp;
        /// <summary>Doors on the busiest way in: two or more mean a second line behind the first.</summary>
        public int doorsOnWay;
        /// <summary>Of the walls facing the outside, the share with a second wall (or rock) behind them.</summary>
        public float doubleWalls;
        public int outerWalls;

        // ---------- Where to build ----------
        public List<ChokeInfo> chokes = new List<ChokeInfo>();
        /// <summary>No narrow place: where the busiest way in crosses open ground a little outside home, and the side it faces.</summary>
        public IntVec3 approach = IntVec3.Invalid;
        public int approachFacing;
        /// <summary>The side most raids come from (0 north .. 3 west).</summary>
        public int mainFacing;

        public class ChokeInfo
        {
            public DefenseMath.Choke c;
            public IntVec3 cell;
            /// <summary>A gate (door) already closes the gap: in the colony's wall, or between the rock faces.</summary>
            public bool gated;
            public int coverInside, trapsOutside, turretsNear, woodWalls;
            /// <summary>The defense line that holds this gap already (its id), or -1; how; and whether that line's gate stands.</summary>
            public int coveredBy = -1;
            public DefenseLineMath.Covered coverHow;
            public bool lineHeld;
            public IntVec3 Outward => IntVec3.North.RotatedBy(new Rot4(c.facing));
            /// <summary>Held by a gate: its own, or the one of the line that holds it.</summary>
            public bool Held => gated || coveredBy >= 0 && lineHeld;
        }

        public bool Reachable => result != null && !result.unreachable;

        public ChokeInfo Main => chokes.Count > 0 ? chokes[0] : null;

        private static readonly Dictionary<Map, DefenseSurvey> cache = new Dictionary<Map, DefenseSurvey>();
        private static Game cacheGame;

        /// <summary>
        /// The survey for a map: the one kept, unless it's a day old, or an hour old and the colony's buildings have
        /// changed a lot since (or <paramref name="force"/>). Null for a map that isn't a home.
        /// </summary>
        public static DefenseSurvey For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return null;
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                failed.Clear();
                dirty.Clear();
                cacheGame = Current.Game;
            }
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(map, out var s) && !force)
            {
                int age = now - s.tick;
                if (age >= 0 && age < GenDate.TicksPerHour) return s;
                int count = map.listerBuildings.allBuildingsColonist.Count;
                bool changed = dirty.Contains(map) || Mathf.Abs(count - s.buildings) >= Mathf.Max(6, s.buildings / 30);
                if (age >= 0 && age < GenDate.TicksPerDay && !changed) return s;
            }
            // A map the survey couldn't read isn't tried again for an hour (the page asks every second).
            if (s == null && !force && failed.TryGetValue(map, out int failedAt) && now - failedAt >= 0 && now - failedAt < GenDate.TicksPerHour) return null;
            // Maps that have gone (a camp left, a site abandoned) are let go.
            if (cache.Count > 0) Prune(cache);
            if (dirty.Count > 1 && Find.Maps != null) dirty.RemoveWhere(m => m != map && !Find.Maps.Contains(m));
            bool first = s == null;
            try
            {
                s = Survey(map);
                failed.Remove(map);
                dirty.Remove(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey", e);
                if (s == null)
                {
                    failed[map] = now;
                    return null;
                }
                s.tick = now;
            }
            cache[map] = s;
            // The busiest way in moved (a wall built, a gap closed): the lines say so once.
            try
            {
                DefenseLines.Of(map)?.NoteMainWay(s, first);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: the busiest way in", e);
            }
            return s;
        }

        private static readonly Dictionary<Map, int> failed = new Dictionary<Map, int>();
        private static readonly HashSet<Map> dirty = new HashSet<Map>();

        /// <summary>Something that changes the ways in was built or taken away near a line or a gap: the survey is made again once it's an hour old.</summary>
        public static void MarkDirty(Map map)
        {
            if (map != null) dirty.Add(map);
        }

        /// <summary>The survey is waiting to be made again (after a change near a line or a gap).</summary>
        public static bool Dirty(Map map) => map != null && dirty.Contains(map);

        /// <summary>How close to a line or a known gap a change has to be to count.</summary>
        public const int NearChange = 6;

        /// <summary>
        /// A building was finished or is going (built, destroyed, taken down or uninstalled): if it's a wall, a door, a
        /// turret, a trap, cover, a fence or rock, within a few cells of a defense line or a gap the survey found, the
        /// survey is marked to be made again. One look per event, nothing kept.
        /// </summary>
        public static void Notify_Changed(Map map, IntVec3 cell, ThingDef def, Faction faction)
        {
            if (map == null || def?.building == null || !cell.IsValid || dirty.Contains(map) || !map.IsPlayerHome) return;
            bool rock = def.building.isNaturalRock || def.mineable && faction == null;
            if (!rock && faction != Faction.OfPlayer) return;
            if (!rock && !Matters(def)) return;
            if (NearLineOrGap(map, cell)) dirty.Add(map);
        }

        /// <summary>Buildings that change how raiders walk in or meet a line.</summary>
        private static bool Matters(ThingDef def) =>
            def.IsDoor || TemplatePlanner.IsWallLike(def) || def.passability == Traversability.Impassable || Avail.IsTrap(def) || Avail.IsCover(def)
            || def.building.isFence || def.thingClass != null && typeof(Building_Turret).IsAssignableFrom(def.thingClass);

        private static bool NearLineOrGap(Map map, IntVec3 cell)
        {
            var lines = DefenseLines.Of(map)?.lines;
            if (lines != null)
                foreach (var l in lines)
                {
                    if (Close(cell, l.gate, Mathf.Max(NearChange, l.width / 2 + NearChange))) return true;
                    foreach (var c in l.span)
                        if (Close(cell, c, NearChange)) return true;
                    // Or on (or just by) a plan placed for the line: a trap corridor out in front, a killbox behind.
                    foreach (var p in l.plans)
                    {
                        var pg = p.Grid;
                        if (pg == null) continue;
                        TemplateMath.RotatedSize(pg.W, pg.H, p.rotation, out int w, out int h);
                        if (cell.x >= p.anchor.x - 2 && cell.x < p.anchor.x + w + 2 && cell.z >= p.anchor.z - 2 && cell.z < p.anchor.z + h + 2) return true;
                    }
                }
            var s = Kept(map);
            if (s?.grid == null) return false;
            foreach (var info in s.chokes)
                foreach (int i in info.c.span)
                    if (Close(cell, new IntVec3(i % s.grid.w, 0, i / s.grid.w), NearChange)) return true;
            return false;
        }

        private static bool Close(IntVec3 a, IntVec3 b, int d) => Mathf.Abs(a.x - b.x) <= d && Mathf.Abs(a.z - b.z) <= d;

        private static void Prune(Dictionary<Map, DefenseSurvey> kept)
        {
            var maps = Find.Maps;
            if (maps == null) return;
            List<Map> gone = null;
            foreach (var m in kept.Keys)
                if (!maps.Contains(m)) (gone ??= new List<Map>()).Add(m);
            if (gone != null)
                foreach (var m in gone) kept.Remove(m);
        }

        /// <summary>The survey kept for a map, without working one out.</summary>
        public static DefenseSurvey Kept(Map map) => map != null && cacheGame == Current.Game && cache.TryGetValue(map, out var s) ? s : null;

        public static void Forget()
        {
            cache.Clear();
            failed.Clear();
            dirty.Clear();
        }

        private static DefenseSurvey Survey(Map map)
        {
            var s = new DefenseSurvey { map = map, tick = Find.TickManager.TicksGame, buildings = map.listerBuildings.allBuildingsColonist.Count };
            s.center = TemplatePlanner.ColonyCenter(map);
            s.grid = Grid(map, s);
            s.result = DefenseMath.Analyse(s.grid);
            s.mainFacing = Mathf.RoundToInt(s.result.angle / 90f) % 4;
            Threats(map, s);
            Fighters(map, s);
            Standing(map, s);
            Places(map, s);
            return s;
        }

        // ---------------- The map as a grid ----------------

        private static DefenseMath.MapGrid Grid(Map map, DefenseSurvey s)
        {
            int w = map.Size.x, h = map.Size.z;
            var m = new DefenseMath.MapGrid(w, h) { noBuildEdge = GenGrid.NoBuildEdgeWidth, centerX = s.center.x, centerZ = s.center.z };
            var pg = map.pathing?.Normal?.pathGrid;
            var home = map.areaManager.Home;
            var player = Faction.OfPlayer;
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new IntVec3(x, 0, z);
                    int i = m.Index(x, z);
                    m.home[i] = home[c];
                    m.kind[i] = KindAt(map, c, player, pg, out int cost);
                    m.cost[i] = cost;
                }
            }
            // Where people live: the rooms with the colonists' beds, else round the colony's middle.
            var beds = MapFacts.Of(map).Beds;
            long bx = 0, bz = 0;
            int bn = 0;
            foreach (var bed in beds)
            {
                if (bed == null || !bed.Spawned || bed.Medical || bed.ForPrisoners) continue;
                bx += bed.Position.x;
                bz += bed.Position.z;
                bn++;
                var room = bed.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors && room.ProperRoom && room.CellCount <= 400)
                {
                    foreach (var c in room.Cells) m.core[m.Index(c.x, c.z)] = true;
                }
                else
                {
                    foreach (var c in GenRadial.RadialCellsAround(bed.Position, 2f, true))
                        if (c.InBounds(map)) m.core[m.Index(c.x, c.z)] = true;
                }
            }
            if (bn > 0) s.bedCenter = new IntVec3((int)(bx / bn), 0, (int)(bz / bn));
            else
                foreach (var c in GenRadial.RadialCellsAround(s.center, 5f, true))
                    if (c.InBounds(map)) m.core[m.Index(c.x, c.z)] = true;
            Doors(map, m);
            return m;
        }

        /// <summary>
        /// The door rule on the grid: a door held open is open ground to raiders, and so is every defense line's gate and
        /// each door in its wall (a shut gate is still the line's way through: raiders break it rather than go far round).
        /// </summary>
        private static void Doors(Map map, DefenseMath.MapGrid m)
        {
            foreach (var b in Avail.ColonyBuildings(map, d => d.IsDoor))
            {
                if (!(b is Building_Door door) || !door.HoldOpen || !b.Position.InBounds(map)) continue;
                int i = m.Index(b.Position.x, b.Position.z);
                if (m.kind[i] == DefenseMath.Door) m.free[i] = true;
            }
            var lines = DefenseLines.Of(map)?.lines;
            if (lines == null) return;
            foreach (var l in lines)
            {
                if (l.gate.InBounds(map)) DefenseLineMath.MarkLineDoors(m, l.gate.x, l.gate.z, l.facing);
                // A door anywhere across the line's gap is its gate too, wherever the line's own mark stands.
                foreach (var c in l.span)
                {
                    if (!c.InBounds(map)) continue;
                    int i = m.Index(c.x, c.z);
                    if (m.kind[i] == DefenseMath.Door) m.free[i] = true;
                }
            }
        }

        // ---------------- The grid as it stands now ----------------

        /// <summary>Changes to the survey's grid that are put back when it's disposed.</summary>
        public sealed class GridPatch : IDisposable
        {
            private readonly DefenseMath.MapGrid g;
            private readonly List<(int i, byte k, bool f, int c)> undo = new List<(int, byte, bool, int)>();

            public GridPatch(DefenseMath.MapGrid g)
            {
                this.g = g;
            }

            public void Set(int i, byte kind, bool free, int cost)
            {
                if (g == null || i < 0 || i >= g.w * g.h) return;
                undo.Add((i, g.kind[i], g.free[i], g.cost[i]));
                g.kind[i] = kind;
                g.free[i] = free;
                g.cost[i] = cost;
            }

            public void Dispose()
            {
                if (g == null) return;
                for (int u = undo.Count - 1; u >= 0; u--)
                {
                    var (i, k, f, c) = undo[u];
                    g.kind[i] = k;
                    g.free[i] = f;
                    g.cost[i] = c;
                }
                undo.Clear();
            }
        }

        /// <summary>
        /// The cells within <paramref name="radius"/> of a place read again from the map into the grid (what's been built,
        /// mined or held open since the survey counts), with the door rule for the lines there; put back on dispose.
        /// </summary>
        public GridPatch PatchAround(IntVec3 at, int radius)
        {
            var patch = new GridPatch(grid);
            if (grid == null || map == null || !at.InBounds(map)) return patch;
            var player = Faction.OfPlayer;
            var pg = map.pathing?.Normal?.pathGrid;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var c = new IntVec3(at.x + dx, 0, at.z + dz);
                    if (!c.InBounds(map)) continue;
                    int i = grid.Index(c.x, c.z);
                    byte k = KindAt(map, c, player, pg, out int cost);
                    bool free = k == DefenseMath.Door && (grid.free[i] && grid.kind[i] == DefenseMath.Door || c.GetEdifice(map) is Building_Door d && d.HoldOpen);
                    if (k != grid.kind[i] || cost != grid.cost[i] || free != grid.free[i]) patch.Set(i, k, free, cost);
                }
            var lines = DefenseLines.Of(map)?.lines;
            if (lines == null) return patch;
            foreach (var l in lines)
            {
                if (!l.gate.InBounds(map) || Mathf.Abs(l.gate.x - at.x) > radius + 20 || Mathf.Abs(l.gate.z - at.z) > radius + 20) continue;
                foreach (int i in DefenseLineMath.LineCells(grid, l.gate.x, l.gate.z, l.facing, 20))
                    if (grid.kind[i] == DefenseMath.Door && !grid.free[i]) patch.Set(i, DefenseMath.Door, true, grid.cost[i]);
                foreach (var c in l.span)
                {
                    if (!c.InBounds(map)) continue;
                    int i = grid.Index(c.x, c.z);
                    if (grid.kind[i] == DefenseMath.Door && !grid.free[i]) patch.Set(i, DefenseMath.Door, true, grid.cost[i]);
                }
                // A double gate: the door right behind it is the line's too.
                var inner = l.gate - l.Outward;
                for (int k = 0; k < 2 && inner.InBounds(map); k++, inner -= l.Outward)
                {
                    int i = grid.Index(inner.x, inner.z);
                    if (grid.kind[i] != DefenseMath.Door) break;
                    if (!grid.free[i]) patch.Set(i, DefenseMath.Door, true, grid.cost[i]);
                }
            }
            return patch;
        }

        // ---------------- Gaps the lines hold ----------------

        private int coverHash = int.MinValue;

        private static int LinesHash(Map map, DefenseLines comp)
        {
            if (comp == null) return 0;
            int h = 17;
            foreach (var l in comp.lines)
            {
                h = h * 31 + l.id;
                h = h * 31 + l.gate.x * 1000 + l.gate.z;
                h = h * 31 + l.facing;
                h = h * 31 + (l.gate.InBounds(map) && l.gate.GetEdifice(map) is Building_Door ? 1 : 0);
            }
            return h * 31 + comp.lines.Count;
        }

        /// <summary>What the survey knows of a line: its cells on the grid and the sampled walks through it.</summary>
        public DefenseLineMath.LineShape Shape(DefenseLine l)
        {
            var shape = new DefenseLineMath.LineShape { id = l.id, gx = l.gate.x, gz = l.gate.z, facing = DefenseLineMath.Norm(l.facing) };
            if (grid == null || !l.gate.InBounds(map)) return shape;
            shape.cells = DefenseLineMath.ShapeCells(grid, l.gate.x, l.gate.z, shape.facing, l.span.Where(c => c.InBounds(map)).Select(c => grid.Index(c.x, c.z)));
            shape.walks = DefenseLineMath.WalksThrough(result?.walked, shape.cells, grid);
            return shape;
        }

        /// <summary>Marks the gaps the lines hold already, when the lines (or their gates) have changed since it was last done.</summary>
        public void RefreshCover()
        {
            var comp = DefenseLines.Of(map);
            int hash = LinesHash(map, comp);
            if (hash == coverHash) return;
            coverHash = hash;
            foreach (var c in chokes)
            {
                // A gap that was a line's and isn't any more is counted round itself again.
                if (c.coveredBy >= 0) Around(map, c, c.cell, c.Outward);
                c.coveredBy = -1;
                c.coverHow = DefenseLineMath.Covered.No;
                c.lineHeld = false;
            }
            if (comp == null || comp.lines.Count == 0 || grid == null || result == null) return;
            try
            {
                var shapes = comp.lines.Select(Shape).ToList();
                foreach (var c in chokes)
                {
                    int k = DefenseLineMath.CoveredBy(grid, c.c, shapes, out var how);
                    if (k < 0) continue;
                    var line = comp.lines[k];
                    c.coveredBy = line.id;
                    c.coverHow = how;
                    c.lineHeld = line.gate.InBounds(map) && line.gate.GetEdifice(map) is Building_Door;
                    // Its cover, traps and turrets are the line's: counted round the line's gate.
                    if (line.gate.InBounds(map)) Around(map, c, line.gate, line.Outward);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: gaps the lines hold", e);
            }
        }

        /// <summary>The defense line that holds a gap already (its cells, the same pass a few cells off, or the same ways in), or null.</summary>
        public DefenseLine CoveringLine(ChokeInfo c)
        {
            if (c == null) return null;
            RefreshCover();
            return c.coveredBy >= 0 ? DefenseLines.Of(map)?.Get(c.coveredBy) : null;
        }

        /// <summary>The busiest gap a line holds, or null.</summary>
        public ChokeInfo GapOf(DefenseLine line)
        {
            if (line == null) return null;
            RefreshCover();
            return chokes.FirstOrDefault(c => c.coveredBy == line.id);
        }

        /// <summary>
        /// What a cell is for the survey: unexplored, a door, rock, the colony's wall, something else in the way, deep
        /// water, a bridge or open ground, and what it costs to walk.
        /// </summary>
        public static byte KindAt(Map map, IntVec3 c, Faction player, Verse.AI.PathGrid pg, out int cost)
        {
            cost = 0;
            if (c.Fogged(map)) return DefenseMath.Unknown;
            var ed = c.GetEdifice(map);
            if (ed != null && ed.def.IsDoor) return DefenseMath.Door;
            if (ed != null && ed.def.passability == Traversability.Impassable)
            {
                bool rock = ed.def.building != null && (ed.def.building.isNaturalRock || (ed.def.mineable && ed.Faction == null));
                return rock ? DefenseMath.Rock : ed.Faction == player ? DefenseMath.Wall : DefenseMath.Other;
            }
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.passability == Traversability.Impassable)
                return terrain != null && terrain.IsWater ? DefenseMath.Water : DefenseMath.Other;
            if (pg != null)
            {
                int raw = pg.Cost(c);
                cost = raw >= 10000 ? 600 : Mathf.Clamp(raw, 0, 600);
            }
            return terrain.bridge ? DefenseMath.Bridge : DefenseMath.Open;
        }

        // ---------------- Threats ----------------

        private static void Threats(Map map, DefenseSurvey s)
        {
            s.raidPoints = Mathf.Max(35f, StorytellerUtility.DefaultThreatPointsNow(map));
            var dropMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("CenterDrop");
            var dropTech = dropMode != null ? dropMode.minTechLevel : TechLevel.Industrial;
            var player = Faction.OfPlayer;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f == player || f.defeated || f.def == null) continue;
                bool hostile;
                try
                {
                    hostile = f.HostileTo(player);
                }
                catch
                {
                    continue;
                }
                if (!hostile) continue;
                if (f == Faction.OfMechanoids)
                {
                    s.mechanoids = true;
                    s.mechsYet = GenDate.DaysPassedSinceSettle >= f.def.earliestRaidDays;
                    continue;
                }
                if (f == Faction.OfInsects)
                {
                    s.insects = true;
                    continue;
                }
                if (ModsConfig.AnomalyActive && f == Faction.OfEntities) continue;
                if (!f.def.humanlikeFaction || f.Hidden) continue;
                s.hostiles.Add(f);
                var tech = f.def.techLevel;
                if (tech <= TechLevel.Neolithic) s.tribal = true;
                else if (tech == TechLevel.Medieval) s.medieval = true;
                else s.industrial = true;
                if (f.def.defName == "Empire") s.empire = true;
                if (f.def.canSiege) s.canSiege = true;
                if (tech >= dropTech) s.canDrop = true;
            }
            s.canBreach = s.hostiles.Count > 0 || (s.mechanoids && s.mechsYet);
            try
            {
                if (ModsConfig.RoyaltyActive && s.mechanoids)
                {
                    var cluster = DefDatabase<IncidentDef>.GetNamedSilentFail("MechCluster");
                    s.mechClusters = cluster != null && s.raidPoints >= cluster.minThreatPoints;
                }
                if (ModsConfig.AnomalyActive)
                    s.anomaly = Find.Storyteller?.difficulty?.AnomalyPlaystyleDef?.enableAnomalyContent ?? false;
                if (ModsConfig.BiotechActive && ThingDefOf.Wastepack != null)
                    s.wastepacks = map.listerThings.ThingsOfDef(ThingDefOf.Wastepack).Sum(t => t.stackCount);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: DLC threats", e);
            }
            // Overhead mountain over the home area: where insects can come up.
            if (Find.Storyteller?.difficulty == null || Find.Storyteller.difficulty.allowBigThreats)
            {
                foreach (var c in map.areaManager.Home.ActiveCells)
                {
                    var roof = c.GetRoof(map);
                    if (roof == null || !roof.isThickRoof || c.GetEdifice(map) != null) continue;
                    s.thickRoof++;
                    if (map.glowGrid.GroundGlowAt(c) < 0.3f && c.GetTemperature(map) > -8f) s.darkWarm++;
                }
            }
        }

        // ---------------- Fighters ----------------

        private static void Fighters(Map map, DefenseSurvey s)
        {
            s.assess = DefenseUtil.Assess(map);
            int low = 0, bestShoot = -1, bestMeleeSkill = -1;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.DevelopmentalStage.Juvenile() || p.IsQuestLodger()) continue;
                s.fighters++;
                var weapon = p.equipment?.Primary;
                int shooting = p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                if (weapon == null) s.unarmed++;
                else if (weapon.def.IsRangedWeapon)
                {
                    s.ranged++;
                    if (weapon.def.techLevel <= TechLevel.Medieval) low++;
                    float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                    if (range > s.bestRange) s.bestRange = range;
                    if (shooting > bestShoot)
                    {
                        bestShoot = shooting;
                        s.bestShooter = p;
                    }
                }
                else
                {
                    s.melee++;
                    if (meleeSkill > bestMeleeSkill)
                    {
                        bestMeleeSkill = meleeSkill;
                        s.bestMelee = p;
                    }
                }
            }
            s.bows = s.ranged > 0 && low * 2 >= s.ranged;
        }

        // ---------------- What stands already ----------------

        private static bool IsEmbrasure(ThingDef d) =>
            d?.building != null && (d.defName.IndexOf("Embrasure", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("EmbPalisade", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsMannedTurret(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !Avail.IsAutoTurret(d) && !DefenseTemplateDef.IsMortar(d);

        public static bool IsEmp(ThingDef d)
        {
            if (d?.Verbs == null) return false;
            foreach (var v in d.Verbs)
                if (v?.defaultProjectile?.projectile?.damageDef == DamageDefOf.EMP) return true;
            return false;
        }

        private static void Standing(Map map, DefenseSurvey s)
        {
            var home = map.areaManager.Home;
            foreach (var b in Avail.ColonyBuildings(map, d => d.building != null))
            {
                var d = b.def;
                if (d.IsDoor) s.doors++;
                else if (d.IsWall) s.walls++;
                if (DefenseTemplateDef.IsMortar(d)) s.mortars++;
                else if (Avail.IsAutoTurret(d)) s.autoTurrets++;
                else if (IsMannedTurret(d)) s.mannedTurrets++;
                if (Avail.IsTrap(d))
                {
                    s.traps++;
                    if (home[b.Position]) s.trapsInHome++;
                    if (b.def.thingClass != null && typeof(Building_TrapExplosive).IsAssignableFrom(b.def.thingClass) && b.def.defName.IndexOf("EMP", StringComparison.OrdinalIgnoreCase) >= 0) s.emp = true;
                }
                if (Avail.IsCover(d)) s.cover++;
                if (IsEmbrasure(d)) s.embrasures++;
            }
            s.medicine = MapFacts.Of(map).Medicine;
            s.hospital = RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.Hospital);
            // Weapons kept near the beds, and anything that fires EMP.
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (!t.Spawned) continue;
                if (s.bedCenter.IsValid && t.IsInAnyStorage() && t.Position.InHorDistOf(s.bedCenter, 15f)) s.weaponsNearBeds++;
                if (!s.emp && IsEmp(t.def)) s.emp = true;
            }
            if (!s.emp)
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (IsEmp(p.equipment?.Primary?.def) || (p.inventory?.innerContainer?.Any(t => IsEmp(t.def)) ?? false))
                    {
                        s.emp = true;
                        break;
                    }
            var empShell = DefDatabase<ThingDef>.GetNamedSilentFail("Shell_EMP");
            if (!s.emp && empShell != null && map.listerThings.ThingsOfDef(empShell).Count > 0) s.emp = true;

            // The busiest way in, on to the beds: how many doors it passes (a second door behind the gate is a line to fall back to).
            var r = s.result;
            if (r != null) s.doorsOnWay = r.doorsToCore;
            // Walls facing the outside, and how many have a second wall or rock behind them.
            if (r != null && !r.unreachable)
            {
                int thick = 0;
                var g = s.grid;
                foreach (var b in Avail.ColonyBuildings(map, d => d.IsWall))
                {
                    var c = b.Position;
                    if (!c.InBounds(map)) continue;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        var step = IntVec3.North.RotatedBy(new Rot4(dir));
                        var outside = c + step;
                        if (!outside.InBounds(map)) continue;
                        int oi = g.Index(outside.x, outside.z);
                        // Raiders' side: walkable, with a way in, and not where people live (the inside of the walls, or home).
                        if (!g.Passable(oi) || r.dist[oi] <= 0) continue;
                        // Faces raiders' ground: is there something solid behind it?
                        s.outerWalls++;
                        var behind = c - step;
                        if (behind.InBounds(map))
                        {
                            byte k = g.kind[g.Index(behind.x, behind.z)];
                            if (k == DefenseMath.Wall || k == DefenseMath.Rock) thick++;
                        }
                        break;
                    }
                }
                s.doubleWalls = s.outerWalls > 0 ? thick / (float)s.outerWalls : 0f;
            }
        }

        // ---------------- Where to build ----------------

        private static void Places(Map map, DefenseSurvey s)
        {
            var r = s.result;
            if (r == null) return;
            foreach (var c in r.chokes)
            {
                var info = new ChokeInfo { c = c, cell = new IntVec3(c.x, 0, c.z) };
                // Held when every way across the gap is a door: a gate in the colony's wall, or doors filling a narrow pass
                // between rock faces. A door with open ground beside it (a wall half built) holds nothing.
                info.gated = c.doors > 0 && c.doors >= c.span.Count;
                Around(map, info, info.cell, info.Outward);
                s.chokes.Add(info);
            }
            // Open country: where the busiest side's middle way in is a few steps outside home.
            if (r.shown.Count > 0)
            {
                int side = 0;
                for (int k = 1; k < 4; k++)
                    if (r.sideReach[k] > r.sideReach[side]) side = k;
                var ways = r.shown.Where(rt => rt.side == side).ToList();
                if (ways.Count == 0) ways = r.shown;
                var way = ways[ways.Count / 2];
                int last = way.cells.Count - 1;
                int at = Mathf.Max(0, last - 8);
                // Not inside the no-build margin by the map's edge.
                while (at < last && Near(way.cells[at], s.grid)) at++;
                int cell = way.cells[at];
                int back = way.cells[Mathf.Max(0, at - 6)];
                int dx = back % s.grid.w - cell % s.grid.w, dz = back / s.grid.w - cell / s.grid.w;
                s.approach = new IntVec3(cell % s.grid.w, 0, cell / s.grid.w);
                s.approachFacing = Mathf.Abs(dz) >= Mathf.Abs(dx) ? (dz >= 0 ? 0 : 2) : (dx >= 0 ? 1 : 3);
            }
        }

        /// <summary>What stands round a gap (or the gate of the line that holds it): cover behind, traps in front, turrets near, wooden walls.</summary>
        private static void Around(Map map, ChokeInfo info, IntVec3 at, IntVec3 outward)
        {
            info.coverInside = info.trapsOutside = info.turretsNear = info.woodWalls = 0;
            foreach (var b in Avail.ColonyBuildings(map, d => Avail.IsCover(d) || Avail.IsTrap(d) || d.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) || d.IsWall))
            {
                var off = b.Position - at;
                int ahead = off.x * outward.x + off.z * outward.z;
                float dist = off.LengthHorizontal;
                if (Avail.IsCover(b.def) && ahead <= 0 && dist <= 9f) info.coverInside++;
                else if (Avail.IsTrap(b.def) && ahead >= 0 && dist <= 14f) info.trapsOutside++;
                else if (b.def.IsWall && dist <= 3.5f && b.Stuff?.stuffProps?.categories != null && b.Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Woody)) info.woodWalls++;
                else if (b.def.thingClass != null && typeof(Building_Turret).IsAssignableFrom(b.def.thingClass) && !DefenseTemplateDef.IsMortar(b.def) && dist <= 16f) info.turretsNear++;
            }
        }

        private static bool Near(int cell, DefenseMath.MapGrid g)
        {
            int x = cell % g.w, z = cell / g.w, e = g.noBuildEdge;
            return x < e || z < e || x >= g.w - e || z >= g.h - e;
        }

        // ---------------- Words ----------------

        /// <summary>"5 cells", "1 cell".</summary>
        public static string Cells(int n) => n == 1 ? "CC_Def_Cell".Translate().ToString() : "CC_Def_Cells".Translate(n).ToString();

        /// <summary>"north-east": a compass point (0 north .. 7 north-west).</summary>
        public static string Direction(int compass) => ("CC_Def_Dir_" + ((compass % 8) + 8) % 8).Translate();

        /// <summary>The side a facing (0 north .. 3 west) looks to.</summary>
        public static string FacingWord(int facing) => Direction(DefenseMath.CompassOfFacing(facing));

        /// <summary>"between two rock faces", "between the rock and the water", "your gate", "the bridge"...</summary>
        public string GapWords(ChokeInfo info)
        {
            var c = info.c;
            if (c.bridge) return "CC_Def_Gap_Bridge".Translate();
            if (info.gated) return "CC_Def_Gap_Gate".Translate();
            byte a = c.endA, b = c.endB;
            bool Has(byte k) => a == k || b == k;
            if (a == DefenseMath.Rock && b == DefenseMath.Rock) return (Mountain(info) ? "CC_Def_Gap_Pass" : "CC_Def_Gap_RockRock").Translate();
            if (Has(DefenseMath.Rock) && Has(DefenseMath.Water)) return "CC_Def_Gap_RockWater".Translate();
            if (a == DefenseMath.Water && b == DefenseMath.Water) return "CC_Def_Gap_WaterWater".Translate();
            if (a == DefenseMath.Wall && b == DefenseMath.Wall) return "CC_Def_Gap_Walls".Translate();
            if (Has(DefenseMath.Wall) && Has(DefenseMath.Rock)) return "CC_Def_Gap_WallRock".Translate();
            if (Has(DefenseMath.Wall) && Has(DefenseMath.Water)) return "CC_Def_Gap_WallWater".Translate();
            if (Has(DefenseMath.Wall)) return "CC_Def_Gap_WallOther".Translate();
            if (Has(DefenseMath.Water)) return "CC_Def_Gap_WaterOther".Translate();
            if (Has(DefenseMath.Rock)) return "CC_Def_Gap_RockOther".Translate();
            return "CC_Def_Gap_Other".Translate();
        }

        /// <summary>Overhead mountain close by the gap: a pass through the mountains rather than between two hills.</summary>
        private bool Mountain(ChokeInfo info)
        {
            int thick = 0;
            foreach (var c in GenRadial.RadialCellsAround(info.cell, 6f, true))
            {
                if (!c.InBounds(map)) continue;
                var roof = c.GetRoof(map);
                if (roof != null && roof.isThickRoof) thick++;
            }
            return thick >= 6;
        }
    }
}
