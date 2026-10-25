using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The defense helper's reading of a line against the ways in (Source/ColonyCouncil/Defense/DefenseLineMath.cs and
/// DefenseApproach.cs): the door rule (a line's gate is its way through, shut or not; a door held open is open; other
/// doors still cost raiders a door to break), gaps a line already holds (the same cells, the same pass a few cells
/// off, the same ways in) and real new gaps, the way raiders walk up to a gate, where a trap corridor goes on it (in
/// front, its lane on the way, leaning on the rock, never across the line) and why it doesn't fit when it doesn't, and
/// walls that would send raiders another way.
/// </summary>
public static class DefenseApproachTests
{
    private static DefenseMath.MapGrid Map(int w, int h, Func<int, int, char> at) => DefenseTests.Map(w, h, at);

    /// <summary>The trap corridor's layout from its def (so the tests follow the XML: drawn for a small raid), else the one it had when they were written.</summary>
    private static DefenseLayout.Grid Corridor(string root)
    {
        var t = DefenseTests.Load(root).FirstOrDefault(x => x.defName == "Rimfolk_Defense_TrapCorridor");
        if (t?.shape != null) return DefenseShapes.Build(t.shape, new DefenseShapeSpec { raidPoints = 500f }).grid;
        var rows = t?.rows ?? new List<string> { "W..W", "WT.W", "W.FW", "WT.W", "W.FW", "WT.W", "W.FW", "WT.W", "W..W" };
        return DefenseLayout.Parse(rows);
    }

    private static IEnumerable<(int x, int z, char c)> Cells(DefenseLayout.Grid t, DefenseApproach.Spot s)
    {
        for (int x = 0; x < t.W; x++)
            for (int z = 0; z < t.H; z++)
            {
                DefenseLayout.ToWorld(t, x, z, s.mirror, s.turn, out int ox, out int oz);
                yield return (s.anchorX + ox, s.anchorZ + oz, t.cells[x, z]);
            }
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "defense approach: " + what);
        }

        // ---------- The door rule ----------
        // An open strip along the north edge (x 0..40) feeds two passes: A (x 16..20) with a line across it at z = 36 and a
        // shut door at x = 18, and B (x 30..32), a little further from the beds (near x = 18).
        DefenseMath.MapGrid TwoPasses() => Map(60, 60, (x, z) =>
        {
            if (z >= 48) return x <= 40 ? '.' : '#';
            if (x >= 16 && x <= 20 && z >= 25) return z == 36 ? (x == 18 ? 'D' : 'W') : '.';
            if (x >= 30 && x <= 32 && z >= 25) return '.';
            if (x >= 5 && x <= 40 && z >= 5 && z <= 24) return x >= 16 && x <= 20 && z >= 10 && z <= 14 ? 'c' : x >= 12 && x <= 24 && z >= 8 && z <= 16 ? 'h' : '.';
            return '#';
        });
        int gateA = 36 * 60 + 18;
        int ThroughA(DefenseMath.Result r) => r.walked.Count(w => w.Contains(gateA));
        int ThroughB(DefenseMath.Result r) => r.walked.Count(w => w.Any(c => c % 60 >= 30 && c % 60 <= 32 && c / 60 == 36));
        {
            var shut = TwoPasses();
            var r = DefenseMath.Analyse(shut);
            Check(ThroughB(r) > ThroughA(r), $"door rule: a shut door that isn't a line's sends the walks to the other pass ({ThroughA(r)} through the door, {ThroughB(r)} by B)");
            var lined = TwoPasses();
            int marked = DefenseLineMath.MarkLineDoors(lined, 18, 36, 0);
            Check(marked == 1 && lined.free[gateA], "door rule: the line's gate is marked as its way through");
            var r2 = DefenseMath.Analyse(lined);
            Check(ThroughA(r2) > ThroughA(r) && ThroughA(r2) >= ThroughB(r2), $"door rule: as a line's gate, the way in runs through it ({ThroughA(r2)} through it, {ThroughB(r2)} by B)");
            Check(r2.chokes.Count > 0 && r2.chokes.Any(c => c.span.Contains(gateA)), "door rule: the gate is one of the narrow places, not left out for another");
            Check(r2.walked.Count == r2.walks && r2.chokes.All(c => c.walks.Count == c.routes && c.walks.All(w => w >= 0 && w < r2.walked.Count)), "the walks are kept, and each gap knows its own");
            // A door held open anywhere is open too.
            var open = TwoPasses();
            open.free[gateA] = true;
            Check(ThroughA(DefenseMath.Analyse(open)) == ThroughA(r2), "door rule: a door held open counts as open");
            Check(DefenseMath.StepCost(shut, gateA, 0) == DefenseMath.Ortho + DefenseMath.DoorCost && DefenseMath.StepCost(lined, gateA, 0) == DefenseMath.Ortho,
                "door rule: only a door raiders must break costs them a door");
        }
        // A double gate: the door right behind the gate is the line's too; a door further in isn't.
        {
            var m = Map(30, 30, (x, z) =>
            {
                if (z == 20 && x >= 10 && x <= 20) return x == 15 ? 'D' : 'W';
                if (z == 19 && x == 15) return 'D';
                if (z == 12 && x == 15) return 'D';
                return '.';
            });
            int n = DefenseLineMath.MarkLineDoors(m, 15, 20, 0);
            Check(n == 2 && m.free[19 * 30 + 15] && !m.free[12 * 30 + 15], $"door rule: a double gate is one gate; a door further in isn't the line's ({n})");
        }

        // ---------- Gaps a line holds already ----------
        // A valley 5 wide from the north edge, a line across it at z = 32, and the home area over the line, so the
        // survey measures the pass just outside it. It narrows to 4 a few cells further out.
        {
            DefenseMath.MapGrid Valley(bool second) => Map(60, 60, (x, z) =>
            {
                if (z == 32 && x >= 18 && x <= 22) return x == 20 ? 'D' : 'W';
                if (x >= 18 && x <= 22 && z >= 25 && z <= 33) return 'h';
                if (z == 40 && x == 22) return '#';
                if (x >= 18 && x <= 22 && z >= 25) return '.';
                if (second && x >= 44 && x <= 46 && z >= 25) return '.';
                if (x >= 5 && x <= 54 && z >= 5 && z <= 24)
                {
                    if (x >= 18 && x <= 22 && z >= 10 && z <= 12) return 'c';
                    if (x >= 12 && x <= 28 && z >= 8 && z <= 24) return 'h';
                    return '.';
                }
                return '#';
            });
            var m = Valley(true);
            DefenseLineMath.MarkLineDoors(m, 20, 32, 0);
            var r = DefenseMath.Analyse(m);
            var shape = new DefenseLineMath.LineShape { id = 7, gx = 20, gz = 32, facing = 0 };
            shape.cells = DefenseLineMath.ShapeCells(m, 20, 32, 0, Enumerable.Range(18, 5).Select(x => m.Index(x, 32)));
            shape.walks = DefenseLineMath.WalksThrough(r.walked, shape.cells, m);
            Check(shape.cells.Count == 5 && shape.walks.Count > 0, $"covered: the line's cells and the walks through it ({shape.cells.Count}, {shape.walks.Count})");
            var lines = new List<DefenseLineMath.LineShape> { shape };
            var near = r.chokes.FirstOrDefault(c => c.x >= 18 && c.x <= 22);
            var far = r.chokes.FirstOrDefault(c => c.x >= 44);
            Check(near != null && near.z > 32 && near.z <= 44, $"covered: the survey finds the pass again just outside the line ({near?.x},{near?.z}, {near?.width} wide)");
            if (near != null)
            {
                int by = DefenseLineMath.CoveredBy(m, near, lines, out var how);
                Check(by == 0 && how != DefenseLineMath.Covered.No, $"covered: the pass measured outside the line is the line's ({how})");
            }
            Check(far != null && DefenseLineMath.CoveredBy(m, far, lines, out _) < 0, "covered: the other pass is a real new gap");
            // The gap measured on the line's own row overlaps it.
            var onRow = new DefenseMath.Choke { x = 20, z = 32, facing = 0, width = 1, doors = 1, span = new List<int> { m.Index(20, 32) } };
            Check(DefenseLineMath.CoveredBy(m, onRow, lines, out var how2) == 0 && how2 == DefenseLineMath.Covered.Overlap, "covered: a gap on the line's gate is the line's");
            // The same pass facing the other way, or one far off, isn't.
            var back = new DefenseMath.Choke { x = 20, z = 36, facing = 2, width = 5, span = Enumerable.Range(18, 5).Select(x => m.Index(x, 36)).ToList() };
            Check(DefenseLineMath.CoveredBy(m, back, lines, out _) < 0, "covered: a gap facing the other way isn't the line's");
            var distant = new DefenseMath.Choke { x = 20, z = 50, facing = 0, width = 5, span = Enumerable.Range(18, 5).Select(x => m.Index(x, 50)).ToList() };
            Check(DefenseLineMath.CoveredBy(m, distant, lines, out _) < 0, "covered: the same valley eighteen cells out is a gap of its own");
            // A gate of the colony's a few cells behind the line is a second line, not this one.
            var inner = new DefenseMath.Choke { x = 20, z = 26, facing = 0, width = 1, doors = 1, span = new List<int> { m.Index(20, 26) } };
            Check(DefenseLineMath.CoveredBy(m, inner, lines, out _) < 0, "covered: a gate of its own behind the line is another line");
            // On the same walks a few cells off, measured across the other way (a bend): the line's.
            var bend = new DefenseMath.Choke { x = 20, z = 38, facing = 1, width = 5, span = new List<int> { m.Index(20, 38) }, walks = shape.walks.Take(5).ToList() };
            Check(DefenseLineMath.CoveredBy(m, bend, lines, out var how3) == 0 && how3 == DefenseLineMath.Covered.SameRoute, $"covered: on the same ways in, a few cells off ({how3})");
            var other = new DefenseMath.Choke { x = 20, z = 38, facing = 1, width = 5, span = new List<int> { m.Index(20, 38) }, walks = new List<int> { 99999 } };
            Check(DefenseLineMath.CoveredBy(m, other, lines, out _) < 0, "covered: a few cells off on other ways in isn't");
        }

        // ---------- The way up to a gate ----------
        var corridor = Corridor(root);
        Check(corridor.W == 4 && corridor.H == 9, $"the trap corridor is 4 by 9 ({corridor.W}x{corridor.H})");
        {
            // A valley 5 wide (x 17..21) from the north edge to a meadow; the line across it at z = 30, gate at x = 20.
            DefenseMath.MapGrid Valley(int from, int to, Func<int, int, char> extra = null) => Map(50, 60, (x, z) =>
            {
                var e = extra?.Invoke(x, z) ?? '\0';
                if (e != '\0') return e;
                if (z == 30 && x >= from && x <= to) return x == 20 ? 'D' : 'W';
                if (x >= from && x <= to && z >= 25) return '.';
                if (x >= 5 && x <= 44 && z >= 5 && z <= 24) return x >= 18 && x <= 22 && z >= 10 && z <= 12 ? 'c' : x >= 14 && x <= 26 && z >= 8 && z <= 14 ? 'h' : '.';
                return '#';
            });
            var m = Valley(17, 21);
            DefenseLineMath.MarkLineDoors(m, 20, 30, 0);
            var r = DefenseMath.Analyse(m);
            var path = DefenseApproach.PathToGate(m, 20, 30, 0, r.flow);
            Check(path.Count >= 12 && path[0] == m.Index(20, 30), $"approach: the way up to the gate, gate first ({path.Count} cells)");
            Check(path.Skip(1).All(c => DefenseLineMath.Ahead(c % 50 - 20, c / 50 - 30, 0) >= 1), "approach: all of it on the raiders' side");
            Check(path.Count(c => r.flow[c] > 0) * 10 >= path.Count * 7, $"approach: along the walks the survey found ({path.Count(c => r.flow[c] > 0)} of {path.Count})");
            var noFlow = DefenseApproach.PathToGate(m, 20, 30, 0, null);
            Check(noFlow.Count >= 12 && noFlow[0] == m.Index(20, 30), $"approach: with no walks known, straight out from the gate ({noFlow.Count})");
            // Rock right in front of the gate: no way up to it.
            var shut = Valley(17, 21, (x, z) => z >= 31 && z <= 33 && x >= 17 && x <= 21 ? '#' : '\0');
            Check(DefenseApproach.PathToGate(shut, 20, 30, 0, null).Count == 0, "approach: nothing walks up to a gate with rock in front of it");

            // ---------- Where the trap corridor goes ----------
            var lineCells = DefenseLineMath.ShapeCells(m, 20, 30, 0, Enumerable.Range(17, 5).Select(x => m.Index(x, 30)));
            var spots = DefenseApproach.Spots(m, corridor, 20, 30, 0, path, lineCells);
            var best = spots.FirstOrDefault();
            Check(best != null && best.Fits, "corridor: a spot fits in the valley");
            if (best != null)
            {
                var cells = Cells(corridor, best).ToList();
                Check(cells.All(c => DefenseLineMath.Ahead(c.x - 20, c.z - 30, 0) >= 1), "corridor: all of it in front of the line, none across its wall or gate");
                Check(best.facing == 0 && best.at >= 1 && best.at <= 4, $"corridor: facing the raiders, its end near the gate ({best.at} out)");
                Check(best.onLane >= 6 && best.onWalls == 0, $"corridor: the way in runs down its lane, not into its walls ({best.onLane} on the lane, {best.onWalls} on walls)");
                Check(DefenseApproach.SidesStanding(best) >= 1 && best.rockSides >= 9, $"corridor: leans on the valley's rock ({best.rockSides} cells of rock)");
            }
            Check(spots.Where(s => s.Fits).All(s => Cells(corridor, s).All(c => DefenseLineMath.Ahead(c.x - 20, c.z - 30, 0) >= 1 && !lineCells.Contains(c.z * 50 + c.x))),
                "corridor: no spot that fits touches the line");

            // A valley two wide in front of the gate: the rock is both its sides, nothing to build but traps.
            var narrow = Map(50, 60, (x, z) =>
            {
                if (z == 30 && x >= 19 && x <= 20) return x == 20 ? 'D' : 'W';
                if (x >= 19 && x <= 20 && z >= 25) return '.';
                if (x >= 5 && x <= 44 && z >= 5 && z <= 24) return x >= 18 && x <= 22 && z >= 10 && z <= 12 ? 'c' : '.';
                return '#';
            });
            DefenseLineMath.MarkLineDoors(narrow, 20, 30, 0);
            var np = DefenseApproach.PathToGate(narrow, 20, 30, 0, DefenseMath.Analyse(narrow).flow);
            var nb = DefenseApproach.Spots(narrow, corridor, 20, 30, 0, np, DefenseLineMath.ShapeCells(narrow, 20, 30, 0, null)).FirstOrDefault();
            Check(nb != null && nb.Fits && nb.build == 0 && nb.mine == 0 && DefenseApproach.SidesStanding(nb) == 2,
                $"corridor: in a two-wide pass, the rock on both sides is its walls (builds {nb?.build}, digs {nb?.mine}, sides {(nb == null ? -1 : DefenseApproach.SidesStanding(nb))})");

            // Open country: walls of its own.
            var open = Map(60, 60, (x, z) => z == 30 && x >= 27 && x <= 33 ? (x == 30 ? 'D' : 'W') : x >= 27 && x <= 33 && z >= 15 && z <= 21 ? (x == 30 && z == 18 ? 'c' : 'h') : '.');
            DefenseLineMath.MarkLineDoors(open, 30, 30, 0);
            var op = DefenseApproach.PathToGate(open, 30, 30, 0, DefenseMath.Analyse(open).flow);
            var ob = DefenseApproach.Spots(open, corridor, 30, 30, 0, op, DefenseLineMath.ShapeCells(open, 30, 30, 0, null)).FirstOrDefault();
            Check(ob != null && ob.Fits && ob.rockSides == 0 && ob.build >= 18, $"corridor: on open ground it builds its own walls ({ob?.build})");

            // Unexplored ground ahead counts against a spot, rock to dig more than open ground.
            var a = new DefenseApproach.Spot { build = 4 };
            var b = new DefenseApproach.Spot { build = 2, mine = 2 };
            var c2 = new DefenseApproach.Spot { build = 2, fog = 2 };
            Check(DefenseApproach.Cost(a, 2, false) < DefenseApproach.Cost(b, 2, false) && DefenseApproach.Cost(b, 2, false) < DefenseApproach.Cost(c2, 2, false), "corridor: known ground before rock to dig, rock before unexplored ground");
            var lane = new DefenseApproach.Spot { build = 4, onLane = 6 };
            var walls = new DefenseApproach.Spot { build = 4, onWalls = 2 };
            Check(DefenseApproach.Cost(lane, 2, false) < DefenseApproach.Cost(a, 2, false) && DefenseApproach.Cost(walls, 2, false) > DefenseApproach.Cost(a, 2, false), "corridor: its lane on the way in counts for it, its walls on the way in against it");
            Check(DefenseApproach.Cost(a, 2, true) > DefenseApproach.Cost(a, 2, false) && DefenseApproach.Cost(new DefenseApproach.Spot { build = 4, at = 7 }, 2, false) > DefenseApproach.Cost(new DefenseApproach.Spot { build = 4, at = 2 }, 2, false),
                "corridor: turned away from the line, or further out than it needs, costs a little");

            // Unexplored valley: the spots in the dark don't fit, and that's why.
            var fog = Valley(17, 21, (x, z) => x >= 17 && x <= 21 && z >= 32 ? '?' : '\0');
            DefenseLineMath.MarkLineDoors(fog, 20, 30, 0);
            var fp = DefenseApproach.PathToGate(fog, 20, 30, 0, null);
            var fs = DefenseApproach.Spots(fog, corridor, 20, 30, 0, fp, DefenseLineMath.ShapeCells(fog, 20, 30, 0, null));
            Check(fs.All(s => !s.Fits), "corridor: nothing fits where the way in is unexplored");
            var why = DefenseApproach.WhyNone(fp, fs, corridor.H, DefenseApproach.DarkAhead(fog, fp, 20, 30, 0));
            Check(why == DefenseApproach.Misfit.Fog, $"corridor: and says it's the unexplored ground ({why})");
            // Rock two cells out: too little ground before it.
            var shortGround = Valley(17, 21, (x, z) => z >= 33 && x >= 17 && x <= 21 ? '#' : '\0');
            var sp = DefenseApproach.PathToGate(shortGround, 20, 30, 0, null);
            var ss = DefenseApproach.Spots(shortGround, corridor, 20, 30, 0, sp, DefenseLineMath.ShapeCells(shortGround, 20, 30, 0, null), mayDig: false);
            Check(ss.All(s => !s.Fits) && DefenseApproach.WhyNone(sp, ss, corridor.H, DefenseApproach.DarkAhead(shortGround, sp, 20, 30, 0)) == DefenseApproach.Misfit.Short,
                $"corridor: no room before the rock, and it says so ({DefenseApproach.WhyNone(sp, ss, corridor.H)})");
            Check(DefenseApproach.WhyNone(new List<int>(), ss, corridor.H) == DefenseApproach.Misfit.NoPath, "corridor: no way up to the gate says so");

            // A pass that runs north from the gate for a few cells, then turns east: the corridor follows the way round the
            // bend, its lane on it, rather than standing in the dead end straight ahead.
            var bend = Map(60, 60, (x, z) =>
            {
                if (z == 30 && x >= 18 && x <= 21) return x == 20 ? 'D' : 'W';
                if (x >= 18 && x <= 21 && z >= 25 && z <= 40) return '.';
                if (z >= 37 && z <= 40 && x >= 18) return '.';
                if (x >= 5 && x <= 44 && z >= 5 && z <= 24) return x >= 18 && x <= 22 && z >= 10 && z <= 12 ? 'c' : '.';
                return '#';
            });
            DefenseLineMath.MarkLineDoors(bend, 20, 30, 0);
            var bp = DefenseApproach.PathToGate(bend, 20, 30, 0, DefenseMath.Analyse(bend).flow);
            Check(bp.Count > 12 && bp.Skip(10).Any(c => c % 60 > 24), $"approach: round a bend, the way in follows the walks out along it ({bp.Count} cells)");
            var bb = DefenseApproach.Spots(bend, corridor, 20, 30, 0, bp, DefenseLineMath.ShapeCells(bend, 20, 30, 0, null)).FirstOrDefault();
            Check(bb != null && bb.Fits && bb.onLane >= DefenseApproach.Need(corridor) && bb.walked >= DefenseApproach.Need(corridor),
                $"corridor: round a bend it lies on the way raiders walk (lane {bb?.onLane}, walked {bb?.walked}, facing {bb?.facing})");
            // A pass that runs off diagonally: no straight lane lies along it, and it says so rather than standing beside it.
            var diag = Map(60, 60, (x, z) =>
            {
                if (z == 30 && x >= 18 && x <= 22) return x == 20 ? 'D' : 'W';
                if (x >= 18 && x <= 22 && z > 30 && z <= 34) return '.';
                if (z > 34 && Math.Abs((x - 20) - (z - 34)) <= 3) return '.';
                if (x >= 5 && x <= 44 && z >= 5 && z <= 29 && !(z >= 26 && (x < 18 || x > 22))) return x >= 18 && x <= 22 && z >= 10 && z <= 12 ? 'c' : '.';
                return '#';
            });
            DefenseLineMath.MarkLineDoors(diag, 20, 30, 0);
            var dp = DefenseApproach.PathToGate(diag, 20, 30, 0, DefenseMath.Analyse(diag).flow);
            var ds = DefenseApproach.Spots(diag, corridor, 20, 30, 0, dp, DefenseLineMath.ShapeCells(diag, 20, 30, 0, null));
            var dwhy = DefenseApproach.WhyNone(dp, ds, corridor.H);
            Check(ds.All(s => !s.Fits || s.walked < 0 || s.walked >= DefenseApproach.Need(corridor)) && (ds.Count == 0 || !ds[0].Fits || ds[0].onLane >= DefenseApproach.Need(corridor)),
                "corridor: a spot it offers always has the way in down its lane");
            Check(ds.Count > 0 && !ds[0].Fits && dwhy == DefenseApproach.Misfit.OffWay, $"corridor: a diagonal pass, and it says the way bends past it ({dwhy})");

            // Its lane is what raiders walk once its walls stand.
            if (best != null)
            {
                int walked = DefenseApproach.LaneWalked(m, corridor, best, 20, 30, 0, path);
                Check(walked >= 5, $"corridor: raiders walk its lane once it stands ({walked} cells)");
                Check(m.kind[best.anchorZ * 50 + best.anchorX] == Valley(17, 21).kind[best.anchorZ * 50 + best.anchorX], "corridor: the grid is put back after trying it");
            }
        }

        // ---------- Walls that send raiders another way ----------
        {
            var m = TwoPasses();
            DefenseLineMath.MarkLineDoors(m, 18, 36, 0);
            var r = DefenseMath.Analyse(m);
            var lineCells = DefenseLineMath.ShapeCells(m, 18, 36, 0, Enumerable.Range(16, 5).Select(x => m.Index(x, 36)));
            var through = DefenseLineMath.WalksThrough(r.walked, lineCells, m);
            var starts = through.Select(w => r.walked[w][0]).ToList();
            var targets = DefenseLineMath.Targets(m);
            // Walls across the whole pass in front of the gate (a corridor wider than the pass, its walls closing it).
            var block = Enumerable.Range(16, 5).Select(x => new DefenseLineMath.Change { cell = m.Index(x, 40), kind = DefenseMath.Wall }).ToList();
            var away = DefenseLineMath.CheckWay(m, targets, starts, lineCells, block);
            Check(away.before > 0 && away.turnedAway && away.after * 2 < away.before, $"walls: closing the way to the gate sends them by the other pass ({away.before} before, {away.after} after)");
            Check(away.elsewhere.Count > 0 && away.elsewhere.Any(c => c % 60 >= 30 && c % 60 <= 32 && c / 60 == 36), "walls: and the way they'd take is known (by B)");
            Check(m.kind[m.Index(18, 40)] == DefenseMath.Open, "walls: the grid is put back");
            // Walls along the sides of the way, leaving it open: they keep coming.
            var sides = new List<DefenseLineMath.Change>();
            for (int z = 39; z <= 46; z++)
            {
                sides.Add(new DefenseLineMath.Change { cell = m.Index(16, z), kind = DefenseMath.Wall });
                sides.Add(new DefenseLineMath.Change { cell = m.Index(20, z), kind = DefenseMath.Wall });
            }
            var keep = DefenseLineMath.CheckWay(m, targets, starts, lineCells, sides);
            Check(keep.before > 0 && !keep.turnedAway && keep.elsewhere.Count == 0, $"walls: walls beside the way leave it the way in ({keep.before} before, {keep.after} after)");
            // A gate for the line counts as its way through.
            var gate = Enumerable.Range(16, 5).Select(x => new DefenseLineMath.Change { cell = m.Index(x, 40), kind = x == 18 ? DefenseMath.Door : DefenseMath.Wall, free = x == 18 }).ToList();
            Check(!DefenseLineMath.CheckWay(m, targets, starts, lineCells, gate).turnedAway, "walls: a second gate of the line's own keeps them coming");
        }
        return checks;
    }
}
