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
    /// The people of a town the colony knows by name (head, heir, envoy, trader, and the faces it met who live there), how
    /// each regards the colony in words, which colonists they know, the last thing between them or the letter on its way,
    /// and a button to write: the Who of a town's card (UI/Cards/), registered at startup. The towns within reach, nearest
    /// first, with their people, are Region → Map's Stance and ties lens as a list (UI/Region/LensModel.cs), which was the
    /// Neighbours page.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NeighboursPanel
    {
        private const float SmallRowH = 46f;

        static NeighboursPanel()
        {
            CardSections.Town(CardSlot.Who, 10f, DrawTown, "CC_Neighbours_TownHeader", "notables", CountAt);
        }

        private class Row
        {
            public Notable n;
            public ThingRef thing;
            /// <summary>The name as a link to their card, with their title: "Edda Marsh, head".</summary>
            public string line;
            public string name, title, regard, knows, last, status, why, tip;
            public Color regardColor;
        }

        private class Town
        {
            public Settlement s;
            /// <summary>The town's name as a link to its card.</summary>
            public string link;
            public string name;
            public List<Row> rows = new List<Row>();
        }

        private static List<Town> towns = new List<Town>();
        private static float builtAt = -1f;
        private static int builtVersion = -1;

        private static void BuildIfStale()
        {
            if (builtVersion == NotableBook.Version && Time.realtimeSinceStartup - builtAt < 2f) return;
            builtAt = Time.realtimeSinceStartup;
            builtVersion = NotableBook.Version;
            try
            {
                towns = Build();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("neighbours panel", e);
                towns = new List<Town>();
            }
        }

        private static List<Town> Build()
        {
            var list = new List<Town>();
            var book = NotableBook.Instance;
            var map = HappeningTracker.HomeMap();
            if (book == null || map == null) return list;
            // Every town with notables: the seat's, and each further home's nearest two (decision 14A).
            foreach (var s in book.AllTowns(map))
            {
                var t = new Town { s = s, name = s.Name, link = ThingRefs.Link(s.Name, ThingRefs.Of(s)) };
                foreach (var n in RowsOf(book, s.ID)) t.rows.Add(MakeRow(n));
                list.Add(t);
            }
            return list;
        }

        private static IEnumerable<Notable> RowsOf(NotableBook book, int town) =>
            book.notables.Where(n => n.town == town && n.Alive).OrderBy(n => (int)n.role).ThenByDescending(n => n.notes.Count);

        private static Row MakeRow(Notable n)
        {
            int op = NotableBook.Opinion(n);
            var r = new Row
            {
                n = n, thing = ThingRefs.Of(n), name = n.Name, title = NotableBook.TitleOf(n).CapitalizeFirst(), regard = NotableBook.RegardWords(op),
                regardColor = NotableBook.RegardColor(op)
            };
            r.line = ThingRefs.Link(r.name, r.thing) + ", " + r.title;
            var known = NotableBook.Known(n).Take(3).ToList();
            // The colonists who know them, each a link to their card.
            if (known.Count > 0)
                r.knows = "CC_Neighbours_Knows".Translate(TextUtil.JoinAnd(known.Select(k =>
                {
                    string who = ThingRefs.Link(k.pawn.LabelShort, ThingRefs.Of(k.pawn));
                    return k.why.NullOrEmpty() ? who : "CC_Neighbours_KnowsFor".Translate(who, k.why).Resolve();
                }).ToList())).Resolve();
            var l = NeighbourLetters.OpenTo(n);
            if (l != null) r.status = StatusWords(l, n);
            var note = n.notes.LastOrDefault();
            if (note != null) r.last = "CC_Neighbours_Last".Translate(note.text, FactionMemory.AgoText(note.tick)).Resolve();
            r.why = WhyNoLetter(n);
            r.tip = Tip(r);
            return r;
        }

        /// <summary>Why no letter or call can start to them now (none of the purposes is open), or null.</summary>
        internal static string WhyNoLetter(Notable n)
        {
            if (!NeighbourLetters.On) return "CC_Letter_Why_Off".Translate();
            string first = null;
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            {
                // A dealing with their council is asked from its own acts (Dealings/DealingActs.cs).
                if (DealingLetters.Owns(p)) continue;
                string why = NeighbourLetters.WhyNot(n, p);
                if (why == null) return null;
                first ??= why;
            }
            return first;
        }

        /// <summary>Where the letter is, in words (never a countdown).</summary>
        public static string StatusWords(NeighbourLetter l, Notable n)
        {
            // Passed on to the head by the one it was written to (World/LetterRoutes.cs): with the head now.
            if (l.passed && (l.stage == LetterStage.Travelling || l.stage == LetterStage.CallbackWait))
            {
                var book = NotableBook.Instance;
                return "CC_Route_Status_Passed".Translate(l.WriterName.Named("PAWN"), (book?.Get(l.notable)?.Name ?? "?").Named("NAME"),
                    (book?.Get(l.passTo)?.Name ?? "?").Named("HEAD")).Resolve();
            }
            // The post's stages: in the mailbag, with the colony's rider, on the road with whoever took it (Post/PostWords.cs).
            string post = PostWords.Status(l, n);
            if (post != null) return post;
            // On the road, or its answer on the way back, by the colony's means (a runner, a rider, a courier, a courier by shuttle).
            if (l.channel == LetterChannel.Courier && l.bearer == null && l.bearerName.NullOrEmpty() && (l.stage == LetterStage.Travelling || l.stage == LetterStage.Answering && l.tripId >= 0))
                return ("CC_LetterRoad_Status_" + l.stage + "_" + NeighbourLetters.Means).Translate(l.WriterName.Named("PAWN"), n.Name.Named("NAME")).Resolve();
            string key = "CC_Letter_Status_" + l.stage;
            return key.CanTranslate() ? key.Translate(l.WriterName.Named("PAWN"), n.Name.Named("NAME")).Resolve() : null;
        }

        private static void DrawRow(Rect r, Row row, bool small)
        {
            float size = small ? 32f : 40f;
            // Their portrait and their name lead to their card (a preview after a pause); what they remember stays in the
            // tooltip of the lines under the name.
            var pic = new Rect(r.x, r.y + 4f, size, size);
            UiKit.Portrait(pic, row.n.pawn, row.regardColor);
            if (Mouse.IsOver(pic)) Previews.Hover(row.thing, pic);
            if (Widgets.ButtonInvisible(pic, false))
            {
                Cards.Click();
                Cards.Open(row.thing);
            }
            float x = r.x + size + 8f;
            const float btnW = 96f;
            float textW = r.xMax - x - btnW - 8f;
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(x, r.y + 20f, textW, r.height - 20f), row.tip);
            LinkText.Line(new Rect(x, r.y + 2f, textW, 20f), row.line, UiKit.Ink);
            string second = row.regard + (row.knows.NullOrEmpty() || small ? "" : ". " + row.knows);
            LinkText.Line(new Rect(x, r.y + 20f, textW, 18f), second, row.regardColor, GameFont.Tiny);
            string third = row.status ?? (small ? row.knows : row.last);
            if (!third.NullOrEmpty()) LinkText.Line(new Rect(x, r.y + 36f, textW, 18f), third, row.status != null ? UiKit.Info : UiKit.Faint, GameFont.Tiny);
            var b = new Rect(r.xMax - btnW, r.y + (small ? 8f : 12f), btnW, 26f);
            bool can = row.why == null;
            if (UiKit.Button(b, "CC_Neighbours_Write".Translate(), ButtonKind.Normal, can))
            {
                if (can) Find.WindowStack.Add(new Dialog_NeighbourLetter(row.n));
            }
            if (!can) TooltipHandler.TipRegion(b, row.why);
            Widgets.DrawLineHorizontal(r.x, r.yMax - 1f, r.width, UiKit.DividerColor);
        }

        /// <summary>Everything they remember of the colony, newest last, with the date of each.</summary>
        private static string Tip(Row row)
        {
            var n = row.n;
            var parts = new List<string> { NotableBook.Called(n) + ": " + row.regard + "." };
            if (!row.knows.NullOrEmpty()) parts.Add(LinkTextMath.Plain(row.knows));
            if (n.notes.Count > 0)
            {
                parts.Add("");
                parts.Add("CC_Neighbours_Remembers".Translate().Resolve());
                foreach (var note in n.notes) parts.Add("  - " + note.text + " (" + DayOf(note.tick) + ")");
            }
            return string.Join("\n", parts);
        }

        private static string DayOf(int tick)
        {
            var map = HappeningTracker.HomeMap();
            float lng = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            return GenDate.DateShortStringAt(GenDate.TickGameToAbs(tick), new Vector2(lng, 0f));
        }

        // ---------------- The town's card ----------------

        /// <summary>How many of a town's people the colony knows by name (the count in the heading of its card's Who).</summary>
        private static int CountAt(Settlement s)
        {
            BuildIfStale();
            foreach (var t in towns)
                if (t.s == s)
                    return t.rows.Count;
            return -1;
        }

        /// <summary>A town's notables and a button to write to each, for the town's card (its Who slot; the card draws the heading).</summary>
        public static float DrawTown(Rect rect, Settlement s)
        {
            if (!NotableBook.On || s == null) return 0f;
            try
            {
                BuildIfStale();
                Town t = null;
                foreach (var x in towns)
                    if (x.s == s)
                        t = x;
                float y = rect.y;
                // Who speaks for the town: its head, or that nobody the colony knows does yet (World/LetterRoutes.cs).
                string speaks = t != null && t.rows.Count > 0 ? LetterRoutes.SpeaksFor(s) : null;
                if (!speaks.NullOrEmpty())
                {
                    float sh = Text.CalcHeight(speaks, rect.width);
                    GUI.color = UiKit.Info;
                    Widgets.Label(new Rect(rect.x, y, rect.width, sh), speaks);
                    GUI.color = Color.white;
                    y += sh + 2f;
                }
                if (t == null || t.rows.Count == 0)
                {
                    string none = t == null ? "CC_Neighbours_OutOfReach".Translate(s.Name).Resolve() : "CC_Neighbours_NobodyYet".Translate(s.Name).Resolve();
                    float h = Text.CalcHeight(none, rect.width);
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(rect.x, y, rect.width, h), none);
                    GUI.color = Color.white;
                    return y + h + 4f - rect.y;
                }
                foreach (var r in t.rows)
                {
                    DrawRow(new Rect(rect.x, y, rect.width, SmallRowH + 10f), r, true);
                    y += SmallRowH + 10f;
                }
                return y + 4f - rect.y;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("neighbours town section", e);
                return 0f;
            }
        }
    }

    /// <summary>
    /// Writing to a neighbour: whom (any notable of the same town), who they are and whether they speak for the town (with a
    /// button to write to its head instead: World/LetterRoutes.cs), what for in two sections, "To the town" (its matters,
    /// for its head: written to anyone else, asked of them to pass on) and "To {NAME}" (personal letters), each purpose with
    /// why it can't be when it can't, and for a kind letter the occasion that makes it fit now (World/LetterOccasions.cs);
    /// what about (the loss, the threat, the help asked for), who writes (the best with words by default) and how (a letter
    /// by hand, or a call on the comms console when both ends can), and in words how it is likely to be taken. In Rimfolk's
    /// frame (key "Letter"), its title in the title bar, its buttons the kit's (Send the primary act).
    /// </summary>
    public class Dialog_NeighbourLetter : FramedWindow
    {
        private Notable to;
        private LetterPurpose purpose = LetterPurpose.Greetings;
        /// <summary>Whose matter the chosen purpose is (a debt or a dealing can be either: World/LetterAddressMath.cs).</summary>
        private LetterScope scope = LetterScope.Personal;
        private LetterChannel channel = LetterChannel.Courier;
        private Pawn writer;
        /// <summary>Someone of their people at the colony who'll carry it home (Outsiders/), or null for a messenger or a call.</summary>
        private readonly Pawn bearer;
        private int topicIndex;
        private string help = "medicine";
        /// <summary>What a letter of stage 2's purposes asks (Diplomacy/DiplomacyLetters.cs: FavourMath.AskCode), or null.</summary>
        private string dipAsk;
        private readonly Map map;
        // What can and can't be written, looked at twice a second rather than every frame (finding a messenger looks over
        // every people and town).
        private float lookedAt = -1f;
        private Notable lookedFor;
        private LetterPurpose lookedPurpose;
        private LetterScope lookedScope;
        private LetterChannel lookedChannel;
        private string byHand, byCall, byRider;
        /// <summary>The colony's own rider takes it (Post/OwnRiders.cs), and who.</summary>
        private bool riderChosen;
        private Pawn rider;
        /// <summary>The rider's supplies for the road (Post/RiderPacks.cs), chosen here with them.</summary>
        private readonly RiderPackPart riderPack = new RiderPackPart();
        private List<string> reach = new List<string>();
        private List<LetterTopic> topics = new List<LetterTopic>();

        /// <summary>One purpose as the window offers it: its section, the one who answers it, why not, and its note (the occasion, or who passes it on).</summary>
        private sealed class Entry
        {
            public LetterPurpose p;
            public LetterScope scope;
            public LetterWay way;
            public Notable who;
            public string label, why, note;
        }

        private readonly List<Entry> entries = new List<Entry>();
        /// <summary>Who they are and whether they speak for the town; whom the window offers to write to instead.</summary>
        private string header;
        private Notable other;
        /// <summary>The one who answers the chosen purpose (the one written to, or the head it is passed on to), and the way it goes there.</summary>
        private Notable answerer;
        private string route;

        private void Look()
        {
            if (lookedFor == to && lookedPurpose == purpose && lookedScope == scope && lookedChannel == channel && Time.realtimeSinceStartup - lookedAt < 0.5f) return;
            lookedAt = Time.realtimeSinceStartup;
            lookedFor = to;
            lookedPurpose = purpose;
            lookedScope = scope;
            lookedChannel = channel;
            byHand = NeighbourLetters.ChannelWhyNot(to, LetterChannel.Courier, map);
            byCall = NeighbourLetters.ChannelWhyNot(to, LetterChannel.Console, map);
            byRider = byHand ?? OwnRiders.WhyNot(to, map);
            reach = PostWords.ReachLines(map, NotableBook.TownOf(to.town));
            header = LetterRoutes.Header(to, out other, map);
            entries.Clear();
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            {
                // A purpose no longer written (the council's business, retired) is never offered; a dealing with their council
                // only when the window was opened for one (Dealings/DealingActs.cs).
                if (NotableMath.Retired(p) || DealingLetters.Owns(p) && purpose != p) continue;
                foreach (var sc in ScopesOf(p))
                {
                    var way = LetterRoutes.Way(to, p, sc, out var who, map);
                    // Asking for a town back only of (or through someone of) a town the colony was (Colonies/TownTakeBack.cs).
                    if (TownLetters.Owns(p) && !TownLetters.Shows(who ?? to)) continue;
                    var e = new Entry { p = p, scope = sc, way = way, who = who, label = Label(p, sc), why = LetterRoutes.WhyNot(to, p, sc, true) };
                    if (e.why == null)
                    {
                        if (way == LetterWay.PassOn) e.note = "CC_Route_Row_Pass".Translate(LetterRoutes.Short(who).Named("HEAD")).Resolve();
                        // Letters by occasion (World/LetterOccasions.cs): the reason a kind letter fits now.
                        else if (LetterOccasionMath.ByOccasion(p) && p != LetterPurpose.News) e.note = LetterOccasions.Reason(who ?? to, p);
                    }
                    entries.Add(e);
                }
            }
            var cur = Current;
            answerer = cur?.who ?? to;
            topics = LetterRoutes.Topics(to, purpose, scope);
            unlisted = purpose == LetterPurpose.Warning ? Warnings.NotListedLines(answerer) : new List<string>();
            route = cur != null && cur.way == LetterWay.PassOn && cur.why == null ? LetterRoutes.Route(to, cur.who, channel) : null;
        }

        /// <summary>The chosen purpose's entry, or null.</summary>
        private Entry Current => entries.FirstOrDefault(e => e.p == purpose && e.scope == scope);

        /// <summary>The scopes a purpose is offered in: a debt both ways (their people's, and one they owe in person), a dealing as it was opened, the rest their own.</summary>
        private IEnumerable<LetterScope> ScopesOf(LetterPurpose p)
        {
            if (p == LetterPurpose.Favour) return new[] { LetterScope.Town, LetterScope.Personal };
            if (p == LetterPurpose.Dealing) return new[] { scope };
            return new[] { LetterAddressMath.ScopeOf(p, false) };
        }

        private static string Label(LetterPurpose p, LetterScope sc)
        {
            if (p == LetterPurpose.Favour && sc == LetterScope.Personal) return "CC_Route_PurposeCap_FavourPersonal".Translate().Resolve();
            if (p == LetterPurpose.Dealing && sc == LetterScope.Personal) return "CC_Route_PurposeCap_DealingReply".Translate().Resolve();
            return ("CC_Letter_PurposeCap_" + p).Translate().Resolve();
        }

        /// <summary>Raiders the colony knows of not listed as a warning to them, and why (World/Warnings.cs).</summary>
        private List<string> unlisted = new List<string>();

        public override Vector2 InitialSize => new Vector2(600f, 860f);

        private static string title;

        protected override string FrameTitle => title ??= "CC_LetterDlg_Title".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.Journal;

        public Dialog_NeighbourLetter(Notable n) : base("Letter")
        {
            to = n;
            map = HappeningTracker.HomeMap();
            writer = NeighbourLetters.DefaultWriter(map);
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            // The first purpose that can be written (a dealing only when chosen for it), and a letter by hand when a messenger can come.
            FirstOpen(n);
            if (NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, map) != null && NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, map) == null)
                channel = LetterChannel.Console;
            // Their own letter waits for an answer (Dealings/WorldWrites.cs): the dialog opens on answering it.
            var ask = WorldWrites.OpenAsk(n);
            if (ask != null && LetterRoutes.WhyNot(n, LetterPurpose.Dealing, LetterScope.Personal, true) == null)
            {
                int i = LetterRoutes.Topics(n, LetterPurpose.Dealing, LetterScope.Personal).FindIndex(t => t.key == DealingLetters.ReplyPrefix + ask.id);
                if (i >= 0)
                {
                    purpose = LetterPurpose.Dealing;
                    scope = LetterScope.Personal;
                    topicIndex = i;
                }
            }
        }

        /// <summary>A letter for a purpose and what it's about, chosen beforehand (the Diplomacy page's "call it in": Diplomacy/DiplomacyPanels.cs).</summary>
        public Dialog_NeighbourLetter(Notable n, LetterPurpose p, string topicKey) : this(n)
        {
            var sc = LetterRoutes.ScopeOf(p, topicKey, n);
            // A debt chosen without its topic: whichever of its two is open.
            if (topicKey == null && p == LetterPurpose.Favour && LetterRoutes.WhyNot(n, p, sc, true) != null) sc = LetterScope.Personal;
            if (LetterRoutes.WhyNot(n, p, sc, true) != null) return;
            purpose = p;
            scope = sc;
            int i = LetterRoutes.Topics(n, p, sc).FindIndex(t => t.key == topicKey);
            topicIndex = Mathf.Max(0, i);
        }

        /// <summary>A letter for <paramref name="bearer"/>, someone of their people at the colony, to carry home; <paramref name="by"/> writes it unless the player picks someone else.</summary>
        public Dialog_NeighbourLetter(Notable n, Pawn bearer, Pawn by) : this(n)
        {
            this.bearer = bearer;
            channel = LetterChannel.Courier;
            if (by != null && NeighbourLetters.Writers(map, true).Contains(by)) writer = by;
        }

        /// <summary>The first purpose (and its scope) that can be written to them, a dealing only when chosen for it.</summary>
        private void FirstOpen(Notable n)
        {
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            {
                if (DealingLetters.Owns(p) || NotableMath.Retired(p)) continue;
                foreach (var sc in p == LetterPurpose.Favour ? new[] { LetterScope.Town, LetterScope.Personal } : new[] { LetterAddressMath.ScopeOf(p, false) })
                    if (LetterRoutes.WhyNot(n, p, sc, true) == null)
                    {
                        purpose = p;
                        scope = sc;
                        return;
                    }
            }
        }

        protected override void DoFramedContents(Rect inRect)
        {
            try
            {
                Contents(inRect);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("neighbour letter dialog", e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private const float RowH = 23f;

        private void Contents(Rect inRect)
        {
            if (to == null || !to.Alive)
            {
                Close();
                return;
            }
            Look();
            float y = inRect.y;
            // The title, unless the frame has it in its bar.
            if (!TitleInBar)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "CC_LetterDlg_Title".Translate());
                Text.Font = GameFont.Small;
                y += 38f;
            }
            else y += 4f;
            // Whom.
            UiKit.Portrait(new Rect(inRect.x, y, 44f, 44f), to.pawn, NotableBook.RegardColor(NotableBook.Opinion(to)));
            var whom = new Rect(inRect.x + 52f, y, inRect.width - 52f, 24f);
            var mates = NotableBook.Instance?.notables.Where(x => x.town == to.town && x.Alive).ToList() ?? new List<Notable>();
            if (UiKit.Button(new Rect(whom.x, whom.y, Mathf.Min(whom.width, 380f), 24f), NotableBook.Called(to), ButtonKind.Normal, mates.Count > 1))
                Find.WindowStack.Add(new FloatMenu(mates.Select(m => new FloatMenuOption(NotableBook.Called(m), () => Pick(m))).ToList()));
            int op = NotableBook.Opinion(to);
            UiKit.LabelFit(new Rect(whom.x, y + 26f, whom.width, 18f), NotableBook.RegardWords(op).CapitalizeFirst(), NotableBook.RegardColor(op), GameFont.Tiny);
            y += 50f;
            // Who they are to their town, and whom to write to for its matters (World/LetterRoutes.cs).
            if (!header.NullOrEmpty())
            {
                const float btnW = 150f;
                float hw = other != null ? inRect.width - btnW - 8f : inRect.width;
                Text.Font = GameFont.Tiny;
                float hh = Mathf.Max(Text.CalcHeight(header, hw), other != null ? 26f : 0f);
                GUI.color = UiKit.Info;
                Widgets.Label(new Rect(inRect.x, y, hw, hh), header);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                if (other != null)
                {
                    var o = other;
                    if (UiKit.Button(new Rect(inRect.xMax - btnW, y, btnW, 26f), "CC_Route_WriteTo".Translate(o.Name.Named("NAME")).Resolve()))
                        Pick(o);
                    TooltipHandler.TipRegion(new Rect(inRect.xMax - btnW, y, btnW, 26f), "CC_Route_WriteTo_Tip".Translate(NotableBook.Called(o).Named("NAME")).Resolve());
                }
                y += hh + 6f;
            }
            // What for: the town's matters and the personal letters, each in its section; the section of what they can answer
            // themselves first.
            bool speaks = entries.Any(e => e.scope == LetterScope.Town && e.p == LetterPurpose.Warning && e.way == LetterWay.Direct);
            var townRows = entries.Where(e => LetterAddressMath.TownSection(e.scope)).ToList();
            var ownRows = entries.Where(e => !LetterAddressMath.TownSection(e.scope)).ToList();
            string townTitle = speaks || townRows.All(e => e.way != LetterWay.PassOn)
                ? "CC_Route_Section_Town".Translate().Resolve()
                : "CC_Route_Section_TownVia".Translate(to.Name.Named("NAME")).Resolve();
            string ownTitle = "CC_Route_Section_Person".Translate(to.Name.Named("NAME")).Resolve();
            if (speaks)
            {
                y = Section(inRect, y, townTitle, townRows);
                y = Section(inRect, y, ownTitle, ownRows);
            }
            else
            {
                y = Section(inRect, y, ownTitle, ownRows);
                y = Section(inRect, y, townTitle, townRows);
            }
            Look();
            var cur = Current;
            string purposeWhy = cur?.why ?? (cur == null ? "CC_Letter_Why_Gone".Translate().Resolve() : null);
            // Passed on: who takes it to whom, how much slower, and their word.
            if (!route.NullOrEmpty() && purposeWhy == null)
            {
                Text.Font = GameFont.Tiny;
                float rh = Text.CalcHeight(route, inRect.width - 10f);
                GUI.color = UiKit.Info;
                Widgets.Label(new Rect(inRect.x + 10f, y, inRect.width - 10f, rh), route);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += rh + 4f;
            }
            // What about.
            if ((purpose == LetterPurpose.Condolences || purpose == LetterPurpose.Warning) && topics.Count > 0)
            {
                topicIndex = Mathf.Clamp(topicIndex, 0, topics.Count - 1);
                Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_About".Translate());
                if (UiKit.Button(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topics[topicIndex].text, ButtonKind.Normal, topics.Count > 1))
                    Find.WindowStack.Add(new FloatMenu(topics.Select((t, i) => new FloatMenuOption(t.text, () => topicIndex = i)).ToList()));
                y += 30f;
            }
            // A warning: raiders the colony knows of that aren't listed, and why (too far, too old, not their enemy).
            if (purpose == LetterPurpose.Warning)
                foreach (var line in unlisted)
                {
                    UiKit.LabelFit(new Rect(inRect.x + 124f, y, inRect.width - 124f, 18f), line, UiKit.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(new Rect(inRect.x + 124f, y, inRect.width - 124f, 18f), line);
                    y += 18f;
                }
            // What about and what is asked, for stage 2's purposes (Diplomacy/DiplomacyLetters.cs).
            string dipWhy = null;
            if (DiplomacyLetters.Owns(purpose) && purposeWhy == null) y += DiplomacyLetters.DrawRows(inRect, y, topics, ref topicIndex, ref dipAsk, map, out dipWhy);
            if (purpose == LetterPurpose.Help)
            {
                Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_Ask".Translate());
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 124f, y, 160f, 24f), "CC_Letter_Help_Medicine".Translate().CapitalizeFirst(), help == "medicine")) help = "medicine";
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 300f, y, 160f, 24f), "CC_Letter_Help_Food".Translate().CapitalizeFirst(), help == "food")) help = "food";
                y += 30f;
            }
            // Who writes.
            var writers = NeighbourLetters.Writers(map, true);
            if (writer == null || !writers.Contains(writer)) writer = NeighbourLetters.DefaultWriter(map);
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), (NeighbourLetters.Spoken ? "CC_LetterDlg_WhoSpeaks" : "CC_LetterDlg_Who").Translate());
            if (UiKit.Button(new Rect(inRect.x + 124f, y, Mathf.Min(inRect.width - 124f, 260f), 26f), writer != null ? WriterLabel(writer) : "-", ButtonKind.Normal, writers.Count > 0))
                Find.WindowStack.Add(new FloatMenu(NeighbourLetters.ByWords(writers).Select(p => new FloatMenuOption(WriterLabel(p), () => writer = p)).ToList()));
            y += 30f;
            // How.
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_How".Translate());
            if (bearer != null)
            {
                Widgets.Label(new Rect(inRect.x + 124f, y + 2f, inRect.width - 124f, 24f), "CC_LetterDlg_ByBearer".Translate(bearer.Named("BEARER")).Resolve());
            }
            else
            {
                // By hand (in the mailbag, or a spoken message in a tribal colony), by the colony's own rider, or by call.
                string handLabel = (NeighbourLetters.Spoken ? "CC_LetterDlg_BySpoken" : PostBook.On ? "CC_LetterDlg_ByPost" : "CC_LetterDlg_ByHand").Translate();
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 124f, y, 200f, 24f), handLabel, channel == LetterChannel.Courier && !riderChosen, byHand != null) && byHand == null)
                {
                    channel = LetterChannel.Courier;
                    riderChosen = false;
                }
                if (byHand != null) TooltipHandler.TipRegion(new Rect(inRect.x + 124f, y, 200f, 24f), byHand);
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 340f, y, 200f, 24f), "CC_LetterDlg_ByCall".Translate(), channel == LetterChannel.Console, byCall != null) && byCall == null)
                {
                    channel = LetterChannel.Console;
                    riderChosen = false;
                }
                if (byCall != null) TooltipHandler.TipRegion(new Rect(inRect.x + 340f, y, 200f, 24f), byCall);
                if (PostBook.On)
                {
                    y += 26f;
                    var riderRect = new Rect(inRect.x + 124f, y, 200f, 24f);
                    if (Widgets.RadioButtonLabeled(riderRect, "CC_LetterDlg_ByRider".Translate(), channel == LetterChannel.Courier && riderChosen, byRider != null) && byRider == null)
                    {
                        channel = LetterChannel.Courier;
                        riderChosen = true;
                    }
                    if (byRider != null) TooltipHandler.TipRegion(riderRect, byRider);
                    if (riderChosen)
                    {
                        var riders = OwnRiders.Candidates(map);
                        if (rider == null || !riders.Contains(rider)) rider = riders.FirstOrDefault();
                        if (UiKit.Button(new Rect(inRect.x + 340f, y, 200f, 24f), rider?.LabelShort ?? "-", ButtonKind.Normal, riders.Count > 1))
                            Find.WindowStack.Add(new FloatMenu(riders.Select(p => new FloatMenuOption(p.LabelShort, () => rider = p)).ToList()));
                    }
                }
            }
            y += 26f;
            // The rider's supplies for the road: what is packed and whether it reaches (Post/RiderPacks.cs).
            bool packing = bearer == null && PostBook.On && channel == LetterChannel.Courier && riderChosen && byRider == null && rider != null;
            if (packing) y += riderPack.Draw(new Rect(inRect.x + 124f, y, inRect.width - 124f, 0f), map, to, rider);
            string channelWhy = bearer != null ? (bearer.Spawned && Outsiders.IsOutsider(bearer) ? null : "CC_LetterDlg_BearerGone".Translate(bearer.Named("BEARER")).Resolve())
                : channel == LetterChannel.Courier ? (riderChosen ? byRider ?? (rider == null ? "CC_Mail_Why_NoRider".Translate().Resolve() : packing ? riderPack.Why : null) : byHand) : byCall;
            string how = channelWhy ?? (bearer != null ? "CC_LetterDlg_BearerNote".Translate(bearer.Named("BEARER"), (to.faction?.Name ?? "").Named("FACTION")).Resolve()
                : channel == LetterChannel.Courier && riderChosen
                ? "CC_LetterDlg_RiderNote".Translate((rider?.LabelShort ?? "?").Named("PAWN")).Resolve()
                : channel == LetterChannel.Courier
                ? (NotableBook.Hostile(to) ? "CC_LetterDlg_WhiteFlag" : PostBook.On ? (NeighbourLetters.Spoken ? "CC_LetterDlg_SpokenNote" : "CC_LetterDlg_PostNote") : "CC_LetterDlg_HandNote")
                    .Translate(to.faction?.Name ?? "").Resolve()
                : "CC_LetterDlg_CallNote".Translate().Resolve());
            // How to reach them now (Post/PostWords.cs): by radio, the post, a relay, out of reach, and the colony's own rider.
            if (channelWhy == null && bearer == null && reach.Count > 0) how += "\n" + string.Join(" ", reach);
            float hh2 = Text.CalcHeight(how, inRect.width - 124f);
            GUI.color = channelWhy != null ? UiKit.Warn : UiKit.Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x + 124f, y, inRect.width - 124f, hh2 + 4f), how);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            y += hh2 + 10f;
            // How it may be taken, by the one who answers it (passed on: with the go-between's word).
            if (writer != null && purposeWhy == null && answerer != null)
            {
                string outlook = NeighbourLetters.Outlook(answerer, writer, purpose, channel, answerer != to ? to : null);
                float oh = Text.CalcHeight(outlook, inRect.width);
                GUI.color = UiKit.Quote;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, oh), outlook);
                GUI.color = Color.white;
                y += oh + 6f;
            }
            // Send.
            bool ok = writer != null && purposeWhy == null && channelWhy == null && dipWhy == null;
            var send = new Rect(inRect.x, inRect.yMax - 34f, 200f, 32f);
            if (UiKit.Button(send, (channel == LetterChannel.Console ? "CC_LetterDlg_Call" : "CC_LetterDlg_Send").Translate(), ButtonKind.Primary, ok))
            {
                bool about = purpose == LetterPurpose.Condolences || purpose == LetterPurpose.Warning || DiplomacyLetters.Owns(purpose);
                LetterTopic? topic = topics.Count > 0 && about ? topics[Mathf.Clamp(topicIndex, 0, topics.Count - 1)] : (LetterTopic?)null;
                string asked = purpose == LetterPurpose.Help ? help : DiplomacyLetters.Owns(purpose) ? dipAsk : null;
                bool byOwnRider = channel == LetterChannel.Courier && riderChosen;
                // The supplies go with the letter the window sends now (taken by OwnRiders.Send).
                RiderPacks.ForNext(byOwnRider ? riderPack.Pack : null);
                try
                {
                    if (NeighbourLetters.Send(to, purpose, channel, writer, topic, asked, bearer, byOwnRider ? rider : null) != null) Close();
                }
                finally
                {
                    RiderPacks.ForNext(null);
                }
            }
            if (UiKit.Button(new Rect(inRect.xMax - 160f, inRect.yMax - 34f, 160f, 32f), "CC_LetterDlg_Cancel".Translate())) Close();
        }

        /// <summary>A section of purposes under its title: each a choice, greyed with why when it can't be, else with its note.</summary>
        private float Section(Rect inRect, float y, string head, List<Entry> rows)
        {
            if (rows.Count == 0) return y;
            UiKit.LabelFit(new Rect(inRect.x, y, inRect.width, 22f), head, UiKit.HeaderColor);
            Widgets.DrawLineHorizontal(inRect.x, y + 22f, inRect.width, UiKit.DividerColor);
            y += 25f;
            foreach (var e in rows)
            {
                var row = new Rect(inRect.x + 10f, y, 220f, RowH);
                if (Widgets.RadioButtonLabeled(row, e.label, purpose == e.p && scope == e.scope, e.why != null) && e.why == null)
                {
                    if (purpose != e.p || scope != e.scope) topicIndex = 0;
                    purpose = e.p;
                    scope = e.scope;
                    dipAsk = null;
                }
                var side = new Rect(row.xMax + 12f, y, inRect.width - row.width - 32f, RowH);
                if (e.why != null)
                {
                    UiKit.LabelFit(side, e.why, UiKit.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(side, e.why);
                }
                else if (!e.note.NullOrEmpty()) UiKit.LabelFit(side, e.note, e.way == LetterWay.PassOn ? UiKit.Info : UiKit.Done, GameFont.Tiny);
                y += RowH + 1f;
            }
            return y + 4f;
        }

        /// <summary>Someone else of the town (or the head or leader the header names) to write to instead: the purpose kept if it can go to them.</summary>
        private void Pick(Notable n)
        {
            if (n == null) return;
            to = n;
            topicIndex = 0;
            dipAsk = null;
            lookedAt = -1f;
            if (LetterRoutes.WhyNot(n, purpose, scope, true) != null) FirstOpen(n);
        }

        /// <summary>"Ana (Social 8): Mayor Bell is warm toward her".</summary>
        private string WriterLabel(Pawn p)
        {
            int social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            int of = NotableBook.OpinionOf(answerer ?? to, p);
            return "CC_LetterDlg_Writer".Translate(p.LabelShort.Named("PAWN"), social.Named("SOCIAL"), ("CC_Notable_RegardOf_" + NotableMath.Level(of)).Translate().Named("REGARD")).Resolve();
        }
    }
}
