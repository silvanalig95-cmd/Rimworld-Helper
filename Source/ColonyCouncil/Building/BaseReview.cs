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
        /// <summary>What the fix does on the map, for its button's tooltip.</summary>
        public string fixTip;
        /// <summary>The fix, done as a step of the shared suggestion core: returns what it did, with its undo and check (null when it couldn't).</summary>
        public Func<SuggestDone> fix;
        /// <summary>A room the planner can put beside what this one is far from (the Plan the room button).</summary>
        public RoomTemplateDef template;
        /// <summary>What it draws on the map: the cells it's about, a second set (the other room, the nearest store), a line between two points, a short label.</summary>
        public List<IntVec3> cells = new List<IntVec3>();
        public List<IntVec3> other = new List<IntVec3>();
        public IntVec3 from = IntVec3.Invalid, to = IntVec3.Invalid;
        public string mapLabel;

        public AdviceLevel Level => (AdviceLevel)NextStepsMath.LevelOf(score);
    }

    /// <summary>
    /// What the base review draws on the map: every finding's cells faintly while the Base page is open or "Show on
    /// the map" is on, the hovered finding in full with its label and line (UI/MapHighlights.cs).
    /// </summary>
    public static class BaseOverlay
    {
        private static int pageFrame = -10;
        private static string hoveredKey;
        private static int hoveredFrame = -10;
        private static bool? show;

        public static bool Show
        {
            get => show ??= PageMemory.GetBool("base.overlay", false);
            set
            {
                show = value;
                PageMemory.SetBool("base.overlay", value);
            }
        }

        public static void PageDrawn() => pageFrame = Time.frameCount;

        public static void Hover(string key)
        {
            hoveredKey = key;
            hoveredFrame = Time.frameCount;
        }

        public static bool Showing => Show || Time.frameCount - pageFrame < 3;

        /// <summary>The finding a row or the little map is hovering over right now, if any.</summary>
        public static string HoveredKey => Time.frameCount - hoveredFrame < 3 ? hoveredKey : null;

        private static readonly Color RoomsColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color OtherColor = new Color(1f, 1f, 1f);
        private static readonly Color WorksColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DoorsColor = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color StoresColor = new Color(0.45f, 0.75f, 1f);

        public static Color ColorOf(BaseFinding f)
        {
            switch (f.group)
            {
                case "rooms":
                case "walks": return RoomsColor;
                case "doors": return DoorsColor;
                case "stores": return StoresColor;
                default: return WorksColor;
            }
        }

        /// <summary>Adds the findings' highlights for this frame (called from the map's update while showing).</summary>
        public static void Draw(Map map)
        {
            if (map == null || !Showing) return;
            string hov = HoveredKey;
            foreach (var f in BaseReview.For(map)) Highlight(map, f, hov != null && f.key == hov);
        }

        /// <summary>One finding on the map: its cells in its part's colour, the other room or store in white, the line between them, its short label.</summary>
        public static void Highlight(Map map, BaseFinding f, bool bright)
        {
            if (map == null || f == null || f.cells.Count == 0 && !f.look.IsValid) return;
            var h = new MapHighlight
            {
                key = "base:" + f.key, map = map, color = ColorOf(f), otherColor = OtherColor, bright = bright, labelAlways = true,
                label = f.mapLabel ?? f.title, labelAt = f.look.IsValid ? f.look : f.cells[0], from = f.from, to = f.to,
                lineColor = f.group == "stores" ? SimpleColor.Cyan : SimpleColor.Orange
            };
            h.cells.AddRange(f.cells.Count > 0 ? f.cells : new List<IntVec3> { f.look });
            h.other.AddRange(f.other);
            MapHighlights.Add(h);
        }
    }

    /// <summary>
    /// The base looked over (the Base page): rooms that belong together standing far apart and rooms that belong
    /// apart standing close, long walks to meals, doors that separate nothing, stubs of wall, conduits and pipes that
    /// lead nowhere, a freezer door that opens into the warm, and the stores (<see cref="StorageReview"/>). Rules in
    /// <see cref="BaseReviewMath"/>. Found once an hour for the plan's proposals, and on the page when asked.
    /// </summary>
    public static class BaseReview
    {
        /// <summary>The source of its steps in the shared suggestion core: keys "base:" and the finding's key.</summary>
        public const string Source = "base";

        private class Cache
        {
            public int tick = -999999;
            public List<BaseFinding> findings = new List<BaseFinding>();
            public List<BaseFinding> open, openFrom;
            public int openVersion = -1;
        }

        private static readonly Dictionary<int, Cache> caches = new Dictionary<int, Cache>();

        /// <summary>
        /// The findings now, without those the player set aside with "Not this" and those whose fix was done and is still
        /// to be checked (the shared suggestion core): what the page, the plan's proposals and the automations go by.
        /// </summary>
        public static List<BaseFinding> For(Map map, bool force = false)
        {
            var c = CacheOf(map, force);
            if (c == null) return new List<BaseFinding>();
            var tracker = SuggestTracker.Instance;
            if (tracker == null) return c.findings;
            if (c.open == null || c.openFrom != c.findings || c.openVersion != tracker.Version)
            {
                c.open = c.findings.Where(f => tracker.Offered(KeyOf(f), map)).ToList();
                c.openFrom = c.findings;
                c.openVersion = tracker.Version;
            }
            return c.open;
        }

        /// <summary>Every finding now, set aside or not (for the check of a step done).</summary>
        public static List<BaseFinding> All(Map map, bool force = false) => CacheOf(map, force)?.findings ?? new List<BaseFinding>();

        private static Cache CacheOf(Map map, bool force)
        {
            if (map == null || !map.IsPlayerHome) return null;
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
            return c;
        }

        public static BaseFinding Finding(Map map, string key) => For(map).FirstOrDefault(f => f.key == key);

        /// <summary>A finding's key in the shared suggestion core ("base:stubs"), the same the Plan page proposes it by.</summary>
        public static string KeyOf(BaseFinding f) => f == null ? null : Source + ":" + f.key;

        /// <summary>A finding as a step: its words and level, Show, and its fix as Do it with the fix's undo and a look later whether the finding has gone.</summary>
        public static Suggestion StepOf(Map map, BaseFinding f)
        {
            if (f == null) return null;
            var fix = f.fix;
            var cells = f.cells.ToList();
            return new Suggestion
            {
                key = KeyOf(f), source = Source, area = f.group, title = f.title, reason = f.detail, level = (int)f.Level, map = map,
                doLabel = f.fixLabel, doTip = f.fixTip, look = f.look.IsValid ? new LookTargets(f.look, map) : null, cells = cells.Count > 0 ? (Func<List<IntVec3>>)(() => cells) : null,
                apply = fix == null ? null : (Func<SuggestDone>)(() =>
                {
                    var d = fix();
                    For(map, true);
                    return d;
                })
            };
        }

        /// <summary>Carries out a finding's fix through the shared suggestion core (the player's click): kept with Undo, checked later.</summary>
        public static SuggestDone Apply(Map map, BaseFinding f)
        {
            var step = StepOf(map, f);
            return step?.apply == null ? null : SuggestTracker.Instance?.Apply(step);
        }

        // ---------------- Rooms ----------------

        private class RoomInfo
        {
            public Room room;
            public string role;
            public (float x, float z) mid;
            /// <summary>The room's own cell nearest its middle: where a walk to or from it starts.</summary>
            public IntVec3 middle;
            public string label;
        }

        /// <summary>The map's cells as people walk them (walls and rock closed, doors open but never passed on the slant), for walks between rooms.</summary>
        private static BaseReviewMath.WalkGrid WalkGridOf(Map map)
        {
            int w = map.Size.x, h = map.Size.z;
            var walk = new bool[w * h];
            var diag = new bool[w * h];
            var ci = map.cellIndices;
            foreach (var c in map.AllCells)
            {
                int i = ci.CellToIndex(c);
                bool ok = c.Walkable(map);
                walk[i] = ok;
                diag[i] = !ok || c.GetEdifice(map) is Building_Door;
            }
            return new BaseReviewMath.WalkGrid(w, h, walk, diag);
        }

        private static IntVec3 MiddleOf(Room room)
        {
            var mid = room.ExtentsClose.CenterCell;
            var best = IntVec3.Invalid;
            int d = int.MaxValue;
            foreach (var c in room.Cells)
            {
                int dd = (c - mid).LengthHorizontalSquared;
                if (dd < d)
                {
                    d = dd;
                    best = c;
                }
            }
            return best.IsValid ? best : mid;
        }

        /// <summary>The colony's rooms with the review's roles, for the little map.</summary>
        public static List<(Room room, string role)> RoomsByRole(Map map)
        {
            var list = new List<(Room, string)>();
            foreach (var r in Rooms(map)) list.Add((r.room, r.role));
            return list;
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
                list.Add(new RoomInfo { room = room, role = role, mid = (mid.x, mid.z), middle = MiddleOf(room), label = role == "Freezer" ? RoleLabel(role) : room.GetRoomRoleLabel() });
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
            // Where walks to and from a role start: each room's middle cell, the gate, the field cells.
            List<int> WalkCells(string role)
            {
                var ci = map.cellIndices;
                if (role == "Gate")
                {
                    var gate = RoomNeighbours.GateCell(map);
                    return gate.IsValid && gate.InBounds(map) ? new List<int> { ci.CellToIndex(gate) } : new List<int>();
                }
                if (role == "Fields")
                {
                    var fields = RoomNeighbours.FieldCells(map);
                    return fields == null ? new List<int>() : fields.Where(c => c.InBounds(map)).Select(c => ci.CellToIndex(c)).ToList();
                }
                return byRole.TryGetValue(role, out var l) ? l.Select(r => ci.CellToIndex(r.middle)).ToList() : new List<int>();
            }
            BaseReviewMath.WalkGrid grid = null;
            BaseReviewMath.WalkGrid Grid() => grid ??= WalkGridOf(map);

            // Rooms that belong together, far apart: by the walk between them, as people go (a door on the far side
            // makes a long way round), with the straight line beside it in the words.
            foreach (var rule in BaseReviewMath.NearRules)
            {
                var a = Mids(rule.a);
                var b = Mids(rule.b);
                if (a.Count == 0 || b.Count == 0) continue;
                var walks = Grid().Walk(WalkCells(rule.b), null, WalkCells(rule.a), out var nearest);
                var far = BaseReviewMath.TooFarWalking(rule, walks, nearest);
                if (far.Count == 0) continue;
                // One finding per rule, about the worst case; the rest in the detail.
                var worst = far.OrderByDescending(f => f.distance).First();
                var roomA = byRole[rule.a][worst.a];
                string other = RoleLabel(rule.b);
                float crow = worst.b >= 0 && worst.b < b.Count ? Mathf.Sqrt((a[worst.a].x - b[worst.b].x) * (a[worst.a].x - b[worst.b].x) + (a[worst.a].z - b[worst.b].z) * (a[worst.a].z - b[worst.b].z)) : -1f;
                string detail = ("CC_Base_Far_" + rule.key).Translate(roomA.label.Named("A"), other.Named("B"), ((int)worst.distance).Named("D")).Resolve();
                if (crow >= 0f) detail += " " + "CC_Same_Base_Walk".Translate(((int)worst.distance).Named("WALK"), ((int)crow).Named("CROW")).Resolve();
                if (far.Count > 1) detail += " " + "CC_Base_FarMore".Translate(far.Count - 1).Resolve();
                // The cheaper of the two to build anew beside the other: the planner's spot finder weighs the same rule.
                var template = Rebuild(rule.key, map);
                if (template != null) detail += " " + "CC_Base_FarPlan".Translate(template.label.Named("ROOM")).Resolve();
                findings.Add(new BaseFinding
                {
                    key = "far:" + rule.key, group = "rooms", score = BaseReviewMath.FarScore(rule, worst.distance),
                    title = "CC_Base_Far".Translate(roomA.label.Named("A"), other.Named("B")).Resolve().CapitalizeFirst(), detail = detail,
                    look = roomA.room.ExtentsClose.CenterCell, template = template,
                    cells = roomA.room.Cells.ToList(), other = OtherCells(rule.b, byRole, worst.b, map),
                    from = roomA.room.ExtentsClose.CenterCell, to = OtherMid(rule.b, byRole, worst.b, map),
                    mapLabel = "CC_Base_Map_Far".Translate(((int)worst.distance).Named("D"), other.Named("B")).Resolve()
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
                    look = roomA.room.ExtentsClose.CenterCell,
                    cells = roomA.room.Cells.ToList(), other = roomB.room.Cells.ToList(),
                    from = roomA.room.ExtentsClose.CenterCell, to = roomB.room.ExtentsClose.CenterCell,
                    mapLabel = "CC_Base_Map_Close".Translate(roomB.label.Named("B")).Resolve()
                });
            }

            // Long walks to meals: the walk from each bedroom to the nearest dining room, as people go.
            if (byRole.TryGetValue("Bedroom", out var bedrooms) && byRole.TryGetValue("DiningRoom", out var diners))
            {
                var walks = Grid().Walk(WalkCells("DiningRoom"), null, WalkCells("Bedroom"), out var nearestDining);
                float walk = BaseReviewMath.MealWalkOf(walks);
                if (walk > BaseReviewMath.LongMealWalk)
                {
                    int far = 0;
                    for (int i = 1; i < walks.Length; i++)
                        if (walks[i] > walks[far]) far = i;
                    var farthest = bedrooms[far];
                    var dining = nearestDining[far] >= 0 && nearestDining[far] < diners.Count ? diners[nearestDining[far]] : NearestRoom(farthest, diners);
                    var template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_DiningRoom");
                    if (template != null && template.UnavailableReason(map) != null) template = null;
                    findings.Add(new BaseFinding
                    {
                        key = "mealwalk", group = "walks", score = Mathf.Min(0.4f, 0.15f + (walk - BaseReviewMath.LongMealWalk) / 100f),
                        title = "CC_Base_MealWalk".Translate().Resolve(),
                        detail = "CC_Base_MealWalk_Detail".Translate(((int)walk).Named("D"), walks.Count(w => w >= 0f).Named("N")).Resolve() + " "
                                 + "CC_Same_Base_MealWalkFar".Translate(farthest.label.Named("ROOM"), ((int)walks[far]).Named("D")).Resolve(),
                        look = farthest.room.ExtentsClose.CenterCell, template = template,
                        cells = farthest.room.Cells.ToList(), other = dining?.room.Cells.ToList() ?? new List<IntVec3>(),
                        from = farthest.room.ExtentsClose.CenterCell, to = dining?.room.ExtentsClose.CenterCell ?? IntVec3.Invalid,
                        mapLabel = "CC_Base_Map_MealWalk".Translate(((int)walk).Named("D")).Resolve()
                    });
                }
            }

            Doors(map, findings);
            Walls(map, findings);
            Conduits(map, findings);
            Pipes(map, findings);
            try
            {
                LineRoutes.Findings(map, findings);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lines under the walls on the Base page", e);
            }
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

        /// <summary>The room plan to put beside the other for a near rule: the smaller or cheaper of the pair, when the colony can build it.</summary>
        private static RoomTemplateDef Rebuild(string ruleKey, Map map)
        {
            string name;
            switch (ruleKey)
            {
                case "kitchenfreezer": name = "Council_Freezer"; break;
                case "kitchendining": name = "Council_DiningRoom"; break;
                case "diningrec": name = "Council_RecRoom"; break;
                case "workshopstore": name = "Council_Storeroom"; break;
                case "hospitalgate": name = "Council_Hospital"; break;
                case "barnfields": name = "Council_Barn"; break;
                default: return null;
            }
            var t = DefDatabase<RoomTemplateDef>.GetNamedSilentFail(name);
            return t != null && t.UnavailableReason(map) == null ? t : null;
        }

        /// <summary>The cells of the other side of a near rule: a room of that role, or the gate cell, or the nearest field.</summary>
        private static List<IntVec3> OtherCells(string role, Dictionary<string, List<RoomInfo>> byRole, int index, Map map)
        {
            if (byRole.TryGetValue(role, out var rooms) && index >= 0 && index < rooms.Count) return rooms[index].room.Cells.ToList();
            var mid = OtherMid(role, byRole, index, map);
            return mid.IsValid ? GenRadial.RadialCellsAround(mid, 1.5f, true).Where(c => c.InBounds(map)).ToList() : new List<IntVec3>();
        }

        private static IntVec3 OtherMid(string role, Dictionary<string, List<RoomInfo>> byRole, int index, Map map)
        {
            if (byRole.TryGetValue(role, out var rooms) && index >= 0 && index < rooms.Count) return rooms[index].room.ExtentsClose.CenterCell;
            if (role == "Gate") return RoomNeighbours.GateCell(map);
            if (role == "Fields")
            {
                var fields = RoomNeighbours.FieldCells(map);
                return fields != null && index >= 0 && index < fields.Count ? fields[index] : IntVec3.Invalid;
            }
            return IntVec3.Invalid;
        }

        private static RoomInfo NearestRoom(RoomInfo from, List<RoomInfo> among)
        {
            RoomInfo best = null;
            float d = float.MaxValue;
            foreach (var r in among)
            {
                float dd = (r.mid.x - from.mid.x) * (r.mid.x - from.mid.x) + (r.mid.z - from.mid.z) * (r.mid.z - from.mid.z);
                if (dd < d)
                {
                    d = dd;
                    best = r;
                }
            }
            return best;
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
                cells = pointless.Select(d => d.Position).ToList(), mapLabel = "CC_Base_Map_Doors".Translate().Resolve(),
                fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fixTip = "CC_Base_TakeDownTip".Translate().Resolve(), fix = () => Deconstruct(map, pointless.Cast<Thing>().ToList())
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
                cells = things.Select(t => t.Position).ToList(), mapLabel = "CC_Base_Map_Stubs".Translate().Resolve(),
                fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fixTip = "CC_Base_TakeDownTip".Translate().Resolve(), fix = () => Deconstruct(map, things)
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
                    look = dead[0].Position, things = dead, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fixTip = "CC_Base_TakeDownTip".Translate().Resolve(), fix = () => Deconstruct(map, dead),
                    cells = dead.Select(t => t.Position).ToList(), mapLabel = "CC_Base_Map_Conduits".Translate().Resolve()
                });
            if (orphan.Count > 0)
                findings.Add(new BaseFinding
                {
                    key = "orphanconduits", group = "works", score = BaseReviewMath.TidyScore(orphan.Count, 0.02f, 0.15f),
                    title = "CC_Base_OrphanConduits".Translate(orphan.Count).Resolve(), detail = "CC_Base_OrphanConduits_Detail".Translate().Resolve(),
                    look = orphan[0].Position, things = orphan, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fixTip = "CC_Base_TakeDownTip".Translate().Resolve(), fix = () => Deconstruct(map, orphan),
                    cells = orphan.Select(t => t.Position).ToList(), mapLabel = "CC_Base_Map_Orphan".Translate().Resolve()
                });
        }

        // ---------------- Pipes (Dubs Bad Hygiene, Vanilla Expanded pipe nets) ----------------

        /// <summary>Which pipe network a thing is part of, as a key ("dbh:Sewage", "ve:Chemfuel"), or null.</summary>
        internal static string PipeKey(Thing t)
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

        internal static string PipeLabel(string key)
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
                    look = dead[0].Position, things = dead, fixLabel = "CC_Base_TakeDown".Translate().Resolve(), fixTip = "CC_Base_TakeDownTip".Translate().Resolve(), fix = () => Deconstruct(map, dead),
                    cells = dead.Select(t => t.Position).ToList(), mapLabel = "CC_Base_Map_Conduits".Translate().Resolve()
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
                        var fix = AirlockFix(map, door, GenAdj.CardinalDirections[i], out var lockCells, out int walls);
                        string detail = "CC_Base_FreezerDoor_Detail".Translate(((int)r.room.Temperature).Named("T"), ((int)beyond.Temperature).Named("TB")).Resolve();
                        detail += " " + (fix != null ? "CC_Same_Base_AirlockHow".Translate(walls.Named("WALLS")).Resolve() : "CC_Same_Base_AirlockNoRoom".Translate().Resolve());
                        var marks = new List<IntVec3> { door.Position };
                        if (fix != null) marks.AddRange(lockCells);
                        findings.Add(new BaseFinding
                        {
                            key = "freezer:" + door.thingIDNumber, group = "doors", score = outdoors ? 0.3f : 0.22f,
                            title = "CC_Base_FreezerDoor".Translate(beyondLabel.Named("B")).Resolve().CapitalizeFirst(),
                            detail = detail,
                            look = door.Position,
                            cells = marks, other = cells.ToList(),
                            mapLabel = "CC_Base_Map_Freezer".Translate(((int)beyond.Temperature).Named("TB")).Resolve(),
                            fix = fix, fixLabel = fix != null ? "CC_Same_Base_AirlockDo".Translate().Resolve() : null,
                            fixTip = fix != null ? "CC_Same_Base_AirlockDoTip".Translate(walls.Named("WALLS")).Resolve() : null
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

        /// <summary>
        /// Orders what the finding named taken down, as the player's own deconstruct orders (cancel them like any other):
        /// a step with Undo (the orders taken back) and a look two days on whether the finding has gone. Null when there
        /// was nothing left to order.
        /// </summary>
        public static SuggestDone Deconstruct(Map map, List<Thing> things)
        {
            int n = 0;
            var dm = map.designationManager;
            var undo = new UndoOrders();
            foreach (var t in things)
            {
                if (t == null || t.Destroyed || !t.Spawned || t.Map != map || t.Faction != Faction.OfPlayer) continue;
                if (dm.DesignationOn(t, DesignationDefOf.Deconstruct) != null) continue;
                dm.AddDesignation(new Designation(t, DesignationDefOf.Deconstruct));
                undo.marked.Add(t);
                undo.markedDefs.Add(DesignationDefOf.Deconstruct.defName);
                n++;
            }
            Messages.Message("CC_Base_TakenDown".Translate(n), n > 0 ? new LookTargets(things[0]) : null, MessageTypeDefOf.SilentInput, false);
            For(map, true);
            return n == 0 ? null : new SuggestDone { undo = undo, map = map, check = new SourceGoneCheck { waitTicks = 2 * SuggestMath.TicksPerDay } };
        }

        // ---------------- The freezer's airlock ----------------

        /// <summary>
        /// An airlock outside a freezer door as a plan the player places at a click: a second door one cell further out
        /// (the freezer door's kind and material) and a wall either side of the cell between (the freezer wall's
        /// material), where all of it can go and people can still walk round it. Null when it can't go there.
        /// </summary>
        private static Func<SuggestDone> AirlockFix(Map map, Building door, IntVec3 dir, out List<IntVec3> cells, out int newWalls)
        {
            cells = new List<IntVec3>();
            newWalls = 0;
            var a = BaseReviewMath.AirlockAt((door.Position.x, door.Position.z), (dir.x, dir.z));
            IntVec3 C((int x, int z) p) => new IntVec3(p.x, 0, p.z);
            IntVec3 lobby = C(a.lobby), door2 = C(a.door), beyond = C(a.beyond);
            var sides = new[] { C(a.wallA), C(a.wallB) };
            var round = new[] { C(a.roundA), C(a.roundB) };
            if (!lobby.InBounds(map) || !door2.InBounds(map) || !beyond.InBounds(map) || lobby.Fogged(map) || door2.Fogged(map)) return null;
            if (lobby.GetEdifice(map) != null || !lobby.Standable(map) || door2.GetEdifice(map) != null || !beyond.Walkable(map)) return null;
            var doorDef = door.def;
            var doorStuff = door.Stuff;
            var rot = door.Rotation;
            if (!GenConstruct.CanPlaceBlueprintAt(doorDef, door2, rot, map, false, null, null, doorStuff).Accepted) return null;
            // The freezer wall's material beside the door, else the usual.
            var wallDef = ThingDefOf.Wall;
            ThingDef wallStuff = null;
            foreach (var side in new[] { door.Position + new IntVec3(-dir.z, 0, dir.x), door.Position - new IntVec3(-dir.z, 0, dir.x) })
            {
                var ed = side.InBounds(map) ? side.GetEdifice(map) : null;
                if (ed != null && ed.def == wallDef && ed.Stuff != null)
                {
                    wallStuff = ed.Stuff;
                    break;
                }
            }
            wallStuff ??= GenStuff.DefaultStuffFor(wallDef);
            var walls = new List<IntVec3>();
            for (int i = 0; i < 2; i++)
            {
                var w = sides[i];
                if (!w.InBounds(map) || w.Fogged(map)) return null;
                var ed = w.GetEdifice(map);
                if (ed != null)
                {
                    if (ed.def.IsWall || TemplatePlanner.IsWallLike(ed.def)) continue;
                    return null;
                }
                // A new wall here mustn't shut a way: the cell beside the second door stays open to walk round.
                if (!round[i].InBounds(map) || !round[i].Walkable(map)) return null;
                if (!GenConstruct.CanPlaceBlueprintAt(wallDef, w, Rot4.North, map, false, null, null, wallStuff).Accepted) return null;
                walls.Add(w);
            }
            cells.Add(lobby);
            cells.Add(door2);
            cells.AddRange(walls);
            newWalls = walls.Count;
            return () => SameRules.Orders(map, () =>
            {
                GenConstruct.PlaceBlueprintForBuild(doorDef, door2, map, rot, Faction.OfPlayer, doorStuff);
                foreach (var w in walls) GenConstruct.PlaceBlueprintForBuild(wallDef, w, map, Rot4.North, Faction.OfPlayer, wallStuff);
            }, 3 * SuggestMath.TicksPerDay);
        }
    }
}
