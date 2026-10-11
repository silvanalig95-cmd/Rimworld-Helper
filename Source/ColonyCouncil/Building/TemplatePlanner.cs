using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
        Blocked
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
        public IntVec3 door = IntVec3.Invalid;
        public PieceState doorState = PieceState.Ready;
        public bool doorExists;
        public string doorReason;

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

        public bool rockBlocked, ownBlocked, edgeBlocked, groundBlocked, otherBlocked;

        public bool Valid => wallBlocked.Count == 0 && interiorBlocked.Count == 0 && doorState != PieceState.Blocked;

        public string FailReason
        {
            get
            {
                var reasons = new List<string>();
                if (doorState == PieceState.Blocked && !doorReason.NullOrEmpty()) reasons.Add(doorReason);
                if (edgeBlocked) reasons.Add("CC_Plan_Block_Edge".Translate());
                if (rockBlocked) reasons.Add("CC_Plan_Block_Rock".Translate());
                if (ownBlocked) reasons.Add("CC_Plan_Block_Own".Translate());
                if (groundBlocked) reasons.Add("CC_Plan_Block_Ground".Translate());
                if (otherBlocked) reasons.Add("CC_Plan_Block_Other".Translate());
                if (reasons.Count == 0 && !Valid) reasons.Add("CC_Plan_Blocked".Translate());
                return reasons.Count == 0 ? null : string.Join("\n", reasons.Distinct());
            }
        }

        /// <summary>Everything the room still needs, by material, compared later with the colony's stock.</summary>
        public Dictionary<ThingDef, int> Cost()
        {
            var cost = new Dictionary<ThingDef, int>();
            void Add(BuildableDef def, ThingDef stuff, int times)
            {
                if (def == null || times <= 0) return;
                if (def.MadeFromStuff && stuff == null) return;
                foreach (var c in def.CostListAdjusted(stuff, false))
                {
                    cost.TryGetValue(c.thingDef, out int have);
                    cost[c.thingDef] = have + c.count * times;
                }
            }
            Add(ThingDefOf.Wall, wallStuff, wallNew.Count + wallWaiting.Count);
            if (!doorExists && doorState != PieceState.Blocked) Add(doorDef, doorStuff, 1);
            foreach (var f in furniture)
            {
                if (f.state != PieceState.Blocked) Add(f.def, f.stuff, 1);
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
            var placed = new List<(ResolvedThing f, CellRect rect, Rot4 rot)>();
            if (furniture != null && S.planFurniture)
            {
                foreach (var f in furniture)
                {
                    var r = f.rect;
                    int offset = f.inWall ? 0 : 1;
                    TemplateMath.TransformRect(r.minX + offset, r.minZ + offset, r.Width, r.Height, w, h, mirror, rot, out int mx, out int mz, out int sx, out int sz);
                    var worldRect = new CellRect(anchor.x + mx, anchor.z + mz, sx, sz);
                    placed.Add((f, worldRect, new Rot4(TemplateMath.TransformFacing(f.rot.AsInt, mirror, rot))));
                    foreach (var c in worldRect) furnitureCells.Add(c);
                }
            }

            int floorCount = 0;
            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, mirror, rot);
                    bool isWall = fx == 0 || fz == 0 || fx == w - 1 || fz == h - 1;
                    bool isDoor = fz == 0 && fx == doorFx;
                    bool isCorner = (fx == 0 || fx == w - 1) && (fz == 0 || fz == h - 1);
                    if (!cell.InBounds(map) || cell.Fogged(map))
                    {
                        plan.edgeBlocked = true;
                        if (isDoor) plan.doorState = PieceState.Blocked;
                        else if (isWall) plan.wallBlocked.Add(cell);
                        else plan.interiorBlocked.Add(cell);
                        continue;
                    }
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
                        if (reservedWall.Contains(((long)fx << 32) | (uint)fz)) continue;
                        if (ed != null && (TemplatePlanner.IsWallLike(ed.def) || ed.def.IsDoor))
                        {
                            plan.wallReuse.Add(cell);
                            if (smooth && !isCorner && ed.def.building?.smoothedThing != null && map.designationManager.DesignationAt(cell, DesignationDefOf.SmoothWall) == null)
                                plan.smoothWalls.Add(cell);
                        }
                        else if (HasWallBlueprint(cell, map)) plan.wallReuse.Add(cell);
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
                        else if (GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map, false, null, null, wallStuff).Accepted) plan.wallNew.Add(cell);
                        else
                        {
                            if (ed == null) plan.groundBlocked = true;
                            else plan.otherBlocked = true;
                            plan.wallBlocked.Add(cell);
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
                            plan.groundBlocked = true;
                            plan.interiorBlocked.Add(cell);
                        }
                        if (!quick)
                        {
                            var terrain = cell.GetTerrain(map);
                            if (plan.floor != null && terrain != plan.floor) floorCount++;
                            else if (plan.floor == null && smooth && terrain.smoothedTerrain != null && ed == null
                                     && map.designationManager.DesignationAt(cell, DesignationDefOf.SmoothFloor) == null)
                                plan.smoothFloors.Add(cell);
                        }
                    }
                }
            }
            plan.floorCells = floorCount;

            foreach (var (f, worldRect, newRot) in placed)
            {
                var root = FindRoot(worldRect, newRot, f.def.size);
                var pt = new PlannedThing { def = f.def, stuff = f.stuff, cell = root, rot = newRot };
                pt.state = FurnitureState(plan, map, f, worldRect, root, newRot, out pt.reason);
                plan.furniture.Add(pt);
            }

            if (!quick) AddWarnings(plan, map);
            return plan;
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
            if (GenConstruct.CanPlaceBlueprintAt(plan.doorDef, cell, Rot4.North, map, false, null, null, plan.doorStuff).Accepted)
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

        private static PieceState FurnitureState(PlacementPlan plan, Map map, ResolvedThing f, CellRect rect, IntVec3 root, Rot4 rot, out string reason)
        {
            reason = null;
            bool waiting = false;
            foreach (var c in rect)
            {
                if (!c.InBounds(map) || c.Fogged(map))
                {
                    reason = "CC_Plan_Block_Edge".Translate();
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
                reason = report.Reason;
                return PieceState.Blocked;
            }
            return PieceState.Ready;
        }

        private static void AddWarnings(PlacementPlan plan, Map map)
        {
            // Overhead mountain inside the room: insects love it.
            int thick = 0;
            foreach (var c in plan.interior)
            {
                var roof = c.GetRoof(map);
                if (roof != null && roof.isThickRoof) thick++;
            }
            if (thick > 0) plan.warnings.Add("CC_Plan_Warn_Mountain".Translate());
            if (plan.zoneCells.Count > 0) plan.warnings.Add("CC_Plan_Warn_Zone".Translate(plan.zoneCells.Count));
            if (plan.mine.Count > 0) plan.warnings.Add("CC_Plan_Warn_Mine".Translate(plan.mine.Count));
            if (plan.deconstruct.Count > 0) plan.warnings.Add("CC_Plan_Warn_Deconstruct".Translate(plan.deconstruct.Count));
            int waiting = plan.furniture.Count(f => f.state == PieceState.Waiting) + plan.wallWaiting.Count + (plan.doorState == PieceState.Waiting ? 1 : 0);
            if (waiting > 0) plan.warnings.Add("CC_Plan_Warn_Waiting".Translate(waiting));
            int blocked = plan.furniture.Count(f => f.state == PieceState.Blocked);
            if (blocked > 0) plan.warnings.Add("CC_Plan_Warn_FurnitureBlocked".Translate(blocked));
            if (plan.door.IsValid)
            {
                var outside = DoorOutside(plan, map);
                if (outside.IsValid && outside.InBounds(map))
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

        /// <summary>The root cell whose occupied rect (for this rotation and size) equals <paramref name="rect"/>.</summary>
        private static IntVec3 FindRoot(CellRect rect, Rot4 rot, IntVec2 size)
        {
            foreach (var c in rect)
            {
                var occ = GenAdj.OccupiedRect(c, rot, size);
                if (occ.minX == rect.minX && occ.minZ == rect.minZ && occ.Width == rect.Width && occ.Height == rect.Height) return c;
            }
            return rect.CenterCell;
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
            int walls = 0, furniture = 0, floors = 0, skipped = 0, queued = 0, cut = 0, mined = 0, removed = 0, smoothed = 0;
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
            foreach (var c in plan.wallWaiting)
            {
                queue?.Add(ThingDefOf.Wall, plan.wallStuff, c, Rot4.North, label);
                queued++;
            }
            if (!plan.doorExists)
            {
                if (plan.doorState == PieceState.Ready) TryPlace(plan.doorDef, plan.door, Rot4.North, plan.doorStuff, map);
                else if (plan.doorState == PieceState.Waiting)
                {
                    queue?.Add(plan.doorDef, plan.doorStuff, plan.door, Rot4.North, label);
                    queued++;
                }
            }
            foreach (var f in plan.furniture)
            {
                if (f.state == PieceState.Ready && TryPlace(f.def, f.cell, f.rot, f.stuff, map)) furniture++;
                else if (f.state == PieceState.Waiting)
                {
                    queue?.Add(f.def, f.stuff, f.cell, f.rot, label);
                    queued++;
                }
                else skipped++;
            }
            if (plan.floor != null)
            {
                foreach (var c in plan.interior)
                {
                    if (c.GetTerrain(map) == plan.floor) continue;
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

            string s = "CC_Plan_Done".Translate(plan.template.LabelCap.ToString(), walls, furniture, plan.wallStuff?.label ?? "-");
            if (mined > 0) s += " " + "CC_Plan_Mined".Translate(mined);
            if (cut > 0) s += " " + "CC_Plan_Cut".Translate(cut);
            if (removed > 0) s += " " + "CC_Plan_Removed".Translate(removed);
            if (floors > 0) s += " " + "CC_Plan_Floors".Translate(floors, plan.floor.label);
            if (smoothed > 0) s += " " + "CC_Plan_Smoothed".Translate(smoothed);
            if (queued > 0) s += " " + "CC_Plan_Queued".Translate(queued);
            if (skipped > 0) s += " " + "CC_Plan_Skipped".Translate(skipped);
            return s;
        }

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

        /// <summary>
        /// Searches around the colony for a good spot: buildable, close to home, sharing existing walls
        /// (cheaper and warmer), near the rooms it goes well with, avoiding zones and long mining jobs.
        /// Tries every rotation; keeps the player's mirror setting.
        /// </summary>
        public static bool FindSpot(RoomTemplateDef t, Map map, List<ResolvedThing> furniture, RoomPlanConfig cfg, out IntVec3 anchor, out int bestRot)
        {
            anchor = IntVec3.Invalid;
            bestRot = cfg.rotation;
            var center = ColonyCenter(map);
            var nearCenters = RoleCenters(map, t.near);
            var avoidCenters = RoleCenters(map, t.avoid);
            float best = float.MinValue;
            int checkedCount = 0;
            var trial = new RoomPlanConfig();
            CopyInto(cfg, trial);
            foreach (var c in GenRadial.RadialCellsAround(center, 40f, true))
            {
                if (((c.x + c.z) & 1) != 0) continue;
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                for (int rot = 0; rot < 4; rot++)
                {
                    var a = AnchorForCenter(t, c, rot);
                    float score = QuickScore(t, map, a, rot, cfg.mirror, center, nearCenters, avoidCenters);
                    if (score <= best) continue;
                    checkedCount++;
                    trial.rotation = rot;
                    var plan = Plan(t, map, a, trial, furniture, true);
                    if (!plan.Valid) continue;
                    score += plan.furniture.Count(f => f.state != PieceState.Blocked) * 2f;
                    score -= plan.mine.Count * 0.8f;
                    if (score > best)
                    {
                        best = score;
                        anchor = a;
                        bestRot = rot;
                    }
                }
                if (checkedCount > 400) break;
            }
            return anchor.IsValid;
        }

        private static void CopyInto(RoomPlanConfig from, RoomPlanConfig to)
        {
            to.mirror = from.mirror;
            to.doorX = from.doorX;
            to.wallStuff = from.wallStuff;
            to.doorDef = from.doorDef;
            to.doorStuff = from.doorStuff;
            to.floorChosen = from.floorChosen;
            to.floor = from.floor;
            to.floorStuff = from.floorStuff;
        }

        private static List<IntVec3> RoleCenters(Map map, List<string> roles)
        {
            var list = new List<IntVec3>();
            if (roles == null || roles.Count == 0) return list;
            foreach (var name in roles)
            {
                var role = DefDatabase<RoomRoleDef>.GetNamedSilentFail(name);
                if (role == null) continue;
                foreach (var room in RoomUtil.ColonyRooms(map))
                {
                    if (room.Role == role) list.Add(room.Cells.First());
                }
            }
            return list;
        }

        /// <summary>Cheap pre-score without blueprint checks. float.MinValue = impossible.</summary>
        private static float QuickScore(RoomTemplateDef t, Map map, IntVec3 anchor, int rot, bool mirror, IntVec3 center, List<IntVec3> near, List<IntVec3> avoid)
        {
            int w = t.FullWidth, h = t.FullHeight;
            float score = 0f;
            var home = map.areaManager.Home;
            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, mirror, rot);
                    if (!cell.InBounds(map) || cell.Fogged(map)) return float.MinValue;
                    bool isWall = fx == 0 || fz == 0 || fx == w - 1 || fz == h - 1;
                    var ed = cell.GetEdifice(map);
                    var terrain = cell.GetTerrain(map);
                    if (terrain.passability == Traversability.Impassable && ed == null) return float.MinValue;
                    if (isWall)
                    {
                        if (ed != null)
                        {
                            if (IsWallLike(ed.def)) score += 3f;
                            else if (!ed.def.IsDoor) return float.MinValue;
                        }
                    }
                    else
                    {
                        if (ed != null)
                        {
                            if (ed.def.mineable) score -= 1.5f;
                            else return float.MinValue;
                        }
                        if (cell.GetZone(map) != null) score -= 1f;
                        if (home[cell]) score += 0.3f;
                        var roof = cell.GetRoof(map);
                        if (roof != null && roof.isThickRoof) score -= 0.5f;
                    }
                }
            }
            var mid = ToWorld(anchor, w / 2, h / 2, w, h, mirror, rot);
            score -= mid.DistanceTo(center) * 0.6f;
            foreach (var n in near)
            {
                float d = mid.DistanceTo(n);
                if (d < 14f) score += (14f - d) * 0.8f;
            }
            foreach (var n in avoid)
            {
                float d = mid.DistanceTo(n);
                if (d < 12f) score -= (12f - d) * 0.8f;
            }
            return score;
        }
    }
}
