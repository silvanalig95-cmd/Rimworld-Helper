using ColonyCouncil;

/// <summary>
/// Wall lamps (Building/LightMath.cs): which light a room gets for its size, how many lamps and where, and how they move
/// when the map gets in the way. The layouts themselves (every template, turn and mirror, with a strong and a weak lamp)
/// run through RoomTemplateTests and RoomThemeTests, which call <see cref="CheckLamps"/> on each and check in
/// CheckWorld that every lamp stands on the floor in front of a wall and faces it, never on or beside a door.
/// </summary>
public static class WallLampTests
{
    /// <summary>No wall lamp to build (the plan's floor lights), a strong lamp (glow radius 12), the weakest that still counts as a light (4).</summary>
    public static readonly float[] Reaches = { 0f, LightMath.Reach(12f), LightMath.Reach(4f) };

    public static string Label(float reach) => reach <= 0f ? "" : $", wall lamps lighting {reach} cells";

    private static bool IsLamp(TemplateLayout.Piece p) => p.role != null && p.role.EndsWith("(on the wall)");

    /// <summary>
    /// One laid-out room: wall lamps only when the colony can build one; a small or medium room hangs them in place of
    /// its plain lights, a large one keeps its standing lamps; a floor light never stays in an area that got lamps; a
    /// light that is part of the room's look stays; no area gets more lamps than its size allows, and with fewer than
    /// that every cell of it is lit well.
    /// </summary>
    public static void CheckLamps(RoomTemplateTests.Template t, List<(TemplateLayout.Piece piece, RoomTemplateTests.Fact fact)> pieces,
        Func<RoomTemplateTests.Thing, string> pick, int style, float reach, string where, Action<bool, string> check)
    {
        var lamps = pieces.Where(p => IsLamp(p.piece)).Select(p => p.piece).ToList();
        if (reach <= 0f)
        {
            check(lamps.Count == 0, $"{where}: wall lamps although the colony can build none");
            return;
        }
        var others = pieces.Where(p => !IsLamp(p.piece)).Select(p => p.piece).ToList();
        var area = LightMath.Areas(t.w, t.h, others, out int areas);
        var lampsIn = new int[areas];
        foreach (var l in lamps)
        {
            int a = area[l.minX, l.minZ];
            check(a >= 0, $"{where}: a wall lamp at ({l.minX},{l.minZ}) stands under a wall");
            if (a >= 0) lampsIn[a]++;
            check(!others.Any(o => !o.inWall && o.Contains(l.minX, l.minZ)), $"{where}: a wall lamp at ({l.minX},{l.minZ}) shares its cell");
        }
        check(lamps.Select(l => (l.minX, l.minZ)).Distinct().Count() == lamps.Count, $"{where}: two wall lamps on one cell");

        // The plan's lights in this style, and which of them may move to the wall.
        var inStyle = t.things.Where(th => TemplateLayout.InStyle(th.tier, th.role, th.optional, style)).ToList();
        var plain = inStyle.Where(th => th.MayHang(pick(th))).ToList();
        var size = LightMath.SizeOf(t.w * t.h);
        if (plain.Count == 0)
        {
            check(lamps.Count == 0, $"{where}: wall lamps in a room whose lights are all part of its look");
            return;
        }
        var floorLights = pieces.Where(p => p.fact.kind != "walllight" && plain.Any(th => p.piece.slot == t.things.IndexOf(th))).Select(p => p.piece).ToList();
        if (size == LightMath.RoomSize.Large && floorLights.Count > 0)
            check(lamps.Count == 0, $"{where}: a large room hung wall lamps although its standing lamps stand");
        if (size != LightMath.RoomSize.Large)
            check(lamps.Count > 0 || floorLights.Count > 0, $"{where}: a {size} room with lights in its plan ended up with none");
        foreach (var f in floorLights)
        {
            int a = area[f.minX, f.minZ];
            check(a < 0 || lampsIn[a] == 0, $"{where}: a floor light at ({f.minX},{f.minZ}) stayed beside wall lamps");
        }
        // Lights that are the room's look (a brazier by a throne) stay where the plan puts them.
        foreach (var th in inStyle.Where(th => th.role == "light" && !th.MayHang(pick(th))))
            check(pieces.Any(p => p.piece.slot == t.things.IndexOf(th)) || pick(th) == null, $"{where}: {pick(th)} at ({th.x},{th.z}) is part of the room's look and was moved");
        for (int a = 0; a < areas; a++)
        {
            if (lampsIn[a] == 0) continue;
            var cells = new List<(int x, int z)>();
            for (int x = 0; x < t.w; x++)
                for (int z = 0; z < t.h; z++)
                    if (area[x, z] == a) cells.Add((x, z));
            int max = LightMath.MaxWallLamps(cells.Count);
            check(lampsIn[a] <= max, $"{where}: {lampsIn[a]} wall lamps in an area of {cells.Count} cells (at most {max})");
            if (lampsIn[a] >= max) continue;
            // Fewer than the most it may have: then every cell is lit well, unless no free wall is left that would light more.
            var inArea = lamps.Where(l => area[l.minX, l.minZ] == a).ToList();
            var dark = cells.Where(c => !inArea.Any(l => (l.minX - c.x) * (l.minX - c.x) + (l.minZ - c.z) * (l.minZ - c.z) <= reach * reach)).ToList();
            if (dark.Count == 0) continue;
            var spots = LightMath.WallSpots(t.w, t.h, t.DoorX, others.Where(p => !p.dropped).ToList(), area).Where(s => s.area == a && !inArea.Any(l => l.minX == s.x && l.minZ == s.z));
            check(!spots.Any(s => dark.Any(c => (s.x - c.x) * (s.x - c.x) + (s.z - c.z) * (s.z - c.z) <= reach * reach)),
                $"{where}: part of an area is left dark although a free wall spot would light it");
        }
    }

    public static int Run(List<RoomTemplateTests.Template> templates, Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, what);
        }

        // Sizes, all from LightMath's thresholds.
        Check(LightMath.SizeOf(1) == LightMath.RoomSize.Small && LightMath.SizeOf(LightMath.SmallRoomMaxCells) == LightMath.RoomSize.Small, "small rooms");
        Check(LightMath.SizeOf(LightMath.SmallRoomMaxCells + 1) == LightMath.RoomSize.Medium && LightMath.SizeOf(LightMath.MediumRoomMaxCells) == LightMath.RoomSize.Medium, "medium rooms");
        Check(LightMath.SizeOf(LightMath.MediumRoomMaxCells + 1) == LightMath.RoomSize.Large, "large rooms");
        Check(LightMath.MaxWallLamps(12) == LightMath.MaxLampsSmall && LightMath.MaxWallLamps(30) == LightMath.MaxLampsMedium && LightMath.MaxWallLamps(90) == LightMath.MaxLampsLarge
              && LightMath.MaxLampsSmall < LightMath.MaxLampsMedium && LightMath.MaxLampsMedium < LightMath.MaxLampsLarge, "bigger rooms may hang more lamps");
        Check(Math.Abs(LightMath.Reach(12f) - 6f) < 1e-4f && LightMath.Reach(0f) == 0f, "a lamp lights half its glow radius well");

        // The size-based choice.
        Check(LightMath.UseWallLamps(16, true, true, LightMath.Auto) && LightMath.UseWallLamps(48, true, true, LightMath.Auto), "small and medium rooms hang wall lamps");
        Check(!LightMath.UseWallLamps(49, true, true, LightMath.Auto), "a large room keeps its standing lamps");
        Check(LightMath.UseWallLamps(90, true, false, LightMath.Auto), "a large room with no standing lamp hangs wall lamps round its walls");
        Check(!LightMath.UseWallLamps(16, false, true, LightMath.Auto) && !LightMath.UseWallLamps(16, false, false, LightMath.AskWall), "no wall lamp to build: the plan's own lights (a torch, a standing lamp)");
        Check(LightMath.UseWallLamps(90, true, true, LightMath.AskWall) && !LightMath.UseWallLamps(16, true, true, LightMath.AskFloor), "what the player picks in the planner wins");
        Check(LightMath.UseWallLamps(16, true, false, LightMath.AskFloor), "a standing lamp picked that fits nowhere still gives way to wall lamps");

        // How many and where, in an empty 5x4 room with its door in the middle of the south wall.
        {
            var none = new List<TemplateLayout.Piece>();
            var strong = LightMath.HangLamps(5, 4, 2, none, new[] { (2, 2) }, LightMath.Reach(12f));
            Check(strong.chosen.Count == 1, $"a strong lamp lights a 5x4 room alone, got {strong.chosen.Count}");
            var s = strong.candidates[strong.chosen[0]];
            Check(s.z == 3 && s.facing == 0 && s.x == 2, $"the one lamp hangs in the middle of the back wall, facing it: got {s}");
            var weak = LightMath.HangLamps(5, 4, 2, none, new[] { (2, 2) }, LightMath.Reach(4f));
            Check(weak.chosen.Count == LightMath.MaxLampsSmall, $"a weak lamp needs more of them, up to {LightMath.MaxLampsSmall} in a small room: got {weak.chosen.Count}");
            var weakMid = LightMath.HangLamps(8, 6, 3, none, new[] { (3, 3) }, LightMath.Reach(4f));
            Check(weakMid.chosen.Count > weak.chosen.Count && weakMid.chosen.Count <= LightMath.MaxLampsMedium, $"a bigger room hangs more lamps: got {weakMid.chosen.Count}");
            var cells = weakMid.chosen.Select(i => (weakMid.candidates[i].x, weakMid.candidates[i].z)).ToList();
            Check(cells.Distinct().Count() == cells.Count, "never two lamps on one cell");
            // Spread round the walls: no two lamps right next to each other when the room needs several.
            bool apart = true;
            foreach (var a in cells)
                foreach (var b in cells)
                    if (a != b && Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2) <= 1) apart = false;
            Check(apart, "lamps spread out, not in a cluster");
            foreach (var c in weak.candidates)
            {
                // Every candidate is on the edge of the floor, facing out to the ring, and never on the door (2,-1).
                TemplateLayout.Step(c.facing, out int dx, out int dz);
                int wx = c.x + dx, wz = c.z + dz;
                Check(wx < 0 || wz < 0 || wx >= 5 || wz >= 4, $"candidate {c} faces a wall");
                Check(!(wx == 2 && wz == -1) && !(c.x == 2 && c.z == 0), $"candidate {c} isn't on or beside the door");
            }
            // A cooler set in the east wall (full (6,2)) is no wall to hang a lamp on.
            var cooler = new TemplateLayout.Piece { inWall = true, minX = 6, minZ = 2, w = 1, h = 1, role = "Cooler" };
            var withCooler = LightMath.HangLamps(5, 4, 2, new List<TemplateLayout.Piece> { cooler }, new[] { (2, 2) }, LightMath.Reach(4f));
            Check(!withCooler.candidates.Any(c => c.x == 4 && c.z == 1 && c.facing == 1), "no lamp on a cooler in the wall");
            // A piece on the floor takes its cell and its work spot.
            var bench = new TemplateLayout.Piece { minX = 0, minZ = 3, w = 3, h = 1, facing = 0, sizeX = 3, sizeZ = 1, rot = 0, x = 1, z = 3 };
            bench.spots.Add((0, -1));
            var withBench = LightMath.HangLamps(5, 4, 2, new List<TemplateLayout.Piece> { bench }, new[] { (2, 2) }, LightMath.Reach(12f));
            Check(!withBench.candidates.Any(c => c.z == 3 && c.x <= 2) && !withBench.candidates.Any(c => c.x == 1 && c.z == 2), "no lamp on a bench or its work spot");
        }

        // Partition walls: every area with a light gets its own lamps (a cell block's cells; not its corridor).
        var byName = templates.ToDictionary(t => t.defName);
        if (byName.TryGetValue("Council_CellBlock", out var block))
        {
            var (pieces, _) = RoomTemplateTests.Arrange(block, th => RoomTemplateTests.Pick(th, false), TemplateLayout.Comfortable, TemplateLayout.Lavish, LightMath.Reach(12f));
            var others = pieces.Where(p => !IsLamp(p.piece)).Select(p => p.piece).ToList();
            var area = LightMath.Areas(block.w, block.h, others, out int areas);
            var lit = pieces.Where(p => IsLamp(p.piece)).Select(p => area[p.piece.minX, p.piece.minZ]).Distinct().Count();
            Check(areas == 4 && lit == 3, $"a cell block lights each of its three cells, not the corridor: {areas} areas, {lit} lit");
        }
        if (byName.TryGetValue("Council_GreatHall", out var hall))
        {
            var (pieces, _) = RoomTemplateTests.Arrange(hall, th => RoomTemplateTests.Pick(th, false), TemplateLayout.Comfortable, TemplateLayout.Lavish, LightMath.Reach(12f));
            Check(!pieces.Any(p => IsLamp(p.piece)), "a great hall keeps its braziers and standing lamps");
        }

        // Lights that are part of the room's look: a brazier listed first by a throne or an altar stays on the floor.
        foreach (var t in templates)
            foreach (var th in t.things.Where(th => th.role == "light" && th.defs.Count > 0 && (th.defs[0] == "Brazier" || th.defs[0] == "DarklightBrazier")))
                Check(th.keepOnFloor, $"{t.file}: {t.defName} light at ({th.x},{th.z}) lists a brazier first: mark it keepOnFloor so it stays by the throne or altar");

        // Obstacles on the map: lamps whose spot is unusable move to usable spots in their own area, never onto a cell
        // another lamp keeps; lamps that stay usable don't move.
        var rng = new Random(1234);
        int moved = 0;
        foreach (var t in templates)
        {
            foreach (float reach in new[] { LightMath.Reach(12f), LightMath.Reach(4f) })
            {
                var others = new List<TemplateLayout.Piece>();
                var (pieces, _) = RoomTemplateTests.Arrange(t, th => RoomTemplateTests.Pick(th, false), TemplateLayout.Comfortable);
                others.AddRange(pieces.Select(p => p.piece));
                var lightAt = t.things.Where(th => th.role == "light").Select(th => (th.x, th.z)).ToList();
                if (lightAt.Count == 0) continue;
                var plan = LightMath.HangLamps(t.w, t.h, t.DoorX, others.Where(p => !p.role.StartsWith("StandingLamp") && !p.role.StartsWith("TorchLamp")).ToList(), lightAt, reach);
                if (plan.chosen.Count == 0) continue;
                for (int trial = 0; trial < 6; trial++)
                {
                    var bad = new HashSet<int>();
                    for (int i = 0; i < plan.candidates.Count; i++)
                        if (rng.NextDouble() < (trial == 0 ? 0.0 : trial * 0.15)) bad.Add(i);
                    var result = LightMath.Relocate(plan, i => !bad.Contains(i));
                    string where = $"{t.file}: {t.defName} with {bad.Count} of {plan.candidates.Count} wall spots blocked";
                    Check(result.Count == plan.chosen.Count, $"{where}: one answer per lamp");
                    var used = new HashSet<(int, int)>();
                    for (int k = 0; k < result.Count; k++)
                    {
                        int r = result[k];
                        var own = plan.candidates[plan.chosen[k]];
                        if (!bad.Contains(plan.chosen[k])) Check(r == plan.chosen[k], $"{where}: a lamp whose wall is fine moved");
                        if (r < 0)
                        {
                            Check(r == LightMath.NotNeeded || r == LightMath.Nowhere, $"{where}: odd answer {r}");
                            continue;
                        }
                        if (r != plan.chosen[k]) moved++;
                        var s = plan.candidates[r];
                        Check(!bad.Contains(r), $"{where}: a lamp moved onto a blocked spot");
                        Check(s.area == own.area, $"{where}: a lamp moved to another area");
                        Check(used.Add((s.x, s.z)), $"{where}: two lamps on one cell after moving");
                    }
                    if (trial == 0) Check(result.SequenceEqual(plan.chosen), $"{where}: nothing blocked, nothing moves");
                }
            }
        }
        Check(moved > 50, $"the obstacle check moved only {moved} lamps: it proves little");
        return n;
    }
}
