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

        /// <summary>The outcomes each part can end with.</summary>
        private static readonly Dictionary<string, string[]> Outcomes = new Dictionary<string, string[]>
        {
            ["keep"] = new[] { "kept", "lost" },
            ["rival"] = new[] { "sold", "refused", "told" },
            ["makegood"] = new[] { "paid", "unpaid" },
            ["debt"] = new[] { "paid", "unpaid" },
            ["ready"] = new[] { "ready", "partly", "unready" },
            ["signs"] = new[] { "found", "missed" },
            ["beast"] = new[] { "killed", "driven", "left" },
            ["shelter"] = new[] { "takein", "place", "sendon", "harmed" },
            ["back"] = new[] { "joined", "visited", "traded", "notjoined" }
        };

        private static void Start(string kind)
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
            var options = new List<FloatMenuOption>();
            foreach (var o in (Outcomes.TryGetValue(part, out var list) ? list : new string[0]).Concat(new[] { StorylineMath.Declined }))
            {
                string outcome = o;
                options.Add(new FloatMenuOption(part + ": " + outcome, () =>
                {
                    var state = outcome == StorylineMath.Declined ? MissionState.Declined
                        : outcome == "lost" || outcome == "unpaid" || outcome == "unready" || outcome == "missed" || outcome == "harmed" ? MissionState.Failed : MissionState.Done;
                    if (outcome == "sold") s.Set("sold");
                    else if (outcome == "told") s.Set("told");
                    else if (outcome == "lost") s.amount = StorylineMath.LostDebt(s.worth / 3f);
                    else if (outcome == "place" || outcome == "sendon") s.Set(outcome);
                    if (outcome == "place" && s.other == null) s.other = ChildrenRules.Neighbours(s.giver);
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
