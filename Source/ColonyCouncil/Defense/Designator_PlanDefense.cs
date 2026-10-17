using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Places a defense template as ordinary blueprints, like a room plan: a preview under the mouse (green can go down,
    /// grey waits for mining or something taken down, red is in the way, blue is already there, amber is the wall
    /// carried on to close the gap), Q and E to turn it, and a panel (<see cref="Window_DefensePlanner"/>) with the
    /// buildings and materials, closing the gap, the cost, and placing it at the spot the survey suggests, turned to
    /// face the raiders. Nothing is placed until the player clicks.
    /// </summary>
    public class Designator_PlanDefense : Designator
    {
        private DefenseTemplateDef template;
        private List<DefensePiece> pieces;
        private int piecesVersion = -1;
        private float piecesTime = -999f;
        private IntVec3 suggestedAnchor = IntVec3.Invalid;
        private int suggestedRot;
        private bool suggestedMirror;
        /// <summary>The choke point the planner was opened for (from a piece of advice), or -1 for the best.</summary>
        public int choke = -1;
        /// <summary>The defense line this plan is for (opened from a line's step), or -1: the plan joins it when placed.</summary>
        public int lineId = -1;

        private DefensePlan cachedPlan;
        private IntVec3 cachedAnchor = IntVec3.Invalid;
        private int cachedVersion = -1, cachedSettings, cachedFrame = -1, lastRotateFrame = -1;

        private static readonly Color PresentColor = new Color(0.55f, 0.75f, 1f, 0.5f);
        private static readonly Color WaitingColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
        private static readonly Color ClearColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color FiringColor = new Color(0.55f, 0.9f, 0.55f, 0.55f);
        private static readonly Color GapColor = new Color(1f, 0.75f, 0.3f);
        private static readonly Color SuggestColor = new Color(1f, 0.9f, 0.4f, 0.8f);
        private static readonly Color RockColor = new Color(1f, 0.85f, 0.3f);

        public Designator_PlanDefense(DefenseTemplateDef template)
        {
            this.template = template;
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public DefenseTemplateDef Template => template;
        public DefensePlanConfig Config => DefensePlanConfig.For(template);

        public override string Label => template?.LabelCapNow ?? "?";
        public override string Desc => template == null ? "" : template.description + (template.purpose.NullOrEmpty() ? "" : "\n\n" + template.purpose);
        public override bool Visible => template != null;

        /// <summary>
        /// Starts planning a defense: closes the council window and selects the plan; with <paramref name="atSpot"/>, the
        /// camera goes to the suggested spot (the choke point the advice was about, or the best one) and the plan is turned to face it.
        /// </summary>
        public static void Begin(DefenseTemplateDef t, bool atSpot, int choke = -1)
        {
            var map = Find.CurrentMap;
            if (t == null || map == null) return;
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanDefense(t) { choke = choke };
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            if (atSpot) des.SuggestSpot();
        }

        /// <summary>
        /// Starts planning a defense for a line, already placed and turned where the line's step wants it: the camera goes
        /// there, the spot is outlined, and "Place it there" or a click puts it down. Nothing is built until then.
        /// </summary>
        public static void BeginAt(DefenseTemplateDef t, IntVec3 anchor, int rot, bool mirror, bool closeGap, int lineId, bool fits)
        {
            var map = Find.CurrentMap;
            if (t == null || map == null) return;
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanDefense(t) { lineId = lineId };
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            var cfg = des.Config;
            cfg.rotation = TemplateMath.Norm(rot);
            cfg.mirror = mirror;
            cfg.closeGap = closeGap;
            cfg.Changed();
            des.suggestedAnchor = anchor;
            des.suggestedRot = cfg.rotation;
            des.suggestedMirror = mirror;
            TemplateMath.RotatedSize(t.Width, t.Height, cfg.rotation, out int rw, out int rh);
            CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
            var line = DefenseLines.Of(map)?.Get(lineId);
            string name = line != null ? DefenseLineReview.NameOf(line, map) : "";
            Messages.Message((fits ? "CC_DefLine_PlanReady" : "CC_DefLine_PlanTight").Translate(t.LabelNow, name).Resolve(),
                fits ? MessageTypeDefOf.SilentInput : MessageTypeDefOf.CautionInput, false);
        }

        /// <summary>The line this plan is for, if any.</summary>
        public DefenseLine Line => lineId < 0 ? null : DefenseLines.Of(Find.CurrentMap)?.Get(lineId);

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (template != null) icon = template.Icon;
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        public override void Selected()
        {
            base.Selected();
            if (template == null) return;
            icon = template.Icon;
            pieces = null;
            cachedPlan = null;
            Config.StartSession();
            Window_DefensePlanner.OpenFor(this);
            DefenseOverlay.planning = true;
        }

        public override void Deselected()
        {
            base.Deselected();
            Window_DefensePlanner.CloseFor(this);
            DefenseOverlay.planning = false;
        }

        public void SetTemplate(DefenseTemplateDef t)
        {
            if (t == null || t == template) return;
            template = t;
            icon = t.Icon;
            pieces = null;
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            Config.StartSession();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        /// <summary>The template's buildings and materials for this colony, worked out again when the choices change or every few seconds.</summary>
        public List<DefensePiece> Pieces
        {
            get
            {
                var map = Find.CurrentMap;
                float now = Time.realtimeSinceStartup;
                if (pieces == null || piecesVersion != Config.version || now - piecesTime > 3f)
                {
                    pieces = map != null ? template.Resolve(map, out _, Config) : new List<DefensePiece>();
                    piecesVersion = Config.version;
                    piecesTime = now;
                }
                return pieces;
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

        /// <summary>Asks the survey for the spot, turns the plan to face the raiders there, and takes the camera to it.</summary>
        public void SuggestSpot()
        {
            var map = Find.CurrentMap;
            if (map == null || template == null) return;
            if (DefensePlanner.FindSpot(template, map, Config, Pieces, choke, out var anchor, out int rot))
            {
                suggestedAnchor = anchor;
                suggestedRot = rot;
                suggestedMirror = Config.mirror;
                Config.rotation = rot;
                Config.Changed();
                TemplateMath.RotatedSize(template.Width, template.Height, rot, out int rw, out int rh);
                CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
                Messages.Message("CC_Def_SpotFound".Translate(template.LabelNow, DefenseSurvey.FacingWord(DefenseLayout.Outward(Config.mirror, rot))), MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                suggestedAnchor = IntVec3.Invalid;
                Messages.Message("CC_Def_NoSpot".Translate(template.LabelNow), MessageTypeDefOf.RejectInput, false);
            }
        }

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
            return (s.planCutPlants ? 1 : 0) | (s.planMineRock ? 2 : 0) | (s.planReplaceOwn ? 4 : 0);
        }

        public DefensePlan PlanAtAnchor(IntVec3 anchor)
        {
            int hash = SettingsHash();
            if (cachedPlan != null && cachedAnchor == anchor && cachedVersion == Config.version && cachedSettings == hash && Time.frameCount - cachedFrame < 30)
                return cachedPlan;
            cachedPlan = DefensePlanner.Plan(template, Find.CurrentMap, anchor, Config, Pieces);
            cachedAnchor = anchor;
            cachedVersion = Config.version;
            cachedSettings = hash;
            cachedFrame = Time.frameCount;
            return cachedPlan;
        }

        public DefensePlan PlanAt(IntVec3 center) => PlanAtAnchor(DefensePlanner.AnchorForCenter(template, center, Config.rotation));

        public DefensePlan CurrentPlan
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null || template == null) return null;
                var mouse = UI.MouseCell();
                return mouse.InBounds(map) ? PlanAt(mouse) : cachedPlan;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (template == null || !c.InBounds(Map)) return false;
            var plan = PlanAt(c);
            if (!plan.CanPlace) return plan.FailReason ?? "CC_Plan_Blocked".Translate();
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var plan = PlanAt(c);
            if (plan.CanPlace) Place(plan);
        }

        private void Place(DefensePlan plan)
        {
            string summary = DefensePlanner.Execute(plan, Map);
            var first = plan.pieces.FirstOrDefault(p => p.state != PieceState.Blocked);
            Messages.Message(summary, first != null ? new LookTargets(first.cell, Map) : LookTargets.Invalid, MessageTypeDefOf.TaskCompletion, false);
            cachedPlan = null;
            suggestedAnchor = IntVec3.Invalid;
            try
            {
                DefenseLines.Of(Map)?.OnPlaced(plan, lineId);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("defense line: a plan placed", e);
            }
            // The survey sees the new blueprints as soon as they're built; the page asks again then.
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            if (map == null || template == null) return;
            if (suggestedAnchor.IsValid)
            {
                var cells = new List<IntVec3>();
                TemplateMath.RotatedSize(template.Width, template.Height, suggestedRot, out int rw, out int rh);
                for (int x = 0; x < rw; x++)
                    for (int z = 0; z < rh; z++)
                        cells.Add(new IntVec3(suggestedAnchor.x + x, 0, suggestedAnchor.z + z));
                GenDraw.DrawFieldEdges(cells, SuggestColor);
            }
            var mouse = UI.MouseCell();
            if (!mouse.InBounds(map)) return;
            var plan = PlanAt(mouse);
            if (plan.clear.Count > 0) GenDraw.DrawFieldEdges(plan.clear, ClearColor);
            if (plan.firing.Count > 0) GenDraw.DrawFieldEdges(plan.firing, FiringColor);
            if (plan.mine.Count > 0) GenDraw.DrawFieldEdges(plan.mine, RockColor);
            if (plan.clearBlocked.Count > 0) GenDraw.DrawFieldEdges(plan.clearBlocked, Designator_Place.CannotPlaceColor);
            foreach (var p in plan.pieces)
            {
                if (!p.cell.InBounds(map)) continue;
                if (p.state == PieceState.Present)
                {
                    GenDraw.DrawFieldEdges(p.rect.Cells.Where(c => c.InBounds(map)).ToList(), PresentColor);
                    continue;
                }
                var col = p.state == PieceState.Blocked ? Designator_Place.CannotPlaceColor
                    : p.state == PieceState.Waiting ? WaitingColor : Designator_Place.CanPlaceColor;
                DrawGhost(p.def, p.stuff, p.cell, p.rot, col);
            }
            if (plan.extension.Count > 0) GenDraw.DrawFieldEdges(plan.extension, GapColor);
            // Which way it faces: an arrow from the middle toward the raiders.
            TemplateMath.RotatedSize(template.Width, template.Height, plan.rotation, out int w, out int h);
            var mid = new IntVec3(plan.anchor.x + w / 2, 0, plan.anchor.z + h / 2);
            var tip = mid + IntVec3.North.RotatedBy(new Rot4(plan.Outward)) * (Mathf.Max(w, h) / 2 + 3);
            GenDraw.DrawLineBetween(mid.ToVector3Shifted(), tip.ToVector3Shifted(), SimpleColor.Orange);
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

        public void HandleKeys()
        {
            if (Event.current == null || Event.current.type != EventType.KeyDown) return;
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
            if (map == null || template == null) return;
            var plan = PlanAt(UI.MouseCell());
            string text = template.LabelCapNow + " — " + "CC_Def_Faces".Translate(DefenseSurvey.FacingWord(plan.Outward));
            if (!plan.CanPlace) text += "\n" + plan.FailReason;
            else
            {
                if (plan.extension.Count > 0) text += "\n" + "CC_Def_Mouse_Gap".Translate(plan.extension.Count);
                if (plan.mine.Count > 0) text += "\n" + "CC_Plan_WillMine".Translate(plan.mine.Count);
            }
            GenUI.DrawMouseAttachment(icon, text);
        }
    }
}
