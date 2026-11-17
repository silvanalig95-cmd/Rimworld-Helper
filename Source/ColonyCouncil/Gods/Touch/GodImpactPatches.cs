using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// While the wind is dead after the calm (GodImpact, proposal 38), a fire doesn't spread from where it burns. Nothing is
    /// looked at outside those hours.
    /// </summary>
    [HarmonyPatch(typeof(Fire), "TrySpread")]
    public static class Patch_GodStillAir
    {
        public static bool Prefix(Fire __instance)
        {
            if (GodImpact.StillUntil < 0 || !RimfolkPatches.On) return true;
            try
            {
                return !GodImpact.StillOn(__instance.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: fire in the still air", e);
                return true;
            }
        }
    }

    /// <summary>
    /// The long noon (GodImpact, proposal 12): until sundown the colony's solar panels give their most, as much as their roof
    /// lets them (the game's own full output; the night's nothing stays nothing after sundown).
    /// </summary>
    [HarmonyPatch(typeof(CompPowerPlantSolar), "DesiredPowerOutput", MethodType.Getter)]
    public static class Patch_GodLongNoon
    {
        public static void Postfix(CompPowerPlantSolar __instance, ref float __result)
        {
            if (GodImpact.NoonUntil < 0 || !RimfolkPatches.On) return;
            try
            {
                var t = __instance?.parent;
                if (t == null || !t.Spawned || !GodImpact.NoonOn(t.Map)) return;
                var cells = t.OccupiedRect().Cells.ToList();
                float open = cells.Count == 0 ? 1f : cells.Count(c => !c.Roofed(t.Map)) / (float)cells.Count;
                __result = Math.Max(__result, -__instance.Props.PowerConsumption * open);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the long noon on the panels", e);
            }
        }
    }

    /// <summary>
    /// The machine that ran on its own hour after it was switched off (GodImpact, proposal 33): that hour it draws no power
    /// and runs whether or not the line has any. Nothing is looked at while no machine is running its free hour.
    /// </summary>
    [HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOutput), MethodType.Getter)]
    public static class Patch_GodFreeHourPower
    {
        public static void Postfix(CompPowerTrader __instance, ref float __result)
        {
            if (GodImpact.Free == null || __result >= 0f) return;
            if (GodImpact.FreeNow(__instance.parent)) __result = 0f;
        }
    }

    /// <summary>The machine running its free hour is on, power or none (GodImpact, proposal 33).</summary>
    [HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Getter)]
    public static class Patch_GodFreeHourOn
    {
        public static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            if (GodImpact.Free == null || __result) return;
            if (GodImpact.FreeNow(__instance.parent)) __result = true;
        }
    }

    /// <summary>
    /// The door kept on a night with no moon (GodImpact, proposal 15): a threat that would come at the home while its keeper
    /// stands at the door waits on the game's own queue until a little after their night is over. A firing another prefix
    /// has already held is left to it.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_GodNightWard
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (queued || fi?.def == null || !RimfolkPatches.On || !Gods.On) return true;
            try
            {
                if (!GodImpact.HoldTillDawn(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the door kept", e);
                return true;
            }
        }
    }
}
