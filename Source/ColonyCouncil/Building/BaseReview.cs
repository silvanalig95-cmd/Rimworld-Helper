using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing the review found: what, how much it matters, where to look, and what can be done at a click.</summary>
    public class BaseFinding
    {
        public string key;
        /// <summary>Which part of the page it goes under: "rooms", "walks", "doors", "walls", "works".</summary>
        public string group;
        /// <summary>How much it matters (0..1, like the advisor's score).</summary>
        public float score;
        public string title, detail;
        public IntVec3 look = IntVec3.Invalid;
        /// <summary>What a fix takes down, if there is one.</summary>
        public List<Thing> things;
        public string fixLabel;
        public Action fix;

        public AdviceLevel Level => (AdviceLevel)NextStepsMath.LevelOf(score);
    }

    /// <summary>
    /// The base looked over (the Base page): rooms that belong together standing far apart and rooms that belong
    /// apart standing close, long walks to meals, doors that separate nothing, stubs of wall, conduits and pipes that
    /// lead nowhere, a freezer door that opens into the warm, and the stores (<see cref="StorageReview"/>). Rules in
    /// <see cref="BaseReviewMath"/>. Found once an hour for the plan's proposals, and on the page when asked.
    /// </summary>
    public static class BaseReview
    {
        private class Cache
        {
            public int tick = -999999;
            public List<BaseFinding> findings = new List<BaseFinding>();
        }

        private static readonly Dictionary<int, Cache> caches = new Dictionary<int, Cache>();

        public static List<BaseFinding> For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new List<BaseFinding>();
            if (!caches.TryGetValue(map.uniqueID, out var c)) caches[map.uniqueID] = c = new Cache();
            int now = Find.TickManager.TicksGame;
            if (force || now - c.tick > Sched.HourTicks || now < c.tick)
            {
                c.tick = now;
                try
                {
                    c.findings = Compute(map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("base review", e);
                    c.findings = new List<BaseFinding>();
                }
            }
            return c.findings;
        }

        public static BaseFinding Finding(Map map, string key) => For(map).FirstOrDefault(f => f.key == key);

        // ---------------- Rooms ----------------

        private class RoomInfo
        {
            public Room room;
            public string role;
            public (float x, float z) mid;
            public string label;
        }

        /// <summary>The role the review reckons by: a cold room with storage is a freezer, barracks sleep like bedrooms, prison barracks like a cell.</summary>
        private static string RoleName(Room room, Map map)
        {
            var role = room.Role;
            if (role == null || role == RoomRoleDefOf.None) return null;
            if (room.Temperature <= BaseReviewMath.FreezerTemp && HasStorage(room, map)) return "Freezer";
            string name = role.defName;
            if (role == RoomRoleDefOf.Barracks) return "Bedroom";
            if (role == RoomRoleDefOf.PrisonBarracks) return "PrisonCell";
            if (role == RoomUtil.Workshop) return "Workshop";
            if (role == RoomUtil.Kitchen) return "Kitchen";
            if (role == RoomUtil.DiningRoom) return "DiningRoom";
            if (role == RoomUtil.RecRoom) return "RecRoom";
            if (role == RoomUtil.Barn) return "Barn";
            if (role == RoomUtil.Hospital) return "Hospital";
            if (role.Worker is RoomRoleWorker_StoreRoom) return "StoreRoom";
            return name;
        }

        private static bool HasStorage(Room room, Map map)
        {
            foreach (var c in room.Cells)
                if (map.zoneManager.ZoneAt(c) is Zone_Stockpile) return true;
            foreach (var t in room.ContainedAndAdjacentThings)
                if (t is Building_Storage s && room.Cells.Contains(s.Position)) return true;
            return false;
        }

        private static string RoleLabel(string role)
        {
            if (role == "Freezer") return "CC_Base_Freezer".Translate();
            if (role == "Gate") return "CC_Base_Gate".Translate();
            if (role == "Fields") return "CC_Base_Fields".Translate();
            var def = RoomUtil.RoleNamed(role);
            return def != null ? def.label : role.ToLowerInvariant();
        }

        private static List<RoomInfo> Rooms(Map map)
        {
            var list = new List<RoomInfo>();
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (room.CellCount > 900 || room.TouchesMapEdge) continue;
                string role = RoleName(room, map);
                if (role == null) continue;
                var mid = room.ExtentsClose.CenterCell;
                list.Add(new RoomInfo { room = room, role = role, mid = (mid.x, mid.z), label = role == "Freezer" ? RoleLabel(role) : room.GetRoomRoleLabel() });
            }
            return list;
        }

        // ---------------- The review ----------------

        private static List<BaseFinding> Compute(Map map)
        {
            var findings = new List<BaseFinding>();
            var rooms = Rooms(map);
            var byRole = rooms.GroupBy(r => r.role).ToDictionary(g => g.Key, g => g.ToList());
            List<(float x, float z)> Mids(string role)
            {
                if (role == "Gate")
                {
                    var gate = RoomNeighbours.GateCell(map);
                    return gate.IsValid ? new List<(float, float)> { (gate.x, gate.z) } : new List<(float, float)>();
                }
                if (role == "Fields")
                {
                    var fields = RoomNeighbours.FieldCells(map);
                    return fields == null ? new List<(float, float)>() : fields.Select(c => ((float)c.x, (float)c.z)).ToList();
                }
                return byRole.TryGetValue(role, out var l) ? l.Select(r => r.mid).ToList() : new List<(float, float)>();
            }

            // Rooms that belong together, far apart.
            foreach (var rule in BaseReviewMath.NearRules)
            {
                var a = Mids(rule.a);
                var b = Mids(rule.b);
                var far = BaseReviewMath.TooFar(rule, a, b);
                if (far.Count == 0) continue;
                // One finding per rule, about the worst case; the rest in the detail.
                var worst = far.OrderByDescending(f => f.distance).First();
                var roomA = byRole[rule.a][worst.a];
                string other = RoleLabel(rule.b);
                string detail = ("CC_Base_Far_" + rule.key).Translate(roomA.label.Named("A"), other.Named("B"), ((int)worst.distance).Named("D")).Resolve();
                if (far.Count > 1) detail += " " + "CC_Base_FarMore".Translate(far.Count - 1).Resolve();
                findings.Add(new BaseFinding
                {
                    key = "far:" + rule.key, group = "rooms", score = BaseReviewMath.FarScore(rule, worst.distance),
                    title = "CC_Base_Far".Translate(roomA.label.Named("A"), other.Named("B")).Resolve().CapitalizeFirst(), detail = detail,
                    look = roomA.room.ExtentsClose.CenterCell
                });
            }

            // Rooms that belong apart, close together.
            foreach (var rule in BaseReviewMath.ApartRules)
            {
                var a = Mids(rule.a);
                var b = Mids(rule.b);
                var close = BaseReviewMath.TooClose(rule, a, b);
                if (close.Count == 0) continue;
                var first = close.OrderBy(c => c.distance).First();
                var roomA = byRole[rule.a][first.a];
                var roomB = byRole[rule.b][first.b];
                int roomsA = close.Select(c => c.a).Distinct().Count();
                findings.Add(new BaseFinding
                {
                    key = "close:" + rule.key, group = "rooms", score = BaseReviewMath.CloseScore(rule, roomsA),
                    title = "CC_Base_Close".Translate(roomsA.Named("N"), roomA.label.Named("A"), roomB.label.Named("B")).Resolve().CapitalizeFirst(),
                    detail = ("CC_Base_Close_" + rule.key).Translate(roomsA.Named("N"), roomB.label.Named("B")).Resolve(),
                    look = roomA.room.ExtentsClose.CenterCell
                });
            }

            // Long walks to meals.
            float walk = BaseReviewMath.MealWalk(Mids("Bedroom"), Mids("DiningRoom"));
            if (walk > BaseReviewMath.LongMealWalk)
            {
                var farthest = byRole["Bedroom"].OrderByDescending(r =>
                {
                    BaseReviewMath.Nearest(r.mid, Mids("DiningRoom"), out float d);
                    return d;
                }).First();
                findings.Add(new BaseFinding
                {
                    key = "mealwalk", group = "walks", score = Mathf.Min(0.4f, 0.15f + (walk - BaseReviewMath.LongMealWalk) / 100f),
                    title = "CC_Base_MealWalk".Translate().Resolve(), detail = "CC_Base_MealWalk_Detail".Translate(((int)walk).Named("D"), byRole["Bedroom"].Count.Named("N")).Resolve(),
                    look = farthest.room.ExtentsClose.CenterCell
                });
            }

            Doors(map, findings);
            Walls(map, findings);
            Conduits(map, findings);
            Pipes(map, findings);
            Freezers(map, rooms, findings);
            try
            {
                findings.AddRange(StorageReview.Findings(map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storage review", e);
            }
            findings.Sort((x, y) => y.score.CompareTo(x.score));
            return findings;
        }

        // ---------------- Doors ----------------

        private static void Doors(Map map, List<BaseFinding> findings)
        {
            var pointless = new List<Building>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_Door door) || door.def.size != IntVec2.One) continue;
                int open = 0;
                var sides = new List<Room>();
                var indoors = new List<bool>();
                for (int i = 0; i < 4; i++)
                {
                    var c = door.Position + GenAdj.CardinalDirections[i];
                    if (!c.InBounds(map)) continue;
                    var ed = c.GetEdifice(map);
                    if (ed != null && (ed.def.IsWall || ed.def.IsDoor || TemplatePlanner.IsWallLike(ed.def))) continue;
                    if (!c.Walkable(map)) continue;
                    open++;
                    var room = c.GetRoom(map);
                    sides.Add(room);
                    indoors.Add(room != null && !room.PsychologicallyOutdoors);
                }
                if (sides.Count < 2) continue;
                int idA = sides[0]?.ID ?? -1, idB = sides[1]?.ID ?? -1;
                if (BaseReviewMath.DoorPointless(idA, idB, indoors[0], indoors[1], open)) pointless.Add(door);
            }
            if (pointless.Count == 0) return;
            findings.Add(new BaseFinding
            {
                key = "doors", group = "doors", score = BaseReviewMath.TidyScore(pointless.Count, 0.06f),
                title = "CC_Base_Doors".Translate(pointless.Count).Resolve(), detail = "CC_Base_Doors_Detail".Translate().Resolve(),
                look = pointless[0].Position, things = pointless.Cast<Thing>().ToList(),
                fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fix = () => Deconstruct(map, pointless.Cast<Thing>().ToList())
            });
        }

        // ---------------- Walls ----------------

        private static void Walls(Map map, List<BaseFinding> findings)
        {
            var walls = new Dictionary<(int x, int z), Building>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b.def.IsWall && b.def.size == IntVec2.One) walls[(b.Position.x, b.Position.z)] = b;
            if (walls.Count == 0) return;
            // A wall separates something when the open cells around it belong to more than one room (or a room and the open).
            var separates = new Dictionary<(int x, int z), bool>();
            bool Separates((int x, int z) c)
            {
                if (separates.TryGetValue(c, out bool v)) return v;
                var rooms = new HashSet<int>();
                for (int i = 0; i < 4; i++)
                {
                    var n = new IntVec3(c.x, 0, c.z) + GenAdj.CardinalDirections[i];
                    if (!n.InBounds(map)) continue;
                    var ed = n.GetEdifice(map);
                    if (ed != null && (ed.def.IsWall || TemplatePlanner.IsWallLike(ed.def))) continue;
                    if (ed != null && ed.def.IsDoor)
                    {
                        // A door in the run makes it a proper wall.
                        rooms.Add(-2);
                        rooms.Add(-3);
                        continue;
                    }
                    var room = n.GetRoom(map);
                    rooms.Add(room == null ? -1 : room.ID);
                }
                v = rooms.Count >= 2;
                separates[c] = v;
                return v;
            }
            var stubs = BaseReviewMath.Stubs(walls.Keys.ToList(), Separates);
            // Not a stub: wall cells with something hung on them or a roof that would fall; those are the player's to judge.
            var things = new List<Thing>();
            int runs = 0;
            foreach (var run in stubs)
            {
                var cells = run.Select(c => walls[c]).ToList();
                if (cells.Any(w => HoldsSomething(w, map))) continue;
                runs++;
                things.AddRange(cells);
            }
            if (things.Count == 0) return;
            findings.Add(new BaseFinding
            {
                key = "stubs", group = "walls", score = BaseReviewMath.TidyScore(runs, 0.04f, 0.2f),
                title = "CC_Base_Stubs".Translate(runs, things.Count).Resolve(), detail = "CC_Base_Stubs_Detail".Translate().Resolve(),
                look = things[0].Position, things = things,
                fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fix = () => Deconstruct(map, things)
            });
        }

        /// <summary>A wall with a lamp or anything else hung on it, or one holding up a roof no other wall reaches.</summary>
        private static bool HoldsSomething(Building wall, Map map)
        {
            foreach (var t in wall.Position.GetThingList(map))
                if (t != wall && t.def.building != null && t.def.building.isAttachment) return true;
            for (int i = 0; i < 4; i++)
            {
                var n = wall.Position + GenAdj.CardinalDirections[i];
                if (!n.InBounds(map)) continue;
                foreach (var t in n.GetThingList(map))
                    if (t.def.building != null && t.def.building.isAttachment && t.Rotation.FacingCell + n == wall.Position) return true;
            }
            if (!wall.Position.Roofed(map)) return false;
            // The roof around it: another support within reach of every roofed cell this one holds.
            foreach (var c in GenRadial.RadialCellsAround(wall.Position, RoofCollapseUtility.RoofMaxSupportDistance, true))
            {
                if (!c.InBounds(map) || !c.Roofed(map) || c.GetRoof(map).isNatural) continue;
                bool other = false;
                foreach (var s in GenRadial.RadialCellsAround(c, RoofCollapseUtility.RoofMaxSupportDistance, true))
                {
                    if (s == wall.Position || !s.InBounds(map)) continue;
                    var ed = s.GetEdifice(map);
                    if (ed != null && ed.def.holdsRoof)
                    {
                        other = true;
                        break;
                    }
                }
                if (!other) return true;
            }
            return false;
        }

        // ---------------- Conduits ----------------

        private static bool IsConduit(Thing t) => t?.def?.building != null && !t.def.building.isEdifice && t.TryGetComp<CompPower>()?.Props?.transmitsPower == true;

        private static void Conduits(Map map, List<BaseFinding> findings)
        {
            var nets = map.powerNetManager?.AllNetsListForReading;
            if (nets == null) return;
            var dead = new List<Thing>();
            var orphan = new List<Thing>();
            int runs = 0;
            foreach (var net in nets)
            {
                var conduits = new Dictionary<(int x, int z), Thing>();
                var others = new List<CompPower>();
                foreach (var t in net.transmitters)
                {
                    if (t?.parent == null || !t.parent.Spawned) continue;
                    if (IsConduit(t.parent)) conduits[(t.parent.Position.x, t.parent.Position.z)] = t.parent;
                    else others.Add(t);
                }
                if (conduits.Count == 0) continue;
                bool anyUser = net.connectors.Count > 0 || net.powerComps.Count > 0 || net.batteryComps.Count > 0 || others.Count > 0;
                if (!anyUser)
                {
                    orphan.AddRange(conduits.Values);
                    continue;
                }
                // A conduit is used when something draws from it, or a powered building stands on or beside it, or
                // something is still being built there.
                bool Used((int x, int z) c)
                {
                    var t = conduits[c];
                    var p = t.TryGetComp<CompPower>();
                    if (p?.connectChildren != null && p.connectChildren.Count > 0) return true;
                    var cell = t.Position;
                    foreach (var th in cell.GetThingList(map))
                        if (th != t && ((th.TryGetComp<CompPower>() != null && !IsConduit(th)) || th is Blueprint || th is Frame)) return true;
                    for (int i = 0; i < 4; i++)
                    {
                        var n = cell + GenAdj.CardinalDirections[i];
                        if (!n.InBounds(map)) continue;
                        foreach (var th in n.GetThingList(map))
                            if ((th.TryGetComp<CompPower>() != null && !IsConduit(th)) || th is Blueprint || th is Frame) return true;
                    }
                    return false;
                }
                foreach (var run in BaseReviewMath.DeadEnds(conduits.Keys.ToList(), Used))
                {
                    runs++;
                    dead.AddRange(run.Select(c => conduits[c]));
                }
            }
            if (dead.Count > 0)
                findings.Add(new BaseFinding
                {
                    key = "conduits", group = "works", score = BaseReviewMath.TidyScore(runs, 0.03f, 0.2f),
                    title = "CC_Base_Conduits".Translate(dead.Count, runs).Resolve(), detail = "CC_Base_Conduits_Detail".Translate().Resolve(),
                    look = dead[0].Position, things = dead, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fix = () => Deconstruct(map, dead)
                });
            if (orphan.Count > 0)
                findings.Add(new BaseFinding
                {
                    key = "orphanconduits", group = "works", score = BaseReviewMath.TidyScore(orphan.Count, 0.02f, 0.15f),
                    title = "CC_Base_OrphanConduits".Translate(orphan.Count).Resolve(), detail = "CC_Base_OrphanConduits_Detail".Translate().Resolve(),
                    look = orphan[0].Position, things = orphan, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fix = () => Deconstruct(map, orphan)
                });
        }

        // ---------------- Pipes (Dubs Bad Hygiene, Vanilla Expanded pipe nets) ----------------

        /// <summary>Which pipe network a thing is part of, as a key ("dbh:Sewage", "ve:Chemfuel"), or null.</summary>
        private static string PipeKey(Thing t)
        {
            var comps = (t as ThingWithComps)?.AllComps;
            if (comps == null) return null;
            foreach (var c in comps)
            {
                var type = c?.GetType();
                if (type == null) continue;
                try
                {
                    if (HomeLife.ClassIs(type, "DubsBadHygiene", "CompPipe"))
                    {
                        var mode = c.props?.GetType().GetField("mode")?.GetValue(c.props);
                        return "dbh:" + (mode?.ToString() ?? "pipe");
                    }
                    if (HomeLife.ClassIs(type, "PipeSystem", "CompResource"))
                    {
                        var net = c.props?.GetType().GetField("pipeNet")?.GetValue(c.props) as Def;
                        return "ve:" + (net?.defName ?? "pipe");
                    }
                }
                catch
                {
                    // A changed mod: no pipes to speak of.
                }
            }
            return null;
        }

        private static string PipeLabel(string key)
        {
            int i = key.IndexOf(':');
            string name = i >= 0 ? key.Substring(i + 1) : key;
            if (key.StartsWith("ve:"))
            {
                var def = DefDatabase<Def>.AllDefsListForReading.FirstOrDefault(d => d.defName == name && d.GetType().Namespace == "PipeSystem");
                if (def != null && !def.label.NullOrEmpty()) return def.label;
            }
            return GenText.SplitCamelCase(name).ToLowerInvariant();
        }

        private static void Pipes(Map map, List<BaseFinding> findings)
        {
            var pipes = new Dictionary<string, Dictionary<(int x, int z), Thing>>();
            var fixtures = new Dictionary<string, HashSet<(int x, int z)>>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                string key = PipeKey(b);
                if (key == null) continue;
                if (!b.def.building.isEdifice && b.def.size == IntVec2.One)
                {
                    if (!pipes.TryGetValue(key, out var d)) pipes[key] = d = new Dictionary<(int, int), Thing>();
                    d[(b.Position.x, b.Position.z)] = b;
                }
                else
                {
                    if (!fixtures.TryGetValue(key, out var f)) fixtures[key] = f = new HashSet<(int, int)>();
                    foreach (var c in b.OccupiedRect())
                    {
                        f.Add((c.x, c.z));
                        for (int i = 0; i < 4; i++)
                        {
                            var n = c + GenAdj.CardinalDirections[i];
                            f.Add((n.x, n.z));
                        }
                    }
                }
            }
            // Something still being built beside a pipe is what it is for.
            var building = new HashSet<(int x, int z)>();
            foreach (var t in map.listerThings.AllThings)
            {
                if (!(t is Blueprint) && !(t is Frame)) continue;
                foreach (var c in t.OccupiedRect())
                {
                    building.Add((c.x, c.z));
                    for (int i = 0; i < 4; i++)
                    {
                        var n = c + GenAdj.CardinalDirections[i];
                        building.Add((n.x, n.z));
                    }
                }
            }
            foreach (var kv in pipes)
            {
                fixtures.TryGetValue(kv.Key, out var near);
                bool Used((int x, int z) c) => (near != null && near.Contains(c)) || building.Contains(c);
                var dead = new List<Thing>();
                int runs = 0;
                foreach (var run in BaseReviewMath.DeadEnds(kv.Value.Keys.ToList(), Used))
                {
                    runs++;
                    dead.AddRange(run.Select(c => kv.Value[c]));
                }
                if (dead.Count == 0) continue;
                string label = PipeLabel(kv.Key);
                string key = kv.Key;
                findings.Add(new BaseFinding
                {
                    key = "pipes:" + key, group = "works", score = BaseReviewMath.TidyScore(runs, 0.03f, 0.2f),
                    title = "CC_Base_Pipes".Translate(dead.Count.Named("N"), label.Named("KIND"), runs.Named("RUNS")).Resolve(), detail = "CC_Base_Pipes_Detail".Translate().Resolve(),
                    look = dead[0].Position, things = dead, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fix = () => Deconstruct(map, dead)
                });
            }
        }

        // ---------------- Freezers ----------------

        private static void Freezers(Map map, List<RoomInfo> rooms, List<BaseFinding> findings)
        {
            foreach (var r in rooms)
            {
                if (r.role != "Freezer") continue;
                var cells = new HashSet<IntVec3>(r.room.Cells);
                foreach (var c in cells)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var n = c + GenAdj.CardinalDirections[i];
                        if (cells.Contains(n) || !n.InBounds(map)) continue;
                        var door = n.GetEdifice(map);
                        if (door == null || !door.def.IsDoor) continue;
                        var beyondCell = n + GenAdj.CardinalDirections[i];
                        if (!beyondCell.InBounds(map) || cells.Contains(beyondCell)) continue;
                        var beyond = beyondCell.GetRoom(map);
                        if (beyond == null || beyond == r.room) continue;
                        bool outdoors = beyond.PsychologicallyOutdoors;
                        int doors = outdoors ? 0 : DoorCount(beyond, map);
                        if (!BaseReviewMath.AirlockWanted(r.room.Temperature, beyond.Temperature, beyond.CellCount, doors, outdoors)) continue;
                        string beyondLabel = outdoors ? "CC_Base_Outdoors".Translate().Resolve() : beyond.GetRoomRoleLabel();
                        findings.Add(new BaseFinding
                        {
                            key = "freezer:" + door.thingIDNumber, group = "doors", score = outdoors ? 0.3f : 0.22f,
                            title = "CC_Base_FreezerDoor".Translate(beyondLabel.Named("B")).Resolve().CapitalizeFirst(),
                            detail = "CC_Base_FreezerDoor_Detail".Translate(((int)r.room.Temperature).Named("T"), ((int)beyond.Temperature).Named("TB")).Resolve(),
                            look = door.Position
                        });
                        break;
                    }
                }
            }
        }

        private static int DoorCount(Room room, Map map)
        {
            int n = 0;
            var cells = new HashSet<IntVec3>(room.Cells);
            var seen = new HashSet<Thing>();
            foreach (var c in cells)
                for (int i = 0; i < 4; i++)
                {
                    var d = c + GenAdj.CardinalDirections[i];
                    if (cells.Contains(d) || !d.InBounds(map)) continue;
                    var ed = d.GetEdifice(map);
                    if (ed != null && ed.def.IsDoor && seen.Add(ed)) n++;
                }
            return n;
        }

        // ---------------- Fixes ----------------

        /// <summary>Orders what the finding named taken down, as the player's own deconstruct orders (cancel them like any other).</summary>
        private static void Deconstruct(Map map, List<Thing> things)
        {
            int n = 0;
            var dm = map.designationManager;
            foreach (var t in things)
            {
                if (t == null || t.Destroyed || !t.Spawned || t.Map != map || t.Faction != Faction.OfPlayer) continue;
                if (dm.DesignationOn(t, DesignationDefOf.Deconstruct) != null) continue;
                dm.AddDesignation(new Designation(t, DesignationDefOf.Deconstruct));
                n++;
            }
            Messages.Message("CC_Base_TakenDown".Translate(n), n > 0 ? new LookTargets(things[0]) : null, MessageTypeDefOf.SilentInput, false);
            For(map, true);
        }
    }
}
