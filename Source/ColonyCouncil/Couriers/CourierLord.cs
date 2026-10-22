using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class CourierDefOf
    {
        public static DutyDef Council_CourierWork;
        public static JobDef Council_CourierDrop;
        public static JobDef Council_CourierLoad;
        public static JobDef Council_CourierFetch;

        static CourierDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CourierDefOf));
        }
    }

    /// <summary>
    /// A courier trip's people as a group: they walk in to the gate, do their business there (and at the stores) until
    /// the trip says they're done, and walk off. Hurt by the colony, or their people turned hostile, they leave at once,
    /// defending themselves. Doors: as any visitor at peace with the colony, they open the colony's doors (the game's
    /// own rule for those who aren't its enemies); nothing here opens more than that.
    /// </summary>
    public class LordJob_Courier : LordJob
    {
        public int tripId = -1;
        public IntVec3 spot;

        public const string MemoLeave = "CourierLeave";

        public LordJob_Courier()
        {
        }

        public LordJob_Courier(int tripId, IntVec3 spot)
        {
            this.tripId = tripId;
            this.spot = spot;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_Travel(spot);
            graph.StartingToil = travel;
            var work = new LordToil_CourierWork(spot, tripId);
            graph.AddToil(work);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            // At the gate (or near enough, after a long walk).
            var arrived = new Transition(travel, work);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddTrigger(new Trigger_TicksPassed(CourierMath.TicksPerHour * 5));
            graph.AddTransition(arrived);

            // Done (the trip says so), or, should nobody ever say, half a day on.
            var goes = new Transition(travel, leave);
            goes.AddSource(work);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            graph.AddTransition(goes);
            var late = new Transition(work, leave);
            late.AddTrigger(new Trigger_TicksPassed(CourierMath.TicksPerHour * 14));
            graph.AddTransition(late);

            // Hurt by the colony: they leave at once, defending themselves.
            int id = tripId;
            var hurt = new Transition(travel, flee);
            hurt.AddSources(work, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer, null, null));
            hurt.AddPostAction(new TransitionAction_Custom(() => CourierTracker.Instance?.Notify_Harmed(id)));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            // Their people turned on the colony meanwhile.
            var war = new Transition(travel, flee);
            war.AddSources(work, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            CourierTracker.Instance?.Notify_Lost(tripId, p, condition);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_Values.Look(ref spot, "spot");
        }
    }

    /// <summary>
    /// At the gate: the people on the courier's duty (<see cref="JobGiver_CourierWork"/>: set down what they brought,
    /// fetch what they came for, else wait close by); a pack animal waits by the gate.
    /// </summary>
    public class LordToil_CourierWork : LordToil
    {
        private readonly IntVec3 spot;
        private readonly int tripId;

        public LordToil_CourierWork(IntVec3 spot, int tripId)
        {
            this.spot = spot;
            this.tripId = tripId;
        }

        public override IntVec3 FlagLoc => spot;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = p.RaceProps.Humanlike ? new PawnDuty(CourierDefOf.Council_CourierWork, spot, 4f) : new PawnDuty(DutyDefOf.WanderClose, spot, 4f);
        }

        public override void LordToilTick()
        {
            // Rimfolk's record of this trip is gone (the mod's part removed, or something broke): they just go.
            if (lord.ticksInToil % 2500 == 1300 && CourierTracker.Instance?.Get(tripId)?.lord != lord) lord.ReceiveMemo(LordJob_Courier.MemoLeave);
        }
    }
}
