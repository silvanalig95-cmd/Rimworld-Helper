using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' hidden state, saved with the game (docs/design/gods.md, "the contract"): the nine (made from their defs by
    /// <see cref="Pantheon"/>; an older save's generated gods dropped on its first load), the ties between them (the defs'),
    /// the colony's standing with each god at each home (it drifts back toward unknown from when it was last set, worked out
    /// when read), the acts of the gods (each with the colonists who saw it), its colonists' vows, and the day's deeds.
    /// None of it is shown: the gods are an unseen presence, and the player learns of them only what the colonists see,
    /// hear and believe (Gods/GodLore.cs, the colony's own book). Kept in the region's day (the nine kept right as peoples
    /// come and go, the day's deeds, the stories and names worked out) and hour (visitors: the colony learns their gods;
    /// the gods' hands remarked on; talk of the gods). Everything goes through <see cref="Gods"/>.
    /// </summary>
    public class GodBook : GameComponent
    {
        public List<God> gods = new List<God>();
        public List<GodTie> ties = new List<GodTie>();
        public List<GodStanding> standings = new List<GodStanding>();
        public List<GodAct> acts = new List<GodAct>();
        public List<GodVow> vows = new List<GodVow>();
        public List<GodDeed> deeds = new List<GodDeed>();
        public int nextId = 1;
        /// <summary>The pantheon was built for this world (a save from before is built on its first load).</summary>
        public bool built;
        public int newborns, lastNewborn = -1;
        /// <summary>The book's version (Pantheon.Nine for the nine; 0 an older save's generated pantheon, let go on load).</summary>
        public int version;
        /// <summary>An older save's generated gods were let go on this load: one line in the world's voice comes in the next hour.</summary>
        [Unsaved] public bool retired;

        /// <summary>The colony remembers at most this many acts of the gods, and this many of one god.</summary>
        public const int MaxActs = 300, MaxActsPerGod = 30;
        /// <summary>At most this many vows kept (the open ones and the newest done), and deeds a year.</summary>
        public const int MaxVows = 40, MaxDeeds = 240;

        [Unsaved] private Dictionary<int, God> byId;

        private static GodBook cached;
        private static Game cachedGame;

        public GodBook(Game game)
        {
        }

        public static GodBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byId = null;
            Gods.Changed();
            if (!Gods.On) return;
            try
            {
                // A new world's pantheon, or an older save's on its first load.
                // The nine for a new world, and for an older save on its first load (its generated gods let go).
                if (!built || version < Pantheon.Nine) Pantheon.Build(this);
                else Pantheon.Keep(this, Now, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the pantheon at load", e);
            }
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= The gods =================

        public God ById(int id)
        {
            if (byId == null || byId.Count != gods.Count)
            {
                byId = new Dictionary<int, God>();
                foreach (var g in gods)
                    if (g != null)
                        byId[g.id] = g;
            }
            return byId.TryGetValue(id, out var x) ? x : null;
        }

        /// <summary>A god added to the pantheon (its id given).</summary>
        public God Add(God g)
        {
            if (g == null) return null;
            g.id = nextId++;
            if (g.look.NullOrEmpty()) g.look = "Rimfolk_GodLook_" + g.domain;
            gods.Add(g);
            byId = null;
            Gods.Changed();
            return g;
        }

        // ================= Ties =================

        public IEnumerable<GodTie> TiesOf(int god) => ties.Where(t => t != null && t.Has(god));

        public GodTie Tie(int a, int b) => ties.FirstOrDefault(t => t != null && t.Has(a) && t.Has(b));

        // ================= Standing =================

        public GodStanding StandingOf(int god, int home, bool make)
        {
            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                if (s != null && s.god == god && s.home == home) return s;
            }
            if (!make) return null;
            var made = new GodStanding { god = god, home = home, tick = Now };
            standings.Add(made);
            return made;
        }

        /// <summary>The standing now, drifted from when it was last set by the god's nature.</summary>
        public float ValueNow(God g, int home)
        {
            var s = g == null ? null : StandingOf(g.id, home, false);
            if (s == null) return 0f;
            return GodsMath.Drift(s.value, (Now - s.tick) / (float)GenDate.TicksPerDay, g.nature);
        }

        /// <summary>Sets a standing (drift taken in first), returning the word before and after.</summary>
        public void Move(God g, int home, float by, out FavourWord before, out FavourWord after)
        {
            var s = StandingOf(g.id, home, true);
            float now = GodsMath.Drift(s.value, (Now - s.tick) / (float)GenDate.TicksPerDay, g.nature);
            before = GodsMath.Word(now);
            s.value = GodsMath.Clamp(now + by);
            s.tick = Now;
            after = GodsMath.Word(s.value);
        }

        // ================= Acts =================

        public void Remember(GodAct a)
        {
            if (a == null || a.god < 0) return;
            if (a.tick <= 0) a.tick = Now;
            acts.Add(a);
            // The oldest of a god with too many, then the oldest of all.
            int ofGod = acts.Count(x => x.god == a.god);
            if (ofGod > MaxActsPerGod) acts.Remove(acts.First(x => x.god == a.god));
            while (acts.Count > MaxActs) acts.RemoveAt(0);
        }

        // ================= Deeds for a newborn =================

        public void NoteDeed(GodDomain d, int home)
        {
            deeds.Add(new GodDeed { domain = d, home = home, tick = Now });
            while (deeds.Count > MaxDeeds) deeds.RemoveAt(0);
        }

        public int DeedsThisYear(GodDomain d, int home)
        {
            int since = Now - GenDate.TicksPerYear;
            return deeds.Count(x => x != null && x.domain == d && x.home == home && x.tick >= since);
        }

        // ================= The day and the hour =================

        /// <summary>The region's day (World/Region/RegionBook.cs): the nine kept, the day's deeds, names worked out in the colony's book, pruning.</summary>
        public static void Daily(int now)
        {
            var b = Instance;
            if (b == null || !Gods.On) return;
            Safe("the pantheon kept", () => Pantheon.Keep(b, now));
            Safe("the day's deeds", () => GodDeeds.Daily(b, now));
            Safe("the colony's book", () => GodLore.Daily(now));
            Safe("pruning", () => b.Prune(now));
        }

        /// <summary>The region's hour: the gods' hands remarked on, talk of the gods at the evening fire and from visitors, and who is on a home map, so the colony learns their gods.</summary>
        public static void Hourly(int now)
        {
            var b = Instance;
            if (b == null || !Gods.On || !b.built) return;
            Safe("the old gods let go", () => b.TellRetired());
            Safe("the gods' hands", () => GodHands.Hourly(now));
            Safe("talk of the gods", () => GodTales.Hourly(now));
            Safe("visitors", () => Pantheon.LearnFromVisitors(b));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: " + what, e);
            }
        }

        /// <summary>
        /// An older save's generated gods were let go: one line, in the world's voice (no god named, nothing of what any god
        /// thinks), the first hour after the load.
        /// </summary>
        private void TellRetired()
        {
            if (!retired) return;
            retired = false;
            ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, Homes.Seat, "CC_God_Retired".Translate(), null);
        }

        /// <summary>Standings come to rest at unknown and untouched for a year are let go; deeds over a year old; vows long done.</summary>
        private void Prune(int now)
        {
            standings.RemoveAll(s => s == null || ById(s.god) == null
                                     || now - s.tick > GenDate.TicksPerYear && Math.Abs(GodsMath.Drift(s.value, (now - s.tick) / (float)GenDate.TicksPerDay, ById(s.god).nature)) < 0.5f);
            deeds.RemoveAll(d => d == null || now - d.tick > GenDate.TicksPerYear);
            while (vows.Count > MaxVows && vows.Any(v => !v.Open)) vows.Remove(vows.First(v => !v.Open));
            vows.RemoveAll(v => v == null || v.pawn == null || v.pawn.Discarded);
            ties.RemoveAll(t => t == null || ById(t.a) == null || ById(t.b) == null);
            acts.RemoveAll(a => a == null || ById(a.god) == null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref gods, "gods", LookMode.Deep);
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref standings, "standings", LookMode.Deep);
            Scribe_Collections.Look(ref acts, "acts", LookMode.Deep);
            Scribe_Collections.Look(ref vows, "vows", LookMode.Deep);
            Scribe_Collections.Look(ref deeds, "deeds", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref built, "built");
            Scribe_Values.Look(ref version, "version");
            Scribe_Values.Look(ref newborns, "newborns");
            Scribe_Values.Look(ref lastNewborn, "lastNewborn", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                gods ??= new List<God>();
                gods.RemoveAll(g => g == null);
                ties ??= new List<GodTie>();
                ties.RemoveAll(t => t == null);
                standings ??= new List<GodStanding>();
                standings.RemoveAll(s => s == null);
                acts ??= new List<GodAct>();
                acts.RemoveAll(a => a == null);
                vows ??= new List<GodVow>();
                vows.RemoveAll(v => v == null);
                deeds ??= new List<GodDeed>();
                deeds.RemoveAll(d => d == null);
                byId = null;
            }
        }
    }
}
