using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Tidying a built room (Building/RoomArrangeMath): drawn rooms with furniture scattered about, and what a sound
/// arrangement has to keep to: nothing on top of anything, the doorway free, everything reachable, beds with their
/// head to a wall, bedside tables by a bed, chairs at the table, shelves along the walls, what can't move left alone,
/// and what already stands well left where it is.
/// </summary>
public static class RoomArrangeTests
{
    /// <summary>A room from a drawing: '#' wall, '+' the door itself (not part of the room), '.' floor, 'D' the floor cell inside a door, 'H' floor that takes heavy pieces.</summary>
    private static RoomArrangeMath.Grid Room(params string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var g = new RoomArrangeMath.Grid { w = w, h = h, floor = new bool[w, h] };
        bool anyHeavy = rows.Any(r => r.Contains('H'));
        if (anyHeavy) g.heavy = new bool[w, h];
        for (int r = 0; r < h; r++)
        {
            int z = h - 1 - r;
            for (int x = 0; x < w; x++)
            {
                char c = rows[r][x];
                bool floor = c == '.' || c == 'D' || c == 'H';
                g.floor[x, z] = floor;
                if (anyHeavy) g.heavy[x, z] = c == 'H' || c == 'D';
                if (c == 'D') g.doorInside.Add((x, z));
            }
        }
        return g;
    }

    private static int nextId;

    /// <summary>With RIMFOLK_ARRANGE_DEBUG set, prints each room as arranged: the grid with item ids, and each item's place and reason.</summary>
    private static void Dump(string label, RoomArrangeMath.Grid g, List<RoomArrangeMath.Item> items, List<string> notes)
    {
        if (Environment.GetEnvironmentVariable("RIMFOLK_ARRANGE_DEBUG") == null) return;
        Console.WriteLine("-- " + label);
        for (int z = g.h - 1; z >= 0; z--)
        {
            var row = new System.Text.StringBuilder();
            for (int x = 0; x < g.w; x++)
            {
                var it = items.FirstOrDefault(i => i.Contains(x, z));
                row.Append(!g.Floor(x, z) ? "##" : g.IsDoorInside(x, z) ? " D" : it != null ? (it.id % 100).ToString().PadLeft(2) : " .");
            }
            Console.WriteLine(row);
        }
        foreach (var it in items)
            Console.WriteLine($"  {it.id} kind {it.kind} {it.sizeX}x{it.sizeZ} from ({it.curX},{it.curZ} r{it.curRot}) to ({it.x},{it.z} r{it.rot}) {(it.fixedPlace ? "fixed" : it.stranded ? "stranded" : it.moved ? "moved" : "stays")} {it.why}");
        foreach (var n in notes) Console.WriteLine("  note: " + n);
    }

    private static RoomArrangeMath.Item Item(int kind, int x, int z, int rot = 0, int sx = 1, int sz = 1, bool passable = false, (int, int)? spot = null, bool fixedPlace = false)
    {
        var it = new RoomArrangeMath.Item { id = ++nextId, kind = kind, curX = x, curZ = z, curRot = rot, sizeX = sx, sizeZ = sz, passable = passable, fixedPlace = fixedPlace };
        if (spot.HasValue) it.spots.Add(spot.Value);
        return it;
    }

    private static RoomArrangeMath.Item Bed(int x, int z, int rot = 0) => Item(RoomArrangeMath.Bed, x, z, rot, 1, 2);
    private static RoomArrangeMath.Item Bench(int x, int z, int rot = 0) => Item(RoomArrangeMath.Bench, x, z, rot, 3, 1, false, (0, -1));

    /// <summary>What every arrangement has to keep to; returns what's wrong, or null.</summary>
    private static string Sound(RoomArrangeMath.Grid g, List<RoomArrangeMath.Item> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var a = items[i];
            for (int x = a.minX; x < a.minX + a.w; x++)
                for (int z = a.minZ; z < a.minZ + a.h; z++)
                {
                    if (a.fixedPlace) continue;
                    if (!g.Floor(x, z)) return $"item {a.id} ({a.kind}) off the floor at ({x},{z})";
                    if (g.IsDoorInside(x, z)) return $"item {a.id} on the cell inside a door";
                }
            for (int j = i + 1; j < items.Count; j++)
            {
                var b = items[j];
                if (a.fixedPlace && b.fixedPlace) continue;
                if (RoomArrangeMath.Overlap(a, b)) return $"items {a.id} and {b.id} overlap";
            }
            if (a.fixedPlace || a.stranded) continue;
            foreach (var (sx, sz) in a.SpotCells())
            {
                if (!g.Floor(sx, sz)) return $"item {a.id}'s work spot is in the wall";
                foreach (var b in items)
                    if (b != a && !b.passable && b.Contains(sx, sz)) return $"item {a.id}'s work spot is under item {b.id}";
            }
        }
        // Everything reachable from the doors.
        var reach = new bool[g.w, g.h];
        var queue = new Queue<(int, int)>();
        foreach (var d in g.doorInside)
        {
            reach[d.x, d.z] = true;
            queue.Enqueue(d);
        }
        bool Solid(int x, int z) => items.Any(it => !it.passable && it.Contains(x, z));
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (!g.Floor(nx, nz) || reach[nx, nz] || Solid(nx, nz)) continue;
                reach[nx, nz] = true;
                queue.Enqueue((nx, nz));
            }
        }
        foreach (var it in items)
        {
            if (it.fixedPlace || it.stranded) continue;
            var spots = it.SpotCells();
            if (spots.Count > 0)
            {
                if (spots.Any(s => !reach[s.x, s.z])) return $"item {it.id}'s work spot can't be reached";
                continue;
            }
            bool ok = false;
            for (int x = it.minX - 1; x <= it.minX + it.w && !ok; x++)
                for (int z = it.minZ - 1; z <= it.minZ + it.h && !ok; z++)
                    if (g.In(x, z) && reach[x, z] && (x >= it.minX && x < it.minX + it.w || z >= it.minZ && z < it.minZ + it.h)) ok = true;
            if (!ok) return $"item {it.id} ({it.kind}) can't be reached";
        }
        return null;
    }

    private static IEnumerable<RoomArrangeMath.Item> benches2(params RoomArrangeMath.Item[] items) => items;

    private static bool HeadToWall(RoomArrangeMath.Grid g, RoomArrangeMath.Item bed)
    {
        int back = (bed.rot + 2) % 4;
        foreach (var (hx, hz) in bed.HeadCells())
        {
            TemplateLayout.Step(back, out int dx, out int dz);
            if (!g.Wall(hx + dx, hz + dz)) return false;
        }
        return true;
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "rearranging a room: " + what);
        }

        // A bedroom with the bed in the middle, a table off in a corner and a lamp in the doorway.
        {
            var g = Room(
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
                "#.....#",
                "#..D..#",
                "###+###");
            var bed = Bed(3, 2, 1);
            var side = Item(RoomArrangeMath.BedSide, 1, 4);
            var lamp = Item(RoomArrangeMath.Decor, 3, 1);
            var items = new List<RoomArrangeMath.Item> { bed, side, lamp };
            var notes = RoomArrangeMath.Arrange(g, items);
            Dump("bedroom", g, items, notes);
            C(Sound(g, items) == null, "the bedroom is sound: " + Sound(g, items));
            C(HeadToWall(g, bed), $"the bed's head goes to a wall (at {bed.x},{bed.z} facing {bed.rot})");
            bool byHead = bed.HeadCells().Any(hc => Math.Abs(hc.x - side.x) + Math.Abs(hc.z - side.z) == 1);
            C(byHead, $"the bedside table goes by the bed's head (table at {side.x},{side.z}, head {string.Join(" ", bed.HeadCells())})");
            C(side.why == "bedside", "and says so");
            C(lamp.moved && !g.IsDoorInside(lamp.x, lamp.z), "the lamp leaves the doorway");
            int wallsByLamp = new[] { (lamp.x + 1, lamp.z), (lamp.x - 1, lamp.z), (lamp.x, lamp.z + 1), (lamp.x, lamp.z - 1) }.Count(c => g.Wall(c.Item1, c.Item2));
            C(wallsByLamp >= 2, "the lamp goes to a corner");
            C(RoomArrangeMath.Moves(items) == 3, $"all three move ({RoomArrangeMath.Moves(items)})");
        }

        // A bedroom already in order stays as it is.
        {
            var g = Room(
                "#####",
                "#...#",
                "#...#",
                "#..D#",
                "###+#");
            var bed = Bed(1, 1, 0);
            var side = Item(RoomArrangeMath.BedSide, 2, 1);
            var items = new List<RoomArrangeMath.Item> { bed, side };
            Dump("tidy bedroom", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the tidy bedroom is sound: " + Sound(g, items));
            C(!bed.moved && !side.moved, $"a bed against the wall with its table beside it stays put (bed {bed.x},{bed.z} r{bed.rot}; table {side.x},{side.z})");
            C(bed.why == "kept" && side.why == "kept", "and both say they were kept");
        }

        // A dining room: chairs end up at the table, facing it.
        {
            var g = Room(
                "########",
                "#......#",
                "#......#",
                "#......#",
                "#......#",
                "#D.....#",
                "#+######");
            var table = Item(RoomArrangeMath.Table, 1, 4, 0, 2, 2);
            var chairs = new List<RoomArrangeMath.Item>();
            for (int i = 0; i < 4; i++) chairs.Add(Item(RoomArrangeMath.Chair, 5, 1 + i, 0, 1, 1, true));
            var items = new List<RoomArrangeMath.Item> { table };
            items.AddRange(chairs);
            Dump("dining room", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the dining room is sound: " + Sound(g, items));
            int facing = chairs.Count(ch =>
            {
                TemplateLayout.Step(ch.rot, out int dx, out int dz);
                return table.Contains(ch.x + dx, ch.z + dz);
            });
            C(facing == 4, $"all four chairs face the table ({facing} do)");
            C(chairs.All(ch => ch.why == "table"), "and say so");
            bool central = table.minX > 0 && table.minZ > 0 && table.minX + table.w < g.w - 1 && table.minZ + table.h < g.h - 1;
            C(central, $"the table stands clear of the walls ({table.minX},{table.minZ})");
        }

        // A workshop: benches against the walls with their work spot free, a tool cabinet against a wall, the doorway clear.
        {
            var g = Room(
                "##########",
                "#........#",
                "#........#",
                "#........#",
                "#........#",
                "#...D....#",
                "####+#####");
            var b1 = Bench(4, 3, 0);
            var b2 = Bench(4, 1, 2);
            var cab = Item(RoomArrangeMath.WallHugger, 4, 4);
            var items = new List<RoomArrangeMath.Item> { b1, b2, cab };
            Dump("workshop", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the workshop is sound: " + Sound(g, items));
            foreach (var b in new[] { b1, b2 })
            {
                int back = RoomArrangeMath.BackOf(b);
                TemplateLayout.Step(back, out int dx, out int dz);
                bool backed = true;
                for (int x = b.minX; x < b.minX + b.w; x++)
                    for (int z = b.minZ; z < b.minZ + b.h; z++)
                        if (b.Contains(x + dx, z + dz)) continue;
                        else if (!g.Wall(x + dx, z + dz)) backed = false;
                C(backed, $"bench {b.id} has its back to a wall (at {b.minX},{b.minZ} facing {b.rot}, back {back})");
                var spot = b.SpotCells()[0];
                C(g.Floor(spot.x, spot.z) && !items.Any(o => o != b && !o.passable && o.Contains(spot.x, spot.z)), "its work spot is free");
            }
            C(cab.why == "wall" || cab.why == "corner", "the cabinet goes to a wall: " + cab.why);
        }

        // A store room: shelves line the walls in runs; a shelf that can't move (full of goods) stays and is worked around.
        {
            var g = Room(
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#...D...#",
                "####+####");
            var full = Item(RoomArrangeMath.Storage, 3, 3, 0, 1, 2, false, null, true);
            var shelves = new List<RoomArrangeMath.Item>();
            for (int i = 0; i < 5; i++) shelves.Add(Item(RoomArrangeMath.Storage, 1 + i, 2, 0, 1, 2));
            var items = new List<RoomArrangeMath.Item> { full };
            items.AddRange(shelves);
            Dump("store room", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the store room is sound: " + Sound(g, items));
            C(full.x == 3 && full.z == 3 && !full.moved, "the full shelf stays where it is");
            int onWalls = shelves.Count(s => s.why == "wall" || s.why == "run");
            C(onWalls == 5, $"every movable shelf stands along a wall ({onWalls} of 5)");
            C(shelves.Count(s => s.why == "run") >= 2, "and some stand in a run with another");
        }

        // Two doors: nothing may cut one off from the other; a crammed passage room still works.
        {
            var g = Room(
                "#######",
                "#.....#",
                "#.....D",
                "#.....#",
                "#..D..#",
                "###+###");
            var items = new List<RoomArrangeMath.Item>();
            for (int i = 0; i < 4; i++) items.Add(Item(RoomArrangeMath.WallHugger, 1 + i, 2, 0, 1, 1));
            items.Add(Item(RoomArrangeMath.Storage, 2, 3, 0, 1, 2));
            Dump("passage room", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the passage room is sound: " + Sound(g, items));
        }

        // A room so full that something has nowhere to go: it stays, and the rest is placed around it.
        {
            var g = Room(
                "#####",
                "#...#",
                "#...#",
                "#.D.#",
                "##+##");
            var items = new List<RoomArrangeMath.Item>
            {
                Item(RoomArrangeMath.Storage, 1, 1, 0, 1, 2),
                Item(RoomArrangeMath.Storage, 3, 1, 0, 1, 2),
                Item(RoomArrangeMath.WallHugger, 2, 2),
                Item(RoomArrangeMath.WallHugger, 1, 3),
                Item(RoomArrangeMath.WallHugger, 3, 3),
                Item(RoomArrangeMath.Decor, 2, 3)
            };
            Dump("crammed room", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the crammed room is still sound: " + Sound(g, items));
            C(items.Count(it => it.stranded) >= 1, "with no room to get at everything, something stays where it stands");
        }

        // Heavy pieces only go where the floor takes them.
        {
            var g = Room(
                "#######",
                "#HHH..#",
                "#HHH..#",
                "#.....#",
                "#..D..#",
                "###+###");
            var heavy = Item(RoomArrangeMath.Bench, 4, 1, 0, 3, 1, false, (0, -1));
            heavy.heavy = true;
            var items = new List<RoomArrangeMath.Item> { heavy };
            Dump("heavy room", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the heavy room is sound: " + Sound(g, items));
            bool onHeavy = true;
            for (int x = heavy.minX; x < heavy.minX + heavy.w; x++)
                for (int z = heavy.minZ; z < heavy.minZ + heavy.h; z++)
                    if (!g.Heavy(x, z)) onHeavy = false;
            C(onHeavy, $"the heavy bench stands on the heavy floor ({heavy.minX},{heavy.minZ} {heavy.w}x{heavy.h})");
        }

        // The walk from the door: a bed goes to the near end of a long room, head still to a wall.
        {
            var g = Room(
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#D..#",
                "#...#",
                "#####");
            var bed = Bed(2, 8, 0);
            var items = new List<RoomArrangeMath.Item> { bed };
            Dump("long bedroom", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the long bedroom is sound: " + Sound(g, items));
            C(HeadToWall(g, bed), "the bed's head goes to a wall");
            C(bed.minZ <= 3, $"the bed goes to the end of the room by the door (minZ {bed.minZ})");
            C(RoomArrangeMath.DoorWalk(g, bed) <= 3, $"the walk from the door to the bed is short ({RoomArrangeMath.DoorWalk(g, bed)})");
        }

        // A big workshop: more benches than the walls hold; the rest stand back to back in islands, none in the open.
        {
            var g = Room(
                "###########",
                "#.........#",
                "#.........#",
                "#.........#",
                "#.........#",
                "#.........#",
                "#.........#",
                "#.........#",
                "#.........#",
                "#....D....#",
                "#####+#####");
            var benches = new List<RoomArrangeMath.Item>();
            for (int i = 0; i < 10; i++) benches.Add(Bench(2 + (i % 3) * 3, 2 + i / 3 * 2, 0));
            var items = new List<RoomArrangeMath.Item>(benches);
            Dump("big workshop", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the big workshop is sound: " + Sound(g, items));
            int backed = benches.Count(b => b.why == "wall" || b.why == "island");
            C(backed >= 8, $"nearly every bench has its back to a wall or another bench ({backed} of 10)");
            C(benches.Any(b => b.why == "island"), "some stand back to back in an island");
            var spots = benches.SelectMany(b => b.SpotCells()).ToList();
            C(spots.Distinct().Count() == spots.Count, "no two work spots share a cell");
        }

        // A tool cabinet goes where it reaches the benches it serves; a stool sits on a bench's work spot facing it; a shelf stands near the bench.
        {
            var g = Room(
                "#################",
                "#...............#",
                "#...............#",
                "#...............#",
                "#...............#",
                "#...D...........#",
                "#####+###########");
            var b1 = Bench(2, 5, 0);
            var b2 = Bench(6, 5, 0);
            var cab = Item(RoomArrangeMath.WallHugger, 15, 1);
            cab.reach = 8f;
            cab.links = new HashSet<int> { b1.id, b2.id };
            var stool = Item(RoomArrangeMath.Chair, 15, 3, 0, 1, 1, true);
            var shelf = Item(RoomArrangeMath.Storage, 14, 2, 0, 1, 2);
            var items = new List<RoomArrangeMath.Item> { b1, b2, cab, stool, shelf };
            Dump("workshop fittings", g, items, RoomArrangeMath.Arrange(g, items));
            C(Sound(g, items) == null, "the fitted workshop is sound: " + Sound(g, items));
            C(!b1.moved && !b2.moved, "the benches along the wall stay");
            C(RoomArrangeMath.Distance(cab, b1) <= 8f && RoomArrangeMath.Distance(cab, b2) <= 8f, $"the cabinet reaches both benches ({cab.x},{cab.z})");
            C(cab.why == "serves", "and says so: " + cab.why);
            bool onSpot = benches2(b1, b2).Any(b => b.SpotCells().Contains((stool.x, stool.z)) && b.Contains(stool.x + 0, stool.z + 1) && stool.rot == 0);
            C(onSpot, $"the stool sits on a bench's work spot facing it ({stool.x},{stool.z} r{stool.rot}, {stool.why})");
            C(RoomArrangeMath.Distance(shelf, b1) <= 5f || RoomArrangeMath.Distance(shelf, b2) <= 5f, $"the shelf stands near a bench ({shelf.minX},{shelf.minZ})");
        }

        // The order the moves go in: a piece whose new cells are free goes first; two that swap places need one set aside.
        {
            var a = new RoomArrangeMath.Item { id = 1, curX = 0, curZ = 0, moved = true, x = 1, z = 0, minX = 1, minZ = 0, w = 1, h = 1 };
            var b = new RoomArrangeMath.Item { id = 2, curX = 1, curZ = 0, moved = true, x = 2, z = 0, minX = 2, minZ = 0, w = 1, h = 1 };
            var c = new RoomArrangeMath.Item { id = 3, curX = 5, curZ = 5, moved = true, x = 6, z = 5, minX = 6, minZ = 5, w = 1, h = 1 };
            var order = RoomArrangeMath.MoveOrder(new List<RoomArrangeMath.Item> { a, b, c }, out var aside);
            C(order.IndexOf(1) < order.IndexOf(0), "the piece standing on another's new cells moves first");
            C(aside.Count == 0, "a chain needs nothing set aside");
            var p = new RoomArrangeMath.Item { id = 1, curX = 0, curZ = 0, moved = true, x = 1, z = 0, minX = 1, minZ = 0, w = 1, h = 1 };
            var q = new RoomArrangeMath.Item { id = 2, curX = 1, curZ = 0, moved = true, x = 0, z = 0, minX = 0, minZ = 0, w = 1, h = 1 };
            order = RoomArrangeMath.MoveOrder(new List<RoomArrangeMath.Item> { p, q }, out aside);
            C(aside.Count == 1 && order.Count == 2, $"two pieces swapping places need one set aside ({aside.Count})");
        }

        // The same room gives the same answer twice.
        {
            var g = Room(
                "########",
                "#......#",
                "#......#",
                "#......#",
                "#..D...#",
                "###+####");
            List<RoomArrangeMath.Item> Make() => new List<RoomArrangeMath.Item>
            {
                Item(RoomArrangeMath.Bed, 3, 2, 0, 1, 2), Item(RoomArrangeMath.BedSide, 1, 1), Item(RoomArrangeMath.Decor, 5, 3), Item(RoomArrangeMath.WallHugger, 4, 1)
            };
            nextId = 100;
            var one = Make();
            nextId = 100;
            var two = Make();
            RoomArrangeMath.Arrange(g, one);
            RoomArrangeMath.Arrange(g, two);
            bool same = true;
            for (int i = 0; i < one.Count; i++)
                if (one[i].x != two[i].x || one[i].z != two[i].z || one[i].rot != two[i].rot) same = false;
            C(same, "the arrangement is the same each time");
        }
        return n;
    }
}
