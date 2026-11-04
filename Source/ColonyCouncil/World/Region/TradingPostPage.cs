using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the colony sees its trading posts (World/Region/TradingPost.cs): the region window's page "trading posts"
    /// (each post standing near a home: whose, where, since when, its keeper, a warning given, a way to look at it; and
    /// how the last ones went, from the history) and a line on This week while a post's keeper has warned it will close.
    /// The region map's panel for a post is TradingPostWords'. Built from the records each time it draws (a handful of
    /// lines), the page at most once a second; nothing when the window is closed.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TradingPostPage
    {
        public const string Name = "TradingPosts";

        private static readonly Color Accent = new Color(0.86f, 0.78f, 0.52f);
        private static Vector2 scroll;
        private static float height = 600f;

        private sealed class Row
        {
            public Map home;
            public TradingPost post;
            public string title;
            public List<string> lines = new List<string>();
        }

        private static List<Row> rows = new List<Row>();
        private static List<string> past = new List<string>();
        private static float builtAt = -1f;

        static TradingPostPage()
        {
            MainTabWindow_World.Register(Name, "CC_Tab_TradingPosts", Icons.Trader, Draw, () => TradingPosts.On || TradingPosts.All().Count > 0, 54f);
        }

        private static string Title(TradingPost p) => "CC_PostPg_Title".Translate((p.name ?? "?").Named("POST"), (p.people?.Name ?? "?").Named("PEOPLE")).Resolve();

        // ---------------- The page ----------------

        public static void Draw(Rect rect)
        {
            if (Time.realtimeSinceStartup - builtAt > 1f)
            {
                try
                {
                    Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trading posts page", e);
                    rows = new List<Row>();
                    past = new List<string>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            string stat = rows.Count > 0 ? "CC_PostPg_Stat".Translate(rows.Count.Named("COUNT")).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Trader, "CC_PostPg_Header".Translate(), "CC_PostPg_Sub".Translate(), stat, UiKit.Good);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (!TradingPosts.On) y += UiKit.Paragraph(0f, y, w, "CC_PostPg_Off".Translate(), UiKit.Faint) + 10f;
                y += UiKit.SectionTitle(0f, y, w, "CC_PostPg_Standing".Translate(), Icons.Trader, Accent);
                if (rows.Count == 0)
                {
                    y += UiKit.EmptyState(0f, y, w, Icons.Trader, "CC_PostPg_None".Translate(), 60f);
                    y += UiKit.Paragraph(0f, y, w, "CC_PostPg_Rules".Translate(), UiKit.Faint) + 8f;
                }
                else
                    foreach (var r in rows)
                        y += DrawRow(0f, y, w, r) + 8f;
                y += 8f;
                y += UiKit.SectionTitle(0f, y, w, "CC_PostPg_Past".Translate(), Icons.History ?? Icons.Chronicle, Accent);
                if (past.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_PostPg_NoPast".Translate(), UiKit.Faint) + 4f;
                else
                    foreach (var line in past)
                        y += UiKit.Paragraph(0f, y, w, line, Color.white) + 4f;
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static void Build()
        {
            var list = new List<Row>();
            foreach (var (home, p) in TradingPosts.All())
                list.Add(new Row { home = home, post = p, title = Title(p), lines = TradingPostWords.Lines(home, p, true) });
            rows = list;
            // How the posts of every home opened, were warned, sacked and closed, newest first, from the history.
            var seen = new HashSet<int>();
            var entries = new List<HistoryEntry>();
            foreach (var home in Homes.All)
                foreach (var e in History.At(Refs.Colony(home), new[] { TradingPostMath.Family }, int.MinValue, 12))
                    if (e != null && seen.Add(e.id))
                        entries.Add(e);
            past = entries.OrderByDescending(e => e.tick).Take(12)
                .Select(e => "CC_PostPg_PastLine".Translate(FactionMemory.AgoText(e.tick).CapitalizeFirst().Named("AGO"), (History.Text(e) ?? e.kind).Named("TEXT")).Resolve())
                .ToList();
        }

        private const float ButtonW = 150f;

        private static float DrawRow(float x, float y, float w, Row r)
        {
            float textW = w - ButtonW - 24f;
            Text.Font = GameFont.Small;
            float h = 10f + Text.CalcHeight(r.title, textW) + 4f;
            foreach (var l in r.lines) h += Text.CalcHeight(l, textW) + 2f;
            h = Mathf.Max(h + 6f, 80f);
            var card = new Rect(x, y, w, h);
            bool warned = r.post.warnedAt >= 0 || r.post.closing >= 0;
            UiKit.Card(card, warned ? UiKit.Warn : UiKit.Good);
            float ty = y + 8f;
            ty += UiKit.Paragraph(x + 12f, ty, textW, r.title, Accent) + 4f;
            foreach (var l in r.lines) ty += UiKit.Paragraph(x + 12f, ty, textW, l, Color.white) + 2f;
            var town = TradingPosts.TownOf(r.post);
            if (town != null && Widgets.ButtonText(new Rect(x + w - ButtonW - 10f, y + 10f, ButtonW, 28f), "CC_PostPg_Show".Translate()))
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(town);
            }
            return h;
        }

        // ---------------- This week ----------------

        /// <summary>A line for each post whose keeper has warned it will close unless things mend (UI/WeekModel.cs): no timer, what would mend it.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null || !TradingPosts.On) return;
            foreach (var (home, p) in TradingPosts.All())
            {
                if (p.warnedAt < 0 && p.closing < 0) continue;
                var town = TradingPosts.TownOf(p);
                string text = "CC_PostPg_Week".Translate((p.name ?? "?").Named("POST"), (p.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Trader,
                    color = UiKit.Warn,
                    text = text,
                    note = (p.closing >= 0 ? "CC_PostPg_WeekClosing" : "CC_PostPg_WeekWarned").Translate().Resolve(),
                    noteColor = UiKit.Warn,
                    tip = string.Join("\n", TradingPostWords.Lines(home, p, false)),
                    click = town != null ? WeekModel.Go(new GlobalTargetInfo(town)) : WeekModel.Page(Name),
                    order = -1
                });
            }
        }
    }
}
