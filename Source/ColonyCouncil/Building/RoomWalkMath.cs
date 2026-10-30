using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// How a room walks and how its floor is used, as plain integer rules with no game types (Building/RoomArrangeMath.cs
    /// weighs an arrangement with them; the tests run them over drawn rooms). People come in at the doors and walk to what
    /// they use, and the shortest ways are traced with the game's own costs: a chair or a bedroll is crossed, slowly, a
    /// work spot is someone's standing place half the time, a wall or a solid piece is not crossed at all. An arrangement
    /// is the worse for a work spot on someone else's way (the worker stands in the walkers' path), a slow piece in the
    /// aisle, a main way one cell wide between two solid things (two people squeeze past each other), floor nobody can
    /// reach, and dead-end cells that serve nothing. The biggest open rectangle of free floor is what is left for a
    /// gathering, or for the next piece of furniture.
    /// </summary>
    public static class RoomWalkMath
    {
        /// <summary>A cell nobody crosses (a wall, a solid piece), as a step cost.</summary>
        public const int Blocked = -1;
        public const int Far = int.MaxValue;
        /// <summary>Entering a cell someone works on costs this many steps: half the time they stand there.</summary>
        public const int SpotCost = 2;
        /// <summary>Through traffic between two doors counts this much on each cell of its way.</summary>
        public const int ThroughWeight = 2;

        /// <summary>A passable piece's path cost (a chair's 30, a bedroll's 42), as extra steps for the one crossing it.</summary>
        public static int StepCost(int pathCost) => 1 + Math.Max(0, pathCost) / 20;

        /// <summary>What a cell is to a walker.</summary>
        public struct Cell
        {
            /// <summary>Steps to enter it; <see cref="Blocked"/> for a wall or a solid piece.</summary>
            public int cost;
            /// <summary>A piece stands on it that people cross (a chair, a bedroll).</summary>
            public bool piece;
            /// <summary>Someone's work spot.</summary>
            public bool spot;
            /// <summary>The cell inside a door.</summary>
            public bool door;

            public bool Passable => cost >= 0;
            /// <summary>Bare floor: nothing on it, nobody's spot, not a doorway.</summary>
            public bool Free => cost >= 0 && !piece && !spot && !door;

            public static readonly Cell Wall = new Cell { cost = Blocked };
            public static readonly Cell Floor = new Cell { cost = 1 };
        }

        /// <summary>A cell people walk to: a bench's work spot, the foot of a bed, a side of the table, a side of a shelf.</summary>
        public class Use
        {
            public int x, z;
            /// <summary>How many times a day people walk here (0: nobody walks here on purpose, it only has to be reachable).</summary>
            public float weight;
            /// <summary>Someone stands here while the piece is used: on another's way, they are in it.</summary>
            public bool standing;
            /// <summary>The item it belongs to.</summary>
            public int owner = -1;
        }

        /// <summary>What the measuring found.</summary>
        public class Review
        {
            /// <summary>Every door can be walked to from every other.</summary>
            public bool doorsJoined = true;
            /// <summary>Cells people need that can't be walked to from a door.</summary>
            public int unreached;
            /// <summary>Work spots that lie on someone else's way.</summary>
            public int inWay;
            /// <summary>Cells on a way with a slow piece on them (a chair in the aisle).</summary>
            public int slowed;
            /// <summary>Cells on a main way with solid things on both sides: one person at a time.</summary>
            public int squeezes;
            /// <summary>The main ways' width at their narrowest (1 to 3; 0 when there is no main way).</summary>
            public int narrowest;
            /// <summary>Bare floor cells reached that lead nowhere (one passable neighbour or none), serving nothing.</summary>
            public int deadEnds;
            /// <summary>Bare floor cells nobody can walk to.</summary>
            public int lostFloor;
            /// <summary>The biggest rectangle of open floor people can walk to, in cells.</summary>
            public int open;
            /// <summary>Everyday walking: each use's steps from the nearest door, times how often people go there.</summary>
            public float walk;
            /// <summary>The longest everyday walk from a door, in steps.</summary>
            public int longestWalk;
        }

        private static readonly (int dx, int dz)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>
        /// The steps from the nearest of the <paramref name="sources"/> to every cell (<see cref="Far"/> where none leads),
        /// and the cell each was reached from ((-1,-1) for a source or an unreached cell).
        /// </summary>
        public static int[,] Distances(int w, int h, IEnumerable<(int x, int z)> sources, Func<int, int, Cell> cell, out (int x, int z)[,] from)
        {
            var dist = new int[w, h];
            from = new (int, int)[w, h];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    dist[x, z] = Far;
                    from[x, z] = (-1, -1);
                }
            var queue = new SortedSet<(int d, int x, int z)>();
            foreach (var (sx, sz) in sources)
            {
                if (sx < 0 || sz < 0 || sx >= w || sz >= h || !cell(sx, sz).Passable || dist[sx, sz] == 0) continue;
                dist[sx, sz] = 0;
                queue.Add((0, sx, sz));
            }
            while (queue.Count > 0)
            {
                var (d, x, z) = queue.Min;
                queue.Remove(queue.Min);
                if (d > dist[x, z]) continue;
                foreach (var (dx, dz) in Dirs)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    var c = cell(nx, nz);
                    if (!c.Passable) continue;
                    int nd = d + c.cost;
                    if (nd >= dist[nx, nz]) continue;
                    dist[nx, nz] = nd;
                    from[nx, nz] = (x, z);
                    queue.Add((nd, nx, nz));
                }
            }
            return dist;
        }

        /// <summary>
        /// Measures a room: <paramref name="cell"/> tells what each cell is, <paramref name="doors"/> are the cells inside the
        /// doors, <paramref name="uses"/> where people go. With <paramref name="wide"/> (a busy room: many pieces or more than
        /// one door) every way is a main way and a one-cell squeeze on any of them counts; otherwise only the ways two or
        /// more walks share.
        /// </summary>
        public static Review Measure(int w, int h, Func<int, int, Cell> cell, List<(int x, int z)> doors, List<Use> uses, bool wide)
        {
            var r = new Review();
            if (w <= 0 || h <= 0 || doors == null || doors.Count == 0) return r;
            bool In(int x, int z) => x >= 0 && z >= 0 && x < w && z < h;
            bool IsDoor(int x, int z)
            {
                foreach (var d in doors) if (d.x == x && d.z == z) return true;
                return false;
            }
            var dist = Distances(w, h, doors, cell, out var from);
            var traffic = new int[w, h];
            // Each use's way from the nearest door, the use's own cell not counted.
            foreach (var u in uses)
            {
                if (!In(u.x, u.z) || dist[u.x, u.z] == Far)
                {
                    r.unreached++;
                    continue;
                }
                if (u.weight <= 0f) continue;
                r.walk += u.weight * dist[u.x, u.z];
                r.longestWalk = Math.Max(r.longestWalk, dist[u.x, u.z]);
                var (x, z) = from[u.x, u.z];
                int guard = 0;
                while (x >= 0 && !IsDoor(x, z) && guard++ < w * h)
                {
                    traffic[x, z]++;
                    (x, z) = from[x, z];
                }
            }
            // Through traffic: from each door to each other.
            for (int i = 0; i < doors.Count; i++)
                for (int j = i + 1; j < doors.Count; j++)
                {
                    var di = Distances(w, h, new[] { doors[i] }, cell, out var fi);
                    var (tx, tz) = doors[j];
                    if (!In(tx, tz) || di[tx, tz] == Far)
                    {
                        r.doorsJoined = false;
                        continue;
                    }
                    var (x, z) = fi[tx, tz];
                    int guard = 0;
                    while (x >= 0 && !(x == doors[i].x && z == doors[i].z) && guard++ < w * h)
                    {
                        traffic[x, z] += ThroughWeight;
                        (x, z) = fi[x, z];
                    }
                }
            // Work spots in others' way, slow pieces on the ways, and squeezes along the main ways.
            foreach (var u in uses)
                if (u.standing && In(u.x, u.z) && traffic[u.x, u.z] > 0) r.inWay++;
            int narrowest = 0;
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    int t = traffic[x, z];
                    if (t <= 0) continue;
                    var c = cell(x, z);
                    if (c.piece) r.slowed++;
                    if (t < (wide ? 1 : ThroughWeight)) continue;
                    var (fx, fz) = from[x, z];
                    int dx = fx < 0 ? 1 : Math.Sign(x - fx), dz = fz < 0 ? 0 : Math.Sign(z - fz);
                    if (dx == 0 && dz == 0) dx = 1;
                    // Across the way: the cells to either side of the step.
                    int width = 1;
                    if (In(x + dz, z + dx) && cell(x + dz, z + dx).Passable) width++;
                    if (In(x - dz, z - dx) && cell(x - dz, z - dx).Passable) width++;
                    if (width == 1) r.squeezes++;
                    narrowest = narrowest == 0 ? width : Math.Min(narrowest, width);
                }
            r.narrowest = narrowest;
            // Floor nobody uses: bare floor out of reach, and bare floor that leads nowhere.
            var open = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    var c = cell(x, z);
                    if (!c.Passable || c.piece) continue;
                    bool reached = dist[x, z] != Far;
                    if (!c.Free)
                    {
                        // A work spot or a doorway: purposeful floor, open for a gathering all the same when reached.
                        open[x, z] = reached && !c.door;
                        continue;
                    }
                    if (!reached)
                    {
                        r.lostFloor++;
                        continue;
                    }
                    open[x, z] = true;
                    int ways = 0;
                    foreach (var (dx, dz) in Dirs)
                        if (In(x + dx, z + dz) && cell(x + dx, z + dz).Passable) ways++;
                    if (ways <= 1) r.deadEnds++;
                }
            r.open = LargestRectangle(open, w, h);
            return r;
        }

        /// <summary>The biggest rectangle of true cells, in cells (the histogram way, row by row).</summary>
        public static int LargestRectangle(bool[,] a, int w, int h)
        {
            int best = 0;
            var height = new int[w];
            var stack = new Stack<int>();
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++) height[x] = a[x, z] ? height[x] + 1 : 0;
                stack.Clear();
                for (int x = 0; x <= w; x++)
                {
                    int cur = x == w ? 0 : height[x];
                    while (stack.Count > 0 && height[stack.Peek()] >= cur)
                    {
                        int top = stack.Pop();
                        int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                        best = Math.Max(best, height[top] * (x - left));
                    }
                    stack.Push(x);
                }
            }
            return best;
        }
    }
}
