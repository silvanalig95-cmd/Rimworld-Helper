using LudeonTK;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): the rooms visitors keep out of on the map in view, as at the last look (a
    /// bedroom of the colony's people, a cold store), each with what the game calls it, its warmth and food, who sleeps in
    /// it, whose bed and whose doctor's work it is; and each visitor, inside one or not (Visitors/KeepOut.cs).
    /// </summary>
    public static class KeepOutDebug
    {
        [DebugAction("Rimfolk", "Visitors: rooms kept from them (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReport()
        {
            var map = Find.CurrentMap;
            var lines = KeepOut.Report(map);
            Log.Message("[Rimfolk] Rooms visitors keep out of on " + map + ":\n" + string.Join("\n", lines));
        }

        [DebugAction("Rimfolk", "Visitors: look at the kept rooms again now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRefresh()
        {
            KeepOut.Refresh(Find.CurrentMap);
            DebugReport();
        }
    }
}
