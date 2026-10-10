using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public class PlannedThing
    {
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rot;
        public bool blocked;
    }

    /// <summary>Where every wall, door and piece of furniture of a template would go, and what's in the way.</summary>
    public class PlacementPlan
    {
        public RoomTemplateDef template;
        public int rotation;
        public IntVec3 anchor;
        public ThingDef wallStuff;
        public ThingDef doorStuff;
        public TerrainDef floor;

        public List<IntVec3> wallNew = new List<IntVec3>();
        public List<IntVec3> wallReuse = new List<IntVec3>();
        public List<IntVec3> wallBlocked = new List<IntVec3>();
        public List<IntVec3> interior = new List<IntVec3>();
        public List<IntVec3> interiorRock = new List<IntVec3>();
        public List<IntVec3> interiorBlocked = new List<IntVec3>();
        public IntVec3 door;
        public bool doorNew;
        public bool doorOk = true;
        public List<PlannedThing> furniture = new List<PlannedThing>();

        public bool Valid => wallBlocked.Count == 0 && interiorBlocked.Count == 0 && doorOk;

        public string FailReason
        {
            get
            {
                if (!doorOk) return "CC_Plan_DoorBlocked".Translate();
                if (wallBlocked.Count > 0 || interiorBlocked.Count > 0) return "CC_Plan_Blocked".Translate();
                return null;
            }
        }
    }

    public static class TemplatePlanner
    {
        /// <summary>Anchor (bottom-left of the rotated footprint) so that <paramref name="center"/> sits in the middle.</summary>
        public static IntVec3 AnchorForCenter(RoomTemplateDef t, IntVec3 center, int rot)
        {
            TemplateMath.RotatedSize(t.FullWidth, t.FullHeight, rot, out int rw, out int rh);
            return new IntVec3(center.x - rw / 2, 0, center.z - rh / 2);
        }

        private static IntVec3 ToWorld(IntVec3 anchor, int fx, int fz, int w, int h, int rot)
        {
            TemplateMath.RotatePoint(fx, fz, w, h, rot, out int rx, out int rz);
            return new IntVec3(anchor.x + rx, 0, anchor.z + rz);
        }

        public static PlacementPlan Plan(RoomTemplateDef t, Map map, IntVec3 anchor, int rot, List<ResolvedThing> furniture, ThingDef wallStuff, TerrainDef floor)
        {
            var plan = new PlacementPlan
            {
                template = t,
                rotation = TemplateMath.Norm(rot),
                anchor = anchor,
                wallStuff = wallStuff,
                doorStuff = ThingDefOf.Door.MadeFromStuff && wallStuff != null && GenStuff.AllowedStuffsFor(ThingDefOf.Door).Contains(wallStuff)
                    ? wallStuff
                    : Avail.BestStuff(ThingDefOf.Door, map),
                floor = floor
            };
            int w = t.FullWidth, h = t.FullHeight;
            int doorFx = t.DoorX + 1;
            var reservedWall = new HashSet<long>();
            if (furniture != null)
            {
                foreach (var f in furniture)
                {
                    if (!f.inWall) continue;
                    foreach (var c in f.rect) reservedWall.Add(((long)c.x << 32) | (uint)c.z);
                }
            }

            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, rot);
                    bool isWall = fx == 0 || fz == 0 || fx == w - 1 || fz == h - 1;
                    bool isDoor = fz == 0 && fx == doorFx;
                    if (!cell.InBounds(map) || cell.Fogged(map))
                    {
                        if (isDoor) plan.doorOk = false;
                        else if (isWall) plan.wallBlocked.Add(cell);
                        else plan.interiorBlocked.Add(cell);
                        continue;
                    }
                    if (isDoor)
                    {
                        plan.door = cell;
                        var ed = cell.GetEdifice(map);
                        if (ed != null && ed.def.IsDoor) plan.doorNew = false;
                        else if (HasBlueprintOf(cell, map, ThingDefOf.Door)) plan.doorNew = false;
                        else if (GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Door, cell, Rot4.North, map, false, null, null, plan.doorStuff).Accepted) plan.doorNew = true;
                        else plan.doorOk = false;
                    }
                    else if (isWall)
                    {
                        if (reservedWall.Contains(((long)fx << 32) | (uint)fz)) continue;
                        var ed = cell.GetEdifice(map);
                        if (ed != null && (IsWallLike(ed.def) || ed.def.IsDoor)) plan.wallReuse.Add(cell);
                        else if (HasBlueprintOf(cell, map, ThingDefOf.Wall)) plan.wallReuse.Add(cell);
                        else if (GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map, false, null, null, wallStuff).Accepted) plan.wallNew.Add(cell);
                        else plan.wallBlocked.Add(cell);
                    }
                    else
                    {
                        plan.interior.Add(cell);
                        var ed = cell.GetEdifice(map);
                        if (ed != null)
                        {
                            if (ed.def.building != null && ed.def.building.isNaturalRock) plan.interiorRock.Add(cell);
                            else plan.interiorBlocked.Add(cell);
                        }
                        else if (cell.GetTerrain(map).passability == Traversability.Impassable)
                        {
                            plan.interiorBlocked.Add(cell);
                        }
                    }
                }
            }

            if (furniture != null)
            {
                foreach (var f in furniture)
                {
                    var r = f.rect;
                    int offset = f.inWall ? 0 : 1;
                    TemplateMath.RotateRect(r.minX + offset, r.minZ + offset, r.Width, r.Height, w, h, rot, out int mx, out int mz, out int sx, out int sz);
                    var worldRect = new CellRect(anchor.x + mx, anchor.z + mz, sx, sz);
                    var newRot = new Rot4(TemplateMath.RotateFacing(f.rot.AsInt, rot));
                    var root = FindRoot(worldRect, newRot, f.def.size);
                    var pt = new PlannedThing { def = f.def, stuff = f.stuff, cell = root, rot = newRot };
                    pt.blocked = !root.InBounds(map) || !CellsFree(worldRect, map)
                                 || !GenConstruct.CanPlaceBlueprintAt(f.def, root, newRot, map, false, null, null, f.stuff).Accepted;
                    plan.furniture.Add(pt);
                }
            }
            return plan;
        }

        private static bool CellsFree(CellRect rect, Map map)
        {
            foreach (var c in rect)
            {
                if (!c.InBounds(map)) return false;
                var ed = c.GetEdifice(map);
                if (ed != null) return false;
            }
            return true;
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
            if (d.building.isNaturalRock) return true;
            return d.IsWall || (d.passability == Traversability.Impassable && d.fillPercent >= 0.99f && d.holdsRoof);
        }

        private static bool HasBlueprintOf(IntVec3 c, Map map, ThingDef def)
        {
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild == def) return true;
            }
            return false;
        }

        /// <summary>Places all the blueprints and mining designations. Returns a summary for the player.</summary>
        public static string Execute(PlacementPlan plan, Map map)
        {
            int walls = 0, furniture = 0, mined = 0, floors = 0, skipped = 0;
            foreach (var c in plan.wallNew)
            {
                if (GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, c, Rot4.North, map, false, null, null, plan.wallStuff).Accepted)
                {
                    GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, c, map, Rot4.North, Faction.OfPlayer, plan.wallStuff);
                    walls++;
                }
            }
            if (plan.doorNew && GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Door, plan.door, Rot4.North, map, false, null, null, plan.doorStuff).Accepted)
                GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Door, plan.door, map, Rot4.North, Faction.OfPlayer, plan.doorStuff);

            foreach (var c in plan.interiorRock)
            {
                if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) == null)
                {
                    map.designationManager.AddDesignation(new Designation(c, DesignationDefOf.Mine));
                    mined++;
                }
            }
            foreach (var f in plan.furniture)
            {
                if (!f.blocked && GenConstruct.CanPlaceBlueprintAt(f.def, f.cell, f.rot, map, false, null, null, f.stuff).Accepted)
                {
                    GenConstruct.PlaceBlueprintForBuild(f.def, f.cell, map, f.rot, Faction.OfPlayer, f.stuff);
                    furniture++;
                }
                else
                {
                    skipped++;
                }
            }
            if (plan.floor != null)
            {
                var floorStuff = plan.floor.MadeFromStuff ? Avail.BestStuff(plan.floor, map) : null;
                foreach (var c in plan.interior)
                {
                    if (plan.interiorRock.Contains(c)) continue;
                    if (c.GetTerrain(map) == plan.floor) continue;
                    if (GenConstruct.CanPlaceBlueprintAt(plan.floor, c, Rot4.North, map, false, null, null, floorStuff).Accepted)
                    {
                        GenConstruct.PlaceBlueprintForBuild(plan.floor, c, map, Rot4.North, Faction.OfPlayer, floorStuff);
                        floors++;
                    }
                }
            }
            string s = "CC_Plan_Done".Translate(plan.template.LabelCap.ToString(), walls, furniture, plan.wallStuff?.label ?? "-");
            if (mined > 0) s += " " + "CC_Plan_Mined".Translate(mined);
            if (floors > 0) s += " " + "CC_Plan_Floors".Translate(floors, plan.floor.label);
            if (skipped > 0) s += " " + "CC_Plan_Skipped".Translate(skipped);
            return s;
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
        /// (cheaper and warmer), avoiding zones and long mining jobs.
        /// </summary>
        public static bool FindSpot(RoomTemplateDef t, Map map, List<ResolvedThing> furniture, ThingDef wallStuff, out IntVec3 anchor, out int bestRot)
        {
            anchor = IntVec3.Invalid;
            bestRot = 0;
            var center = ColonyCenter(map);
            float best = float.MinValue;
            int w = t.FullWidth, h = t.FullHeight;
            int checkedCount = 0;
            foreach (var c in GenRadial.RadialCellsAround(center, 40f, true))
            {
                if (((c.x + c.z) & 1) != 0) continue;
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                for (int rot = 0; rot < 4; rot++)
                {
                    var a = AnchorForCenter(t, c, rot);
                    float score = QuickScore(t, map, a, rot, center);
                    if (score <= best) continue;
                    checkedCount++;
                    var plan = Plan(t, map, a, rot, furniture, wallStuff, null);
                    if (!plan.Valid) continue;
                    score += plan.furniture.Count(f => !f.blocked) * 2f;
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

        /// <summary>Cheap pre-score without blueprint checks. float.MinValue = impossible.</summary>
        private static float QuickScore(RoomTemplateDef t, Map map, IntVec3 anchor, int rot, IntVec3 center)
        {
            int w = t.FullWidth, h = t.FullHeight;
            float score = 0f;
            var home = map.areaManager.Home;
            for (int fx = 0; fx < w; fx++)
            {
                for (int fz = 0; fz < h; fz++)
                {
                    var cell = ToWorld(anchor, fx, fz, w, h, rot);
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
                            if (ed.def.building != null && ed.def.building.isNaturalRock) score -= 1.5f;
                            else return float.MinValue;
                        }
                        if (cell.GetZone(map) != null) score -= 1f;
                        if (home[cell]) score += 0.3f;
                    }
                }
            }
            var mid = ToWorld(anchor, w / 2, h / 2, w, h, rot);
            score -= mid.DistanceTo(center) * 0.6f;
            return score;
        }
    }
}
