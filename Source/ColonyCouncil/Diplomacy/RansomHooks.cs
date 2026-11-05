using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's ransom demand without a comms console (decision 4): the storyteller fires its own incident at its own
    /// rate; where the home has no powered console, it may fire all the same, and Rimfolk's messenger brings the demand to
    /// the gate (Ransoms.Demand) instead of the game's radio letter, with the game's fee and day to answer. With a powered
    /// console nothing changes. Off with "Ransom" or the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RansomDemand), "CanFireNowSub")]
    public static class Patch_RansomByHand_CanFire
    {
        public static void Postfix(IncidentParms parms, ref bool __result)
        {
            if (__result || !RimfolkPatches.On || !Ransoms.On || !(parms?.target is Map map) || !Homes.IsHome(map)) return;
            try
            {
                if (!CommsConsoleUtility.PlayerHasPoweredCommsConsole(map) && Ransoms.DemandCandidate(map, out _) != null) __result = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the game's demand, can it come", e);
            }
        }
    }

    /// <summary>The game's demand fires at a home with no powered console: their messenger brings it to the gate.</summary>
    [HarmonyPatch(typeof(IncidentWorker_RansomDemand), "TryExecuteWorker")]
    public static class Patch_RansomByHand_Execute
    {
        public static bool Prefix(IncidentParms parms, ref bool __result)
        {
            if (!RimfolkPatches.On || !Ransoms.On || !(parms?.target is Map map) || !Homes.IsHome(map)) return true;
            try
            {
                if (CommsConsoleUtility.PlayerHasPoweredCommsConsole(map)) return true;
                __result = Ransoms.Demand(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the game's demand by messenger", e);
                __result = false;
            }
            return false;
        }
    }
}
