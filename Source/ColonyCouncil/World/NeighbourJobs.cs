using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Writing a letter to a neighbour (World/NeighbourLetters.cs): the writer sits at a table (target A, with the seat
    /// at target B; or on a chair, or where they stand) for about an hour, practising their way with words. When it is
    /// done a messenger is sent for. Interrupted, they sit down to it again later.
    /// </summary>
    public class JobDriver_WriteLetter : JobDriver
    {
        private NeighbourLetter Letter => NeighbourLetters.At(pawn, LetterStage.Writing);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var seat = job.targetB.Cell;
            var chair = seat.IsValid && seat.InBounds(pawn.Map) ? seat.GetEdifice(pawn.Map) : null;
            return chair == null || !(chair.def.building?.isSittable ?? false) || pawn.Reserve(chair, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            var l = Letter;
            return l != null ? "CC_Letter_Report_Write".Translate(l.toName ?? "?").Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Letter == null);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var write = ToilMaker.MakeToil("WriteLetter");
            write.defaultCompleteMode = ToilCompleteMode.Delay;
            write.defaultDuration = NeighbourLetters.WriteTicks;
            write.handlingFacing = true;
            write.initAction = () => NeighbourLetters.Say(pawn, "write", NotableBook.Instance?.Get(Letter?.notable ?? -1));
            write.tickIntervalAction = delta =>
            {
                if (job.targetA.IsValid && job.targetA.Cell != pawn.Position) pawn.rotationTracker.FaceCell(job.targetA.Cell);
                pawn.skills?.Learn(SkillDefOf.Social, 0.03f * delta);
            };
            write.WithProgressBarToilDelay(TargetIndex.B);
            yield return write;
            var done = ToilMaker.MakeToil("LetterWritten");
            done.initAction = () =>
            {
                try
                {
                    NeighbourLetters.Written(Letter, pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("letter written", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>Carrying the letter out to the messenger waiting at the gate (target A) and handing it over.</summary>
    public class JobDriver_HandLetter : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var x = job.targetA.Thing as Pawn;
            return x != null ? "CC_Letter_Report_Hand".Translate(x.LabelShort).Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                var t = CourierTracker.Instance?.TripOf(job.targetA.Thing as Pawn);
                return t == null || t.settled || t.received || t.mailId < 0;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var wait = Toils_General.Wait(90, TargetIndex.A);
            wait.handlingFacing = true;
            wait.tickIntervalAction = _ => pawn.rotationTracker.FaceTarget(job.targetA);
            yield return wait;
            var hand = ToilMaker.MakeToil("HandLetter");
            hand.initAction = () =>
            {
                try
                {
                    // together: the job brought them face to face with the messenger.
                    NeighbourLetters.Handed(pawn, job.targetA.Thing as Pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("letter handed", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return hand;
        }
    }

    /// <summary>
    /// A call to a neighbour at a comms console (target A): the caller stands at it and talks for a while, a line now
    /// and then over their head; as it ends, the notable has answered or an aide has taken a message.
    /// </summary>
    public class JobDriver_NeighbourCall : JobDriver
    {
        private NeighbourLetter Letter => NeighbourLetters.At(pawn, LetterStage.Calling);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var l = Letter;
            return l != null ? "CC_Call_Report".Translate(l.toName ?? "?").Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Letter == null || !(job.targetA.Thing is Building_CommsConsole c) || !c.CanUseCommsNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            var talk = NeighbourCallToils.Talk(this, pawn, job, "call", () => NotableBook.Instance?.Get(Letter?.notable ?? -1));
            yield return talk;
            var end = ToilMaker.MakeToil("CallEnds");
            end.initAction = () =>
            {
                try
                {
                    var n = NotableBook.Instance?.Get(Letter?.notable ?? -1);
                    string moment = NeighbourLetters.CallMade(Letter, pawn);
                    if (moment != null) NeighbourLetters.Say(pawn, "end" + moment, n);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("call made", e);
                }
            };
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }

    /// <summary>Answering a neighbour's callback at the ringing comms console (target A).</summary>
    public class JobDriver_AnswerCall : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var n = NeighbourLetters.Ringing(pawn.Map);
            return n != null ? "CC_Call_Report_Answer".Translate(n.Name).Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => NeighbourLetters.Ringing(pawn.Map) == null || !(job.targetA.Thing is Building_CommsConsole c) || !c.CanUseCommsNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Notable who = null;
            var talk = NeighbourCallToils.Talk(this, pawn, job, "answer", () => who ??= NeighbourLetters.Ringing(pawn.Map));
            yield return talk;
            var end = ToilMaker.MakeToil("CallbackEnds");
            end.initAction = () =>
            {
                try
                {
                    var n = NeighbourLetters.Ringing(pawn.Map);
                    string moment = NeighbourLetters.CallAnswered(pawn);
                    if (moment != null) NeighbourLetters.Say(pawn, "end" + moment, n);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("call answered", e);
                }
            };
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }

    /// <summary>The talk at the console both calls share: standing at it, facing it, a line at the start and one or two as it goes on.</summary>
    public static class NeighbourCallToils
    {
        public static Toil Talk(JobDriver driver, Pawn pawn, Job job, string first, Func<Notable> who)
        {
            var talk = ToilMaker.MakeToil("NeighbourTalk");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = NeighbourLetters.CallTicks;
            talk.handlingFacing = true;
            int said = 0;
            talk.initAction = () =>
            {
                said = 0;
                NeighbourLetters.Say(pawn, first, who());
            };
            talk.tickIntervalAction = delta =>
            {
                var console = job.targetA.Thing;
                if (console != null) pawn.rotationTracker.FaceCell(console.Position);
                int ticks = NeighbourLetters.CallTicks - driver.ticksLeftThisToil;
                if (said < 2 && ticks > (said + 1) * NeighbourLetters.CallTicks / 3)
                {
                    said++;
                    NeighbourLetters.Say(pawn, "talk", who());
                }
            };
            talk.WithProgressBarToilDelay(TargetIndex.A);
            return talk;
        }
    }
}
