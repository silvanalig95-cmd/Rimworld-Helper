using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A masterwork or a legend made at the colony (item 60): the piece, its maker and home, when, and whether a master was rolled for.</summary>
    public class PieceRecord : IExposable
    {
        public Thing thing;
        public Pawn maker;
        public Map map;
        public int tick, quality;
        public bool rolled;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_References.Look(ref maker, "maker");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref quality, "quality");
            Scribe_Values.Look(ref rolled, "rolled");
        }
    }

    /// <summary>A journeyman whose wander-year at the colony ended well (item 61): they come back a master when it is due.</summary>
    public class ComeBackRecord : IExposable
    {
        public Pawn pawn, maker;
        public Faction faction;
        public Map map;
        public string name;
        public int ended, due = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref maker, "maker");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// A Signal Line mast at a friend's town (item 65): the town (its id and name), the notable there the colony writes to, the Line,
    /// when the mast stands, and whether it is up (or lost with the town).
    /// </summary>
    public class TownMastRecord : IExposable
    {
        public int town = -1, notable = -1, due = -1;
        public string townName, notableName;
        public Faction faction;
        public Map map;
        public bool raised, done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref notableName, "notableName");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref raised, "raised");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>The union's year's share of a well it sank (item 68): its home, the union, when the next is due and how many are left.</summary>
    public class ShareRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public int next = -1, left;

        public bool Done => left <= 0;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref left, "left");
        }
    }

    /// <summary>
    /// The organizer's evening (item 69): the home, the union, when they spoke, the decree the petition is for (to declare, or to
    /// lift: long hours), who brought it and when, and the stage: "spoke" (the morning not yet come), "asked" (before the council),
    /// "done".
    /// </summary>
    public class EveningRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public Pawn asker;
        public string decree, stage = "spoke";
        public bool repeal;
        public int spoke, asked = -1;

        public bool Done => stage == "done";

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref asker, "asker");
            Scribe_Values.Look(ref decree, "decree");
            Scribe_Values.Look(ref stage, "stage", "spoke");
            Scribe_Values.Look(ref repeal, "repeal");
            Scribe_Values.Look(ref spoke, "spoke");
            Scribe_Values.Look(ref asked, "asked", -1);
        }
    }

    /// <summary>
    /// What the peoples' own events of round 7 keep with the game (Peoples/Events7/): the masterworks a master may come to judge, the
    /// journeymen due back, the Line's masts at friends' towns, the union's shares of a well, the organizers' evenings and their
    /// petitions, the strikes a picket was rolled for, and when the code-fired visits last came. The rest lives in the events' visits,
    /// a camp's own state and round 2's seasons away. A save from before has none of it.
    /// </summary>
    public class PeopleEvents7Book : GameComponent
    {
        public List<PieceRecord> marks = new List<PieceRecord>();
        public List<ComeBackRecord> comeBacks = new List<ComeBackRecord>();
        public List<TownMastRecord> masts = new List<TownMastRecord>();
        public List<ShareRecord> shares = new List<ShareRecord>();
        public List<EveningRecord> evenings = new List<EveningRecord>();
        /// <summary>"factionId:strikeTick" for each strike a picket was rolled for, eight kept.</summary>
        public List<string> picketRolled = new List<string>();
        /// <summary>When a master, an organizer and a picket last came.</summary>
        public int lastMark = -1, lastEvening = -1, lastPicket = -1;

        private static PeopleEvents7Book cached;
        private static Game cachedGame;

        public PeopleEvents7Book(Game game)
        {
        }

        public static PeopleEvents7Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents7Book>();
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
            PeopleEvents7.Forget();
        }

        public void AddMark(PieceRecord r)
        {
            if (r?.thing == null || marks.Any(m => m.thing == r.thing)) return;
            marks.Add(r);
            while (marks.Count > 6) marks.RemoveAt(0);
        }

        public void AddComeBack(ComeBackRecord r)
        {
            if (r?.pawn == null || comeBacks.Any(c => c.pawn == r.pawn && !c.done)) return;
            comeBacks.Add(r);
            while (comeBacks.Count > PeopleEvents7Math.MaxComeBacks && comeBacks.Any(x => x.done)) comeBacks.Remove(comeBacks.First(x => x.done));
        }

        public void AddMast(TownMastRecord r)
        {
            if (r == null || r.town < 0 || masts.Any(m => !m.done && m.town == r.town)) return;
            masts.Add(r);
            while (masts.Count > PeopleEvents7Math.MaxTownMasts && masts.Any(x => x.done)) masts.Remove(masts.First(x => x.done));
        }

        public void AddShare(ShareRecord r)
        {
            if (r?.map == null) return;
            shares.Add(r);
            while (shares.Count > 4 && shares.Any(x => x.Done)) shares.Remove(shares.First(x => x.Done));
        }

        public void AddEvening(EveningRecord r)
        {
            if (r?.map == null) return;
            evenings.Add(r);
            while (evenings.Count > 4 && evenings.Any(x => x.Done)) evenings.Remove(evenings.First(x => x.Done));
            while (evenings.Count > 4) evenings.RemoveAt(0);
        }

        public bool PicketRolled(Faction f, int strike) => f != null && picketRolled.Contains(f.loadID + ":" + strike);

        public void NotePicket(Faction f, int strike)
        {
            if (f == null) return;
            string key = f.loadID + ":" + strike;
            if (picketRolled.Contains(key)) return;
            picketRolled.Add(key);
            while (picketRolled.Count > 8) picketRolled.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            Scribe_Collections.Look(ref comeBacks, "comeBacks", LookMode.Deep);
            Scribe_Collections.Look(ref masts, "masts", LookMode.Deep);
            Scribe_Collections.Look(ref shares, "shares", LookMode.Deep);
            Scribe_Collections.Look(ref evenings, "evenings", LookMode.Deep);
            Scribe_Collections.Look(ref picketRolled, "picketRolled", LookMode.Value);
            Scribe_Values.Look(ref lastMark, "lastMark", -1);
            Scribe_Values.Look(ref lastEvening, "lastEvening", -1);
            Scribe_Values.Look(ref lastPicket, "lastPicket", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                marks ??= new List<PieceRecord>();
                marks.RemoveAll(r => r?.thing == null || r.maker == null);
                comeBacks ??= new List<ComeBackRecord>();
                comeBacks.RemoveAll(r => r?.pawn == null);
                masts ??= new List<TownMastRecord>();
                masts.RemoveAll(r => r == null);
                shares ??= new List<ShareRecord>();
                shares.RemoveAll(r => r?.map == null);
                evenings ??= new List<EveningRecord>();
                evenings.RemoveAll(r => r?.map == null);
                picketRolled ??= new List<string>();
            }
        }
    }
}
