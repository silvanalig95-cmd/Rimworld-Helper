using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Region → Gods' book: the colony's own book (Gods/GodLore.cs), as a holy book (the player, 12 November 2026: "more like
    /// a wiki page basically building each up based on gained knowledge, only a more mysterious and ominous wiki, you know
    /// like a religious text-style wiki"). Text only, in either of Rimfolk's looks: on the left the chapters (the names the
    /// colony has heard gods by, numbered as they were first heard), "the whole book" and the things nobody could name; on
    /// the right a name's chapter: its number in small capitals, the name set large, the other names it was worked out to
    /// be, when and from whom it was first heard, an ornament, then its parts (GodBookMath.Compose): of its names, of what it
    /// is, of how it is known, of what it loves and what it hates, the sayings, the tales, of its kin and its enemies, its
    /// feast and its rites; each verse numbered, the first opening with a drop capital, each said as someone's; disputes
    /// side by side; a tale's untold pieces left as gaps; a part with nothing in it as silence, two or more as a torn page.
    /// The margin keeps each verse's date, a change of the keeper's hand, and the keeper's own notes; the witnesses (every
    /// entry by date) come last. Nothing in it is more than the colonists saw, were told and believe: no favour, no rules, no
    /// truth marked. A god the colony has never heard of isn't in it. What it shows is laid out at most once a second, or
    /// when the book changes.
    /// </summary>
    public static class GodLorePage
    {
        /// <summary>The page chosen: a name, or one of these.</summary>
        private const string Whole = "\u0001whole", Unnamed = "\u0001unnamed";

        private static string chosen = Whole;
        private static Vector2 listScroll, pageScroll;
        private static float listHeight = 300f, pageHeight = 600f;
        private static ViewHeadModel head;

        private sealed class Page
        {
            public string title;
            public int chapter;
            public List<string> also = new List<string>();
            public LoreName name;
            public List<LoreEntry> entries = new List<LoreEntry>();
            public List<BookVerse> verses = new List<BookVerse>();
            public List<BookLine> lines;
            /// <summary>The god the chapter holds itself to be about, from what was told under its name (GodSignsMath.Believed), or -1.</summary>
            public int believed = -1;
        }

        private static readonly List<Page> pages = new List<Page>();
        private static readonly List<LoreEntry> unnamed = new List<LoreEntry>();
        private static float builtAt = -10f;
        private static int builtVersion = -1;

        private static Dictionary<string, int> pieces;

        /// <summary>How many pieces each tale has (a tale's length shows only once one of its pieces is heard).</summary>
        private static Dictionary<string, int> Pieces
        {
            get
            {
                if (pieces != null) return pieces;
                pieces = new Dictionary<string, int>();
                foreach (var d in DefDatabase<GodDef>.AllDefsListForReading)
                    foreach (var s in d.stories ?? new List<GodStory>())
                        if (s != null && !s.key.NullOrEmpty())
                            pieces[s.key] = s.pieces?.Count ?? 0;
                return pieces;
            }
        }

        private static void Refresh(GodLore lore)
        {
            if (Time.realtimeSinceStartup - builtAt < 1f && builtAt <= Time.realtimeSinceStartup && builtVersion == GodLore.Version) return;
            lore.Migrate();
            builtAt = Time.realtimeSinceStartup;
            builtVersion = GodLore.Version;
            pages.Clear();
            unnamed.Clear();
            splits.Clear();
            var byTitle = new Dictionary<string, Page>();
            foreach (var n in lore.names.OrderBy(x => x.first))
            {
                var root = lore.Root(n);
                if (root == null) continue;
                if (!byTitle.TryGetValue(root.name, out var page))
                {
                    page = new Page { title = root.name, name = root, chapter = pages.Count + 1 };
                    byTitle[root.name] = page;
                    pages.Add(page);
                }
                if (n != root && !page.also.Contains(n.name)) page.also.Add(n.name);
            }
            foreach (var e in lore.entries)
            {
                if (e.page == null)
                {
                    unnamed.Add(e);
                    continue;
                }
                var root = lore.Root(lore.Named(e.page));
                if (root != null && byTitle.TryGetValue(root.name, out var page)) page.entries.Add(e);
            }
            foreach (var v in lore.verses)
            {
                var root = lore.Root(lore.Named(v.page));
                if (root != null && byTitle.TryGetValue(root.name, out var page)) page.verses.Add(v);
            }
            foreach (var p in pages)
            {
                // A name the book has but no verse of its names (heard in a tale, or kept before the verses): as first heard.
                foreach (var n in lore.names.Where(x => lore.Root(x) == p.name))
                    if (!p.verses.Any(v => v.part == BookPart.Names && v.key == n.name))
                        p.verses.Add(new BookVerse { part = BookPart.Names, god = n.god, page = p.title, key = n.name, text = n.name, how = "First", about = n.from, tick = n.first });
                p.lines = GodBookMath.Compose(p.verses, Pieces);
                p.believed = GodSignsMath.Believed(p.verses);
            }
            if (chosen != Whole && chosen != Unnamed && !byTitle.ContainsKey(chosen)) chosen = Whole;
        }

        /// <summary>Opens the book at a name the colony knows a god by (another page, a dev action).</summary>
        public static void Show(string name)
        {
            chosen = name ?? Whole;
            pageScroll = Vector2.zero;
        }

        // ================= The view =================

        public static void Draw(Rect rect)
        {
            var lore = GodLore.Instance;
            var keeper = GodLore.Keeper(out _);
            string line = keeper != null ? "CC_GodLore_KeptBy".Translate(keeper.LabelShort.Named("KEEPER")).Resolve() : "CC_GodLore_KeptByNobody".Translate().Resolve();
            if (lore != null) Refresh(lore);
            string stat = pages.Count > 0 ? "CC_GodLore_Stat".Translate(pages.Count.Named("COUNT")).Resolve() : null;
            float top = UiKit.ViewHeader(rect, Icons.Journal ?? Icons.Chronicle, "CC_Tab_Gods".Translate(), line, stat, null, 0f, null,
                head ??= new ViewHeadModel { article = PagesMath.ArticleOf(RegionView.Gods) });
            var body = new Rect(rect.x, rect.y + top + 4f, rect.width, rect.height - top - 4f);
            if (!Gods.On || lore == null)
            {
                UiKit.EmptyState(body, Icons.Journal ?? Icons.Chronicle, "CC_GodLore_Off".Translate());
                return;
            }
            if (lore.entries.Count == 0 && pages.Count == 0)
            {
                UiKit.EmptyState(body, Icons.Journal ?? Icons.Chronicle, "CC_GodLore_Empty".Translate());
                return;
            }
            try
            {
                float lw = Mathf.Clamp(body.width * 0.24f, 170f, 250f);
                List(new Rect(body.x, body.y, lw, body.height));
                var page = new Rect(body.x + lw + 14f, body.y, body.width - lw - 14f, body.height);
                Widgets.DrawBoxSolid(new Rect(page.x - 7.5f, page.y, 1f, page.height), UiKit.DividerColor);
                PageView(page);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ================= The chapters =================

        private static void List(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, listHeight);
            Widgets.BeginScrollView(rect, ref listScroll, view);
            try
            {
                float y = 0f, w = view.width;
                y = Row(y, w, Whole, "CC_GodLore_Whole".Translate(), null, null, 0);
                y += 6f;
                for (int i = 0; i < pages.Count; i++)
                {
                    var p = pages[i];
                    string sub = p.also.Count > 0 ? "CC_GodLore_AlsoShort".Translate(p.also.ToCommaList(true).Named("NAMES")).Resolve() : null;
                    y = Row(y, w, p.title, p.title.CapitalizeFirst(), sub, GodBookMath.Roman(p.chapter), i, SignOf(p, false));
                }
                if (unnamed.Count > 0)
                {
                    y += 6f;
                    y = Row(y, w, Unnamed, "CC_GodLore_Unnamed".Translate(), null, null, 0);
                }
                listHeight = Mathf.Max(y + 6f, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        private static float Row(float y, float w, string key, string label, string sub, string number, int index, Texture2D sign = null)
        {
            float h = sub.NullOrEmpty() ? 28f : 40f;
            var r = new Rect(0f, y, w, h);
            bool on = chosen == key;
            if (on) UiKit.RowLit(r, true);
            else
            {
                UiKit.Zebra(r, index);
                UiKit.RowHover(r);
            }
            float nx = 0f;
            if (!number.NullOrEmpty())
            {
                // The chapter's number, small and quiet, before its name.
                nx = 30f;
                UiKit.LabelFit(new Rect(r.x + 6f, r.y + 4f, nx - 6f, 20f), number, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            }
            // The colony's sign of the god, small, at the row's end, where the chapter shows one.
            float sx = 0f;
            if (sign != null)
            {
                sx = 24f;
                GodSigns.Draw(new Rect(r.xMax - 24f, r.y + (h - 20f) / 2f, 20f, 20f), sign, on ? 1f : 0.8f);
            }
            UiKit.LabelFit(new Rect(r.x + 8f + nx, r.y + 4f, r.width - 12f - nx - sx, 20f), label, on ? UiKit.Gold : UiKit.Ink);
            if (!sub.NullOrEmpty()) UiKit.LabelFit(new Rect(r.x + 8f + nx, r.y + 22f, r.width - 12f - nx - sx, 16f), sub, UiKit.Faint, GameFont.Tiny);
            if (Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                chosen = key;
                pageScroll = Vector2.zero;
            }
            return y + h + 2f;
        }

        // ================= A page =================

        /// <summary>The widest a column of the book's text runs, for reading.</summary>
        public const float Column = 600f;

        /// <summary>The margin beside the column for dates, hands and the keeper's notes, when the page is wide enough.</summary>
        private const float Margin = 150f, Gutter = 26f;

        private static void PageView(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, pageHeight);
            Widgets.BeginScrollView(rect, ref pageScroll, view);
            try
            {
                bool wide = view.width >= Column + Margin + 72f;
                float cw = wide ? Column : Mathf.Min(Column, view.width - 40f);
                float x = wide ? Mathf.Max(20f, (view.width - cw - Margin - 16f) / 2f) : (view.width - cw) / 2f;
                float mx = x + cw + 16f, mw = wide ? Margin : 0f;
                // The page itself: a leaf the column and its margin sit on.
                UiKit.Card(new Rect(x - 18f, 4f, cw + 36f + (wide ? Margin + 16f : 0f), pageHeight - 8f), null, false, false);
                float y = 22f;
                if (chosen == Whole) y = WholeBook(x, y, cw);
                else if (chosen == Unnamed) y = Entries(x, Title(x, y, cw, "CC_GodLore_Unnamed".Translate(), null, "CC_GodLore_UnnamedLine".Translate()), cw, unnamed, false);
                else
                {
                    var p = pages.FirstOrDefault(q => q.title == chosen);
                    if (p != null) y = Chapter(x, y, cw, mx, mw, p);
                }
                pageHeight = Mathf.Max(y + 30f, rect.height);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The page's head: a title set large (with the colony's sign beside it, where it has one), an italic line under it, and a small italic note; then an ornament.</summary>
        private static float Title(float x, float y, float w, string title, string italic, string note, Texture2D sign = null)
        {
            string big = "<size=26>" + title.CapitalizeFirst() + "</size>";
            if (sign != null)
            {
                // The sign beside the name, the two set together in the middle of the column.
                Text.Font = GameFont.Medium;
                float tw = Mathf.Min(Text.CalcSize(big).x, w - SignSize - 12f), th = Mathf.Max(Text.CalcHeight(big, tw), SignSize);
                Text.Font = GameFont.Small;
                float x0 = x + (w - SignSize - 12f - tw) / 2f;
                GodSigns.Draw(new Rect(x0, y + (th - SignSize) / 2f, SignSize, SignSize), sign);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = UiKit.Ink;
                Widgets.Label(new Rect(x0 + SignSize + 12f, y, tw + 4f, th), big);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                y += th + 4f;
            }
            else y += Para(x, y, w, big, UiKit.Ink, GameFont.Medium, TextAnchor.UpperCenter) + 2f;
            if (!italic.NullOrEmpty()) y += Para(x, y, w, "<i>" + italic + "</i>", UiKit.Faint, GameFont.Small, TextAnchor.UpperCenter) + 2f;
            if (!note.NullOrEmpty()) y += Para(x, y, w, "<i>" + note + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter);
            return Ornament(x, y + 12f, w) + 14f;
        }

        // ================= A chapter =================

        private static float Chapter(float x, float y, float w, float mx, float mw, Page p)
        {
            y += Para(x, y, w, SmallCaps("CC_GodBook_Chapter".Translate(GodBookMath.Roman(p.chapter).Named("NUM")).Resolve(), 13, 10), UiKit.Faint, GameFont.Small,
                TextAnchor.UpperCenter) + 2f;
            string also = p.also.Count > 0 ? "CC_GodLore_Also".Translate(p.also.ToCommaList(true).Named("NAMES")).Resolve() : null;
            string first = p.name != null
                ? "CC_GodLore_FirstHeard".Translate(Date(p.name.first).Named("DATE"), (p.name.from ?? "CC_GodLore_FromNowhere".Translate().ToString()).Named("FROM")).Resolve()
                : null;
            y = Title(x, y, w, p.title, also, first, SignOf(p, true));
            y = PlainLine(x, y, w, p);
            marginBottom = y;
            GodLore.Keeper(out var voice);
            foreach (var l in p.lines ?? new List<BookLine>())
            {
                try
                {
                    y = Line(x, y, w, mx, mw, p, l, voice);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a line of the book", e);
                }
            }
            y = Holders(x, y, w, p);
            y = SaintsPart(x, y, w, p);
            // The witnesses: every entry under the name, by date.
            y = PartHead(x, y, w, "CC_GodBook_Part_Witnesses".Translate(), UiKit.Quieter(UiKit.Gold, 0.35f));
            if (p.entries.Count == 0) return y + Para(x, y, w, "<i>" + "CC_GodBook_Silence_0".Translate() + "</i>", UiKit.Dim, GameFont.Small, TextAnchor.UpperCenter) + 8f;
            return Entries(x, y, w, p.entries, false);
        }

        /// <summary>
        /// Who holds to the god here, and who dreads it, in the keeper's hand (Gods/Belief/PawnGods.cs): the colony's people by
        /// name with how they hold it in a word, under the chapter the colony keeps that god under (its first name for it; a
        /// second name not yet worked out to be the same has nobody). Nothing while nobody does.
        /// </summary>
        private static float Holders(float x, float y, float w, Page p)
        {
            var g = p?.name != null && p.name.god >= 0 ? Gods.ById(p.name.god) : null;
            if (g == null || GodLore.Instance?.PageOf(g)?.name != p.title) return y;
            // Worked out at most once a second.
            float now = Time.realtimeSinceStartup;
            if (!holdersCache.TryGetValue(g.id, out var c) || now - c.at > 1f || now < c.at)
            {
                var holders = PawnGods.HoldersOf(g);
                string Of(IEnumerable<(Pawn pawn, BeliefWord word)> list) =>
                    string.Join("; ", list.Select(h => "CC_GodBook_Holder".Translate(h.pawn.LabelShort.Named("PAWN"), ("CC_Belief_Short_" + h.word).Translate().Named("WORD")).Resolve()));
                var hold = holders.Where(h => !PawnGodsMath.Fears(h.word)).ToList();
                var dread = holders.Where(h => PawnGodsMath.Fears(h.word)).ToList();
                var keeper = GodLore.Keeper(out _);
                c = (now, holders.Count == 0 ? null : keeper != null ? "CC_GodBook_HoldersHand".Translate(keeper.LabelShort.Named("KEEPER")).Resolve() : "CC_GodBook_HoldersNobody".Translate().Resolve(),
                    hold.Count > 0 ? "CC_GodBook_HoldTo".Translate(Of(hold).Named("WHO")).Resolve() : null,
                    dread.Count > 0 ? "CC_GodBook_DreadIt".Translate(Of(dread).Named("WHO")).Resolve() : null);
                holdersCache[g.id] = c;
            }
            if (c.head == null) return y;
            y = PartHead(x, y, w, "CC_GodBook_Part_Holders".Translate(), UiKit.Quieter(UiKit.Gold, 0.35f));
            y += Para(x + Gutter, y, w - Gutter, "<i>" + c.head + "</i>", UiKit.Quieter(UiKit.Gold, 0.3f), GameFont.Tiny) + 2f;
            if (c.hold != null) y += Para(x + Gutter, y, w - Gutter, c.hold, UiKit.Ink, GameFont.Small) + 4f;
            if (c.dread != null) y += Para(x + Gutter, y, w - Gutter, c.dread, UiKit.Ink, GameFont.Small) + 4f;
            return y + 8f;
        }

        /// <summary>
        /// The saints held to be the god's, in the keeper's hand (Gods/Saints/Saints.cs, BookLines): the colony's own, its faiths'
        /// saints and damned, those kept no more; under the chapter the colony keeps that god under only. Nothing while none.
        /// </summary>
        private static float SaintsPart(float x, float y, float w, Page p)
        {
            var g = p?.name != null && p.name.god >= 0 ? Gods.ById(p.name.god) : null;
            if (g == null || GodLore.Instance?.PageOf(g)?.name != p.title) return y;
            var lines = Saints.BookLines(g);
            if (lines.Count == 0) return y;
            var keeper = GodLore.Keeper(out _);
            y = PartHead(x, y, w, "CC_Saint_Book_Part".Translate(), UiKit.Quieter(UiKit.Gold, 0.35f));
            string head = keeper != null ? "CC_Saint_Book_Hand".Translate(keeper.LabelShort.Named("KEEPER")).Resolve() : "CC_Saint_Book_Nobody".Translate().Resolve();
            y += Para(x + Gutter, y, w - Gutter, "<i>" + head + "</i>", UiKit.Quieter(UiKit.Gold, 0.3f), GameFont.Tiny) + 2f;
            foreach (var l in lines) y += Para(x + Gutter, y, w - Gutter, l, UiKit.Ink, GameFont.Small) + 4f;
            return y + 8f;
        }

        private static readonly Dictionary<int, (float at, string head, string hold, string dread)> holdersCache = new Dictionary<int, (float, string, string, string)>();

        /// <summary>How large a chapter's sign is drawn beside its name.</summary>
        private const float SignSize = 56f;

        /// <summary>
        /// The god a chapter holds itself to be about (its believed god, from what was told under its name), with a sign of
        /// Rimfolk's own; null while nobody has told what it is under that name, nor seen it plainly.
        /// </summary>
        private static God BelievedGod(Page p)
        {
            var g = p?.believed >= 0 ? Gods.ById(p.believed) : null;
            return GodSigns.KeyOf(g) != null ? g : null;
        }

        /// <summary>The chapter's sign: in the colony's age's form beside its name, plain in the list; none while nobody has told what it is or seen it.</summary>
        private static Texture2D SignOf(Page p, bool ofAge)
        {
            string key = GodSigns.KeyOf(BelievedGod(p));
            return key == null ? null : ofAge ? GodSigns.OfColony(key) : GodSigns.Plain(key);
        }

        /// <summary>
        /// The chapter's plain line, under its head, in the keeper's hand: what the colony takes the god to be (its believed
        /// god's own plain words), as held, never as a truth; nothing while nobody has told what it is under that name, nor
        /// seen it.
        /// </summary>
        private static float PlainLine(float x, float y, float w, Page p)
        {
            string plain = BelievedGod(p)?.def?.plain;
            if (plain.NullOrEmpty()) return y;
            var keeper = GodLore.Keeper(out _);
            string head = keeper != null ? "CC_GodBook_Plain".Translate(keeper.LabelShort.Named("KEEPER")).Resolve() : "CC_GodBook_PlainNobody".Translate().Resolve();
            float iw = Mathf.Min(w, w * 0.86f + 40f), ix = x + (w - iw) / 2f;
            y += Para(ix, y, iw, "<i>" + head + "</i>", UiKit.Quieter(UiKit.Gold, 0.3f), GameFont.Tiny, TextAnchor.UpperCenter) + 2f;
            y += Para(ix, y, iw, plain, UiKit.Ink, GameFont.Small, TextAnchor.UpperCenter);
            return Rule(x + w * 0.3f, y + 10f, w * 0.4f) + 8f;
        }

        private static float Line(float x, float y, float w, float mx, float mw, Page p, BookLine l, GodLoreMath.Voice voice)
        {
            float tx = x + Gutter, tw = w - Gutter;
            var v = l.verse;
            switch (l.kind)
            {
                case BookLineKind.Head:
                    return PartHead(x, y, w, Part(l.part), UiKit.Quieter(UiKit.Gold, 0.35f));
                case BookLineKind.Silence:
                {
                    y = PartHead(x, y, w, Part(l.part), UiKit.Dim);
                    return y + Para(x, y, w, "<i>" + Words(l, p) + "</i>", UiKit.Dim, GameFont.Small, TextAnchor.UpperCenter) + 6f;
                }
                case BookLineKind.Torn:
                {
                    y += 14f;
                    y = Tear(x, y, w, (int)l.part + 3) + 8f;
                    y += Para(x + 20f, y, w - 40f, "<i>" + Words(l, p) + "</i>", UiKit.Dim, GameFont.Small, TextAnchor.UpperCenter) + 8f;
                    return Tear(x, y, w, (int)l.part + 11) + 8f;
                }
                case BookLineKind.TaleHead:
                    y += 6f;
                    return y + Para(x, y, w, SmallCaps(Words(l, p), 12, 10), UiKit.Quieter(UiKit.Ink, 0.25f), GameFont.Small, TextAnchor.UpperCenter) + 6f;
                case BookLineKind.Gap:
                {
                    // One row of faint dots for each piece not yet heard, and the telling broken off.
                    for (int i = 0; i < Mathf.Clamp(l.gaps, 1, 4); i++)
                    {
                        Dots(tx, y + 7f, tw);
                        y += 14f;
                    }
                    return y + Para(tx, y, tw, "<i>" + Words(l, p) + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter) + 8f;
                }
                case BookLineKind.Note:
                {
                    string note = Note(l.note, voice);
                    if (note.NullOrEmpty()) return y;
                    if (mw > 0f)
                    {
                        // The keeper's note in the margin, beside the end of the part it is about, below the margin's last note;
                        // the column goes on below it, so the next verse's margin stays clear.
                        float top = Mathf.Max(y - 14f, marginBottom + 6f);
                        float nh = Para(mx, top, mw, "<i>" + note + "</i>", UiKit.Quieter(UiKit.Gold, 0.3f), GameFont.Tiny);
                        marginBottom = top + nh;
                        return Mathf.Max(y, marginBottom - 6f);
                    }
                    return y + Para(tx, y, tw, "<i>" + note + "</i>", UiKit.Quieter(UiKit.Gold, 0.3f), GameFont.Tiny, TextAnchor.UpperRight) + 6f;
                }
                case BookLineKind.Doubt:
                {
                    Number(x, y, l.number);
                    float h = Para(tx + 14f, y, tw - 14f, "<i>" + Words(l, p) + "</i>", UiKit.Faint, GameFont.Small);
                    return y + Mathf.Max(h, Margins(mx, mw, y, l, v)) + 8f;
                }
                case BookLineKind.Retold:
                {
                    Number(x, y, l.number);
                    float h = Para(tx, y, tw, "<i>" + GodBookWords.Retold(v, WordsFor(p.title)) + "</i>", UiKit.Faint, GameFont.Tiny) + 2f;
                    float body = Para(tx + 12f, y + h, tw - 12f, "<i>" + Words(l, p) + "</i>", UiKit.Ink, GameFont.Small);
                    Widgets.DrawBoxSolid(new Rect(tx + 3f, y + h + 2f, 1f, body - 4f), UiKit.Quieter(UiKit.Gold, 0.35f));
                    return y + Mathf.Max(h + body, Margins(mx, mw, y, l, v)) + 10f;
                }
                case BookLineKind.Verse:
                {
                    if (l.hand && mw <= 0f) y += Para(tx, y, tw, "<i>" + Hand(v) + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter) + 2f;
                    Number(x, y, l.number);
                    string s = Words(l, p);
                    float h = l.first ? DropCap(tx, y, tw, s, UiKit.Ink) : Para(tx, y, tw, s, UiKit.Ink, GameFont.Small);
                    if (l.part == BookPart.Tales && mw <= 0f) h += Para(tx, y + h, tw, "<i>" + ToldBy(v) + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperRight);
                    // Kin: the other's chapter, a step away.
                    if (l.part == BookPart.Kin && Other(v) is string other && other != p.title && pages.Any(q => q.title == other))
                        h += 2f + Link(tx, y + h + 2f, tw, "CC_GodBook_SeeOther".Translate(other.Named("OTHER")).Resolve(), other);
                    return y + Mathf.Max(h, Margins(mx, mw, y, l, v)) + 8f;
                }
            }
            return y;
        }

        /// <summary>The margin beside a verse, when the page is wide: who told a tale's piece, its date, and a new hand.</summary>
        private static float Margins(float mx, float mw, float y, BookLine l, BookVerse v)
        {
            if (mw <= 0f || v == null) return 0f;
            var notes = new List<string>();
            if (l.part == BookPart.Tales && l.kind == BookLineKind.Verse) notes.Add(ToldBy(v));
            if (v.tick > 0) notes.Add(Date(v.tick));
            if (l.hand) notes.Add(Hand(v));
            if (notes.Count == 0) return 0f;
            float h = Para(mx, y + 2f, mw, "<i>" + string.Join("\n", notes) + "</i>", UiKit.Dim, GameFont.Tiny);
            marginBottom = y + 2f + h;
            return h;
        }

        /// <summary>Where the margin's last note ends, so a keeper's note goes below it.</summary>
        private static float marginBottom;

        // ================= The words of a line (chosen in GodBookWords) =================

        private static BookWordsContext words;

        /// <summary>What the words need from the game, for a chapter: keys in the player's language, the book's names for the other gods, a tale's title.</summary>
        private static BookWordsContext WordsFor(string chapter)
        {
            words ??= new BookWordsContext
            {
                T = (key, args) => key.Translate(args.Select(a => a.value.Named(a.name)).ToArray()).Resolve(),
                OtherName = id => GodLore.Instance?.PageOf(Gods.ById(id))?.name,
                TaleTitle = TaleTitle,
                The = The
            };
            words.Chapter = chapter;
            return words;
        }

        private static string Part(BookPart part) => GodBookWords.Part(part, WordsFor(null));

        private static string Words(BookLine l, Page p) => GodBookWords.Line(l, WordsFor(p.title));

        private static string ToldBy(BookVerse v) => GodBookWords.ToldBy(v, WordsFor(null));

        private static string Hand(BookVerse v) => GodBookWords.Hand(v, WordsFor(null));

        private static string Note(string note, GodLoreMath.Voice voice) => GodBookWords.Note(note, voice.ToString(), WordsFor(null));

        /// <summary>An event in words after "after": "the crop blight".</summary>
        private static string The(string what)
        {
            if (what.NullOrEmpty()) return what;
            string w = what.UncapitalizeFirst();
            return w.StartsWith("a ") || w.StartsWith("an ") || w.StartsWith("the ") ? w : Find.ActiveLanguageWorker.WithDefiniteArticle(w);
        }

        /// <summary>The other god's name in the colony's book, for a verse of kin.</summary>
        private static string Other(BookVerse v) => v?.other >= 0 ? GodLore.Instance?.PageOf(Gods.ById(v.other))?.name : null;

        private static string TaleTitle(string key)
        {
            foreach (var d in DefDatabase<GodDef>.AllDefsListForReading)
            {
                var s = d.Story(key);
                if (s != null) return s.title;
            }
            return key ?? "";
        }

        // ================= The whole book, and the witnesses =================

        private static float WholeBook(float x, float y, float w)
        {
            y = Title(x, y, w, "CC_GodLore_BookTitle".Translate(), "CC_GodLore_BookLine".Translate(Faction.OfPlayerSilentFail?.Name ?? "").Resolve(), null);
            var lore = GodLore.Instance;
            return Entries(x, y, w, lore?.entries ?? new List<LoreEntry>(), true);
        }

        /// <summary>The entries in order, each dated (and, in the whole book, under whose name it is), the keeper's change of hand marked.</summary>
        private static float Entries(float x, float y, float w, List<LoreEntry> list, bool whole)
        {
            string hand = "\u0002";
            var lore = GodLore.Instance;
            foreach (var e in list)
            {
                if (e == null) continue;
                string k = e.keeper ?? "";
                if (k != hand)
                {
                    if (hand != "\u0002") y = Rule(x + w * 0.3f, y + 4f, w * 0.4f) + 6f;
                    string change = e.keeper != null ? "CC_GodLore_Hand".Translate(e.keeper.Named("KEEPER")).Resolve() : "CC_GodLore_NoHand".Translate().Resolve();
                    y += Para(x, y, w, "<i>" + change + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter) + 8f;
                    hand = k;
                }
                string when = Date(e.tick);
                if (whole)
                {
                    var root = e.page != null && lore != null ? lore.Root(lore.Named(e.page)) : null;
                    string of = root != null ? "CC_GodLore_Of".Translate(root.name.Named("NAME")).Resolve() : "CC_GodLore_OfUnnamed".Translate().Resolve();
                    when = when + "  ·  " + of;
                }
                y += Para(x, y, w, "<i>" + when + "</i>", UiKit.Faint, GameFont.Tiny);
                y += Para(x, y, w, e.text ?? "", UiKit.Ink, GameFont.Small) + 12f;
            }
            return y;
        }

        // ================= Type =================

        private static float Para(float x, float y, float w, string text, Color c, GameFont font, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            if (text.NullOrEmpty()) return 0f;
            Text.Font = font;
            Text.Anchor = anchor;
            float h = Text.CalcHeight(text, w);
            GUI.color = c;
            Widgets.Label(new Rect(x, y, w, h), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            return h;
        }

        /// <summary>
        /// Small capitals by size (the game has no small-caps font): every letter a capital, each word's first a size larger,
        /// the little words all small.
        /// </summary>
        private static string SmallCaps(string s, int big, int small)
        {
            if (s.NullOrEmpty()) return s;
            var sb = new StringBuilder();
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                string up = word.ToUpperInvariant();
                bool little = i > 0 && Little.Contains(word);
                if (little) sb.Append("<size=").Append(small).Append('>').Append(up).Append("</size>");
                else sb.Append("<size=").Append(big).Append('>').Append(up[0]).Append("</size><size=").Append(small).Append('>').Append(up.Substring(1)).Append("</size>");
            }
            return sb.ToString();
        }

        /// <summary>The little words a heading in small capitals keeps small throughout.</summary>
        private static readonly HashSet<string> Little = new HashSet<string> { "of", "and", "the", "its", "it", "is", "what", "on", "in", "a", "to", "by" };

        /// <summary>A part's heading: small capitals over a short rule.</summary>
        private static float PartHead(float x, float y, float w, string label, Color c)
        {
            y += 14f;
            y += Para(x, y, w, SmallCaps(label, 15, 11), c, GameFont.Small, TextAnchor.UpperCenter) + 3f;
            return Rule(x + w * 0.3f, y, w * 0.4f) + 10f;
        }

        /// <summary>A verse's number in the gutter, small and quiet.</summary>
        private static void Number(float x, float y, int n)
        {
            if (n <= 0) return;
            Para(x, y + 3f, Gutter - 8f, n.ToString(), UiKit.Faint, GameFont.Tiny, TextAnchor.UpperRight);
        }

        /// <summary>The cache of where a drop capital's verse breaks from beside the capital to the full column.</summary>
        private static readonly Dictionary<string, (string beside, string after)> splits = new Dictionary<string, (string, string)>();

        /// <summary>
        /// A verse opening with a drop capital: its first letter drawn large in the book's gold, the words beside it for two
        /// lines, then the full column.
        /// </summary>
        private static float DropCap(float x, float y, float w, string text, Color c)
        {
            if (text.NullOrEmpty() || text.Length < 3 || text.Contains("<")) return Para(x, y, w, text, c, GameFont.Small);
            string cap = "<size=38>" + text.Substring(0, 1) + "</size>";
            Text.Font = GameFont.Small;
            var size = Text.CalcSize(cap);
            float capW = size.x + 4f, lineH = Text.LineHeight;
            GUI.color = UiKit.Gold;
            Widgets.Label(new Rect(x, y - 7f, capW + 6f, size.y + 4f), cap);
            GUI.color = Color.white;
            string rest = text.Substring(1);
            string k = rest + "|" + (int)w;
            if (!splits.TryGetValue(k, out var split))
            {
                // The most words that fit beside the capital in two lines.
                var words = rest.Split(' ');
                int lo = 1, hi = words.Length, best = 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    if (Text.CalcHeight(string.Join(" ", words, 0, mid), w - capW) <= lineH * 2f + 1f)
                    {
                        best = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                split = (string.Join(" ", words, 0, best), best < words.Length ? string.Join(" ", words, best, words.Length - best) : null);
                if (splits.Count > 200) splits.Clear();
                splits[k] = split;
            }
            float h = Mathf.Max(Para(x + capW, y, w - capW, split.beside, c, GameFont.Small), lineH * 2f);
            if (!split.after.NullOrEmpty()) h += Para(x, y + h, w, split.after, c, GameFont.Small);
            return h;
        }

        /// <summary>A link to another chapter: small, underlined, in the link colour.</summary>
        private static float Link(float x, float y, float w, string label, string target)
        {
            Text.Font = GameFont.Tiny;
            var size = Text.CalcSize(label);
            var r = new Rect(x, y, Mathf.Min(size.x, w), size.y);
            bool over = Mouse.IsOver(r);
            GUI.color = over ? UiKit.LinkHot : UiKit.Link;
            Widgets.Label(r, label);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 1f), over ? UiKit.LinkHot : UiKit.LinkLine);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Show(target);
            }
            return size.y;
        }

        /// <summary>A thin rule across the column, a little shorter than it.</summary>
        private static float Rule(float x, float y, float w)
        {
            GUI.color = UiKit.DividerColor;
            Widgets.DrawLineHorizontal(x + w * 0.1f, y, w * 0.8f);
            GUI.color = Color.white;
            return y + 1f;
        }

        /// <summary>A small diamond, drawn a row at a time.</summary>
        private static void Diamond(float cx, float cy, int r, Color c)
        {
            for (int i = -r; i <= r; i++)
            {
                int half = r - Math.Abs(i);
                Widgets.DrawBoxSolid(new Rect(Mathf.Round(cx) - half, Mathf.Round(cy) + i, half * 2 + 1, 1f), c);
            }
        }

        /// <summary>The book's ornament under a title: two rules, a gold diamond between them, a small one either side.</summary>
        private static float Ornament(float x, float y, float w)
        {
            float cx = x + w / 2f, arm = w * 0.26f;
            Widgets.DrawBoxSolid(new Rect(cx - 18f - arm, y, arm, 1f), UiKit.DividerColor);
            Widgets.DrawBoxSolid(new Rect(cx + 18f, y, arm, 1f), UiKit.DividerColor);
            Diamond(cx, y, 4, UiKit.Quieter(UiKit.Gold, 0.25f));
            Diamond(cx - 11f, y, 1, UiKit.DividerColor);
            Diamond(cx + 11f, y, 1, UiKit.DividerColor);
            return y + 5f;
        }

        /// <summary>Three faint dots across the column: a piece of a tale nobody here has heard.</summary>
        private static void Dots(float x, float y, float w)
        {
            float cx = x + w / 2f;
            for (int i = -1; i <= 1; i++) Diamond(cx + i * 14f, y, 1, UiKit.Dim);
        }

        /// <summary>A torn edge across the column: a ragged line, the same for the same page.</summary>
        private static float Tear(float x, float y, float w, int seed)
        {
            int n = Mathf.Max(10, (int)(w / 12f));
            var prev = new Vector2(x, y + Jag(seed, 0));
            for (int i = 1; i <= n; i++)
            {
                var next = new Vector2(x + w * i / n, y + Jag(seed, i));
                Widgets.DrawLine(prev, next, UiKit.DividerColor, 1f);
                prev = next;
            }
            return y + 5f;
        }

        private static float Jag(int seed, int i)
        {
            uint h = ((uint)(seed * 73856093) ^ (uint)(i * 19349663)) * 2654435761u;
            return (h >> 16) % 7 - 3f;
        }

        /// <summary>A date as the colony writes it: "3rd of Aprimay, 5502".</summary>
        public static string Date(int tick)
        {
            try
            {
                var seat = Homes.Seat;
                var at = seat != null ? Find.WorldGrid.LongLatOf(seat.Tile) : Vector2.zero;
                return GenDate.DateFullStringAt(GenDate.TickGameToAbs(tick), at);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
