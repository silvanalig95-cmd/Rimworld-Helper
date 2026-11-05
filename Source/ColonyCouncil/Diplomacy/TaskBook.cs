using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One office's task at a home (CouncilTasks.cs; docs/design/stage2-diplomacy.md, item 8): the office, its kind and
    /// target, the officer, where it stands and its last step, when the next step is due (never shown), what came of it
    /// so far, and the officer's visit by caravan. One record per office per home (TaskMath.MaxPerHome), kept after the
    /// task ends until the office takes another, so the office's last visit counts toward "once a season".
    /// </summary>
    public class CouncilTask : IExposable
    {
        public int id;
        /// <summary>The office's defName.</summary>
        public string office;
        public TaskKind kind;
        public TaskStage stage;
        public Pawn officer;
        public string officerName;
        /// <summary>The target: a notable (by the notable book's id), a people, a town (world object id), one of the colony's held elsewhere.</summary>
        public int notable = -1;
        public Faction people;
        public int town = -1;
        public string townName;
        public Pawn about;
        public string targetName;
        public int started = -1, ended = -1;
        /// <summary>The last step taken, when, and how it went (the answer's tone, a roll's result, or -1).</summary>
        public TaskStep last;
        public int lastTick = -1, lastHow = -1;
        /// <summary>When the next step falls due (the hour's look; never shown as a time left).</summary>
        public int nextTick = -1;
        /// <summary>The trade's way so far (TaskMath.TradeGoal).</summary>
        public float progress;
        public int letters, visits, talks, patrols, carts;
        /// <summary>The letter or call on the road now, the courier trip met at the gate (an informer, a cart), or -1.</summary>
        public int letterId = -1, tripId = -1;
        /// <summary>The words of what it came to, for the cards and the history.</summary>
        public string result;
        // The visit by caravan.
        public VisitStage visit;
        public int planId = -1;
        public Pawn companion;
        public string companionName;
        public int visitSince = -1, lastVisit = -1;
        /// <summary>The marshal's drill: when their fighters were drilled (their band is stronger for a season from then).</summary>
        public int drilledAt = -1;
        /// <summary>The steward's feast: planned with their notables (its id), and held.</summary>
        public int feastId = -1;
        /// <summary>The day of the last talk with one of theirs here (one a day).</summary>
        public int talkDay = -1;
        /// <summary>The warden's word on a price asked for at their gate; the steward's feast planned with them.</summary>
        public bool asked;

        public bool Live => stage == TaskStage.Running;

        public bool Away => visit != VisitStage.None;

        public int Steps => letters + visits + talks + patrols + carts;

        public string OfficerName => officer != null && !officer.Discarded ? officer.LabelShort : officerName ?? "?";

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (officer != null && officer.Discarded) officer = null;
                if (about != null && about.Discarded) about = null;
                if (companion != null && companion.Discarded) companion = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref office, "office");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref officer, "officer");
            Scribe_Values.Look(ref officerName, "officerName");
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_References.Look(ref about, "about");
            Scribe_Values.Look(ref targetName, "targetName");
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref ended, "ended", -1);
            Scribe_Values.Look(ref last, "last");
            Scribe_Values.Look(ref lastTick, "lastTick", -1);
            Scribe_Values.Look(ref lastHow, "lastHow", -1);
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref letters, "letters");
            Scribe_Values.Look(ref visits, "visits");
            Scribe_Values.Look(ref talks, "talks");
            Scribe_Values.Look(ref patrols, "patrols");
            Scribe_Values.Look(ref carts, "carts");
            Scribe_Values.Look(ref letterId, "letterId", -1);
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_Values.Look(ref result, "result");
            Scribe_Values.Look(ref visit, "visit");
            Scribe_Values.Look(ref planId, "planId", -1);
            Scribe_References.Look(ref companion, "companion");
            Scribe_Values.Look(ref companionName, "companionName");
            Scribe_Values.Look(ref visitSince, "visitSince", -1);
            Scribe_Values.Look(ref lastVisit, "lastVisit", -1);
            Scribe_Values.Look(ref drilledAt, "drilledAt", -1);
            Scribe_Values.Look(ref feastId, "feastId", -1);
            Scribe_Values.Look(ref talkDay, "talkDay", -1);
            Scribe_Values.Look(ref asked, "asked");
        }
    }
}
