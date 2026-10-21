using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One panel's options, from its bar's gear, its menu, or the buttons under "On-screen panels" in Rimfolk's
    /// settings: shown or not, compact or full, the order and whom it lists (the mood board, health, work and idle), the
    /// health panel's rows (a summary or a line for each condition), the order, the game's own list and the alerts
    /// hidden in the panel (the alerts), the style, order, tiles in a row, pinned items and the game's own list (the
    /// inventory), the range (the trend), which rows or columns show, how many rows before "and N more", its scale and
    /// background. Changes show at once and are saved when the window closes.
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
                case HudLayout.Alerts: return HudAlerts.Parts;
                case HudLayout.Health: return HudHealth.Parts;
                case HudLayout.Work: return HudWork.Parts;
                case HudLayout.Inventory: return HudInventory.Parts;
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
                l.CheckboxLabeled(HudKit.T("CC_Pnl_Opt_Show"), ref st.on);
                l.CheckboxLabeled(HudKit.T("CC_Pnl_Opt_Compact"), ref st.compact, HudKit.T("CC_Pnl_Opt_Compact_" + id));
                if (id == HudLayout.Moods || id == HudLayout.Health || id == HudLayout.Work)
                {
                    l.GapLine();
                    l.Label(HudKit.T("CC_Pnl_Opt_Order"));
                    if (l.RadioButton(HudKit.T(id == HudLayout.Moods ? "CC_Pnl_SortRisk" : "CC_Pnl_SortUrgent"), st.sort != HudSort.Bar, 12f)) st.sort = HudSort.Risk;
                    if (l.RadioButton(HudKit.T("CC_Pnl_SortBar"), st.sort == HudSort.Bar, 12f)) st.sort = HudSort.Bar;
                    l.Label(HudKit.T("CC_Pnl_Opt_Who"));
                    foreach (HudWho w in Enum.GetValues(typeof(HudWho)))
                    {
                        // Prisoners don't work for the colony.
                        if (id == HudLayout.Work && w == HudWho.WithPrisoners) continue;
                        if (l.RadioButton(HudKit.T("CC_Pnl_Who_" + w), st.who == w, 12f)) st.who = w;
                    }
                }
                if (id == HudLayout.Health)
                {
                    l.Label(HudKit.T("CC_Pnl_H_Opt_Rows"));
                    bool lines = st.style == HudHealth.StyleLines;
                    if (l.RadioButton(HudKit.T("CC_Pnl_H_Opt_Summary"), !lines, 12f, HudKit.T("CC_Pnl_H_Opt_Summary_Tip"))) st.style = HudHealth.StyleSummary;
                    if (l.RadioButton(HudKit.T("CC_Pnl_H_Opt_Lines"), lines, 12f, HudKit.T("CC_Pnl_H_Opt_Lines_Tip"))) st.style = HudHealth.StyleLines;
                }
                if (id == HudLayout.Alerts)
                {
                    l.GapLine();
                    l.Label(HudKit.T("CC_Pnl_Opt_Order"));
                    if (l.RadioButton(HudKit.T("CC_Pnl_SortPriority"), st.sort != HudSort.Newest, 12f)) st.sort = HudSort.Risk;
                    if (l.RadioButton(HudKit.T("CC_Pnl_SortNewest"), st.sort == HudSort.Newest, 12f)) st.sort = HudSort.Newest;
                    l.Gap(4f);
                    var s = ColonyCouncilMod.Settings;
                    l.CheckboxLabeled(HudKit.T("CC_Settings_HudHideGameAlerts"), ref s.hudHideGameAlerts, HudKit.T("CC_Settings_HudHideGameAlerts_Tip"));
                    HiddenAlerts(l, st);
                }
                if (id == HudLayout.Inventory) Inventory(l, st);
                if (id == HudLayout.Trend)
                {
                    l.GapLine();
                    l.Label(HudKit.T("CC_Pnl_Opt_Range"));
                    if (l.RadioButton("CC_Pnl_TrendDays".Translate(3).Resolve(), st.days != 7, 12f)) st.days = 3;
                    if (l.RadioButton("CC_Pnl_TrendDays".Translate(7).Resolve(), st.days == 7, 12f)) st.days = 7;
                }
                l.GapLine();
                l.Label(HudKit.T("CC_Pnl_Opt_Parts"));
                foreach (var part in PartsOf(id))
                {
                    bool on = st.Shows(part);
                    l.CheckboxLabeled(HudKit.T("CC_Pnl_Part_" + part), ref on, 12f);
                    if (on != st.Shows(part)) st.Toggle(part);
                }
                l.GapLine();
                if (id != HudLayout.Trend)
                {
                    string rows = st.maxRows <= 0 ? HudKit.T("CC_Pnl_Opt_RowsAll").ToString() : st.maxRows.ToString();
                    st.maxRows = Mathf.RoundToInt(l.SliderLabeled("CC_Pnl_Opt_Rows".Translate(rows), st.maxRows, 0f, 20f, 0.5f, HudKit.T("CC_Pnl_Opt_RowsTip")));
                }
                st.scale = HudMath.ClampScale(l.SliderLabeled("CC_Pnl_Opt_Scale".Translate(st.scale.ToStringPercent()), st.scale, HudMath.MinScale, HudMath.MaxScale, 0.5f));
                st.opacity = HudMath.ClampOpacity(l.SliderLabeled("CC_Pnl_Opt_Opacity".Translate(st.opacity.ToStringPercent()), st.opacity, HudMath.MinOpacity,
                    HudMath.MaxOpacity, 0.5f));
                l.Gap(6f);
                if (l.ButtonText(HudKit.T("CC_Pnl_Opt_Place")))
                {
                    var d = HudLayout.Default(id);
                    st.x = d.x;
                    st.y = d.y;
                    st.right = d.right;
                    st.bottom = d.bottom;
                }
                if (l.ButtonText(HudKit.T("CC_Pnl_Opt_Reset")))
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
                Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 32f, 200f, 32f), HudKit.T("CC_Pnl_AllOptions")))
                HudManager.OpenSettings();
        }

        private static bool Changed(HudPanelState a, HudPanelState b) =>
            a.compact != b.compact || a.sort != b.sort || a.who != b.who || a.days != b.days || a.maxRows != b.maxRows || Math.Abs(a.scale - b.scale) > 0.001f ||
            (a.hidden?.Count ?? 0) != (b.hidden?.Count ?? 0) || a.style != b.style || a.columns != b.columns || (a.pins?.Count ?? 0) != (b.pins?.Count ?? 0) ||
            (a.folds?.Count ?? 0) != (b.folds?.Count ?? 0) || (a.alertsHidden?.Count ?? 0) != (b.alertsHidden?.Count ?? 0) ||
            (a.alertsShown?.Count ?? 0) != (b.alertsShown?.Count ?? 0);

        /// <summary>The alerts hidden in the panel, each with a button to show it again, and one to show them all.</summary>
        private static void HiddenAlerts(Listing_Standard l, HudPanelState st)
        {
            l.Gap(6f);
            var hidden = HudAlerts.HiddenNow(st);
            l.Label(HudKit.T(hidden.Count == 0 ? "CC_Pnl_A_NoneHidden" : "CC_Pnl_A_Hidden"));
            foreach (var cls in hidden)
            {
                var row = l.GetRect(26f);
                string label = HudAlerts.LabelOf(cls);
                if (cls == HudAlerts.WishesAlert && HudAlerts.HiddenByDefault(cls)) label += " " + HudKit.T("CC_Pnl_A_InCouncil");
                var b = new Rect(row.xMax - 90f, row.y + 1f, 90f, 24f);
                UiKit.LabelFit(new Rect(row.x + 12f, row.y, row.width - 106f, row.height), label, Color.white, GameFont.Small);
                if (Widgets.ButtonText(b, HudKit.T("CC_Pnl_A_Unhide"))) HudAlerts.Unhide(st, cls);
            }
            if (hidden.Count > 1 && l.ButtonText(HudKit.T("CC_Pnl_A_ShowAll"))) HudAlerts.ShowAll(st);
        }

        /// <summary>The inventory's style, order, tiles in a row, the game's own list, and the pinned items.</summary>
        private static void Inventory(Listing_Standard l, HudPanelState st)
        {
            l.GapLine();
            l.Label(HudKit.T("CC_Pnl_I_Opt_Style"));
            string style = HudInventory.Styles.Contains(st.style) ? st.style : HudInventory.StyleClassic;
            foreach (var sty in HudInventory.Styles)
                if (l.RadioButton(HudKit.T("CC_Pnl_I_Style_" + sty), style == sty, 12f, HudKit.T("CC_Pnl_I_Style_" + sty + "_Tip")))
                    st.style = sty;
            if (style == HudInventory.StyleGrid)
                st.columns = Mathf.RoundToInt(l.SliderLabeled("CC_Pnl_I_Opt_Columns".Translate(st.columns), st.columns, 2f, 24f, 0.5f));
            l.Label(HudKit.T("CC_Pnl_Opt_Order"));
            foreach (var sort in HudInventory.Sorts)
                if (l.RadioButton(HudKit.T("CC_Pnl_I_Sort_" + sort), st.sort == sort || (sort == HudSort.Category && !HudInventory.Sorts.Contains(st.sort)), 12f))
                    st.sort = sort;
            l.Gap(4f);
            var s = ColonyCouncilMod.Settings;
            l.CheckboxLabeled(HudKit.T("CC_Settings_HudHideGameResources"), ref s.hudHideGameResources, HudKit.T("CC_Settings_HudHideGameResources_Tip"));
            l.Gap(6f);
            l.Label(HudKit.T(st.pins.Count == 0 ? "CC_Pnl_I_NonePinned" : "CC_Pnl_I_PinnedList"));
            foreach (var name in st.pins.ToList())
            {
                var row = l.GetRect(26f);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                var b = new Rect(row.xMax - 90f, row.y + 1f, 90f, 24f);
                if (def != null) Widgets.ThingIcon(new Rect(row.x + 12f, row.y + 2f, 22f, 22f), def);
                UiKit.LabelFit(new Rect(row.x + 40f, row.y, row.width - 134f, row.height), def?.LabelCap ?? name, Color.white, GameFont.Small);
                if (Widgets.ButtonText(b, HudKit.T("CC_Pnl_I_UnpinShort"))) st.pins.Remove(name);
            }
            if (st.folds.Count > 0 && l.ButtonText(HudKit.T("CC_Pnl_I_UnfoldAll"))) st.folds.Clear();
        }
    }
}
