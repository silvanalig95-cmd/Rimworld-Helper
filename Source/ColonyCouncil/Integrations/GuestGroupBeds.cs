using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's report (7 November 2026): "why do 4-5 people visitor groups stay in my colony if i only have one
    /// reserved visitor bed?" Hospitality sizes a guest group from its own settings and the years played
    /// (IncidentWorker_VisitorGroup.GetGroupSize), never from the guest beds; its only bed rule is to refuse guests until
    /// there is a guest bed at all. With the setting "Guest groups fit your guest beds" (on), a group of its random size
    /// is cut to the free guest bed places on the map (GuestGroupBedsMath.Size): the ones left out never set off, and with
    /// every guest bed taken no new group comes until one is free (Hospitality's own spawn then gives up, as it does when
    /// nobody is left to send). A size asked for outright (a quest's, a debug action's) is left alone.
    /// </summary>
    public static class GuestGroupBeds
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.guestGroupsFitBeds ?? true) && People.ModActive(Guests.Mod);

        /// <summary>The guest beds' places on the map, and those nobody has claimed.</summary>
        public static (int places, int free) Places(Map map)
        {
            int places = 0, free = 0;
            if (map?.listerBuildings == null) return (0, 0);
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_Bed bed) || !GuestBeds.IsGuestBed(bed) || bed.Destroyed) continue;
                int slots = bed.SleepingSlotsCount;
                places += slots;
                int owned = bed.OwnersForReading?.Count(o => o != null && !o.Dead) ?? 0;
                free += Math.Max(0, slots - owned);
            }
            return (places, free);
        }
    }

    /// <summary>Hospitality's guests to send, cut to the free guest beds (Integrations/GuestGroupBeds.cs).</summary>
    [HarmonyPatch]
    public static class Patch_GuestGroupFitsBeds
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() =>
            GenTypes.GetTypeInAnyAssembly("Hospitality.IncidentWorker_VisitorGroup") is Type t ? AccessTools.Method(t, "GetPawnsToSpawn", new[] { typeof(IncidentParms) }) : null;

        public static void Postfix(IncidentParms __0, ref IEnumerable<Pawn> __result)
        {
            if (!GuestGroupBeds.On || __0 == null || __0.pawnCount > 0 || !(__0.target is Map map) || __result == null) return;
            try
            {
                var group = __result.ToList();
                var (places, free) = GuestGroupBeds.Places(map);
                int size = GuestGroupBedsMath.Size(group.Count, places, free);
                if (size >= group.Count)
                {
                    __result = group;
                    return;
                }
                if (Prefs.DevMode)
                    Log.Message("[Rimfolk] Hospitality's guest group of " + group.Count + " cut to " + size + " (" + free + " of " + places + " guest bed places free).");
                __result = group.Take(size).ToList();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest group fitting the guest beds", e);
            }
        }
    }
}
