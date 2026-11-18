using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A brewery at home in the kitchen as well as the workshop (the player, 18 November 2026: "the brewery gets a debuff
    /// once it's not in the workshop, however, i think the brewery should also be able to be placed in the kitchen"). The
    /// game (1.6) names one room for each bench: the brewery's is the workshop, and outside it the bench works at 80%
    /// (StatPart_WorkTableRoomRole). Brewing is Cooking work, done by the cooks beside the food stores, so a cooking bench
    /// of the workshop (<see cref="BreweryKitchenMath.CookingBench"/>: the brewery, and any mod's bench whose work is all
    /// cooking) works at full speed in a kitchen too; and beside a stove it counts toward the kitchen, not the workshop,
    /// so it never turns the kitchen into a workshop and slows its stoves (the game: a kitchen 28 a stove, a workshop 27 a
    /// bench, the workshop first on a tie). Behind the deeper hooks and its own setting.
    /// </summary>
    public static class BreweryKitchen
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.breweryInKitchen ?? true);

        private static readonly Dictionary<ThingDef, bool> cooking = new Dictionary<ThingDef, bool>();

        /// <summary>A bench of the workshop whose work is cooking (cached by def).</summary>
        public static bool CookingBench(ThingDef d)
        {
            if (d?.building == null || RoomUtil.Workshop == null || d.building.workTableRoomRole != RoomUtil.Workshop) return false;
            if (cooking.TryGetValue(d, out bool yes)) return yes;
            try
            {
                yes = BreweryKitchenMath.CookingBench(d.AllRecipes?.Select(r => r?.workSkill?.defName) ?? Enumerable.Empty<string>());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the brewery in the kitchen: the recipes of " + d.defName, e);
                yes = false;
            }
            cooking[d] = yes;
            return yes;
        }

        /// <summary>The game would call the room a kitchen by what is in it: its kitchen score above nothing.</summary>
        public static bool KitchenBenchIn(Room room)
        {
            var kitchen = RoomUtil.Kitchen;
            if (room == null || kitchen?.Worker == null) return false;
            try
            {
                return kitchen.Worker.GetScore(room) > 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the brewery in the kitchen: the kitchen's score", e);
                return false;
            }
        }

        /// <summary>A cooking bench standing in a room the game calls a kitchen.</summary>
        public static bool AtHome(ThingDef d, Room room) => room != null && RoomUtil.Kitchen != null && room.Role == RoomUtil.Kitchen && CookingBench(d);
    }

    /// <summary>No room penalty for a cooking bench in a kitchen: its speed, its line in the stat's breakdown (Building/BreweryKitchen.cs).</summary>
    [HarmonyPatch(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.Applies))]
    public static class Patch_BreweryKitchenSpeed
    {
        public static void Postfix(Thing __0, ref bool __result)
        {
            if (!__result || __0 == null || !BreweryKitchen.On) return;
            if (BreweryKitchen.AtHome(__0.def, __0.GetRoom())) __result = false;
        }
    }

    /// <summary>The same while placing one: no note that it would work slower in the kitchen (Building/BreweryKitchen.cs).</summary>
    [HarmonyPatch(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.WouldApplyToBuildingIfPlaced))]
    public static class Patch_BreweryKitchenPlacing
    {
        public static void Postfix(ThingDef __0, Map __1, IntVec3 __2, ref bool __result)
        {
            if (!__result || __0 == null || __1 == null || !BreweryKitchen.On || !__2.InBounds(__1)) return;
            var room = __2.GetRoom(__1);
            if (BreweryKitchen.AtHome(__0, room) || BreweryKitchen.CookingBench(__0) && BreweryKitchen.KitchenBenchIn(room)) __result = false;
        }
    }

    /// <summary>Beside a stove, a cooking bench counts toward the kitchen, not the workshop (Building/BreweryKitchen.cs).</summary>
    [HarmonyPatch(typeof(RoomRoleWorker_Workshop), nameof(RoomRoleWorker_Workshop.GetScore))]
    public static class Patch_BreweryKitchenRoom
    {
        public static void Postfix(Room __0, ref float __result)
        {
            if (__result <= 0f || __0 == null || !BreweryKitchen.On) return;
            var workshop = RoomUtil.Workshop;
            int benches = 0, cooks = 0;
            var things = __0.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var d = things[i]?.def;
                if (d?.building == null || d.building.workTableRoomRole != workshop) continue;
                benches++;
                if (BreweryKitchen.CookingBench(d)) cooks++;
            }
            if (cooks == 0) return;
            __result = BreweryKitchenMath.WorkshopScore(__result, benches, cooks, BreweryKitchen.KitchenBenchIn(__0));
        }
    }

    /// <summary>The same while placing one beside a stove: the room isn't shown as becoming a workshop (Building/BreweryKitchen.cs).</summary>
    [HarmonyPatch(typeof(RoomRoleWorker_Workshop), nameof(RoomRoleWorker_Workshop.GetScoreDeltaIfBuildingPlaced))]
    public static class Patch_BreweryKitchenRoomPlacing
    {
        public static void Postfix(Room __0, ThingDef __1, ref float __result)
        {
            if (__result <= 0f || __0 == null || !BreweryKitchen.On) return;
            if (BreweryKitchen.CookingBench(__1) && BreweryKitchen.KitchenBenchIn(__0)) __result = 0f;
        }
    }
}
