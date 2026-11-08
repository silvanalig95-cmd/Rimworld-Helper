using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // Release F's game hooks (Peoples/PeoplesF.cs, PeoplesF2.cs), each off with the deeper game hooks (RimfolkPatches.On):
    // the Signal Line's mast warning of a raid a little before it comes, the Deepwell Union's strike stalling its trade
    // (no caravans, nothing sold by its visitors, no trade at its pits), and a citizen's terms with the Last Republic's
    // traders.

    /// <summary>
    /// A raid the storyteller sends to a home whose relay mast sings (not a quest's, not one coming back out of the game's
    /// queue) goes into the game's incident queue a couple of hours on, and the line's warning is told now
    /// (RelayMast.Warn); the storyteller doesn't fire it now. The Conclave's watch on the sky (Patch_GlassHold) holds
    /// only the sky's conditions, so the two never hold the same incident.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_LineMastWarns
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result)
        {
            if (queued || !RimfolkPatches.On || fi?.def == null || !(fi.def.Worker is IncidentWorker_Raid)) return true;
            try
            {
                if (!RelayMast.Warn(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the mast warns of a raid", e);
                return true;
            }
        }
    }

    /// <summary>A Deepwell Union on strike sends no trader caravan (UnionStrike.Striking).</summary>
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), nameof(IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource))]
    public static class Patch_UnionStrikeCaravans
    {
        public static void Postfix(Faction f, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                if (UnionStrike.Striking(f)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: a strike's caravans", e);
            }
        }
    }

    /// <summary>The visitors of a Deepwell Union on strike bring nothing to sell: none of them is made a trader.</summary>
    [HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryConvertOnePawnToSmallTrader")]
    public static class Patch_UnionStrikeVisitors
    {
        public static bool Prefix(Faction faction, ref bool __result)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                if (!UnionStrike.Striking(faction)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: a strike's visitors", e);
                return true;
            }
        }
    }

    /// <summary>A pit of a Deepwell Union on strike doesn't trade with the colony's caravans.</summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.CanTradeNow), MethodType.Getter)]
    public static class Patch_UnionStrikeTown
    {
        public static void Postfix(Settlement __instance, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On || __instance == null) return;
            try
            {
                if (UnionStrike.Striking(__instance.Faction)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: a strike's pits", e);
            }
        }
    }

    /// <summary>A trader of the Last Republic (a caravan's or a visitor's) gives a colony holding its papers a citizen's terms (Papers.Terms).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_PapersPawnTerms
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null || __instance.Faction.IsPlayer) return;
            try
            {
                __result += Papers.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: a citizen's terms with a trader", e);
            }
        }
    }

    /// <summary>A town of the Last Republic gives the colony's caravan a citizen's terms while the colony holds its papers.</summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_PapersTownTerms
    {
        public static void Postfix(Settlement __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null) return;
            try
            {
                __result += Papers.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: a citizen's terms at a town", e);
            }
        }
    }
}
