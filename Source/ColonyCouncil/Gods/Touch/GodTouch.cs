using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' touch, saved with the game: when each god's small, notable and great effects last came at each home, when
    /// each occurrence last left one, when a god last took an offering before everyone's eyes, the effects lately (to lean
    /// away from them) and the occurrences lately (to give the other gods their turn). Bounded: a few keys a god and a home,
    /// the two lists capped.
    /// </summary>
    public class GodTouchBook : GameComponent
    {
        /// <summary>
        /// When things last came, by key: "s|god|home", "n|god|home", "g|god|home" (a god's small, notable, great), "nc|home",
        /// "gc|home" (any god's notable and great), "k|occurrence|home" (an occurrence's last effect), "p|god|home" and
        /// "pc|home" (an offering taken plainly).
        /// </summary>
        public Dictionary<string, int> last = new Dictionary<string, int>();

        /// <summary>The effects lately, newest last: "home|god|effect|tick", <see cref="GodTouchMath.RecentKept"/> at most.</summary>
        public List<string> recent = new List<string>();

        /// <summary>The occurrences seen lately, newest last: "home|god|tick", <see cref="MaxSeen"/> at most, and none older than a season.</summary>
        public List<string> seen = new List<string>();

        public const int MaxSeen = 60;

        /// <summary>
        /// What an occurrence's own effect keeps doing for a while (Gods/Touch/GodImpact.cs: a body rotting away, a room held
        /// cold, the warm ground, the long noon, rain on its way ...), <see cref="GodImpactMath.RunningKept"/> at most, each gone
        /// when its time is up.
        /// </summary>
        public List<GodTouchRunning> running = new List<GodTouchRunning>();

        /// <summary>Works named for a god and admired since (their beauty a little higher), <see cref="GodImpactMath.AdmiredKept"/> at most.</summary>
        public List<Thing> admired = new List<Thing>();

        public GodTouchBook(Game game)
        {
        }

        private static GodTouchBook cached;
        private static Game cachedGame;

        public static GodTouchBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodTouchBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public int Last(string key) => key != null && last.TryGetValue(key, out int t) ? t : -999999;

        public void Set(string key, int tick)
        {
            if (key != null) last[key] = tick;
        }

        /// <summary>Whole days since a key's tick (a long time for never).</summary>
        public float DaysSince(string key, int now)
        {
            int t = Last(key);
            return t < -900000 ? 9999f : (now - t) / (float)GenDate.TicksPerDay;
        }

        /// <summary>The effects a god left at a home lately, newest first, by name (for GodPaceMath.Lean).</summary>
        public List<string> RecentOf(int home, string god)
        {
            var list = new List<string>();
            string head = home + "|" + god + "|";
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                var r = recent[i];
                if (r == null || !r.StartsWith(head, StringComparison.Ordinal)) continue;
                var parts = r.Split('|');
                if (parts.Length >= 3) list.Add(parts[2]);
            }
            return list;
        }

        public void Did(int home, string god, TouchEffect e, int now)
        {
            recent.Add(home + "|" + god + "|" + e + "|" + now);
            while (recent.Count > GodTouchMath.RecentKept) recent.RemoveAt(0);
        }

        /// <summary>How many of a god's occurrences were seen at a home in the last <paramref name="days"/> days.</summary>
        public int SeenOf(int home, string god, int now, float days)
        {
            int n = 0;
            string head = home + "|" + god + "|";
            foreach (var s in seen)
            {
                if (s == null || !s.StartsWith(head, StringComparison.Ordinal)) continue;
                if (int.TryParse(s.Substring(head.Length), out int t) && now - t <= days * GenDate.TicksPerDay) n++;
            }
            return n;
        }

        public void Saw(int home, string god, int now)
        {
            seen.Add(home + "|" + god + "|" + now);
            seen.RemoveAll(s =>
            {
                int bar = s?.LastIndexOf('|') ?? -1;
                return bar < 0 || !int.TryParse(s.Substring(bar + 1), out int t) || now - t > GodTouchMath.SeasonDays * GenDate.TicksPerDay;
            });
            while (seen.Count > MaxSeen) seen.RemoveAt(0);
        }

        [Unsaved] private int nextDue = int.MaxValue;

        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            GodTouch.Step(this, now);
            GodImpact.Step(this, now);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            GodImpact.Refresh(this);
            if (running.Count > 0) Due(Find.TickManager.TicksGame + 1);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref recent, "recent", LookMode.Value);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value);
            Scribe_Collections.Look(ref running, "running", LookMode.Deep);
            Scribe_Collections.Look(ref admired, "admired", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                last ??= new Dictionary<string, int>();
                recent ??= new List<string>();
                seen ??= new List<string>();
                running ??= new List<GodTouchRunning>();
                running.RemoveAll(r => r == null || r.Gone);
                admired ??= new List<Thing>();
                admired.RemoveAll(t => t == null || t.Destroyed);
            }
        }
    }

    /// <summary>
    /// What a god's occurrence does to those who saw it and to the map (the player, 15 November 2026: "for many of the gods
    /// occurences i don't see any real game impact ... also pawns seem to take no mood or heart/strain or other gameplay impact
    /// either"). Every occurrence someone of the colony saw (Gods.Remember and Gods.Moment, the seams every part already goes
    /// through, and an offering found taken) is noted here; a moment and the words written of it a moment later are one
    /// occurrence. Once the god's moment has played out:
    /// <list type="bullet">
    /// <item>each who saw it feels it by their belief (<see cref="Felt"/>: a mood, and the heart eased or strained);</item>
    /// <item>an occurrence with no effect of its own brings one of its god's own (<see cref="Follow"/>), small most of the time,
    /// notable now and then, great rarely, shown where it happened (Gods/Touch/GodTouchEffects.cs, GodTouchFx.cs).</item>
    /// </list>
    /// The unseen rule holds: what happens is told as what was seen, a god named only where the colony knows it was that god
    /// (Gods.CardName), and nothing shows a favour or a rule.
    /// </summary>
    public static class GodTouch
    {
        internal static int Now => Find.TickManager.TicksGame;

        internal static GodTouchBook Book => Gods.On ? GodTouchBook.Instance : null;

        /// <summary>One occurrence being gathered: its god, home, place, what it was like, its words keys, and who saw it.</summary>
        internal class Pending
        {
            public God god, taken;
            public Map map;
            public IntVec3 cell = IntVec3.Invalid;
            public TouchTone tone;
            public bool plain, coming;
            public readonly List<string> keys = new List<string>();
            public readonly List<Pawn> pawns = new List<Pawn>();
            public int at, due;
            /// <summary>The offering whose taking it is, with what it was worth (null for anything else).</summary>
            public TakenGift gift;
            /// <summary>How strongly what follows leans without a gift: a rite at the god's place, by how it went (GodTouch.Rite); else 1.</summary>
            public float lean = 1f;
            /// <summary>What was seen, in the words of the first that told it (the mood's "Why"; null when none were said).</summary>
            public string words;
            /// <summary>What each of its words keys was about, for the occurrence's own effect (<see cref="About"/>).</summary>
            public readonly Dictionary<string, TouchAbout> about = new Dictionary<string, TouchAbout>();
            /// <summary>Where each of its words keys happened (two signs of one god within the hours are one occurrence, each its own place).</summary>
            public readonly Dictionary<string, IntVec3> cellOf = new Dictionary<string, IntVec3>();
        }

        /// <summary>What occurrences were about, waiting for their words to be written (by home and key, two hours at most).</summary>
        private static readonly List<TouchAbout> abouts = new List<TouchAbout>();

        /// <summary>
        /// What an occurrence is about, said just before its words are written (Gods.Remember, Gods.Moment), for the effect of
        /// its own that follows it (GodImpactMath.Table): the one it is about (someone dying, a binger, a dead one), another
        /// (the second dreamer), a thing (a work of art, a cup), several people (the miners who dreamt, those who watched the
        /// sunrise), cells (a line of frost). Anything left unsaid is found from the place.
        /// </summary>
        public static void About(Map map, string key, Pawn pawn = null, Pawn other = null, Thing thing = null, IEnumerable<Pawn> pawns = null, IEnumerable<IntVec3> cells = null)
        {
            if (map == null || key.NullOrEmpty() || !GodImpactMath.Has(key)) return;
            var a = AboutOf(map, key, true);
            if (pawn != null) a.pawn = pawn;
            if (other != null) a.other = other;
            if (thing != null) a.thing = thing;
            if (pawns != null) a.pawns = pawns.Where(x => x != null).ToList();
            if (cells != null) a.cells = cells.ToList();
        }

        /// <summary>The one named in an occurrence's words ({WHO}), as GodsOnMapWords.Saw writes them.</summary>
        internal static void Saying(Map map, string key, Pawn who)
        {
            if (map == null || who == null || !GodImpactMath.Has(key)) return;
            AboutOf(map, key, true).who = who;
        }

        private static TouchAbout AboutOf(Map map, string key, bool make)
        {
            if (Current.Game != pendingGame)
            {
                pending.Clear();
                giftsComing.Clear();
                abouts.Clear();
                pendingGame = Current.Game;
            }
            int now = Now;
            abouts.RemoveAll(x => x.map == null || x.map.Disposed || now - x.at > GenDate.TicksPerHour * 2);
            var a = abouts.FirstOrDefault(x => x.map == map && x.key == key);
            if (a == null && make)
            {
                a = new TouchAbout { map = map, key = key, at = now };
                abouts.Add(a);
            }
            return a;
        }

        /// <summary>A gift taken before everyone's eyes, waiting for its moment's note to carry it (by god and home, two hours).</summary>
        private static readonly List<(God god, Map map, int at, TakenGift gift)> giftsComing = new List<(God, Map, int, TakenGift)>();

        private static readonly List<Pending> pending = new List<Pending>();
        private static Game pendingGame;

        /// <summary>While the touch itself plays (its moment, its words in the book), nothing it does is noted again.</summary>
        private static bool resolving;

        /// <summary>The wait after the last note before it is felt: the god's moment plays out first (a few seconds of game time).</summary>
        public const int SettleTicks = 400;

        /// <summary>An occurrence waits this long at most for a moment to finish playing.</summary>
        public const int LongestWait = GenDate.TicksPerHour;

        // ================= What it was like =================

        /// <summary>
        /// What an act of the gods was like: a blessing kind, an anger dark, an omen a sign, a coming or a god at work a sight,
        /// a departure or a mark nothing. The Hunger's signs and sights are dark; its gifts are still gifts.
        /// </summary>
        public static TouchTone ToneOf(GodActKind k, God g)
        {
            bool hunger = g != null && g.kind == GodKind.Hunger;
            switch (k)
            {
                case GodActKind.Blessing: return TouchTone.Kind;
                case GodActKind.Wrath: return TouchTone.Dark;
                case GodActKind.Omen: return hunger ? TouchTone.Dark : TouchTone.Sign;
                case GodActKind.Appear:
                case GodActKind.Act:
                case GodActKind.Reveal:
                    return hunger ? TouchTone.Dark : TouchTone.Sight;
                default: return TouchTone.None;
            }
        }

        /// <summary>The stronger of two: an anger over a kindness over a sight over a sign.</summary>
        internal static TouchTone Stronger(TouchTone a, TouchTone b) => Rank(a) >= Rank(b) ? a : b;

        private static int Rank(TouchTone t) => t == TouchTone.Dark ? 4 : t == TouchTone.Kind ? 3 : t == TouchTone.Sight ? 2 : t == TouchTone.Sign ? 1 : 0;

        // ================= Noted =================

        /// <summary>
        /// An act of the gods on the land (Gods.Remember): by its words key (GodAct.what) and who saw it (empty when its moment
        /// was just written, the moment's own note has them).
        /// </summary>
        public static void Note(GodAct act, List<Pawn> pawns, Map map, God taken)
        {
            if (act == null || resolving || !Gods.On) return;
            var g = Gods.ById(act.god);
            if (g == null || map == null || !Homes.IsHome(map)) return;
            if (GodTouchMath.WordsOnly(act.what)) return;
            bool coming = act.kind == GodActKind.Appear || act.kind == GodActKind.Reveal;
            Add(g, map, act.cell, ToneOf(act.kind, g), false, coming, act.what, pawns, taken, act.text);
        }

        /// <summary>A god's moment played (Gods.Moment): who saw it, plainly or not, under the god they took it for.</summary>
        public static void NoteMoment(PresenceSpec spec, List<Pawn> witnesses, bool plain, God taken)
        {
            if (spec?.god == null || resolving || !Gods.On || spec.map == null || !Homes.IsHome(spec.map)) return;
            var kind = PawnGods.KindOf(spec.kind);
            bool coming = spec.kind == PresenceKind.Appear || spec.kind == PresenceKind.Reveal;
            Add(spec.god, spec.map, spec.cell, ToneOf(kind, spec.god), plain, coming, spec.what, witnesses, taken, GodMarkMath.WithLeft(spec.text, spec.left));
        }

        /// <summary>
        /// An offering found taken (Gods/Worship/Offerings.cs): by whoever saw it go, or noticed the place empty by day; the
        /// god accepted it, a sight of it at work.
        /// </summary>
        public static void Taken(God g, Map map, IntVec3 cell, IEnumerable<Pawn> saw, TakenGift gift = null)
        {
            if (g == null || map == null || resolving || !Gods.On || !Homes.IsHome(map)) return;
            var pawns = saw?.Where(p => p != null).ToList() ?? new List<Pawn>();
            if (pawns.Count == 0) pawns = Gods.Witnesses(map, cell);
            if (gift != null) Coming(g, map, gift);
            Add(g, map, cell, TouchTone.Sight, false, false, "taken", pawns, g);
        }

        /// <summary>
        /// An offering about to be taken before everyone's eyes (Offerings.Settle): its worth goes with the moment that is
        /// about to be noted, so what follows leans by it and may be a return in kind (Gods/Touch/GodTouchReturn.cs).
        /// </summary>
        public static void Coming(God g, Map map, TakenGift gift)
        {
            if (g == null || map == null || gift == null) return;
            int now = Now;
            giftsComing.RemoveAll(c => c.god == g && c.map == map || now - c.at > GenDate.TicksPerHour * 2 || c.map == null || c.map.Disposed);
            giftsComing.Add((g, map, now, gift));
        }

        /// <summary>
        /// A rite at the god's place answered (Gods/Worship/GodRites.cs): a kindness felt by those who came, and what follows
        /// leans by how the rite went (<paramref name="lean"/>, as an offering's worth does), with the god's own effects and waits.
        /// </summary>
        public static void Rite(God g, Map map, IntVec3 cell, IEnumerable<Pawn> came, float lean, string words = null)
        {
            if (g == null || map == null || resolving || !Gods.On || !Homes.IsHome(map)) return;
            var pawns = came?.Where(p => p != null).ToList() ?? new List<Pawn>();
            if (pawns.Count == 0) return;
            Add(g, map, cell, TouchTone.Kind, false, false, "rite:" + g.Key, pawns, g, words, lean);
        }

        private static void Add(God g, Map map, IntVec3 cell, TouchTone tone, bool plain, bool coming, string key, List<Pawn> pawns, God taken,
            string words = null, float lean = 1f)
        {
            if (Current.Game != pendingGame)
            {
                pending.Clear();
                giftsComing.Clear();
                abouts.Clear();
                pendingGame = Current.Game;
            }
            int now = Now;
            var p = pending.FirstOrDefault(x => x.god == g && x.map == map && now - x.at < GenDate.TicksPerHour * 2);
            if (p == null)
            {
                p = new Pending { god = g, map = map, at = now, taken = taken };
                pending.Add(p);
            }
            if (p.words == null && !words.NullOrEmpty()) p.words = words;
            if (lean > p.lean) p.lean = lean;
            if (p.gift == null)
                for (int i = giftsComing.Count - 1; i >= 0; i--)
                    if (giftsComing[i].god == g && giftsComing[i].map == map && now - giftsComing[i].at <= GenDate.TicksPerHour * 2)
                    {
                        p.gift = giftsComing[i].gift;
                        giftsComing.RemoveAt(i);
                        break;
                    }
            if (!p.cell.IsValid && cell.IsValid && cell.InBounds(map)) p.cell = cell;
            p.tone = Stronger(p.tone, tone);
            p.plain |= plain;
            p.coming |= coming;
            if (plain) p.taken = g;
            else if (p.taken == null) p.taken = taken;
            if (!key.NullOrEmpty() && !p.keys.Contains(key)) p.keys.Add(key);
            if (!key.NullOrEmpty() && cell.IsValid && cell.InBounds(map) && !p.cellOf.ContainsKey(key)) p.cellOf[key] = cell;
            if (!key.NullOrEmpty() && !p.about.ContainsKey(key) && AboutOf(map, key, false) is TouchAbout about)
            {
                p.about[key] = about;
                abouts.Remove(about);
            }
            if (pawns != null)
                foreach (var x in pawns)
                    if (x != null && !p.pawns.Contains(x))
                        p.pawns.Add(x);
            p.due = now + SettleTicks;
            Book?.Due(p.due);
        }

        /// <summary>The book's due tick: each occurrence once its moment has played, felt and followed.</summary>
        internal static void Step(GodTouchBook b, int now)
        {
            if (Current.Game != pendingGame)
            {
                pending.Clear();
                pendingGame = Current.Game;
                return;
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (now < p.due)
                {
                    b.Due(p.due);
                    continue;
                }
                // Its moment still playing (the music, the card): wait for it, an hour at most.
                if (GodPresence.Busy && now - p.at < LongestWait)
                {
                    b.Due(now + 120);
                    continue;
                }
                pending.RemoveAt(i);
                if (p.map == null || p.map.Disposed || p.god == null) continue;
                try
                {
                    Resolve(b, p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: what an occurrence leaves behind", e);
                }
            }
        }

        private static void Resolve(GodTouchBook b, Pending p, int now)
        {
            var ours = p.pawns.Where(x => x != null && !x.Dead && x.Spawned && x.Map == p.map && Worship.Ours(x)).ToList();
            int home = Gods.HomeKey(p.map);
            if (p.tone != TouchTone.None && ours.Count > 0)
            {
                b.Saw(home, p.god.Key, now);
                bool own = p.keys.Any(GodTouchMath.HasOwnEffect);
                // A gift answered in kind (now and then, more for a rich one): seen as a kindness, and that is what follows it.
                bool returned = !own && p.gift != null && GodTouchReturn.Try(b, p, ours, home, now);
                if (returned) p.tone = TouchTone.Kind;
                Felt(ours, p);
                if (!own && !returned) Follow(b, p, ours, home, now);
            }
            // The occurrence's own effect (GodImpactMath.Table): on the place whether or not anyone of the colony saw it, on
            // those it names, or on those who saw it.
            foreach (var impact in p.keys.Where(GodImpactMath.Has).ToList()) GodImpact.Follow(b, p, ours, impact);
        }

        /// <summary>Does something of the touch's own without noting it again as an occurrence.</summary>
        internal static bool Quietly(Func<bool> doing)
        {
            bool was = resolving;
            resolving = true;
            try
            {
                return doing();
            }
            finally
            {
                resolving = was;
            }
        }

        // ================= Felt =================

        /// <summary>
        /// The mood's "Why": what was seen, and whose doing this one holds it was, by the name they know that god by (the god
        /// the colony took it for, possibly wrong), or that they don't know (the player, 17 November 2026: "if they don't know
        /// who it was, then, they should guess or say they don't know"). Never the true god's name unless it is theirs for it.
        /// </summary>
        internal static string WhyFelt(Pawn x, Pending p)
        {
            string name = p.taken != null ? GodLore.NameOf(p.taken, x) : null;
            string whose = name.NullOrEmpty()
                ? "CC_GodTouch_Why_Unknown".Translate(x.Named("PAWN")).Resolve()
                : "CC_GodTouch_Why_Guess".Translate(x.Named("PAWN"), name.Named("GOD")).Resolve();
            return GodTouchMath.Why(p.words, whose);
        }

        /// <summary>How someone of the colony stands toward the god they took it for (null: nothing they can name).</summary>
        public static TouchStance StanceOf(Pawn p, God taken)
        {
            if (Worship.Scoffer(p)) return TouchStance.Scoffer;
            if (taken == null) return TouchStance.Ordinary;
            if (PawnGods.Dreads(p, taken)) return TouchStance.Dreads;
            return PawnGods.HoldOf(p, taken) >= PawnGodsMath.HoldsFrom ? TouchStance.Devoted : TouchStance.Ordinary;
        }

        /// <summary>
        /// Each who saw it, by their belief: a believer glad of their god's kindness (the more, seen plainly) and comforted, one
        /// in dread of it afraid and strained, someone of no strong belief awed or uneasy, a scoffer unmoved. The heart through
        /// its own caps (the day's small joys, dread).
        /// </summary>
        internal static void Felt(List<Pawn> ours, Pending p)
        {
            foreach (var x in ours)
            {
                if (x.needs?.mood == null) continue;
                var stance = StanceOf(x, p.taken);
                var (mood, stage) = GodTouchMath.MoodFor(stance, p.tone, p.plain);
                var def = ThoughtOf(mood);
                if (def != null)
                {
                    // Their god's kindness seen takes the place of the smaller "my god showed itself" the belief round gave them.
                    if (mood == TouchMood.Blessed) x.needs.mood.thoughts.memories.RemoveMemoriesOfDef(PawnGodsDefOf.Rimfolk_Belief_GodActed);
                    CouncilGameComponent.GiveThought(x, def, stage: stage, cause: WhyFelt(x, p));
                }
                string why = def?.stages != null && stage < def.stages.Count ? def.stages[stage]?.label : null;
                why ??= GodTouchDefOf.Rimfolk_GodTouch_Wonder?.stages?[0]?.label;
                float ease = GodTouchMath.EaseFor(stance, p.tone, p.plain);
                if (ease > 0f) HeartLogic.EaseJoys(x, ease, why);
                float strain = GodTouchMath.StrainFor(stance, p.tone, p.plain);
                if (strain > 0f) HeartLogic.Build(x, StrainSource.Dread, strain, why);
            }
        }

        private static ThoughtDef ThoughtOf(TouchMood m)
        {
            switch (m)
            {
                case TouchMood.Blessed: return GodTouchDefOf.Rimfolk_GodTouch_Blessed;
                case TouchMood.Acted: return PawnGodsDefOf.Rimfolk_Belief_GodActed;
                case TouchMood.Wonder: return GodTouchDefOf.Rimfolk_GodTouch_Wonder;
                case TouchMood.Uneasy: return GodTouchDefOf.Rimfolk_GodTouch_Uneasy;
                case TouchMood.Afraid: return GodTouchDefOf.Rimfolk_GodTouch_Afraid;
                default: return null;
            }
        }

        // ================= What follows =================

        /// <summary>
        /// One of the god's own effects for an occurrence with none of its own: small most of the time, notable now and then,
        /// great rarely (GodTouchMath.Tier, each with its waits), leaned away from what it did lately at the home, played out
        /// where it happened. The same occurrence again soon leaves the moods only.
        /// </summary>
        internal static void Follow(GodTouchBook b, Pending p, List<Pawn> ours, int home, int now)
        {
            string god = p.god.Key;
            // A gift leans what follows its taking by what it was worth and how well it suited the god (OfferingGiftMath.Lean).
            float lean = p.gift != null ? OfferingGiftMath.Lean(p.gift.worth, p.gift.fit, p.gift.precious) : p.lean;
            bool precious = p.gift?.precious == true;
            var gapKeys = p.keys.Select(k => GodTouchMath.GapKey(k, god)).Where(k => k != null).ToList();
            foreach (var k in gapKeys)
                if (b.DaysSince("k|" + k + "|" + home, now) < GodTouchMath.GapDays(k, lean, precious))
                    return;
            var tier = Forced ?? GodTouchMath.TierFor(p.tone, GodTouchMath.Big(p.tone, p.coming, p.plain), p.plain, Rand.Value, Rand.Value,
                b.DaysSince("s|" + god + "|" + home, now), b.DaysSince("n|" + god + "|" + home, now), b.DaysSince("nc|" + home, now),
                b.DaysSince("g|" + god + "|" + home, now), b.DaysSince("gc|" + home, now), lean);
            if (tier == TouchTier.None) return;
            var ctx = new TouchContext(p.god, p.map, Place(p, ours), ours, p.taken, p.plain, tier, p.tone == TouchTone.Dark);
            var entries = GodTouchMath.Of(god);
            var recent = b.RecentOf(home, god);
            var tried = new HashSet<TouchEffect>();
            // A great one that can't happen here falls to a notable one only where a notable one's waits are past too.
            bool notableOk = Forced.HasValue || GodTouchMath.NotableWaitPast(b.DaysSince("n|" + god + "|" + home, now), b.DaysSince("nc|" + home, now), lean);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var pick = ForcedEffect.HasValue && attempt == 0
                    ? entries.FirstOrDefault(e => e.effect == ForcedEffect.Value)
                    : GodTouchMath.Pick(entries, tier, ctx.dark, recent, e => !tried.Contains(e.effect) && (e.tier != TouchTier.Notable || notableOk) && GodTouchEffects.Can(e, ctx),
                        Rand.Value);
                if (pick.effect == TouchEffect.None) return;
                tried.Add(pick.effect);
                ctx.tier = pick.tier;
                bool done;
                resolving = true;
                try
                {
                    done = GodTouchEffects.Do(pick, ctx);
                }
                finally
                {
                    resolving = false;
                }
                if (!done) continue;
                b.Did(home, god, pick.effect, now);
                foreach (var k in gapKeys) b.Set("k|" + k + "|" + home, now);
                switch (pick.tier)
                {
                    case TouchTier.Great:
                        b.Set("g|" + god + "|" + home, now);
                        b.Set("gc|" + home, now);
                        // Once in two colony-years at most, the god's rarest gift of the player's mods is left there too (GodRareGifts).
                        GodRareGifts.AfterGreat(b, ctx);
                        break;
                    case TouchTier.Notable:
                        b.Set("n|" + god + "|" + home, now);
                        b.Set("nc|" + home, now);
                        break;
                    default:
                        b.Set("s|" + god + "|" + home, now);
                        break;
                }
                return;
            }
        }

        /// <summary>Where it happened, or where whoever saw it stands for what the whole sky showed.</summary>
        internal static IntVec3 Place(Pending p, List<Pawn> ours)
        {
            if (p.cell.IsValid && p.cell.InBounds(p.map)) return p.cell;
            var near = ours.FirstOrDefault(x => x.Spawned && x.Map == p.map);
            return near?.Position ?? p.map.Center;
        }

        /// <summary>
        /// Remembers what followed among the god's acts, and in the colony's book as those who saw it saw it, under the god
        /// they took it for (which may be the wrong one). Written straight into the book: the occurrence's own moment a few
        /// moments before would otherwise keep it out (Gods.Remember writes one moment once). Nothing noted again.
        /// </summary>
        internal static void Write(TouchContext c, GodActKind kind, string words)
        {
            if (words.NullOrEmpty()) return;
            bool was = resolving;
            resolving = true;
            try
            {
                Gods.Remember(new GodAct
                {
                    god = c.god.id, home = Gods.HomeKey(c.map), tick = Now, kind = kind, cell = c.cell, text = words,
                    witnesses = c.saw.Select(p => p.LabelShort).ToList(), booked = true
                });
                if (c.saw.Count > 0) GodLore.Saw(c.saw, c.taken, words);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what followed, in the book", e);
            }
            finally
            {
                resolving = was;
            }
        }

        /// <summary>Plays a moment for a great effect without noting it again.</summary>
        internal static bool Moment(PresenceSpec spec)
        {
            bool was = resolving;
            resolving = true;
            try
            {
                return GodPresenceSeam.Call(spec);
            }
            finally
            {
                resolving = was;
            }
        }

        // ================= An offering taken before everyone's eyes, a god's turn =================

        /// <summary>A god may take an offering before everyone's eyes at this home now (a month apart, any god a week and a day).</summary>
        public static bool PlainTakingAllowed(God g, Map map)
        {
            var b = Book;
            if (b == null || g == null || map == null) return true;
            int home = Gods.HomeKey(map), now = Now;
            return GodTouchMath.PlainTaking(b.DaysSince("p|" + g.Key + "|" + home, now), b.DaysSince("pc|" + home, now));
        }

        /// <summary>It did.</summary>
        public static void PlainTaken(God g, Map map)
        {
            var b = Book;
            if (b == null || g == null || map == null) return;
            int home = Gods.HomeKey(map), now = Now;
            b.Set("p|" + g.Key + "|" + home, now);
            b.Set("pc|" + home, now);
        }

        /// <summary>A god's own happenings at a home, the less likely the more of its occurrences were seen there lately (GodTouchMath.Turn).</summary>
        public static float Turn(God g, Map map)
        {
            var b = Book;
            if (b == null || g == null || map == null) return 1f;
            return GodTouchMath.Turn(b.SeenOf(Gods.HomeKey(map), g.Key, Now, GodTouchMath.TurnDays));
        }

        // ================= Dev mode =================

        /// <summary>The dev action's tier and effect while it fires (every chance and wait gives way).</summary>
        internal static TouchTier? Forced;
        internal static TouchEffect? ForcedEffect;

        /// <summary>
        /// Dev mode: an occurrence of the table's at <paramref name="cell"/> (GodImpactMath.Table), seen by everyone near, with
        /// its own effect now (what it is about found from the place and the selection: GodImpact.DevAbout). False when it had
        /// nothing to do there.
        /// </summary>
        internal static bool DevImpact(Map map, IntVec3 cell, string key)
        {
            var b = Book;
            if (b == null || map == null || !GodImpactMath.Has(key)) return false;
            var g = Gods.ByKey(GodImpact.DevGod(key)) ?? Gods.All.FirstOrDefault(x => x != null && x.kind != GodKind.Dead);
            if (g == null) return false;
            var pawns = Gods.Witnesses(map, IntVec3.Invalid).Where(x => !cell.IsValid || x.Position.InHorDistOf(cell, 40f)).ToList();
            if (pawns.Count == 0) pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            var p = new Pending { god = g, map = map, cell = cell, tone = TouchTone.Sign, taken = g, at = Now, due = Now };
            p.keys.Add(key);
            p.cellOf[key] = cell;
            p.pawns.AddRange(pawns);
            p.about[key] = GodImpact.DevAbout(map, cell, key, pawns);
            var ours = pawns.Where(x => Worship.Ours(x)).ToList();
            return GodImpact.Follow(b, p, ours, key);
        }

        /// <summary>Dev mode: an occurrence of <paramref name="g"/> at <paramref name="cell"/> seen by everyone near, felt and followed now.</summary>
        internal static void DevNow(God g, Map map, IntVec3 cell, TouchTone tone, TouchTier tier, TouchEffect? effect, bool plain)
        {
            var b = Book;
            if (b == null || g == null || map == null) return;
            var pawns = Gods.Witnesses(map, IntVec3.Invalid).Where(x => !cell.IsValid || x.Position.InHorDistOf(cell, 40f)).ToList();
            if (pawns.Count == 0) pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            var p = new Pending { god = g, map = map, cell = cell, tone = tone, plain = plain, coming = plain, taken = g, at = Now, due = Now };
            p.pawns.AddRange(pawns);
            Forced = tier;
            ForcedEffect = effect;
            try
            {
                var ours = p.pawns.Where(x => Worship.Ours(x)).ToList();
                Felt(ours, p);
                Follow(b, p, ours, Gods.HomeKey(map), Now);
            }
            finally
            {
                Forced = null;
                ForcedEffect = null;
            }
        }
    }

    /// <summary>
    /// An offering as what follows its taking needs it (Gods/Worship/Offerings.cs): what it was worth and how well it suited
    /// the god, whether it was something precious given with the colony's leave, what it was in words, and its line in the
    /// colony's book (to add what was found in its place).
    /// </summary>
    public sealed class TakenGift
    {
        public float worth, fit;
        public bool precious;
        public string label;
        public LoreGift lore;
    }

    /// <summary>What an effect needs to know: the god, the home, the place, who saw it, the god they took it for, how plainly, how strong, which side.</summary>
    public class TouchContext
    {
        public readonly God god, taken;
        public readonly Map map;
        public readonly IntVec3 cell;
        public readonly List<Pawn> saw;
        public readonly bool plain, dark;
        public TouchTier tier;

        public TouchContext(God god, Map map, IntVec3 cell, List<Pawn> saw, God taken, bool plain, TouchTier tier, bool dark)
        {
            this.god = god;
            this.map = map;
            this.cell = cell;
            this.saw = saw ?? new List<Pawn>();
            this.taken = taken;
            this.plain = plain;
            this.tier = tier;
            this.dark = dark;
        }
    }

    [DefOf]
    public static class GodTouchDefOf
    {
        public static ThoughtDef Rimfolk_GodTouch_Blessed;
        public static ThoughtDef Rimfolk_GodTouch_Wonder;
        public static ThoughtDef Rimfolk_GodTouch_Uneasy;
        public static ThoughtDef Rimfolk_GodTouch_Afraid;
        public static ThoughtDef Rimfolk_GodTouch_Remembered;
        public static HediffDef Rimfolk_GodTouch_Hardy;
        public static HediffDef Rimfolk_GodTouch_SunWarmed;
        public static HediffDef Rimfolk_GodTouch_Ward;
        public static HediffDef Rimfolk_GodTouch_Sleepless;

        static GodTouchDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GodTouchDefOf));
    }
}
