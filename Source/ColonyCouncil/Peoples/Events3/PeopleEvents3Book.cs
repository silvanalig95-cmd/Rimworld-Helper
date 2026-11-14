using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The league's page for the colony (item 2): the wrong written in red, what is owed for it, and how the colony answered.</summary>
    public class RedInkRecord : IExposable
    {
        public int id;
        /// <summary>The league whose factor came, and the people the colony wronged.</summary>
        public Faction league, wronged;
        /// <summary>The wrong in the wronged people's memory (FactionDeed.id) and its kind.</summary>
        public int deed;
        public string kind;
        public int owed;
        public int since = -1;
        /// <summary>"refused" (worse terms until paid), "twice" (written twice, worse terms until paid), "paid", "struck", "lapsed".</summary>
        public string state;
        public bool done;

        /// <summary>The league trades at worse terms while this stands.</summary>
        public bool Open => !done && (state == "refused" || state == "twice");

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref league, "league");
            Scribe_References.Look(ref wronged, "wronged");
            Scribe_Values.Look(ref deed, "deed");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref owed, "owed");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>A shrine the pilgrims raised on the colony's ground (item 15): where, at what, and how it is kept.</summary>
    public class OldShrineRecord : IExposable
    {
        public Map map;
        public Thing shrine;
        public IntVec3 cell = IntVec3.Invalid;
        public Faction faction;
        /// <summary>What it was raised at ("ancient", "grave", "tree", "landmark") and its name.</summary>
        public string holy, holyName;
        public int built = -1, lastStop = -1, mournedAt = -1;
        /// <summary>Fallen to ruin (or gone): mourned, and no pilgrim stops there until it is kept again.</summary>
        public bool ruined, gone;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref shrine, "shrine");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref holy, "holy");
            Scribe_Values.Look(ref holyName, "holyName");
            Scribe_Values.Look(ref built, "built", -1);
            Scribe_Values.Look(ref lastStop, "lastStop", -1);
            Scribe_Values.Look(ref mournedAt, "mournedAt", -1);
            Scribe_Values.Look(ref ruined, "ruined");
            Scribe_Values.Look(ref gone, "gone");
        }
    }

    /// <summary>
    /// Beasts of the colony driven off by raiders (item 11): by whom, when, which, and how far the herders' riders took it.
    /// Its stage: "driving" (the raiders still leading them across the map), "taken" (gone off the map with them),
    /// "asked" (the riders at the gate), "alone" (the riders gone after them by themselves), "ride" (the colony's rider
    /// with them, the thieves' camp on the world map), "over".
    /// </summary>
    public class TheftRecord : IExposable
    {
        public int id;
        public Map map;
        public Faction thieves;
        public List<Pawn> animals = new List<Pawn>();
        public int tick = -1, due = -1, tries;
        public string stage = "driving";
        /// <summary>The thieves' camp on the world map (a site's id), while the colony's rider is on the trail (-1: none).</summary>
        public int site = -1;
        /// <summary>The riders' people, once they came.</summary>
        public Faction herders;
        /// <summary>Whether the riders' coming was rolled (once a theft).</summary>
        public bool rolled;
        public bool done;

        public bool Open => !done && stage != "over";

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref thieves, "thieves");
            Scribe_References.Look(ref herders, "herders");
            Scribe_Collections.Look(ref animals, "animals", LookMode.Reference);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref tries, "tries");
            Scribe_Values.Look(ref stage, "stage", "driving");
            Scribe_Values.Look(ref site, "site", -1);
            Scribe_Values.Look(ref rolled, "rolled");
            Scribe_Values.Look(ref done, "done");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                animals ??= new List<Pawn>();
                animals.RemoveAll(p => p == null);
                stage ??= "over";
            }
        }
    }

    /// <summary>
    /// What the peoples' own events of round 3 keep with the game (Peoples/Events3/): the league's red ink, the shrines the
    /// pilgrims raised, the beasts the colony's thieves drove off, how many raiders the order carried away or saw kept, the
    /// sick room standing, the faiths settled at one table, and when the code-fired visits last came. The rest lives in the
    /// events' visits (the litters, the sick room's days, a drove crossing, the trail, the shrine's work, a pilgrim's stop) and
    /// in a camp's own state. A save from before has none of it.
    /// </summary>
    public class PeopleEvents3Book : GameComponent
    {
        public List<RedInkRecord> redInk = new List<RedInkRecord>();
        public int nextRedInk = 1;
        public List<OldShrineRecord> shrines = new List<OldShrineRecord>();
        public List<TheftRecord> thefts = new List<TheftRecord>();
        public int nextTheft = 1;
        /// <summary>The order's book: raiders it carried to the sickhouse from the colony, and raiders the colony kept.</summary>
        public int carried, kept;
        /// <summary>The sick room: its map's id and until when it stands (-1: none).</summary>
        public int sickRoomMap = -1, sickRoomUntil = -1;
        /// <summary>Two faiths settled at one table: until when the clash rests (-1: none).</summary>
        public int settledUntil = -1;
        /// <summary>The palmer's evening waiting for its end: its gathering's id, what the roll gave, why, the two faiths by name, since when, the palmer, their people.</summary>
        public int tablePlay = -1, tableSince = -1;
        public bool tableSplit;
        public string tableWhy, tableFaiths;
        public Pawn tablePalmer;
        public Faction tableFaction;
        /// <summary>When the litters last came, and the thieves' trail.</summary>
        public int lastLitters = -1, lastTrail = -1;

        private static PeopleEvents3Book cached;
        private static Game cachedGame;

        public PeopleEvents3Book(Game game)
        {
        }

        public static PeopleEvents3Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents3Book>();
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
            PeopleEvents3.Forget();
        }

        public RedInkRecord AddRedInk(RedInkRecord r)
        {
            if (r == null) return null;
            r.id = nextRedInk++;
            redInk.Add(r);
            while (redInk.Count > PeopleEvents3Math.MaxRedInk && redInk.Any(x => x.done)) redInk.Remove(redInk.First(x => x.done));
            return r;
        }

        public void AddShrine(OldShrineRecord r)
        {
            if (r == null) return;
            shrines.Add(r);
            while (shrines.Count > PeopleEvents3Math.MaxShrines && shrines.Any(x => x.gone)) shrines.Remove(shrines.First(x => x.gone));
            while (shrines.Count > PeopleEvents3Math.MaxShrines) shrines.RemoveAt(0);
        }

        public TheftRecord AddTheft(TheftRecord r)
        {
            if (r == null) return null;
            r.id = nextTheft++;
            thefts.Add(r);
            while (thefts.Count > PeopleEvents3Math.MaxThefts && thefts.Any(x => x.done)) thefts.Remove(thefts.First(x => x.done));
            while (thefts.Count > PeopleEvents3Math.MaxThefts) thefts.RemoveAt(0);
            return r;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref redInk, "redInk", LookMode.Deep);
            Scribe_Values.Look(ref nextRedInk, "nextRedInk", 1);
            Scribe_Collections.Look(ref shrines, "shrines", LookMode.Deep);
            Scribe_Collections.Look(ref thefts, "thefts", LookMode.Deep);
            Scribe_Values.Look(ref nextTheft, "nextTheft", 1);
            Scribe_Values.Look(ref carried, "carried");
            Scribe_Values.Look(ref kept, "kept");
            Scribe_Values.Look(ref sickRoomMap, "sickRoomMap", -1);
            Scribe_Values.Look(ref sickRoomUntil, "sickRoomUntil", -1);
            Scribe_Values.Look(ref settledUntil, "settledUntil", -1);
            Scribe_Values.Look(ref tablePlay, "tablePlay", -1);
            Scribe_Values.Look(ref tableSince, "tableSince", -1);
            Scribe_Values.Look(ref tableSplit, "tableSplit");
            Scribe_Values.Look(ref tableWhy, "tableWhy");
            Scribe_Values.Look(ref tableFaiths, "tableFaiths");
            Scribe_References.Look(ref tablePalmer, "tablePalmer");
            Scribe_References.Look(ref tableFaction, "tableFaction");
            Scribe_Values.Look(ref lastLitters, "lastLitters", -1);
            Scribe_Values.Look(ref lastTrail, "lastTrail", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                redInk ??= new List<RedInkRecord>();
                redInk.RemoveAll(r => r == null);
                shrines ??= new List<OldShrineRecord>();
                shrines.RemoveAll(r => r == null);
                thefts ??= new List<TheftRecord>();
                thefts.RemoveAll(r => r == null);
            }
        }
    }
}
