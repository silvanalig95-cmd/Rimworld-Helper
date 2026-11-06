using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where an election stands.</summary>
    public enum ElectionPhase
    {
        /// <summary>Announced: the candidates make their case to the voters in their free time.</summary>
        Campaign,
        /// <summary>The evening before polling day: the candidates speak at a gathering.</summary>
        Hustings,
        /// <summary>Polling day: everyone at home walks over and casts a vote.</summary>
        Polling,
        /// <summary>The evening of polling day (or a ceremony's evening): the votes are counted aloud.</summary>
        Count
    }

    /// <summary>One candidate in a race, with what they stand for (a plank of the office's, chosen from how the colony is doing).</summary>
    public class ElectionEntry : IExposable
    {
        public Pawn pawn;
        /// <summary>The plank's key (its lines' tag, plank:KEY), and the letter's sentence about it.</summary>
        public string plank;
        public string platform;
        /// <summary>The record they run on, as the letter says it ("In the seat, Alo has tended to the sick and hurt 23 times."), or null.</summary>
        public string record;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref plank, "plank");
            Scribe_Values.Look(ref platform, "platform");
            Scribe_Values.Look(ref record, "record");
        }
    }

    /// <summary>One seat being voted on: who stands, the council's endorsement, the ranked ballots cast, the poll day by day.</summary>
    public class ElectionRace : IExposable
    {
        public OfficeDef office;
        public int seat;
        public List<ElectionEntry> entries = new List<ElectionEntry>();
        /// <summary>The candidate the council endorsed (once per race), or null.</summary>
        public Pawn endorsed;
        /// <summary>Each ballot: the voter's candidates by thingIDNumber, first choice first ("12>4>9").</summary>
        public List<string> ballots = new List<string>();
        /// <summary>The poll as it stood each day of the campaign: whole votes per candidate, by thingIDNumber ("12:4,4:3").</summary>
        public List<string> pollDays = new List<string>();

        // The seat's holder and the promise they won it on at the last election, judged when this one was called
        // (Offices/SeatRecords.cs): what was promised, what the map showed of it since, and what that means at the vote.
        public Pawn holder;
        public int promiseId = -1;
        public string promise;
        public PromiseTest test;
        public PromiseVerdict verdict;
        public string evidence;
        public string evidenceMine;
        public float lean;

        public bool Contested => entries.Count >= 2;

        public IEnumerable<Pawn> Candidates => entries.Select(e => e.pawn);

        public ElectionEntry EntryOf(Pawn p) => p == null ? null : entries.FirstOrDefault(e => e.pawn == p);

        public void ExposeData()
        {
            // A seat the council no longer has (the keeper of secrets, retired 5 November 2026) loads as none, quietly, and
            // the election drops its race (Election.ExposeData).
            CouncilScribe.LookDef(ref office, "office");
            Scribe_Values.Look(ref seat, "seat");
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            Scribe_References.Look(ref endorsed, "endorsed");
            Scribe_Collections.Look(ref ballots, "ballots", LookMode.Value);
            Scribe_Collections.Look(ref pollDays, "pollDays", LookMode.Value);
            Scribe_References.Look(ref holder, "holder");
            Scribe_Values.Look(ref promiseId, "promiseId", -1);
            Scribe_Values.Look(ref promise, "promise");
            Scribe_Values.Look(ref test, "test");
            Scribe_Values.Look(ref verdict, "verdict");
            Scribe_Values.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref evidenceMine, "evidenceMine");
            Scribe_Values.Look(ref lean, "lean");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null) entries = new List<ElectionEntry>();
                entries.RemoveAll(e => e == null || e.pawn == null);
                if (ballots == null) ballots = new List<string>();
                if (pollDays == null) pollDays = new List<string>();
                if (endorsed != null && EntryOf(endorsed) == null) endorsed = null;
            }
        }
    }

    /// <summary>What one candidate has done for themselves: whom they talked round on their visits, and how they did at the hustings.</summary>
    public class ElectionCampaigner : IExposable
    {
        public Pawn pawn;
        /// <summary>Each voter's lean toward them from their visits (<see cref="ElectionMath.CanvassCap"/> at most either way).</summary>
        public Dictionary<Pawn, float> canvassed = new Dictionary<Pawn, float>();
        /// <summary>Visits made today, and the day they count for.</summary>
        public int visitsToday;
        public int visitDay = -1;
        /// <summary>Spoke at the hustings, how it went, and who was there to hear it.</summary>
        public bool spoke;
        public float showing;
        public List<Pawn> heard = new List<Pawn>();

        private List<Pawn> k1;
        private List<float> v1;

        public float LeanOf(Pawn voter)
        {
            float s = 0f;
            if (voter != null && canvassed.TryGetValue(voter, out float c)) s += c;
            if (spoke && voter != null && heard.Contains(voter)) s += showing;
            return s;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref canvassed, "canvassed", LookMode.Reference, LookMode.Value, ref k1, ref v1);
            Scribe_Values.Look(ref visitsToday, "visitsToday");
            Scribe_Values.Look(ref visitDay, "visitDay", -1);
            Scribe_Values.Look(ref spoke, "spoke");
            Scribe_Values.Look(ref showing, "showing");
            Scribe_Collections.Look(ref heard, "heard", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (canvassed == null) canvassed = new Dictionary<Pawn, float>();
                canvassed.RemoveAll(kv => kv.Key == null);
                if (heard == null) heard = new List<Pawn>();
                heard.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// An election from its announcement to the count (Offices/OfficesElection.cs): the dates, the races, what the
    /// candidates did, who has voted, and how far the count has got. Kept on the office tracker and saved with it.
    /// </summary>
    public class Election : IExposable
    {
        public int year;
        public bool byElection;
        /// <summary>No seat has two candidates: a short ceremony on the evening instead of a campaign and a vote.</summary>
        public bool ceremony;
        public ElectionPhase phase;
        public Map map;
        public int announcedTick;
        /// <summary>When the hustings, the polls and the count are set for (the count's is polling day's evening).</summary>
        public int hustingsTick = -1;
        public int pollTick = -1;
        public int countTick = -1;
        /// <summary>How often the hustings, and then polling day, were put off (a raid, nowhere calm to gather).</summary>
        public int hustingsPutOffs;
        public int pollPutOffs;
        public int countPutOffs;
        /// <summary>The hustings went ahead (or were let go after too many evenings put off).</summary>
        public bool hustingsDone;
        public bool hustingsHeld;
        /// <summary>Where they gather, speak and vote: the meeting place, and its words for the letters.</summary>
        public IntVec3 spot = IntVec3.Invalid;
        public string venue;
        /// <summary>Everyone with a say when it was called (the seats it opens are for this many).</summary>
        public int voterCount;
        public List<ElectionRace> races = new List<ElectionRace>();
        public List<ElectionCampaigner> campaigners = new List<ElectionCampaigner>();
        /// <summary>Who has cast their vote.</summary>
        public List<Pawn> voted = new List<Pawn>();
        /// <summary>The gathering under way (hustings or count) and how far its talk has got.</summary>
        public bool gathering;
        public int gatheringTick = -1;
        public Pawn chair;
        public bool opened;
        public int step;
        public int raceStep;
        public int callStep;
        public bool countDone;
        public bool countStarted;
        /// <summary>The poll is noted once a day, on this day.</summary>
        public int pollNotedDay = -1;

        public bool Started => gathering;

        public ElectionCampaigner CampaignerOf(Pawn p)
        {
            if (p == null) return null;
            for (int i = 0; i < campaigners.Count; i++)
                if (campaigners[i].pawn == p) return campaigners[i];
            return null;
        }

        public ElectionCampaigner For(Pawn p)
        {
            var c = CampaignerOf(p);
            if (c == null && p != null)
            {
                c = new ElectionCampaigner { pawn = p };
                campaigners.Add(c);
            }
            return c;
        }

        public IEnumerable<Pawn> AllCandidates => races.SelectMany(r => r.Candidates).Distinct();

        public bool IsCandidate(Pawn p) => p != null && races.Any(r => r.EntryOf(p) != null);

        public bool Contested => races.Any(r => r.Contested);

        public void ExposeData()
        {
            Scribe_Values.Look(ref year, "year");
            Scribe_Values.Look(ref byElection, "byElection");
            Scribe_Values.Look(ref ceremony, "ceremony");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref announcedTick, "announcedTick");
            Scribe_Values.Look(ref hustingsTick, "hustingsTick", -1);
            Scribe_Values.Look(ref pollTick, "pollTick", -1);
            Scribe_Values.Look(ref countTick, "countTick", -1);
            Scribe_Values.Look(ref hustingsPutOffs, "hustingsPutOffs");
            Scribe_Values.Look(ref pollPutOffs, "pollPutOffs");
            Scribe_Values.Look(ref countPutOffs, "countPutOffs");
            Scribe_Values.Look(ref hustingsDone, "hustingsDone");
            Scribe_Values.Look(ref hustingsHeld, "hustingsHeld");
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref venue, "venue");
            Scribe_Values.Look(ref voterCount, "voterCount");
            Scribe_Collections.Look(ref races, "races", LookMode.Deep);
            Scribe_Collections.Look(ref campaigners, "campaigners", LookMode.Deep);
            Scribe_Collections.Look(ref voted, "voted", LookMode.Reference);
            Scribe_Values.Look(ref gathering, "gathering");
            Scribe_Values.Look(ref gatheringTick, "gatheringTick", -1);
            Scribe_References.Look(ref chair, "chair");
            Scribe_Values.Look(ref opened, "opened");
            Scribe_Values.Look(ref step, "step");
            Scribe_Values.Look(ref raceStep, "raceStep");
            Scribe_Values.Look(ref callStep, "callStep");
            Scribe_Values.Look(ref countDone, "countDone");
            Scribe_Values.Look(ref countStarted, "countStarted");
            Scribe_Values.Look(ref pollNotedDay, "pollNotedDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (races == null) races = new List<ElectionRace>();
                races.RemoveAll(r => r == null || r.office == null);
                if (campaigners == null) campaigners = new List<ElectionCampaigner>();
                campaigners.RemoveAll(c => c == null || c.pawn == null);
                if (voted == null) voted = new List<Pawn>();
                voted.RemoveAll(p => p == null);
            }
        }
    }
}
