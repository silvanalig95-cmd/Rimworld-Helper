using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Where something is drunk, smoked or eaten (the setting "Drinks and smokes out of the freezer"): when the game
    /// finds no seat near enough and would have someone stand where they picked it up, in a freezer or a store room
    /// with nowhere to sit, they take it to a seat at a table within 40 cells instead, else to a spot just outside that
    /// room. Anywhere else, and wherever they'd sit anyway, the game's own choice stands.
    /// </summary>
    public static class ChewSpots
    {
        public const float SeatRadius = 40f, OutsideRadius = 18f;

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.chewOutOfStores ?? true);

        /// <summary>A freezer, or a room with stores and nowhere to sit (ChewSpotMath.StoreRoom).</summary>
        public static bool StoreRoom(Room room, Map map, Dictionary<Room, bool> seen = null)
        {
            if (room == null || map == null) return false;
            if (seen != null && seen.TryGetValue(room, out bool known)) return known;
            int storage = 0, seats = 0;
            if (!room.PsychologicallyOutdoors && !room.IsHuge)
            {
                foreach (var c in room.Cells)
                {
                    if (map.haulDestinationManager.SlotGroupAt(c) != null) storage++;
                    var e = c.GetEdifice(map);
                    if (e?.def.building != null && e.def.building.isSittable) seats++;
                }
            }
            bool store = ChewSpotMath.StoreRoom(room.PsychologicallyOutdoors || room.IsHuge, room.Temperature, storage, seats);
            if (seen != null) seen[room] = store;
            return store;
        }

        /// <summary>A better place than <paramref name="cell"/> to have it, or false to keep the game's.</summary>
        public static bool Better(Pawn pawn, IntVec3 cell, out IntVec3 better)
        {
            better = IntVec3.Invalid;
            var map = pawn?.Map;
            if (map == null || !pawn.RaceProps.Humanlike || pawn.Drafted || pawn.IsPrisoner || pawn.HostileTo(Faction.OfPlayer)) return false;
            if (Seat(cell, map)) return false;
            var room = cell.GetRoom(map);
            var seen = new Dictionary<Room, bool>();
            if (!StoreRoom(room, map, seen)) return false;
            var chair = GenClosest.ClosestThingReachable(pawn.Position, map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell,
                TraverseParms.For(pawn), SeatRadius, t => SeatFits(t, pawn, map, seen));
            if (chair != null && Toils_Ingest.TryFindFreeSittingSpotOnThing(chair, pawn, out var seat))
            {
                better = seat;
                return true;
            }
            foreach (var c in GenRadial.RadialCellsAround(pawn.Position, OutsideRadius, false))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetDoor(map) != null) continue;
                var r = c.GetRoom(map);
                if (r == null || r == room || StoreRoom(r, map, seen)) continue;
                if (map.haulDestinationManager.SlotGroupAt(c) != null || c.IsForbidden(pawn) || c.GetDangerFor(pawn, map) != Danger.None) continue;
                if (!pawn.CanReserveSittableOrSpot(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                better = c;
                return true;
            }
            return false;
        }

        private static bool Seat(IntVec3 c, Map map)
        {
            var list = c.GetThingList(map);
            for (int i = 0; i < list.Count; i++)
                if (list[i].def.building != null && list[i].def.building.isSittable) return true;
            return false;
        }

        /// <summary>A seat at a table, as the game would take one, and not in a freezer or a store room.</summary>
        private static bool SeatFits(Thing t, Pawn pawn, Map map, Dictionary<Room, bool> seen)
        {
            if (t.def.building == null || !t.def.building.isSittable) return false;
            if (t.Faction != pawn.Faction && pawn.Faction != null && pawn.Faction.IsPlayer) return false;
            if (t.IsForbidden(pawn) || (pawn.IsColonist && t.Position.Fogged(map)) || t.IsBurning() || t.HostileTo(pawn)) return false;
            if (!pawn.CanReserve(t) || !t.IsSociallyProper(pawn) || t.Position.GetDangerFor(pawn, map) != Danger.None) return false;
            if (StoreRoom(t.GetRoom(), map, seen)) return false;
            if (!Toils_Ingest.TryFindFreeSittingSpotOnThing(t, pawn, out var spot)) return false;
            for (int i = 0; i < 4; i++)
            {
                var e = (spot + GenAdj.CardinalDirections[i]).GetEdifice(map);
                if (e != null && e.def.surfaceType == SurfaceType.Eat) return true;
            }
            return false;
        }
    }

    /// <summary>Someone about to drink, smoke or eat standing in a freezer or a store room has it at a table, or outside the room (ChewSpots).</summary>
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TryFindChairOrSpot))]
    public static class Patch_ChewOutOfStores
    {
        private static bool broken;

        public static void Postfix(Pawn pawn, ref IntVec3 cell, ref bool __result)
        {
            if (!__result || broken || !cell.IsValid || !ChewSpots.On) return;
            try
            {
                if (ChewSpots.Better(pawn, cell, out var better)) cell = better;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("drinks out of the freezer", e);
            }
        }
    }
}
