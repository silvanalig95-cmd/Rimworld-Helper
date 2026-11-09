using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Lines for RimHUD's pawn panel. RimHUD (1.6) loads "custom value" widgets from defs (Defs/Misc/Council_RimHUD.xml,
    /// loaded only with RimHUD) and calls their class's static GetParameters(Pawn) by reflection, so Rimfolk needs no
    /// reference to it. The player adds them in RimHUD's layout editor, under third-party widgets. Each returns
    /// (label, value, tooltip, onHover, onClick); all null leaves the line blank (not a colonist, feature switched off).
    /// </summary>
    public static class RimHud_Trust
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var comp = RimHudUtil.For(pawn);
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) return (null, null, null, null, null);
            var level = comp.standings.LevelOf(pawn);
            float trust = comp.standings.TrustOf(pawn);
            string value = ("CC_Trust_" + level).Translate().CapitalizeFirst();
            return ("CC_Hud_Trust".Translate(), value, () => "CC_Hud_Trust_Tip".Translate(pawn.LabelShort, trust.ToString("0")).Resolve(), null, null);
        }
    }

    public static class RimHud_Wish
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var comp = RimHudUtil.For(pawn);
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) return (null, null, null, null, null);
            var w = comp.OpenWishBy(pawn) ?? comp.OpenWishInvolving(pawn);
            if (w == null) return ("CC_Hud_Wish".Translate(), "CC_Hud_None".Translate(), null, null, null);
            return ("CC_Hud_Wish".Translate(), w.Label, () => RimHudUtil.WishTip(w), null, null);
        }
    }

    public static class RimHud_Office
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var comp = RimHudUtil.For(pawn);
            if (comp == null) return (null, null, null, null, null);
            var office = OfficeTracker.HeldBy(pawn);
            if (office == null) return ("CC_Hud_Office".Translate(), "CC_Hud_None".Translate(), null, null, null);
            return ("CC_Hud_Office".Translate(), office.NameCap, () => office.Description, null, null);
        }
    }

    public static class RimHud_Goal
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var comp = RimHudUtil.For(pawn);
            if (comp == null) return (null, null, null, null, null);
            var goal = comp.aspirations.Active(pawn);
            if (goal == null) return ("CC_Hud_Goal".Translate(), "CC_Hud_None".Translate(), null, null, null);
            string text = goal.Goal.CapitalizeFirst() + " (" + goal.Progress.ToStringPercent() + ")";
            string spark = goal.SparkText;
            return ("CC_Hud_Goal".Translate(), text, () => spark.NullOrEmpty() ? text : text + "\n" + spark.CapitalizeFirst(), null, null);
        }
    }

    /// <summary>
    /// The heart at a glance, short enough for RimHUD's value cell (the player's screenshot of 9 November 2026: "Strain
    /// jown (60) · fighting ...": RimHUD draws a value too long for its cell right-aligned and cut from the left, then
    /// "..."): strain's band in a word and its value in its band's colour, then as much as fits of a day off or bad
    /// afternoon under way, the top cause while strained and the good side in green ("Worn 60 · fighting"). The whole line
    /// leads the tooltip ("Worn down (61) · in good heart (48) · the plague · day off"), then strain in full with what would
    /// help first; a click opens their Mood tab (Heart/). The line is fitted once each time the heart changes, not per
    /// frame. RimHUD's hooks give a value widget no bar of its own; strain's bar is its own widget (RimHud_StrainBar).
    /// </summary>
    public static class RimHud_Strain
    {
        /// <summary>
        /// The value's width in RimHUD's small text, which its cell shows whole: the cell beside the label at RimHUD's default
        /// label width in a two-column pane, with room for its "...".
        /// </summary>
        public const float Budget = 112f;

        /// <summary>Each colonist's line as fitted, and the full line it was fitted from (worked out again when that changes).</summary>
        private static readonly Dictionary<int, (string from, string value)> fitted = new Dictionary<int, (string, string)>();

        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null) return (null, null, null, null, null);
            string value = Compact(pawn, h);
            bool cut = value != h.hudStrain;
            return ("CC_Hud_Strain".Translate(), value, () => (cut ? h.hudStrain + "\n\n" : "") + RimHudUtil.HeartTip(pawn, h), null, () => MoodView.ShowMoodTab(pawn, false));
        }

        /// <summary>The line fitted to the cell, kept until the heart's full line changes (HeartView.Refresh, hourly or on a change).</summary>
        public static string Compact(Pawn pawn, PawnHeart h)
        {
            string from = h.hudStrain ?? "";
            if (fitted.TryGetValue(pawn.thingIDNumber, out var f) && ReferenceEquals(f.from, from)) return f.value;
            string value;
            try
            {
                value = HeartMeterMath.Fit(Parts(h), Width, Budget);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimHUD: the strain line fitted", e);
                value = from;
            }
            if (fitted.Count > 200) fitted.Clear();
            fitted[pawn.thingIDNumber] = (from, value);
            return value;
        }

        /// <summary>
        /// The line's parts by how much they matter: the band in a word and its value (strain's, or the good side's while strain
        /// is steady and it shows), time off under way, the top cause while strained, the good side beside strain.
        /// </summary>
        private static List<string> Parts(PawnHeart h)
        {
            var parts = new List<string>(4);
            var good = GoodHeart.Band(h);
            string Short(string band, float v) => "CC_Hud_ShortValue".Translate(band.Named("BAND"), v.ToString("0").Named("VALUE")).Resolve();
            if (h.Band == StrainBand.Steady && good != GoodBand.None)
                parts.Add(Short(("CC_Hud_GoodShort_" + good).Translate().Resolve().CapitalizeFirst(), h.good).Colorize(HeartView.GoodColor));
            else parts.Add(Short(("CC_Hud_StrainShort_" + h.Band).Translate().Resolve(), h.strain).Colorize(HeartView.BandColor(h.Band)));
            var off = HeartTimeOff.KindOf(h);
            if (off != TimeOffKind.None) parts.Add(("CC_Count_OffShort_" + off).Translate().Resolve().Colorize(UiKit.Info));
            if (h.Band >= StrainBand.Strained)
            {
                string cause = HeartView.TopCause(h);
                if (!cause.NullOrEmpty()) parts.Add(cause.Colorize(UiKit.Faint));
                if (good != GoodBand.None) parts.Add(Short(("CC_Hud_GoodShort_" + good).Translate().Resolve(), h.good).Colorize(HeartView.GoodColor));
            }
            return parts;
        }

        /// <summary>A part's width in the small font (RimHUD's small text is a point under it, so what fits here fits there).</summary>
        private static float Width(string plain)
        {
            try
            {
                var old = Text.Font;
                Text.Font = GameFont.Small;
                float w = Text.CalcSize(plain).x;
                Text.Font = old;
                return w;
            }
            catch (Exception)
            {
                return plain.Length * 7f;
            }
        }
    }

    /// <summary>
    /// What would help them most, in a few words ("a day off, days of rest"); the tooltip has each in its sentence, and a
    /// click opens their Mood tab, where the buttons are (Heart/HeartAdvice.cs). Blank below strained.
    /// </summary>
    public static class RimHud_HeartAdvice
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null || !HeartAdvice.On) return (null, null, null, null, null);
            string value = HeartAdvice.Short(pawn, h, 2);
            if (value == null) return ("CC_Hud_Advice".Translate(), "CC_Hud_None".Translate(), null, null, null);
            return ("CC_Hud_Advice".Translate(), value.CapitalizeFirst().Colorize(UiKit.Gold), () => HeartAdvice.Tip(pawn, h, 4), null, () => MoodView.ShowMoodTab(pawn, false));
        }
    }

    /// <summary>The heart's good side as a bar, like strain's: it fills as it grows, marked where it's steady, in good heart and in high spirits.</summary>
    public static class RimHud_GoodBar
    {
        private static readonly float[] Bands = { HeartMath.SteadyAt / 100f, HeartMath.GoodHeartAt / 100f, HeartMath.HighSpiritsAt / 100f };

        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null || !GoodHeart.On) return (null, null, -1f, null, null, null, null);
            return ("CC_Good_MeterLabel".Translate(), h.good.ToString("0"), Mathf.Clamp01(h.good / HeartMath.Max), Bands, () => GoodHeart.Tip(pawn, h), null,
                () => MoodView.ShowMoodTab(pawn, false));
        }
    }

    /// <summary>
    /// Strain as a bar, like RimHUD's own needs: it fills and turns from the bar colour toward red as strain builds, with
    /// marks where it becomes strained, worn down and near breaking, and at 85 where a day to themselves may come (the Mood
    /// tab's meter has the same). Its value is the number alone: RimHUD's value cell beside a bar is 40 wide by default, so
    /// "61, day off" was cut; time off under way is said in its tooltip. Hidden (fill below 0) when there's no heart.
    /// </summary>
    public static class RimHud_StrainBar
    {
        private static readonly float[] Bands = Array.ConvertAll(HeartMeterMath.StrainTicks, t => t / HeartMath.Max);

        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null) return (null, null, -1f, null, null, null, null);
            return ("CC_Hud_Strain".Translate(), h.strain.ToString("0"), Mathf.Clamp01(h.strain / HeartMath.Max), Bands, () => RimHudUtil.HeartTip(pawn, h), null,
                () => MoodView.ShowMoodTab(pawn, false));
        }
    }

    /// <summary>
    /// One of the heart's four needs as a bar (fuller is better), marked where it starts to be missed (45) and is well
    /// met (60). The tooltip says what the value means, what moved it, what follows from it and what would help.
    /// </summary>
    public static class RimHud_NeedBar
    {
        private static readonly float[] Marks = { HeartNeedMath.MissedBelow / 100f, HeartNeedMath.WellMet / 100f };

        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) For(Pawn pawn, HeartNeed need)
        {
            var h = RimHudUtil.HeartOf(pawn);
            int i = (int)need;
            if (h?.needs == null || h.needs.Length != 4) return (null, null, -1f, null, null, null, null);
            var n = h.needs[i];
            string label = HeartView.NeedLabel(need).CapitalizeFirst();
            return (label, n.value.ToString("0"), Mathf.Clamp01(n.value / 100f), Marks, () => HeartView.NeedTip(pawn, h, need), null, null);
        }
    }

    public static class RimHud_Belonging
    {
        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn) =>
            RimHud_NeedBar.For(pawn, HeartNeed.Belonging);
    }

    public static class RimHud_Purpose
    {
        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn) =>
            RimHud_NeedBar.For(pawn, HeartNeed.Purpose);
    }

    public static class RimHud_Safety
    {
        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn) =>
            RimHud_NeedBar.For(pawn, HeartNeed.Safety);
    }

    public static class RimHud_Freedom
    {
        public static (string label, string value, float fill, float[] thresholds, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn) =>
            RimHud_NeedBar.For(pawn, HeartNeed.Freedom);
    }

    /// <summary>The feelings that stand out; the tooltip lists every feeling with its causes.</summary>
    public static class RimHud_Feelings
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null) return (null, null, null, null, null);
            if (h.hudFeelings.NullOrEmpty()) return ("CC_Hud_Feelings".Translate(), "CC_Hud_None".Translate(), null, null, null);
            return ("CC_Hud_Feelings".Translate(), h.hudFeelings, () => HeartView.FeelingsTip(h), null, null);
        }
    }

    /// <summary>
    /// The heart's need they lack most, or that all are met ("lacks belonging (15)"); the tooltip shows all four, what
    /// each value means, what follows from the one they lack most and what would help.
    /// </summary>
    public static class RimHud_Needs
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h?.needs == null || h.needs.Length != 4 || h.hudNeeds == null) return (null, null, null, null, null);
            return ("CC_Hud_Needs".Translate(), h.hudNeeds, () => HeartView.NeedsTip(pawn, h), null, null);
        }
    }

    internal static class RimHudUtil
    {
        /// <summary>
        /// The heart's tooltip for RimHUD: the Mood tab's summary of it (the heart, the weight, time off, what the bands do),
        /// what would help first, then strain in full.
        /// </summary>
        public static string HeartTip(Pawn pawn, PawnHeart h)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(MoodGlance.HeartLine(pawn, h));
            string off = HeartTimeOff.Line(pawn, h, Find.TickManager.TicksGame);
            if (off != null) sb.Append("\n").Append(off);
            string advice = HeartAdvice.Tip(pawn, h, 3);
            if (advice != null) sb.Append("\n\n").Append(advice.Colorize(UiKit.Gold));
            sb.Append("\n\n").Append(HeartView.StrainTip(pawn, h));
            sb.Append("\n\n").Append("CC_Hud_OpensMood".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        /// <summary>The colonist's heart, once it has been worked out, while the feature is on; else null.</summary>
        public static PawnHeart HeartOf(Pawn pawn)
        {
            if (pawn == null || Current.ProgramState != ProgramState.Playing) return null;
            var h = Heart.ForView(pawn);
            return h != null && h.evaluated ? h : null;
        }

        /// <summary>Rimfolk's game data, when the pawn is one of the colony's people; else null.</summary>
        public static CouncilGameComponent For(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonist || !People.HasInnerLife(pawn)) return null;
            return Current.ProgramState == ProgramState.Playing ? CouncilGameComponent.Instance : null;
        }

        public static string WishTip(Wish w)
        {
            try
            {
                string tip = w.Label;
                if (!w.quote.NullOrEmpty()) tip += "\n\n\"" + w.quote + "\"";
                string when = w.Worker.FulfilledWhen(w);
                if (!when.NullOrEmpty()) tip += "\n\n" + "CC_FulfilledWhen".Translate() + ": " + when;
                tip += "\n" + "CC_Expires".Translate() + ": " + TextUtil.DaysString(w.TicksLeft);
                return tip;
            }
            catch
            {
                return w.Label;
            }
        }
    }
}
