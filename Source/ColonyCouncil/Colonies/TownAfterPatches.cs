using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The town a colony was, when the game makes its map (an attack, a fight the colony joins): the game's base generation
    /// stands down for that map only, and the colony's kept layout is placed instead with its people at home in it
    /// (Colonies/TownLayout.cs, the player's decision 7A). A guarded prefix on the game's scatter step for its settlement
    /// step: <c>GenStep_Settlement</c>, and the Vanilla Expanded Framework's own settlement step (read: KCSG's
    /// GenStep_Settlement derives from the game's and only overrides where it scatters and what it builds), which Vanilla
    /// Base Generation Expanded's layouts give every tribal and outlander people, ours among them; every other scatter
    /// step and every other map pass untouched with one type check. With no layout kept, the game builds as always.
    /// </summary>
    [HarmonyPatch(typeof(GenStep_Scatterer), nameof(GenStep_Scatterer.Generate))]
    public static class Patch_TownLayoutSettlement
    {
        public static bool Prefix(GenStep_Scatterer __instance, Map map)
        {
            if (!(__instance is GenStep_Settlement) || map == null || !RimfolkPatches.On) return true;
            try
            {
                return !TownLayouts.PlaceTown(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the town's map from its kept layout", e);
                // Part of the layout may stand already: never the game's base on top of it.
                return !TownLayouts.Placed(map);
            }
        }
    }

    /// <summary>The game's power step (the settlement's generators and conduits) stands down for a town made from the colony's layout, whose own power lines are kept.</summary>
    [HarmonyPatch(typeof(GenStep_Power), nameof(GenStep_Power.Generate))]
    public static class Patch_TownLayoutPower
    {
        public static bool Prefix(Map map)
        {
            try
            {
                return !(RimfolkPatches.On && TownLayouts.Placed(map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the town's power", e);
                return true;
            }
        }
    }
}
