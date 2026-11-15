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
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref recent, "recent", LookMode.Value);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                last ??= new Dictionary<string, int>();
                recent ??= new List<string>();
                seen ??= new List<string>();
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
        }

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
            Add(g, map, act.cell, ToneOf(act.kind, g), false, coming, act.what, pawns, taken);
        }

        /// <summary>A god's moment played (Gods.Moment): who saw it, plainly or not, under the god they took it for.</summary>
        public static void NoteMoment(PresenceSpec spec, List<Pawn> witnesses, bool plain, God taken)
        {
            if (spec?.god == null || resolving || !Gods.On || spec.map == null || !Homes.IsHome(spec.map)) return;
            var kind = PawnGods.KindOf(spec.kind);
            bool coming = spec.kind == PresenceKind.Appear || spec.kind == PresenceKind.Reveal;
            Add(spec.god, spec.map, spec.cell, ToneOf(kind, spec.god), plain, coming, spec.what, witnesses, taken);
        }

        /// <summary>
        /// An offering found taken (Gods/Worship/Offerings.cs): by whoever saw it go, or noticed the place empty by day; the
        /// god accepted it, a sight of it at work.
        /// </summary>
        public static void Taken(God g, Map map, IntVec3 cell, IEnumerable<Pawn> saw)
        {
            if (g == null || map == null || resolving || !Gods.On || !Homes.IsHome(map)) return;
            var pawns = saw?.Where(p => p != null).ToList() ?? new List<Pawn>();
            if (pawns.Count == 0) pawns = Gods.Witnesses(map, cell);
            Add(g, map, cell, TouchTone.Sight, false, false, "taken", pawns, g);
        }

        private static void Add(God g, Map map, IntVec3 cell, TouchTone tone, bool plain, bool coming, string key, List<Pawn> pawns, God taken)
        {
            if (Current.Game != pendingGame)
            {
                pending.Clear();
                pendingGame = Current.Game;
            }
            int now = Now;
            var p = pending.FirstOrDefault(x => x.god == g && x.map == map && now - x.at < GenDate.TicksPerHour * 2);
            if (p == null)
            {
                p = new Pending { god = g, map = map, at = now, taken = taken };
                pending.Add(p);
            }
            if (!p.cell.IsValid && cell.IsValid && cell.InBounds(map)) p.cell = cell;
            p.tone = Stronger(p.tone, tone);
            p.plain |= plain;
            p.coming |= coming;
            if (plain) p.taken = g;
            else if (p.taken == null) p.taken = taken;
            if (!key.NullOrEmpty() && !p.keys.Contains(key)) p.keys.Add(key);
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
            if (p.tone == TouchTone.None) return;
            var ours = p.pawns.Where(x => x != null && !x.Dead && x.Spawned && x.Map == p.map && Worship.Ours(x)).ToList();
            if (ours.Count == 0) return;
            int home = Gods.HomeKey(p.map);
            b.Saw(home, p.god.Key, now);
            Felt(ours, p);
            if (p.keys.Any(GodTouchMath.HasOwnEffect)) return;
            Follow(b, p, ours, home, now);
        }

        // ================= Felt =================

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
                    CouncilGameComponent.GiveThought(x, def, stage: stage);
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
            var gapKeys = p.keys.Select(k => GodTouchMath.GapKey(k, god)).Where(k => k != null).ToList();
            foreach (var k in gapKeys)
                if (b.DaysSince("k|" + k + "|" + home, now) < GodTouchMath.SameKeyGapDays(k))
                    return;
            var tier = Forced ?? GodTouchMath.Tier(p.tone, GodTouchMath.Big(p.tone, p.coming, p.plain), p.plain, Rand.Value, Rand.Value,
                b.DaysSince("s|" + god + "|" + home, now), b.DaysSince("n|" + god + "|" + home, now), b.DaysSince("nc|" + home, now),
                b.DaysSince("g|" + god + "|" + home, now), b.DaysSince("gc|" + home, now));
            if (tier == TouchTier.None) return;
            var ctx = new TouchContext(p.god, p.map, Place(p, ours), ours, p.taken, p.plain, tier, p.tone == TouchTone.Dark);
            var entries = GodTouchMath.Of(god);
            var recent = b.RecentOf(home, god);
            var tried = new HashSet<TouchEffect>();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var pick = ForcedEffect.HasValue && attempt == 0
                    ? entries.FirstOrDefault(e => e.effect == ForcedEffect.Value)
                    : GodTouchMath.Pick(entries, tier, ctx.dark, recent, e => !tried.Contains(e.effect) && GodTouchEffects.Can(e, ctx), Rand.Value);
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
        private static IntVec3 Place(Pending p, List<Pawn> ours)
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
