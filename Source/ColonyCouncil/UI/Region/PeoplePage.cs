using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The chosen people's page beside the list (docs/design/diplomacy-screen.md, step A, point 2). Its header: picture and
    /// name, its age with which way it is heading (0.169.0), its stance in words, its kind, and its regard with the meter,
    /// this season's trend and the deeds behind it. Then five tabs: Overview (what they want and fear from what Rimfolk
    /// knows of them, how to reach them now, and "Do something with...": UI/Region/PeopleActs.cs), Towns (closest first,
    /// size, days, reach; a click shows the town on the region map), People (whom the colony knows of theirs by name, and
    /// who of theirs is at the colony), Dealings (ties, debts each way, what is under way: Diplomacy/DiplomacyPanels.cs) and
    /// History (what they remember, their timeline with the colony). With all peoples chosen it is what is owed each way
    /// and what every people remembers. Lines are links; their layouts are kept per width (UI/Cards/LinkText.cs).
    /// </summary>
    public static class PeoplePage
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static int scrollFor = int.MinValue;
        private static PeopleTab scrollTab;
        private static readonly Folds folds = new Folds("remember");

        public const float HeadH = 84f;

        // ================= The page =================

        public static void Draw(Rect rect, PeopleEntry e)
        {
            int key = e?.f.loadID ?? RegionView.AllPeoples;
            if (key != scrollFor || RegionView.tab != scrollTab)
            {
                scroll = Vector2.zero;
                scrollFor = key;
                scrollTab = RegionView.tab;
            }
            float top = rect.y;
            if (e != null)
            {
                top += Head(new Rect(rect.x, top, rect.width, HeadH), e);
                top += TabsRow(new Rect(rect.x, top, rect.width, UiKit.ModesHeight), e) + 6f;
            }
            var outRect = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = e == null ? AllPeoples(view.width) : Body(e, view.width, outRect.height);
                height = Mathf.Max(y + 10f, 100f);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("region window: a people's page", ex);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static float Body(PeopleEntry e, float w, float viewH)
        {
            switch (RegionView.tab)
            {
                case PeopleTab.Towns: return Towns(e, w, viewH);
                case PeopleTab.People: return People(e, w);
                case PeopleTab.Dealings: return Dealings(e, w);
                case PeopleTab.History: return History(e, w, viewH);
                default: return Overview(e, w, viewH);
            }
        }

        // ================= The header =================

        private static float Head(Rect r, PeopleEntry e)
        {
            UiKit.Picture(new Rect(r.x, r.y + 2f, 32f, 32f), null, e.f, Icons.People, UiKit.Faint);
            float x = r.x + 40f, right = r.xMax;
            // On the map: its towns lit there.
            var show = new Rect(right - 26f, r.y + 4f, 26f, 26f);
            if (UiKit.IconButton(show, Icons.ModeMap, ShowTip))
            {
                RegionView.lit = e.f;
                RegionView.list = false;
                Pages.Open(RegionView.Map);
            }
            right = show.x - 8f;
            // Their name, a link to their card in the drawer (drawn here in the view's title size).
            var nameRect = new Rect(x, r.y, Mathf.Max(0f, right - x), 34f);
            bool over = Mouse.IsOver(nameRect);
            UiKit.LabelFit(nameRect, e.name, over ? UiKit.LinkHot : UiKit.Link, GameFont.Medium);
            if (over) Previews.Hover(e.thing, nameRect);
            if (Widgets.ButtonInvisible(nameRect, false))
            {
                Cards.Click();
                Cards.Open(e.thing);
            }
            // Their age and which way it is heading, and their kind.
            float y = r.y + 36f;
            float cx = x + UiKit.StateChip(x, y, e.ageWord.CapitalizeFirst(), UiKit.Cool, 140f, AgeTip) + 6f;
            string line = (e.heading.NullOrEmpty() ? "" : e.heading.CapitalizeFirst() + " · ") + e.kind;
            UiKit.LabelFit(new Rect(cx, y, Mathf.Max(0f, r.xMax - cx), UiKit.ChipHeight), line, UiKit.Faint, GameFont.Tiny);
            if (!e.heading.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(cx, y, Mathf.Max(0f, r.xMax - cx), UiKit.ChipHeight), e.heading.CapitalizeFirst() + ".");
            // Their stance, their regard with its meter, this season's trend and why.
            y += 24f;
            cx = x + UiKit.StateChip(x, y, e.stance.CapitalizeFirst(), e.stanceColor, 160f, e.standing) + 8f;
            if (e.facts.hasGoodwill && !e.facts.permanentEnemy)
            {
                var bar = new Rect(cx, y + 7f, 90f, 6f);
                UiKit.Bar(bar, PeoplesViewMath.RegardFill(e.facts.goodwill), e.stanceColor);
                TooltipHandler.TipRegion(new Rect(cx, y, 90f, UiKit.ChipHeight), e.standing);
                cx += 96f;
                string gw = e.facts.goodwill.ToStringWithSign();
                UiKit.LabelFit(new Rect(cx, y, 34f, UiKit.ChipHeight), gw, UiKit.Faint, GameFont.Tiny);
                cx += 36f;
            }
            if (e.trend != 0)
            {
                UiKit.Icon(new Rect(cx, y + 4f, 12f, 12f), e.trend > 0 ? Icons.TrendUp : Icons.TrendDown, e.trend > 0 ? UiKit.Good : UiKit.Bad);
                cx += 16f;
            }
            string trend = e.trendLine;
            var tr = new Rect(cx, y, Mathf.Max(0f, r.xMax - cx), UiKit.ChipHeight);
            UiKit.LabelFit(tr, trend, e.trend > 0 ? UiKit.Good : e.trend < 0 ? UiKit.Bad : UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(tr, trend);
            UiKit.Divider(r.x, r.y + HeadH - 6f, r.width);
            return HeadH;
        }

        // ================= The tabs =================

        private static string[] tabLabels = new string[0];
        private static int tabTowns = -1, tabPeople = -1, tabDealings = -1;

        private static float TabsRow(Rect r, PeopleEntry e)
        {
            int towns = e.towns.Count, people = e.notables.Count, dealings = (e.dip == null ? 0 : e.dip.ties.Count + e.dip.underway.Count) + ColonyCouncil.Dealings.With(e.f).Count(d => d.Live);
            if (tabTowns != towns || tabPeople != people || tabDealings != dealings)
            {
                tabTowns = towns;
                tabPeople = people;
                tabDealings = dealings;
                tabLabels = new[]
                {
                    "CC_PplP_Tab_Overview".Translate().ToString(), Counted("CC_PplP_Tab_Towns", towns), Counted("CC_PplP_Tab_People", people),
                    Counted("CC_PplP_Tab_Dealings", dealings), "CC_PplP_Tab_History".Translate().ToString()
                };
            }
            int pick = UiKit.Modes(r, tabLabels, (int)RegionView.tab, null, TabTips);
            if (pick >= 0) RegionView.tab = PeoplesViewMath.Tabs[pick];
            return r.height;
        }

        private static string Counted(string key, int n) => n > 0 ? "CC_PplP_TabCount".Translate(key.Translate().Named("TAB"), n.Named("COUNT")).Resolve() : key.Translate().ToString();

        private static string[] tabTips;

        private static string[] TabTips => tabTips ??= Array.ConvertAll(PeoplesViewMath.Tabs, t => ("CC_PplP_Tab_" + t + "_Tip").Translate().ToString());

        // ================= Overview =================

        private sealed class Kept
        {
            public int people;
            public float at;
            public List<string> wants = new List<string>();
            public List<string> reach = new List<string>();
            public string here;
        }

        private static readonly Kept known = new Kept { people = int.MinValue };

        private static float Overview(PeopleEntry e, float w, float viewH)
        {
            float y = 0f;
            var wants = Wants(e);
            y += UiKit.SectionTitle(0f, y, w, WantsTitle, Icons.Heart, UiKit.Heading);
            foreach (var line in wants) y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), line, Color.white) + 3f;
            y += 10f;
            // How to reach them now: the post's reach line for their nearest town (Post/PostWords.cs).
            y += UiKit.SectionTitle(0f, y, w, ReachTitle, Icons.Journal, UiKit.Heading);
            if (!e.nearestLine.NullOrEmpty()) y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), e.nearestLine, UiKit.Faint, GameFont.Tiny) + 2f;
            foreach (var l in known.reach) y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), l, Color.white) + 2f;
            if (!known.here.NullOrEmpty()) y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), known.here, UiKit.StrangerColor) + 2f;
            y += 12f;
            // Do something with them: every act that fits, each with what it brings, how it travels and what it costs.
            y += UiKit.SectionTitle(0f, y, w, ActsTitle, Icons.Promise ?? Icons.Trust, UiKit.Heading);
            return PeopleActs.Draw(e, y, w, scroll.y, scroll.y + viewH);
        }

        /// <summary>
        /// What they want and fear, from what Rimfolk knows of them: what they honor and fear (a people of Rimfolk's), what
        /// they hope for at a feast (by their age), the hard times on them, the threats to them the colony knows of, whom they
        /// are at war with, and what they ask of the colony now. Never invented: a people the colony knows little of says so.
        /// </summary>
        private static List<string> Wants(PeopleEntry e)
        {
            float t = Time.realtimeSinceStartup;
            if (known.people == e.f.loadID && t - known.at < 2f && t >= known.at) return known.wants;
            known.people = e.f.loadID;
            known.at = t;
            var list = known.wants;
            list.Clear();
            known.reach.Clear();
            known.here = null;
            try
            {
                var home = PeoplesModel.Home;
                if (e.nearest != null && home != null) known.reach.AddRange(PostWords.ReachLines(home, e.nearest.s));
                if (known.reach.Count == 0) known.reach.Add(PeopleActs.ReachLine(home, e.nearest?.s, FavourCalls.HeadOf(e.f, home)) ?? "");
                var here = PeopleActs.Here(e);
                if (here.Count > 0)
                    known.here = "CC_PplP_HereNow".Translate(TextUtil.JoinAnd(here.Select(p => ThingRefs.Link(p.LabelShort, ThingRefs.Of(p))).ToList()).Named("WHO")).Resolve();
                var f = e.f;
                var ext = Peoples.Of(f);
                if (ext != null && !ext.honours.NullOrEmpty()) list.Add("CC_PplP_Honours".Translate(ext.honours.Trim().Named("HONORS")).Resolve());
                if (ext != null && !ext.fears.NullOrEmpty()) list.Add("CC_PplP_Fears".Translate(ext.fears.Trim().Named("FEARS")).Resolve());
                if (Feasts.On && !f.def.permanentEnemy)
                {
                    string hope = FeastWords.Hope(Feasts.HopeOf(f));
                    if (!hope.NullOrEmpty()) list.Add("CC_PplP_FeastHope".Translate(hope.Named("HOPE")).Resolve());
                }
                var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
                if (ws != null)
                    foreach (var s in ws.Running)
                        if (s?.def != null && s.def.bad && s.ctx?.faction == f && !s.Label.NullOrEmpty())
                            list.Add("CC_PplP_HardTimes".Translate(s.Label.Named("EVENT")).Resolve());
                var head = FavourCalls.HeadOf(f, PeoplesModel.Home);
                if (head != null)
                    foreach (var topic in NeighbourLetters.Topics(head, LetterPurpose.Warning).Take(3))
                        list.Add("CC_PplP_Threat".Translate(topic.text.Named("THREAT")).Resolve());
                if (f.def.permanentEnemy) list.Add("CC_Peoples_EveryonesFoe".Translate().Resolve());
                else if (Facts.Counts(f))
                {
                    var foes = new List<string>();
                    foreach (var g in Find.FactionManager.AllFactionsListForReading)
                        if (g != f && Facts.Counts(g) && f.HostileTo(g) && !g.IsPlayer)
                            foes.Add(ThingRefs.Link(g.Name, ThingRefs.Of(g)));
                    if (foes.Count > 0) list.Add("CC_Peoples_Wars".Translate(TextUtil.JoinAnd(foes).Named("FOES")).Resolve());
                }
                if (WorldLedger.Instance != null)
                    foreach (var tie in FavourAsks.Waiting())
                        if (Refs.Faction(tie.to) == f && FavourMath.ParseNeed(tie.terms.goods, out var need))
                            list.Add("CC_PplP_Asks".Translate(DipWords.Need(need).Named("NEED")).Resolve());
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("region window: what a people wants", ex);
            }
            if (list.Count == 0) list.Add("CC_PplP_LittleKnown".Translate().Resolve());
            return list;
        }

        // ================= Towns =================

        private static PeopleEntry townsFor;
        private static readonly List<PeopleTown> townRows = new List<PeopleTown>();
        private const float TownH = 48f;

        private static float Towns(PeopleEntry e, float w, float viewH)
        {
            if (townsFor != e)
            {
                townsFor = e;
                townRows.Clear();
                townRows.AddRange(e.towns.OrderBy(t => t.daysWord != null && t.days >= 0f ? t.days : 100000f + t.tiles));
            }
            float y = 0f;
            if (townRows.Count == 0) return UiKit.EmptyState(0f, 0f, w, Icons.World, e.Named("CC_PplP_NoTowns"), 70f);
            int looked = 0;
            for (int i = 0; i < townRows.Count; i++)
            {
                var t = townRows[i];
                // A couple of towns' days a frame, as the world's paths are worked out (Post/PostReach.cs).
                if ((t.daysWord == null || !t.known) && looked < 2 && Event.current.type == EventType.Repaint)
                {
                    PeoplesModel.Look(t);
                    looked++;
                }
                if (y + TownH >= scroll.y && y <= scroll.y + viewH) TownRow(new Rect(0f, y, w, TownH), t, e, i);
                y += TownH + 2f;
            }
            return y + 6f;
        }

        private static void TownRow(Rect r, PeopleTown t, PeopleEntry e, int i)
        {
            UiKit.Zebra(r, i);
            Widgets.DrawHighlightIfMouseover(r);
            Texture2D icon = null;
            try
            {
                icon = t.s.ExpandingIcon;
            }
            catch
            {
                icon = null;
            }
            if (icon == null || icon == BaseContent.BadTex) icon = Icons.World;
            Color fc;
            try
            {
                fc = e.f.Color;
            }
            catch
            {
                fc = UiKit.Faint;
            }
            var pic = new Rect(r.x + 6f, r.y + 10f, 26f, 26f);
            UiKit.Icon(pic, icon, fc);
            // The post's range (Post/PostRing.cs): a soft ring round a town far off (by relay) or out of reach.
            if (PostRing.Ring(PeoplesModel.Home?.Tile ?? PlanetTile.Invalid, t.s, out var ring)) UiKit.RingDraw(pic.ExpandedBy(4f), ring);
            float right = r.xMax - 4f;
            var act = new Rect(right - 26f, r.y + 11f, 26f, 26f);
            if (UiKit.IconButton(act, Icons.Promise ?? Icons.Trust, ActOnTownTip))
            {
                RegionView.target = ActTarget.Town;
                RegionView.targetTown = t.s.ID;
                RegionView.tab = PeopleTab.Overview;
                PeopleActs.Dirty();
            }
            right = act.x - 4f;
            var show = new Rect(right - 26f, r.y + 11f, 26f, 26f);
            if (UiKit.IconButton(show, Icons.ModeMap, ShowTownTip)) Cards.ShowOnMap(t.s);
            right = show.x - 8f;
            string days = t.daysWord ?? PeoplesModel.DaysWord(-1f);
            float dw = Mathf.Min(UiKit.KeptWidth(days, GameFont.Tiny) + 4f, 80f);
            UiKit.LabelFit(new Rect(right - dw, r.y + 3f, dw, 20f), days, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            float x = pic.xMax + 10f;
            float nw = UiKit.NameLink(new Rect(x, r.y + 3f, Mathf.Max(0f, right - dw - x - 6f), 20f), t.s.Name, t.thing);
            UiKit.LabelFit(new Rect(x + nw + 8f, r.y + 3f, Mathf.Max(0f, right - dw - x - nw - 14f), 20f), t.size, UiKit.Faint, GameFont.Tiny);
            var home = PeoplesModel.Home;
            var lines = home != null ? PostWords.ReachLines(home, t.s) : null;
            string reach = lines != null && lines.Count > 0 ? lines[0] : "";
            UiKit.LabelFit(new Rect(x, r.y + 24f, Mathf.Max(0f, r.xMax - 64f - x), 18f), reach, UiKit.Dim, GameFont.Tiny);
            if (lines != null && lines.Count > 1) TooltipHandler.TipRegion(new Rect(x, r.y + 22f, Mathf.Max(0f, r.xMax - 64f - x), 22f), new TipSignal(() => string.Join("\n", lines), t.s.ID ^ 0x6A31));
            // A click on the rest of the row shows the town on the region map, its card the map's panel.
            if (Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Cards.ShowOnMap(t.s);
            }
        }

        // ================= People =================

        private const float PersonH = 48f;

        private sealed class PersonLine
        {
            public Notable n;
            public ThingRef who;
            public string name, second, why;
            public Color color;
        }

        private static readonly List<PersonLine> persons = new List<PersonLine>();
        private static readonly List<Pawn> hereNow = new List<Pawn>();
        private static readonly List<string> hereNames = new List<string>();
        private static PeopleEntry personsFor;
        private static float personsAt = -100f;

        /// <summary>Their notables' rows and who of theirs is here, made at most once a second.</summary>
        private static void Persons(PeopleEntry e)
        {
            float t = Time.realtimeSinceStartup;
            if (personsFor == e && t - personsAt < 1f && t >= personsAt) return;
            personsFor = e;
            personsAt = t;
            persons.Clear();
            hereNow.Clear();
            hereNames.Clear();
            try
            {
                foreach (var n in e.notables)
                {
                    int op = NotableBook.Opinion(n);
                    var who = ThingRefs.Of(n);
                    string town = n.townName.NullOrEmpty() ? "" : n.town >= 0 && ThingRefs.Exists(ThingRefs.Of(Ref.Town(n.town))) ? ThingRefs.Link(n.townName, ThingRefs.Of(Ref.Town(n.town))) : n.townName;
                    persons.Add(new PersonLine
                    {
                        n = n, who = who, color = NotableBook.RegardColor(op), name = ThingRefs.Link(n.Name, who) + ", " + NotableBook.TitleOf(n),
                        second = (town.NullOrEmpty() ? "" : town + " · ") + NotableBook.RegardWords(op), why = NeighboursPanel.WhyNoLetter(n)
                    });
                }
                foreach (var p in PeopleActs.Here(e))
                {
                    hereNow.Add(p);
                    hereNames.Add(ThingRefs.Link(p.LabelShort, ThingRefs.Of(p)));
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("region window: a people's notables", ex);
            }
        }

        private static float People(PeopleEntry e, float w)
        {
            Persons(e);
            float y = 0f;
            var all = persons;
            y += UiKit.SectionHeading(0f, y, w, "CC_Faces_Known".Translate(), all.Count, null, null, out _);
            if (!NotableBook.On) y += UiKit.Paragraph(4f, y, w - 4f, "CC_Faces_Known_Off".Translate(), UiKit.Faint) + 8f;
            else if (all.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, e.Named("CC_PplP_NobodyYet"), UiKit.Faint) + 8f;
            for (int i = 0; i < all.Count; i++)
            {
                PersonRow(new Rect(0f, y, w, PersonH), all[i], i);
                y += PersonH + 2f;
            }
            if (hereNow.Count > 0)
            {
                y += 8f;
                y += UiKit.SectionHeading(0f, y, w, AtColonyTitle, hereNow.Count, null, null, out _);
                for (int i = 0; i < hereNow.Count; i++)
                {
                    var p = hereNow[i];
                    if (UiKit.Row(new Rect(0f, y, w, UiKit.RowPortrait), i, hereNames[i], Color.white, AtColonyLine, p, null, null, null, null, null, null, true))
                        Cards.ShowOnMap(new GlobalTargetInfo(p));
                    y += UiKit.RowPortrait + 2f;
                }
            }
            y += 10f;
            // Everyone the colony knows by name, its old foes and those who left: the view's Faces.
            if (UiKit.RowAct(new Rect(0f, y, Mathf.Min(w, 220f), 22f), FacesLabel, null, FacesTip))
            {
                RegionView.faces = true;
                PeoplesView.ResetScroll();
            }
            return y + 30f;
        }

        private static void PersonRow(Rect r, PersonLine row, int i)
        {
            UiKit.Zebra(r, i);
            Widgets.DrawHighlightIfMouseover(r);
            var n = row.n;
            var color = row.color;
            var who = row.who;
            var pic = new Rect(r.x + 4f, r.y + 6f, 36f, 36f);
            UiKit.Portrait(pic, n.pawn, color);
            if (Mouse.IsOver(pic)) Previews.Hover(who, pic);
            const float btnW = 96f;
            var act = new Rect(r.xMax - 26f - 4f, r.y + 11f, 26f, 26f);
            if (UiKit.IconButton(act, Icons.Promise ?? Icons.Trust, ActOnPersonTip))
            {
                RegionView.target = ActTarget.Person;
                RegionView.targetPerson = n.id;
                RegionView.tab = PeopleTab.Overview;
                PeopleActs.Dirty();
            }
            var b = new Rect(act.x - btnW - 6f, r.y + 11f, btnW, 26f);
            if (UiKit.ActButton(b, WriteLabel, row.why)) Find.WindowStack.Add(new Dialog_NeighbourLetter(n));
            float x = pic.xMax + 8f, tw = Mathf.Max(0f, b.x - x - 6f);
            LinkText.Line(new Rect(x, r.y + 4f, tw, 20f), row.name, Color.white);
            LinkText.Line(new Rect(x, r.y + 24f, tw, 18f), row.second, color, GameFont.Tiny);
        }

        // ================= Dealings =================

        private static float Dealings(PeopleEntry e, float w)
        {
            float y = 0f;
            bool whole = ColonyCouncil.Dealings.With(e.f).Count > 0;
            if (e.dip == null && !whole)
                return y + UiKit.EmptyState(0f, y, w, Icons.Promise ?? Icons.Trust, e.Named("CC_PplP_NoDealings"), 80f);
            // The colony's dealings with them as a whole and with their towns: contracts, works, rights, petitions (Dealings/).
            if (whole) y += DealingActs.Draw(e.f, 4f, y, w - 4f);
            // Their head, their ties in their age's words (pacts, fealty, debts each way, marriages), what is under way.
            if (e.dip != null) y += DiplomacyPanels.PeopleBody(4f, y, w - 4f, e.dip, true);
            return y + 8f;
        }

        // ================= History =================

        private sealed class HistoryKept
        {
            public int people = int.MinValue, version = -1;
            public float at;
            public readonly List<(string date, string line)> lines = new List<(string, string)>();
            public readonly List<(string text, Color color)> remembers = new List<(string, Color)>();
        }

        private static readonly HistoryKept story = new HistoryKept();
        private const int StoryMost = 30;

        private static float History(PeopleEntry e, float w, float viewH)
        {
            Story(e);
            float y = 0f;
            y += UiKit.SectionHeading(0f, y, w, "CC_Card_Head_TheyRemember".Translate(), story.remembers.Count, null, null, out _);
            if (story.remembers.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, "CC_PplP_RemembersNothing".Translate().Resolve(), UiKit.Faint) + 6f;
            foreach (var (text, color) in story.remembers) y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), text, color) + 2f;
            y += 10f;
            y += UiKit.SectionHeading(0f, y, w, "CC_PplP_Timeline".Translate(), story.lines.Count, null, null, out _);
            if (story.lines.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, "CC_PplP_TimelineNone".Translate().Resolve(), UiKit.Faint) + 6f;
            const float dateW = 70f;
            foreach (var (date, line) in story.lines)
            {
                float h = Mathf.Max(18f, LinkText.Height(line, w - dateW - 12f));
                if (y + h >= scroll.y && y <= scroll.y + viewH)
                {
                    LinkText.Line(new Rect(4f, y, dateW, 18f), date, UiKit.Faint, GameFont.Tiny);
                    LinkText.Draw(new Rect(dateW + 8f, y, w - dateW - 12f, 999f), line, Color.white);
                }
                y += h + 3f;
            }
            y += 8f;
            if (UiKit.RowAct(new Rect(0f, y, Mathf.Min(w, 200f), 22f), "CC_Peoples_TheirHistory".Translate(), null, e.Named("CC_Peoples_TheirHistoryTip")))
                WorldHistoryPage.ShowFor(Refs.Of(e.f), Ref.None);
            return y + 30f;
        }

        /// <summary>What they remember (strongest first) and their timeline with the colony (newest first), kept until the history moves.</summary>
        private static void Story(PeopleEntry e)
        {
            float t = Time.realtimeSinceStartup;
            int v = global::ColonyCouncil.History.Version + FactionMemory.Version;
            if (story.people == e.f.loadID && story.version == v && t - story.at < 5f && t >= story.at) return;
            story.people = e.f.loadID;
            story.version = v;
            story.at = t;
            story.lines.Clear();
            story.remembers.Clear();
            try
            {
                var view = WorldLedger.People(e.f);
                if (view != null)
                    foreach (var d in view.remembers.Take(6))
                    {
                        string text = FactionMemory.Text(new FactionDeed { kind = d.kind, count = d.count, faction = e.f, name = d.name });
                        if (text.NullOrEmpty()) continue;
                        bool bad = d.side == DeedSide.Wrong || d.side == DeedSide.TheirWrong;
                        story.remembers.Add((text.CapitalizeFirst() + " (" + FactionMemory.AgoText(d.tick) + ").", bad ? UiKit.Bad : UiKit.Good));
                    }
                var player = Faction.OfPlayerSilentFail;
                var us = player != null ? Refs.Of(player) : Ref.None;
                foreach (var h in global::ColonyCouncil.History.Of(Refs.Of(e.f), max: 120))
                {
                    if (story.lines.Count >= StoryMost) break;
                    if (!WithColony(h, us)) continue;
                    story.lines.Add((CardWords.HistoryDate(h, true), CardWords.HistoryLine(h, true)));
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("region window: a people's history", ex);
            }
        }

        /// <summary>An entry of their history the colony was part of: one of its homes, its people as a whole, or one of its own, or at a home.</summary>
        private static bool WithColony(HistoryEntry h, Ref us)
        {
            if (h.place.kind == RefKind.Colony) return true;
            foreach (var a in h.actors)
            {
                if (a.who.kind == RefKind.Colony || a.who == us) return true;
                if (a.who.kind == RefKind.Person && Refs.Pawn(a.who) is Pawn p && p.Faction != null && p.Faction.IsPlayer) return true;
            }
            return false;
        }

        // ================= All peoples =================

        /// <summary>Every people at once: what is owed each way (with calling in and answering), then what each remembers of the colony.</summary>
        private static float AllPeoples(float w)
        {
            float y = UiKit.SectionTitle(0f, 0f, w, AllTitle, Icons.People, UiKit.Heading);
            y += UiKit.Paragraph(0f, y, w, AllLine, UiKit.Faint) + 8f;
            var dip = DiplomacyModel.Current;
            if (dip != null) y = DiplomacyPanels.Owed(y, w, dip, folds);
            y += 8f;
            // What each people remembers of the colony (World/FactionMemoryPanel.cs).
            return FactionMemoryPanel.Draw(y, w);
        }

        // ================= Words, made once =================

        private static string atColonyTitle, atColonyLine, writeLabel, showTownTip;
        private static string AtColonyTitle => atColonyTitle ??= "CC_PplP_AtColony".Translate();
        private static string AtColonyLine => atColonyLine ??= "CC_PplP_AtColonyLine".Translate();
        private static string WriteLabel => writeLabel ??= "CC_Neighbours_Write".Translate();
        private static string ShowTownTip => showTownTip ??= "CC_Lens_ShowTip".Translate();

        private static string showTip, ageTip, wantsTitle, reachTitle, actsTitle, actOnTownTip, actOnPersonTip, facesLabel, facesTip, allTitle, allLine;

        private static string ShowTip => showTip ??= "CC_PplP_ShowTip".Translate();
        private static string AgeTip => ageTip ??= "CC_PplP_AgeTip".Translate();
        private static string WantsTitle => wantsTitle ??= "CC_PplP_Wants".Translate();
        private static string ReachTitle => reachTitle ??= "CC_PplP_Reach".Translate();
        private static string ActsTitle => actsTitle ??= "CC_PplP_Acts".Translate();
        private static string ActOnTownTip => actOnTownTip ??= "CC_PplP_ActOnTownTip".Translate();
        private static string ActOnPersonTip => actOnPersonTip ??= "CC_PplP_ActOnPersonTip".Translate();
        private static string FacesLabel => facesLabel ??= "CC_PplP_Faces".Translate();
        private static string FacesTip => facesTip ??= "CC_PplP_FacesTip".Translate();
        private static string AllTitle => allTitle ??= "CC_PplV_All".Translate();
        private static string AllLine => allLine ??= "CC_PplP_AllLine".Translate();
    }
}
