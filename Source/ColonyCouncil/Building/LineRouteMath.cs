using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Laying a net's lines (power conduits, plumbing pipes) under the walls, as plain rules; Building/LineRoutes.cs
    /// reads the map. The net's fixed pieces (batteries, generators, fixtures: its anchors) carry the line on and must
    /// stay joined, and a powered piece wired to a conduit must keep a line within reach that is nearer than any other
    /// net's. Lines under walls, doors and furniture stay where they are. The ones over floors and open ground are
    /// lifted, and the pieces left are joined again by the cheapest cells: under a wall first, then under furniture,
    /// then open ground, a floor last, with a line that's there already a little cheaper than a new one. Then every
    /// line that isn't needed after all goes, the most in sight first.
    /// </summary>
    public static class LineRouteMath
    {
        /// <summary>Where a cell stands for a line: under a wall or door, under furniture, on open ground, on a floor; or no line can go there.</summary>
        public enum Spot : byte { Blocked, Wall, Furniture, Ground, Floor }

        /// <summary>What a new line costs on a cell, in tenths: out of sight under a wall, in everyone's way on a floor (-1: none can go there).</summary>
        public static int LayCost(Spot s)
        {
            switch (s)
            {
                case Spot.Wall: return 10;
                case Spot.Furniture: return 30;
                case Spot.Ground: return 50;
                case Spot.Floor: return 80;
                default: return -1;
            }
        }

        /// <summary>A line that's there already, in sight, costs this share of a new one: no work, but still in sight.</summary>
        public const int KeepPercent = 80;

        /// <summary>A powered piece's reach to a line, Euclidean, as the room rearranger lays conduits by.</summary>
        public const float Reach = ConduitMath.Reach;

        public static bool Hidden(Spot s) => s == Spot.Wall || s == Spot.Furniture;

        /// <summary>A fixed piece of the net: the cells it stands on, and the cell wires are measured from (-1: it takes no wires).</summary>
        public class Anchor
        {
            public List<int> cells = new List<int>();
            public int wireAt = -1;
        }

        /// <summary>A powered piece wired to one of the net's lines: where it stands, and how near another net's nearest line or piece is.</summary>
        public class Reacher
        {
            public int x, z;
            /// <summary>The squared distance to the nearest line or piece of another net within its reach (int.MaxValue: none).</summary>
            public int otherSq = int.MaxValue;
        }

        /// <summary>One net, on a grid of w × h cells (index z * w + x).</summary>
        public class Net
        {
            public int w, h;
            /// <summary>Where each cell stands, for a line there now or one laid anew.</summary>
            public Spot[] spots;
            /// <summary>A new line can be laid on the cell: the game allows it there, and it isn't beside another net.</summary>
            public bool[] canLay;
            /// <summary>Asked instead of <see cref="canLay"/> when set: the map's own check is dear, and the rules look at few cells.</summary>
            public Func<int, bool> canLayAt;
            public HashSet<int> lines = new HashSet<int>();
            public List<Anchor> anchors = new List<Anchor>();
            public List<Reacher> reachers = new List<Reacher>();
        }

        public class Plan
        {
            public bool ok;
            /// <summary>Why there's no plan: "joined" (the net doesn't hang together as read), "cut off" (something can't be reached again), "nothing".</summary>
            public string why;
            public List<int> lay = new List<int>(), takeUp = new List<int>();
            public int inSightBefore, inSightAfter;
            /// <summary>The net's lines as they would stand.</summary>
            public HashSet<int> after = new HashSet<int>();
        }

        /// <summary>What it costs for the net to take a cell in (tenths; -1 when it can't): its own pieces and its lines out of sight cost nothing.</summary>
        private static int Cost(Net n, int[] anchorOf, int i)
        {
            if (anchorOf[i] >= 0) return 0;
            var s = n.spots[i];
            if (n.lines.Contains(i))
            {
                if (Hidden(s)) return 0;
                int c = LayCost(s == Spot.Blocked ? Spot.Floor : s);
                return c * KeepPercent / 100;
            }
            return CanLay(n, i) ? LayCost(s) : -1;
        }

        private static bool CanLay(Net n, int i) => n.canLayAt != null ? n.canLayAt(i) : n.canLay != null && n.canLay[i];

        private static int[] AnchorMap(Net n)
        {
            var anchorOf = new int[n.w * n.h];
            for (int i = 0; i < anchorOf.Length; i++) anchorOf[i] = -1;
            for (int a = 0; a < n.anchors.Count; a++)
                foreach (int c in n.anchors[a].cells)
                    if (c >= 0 && c < anchorOf.Length) anchorOf[c] = a;
            return anchorOf;
        }

        /// <summary>For each reacher, the cells that would keep it on the net if they're in it: a line within reach and nearer than any other net's, or an anchor that takes wires.</summary>
        private static List<int>[] Satisfiers(Net n, int[] anchorOf)
        {
            int r2 = (int)(Reach * Reach);
            var result = new List<int>[n.reachers.Count];
            for (int k = 0; k < n.reachers.Count; k++)
            {
                var r = n.reachers[k];
                var list = result[k] = new List<int>();
                int lim = (int)Reach;
                for (int dz = -lim; dz <= lim; dz++)
                for (int dx = -lim; dx <= lim; dx++)
                {
                    int x = r.x + dx, z = r.z + dz;
                    if (x < 0 || z < 0 || x >= n.w || z >= n.h) continue;
                    int i = z * n.w + x;
                    int at = i;
                    if (anchorOf[i] >= 0)
                    {
                        at = n.anchors[anchorOf[i]].wireAt;
                        if (at < 0) continue;
                    }
                    int ax = at % n.w - r.x, az = at / n.w - r.z;
                    int d = ax * ax + az * az;
                    if (d <= r2 && d < r.otherSq) list.Add(i);
                }
            }
            return result;
        }

        private static IEnumerable<int> Beside(Net n, int i)
        {
            int x = i % n.w, z = i / n.w;
            if (x > 0) yield return i - 1;
            if (x < n.w - 1) yield return i + 1;
            if (z > 0) yield return i - n.w;
            if (z < n.h - 1) yield return i + n.w;
        }

        /// <summary>The cells of <paramref name="set"/> joined to <paramref name="from"/> through each other.</summary>
        private static HashSet<int> Reached(Net n, HashSet<int> set, IEnumerable<int> from)
        {
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (int f in from)
                if (set.Contains(f) && seen.Add(f)) stack.Push(f);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                foreach (int b in Beside(n, c))
                    if (set.Contains(b) && seen.Add(b)) stack.Push(b);
            }
            return seen;
        }

        /// <summary>Every anchor joined to the first, and every reacher kept on by a cell joined to them.</summary>
        private static bool Holds(Net n, HashSet<int> set, List<int>[] satisfiers, IEnumerable<int> seed)
        {
            var reached = Reached(n, set, seed);
            foreach (var a in n.anchors)
                foreach (int c in a.cells)
                    if (!reached.Contains(c)) return false;
            foreach (var s in satisfiers)
                if (!s.Any(reached.Contains)) return false;
            return true;
        }

        private static List<int> SeedOf(Net n, HashSet<int> set, List<int>[] satisfiers)
        {
            if (n.anchors.Count > 0 && n.anchors[0].cells.Count > 0) return new List<int> { n.anchors[0].cells[0] };
            // No fixed piece: the line that keeps the most powered pieces on.
            int best = -1, most = -1;
            foreach (int c in set.OrderBy(c => c))
            {
                int k = satisfiers.Count(s => s.Contains(c));
                if (k > most)
                {
                    most = k;
                    best = c;
                }
            }
            return best >= 0 ? new List<int> { best } : new List<int>();
        }

        /// <summary>The net's lines laid again under the walls, with what's laid and what's taken up.</summary>
        public static Plan Route(Net n)
        {
            var plan = new Plan();
            int size = n.w * n.h;
            if (n.spots == null || n.spots.Length != size || n.canLayAt == null && (n.canLay == null || n.canLay.Length != size) || n.lines.Count == 0)
            {
                plan.why = "nothing";
                return plan;
            }
            var anchorOf = AnchorMap(n);
            var satisfiers = Satisfiers(n, anchorOf);
            plan.inSightBefore = n.lines.Count(i => anchorOf[i] < 0 && !Hidden(n.spots[i]));

            // As it stands, the net must hang together as read; else the map knows something these rules don't.
            var now = new HashSet<int>(n.lines);
            foreach (var a in n.anchors) now.UnionWith(a.cells);
            var seed = SeedOf(n, now, satisfiers);
            if (seed.Count == 0 || !Holds(n, now, satisfiers, seed))
            {
                plan.why = "joined";
                return plan;
            }

            // What stays: the anchors and the lines out of sight, in the pieces they make.
            var comp = new int[size];
            for (int i = 0; i < size; i++) comp[i] = -1;
            var comps = new List<List<int>>();
            var required = new List<bool>();
            for (int i = 0; i < size; i++)
            {
                bool stays = anchorOf[i] >= 0 || n.lines.Contains(i) && Hidden(n.spots[i]);
                if (!stays || comp[i] >= 0) continue;
                var cells = new List<int>();
                bool anchored = false;
                var stack = new Stack<int>();
                stack.Push(i);
                comp[i] = comps.Count;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    cells.Add(c);
                    if (anchorOf[c] >= 0) anchored = true;
                    foreach (int b in Beside(n, c))
                    {
                        if (comp[b] >= 0) continue;
                        if (!(anchorOf[b] >= 0 || n.lines.Contains(b) && Hidden(n.spots[b]))) continue;
                        comp[b] = comps.Count;
                        stack.Push(b);
                    }
                }
                comps.Add(cells);
                required.Add(anchored);
            }

            // The tree grows from the first anchor's piece (or the line keeping the most on), to the nearest piece or
            // powered piece still apart, until all are in.
            var tree = new HashSet<int>();
            var joined = new bool[comps.Count];
            var kept = new bool[n.reachers.Count];
            void Take(int c)
            {
                if (!tree.Add(c)) return;
                if (comp[c] >= 0 && !joined[comp[c]])
                {
                    joined[comp[c]] = true;
                    foreach (int o in comps[comp[c]]) Take(o);
                }
            }
            foreach (int s in seed) Take(s);
            void Mark()
            {
                for (int k = 0; k < kept.Length; k++)
                    if (!kept[k] && satisfiers[k].Any(tree.Contains)) kept[k] = true;
            }
            Mark();
            var dist = new long[size];
            var parent = new int[size];
            while (true)
            {
                bool done = true;
                for (int k = 0; k < comps.Count && done; k++) if (required[k] && !joined[k]) done = false;
                for (int k = 0; k < kept.Length && done; k++) if (!kept[k]) done = false;
                if (done) break;
                var wanted = new HashSet<int>();
                for (int k = 0; k < kept.Length; k++)
                    if (!kept[k]) wanted.UnionWith(satisfiers[k]);
                for (int i = 0; i < size; i++)
                {
                    dist[i] = long.MaxValue;
                    parent[i] = -1;
                }
                var heap = new DefenseMath.MinHeap(256);
                foreach (int t in tree.OrderBy(t => t))
                {
                    dist[t] = 0;
                    heap.Push(0, t);
                }
                int target = -1;
                while (heap.Count > 0)
                {
                    heap.Pop(out long d, out int c);
                    if (d > dist[c]) continue;
                    if (!tree.Contains(c) && (comp[c] >= 0 && required[comp[c]] && !joined[comp[c]] || wanted.Contains(c)))
                    {
                        target = c;
                        break;
                    }
                    foreach (int b in Beside(n, c))
                    {
                        int cost = Cost(n, anchorOf, b);
                        if (cost < 0) continue;
                        long nd = d + cost;
                        if (nd < dist[b])
                        {
                            dist[b] = nd;
                            parent[b] = c;
                            heap.Push(nd, b);
                        }
                    }
                }
                if (target < 0)
                {
                    plan.why = "cut off";
                    return plan;
                }
                // The whole way back first: a piece taken in on the way can hold cells further along it.
                var way = new List<int>();
                for (int c = target; c >= 0 && !tree.Contains(c); c = parent[c]) way.Add(c);
                foreach (int c in way) Take(c);
                Mark();
            }

            // Every line not needed after all goes: in sight first, the dearest first.
            var candidates = tree.Where(c => anchorOf[c] < 0 && !(n.lines.Contains(c) && Hidden(n.spots[c])))
                .OrderBy(c => Hidden(n.spots[c]) ? 1 : 0).ThenByDescending(c => Cost(n, anchorOf, c)).ThenBy(c => c).ToList();
            foreach (int c in candidates)
            {
                tree.Remove(c);
                if (!Holds(n, tree, satisfiers, seed)) tree.Add(c);
            }
            var final = Reached(n, tree, seed);
            foreach (int c in final.OrderBy(c => c))
            {
                if (anchorOf[c] >= 0) continue;
                plan.after.Add(c);
                if (!n.lines.Contains(c)) plan.lay.Add(c);
            }
            plan.takeUp = n.lines.Where(c => !final.Contains(c)).OrderBy(c => c).ToList();
            plan.inSightAfter = plan.after.Count(c => !Hidden(n.spots[c]));
            plan.ok = true;
            return plan;
        }

        /// <summary>
        /// Of <paramref name="candidates"/> (lines to take up once the new ones stand), the ones the net can do without as
        /// it stands now, most in sight first: each goes only if every anchor stays joined and every powered piece kept on.
        /// </summary>
        public static List<int> Removable(Net n, IEnumerable<int> candidates)
        {
            var result = new List<int>();
            int size = n.w * n.h;
            if (n.spots == null || n.spots.Length != size) return result;
            var anchorOf = AnchorMap(n);
            var satisfiers = Satisfiers(n, anchorOf);
            var set = new HashSet<int>(n.lines);
            foreach (var a in n.anchors) set.UnionWith(a.cells);
            var seed = SeedOf(n, set, satisfiers);
            if (seed.Count == 0 || !Holds(n, set, satisfiers, seed)) return result;
            foreach (int c in candidates.Where(c => n.lines.Contains(c) && anchorOf[c] < 0).Distinct()
                         .OrderBy(c => Hidden(n.spots[c]) ? 1 : 0).ThenByDescending(c => LayCost(n.spots[c])).ThenBy(c => c))
            {
                set.Remove(c);
                if (Holds(n, set, satisfiers, seed)) result.Add(c);
                else set.Add(c);
            }
            return result;
        }
    }
}
