using System;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the ways to play ("Rimfolk", "Ways to play: ..."). The way leaning the game and every lever's value now in
    /// the log; the next story direction (the storyteller itself is switched in the game's own options, Gameplay, Change
    /// storyteller); Rise and Fall's omen made to come now; who a raid with a reason would come from.
    /// </summary>
    public static class StoryWaysDebug
    {
        private static void Say(string text) => Messages.Message("Ways to play: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Ways to play: show the levers now (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShow()
        {
            Log.Message(StoryDirection.Describe());
            Say("the levers are in the log.");
        }

        [DebugAction("Rimfolk", "Ways to play: next story direction", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugNextDirection()
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null) return;
            var all = (StoryWay[])Enum.GetValues(typeof(StoryWay));
            s.storyDirection = all[(Array.IndexOf(all, s.storyDirection) + 1) % all.Length];
            s.Write();
            Say("the story direction is " + s.storyDirection + " (the storyteller: Options, Gameplay, Change storyteller).");
        }

        [DebugAction("Rimfolk", "Ways to play: Rise and Fall's omen now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOmen()
        {
            var map = WorldSituations.PlaceNear() ?? Find.CurrentMap;
            string why = StorytellerComp_WayOmen.Try(map, true);
            Say(why == null ? "the word of the arc's trouble (" + StoryDirection.Trouble + ") has come." : why);
        }

        [DebugAction("Rimfolk", "Ways to play: who a raid with a reason would come from", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReasons()
        {
            var map = Find.CurrentMap;
            var mem = FactionMemory.Instance;
            var sb = new System.Text.StringBuilder("[Rimfolk] ways to play: the raids' reasons at this home (the active way aims "
                                                   + StoryDirection.Factor(StoryWaysMath.RaidReasons).ToString("0.00") + " of them):\n");
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
                if (Raids.CanRaid(f, map))
                    sb.AppendLine("  " + f.Name + ": reason " + StoryReasons.Reason(mem, f).ToString("0.0") + (StoryReasons.Reason(mem, f) >= StoryWaysMath.GrievanceFloor ? "" : " (not enough)"));
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            var pick = StoryReasons.Pick(map, parms, IncidentDefOf.RaidEnemy.Worker as IncidentWorker_RaidEnemy);
            sb.AppendLine("  A raid now would come from: " + (pick?.Name ?? "the game's own choice"));
            Log.Message(sb.ToString());
            Say("the reasons are in the log.");
        }
    }
}
