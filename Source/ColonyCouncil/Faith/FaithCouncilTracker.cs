using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>An evening of faith called and waiting to be held (or under way): its gathering, its faith, its speaker and its place.</summary>
    public class FaithEveningPlan : IExposable
    {
        /// <summary>The gathering's play-out (PlayOut/).</summary>
        public int playId = -1;
        public Ideo faith;
        public Pawn speaker;
        /// <summary>The faith's altar or ideogram it was called to, if there was one.</summary>
        public Thing place;
        public int created;

        public void ExposeData()
        {
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_References.Look(ref faith, "faith");
            Scribe_References.Look(ref speaker, "speaker");
            Scribe_References.Look(ref place, "place");
            Scribe_Values.Look(ref created, "created");
        }
    }

    /// <summary>Someone heard a faith spoken for (at an evening of faith, or an evening spent with a believer): who, which, when.</summary>
    public class FaithHeard : IExposable
    {
        public int pawn;
        public int ideo;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// What the council side of faith in the colony keeps between saves (Faith/FaithEvenings.cs, Decrees/DecreeFaith.cs,
    /// Wishes/WishWorkers_Faith.cs): when the last evening of faith was held, and each faith's last; the evening waiting
    /// to be held; the faith the decree names; and who heard which faith spoken for lately (for the wish to learn it).
    /// Nothing here ticks: it is only read and written by the things that use it.
    /// </summary>
    public class FaithCouncilTracker : GameComponent
    {
        /// <summary>When the last evening of faith was held anywhere in the colony.</summary>
        public int lastEvening = -999999;
        /// <summary>Each faith's last evening (by the game's id for the faith).</summary>
        public Dictionary<int, int> faithEvenings = new Dictionary<int, int>();
        /// <summary>The evening waiting to be held (one at a time).</summary>
        public List<FaithEveningPlan> plans = new List<FaithEveningPlan>();
        /// <summary>The faith the decree "one faith for the colony" names, while it holds (or as last chosen).</summary>
        public Ideo decreeFaith;
        /// <summary>Who heard which faith spoken for lately: one each, a month at most.</summary>
        public List<FaithHeard> heard = new List<FaithHeard>();

        /// <summary>At most this many kept in <see cref="heard"/>; older ones go first.</summary>
        public const int MaxHeard = 40;

        public const int HeardDays = 30;

        private static FaithCouncilTracker cached;
        private static Game cachedGame;

        public static FaithCouncilTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<FaithCouncilTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public FaithCouncilTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Evenings ----------------

        public float DaysSinceEvening => (Now - lastEvening) / (float)GenDate.TicksPerDay;

        public float DaysSinceFaith(Ideo faith)
        {
            if (faith == null || !faithEvenings.TryGetValue(faith.id, out int t)) return 9999f;
            return (Now - t) / (float)GenDate.TicksPerDay;
        }

        public void Held(Ideo faith, bool anyEvening)
        {
            if (faith == null) return;
            // Faiths the game no longer has (every believer gone, the faith dissolved), and evenings long past, are let go.
            int now = Now;
            try
            {
                var ids = new HashSet<int>(Find.IdeoManager.IdeosListForReading.Select(i => i.id));
                foreach (var k in faithEvenings.Where(kv => !ids.Contains(kv.Key) || now - kv.Value > GenDate.TicksPerDay * 60).Select(kv => kv.Key).ToList())
                    faithEvenings.Remove(k);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("faith evenings pruning", e);
            }
            faithEvenings[faith.id] = now;
            if (anyEvening) lastEvening = now;
        }

        /// <summary>The evening waiting on this map, if any; plans whose gathering is over or gone are let go first.</summary>
        public FaithEveningPlan Pending(Map map)
        {
            var tracker = PlayOutTracker.Instance;
            plans.RemoveAll(p => p == null || p.faith == null || tracker == null || tracker.Get(p.playId) == null || tracker.Get(p.playId).state == PlayState.Done);
            foreach (var p in plans)
                if (map == null || tracker.Get(p.playId)?.map == map) return p;
            return null;
        }

        public FaithEveningPlan PlanFor(int playId)
        {
            foreach (var p in plans)
                if (p != null && p.playId == playId) return p;
            return null;
        }

        // ---------------- Who heard which faith ----------------

        public void Heard(Pawn p, Ideo faith)
        {
            if (p == null || faith == null) return;
            int now = Now;
            heard.RemoveAll(h => h.pawn == p.thingIDNumber || now - h.tick > HeardDays * GenDate.TicksPerDay);
            heard.Add(new FaithHeard { pawn = p.thingIDNumber, ideo = faith.id, tick = now });
            if (heard.Count > MaxHeard) heard.RemoveRange(0, heard.Count - MaxHeard);
        }

        /// <summary>This colonist heard this faith spoken for since <paramref name="since"/>.</summary>
        public bool HeardSince(Pawn p, Ideo faith, int since)
        {
            if (p == null || faith == null) return false;
            foreach (var h in heard)
                if (h.pawn == p.thingIDNumber && h.ideo == faith.id && h.tick >= since) return true;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastEvening, "lastEvening", -999999);
            Scribe_Collections.Look(ref faithEvenings, "faithEvenings", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref plans, "plans", LookMode.Deep);
            Scribe_References.Look(ref decreeFaith, "decreeFaith");
            Scribe_Collections.Look(ref heard, "heard", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (faithEvenings == null) faithEvenings = new Dictionary<int, int>();
                if (plans == null) plans = new List<FaithEveningPlan>();
                plans.RemoveAll(p => p == null || p.faith == null);
                if (heard == null) heard = new List<FaithHeard>();
                heard.RemoveAll(h => h == null);
            }
        }
    }
}
