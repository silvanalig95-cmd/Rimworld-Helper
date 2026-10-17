using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where every piece of a defense template would go on the map, what's in the way, and what the wall line closes.</summary>
    public class DefensePlan
    {
        public DefenseTemplateDef template;
        public int rotation;
        public bool mirror;
        public IntVec3 anchor;
        public List<PlannedThing> pieces = new List<PlannedThing>();
        /// <summary>Open ground the plan keeps clear (the kill zone, the walkways) and the firing positions.</summary>
        public List<IntVec3> clear = new List<IntVec3>();
        public List<IntVec3> firing = new List<IntVec3>();
        /// <summary>Wall cells added to close the gap, and why the line stopped on each side.</summary>
        public List<IntVec3> extension = new List<IntVec3>();
        public int stopA = -1, stopB = -1;
        public List<IntVec3> mine = new List<IntVec3>();
        public HashSet<IntVec3> mineSet = new HashSet<IntVec3>();
        public List<Thing> deconstruct = new List<Thing>();
        public HashSet<IntVec3> deconstructCells = new HashSet<IntVec3>();
        public List<Plant> cut = new List<Plant>();
        /// <summary>Cells the plan keeps open that have something built on them (a kill zone with a shed in it).</summary>
        public List<IntVec3> clearBlocked = new List<IntVec3>();
        /// <summary>Pieces left out because they'd stand in rock the plan doesn't dig (where a gap ends against the hill).</summary>
        public HashSet<PlannedThing> inRock = new HashSet<PlannedThing>();
        public int chunks;
        public List<string> warnings = new List<string>();
        public bool edgeBlocked;

        public int Count(PieceState s) => pieces.Count(p => p.state == s);

        /// <summary>Something can go down, and no part of the plan is off the map or in unexplored ground.</summary>
        public bool CanPlace => !edgeBlocked && pieces.Any(p => p.state == PieceState.Ready || p.state == PieceState.Waiting);

        public string FailReason
        {
            get
            {
                if (edgeBlocked) return "CC_Plan_Block_Edge".Translate();
                if (!pieces.Any(p => p.state == PieceState.Ready || p.state == PieceState.Waiting))
                    return pieces.Count > 0 && pieces.All(p => p.state == PieceState.Present) ? "CC_Def_AllThere".Translate().ToString() : "CC_Plan_Blocked".Translate().ToString();
                return null;
            }
        }

        /// <summary>The way the plan faces once turned.</summary>
        public int Outward => DefenseLayout.Outward(mirror, rotation);

        private Dictionary<ThingDef, int> cost;

        public Dictionary<ThingDef, int> Cost()
        {
            if (cost != null) return cost;
            cost = new Dictionary<ThingDef, int>();
            foreach (var p in pieces)
            {
                if (p.state != PieceState.Ready && p.state != PieceState.Waiting) continue;
                if (p.def == null || (p.def.MadeFromStuff && p.stuff == null)) continue;
                try
                {
                    foreach (var c in p.def.CostListAdjusted(p.stuff, false))
                    {
                        if (c?.thingDef == null) continue;
                        cost.TryGetValue(c.thingDef, out int have);
                        cost[c.thingDef] = have + c.count;
                    }
                }
                catch
                {
                    // A modded cost list that can't be read: left out of the sum.
                }
            }
            return cost;
        }
    }

    /// <summary>
    /// Lays a defense template on the map, as the room planner does a room: every piece turned and mirrored with the
    /// template, rock mined and the colony's own buildings taken down where the player allows it, plants cut, and pieces
    /// that must wait queued until the way is clear. A wall line can carry on to the rock on either side ("close the
    /// gap"). Nothing is placed until the player clicks.
    /// </summary>
    public static class DefensePlanner
    {
        public const int MaxCarry = 12;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static IntVec3 ToWorld(DefenseTemplateDef t, IntVec3 anchor, int x, int z, bool mirror, int rot)
        {
            DefenseLayout.ToWorld(t.Grid, x, z, mirror, rot, out int wx, out int wz);
            return new IntVec3(anchor.x + wx, 0, anchor.z + wz);
        }

        /// <summary>Anchor that puts the middle of the template at <paramref name="center"/>.</summary>
        public static IntVec3 AnchorForCenter(DefenseTemplateDef t, IntVec3 center, int rot)
        {
            TemplateMath.RotatedSize(t.Width, t.Height, rot, out int rw, out int rh);
            return new IntVec3(center.x - rw / 2, 0, center.z - rh / 2);
        }

        private static bool IsOwn(Thing t) => t.Faction == Faction.OfPlayer;

        private static bool CanTakeDown(Thing t)
        {
            if (!IsOwn(t) || t.def.building == null || !t.def.building.IsDeconstructible) return false;
            return !(t is Building b) || b.DeconstructibleBy(Faction.OfPlayer).Accepted;
        }

        private static bool SameBuild(Thing t, ThingDef def) =>
            t != null && (t.def == def || ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild == def));

        /// <summary>Works out every cell of the plan. Nothing changes on the map.</summary>
        public static DefensePlan Plan(DefenseTemplateDef t, Map map, IntVec3 anchor, DefensePlanConfig cfg, List<DefensePiece> resolved, bool quick = false)
        {
            int rot = TemplateMath.Norm(cfg.rotation);
            bool mirror = cfg.mirror;
            var plan = new DefensePlan { template = t, rotation = rot, mirror = mirror, anchor = anchor };
            var g = t.Grid;
            bool cutPlants = S.planCutPlants, replace = S.planReplaceOwn;
            // A plan that closes a natural gap never digs: widening the gap would undo it. Rock where a piece would go is
            // simply where the gap ends. Other plans dig their positions out of the rock when the player allows it.
            bool closes = t.kind == "gate" || t.kind == "wall" || t.kind == "palisade";
            bool mineRock = S.planMineRock && !closes;
            var taken = new HashSet<IntVec3>();

            foreach (var piece in resolved)
            {
                var r = piece.rect;
                bool ok = TemplateLayout.ToWorld(g.W, g.H, mirror, rot, r.minX, r.minZ, r.Width, r.Height, piece.facing,
                    piece.def.rotatable, piece.def.IsDoor, piece.def.size.x, piece.def.size.z,
                    out int mx, out int mz, out int ww, out int wh, out int facing, out int rx, out int rz);
                var pt = new PlannedThing
                {
                    def = piece.def,
                    stuff = piece.stuff,
                    cell = new IntVec3(anchor.x + rx, 0, anchor.z + rz),
                    rot = new Rot4(facing),
                    rect = new CellRect(anchor.x + mx, anchor.z + mz, ww, wh),
                    group = piece.role
                };
                if (!ok)
                {
                    pt.state = PieceState.Blocked;
                    pt.reason = "CC_Plan_Block_CantTurn".Translate(piece.def.label);
                }
                else pt.state = PieceStateAt(plan, map, pt, piece.role, mineRock, replace, out pt.reason, out pt.ground);
                plan.pieces.Add(pt);
                foreach (var c in pt.rect) taken.Add(c);
            }

            // The open ground and firing positions: kept clear (trees and bushes give raiders cover).
            for (int x = 0; x < g.W; x++)
            {
                for (int z = 0; z < g.H; z++)
                {
                    char ch = g.cells[x, z];
                    if (ch != DefenseLayout.Open && ch != DefenseLayout.Firing) continue;
                    var c = ToWorld(t, anchor, x, z, mirror, rot);
                    if (!c.InBounds(map) || c.Fogged(map)) continue;
                    (ch == DefenseLayout.Firing ? plan.firing : plan.clear).Add(c);
                    // Open ground must be open: rock is dug out (raiders and defenders have to get through), anything
                    // else built there is the player's to move.
                    var ed = c.GetEdifice(map);
                    if (ed == null || ed.def.passability != Traversability.Impassable || ed.def.IsDoor) continue;
                    if (ed.def.mineable && mineRock)
                    {
                        if (plan.mineSet.Add(c)) plan.mine.Add(c);
                    }
                    // The rock beside a gap is the gap's own side, not something in the way.
                    else if (!(ed.def.mineable && closes)) plan.clearBlocked.Add(c);
                }
            }

            if (t.line >= 0 && cfg.closeGap) CloseGap(plan, map, t, anchor, mirror, rot, taken);

            if (!quick)
            {
                if (cutPlants)
                {
                    foreach (var p in plan.pieces)
                    {
                        if (p.state == PieceState.Blocked || p.state == PieceState.Present) continue;
                        foreach (var c in p.rect)
                        {
                            var plant = c.InBounds(map) ? c.GetPlant(map) : null;
                            if (plant != null && !plan.cut.Contains(plant)) plan.cut.Add(plant);
                        }
                    }
                    foreach (var c in plan.clear.Concat(plan.firing))
                    {
                        var plant = c.GetPlant(map);
                        if (plant != null && plant.def.fillPercent > 0.05f && !plan.cut.Contains(plant)) plan.cut.Add(plant);
                    }
                }
                foreach (var c in plan.clear)
                    foreach (var thing in c.GetThingList(map))
                        if (thing.def.thingCategories != null && thing.def.thingCategories.Contains(ThingCategoryDefOf.Chunks)) plan.chunks++;
                Warnings(plan, map);
            }
            return plan;
        }

        /// <summary>
        /// Whether a piece can go down: Present when it (or the same plan) is already there, or the rock already closes a
        /// wall cell; Waiting when rock has to be mined or one of the colony's buildings taken down; else Ready or Blocked
        /// with the reason (off the map or in unexplored ground blocks the whole plan).
        /// </summary>
        private static PieceState PieceStateAt(DefensePlan plan, Map map, PlannedThing pt, string role, bool mineRock, bool replace, out string reason, out bool ground)
        {
            reason = null;
            ground = false;
            bool waiting = false;
            foreach (var c in pt.rect)
            {
                if (!c.InBounds(map) || c.Fogged(map))
                {
                    reason = "CC_Plan_Block_Edge".Translate();
                    plan.edgeBlocked = true;
                    return PieceState.Blocked;
                }
            }
            // Already there: the same building or its blueprint, a door where a gate goes, rock or a wall where a wall goes.
            var root = pt.cell.GetEdifice(map);
            if (root != null && (root.def == pt.def || (role == "gate" && root.def.IsDoor) || (role == "wall" && TemplatePlanner.IsWallLike(root.def))))
                return PieceState.Present;
            foreach (var thing in pt.cell.GetThingList(map))
                if (SameBuild(thing, pt.def) || (role == "wall" && (thing is Blueprint_Build || thing is Frame) && thing.def.entityDefToBuild is ThingDef td && TemplatePlanner.IsWallLike(td)))
                    return PieceState.Present;
            foreach (var c in pt.rect)
            {
                var ed = c.GetEdifice(map);
                if (ed == null) continue;
                if (ed.def.mineable)
                {
                    if (!mineRock)
                    {
                        reason = "CC_Def_InRock".Translate();
                        plan.inRock.Add(pt);
                        return PieceState.Blocked;
                    }
                    if (plan.mineSet.Add(c)) plan.mine.Add(c);
                    waiting = true;
                }
                else if (IsOwn(ed) && replace && CanTakeDown(ed))
                {
                    if (!plan.deconstruct.Contains(ed)) plan.deconstruct.Add(ed);
                    plan.deconstructCells.Add(c);
                    waiting = true;
                }
                else
                {
                    reason = "CC_Plan_Block_Thing".Translate(ed.LabelShort);
                    return PieceState.Blocked;
                }
            }
            if (!TemplatePlanner.GroundHolds(pt.def, pt.cell, map, pt.rot, pt.stuff))
            {
                ground = true;
                reason = "CC_Plan_Skip_FurnitureGround".Translate(pt.def.label);
                return PieceState.Blocked;
            }
            if (waiting) return PieceState.Waiting;
            var report = GenConstruct.CanPlaceBlueprintAt(pt.def, pt.cell, pt.rot, map, false, null, null, pt.stuff);
            if (!report.Accepted)
            {
                reason = report.Reason.NullOrEmpty() ? "CC_Plan_Blocked".Translate().ToString() : report.Reason;
                return PieceState.Blocked;
            }
            return PieceState.Ready;
        }

        /// <summary>
        /// The wall line carried on beyond the template on both sides until rock, a wall, deep water or another building
        /// closes it (<see cref="DefenseLayout.CarryLine"/>), at most <see cref="MaxCarry"/> cells. Shallow water, marsh and
        /// mud take no wall, so the line stops there and the plan says it leaves a way round.
        /// </summary>
        private static void CloseGap(DefensePlan plan, Map map, DefenseTemplateDef t, IntVec3 anchor, bool mirror, int rot, HashSet<IntVec3> taken)
        {
            var g = t.Grid;
            var wall = plan.pieces.FirstOrDefault(p => p.group == "wall");
            if (wall == null) return;
            for (int side = 0; side < 2; side++)
            {
                int dir = side == 0 ? -1 : 1;
                int start = side == 0 ? -1 : g.W;
                var cells = new List<IntVec3>();
                int added = DefenseLayout.CarryLine(i =>
                {
                    var c = ToWorld(t, anchor, start + dir * i, g.line, mirror, rot);
                    cells.Add(c);
                    if (!c.InBounds(map) || c.Fogged(map) || c.InNoBuildEdgeArea(map)) return DefenseLayout.GapBlocked;
                    if (taken.Contains(c)) return DefenseLayout.GapClosed;
                    var ed = c.GetEdifice(map);
                    if (ed != null)
                        return TemplatePlanner.IsWallLike(ed.def) || ed.def.IsDoor || ed.def.passability == Traversability.Impassable ? DefenseLayout.GapClosed : DefenseLayout.GapBlocked;
                    foreach (var thing in c.GetThingList(map))
                        if ((thing is Blueprint_Build || thing is Frame) && thing.def.entityDefToBuild is ThingDef td && (TemplatePlanner.IsWallLike(td) || td.IsDoor))
                            return DefenseLayout.GapClosed;
                    var terrain = c.GetTerrain(map);
                    if (terrain == null || terrain.passability == Traversability.Impassable) return DefenseLayout.GapClosed;
                    if (!TemplatePlanner.GroundHolds(wall.def, c, map, Rot4.North, wall.stuff)) return DefenseLayout.GapGround;
                    if (!GenConstruct.CanPlaceBlueprintAt(wall.def, c, Rot4.North, map, false, null, null, wall.stuff).Accepted) return DefenseLayout.GapBlocked;
                    return DefenseLayout.GapOpen;
                }, MaxCarry, out int stop);
                if (side == 0) plan.stopA = stop;
                else plan.stopB = stop;
                for (int i = 0; i < added; i++)
                {
                    var c = cells[i];
                    plan.extension.Add(c);
                    plan.pieces.Add(new PlannedThing
                    {
                        def = wall.def,
                        stuff = wall.stuff,
                        cell = c,
                        rot = Rot4.North,
                        rect = CellRect.SingleCell(c),
                        group = "wall",
                        state = PieceState.Ready
                    });
                }
            }
        }

        private static void Warnings(DefensePlan plan, Map map)
        {
            var t = plan.template;
            if (t.line >= 0 && ColonyCouncilMod.Settings != null)
            {
                foreach (int stop in new[] { plan.stopA, plan.stopB })
                {
                    if (stop == DefenseLayout.GapGround) plan.warnings.Add("CC_Def_Warn_GapWater".Translate());
                    else if (stop == DefenseLayout.GapTooWide) plan.warnings.Add("CC_Def_Warn_GapWide".Translate(MaxCarry));
                    else if (stop == DefenseLayout.GapBlocked) plan.warnings.Add("CC_Def_Warn_GapBlocked".Translate());
                }
            }
            int ground = plan.pieces.Count(p => p.state == PieceState.Blocked && p.ground);
            if (ground > 0) plan.warnings.Add("CC_Def_Warn_Ground".Translate(ground));
            int blocked = plan.pieces.Count(p => p.state == PieceState.Blocked && !p.ground && !plan.inRock.Contains(p));
            if (blocked > 0) plan.warnings.Add("CC_Def_Warn_Blocked".Translate(blocked));
            if (plan.inRock.Count > 0) plan.warnings.Add("CC_Def_Warn_InRock".Translate(plan.inRock.Count));
            if (plan.mine.Count > 0) plan.warnings.Add("CC_Plan_Warn_Mine".Translate(plan.mine.Count));
            if (plan.deconstruct.Count > 0) plan.warnings.Add("CC_Plan_Warn_Deconstruct".Translate(plan.deconstruct.Count));
            int waiting = plan.Count(PieceState.Waiting);
            if (waiting > 0) plan.warnings.Add("CC_Plan_Warn_Waiting".Translate(waiting));
            // Mortars under a roof can't fire.
            foreach (var p in plan.pieces)
            {
                if (p.group != "mortar") continue;
                if (p.rect.Cells.Any(c => c.InBounds(map) && c.Roofed(map)))
                {
                    plan.warnings.Add("CC_Def_Warn_MortarRoof".Translate());
                    break;
                }
            }
            // Walls of wood near where the fighting is.
            var wall = plan.pieces.FirstOrDefault(p => p.group == "wall" && p.state != PieceState.Present);
            if (wall?.stuff?.stuffProps?.categories != null && wall.stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Woody) && t.kind != "palisade")
                plan.warnings.Add("CC_Def_Warn_Wood".Translate(wall.stuff.label));
            if (plan.pieces.Any(p => p.group == "trap" && p.state != PieceState.Present && map.areaManager.Home[p.cell]))
                plan.warnings.Add("CC_Def_Warn_TrapsHome".Translate());
            if (plan.clearBlocked.Count > 0) plan.warnings.Add("CC_Def_Warn_ClearBlocked".Translate(plan.clearBlocked.Count));
            if (plan.chunks > 0) plan.warnings.Add("CC_Def_Warn_Chunks".Translate(plan.chunks));
        }

        // ---------------- Doing it ----------------

        /// <summary>Places the blueprints and designations, queues what has to wait, and says what was planned.</summary>
        public static string Execute(DefensePlan plan, Map map)
        {
            int placed = 0, queued = 0, cut = 0, mined = 0, removed = 0, skipped = 0, present = 0;
            var queue = map.GetComponent<CouncilPlanQueue>();
            var dm = map.designationManager;
            string label = plan.template.LabelNow;
            foreach (var p in plan.cut)
            {
                if (p.Destroyed || !p.Spawned || dm.DesignationOn(p) != null) continue;
                var des = p.HarvestableNow && p.def.plant?.harvestedThingDef != null ? DesignationDefOf.HarvestPlant : DesignationDefOf.CutPlant;
                dm.AddDesignation(new Designation(p, des));
                cut++;
            }
            foreach (var c in plan.mine)
            {
                if (dm.DesignationAt(c, DesignationDefOf.Mine) != null) continue;
                var ed = c.GetEdifice(map);
                if (ed == null || !ed.def.mineable) continue;
                dm.AddDesignation(new Designation(c, DesignationDefOf.Mine));
                mined++;
            }
            foreach (var t in plan.deconstruct)
            {
                if (t.Destroyed || dm.DesignationOn(t, DesignationDefOf.Deconstruct) != null) continue;
                dm.AddDesignation(new Designation(t, DesignationDefOf.Deconstruct));
                removed++;
            }
            var built = new Dictionary<ThingDef, int>();
            foreach (var p in plan.pieces)
            {
                switch (p.state)
                {
                    case PieceState.Present:
                        present++;
                        break;
                    case PieceState.Ready:
                        if (TemplatePlanner.TryPlace(p.def, p.cell, p.rot, p.stuff, map))
                        {
                            placed++;
                            built[p.def] = (built.TryGetValue(p.def, out int n) ? n : 0) + 1;
                        }
                        else skipped++;
                        break;
                    case PieceState.Waiting:
                        queue?.Add(p.def, p.stuff, p.cell, p.rot, label);
                        queued++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
            string what = built.Count == 0 ? "CC_Def_Nothing".Translate().ToString()
                : TextUtil.JoinAnd(built.OrderByDescending(kv => kv.Value).Select(kv => kv.Value > 1 ? kv.Value + " × " + kv.Key.label : kv.Key.label).ToList());
            string s = "CC_Def_Done".Translate(plan.template.LabelNow, what).Resolve();
            if (plan.extension.Count > 0) s += " " + "CC_Def_DoneGap".Translate(plan.extension.Count).Resolve();
            if (mined > 0) s += " " + "CC_Plan_Mined".Translate(mined);
            if (cut > 0) s += " " + "CC_Plan_Cut".Translate(cut);
            if (removed > 0) s += " " + "CC_Plan_Removed".Translate(removed);
            if (queued > 0) s += " " + "CC_Plan_Queued".Translate(queued);
            if (skipped > 0) s += " " + "CC_Def_Skipped".Translate(skipped);
            return s;
        }

        // ---------------- The suggested spot ----------------

        /// <summary>
        /// Where the survey would put this template: on the gap the most raids pass that isn't held yet (its line across
        /// the gap, facing the raiders), in front of it (a trap corridor), behind it (a turret nest, a shooting post),
        /// between it and the beds (a fallback line), or among the beds and deep inside (a keep, a mortar pit). Without a
        /// narrow place, where the busiest way in crosses open ground. The spot and turn where most of it can be built
        /// nearby win. False if nothing fits.
        /// </summary>
        public static bool FindSpot(DefenseTemplateDef t, Map map, DefensePlanConfig cfg, List<DefensePiece> resolved, int chokeIndex, out IntVec3 anchor, out int rot)
        {
            anchor = IntVec3.Invalid;
            rot = cfg.rotation;
            var s = DefenseSurvey.For(map);
            if (s == null || resolved == null || resolved.Count == 0) return false;
            var targets = Targets(t, s, chokeIndex);
            float best = float.MinValue;
            var trial = cfg.Copy();
            var g = t.Grid;
            foreach (var (cell, facing, cx, cz, jitter) in targets)
            {
                for (int dx = -jitter; dx <= jitter; dx++)
                {
                    for (int dz = -jitter; dz <= jitter; dz++)
                    {
                        var at = new IntVec3(cell.x + dx, 0, cell.z + dz);
                        if (!at.InBounds(map)) continue;
                        DefenseLayout.FaceAt(g, cx, cz, trial.mirror, facing, at.x, at.z, out int turn, out int ax, out int az);
                        trial.rotation = turn;
                        var a = new IntVec3(ax, 0, az);
                        var plan = Plan(t, map, a, trial, resolved, true);
                        if (!plan.CanPlace) continue;
                        float score = plan.Count(PieceState.Ready) * 2f + plan.Count(PieceState.Present) * 2.5f - plan.Count(PieceState.Waiting) * 0.5f
                                      - plan.pieces.Count(p => p.state == PieceState.Blocked && !plan.inRock.Contains(p)) * 4f - plan.inRock.Count * 0.5f
                                      - plan.mine.Count * 0.6f - plan.clearBlocked.Count * 3f
                                      - (Mathf.Abs(dx) + Mathf.Abs(dz)) * 0.8f;
                        // The roof over a mortar stops it firing.
                        if (t.kind == "mortar" && plan.pieces.Any(p => p.group == "mortar" && p.rect.Cells.Any(c => c.Roofed(map)))) score -= 40f;
                        if (score > best)
                        {
                            best = score;
                            anchor = a;
                            rot = turn;
                        }
                    }
                }
            }
            return anchor.IsValid;
        }

        /// <summary>The cells the template's anchor cell (cx, cz) could land on, the side to face, and how far to look round each.</summary>
        private static List<(IntVec3 cell, int facing, int cx, int cz, int jitter)> Targets(DefenseTemplateDef t, DefenseSurvey s, int chokeIndex)
        {
            var list = new List<(IntVec3, int, int, int, int)>();
            var g = t.Grid;
            int midX = g.W / 2;
            // The line's gate, when it has one in the middle: it should sit on the gap.
            if (g.line >= 0)
            {
                for (int x = 0; x < g.W; x++)
                    if (g.cells[x, g.line] == DefenseLayout.Gate || (t.kind == "killbox" && g.cells[x, g.line] == DefenseLayout.Open))
                    {
                        midX = x;
                        break;
                    }
            }
            var spots = new List<(IntVec3 cell, int facing)>();
            if (chokeIndex >= 0 && chokeIndex < s.chokes.Count) spots.Add((s.chokes[chokeIndex].cell, s.chokes[chokeIndex].c.facing));
            else
            {
                foreach (var c in s.chokes.Where(c => !c.gated || t.kind != "gate").Take(3)) spots.Add((c.cell, c.c.facing));
                foreach (var c in s.chokes.Where(c => c.gated && t.kind == "gate").Take(1)) spots.Add((c.cell, c.c.facing));
            }
            if (spots.Count == 0 && s.approach.IsValid) spots.Add((s.approach, s.approachFacing));
            var core = s.bedCenter.IsValid ? s.bedCenter : s.center;
            switch (t.placeAt)
            {
                case "front":
                    foreach (var (cell, facing) in spots) list.Add((cell + IntVec3.North.RotatedBy(new Rot4(facing)) * 2, facing, midX, 0, 2));
                    break;
                case "behind":
                    foreach (var (cell, facing) in spots) list.Add((cell - IntVec3.North.RotatedBy(new Rot4(facing)) * t.behind, facing, midX, g.H - 1, 3));
                    break;
                case "between":
                    foreach (var (cell, facing) in spots)
                    {
                        float d = cell.DistanceTo(core);
                        var toward = core - cell;
                        float k = d > 1f ? Mathf.Min(14f, d * 0.5f) / d : 0f;
                        var at = new IntVec3(cell.x + Mathf.RoundToInt(toward.x * k), 0, cell.z + Mathf.RoundToInt(toward.z * k));
                        list.Add((at, facing, midX, Mathf.Max(0, g.line), 3));
                    }
                    break;
                case "core":
                    // Among the beds (a keep), or deep inside (a mortar pit), the door or the guns facing the raiders.
                    int f = s.chokes.Count > 0 ? s.chokes[0].c.facing : s.mainFacing;
                    var from = t.kind == "mortar" ? s.center - IntVec3.North.RotatedBy(new Rot4(f)) * 6 : core;
                    list.Add((from, f, g.W / 2, g.H / 2, 8));
                    break;
                default:
                    foreach (var (cell, facing) in spots) list.Add((cell, facing, midX, Mathf.Max(0, g.line), 1));
                    break;
            }
            return list;
        }
    }
}
