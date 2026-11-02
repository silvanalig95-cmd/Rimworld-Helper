using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// An election's gathering (Offices/OfficesElection.cs): the hustings on the evening before polling day, where the
    /// candidates speak, or the count on polling day's evening (or a ceremony), where the votes are read out. The game's
    /// own party at its heart, like the council meeting's: people drift over when they're free and stand about the place.
    /// Called off by any danger, and ended once all is said.
    /// </summary>
    public class LordJob_Election : LordJob_Joinable_Gathering
    {
        public bool hustings;

        /// <summary>Sent to the lord when everything has been said: the gathering ends as finished.</summary>
        public const string DoneMemo = "RimfolkElectionDone";

        public LordJob_Election()
        {
        }

        public LordJob_Election(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef, bool hustings, int ticks) : base(spot, organizer, gatheringDef)
        {
            this.hustings = hustings;
            durationTicks = ticks;
        }

        public int Duration => durationTicks;

        public override bool AllowStartNewGatherings => false;

        public override bool OrganizerIsStartingPawn => true;

        protected override LordToil CreateGatheringToil(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) => new LordToil_Party(spot, gatheringDef, 1.5E-05f);

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var toil = CreateGatheringToil(spot, organizer, gatheringDef);
            graph.AddToil(toil);
            var end = new LordToil_End();
            graph.AddToil(end);

            var calledOff = new Transition(toil, end);
            calledOff.AddTrigger(new Trigger_TickCondition(() => ShouldBeCalledOff(), 60));
            calledOff.AddTrigger(new Trigger_PawnKilled());
            calledOff.AddPreAction(new TransitionAction_Custom(() => Over(true)));
            graph.AddTransition(calledOff);

            timeoutTrigger = GetTimeoutTrigger();
            var done = new Transition(toil, end);
            done.AddTrigger(timeoutTrigger);
            done.AddTrigger(new Trigger_Memo(DoneMemo));
            done.AddPreAction(new TransitionAction_Custom(() => Over(false)));
            graph.AddTransition(done);
            return graph;
        }

        private void Over(bool early)
        {
            try
            {
                CouncilGameComponent.Instance?.offices.GatheringOver(hustings, early);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("election gathering end", e);
                var offices = CouncilGameComponent.Instance?.offices;
                if (offices?.election != null) offices.election.gathering = false;
            }
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (Find.TickManager.TicksGame % ElectionMath.LineTicks != 0) return;
            try
            {
                var offices = CouncilGameComponent.Instance?.offices;
                if (offices?.election == null) return;
                if (hustings) offices.HustingsTalk(lord);
                else offices.CountTalk(lord);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("election talk", e);
            }
        }

        public override string GetReport(Pawn pawn) => (hustings ? "CC_Elect_Report_Hustings" : "CC_Elect_Report_Count").Translate();

        /// <summary>Any danger ends it at once (the next evening is tried); so does weather no gathering survives, and the election ending.</summary>
        protected override bool ShouldBeCalledOff()
        {
            try
            {
                var map = Map;
                if (map == null || CouncilGameComponent.Instance?.offices?.election == null) return true;
                if (map.dangerWatcher.DangerRating != StoryDanger.None || !GatheringsUtility.AcceptableGameConditionsToContinueGathering(map)) return true;
                return lord != null && lord.ownedPawns.Count == 0 && lord.ticksInToil > GenDate.TicksPerHour;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("election call-off check", e);
                return true;
            }
        }

        /// <summary>Those with a say in the council, and never to a place that would hurt them (PlayOut/PlaySafety.cs).</summary>
        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            try
            {
                if (!MeetingAttendance.IsMember(p) || !PlayOuts.CanJoin(p) || !PlaySafety.Safe(p, spot, Map)) return 0f;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("election join", e);
                return 0f;
            }
            return base.VoluntaryJoinPriorityFor(p);
        }

        /// <summary>When it ends, nobody drops a meal, a load in their arms, or work of their own.</summary>
        public override bool EndPawnJobOnCleanup(Pawn p)
        {
            var job = p?.CurJob;
            if (job != null && (job.def == JobDefOf.Ingest || p.carryTracker?.CarriedThing != null || job.workGiverDef != null || job.playerForced)) return false;
            return base.EndPawnJobOnCleanup(p);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hustings, "hustings");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && gatheringDef == null) gatheringDef = OfficeTracker.ElectionGathering;
        }
    }

    /// <summary>Required by the gathering def; an election's gatherings are only ever started by the election.</summary>
    public class GatheringWorker_Election : GatheringWorker
    {
        public override bool CanExecute(Map map, Pawn organizer = null) => false;

        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            return false;
        }

        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer) => null;
    }

    /// <summary>
    /// Polling day: a colonist walks to the meeting place, stands at it a moment, and casts their vote. Gives up in
    /// danger, when the polls are no longer open, or once they've voted; like any job, when drafted, downed or in need.
    /// </summary>
    public class JobDriver_CastVote : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private static bool Open(Pawn p)
        {
            var e = CouncilGameComponent.Instance?.offices?.election;
            return e != null && e.phase == ElectionPhase.Polling && !e.voted.Contains(p);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None || !Open(pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var mark = Toils_General.Wait(150, TargetIndex.A);
            mark.WithProgressBarToilDelay(TargetIndex.A);
            yield return mark;
            yield return Toils_General.Do(() => CouncilGameComponent.Instance?.offices.CastVote(pawn));
        }
    }
}
