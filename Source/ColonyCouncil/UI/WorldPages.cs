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
    /// The colony's letters and calls (World/NeighbourLetters.cs), as the region's views show them: those on the road (in
    /// words, never a countdown) as a section of Here and away, the letters of the last days as the History view's
    /// "Letters lately", and everyone the colony can write to, with the button to do so, as Peoples → Faces' "Known by
    /// name". Built when the letters change and at most every two seconds while a view shows them. (Its old page name,
    /// Letters, forwards to Here and away.)
    /// </summary>
    public static class LettersPage
    {
        private const float DateW = 74f;

        private class Line
        {
            /// <summary>With links: the date to the letter's card, the names in the title to theirs.</summary>
            public string title, status, date;
            public Color color;
            public bool call;
            public ThingRef letter;
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

        private static void BuildIfStale()
        {
            if (builtVersion == NotableBook.Version && Time.realtimeSinceStartup - builtAt <= 2f && builtAt <= Time.realtimeSinceStartup) return;
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

        /// <summary>How many letters and calls are on the road.</summary>
        public static int UnderWay
        {
            get
            {
                if (!NotableBook.On) return 0;
                BuildIfStale();
                return under.Count;
            }
        }

        /// <summary>
        /// Letters on the road, under their heading with their count (Here and away): each with whom to, from whom and
        /// where it is; a sentence when none is, or when letters are off. Returns the y below it.
        /// </summary>
        public static float UnderWaySection(float y, float w, Folds folds)
        {
            if (!NotableBook.On)
            {
                y += UiKit.SectionHeading(0f, y, w, "CC_LettersPg_UnderWay".Translate(), -1, null, null, out _);
                return y + UiKit.Paragraph(4f, y, w - 4f, "CC_LettersPg_Off".Translate(), UiKit.Faint) + 10f;
            }
            BuildIfStale();
            y += UiKit.SectionHeading(0f, y, w, "CC_LettersPg_UnderWay".Translate(), under.Count, folds, "letters", out bool open);
            if (!open) return y + 4f;
            if (under.Count == 0) return y + UiKit.Paragraph(4f, y, w - 4f, "CC_LettersPg_NoneUnderWay".Translate(), UiKit.Faint) + 10f;
            return Lines(y, w, under) + 8f;
        }

        /// <summary>The letters of the last days, newest first, answered or called off (the History view's "Letters lately"). Returns the y below them.</summary>
        public static float LatelyRows(float y, float w)
        {
            BuildIfStale();
            if (lately.Count == 0 && under.Count == 0)
                return y + UiKit.EmptyState(0f, y, w, Icons.Journal, (NotableBook.On ? "CC_LettersPg_NoneLately" : "CC_LettersPg_Off").Translate(), 80f);
            if (under.Count > 0) y = Lines(y, w, under) + 4f;
            return Lines(y, w, lately);
        }

        /// <summary>How many letters "Letters lately" lists.</summary>
        public static int LatelyCount
        {
            get
            {
                BuildIfStale();
                return under.Count + lately.Count;
            }
        }

        /// <summary>
        /// Everyone the colony knows by name, by their town, each with their portrait and name leading to their card and a
        /// button to write (Peoples → Faces). Returns the y below it.
        /// </summary>
        public static float KnownSection(float y, float w, Folds folds, float sightTop = float.MinValue, float sightBottom = float.MaxValue)
        {
            if (!NotableBook.On)
            {
                y += UiKit.SectionHeading(0f, y, w, "CC_Faces_Known".Translate(), -1, null, null, out _);
                return y + UiKit.Paragraph(4f, y, w - 4f, "CC_Faces_Known_Off".Translate(), UiKit.Faint) + 10f;
            }
            BuildIfStale();
            y += UiKit.SectionHeading(0f, y, w, "CC_Faces_Known".Translate(), people.Count, folds, "known", out bool open);
            if (!open) return y + 4f;
            if (people.Count == 0) return y + UiKit.Paragraph(4f, y, w - 4f, "CC_LettersPg_NoOne".Translate(), UiKit.Faint) + 10f;
            foreach (var p in people)
            {
                // Only the rows in sight are drawn.
                if (y + 46f >= sightTop && y <= sightBottom) DrawPerson(new Rect(0f, y, w, 46f), p);
                y += 48f;
            }
            return y + 8f;
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
                letter = ThingRefs.Of(l),
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
                Widgets.DrawHighlightIfMouseover(r);
                LinkText.Line(new Rect(4f, y + 3f, DateW - 6f, 20f), l.date, UiKit.Faint, GameFont.Tiny);
                UiKit.Icon(new Rect(DateW, y + 4f, 16f, 16f), l.call ? Icons.Visitor : Icons.Journal, l.color);
                float th = LinkText.Draw(new Rect(DateW + 22f, y + 2f, tw, 999f), l.title, Color.white);
                if (!l.status.NullOrEmpty()) UiKit.LabelFit(new Rect(DateW + 22f, y + 2f + th, tw, 18f), l.status, l.color, GameFont.Tiny);
                // A click on the row (not on a name) opens the letter's card.
                if (!l.letter.IsNone && Widgets.ButtonInvisible(r, false))
                {
                    Cards.Click();
                    Cards.Open(l.letter);
                }
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
            // The rest of the row opens their card too.
            if (Widgets.ButtonInvisible(r, false))
            {
                Cards.Click();
                Cards.Open(p.thing);
            }
        }
    }

    /// <summary>
    /// Region → History: the world's one history (World/Ledger/History.cs), newest first. At its top the world's events
    /// running now, as a strip (each leading to where it is, or to its people); then its scope (this colony, the region
    /// around it, the whole world) and "Letters lately" as chips; then the filters by a people, a town or a kind of event:
    /// History.Of for a people, History.At (and what the town itself did) for a town, the history's own words
    /// (History.Text) for each line, or the kind and who took part where it has none, each name it mentions a link to
    /// its card and each day a link to the entry's own (UI/Cards/CardWords.HistoryLine). Built again when the history,
    /// the scope or a filter changes, at most once a second. Its scope and filters are part of the way back
    /// (UI/Cards/Nav.cs). Pages other mods still add with MainTabWindow_World.Register are under its "More".
    /// </summary>
    public static class WorldHistoryPage
    {
        static WorldHistoryPage()
        {
            Nav.Filters("WorldHistory", () => filters, SetFilters);
        }

        /// <summary>The scope and filters as the way back keeps them ("R|f3|t55|deed|L"), or null for the view as it opens; made when they change.</summary>
        private static string filters;

        private static void FiltersChanged() =>
            filters = people.IsNone && town.IsNone && family == null && scope == WorldHistoryMath.DefaultScope && !letters
                ? null
                : WorldHistoryMath.ScopeCode(scope) + "|" + people + "|" + town + "|" + (family ?? "") + "|" + (letters ? "L" : "");

        /// <summary>The scope and filters put back from a step of the way back.</summary>
        private static void SetFilters(string text)
        {
            people = town = Ref.None;
            family = null;
            scope = WorldHistoryMath.DefaultScope;
            letters = false;
            if (!text.NullOrEmpty())
            {
                var parts = text.Split('|');
                if (parts.Length > 0) scope = WorldHistoryMath.ScopeOf(parts[0]);
                if (parts.Length > 1) people = Ref.Parse(parts[1]);
                if (parts.Length > 2) town = Ref.Parse(parts[2]);
                if (parts.Length > 3 && parts[3].Length > 0) family = parts[3];
                letters = parts.Length > 4 && parts[4] == "L";
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
        private static HistoryScope scope = WorldHistoryMath.DefaultScope;
        private static bool letters;

        private class Row
        {
            public string date, text;
            public Texture2D icon;
            public Color color;
            public ThingRef entry;
            public float width = -1f, h;
        }

        private sealed class Running
        {
            public string label, tip;
            public ThingRef thing;
            public float width;
        }

        private static List<Row> rows = new List<Row>();
        private static List<Running> running = new List<Running>();
        private static int builtVersion = -1;
        private static float builtAt = -1f, runningAt = -1f;
        private static bool dirty = true;
        private static readonly Dictionary<Ref, string> names = new Dictionary<Ref, string>();

        /// <summary>Opens the region window's History view on what one people (or one town) took part in, anywhere in the world.</summary>
        public static void ShowFor(Ref who, Ref where)
        {
            people = who;
            town = where;
            family = null;
            letters = false;
            scope = HistoryScope.World;
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
            if (Time.realtimeSinceStartup - runningAt > 1f || runningAt > Time.realtimeSinceStartup)
            {
                try
                {
                    BuildRunning();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: the world's events running", e);
                    running = new List<Running>();
                }
                runningAt = Time.realtimeSinceStartup;
            }
            string stat = letters ? "CC_WHist_StatLetters".Translate(LettersPage.LatelyCount.Named("COUNT")).Resolve() : "CC_WHist_Stat".Translate(rows.Count).Resolve();
            float y = rect.y + UiKit.ViewHeader(rect, Icons.History ?? Icons.Chronicle, "CC_Tab_WorldHistory".Translate(), "CC_WHist_Line".Translate(), stat, null, -1, out _);
            y += Strip(new Rect(rect.x, y + 2f, rect.width, 24f)) + 6f;
            y += Chips(new Rect(rect.x, y, rect.width, 24f)) + 6f;
            if (!letters) y += Filters(new Rect(rect.x, y, rect.width, 28f)) + 8f;
            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float ry = 0f, w = view.width;
                if (letters) ry = LettersPage.LatelyRows(ry, w);
                else
                {
                    if (rows.Count == 0)
                        ry += UiKit.EmptyState(0f, ry, w, Icons.History ?? Icons.Chronicle, ("CC_WHist_None_" + scope).Translate(), 80f);
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
                            Widgets.DrawHighlightIfMouseover(row);
                            LinkText.Line(new Rect(4f, ry + 2f, DateW - 6f, 20f), r.date, UiKit.Faint, GameFont.Tiny);
                            UiKit.Icon(new Rect(DateW, ry + 4f, 14f, 14f), r.icon, r.color);
                            LinkText.Draw(new Rect(DateW + 22f, ry + 2f, w - DateW - 28f, r.h), r.text, r.color);
                            // A click on the row (not on a name) opens the entry's own card.
                            if (!r.entry.IsNone && Widgets.ButtonInvisible(row, false))
                            {
                                Cards.Click();
                                Cards.Open(r.entry);
                            }
                        }
                        ry += r.h;
                    }
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

        // ---------------- The world's events running ----------------

        private static void BuildRunning()
        {
            running = new List<Running>();
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            if (ws == null) return;
            foreach (var s in ws.Running.OrderBy(s => s.startTick))
            {
                var thing = !s.place.IsNone ? ThingRef.From(s.place) : s.ctx?.faction != null ? ThingRefs.Of(s.ctx.faction) : ThingRef.None;
                var tip = new List<string> { s.Label };
                string summary = WorldSituationsPanel.Summary(s);
                if (!summary.NullOrEmpty()) tip.Add(summary);
                if (s.deeds.Count > 0) tip.Add("CC_WS_ColonyPart".Translate(TextUtil.JoinAnd(s.deeds.Distinct().ToList())).Resolve());
                if (!s.ctx.placeName.NullOrEmpty()) tip.Add("CC_WHist_EventAt".Translate(s.ctx.placeName.Named("PLACE")).Resolve());
                string label = s.Label;
                running.Add(new Running { label = label, tip = string.Join("\n\n", tip), thing = thing, width = UiKit.KeptWidth(label, GameFont.Tiny) + 30f });
            }
        }

        /// <summary>The world's events running now, as chips along the top: a click leads to where each is (or to its people). Returns the height used.</summary>
        private static float Strip(Rect r)
        {
            UiKit.Icon(new Rect(r.x, r.y + 4f, 16f, 16f), Icons.WorldEvent, UiKit.WorldColor);
            float x = r.x + 22f;
            if (running.Count == 0)
            {
                UiKit.LabelFit(new Rect(x, r.y, r.width - 22f, r.height), (ColonyCouncilMod.Settings.worldSituations ? "CC_WHist_NoEvents" : "CC_EventsPg_Off").Translate(),
                    UiKit.Faint, GameFont.Tiny);
                return r.height;
            }
            for (int i = 0; i < running.Count; i++)
            {
                var s = running[i];
                float w = Mathf.Min(s.width, r.xMax - x);
                if (w < 40f) break;
                var c = new Rect(x, r.y + 1f, w, r.height - 2f);
                bool over = Mouse.IsOver(c);
                Widgets.DrawBoxSolidWithOutline(c, over ? UiKit.CardHover : UiKit.CardFill, UiKit.WithAlpha(UiKit.WorldColor, 0.45f), 1);
                UiKit.DiscFill(new Rect(c.x + 7f, c.y + c.height / 2f - 3.5f, 7f, 7f), UiKit.WorldColor);
                UiKit.LabelFit(new Rect(c.x + 18f, c.y, c.width - 22f, c.height), s.label, over ? Color.white : UiKit.WorldColor, GameFont.Tiny);
                TooltipHandler.TipRegion(c, s.tip);
                if (!s.thing.IsNone && Widgets.ButtonInvisible(c, false))
                {
                    Cards.Click();
                    if (s.thing.IsPlace) Cards.ShowOnMap(s.thing);
                    else Cards.Open(s.thing);
                }
                x += w + 6f;
            }
            return r.height;
        }

        // ---------------- Scope, letters and the pages under More ----------------

        private static string[] scopeLabels, scopeTips;

        /// <summary>The scope's chips (this colony, the region, the world), "Letters lately", and More for other mods' pages. Returns the height used.</summary>
        private static float Chips(Rect r)
        {
            scopeLabels ??= new[] { "CC_WHist_Scope_Colony".Translate().ToString(), "CC_WHist_Scope_Region".Translate().ToString(), "CC_WHist_Scope_World".Translate().ToString() };
            scopeTips ??= new[] { "CC_WHist_Scope_Colony_Tip".Translate().ToString(), "CC_WHist_Scope_Region_Tip".Translate().ToString(), "CC_WHist_Scope_World_Tip".Translate().ToString() };
            float x = r.x;
            for (int i = 0; i < 3; i++)
            {
                float w = UiKit.FilterChipWidth(scopeLabels[i]);
                if (UiKit.FilterChip(new Rect(x, r.y, w, r.height), scopeLabels[i], !letters && (int)scope == i, scopeTips[i]))
                {
                    scope = (HistoryScope)i;
                    letters = false;
                    FiltersChanged();
                    dirty = true;
                    scroll = Vector2.zero;
                }
                x += w + 4f;
            }
            x += 8f;
            string lettersLabel = "CC_WHist_Letters".Translate();
            float lw = UiKit.FilterChipWidth(lettersLabel);
            if (UiKit.FilterChip(new Rect(x, r.y, lw, r.height), lettersLabel, letters, "CC_WHist_Letters_Tip".Translate()))
            {
                letters = !letters;
                FiltersChanged();
                scroll = Vector2.zero;
            }
            // Pages other mods added to the region window, until they find their place in a view.
            var more = MainTabWindow_World.ExtraPages();
            if (more.Count > 0)
            {
                string label = "CC_WHist_More".Translate();
                float mw = UiKit.FilterChipWidth(label) + 8f;
                if (UiKit.FilterChip(new Rect(r.xMax - mw, r.y, mw, r.height), label, false, "CC_WHist_More_Tip".Translate()))
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var p in more)
                    {
                        string name = p.name;
                        options.Add(new FloatMenuOption(p.Label, () => Pages.Open(name), p.icon, Color.white));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            return r.height;
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
            else found = book.OfKind(family, max: scope == HistoryScope.World ? WorldHistoryMath.MaxShown : int.MaxValue);
            var parties = Parties(scope);
            foreach (var e in found)
            {
                if (rows.Count >= WorldHistoryMath.MaxShown) break;
                if (!WorldHistoryMath.Keeps(e, people, town, family) || !WorldHistoryMath.InScope(e, scope, parties)) continue;
                rows.Add(MakeRow(e));
            }
        }

        /// <summary>
        /// Who a scope is about: for the colony, its homes, its people and its own colonists; for the region, those and the
        /// towns within reach, their peoples and the people of them the colony knows by name. Null for the world.
        /// </summary>
        private static HashSet<Ref> Parties(HistoryScope s)
        {
            if (s == HistoryScope.World) return null;
            var set = new HashSet<Ref>();
            foreach (var home in Homes.All) set.Add(Refs.Colony(home));
            set.Add(Refs.Of(Faction.OfPlayerSilentFail));
            foreach (var p in WeekModel.Colonists()) set.Add(Refs.Of(p));
            if (s == HistoryScope.Colony) return set;
            var reach = RegionTowns.InDetail();
            foreach (int id in reach)
            {
                set.Add(Ref.Town(id));
                var t = NotableBook.TownOf(id);
                if (t?.Faction != null) set.Add(Refs.Of(t.Faction));
            }
            var book = NotableBook.Instance;
            if (book != null)
                foreach (var n in book.notables)
                    if (n != null && reach.Contains(n.town))
                        set.Add(n.Person);
            return set;
        }

        private static Row MakeRow(HistoryEntry e)
        {
            // Its words with each name it mentions a link to its card, and its day a link to its own (Rimfolk's windows only).
            string text = CardWords.HistoryLine(e, true);
            string fam = HistoryKinds.Family(e.kind);
            int ck = Array.IndexOf(HistoryKinds.Chronicle, fam);
            return new Row
            {
                entry = ThingRefs.Of(e),
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
