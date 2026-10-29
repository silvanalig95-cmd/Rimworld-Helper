using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>One room on the ground plan: what, where, and why there.</summary>
    public class GroundPlanRoom
    {
        public RoomTemplateDef template;
        public NextStep step;
        public TemplatePlanner.SpotCandidate spot;
        public List<IntVec3> edge = new List<IntVec3>();
        public IntVec3 mid;
        public Color color;
        public string label;
        /// <summary>No spot was found near the colony for it.</summary>
        public bool nowhere;
    }

    /// <summary>
    /// The ground plan: the rooms the colony should build next (the plan's steps with a room, then the proposals),
    /// each given a spot by the planner's own spot finder with the rooms before it kept clear, drawn together on the
    /// map with their names. Shows how the base grows if the plan is followed in order; each room opens in the
    /// planner at its spot with one click. Rules in <see cref="GroundPlanMath"/>.
    /// </summary>
    public static class GroundPlan
    {
        private static Map shownMap;
        private static List<GroundPlanRoom> rooms;

        public static bool Shown => rooms != null && shownMap != null && Find.CurrentMap == shownMap;
        public static List<GroundPlanRoom> Rooms => rooms ?? new List<GroundPlanRoom>();

        /// <summary>Works the plan out for the map and opens its window.</summary>
        public static void Show(Map map)
        {
            if (map == null) return;
            try
            {
                rooms = Compute(map);
                shownMap = map;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("ground plan", e);
                rooms = new List<GroundPlanRoom>();
                shownMap = map;
            }
            Window_GroundPlan.Open();
        }

        public static void Hide()
        {
            rooms = null;
            shownMap = null;
        }

        private static List<GroundPlanRoom> Compute(Map map)
        {
            var result = new List<GroundPlanRoom>();
            // The rooms: each step with a room to build (not one to furnish), one per kind of room.
            var steps = new List<NextStep>();
            var seenTypes = new HashSet<object>();
            foreach (var s in NextSteps.PlanFor(map).Concat(NextSteps.ProposedFor(map)))
            {
                if (s.template == null || s.upgrade || s.Done || s.Dropped) continue;
                object type = (object)RoomTypeDef.Of(s.template) ?? s.template;
                if (!seenTypes.Add(type)) continue;
                steps.Add(s);
            }
            if (steps.Count == 0) return result;
            var order = GroundPlanMath.Order(steps.Select(s => s.Planned).ToList(), steps.Select(s => s.score).ToList());
            var taken = new HashSet<IntVec3>();
            int n = 0;
            foreach (int i in order)
            {
                var step = steps[i];
                var t = step.template;
                if (t.UnavailableReason(map) != null)
                {
                    // The size the step named can't be built yet: the kind's size that can.
                    var type = RoomTypeDef.Of(t);
                    var other = type != null ? RoomMenu.Representative(type, map) : null;
                    if (other == null || other.UnavailableReason(map) != null) continue;
                    t = other;
                }
                var furniture = t.ResolveCached(map, out string reason);
                if (reason != null) continue;
                var room = new GroundPlanRoom { template = t, step = step, label = RoomMenu.Label(t) };
                var rgb = GroundPlanMath.ColorOf(n++);
                room.color = new Color(rgb.r, rgb.g, rgb.b);
                var cfg = RoomPlanConfig.For(t);
                var spots = TemplatePlanner.FindSpots(t, map, furniture, cfg, 1, taken);
                if (spots.Count == 0)
                {
                    room.nowhere = true;
                    result.Add(room);
                    continue;
                }
                room.spot = spots[0];
                TemplateMath.RotatedSize(t.FullWidth, t.FullHeight, room.spot.rot, out int rw, out int rh);
                foreach (var (x, z) in GroundPlanMath.Edge(room.spot.anchor.x, room.spot.anchor.z, rw, rh))
                {
                    var c = new IntVec3(x, 0, z);
                    if (c.InBounds(map)) room.edge.Add(c);
                }
                room.mid = new IntVec3(room.spot.anchor.x + rw / 2, 0, room.spot.anchor.z + rh / 2);
                foreach (var (x, z) in GroundPlanMath.Kept(room.spot.anchor.x, room.spot.anchor.z, rw, rh)) taken.Add(new IntVec3(x, 0, z));
                result.Add(room);
            }
            return result;
        }

        /// <summary>Opens the planner on the room, at its spot on the ground plan.</summary>
        public static void PlanHere(GroundPlanRoom r)
        {
            if (r?.template == null || r.spot == null) return;
            Designator_PlanRoom.BeginAt(r.template, r.spot);
        }
    }

    /// <summary>Draws the ground plan on the map while its window is open: each room's outline in its colour, its name in the middle.</summary>
    public class GroundPlanOverlay : MapComponent
    {
        public GroundPlanOverlay(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            if (!GroundPlan.Shown || map != Find.CurrentMap) return;
            foreach (var r in GroundPlan.Rooms)
                if (r.edge.Count > 0) GenDraw.DrawFieldEdges(r.edge, r.color);
        }

        public override void MapComponentOnGUI()
        {
            if (!GroundPlan.Shown || map != Find.CurrentMap) return;
            if (Find.CameraDriver.CurrentZoom > CameraZoomRange.Far) return;
            foreach (var r in GroundPlan.Rooms)
                if (!r.nowhere && r.mid.InBounds(map)) GenMapUI.DrawThingLabel(r.mid.ToVector3Shifted().MapToUIPosition(), r.label, r.color);
        }
    }

    /// <summary>The ground plan's rooms, in order, each with its reasons and a button that opens the planner there.</summary>
    public class Window_GroundPlan : Window
    {
        private Vector2 scroll;
        private float height = 300f;

        private const float Width = 440f, BottomH = 40f;

        public static void Open()
        {
            var existing = Find.WindowStack.WindowOfType<Window_GroundPlan>();
            if (existing != null) return;
            Find.WindowStack.Add(new Window_GroundPlan());
        }

        public Window_GroundPlan()
        {
            layer = WindowLayer.GameUI;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            draggable = true;
            soundAppear = null;
            closeOnAccept = false;
        }

        protected override float Margin => 10f;

        public override Vector2 InitialSize => new Vector2(Width, Mathf.Min(520f, UI.screenHeight - 120f));

        protected override void SetInitialSizeAndPosition()
        {
            var size = InitialSize;
            windowRect = new Rect(UI.screenWidth - size.x - 20f, 80f, size.x, size.y).Rounded();
        }

        public override void PostClose()
        {
            base.PostClose();
            GroundPlan.Hide();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            if (map == null || !GroundPlan.Shown)
            {
                Close(false);
                return;
            }
            float y = inRect.y;
            UiKit.Icon(new Rect(inRect.x, y + 2f, 24f, 24f), Icons.Plan, UiKit.Gold);
            UiKit.LabelFit(new Rect(inRect.x + 30f, y, inRect.width - 30f, 30f), "CC_Ground_Title".Translate(), Color.white, GameFont.Medium);
            y += 32f;
            Text.Font = GameFont.Small;
            y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_Ground_Sub".Translate(), UiKit.Faint) + 6f;

            var rooms = GroundPlan.Rooms;
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - BottomH - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float vy = 0f;
            if (rooms.Count == 0) vy += UiKit.Paragraph(6f, vy, view.width - 12f, "CC_Ground_Empty".Translate(), UiKit.Dim) + 6f;
            int n = 0;
            foreach (var r in rooms) vy += Row(vy, view.width, r, ++n, map) + 4f;
            height = Mathf.Max(vy + 6f, 50f);
            Widgets.EndScrollView();

            float by = inRect.yMax - 30f;
            if (Widgets.ButtonText(new Rect(inRect.x, by, 150f, 30f), "CC_Ground_Again".Translate()))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                GroundPlan.Show(map);
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, by, 110f, 30f), "CC_Ground_Close".Translate())) Close(false);
        }

        private static float Row(float y, float width, GroundPlanRoom r, int n, Map map)
        {
            Text.Font = GameFont.Small;
            string why = r.nowhere ? "CC_Ground_NoSpot".Translate().Resolve() : SpotText.Line(r.spot, 3);
            Text.Font = GameFont.Tiny;
            float whyH = Text.CalcHeight(why, width - 200f);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(56f, 24f + whyH + 10f);
            var row = new Rect(0f, y, width, h);
            UiKit.Card(row, r.color);
            Widgets.DrawBoxSolid(new Rect(10f, y + 10f, 14f, 14f), r.color);
            UiKit.LabelFit(new Rect(30f, y + 4f, width - 200f - 30f, 24f), n + ". " + r.label, Color.white);
            if (r.template?.Icon != null) GUI.DrawTexture(new Rect(width - 190f, y + 6f, 20f, 20f), r.template.Icon);
            Text.Font = GameFont.Tiny;
            GUI.color = r.nowhere ? UiKit.Warn : UiKit.Faint;
            Widgets.Label(new Rect(30f, y + 26f, width - 200f - 30f, whyH), why);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (r.spot != null)
            {
                var spot = r.spot;
                var reasons = spot.reasons;
                if (reasons != null && reasons.Count > 0)
                    TooltipHandler.TipRegion(new Rect(0f, y, width - 170f, h), string.Join("\n", reasons.Select(x => (x.good ? "+ " : "- ") + x.text)));
                if (Widgets.ButtonText(new Rect(width - 166f, y + 6f, 110f, 26f), "CC_Ground_PlanHere".Translate()))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    GroundPlan.PlanHere(r);
                }
                if (Widgets.ButtonText(new Rect(width - 52f, y + 6f, 48f, 26f), "CC_Ground_Show".Translate())) CameraJumper.TryJump(r.mid, map);
            }
            return h;
        }
    }
}
