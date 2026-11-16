using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk", "Storylines: ..."): each storyline started now (the pace skipped), the part under
    /// way ended with any of its outcomes (the branch only: what the part would play out on the map is skipped), the next
    /// part or ending made due now, and what stands in the log.
    /// </summary>
    public static class StorylinesDebug
    {
        private static void Say(string text) => Messages.Message("Storylines: " + text, MessageTypeDefOf.SilentInput, false);

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Starts a storyline now, whatever the pace says (each storyline's own dev action calls this).</summary>
        internal static void Start(string kind)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            if (Storylines.Running.Any(s => s.kind == kind))
            {
                Say(kind + " is running already.");
                return;
            }
            var open = Missions.All.FirstOrDefault(m => m.Open);
            if (open != null)
            {
                Say("a mission is open (" + open.title + "): one at a time. End it first (\"Storylines: end the current part\", or the Quests tab).");
                return;
            }
            var def = Storylines.PartDef(kind, StorylineMath.FirstPart(kind));
            if (def == null)
            {
                Say("no event offers the first part of " + kind + ".");
                return;
            }
            Storylines.debugForce = true;
            bool ok;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                ok = def.Worker.TryExecute(parms);
            }
            finally
            {
                Storylines.debugForce = false;
            }
            Say(ok ? kind + " started: its first part is in the Quests tab (or a letter)." : kind + " couldn't start here (no people of the right kind at peace, or what it needs is missing: see the log).");
        }

        [DebugAction("Rimfolk", "Storylines: start the factor's goods now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFactor() => Start(StorylineMath.Factor);

        [DebugAction("Rimfolk", "Storylines: start the beast coming down the valley now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeast() => Start(StorylineMath.Beast);

        [DebugAction("Rimfolk", "Storylines: start the children from the burned village now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugChildren() => Start(StorylineMath.Children);

        [DebugAction("Rimfolk", "Storylines: start the sickness in the next town now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSickness() => Start(SicknessFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start the strayed herd now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHerd() => Start(HerdFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start the pilgrims' relic now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRelic() => Start(RelicFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start two towns' quarrel now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuarrel() => Start(QuarrelFlow.Key);

        [DebugAction("Rimfolk", "Storylines: the pilgrims' relic stolen tonight, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTheft()
        {
            var s = Storylines.Running.FirstOrDefault(x => x.kind == RelicFlow.Key && x.missionId >= 0);
            var m = s == null ? null : Missions.ById(s.missionId);
            if (m == null || !(Missions.Worker(m) is HappeningWorker_RelicRest w) || m.stage != 1)
            {
                Say("no pilgrims with their relic are staying at the colony now.");
                return;
            }
            Say(w.DebugSteal(m) ? "the relic is stolen." : "it couldn't be stolen now (the relic isn't lying where they sleep, or the thief isn't here).");
        }

        [DebugAction("Rimfolk", "Storylines: end the current part with...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEndPart()
        {
            var s = Storylines.Running.FirstOrDefault(x => x.missionId >= 0 && Missions.ById(x.missionId)?.Open == true);
            if (s == null)
            {
                Say("no storyline has a part offered or under way.");
                return;
            }
            var m = Missions.ById(s.missionId);
            string part = s.Last?.part ?? "";
            var flow = StorylineMath.Flow(s.kind);
            var options = new List<FloatMenuOption>();
            foreach (var o in StorylineMath.Outcomes(s.kind, part).Concat(new[] { StorylineMath.Declined }))
            {
                string outcome = o;
                options.Add(new FloatMenuOption(part + ": " + outcome, () =>
                {
                    var state = outcome == StorylineMath.Declined ? MissionState.Declined : flow != null && flow.Failed(outcome) ? MissionState.Failed : MissionState.Done;
                    // What the branch reads, set as the part would have (a flag, the neighbours, a sum).
                    Storylines.Rules(s.kind)?.DebugEnded(s, part, outcome);
                    Missions.Finish(m, state, outcome, null);
                    Say(s.kind + ": " + part + " ended " + outcome + " (the branch only). Next: " + (s.Running ? s.next ?? "-" : "ended " + s.ending) + ".");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Storylines: the next part (or ending) due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNextNow()
        {
            var s = Storylines.Running.FirstOrDefault(x => x.missionId < 0 && x.next != null);
            if (s == null)
            {
                Say("no storyline is waiting for its next part.");
                return;
            }
            s.due = Now;
            s.tries = 0;
            Storylines.Step(s, Now);
            Say(s.kind + ": " + (s.Running ? s.missionId >= 0 ? "its next part is offered." : "not now (another mission open, a question waiting or a fight on); tried again in a few hours." : "ended " + s.ending + "."));
        }

        [DebugAction("Rimfolk", "Storylines: what stands (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var sb = new StringBuilder("[Rimfolk] Storylines:\n");
            foreach (var kind in StorylineMath.Kinds) sb.AppendLine("  may start " + kind + ": " + Storylines.CanStart(kind));
            foreach (var s in Storylines.All) sb.AppendLine("  " + Storylines.Describe(s));
            foreach (var a in Storylines.Book?.afters ?? new List<StorylineAfter>())
                sb.AppendLine("  after: " + a.kind + " with " + (a.faction?.Name ?? "?") + (a.kind == "share" ? ", " + a.left + " more, " + a.amount + " silver each" : ", until day " + (a.until / GenDate.TicksPerDay)));
            Log.Message(sb.ToString());
            Say("written to the log.");
        }
    }
}
