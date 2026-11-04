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
    /// Where the colony sees its raiders' camps (World/Region/RaidersCamp.cs): the region window's page "raiders' camps"
    /// (each camp standing near a home, where, since when, its raids, its price with a way to pay it, and how the last
    /// ones ended, from the history), the region map's panel for the camp's site and for the home it threatens, and a line
    /// on This week while one stands. A camp not yet found shows nowhere. Built from the records each time it draws (a
    /// handful of lines), the page at most once a second; nothing when the window is closed.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RaidersCampPage
    {
        public const string Name = "RaidersCamps";

        private static readonly Color Accent = new Color(0.93f, 0.55f, 0.45f);
        private static Vector2 scroll;
        private static float height = 600f;

        private sealed class Row
        {
            public Map home;
            public RaidersCamp camp;
            public string title;
            public List<string> lines = new List<string>();
        }

        private static List<Row> rows = new List<Row>();
        private static List<string> past = new List<string>();
        private static float builtAt = -1f;

        static RaidersCampPage()
        {
            MainTabWindow_World.Register(Name, "CC_Tab_RaidersCamps", Icons.Raid, Draw, () => RaidersCamps.On || RaidersCamps.Known().Count > 0, 55f);
            NeighbourPanel.PlaceSections.Add(DrawSite);
            NeighbourPanel.HomeSections.Add(DrawHome);
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Words ----------------

        /// <summary>"A brigands' hold of the Kotar" with its leader, where it lies from its home, since when, its raids, its price.</summary>
        public static List<string> Lines(Map home, RaidersCamp c, bool withHome)
        {
            var lines = new List<string>();
            if (home == null || c == null || !c.Shown) return lines;
            var people = c.people?.Name ?? "?";
            string where = RaidersCamps.Where(home, c);
            lines.Add(withHome && Homes.All.Count > 1
                ? "CC_RaidCampPg_WhereHome".Translate(where.Named("WHERE"), Homes.Name(home).Named("HOME")).Resolve().CapitalizeFirst()
                : where.CapitalizeFirst() + ".");
            lines.Add("CC_RaidCampPg_Since".Translate(FactionMemory.AgoText(c.foundAt).Named("FOUND"), people.Named("PEOPLE")).Resolve());
            if (c.leader != null && !RaidersCamps.LeaderLost(c)) lines.Add("CC_RaidCampPg_Leader".Translate(c.leader.LabelShort.Named("LEADER")).Resolve());
            lines.Add(c.raids == 0 ? "CC_RaidCampPg_NoRaids".Translate().Resolve()
                : "CC_RaidCampPg_Raids".Translate(c.raids.Named("RAIDS"), c.beaten.Named("BEATEN")).Resolve());
            if (c.stage == CampStage.Paying) lines.Add("CC_RaidCampPg_Paying".Translate(c.price.Named("SILVER")).Resolve());
            else if (c.offered) lines.Add("CC_RaidCampPg_Price".Translate(c.price.Named("SILVER")).Resolve());
            else lines.Add("CC_RaidCampPg_NoTerms".Translate().Resolve());
            return lines;
        }

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
                    CouncilLog.ErrorOnce("raiders' camps page", e);
                    rows = new List<Row>();
                    past = new List<string>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            string stat = rows.Count > 0 ? "CC_RaidCampPg_Stat".Translate(rows.Count.Named("COUNT")).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Raid, "CC_RaidCampPg_Header".Translate(), "CC_RaidCampPg_Sub".Translate(), stat, UiKit.Bad);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (!RaidersCamps.On) y += UiKit.Paragraph(0f, y, w, "CC_RaidCampPg_Off".Translate(), UiKit.Faint) + 10f;
                y += UiKit.SectionTitle(0f, y, w, "CC_RaidCampPg_Standing".Translate(), Icons.Raid, Accent);
                if (rows.Count == 0)
                {
                    y += UiKit.EmptyState(0f, y, w, Icons.Raid, "CC_RaidCampPg_None".Translate(), 60f);
                    y += UiKit.Paragraph(0f, y, w, "CC_RaidCampPg_Rules".Translate(), UiKit.Faint) + 8f;
                }
                else
                    foreach (var r in rows)
                        y += DrawRow(0f, y, w, r) + 8f;
                y += 8f;
                y += UiKit.SectionTitle(0f, y, w, "CC_RaidCampPg_Past".Translate(), Icons.History ?? Icons.Chronicle, Accent);
                if (past.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_RaidCampPg_NoPast".Translate(), UiKit.Faint) + 4f;
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
            foreach (var (home, c) in RaidersCamps.Known())
                list.Add(new Row { home = home, camp = c, title = "CC_RaidCampPg_Title".Translate(c.label.Named("NAME"), (c.people?.Name ?? "?").Named("PEOPLE")).Resolve(), lines = Lines(home, c, true) });
            rows = list;
            // How the camps of every home ended (and were found, raided, put back), newest first, from the history.
            var seen = new HashSet<int>();
            var entries = new List<HistoryEntry>();
            foreach (var home in Homes.All)
                foreach (var e in History.At(Refs.Colony(home), new[] { "world:camp" }, int.MinValue, 12))
                    if (e != null && seen.Add(e.id) && e.kind != RaidersCampMath.KindRaid)
                        entries.Add(e);
            past = entries.OrderByDescending(e => e.tick).Take(12)
                .Select(e => "CC_RaidCampPg_PastLine".Translate(FactionMemory.AgoText(e.tick).CapitalizeFirst().Named("AGO"), (History.Text(e) ?? e.kind).Named("TEXT")).Resolve())
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
            UiKit.Card(card, UiKit.Bad);
            float ty = y + 8f;
            ty += UiKit.Paragraph(x + 12f, ty, textW, r.title, Accent) + 4f;
            foreach (var l in r.lines) ty += UiKit.Paragraph(x + 12f, ty, textW, l, Color.white) + 2f;
            float bx = x + w - ButtonW - 10f, by = y + 10f;
            var site = RaidersCamps.SiteOf(r.camp);
            if (site != null && Widgets.ButtonText(new Rect(bx, by, ButtonW, 28f), "CC_RaidCampPg_Show".Translate()))
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(site);
            }
            by += 34f;
            if (r.camp.offered && r.camp.stage == CampStage.Found)
            {
                var b = new Rect(bx, by, ButtonW, 28f);
                if (Widgets.ButtonText(b, "CC_RaidCamp_Pay".Translate(r.camp.price.Named("SILVER")).Resolve())) AskToPay(r.home, r.camp);
                TooltipHandler.TipRegion(b, "CC_RaidCampPg_PayTip".Translate());
            }
            return h;
        }

        /// <summary>The player's click: pay their price now, after a word to confirm; the answer to the click itself is a direct message.</summary>
        private static void AskToPay(Map home, RaidersCamp c)
        {
            string why = RaidersCampTerms.CantPay(home, c);
            if (why != null)
            {
                // direct: the answer to the player's own click.
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            string text = "CC_RaidCampPg_PayConfirm".Translate(c.kind.Named("KIND"), c.price.Named("SILVER")).Resolve();
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
            {
                var now = RaidersCamps.CampOf(home);
                if (now == c) RaidersCamps.Pay(home, c);
                builtAt = -1f;
            }));
        }

        // ---------------- The region map's panel ----------------

        private static float DrawSite(Rect rect, PlanetTile tile, WorldObject wo)
        {
            if (!(wo is Site s)) return 0f;
            try
            {
                var c = RaidersCamps.CampAt(s, out var home);
                if (c == null) return 0f;
                var lines = Lines(home, c, true);
                lines.Insert(0, "CC_RaidCampPg_Title".Translate(c.label.Named("NAME"), (c.people?.Name ?? "?").Named("PEOPLE")).Resolve());
                return Panel(rect, lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a raiders' camp", e);
                return 0f;
            }
        }

        private static float DrawHome(Rect rect, Map home)
        {
            try
            {
                var c = RaidersCamps.CampOf(home);
                if (c == null || !c.Shown) return 0f;
                var lines = new List<string>
                {
                    "CC_RaidCampPg_HomeLine".Translate(c.kind.Named("KIND"), (c.people?.Name ?? "?").Named("PEOPLE"), RaidersCamps.Where(home, c).Named("WHERE")).Resolve(),
                    c.raids == 0 ? "CC_RaidCampPg_NoRaids".Translate().Resolve() : "CC_RaidCampPg_Raids".Translate(c.raids.Named("RAIDS"), c.beaten.Named("BEATEN")).Resolve()
                };
                return Panel(rect, lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a home's raiders' camp", e);
                return 0f;
            }
        }

        private static float Panel(Rect rect, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return 0f;
            float y = rect.y;
            UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), "CC_RaidCampPg_PanelHeader".Translate(), Accent);
            y += 24f;
            foreach (var l in lines) y += UiKit.Paragraph(rect.x, y, rect.width, l, Color.white, GameFont.Small) + 2f;
            return y + 2f - rect.y;
        }

        // ---------------- This week ----------------

        /// <summary>A line for each camp standing near a home, on the card of missions, camps and guests (UI/WeekModel.cs): no timer, its raids so far.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null) return;
            foreach (var (home, c) in RaidersCamps.Known())
            {
                var site = RaidersCamps.SiteOf(c);
                string text = "CC_RaidCampPg_Week".Translate(c.label.Named("NAME"), (c.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Raid,
                    color = UiKit.Bad,
                    text = text,
                    note = c.stage == CampStage.Paying ? "CC_RaidCampPg_WeekPaying".Translate().Resolve()
                        : c.raids == 0 ? "CC_RaidCampPg_WeekNoRaid".Translate().Resolve() : "CC_RaidCampPg_WeekRaids".Translate(c.raids.Named("RAIDS")).Resolve(),
                    noteColor = UiKit.Bad,
                    tip = string.Join("\n", Lines(home, c, false)),
                    click = site != null ? WeekModel.Go(new GlobalTargetInfo(site)) : WeekModel.Page(Name),
                    order = -2
                });
            }
        }
    }
}
