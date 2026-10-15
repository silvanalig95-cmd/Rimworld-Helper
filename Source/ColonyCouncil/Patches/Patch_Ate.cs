using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist ate something: noted for the everyday pressures (the same meal again, a varied table). Only reads
    /// what was eaten; the meal, its mood and nutrition are the game's own. Without this hook (deeper game hooks off),
    /// meals simply aren't counted and neither mood comes up.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_Ate
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null || __instance?.def == null || ingester.Faction != Faction.OfPlayer || !ingester.RaceProps.Humanlike) return;
            try
            {
                CouncilGameComponent.Instance?.pressures?.Notify_Ate(ingester, __instance);
                // A meal eaten at a feast an answer called (PlayOut/): counted for its closing message.
                PlayOuts.Notify_Ate(ingester, __instance);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meal hook", e);
            }
        }
    }
}
