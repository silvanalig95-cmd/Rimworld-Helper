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
    /// Where the colony sees its hamlets (World/Region/Hamlet.cs): the region window's page "hamlets" (each home's hamlet or
    /// the settlers on their way: whose, where, since when, its head, its oath, what it asked lately, a way to look at it;
    /// and how the last ones went, from the history) and a line on This week while settlers wait at the gate for the
    /// colony's caravan (never with a timer). The region map's panel for a hamlet is HamletWords'. Built from the records
    /// each time it draws (a handful of lines), the page at most once a second; nothing when the window is closed.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HamletPage
    {
        public const string Name = "Hamlets";

        private static readonly Color Accent = new Color(0.62f, 0.80f, 0.55f);
        private static Vector2 scroll;
        private static float height = 600f;

        private sealed class Row
        {
            public Map home;
            public HamletHome h;
            public string title;
            public List<string> lines = new List<string>();
        }

        private static List<Row> rows = new List<Row>();
        private static List<string> past = new List<string>();
        private static float builtAt = -1f;

        static HamletPage()
        {
            MainTabWindow_World.Register(Name, "CC_Tab_Hamlets", Icons.Colony, Draw, () => Hamlets.On || Hamlets.All().Any(x => x.h.stage != HamletStage.None), 56f);
        }

        private static string Title(HamletHome h) =>
            HamletMath.Stands(h.stage) && !h.name.NullOrEmpty()
                ? "CC_HamletPg_Title".Translate(h.name.Named("HAMLET"), (h.people?.Name ?? "?").Named("PEOPLE")).Resolve()
                : "CC_HamletPg_TitleSettlers".Translate((h.people?.Name ?? "?").Named("PEOPLE")).Resolve();

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
                    CouncilLog.ErrorOnce("hamlets page", e);
                    rows = new List<Row>();
                    past = new List<string>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            string stat = rows.Count > 0 ? "CC_HamletPg_Stat".Translate(rows.Count.Named("COUNT")).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Colony, "CC_HamletPg_Header".Translate(), "CC_HamletPg_Sub".Translate(), stat, UiKit.Good);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (!Hamlets.On) y += UiKit.Paragraph(0f, y, w, "CC_HamletPg_Off".Translate(), UiKit.Faint) + 10f;
                y += UiKit.SectionTitle(0f, y, w, "CC_HamletPg_Now".Translate(), Icons.Colony, Accent);
                if (rows.Count == 0)
                {
                    y += UiKit.EmptyState(0f, y, w, Icons.Colony, "CC_HamletPg_None".Translate(), 60f);
                    y += UiKit.Paragraph(0f, y, w, "CC_HamletPg_Rules".Translate(), UiKit.Faint) + 8f;
                }
                else
                    foreach (var r in rows)
                        y += DrawRow(0f, y, w, r) + 8f;
                y += 8f;
                y += UiKit.SectionTitle(0f, y, w, "CC_HamletPg_Past".Translate(), Icons.History ?? Icons.Chronicle, Accent);
                if (past.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_HamletPg_NoPast".Translate(), UiKit.Faint) + 4f;
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
            foreach (var (home, h) in Hamlets.All())
            {
                if (h.stage == HamletStage.None || h.stage == HamletStage.Over) continue;
                list.Add(new Row { home = home, h = h, title = Title(h), lines = HamletWords.Lines(home, h, true) });
            }
            rows = list;
            var seen = new HashSet<int>();
            var entries = new List<HistoryEntry>();
            foreach (var home in Homes.All)
                foreach (var e in History.At(Refs.Colony(home), new[] { HamletMath.Family }, int.MinValue, 12))
                    if (e != null && seen.Add(e.id))
                        entries.Add(e);
            past = entries.OrderByDescending(e => e.tick).Take(12)
                .Select(e => "CC_HamletPg_PastLine".Translate(FactionMemory.AgoText(e.tick).CapitalizeFirst().Named("AGO"), (History.Text(e) ?? e.kind).Named("TEXT")).Resolve())
                .ToList();
        }

        private const float ButtonW = 150f;

        /// <summary>Where a row's button takes the player: the hamlet, the caravan they go with, their camp's leader, the place.</summary>
        private static GlobalTargetInfo Target(Row r)
        {
            var town = Hamlets.TownOf(r.h);
            if (town != null) return new GlobalTargetInfo(town);
            var c = r.h.stage == HamletStage.Escorted ? r.h.escort?.GetCaravan() : null;
            if (c != null) return new GlobalTargetInfo(c);
            var leader = r.h.leader;
            if (leader != null && leader.Spawned) return new GlobalTargetInfo(leader);
            var t = Hamlets.TileOf(r.h);
            return t.Valid ? new GlobalTargetInfo(t) : GlobalTargetInfo.Invalid;
        }

        private static float DrawRow(float x, float y, float w, Row r)
        {
            float textW = w - ButtonW - 24f;
            Text.Font = GameFont.Small;
            float h = 10f + Text.CalcHeight(r.title, textW) + 4f;
            foreach (var l in r.lines) h += Text.CalcHeight(l, textW) + 2f;
            h = Mathf.Max(h + 6f, 80f);
            var card = new Rect(x, y, w, h);
            UiKit.Card(card, HamletMath.Stands(r.h.stage) ? UiKit.Good : UiKit.Warn);
            float ty = y + 8f;
            ty += UiKit.Paragraph(x + 12f, ty, textW, r.title, Accent) + 4f;
            foreach (var l in r.lines) ty += UiKit.Paragraph(x + 12f, ty, textW, l, Color.white) + 2f;
            var target = Target(r);
            if (target.IsValid && Widgets.ButtonText(new Rect(x + w - ButtonW - 10f, y + 10f, ButtonW, 28f), "CC_HamletPg_Show".Translate()))
            {
                // On the region map with its card (a settler on the home map is jumped to, as before).
                Cards.ShowOnMap(target);
            }
            return h;
        }

        // ---------------- This week ----------------

        /// <summary>A line while settlers wait at the gate (or on the road below it) for the colony's caravan (UI/WeekModel.cs): no timer, what takes them along.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null || !Hamlets.On) return;
            foreach (var (home, h) in Hamlets.All())
            {
                if (h.stage != HamletStage.Waiting) continue;
                string text = "CC_HamletPg_Week".Translate((h.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                var leader = h.leader != null && h.leader.Spawned ? h.leader : null;
                var t = Hamlets.TileOf(h);
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Colony,
                    color = UiKit.Good,
                    text = text,
                    note = "CC_HamletPg_WeekNote".Translate().Resolve(),
                    noteColor = UiKit.Good,
                    tip = string.Join("\n", HamletWords.Lines(home, h, false)),
                    click = leader != null ? WeekModel.Go(new GlobalTargetInfo(leader)) : t.Valid ? WeekModel.Go(new GlobalTargetInfo(t)) : WeekModel.Page(Name),
                    order = -1
                });
            }
        }
    }
}
