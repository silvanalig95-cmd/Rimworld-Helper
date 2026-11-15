using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class AnimalDaysDefOf
    {
        public static JobDef Rimfolk_AnimalBeg;
        public static JobDef Rimfolk_AnimalShelter;
        public static JobDef Rimfolk_AnimalMourn;
        public static JobDef Rimfolk_AnimalPlay;
        public static JobDef Rimfolk_PlayWithAnimal;
        public static JobDef Rimfolk_KeeperRound;
        public static JobDef Rimfolk_SitWithAnimal;

        static AnimalDaysDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(AnimalDaysDefOf));
    }

    /// <summary>
    /// An animal at play (Animals/AnimalDays.cs, AnimalScenes.cs; Defs/Misc/Rimfolk_AnimalDays_Jobs.xml): for a while (the
    /// job's count, in ticks) it bounds from spot to spot about target A (a child it plays chase with, someone it shows a trick,
    /// or the snow), at a run. Target C, when there is one, is whom it plays with, by name in its report. Ended by danger.
    /// </summary>
    public class JobDriver_AnimalPlay : JobDriver
    {
        private int until = -1;
        private string about;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            about = (job.targetC.Thing as Pawn)?.LabelShort ?? (job.targetA.Thing as Pawn)?.LabelShort;
        }

        public override string GetReport() =>
            !about.NullOrEmpty() && "CC_AnimalJob_Play".CanTranslate() ? "CC_AnimalJob_Play".Translate(about.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed || pawn.InMentalState || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            var play = ToilMaker.MakeToil("AnimalPlay");
            play.initAction = () =>
            {
                if (until < 0) until = Find.TickManager.TicksGame + Math.Max(300, job.count);
                Bound();
            };
            play.tickIntervalAction = delta =>
            {
                if (job.targetA.HasThing && (!job.targetA.Thing.Spawned || job.targetA.Thing.Map != pawn.Map))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (Find.TickManager.TicksGame >= until)
                {
                    ReadyForNextToil();
                    return;
                }
                if (!pawn.pather.Moving) Bound();
            };
            play.defaultCompleteMode = ToilCompleteMode.Never;
            yield return play;
        }

        /// <summary>A bound to another spot near what it plays about, a few steps off.</summary>
        private void Bound()
        {
            var map = pawn.Map;
            var at = job.targetA.HasThing ? job.targetA.Thing.Position : job.targetA.Cell;
            if (!at.IsValid || map == null) return;
            if (CellFinder.TryFindRandomCellNear(at, map, 4, c => c != pawn.Position && c.Standable(map) && !c.Fogged(map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                pawn.pather.StartPath(cell, PathEndMode.OnCell);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref about, "about");
        }
    }

    /// <summary>
    /// A colonist (a child, most often) playing chase with an animal (target A): for a while (the job's count) they run after
    /// it wherever it goes, and enjoy it. Ended by danger or when the animal is gone.
    /// </summary>
    public class JobDriver_PlayWithAnimal : JobDriver
    {
        private int until = -1;

        private Pawn Animal => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => Animal == null || Animal.Dead || !Animal.Spawned || Animal.Map != pawn.Map || Animal.Downed);
            var play = ToilMaker.MakeToil("PlayWithAnimal");
            play.initAction = () =>
            {
                if (until < 0) until = Find.TickManager.TicksGame + Math.Max(300, job.count);
            };
            play.tickIntervalAction = delta =>
            {
                if (Find.TickManager.TicksGame >= until)
                {
                    ReadyForNextToil();
                    return;
                }
                var a = Animal;
                if (!pawn.pather.Moving && !pawn.Position.InHorDistOf(a.Position, 1.9f)) pawn.pather.StartPath(a, PathEndMode.Touch);
                pawn.needs?.joy?.GainJoy(AnimalDaysMath.PlayJoyPerTick * delta, JoyKindDefOf.Social);
            };
            play.defaultCompleteMode = ToilCompleteMode.Never;
            play.socialMode = RandomSocialMode.Off;
            yield return play;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref until, "until", -1);
        }
    }

    /// <summary>
    /// Someone sitting with an animal a while (target A): walking over, then staying beside it facing it for the job's count
    /// of ticks, talking to it or watching what it does. A little company for them. Ended by danger or when it's gone.
    /// </summary>
    public class JobDriver_SitWithAnimal : JobDriver
    {
        private Pawn Animal => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => Animal == null || Animal.Dead || !Animal.Spawned || Animal.Map != pawn.Map);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var sit = ToilMaker.MakeToil("SitWithAnimal");
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = Math.Max(300, job.count);
            sit.handlingFacing = true;
            sit.tickIntervalAction = delta =>
            {
                var a = Animal;
                if (!pawn.Position.InHorDistOf(a.Position, 4f))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(a);
                pawn.needs?.joy?.GainJoy(AnimalDaysMath.PlayJoyPerTick * 0.6f * delta, JoyKindDefOf.Social);
            };
            yield return sit;
        }
    }

    /// <summary>
    /// The keeper's round of the animals (the herdmaster, or whoever looks after them): to each animal of the job's queue in
    /// turn, a while beside it looking it over (it holds still for them), a little handling practice; then on to the next.
    /// The first one gets a word (AnimalDays.Looked). Ended by danger.
    /// </summary>
    public class JobDriver_KeeperRound : JobDriver
    {
        private int looked;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            var go = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var next = ToilMaker.MakeToil("KeeperNext");
            next.initAction = () =>
            {
                var queue = job.GetTargetQueue(TargetIndex.A);
                while (queue != null && queue.Count > 0)
                {
                    var t = queue[0];
                    queue.RemoveAt(0);
                    if (t.Thing is Pawn a && a.Spawned && !a.Dead && a.Map == pawn.Map && pawn.CanReach(a, PathEndMode.Touch, Danger.Some))
                    {
                        job.SetTarget(TargetIndex.A, a);
                        JumpToToil(go);
                        return;
                    }
                }
                EndJobWith(JobCondition.Succeeded);
            };
            // The first animal is target A as the job starts; a gone one is passed over.
            yield return Toils_Jump.JumpIf(next, () => !(job.targetA.Thing is Pawn first) || first.Dead || !first.Spawned || first.Map != pawn.Map);
            yield return go;
            var look = ToilMaker.MakeToil("KeeperLooks");
            look.initAction = () =>
            {
                if (job.targetA.Thing is Pawn a)
                {
                    AnimalDays.Looked(pawn, a, looked == 0);
                    looked++;
                }
            };
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = AnimalDaysMath.LookTicks;
            look.handlingFacing = true;
            look.tickIntervalAction = delta =>
            {
                if (job.targetA.Thing is Pawn a && a.Spawned) pawn.rotationTracker.FaceTarget(a);
                pawn.skills?.Learn(SkillDefOf.Animals, 0.04f * delta);
            };
            look.WithProgressBarToilDelay(TargetIndex.A);
            yield return look;
            yield return next;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref looked, "looked");
        }
    }
}
