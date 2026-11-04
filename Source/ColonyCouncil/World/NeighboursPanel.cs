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
    /// The region window's Neighbours page, its "Neighbours": the towns within reach, nearest first, each with the people of it the colony knows
    /// by name (head, heir, envoy, trader, and the faces it met who live there), how each regards the colony in words,
    /// which colonists they know, the last thing between them or the letter on its way, and a button to write. The same
    /// rows, shorter, are the region map's section for a town (<see cref="NeighbourPanel"/>), registered at startup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NeighboursPanel
    {
        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);
        private const float RowH = 60f;
        private const float SmallRowH = 46f;
        private static readonly HashSet<int> open = new HashSet<int>();
        private static readonly HashSet<int> shut = new HashSet<int>();

        static NeighboursPanel()
        {
            NeighbourPanel.Sections.Add(DrawTown);
        }

        private class Row
        {
            public Notable n;
            public string name, title, regard, knows, last, status, why, tip;
            public Color regardColor;
        }

        private class Town
        {
            public Settlement s;
            public string name, distance, standing;
            public Color standingColor;
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
            foreach (var s in book.Towns(map))
            {
                var t = new Town { s = s, name = s.Name, distance = DistanceWords(map, s) };
                var f = s.Faction;
                bool hostile = f.HostileTo(Faction.OfPlayer);
                t.standing = hostile ? "CC_Neighbours_AtWar".Translate().Resolve() : f.HasGoodwill && f.PlayerGoodwill >= 75 ? "CC_Neighbours_Allies".Translate().Resolve()
                    : "CC_Neighbours_AtPeace".Translate().Resolve();
                t.standingColor = hostile ? UiKit.Bad : f.HasGoodwill && f.PlayerGoodwill >= 75 ? UiKit.Good : UiKit.Faint;
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
                n = n, name = n.Name, title = NotableBook.TitleOf(n).CapitalizeFirst(), regard = NotableBook.RegardWords(op), regardColor = NotableBook.RegardColor(op)
            };
            var known = NotableBook.Known(n).Take(3).ToList();
            if (known.Count > 0)
                r.knows = "CC_Neighbours_Knows".Translate(TextUtil.JoinAnd(known.Select(k => k.why.NullOrEmpty() ? k.pawn.LabelShort : "CC_Neighbours_KnowsFor".Translate(k.pawn.LabelShort, k.why).Resolve()).ToList())).Resolve();
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
                string why = NeighbourLetters.WhyNot(n, p);
                if (why == null) return null;
                first ??= why;
            }
            return first;
        }

        /// <summary>Where the letter is, in words (never a countdown).</summary>
        public static string StatusWords(NeighbourLetter l, Notable n)
        {
            string key = "CC_Letter_Status_" + l.stage;
            return key.CanTranslate() ? key.Translate(l.WriterName.Named("PAWN"), n.Name.Named("NAME")).Resolve() : null;
        }

        /// <summary>"about a day's walk", "about four days by courier": how far, by the colony's messengers' means.</summary>
        private static string DistanceWords(Map map, Settlement s)
        {
            float tiles = LivingEvents.Distance(map.Tile, s.Tile);
            float days = NotableMath.TravelDays(tiles, (int)(s.Faction?.def?.techLevel ?? TechLevel.Industrial));
            int d = Mathf.Max(1, Mathf.RoundToInt(days));
            return d <= 1 ? "CC_Neighbours_Day".Translate().Resolve() : "CC_Neighbours_Days".Translate(d).Resolve();
        }

        // ---------------- The Neighbours page ----------------

        /// <summary>Draws the section at <paramref name="y"/> inside the Neighbours page's scroll view; returns the y below it.</summary>
        public static float Draw(float y, float w)
        {
            if (!NotableBook.On) return y;
            BuildIfStale();
            int under = NeighbourLetters.OpenCount;
            string right = under > 0 ? "CC_Neighbours_OnTheirWay".Translate(under).Resolve() : null;
            y += UiKit.SectionTitle(0f, y, w, "CC_Neighbours_Header".Translate(), Icons.Visitor, Accent, right);
            if (towns.Count == 0) return y + UiKit.EmptyState(0f, y, w, Icons.Visitor, "CC_Neighbours_None".Translate(), 60f) + 10f;
            y += 2f;
            for (int i = 0; i < towns.Count; i++)
            {
                var t = towns[i];
                bool isOpen = open.Contains(t.s.ID) || i < 2 && !shut.Contains(t.s.ID);
                y = DrawTownCard(y, w, t, isOpen, i);
            }
            return y + 8f;
        }

        private static float DrawTownCard(float y, float w, Town t, bool isOpen, int index)
        {
            // What its people told the colony lately (Outsiders/OutsiderHeard.cs), under its people.
            float heardH = isOpen ? OutsiderHeard.Height(t.s.ID) : 0f;
            float h = 30f + (isOpen ? Mathf.Max(1, t.rows.Count) * RowH + 6f + heardH : 0f);
            var card = new Rect(0f, y, w, h);
            UiKit.Card(card, UiKit.WithAlpha(t.standingColor, 0.6f), false, false);
            var head = new Rect(card.x, card.y, card.width, 28f);
            Widgets.DrawHighlightIfMouseover(head);
            float x = card.x + 10f;
            UiKit.LabelFit(new Rect(x, y + 4f, 18f, 20f), isOpen ? "-" : "+", UiKit.Faint);
            x += 16f;
            float nameW = Mathf.Min(220f, Text.CalcSize(t.name).x + 8f);
            UiKit.LabelFit(new Rect(x, y + 4f, nameW, 22f), t.name, UiKit.HeaderColor);
            x += nameW + 6f;
            x += UiKit.FactionChip(x, y + 4f, t.s.Faction, 22f, Mathf.Min(200f, w * 0.3f)) + 8f;
            UiKit.LabelFit(new Rect(x, y + 4f, card.xMax - x - 10f, 22f), t.distance + " · " + t.standing, t.standingColor, GameFont.Tiny, TextAnchor.MiddleRight);
            if (Widgets.ButtonInvisible(head))
            {
                if (isOpen)
                {
                    open.Remove(t.s.ID);
                    shut.Add(t.s.ID);
                }
                else
                {
                    open.Add(t.s.ID);
                    shut.Remove(t.s.ID);
                }
            }
            if (!isOpen) return y + h + 6f;
            float ry = y + 30f;
            if (t.rows.Count == 0)
            {
                UiKit.LabelFit(new Rect(card.x + 12f, ry, card.width - 24f, RowH - 8f), "CC_Neighbours_NobodyYet".Translate(t.name).Resolve(), UiKit.Faint, GameFont.Small);
                if (heardH > 0f) OutsiderHeard.Draw(new Rect(card.x + 12f, ry + RowH, card.width - 24f, heardH), t.s.ID);
                return y + h + 6f;
            }
            foreach (var r in t.rows)
            {
                DrawRow(new Rect(card.x + 8f, ry, card.width - 16f, RowH), r, false);
                ry += RowH;
            }
            if (heardH > 0f) OutsiderHeard.Draw(new Rect(card.x + 12f, ry + 2f, card.width - 24f, heardH), t.s.ID);
            return y + h + 6f;
        }

        private static void DrawRow(Rect r, Row row, bool small)
        {
            float size = small ? 32f : 40f;
            UiKit.Portrait(new Rect(r.x, r.y + 4f, size, size), row.n.pawn, row.regardColor);
            float x = r.x + size + 8f;
            const float btnW = 96f;
            float textW = r.xMax - x - btnW - 8f;
            UiKit.LabelFit(new Rect(x, r.y + 2f, textW, 20f), row.name + ", " + row.title, Color.white);
            string second = row.regard + (row.knows.NullOrEmpty() || small ? "" : ". " + row.knows);
            UiKit.LabelFit(new Rect(x, r.y + 20f, textW, 18f), second, row.regardColor, GameFont.Tiny);
            string third = row.status ?? (small ? row.knows : row.last);
            if (!third.NullOrEmpty()) UiKit.LabelFit(new Rect(x, r.y + 36f, textW, 18f), third, row.status != null ? UiKit.Info : UiKit.Faint, GameFont.Tiny);
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(r.x, r.y, r.width - btnW - 8f, r.height), row.tip);
            var b = new Rect(r.xMax - btnW, r.y + (small ? 8f : 12f), btnW, 26f);
            bool can = row.why == null;
            if (Widgets.ButtonText(b, "CC_Neighbours_Write".Translate(), true, true, can))
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
            if (!row.knows.NullOrEmpty()) parts.Add(row.knows);
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

        // ---------------- The region map's town panel ----------------

        /// <summary>A town's notables and a button to write, for the region map's panel (<see cref="NeighbourPanel.Sections"/>).</summary>
        public static float DrawTown(Rect rect, Settlement s)
        {
            if (!NotableBook.On || s == null) return 0f;
            try
            {
                BuildIfStale();
                var t = towns.FirstOrDefault(x => x.s == s);
                float y = rect.y;
                UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), "CC_Neighbours_TownHeader".Translate(), Accent);
                y += 24f;
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
    /// Writing to a neighbour: whom (any notable of the same town), what for (each purpose, with why it can't be when it
    /// can't), what about (the loss, the threat, the help asked for), who writes (the speaker by default) and how (a
    /// letter by hand, or a call on the comms console when both ends can), and in words how it is likely to be taken.
    /// </summary>
    public class Dialog_NeighbourLetter : Window
    {
        private Notable to;
        private LetterPurpose purpose = LetterPurpose.Greetings;
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
        private string byHand, byCall;
        private readonly Dictionary<LetterPurpose, string> whyNot = new Dictionary<LetterPurpose, string>();
        private List<LetterTopic> topics = new List<LetterTopic>();

        private void Look()
        {
            if (lookedFor == to && lookedPurpose == purpose && Time.realtimeSinceStartup - lookedAt < 0.5f) return;
            lookedAt = Time.realtimeSinceStartup;
            lookedFor = to;
            lookedPurpose = purpose;
            byHand = NeighbourLetters.ChannelWhyNot(to, LetterChannel.Courier, map);
            byCall = NeighbourLetters.ChannelWhyNot(to, LetterChannel.Console, map);
            whyNot.Clear();
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose))) whyNot[p] = NeighbourLetters.WhyNot(to, p);
            topics = NeighbourLetters.Topics(to, purpose);
        }

        public override Vector2 InitialSize => new Vector2(600f, 640f);

        public Dialog_NeighbourLetter(Notable n)
        {
            to = n;
            map = HappeningTracker.HomeMap();
            writer = NeighbourLetters.DefaultWriter(map);
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            // The first purpose that can be written, and a letter by hand when a messenger can come.
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
                if (NeighbourLetters.WhyNot(n, p) == null)
                {
                    purpose = p;
                    break;
                }
            if (NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, map) != null && NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, map) == null)
                channel = LetterChannel.Console;
        }

        /// <summary>A letter for a purpose and what it's about, chosen beforehand (the Diplomacy page's "call it in": Diplomacy/DiplomacyPanels.cs).</summary>
        public Dialog_NeighbourLetter(Notable n, LetterPurpose p, string topicKey) : this(n)
        {
            if (NeighbourLetters.WhyNot(n, p) != null) return;
            purpose = p;
            int i = NeighbourLetters.Topics(n, p).FindIndex(t => t.key == topicKey);
            topicIndex = Mathf.Max(0, i);
        }

        /// <summary>A letter for <paramref name="bearer"/>, someone of their people at the colony, to carry home; <paramref name="by"/> writes it unless the player picks someone else.</summary>
        public Dialog_NeighbourLetter(Notable n, Pawn bearer, Pawn by) : this(n)
        {
            this.bearer = bearer;
            channel = LetterChannel.Courier;
            if (by != null && NeighbourLetters.Writers(map).Contains(by)) writer = by;
        }

        public override void DoWindowContents(Rect inRect)
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

        private void Contents(Rect inRect)
        {
            if (to == null || !to.Alive)
            {
                Close();
                return;
            }
            Look();
            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "CC_LetterDlg_Title".Translate());
            Text.Font = GameFont.Small;
            y += 38f;
            // Whom.
            UiKit.Portrait(new Rect(inRect.x, y, 44f, 44f), to.pawn, NotableBook.RegardColor(NotableBook.Opinion(to)));
            var whom = new Rect(inRect.x + 52f, y, inRect.width - 52f, 24f);
            var mates = NotableBook.Instance?.notables.Where(x => x.town == to.town && x.Alive).ToList() ?? new List<Notable>();
            if (Widgets.ButtonText(new Rect(whom.x, whom.y, Mathf.Min(whom.width, 380f), 24f), NotableBook.Called(to), true, true, mates.Count > 1))
                Find.WindowStack.Add(new FloatMenu(mates.Select(m => new FloatMenuOption(NotableBook.Called(m), () => Pick(m))).ToList()));
            int op = NotableBook.Opinion(to);
            UiKit.LabelFit(new Rect(whom.x, y + 26f, whom.width, 18f), NotableBook.RegardWords(op).CapitalizeFirst(), NotableBook.RegardColor(op), GameFont.Tiny);
            y += 52f;
            // What for.
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "CC_LetterDlg_WhatFor".Translate());
            y += 24f;
            foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            {
                string why = whyNot.TryGetValue(p, out var w) ? w : null;
                var row = new Rect(inRect.x + 10f, y, 220f, 24f);
                if (Widgets.RadioButtonLabeled(row, ("CC_Letter_PurposeCap_" + p).Translate(), purpose == p, why != null) && why == null)
                {
                    purpose = p;
                    topicIndex = 0;
                }
                if (why != null) UiKit.LabelFit(new Rect(row.xMax + 12f, y, inRect.width - row.width - 32f, 24f), why, UiKit.Faint, GameFont.Tiny);
                y += 26f;
            }
            Look();
            string purposeWhy = whyNot.TryGetValue(purpose, out var pw) ? pw : null;
            // What about.
            if ((purpose == LetterPurpose.Condolences || purpose == LetterPurpose.Warning) && topics.Count > 0)
            {
                topicIndex = Mathf.Clamp(topicIndex, 0, topics.Count - 1);
                Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_About".Translate());
                if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topics[topicIndex].text, true, true, topics.Count > 1))
                    Find.WindowStack.Add(new FloatMenu(topics.Select((t, i) => new FloatMenuOption(t.text, () => topicIndex = i)).ToList()));
                y += 30f;
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
            var writers = NeighbourLetters.Writers(map);
            if (writer == null || !writers.Contains(writer)) writer = NeighbourLetters.DefaultWriter(map);
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_Who".Translate());
            if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, 260f, 26f), writer != null ? WriterLabel(writer) : "-", true, true, writers.Count > 0))
                Find.WindowStack.Add(new FloatMenu(writers.Select(p => new FloatMenuOption(WriterLabel(p), () => writer = p)).ToList()));
            y += 30f;
            // How.
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_How".Translate());
            if (bearer != null)
            {
                Widgets.Label(new Rect(inRect.x + 124f, y + 2f, inRect.width - 124f, 24f), "CC_LetterDlg_ByBearer".Translate(bearer.Named("BEARER")).Resolve());
            }
            else
            {
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 124f, y, 200f, 24f), "CC_LetterDlg_ByHand".Translate(), channel == LetterChannel.Courier, byHand != null) && byHand == null)
                    channel = LetterChannel.Courier;
                if (byHand != null) TooltipHandler.TipRegion(new Rect(inRect.x + 124f, y, 200f, 24f), byHand);
                if (Widgets.RadioButtonLabeled(new Rect(inRect.x + 340f, y, 200f, 24f), "CC_LetterDlg_ByCall".Translate(), channel == LetterChannel.Console, byCall != null) && byCall == null)
                    channel = LetterChannel.Console;
                if (byCall != null) TooltipHandler.TipRegion(new Rect(inRect.x + 340f, y, 200f, 24f), byCall);
            }
            y += 26f;
            string channelWhy = bearer != null ? (bearer.Spawned && Outsiders.IsOutsider(bearer) ? null : "CC_LetterDlg_BearerGone".Translate(bearer.Named("BEARER")).Resolve())
                : channel == LetterChannel.Courier ? byHand : byCall;
            string how = channelWhy ?? (bearer != null ? "CC_LetterDlg_BearerNote".Translate(bearer.Named("BEARER"), (to.faction?.Name ?? "").Named("FACTION")).Resolve()
                : channel == LetterChannel.Courier
                ? (NotableBook.Hostile(to) ? "CC_LetterDlg_WhiteFlag" : "CC_LetterDlg_HandNote").Translate(to.faction?.Name ?? "").Resolve()
                : "CC_LetterDlg_CallNote".Translate().Resolve());
            float hh = Text.CalcHeight(how, inRect.width - 124f);
            GUI.color = channelWhy != null ? UiKit.Warn : UiKit.Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x + 124f, y, inRect.width - 124f, hh + 4f), how);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            y += hh + 10f;
            // How it may be taken.
            if (writer != null && purposeWhy == null)
            {
                string outlook = NeighbourLetters.Outlook(to, writer, purpose, channel);
                float oh = Text.CalcHeight(outlook, inRect.width);
                GUI.color = UiKit.Quote;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, oh), outlook);
                GUI.color = Color.white;
                y += oh + 6f;
            }
            // Send.
            bool ok = writer != null && purposeWhy == null && channelWhy == null && dipWhy == null;
            var send = new Rect(inRect.x, inRect.yMax - 34f, 200f, 32f);
            if (Widgets.ButtonText(send, (channel == LetterChannel.Console ? "CC_LetterDlg_Call" : "CC_LetterDlg_Send").Translate(), true, true, ok) && ok)
            {
                bool about = purpose == LetterPurpose.Condolences || purpose == LetterPurpose.Warning || DiplomacyLetters.Owns(purpose);
                LetterTopic? topic = topics.Count > 0 && about ? topics[Mathf.Clamp(topicIndex, 0, topics.Count - 1)] : (LetterTopic?)null;
                string asked = purpose == LetterPurpose.Help ? help : DiplomacyLetters.Owns(purpose) ? dipAsk : null;
                if (NeighbourLetters.Send(to, purpose, channel, writer, topic, asked, bearer) != null) Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 160f, inRect.yMax - 34f, 160f, 32f), "CC_LetterDlg_Cancel".Translate())) Close();
        }

        private void Pick(Notable n)
        {
            to = n;
            topicIndex = 0;
            if (NeighbourLetters.WhyNot(n, purpose) != null)
                foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
                    if (NeighbourLetters.WhyNot(n, p) == null)
                    {
                        purpose = p;
                        break;
                    }
        }

        /// <summary>"Ana (Social 8): Mayor Bell is warm toward her".</summary>
        private string WriterLabel(Pawn p)
        {
            int social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            int of = NotableBook.OpinionOf(to, p);
            return "CC_LetterDlg_Writer".Translate(p.LabelShort.Named("PAWN"), social.Named("SOCIAL"), ("CC_Notable_RegardOf_" + NotableMath.Level(of)).Translate().Named("REGARD")).Resolve();
        }
    }
}
