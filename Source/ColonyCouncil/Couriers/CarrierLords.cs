using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The walk in, the wait and the way out of Rimfolk's carriers (couriers and porters, a go-between under a white flag:
    /// <see cref="LordJob_Courier"/>; the post rider: <see cref="LordJob_PostRider"/>). The player (10 November 2026): "the
    /// couriers that bring letters and material take an awkward routine around my base before delivering the message a bit
    /// away from the base ... for the message they unmount and then they walk off map without remounting". Before, they
    /// travelled by the game's own duty, which sends each of them to a random cell within six of the spot and has them stand
    /// and wander in turn until the slowest is there; at the spot they wandered round it; and they left by the game's exit
    /// toil, which cuts every job short, the mount's "Mounted" job too, setting a rider down. Now they walk straight to their
    /// own place beside the spot and wait there (<see cref="JobGiver_CarrierGoTo"/>, <see cref="JobGiver_CarrierWait"/>),
    /// and on the way out a rider gets back on their beast first, which stands for them (<see cref="LordToil_CarrierLeave"/>,
    /// <see cref="JobGiver_CarrierRemount"/>, <see cref="JobGiver_CarrierMountWait"/>), then they leave by the nearest way.
    /// </summary>
    public static class CarrierMounts
    {
        /// <summary>The beast a carrier rode in on (a courier trip's, paired with its person; the post rider's), or null.</summary>
        public static Pawn MountOf(Pawn rider)
        {
            if (rider?.RaceProps == null || !rider.RaceProps.Humanlike) return null;
            var lord = rider.GetLord();
            if (lord?.LordJob is LordJob_Courier) return CourierTracker.Instance?.TripOf(rider)?.MountOf(rider);
            if (lord?.LordJob is LordJob_PostRider) return lord.ownedPawns.FirstOrDefault(p => p != null && p.RaceProps.Animal);
            return null;
        }

        /// <summary>The carrier a beast came in carrying (the other way round), or null.</summary>
        public static Pawn RiderOf(Pawn animal)
        {
            if (animal?.RaceProps == null || !animal.RaceProps.Animal) return null;
            var lord = animal.GetLord();
            if (lord?.LordJob is LordJob_Courier) return CourierTracker.Instance?.TripOf(animal)?.RiderOf(animal);
            if (lord?.LordJob is LordJob_PostRider) return lord.ownedPawns.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);
            return null;
        }

        /// <summary>
        /// What the rider does about their mount now (CarrierMath.Remount): both of the same group and fit (up, in their right
        /// mind, the beast not held by someone else's rope), the beast within reach, an hour at most from when they set off.
        /// </summary>
        public static RemountStep Step(Pawn rider, Pawn mount)
        {
            if (rider == null || mount == null) return RemountStep.None;
            bool riding = GiddyUpMounts.Riding(rider, mount);
            var lord = rider.GetLord();
            bool ok = rider.Spawned && !rider.Dead && !rider.Downed && !rider.InMentalState
                      && mount.Spawned && !mount.Dead && !mount.Downed && !mount.InMentalState && mount.Map == rider.Map && mount.Faction == rider.Faction
                      && lord != null && mount.GetLord() == lord && !(mount.roping?.IsRopedByPawn ?? false);
            bool reach = ok && rider.CanReach(mount, PathEndMode.Touch, Danger.Some);
            float distance = ok ? rider.Position.DistanceTo(mount.Position) : float.MaxValue;
            return CarrierMath.Remount(GiddyUpMounts.On, true, riding, ok, reach, distance, lord?.ticksInToil ?? 0);
        }
    }

    /// <summary>
    /// The walk in: everyone of the group goes straight to their own place beside the spot (the mailbag, the gate) and waits
    /// there until the others are in (the game's travel toil checks that all are within ten cells).
    /// </summary>
    public class LordToil_CarrierTravel : LordToil_Travel
    {
        public LordToil_CarrierTravel(IntVec3 dest) : base(dest)
        {
        }

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
            {
                if (p?.mindState == null) continue;
                p.mindState.duty = new PawnDuty(CourierDefOf.Rimfolk_CarrierTravel, FlagLoc) { locomotion = Pace.Errand };
            }
        }
    }

    /// <summary>
    /// The way out: as the game's exit toil, by the nearest way off the map, at an errand's pace; but a rider gets back in the
    /// saddle first (the beast stands for them), and only the jobs of the business are cut short: never a beast's carrying
    /// its rider, nor a rider getting on.
    /// </summary>
    public class LordToil_CarrierLeave : LordToil_ExitMap
    {
        public LordToil_CarrierLeave() : base(Pace.Errand, false, false)
        {
        }

        public override DutyDef ExitDuty => CourierDefOf.Rimfolk_CarrierLeave;

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
            foreach (var p in lord.ownedPawns.ToList())
            {
                if (p?.jobs?.curJob == null || Keep(p)) continue;
                try
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("carriers: setting off", e);
                }
            }
        }

        /// <summary>A job not cut short as they set off: a beast carrying its rider, a rider getting on or walking to their beast, a walk off the map already.</summary>
        private static bool Keep(Pawn p)
        {
            var job = p.CurJob;
            if (job == null) return true;
            if (GiddyUpMounts.CarryingRider(p) || GiddyUpMounts.Mounting(p) || job.exitMapOnArrival) return true;
            return job.def == JobDefOf.Goto && job.targetA.Thing is Pawn;
        }
    }

    /// <summary>The walk in (Rimfolk_CarrierTravel): straight to their own place beside the spot, then a wait there.</summary>
    public class JobGiver_CarrierGoTo : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                return CarrierJobs.ToPlace(pawn, 2500);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: the walk in", e);
                return null;
            }
        }
    }

    /// <summary>At the spot, with nothing to do this moment (Rimfolk_CarrierWait; the courier's duty after its work): back to their place, and a wait there.</summary>
    public class JobGiver_CarrierWait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                return CarrierJobs.ToPlace(pawn, 600);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: the wait", e);
                return null;
            }
        }
    }

    /// <summary>On the way out (Rimfolk_CarrierLeave): a rider not in the saddle gets on now, or walks over to their beast and gets on.</summary>
    public class JobGiver_CarrierRemount : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                var mount = CarrierMounts.MountOf(pawn);
                if (mount == null) return null;
                switch (CarrierMounts.Step(pawn, mount))
                {
                    case RemountStep.MountNow:
                        if (GiddyUpMounts.MountNow(pawn, mount)) return null;
                        return GiddyUpMounts.WalkAndMount(pawn, mount);
                    case RemountStep.WalkToMount:
                        var job = GiddyUpMounts.WalkAndMount(pawn, mount);
                        if (job != null) return job;
                        // No "Mount" job to be had: walk to it, and get on once beside it.
                        var go = JobMaker.MakeJob(JobDefOf.Goto, mount);
                        go.locomotionUrgency = Pace.Errand;
                        go.expiryInterval = 300;
                        go.checkOverrideOnExpire = true;
                        return go;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: back in the saddle", e);
            }
            return null;
        }
    }

    /// <summary>On the way out (Rimfolk_CarrierLeave): a beast whose rider is coming to get on stands where it is.</summary>
    public class JobGiver_CarrierMountWait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                var rider = CarrierMounts.RiderOf(pawn);
                if (rider == null || !CarrierMath.MountWaits(CarrierMounts.Step(rider, pawn))) return null;
                var job = JobMaker.MakeJob(JobDefOf.Wait);
                job.expiryInterval = 90;
                job.checkOverrideOnExpire = true;
                return job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: a beast waiting", e);
                return null;
            }
        }
    }

    /// <summary>The jobs of a carrier's walk to their place and their wait.</summary>
    public static class CarrierJobs
    {
        /// <summary>The place a pawn of a carriers' group stands at the spot (its duty's focus): the <c>i</c>th place round it.</summary>
        public static IntVec3 PlaceOf(Pawn pawn, IntVec3 spot)
        {
            var lord = pawn?.GetLord();
            int i = lord != null ? Math.Max(0, lord.ownedPawns.IndexOf(pawn)) : 0;
            return CarrierSpots.StandCell(pawn?.Map, spot, i, pawn);
        }

        /// <summary>Straight to their place (an errand's pace); there, a wait facing the spot.</summary>
        public static Job ToPlace(Pawn pawn, int expiry)
        {
            var duty = pawn?.mindState?.duty;
            if (duty == null || pawn.Map == null || !duty.focus.IsValid) return null;
            var spot = duty.focus.Cell;
            var place = PlaceOf(pawn, spot);
            if (!place.IsValid) return null;
            if (pawn.Position == place || pawn.Position.AdjacentTo8WayOrInside(place) && pawn.Position.InHorDistOf(spot, 2.9f)) return Wait();
            if (!pawn.CanReach(place, PathEndMode.OnCell, Danger.Some))
            {
                // Shut out (a door locked meanwhile): they wait where they are, and the group's clock moves them on.
                if (!pawn.CanReach(spot, PathEndMode.ClosestTouch, Danger.Some)) return Wait();
                place = spot;
            }
            var go = JobMaker.MakeJob(JobDefOf.Goto, place);
            go.locomotionUrgency = Pace.Errand;
            go.expiryInterval = expiry;
            go.checkOverrideOnExpire = true;
            return go;
        }

        /// <summary>A short wait facing the spot (the game's wait faces the duty's focus), looked at again when it runs out.</summary>
        private static Job Wait()
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = Rand.Range(120, 240);
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }
}
