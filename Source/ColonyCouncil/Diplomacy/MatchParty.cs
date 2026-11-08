using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their kin come for the colony's own (Matches.cs, a couple who will live at their town): the one they marry and one of
    /// their kin walk in from the edge to the colony's gate and wait there; the colony's own walks out to them
    /// (JobDriver_MatchLeave), becomes one of their people and joins them, and they all walk home. They go home when done,
    /// or once the wait is long (the colony's own never came); hurt by the colony, they run. The lord is the game's own
    /// (saved with the map).
    /// </summary>
    public class LordJob_MatchEscort : LordJob
    {
        /// <summary>The match they came for (MatchPlan.id).</summary>
        public int plan = -1;
        public IntVec3 gate;
        public int arrived = -1;
        /// <summary>The colony's own is with them now (or the match came to nothing): they go home.</summary>
        public bool done;

        public LordJob_MatchEscort()
        {
        }

        public LordJob_MatchEscort(int plan, IntVec3 gate)
        {
            this.plan = plan;
            this.gate = gate;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_TravelOnErrand(gate);
            graph.StartingToil = walk;
            var wait = new LordToil_DefendPoint(gate, 3f, 3f);
            graph.AddToil(wait);
            var leave = new LordToil_ExitMap(Pace.Errand, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, wait);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddPostAction(new TransitionAction_Custom(Arrived));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(wait);
            goes.AddTrigger(new Trigger_Custom(s => s.type == TriggerSignalType.Tick && done));
            goes.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt((MatchMath.EscortWaitHours + 14f) * GenDate.TicksPerHour)));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(wait, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            var war = new Transition(walk, flee);
            war.AddSources(wait, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (arrived < 0 || done || Find.TickManager.TicksGame % 60 != 0) return;
            try
            {
                Matches.TakeLeave(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: leaving with their kin", e);
            }
        }

        private void Arrived()
        {
            if (arrived >= 0) return;
            arrived = Find.TickManager.TicksGame;
            try
            {
                Matches.EscortArrived(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: their kin at the gate", e);
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref plan, "plan", -1);
            Scribe_Values.Look(ref gate, "gate");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// The colony's own walks out to their kin at the gate (target A: the one they marry, or their kin) and, once face to
    /// face, is ready to go with them: their kin's group takes them along on its next tick (Matches.TakeLeave, then
    /// Matches.Departs), so the change of people never happens inside this job.
    /// </summary>
    public class JobDriver_MatchLeave : JobDriver
    {
        private Pawn Them => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Them == null || Them.Downed || !(Them.GetLord()?.LordJob is LordJob_MatchEscort));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var farewell = ToilMaker.MakeToil("MatchFarewell");
            farewell.initAction = () => pawn.rotationTracker.FaceTarget(Them);
            farewell.defaultCompleteMode = ToilCompleteMode.Delay;
            farewell.defaultDuration = 300;
            yield return farewell;
            var go = ToilMaker.MakeToil("MatchGo");
            go.initAction = () => Matches.ReadyToLeave(pawn, Them);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
