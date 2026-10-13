using System;
using RimWorld;
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
            var office = comp.offices.OfficeOf(pawn);
            if (office == null) return ("CC_Hud_Office".Translate(), "CC_Hud_None".Translate(), null, null, null);
            return ("CC_Hud_Office".Translate(), office.LabelCap, () => office.description, null, null);
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

    /// <summary>Strain and its band; the tooltip says what built it and what would help (Heart/).</summary>
    public static class RimHud_Strain
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h == null) return (null, null, null, null, null);
            return ("CC_Hud_Strain".Translate(), h.hudStrain, () => HeartView.StrainTip(pawn, h), null, null);
        }
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

    /// <summary>The heart's need they lack most, or that all are met; the tooltip shows all four.</summary>
    public static class RimHud_Needs
    {
        public static (string label, string value, Func<string> tooltip, Action onHover, Action onClick) GetParameters(Pawn pawn)
        {
            var h = RimHudUtil.HeartOf(pawn);
            if (h?.needs == null || h.needs.Length != 4 || h.hudNeeds == null) return (null, null, null, null, null);
            return ("CC_Hud_Needs".Translate(), h.hudNeeds, () => HeartView.NeedsTip(h), null, null);
        }
    }

    internal static class RimHudUtil
    {
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
