using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A colonist the learned society named a corresponding fellow (item 18): when, and when their next book comes by the post.</summary>
    public class FellowRecord : IExposable
    {
        public Pawn pawn;
        public Faction faction;
        public int since = -1, nextBook = -1, books;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref nextBook, "nextBook", -1);
            Scribe_Values.Look(ref books, "books");
        }
    }

    /// <summary>
    /// The enclave's contract (item 23): for what (a kind of the colony's goods and, for one thing, its def), how many at what
    /// worth each, the price, the day it is due, and how it went: "open", "kept", "short", "default".
    /// </summary>
    public class ContractRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Map map;
        public string kind, def;
        public int count, price, since = -1, due = -1;
        public float unit;
        public string state = "open";
        public bool done, feePaid, asked;

        public bool Open => !done && state == "open";

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref price, "price");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref unit, "unit");
            Scribe_Values.Look(ref state, "state", "open");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref feePaid, "feePaid");
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>
    /// A broker's pod come down hard on the colony's land (item 24): the broker, the cargo's crates and their worth, and the
    /// colony's answer: "waiting" (no answer yet), "rescue", "left" (left for their lander), "robbed"; the lander's day.
    /// </summary>
    public class PodRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Map map;
        public Pawn broker;
        public List<Thing> cargo = new List<Thing>();
        public int crates, worth, due = -1, since = -1;
        public string state = "waiting";
        public bool done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref broker, "broker");
            Scribe_Collections.Look(ref cargo, "cargo", LookMode.Reference);
            Scribe_Values.Look(ref crates, "crates");
            Scribe_Values.Look(ref worth, "worth");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref state, "state", "waiting");
            Scribe_Values.Look(ref done, "done");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cargo ??= new List<Thing>();
                cargo.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>Water the colony fouled and left (item 27): whose catches stopped for it, where it lies, since when, until when at most.</summary>
    public class FoulRecord : IExposable
    {
        public Faction faction;
        public Map map;
        public List<IntVec3> cells = new List<IntVec3>();
        public int since = -1, until = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref done, "done");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }
    }

    /// <summary>A great beast a colonist brought down (item 31): who, what it was and of what kind, its hide, and when; the Wardens' page waits on it.</summary>
    public class PageRecord : IExposable
    {
        public int id;
        public Pawn killer;
        public Map map;
        public string beast, kind, leather;
        public float hide;
        public bool ranged, done;
        public int tick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref killer, "killer");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref beast, "beast");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref leather, "leather");
            Scribe_Values.Look(ref hide, "hide");
            Scribe_Values.Look(ref ranged, "ranged");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref tick, "tick", -1);
        }
    }

    /// <summary>
    /// What the peoples' own events of round 4 keep with the game (Peoples/Events4/): the society's corresponding fellows and
    /// whom it examined, the summer school's best students, the enclave's contracts and their size, the pods come down, the
    /// fouled water left, the knot nights' lanterns set, the great beasts brought down waiting for their page, and when the
    /// code-fired visits last came. The rest lives in the events' visits (the lander, the cleaning, the lanterns, the manhunt,
    /// the hive) and in a camp's own state. A save from before has none of it.
    /// </summary>
    public class PeopleEvents4Book : GameComponent
    {
        public List<FellowRecord> fellows = new List<FellowRecord>();
        /// <summary>"id:tick" for each colonist examined (or whose examination was declined), so none is asked again soon.</summary>
        public List<string> examined = new List<string>();
        /// <summary>The summer school's best students (their ids): examined at a lower level.</summary>
        public List<int> schoolBest = new List<int>();
        /// <summary>The examination before the colony waiting for its evening's end: its gathering's id, since when, the verdict the card's roll gave, who and by whom.</summary>
        public int examPlay = -1, examSince = -1;
        public bool examPass;
        public Pawn examPawn, examiner;
        public Faction examFaction;
        public List<ContractRecord> contracts = new List<ContractRecord>();
        public int nextContract = 1;
        /// <summary>The next contract's size (bigger after one kept), and until when the terms are cooler after a default (-1: not).</summary>
        public float contractScale = 1f;
        public int coolUntil = -1;
        public List<PodRecord> pods = new List<PodRecord>();
        public int nextPod = 1;
        public List<FoulRecord> fouled = new List<FoulRecord>();
        /// <summary>"factionId:year" for each knot night whose lanterns came.</summary>
        public List<string> lanternYears = new List<string>();
        public List<PageRecord> pages = new List<PageRecord>();
        public int nextPage = 1;
        /// <summary>When the Wardens last came to a manhunt, and to write a page.</summary>
        public int lastManhunt = -1, lastPage = -1;

        private static PeopleEvents4Book cached;
        private static Game cachedGame;

        public PeopleEvents4Book(Game game)
        {
        }

        public static PeopleEvents4Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents4Book>();
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
            PeopleEvents4.Forget();
        }

        // ---------------- Examined ----------------

        /// <summary>Examined (or declined) within the gap: not asked again yet.</summary>
        public bool ExaminedLately(Pawn p, int now)
        {
            if (p == null) return false;
            string pre = p.thingIDNumber + ":";
            foreach (var e in examined)
                if (e.StartsWith(pre) && int.TryParse(e.Substring(pre.Length), out int t) && now - t < PeopleEvents4Math.ExamGapDays * GenDate.TicksPerDay)
                    return true;
            return false;
        }

        public void NoteExamined(Pawn p, int now)
        {
            if (p == null) return;
            string pre = p.thingIDNumber + ":";
            examined.RemoveAll(e => e.StartsWith(pre));
            examined.Add(pre + now);
            while (examined.Count > PeopleEvents4Math.MaxFellows * 2) examined.RemoveAt(0);
        }

        public void AddFellow(FellowRecord r)
        {
            if (r?.pawn == null || fellows.Any(f => f.pawn == r.pawn)) return;
            fellows.Add(r);
            while (fellows.Count > PeopleEvents4Math.MaxFellows) fellows.RemoveAt(0);
        }

        public void NoteBestStudent(Pawn p)
        {
            if (p == null || schoolBest.Contains(p.thingIDNumber)) return;
            schoolBest.Add(p.thingIDNumber);
            while (schoolBest.Count > PeopleEvents4Math.MaxFellows) schoolBest.RemoveAt(0);
        }

        // ---------------- Contracts, pods, pages ----------------

        public ContractRecord AddContract(ContractRecord r)
        {
            if (r == null) return null;
            r.id = nextContract++;
            contracts.Add(r);
            while (contracts.Count > 6 && contracts.Any(x => x.done)) contracts.Remove(contracts.First(x => x.done));
            return r;
        }

        public PodRecord AddPod(PodRecord r)
        {
            if (r == null) return null;
            r.id = nextPod++;
            pods.Add(r);
            while (pods.Count > 4 && pods.Any(x => x.done)) pods.Remove(pods.First(x => x.done));
            return r;
        }

        public PageRecord AddPage(PageRecord r)
        {
            if (r == null) return null;
            r.id = nextPage++;
            pages.Add(r);
            while (pages.Count > PeopleEvents4Math.MaxPages && pages.Any(x => x.done)) pages.Remove(pages.First(x => x.done));
            while (pages.Count > PeopleEvents4Math.MaxPages) pages.RemoveAt(0);
            return r;
        }

        public void AddFoul(FoulRecord r)
        {
            if (r == null) return;
            fouled.Add(r);
            while (fouled.Count > 4 && fouled.Any(x => x.done)) fouled.Remove(fouled.First(x => x.done));
            while (fouled.Count > 4) fouled.RemoveAt(0);
        }

        public bool LanternsCame(Faction f, int year) => f != null && lanternYears.Contains(f.loadID + ":" + year);

        public void NoteLanterns(Faction f, int year)
        {
            if (f == null || LanternsCame(f, year)) return;
            lanternYears.Add(f.loadID + ":" + year);
            while (lanternYears.Count > 12) lanternYears.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref fellows, "fellows", LookMode.Deep);
            Scribe_Collections.Look(ref examined, "examined", LookMode.Value);
            Scribe_Collections.Look(ref schoolBest, "schoolBest", LookMode.Value);
            Scribe_Values.Look(ref examPlay, "examPlay", -1);
            Scribe_Values.Look(ref examSince, "examSince", -1);
            Scribe_Values.Look(ref examPass, "examPass");
            Scribe_References.Look(ref examPawn, "examPawn");
            Scribe_References.Look(ref examiner, "examiner");
            Scribe_References.Look(ref examFaction, "examFaction");
            Scribe_Collections.Look(ref contracts, "contracts", LookMode.Deep);
            Scribe_Values.Look(ref nextContract, "nextContract", 1);
            Scribe_Values.Look(ref contractScale, "contractScale", 1f);
            Scribe_Values.Look(ref coolUntil, "coolUntil", -1);
            Scribe_Collections.Look(ref pods, "pods", LookMode.Deep);
            Scribe_Values.Look(ref nextPod, "nextPod", 1);
            Scribe_Collections.Look(ref fouled, "fouled", LookMode.Deep);
            Scribe_Collections.Look(ref lanternYears, "lanternYears", LookMode.Value);
            Scribe_Collections.Look(ref pages, "pages", LookMode.Deep);
            Scribe_Values.Look(ref nextPage, "nextPage", 1);
            Scribe_Values.Look(ref lastManhunt, "lastManhunt", -1);
            Scribe_Values.Look(ref lastPage, "lastPage", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                fellows ??= new List<FellowRecord>();
                fellows.RemoveAll(r => r?.pawn == null);
                examined ??= new List<string>();
                schoolBest ??= new List<int>();
                contracts ??= new List<ContractRecord>();
                contracts.RemoveAll(r => r == null);
                pods ??= new List<PodRecord>();
                pods.RemoveAll(r => r == null);
                fouled ??= new List<FoulRecord>();
                fouled.RemoveAll(r => r == null);
                lanternYears ??= new List<string>();
                pages ??= new List<PageRecord>();
                pages.RemoveAll(r => r == null);
            }
        }
    }
}
