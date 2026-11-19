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

        private static readonly Dictionary<string, RoomRoleDef> byName = new Dictionary<string, RoomRoleDef>();

        /// <summary>A room role by the name the templates use ("Storeroom", "Kitchen"): the def of that name, else the one whose worker is named so.</summary>
        public static RoomRoleDef RoleNamed(string name)
        {
            if (name.NullOrEmpty()) return null;
            if (byName.TryGetValue(name, out var cached)) return cached;
            var def = DefDatabase<RoomRoleDef>.GetNamedSilentFail(name);
            if (def == null)
            {
                string worker = name == "Storeroom" ? "StoreRoom" : name;
                def = DefDatabase<RoomRoleDef>.AllDefsListForReading.FirstOrDefault(r => r.workerClass != null && r.workerClass.Name == "RoomRoleWorker_" + worker);
            }
            byName[name] = def;
            return def;
        }

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

        // ---------------- Rooms by what is in them ----------------

        private struct RoomUse
        {
            public int tick;
            public bool dining, rec;
        }

        /// <summary>A room's reading, kept for a second of game time: the advisor, the wishes and the pressures ask often.</summary>
        private static readonly Dictionary<Room, RoomUse> uses = new Dictionary<Room, RoomUse>();

        private const int UseTicks = 60;

        /// <summary>
        /// The room serves as a dining room: it holds a dining table of the colony's (a building with an eating surface, as
        /// the game's dining room counts them) and nobody sleeps, is nursed, kept or washes there, whatever role the game
        /// shows (a temple, a ballroom, a kitchen with a table; RoomUseMath).
        /// </summary>
        public static bool ServesAsDining(Room room) => room != null && UseOf(room).dining;

        /// <summary>The room serves as a rec room: it holds a recreation building of the colony's (one with a kind of recreation, as the game's rec room counts them), and isn't private.</summary>
        public static bool ServesAsRec(Room room) => room != null && UseOf(room).rec;

        /// <summary>A room where people eat: the game calls it a dining room, or it serves as one.</summary>
        public static bool IsDiningRoom(Room room) => room != null && (DiningRoom != null && room.Role == DiningRoom || ServesAsDining(room));

        /// <summary>A room for recreation: the game calls it a rec room, or it serves as one.</summary>
        public static bool IsRecRoom(Room room) => room != null && (RecRoom != null && room.Role == RecRoom || ServesAsRec(room));

        /// <summary>A room where people eat or relax together.</summary>
        public static bool IsCommonRoom(Room room) => IsDiningRoom(room) || IsRecRoom(room);

        /// <summary>The order a table is looked for in: 0 a room the game calls a dining room, 1 one that serves as one, -1 neither.</summary>
        public static int DiningRank(Room room) => room == null ? -1 : RoomUseMath.Rank(DiningRoom != null && room.Role == DiningRoom, ServesAsDining(room));

        /// <summary>The colony's rooms where people eat: those the game calls a dining room first, then those that serve as one, the largest first in each.</summary>
        public static List<Room> DiningRooms(Map map) => ByUse(map, DiningRoom, ServesAsDining);

        /// <summary>The colony's rooms for recreation: those the game calls a rec room first, then those that serve as one, the largest first in each.</summary>
        public static List<Room> RecRooms(Map map) => ByUse(map, RecRoom, ServesAsRec);

        public static bool AnyDiningRoom(Map map) => AnyRoom(map, IsDiningRoom);

        public static bool AnyRecRoom(Map map) => AnyRoom(map, IsRecRoom);

        /// <summary>
        /// The rooms of a role as Rimfolk reads it: for the dining and rec rooms every room that serves as one
        /// (<see cref="DiningRooms"/>, <see cref="RecRooms"/>); for any other role, the rooms the game gives it.
        /// </summary>
        public static IEnumerable<Room> RoomsFor(Map map, RoomRoleDef role)
        {
            if (role == null) return Enumerable.Empty<Room>();
            if (role == DiningRoom) return DiningRooms(map);
            if (role == RecRoom) return RecRooms(map);
            return RoomsWithRole(map, role);
        }

        private static List<Room> ByUse(Map map, RoomRoleDef role, Func<Room, bool> serves)
        {
            var rooms = new List<Room>();
            var ranks = new List<int>();
            var cells = new List<int>();
            foreach (var r in ColonyRooms(map))
            {
                int rank = RoomUseMath.Rank(role != null && r.Role == role, serves(r));
                if (rank < 0) continue;
                rooms.Add(r);
                ranks.Add(rank);
                cells.Add(r.CellCount);
            }
            var list = new List<Room>(rooms.Count);
            foreach (int i in RoomUseMath.Order(ranks, cells)) list.Add(rooms[i]);
            return list;
        }

        private static RoomUse UseOf(Room room)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (uses.TryGetValue(room, out var u) && now >= u.tick && now - u.tick < UseTicks) return u;
            if (uses.Count > 1024) uses.Clear();
            u = new RoomUse { tick = now };
            try
            {
                if (!room.Dereferenced && room.ProperRoom && !room.IsHuge && !room.PsychologicallyOutdoors && !room.IsDoorway)
                {
                    int tables = 0, games = 0;
                    var things = room.ContainedAndAdjacentThings;
                    for (int i = 0; i < things.Count; i++)
                    {
                        var t = things[i];
                        var d = t?.def;
                        if (d?.building == null || d.category != ThingCategory.Building || d.IsFrame || d.IsBlueprint || t.Faction != Faction.OfPlayer) continue;
                        if (d.surfaceType == SurfaceType.Eat) tables++;
                        if (d.building.joyKind != null) games++;
                    }
                    bool closed = RoomUseMath.Private(room.Role?.defName, room.IsPrisonCell);
                    u.dining = RoomUseMath.ServesAsDining(tables, closed);
                    u.rec = RoomUseMath.ServesAsRec(games, closed);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rooms by what is in them", e);
            }
            uses[room] = u;
            return u;
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
