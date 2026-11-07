using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A people that arose during play (Peoples/Arising.cs): who they are, from what (the cause's kind and its key, so a
    /// cause gives one people at most), when, and the names their card and history give it by (the fallen town, the one
    /// sent away, the faith they broke with, the league's towns), with the people it came from when there was one.
    /// </summary>
    public class ArisenPeople : IExposable
    {
        public Faction people;
        /// <summary>One of <see cref="ArisingMath.Kinds"/>.</summary>
        public string kind;
        /// <summary>The cause's key (<see cref="ArisingMath.Key"/>, <see cref="ArisingMath.LeagueKey"/>).</summary>
        public string cause;
        public int tick = -1;
        /// <summary>What it came from, by name: the fallen town, the one sent away, the faith, the league's towns.</summary>
        public string from;
        /// <summary>The people it came from (the fallen town's, the faith's), and their name for when they are gone.</summary>
        public Faction parent;
        public string parentName;

        /// <summary>Its people stands: in the world and not defeated.</summary>
        public bool Standing => people != null && !people.defeated;

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref from, "from");
            Scribe_References.Look(ref parent, "parent");
            Scribe_Values.Look(ref parentName, "parentName");
        }
    }

    /// <summary>
    /// The peoples that arose during play, kept with the game (release B; Peoples/Arising.cs makes them): one record a
    /// people, at most <see cref="ArisingMath.MaxRecords"/> (the oldest of a people no longer standing go first). The
    /// pace reads it: when the last arose, the last of each cause, how many stand. A save from before has none.
    /// </summary>
    public class ArisingBook : GameComponent
    {
        public List<ArisenPeople> arisen = new List<ArisenPeople>();

        private static ArisingBook cached;
        private static Game cachedGame;

        public ArisingBook(Game game)
        {
        }

        public static ArisingBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<ArisingBook>();
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
        }

        /// <summary>A new world: the river folk's towns the game put where no river runs move to one in reach (Peoples/PeopleTowns.cs).</summary>
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            try
            {
                PeopleTowns.ToTheRivers();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the landings to the rivers", e);
            }
        }

        /// <summary>When the last people arose of any cause (<paramref name="kind"/> null) or of this one; -1 for none.</summary>
        public int LastAt(string kind = null)
        {
            int last = -1;
            foreach (var r in arisen)
                if (r != null && (kind == null || r.kind == kind) && r.tick > last)
                    last = r.tick;
            return last;
        }

        /// <summary>How many of the peoples that arose stand.</summary>
        public int Standing => arisen.Count(r => r != null && r.Standing);

        /// <summary>This cause has given a people already.</summary>
        public bool Used(string cause) => cause != null && arisen.Any(r => r != null && r.cause == cause);

        /// <summary>A town that is one of a league already.</summary>
        public bool InLeague(int town) => town >= 0 && arisen.Any(r => r != null && r.kind == ArisingMath.League && ArisingMath.LeagueTowns(r.cause).Contains(town));

        /// <summary>The record of a people that arose, or null for any other people.</summary>
        public ArisenPeople Of(Faction f) => f == null ? null : arisen.FirstOrDefault(r => r != null && r.people == f);

        public void Add(ArisenPeople r)
        {
            if (r == null) return;
            arisen.Add(r);
            while (arisen.Count > ArisingMath.MaxRecords)
            {
                var old = arisen.Where(x => x == null || !x.Standing).OrderBy(x => x?.tick ?? -1).FirstOrDefault() ?? arisen.OrderBy(x => x.tick).First();
                arisen.Remove(old);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref arisen, "arisen", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                arisen ??= new List<ArisenPeople>();
                // A people whose def is gone (Rimfolk's peoples taken out of the mod list and back) leaves its record with no people: the cause stays used.
                arisen.RemoveAll(r => r == null || r.kind.NullOrEmpty());
            }
        }
    }
}
