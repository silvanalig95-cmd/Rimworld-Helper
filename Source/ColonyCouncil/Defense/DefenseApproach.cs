using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Where a plan that raiders walk through goes in front of a defense line (a trap corridor, anything placed "in
    /// front"), with no game types: the way raiders walk up to the gate, the spots along it where the plan's lane lies on
    /// that way and leads straight to the gate, what each spot would cost (walls to build, rock to dig, unexplored ground,
    /// walls standing where raiders walk), and, when none fits, why. The planner then tries the cheapest spots on the
    /// map itself. Facings are 0 north, 1 east, 2 south, 3 west, the side raiders come from.
    /// </summary>
    public static class DefenseApproach
    {
        /// <summary>How far out from the gate the way in is followed.</summary>
        public const int Reach = 24;

        /// <summary>
        /// The way raiders walk up to a line's gate, gate first: from the furthest cell out that the survey's busiest walks
        /// pass (<paramref name="flow"/>; straight out from the gate when none come by), along the easiest way to the gate
        /// over the raiders' side of the line only, then on out along the busiest stream while walks still come that way.
        /// Empty when nothing on that side reaches the gate.
        /// </summary>
        public static List<int> PathToGate(DefenseMath.MapGrid m, int gx, int gz, int facing, int[] flow, int reach = Reach)
        {
            var path = new List<int>();
            if (!m.In(gx, gz)) return path;
            int gate = m.Index(gx, gz);
            int x0 = System.Math.Max(0, gx - reach - 2), x1 = System.Math.Min(m.w - 1, gx + reach + 2);
            int z0 = System.Math.Max(0, gz - reach - 2), z1 = System.Math.Min(m.h - 1, gz + reach + 2);
            bool Front(int i) => i == gate || DefenseLineMath.Ahead(i % m.w - gx, i / m.w - gz, facing) >= 1;
            bool Inside(int x, int z) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
            // The cost of the easiest way from each cell in front to the gate (the gate counts as open: it's the way through).
            var dist = new Dictionary<int, int> { [gate] = 0 };
            var heap = new DefenseMath.MinHeap(256);
            heap.Push(0, gate);
            var done = new HashSet<int>();
            while (heap.Count > 0)
            {
                heap.Pop(out long key, out int i);
                if (!done.Add(i)) continue;
                int x = i % m.w, z = i / m.w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (!Inside(nx, nz) || !m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (done.Contains(j) || !Front(j) || !m.Passable(j)) continue;
                    if (d >= 4 && (!m.Passable(m.Index(nx, z)) || !m.Passable(m.Index(x, nz)))) continue;
                    // Stepping from j onto i, walking toward the gate.
                    int nd = (int)key + DefenseMath.StepCost(m, i == gate ? j : i, d);
                    if (dist.TryGetValue(j, out int have) && have <= nd) continue;
                    dist[j] = nd;
                    heap.Push(nd, j);
                }
            }
            // Where they come from: the furthest cell out in front that carries at least half as many of the survey's
            // walks as the busiest; with no walk coming by, as far out as the ground goes, near the line's own axis.
            int maxFlow = 0;
            if (flow != null && flow.Length == m.w * m.h)
                foreach (var kv in dist)
                    if (kv.Key != gate && flow[kv.Key] > maxFlow) maxFlow = flow[kv.Key];
            int start = -1, startScore = int.MinValue;
            foreach (var kv in dist)
            {
                int i = kv.Key;
                if (i == gate) continue;
                int dx = i % m.w - gx, dz = i / m.w - gz;
                int ahead = DefenseLineMath.Ahead(dx, dz, facing), lat = System.Math.Abs(DefenseLineMath.Lateral(dx, dz, facing));
                if (ahead > reach) continue;
                if (maxFlow > 0 && flow[i] * 2 < maxFlow) continue;
                int score = maxFlow > 0 ? ahead * 8 - lat + flow[i] * 8 / maxFlow : ahead * 4 - lat * 3;
                if (score > startScore)
                {
                    startScore = score;
                    start = i;
                }
            }
            if (start < 0) return path;
            // Downhill from there to the gate.
            int at = start, left = dist.Count + 2;
            var walk = new List<int> { at };
            while (at != gate && left-- > 0)
            {
                int x = at % m.w, z = at / m.w, best = -1, bestD = dist[at];
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (!m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (!dist.TryGetValue(j, out int dj) || dj >= bestD) continue;
                    if (d >= 4 && (!m.Passable(m.Index(nx, z)) || !m.Passable(m.Index(x, nz)))) continue;
                    best = j;
                    bestD = dj;
                }
                if (best < 0) break;
                at = best;
                walk.Add(at);
            }
            if (at != gate) return path;
            for (int k = walk.Count - 1; k >= 0; k--) path.Add(walk[k]);
            // On out along the busiest stream from there, while walks still come that way (round a bend, say).
            if (maxFlow > 0)
            {
                var used = new HashSet<int>(path);
                int cur = path[path.Count - 1];
                for (int guard = 0; guard < reach * 3; guard++)
                {
                    int x = cur % m.w, z = cur / m.w, best = -1, bestF = 0, bestD = -1;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + DX[d], nz = z + DZ[d];
                        if (!m.In(nx, nz)) continue;
                        int j = m.Index(nx, nz);
                        if (used.Contains(j) || !dist.TryGetValue(j, out int dj) || dj <= dist[cur] || flow[j] <= 0) continue;
                        if (flow[j] > bestF || flow[j] == bestF && dj > bestD)
                        {
                            best = j;
                            bestF = flow[j];
                            bestD = dj;
                        }
                    }
                    if (best < 0) break;
                    path.Add(best);
                    used.Add(best);
                    cur = best;
                }
            }
            return path;
        }

        private static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DZ = { 1, 0, -1, 0, 1, -1, -1, 1 };

        // ---------------- Spots ----------------

        /// <summary>Why a spot (or every spot) doesn't fit.</summary>
        public enum Misfit
        {
            None,
            /// <summary>Nothing on the raiders' side reaches the gate.</summary>
            NoPath,
            /// <summary>The ground in front runs out before the plan does.</summary>
            Short,
            /// <summary>Part of it would stand on the line's own wall or gate, or behind it.</summary>
            Crosses,
            /// <summary>Off the map, or in the strip by its edge where nothing can be built.</summary>
            Edge,
            /// <summary>Deep water where a piece or the lane would go.</summary>
            Water,
            /// <summary>A building (the colony's or not) or a door where a piece or the lane would go.</summary>
            Blocked,
            /// <summary>Mostly unexplored ground.</summary>
            Fog,
            /// <summary>Rock where the lane goes, and digging isn't allowed.</summary>
            Rock,
            /// <summary>It would stand beside the way raiders walk, not on it (the way bends, or runs past it).</summary>
            OffWay
        }

        /// <summary>One way to lay the plan: where, turned and mirrored how, and what it takes.</summary>
        public class Spot
        {
            public int anchorX, anchorZ, turn, facing;
            public bool mirror;
            /// <summary>Its inner end lies on the way in, this many steps out from the gate.</summary>
            public int at;
            /// <summary>Walls (and other solid pieces) to build, side walls already there (rock or the colony's), rock to dig, cells unexplored.</summary>
            public int build, rockSides, wallSides, mine, fog;
            /// <summary>Pieces in its lane (traps, what slows raiders): the same wherever it goes.</summary>
            public int lanePieces;
            /// <summary>Of its side walls, those standing already on the left and the right (looking out), and how many each side has.</summary>
            public int leftHave, leftAll, rightHave, rightAll;
            /// <summary>Cells of the way in that run down its lane, and those its walls would stand on.</summary>
            public int onLane, onWalls;
            /// <summary>Cells of its lane the way in uses once its walls stand (-1: not worked out).</summary>
            public int walked = -1;
            public Misfit misfit;
            public float cost;

            public bool Fits => misfit == Misfit.None;
        }

        /// <summary>The weights of a spot's cost: lower is better.</summary>
        public const float BuildCost = 1f, MineCost = 2f, FogCost = 3.5f, OnWallsCost = 4f, LaneGain = 1f, AtCost = 0.6f, TurnCost = 1.5f, PresentGain = 0.25f, WalkedGain = 0.5f, OffPath = 15f;

        /// <summary>Fewer cells of the way in down its lane than this, and the plan stands beside the way, not on it.</summary>
        public const int OnWay = 3;

        /// <summary>
        /// A spot's cost: walls to build, rock to dig (dearer) and unexplored ground (dearer still), walls standing where
        /// raiders now walk, less for every cell of the way in that runs down its lane and every side wall the rock or the
        /// colony gives it; a little for each step further from <paramref name="want"/> cells out, and for turning away
        /// from the line's own facing.
        /// </summary>
        public static float Cost(Spot s, int want, bool turned)
        {
            float c = s.build * BuildCost + s.mine * MineCost + s.fog * FogCost + s.onWalls * OnWallsCost - s.onLane * LaneGain
                      - (s.rockSides + s.wallSides) * PresentGain + System.Math.Abs(s.at - want) * AtCost + (turned ? TurnCost : 0f);
            if (s.onLane < OnWay) c += OffPath;
            if (s.walked >= 0)
            {
                c -= s.walked * WalkedGain;
                if (s.walked < OnWay) c += OffPath;
            }
            return c;
        }

        /// <summary>Template characters raiders walk on: open ground, traps, what slows them, cover, firing positions.</summary>
        public static bool Lane(char c) => c == DefenseLayout.Open || c == DefenseLayout.Trap || c == DefenseLayout.Slow || c == DefenseLayout.Cover || c == DefenseLayout.Firing;

        /// <summary>
        /// Every way to lay template <paramref name="t"/> on the way in (<paramref name="path"/>, gate first): its inner row's
        /// lane cell on each cell of the way from 1 to <paramref name="maxAt"/> steps out, facing the line's way and the way
        /// the path runs there, each lane column, mirrored and not. Each is checked cell by cell against the grid (never on
        /// or behind the line's own cells <paramref name="lineCells"/>, never in the map's no-build strip, deep water or
        /// someone's building) and costed; the ones that fit come first, cheapest first.
        /// </summary>
        public static List<Spot> Spots(DefenseMath.MapGrid m, DefenseLayout.Grid t, int gx, int gz, int facing, IList<int> path, HashSet<int> lineCells,
            int want = 2, int maxAt = 10, bool mayDig = true, int slowCost = 40, int walkChecks = 24)
        {
            var list = new List<Spot>();
            if (t == null || t.W < 1 || t.H < 1 || path == null || path.Count < 2) return list;
            facing = DefenseLineMath.Norm(facing);
            var lanes = new List<int>();
            for (int x = 0; x < t.W; x++)
                if (Lane(t.cells[x, 0])) lanes.Add(x);
            if (lanes.Count == 0) lanes.Add(t.W / 2);
            var pathAt = new Dictionary<int, int>();
            for (int k = 0; k < path.Count; k++) pathAt[path[k]] = k;
            int last = System.Math.Min(maxAt, path.Count - 1);
            var seen = new HashSet<(int, int, int, bool)>();
            for (int k = 1; k <= last; k++)
            {
                int p = path[k];
                int px = p % m.w, pz = p / m.w;
                var facings = new List<int> { facing };
                int far = path[System.Math.Min(path.Count - 1, k + 5)];
                int along = DefenseLineMath.FacingOf(far % m.w - px, far / m.w - pz);
                if (far != p && along != facing && along != (facing + 2) % 4) facings.Add(along);
                foreach (int f in facings)
                    foreach (int lane in lanes)
                        for (int mi = 0; mi < 2; mi++)
                        {
                            bool mirror = mi == 1;
                            DefenseLayout.FaceAt(t, lane, 0, mirror, f, px, pz, out int turn, out int ax, out int az);
                            if (!seen.Add((ax, az, turn, mirror))) continue;
                            var s = Judge(m, t, ax, az, turn, mirror, gx, gz, facing, pathAt, lineCells, mayDig);
                            s.facing = f;
                            s.at = k;
                            s.cost = Cost(s, want, f != facing);
                            list.Add(s);
                        }
            }
            // On the way, not beside it: at least half its length of the way in runs down its lane.
            int need = Need(t);
            foreach (var s in list)
                if (s.Fits && s.onLane < need) s.misfit = Misfit.OffWay;
            Sort(list);
            // The cheapest, tried with its walls standing: do raiders still walk the lane, or round it? Until the
            // cheapest that fits has been tried and holds.
            for (int tried = 0; tried < walkChecks; tried++)
            {
                var best = list.Count > 0 && list[0].Fits ? list[0] : null;
                if (best == null || best.walked >= 0) break;
                best.walked = LaneWalked(m, t, best, gx, gz, facing, path, slowCost);
                if (best.walked < need) best.misfit = Misfit.OffWay;
                best.cost = Cost(best, want, best.facing != facing);
                Sort(list);
            }
            return list;
        }

        /// <summary>How many cells of the way in must run down a plan's lane for it to stand on the way: half its length, at least <see cref="OnWay"/>.</summary>
        public static int Need(DefenseLayout.Grid t) => System.Math.Max(OnWay, ((t?.H ?? 0) + 1) / 2);

        private static void Sort(List<Spot> list) => list.Sort((a, b) => a.Fits != b.Fits ? (a.Fits ? -1 : 1) : a.cost.CompareTo(b.cost));

        /// <summary>Unexplored ground just past the far end of the way in: the way goes on into the dark.</summary>
        public static bool DarkAhead(DefenseMath.MapGrid m, IList<int> path, int gx, int gz, int facing)
        {
            if (path == null || path.Count == 0) return false;
            int end = path[path.Count - 1], ex = end % m.w, ez = end / m.w;
            int ahead = DefenseLineMath.Ahead(ex - gx, ez - gz, facing);
            for (int d = 0; d < 8; d++)
            {
                int nx = ex + DX[d], nz = ez + DZ[d];
                if (!m.In(nx, nz) || DefenseLineMath.Ahead(nx - gx, nz - gz, facing) < ahead) continue;
                if (m.kind[m.Index(nx, nz)] == DefenseMath.Unknown) return true;
            }
            return false;
        }

        /// <summary>One spot, cell by cell.</summary>
        public static Spot Judge(DefenseMath.MapGrid m, DefenseLayout.Grid t, int ax, int az, int turn, bool mirror, int gx, int gz, int facing,
            Dictionary<int, int> pathAt, HashSet<int> lineCells, bool mayDig)
        {
            var s = new Spot { anchorX = ax, anchorZ = az, turn = turn, mirror = mirror };
            int cells = 0;
            for (int x = 0; x < t.W; x++)
            {
                for (int z = 0; z < t.H; z++)
                {
                    char c = t.cells[x, z];
                    if (c == DefenseLayout.Untouched) continue;
                    DefenseLayout.ToWorld(t, x, z, mirror, turn, out int ox, out int oz);
                    int wx = ax + ox, wz = az + oz;
                    if (!m.In(wx, wz))
                    {
                        s.misfit = Worse(s.misfit, Misfit.Edge);
                        continue;
                    }
                    int i = m.Index(wx, wz);
                    cells++;
                    if (lineCells != null && lineCells.Contains(i) || DefenseLineMath.Ahead(wx - gx, wz - gz, facing) <= 0)
                    {
                        s.misfit = Worse(s.misfit, Misfit.Crosses);
                        continue;
                    }
                    bool piece = DefenseLayout.RoleOf(c) != null;
                    int e = m.noBuildEdge;
                    if (piece && (wx < e || wz < e || wx >= m.w - e || wz >= m.h - e)) s.misfit = Worse(s.misfit, Misfit.Edge);
                    byte k = m.kind[i];
                    bool onPath = pathAt != null && pathAt.ContainsKey(i);
                    if (c == DefenseLayout.Wall || c == DefenseLayout.Embrasure)
                    {
                        // A side wall: rock or a wall of the colony's that's there already does the job.
                        bool left = x < t.W / 2;
                        if (left) s.leftAll++;
                        else s.rightAll++;
                        if (onPath) s.onWalls++;
                        switch (k)
                        {
                            case DefenseMath.Rock:
                                s.rockSides++;
                                if (left) s.leftHave++;
                                else s.rightHave++;
                                break;
                            case DefenseMath.Wall:
                                s.wallSides++;
                                if (left) s.leftHave++;
                                else s.rightHave++;
                                break;
                            case DefenseMath.Open:
                            case DefenseMath.Bridge:
                                s.build++;
                                break;
                            case DefenseMath.Unknown:
                                s.fog++;
                                break;
                            case DefenseMath.Water:
                                s.misfit = Worse(s.misfit, Misfit.Water);
                                break;
                            default:
                                s.misfit = Worse(s.misfit, Misfit.Blocked);
                                break;
                        }
                        continue;
                    }
                    if (DefenseLayout.Solid(c) && onPath) s.onWalls++;
                    if (Lane(c) && onPath) s.onLane++;
                    switch (k)
                    {
                        case DefenseMath.Open:
                        case DefenseMath.Bridge:
                            if (piece && DefenseLayout.Solid(c)) s.build++;
                            else if (piece) s.lanePieces++;
                            break;
                        case DefenseMath.Rock:
                            if (mayDig) s.mine++;
                            else s.misfit = Worse(s.misfit, Misfit.Rock);
                            break;
                        case DefenseMath.Unknown:
                            s.fog++;
                            if (mayDig) s.mine++;
                            break;
                        case DefenseMath.Water:
                            s.misfit = Worse(s.misfit, Misfit.Water);
                            break;
                        default:
                            s.misfit = Worse(s.misfit, Misfit.Blocked);
                            break;
                    }
                }
            }
            // Wholly or mostly in the dark: nothing to start from, and nothing known of what's there.
            if (s.misfit == Misfit.None && cells > 0 && s.fog * 2 > cells) s.misfit = Misfit.Fog;
            return s;
        }

        /// <summary>The reasons in the order that says most: a spot on the line is the worst, then off the map, then what's in the way.</summary>
        private static Misfit Worse(Misfit a, Misfit b)
        {
            int Rank(Misfit x)
            {
                switch (x)
                {
                    case Misfit.Crosses: return 7;
                    case Misfit.Edge: return 6;
                    case Misfit.Water: return 5;
                    case Misfit.Blocked: return 4;
                    case Misfit.Rock: return 3;
                    case Misfit.OffWay: return 3;
                    case Misfit.Fog: return 2;
                    case Misfit.None: return 0;
                    default: return 1;
                }
            }
            return Rank(b) > Rank(a) ? b : a;
        }

        /// <summary>
        /// Why nothing fits, from all the spots tried: no way in to follow, too little ground before it (or unexplored
        /// ground, when the way runs on into the dark), or what kept the spots out (what stood in the most of them, the
        /// line itself counting least: every spot that reaches the line also has others that don't).
        /// </summary>
        public static Misfit WhyNone(IList<int> path, IList<Spot> spots, int length, bool darkAhead = false)
        {
            if (path == null || path.Count < 2) return darkAhead ? Misfit.Fog : Misfit.NoPath;
            if (path.Count - 1 < System.Math.Max(2, length / 2)) return darkAhead ? Misfit.Fog : Misfit.Short;
            var counts = new Dictionary<Misfit, int>();
            foreach (var s in spots)
            {
                if (s.Fits) return Misfit.None;
                counts.TryGetValue(s.misfit, out int n);
                counts[s.misfit] = n + 1;
            }
            Misfit best = Misfit.Short;
            int bestN = 0;
            foreach (var kv in counts)
            {
                int n = kv.Key == Misfit.Crosses ? kv.Value / 3 : kv.Value;
                if (n > bestN)
                {
                    best = kv.Key;
                    bestN = n;
                }
            }
            return best;
        }

        /// <summary>
        /// How much of a spot's lane the way in uses once its solid pieces stand: the easiest way from where the way in
        /// starts to the gate, over the raiders' side, with the spot's walls in place and what slows raiders costing
        /// <paramref name="slowCost"/> more. Fewer than a few cells means they'd walk round it. The grid is put back.
        /// </summary>
        public static int LaneWalked(DefenseMath.MapGrid m, DefenseLayout.Grid t, Spot s, int gx, int gz, int facing, IList<int> path, int slowCost = 40)
        {
            if (path == null || path.Count < 2) return 0;
            var lane = new HashSet<int>();
            var saved = new List<(int i, byte k, int c)>();
            try
            {
                for (int x = 0; x < t.W; x++)
                    for (int z = 0; z < t.H; z++)
                    {
                        char c = t.cells[x, z];
                        if (c == DefenseLayout.Untouched) continue;
                        DefenseLayout.ToWorld(t, x, z, s.mirror, s.turn, out int ox, out int oz);
                        int wx = s.anchorX + ox, wz = s.anchorZ + oz;
                        if (!m.In(wx, wz)) continue;
                        int i = m.Index(wx, wz);
                        if (Lane(c)) lane.Add(i);
                        if (DefenseLayout.Solid(c))
                        {
                            saved.Add((i, m.kind[i], m.cost[i]));
                            m.kind[i] = DefenseMath.Wall;
                        }
                        else if (Lane(c))
                        {
                            // Dug out where it was rock; what slows raiders costs them a little.
                            saved.Add((i, m.kind[i], m.cost[i]));
                            if (m.kind[i] == DefenseMath.Rock || m.kind[i] == DefenseMath.Unknown) m.kind[i] = DefenseMath.Open;
                            if (c == DefenseLayout.Slow || c == DefenseLayout.Cover) m.cost[i] = System.Math.Max(m.cost[i], slowCost);
                        }
                    }
                var way = PathFrom(m, gx, gz, facing, path[path.Count - 1]);
                int n = 0;
                foreach (int c in way)
                    if (lane.Contains(c)) n++;
                return n;
            }
            finally
            {
                for (int u = saved.Count - 1; u >= 0; u--)
                {
                    m.kind[saved[u].i] = saved[u].k;
                    m.cost[saved[u].i] = saved[u].c;
                }
            }
        }

        /// <summary>The easiest way from one cell in front of the gate to the gate, over the raiders' side (gate last); empty if none.</summary>
        public static List<int> PathFrom(DefenseMath.MapGrid m, int gx, int gz, int facing, int from, int reach = Reach)
        {
            var way = new List<int>();
            if (!m.In(gx, gz) || from < 0) return way;
            int gate = m.Index(gx, gz);
            int x0 = System.Math.Max(0, gx - reach - 2), x1 = System.Math.Min(m.w - 1, gx + reach + 2);
            int z0 = System.Math.Max(0, gz - reach - 2), z1 = System.Math.Min(m.h - 1, gz + reach + 2);
            var dist = new Dictionary<int, int> { [gate] = 0 };
            var heap = new DefenseMath.MinHeap(256);
            heap.Push(0, gate);
            var done = new HashSet<int>();
            while (heap.Count > 0)
            {
                heap.Pop(out long key, out int i);
                if (!done.Add(i)) continue;
                if (i == from) break;
                int x = i % m.w, z = i / m.w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (nx < x0 || nx > x1 || nz < z0 || nz > z1 || !m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (done.Contains(j) || !m.Passable(j) || DefenseLineMath.Ahead(nx - gx, nz - gz, facing) < 1) continue;
                    if (d >= 4 && (!m.Passable(m.Index(nx, z)) || !m.Passable(m.Index(x, nz)))) continue;
                    int nd = (int)key + DefenseMath.StepCost(m, i == gate ? j : i, d);
                    if (dist.TryGetValue(j, out int have) && have <= nd) continue;
                    dist[j] = nd;
                    heap.Push(nd, j);
                }
            }
            if (!dist.ContainsKey(from)) return way;
            int cur = from, guard = dist.Count + 2;
            way.Add(cur);
            while (cur != gate && guard-- > 0)
            {
                int x = cur % m.w, z = cur / m.w, best = -1, bestD = dist[cur];
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (!m.In(nx, nz)) continue;
                    int j = m.Index(nx, nz);
                    if (!dist.TryGetValue(j, out int dj) || dj >= bestD) continue;
                    if (d >= 4 && (!m.Passable(m.Index(nx, z)) || !m.Passable(m.Index(x, nz)))) continue;
                    best = j;
                    bestD = dj;
                }
                if (best < 0) break;
                cur = best;
                way.Add(cur);
            }
            return cur == gate ? way : new List<int>();
        }

        /// <summary>The sides a spot leans on: 2 both, 1 one, 0 none, counting a side when most of its wall is there already.</summary>
        public static int SidesStanding(Spot s)
        {
            int n = 0;
            if (s.leftAll > 0 && s.leftHave * 10 >= s.leftAll * 6) n++;
            if (s.rightAll > 0 && s.rightHave * 10 >= s.rightAll * 6) n++;
            return n;
        }
    }
}
