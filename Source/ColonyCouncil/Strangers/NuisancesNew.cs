using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The smell of a butcher's table or spot (any building of ours named for butchering) where someone eats or sleeps:
    /// the room they're eating in right now, or, while they're in it, the bedroom or barracks they sleep in. Kitchens
    /// count too when they eat there. The fix is plain to see: move it.
    /// </summary>
    public class NuisanceCheck_ButcherSmell : NuisanceCheckWorker
    {
        public override string Check(Pawn p, Map map)
        {
            var here = IndoorRoom(p);
            if (here == null) return null;
            if (p.CurJobDef == JobDefOf.Ingest) return Butcher(here)?.def.label;
            var bedroom = RoomUtil.BedRoom(p);
            return bedroom == here ? Butcher(bedroom)?.def.label : null;
        }

        /// <summary>A butcher's table or spot of ours standing in this closed room, or null.</summary>
        public static Thing Butcher(Room room)
        {
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
            {
                var t = things[i];
                if (t.def.category != ThingCategory.Building || t.Faction != Faction.OfPlayer) continue;
                if (t.def.defName.IndexOf("Butcher", StringComparison.OrdinalIgnoreCase) < 0 || t.GetRoom() != room) continue;
                return t;
            }
            return null;
        }
    }

    /// <summary>
    /// A draught at the bed: below 5 °C outside (for an ordinary body; their own comfortable range moves it), their bed stands a few steps from a door that opens straight from the
    /// bedroom to the outside. The room can be warm on average; an anteroom (a second door) stops it. {THING} is the door.
    /// </summary>
    public class NuisanceCheck_DraughtyBed : NuisanceCheckWorker
    {
        /// <summary>How close the door must be to the bed: a few steps.</summary>
        public const float Steps = 3.9f;

        public override string Check(Pawn p, Map map)
        {
            // Below 5 °C outside, moved by their own comfortable range (genes, traits: Health/BodyMath.cs).
            BodyFit.TempShift(p, out float cold, out _);
            if (!BodyMath.Draughty(map.mapTemperature.OutdoorTemp, cold)) return null;
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned || bed.Map != map || bed.Medical) return null;
            var room = bed.GetRoom();
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            foreach (var t in GenRadial.RadialDistinctThingsAround(bed.Position, map, Steps, true))
                if (t is Building_Door door && door.Spawned && StraightOut(door, room, map))
                    return door.def.label;
            return null;
        }

        /// <summary>The door has this room on one side and the outdoors on another.</summary>
        public static bool StraightOut(Building_Door door, Room room, Map map)
        {
            bool inside = false, outside = false;
            foreach (var c in GenAdj.CellsAdjacentCardinal(door))
            {
                if (!c.InBounds(map)) continue;
                var r = c.GetRoom(map);
                if (r == null) continue;
                if (r == room) inside = true;
                else if (r.PsychologicallyOutdoors || r.UsesOutdoorTemperature) outside = true;
            }
            return inside && outside;
        }
    }
}
