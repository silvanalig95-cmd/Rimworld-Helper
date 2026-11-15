using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a cell of a camp's site holds, as the campers see it (read off the map by CampSite).</summary>
    public struct CampCell
    {
        /// <summary>Off the map, or still fogged.</summary>
        public bool outside;
        /// <summary>Water, or ground nothing can be built on (impassable, or too soft for the lightest building).</summary>
        public bool badGround;
        /// <summary>
        /// The colony's: its home area, a zone, a plan or a mark on it, or a building, a plant or an item of the colony's
        /// (one it stored or forbade).
        /// </summary>
        public bool colony;
        /// <summary>A building of anyone else (a ruin, another people's, what an earlier camp left standing): left as it is.</summary>
        public bool building;
        /// <summary>A natural rock whose yield is plain stone or nothing (CampTakeMath.PlainRock).</summary>
        public bool rock;
        /// <summary>A mineable worth more than plain stone: an ore, jade, components, the mods' ores.</summary>
        public bool ore;
        /// <summary>Under a natural roof: overhead rock nobody would build or dig under.</summary>
        public bool roofed;
        /// <summary>A wild tree on it (nobody sowed it, nobody marked it).</summary>
        public bool tree;
        /// <summary>A wild plant in the way of building (a bush, tall grass; not a tree).</summary>
        public bool plant;
        /// <summary>A plant someone sowed, or one marked for cutting or harvest.</summary>
        public bool sown;
        /// <summary>A rock chunk, or something else worth no more than plain stone that nobody owns: carried aside.</summary>
        public bool chunk;
        /// <summary>An item of nobody's worth more than plain stone: never moved.</summary>
        public bool valuable;
    }

    /// <summary>What becomes of a cell a camp means to build on: free now, cleared first (as a player's builders would), or never.</summary>
    public enum CellFate
    {
        Free,
        /// <summary>A bush or tall grass is cut.</summary>
        Cut,
        /// <summary>A tree is felled (its logs are the camp's).</summary>
        Fell,
        /// <summary>A chunk is carried aside.</summary>
        Haul,
        /// <summary>Plain rock is mined (its chunk is the camp's).</summary>
        Mine,
        /// <summary>Never touched: the colony's, an ore, water, someone else's building, under the mountain.</summary>
        Never
    }

    /// <summary>How a camp's people sleep (CampKindDef.sleep).</summary>
    public enum CampSleepForm
    {
        /// <summary>Round the fire, a bed each (a war party, mourners, pilgrims).</summary>
        Ring,
        /// <summary>One shelter for all of them: walls, a door and a roof (settlers, a hermit, a wardens' post).</summary>
        Hut,
        /// <summary>Tents: a tent each where a mod has one that is a bed, else small rooms of tent walls for two (traders, road folk).</summary>
        Tents
    }

    /// <summary>One building of a camp's layout, in the camp's own frame: the fire at (0,0), +z away from the colony, +x to its side.</summary>
    public sealed class CampPiece
    {
        public string role;
        public int minX, minZ, maxX, maxZ;
        /// <summary>Which way it faces: 0 north (+z), 1 east, 2 south, 3 west (a bed's head, a seat toward its table).</summary>
        public int facing;
        /// <summary>Which room it belongs to (a hut or a tent), 0 for none.</summary>
        public int room;

        public int Width => maxX - minX + 1;
        public int Depth => maxZ - minZ + 1;

        public IEnumerable<(int x, int z)> Cells()
        {
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    yield return (x, z);
        }

        public bool Holds(int x, int z) => x >= minX && x <= maxX && z >= minZ && z <= maxZ;

        public override string ToString() => $"{role} ({minX},{minZ})-({maxX},{maxZ}) f{facing}" + (room > 0 ? " r" + room : "");
    }

    /// <summary>What a camp means to build (built from its kind, its people, its stay and the weather: CampSite.SpecFor).</summary>
    public sealed class CampLayoutSpec
    {
        public int sleepers = 3;
        public CampSleepForm sleep = CampSleepForm.Ring;
        /// <summary>The tents are beds of their own (a mod's tent), set out in rows: no walls.</summary>
        public bool tentBeds;
        /// <summary>The size of one bed (a bedroll 1x2), as it stands facing north.</summary>
        public int bedW = 1, bedD = 2;
        /// <summary>A short wall behind the beds round the fire, for cold nights.</summary>
        public bool windbreak;
        public int tables;
        public int tableW = 1, tableD = 2;
        /// <summary>A seat at each long side of each table cell (stools, cushions); none, and they sit on the ground at it.</summary>
        public bool seats = true;
        public bool butcher, craft, picket, stash, stall, spot;
        public int lights;

        /// <summary>How many sit down to eat: everyone who can, a seat each.</summary>
        public int SeatsNeeded => sleepers;
    }

    /// <summary>The layout a camp settles on: its pieces, where its goods pile (the stash) and what it had to leave out.</summary>
    public sealed class CampLayout
    {
        public readonly List<CampPiece> pieces = new List<CampPiece>();
        public (int x, int z) stash;
        /// <summary>The roles it wanted and found no room for (on a site with nothing in the way, none).</summary>
        public readonly List<string> missing = new List<string>();
        /// <summary>What clearing it takes: the sum of the cells' costs (CampBuildMath.Cost).</summary>
        public int cost;
        /// <summary>Everything wanted is in it.</summary>
        public bool Complete => missing.Count == 0;
        /// <summary>The fire, the beds and the shelter are in it (what a camp can't do without).</summary>
        public bool Livable;

        public int Count(string role) => pieces.Count(p => p.role == role);
    }

    /// <summary>What a camp is and has, for what it adds over its stay (read off the game by Camps/CampGrow.cs).</summary>
    public struct GrowFacts
    {
        public int people;
        /// <summary>How long they mean to stay, in days.</summary>
        public float days;
        /// <summary>Their people's age: tribal, medieval, industrial or spacer.</summary>
        public string age;
        /// <summary>The first of their kind's callings (CampKindDef.works): Mine, Craft, Chop, Hunt...; null for none.</summary>
        public string calling;
        /// <summary>They hunt (a beast every day or two, or a hunting calling).</summary>
        public bool hunts;
        /// <summary>They brought pack beasts or a herd.</summary>
        public bool beasts;
        /// <summary>A people of a faith (with Ideology).</summary>
        public bool faith;
        /// <summary>Their kind has a spot of its own already (a shrine's, a stage, a forge).</summary>
        public bool ownSpot;
        /// <summary>That spot of theirs is a work table (the smith's forge).</summary>
        public bool workTable;
        /// <summary>A shelf stands for their goods.</summary>
        public bool stash;
        /// <summary>They sit down to eat at tables.</summary>
        public bool dines;
        /// <summary>The Empire's people (their own banner).</summary>
        public bool empire;
        /// <summary>How many of their goods lie about with no store that takes them or has room, and whether any of it is food.</summary>
        public int loose;
        public bool looseFood;
        /// <summary>The role has a piece in this game that their people know how to build (the kit's, their age, the player's mods).</summary>
        public Func<string, bool> has;
    }

    /// <summary>
    /// The camp as it stands, for placing what it adds (read off the map by Camps/CampGrow.cs), in its own frame: the fire
    /// at (0,0), +z away from the colony.
    /// </summary>
    public sealed class GrowSite
    {
        /// <summary>Cells something of theirs stands on.</summary>
        public readonly HashSet<(int x, int z)> taken = new HashSet<(int, int)>();
        /// <summary>Cells kept free: the fire's ring, door steps, work spots' places to stand, the stash's ground, a hut's inside.</summary>
        public readonly HashSet<(int x, int z)> keep = new HashSet<(int, int)>();
        /// <summary>What a lean-to roofs over: the stash's ground and its shelf.</summary>
        public readonly List<(int x, int z)> cover = new List<(int, int)>();
        public (int x, int z)? stash, picket, butcher;
        /// <summary>Their beds' cells (a latrine keeps away from them).</summary>
        public readonly List<(int x, int z)> beds = new List<(int, int)>();
        /// <summary>What a cell costs to clear (CampBuildMath.Cost of its fate), -1 for never: the colony's, water, ore, someone else's.</summary>
        public Func<int, int, int> cost;
    }

    /// <summary>
    /// How a camp is laid out and what its builders clear first (Camps/CampSite.cs): the fate of each cell, a bed for every
    /// sleeper, a shelter that is a real room (walls all round, one door, room for the roof), tables with a seat for everyone,
    /// the work spots, lights, the picket and the stash, each placed where it fits. Pure (no game types), so Tools/Tests can
    /// check it.
    /// </summary>
    public static class CampBuildMath
    {
        // ---------------- Cells ----------------

        /// <summary>
        /// What the campers do with a cell before they build on it, as a player's builders would: a tree felled, a bush cut, a
        /// chunk carried aside, plain rock mined. Never for the colony's, an ore, water and soft ground, someone else's
        /// building, a sown plant, anything of value, or under the mountain.
        /// </summary>
        public static CellFate Fate(CampCell c)
        {
            if (c.outside || c.badGround || c.colony || c.building || c.ore || c.roofed || c.sown || c.valuable) return CellFate.Never;
            if (c.rock) return CellFate.Mine;
            if (c.tree) return CellFate.Fell;
            if (c.chunk) return CellFate.Haul;
            if (c.plant) return CellFate.Cut;
            return CellFate.Free;
        }

        /// <summary>What a fate costs the layout (the site with least to clear is the one picked); -1 for never.</summary>
        public static int Cost(CellFate f)
        {
            switch (f)
            {
                case CellFate.Free: return 0;
                case CellFate.Cut: return 1;
                case CellFate.Haul: return 2;
                case CellFate.Fell: return 3;
                case CellFate.Mine: return 8;
                default: return -1;
            }
        }

        // ---------------- Rooms ----------------

        /// <summary>The most beds in one row of a shelter.</summary>
        public const int RowMax = 7;

        /// <summary>
        /// A shelter for <paramref name="n"/> sleepers: its inside's width and depth, and whether the beds stand in two rows
        /// (heads to the back and front walls, the aisle between) or one (heads to the back wall, the aisle by the door).
        /// A hut is at least three wide; a tent as wide as its sleepers.
        /// </summary>
        public static (int width, int depth, bool twoRows) RoomSize(int n, bool tent, int bedW = 1, int bedD = 2)
        {
            n = Math.Max(1, n);
            bedW = Math.Max(1, bedW);
            bedD = Math.Max(1, bedD);
            int oneRow = Math.Max(1, RowMax / bedW);
            if (n <= Math.Min(oneRow, 6))
                return (Math.Max(tent ? 1 : 3, n * bedW), bedD + 1, false);
            // Two rows: the front row leaves the doorway's column free.
            int perRow = (n + 2) / 2;
            return (Math.Max(3, perRow * bedW), bedD * 2 + 1, true);
        }

        /// <summary>
        /// A room in its own frame: the front wall along z = 0 with the door in it at <paramref name="doorX"/>, the inside from
        /// (1,1) to (width, depth), the back wall at z = depth + 1. Walls ("hut"), the door, and the beds ("hutbed") for
        /// <paramref name="n"/>, heads to the wall.
        /// </summary>
        public static List<CampPiece> Room(int n, bool tent, int bedW, int bedD, int room)
        {
            var (w, d, two) = RoomSize(n, tent, bedW, bedD);
            int doorX = 1 + (w - 1) / 2;
            var list = new List<CampPiece>();
            for (int x = 0; x <= w + 1; x++)
                for (int z = 0; z <= d + 1; z++)
                {
                    bool edge = x == 0 || x == w + 1 || z == 0 || z == d + 1;
                    if (!edge) continue;
                    bool door = z == 0 && x == doorX;
                    list.Add(new CampPiece { role = door ? "door" : "hut", minX = x, minZ = z, maxX = x, maxZ = z, room = room });
                }
            int placed = 0;
            // The back row: heads to the back wall.
            for (int x = 1; x + bedW - 1 <= w && placed < n; x += bedW)
            {
                list.Add(new CampPiece { role = "hutbed", minX = x, maxX = x + bedW - 1, minZ = d - bedD + 1, maxZ = d, facing = 0, room = room });
                placed++;
            }
            if (two)
                // The front row: heads to the front wall, the doorway's column left for the way in.
                for (int x = 1; x + bedW - 1 <= w && placed < n; x += bedW)
                {
                    if (doorX >= x && doorX <= x + bedW - 1) continue;
                    list.Add(new CampPiece { role = "hutbed", minX = x, maxX = x + bedW - 1, minZ = 1, maxZ = bedD, facing = 2, room = room });
                    placed++;
                }
            return list;
        }

        /// <summary>
        /// A row of <paramref name="tents"/> tents of tent walls side by side, each with its own door, sharing their side walls:
        /// <paramref name="n"/> sleepers, two to a tent (the last may hold one).
        /// </summary>
        public static List<CampPiece> TentRow(int n, int tents, int bedW, int bedD, int firstRoom)
        {
            var list = new List<CampPiece>();
            int x0 = 0, left = n;
            for (int t = 0; t < tents && left > 0; t++)
            {
                int here = Math.Min(2, left);
                // The last tent with one sleeper is as wide as two all the same, for the row's sake.
                var room = Room(2, true, bedW, bedD, firstRoom + t);
                int beds = 0;
                foreach (var p in room)
                {
                    if (p.role == "hutbed" && ++beds > here) continue;
                    // Shared side walls: the next tent's left wall is this one's right.
                    if (t > 0 && p.minX == 0) continue;
                    p.minX += x0;
                    p.maxX += x0;
                    list.Add(p);
                }
                x0 += RoomSize(2, true, bedW, bedD).width + 1;
                left -= here;
            }
            return list;
        }

        /// <summary>
        /// Every room of the layout is a room the game counts as one: its walls and door close it all round (each cell of its
        /// inside is cut off from the outside but through the door), it has exactly one door, and every bed of it is inside.
        /// </summary>
        public static bool Enclosed(List<CampPiece> pieces, int room, out string why)
        {
            why = null;
            var walls = pieces.Where(p => p.room == room && (p.role == "hut" || p.role == "door")).ToList();
            var doors = walls.Where(p => p.role == "door").ToList();
            if (walls.Count == 0)
            {
                why = "no walls";
                return false;
            }
            if (doors.Count != 1)
            {
                why = doors.Count + " doors";
                return false;
            }
            var solid = new HashSet<(int, int)>();
            foreach (var p in pieces.Where(p => p.role == "hut" || p.role == "door"))
                foreach (var c in p.Cells()) solid.Add(c);
            var beds = pieces.Where(p => p.room == room && p.role == "hutbed").ToList();
            if (beds.Count == 0)
            {
                why = "no beds";
                return false;
            }
            // Flood from the first bed without crossing walls (its own, or a neighbour's it shares): it must never leave the
            // bounds of all the walls.
            var all = pieces.Where(p => p.role == "hut" || p.role == "door").ToList();
            int minX = all.Min(p => p.minX), maxX = all.Max(p => p.maxX), minZ = all.Min(p => p.minZ), maxZ = all.Max(p => p.maxZ);
            var start = (beds[0].minX, beds[0].minZ);
            var seen = new HashSet<(int, int)> { start };
            var todo = new Queue<(int, int)>();
            todo.Enqueue(start);
            while (todo.Count > 0)
            {
                var (x, z) = todo.Dequeue();
                if (x <= minX || x >= maxX || z <= minZ || z >= maxZ)
                {
                    why = $"open at ({x},{z})";
                    return false;
                }
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = (x + dx, z + dz);
                    if (solid.Contains(n) || !seen.Add(n)) continue;
                    todo.Enqueue(n);
                }
            }
            foreach (var b in beds)
                foreach (var c in b.Cells())
                    if (!seen.Contains(c))
                    {
                        why = "a bed outside";
                        return false;
                    }
            // The door opens onto the inside on one side and the outside on the other.
            var door = doors[0];
            bool inside = false, outside = false;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (door.minX + dx, door.minZ + dz);
                if (solid.Contains(n)) continue;
                if (seen.Contains(n)) inside = true;
                else outside = true;
            }
            if (!inside || !outside)
            {
                why = "the door leads nowhere";
                return false;
            }
            return true;
        }

        /// <summary>The cells under a room's roof: its inside (everything its walls close in, a wall it shares with the next tent too).</summary>
        public static List<(int x, int z)> Inside(List<CampPiece> pieces, int room)
        {
            var all = pieces.Where(p => p.role == "hut" || p.role == "door").ToList();
            var beds = pieces.Where(p => p.room == room && p.role == "hutbed").ToList();
            if (all.Count == 0 || beds.Count == 0) return new List<(int, int)>();
            var solid = new HashSet<(int, int)>(all.SelectMany(p => p.Cells()));
            return Flood(solid, beds.SelectMany(b => b.Cells()), all.Min(p => p.minX), all.Max(p => p.maxX), all.Min(p => p.minZ), all.Max(p => p.maxZ)) ?? new List<(int, int)>();
        }

        /// <summary>
        /// The cells reached from <paramref name="start"/> without crossing <paramref name="solid"/>; null when that leaves
        /// the bounds (the room isn't closed).
        /// </summary>
        public static List<(int x, int z)> Flood(HashSet<(int, int)> solid, IEnumerable<(int x, int z)> start, int minX, int maxX, int minZ, int maxZ)
        {
            var seen = new HashSet<(int, int)>();
            var todo = new Queue<(int, int)>();
            foreach (var s in start)
                if (!solid.Contains(s) && seen.Add(s)) todo.Enqueue(s);
            while (todo.Count > 0)
            {
                var (x, z) = todo.Dequeue();
                if (x <= minX || x >= maxX || z <= minZ || z >= maxZ) return null;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = (x + dx, z + dz);
                    if (solid.Contains(n) || !seen.Add(n)) continue;
                    todo.Enqueue(n);
                }
            }
            return seen.ToList();
        }

        // ---------------- The whole camp ----------------

        /// <summary>How many of the first fitting places are weighed for each piece (the one with least to clear wins).</summary>
        public const int Weigh = 5;

        /// <summary>The camp's reach from its fire: every piece within this (CampSite.CoreRadius).</summary>
        public const int Reach = 10;

        private sealed class Grid
        {
            /// <summary>1: a piece stands there; 2: kept free (a way round the fire, a door's step, a work spot's place to stand).</summary>
            public readonly Dictionary<(int, int), int> cells = new Dictionary<(int, int), int>();
            public readonly Func<int, int, int> cost;

            public Grid(Func<int, int, int> cost)
            {
                this.cost = cost ?? ((x, z) => 0);
            }

            /// <summary>The cost of standing these pieces here (and keeping these cells free), or -1 when they can't.</summary>
            public int Try(List<CampPiece> solid, List<(int x, int z)> keep, bool keepMustWalk)
            {
                int total = 0;
                foreach (var p in solid)
                    foreach (var c in p.Cells())
                    {
                        if (Math.Abs(c.x) > Reach || Math.Abs(c.z) > Reach) return -1;
                        if (cells.ContainsKey(c)) return -1;
                        int k = cost(c.x, c.z);
                        if (k < 0) return -1;
                        total += k;
                    }
                foreach (var c in keep)
                {
                    if (cells.TryGetValue(c, out int v) && v == 1) return -1;
                    if (solid.Any(p => p.Holds(c.x, c.z))) return -1;
                    if (keepMustWalk && cost(c.x, c.z) < 0) return -1;
                }
                return total;
            }

            public void Put(List<CampPiece> solid, List<(int x, int z)> keep)
            {
                foreach (var p in solid)
                    foreach (var c in p.Cells()) cells[c] = 1;
                foreach (var c in keep)
                    if (!cells.ContainsKey(c)) cells[c] = 2;
            }
        }

        private static List<(int, int)> Ring((int x, int z) at, int r = 1)
        {
            var list = new List<(int, int)>();
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (dx != 0 || dz != 0) list.Add((at.x + dx, at.z + dz));
            return list;
        }

        private static List<(int, int)> Around(CampPiece p)
        {
            var list = new List<(int, int)>();
            for (int x = p.minX - 1; x <= p.maxX + 1; x++)
                for (int z = p.minZ - 1; z <= p.maxZ + 1; z++)
                    if (!p.Holds(x, z)) list.Add((x, z));
            return list;
        }

        /// <summary>A piece of <paramref name="w"/> by <paramref name="d"/> (as it stands facing north) with its corner at (x, z), facing <paramref name="facing"/>.</summary>
        public static CampPiece At(string role, int x, int z, int w, int d, int facing)
        {
            bool turned = facing == 1 || facing == 3;
            int ww = turned ? d : w, dd = turned ? w : d;
            return new CampPiece { role = role, minX = x, minZ = z, maxX = x + ww - 1, maxZ = z + dd - 1, facing = facing };
        }

        /// <summary>Turns a block of pieces about (0,0) by quarter turns clockwise (k = 1: its +z becomes +x) and moves it by (ox, oz).</summary>
        public static List<CampPiece> Turned(List<CampPiece> block, int k, int ox, int oz)
        {
            k = ((k % 4) + 4) % 4;
            var list = new List<CampPiece>();
            foreach (var p in block)
            {
                var (ax, az) = Turn(p.minX, p.minZ, k);
                var (bx, bz) = Turn(p.maxX, p.maxZ, k);
                list.Add(new CampPiece
                {
                    role = p.role, room = p.room, facing = (p.facing + k) % 4,
                    minX = Math.Min(ax, bx) + ox, maxX = Math.Max(ax, bx) + ox, minZ = Math.Min(az, bz) + oz, maxZ = Math.Max(az, bz) + oz
                });
            }
            return list;
        }

        /// <summary>A cell turned about (0,0) by quarter turns clockwise: k = 1 sends +z to +x.</summary>
        public static (int x, int z) Turn(int x, int z, int k)
        {
            switch (((k % 4) + 4) % 4)
            {
                case 1: return (z, -x);
                case 2: return (-x, -z);
                case 3: return (-z, x);
                default: return (x, z);
            }
        }

        /// <summary>
        /// A shelter block (rooms or a tent row, its front wall along z = 0, the door side toward the fire) placed with its
        /// front three cells out from the fire: behind it first, then its sides, then before it, a little to either side.
        /// </summary>
        private static IEnumerable<List<CampPiece>> ShelterPlaces(List<CampPiece> block)
        {
            int w = block.Max(p => p.maxX) + 1;
            foreach (int side in new[] { 0, 1, 3, 2 })
                for (int gap = 3; gap <= 6; gap++)
                    foreach (int shift in new[] { 0, -2, 2, -4, 4, -6, 6 })
                    {
                        // In the block's own frame the fire is at (w/2 + shift, -gap): turn the block so its front faces it.
                        var moved = block.Select(p => new CampPiece
                        {
                            role = p.role, room = p.room, facing = p.facing,
                            minX = p.minX - w / 2 + shift, maxX = p.maxX - w / 2 + shift, minZ = p.minZ + gap, maxZ = p.maxZ + gap
                        }).ToList();
                        yield return Turned(moved, side, 0, 0);
                    }
        }

        /// <summary>The cells round the fire, nearest first, for a piece of the given size, both ways round.</summary>
        private static IEnumerable<CampPiece> Around((int x, int z) at, string role, int w, int d, int from, int to, bool turn)
        {
            var cands = new List<(CampPiece p, float dist, float angle)>();
            for (int x = at.x - to; x <= at.x + to; x++)
                for (int z = at.z - to; z <= at.z + to; z++)
                    foreach (int f in turn ? new[] { 0, 1 } : new[] { 0 })
                    {
                        var p = At(role, x, z, w, d, f);
                        float cx = (p.minX + p.maxX) / 2f - at.x, cz = (p.minZ + p.maxZ) / 2f - at.z;
                        float dist = (float)Math.Sqrt(cx * cx + cz * cz);
                        if (dist < from || dist > to) continue;
                        cands.Add((p, dist, (float)Math.Atan2(cz, cx)));
                    }
            // Nearest first; at a distance, the far side from the colony first (+z), then its sides, the colony's side last.
            return cands.OrderBy(c => Math.Round(c.dist)).ThenBy(c => Math.Abs(c.angle - Math.PI / 2)).Select(c => c.p);
        }

        /// <summary>The first fitting places (up to <see cref="Weigh"/>), the cheapest of them taken; false when none fits.</summary>
        private static bool Place(Grid g, IEnumerable<(List<CampPiece> solid, List<(int, int)> keep)> places, CampLayout layout, bool keepMustWalk = false)
        {
            int best = int.MaxValue, seen = 0;
            (List<CampPiece> solid, List<(int, int)> keep) pick = (null, null);
            foreach (var pl in places)
            {
                int c = g.Try(pl.solid, pl.keep, keepMustWalk);
                if (c < 0) continue;
                if (c < best)
                {
                    best = c;
                    pick = pl;
                }
                if (++seen >= Weigh || c == 0) break;
            }
            if (pick.solid == null) return false;
            g.Put(pick.solid, pick.keep);
            layout.pieces.AddRange(pick.solid);
            layout.cost += best;
            return true;
        }

        private static IEnumerable<(List<CampPiece>, List<(int, int)>)> Singles(IEnumerable<CampPiece> pieces, Func<CampPiece, List<(int, int)>> keep) =>
            pieces.Select(p => (new List<CampPiece> { p }, keep(p)));

        /// <summary>
        /// The camp laid out round its fire at (0,0), +z away from the colony, piece by piece where each fits
        /// (<paramref name="cost"/>: what a cell costs to clear, -1 never; none: open ground everywhere): the fire with a
        /// way round it; the shelter (one hut, or a row of tents) facing the fire; a bed for every sleeper the shelter
        /// doesn't hold, round the fire; the stash on the far side; the tables with their seats; the butcher's and the
        /// crafter's spots, the stall, the spot, the lights, the picket by the stash; the windbreak behind the beds.
        /// </summary>
        public static CampLayout Plan(CampLayoutSpec spec, Func<int, int, int> cost = null)
        {
            var layout = new CampLayout();
            var g = new Grid(cost);
            var fire = new CampPiece { role = "fire" };
            // The fire, with the ring round it kept free to sit, cook and walk past.
            if (g.Try(new List<CampPiece> { fire }, Ring((0, 0)), true) < 0)
            {
                layout.missing.Add("fire");
                return layout;
            }
            g.Put(new List<CampPiece> { fire }, Ring((0, 0)));
            layout.pieces.Add(fire);
            layout.cost += Math.Max(0, g.cost(0, 0));
            int n = Math.Max(1, spec.sleepers);
            int bedsLeft = n;
            bool livable = true;

            // ---- The shelter ----
            if (spec.sleep == CampSleepForm.Hut || (spec.sleep == CampSleepForm.Tents && !spec.tentBeds))
            {
                var blocks = new List<List<CampPiece>>();
                if (spec.sleep == CampSleepForm.Hut) blocks.Add(Room(n, false, spec.bedW, spec.bedD, 1));
                else
                {
                    // Tents of two, no more than four in a row; a second row for a big camp.
                    int tents = (n + 1) / 2;
                    int first = Math.Min(tents, 4);
                    blocks.Add(TentRow(Math.Min(n, first * 2), first, spec.bedW, spec.bedD, 1));
                    if (tents > first) blocks.Add(TentRow(n - first * 2, tents - first, spec.bedW, spec.bedD, 1 + first));
                }
                foreach (var block in blocks)
                {
                    bool ok = Place(g, ShelterPlaces(block).Select(b => (b, ShelterKeep(b))), layout, true);
                    if (ok) bedsLeft -= block.Count(p => p.role == "hutbed");
                    else layout.missing.Add(spec.sleep == CampSleepForm.Hut ? "hut" : "tent");
                }
            }

            // ---- Beds round the fire (and a tent each, where the tents are beds: further out, in a ring of their own) ----
            for (int i = 0; i < bedsLeft; i++)
            {
                int from = spec.sleep == CampSleepForm.Tents ? 3 : 2;
                var places = Around((0, 0), "bed", spec.bedW, spec.bedD, from, 7, true).Select(p => (new List<CampPiece> { p }, Around(p)));
                if (!Place(g, places, layout))
                {
                    layout.missing.Add("bed");
                    livable = false;
                }
            }

            // ---- The windbreak just behind the beds round the fire (on the far side first) ----
            if (spec.windbreak)
            {
                var beds = layout.pieces.Where(p => p.role == "bed").ToList();
                int back = beds.Count > 0 ? Math.Min(Reach, beds.Where(p => p.minZ >= 0).Select(p => p.maxZ + 1).DefaultIfEmpty(3).Max()) : 3;
                var places = new List<(List<CampPiece>, List<(int, int)>)>();
                foreach (int side in new[] { 0, 1, 3, 2 })
                    foreach (int len in new[] { 7, 5 })
                        for (int z = Math.Max(3, back - 1); z <= Reach; z++)
                            foreach (int shift in new[] { 0, -1, 1, -2, 2 })
                            {
                                var line = new List<CampPiece>();
                                for (int x = -(len / 2); x <= len / 2; x++) line.Add(new CampPiece { role = "wall", minX = x + shift, maxX = x + shift, minZ = z, maxZ = z });
                                places.Add((Turned(line, side, 0, 0), new List<(int, int)>()));
                            }
                if (!Place(g, places, layout)) layout.missing.Add("wall");
            }

            // ---- The stash: room for their goods, out of the way of the rest ----
            {
                int bestCost = int.MaxValue, seen = 0;
                (int x, int z)? at = null;
                foreach (var p in Around((0, 0), "stash", 2, 2, 4, 9, false).Where(p => p.minZ >= -1))
                {
                    int c = 0;
                    bool ok = true;
                    foreach (var k in p.Cells())
                    {
                        int v = g.cells.ContainsKey(k) ? -1 : g.cost(k.x, k.z);
                        if (v < 0)
                        {
                            ok = false;
                            break;
                        }
                        c += v;
                    }
                    if (!ok) continue;
                    if (c < bestCost)
                    {
                        bestCost = c;
                        at = (p.minX, p.minZ);
                    }
                    if (++seen >= Weigh || c == 0) break;
                }
                if (at.HasValue)
                {
                    layout.stash = at.Value;
                    var area = At("stash", at.Value.x, at.Value.z, 2, 2, 0);
                    if (spec.stash)
                    {
                        // A shelf or a basket along its back, the rest of it for what doesn't go on it.
                        var shelf = At("stash", at.Value.x, at.Value.z + 1, 2, 1, 0);
                        g.Put(new List<CampPiece> { shelf }, area.Cells().Where(c => !shelf.Holds(c.x, c.z)).ToList());
                        layout.pieces.Add(shelf);
                        layout.cost += bestCost;
                    }
                    else g.Put(new List<CampPiece>(), area.Cells().ToList());
                }
                else
                {
                    layout.stash = (3, 3);
                    if (spec.stash) layout.missing.Add("stash");
                }
            }

            // ---- The tables, a seat at each side ----
            for (int t = 0; t < spec.tables; t++)
            {
                var places = Around((0, 0), "table", spec.tableW, spec.tableD, 3, 8, true).Select(tb =>
                {
                    var solid = new List<CampPiece> { tb };
                    if (spec.seats) solid.AddRange(SeatsFor(tb));
                    return (solid, Around(Bounds(solid, "dining")));
                });
                if (!Place(g, places, layout)) layout.missing.Add("table");
            }

            // ---- The work spots and the rest ----
            void One(string role, bool wanted, int from, int to, Func<CampPiece, bool> where = null)
            {
                if (!wanted) return;
                var places = Around((0, 0), role, 1, 1, from, to, false).Where(p => where == null || where(p))
                    .Select(p => (new List<CampPiece> { p }, new List<(int, int)> { (p.minX, p.minZ - 1) }));
                if (!Place(g, places, layout, true)) layout.missing.Add(role);
            }
            One("butcher", spec.butcher, 3, 8, p => p.minZ <= 2);
            One("craft", spec.craft, 3, 8);
            if (spec.stall)
            {
                // On the colony's side first, where the colony comes to trade; anywhere round the fire if that's full.
                var cands = Around((0, 0), "stall", 1, 2, 3, 9, true).ToList();
                var places = cands.Where(p => p.maxZ <= 0).Concat(cands.Where(p => p.maxZ > 0)).Select(p => (new List<CampPiece> { p }, Around(p)));
                if (!Place(g, places, layout)) layout.missing.Add("stall");
            }
            One("spot", spec.spot, 3, 8);
            for (int i = 0; i < spec.lights; i++)
                One("light", true, 3, 8, p => layout.pieces.Where(x => x.role == "light").All(x => Math.Abs(x.minX - p.minX) + Math.Abs(x.minZ - p.minZ) >= 6));
            if (spec.picket)
            {
                var s = layout.stash;
                var places = Around((s.x, s.z), "picket", 1, 1, 2, 5, false).Select(p => (new List<CampPiece> { p }, Ring((p.minX, p.minZ))));
                if (!Place(g, places, layout)) layout.missing.Add("picket");
            }

            layout.Livable = livable && !layout.missing.Contains("hut") && !layout.missing.Contains("tent") && !layout.missing.Contains("fire");
            return layout;
        }

        /// <summary>The cells a shelter keeps free: the step outside each door, and its inside's way from the door.</summary>
        private static List<(int, int)> ShelterKeep(List<CampPiece> block)
        {
            var keep = new List<(int, int)>();
            var solid = new HashSet<(int, int)>(block.SelectMany(p => p.Cells()));
            foreach (var d in block.Where(p => p.role == "door"))
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var c = (d.minX + dx, d.minZ + dz);
                    if (!solid.Contains(c)) keep.Add(c);
                }
            foreach (int room in block.Select(p => p.room).Distinct())
                keep.AddRange(Inside(block, room).Where(c => !solid.Contains(c)));
            return keep;
        }

        /// <summary>A seat on each long side of each table cell, facing it.</summary>
        public static List<CampPiece> SeatsFor(CampPiece table)
        {
            var list = new List<CampPiece>();
            bool alongZ = table.Depth >= table.Width;
            if (alongZ)
                for (int z = table.minZ; z <= table.maxZ; z++)
                {
                    list.Add(new CampPiece { role = "seat", minX = table.minX - 1, maxX = table.minX - 1, minZ = z, maxZ = z, facing = 1 });
                    list.Add(new CampPiece { role = "seat", minX = table.maxX + 1, maxX = table.maxX + 1, minZ = z, maxZ = z, facing = 3 });
                }
            else
                for (int x = table.minX; x <= table.maxX; x++)
                {
                    list.Add(new CampPiece { role = "seat", minX = x, maxX = x, minZ = table.minZ - 1, maxZ = table.minZ - 1, facing = 0 });
                    list.Add(new CampPiece { role = "seat", minX = x, maxX = x, minZ = table.maxZ + 1, maxZ = table.maxZ + 1, facing = 2 });
                }
            return list;
        }

        private static CampPiece Bounds(List<CampPiece> pieces, string role) => new CampPiece
        {
            role = role, minX = pieces.Min(p => p.minX), maxX = pieces.Max(p => p.maxX), minZ = pieces.Min(p => p.minZ), maxZ = pieces.Max(p => p.maxZ)
        };

        // ---------------- Sizes ----------------

        /// <summary>A table (1x2, four seats) for every four of them, up to three; none for a night's stop.</summary>
        public static int TablesFor(int people, float days, bool tables)
        {
            if (!tables || days < 1.5f) return 0;
            return Math.Max(1, Math.Min(3, (people + 3) / 4));
        }

        /// <summary>A torch for a small camp, two from six, three from ten.</summary>
        public static int LightsFor(int people, bool lights) => !lights ? 0 : people >= 10 ? 3 : people >= 6 ? 2 : 1;

        /// <summary>A crafting spot for a stay of two days or more.</summary>
        public static bool CraftFor(float days) => days >= 2f;

        // ---------------- Growing over the stay ----------------

        /// <summary>A stay shorter than this adds nothing to the camp: it stays as it was put up.</summary>
        public const float GrowFromDays = 2.5f;

        /// <summary>The most times a camp adds to itself, however long and big.</summary>
        public const int GrowMost = 6;

        /// <summary>They add nothing new in the last day of their stay.</summary>
        public const float GrowStopDays = 1f;

        /// <summary>A step not built within this many days is given up (its blueprints taken up, the materials back in the stash).</summary>
        public const float GrowGiveUpDays = 1.5f;

        /// <summary>Everything a camp can add as it settles in, by the role its pieces take (Camps/CampGrow.cs; the kits' grows, Rimfolk_CampKits.xml).</summary>
        public static readonly string[] GrowRoles = { "fireseat", "post", "fence", "drying", "stonework", "tailoring", "woodpile", "shrine", "banner", "latrine", "basket", "store" };

        /// <summary>
        /// How many times a camp adds to itself over its stay: none for a stay under two and a half days, about once for every
        /// day and a quarter after the first, never more than two and one for every two of its people, six at most. A short
        /// camp stays as it was put up, a long one grows to a settled look; everything stays within its reach of the fire.
        /// </summary>
        public static int GrowCeiling(int people, float days)
        {
            if (days < GrowFromDays || people <= 0) return 0;
            int byDays = (int)Math.Floor((days - 1.25f) / 1.25f);
            return Math.Max(0, Math.Min(GrowMost, Math.Min(byDays, 2 + people / 2)));
        }

        /// <summary>The hours to the next addition (<paramref name="roll"/> 0 to 1): the first 18 to 28 hours after the camp stands, the rest a day or so apart (19 to 34 hours).</summary>
        public static int GrowGapHours(int step, double roll)
        {
            roll = Math.Max(0, Math.Min(1, roll));
            return step <= 0 ? 18 + (int)Math.Round(roll * 10) : 19 + (int)Math.Round(roll * 15);
        }

        /// <summary>
        /// What a camp would add as it settles in, the most fitting first: its kind's own (a pen for their beasts, a drying
        /// rack for hunters, a stonecutter's table for those who come to cut rock, a tailor's bench for crafters, a firewood
        /// rack for woodcutters, a small altar for a people of a faith without a spot of their own) in turn with what any
        /// camp would (seats round the fire, a lean-to over the stash for a stay of four days or more, a latrine for four
        /// people or more staying four days, a banner at the edge for a stay of five days, of a people past the tribal age or
        /// the Empire's); first of all a second store when their goods lie about with no room for them. Only roles with a
        /// piece in this game that their people know how to build (<see cref="GrowFacts.has"/>).
        /// </summary>
        public static List<string> GrowWants(GrowFacts f)
        {
            var own = new List<string>();
            if (f.beasts) own.Add("fence");
            if (f.hunts) own.Add("drying");
            if (f.calling == "Mine") own.Add("stonework");
            else if (f.calling == "Craft" && !f.workTable) own.Add("tailoring");
            if (f.calling == "Chop") own.Add("woodpile");
            if (f.faith && !f.ownSpot) own.Add("shrine");
            var common = new List<string>();
            if (f.dines && f.people >= 2) common.Add("fireseat");
            if (f.stash && f.days >= 4f) common.Add("post");
            if (f.people >= 4 && f.days >= 4f) common.Add("latrine");
            if (f.days >= 5f && (f.empire || f.age != "tribal")) common.Add("banner");
            var list = new List<string>();
            if (f.loose > 0) list.Add(f.looseFood && (f.has == null || f.has("basket")) ? "basket" : "store");
            int i = 0, j = 0;
            while (i < own.Count || j < common.Count)
            {
                if (i < own.Count) list.Add(own[i++]);
                if (j < common.Count) list.Add(common[j++]);
            }
            return list.Where(r => f.has == null || f.has(r)).Distinct().ToList();
        }

        /// <summary>The next thing they add: the first wanted they haven't built or tried; null when there's nothing left, or they've grown as much as the stay allows.</summary>
        public static string GrowNext(GrowFacts f, ICollection<string> done, int grown) =>
            grown >= GrowCeiling(f.people, f.days) ? null : GrowWants(f).FirstOrDefault(r => done == null || !done.Contains(r));

        /// <summary>How many pieces one addition is: a seat round the fire for each of them up to four, four posts for a lean-to, one of the rest (a pen's fences are counted when it's placed).</summary>
        public static int GrowPieces(string role, int people) => role == "fireseat" ? Math.Max(2, Math.Min(4, people)) : role == "post" ? 4 : role == "fence" ? 16 : 1;

        /// <summary>A piece's front cell: where one stands to use it (a bench's place to stand), the middle of its side by its facing.</summary>
        public static (int x, int z) FrontCell(CampPiece p)
        {
            int midX = p.minX + (p.Width - 1) / 2, midZ = p.minZ + (p.Depth - 1) / 2;
            switch (p.facing)
            {
                case 1: return (p.minX - 1, midZ);
                case 2: return (midX, p.maxZ + 1);
                case 3: return (p.maxX + 1, midZ);
                default: return (midX, p.minZ - 1);
            }
        }

        /// <summary>The facing that puts a piece's front toward a point (a bench's place to stand on the fire's side).</summary>
        public static int FrontToward(float dx, float dz) => Math.Abs(dz) >= Math.Abs(dx) ? (dz > 0 ? 0 : 2) : (dx > 0 ? 1 : 3);

        /// <summary>The facing of a seat at (dx, dz) from the fire, looking at it.</summary>
        public static int LookToward(float dx, float dz) => Math.Abs(dz) >= Math.Abs(dx) ? (dz > 0 ? 2 : 0) : (dx > 0 ? 3 : 1);

        /// <summary>
        /// Where one addition goes, in the camp's frame (the fire at (0,0), +z away from the colony), or null when there's no
        /// room for it: seats round the fire, a cell apart, looking at it; four posts at the corners of a roof over the
        /// stash; a pen of fences round a free patch near the picket with a gap on the fire's side; the rest each where it
        /// fits best by what it is (a drying rack by the butcher's spot, a store by the stash, a bench or a firewood rack
        /// near the fire with its front free, an altar on the far side, a banner at the edge on the colony's side, a latrine
        /// at the far edge away from the beds). Never on what stands, on a cell kept free, or on the colony's ground; all
        /// within the camp's reach. <paramref name="w"/>, <paramref name="d"/>: the piece's size facing north.
        /// </summary>
        public static List<CampPiece> GrowPlace(string role, GrowSite site, int w = 1, int d = 1, int count = 1, int beasts = 0)
        {
            if (site == null) return null;
            var g = new Grid(site.cost);
            foreach (var c in site.taken) g.cells[c] = 1;
            foreach (var c in site.keep)
                if (!g.cells.ContainsKey(c)) g.cells[c] = 2;
            // The fire's ring stays free whatever the site says.
            foreach (var c in Ring((0, 0)))
                if (!g.cells.ContainsKey(c)) g.cells[c] = 2;
            g.cells[(0, 0)] = 1;
            w = Math.Max(1, w);
            d = Math.Max(1, d);
            switch (role)
            {
                case "fireseat": return FireSeats(g, Math.Max(2, Math.Min(4, count)));
                case "post": return LeanTo(g, site);
                case "fence": return Pen(g, site, beasts);
                case "drying": return GrowOne(g, role, site.butcher ?? (0, 0), w, d, 1, site.butcher.HasValue ? 3 : 6, null);
                case "basket":
                case "store": return GrowOne(g, role, site.stash ?? (0, 3), w, d, 1, 6, null);
                case "woodpile":
                case "stonework":
                case "tailoring": return GrowOne(g, role, (0, 0), w, d, 3, 8, null);
                case "shrine": return GrowOne(g, role, (0, 0), w, d, 5, 9, p => p.minZ >= 2);
                case "banner": return GrowOne(g, role, (0, 0), w, d, 6, Reach, p => p.maxZ <= -4);
                case "latrine":
                    return GrowOne(g, role, (0, 0), w, d, 7, Reach, p => p.minZ >= 3 && site.beds.All(b => Math.Abs(b.x - p.minX) + Math.Abs(b.z - p.minZ) >= 5));
                default: return null;
            }
        }

        /// <summary>A piece (or a few) placed where it fits, nearest <paramref name="at"/> first, the far side from the colony first, its front toward the fire and kept free.</summary>
        private static List<CampPiece> GrowOne(Grid g, string role, (int x, int z) at, int w, int d, int from, int to, Func<CampPiece, bool> where)
        {
            var cands = new List<(CampPiece p, float dist, float angle)>();
            int span = to + Math.Max(w, d);
            for (int x = at.x - span; x <= at.x + span; x++)
                for (int z = at.z - span; z <= at.z + span; z++)
                    for (int f = 0; f < 4; f++)
                    {
                        // A square piece needs no turning; a long one is tried both ways.
                        if (w == d && f % 2 == 1) continue;
                        var p = At(role, x, z, w, d, f);
                        float cx = (p.minX + p.maxX) / 2f, cz = (p.minZ + p.maxZ) / 2f;
                        float dist = (float)Math.Sqrt((cx - at.x) * (cx - at.x) + (cz - at.z) * (cz - at.z));
                        if (dist < from || dist > to) continue;
                        // Its front toward the fire (a square piece's toward it too, whichever way it was tried).
                        int want = FrontToward(cx, cz);
                        if (w != d && f != want) continue;
                        p.facing = want;
                        if (where != null && !where(p)) continue;
                        cands.Add((p, dist, (float)Math.Atan2(cz, cx)));
                    }
            var layout = new CampLayout();
            var places = cands.OrderBy(c => Math.Round(c.dist)).ThenBy(c => Math.Abs(c.angle - Math.PI / 2)).Select(c => (new List<CampPiece> { c.p }, new List<(int, int)> { FrontCell(c.p) }));
            return Place(g, places, layout, true) ? layout.pieces : null;
        }

        /// <summary>Seats round the fire two cells out, looking at it, a cell apart so there is always a way through, the far side first.</summary>
        private static List<CampPiece> FireSeats(Grid g, int count)
        {
            var ring = new List<(int x, int z)>();
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) == 2) ring.Add((x, z));
            var placed = new List<CampPiece>();
            foreach (var (x, z) in ring.OrderBy(c => Math.Abs(Math.Atan2(c.z, c.x) - Math.PI / 2)).ThenBy(c => Math.Abs(c.x) + Math.Abs(c.z)))
            {
                if (placed.Count >= count) break;
                // A cell apart from the others, so the seats never close the fire in.
                if (placed.Any(p => Math.Abs(p.minX - x) <= 1 && Math.Abs(p.minZ - z) <= 1)) continue;
                var seat = new CampPiece { role = "fireseat", minX = x, maxX = x, minZ = z, maxZ = z, facing = LookToward(x, z) };
                if (g.Try(new List<CampPiece> { seat }, new List<(int, int)>(), false) < 0) continue;
                g.Put(new List<CampPiece> { seat }, new List<(int, int)>());
                placed.Add(seat);
            }
            return placed.Count >= 2 ? placed : null;
        }

        /// <summary>The most posts a lean-to stands on, and how far its roof may reach from the nearest of them (the game's roofs need a holder within six cells).</summary>
        public const int LeanToPosts = 4;
        public const int RoofReach = 5;

        /// <summary>
        /// A lean-to over the stash: posts round the stash's ground and its shelf, the corners first and as far apart as fits
        /// (two at least, four at most), a cell or two out from it; its roof (<see cref="Roofed"/>) over the stash, the shelf
        /// and the posts, every roofed cell their own ground and near a post. Null without a stash or room for two posts.
        /// </summary>
        private static List<CampPiece> LeanTo(Grid g, GrowSite site)
        {
            if (site.cover.Count == 0) return null;
            int x0 = site.cover.Min(c => c.x), x1 = site.cover.Max(c => c.x), z0 = site.cover.Min(c => c.z), z1 = site.cover.Max(c => c.z);
            // Cells a cell or two out from the stash, the corners a cell out first.
            var ring = new List<(int x, int z, int rank)>();
            for (int x = x0 - 2; x <= x1 + 2; x++)
                for (int z = z0 - 2; z <= z1 + 2; z++)
                {
                    if (x >= x0 && x <= x1 && z >= z0 && z <= z1) continue;
                    int dx = x < x0 ? x0 - x : x > x1 ? x - x1 : 0, dz = z < z0 ? z0 - z : z > z1 ? z - z1 : 0;
                    int rank = dx == 1 && dz == 1 ? 0 : Math.Max(dx, dz) == 1 ? 1 : dx == 2 && dz == 2 ? 2 : 3;
                    ring.Add((x, z, rank));
                }
            var posts = new List<CampPiece>();
            while (posts.Count < LeanToPosts)
            {
                CampPiece pick = null;
                float best = float.MinValue;
                foreach (var (x, z, rank) in ring)
                {
                    if (posts.Any(p => Math.Max(Math.Abs(p.minX - x), Math.Abs(p.minZ - z)) < 2)) continue;
                    var post = new CampPiece { role = "post", minX = x, maxX = x, minZ = z, maxZ = z };
                    if (g.Try(new List<CampPiece> { post }, new List<(int, int)>(), false) < 0) continue;
                    // The corners first, then as far from the posts already standing as can be.
                    float apart = posts.Count == 0 ? 0f : posts.Min(p => Math.Abs(p.minX - x) + Math.Abs(p.minZ - z));
                    float score = apart * 2f - rank * 3f;
                    if (score <= best) continue;
                    best = score;
                    pick = post;
                }
                if (pick == null) break;
                posts.Add(pick);
                ring.RemoveAll(c => c.x == pick.minX && c.z == pick.minZ);
            }
            if (posts.Count < 2) return null;
            // The roof only over ground of theirs to build on (or what stands of theirs), within the camp's reach and near a post.
            var roof = Roofed(posts.Select(p => (p.minX, p.minZ)), site.cover);
            foreach (var (x, z) in roof)
                if (Math.Abs(x) > Reach || Math.Abs(z) > Reach || (!g.cells.ContainsKey((x, z)) && g.cost(x, z) < 0)
                    || !posts.Any(p => Math.Max(Math.Abs(p.minX - x), Math.Abs(p.minZ - z)) <= RoofReach)) return null;
            g.Put(posts, new List<(int, int)>());
            return posts;
        }

        /// <summary>The roof a lean-to's posts hold: every cell within them and the stash they stand round (<paramref name="cover"/>).</summary>
        public static List<(int x, int z)> Roofed(IEnumerable<(int x, int z)> posts, IEnumerable<(int x, int z)> cover = null)
        {
            var list = posts.ToList();
            var cells = new List<(int, int)>();
            if (list.Count < 2) return cells;
            var all = list.Concat(cover ?? Enumerable.Empty<(int x, int z)>()).ToList();
            for (int x = all.Min(p => p.x); x <= all.Max(p => p.x); x++)
                for (int z = all.Min(p => p.z); z <= all.Max(p => p.z); z++)
                    cells.Add((x, z));
            return cells;
        }

        /// <summary>
        /// A pen for their beasts: fences round a free patch (three by three, four by three for four beasts or more) near the
        /// picket (else the stash), a gap on the side nearest the fire so the beasts can walk out with them when they go; null
        /// when no patch fits.
        /// </summary>
        private static List<CampPiece> Pen(Grid g, GrowSite site, int beasts)
        {
            int iw = beasts >= 4 ? 4 : 3, id = 3;
            var near = site.picket ?? site.stash ?? (0, 5);
            List<CampPiece> best = null;
            List<(int, int)> bestKeep = null;
            float bestScore = float.MaxValue;
            for (int ix = near.x - 7; ix <= near.x + 7; ix++)
                for (int iz = near.z - 7; iz <= near.z + 7; iz++)
                {
                    int ox0 = ix - 1, ox1 = ix + iw, oz0 = iz - 1, oz1 = iz + id;
                    if (Math.Abs(ox0) > Reach || Math.Abs(ox1) > Reach || Math.Abs(oz0) > Reach || Math.Abs(oz1) > Reach) continue;
                    // Clear of the fire's ring and two cells beyond.
                    if (ox0 <= 3 && ox1 >= -3 && oz0 <= 3 && oz1 >= -3) continue;
                    var ring = new List<(int x, int z)>();
                    var inside = new List<(int x, int z)>();
                    for (int x = ox0; x <= ox1; x++)
                        for (int z = oz0; z <= oz1; z++)
                        {
                            bool edge = x == ox0 || x == ox1 || z == oz0 || z == oz1;
                            if (edge) ring.Add((x, z));
                            else inside.Add((x, z));
                        }
                    // The gap: the side cell nearest the fire (never a corner), and the step outside it.
                    var gap = ring.Where(c => !((c.x == ox0 || c.x == ox1) && (c.z == oz0 || c.z == oz1))).OrderBy(c => c.x * c.x + c.z * c.z).First();
                    var step = (gap.x == ox0 ? gap.x - 1 : gap.x == ox1 ? gap.x + 1 : gap.x, gap.z == oz0 ? gap.z - 1 : gap.z == oz1 ? gap.z + 1 : gap.z);
                    var fences = ring.Where(c => c != gap).Select(c => new CampPiece { role = "fence", minX = c.x, maxX = c.x, minZ = c.z, maxZ = c.z }).ToList();
                    var keep = inside.Concat(new[] { gap, step }).ToList();
                    int cost = g.Try(fences, keep, true);
                    if (cost < 0 || keep.Any(k => g.cells.ContainsKey(k))) continue;
                    float cx = (ox0 + ox1) / 2f, cz = (oz0 + oz1) / 2f;
                    float score = (float)Math.Sqrt((cx - near.x) * (cx - near.x) + (cz - near.z) * (cz - near.z)) + cost * 0.2f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = fences;
                    bestKeep = keep;
                }
            if (best == null) return null;
            g.Put(best, bestKeep);
            return best;
        }

        /// <summary>The patch a pen's fences close in (its inside), from the fences as they stand.</summary>
        public static List<(int x, int z)> PenInside(IEnumerable<(int x, int z)> fences)
        {
            var list = fences.ToList();
            var cells = new List<(int, int)>();
            if (list.Count < 8) return cells;
            int x0 = list.Min(c => c.x), x1 = list.Max(c => c.x), z0 = list.Min(c => c.z), z1 = list.Max(c => c.z);
            for (int x = x0 + 1; x < x1; x++)
                for (int z = z0 + 1; z < z1; z++)
                    cells.Add((x, z));
            return cells;
        }

        // ---------------- Ages ----------------

        /// <summary>The form a people's camp takes by their age: tribal, medieval, industrial, spacer (the TechLevel's number: 2 neolithic, 3 medieval...).</summary>
        public static string AgeOf(int techLevel) => techLevel <= 2 ? "tribal" : techLevel == 3 ? "medieval" : techLevel == 4 ? "industrial" : "spacer";

        public static readonly string[] Ages = { "tribal", "medieval", "industrial", "spacer" };

        /// <summary>A research's age (its TechLevel's number) within a people's: they know how to build it.</summary>
        public static bool Within(int researchTech, int peopleTech) => researchTech <= Math.Max(2, peopleTech);
    }
}
