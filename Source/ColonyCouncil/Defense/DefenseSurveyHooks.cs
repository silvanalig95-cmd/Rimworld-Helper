using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // When the ways in may have changed: a wall, door, turret, trap or cover finished or gone near a defense line or a
    // gap the survey found. Each only looks at the one building and marks the survey to be made again (at most once a
    // game hour, the next time something asks for it); nothing in the game's own outcome changes.

    /// <summary>A building was finished: the survey looks again if it stands by a line or a gap.</summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_DefenseBuilt
    {
        public class Site
        {
            public Map map;
            public IntVec3 cell;
            public ThingDef def;
        }

        public static void Prefix(Frame __instance, out Site __state)
        {
            __state = null;
            try
            {
                if (RimfolkPatches.On && __instance?.Map != null && __instance.def?.entityDefToBuild is ThingDef td && td.building != null)
                    __state = new Site { map = __instance.Map, cell = __instance.Position, def = td };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: a building finished (before)", e);
            }
        }

        public static void Postfix(Site __state)
        {
            if (__state?.map == null) return;
            try
            {
                DefenseSurvey.Notify_Changed(__state.map, __state.cell, __state.def, Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: a building finished", e);
            }
        }
    }

    /// <summary>A building is going (destroyed, taken down, uninstalled, or rock mined out): the survey looks again if it stood by a line or a gap.</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
    public static class Patch_DefenseGone
    {
        public static void Prefix(Building __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                var map = __instance.Map;
                if (map == null || !map.IsPlayerHome) return;
                DefenseSurvey.Notify_Changed(map, __instance.Position, __instance.def, __instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: a building gone", e);
            }
        }
    }
}
