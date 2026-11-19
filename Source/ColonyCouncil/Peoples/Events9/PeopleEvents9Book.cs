using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The vault's call for the colony's fighters (item 91): its people, its town, the home that answered, what came up from the lower
    /// level, the day the call lapses, and once the fight is on, its map and what came up there. Stage "called" (waiting for a caravan),
    /// "fight" (on the fight's map), "done".
    /// </summary>
    public class SealRecord : IExposable
    {
        public Faction faction;
        public Settlement town;
        public Map home;
        public bool insects;
        public int due = -1;
        public string stage = "called";
        public Map fight;
        public List<Pawn> enemies = new List<Pawn>();

        public bool Open => stage != "done";

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref town, "town");
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref insects, "insects");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref stage, "stage", "called");
            Scribe_References.Look(ref fight, "fight");
            Scribe_Collections.Look(ref enemies, "enemies", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                enemies ??= new List<Pawn>();
                enemies.RemoveAll(p => p == null);
                stage ??= "called";
            }
        }
    }

    /// <summary>
    /// What the peoples' own events of round 9 keep with the game (Peoples/Events9/): the long breaks the Choir was rolled for and when
    /// its listener last came, the void's next threat a warden of the quiet named and until when the word holds, and the vault's calls
    /// for fighters. The rest lives in the events' visits, round 1's work on a colonist, the Lattice's link on the mechanitor (a hediff)
    /// and the camps' own states. A save from before has none of it.
    /// </summary>
    public class PeopleEvents9Book : GameComponent
    {
        /// <summary>"pawnId:startTick" for each long break rolled for a listener, PeopleEvents9Math.MaxShoutsRolled kept.</summary>
        public List<string> shouts = new List<string>();
        public List<SealRecord> seals = new List<SealRecord>();
        /// <summary>The void's next threat as a warden of the quiet named it (an IncidentDef), who named it, until when the word holds, and where.</summary>
        public string foretold;
        public string foretoldBy;
        public int foretoldUntil = -1;
        public Map foretoldMap;
        /// <summary>When a listener last came for a mind that shouts.</summary>
        public int lastShout = -1;

        private static PeopleEvents9Book cached;
        private static Game cachedGame;

        public PeopleEvents9Book(Game game)
        {
        }

        public static PeopleEvents9Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents9Book>();
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
            PeopleEvents9.Forget();
        }

        public bool ShoutRolled(string key) => key != null && shouts.Contains(key);

        public void NoteShout(string key)
        {
            if (key == null || shouts.Contains(key)) return;
            shouts.Add(key);
            while (shouts.Count > PeopleEvents9Math.MaxShoutsRolled) shouts.RemoveAt(0);
        }

        public void AddSeal(SealRecord r)
        {
            if (r?.faction == null) return;
            seals.Add(r);
            while (seals.Count > 3 && seals.Any(x => !x.Open)) seals.Remove(seals.First(x => !x.Open));
            while (seals.Count > 3) seals.RemoveAt(0);
        }

        public void Foretell(string incident, string by, int until, Map map)
        {
            foretold = incident;
            foretoldBy = by;
            foretoldUntil = until;
            foretoldMap = map;
        }

        public void ClearForetold()
        {
            foretold = null;
            foretoldBy = null;
            foretoldUntil = -1;
            foretoldMap = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref shouts, "shouts", LookMode.Value);
            Scribe_Collections.Look(ref seals, "seals", LookMode.Deep);
            Scribe_Values.Look(ref foretold, "foretold");
            Scribe_Values.Look(ref foretoldBy, "foretoldBy");
            Scribe_Values.Look(ref foretoldUntil, "foretoldUntil", -1);
            Scribe_References.Look(ref foretoldMap, "foretoldMap");
            Scribe_Values.Look(ref lastShout, "lastShout", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                shouts ??= new List<string>();
                seals ??= new List<SealRecord>();
                seals.RemoveAll(r => r?.faction == null);
            }
        }
    }
}
