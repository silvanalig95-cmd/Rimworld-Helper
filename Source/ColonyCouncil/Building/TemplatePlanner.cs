using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum PieceState
    {
        /// <summary>Blueprint can go down right away.</summary>
        Ready,
        /// <summary>Rock has to be mined or a building taken down first; the blueprint follows by itself.</summary>
        Waiting,
        /// <summary>Can't be placed here; it is left out.</summary>
        Blocked,
        /// <summary>Upgrading a room: a piece like it is already there (built or planned), so nothing is added.</summary>
        Present
    }

    public class PlannedThing
    {
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rot;
        public PieceState state;
        public string reason;
        public bool blocked => state == PieceState.Blocked;
        /// <summary>The template slot it came from (-1 for added decor).</summary>
        public int slot = -1;
        public string group;
        /// <summary>Its footprint on the map.</summary>
        public CellRect rect;
        public ResolvedThing source;
        /// <summary>Left out because the ground can't hold it (water, marsh, mud), not because something is in the way.</summary>
        public bool ground;
    }

    /// <summary>Where every wall, door and piece of furniture of a template would go, and what's in the way.</summary>
    public class PlacementPlan
    {
        public RoomTemplateDef template;
        public int rotation;
        public bool mirror;
        public IntVec3 anchor;
        public ThingDef wallStuff;
        public ThingDef doorDef;
        public ThingDef doorStuff;
        public TerrainDef floor;
        public ThingDef floorStuff;

        public List<IntVec3> wallNew = new List<IntVec3>();
        public List<IntVec3> wallWaiting = new List<IntVec3>();
        public List<IntVec3> wallReuse = new List<IntVec3>();
        public List<IntVec3> wallBlocked = new List<IntVec3>();
        public List<IntVec3> interior = new List<IntVec3>();
        public List<IntVec3> interiorBlocked = new List<IntVec3>();
        /// <summary>Wall cells on ground that can't hold a wall (water, marsh, mud): left open, the rest still goes down.</summary>
        public List<IntVec3> wallSkipped = new List<IntVec3>();
        /// <summary>Cells inside on ground nothing can stand on (deep water): no floor and no furniture there.</summary>
        public List<IntVec3> interiorSkipped = new List<IntVec3>();
        public IntVec3 door = IntVec3.Invalid;
        public PieceState doorState = PieceState.Ready;
        public bool doorExists;
        public string doorReason;
        /// <summary>The door's cell can't hold a door, so the room goes down without one (<see cref="doorState"/> is Blocked).</summary>
        public bool doorSkipped;
        /// <summary>What the game said about a wall it won't place, when it isn't the ground or a building.</summary>
        public string otherReason;

        /// <summary>
        /// Wall cells in unexplored ground. Nothing is said about what's there: rock stays as the wall, open ground gets a
        /// wall once the colonists uncover it (queued, see <see cref="CouncilPlanQueue"/>).
        /// </summary>
        public List<IntVec3> wallFog = new List<IntVec3>();
        /// <summary>Cells inside the room in unexplored ground: marked for mining and dug out as the colonists reach them.</summary>
        public HashSet<IntVec3> fogInside = new HashSet<IntVec3>();
        /// <summary>Part of the room lies in unexplored ground and "mine rock" is off, or all of it does.</summary>
        public bool fogBlocked, allFog;

        /// <summary>Cells to mine (inside the room, the doorway, and the step in front of the door).</summary>
        public List<IntVec3> mine = new List<IntVec3>();
        public HashSet<IntVec3> mineSet = new HashSet<IntVec3>();
        public List<Thing> deconstruct = new List<Thing>();
        public HashSet<IntVec3> deconstructCells = new HashSet<IntVec3>();
        public List<Plant> cut = new List<Plant>();
        public List<IntVec3> smoothWalls = new List<IntVec3>();
        public List<IntVec3> smoothFloors = new List<IntVec3>();
        /// <summary>Growing-zone cells that would end up under walls or inside the room.</summary>
        public List<IntVec3> zoneCells = new List<IntVec3>();
        public List<PlannedThing> furniture = new List<PlannedThing>();
        public List<string> warnings = new List<string>();

        public bool rockBlocked, ownBlocked, edgeBlocked, otherBlocked;
        /// <summary>Furnishing a room that's already built: no walls or door, only what's missing.</summary>
        public bool upgrade;
        public Room upgradeRoom;

        /// <summary>A door in a west or east wall turns with it (the game draws doors by the wall they sit in).</summary>
        public Rot4 DoorRot => new Rot4(TemplateMath.DoorFacing(rotation));

        /// <summary>
        /// Every part of the room can go down where it's drawn: nothing in the way and nothing left out for the ground.
        /// "Find a good spot" only suggests spots like this.
        /// </summary>
        public bool Valid => CanPlace && Skipped == 0;

        /// <summary>
        /// Nothing the player has to deal with first is in the way, so a click places it. Cells whose ground can't hold
        /// anything are simply left out (a warning), unless that leaves nothing at all to build.
        /// </summary>
        public bool CanPlace => !allFog && wallBlocked.Count == 0 && interiorBlocked.Count == 0 && (doorState != PieceState.Blocked || doorSkipped)
                                && (Skipped == 0 || AnythingToBuild);

        /// <summary>Wall segments, inside cells and the door left out because of the ground.</summary>
        public int Skipped => wallSkipped.Count + interiorSkipped.Count + (doorSkipped ? 1 : 0);

        /// <summary>Furniture left out because the ground under it can't hold it.</summary>
        public int GroundFurniture => furniture.Count(f => f.state == PieceState.Blocked && f.ground);

        /// <summary>Anything at all left out because of the ground: the click then says what and why.</summary>
        public bool LeavesOutGround => Skipped > 0 || GroundFurniture > 0;

        /// <summary>At least one wall, the door, a piece of furniture or some floor would go down.</summary>
        public bool AnythingToBuild =>
            wallNew.Count + wallWaiting.Count + wallFog.Count > 0
            || (!doorExists && (doorState == PieceState.Ready || doorState == PieceState.Waiting))
            || furniture.Any(f => f.state == PieceState.Ready || f.state == PieceState.Waiting)
            || floorCells > 0;

        public string FailReason
        {
            get
            {
                var reasons = new List<string>();
                if (doorState == PieceState.Blocked && !doorSkipped && !doorReason.NullOrEmpty()) reasons.Add(doorReason);
                if (edgeBlocked) reasons.Add("CC_Plan_Block_Edge".Translate());
                if (allFog) reasons.Add("CC_Plan_Block_AllFog".Translate());
                else if (fogBlocked) reasons.Add("CC_Plan_Block_Fog".Translate());
                if (rockBlocked) reasons.Add("CC_Plan_Block_Rock".Translate());
                if (ownBlocked) reasons.Add("CC_Plan_Block_Own".Translate());
                if (otherBlocked) reasons.Add(otherReason.NullOrEmpty() ? "CC_Plan_Block_Other".Translate().ToString() : otherReason);
                if (Skipped > 0 && !AnythingToBuild) reasons.Add("CC_Plan_Block_Ground".Translate());
                if (reasons.Count == 0 && !CanPlace) reasons.Add("CC_Plan_Blocked".Translate());
                return reasons.Count == 0 ? null : string.Join("\n", reasons.Distinct());
            }
        }

        private Dictionary<ThingDef, int> cost;

        /// <summary>Everything the room still needs, by material, compared later with the colony's stock. Worked out once per plan (the panel asks every frame).</summary>
        public Dictionary<ThingDef, int> Cost() => cost ??= CountCost();

        private Dictionary<ThingDef, int> CountCost()
        {
            var cost = new Dictionary<ThingDef, int>();
            void Add(BuildableDef def, ThingDef stuff, int times)
            {
                if (def == null || times <= 0) return;
                if (def.MadeFromStuff && stuff == null) return;
                try
                {
                    foreach (var c in def.CostListAdjusted(stuff, false))
                    {
                        if (c?.thingDef == null) continue;
                        cost.TryGetValue(c.thingDef, out int have);
                        cost[c.thingDef] = have + c.count * times;
                    }
                }
                catch
                {
                    // A modded cost list that can't be read: left out of the sum rather than breaking the panel.
                }
            }
            Add(ThingDefOf.Wall, wallStuff, wallNew.Count + wallWaiting.Count);
            if (!doorExists && doorState != PieceState.Blocked) Add(doorDef, doorStuff, 1);
            foreach (var f in furniture)
            {
                if (f.state == PieceState.Ready || f.state == PieceState.Waiting) Add(f.def, f.stuff, 1);
            }
            if (floor != null) Add(floor, floorStuff, FloorCellCount);
            return cost;
        }

        [Unsaved] public int floorCells = -1;
        public int FloorCellCount => floorCells;
    }

    public static class TemplatePlanner
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Anchor (bottom-left of the rotated footprint) so that <paramref name="center"/> sits in the middle.</summary>
        public static IntVec3 AnchorForCenter(RoomTemplateDef t, IntVec3 center, int rot)
        {
            TemplateMath.RotatedSize(t.FullWidth, t.FullHeight, rot, out int rw, out int rh);
            return new IntVec3(center.x - rw / 2, 0, center.z - rh / 2);
        }

        private static IntVec3 ToWorld(IntVec3 anchor, int fx, int fz, int w, int h, bool mirror, int rot)
        {
            TemplateMath.Transform(fx, fz, w, h, mirror, rot, out int rx, out int rz);
            return new IntVec3(anchor.x + rx, 0, anchor.z + rz);
        }

        private static bool IsOwn(Thing t) => t.Faction == Faction.OfPlayer;

        private static bool CanTakeDown(Thing t)
        {
            if (!IsOwn(t) || t.def.building == null || !t.def.building.IsDeconstructible) return false;
            return !(t is Building b) || b.DeconstructibleBy(Faction.OfPlayer).Accepted;
        }

        private static bool IsMineable(Thing t) => t?.def != null && t.def.mineable;

        /// <summary>Works out every cell of the room. Nothing is changed on the map.</summary>
        public static PlacementPlan Plan(RoomTemplateDef t, Map map, IntVec3 anchor, RoomPlanConfig cfg, List<ResolvedThing> furniture, bool quick = false)
        {
            int rot = TemplateMath.Norm(cfg.rotation);
            bool mirror = cfg.mirror;
            var wallStuff = cfg.WallStuff(t, map);
            var doorDef = cfg.DoorDef(map);
            var plan = new PlacementPlan
            {
                template = t,
                rotation = rot,
                mirror = mirror,
                anchor = anchor,
                wallStuff = wallStuff,
                doorDef = doorDef,
                doorStuff = cfg.DoorStuff(doorDef, wallStuff, map),
                floor = cfg.Floor(t, map)
            };
            plan.floorStuff = cfg.FloorStuff(plan.floor, map, t.size.x * t.size.z);
            bool cutPlants = S.planCutPlants, mineRock = S.planMineRock, replace = S.planReplaceOwn, smooth = S.planSmoothStone;

            int w = t.FullWidth, h = t.FullHeight;
            int doorFx = cfg.DoorX(t) + 1;
            var reservedWall = new HashSet<long>();
            // In-wall pieces (coolers) replace a wall segment, but only if furniture is being planned at all.
            if (furniture != null && S.planFurniture)
            {
                foreach (var f in furniture)
                {
                    if (!f.inWall) continue;
                    foreach (var c in f.rect) reservedWall.Add(((long)c.x << 32) | (uint)c.z);
                }
            }

            // Cells of furniture footprints, so plants there are always cleared and rock knows what waits on it.
            var furnitureCells = new HashSet<IntVec3>();
            var placed = new List<PlannedThing>();
            if (furniture != null && S.planFurniture)
            {
                foreach (var f in furniture)
                {
                    var pt = Piece(f, t, anchor, mirror, rot, out var worldRect);
                    pt.rect = worldRect;
                    placed.Add(pt);
                    foreach (var c in worldRect) furnitureCells.Add(c);
                }
            }

            int floorCount = 0, seen = 0;
            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, mirror, rot);
                    bool isWall = fx == 0 || fz == 0 || fx == w - 1 || fz == h - 1;
                    bool isDoor = fz == 0 && fx == doorFx;
                    bool isCorner = (fx == 0 || fx == w - 1) && (fz == 0 || fz == h - 1);
                    if (!cell.InBounds(map))
                    {
                        plan.edgeBlocked = true;
                        if (isDoor) plan.doorState = PieceState.Blocked;
                        else if (isWall) plan.wallBlocked.Add(cell);
                        else plan.interiorBlocked.Add(cell);
                        continue;
                    }
                    if (cell.Fogged(map))
                    {
                        PlanFog(plan, map, cell, isDoor, isWall && !reservedWall.Contains(((long)fx << 32) | (uint)fz), isWall, mineRock);
                        continue;
                    }
                    seen++;
                    var ed = cell.GetEdifice(map);

                    if (!quick)
                    {
                        bool inFurniture = furnitureCells.Contains(cell);
                        if (cutPlants && (isWall || inFurniture || !t.keepPlants))
                        {
                            var plant = cell.GetPlant(map);
                            if (plant != null) plan.cut.Add(plant);
                        }
                        var zone = cell.GetZone(map);
                        if (zone is Zone_Growing && (isWall || inFurniture || !t.keepPlants)) plan.zoneCells.Add(cell);
                    }

                    if (isDoor)
                    {
                        plan.door = cell;
                        PlanDoor(plan, map, cell, ed, mineRock, replace);
                    }
                    else if (isWall)
                    {
                        if (reservedWall.Contains(((long)fx << 32) | (uint)fz))
                        {
                            // The piece in the wall (a cooler) needs its cell cleared like any other wall cell. If it can't
                            // go in after all, the wall goes up there instead (WallInstead, below).
                            if (ed != null && IsOwn(ed) && replace && !TemplatePlanner.IsWallLike(ed.def) && !ed.def.IsDoor && CanTakeDown(ed))
                            {
                                if (!plan.deconstruct.Contains(ed)) plan.deconstruct.Add(ed);
                                plan.deconstructCells.Add(cell);
                            }
                            continue;
                        }
                        if (ed != null && (TemplatePlanner.IsWallLike(ed.def) || ed.def.IsDoor))
                        {
                            plan.wallReuse.Add(cell);
                            if (smooth && !isCorner && ed.def.building?.smoothedThing != null && map.designationManager.DesignationAt(cell, DesignationDefOf.SmoothWall) == null)
                                plan.smoothWalls.Add(cell);
                        }
                        else if (HasWallBlueprint(cell, map)) plan.wallReuse.Add(cell);
                        // Water, marsh or mud: no wall can stand there whatever is cleared, so the gap is left and the rest goes down.
                        else if (!GroundHolds(ThingDefOf.Wall, cell, map, Rot4.North, wallStuff)) plan.wallSkipped.Add(cell);
                        else if (ed != null && IsOwn(ed) && replace && CanTakeDown(ed))
                        {
                            plan.deconstruct.Add(ed);
                            plan.deconstructCells.Add(cell);
                            plan.wallWaiting.Add(cell);
                        }
                        else if (ed != null && IsOwn(ed))
                        {
                            plan.ownBlocked = true;
                            plan.wallBlocked.Add(cell);
                        }
                        else
                        {
                            var report = GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map, false, null, null, wallStuff);
                            if (report.Accepted) plan.wallNew.Add(cell);
                            else
                            {
                                plan.otherBlocked = true;
                                if (ed == null && plan.otherReason == null && !report.Reason.NullOrEmpty()) plan.otherReason = report.Reason;
                                plan.wallBlocked.Add(cell);
                            }
                        }
                    }
                    else
                    {
                        plan.interior.Add(cell);
                        if (ed != null)
                        {
                            if (IsMineable(ed))
                            {
                                if (mineRock) AddMine(plan, cell, map);
                                else
                                {
                                    plan.rockBlocked = true;
                                    plan.interiorBlocked.Add(cell);
                                }
                            }
                            else if (IsOwn(ed) && replace && CanTakeDown(ed))
                            {
                                if (!plan.deconstruct.Contains(ed)) plan.deconstruct.Add(ed);
                                plan.deconstructCells.Add(cell);
                            }
                            else if (IsOwn(ed))
                            {
                                plan.ownBlocked = true;
                                plan.interiorBlocked.Add(cell);
                            }
                            else
                            {
                                plan.otherBlocked = true;
                                plan.interiorBlocked.Add(cell);
                            }
                        }
                        else if (cell.GetTerrain(map).passability == Traversability.Impassable)
                        {
                            // Deep water and the like: nothing goes there, the rest of the room still does.
                            plan.interiorSkipped.Add(cell);
                            continue;
                        }
                        if (!quick)
                        {
                            var terrain = cell.GetTerrain(map);
                            // Only where the floor can be laid: it isn't counted (or paid for) on ground that won't take it.
                            if (plan.floor != null && terrain != plan.floor)
                            {
                                if (GroundHolds(plan.floor, cell, map, Rot4.North, plan.floorStuff)) floorCount++;
                            }
                            else if (plan.floor == null && smooth && terrain.smoothedTerrain != null && ed == null
                                     && map.designationManager.DesignationAt(cell, DesignationDefOf.SmoothFloor) == null)
                                plan.smoothFloors.Add(cell);
                        }
                    }
                }
            }
            plan.floorCells = floorCount;
            // Digging in from ground the colony knows: a room wholly in the dark has nowhere to start from.
            if (seen == 0 && !plan.edgeBlocked) plan.allFog = true;

            var lamps = new List<PlannedThing>();
            foreach (var pt in placed)
            {
                // Wall lamps last: they need to know which walls go up and where everything else stands.
                if (pt.source != null && pt.source.OnWall)
                {
                    lamps.Add(pt);
                    continue;
                }
                if (pt.state != PieceState.Blocked) pt.state = FurnitureState(plan, map, pt.source, pt.rect, pt.cell, pt.rot, out pt.reason, out pt.ground);
                plan.furniture.Add(pt);
                // A piece in the wall that can't go in leaves no hole in it.
                if (pt.state == PieceState.Blocked && pt.source != null && pt.source.inWall)
                    foreach (var c in pt.rect) WallInstead(plan, map, c);
            }
            if (lamps.Count > 0) HangLamps(plan, map, lamps, cutPlants && !quick);

            if (!quick) AddWarnings(plan, map);
            return plan;
        }

        /// <summary>
        /// Where a piece goes once the room is mirrored and turned: its footprint, its root cell and facing, worked out by
        /// the same rules the tests check (<see cref="TemplateLayout.ToWorld"/>). A piece the game only builds facing north
        /// keeps facing north if its footprint allows, else it's left out; a door faces the way of its wall.
        /// </summary>
        private static PlannedThing Piece(ResolvedThing f, RoomTemplateDef t, IntVec3 anchor, bool mirror, int turn, out CellRect worldRect)
        {
            var r = f.rect;
            int offset = f.inWall ? 0 : 1;
            bool ok = TemplateLayout.ToWorld(t.FullWidth, t.FullHeight, mirror, turn, r.minX + offset, r.minZ + offset, r.Width, r.Height, f.rot.AsInt,
                f.def.rotatable, f.def.IsDoor, f.def.size.x, f.def.size.z,
                out int mx, out int mz, out int sx, out int sz, out int facing, out int rx, out int rz);
            worldRect = new CellRect(anchor.x + mx, anchor.z + mz, sx, sz);
            var pt = new PlannedThing
            {
                def = f.def,
                stuff = f.stuff,
                cell = new IntVec3(anchor.x + rx, 0, anchor.z + rz),
                rot = new Rot4(facing),
                slot = f.slot,
                group = f.group,
                source = f
            };
            if (!ok)
            {
                pt.state = PieceState.Blocked;
                pt.reason = "CC_Plan_Block_CantTurn".Translate(f.def.label);
            }
            return pt;
        }

        // ---------------- Wall lamps ----------------

        /// <summary>
        /// The wall lamps on the map: each on the floor cell in front of its wall, facing it (the game looks for the wall at
        /// cell + rot.FacingCell). A lamp whose wall won't be there or won't take it (a gap left over water, a door, a
        /// cooler, rock that holds no lamp), or whose cell is taken, moves to the best other spot on the same area's walls
        /// (<see cref="LightMath.Relocate"/>); one the other lamps make unneeded is dropped, and one with nowhere left to
        /// hang is left out and says why. The same in the preview and in the blueprints.
        /// </summary>
        private static void HangLamps(PlacementPlan plan, Map map, List<PlannedThing> lamps, bool cutPlants)
        {
            var t = plan.template;
            int w = t.FullWidth, h = t.FullHeight;
            var newWalls = new HashSet<IntVec3>(plan.wallNew);
            var waitingWalls = new HashSet<IntVec3>(plan.wallWaiting);
            // A wall in unexplored ground is still to come (rock that stays, or a wall once it's uncovered).
            waitingWalls.UnionWith(plan.wallFog);
            var oldWalls = new HashSet<IntVec3>(plan.wallReuse);
            var taken = new HashSet<IntVec3>(plan.interiorBlocked);
            taken.UnionWith(plan.interiorSkipped);
            foreach (var f in plan.furniture)
            {
                if (f.state == PieceState.Blocked) continue;
                foreach (var c in f.rect) taken.Add(c);
                // A partition wall the plan builds holds a lamp like the room's own walls.
                if (f.def != null && f.def.IsWall && WallLights.Supports(f.def)) (f.state == PieceState.Waiting ? waitingWalls : newWalls).Add(f.cell);
            }
            foreach (var group in lamps.GroupBy(l => l.source.lamps))
            {
                var lp = group.Key;
                var first = group.First();
                (IntVec3 cell, Rot4 rot) World(int i)
                {
                    var s = lp.candidates[i];
                    var cell = ToWorld(plan.anchor, s.x + 1, s.z + 1, w, h, plan.mirror, plan.rotation);
                    return (cell, new Rot4(TemplateMath.TransformFacing(s.facing, plan.mirror, plan.rotation)));
                }
                bool Usable(int i)
                {
                    var (cell, rot) = World(i);
                    return LampState(plan, map, first.def, first.stuff, cell, rot, newWalls, waitingWalls, oldWalls, taken, out _) != PieceState.Blocked;
                }
                List<int> spots;
                try
                {
                    spots = LightMath.Relocate(lp, Usable);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("room plan wall lamps", e);
                    spots = new List<int>(lp.chosen);
                }
                foreach (var pt in group)
                {
                    int k = pt.source.lamp;
                    int i = k >= 0 && k < spots.Count ? spots[k] : LightMath.Nowhere;
                    // The other lamps light its part of the room.
                    if (i == LightMath.NotNeeded) continue;
                    if (i < 0)
                    {
                        pt.state = PieceState.Blocked;
                        pt.reason = "CC_Plan_Block_NoWallForLamp".Translate(pt.def.label);
                        plan.furniture.Add(pt);
                        continue;
                    }
                    var (cell, rot) = World(i);
                    bool moved = cell != pt.cell;
                    pt.cell = cell;
                    pt.rot = rot;
                    pt.rect = CellRect.SingleCell(cell);
                    pt.state = LampState(plan, map, pt.def, pt.stuff, cell, rot, newWalls, waitingWalls, oldWalls, taken, out pt.reason);
                    plan.furniture.Add(pt);
                    // Smoothing swaps the rock for a new wall, and what hangs on it could come down: that stretch stays rough.
                    if (pt.state != PieceState.Blocked) plan.smoothWalls.Remove(cell + rot.FacingCell);
                    if (moved && cutPlants && cell.InBounds(map))
                    {
                        var plant = cell.GetPlant(map);
                        if (plant != null && !plan.cut.Contains(plant)) plan.cut.Add(plant);
                    }
                }
            }
        }

        /// <summary>
        /// Whether a wall lamp can go on this cell facing this way: a free floor cell (or one the plan clears first) in
        /// front of a wall that takes lamps: one this plan builds, or one already there that the plan keeps. It waits while
        /// its wall or its cell waits on mining or something being taken down. Where the wall is already standing, the
        /// game's own placement check (its wall-attachment rule included) has the last word.
        /// </summary>
        private static PieceState LampState(PlacementPlan plan, Map map, ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot,
            HashSet<IntVec3> newWalls, HashSet<IntVec3> waitingWalls, HashSet<IntVec3> oldWalls, HashSet<IntVec3> taken, out string reason)
        {
            reason = null;
            if (!cell.InBounds(map) || (cell.Fogged(map) && !plan.mineSet.Contains(cell)))
            {
                reason = "CC_Plan_Block_Edge".Translate();
                return PieceState.Blocked;
            }
            if (taken.Contains(cell) || WallLights.PlannedHere(cell, map))
            {
                reason = "CC_Plan_Block_LampCell".Translate(def.label);
                return PieceState.Blocked;
            }
            bool waiting = plan.mineSet.Contains(cell) || plan.deconstructCells.Contains(cell);
            var ed = cell.GetEdifice(map);
            if (ed != null && !waiting)
            {
                reason = "CC_Plan_Block_Thing".Translate(ed.LabelShort);
                return PieceState.Blocked;
            }
            var wall = cell + rot.FacingCell;
            bool standing = false;
            if (waitingWalls.Contains(wall)) waiting = true;
            else if (!newWalls.Contains(wall))
            {
                if (!oldWalls.Contains(wall) || !WallLights.Holds(wall, map))
                {
                    reason = "CC_Plan_Block_NoWallForLamp".Translate(def.label);
                    return PieceState.Blocked;
                }
                // Built, not just planned: only then can the game's own check judge the lamp now.
                standing = wall.GetEdifice(map) is Building built && WallLights.Supports(built.def);
            }
            if (WallLights.SideTaken(cell, rot, map))
            {
                reason = "CC_Plan_Block_LampTaken".Translate(def.label);
                return PieceState.Blocked;
            }
            if (waiting) return PieceState.Waiting;
            if (standing)
            {
                var report = GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map, false, null, null, stuff);
                if (!report.Accepted)
                {
                    reason = report.Reason;
                    return PieceState.Blocked;
                }
            }
            return PieceState.Ready;
        }

        // ---------------- Upgrading a built room ----------------

        /// <summary>
        /// Lines a template up with a built room: turned so its door wall is the wall with the room's door, the door
        /// lined up where the room allows, and the template inside the room. False (with a reason) if the room can't take it.
        /// <paramref name="doorX"/> is where the room's own door sits in the template's door wall (-1 if it doesn't line up).
        /// </summary>
        public static bool FitRoom(RoomTemplateDef t, Room room, RoomPlanConfig cfg, out IntVec3 anchor, out int rot, out int doorX, out string reason)
        {
            anchor = IntVec3.Invalid;
            rot = cfg.rotation;
            doorX = -1;
            reason = null;
            if (room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge || !room.ProperRoom || room.IsDoorway)
            {
                reason = "CC_Upgrade_NotARoom".Translate();
                return false;
            }
            var rect = room.ExtentsClose;
            var map = room.Map;
            var ring = rect.ExpandedBy(1);
            IntVec3 door = IntVec3.Invalid;
            foreach (var c in ring.EdgeCells)
            {
                bool corner = (c.x == ring.minX || c.x == ring.maxX) && (c.z == ring.minZ || c.z == ring.maxZ);
                if (corner || !c.InBounds(map)) continue;
                if (c.GetEdifice(map) is Building ed && ed.def.IsDoor)
                {
                    door = c;
                    rot = TemplateMath.TurnForDoorSide(c.z == ring.minZ ? 0 : c.x == ring.minX ? 1 : c.z == ring.maxZ ? 2 : 3);
                    break;
                }
            }
            int w = t.FullWidth, h = t.FullHeight;
            TemplateMath.RotatedSize(w, h, rot, out int rw, out int rh);
            if (rw - 2 > rect.Width || rh - 2 > rect.Height)
            {
                // Both sizes the way the room lies on the map, so they can be compared.
                reason = "CC_Upgrade_TooSmall".Translate(rect.Width, rect.Height, rw - 2, rh - 2);
                return false;
            }
            // Line the template's door up with the room's, then keep the template inside the room.
            TemplateMath.Transform(cfg.DoorX(t) + 1, 0, w, h, cfg.mirror, rot, out int dx, out int dz);
            int ax = door.IsValid ? door.x - dx : rect.minX - 1;
            int az = door.IsValid ? door.z - dz : rect.minZ - 1;
            ax = Mathf.Clamp(ax, rect.minX - 1, rect.maxX + 1 - (rw - 1));
            az = Mathf.Clamp(az, rect.minZ - 1, rect.maxZ + 1 - (rh - 1));
            anchor = new IntVec3(ax, 0, az);
            if (door.IsValid)
            {
                TemplateMath.InverseTransform(door.x - ax, door.z - az, w, h, cfg.mirror, rot, out int tx, out int tz);
                if (tz == 0 && tx >= 1 && tx <= t.size.x) doorX = tx - 1;
            }
            return true;
        }

        /// <summary>
        /// Furnishing a room that's already built with a template: no walls, no door, only the pieces it doesn't have yet.
        /// A piece counts as there when the room already holds (or has planned) something of the same kind; the floor goes
        /// only on bare ground.
        /// </summary>
        public static PlacementPlan PlanUpgrade(RoomTemplateDef t, Map map, Room room, RoomPlanConfig cfg, System.Func<int, List<ResolvedThing>> resolve)
        {
            var plan = new PlacementPlan { template = t, upgrade = true, upgradeRoom = room, mirror = cfg.mirror };
            if (!FitRoom(t, room, cfg, out var anchor, out int rot, out int doorX, out string reason))
            {
                plan.doorState = PieceState.Blocked;
                plan.doorReason = reason;
                plan.doorExists = true;
                return plan;
            }
            plan.anchor = anchor;
            plan.rotation = rot;
            plan.doorExists = true;
            plan.floor = cfg.Floor(t, map);
            plan.floorStuff = cfg.FloorStuff(plan.floor, map, t.size.x * t.size.z);
            plan.wallStuff = cfg.WallStuff(t, map);
            var furniture = resolve(doorX) ?? new List<ResolvedThing>();
            var roomCells = new HashSet<IntVec3>(room.Cells);
            plan.interior.AddRange(room.Cells);
            int bare = 0;
            if (plan.floor != null)
                foreach (var c in room.Cells)
                {
                    var terrain = c.GetTerrain(map);
                    if (terrain != plan.floor && IsBare(terrain) && GroundHolds(plan.floor, c, map, Rot4.North, plan.floorStuff)) bare++;
                }
            plan.floorCells = bare;

            // What the room already has (built, framed or planned), so only the missing pieces are added.
            var existing = new List<(Thing thing, ThingDef def)>();
            foreach (var thing in room.ContainedAndAdjacentThings)
            {
                ThingDef def = thing is Building b && b.Faction == Faction.OfPlayer ? b.def
                    : (thing is Blueprint_Build || thing is Frame) ? thing.def.entityDefToBuild as ThingDef : null;
                if (def == null || def.IsWall || def.IsDoor) continue;
                if (!thing.OccupiedRect().Cells.Any(roomCells.Contains)) continue;
                existing.Add((thing, def));
            }
            var claimed = new HashSet<Thing>();
            if (S.planFurniture)
            {
                foreach (var f in furniture)
                {
                    // Walls and doors are the room's own business: a plan's partition or second door would split a room
                    // that's already laid out. Wall lamps go on its walls below.
                    if (f.def == null || f.def.IsWall || f.def.IsDoor || f.OnWall) continue;
                    var pt = Piece(f, t, anchor, cfg.mirror, rot, out var worldRect);
                    pt.rect = worldRect;
                    var match = existing.FirstOrDefault(e => !claimed.Contains(e.thing) && SameKind(e.def, f, t));
                    if (match.thing != null)
                    {
                        claimed.Add(match.thing);
                        pt.state = PieceState.Present;
                        plan.furniture.Add(pt);
                        continue;
                    }
                    if (f.inWall)
                    {
                        pt.state = PieceState.Blocked;
                        pt.reason = "CC_Upgrade_InWall".Translate(f.def.label);
                    }
                    else if (worldRect.Cells.Any(c => !roomCells.Contains(c)))
                    {
                        pt.state = PieceState.Blocked;
                        pt.reason = "CC_Upgrade_Outside".Translate(f.def.label);
                    }
                    else if (pt.state != PieceState.Blocked)
                    {
                        pt.state = FurnitureState(plan, map, f, worldRect, pt.cell, pt.rot, out pt.reason, out pt.ground);
                    }
                    plan.furniture.Add(pt);
                }
                var lamp = furniture.FirstOrDefault(f => f.OnWall);
                if (lamp != null) HangLampsInRoom(plan, map, room, lamp, existing, claimed);
            }
            int groundFurniture = plan.GroundFurniture;
            if (groundFurniture > 0) plan.warnings.Add("CC_Plan_Warn_FurnitureGround".Translate(groundFurniture));
            int blocked = plan.furniture.Count(p => p.state == PieceState.Blocked) - groundFurniture;
            if (blocked > 0) plan.warnings.Add("CC_Plan_Warn_FurnitureBlocked".Translate(blocked));
            return plan;
        }

        /// <summary>
        /// Wall lamps for a room that's already built: on its own walls, wherever they light what its lights (built or
        /// planned) leave dark, as many as it needs (<see cref="WallLights.SpotsInRoom"/>). The room may be bigger than the
        /// plan, so the spots come from the room, not from the plan's walls. Lights it already has count as there.
        /// </summary>
        private static void HangLampsInRoom(PlacementPlan plan, Map map, Room room, ResolvedThing lamp, List<(Thing thing, ThingDef def)> existing, HashSet<Thing> claimed)
        {
            foreach (var e in existing)
            {
                if (claimed.Contains(e.thing) || !Avail.IsLight(e.def)) continue;
                claimed.Add(e.thing);
                plan.furniture.Add(new PlannedThing
                {
                    def = e.def,
                    stuff = e.thing.Stuff,
                    cell = e.thing.Position,
                    rot = e.thing.Rotation,
                    rect = e.thing.OccupiedRect(),
                    state = PieceState.Present,
                    group = lamp.group,
                    source = lamp
                });
            }
            var avoid = new HashSet<IntVec3>();
            foreach (var f in plan.furniture)
                if (f.state != PieceState.Blocked)
                    foreach (var c in f.rect) avoid.Add(c);
            foreach (var (cell, rot) in WallLights.SpotsInRoom(room, map, lamp.def, lamp.stuff, LightMath.MaxWallLamps(room.CellCount), avoid, LightMath.MinWallLamps(room.CellCount)))
            {
                var rect = CellRect.SingleCell(cell);
                var pt = new PlannedThing { def = lamp.def, stuff = lamp.stuff, cell = cell, rot = rot, rect = rect, group = lamp.group, source = lamp };
                pt.state = FurnitureState(plan, map, lamp, rect, cell, rot, out pt.reason, out pt.ground);
                plan.furniture.Add(pt);
            }
        }

        private static HashSet<TerrainDef> smoothedFloors;

        /// <summary>Ground with no floor on it: not a laid floor, and not stone smoothed into one.</summary>
        public static bool IsBare(TerrainDef t)
        {
            if (t == null || t.BuildableByPlayer) return false;
            smoothedFloors ??= new HashSet<TerrainDef>(DefDatabase<TerrainDef>.AllDefsListForReading.Where(d => d.smoothedTerrain != null).Select(d => d.smoothedTerrain));
            return !smoothedFloors.Contains(t);
        }

        /// <summary>The room already has this kind of piece: the same building, or one with the same purpose.</summary>
        private static bool SameKind(ThingDef def, ResolvedThing f, RoomTemplateDef t)
        {
            if (def == f.def) return true;
            var tt = t.ThingInGroup(f.group);
            if (tt == null) return false;
            if (tt.defs != null && tt.defs.Contains(def.defName)) return true;
            if (tt.role.NullOrEmpty()) return false;
            try
            {
                var pred = RoomTemplateDef.RolePredicate(tt.role);
                return pred != null && pred(def);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// A cell of the room in unexplored ground. The plan doesn't look at what's there (the player can't see it either):
        /// the inside, the doorway and in-wall pieces are marked for mining (only rock actually gets the designation, and
        /// the colonists dig in from the ground they know), and their furniture and floor wait; a wall cell waits too, and
        /// is either rock that stays as the wall or open ground that gets its wall once uncovered.
        /// </summary>
        private static void PlanFog(PlacementPlan plan, Map map, IntVec3 cell, bool isDoor, bool isWall, bool inWallLine, bool mineRock)
        {
            if (isDoor) plan.door = cell;
            if (isWall && !isDoor)
            {
                plan.wallFog.Add(cell);
                return;
            }
            if (!isDoor && !inWallLine) plan.interior.Add(cell);
            if (!mineRock)
            {
                plan.fogBlocked = true;
                if (isDoor)
                {
                    plan.doorState = PieceState.Blocked;
                    plan.doorReason = "CC_Plan_Block_Fog".Translate();
                }
                else plan.interiorBlocked.Add(cell);
                return;
            }
            if (!isDoor && !inWallLine) plan.fogInside.Add(cell);
            AddMine(plan, cell, map);
            if (isDoor) plan.doorState = PieceState.Waiting;
        }

        private static void AddMine(PlacementPlan plan, IntVec3 cell, Map map)
        {
            if (plan.mineSet.Add(cell)) plan.mine.Add(cell);
        }

        private static void PlanDoor(PlacementPlan plan, Map map, IntVec3 cell, Building ed, bool mineRock, bool replace)
        {
            if (ed != null && ed.def.IsDoor)
            {
                plan.doorExists = true;
                return;
            }
            foreach (var thing in cell.GetThingList(map))
            {
                if ((thing is Blueprint_Build || thing is Frame) && thing.def.entityDefToBuild is ThingDef td && td.IsDoor)
                {
                    plan.doorExists = true;
                    return;
                }
            }
            // A door can't stand in water, marsh or mud: the room goes down without it, and the planner says so.
            if (!GroundHolds(plan.doorDef, cell, map, plan.DoorRot, plan.doorStuff))
            {
                plan.doorState = PieceState.Blocked;
                plan.doorSkipped = true;
                plan.doorReason = "CC_Plan_Warn_SkipDoor".Translate();
                return;
            }
            if (ed != null && IsMineable(ed))
            {
                if (mineRock)
                {
                    AddMine(plan, cell, map);
                    plan.doorState = PieceState.Waiting;
                }
                else
                {
                    plan.doorState = PieceState.Blocked;
                    plan.doorReason = "CC_Plan_Block_DoorRock".Translate();
                }
                return;
            }
            if (GenConstruct.CanPlaceBlueprintAt(plan.doorDef, cell, plan.DoorRot, map, false, null, null, plan.doorStuff).Accepted)
            {
                plan.doorState = PieceState.Ready;
                return;
            }
            if (ed != null && IsOwn(ed) && replace && CanTakeDown(ed))
            {
                plan.deconstruct.Add(ed);
                plan.deconstructCells.Add(cell);
                plan.doorState = PieceState.Waiting;
                return;
            }
            plan.doorState = PieceState.Blocked;
            plan.doorReason = ed != null && IsOwn(ed) ? "CC_Plan_Block_DoorOwn".Translate() : "CC_Plan_DoorBlocked".Translate();
        }

        /// <summary>A wall cell kept for a piece in the wall that can't go in: the wall goes up there as on the rest of the line.</summary>
        private static void WallInstead(PlacementPlan plan, Map map, IntVec3 cell)
        {
            // Off the map or in the dark it was counted already; rock or a wall standing there closes it as it is.
            if (!cell.InBounds(map) || cell.Fogged(map)) return;
            var ed = cell.GetEdifice(map);
            if (ed != null && (TemplatePlanner.IsWallLike(ed.def) || ed.def.IsDoor || IsMineable(ed))) return;
            if (plan.deconstructCells.Contains(cell)) plan.wallWaiting.Add(cell);
            else if (HasWallBlueprint(cell, map)) plan.wallReuse.Add(cell);
            else if (!GroundHolds(ThingDefOf.Wall, cell, map, Rot4.North, plan.wallStuff)) plan.wallSkipped.Add(cell);
            else if (ed != null && IsOwn(ed))
            {
                plan.ownBlocked = true;
                plan.wallBlocked.Add(cell);
            }
            else if (GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map, false, null, null, plan.wallStuff).Accepted) plan.wallNew.Add(cell);
            else
            {
                plan.otherBlocked = true;
                plan.wallBlocked.Add(cell);
            }
        }

        /// <param name="ground">Set when it's left out because the ground under it can't hold it.</param>
        private static PieceState FurnitureState(PlacementPlan plan, Map map, ResolvedThing f, CellRect rect, IntVec3 root, Rot4 rot, out string reason, out bool ground)
        {
            reason = null;
            ground = false;
            bool waiting = false;
            foreach (var c in rect)
            {
                if (!c.InBounds(map))
                {
                    reason = "CC_Plan_Block_Edge".Translate();
                    return PieceState.Blocked;
                }
                if (c.Fogged(map) && !plan.mineSet.Contains(c))
                {
                    reason = "CC_Plan_Block_Fog".Translate();
                    return PieceState.Blocked;
                }
                if (plan.mineSet.Contains(c) || plan.deconstructCells.Contains(c))
                {
                    waiting = true;
                    continue;
                }
                var ed = c.GetEdifice(map);
                if (ed != null && !(f.inWall && TemplatePlanner.IsWallLike(ed.def) && IsOwn(ed)))
                {
                    reason = "CC_Plan_Block_Thing".Translate(ed.LabelShort);
                    return PieceState.Blocked;
                }
            }
            if (waiting) return PieceState.Waiting;
            var report = GenConstruct.CanPlaceBlueprintAt(f.def, root, rot, map, false, null, null, f.stuff);
            if (!report.Accepted)
            {
                ground = !GroundHolds(f.def, root, map, rot, f.stuff);
                reason = ground ? "CC_Plan_Skip_FurnitureGround".Translate(f.def.label).ToString() : report.Reason;
                return PieceState.Blocked;
            }
            return PieceState.Ready;
        }

        private static void AddWarnings(PlacementPlan plan, Map map)
        {
            // What the ground can't hold first: it changes what the room will be.
            if (plan.wallSkipped.Count > 0) plan.warnings.Add("CC_Plan_Warn_SkipWalls".Translate(plan.wallSkipped.Count));
            if (plan.doorSkipped) plan.warnings.Add("CC_Plan_Warn_SkipDoor".Translate());
            if (plan.interiorSkipped.Count > 0) plan.warnings.Add("CC_Plan_Warn_SkipInside".Translate(plan.interiorSkipped.Count));
            // Overhead mountain inside the room: insects love it.
            int thick = 0;
            foreach (var c in plan.interior)
            {
                var roof = c.GetRoof(map);
                if (roof != null && roof.isThickRoof) thick++;
            }
            if (thick > 0) plan.warnings.Add("CC_Plan_Warn_Mountain".Translate());
            if (plan.zoneCells.Count > 0) plan.warnings.Add("CC_Plan_Warn_Zone".Translate(plan.zoneCells.Count));
            int dark = plan.wallFog.Count + plan.fogInside.Count;
            if (dark > 0) plan.warnings.Add("CC_Plan_Warn_Fog".Translate(dark));
            if (plan.mine.Count > 0) plan.warnings.Add("CC_Plan_Warn_Mine".Translate(plan.mine.Count));
            if (plan.deconstruct.Count > 0) plan.warnings.Add("CC_Plan_Warn_Deconstruct".Translate(plan.deconstruct.Count));
            int waiting = plan.furniture.Count(f => f.state == PieceState.Waiting) + plan.wallWaiting.Count + plan.wallFog.Count + (plan.doorState == PieceState.Waiting ? 1 : 0);
            if (waiting > 0) plan.warnings.Add("CC_Plan_Warn_Waiting".Translate(waiting));
            int groundFurniture = plan.GroundFurniture;
            if (groundFurniture > 0) plan.warnings.Add("CC_Plan_Warn_FurnitureGround".Translate(groundFurniture));
            int blocked = plan.furniture.Count(f => f.state == PieceState.Blocked) - groundFurniture;
            if (blocked > 0) plan.warnings.Add("CC_Plan_Warn_FurnitureBlocked".Translate(blocked));
            if (plan.template.layoutNotes != null && plan.template.layoutNotes.Count > 0)
                plan.warnings.Add("CC_Plan_Warn_Rearranged".Translate());
            if (plan.door.IsValid && !plan.doorSkipped)
            {
                var outside = DoorOutside(plan, map);
                if (outside.IsValid && outside.InBounds(map) && outside.Fogged(map))
                {
                    if (S.planMineRock) AddMine(plan, outside, map);
                }
                else if (outside.IsValid && outside.InBounds(map))
                {
                    var ed = outside.GetEdifice(map);
                    if (ed != null && IsMineable(ed) && S.planMineRock) AddMine(plan, outside, map);
                    else if (ed != null && ed.def.passability == Traversability.Impassable && !ed.def.IsDoor)
                        plan.warnings.Add("CC_Plan_Warn_DoorFacesWall".Translate());
                }
            }
        }

        /// <summary>The cell just outside the door.</summary>
        public static IntVec3 DoorOutside(PlacementPlan plan, Map map)
        {
            var t = plan.template;
            int w = t.FullWidth, h = t.FullHeight;
            var cfg = RoomPlanConfig.For(t);
            int doorFx = cfg.DoorX(t) + 1;
            var door = ToWorld(plan.anchor, doorFx, 0, w, h, plan.mirror, plan.rotation);
            var inside = ToWorld(plan.anchor, doorFx, 1, w, h, plan.mirror, plan.rotation);
            return door + (door - inside);
        }

        public static bool IsWallLike(ThingDef d)
        {
            if (d?.building == null) return false;
            if (d.building.isNaturalRock || d.mineable) return true;
            return d.IsWall || (d.passability == Traversability.Impassable && d.fillPercent >= 0.99f && d.holdsRoof);
        }

        private static bool HasWallBlueprint(IntVec3 c, Map map)
        {
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild is ThingDef td && (td.IsWall || td.IsDoor)) return true;
            }
            return false;
        }

        // ---------------- Doing it ----------------

        /// <summary>Places all blueprints and designations, queues what has to wait. Returns a summary for the player.</summary>
        public static string Execute(PlacementPlan plan, Map map)
        {
            int walls = 0, furniture = 0, floors = 0, skipped = 0, groundSkipped = 0, queued = 0, cut = 0, mined = 0, removed = 0, smoothed = 0, present = 0;
            var queue = map.GetComponent<CouncilPlanQueue>();
            string label = plan.template.label;
            var dm = map.designationManager;

            // Clearing first, so the builders find a clean site.
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
            if (plan.zoneCells.Count > 0)
            {
                var zones = new HashSet<Zone>();
                foreach (var c in plan.zoneCells)
                {
                    var z = c.GetZone(map);
                    if (z == null) continue;
                    z.RemoveCell(c);
                    zones.Add(z);
                }
                foreach (var z in zones)
                {
                    if (z.CellCount == 0) z.Delete(false);
                    else z.CheckContiguous();
                }
            }

            foreach (var c in plan.wallNew)
            {
                if (TryPlace(ThingDefOf.Wall, c, Rot4.North, plan.wallStuff, map)) walls++;
            }
            foreach (var c in plan.wallWaiting.Concat(plan.wallFog))
            {
                queue?.Add(ThingDefOf.Wall, plan.wallStuff, c, Rot4.North, label);
                queued++;
            }
            if (!plan.doorExists)
            {
                if (plan.doorState == PieceState.Ready) TryPlace(plan.doorDef, plan.door, plan.DoorRot, plan.doorStuff, map);
                else if (plan.doorState == PieceState.Waiting)
                {
                    queue?.Add(plan.doorDef, plan.doorStuff, plan.door, plan.DoorRot, label);
                    queued++;
                }
            }
            foreach (var f in plan.furniture)
            {
                if (f.state == PieceState.Present) present++;
                else if (f.state == PieceState.Ready && TryPlace(f.def, f.cell, f.rot, f.stuff, map)) furniture++;
                else if (f.state == PieceState.Ready && f.source != null && f.source.OnWall)
                {
                    // Its wall is only a blueprint yet: the lamp follows once the wall is up.
                    queue?.Add(f.def, f.stuff, f.cell, f.rot, label);
                    queued++;
                }
                else if (f.state == PieceState.Waiting)
                {
                    queue?.Add(f.def, f.stuff, f.cell, f.rot, label);
                    queued++;
                }
                else if (f.state == PieceState.Blocked && f.ground) groundSkipped++;
                else skipped++;
            }
            if (plan.floor != null)
            {
                var noFloor = plan.interiorSkipped.Count > 0 ? new HashSet<IntVec3>(plan.interiorSkipped) : null;
                foreach (var c in plan.interior)
                {
                    if (c.GetTerrain(map) == plan.floor || (noFloor != null && noFloor.Contains(c))) continue;
                    // Upgrading keeps the floors already laid; only bare ground gets the new floor.
                    if (plan.upgrade && !IsBare(c.GetTerrain(map))) continue;
                    if (plan.mineSet.Contains(c) || plan.deconstructCells.Contains(c))
                    {
                        queue?.Add(plan.floor, plan.floorStuff, c, Rot4.North, label);
                        continue;
                    }
                    if (TryPlace(plan.floor, c, Rot4.North, plan.floorStuff, map)) floors++;
                }
            }
            foreach (var c in plan.smoothWalls)
            {
                if (dm.DesignationAt(c, DesignationDefOf.SmoothWall) != null) continue;
                dm.AddDesignation(new Designation(c, DesignationDefOf.SmoothWall));
                smoothed++;
            }
            foreach (var c in plan.smoothFloors)
            {
                if (dm.DesignationAt(c, DesignationDefOf.SmoothFloor) != null || c.GetEdifice(map) != null) continue;
                dm.AddDesignation(new Designation(c, DesignationDefOf.SmoothFloor));
                smoothed++;
            }

            string s = plan.upgrade
                ? "CC_Upgrade_Done".Translate(plan.template.label ?? "", furniture, present).Resolve()
                : "CC_Plan_Done".Translate(plan.template.LabelCap.ToString(), walls, furniture, plan.wallStuff?.label ?? "-").Resolve();
            if (mined > 0) s += " " + "CC_Plan_Mined".Translate(mined);
            if (cut > 0) s += " " + "CC_Plan_Cut".Translate(cut);
            if (removed > 0) s += " " + "CC_Plan_Removed".Translate(removed);
            if (floors > 0) s += " " + "CC_Plan_Floors".Translate(floors, plan.floor.label);
            if (smoothed > 0) s += " " + "CC_Plan_Smoothed".Translate(smoothed);
            if (queued > 0) s += " " + "CC_Plan_Queued".Translate(queued);
            if (skipped > 0) s += " " + "CC_Plan_Skipped".Translate(skipped);
            // What the ground couldn't hold, and why, so a gap in the wall or a missing door is no surprise.
            var left = new List<string>();
            if (plan.wallSkipped.Count > 0) left.Add("CC_Plan_SkipPart_Walls".Translate(plan.wallSkipped.Count));
            if (plan.doorSkipped && !plan.doorExists) left.Add("CC_Plan_SkipPart_Door".Translate());
            if (groundSkipped > 0) left.Add("CC_Plan_SkipPart_Furniture".Translate(groundSkipped));
            if (left.Count > 0) s += " " + "CC_Plan_SkippedGround".Translate(TextUtil.JoinAnd(left)).Resolve();
            return s;
        }

        private static bool terrainCheckMissing;

        /// <summary>
        /// Whether the ground itself can hold this: walls and doors need firm ground, so water, marsh and mud can't take
        /// them. Asks the game; if that method isn't there (another game version), reads the terrain's affordances itself.
        /// </summary>
        public static bool GroundHolds(BuildableDef def, IntVec3 c, Map map, Rot4 rot, ThingDef stuff)
        {
            if (def == null || map == null || !c.InBounds(map)) return true;
            if (!terrainCheckMissing)
            {
                try
                {
                    return GameSaysGroundHolds(def, c, map, rot, stuff);
                }
                catch (System.MissingMethodException)
                {
                    terrainCheckMissing = true;
                }
                catch
                {
                    // One odd modded piece: read the terrain directly below.
                }
            }
            var need = def.terrainAffordanceNeeded;
            if (need == null) return true;
            var rect = def is ThingDef td ? GenAdj.OccupiedRect(c, rot, td.size) : CellRect.SingleCell(c);
            foreach (var cell in rect)
            {
                if (!cell.InBounds(map)) return false;
                var affordances = cell.GetTerrain(map)?.affordances;
                if (affordances == null || !affordances.Contains(need)) return false;
            }
            return true;
        }

        /// <summary>Kept apart so a missing game method shows up as an exception <see cref="GroundHolds"/> can catch.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool GameSaysGroundHolds(BuildableDef def, IntVec3 c, Map map, Rot4 rot, ThingDef stuff) =>
            GenConstruct.CanBuildOnTerrain(def, c, map, rot, null, stuff);

        public static bool TryPlace(BuildableDef def, IntVec3 c, Rot4 rot, ThingDef stuff, Map map)
        {
            if (def == null || !c.InBounds(map)) return false;
            if (!GenConstruct.CanPlaceBlueprintAt(def, c, rot, map, false, null, null, stuff).Accepted) return false;
            GenConstruct.PlaceBlueprintForBuild(def, c, map, rot, Faction.OfPlayer, stuff);
            return true;
        }

        // ---------------- Finding a spot ----------------

        public static IntVec3 ColonyCenter(Map map)
        {
            var beds = map.listerBuildings.allBuildingsColonist.Where(b => b is Building_Bed).ToList();
            if (beds.Count > 0)
                return new IntVec3((int)beds.Average(b => b.Position.x), 0, (int)beds.Average(b => b.Position.z));
            var pawns = map.mapPawns.FreeColonistsSpawned;
            if (pawns.Count > 0)
                return new IntVec3((int)pawns.Average(p => p.Position.x), 0, (int)pawns.Average(p => p.Position.z));
            return map.Center;
        }

        /// <summary>One spot the council suggests for a room: where, which way round, how good, and why (SpotMath).</summary>
        public class SpotCandidate
        {
            public IntVec3 anchor, mid;
            public int rot;
            public float score;
            public SpotMath.Features features;
            public List<string> nearRoles, avoidRoles;
            /// <summary>The reasons, best first: "beside the kitchen", "shares 6 cells of wall", "far from the gate" (SpotText).</summary>
            public List<(string text, bool good)> reasons = new List<(string, bool)>();
        }

        /// <summary>
        /// Searches around the colony for a good spot: buildable, close to home, sharing existing walls
        /// (cheaper and warmer), near the rooms it goes well with, avoiding zones and long mining jobs.
        /// Tries every rotation; keeps the player's mirror setting.
        /// </summary>
        public static bool FindSpot(RoomTemplateDef t, Map map, List<ResolvedThing> furniture, RoomPlanConfig cfg, out IntVec3 anchor, out int bestRot)
        {
            var spots = FindSpots(t, map, furniture, cfg, 1);
            if (spots.Count == 0)
            {
                anchor = IntVec3.Invalid;
                bestRot = cfg.rotation;
                return false;
            }
            anchor = spots[0].anchor;
            bestRot = spots[0].rot;
            return true;
        }

        /// <summary>
        /// The best few spots, apart from one another, each with its reasons: the rooms it goes with and away from
        /// (the template's near and avoid, and the usual for its kind of room), the walls it would share, the gate for
        /// a hospital or an armory, the fields for a barn, quiet for a bedroom, the rock to mine and the walk to the
        /// colony's middle. Only spots where all of it can be built.
        /// </summary>
        public static List<SpotCandidate> FindSpots(RoomTemplateDef t, Map map, List<ResolvedThing> furniture, RoomPlanConfig cfg, int want = 3, HashSet<IntVec3> taken = null)
        {
            var result = new List<SpotCandidate>();
            if (t == null || map == null) return result;
            var center = ColonyCenter(map);
            var nearRoles = RoomNeighbours.Near(t);
            var avoidRoles = RoomNeighbours.Avoid(t);
            var near = nearRoles.Select(r => RoleCenters(map, r)).ToList();
            var avoid = avoidRoles.Select(r => RoleCenters(map, r)).ToList();
            var noisy = RoomNeighbours.Quiet(t) ? RoomNeighbours.NoisyRoles.Select(r => RoleCenters(map, r)).ToList() : null;
            var gate = RoomNeighbours.NearGate(t) ? RoomNeighbours.GateCell(map) : IntVec3.Invalid;
            var fields = RoomNeighbours.NearFields(t) ? RoomNeighbours.FieldCells(map) : null;
            var trial = new RoomPlanConfig();
            CopyInto(cfg, trial);
            var candidates = new List<SpotCandidate>();
            int checkedCount = 0;
            float floor = float.MinValue;
            foreach (var c in GenRadial.RadialCellsAround(center, 40f, true))
            {
                if (((c.x + c.z) & 1) != 0) continue;
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                for (int rot = 0; rot < 4; rot++)
                {
                    var a = AnchorForCenter(t, c, rot);
                    // Ground kept for another room (the ground plan's earlier picks) is no spot.
                    if (taken != null && taken.Count > 0 && Touches(t, a, rot, cfg.mirror, taken)) continue;
                    if (!Features(t, map, a, rot, cfg.mirror, center, near, avoid, noisy, gate, fields, out var f)) continue;
                    float score = SpotMath.Score(f);
                    // The costly check only for spots that could beat what's found (a few more than wanted, so distinct ones are left).
                    if (candidates.Count >= want * 4 && score <= floor) continue;
                    checkedCount++;
                    trial.rotation = rot;
                    var plan = Plan(t, map, a, trial, furniture, true);
                    // Only spots where all of it can be built: a room with gaps over water is for the player to place, never a suggestion.
                    if (!plan.Valid) continue;
                    score += plan.furniture.Count(x => x.state != PieceState.Blocked) * 2f;
                    score -= plan.mine.Count * 0.8f;
                    f.rock = Mathf.Max(f.rock, plan.mine.Count);
                    candidates.Add(new SpotCandidate
                    {
                        anchor = a, rot = rot, score = score, features = f, nearRoles = nearRoles, avoidRoles = avoidRoles,
                        mid = ToWorld(a, t.FullWidth / 2, t.FullHeight / 2, t.FullWidth, t.FullHeight, cfg.mirror, rot)
                    });
                    if (candidates.Count >= want * 4) floor = candidates.OrderByDescending(x => x.score).Skip(want * 4 - 1).First().score;
                }
                if (checkedCount > 400) break;
            }
            if (candidates.Count == 0) return result;
            float gap = Mathf.Max(t.FullWidth, t.FullHeight) * 0.9f;
            foreach (int i in SpotMath.Distinct(candidates.Select(x => x.score).ToList(), candidates.Select(x => (x.mid.x, x.mid.z)).ToList(), gap, want))
            {
                var s = candidates[i];
                s.reasons = SpotText.Reasons(s, map);
                result.Add(s);
            }
            return result;
        }

        private static bool Touches(RoomTemplateDef t, IntVec3 anchor, int rot, bool mirror, HashSet<IntVec3> taken)
        {
            int w = t.FullWidth, h = t.FullHeight;
            for (int fx = 0; fx < w; fx++)
                for (int fz = 0; fz < h; fz++)
                    if (taken.Contains(ToWorld(anchor, fx, fz, w, h, mirror, rot))) return true;
            return false;
        }

        private static void CopyInto(RoomPlanConfig from, RoomPlanConfig to)
        {
            to.style = from.style;
            to.theme = from.theme;
            to.mirror = from.mirror;
            to.doorX = from.doorX;
            to.wallStuff = from.wallStuff;
            to.doorDef = from.doorDef;
            to.doorStuff = from.doorStuff;
            to.floorChosen = from.floorChosen;
            to.floor = from.floor;
            to.floorStuff = from.floorStuff;
        }

        /// <summary>The middles of the colony's rooms with this role (by name, as the templates give it).</summary>
        private static List<IntVec3> RoleCenters(Map map, string roleName)
        {
            var list = new List<IntVec3>();
            var role = RoomUtil.RoleNamed(roleName);
            if (role == null) return list;
            foreach (var room in RoomUtil.ColonyRooms(map))
                if (room.Role == role) list.Add(room.ExtentsClose.CenterCell);
            return list;
        }

        private static float Nearest(IntVec3 from, List<IntVec3> cells)
        {
            if (cells == null || cells.Count == 0) return -1f;
            float best = float.MaxValue;
            foreach (var c in cells) best = Mathf.Min(best, from.DistanceTo(c));
            return best;
        }

        /// <summary>The spot's features, cheaply, without blueprint checks. False when the room can't stand here at all.</summary>
        private static bool Features(RoomTemplateDef t, Map map, IntVec3 anchor, int rot, bool mirror, IntVec3 center, List<List<IntVec3>> near,
            List<List<IntVec3>> avoid, List<List<IntVec3>> noisy, IntVec3 gate, List<IntVec3> fields, out SpotMath.Features f)
        {
            f = default;
            int w = t.FullWidth, h = t.FullHeight;
            var home = map.areaManager.Home;
            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, mirror, rot);
                    if (!cell.InBounds(map) || cell.Fogged(map)) return false;
                    bool isWall = fx == 0 || fz == 0 || fx == w - 1 || fz == h - 1;
                    var ed = cell.GetEdifice(map);
                    var terrain = cell.GetTerrain(map);
                    if (terrain.passability == Traversability.Impassable && ed == null) return false;
                    if (isWall)
                    {
                        if (ed != null)
                        {
                            if (IsWallLike(ed.def)) f.sharedWalls++;
                            else if (!ed.def.IsDoor) return false;
                        }
                    }
                    else
                    {
                        f.floorCells++;
                        if (ed != null)
                        {
                            if (ed.def.mineable) f.rock++;
                            else return false;
                        }
                        if (cell.GetZone(map) != null) f.zoneCells++;
                        if (home[cell]) f.homeCells++;
                        var roof = cell.GetRoof(map);
                        if (roof != null && roof.isThickRoof) f.thickRoof++;
                    }
                }
            }
            var mid = ToWorld(anchor, w / 2, h / 2, w, h, mirror, rot);
            f.distCenter = mid.DistanceTo(center);
            f.near = near.Select(l => Nearest(mid, l)).ToArray();
            f.avoid = avoid.Select(l => Nearest(mid, l)).ToArray();
            f.wantsQuiet = noisy != null;
            if (noisy != null) f.noisy = noisy.Select(l => Nearest(mid, l)).ToArray();
            f.wantsGate = gate.IsValid;
            f.distGate = gate.IsValid ? mid.DistanceTo(gate) : -1f;
            f.wantsFields = fields != null;
            f.distFields = fields != null ? Nearest(mid, fields) : -1f;
            return true;
        }
    }

    /// <summary>
    /// Which rooms a room plan goes with and away from: the template's own near and avoid, and the usual for its kind
    /// of room where the template says nothing (bedrooms want quiet, a hospital and an armory the gate, a barn the
    /// fields). The names are room roles; RoomUtil.RoleNamed finds them whatever the def is called.
    /// </summary>
    public static class RoomNeighbours
    {
        /// <summary>The rooms that are noisy to sleep beside.</summary>
        public static readonly string[] NoisyRoles = { "Workshop", "Barn" };

        public static List<string> Near(RoomTemplateDef t) => t?.near ?? new List<string>();

        public static List<string> Avoid(RoomTemplateDef t) => t?.avoid ?? new List<string>();

        public static bool Quiet(RoomTemplateDef t) => t != null && t.quiet;

        public static bool NearGate(RoomTemplateDef t) => t != null && t.nearGate;

        public static bool NearFields(RoomTemplateDef t) => t != null && t.nearFields;

        /// <summary>The gate: the busiest way in from the defense survey, else the way out of the colony toward the map's edge.</summary>
        public static IntVec3 GateCell(Map map)
        {
            try
            {
                var s = DefenseSurvey.Kept(map);
                if (s != null)
                {
                    if (s.chokes.Count > 0 && s.chokes[0].cell.IsValid) return s.chokes[0].cell;
                    if (s.approach.IsValid) return s.approach;
                }
            }
            catch
            {
                // The survey isn't there yet: the plain way out serves.
            }
            return PlaySpots.GateCell(map, null);
        }

        /// <summary>The middles of the colony's fields (growing zones), or null when it has none.</summary>
        public static List<IntVec3> FieldCells(Map map)
        {
            var list = new List<IntVec3>();
            foreach (var z in map.zoneManager.AllZones)
                if (z is Zone_Growing g && g.Cells.Count > 0)
                    list.Add(new IntVec3((int)g.Cells.Average(c => c.x), 0, (int)g.Cells.Average(c => c.z)));
            return list.Count > 0 ? list : null;
        }
    }

    /// <summary>The reasons for a spot, in words.</summary>
    public static class SpotText
    {
        private static string RoleLabel(string roleName)
        {
            var role = RoomUtil.RoleNamed(roleName);
            return role != null ? role.label : roleName.ToLowerInvariant();
        }

        public static List<(string text, bool good)> Reasons(TemplatePlanner.SpotCandidate s, Map map)
        {
            var list = new List<(string, bool)>();
            var f = s.features;
            for (int i = 0; i < s.nearRoles.Count && i < f.near.Length; i++)
            {
                int c = SpotMath.Closeness(f.near[i]);
                if (c == 2) list.Add(("CC_Spot_Beside".Translate(RoleLabel(s.nearRoles[i]).Named("ROOM")).Resolve(), true));
                else if (c == 1) list.Add(("CC_Spot_Near".Translate(RoleLabel(s.nearRoles[i]).Named("ROOM")).Resolve(), true));
                else if (c == 0) list.Add(("CC_Spot_FarFrom".Translate(RoleLabel(s.nearRoles[i]).Named("ROOM")).Resolve(), false));
            }
            for (int i = 0; i < s.avoidRoles.Count && i < f.avoid.Length; i++)
            {
                float d = f.avoid[i];
                if (d >= 0f && d < SpotMath.AvoidReach) list.Add(("CC_Spot_CloseTo".Translate(RoleLabel(s.avoidRoles[i]).Named("ROOM")).Resolve(), false));
                else if (d >= 0f) list.Add(("CC_Spot_AwayFrom".Translate(RoleLabel(s.avoidRoles[i]).Named("ROOM")).Resolve(), true));
            }
            if (f.wantsQuiet && f.noisy != null)
            {
                bool loud = f.noisy.Any(d => d >= 0f && d < SpotMath.QuietReach);
                bool any = f.noisy.Any(d => d >= 0f);
                if (loud) list.Add(("CC_Spot_Noisy".Translate().Resolve(), false));
                else if (any) list.Add(("CC_Spot_Quiet".Translate().Resolve(), true));
            }
            if (f.wantsGate && f.distGate >= 0f)
                list.Add((f.distGate < SpotMath.GateReach ? "CC_Spot_NearGate".Translate().Resolve() : "CC_Spot_FarGate".Translate().Resolve(), f.distGate < SpotMath.GateReach));
            if (f.wantsFields && f.distFields >= 0f)
                list.Add((f.distFields < SpotMath.FieldsReach ? "CC_Spot_ByFields".Translate().Resolve() : "CC_Spot_FarFields".Translate().Resolve(), f.distFields < SpotMath.FieldsReach));
            if (f.sharedWalls >= 3) list.Add(("CC_Spot_SharesWalls".Translate(f.sharedWalls).Resolve(), true));
            if (f.rock > 0) list.Add(("CC_Spot_Rock".Translate(f.rock).Resolve(), false));
            if (f.zoneCells > 0) list.Add(("CC_Spot_OverZone".Translate(f.zoneCells).Resolve(), false));
            list.Add(("CC_Spot_Steps".Translate(Mathf.RoundToInt(f.distCenter)).Resolve(), f.distCenter <= 20f));
            return list;
        }

        /// <summary>One line: the reasons that count most, joined.</summary>
        public static string Line(TemplatePlanner.SpotCandidate s, int most = 3) =>
            string.Join(", ", s.reasons.Take(most).Select(r => r.text)).CapitalizeFirst();
    }
}
