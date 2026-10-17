using System;
using HarmonyLib;
using RimWorld;

namespace ColonyCouncil
{
    /// <summary>
    /// Deeds the game writes into its history (an execution, an organ harvested, human flesh eaten) with the one who did
    /// them: counted for the rare trait changes that need a history like that (TraitChanges.Notify_Event). Only a
    /// dictionary lookup for every other event.
    /// </summary>
    [HarmonyPatch(typeof(HistoryEventsManager), nameof(HistoryEventsManager.RecordEvent))]
    public static class Patch_TraitChangeDeeds
    {
        public static void Postfix(HistoryEvent historyEvent)
        {
            try
            {
                TraitChanges.Notify_Event(historyEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes: a deed", e);
            }
        }
    }
}
