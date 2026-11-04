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
    /// Region → Peoples: each people of the world the colony can know, how it stands with the colony, whom it is at war
    /// with and the world events about it, with a way to its history; then what the peoples remember of the colony
    /// (World/FactionMemoryPanel.cs). Built every two seconds while the page is open.
    /// </summary>
    public static class PeoplesPage
    {
        private static readonly Color Accent = new Color(0.8f, 0.72f, 0.92f);
        private static Vector2 scroll;
        private static float height = 600f;

        private class Row
        {
            public Faction f;
            public string standing, kind, wars, events;
            public Color color;
            public float width = -1f, height;
        }

        private static List<Row> rows = new List<Row>();
        private static float builtAt = -1f;

        public static void Draw(Rect rect)
        {
            if (Time.realtimeSinceStartup - builtAt > 2f)
            {
                try
                {
                    rows = Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: the peoples", e);
                    rows = new List<Row>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            string stat = rows.Count > 0 ? "CC_Peoples_Stat".Translate(rows.Count).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Trust ?? Icons.People, "CC_Peoples_Header".Translate(), "CC_Peoples_Sub".Translate(), stat, Accent);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                y += UiKit.SectionTitle(0f, y, w, "CC_Peoples_Stance".Translate(), Icons.Trust ?? Icons.People, Accent);
                if (rows.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.People, "CC_Peoples_None".Translate(), 60f);
                else
                {
                    y += 4f;
                    foreach (var r in rows)
                    {
                        float h = Measure(r, w);
                        DrawRow(new Rect(0f, y, w, h), r);
                        y += h + 6f;
                    }
                }
                y += 12f;
                // What each people remembers of the colony.
                y = FactionMemoryPanel.Draw(y, w);
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

        /// <summary>A people the colony can know: of humans, not hidden, not gone.</summary>
        public static bool Listed(Faction f) => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && f.def != null && f.def.humanlikeFaction;

        private static List<Row> Build()
        {
            var list = new List<Row>();
            var all = Find.FactionManager?.AllFactionsListForReading;
            if (all == null) return list;
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            foreach (var f in all)
            {
                if (!Listed(f)) continue;
                var r = new Row { f = f, standing = RegionMap.StandingText(f), color = RegionMap.StandingColor(f), kind = f.def.LabelCap };
                if (f.def.permanentEnemy) r.wars = "CC_Peoples_EveryonesFoe".Translate().Resolve();
                else if (Facts.Counts(f))
                {
                    var foes = new List<string>();
                    foreach (var g in all)
                        if (g != f && Facts.Counts(g) && f.HostileTo(g))
                            foes.Add(ThingRefs.Link(g.Name, ThingRefs.Of(g)));
                    if (foes.Count > 0) r.wars = "CC_Peoples_Wars".Translate(TextUtil.JoinAnd(foes).Named("FOES")).Resolve();
                }
                if (ws != null)
                {
                    var about = new List<string>();
                    foreach (var s in ws.Running)
                        if ((s.ctx?.faction == f || s.ctx?.enemy == f) && !s.Label.NullOrEmpty())
                            about.Add(s.Label);
                    if (about.Count > 0) r.events = "CC_Peoples_Events".Translate(TextUtil.JoinAnd(about).Named("EVENTS")).Resolve();
                }
                list.Add(r);
            }
            // Allies and friends first, enemies last.
            return list.OrderByDescending(r => r.f.HasGoodwill ? r.f.PlayerGoodwill : -200).ThenBy(r => r.f.Name).ToList();
        }

        private const float HistoryW = 130f;

        private static float Measure(Row r, float w)
        {
            if (Mathf.Abs(r.width - w) < 0.5f) return r.height;
            r.width = w;
            float tw = w - 20f - HistoryW - 8f;
            Text.Font = GameFont.Small;
            float h = 6f + 26f + 18f;
            if (!r.wars.NullOrEmpty()) h += LinkText.Height(r.wars, tw) + 2f;
            if (!r.events.NullOrEmpty()) h += Text.CalcHeight(r.events, tw) + 2f;
            r.height = Mathf.Max(h + 6f, 60f);
            return r.height;
        }

        private static void DrawRow(Rect card, Row r)
        {
            UiKit.Card(card, UiKit.WithAlpha(r.color, 0.6f), false, false);
            float x = card.x + 10f, y = card.y + 6f;
            float tw = card.width - 20f - HistoryW - 8f;
            float chip = UiKit.FactionChip(x, y, r.f, 22f, Mathf.Min(260f, tw * 0.55f), true);
            UiKit.LabelFit(new Rect(x + chip + 10f, y, Mathf.Max(0f, tw - chip - 10f), 22f), r.standing, r.color);
            y += 26f;
            UiKit.LabelFit(new Rect(x, y, tw, 18f), r.kind, UiKit.Faint, GameFont.Tiny);
            y += 18f;
            if (!r.wars.NullOrEmpty()) y += LinkText.Draw(new Rect(x, y, tw, 999f), r.wars, r.f.def.permanentEnemy ? UiKit.Faint : UiKit.Warn) + 2f;
            if (!r.events.NullOrEmpty()) y += UiKit.Paragraph(x, y, tw, r.events, UiKit.WorldColor) + 2f;
            var b = new Rect(card.xMax - HistoryW - 10f, card.y + 8f, HistoryW, 26f);
            if (Widgets.ButtonText(b, "CC_Peoples_TheirHistory".Translate())) WorldHistoryPage.ShowFor(Refs.Of(r.f), Ref.None);
            TooltipHandler.TipRegion(b, "CC_Peoples_TheirHistoryTip".Translate(r.f.Name.Named("PEOPLE")));
        }
    }

    /// <summary>
    /// Region → Letters and calls: what the colony has written to its neighbours or called them about, where each letter
    /// is (in words, never a countdown), the answers of the last days, and everyone it can write to with the button to
    /// do so (World/NeighbourLetters.cs).
    /// </summary>
    public static class LettersPage
    {
        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);
        private static Vector2 scroll;
        private static float height = 600f;
        private const float DateW = 74f;

        private class Line
        {
            /// <summary>With links: the date to the letter's card, the names in the title to theirs.</summary>
            public string title, status, date;
            public Color color;
            public bool call;
        }

        private class Person
        {
            public Notable n;
            public ThingRef thing;
            public string name, second, why;
            public Color color;
        }

        private static List<Line> under = new List<Line>(), lately = new List<Line>();
        private static List<Person> people = new List<Person>();
        private static float builtAt = -1f;
        private static int builtVersion = -1;

        public static void Draw(Rect rect)
        {
            if (!NotableBook.On)
            {
                UiKit.EmptyState(rect, Icons.Journal, "CC_LettersPg_Off".Translate());
                return;
            }
            if (builtVersion != NotableBook.Version || Time.realtimeSinceStartup - builtAt > 2f)
            {
                try
                {
                    Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: letters and calls", e);
                    under = new List<Line>();
                    lately = new List<Line>();
                    people = new List<Person>();
                }
                builtAt = Time.realtimeSinceStartup;
                builtVersion = NotableBook.Version;
            }
            string stat = under.Count > 0 ? "CC_Neighbours_OnTheirWay".Translate(under.Count).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Journal, "CC_LettersPg_Header".Translate(), "CC_LettersPg_Sub".Translate(), stat, Accent);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                y += UiKit.SectionTitle(0f, y, w, "CC_LettersPg_UnderWay".Translate(), Icons.Journal, Accent);
                if (under.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.Journal, "CC_LettersPg_NoneUnderWay".Translate(), 56f);
                else y = Lines(y, w, under);
                if (lately.Count > 0)
                {
                    y += 10f;
                    y += UiKit.SectionTitle(0f, y, w, "CC_LettersPg_Lately".Translate(), Icons.Journal, UiKit.Faint);
                    y = Lines(y, w, lately);
                }
                y += 10f;
                y += UiKit.SectionTitle(0f, y, w, "CC_LettersPg_WriteTo".Translate(), Icons.Visitor, Accent);
                if (people.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.Visitor, "CC_LettersPg_NoOne".Translate(), 56f);
                else
                    foreach (var p in people)
                    {
                        DrawPerson(new Rect(0f, y, w, 46f), p);
                        y += 48f;
                    }
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
            under = new List<Line>();
            lately = new List<Line>();
            people = new List<Person>();
            var book = NotableBook.Instance;
            if (book == null) return;
            foreach (var l in book.letters.Where(x => x != null).OrderByDescending(x => x.created))
            {
                var n = book.Get(l.notable);
                var line = MakeLine(l, n);
                if (l.Open) under.Add(line);
                else lately.Add(line);
            }
            foreach (var n in book.notables.Where(x => x.Alive).OrderBy(x => x.townName ?? "").ThenBy(x => (int)x.role))
            {
                int op = NotableBook.Opinion(n);
                string town = n.townName ?? n.faction?.Name ?? "";
                var who = ThingRefs.Of(n);
                var townRef = n.town >= 0 ? ThingRefs.Of(Ref.Town(n.town)) : ThingRef.None;
                string townLink = town.NullOrEmpty() || townRef.IsNone || !ThingRefs.Exists(townRef) ? town : ThingRefs.Link(town, townRef);
                people.Add(new Person
                {
                    n = n, thing = who, name = ThingRefs.Link(n.Name, who) + ", " + NotableBook.TitleOf(n), color = NotableBook.RegardColor(op),
                    second = (town.NullOrEmpty() ? "" : townLink + " · ") + NotableBook.RegardWords(op), why = NeighboursPanel.WhyNoLetter(n)
                });
            }
        }

        private static Line MakeLine(NeighbourLetter l, Notable n)
        {
            string purpose = ("CC_Letter_Purpose_" + l.purpose).Translate();
            string town = n?.townName;
            // The one written to, their town and the writer are links to their cards; the date to the letter's own.
            string name = l.toName ?? n?.Name ?? "?";
            if (n != null) name = ThingRefs.Link(name, ThingRefs.Of(n));
            if (!town.NullOrEmpty() && n != null && n.town >= 0 && ThingRefs.Exists(ThingRefs.Of(Ref.Town(n.town)))) town = ThingRefs.Link(town, ThingRefs.Of(Ref.Town(n.town)));
            string to = town.NullOrEmpty() ? name : "CC_LettersPg_NameOf".Translate(name.Named("NAME"), town.Named("TOWN")).Resolve();
            string writer = l.writer != null && !l.writer.Discarded ? ThingRefs.Link(l.WriterName, ThingRefs.Of(l.writer)) : l.WriterName;
            bool call = l.channel == LetterChannel.Console;
            var line = new Line
            {
                call = call,
                date = ThingRefs.Link(MainTabWindow_Council.DayString(l.created), ThingRefs.Of(l)),
                title = (call ? "CC_LettersPg_Call" : "CC_LettersPg_Letter").Translate(purpose.Named("PURPOSE"), to.Named("NAME"), writer.Named("PAWN")).Resolve()
            };
            switch (l.stage)
            {
                case LetterStage.Done:
                    line.status = "CC_LettersPg_Answered".Translate(NeighbourLetters.ToneWord(l.tone).Named("TONE")).Resolve();
                    line.color = l.tone <= AnswerTone.Cordial ? UiKit.Good : l.tone >= AnswerTone.Refusal ? UiKit.Bad : UiKit.Faint;
                    break;
                case LetterStage.Cancelled:
                    line.status = "CC_LettersPg_CalledOff".Translate();
                    line.color = UiKit.Faint;
                    break;
                default:
                    line.status = n != null ? NeighboursPanel.StatusWords(l, n) : null;
                    line.color = UiKit.Info;
                    break;
            }
            return line;
        }

        private static float Lines(float y, float w, List<Line> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                Text.Font = GameFont.Small;
                float tw = w - DateW - 28f;
                float h = LinkText.Height(l.title, tw) + (l.status.NullOrEmpty() ? 0f : 18f) + 6f;
                var r = new Rect(0f, y, w, Mathf.Max(28f, h));
                UiKit.Zebra(r, i);
                LinkText.Line(new Rect(4f, y + 3f, DateW - 6f, 20f), l.date, UiKit.Faint, GameFont.Tiny);
                UiKit.Icon(new Rect(DateW, y + 4f, 16f, 16f), l.call ? Icons.Visitor : Icons.Journal, l.color);
                float th = LinkText.Draw(new Rect(DateW + 22f, y + 2f, tw, 999f), l.title, Color.white);
                if (!l.status.NullOrEmpty()) UiKit.LabelFit(new Rect(DateW + 22f, y + 2f + th, tw, 18f), l.status, l.color, GameFont.Tiny);
                y += r.height + 2f;
            }
            return y;
        }

        private static void DrawPerson(Rect r, Person p)
        {
            Widgets.DrawHighlightIfMouseover(r);
            // The portrait and the name lead to their card.
            var pic = new Rect(r.x + 2f, r.y + 4f, 36f, 36f);
            UiKit.Portrait(pic, p.n.pawn, p.color);
            if (Mouse.IsOver(pic)) Previews.Hover(p.thing, pic);
            if (Widgets.ButtonInvisible(pic, false))
            {
                Cards.Click();
                Cards.Open(p.thing);
            }
            const float btnW = 96f;
            float x = r.x + 46f, tw = r.xMax - x - btnW - 10f;
            LinkText.Line(new Rect(x, r.y + 3f, tw, 20f), p.name, Color.white);
            LinkText.Line(new Rect(x, r.y + 22f, tw, 18f), p.second, p.color, GameFont.Tiny);
            var b = new Rect(r.xMax - btnW - 4f, r.y + 10f, btnW, 26f);
            bool can = p.why == null;
            if (Widgets.ButtonText(b, "CC_Neighbours_Write".Translate(), true, true, can) && can) Find.WindowStack.Add(new Dialog_NeighbourLetter(p.n));
            if (!can) TooltipHandler.TipRegion(b, p.why);
            Widgets.DrawLineHorizontal(r.x, r.yMax, r.width, UiKit.DividerColor);
        }
    }

    /// <summary>
    /// Region → Journeys: the journey planned ahead from the colony in view (UI/JourneyDialog.cs), the other colonies'
    /// plans, and the colony's caravans out on the land, each with a button to see it on the world.
    /// </summary>
    public static class JourneysPage
    {
        private static readonly Color Accent = new Color(0.8f, 0.86f, 0.7f);
        private static Vector2 scroll;
        private static float height = 400f;

        private class Away
        {
            public Caravan c;
            /// <summary>Its name, and the same as a link to its card.</summary>
            public string name, link, line;
        }

        private static List<Away> caravans = new List<Away>();
        private static List<string> others = new List<string>();
        private static float builtAt = -1f;

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
                    CouncilLog.ErrorOnce("region window: journeys", e);
                    caravans = new List<Away>();
                    others = new List<string>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            float top = UiKit.PageHeader(rect, Icons.Trader, "CC_JourneysPg_Header".Translate(), "CC_JourneysPg_Sub".Translate(),
                caravans.Count > 0 ? "CC_JourneysPg_Stat".Translate(caravans.Count).Resolve() : null, Accent);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (!JourneyBook.On) y += UiKit.Paragraph(0f, y, w, "CC_JourneysPg_Off".Translate(), UiKit.Faint) + 10f;
                else
                {
                    y += JourneyPanel.Section(0f, y, w);
                    foreach (var o in others) y += UiKit.Paragraph(4f, y, w - 8f, o, UiKit.Faint) + 4f;
                    y += 6f;
                }
                y += UiKit.SectionTitle(0f, y, w, "CC_JourneysPg_Away".Translate(), Icons.Trader, Accent);
                if (caravans.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.Trader, "CC_JourneysPg_NoneAway".Translate(), 56f);
                else
                    for (int i = 0; i < caravans.Count; i++)
                    {
                        var a = caravans[i];
                        var r = new Rect(0f, y, w, 46f);
                        UiKit.Zebra(r, i);
                        LinkText.Line(new Rect(8f, y + 3f, w - 150f, 22f), a.link, UiKit.HeaderColor);
                        UiKit.LabelFit(new Rect(8f, y + 24f, w - 150f, 18f), a.line, UiKit.Faint, GameFont.Tiny);
                        var b = new Rect(w - 134f, y + 9f, 130f, 26f);
                        // On the region map with its card, not the game's world view (the card's head keeps a button for that).
                        if (Widgets.ButtonText(b, "CC_JourneysPg_Show".Translate()) && a.c != null && !a.c.Destroyed) Cards.ShowOnMap(a.c);
                        y += 48f;
                    }
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
            caravans = new List<Away>();
            others = new List<string>();
            var here = Dialog_JourneyPlan.HomeMap();
            if (ColonyState.Several)
                foreach (var plan in JourneyBook.Plans)
                    if (plan.map != null && plan.map != here)
                        others.Add("CC_Tell_At".Translate(Homes.Name(plan.map).Named("PLACE"), JourneyPanel.Status(plan).Named("TEXT")).Resolve());
            var all = Find.WorldObjects?.Caravans;
            if (all == null) return;
            foreach (var c in all)
            {
                if (c == null || c.Destroyed || !c.IsPlayerControlled) continue;
                int people = c.PawnsListForReading.Count(p => p.RaceProps.Humanlike);
                string line = "CC_JourneysPg_People".Translate(people).Resolve();
                if (c.pather != null && c.pather.Moving)
                {
                    var to = Find.WorldObjects.SettlementAt(c.pather.Destination);
                    line += " · " + (to != null ? "CC_JourneysPg_HeadingTo".Translate(to.LabelCap.Named("PLACE")).Resolve() : "CC_JourneysPg_OnTheMove".Translate().Resolve());
                }
                else line += " · " + "CC_JourneysPg_Resting".Translate().Resolve();
                caravans.Add(new Away { c = c, name = c.LabelCap, link = ThingRefs.Link(c.LabelCap, ThingRefs.Of(c)), line = line });
            }
        }
    }

    /// <summary>
    /// Region → World events: what's going on in the world around the colony (World/WorldSituationsPanel.cs), and what
    /// the world has brought lately (the chronicle's world lines, UI/WorldPanel.cs).
    /// </summary>
    public static class EventsPage
    {
        private static Vector2 scroll;
        private static float height = 600f;

        public static void Draw(Rect rect)
        {
            var comp = CouncilGameComponent.Instance;
            int running = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance?.Running.Count() ?? 0 : 0;
            float top = UiKit.PageHeader(rect, Icons.WorldEvent, "CC_EventsPg_Header".Translate(), "CC_EventsPg_Sub".Translate(),
                running > 0 ? "CC_EventsPg_Stat".Translate(running).Resolve() : null, UiKit.WorldColor);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (!ColonyCouncilMod.Settings.worldSituations) y += UiKit.Paragraph(0f, y, w, "CC_EventsPg_Off".Translate(), UiKit.Faint) + 10f;
                else y = WorldSituationsPanel.Draw(0f, y, w);
                y = WorldPanel.Lately(y, w, comp);
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
    }

    /// <summary>
    /// Region → History: the world's one history (World/Ledger/History.cs), newest first, filtered by a people, a town
    /// or a kind of event: History.Of for a people, History.At (and what the town itself did) for a town, the history's
    /// own words (History.Text) for each line, or the kind and who took part where it has none, each name it mentions a
    /// link to its card and each day a link to the entry's own (UI/Cards/CardWords.HistoryLine). Built again when the
    /// history or a filter changes, at most once a second. Its filters are part of the way back (UI/Cards/Nav.cs).
    /// </summary>
    public static class WorldHistoryPage
    {
        static WorldHistoryPage()
        {
            Nav.Filters("WorldHistory", () => filters, SetFilters);
        }

        /// <summary>The filters as the way back keeps them ("f3|t55|deed"), or null for none; made when they change.</summary>
        private static string filters;

        private static void FiltersChanged() =>
            filters = people.IsNone && town.IsNone && family == null ? null : people + "|" + town + "|" + (family ?? "");

        /// <summary>The filters put back from a step of the way back.</summary>
        private static void SetFilters(string text)
        {
            people = town = Ref.None;
            family = null;
            if (!text.NullOrEmpty())
            {
                var parts = text.Split('|');
                if (parts.Length > 0) people = Ref.Parse(parts[0]);
                if (parts.Length > 1) town = Ref.Parse(parts[1]);
                if (parts.Length > 2 && parts[2].Length > 0) family = parts[2];
            }
            FiltersChanged();
            dirty = true;
            scroll = Vector2.zero;
        }

        private static Vector2 scroll;
        private static float height = 600f;
        private const float DateW = 96f;

        private static Ref people = Ref.None, town = Ref.None;
        private static string family;

        private class Row
        {
            public string date, text;
            public Texture2D icon;
            public Color color;
            public float width = -1f, h;
        }

        private static List<Row> rows = new List<Row>();
        private static int builtVersion = -1;
        private static float builtAt = -1f;
        private static bool dirty = true;
        private static readonly Dictionary<Ref, string> names = new Dictionary<Ref, string>();

        /// <summary>Opens the region window's History page on what one people (or one town) took part in.</summary>
        public static void ShowFor(Ref who, Ref where)
        {
            people = who;
            town = where;
            family = null;
            FiltersChanged();
            dirty = true;
            scroll = Vector2.zero;
            Pages.Open("WorldHistory");
        }

        public static void Draw(Rect rect)
        {
            if (dirty || builtVersion != History.Version && Time.realtimeSinceStartup - builtAt > 1f)
            {
                try
                {
                    Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: the world's history", e);
                    rows = new List<Row>();
                }
                dirty = false;
                builtVersion = History.Version;
                builtAt = Time.realtimeSinceStartup;
            }
            float top = UiKit.PageHeader(rect, Icons.History ?? Icons.Chronicle, "CC_WHist_Header".Translate(), "CC_WHist_Sub".Translate(),
                "CC_WHist_Stat".Translate(rows.Count).Resolve(), UiKit.HeaderColor);
            float y = rect.y + top;
            y += Filters(new Rect(rect.x, y, rect.width, 28f)) + 8f;
            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float ry = 0f, w = view.width;
                if (rows.Count == 0) ry += UiKit.EmptyState(0f, ry, w, Icons.History ?? Icons.Chronicle, "CC_WHist_None".Translate(), 80f);
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    if (Mathf.Abs(r.width - w) >= 0.5f)
                    {
                        r.width = w;
                        r.h = Mathf.Max(24f, LinkText.Height(r.text, w - DateW - 28f) + 4f);
                    }
                    // Only the rows on screen are drawn.
                    if (ry + r.h >= scroll.y && ry <= scroll.y + outRect.height)
                    {
                        var row = new Rect(0f, ry, w, r.h);
                        UiKit.Zebra(row, i);
                        LinkText.Line(new Rect(4f, ry + 2f, DateW - 6f, 20f), r.date, UiKit.Faint, GameFont.Tiny);
                        UiKit.Icon(new Rect(DateW, ry + 4f, 14f, 14f), r.icon, r.color);
                        LinkText.Draw(new Rect(DateW + 22f, ry + 2f, w - DateW - 28f, r.h), r.text, r.color);
                    }
                    ry += r.h;
                }
                height = Mathf.Max(ry + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The three filters (a people, a town, a kind) and a button to clear them; returns the height used.</summary>
        private static float Filters(Rect r)
        {
            const float gap = 8f;
            float clearW = 110f;
            float bw = Mathf.Min(240f, (r.width - clearW - gap * 3f) / 3f);
            var a = new Rect(r.x, r.y, bw, r.height);
            var b = new Rect(a.xMax + gap, r.y, bw, r.height);
            var c = new Rect(b.xMax + gap, r.y, bw, r.height);
            var d = new Rect(c.xMax + gap, r.y, clearW, r.height);
            string any = "CC_WHist_Any".Translate();
            if (Widgets.ButtonText(a, UiKit.Fit("CC_WHist_People".Translate((people.IsNone ? any : NameOf(people)).Named("WHO")).Resolve(), a.width - 12f)))
                Find.WindowStack.Add(new FloatMenu(PeopleOptions()));
            if (Widgets.ButtonText(b, UiKit.Fit("CC_WHist_Town".Translate((town.IsNone ? any : NameOf(town)).Named("WHERE")).Resolve(), b.width - 12f)))
                Find.WindowStack.Add(new FloatMenu(TownOptions()));
            if (Widgets.ButtonText(c, UiKit.Fit("CC_WHist_Kind".Translate((family == null ? any : KindName(family)).Named("KIND")).Resolve(), c.width - 12f)))
                Find.WindowStack.Add(new FloatMenu(KindOptions()));
            bool set = !people.IsNone || !town.IsNone || family != null;
            if (Widgets.ButtonText(d, "CC_WHist_Clear".Translate(), true, true, set) && set)
            {
                people = town = Ref.None;
                family = null;
                FiltersChanged();
                dirty = true;
            }
            return r.height;
        }

        private static List<FloatMenuOption> PeopleOptions()
        {
            var list = new List<FloatMenuOption> { new FloatMenuOption("CC_WHist_AnyPeople".Translate(), () => Set(ref people, Ref.None)) };
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.IsPlayer || f.Hidden || f.def == null || !f.def.humanlikeFaction) continue;
                var r = Refs.Of(f);
                if (History.Count(who: r) == 0) continue;
                list.Add(new FloatMenuOption(f.Name, () => Set(ref people, r), f.def.FactionIcon, f.Color));
            }
            return list;
        }

        private static List<FloatMenuOption> TownOptions()
        {
            var list = new List<FloatMenuOption> { new FloatMenuOption("CC_WHist_AnyTown".Translate(), () => Set(ref town, Ref.None)) };
            var seen = new HashSet<Ref>();
            var events = WorldLedger.Instance?.Book?.events;
            if (events != null)
                foreach (var e in events)
                {
                    if (e.place.kind == RefKind.Town) seen.Add(e.place);
                    foreach (var a in e.actors)
                        if (a.who.kind == RefKind.Town)
                            seen.Add(a.who);
                }
            foreach (var r in seen.Select(x => (r: x, name: NameOf(x))).OrderBy(x => x.name))
            {
                var t = r.r;
                list.Add(new FloatMenuOption(r.name, () => Set(ref town, t)));
            }
            return list;
        }

        private static List<FloatMenuOption> KindOptions()
        {
            var list = new List<FloatMenuOption> { new FloatMenuOption("CC_WHist_AnyKind".Translate(), () => SetKind(null)) };
            var events = WorldLedger.Instance?.Book?.events;
            foreach (var f in WorldHistoryMath.Families(events?.Select(e => e.kind)))
            {
                string k = f;
                list.Add(new FloatMenuOption(KindName(k), () => SetKind(k)));
            }
            return list;
        }

        private static void Set(ref Ref field, Ref value)
        {
            field = value;
            FiltersChanged();
            dirty = true;
            scroll = Vector2.zero;
        }

        private static void SetKind(string k)
        {
            family = k;
            FiltersChanged();
            dirty = true;
            scroll = Vector2.zero;
        }

        /// <summary>A kind's name for the filter: its own key where it has one, else its words.</summary>
        private static string KindName(string kind)
        {
            string key = "CC_WHist_Kind_" + kind;
            return key.CanTranslate() ? key.Translate().ToString() : WorldHistoryMath.Words(kind).CapitalizeFirst();
        }

        private static void Build()
        {
            rows = new List<Row>();
            names.Clear();
            var book = WorldLedger.Instance?.Book;
            if (book == null) return;
            string[] kinds = family == null ? null : new[] { family };
            List<HistoryEntry> found;
            if (!people.IsNone) found = History.Of(people, kinds, max: WorldHistoryMath.MaxShown * 2);
            else if (!town.IsNone)
            {
                // What happened at the town, and what the town itself did, newest first, each once.
                found = History.At(town, kinds, max: WorldHistoryMath.MaxShown).Concat(History.Of(town, kinds, max: WorldHistoryMath.MaxShown))
                    .GroupBy(e => e.id).Select(g => g.First()).OrderByDescending(e => e.tick).ThenByDescending(e => e.id).ToList();
            }
            else found = book.OfKind(family, max: WorldHistoryMath.MaxShown);
            foreach (var e in found)
            {
                if (rows.Count >= WorldHistoryMath.MaxShown) break;
                if (!WorldHistoryMath.Keeps(e, people, town, family)) continue;
                rows.Add(MakeRow(e));
            }
        }

        private static Row MakeRow(HistoryEntry e)
        {
            // Its words with each name it mentions a link to its card, and its day a link to its own (Rimfolk's windows only).
            string text = CardWords.HistoryLine(e, true);
            string fam = HistoryKinds.Family(e.kind);
            int ck = Array.IndexOf(HistoryKinds.Chronicle, fam);
            return new Row
            {
                date = CardWords.HistoryDate(e, true),
                text = text,
                icon = ck >= 0 ? Icons.For((ChronicleKind)ck) : fam == HistoryKinds.Deed ? Icons.Trust ?? Icons.Chronicle : fam == HistoryKinds.Tie ? Icons.Promise : Icons.WorldEvent,
                color = ck >= 0 ? UiKit.ChronicleColor((ChronicleKind)ck) : UiKit.WorldColor
            };
        }

        /// <summary>A name for someone or somewhere in the history, while the game or the ledger still knows it; kept for the build.</summary>
        private static string NameOf(Ref r)
        {
            if (r.IsNone) return null;
            if (names.TryGetValue(r, out var n)) return n;
            try
            {
                switch (r.kind)
                {
                    case RefKind.Person:
                        n = Refs.Pawn(r)?.LabelShort ?? WorldLedger.NameOf(r);
                        break;
                    case RefKind.People:
                        n = Refs.Faction(r)?.Name;
                        break;
                    case RefKind.Town:
                        n = Refs.Town(r)?.Name ?? WorldLedger.TownRecord(r.id)?.name;
                        break;
                    case RefKind.Colony:
                        var home = Refs.Home(r);
                        n = home != null ? Homes.Name(home) : "CC_WHist_TheColony".Translate().ToString();
                        break;
                    default:
                        // A house, a tile: what the cards call it.
                        n = ThingRefs.Name(ThingRef.From(r));
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: a name in the history", e);
            }
            names[r] = n;
            return n;
        }
    }
}
