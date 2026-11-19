using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The peoples' own events, round 4 (docs/peoples-events-proposals.md, items 18, 21, 23, 24, 27, 28, 29, 30, 31, 32 and 33;
    // the defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents4.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents4.xml):
    // the learned society's examination for a corresponding fellow and its summer school, the spacer enclave's contract and
    // its broker stranded by a pod crash, the river folk's fouled water, their lanterns on the knot night, their families
    // flooded out and their boat-builders, and the Lantern Wardens' page in the book, their run to a manhunt and the hive
    // they burn. Each comes only from a people of its calling at peace with the colony, takes the form of the colony's age
    // and theirs, and plays out on the map, built on rounds 1 to 3's shapes: the visitor at the gate, the visitor who works
    // on a colonist, the camp kind with ways of its own, the hunt's lord and skinning, the events' visits ("pev:...", dispatched through rounds 1 to 3).

    /// <summary>Round 4's visits, by kind, its watch on the homes, and its jobs' ends.</summary>
    public static class PeopleEvents4
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef CleanJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Clean");
        public static JobDef LiftJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Lift");
        public static JobDef LanternJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_SetLantern");
        public static JobDef BurnJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Burn");
        public static JobDef HullJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Hull");
        public static JobDef TeachJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Teach");

        /// <summary>An hour of one of round 4's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case BrokerPod.LanderKind: return BrokerPod.LanderHour(v, now);
                case FouledWater.CleanKind: return FouledWater.CleanHour(v, now);
                case KnotLanterns.Kind: return KnotLanterns.Hour(v, now);
                case WardensManhunt.Kind: return WardensManhunt.Hour(v, now);
                case HiveBurned.Kind: return HiveBurned.Hour(v, now);
            }
            return false;
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through round 3's): the fellows' books by the post, the contracts' due
        /// days, the pods' cargo, the knot night's dusk, a great beast's page, a manhunt, the fouled water put right.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the lanterns' night", () => KnotLanterns.Watch(map, now));
                Safe("the page's watch", () => WardensPage.Watch(map, now));
                Safe("the manhunt's watch", () => WardensManhunt.Watch(map, now));
            }
            Safe("the fellows' books", () => Fellowship.Books(now));
            Safe("the contracts", () => EnclaveContract.Watch(now));
            Safe("the pods", () => BrokerPod.Watch(now));
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Safe("the fouled water", FouledWater.Tidy);
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
            WardensManhunt.Forget();
            WardensPage.Forget();
        }

        /// <summary>One of round 4's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == CleanJob) FouledWater.Cleaned(pawn, target);
                else if (job == LiftJob) BrokerPod.Lifted(pawn, target.Thing);
                else if (job == LanternJob) KnotLanterns.Set(pawn, target.Cell);
                else if (job == BurnJob) HiveBurned.Burned(pawn, target.Thing);
                else if (job == HullJob) BoatBuilders.Worked(pawn, target.Thing);
                else if (job == SummerSchool.SettleJob) SummerSchool.Settled(pawn);
                else if (job == WardensManhunt.DressJob) WardensManhunt.Dressed(pawn, target.Thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: work done", e);
            }
        }

        /// <summary>One of theirs sent to work at a target for so long (one of round 4's jobs), at an errand's pace; false when it couldn't start.</summary>
        public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks)
        {
            if (p == null || job == null || !target.IsValid || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.CurJobDef == job) return false;
            if (!p.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
            try
            {
                var j = JobMaker.MakeJob(job, target);
                j.count = Mathf.Max(60, ticks);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return p.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: a job", e);
                return false;
            }
        }

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: " + what, e);
            }
        }
    }

    /// <summary>How one of round 4's jobs looks while it is done: "water" (splashes), "dust", "fire" (a torch's glow and smoke), "sparks", "quiet".</summary>
    public class PEv4Work : DefModExtension
    {
        public string mark = "dust";
    }

    /// <summary>
    /// One of round 4's jobs (TargetA a thing or a cell): up to it, a while at it with a bar and its mark (PEv4Work), then
    /// what it was for (PeopleEvents4.WorkDone): a fouled spot cleaned, the cargo lifted aboard, a lantern set on the water,
    /// a hive fired, a day's work on the hull; a fellow at the school's table has no end but the lesson's hours.
    /// </summary>
    public class JobDriver_PEv4Work : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private string Mark => job.def.GetModExtension<PEv4Work>()?.mark ?? "dust";

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (job.targetA.HasThing) this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return job.targetA.HasThing ? Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch) : Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PEv4Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.Position : job.targetA.Cell;
                pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "water": FleckMaker.WaterSplash(at.ToVector3Shifted(), map, 1.2f, 0.4f); break;
                    case "fire":
                        FleckMaker.ThrowFireGlow(at.ToVector3Shifted(), map, 0.6f);
                        FleckMaker.ThrowSmoke(at.ToVector3Shifted(), map, 0.6f);
                        break;
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    case "quiet": break;
                    default: FleckMaker.ThrowDustPuff(at, map, 0.5f); break;
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PEv4WorkDone");
            done.initAction = () => PeopleEvents4.WorkDone(pawn, job.def, job.targetA);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
