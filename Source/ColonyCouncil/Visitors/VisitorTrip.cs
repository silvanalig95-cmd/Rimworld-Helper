using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Where a group of visitors goes when they come.</summary>
    public enum VisitSpot
    {
        /// <summary>The colony's gate (where couriers wait), else where traders set down their goods.</summary>
        Gate,
        /// <summary>Open ground well away from the colony: a far meadow for a herd, a camp for the night.</summary>
        Far,
        /// <summary>The water nearest the colony (a herd to drink), else the far ground.</summary>
        Water,
        /// <summary>Just outside the colony, on their way past.</summary>
        Road
    }

    /// <summary>
    /// One visit (see <see cref="Visitors"/>): who comes, from which people, where they go and what they wait for. The
    /// people and animals are made when they set out and walk in from the edge; nobody is kept once they're gone.
    /// </summary>
    public class VisitorTrip : IExposable
    {
        public int id;
        public VisitKind kind;
        public VisitState state;
        public Map map;
        public Faction faction;

        // ---- Who ----

        /// <summary>What they are, for what they say and the messages: rider, family, travelers, mourners, drovers...</summary>
        public string what;
        public int count = 1;
        public bool family;
        public bool armed;
        public bool young;
        public Gender gender = Gender.None;
        public FloatRange ages = new FloatRange(18f, 50f);
        /// <summary>Came with next to nothing (robbed on the road): no weapon, few clothes.</summary>
        public bool robbed;
        /// <summary>A pack animal of their caravans (a cart's ox), for a people of the old ways.</summary>
        public bool pack;
        /// <summary>Their own animal ("dog", "goat", a kind): it joins with them.</summary>
        public string pet;
        /// <summary>Animals of a herd they drive, and the kind wanted first (a heifer: "Cow").</summary>
        public int livestock;
        public string beast;

        // ---- When and where ----

        public int created;
        public int due;
        public int giveUp;
        /// <summary>The hours they come in ([from, to); equal: any hour).</summary>
        public int hourFrom;
        public int hourTo;
        public VisitSpot spotKind;
        public IntVec3 spot = IntVec3.Invalid;
        /// <summary>On past the colony when they leave (toward this edge), not back the way they came.</summary>
        public bool onward;
        public IntVec3 exit = IntVec3.Invalid;
        public int setOut = -1;
        public int arrived = -1;
        public int endTick = -1;

        // ---- What they wait for ----

        /// <summary>The letter they wait on (its ID): -1 not found yet, -2 none (nothing was asked).</summary>
        public int letterId = -1;
        /// <summary>A world event's id (claims after a reload), or -1.</summary>
        public int situationId = -1;
        /// <summary>Hours they stay when nothing is asked (a blessing at the gate), or a passing group's stop.</summary>
        public float hours;
        public int answeredAt = -1;
        /// <summary>Guests: they go on at this tick.</summary>
        public int leaveAt = -1;
        /// <summary>Nights they stay once the answer says so (0: none), and where (their own spot, or another).</summary>
        public int stayNights;
        public VisitSpot staySpot = VisitSpot.Gate;
        public bool saidWait;
        /// <summary>A world event's beat that tells of this visit afterward ("the retinue stayed the night"): held until they've gone.</summary>
        public string holdBeat;

        // ---- The people ----

        public List<Pawn> people = new List<Pawn>();
        public List<Pawn> animals = new List<Pawn>();
        public Lord lord;

        public VisitEnd end;
        public bool harmed;

        /// <summary>The context of the letter they came with, until its ID is found (not saved).</summary>
        [Unsaved] public DilemmaContext ctxRef;

        public bool MapThere => map != null && Find.Maps.Contains(map);

        public IEnumerable<Pawn> Party
        {
            get
            {
                foreach (var p in people)
                    if (p != null) yield return p;
                foreach (var a in animals)
                    if (a != null) yield return a;
            }
        }

        /// <summary>Those still on this map, alive and in the visit's group.</summary>
        public List<Pawn> Here => Party.Where(p => !p.Dead && p.Spawned && p.Map == map && lord != null && p.GetLord() == lord).ToList();

        /// <summary>The people (not the animals) still with the group and on their feet.</summary>
        public List<Pawn> Standing => people.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && lord != null && p.GetLord() == lord).ToList();

        public bool OnMap => state == VisitState.Coming || state == VisitState.Here || state == VisitState.Leaving;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref state, "state");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref family, "family");
            Scribe_Values.Look(ref armed, "armed");
            Scribe_Values.Look(ref young, "young");
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref ages, "ages", new FloatRange(18f, 50f));
            Scribe_Values.Look(ref robbed, "robbed");
            Scribe_Values.Look(ref pack, "pack");
            Scribe_Values.Look(ref pet, "pet");
            Scribe_Values.Look(ref livestock, "livestock");
            Scribe_Values.Look(ref beast, "beast");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref giveUp, "giveUp");
            Scribe_Values.Look(ref hourFrom, "hourFrom");
            Scribe_Values.Look(ref hourTo, "hourTo");
            Scribe_Values.Look(ref spotKind, "spotKind");
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref onward, "onward");
            Scribe_Values.Look(ref exit, "exit", IntVec3.Invalid);
            Scribe_Values.Look(ref setOut, "setOut", -1);
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref letterId, "letterId", -1);
            Scribe_Values.Look(ref situationId, "situationId", -1);
            Scribe_Values.Look(ref hours, "hours");
            Scribe_Values.Look(ref answeredAt, "answeredAt", -1);
            Scribe_Values.Look(ref leaveAt, "leaveAt", -1);
            Scribe_Values.Look(ref stayNights, "stayNights");
            Scribe_Values.Look(ref staySpot, "staySpot", VisitSpot.Gate);
            Scribe_Values.Look(ref saidWait, "saidWait");
            Scribe_Values.Look(ref holdBeat, "holdBeat");
            Scribe_Collections.Look(ref people, "people", LookMode.Reference);
            Scribe_Collections.Look(ref animals, "animals", LookMode.Reference);
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref harmed, "harmed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (people == null) people = new List<Pawn>();
                people.RemoveAll(p => p == null);
                if (animals == null) animals = new List<Pawn>();
                animals.RemoveAll(p => p == null);
            }
        }
    }
}
