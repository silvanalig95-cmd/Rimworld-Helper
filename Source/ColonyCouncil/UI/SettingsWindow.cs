using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's mod settings window: a search box at the top, the list of pages on the left with the presets under it,
    /// and the page on the right. Every row comes from <see cref="SettingsCatalog"/>, so a setting is drawn in one place
    /// and found by the search; each shows a grey line of what you'll notice with it on, and grays out with the reason
    /// while what it needs is switched off. Everything scrolls, and the game's dialog is shrunk to fit a small screen.
    /// </summary>
    public static class SettingsWindow
    {
        private const float LineH = 24f;
        private const float ButtonH = 28f;
        private const float RowGap = 8f;
        private const float IndentW = 20f;
        private const float SegmentGap = 4f;

        private static SettingsPage page = SettingsPage.Council;
        private static readonly Dictionary<SettingsPage, Vector2> scrolls = new Dictionary<SettingsPage, Vector2>();
        private static readonly Dictionary<SettingsPage, float> heights = new Dictionary<SettingsPage, float>();
        private static Vector2 searchScroll, leftScroll;
        private static float searchHeight;
        private static QuickSearchWidget search;
        private static List<SettingEntry> matches = new List<SettingEntry>();
        private static string matchedFor;
        private static SettingEntry jumpTo;
        private static readonly Dictionary<SettingsPage, int> changed = new Dictionary<SettingsPage, int>();
        private static int countedFrame = -1000;

        /// <summary>A row's text in the current language, worked out once.</summary>
        private sealed class Texts
        {
            public string label;
            public bool valueInLabel;
            public string tip;
            public string note;
            public string[] options, optionTips, optionNotes;
            public string haystack;
        }

        private static Dictionary<SettingEntry, Texts> texts = new Dictionary<SettingEntry, Texts>();
        private static string textsLanguage;
        private static readonly Dictionary<(string, int), float> noteHeights = new Dictionary<(string, int), float>();

        public static void Draw(CouncilSettings s, Rect inRect)
        {
            SettingsAccess.Verify();
            FitDialogToScreen();
            search ??= new QuickSearchWidget();
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                float leftW = SettingsLayoutMath.LeftWidth(inRect.width);
                float gap = SettingsLayoutMath.Gap;

                // The search box, and beside it what the page is for (or how many settings match).
                var searchRect = new Rect(inRect.x, inRect.y + (SettingsLayoutMath.TopBar - QuickSearchWidget.WidgetHeight) / 2f, leftW, QuickSearchWidget.WidgetHeight);
                search.OnGUI(searchRect, () => matchedFor = null);
                bool searching = search.filter.Active;
                if (searching) UpdateMatches();
                string info = !searching ? SettingsCatalog.PageDescKey(page).Translate().Resolve()
                    : matches.Count == 0 ? "CC_SetUI_NoResults".Translate(search.filter.Text).Resolve()
                    : "CC_SetUI_Results".Translate(matches.Count).Resolve();
                UiKit.LabelFit(new Rect(searchRect.xMax + gap * 1.5f, inRect.y, inRect.width - leftW - gap * 1.5f, SettingsLayoutMath.TopBar), info, UiKit.Faint);

                float bodyY = inRect.y + SettingsLayoutMath.TopBar + gap;
                var left = new Rect(inRect.x, bodyY, leftW, inRect.yMax - bodyY);
                var right = new Rect(left.xMax + gap, bodyY, inRect.xMax - left.xMax - gap, left.height);
                LeftColumn(s, left, searching);
                Widgets.DrawMenuSection(right);
                var inner = right.ContractedBy(gap);
                if (searching) SearchView(s, inner);
                else PageView(s, inner);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("settings window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------- The page list and presets ----------------

        private static void LeftColumn(CouncilSettings s, Rect rect, bool searching)
        {
            var presets = SettingsCatalog.StylePresets.ToList();
            float need = SettingsLayoutMath.LeftColumnHeight(SettingsCatalog.Pages.Length, presets.Count);
            bool scrolling = need > rect.height;
            Vector2 origin = rect.position;
            float width = rect.width;
            if (scrolling)
            {
                width -= SettingsLayoutMath.ScrollBar;
                Widgets.BeginScrollView(rect, ref leftScroll, new Rect(0f, 0f, width, need));
                origin = Vector2.zero;
            }
            try
            {
                CountChanged(s);
                float y = origin.y;
                foreach (var p in SettingsCatalog.Pages)
                {
                    PageButton(new Rect(origin.x, y, width, SettingsLayoutMath.PageRow), p, !searching && p == page);
                    y += SettingsLayoutMath.PageRow;
                }
                y += SettingsLayoutMath.Gap * 2f;
                UiKit.LabelFit(new Rect(origin.x + 2f, y, width - 2f, SettingsLayoutMath.PresetHeader - 4f), "CC_SetPreset_Header".Translate(), UiKit.HeaderColor);
                TooltipHandler.TipRegion(new Rect(origin.x, y, width, SettingsLayoutMath.PresetHeader), "CC_SetPreset_Header_Tip".Translate());
                UiKit.Divider(origin.x, y + SettingsLayoutMath.PresetHeader - 3f, width);
                y += SettingsLayoutMath.PresetHeader;
                foreach (var p in presets)
                {
                    var r = new Rect(origin.x, y, width, SettingsLayoutMath.PresetRow);
                    TooltipHandler.TipRegion(r, p.tip.Translate());
                    if (Widgets.ButtonText(r, UiKit.Fit(p.label.Translate(), width - 12f))) SettingsPresets.Confirm(s, p);
                    y += SettingsLayoutMath.PresetRow + 4f;
                }
            }
            finally
            {
                if (scrolling) Widgets.EndScrollView();
            }
        }

        private static void PageButton(Rect r, SettingsPage p, bool active)
        {
            if (active)
            {
                Widgets.DrawBoxSolid(r, UiKit.CardSelected);
                Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), UiKit.Gold);
            }
            Widgets.DrawHighlightIfMouseover(r);
            UiKit.Icon(new Rect(r.x + 9f, r.y + (r.height - 18f) / 2f, 18f, 18f), PageIcon(p), active ? UiKit.Gold : UiKit.Faint);
            float countW = 0f;
            changed.TryGetValue(p, out int n);
            if (n > 0)
            {
                string count = n.ToString();
                Text.Font = GameFont.Tiny;
                countW = Text.CalcSize(count).x + 10f;
                Text.Font = GameFont.Small;
                var cr = new Rect(r.xMax - countW - 4f, r.y + (r.height - 18f) / 2f, countW, 18f);
                Widgets.DrawBoxSolid(cr, UiKit.WithAlpha(UiKit.Gold, 0.16f));
                UiKit.LabelFit(cr, count, UiKit.Gold, GameFont.Tiny, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(cr, "CC_SetUI_Changed_Tip".Translate(n));
                countW += 8f;
            }
            UiKit.LabelFit(new Rect(r.x + 33f, r.y, r.width - 36f - countW, r.height), SettingsCatalog.PageKey(p).Translate(), active ? Color.white : UiKit.Faint);
            if (Widgets.ButtonInvisible(r))
            {
                page = p;
                search.Reset();
                matchedFor = null;
                SoundDefOf.RowTabSelect.PlayOneShotOnCamera();
            }
        }

        private static Texture2D PageIcon(SettingsPage p)
        {
            switch (p)
            {
                case SettingsPage.Council: return Icons.Council;
                case SettingsPage.People: return Icons.Moods;
                case SettingsPage.HeartHealth: return Icons.Heart;
                case SettingsPage.Events: return Icons.World;
                case SettingsPage.Building: return Icons.Rooms;
                case SettingsPage.Interface: return Icons.CatMods;
                default: return Icons.CatManagement;
            }
        }

        /// <summary>How many rows on each page differ from the defaults, counted twice a second rather than every frame.</summary>
        private static void CountChanged(CouncilSettings s)
        {
            if (Time.frameCount - countedFrame < 30 && Time.frameCount >= countedFrame) return;
            countedFrame = Time.frameCount;
            foreach (var p in SettingsCatalog.Pages)
                changed[p] = SettingsCatalog.On(p).Count(e => SettingsCatalog.FieldsOf(e).Any(f => !SettingsAccess.IsDefault(s, f)));
        }

        // ---------------- A page, and the search results ----------------

        private static void PageView(CouncilSettings s, Rect outRect)
        {
            scrolls.TryGetValue(page, out var scroll);
            heights.TryGetValue(page, out float height);
            var view = new Rect(0f, 0f, outRect.width - SettingsLayoutMath.ScrollBar, Mathf.Max(height, 10f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            try
            {
                float top = scroll.y - 40f, bottom = scroll.y + outRect.height + 40f;
                string section = null;
                foreach (var e in SettingsCatalog.On(page))
                {
                    if (e.section != section)
                    {
                        section = e.section;
                        if (jumpTo != null && jumpTo.page == page && jumpTo.section == section)
                        {
                            scroll.y = y;
                            jumpTo = null;
                        }
                        y += SectionHeader(0f, y, view.width, SectionLabel(section), SettingsCatalog.SectionNote(section), null, y == 0f);
                    }
                    y += DrawEntry(s, e, 0f, y, view.width, top, bottom);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            scrolls[page] = scroll;
            heights[page] = y + 10f;
        }

        private static void SearchView(CouncilSettings s, Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - SettingsLayoutMath.ScrollBar, Mathf.Max(searchHeight, 10f));
            Widgets.BeginScrollView(outRect, ref searchScroll, view);
            float y = 0f;
            try
            {
                float top = searchScroll.y - 40f, bottom = searchScroll.y + outRect.height + 40f;
                if (matches.Count == 0)
                    y += UiKit.Paragraph(4f, 6f, view.width - 8f, "CC_SetUI_NoResults_Hint".Translate(), UiKit.Faint) + 12f;
                SettingsPage? lastPage = null;
                string lastSection = null;
                foreach (var e in matches)
                {
                    if (e.page != lastPage || e.section != lastSection)
                    {
                        lastPage = e.page;
                        lastSection = e.section;
                        // The heading says where the setting lives; clicking it opens that page there.
                        float h = SectionHeader(0f, y, view.width, SettingsCatalog.PageKey(e.page).Translate() + "  ›  " + SectionLabel(e.section), null,
                            PageIcon(e.page), y == 0f, "CC_SetUI_GoToPage".Translate());
                        var hr = new Rect(0f, y, view.width, h);
                        Widgets.DrawHighlightIfMouseover(hr);
                        TooltipHandler.TipRegion(hr, "CC_SetUI_GoToPage_Tip".Translate());
                        if (Widgets.ButtonInvisible(hr))
                        {
                            page = e.page;
                            jumpTo = e;
                            search.Reset();
                            matchedFor = null;
                        }
                        y += h;
                    }
                    y += DrawEntry(s, e, 0f, y, view.width, top, bottom);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            searchHeight = y + 10f;
        }

        /// <summary>Every word typed must turn up in the setting's label, tooltip, notes, options or heading.</summary>
        private static void UpdateMatches()
        {
            string text = search.filter.Text ?? "";
            if (matchedFor == text) return;
            matchedFor = text;
            searchScroll = Vector2.zero;
            var words = text.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            matches = SettingsCatalog.All.Where(e => words.All(w => TextsOf(e).haystack.Contains(w))).ToList();
            search.noResultsMatched = matches.Count == 0;
        }

        private static float SectionHeader(float x, float y, float width, string text, string noteKey, Texture2D icon, bool first, string right = null)
        {
            float top = first ? 0f : 6f;
            float h = top + UiKit.SectionTitle(x, y + top, width, text, icon, null, right);
            if (noteKey != null)
            {
                string note = noteKey.Translate();
                h += UiKit.Paragraph(x, y + h - 2f, width, note, UiKit.Faint, GameFont.Tiny) + 4f;
            }
            return h;
        }

        private static string SectionLabel(string key) => key.NullOrEmpty() ? "" : key.Translate().RawText.TrimEnd(' ', ':');

        // ---------------- One row ----------------

        /// <summary>Draws a row (when it's in view) and returns its height: the control, the note under it and a gap.</summary>
        private static float DrawEntry(CouncilSettings s, SettingEntry e, float x, float y, float width, float visibleTop, float visibleBottom)
        {
            if (e.Indented)
            {
                x += IndentW;
                width -= IndentW;
            }
            var t = TextsOf(e);
            bool on = SettingsAccess.Met(s, e);
            float controlH = ControlHeight(e, t, width);
            string note = on ? NoteText(s, e, t) : WhyOff(s, e);
            float noteH = note.NullOrEmpty() ? 0f : NoteHeight(note, width);
            float h = controlH + noteH + RowGap;
            if (y + h < visibleTop || y > visibleBottom) return h;

            var row = new Rect(x - 4f, y - 2f, width + 8f, controlH + noteH + 4f);
            if (on && e.kind != SettingKind.Action) Widgets.DrawHighlightIfMouseover(row);
            if (e.Indented) Widgets.DrawBoxSolid(new Rect(x - IndentW + 8f, y + 3f, 1f, controlH + noteH - 4f), UiKit.DividerColor);

            switch (e.kind)
            {
                case SettingKind.Toggle:
                    ToggleRow(s, e, t, new Rect(x, y, width, LineH), on);
                    break;
                case SettingKind.Slider:
                    SliderRow(s, e, t, x, y, width, on);
                    break;
                case SettingKind.Choice:
                    ChoiceRow(s, e, t, x, y, width, on);
                    break;
                case SettingKind.Dropdown:
                    DropdownRow(s, e, t, x, y, width, on);
                    break;
                case SettingKind.Action:
                    ActionRow(s, e, t, x, y, width, on);
                    break;
            }
            if (noteH > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = on ? UiKit.Faint : UiKit.Dim;
                Widgets.Label(new Rect(x, y + controlH, width, noteH + 2f), note);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            // Choices and actions carry their tooltips on their parts.
            if (e.kind != SettingKind.Choice && e.kind != SettingKind.Action)
            {
                string tip = Tip(e, t);
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(x, y, width, controlH + noteH), tip);
            }
            return h;
        }

        private static float ControlHeight(SettingEntry e, Texts t, float width)
        {
            switch (e.kind)
            {
                case SettingKind.Slider: return SettingsLayoutMath.SliderBeside(width) ? LineH : LineH * 2f;
                case SettingKind.Choice: return ChoiceInline(t, width) ? ButtonH + 2f : LineH + ButtonH + 4f;
                case SettingKind.Dropdown: return DropdownBeside(width) ? ButtonH + 2f : LineH + ButtonH + 4f;
                case SettingKind.Action: return ButtonH + 4f;
                default: return LineH;
            }
        }

        private static void ToggleRow(CouncilSettings s, SettingEntry e, Texts t, Rect r, bool on)
        {
            if (!(SettingsAccess.Get(s, e.id) is bool value)) return;
            bool was = value;
            string label = UiKit.Fit(t.label, r.width - 32f);
            if (!on) GUI.color = UiKit.Dim;
            Widgets.CheckboxLabeled(r, label, ref value, !on);
            GUI.color = Color.white;
            if (label.Length != t.label.Length) TooltipHandler.TipRegion(r, t.label);
            if (value != was) Changed(s, e, value);
        }

        private static void SliderRow(CouncilSettings s, SettingEntry e, Texts t, float x, float y, float width, bool on)
        {
            object raw = SettingsAccess.Get(s, e.id);
            float value = raw is int i ? i : raw is float f ? f : 0f;
            string valueText = SettingsAccess.ValueText(e, raw);
            string label = t.valueInLabel ? e.label.Translate(valueText).Resolve() : t.label + ": " + valueText;
            bool beside = SettingsLayoutMath.SliderBeside(width);
            float labelW = beside ? Mathf.Round(width * 0.55f) : width;
            UiKit.LabelFit(new Rect(x, y, labelW - 8f, LineH), label, on ? Color.white : UiKit.Dim);
            var sr = beside ? new Rect(x + labelW, y + 2f, width - labelW, LineH - 4f) : new Rect(x + 6f, y + LineH + 2f, width - 12f, LineH - 4f);
            if (!on)
            {
                Widgets.DrawBoxSolid(new Rect(sr.x, sr.center.y - 1f, sr.width, 2f), UiKit.MeterBack);
                return;
            }
            float next = Snap(Widgets.HorizontalSlider(sr, value, e.min, e.max, false, null, null, null, e.step), e);
            if (raw is int whole)
            {
                int n = Mathf.RoundToInt(next);
                if (n != whole) Changed(s, e, n);
            }
            else if (Mathf.Abs(next - value) > 0.0001f) Changed(s, e, next);
        }

        /// <summary>A slider's value on its steps (0.05 steps as twentieths, as the settings always rounded them), within its range.</summary>
        private static float Snap(float v, SettingEntry e)
        {
            if (e.step <= 0f) return Mathf.Clamp(v, e.min, e.max);
            float snapped = e.step < 1f ? Mathf.Round(v * Mathf.Round(1f / e.step)) / Mathf.Round(1f / e.step) : Mathf.Round(v / e.step) * e.step;
            return Mathf.Clamp(snapped, e.min, e.max);
        }

        private static float ChoiceLabelWidth(Texts t, float width)
        {
            Text.Font = GameFont.Small;
            return Mathf.Min(Text.CalcSize(t.label).x + 16f, width * 0.4f);
        }

        /// <summary>The options' natural widths together: each its text and a margin.</summary>
        private static float OptionsWidth(Texts t)
        {
            Text.Font = GameFont.Small;
            return t.options.Sum(o => Text.CalcSize(o).x + 20f) + SegmentGap * (t.options.Length - 1);
        }

        /// <summary>The options on the same line as the label when they fit there at their natural widths.</summary>
        private static bool ChoiceInline(Texts t, float width) => ChoiceLabelWidth(t, width) + OptionsWidth(t) <= width;

        /// <summary>Each option as wide as its text, sharing what's left over; all the same width when they don't fit.</summary>
        private static float[] SegmentWidths(Texts t, float available)
        {
            int n = t.options.Length;
            var widths = new float[n];
            float free = available - OptionsWidth(t);
            for (int i = 0; i < n; i++)
                widths[i] = free >= 0f ? Text.CalcSize(t.options[i]).x + 20f + free / n : SettingsLayoutMath.ButtonWidth(available, n, SegmentGap);
            return widths;
        }

        private static void ChoiceRow(CouncilSettings s, SettingEntry e, Texts t, float x, float y, float width, bool on)
        {
            string current = CurrentOption(s, e);
            bool inline = ChoiceInline(t, width);
            float labelW = inline ? ChoiceLabelWidth(t, width) : width;
            var lr = new Rect(x, y + (inline ? 2f : 0f), labelW, LineH);
            UiKit.LabelFit(lr, t.label, on ? Color.white : UiKit.Dim);
            string tip = Tip(e, t);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(lr, tip);
            float sx = inline ? x + labelW : x, sy = inline ? y : y + LineH + 2f, sw = inline ? width - labelW : width;
            var widths = SegmentWidths(t, sw);
            for (int i = 0; i < e.options.Length; i++)
            {
                var b = new Rect(sx, sy, widths[i], ButtonH);
                sx += widths[i] + SegmentGap;
                bool chosen = e.options[i] == current;
                if (Segment(b, t.options[i], chosen, on, t.optionTips[i]) && !chosen) Choose(s, e, e.options[i]);
            }
        }

        /// <summary>One option of a choice: a flat box, gold-edged and gold-lettered when chosen.</summary>
        private static bool Segment(Rect r, string label, bool chosen, bool enabled, string tip)
        {
            UiKit.Card(r, null, chosen, enabled);
            if (chosen) Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 2f), enabled ? UiKit.Gold : UiKit.Dim);
            UiKit.LabelFit(new Rect(r.x + 4f, r.y, r.width - 8f, r.height), label, !enabled ? UiKit.Dim : chosen ? UiKit.Gold : Color.white, GameFont.Small,
                TextAnchor.MiddleCenter);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!enabled || !Widgets.ButtonInvisible(r)) return false;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            return true;
        }

        private static string CurrentOption(CouncilSettings s, SettingEntry e)
        {
            if (e.id == "colonyEra")
                return Current.ProgramState == ProgramState.Playing ? CouncilGameComponent.Instance?.era.ToString() : null;
            return SettingsAccess.Get(s, e.id)?.ToString();
        }

        private static void Choose(CouncilSettings s, SettingEntry e, string option)
        {
            try
            {
                if (e.id == "colonyEra")
                {
                    var colony = CouncilGameComponent.Instance;
                    if (colony == null) return;
                    colony.era = (EraMode)Enum.Parse(typeof(EraMode), option);
                    Avail.ResetCache();
                    return;
                }
                var f = SettingsAccess.Field(e.id);
                if (f != null) Changed(s, e, Enum.Parse(f.FieldType, option));
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("settings: choosing " + e.id, ex);
            }
        }

        private static bool DropdownBeside(float width) => width >= SettingsLayoutMath.SliderBesideWidth;

        private static void DropdownRow(CouncilSettings s, SettingEntry e, Texts t, float x, float y, float width, bool on)
        {
            bool beside = DropdownBeside(width);
            float labelW = beside ? Mathf.Round(width * 0.5f) : width;
            UiKit.LabelFit(new Rect(x, y + (beside ? 2f : 0f), labelW - 8f, LineH), t.label, on ? Color.white : UiKit.Dim);
            var b = beside ? new Rect(x + labelW, y, width - labelW, ButtonH) : new Rect(x, y + LineH + 2f, Mathf.Min(width, 320f), ButtonH);
            string current = CurrentOption(s, e);
            int index = Array.IndexOf(e.options, current);
            if (!Widgets.ButtonText(b, UiKit.Fit(index >= 0 ? t.options[index] : current ?? "-", b.width - 12f), true, true, on) || !on) return;
            var menu = new List<FloatMenuOption>();
            for (int i = 0; i < e.options.Length; i++)
            {
                string option = e.options[i];
                menu.Add(new FloatMenuOption(t.options[i], () => Choose(s, e, option)));
            }
            Find.WindowStack.Add(new FloatMenu(menu));
        }

        private static void ActionRow(CouncilSettings s, SettingEntry e, Texts t, float x, float y, float width, bool on)
        {
            float oneW = Mathf.Min(width, 360f);
            var one = new Rect(x, y, oneW, ButtonH);
            switch (e.id)
            {
                case "quickSetup":
                {
                    var scope = SettingsCatalog.ScopePresets.ToList();
                    float bw = SettingsLayoutMath.ButtonWidth(width, scope.Count);
                    for (int i = 0; i < scope.Count; i++)
                    {
                        var b = new Rect(x + i * (bw + 6f), y, bw, ButtonH);
                        TooltipHandler.TipRegion(b, scope[i].tip.Translate());
                        if (Widgets.ButtonText(b, UiKit.Fit(scope[i].label.Translate(), bw - 12f))) SettingsPresets.Confirm(s, scope[i]);
                    }
                    break;
                }
                case "slowButtonsReset":
                {
                    bool moved = s.slowButtonsX >= 0f || s.slowButtonsY >= 0f;
                    if (Widgets.ButtonText(one, UiKit.Fit(t.label, oneW - 12f), true, true, on && moved) && on && moved)
                    {
                        s.slowButtonsX = s.slowButtonsY = -1f;
                        countedFrame = -1000;
                    }
                    if (!moved && on) TooltipHandler.TipRegion(one, "CC_SetUI_SlowButtonsHome".Translate());
                    break;
                }
                case "selfCheck":
                    if (Widgets.ButtonText(one, UiKit.Fit(t.label, oneW - 12f), true, true, on) && on) Find.WindowStack.Add(new Dialog_SelfCheck());
                    break;
                case "resetAll":
                    if (Widgets.ButtonText(one, UiKit.Fit(t.label, oneW - 12f))) SettingsPresets.ConfirmReset(s);
                    break;
            }
            if (e.id != "quickSetup" && !t.tip.NullOrEmpty()) TooltipHandler.TipRegion(one, t.tip);
        }

        private static void Changed(CouncilSettings s, SettingEntry e, object value)
        {
            SettingsAccess.Set(s, e.id, value);
            SettingsAccess.AfterChange(new[] { e.id });
            countedFrame = -1000;
        }

        // ---------------- Text ----------------

        private static Texts TextsOf(SettingEntry e)
        {
            string language = LanguageDatabase.activeLanguage?.folderName ?? "";
            if (textsLanguage != language)
            {
                texts = new Dictionary<SettingEntry, Texts>();
                noteHeights.Clear();
                textsLanguage = language;
                matchedFor = null;
            }
            if (texts.TryGetValue(e, out var t)) return t;
            t = new Texts();
            string raw = e.label.Translate().RawText ?? e.label;
            t.valueInLabel = raw.Contains("{0}");
            t.label = t.valueInLabel ? raw.Replace("{0}", "").Trim().TrimEnd(':') : raw;
            t.tip = e.tip != null && e.tip.CanTranslate() ? e.tip.Translate().RawText : null;
            if (e.IsField)
            {
                string fallback = "CC_SetUI_DefaultIs".Translate(SettingsAccess.ValueText(e, SettingsAccess.DefaultOf(e.id))).Resolve();
                t.tip = t.tip.NullOrEmpty() ? fallback : t.tip + "\n\n" + fallback;
            }
            t.note = e.note?.Translate().Resolve();
            int n = e.options?.Length ?? 0;
            t.options = new string[n];
            t.optionTips = new string[n];
            t.optionNotes = new string[n];
            for (int i = 0; i < n; i++)
            {
                t.options[i] = SettingsAccess.OptionLabel(e, e.options[i]);
                string tipKey = e.optionPrefix + e.options[i] + "_Tip";
                t.optionTips[i] = tipKey.CanTranslate() ? tipKey.Translate().Resolve() : null;
                t.optionNotes[i] = e.notePrefix != null ? (e.notePrefix + e.options[i]).Translate().Resolve() : null;
            }
            var hay = new List<string> { t.label, t.tip, t.note, SectionLabel(e.section) };
            hay.AddRange(t.options);
            hay.AddRange(t.optionNotes);
            t.haystack = string.Join("\n", hay.Where(h => !h.NullOrEmpty())).ToLowerInvariant();
            texts[e] = t;
            return t;
        }

        /// <summary>The tooltip: the setting's own, with its default; the deeper game hooks also say how many took.</summary>
        private static string Tip(SettingEntry e, Texts t)
        {
            if (e.id == "deepHooks")
                return "CC_Settings_DeepHooks_Tip".Translate(RimfolkPatches.Applied.Count, RimfolkPatches.Failed.Count).Resolve() + "\n\n"
                       + "CC_SetUI_DefaultIs".Translate(SettingsAccess.ValueText(e, SettingsAccess.DefaultOf(e.id))).Resolve();
            return t.tip;
        }

        /// <summary>The line under the row: what you'll notice (for a choice, with the option now chosen).</summary>
        private static string NoteText(CouncilSettings s, SettingEntry e, Texts t)
        {
            if (e.notePrefix == null) return t.note;
            int i = Array.IndexOf(e.options, CurrentOption(s, e));
            return i >= 0 ? t.optionNotes[i] : t.note;
        }

        /// <summary>Why a row is grayed out: the switch it needs, or its own reason.</summary>
        private static string WhyOff(CouncilSettings s, SettingEntry e)
        {
            foreach (var term in SettingsCatalog.ParseNeeds(e.needs))
            {
                object v = SettingsAccess.Get(s, term.field);
                bool holds = term.values == null ? v is bool b && b != term.negate : v != null && term.values.Contains(v.ToString()) != term.negate;
                if (holds) continue;
                if (term.values == null && !term.negate)
                {
                    var parent = SettingsCatalog.Find(term.field);
                    if (parent != null) return "CC_SetUI_Needs".Translate(SettingsAccess.PlainLabel(parent));
                }
                break;
            }
            return e.whyOff != null ? e.whyOff.Translate().Resolve() : "CC_SetUI_NotNow".Translate().Resolve();
        }

        private static float NoteHeight(string note, float width)
        {
            var key = (note, Mathf.RoundToInt(width));
            if (noteHeights.TryGetValue(key, out float h)) return h;
            Text.Font = GameFont.Tiny;
            h = Text.CalcHeight(note, width);
            Text.Font = GameFont.Small;
            if (noteHeights.Count > 3000) noteHeights.Clear();
            noteHeights[key] = h;
            return h;
        }

        // ---------------- The game's dialog ----------------

        /// <summary>
        /// The game's mod settings dialog is 900 by 700 whatever the screen; on a small screen at a big interface scale it
        /// would hang off the edges, close button and all. Shrink it to fit (our pages scroll), and leave it alone otherwise.
        /// </summary>
        private static void FitDialogToScreen()
        {
            try
            {
                var dialog = Find.WindowStack?.WindowOfType<Dialog_ModSettings>();
                if (dialog == null) return;
                float sw = UI.screenWidth, sh = UI.screenHeight;
                var r = dialog.windowRect;
                if (r.x >= 0f && r.y >= 0f && r.xMax <= sw && r.yMax <= sh) return;
                SettingsLayoutMath.DialogSize(sw, sh, out float w, out float h);
                w = Mathf.Min(r.width, w);
                h = Mathf.Min(r.height, h);
                dialog.windowRect = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h).Rounded();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fitting the settings window to the screen", e);
            }
        }
    }
}
