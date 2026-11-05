using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One guest of a feast (Feasts.cs): the notable invited (or come for a wedding), their people and town, their letter,
    /// whether they come, when they can be there, who walks in with them, what they hope for, and how they were welcomed.
    /// </summary>
    public class FeastGuest : IExposable
    {
        public int notable = -1;
        public Pawn pawn;
        public string name;
        public Faction people;
        /// <summary>Their town's world object ID, or -1.</summary>
        public int town = -1;
        public GuestState state;
        /// <summary>Their invitation's letter (NeighbourLetter.id), or -1 (invited otherwise: a wedding's guests).</summary>
        public int letter = -1;
        /// <summary>The earliest they can be at the colony once they said yes, or -1.</summary>
        public int comeBy = -1;
        /// <summary>One or two of their people who walk in with them.</summary>
        public List<Pawn> retinue = new List<Pawn>();
        public FeastHope hope;
        /// <summary>Their welcome: a meal of the colony's at the feast, a fine one, a bed or the floor for the night, a bout's real blow, a night gone sour, the prize.</summary>
        public bool ate, fine, bed, floor, hurt, sour, prize;
        /// <summary>How their regard moved, once it is over.</summary>
        public float welcome;

        public bool Has(Pawn p) => p != null && (pawn == p || retinue.Contains(p));

        public IEnumerable<Pawn> Party
        {
            get
            {
                if (pawn != null) yield return pawn;
                foreach (var r in retinue)
                    if (r != null) yield return r;
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (pawn != null && pawn.Discarded) pawn = null;
                retinue?.RemoveAll(r => r == null || r.Discarded);
            }
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref letter, "letter", -1);
            Scribe_Values.Look(ref comeBy, "comeBy", -1);
            Scribe_Collections.Look(ref retinue, "retinue", LookMode.Reference);
            Scribe_Values.Look(ref hope, "hope");
            Scribe_Values.Look(ref ate, "ate");
            Scribe_Values.Look(ref fine, "fine");
            Scribe_Values.Look(ref bed, "bed");
            Scribe_Values.Look(ref floor, "floor");
            Scribe_Values.Look(ref hurt, "hurt");
            Scribe_Values.Look(ref sour, "sour");
            Scribe_Values.Look(ref prize, "prize");
            Scribe_Values.Look(ref welcome, "welcome");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                retinue ??= new List<Pawn>();
                retinue.RemoveAll(r => r == null);
            }
        }
    }

    /// <summary>How one contest went: who won it, of the colony or of the guests' people, by how much, among how many.</summary>
    public class FeastResult : IExposable
    {
        public ContestKind kind;
        public Pawn winner;
        public string winnerName;
        /// <summary>Won by one of the colony's.</summary>
        public bool ours;
        /// <summary>The winner's people when a guest won.</summary>
        public Faction people;
        /// <summary>Bouts won, the time of the race, kills, games won, hits.</summary>
        public int count;
        public int entrants;
        /// <summary>It never came about (the weather, the light, nobody to take part, a raid), and why, in words.</summary>
        public bool off;
        public string why;
        /// <summary>A game at the tables: what was played ("chess").</summary>
        public string what;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && winner != null && winner.Discarded) winner = null;
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref winner, "winner");
            Scribe_Values.Look(ref winnerName, "winnerName");
            Scribe_Values.Look(ref ours, "ours");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref entrants, "entrants");
            Scribe_Values.Look(ref off, "off");
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref what, "what");
        }
    }

    /// <summary>
    /// A home's feast with guests (Feasts.cs; docs/design/stage2-diplomacy.md, item 5): why the guests come (a feast, or a
    /// wedding of release 2b.2, which hosts its guests here), who they are, the contests and the prize, the day, where it
    /// stands, and how each contest went. One at a time per home; the last one is kept for its card.
    /// </summary>
    public class GuestFeast : IExposable
    {
        public int id;
        public FeastPurpose purpose;
        /// <summary>What another part of the mod hosts the guests for (a wedding's key), or null.</summary>
        public string tag;
        public FeastStage stage;
        public int created = -1;
        /// <summary>The history's entry for the invitations, and the one for the feast held (-1: none yet).</summary>
        public int entry = -1, heldEntry = -1;
        public List<FeastGuest> guests = new List<FeastGuest>();
        public List<ContestKind> contests = new List<ContestKind>();
        public Thing prize;
        public string prizeName;
        /// <summary>Who wrote the invitations.</summary>
        public string writerName;
        /// <summary>The day it is set for (a day number: the year's days and years), or -1 while answers are out.</summary>
        public int day = -1;
        /// <summary>When the guests came; the contest under way (its index) and when it began; when the night began.</summary>
        public int arrived = -1, contest, contestStart = -1, nightAt = -1, ended = -1;
        /// <summary>The feast's gathering (PlayOut.id), or -1; it was held; music was played for the guests (the game's concert).</summary>
        public int playId = -1;
        public bool held, music;
        public List<FeastResult> results = new List<FeastResult>();
        /// <summary>The champion of the day, and whether the prize was carried to them.</summary>
        public Pawn champion;
        public string championName;
        public bool prizeGiven;
        /// <summary>Called off, and why in words (the weather, a raid, the colony's own word).</summary>
        public bool calledOff;
        public string offWhy;

        public bool Live => stage != FeastStage.Done;

        /// <summary>The guests are on the map (from their arrival to their leaving).</summary>
        public bool OnMap => stage == FeastStage.Here || stage == FeastStage.Contests || stage == FeastStage.Feasting || stage == FeastStage.Night;

        public FeastGuest GuestOf(Pawn p)
        {
            if (p == null) return null;
            foreach (var g in guests)
                if (g != null && g.Has(p))
                    return g;
            return null;
        }

        public IEnumerable<Pawn> AllGuests
        {
            get
            {
                foreach (var g in guests)
                    if (g != null && g.state == GuestState.Here)
                        foreach (var p in g.Party)
                            yield return p;
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                guests?.RemoveAll(g => g == null);
                results?.RemoveAll(r => r == null);
                if (prize != null && prize.Destroyed) prize = null;
                if (champion != null && champion.Discarded) champion = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref purpose, "purpose");
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref created, "created", -1);
            Scribe_Values.Look(ref entry, "entry", -1);
            Scribe_Values.Look(ref heldEntry, "heldEntry", -1);
            Scribe_Collections.Look(ref guests, "guests", LookMode.Deep);
            Scribe_Collections.Look(ref contests, "contests", LookMode.Value);
            Scribe_References.Look(ref prize, "prize");
            Scribe_Values.Look(ref prizeName, "prizeName");
            Scribe_Values.Look(ref writerName, "writerName");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref contest, "contest");
            Scribe_Values.Look(ref contestStart, "contestStart", -1);
            Scribe_Values.Look(ref nightAt, "nightAt", -1);
            Scribe_Values.Look(ref ended, "ended", -1);
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_Values.Look(ref held, "held");
            Scribe_Values.Look(ref music, "music");
            Scribe_Collections.Look(ref results, "results", LookMode.Deep);
            Scribe_References.Look(ref champion, "champion");
            Scribe_Values.Look(ref championName, "championName");
            Scribe_Values.Look(ref prizeGiven, "prizeGiven");
            Scribe_Values.Look(ref calledOff, "calledOff");
            Scribe_Values.Look(ref offWhy, "offWhy");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                guests ??= new List<FeastGuest>();
                guests.RemoveAll(g => g == null);
                contests ??= new List<ContestKind>();
                results ??= new List<FeastResult>();
                results.RemoveAll(r => r == null);
            }
        }
    }

    /// <summary>One of the colony's who won something at a neighbour's feast (the missions' invitation): told when they are home again.</summary>
    public class FeastTale : IExposable
    {
        public Pawn pawn;
        public string text;
        public int at;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null && pawn.Discarded) pawn = null;
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref at, "at");
        }
    }
}
