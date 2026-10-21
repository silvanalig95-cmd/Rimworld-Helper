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
        /// <summary>Why the plan stands where it was put ("On the way to the gate, 6 cells out..."), or why nothing fitted.</summary>
        public string why;
        /// <summary>What the helper warned of before it was put there (walls that would send raiders another way).</summary>
        public string warn;

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
            if (atSpot) des.SuggestSpot(true);
        }

        /// <summary>
        /// Starts planning a defense for a line, already placed and turned where the line's step wants it: the camera goes
        /// there, the spot is outlined, and "Place it there" or a click puts it down. Nothing is built until then.
        /// </summary>
        public static void BeginAt(DefenseTemplateDef t, IntVec3 anchor, int rot, bool mirror, bool closeGap, int lineId, bool fits,
            string why = null, string warn = null, ICollection<string> leaveOut = null)
        {
            var map = Find.CurrentMap;
            if (t == null || map == null) return;
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanDefense(t) { lineId = lineId, why = why, warn = warn };
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            var cfg = des.Config;
            cfg.rotation = TemplateMath.Norm(rot);
            cfg.mirror = mirror;
            cfg.closeGap = closeGap;
            if (leaveOut != null)
                foreach (var role in leaveOut) cfg.leaveOut.Add(role);
            cfg.Changed();
            des.suggestedAnchor = anchor;
            des.suggestedRot = cfg.rotation;
            des.suggestedMirror = mirror;
            string cheaper = des.CheaperIfShort(anchor);
            TemplateMath.RotatedSize(t.Width, t.Height, cfg.rotation, out int rw, out int rh);
            CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
            var line = DefenseLines.Of(map)?.Get(lineId);
            if (line != null) DefenseLines.Select(map, line);
            string name = line != null ? DefenseLineReview.NameOf(line, map) : "";
            string text = (fits ? "CC_DefLine_PlanReady" : "CC_DefLine_PlanTight").Translate(t.LabelNow, name).Resolve();
            if (!why.NullOrEmpty()) text += " " + why;
            if (!cheaper.NullOrEmpty()) text += " " + cheaper;
            Messages.Message(text, fits && warn.NullOrEmpty() ? MessageTypeDefOf.SilentInput : MessageTypeDefOf.CautionInput, false);
        }

        /// <summary>Opens the planner for a line with nothing placed (nothing fitted where the helper would put it): the player places it by hand.</summary>
        public static void BeginBlank(DefenseTemplateDef t, DefenseLine line, IntVec3 look, string why)
        {
            var map = Find.CurrentMap;
            if (t == null || map == null) return;
            string reason = t.UnavailableReason(map);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanDefense(t) { lineId = line?.id ?? -1, why = why };
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            if (line != null)
            {
                DefenseLines.Select(map, line);
                des.Config.rotation = DefenseLineMath.Norm(line.facing);
                des.Config.Changed();
            }
            if (look.IsValid && look.InBounds(map)) CameraJumper.TryJump(look, map);
        }

        /// <summary>
        /// The first proposal: when the materials it picked (remembered from earlier plans) come to more than the colony
        /// has, each part is picked again from what's in stock, and kept if that leaves less short. Says what changed.
        /// </summary>
        public string CheaperIfShort(IntVec3 anchor)
        {
            var map = Find.CurrentMap;
            if (map == null || template == null) return null;
            try
            {
                var cfg = Config;
                var plan = PlanAtAnchor(anchor);
                var shortOf = Shortfall(plan, map);
                if (shortOf.Count == 0 || cfg.stuff.Count == 0) return null;
                var kept = new Dictionary<string, ThingDef>(cfg.stuff);
                var keptNow = new HashSet<string>(cfg.pickedNow);
                cfg.FromStockForNow();
                pieces = null;
                cachedPlan = null;
                var again = PlanAtAnchor(anchor);
                var still = Shortfall(again, map);
                if (still.Count < shortOf.Count || still.Sum(x => x.missing) < shortOf.Sum(x => x.missing))
                {
                    var now = again.Cost().Keys.Where(d => d.IsStuff).Select(d => d.label).ToList();
                    return "CC_Def_Cheaper".Translate(TextUtil.JoinAnd(shortOf.Select(x => x.def.label).ToList()), now.Count > 0 ? TextUtil.JoinAnd(now) : "?").Resolve();
                }
                // No better: the player's own picks stay.
                foreach (var kv in kept) cfg.stuff[kv.Key] = kv.Value;
                foreach (var r in keptNow) cfg.pickedNow.Add(r);
                cfg.Changed();
                pieces = null;
                cachedPlan = null;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("defense plan: cheaper materials", e);
            }
            return null;
        }

        private static List<(ThingDef def, int missing)> Shortfall(DefensePlan plan, Map map)
        {
            var list = new List<(ThingDef, int)>();
            if (plan == null) return list;
            foreach (var kv in plan.Cost())
            {
                int have = MaterialCounts.Have(map, kv.Key);
                if (have < kv.Value) list.Add((kv.Key, kv.Value - have));
            }
            return list;
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
            why = null;
            warn = null;
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

        /// <summary>
        /// Asks the survey for the spot, turns the plan to face the raiders there, and takes the camera to it. A plan in
        /// front of a gap follows the way raiders walk up to it. <paramref name="first"/>: the plan's first proposal, when
        /// materials remembered from earlier plans that the colony is short of are picked again from stock.
        /// </summary>
        public void SuggestSpot(bool first = false)
        {
            var map = Find.CurrentMap;
            if (map == null || template == null) return;
            IntVec3 anchor;
            int rot;
            bool mirror = Config.mirror, found;
            why = null;
            warn = null;
            // A plan in front of a gap follows the way raiders walk up to it.
            var line = Line;
            if (DefenseApproachPlanner.Applies(template, null))
            {
                if (line != null)
                {
                    var c = DefenseApproachPlanner.Find(map, template, Config, Pieces, line.gate, line.facing, 2, line.span);
                    found = c.fits;
                    anchor = c.anchor;
                    rot = c.rot;
                    mirror = c.mirror;
                    why = c.why;
                }
                else
                {
                    found = DefenseApproachPlanner.ForGap(map, template, Config, Pieces, choke, out anchor, out rot, out mirror, out string w);
                    why = w;
                }
            }
            else found = DefensePlanner.FindSpot(template, map, Config, Pieces, choke, out anchor, out rot);
            if (found)
            {
                suggestedAnchor = anchor;
                suggestedRot = rot;
                suggestedMirror = mirror;
                Config.rotation = rot;
                Config.mirror = mirror;
                Config.Changed();
                string cheaper = first ? CheaperIfShort(anchor) : null;
                TemplateMath.RotatedSize(template.Width, template.Height, rot, out int rw, out int rh);
                CameraJumper.TryJump(new IntVec3(anchor.x + rw / 2, 0, anchor.z + rh / 2), map);
                string text = "CC_Def_SpotFound".Translate(template.LabelNow, DefenseSurvey.FacingWord(DefenseLayout.Outward(Config.mirror, rot))).Resolve();
                if (!why.NullOrEmpty()) text += " " + why;
                if (!cheaper.NullOrEmpty()) text += " " + cheaper;
                Messages.Message(text, MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                suggestedAnchor = IntVec3.Invalid;
                Messages.Message(why.NullOrEmpty() ? "CC_Def_NoSpot".Translate(template.LabelNow).ToString() : why, MessageTypeDefOf.RejectInput, false);
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
                DefenseLines.Of(Map)?.OnPlaced(plan, lineId, Config.closeGap, Config.leaveOut);
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
