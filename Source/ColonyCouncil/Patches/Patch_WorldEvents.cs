using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A colonist downed: who did it, exactly (for nemeses).</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_Downed
    {
        public static void Postfix(Pawn ___pawn, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || ___pawn == null || !___pawn.IsColonist) return;
            try
            {
                if (dinfo?.Instigator is Pawn attacker) CouncilGameComponent.Instance?.nemeses?.Notify_Harmed(___pawn, attacker, "downed");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("downed hook", e);
            }
        }
    }

    /// <summary>A colonist killed: who did it, exactly (for nemeses). Runs before the death, while they're still ours.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Killed
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || !__instance.IsColonist) return;
            try
            {
                if (__instance.Spawned) CouncilGameComponent.Instance?.social?.Notify_ColonistFell(__instance);
                if (dinfo?.Instigator is Pawn attacker) CouncilGameComponent.Instance?.nemeses?.Notify_Harmed(__instance, attacker, "killed");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("killed hook", e);
            }
        }
    }

    [HarmonyPatch(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married))]
    public static class Patch_Married
    {
        public static void Postfix(Pawn firstPawn, Pawn secondPawn)
        {
            try
            {
                WorldReactions.Wedding(firstPawn, secondPawn);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("wedding hook", e);
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class Patch_Birth
    {
        public static void Postfix(Thing __result, Pawn geneticMother, Pawn father)
        {
            try
            {
                if (__result is Pawn baby) WorldReactions.Birth(baby, geneticMother, father);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("birth hook", e);
            }
        }
    }

    /// <summary>
    /// A caravan arriving at a home map: a homecoming. (The other way a caravan enters a map hands over to this one.)
    /// </summary>
    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
        new[] { typeof(Caravan), typeof(Map), typeof(System.Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
    public static class Patch_CaravanHome
    {
        public static void Prefix(Caravan caravan, out List<Pawn> __state)
        {
            __state = RimfolkPatches.On && caravan != null && caravan.IsPlayerControlled ? caravan.PawnsListForReading.ToList() : null;
        }

        public static void Postfix(Map map, List<Pawn> __state)
        {
            if (__state == null || map == null || !map.IsPlayerHome) return;
            try
            {
                WorldReactions.Homecoming(__state);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("homecoming hook", e);
            }
        }
    }

    /// <summary>A colonist breaking down from their mood (not breaks forced by events or psycasts).</summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_Breakdown
    {
        public static void Postfix(bool __result, Pawn ___pawn, MentalStateDef stateDef, bool causedByMood)
        {
            if (!__result || !causedByMood) return;
            try
            {
                WorldReactions.Breakdown(___pawn, stateDef);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("breakdown hook", e);
            }
        }
    }
}
