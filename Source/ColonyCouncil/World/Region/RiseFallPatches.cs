using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town defeated by the colony's people (the game's <c>SettlementDefeatUtility.CheckDefeated</c>, which puts its
    /// destroyed settlement in its place and takes the town away): within a home's reach, a ruin will stand on its tile
    /// once the destroyed settlement and its map are gone, where the game would leave nothing (decision 5,
    /// World/Region/RiseFall.cs). Notes it only; nothing of the game's own is changed.
    /// </summary>
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
    public static class Patch_RiseFallDefeated
    {
        public static void Prefix(Settlement factionBase, out (int id, PlanetTile tile, Faction people, string name) __state)
        {
            __state = (-1, PlanetTile.Invalid, null, null);
            if (factionBase == null || factionBase.Faction == null || factionBase.Faction.IsPlayer || !RimfolkPatches.On || !RiseFall.On) return;
            __state = (factionBase.ID, factionBase.Tile, factionBase.Faction, factionBase.HasName ? factionBase.Name : null);
        }

        public static void Postfix(Settlement factionBase, (int id, PlanetTile tile, Faction people, string name) __state)
        {
            if (__state.id < 0 || factionBase == null || !factionBase.Destroyed) return;
            try
            {
                RiseFall.Fell(__state.id, __state.tile, __state.people, __state.name, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a town defeated", e);
            }
        }
    }

    /// <summary>
    /// A ruin Rimfolk keeps says in the game's own inspect pane what town it was ("Once Ashford of the Lenlia, given up
    /// 40 days ago."), so the map shows what the news told. A line added after the game's (and any other mod's); nothing
    /// for any other abandoned settlement.
    /// </summary>
    [HarmonyPatch(typeof(AbandonedSettlement), nameof(AbandonedSettlement.GetInspectString))]
    public static class Patch_RiseFallRuinInspect
    {
        public static void Postfix(AbandonedSettlement __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                string line = RiseFallWords.Inspect(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a ruin's inspect line", e);
            }
        }
    }

    /// <summary>
    /// A town's fortune in its people's caravans (docs/design/stage1-region.md, item 2, "What it changes"): when the game
    /// sends a trader caravan without saying whose, each people that could send one weighs 1.25 while their town nearest
    /// the home thrives or flourishes and 0.6 while it struggles (RiseFallMath.CaravanFactor), instead of all alike. Only
    /// when one of them weighs other than 1; a caravan whose people is already chosen (a quest's, another mod's) is left
    /// as it is, and the game picks as before when this finds none.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RiseFallCaravans
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => AccessTools.Method(typeof(IncidentWorker_TraderCaravanArrival), "TryResolveParmsGeneral");

        public static void Prefix(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms)
        {
            if (parms == null || parms.faction != null || !(parms.target is Map map) || !RimfolkPatches.On || !RiseFallEffects.On) return;
            try
            {
                var all = Find.FactionManager.AllFactionsListForReading;
                var can = new List<Faction>();
                var weights = new List<float>();
                bool any = false;
                foreach (var f in all)
                {
                    if (f == null || !__instance.FactionCanBeGroupSource(f, parms)) continue;
                    float w = RiseFallEffects.CaravanFactor(f, map);
                    if (w != 1f) any = true;
                    can.Add(f);
                    weights.Add(w);
                }
                if (!any || can.Count == 0) return;
                float sum = 0f;
                foreach (float w in weights) sum += w;
                float at = Rand.Value * sum;
                for (int i = 0; i < can.Count; i++)
                {
                    at -= weights[i];
                    if (at > 0f && i < can.Count - 1) continue;
                    parms.faction = can[i];
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a caravan's people", e);
            }
        }
    }
}
