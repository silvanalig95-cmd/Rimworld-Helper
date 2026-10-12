using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>One building a camp means to put up: what, of what, where, which way round.</summary>
    public class CampPlan : IExposable
    {
        public string role;
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rot = Rot4.North;
        public bool placed;
        /// <summary>Given up on (blocked, or nobody could build it in time).</summary>
        public bool dropped;

        public CellRect Rect => GenAdj.OccupiedRect(cell, rot, def?.size ?? IntVec2.One);

        /// <summary>The finished building, if it stands (and is still theirs).</summary>
        public Building Built(Map map, Faction f)
        {
            if (map == null || def == null || f == null || !cell.InBounds(map)) return null;
            foreach (var t in cell.GetThingList(map))
                if (t is Building b && b.def == def && b.Faction == f) return b;
            return null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref role, "role");
            // Some of these come from a DLC (a meditation spot): one removed since loads as null, without errors.
            CouncilScribe.LookDef(ref def, "def");
            CouncilScribe.LookDef(ref stuff, "stuff");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rot, "rot", Rot4.North);
            Scribe_Values.Look(ref placed, "placed");
            Scribe_Values.Look(ref dropped, "dropped");
        }
    }

    /// <summary>
    /// Where a camp can go: open, reachable ground well away from the colony (no home area, zones or buildings of the
    /// colony nearby), close enough that the two share the land. And how the camp is laid out around its fire.
    /// </summary>
    public static class CampSite
    {
        /// <summary>How far the camp itself reaches from its fire.</summary>
        public const int CoreRadius = 7;
        /// <summary>Nearest a camp may be to the home area or any of the colony's buildings.</summary>
        public const int MinFromColony = 28;
        public const int MaxFromColony = 80;
        /// <summary>How far they go for wood and game.</summary>
        public const float WorkRadius = 26f;
        /// <summary>Nothing they take may lie this close to the home area.</summary>
        public const int HomeMargin = 8;

        /// <summary>Cells of the colony: its buildings and (thinned out) its home area.</summary>
        public static List<IntVec3> ColonyPoints(Map map)
        {
            var list = new List<IntVec3>();
            foreach (var b in map.listerBuildings.allBuildingsColonist) list.Add(b.Position);
            var home = map.areaManager.Home;
            if (home != null)
                foreach (var c in home.ActiveCells)
                    if ((c.x + c.z) % 3 == 0) list.Add(c);
            return list;
        }

        /// <summary>The middle of the colony: the average of its buildings, or of its home area.</summary>
        public static IntVec3 ColonyCenter(Map map)
        {
            var pts = map.listerBuildings.allBuildingsColonist.Select(b => b.Position).ToList();
            if (pts.Count == 0 && map.areaManager.Home != null) pts = map.areaManager.Home.ActiveCells.ToList();
            if (pts.Count == 0) return map.Center;
            return new IntVec3(Mathf.RoundToInt((float)pts.Average(p => p.x)), 0, Mathf.RoundToInt((float)pts.Average(p => p.z)));
        }

        private static int MinDistSquared(IntVec3 c, List<IntVec3> pts)
        {
            int best = int.MaxValue;
            foreach (var p in pts)
            {
                int dx = p.x - c.x, dz = p.z - c.z;
                int d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Open ground for the camp: standable, unroofed, outdoors, dry, no zone, no building, no home area.</summary>
        public static bool OpenGround(IntVec3 c, Map map)
        {
            if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.Roofed(map)) return false;
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.IsWater || terrain.passability == Traversability.Impassable) return false;
            if (map.zoneManager.ZoneAt(c) != null) return false;
            if (map.areaManager.Home != null && map.areaManager.Home[c]) return false;
            if (c.GetFirstBuilding(map) != null) return false;
            return !Marked(c, map);
        }

        /// <summary>The colony has plans for this spot: a plan drawn on it, or work marked on the ground.</summary>
        public static bool Marked(IntVec3 c, Map map) => map.planManager?.PlanAt(c) != null || map.designationManager.HasMapDesignationAt(c);

        public static bool TryFind(Map map, out IntVec3 center)
        {
            center = IntVec3.Invalid;
            if (map == null) return false;
            var pts = ColonyPoints(map);
            int margin = CoreRadius + 8;
            if (map.Size.x < margin * 2 + 20 || map.Size.z < margin * 2 + 20) return false;
            var traverse = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            float bestScore = float.MinValue;
            int minSq = MinFromColony * MinFromColony, maxSq = MaxFromColony * MaxFromColony;
            for (int i = 0; i < 260; i++)
            {
                var c = new IntVec3(Rand.RangeInclusive(margin, map.Size.x - margin - 1), 0, Rand.RangeInclusive(margin, map.Size.z - margin - 1));
                if (!OpenGround(c, map)) continue;
                int d = pts.Count > 0 ? MinDistSquared(c, pts) : int.MaxValue;
                if (d < minSq || (pts.Count > 0 && d > maxSq)) continue;
                // Room for the camp: nearly all of it open ground.
                int open = 0, total = 0;
                foreach (var x in GenRadial.RadialCellsAround(c, CoreRadius, true))
                {
                    total++;
                    if (OpenGround(x, map)) open++;
                    else if (x.InBounds(map) && (map.zoneManager.ZoneAt(x) != null || (map.areaManager.Home != null && map.areaManager.Home[x])))
                    {
                        open = -999;
                        break;
                    }
                }
                if (open < total * 0.8f) continue;
                if (!map.reachability.CanReachMapEdge(c, traverse) || !map.reachability.CanReachColony(c)) continue;
                // Close enough to share the land, with trees about; a little chance for variety.
                float dist = pts.Count > 0 ? Mathf.Sqrt(d) : 50f;
                int trees = 0;
                foreach (var x in GenRadial.RadialCellsAround(c, 14f, false))
                    if (x.InBounds(map) && x.GetPlant(map) is Plant p && p.def.plant.IsTree) trees++;
                float score = -Mathf.Abs(dist - 42f) * 0.6f + Mathf.Min(trees, 30) * 0.3f + Rand.Range(0f, 6f);
                if (score > bestScore)
                {
                    bestScore = score;
                    center = c;
                }
            }
            return center.IsValid;
        }

        private static readonly Dictionary<int, (int day, bool ok)> cache = new Dictionary<int, (int, bool)>();

        /// <summary>Whether a camp could go anywhere on this map (worked out once a day).</summary>
        public static bool AnyRoom(Map map)
        {
            int day = GenDate.DaysPassed;
            if (cache.TryGetValue(map.uniqueID, out var c) && c.day == day) return c.ok;
            bool ok = TryFind(map, out _);
            cache[map.uniqueID] = (day, ok);
            return ok;
        }

        /// <summary>A unit step from the colony toward the camp (which way is "away").</summary>
        public static IntVec3 Away(IntVec3 center, IntVec3 colony)
        {
            var v = new Vector3(center.x - colony.x, 0f, center.z - colony.z);
            if (v.sqrMagnitude < 0.01f) return IntVec3.North;
            v.Normalize();
            return Mathf.Abs(v.x) > Mathf.Abs(v.z) ? new IntVec3(v.x > 0 ? 1 : -1, 0, 0) : new IntVec3(0, 0, v.z > 0 ? 1 : -1);
        }

        /// <summary>Which way the camp lies from the colony, in words.</summary>
        public static string Direction(IntVec3 center, IntVec3 colony)
        {
            float dx = center.x - colony.x, dz = center.z - colony.z;
            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            string[] keys = { "CC_Camp_Dir_E", "CC_Camp_Dir_NE", "CC_Camp_Dir_N", "CC_Camp_Dir_NW", "CC_Camp_Dir_W", "CC_Camp_Dir_SW", "CC_Camp_Dir_S", "CC_Camp_Dir_SE" };
            int i = Mathf.RoundToInt(angle / 45f) % 8;
            return keys[i].Translate();
        }

        // ---------------- Layout ----------------

        private static bool Fits(ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot, Map map, HashSet<IntVec3> taken)
        {
            var rect = GenAdj.OccupiedRect(cell, rot, def.size);
            // Nothing lying there either: nobody's things get moved aside to make room.
            foreach (var c in rect)
                if (!OpenGround(c, map) || taken.Contains(c) || c.GetFirstItem(map) != null) return false;
            try
            {
                return GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map, false, null, null, stuff).Accepted;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp placement " + def.defName, e);
                return false;
            }
        }

        private static void Take(CampPlan p, HashSet<IntVec3> taken)
        {
            foreach (var c in p.Rect) taken.Add(c);
        }

        private static CampPlan Plan(CampStructure s, ThingDef def, IntVec3 cell, Rot4 rot) =>
            new CampPlan { role = s.role, def = def, stuff = s.StuffFor(def), cell = cell, rot = rot };

        /// <summary>
        /// The camp around its fire: beds in a ring, a windbreak or a hut on the far side, a stall toward the colony,
        /// the stash on the far side. Null when not even the fire fits.
        /// </summary>
        public static List<CampPlan> Layout(Map map, IntVec3 center, CampKindDef kind, int people, IntVec3 colony, out IntVec3 stash, out IntVec3 share)
        {
            var away = Away(center, colony);
            var side = new IntVec3(away.z, 0, -away.x);
            stash = center + away * 4 + side * 3;
            share = center - away * 7;
            if (!stash.InBounds(map) || !stash.Standable(map)) stash = center + away * 3;
            if (!share.InBounds(map) || !share.Standable(map)) share = center - away * 5;
            var plans = new List<CampPlan>();
            var taken = new HashSet<IntVec3>();
            // The stash and the way to it stay clear.
            foreach (var c in GenRadial.RadialCellsAround(stash, 1.5f, true)) taken.Add(c);

            var fireS = kind.structures.FirstOrDefault(s => s.role == "fire");
            var fireDef = fireS?.Def;
            if (fireDef == null) return null;
            CampPlan fire = null;
            foreach (var c in GenRadial.RadialCellsAround(center, 2f, true))
            {
                if (!Fits(fireDef, fireS.StuffFor(fireDef), c, Rot4.North, map, taken)) continue;
                fire = Plan(fireS, fireDef, c, Rot4.North);
                break;
            }
            if (fire == null) return null;
            plans.Add(fire);
            Take(fire, taken);
            // Room around the fire to sit and cook.
            foreach (var c in GenRadial.RadialCellsAround(fire.cell, 1.5f, false)) taken.Add(c);
            var fireCell = fire.cell;

            foreach (var s in kind.structures)
            {
                if (s == fireS) continue;
                var def = s.Def;
                if (def == null) continue;
                try
                {
                    switch (s.role)
                    {
                        case "bed":
                            // One each (a hut's bed counts).
                            int beds = (s.count > 0 ? Mathf.Min(s.count, people) : people) - plans.Count(p => p.role == "hutbed");
                            if (beds > 0) PlaceRing(plans, taken, s, def, map, fireCell, 2.5f, 5.5f, beds, true);
                            break;
                        case "wall":
                            PlaceWall(plans, taken, s, def, map, fireCell, away, side);
                            break;
                        case "hut":
                            PlaceHut(plans, taken, s, def, map, fireCell, away, side, kind);
                            break;
                        case "stall":
                            PlaceRing(plans, taken, s, def, map, fireCell - away * 2, 2f, 4.5f, Mathf.Max(1, s.count), false);
                            break;
                        default:
                            PlaceRing(plans, taken, s, def, map, fireCell, 3.5f, 6.5f, Mathf.Max(1, s.count), false);
                            break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp layout " + s.role, e);
                }
            }
            return plans;
        }

        private static void PlaceRing(List<CampPlan> plans, HashSet<IntVec3> taken, CampStructure s, ThingDef def, Map map, IntVec3 around, float min, float max, int count, bool faceIn)
        {
            int placed = 0;
            var cells = GenRadial.RadialCellsAround(around, max, true).Where(c => c.DistanceTo(around) >= min).InRandomOrder().ToList();
            foreach (var c in cells)
            {
                if (placed >= count) break;
                var rots = def.rotatable ? new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West }.InRandomOrder() : new[] { Rot4.North };
                foreach (var rot in rots)
                {
                    if (!Fits(def, s.StuffFor(def), c, rot, map, taken)) continue;
                    var p = Plan(s, def, c, rot);
                    plans.Add(p);
                    Take(p, taken);
                    // A walking gap around beds and stalls.
                    foreach (var x in p.Rect.ExpandedBy(1)) if (!taken.Contains(x) && Rand.Chance(faceIn ? 0.5f : 0.35f)) taken.Add(x);
                    placed++;
                    break;
                }
            }
        }

        /// <summary>A short windbreak on the far side of the fire.</summary>
        private static void PlaceWall(List<CampPlan> plans, HashSet<IntVec3> taken, CampStructure s, ThingDef def, Map map, IntVec3 fire, IntVec3 away, IntVec3 side)
        {
            int n = Mathf.Clamp(s.count, 2, 7);
            var root = fire + away * 6;
            var line = new List<IntVec3>();
            for (int i = 0; i < n; i++) line.Add(root + side * (i - n / 2));
            var stuff = s.StuffFor(def);
            var fitting = line.Where(c => Fits(def, stuff, c, Rot4.North, map, taken)).ToList();
            if (fitting.Count < 2) return;
            foreach (var c in fitting)
            {
                var p = Plan(s, def, c, Rot4.North);
                plans.Add(p);
                Take(p, taken);
            }
        }

        /// <summary>A hut: a ring of wall around a 3x3 floor with one bed, a door facing the fire, and (once built) a roof.</summary>
        private static void PlaceHut(List<CampPlan> plans, HashSet<IntVec3> taken, CampStructure s, ThingDef wallDef, Map map, IntVec3 fire, IntVec3 away, IntVec3 side, CampKindDef kind)
        {
            var mid = fire + away * 5;
            var rect = CellRect.CenteredOn(mid, 2);
            var stuff = s.StuffFor(wallDef);
            var doorDef = ThingDefOf.Door;
            var doorCell = mid - away * 2;
            foreach (var c in rect)
                if (!OpenGround(c, map) || taken.Contains(c)) return;
            var hut = new List<CampPlan>();
            foreach (var c in rect.EdgeCells)
            {
                if (c == doorCell)
                {
                    if (!Fits(doorDef, stuff, c, Rot4.North, map, taken)) return;
                    hut.Add(new CampPlan { role = "door", def = doorDef, stuff = stuff, cell = c, rot = Rot4.North });
                    continue;
                }
                if (!Fits(wallDef, stuff, c, Rot4.North, map, taken)) return;
                hut.Add(new CampPlan { role = "hut", def = wallDef, stuff = stuff, cell = c, rot = Rot4.North });
            }
            foreach (var p in hut)
            {
                plans.Add(p);
                Take(p, taken);
            }
            // The bed goes inside.
            var bedS = kind.structures.FirstOrDefault(x => x.role == "bed");
            var bedDef = bedS?.Def;
            if (bedDef != null)
            {
                foreach (var rot in new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West })
                {
                    var c = mid;
                    var bedRect = GenAdj.OccupiedRect(c, rot, bedDef.size);
                    if (!bedRect.All(x => rect.ContractedBy(1).Contains(x)) || !Fits(bedDef, bedS.StuffFor(bedDef), c, rot, map, taken)) continue;
                    var p = Plan(bedS, bedDef, c, rot);
                    p.role = "hutbed";
                    plans.Add(p);
                    Take(p, taken);
                    break;
                }
            }
            foreach (var c in rect) taken.Add(c);
        }

        /// <summary>The cells under a hut's roof (inside its walls), if it has one.</summary>
        public static IEnumerable<IntVec3> HutInside(List<CampPlan> plans)
        {
            var walls = plans.Where(p => p.role == "hut" || p.role == "door").Select(p => p.cell).ToList();
            if (walls.Count < 8) yield break;
            int minX = walls.Min(c => c.x), maxX = walls.Max(c => c.x), minZ = walls.Min(c => c.z), maxZ = walls.Max(c => c.z);
            for (int x = minX + 1; x < maxX; x++)
                for (int z = minZ + 1; z < maxZ; z++)
                    yield return new IntVec3(x, 0, z);
        }
    }
}
