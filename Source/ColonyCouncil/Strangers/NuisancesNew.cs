using System;
using System.Linq;
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
    /// The hearth gone out in the night (the review of the visitors and strangers, a nuisance in the frame there is): cold
    /// outside (below 5 °C for an ordinary body, as the draught at the bed reads it), and the fire that warms the room they
    /// sleep in (a campfire, a fireplace, a brazier of the colony's: anything that burns fuel for heat) has burned out, its
    /// fuel all gone. {THING} is the fire. Fixed by keeping it fed, or by heat that needs no feeding.
    /// </summary>
    public class NuisanceCheck_VisR7HearthOut : NuisanceCheckWorker
    {
        public override string Check(Pawn p, Map map)
        {
            BodyFit.TempShift(p, out float cold, out _);
            if (!BodyMath.Draughty(map.mapTemperature.OutdoorTemp, cold)) return null;
            var room = RoomUtil.BedRoom(p);
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
            {
                var t = things[i];
                if (t.def.category != ThingCategory.Building || t.Faction != Faction.OfPlayer || t.GetRoom() != room) continue;
                if (t.def.GetCompProperties<CompProperties_HeatPusher>() == null) continue;
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel != null && !fuel.HasFuel) return t.def.label;
            }
            return null;
        }
    }

    /// <summary>
    /// Worn-out footwear (the review of the visitors and strangers): something they wear on their feet alone (boots,
    /// shoes, sandals; never armour that covers the feet with the rest) is down to less than half its hit points. {THING}
    /// is it. Fixed by a new pair.
    /// </summary>
    public class NuisanceCheck_VisR7WornBoots : NuisanceCheckWorker
    {
        /// <summary>Worn out below this share of its hit points.</summary>
        public const float WornBelow = 0.5f;

        public override string Check(Pawn p, Map map)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return null;
            foreach (var ap in worn)
            {
                var groups = ap.def.apparel?.bodyPartGroups;
                if (groups == null || groups.Count == 0 || !ap.def.useHitPoints || ap.MaxHitPoints <= 0) continue;
                if (!groups.All(g => g != null && g.defName == "Feet")) continue;
                if (ap.HitPoints < ap.MaxHitPoints * WornBelow) return ap.def.label;
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
