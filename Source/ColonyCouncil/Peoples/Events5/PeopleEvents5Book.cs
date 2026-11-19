using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gravewardens' burial after the field stirred (item 35): where, which of the dead, the row of graves and how many have
    /// had their bell, and the colony's answer: "bury" (the fight and the burial), "promise" (and the colony's promise to bury
    /// its dead from now on), "away" (thanked and sent off).
    /// </summary>
    public class StirRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public string how = "bury";
        public List<Thing> dead = new List<Thing>();
        public List<IntVec3> plots = new List<IntVec3>();
        public int rot, rung, since = -1;
        public bool burying, done;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref how, "how", "bury");
            Scribe_Collections.Look(ref dead, "dead", LookMode.Reference);
            Scribe_Collections.Look(ref plots, "plots", LookMode.Value);
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref rung, "rung");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref burying, "burying");
            Scribe_Values.Look(ref done, "done");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dead ??= new List<Thing>();
                dead.RemoveAll(t => t == null);
                plots ??= new List<IntVec3>();
            }
        }
    }

    /// <summary>The raiders' people whose dead the Bell buried on the colony's land (item 36): their mourners are due a season on.</summary>
    public class MournRecord : IExposable
    {
        public Faction people, bell;
        public Map map;
        public int buried, due = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_References.Look(ref bell, "bell");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref buried, "buried");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// The colony's grave-garden (item 37): its bell-post, its ground (the middle and how far it reaches), and the work the
    /// sextons still have to do (the stones laid, the roses planted, the post raised); <see cref="whole"/> the whole garden,
    /// else the bell-post alone.
    /// </summary>
    public class GardenRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public Thing post;
        public IntVec3 center = IntVec3.Invalid, postCell = IntVec3.Invalid;
        public float radius = 8f;
        public List<IntVec3> stones = new List<IntVec3>();
        public List<IntVec3> roses = new List<IntVec3>();
        public int laid, planted, since = -1;
        public bool whole = true, worked;

        /// <summary>The garden stands: its bell-post is up on its map.</summary>
        public bool Stands => post != null && !post.Destroyed && post.Spawned && post.Map == map;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref post, "post");
            Scribe_Values.Look(ref center, "center", IntVec3.Invalid);
            Scribe_Values.Look(ref postCell, "postCell", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "radius", 8f);
            Scribe_Collections.Look(ref stones, "stones", LookMode.Value);
            Scribe_Collections.Look(ref roses, "roses", LookMode.Value);
            Scribe_Values.Look(ref laid, "laid");
            Scribe_Values.Look(ref planted, "planted");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref whole, "whole", true);
            Scribe_Values.Look(ref worked, "worked");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stones ??= new List<IntVec3>();
                roses ??= new List<IntVec3>();
            }
        }
    }

    /// <summary>
    /// An egg from the crater (item 41): the egg, of what, its keeper, who tended it and for how many hours, the beast once it
    /// hatched, and the tamer's coming back a season on.
    /// </summary>
    public class EggRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Thing egg;
        public string def, race;
        public Map map;
        public IntVec3 lastCell = IntVec3.Invalid;
        public Pawn keeper, beast;
        public List<Pawn> tenders = new List<Pawn>();
        public List<int> hours = new List<int>();
        public int since = -1, hatched = -1, back = -1;
        public bool lost, came, done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref egg, "egg");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref race, "race");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref lastCell, "lastCell", IntVec3.Invalid);
            Scribe_References.Look(ref keeper, "keeper");
            Scribe_References.Look(ref beast, "beast");
            Scribe_Collections.Look(ref tenders, "tenders", LookMode.Reference);
            Scribe_Collections.Look(ref hours, "hours", LookMode.Value);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref hatched, "hatched", -1);
            Scribe_Values.Look(ref back, "back", -1);
            Scribe_Values.Look(ref lost, "lost");
            Scribe_Values.Look(ref came, "came");
            Scribe_Values.Look(ref done, "done");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tenders ??= new List<Pawn>();
                hours ??= new List<int>();
                // The two lists go together: a tender lost to the save takes their hours with them.
                for (int i = tenders.Count - 1; i >= 0; i--)
                    if (tenders[i] == null)
                    {
                        tenders.RemoveAt(i);
                        if (i < hours.Count) hours.RemoveAt(i);
                    }
                while (hours.Count > tenders.Count) hours.RemoveAt(hours.Count - 1);
                while (hours.Count < tenders.Count) hours.Add(0);
            }
        }

        /// <summary>An hour's tending, to whoever it was.</summary>
        public void Tend(Pawn p)
        {
            if (p == null) return;
            int i = tenders.IndexOf(p);
            if (i < 0)
            {
                tenders.Add(p);
                hours.Add(1);
            }
            else hours[i]++;
        }

        /// <summary>The one who tended it most, else its keeper.</summary>
        public Pawn MostTended()
        {
            int i = PeopleEvents5Math.Most(hours.ToArray());
            var p = i >= 0 && i < tenders.Count ? tenders[i] : null;
            return p != null && !p.Dead ? p : keeper;
        }
    }

    /// <summary>
    /// A piece made better (item 44): whose, the piece, the fixer who took it, and how far along: "taking" (the fixer on the way
    /// to the colonist), "away" (at their cart, off the map), "back" (bringing it back), "done".
    /// </summary>
    public class BetterRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Map map;
        public Pawn fixer, owner;
        public Thing item;
        public string stage = "taking";
        public int quality = -1, since = -1, due = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref fixer, "fixer");
            Scribe_References.Look(ref owner, "owner");
            Scribe_References.Look(ref item, "item");
            Scribe_Values.Look(ref stage, "stage", "taking");
            Scribe_Values.Look(ref quality, "quality", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>A Wheel town moved near a home (item 46): its yard is due at the map's edge.</summary>
    public class YardRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public string town;
        public int due = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// What the peoples' own events of round 5 keep with the game (Peoples/Events5/): the gravewardens' burials and the
    /// colony's promise to the Bell, the mourners due, the colony's grave-gardens, the crater eggs, the pieces at the Wheel's
    /// cart, the yards due, and when the code-fired visits last came. The rest lives in the events' visits and in a camp's own
    /// state. A save from before has none of it.
    /// </summary>
    public class PeopleEvents5Book : GameComponent, IThingHolder
    {
        /// <summary>The pieces at the Wheel's cart (item 44), off every map while they are made better.</summary>
        public ThingOwner<Thing> held;

        public List<StirRecord> stirs = new List<StirRecord>();
        /// <summary>The colony promised the Bell to bury its dead (the tick, -1 for none), and whether it has heard it broken.</summary>
        public int promise = -1;
        public Faction promiseTo;
        public bool promiseBroken;
        public List<MournRecord> mourns = new List<MournRecord>();
        public List<GardenRecord> gardens = new List<GardenRecord>();
        public List<EggRecord> eggs = new List<EggRecord>();
        public int nextEgg = 1;
        public List<BetterRecord> betters = new List<BetterRecord>();
        public int nextBetter = 1;
        public List<YardRecord> yards = new List<YardRecord>();
        /// <summary>"mapId:tick" for each raid whose night the Wheel's crew was rolled for, so none is asked twice.</summary>
        public List<string> mendRaids = new List<string>();
        /// <summary>When the gravewardens and the night crew last came.</summary>
        public int lastStir = -1, lastMend = -1;

        private static PeopleEvents5Book cached;
        private static Game cachedGame;

        public PeopleEvents5Book(Game game)
        {
            held = new ThingOwner<Thing>(this);
        }

        public IThingHolder ParentHolder => null;

        public ThingOwner GetDirectlyHeldThings() => held;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public static PeopleEvents5Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents5Book>();
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
            PeopleEvents5.Forget();
        }

        public void AddStir(StirRecord r)
        {
            if (r == null) return;
            stirs.Add(r);
            while (stirs.Count > 4 && stirs.Any(x => x.done)) stirs.Remove(stirs.First(x => x.done));
            while (stirs.Count > 4) stirs.RemoveAt(0);
        }

        public void AddMourn(MournRecord r)
        {
            if (r?.people == null || mourns.Any(m => !m.done && m.people == r.people && m.map == r.map)) return;
            mourns.Add(r);
            while (mourns.Count > PeopleEvents5Math.MaxMourns && mourns.Any(x => x.done)) mourns.Remove(mourns.First(x => x.done));
            while (mourns.Count > PeopleEvents5Math.MaxMourns) mourns.RemoveAt(0);
        }

        public GardenRecord GardenAt(Map map) => gardens.FirstOrDefault(g => g != null && g.map == map);

        public void AddGarden(GardenRecord r)
        {
            if (r?.map == null) return;
            gardens.RemoveAll(g => g == null || g.map == r.map);
            gardens.Add(r);
            while (gardens.Count > 4) gardens.RemoveAt(0);
        }

        public EggRecord AddEgg(EggRecord r)
        {
            if (r == null) return null;
            r.id = nextEgg++;
            eggs.Add(r);
            while (eggs.Count > PeopleEvents5Math.MaxEggs && eggs.Any(x => x.done)) eggs.Remove(eggs.First(x => x.done));
            while (eggs.Count > PeopleEvents5Math.MaxEggs) eggs.RemoveAt(0);
            return r;
        }

        public BetterRecord AddBetter(BetterRecord r)
        {
            if (r == null) return null;
            r.id = nextBetter++;
            betters.Add(r);
            while (betters.Count > 4 && betters.Any(x => x.done)) betters.Remove(betters.First(x => x.done));
            return r;
        }

        public void AddYard(YardRecord r)
        {
            if (r?.map == null || yards.Any(y => !y.done && y.map == r.map)) return;
            yards.Add(r);
            while (yards.Count > 4 && yards.Any(x => x.done)) yards.Remove(yards.First(x => x.done));
            while (yards.Count > 4) yards.RemoveAt(0);
        }

        public bool MendRolled(Map map, int raid) => map != null && mendRaids.Contains(map.uniqueID + ":" + raid);

        public void NoteMendRaid(Map map, int raid)
        {
            if (map == null || MendRolled(map, raid)) return;
            mendRaids.Add(map.uniqueID + ":" + raid);
            while (mendRaids.Count > 8) mendRaids.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref stirs, "stirs", LookMode.Deep);
            Scribe_Values.Look(ref promise, "promise", -1);
            Scribe_References.Look(ref promiseTo, "promiseTo");
            Scribe_Values.Look(ref promiseBroken, "promiseBroken");
            Scribe_Collections.Look(ref mourns, "mourns", LookMode.Deep);
            Scribe_Collections.Look(ref gardens, "gardens", LookMode.Deep);
            Scribe_Collections.Look(ref eggs, "eggs", LookMode.Deep);
            Scribe_Values.Look(ref nextEgg, "nextEgg", 1);
            Scribe_Collections.Look(ref betters, "betters", LookMode.Deep);
            Scribe_Values.Look(ref nextBetter, "nextBetter", 1);
            Scribe_Collections.Look(ref yards, "yards", LookMode.Deep);
            Scribe_Collections.Look(ref mendRaids, "mendRaids", LookMode.Value);
            Scribe_Values.Look(ref lastStir, "lastStir", -1);
            Scribe_Values.Look(ref lastMend, "lastMend", -1);
            Scribe_Deep.Look(ref held, "held", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                held ??= new ThingOwner<Thing>(this);
                stirs ??= new List<StirRecord>();
                stirs.RemoveAll(r => r == null);
                mourns ??= new List<MournRecord>();
                mourns.RemoveAll(r => r?.people == null);
                gardens ??= new List<GardenRecord>();
                gardens.RemoveAll(r => r?.map == null);
                eggs ??= new List<EggRecord>();
                eggs.RemoveAll(r => r == null);
                betters ??= new List<BetterRecord>();
                betters.RemoveAll(r => r == null);
                yards ??= new List<YardRecord>();
                yards.RemoveAll(r => r == null);
                mendRaids ??= new List<string>();
            }
        }
    }
}
