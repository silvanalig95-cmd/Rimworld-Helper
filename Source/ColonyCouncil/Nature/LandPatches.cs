using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A plant burned to death (the game's fire, by its flame damage): the land keeps the burn as a patch to come back (Nature/LandRegrowth.cs).</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.Kill))]
    public static class Patch_LandBurnt
    {
        public static void Prefix(Plant __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || dinfo == null || dinfo.Value.Def != DamageDefOf.Flame) return;
            try
            {
                LandRegrowth.Burnt(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: a plant burned", e);
            }
        }
    }

    /// <summary>A tree cut down by one of the colony's people (counted before it is gone from the map): the land keeps the felled wood as a patch to come back.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_LandFelled
    {
        public static void Prefix(Plant __instance, Pawn by)
        {
            if (!RimfolkPatches.On || __instance?.def?.plant == null || !__instance.def.plant.IsTree || by?.Faction == null || !by.Faction.IsPlayer) return;
            try
            {
                LandRegrowth.Felled(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: a tree felled", e);
            }
        }
    }

    /// <summary>
    /// The game's blight on a wild plant (a sickness of the land's: Nature/LandSickness.cs) doesn't spread on its own: the
    /// land's own rules spread it, among its kind and within its caps. The game's crop blight in the colony's fields is left
    /// as it is.
    /// </summary>
    [HarmonyPatch(typeof(Blight), nameof(Blight.TryReproduceNow))]
    public static class Patch_LandWildBlight
    {
        public static bool Prefix(Blight __instance)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                var plant = __instance?.Plant;
                return plant == null || plant.sown;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: blight on a wild plant", e);
                return true;
            }
        }
    }
}
