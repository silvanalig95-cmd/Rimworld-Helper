using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // World situations reaching into the game's own events, through Harmony. All of them do nothing unless a world
    // situation is running, and all switch off with the "deeper game hooks" setting (RimfolkPatches.On).

    /// <summary>
    /// While a world situation lasts, some of the game's own incidents are likelier or rarer (more herds in a
    /// migration, plague likelier in a plague year). This only matters where the storyteller picks among a kind of
    /// incident by weight; Rimfolk's own events are weighed in HappeningTracker.StorytellerFactor instead.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.BaseChanceThisGame), MethodType.Getter)]
    public static class Patch_WS_IncidentChance
    {
        public static void Postfix(IncidentWorker __instance, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On || __instance?.def == null) return;
            try
            {
                float f = WorldSituations.IncidentFactor(__instance.def);
                if (f != 1f && !__instance.def.HasModExtension<HappeningExtension>()) __result *= f;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world situation incident weight", e);
            }
        }
    }

    /// <summary>A warlord's people raid harder while he rises.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), nameof(IncidentWorker_Raid.AdjustedRaidPoints))]
    public static class Patch_WS_RaidPoints
    {
        public static void Postfix(Faction faction, ref float __result)
        {
            if (!RimfolkPatches.On || faction == null || __result <= 0f) return;
            try
            {
                __result *= WorldSituations.RaidPointsFactor(faction);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world situation raid points", e);
            }
        }
    }

    /// <summary>
    /// A warlord's people raid more often while he rises: when the storyteller sends a raid without saying from whom,
    /// it's sometimes his people (if they could have come anyway). Not a quest's raid, nor one whose plan or way in
    /// was already chosen (that may not suit his people).
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
    public static class Patch_WS_RaidFaction
    {
        public static void Prefix(IncidentWorker_RaidEnemy __instance, IncidentParms parms)
        {
            if (!RimfolkPatches.On || parms == null || parms.faction != null || parms.quest != null || parms.raidStrategy != null || parms.raidArrivalMode != null) return;
            try
            {
                var f = WorldSituations.RaidFaction();
                if (f == null || !Rand.Chance(WarlordWorker.RaidShare) || !Raids.CanRaid(f)) return;
                if (parms.points > 0f && parms.points < f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, null)) return;
                if (!__instance.FactionCanBeGroupSource(f, parms, false)) return;
                parms.faction = f;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world situation raid faction", e);
            }
        }
    }

    /// <summary>A raid of the warlord's people has just arrived: sometimes he walks in at its head.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_WS_WarlordJoins
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns)
        {
            if (!RimfolkPatches.On || Raids.Sending) return;
            try
            {
                WarlordWorker.JoinArrivingRaid(parms, pawns);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("warlord raid hook", e);
            }
        }
    }

    /// <summary>The raid letter names the warlord when he rides with it.</summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "GetLetterText")]
    public static class Patch_WS_RaidLetter
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns, ref string __result)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                string note = WarlordWorker.RaidNote(parms, pawns);
                if (!note.NullOrEmpty()) __result += "\n\n" + note;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("warlord raid letter", e);
            }
        }
    }
}
