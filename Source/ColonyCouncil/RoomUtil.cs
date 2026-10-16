using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Room and colonist helpers shared by wishes, advice and ambitions.</summary>
    public static class RoomUtil
    {
        private static readonly Dictionary<Type, RoomRoleDef> roleCache = new Dictionary<Type, RoomRoleDef>();

        /// <summary>Finds a room role by its worker class, so it works whatever the defName.</summary>
        public static RoomRoleDef RoleByWorker(Type workerType)
        {
            if (roleCache.TryGetValue(workerType, out var cached)) return cached;
            var def = DefDatabase<RoomRoleDef>.AllDefsListForReading.FirstOrDefault(r => r.workerClass == workerType);
            roleCache[workerType] = def;
            return def;
        }

        public static RoomRoleDef Kitchen => RoleByWorker(typeof(RoomRoleWorker_Kitchen));
        public static RoomRoleDef DiningRoom => RoleByWorker(typeof(RoomRoleWorker_DiningRoom));
        public static RoomRoleDef RecRoom => RoleByWorker(typeof(RoomRoleWorker_RecRoom));
        public static RoomRoleDef Barn => RoleByWorker(typeof(RoomRoleWorker_Barn));
        public static RoomRoleDef Workshop => RoomRoleDefOf.Workshop ?? RoleByWorker(typeof(RoomRoleWorker_Workshop));
        public static RoomRoleDef Laboratory => RoomRoleDefOf.Laboratory ?? RoleByWorker(typeof(RoomRoleWorker_Laboratory));
        public static RoomRoleDef Hospital => RoomRoleDefOf.Hospital ?? RoleByWorker(typeof(RoomRoleWorker_Hospital));

        public static IEnumerable<Room> ColonyRooms(Map map)
        {
            if (map?.regionGrid == null) yield break;
            foreach (var room in map.regionGrid.AllRooms)
            {
                if (room == null || room.Dereferenced || room.PsychologicallyOutdoors || room.Fogged || room.IsDoorway) continue;
                yield return room;
            }
        }

        public static bool AnyRoom(Map map, Func<Room, bool> pred)
        {
            foreach (var r in ColonyRooms(map))
                if (pred(r)) return true;
            return false;
        }

        public static IEnumerable<Room> RoomsWithRole(Map map, RoomRoleDef role)
        {
            if (role == null) yield break;
            foreach (var r in ColonyRooms(map))
                if (r.Role == role) yield return r;
        }

        public static bool RoomContains(Room room, Func<ThingDef, bool> pred)
        {
            if (room == null) return false;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t.def.category == ThingCategory.Building && t.Faction == Faction.OfPlayer && room.ContainsCell(t.Position) && pred(t.def))
                    return true;
            }
            return false;
        }

        public static int RoomCount(Room room, Func<ThingDef, bool> pred)
        {
            if (room == null) return 0;
            int n = 0;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t.def.category == ThingCategory.Building && !t.def.IsFrame && room.ContainsCell(t.Position) && pred(t.def)) n++;
            }
            return n;
        }

        /// <summary>A private bedroom the colonist owns, if any.</summary>
        public static Room PrivateRoom(Pawn p) => p?.ownership?.OwnedRoom;

        public static Room BedRoom(Pawn p)
        {
            var bed = p?.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return null;
            return bed.GetRoom();
        }

        public static float Impressiveness(Room room) => room == null ? 0f : room.GetStat(RoomStatDefOf.Impressiveness);

        public static float Space(Room room) => room == null ? 0f : room.GetStat(RoomStatDefOf.Space);

        /// <summary>The next quality stage up for a room stat, or -1 if already at the top.</summary>
        public static float NextStageThreshold(RoomStatDef stat, float value)
        {
            if (stat?.scoreStages == null) return -1f;
            int idx = stat.GetScoreStageIndex(value);
            if (idx < 0 || idx + 1 >= stat.scoreStages.Count) return -1f;
            return stat.scoreStages[idx + 1].minScore;
        }

        public static string StageLabel(RoomStatDef stat, float value) => stat?.GetScoreStage(value)?.label ?? value.ToString("0");

        /// <summary>Is there a cold room (below 0 °C) holding storage? That's a freezer. So is a working fridge from a mod.</summary>
        public static bool HasFreezer(Map map)
        {
            if (map == null) return false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!Avail.IsFridge(b.def)) continue;
                var power = b.TryGetComp<CompPowerTrader>();
                if (power == null || power.PowerOn) return true;
            }
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Stockpile)) continue;
                foreach (var c in zone.Cells)
                {
                    var room = c.GetRoom(map);
                    if (room != null && !room.PsychologicallyOutdoors && room.Temperature < 0f) return true;
                    break;
                }
            }
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_Storage)
                {
                    var room = b.GetRoom();
                    if (room != null && !room.PsychologicallyOutdoors && room.Temperature < 0f) return true;
                }
            }
            return false;
        }

        /// <summary>A grow zone that is mostly roofed and inside a room: a greenhouse.</summary>
        public static bool HasGreenhouse(Map map, int minCells = 12)
        {
            if (map == null) return false;
            if (Avail.ColonyHas(map, Avail.IsHydroponics)) return true;
            // A roofed grow zone only works with a sun lamp; without one plants sit in the dark.
            if (!Avail.ColonyHas(map, Avail.IsSunLamp)) return false;
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing)) continue;
                int roofed = 0;
                foreach (var c in zone.Cells)
                {
                    if (c.Roofed(map))
                    {
                        var room = c.GetRoom(map);
                        if (room != null && !room.PsychologicallyOutdoors) roofed++;
                    }
                }
                if (roofed >= minCells) return true;
            }
            return false;
        }

        public static bool HasActiveThought(Pawn p, ThoughtDef def)
        {
            if (def == null || p?.needs?.mood?.thoughts == null) return false;
            tmpThoughts.Clear();
            p.needs.mood.thoughts.GetAllMoodThoughts(tmpThoughts);
            for (int i = 0; i < tmpThoughts.Count; i++)
                if (tmpThoughts[i].def == def) return true;
            return false;
        }

        public static bool HasActiveThought(Pawn p, string defName) =>
            HasActiveThought(p, DefDatabase<ThoughtDef>.GetNamedSilentFail(defName));

        private static readonly List<Thought> tmpThoughts = new List<Thought>();

        public static int ColonistCount(Map map) => map?.mapPawns?.FreeColonistsSpawnedCount ?? 0;

        public static int ColonyAnimalCount(Map map)
        {
            if (map == null) return 0;
            int n = 0;
            foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                if (p.RaceProps.Animal) n++;
            return n;
        }

        public static ExpectationDef Expectation(Pawn p)
        {
            try
            {
                return ExpectationsUtility.CurrentExpectationFor(p);
            }
            catch
            {
                return null;
            }
        }

        public static int ExpectationOrder(Pawn p) => Expectation(p)?.order ?? 2;
    }
}
