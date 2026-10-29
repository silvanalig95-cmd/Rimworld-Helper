using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Tidying a built room (Building/RoomRearranger.cs) as plain integer rules, with no game types, so the tests can
    /// run them over drawn rooms. The room is a mask of floor cells of any shape with one or more doors; the furniture
    /// is a list of items, each with its size, facing rules and work spots (like <see cref="TemplateLayout.Piece"/>),
    /// its kind, and where it stands now. Items that stay put (walls, pipes, what can't be moved) block their cells.
    /// Everything else is placed anew, biggest and most particular first: beds with their head to a wall and room to
    /// get in, bedside tables by a bed's head, benches with their back to a wall and their work spot free, tables in
    /// the middle with chairs facing them, shelves along the walls in runs, heaters and the like flat against a wall,
    /// plant pots and lamps in corners. The cell inside each door stays free, nothing covers anything else or a work
    /// spot, and every piece can be reached from the doors, or it is placed again until it can. What is used every
    /// day (beds, benches, tables) leans toward the door so the walk is short, and lamps and pots keep out of the way.
    /// In a big workshop benches stand in rows and, when the walls run out, back to back in islands; a tool cabinet
    /// goes where it reaches the most benches, shelves near the benches, a stool on a bench's work spot. A piece already
    /// standing as well as it could stays where it is: moving furniture is work.
    /// </summary>
    public static class RoomArrangeMath
    {
        // ---------------- Kinds ----------------

        /// <summary>What a piece is for, as far as its place in the room goes.</summary>
        public const int Bed = 0, BedSide = 1, Bench = 2, Table = 3, Chair = 4, Storage = 5, WallHugger = 6, Decor = 7, Centre = 8, Other = 9;

        /// <summary>The order the kinds are placed in: the big and particular first, what fits around them after.</summary>
        private static readonly int[] PlaceOrder = { Bed, Bench, Table, Centre, Storage, WallHugger, BedSide, Chair, Decor, Other };

        /// <summary>How much better a new place has to be before a piece is moved.</summary>
        public const float KeepBonus = 2.5f;

        /// <summary>Tries at placing again what ended up out of reach.</summary>
        private const int ReachRounds = 6;

        // ---------------- Input ----------------

        public class Item
        {
            public int id;
            public int kind = Other;
            public int sizeX = 1, sizeZ = 1;
            /// <summary>False: the game only builds it facing north.</summary>
            public bool rotatable = true;
            /// <summary>People walk over or sit in it.</summary>
            public bool passable;
            /// <summary>Needs the heavier kind of floor (<see cref="Grid.heavy"/>).</summary>
            public bool heavy;
            /// <summary>Its work spots for a north facing, relative to the root cell.</summary>
            public List<(int x, int z)> spots = new List<(int x, int z)>();
            /// <summary>Stays where it is, and blocks its cells.</summary>
            public bool fixedPlace;
            /// <summary>A facility (a tool cabinet, a dresser): how far it reaches the pieces it serves; 0 for none.</summary>
            public float reach;
            /// <summary>The ids of the pieces this facility serves (benches for a cabinet, beds for a dresser).</summary>
            public HashSet<int> links;
            /// <summary>Where it stands now: root cell and facing, in grid coordinates.</summary>
            public int curX, curZ, curRot;

            // Results
            /// <summary>Where it goes: root cell and facing.</summary>
            public int x, z, rot;
            public int minX, minZ, w, h;
            /// <summary>It goes somewhere else than it stands.</summary>
            public bool moved;
            /// <summary>Nowhere to put it: it stays where it stands (and blocks its cells).</summary>
            public bool stranded;
            /// <summary>Why it goes where it goes, as a short key: "bedwall", "bedside", "table", "wall", "corner", "run", "middle", "kept", "fits".</summary>
            public string why;

            public bool Contains(int cx, int cz) => cx >= minX && cx < minX + w && cz >= minZ && cz < minZ + h;

            /// <summary>The work spots for the result's footprint and facing.</summary>
            public List<(int x, int z)> SpotCells()
            {
                var result = new List<(int, int)>();
                if (spots == null) return result;
                foreach (var (ox, oz) in spots)
                {
                    TemplateLayout.RotateOffset(ox, oz, rot, out int sx, out int sz);
                    result.Add((x + sx, z + sz));
                }
                return result;
            }

            /// <summary>The cells a sleeper's head lies on: the row of the footprint at the back, away from the facing (BedUtility).</summary>
            public List<(int x, int z)> HeadCells()
            {
                var result = new List<(int, int)>();
                switch (rot)
                {
                    case 0:
                        for (int cx = minX; cx < minX + w; cx++) result.Add((cx, minZ));
                        break;
                    case 2:
                        for (int cx = minX; cx < minX + w; cx++) result.Add((cx, minZ + h - 1));
                        break;
                    case 1:
                        for (int cz = minZ; cz < minZ + h; cz++) result.Add((minX, cz));
                        break;
                    default:
                        for (int cz = minZ; cz < minZ + h; cz++) result.Add((minX + w - 1, cz));
                        break;
                }
                return result;
            }
        }

        /// <summary>The room: which cells are floor, which take heavy furniture, and the floor cell inside each door.</summary>
        public class Grid
        {
            public int w, h;
            public bool[,] floor;
            /// <summary>Null: every floor cell takes anything.</summary>
            public bool[,] heavy;
            public List<(int x, int z)> doorInside = new List<(int x, int z)>();

            public bool In(int x, int z) => x >= 0 && z >= 0 && x < w && z < h;
            public bool Floor(int x, int z) => In(x, z) && floor[x, z];
            /// <summary>Not floor: a wall, or anything else that bounds the room.</summary>
            public bool Wall(int x, int z) => !Floor(x, z);
            public bool Heavy(int x, int z) => heavy == null || (In(x, z) && heavy[x, z]);
            public bool IsDoorInside(int x, int z)
            {
                foreach (var d in doorInside) if (d.x == x && d.z == z) return true;
                return false;
            }
        }

        // ---------------- Arranging ----------------

        private class State
        {
            public Grid g;
            /// <summary>The item on each cell (-1 none); fixed items and placed ones.</summary>
            public int[,] occ;
            /// <summary>The item whose work spot is on each cell (-1 none).</summary>
            public int[,] spot;
            public List<Item> items;

            public State(Grid g, List<Item> items)
            {
                this.g = g;
                this.items = items;
                occ = new int[g.w, g.h];
                spot = new int[g.w, g.h];
                for (int x = 0; x < g.w; x++)
                    for (int z = 0; z < g.h; z++)
                        occ[x, z] = spot[x, z] = -1;
            }

            public Item At(int x, int z) => g.In(x, z) && occ[x, z] >= 0 ? items[occ[x, z]] : null;

            public bool Solid(int x, int z)
            {
                var it = At(x, z);
                return it != null && !it.passable;
            }

            /// <summary>A wall (or a pillar, a partition: anything that isn't the room's floor): what a piece can lean on.</summary>
            public bool Backing(int x, int z) => g.Wall(x, z);

            public void Put(int i, Item it)
            {
                for (int x = it.minX; x < it.minX + it.w; x++)
                    for (int z = it.minZ; z < it.minZ + it.h; z++)
                        if (g.In(x, z)) occ[x, z] = i;
                foreach (var (sx, sz) in it.SpotCells())
                    if (g.In(sx, sz)) spot[sx, sz] = i;
            }

            public void Lift(int i, Item it)
            {
                for (int x = 0; x < g.w; x++)
                    for (int z = 0; z < g.h; z++)
                    {
                        if (occ[x, z] == i) occ[x, z] = -1;
                        if (spot[x, z] == i) spot[x, z] = -1;
                    }
            }
        }

        /// <summary>
        /// Places every item that isn't fixed. Returns notes on what happened. Afterwards each item has its result
        /// fields set; a stranded one keeps its current place.
        /// </summary>
        public static List<string> Arrange(Grid g, List<Item> items)
        {
            var notes = new List<string>();
            if (g == null || items == null) return notes;
            // What can't be placed anywhere stays and blocks its cells; then the rest is placed again around it. Each
            // round that doesn't end it leaves one more piece standing, so it ends.
            for (int round = 0; round <= items.Count; round++)
            {
                var state = new State(g, items);
                bool stranded = false;
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (!it.fixedPlace && !it.stranded) continue;
                    SetResult(it, it.curX, it.curZ, it.curRot);
                    it.moved = false;
                    it.why = it.fixedPlace ? null : "fits";
                    state.Put(i, it);
                }
                foreach (int kind in PlaceOrder)
                {
                    var order = new List<int>();
                    for (int i = 0; i < items.Count; i++)
                        if (!items[i].fixedPlace && !items[i].stranded && items[i].kind == kind) order.Add(i);
                    order.Sort((a, b) =>
                    {
                        int area = items[b].sizeX * items[b].sizeZ - items[a].sizeX * items[a].sizeZ;
                        return area != 0 ? area : items[a].id.CompareTo(items[b].id);
                    });
                    foreach (int i in order)
                    {
                        if (!Place(state, i, null))
                        {
                            items[i].stranded = true;
                            stranded = true;
                            notes.Add($"item {items[i].id}: nowhere to put it, stays");
                        }
                    }
                }
                if (stranded) continue;
                // Everything has to be reachable from the doors, and the doors from one another. What can't be placed
                // within reach stays where it stands, and the room is arranged again around it.
                bool ok = false;
                int last = -1;
                for (int r = 0; r < ReachRounds; r++)
                {
                    int culprit = WorstPlaced(state, out string problem);
                    if (culprit < 0)
                    {
                        ok = true;
                        break;
                    }
                    var it = items[culprit];
                    last = culprit;
                    state.Lift(culprit, it);
                    var reach = Reach(state, -1);
                    if (!Place(state, culprit, reach))
                    {
                        notes.Add($"item {it.id}: {problem}, and no reachable place for it, stays");
                        it.stranded = true;
                        stranded = true;
                        break;
                    }
                    notes.Add($"item {it.id}: {problem}, placed again");
                }
                if (ok) break;
                if (!stranded && last >= 0)
                {
                    notes.Add($"item {items[last].id}: keeps getting in the way, stays");
                    items[last].stranded = true;
                }
            }
            foreach (var it in items)
            {
                if (it.fixedPlace) continue;
                it.moved = it.x != it.curX || it.z != it.curZ || it.rot != it.curRot;
                if (!it.moved && it.why != "fits") it.why = "kept";
            }
            return notes;
        }

        private static void SetResult(Item it, int x, int z, int rot)
        {
            it.x = x;
            it.z = z;
            it.rot = it.rotatable ? TemplateMath.Norm(rot) : 0;
            TemplateLayout.OccupiedRect(x, z, it.rot, it.sizeX, it.sizeZ, out it.minX, out it.minZ, out it.w, out it.h);
        }

        /// <summary>
        /// Puts item <paramref name="i"/> at its best valid place (the one it has, if that is nearly as good). With
        /// <paramref name="reach"/> given, only places whose work spots, or a cell beside them, are on reached floor.
        /// </summary>
        private static bool Place(State s, int i, bool[,] reach)
        {
            var it = s.items[i];
            float best = float.MinValue;
            int bx = 0, bz = 0, br = 0;
            string bwhy = null;
            bool found = false;
            int facings = it.rotatable ? 4 : 1;
            for (int z = 0; z < s.g.h; z++)
                for (int x = 0; x < s.g.w; x++)
                    for (int f = 0; f < facings; f++)
                    {
                        SetResult(it, x, z, f);
                        if (!Valid(s, i, it, reach)) continue;
                        float score = Score(s, i, it, out string why);
                        bool same = x == it.curX && z == it.curZ && it.rot == TemplateMath.Norm(it.rotatable ? it.curRot : 0);
                        if (same) score += KeepBonus;
                        // Ties: closest to where it stands, then the lowest, leftmost.
                        if (found && score <= best + 0.0001f)
                        {
                            if (score < best - 0.0001f) continue;
                            if (Dist(x, z, it) >= Dist(bx, bz, it)) continue;
                        }
                        best = score;
                        bx = x;
                        bz = z;
                        br = f;
                        bwhy = why;
                        found = true;
                    }
            if (!found) return false;
            SetResult(it, bx, bz, br);
            it.why = bwhy;
            s.Put(i, it);
            return true;
        }

        private static int Dist(int x, int z, Item it) => Math.Abs(x - it.curX) + Math.Abs(z - it.curZ);

        private static bool Valid(State s, int i, Item it, bool[,] reach)
        {
            var g = s.g;
            for (int x = it.minX; x < it.minX + it.w; x++)
                for (int z = it.minZ; z < it.minZ + it.h; z++)
                {
                    if (!g.Floor(x, z) || g.IsDoorInside(x, z)) return false;
                    if (it.heavy && !g.Heavy(x, z)) return false;
                    int o = s.occ[x, z];
                    if (o >= 0 && o != i) return false;
                    // Standing on someone's work spot is fine only for what people walk over.
                    int sp = s.spot[x, z];
                    if (sp >= 0 && sp != i && !it.passable) return false;
                }
            var spots = it.SpotCells();
            foreach (var (sx, sz) in spots)
            {
                if (!g.Floor(sx, sz) || g.IsDoorInside(sx, sz) || it.Contains(sx, sz)) return false;
                if (s.Solid(sx, sz)) return false;
                // Two work spots on one cell: two people in each other's way.
                if (s.spot[sx, sz] >= 0 && s.spot[sx, sz] != i) return false;
                if (reach != null && !reach[sx, sz]) return false;
            }
            if (reach != null && spots.Count == 0 && !BesideReached(it, reach, g)) return false;
            return true;
        }

        private static bool BesideReached(Item it, bool[,] reach, Grid g)
        {
            if (it.passable)
            {
                for (int x = it.minX; x < it.minX + it.w; x++)
                    for (int z = it.minZ; z < it.minZ + it.h; z++)
                        if (g.In(x, z) && reach[x, z]) return true;
            }
            foreach (var (x, z) in Ring(it))
                if (g.In(x, z) && reach[x, z]) return true;
            return false;
        }

        /// <summary>The cells cardinally beside the footprint.</summary>
        private static IEnumerable<(int x, int z)> Ring(Item it)
        {
            for (int x = it.minX; x < it.minX + it.w; x++)
            {
                yield return (x, it.minZ - 1);
                yield return (x, it.minZ + it.h);
            }
            for (int z = it.minZ; z < it.minZ + it.h; z++)
            {
                yield return (it.minX - 1, z);
                yield return (it.minX + it.w, z);
            }
        }

        // ---------------- Scoring ----------------

        private static float Score(State s, int i, Item it, out string why)
        {
            var g = s.g;
            why = "fits";
            float score = 0f;
            bool serves = false;
            // The doorway stays clear: nothing solid right beside the cell inside a door.
            if (!it.passable)
                foreach (var (x, z) in Ring(it))
                    if (g.IsDoorInside(x, z)) score -= 3f;
            int walls = 0, sides = 0;
            foreach (var (x, z) in Ring(it)) if (s.Backing(x, z)) walls++;
            int north = SideBacking(s, it, 0), east = SideBacking(s, it, 1), south = SideBacking(s, it, 2), west = SideBacking(s, it, 3);
            sides = (north > 0 ? 1 : 0) + (east > 0 ? 1 : 0) + (south > 0 ? 1 : 0) + (west > 0 ? 1 : 0);
            // A facility counts the pieces it serves within its reach: a cabinet among the benches, a dresser by the beds.
            if (it.reach > 0f && it.links != null)
            {
                int served = 0;
                for (int j = 0; j < s.items.Count; j++)
                {
                    var o = s.items[j];
                    if (o == it || !it.links.Contains(o.id) || !InPlace(s, j, o)) continue;
                    if (Distance(it, o) <= it.reach) served++;
                }
                score += Math.Min(served, 4) * 2f;
                if (served > 0) serves = true;
            }
            int walk = DoorWalk(g, it);
            switch (it.kind)
            {
                case Bed:
                {
                    // Head to the wall, feet free to get in, not touching another bed, and a cell beside the head for a table.
                    int back = TemplateMath.Norm(it.rot + 2);
                    int headWall = SideBacking(s, it, back);
                    int headLen = back == 0 || back == 2 ? it.w : it.h;
                    if (headWall == headLen)
                    {
                        score += 10f;
                        why = "bedwall";
                    }
                    else if (headWall > 0) score += 4f;
                    if (SideBacking(s, it, it.rot) > 0) score -= 6f;
                    foreach (var (x, z) in Ring(it))
                    {
                        var other = s.At(x, z);
                        if (other != null && other != it && other.kind == Bed) score -= 4f;
                    }
                    score += Math.Min(2, sides - (headWall > 0 ? 1 : 0)) * 1.5f;
                    // Near the door: the walk to bed is made every day.
                    score -= Math.Min(walk, 12) * 0.2f;
                    break;
                }
                case Bench:
                {
                    // Back to the wall, or back to back with another bench when the walls run out; the work spot in the open.
                    int back = BackOf(it);
                    int backWall = SideBacking(s, it, back);
                    int backFurniture = SideLeaning(s, it, back);
                    int backLen = back == 0 || back == 2 ? it.w : it.h;
                    if (backLen > 0 && backWall == backLen)
                    {
                        score += 8f;
                        why = "wall";
                    }
                    else if (backLen > 0 && backWall + backFurniture == backLen)
                    {
                        score += 6f;
                        why = "island";
                    }
                    else if (backLen > 0 && backWall + backFurniture >= (backLen + 1) / 2)
                    {
                        score += 4f;
                        why = "island";
                    }
                    else if (backWall + backFurniture > 0) score += 3f;
                    // Nothing to lean on yet: a place with open floor behind leaves room for the next bench to lean on this one.
                    else if (backLen > 0 && BackFree(s, it, back) == backLen) score += 1f;
                    score += Math.Min(2, sides - (backWall > 0 ? 1 : 0)) * 1f;
                    // In a row with the next bench, facing the same way.
                    foreach (var (x, z) in SideCells(it, TemplateMath.Norm(back + 1)).Concat(SideCells(it, TemplateMath.Norm(back + 3))))
                    {
                        var o = s.At(x, z);
                        if (o != null && o != it && o.kind == Bench && BackOf(o) == back) score += 1.5f;
                    }
                    foreach (var (sx, sz) in it.SpotCells())
                        foreach (var (x, z) in Beside(sx, sz))
                            if (g.IsDoorInside(x, z)) score -= 2f;
                    score -= Math.Min(walk, 12) * 0.1f;
                    break;
                }
                case Table:
                case Centre:
                {
                    // In the middle, with room all round for chairs and people.
                    int free = 0, ring = 0;
                    foreach (var (x, z) in Ring(it))
                    {
                        ring++;
                        if (g.Floor(x, z) && !s.Solid(x, z)) free++;
                    }
                    score += free * 0.8f;
                    if (free == ring) why = "middle";
                    score -= walls * 0.6f;
                    score -= Math.Min(walk, 12) * 0.1f;
                    break;
                }
                case Chair:
                {
                    TemplateLayout.Step(it.rot, out int dx, out int dz);
                    var faced = s.At(it.x + dx, it.z + dz);
                    int spotOf = g.In(it.x, it.z) ? s.spot[it.x, it.z] : -1;
                    if (faced != null && (faced.kind == Table || faced.kind == Centre))
                    {
                        score += 10f;
                        why = "table";
                    }
                    else if (spotOf >= 0 && spotOf != i && s.items[spotOf].kind == Bench && faced == s.items[spotOf])
                    {
                        // A stool on a bench's work spot, facing the bench: the worker sits.
                        score += 9f;
                        why = "bench";
                    }
                    else
                    {
                        bool beside = false;
                        foreach (var (x, z) in Ring(it))
                        {
                            var o = s.At(x, z);
                            if (o != null && (o.kind == Table || o.kind == Centre)) beside = true;
                        }
                        if (beside) score += 5f;
                        else if (walls > 0)
                        {
                            score += 2f;
                            why = "wall";
                        }
                    }
                    break;
                }
                case BedSide:
                {
                    bool byHead = false, crowded = false;
                    foreach (var (x, z) in Ring(it))
                    {
                        var bed = s.At(x, z);
                        if (bed == null || bed.kind != Bed) continue;
                        foreach (var (hx, hz) in bed.HeadCells())
                        {
                            if (hx != x || hz != z) continue;
                            byHead = true;
                            // Another table already by that head: leave it for a bed without one.
                            foreach (var (nx, nz) in Beside(hx, hz))
                            {
                                var o = s.At(nx, nz);
                                if (o != null && o != it && o.kind == BedSide) crowded = true;
                            }
                        }
                    }
                    if (byHead)
                    {
                        score += crowded ? 4f : 12f;
                        why = "bedside";
                    }
                    else if (walls > 0)
                    {
                        score += 2f;
                        why = "wall";
                    }
                    break;
                }
                case Storage:
                {
                    // Along a wall, in a run with the other shelves, not in front of a bench's work spot.
                    if (walls > 0)
                    {
                        score += 4f + Math.Min(walls, 4) * 1f;
                        why = "wall";
                    }
                    bool run = false;
                    foreach (var (x, z) in Ring(it))
                    {
                        var o = s.At(x, z);
                        if (o != null && o != it && o.kind == Storage) run = true;
                    }
                    if (run)
                    {
                        score += 4f;
                        if (walls > 0) why = "run";
                    }
                    foreach (var (x, z) in Ring(it))
                        if (g.In(x, z) && s.spot[x, z] >= 0 && s.spot[x, z] != i) score -= 2f;
                    // Near the benches, so the materials are at hand.
                    int benches = 0;
                    for (int j = 0; j < s.items.Count; j++)
                    {
                        var o = s.items[j];
                        if (o != it && o.kind == Bench && InPlace(s, j, o) && Distance(it, o) <= 5f) benches++;
                    }
                    score += Math.Min(benches, 3) * 1.5f;
                    score -= Math.Min(walk, 12) * 0.05f;
                    break;
                }
                case WallHugger:
                {
                    if (walls > 0)
                    {
                        score += 3f + Math.Min(walls, 4) * 1f;
                        why = sides >= 2 ? "corner" : "wall";
                    }
                    break;
                }
                case Decor:
                {
                    if (sides >= 2)
                    {
                        score += 6f;
                        why = "corner";
                    }
                    else if (walls > 0)
                    {
                        score += 2.5f;
                        why = "wall";
                    }
                    else score -= 1f;
                    // Spread the lamps and pots about rather than bunching them, and out of the way of the door.
                    foreach (var (x, z) in Ring(it))
                    {
                        var o = s.At(x, z);
                        if (o != null && o != it && o.kind == Decor) score -= 3f;
                    }
                    score += Math.Min(walk, 12) * 0.1f;
                    break;
                }
                default:
                    score += Math.Min(walls, 3) * 1f;
                    if (walls > 0) why = "wall";
                    break;
            }
            if (serves) why = "serves";
            // Reach from the doors is checked afterwards; a little for staying near where it was.
            score -= Dist(it.x, it.z, it) * 0.02f;
            return score;
        }

        /// <summary>How many cells of the footprint's <paramref name="side"/> (0 north, 1 east, 2 south, 3 west) lean on a wall.</summary>
        private static int SideBacking(State s, Item it, int side)
        {
            int n = 0;
            switch (side)
            {
                case 0:
                    for (int x = it.minX; x < it.minX + it.w; x++) if (s.Backing(x, it.minZ + it.h)) n++;
                    break;
                case 2:
                    for (int x = it.minX; x < it.minX + it.w; x++) if (s.Backing(x, it.minZ - 1)) n++;
                    break;
                case 1:
                    for (int z = it.minZ; z < it.minZ + it.h; z++) if (s.Backing(it.minX + it.w, z)) n++;
                    break;
                default:
                    for (int z = it.minZ; z < it.minZ + it.h; z++) if (s.Backing(it.minX - 1, z)) n++;
                    break;
            }
            return n;
        }

        /// <summary>How many cells of the footprint's <paramref name="side"/> lean on furniture nobody walks through and nobody works at from there (another bench's back, a shelf).</summary>
        private static int SideLeaning(State s, Item it, int side)
        {
            int n = 0;
            foreach (var (x, z) in SideCells(it, side))
            {
                var o = s.At(x, z);
                if (o == null || o == it || o.passable || o.kind == Table || o.kind == Centre || o.kind == Bed || o.kind == Chair) continue;
                if (s.spot[x, z] >= 0) continue;
                n++;
            }
            return n;
        }

        /// <summary>How many cells of the footprint's <paramref name="side"/> have open floor beyond them: no wall, nothing standing, nobody's work spot, not a doorway.</summary>
        private static int BackFree(State s, Item it, int side)
        {
            int n = 0;
            foreach (var (x, z) in SideCells(it, side))
                if (s.g.Floor(x, z) && !s.g.IsDoorInside(x, z) && s.At(x, z) == null && s.spot[x, z] < 0) n++;
            return n;
        }

        /// <summary>The cells just beyond the footprint on one side (0 north, 1 east, 2 south, 3 west).</summary>
        private static IEnumerable<(int x, int z)> SideCells(Item it, int side)
        {
            switch (side)
            {
                case 0:
                    for (int x = it.minX; x < it.minX + it.w; x++) yield return (x, it.minZ + it.h);
                    break;
                case 2:
                    for (int x = it.minX; x < it.minX + it.w; x++) yield return (x, it.minZ - 1);
                    break;
                case 1:
                    for (int z = it.minZ; z < it.minZ + it.h; z++) yield return (it.minX + it.w, z);
                    break;
                default:
                    for (int z = it.minZ; z < it.minZ + it.h; z++) yield return (it.minX - 1, z);
                    break;
            }
        }

        /// <summary>Whether item <paramref name="j"/> stands in the room now: fixed, left standing, or placed already.</summary>
        private static bool InPlace(State s, int j, Item o)
        {
            if (o.fixedPlace || o.stranded) return true;
            for (int x = o.minX; x < o.minX + o.w; x++)
                for (int z = o.minZ; z < o.minZ + o.h; z++)
                    if (s.g.In(x, z) && s.occ[x, z] == j) return true;
            return false;
        }

        /// <summary>The distance between two footprints' middles.</summary>
        public static float Distance(Item a, Item b)
        {
            float ax = a.minX + a.w / 2f, az = a.minZ + a.h / 2f, bx = b.minX + b.w / 2f, bz = b.minZ + b.h / 2f;
            return (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
        }

        /// <summary>
        /// The walk from the nearest door to where the piece is used: the cell beyond a bed's feet, a bench's work spot,
        /// else the piece itself. Straight-line cells, not the path: enough to tell the near end of a room from the far.
        /// </summary>
        public static int DoorWalk(Grid g, Item it)
        {
            if (g.doorInside.Count == 0) return 0;
            int ux = it.x, uz = it.z;
            if (it.kind == Bed)
            {
                var feet = it.rot == 0 ? (it.x, it.minZ + it.h) : it.rot == 2 ? (it.x, it.minZ - 1) : it.rot == 1 ? (it.minX + it.w, it.z) : (it.minX - 1, it.z);
                ux = feet.Item1;
                uz = feet.Item2;
            }
            else
            {
                var spots = it.SpotCells();
                if (spots.Count > 0)
                {
                    ux = spots[0].x;
                    uz = spots[0].z;
                }
            }
            int best = int.MaxValue;
            foreach (var (dx, dz) in g.doorInside) best = Math.Min(best, Math.Abs(dx - ux) + Math.Abs(dz - uz));
            return best;
        }

        /// <summary>The side of a bench away from its work spot (where the worker stands), or its facing when it has none.</summary>
        public static int BackOf(Item it)
        {
            var spots = it.SpotCells();
            if (spots.Count == 0) return it.rot;
            var (sx, sz) = spots[0];
            if (sz >= it.minZ + it.h) return 2;
            if (sz < it.minZ) return 0;
            if (sx >= it.minX + it.w) return 3;
            if (sx < it.minX) return 1;
            return it.rot;
        }

        private static IEnumerable<(int x, int z)> Beside(int x, int z)
        {
            yield return (x + 1, z);
            yield return (x - 1, z);
            yield return (x, z + 1);
            yield return (x, z - 1);
        }

        // ---------------- Reach ----------------

        /// <summary>The floor people can walk to from the doors, with every item but <paramref name="skip"/> in place.</summary>
        private static bool[,] Reach(State s, int skip)
        {
            var g = s.g;
            var reach = new bool[g.w, g.h];
            var queue = new Queue<(int, int)>();
            foreach (var (dx, dz) in g.doorInside)
            {
                if (!g.Floor(dx, dz) || reach[dx, dz]) continue;
                reach[dx, dz] = true;
                queue.Enqueue((dx, dz));
            }
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                foreach (var (nx, nz) in Beside(x, z))
                {
                    if (!g.Floor(nx, nz) || reach[nx, nz]) continue;
                    int o = s.occ[nx, nz];
                    if (o >= 0 && o != skip && !s.items[o].passable) continue;
                    reach[nx, nz] = true;
                    queue.Enqueue((nx, nz));
                }
            }
            return reach;
        }

        /// <summary>
        /// The placed item most in the way: one that cuts a door off from the others, else one nobody can get to
        /// (a work spot or a cell beside it off the reached floor). -1 when all is well.
        /// </summary>
        private static int WorstPlaced(State s, out string problem)
        {
            problem = null;
            var reach = Reach(s, -1);
            var g = s.g;
            // A door cut off: the movable item whose lifting joins the most floor back.
            bool cut = false;
            foreach (var (dx, dz) in g.doorInside)
                if (g.Floor(dx, dz) && !Joined(s, reach, dx, dz)) cut = true;
            if (cut)
            {
                int bestI = -1, bestGain = 0;
                int now = Count(reach);
                for (int i = 0; i < s.items.Count; i++)
                {
                    var it = s.items[i];
                    if (it.fixedPlace || it.stranded || it.passable) continue;
                    int gain = Count(Reach(s, i)) - now;
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestI = i;
                    }
                }
                if (bestI >= 0)
                {
                    problem = "cuts a door off";
                    return bestI;
                }
            }
            for (int i = 0; i < s.items.Count; i++)
            {
                var it = s.items[i];
                if (it.fixedPlace || it.stranded) continue;
                var spots = it.SpotCells();
                bool fine = true;
                if (spots.Count > 0)
                {
                    foreach (var (sx, sz) in spots)
                        if (!g.In(sx, sz) || !reach[sx, sz]) fine = false;
                }
                else fine = BesideReached(it, reach, g);
                if (!fine)
                {
                    problem = "out of reach";
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Whether the floor inside this door joins the rest of the reached floor (the first door's, when there are several).</summary>
        private static bool Joined(State s, bool[,] reach, int dx, int dz)
        {
            // Reach starts from every door at once, so a door on its own island is found by walking from it alone.
            var g = s.g;
            if (g.doorInside.Count < 2) return true;
            var first = g.doorInside[0];
            if (first.x == dx && first.z == dz) return true;
            var own = new bool[g.w, g.h];
            var queue = new Queue<(int, int)>();
            own[dx, dz] = true;
            queue.Enqueue((dx, dz));
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                if (x == first.x && z == first.z) return true;
                foreach (var (nx, nz) in Beside(x, z))
                {
                    if (!g.Floor(nx, nz) || own[nx, nz]) continue;
                    int o = s.occ[nx, nz];
                    if (o >= 0 && !s.items[o].passable) continue;
                    own[nx, nz] = true;
                    queue.Enqueue((nx, nz));
                }
            }
            return !g.Floor(first.x, first.z);
        }

        private static int Count(bool[,] a)
        {
            int n = 0;
            foreach (bool b in a) if (b) n++;
            return n;
        }

        // ---------------- Checks the tests and the game share ----------------

        /// <summary>Whether two items' result footprints overlap.</summary>
        public static bool Overlap(Item a, Item b) =>
            a.minX < b.minX + b.w && b.minX < a.minX + a.w && a.minZ < b.minZ + b.h && b.minZ < a.minZ + a.h;

        /// <summary>How many items go somewhere else.</summary>
        public static int Moves(List<Item> items)
        {
            int n = 0;
            foreach (var it in items) if (!it.fixedPlace && !it.stranded && it.moved) n++;
            return n;
        }

        /// <summary>
        /// The order to carry the moves out in: an item whose new cells nobody still stands on goes first; the rest
        /// follow as their cells free up. Items left over stand in a ring (each on another's new cells): the first of
        /// them is set aside first (<paramref name="setAside"/>) so the ring can turn.
        /// </summary>
        public static List<int> MoveOrder(List<Item> items, out List<int> setAside)
        {
            setAside = new List<int>();
            var order = new List<int>();
            var left = new List<int>();
            for (int i = 0; i < items.Count; i++)
                if (!items[i].fixedPlace && !items[i].stranded && items[i].moved) left.Add(i);
            var standing = new Dictionary<int, (int minX, int minZ, int w, int h)>();
            foreach (int i in left)
            {
                var it = items[i];
                TemplateLayout.OccupiedRect(it.curX, it.curZ, it.rotatable ? TemplateMath.Norm(it.curRot) : 0, it.sizeX, it.sizeZ, out int mx, out int mz, out int w, out int h);
                standing[i] = (mx, mz, w, h);
            }
            while (left.Count > 0)
            {
                int pick = -1;
                foreach (int i in left)
                {
                    bool clear = true;
                    foreach (int j in left)
                    {
                        if (i == j) continue;
                        var st = standing[j];
                        var it = items[i];
                        if (it.minX < st.minX + st.w && st.minX < it.minX + it.w && it.minZ < st.minZ + st.h && st.minZ < it.minZ + it.h)
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (clear)
                    {
                        pick = i;
                        break;
                    }
                }
                if (pick < 0)
                {
                    // A ring: set the first aside, which frees its cells for the next.
                    pick = left[0];
                    setAside.Add(pick);
                }
                order.Add(pick);
                left.Remove(pick);
                standing.Remove(pick);
            }
            return order;
        }
    }
}
