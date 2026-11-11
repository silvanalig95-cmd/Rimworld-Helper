using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One letter of an exchange, as it was written: whose, by whom, when, its words and the narrator's lines under it.</summary>
    public class LetterPage : IExposable
    {
        public bool ours;
        public string who;
        public int tick;
        public string text;
        public string note;
        /// <summary>The shape it took (LetterShape): a letter, a call's transcript, words passed on.</summary>
        public byte shape;
        /// <summary>The game letter (its ID) it was told by, for the card's "Read ... letter"; -1 until it is told.</summary>
        public int shownAs = -1;
        /// <summary>The shape the letter took (LetterKind): a writer doesn't take the same unusual one twice running.</summary>
        public byte kind;
        /// <summary>The answer to the riddle it asks, until the writer's next letter gives it; else null.</summary>
        public string answer;

        /// <summary>A long letter, read in pages (its words over what the game's own letter shows whole).</summary>
        public bool Long => text != null && text.Length > LetterKindMath.ShowMax;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ours, "ours");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref note, "note");
            Scribe_Values.Look(ref shape, "shape");
            Scribe_Values.Look(ref shownAs, "shownAs", -1);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref answer, "answer");
        }
    }

    /// <summary>
    /// An exchange of letters with one notable: the colony's letter and their answer; their letter written first, the
    /// colony's reply and their thanks; or a word of theirs on its own (a friend's, a call). Three pages at most.
    /// </summary>
    public class LetterExchange : IExposable
    {
        public int id;
        public int notable = -1;
        public string name, town;
        /// <summary>The colony's letter it began with (NeighbourLetter.id), or the world's letter it answers (WorldAsk.id), else -1.</summary>
        public int letterId = -1, askId = -1;
        public int started;
        public List<LetterPage> pages = new List<LetterPage>();

        public LetterPage Ours => pages.FirstOrDefault(p => p.ours);

        public int Last => pages.Count == 0 ? started : pages.Max(p => p.tick);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref letterId, "letterId", -1);
            Scribe_Values.Look(ref askId, "askId", -1);
            Scribe_Values.Look(ref started, "started");
            Scribe_Collections.Look(ref pages, "pages", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pages ??= new List<LetterPage>();
                pages.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>A thread of a correspondence, saved (Letters/LetterThreadMath.cs, ThreadRec): something a letter said that a later one takes up.</summary>
    public class LetterThread : ThreadRec, IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref ours, "ours");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref touched, "touched", -1);
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref told, "told");
        }

        public static LetterThread Of(ThreadRec r, int at, int tick) => new LetterThread { key = r.key, value = r.value ?? "", ours = r.ours, at = at, tick = tick };
    }

    /// <summary>
    /// A correspondence with one notable, as it builds up (the player, 10 November 2026: "continued letter exchange should
    /// build up"): how many letters have passed each way, since when and when last, the warmest their regard reached, who
    /// of the colony writes to them, and the threads a later letter can take up (Letters/LetterThreadMath.cs). Small and
    /// bounded: LetterBook.MaxCorrespondences, LetterThreadMath.MaxThreads threads each.
    /// </summary>
    public class Correspondence : IExposable
    {
        public int notable = -1;
        public int ours, theirs;
        /// <summary>The first letter and the last either way, the colony's last and theirs (ticks; -1 none).</summary>
        public int first = -1, last = -1, lastOurs = -1, lastTheirs = -1;
        /// <summary>The warmest their regard of the colony was while they wrote.</summary>
        public int peak;
        /// <summary>Who of the colony wrote its last letters to them, how many in a row, and who wrote before them (two or more in a row).</summary>
        public string pen, oldPen;
        public int penRun;
        public List<LetterThread> threads = new List<LetterThread>();

        public int Letters => ours + theirs;

        public void ExposeData()
        {
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref ours, "ours");
            Scribe_Values.Look(ref theirs, "theirs");
            Scribe_Values.Look(ref first, "first", -1);
            Scribe_Values.Look(ref last, "last", -1);
            Scribe_Values.Look(ref lastOurs, "lastOurs", -1);
            Scribe_Values.Look(ref lastTheirs, "lastTheirs", -1);
            Scribe_Values.Look(ref peak, "peak");
            Scribe_Values.Look(ref pen, "pen");
            Scribe_Values.Look(ref oldPen, "oldPen");
            Scribe_Values.Look(ref penRun, "penRun");
            Scribe_Collections.Look(ref threads, "threads", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                threads ??= new List<LetterThread>();
                threads.RemoveAll(t => t == null || !LetterThreadMath.Known(t.key));
            }
        }
    }

    /// <summary>
    /// The letters between the colony and the world, kept to be read again (the player, 9 November 2026): the colony's own
    /// letters as written (LetterForm.Ours), their answers, their letters written first and a friend's word, as exchanges with
    /// each notable. Read from the answer's event card ("Read ... letter", <see cref="ReadAct"/>), the Diplomacy screen's
    /// people page (<see cref="With"/>) and the mailbag, in the reading window (Letters/LetterReadWindow.cs). Kept: the
    /// newest <see cref="MaxPerNotable"/> with each notable and <see cref="MaxExchanges"/> in all, each page capped
    /// (LetterFormMath.MaxKept, MaxNote). An older save starts it empty.
    /// </summary>
    public class LetterBook : GameComponent
    {
        public List<LetterExchange> exchanges = new List<LetterExchange>();
        public int nextId = 1;
        /// <summary>
        /// The long pieces written into letters (a poem, a sermon, a story's parts, a raid's account...), by their first
        /// words (LetterKindMath.Key), the newest last: none comes round again while it is here; LetterKindMath.MaxUsed at most.
        /// </summary>
        public List<string> used = new List<string>();
        /// <summary>Each correspondence as it builds up (Letters/LetterThreads.cs): MaxCorrespondences at most, the longest quiet let go.</summary>
        public List<Correspondence> correspondences = new List<Correspondence>();

        /// <summary>Exchanges kept at most, and with any one notable.</summary>
        public const int MaxExchanges = 60, MaxPerNotable = 8;

        /// <summary>Correspondences kept at most (the longest quiet let go).</summary>
        public const int MaxCorrespondences = 60;

        /// <summary>The newest this many long letters are kept whole (LetterFormMath.MaxKeptLong); older ones are cut to LetterFormMath.MaxKept.</summary>
        public const int MaxLong = 16;

        /// <summary>A page not told yet is matched to a game letter only this long after it was written.</summary>
        private const int PendingDays = 120;

        private static LetterBook cached;
        private static Game cachedGame;
        [Unsaved] private int version;
        [Unsaved] private static string keptText;
        [Unsaved] private static int keptVersion = -1;
        [Unsaved] private HashSet<string> usedSet;

        public LetterBook(Game game)
        {
        }

        public static LetterBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<LetterBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Bumped whenever an exchange changes (the people page and the reading window rebuild from it).</summary>
        public static int Version => Instance?.version ?? 0;

        private void Changed()
        {
            version++;
            Trim();
        }

        // ================= Written and answered =================

        /// <summary>The colony's letter written: a new exchange, or the reply to the world's letter it answers.</summary>
        public static void Written(NeighbourLetter l, Notable n, Pawn writer, LetterText t)
        {
            var b = Instance;
            if (b == null || l == null || n == null || t == null) return;
            var x = ReplyTo(l) ?? b.Of(l.id) ?? b.New(n, l.id, -1);
            x.pages.RemoveAll(p => p.ours);
            x.pages.Add(Page(true, writer?.LabelShort ?? l.WriterName, t, LetterShape.Written));
            if (l.spoken) x.pages[x.pages.Count - 1].shape = (byte)LetterShape.Spoken;
            b.Changed();
        }

        /// <summary>Their answer made (to be told when it reaches the colony): the exchange's next page.</summary>
        public static void Answered(NeighbourLetter l, Notable n, LetterText t, LetterShape shape)
        {
            var b = Instance;
            if (b == null || l == null || n == null || t == null) return;
            var x = ReplyTo(l) ?? b.Of(l.id) ?? b.New(n, l.id, -1);
            if (shape == LetterShape.Written) b.Given(n.id);
            x.pages.Add(Page(false, n.Name, t, shape));
            while (x.pages.Count > 3) x.pages.RemoveAt(0);
            b.Changed();
        }

        /// <summary>Their letter written first (Dealings/WorldWrites.cs): a new exchange the colony's reply will join.</summary>
        public static void TheirFirst(Notable n, int askId, LetterText t)
        {
            var b = Instance;
            if (b == null || n == null || t == null) return;
            var x = b.New(n, -1, askId);
            b.Given(n.id);
            x.pages.Add(Page(false, n.Name, t, LetterShape.Written));
            b.Changed();
        }

        /// <summary>A word of theirs on its own (a friend's): an exchange of one page.</summary>
        public static void Note(Notable n, LetterText t)
        {
            var b = Instance;
            if (b == null || n == null || t == null) return;
            var x = b.New(n, -1, -1);
            b.Given(n.id);
            x.pages.Add(Page(false, n.Name, t, LetterShape.Written));
            b.Changed();
        }

        /// <summary>A letter called off before it left: an exchange that is only the colony's letter is let go.</summary>
        public static void Dropped(NeighbourLetter l)
        {
            var b = Instance;
            var x = b?.Of(l?.id ?? -1);
            if (x == null || x.pages.Any(p => !p.ours)) return;
            b.exchanges.Remove(x);
            b.version++;
        }

        private static LetterPage Page(bool ours, string who, LetterText t, LetterShape shape) => new LetterPage
        {
            ours = ours, who = who, tick = Now, text = LetterFormMath.Cap(t.letter, t.letter.Length > LetterFormMath.MaxKept ? LetterFormMath.MaxKeptLong : LetterFormMath.MaxKept),
            note = LetterFormMath.Cap(t.note, LetterFormMath.MaxNote), shape = (byte)shape, kind = (byte)t.kind, answer = t.riddleAnswer
        };

        /// <summary>An answer owed to a riddle of theirs was given in their new letter (its P.S.): owed no more.</summary>
        private void Given(int notable)
        {
            foreach (var x in exchanges)
                if (x.notable == notable)
                    foreach (var p in x.pages)
                        if (!p.ours)
                            p.answer = null;
        }

        private LetterExchange Of(int letterId) => letterId < 0 ? null : exchanges.LastOrDefault(x => x.letterId == letterId);

        /// <summary>The exchange a reply to the world's letter belongs to (its topic "reply:ID").</summary>
        private static LetterExchange ReplyTo(NeighbourLetter l)
        {
            var a = l?.purpose == LetterPurpose.Dealing ? WorldWrites.AskOf(l.topicKey) : null;
            if (a == null) return null;
            var x = Instance?.exchanges.LastOrDefault(e => e.askId == a.id);
            if (x != null && x.letterId < 0) x.letterId = l.id;
            return x;
        }

        private LetterExchange New(Notable n, int letterId, int askId)
        {
            var x = new LetterExchange { id = nextId++, notable = n.id, name = n.Name, town = n.townName, letterId = letterId, askId = askId, started = Now };
            exchanges.Add(x);
            return x;
        }

        /// <summary>The newest kept with each notable, and in all; an empty exchange let go.</summary>
        private void Trim()
        {
            exchanges.RemoveAll(x => x == null || x.pages.Count == 0 && Now - x.started > GenDate.TicksPerDay * 2);
            foreach (var g in exchanges.GroupBy(x => x.notable).Where(g => g.Count() > MaxPerNotable).ToList())
                foreach (var old in g.OrderBy(x => x.Last).Take(g.Count() - MaxPerNotable).ToList())
                    exchanges.Remove(old);
            while (exchanges.Count > MaxExchanges) exchanges.Remove(exchanges.OrderBy(x => x.Last).First());
            // The newest long letters kept whole; older ones cut to a usual letter's length.
            var longPages = exchanges.SelectMany(x => x.pages).Where(p => p != null && p.text != null && p.text.Length > LetterFormMath.MaxKept).OrderByDescending(p => p.tick).ToList();
            for (int i = MaxLong; i < longPages.Count; i++) longPages[i].text = LetterFormMath.Cap(longPages[i].text, LetterFormMath.MaxKept);
            while (used.Count > LetterKindMath.MaxUsed) used.RemoveAt(0);
            TrimThreads();
        }

        /// <summary>The correspondences in bounds: MaxCorrespondences, the longest quiet let go; each one's threads stale let go, LetterThreadMath.MaxThreads at most.</summary>
        private void TrimThreads()
        {
            int now = Now;
            correspondences.RemoveAll(c => c == null || c.notable < 0);
            while (correspondences.Count > MaxCorrespondences) correspondences.Remove(correspondences.OrderBy(c => c.last).First());
            foreach (var c in correspondences)
            {
                c.threads.RemoveAll(t => t == null || LetterThreadMath.Stale(t, now));
                while (c.threads.Count > LetterThreadMath.MaxThreads) c.threads.RemoveAt(0);
            }
        }

        // ================= The correspondence =================

        /// <summary>
        /// The correspondence with a notable, or null when none yet; made when asked to (an older save's: counted from the
        /// letters kept with them, its first and last letter theirs).
        /// </summary>
        public static Correspondence CorrespondenceWith(Notable n, bool make)
        {
            var b = Instance;
            if (b == null || n == null) return null;
            var c = b.correspondences.FirstOrDefault(x => x.notable == n.id);
            if (c != null || !make) return c;
            c = new Correspondence { notable = n.id, peak = NotableBook.Opinion(n) };
            foreach (var x in b.exchanges.Where(x => x.notable == n.id))
                foreach (var p in x.pages.Where(p => p != null))
                {
                    if (p.ours)
                    {
                        c.ours++;
                        c.lastOurs = Math.Max(c.lastOurs, p.tick);
                        c.pen = p.who;
                    }
                    else
                    {
                        c.theirs++;
                        c.lastTheirs = Math.Max(c.lastTheirs, p.tick);
                    }
                    c.first = c.first < 0 ? p.tick : Math.Min(c.first, p.tick);
                    c.last = Math.Max(c.last, p.tick);
                }
            if (c.pen != null) c.penRun = 1;
            b.correspondences.Add(c);
            b.TrimThreads();
            return c;
        }

        // ================= The shapes =================

        /// <summary>
        /// The shape of the last letter a writer wrote: theirs (a notable's), or the colony's to them by this colonist
        /// (<paramref name="who"/>); the usual when there is none.
        /// </summary>
        public static LetterKind LastKind(int notable, bool ours, string who)
        {
            var b = Instance;
            if (b == null) return LetterKind.Usual;
            LetterPage last = null;
            foreach (var x in b.exchanges)
            {
                if (x.notable != notable) continue;
                foreach (var p in x.pages)
                    if (p != null && p.ours == ours && (!ours || p.who == who) && (last == null || p.tick >= last.tick))
                        last = p;
            }
            return last == null ? LetterKind.Usual : (LetterKind)last.kind;
        }

        /// <summary>The answer to the riddle in a notable's letter that their next one owes, or null.</summary>
        public static string Owed(int notable)
        {
            var b = Instance;
            if (b == null) return null;
            LetterPage owed = null;
            foreach (var x in b.exchanges)
                if (x.notable == notable)
                    foreach (var p in x.pages)
                        if (p != null && !p.ours && !p.answer.NullOrEmpty() && (owed == null || p.tick >= owed.tick))
                            owed = p;
            return owed?.answer;
        }

        /// <summary>A long piece already written into a letter (by its key) and still remembered.</summary>
        public static bool Used(string key)
        {
            var b = Instance;
            if (b == null || key.NullOrEmpty()) return false;
            b.usedSet ??= new HashSet<string>(b.used);
            return b.usedSet.Contains(key);
        }

        /// <summary>A long piece written into a letter: remembered (the newest last), the oldest let go past LetterKindMath.MaxUsed.</summary>
        public static void Use(string key)
        {
            var b = Instance;
            if (b == null || key.NullOrEmpty()) return;
            LetterKindMath.Remember(b.used, key);
            b.usedSet = null;
        }

        // ================= Told, and read =================

        /// <summary>
        /// A letter the teller made (UI/Tell.cs): if its words are a page of theirs not told yet, that page was told by it,
        /// so the card can offer the colony's letter beside it.
        /// </summary>
        public static void Told(Letter letter)
        {
            var b = Instance;
            if (b == null || letter == null || b.exchanges.Count == 0) return;
            try
            {
                string body = (letter as ChoiceLetter)?.Text.Resolve();
                if (body.NullOrEmpty()) return;
                int now = Now;
                foreach (var x in b.exchanges)
                    foreach (var p in x.pages)
                    {
                        if (p.ours || p.shownAs >= 0 || p.text.NullOrEmpty() || now - p.tick > GenDate.TicksPerDay * PendingDays) continue;
                        string head = p.text.Length > 160 ? p.text.Substring(0, 160) : p.text;
                        if (!body.Contains(head)) continue;
                        p.shownAs = letter.ID;
                        b.version++;
                        return;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: matching a letter told", e);
            }
        }

        /// <summary>The exchange a game letter told, or null.</summary>
        public static LetterExchange ByLetter(int letterId) => letterId < 0 ? null : Instance?.exchanges.FirstOrDefault(x => x.pages.Any(p => p.shownAs == letterId));

        /// <summary>
        /// The act on an answer's card: "Read {PAWN}'s letter", when the exchange it ends holds the colony's letter. It opens
        /// the reading window over the card and leaves the card open.
        /// </summary>
        public static DiaOption ReadAct(Letter letter, out string label)
        {
            label = null;
            var x = letter != null ? ByLetter(letter.ID) : null;
            if (x == null) return null;
            var ours = x.Ours;
            // A long letter, of which the card shows the first page: the whole of it, beside the colony's letter when there is one.
            var told = x.pages.FirstOrDefault(p => p.shownAs == letter.ID);
            if (told != null && told.Long) label = "CC_LetterRead_Whole".Translate().Resolve();
            else if (ours != null) label = "CC_LetterRead_Act".Translate((ours.who ?? "?").Named("PAWN")).Resolve();
            else return null;
            return new DiaOption(label) { action = () => LetterReadWindow.Open(x), resolveTree = false };
        }

        /// <summary>The exchanges with a notable, newest first.</summary>
        public static List<LetterExchange> With(int notable) =>
            Instance?.exchanges.Where(x => x.notable == notable && x.pages.Count > 0).OrderByDescending(x => x.Last).ToList() ?? new List<LetterExchange>();

        /// <summary>The exchange a colony's letter began (still in the mailbag, or on its way).</summary>
        public static LetterExchange ForLetter(int letterId) => Instance?.Of(letterId);

        /// <summary>The colonist who last wrote to them, or null.</summary>
        public static string LastWriter(Notable n) =>
            n == null ? null : Instance?.exchanges.Where(x => x.notable == n.id && x.Ours != null).OrderByDescending(x => x.Last).FirstOrDefault()?.Ours.who;

        /// <summary>Every kept letter's words together, for the lines not to repeat what was already written (rebuilt as they change).</summary>
        public static string KeptText
        {
            get
            {
                var b = Instance;
                if (b == null) return null;
                if (keptVersion == b.version && keptText != null) return keptText;
                keptVersion = b.version;
                keptText = string.Join("\n", b.exchanges.SelectMany(x => x.pages).Select(p => p.text));
                return keptText;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Trim();
            Scribe_Collections.Look(ref exchanges, "exchanges", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref used, "used", LookMode.Value);
            Scribe_Collections.Look(ref correspondences, "correspondences", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                exchanges ??= new List<LetterExchange>();
                exchanges.RemoveAll(x => x == null);
                used ??= new List<string>();
                used.RemoveAll(k => k.NullOrEmpty());
                correspondences ??= new List<Correspondence>();
                correspondences.RemoveAll(c => c == null || c.notable < 0);
                usedSet = null;
                version++;
            }
        }
    }
}
