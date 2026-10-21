using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The panel shown while a defense plan is selected: another plan, turning and mirroring, closing the gap, the
    /// suggested spot, the building and material for each part (picked from what the colony has), clearing the site,
    /// and the cost and warnings for the spot under the mouse. Like the room planner's.
    /// </summary>
    public class Window_DefensePlanner : Window
    {
        private readonly Designator_PlanDefense des;
        private Vector2 scroll;
        private float contentHeight = 400f;
        private const float Width = 340f;
        private const float RowH = 26f;
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Warn = new Color(1f, 0.75f, 0.35f);
        private static readonly Color Short = new Color(1f, 0.45f, 0.45f);

        public Window_DefensePlanner(Designator_PlanDefense des)
        {
            this.des = des;
            layer = WindowLayer.GameUI;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            focusWhenOpened = false;
            drawShadow = false;
            soundAppear = null;
            soundClose = null;
        }

        public static void OpenFor(Designator_PlanDefense des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_DefensePlanner>();
            if (existing != null)
            {
                if (existing.des == des) return;
                existing.Close(false);
            }
            Find.WindowStack.Add(new Window_DefensePlanner(des));
        }

        public static void CloseFor(Designator_PlanDefense des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_DefensePlanner>();
            if (existing != null && existing.des == des) existing.Close(false);
        }

        protected override float Margin => 8f;
        public override Vector2 InitialSize => new Vector2(Width, 420f);

        protected override void SetInitialSizeAndPosition() => windowRect = TargetRect();

        private Rect TargetRect()
        {
            float bottom = UI.screenHeight - 35f - 6f;
            float height = Mathf.Min(contentHeight + Margin * 2f, Mathf.Max(160f, bottom - 60f));
            return new Rect(0f, bottom - height, Width, height);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (Find.DesignatorManager.SelectedDesignator != des || Find.CurrentMap == null)
            {
                Close(false);
                return;
            }
            windowRect = TargetRect();
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            if (Find.DesignatorManager.SelectedDesignator == des && GUIUtility.keyboardControl == 0) des.HandleKeys();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            var t = des.Template;
            if (map == null || t == null) return;
            var cfg = des.Config;
            var s = ColonyCouncilMod.Settings;
            Text.Font = GameFont.Small;
            var view = new Rect(0f, 0f, inRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            float y = 0f;
            float w = view.width;

            Widgets.DrawTextureFitted(new Rect(0f, y, 28f, 28f), t.Icon, 1f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(34f, y, w - 34f, 32f), t.LabelCapNow.Truncate(w - 40f));
            Text.Font = GameFont.Small;
            y += 30f;
            GUI.color = Faint;
            Widgets.Label(new Rect(0f, y, w, 22f), "CC_Def_Size".Translate(t.Width, t.Height));
            GUI.color = Color.white;
            y += 22f;
            var line = des.Line;
            if (line != null)
            {
                GUI.color = UiKit.Gold;
                y += Label(y, w, "CC_DefLine_PlanningFor".Translate(DefenseLineReview.NameOf(line, map)).Resolve());
                GUI.color = Color.white;
            }
            // Why it stands where it was put, or why nothing fitted; and what the helper warned of.
            if (!des.why.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Faint;
                y += Label(y, w, des.why) + 2f;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
            if (!des.warn.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Warn;
                y += Label(y, w, des.warn) + 2f;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Another plan
            if (Widgets.ButtonText(new Rect(0f, y, w, RowH), "CC_Def_Another".Translate()))
            {
                var opts = new List<FloatMenuOption>();
                foreach (var other in DefenseTemplateDef.All)
                {
                    var local = other;
                    string reason = other.UnavailableReason(map);
                    var opt = new FloatMenuOption(reason == null ? other.LabelCapNow : other.LabelCapNow + " (" + reason + ")",
                        reason == null ? () => des.SetTemplate(local) : (System.Action)null);
                    opts.Add(opt);
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += RowH + 3f;

            // Turning, mirroring, the gap
            float half = (w - 4f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, half, RowH), "CC_Plan_RotateLeft".Translate())) des.Rotate(-1);
            if (Widgets.ButtonText(new Rect(half + 4f, y, half, RowH), "CC_Plan_RotateRight".Translate())) des.Rotate(1);
            y += RowH + 3f;
            if (Widgets.ButtonText(new Rect(0f, y, half, RowH), cfg.mirror ? "CC_Planner_Mirrored".Translate() : "CC_Planner_Mirror".Translate()))
            {
                cfg.mirror = !cfg.mirror;
                cfg.Changed();
            }
            if (t.line >= 0)
            {
                var gapRect = new Rect(half + 4f, y + 2f, half, RowH - 4f);
                bool gap = cfg.closeGap;
                Widgets.CheckboxLabeled(gapRect, "CC_Def_CloseGap".Translate(), ref gap);
                TooltipHandler.TipRegion(gapRect, "CC_Def_CloseGap_Tip".Translate(DefensePlanner.MaxCarry));
                if (gap != cfg.closeGap)
                {
                    cfg.closeGap = gap;
                    cfg.Changed();
                }
            }
            y += RowH + 3f;
            // A plan whose walls are only its sides can do without them (raiders keep coming when the sides are open).
            if (DefenseWayCheck.WallsAreSides(t) && t.Grid.pieces.Any(p => p.role == "wall"))
            {
                var openRect = new Rect(0f, y, w, RowH - 4f);
                bool open = cfg.leaveOut.Contains("wall");
                Widgets.CheckboxLabeled(openRect, "CC_Def_SidesOpen".Translate(), ref open);
                TooltipHandler.TipRegion(openRect, "CC_Def_SidesOpen_Tip".Translate());
                if (open != cfg.leaveOut.Contains("wall")) cfg.LeaveOut("wall", open);
                y += RowH - 1f;
            }
            if (Widgets.ButtonText(new Rect(0f, y, des.HasSuggestion ? half : w, RowH), "CC_Def_FindSpot".Translate())) des.SuggestSpot();
            TooltipHandler.TipRegion(new Rect(0f, y, des.HasSuggestion ? half : w, RowH), "CC_Def_FindSpot_Tip".Translate());
            if (des.HasSuggestion && Widgets.ButtonText(new Rect(half + 4f, y, half, RowH), "CC_Planner_PlaceThere".Translate())) des.PlaceAtSuggestion();
            y += RowH + 6f;

            // What each part is built of
            y = Section(y, w, "CC_Def_Parts".Translate());
            var useRect = new Rect(w - 150f, y - 21f, 150f, 20f);
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(useRect, "CC_Planner_UseWhatWeHave".Translate()))
            {
                cfg.UseWhatWeHave();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(useRect, "CC_Planner_UseWhatWeHave_Tip".Translate());
            var pieces = des.Pieces;
            foreach (var role in t.RolesUsed)
            {
                var of = pieces.Where(p => p.role == role).ToList();
                var block = t.Grid.pieces.First(p => p.role == role);
                string caption = ("CC_DefRole_" + role).Translate().CapitalizeFirst();
                if (of.Count == 0)
                {
                    bool chosenOut = cfg.leaveOut.Contains(role);
                    GUI.color = t.Optional(role) || chosenOut ? Faint : Short;
                    y += Label(y, w, caption + ": " + (chosenOut ? "CC_Def_LeftOutByChoice" : t.Optional(role) ? "CC_Def_LeftOut" : "CC_Def_CantBuild").Translate());
                    GUI.color = Color.white;
                    continue;
                }
                var first = of[0];
                string name = first.def.LabelCap + (of.Count > 1 ? " ×" + of.Count : "");
                var localRole = role;
                y = PickRow(y, w, caption, name, first.stuff != null ? first.stuff.LabelCap + " " + MaterialCounts.Short(map, first.stuff) : null, () =>
                {
                    var opts = t.VariantsFor(localRole, map, block.w, block.h).Select(d => new FloatMenuOption(d.LabelCap, () => cfg.PickVariant(localRole, d), d)).ToList();
                    if (opts.Count > 1) Find.WindowStack.Add(new FloatMenu(opts));
                    else Messages.Message("CC_Planner_NoVariants".Translate(), MessageTypeDefOf.RejectInput, false);
                }, first.stuff == null ? (System.Action)null : () => StuffMenu(first.def, first.stuff, map, DefenseTemplateDef.UseOf(localRole), of.Count, x => cfg.PickStuff(localRole, x)),
                    first.def, first.stuff != null ? MaterialPicker.CoverageColor(MaterialPicker.Coverage(first.def, first.stuff, of.Count, map)) : (Color?)null);
            }

            // Clearing
            y = Section(y + 4f, w, "CC_Planner_Clearing".Translate());
            y = Toggle(y, w, "CC_Planner_CutPlants", ref s.planCutPlants);
            y = Toggle(y, w, "CC_Planner_MineRock", ref s.planMineRock);
            y = Toggle(y, w, "CC_Planner_ReplaceOwn", ref s.planReplaceOwn);

            // Cost and warnings under the mouse
            var plan = des.CurrentPlan;
            if (plan != null)
            {
                y = Section(y + 4f, w, "CC_Planner_Cost".Translate());
                var cost = plan.Cost();
                if (cost.Count == 0)
                {
                    GUI.color = Faint;
                    y += Label(y, w, "CC_Planner_Free".Translate());
                }
                foreach (var kv in cost.OrderByDescending(k => k.Value))
                {
                    int stored = MaterialCounts.Stored(map, kv.Key);
                    int lying = MaterialCounts.LyingAround(map, kv.Key);
                    int have = stored + lying;
                    GUI.color = MaterialPicker.CoverageColor(have >= Mathf.CeilToInt(kv.Value * MaterialPicker.Margin) ? 2 : have >= kv.Value ? 1 : 0);
                    y += Label(y, w, lying > 0
                        ? "CC_Planner_CostLineLying".Translate(kv.Key.LabelCap, kv.Value, stored, lying).Resolve()
                        : "CC_Planner_CostLine".Translate(kv.Key.LabelCap, kv.Value, stored).Resolve());
                }
                GUI.color = Warn;
                foreach (var warning in plan.warnings) y += Label(y, w, warning);
                if (!plan.CanPlace)
                {
                    GUI.color = Short;
                    y += Label(y, w, plan.FailReason ?? "");
                }
                GUI.color = Color.white;
            }

            string tip = Flavor.PickTip(t.tips, (int)(Time.realtimeSinceStartup / 12f));
            if (!tip.NullOrEmpty())
            {
                GUI.color = Faint;
                y += 4f + Label(y + 4f, w, tip);
                GUI.color = Color.white;
            }
            contentHeight = y + 4f;
            Widgets.EndScrollView();
        }

        private static float Section(float y, float w, string text)
        {
            Widgets.DrawLineHorizontal(0f, y + 2f, w);
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, y + 4f, w, 18f), text);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            return y + 22f;
        }

        private static float Label(float y, float w, string text)
        {
            float h = Text.CalcHeight(text, w);
            Widgets.Label(new Rect(0f, y, w, h), text);
            return h;
        }

        private static float PickRow(float y, float w, string caption, string thingLabel, string stuffLabel, System.Action onThing, System.Action onStuff, ThingDef icon, Color? stuffColor)
        {
            float x = 0f;
            if (icon != null)
            {
                Widgets.ThingIcon(new Rect(0f, y + 1f, 24f, 24f), icon);
                x = 28f;
            }
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x, y + 5f, 64f, RowH), caption.Truncate(62f));
            Text.Font = GameFont.Small;
            x += 66f;
            float rest = w - x;
            if (stuffLabel != null)
            {
                float a = rest * 0.5f - 2f;
                if (Widgets.ButtonText(new Rect(x, y, a, RowH - 2f), thingLabel.Truncate(a - 6f))) onThing?.Invoke();
                if (Widgets.ButtonText(new Rect(x + a + 4f, y, rest - a - 4f, RowH - 2f), stuffLabel.Truncate(rest - a - 10f), true, true, stuffColor ?? Color.white)) onStuff?.Invoke();
            }
            else if (Widgets.ButtonText(new Rect(x, y, rest, RowH - 2f), thingLabel.Truncate(rest - 6f))) onThing?.Invoke();
            return y + RowH;
        }

        private static float Toggle(float y, float w, string key, ref bool value)
        {
            bool before = value;
            var rect = new Rect(0f, y, w, RowH - 2f);
            Widgets.CheckboxLabeled(rect, key.Translate(), ref value);
            TooltipHandler.TipRegion(rect, (key + "_Tip").Translate());
            if (before != value)
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                CouncilSettings.SaveNow();
            }
            return y + RowH - 2f;
        }

        private static void StuffMenu(BuildableDef def, ThingDef current, Map map, MaterialUse use, int units, System.Action<ThingDef> set)
        {
            if (def == null || !def.MadeFromStuff) return;
            var opts = RoomPlanConfig.StuffOptions(def, map, current, use)
                .Select(x => new FloatMenuOption(x.LabelCap + "    " + MaterialCounts.Short(map, x)
                                                 + "CC_Mat_NeedHere".Translate(units * MaterialCounts.PerUnit(def, x)).Resolve(), () => set(x), x))
                .ToList();
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Plan_NoMaterials".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
