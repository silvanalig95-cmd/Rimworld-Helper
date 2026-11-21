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
        public static JobDef Rimfolk_AnimalGreet;
        public static JobDef Rimfolk_AnimalKeepClose;
        public static JobDef Rimfolk_AnimalPester;
        public static JobDef Rimfolk_AnimalGuard;
        public static JobDef Rimfolk_AnimalCry;

        static AnimalDaysDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(AnimalDaysDefOf));
    }

    /// <summary>
    /// What is said or told once an animal (or someone going to one) gets where it was going, so the words come when the
    /// thing is seen: the line as the pet reaches someone eating, the encounter told as the dog reaches its person. Keyed by
    /// the job's id (jobs are pooled and reused, their ids are not); this session's only, so after a load the job plays out
    /// without its words. Called by JobDriver_AnimalLinger, JobDriver_AnimalRunTo and JobDriver_SitWithAnimal as they arrive.
    /// </summary>
    internal static class AnimalArrivals
    {
        private static readonly Dictionary<int, Action> waiting = new Dictionary<int, Action>();
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            waiting.Clear();
        }

        /// <summary>Runs <paramref name="then"/> when this job arrives. Set before the job starts: one already there arrives at once.</summary>
        internal static void On(Job job, Action then)
        {
            if (job == null || then == null) return;
            Fresh();
            // Jobs cut short never arrive: what they left is dropped now and then.
            if (waiting.Count > 200) waiting.Clear();
            waiting[job.loadID] = then;
        }

        internal static void Forget(Job job)
        {
            if (job != null) waiting.Remove(job.loadID);
        }

        internal static void Arrived(Job job)
        {
            if (job == null) return;
            Fresh();
            if (!waiting.TryGetValue(job.loadID, out var then)) return;
            waiting.Remove(job.loadID);
            try
            {
                then();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal arrivals", e);
            }
        }
    }

    /// <summary>How an animal runs to someone (Defs/Misc/Rimfolk_AnimalDays_Jobs.xml): with the game's own nuzzle when it gets there, or not.</summary>
    public class AnimalRunToExtension : DefModExtension
    {
        public bool nuzzle;
        /// <summary>Its report with whom it runs to ({OTHER}), a translation key; the job's own words without one.</summary>
        public string report;
    }

    /// <summary>
    /// An animal going to someone (target A) at the job's pace (Animals/AnimalDays.cs, AnimalScenes.cs): a pet running to its
    /// person home again, an old one walking over, one running at someone's heels in a grudge. Once there it faces them, gives
    /// the game's own nuzzle where its def says so (the nuzzle's interaction and mood, as the game's animals give it), and
    /// stays at their side for the job's count of ticks; what was waiting for it to get there is said then (AnimalArrivals).
    /// Ended by danger, or when they are gone or down.
    /// </summary>
    public class JobDriver_AnimalRunTo : JobDriver
    {
        private string about;

        private Pawn Person => job.targetA.Thing as Pawn;

        private AnimalRunToExtension Ext => job.def.GetModExtension<AnimalRunToExtension>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            about = Person?.LabelShort;
        }

        public override string GetReport()
        {
            var ext = Ext;
            if (ext?.report != null && !about.NullOrEmpty() && ext.report.CanTranslate()) return ext.report.Translate(about.Named("OTHER")).Resolve();
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Downed || pawn.InMentalState || Person == null || Person.Dead || Person.Downed
                              || pawn.Map != null && pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var meet = ToilMaker.MakeToil("AnimalRunToMeet");
            meet.initAction = () =>
            {
                var p = Person;
                if (p == null) return;
                pawn.rotationTracker.FaceTarget(p);
                // together: the job has brought it to their side (the goto before), and the game's nuzzle checks they're face to face.
                if (Ext?.nuzzle == true && pawn.interactions != null && InteractionDefOf.Nuzzle != null) pawn.interactions.TryInteractWith(p, InteractionDefOf.Nuzzle);
                AnimalArrivals.Arrived(job);
            };
            meet.defaultCompleteMode = ToilCompleteMode.Delay;
            meet.defaultDuration = Math.Max(60, job.count);
            // The game turns it: to its path when it trots after them, to them (target A, the job's target to face) beside them.
            meet.socialMode = RandomSocialMode.Off;
            meet.tickIntervalAction = delta =>
            {
                var p = Person;
                if (p == null || !p.Spawned) return;
                // Still beside them: they may walk on, and it keeps at their side for the rest of the moment.
                if (!pawn.Position.InHorDistOf(p.Position, 2.5f))
                {
                    if (!pawn.pather.Moving) pawn.pather.StartPath(p, PathEndMode.Touch);
                }
                else pawn.rotationTracker.FaceTarget(p);
            };
            yield return meet;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref about, "about");
        }
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
    /// of ticks, talking to it or watching what it does. A little company for them. What waited for them to get there is
    /// told then (AnimalArrivals). Ended by danger or when it's gone.
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
            sit.initAction = () => AnimalArrivals.Arrived(job);
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
    /// turn, a while beside it looking it over (it holds still for them), a little handling practice and a little of the
    /// recreation their free time would have given; then on to the next.
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
                // Their free time, spent with the animals: a little of the rest it takes the place of.
                pawn.needs?.joy?.GainJoy(AnimalDaysMath.PlayJoyPerTick * 0.6f * delta, JoyKindDefOf.Social);
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
