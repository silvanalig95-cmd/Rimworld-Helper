using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Places a whole room template as ordinary blueprints. Shows a preview under the mouse: green walls
    /// are new, blue walls are reused, red cells are blocked, yellow cells will be mined, grey pieces wait
    /// until the way is clear. While it is selected a planner panel (<see cref="Window_RoomPlanner"/>)
    /// offers the room's sizes and styles, rotation, mirroring, the door position, materials and furniture variants.
    /// In upgrade mode it furnishes a room that's already built instead: hover a room, click, and only what's
    /// missing is planned.
    /// </summary>
    public class Designator_PlanRoom : Designator
    {
        protected RoomTemplateDef template;

        private List<ResolvedThing> furniture;
        private int furnitureVersion = -1;
        private float furnitureTime = -999f;
        private IntVec3 suggestedAnchor = IntVec3.Invalid;
        private int suggestedRot;
        private bool suggestedMirror;

        private PlacementPlan cachedPlan;
        private IntVec3 cachedAnchor = IntVec3.Invalid;
        private Room cachedRoom;
        private int cachedVersion = -1;
        private int cachedSettingsHash;
        private int cachedFrame = -1;
        private int lastRotateFrame = -1;

        /// <summary>Furnish a built room instead of building a new one.</summary>
        public bool upgradeMode;

        private static readonly Color NewWallColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color ReuseColor = new Color(0.4f, 0.7f, 1f);
        private static readonly Color BlockedColor = new Color(1f, 0.3f, 0.3f);
        private static readonly Color RockColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color WaitingColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
        private static readonly Color PresentColor = new Color(0.55f, 0.75f, 1f, 0.35f);
        private static readonly Color SuggestColor = new Color(1f, 0.9f, 0.4f, 0.8f);
        private static readonly Color RoomColor = new Color(0.55f, 0.85f, 1f);
        /// <summary>Left out because the ground can't hold it (water, marsh, mud): amber, a warning rather than a block.</summary>
        private static readonly Color SkipColor = new Color(1f, 0.6f, 0.2f);

        public Designator_PlanRoom(RoomTemplateDef template)
        {
            this.template = template;
            Init();
        }

        private void Init()
        {
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public virtual RoomTemplateDef Template => template;

        /// <summary>Starts planning a room: closes any open tab and selects the plan, optionally at a suggested spot.</summary>
        public static void Begin(RoomTemplateDef t, bool findSpot, bool upgrade = false)
        {
            var map = Find.CurrentMap;
            if (t == null || map == null) return;
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanRoom(t) { upgradeMode = upgrade };
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            if (findSpot && !upgrade) des.SuggestSpot();
            if (upgrade) Messages.Message("CC_Upgrade_Pick".Translate(t.label), MessageTypeDefOf.SilentInput, false);
        }

        public RoomPlanConfig Config => RoomPlanConfig.For(Template);

        public override string Label => Template != null ? Template.FullLabel : "?";
        public override string Desc => Template == null ? "" : Template.description + (Template.purpose.NullOrEmpty() ? "" : "\n\n" + Template.purpose);
        public override bool Visible => Template != null;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (Template != null) icon = Template.Icon;
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        public override void Selected()
        {
            base.Selected();
            if (Template == null) return;
            icon = Template.Icon;
            furniture = null;
            cachedPlan = null;
            // Earlier material picks count again only while there's still enough of them.
            Config.StartSession();
            Window_RoomPlanner.OpenFor(this);
        }

        public override void Deselected()
        {
            base.Deselected();
            Window_RoomPlanner.CloseFor(this);
        }

        /// <summary>Switches to another size or kind of the same room; the choices made so far carry over.</summary>
        public void SetTemplate(RoomTemplateDef t)
        {
            if (t == null || t == template) return;
            template = t;
            icon = t.Icon;
            furniture = null;
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            Config.doorX = -1;
            Config.Changed();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        public void ToggleUpgrade()
        {
            upgradeMode = !upgradeMode;
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            Config.Changed();
            if (upgradeMode) Messages.Message("CC_Upgrade_Pick".Translate(Template.label), MessageTypeDefOf.SilentInput, false);
        }

        public List<ResolvedThing> Furniture
        {
            get
            {
                var map = Find.CurrentMap;
                float now = Time.realtimeSinceStartup;
                if (furniture == null || furnitureVersion != Config.version || now - furnitureTime > 3f)
                {
                    furniture = map != null ? Template.Resolve(map, out _, Config) : new List<ResolvedThing>();
                    furnitureVersion = Config.version;
                    furnitureTime = now;
                }
                return furniture;
            }
        }

        public void Rotate(int dir)
        {
            if (Time.frameCount == lastRotateFrame) return;
            lastRotateFrame = Time.frameCount;
            Config.rotation = TemplateMath.Norm(Config.rotation + dir);
            Config.Changed();
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
        }

        public bool HasSuggestion => suggestedAnchor.IsValid;

        /// <summary>Ask the planner for a good spot and jump the camera there.</summary>
        public void SuggestSpot()
        {
            var map = Find.CurrentMap;
            if (map == null || Template == null) return;
            upgradeMode = false;
            if (TemplatePlanner.FindSpot(Template, map, Furniture, Config, out var anchor, out int rot))
            {
                suggestedAnchor = anchor;
                suggestedRot = rot;
                suggestedMirror = Config.mirror;
                // The panel, the preview and the blueprints all use this turn from now on.
                Config.rotation = rot;
                Config.Changed();
                TemplateMath.RotatedSize(Template.FullWidth, Template.FullHeight, rot, out int rw, out int rh);
                CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
                Messages.Message("CC_Plan_SpotFound".Translate(Template.label), MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                suggestedAnchor = IntVec3.Invalid;
                Messages.Message("CC_Plan_NoSpot".Translate(Template.label), MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>Places the room at the suggested spot (as found, whatever the mouse is doing).</summary>
        public void PlaceAtSuggestion()
        {
            if (!suggestedAnchor.IsValid) return;
            Config.rotation = suggestedRot;
            Config.mirror = suggestedMirror;
            Config.Changed();
            var plan = PlanAtAnchor(suggestedAnchor);
            if (plan.CanPlace) Place(plan);
            else Messages.Message(plan.FailReason ?? "CC_Plan_Blocked".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        private static int SettingsHash()
        {
            var s = ColonyCouncilMod.Settings;
            return (s.planCutPlants ? 1 : 0) | (s.planMineRock ? 2 : 0) | (s.planReplaceOwn ? 4 : 0) | (s.planSmoothStone ? 8 : 0) | (s.planFurniture ? 16 : 0);
        }

        public PlacementPlan PlanAtAnchor(IntVec3 anchor)
        {
            var map = Find.CurrentMap;
            int hash = SettingsHash();
            if (cachedPlan != null && !cachedPlan.upgrade && cachedAnchor == anchor && cachedVersion == Config.version && cachedSettingsHash == hash && Time.frameCount - cachedFrame < 30)
                return cachedPlan;
            cachedPlan = TemplatePlanner.Plan(Template, map, anchor, Config, Furniture);
            cachedAnchor = anchor;
            cachedRoom = null;
            cachedVersion = Config.version;
            cachedSettingsHash = hash;
            cachedFrame = Time.frameCount;
            return cachedPlan;
        }

        /// <summary>The upgrade plan for the built room under a cell (its own door decides how the template is turned).</summary>
        public PlacementPlan PlanUpgradeAt(IntVec3 c)
        {
            var map = Find.CurrentMap;
            var room = c.InBounds(map) ? c.GetRoom(map) : null;
            int hash = SettingsHash();
            if (cachedPlan != null && cachedPlan.upgrade && cachedRoom == room && cachedVersion == Config.version && cachedSettingsHash == hash && Time.frameCount - cachedFrame < 60)
                return cachedPlan;
            cachedPlan = TemplatePlanner.PlanUpgrade(Template, map, room, Config, door =>
            {
                if (door < 0 || door == Config.DoorX(Template)) return Furniture;
                // Lay the furniture out around the room's own door.
                var copy = Config.WithDoor(door);
                return Template.Resolve(map, out _, copy);
            });
            cachedRoom = room;
            cachedAnchor = IntVec3.Invalid;
            cachedVersion = Config.version;
            cachedSettingsHash = hash;
            cachedFrame = Time.frameCount;
            return cachedPlan;
        }

        public PlacementPlan PlanAt(IntVec3 center) =>
            upgradeMode ? PlanUpgradeAt(center) : PlanAtAnchor(TemplatePlanner.AnchorForCenter(Template, center, Config.rotation));

        /// <summary>The plan under the mouse, for the panel's cost readout.</summary>
        public PlacementPlan CurrentPlan
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null || Template == null) return null;
                var mouse = UI.MouseCell();
                if (!mouse.InBounds(map)) return cachedPlan;
                return PlanAt(mouse);
            }
        }

        /// <summary>The built room under the mouse while upgrading.</summary>
        public Room RoomUnderMouse
        {
            get
            {
                var map = Find.CurrentMap;
                var mouse = UI.MouseCell();
                return map != null && mouse.InBounds(map) ? mouse.GetRoom(map) : null;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (Template == null || !c.InBounds(Map)) return false;
            var plan = PlanAt(c);
            if (!plan.CanPlace) return plan.FailReason ?? "CC_Plan_Blocked".Translate();
            if (plan.upgrade && !plan.furniture.Any(f => f.state == PieceState.Ready || f.state == PieceState.Waiting) && plan.floorCells <= 0)
                return "CC_Upgrade_Nothing".Translate();
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var plan = PlanAt(c);
            if (!plan.CanPlace) return;
            Place(plan);
        }

        private void Place(PlacementPlan plan)
        {
            string summary = TemplatePlanner.Execute(plan, Map);
            var look = plan.door.IsValid ? new LookTargets(plan.door, Map) : plan.upgradeRoom != null ? new LookTargets(plan.upgradeRoom.Cells.First(), Map) : LookTargets.Invalid;
            // Something left out for the ground: still placed, but said in the warning color.
            Messages.Message(summary, look, plan.LeavesOutGround ? MessageTypeDefOf.CautionInput : MessageTypeDefOf.TaskCompletion, false);
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            // Stays selected, like a normal build order: place another, or right-click to stop.
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            if (map == null || Template == null) return;

            if (suggestedAnchor.IsValid && !upgradeMode)
            {
                var cells = new List<IntVec3>();
                TemplateMath.RotatedSize(Template.FullWidth, Template.FullHeight, suggestedRot, out int rw, out int rh);
                for (int x = 0; x < rw; x++)
                    for (int z = 0; z < rh; z++)
                        cells.Add(new IntVec3(suggestedAnchor.x + x, 0, suggestedAnchor.z + z));
                GenDraw.DrawFieldEdges(cells, SuggestColor);
            }

            var queue = map.GetComponent<CouncilPlanQueue>();
            if (queue != null)
            {
                foreach (var q in queue.queue)
                {
                    if (q.thing != null) DrawGhost(q.thing, q.stuff, q.cell, q.rot, WaitingColor);
                }
            }

            var mouse = UI.MouseCell();
            if (!mouse.InBounds(map)) return;
            var plan = PlanAt(mouse);
            if (plan.upgrade)
            {
                var room = plan.upgradeRoom;
                if (room != null && !room.PsychologicallyOutdoors && room.CellCount <= 800)
                    GenDraw.DrawFieldEdges(room.Cells.ToList(), plan.CanPlace ? RoomColor : BlockedColor);
            }
            if (plan.wallNew.Count > 0) GenDraw.DrawFieldEdges(plan.wallNew, NewWallColor);
            if (plan.wallReuse.Count > 0) GenDraw.DrawFieldEdges(plan.wallReuse, ReuseColor);
            if (plan.wallWaiting.Count > 0) GenDraw.DrawFieldEdges(plan.wallWaiting, RockColor);
            if (plan.wallBlocked.Count > 0) GenDraw.DrawFieldEdges(plan.wallBlocked, BlockedColor);
            if (plan.wallSkipped.Count > 0) GenDraw.DrawFieldEdges(plan.wallSkipped, SkipColor);
            if (plan.interiorSkipped.Count > 0) GenDraw.DrawFieldEdges(plan.interiorSkipped, SkipColor);
            if (plan.mine.Count > 0) GenDraw.DrawFieldEdges(plan.mine, RockColor);
            if (plan.interiorBlocked.Count > 0) GenDraw.DrawFieldEdges(plan.interiorBlocked, BlockedColor);
            if (!plan.upgrade && plan.door.IsValid && plan.door.InBounds(map))
            {
                var doorColor = plan.doorSkipped ? SkipColor : plan.doorState == PieceState.Blocked ? BlockedColor : plan.doorState == PieceState.Waiting ? RockColor : Color.white;
                GenDraw.DrawFieldEdges(new List<IntVec3> { plan.door }, doorColor);
                // Turned with its wall: a door in a west or east wall is drawn upright, the way the game will build it.
                if (!plan.doorExists && plan.doorState != PieceState.Blocked) DrawGhost(plan.doorDef, plan.doorStuff, plan.door, plan.DoorRot, Designator_Place.CanPlaceColor);
            }
            foreach (var f in plan.furniture)
            {
                if (!f.cell.InBounds(map)) continue;
                if (f.state == PieceState.Present)
                {
                    GenDraw.DrawFieldEdges(f.rect.Cells.Where(c => c.InBounds(map)).ToList(), PresentColor);
                    continue;
                }
                var col = f.state == PieceState.Blocked ? Designator_Place.CannotPlaceColor
                    : f.state == PieceState.Waiting ? WaitingColor : Designator_Place.CanPlaceColor;
                DrawGhost(f.def, f.stuff, f.cell, f.rot, col);
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

        public override void SelectedProcessInput(Event ev)
        {
            base.SelectedProcessInput(ev);
            HandleKeys();
        }

        /// <summary>Q / E rotate. Called from both the designator and the planner panel; the frame guard keeps it to one turn per press.</summary>
        public void HandleKeys()
        {
            if (Event.current == null || Event.current.type != EventType.KeyDown) return;
            if (upgradeMode) return;
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                Rotate(1);
                Event.current.Use();
            }
            else if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                Rotate(-1);
                Event.current.Use();
            }
        }

        public override void DrawMouseAttachments()
        {
            var map = Find.CurrentMap;
            if (map == null || Template == null) return;
            var plan = PlanAt(UI.MouseCell());
            string text = Template.FullLabel.CapitalizeFirst() + " (" + Template.size.x + "x" + Template.size.z + ")";
            if (plan.upgrade)
            {
                text = "CC_Upgrade_Mouse".Translate(Template.FullLabel).CapitalizeFirst();
                if (!plan.CanPlace) text += "\n" + plan.FailReason;
                else
                {
                    int add = plan.furniture.Count(f => f.state == PieceState.Ready || f.state == PieceState.Waiting);
                    int have = plan.furniture.Count(f => f.state == PieceState.Present);
                    text += "\n" + "CC_Upgrade_Counts".Translate(add, have);
                }
            }
            else if (!plan.CanPlace) text += "\n" + plan.FailReason;
            else
            {
                if (plan.LeavesOutGround) text += "\n" + "CC_Plan_Mouse_Skips".Translate();
                if (plan.mine.Count > 0) text += "\n" + "CC_Plan_WillMine".Translate(plan.mine.Count);
            }
            GenUI.DrawMouseAttachment(icon, text);
        }
    }
}
