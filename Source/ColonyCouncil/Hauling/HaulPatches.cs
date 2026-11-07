using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Carrying and hauling's hooks (Hauling/). Harvest and haul's (the job choice, the drop of unused inventory, and the
    // seven places work drops things) are the player's Harvest and Haul's own prefixes and postfixes: they only watch
    // what the game does, and none of them is applied while the standalone mod is loaded (Prepare, with the reason for
    // the self-check). The material run's two (the hands' pickup, and the delivery job for a frame or blueprint) and
    // things on the way don't touch what that mod patches, so they work beside it. Every hook does nothing while the
    // deeper game hooks are off, and swallows its own errors.

    internal static class HaulHook
    {
        public static void Run(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, " + what, e);
            }
        }

        public static HashSet<ThingDef> DefsOf(List<ThingDefCountClass> costs)
        {
            if (costs == null || costs.Count == 0) return null;
            var defs = new HashSet<ThingDef>();
            for (int i = 0; i < costs.Count; i++)
                if (costs[i]?.thingDef != null)
                    defs.Add(costs[i].thingDef);
            return defs;
        }

        /// <summary>Harvest and haul's hooks are applied only without the standalone mod.</summary>
        public static bool Mine => !HaulLinks.Standalone;
    }

    /// <summary>The window in which a pawn chooses a new job because its last one ended (not the game's regular look for a better one).</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
    public static class Patch_HaulChoosing
    {
        public static Pawn choosing;

        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(Pawn ___pawn, out Pawn __state)
        {
            __state = choosing;
            choosing = ___pawn;
        }

        public static Exception Finalizer(Exception __exception, Pawn __state)
        {
            choosing = __state;
            return __exception;
        }
    }

    /// <summary>The job just chosen becomes a trip to storage first when the pawn's load should go now.</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Patch_HaulNextJob
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Pawn ___pawn, ref ThinkResult __result)
        {
            if (___pawn == null || Patch_HaulChoosing.choosing != ___pawn || !RimfolkPatches.On) return;
            try
            {
                HarvestHaul.TryReplaceNextJob(___pawn, ref __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, choosing a delivery", e);
            }
        }
    }

    /// <summary>The game drops raw food from a pawn's inventory in the home area; what is on its way to storage or to a build stays.</summary>
    [HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
    public static class Patch_HaulKeepOwed
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static bool Prefix(Pawn pawn, Thing thing)
        {
            if (!RimfolkPatches.On || thing == null) return true;
            var load = HaulBook.Get?.LoadOf(pawn);
            return load == null || !load.Owes(thing.def) && load.RunCount(thing.def) <= 0;
        }
    }

    /// <summary>A plant was harvested or cut, also as part of another job (clearing a cell for sowing or building).</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_HaulPlantCollected
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Plant __instance, Pawn by)
        {
            if (by == null || !RimfolkPatches.On) return;
            HaulHook.Run(() => HarvestHaul.PlantCollected(by, __instance), "a harvest");
        }
    }

    /// <summary>A cell was mined out (ore, steel, components...).</summary>
    [HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined))]
    public static class Patch_HaulMined
    {
        public struct State
        {
            public Map map;
            public CellRect rect;
            public ThingDef product;
        }

        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(Mineable __instance, out State __state)
        {
            __state = default;
            if (__instance == null || !__instance.Spawned || !RimfolkPatches.On) return;
            __state.map = __instance.Map;
            __state.rect = __instance.OccupiedRect();
            __state.product = __instance.def?.building?.mineableThing;
        }

        public static void Postfix(Pawn pawn, State __state)
        {
            if (pawn == null || __state.map == null || __state.product == null) return;
            HaulHook.Run(() => HarvestHaul.Mined(pawn, __state.map, __state.rect, __state.product), "what was mined");
        }
    }

    /// <summary>A deep drill brought up a portion.</summary>
    [HarmonyPatch(typeof(CompDeepDrill), "TryProducePortion")]
    public static class Patch_HaulDeepDrill
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(CompDeepDrill __instance, Pawn driller)
        {
            if (driller == null || __instance == null || !RimfolkPatches.On) return;
            HaulHook.Run(() => HarvestHaul.DeepDrillPortion(driller, __instance.parent), "the deep drill's portion");
        }
    }

    /// <summary>An animal was shorn or milked (or another of its body's products gathered).</summary>
    [HarmonyPatch(typeof(CompHasGatherableBodyResource), nameof(CompHasGatherableBodyResource.Gathered))]
    public static class Patch_HaulGathered
    {
        private static Func<CompHasGatherableBodyResource, ThingDef> resourceDef;
        private static bool noGetter;

        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(CompHasGatherableBodyResource __instance, Pawn doer)
        {
            if (doer == null || __instance == null || !RimfolkPatches.On || !Getter()) return;
            HaulHook.Run(() => HarvestHaul.Gathered(doer, resourceDef(__instance)), "an animal's product");
        }

        // ResourceDef is protected: a delegate to it, made once.
        private static bool Getter()
        {
            if (resourceDef != null) return true;
            if (noGetter) return false;
            try
            {
                MethodInfo get = AccessTools.PropertyGetter(typeof(CompHasGatherableBodyResource), "ResourceDef");
                if (get != null) resourceDef = AccessTools.MethodDelegate<Func<CompHasGatherableBodyResource, ThingDef>>(get);
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Animals' products won't be collected: " + e.Message);
            }
            noGetter = resourceDef == null;
            return !noGetter;
        }
    }

    /// <summary>
    /// A building was taken apart: the materials it gives back go into the inventory, and anything else it leaves
    /// (chunks, fuel, its contents) is hauled by hand right after.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Deconstruct), "FinishedRemoving")]
    public static class Patch_HaulDeconstructed
    {
        public struct State
        {
            public Map map;
            public CellRect rect;
            public HashSet<ThingDef> refunds;
            public HashSet<int> before;
        }

        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(JobDriver_Deconstruct __instance, out State __state)
        {
            __state = default;
            if (!RimfolkPatches.On) return;
            try
            {
                Thing target = __instance.job?.targetA.Thing;
                if (target == null || !target.Spawned) return;
                __state.map = target.Map;
                __state.rect = target.OccupiedRect();
                __state.refunds = HaulHook.DefsOf(target.CostListAdjusted());
                __state.before = HarvestHaul.SnapshotLeftovers(__state.map, __state.rect);
            }
            catch (Exception e)
            {
                __state = default;
                CouncilLog.ErrorOnce("carrying and hauling, a deconstruction's refunds", e);
            }
        }

        public static void Postfix(JobDriver_Deconstruct __instance, State __state)
        {
            Pawn pawn = __instance?.pawn;
            if (pawn == null || __state.map == null) return;
            if (__state.refunds != null) HaulHook.Run(() => HarvestHaul.Deconstructed(pawn, __state.map, __state.rect, __state.refunds), "a deconstruction's refunds");
            if (__state.before != null) HaulHook.Run(() => HarvestHaul.DeconstructionLeftovers(pawn, __state.map, __state.rect, __state.before), "a deconstruction's leftovers");
        }
    }

    /// <summary>A floor was taken up and gave back part of its materials.</summary>
    [HarmonyPatch(typeof(JobDriver_RemoveFloor), "DoEffect", new[] { typeof(IntVec3) })]
    public static class Patch_HaulFloorRemoved
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Prefix(JobDriver_RemoveFloor __instance, IntVec3 c, out HashSet<ThingDef> __state)
        {
            __state = null;
            if (!RimfolkPatches.On) return;
            Map map = __instance?.pawn?.Map;
            if (map != null && c.InBounds(map)) __state = HaulHook.DefsOf(c.GetTerrain(map)?.CostList);
        }

        public static void Postfix(JobDriver_RemoveFloor __instance, IntVec3 c, HashSet<ThingDef> __state)
        {
            Pawn pawn = __instance?.pawn;
            if (pawn?.Map == null || __state == null) return;
            HaulHook.Run(() => HarvestHaul.Deconstructed(pawn, pawn.Map, CellRect.SingleCell(c), __state), "a floor's refunds");
        }
    }

    /// <summary>Fish were caught (Odyssey); they are placed right after this returns.</summary>
    [HarmonyPatch(typeof(FishingUtility), nameof(FishingUtility.GetCatchesFor))]
    public static class Patch_HaulFish
    {
        public static bool Prepare() => HaulHook.Mine;
        public static string StandsDownWhy() => HaulLinks.StandsDownWhy;

        public static void Postfix(Pawn pawn, IntVec3 cell, List<Thing> __result)
        {
            if (pawn == null || __result == null || !RimfolkPatches.On || !(pawn.jobs?.curDriver is JobDriver_Fish)) return;
            HaulHook.Run(() => HarvestHaul.FishCaught(pawn, cell, __result), "a catch");
        }
    }

    /// <summary>
    /// The material run, at the stack: before the game's delivery job puts building material in the hands, the inventory
    /// takes what the rest of the build area needs (MaterialRun.AtPickup). Only for the game's own delivery to a frame
    /// or blueprint; every other job that starts carrying something is left as it is.
    /// </summary>
    [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.StartCarryThing))]
    public static class Patch_HaulRunPickup
    {
        public static void Postfix(Toil __result, TargetIndex haulableInd)
        {
            if (__result == null || haulableInd != TargetIndex.A) return;
            Toil toil = __result;
            toil.AddPreInitAction(() =>
            {
                if (!RimfolkPatches.On) return;
                HaulHook.Run(() => MaterialRun.AtPickup(toil.actor), "a material run's pickup");
            });
        }
    }

    /// <summary>
    /// The game's delivery job for a frame or blueprint: out of the pawn's run stock when it has what this one needs
    /// (before the game would look for a stack); none from the stockpile for material another pawn's run covers here;
    /// and before a long walk back to a far stockpile, the loose things on the way are put in storage first.
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
    public static class Patch_HaulRunDeliver
    {
        public static bool Prefix(Pawn pawn, IConstructible c, bool forced, ref Job __result)
        {
            if (!RimfolkPatches.On || pawn == null || c == null) return true;
            try
            {
                Job fromStock = MaterialRun.FromStock(pawn, c, forced);
                if (fromStock != null)
                {
                    __result = fromStock;
                    return false;
                }
                if (MaterialRun.CoveredForOthers(pawn, c, forced))
                {
                    __result = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, a delivery out of the stock", e);
            }
            return true;
        }

        public static void Postfix(Pawn pawn, IConstructible c, bool forced, ref Job __result)
        {
            if (__result == null || forced || pawn == null || !RimfolkPatches.On || FloatMenuMakerMap.makingFor == pawn) return;
            if (__result.def != JobDefOf.HaulToContainer || c is Blueprint_Install || !(c is Thing target)) return;
            try
            {
                var found = OnTheWay.BeforeFetch(pawn, __result.targetA.Thing, target);
                if (found.Count == 0) return;
                var job = JobMaker.MakeJob(HaulDefOf.Rimfolk_CarryToStorage);
                job.targetQueueA = new List<LocalTargetInfo>();
                foreach (var t in found) job.targetQueueA.Add(t);
                __result = job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, things on the way", e);
            }
        }
    }
}
