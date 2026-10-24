using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp's people as a group: they walk to their spot, live there (building, cooking, sleeping, working the land
    /// around it) until told to go, and leave. Hurt by the colony while still at peace with it, they flee; at war with
    /// it (the game's own goodwill decides) before they've packed up, they hold their camp for a while and then fight
    /// their way out.
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

        // The group ends when its people are gone, whatever is left standing at the camp.
        public override bool KeepExistingWhileHasAnyBuilding => false;

        public override bool ShouldExistWithoutPawns => false;

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
            scatter.AddTrigger(new Trigger_ChanceOnPlayerHarmNPCBuilding(1f));
            // Their animals are taken out of the group first and walked off on their own, so they never fight anyone (CampLook.cs).
            scatter.AddPreAction(new TransitionAction_Custom(() => CampBeasts.LeadOff(lord)));
            scatter.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(scatter);

            // At war with the colony now: they hold the camp. A group already packed and walking off keeps going (and
            // defends itself if the colony comes after it, as above).
            var war = new Transition(travel, defend);
            war.AddSources(camp);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPreAction(new TransitionAction_Custom(() => CampBeasts.LeadOff(lord)));
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

    /// <summary>
    /// Life at the camp: every one of them on the camp duty (see <see cref="JobGiver_CampLife"/>), their animals on their
    /// own (see <see cref="JobGiver_CampBeasts"/>).
    /// </summary>
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
                p.mindState.duty = p.RaceProps.Humanlike
                    ? new PawnDuty(CampDefOf.Council_CampLife, center, CampSite.CoreRadius)
                    : new PawnDuty(CampDefOf.Council_CampBeasts, center, CampSite.CoreRadius + 6f);
        }

        public override void LordToilTick()
        {
            // A hunt is over once the animal falls: the game's own hunt would carry the body off to the colony's
            // stores, so it ends here, and the camp fetches the body itself.
            if (lord.ticksInToil % 30 == 0)
                foreach (var p in lord.ownedPawns)
                    if (p.CurJobDef == JobDefOf.Hunt && (p.CurJob.targetA.Thing is Corpse || (p.CurJob.targetA.Thing is Pawn v && v.Dead)))
                        p.jobs.EndCurrentJob(JobCondition.Succeeded);
            // Rimfolk's record of this camp is gone (the mod's part was removed, or something broke): they just go.
            if (lord.ticksInToil % 2500 == 1250 && CampTracker.Instance?.camp?.lord != lord) lord.ReceiveMemo(LordJob_Camp.MemoLeave);
        }
    }
}
