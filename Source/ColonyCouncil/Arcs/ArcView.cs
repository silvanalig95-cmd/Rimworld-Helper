using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Personal stories in the interface: for the Story tab, the story under way and where it stands (and the stories
    /// they're part of), and the ones that have ended; and the dev mode actions. The setting is drawn by the settings window.
    /// </summary>
    public static class ArcView
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Done = new Color(0.6f, 0.9f, 0.6f);

        private static bool Off(Pawn p) => p == null || ArcTracker.Instance == null || ColonyCouncilMod.Settings.arcFrequency == ArcFrequency.Off;

        /// <summary>
        /// The Story tab's overview line: the story under way and the stage it's at ("The long road: second thoughts"),
        /// else one they're part of; <paramref name="tip"/> has where it stands, what it waits for and the stories they're
        /// part of. <paramref name="stage"/> is the stage it's at (0 first) of <paramref name="stages"/>, or -1 for
        /// someone else's story. Null when there's none (or stories are off).
        /// </summary>
        public static string Now(Pawn p, out string tip, out int stage, out int stages)
        {
            tip = null;
            stage = -1;
            stages = 0;
            if (Off(p)) return null;
            try
            {
                var tracker = ArcTracker.Instance;
                var tips = new List<string>();
                string line = null;
                var arc = tracker.Of(p);
                if (arc?.def != null && arc.Stage != null)
                {
                    line = "CC_Arc_Story_Short".Translate(arc.def.LabelCap, arc.Stage.label).Resolve();
                    stage = arc.stage;
                    stages = arc.def.stages.Count;
                    tips.Add("CC_Arc_Story_Now".Translate(arc.def.LabelCap, arc.Stage.label, arc.stage + 1, arc.def.stages.Count).Resolve());
                    string waiting = ArcText.Waiting(arc);
                    if (!waiting.NullOrEmpty()) tips.Add(waiting);
                }
                foreach (var a in tracker.PartOf(p).Where(a => a.def != null && a.pawn != null))
                {
                    string part = "CC_Arc_Story_PartOf".Translate(a.pawn.LabelShort, a.def.label).Resolve();
                    line ??= part;
                    tips.Add(part);
                }
                if (line != null) tip = string.Join("\n\n", tips);
                return line;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("personal story overview", e);
                return null;
            }
        }

        /// <summary>The stories of theirs that have ended, newest first, for the Story tab's life so far.</summary>
        public static List<(string, Color)> Past(Pawn p, int max = 3)
        {
            var lines = new List<(string, Color)>();
            if (Off(p)) return lines;
            try
            {
                foreach (var r in ArcTracker.Instance.PastOf(p).Take(max))
                {
                    string label = DefDatabase<ArcDef>.GetNamedSilentFail(r.defName)?.LabelCap ?? r.label?.CapitalizeFirst() ?? "?";
                    string day = "CC_DayN".Translate(r.endTick / GenDate.TicksPerDay + 1);
                    string key = r.outcome == ArcOutcome.Done ? "CC_Arc_Story_Done" : "CC_Arc_Story_Fizzled";
                    lines.Add((key.Translate(label, day).Resolve(), r.outcome == ArcOutcome.Done ? Done : Faint));
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("personal story lines", e);
            }
            return lines;
        }

        // ---------------- Dev mode ----------------

        [DebugAction("Rimfolk", "Personal story: begin one for...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BeginFor(Pawn p)
        {
            var tracker = ArcTracker.Instance;
            if (tracker == null || p == null) return;
            if (tracker.Of(p) != null)
            {
                Messages.Message(p.LabelShort + " already has a story under way.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var home = ArcTracker.HomeColonists();
            var options = tracker.Candidates(home, Find.TickManager.TicksGame, true).Where(o => o.c.pawn == p).ToList();
            if (options.Count == 0)
            {
                Messages.Message("No personal story fits " + p.LabelShort + " right now.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options.Select(o => new FloatMenuOption(o.def.label + (o.c.other != null ? " (" + o.c.other.LabelShort + (o.c.third != null ? ", " + o.c.third.LabelShort : "") + ")" : ""),
                () => tracker.Begin(o.def, o.c))).ToList()));
        }

        [DebugAction("Rimfolk", "Personal story: next stage", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NextStage(Pawn p)
        {
            var tracker = ArcTracker.Instance;
            var arc = tracker?.Of(p);
            if (arc == null) Messages.Message("No story under way for " + p?.LabelShort + ".", MessageTypeDefOf.RejectInput, false);
            else tracker.DebugAdvance(arc);
        }

        [DebugAction("Rimfolk", "Personal story: let it fizzle", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LetFizzle(Pawn p)
        {
            var tracker = ArcTracker.Instance;
            tracker?.DebugFizzle(tracker.Of(p));
        }

        [DebugAction("Rimfolk", "Personal story: list what could begin", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ListCandidates()
        {
            var tracker = ArcTracker.Instance;
            if (tracker == null) return;
            var options = tracker.Candidates(ArcTracker.HomeColonists(), Find.TickManager.TicksGame, true);
            string text = options.Count == 0 ? "No personal story fits anyone right now."
                : string.Join("\n", options.Select(o => o.c.pawn.LabelShort + ": " + o.def.label + (o.c.other != null ? " with " + o.c.other.LabelShort : "") + (o.c.third != null ? " and " + o.c.third.LabelShort : "") + " (" + o.weight.ToString("0.##") + ")"));
            Log.Message("[Rimfolk] Personal stories that could begin:\n" + text);
            Messages.Message(options.Count + " personal stories could begin (listed in the log).", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
