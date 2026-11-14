using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A god's coming planned for a home: what comes, which god, when, and its omens in the days before.</summary>
    public class GodComing : IExposable
    {
        public ComingKind kind;
        public int god = -1;
        public Map map;
        /// <summary>The tick it comes from (the stranger's question waits for the letters' gap after it).</summary>
        public int at;
        /// <summary>Times it waited (no room for a question, a fight on the map): given up after a day of it.</summary>
        public int waited;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind", ComingKind.None);
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref waited, "waited");
        }
    }

    /// <summary>One omen waiting to show (GodOmens.Before): of which god, where, which kind, from when.</summary>
    public class GodOmenNote : IExposable
    {
        public int god = -1;
        public Map map;
        public OmenKind kind;
        public int at;
        public int tries;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref kind, "kind", OmenKind.Calm);
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref tries, "tries");
        }
    }

    /// <summary>
    /// A mark a god left on the land, remembered (Gods/OnMap/GodMarks.cs): which god, at which home, where, when, what it
    /// did in a few words ("came to the gate as a stranger and blessed the fields"), how often someone has gone to it.
    /// </summary>
    public class GodMarkNote : IExposable
    {
        public int god = -1;
        public int home = HomesMath.None;
        public IntVec3 cell = IntVec3.Invalid;
        public int tick;
        public string words;
        public PresenceKind kind;
        public int visits;
        public int lastVisit = -1;
        /// <summary>The god showed itself plainly there (drawn near: GodNear), so what is said there says so; else it was felt.</summary>
        public bool plain;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref words, "words");
            Scribe_Values.Look(ref kind, "kind", PresenceKind.Appear);
            Scribe_Values.Look(ref visits, "visits");
            Scribe_Values.Look(ref lastVisit, "lastVisit", -1);
            Scribe_Values.Look(ref plain, "plain");
        }
    }

    /// <summary>
    /// The gods on the map, saved with the game (docs/design/gods.md, "On the map"): the coming planned and its omens, the
    /// stranger at the gate while they stay (GodStranger.cs), the sacred animal while it walks the land (SacredAnimal.cs),
    /// when each last came, when a god last answered in the weather at each home (GodWeather.cs), and the marks the gods
    /// left (GodMarks.cs). Kept in the gods' own day and hour (GodBook.Daily, Hourly); a stay's short steps (a farewell, a
    /// lead, a lamp guttering) run on their own due tick, and nothing at all with none under way.
    /// </summary>
    public class GodsOnMapBook : GameComponent
    {
        public GodComing coming;
        public List<GodOmenNote> omens = new List<GodOmenNote>();
        public StrangerStay stranger;
        public SacredStay beast;
        public List<GodMarkNote> marks = new List<GodMarkNote>();
        /// <summary>When a god last answered in the weather, by home (two lists side by side).</summary>
        public List<int> weatherHomes = new List<int>(), weatherTicks = new List<int>();
        /// <summary>The newest act of the gods already looked at for an answer in the weather.</summary>
        public int actsSeen = -1;
        public int lastAny = -999999, lastStranger = -999999, lastAnimal = -999999, lastVisit = -999999, lastRising = -999999;
        /// <summary>
        /// What the stranger remembers of the colony (it never forgets the answer): which god came last, how often, and how it
        /// was kept the last time; and the good welcome's late gift, when it is due, from whom and at which home.
        /// </summary>
        public int strangerGod = -1, strangerVisits;
        public Treatment strangerLast = Treatment.Fair;
        public int lateGiftAt = -1, lateGiftGod = -1, lateGiftHome = HomesMath.None;
        /// <summary>
        /// The sleeper under the mountain (GodWaking): how many times the rock has rumbled over the colony's deep mining
        /// and when last, when it last woke, and its true form while it is up on the land (until it goes).
        /// </summary>
        public int deepWarnings, lastDeep = -999999, lastWake = -999999;
        public Pawn woken;
        public int wokenGod = -1, wokenUntil = -1;
        /// <summary>A god's true form surfaced from the land (GodRising: Murmur in the marsh), and when it sinks again.</summary>
        public Pawn risen;
        public int risenUntil = -1;
        /// <summary>When the moon last returned a deed by night (GodReturn).</summary>
        public int lastReturn = -999999;
        /// <summary>
        /// A short memory of the gods' last happenings, newest last ("return|Nightmare|1234": GodPaceMath.Note, at most
        /// GodPaceMath.Memory), so the next one leans to a different form and a story that comes back takes its next step.
        /// </summary>
        public List<string> lately = new List<string>();

        /// <summary>At most this many omens wait at once.</summary>
        public const int MaxOmens = 12;

        private static GodsOnMapBook cached;
        private static Game cachedGame;

        public GodsOnMapBook(Game game)
        {
        }

        public static GodsOnMapBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodsOnMapBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Due steps =================

        [Unsaved] private int nextDue = int.MaxValue;

        /// <summary>Something under way has a step due at <paramref name="tick"/> (the earliest is kept).</summary>
        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            int now = Now;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            Safe("the stranger's step", () => GodStranger.Step(this, now));
            Safe("the sacred animal's step", () => SacredAnimal.Step(this, now));
            Safe("a slain beast's body", SacredAnimal.Ash);
            Safe("a guttering light", () => GodOmens.Step(this, now));
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            nextDue = Now + 1;
        }

        // ================= The gods' day and hour =================

        /// <summary>The gods' day (GodBook.Daily): a coming planned, the marks kept, a mark visited.</summary>
        public static void Daily(int now)
        {
            var b = Instance;
            if (b == null || !Gods.On) return;
            Safe("a coming", () => GodComings.Daily(b, now));
            Safe("a late gift", () => GodGifts.LateGift(b, now));
            Safe("the marks", () => GodMarks.Daily(b, now));
            Safe("a new grave kept", () => MogsWays.Daily(now));
        }

        /// <summary>The gods' hour (GodBook.Hourly): the omens, the coming, the stranger, the animal, the weather, the marks.</summary>
        public static void Hourly(int now)
        {
            var b = Instance;
            if (b == null || !Gods.On) return;
            Safe("the omens", () => GodOmens.Hourly(b, now));
            Safe("the coming", () => GodComings.Hourly(b, now));
            Safe("the stranger", () => GodStranger.Hourly(b, now));
            Safe("the sacred animal", () => SacredAnimal.Hourly(b, now));
            Safe("the sleeper", () => GodWaking.Hourly(b, now));
            Safe("the risen", () => GodRising.Hourly(b, now));
            Safe("the night kept", () => GodOmens.NightWatch(b, now));
            Safe("the moon's return", () => GodReturn.Hourly(b, now));
            Safe("the weather", () => GodWeather.Hourly(b, now));
            Safe("the marks spoken of", () => GodMarks.Hourly(b, now));
            Safe("the gods' own presences", () => GodPresenceOwn.Hourly(now));
            Safe("the keeper of the dead", () => MogsWays.Hourly(now));
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods on the map: " + what, e);
            }
        }

        // ================= The weather's last answers =================

        public int LastWeather(int home)
        {
            int i = weatherHomes.IndexOf(home);
            return i >= 0 && i < weatherTicks.Count ? weatherTicks[i] : -999999;
        }

        public void NoteWeather(int home, int tick)
        {
            int i = weatherHomes.IndexOf(home);
            if (i >= 0 && i < weatherTicks.Count) weatherTicks[i] = tick;
            else
            {
                weatherHomes.Add(home);
                weatherTicks.Add(tick);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref coming, "coming");
            Scribe_Collections.Look(ref omens, "omens", LookMode.Deep);
            Scribe_Deep.Look(ref stranger, "stranger");
            Scribe_Deep.Look(ref beast, "beast");
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            Scribe_Collections.Look(ref weatherHomes, "weatherHomes", LookMode.Value);
            Scribe_Collections.Look(ref weatherTicks, "weatherTicks", LookMode.Value);
            Scribe_Values.Look(ref actsSeen, "actsSeen", -1);
            Scribe_Values.Look(ref lastAny, "lastAny", -999999);
            Scribe_Values.Look(ref lastStranger, "lastStranger", -999999);
            Scribe_Values.Look(ref lastAnimal, "lastAnimal", -999999);
            Scribe_Values.Look(ref lastVisit, "lastVisit", -999999);
            Scribe_Values.Look(ref lastRising, "lastRising", -999999);
            Scribe_Values.Look(ref strangerGod, "strangerGod", -1);
            Scribe_Values.Look(ref strangerVisits, "strangerVisits");
            Scribe_Values.Look(ref strangerLast, "strangerLast", Treatment.Fair);
            Scribe_Values.Look(ref lateGiftAt, "lateGiftAt", -1);
            Scribe_Values.Look(ref lateGiftGod, "lateGiftGod", -1);
            Scribe_Values.Look(ref lateGiftHome, "lateGiftHome", HomesMath.None);
            Scribe_Values.Look(ref deepWarnings, "deepWarnings");
            Scribe_Values.Look(ref lastDeep, "lastDeep", -999999);
            Scribe_Values.Look(ref lastWake, "lastWake", -999999);
            Scribe_References.Look(ref woken, "woken");
            Scribe_Values.Look(ref wokenGod, "wokenGod", -1);
            Scribe_Values.Look(ref wokenUntil, "wokenUntil", -1);
            Scribe_References.Look(ref risen, "risen");
            Scribe_Values.Look(ref risenUntil, "risenUntil", -1);
            Scribe_Values.Look(ref lastReturn, "lastReturn", -999999);
            Scribe_Collections.Look(ref lately, "lately", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lately ??= new List<string>();
                lately.RemoveAll(x => x == null);
                omens ??= new List<GodOmenNote>();
                omens.RemoveAll(o => o == null || o.map == null);
                marks ??= new List<GodMarkNote>();
                marks.RemoveAll(m => m == null);
                weatherHomes ??= new List<int>();
                weatherTicks ??= new List<int>();
                if (weatherHomes.Count != weatherTicks.Count)
                {
                    weatherHomes.Clear();
                    weatherTicks.Clear();
                }
                if (coming != null && (coming.map == null || coming.kind == ComingKind.None)) coming = null;
                if (stranger != null && stranger.x == null) stranger = null;
                if (beast != null && beast.a == null) beast = null;
                nextDue = 0;
            }
        }
    }

    /// <summary>
    /// When a god sets out for the colony, and as what (GodsOnMapMath.Due): planned in the gods' day, a few days ahead, with
    /// its omens in the days before (GodOmens.Before); in the hour it comes, the stranger's question once the letters'
    /// gap allows it (Tell.CanAsk) and the map is quiet, the sacred animal once the map is quiet. Never two at once, and never
    /// announced: only the omens say something is coming.
    /// </summary>
    public static class GodComings
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A coming is planned, waiting, or under way (a stranger staying, an animal on the land).</summary>
        public static bool Busy(GodsOnMapBook b) => b != null && (b.coming != null || b.stranger != null || b.beast != null);

        internal static void Daily(GodsOnMapBook b, int now)
        {
            // Mog's season: Ennu may come to the gate while she stays, and she will know it (SacredAnimal.Stare).
            if (b.coming == null && b.stranger == null && SacredAnimal.IsMog(b.beast))
            {
                var ennu = Gods.All.FirstOrDefault(g => g != null && GodShapes.LineTag(g) == "ennu");
                var at = b.beast.map;
                if (ennu != null && at != null && Homes.IsHome(at)
                    && GodsOnMapMath.GuestInHerSeason(Days(now, b.lastStranger), GodStranger.CanCome(at), Rand.Value))
                    Plan(b, ComingKind.Stranger, at, ennu, Rand.RangeInclusive(1, 2));
                return;
            }
            if (Busy(b)) return;
            var home = Homes.Seat ?? Homes.All.FirstOrDefault();
            if (home == null || !home.IsPlayerHome) return;
            int day = GenDate.DaysPassedSinceSettle;
            // Very rarely, a god with no body rises from the land for an hour (Hollin), after its own long gap.
            // The one drawn nearest is the likelier (Murmur in a long drought).
            var riser = Gods.All.Where(g => g != null && GodShapes.Rises(g)).RandomElementByWeightWithFallback(g => 0.25f + GodNear.Of(g, home));
            if (riser != null && GodsOnMapMath.RisingDue(day, Days(now, b.lastAny), Days(now, b.lastRising), true, Rand.Value, GodNear.Of(riser, home)))
            {
                Plan(b, ComingKind.Rising, home, riser, Rand.RangeInclusive(GodsOnMapMath.OmenDaysMin, GodsOnMapMath.OmenDaysMax));
                return;
            }
            bool strangerCan = GodStranger.CanCome(home);
            bool animalCan = SacredAnimal.AnyFor(home);
            var kind = GodsOnMapMath.Due(day, Days(now, b.lastAny), Days(now, b.lastStranger), Days(now, b.lastAnimal), strangerCan, animalCan, Rand.Value, Rand.Value);
            if (kind == ComingKind.None) return;
            Plan(b, kind, home, null, Rand.RangeInclusive(GodsOnMapMath.OmenDaysMin, GodsOnMapMath.OmenDaysMax));
        }

        private static int Days(int now, int since) => since < -900000 ? 99999 : (now - since) / GenDate.TicksPerDay;

        /// <summary>
        /// A coming of <paramref name="kind"/> planned at <paramref name="home"/> in <paramref name="days"/> days (0: within
        /// the hour, no omens), by <paramref name="god"/> or one chosen for it. False when no god fits.
        /// </summary>
        public static bool Plan(GodsOnMapBook b, ComingKind kind, Map home, God god, int days)
        {
            if (b == null || home == null || kind == ComingKind.None) return false;
            god ??= kind == ComingKind.Stranger ? GodStranger.PickGod(home) : kind == ComingKind.Animal ? SacredAnimal.PickGod(home)
                : Gods.All.Where(g => g != null && GodShapes.Rises(g)).RandomElementWithFallback();
            if (god == null) return false;
            int now = Now;
            int at = days <= 0 ? now : now + days * GenDate.TicksPerDay + Rand.Range(-GenDate.TicksPerHour * 3, GenDate.TicksPerHour * 3);
            b.coming = new GodComing { kind = kind, god = god.id, map = home, at = at };
            if (days > 0) GodOmens.Before(god, home, days);
            return true;
        }

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var c = b.coming;
            if (c == null || now < c.at) return;
            var god = Gods.ById(c.god);
            if (god == null || c.map == null || !Homes.IsHome(c.map))
            {
                b.coming = null;
                return;
            }
            bool came;
            if (MapFacts.ThreatOn(c.map)) came = false;
            else if (c.kind == ComingKind.Stranger) came = GodStranger.Come(b, c, god);
            else if (c.kind == ComingKind.Rising) came = GodRising.Rise(c.map, god);
            else came = SacredAnimal.Come(b, c, god);
            if (came)
            {
                b.coming = null;
                b.lastAny = now;
                if (c.kind == ComingKind.Stranger) b.lastStranger = now;
                else if (c.kind == ComingKind.Rising) b.lastRising = now;
                else b.lastAnimal = now;
                return;
            }
            // Waits for the letters' gap and a quiet map, a day at most; then it doesn't come this time.
            if (++c.waited > 24)
            {
                b.coming = null;
                b.lastAny = now - (GodsOnMapMath.AnyGapDays - 10) * GenDate.TicksPerDay;
            }
        }
    }
}
