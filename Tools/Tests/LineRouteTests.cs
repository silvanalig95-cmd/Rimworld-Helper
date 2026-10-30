using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Lines laid again under the walls (Building/LineRouteMath): runs over floors lifted and the net joined again by the
/// walls, nothing cut off, every powered piece kept on and nearer its own net than another, nothing laid where no
/// line can go. Small bases drawn as text, then thousands of random ones.
/// </summary>
public static class LineRouteTests
{
    // Drawn north up. # wall, R rock (no line), . floor, , open ground, f furniture, X beside another net (floor, no new line),
    // c line on the floor, g line on open ground, C line under a wall, A an anchor that takes wires, B one that doesn't,
    // r a powered piece on the floor.
    private static LineRouteMath.Net Parse(string[] rows, out List<(int x, int z)> reachers)
    {
        int h = rows.Length, w = rows.Max(r => r.Length);
        var n = new LineRouteMath.Net { w = w, h = h, spots = new LineRouteMath.Spot[w * h], canLay = new bool[w * h] };
        reachers = new List<(int, int)>();
        for (int row = 0; row < h; row++)
        {
            int z = h - 1 - row;
            for (int x = 0; x < w; x++)
            {
                char ch = x < rows[row].Length ? rows[row][x] : ' ';
                int i = z * w + x;
                var spot = LineRouteMath.Spot.Floor;
                bool lay = true;
                switch (ch)
                {
                    case '#': spot = LineRouteMath.Spot.Wall; break;
                    case 'R': spot = LineRouteMath.Spot.Blocked; lay = false; break;
                    case ',': spot = LineRouteMath.Spot.Ground; break;
                    case 'f': spot = LineRouteMath.Spot.Furniture; break;
                    case 'X': lay = false; break;
                    case 'c': n.lines.Add(i); break;
                    case 'g': spot = LineRouteMath.Spot.Ground; n.lines.Add(i); break;
                    case 'C': spot = LineRouteMath.Spot.Wall; n.lines.Add(i); break;
                    case 'A':
                    case 'B':
                        lay = false;
                        n.anchors.Add(new LineRouteMath.Anchor { cells = new List<int> { i }, wireAt = ch == 'A' ? i : -1 });
                        break;
                    case 'r':
                        lay = true;
                        n.reachers.Add(new LineRouteMath.Reacher { x = x, z = z });
                        reachers.Add((x, z));
                        break;
                    case ' ': spot = LineRouteMath.Spot.Blocked; lay = false; break;
                }
                n.spots[i] = spot;
                n.canLay[i] = lay;
            }
        }
        return n;
    }

    private static IEnumerable<int> Beside(LineRouteMath.Net n, int i)
    {
        int x = i % n.w, z = i / n.w;
        if (x > 0) yield return i - 1;
        if (x < n.w - 1) yield return i + 1;
        if (z > 0) yield return i - n.w;
        if (z < n.h - 1) yield return i + n.w;
    }

    /// <summary>The rules checked from outside: anchors all joined through the lines after, each powered piece with a line or wire-taking anchor of its own within reach and nearer than another net's.</summary>
    private static bool Holds(LineRouteMath.Net n, HashSet<int> lines)
    {
        var set = new HashSet<int>(lines);
        foreach (var a in n.anchors) set.UnionWith(a.cells);
        int start = n.anchors.Count > 0 ? n.anchors[0].cells[0] : lines.Count > 0 ? lines.Min() : -1;
        if (start < 0) return n.reachers.Count == 0;
        var seen = new HashSet<int> { start };
        var stack = new Stack<int>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            int c = stack.Pop();
            foreach (int b in Beside(n, c))
                if (set.Contains(b) && seen.Add(b)) stack.Push(b);
        }
        if (n.anchors.Any(a => a.cells.Any(c => !seen.Contains(c)))) return false;
        foreach (var r in n.reachers)
        {
            bool on = false;
            foreach (int c in seen)
            {
                int at = c;
                var anchor = n.anchors.FirstOrDefault(a => a.cells.Contains(c));
                if (anchor != null)
                {
                    if (anchor.wireAt < 0) continue;
                    at = anchor.wireAt;
                }
                int dx = at % n.w - r.x, dz = at / n.w - r.z, d = dx * dx + dz * dz;
                if (d <= LineRouteMath.Reach * LineRouteMath.Reach && d < r.otherSq)
                {
                    on = true;
                    break;
                }
            }
            if (!on) return false;
        }
        return true;
    }

    private static bool Sound(LineRouteMath.Net n, LineRouteMath.Plan p, out string why)
    {
        why = null;
        if (p.lay.Any(c => !n.canLay[c] || n.lines.Contains(c))) why = "lays where it can't, or where a line is";
        else if (p.takeUp.Any(c => !n.lines.Contains(c))) why = "takes up a line that isn't there";
        else if (!p.after.SetEquals(n.lines.Where(c => !p.takeUp.Contains(c)).Concat(p.lay))) why = "the lines after aren't the lines kept and laid";
        else if (!Holds(n, p.after)) why = "something is cut off";
        else if (p.inSightAfter != p.after.Count(c => !LineRouteMath.Hidden(n.spots[c]))) why = "miscounts the lines in sight";
        return why == null;
    }

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void C(bool ok, string what)
        {
            count++;
            check(ok, "lines under the walls: " + what);
        }

        // A run across a room's floor to a lamp at the far end: it goes along the wall instead.
        var room = Parse(new[]
        {
            "################",
            "#..............#",
            "#..............#",
            "ACccccccccc.r..#",
            "#..............#",
            "################",
        }, out _);
        var p = LineRouteMath.Route(room);
        C(p.ok, "a run across a room can be laid again");
        C(Sound(room, p, out string why), "the room's plan is sound: " + why);
        C(p.inSightBefore == 9 && p.inSightAfter == 0, $"no line is left on its floor ({p.inSightBefore} before, {p.inSightAfter} after)");
        C(p.lay.All(c => room.spots[c] == LineRouteMath.Spot.Wall), "the new line is all under the wall");
        C(p.takeUp.Count == 9, $"the run over the floor is taken up ({p.takeUp.Count})");
        C(p.lay.Count <= 13, $"and the wall run is no longer than it needs ({p.lay.Count})");
        C(LineRouteMath.Route(room).lay.SequenceEqual(p.lay), "the same base gives the same plan");

        // A run nobody needs: the wall line already reaches the lamp.
        var near = Parse(new[]
        {
            "#########",
            "ACcccc..#",
            "#..r....#",
            "#########",
        }, out _);
        p = LineRouteMath.Route(near);
        C(p.ok && p.lay.Count == 0 && p.takeUp.Count == 4 && Sound(near, p, out _), "a run the lamp doesn't need is only taken up");

        // A lamp in the middle of a hall, out of reach of every wall: a run over the floor stays, as short as it can be.
        var hall = new List<string> { new string('#', 25) };
        for (int z = 0; z < 23; z++)
        {
            char[] row = ("#" + new string('.', 23) + "#").ToCharArray();
            if (z == 11)
            {
                row = ("AC" + new string('c', 11) + new string('.', 11) + "#").ToCharArray();
                row[12] = 'r';
            }
            hall.Add(new string(row));
        }
        hall.Add(new string('#', 25));
        var big = Parse(hall.ToArray(), out _);
        p = LineRouteMath.Route(big);
        C(p.ok && Sound(big, p, out why), "a lamp out of every wall's reach stays on: " + why);
        C(p.inSightAfter > 0 && p.inSightAfter <= p.inSightBefore, $"through a short run over the floor ({p.inSightBefore} before, {p.inSightAfter} after)");
        C(p.lay.Count == 0, "made of the line that's there, not a new one");

        // Walls of rock take no line: the run over the floor stays.
        var rock = Parse(new[]
        {
            "RRRRRRRRRRRRRRRR",
            "R..............R",
            "ACcccccccccc.r.R",
            "R..............R",
            "RRRRRRRRRRRRRRRR",
        }, out _);
        p = LineRouteMath.Route(rock);
        C(p.ok && Sound(rock, p, out _), "a room in the rock keeps its lamp on");
        C(p.lay.All(c => rock.spots[c] != LineRouteMath.Spot.Wall), "nothing is laid in the rock");
        C(p.takeUp.Count <= 5, $"and most of its run stays ({p.takeUp.Count} taken up)");

        // Two fixtures joined across a bathroom: they're joined again round the walls.
        var bath = Parse(new[]
        {
            "#########",
            "#.......#",
            "BcccccccB",
            "#..c....#",
            "#..c....#",
            "###C#####",
            "...A.....",
        }, out _);
        p = LineRouteMath.Route(bath);
        C(p.ok && Sound(bath, p, out why), "fixtures across a room stay joined: " + why);
        C(p.inSightAfter == 0, $"by the walls alone ({p.inSightAfter} in sight)");

        // Furniture hides a line better than the floor, not as well as a wall.
        C(LineRouteMath.LayCost(LineRouteMath.Spot.Wall) < LineRouteMath.LayCost(LineRouteMath.Spot.Furniture)
          && LineRouteMath.LayCost(LineRouteMath.Spot.Furniture) < LineRouteMath.LayCost(LineRouteMath.Spot.Ground)
          && LineRouteMath.LayCost(LineRouteMath.Spot.Ground) < LineRouteMath.LayCost(LineRouteMath.Spot.Floor)
          && LineRouteMath.LayCost(LineRouteMath.Spot.Blocked) < 0, "a wall, then furniture, open ground, a floor; never rock");

        // Another net close to the lamp: its own line must be nearer than that one.
        var two = Parse(new[]
        {
            "################",
            "#..............#",
            "#..............#",
            "ACccccccccc.r..#",
            "#..............#",
            "################",
        }, out _);
        two.reachers[0].otherSq = 5;
        p = LineRouteMath.Route(two);
        C(p.ok && Sound(two, p, out why), "with another net a little over two cells off, the lamp stays on its own: " + why);
        C(p.after.Any(c => Math.Pow(c % two.w - 12, 2) + Math.Pow(c / two.w - 2, 2) < 5), "through a line nearer than the other net's");

        // A line beside another net can't be laid: the wall there is shut to it.
        var shut = Parse(new[]
        {
            "#XXXXXXXXXXXXXX#",
            "#..............#",
            "ACccccccccc.r..#",
            "#..............#",
            "#XXXXXXXXXXXXXX#",
        }, out _);
        p = LineRouteMath.Route(shut);
        C(p.ok && Sound(shut, p, out _) && p.lay.All(c => shut.canLay[c]), "nothing is laid beside another net");

        // A net that doesn't hang together as read is left alone.
        var apart = Parse(new[]
        {
            "#########",
            "AC....CA#",
            "#########",
        }, out _);
        p = LineRouteMath.Route(apart);
        C(!p.ok && p.why == "joined", "two pieces that aren't joined now are left alone");

        // Once the new lines stand: the old ones go if nothing needs them, and stay if something does.
        p = LineRouteMath.Route(room);
        var built = Parse(new[]
        {
            "################",
            "#..............#",
            "#..............#",
            "ACccccccccc.r..#",
            "#..............#",
            "################",
        }, out _);
        built.lines.UnionWith(p.lay);
        var gone = LineRouteMath.Removable(built, p.takeUp);
        C(gone.Count == p.takeUp.Count, $"once the wall run stands, the whole floor run can go ({gone.Count} of {p.takeUp.Count})");
        var half = Parse(new[]
        {
            "################",
            "#..............#",
            "#..............#",
            "ACccccccccc.r..#",
            "#..............#",
            "################",
        }, out _);
        half.lines.UnionWith(p.lay.Take(p.lay.Count / 2));
        gone = LineRouteMath.Removable(half, p.takeUp);
        var leftLines = new HashSet<int>(half.lines.Where(c => !gone.Contains(c)));
        C(Holds(half, leftLines), "with only half the wall run built, what goes leaves the lamp on");
        C(gone.Count < p.takeUp.Count, $"and some of the floor run stays ({gone.Count} of {p.takeUp.Count} go)");

        // Random bases: rooms of walls, anchors, lamps and a net of lines that holds; every plan sound.
        var rng = new Random(7);
        int plans = 0, better = 0, worse = 0, unsound = 0;
        for (int t = 0; t < 1500; t++)
        {
            var n = RandomBase(rng);
            if (n == null) continue;
            var q = LineRouteMath.Route(n);
            if (!q.ok) continue;
            plans++;
            if (!Sound(n, q, out why))
            {
                unsound++;
                if (unsound <= 3) C(false, $"random base {t}: {why}");
            }
            if (q.inSightAfter < q.inSightBefore) better++;
            if (q.inSightAfter > q.inSightBefore) worse++;
        }
        C(unsound == 0, $"every random plan is sound ({unsound} of {plans} not)");
        C(plans >= 500, $"most random bases get a plan ({plans})");
        C(better * 2 >= plans, $"most of them leave fewer lines in sight ({better} of {plans})");
        C(worse * 50 <= plans, $"and hardly any leave more ({worse} of {plans})");
        return count;
    }

    /// <summary>A base of rooms on a grid: walls every few cells with doors, some rock, anchors, lamps, and a tree of lines joining them, partly over the floors.</summary>
    private static LineRouteMath.Net RandomBase(Random rng)
    {
        int w = rng.Next(14, 34), h = rng.Next(14, 34);
        int size = w * h;
        var n = new LineRouteMath.Net { w = w, h = h, spots = new LineRouteMath.Spot[size], canLay = new bool[size] };
        int step = rng.Next(5, 10);
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            int i = z * w + x;
            bool wall = x % step == 0 || z % step == 0;
            n.spots[i] = wall ? LineRouteMath.Spot.Wall : rng.NextDouble() < 0.1 ? LineRouteMath.Spot.Furniture : rng.NextDouble() < 0.3 ? LineRouteMath.Spot.Ground : LineRouteMath.Spot.Floor;
            n.canLay[i] = true;
            if (wall && rng.NextDouble() < 0.08)
            {
                n.spots[i] = LineRouteMath.Spot.Blocked;
                n.canLay[i] = false;
            }
            else if (rng.NextDouble() < 0.03) n.canLay[i] = false;
        }
        // Anchors and lamps on open cells.
        var open = Enumerable.Range(0, size).Where(i => n.canLay[i] && n.spots[i] != LineRouteMath.Spot.Wall).ToList();
        if (open.Count < 10) return null;
        int anchors = rng.Next(1, 4), lamps = rng.Next(0, 6);
        var used = new HashSet<int>();
        for (int a = 0; a < anchors; a++)
        {
            int c = open[rng.Next(open.Count)];
            if (!used.Add(c)) continue;
            n.anchors.Add(new LineRouteMath.Anchor { cells = new List<int> { c }, wireAt = rng.NextDouble() < 0.5 ? c : -1 });
            n.canLay[c] = false;
        }
        var lampAt = new List<int>();
        for (int l = 0; l < lamps; l++)
        {
            int c = open[rng.Next(open.Count)];
            if (!used.Add(c)) continue;
            lampAt.Add(c);
        }
        // Lines: the shortest walk from the first anchor to each other anchor and next to each lamp, over anything a line can take.
        int start = n.anchors[0].cells[0];
        var tree = new HashSet<int> { start };
        var targets = n.anchors.Skip(1).Select(a => a.cells[0]).Concat(lampAt).ToList();
        foreach (int target in targets)
        {
            var prev = new Dictionary<int, int>();
            var queue = new Queue<int>();
            foreach (int t0 in tree)
            {
                prev[t0] = -1;
                queue.Enqueue(t0);
            }
            int found = -1;
            while (queue.Count > 0 && found < 0)
            {
                int c = queue.Dequeue();
                foreach (int b in Beside(n, c))
                {
                    if (prev.ContainsKey(b)) continue;
                    bool anchorCell = n.anchors.Any(a => a.cells.Contains(b));
                    if (b != target && !n.canLay[b] && !anchorCell) continue;
                    prev[b] = c;
                    if (b == target) { found = b; break; }
                    queue.Enqueue(b);
                }
            }
            if (found < 0) return null;
            bool lamp = lampAt.Contains(target);
            for (int c = lamp ? prev[found] : found; c >= 0 && !tree.Contains(c); c = prev[c]) tree.Add(c);
        }
        foreach (int c in tree)
            if (!n.anchors.Any(a => a.cells.Contains(c))) n.lines.Add(c);
        foreach (int c in lampAt) n.reachers.Add(new LineRouteMath.Reacher { x = c % w, z = c / w });
        // A line can't stand where a new one is shut out only by the game's say: lines stand on cells that take them.
        if (n.lines.Count == 0) return null;
        return n;
    }
}
