using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One panel's options, from its bar's gear, its menu, or the buttons under "On-screen panels" in Rimfolk's
    /// settings: shown or not, compact or full, the order and whom it lists (the mood board), the range (the trend),
    /// which rows or columns show, how many rows before "and N more", its scale and background. Changes show at once
    /// and are saved when the window closes.
    /// </summary>
    public class Dialog_HudPanel : Window
    {
        private readonly string id;
        private Vector2 scroll;
        private float viewHeight = 600f;

        public static void Open(string id)
        {
            try
            {
                Find.WindowStack.TryRemove(typeof(Dialog_HudPanel), false);
                Find.WindowStack.Add(new Dialog_HudPanel(id));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panel options", e);
            }
        }

        public Dialog_HudPanel(string id)
        {
            this.id = id;
            doCloseX = true;
            closeOnClickedOutside = true;
            closeOnCancel = true;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            draggable = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(380f, Mathf.Min(640f, UI.screenHeight - 40f));

        public override void PostClose()
        {
            base.PostClose();
            HudLayout.Save();
        }

        private static string[] PartsOf(string id)
        {
            switch (id)
            {
                case HudLayout.Moods: return HudMoodBoard.Parts;
                case HudLayout.Trend: return HudMoodTrend.Parts;
                case HudLayout.Vitals: return HudVitals.Parts;
                case HudLayout.Council: return HudCouncil.Parts;
                default: return new string[0];
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var st = HudLayout.State(id);
            var before = st.Copy();
            UiKit.LabelFit(new Rect(inRect.x, inRect.y, inRect.width - 30f, 32f), "CC_Pnl_OptionsTitle".Translate(HudLayout.Label(id)), Color.white, GameFont.Medium);
            var outRect = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f - 40f);
            var view = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            var l = new Listing_Standard();
            l.Begin(view);
            try
            {
                l.CheckboxLabeled("CC_Pnl_Opt_Show".Translate(), ref st.on);
                l.CheckboxLabeled("CC_Pnl_Opt_Compact".Translate(), ref st.compact, ("CC_Pnl_Opt_Compact_" + id).Translate());
                if (id == HudLayout.Moods)
                {
                    l.GapLine();
                    l.Label("CC_Pnl_Opt_Order".Translate());
                    if (l.RadioButton("CC_Pnl_SortRisk".Translate(), st.sort == HudSort.Risk, 12f)) st.sort = HudSort.Risk;
                    if (l.RadioButton("CC_Pnl_SortBar".Translate(), st.sort == HudSort.Bar, 12f)) st.sort = HudSort.Bar;
                    l.Label("CC_Pnl_Opt_Who".Translate());
                    foreach (HudWho w in Enum.GetValues(typeof(HudWho)))
                        if (l.RadioButton(("CC_Pnl_Who_" + w).Translate(), st.who == w, 12f))
                            st.who = w;
                }
                if (id == HudLayout.Trend)
                {
                    l.GapLine();
                    l.Label("CC_Pnl_Opt_Range".Translate());
                    if (l.RadioButton("CC_Pnl_TrendDays".Translate(3).Resolve(), st.days != 7, 12f)) st.days = 3;
                    if (l.RadioButton("CC_Pnl_TrendDays".Translate(7).Resolve(), st.days == 7, 12f)) st.days = 7;
                }
                l.GapLine();
                l.Label("CC_Pnl_Opt_Parts".Translate());
                foreach (var part in PartsOf(id))
                {
                    bool on = st.Shows(part);
                    l.CheckboxLabeled(("CC_Pnl_Part_" + part).Translate(), ref on, 12f);
                    if (on != st.Shows(part)) st.Toggle(part);
                }
                l.GapLine();
                if (id != HudLayout.Trend)
                {
                    string rows = st.maxRows <= 0 ? "CC_Pnl_Opt_RowsAll".Translate().ToString() : st.maxRows.ToString();
                    st.maxRows = Mathf.RoundToInt(l.SliderLabeled("CC_Pnl_Opt_Rows".Translate(rows), st.maxRows, 0f, 20f, 0.5f, "CC_Pnl_Opt_RowsTip".Translate()));
                }
                st.scale = HudMath.ClampScale(l.SliderLabeled("CC_Pnl_Opt_Scale".Translate(st.scale.ToStringPercent()), st.scale, HudMath.MinScale, HudMath.MaxScale, 0.5f));
                st.opacity = HudMath.ClampOpacity(l.SliderLabeled("CC_Pnl_Opt_Opacity".Translate(st.opacity.ToStringPercent()), st.opacity, HudMath.MinOpacity,
                    HudMath.MaxOpacity, 0.5f));
                l.Gap(6f);
                if (l.ButtonText("CC_Pnl_Opt_Place".Translate()))
                {
                    var d = HudLayout.Default(id);
                    st.x = d.x;
                    st.y = d.y;
                    st.right = d.right;
                    st.bottom = d.bottom;
                }
                if (l.ButtonText("CC_Pnl_Opt_Reset".Translate()))
                {
                    var d = HudLayout.Default(id);
                    d.on = st.on;
                    int i = ColonyCouncilMod.Settings.hudPanels.IndexOf(st);
                    if (i >= 0) ColonyCouncilMod.Settings.hudPanels[i] = d;
                }
                viewHeight = l.CurHeight + 12f;
            }
            finally
            {
                l.End();
                Widgets.EndScrollView();
            }
            if (st.on && !before.on) ColonyCouncilMod.Settings.hudShown = true;
            if (Changed(before, st)) HudManager.Window(id)?.MarkDirty();
            // Rimfolk's settings, unless this was opened from there.
            if (!Find.WindowStack.IsOpen(typeof(RimWorld.Dialog_ModSettings)) &&
                Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 32f, 200f, 32f), "CC_Pnl_AllOptions".Translate()))
                HudManager.OpenSettings();
        }

        private static bool Changed(HudPanelState a, HudPanelState b) =>
            a.compact != b.compact || a.sort != b.sort || a.who != b.who || a.days != b.days || a.maxRows != b.maxRows || Math.Abs(a.scale - b.scale) > 0.001f ||
            (a.hidden?.Count ?? 0) != (b.hidden?.Count ?? 0);
    }
}
