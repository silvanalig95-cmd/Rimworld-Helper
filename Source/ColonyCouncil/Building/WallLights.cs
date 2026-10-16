using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Lights that hang on walls (1.6 wall attachments), for the room planner, the advisor, wishes and personal touches.
    /// The rules themselves (which light for which room, where lamps hang, how many) are pure, in <see cref="LightMath"/>;
    /// this reads the game: what the colony can build, which walls take a lamp, which spots are free.
    /// </summary>
    public static class WallLights
    {
        /// <summary>
        /// Hung on a wall: <c>building.isAttachment</c>, or a placeworker that attaches it to a wall (vanilla's, or a
        /// mod's own, like Vanilla Expanded's multi-cell one).
        /// </summary>
        public static bool IsAttached(ThingDef d)
        {
            if (d?.building == null) return false;
            if (d.building.isAttachment) return true;
            var workers = d.placeWorkers;
            if (workers == null) return false;
            foreach (var t in workers)
                if (t != null && t.Name.IndexOf("AttachedToWall", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// A light that hangs on a wall and lights a room: a single cell that can face any wall, with a glower strong
        /// enough to count as a light (<see cref="Avail.IsLight"/>). Wall heaters that happen to glow are heaters.
        /// </summary>
        public static bool IsWallLight(ThingDef d) =>
            d != null && Avail.IsLight(d) && IsAttached(d) && d.size.x == 1 && d.size.z == 1 && d.rotatable
            && !(d.thingClass != null && typeof(Building_Heater).IsAssignableFrom(d.thingClass));

        /// <summary>A light that stands on the floor (a standing lamp, a torch, a brazier).</summary>
        public static bool IsFloorLight(ThingDef d) => d != null && Avail.IsLight(d) && !IsAttached(d);

        public static float GlowRadius(ThingDef d) => d?.GetCompProperties<CompProperties_Glower>()?.glowRadius ?? 0f;

        /// <summary>How far the lamp lights the floor well (<see cref="LightMath.Reach"/>).</summary>
        public static float Reach(ThingDef d) => LightMath.Reach(GlowRadius(d));

        /// <summary>A wall that takes attachments (never a door).</summary>
        public static bool Supports(ThingDef d) => d?.building != null && d.building.supportsWallAttachments && !d.IsDoor;

        /// <summary>The plan's own walls take wall lamps (they're built of the plain wall).</summary>
        public static bool PlannedWallsHoldLamps => Supports(ThingDefOf.Wall);

        /// <summary>Something on this cell will hold a wall lamp: a wall that takes attachments, built, framed or planned.</summary>
        public static bool Holds(IntVec3 c, Map map)
        {
            if (map == null || !c.InBounds(map)) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                var def = t is Blueprint || t is Frame ? t.def.entityDefToBuild as ThingDef : t.def;
                if (Supports(def)) return true;
            }
            return false;
        }

        /// <summary>Already hung (or planned) on this cell facing this way: the game allows one attachment per wall side.</summary>
        public static bool SideTaken(IntVec3 c, Rot4 rot, Map map)
        {
            if (map == null || !c.InBounds(map)) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                var def = t is Blueprint || t is Frame ? t.def.entityDefToBuild as ThingDef : t.def;
                if (IsAttached(def) && t.Rotation == rot) return true;
            }
            return false;
        }

        /// <summary>Names to try first for a wall lamp, best first: the electric lamp, then the wall torch.</summary>
        private static readonly string[] Preferred = { "WallLamp", "TorchWallLamp" };

        /// <summary>
        /// The wall lamp the colony would hang: the first of <paramref name="first"/> (a theme's or a plan's own) that it
        /// can build, else the electric wall lamp, the wall torch, else the cheapest to build of any mod's. Research,
        /// electricity, the build menu and the era are checked as for every other piece (<see cref="Avail.CanBuild"/>).
        /// Null if it can build none.
        /// </summary>
        public static ThingDef Best(Map map, IEnumerable<string> first = null)
        {
            try
            {
                if (first != null)
                    foreach (var name in first)
                    {
                        var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                        if (IsWallLight(d) && Avail.CanBuild(d, map)) return d;
                    }
                return Avail.BestBuildable(map, IsWallLight, Preferred);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wall lights", e);
                return null;
            }
        }

        /// <summary>The standing light the colony would put on the floor: a standing lamp, a torch, else the cheapest.</summary>
        public static ThingDef BestFloor(Map map) => Avail.BestBuildable(map, IsFloorLight, "StandingLamp", "TorchLamp");

        /// <summary>
        /// The light to suggest for a room of this many cells: a wall lamp unless the room is large (or the colony can't
        /// build one), else a standing lamp. Null if it can build no light at all.
        /// </summary>
        public static ThingDef ForRoom(Map map, int cells)
        {
            var wall = Best(map);
            var floor = BestFloor(map);
            return LightMath.UseWallLamps(cells, wall != null, floor != null, LightMath.Auto) ? wall : floor ?? wall;
        }

        /// <summary>The light to suggest for several rooms at once: wall lamps when most of them aren't large.</summary>
        public static ThingDef ForRooms(Map map, IEnumerable<Room> rooms)
        {
            int small = 0, large = 0;
            foreach (var r in rooms)
            {
                if (r == null) continue;
                if (LightMath.SizeOf(r.CellCount) == LightMath.RoomSize.Large) large++;
                else small++;
            }
            return ForRoom(map, small >= large ? LightMath.SmallRoomMaxCells : LightMath.MediumRoomMaxCells + 1);
        }

        /// <summary>Rooms bigger than this aren't searched for wall spots (a cavern, an open yard).</summary>
        private const int MaxRoomCells = 400;

        /// <summary>
        /// Where wall lamps would go in a built room: floor cells next to a wall that takes them (built or planned), facing
        /// it, free of buildings and plans and not beside a door, and where the game would accept the blueprint. Chosen
        /// one at a time where they light the most floor the room's lights (built or planned) leave dark; none if those
        /// light it all already. At most <paramref name="max"/>. Cells in <paramref name="avoid"/> are left alone.
        /// </summary>
        public static List<(IntVec3 cell, Rot4 rot)> SpotsInRoom(Room room, Map map, ThingDef def, ThingDef stuff, int max, ICollection<IntVec3> avoid = null, int min = 1)
        {
            var result = new List<(IntVec3 cell, Rot4 rot)>();
            if (room == null || map == null || def == null || max <= 0 || room.PsychologicallyOutdoors || room.CellCount > MaxRoomCells) return result;
            try
            {
                var cells = room.Cells.ToList();
                var lit = new List<(int x, int z)>();
                foreach (var t in room.ContainedAndAdjacentThings)
                {
                    if (t == null || !room.ContainsCell(t.Position)) continue;
                    var built = t is Blueprint || t is Frame ? t.def.entityDefToBuild as ThingDef : t.def;
                    if (built != null && built.category == ThingCategory.Building && Avail.IsLight(built)) lit.Add((t.Position.x, t.Position.z));
                }
                var spots = new List<(IntVec3 cell, Rot4 rot)>();
                var at = new List<(int x, int z)>();
                foreach (var c in cells)
                {
                    if (avoid != null && avoid.Contains(c)) continue;
                    if (!c.Standable(map) || c.GetEdifice(map) != null || NextToDoor(c, map) || PlannedHere(c, map)) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        var rot = new Rot4(f);
                        if (!Holds(c + rot.FacingCell, map) || SideTaken(c, rot, map)) continue;
                        if (!GenConstruct.CanPlaceBlueprintAt(def, c, rot, map, false, null, null, stuff).Accepted) continue;
                        spots.Add((c, rot));
                        at.Add((c.x, c.z));
                    }
                }
                foreach (int i in LightMath.Spread(at, cells.Select(c => (c.x, c.z)).ToList(), Reach(def), max, lit, min))
                    result.Add(spots[i]);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wall lamp spots", e);
            }
            return result;
        }

        private static bool NextToDoor(IntVec3 c, Map map)
        {
            foreach (var d in GenAdj.CardinalDirections)
            {
                var n = c + d;
                if (!n.InBounds(map)) continue;
                var ed = n.GetEdifice(map);
                if (ed != null && ed.def.IsDoor) return true;
                foreach (var t in n.GetThingList(map))
                    if ((t is Blueprint || t is Frame) && t.def.entityDefToBuild is ThingDef td && td.IsDoor) return true;
            }
            return false;
        }

        /// <summary>A building is planned or half-built on the cell (a floor being laid doesn't count).</summary>
        public static bool PlannedHere(IntVec3 c, Map map)
        {
            foreach (var t in c.GetThingList(map))
                if ((t is Blueprint || t is Frame) && t.def.entityDefToBuild is ThingDef) return true;
            return false;
        }
    }
}
