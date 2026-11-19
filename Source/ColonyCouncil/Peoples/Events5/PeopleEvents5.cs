using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    // The peoples' own events, round 5 (docs/peoples-events-proposals.md, items 35, 36, 37, 38, 39, 41, 42, 43, 44, 45 and 46;
    // the defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents5.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents5.xml):
    // the Quiet Bell's gravewardens when the colony's unburied dead rise, mourners of the raiders' people at the graves the
    // Bell dug, a grave-garden laid out among the colony's graves and a camp at the field of the fallen; the Circuit of
    // Tamers' bond freely given, an egg from the crater and a bout-ring by the colony while the fair runs; the Wheel's crew
    // mending by night after a raid, a piece made better at their cart, a wagon built at the gate and a yard that stops by
    // when its town moves near. Each comes only from a people of its calling at peace with the colony, takes the form of the
    // colony's age and theirs, and plays out on the map, built on rounds 1 to 4's shapes: the visitor at the gate, the
    // visitor who works on a colonist (here also on a beast), the camp kind with ways of its own, the hunt's assault lord, the
    // undertakers' graves, the friendly bout, the fixer's mending, the events' visits ("pev:...", dispatched through rounds 1
    // to 4).

    /// <summary>Round 5's visits, by kind, its watch on the homes, and its jobs' ends.</summary>
    public static class PeopleEvents5
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef StoneJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Stone");
        public static JobDef RoseJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Rose");
        public static JobDef PostJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Post");
        public static JobDef MendJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Mend");
        public static JobDef GatherJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Gather");
        public static JobDef FrameJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Frame");
        public static JobDef DigJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Dig");
        public static JobDef BuryJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Bury");
        public static JobDef RingJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Ring");

        /// <summary>An hour of one of round 5's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case FieldStirs.Kind: return FieldStirs.Hour(v, now);
                case OtherSideMourners.Kind: return OtherSideMourners.Hour(v, now);
                case GraveGarden.Kind: return GraveGarden.Hour(v, now);
                case MendTonight.Kind: return MendTonight.Hour(v, now);
                case WagonOfOurOwn.Kind: return WagonOfOurOwn.Hour(v, now);
            }
            // Round 6's (Peoples/Events6/): the storm-walkers' night, the tide's runner, the thorn-guards at a fire.
            return PeopleEvents6.Hour(v, comp, now);
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 and 4's): the dead lying out and any that rise, the
        /// mourners due, the grave-gardens' quiet ground and their funerals, the crater eggs and who tends them, a raid's
        /// night, the pieces at the Wheel's cart, the yards due.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the field's watch", () => FieldStirs.Watch(map, now));
                Safe("the grave-garden", () => GraveGarden.Watch(map, now));
                Safe("the night after a raid", () => MendTonight.Watch(map, now));
            }
            Safe("the mourners", () => OtherSideMourners.Watch(now));
            Safe("the crater eggs", () => CraterEgg.Watch(now));
            Safe("the pieces at the cart", () => MadeBetter.Watch(now));
            Safe("the yards", () => WheelStopsBy.Watch(now));
            // Round 6's watch (Peoples/Events6/): a dry spell, a fire and what it left, the tides held, the Banner's contracts and musters.
            Safe("round 6", () => PeopleEvents6.Hourly(now));
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
            FieldStirs.Forget();
            GraveGarden.Forget();
            CircuitRing.Forget();
        }

        /// <summary>One of round 5's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == StoneJob) GraveGarden.Laid(pawn, target.Cell);
                else if (job == RoseJob) GraveGarden.Planted(pawn, target.Cell);
                else if (job == PostJob) GraveGarden.Raised(pawn, target.Cell);
                else if (job == MendJob) MendTonight.Mended(pawn, target.Thing);
                else if (job == GatherJob) MendTonight.Gathered(pawn, target.Thing);
                else if (job == FrameJob) WagonOfOurOwn.Worked(pawn, target.Cell);
                else if (job == DigJob) FieldOfFallen.Dug(pawn, target.Cell);
                else if (job == RingJob) FieldOfFallen.Rang(pawn, target.Cell);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: work done", e);
            }
        }

        /// <summary>
        /// One of theirs sent to work at a target (and then the rest of a queue, if any) for so long each, at an errand's pace;
        /// false when it couldn't start.
        /// </summary>
        public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks, List<LocalTargetInfo> rest = null)
        {
            if (p == null || job == null || !target.IsValid || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.CurJobDef == job) return false;
            if (!p.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
            try
            {
                var j = JobMaker.MakeJob(job, target);
                j.count = Mathf.Max(60, ticks);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                if (!rest.NullOrEmpty()) j.targetQueueA = rest.ToList();
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return p.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: a job", e);
                return false;
            }
        }

        /// <summary>A job for one of a camp's people (its DayJob): the same work at a target, for so long.</summary>
        public static Job Make(JobDef job, LocalTargetInfo target, int ticks)
        {
            if (job == null || !target.IsValid) return null;
            var j = JobMaker.MakeJob(job, target);
            j.count = Mathf.Max(60, ticks);
            return j;
        }

        /// <summary>The bell rung once where it stands (the game's own small bell), heard there.</summary>
        public static void Bell(Map map, IntVec3 at)
        {
            if (map == null || !at.IsValid) return;
            try
            {
                SoundDefOf.TinyBell?.PlayOneShot(SoundInfo.InMap(new TargetInfo(at, map)));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the bell", e);
            }
        }

        /// <summary>
        /// The people a visit of the round is for: the one its text names (rounds 2 to 4 put it there), else the people of
        /// whoever came (round 5's and 6's visits keep their own words in the text).
        /// </summary>
        public static Faction PeopleOf(Visit v) =>
            PeoplesF.PeopleOf(v) ?? v?.group.Prepend(v.x).FirstOrDefault(p => p?.Faction != null && !p.Faction.IsPlayer)?.Faction;

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: " + what, e);
            }
        }

        /// <summary>The people's age tags, and the fire their age brings to what will not stay down.</summary>
        public static void AgeTags(HashSet<string> tags, Faction f) => PeopleEvents.AgeTags(tags, f);
    }

    /// <summary>How one of round 5's jobs looks while it is done: "dust", "dig" (earth turned), "sparks", "lamp" (sparks by lamplight), "quiet".</summary>
    public class PEv5Work : DefModExtension
    {
        public string mark = "dust";
    }

    /// <summary>
    /// One of round 5's jobs (TargetA a thing or a cell, and a queue after it): up to it, a while at it with a bar and its mark
    /// (PEv5Work), then what it was for (PeopleEvents5.WorkDone); the next of the queue, if any. Its place in the queue isn't
    /// kept with the game: a job loaded halfway starts its current target again.
    /// </summary>
    public class JobDriver_PEv5Work : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private string Mark => job.def.GetModExtension<PEv5Work>()?.mark ?? "dust";

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PEv5Next");
            next.initAction = () =>
            {
                // The target gone (a body buried by someone else, a wall knocked down): on to the next of the queue.
                while (!job.targetA.IsValid || job.targetA.HasThing && (job.targetA.Thing.Destroyed || !job.targetA.Thing.Spawned))
                {
                    if (job.targetQueueA.NullOrEmpty())
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    job.targetA = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                }
            };
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return next;
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PEv5Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.Position : job.targetA.Cell;
                pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    // Mending by night (the Wheel's crew): sparks, and the glow of the lamp they work by.
                    case "lamp":
                        FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map);
                        FleckMaker.ThrowFireGlow(pawn.DrawPos, map, 0.5f);
                        break;
                    case "dig":
                        FleckMaker.ThrowDustPuff(at, map, 0.7f);
                        if (pawn.IsHashIntervalTick(960, delta)) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Dirt);
                        break;
                    case "quiet": break;
                    default: FleckMaker.ThrowDustPuff(at, map, 0.5f); break;
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PEv5WorkDone");
            done.initAction = () =>
            {
                PeopleEvents5.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// One of the Bell's people carrying one of the dead (A) to a grave they dug (B) and laying it in (the field of the
    /// fallen's own; the undertakers' job of Visitors/CallerJobs.cs, for a camp's people).
    /// </summary>
    public class JobDriver_PEv5Bury : JobDriver
    {
        private bool laid;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            AddFinishAction(cond =>
            {
                if (!laid && pawn.carryTracker?.CarriedThing != null && pawn.Spawned) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var lay = ToilMaker.MakeToil("PEv5Bury");
            lay.defaultCompleteMode = ToilCompleteMode.Delay;
            lay.defaultDuration = 240;
            lay.WithProgressBarToilDelay(TargetIndex.B);
            yield return lay;
            yield return Toils_General.Do(() =>
            {
                FieldOfFallen.Buried(pawn, job.targetB.Thing as Building_Grave);
                laid = pawn.carryTracker?.CarriedThing == null;
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref laid, "laid");
        }
    }
}
