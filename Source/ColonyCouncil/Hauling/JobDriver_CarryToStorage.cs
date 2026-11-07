using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Takes what Rimfolk put in a pawn's inventory to storage, one stack after the other, the game's own way (walk to
    /// the storage cell with it still in the inventory, take it out there, place it with the game's hauling steps, so
    /// stockpiles, shelves and storage mods work as usual). First it picks up the things on the way it was given (the
    /// job's first target queue), then the stack that didn't fit if it still lies close by, carried in the hands, then
    /// the inventory. It delivers only what is owed and never what the pawn keeps; a stack no storage takes is set down
    /// where the pawn stands for the haulers. Pick Up And Haul's unloading, when that mod has stacks to unload, comes next.
    /// From the player's Harvest and Haul (its delivery job), with the things on the way and the kept amounts added.
    /// </summary>
    public class JobDriver_CarryToStorage : JobDriver
    {
        private const TargetIndex ItemInd = TargetIndex.A;
        private const TargetIndex CellInd = TargetIndex.B;
        private const int TakeOutTicks = 10;

        private int countToDeliver = -1;
        private bool carryingLeftover;
        private bool collecting = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref countToDeliver, "countToDeliver", -1);
            Scribe_Values.Look(ref carryingLeftover, "carryingLeftover", false);
            Scribe_Values.Look(ref collecting, "collecting", true);
        }

        // Things and storage cells are reserved one at a time as the job goes.
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition =>
            {
                if (condition == JobCondition.Incompletable || condition == JobCondition.Errored) Carrying.Notify_DeliveryFailed(pawn);
            });

            Toil findNext = ToilMaker.MakeToil("CarryToStorage_FindNext");
            Toil gotoLeftover = Toils_Goto.GotoThing(ItemInd, PathEndMode.ClosestTouch);

            // --- Things on the way ---
            Toil collectNext = ToilMaker.MakeToil("CarryToStorage_NextOnTheWay");
            Toil gotoLoose = ToilMaker.MakeToil("CarryToStorage_GotoOnTheWay");
            collectNext.initAction = () =>
            {
                if (!collecting || !NextOnTheWay())
                {
                    collecting = false;
                    JumpToToil(findNext);
                }
            };
            collectNext.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return collectNext;

            gotoLoose.initAction = () => pawn.pather.StartPath(job.GetTarget(ItemInd), PathEndMode.ClosestTouch);
            gotoLoose.tickAction = () =>
            {
                Thing t = job.GetTarget(ItemInd).Thing;
                if (t == null || !t.Spawned || t.Map != pawn.Map || t.IsForbidden(pawn)) JumpToToil(collectNext);
            };
            gotoLoose.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return gotoLoose;

            Toil takeLoose = ToilMaker.MakeToil("CarryToStorage_TakeOnTheWay");
            takeLoose.initAction = () =>
            {
                TakeOnTheWay();
                JumpToToil(collectNext);
            };
            takeLoose.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return takeLoose;

            // --- What to take next ---
            findNext.initAction = () =>
            {
                switch (TrySetupNextDelivery())
                {
                    case NextStep.Done:
                        Finish();
                        break;
                    case NextStep.Leftover:
                        JumpToToil(gotoLeftover);
                        break;
                }
            };
            findNext.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findNext;

            // --- A stack from the inventory ---
            Toil gotoStorage = Toils_Goto.GotoCell(CellInd, PathEndMode.ClosestTouch);
            gotoStorage.FailOn(() => !ItemStillInInventory());
            yield return gotoStorage;

            Toil takeOutDelay = Toils_General.Wait(TakeOutTicks, CellInd);
            takeOutDelay.FailOn(() => !ItemStillInInventory());
            yield return takeOutDelay;

            Toil takeOut = ToilMaker.MakeToil("CarryToStorage_TakeOut");
            takeOut.initAction = () =>
            {
                Thing thing = job.GetTarget(ItemInd).Thing;
                if (!ItemStillInInventory())
                {
                    JumpToToil(findNext);
                    return;
                }
                ThingDef def = thing.def;
                int count = Mathf.Min(countToDeliver, thing.stackCount, pawn.carryTracker.AvailableStackSpace(def));
                int moved = count > 0 ? pawn.inventory.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer, count, canMergeWithExistingStacks: false) : 0;
                Thing carried = pawn.carryTracker.CarriedThing;
                if (moved <= 0 || carried == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                HaulBook.Get?.LoadOf(pawn)?.Remove(def, moved);
                carried.SetForbidden(false, warnOnFail: false);
                job.SetTarget(ItemInd, carried);
                job.count = carried.stackCount;
            };
            takeOut.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return takeOut;

            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(CellInd);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(CellInd, carryToCell, storageMode: true);
            yield return Toils_Jump.Jump(findNext);

            // --- The stack that didn't fit, carried in the hands ---
            gotoLeftover.JumpIfDespawnedOrNullOrForbidden(ItemInd, findNext);
            yield return gotoLeftover;
            yield return Toils_Haul.StartCarryThing(ItemInd, reserve: false);
            Toil carryLeftover = Toils_Haul.CarryHauledThingToCell(CellInd);
            yield return carryLeftover;
            yield return Toils_Haul.PlaceHauledThingInCell(CellInd, carryLeftover, storageMode: true);
            yield return Toils_Jump.Jump(findNext);
        }

        private enum NextStep
        {
            Done,
            Inventory,
            Leftover
        }

        private bool ItemStillInInventory()
        {
            Thing thing = job.GetTarget(ItemInd).Thing;
            return thing != null && !thing.Destroyed && pawn.inventory != null && pawn.inventory.innerContainer.Contains(thing);
        }

        /// <summary>The nearest of the things on the way still to pick up, reserved; false when there are none left.</summary>
        private bool NextOnTheWay()
        {
            var queue = job.targetQueueA;
            Map map = pawn.Map;
            while (queue != null && queue.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < queue.Count; i++)
                    if (queue[i].Cell.DistanceToSquared(pawn.Position) < queue[best].Cell.DistanceToSquared(pawn.Position))
                        best = i;
                Thing t = queue[best].Thing;
                queue.RemoveAt(best);
                if (t == null || !t.Spawned || t.Map != map || t.IsForbidden(pawn) || t.IsInValidStorage()) continue;
                if (Carrying.CountThatFits(pawn, t) <= 0) continue;
                if (!pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some) || !pawn.Reserve(t, job, errorOnFailed: false)) continue;
                job.SetTarget(ItemInd, t);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes the thing on the way into the inventory. A kind the pawn carried none of goes to Pick Up And Haul's
        /// unloading when that mod is loaded; otherwise it is owed to storage with the rest.
        /// </summary>
        private void TakeOnTheWay()
        {
            Thing t = job.GetTarget(ItemInd).Thing;
            if (t == null || !t.Spawned) return;
            ThingDef def = t.def;
            int before = Carrying.InInventory(pawn, def);
            int taken = Carrying.TakeToInventory(pawn, t, t.stackCount);
            if (!t.Destroyed && t.Spawned) pawn.Map.reservationManager.Release(t, pawn, job);
            if (taken <= 0) return;
            if (before == 0 && HaulLinks.PuahFor(pawn) && HaulLinks.HandToPuah(pawn, Carrying.FirstInInventory(pawn, def)))
            {
                Carrying.Debug($"{pawn.LabelShort} picked up {taken}x {def.label} on the way, for Pick Up And Haul's unloading.");
                return;
            }
            var load = HaulBook.Get?.LoadFor(pawn);
            if (load == null) return;
            load.Add(def, taken);
            load.now = true;
            Carrying.Debug($"{pawn.LabelShort} picked up {taken}x {def.label} on the way.");
        }

        /// <summary>
        /// What to take next: the stack that didn't fit, then what is owed. A stack no storage takes is set down here for
        /// the haulers; what the pawn keeps stays in its inventory.
        /// </summary>
        private NextStep TrySetupNextDelivery()
        {
            var load = HaulBook.Get?.LoadOf(pawn);
            Map map = pawn.Map;
            if (map == null || pawn.inventory == null) return NextStep.Done;
            map.reservationManager.ReleaseClaimedBy(pawn, job);
            if (load == null) return NextStep.Done;
            HandleStillCarriedThing(load);

            Thing leftover = load.leftover;
            load.leftover = null;
            if (TrySetupLeftover(leftover, map)) return NextStep.Leftover;

            load.Reconcile(pawn);
            var inventory = pawn.inventory.innerContainer;
            // Each turn sets up a delivery or takes a kind off the load, so this ends.
            for (int guard = 0; guard < 200 && !load.Empty; guard++)
            {
                Thing thing = null;
                for (int i = 0; i < inventory.Count; i++)
                    if (load.Owes(inventory[i].def))
                    {
                        thing = inventory[i];
                        break;
                    }
                if (thing == null) return NextStep.Done;
                ThingDef def = thing.def;
                int count = HaulMath.Deliverable(load.OwedCount(def), Carrying.InInventory(pawn, def), Carrying.Kept(pawn, def));
                if (count <= 0)
                {
                    // All that is left of it, the pawn keeps.
                    load.Remove(def, load.OwedCount(def));
                    continue;
                }
                count = Mathf.Min(count, thing.stackCount);
                int carryable = Mathf.Min(count, pawn.carryTracker.AvailableStackSpace(def));
                if (carryable > 0 && StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, map, StoragePriority.Unstored, pawn.Faction, out IntVec3 cell)
                    && pawn.Reserve(cell, job, errorOnFailed: false))
                {
                    job.SetTarget(ItemInd, thing);
                    job.SetTarget(CellInd, cell);
                    job.count = carryable;
                    countToDeliver = carryable;
                    carryingLeftover = false;
                    return NextStep.Inventory;
                }
                Carrying.Debug($"{pawn.LabelShort}: no storage for {thing.LabelCap}, setting {count} down here.");
                if (inventory.TryDrop(thing, pawn.Position, map, ThingPlaceMode.Near, count, out Thing dropped)) dropped?.SetForbidden(false, warnOnFail: false);
                load.Remove(def, count);
            }
            return NextStep.Done;
        }

        private bool TrySetupLeftover(Thing leftover, Map map)
        {
            if (leftover == null || leftover.Destroyed || !leftover.Spawned || leftover.Map != map) return false;
            if (pawn.carryTracker.CarriedThing != null || leftover.IsForbidden(pawn) || leftover.IsInValidStorage()) return false;
            if (!leftover.Position.InHorDistOf(pawn.Position, HaulMath.MaxLeftoverDistance)) return false;
            if (!pawn.CanReserveAndReach(leftover, PathEndMode.ClosestTouch, Danger.Some)) return false;
            int count = Mathf.Min(leftover.stackCount, pawn.carryTracker.AvailableStackSpace(leftover.def));
            if (count <= 0) return false;
            if (!StoreUtility.TryFindBestBetterStoreCellFor(leftover, pawn, map, StoragePriority.Unstored, pawn.Faction, out IntVec3 cell) || !pawn.Reserve(cell, job, errorOnFailed: false))
                return false;
            // Reserved at once, so no hauler takes it while the pawn walks over.
            if (!pawn.Reserve(leftover, job, errorOnFailed: false))
            {
                map.reservationManager.Release(cell, pawn, job);
                return false;
            }
            job.SetTarget(ItemInd, leftover);
            job.SetTarget(CellInd, cell);
            job.count = count;
            countToDeliver = count;
            carryingLeftover = true;
            Carrying.Debug($"{pawn.LabelShort} also carries the {count}x {leftover.LabelNoCount} that didn't fit.");
            return true;
        }

        /// <summary>
        /// Something still in the hands here didn't fit the storage cell (or the job was loaded from a save): a stack from
        /// the inventory goes back in, the carried leftover is set down.
        /// </summary>
        private void HandleStillCarriedThing(HaulLoad load)
        {
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried == null || carried != job.GetTarget(ItemInd).Thing) return;
            if (carryingLeftover)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
                return;
            }
            ThingDef def = carried.def;
            int count = carried.stackCount;
            int moved = pawn.carryTracker.innerContainer.TryTransferToContainer(carried, pawn.inventory.innerContainer, count);
            if (moved > 0) load.Add(def, moved);
            if (moved < count) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
        }

        /// <summary>Done with Rimfolk's part: Pick Up And Haul's unloading next, when it has stacks to unload.</summary>
        private void Finish()
        {
            if (HaulLinks.PuahFor(pawn) && HaulLinks.PuahCount(pawn) > 0 && pawn.jobs?.jobQueue != null)
            {
                var unload = HaulLinks.PuahUnloadJob(pawn);
                if (unload != null) pawn.jobs.jobQueue.EnqueueFirst(unload, JobTag.MiscWork);
            }
            EndJobWith(JobCondition.Succeeded);
        }

        public override string GetReport()
        {
            Thing thing = job.GetTarget(ItemInd).Thing;
            if (thing == null) return "CC_Haul_Report".Translate();
            if (collecting && thing.Spawned) return "CC_Haul_ReportOnTheWay".Translate(thing.LabelShort.Named("THING"));
            return "CC_Haul_ReportThing".Translate(thing.LabelShort.Named("THING"));
        }
    }
}
