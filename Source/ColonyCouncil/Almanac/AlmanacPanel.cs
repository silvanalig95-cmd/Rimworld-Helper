using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The Almanac tab: categories on the left, the page list in the middle, the page on the right. The categories and
    /// the list fold into a thin strip so a page gets the whole width, and the page is written at the text size picked
    /// with the "Aa" buttons at the top (UI/TextScale.cs). The widths, and how a table's columns share the page, are
    /// worked out in UI/AlmanacLayoutMath.cs.
    /// </summary>
    public static class AlmanacPanel
    {
        private static AlmanacCategory? category;
        private static AlmanacArticleDef selected;
        private static string search = "";
        private static bool showAllDlc;
        private static Vector2 listScroll;
        private static Vector2 categoryScroll;
        private static Vector2 pageScroll;
        private static float listHeight = 500f;
        private static float pageHeight = 500f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color QuoteColor = new Color(1f, 0.93f, 0.75f);
        private static readonly Color Header = new Color(0.85f, 0.8f, 0.6f);
        private static readonly Color TableHeader = new Color(0.85f, 0.8f, 0.6f);
        private static readonly Color CardValue = new Color(0.92f, 0.92f, 0.92f);

        /// <summary>The page open in the almanac, if any (remembered between sessions by the council window).</summary>
        public static AlmanacArticleDef Selected => selected;

        /// <summary>Open the almanac on this page (from a priority's "read more").</summary>
        public static void Show(AlmanacArticleDef article)
        {
            if (article == null) return;
            selected = article;
            category = article.category;
            search = "";
            pageScroll = Vector2.zero;
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;
        private static AlmanacTextSize TextSize => Settings?.almanacTextSize ?? AlmanacTextSize.Normal;
        private static bool Folded => Settings?.almanacFolded ?? false;

        private static IEnumerable<AlmanacArticleDef> Visible =>
            DefDatabase<AlmanacArticleDef>.AllDefsListForReading.Where(a => showAllDlc || a.DlcActive);

        // How many pages each part has (DLCs and mods don't change while the game runs), worked out once per setting.
        private static readonly Dictionary<AlmanacCategory, int> counts = new Dictionary<AlmanacCategory, int>();
        private static int visibleCount;
        private static bool? countsFor;

        private static void Count()
        {
            if (countsFor == showAllDlc) return;
            counts.Clear();
            visibleCount = 0;
            foreach (var a in Visible)
            {
                visibleCount++;
                counts[a.category] = counts.TryGetValue(a.category, out int n) ? n + 1 : 1;
            }
            countsFor = showAllDlc;
        }

        public static void Draw(Rect rect)
        {
            Count();
            float controls = HeaderControlsWidth;
            float top = UiKit.PageHeader(rect, Icons.Almanac, "CC_Tab_Almanac".Translate(), "CC_UI_Sub_Almanac".Translate(),
                "CC_UI_Stat_Pages".Translate(visibleCount).Resolve(), reserveRight: controls + 12f);
            DrawHeaderControls(new Rect(rect.xMax - controls, rect.y + 5f, controls, 24f));
            rect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            bool folded = Folded;
            AlmanacLayoutMath.Columns(rect.width, folded, out float catW, out float listW, out float pageW);
            var pageRect = new Rect(rect.xMax - pageW, rect.y, pageW, rect.height);
            EnsureList();
            if (folded)
            {
                if (selected == null && listArticles.Count > 0) selected = listArticles[0];
                DrawStrip(new Rect(rect.x, rect.y, catW, rect.height));
            }
            else
            {
                var catRect = new Rect(rect.x, rect.y, catW, rect.height);
                DrawCategories(catRect);
                DrawList(new Rect(catRect.xMax + AlmanacLayoutMath.CategoryGap, rect.y, listW, rect.height));
            }
            DrawPage(pageRect);
        }

        // ---------------- The header's buttons: fold the lists away, and the text size ----------------

        private const float FoldW = 26f, AaW = 24f, StepW = 22f, StepGap = 2f;
        private static readonly AlmanacTextSize[] Steps = (AlmanacTextSize[])System.Enum.GetValues(typeof(AlmanacTextSize));

        private static float HeaderControlsWidth => FoldW + 12f + AaW + Steps.Length * (StepW + StepGap) - StepGap;

        private static void DrawHeaderControls(Rect r)
        {
            bool folded = Folded;
            if (FlatButton(new Rect(r.x, r.y, FoldW, r.height), folded ? "»" : "«", false,
                    folded ? "CC_Almanac_Unfold".Translate() : "CC_Almanac_Fold".Translate()))
                SetFolded(!folded);
            float x = r.x + FoldW + 12f;
            var aa = new Rect(x, r.y, AaW, r.height);
            UiKit.LabelFit(aa, "Aa", Faint, GameFont.Small, TextAnchor.MiddleCenter);
            TooltipHandler.TipRegion(aa, "CC_Almanac_TextSizeNote".Translate());
            x += AaW;
            var size = TextSize;
            foreach (var step in Steps)
            {
                var b = new Rect(x, r.y, StepW, r.height);
                x += StepW + StepGap;
                bool chosen = step == size;
                UiKit.Card(b, null, chosen);
                if (chosen) Widgets.DrawBoxSolid(new Rect(b.x, b.yMax - 2f, b.width, 2f), UiKit.Gold);
                // Each button's "A" at its own size.
                using (TextScale.Push(AlmanacLayoutMath.Scale(step)))
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = chosen ? UiKit.Gold : Color.white;
                    Widgets.Label(b, "A");
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(b, StepTip(step));
                if (Widgets.ButtonInvisible(b) && !chosen)
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    SetTextSize(step);
                }
            }
        }

        private static string[] stepTips;

        private static string StepTip(AlmanacTextSize step)
        {
            if (stepTips == null)
            {
                stepTips = new string[Steps.Length];
                for (int i = 0; i < Steps.Length; i++)
                {
                    string name = ("CC_TextSize_" + Steps[i]).Translate();
                    stepTips[i] = "CC_Almanac_TextSize".Translate(name) + "\n\n" + ("CC_TextSize_" + Steps[i] + "_Tip").Translate() + "\n\n" +
                                  "CC_Almanac_TextSizeNote".Translate();
                }
            }
            return stepTips[(int)step];
        }

        /// <summary>A small flat button with a glyph or a texture in the middle; true when clicked.</summary>
        private static bool FlatButton(Rect r, string glyph, bool active, string tip, Texture2D icon = null)
        {
            UiKit.Card(r, null, active);
            if (icon != null) UiKit.Icon(new Rect(r.center.x - 8f, r.center.y - 8f, 16f, 16f), icon, Mouse.IsOver(r) ? Color.white : Faint);
            else UiKit.LabelFit(r, glyph, Mouse.IsOver(r) ? UiKit.Gold : Color.white, GameFont.Small, TextAnchor.MiddleCenter);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            return Widgets.ButtonInvisible(r);
        }

        private static void SetFolded(bool folded)
        {
            var s = Settings;
            if (s == null || s.almanacFolded == folded) return;
            s.almanacFolded = folded;
            CouncilSettings.SaveNow();
        }

        private static void SetTextSize(AlmanacTextSize size)
        {
            var s = Settings;
            if (s == null || s.almanacTextSize == size) return;
            s.almanacTextSize = size;
            CouncilSettings.SaveNow();
        }

        // ---------------- The categories, or the strip they fold into ----------------

        private static void DrawCategories(Rect rect)
        {
            float y = rect.y;
            Text.Font = GameFont.Small;
            var searchRect = new Rect(rect.x, y, rect.width, 28f);
            string before = search;
            search = Widgets.TextField(searchRect, search);
            if (search.NullOrEmpty())
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(searchRect.x + 6f, searchRect.y + 4f, searchRect.width, 24f), UiKit.Fit("CC_Almanac_Search".Translate(), searchRect.width - 10f));
                GUI.color = Color.white;
            }
            if (before != search) listScroll = Vector2.zero;
            y += 34f;

            // The parts of the almanac, scrolling when the window is short.
            float checkH = 24f;
            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y - checkH - 8f);
            int rows = 1;
            foreach (AlmanacCategory c in System.Enum.GetValues(typeof(AlmanacCategory)))
                if (counts.TryGetValue(c, out int n) && n > 0) rows++;
            var view = new Rect(0f, 0f, outRect.width - (rows * 28f > outRect.height ? 16f : 0f), rows * 28f);
            Widgets.BeginScrollView(outRect, ref categoryScroll, view);
            float cy = 0f;
            if (DrawCategoryButton(new Rect(0f, cy, view.width, 26f), "CC_Almanac_All".Translate(), visibleCount, category == null, Icons.Almanac)) category = null;
            cy += 28f;
            foreach (AlmanacCategory c in System.Enum.GetValues(typeof(AlmanacCategory)))
            {
                if (!counts.TryGetValue(c, out int n) || n == 0) continue;
                if (DrawCategoryButton(new Rect(0f, cy, view.width, 26f), ("CC_AlmanacCat_" + c).Translate(), n, category == c, Icons.For(c)))
                {
                    category = c;
                    listScroll = Vector2.zero;
                }
                cy += 28f;
            }
            Widgets.EndScrollView();
            bool all = showAllDlc;
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.yMax - checkH, rect.width, checkH), UiKit.Fit("CC_Almanac_AllDlc".Translate(), rect.width - 30f), ref showAllDlc);
            if (all != showAllDlc) listScroll = Vector2.zero;
            TooltipHandler.TipRegion(new Rect(rect.x, rect.yMax - checkH, rect.width, checkH), "CC_Almanac_AllDlc".Translate());
        }

        private static bool DrawCategoryButton(Rect r, string label, int count, bool active, Texture2D icon)
        {
            if (active) Widgets.DrawHighlightSelected(r);
            else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            if (active) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), UiKit.Gold);
            UiKit.Icon(new Rect(r.x + 6f, r.y + 5f, 16f, 16f), icon, active ? UiKit.Gold : Faint);
            UiKit.LabelFit(new Rect(r.x + 28f, r.y, r.width - 62f, r.height), label, active ? Color.white : new Color(0.9f, 0.9f, 0.9f));
            UiKit.LabelFit(new Rect(r.xMax - 34f, r.y, 30f, r.height), count.ToString(), Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            return Widgets.ButtonInvisible(r);
        }

        /// <summary>
        /// The categories and the list folded away: a button to bring them back, the open page's part, and buttons to turn
        /// to the previous and next page of the list. The rest of the strip unfolds too.
        /// </summary>
        private static void DrawStrip(Rect r)
        {
            Widgets.DrawMenuSection(r);
            var inner = r.ContractedBy(2f);
            float y = inner.y + 2f;
            string unfold = "CC_Almanac_Unfold".Translate();
            if (FlatButton(new Rect(inner.x, y, inner.width, 26f), "»", false, unfold)) SetFolded(false);
            y += 34f;
            if (selected != null)
            {
                var ir = new Rect(inner.center.x - 8f, y, 16f, 16f);
                UiKit.Icon(ir, Icons.For(selected.category), UiKit.Gold);
                TooltipHandler.TipRegion(ir.ExpandedBy(4f), ("CC_AlmanacCat_" + selected.category).Translate());
                y += 26f;
            }
            int i = selected == null ? -1 : listArticles.IndexOf(selected);
            var prev = i > 0 ? listArticles[i - 1] : null;
            var next = i + 1 < listArticles.Count ? listArticles[i + 1] : null;
            if (prev != null && FlatButton(new Rect(inner.x, y, inner.width, 26f), null, false, "CC_Almanac_PrevPage".Translate(prev.LabelCap), TexButton.ReorderUp))
                Open(prev);
            y += 30f;
            if (next != null && FlatButton(new Rect(inner.x, y, inner.width, 26f), null, false, "CC_Almanac_NextPage".Translate(next.LabelCap), TexButton.ReorderDown))
                Open(next);
            y += 34f;
            var rest = new Rect(inner.x, y, inner.width, inner.yMax - y);
            if (rest.height > 10f)
            {
                Widgets.DrawHighlightIfMouseover(rest);
                TooltipHandler.TipRegion(rest, unfold);
                if (Widgets.ButtonInvisible(rest)) SetFolded(false);
            }
        }

        private static void Open(AlmanacArticleDef a)
        {
            selected = a;
            pageScroll = Vector2.zero;
        }

        // ---------------- The page list ----------------

        // The filtered list, worked out again only when the filter changes, and each row's height, again only when the width does.
        private static List<AlmanacArticleDef> listArticles;
        private static List<(float title, float sub)> listHeights;
        private static string listKey;
        private static float heightsWidth = -1f;

        private static void EnsureList()
        {
            string key = (category?.ToString() ?? "") + "|" + search + "|" + visibleCount + "|" + showAllDlc;
            if (key == listKey && listArticles != null) return;
            listKey = key;
            listArticles = Visible
                .Where(a => (category == null || a.category == category || !search.NullOrEmpty()) && a.Matches(search))
                .OrderBy(a => a.category).ThenBy(a => a.order).ThenBy(a => a.label)
                .ToList();
            heightsWidth = -1f;
        }

        private static void DrawList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(4f);
            var view = new Rect(0f, 0f, inner.width - 16f, listHeight);
            // With everything (or a search) shown, each row gets its part's icon.
            bool icons = category == null || !search.NullOrEmpty();
            float textX = icons ? 26f : 4f;
            var articles = listArticles;
            if (heightsWidth != view.width - textX || listHeights == null || listHeights.Count != articles.Count)
            {
                heightsWidth = view.width - textX;
                listHeights = new List<(float, float)>(articles.Count);
                foreach (var a in articles)
                {
                    Text.Font = GameFont.Small;
                    float th = Text.CalcHeight(a.LabelCap, view.width - textX - 4f);
                    Text.Font = GameFont.Tiny;
                    float sh = a.summary.NullOrEmpty() ? 0f : Text.CalcHeight(a.summary, view.width - textX - 4f);
                    listHeights.Add((th, sh));
                }
                Text.Font = GameFont.Small;
            }
            Widgets.BeginScrollView(inner, ref listScroll, view);
            float y = 0f;
            if (articles.Count == 0) y += UiKit.EmptyState(0f, y, view.width, Icons.Almanac, "CC_Almanac_NothingFound".Translate(), 120f);
            for (int i = 0; i < articles.Count; i++)
            {
                var a = articles[i];
                float th = listHeights[i].title, sh = listHeights[i].sub;
                float h = th + sh + 8f;
                // Only the rows in sight are drawn.
                if (y + h < listScroll.y || y > listScroll.y + inner.height)
                {
                    y += h + 2f;
                    continue;
                }
                string title = a.LabelCap;
                string sub = a.summary ?? "";
                var row = new Rect(0f, y, view.width, h);
                if (a == selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                if (a == selected) Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), UiKit.Gold);
                if (icons) UiKit.Icon(new Rect(6f, y + 5f, 14f, 14f), Icons.For(a.category), a == selected ? UiKit.Gold : UiKit.Dim);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(textX, y + 3f, view.width - textX - 4f, th), title);
                if (sh > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = Faint;
                    Widgets.Label(new Rect(textX, y + 3f + th, view.width - textX - 4f, sh), sub);
                    GUI.color = Color.white;
                }
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(row)) Open(a);
                y += h + 2f;
            }
            listHeight = y + 10f;
            Widgets.EndScrollView();
            if (selected == null && articles.Count > 0) selected = articles[0];
        }

        // ---------------- The page ----------------

        /// <summary>The page's measures at the chosen text size: a line of text, and the spaces that follow from it.</summary>
        private struct Metrics
        {
            public float line, paragraph, headingAbove, headingBelow, item, indent;
            public int font;
        }

        private static Metrics m;

        private static void DrawPage(Rect rect)
        {
            var a = selected;
            if (a == null)
            {
                UiKit.EmptyState(rect, Icons.Almanac, "CC_Almanac_Pick".Translate());
                return;
            }
            var size = TextSize;
            using (TextScale.Push(AlmanacLayoutMath.Scale(size)))
            {
                Text.Font = GameFont.Small;
                float line = Text.CalcSize("Ag").y;
                m = new Metrics
                {
                    line = line,
                    paragraph = Mathf.Round(line * 0.45f),
                    headingAbove = Mathf.Round(line * 0.6f),
                    headingBelow = Mathf.Round(line * 0.15f),
                    item = Mathf.Round(line * 0.18f),
                    indent = Mathf.Round(line * 0.7f),
                    font = (int)size
                };
                DrawArticle(rect, a);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawArticle(Rect rect, AlmanacArticleDef a)
        {
            // A little room on the left, and between the text and the scroll bar.
            var outer = new Rect(rect.x + AlmanacLayoutMath.ArticleLeft, rect.y, rect.width - AlmanacLayoutMath.ArticleLeft, rect.height);
            var view = new Rect(0f, 0f, outer.width - AlmanacLayoutMath.ScrollBar, pageHeight);
            Widgets.BeginScrollView(outer, ref pageScroll, view);
            float y = 0f;
            float w = AlmanacLayoutMath.TextWidth(rect.width);

            Text.Font = GameFont.Medium;
            float h = Text.CalcHeight(a.LabelCap, w);
            Widgets.Label(new Rect(0f, y, w, h), a.LabelCap);
            Text.Font = GameFont.Small;
            y += h + 2f;
            string meta = ("CC_AlmanacCat_" + a.category).Translate();
            if (!a.dlc.NullOrEmpty()) meta += "  ·  " + a.dlc + (a.DlcActive ? "" : " (" + "CC_Almanac_NotActive".Translate() + ")");
            if (!a.mods.NullOrEmpty()) meta += "  ·  " + "CC_Almanac_FromMod".Translate(a.ModNames()).ToString() + (a.ModsActive ? "" : " (" + "CC_Almanac_NotActive".Translate().ToString() + ")");
            if (a.approximate) meta += "  ·  " + "CC_Almanac_Approximate".Translate();
            float icon = Mathf.Round(m.line * 0.64f);
            UiKit.Icon(new Rect(0f, y + 2f, icon, icon), Icons.For(a.category), UiKit.Gold);
            y += UiKit.Paragraph(icon + 6f, y, w - icon - 6f, meta, Faint, GameFont.Tiny) + 4f;
            UiKit.Divider(0f, y, w);
            y += m.paragraph;

            if (!a.tip.NullOrEmpty())
            {
                string q = "\"" + a.tip + "\"";
                h = Text.CalcHeight(q, w - 20f);
                GUI.color = QuoteColor;
                Widgets.Label(new Rect(14f, y, w - 20f, h), q);
                GUI.color = Color.white;
                y += h + m.paragraph;
            }
            y = Paragraph(y, w, a.description);
            if (!a.sections.NullOrEmpty())
            {
                foreach (var sec in a.sections)
                {
                    if (sec == null) continue;
                    y = Heading(y, w, sec.heading);
                    y = Paragraph(y, w, sec.text);
                    y = Points(y, w, null, sec.points);
                    y = Table(y, w, sec.table);
                    y = Paragraph(y, w, sec.after);
                }
            }
            // No fixed "facts" and "advice" headings: the page reads on from its description. What Rimfolk itself does
            // with a mod gets its own short part, since that's a different subject.
            var points = new List<string>();
            var rimfolk = new List<string>();
            foreach (var line in (a.facts ?? new List<string>()).Concat(a.advice ?? new List<string>()))
            {
                if (line.NullOrEmpty()) continue;
                if (line.StartsWith(RimfolkPrefix)) rimfolk.Add(line.Substring(RimfolkPrefix.Length).TrimStart().CapitalizeFirst());
                else points.Add(line);
            }
            y = Points(y, w, null, points);
            y = Points(y, w, "CC_Almanac_WithRimfolk".Translate(), rimfolk);

            float buttonH = m.line + 8f;
            if (a.template != null && Find.CurrentMap != null)
            {
                y += 4f;
                string reason = a.template.UnavailableReason(Find.CurrentMap);
                string label = "CC_Almanac_PlanRoom".Translate(a.template.label);
                var br = new Rect(0f, y, Mathf.Min(w, Mathf.Max(240f, Text.CalcSize(label).x + 24f)), buttonH);
                if (Widgets.ButtonText(br, label, true, true, reason == null) && reason == null)
                {
                    var des = new Designator_PlanRoom(a.template);
                    Find.MainTabsRoot.EscapeCurrentTab(false);
                    Find.DesignatorManager.Select(des);
                }
                if (reason != null) TooltipHandler.TipRegion(br, reason);
                y += buttonH + m.paragraph;
            }
            if (a.seeAlso != null && a.seeAlso.Count > 0)
            {
                y = Heading(y, w, "CC_Almanac_SeeAlso".Translate());
                float x = 0f;
                float seeH = m.line + 4f;
                foreach (var other in a.seeAlso.Where(o => o != null))
                {
                    float bw = Mathf.Min(w, Text.CalcSize(other.LabelCap).x + 20f);
                    if (x > 0f && x + bw > w)
                    {
                        x = 0f;
                        y += seeH + 4f;
                    }
                    if (Widgets.ButtonText(new Rect(x, y, bw, seeH), other.LabelCap)) Show(other);
                    x += bw + 6f;
                }
                y += seeH + m.paragraph;
            }
            pageHeight = y + 20f;
            Widgets.EndScrollView();
        }

        /// <summary>How the mod pages mark what Rimfolk does with a mod ("With Rimfolk: ...").</summary>
        private static string RimfolkPrefix => "CC_Almanac_WithRimfolkPrefix".Translate();

        private static float Paragraph(float y, float w, string text)
        {
            if (text.NullOrEmpty()) return y;
            float h = Text.CalcHeight(text, w);
            Widgets.Label(new Rect(0f, y, w, h), text);
            return y + h + m.paragraph;
        }

        /// <summary>A section's heading, with room above it to set it off from what came before.</summary>
        private static float Heading(float y, float w, string title)
        {
            if (title.NullOrEmpty()) return y;
            y += m.headingAbove;
            GUI.color = Header;
            float h = Text.CalcHeight(title, w);
            Widgets.Label(new Rect(0f, y, w, h), title);
            GUI.color = Color.white;
            return y + h + m.headingBelow;
        }

        private static float Points(float y, float w, string title, List<string> items)
        {
            if (items == null || items.Count == 0) return y;
            y = Heading(y, w, title);
            float indent = m.indent;
            foreach (var item in items)
            {
                float h = Text.CalcHeight(item, w - indent - 2f);
                Widgets.Label(new Rect(0f, y, indent, m.line), "-");
                Widgets.Label(new Rect(indent, y, w - indent - 2f, h), item);
                y += h + m.item;
            }
            return y + m.paragraph;
        }

        // ---------------- Tables ----------------

        /// <summary>A table measured at one text size, and laid out for one width (worked out again when either changes).</summary>
        private sealed class TableLayout
        {
            public string[][] cells;
            public int cols;
            public int font;
            /// <summary>Each column's longest word, longest cell and average cell, as text widths.</summary>
            public float[] word, full, typical;
            /// <summary>For cards: the widest column name and the longest word of any value.</summary>
            public float label, valueWord;
            public float charWidth;

            public float width = -1f;
            public TableMode mode;
            public float[] widths;
            public bool beside;
            /// <summary>Each row's height (as a row of the table, or as a card), and for cards each line's.</summary>
            public float[] heights;
            public float[][] lines;
        }

        private static readonly Dictionary<List<string>, TableLayout> tables = new Dictionary<List<string>, TableLayout>();

        private static TableLayout TableFor(List<string> rows, float w)
        {
            if (!tables.TryGetValue(rows, out var t) || t.font != m.font)
            {
                if (tables.Count > 256) tables.Clear();
                tables[rows] = t = Measure(rows);
            }
            if (Mathf.Abs(t.width - w) > 0.5f) Layout(t, w);
            return t;
        }

        private static TableLayout Measure(List<string> rows)
        {
            var t = new TableLayout { font = m.font };
            t.cells = rows.Select(r => (r ?? "").Split('|').Select(c => c.Trim()).ToArray()).ToArray();
            t.cols = t.cells.Max(r => r.Length);
            t.word = new float[t.cols];
            t.full = new float[t.cols];
            t.typical = new float[t.cols];
            t.widths = new float[t.cols];
            var counted = new int[t.cols];
            Text.Font = GameFont.Small;
            t.charWidth = Text.CalcSize("abcdefghijklmnopqrstuvwxyz").x / 26f;
            for (int i = 0; i < t.cells.Length; i++)
            {
                var row = t.cells[i];
                for (int c = 0; c < row.Length; c++)
                {
                    string s = row[c];
                    if (s.NullOrEmpty()) continue;
                    float full = Text.CalcSize(s).x;
                    float word = LongestWord(s);
                    t.full[c] = Mathf.Max(t.full[c], full);
                    t.word[c] = Mathf.Max(t.word[c], word);
                    if (i == 0)
                    {
                        if (c > 0) t.label = Mathf.Max(t.label, full);
                        continue;
                    }
                    t.typical[c] += full;
                    counted[c]++;
                    if (c > 0) t.valueWord = Mathf.Max(t.valueWord, word);
                }
            }
            for (int c = 0; c < t.cols; c++) t.typical[c] /= Mathf.Max(1, counted[c]);
            return t;
        }

        /// <summary>The widest word of a cell (the game wraps only at spaces, so a word wider than its column breaks).</summary>
        private static float LongestWord(string s)
        {
            float best = 0f;
            foreach (var word in s.Split(' '))
                if (word.Length > 0) best = Mathf.Max(best, Text.CalcSize(word).x);
            return best;
        }

        private static void Layout(TableLayout t, float w)
        {
            t.width = w;
            Text.Font = GameFont.Small;
            t.mode = AlmanacLayoutMath.Widths(t.word, t.full, t.typical, t.charWidth, w, t.widths);
            // A table of one column, or with nothing under its header, stays a table.
            if (t.mode == TableMode.Cards && (t.cols < 2 || t.cells.Length < 2))
            {
                t.mode = TableMode.Shared;
                for (int c = 0; c < t.cols; c++) t.widths[c] = w / t.cols;
            }
            t.heights = new float[t.cells.Length];
            if (t.mode != TableMode.Cards)
            {
                for (int i = 0; i < t.cells.Length; i++)
                {
                    var row = t.cells[i];
                    float rh = m.line + 4f;
                    for (int c = 0; c < row.Length; c++)
                        if (!row[c].NullOrEmpty()) rh = Mathf.Max(rh, Text.CalcHeight(row[c], t.widths[c] - AlmanacLayoutMath.CellPad) + 4f);
                    t.heights[i] = rh;
                }
                return;
            }
            float inside = w - AlmanacLayoutMath.CardPad;
            t.beside = AlmanacLayoutMath.LabelsBeside(w, t.label, t.valueWord);
            float labelCol = t.label + AlmanacLayoutMath.LabelGap;
            var names = t.cells[0];
            t.lines = new float[t.cells.Length][];
            for (int i = 1; i < t.cells.Length; i++)
            {
                var row = t.cells[i];
                var lines = t.lines[i] = new float[t.cols];
                float h = CardPadY * 2f;
                lines[0] = row.Length > 0 && !row[0].NullOrEmpty() ? Text.CalcHeight(row[0], inside) : 0f;
                h += lines[0] > 0f ? lines[0] + 2f : 0f;
                for (int c = 1; c < row.Length; c++)
                {
                    if (row[c].NullOrEmpty()) continue;
                    string name = c < names.Length ? names[c] : "";
                    lines[c] = t.beside
                        ? Mathf.Max(name.NullOrEmpty() ? 0f : Text.CalcHeight(name, labelCol - AlmanacLayoutMath.LabelGap), Text.CalcHeight(row[c], inside - labelCol))
                        : Text.CalcHeight(Inline(name, row[c]), inside);
                    h += lines[c] + 1f;
                }
                t.heights[i] = h;
            }
        }

        private const float CardPadY = 5f, CardGap = 6f;

        /// <summary>"Column: value" on one line, the column's name in the header color.</summary>
        private static string Inline(string name, string value) => name.NullOrEmpty() ? value : (name + ":").Colorize(TableHeader) + " " + value;

        /// <summary>A table: columns sized to their text (UI/AlmanacLayoutMath.cs), or a card for each row where they don't fit.</summary>
        private static float Table(float y, float w, List<string> rows)
        {
            if (rows.NullOrEmpty()) return y;
            var t = TableFor(rows, w);
            if (t.mode == TableMode.Cards) return Cards(y, w, t);
            float tableW = Mathf.Min(w, t.widths.Sum());
            for (int i = 0; i < t.cells.Length; i++)
            {
                var row = t.cells[i];
                float rh = t.heights[i];
                var rr = new Rect(0f, y, tableW, rh);
                if (i == 0) Widgets.DrawBoxSolid(rr, new Color(1f, 1f, 1f, 0.06f));
                else if (i % 2 == 0) Widgets.DrawLightHighlight(rr);
                float x = 0f;
                for (int c = 0; c < row.Length && c < t.cols; c++)
                {
                    if (i == 0) GUI.color = TableHeader;
                    Widgets.Label(new Rect(x + AlmanacLayoutMath.CellPad / 2f, y + 2f, t.widths[c] - AlmanacLayoutMath.CellPad, rh), row[c]);
                    GUI.color = Color.white;
                    x += t.widths[c];
                }
                y += rh;
            }
            return y + m.paragraph;
        }

        /// <summary>A table too wide for the page: each row a small card, its first cell as the title, then "Column: value".</summary>
        private static float Cards(float y, float w, TableLayout t)
        {
            float pad = AlmanacLayoutMath.CardPad / 2f;
            float inside = w - AlmanacLayoutMath.CardPad;
            float labelCol = t.label + AlmanacLayoutMath.LabelGap;
            var names = t.cells[0];
            for (int i = 1; i < t.cells.Length; i++)
            {
                var row = t.cells[i];
                var lines = t.lines[i];
                var card = new Rect(0f, y, w, t.heights[i]);
                Widgets.DrawBoxSolidWithOutline(card, UiKit.CardFill, UiKit.CardBorder, 1);
                Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), UiKit.WithAlpha(TableHeader, 0.6f));
                float cy = y + CardPadY;
                if (lines[0] > 0f)
                {
                    Widgets.Label(new Rect(pad, cy, inside, lines[0]), row[0]);
                    cy += lines[0] + 2f;
                }
                for (int c = 1; c < row.Length; c++)
                {
                    if (row[c].NullOrEmpty()) continue;
                    string name = c < names.Length ? names[c] : "";
                    if (t.beside)
                    {
                        GUI.color = TableHeader;
                        Widgets.Label(new Rect(pad, cy, labelCol - AlmanacLayoutMath.LabelGap, lines[c]), name);
                        GUI.color = CardValue;
                        Widgets.Label(new Rect(pad + labelCol, cy, inside - labelCol, lines[c]), row[c]);
                    }
                    else
                    {
                        GUI.color = CardValue;
                        Widgets.Label(new Rect(pad, cy, inside, lines[c]), Inline(name, row[c]));
                    }
                    GUI.color = Color.white;
                    cy += lines[c] + 1f;
                }
                y += t.heights[i] + CardGap;
            }
            return y + m.paragraph - CardGap;
        }
    }
}
