using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their envoy's stay as a group (Envoys.cs): they walk in to the colony's common rooms, stay there the season as its
    /// guests (LordToil_EnvoyStay: about the rooms by day, indoors in danger; Envoys' hour sees to their meals and beds), and
    /// walk out once told. Hurt by the colony, or their people turned on it, they leave at once, defending themselves.
    /// </summary>
    public class LordJob_EnvoyStay : LordJob
    {
        public int home = HomesMath.None;
        public IntVec3 spot;

        public const string MemoLeave = "EnvoyLeave";

        public LordJob_EnvoyStay()
        {
        }

        public LordJob_EnvoyStay(int home, IntVec3 spot)
        {
            this.home = home;
            this.spot = spot;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override bool CanOpenAnyDoor(Pawn p) => true;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_TravelOnErrand(spot);
            graph.StartingToil = walk;
            var stay = new LordToil_EnvoyStay(spot);
            graph.AddToil(stay);
            var leave = new LordToil_ExitMap(Pace.Errand, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, stay);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 8));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(stay);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            goes.AddPostAction(new TransitionAction_WakeAll());
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(stay, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(() =>
            {
                try
                {
                    Envoys.Harmed(home);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("envoys: harmed by the colony", e);
                }
            }));
            hurt.AddPostAction(new TransitionAction_WakeAll());
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            var war = new Transition(walk, flee);
            war.AddSources(stay, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_WakeAll());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            try
            {
                // Walking off the map at the end is no loss to the stay.
                if (condition != PawnLostCondition.ExitedMap) Envoys.Lost(home, p, condition);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: one of them lost", e);
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref spot, "spot");
        }
    }

    /// <summary>
    /// The season at the colony: by day they keep about its common rooms (Rimfolk_EnvoyStay), in danger they keep to a room
    /// indoors (Rimfolk_EnvoyIndoors); their long needs are met (Envoys' hour sends them to eat and to bed).
    /// </summary>
    public class LordToil_EnvoyStay : LordToil
    {
        private readonly IntVec3 spot;
        [Unsaved] private bool indoors;
        [Unsaved] private IntVec3 inside = IntVec3.Invalid;

        public LordToil_EnvoyStay(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override IntVec3 FlagLoc => spot;

        public override bool AllowSatisfyLongNeeds => true;

        public override bool AllowRestingInBed => true;

        private bool Danger => Map?.dangerWatcher != null && Map.dangerWatcher.DangerRating == StoryDanger.High;

        public override void UpdateAllDuties()
        {
            var stayDuty = RansomDefOf.Rimfolk_EnvoyStay;
            var inDuty = RansomDefOf.Rimfolk_EnvoyIndoors;
            if (indoors && !inside.IsValid) inside = Visitors.IndoorsCell(Map, lord.ownedPawns.ToList());
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = indoors && inDuty != null
                    ? new PawnDuty(inDuty, inside.IsValid ? inside : spot, 4f)
                    : new PawnDuty(stayDuty ?? DutyDefOf.WanderClose, spot, 10f);
        }

        public override void LordToilTick()
        {
            if (lord.ticksInToil % 250 != 0) return;
            bool now = Danger;
            if (now == indoors) return;
            indoors = now;
            if (!now) inside = IntVec3.Invalid;
            UpdateAllDuties();
        }
    }

    /// <summary>The envoy walks out to their runner at the gate (target A) and hands over their letter home face to face.</summary>
    public class JobDriver_EnvoyLetter : JobDriver
    {
        private Pawn Runner => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => RansomWords.P("CC_Envoy_Report_Letter");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Runner == null || Runner.Dead || Runner.Downed || !Runner.Spawned || !(Runner.GetLord()?.LordJob is LordJob_Messenger m) || m.done);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var talk = ToilMaker.MakeToil("EnvoyLetter");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 300;
            talk.handlingFacing = true;
            talk.initAction = () =>
            {
                // together: face to face at the gate.
                Outsiders.Say(pawn, "envoyletter", Runner);
            };
            talk.tickIntervalAction = delta =>
            {
                var r = Runner;
                if (r != null) pawn.rotationTracker.FaceTarget(r);
            };
            yield return talk;
            var done = ToilMaker.MakeToil("EnvoyLetterHanded");
            done.initAction = () => Envoys.LetterHanded(pawn, Runner);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>The envoy walks over to the council's meeting (to the cell, target A) and stands by while it sits, facing its middle (target B).</summary>
    public class JobDriver_EnvoySitIn : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => RansomWords.P("CC_Envoy_Report_SitIn");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => AmbitionTracker.MeetingOn(pawn.Map) == null);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var sit = ToilMaker.MakeToil("EnvoySitIn");
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = 2 * GenDate.TicksPerHour;
            sit.handlingFacing = true;
            sit.socialMode = RandomSocialMode.Quiet;
            sit.tickIntervalAction = delta => pawn.rotationTracker.FaceCell(job.GetTarget(TargetIndex.B).Cell);
            yield return sit;
        }
    }
}
