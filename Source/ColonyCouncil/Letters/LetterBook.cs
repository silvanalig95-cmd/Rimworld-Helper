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

        public void ExposeData()
        {
            Scribe_Values.Look(ref ours, "ours");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref note, "note");
            Scribe_Values.Look(ref shape, "shape");
            Scribe_Values.Look(ref shownAs, "shownAs", -1);
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

        /// <summary>Exchanges kept at most, and with any one notable.</summary>
        public const int MaxExchanges = 60, MaxPerNotable = 8;

        /// <summary>A page not told yet is matched to a game letter only this long after it was written.</summary>
        private const int PendingDays = 120;

        private static LetterBook cached;
        private static Game cachedGame;
        [Unsaved] private int version;
        [Unsaved] private static string keptText;
        [Unsaved] private static int keptVersion = -1;

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
            x.pages.Add(Page(false, n.Name, t, LetterShape.Written));
            b.Changed();
        }

        /// <summary>A word of theirs on its own (a friend's): an exchange of one page.</summary>
        public static void Note(Notable n, LetterText t)
        {
            var b = Instance;
            if (b == null || n == null || t == null) return;
            var x = b.New(n, -1, -1);
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
            ours = ours, who = who, tick = Now, text = LetterFormMath.Cap(t.letter, LetterFormMath.MaxKept), note = LetterFormMath.Cap(t.note, LetterFormMath.MaxNote),
            shape = (byte)shape
        };

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
            var ours = x?.Ours;
            if (ours == null) return null;
            label = "CC_LetterRead_Act".Translate((ours.who ?? "?").Named("PAWN")).Resolve();
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                exchanges ??= new List<LetterExchange>();
                exchanges.RemoveAll(x => x == null);
                version++;
            }
        }
    }
}
