using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The Almanac tab: categories on the left, articles in the middle, the page on the right.</summary>
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
            float top = UiKit.PageHeader(rect, Icons.Almanac, "CC_Tab_Almanac".Translate(), "CC_UI_Sub_Almanac".Translate(),
                "CC_UI_Stat_Pages".Translate(visibleCount).Resolve());
            rect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            float catW = Mathf.Min(190f, rect.width * 0.22f);
            float listW = Mathf.Min(290f, rect.width * 0.3f);
            var catRect = new Rect(rect.x, rect.y, catW, rect.height);
            var listRect = new Rect(catRect.xMax + 8f, rect.y, listW, rect.height);
            var pageRect = new Rect(listRect.xMax + 10f, rect.y, rect.xMax - listRect.xMax - 10f, rect.height);
            DrawCategories(catRect);
            DrawList(listRect);
            DrawPage(pageRect);
        }

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

        // The filtered list and each row's height, worked out again only when the filter or the width changes.
        private static List<AlmanacArticleDef> listArticles;
        private static List<(float title, float sub)> listHeights;
        private static string listKey;

        private static void DrawList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(4f);
            var view = new Rect(0f, 0f, inner.width - 16f, listHeight);
            // With everything (or a search) shown, each row gets its part's icon.
            bool icons = category == null || !search.NullOrEmpty();
            float textX = icons ? 26f : 4f;
            string key = (category?.ToString() ?? "") + "|" + search + "|" + view.width + "|" + visibleCount + "|" + showAllDlc;
            if (key != listKey || listArticles == null)
            {
                listKey = key;
                listArticles = Visible
                    .Where(a => (category == null || a.category == category || !search.NullOrEmpty()) && a.Matches(search))
                    .OrderBy(a => a.category).ThenBy(a => a.order).ThenBy(a => a.label)
                    .ToList();
                listHeights = new List<(float, float)>(listArticles.Count);
                foreach (var a in listArticles)
                {
                    Text.Font = GameFont.Small;
                    float th = Text.CalcHeight(a.LabelCap, view.width - textX - 4f);
                    Text.Font = GameFont.Tiny;
                    float sh = a.summary.NullOrEmpty() ? 0f : Text.CalcHeight(a.summary, view.width - textX - 4f);
                    listHeights.Add((th, sh));
                }
                Text.Font = GameFont.Small;
            }
            var articles = listArticles;
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
                if (Widgets.ButtonInvisible(row))
                {
                    selected = a;
                    pageScroll = Vector2.zero;
                }
                y += h + 2f;
            }
            listHeight = y + 10f;
            Widgets.EndScrollView();
            if (selected == null && articles.Count > 0) selected = articles[0];
        }

        private static void DrawPage(Rect rect)
        {
            var a = selected;
            if (a == null)
            {
                UiKit.EmptyState(rect, Icons.Almanac, "CC_Almanac_Pick".Translate());
                return;
            }
            var view = new Rect(0f, 0f, rect.width - 18f, pageHeight);
            Widgets.BeginScrollView(rect, ref pageScroll, view);
            float y = 0f;
            float w = view.width;

            Text.Font = GameFont.Medium;
            float h = Text.CalcHeight(a.LabelCap, w);
            Widgets.Label(new Rect(0f, y, w, h), a.LabelCap);
            y += h + 2f;
            string meta = ("CC_AlmanacCat_" + a.category).Translate();
            if (!a.dlc.NullOrEmpty()) meta += "  ·  " + a.dlc + (a.DlcActive ? "" : " (" + "CC_Almanac_NotActive".Translate() + ")");
            if (!a.mods.NullOrEmpty()) meta += "  ·  " + "CC_Almanac_FromMod".Translate(a.ModNames()).ToString() + (a.ModsActive ? "" : " (" + "CC_Almanac_NotActive".Translate().ToString() + ")");
            if (a.approximate) meta += "  ·  " + "CC_Almanac_Approximate".Translate();
            UiKit.Icon(new Rect(0f, y + 2f, 14f, 14f), Icons.For(a.category), UiKit.Gold);
            y += UiKit.Paragraph(20f, y, w - 20f, meta, Faint, GameFont.Tiny) + 4f;
            UiKit.Divider(0f, y, w);
            y += 8f;

            if (!a.tip.NullOrEmpty())
            {
                string q = "\"" + a.tip + "\"";
                h = Text.CalcHeight(q, w - 20f);
                GUI.color = QuoteColor;
                Widgets.Label(new Rect(14f, y, w - 20f, h), q);
                GUI.color = Color.white;
                y += h + 10f;
            }
            if (!a.description.NullOrEmpty())
            {
                h = Text.CalcHeight(a.description, w);
                Widgets.Label(new Rect(0f, y, w, h), a.description);
                y += h + 10f;
            }
            if (!a.sections.NullOrEmpty())
            {
                foreach (var sec in a.sections)
                {
                    if (sec == null) continue;
                    y = Heading(y + 4f, w, sec.heading);
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

            if (a.template != null && Find.CurrentMap != null)
            {
                y += 4f;
                string reason = a.template.UnavailableReason(Find.CurrentMap);
                if (Widgets.ButtonText(new Rect(0f, y, 240f, 30f), "CC_Almanac_PlanRoom".Translate(a.template.label), true, true, reason == null) && reason == null)
                {
                    var des = new Designator_PlanRoom(a.template);
                    Find.MainTabsRoot.EscapeCurrentTab(false);
                    Find.DesignatorManager.Select(des);
                }
                if (reason != null) TooltipHandler.TipRegion(new Rect(0f, y, 240f, 30f), reason);
                y += 36f;
            }
            if (a.seeAlso != null && a.seeAlso.Count > 0)
            {
                GUI.color = Header;
                Widgets.Label(new Rect(0f, y, w, 24f), "CC_Almanac_SeeAlso".Translate());
                GUI.color = Color.white;
                y += 24f;
                float x = 0f;
                foreach (var other in a.seeAlso.Where(o => o != null))
                {
                    float bw = Mathf.Min(w, Text.CalcSize(other.LabelCap).x + 20f);
                    if (x + bw > w)
                    {
                        x = 0f;
                        y += 30f;
                    }
                    if (Widgets.ButtonText(new Rect(x, y, bw, 26f), other.LabelCap)) Show(other);
                    x += bw + 6f;
                }
                y += 32f;
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
            return y + h + 8f;
        }

        private static readonly Color TableHeader = new Color(0.85f, 0.8f, 0.6f);

        /// <summary>A small table: columns as wide as their text where they fit, the last one wraps.</summary>
        private static float Table(float y, float w, List<string> rows)
        {
            if (rows.NullOrEmpty()) return y;
            var cells = rows.Select(r => r.Split('|').Select(c => c.Trim()).ToArray()).ToList();
            int cols = cells.Max(r => r.Length);
            var widths = new float[cols];
            for (int c = 0; c < cols; c++)
                widths[c] = cells.Where(r => c < r.Length).Max(r => Text.CalcSize(r[c]).x) + 14f;
            if (widths.Sum() > w)
            {
                // Earlier columns keep their width up to a share of the page; the last one takes what's left and wraps.
                float cap = w * (cols > 2 ? 0.3f : 0.42f);
                for (int c = 0; c < cols - 1; c++) widths[c] = Mathf.Min(widths[c], cap);
                widths[cols - 1] = Mathf.Max(60f, w - widths.Take(cols - 1).Sum());
            }
            for (int i = 0; i < cells.Count; i++)
            {
                var row = cells[i];
                float rh = 22f;
                for (int c = 0; c < row.Length; c++) rh = Mathf.Max(rh, Text.CalcHeight(row[c], widths[c] - 10f) + 4f);
                var rr = new Rect(0f, y, Mathf.Min(w, widths.Sum()), rh);
                if (i == 0) Widgets.DrawBoxSolid(rr, new Color(1f, 1f, 1f, 0.06f));
                else if (i % 2 == 0) Widgets.DrawLightHighlight(rr);
                float x = 0f;
                for (int c = 0; c < row.Length; c++)
                {
                    if (i == 0) GUI.color = TableHeader;
                    Widgets.Label(new Rect(x + 5f, y + 2f, widths[c] - 10f, rh), row[c]);
                    GUI.color = Color.white;
                    x += widths[c];
                }
                y += rh;
            }
            return y + 10f;
        }

        private static float Heading(float y, float w, string title)
        {
            if (title.NullOrEmpty()) return y;
            GUI.color = Header;
            float h = Text.CalcHeight(title, w);
            Widgets.Label(new Rect(0f, y, w, h), title);
            GUI.color = Color.white;
            return y + h + 2f;
        }

        private static float Points(float y, float w, string title, List<string> items)
        {
            if (items == null || items.Count == 0) return y;
            y = Heading(y, w, title);
            foreach (var item in items)
            {
                float h = Text.CalcHeight(item, w - 16f);
                Widgets.Label(new Rect(0f, y, 14f, 24f), "-");
                Widgets.Label(new Rect(14f, y, w - 16f, h), item);
                y += h + 3f;
            }
            return y + 8f;
        }
    }
}
