using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    // The jobs of the callers (Visitors/Callers.cs): what each of them, and the colonists and animals they see to, do on
    // the map. Every one ends at danger, and puts down what it carries if it's cut short.

    /// <summary>The game's effects and sounds the callers' work shows, found by name once.</summary>
    public static class CallerLooks
    {
        private static bool looked;
        private static EffecterDef tailor;
        private static SoundDef tailorSound, tendSound;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            tailor = DefDatabase<EffecterDef>.GetNamedSilentFail("Tailor");
            tailorSound = DefDatabase<SoundDef>.GetNamedSilentFail("Recipe_Tailor");
            tendSound = DefDatabase<SoundDef>.GetNamedSilentFail("Interact_Tend");
        }

        /// <summary>Snipping: the tailor's work effect.</summary>
        public static EffecterDef Snip
        {
            get
            {
                Look();
                return tailor;
            }
        }

        public static SoundDef SnipSound
        {
            get
            {
                Look();
                return tailorSound;
            }
        }

        public static SoundDef TendSound
        {
            get
            {
                Look();
                return tendSound;
            }
        }

        public static bool Danger(Pawn p) => p.Map == null || p.Map.dangerWatcher.DangerRating != StoryDanger.None;
    }

    /// <summary>The collector before a piece of the colony's art: to a place in front of it (A), and a while there looking at it (B).</summary>
    public class JobDriver_CallerView : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            job.targetB.Thing is Thing t ? "CC_Caller_Report_View".Translate(PlayGive.Label(t).Named("THING")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var look = ToilMaker.MakeToil("CallerView");
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = Mathf.Max(120, job.count);
            look.handlingFacing = true;
            look.socialMode = RandomSocialMode.Quiet;
            look.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetB);
            yield return look;
            yield return Toils_General.Do(() => Callers.Viewed(pawn, job.targetB.Thing));
        }
    }

    /// <summary>
    /// One of the colony carrying a piece of art out to the collector: to the piece (A), taking it up first if it stands
    /// installed (the game's own uninstalling, a while at it), then over to the collector (B), into whose hands it goes.
    /// </summary>
    public class JobDriver_CallerCarryArt : JobDriver
    {
        private bool handed;

        private Pawn Buyer => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport() =>
            Buyer != null && (job.targetA.Thing ?? pawn.carryTracker?.CarriedThing) is Thing t
                ? "CC_Caller_Report_CarryArt".Translate(PlayGive.Label(t).Named("THING"), Buyer.Named("OTHER")).Resolve()
                : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn) || !PlayOuts.CanJoin(pawn));
            this.FailOn(() => Buyer == null || Buyer.Dead || !Buyer.Spawned || Buyer.Map != pawn.Map);
            AddFinishAction(cond =>
            {
                try
                {
                    // Not handed over: it stays the colony's, set down where they stand.
                    if (!handed && pawn.carryTracker?.CarriedThing != null && pawn.Spawned) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("art carried out", e);
                }
            });
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            if (job.targetA.Thing is Building b && b.def.Minifiable)
            {
                var up = ToilMaker.MakeToil("CallerUninstall");
                up.defaultCompleteMode = ToilCompleteMode.Delay;
                up.defaultDuration = Mathf.Clamp(Mathf.RoundToInt(b.def.building?.uninstallWork ?? 300f), 200, 1500);
                up.handlingFacing = true;
                up.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
                up.WithProgressBarToilDelay(TargetIndex.A);
                up.WithEffect(DefDatabase<EffecterDef>.GetNamedSilentFail("ConstructWood") ?? EffecterDefOf.ConstructMetal, TargetIndex.A);
                up.activeSkill = () => SkillDefOf.Construction;
                yield return up;
                yield return Toils_General.Do(() =>
                {
                    // The game's own uninstalling: the piece packed up where it stood, for carrying.
                    var packed = MinifyUtility.Uninstall(job.targetA.Thing);
                    if (packed != null) job.SetTarget(TargetIndex.A, packed);
                    else EndJobWith(JobCondition.Incompletable);
                });
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            }
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var hand = ToilMaker.MakeToil("CallerHandArt");
            hand.initAction = () =>
            {
                try
                {
                    // together: the carrier has walked the piece over to the collector.
                    Callers.Handed(pawn, Buyer);
                    handed = pawn.carryTracker?.CarriedThing == null;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("art handed over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 90;
            hand.handlingFacing = true;
            hand.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetB);
            yield return hand;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handed, "handed");
        }
    }

    /// <summary>The barber at their stool (B), waiting for the colonist (A) to come over, then the cut, snipping, until it's done.</summary>
    public class JobDriver_CallerCut : JobDriver
    {
        private Pawn Client => job.targetA.Thing as Pawn;

        private int waited;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Client != null ? "CC_Caller_Report_Cut".Translate(Client.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waited, "waited");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Client == null || Client.Dead || !Client.Spawned || Client.Map != pawn.Map || Client.Drafted || Client.InMentalState);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var wait = ToilMaker.MakeToil("CallerCutWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                waited += delta;
                if (Client.Position.InHorDistOf(pawn.Position, 1.5f)) ReadyForNextToil();
                // They aren't coming after all (called away, or a long way off): on to the next.
                else if (Client.CurJobDef != CallerDefOf.Rimfolk_CallerGetCut || waited > 2500) EndJobWith(JobCondition.Incompletable);
            };
            yield return wait;
            var cut = ToilMaker.MakeToil("CallerCut");
            cut.defaultCompleteMode = ToilCompleteMode.Delay;
            cut.defaultDuration = Mathf.Max(200, job.count);
            cut.handlingFacing = true;
            cut.socialMode = RandomSocialMode.SuperActive;
            cut.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (!Client.Position.InHorDistOf(pawn.Position, 2.5f)) EndJobWith(JobCondition.Incompletable);
            };
            cut.WithProgressBarToilDelay(TargetIndex.A);
            if (CallerLooks.Snip != null) cut.WithEffect(CallerLooks.Snip, TargetIndex.A);
            if (CallerLooks.SnipSound != null) cut.PlaySustainerOrSound(CallerLooks.SnipSound);
            yield return cut;
            yield return Toils_General.Do(() => Callers.Cut(pawn, Client));
        }
    }

    /// <summary>A colonist going over to the barber (A) for their cut, and sitting still for it, facing them, until it's done.</summary>
    public class JobDriver_CallerGetCut : JobDriver
    {
        private Pawn Barber => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Barber != null ? "CC_Caller_Report_GetCut".Translate(Barber.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Barber == null || Barber.Dead || !Barber.Spawned || Barber.Map != pawn.Map);
            this.FailOn(() => Barber.CurJobDef != CallerDefOf.Rimfolk_CallerCut || Barber.CurJob.targetA.Thing != pawn);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var sit = ToilMaker.MakeToil("CallerGetCut");
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = Mathf.Max(200, job.count);
            sit.handlingFacing = true;
            sit.socialMode = RandomSocialMode.SuperActive;
            sit.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                PawnUtility.GainComfortFromCellIfPossible(pawn, delta);
            };
            yield return sit;
        }
    }

    /// <summary>Someone a caller works on standing still for it (an animal being tended or trained), facing them, until the work is done or the caller moves off.</summary>
    public class JobDriver_CallerHold : JobDriver
    {
        private Pawn Other => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Other != null ? "CC_Service_Report_Hold".Translate(Other.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Other == null || Other.Dead || !Other.Spawned || Other.Map != pawn.Map);
            this.FailOn(() => CallerLooks.Danger(pawn));
            var stand = ToilMaker.MakeToil("CallerHold");
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = Mathf.Max(60, job.count);
            stand.handlingFacing = true;
            stand.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(Other);
                if (!Callers.WorkingOn(Other, pawn)) ReadyForNextToil();
            };
            yield return stand;
        }
    }

    /// <summary>The animal doctor tending one of the colony's animals (A) where it is: over to it, kneeling to it a while, the game's own tend.</summary>
    public class JobDriver_CallerTend : JobDriver
    {
        private Pawn Animal => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Animal != null ? "CC_Caller_Report_Tend".Translate(Animal.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Animal == null || Animal.Dead || !Animal.Spawned || Animal.Map != pawn.Map);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var tend = ToilMaker.MakeToil("CallerTend");
            tend.initAction = () =>
            {
                // together: the animal doctor is at its side; an animal on its feet stands still for it.
                if (!Animal.Downed && Animal.Awake()) Callers.HoldStill(Animal, pawn, job.count + 120);
            };
            tend.defaultCompleteMode = ToilCompleteMode.Delay;
            tend.defaultDuration = Mathf.Max(200, job.count);
            tend.handlingFacing = true;
            tend.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (!Animal.Position.InHorDistOf(pawn.Position, 2.5f)) EndJobWith(JobCondition.Incompletable);
            };
            tend.WithProgressBarToilDelay(TargetIndex.A);
            if (CallerLooks.TendSound != null) tend.PlaySustainerOrSound(CallerLooks.TendSound);
            yield return tend;
            yield return Toils_General.Do(() => Callers.Tended(pawn, Animal));
        }
    }

    /// <summary>The animal doctor working a young animal of the colony's (A) a while, with the game's own training talk, one step of its training at the end.</summary>
    public class JobDriver_CallerTrain : JobDriver
    {
        private Pawn Animal => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Animal != null ? "CC_Caller_Report_Train".Translate(Animal.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Animal == null || Animal.Dead || !Animal.Spawned || Animal.Map != pawn.Map || Animal.Downed);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("CallerTrain");
            work.initAction = () =>
            {
                // together: the animal doctor is at its side, and it stands for the session.
                Callers.HoldStill(Animal, pawn, job.count + 120);
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(600, job.count);
            work.handlingFacing = true;
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (!Animal.Position.InHorDistOf(pawn.Position, 2.5f))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // together: the doctor beside the animal, giving it the game's own training words now and then.
                if (pawn.IsHashIntervalTick(500, delta)) pawn.interactions?.TryInteractWith(Animal, InteractionDefOf.TrainAttempt);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            yield return work;
            yield return Toils_General.Do(() => Callers.Trained(pawn, Animal));
        }
    }

    /// <summary>An undertaker digging a grave at a place of the row (A): a while at it with the game's own digging effect, and the grave stands there.</summary>
    public class JobDriver_CallerDig : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Caller_Report_Dig".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var dig = ToilMaker.MakeToil("CallerDig");
            dig.defaultCompleteMode = ToilCompleteMode.Delay;
            dig.defaultDuration = Mathf.Max(300, job.count);
            dig.handlingFacing = true;
            dig.tickIntervalAction = delta => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            dig.WithProgressBarToilDelay(TargetIndex.A);
            dig.WithEffect(EffecterDefOf.ConstructDirt, TargetIndex.A);
            yield return dig;
            yield return Toils_General.Do(() => Callers.Dug(pawn, job.targetA.Cell));
        }
    }

    /// <summary>An undertaker carrying one of the enemy dead (A) to a grave they dug (B) and laying it in.</summary>
    public class JobDriver_CallerBury : JobDriver
    {
        private bool laid;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport() => "CC_Caller_Report_Bury".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOnDestroyedOrNull(TargetIndex.B);
            AddFinishAction(cond =>
            {
                if (!laid && pawn.carryTracker?.CarriedThing != null && pawn.Spawned) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var lay = ToilMaker.MakeToil("CallerBury");
            lay.defaultCompleteMode = ToilCompleteMode.Delay;
            lay.defaultDuration = 180;
            lay.WithProgressBarToilDelay(TargetIndex.B);
            yield return lay;
            yield return Toils_General.Do(() =>
            {
                Callers.Buried(pawn, job.targetB.Thing as Building_Grave);
                laid = pawn.carryTracker?.CarriedThing == null;
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref laid, "laid");
        }
    }

    /// <summary>An undertaker carrying one of the enemy dead (A), and the weapons lying by it, to their pack beast (B), and loading it on.</summary>
    public class JobDriver_CallerLoad : JobDriver
    {
        private bool loaded;

        private Pawn Beast => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport() => Beast != null ? "CC_Caller_Report_Load".Translate(Beast.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Beast == null || Beast.Dead || !Beast.Spawned || Beast.Map != pawn.Map);
            AddFinishAction(cond =>
            {
                if (!loaded && pawn.carryTracker?.CarriedThing != null && pawn.Spawned) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_General.Do(() => Callers.TookUp(pawn, job.targetA.Thing));
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var load = ToilMaker.MakeToil("CallerLoad");
            load.defaultCompleteMode = ToilCompleteMode.Delay;
            load.defaultDuration = 150;
            load.handlingFacing = true;
            load.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetB);
            yield return load;
            yield return Toils_General.Do(() =>
            {
                Callers.Loaded(pawn, Beast);
                loaded = pawn.carryTracker?.CarriedThing == null;
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref loaded, "loaded");
        }
    }

    /// <summary>One of a captive's kin at the door of their room (B), talking with them (A) an hour.</summary>
    public class JobDriver_CallerTalk : JobDriver
    {
        private Pawn Captive => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Captive != null ? "CC_Caller_Report_Talk".Translate(Captive.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CallerLooks.Danger(pawn));
            this.FailOn(() => Captive == null || Captive.Dead || !Captive.Spawned || Captive.Map != pawn.Map);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var talk = ToilMaker.MakeToil("CallerTalk");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = Mathf.Max(600, job.count);
            talk.handlingFacing = true;
            talk.socialMode = RandomSocialMode.SuperActive;
            talk.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
            yield return talk;
            // together: the kin has stood an hour at the door, talking with them.
            yield return Toils_General.Do(() => Callers.Talked(pawn, Captive));
        }
    }
}
