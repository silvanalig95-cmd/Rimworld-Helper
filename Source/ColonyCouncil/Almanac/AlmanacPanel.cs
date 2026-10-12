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
        private static Vector2 pageScroll;
        private static float listHeight = 500f;
        private static float pageHeight = 500f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color QuoteColor = new Color(1f, 0.93f, 0.75f);
        private static readonly Color Header = new Color(0.85f, 0.8f, 0.6f);

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

        public static void Draw(Rect rect)
        {
            float catW = 170f;
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
                Widgets.Label(new Rect(searchRect.x + 6f, searchRect.y + 4f, searchRect.width, 24f), "CC_Almanac_Search".Translate());
                GUI.color = Color.white;
            }
            if (before != search) listScroll = Vector2.zero;
            y += 34f;

            var all = Visible.ToList();
            if (DrawCategoryButton(new Rect(rect.x, y, rect.width, 26f), "CC_Almanac_All".Translate(), all.Count, category == null)) category = null;
            y += 28f;
            foreach (AlmanacCategory c in System.Enum.GetValues(typeof(AlmanacCategory)))
            {
                int n = all.Count(a => a.category == c);
                if (n == 0) continue;
                if (DrawCategoryButton(new Rect(rect.x, y, rect.width, 26f), ("CC_AlmanacCat_" + c).Translate(), n, category == c))
                {
                    category = c;
                    listScroll = Vector2.zero;
                }
                y += 28f;
            }
            y += 8f;
            string allLabel = "CC_Almanac_AllDlc".Translate();
            float allH = Mathf.Max(24f, Text.CalcHeight(allLabel, rect.width - 30f));
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, allH), allLabel, ref showAllDlc);
        }

        private static bool DrawCategoryButton(Rect r, string label, int count, bool active)
        {
            if (active) Widgets.DrawHighlightSelected(r);
            else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            Widgets.Label(new Rect(r.x + 6f, r.y + 3f, r.width - 40f, r.height), label.Truncate(r.width - 40f));
            GUI.color = Faint;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(r.x, r.y + 3f, r.width - 6f, r.height), count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
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
            string key = (category?.ToString() ?? "") + "|" + search + "|" + view.width + "|" + Visible.Count();
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
                    float th = Text.CalcHeight(a.LabelCap, view.width - 8f);
                    Text.Font = GameFont.Tiny;
                    float sh = a.summary.NullOrEmpty() ? 0f : Text.CalcHeight(a.summary, view.width - 8f);
                    listHeights.Add((th, sh));
                }
                Text.Font = GameFont.Small;
            }
            var articles = listArticles;
            Widgets.BeginScrollView(inner, ref listScroll, view);
            float y = 0f;
            if (articles.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(4f, y, view.width, 40f), "CC_Almanac_NothingFound".Translate());
                GUI.color = Color.white;
                y += 40f;
            }
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
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(4f, y + 3f, view.width - 8f, th), title);
                if (sh > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = Faint;
                    Widgets.Label(new Rect(4f, y + 3f + th, view.width - 8f, sh), sub);
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
                GUI.color = Faint;
                Widgets.Label(rect, "CC_Almanac_Pick".Translate());
                GUI.color = Color.white;
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
            Text.Font = GameFont.Tiny;
            GUI.color = Faint;
            string meta = ("CC_AlmanacCat_" + a.category).Translate();
            if (!a.dlc.NullOrEmpty()) meta += "  ·  " + a.dlc + (a.DlcActive ? "" : " (" + "CC_Almanac_NotActive".Translate() + ")");
            if (!a.mods.NullOrEmpty()) meta += "  ·  " + "CC_Almanac_FromMod".Translate(a.ModNames()).ToString() + (a.ModsActive ? "" : " (" + "CC_Almanac_NotActive".Translate().ToString() + ")");
            if (a.approximate) meta += "  ·  " + "CC_Almanac_Approximate".Translate();
            Widgets.Label(new Rect(0f, y, w, 18f), meta);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 22f;

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
