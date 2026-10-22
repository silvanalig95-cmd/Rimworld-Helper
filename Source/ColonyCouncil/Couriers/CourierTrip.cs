using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    public enum CourierState
    {
        /// <summary>Not on the map yet: they come some hours later, by day, when it's safe.</summary>
        Pending,
        /// <summary>Walking in from the edge to the gate.</summary>
        Coming,
        /// <summary>At the gate: setting down what they brought, fetching what they came for, waiting for an errand.</summary>
        Working,
        /// <summary>Walking off the map.</summary>
        Leaving,
        /// <summary>Over: kept a little while, so what they carry off is cleared when they're gone.</summary>
        Done
    }

    /// <summary>
    /// One courier trip (see <see cref="Couriers"/>): who comes, from which people, what they bring and what they take.
    /// Things they bring wait in <see cref="goods"/> until the people are made, then ride in their packs; what they take
    /// is counted in <see cref="taken"/>. A trip settles at most once (<see cref="settled"/>), so nothing is given or
    /// taken twice whatever happens to them.
    /// </summary>
    public class CourierTrip : IExposable, IThingHolder
    {
        public int id;
        public CourierKind kind;
        public CourierState state;
        public Map map;
        /// <summary>The event's own people, if any: they send it when they can.</summary>
        public Faction preferred;
        /// <summary>Who sends it (chosen when they set out).</summary>
        public Faction faction;
        public int created;
        /// <summary>They come no earlier than this...</summary>
        public int due;
        /// <summary>...and if they can't come by this, it's settled the old way.</summary>
        public int giveUp;
        /// <summary>When they reached the gate, and when their business was done (-1: not yet).</summary>
        public int arrived = -1;
        public int doneTick = -1;
        public int endTick = -1;
        /// <summary>Where they hand over and wait: the colony's gate, else where traders leave their goods.</summary>
        public IntVec3 spot = IntVec3.Invalid;
        public bool atGate;
        /// <summary>Where the old way would have set the things down (the caller's spot, else the trade spot).</summary>
        public IntVec3 oldSpot = IntVec3.Invalid;

        // ---- What they bring ----

        /// <summary>Things for the colony that haven't been put in anyone's pack yet.</summary>
        public ThingOwner<Thing> goods;
        /// <summary>"150 silver", "a longsword and 40 steel": what they bring, for the messages.</summary>
        public string bringLabel;
        /// <summary>Those of the party (the pack animal too) still carrying things for the colony.</summary>
        public List<Pawn> toUnload = new List<Pawn>();
        /// <summary>Things set down for the colony, in all.</summary>
        public int unloaded;
        /// <summary>The kind of goods they bring (silver, food, medicine, goods), for what they say.</summary>
        public string bringTag;

        // ---- What they take ----

        /// <summary>From the stores: "silver", "rawfood", "medicine", "meals", "wood", or a defName; empty: <see cref="def"/>.</summary>
        public string what;
        public ThingDef def;
        public int amount;
        public int taken;
        /// <summary>Particular things to take (a neighbour's cargo lying on the colony's land), instead of from the stores.</summary>
        public List<Thing> things = new List<Thing>();
        public string takeLabel;
        public string takeTag;
        /// <summary>What it's called without a number ("cooked meals"), for a word on part of it.</summary>
        public string takeNoun;

        /// <summary>Where the goods come from: the stores by PlayCarry's words (null), or a mission's goods ("mission": Missions.Matching).</summary>
        public string source;

        // ---- Someone handed over to them ----

        /// <summary>A prisoner sold to them, an animal given back: they come for them, and they leave with them.</summary>
        public Pawn fetch;
        public bool fetchPrisoner;
        /// <summary>A prisoner's own people, given back to them once they're off the map.</summary>
        public Faction fetchFaction;
        public bool fetched;
        /// <summary>The one they came for was gone (dead, freed, recruited): they don't pay, and go.</summary>
        public bool fetchFailed;

        // ---- The event's own visitor, carrying it in ----

        /// <summary>
        /// Not a courier at all: the event's own visitor (people[0], in their own group) has it in their pack and sets it
        /// down when they reach the colony (at <see cref="spot"/> if given).
        /// </summary>
        public bool bearerTrip;
        public int nextTry;

        // ---- A letter that waits for them ----

        /// <summary>A letter about what they bring (a venture come home, a fair's takings), sent when they set it down.</summary>
        public string letterLabel;
        public string letterText;
        public LetterDef letterDef;
        public string chronicle;
        public Pawn chroniclePawn;

        // ---- An errand's courier ----

        /// <summary>The errand (PlayOut) whose things a colonist carries out to them.</summary>
        public int playId = -1;
        /// <summary>The errand handed its things over.</summary>
        public bool received;

        // ---- The people ----

        public List<Pawn> people = new List<Pawn>();
        public Pawn animal;
        public Lord lord;

        public CourierEnd end;
        /// <summary>The colony hurt, arrested or killed one of them.</summary>
        public bool harmed;
        /// <summary>What was left of the deal has been settled (once only).</summary>
        public bool settled;
        /// <summary>No messages (a trip settled when the setting was off).</summary>
        public bool quiet;

        public CourierTrip()
        {
            goods = new ThingOwner<Thing>(this);
        }

        /// <summary>They bring something (a delivery, a sale's payment, an errand's courier bringing the price).</summary>
        public bool Brings => kind == CourierKind.Deliver || kind == CourierKind.Exchange || !bringLabel.NullOrEmpty();

        public bool Takes => kind == CourierKind.Collect || kind == CourierKind.Exchange;

        /// <summary>Waiting to take someone away: they pay and finish once that's done.</summary>
        public bool FetchPending => fetch != null && !fetched && !fetchFailed;

        /// <summary>Still owed from the stores (or of the particular things).</summary>
        public int LeftToTake => CourierMath.Left(amount, taken);

        public bool MapThere => map != null && Find.Maps.Contains(map);

        /// <summary>Everyone of the trip, the pack animal too.</summary>
        public IEnumerable<Pawn> Party
        {
            get
            {
                foreach (var p in people)
                    if (p != null) yield return p;
                if (animal != null) yield return animal;
            }
        }

        /// <summary>Those still on this map, alive and in the trip's group.</summary>
        public List<Pawn> Here => Party.Where(p => !p.Dead && p.Spawned && p.Map == map && lord != null && p.GetLord() == lord).ToList();

        /// <summary>The people (not the animal) still on the job.</summary>
        public List<Pawn> Bearers => people.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && lord != null && p.GetLord() == lord).ToList();

        /// <summary>Things still to be set down by someone who is here to do it (or not given out yet).</summary>
        public bool StillCarrying => goods.Count > 0 || toUnload.Any(p => p != null && !p.Dead && p.Spawned && p.Map == map);

        public IThingHolder ParentHolder => null;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings() => goods;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref state, "state");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref preferred, "preferred");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref giveUp, "giveUp");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref doneTick, "doneTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref atGate, "atGate");
            Scribe_Values.Look(ref oldSpot, "oldSpot", IntVec3.Invalid);
            Scribe_Deep.Look(ref goods, "goods", this);
            Scribe_Values.Look(ref bringLabel, "bringLabel");
            Scribe_Collections.Look(ref toUnload, "toUnload", LookMode.Reference);
            Scribe_Values.Look(ref unloaded, "unloaded");
            Scribe_Values.Look(ref bringTag, "bringTag");
            Scribe_Values.Look(ref what, "what");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref taken, "taken");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Values.Look(ref takeLabel, "takeLabel");
            Scribe_Values.Look(ref takeTag, "takeTag");
            Scribe_Values.Look(ref takeNoun, "takeNoun");
            Scribe_Values.Look(ref source, "source");
            Scribe_References.Look(ref fetch, "fetch");
            Scribe_Values.Look(ref fetchPrisoner, "fetchPrisoner");
            Scribe_References.Look(ref fetchFaction, "fetchFaction");
            Scribe_Values.Look(ref fetched, "fetched");
            Scribe_Values.Look(ref fetchFailed, "fetchFailed");
            Scribe_Values.Look(ref bearerTrip, "bearerTrip");
            Scribe_Values.Look(ref nextTry, "nextTry");
            Scribe_Values.Look(ref letterLabel, "letterLabel");
            Scribe_Values.Look(ref letterText, "letterText");
            Scribe_Defs.Look(ref letterDef, "letterDef");
            Scribe_Values.Look(ref chronicle, "chronicle");
            Scribe_References.Look(ref chroniclePawn, "chroniclePawn");
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_Values.Look(ref received, "received");
            Scribe_Collections.Look(ref people, "people", LookMode.Reference);
            Scribe_References.Look(ref animal, "animal");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref settled, "settled");
            Scribe_Values.Look(ref quiet, "quiet");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (goods == null) goods = new ThingOwner<Thing>(this);
                if (things == null) things = new List<Thing>();
                things.RemoveAll(t => t == null);
                if (people == null) people = new List<Pawn>();
                people.RemoveAll(p => p == null);
                if (toUnload == null) toUnload = new List<Pawn>();
                toUnload.RemoveAll(p => p == null);
            }
        }
    }
}
