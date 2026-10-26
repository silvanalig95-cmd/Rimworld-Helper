using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A fix to the game's own check of whether a material is on the map in a given amount (ItemAvailability), asked
    /// before anyone carries materials to a blueprint or frame. The game remembers the answer for the rest of the tick
    /// under the material alone, so the first amount asked decides for every later one: a build that needs more steel
    /// than the colony has (a research bench short of steel) answered "no" first, and then no other build got any steel
    /// that tick either, a drug lab that needs far less included, tick after tick. Here the count on hand is remembered
    /// instead, and each amount is set against it. The game's own rule stays: a build gets a material only when the whole
    /// amount it still needs is on the map. With the deeper game hooks off, the game's check stands.
    /// </summary>
    [HarmonyPatch(typeof(ItemAvailability), nameof(ItemAvailability.ThingsAvailableAnywhere))]
    public static class Patch_MaterialsOnHand
    {
        private static int tick = -1;
        private static readonly Dictionary<(Map, ThingDef, Faction), int> onHand = new Dictionary<(Map, ThingDef, Faction), int>();

        public static bool Prefix(ThingDef need, int amount, Pawn pawn, Map ___map, ref bool __result)
        {
            if (!RimfolkPatches.On || need == null || pawn == null || ___map == null) return true;
            try
            {
                int now = Find.TickManager.TicksGame;
                if (now != tick)
                {
                    tick = now;
                    onHand.Clear();
                }
                var key = (___map, need, pawn.Faction);
                if (!onHand.TryGetValue(key, out int n))
                {
                    // As the game counts it: every stack of it on the map that this pawn's people may use.
                    var list = ___map.listerThings.ThingsOfDef(need);
                    for (int i = 0; i < list.Count; i++)
                        if (!list[i].IsForbidden(pawn)) n += list[i].stackCount;
                    onHand[key] = n;
                }
                __result = n >= amount;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("materials on hand", e);
                return true;
            }
        }
    }
}
