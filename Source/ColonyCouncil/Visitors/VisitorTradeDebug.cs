using LudeonTK;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): a visitor sent over to the colony's market now, if they may (their group's turn
    /// and the market's hours aside), and every group of visitors on the map with its turns and why each of them would or
    /// wouldn't go (Visitors/VisitorTradeGuests.cs).
    /// </summary>
    public static class VisitorTradeDebug
    {
        [DebugAction("Rimfolk", "Market: send this visitor to the stalls now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSendNow(Pawn p)
        {
            string why = VisitorTrade.GuestNow(p);
            Log.Message("[Rimfolk] Market: " + p.LabelShort + (why == null ? " sets off for the stalls." : " doesn't go: " + why + "."));
        }

        [DebugAction("Rimfolk", "Market: every visitor and why (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReport()
        {
            var lines = VisitorTrade.GuestReport(Find.CurrentMap);
            Log.Message("[Rimfolk] Market, every other visitor on " + Find.CurrentMap + (lines.Count == 0 ? ": none." : ":\n" + string.Join("\n", lines)));
        }
    }
}
