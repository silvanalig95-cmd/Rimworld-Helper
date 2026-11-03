using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The game's hooks for fields by the weather (Fields/FieldWeather.cs). They only hold sowing back and add to what a
    // field shows: Smart Farming (Owlchemist's, or ReGrowth's copy) checks sowing inside the same method with its own
    // transpiler, and both can stand together; neither needs the other.

    /// <summary>
    /// No sowing job on a cell of a field set to sow by the weather while its crop wouldn't pay: one look at a set of
    /// paused fields, nothing worked out here. It never makes anyone sow (the game's own season check still applies),
    /// and a sowing the player orders by hand goes ahead.
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_GrowerSow), nameof(WorkGiver_GrowerSow.JobOnCell))]
    public static class Patch_FieldWeatherSow
    {
        private static bool broken;

        public static bool Prefix(Pawn pawn, IntVec3 c, bool forced, ref Job __result)
        {
            if (forced || broken || !FieldWeather.SowOn) return true;
            try
            {
                if (!FieldWeather.Paused(pawn, c)) return true;
                __result = null;
                return false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("fields by the weather: sowing", e);
                return true;
            }
        }
    }

    /// <summary>The field's "By the weather" switch, and its frost harvest button while one is open.</summary>
    [HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.GetGizmos))]
    public static class Patch_FieldWeatherGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Zone_Growing __instance)
        {
            foreach (var g in __result) yield return g;
            List<Gizmo> more = null;
            if (RimfolkPatches.On)
                try
                {
                    more = FieldWeather.Of(__instance?.Map)?.GizmosFor(__instance);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fields by the weather: the buttons", e);
                }
            if (more != null)
                foreach (var g in more)
                    yield return g;
        }
    }

    /// <summary>The field's outlook by the weather, and a frost harvest open for it, in its inspect pane.</summary>
    [HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.GetInspectString))]
    public static class Patch_FieldWeatherInspect
    {
        public static void Postfix(Zone_Growing __instance, ref string __result)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                var fw = FieldWeather.Of(__instance?.Map);
                if (fw != null) __result = fw.Inspect(__instance, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fields by the weather: the field's words", e);
            }
        }
    }

    /// <summary>A plant a frost harvest marked is harvested: counted for the step's check after the frost.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_FieldWeatherCollected
    {
        public static void Prefix(Plant __instance)
        {
            if (!FieldWeather.Watching) return;
            try
            {
                FieldWeather.Collected(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fields by the weather: a harvest counted", e);
            }
        }
    }
}
