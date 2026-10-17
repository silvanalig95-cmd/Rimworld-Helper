using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where a day on the colony calendar comes from.</summary>
    public enum CalendarKind
    {
        /// <summary>The day the colony was founded (the game's start).</summary>
        Founding,
        /// <summary>The colony's first good harvest.</summary>
        Harvest,
        /// <summary>The first snow: it comes round with the first snow of each winter, not on a date.</summary>
        FirstSnow,
        /// <summary>A battle the colony remembers: a big raid seen off, ours who fell, a nemesis brought down.</summary>
        Battle,
        /// <summary>The death of someone the colony loved.</summary>
        Death,
        Wedding,
        /// <summary>The day one of the colony's first members arrived (after the founding, within its first year).</summary>
        Arrival,
        /// <summary>A long-serving colonist's birthday.</summary>
        Birthday,
        /// <summary>A day the player named.</summary>
        Holiday,
        /// <summary>The day the first child was born into the colony (Biotech).</summary>
        FirstChild,
        /// <summary>The day the colony's walls went up: the first day it had a real wall around it.</summary>
        Walls,
        /// <summary>A caravan's safe return: the longest journey the colony's people came home from.</summary>
        Homecoming,
        /// <summary>The first harvest of a crop the colony had never grown before.</summary>
        NewCrop,
        /// <summary>The colony's first trade.</summary>
        FirstTrade,
        /// <summary>A great hunt: a day the hunters brought down a good many animals, with no raid to thank for it.</summary>
        GreatHunt,
        /// <summary>The first foal: the first animal born to the colony's own herd (Calendar/CalendarAnimals.cs).</summary>
        Foal,
        /// <summary>A beloved animal's day: the death of a bonded animal that was long with the colony, or fought for its person.</summary>
        Companion
    }

    /// <summary>How the colony keeps a day.</summary>
    public enum DayWay
    {
        Feast,
        Remembrance,
        Rest,
        Games,
        /// <summary>A birthday or an arrival: the person's own day, marked without a gathering.</summary>
        Personal,
        /// <summary>A fire at nightfall, built for the day and burned down to ash, with music.</summary>
        Bonfire,
        /// <summary>Small gifts given and taken, and friendships the better for it.</summary>
        Gifts,
        /// <summary>The hunters go out for the day's game, and come back with something for the pot.</summary>
        Hunt,
        /// <summary>The colony sows the fields together, and its grower says a few words at the field's edge (Calendar/CalendarPlanting.cs).</summary>
        Planting
    }

    /// <summary>
    /// One day on the colony calendar. People are kept by ThingID and name, never by reference, so a death, a departure
    /// or a pawn the game has let go of can't break a save: whoever is gone is simply not found, and the day is dropped
    /// (or, for the dead, remembered by name).
    /// </summary>
    public class CalendarDay : IExposable
    {
        /// <summary>What the day is, unique on the calendar: "founding", "harvest", "death|Human123", "holiday|3"...</summary>
        public string id;
        public CalendarKind kind;
        public DayWay way;
        /// <summary>The day's name as shown (the player may rename it).</summary>
        public string label;
        /// <summary>Day of the year, 0 to 59.</summary>
        public int day;
        /// <summary>The year it first happened (or was named).</summary>
        public int sinceYear;
        /// <summary>When it happened, in game ticks (for the page and the backfill).</summary>
        public int tick;
        /// <summary>The person the day is about (the dead, the birthday colonist, one of a couple) and the other of a couple.</summary>
        public string pawnId;
        public string pawnName;
        public string otherId;
        public string otherName;
        /// <summary>What the day is about besides a person: a new crop's name, the game of a great hunt.</summary>
        public string subject;
        /// <summary>A battle's enemy, how many of them fell, and the names of ours who fell.</summary>
        public string enemy;
        public int kills;
        public List<string> fallen = new List<string>();
        /// <summary>ThingIDs of those who knew the dead, or fought in the battle.</summary>
        public List<string> knew = new List<string>();
        /// <summary>ThingIDs of the dead's family and partner among them.</summary>
        public List<string> kin = new List<string>();
        /// <summary>What the day meant when it was written down (see CalendarMath).</summary>
        public float weight;
        /// <summary>The player named it.</summary>
        public bool byPlayer;
        /// <summary>The year it last came round (kept, let pass or missed), so it comes once a year.</summary>
        public int lastYear = -99999;
        /// <summary>Years kept in a row, times kept in all, a tradition, and traditions let pass in a row.</summary>
        public int keptYears;
        public int timesKept;
        public bool tradition;
        public int breaks;
        public int lastKeptYear = -99999;

        public bool Personal => way == DayWay.Personal;

        /// <summary>Days the colony writes down by itself (not the founding day, not the player's own).</summary>
        public bool Auto => !byPlayer && kind != CalendarKind.Founding;

        /// <summary>The founding day, the player's own days and traditions never make way for a new day.</summary>
        public bool Fixed => byPlayer || kind == CalendarKind.Founding || tradition;

        public float Standing => CalendarMath.Standing(weight, tradition, keptYears);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref way, "way");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref day, "day");
            Scribe_Values.Look(ref sinceYear, "sinceYear");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref pawnName, "pawnName");
            Scribe_Values.Look(ref otherId, "otherId");
            Scribe_Values.Look(ref otherName, "otherName");
            Scribe_Values.Look(ref enemy, "enemy");
            Scribe_Values.Look(ref subject, "subject");
            Scribe_Values.Look(ref kills, "kills");
            Scribe_Collections.Look(ref fallen, "fallen", LookMode.Value);
            Scribe_Collections.Look(ref knew, "knew", LookMode.Value);
            Scribe_Collections.Look(ref kin, "kin", LookMode.Value);
            Scribe_Values.Look(ref weight, "weight");
            Scribe_Values.Look(ref byPlayer, "byPlayer");
            Scribe_Values.Look(ref lastYear, "lastYear", -99999);
            Scribe_Values.Look(ref keptYears, "keptYears");
            Scribe_Values.Look(ref timesKept, "timesKept");
            Scribe_Values.Look(ref tradition, "tradition");
            Scribe_Values.Look(ref breaks, "breaks");
            Scribe_Values.Look(ref lastKeptYear, "lastKeptYear", -99999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (fallen == null) fallen = new List<string>();
                if (knew == null) knew = new List<string>();
                if (kin == null) kin = new List<string>();
                fallen.RemoveAll(s => s.NullOrEmpty());
                knew.RemoveAll(s => s.NullOrEmpty());
            }
        }
    }
}
