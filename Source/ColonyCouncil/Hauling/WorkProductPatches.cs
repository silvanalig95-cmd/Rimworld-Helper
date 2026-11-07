using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The hooks of everything else work makes (WorkProducts.cs). Like harvest and haul's own, none of them is applied
    // while the standalone Harvest and Haul is loaded (without the job choice's hooks a pocket would never be emptied),
    // except the bills' counts, which count whatever Rimfolk owes to storage; each does nothing while the deeper game
    // hooks are off. All are plain prefixes, postfixes and finalizers that watch: two mark the game's last toil of a bill
    // and take its products off the list as it hands them on; three mark whose job runs a toil or a tick (the actor);
    // three note, while an actor is set, what is made, split off a stack or dropped out of a container; one takes what a
    // job left beside the pawn as the job ends; three add what pockets hold to a "do until" bill's count. The carry
    // step's hook is the material run's, in HaulPatches.cs.

    /// <summary>The game's last toil of a bill: while it runs, the worker is the one finishing (WorkProducts.finishing).</summary>
    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct))]
    public static class Patch_HaulBillFinish
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Toil __result)
        {
            Action inner = __result?.initAction;
            if (inner == null) return;
            Toil toil = __result;
            toil.initAction = () =>
            {
                Pawn before = WorkProducts.finishing;
                WorkProducts.finishing = RimfolkPatches.On ? toil.actor : null;
                try
                {
                    inner();
                }
                finally
                {
                    WorkProducts.finishing = before;
                }
            };
        }
    }

    /// <summary>
    /// The bill's products are made and about to be placed (the game counts them for the worker's records here): those
    /// that may go into the inventory go in and come off the list. Last, so other mods counting them see them all.
    /// </summary>
    [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone))]
    public static class Patch_HaulBillProducts
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn billDoer, List<Thing> products)
        {
            if (billDoer == null || products == null || WorkProducts.finishing != billDoer || !RimfolkPatches.On) return;
            HaulHook.Run(() => WorkProducts.PocketBill(billDoer, products), "a bill's products");
        }
    }

    /// <summary>A job driver starts its next toil (its init action runs here): the pawn is the actor meanwhile.</summary>
    [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
    public static class Patch_HaulWorkToil
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(JobDriver __instance, out Pawn __state)
        {
            __state = WorkProducts.actor;
            WorkProducts.actor = RimfolkPatches.On ? __instance.pawn : null;
        }

        public static Exception Finalizer(Exception __exception, Pawn __state)
        {
            WorkProducts.actor = __state;
            return __exception;
        }
    }

    /// <summary>A job driver's tick (its toil's tick action): the pawn is the actor meanwhile.</summary>
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.DriverTick))]
    public static class Patch_HaulWorkTick
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(JobDriver __instance, out Pawn __state)
        {
            __state = WorkProducts.actor;
            WorkProducts.actor = RimfolkPatches.On ? __instance.pawn : null;
        }

        public static Exception Finalizer(Exception __exception, Pawn __state)
        {
            WorkProducts.actor = __state;
            return __exception;
        }
    }

    /// <summary>A job driver's interval tick (its toil's interval action): the pawn is the actor meanwhile.</summary>
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.DriverTickInterval))]
    public static class Patch_HaulWorkTickInterval
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(JobDriver __instance, out Pawn __state)
        {
            __state = WorkProducts.actor;
            WorkProducts.actor = RimfolkPatches.On ? __instance.pawn : null;
        }

        public static Exception Finalizer(Exception __exception, Pawn __state)
        {
            WorkProducts.actor = __state;
            return __exception;
        }
    }

    /// <summary>A thing was made: while an actor's job runs, an item is noted as that job's.</summary>
    [HarmonyPatch(typeof(ThingMaker), nameof(ThingMaker.MakeThing))]
    public static class Patch_HaulWorkMade
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Thing __result)
        {
            if (WorkProducts.actor == null || __result == null || !RimfolkPatches.On) return;
            try
            {
                WorkProducts.NoteMade(__result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, noting what work made", e);
            }
        }
    }

    /// <summary>A stack was split in two: the new part was never made by the work (a hauler's handful, a dropped part).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class Patch_HaulWorkSplit
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Thing __instance, Thing __result)
        {
            if (WorkProducts.actor == null || __result == null || __result == __instance || !RimfolkPatches.On) return;
            WorkProducts.Forget(__result);
        }
    }

    /// <summary>
    /// Something is dropped out of a container: out of a building's while an actor's job runs, it was taken out by the
    /// work (an egg box emptied); out of a pawn's hands or inventory, it was dropped on purpose and is never taken.
    /// </summary>
    [HarmonyPatch(typeof(GenDrop), nameof(GenDrop.TryDropSpawn))]
    public static class Patch_HaulWorkDrop
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(Thing thing, out IThingHolder __state)
        {
            __state = WorkProducts.actor != null && RimfolkPatches.On ? thing?.holdingOwner?.Owner : null;
        }

        public static void Postfix(bool __result, Thing thing, ref Thing resultingThing, IThingHolder __state)
        {
            if (__state == null) return;
            try
            {
                WorkProducts.Dropped(__state, thing, resultingThing, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, noting what work took out", e);
            }
        }
    }

    /// <summary>
    /// A "do until" bill counts its product: what pawns carry of it to storage for Rimfolk counts too, as the game counts
    /// what is carried in the hands, so the bill doesn't make more meanwhile. Counts whatever Rimfolk owes to storage, so it
    /// works beside the standalone mod.
    /// </summary>
    [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts))]
    public static class Patch_HaulBillCount
    {
        public static void Postfix(RecipeWorkerCounter __instance, Bill_Production bill, ref int __result)
        {
            if (bill == null || !RimfolkPatches.On) return;
            try
            {
                __result += WorkProducts.CarriedFor(__instance, bill);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, a bill's count", e);
            }
        }
    }

    /// <summary>The same for the stone blocks a "do until" stonecutting bill counts.</summary>
    [HarmonyPatch(typeof(RecipeWorkerCounter_MakeStoneBlocks), nameof(RecipeWorkerCounter_MakeStoneBlocks.CountProducts))]
    public static class Patch_HaulBillCountBlocks
    {
        public static void Postfix(Bill_Production bill, ref int __result)
        {
            if (bill == null || !RimfolkPatches.On) return;
            try
            {
                __result += WorkProducts.CarriedOf(bill.Map, ThingCategoryDefOf.StoneBlocks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, a stonecutting bill's count", e);
            }
        }
    }

    /// <summary>The same for the meat a "do until" butchering bill counts.</summary>
    [HarmonyPatch(typeof(RecipeWorkerCounter_ButcherAnimals), nameof(RecipeWorkerCounter_ButcherAnimals.CountProducts))]
    public static class Patch_HaulBillCountMeat
    {
        public static void Postfix(Bill_Production bill, ref int __result)
        {
            if (bill == null || !RimfolkPatches.On) return;
            try
            {
                __result += WorkProducts.CarriedOf(bill.Map, ThingCategoryDefOf.MeatRaw);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, a butchering bill's count", e);
            }
        }
    }

    /// <summary>A job ends (before the game drops what the hands hold): what it made and left beside the pawn goes into the inventory.</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    public static class Patch_HaulWorkJobEnd
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(Pawn_JobTracker __instance, Pawn ___pawn, JobCondition condition)
        {
            if (!WorkProducts.Any || ___pawn == null) return;
            if (!RimfolkPatches.On)
            {
                WorkProducts.Drop(___pawn);
                return;
            }
            try
            {
                WorkProducts.JobEnding(___pawn, __instance.curJob, condition);
            }
            catch (Exception e)
            {
                WorkProducts.Drop(___pawn);
                CouncilLog.ErrorOnce("carrying and hauling, what a job made", e);
            }
        }
    }
}
