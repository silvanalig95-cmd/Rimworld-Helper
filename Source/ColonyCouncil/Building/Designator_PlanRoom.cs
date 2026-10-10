using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Places a whole room template as ordinary blueprints. Shows a preview under the mouse: green walls
    /// are new, blue walls are reused, red cells are blocked, yellow cells will be mined out.
    /// Q / E rotate. The panel in the bottom-left lets you change the wall material or ask the council
    /// to find a good spot.
    /// </summary>
    public class Designator_PlanRoom : Designator
    {
        private readonly RoomTemplateDef template;
        private int rotation;
        private ThingDef wallStuff;
        private List<ResolvedThing> furniture;
        private TerrainDef floor;
        private bool useFloor = true;
        private IntVec3 suggestedAnchor = IntVec3.Invalid;
        private int suggestedRot;

        private PlacementPlan cachedPlan;
        private IntVec3 cachedCell = IntVec3.Invalid;
        private int cachedRot = -1;
        private int cachedFrame = -1;

        private static readonly Color NewWallColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color ReuseColor = new Color(0.4f, 0.7f, 1f);
        private static readonly Color BlockedColor = new Color(1f, 0.3f, 0.3f);
        private static readonly Color RockColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color SuggestColor = new Color(1f, 0.9f, 0.4f, 0.8f);

        public Designator_PlanRoom(RoomTemplateDef template)
        {
            this.template = template;
            defaultLabel = "CC_PlanRoom".Translate(template.label);
            defaultDesc = template.description;
            icon = ThingDefOf.Wall.uiIcon;
            useMouseIcon = true;
        }

        public override bool Visible => true;

        public override void Selected()
        {
            base.Selected();
            var map = Find.CurrentMap;
            if (map == null) return;
            furniture = template.Resolve(map, out _);
            wallStuff = wallStuff ?? Avail.BestStuff(ThingDefOf.Wall, map, (template.FullWidth + template.FullHeight) * 2 * ThingDefOf.Wall.CostStuffCount);
            floor = template.ResolveFloor(map);
        }

        /// <summary>Ask the planner for a good spot and jump the camera there.</summary>
        public void SuggestSpot()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            if (furniture == null) Selected();
            if (TemplatePlanner.FindSpot(template, map, furniture, wallStuff, out var anchor, out int rot))
            {
                suggestedAnchor = anchor;
                suggestedRot = rot;
                rotation = rot;
                TemplateMath.RotatedSize(template.FullWidth, template.FullHeight, rot, out int rw, out int rh);
                CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
                Messages.Message("CC_Plan_SpotFound".Translate(template.label), MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                suggestedAnchor = IntVec3.Invalid;
                Messages.Message("CC_Plan_NoSpot".Translate(template.label), MessageTypeDefOf.RejectInput, false);
            }
        }

        private PlacementPlan PlanAt(IntVec3 center)
        {
            var map = Find.CurrentMap;
            if (furniture == null) Selected();
            if (cachedPlan != null && cachedCell == center && cachedRot == rotation && Time.frameCount - cachedFrame < 30)
                return cachedPlan;
            var anchor = TemplatePlanner.AnchorForCenter(template, center, rotation);
            cachedPlan = TemplatePlanner.Plan(template, map, anchor, rotation, furniture, wallStuff, useFloor ? floor : null);
            cachedCell = center;
            cachedRot = rotation;
            cachedFrame = Time.frameCount;
            return cachedPlan;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            var plan = PlanAt(c);
            if (!plan.Valid) return plan.FailReason ?? "CC_Plan_Blocked".Translate();
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var plan = PlanAt(c);
            if (!plan.Valid) return;
            string summary = TemplatePlanner.Execute(plan, Map);
            Messages.Message(summary, new LookTargets(plan.door, Map), MessageTypeDefOf.TaskCompletion, false);
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            Find.DesignatorManager.Deselect();
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            if (map == null) return;

            if (suggestedAnchor.IsValid)
            {
                var cells = new List<IntVec3>();
                TemplateMath.RotatedSize(template.FullWidth, template.FullHeight, suggestedRot, out int rw, out int rh);
                for (int x = 0; x < rw; x++)
                    for (int z = 0; z < rh; z++)
                        cells.Add(new IntVec3(suggestedAnchor.x + x, 0, suggestedAnchor.z + z));
                GenDraw.DrawFieldEdges(cells, SuggestColor);
            }

            var mouse = UI.MouseCell();
            if (!mouse.InBounds(map)) return;
            var plan = PlanAt(mouse);
            if (plan.wallNew.Count > 0) GenDraw.DrawFieldEdges(plan.wallNew, NewWallColor);
            if (plan.wallReuse.Count > 0) GenDraw.DrawFieldEdges(plan.wallReuse, ReuseColor);
            if (plan.wallBlocked.Count > 0) GenDraw.DrawFieldEdges(plan.wallBlocked, BlockedColor);
            if (plan.interiorRock.Count > 0) GenDraw.DrawFieldEdges(plan.interiorRock, RockColor);
            if (plan.interiorBlocked.Count > 0) GenDraw.DrawFieldEdges(plan.interiorBlocked, BlockedColor);
            if (plan.door.IsValid && plan.door.InBounds(map))
                GenDraw.DrawFieldEdges(new List<IntVec3> { plan.door }, plan.doorOk ? Color.white : BlockedColor);

            foreach (var f in plan.furniture)
            {
                if (!f.cell.InBounds(map)) continue;
                var col = f.blocked ? Designator_Place.CannotPlaceColor : Designator_Place.CanPlaceColor;
                try
                {
                    GhostDrawer.DrawGhostThing(f.cell, f.rot, f.def, f.def.graphic, col, AltitudeLayer.Blueprint, null, false, f.stuff);
                }
                catch
                {
                    GenDraw.DrawFieldEdges(GenAdj.OccupiedRect(f.cell, f.rot, f.def.size).Cells.ToList(), col);
                }
            }
        }

        public override void SelectedProcessInput(Event ev)
        {
            base.SelectedProcessInput(ev);
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                rotation = TemplateMath.Norm(rotation + 1);
                Event.current.Use();
            }
            if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                rotation = TemplateMath.Norm(rotation - 1);
                Event.current.Use();
            }
        }

        public override void DrawMouseAttachments()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var plan = PlanAt(UI.MouseCell());
            string text = template.LabelCap + " (" + template.size.x + "×" + template.size.z + ")";
            if (!plan.Valid) text += "\n" + plan.FailReason;
            else if (plan.interiorRock.Count > 0) text += "\n" + "CC_Plan_WillMine".Translate(plan.interiorRock.Count);
            GenUI.DrawMouseAttachment(icon, text);
        }

        public override void DoExtraGuiControls(float leftX, float bottomY)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            float width = 220f;
            float height = 176f;
            var rect = new Rect(leftX, bottomY - height, width, height);
            Find.WindowStack.ImmediateWindow(73195521, rect, WindowLayer.GameUI, delegate
            {
                var inner = rect.AtZero().ContractedBy(7f);
                var l = new Listing_Standard();
                l.Begin(inner);
                Text.Font = GameFont.Small;
                l.Label(template.LabelCap);
                if (l.ButtonText("CC_Plan_Material".Translate(wallStuff?.LabelCap ?? "-")))
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var stuff in GenStuff.AllowedStuffsFor(ThingDefOf.Wall).OrderByDescending(s => map.resourceCounter.GetCount(s)))
                    {
                        int count = map.resourceCounter.GetCount(stuff);
                        if (count <= 0) continue;
                        var local = stuff;
                        options.Add(new FloatMenuOption(stuff.LabelCap + " (" + count + ")", () =>
                        {
                            wallStuff = local;
                            cachedPlan = null;
                        }));
                    }
                    if (options.Count == 0) options.Add(new FloatMenuOption("CC_Plan_NoMaterials".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                var half = l.GetRect(28f);
                if (Widgets.ButtonText(half.LeftHalf().ContractedBy(1f), "CC_Plan_RotateLeft".Translate()))
                {
                    rotation = TemplateMath.Norm(rotation - 1);
                    cachedPlan = null;
                }
                if (Widgets.ButtonText(half.RightHalf().ContractedBy(1f), "CC_Plan_RotateRight".Translate()))
                {
                    rotation = TemplateMath.Norm(rotation + 1);
                    cachedPlan = null;
                }
                if (l.ButtonText("CC_Plan_FindSpot".Translate())) SuggestSpot();
                if (floor != null)
                {
                    bool before = useFloor;
                    l.CheckboxLabeled("CC_Plan_Floor".Translate(floor.label), ref useFloor);
                    if (before != useFloor) cachedPlan = null;
                }
                l.End();
            }, true, false, 1f);
        }
    }
}
