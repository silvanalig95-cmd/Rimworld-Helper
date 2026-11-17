using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Factions Expanded - Empire's ballroom only where the room is for music (the player, 17 November 2026: "as
    /// soon as I have this drum in the room, it's counted as a ballroom ... It's not even counted as kind of an ideology
    /// room ... it's definitely not a dining room"; "ballroom seems to override temple, which is not correct"). Its room
    /// role scores 10000 for any musical instrument, the Ideology drum among them, unless the room holds a bed, throne or
    /// work table, so it beats the temple (an altar, at least 2000) and the dining room (12 a table). Here it stands down
    /// where an altar makes the room a temple, where dining tables make it a dining room, and where the only instruments
    /// are drums (<see cref="BallroomMath.StandsDown"/>). A room with a harp or piano and recreation is still its ballroom,
    /// for its titles and balls. The setting is on the "Your other mods" page, with the deeper hooks.
    /// </summary>
    public static class BallroomByUse
    {
        public const string PackageId = "oskarpotocki.vfe.empire";

        public static Type WorkerType => AccessTools.TypeByName("VFEEmpire.RoomRoleWorker_Ballroom");

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.ballroomByUse ?? true);

        private static RoomRoleDef worship;
        private static bool worshipLooked;

        /// <summary>The room is a temple, a dining room, or has only drums, so it isn't a ballroom.</summary>
        public static bool StandsDown(Room room, ThingDef placing = null)
        {
            if (room == null) return false;
            bool dining = false;
            int instruments = 0, drums = 0;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var d = things[i]?.def;
                if (d == null || d.category != ThingCategory.Building) continue;
                if (d.surfaceType == SurfaceType.Eat) dining = true;
                if (IsInstrument(d))
                {
                    instruments++;
                    if (IsDrum(d)) drums++;
                }
            }
            if (placing != null && IsInstrument(placing))
            {
                instruments++;
                if (IsDrum(placing)) drums++;
            }
            return BallroomMath.StandsDown(Temple(room), dining, instruments, drums);
        }

        public static bool IsInstrument(ThingDef d) => d?.thingClass != null && typeof(Building_MusicalInstrument).IsAssignableFrom(d.thingClass);

        /// <summary>A drum: Ideology's, or another mod's instrument named a drum.</summary>
        public static bool IsDrum(ThingDef d) => d != null && d.defName.IndexOf("Drum", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The game's own worship room (Ideology's temple) would have the room: an altar of the colony's faith in it.</summary>
        private static bool Temple(Room room)
        {
            if (!worshipLooked)
            {
                worshipLooked = true;
                worship = DefDatabase<RoomRoleDef>.GetNamedSilentFail("WorshipRoom");
            }
            if (worship?.Worker == null) return false;
            try
            {
                return worship.Worker.GetScore(room) > 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ballroom: the temple's score", e);
                return false;
            }
        }
    }

    /// <summary>The ballroom's score, gone where the room is a temple, a dining room or has only drums (Integrations/BallroomByUse.cs).</summary>
    [HarmonyPatch]
    public static class Patch_BallroomByUse
    {
        public static bool Prepare() => TargetMethod() != null;

        public static string StandsDownWhy() => BallroomByUse.WorkerType == null ? "Vanilla Factions Expanded - Empire isn't loaded" : "";

        public static MethodBase TargetMethod() => BallroomByUse.WorkerType == null ? null : AccessTools.Method(BallroomByUse.WorkerType, "GetScore", new[] { typeof(Room) });

        public static void Postfix(Room room, ref float __result)
        {
            if (__result <= 0f || !BallroomByUse.On) return;
            if (BallroomByUse.StandsDown(room)) __result = 0f;
        }
    }

    /// <summary>
    /// The same while an instrument is being placed: the game's note that the room will become a ballroom isn't shown
    /// where it won't (Integrations/BallroomByUse.cs).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_BallroomByUsePlacing
    {
        public static bool Prepare() => TargetMethod() != null;

        public static string StandsDownWhy() => BallroomByUse.WorkerType == null ? "Vanilla Factions Expanded - Empire isn't loaded" : "";

        public static MethodBase TargetMethod() =>
            BallroomByUse.WorkerType == null ? null : AccessTools.Method(BallroomByUse.WorkerType, "GetScoreDeltaIfBuildingPlaced", new[] { typeof(Room), typeof(ThingDef) });

        public static void Postfix(Room room, ThingDef buildingDef, ref float __result)
        {
            if (__result <= 0f || !BallroomByUse.On || !BallroomByUse.IsInstrument(buildingDef)) return;
            if (BallroomByUse.StandsDown(room, buildingDef)) __result = 0f;
        }
    }
}
