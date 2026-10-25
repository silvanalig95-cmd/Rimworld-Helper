using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Taking cover with a drag (Defense/CoverDragMath.cs, Defense/CoverDrag.cs): the drag's frame faces the enemy, each
/// colonist gets a cell of their own with the most cover they can shoot from, long guns a step back and melee fighters
/// a step forward, spread across the line in the order they stand, and never off the map or into a cell they can't
/// reach. The game side runs on the game's own drag and gives its orders without a way off the map.
/// </summary>
public static class CoverDragTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "cover drag: " + what);
        }

        // ---------- The frame ----------
        var ahead = CoverDragMath.Frame(10, 15, 10, 10, 0f, 1f);
        var right = CoverDragMath.Frame(13, 10, 10, 10, 0f, 1f);
        var behindEast = CoverDragMath.Frame(7, 10, 10, 10, 1f, 0f);
        Check(Near(ahead.depth, 5f) && Near(ahead.lateral, 0f), "a cell toward the enemy is ahead, on the line's middle");
        Check(Near(right.lateral, 3f) && Near(right.depth, 0f), "a cell to the right (facing the enemy) is to the right");
        Check(Near(behindEast.depth, -3f), "facing east, a cell to the west is behind");
        Check(CoverDragMath.HalfWidth(1) >= 4 && CoverDragMath.HalfWidth(12) >= (12 - 1) * CoverDragMath.Spacing / 2f, "the strip looked at is wide enough for everyone with room between");
        Check(Near(CoverDragMath.IdealLateral(0, 3), -2f) && Near(CoverDragMath.IdealLateral(1, 3), 0f) && Near(CoverDragMath.IdealLateral(2, 3), 2f), "places on the line are two cells apart, centred");

        // ---------- A wall in front, open ground behind ----------
        // Facing north (+z) from (10,10): a low wall's cover along depth 0 from x 6 to 14, open ground elsewhere.
        var cells = new List<CoverCell>();
        for (int x = 4; x <= 16; x++)
            for (int z = 4; z <= 14; z++)
            {
                bool wall = z == 10 && x >= 6 && x <= 14;
                bool blind = z == 10 && x == 10;
                cells.Add(new CoverCell { x = x, z = z, lateral = x - 10, depth = z - 10, cover = wall ? 0.75f : 0.05f, sees = !blind });
            }
        int Find(int x, int z) => cells.FindIndex(c => c.x == x && c.z == z);
        float[] rifles = { 30f, 22f, 22f, 22f };
        float[] spread = { -3f, -1f, 1f, 3f };
        var got = CoverDragMath.Assign(cells, rifles, spread, true, null);
        Check(got.All(i => i >= 0) && got.Distinct().Count() == got.Length, "each colonist gets a cell of their own");
        Check(got.Skip(1).All(i => cells[i].z == 10 && cells[i].cover > 0.5f), "shooters take the wall's cover");
        Check(got.All(i => cells[i].sees), "nobody is put where they can't shoot from");
        Check(cells[got[0]].z == 10 || cells[got[0]].depth <= 0f, "a long gun is never put ahead of the line");
        var laterals = got.Select(i => cells[i].lateral).ToList();
        Check(laterals.Skip(1).Zip(laterals, (b, a) => b > a).All(x => x) || got.Length < 2, "they keep the order they stood in, left to right: nobody crosses another's path");
        Check(got.All(i => got.Count(j => Math.Abs(cells[j].x - cells[i].x) <= 1 && Math.Abs(cells[j].z - cells[i].z) <= 1) == 1), "not on top of each other: no two side by side when there is room");

        // A long gun with cover at the line and a step back both equal: the step back.
        var two = new List<CoverCell>
        {
            new CoverCell { x = 0, z = 0, lateral = 0, depth = 0, cover = 0.6f, sees = true },
            new CoverCell { x = 0, z = -2, lateral = 0, depth = -2, cover = 0.6f, sees = true },
        };
        Check(CoverDragMath.Assign(two, new[] { 30f }, new[] { 0f }, true, null)[0] == 1, "a long gun takes the cell a step back");
        Check(CoverDragMath.Assign(two, new[] { 12f }, new[] { 0f }, true, null)[0] == 0, "a short gun takes the cell on the line");

        // Too far forward.
        var forward = new List<CoverCell>
        {
            new CoverCell { x = 0, z = 4, lateral = 0, depth = 4, cover = 0.9f, sees = true },
            new CoverCell { x = 0, z = 0, lateral = 0, depth = 0, cover = 0.1f, sees = true },
        };
        Check(CoverDragMath.Assign(forward, new[] { 22f }, new[] { 0f }, true, null)[0] == 1, "a shooter never runs ahead toward the enemy for cover");
        Check(CoverDragMath.Assign(forward, new[] { 1f }, new[] { 0f }, true, null)[0] == 0, "a melee fighter may step forward to cover");
        Check(CoverDragMath.Assign(forward, new[] { 22f }, new[] { 0f }, false, null)[0] == 0, "with no enemy side, only the cover around and the place count");

        // Can't get there.
        int wallMiddle = Find(9, 10);
        var refused = CoverDragMath.Assign(cells, new[] { 22f }, new[] { -1f }, true, (p, c) => c != wallMiddle);
        Check(refused[0] >= 0 && refused[0] != wallMiddle, "a cell a colonist can't reach goes to nobody, and they get the next best");
        var none = CoverDragMath.Assign(new List<CoverCell> { two[0] }, new[] { 22f, 22f }, new[] { 0f, 1f }, true, null);
        Check(none.Count(i => i >= 0) == 1 && none.Count(i => i < 0) == 1, "more colonists than cells: the rest are left for the game's own drag");
        Check(CoverDragMath.Assign(new List<CoverCell>(), new float[0], new float[0], true, null).Length == 0, "nobody to place, nothing placed");

        // ---------- The game side ----------
        string code = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Defense/CoverDrag.cs"));
        Check(code.Contains("IsExitCell") && code.Contains("PawnGotoAction(dests[i], pawns[i], dests[i])"), "orders never lead off the map, wherever the drag ended");
        Check(code.Contains("controller.StartInteraction(gather)") && code.Contains("controller.AddPawn"), "it runs on the game's own drag of several colonists (pressing, dragging and letting go as the game does)");
        Check(code.Contains("HasOwnOrders") && code.Contains("CtrlHeld"), "only with Ctrl held, and a right-click on something with its own orders is the game's");
        foreach (var patch in new[] { "Patch_CoverDragStart", "Patch_CoverDragCells", "Patch_CoverDragOrders", "Patch_CoverDragEnd", "Patch_CoverDragLabels" })
            Check(code.Contains("class " + patch), "its hook " + patch + " is there");
        Check(code.Contains("RimfolkPatches.Applied.Contains(nameof(Patch_CoverDragOrders))"), "it starts only when the hooks that give the orders were applied");
        return cases;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
}
