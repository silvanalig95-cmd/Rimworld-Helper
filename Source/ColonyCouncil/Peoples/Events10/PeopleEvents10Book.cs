using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A price a ruling moved for a season (item 96): the thing, the factor, until when, and the people whose doing it is.</summary>
    public class SeasonPrice : IExposable
    {
        public string thing;
        public float factor = 1f;
        public int until = -1;
        public string why;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thing, "thing");
            Scribe_Values.Look(ref factor, "factor", 1f);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref why, "why");
        }
    }

    /// <summary>
    /// What the peoples' own events of round 10 keep with the game (Peoples/Events10/): the prices a moot's ruling moved for a season, and
    /// the peoples that have handed on their art to one of the colony's (once a game each). The rest lives in the events' visits and round
    /// 2's seasons away. A save from before has none of it.
    /// </summary>
    public class PeopleEvents10Book : GameComponent
    {
        public List<SeasonPrice> prices = new List<SeasonPrice>();
        /// <summary>The peoples (Faction.GetUniqueLoadID) that have handed on their art.</summary>
        public List<string> artGiven = new List<string>();

        private static PeopleEvents10Book cached;
        private static Game cachedGame;

        /// <summary>The prices in force now, by thing (read by the trade price hook, so kept ready rather than looked up each time).</summary>
        private static readonly Dictionary<ThingDef, float> inForce = new Dictionary<ThingDef, float>();

        private static bool anyInForce;

        public PeopleEvents10Book(Game game)
        {
        }

        public static PeopleEvents10Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents10Book>();
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
            Refresh(Find.TickManager?.TicksGame ?? 0);
        }

        /// <summary>A season's price is in force for some thing now.</summary>
        public static bool AnyPrice => anyInForce;

        /// <summary>The season's factor on this thing's price now (1 for none).</summary>
        public static float Factor(ThingDef d) => anyInForce && d != null && inForce.TryGetValue(d, out float f) ? f : 1f;

        /// <summary>A ruling's price for a season: a new one for the same thing takes the old one's place.</summary>
        public void AddPrice(string thing, float factor, int until, string why)
        {
            if (thing.NullOrEmpty()) return;
            prices.RemoveAll(p => p == null || p.thing == thing);
            prices.Add(new SeasonPrice { thing = thing, factor = factor, until = until, why = why });
            while (prices.Count > PeopleEvents10Math.MaxPrices) prices.RemoveAt(0);
            Refresh(Find.TickManager.TicksGame);
        }

        /// <summary>The prices whose season is over are dropped (returned, to be told), and what is in force is read again.</summary>
        public List<SeasonPrice> Refresh(int now)
        {
            var over = prices.Where(p => p == null || p.until <= now).ToList();
            prices.RemoveAll(p => p == null || p.until <= now);
            inForce.Clear();
            foreach (var p in prices)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(p.thing ?? "");
                if (d != null) inForce[d] = p.factor;
            }
            anyInForce = inForce.Count > 0;
            return over.Where(p => p != null).ToList();
        }

        public bool ArtGiven(Faction f) => f != null && artGiven.Contains(f.GetUniqueLoadID());

        public void GaveArt(Faction f)
        {
            if (f == null || ArtGiven(f)) return;
            artGiven.Add(f.GetUniqueLoadID());
            while (artGiven.Count > PeopleEvents10Math.Arts.Count * 3) artGiven.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref prices, "prices", LookMode.Deep);
            Scribe_Collections.Look(ref artGiven, "artGiven", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                prices ??= new List<SeasonPrice>();
                artGiven ??= new List<string>();
                prices.RemoveAll(p => p == null || p.thing.NullOrEmpty());
            }
        }
    }
}
