using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A new colony being founded from one of the homes (Colonies/Foundings.cs): the place, who goes from the colony and by
    /// what means, whether newcomers come, whether the old colony is kept (and the seat moved) or abandoned, and how far
    /// it has come. One at a time. Its journey (a caravan) is the journey planner's own plan, by its id.
    /// </summary>
    public class FoundingPlan : IExposable
    {
        public int id;
        /// <summary>The home it sets out from (its key), the colony the new one is founded from.</summary>
        public int from = HomesMath.None;
        public string fromName;
        /// <summary>The place chosen on the world map: its tile on the home's layer.</summary>
        public int tile = -1;
        public int layer;
        public string place;
        public FoundingMeans means;
        public FoundingStage stage;
        /// <summary>The old colony kept as a second home; else abandoned once the new one stands and those who go have left.</summary>
        public bool keepOld = true;
        /// <summary>Make the new colony the seat once it stands (by the seat's own rule, at most once a season).</summary>
        public bool makeSeat;
        /// <summary>Newcomers come (made with the game's own window for new people when the colony stands, or at once with nobody going).</summary>
        public bool newcomers;
        public bool newcomersChosen;
        /// <summary>The colony's own who go.</summary>
        public List<Pawn> party = new List<Pawn>();
        /// <summary>Those sent against their wish, and those kept against it: each feels uprooted for a season once the colony stands.</summary>
        public List<Pawn> sentAgainst = new List<Pawn>();
        public List<Pawn> keptAgainst = new List<Pawn>();
        public int started = -1;
        public int founded = -1;
        /// <summary>The new home's key once it stands.</summary>
        public int home = HomesMath.None;
        /// <summary>The journey plan that takes the party (a caravan), by its id, or -1.</summary>
        public int journey = -1;
        /// <summary>The game's colony limit as Rimfolk raised it for this founding (decision 5B), or -1 when it wasn't raised.</summary>
        public int limitRaisedTo = -1;
        /// <summary>The caravan last asked whether to settle here (its id), so it is asked once.</summary>
        public int askedCaravan = -1;
        /// <summary>What has been done once the colony stands: the moods, the first night, the old colony abandoned, the newcomers asked for.</summary>
        public bool moodsGiven, firstNight, abandoned, newcomersAsked;
        /// <summary>The seat moved here as it stood; someone of the party still on the old map was told of once.</summary>
        public bool seatMoved, waitTold;

        public PlanetTile Tile => tile < 0 ? PlanetTile.Invalid : new PlanetTile(tile, layer);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref from, "from", HomesMath.None);
            Scribe_Values.Look(ref fromName, "fromName");
            Scribe_Values.Look(ref tile, "tile", -1);
            Scribe_Values.Look(ref layer, "layer");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref means, "means");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref keepOld, "keepOld", true);
            Scribe_Values.Look(ref makeSeat, "makeSeat");
            Scribe_Values.Look(ref newcomers, "newcomers");
            Scribe_Values.Look(ref newcomersChosen, "newcomersChosen");
            Scribe_Collections.Look(ref party, "party", LookMode.Reference);
            Scribe_Collections.Look(ref sentAgainst, "sentAgainst", LookMode.Reference);
            Scribe_Collections.Look(ref keptAgainst, "keptAgainst", LookMode.Reference);
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref journey, "journey", -1);
            Scribe_Values.Look(ref limitRaisedTo, "limitRaisedTo", -1);
            Scribe_Values.Look(ref askedCaravan, "askedCaravan", -1);
            Scribe_Values.Look(ref moodsGiven, "moodsGiven");
            Scribe_Values.Look(ref firstNight, "firstNight");
            Scribe_Values.Look(ref abandoned, "abandoned");
            Scribe_Values.Look(ref newcomersAsked, "newcomersAsked");
            Scribe_Values.Look(ref seatMoved, "seatMoved");
            Scribe_Values.Look(ref waitTold, "waitTold");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                party ??= new List<Pawn>();
                party.RemoveAll(p => p == null);
                sentAgainst ??= new List<Pawn>();
                sentAgainst.RemoveAll(p => p == null);
                keptAgainst ??= new List<Pawn>();
                keptAgainst.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Someone sent or kept against their wish with the old colony kept: they may ask, once in the season after the
    /// founding, to move to the colony they wanted (decision 2A).
    /// </summary>
    public class FoundingAsk : IExposable
    {
        public Pawn pawn;
        /// <summary>The colony they would rather live at (its key).</summary>
        public int wants = HomesMath.None;
        public int founded = -1;
        public bool asked;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref wants, "wants", HomesMath.None);
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>
    /// What a fresh start keeps between hours, on the homes' own component (Lean/Homes.cs): the founding under way, the
    /// questions about moving still to come, when the seat last moved, and when newcomers last came with their passage
    /// paid. A save of one colony writes none of it.
    /// </summary>
    public partial class Homes
    {
        public List<FoundingPlan> foundings = new List<FoundingPlan>();
        public List<FoundingAsk> asks = new List<FoundingAsk>();
        /// <summary>When the seat last moved (decision 1A: at most once a season), or -1.</summary>
        public int seatMovedAt = -1;
        /// <summary>When newcomers last came with the old colony kept and their passage paid (decision 4A: at most once a year), or -1.</summary>
        public int newcomersPaidAt = -1;
        public int nextFounding = 1;

        private void ExposeFounding()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foundings.RemoveAll(f => f == null);
                asks.RemoveAll(a => a?.pawn == null || a.asked);
            }
            if (Scribe.mode != LoadSaveMode.Saving || foundings.Count > 0) Scribe_Collections.Look(ref foundings, "foundings", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.Saving || asks.Count > 0) Scribe_Collections.Look(ref asks, "foundingAsks", LookMode.Deep);
            Scribe_Values.Look(ref seatMovedAt, "seatMovedAt", -1);
            Scribe_Values.Look(ref newcomersPaidAt, "newcomersPaidAt", -1);
            Scribe_Values.Look(ref nextFounding, "nextFounding", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                foundings ??= new List<FoundingPlan>();
                foundings.RemoveAll(f => f == null || f.tile < 0);
                asks ??= new List<FoundingAsk>();
                asks.RemoveAll(a => a?.pawn == null);
            }
        }
    }
}
