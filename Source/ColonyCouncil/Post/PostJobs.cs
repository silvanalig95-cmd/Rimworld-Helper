using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class PostDefOf
    {
        public static JobDef Rimfolk_PostTake;
        public static JobDef Rimfolk_PostDrop;
        public static JobDef Rimfolk_PostSpeak;
        public static JobDef Rimfolk_RiderSetOut;
        public static JobDef Rimfolk_PostPutIn;

        static PostDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PostDefOf));
        }
    }

    /// <summary>
    /// A carrier going their way (an outsider leaving, the post rider, one of the colony's caravan) walks to the mailbag
    /// (target A: its cell, or the plain cell by the gate), takes the letters kept for them, and, for a spoken message,
    /// waits there a while for a colonist to come and say it (Post/MailWatch.cs). Then they go about their way.
    /// </summary>
    public class JobDriver_PostTake : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Mail_Report_Take".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            var look = Toils_General.Wait(150, TargetIndex.None);
            look.WithProgressBarToilDelay(TargetIndex.A);
            look.handlingFacing = true;
            look.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            yield return look;
            var take = ToilMaker.MakeToil("PostTake");
            take.initAction = () =>
            {
                try
                {
                    MailWatch.Took(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: letters taken", e);
                }
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
            // A spoken message: they wait for someone to come and say it.
            var wait = ToilMaker.MakeToil("PostWaitWords");
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = PostMath.SpokenWaitTicks;
            wait.handlingFacing = true;
            wait.initAction = () =>
            {
                if (MailWatch.SpokenFor(pawn) == null) ReadyForNextToil();
                else MailWatch.CallSpeaker(pawn);
            };
            wait.tickIntervalAction = delta =>
            {
                if (pawn.IsHashIntervalTick(250, delta))
                {
                    if (MailWatch.SpokenFor(pawn) == null)
                    {
                        ReadyForNextToil();
                        return;
                    }
                    MailWatch.CallSpeaker(pawn);
                }
            };
            wait.AddFinishAction(() => MailWatch.Waited(pawn));
            yield return wait;
        }
    }

    /// <summary>
    /// The writer of a letter just written for the post walks it to the mailbag (target A: its cell, or the plain cell by the
    /// gate) and puts it in; only then is it in the post (Post/Outbox.cs, BroughtIn). Cut off on the way (drafted, downed,
    /// the letter called off), it ends, and the letters' pass sends them again or puts it in later.
    /// </summary>
    public class JobDriver_PostPutIn : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Mail_Report_PutIn".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Outbox.Carrying(pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            var put = Toils_General.Wait(PostMath.PutInTicks, TargetIndex.None);
            put.WithProgressBarToilDelay(TargetIndex.A);
            put.handlingFacing = true;
            put.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            yield return put;
            var done = ToilMaker.MakeToil("PostPutIn");
            done.initAction = () =>
            {
                try
                {
                    Outbox.BroughtIn(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: a letter put in", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>A carrier coming in walks the mail to the mailbag (target A) and sets it down: each letter opens then (Post/MailIn.cs).</summary>
    public class JobDriver_PostDrop : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Mail_Report_Drop".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            var set = Toils_General.Wait(120, TargetIndex.None);
            set.WithProgressBarToilDelay(TargetIndex.A);
            set.handlingFacing = true;
            set.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            yield return set;
            var drop = ToilMaker.MakeToil("PostDrop");
            drop.initAction = () =>
            {
                try
                {
                    MailIn.Delivered(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: mail set down", e);
                }
            };
            drop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return drop;
        }
    }

    /// <summary>
    /// A tribal colony's spoken message: a colonist walks over to the carrier waiting at the mailbag (target A) and says
    /// the words, a line at the start and their answer at the end; the message goes with them (Post/MailWatch.cs).
    /// </summary>
    public class JobDriver_PostSpeak : JobDriver
    {
        private Pawn Carrier => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => Carrier != null ? "CC_Mail_Report_Speak".Translate(Carrier.LabelShort).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => MailWatch.SpokenFor(Carrier) == null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var talk = ToilMaker.MakeToil("PostSpeak");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 600;
            talk.handlingFacing = true;
            talk.initAction = () =>
            {
                var l = MailWatch.SpokenFor(Carrier);
                PostWords.Say(pawn, "speak", l);
            };
            talk.tickIntervalAction = _ =>
            {
                if (Carrier != null) pawn.rotationTracker.FaceTarget(Carrier);
            };
            talk.WithProgressBarToilDelay(TargetIndex.A);
            yield return talk;
            var done = ToilMaker.MakeToil("PostSpoken");
            done.initAction = () =>
            {
                try
                {
                    // together: the job brought the colonist face to face with the carrier.
                    MailWatch.Spoke(pawn, Carrier);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: words given", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>The colony's own rider takes the letter from the mailbag (target A), walks to the map's edge (target B) and sets out (Post/OwnRiders.cs).</summary>
    public class JobDriver_RiderSetOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Mail_Report_SetOut".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            var take = Toils_General.Wait(90, TargetIndex.None);
            take.WithProgressBarToilDelay(TargetIndex.A);
            yield return take;
            var took = ToilMaker.MakeToil("RiderTook");
            took.initAction = () => OwnRiders.TookLetter(pawn);
            took.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return took;
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("RiderSetOut");
            go.initAction = () => OwnRiders.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
