using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The base in a few colours on the Base page, like the defense page's map: the rooms by what they're for, the
    /// walls and doors, the rock, and over them what the review found, each in its part's colour (the rooms it's
    /// about outlined, the pieces it names filled in). Hovering a finding here or in the list marks it on the other
    /// and brightens it on the map; a click goes there. Drawn once per review into a texture.
    /// </summary>
    public static class BaseMiniMap
    {
        private static Texture2D tex;
        private static List<BaseFinding> texFor;
        private static Map texMap;
        private static CellRect bounds;
        private static readonly Dictionary<int, BaseFinding> findingAt = new Dictionary<int, BaseFinding>();
        private static readonly Dictionary<string, CellRect> findingBounds = new Dictionary<string, CellRect>();

        private static readonly Color32 CRock = new Color32(92, 86, 78, 255);
        private static readonly Color32 COpen = new Color32(46, 52, 40, 255);
        private static readonly Color32 CHome = new Color32(70, 98, 58, 255);
        private static readonly Color32 CWater = new Color32(40, 76, 120, 255);
        private static readonly Color32 CWall = new Color32(200, 200, 200, 255);
        private static readonly Color32 CDoor = new Color32(115, 190, 255, 255);
        private static readonly Color32 CRoom = new Color32(96, 96, 92, 255);

        /// <summary>The room kinds the review tells apart, each with its colour on the little map; the legend lists them in this order.</summary>
        public static readonly (string role, Color32 color)[] RoomColors =
        {
            ("Bedroom", new Color32(118, 108, 172, 255)),
            ("Kitchen", new Color32(176, 112, 60, 255)),
            ("DiningRoom", new Color32(182, 152, 82, 255)),
            ("RecRoom", new Color32(88, 150, 128, 255)),
            ("Workshop", new Color32(108, 126, 144, 255)),
            ("StoreRoom", new Color32(150, 134, 98, 255)),
            ("Freezer", new Color32(120, 172, 204, 255)),
            ("Hospital", new Color32(190, 148, 170, 255)),
            ("Barn", new Color32(120, 132, 70, 255)),
            ("PrisonCell", new Color32(134, 82, 82, 255)),
        };

        /// <summary>Whether there's anything to draw: a home area or a room the review knows the use of.</summary>
        public static bool Any(Map map) => map != null && (map.areaManager.Home.TrueCount > 0 || RoomUtil.ColonyRooms(map).Any());

        /// <summary>Draws the map into the area (kept to the base's own bounds, centred) and handles the mouse over it. Returns the rect it filled.</summary>
        public static Rect Draw(Rect area, Map map, List<BaseFinding> findings)
        {
            if (texFor != findings || texMap != map || tex == null)
            {
                if (tex != null) Object.Destroy(tex);
                tex = null;
                try
                {
                    tex = Make(map, findings);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("base mini map", e);
                }
                texFor = findings;
                texMap = map;
            }
            if (tex == null) return area;
            float scale = Mathf.Min(area.width / bounds.Width, area.height / bounds.Height);
            var r = new Rect(area.x + (area.width - bounds.Width * scale) / 2f, area.y + (area.height - bounds.Height * scale) / 2f, bounds.Width * scale, bounds.Height * scale);
            GUI.DrawTexture(r, tex);
            Widgets.DrawBox(r);

            // The hovered finding (a row in the list or a cell here) gets a frame.
            string hov = BaseOverlay.HoveredKey;
            if (hov != null && findingBounds.TryGetValue(hov, out var fb))
            {
                var box = new Rect(r.x + (fb.minX - bounds.minX) * scale - 2f, r.yMax - (fb.maxZ + 1 - bounds.minZ) * scale - 2f, fb.Width * scale + 4f, fb.Height * scale + 4f);
                GUI.color = Color.white;
                Widgets.DrawBox(box, 2);
            }
            if (!Mouse.IsOver(r)) return r;
            var mp = Event.current.mousePosition;
            int cx = bounds.minX + Mathf.Clamp((int)((mp.x - r.x) / scale), 0, bounds.Width - 1);
            int cz = bounds.minZ + Mathf.Clamp((int)((r.yMax - mp.y) / scale), 0, bounds.Height - 1);
            var cell = new IntVec3(cx, 0, cz);
            string tip = "CC_Base_MapTip".Translate();
            if (cell.InBounds(map))
            {
                var room = cell.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors && room.Role != null && room.Role != RoomRoleDefOf.None)
                    tip = room.GetRoomRoleLabel().CapitalizeFirst() + "\n\n" + tip;
                if (findingAt.TryGetValue(Index(cell), out var f))
                {
                    BaseOverlay.Hover(f.key);
                    tip = f.title + "\n\n" + tip;
                }
            }
            TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r))
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJump(cell, map);
            }
            return r;
        }

        /// <summary>A legend of colour and word pairs, wrapping within the width. Returns the height used.</summary>
        public static float Legend(float x, float y, float width, IEnumerable<(Color color, string text)> items)
        {
            Text.Font = GameFont.Tiny;
            float lx = x, ly = y;
            foreach (var (c, text) in items)
            {
                float tw = Text.CalcSize(text).x;
                if (lx + 14f + tw > x + width && lx > x)
                {
                    lx = x;
                    ly += 18f;
                }
                Widgets.DrawBoxSolid(new Rect(lx, ly + 5f, 10f, 8f), c);
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(lx + 14f, ly, tw + 4f, 18f), text);
                GUI.color = Color.white;
                lx += 14f + tw + 12f;
            }
            Text.Font = GameFont.Small;
            return ly + 18f - y;
        }

        private static int Index(IntVec3 c) => (c.z - bounds.minZ) * bounds.Width + (c.x - bounds.minX);

        private static Texture2D Make(Map map, List<BaseFinding> findings)
        {
            findingAt.Clear();
            findingBounds.Clear();
            var rooms = BaseReview.RoomsByRole(map);
            // The bounds: the home area, the rooms and what the findings point at, with a margin.
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            void Take(IntVec3 c)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.z < minZ) minZ = c.z;
                if (c.z > maxZ) maxZ = c.z;
            }
            foreach (var c in map.areaManager.Home.ActiveCells) Take(c);
            foreach (var (room, _) in rooms) foreach (var c in room.Cells) Take(c);
            foreach (var f in findings)
            {
                foreach (var c in f.cells) Take(c);
                foreach (var c in f.other) Take(c);
                if (f.look.IsValid) Take(f.look);
            }
            if (minX > maxX) return null;
            bounds = CellRect.FromLimits(minX - 3, minZ - 3, maxX + 3, maxZ + 3).ClipInsideMap(map);
            if (bounds.Width < 2 || bounds.Height < 2) return null;

            var roleOf = new Dictionary<Room, string>();
            foreach (var (room, role) in rooms) roleOf[room] = role;
            var roomColor = RoomColors.ToDictionary(p => p.role, p => p.color);
            var home = map.areaManager.Home;
            var px = new Color32[bounds.Width * bounds.Height];
            foreach (var c in bounds)
            {
                int i = Index(c);
                Color32 col;
                var edifice = c.GetEdifice(map);
                var room = c.GetRoom(map);
                bool outdoors = room == null || room.PsychologicallyOutdoors;
                Color32 floor = outdoors ? (home[c] ? CHome : COpen)
                    : room != null && roleOf.TryGetValue(room, out var role) && roomColor.TryGetValue(role, out var rc) ? rc : CRoom;
                if (c.GetTerrain(map).IsWater) col = CWater;
                else if (edifice == null) col = floor;
                else if (edifice.def.building != null && edifice.def.building.isNaturalRock || edifice.def.mineable) col = CRock;
                else if (edifice.def.IsDoor) col = CDoor;
                else if (edifice.def.Fillage == FillCategory.Full && edifice.def.passability == Traversability.Impassable) col = CWall;
                else if (edifice.def.passability == Traversability.Impassable || edifice.def.Fillage == FillCategory.Partial)
                    col = Color32.Lerp(floor, new Color32(0, 0, 0, 255), 0.3f);
                else col = floor;
                px[i] = col;
            }
            // The findings over it: the rooms a finding is about outlined, small things filled, most pressing last so it wins where they meet.
            foreach (var f in findings.OrderBy(f => f.score))
            {
                var color = BaseOverlay.ColorOf(f);
                var c32 = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), 255);
                var cells = f.cells.Count > 0 ? f.cells : f.look.IsValid ? new List<IntVec3> { f.look } : null;
                if (cells == null) continue;
                var set = new HashSet<IntVec3>(cells);
                bool outline = cells.Count > 12;
                int fminX = int.MaxValue, fminZ = int.MaxValue, fmaxX = int.MinValue, fmaxZ = int.MinValue;
                foreach (var c in cells)
                {
                    if (!bounds.Contains(c)) continue;
                    if (c.x < fminX) fminX = c.x;
                    if (c.x > fmaxX) fmaxX = c.x;
                    if (c.z < fminZ) fminZ = c.z;
                    if (c.z > fmaxZ) fmaxZ = c.z;
                    findingAt[Index(c)] = f;
                    if (outline && set.Contains(c + IntVec3.North) && set.Contains(c + IntVec3.South) && set.Contains(c + IntVec3.East) && set.Contains(c + IntVec3.West)) continue;
                    px[Index(c)] = c32;
                }
                if (fminX <= fmaxX) findingBounds[f.key] = CellRect.FromLimits(fminX, fminZ, fmaxX, fmaxZ);
            }
            var t = new Texture2D(bounds.Width, bounds.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }
    }
}
