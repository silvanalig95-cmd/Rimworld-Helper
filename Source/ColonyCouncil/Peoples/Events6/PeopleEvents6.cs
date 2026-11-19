using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The peoples' own events, round 6 (docs/peoples-events-proposals.md, items 48, 49, 50, 52, 53, 54, 55, 56, 57 and 58; the
    // defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents6.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents6.xml): the
    // Stormglass Conclave's storm-walkers who bring the rain in a drought, the psychic tide their watch reads ahead and the storm
    // tower they camp at; the Mossborn's thorn-guards at a fire in the canopy, the seeds of the old wood and their camp in the ash
    // after a fire or a blight; the Ashen Banner's contract on the colony's enemy, the old soldier who settles, the contract the
    // other side made with them and a company camped a night on its march. Each comes only from a people of its calling at peace
    // with the colony, takes the form of the colony's age and theirs, and plays out on the map, built on rounds 1 to 5's shapes:
    // the visitor at the gate, the visitor who works on a colonist, the season away, the camp kind with ways of its own, the
    // Conclave's watch and rod, the Mossborn's planting and ledger, the calls to arms' fight on the game's map, the game's
    // incident queue as the watch and the relay mast hold things back, a band against a camp, the events' visits ("pev:...",
    // dispatched through rounds 1 to 5).

    /// <summary>Round 6's visits, by kind, its watch on the homes, its jobs' ends, and what its events share.</summary>
    public static class PeopleEvents6
    {
        public static int Now => Find.TickManager.TicksGame;

        /// <summary>The Banner's old soldiers kept on record at most (the dead and gone let go first).</summary>
        public const int MaxVeterans = 6;

        public static JobDef RodJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Rod");
        public static JobDef KeepJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_KeepNight");
        public static JobDef RodDownJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_RodDown");
        public static JobDef TowerWatchJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_TowerWatch");
        public static JobDef FirefightJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Firefight");
        public static JobDef CutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_CutBurned");
        public static JobDef SaplingJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Sapling");
        public static JobDef SingJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Sing");
        public static JobDef DrillJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Drill");

        /// <summary>An hour of one of round 6's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case DroughtBroken.Kind: return DroughtBroken.Hour(v, now);
                case TideRead.RunnerKind: return TideRead.RunnerHour(v, now);
                case CanopyFire.Kind: return CanopyFire.Hour(v, now);
            }
            return false;
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 to 5's): a dry spell, a wildfire and what it left, the
        /// tides held back, the Banner's contracts, its old soldiers' musters and the raids it was hired for.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the dry spell", () => DroughtBroken.Watch(map, now));
                Safe("the fire in the canopy", () => CanopyFire.Watch(map, now));
                Safe("after the fire", () => AfterFire.Watch(map, now));
            }
            Safe("the tides", () => TideRead.Watch(now));
            Safe("the contracts", () => BannerContract.Watch(now));
            Safe("the musters", () => OldSoldier.Watch(now));
            Safe("the other side's contracts", () => OtherSideContract.Watch(now));
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
            StormTower.Forget();
        }

        /// <summary>One of round 6's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == RodJob) DroughtBroken.RodSet(pawn, target.Cell);
                else if (job == RodDownJob) DroughtBroken.RodDown(pawn, target.Thing);
                else if (job == CutJob) AfterFire.Cut(pawn, target.Thing);
                else if (job == SaplingJob) AfterFire.Planted(pawn, target.Cell);
                else if (job == SingJob) AfterFire.Sang(pawn);
                else if (job == DrillJob) CompanyMarch.Drilled(pawn);
                else if (job == TowerWatchJob) StormTower.Watched(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: work done", e);
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
                CouncilLog.ErrorOnce("peoples' events 6: a job", e);
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
                CouncilLog.ErrorOnce("peoples' events 6: " + what, e);
            }
        }

        // ================= Shared: the high ground, the rods, the fires =================

        /// <summary>
        /// The colony's high ground (the relay mast's own choice: between eight and forty cells from the colony's middle, the
        /// hillside by the rock first, rough ground next, the nearer the better); <paramref name="rocky"/> when rock stands by it.
        /// </summary>
        public static IntVec3 HighGround(Map map, out bool rocky)
        {
            rocky = false;
            var c = RelayMast.Site(map);
            if (!c.IsValid) return IntVec3.Invalid;
            int rock = 0;
            foreach (var n in GenRadial.RadialCellsAround(c, 4f, false))
                if (n.InBounds(map) && n.GetEdifice(map) is Building b && b.def.building != null && b.def.building.isNaturalRock && ++rock >= 4) break;
            rocky = rock >= 4;
            return c;
        }

        /// <summary>The words for that place ({PLACE}): the high ground by the rocks, or the open ground by the colony.</summary>
        public static string HighGroundWords(bool rocky) => (rocky ? "CC_PEv6_HighGroundRock" : "CC_PEv6_HighGroundOpen").Translate().Resolve();

        /// <summary>Open cells near a spot for so many rods, a few cells apart: standable, unroofed, nothing on them.</summary>
        public static List<IntVec3> RodCells(Map map, IntVec3 at, int n)
        {
            var list = new List<IntVec3>();
            if (map == null || !at.IsValid) return list;
            foreach (var c in GenRadial.RadialCellsAround(at, 6f, true))
            {
                if (list.Count >= n) break;
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetFirstBuilding(map) != null || c.GetFirstItem(map) != null
                    || c.GetPlant(map) is Plant p && p.def.plant.IsTree || c.GetTerrain(map).IsWater) continue;
                if (list.Any(o => o.DistanceToSquared(c) < PeopleEvents6Math.RodsApart * PeopleEvents6Math.RodsApart)) continue;
                list.Add(c);
            }
            return list;
        }

        /// <summary>One of the Conclave's rods set at a cell for a people (theirs, not the colony's): it takes the lightning near it as the colony's rod does.</summary>
        public static Thing Rod(Map map, IntVec3 c, Faction f)
        {
            var def = PeoplesE.RodDef;
            if (map == null || def == null || !c.InBounds(map) || !c.Standable(map) || c.GetFirstBuilding(map) != null) return null;
            try
            {
                var rod = ThingMaker.MakeThing(def);
                rod.SetFaction(f);
                GenSpawn.Spawn(rod, c, map);
                FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(), map);
                return rod;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: a rod set", e);
                return null;
            }
        }

        /// <summary>Their rod taken down and carried off.</summary>
        public static void Unrod(Thing rod)
        {
            if (rod == null || rod.Destroyed || !rod.Spawned) return;
            FleckMaker.ThrowDustPuff(rod.Position, rod.Map, 0.5f);
            rod.Destroy();
        }

        /// <summary>Every fire burning within reach of a spot (the game's own fires, on the ground or on something).</summary>
        public static List<Fire> Fires(Map map, IntVec3 at, float radius)
        {
            var list = new List<Fire>();
            if (map == null || !at.IsValid) return list;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Fire))
                if (t is Fire f && f.Spawned && !f.Destroyed && f.Position.InHorDistOf(at, radius) && !f.Position.Fogged(map))
                    list.Add(f);
            return list;
        }

        /// <summary>The nearest fire a pawn can get to within reach of a spot, or null.</summary>
        public static Fire NearestFire(Map map, Pawn p, IntVec3 at, float radius)
        {
            if (map == null || p == null) return null;
            foreach (var f in Fires(map, at, radius).OrderBy(f => f.Position.DistanceToSquared(p.Position)).Take(6))
                if (p.CanReach(f, PathEndMode.Touch, Danger.Deadly))
                    return f;
            return null;
        }

        /// <summary>Those of theirs not already at it sent to beat out the fires within reach of a spot (the game's own beating).</summary>
        public static void BeatFires(IEnumerable<Pawn> pawns, IntVec3 at, float radius)
        {
            var job = FirefightJob;
            if (job == null) return;
            foreach (var p in pawns)
            {
                if (p == null || !p.Spawned || p.Dead || p.Downed || p.InMentalState || p.CurJobDef == job) continue;
                if (NearestFire(p.Map, p, at, radius) == null) continue;
                try
                {
                    var j = JobMaker.MakeJob(job, at);
                    j.count = Mathf.RoundToInt(radius);
                    j.locomotionUrgency = LocomotionUrgency.Jog;
                    p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: beating out a fire", e);
                }
            }
        }

        /// <summary>A people at peace with the colony who may be paid or thanked (not the colony, not gone).</summary>
        public static bool Thanks(Faction f) => f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);
    }

    /// <summary>How one of round 6's jobs looks while it is done: "dust", "sparks", "sow", "glass" (sparks and a pale glow), "drill", "chop", "quiet".</summary>
    public class PEv6Work : DefModExtension
    {
        public string mark = "dust";
    }

    /// <summary>
    /// One of round 6's jobs (TargetA a thing or a cell, and a queue after it): up to it, a while at it with a bar and its mark
    /// (PEv6Work), then what it was for (PeopleEvents6.WorkDone); the next of the queue, if any. Its place in the queue isn't
    /// kept with the game: a job loaded halfway starts its current target again.
    /// </summary>
    public class JobDriver_PEv6Work : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private string Mark => job.def.GetModExtension<PEv6Work>()?.mark ?? "dust";

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PEv6Next");
            next.initAction = () =>
            {
                // The target gone (a burned tree cut by someone else, a rod knocked down): on to the next of the queue.
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
            var work = ToilMaker.MakeToil("PEv6Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.Position : job.targetA.Cell;
                if (Mark != "drill") pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    // The Conclave's glass at work: a spark at the rod and the pale glow of the vials.
                    case "glass":
                        FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map);
                        FleckMaker.ThrowLightningGlow(at.ToVector3Shifted(), map, 0.6f);
                        break;
                    case "sow": FleckMaker.ThrowDustPuff(at, map, 0.6f); break;
                    case "chop":
                        FleckMaker.ThrowDustPuff(at, map, 0.7f);
                        if (pawn.IsHashIntervalTick(960, delta)) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash);
                        break;
                    // The Banner's drill: a step and a turn where they stand, dust at their feet.
                    case "drill":
                        pawn.Rotation = new Rot4((pawn.Rotation.AsInt + 1) % 4);
                        FleckMaker.ThrowDustPuff(pawn.Position, map, 0.5f);
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
            var done = ToilMaker.MakeToil("PEv6WorkDone");
            done.initAction = () =>
            {
                PeopleEvents6.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// One of theirs beating out the fires within reach of a spot (TargetA, its reach the job's count): the nearest fire, up to
    /// it, beaten with the game's own beating until it is out, then the next; done when none is left within reach. The game's
    /// own beating (Pawn_NativeVerbs.TryBeatFire), its burns the game's own.
    /// </summary>
    public class JobDriver_PEv6Firefight : JobDriver
    {
        private Fire fire;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var fight = ToilMaker.MakeToil("PEv6Firefight");
            fight.tickIntervalAction = delta =>
            {
                var map = pawn.Map;
                if (map == null) return;
                if (fire == null || fire.Destroyed || !fire.Spawned)
                {
                    fire = PeopleEvents6.NearestFire(map, pawn, job.targetA.Cell, Mathf.Max(8, job.count));
                    if (fire == null)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                }
                if (pawn.Position.AdjacentTo8WayOrInside(fire.Position))
                {
                    if (pawn.pather.Moving) pawn.pather.StopDead();
                    pawn.rotationTracker.FaceCell(fire.Position);
                    pawn.natives?.TryBeatFire(fire);
                    if (fire.Destroyed) pawn.records?.Increment(RecordDefOf.FiresExtinguished);
                }
                else if (!pawn.pather.Moving || pawn.pather.Destination.Cell != fire.Position) pawn.pather.StartPath(fire, PathEndMode.Touch);
            };
            fight.defaultCompleteMode = ToilCompleteMode.Never;
            fight.handlingFacing = true;
            yield return fight;
        }
    }
}
