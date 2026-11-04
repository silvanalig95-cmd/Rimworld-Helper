using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The settlers beside a caravan of the colony's (decision 10: kept beside it, not in its pawn list): a line in the
    /// game's own inspect pane of the caravan names them ("With them: six settlers of the Lenlia under Tamsin Coor, on the
    /// way to found their hamlet."), so the map shows what the news told. A line added after the game's; nothing for any
    /// other caravan.
    /// </summary>
    [HarmonyPatch(typeof(Caravan), nameof(Caravan.GetInspectString))]
    public static class Patch_HamletCaravanLine
    {
        public static void Postfix(Caravan __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.IsPlayerControlled) return;
            try
            {
                string line = HamletWords.CaravanLine(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the caravan's line", e);
            }
        }
    }

    /// <summary>
    /// A fight's map made for a caravan of the colony's (the game's caravan demand refused, an ambush): when settlers on their
    /// way to found a hamlet go with it, its grown settlers stand among the defenders (World/Region/HamletEscort.cs). Read
    /// before the game takes the caravan onto the map; the settlers come after it has. Nothing of the game's own is changed.
    /// </summary>
    [HarmonyPatch(typeof(CaravanIncidentUtility), nameof(CaravanIncidentUtility.SetupCaravanAttackMap))]
    public static class Patch_HamletFight
    {
        public static void Prefix(Caravan caravan, out (HamletHome h, Map home) __state)
        {
            __state = (null, null);
            if (!RimfolkPatches.On || caravan == null) return;
            try
            {
                var h = Hamlets.EscortedBy(caravan, out var home);
                if (h != null) __state = (h, home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a fight on the road", e);
            }
        }

        public static void Postfix(Map __result, List<Pawn> enemies, (HamletHome h, Map home) __state)
        {
            if (__state.h == null || __result == null) return;
            try
            {
                Hamlets.Fight(__state.h, __state.home, __result, enemies);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: settlers in the fight", e);
            }
        }
    }
}
