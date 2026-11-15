using LudeonTK;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): a visitor's idle hour filled now (the chance and their next look aside, everything
    /// else as always), and every visitor on the map with their group and what fits them now, or why nothing does
    /// (Visitors/IdleHours.cs).
    /// </summary>
    public static class IdleHoursDebug
    {
        [DebugAction("Rimfolk", "Visitors: fill this visitor's idle hour now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFillNow(Pawn p)
        {
            var job = IdleHours.JobFor(p, true);
            if (job == null)
            {
                IdleHours.LookFor(p, out string why);
                Log.Message("[Rimfolk] Idle hours: nothing for " + p.LabelShort + " now (" + (why ?? "nothing about them fits the hour, the weather and who they are") + ").");
                return;
            }
            p.jobs.StartJob(job, Verse.AI.JobCondition.InterruptForced, null, false, true);
            Log.Message("[Rimfolk] Idle hours: " + p.LabelShort + " sets off: " + job.def.defName + ".");
        }

        [DebugAction("Rimfolk", "Visitors: idle hours of everyone here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReport()
        {
            var lines = IdleHours.Report(Find.CurrentMap);
            Log.Message("[Rimfolk] Visitors' idle hours on " + Find.CurrentMap + (lines.Count == 0 ? ": nobody from outside." : ":\n" + string.Join("\n", lines)));
        }
    }
}
