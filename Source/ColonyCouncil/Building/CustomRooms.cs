using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One piece of furniture in a saved room, in template coordinates (the door in the south wall).</summary>
    public class CustomRoomPiece : IExposable
    {
        public string def;
        public string stuff;
        public string role;
        public int x, z, rot;
        public bool inWall;

        public void ExposeData()
        {
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref role, "role");
            Scribe_Values.Look(ref x, "x");
            Scribe_Values.Look(ref z, "z");
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref inWall, "inWall");
        }
    }

    /// <summary>
    /// A room the player saved from a map, kept in the mod settings so it can be planned in any save ("Your rooms"). Only
    /// names are stored: a piece from a mod that's gone falls back to anything with the same purpose and size.
    /// </summary>
    public class CustomRoomData : IExposable
    {
        public int id;
        public string name;
        public int width, height, doorX;
        public string wallStuff;
        public string floor;
        public List<CustomRoomPiece> pieces = new List<CustomRoomPiece>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref width, "width");
            Scribe_Values.Look(ref height, "height");
            Scribe_Values.Look(ref doorX, "doorX");
            Scribe_Values.Look(ref wallStuff, "wallStuff");
            Scribe_Values.Look(ref floor, "floor");
            Scribe_Collections.Look(ref pieces, "pieces", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pieces ??= new List<CustomRoomPiece>();
                pieces.RemoveAll(p => p == null || p.def.NullOrEmpty());
            }
        }

        [Unsaved] private RoomTemplateDef template;

        /// <summary>The saved room as a template the planner can use (built once; not added to the def database).</summary>
        public RoomTemplateDef Template => template ??= Build();

        private RoomTemplateDef Build()
        {
            var t = new RoomTemplateDef
            {
                defName = "Council_Yours_" + id,
                label = name.NullOrEmpty() ? "CC_Yours_Unnamed".Translate().ToString() : name,
                description = "CC_Yours_Desc".Translate(width, height).ToString(),
                size = new IntVec2(Mathf.Max(1, width), Mathf.Max(1, height)),
                doorX = doorX,
                group = CustomRooms.Group,
                order = id,
                custom = this,
                wallStuffHint = wallStuff,
                floors = floor.NullOrEmpty() ? null : new List<string> { floor },
                rug = false
            };
            foreach (var p in pieces)
            {
                t.things.Add(new TemplateThing
                {
                    defs = new List<string> { p.def },
                    role = p.role,
                    pos = new IntVec2(p.x, p.z),
                    rot = new Rot4(TemplateMath.Norm(p.rot)),
                    inWall = p.inWall,
                    stuff = p.stuff,
                    optional = true,
                    // A saved room keeps all of its furniture in every style.
                    tier = "core"
                });
            }
            return t;
        }

        public void Forget() => template = null;
    }

    /// <summary>The player's saved rooms: saving one from the map, listing and deleting them.</summary>
    public static class CustomRooms
    {
        public const string Group = "Yours";
        public const int MaxSide = 24;

        public static List<CustomRoomData> All => ColonyCouncilMod.Settings?.savedRooms ?? new List<CustomRoomData>();

        public static IEnumerable<RoomTemplateDef> Templates => All.Select(r => r.Template);

        public static void Delete(CustomRoomData data)
        {
            var s = ColonyCouncilMod.Settings;
            if (s?.savedRooms == null || data == null) return;
            s.savedRooms.Remove(data);
            s.planMaterials?.RemoveAll(e => e != null && e.StartsWith("custom:" + data.id + "::"));
            CouncilSettings.SaveNow();
            Messages.Message("CC_Yours_Deleted".Translate(data.name), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>The room can be saved: enclosed, a rectangle, not too big, with a door in a side wall.</summary>
        public static AcceptanceReport CanSave(Room room)
        {
            if (room == null || room.Map == null || room.PsychologicallyOutdoors || room.TouchesMapEdge || !room.ProperRoom || room.IsDoorway)
                return "CC_Yours_NotARoom".Translate();
            var rect = room.ExtentsClose;
            if (room.CellCount != rect.Area) return "CC_Yours_NotRectangle".Translate();
            if (rect.Width > MaxSide || rect.Height > MaxSide) return "CC_Yours_TooBig".Translate(MaxSide);
            if (!FindDoor(room, rect, out _, out _)) return "CC_Yours_NoDoor".Translate();
            return true;
        }

        /// <summary>The first door in the room's walls (not in a corner), and which side it's on (0 south, 1 west, 2 north, 3 east).</summary>
        private static bool FindDoor(Room room, CellRect rect, out IntVec3 door, out int side)
        {
            var map = room.Map;
            var ring = rect.ExpandedBy(1);
            foreach (var c in ring.EdgeCells)
            {
                bool corner = (c.x == ring.minX || c.x == ring.maxX) && (c.z == ring.minZ || c.z == ring.maxZ);
                if (corner || !c.InBounds(map)) continue;
                var ed = c.GetEdifice(map);
                if (ed == null || !ed.def.IsDoor) continue;
                door = c;
                side = c.z == ring.minZ ? 0 : c.x == ring.minX ? 1 : c.z == ring.maxZ ? 2 : 3;
                return true;
            }
            door = IntVec3.Invalid;
            side = 0;
            return false;
        }

        /// <summary>
        /// Captures a built room: turned so its door is in the south wall like every template, with the furniture that can
        /// be built (art and other crafted things are left out), the most common wall material and floor.
        /// </summary>
        public static CustomRoomData Capture(Room room, string name)
        {
            var map = room.Map;
            var rect = room.ExtentsClose;
            if (!FindDoor(room, rect, out var door, out int side)) return null;
            int turn = TemplateMath.TurnForDoorSide(side);
            var full = rect.ExpandedBy(1);
            // The template's own full size: an odd turn swaps width and height back.
            TemplateMath.RotatedSize(full.Width, full.Height, turn, out int tw, out int th);
            TemplateMath.InverseRotatePoint(door.x - full.minX, door.z - full.minZ, tw, th, turn, out int dfx, out int dfz);
            var data = new CustomRoomData
            {
                id = NextId(),
                name = name,
                width = tw - 2,
                height = th - 2,
                doorX = Mathf.Clamp(dfx - 1, 0, tw - 3)
            };

            // Walls: the most common material.
            var wallCounts = new Dictionary<ThingDef, int>();
            foreach (var c in full.EdgeCells)
            {
                var ed = c.InBounds(map) ? c.GetEdifice(map) : null;
                if (ed?.Stuff != null && ed.def.IsWall)
                {
                    wallCounts.TryGetValue(ed.Stuff, out int n);
                    wallCounts[ed.Stuff] = n + 1;
                }
            }
            data.wallStuff = wallCounts.Count > 0 ? wallCounts.OrderByDescending(kv => kv.Value).First().Key.defName : null;

            // Floor: the most common laid floor.
            var floorCounts = new Dictionary<TerrainDef, int>();
            foreach (var c in room.Cells)
            {
                var terrain = c.GetTerrain(map);
                if (terrain == null || !terrain.BuildableByPlayer || !RoomPlanConfig.IsFloor(terrain)) continue;
                floorCounts.TryGetValue(terrain, out int n);
                floorCounts[terrain] = n + 1;
            }
            data.floor = floorCounts.Count > 0 ? floorCounts.OrderByDescending(kv => kv.Value).First().Key.defName : null;

            var seen = new HashSet<Thing>();
            foreach (var thing in room.ContainedAndAdjacentThings)
            {
                if (!(thing is Building b) || !seen.Add(b) || b.def == null) continue;
                var def = b.def;
                if (!def.BuildableByPlayer || def.IsWall || def.IsDoor || def.IsFrame || def.IsBlueprint || b.Faction != Faction.OfPlayer) continue;
                var occ = b.OccupiedRect();
                bool inside = rect.Contains(occ.Min) && rect.Contains(occ.Max);
                bool inWall = !inside && occ.Width == 1 && occ.Height == 1 && full.EdgeCells.Contains(occ.Min);
                if (!inside && !inWall) continue;
                // The footprint in the template's own frame, and the facing.
                TemplateMath.InverseRotateRect(occ.minX - full.minX, occ.minZ - full.minZ, occ.Width, occ.Height, tw, th, turn,
                    out int fx, out int fz, out int fw, out int fh);
                int facing = def.rotatable ? TemplateMath.InverseFacing(b.Rotation.AsInt, turn) : 0;
                if (!inWall)
                {
                    fx -= 1;
                    fz -= 1;
                }
                if (!TemplateLayout.RootFor(fx, fz, fw, fh, facing, def.size.x, def.size.z, out int rx, out int rz)) continue;
                data.pieces.Add(new CustomRoomPiece
                {
                    def = def.defName,
                    stuff = b.Stuff?.defName,
                    role = RoleOf(def),
                    x = rx,
                    z = rz,
                    rot = facing,
                    inWall = inWall
                });
            }
            return data;
        }

        private static string RoleOf(ThingDef def)
        {
            foreach (var role in RoomTemplateDef.InferableRoles)
            {
                try
                {
                    var pred = RoomTemplateDef.RolePredicate(role);
                    if (pred != null && pred(def)) return role;
                }
                catch
                {
                    // A modded def that a role check can't read: no role, the exact def still works.
                }
            }
            return null;
        }

        private static int NextId()
        {
            int id = 1;
            foreach (var r in All) id = Mathf.Max(id, r.id + 1);
            return id;
        }

        public static void Add(CustomRoomData data)
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || data == null) return;
            s.savedRooms ??= new List<CustomRoomData>();
            s.savedRooms.Add(data);
            CouncilSettings.SaveNow();
        }
    }

    /// <summary>"Save a room": click a built room to keep it as a plan of your own.</summary>
    public class Designator_SaveRoom : Designator
    {
        private static readonly Color Outline = new Color(0.55f, 0.85f, 1f);

        public Designator_SaveRoom()
        {
            useMouseIcon = true;
            icon = TexButton.Copy;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public override string Label => "CC_Yours_Save".Translate();
        public override string Desc => "CC_Yours_SaveDesc".Translate();

        public static void Begin()
        {
            if (Find.CurrentMap == null) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(new Designator_SaveRoom());
            Messages.Message("CC_Yours_PickRoom".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            return CustomRooms.CanSave(c.GetRoom(Map));
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var room = c.GetRoom(Map);
            if (!CustomRooms.CanSave(room).Accepted) return;
            string suggested = room.Role != null && room.Role != RoomRoleDefOf.None ? room.Role.label : "CC_Yours_Unnamed".Translate().ToString();
            Find.WindowStack.Add(new Dialog_NameRoom(suggested, name =>
            {
                var data = CustomRooms.Capture(room, name);
                if (data == null) return;
                CustomRooms.Add(data);
                Messages.Message("CC_Yours_Saved".Translate(name, data.width, data.height, data.pieces.Count), MessageTypeDefOf.TaskCompletion, false);
            }));
            Find.DesignatorManager.Deselect();
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            if (map == null) return;
            var c = UI.MouseCell();
            if (!c.InBounds(map)) return;
            var room = c.GetRoom(map);
            if (room == null || room.PsychologicallyOutdoors || room.CellCount > 600) return;
            GenDraw.DrawFieldEdges(room.Cells.ToList(), CustomRooms.CanSave(room).Accepted ? Outline : Color.red);
        }
    }

    /// <summary>Asks for a saved room's name.</summary>
    public class Dialog_NameRoom : Window
    {
        private string name;
        private readonly System.Action<string> done;
        private bool focused;

        public Dialog_NameRoom(string suggested, System.Action<string> done)
        {
            name = suggested ?? "";
            this.done = done;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(360f, 160f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 28f), "CC_Yours_NamePrompt".Translate());
            GUI.SetNextControlName("RimfolkRoomName");
            name = Widgets.TextField(new Rect(0f, 32f, inRect.width, 32f), name, 40);
            if (!focused)
            {
                UI.FocusControl("RimfolkRoomName", this);
                focused = true;
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 70f, inRect.height - 38f, 140f, 34f), "OK".Translate()) || KeyBindingDefOf.Accept.KeyDownEvent)
                Accept();
        }

        public override void OnAcceptKeyPressed() => Accept();

        private bool finished;

        private void Accept()
        {
            if (finished) return;
            string n = name?.Trim();
            if (n.NullOrEmpty())
            {
                Messages.Message("CC_Yours_NeedName".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            finished = true;
            done?.Invoke(n);
            Close();
        }
    }
}
