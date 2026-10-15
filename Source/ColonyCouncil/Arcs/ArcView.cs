using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Personal stories in the interface: a few lines for the Story tab (the story under way and where it stands, the
    /// stories they're part of, the ones that have ended), the setting, and the dev mode actions.
    /// </summary>
    public static class ArcView
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Done = new Color(0.6f, 0.9f, 0.6f);

        /// <summary>The Story tab's lines for this colonist; empty when there's nothing to tell (or stories are off).</summary>
        public static List<(string, Color)> Lines(Pawn p)
        {
            var lines = new List<(string, Color)>();
            var tracker = ArcTracker.Instance;
            if (p == null || tracker == null || ColonyCouncilMod.Settings.arcFrequency == ArcFrequency.Off) return lines;
            try
            {
                var arc = tracker.Of(p);
                if (arc?.def != null && arc.Stage != null)
                {
                    lines.Add(("CC_Arc_Story_Now".Translate(arc.def.LabelCap, arc.Stage.label, arc.stage + 1, arc.def.stages.Count).Resolve(), Color.white));
                    string waiting = ArcText.Waiting(arc);
                    if (!waiting.NullOrEmpty()) lines.Add((waiting, Faint));
                }
                foreach (var a in tracker.PartOf(p).Where(a => a.def != null && a.pawn != null))
                    lines.Add(("CC_Arc_Story_PartOf".Translate(a.pawn.LabelShort, a.def.label).Resolve(), Faint));
                foreach (var r in tracker.PastOf(p).Take(3))
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

        /// <summary>The setting, as a row of choices (in the People page of the mod settings).</summary>
        public static void SettingsRow(Listing_Standard l, CouncilSettings s)
        {
            if (l.ButtonTextLabeled("CC_Settings_Arcs".Translate(), ("CC_Arcs_" + s.arcFrequency).Translate(), TextAnchor.UpperLeft, null, "CC_Settings_Arcs_Tip".Translate()))
            {
                var options = new List<FloatMenuOption>();
                foreach (ArcFrequency f in System.Enum.GetValues(typeof(ArcFrequency)))
                {
                    var local = f;
                    options.Add(new FloatMenuOption(("CC_Arcs_" + f).Translate(), () => s.arcFrequency = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
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
            Find.WindowStack.Add(new FloatMenu(options.Select(o => new FloatMenuOption(o.def.label + (o.c.other != null ? " (" + o.c.other.LabelShort + ")" : ""),
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
                : string.Join("\n", options.Select(o => o.c.pawn.LabelShort + ": " + o.def.label + (o.c.other != null ? " with " + o.c.other.LabelShort : "") + " (" + o.weight.ToString("0.##") + ")"));
            Log.Message("[Rimfolk] Personal stories that could begin:\n" + text);
            Messages.Message(options.Count + " personal stories could begin (listed in the log).", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
