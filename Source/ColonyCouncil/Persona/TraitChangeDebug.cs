using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode (Debug actions, "Rimfolk"): see why a colonist's traits would or wouldn't change, or make the first change that holds happen.</summary>
    public static class TraitChangeDebug
    {
        [DebugAction("Rimfolk", "Traits that change: why or why not", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WhyNot(Pawn p)
        {
            Log.Message("[Rimfolk] " + TraitChanges.DebugFor(p, false));
            Messages.Message("Traits that change: the reasons are in the log for " + p.LabelShort + ".", p, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Traits that change: the first that holds, now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChangeNow(Pawn p) => Log.Message("[Rimfolk] " + TraitChanges.DebugFor(p, true));
    }
}
