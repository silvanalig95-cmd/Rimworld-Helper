using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What a worship job is for (its JobDef's <see cref="WorshipJobExtension"/>).</summary>
    public enum WorshipAct
    {
        /// <summary>Dedicating a place to a god: the words said there.</summary>
        Dedicate,
        /// <summary>A prayer at a shrine, or where they stand.</summary>
        Pray,
        /// <summary>A festival's rite at the god's shrine, led by the priest or the keeper of the day.</summary>
        Rite
    }

    /// <summary>The act a worship job's JobDef stands for (Defs/GodDefs/Rimfolk_Worship_Jobs.xml).</summary>
    public class WorshipJobExtension : DefModExtension
    {
        public WorshipAct act;
    }

    /// <summary>
    /// A worship act at a place (Gods/Worship/): walk to the shrine (or stay where they stand, for a prayer said there), say
    /// the line as they arrive, and stay a while facing it, kneeling still; then it is done, and the book hears of it
    /// (a place dedicated, a prayer said, a rite held). Danger, drafting, a mental state or the player's word ends it unsaid.
    /// </summary>
    public class JobDriver_WorshipAt : JobDriver
    {
        private bool arrived;

        private WorshipAct Act => job.def.GetModExtension<WorshipJobExtension>()?.act ?? WorshipAct.Pray;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!job.targetA.HasThing && job.targetA.IsValid && job.targetA.Cell != pawn.Position)
                pawn.Map?.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            return true;
        }

        public override string GetReport()
        {
            God g = null;
            try
            {
                g = Act == WorshipAct.Dedicate || Act == WorshipAct.Rite ? Shrines.GodAt(job.targetA.Thing) : Prayers.GodFor(pawn);
            }
            catch
            {
            }
            string key = "CC_Wsh_Report_" + Act + (g != null ? "" : "_NoGod");
            return key.Translate(Worship.Called(g).Named("GOD")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Worship.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else if (job.targetA.IsValid && job.targetA.Cell != pawn.Position)
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("WorshipAt");
            stay.initAction = Arrive;
            stay.tickIntervalAction = delta =>
            {
                if (job.targetA.HasThing) pawn.rotationTracker.FaceTarget(job.targetA);
                // A small light now and then as they pray.
                if (Act != WorshipAct.Dedicate && pawn.IsHashIntervalTick(400, delta))
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Meditating);
            };
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(200, job.count);
            stay.handlingFacing = true;
            stay.socialMode = RandomSocialMode.Off;
            // A festival's rite is kept with the chant of the god's kind, where Alpha Memes or Ideology has one.
            if (Act == WorshipAct.Rite)
            {
                SoundDef chant = null;
                try
                {
                    chant = Festivals.ChantFor(Shrines.GodAt(job.targetA.Thing));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: a rite's chant", e);
                }
                if (chant != null) stay.PlaySustainerOrSound(chant);
            }
            yield return stay;
            yield return Toils_General.Do(Done);
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            try
            {
                switch (Act)
                {
                    case WorshipAct.Dedicate:
                        Shrines.Arrive(pawn, job.targetA.Thing);
                        break;
                    case WorshipAct.Rite:
                        Festivals.RiteArrive(pawn, job.targetA.Thing);
                        break;
                    default:
                        Prayers.Arrive(pawn, job.targetA.Thing);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: arriving", e);
            }
        }

        private void Done()
        {
            try
            {
                switch (Act)
                {
                    case WorshipAct.Dedicate:
                        Shrines.Dedicated(pawn, job.targetA.Thing);
                        break;
                    case WorshipAct.Rite:
                        Festivals.RiteDone(pawn, job.targetA.Thing);
                        break;
                    default:
                        Prayers.Prayed(pawn, job.targetA.Thing);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: done", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }

    /// <summary>
    /// An offering carried to a shrine (Worship/Offerings.cs): take it from the stores, carry it to the shrine, set it down on a
    /// free cell beside it (left forbidden, so nobody carries it back), and stay a moment with a word to the god. Danger or
    /// need ends it, and whatever they carry is set down where they stand, still the colony's.
    /// </summary>
    public class JobDriver_CarryOffering : JobDriver
    {
        private bool placed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, job.count, null, errorOnFailed);

        public override string GetReport()
        {
            var g = Offerings.GodOf(job.targetA.Thing ?? pawn.carryTracker?.CarriedThing, job.targetB.Thing);
            return (g != null ? "CC_Wsh_Report_Offer" : "CC_Wsh_Report_Offer_NoGod").Translate(Worship.Called(g).Named("GOD")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Worship.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            AddFinishAction(cond =>
            {
                try
                {
                    if (!placed && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                    if (!placed) Offerings.Dropped(pawn, job.targetA.Thing);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: an offering not set down", e);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var set = ToilMaker.MakeToil("SetOffering");
            set.initAction = () =>
            {
                try
                {
                    var shrine = job.targetB.Thing;
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried == null || shrine == null) return;
                    var cell = Offerings.DropCell(shrine, pawn);
                    if (!cell.IsValid) return;
                    if (!pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var lying) || lying == null) return;
                    placed = true;
                    Offerings.Placed(pawn, lying, shrine);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: setting an offering down", e);
                }
            };
            set.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return set;
            var stay = ToilMaker.MakeToil("OfferingWord");
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = 240;
            stay.handlingFacing = true;
            stay.tickIntervalAction = _ => pawn.rotationTracker.FaceTarget(job.targetB);
            yield return stay;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref placed, "placed");
        }
    }

    /// <summary>
    /// A pilgrimage on foot (a vowed life goal, Worship/Vows.cs): out to the god's holy place on the colony's land (an old
    /// tree, the water's edge, the graves, the far edge of the land), a word as they set out, an hour there in prayer, and
    /// home again. Danger ends it, and it is walked another day.
    /// </summary>
    public class JobDriver_Pilgrimage : JobDriver
    {
        private bool there;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var g = Vows.PilgrimGod(pawn);
            return (there ? "CC_Wsh_Report_PilgrimThere" : "CC_Wsh_Report_Pilgrim").Translate(Worship.Called(g).Named("GOD")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Worship.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_General.Do(() => Vows.PilgrimSetsOut(pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var pray = ToilMaker.MakeToil("PilgrimPrays");
            pray.initAction = () =>
            {
                there = true;
                Vows.PilgrimArrives(pawn, job.targetA.Cell);
            };
            pray.defaultCompleteMode = ToilCompleteMode.Delay;
            pray.defaultDuration = Mathf.Max(600, job.count);
            pray.handlingFacing = true;
            pray.tickIntervalAction = delta =>
            {
                if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                if (pawn.IsHashIntervalTick(500, delta))
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Meditating);
            };
            yield return pray;
            yield return Toils_General.Do(() => Vows.PilgrimageMade(pawn));
            if (job.targetB.IsValid) yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref there, "there");
        }
    }

    /// <summary>Starting worship's jobs, safely: only someone up and about, safe, in their free time and at nobody's gathering.</summary>
    public static class WorshipJobs
    {
        public static JobDef Def(string name) => DefDatabase<JobDef>.GetNamedSilentFail(name);

        public const string DedicateJob = "Rimfolk_Worship_Dedicate", PrayJob = "Rimfolk_Worship_Pray", RiteJob = "Rimfolk_Worship_Rite",
            OfferJob = "Rimfolk_Worship_Offer", PilgrimJob = "Rimfolk_Worship_Pilgrimage";

        /// <summary>Up and about, safe, at home, at nobody's play-out or gathering; with <paramref name="freeTime"/>, in their free time too.</summary>
        public static bool Free(Pawn p, bool freeTime = true)
        {
            if (!HeartActs.Safe(p, null)) return false;
            if (p.GetLord() != null || (p.CurJob?.playerForced ?? false)) return false;
            if (p.CurJobDef != null && p.CurJob.def.driverClass != null && typeof(JobDriver_WorshipAt).IsAssignableFrom(p.CurJob.def.driverClass)) return false;
            return !freeTime || Together.FreeTime(p);
        }

        /// <summary>Starts a job now (it takes the place of what they were idling at); false when it didn't start.</summary>
        public static bool Start(Pawn p, Job job)
        {
            if (p?.jobs == null || job == null) return false;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJob == job;
        }

        /// <summary>A worship act at a place: <paramref name="ticks"/> there (the book knows which god it is for).</summary>
        public static Job At(string def, LocalTargetInfo where, int ticks)
        {
            var d = Def(def);
            if (d == null) return null;
            var job = JobMaker.MakeJob(d, where);
            job.count = ticks;
            return job;
        }
    }
}
