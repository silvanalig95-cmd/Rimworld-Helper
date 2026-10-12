using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp's people as a group: they walk to their spot, live there (building, cooking, sleeping, working the land
    /// around it) until told to go, and leave. Hurt by the colony while still at peace with it, they flee; at war with
    /// it (the game's own goodwill decides), they hold their camp for a while and then fight their way out.
    /// </summary>
    public class LordJob_Camp : LordJob
    {
        public IntVec3 center;

        public const string MemoLeave = "CampLeave";
        public const string MemoFlee = "CampFlee";

        public LordJob_Camp()
        {
        }

        public LordJob_Camp(IntVec3 center)
        {
            this.center = center;
        }

        public override bool AddFleeToil => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_Travel(center);
            graph.StartingToil = travel;
            var camp = new LordToil_Camp(center);
            graph.AddToil(camp);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);
            var defend = new LordToil_DefendPoint(center, 14f, 8f);
            graph.AddToil(defend);
            var breakOut = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(breakOut);

            // At the spot (or near enough, after a long walk).
            var arrived = new Transition(travel, camp);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 8));
            graph.AddTransition(arrived);

            // Time to go, in their own time.
            var goes = new Transition(travel, leave);
            goes.AddSource(camp);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            graph.AddTransition(goes);

            // A raid, or the colony hurt one of them while still at peace: they leave at once, defending themselves.
            var scatter = new Transition(travel, flee);
            scatter.AddSources(camp, leave);
            scatter.AddTrigger(new Trigger_Memo(MemoFlee));
            scatter.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer, null, null));
            scatter.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(scatter);

            // At war with the colony now: they hold the camp.
            var war = new Transition(travel, defend);
            war.AddSources(camp, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_WakeAll());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war, true);

            // ...for a while, or until half of them are down, then they fight their way out.
            var retreat = new Transition(defend, breakOut);
            retreat.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 10));
            retreat.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
            retreat.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            graph.AddTransition(retreat);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
        }
    }

    /// <summary>Life at the camp: every one of them on the camp duty (see <see cref="JobGiver_CampLife"/>).</summary>
    public class LordToil_Camp : LordToil
    {
        private readonly IntVec3 center;

        public LordToil_Camp(IntVec3 center)
        {
            this.center = center;
        }

        public override IntVec3 FlagLoc => center;

        public override bool AllowSatisfyLongNeeds => true;

        public override bool AllowRestingInBed => true;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = new PawnDuty(CampDefOf.Council_CampLife, center, CampSite.CoreRadius);
        }

        public override void LordToilTick()
        {
            // Rimfolk's record of this camp is gone (the mod's part was removed, or something broke): they just go.
            if (lord.ticksInToil % 2500 == 1250 && CampTracker.Instance?.camp?.lord != lord) lord.ReceiveMemo(LordJob_Camp.MemoLeave);
        }
    }
}
