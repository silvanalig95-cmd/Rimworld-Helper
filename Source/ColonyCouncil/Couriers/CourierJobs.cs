using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A courier's next job at the gate: first set down what they carry for the colony (and unload the pack animal),
    /// then, if they came for goods, fetch the next stack from the stores; otherwise nothing (the duty's wander keeps
    /// them by the gate). Never anything but the trip's own goods.
    /// </summary>
    public class JobGiver_CourierWork : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                return Next(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier job", e);
                return null;
            }
        }

        private static Job Next(Pawn pawn)
        {
            if (pawn == null || pawn.Downed || !pawn.RaceProps.Humanlike) return null;
            var t = CourierTracker.Instance?.TripOf(pawn);
            if (t == null || t.state != CourierState.Working || t.settled) return null;
            var map = pawn.Map;
            // Someone to take away first (they pay once they have them): the first of them goes for them.
            if (t.FetchPending)
            {
                var x = t.fetch;
                // Not while they're down or out of their mind: they wait (and in the end it's done the old way).
                if (t.Bearers.FirstOrDefault() != pawn || !CourierTracker.FetchOk(t) || x.Downed || x.InMentalState
                    || !pawn.CanReach(x, PathEndMode.Touch, Danger.Some)) return null;
                var fetch = JobMaker.MakeJob(CourierDefOf.Council_CourierFetch, x);
                fetch.locomotionUrgency = LocomotionUrgency.Walk;
                return fetch;
            }
            if (t.toUnload.Contains(pawn))
                return Drop(new LocalTargetInfo(t.spot));
            // The pack animal's load: the first of them still about unloads it, wherever it stands.
            var beast = t.animal;
            if (beast != null && t.toUnload.Contains(beast) && beast.Spawned && !beast.Dead && beast.Map == map && t.Bearers.FirstOrDefault() == pawn
                && pawn.CanReach(beast, PathEndMode.Touch, Danger.Some))
                return Drop(new LocalTargetInfo(beast));
            if (t.Takes && !t.StillCarrying)
            {
                int left = t.LeftToTake - InHand(t, pawn);
                if (left <= 0) return null;
                var stack = Stack(t, pawn);
                if (stack == null) return null;
                int n = t.things.Contains(stack) ? stack.stackCount : Mathf.Min(left, stack.stackCount);
                var job = JobMaker.MakeJob(CourierDefOf.Council_CourierLoad, stack);
                job.count = n;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                return job;
            }
            return null;
        }

        private static Job Drop(LocalTargetInfo at)
        {
            var job = JobMaker.MakeJob(CourierDefOf.Council_CourierDrop, at);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }

        /// <summary>What the others of the trip are on their way to take.</summary>
        private static int InHand(CourierTrip t, Pawn except)
        {
            int n = 0;
            foreach (var p in t.Bearers)
                if (p != except && p.CurJobDef == CourierDefOf.Council_CourierLoad && p.CurJob != null)
                    n += p.CurJob.count;
            return n;
        }

        /// <summary>
        /// The next stack to take: the trip's particular things, else the goods the trip names from the stores (stored
        /// stacks before loose ones, the nearest to the gate first); only what they may reserve and reach, and nothing
        /// the colony has forbidden.
        /// </summary>
        public static Thing Stack(CourierTrip t, Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null) return null;
            bool particular = CourierStock.Particular(t);
            var cands = particular ? CourierStock.Of(t, map) : CourierStock.Of(t, map).Where(x => x != null && x.Spawned && !x.IsForbidden(Faction.OfPlayer));
            var spot = t.spot;
            foreach (var x in cands.OrderByDescending(x => x.IsInAnyStorage()).ThenBy(x => x.Position.DistanceToSquared(spot)).Take(40))
            {
                if (!pawn.CanReserve(x, 1, -1, null, false)) continue;
                if (!pawn.CanReach(x, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return x;
            }
            return null;
        }
    }

    /// <summary>A courier walks to the gate (or to their pack animal) and sets down what is carried for the colony.</summary>
    public class JobDriver_CourierDrop : JobDriver
    {
        private CourierTrip Trip => CourierTracker.Instance?.TripOf(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            job.targetA.Thing is Pawn ? "CC_Courier_Report_Unload".Translate().Resolve() : "CC_Courier_Report_Drop".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Trip == null || Trip.settled);
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var wait = Toils_General.Wait(90, TargetIndex.None);
            wait.handlingFacing = true;
            wait.tickIntervalAction = _ =>
            {
                if (job.targetA.HasThing) pawn.rotationTracker.FaceTarget(job.targetA);
            };
            yield return wait;
            var drop = ToilMaker.MakeToil("CourierSetDown");
            drop.initAction = () =>
            {
                try
                {
                    var t = Trip;
                    var tracker = CourierTracker.Instance;
                    if (t == null || tracker == null) return;
                    var from = job.targetA.Thing as Pawn ?? pawn;
                    var at = job.targetA.HasThing ? from.Position : job.targetA.Cell;
                    if (!at.IsValid) at = pawn.Position;
                    tracker.Unload(t, pawn, from, at);
                    // Their own pack and the animal's, when it waits beside them.
                    if (from == pawn && t.animal != null && t.toUnload.Contains(t.animal) && t.animal.Spawned && t.animal.Map == pawn.Map
                        && t.animal.Position.InHorDistOf(pawn.Position, 8f))
                        tracker.Unload(t, null, t.animal, at);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier set down", e);
                }
            };
            drop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return drop;
        }
    }

    /// <summary>A courier walks to the one they came for (a prisoner sold to them, an animal given back) and takes them along.</summary>
    public class JobDriver_CourierFetch : JobDriver
    {
        private CourierTrip Trip => CourierTracker.Instance?.TripOf(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var x = job.targetA.Thing as Pawn;
            return x != null ? "CC_Courier_Report_Fetch".Translate(x.LabelShort).Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Trip == null || Trip.settled || !Trip.FetchPending);
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var wait = Toils_General.Wait(120, TargetIndex.A);
            yield return wait;
            var take = ToilMaker.MakeToil("CourierFetch");
            take.initAction = () =>
            {
                try
                {
                    CourierTracker.Instance?.Fetched(Trip, pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier fetch job", e);
                }
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
        }
    }

    /// <summary>A courier walks to a stack in the colony's stores and takes some into their pack (job.count).</summary>
    public class JobDriver_CourierLoad : JobDriver
    {
        private CourierTrip Trip => CourierTracker.Instance?.TripOf(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, job.count, null, errorOnFailed);

        public override string GetReport()
        {
            var thing = job.targetA.Thing;
            return thing != null ? "CC_Courier_Report_Load".Translate(thing.LabelNoCount).Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Trip == null || Trip.settled || Trip.state != CourierState.Working);
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_General.Wait(45, TargetIndex.A);
            var take = ToilMaker.MakeToil("CourierTake");
            take.initAction = () =>
            {
                try
                {
                    var t = Trip;
                    var thing = job.targetA.Thing;
                    if (t == null || thing == null || !thing.Spawned || pawn.inventory == null) return;
                    bool particular = t.things.Contains(thing);
                    int n = particular ? thing.stackCount : Mathf.Min(job.count, thing.stackCount, t.LeftToTake);
                    if (n <= 0) return;
                    var part = thing.SplitOff(n);
                    if (!pawn.inventory.innerContainer.TryAdd(part, true))
                    {
                        GenPlace.TryPlaceThing(part, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                        return;
                    }
                    if (particular) t.things.Remove(thing);
                    CourierTracker.Instance?.Notify_Loaded(t, pawn, n);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier take", e);
                }
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
        }
    }
}
