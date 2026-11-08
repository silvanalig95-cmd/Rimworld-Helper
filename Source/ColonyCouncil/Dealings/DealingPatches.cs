using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A trader of a people the colony has dealings with (a caravan's or a visitor's) gives it the better terms they bring:
    /// market rights in their town, the colony's trade terms posted and taken up, a pen friend among them (Dealings.Terms,
    /// at most DealingMath.MaxOffset). With the deeper game hooks off, nothing changes.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_DealingPawnTerms
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null || __instance.Faction.IsPlayer) return;
            try
            {
                __result += Dealings.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the terms with a trader", e);
            }
        }
    }

    /// <summary>A town of a people the colony has dealings with gives the colony's caravan the same better terms.</summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_DealingTownTerms
    {
        public static void Postfix(Settlement __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null) return;
            try
            {
                __result += Dealings.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the terms at a town", e);
            }
        }
    }

    /// <summary>
    /// The game's choice of raiders (IncidentWorker_RaidEnemy.FactionCanBeGroupSource): a people that granted the colony's
    /// petition about its raiders is not chosen while the term runs, as a truce does. Raids whose people is set beforehand
    /// never ask it; one of theirs that comes all the same breaks their word (Patch_DealingRaided).
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), nameof(IncidentWorker_RaidEnemy.FactionCanBeGroupSource))]
    public static class Patch_DealingRaiders
    {
        public static void Postfix(Faction f, IncidentParms parms, ref bool __result)
        {
            if (!__result || f == null || !RimfolkPatches.On || !(parms?.target is Map map) || !Homes.IsHome(map)) return;
            try
            {
                if (Dealings.KeepsAway(f)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the choice of raiders", e);
            }
        }
    }

    /// <summary>A raid has arrived at a home: one of a people whose raiders were to be kept away breaks their word.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_DealingRaided
    {
        public static void Postfix(IncidentParms parms)
        {
            if (!RimfolkPatches.On || !(parms?.target is Map map) || parms.faction == null || !Homes.IsHome(map)) return;
            try
            {
                if (Dealings.KeepsAway(parms.faction)) Dealings.Raided(parms.faction, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: a raid arrived", e);
            }
        }
    }

    /// <summary>
    /// A raid the storyteller sends to a home that a watchtower built together or a pen friend can see coming (not a
    /// quest's, not one coming back out of the game's queue) goes into the game's incident queue a few hours on, and the
    /// word comes now (DealingWarnings.Warn); the storyteller doesn't fire it now. While the colony's relay mast sings, its
    /// own warning has the raid instead (Peoples/PeoplesFPatches.cs).
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_DealingWarns
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result)
        {
            if (queued || !RimfolkPatches.On || fi?.def == null || !(fi.def.Worker is IncidentWorker_RaidEnemy)) return true;
            try
            {
                if (!DealingWarnings.Warn(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: word of a raid", e);
                return true;
            }
        }
    }
}
