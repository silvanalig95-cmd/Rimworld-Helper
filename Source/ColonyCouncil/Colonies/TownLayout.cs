using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony as it was left, on its town's record (<see cref="LedgerFormer.layout"/>; the player's decision 7A): the
    /// grids of <see cref="LayoutGrid"/> saved the way the game saves its own grids (DataExposeUtility.LookByteArray, two
    /// bytes a cell, deflated), the table of def names, the beds of those who stayed and who sleeps in each, what was let go
    /// past the cap, and its size in the save. Written once, at the hand-over; read when the town's map is made.
    /// </summary>
    public class LedgerLayout : IExposable
    {
        public LayoutGrid grid = new LayoutGrid();

        /// <summary>Those who stayed, each beside their bed's cell in <see cref="LayoutGrid.beds"/> (the same order).</summary>
        public List<Pawn> sleepers = new List<Pawn>();

        /// <summary>What was let go past the cap (LayoutMath.Fit), as its words: "roofs", "roofs,floors"... or empty.</summary>
        public string dropped;

        /// <summary>What it took in the save when it was kept, in bytes.</summary>
        public int size;

        /// <summary>When it was kept.</summary>
        public int kept = -1;

        public void ExposeData()
        {
            grid ??= new LayoutGrid();
            var g = grid;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // A bed's sleeper the game let go: the bed is kept, nobody is known to sleep there.
                for (int i = Math.Min(g.beds.Count, sleepers.Count) - 1; i >= 0; i--)
                    if (sleepers[i] == null || sleepers[i].Discarded)
                    {
                        sleepers.RemoveAt(i);
                        g.beds.RemoveAt(i);
                    }
            }
            Scribe_Values.Look(ref g.originX, "originX");
            Scribe_Values.Look(ref g.originZ, "originZ");
            Scribe_Values.Look(ref g.width, "width");
            Scribe_Values.Look(ref g.height, "height");
            Scribe_Values.Look(ref g.mapX, "mapX");
            Scribe_Values.Look(ref g.mapZ, "mapZ");
            Scribe_Values.Look(ref dropped, "dropped");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref kept, "kept", -1);
            Scribe_Collections.Look(ref g.table, "table", LookMode.Value);
            // The grids, as the game saves its own: bytes, deflated (a layer let go is not written).
            byte[] things = Scribe.mode == LoadSaveMode.Saving ? LayoutMath.Bytes(g.things) : null;
            byte[] under = Scribe.mode == LoadSaveMode.Saving ? LayoutMath.Bytes(g.under) : null;
            byte[] floors = Scribe.mode == LoadSaveMode.Saving ? LayoutMath.Bytes(g.floors) : null;
            byte[] bases = Scribe.mode == LoadSaveMode.Saving ? LayoutMath.Bytes(g.bases) : null;
            byte[] flags = Scribe.mode == LoadSaveMode.Saving ? g.flags : null;
            DataExposeUtility.LookByteArray(ref things, "things");
            DataExposeUtility.LookByteArray(ref under, "under");
            DataExposeUtility.LookByteArray(ref floors, "floors");
            DataExposeUtility.LookByteArray(ref bases, "bases");
            DataExposeUtility.LookByteArray(ref flags, "flags");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                int cells = g.Cells;
                g.things = LayoutMath.Shorts(things, cells);
                g.under = LayoutMath.Shorts(under, cells);
                g.floors = LayoutMath.Shorts(floors, cells);
                g.bases = LayoutMath.Shorts(bases, cells);
                g.flags = flags != null && flags.Length == cells ? flags : null;
            }
            Scribe_Collections.Look(ref g.beds, "beds", LookMode.Value);
            Scribe_Collections.Look(ref sleepers, "sleepers", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                g.table ??= new List<string>();
                g.beds ??= new List<int>();
                sleepers ??= new List<Pawn>();
                int n = Math.Min(g.beds.Count, sleepers.Count);
                if (g.beds.Count > n) g.beds.RemoveRange(n, g.beds.Count - n);
                if (sleepers.Count > n) sleepers.RemoveRange(n, sleepers.Count - n);
                for (int i = n - 1; i >= 0; i--)
                    if (sleepers[i] == null)
                    {
                        sleepers.RemoveAt(i);
                        g.beds.RemoveAt(i);
                    }
            }
        }
    }

    /// <summary>
    /// Keeping the colony's layout at the hand-over and placing it when the town's map is made (docs/design/
    /// stage3-many-colonies.md, section 2, "Keeping the layout"; release 2b, the player's decision 7A).
    /// <list type="bullet">
    /// <item><b>Kept</b> (<see cref="Capture"/>, inside the hand-over's long event, just before the people take the place):
    /// over the home area's bounds and a margin, every building of the colony's by its origin (an edifice, and anything
    /// under or on it: a conduit, a lamp), its def, stuff and rotation; the floors laid and the foundations; the roofs the
    /// colony built; and where natural rock is gone. Not items, plants, filth or art's tales. Past 64 KB in the save,
    /// roofs, then floors are let go.</item>
    /// <item><b>Placed</b> when the game makes the town's map (an attack, a fight the colony joins): the game's base
    /// generation stands down for that map only (Patch_TownLayoutSettlement, a guarded prefix on the game's settlement
    /// step, which also stands down the Vanilla Expanded Framework's own, a step of the same kind; Patch_TownLayoutPower,
    /// the game's power step for the same map), the rock gone is cleared, the floors, buildings and roofs are placed the
    /// way the game places its own structures (<see cref="Sketch"/>), a def the game no longer has left out; then its
    /// people: the notables at home, in their own beds where the layout knows them, and the game's own settlement group of
    /// that people under its defend-the-town lord. Taken back, the same layout on the colony's own map
    /// (<see cref="Place"/>, the colony's things). Without Rimfolk, or with no layout kept, the game makes its own base.</item>
    /// </list>
    /// </summary>
    public static class TownLayouts
    {
        /// <summary>Maps whose layout was placed while they were made (the power step stands down for them too). Kept for the session.</summary>
        private static readonly HashSet<int> placed = new HashSet<int>();
        private static Game placedGame;

        /// <summary>This map was made from a kept layout.</summary>
        public static bool Placed(Map map)
        {
            if (map == null) return false;
            if (Current.Game != placedGame) return false;
            return placed.Contains(map.uniqueID);
        }

        private static void NotePlaced(Map map)
        {
            if (Current.Game != placedGame)
            {
                placedGame = Current.Game;
                placed.Clear();
            }
            placed.Add(map.uniqueID);
        }

        // ================= Kept =================

        /// <summary>A building of the colony's worth keeping: the colony's own, built (not a frame or blueprint), and not the land's own rock.</summary>
        private static bool Kept(Building b)
        {
            var d = b?.def;
            if (d?.building == null || b.Destroyed || !b.Spawned || d.category != ThingCategory.Building) return false;
            if (b is Frame || d.IsFrame || d.IsBlueprint || d.building.isNaturalRock || d.mineable || !d.destroyable) return false;
            return b.Faction == Faction.OfPlayer;
        }

        /// <summary>
        /// The colony's layout as it is now (see the class), with the beds of <paramref name="stayers"/>; null when there is
        /// nothing to keep or it can't fit (then the game makes its ordinary base for the town).
        /// </summary>
        public static LedgerLayout Capture(Map map, IEnumerable<Pawn> stayers)
        {
            if (map == null) return null;
            try
            {
                int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
                void Take(IntVec3 c)
                {
                    if (c.x < minX) minX = c.x;
                    if (c.z < minZ) minZ = c.z;
                    if (c.x > maxX) maxX = c.x;
                    if (c.z > maxZ) maxZ = c.z;
                }
                var home = map.areaManager?.Home;
                if (home != null)
                    foreach (var c in home.ActiveCells)
                        Take(c);
                if (maxX < minX)
                    foreach (var b in map.listerBuildings.allBuildingsColonist)
                        if (Kept(b))
                            foreach (var c in b.OccupiedRect())
                                Take(c);
                LayoutMath.Rect(minX, minZ, maxX, maxZ, LayoutMath.Margin, map.Size.x, map.Size.z, out int x, out int z, out int w, out int h);
                if (w <= 0 || h <= 0) return null;
                var g = LayoutGrid.Make(x, z, w, h, map.Size.x, map.Size.z);
                var index = new Dictionary<string, int>();
                bool In(IntVec3 c) => c.x >= x && c.z >= z && c.x < x + w && c.z < z + h;
                // The buildings, each at its origin.
                foreach (var b in map.listerBuildings.allBuildingsColonist.ToList())
                {
                    if (!Kept(b) || !In(b.Position)) continue;
                    int no = LayoutMath.Add(g, LayoutMath.BuildingEntry(b.def.defName, b.Stuff?.defName), index);
                    if (no == 0) continue;
                    int i = g.Index(b.Position.x - x, b.Position.z - z);
                    ushort v = LayoutMath.Pack(no, b.Rotation.AsInt);
                    if (b.def.building.isEdifice)
                    {
                        if (g.things[i] == 0) g.things[i] = v;
                    }
                    else if (g.under[i] == 0) g.under[i] = v;
                }
                // The floors and foundations, the roofs the colony built, and where the land's rock is gone.
                var tg = map.terrainGrid;
                var rg = map.roofGrid;
                for (int lz = 0; lz < h; lz++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        var c = new IntVec3(x + lx, 0, z + lz);
                        int i = g.Index(lx, lz);
                        var top = tg.TopTerrainAt(c);
                        if (top != null && (top.layerable || top.IsFloor && !top.natural)) g.floors[i] = (ushort)LayoutMath.Add(g, LayoutMath.TerrainEntry(top.defName), index);
                        var found = tg.FoundationAt(c);
                        if (found != null) g.bases[i] = (ushort)LayoutMath.Add(g, LayoutMath.TerrainEntry(found.defName), index);
                        var roof = rg.RoofAt(c);
                        byte flag = 0;
                        if (roof != null && !roof.isNatural) flag |= LayoutMath.Roof;
                        var ed = c.GetEdifice(map);
                        if (ed == null || !Rock(ed.def)) flag |= LayoutMath.Clear;
                        g.flags[i] = flag;
                    }
                // Who sleeps where, of those who stay.
                var sleepers = new List<Pawn>();
                foreach (var p in stayers ?? Enumerable.Empty<Pawn>())
                {
                    var bed = p?.ownership?.OwnedBed;
                    if (bed == null || bed.Map != map || !In(bed.Position)) continue;
                    g.beds.Add(g.Index(bed.Position.x - x, bed.Position.z - z));
                    sleepers.Add(p);
                }
                if (LayoutMath.Empty(g)) return null;
                var dropped = LayoutMath.Fit(g, CompressUtility.Compress);
                if (dropped.Contains(LayoutMath.DropAll)) return null;
                if (g.beds.Count != sleepers.Count) sleepers.Clear();
                var layout = new LedgerLayout
                {
                    grid = g, sleepers = g.beds.Count == sleepers.Count ? sleepers : new List<Pawn>(), dropped = string.Join(",", dropped),
                    size = LayoutMath.SavedSize(g, CompressUtility.Compress), kept = Find.TickManager?.TicksGame ?? 0
                };
                if (layout.sleepers.Count == 0) g.beds.Clear();
                if (Prefs.DevMode)
                    Log.Message("[Rimfolk] Kept the layout of " + Homes.Name(map) + ": " + w + "x" + h + " cells, " + g.table.Count + " defs, about " + (layout.size / 1024f).ToString("0.0")
                                + " KB" + (dropped.Count > 0 ? " (let go: " + layout.dropped + ")" : "") + ".");
                return layout;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: keeping the colony's layout", e);
                return null;
            }
        }

        /// <summary>The land's own rock (and its ores): what mining takes away.</summary>
        private static bool Rock(ThingDef d) => d?.building != null && (d.building.isNaturalRock || d.mineable);

        // ================= Placed =================

        /// <summary>
        /// The town's map is being made (the game's settlement step, Patch_TownLayoutSettlement): when it is a town the
        /// colony was, still its people's, with a layout kept, the layout is placed for its people and its people are at
        /// home in it; the game's own base stands down. False: not such a map (the game builds as always).
        /// </summary>
        public static bool PlaceTown(Map map)
        {
            if (!(map?.Parent is Settlement s) || s.Faction == null || s.Faction.IsPlayer) return false;
            var rec = WorldLedger.FormerOf(s.ID);
            if (rec?.layout == null || rec.people == null || rec.people != s.Faction || rec.end != TownEnd.None) return false;
            var rect = Place(map, rec.layout, s.Faction, false);
            if (rect == null) return false;
            NotePlaced(map);
            try
            {
                MapGenerator.SetVar("SettlementRect", rect.Value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the town's ground", e);
            }
            try
            {
                AtHome(map, rec, rect.Value, s.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the town's people at home", e);
            }
            return true;
        }

        /// <summary>Where the layout's corner goes on this map (where it was, or moved in to fit a map of another size).</summary>
        public static IntVec3 Corner(Map map, LayoutGrid g) => new IntVec3(LayoutMath.Shift(g.originX, g.width, map.Size.x), 0, LayoutMath.Shift(g.originZ, g.height, map.Size.z));

        /// <summary>A cell of the layout on this map, by its local index.</summary>
        public static IntVec3 CellOf(Map map, LayoutGrid g, int local)
        {
            var o = Corner(map, g);
            return new IntVec3(o.x + local % g.width, 0, o.z + local / g.width);
        }

        /// <summary>
        /// Places the layout on the map for <paramref name="faction"/>: the rock gone cleared, the foundations, the floors,
        /// the buildings (edifices, then what lies under or on them) and the roofs, through the game's own sketches; a def
        /// the game no longer has, or a stuff it can't be made of, is left out. <paramref name="unfog"/>: the colony's own
        /// map, which it knows. Returns the rectangle placed, or null when there was nothing to place.
        /// </summary>
        public static CellRect? Place(Map map, LedgerLayout layout, Faction faction, bool unfog)
        {
            var g = layout?.grid;
            if (map == null || g == null || g.Cells == 0 || g.things == null && g.under == null) return null;
            var o = Corner(map, g);
            int w = Math.Min(g.width, map.Size.x - o.x), h = Math.Min(g.height, map.Size.z - o.z);
            if (w <= 0 || h <= 0) return null;
            var rect = new CellRect(o.x, o.z, w, h);
            // The table read once: what the game still has.
            int n = g.table.Count;
            var things = new ThingDef[n + 1];
            var stuffs = new ThingDef[n + 1];
            var terrains = new TerrainDef[n + 1];
            for (int i = 1; i <= n; i++)
            {
                string e = LayoutMath.Entry(g, i);
                if (LayoutMath.ParseBuilding(e, out string def, out string stuff))
                {
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(def);
                    if (d?.building == null || d.category != ThingCategory.Building) continue;
                    things[i] = d;
                    var sd = stuff != null ? DefDatabase<ThingDef>.GetNamedSilentFail(stuff) : null;
                    stuffs[i] = d.MadeFromStuff ? sd != null && sd.stuffProps != null && d.stuffCategories != null && sd.stuffProps.CanMake(d) ? sd : GenStuff.DefaultStuffFor(d) : null;
                }
                else if (LayoutMath.ParseTerrain(e, out string tdef)) terrains[i] = DefDatabase<TerrainDef>.GetNamedSilentFail(tdef);
            }
            IntVec3 Cell(int lx, int lz) => new IntVec3(o.x + lx, 0, o.z + lz);
            bool Inside(int lx, int lz) => lx < w && lz < h;
            if (unfog)
                foreach (var c in rect)
                    map.fogGrid.Unfog(c);
            // The rock that is gone (mined out, or never there): cleared, its hewn ground left as mining leaves it.
            if (g.flags != null)
                for (int lz = 0; lz < h; lz++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        int i = g.Index(lx, lz);
                        if ((g.flags[i] & LayoutMath.Clear) == 0) continue;
                        var c = Cell(lx, lz);
                        var ed = c.GetEdifice(map);
                        if (ed == null || !Rock(ed.def)) continue;
                        var leave = ed.def.building.leaveTerrain;
                        ed.Destroy(DestroyMode.Vanish);
                        if (leave != null && (g.floors == null || g.floors[i] == 0)) map.terrainGrid.SetTerrain(c, leave);
                    }
            // The foundations (a bridge, a substructure), then the floors, as the game places its own.
            var ground = new Sketch();
            for (int lz = 0; lz < h; lz++)
                for (int lx = 0; lx < w; lx++)
                {
                    int i = g.Index(lx, lz);
                    var c = Cell(lx, lz);
                    var b = g.bases != null && g.bases[i] > 0 && g.bases[i] <= n ? terrains[g.bases[i]] : null;
                    if (b != null && b.isFoundation && map.terrainGrid.FoundationAt(c) == null) map.terrainGrid.SetFoundation(c, b);
                    var f = g.floors != null && g.floors[i] > 0 && g.floors[i] <= n ? terrains[g.floors[i]] : null;
                    if (f != null && map.terrainGrid.TerrainAt(c) != f) ground.AddTerrain(f, c, false);
                }
            if (!ground.Empty) ground.Spawn(map, IntVec3.Zero, faction, Sketch.SpawnPosType.Unchanged, Sketch.SpawnMode.Normal, true, false);
            // The buildings: the edifices, then what lies under or on them (conduits, lamps), each at its origin.
            foreach (var layer in new[] { g.things, g.under })
            {
                if (layer == null) continue;
                var sk = new Sketch();
                for (int lz = 0; lz < h; lz++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        if (!Inside(lx, lz)) continue;
                        ushort v = layer[g.Index(lx, lz)];
                        int no = LayoutMath.NumberOf(v);
                        if (v == 0 || no < 1 || no > n || things[no] == null) continue;
                        var d = things[no];
                        QualityCategory? q = d.HasComp(typeof(CompQuality)) ? QualityCategory.Normal : (QualityCategory?)null;
                        sk.AddThing(d, Cell(lx, lz), new Rot4(LayoutMath.RotOf(v)), stuffs[no], 1, q, null, false);
                    }
                if (!sk.Empty) sk.Spawn(map, IntVec3.Zero, faction, Sketch.SpawnPosType.Unchanged, Sketch.SpawnMode.Normal, true, true);
            }
            // The roofs the colony built.
            if (g.flags != null)
                for (int lz = 0; lz < h; lz++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        if ((g.flags[g.Index(lx, lz)] & LayoutMath.Roof) == 0) continue;
                        var c = Cell(lx, lz);
                        if (!c.Roofed(map)) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    }
            // The rock taken away had the game mark the roofs it held to fall; the colony's own walls hold them now, as
            // they did when it was left, so only a roof nothing holds still falls.
            try
            {
                map.roofCollapseBuffer.CellsMarkedToCollapse.RemoveAll(c => rect.Contains(c) && RoofCollapseUtility.WithinRangeOfRoofHolder(c, map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the roofs the colony's walls hold", e);
            }
            return rect;
        }

        // ================= Its people at home =================

        /// <summary>While a raid on the town makes its map (Colonies/TownRaid.cs): the points its defenders stand with; else 0 (the game's own).</summary>
        public static float DefendPoints;

        /// <summary>A pawn of the world, at home (free, or their people's leader at their seat; not in a caravan, not held), alive and of this people.</summary>
        public static bool FreeAtHome(Pawn p, Faction f)
        {
            if (p == null || p.Dead || p.Discarded || p.Spawned || p.Faction != f || !Find.WorldPawns.Contains(p)) return false;
            var at = Find.WorldPawns.GetSituation(p);
            return at == WorldPawnSituation.Free || at == WorldPawnSituation.FactionLeader;
        }

        /// <summary>Where someone of the town is at home: their own bed when the layout knows it, else a roofed cell inside, else any open cell of the town.</summary>
        public static IntVec3 HomeCell(Map map, LedgerLayout layout, CellRect rect, Pawn p)
        {
            var g = layout?.grid;
            if (g != null && p != null)
            {
                int i = layout.sleepers.IndexOf(p);
                if (i >= 0 && i < g.beds.Count)
                {
                    var c = CellOf(map, g, g.beds[i]);
                    if (c.InBounds(map) && c.Standable(map)) return c;
                }
            }
            if (CellFinder.TryFindRandomCellInsideWith(rect, c => c.Standable(map) && c.Roofed(map) && !c.Fogged(map), out var inside)) return inside;
            if (CellFinder.TryFindRandomCellInsideWith(rect, c => c.Standable(map), out var any)) return any;
            return rect.CenterCell;
        }

        /// <summary>
        /// The town's people on its map as it is made: the notables at home (their own beds where kept), and the game's
        /// own settlement group of that people (the points the game gives a town, less what the notables already bring),
        /// all under the game's defend-the-town lord at the town's middle.
        /// </summary>
        private static void AtHome(Map map, LedgerFormer rec, CellRect rect, Faction f)
        {
            var lord = LordMaker.MakeNewLord(f, new LordJob_DefendBase(f, rect.CenterCell, 25000), map);
            // A raid on the town (release 5, Colonies/TownRaid.cs): its defenders as the raid reckoned them, by its fortune.
            float points = DefendPoints > 0f ? DefendPoints : SymbolResolver_Settlement.DefaultPawnsPoints.RandomInRange;
            var book = NotableBook.Instance;
            if (book != null)
                foreach (var nb in book.notables.Where(x => x != null && x.town == rec.key && x.Alive).ToList())
                {
                    var p = nb.pawn;
                    if (!FreeAtHome(p, f)) continue;
                    var cell = HomeCell(map, rec.layout, rect, p);
                    GenSpawn.Spawn(p, cell, map);
                    lord.AddPawn(p);
                    points -= p.kindDef?.combatPower ?? 0f;
                }
            var parms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Settlement, tile = map.Tile, faction = f, points = Math.Max(250f, points), inhabitants = true
            };
            foreach (var p in PawnGroupMakerUtility.GeneratePawns(parms, false).ToList())
            {
                var cell = HomeCell(map, null, rect, p);
                GenSpawn.Spawn(p, cell, map);
                lord.AddPawn(p);
            }
        }
    }
}
