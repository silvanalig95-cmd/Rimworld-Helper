using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The Peoples view's left column (docs/design/diplomacy-screen.md, step A, point 1): a search, the filters as chips
    /// with their counts (neighbours, mail, dealings), the sort (closest first by default; regard, name, age), then every
    /// people in its group (allies, at peace, at war), each row its picture, name, stance in a word with its dot, a small
    /// regard meter, its nearest town's days by the post's reach rule, a mark for mail and its age; at the top a row for all
    /// peoples at once; at the foot the small web of who stands with whom. A click on a row chooses the people for the page
    /// beside it and lights its towns on the region map. Rows are drawn only in sight; the web's lines are found when the
    /// list is built.
    /// </summary>
    public static class PeoplesList
    {
        private static Vector2 scroll;
        private static float height = 400f;
        private static readonly Folds folds = new Folds();
        private static readonly string[] GroupKeys = { "group0", "group1", "group2" };
        private const string SearchControl = "RimfolkPeoplesSearch";

        // ================= The column =================

        public static void Draw(Rect rect)
        {
            bool compact = PeoplesViewMath.Compact(rect.width);
            float y = rect.y;
            y = Search(new Rect(rect.x, y, rect.width, 24f)) + 4f;
            y = Chips(new Rect(rect.x, y, rect.width, UiKit.ChipHeight)) + 4f;
            y = Sort(new Rect(rect.x, y, rect.width, 24f), compact) + 6f;
            bool webFixed = PeoplesViewMath.WebFixed(rect.height);
            float bottom = webFixed ? rect.yMax - PeoplesViewMath.WebH - 6f : rect.yMax;
            var outRect = new Rect(rect.x, y, rect.width, Mathf.Max(40f, bottom - y));
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float ly = Rows(view.width, outRect.height, compact);
                if (!webFixed) ly = Web(new Rect(0f, ly + 6f, view.width, PeoplesViewMath.WebH)) + 6f;
                height = Mathf.Max(ly + 6f, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            if (webFixed) Web(new Rect(rect.x, rect.yMax - PeoplesViewMath.WebH, rect.width, PeoplesViewMath.WebH));
        }

        // ---------------- Search, filters, sort ----------------

        private static float Search(Rect r)
        {
            if (UiKit.OwnLook) return OwnSearch(r);
            UiKit.Icon(new Rect(r.x + 2f, r.y + 5f, 14f, 14f), Icons.Search, UiKit.Faint);
            var field = new Rect(r.x + 20f, r.y, r.width - (RegionView.query.NullOrEmpty() ? 20f : 44f), r.height);
            GUI.SetNextControlName(SearchControl);
            string q = Widgets.TextField(field, RegionView.query ?? "");
            if (q != RegionView.query) RegionView.query = q.Length > 40 ? q.Substring(0, 40) : q;
            if (RegionView.query.NullOrEmpty() && GUI.GetNameOfFocusedControl() != SearchControl)
                UiKit.LabelFit(new Rect(field.x + 6f, field.y, field.width - 8f, field.height), SearchHint, UiKit.Dim, GameFont.Tiny);
            if (!RegionView.query.NullOrEmpty())
            {
                var clear = new Rect(r.xMax - 22f, r.y + 1f, 22f, 22f);
                if (Widgets.ButtonImage(clear.ContractedBy(3f), TexButton.CloseXSmall))
                {
                    RegionView.query = "";
                    GUI.FocusControl(null);
                }
                TooltipHandler.TipRegion(clear, ClearTip);
            }
            return r.yMax;
        }

        /// <summary>The search in Rimfolk's own look: one rounded field with the icon in it, its edge lit while typing, a clear button.</summary>
        private static float OwnSearch(Rect r)
        {
            bool has = !RegionView.query.NullOrEmpty();
            string q = UiKit.TextField(new Rect(r.x, r.y, r.width, r.height), RegionView.query ?? "", SearchControl, null);
            if (q != RegionView.query) RegionView.query = q.Length > 40 ? q.Substring(0, 40) : q;
            bool focused = GUI.GetNameOfFocusedControl() == SearchControl;
            UiKit.Icon(new Rect(r.xMax - (has ? 40f : 20f), r.y + (r.height - 14f) / 2f, 14f, 14f), Icons.Search, focused ? UiKit.Accent : UiKit.Faint);
            if (!has && !focused) UiKit.LabelFit(new Rect(r.x + 8f, r.y, r.width - 30f, r.height), SearchHint, UiKit.Dim, GameFont.Tiny);
            if (has)
            {
                var clear = new Rect(r.xMax - 22f, r.y + (r.height - 20f) / 2f, 20f, 20f);
                if (UiKit.IconPlate(clear, TexButton.CloseXSmall, ClearTip))
                {
                    RegionView.query = "";
                    GUI.FocusControl(null);
                }
            }
            return r.yMax;
        }

        private static float Chips(Rect r)
        {
            float x = r.x;
            var labels = FilterLabels;
            for (int i = 0; i < PeoplesViewMath.Filters.Length; i++)
            {
                var f = PeoplesViewMath.Filters[i];
                int n = PeoplesModel.counts[i];
                float cw = Mathf.Min(UiKit.FilterChipWidth(labels[i], n), (r.width - 8f) / 3f);
                bool on = (RegionView.filters & f) != 0;
                if (UiKit.FilterChip(new Rect(x, r.y, cw, r.height), labels[i], n, on, FilterTips[i]))
                {
                    RegionView.filters ^= f;
                    scroll = Vector2.zero;
                }
                x += cw + 4f;
            }
            return r.yMax;
        }

        private static float Sort(Rect r, bool compact)
        {
            var labels = SortLabels;
            if (compact)
            {
                if (UiKit.Button(r, UiKit.Fit("CC_PplV_SortBy".Translate(labels[(int)RegionView.sort].Named("SORT")).Resolve(), r.width - 10f)))
                {
                    var opts = new List<FloatMenuOption>();
                    for (int i = 0; i < labels.Length; i++)
                    {
                        var s = PeoplesViewMath.Sorts[i];
                        opts.Add(new FloatMenuOption(labels[i], () => RegionView.sort = s));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                return r.yMax;
            }
            int pick = UiKit.Modes(r, labels, (int)RegionView.sort, null, SortTips);
            if (pick >= 0) RegionView.sort = PeoplesViewMath.Sorts[pick];
            return r.yMax;
        }

        // ---------------- The rows ----------------

        private static float Rows(float w, float viewH, bool compact)
        {
            float y = 0f;
            // Every people at once: what is owed each way and what each remembers.
            AllRow(new Rect(0f, y, w, 30f));
            y += 34f;
            int shown = 0;
            for (int g = 0; g < PeoplesViewMath.Groups.Length; g++)
            {
                var list = PeoplesModel.groups[g];
                if (list.Count == 0) continue;
                y += UiKit.SectionHeading(0f, y, w, GroupLabels[g], list.Count, folds, GroupKeys[g], out bool open);
                if (!open) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    // Only the rows in sight are drawn.
                    if (y + PeoplesViewMath.RowH >= scroll.y && y <= scroll.y + viewH) Row(new Rect(0f, y, w, PeoplesViewMath.RowH), PeoplesModel.entries[list[i]], i, compact);
                    y += PeoplesViewMath.RowH;
                    shown++;
                }
                y += 6f;
            }
            if (shown == 0)
                y += UiKit.EmptyState(0f, y, w, Icons.People, (PeoplesModel.entries.Count == 0 ? "CC_Peoples_None" : "CC_PplV_NoneFits").Translate().Resolve(), 70f);
            return y;
        }

        private static void AllRow(Rect r)
        {
            bool on = RegionView.chosen == RegionView.AllPeoples;
            if (UiKit.OwnLook)
            {
                if (on) UiKit.RowLit(r, true);
                else UiKit.RoundBox(r, Mouse.IsOver(r) ? UiKit.RaisedHover : UiKit.Raised, UiKit.Edge, SkinMath.RadiusMedium);
            }
            else
            {
                Widgets.DrawBoxSolid(r, on ? UiKit.CardSelected : Mouse.IsOver(r) ? UiKit.CardHover : UiKit.CardFill);
                if (on) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), UiKit.Gold);
            }
            UiKit.Icon(new Rect(r.x + 8f, r.y + 7f, 16f, 16f), Icons.People, on ? UiKit.Gold : UiKit.Faint);
            UiKit.LabelFit(new Rect(r.x + 32f, r.y, r.width - 40f, r.height), AllLabel, on ? UiKit.Gold : UiKit.Ink);
            TooltipHandler.TipRegion(r, AllTip);
            if (!Widgets.ButtonInvisible(r, false)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            RegionView.chosen = RegionView.AllPeoples;
            RegionView.lit = null;
        }

        /// <summary>A people's row: picture, name, days to their nearest town and mail on the first line; stance, age, regard on the second.</summary>
        private static void Row(Rect r, PeopleEntry e, int index, bool compact)
        {
            bool on = RegionView.chosen == e.f.loadID;
            if (on) UiKit.RowLit(r, true);
            else
            {
                UiKit.Zebra(r, index);
                UiKit.RowHover(r);
            }
            UiKit.Picture(new Rect(r.x + 6f, r.y + 8f, 24f, 24f), null, e.f, Icons.People, UiKit.Faint);
            float x = r.x + 36f, right = r.xMax - 4f;
            // The first line: their name, and at the right their nearest town's days and a mark for mail.
            float dw = Mathf.Min(UiKit.KeptWidth(e.daysWord, GameFont.Tiny) + 4f, 70f);
            UiKit.LabelFit(new Rect(right - dw, r.y + 2f, dw, 20f), e.daysWord, e.facts.days >= 0f ? UiKit.Faint : UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
            right -= dw + 4f;
            if (e.mail > 0)
            {
                UiKit.Icon(new Rect(right - 14f, r.y + 5f, 14f, 14f), Icons.Inbox, UiKit.Gold);
                right -= 18f;
            }
            UiKit.LabelFit(new Rect(x, r.y + 2f, Mathf.Max(0f, right - x), 20f), e.name, on ? UiKit.Gold : Color.white);
            // The second line: the stance's dot and word, the age; the regard meter at the right.
            float y2 = r.y + 21f;
            float mw = compact ? 34f : 52f;
            var meter = new Rect(r.xMax - 4f - mw, y2 + 6f, mw, 5f);
            if (e.facts.hasGoodwill && !e.facts.permanentEnemy) UiKit.Bar(meter, PeoplesViewMath.RegardFill(e.facts.goodwill), e.stanceColor);
            float mx = meter.x - 4f;
            if (e.trend != 0)
            {
                UiKit.Icon(new Rect(mx - 10f, y2 + 3f, 10f, 10f), e.trend > 0 ? Icons.TrendUp : Icons.TrendDown, e.trend > 0 ? UiKit.Good : UiKit.Bad);
                mx -= 14f;
            }
            UiKit.DiscFill(new Rect(x + 1f, y2 + 5f, 7f, 7f), e.stanceColor);
            string second = compact ? e.stance : e.second;
            UiKit.LabelFit(new Rect(x + 12f, y2, Mathf.Max(0f, mx - x - 12f), 16f), second, UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, new TipSignal(() => RowTip(e), e.f.loadID ^ 0x51C3));
            if (!Widgets.ButtonInvisible(r, false)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            PeoplesView.Choose(e.f);
        }

        private static string RowTip(PeopleEntry e)
        {
            var parts = new List<string> { e.name, e.tip };
            if (!e.heading.NullOrEmpty()) parts.Add(e.heading.CapitalizeFirst() + ".");
            if (e.mail > 0) parts.Add("CC_PplV_MailTip".Translate(e.mail.Named("COUNT")).Resolve());
            return string.Join("\n", parts);
        }

        // ================= The web =================

        private sealed class Node
        {
            public PeopleEntry e;
            public Vector2 at;
        }

        private static readonly List<Node> nodes = new List<Node>();
        private static readonly List<(int a, int b, WebTie tie)> ties = new List<(int, int, WebTie)>();
        private static List<int>[] webFor;
        private static Rect webRect;

        /// <summary>
        /// Who stands with whom: the first peoples of the list round an ellipse, the colony in gold at its middle, a line in
        /// warm green for allies and in red for a war between them (and from the colony to its own allies and enemies); a
        /// people at war with everyone has a red ring instead of lines. A dot's name on hover; a click chooses it.
        /// </summary>
        private static float Web(Rect r)
        {
            UiKit.Divider(r.x, r.y, r.width);
            UiKit.LabelFit(new Rect(r.x, r.y + 2f, r.width, 18f), WebTitle, UiKit.Heading, GameFont.Tiny);
            var area = new Rect(r.x + 14f, r.y + 26f, r.width - 28f, r.height - 52f);
            if (webFor != PeoplesModel.groups || webRect != area) LayOut(area);
            var e0 = Event.current;
            if (e0.type == EventType.Repaint)
            {
                var c = area.center;
                foreach (var (a, b, tie) in ties)
                {
                    var pa = a < 0 ? c : nodes[a].at;
                    var pb = nodes[b].at;
                    var col = tie == WebTie.Allies ? UiKit.WithAlpha(UiKit.Good, 0.75f) : UiKit.WithAlpha(UiKit.Bad, 0.6f);
                    Widgets.DrawLine(pa, pb, col, tie == WebTie.Allies ? 2f : 1f);
                }
                UiKit.DiscFill(new Rect(c.x - 5f, c.y - 5f, 10f, 10f), UiKit.Gold);
            }
            Node hover = null;
            foreach (var n in nodes)
            {
                bool on = RegionView.chosen == n.e.f.loadID;
                float s = on ? 12f : 9f;
                var dot = new Rect(n.at.x - s / 2f, n.at.y - s / 2f, s, s);
                if (e0.type == EventType.Repaint)
                {
                    Color fc;
                    try
                    {
                        fc = n.e.f.Color;
                    }
                    catch
                    {
                        fc = UiKit.Faint;
                    }
                    UiKit.DiscFill(dot.ExpandedBy(1.5f), UiKit.Ground);
                    UiKit.DiscFill(dot, fc);
                    if (n.e.facts.permanentEnemy) UiKit.RingDraw(dot.ExpandedBy(3f), UiKit.Bad);
                    if (on) UiKit.RingDraw(dot.ExpandedBy(4f), UiKit.Gold);
                }
                var hit = dot.ExpandedBy(4f);
                if (Mouse.IsOver(hit)) hover = n;
                TooltipHandler.TipRegion(hit, new TipSignal(n.e.name + "\n" + n.e.stance, n.e.f.loadID ^ 0x2B07));
                if (Widgets.ButtonInvisible(hit, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    PeoplesView.Choose(n.e.f);
                }
            }
            // The chosen people's name (or the one under the mouse) beside its dot.
            var named = hover ?? nodes.Find(n => RegionView.chosen == n.e.f.loadID);
            if (named != null && e0.type == EventType.Repaint)
            {
                float lw = Mathf.Min(UiKit.KeptWidth(named.e.name, GameFont.Tiny) + 8f, area.width * 0.7f);
                float lx = Mathf.Clamp(named.at.x - lw / 2f, r.x, r.xMax - lw);
                var lr = new Rect(lx, named.at.y + 7f, lw, 15f);
                Widgets.DrawBoxSolid(lr, UiKit.Shade);
                UiKit.LabelFit(lr, named.e.name, Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
            UiKit.LabelFit(new Rect(r.x, r.yMax - 22f, r.width, 20f), WebLegend, UiKit.Dim, GameFont.Tiny);
            return r.yMax;
        }

        /// <summary>The web's dots and lines, found again when the list or the web's place changes (never per frame).</summary>
        private static void LayOut(Rect area)
        {
            webFor = PeoplesModel.groups;
            webRect = area;
            nodes.Clear();
            ties.Clear();
            var order = new List<PeopleEntry>();
            foreach (var g in PeoplesModel.groups)
                foreach (int i in g)
                    if (order.Count < PeoplesViewMath.WebMost)
                        order.Add(PeoplesModel.entries[i]);
            for (int i = 0; i < order.Count; i++)
            {
                PeoplesViewMath.WebPoint(i, order.Count, area.center.x, area.center.y, area.width / 2f, area.height / 2f, out float x, out float y);
                nodes.Add(new Node { e = order[i], at = new Vector2(x, y) });
            }
            try
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var a = nodes[i].e;
                    // The colony's own allies and enemies, from its middle.
                    var own = PeoplesViewMath.TieOf(a.facts.relation, a.facts.permanentEnemy, false);
                    if (own != WebTie.None) ties.Add((-1, i, own));
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var b = nodes[j].e;
                        // The game's relation between the two, quietly (a pair with none is merely at peace).
                        var k = a.f.RelationWith(b.f, true)?.kind ?? FactionRelationKind.Neutral;
                        int rel = k == FactionRelationKind.Ally ? 1 : k == FactionRelationKind.Hostile ? -1 : 0;
                        var tie = PeoplesViewMath.TieOf(rel, a.facts.permanentEnemy, b.facts.permanentEnemy);
                        if (tie != WebTie.None) ties.Add((j, i, tie));
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: the web of peoples", e);
            }
        }

        // ================= Words, made once =================

        private static string searchHint, clearTip, allLabel, allTip, webTitle, webLegend;
        private static string[] filterLabels, filterTips, sortLabels, sortTips, groupLabels;

        private static string SearchHint => searchHint ??= "CC_PplV_Search".Translate();
        private static string ClearTip => clearTip ??= "CC_PplV_SearchClear".Translate();
        private static string AllLabel => allLabel ??= "CC_PplV_All".Translate();
        private static string AllTip => allTip ??= "CC_PplV_AllTip".Translate();
        private static string WebTitle => webTitle ??= "CC_PplV_Web".Translate();
        private static string WebLegend => webLegend ??= "CC_PplV_WebLegend".Translate();

        private static string[] FilterLabels => filterLabels ??= Array.ConvertAll(PeoplesViewMath.Filters, f => ("CC_PplV_Filter_" + f).Translate().ToString());
        private static string[] FilterTips => filterTips ??= Array.ConvertAll(PeoplesViewMath.Filters, f => ("CC_PplV_Filter_" + f + "_Tip").Translate().ToString());
        private static string[] SortLabels => sortLabels ??= Array.ConvertAll(PeoplesViewMath.Sorts, s => ("CC_PplV_Sort_" + s).Translate().ToString());
        private static string[] SortTips => sortTips ??= Array.ConvertAll(PeoplesViewMath.Sorts, s => ("CC_PplV_Sort_" + s + "_Tip").Translate().ToString());
        private static string[] GroupLabels => groupLabels ??= Array.ConvertAll(PeoplesViewMath.Groups, g => ("CC_PplV_Group_" + g).Translate().ToString());
    }
}
