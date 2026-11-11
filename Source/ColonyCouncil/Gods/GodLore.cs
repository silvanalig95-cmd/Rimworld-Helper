using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What an entry of the colony's book is.</summary>
    public enum LoreKind
    {
        /// <summary>Something colonists saw with their own eyes (a god's hand in an event, a god's moment), and what they said.</summary>
        Saw,
        /// <summary>Something a teller told: a visitor, a neighbour's letter, a colonist at supper, the priest.</summary>
        Heard,
        /// <summary>What one colonist believes.</summary>
        Believes,
        /// <summary>What one colonist will have none of.</summary>
        Doubts,
        /// <summary>Two names worked out to be one (by a colonist, or a teller saying so).</summary>
        Connected,
        /// <summary>A dream told at breakfast.</summary>
        Dreamt,
        /// <summary>A prayer, and what followed as the one who prayed felt it.</summary>
        Prayed
    }

    /// <summary>
    /// One entry of the colony's book: when, what kind, under which name (the page; null for things nobody could name),
    /// who saw it or believes it (colonists, by name), who told it and how, the story and piece it is of, and its text,
    /// written once in the voice of whoever kept the book then. Nothing in it is marked true or false.
    /// </summary>
    public class LoreEntry : IExposable
    {
        public int tick;
        public LoreKind kind;
        public string page;
        public List<string> who = new List<string>();
        public string teller;
        public string story;
        public int piece = -1;
        public string text;
        /// <summary>Whose hand it is in (the keeper's name, or null for none), and the voice.</summary>
        public string keeper;
        public GodLoreMath.Voice voice;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kind, "kind", LoreKind.Saw);
            Scribe_Values.Look(ref page, "page");
            Scribe_Collections.Look(ref who, "who", LookMode.Value);
            Scribe_Values.Look(ref teller, "teller");
            Scribe_Values.Look(ref story, "story");
            Scribe_Values.Look(ref piece, "piece", -1);
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref keeper, "keeper");
            Scribe_Values.Look(ref voice, "voice", GodLoreMath.Voice.Bare);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) who ??= new List<string>();
        }
    }

    /// <summary>
    /// A name the colony has heard a god by: the name itself (a page of the book), the god it is (hidden: never shown), when
    /// and from whom it was first heard, and the earlier name it was worked out to be (then its entries show under that one).
    /// </summary>
    public class LoreName : IExposable
    {
        public string name;
        public int god = -1;
        public int first;
        public string from;
        public string joinedTo;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref first, "first");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref joinedTo, "joinedTo");
        }
    }

    /// <summary>A god's hand the colony saw, kept a while so a neighbour's letter can speak of it later.</summary>
    public class LoreHand : IExposable
    {
        public int god = -1;
        public int tick;
        public int home = HomesMath.None;
        public string what;
        public bool written;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref written, "written");
        }
    }

    /// <summary>
    /// The colony's own book of the gods (the player, 11 November 2026: "information about the gods should only ever be
    /// like from the point of view of a the pawn ... what i learn and see about them is the same as my pawns do"). Every
    /// entry is something a colonist saw, was told, believes or felt, with its witnesses or teller and its date, written in
    /// the voice of the book's keeper (the colony's chaplain, else its moral guide, else its most devout, else its most
    /// lettered colonist; with none, short and plain). It can be wrong: a god's hand taken for another's is written as it
    /// was believed, and the truth is never marked. A god appears only under the names the colony has heard it by, each a
    /// page of its own until someone works out, or a teller says, that two names are one.
    /// <para>
    /// What feeds it: the gods' hands in events (Gods/GodHands.cs), the gods' moments and every act remembered with
    /// colonists who saw it (<see cref="Gods.Remember"/>), stories at supper, from visitors and in letters
    /// (Gods/GodTales.cs), and the other parts through <see cref="Saw"/>, <see cref="Heard(Pawn, God, string)"/>,
    /// <see cref="Believes"/>, <see cref="Dreamt"/> and <see cref="Prayed"/>. Saved with the game; the region window's
    /// "Book of the gods" reads it (Gods/GodLorePage.cs).
    /// </para>
    /// </summary>
    public class GodLore : GameComponent
    {
        public List<LoreEntry> entries = new List<LoreEntry>();
        public List<LoreName> names = new List<LoreName>();
        /// <summary>How many pieces of each story the colony has heard.</summary>
        public Dictionary<string, int> told = new Dictionary<string, int>();
        /// <summary>The last tick each god's hand was remarked on, and the last at each home.</summary>
        public Dictionary<string, int> lastHand = new Dictionary<string, int>();
        public Dictionary<int, int> lastHandAt = new Dictionary<int, int>();
        /// <summary>The last talk of the gods at each home.</summary>
        public Dictionary<int, int> lastTalk = new Dictionary<int, int>();
        /// <summary>The hands seen lately, for neighbours' letters.</summary>
        public List<LoreHand> hands = new List<LoreHand>();
        /// <summary>The stories retold whole in a neighbour's letter (each once).</summary>
        public List<string> retold = new List<string>();
        /// <summary>How many of the gods' doings each colonist has seen (by name), which makes them readier to believe.</summary>
        public Dictionary<string, int> seen = new Dictionary<string, int>();

        private List<string> tmpKeys;
        private List<int> tmpInts, tmpIntKeys;

        public GodLore(Game game)
        {
        }

        private static GodLore cached;
        private static Game cachedGame;

        public static GodLore Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodLore>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        private static GodLore Book => Gods.On ? Instance : null;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The book changed (for a page that keeps what it laid out).</summary>
        public static int Version { get; private set; }

        // ================= The keeper =================

        private static int keeperAt = -1;
        private static Pawn keeper;
        private static GodLoreMath.Voice keeperVoice = GodLoreMath.Voice.Bare;

        /// <summary>Who keeps the book now (worked out at most once an hour), and in what voice; null and Bare for nobody.</summary>
        public static Pawn Keeper(out GodLoreMath.Voice voice)
        {
            int now = Now;
            if (keeperAt < 0 || now - keeperAt >= GenDate.TicksPerHour || now < keeperAt || keeper != null && (keeper.Dead || keeper.Destroyed))
            {
                keeperAt = now;
                keeper = FindKeeper(out keeperVoice);
            }
            voice = keeperVoice;
            return keeper;
        }

        private static Pawn FindKeeper(out GodLoreMath.Voice voice)
        {
            voice = GodLoreMath.Voice.Bare;
            try
            {
                var people = Homes.All.SelectMany(Homes.People).Where(CanKeep).Distinct().ToList();
                if (people.Count == 0) return null;
                var priest = Interplay.Officer(Interplay.Chaplain, Homes.Seat);
                if (priest != null && people.Contains(priest))
                {
                    voice = GodLoreMath.VoiceOf(true, true, false, Intellect(priest));
                    return priest;
                }
                var guide = people.FirstOrDefault(MoralGuide);
                if (guide != null)
                {
                    voice = GodLoreMath.VoiceOf(true, true, false, Intellect(guide));
                    return guide;
                }
                var devout = people.OrderByDescending(Credence).First();
                if (Credence(devout) >= GodLoreMath.DevoutFrom)
                {
                    voice = GodLoreMath.VoiceOf(true, false, true, Intellect(devout));
                    return devout;
                }
                var lettered = people.OrderByDescending(Intellect).First();
                if (Intellect(lettered) >= GodLoreMath.LiterateFrom)
                {
                    voice = GodLoreMath.VoiceOf(true, false, false, Intellect(lettered));
                    return lettered;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book's keeper", e);
            }
            return null;
        }

        private static bool CanKeep(Pawn p) => p != null && !p.Dead && p.RaceProps?.Humanlike == true && p.DevelopmentalStage.Adult() && p.IsFreeNonSlaveColonist
                                               && !p.WorkTagIsDisabled(WorkTags.Intellectual);

        private static bool MoralGuide(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p?.Ideo == null) return false;
            try
            {
                var role = p.Ideo.GetRole(p);
                return role?.def?.defName != null && role.def.defName.IndexOf("Moralist", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static int Intellect(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;

        private static TraitDef tooSmart;

        /// <summary>How readily a colonist believes the gods are at work (GodLoreMath.Credence).</summary>
        public static float Credence(Pawn p)
        {
            if (p == null) return 0f;
            try
            {
                tooSmart ??= DefDatabase<TraitDef>.GetNamedSilentFail("TooSmart");
                float psychic = p.GetStatValue(StatDefOf.PsychicSensitivity);
                float certainty = ModsConfig.IdeologyActive && p.ideo != null ? p.ideo.Certainty : -1f;
                bool elder = p.ageTracker != null && p.ageTracker.AgeBiologicalYears >= 60;
                bool smart = tooSmart != null && p.story?.traits?.HasTrait(tooSmart) == true;
                int saw = Instance != null && Instance.seen.TryGetValue(p.LabelShort ?? "", out int n) ? n : 0;
                return GodLoreMath.Credence(psychic, certainty, elder, smart, saw, p.thingIDNumber);
            }
            catch
            {
                return 0f;
            }
        }

        // ================= Names =================

        /// <summary>A name's page: the name it was worked out to be, followed to the first.</summary>
        public LoreName Root(LoreName n)
        {
            int guard = 0;
            while (n?.joinedTo != null && guard++ < 20)
            {
                var to = names.FirstOrDefault(x => x.name == n.joinedTo);
                if (to == null) break;
                n = to;
            }
            return n;
        }

        public LoreName Named(string name) => name == null ? null : names.FirstOrDefault(x => x.name == name);

        /// <summary>The page the colony keeps a god under (its first name for it, not worked into another), or null for a god it has no name for.</summary>
        public LoreName PageOf(God g) => g == null ? null : names.Where(x => x.god == g.id && x.joinedTo == null).OrderBy(x => x.first).FirstOrDefault();

        /// <summary>The gods the colony has a name for (by key): what a believer can name.</summary>
        public HashSet<string> KnownOf()
        {
            var set = new HashSet<string>();
            foreach (var n in names)
            {
                var g = Gods.ById(n.god);
                if (g != null) set.Add(g.Key);
            }
            return set;
        }

        /// <summary>
        /// The name a colonist uses for a god: the colony's page for it where it has one, else their own people's name (the
        /// short name or their age's, Gods.NameBy), which becomes a page when it is written.
        /// </summary>
        public static string NameOf(God g, Pawn speaker)
        {
            if (g == null) return null;
            var b = Book;
            if (speaker != null && speaker.IsColonist && b != null)
            {
                var page = b.PageOf(g);
                if (page != null) return page.name;
            }
            return Gods.NameBy(g, speaker);
        }

        /// <summary>
        /// What the colony calls a god it sees (over it on the map, on its card): its name in the colony's book once someone
        /// here has heard of it, else what people see of it ("a great white thrumbo"). By key (a god's short name); anything
        /// else (a look's own sample, a name it gives at the door) as it is.
        /// </summary>
        public static string LabelFor(string key)
        {
            var g = Gods.ByKey(key);
            if (g == null) return key;
            var page = Instance?.PageOf(g);
            if (page != null) return page.name;
            return g.def?.unheard ?? key;
        }

        /// <summary>Makes sure the colony has the name as a page (heard from <paramref name="from"/>), and says under which page it is written.</summary>
        public string Page(string name, God g, string from)
        {
            if (name.NullOrEmpty()) return null;
            var n = Named(name);
            if (n == null)
            {
                if (names.Count >= GodLoreMath.MaxNames) return PageOf(g)?.name;
                n = new LoreName { name = name, god = g?.id ?? -1, first = Now, from = from };
                names.Add(n);
                Version++;
            }
            return Root(n)?.name ?? name;
        }

        // ================= Writing =================

        /// <summary>Writes an entry in the keeper's voice (its text made by <paramref name="make"/>), keeping the book within its size.</summary>
        private LoreEntry Write(LoreKind kind, string page, IEnumerable<string> who, string teller, Func<GodLoreMath.Voice, string> make, string story = null, int piece = -1)
        {
            var k = Keeper(out var voice);
            string text = make(voice);
            if (text.NullOrEmpty()) return null;
            var e = new LoreEntry
            {
                tick = Now, kind = kind, page = page, who = who?.Where(x => !x.NullOrEmpty()).Distinct().ToList() ?? new List<string>(), teller = teller,
                story = story, piece = piece, text = text.CapitalizeFirst(), keeper = k?.LabelShort, voice = voice
            };
            entries.Add(e);
            while (entries.Count > GodLoreMath.MaxEntries) entries.RemoveAt(0);
            Version++;
            return e;
        }

        private static string Key(string what, GodLoreMath.Voice v) => "CC_GodLore_" + what + "_" + v;

        private static string Names(IEnumerable<string> who) => TextUtil.JoinAnd((who ?? Enumerable.Empty<string>()).Where(x => !x.NullOrEmpty()).Distinct().ToList());

        /// <summary>
        /// Colonists saw something of a god (or of something nobody could name: <paramref name="g"/> null): their names, what
        /// they saw in words, and what was said there ((speaker, line, scoffed)). <paramref name="g"/> is the god they took it
        /// for, which may not be the god it was. Each witness counts it as seen.
        /// </summary>
        public static LoreEntry Saw(IEnumerable<Pawn> pawns, God g, string words, IList<(Pawn who, string line, bool scoffed)> said = null, string key = "Saw")
        {
            var b = Book;
            var list = pawns?.Where(p => p != null).Distinct().ToList() ?? new List<Pawn>();
            if (b == null || list.Count == 0 || words.NullOrEmpty()) return null;
            try
            {
                var first = list.FirstOrDefault(p => p.IsColonist) ?? list[0];
                string page = g != null ? b.Page(NameOf(g, first), g, "CC_GodLore_FromSeen".Translate(Names(list.Select(p => p.LabelShort)).Named("WHO")).Resolve()) : null;
                foreach (var p in list.Where(p => p.IsColonist))
                    b.seen[p.LabelShort] = (b.seen.TryGetValue(p.LabelShort, out int n) ? n : 0) + 1;
                var who = list.Select(p => p.LabelShort).ToList();
                return b.Write(LoreKind.Saw, page, who, null, v =>
                {
                    string text = Key(key ?? "Saw", v).Translate(Names(who).Named("WHO"), words.Named("WHAT")).Resolve();
                    if (said != null)
                        foreach (var (p, line, scoffed) in said)
                            if (p != null && !line.NullOrEmpty())
                                text += " " + Key(scoffed ? "Scoffed" : "Said", v).Translate(p.LabelShort.Named("PAWN"), line.Named("LINE")).Resolve();
                    return text;
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book, seen", e);
                return null;
            }
        }

        /// <summary>A teller (a visitor, a neighbour, a colonist at supper, the priest) told something of a god (or of something unnamed): <paramref name="how"/> is where (CC_GodLore_How_*: Fire, Gate, Letter, Supper, Sermon).</summary>
        public static LoreEntry Heard(Pawn teller, God g, string words, string how = "Fire", string storyKey = null, int piece = -1, string name = null) =>
            Heard(TellerOf(teller), g, words, how, storyKey, piece, name ?? NameOf(g, teller));

        /// <summary>The same, from a teller known only by name ("Mirel of Karond-Keth"), with the name they used for the god.</summary>
        public static LoreEntry Heard(string teller, God g, string words, string how, string storyKey, int piece, string name)
        {
            var b = Book;
            if (b == null || words.NullOrEmpty()) return null;
            try
            {
                teller ??= "CC_GodLore_Someone".Translate();
                string page = g != null ? b.Page(name ?? Gods.Name(g), g, "CC_GodLore_FromTold".Translate(teller.Named("TELLER"), HowWords(how).Named("HOW")).Resolve()) : null;
                string title = storyKey != null ? g?.def?.Story(storyKey)?.title ?? StoryTitle(storyKey) : null;
                var e = b.Write(LoreKind.Heard, page, null, teller, v => (title != null ? Key("HeardStory", v) : Key("Heard", v))
                    .Translate(teller.Named("TELLER"), HowWords(how).Named("HOW"), words.Named("WORDS"), (title ?? "").UncapitalizeFirst().Named("TITLE")).Resolve(), storyKey, piece);
                // A teller who uses another name than the colony's for a god may say so.
                var root = g != null ? b.PageOf(g) : null;
                if (e != null && root != null && page != null && page != root.name && Rand.Chance(GodLoreMath.TellerConnects))
                    b.Connect(Named(b, page), root, null, teller);
                return e;
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: the book, heard", ex);
                return null;
            }
        }

        private static LoreName Named(GodLore b, string page) => b.Named(page);

        /// <summary>A story's title by its key, from whichever god's def tells it.</summary>
        private static string StoryTitle(string key) => DefDatabase<GodDef>.AllDefsListForReading.Select(d => d.Story(key)).FirstOrDefault(s => s != null)?.title;

        public static string HowWords(string how) => ("CC_GodLore_How_" + (how ?? "Fire")).Translate();

        /// <summary>A teller as the book names them: a colonist by name, anyone else by name and people ("Mirel of Karond-Keth").</summary>
        public static string TellerOf(Pawn p)
        {
            if (p == null) return null;
            if (p.IsColonist || p.Faction == null || p.Faction.IsPlayer) return p.LabelShort;
            return "CC_GodLore_TellerOf".Translate(p.LabelShort.Named("NAME"), p.Faction.Name.Named("PEOPLE")).Resolve();
        }

        /// <summary>A colonist believes something of a god (their words), or (<paramref name="doubts"/>) will have none of it.</summary>
        public static LoreEntry Believes(Pawn p, God g, string words, bool doubts = false)
        {
            var b = Book;
            if (b == null || p == null || words.NullOrEmpty()) return null;
            try
            {
                string page = g != null ? b.Page(NameOf(g, p), g, "CC_GodLore_FromBelief".Translate(p.LabelShort.Named("PAWN")).Resolve()) : null;
                return b.Write(doubts ? LoreKind.Doubts : LoreKind.Believes, page, new[] { p.LabelShort }, null,
                    v => Key(doubts ? "Doubts" : "Believes", v).Translate(p.LabelShort.Named("PAWN"), words.Named("WORDS")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book, believed", e);
                return null;
            }
        }

        /// <summary>A colonist dreamt something of a god and told it at breakfast (their words).</summary>
        public static LoreEntry Dreamt(Pawn p, God g, string words) => Felt(LoreKind.Dreamt, "Dreamt", p, g, words);

        /// <summary>A colonist prayed, and what followed as they felt it (their words).</summary>
        public static LoreEntry Prayed(Pawn p, God g, string words) => Felt(LoreKind.Prayed, "Prayed", p, g, words);

        private static LoreEntry Felt(LoreKind kind, string key, Pawn p, God g, string words)
        {
            var b = Book;
            if (b == null || p == null || words.NullOrEmpty()) return null;
            try
            {
                string page = g != null ? b.Page(NameOf(g, p), g, "CC_GodLore_FromBelief".Translate(p.LabelShort.Named("PAWN")).Resolve()) : null;
                return b.Write(kind, page, new[] { p.LabelShort }, null, v => Key(key, v).Translate(p.LabelShort.Named("PAWN"), words.Named("WORDS")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book, felt", e);
                return null;
            }
        }

        /// <summary>
        /// Two names worked out to be one: the later page under the earlier, by a colonist who worked it out or a teller who
        /// said so. Written in the keeper's voice.
        /// </summary>
        public void Connect(LoreName a, LoreName into, Pawn by, string teller)
        {
            if (a == null || into == null || a == into || a.joinedTo != null) return;
            a.joinedTo = into.name;
            Version++;
            string who = by?.LabelShort ?? teller;
            Write(LoreKind.Connected, into.name, by != null ? new[] { by.LabelShort } : null, teller,
                v => Key(by != null ? "Connected" : "ConnectedTold", v).Translate((who ?? "").Named("WHO"), a.name.Named("A"), into.name.Named("B")).Resolve());
        }

        // ================= The day =================

        /// <summary>The region's day: now and then, someone works out that two names in the book are one god; old hands let go.</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            b.hands.RemoveAll(h => h == null || now - h.tick > GodHandsMath.LetterUntilDays * GenDate.TicksPerDay);
            foreach (var group in b.names.Where(n => n.god >= 0 && n.joinedTo == null).GroupBy(n => n.god).Where(gr => gr.Count() >= 2).ToList())
            {
                var pages = group.OrderBy(n => n.first).ToList();
                var into = pages[0];
                var a = pages[1];
                var by = Homes.All.SelectMany(Homes.People).Where(CanKeep).OrderByDescending(Intellect).FirstOrDefault();
                if (by == null) continue;
                int under = b.entries.Count(e => e.page == into.name || e.page == a.name);
                if (!Rand.Chance(GodLoreMath.ConnectChance(Intellect(by), under))) continue;
                b.Connect(a, into, by, null);
                return;
            }
        }

        // ================= Saving =================

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            Scribe_Collections.Look(ref names, "names", LookMode.Deep);
            Scribe_Collections.Look(ref told, "told", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpInts);
            Scribe_Collections.Look(ref lastHand, "lastHand", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpInts);
            Scribe_Collections.Look(ref lastHandAt, "lastHandAt", LookMode.Value, LookMode.Value, ref tmpIntKeys, ref tmpInts);
            Scribe_Collections.Look(ref lastTalk, "lastTalk", LookMode.Value, LookMode.Value, ref tmpIntKeys, ref tmpInts);
            Scribe_Collections.Look(ref hands, "hands", LookMode.Deep);
            Scribe_Collections.Look(ref retold, "retold", LookMode.Value);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpInts);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                entries ??= new List<LoreEntry>();
                entries.RemoveAll(e => e == null);
                names ??= new List<LoreName>();
                names.RemoveAll(n => n == null || n.name.NullOrEmpty());
                told ??= new Dictionary<string, int>();
                lastHand ??= new Dictionary<string, int>();
                lastHandAt ??= new Dictionary<int, int>();
                lastTalk ??= new Dictionary<int, int>();
                hands ??= new List<LoreHand>();
                hands.RemoveAll(h => h == null);
                retold ??= new List<string>();
                seen ??= new Dictionary<string, int>();
                Version++;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            keeperAt = -1;
            // The book names gods by the ids of the nine: a name of a god no longer there (an older save's) is a name of nothing.
            foreach (var n in names)
                if (n.god >= 0 && Gods.ById(n.god) == null)
                    n.god = -1;
        }
    }
}
