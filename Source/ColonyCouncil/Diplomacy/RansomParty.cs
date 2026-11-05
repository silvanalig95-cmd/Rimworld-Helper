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
    /// A messenger at the gate (Ransoms.cs, Envoys.cs): one of their people, or a go-between of a people at peace with both,
    /// walks in from the edge to the colony's gate and waits there. At the gate their errand is done: an offer for a
    /// captive or a demand asked (their letter), a price set down, an exchange made, an envoy's letter home taken. They go
    /// home when told, or once the wait is long; hurt by the colony, they run. The lord is the game's own (saved with the map).
    /// </summary>
    public class LordJob_Messenger : LordJob
    {
        /// <summary>The ransom offer they carry word of (Ransoms), or -1.</summary>
        public int offer = -1;
        /// <summary>An envoy's runner: the home whose guest's letter they take (Envoys), or none.</summary>
        public int envoyHome = HomesMath.None;
        public IntVec3 gate;
        public float waitHours = RansomMath.WaitHours;
        /// <summary>When they reached the gate, or -1.</summary>
        public int arrived = -1;
        public bool harmed;
        /// <summary>Their errand at the gate is done (set from their arrival, where a memo can't be sent): they go home.</summary>
        public bool done;

        public const string MemoLeave = "MessengerLeave";

        public LordJob_Messenger()
        {
        }

        public LordJob_Messenger(int offer, int envoyHome, IntVec3 gate, float waitHours)
        {
            this.offer = offer;
            this.envoyHome = envoyHome;
            this.gate = gate;
            this.waitHours = waitHours;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_Travel(gate);
            graph.StartingToil = walk;
            var wait = new LordToil_DefendPoint(gate, 3f, 3f);
            graph.AddToil(wait);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, wait);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddPostAction(new TransitionAction_Custom(Arrived));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(wait);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            goes.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt((waitHours + 12f) * GenDate.TicksPerHour)));
            goes.AddTrigger(new Trigger_Custom(s => s.type == TriggerSignalType.Tick && done));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(wait, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(Harmed));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            var war = new Transition(walk, flee);
            war.AddSources(wait, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        private void Arrived()
        {
            if (arrived >= 0) return;
            arrived = Find.TickManager.TicksGame;
            try
            {
                if (offer >= 0) Ransoms.Arrived(this);
                else if (envoyHome != HomesMath.None) Envoys.RunnerArrived(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a messenger at the gate", e);
            }
        }

        private void Harmed()
        {
            if (harmed) return;
            harmed = true;
            try
            {
                if (offer >= 0) Ransoms.Harmed(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a messenger harmed", e);
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref offer, "offer", -1);
            Scribe_Values.Look(ref envoyHome, "envoyHome", HomesMath.None);
            Scribe_Values.Look(ref gate, "gate");
            Scribe_Values.Look(ref waitHours, "waitHours", RansomMath.WaitHours);
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// The colony's speaker carries the silver of their demand out to their messenger at the gate (target A; the silver,
    /// target B) and hands it over face to face. Called away, what was carried is set down and the rest comes from the
    /// stores as the messenger takes it.
    /// </summary>
    public class JobDriver_RansomPay : JobDriver
    {
        private Pawn Messenger => job.GetTarget(TargetIndex.A).Thing as Pawn;

        private LordJob_Messenger Errand => Messenger?.GetLord()?.LordJob as LordJob_Messenger;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.GetTarget(TargetIndex.B).HasThing || pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

        public override string GetReport()
        {
            var f = Messenger?.Faction;
            return f != null ? RansomWords.P("CC_Ransom_Report_Pay") : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Messenger == null || Messenger.Dead || Messenger.Downed || !Messenger.Spawned || Errand == null || Errand.harmed);
            bool handed = false;
            AddFinishAction(cond =>
            {
                try
                {
                    if (!handed && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ransom: the silver carried out", e);
                }
            });
            if (job.GetTarget(TargetIndex.B).HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var talk = ToilMaker.MakeToil("RansomPay");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 300;
            talk.handlingFacing = true;
            talk.initAction = () =>
            {
                // together: face to face at the gate.
                Outsiders.Say(pawn, "ransompay", Messenger);
            };
            talk.tickIntervalAction = delta =>
            {
                var m = Messenger;
                if (m != null) pawn.rotationTracker.FaceTarget(m);
            };
            talk.WithProgressBarToilDelay(TargetIndex.A);
            yield return talk;
            var hand = ToilMaker.MakeToil("RansomHanded");
            hand.initAction = () =>
            {
                try
                {
                    int n = 0;
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried != null && carried.def == ThingDefOf.Silver)
                    {
                        // The silver goes into their hands, and home with them.
                        n = carried.stackCount;
                        pawn.carryTracker.DestroyCarriedThing();
                    }
                    handed = true;
                    Ransoms.Paid(Errand?.offer ?? -1, pawn, n);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ransom: handed over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return hand;
        }
    }
}
