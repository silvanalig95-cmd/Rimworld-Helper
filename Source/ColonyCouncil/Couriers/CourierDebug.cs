using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode: porters on their way now, to watch the walk to the mailbag (and what they do when there is no way in).</summary>
    public static class CourierDebug
    {
        [DebugAction("Rimfolk", "Couriers: porters bring 50 silver now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeliverNow()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            if (!Couriers.DeliverTrip(map, Couriers.Make(ThingDefOf.Silver, 50), null, DropCellFinder.TradeDropSpot(map), null, null, out var trip, true) || trip == null)
            {
                Messages.Message("Couriers: nobody will come (the setting is off, or no people at peace with the colony can send anyone).", MessageTypeDefOf.RejectInput, false);
                return;
            }
            // They set out on the next pass, by day and with the map safe, as any trip.
            trip.due = Find.TickManager.TicksGame;
            Messages.Message("Couriers: porters set out within a few seconds (by day, with the map safe).", MessageTypeDefOf.SilentInput, false);
        }
    }
}
