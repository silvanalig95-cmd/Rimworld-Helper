using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class OutsiderTendDefOf
    {
        public static JobDef Rimfolk_TendOutsider;

        static OutsiderTendDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OutsiderTendDefOf));
        }
    }

    /// <summary>
    /// The colony's doctor seeing to someone from outside where they are: the camp's sick by their fire, a war party's
    /// wounded, a patient come to the gate. They fetch a medicine from the stores (the plainest first, as answers take
    /// it), carry it over, kneel to it a while and tend with it (the game's own tend), one patient after another. The game's
    /// own tending wants a patient in bed, which someone at a camp or a gate seldom is; this goes to them where they are.
    /// </summary>
    public static class OutsiderTend
    {
        /// <summary>Sends the doctor to each patient in turn. False when nothing could be set going (then the answer's effect is had as before).</summary>
        public static bool Send(Pawn doctor, IEnumerable<Pawn> patients)
        {
            var map = doctor?.Map;
            if (map == null || doctor.Dead || doctor.Downed || doctor.jobs == null || doctor.WorkTagIsDisabled(WorkTags.Caring)) return false;
            var jobs = new List<Job>();
            foreach (var p in patients)
            {
                if (p == null || p.Dead || !p.Spawned || p.Map != map || !doctor.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;
                var med = DilemmaEffect_Consume.Medicine(map).FirstOrDefault(m => m.Spawned && !m.IsForbidden(doctor) && doctor.CanReserveAndReach(m, PathEndMode.ClosestTouch, Danger.Some));
                var job = JobMaker.MakeJob(OutsiderTendDefOf.Rimfolk_TendOutsider, p, med);
                job.count = 1;
                jobs.Add(job);
            }
            if (jobs.Count == 0) return false;
            // apart: the doctor's own job; the patient is only its target.
            if (!doctor.jobs.TryTakeOrderedJob(jobs[0], JobTag.Misc)) return false;
            for (int i = 1; i < jobs.Count; i++) doctor.jobs.jobQueue.EnqueueLast(jobs[i], JobTag.Misc);
            return true;
        }

        /// <summary>The tend itself, with the medicine carried there: the game's own, and the camp fever's worst over if that's what it was.</summary>
        public static void Done(Pawn doctor, Pawn patient)
        {
            if (doctor == null || patient == null || patient.Dead) return;
            var carried = doctor.carryTracker?.CarriedThing;
            var med = carried as Medicine;
            try
            {
                TendUtility.DoTend(doctor, patient, med);
                var flu = Camp.Fever != null ? patient.health?.hediffSet?.GetFirstHediffOfDef(Camp.Fever) : null;
                if (flu != null) flu.Severity = Mathf.Min(flu.Severity, 0.15f);
                KinMemory.Notify_Tended(doctor, patient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tending someone from outside", e);
            }
            // The medicine is spent on them, whatever the tend made of it.
            if (carried != null && !carried.Destroyed && doctor.carryTracker.CarriedThing == carried) doctor.carryTracker.DestroyCarriedThing();
        }
    }

    /// <summary>
    /// A (the colony's doctor) sees to the event's visitor X where they are, with a medicine from the stores carried over
    /// (OutsiderTend). Greyed out without a medicine, as taking one is; should A be unable to go, the medicine is used as before.
    /// </summary>
    public class DilemmaEffect_TendStranger : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) => new DilemmaEffect_Consume { what = "medicine", count = 1 }.Unavailable(ctx);

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.a != null && ctx.stranger != null && OutsiderTend.Send(ctx.a, new[] { ctx.stranger })) return;
            if (ctx?.map != null) PlayCarry.Take(ctx.map, "medicine", null, 1);
        }
    }

    /// <summary>Fetch the medicine (if there's any), go to the patient, kneel to them a while, tend.</summary>
    public class JobDriver_TendOutsider : JobDriver
    {
        private Pawn Patient => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.targetB.HasThing || pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Patient == null || Patient.Dead || !Patient.Spawned || Patient.Map != pawn.Map);
            if (job.targetB.HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            int ticks = Mathf.RoundToInt(600f / Mathf.Max(0.1f, pawn.GetStatValue(StatDefOf.MedicalTendSpeed)));
            var tend = Toils_General.Wait(ticks, TargetIndex.A);
            tend.WithProgressBarToilDelay(TargetIndex.A);
            tend.activeSkill = () => SkillDefOf.Medicine;
            yield return tend;
            yield return Toils_General.Do(() => OutsiderTend.Done(pawn, Patient));
        }
    }
}
