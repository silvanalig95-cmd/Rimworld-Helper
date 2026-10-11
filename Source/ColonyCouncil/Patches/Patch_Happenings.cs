using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Someone who isn't a colonist dies: a prisoner in the colony's hands (grudges), the great beast. Runs before the death.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_KilledStranger
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || __instance.IsColonist) return;
            try
            {
                CouncilGameComponent.Instance?.happenings?.Notify_Killed(__instance, dinfo);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("stranger death hook", e);
            }
        }
    }

    /// <summary>A colonist banished (or walking out): they might come back one day. Both ways of banishing are covered.</summary>
    [HarmonyPatch(typeof(PawnBanishUtility), nameof(PawnBanishUtility.Banish), new[] { typeof(Pawn), typeof(bool) })]
    public static class Patch_Banished
    {
        public static void Prefix(Pawn pawn, out bool __state)
        {
            __state = RimfolkPatches.On && pawn != null && !pawn.Dead && pawn.IsColonist;
        }

        public static void Postfix(Pawn pawn, bool __state)
        {
            if (!__state || pawn == null || pawn.Dead) return;
            try
            {
                CouncilGameComponent.Instance?.happenings?.Notify_Banished(pawn);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("banish hook", e);
            }
        }
    }

    [HarmonyPatch(typeof(PawnBanishUtility), nameof(PawnBanishUtility.Banish), new[] { typeof(Pawn), typeof(RimWorld.Planet.PlanetTile), typeof(bool) })]
    public static class Patch_BanishedFromCaravan
    {
        public static void Prefix(Pawn pawn, out bool __state) => Patch_Banished.Prefix(pawn, out __state);

        public static void Postfix(Pawn pawn, bool __state) => Patch_Banished.Postfix(pawn, __state);
    }
}
