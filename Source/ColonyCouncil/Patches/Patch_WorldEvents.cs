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
    /// A caravan arriving: who was in it and how long they were away; afterwards, if it's home, a homecoming. Both ways
    /// a caravan can enter a map are hooked; whichever runs first handles it.
    /// </summary>
    public static class CaravanHome
    {
        private static int lastCaravan = -1;
        private static int lastTick = -1;

        public static void Before(Caravan caravan, out (List<Pawn> pawns, int away, int id) state)
        {
            state = (null, 0, -1);
            if (!RimfolkPatches.On || caravan == null || !caravan.IsPlayerControlled) return;
            int now = Find.TickManager.TicksGame;
            if (caravan.ID == lastCaravan && now == lastTick) return;
            lastCaravan = caravan.ID;
            lastTick = now;
            state = (caravan.PawnsListForReading.ToList(), now - caravan.creationGameTicks, caravan.ID);
        }

        public static void After(Map map, (List<Pawn> pawns, int away, int id) state)
        {
            if (state.pawns == null || map == null || !map.IsPlayerHome) return;
            try
            {
                WorldReactions.Homecoming(state.pawns, state.away);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("homecoming hook", e);
            }
        }
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
        new[] { typeof(Caravan), typeof(Map), typeof(System.Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
    public static class Patch_CaravanHome
    {
        public static void Prefix(Caravan caravan, out (List<Pawn> pawns, int away, int id) __state) => CaravanHome.Before(caravan, out __state);
        public static void Postfix(Map map, (List<Pawn> pawns, int away, int id) __state) => CaravanHome.After(map, __state);
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
        new[] { typeof(Caravan), typeof(Map), typeof(CaravanEnterMode), typeof(CaravanDropInventoryMode), typeof(bool), typeof(System.Predicate<IntVec3>) })]
    public static class Patch_CaravanHomeByMode
    {
        public static void Prefix(Caravan caravan, out (List<Pawn> pawns, int away, int id) __state) => CaravanHome.Before(caravan, out __state);
        public static void Postfix(Map map, (List<Pawn> pawns, int away, int id) __state) => CaravanHome.After(map, __state);
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
