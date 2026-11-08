using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// How fast Rimfolk's people go about the map (the player's report, 8 November 2026: "some of the npcs you create,
    /// including the earlier couriers, seem to walk only quite slowly on the map, never as fast as my colonists"). The
    /// game's "walk" urgency is half the ordinary pace and "amble" a third; the colony's own people go about their work at
    /// the ordinary pace ("jog"). Errands and trips (couriers, riders, envoys and parties, visitors and camps coming and
    /// going, someone sent somewhere) go at <see cref="Errand"/>; a stroll, a wander about a camp, browsing a stall, a herd
    /// or a pet on its own business, a procession or a group keeping together keep their slower pace on purpose.
    /// </summary>
    public static class Pace
    {
        public const LocomotionUrgency Errand = LocomotionUrgency.Jog;
    }

    /// <summary>The game's travel to a spot, at an errand's pace (its own duty walks at half speed).</summary>
    public class LordToil_TravelOnErrand : LordToil_Travel
    {
        public LordToil_TravelOnErrand(IntVec3 dest) : base(dest)
        {
        }

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
            foreach (var p in lord.ownedPawns)
                if (p?.mindState?.duty != null)
                    p.mindState.duty.locomotion = Pace.Errand;
        }
    }
}
