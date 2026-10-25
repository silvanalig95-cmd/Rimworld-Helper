using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Places a plan that raiders walk through in front of a defense line (the trap corridor, anything placed "in front"):
    /// on the way the survey says raiders walk up to the gate, its lane on that way and leading straight to the gate,
    /// inside the passage with the rock or the colony's walls as its sides where they fit, never on or behind the line's
    /// own wall or gate (<see cref="DefenseApproach"/>). The cheapest spots are tried on the map in turn (known ground
    /// before unexplored, as little digging and building as can be); the first the game would take is the one. It says
    /// why it chose it ("On the way to the gate, 6 cells out, using the rock on both sides."), or why nothing fits and
    /// what would make room. Worked out when asked, never on a tick.
    /// </summary>
    public static class DefenseApproachPlanner
    {
        public class Choice
        {
            public bool fits;
            public IntVec3 anchor = IntVec3.Invalid;
            public int rot;
            public bool mirror;
            public DefenseApproach.Spot spot;
            /// <summary>Why this spot, or why none.</summary>
            public string why;
            public DefenseApproach.Misfit misfit;
            /// <summary>Rock the chosen plan digs out, and cells of it in unexplored ground.</summary>
            public int mine, fog;
        }

        /// <summary>Plans placed in front of a line follow the way in.</summary>
        public static bool Applies(DefenseTemplateDef t, string placeAt) => t != null && (placeAt == "front" || placeAt.NullOrEmpty() && t.placeAt == "front");

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>A layout as it will be built: roles left out are ground it leaves alone.</summary>
        public static DefenseLayout.Grid LayoutFor(DefenseLayout.Grid g, ICollection<string> leaveOut)
        {
            if (leaveOut == null || leaveOut.Count == 0) return g;
            var rows = new List<string>();
            for (int r = 0; r < g.H; r++)
            {
                int z = g.H - 1 - r;
                var chars = new char[g.W];
                for (int x = 0; x < g.W; x++)
                {
                    char c = g.cells[x, z];
                    string role = DefenseLayout.RoleOf(c);
                    chars[x] = role != null && leaveOut.Contains(role) ? DefenseLayout.Untouched : c;
                }
                rows.Add(new string(chars));
            }
            return DefenseLayout.Parse(rows, -1);
        }

        /// <summary>
        /// Where the plan goes in front of a gate facing <paramref name="facing"/>: the spots along the way in, cheapest
        /// first, each tried on the map until one can go down with nothing blocking it. <paramref name="span"/> is the
        /// line's own gap, which nothing of it may stand on; <paramref name="want"/> how many cells out its end should be.
        /// </summary>
        public static Choice Find(Map map, DefenseTemplateDef t, DefensePlanConfig cfg, List<DefensePiece> pieces, IntVec3 gate, int facing, int want, IEnumerable<IntVec3> span)
        {
            var choice = new Choice();
            facing = DefenseLineMath.Norm(facing);
            var s = DefenseSurvey.For(map);
            if (s?.grid == null || t == null || pieces == null || pieces.Count == 0 || !gate.InBounds(map))
            {
                choice.misfit = DefenseApproach.Misfit.NoPath;
                choice.why = "CC_Def_NoSurvey".Translate();
                return choice;
            }
            var g = s.grid;
            var layout = LayoutFor(t.GridFor(map, cfg), cfg?.leaveOut);
            bool dig = S?.planMineRock ?? true;
            List<int> path;
            List<DefenseApproach.Spot> spots;
            bool dark;
            using (s.PatchAround(gate, DefenseApproach.Reach + 6))
            {
                var spanCells = span?.Where(c => c.InBounds(map)).Select(c => g.Index(c.x, c.z));
                var lineCells = DefenseLineMath.ShapeCells(g, gate.x, gate.z, facing, spanCells);
                path = DefenseApproach.PathToGate(g, gate.x, gate.z, facing, s.result?.flow);
                spots = DefenseApproach.Spots(g, layout, gate.x, gate.z, facing, path, lineCells, Mathf.Max(1, want), 10, dig, SlowCost(pieces));
                dark = DefenseApproach.DarkAhead(g, path, gate.x, gate.z, facing);
            }
            choice.misfit = DefenseApproach.WhyNone(path, spots, layout.H, dark);
            int blockedOnMap = 0, tried = 0;
            string blocker = null;
            foreach (var spot in spots)
            {
                if (!spot.Fits || tried >= 14) break;
                tried++;
                var trial = (cfg ?? new DefensePlanConfig()).Copy();
                trial.rotation = spot.turn;
                trial.mirror = spot.mirror;
                var a = new IntVec3(spot.anchorX, 0, spot.anchorZ);
                var plan = DefensePlanner.Plan(t, map, a, trial, pieces, true);
                var stuck = plan.pieces.FirstOrDefault(p => p.state == PieceState.Blocked && !t.Optional(p.group));
                if (!plan.CanPlace || stuck != null || plan.clearBlocked.Count > 0)
                {
                    blockedOnMap++;
                    if (blocker == null)
                    {
                        if (stuck != null) blocker = stuck.reason;
                        else if (plan.clearBlocked.Count > 0) blocker = plan.clearBlocked[0].GetEdifice(map)?.LabelShort;
                        else blocker = plan.FailReason;
                    }
                    continue;
                }
                choice.fits = true;
                choice.spot = spot;
                choice.anchor = a;
                choice.rot = spot.turn;
                choice.mirror = spot.mirror;
                choice.mine = plan.mine.Count;
                choice.fog = plan.fog.Count;
                choice.misfit = DefenseApproach.Misfit.None;
                choice.why = Explain(choice, t, facing, cfg?.leaveOut);
                return choice;
            }
            if (choice.misfit == DefenseApproach.Misfit.None) choice.misfit = DefenseApproach.Misfit.Blocked;
            choice.why = FailText(choice.misfit, t, layout, path, blockedOnMap > 0 ? blocker : null, dig);
            return choice;
        }

        /// <summary>What a raider pays to cross the plan's slowing pieces, as the game's path grid has it.</summary>
        private static int SlowCost(List<DefensePiece> pieces)
        {
            int cost = 0;
            foreach (var p in pieces)
                if ((p.role == "slow" || p.role == "cover") && p.def != null) cost = Mathf.Max(cost, p.def.pathCost);
            return cost > 0 ? Mathf.Clamp(cost, 10, 600) : 40;
        }

        /// <summary>"On the way to the gate, 6 cells out, using the rock on both sides." and what else it takes.</summary>
        public static string Explain(Choice c, DefenseTemplateDef t, int facing, ICollection<string> leaveOut)
        {
            var s = c.spot;
            if (s == null) return null;
            string sides;
            bool open = leaveOut != null && leaveOut.Contains("wall");
            int standing = DefenseApproach.SidesStanding(s);
            if (open) sides = "CC_DefLine_Approach_Open".Translate();
            else if (s.leftAll + s.rightAll == 0) sides = "CC_DefLine_Approach_NoWalls".Translate();
            else if (standing == 2) sides = (s.wallSides == 0 ? "CC_DefLine_Approach_RockBoth" : s.rockSides == 0 ? "CC_DefLine_Approach_WallsBoth" : "CC_DefLine_Approach_RockAndWall").Translate();
            else if (standing == 1) sides = (s.rockSides >= s.wallSides ? "CC_DefLine_Approach_RockOne" : "CC_DefLine_Approach_WallOne").Translate();
            else sides = "CC_DefLine_Approach_OwnWalls".Translate();
            string text = "CC_DefLine_Approach_Why".Translate(DefenseSurvey.Cells(Mathf.Max(1, s.at)), sides).Resolve();
            if (s.facing != DefenseLineMath.Norm(facing)) text += " " + "CC_DefLine_Approach_Turned".Translate().Resolve();
            if (c.mine > 0) text += " " + "CC_DefLine_Approach_Dig".Translate(c.mine).Resolve();
            if (c.fog > 0) text += " " + "CC_DefLine_Approach_Dark".Translate(c.fog).Resolve();
            return text;
        }

        /// <summary>Why nothing fits, and what would make room.</summary>
        public static string FailText(DefenseApproach.Misfit why, DefenseTemplateDef t, DefenseLayout.Grid g, List<int> path, string blocker, bool dig)
        {
            string label = t.LabelNow;
            switch (why)
            {
                case DefenseApproach.Misfit.NoPath: return "CC_DefLine_Approach_NoPath".Translate(label).Resolve();
                case DefenseApproach.Misfit.Short:
                    return "CC_DefLine_Approach_Short".Translate(label, DefenseSurvey.Cells(Mathf.Max(1, (path?.Count ?? 1) - 1)), g.W, g.H).Resolve();
                case DefenseApproach.Misfit.Fog: return "CC_DefLine_Approach_Fog".Translate(label).Resolve();
                case DefenseApproach.Misfit.Rock:
                    return (dig ? "CC_DefLine_Approach_RockNarrow" : "CC_DefLine_Approach_RockNoDig").Translate(label).Resolve();
                case DefenseApproach.Misfit.Water: return "CC_DefLine_Approach_Water".Translate(label).Resolve();
                case DefenseApproach.Misfit.Edge: return "CC_DefLine_Approach_Edge".Translate(label).Resolve();
                case DefenseApproach.Misfit.Crosses: return "CC_DefLine_Approach_Crosses".Translate(label).Resolve();
                case DefenseApproach.Misfit.OffWay: return "CC_DefLine_Approach_OffWay".Translate(label).Resolve();
                default:
                    return blocker.NullOrEmpty()
                        ? "CC_DefLine_Approach_Blocked".Translate(label).Resolve()
                        : "CC_DefLine_Approach_BlockedBy".Translate(label, blocker).Resolve();
            }
        }

        // ---------------- For a line's step ----------------

        private static readonly Dictionary<int, (DefenseSurvey s, int version, DefenseTemplateDef t, int stamp, Choice choice)> trials =
            new Dictionary<int, (DefenseSurvey, int, DefenseTemplateDef, int, Choice)>();

        /// <summary>Where the line's step would go, kept until the survey, the line, the plan or the planner's choices change.</summary>
        public static Choice TrialFor(DefenseLineFacts f, DefenseTemplateDef t, int want)
        {
            if (f?.line == null || t == null) return null;
            int stamp = (S != null && S.planMineRock ? 1 : 0) + want * 31;
            if (trials.TryGetValue(f.line.id, out var kept) && kept.s == f.s && kept.version == f.line.version && kept.t == t && kept.stamp == stamp) return kept.choice;
            Choice choice = null;
            try
            {
                // As "Plan this step" first proposes it: the whole plan, walls and all.
                var trial = DefensePlanConfig.For(t).Copy();
                trial.leaveOut.Clear();
                choice = Find(f.map, t, trial, t.ResolveCached(f.map, out _, trial), f.gate, f.line.facing, want, f.line.span);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense line: placing a plan on the way in", e);
            }
            if (trials.Count > 20) trials.Clear();
            trials[f.line.id] = (f.s, f.line.version, t, stamp, choice);
            return choice;
        }

        /// <summary>
        /// For the survey's own "find the spot" on a gap: in front of the line that holds it (or the gap itself when no
        /// line does), on the way in. False when nothing fits; <paramref name="why"/> says why either way.
        /// </summary>
        public static bool ForGap(Map map, DefenseTemplateDef t, DefensePlanConfig cfg, List<DefensePiece> pieces, int chokeIndex,
            out IntVec3 anchor, out int rot, out bool mirror, out string why)
        {
            anchor = IntVec3.Invalid;
            rot = cfg?.rotation ?? 0;
            mirror = cfg?.mirror ?? false;
            why = null;
            var s = DefenseSurvey.For(map);
            if (s == null) return false;
            var choke = chokeIndex >= 0 && chokeIndex < s.chokes.Count ? s.chokes[chokeIndex] : s.Main;
            IntVec3 gate;
            int facing;
            IEnumerable<IntVec3> span;
            var line = choke != null ? s.CoveringLine(choke) : DefenseLines.Of(map)?.lines.FirstOrDefault();
            if (line != null)
            {
                gate = line.gate;
                facing = line.facing;
                span = line.span;
            }
            else if (choke != null)
            {
                gate = choke.cell;
                facing = choke.c.facing;
                span = choke.c.span.Select(i => new IntVec3(i % s.grid.w, 0, i / s.grid.w));
            }
            else if (s.approach.IsValid)
            {
                gate = s.approach;
                facing = s.approachFacing;
                span = null;
            }
            else return false;
            var c = Find(map, t, cfg, pieces, gate, facing, 2, span);
            why = c.why;
            if (!c.fits) return false;
            anchor = c.anchor;
            rot = c.rot;
            mirror = c.mirror;
            return true;
        }
    }

    /// <summary>
    /// Whether raiders would still come through a line once a plan's walls stood, by the survey's own reckoning of the
    /// ways in (<see cref="DefenseLineMath.CheckWay"/>): the walks that cross the line now, walked again with the plan
    /// built (its walls as walls, its gate on the line as the line's way through, the rock it digs out as open ground).
    /// When most would go another way, it names the way ("These walls would make the north gap the easier way in") and
    /// says what keeps them coming: the walls left out where an open side is enough, or the other gap closed first.
    /// </summary>
    public static class DefenseWayCheck
    {
        public class Result
        {
            public DefenseLineMath.WayCheck check;
            /// <summary>The survey's gap the other way passes (its index), or -1; the line that holds it, if one does.</summary>
            public int otherGap = -1;
            public DefenseLine otherLine;
            public IntVec3 otherCell = IntVec3.Invalid;
            public string otherWords;
            public bool TurnedAway => check != null && check.turnedAway;
        }

        /// <summary>Plans whose walls are worth the check: those on or in front of a line and behind its gate (not a fallback, a keep or a mortar pit deep inside).</summary>
        public static bool Worth(DefenseTemplateDef t) =>
            t != null && t.kind != "fallback" && t.kind != "keep" && t.kind != "mortar"
            && t.SampleGrid.pieces.Any(p => p.role == "wall" || p.role == "embrasure" || p.role == "turret");

        /// <summary>Templates whose walls are only the sides of a lane on the way in (the trap corridor): it can do without them.</summary>
        public static bool WallsAreSides(DefenseTemplateDef t) => t != null && t.kind == "traps";

        public const int MaxWalks = 30;

        public static Result For(Map map, DefenseLine line, DefensePlan plan)
        {
            var s = DefenseSurvey.For(map);
            if (s?.grid == null || s.result == null || line == null || plan == null || !line.gate.InBounds(map)) return null;
            var g = s.grid;
            var res = new Result();
            using (s.PatchAround(line.gate, 30))
            {
                var lineCells = DefenseLineMath.ShapeCells(g, line.gate.x, line.gate.z, line.facing, line.span.Where(c => c.InBounds(map)).Select(c => g.Index(c.x, c.z)));
                foreach (var p in plan.pieces)
                    if (p.group == "gate" && OnLine(line, p.cell) && p.cell.InBounds(map)) lineCells.Add(g.Index(p.cell.x, p.cell.z));
                var walks = DefenseLineMath.WalksThrough(s.result.walked, lineCells, g).OrderBy(w => w).ToList();
                if (walks.Count == 0) return null;
                var starts = new List<int>();
                int step = Mathf.Max(1, walks.Count / MaxWalks);
                for (int k = 0; k < walks.Count && starts.Count < MaxWalks; k += step)
                {
                    var cells = s.result.walked[walks[k]];
                    if (cells.Length > 0) starts.Add(cells[0]);
                }
                res.check = DefenseLineMath.CheckWay(g, DefenseLineMath.Targets(g), starts, lineCells, Changes(map, g, line, plan));
            }
            if (!res.TurnedAway) return res;
            Name(s, line, res);
            return res;
        }

        /// <summary>A gate piece on the line's row, a cell off it, or right behind its gate (a double gate): the line's own way through.</summary>
        private static bool OnLine(DefenseLine line, IntVec3 c)
        {
            int ahead = line.Ahead(c);
            return ahead <= 1 && ahead >= -2 && Mathf.Abs(line.Lateral(c)) <= Mathf.Max(2, line.width / 2 + 2);
        }

        /// <summary>What the plan changes on the grid once built: dug-out rock opens, walls close, its gate on the line stays the way through, cover costs a little to climb.</summary>
        public static List<DefenseLineMath.Change> Changes(Map map, DefenseMath.MapGrid g, DefenseLine line, DefensePlan plan)
        {
            var list = new List<DefenseLineMath.Change>();
            foreach (var c in plan.mine.Concat(plan.deconstructCells))
                if (c.InBounds(map)) list.Add(new DefenseLineMath.Change { cell = g.Index(c.x, c.z), kind = DefenseMath.Open, cost = 0 });
            foreach (var p in plan.pieces)
            {
                if (p.state == PieceState.Present || p.state == PieceState.Blocked || p.def == null) continue;
                foreach (var c in p.rect)
                {
                    if (!c.InBounds(map)) continue;
                    int i = g.Index(c.x, c.z);
                    switch (p.group)
                    {
                        case "gate":
                            list.Add(new DefenseLineMath.Change { cell = i, kind = DefenseMath.Door, cost = 0, free = line != null && OnLine(line, c) });
                            break;
                        case "cover":
                        case "slow":
                            list.Add(new DefenseLineMath.Change { cell = i, kind = DefenseMath.Open, cost = Mathf.Clamp(Mathf.Max(g.cost[i], p.def.pathCost), 0, 600) });
                            break;
                        case "trap":
                            // Raiders don't see traps: they cost them nothing to walk on.
                            list.Add(new DefenseLineMath.Change { cell = i, kind = DefenseMath.Open, cost = g.cost[i] });
                            break;
                        default:
                            if (p.def.passability == Traversability.Impassable) list.Add(new DefenseLineMath.Change { cell = i, kind = DefenseMath.Wall });
                            break;
                    }
                }
            }
            return list;
        }

        /// <summary>The way they'd take instead, in words: the gap it passes ("the north gap"), the line that holds that, or the side it comes in from.</summary>
        private static void Name(DefenseSurvey s, DefenseLine line, Result res)
        {
            var way = res.check.elsewhere;
            var comp = DefenseLines.Of(s.map);
            if (way != null && way.Count > 0)
            {
                var cells = new HashSet<int>(way);
                for (int k = 0; k < s.chokes.Count; k++)
                {
                    var c = s.chokes[k];
                    if (!c.c.span.Any(cells.Contains)) continue;
                    var holder = s.CoveringLine(c);
                    if (holder == line) continue;
                    res.otherGap = k;
                    res.otherLine = holder;
                    res.otherCell = c.cell;
                    res.otherWords = holder != null ? DefenseLineReview.NameOf(holder, s.map) : "CC_DefLine_Way_Gap".Translate(DefenseAdvice.Where(s, c.cell)).Resolve();
                    return;
                }
                // No gap the survey knows: the side the walk comes in from.
                int mid = way[Mathf.Min(way.Count - 1, way.Count / 3)];
                res.otherCell = new IntVec3(mid % s.grid.w, 0, mid / s.grid.w);
                res.otherWords = "CC_DefLine_Way_Side".Translate(DefenseAdvice.Where(s, res.otherCell)).Resolve();
                return;
            }
            res.otherWords = "CC_DefLine_Way_Walls".Translate();
        }

        /// <summary>"These walls would make the north gap the easier way in: of the raids that come through the south-west pass now, about 2 in 10 still would."</summary>
        public static string Words(Result r, DefenseLine line, Map map)
        {
            if (r?.check == null) return null;
            int share = r.check.before > 0 ? Mathf.RoundToInt(r.check.after * 10f / r.check.before) : 0;
            return "CC_DefLine_Away".Translate(r.otherWords ?? "CC_DefLine_Way_Somewhere".Translate().ToString(), DefenseLineReview.NameOf(line, map), share).Resolve();
        }
    }
}
