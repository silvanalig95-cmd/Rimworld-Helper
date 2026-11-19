using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The peoples' own events, round 7 (docs/peoples-events-proposals.md, items 60, 61, 62, 64, 65, 66, 67, 68, 69 and 70; the
    // defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents7.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents7.xml): the Kiln
    // Guild's master who comes to judge a masterwork the colony made, the journeyman of a wander-year who comes back a master a year
    // on, and three journeymen camped while they build a great work with the colony's stone or wood; the Signal Line's one call a
    // season on its masts, a mast raised at a friend's town and a line crew camped on the high ground; the Deepwell Union's call for
    // the colony's miner and doctor when one of its pits caves in, the oil or the deep seam its assayer finds under the colony, a
    // shift-captain who speaks at the colony's fire when its people are worn down, and the picket kept by the colony's road while a
    // strike lasts. Each comes only from a people of its calling at peace with the colony, takes the form of the colony's age and
    // theirs, and plays out on the map, built on rounds 1 to 6's shapes: the visitor at the gate, the season away (a caravan of
    // one), the camp kind with ways of its own, the fine-piece hook and the wander-year's deed, the relay mast and the letters' road,
    // the union's drill and its strike, the council's petitions and decrees, a band against a camp, the events' visits ("pev:...",
    // dispatched through rounds 1 to 6).

    /// <summary>Round 7's visits, by kind, its watch, its jobs' ends, and what its events share.</summary>
    public static class PeopleEvents7
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef JudgeJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Judge");
        public static JobDef StandByJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_StandBy");
        public static JobDef CarveJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Carve");
        public static JobDef TearJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_TearDown");
        public static JobDef CallJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Call");
        public static JobDef StringJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_String");
        public static JobDef ClimbJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Climb");
        public static JobDef ListenJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Listen");
        public static JobDef SinkJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Sink");
        public static JobDef SingJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv7_Sing");

        /// <summary>An hour of one of round 7's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case MastersMark.Kind: return MastersMark.Hour(v, now);
                case VoiceForColony.Kind: return VoiceForColony.Hour(v, now);
                case OilInGround.Kind: return OilInGround.Hour(v, now);
            }
            // Round 8's (Peoples/Events8/): the clerk at the vote, the medal, the officers on the gravship, the sleeper's casket, the Works' gene work.
            return PeopleEvents8.Hour(v, comp, now);
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 to 6's): a masterwork the guild may judge, a journeyman due
        /// back, a mast at a friend's town raised, a well's share due, the people worn down and their petition, a strike's picket.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the organizer's evening", () => OrganizersEvening.Watch(map, now));
            }
            Safe("the masterworks", () => MastersMark.Watch(now));
            Safe("the journeymen due back", () => ComeBackBetter.Watch(now));
            Safe("the masts at friends' towns", () => TownMast.Watch(now));
            Safe("the wells' shares", () => OilInGround.WatchShares(now));
            Safe("the shifts at a pit", () => PitCaveIn.Shifts(now));
            Safe("the petitions", () => OrganizersEvening.Petitions(now));
            Safe("the pickets", () => Picket.Watch(now));
            // Round 8's watch (Peoples/Events8/): elections, deeds, famines, the Fleet's sleepers woken.
            Safe("round 8", () => PeopleEvents8.Hourly(now));
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
        }

        /// <summary>One of round 7's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == JudgeJob) MastersMark.Judged(pawn);
                else if (job == StandByJob) MastersMark.StoodBy(pawn);
                else if (job == CarveJob) WorkThatStays.Carved(pawn, target.Cell);
                else if (job == TearJob) WorkThatStays.TornDown(pawn, target.Cell);
                else if (job == CallJob) VoiceForColony.Called(pawn);
                else if (job == StringJob) LineCrew.Strung(pawn, target);
                else if (job == ClimbJob) LineCrew.Climbed(pawn, target.Thing);
                else if (job == ListenJob) LineCrew.Listened(pawn);
                else if (job == SinkJob) OilInGround.Sunk(pawn, target.Cell);
                else if (job == SingJob) Picket.Sang(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: work done", e);
            }
        }

        /// <summary>
        /// One of theirs (or one of the colony's) sent to work at a target (and then the rest of a queue, if any) for so long each, at
        /// an errand's pace; false when it couldn't start.
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
                CouncilLog.ErrorOnce("peoples' events 7: a job", e);
                return false;
            }
        }

        /// <summary>A job for one of a camp's people (its DayJob or NightJob): the same work at a target, for so long.</summary>
        public static Job Make(JobDef job, LocalTargetInfo target, int ticks)
        {
            if (job == null || !target.IsValid) return null;
            var j = JobMaker.MakeJob(job, target);
            j.count = Mathf.Max(60, ticks);
            return j;
        }

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: " + what, e);
            }
        }

        /// <summary>A people at peace with the colony who may be paid or thanked (not the colony, not gone).</summary>
        public static bool Thanks(Faction f) => f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);

        /// <summary>Goodwill with a people at peace, for a reason.</summary>
        public static void Goodwill(Faction f, int amount, HistoryEventDef why)
        {
            if (Thanks(f) && amount != 0) WorldSituationWorker.Goodwill(f, amount, why);
        }

        /// <summary>
        /// The colony's letters to a notable go down the Signal Line's wire (World/NeighbourLetters.cs): a mast the Line raised at the
        /// notable's town (item 65), or a line crew camped at the writer's home with its mast up (item 66). The colony's own relay
        /// mast is the release F rule beside it; together they are still half the days, never a quarter.
        /// </summary>
        public static bool Relays(Map home, Notable n)
        {
            try
            {
                return TownMast.Relays(n) || LineCrew.Relays(home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the letters' wire", e);
                return false;
            }
        }

        /// <summary>A colonist's mood memory of round 7's own, if the def is there.</summary>
        public static void Thought(Pawn p, string def)
        {
            var t = DefDatabase<ThoughtDef>.GetNamedSilentFail(def);
            if (t != null && p != null && !p.Dead && p.needs?.mood != null) CouncilGameComponent.GiveThought(p, t, null);
        }
    }

    /// <summary>How one of round 7's jobs looks while it is done: "dust", "chisel" (stone chips), "sparks", "wire" (sparks aloft), "drill" (earth turned), "look", "quiet".</summary>
    public class PEv7Work : DefModExtension
    {
        public string mark = "dust";
    }

    /// <summary>
    /// One of round 7's jobs (TargetA a thing or a cell, and a queue after it): up to it, a while at it with a bar and its mark
    /// (PEv7Work), then what it was for (PeopleEvents7.WorkDone); the next of the queue, if any. Its place in the queue isn't kept
    /// with the game: a job loaded halfway starts its current target again.
    /// </summary>
    public class JobDriver_PEv7Work : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private string Mark => job.def.GetModExtension<PEv7Work>()?.mark ?? "dust";

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PEv7Next");
            next.initAction = () =>
            {
                // The target gone (a piece carried off, a mast knocked down): on to the next of the queue.
                while (!job.targetA.IsValid || job.targetA.HasThing && (job.targetA.Thing.Destroyed || !job.targetA.Thing.SpawnedOrAnyParentSpawned))
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
            var work = ToilMaker.MakeToil("PEv7Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.PositionHeld : job.targetA.Cell;
                pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    // The Line's work on the wire: sparks at the mast and a glint of the copper.
                    case "wire":
                        FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map);
                        if (pawn.IsHashIntervalTick(720, delta)) FleckMaker.ThrowLightningGlow(at.ToVector3Shifted(), map, 0.3f);
                        break;
                    // The guild's chisels: dust, and chips of the stone underfoot now and then.
                    case "chisel":
                        FleckMaker.ThrowDustPuff(at, map, 0.7f);
                        if (pawn.IsHashIntervalTick(1200, delta)) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_RubbleRock);
                        break;
                    case "drill":
                        FleckMaker.ThrowDustPuff(at, map, 0.9f);
                        if (pawn.IsHashIntervalTick(960, delta)) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Dirt);
                        break;
                    case "look":
                    case "quiet": break;
                    default: FleckMaker.ThrowDustPuff(at, map, 0.5f); break;
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PEv7WorkDone");
            done.initAction = () =>
            {
                PeopleEvents7.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
