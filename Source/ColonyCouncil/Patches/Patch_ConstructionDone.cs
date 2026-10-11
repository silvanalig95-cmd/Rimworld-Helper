using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// When a colonist finishes building something, wishes it fulfils come true at once (instead of at the next hourly
    /// check), so the joy lands while the builder is still standing there.
    /// </summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_ConstructionDone
    {
        public static void Postfix(Pawn worker)
        {
            if (!RimfolkPatches.On || worker?.Faction != Faction.OfPlayer) return;
            CouncilGameComponent.Instance?.Notify_Constructed();
        }
    }
}
