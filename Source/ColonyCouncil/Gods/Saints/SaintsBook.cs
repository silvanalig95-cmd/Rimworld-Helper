using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the colony's own saints (Gods/Saints/Saints.cs): a fallen colonist the council named, by name only (never their
    /// kin, never a line of descent), the god they were named a saint of, why, and when their day is.
    /// </summary>
    public class ColonySaint : IExposable
    {
        public int id = -1;
        /// <summary>Their short name and their full name, as the colony remembers them.</summary>
        public string name, fullName;
        /// <summary>The dead pawn's ThingID, to find their grave by.</summary>
        public string pawnId;
        public Gender gender;
        /// <summary>The god they were named a saint of (God.id), or -1.</summary>
        public int god = -1;
        /// <summary>The faith they were named a saint of (Ideology), and its name as it was then.</summary>
        public Ideo ideo;
        public string faithName;
        public SaintReason reason;
        /// <summary>When they died and when they were named; the day of the year they died (their day) and the year they were named.</summary>
        public int died = -1, named = -1, feastDay = -1, namedYear = -1;
        /// <summary>The last year their day was kept, or -1.</summary>
        public int lastFeastYear = -1;
        /// <summary>The home they were named at (Gods.HomeKey).</summary>
        public int home = -1;
        /// <summary>The colony's age when they were named (their title's word).</summary>
        public int age = 1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref fullName, "fullName");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref faithName, "faithName");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref died, "died", -1);
            Scribe_Values.Look(ref named, "named", -1);
            Scribe_Values.Look(ref feastDay, "feastDay", -1);
            Scribe_Values.Look(ref namedYear, "namedYear", -1);
            Scribe_Values.Look(ref lastFeastYear, "lastFeastYear", -1);
            Scribe_Values.Look(ref home, "home", -1);
            Scribe_Values.Look(ref age, "age", 1);
        }
    }

    /// <summary>A death that may merit a saint, waiting for the council's card (SaintsMath.CandidateDays at most).</summary>
    public class SaintCandidate : IExposable
    {
        public string pawnId, name, fullName;
        public Gender gender;
        public int god = -1;
        public Ideo ideo;
        public SaintReason reason;
        public int died = -1, feastDay = -1, home = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref fullName, "fullName");
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref died, "died", -1);
            Scribe_Values.Look(ref feastDay, "feastDay", -1);
            Scribe_Values.Look(ref home, "home", -1);
        }
    }

    /// <summary>
    /// A statue raised to a saint (Gods/Saints/SaintsPlaces.cs): the colony's own saint (<see cref="saint"/>), or one of a
    /// faith's saints by name, with the god the faith ties them to; its art's own title before, to give back.
    /// </summary>
    public class SaintPlace : IExposable
    {
        public Thing thing;
        /// <summary>The colony's saint (ColonySaint.id), or -1 for a faith's.</summary>
        public int saint = -1;
        /// <summary>A faith's saint: their name, the faith, the god it ties them to (God.id), the word set their title is in.</summary>
        public string name;
        public Ideo ideo;
        public int god = -1;
        public string set;
        /// <summary>Waiting for someone to go and say the words, and when it was asked.</summary>
        public bool pending;
        public int asked = -1, since = -1;
        public string byName, oldTitle;

        public bool Live => thing != null && !thing.Destroyed && thing.Spawned;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref saint, "saint", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref set, "set");
            Scribe_Values.Look(ref pending, "pending");
            Scribe_Values.Look(ref asked, "asked", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref byName, "byName");
            Scribe_Values.Look(ref oldTitle, "oldTitle");
        }
    }

    /// <summary>A faith's saint (or damned) the colony has seen among its own faiths, kept so the book can say one is kept no more.</summary>
    public class FaithSaintSeen : IExposable
    {
        public Ideo ideo;
        public string faithName, name, set;
        public int god = -1;
        public bool damned;
        public int first = -1, gone = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref faithName, "faithName");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref set, "set");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref damned, "damned");
            Scribe_Values.Look(ref first, "first", -1);
            Scribe_Values.Look(ref gone, "gone", -1);
        }
    }

    /// <summary>
    /// The saints, saved with the game (Gods/Saints/Saints.cs): the colony's own (SaintsMath.MaxSaints), the deaths waiting for
    /// the council's card (SaintsMath.MaxWaiting, SaintsMath.CandidateDays each), the statues raised to a saint
    /// (SaintsMath.MaxPlaces, gone with the statue), the faith's saints seen (SaintsMath.MaxSeen), and who took their faith's
    /// damned to heart (SaintsMath.MaxFeared).
    /// </summary>
    public class SaintsBook : GameComponent
    {
        public List<ColonySaint> saints = new List<ColonySaint>();
        public List<SaintCandidate> waiting = new List<SaintCandidate>();
        public List<SaintPlace> places = new List<SaintPlace>();
        public List<FaithSaintSeen> seen = new List<FaithSaintSeen>();
        public List<string> feared = new List<string>();
        public int nextId;

        private static SaintsBook cached;
        private static Game cachedGame;

        public SaintsBook(Game game)
        {
        }

        public static SaintsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<SaintsBook>();
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

        public ColonySaint ById(int id) => id < 0 ? null : saints.FirstOrDefault(s => s != null && s.id == id);

        public ColonySaint ByPawn(string pawnId) => pawnId.NullOrEmpty() ? null : saints.FirstOrDefault(s => s != null && s.pawnId == pawnId);

        public SaintPlace PlaceOf(Thing t) => t == null ? null : places.FirstOrDefault(p => p != null && p.thing == t);

        /// <summary>Everything kept within its bounds.</summary>
        public void Trim()
        {
            saints.RemoveAll(s => s == null);
            while (saints.Count > SaintsMath.MaxSaints) saints.RemoveAt(0);
            waiting.RemoveAll(c => c == null);
            while (waiting.Count > SaintsMath.MaxWaiting) waiting.RemoveAt(0);
            places.RemoveAll(p => p == null || p.thing == null || p.thing.Destroyed);
            while (places.Count > SaintsMath.MaxPlaces) places.RemoveAt(0);
            seen.RemoveAll(s => s == null);
            while (seen.Count > SaintsMath.MaxSeen)
            {
                var old = seen.FirstOrDefault(s => s.gone >= 0) ?? seen[0];
                seen.Remove(old);
            }
            while (feared.Count > SaintsMath.MaxFeared) feared.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Trim();
            Scribe_Collections.Look(ref saints, "saints", LookMode.Deep);
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            Scribe_Collections.Look(ref places, "places", LookMode.Deep);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Deep);
            Scribe_Collections.Look(ref feared, "feared", LookMode.Value);
            Scribe_Values.Look(ref nextId, "nextId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                saints ??= new List<ColonySaint>();
                waiting ??= new List<SaintCandidate>();
                places ??= new List<SaintPlace>();
                seen ??= new List<FaithSaintSeen>();
                feared ??= new List<string>();
                places.RemoveAll(p => p == null || p.thing == null);
                Trim();
            }
        }
    }
}
