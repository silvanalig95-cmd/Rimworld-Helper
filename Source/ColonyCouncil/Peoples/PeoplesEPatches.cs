using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Release E's game hooks (Peoples/PeoplesE.cs), each off with the deeper game hooks (RimfolkPatches.On): the Stormglass
    // Conclave's watch holding a condition of the sky back a day so its warning comes first, its rod drawing the lightning
    // that would strike the open ground near it, and the Mossborn's ledger counting the trees the colony's people fell.

    /// <summary>
    /// A condition of the sky the storyteller sends (not a quest's, not one coming back out of the game's own queue) goes
    /// into the game's incident queue a day on while the Conclave watches that home's sky (GlassWatch.Hold), so the
    /// warning comes first; the storyteller doesn't fire it now.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_GlassHold
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result)
        {
            if (queued || !RimfolkPatches.On || fi?.def == null || !PeoplesEMath.Foreseen(fi.def.defName)) return true;
            try
            {
                if (!GlassWatch.Hold(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the glass holds a condition", e);
                return true;
            }
        }
    }

    /// <summary>
    /// A lightning strike on the open ground within reach of one of the Conclave's rods strikes the rod instead: the bolt
    /// and its flash and sparks there, and no fire (Patch_GlassRodExplosion skips the strike's own blast at the rod).
    /// </summary>
    [HarmonyPatch(typeof(WeatherEvent_LightningStrike), nameof(WeatherEvent_LightningStrike.FireEvent))]
    public static class Patch_GlassRodStrike
    {
        private static readonly AccessTools.FieldRef<WeatherEvent_LightningStrike, IntVec3> Loc = AccessTools.FieldRefAccess<WeatherEvent_LightningStrike, IntVec3>("strikeLoc");

        private static readonly AccessTools.FieldRef<WeatherEvent, Map> MapOf = AccessTools.FieldRefAccess<WeatherEvent, Map>("map");

        public static void Prefix(WeatherEvent_LightningStrike __instance)
        {
            GlassRod.Striking = IntVec3.Invalid;
            GlassRod.StrikingMap = null;
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                var map = MapOf(__instance);
                var def = PeoplesE.RodDef;
                if (map == null || def == null || map.listerThings.ThingsOfDef(def).Count == 0) return;
                var loc = Loc(__instance);
                // Where the game would strike, chosen as the game chooses it: open, standable ground.
                if (!loc.IsValid) loc = CellFinderLoose.RandomCellWith(c => c.Standable(map) && !map.roofGrid.Roofed(c), map);
                if (!loc.IsValid) return;
                var rod = GlassRod.Near(map, loc);
                if (rod == null)
                {
                    Loc(__instance) = loc;
                    return;
                }
                Loc(__instance) = rod.Position;
                GlassRod.Striking = rod.Position;
                GlassRod.StrikingMap = map;
                GlassRod.Struck(map, rod);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the rod draws the lightning", e);
            }
        }

        public static void Finalizer()
        {
            GlassRod.Striking = IntVec3.Invalid;
            GlassRod.StrikingMap = null;
        }
    }

    /// <summary>The blast of a strike the rod took: none, so no fire starts round it.</summary>
    [HarmonyPatch(typeof(GenExplosion), nameof(GenExplosion.DoExplosion))]
    public static class Patch_GlassRodExplosion
    {
        public static bool Prefix(IntVec3 center, Map map) => GlassRod.StrikingMap == null || map != GlassRod.StrikingMap || center != GlassRod.Striking;
    }

    /// <summary>A tree cut down by one of the colony's people (counted before it is gone from the map): the Mossborn's ledger counts it (MossLedger).</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_MossFelled
    {
        public static void Prefix(Plant __instance, Pawn by)
        {
            if (!RimfolkPatches.On || __instance?.def?.plant == null || !__instance.def.plant.IsTree) return;
            try
            {
                MossLedger.Notify_Felled(__instance, by);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: a tree felled", e);
            }
        }
    }
}
