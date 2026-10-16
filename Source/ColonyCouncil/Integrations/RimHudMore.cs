using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // More lines for RimHUD's pawn panel (see RimHud.cs for how RimHUD finds them): Rimfolk's ailments, the recovery
    // after an illness, the personal story under way, and a doctor's or midwife's standing. Defs in
    // Defs/Misc/Council_RimHUD_More.xml, loaded only with RimHUD.

    /// <summary>Rimfolk's ailments and illnesses they have now (the lingering ones too); the tooltip has the doctor's notes.</summary>
    public static class RimHud_Ailments
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            if (RimHudUtil.For(pawn) == null || !AilmentTracker.On) return (null, null, null, null, null);
            var list = RimHudMore.Ailments(pawn);
            if (list.Count == 0) return ("CC_Hud_Ailments".Translate(), "CC_Hud_None".Translate(), null, null, null);
            string value = string.Join(", ", list.Select(h => h.LabelBase));
            return ("CC_Hud_Ailments".Translate(), value.CapitalizeFirst(), () => RimHudMore.AilmentsTip(list), null, null);
        }
    }

    /// <summary>Convalescing: about how many days to full strength (fewer with rest in bed).</summary>
    public static class RimHud_Recovery
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            if (RimHudUtil.For(pawn) == null || !HealthTracker.ConvalescenceOn) return (null, null, null, null, null);
            var h = HealthTracker.ConvalescenceOf(pawn);
            if (h == null) return ("CC_Hud_Recovery".Translate(), "CC_Hud_Recovery_Full".Translate(), null, null, null);
            string days = HealthMath.DaysUp(h.Severity).ToString("0");
            string bed = HealthMath.DaysInBed(h.Severity).ToString("0");
            return ("CC_Hud_Recovery".Translate(), "CC_Hud_Recovery_Days".Translate(days), () => "CC_Health_Story_Convalescing".Translate(days.Named("DAYS"), bed.Named("BEDDAYS")).Resolve()
                + "\n\n" + h.def.description, null, null);
        }
    }

    /// <summary>
    /// Recovery as a bar: it fills as they get their strength back (empty at the longest convalescence there is), marked
    /// where they stop being weak. Hidden (fill below 0) while they aren't convalescing.
    /// </summary>
    public static class RimHud_RecoveryBar
    {
        private static readonly float[] Marks = { 1f - HealthMath.WeakFrom / HealthMath.MaxSeverity };

        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            if (RimHudUtil.For(pawn) == null || !HealthTracker.ConvalescenceOn) return (null, null, -1f, null, null, null, null);
            var h = HealthTracker.ConvalescenceOf(pawn);
            if (h == null) return (null, null, -1f, null, null, null, null);
            string days = HealthMath.DaysUp(h.Severity).ToString("0");
            string bed = HealthMath.DaysInBed(h.Severity).ToString("0");
            float fill = UnityEngine.Mathf.Clamp01(1f - h.Severity / HealthMath.MaxSeverity);
            return ("CC_Hud_Recovery".Translate(), "CC_Hud_Recovery_Short".Translate(days), fill, Marks,
                () => "CC_Health_Story_Convalescing".Translate(days.Named("DAYS"), bed.Named("BEDDAYS")).Resolve(), null, null);
        }
    }

    /// <summary>The personal story under way and its stage; the tooltip says what it waits for and what it's about.</summary>
    public static class RimHud_Story
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            if (RimHudUtil.For(pawn) == null || !ArcTracker.On) return (null, null, null, null, null);
            var arc = ArcTracker.Instance?.Of(pawn);
            if (arc?.def == null || arc.Stage == null) return ("CC_Hud_Story".Translate(), "CC_Hud_None".Translate(), null, null, null);
            string value = arc.def.LabelCap + " (" + arc.Stage.label + ")";
            return ("CC_Hud_Story".Translate(), value, () => RimHudMore.StoryTip(arc), null, null);
        }
    }

    /// <summary>Standing as a doctor and a midwife: trusted, renowned, or how many pulled through so far.</summary>
    public static class RimHud_Healer
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            if (RimHudUtil.For(pawn) == null || !HealthTracker.ReputationOn) return (null, null, null, null, null);
            var rec = HealthTracker.Instance?.RecordOf(pawn);
            if (rec == null || rec.Saved + rec.lost + rec.births == 0) return ("CC_Hud_Healer".Translate(), "CC_Hud_None".Translate(), null, null, null);
            var parts = new List<string>();
            int level = rec.Level;
            if (level >= 2) parts.Add("CC_Hud_Healer_Renowned".Translate());
            else if (level == 1) parts.Add("CC_Hud_Healer_Trusted".Translate());
            else if (rec.Saved > 0) parts.Add("CC_Hud_Healer_Saved".Translate(rec.Saved));
            int midwife = HealthMath.MidwifeLevel(rec.births);
            if (midwife > 0) parts.Add(midwife >= 2 ? "CC_Hud_Healer_MidwifeRenowned".Translate() : "CC_Hud_Healer_Midwife".Translate());
            if (parts.Count == 0) parts.Add("CC_Hud_Healer_Saved".Translate(rec.Saved));
            string value = string.Join(", ", parts).CapitalizeFirst();
            return ("CC_Hud_Healer".Translate(), value, () => string.Join("\n", HealthTracker.StoryLines(pawn)), null, null);
        }
    }

    internal static class RimHudMore
    {
        /// <summary>Rimfolk's ailments on them, the ones that show.</summary>
        public static List<Hediff> Ailments(Pawn pawn)
        {
            var list = new List<Hediff>();
            var hs = pawn?.health?.hediffSet?.hediffs;
            if (hs == null) return list;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h?.def == null || !h.Visible || h.def == HealthDefOf.Council_Ail_Convalescent) continue;
                if (AilmentTracker.InfoOf(h.def) != null) list.Add(h);
            }
            return list;
        }

        public static string AilmentsTip(List<Hediff> list)
        {
            try
            {
                var lines = new List<string>();
                foreach (var h in list)
                {
                    string note = AilmentTracker.DoctorsNote(h.def);
                    lines.Add(h.LabelCap + (note.NullOrEmpty() ? "" : ": " + note));
                }
                return string.Join("\n\n", lines);
            }
            catch
            {
                return string.Join(", ", list.Select(h => h.LabelCap));
            }
        }

        public static string StoryTip(Arc arc)
        {
            try
            {
                string tip = "CC_Arc_Story_Now".Translate(arc.def.LabelCap, arc.Stage.label, arc.stage + 1, arc.def.stages.Count).Resolve();
                string waiting = ArcText.Waiting(arc);
                string level = ArcWorker_MagicDiscipline.Progress(arc);
                if (!waiting.NullOrEmpty()) tip += "\n" + waiting + (level == null ? "" : " (" + level + ")");
                if (!arc.def.summary.NullOrEmpty()) tip += "\n\n" + arc.def.summary;
                return tip;
            }
            catch
            {
                return arc.def.LabelCap;
            }
        }
    }
}
