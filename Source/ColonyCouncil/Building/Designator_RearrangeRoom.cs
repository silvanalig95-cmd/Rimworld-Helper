using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// "Rearrange a room": click a built room to see where the council would put its furniture, then say go. While
    /// the window is open the new places show on the map as green outlines, what stays in grey; the moves are
    /// carried out by the game's own reinstall orders (<see cref="RoomRearranger"/>).
    /// </summary>
    public class Designator_RearrangeRoom : Designator
    {
        private static readonly Color Outline = new Color(0.55f, 0.85f, 1f);
        private static readonly Color MoveColor = new Color(0.45f, 0.9f, 0.5f, 0.6f);
        private static readonly Color FromColor = new Color(0.9f, 0.6f, 0.3f, 0.5f);
        private static readonly Color StayColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);

        /// <summary>The room looked over, while its window is open.</summary>
        public RearrangePlan plan;

        public Designator_RearrangeRoom()
        {
            useMouseIcon = true;
            icon = Icons.Rooms;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public override string Label => "CC_Rearr_Button".Translate();
        public override string Desc => "CC_Rearr_Desc".Translate();

        public static void Begin()
        {
            if (Find.CurrentMap == null) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(new Designator_RearrangeRoom());
            Messages.Message("CC_Rearr_PickRoom".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            return RoomRearranger.CanDo(c.GetRoom(Map));
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var room = c.GetRoom(Map);
            if (!RoomRearranger.CanDo(room).Accepted) return;
            Look(room, Window_Rearrange.MoveStocked);
        }

        /// <summary>Looks the room over (again, with other choices) and opens or refreshes the window.</summary>
        public void Look(Room room, bool moveStocked)
        {
            try
            {
                plan = RoomRearranger.Survey(room, moveStocked);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("looking a room over", e);
                plan = null;
            }
            if (plan == null)
            {
                Messages.Message("CC_Rearr_NotARoom".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Window_Rearrange.OpenFor(this);
        }

        /// <summary>The room looked over, found again from its cells (rooms are rebuilt as the map changes).</summary>
        public Room RoomNow()
        {
            if (plan == null || plan.map == null || Find.CurrentMap != plan.map) return null;
            foreach (var c in plan.cells)
            {
                var room = c.GetRoom(plan.map);
                if (room != null && RoomRearranger.CanDo(room).Accepted) return room;
            }
            return null;
        }

        public void Go()
        {
            if (plan == null) return;
            RoomRearranger.Apply(plan);
            plan = null;
            Find.DesignatorManager.Deselect();
        }

        public override void Deselected()
        {
            base.Deselected();
            Window_Rearrange.CloseFor(this);
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            if (map == null) return;
            if (plan == null || plan.map != map)
            {
                var c = UI.MouseCell();
                if (!c.InBounds(map)) return;
                var room = c.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors || room.CellCount > 600) return;
                GenDraw.DrawFieldEdges(room.Cells.ToList(), RoomRearranger.CanDo(room).Accepted ? Outline : Color.red);
                return;
            }
            GenDraw.DrawFieldEdges(plan.cells, Outline);
            foreach (var p in plan.pieces)
            {
                if (p.thing == null) continue;
                if (p.Moves)
                {
                    GenDraw.DrawFieldEdges(p.thing.OccupiedRect().Cells.ToList(), FromColor);
                    DrawGhost(p.thing.def, p.thing.Stuff, p.to, p.toRot, MoveColor);
                }
                else if (p.Stays) GenDraw.DrawFieldEdges(p.thing.OccupiedRect().Cells.ToList(), StayColor);
            }
        }

        private static void DrawGhost(ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot, Color col)
        {
            try
            {
                GhostDrawer.DrawGhostThing(cell, rot, def, def.graphic, col, AltitudeLayer.Blueprint, null, false, stuff);
            }
            catch
            {
                GenDraw.DrawFieldEdges(GenAdj.OccupiedRect(cell, rot, def.size).Cells.ToList(), col);
            }
        }
    }

    /// <summary>What the council would move, what stays and why, and the button to go ahead.</summary>
    public class Window_Rearrange : Window
    {
        private readonly Designator_RearrangeRoom des;
        private Vector2 scroll;
        private float height = 400f;

        /// <summary>Shelves and stores with goods in them are moved too. Remembered for the session.</summary>
        public static bool MoveStocked;

        private const float Width = 460f, BottomH = 40f;

        public static void OpenFor(Designator_RearrangeRoom des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_Rearrange>();
            if (existing != null)
            {
                if (existing.des == des) return;
                existing.Close(false);
            }
            Find.WindowStack.Add(new Window_Rearrange(des));
        }

        public static void CloseFor(Designator_RearrangeRoom des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_Rearrange>();
            if (existing != null && existing.des == des) existing.Close(false);
        }

        public Window_Rearrange(Designator_RearrangeRoom des)
        {
            this.des = des;
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

        public override Vector2 InitialSize => new Vector2(Width, Mathf.Min(560f, UI.screenHeight - 120f));

        protected override void SetInitialSizeAndPosition()
        {
            var size = InitialSize;
            windowRect = new Rect(UI.screenWidth - size.x - 20f, 80f, size.x, size.y).Rounded();
        }

        public override void PostClose()
        {
            base.PostClose();
            if (Find.DesignatorManager.SelectedDesignator == des) Find.DesignatorManager.Deselect();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var plan = des.plan;
            if (plan == null)
            {
                Close(false);
                return;
            }
            float y = inRect.y;
            UiKit.Icon(new Rect(inRect.x, y + 2f, 24f, 24f), Icons.Rooms, UiKit.Gold);
            UiKit.LabelFit(new Rect(inRect.x + 30f, y, inRect.width - 30f, 30f), "CC_Rearr_Title".Translate(plan.roomLabel), Color.white, GameFont.Medium);
            y += 32f;
            Text.Font = GameFont.Small;
            int moves = plan.Moves, kept = plan.KeptCount, stays = plan.StayCount;
            string summary = moves == 0 && kept + stays == 0 ? "CC_Rearr_Nothing".Translate().Resolve()
                : "CC_Rearr_Summary".Translate(moves, kept, stays).Resolve();
            y += UiKit.Paragraph(inRect.x, y, inRect.width, summary, UiKit.Faint) + 6f;

            bool before = MoveStocked;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, inRect.width, 24f), "CC_Rearr_MoveStocked".Translate(), ref MoveStocked);
            TooltipHandler.TipRegion(new Rect(inRect.x, y, inRect.width, 24f), "CC_Rearr_MoveStockedTip".Translate());
            y += 28f;
            if (before != MoveStocked)
            {
                var room = des.RoomNow();
                if (room != null) des.Look(room, MoveStocked);
                else Close(false);
                return;
            }

            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - BottomH - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float vy = 0f;
            var unpowered = plan.Unpowered.ToList();
            if (unpowered.Count > 0)
            {
                vy += UiKit.SectionTitle(0f, vy, view.width, "CC_Rearr_Mind".Translate(), Icons.Warning, UiKit.Warn) + 2f;
                vy += UiKit.Paragraph(6f, vy, view.width - 12f, "CC_Rearr_Unpowered".Translate(unpowered.Count, string.Join(", ", unpowered.Select(p => p.thing.LabelCap.ToString()))), UiKit.Warn) + 8f;
            }
            vy += UiKit.SectionTitle(0f, vy, view.width, "CC_Rearr_Moving".Translate(moves), null, UiKit.HeaderColor) + 2f;
            if (moves == 0) vy += UiKit.Paragraph(6f, vy, view.width - 12f, "CC_Rearr_NoMoves".Translate(), UiKit.Dim) + 6f;
            foreach (var p in plan.Moving) vy += Row(vy, view.width, p, UiKit.Good, plan.map) + 2f;
            vy += 8f;
            var staying = plan.Staying.ToList();
            if (staying.Count > 0)
            {
                vy += UiKit.SectionTitle(0f, vy, view.width, "CC_Rearr_Staying".Translate(staying.Count), null, UiKit.HeaderColor) + 2f;
                foreach (var p in staying) vy += Row(vy, view.width, p, UiKit.Dim, plan.map) + 2f;
                vy += 8f;
            }
            if (kept > 0) vy += UiKit.Paragraph(6f, vy, view.width - 12f, "CC_Rearr_KeptNote".Translate(kept), UiKit.Faint) + 6f;
            height = Mathf.Max(vy + 6f, 50f);
            Widgets.EndScrollView();

            // The buttons: go ahead, call off what's still under way here, cancel.
            float by = inRect.yMax - 30f;
            var queue = plan.map.GetComponent<RearrangeQueue>();
            int underWay = queue?.CountIn(plan.rect) ?? 0;
            float bw = 150f;
            string go = "CC_Rearr_Go".Translate(moves).Resolve();
            if (Widgets.ButtonText(new Rect(inRect.x, by, bw, 30f), go, true, true, moves > 0) && moves > 0)
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                des.Go();
                Close(false);
                return;
            }
            if (underWay > 0)
            {
                var stopRect = new Rect(inRect.x + bw + 8f, by, bw, 30f);
                if (Widgets.ButtonText(stopRect, "CC_Rearr_Stop".Translate(underWay)))
                {
                    queue.Cancel(plan.rect);
                    Messages.Message("CC_Rearr_Stopped".Translate(), MessageTypeDefOf.SilentInput, false);
                }
                TooltipHandler.TipRegion(stopRect, "CC_Rearr_StopTip".Translate());
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, by, 110f, 30f), "CC_Rearr_Cancel".Translate())) Close(false);
        }

        /// <summary>One piece: its icon and name, where it goes or why it stays; click to look at it.</summary>
        private static float Row(float y, float width, RearrangePiece p, Color color, Map map)
        {
            Text.Font = GameFont.Small;
            string name = p.thing?.LabelCap ?? "?";
            string reason = p.Reason;
            Text.Font = GameFont.Tiny;
            float reasonH = Text.CalcHeight(reason, width - 40f);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(30f, 22f + reasonH + 4f);
            var r = new Rect(0f, y, width, h);
            UiKit.Card(r, color);
            if (p.thing != null) Widgets.ThingIcon(new Rect(8f, y + 3f, 24f, 24f), p.thing);
            UiKit.LabelFit(new Rect(40f, y + 2f, width - 48f, 22f), name, Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = color;
            Widgets.Label(new Rect(40f, y + 22f, width - 48f, reasonH), reason);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r) && p.thing != null)
            {
                var cell = p.Moves ? p.to : p.thing.Position;
                CameraJumper.TryJump(cell, map);
            }
            return h;
        }
    }
}
