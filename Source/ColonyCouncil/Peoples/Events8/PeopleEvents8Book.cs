using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A sleeper of the Hollow Fleet's in one of the game's old caskets on the colony's land (item 76), left asleep when the deck-runner
    /// went: woken later at the colony, they find their own way home. The casket, the sleeper, the Fleet and the home.
    /// </summary>
    public class SleeperRecord : IExposable
    {
        public Thing casket;
        public Pawn sleeper;
        public Faction faction;
        public Map map;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref casket, "casket");
            Scribe_References.Look(ref sleeper, "sleeper");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// What the peoples' own events of round 8 keep with the game (Peoples/Events8/): the elections the Republic was rolled for, the
    /// deeds rolled for its medal, the famines rolled for its ration post, the Fleet's sleepers left asleep in the colony's caskets,
    /// the Fleets whose order to disembark was given, and when the code-fired visits last came. The rest lives in the events' visits,
    /// a camp's own state and round 2's seasons away. A save from before has none of it.
    /// </summary>
    public class PeopleEvents8Book : GameComponent
    {
        /// <summary>"mapId:announcedTick" for each election rolled for an observer, six kept.</summary>
        public List<string> elections = new List<string>();
        /// <summary>The deeds (their ids) rolled for a medal, PeopleEvents8Math.MaxMedalDeeds kept.</summary>
        public List<int> medalDeeds = new List<int>();
        /// <summary>"defName:startTick" for each famine rolled for a ration post, six kept.</summary>
        public List<string> famines = new List<string>();
        public List<SleeperRecord> sleepers = new List<SleeperRecord>();
        /// <summary>The Fleets (their load ids) whose order to disembark was given, eight kept.</summary>
        public List<int> disembarked = new List<int>();
        /// <summary>When an observer, a medal and a ration post last came.</summary>
        public int lastObserver = -1, lastMedal = -1, lastRation = -1;

        private static PeopleEvents8Book cached;
        private static Game cachedGame;

        public PeopleEvents8Book(Game game)
        {
        }

        public static PeopleEvents8Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents8Book>();
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
            PeopleEvents8.Forget();
        }

        private static void Note<T>(List<T> list, T item, int keep)
        {
            if (list.Contains(item)) return;
            list.Add(item);
            while (list.Count > keep) list.RemoveAt(0);
        }

        public bool ElectionRolled(string key) => key != null && elections.Contains(key);

        public void NoteElection(string key)
        {
            if (key != null) Note(elections, key, 6);
        }

        public bool DeedRolled(int id) => medalDeeds.Contains(id);

        public void NoteDeed(int id) => Note(medalDeeds, id, PeopleEvents8Math.MaxMedalDeeds);

        public bool FamineRolled(string key) => key != null && famines.Contains(key);

        public void NoteFamine(string key)
        {
            if (key != null) Note(famines, key, 6);
        }

        public void AddSleeper(SleeperRecord r)
        {
            if (r?.sleeper == null || sleepers.Any(s => s.sleeper == r.sleeper && !s.done)) return;
            sleepers.Add(r);
            while (sleepers.Count > 4 && sleepers.Any(x => x.done)) sleepers.Remove(sleepers.First(x => x.done));
            while (sleepers.Count > 4) sleepers.RemoveAt(0);
        }

        public bool Disembarked(Faction f) => f != null && disembarked.Contains(f.loadID);

        public void NoteDisembarked(Faction f)
        {
            if (f != null) Note(disembarked, f.loadID, 8);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref elections, "elections", LookMode.Value);
            Scribe_Collections.Look(ref medalDeeds, "medalDeeds", LookMode.Value);
            Scribe_Collections.Look(ref famines, "famines", LookMode.Value);
            Scribe_Collections.Look(ref sleepers, "sleepers", LookMode.Deep);
            Scribe_Collections.Look(ref disembarked, "disembarked", LookMode.Value);
            Scribe_Values.Look(ref lastObserver, "lastObserver", -1);
            Scribe_Values.Look(ref lastMedal, "lastMedal", -1);
            Scribe_Values.Look(ref lastRation, "lastRation", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                elections ??= new List<string>();
                medalDeeds ??= new List<int>();
                famines ??= new List<string>();
                sleepers ??= new List<SleeperRecord>();
                sleepers.RemoveAll(r => r?.sleeper == null);
                disembarked ??= new List<int>();
            }
        }
    }
}
