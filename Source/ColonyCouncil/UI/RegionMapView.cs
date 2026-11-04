using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ColonyCouncil.RegionMapMath;

namespace ColonyCouncil
{
    /// <summary>
    /// The region map, the first page of the region window (UI/MainTabWindow_World.cs): the land around the colony over
    /// the colony, never the game's world view. Where it was looking is kept for the session. The mouse wheel zooms, a
    /// drag looks around, a hover tells the land, a click picks a settlement (or any tile) and opens its card in the
    /// region window's drawer, the map's panel (UI/Cards/: the place card has its name, its people and how they stand
    /// with the colony, how far it is by caravan, what Rimfolk knows of them, the sections other parts add, and a small
    /// button to see it in the game's world view, only when asked). Its picture is World/RegionMap.cs, made when the
    /// page is first drawn and let go when the window closes.
    /// </summary>
    public class RegionMapView
    {
        private const float HeaderH = 26f;
        private static readonly Color MapBack = new Color(0.07f, 0.08f, 0.09f);
        private static readonly Color LabelBack = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color MarkBack = new Color(0.06f, 0.07f, 0.08f, 0.8f);

        // Kept for the session: the view of the last map shown.
        private static View? lastView;
        private static string lastViewFor;

        private RegionMap map;
        private string failed, builtFor;
        private View view;
        private float homeCheckedAt = -100f;

        private WorldObject picked;
        private int pickedTile = -1;
        private List<Vector2> pickedCorners;

        private bool pressed, dragging;
        private Vector2 pressAt, lastMouse;
        private int hoverTile = -1, tipFor = int.MinValue;
        private RegionMark hoverMark;
        private List<Vector2> hoverCorners;
        private int hoverCornersFor = -1;
        private string tip;
        private readonly List<Rect> labelRects = new List<Rect>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>
        /// The window closed: the picture is let go (it is made again when the page is next drawn), and where the map was
        /// looking is kept for then. What was picked stays picked if it is still on the new picture.
        /// </summary>
        public void Release()
        {
            if (map != null)
            {
                lastView = view;
                lastViewFor = builtFor;
            }
            map?.Dispose();
            map = null;
            builtFor = null;
            homeCheckedAt = -100f;
            pressed = dragging = false;
            hoverTile = -1;
            hoverMark = null;
        }

        /// <summary>Picks a settlement or other world object, as a click on it would: its card becomes the map's panel (a link from another page).</summary>
        public void Pick(WorldObject o)
        {
            if (o == null || o.Destroyed) return;
            Drawer.Open(RimfolkWindow.World, ThingRefs.Of(o), true);
        }

        /// <summary>The lens the map is read through (UI/Region/LensModel.cs): its marks' colours, its lines, its legend.</summary>
        private LensModel lens;

        /// <summary>
        /// The map read through a lens, with what the lens shows, the way home and the reach along the top of
        /// <paramref name="inRect"/> and the lens's legend at its foot. Its panel is the region window's drawer
        /// (UI/Cards/Drawer.cs): what is picked is the place whose card is open there.
        /// </summary>
        public void Draw(Rect inRect, LensModel lens)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
            {
                UiKit.EmptyState(inRect, Icons.World, "CC_NoGame".Translate());
                return;
            }
            this.lens = lens;
            var font = Text.Font;
            try
            {
                Header(new Rect(inRect.x, inRect.y, inRect.width, HeaderH));
                Keep();
                SyncPick();
                var mapRect = new Rect(inRect.x, inRect.y + HeaderH + 4f, inRect.width, inRect.height - HeaderH - 4f);
                if (map?.texture == null)
                {
                    Widgets.DrawBoxSolid(mapRect, MapBack);
                    UiKit.EmptyState(mapRect, Icons.World, failed ?? "CC_Region_NoHome".Translate().ToString());
                }
                else DrawMap(mapRect);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map page", e);
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------- The header ----------------

        /// <summary>What the lens shows in one line on the left; the way home and the reach on the right.</summary>
        private void Header(Rect r)
        {
            float right = r.xMax;
            const float bw = 22f;
            int reach = Mathf.Clamp(S.regionMapRadius, ReachMin, ReachMax);
            var plus = new Rect(right - bw, r.y + (r.height - bw) / 2f, bw, bw);
            string reachText = "CC_Region_Reach".Translate(reach).Resolve();
            float tw = UiKit.KeptWidth(reachText, GameFont.Small) + 12f;
            var reachRect = new Rect(plus.x - tw, r.y, tw, r.height);
            var minus = new Rect(reachRect.x - bw, plus.y, bw, bw);
            if (Widgets.ButtonImage(minus, TexButton.Minus) && reach > ReachMin) SetReach(reach - ReachStep);
            UiKit.LabelFit(reachRect, reachText, Color.white, GameFont.Small, TextAnchor.MiddleCenter);
            if (Widgets.ButtonImage(plus, TexButton.Plus) && reach < ReachMax) SetReach(reach + ReachStep);
            TooltipHandler.TipRegion(new Rect(minus.x, r.y, plus.xMax - minus.x, r.height), "CC_Region_Reach_Tip".Translate());
            string homeLabel = "CC_Region_Home".Translate();
            float hw = Mathf.Clamp(UiKit.KeptWidth(homeLabel, GameFont.Small) + 24f, 80f, 150f);
            var home = new Rect(minus.x - 10f - hw, r.y + (r.height - 24f) / 2f, hw, 24f);
            if (Widgets.ButtonText(home, homeLabel, active: map != null))
            {
                view = new View(0, 0, map != null ? view.scale : 0);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(home, "CC_Region_Home_Tip".Translate());
            if (lens != null && !lens.line.NullOrEmpty())
                UiKit.LabelFit(new Rect(r.x + 2f, r.y, Mathf.Max(0f, home.x - r.x - 12f), r.height), lens.line, UiKit.Faint, GameFont.Tiny);
        }

        public const int ReachMin = 20, ReachMax = 60, ReachStep = 5;

        private void SetReach(int reach)
        {
            S.regionMapRadius = Mathf.Clamp(reach, ReachMin, ReachMax);
            try
            {
                S.Write();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saving the region map's reach", e);
            }
            homeCheckedAt = -100f;
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        // ---------------- Keeping the picture ----------------

        /// <summary>Draws the picture when the page is first drawn, and again when the reach or the home changes (checked every two seconds).</summary>
        private void Keep()
        {
            if (Time.realtimeSinceStartup - homeCheckedAt < 2f) return;
            homeCheckedAt = Time.realtimeSinceStartup;
            if (!RegionMap.HomeTile(out var layer, out var tile))
            {
                Drop();
                failed = "CC_Region_NoHome".Translate();
                builtFor = null;
                return;
            }
            int reach = Mathf.Clamp(S.regionMapRadius, ReachMin, ReachMax);
            string key = layer.LayerID + ":" + tile.tileId + ":" + reach;
            if (key == builtFor) return;
            bool sameHome = builtFor != null && builtFor.StartsWith(layer.LayerID + ":" + tile.tileId + ":");
            Drop();
            builtFor = key;
            try
            {
                map = RegionMap.Build(layer, tile, reach);
                failed = null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drawing the region map", e);
                map?.Dispose();
                map = null;
                failed = "CC_Region_Failed".Translate();
                return;
            }
            // The view is kept across a change of reach, and from the last time the window was open on the same map.
            if (sameHome) view = new View(view.cx, view.cy, view.scale);
            else if (lastView.HasValue && lastViewFor == key) view = lastView.Value;
            else view = new View(0, 0, 0);
            // What was picked stays picked while it is still on the picture (the drawer keeps its card).
            pickedFor = ThingRef.None;
        }

        private void Drop()
        {
            map?.Dispose();
            map = null;
            picked = null;
            pickedTile = -1;
            pickedCorners = null;
            pickedFor = ThingRef.None;
            hoverTile = -1;
            hoverMark = null;
            hoverCornersFor = -1;
            hoverCorners = null;
            tipFor = int.MinValue;
        }

        /// <summary>The place the drawer's card is (or the last place followed from), picked on the map; nothing picked with no card.</summary>
        private ThingRef pickedFor;

        private void SyncPick()
        {
            var cur = Drawer.Current(RimfolkWindow.World);
            // A person or a people followed from a place keeps the place picked.
            if (!cur.IsNone && !cur.IsPlace) return;
            if (cur == pickedFor && (picked == null || !picked.Destroyed)) return;
            pickedFor = cur;
            picked = null;
            pickedTile = -1;
            pickedCorners = null;
            if (cur.IsNone || map == null) return;
            var wo = ThingRefs.WorldObject(cur);
            if (wo != null && map.TryIndex(wo.Tile, out int i))
            {
                picked = wo;
                pickedTile = i;
                return;
            }
            var t = ThingRefs.TileOf(cur);
            if (t.Valid && map.TryIndex(t, out i))
            {
                pickedTile = i;
                pickedCorners = map.Corners(i);
            }
        }

        // ---------------- The map ----------------

        private double MostScale(double w, double h) => Math.Max(map.ppt * 2.5, FitScale(w, h, map.half) * 2);

        private void DrawMap(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, MapBack);
            Widgets.BeginGroup(rect);
            try
            {
                double w = rect.width, h = rect.height;
                var local = new Rect(0f, 0f, rect.width, rect.height);
                view = Clamp(view, w, h, map.half, MostScale(w, h));
                ToScreen(view, w, h, -map.half, map.half, out double sx, out double sy);
                float side = (float)(2 * map.half * view.scale);
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect((float)sx, (float)sy, side, side), map.texture);

                var marks = map.Marks();
                var mouse = Event.current.mousePosition;
                bool over = Mouse.IsOver(local);
                Hover(marks, mouse, over, w, h);

                if (hoverTile >= 0 && hoverMark == null) Outline(HoverCorners(), new Color(1f, 1f, 1f, 0.45f), 1f, w, h);
                if (picked == null && pickedTile >= 0) Outline(pickedCorners, UiKit.Gold, 2f, w, h);
                Index(marks);
                DrawLines(w, h);
                DrawEventTiles(w, h);
                DrawMarks(marks, w, h);
                Labels(marks, w, h);
                // The lens's legend at the map's foot (the whole of it in the panel with nothing picked).
                if (lens != null) UiKit.LegendStrip(new Rect(6f, 0f, local.width - 44f, local.height - 6f), lens.legend);

                // The zoom buttons, before the map's own mouse handling so a click on them stays theirs.
                var zin = new Rect(local.xMax - 30f, 6f, 24f, 24f);
                var zout = new Rect(local.xMax - 30f, 34f, 24f, 24f);
                if (Widgets.ButtonImage(zin, TexButton.Plus)) view = ZoomAt(view, w, h, w / 2, h / 2, view.scale * 1.5);
                if (Widgets.ButtonImage(zout, TexButton.Minus)) view = ZoomAt(view, w, h, w / 2, h / 2, view.scale / 1.5);
                TooltipHandler.TipRegion(zin, "CC_Region_ZoomIn".Translate());
                TooltipHandler.TipRegion(zout, "CC_Region_ZoomOut".Translate());

                Input(local, marks, w, h);
                if (over && !tip.NullOrEmpty() && !dragging && !zin.Contains(mouse) && !zout.Contains(mouse))
                    TooltipHandler.TipRegion(local, new TipSignal(tip, 0x5E61A3 ^ tipFor));
            }
            finally
            {
                Widgets.EndGroup();
            }
            GUI.color = UiKit.CardBorder;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        private void Hover(List<RegionMark> marks, Vector2 mouse, bool over, double w, double h)
        {
            if (!over || dragging)
            {
                hoverTile = -1;
                hoverMark = null;
                return;
            }
            hoverMark = MarkAt(marks, mouse, w, h);
            ToMap(view, w, h, mouse.x, mouse.y, out double x, out double y);
            hoverTile = hoverMark != null ? hoverMark.index : map.TileAt(x, y);
            int key = hoverMark != null ? 0x40000000 ^ hoverMark.obj.ID : hoverTile;
            if (key == tipFor) return;
            tipFor = key;
            tip = hoverMark != null ? MarkTip(hoverMark) : map.TileText(hoverTile, true);
        }

        private string MarkTip(RegionMark m)
        {
            var lines = new List<string> { m.label ?? m.obj.def?.LabelCap.ToString() ?? "?" };
            var f = m.obj.Faction;
            if (f != null && m.kind != RegionMarkKind.Colony && m.kind != RegionMarkKind.Caravan)
            {
                lines.Add(f.Name);
                string st = RegionMap.StandingText(f);
                if (!st.NullOrEmpty()) lines.Add(st);
            }
            string land = map.TileText(m.index, true);
            if (!land.NullOrEmpty()) lines.Add("\n" + land);
            lines.Add("\n" + "CC_Region_ClickTip".Translate());
            return string.Join("\n", lines);
        }

        private RegionMark MarkAt(List<RegionMark> marks, Vector2 mouse, double w, double h)
        {
            RegionMark best = null;
            float bestD = float.MaxValue;
            foreach (var m in marks)
            {
                ToScreen(view, w, h, m.x, m.y, out double sx, out double sy);
                float d = Vector2.Distance(mouse, new Vector2((float)sx, (float)sy));
                if (d > m.Size / 2f + 3f || d >= bestD) continue;
                best = m;
                bestD = d;
            }
            return best;
        }

        private List<Vector2> HoverCorners()
        {
            if (hoverCornersFor != hoverTile)
            {
                hoverCornersFor = hoverTile;
                hoverCorners = map.Corners(hoverTile);
            }
            return hoverCorners;
        }

        private void Outline(List<Vector2> corners, Color color, float width, double w, double h)
        {
            if (corners == null || corners.Count < 3 || Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < corners.Count; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Count];
                ToScreen(view, w, h, a.x, a.y, out double ax, out double ay);
                ToScreen(view, w, h, b.x, b.y, out double bx, out double by);
                Widgets.DrawLine(new Vector2((float)ax, (float)ay), new Vector2((float)bx, (float)by), color, width);
            }
        }

        private void DrawMarks(List<RegionMark> marks, double w, double h)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var m in marks)
            {
                ToScreen(view, w, h, m.x, m.y, out double sx, out double sy);
                float s = m.Size;
                if (sx < -s || sy < -s || sx > w + s || sy > h + s) continue;
                var r = new Rect((float)sx - s / 2f, (float)sy - s / 2f, s, s);
                // The lens's colour for the mark, where it has one (a mark it isn't about is drawn faint).
                var color = m.color;
                bool faint = m.kind == RegionMarkKind.Other;
                if (lens != null && lens.colours.TryGetValue(m.obj.ID, out var c))
                {
                    color = c;
                    faint = c.a < 0.9f;
                }
                UiKit.DiscFill(r.ExpandedBy(3f), faint ? UiKit.WithAlpha(MarkBack, 0.55f) : MarkBack);
                if (m.kind == RegionMarkKind.Colony) UiKit.RingDraw(r.ExpandedBy(5f), UiKit.Gold);
                if (lens != null && lens.events.Contains(m.obj.ID)) UiKit.RingDraw(r.ExpandedBy(5f), UiKit.WorldColor);
                else if (lens != null && lens.rings.TryGetValue(m.obj.ID, out var regard)) UiKit.RingDraw(r.ExpandedBy(4f), UiKit.WithAlpha(regard, 0.85f));
                if (m.obj == picked) UiKit.RingDraw(r.ExpandedBy(7f), Color.white);
                else if (m == hoverMark) UiKit.RingDraw(r.ExpandedBy(5f), new Color(1f, 1f, 1f, 0.5f));
                if (m.icon != null)
                {
                    GUI.color = faint ? UiKit.WithAlpha(color, Mathf.Min(color.a, 0.75f)) : color;
                    GUI.DrawTexture(r, m.icon);
                    GUI.color = Color.white;
                }
                else UiKit.DiscFill(r.ContractedBy(s * 0.25f), color);
                // A fortune that moved this season: a small arrow at the mark's top right.
                if (lens != null && lens.arrows.TryGetValue(m.obj.ID, out int moved))
                    UiKit.Icon(new Rect(r.xMax - 2f, r.y - 6f, 10f, 10f), moved > 0 ? Icons.TrendUp : Icons.TrendDown, moved > 0 ? UiKit.Good : UiKit.Bad);
            }
        }

        // ---------------- The lens's lines ----------------

        /// <summary>The marks by their world object's id, made again when the marks are gathered again.</summary>
        private readonly Dictionary<int, RegionMark> byId = new Dictionary<int, RegionMark>();
        private int indexedFor = -1;
        private RegionMap indexedMap;

        private void Index(List<RegionMark> marks)
        {
            if (indexedMap == map && indexedFor == map.MarksVersion) return;
            indexedMap = map;
            indexedFor = map.MarksVersion;
            byId.Clear();
            foreach (var m in marks)
                if (m.obj != null)
                    byId[m.obj.ID] = m;
        }

        /// <summary>The lens's lines between places on the map (a few dozen at most), under the marks.</summary>
        private void DrawLines(double w, double h)
        {
            if (lens == null || lens.lines.Count == 0 || Event.current.type != EventType.Repaint) return;
            foreach (var l in lens.lines)
            {
                if (!byId.TryGetValue(l.from, out var a) || !byId.TryGetValue(l.to, out var b)) continue;
                ToScreen(view, w, h, a.x, a.y, out double ax, out double ay);
                ToScreen(view, w, h, b.x, b.y, out double bx, out double by);
                // Neither end in sight and the line not across it: nothing to draw.
                if (Math.Max(ax, bx) < 0 || Math.Min(ax, bx) > w || Math.Max(ay, by) < 0 || Math.Min(ay, by) > h) continue;
                UiKit.DashedLine(new Vector2((float)ax, (float)ay), new Vector2((float)bx, (float)by), RegionViewsMath.Look(l.kind), 0.9f);
            }
        }

        /// <summary>World events at no town: a small mark of the world's colour on their tile.</summary>
        private void DrawEventTiles(double w, double h)
        {
            if (lens == null || lens.eventTiles.Count == 0 || Event.current.type != EventType.Repaint) return;
            foreach (var t in lens.eventTiles)
            {
                if (!map.TryIndex(t, out int i)) continue;
                ToScreen(view, w, h, map.cx[i], map.cy[i], out double sx, out double sy);
                if (sx < -10 || sy < -10 || sx > w + 10 || sy > h + 10) continue;
                var r = new Rect((float)sx - 8f, (float)sy - 8f, 16f, 16f);
                UiKit.DiscFill(r.ExpandedBy(2f), MarkBack);
                UiKit.Icon(r, Icons.WorldEvent, UiKit.WorldColor);
            }
        }

        /// <summary>Names under the settlements (and under anything picked or hovered), never one over another.</summary>
        private void Labels(List<RegionMark> marks, double w, double h)
        {
            if (Event.current.type != EventType.Repaint) return;
            labelRects.Clear();
            bool roomy = view.scale >= 7.0;
            var font = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            for (int pass = 0; pass < 3; pass++)
                foreach (var m in marks)
                {
                    if (m.label.NullOrEmpty()) continue;
                    bool first = m.obj == picked || m == hoverMark;
                    bool wanted = pass == 0 ? first : pass == 1 ? !first && m.kind == RegionMarkKind.Colony : !first && m.kind == RegionMarkKind.Settlement && roomy;
                    if (!wanted) continue;
                    ToScreen(view, w, h, m.x, m.y, out double sx, out double sy);
                    float lw = Mathf.Min(m.labelWidth + 8f, 180f);
                    var r = new Rect((float)sx - lw / 2f, (float)sy + m.Size / 2f + 3f, lw, 16f);
                    if (r.xMax < 0 || r.x > w || r.yMax < 0 || r.y > h) continue;
                    if (pass > 0 && labelRects.Any(o => o.Overlaps(r))) continue;
                    labelRects.Add(r);
                    Widgets.DrawBoxSolid(r, LabelBack);
                    GUI.color = m.kind == RegionMarkKind.Colony ? UiKit.Gold : Color.white;
                    Widgets.Label(r, UiKit.Fit(m.label, lw - 4f));
                    GUI.color = Color.white;
                }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = font;
        }

        /// <summary>The wheel zooms where the mouse is, a drag moves the land with it, a click picks what is under it.</summary>
        private void Input(Rect local, List<RegionMark> marks, double w, double h)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.type)
            {
                case EventType.ScrollWheel when local.Contains(e.mousePosition):
                    view = ZoomAt(view, w, h, e.mousePosition.x, e.mousePosition.y, view.scale * (e.delta.y < 0 ? 1.25 : 0.8));
                    e.Use();
                    break;
                case EventType.MouseDown when local.Contains(e.mousePosition) && (e.button == 0 || e.button == 2):
                    GUIUtility.hotControl = id;
                    pressed = true;
                    dragging = false;
                    pressAt = lastMouse = e.mousePosition;
                    e.Use();
                    break;
                case EventType.MouseDrag when pressed && GUIUtility.hotControl == id:
                    if (!dragging && (e.mousePosition - pressAt).magnitude > 4f) dragging = true;
                    if (dragging) view = Pan(view, e.mousePosition.x - lastMouse.x, e.mousePosition.y - lastMouse.y);
                    lastMouse = e.mousePosition;
                    e.Use();
                    break;
                case EventType.MouseUp when pressed:
                    if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
                    if (!dragging && e.button == 0 && local.Contains(e.mousePosition)) Click(marks, e.mousePosition, w, h);
                    pressed = false;
                    dragging = false;
                    e.Use();
                    break;
            }
        }

        private void Click(List<RegionMark> marks, Vector2 mouse, double w, double h)
        {
            var m = MarkAt(marks, mouse, w, h);
            if (m == null)
            {
                ToMap(view, w, h, mouse.x, mouse.y, out double x, out double y);
                int tile = map.TileAt(x, y);
                if (tile < 0) return;
                m = marks.FirstOrDefault(o => o.index == tile && o.kind != RegionMarkKind.Caravan && o.kind != RegionMarkKind.Other)
                    ?? marks.FirstOrDefault(o => o.index == tile);
                if (m == null)
                {
                    // A bare tile: its card (the land, how far, what other parts know of it).
                    Drawer.Open(RimfolkWindow.World, ThingRefs.OfTile(map.TileOf(tile)), true);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    return;
                }
            }
            Drawer.Open(RimfolkWindow.World, ThingRefs.Of(m.obj), true);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        // ---------------- The panel with nothing picked ----------------

        /// <summary>
        /// The map's panel (the region window's drawer) before anything is picked: how to use the map, and what its colours
        /// mean. With something picked, the drawer shows its card (UI/Cards/Cards.cs: the place card, which took the panel's
        /// lines and sections).
        /// </summary>
        public void DrawEmpty(Rect r)
        {
            float y = r.y;
            y += UiKit.Paragraph(r.x, y, r.width, map == null ? failed ?? "CC_Region_NoHome".Translate().ToString() : "CC_Region_PickHint".Translate().ToString(), UiKit.Faint) + 12f;
            if (map == null || lens == null) return;
            y += UiKit.SectionTitle(r.x, y, r.width, "CC_Region_Legend".Translate(), RegionView.LensIcons[(int)lens.lens], UiKit.HeaderColor, RegionView.LensLabel(lens.lens)) + 2f;
            y += UiKit.Paragraph(r.x, y, r.width, lens.line, UiKit.Faint, GameFont.Tiny) + 6f;
            UiKit.LegendList(r.x, y, r.width, lens.legend);
        }

        private static List<LegendItem> landLegend;

        /// <summary>The land's colours as a legend (the Land lens's), made once.</summary>
        public static List<LegendItem> LandLegend()
        {
            if (landLegend != null) return landLegend;
            landLegend = new List<LegendItem>();
            foreach (var (key, colour) in Legend)
                landLegend.Add(new LegendItem { word = key.Translate(), color = new Color32(colour.r, colour.g, colour.b, 255), swatch = true });
            return landLegend;
        }

        private static readonly (string key, Rgb colour)[] Legend =
        {
            ("CC_Region_Leg_Water", ColourOf(Land.Ocean)),
            ("CC_Region_Leg_Forest", ColourOf(Land.Temperate)),
            ("CC_Region_Leg_Jungle", ColourOf(Land.Tropical)),
            ("CC_Region_Leg_Boreal", ColourOf(Land.Boreal)),
            ("CC_Region_Leg_Grass", ColourOf(Land.Grassland)),
            ("CC_Region_Leg_Shrub", ColourOf(Land.Shrubland)),
            ("CC_Region_Leg_Desert", ColourOf(Land.Desert)),
            ("CC_Region_Leg_Swamp", ColourOf(Land.Swamp)),
            ("CC_Region_Leg_Tundra", ColourOf(Land.Tundra)),
            ("CC_Region_Leg_Ice", ColourOf(Land.Ice)),
            ("CC_Region_Leg_Hills", WithHills(ColourOf(Land.Temperate), 3)),
            ("CC_Region_Leg_Mountains", WithHills(ColourOf(Land.Temperate), 4)),
            ("CC_Region_Leg_River", new Rgb(70, 118, 168)),
            ("CC_Region_Leg_Road", new Rgb(138, 104, 70))
        };

    }
}
