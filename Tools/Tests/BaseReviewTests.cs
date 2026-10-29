using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The base looked over (Building/BaseReviewMath): rooms far apart and close together, long walks to meals, doors
/// that separate nothing, stubs of wall, dead ends of conduit and pipe, a freezer door into the warm.
/// </summary>
public static class BaseReviewTests
{
    /// <summary>Cells marked with <paramref name="mark"/> in a drawing, (x, z) with z up.</summary>
    private static List<(int x, int z)> Cells(char mark, params string[] rows)
    {
        var list = new List<(int, int)>();
        int h = rows.Length;
        for (int r = 0; r < h; r++)
            for (int x = 0; x < rows[r].Length; x++)
                if (rows[r][x] == mark) list.Add((x, h - 1 - r));
        return list;
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the base looked over: " + what);
        }

        // Rooms far apart and close together.
        {
            var kf = BaseReviewMath.NearRules.First(r => r.key == "kitchenfreezer");
            var kitchens = new List<(float x, float z)> { (10f, 10f) };
            var near = new List<(float x, float z)> { (14f, 10f) };
            var far = new List<(float x, float z)> { (40f, 10f), (60f, 60f) };
            C(BaseReviewMath.TooFar(kf, kitchens, near).Count == 0, "a freezer four cells from the kitchen is near enough");
            var found = BaseReviewMath.TooFar(kf, kitchens, far);
            C(found.Count == 1 && found[0].b == 0 && found[0].distance == 30f, "a freezer thirty cells off is far, measured to the nearest one");
            C(BaseReviewMath.TooFar(kf, kitchens, new List<(float x, float z)>()).Count == 0, "no freezer at all is not a far freezer (the advisor's business)");
            C(BaseReviewMath.FarScore(kf, 30f) > BaseReviewMath.FarScore(kf, 21f) && BaseReviewMath.FarScore(kf, 200f) <= kf.weight * 2f, "farther matters more, up to twice the rule's weight");
            var bw = BaseReviewMath.ApartRules.First(r => r.key == "bedroomworkshop");
            var beds = new List<(float x, float z)> { (0f, 0f), (5f, 0f), (40f, 0f) };
            var shops = new List<(float x, float z)> { (3f, 3f) };
            var close = BaseReviewMath.TooClose(bw, beds, shops);
            C(close.Count == 2 && close.All(c => c.a != 2), "two bedrooms by the workshop are close, the far one isn't");
            C(BaseReviewMath.CloseScore(bw, 2) > BaseReviewMath.CloseScore(bw, 1), "more bedrooms by it matter more");
        }

        // Long walks to meals.
        {
            var beds = new List<(float x, float z)> { (0f, 0f), (0f, 10f), (0f, 80f) };
            var dining = new List<(float x, float z)> { (0f, 5f) };
            float walk = BaseReviewMath.MealWalk(beds, dining);
            C(Math.Abs(walk - (5f + 5f + 75f) / 3f) < 0.01f, $"the meal walk is the average to the nearest dining room ({walk})");
            C(BaseReviewMath.MealWalk(beds, new List<(float x, float z)>()) < 0f, "no dining room: no walk to speak of");
        }

        // Doors.
        {
            C(BaseReviewMath.DoorPointless(3, 3, true, true, 2), "a door with the same indoor room on both sides separates nothing");
            C(!BaseReviewMath.DoorPointless(3, 4, true, true, 2), "a door between two rooms is a door");
            C(!BaseReviewMath.DoorPointless(7, 7, false, false, 2), "a gate in a perimeter wall, open air both sides, is left alone");
            C(BaseReviewMath.DoorPointless(3, 4, true, true, 4), "a door with no wall beside it separates nothing");
            C(!BaseReviewMath.DoorPointless(-1, -1, false, false, 2), "a door with nothing on either side yet is left alone");
        }

        // Stubs of wall: a proper room with a two-cell stub into it and a free-standing pillar.
        {
            string[] room =
            {
                "#########",
                "#.......#",
                "###.....#",
                "#.......#",
                "#...#...#",
                "#.......#",
                "#########"
            };
            var walls = Cells('#', room);
            var set = new HashSet<(int x, int z)>(walls);
            // The outer ring separates the room from the open; anything inside does not.
            bool Separates((int x, int z) c) => c.x == 0 || c.z == 0 || c.x == 8 || c.z == 6;
            var stubs = BaseReviewMath.Stubs(walls, Separates);
            C(stubs.Count == 1 && stubs[0].Count == 2, $"the two-cell stub off the west wall is found, the pillar in the middle is not ({stubs.Count} stub(s))");
            C(stubs.Count == 1 && stubs[0].All(c => c.z == 4 && (c.x == 1 || c.x == 2)), "and it is the right two cells");
            string[] partition =
            {
                "#########",
                "#.......#",
                "#.#####.#",
                "#.......#",
                "#########"
            };
            var walls2 = Cells('#', partition);
            bool Sep2((int x, int z) c) => c.x == 0 || c.z == 0 || c.x == 8 || c.z == 4;
            C(BaseReviewMath.Stubs(walls2, Sep2).Count == 0, "a five-cell partition isn't a stub");
            string[] attached =
            {
                "#########",
                "#.......#",
                "###.....#",
                "#.......#",
                "#########"
            };
            var walls3 = Cells('#', attached);
            bool Sep3((int x, int z) c) => c.x == 0 || c.z == 0 || c.x == 8 || c.z == 4;
            var stubs3 = BaseReviewMath.Stubs(walls3, Sep3);
            C(stubs3.Count == 1 && stubs3[0].Count == 2, "a two-cell stub off the west wall at (1,2)-(2,2) is found");
        }

        // Dead ends of conduit: 'c' conduit, 'L' a conduit under a lamp (used).
        {
            string[] net =
            {
                "..........",
                "..c.......",
                "..c.......",
                "cccLcccc..",
                "......c...",
                "......c...",
                "..........",
                ".cc.......",
            };
            var all = Cells('c', net).Concat(Cells('L', net)).ToList();
            var used = new HashSet<(int x, int z)>(Cells('L', net));
            var dead = BaseReviewMath.DeadEnds(all, c => used.Contains(c));
            // The two-cell line at the bottom left is a dead stretch on its own; the branch up from the fork at (2,4), the tail
            // west of it, the branch down from (6,4) and the tail east all lead nowhere, and once they're gone so do the forks
            // and the stretch between the lamp and the eastern fork; nothing is used but the lamp's cell.
            int cells = dead.Sum(d => d.Count);
            C(dead.Count >= 4, $"the branches off the lamp's line are dead ends ({dead.Count} runs)");
            C(dead.Any(d => d.Count == 2 && d.All(c => c.z == 0)), "a stretch joined to nothing at all is a dead end on its own");
            C(dead.All(d => !d.Contains((3, 4))), "the used cell is never among them");
            C(cells == all.Count - 1, $"everything but the lamp's cell is dead, forks included once their branches are gone ({cells} of {all.Count})");
            string[] ring =
            {
                "ccc",
                "c.c",
                "ccc"
            };
            C(BaseReviewMath.DeadEnds(Cells('c', ring), c => false).Count == 0, "a ring has no ends to find (the whole net is judged instead)");
            string[] fed =
            {
                "cccL"
            };
            var fedDead = BaseReviewMath.DeadEnds(Cells('c', fed).Concat(Cells('L', fed)).ToList(), c => c.x == 3);
            C(fedDead.Count == 1 && fedDead[0].Count == 3, "a line that only feeds at its very end is dead up to that end");
            var line = BaseReviewMath.DeadEnds(Cells('c', fed).Concat(Cells('L', fed)).ToList(), c => c.x == 0);
            C(line.Count == 1 && line[0].Count == 3 && !line[0].Contains((0, 0)), "fed at the other end, the rest is the dead stretch");
        }

        // Freezer doors.
        {
            C(BaseReviewMath.AirlockWanted(-10f, 21f, 40, 1, false), "a freezer door into a warm room wants an airlock");
            C(!BaseReviewMath.AirlockWanted(-10f, 21f, 4, 2, false), "a little lobby with two doors is the airlock");
            C(!BaseReviewMath.AirlockWanted(-10f, -5f, 40, 1, false), "a door into another cold room is fine");
            C(BaseReviewMath.AirlockWanted(-10f, 0f, 400, 1, true), "a door straight into the open air wants one");
            C(!BaseReviewMath.AirlockWanted(5f, 21f, 40, 1, false), "a cool room that isn't a freezer doesn't");
        }

        C(BaseReviewMath.TidyScore(1, 0.06f) < BaseReviewMath.TidyScore(3, 0.06f) && BaseReviewMath.TidyScore(100, 0.06f) == 0.3f, "tidy-up findings grow with the count and stop at the cap");
        return n;
    }
}
