using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeps the deeds the colony's people did and who knows of them (<see cref="Deed"/>), in a game component of its own.
    /// What one person makes of another from what they've heard, and the colony's view of each person (their name), are
    /// worked out from the deeds when asked and kept until something changes (or for the hour, as deeds fade and people
    /// change). Its own hour (<see cref="Sched.Reputation"/>) watches fights and work; its first hour of the day records
    /// the days of idling and of tending, tells of names that have formed or faded, and lets go of the dead and departed.
    /// Readers go through <see cref="Reputations"/>, recorders through <see cref="Deeds"/>, talk through <see cref="Gossip"/>.
    /// </summary>
    public class ReputationTracker : GameComponent
    {
        public List<Deed> deeds = new List<Deed>();
        public int nextId = 1;
        /// <summary>Per person (thingIDNumber): the degree each side of their name was last told at (packed, 3 bits a side).</summary>
        public Dictionary<int, int> told = new Dictionary<int, int>();

        private static ReputationTracker cached;
        private static Game cachedGame;

        public static ReputationTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<ReputationTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public ReputationTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Changed();
            Gossip.Reset();
            ReputationWatch.Reset();
        }

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- The colony's people ----------------

        [Unsaved] private Dictionary<int, Pawn> colony;
        [Unsaved] private int colonyTick = -99999;

        /// <summary>The colony's own people with a name to keep (see <see cref="Reputations.Public"/>), by thingIDNumber; refreshed every 250 ticks.</summary>
        public Dictionary<int, Pawn> Colony
        {
            get
            {
                int now = Now;
                if (colony == null || now - colonyTick >= Sched.RareTicks || now < colonyTick)
                {
                    var map = new Dictionary<int, Pawn>();
                    foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                        if (Reputations.Public(p)) map[p.thingIDNumber] = p;
                    try
                    {
                        foreach (var p in People.OffMapColonists())
                            if (Reputations.Public(p)) map[p.thingIDNumber] = p;
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("reputation: people off the map", e);
                    }
                    if (colony == null || map.Count != colony.Count || map.Keys.Any(k => !colony.ContainsKey(k))) dirty = true;
                    colony = map;
                    colonyTick = now;
                }
                return colony;
            }
        }

        public Pawn PawnOf(int id) => Colony.TryGetValue(id, out var p) ? p : null;

        // ---------------- Indices ----------------

        [Unsaved] private Dictionary<int, List<Deed>> byDoer;
        [Unsaved] private Dictionary<int, List<Deed>> byKnower;

        private void Index()
        {
            if (byDoer != null) return;
            byDoer = new Dictionary<int, List<Deed>>();
            byKnower = new Dictionary<int, List<Deed>>();
            foreach (var d in deeds)
            {
                Add(byDoer, d.doer, d);
                foreach (var k in d.heard) Add(byKnower, k.id, d);
            }
        }

        private static void Add(Dictionary<int, List<Deed>> index, int id, Deed d)
        {
            if (!index.TryGetValue(id, out var list)) index[id] = list = new List<Deed>();
            if (!list.Contains(d)) list.Add(d);
        }

        private static readonly List<Deed> none = new List<Deed>();

        /// <summary>What this person did (newest last). Don't change the list.</summary>
        public List<Deed> By(int doer)
        {
            Index();
            return byDoer.TryGetValue(doer, out var list) ? list : none;
        }

        /// <summary>The deeds this person knows of. Don't change the list.</summary>
        public List<Deed> KnownBy(int knower)
        {
            Index();
            return byKnower.TryGetValue(knower, out var list) ? list : none;
        }

        /// <summary>A new deed: kept, and the weakest let go if its doer has too many.</summary>
        public void Keep(Deed d)
        {
            if (d == null) return;
            Index();
            if (d.id <= 0) d.id = nextId++;
            deeds.Add(d);
            Add(byDoer, d.doer, d);
            foreach (var k in d.heard) Add(byKnower, k.id, d);
            int now = Now;
            var mine = By(d.doer);
            while (mine.Count > RepMath.MaxDeedsPerSubject)
                Forget(mine.Where(x => x != d).OrderBy(x => x.Weight(now)).First());
            if (deeds.Count > RepMath.MaxDeeds) Forget(deeds.Where(x => x != d).OrderBy(x => x.Weight(now)).First());
            Changed();
        }

        /// <summary>
        /// Someone learns of a deed (they saw it, or <paramref name="from"/> told them): kept with their version and how
        /// far they believe it. Someone who already knows only takes the stronger of the two versions (a witness keeps
        /// what they saw). True when they didn't know before.
        /// </summary>
        public bool Learn(Deed d, Pawn who, Pawn from, float version, float belief, Slant slant)
        {
            if (d == null || who == null || who.thingIDNumber == d.doer) return false;
            var k = d.KnowerOf(who.thingIDNumber);
            if (k != null)
            {
                // Seeing it for themselves (or seeing it again, as it grew) puts paid to any version they heard.
                if (from == null && (!k.Witness || k.version < d.size))
                {
                    k.from = -1;
                    k.fromName = null;
                    k.version = d.size;
                    k.belief = 1f;
                    Changed();
                }
                return false;
            }
            if (d.heard.Count >= RepMath.MaxKnowers) return false;
            k = new DeedKnower
            {
                id = who.thingIDNumber,
                name = who.LabelShort,
                from = from?.thingIDNumber ?? -1,
                fromName = from?.LabelShort,
                tick = Now,
                version = from == null ? d.size : version,
                belief = from == null ? 1f : RepMath.Clamp(belief, 0.2f, 1f),
                slant = slant
            };
            d.heard.Add(k);
            Index();
            Add(byKnower, k.id, d);
            Changed();
            return true;
        }

        public void Forget(Deed d)
        {
            if (d == null) return;
            deeds.Remove(d);
            if (byDoer != null && byDoer.TryGetValue(d.doer, out var list)) list.Remove(d);
            if (byKnower != null)
                foreach (var k in d.heard)
                    if (byKnower.TryGetValue(k.id, out var l)) l.Remove(d);
            Changed();
        }

        // ---------------- What people make of each other ----------------

        [Unsaved] private bool dirty = true;
        [Unsaved] private int builtTick = -99999;
        [Unsaved] private readonly Dictionary<long, float[]> impressions = new Dictionary<long, float[]>();
        [Unsaved] private readonly Dictionary<int, float[]> standing = new Dictionary<int, float[]>();
        [Unsaved] private int publicCount;

        /// <summary>Something changed: worked out again the next time anyone asks.</summary>
        public void Changed() => dirty = true;

        private static long Key(int listener, int subject) => ((long)listener << 32) | (uint)subject;

        private void Build()
        {
            int now = Now;
            if (!dirty && now - builtTick >= 0 && now - builtTick < GenDate.TicksPerHour) return;
            // A change is worked in at most every half second of game time; the hour keeps fading and changed characters in.
            if (dirty && now - builtTick >= 0 && now - builtTick < 30) return;
            var col = Colony;
            impressions.Clear();
            standing.Clear();
            var personas = new Dictionary<int, float[]>();
            foreach (var d in deeds)
            {
                if (d.def == null || !col.ContainsKey(d.doer)) continue;
                float fade = d.Fade(now);
                if (d.size * fade < RepMath.Forgotten) continue;
                var shifts = d.def.Shifts;
                foreach (var k in d.heard)
                {
                    if (k.id == d.doer || !col.TryGetValue(k.id, out var listener)) continue;
                    if (!personas.TryGetValue(k.id, out var persona)) personas[k.id] = persona = Persona.Values(listener);
                    long key = Key(k.id, d.doer);
                    if (!impressions.TryGetValue(key, out var arr)) impressions[key] = arr = new float[RepMath.Facets];
                    for (int f = 0; f < RepMath.Facets; f++)
                    {
                        if (shifts[f] == 0f) continue;
                        arr[f] += RepMath.Part(shifts[f], k.version, k.belief, fade, RepMath.Weigh((RepFacet)f, shifts[f], persona));
                    }
                }
            }
            var sums = new Dictionary<int, float[]>();
            foreach (var kv in impressions)
            {
                var arr = kv.Value;
                for (int f = 0; f < RepMath.Facets; f++) arr[f] = RepMath.CapFacet(arr[f]);
                int subject = (int)(kv.Key & 0xffffffffL);
                if (!sums.TryGetValue(subject, out var s)) sums[subject] = s = new float[RepMath.Facets];
                for (int f = 0; f < RepMath.Facets; f++) s[f] += arr[f];
            }
            publicCount = col.Count;
            foreach (var kv in sums)
            {
                var st = new float[RepMath.Facets];
                for (int f = 0; f < RepMath.Facets; f++) st[f] = RepMath.Standing(kv.Value[f], publicCount - 1);
                standing[kv.Key] = st;
            }
            builtTick = now;
            dirty = false;
        }

        /// <summary>What <paramref name="listener"/> makes of <paramref name="subject"/> from what they've heard, side by side; null for nothing.</summary>
        public float[] Impression(int listener, int subject)
        {
            Build();
            return impressions.TryGetValue(Key(listener, subject), out var arr) ? arr : null;
        }

        /// <summary>The colony's view of someone, side by side; null for nothing said.</summary>
        public float[] StandingOf(int subject)
        {
            Build();
            return standing.TryGetValue(subject, out var arr) ? arr : null;
        }

        /// <summary>How many people the colony's view is shared out over (the colony's own people).</summary>
        public int PublicCount
        {
            get
            {
                Build();
                return publicCount;
            }
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Reputation) || !Reputations.On) return;
            try
            {
                ReputationWatch.Hourly(this);
                if (Sched.FirstHourOfDay(now))
                {
                    ReputationWatch.Daily(this);
                    Prune(false);
                    Announce();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reputation hour", e);
            }
        }

        // ---------------- Names told ----------------

        private static int Packed(float[] st)
        {
            int v = 0;
            for (int f = 0; f < RepMath.Facets; f++) v |= (RepMath.Degree(st?[f] ?? 0f) + 2) << (f * 3);
            return v;
        }

        private static int Unpack(int packed, int f) => ((packed >> (f * 3)) & 7) - 2;

        /// <summary>
        /// Once a day: a side of someone's name that has newly formed, grown strong or faded away is told in the news (two
        /// a day at most; the rest wait for tomorrow), so the player sees a reputation form.
        /// </summary>
        private void Announce()
        {
            int posted = 0;
            foreach (var kv in Colony.ToList())
            {
                if (posted >= 2) return;
                var p = kv.Value;
                var st = StandingOf(kv.Key);
                int now = Packed(st);
                // Someone new to the record starts from nothing said.
                if (!told.TryGetValue(kv.Key, out int before)) before = Packed(null);
                if (now == before)
                {
                    told[kv.Key] = now;
                    continue;
                }
                int kept = before;
                bool said = false;
                for (int f = 0; f < RepMath.Facets; f++)
                {
                    int was = Unpack(before, f), isNow = Unpack(now, f);
                    if (was == isNow) continue;
                    string text = null;
                    // Formed, grown strong, or turned round (the coward who stood their ground): told. Weakened: quietly.
                    if (isNow != 0 && (Math.Abs(isNow) > Math.Abs(was) || Math.Sign(isNow) != Math.Sign(was)))
                        text = "CC_Rep_News_Formed".Translate(p.Named("PAWN"), Reputations.Word((RepFacet)f, st[f]).Named("WORD")).Resolve();
                    else if (isNow == 0)
                    {
                        float v = Math.Sign(was) * (Math.Abs(was) >= 2 ? RepMath.Strong : RepMath.Shows);
                        text = "CC_Rep_News_Faded".Translate(p.Named("PAWN"), Reputations.Word((RepFacet)f, v).Named("WORD")).Resolve();
                    }
                    if (text != null)
                    {
                        // One side a day per person, two people a day: the rest is told tomorrow.
                        if (posted >= 2 || said) continue;
                        // apart: news of what the colony now says about someone.
                        News.Post(text, new LookTargets(p), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                        posted++;
                        said = true;
                    }
                    kept = (kept & ~(7 << (f * 3))) | ((isNow + 2) << (f * 3));
                }
                told[kv.Key] = kept;
            }
        }

        // ---------------- Keeping it small ----------------

        /// <summary>
        /// Lets go of deeds whose doer is dead or no longer one of the colony's people, of those who knew them and are
        /// gone, and of deeds faded to nothing. With <paramref name="saving"/>, only of those the game itself no longer knows.
        /// </summary>
        public void Prune(bool saving)
        {
            int now = Now;
            HashSet<int> keep;
            if (saving) keep = CouncilGameComponent.KnownPawnNumbers();
            else keep = new HashSet<int>(Colony.Keys);
            int before = deeds.Count;
            deeds.RemoveAll(d => d?.def == null || !keep.Contains(d.doer) || d.Weight(now) < RepMath.Forgotten);
            foreach (var d in deeds) d.heard.RemoveAll(k => !keep.Contains(k.id));
            foreach (var id in told.Keys.Where(id => !keep.Contains(id)).ToList()) told.Remove(id);
            byDoer = null;
            byKnower = null;
            if (deeds.Count != before) Changed();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune(true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("reputation clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref deeds, "deeds", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref told, "told", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                deeds ??= new List<Deed>();
                deeds.RemoveAll(d => d?.def == null);
                told ??= new Dictionary<int, int>();
                foreach (var d in deeds)
                    if (d.id >= nextId) nextId = d.id + 1;
                byDoer = null;
                byKnower = null;
                Changed();
            }
        }
    }
}
