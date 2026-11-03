using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A traveller in the last hours before setting off puts work aside (an emergency still calls them).</summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_JourneyNoWork
    {
        public static bool Prefix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (!RimfolkPatches.On || __instance == null || __instance.emergency || !JourneyBook.Excused(pawn)) return true;
            __result = ThinkResult.NoJob;
            return false;
        }
    }

    /// <summary>A traveller keeps the food packed for the road: the game's rule of dropping raw food not eaten lately waits until they're back.</summary>
    [HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "TryGiveJob")]
    public static class Patch_JourneyKeepPacks
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!RimfolkPatches.On || !JourneyBook.Excused(pawn)) return true;
            __result = null;
            return false;
        }
    }

    /// <summary>A traveller who changed for the road stays in those clothes: the game's own outfit choice waits until they're back.</summary>
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    public static class Patch_JourneyKeepClothes
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!RimfolkPatches.On || !(ColonyCouncilMod.Settings?.journeyClothes ?? true) || !JourneyBook.Excused(pawn)) return true;
            __result = null;
            return false;
        }
    }

    /// <summary>
    /// Waiting at the gate to set off: walk to a spot near it, then stand a while and chat with whoever else is there
    /// (the journey's pass gives it again while they wait). Ends when they are no longer a traveller in the last hours.
    /// </summary>
    public class JobDriver_JourneyWait : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !JourneyBook.Excused(pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var wait = new Toil
            {
                initAction = () =>
                {
                    if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                    JourneyRun.AtGate(pawn);
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = 900,
                socialMode = RandomSocialMode.Normal
            };
            yield return wait;
        }
    }
}
