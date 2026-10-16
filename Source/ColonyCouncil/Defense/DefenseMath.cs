using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Reading a map for its defense, with no game types (the survey fills a grid from the map; the tests from strings):
    /// how far every cell is from home along the easiest way in, which map edges raiders can reach the colony from, the
    /// routes they walk, the narrow places on those routes (choke points: a pass between hills, a gap between rock and
    /// water, a bridge, a gate in the colony's own wall), and whether the colony is walled in.
    /// <para>
    /// The way in is worked out once from home outward (Dijkstra over the walkable cells, doors costing extra, as raiders
    /// would have to break them), then every reachable edge cell walks downhill to home. Raids come from a random
    /// reachable edge cell, so the share of those walks that pass a place is about the share of raids that will.
    /// </para>
    /// </summary>
    public static class DefenseMath
    {
        // What a cell is, as the survey reads it.
        public const byte Open = 0, Rock = 1, Water = 2, Wall = 3, Door = 4, Other = 5, Bridge = 6, Unknown = 7;
        /// <summary>Not a cell kind: the end of a span at the map's edge.</summary>
        public const byte Edge = 8;

        public const int Ortho = 13, Diag = 18, DoorCost = 400;
        /// <summary>The widest place still counted as a choke point.</summary>
        public const int MaxChokeWidth = 13;
        /// <summary>How far each side of a cell a gap is measured before it counts as open country.</summary>
        public const int MaxSpanSide = 14;
        /// <summary>Choke points are looked for between these many steps out from home.</summary>
        public const int MinOut = 1, MaxOut = 90;

        public class MapGrid
        {
            public readonly int w, h;
            public readonly byte[] kind;
            /// <summary>Extra cost to walk the cell (mud, shallow water, things in the way), in the game's path cost units.</summary>
            public readonly int[] cost;
            public readonly bool[] home;
            /// <summary>Where people sleep and live: what "walled in" protects. Falls back to home.</summary>
            public readonly bool[] core;
            public int centerX, centerZ;
            /// <summary>Nothing can be built this close to the map's edge (the game's own margin): no choke point there.</summary>
            public int noBuildEdge;

            public MapGrid(int w, int h)
            {
                this.w = w;
                this.h = h;
                kind = new byte[w * h];
                cost = new int[w * h];
                home = new bool[w * h];
                core = new bool[w * h];
                centerX = w / 2;
                centerZ = h / 2;
            }

            public int Index(int x, int z) => z * w + x;
            public bool In(int x, int z) => x >= 0 && z >= 0 && x < w && z < h;
            public bool Passable(int i) => kind[i] == Open || kind[i] == Door || kind[i] == Bridge;
        }

        public class Route
        {
            /// <summary>Cell indices from the map edge to home.</summary>
            public List<int> cells = new List<int>();
            /// <summary>The edge it starts from: 0 north, 1 east, 2 south, 3 west.</summary>
            public int side;
        }

        public class Choke
        {
            /// <summary>The middle of the gap.</summary>
            public int x, z;
            public int width;
            /// <summary>The side raiders come from at this gap (0 north, 1 east, 2 south, 3 west): a gate here faces it.</summary>
            public int facing;
            /// <summary>The walkable cells across the gap.</summary>
            public List<int> span = new List<int>();
            /// <summary>What closes it at either end (west or south first): Rock, Water, Wall, Other or Unknown.</summary>
            public byte endA, endB;
            public bool bridge;
            /// <summary>Gap cells that are doors already.</summary>
            public int doors;
            /// <summary>The share of raids whose way in passes here (0..1), and how many of the sampled walks did.</summary>
            public float share;
            public int routes;
            /// <summary>Steps from home along the way in.</summary>
            public int fromHome;
            /// <summary>Which edges the raids passing here come from (bit per side).</summary>
            public int sides;
        }

        public class Result
        {
            public int w, h;
            /// <summary>Cost of the easiest way from each cell to home; -1 where none.</summary>
            public int[] dist;
            /// <summary>Walks from the edge passing each cell.</summary>
            public int[] flow;
            /// <summary>Edge cells per side (N, E, S, W) people can stand on, and those with a way to home.</summary>
            public int[] sideWalkable = new int[4], sideReach = new int[4];
            public int walks;
            /// <summary>A few of the walks, for the map: up to three per side.</summary>
            public List<Route> shown = new List<Route>();
            public List<Choke> chokes = new List<Choke>();
            /// <summary>No way from any map edge to where people live without passing a door.</summary>
            public bool enclosed;
            /// <summary>Places the home area can be entered without a door (clusters of open boundary).</summary>
            public int openings;
            /// <summary>Raiders can't reach home at all (walled in by rock and water, or no home yet).</summary>
            public bool unreachable;
            /// <summary>Where the reachable edge lies, seen from the colony's middle: the mean direction (degrees, 0 = north, clockwise) and how one-sided it is (0 all round .. 1 one way).</summary>
            public float angle, concentration;
            /// <summary>The walks that pass no choke point narrower than <see cref="MaxChokeWidth"/> (open country all the way).</summary>
            public float openShare;

            public int TotalReach => sideReach[0] + sideReach[1] + sideReach[2] + sideReach[3];
        }

        private static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DZ = { 1, 0, -1, 0, 1, -1, -1, 1 };

        /// <summary>The whole reading. <paramref name="maxWalks"/> caps the edge cells walked (evenly spread).</summary>
        public static Result Analyse(MapGrid m, int maxWalks = 600)
        {
            var r = new Result { w = m.w, h = m.h };
            int n = m.w * m.h;
            // Walled in: where people live can't be reached from any map edge without passing a door. Then the ways in
            // end inside the walls, so the gates themselves show as the narrow places; else they end at the home area.
            var inner = Inner(m);
            r.enclosed = true;
            for (int side = 0; side < 4 && r.enclosed; side++)
                foreach (int i in EdgeCells(m, side))
                    if (inner[i])
                    {
                        r.enclosed = false;
                        break;
                    }
            bool anyInner = false;
            for (int i = 0; i < n && !anyInner; i++) anyInner = inner[i];
            if (!anyInner) r.enclosed = false;
            var sources = new bool[n];
            bool anyHome = false;
            for (int i = 0; i < n; i++)
            {
                sources[i] = r.enclosed ? inner[i] : m.home[i];
                anyHome |= sources[i];
            }
            if (!anyHome)
                for (int i = 0; i < n; i++) sources[i] = m.core[i];
            r.dist = Distances(m, sources);
            r.flow = new int[n];
            int minOut = r.enclosed ? 1 : MinOut;

            // The edge: which cells people can stand on, and which have a way in.
            var entries = new List<(int idx, int side)>();
            for (int side = 0; side < 4; side++)
            {
                foreach (int i in EdgeCells(m, side))
                {
                    if (!m.Passable(i)) continue;
                    r.sideWalkable[side]++;
                    if (r.dist[i] < 0) continue;
                    r.sideReach[side]++;
                    entries.Add((i, side));
                }
            }
            r.unreachable = entries.Count == 0;
            if (r.unreachable) return r;

            // Which way the open edge lies, seen from the colony.
            double sx = 0, sz = 0;
            foreach (var (i, _) in entries)
            {
                double dx = i % m.w - m.centerX, dz = i / m.w - m.centerZ;
                double len = System.Math.Sqrt(dx * dx + dz * dz);
                if (len < 1) continue;
                sx += dx / len;
                sz += dz / len;
            }
            sx /= entries.Count;
            sz /= entries.Count;
            r.concentration = (float)System.Math.Sqrt(sx * sx + sz * sz);
            r.angle = (float)((System.Math.Atan2(sx, sz) * 180.0 / System.Math.PI + 360.0) % 360.0);

            // Walk from the edge to home, evenly spread over the reachable edge.
            int stride = System.Math.Max(1, entries.Count / System.Math.Max(1, maxWalks));
            var candidates = new List<(int route, int cell, int width, int axis, List<int> span, byte a, byte b, int outX, int outZ, int fromHome, int side)>();
            var perSide = new List<Route>[4];
            for (int s = 0; s < 4; s++) perSide[s] = new List<Route>();
            int budget = 600000;
            int routeId = 0;
            int open = 0;
            for (int e = 0; e < entries.Count && budget > 0; e += stride)
            {
                var (start, side) = entries[e];
                var route = Walk(m, r.dist, start, ref budget);
                if (route == null) continue;
                route.side = side;
                foreach (int c in route.cells) r.flow[c]++;
                r.walks++;
                perSide[side].Add(route);
                var cand = Narrowest(m, route, sources, minOut);
                if (cand.width > 0) candidates.Add((routeId, cand.cell, cand.width, cand.axis, cand.span, cand.a, cand.b, cand.outX, cand.outZ, cand.fromHome, side));
                else open++;
                routeId++;
            }
            r.openShare = r.walks > 0 ? open / (float)r.walks : 1f;
            for (int s = 0; s < 4; s++)
            {
                var list = perSide[s];
                if (list.Count == 0) continue;
                int take = System.Math.Min(3, list.Count);
                for (int k = 0; k < take; k++) r.shown.Add(list[(int)((k + 0.5f) * list.Count / take)]);
            }

            // Gaps that several walks pass are one choke point.
            candidates.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.fromHome.CompareTo(b.fromHome));
            var groups = new List<(Choke choke, HashSet<int> routes, HashSet<int> cells, int outX, int outZ)>();
            foreach (var c in candidates)
            {
                int found = -1;
                for (int g = 0; g < groups.Count && found < 0; g++)
                {
                    var grp = groups[g];
                    if (System.Math.Abs(grp.choke.x - c.cell % m.w) <= 4 && System.Math.Abs(grp.choke.z - c.cell / m.w) <= 4) found = g;
                    else
                        foreach (int sc in c.span)
                            if (grp.cells.Contains(sc))
                            {
                                found = g;
                                break;
                            }
                }
                if (found >= 0)
                {
                    var grp = groups[found];
                    grp.routes.Add(c.route);
                    grp.choke.sides |= 1 << c.side;
                    groups[found] = (grp.choke, grp.routes, grp.cells, grp.outX + c.outX, grp.outZ + c.outZ);
                    continue;
                }
                var choke = new Choke
                {
                    width = c.width,
                    span = c.span,
                    endA = c.a,
                    endB = c.b,
                    fromHome = c.fromHome,
                    sides = 1 << c.side
                };
                int mid = c.span[c.span.Count / 2];
                choke.x = mid % m.w;
                choke.z = mid / m.w;
                foreach (int sc in c.span)
                {
                    if (m.kind[sc] == Bridge) choke.bridge = true;
                    if (m.kind[sc] == Door) choke.doors++;
                }
                // The span runs across the way in; the gate faces along it, toward where the raiders come from.
                choke.facing = c.axis == 0 ? (c.outZ >= 0 ? 0 : 2) : (c.outX >= 0 ? 1 : 3);
                groups.Add((choke, new HashSet<int> { c.route }, new HashSet<int>(c.span), c.outX, c.outZ));
            }
            foreach (var g in groups)
            {
                g.choke.routes = g.routes.Count;
                g.choke.share = r.walks > 0 ? g.routes.Count / (float)r.walks : 0f;
                // Many walks: their summed outward direction decides, along the span's own axis.
                bool alongZ = g.choke.facing == 0 || g.choke.facing == 2;
                g.choke.facing = alongZ ? (g.outZ >= 0 ? 0 : 2) : (g.outX >= 0 ? 1 : 3);
                r.chokes.Add(g.choke);
            }
            r.chokes.Sort((a, b) => System.Math.Abs(a.share - b.share) > 0.001f ? b.share.CompareTo(a.share) : a.width.CompareTo(b.width));
            if (r.chokes.Count > 8) r.chokes.RemoveRange(8, r.chokes.Count - 8);

            if (!r.enclosed) Openings(m, entries, r);
            return r;
        }

        /// <summary>The cells of one map edge (0 north, 1 east, 2 south, 3 west), corners on the north and south edges only.</summary>
        public static IEnumerable<int> EdgeCells(MapGrid m, int side)
        {
            switch (side)
            {
                case 0:
                    for (int x = 0; x < m.w; x++) yield return m.Index(x, m.h - 1);
                    break;
                case 2:
                    for (int x = 0; x < m.w; x++) yield return m.Index(x, 0);
                    break;
                case 1:
                    for (int z = 1; z < m.h - 1; z++) yield return m.Index(m.w - 1, z);
                    break;
                default:
                    for (int z = 1; z < m.h - 1; z++) yield return m.Index(0, z);
                    break;
            }
        }

        /// <summary>A diagonal step needs both cells beside it open (no cutting round a corner), as in the game.</summary>
        private static bool CanStep(MapGrid m, int x, int z, int d)
        {
            int nx = x + DX[d], nz = z + DZ[d];
            if (!m.In(nx, nz) || !m.Passable(m.Index(nx, nz))) return false;
            if (d < 4) return true;
            return m.Passable(m.Index(x + DX[d], z)) && m.Passable(m.Index(x, z + DZ[d]));
        }

        private static int StepCost(MapGrid m, int to, int d) =>
            (d < 4 ? Ortho : Diag) + System.Math.Max(0, System.Math.Min(m.cost[to], 600)) + (m.kind[to] == Door ? DoorCost : 0);

        /// <summary>The cost of the easiest way from every cell to the sources (Dijkstra from all of them at once); -1 where there's none.</summary>
        public static int[] Distances(MapGrid m, bool[] sources)
        {
            int n = m.w * m.h;
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = -1;
            var heap = new MinHeap(1024);
            for (int i = 0; i < n; i++)
            {
                if (!sources[i] || !m.Passable(i)) continue;
                dist[i] = 0;
                heap.Push(0, i);
            }
            var done = new bool[n];
            while (heap.Count > 0)
            {
                heap.Pop(out long key, out int i);
                if (done[i]) continue;
                done[i] = true;
                int x = i % m.w, z = i / m.w;
                for (int d = 0; d < 8; d++)
                {
                    if (!CanStep(m, x, z, d)) continue;
                    int j = m.Index(x + DX[d], z + DZ[d]);
                    if (done[j]) continue;
                    // The cost of stepping from j onto i: walking out of j toward home.
                    int nd = dist[i] + StepCost(m, i, d);
                    if (dist[j] < 0 || nd < dist[j])
                    {
                        dist[j] = nd;
                        heap.Push(nd, j);
                    }
                }
            }
            return dist;
        }

        /// <summary>Downhill from an edge cell to home; null if it gets stuck (it can't, on a proper distance field).</summary>
        public static Route Walk(MapGrid m, int[] dist, int start, ref int budget)
        {
            var route = new Route();
            int cur = start;
            route.cells.Add(cur);
            while (dist[cur] > 0 && budget-- > 0)
            {
                int x = cur % m.w, z = cur / m.w;
                int best = -1, bestDist = dist[cur];
                for (int d = 0; d < 8; d++)
                {
                    if (!CanStep(m, x, z, d)) continue;
                    int j = m.Index(x + DX[d], z + DZ[d]);
                    if (dist[j] >= 0 && dist[j] < bestDist)
                    {
                        best = j;
                        bestDist = dist[j];
                    }
                }
                if (best < 0) return null;
                cur = best;
                route.cells.Add(cur);
            }
            return dist[cur] == 0 ? route : null;
        }

        /// <summary>
        /// The narrowest place on a walk, between <paramref name="minOut"/> and <see cref="MaxOut"/> steps out from home
        /// and outside it: the walkable cells across the way (along x or z, whichever is shorter), closed at both ends by
        /// something nobody walks through or the map's edge. Width 0: open country all the way.
        /// </summary>
        public static (int cell, int width, int axis, List<int> span, byte a, byte b, int outX, int outZ, int fromHome) Narrowest(MapGrid m, Route route, bool[] home, int minOut)
        {
            int last = route.cells.Count - 1;
            int bestWidth = int.MaxValue, bestK = -1, bestAxis = 0;
            for (int k = last; k >= 0; k--)
            {
                int steps = last - k;
                if (steps < minOut) continue;
                if (steps > MaxOut) break;
                int c = route.cells[k];
                if (home[c]) continue;
                int cx = c % m.w, cz = c / m.w;
                if (cx < m.noBuildEdge || cz < m.noBuildEdge || cx >= m.w - m.noBuildEdge || cz >= m.h - m.noBuildEdge) continue;
                int wx = Span(m, c, 0, out bool boundX);
                int wz = Span(m, c, 1, out bool boundZ);
                int width = int.MaxValue, axis = 0;
                if (boundX && wx < width)
                {
                    width = wx;
                    axis = 0;
                }
                if (boundZ && wz < width)
                {
                    width = wz;
                    axis = 1;
                }
                // Nearest home wins a tie: a gap closer in is easier to hold.
                if (width < bestWidth)
                {
                    bestWidth = width;
                    bestK = k;
                    bestAxis = axis;
                }
            }
            if (bestK < 0 || bestWidth > MaxChokeWidth) return (-1, 0, 0, null, 0, 0, 0, 0, 0);
            int cell = route.cells[bestK];
            var span = SpanCells(m, cell, bestAxis, out byte endA, out byte endB);
            // Outward: toward the edge the walk came from, a few steps back along it.
            int back = route.cells[System.Math.Max(0, bestK - 6)];
            int outX = back % m.w - cell % m.w, outZ = back / m.w - cell / m.w;
            return (cell, bestWidth, bestAxis, span, endA, endB, outX, outZ, last - bestK);
        }

        /// <summary>
        /// Walkable cells in a line through <paramref name="c"/> (axis 0 along x, 1 along z); bounded when both ends meet
        /// something closed within reach. The map's edge doesn't close a gap: nobody can build out to it.
        /// </summary>
        public static int Span(MapGrid m, int c, int axis, out bool bounded)
        {
            int x = c % m.w, z = c / m.w;
            int count = 1;
            bool closedA = false, closedB = false;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int k = 1; k <= MaxSpanSide + 1; k++)
                {
                    int nx = axis == 0 ? x + sign * k : x, nz = axis == 1 ? z + sign * k : z;
                    if (!m.In(nx, nz)) break;
                    if (!m.Passable(m.Index(nx, nz)))
                    {
                        if (sign < 0) closedA = true;
                        else closedB = true;
                        break;
                    }
                    if (k > MaxSpanSide) break;
                    count++;
                }
            }
            bounded = closedA && closedB;
            return count;
        }

        private static List<int> SpanCells(MapGrid m, int c, int axis, out byte endA, out byte endB)
        {
            int x = c % m.w, z = c / m.w;
            var list = new List<int> { c };
            endA = endB = Edge;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int k = 1; k <= MaxSpanSide + 1; k++)
                {
                    int nx = axis == 0 ? x + sign * k : x, nz = axis == 1 ? z + sign * k : z;
                    byte end = Edge;
                    bool stop = !m.In(nx, nz);
                    if (!stop)
                    {
                        int j = m.Index(nx, nz);
                        if (!m.Passable(j))
                        {
                            stop = true;
                            end = m.kind[j];
                        }
                        else if (sign < 0) list.Insert(0, j);
                        else list.Add(j);
                    }
                    if (stop)
                    {
                        if (sign < 0) endA = end;
                        else endB = end;
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Where people live and what they reach without passing a door or a wall: the cells round the core, flooded over
        /// open ground. A colony walled in keeps this inside its walls.
        /// </summary>
        public static bool[] Inner(MapGrid m)
        {
            int n = m.w * m.h;
            var seen = new bool[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (!m.core[i] || !m.Passable(i) || m.kind[i] == Door) continue;
                seen[i] = true;
                queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % m.w, z = i / m.w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (!m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (seen[j] || !m.Passable(j) || m.kind[j] == Door) continue;
                    seen[j] = true;
                    queue.Enqueue(j);
                }
            }
            return seen;
        }

        /// <summary>How many separate openings the home area has where raiders walk in without a door (cells within two of each other are one).</summary>
        private static void Openings(MapGrid m, List<(int idx, int side)> entries, Result r)
        {
            int n = m.w * m.h;
            var seen = new bool[n];
            var queue = new Queue<int>();
            foreach (var (i, _) in entries)
            {
                if (m.kind[i] == Door || seen[i]) continue;
                seen[i] = true;
                queue.Enqueue(i);
            }
            var doorway = new List<int>();
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % m.w, z = i / m.w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (!m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (seen[j] || !m.Passable(j) || m.kind[j] == Door) continue;
                    seen[j] = true;
                    // Into the home area from outside it, with no door in the way: an opening. The flood stops there.
                    if (m.home[j] && !m.home[i])
                    {
                        doorway.Add(j);
                        continue;
                    }
                    queue.Enqueue(j);
                }
            }
            var used = new bool[doorway.Count];
            int clusters = 0;
            for (int a = 0; a < doorway.Count; a++)
            {
                if (used[a]) continue;
                clusters++;
                var stack = new Stack<int>();
                stack.Push(a);
                used[a] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int px = doorway[p] % m.w, pz = doorway[p] / m.w;
                    for (int b = 0; b < doorway.Count; b++)
                    {
                        if (used[b]) continue;
                        int bx = doorway[b] % m.w, bz = doorway[b] / m.w;
                        if (System.Math.Abs(bx - px) <= 2 && System.Math.Abs(bz - pz) <= 2)
                        {
                            used[b] = true;
                            stack.Push(b);
                        }
                    }
                }
            }
            r.openings = clusters;
        }

        /// <summary>The compass point (0 north .. 7 north-west, clockwise) nearest an angle in degrees.</summary>
        public static int Compass(float angle) => (int)System.Math.Round(((angle % 360f) + 360f) % 360f / 45f) % 8;

        /// <summary>The compass point of a facing (0 north, 1 east, 2 south, 3 west).</summary>
        public static int CompassOfFacing(int facing) => (TemplateMath.Norm(facing) * 2) % 8;

        /// <summary>A small binary heap of (cost, cell), lowest cost first.</summary>
        public class MinHeap
        {
            private long[] keys;
            private int[] vals;
            public int Count;

            public MinHeap(int capacity)
            {
                keys = new long[capacity];
                vals = new int[capacity];
            }

            public void Push(long key, int val)
            {
                if (Count == keys.Length)
                {
                    System.Array.Resize(ref keys, Count * 2);
                    System.Array.Resize(ref vals, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= key) break;
                    keys[i] = keys[p];
                    vals[i] = vals[p];
                    i = p;
                }
                keys[i] = key;
                vals[i] = val;
            }

            public void Pop(out long key, out int val)
            {
                key = keys[0];
                val = vals[0];
                Count--;
                if (Count == 0) return;
                long k = keys[Count];
                int v = vals[Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= Count) break;
                    int c = l + 1 < Count && keys[l + 1] < keys[l] ? l + 1 : l;
                    if (keys[c] >= k) break;
                    keys[i] = keys[c];
                    vals[i] = vals[c];
                    i = c;
                }
                keys[i] = k;
                vals[i] = v;
            }
        }
    }
}
