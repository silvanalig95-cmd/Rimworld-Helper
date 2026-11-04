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
    /// drag looks around, a hover tells the land, a click picks a settlement (or any tile) for the panel beside the map:
    /// its name, its people and how they stand with the colony, how far it is by caravan, what Rimfolk knows of them,
    /// the sections other parts of Rimfolk add (World/NeighbourPanel.cs), and a button to see it in the game's world
    /// view, only when asked. Its picture is World/RegionMap.cs, made when the page is first drawn and let go when the
    /// window closes.
    /// </summary>
    public class RegionMapView
    {
        private const float HeaderH = 32f;
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
        private readonly Dictionary<int, int> ticksTo = new Dictionary<int, int>();

        private bool pressed, dragging;
        private Vector2 pressAt, lastMouse;
        private int hoverTile = -1, tipFor = int.MinValue;
        private RegionMark hoverMark;
        private List<Vector2> hoverCorners;
        private int hoverCornersFor = -1;
        private string tip;
        private Vector2 scroll;
        private float panelHeight = 400f;
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

        /// <summary>The map and its panel, with the reach and the way home along the top of <paramref name="inRect"/>.</summary>
        public void Draw(Rect inRect)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
            {
                UiKit.EmptyState(inRect, Icons.World, "CC_NoGame".Translate());
                return;
            }
            var font = Text.Font;
            try
            {
                Header(new Rect(inRect.x, inRect.y, inRect.width, HeaderH));
                Keep();
                var body = new Rect(inRect.x, inRect.y + HeaderH + 6f, inRect.width, inRect.height - HeaderH - 6f);
                Rect mapRect, panelRect;
                if (body.width >= 640f)
                {
                    float pw = Mathf.Clamp(body.width * 0.34f, 250f, 340f);
                    mapRect = new Rect(body.x, body.y, body.width - pw - 10f, body.height);
                    panelRect = new Rect(mapRect.xMax + 10f, body.y, pw, body.height);
                }
                else
                {
                    float ph = Mathf.Clamp(body.height * 0.4f, 170f, 260f);
                    mapRect = new Rect(body.x, body.y, body.width, body.height - ph - 8f);
                    panelRect = new Rect(body.x, mapRect.yMax + 8f, body.width, ph);
                }
                if (map?.texture == null)
                {
                    Widgets.DrawBoxSolid(mapRect, MapBack);
                    UiKit.EmptyState(mapRect, Icons.World, failed ?? "CC_Region_NoHome".Translate().ToString());
                }
                else DrawMap(mapRect);
                Panel(panelRect);
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

        private void Header(Rect r)
        {
            UiKit.Icon(new Rect(r.x, r.y + 3f, 26f, 26f), Icons.World, UiKit.Gold);
            float right = r.xMax;
            const float bw = 26f;
            int reach = Mathf.Clamp(S.regionMapRadius, ReachMin, ReachMax);
            var plus = new Rect(right - bw, r.y + 3f, bw, bw);
            string reachText = "CC_Region_Reach".Translate(reach).Resolve();
            Text.Font = GameFont.Small;
            float tw = Text.CalcSize(reachText).x + 12f;
            var reachRect = new Rect(plus.x - tw, r.y, tw, r.height);
            var minus = new Rect(reachRect.x - bw, r.y + 3f, bw, bw);
            if (Widgets.ButtonImage(minus, TexButton.Minus) && reach > ReachMin) SetReach(reach - ReachStep);
            UiKit.LabelFit(reachRect, reachText, Color.white, GameFont.Small, TextAnchor.MiddleCenter);
            if (Widgets.ButtonImage(plus, TexButton.Plus) && reach < ReachMax) SetReach(reach + ReachStep);
            TooltipHandler.TipRegion(new Rect(minus.x, r.y, plus.xMax - minus.x, r.height), "CC_Region_Reach_Tip".Translate());
            var home = new Rect(minus.x - 10f - 150f, r.y + 2f, 150f, 28f);
            if (Widgets.ButtonText(home, "CC_Region_Home".Translate(), active: map != null))
            {
                view = new View(0, 0, map != null ? view.scale : 0);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(home, "CC_Region_Home_Tip".Translate());
            UiKit.LabelFit(new Rect(r.x + 34f, r.y, Mathf.Max(0f, home.x - r.x - 44f), r.height), "CC_Region_Title".Translate(), Color.white, GameFont.Medium);
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
            var oldPicked = picked;
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
            // What was picked stays picked while it is still in this game's world and on the picture.
            if (oldPicked != null && !oldPicked.Destroyed && Find.WorldObjects.Contains(oldPicked) && map.TryIndex(oldPicked.Tile, out _)) picked = oldPicked;
        }

        private void Drop()
        {
            map?.Dispose();
            map = null;
            picked = null;
            pickedTile = -1;
            pickedCorners = null;
            hoverTile = -1;
            hoverMark = null;
            hoverCornersFor = -1;
            hoverCorners = null;
            tipFor = int.MinValue;
            ticksTo.Clear();
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
                DrawMarks(marks, w, h);
                Labels(marks, w, h);

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
                UiKit.DiscFill(r.ExpandedBy(3f), m.kind == RegionMarkKind.Other ? UiKit.WithAlpha(MarkBack, 0.55f) : MarkBack);
                if (m.kind == RegionMarkKind.Colony) UiKit.RingDraw(r.ExpandedBy(5f), UiKit.Gold);
                if (m.obj == picked) UiKit.RingDraw(r.ExpandedBy(7f), Color.white);
                else if (m == hoverMark) UiKit.RingDraw(r.ExpandedBy(5f), new Color(1f, 1f, 1f, 0.5f));
                if (m.icon != null)
                {
                    GUI.color = m.kind == RegionMarkKind.Other ? UiKit.WithAlpha(m.color, 0.75f) : m.color;
                    GUI.DrawTexture(r, m.icon);
                    GUI.color = Color.white;
                }
                else UiKit.DiscFill(r.ContractedBy(s * 0.25f), m.color);
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
                    picked = null;
                    pickedTile = tile;
                    pickedCorners = map.Corners(tile);
                    scroll = Vector2.zero;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    return;
                }
            }
            picked = m.obj;
            pickedTile = m.index;
            pickedCorners = null;
            scroll = Vector2.zero;
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        // ---------------- The panel ----------------

        private void Panel(Rect rect)
        {
            Widgets.DrawBoxSolidWithOutline(rect, UiKit.CardFill, UiKit.CardBorder);
            var inner = rect.ContractedBy(10f);
            if (picked != null && (picked.Destroyed || map == null || !map.TryIndex(picked.Tile, out pickedTile)))
            {
                picked = null;
                pickedTile = -1;
            }
            if (map == null || (picked == null && pickedTile < 0))
            {
                Hint(inner);
                return;
            }
            const float buttonH = 30f;
            // The button sits at the panel's foot, short of its right edge.
            var button = new Rect(inner.x, inner.yMax - buttonH - 8f, Mathf.Min(inner.width - 20f, 240f), buttonH);
            var outRect = new Rect(inner.x, inner.y, inner.width, button.y - inner.y - 8f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(panelHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f, w = viewRect.width;
            try
            {
                if (picked != null) y = Picked(picked, w, y, outRect.height);
                else y = LandPanel(pickedTile, w, y);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map panel", e);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            panelHeight = y + 6f;
            if (Widgets.ButtonText(button, "CC_Region_ShowOnWorld".Translate()))
            {
                if (picked != null) CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(picked));
                else CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(map.TileOf(pickedTile)));
            }
            TooltipHandler.TipRegion(button, "CC_Region_ShowOnWorld_Tip".Translate());
        }

        /// <summary>What the panel says before anything is picked: how to use the map, and what its colours mean.</summary>
        private void Hint(Rect r)
        {
            float y = r.y;
            y += UiKit.Paragraph(r.x, y, r.width, map == null ? failed ?? "CC_Region_NoHome".Translate().ToString() : "CC_Region_PickHint".Translate().ToString(), UiKit.Faint) + 12f;
            if (map == null) return;
            y += UiKit.SectionTitle(r.x, y, r.width, "CC_Region_Legend".Translate()) + 2f;
            foreach (var (key, colour) in Legend)
            {
                if (y + 18f > r.yMax - 10f) break;
                Widgets.DrawBoxSolid(new Rect(r.x + 2f, y + 3f, 14f, 12f), new Color32(colour.r, colour.g, colour.b, 255));
                UiKit.LabelFit(new Rect(r.x + 24f, y, r.width - 24f, 18f), key.Translate(), Color.white, GameFont.Tiny);
                y += 18f;
            }
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

        private float Picked(WorldObject wo, float w, float y, float visibleH)
        {
            int index = pickedTile;
            string title = wo is Settlement && wo.Faction == Faction.OfPlayerSilentFail && wo.Tile == map.home
                ? "CC_Region_YourColony".Translate(wo.LabelCap).Resolve()
                : wo.LabelCap;
            UiKit.LabelFit(new Rect(0f, y, w, 30f), title, UiKit.HeaderColor, GameFont.Medium);
            y += 32f;
            var f = wo.Faction;
            if (f != null && !(wo is Caravan))
            {
                y += UiKit.FactionChip(0f, y, f, 22f, w) > 0f ? 26f : 0f;
                y += UiKit.Paragraph(0f, y, w, RegionMap.StandingText(f), RegionMap.StandingColor(f)) + 2f;
                if (!f.IsPlayer && f.def != null) y += UiKit.Paragraph(0f, y, w, f.def.LabelCap, UiKit.Faint, GameFont.Tiny) + 4f;
            }
            else if (!(wo is Settlement) && wo.def != null) y += UiKit.Paragraph(0f, y, w, wo.def.LabelCap, UiKit.Faint, GameFont.Tiny) + 4f;
            y += UiKit.Paragraph(0f, y, w, Distance(wo.Tile, index), Color.white) + 6f;
            if (f != null && !f.IsPlayer) y = Known(f, w, y);
            y += UiKit.Paragraph(0f, y, w, map.TileText(index, false), UiKit.Faint, GameFont.Tiny) + 8f;
            // What other parts of Rimfolk add about another people's settlement (the colony's own get none).
            if (wo is Settlement s && (s.Faction == null || !s.Faction.IsPlayer))
                for (int i = 0; i < NeighbourPanel.Sections.Count; i++)
                {
                    float used = 0f;
                    try
                    {
                        used = NeighbourPanel.Sections[i](new Rect(0f, y, w, Mathf.Max(80f, visibleH)), s);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("region map section " + i, e);
                    }
                    finally
                    {
                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                    if (used > 0f) y += used + 6f;
                }
            else if (wo.Tile != map.home) y = Places(wo.Tile, wo, w, y);
            else if (wo is MapParent home && home.HasMap) y = HomePlaces(home.Map, w, y);
            return y;
        }

        /// <summary>What other parts of Rimfolk add about the home the map is drawn around (NeighbourPanel.HomeSections).</summary>
        private static float HomePlaces(Map home, float w, float y)
        {
            for (int i = 0; i < NeighbourPanel.HomeSections.Count; i++)
            {
                float used = 0f;
                try
                {
                    used = NeighbourPanel.HomeSections[i](new Rect(0f, y, w, 80f), home);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region map home section " + i, e);
                }
                finally
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
                if (used > 0f) y += used + 6f;
            }
            return y;
        }

        /// <summary>What other parts of Rimfolk add about a place that isn't another people's town (NeighbourPanel.PlaceSections).</summary>
        private static float Places(PlanetTile tile, WorldObject wo, float w, float y)
        {
            for (int i = 0; i < NeighbourPanel.PlaceSections.Count; i++)
            {
                float used = 0f;
                try
                {
                    used = NeighbourPanel.PlaceSections[i](new Rect(0f, y, w, 80f), tile, wo);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region map place section " + i, e);
                }
                finally
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
                if (used > 0f) y += used + 6f;
            }
            return y;
        }

        /// <summary>A tile without a settlement: what the land is and how far.</summary>
        private float LandPanel(int index, float w, float y)
        {
            UiKit.LabelFit(new Rect(0f, y, w, 30f), "CC_Region_TileTitle".Translate(), UiKit.HeaderColor, GameFont.Medium);
            y += 32f;
            y += UiKit.Paragraph(0f, y, w, Distance(map.TileOf(index), index), Color.white) + 6f;
            y += UiKit.Paragraph(0f, y, w, map.TileText(index, false), UiKit.Faint) + 6f;
            var tile = map.TileOf(index);
            if (tile.Valid && tile != map.home) y = Places(tile, null, w, y);
            return y;
        }

        /// <summary>What Rimfolk knows of a people: the world events about them, and what they remember of the colony.</summary>
        private static float Known(Faction f, float w, float y)
        {
            var lines = new List<string>();
            var ws = WorldSituations.Instance;
            if (ws != null && S.worldSituations)
                foreach (var s in ws.Running)
                    if (s.ctx?.faction == f && !s.Label.NullOrEmpty()) lines.Add("CC_Region_InTheWorld".Translate(s.Label).Resolve());
            if (FactionMemory.On && FactionMemory.Remembers(f))
            {
                int tone = FactionMemory.Instance.Tone(f);
                if (tone != 0) lines.Add("CC_Region_Remember".Translate(("CC_Memory_Tone_" + tone).Translate()).Resolve());
            }
            foreach (var l in lines) y += UiKit.Paragraph(0f, y, w, l, UiKit.WorldColor, GameFont.Small) + 2f;
            return lines.Count > 0 ? y + 4f : y;
        }

        /// <summary>How far from the colony: the game's own caravan estimate, worked out once for each place picked.</summary>
        private string Distance(PlanetTile tile, int index)
        {
            int tiles = Mathf.RoundToInt(map.TilesFromHome(index));
            if (tile == map.home) return "CC_Region_HomeTile".Translate();
            if (!ticksTo.TryGetValue(tile.tileId, out int ticks))
            {
                try
                {
                    ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.home, tile, null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region map: the caravan estimate", e);
                    ticks = -1;
                }
                ticksTo[tile.tileId] = ticks;
            }
            if (ticks > 0) return "CC_Region_Days".Translate(TextUtil.DaysString(ticks), tiles).Resolve();
            if (ticks == 0) return "CC_Region_NoWay".Translate(tiles).Resolve();
            return "CC_Region_TilesAway".Translate(tiles).Resolve();
        }
    }
}
