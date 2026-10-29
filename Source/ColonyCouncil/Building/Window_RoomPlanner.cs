using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The panel shown while a room plan is selected, wherever it was picked from (council tab, rooms menu, architect
    /// menu): the room's sizes and styles, turning, mirroring, door position, materials (picked from what the colony
    /// has), furniture variants, clearing options, and the cost against the stock. It also switches to upgrading a
    /// room that's already built.
    /// </summary>
    public class Window_RoomPlanner : Window
    {
        private readonly Designator_PlanRoom des;
        private Vector2 scroll;
        private float contentHeight = 400f;
        private const float Width = 340f;
        private const float RowH = 26f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Warn = new Color(1f, 0.75f, 0.35f);
        private static readonly Color Short = new Color(1f, 0.45f, 0.45f);

        public Window_RoomPlanner(Designator_PlanRoom des)
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
            doWindowBackground = true;
        }

        public static void OpenFor(Designator_PlanRoom des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_RoomPlanner>();
            if (existing != null)
            {
                if (existing.des == des) return;
                existing.Close(false);
            }
            Find.WindowStack.Add(new Window_RoomPlanner(des));
        }

        public static void CloseFor(Designator_PlanRoom des)
        {
            var existing = Find.WindowStack.WindowOfType<Window_RoomPlanner>();
            if (existing != null && existing.des == des) existing.Close(false);
        }

        public override void PostClose()
        {
            base.PostClose();
            Find.WindowStack.WindowOfType<Window_FloorPicker>()?.Close(false);
        }

        protected override float Margin => 8f;

        public override Vector2 InitialSize => new Vector2(Width, 420f);

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = TargetRect();
        }

        /// <summary>Bottom-left, above the architect menu when it's open, sized to the content.</summary>
        private Rect TargetRect()
        {
            float bottom = UI.screenHeight - 35f - 6f;
            if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Architect && MainButtonDefOf.Architect.TabWindow is MainTabWindow_Architect arch)
                bottom = UI.screenHeight - 35f - arch.WinHeight - 270f - 6f;
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

        /// <summary>Gets every keyboard event, even when the map has focus, so Q / E always turn the room.</summary>
        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            // Not while typing in a text field (a room's name, a search): Q and E are letters there.
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

            // Title
            Widgets.DrawTextureFitted(new Rect(0f, y, 28f, 28f), t.Icon, 1f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(34f, y, w - 34f, 32f), t.LabelCap.ToString().Truncate(w - 40f));
            Text.Font = GameFont.Small;
            y += 30f;
            GUI.color = Faint;
            Widgets.Label(new Rect(0f, y, w, 22f), "CC_Planner_Size".Translate(t.size.x, t.size.z));
            GUI.color = Color.white;
            y += 22f;

            y = VariantRow(y, w, t, map);
            y = StyleRow(y, w, cfg);
            y = ThemeRow(y, w, cfg, map);

            // Building new or upgrading a built room
            if (Widgets.ButtonText(new Rect(0f, y, w, RowH), des.upgradeMode ? "CC_Upgrade_Back".Translate() : "CC_Upgrade_Button".Translate()))
                des.ToggleUpgrade();
            TooltipHandler.TipRegion(new Rect(0f, y, w, RowH), "CC_Upgrade_Tip".Translate());
            y += RowH + 3f;

            // Orientation
            float half = (w - 4f) / 2f;
            float third = (w - 8f) / 3f;
            if (!des.upgradeMode)
            {
                if (Widgets.ButtonText(new Rect(0f, y, half, RowH), "CC_Plan_RotateLeft".Translate())) des.Rotate(-1);
                if (Widgets.ButtonText(new Rect(half + 4f, y, half, RowH), "CC_Plan_RotateRight".Translate())) des.Rotate(1);
                y += RowH + 3f;
            }
            if (Widgets.ButtonText(new Rect(0f, y, des.upgradeMode ? w : third, RowH), cfg.mirror ? "CC_Planner_Mirrored".Translate() : "CC_Planner_Mirror".Translate()))
            {
                cfg.mirror = !cfg.mirror;
                cfg.Changed();
            }
            if (!des.upgradeMode)
            {
                int door = cfg.DoorX(t);
                if (Widgets.ButtonText(new Rect(third + 4f, y, third, RowH), "CC_Planner_DoorLeft".Translate()) && door > 0)
                {
                    cfg.doorX = door - 1;
                    cfg.Changed();
                }
                if (Widgets.ButtonText(new Rect(2f * third + 8f, y, third, RowH), "CC_Planner_DoorRight".Translate()) && door < t.size.x - 1)
                {
                    cfg.doorX = door + 1;
                    cfg.Changed();
                }
                y += RowH + 3f;
                if (Widgets.ButtonText(new Rect(0f, y, des.HasSuggestion ? half : w, RowH), "CC_Plan_FindSpot".Translate())) des.SuggestSpot();
                if (des.HasSuggestion && Widgets.ButtonText(new Rect(half + 4f, y, half, RowH), "CC_Planner_PlaceThere".Translate())) des.PlaceAtSuggestion();
                // Where should this go: the best few spots, each with its reasons; the chosen one is outlined on the map.
                var spots = des.Suggestions;
                if (spots.Count > 0)
                {
                    y += RowH + 4f;
                    Text.Font = GameFont.Tiny;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(0f, y, w, 18f), "CC_Spot_Title".Translate());
                    GUI.color = Color.white;
                    y += 18f;
                    for (int i = 0; i < spots.Count; i++)
                    {
                        var spot = spots[i];
                        bool chosen = i == des.SuggestionIndex;
                        var row = new Rect(0f, y, w, 22f);
                        if (chosen) Widgets.DrawHighlightSelected(row);
                        else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                        string line = (i + 1) + ". " + SpotText.Line(spot);
                        UiKit.LabelFit(new Rect(4f, y, w - 70f, 22f), line, chosen ? Color.white : UiKit.Faint, GameFont.Tiny);
                        TooltipHandler.TipRegion(row, string.Join("\n", spot.reasons.Select(r => (r.good ? "+ " : "- ") + r.text.CapitalizeFirst())));
                        if (Widgets.ButtonInvisible(new Rect(0f, y, w - 70f, 22f))) des.UseSuggestion(i);
                        if (Widgets.ButtonText(new Rect(w - 66f, y + 1f, 64f, 20f), "CC_Spot_PlaceHere".Translate()))
                        {
                            des.UseSuggestion(i);
                            des.PlaceAtSuggestion();
                        }
                        y += 23f;
                    }
                    Text.Font = GameFont.Small;
                    y -= RowH + 4f;
                }
            }
            y += RowH + 6f;

            // Materials, picked from what the colony has unless the player picks one
            y = Section(y, w, "CC_Planner_Materials".Translate());
            var useRect = new Rect(w - 150f, y - 21f, 150f, 20f);
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(useRect, "CC_Planner_UseWhatWeHave".Translate()))
            {
                cfg.UseWhatWeHave();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(useRect, "CC_Planner_UseWhatWeHave_Tip".Translate());
            var walls = cfg.WallStuff(t, map);
            if (!des.upgradeMode)
            {
                int wallUnits = RoomPlanConfig.WallCount(t);
                y = PickRow(y, w, "CC_Planner_Walls".Translate(), null, StuffLabel(walls, map), () => StuffMenu(ThingDefOf.Wall, walls, map, MaterialUse.Wall, wallUnits, cfg.PickWalls),
                    stuffColor: Coverage(ThingDefOf.Wall, walls, wallUnits, map));
                var doorDef = cfg.DoorDef(map);
                var doorStuff = cfg.DoorStuff(doorDef, walls, map);
                y = PickRow(y, w, "CC_Planner_Door".Translate(), doorDef.LabelCap, doorStuff != null ? StuffLabel(doorStuff, map) : null, () =>
                {
                    var opts = RoomPlanConfig.DoorOptions(map).Select(d => new FloatMenuOption(d.LabelCap, () =>
                    {
                        cfg.doorDef = d;
                        cfg.Changed();
                    }, d)).ToList();
                    if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
                }, () => StuffMenu(doorDef, doorStuff, map, MaterialUse.Door, 1, cfg.PickDoorStuff), stuffColor: Coverage(doorDef, doorStuff, 1, map));
            }
            var floor = cfg.Floor(t, map);
            var floorStuff = cfg.FloorStuff(floor, map, t.size.x * t.size.z);
            y = PickRow(y, w, "CC_Planner_Floor".Translate(), floor != null ? GenLabel.ThingLabel(floor, floorStuff).CapitalizeFirst() : "CC_Planner_NoFloor".Translate().ToString(), null,
                () => Window_FloorPicker.Open(t, cfg, map, windowRect));

            // Furniture
            y += 4f;
            bool furn = s.planFurniture;
            Widgets.CheckboxLabeled(new Rect(0f, y, w, RowH), "CC_Planner_Furniture".Translate(), ref furn);
            if (furn != s.planFurniture)
            {
                s.planFurniture = furn;
                CouncilSettings.SaveNow();
            }
            y += RowH;
            if (s.planFurniture)
            {
                foreach (var group in des.Furniture.GroupBy(f => f.group))
                {
                    var first = group.First();
                    var tt = t.ThingInGroup(group.Key);
                    int count = group.Count();
                    string name = first.def.LabelCap + (count > 1 ? " x" + count : "");
                    var groupKey = group.Key;
                    y = PickRow(y, w, null, name, first.stuff != null ? StuffLabel(first.stuff, map) : null, () =>
                    {
                        if (tt == null) return;
                        var opts = t.VariantsFor(tt, map).Select(d => new FloatMenuOption(d.LabelCap, () => t.PickVariant(cfg, groupKey, d), d)).ToList();
                        if (opts.Count > 1) Find.WindowStack.Add(new FloatMenu(opts));
                        else Messages.Message("CC_Planner_NoVariants".Translate(), MessageTypeDefOf.RejectInput, false);
                    }, first.stuff == null ? (System.Action)null : () => StuffMenu(first.def, first.stuff, map, RoomTemplateDef.UseOf(tt), count, x => cfg.PickStuff(groupKey, x)),
                        first.def, Coverage(first.def, first.stuff, count, map));
                }
            }

            // Clearing
            if (!des.upgradeMode)
            {
                y = Section(y + 4f, w, "CC_Planner_Clearing".Translate());
                y = Toggle(y, w, "CC_Planner_CutPlants", ref s.planCutPlants);
                y = Toggle(y, w, "CC_Planner_MineRock", ref s.planMineRock);
                y = Toggle(y, w, "CC_Planner_ReplaceOwn", ref s.planReplaceOwn);
                y = Toggle(y, w, "CC_Planner_Smooth", ref s.planSmoothStone);
            }

            // The built room under the mouse, while upgrading
            if (des.upgradeMode)
            {
                y = Section(y + 4f, w, "CC_Upgrade_Room".Translate());
                var room = des.RoomUnderMouse;
                GUI.color = Faint;
                if (room == null || room.PsychologicallyOutdoors || !room.ProperRoom) y += Label(y, w, "CC_Upgrade_HoverRoom".Translate());
                else
                {
                    GUI.color = Color.white;
                    float imp = room.GetStat(RoomStatDefOf.Impressiveness);
                    var stage = RoomStatDefOf.Impressiveness.GetScoreStage(imp);
                    y += Label(y, w, "CC_Upgrade_Impressiveness".Translate(room.GetRoomRoleLabel().CapitalizeFirst(), imp.ToString("F0"), stage?.label ?? "-"));
                }
                GUI.color = Color.white;
            }

            // Cost and warnings for the spot under the mouse
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
                    GUI.color = have >= Mathf.CeilToInt(kv.Value * MaterialPicker.Margin) ? MaterialPicker.CoverageColor(2) : have >= kv.Value ? MaterialPicker.CoverageColor(1) : MaterialPicker.CoverageColor(0);
                    string line = lying > 0
                        ? "CC_Planner_CostLineLying".Translate(kv.Key.LabelCap, kv.Value, stored, lying).Resolve()
                        : "CC_Planner_CostLine".Translate(kv.Key.LabelCap, kv.Value, stored).Resolve();
                    y += Label(y, w, line);
                }
                GUI.color = Warn;
                foreach (var warning in plan.warnings) y += Label(y, w, warning);
                // Only what has to be dealt with first is red; what the ground can't hold is one of the amber warnings above.
                if (!plan.CanPlace)
                {
                    GUI.color = Short;
                    y += Label(y, w, plan.FailReason ?? "");
                }
                GUI.color = Color.white;
            }

            // A word of advice that fits the colony's era
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

        /// <summary>The room type's sizes and kinds ("cozy", "standard", "roomy"), one button each; more than four wrap to a second row.</summary>
        private float VariantRow(float y, float w, RoomTemplateDef t, Map map)
        {
            var type = RoomTypeDef.Of(t);
            if (type == null || type.Variants.Count < 2) return y;
            var vs = type.Variants;
            float gap = 3f;
            int perRow = vs.Count <= 4 ? vs.Count : (vs.Count + 1) / 2;
            int rows = (vs.Count + perRow - 1) / perRow;
            float bw = (w - gap * (perRow - 1)) / perRow;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < vs.Count; i++)
            {
                var (vt, label) = vs[i];
                var r = new Rect(i % perRow * (bw + gap), y + i / perRow * 27f, bw, 24f);
                string reason = vt.UnavailableReason(map);
                bool current = vt == t;
                if (current) Widgets.DrawHighlightSelected(r);
                var color = reason != null ? Faint : current ? new Color(1f, 0.9f, 0.55f) : Color.white;
                if (Widgets.ButtonText(r, label.CapitalizeFirst().Truncate(bw - 6f), true, true, color))
                {
                    if (reason != null) Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                    else des.SetTemplate(vt);
                }
                var tipT = vt;
                TooltipHandler.TipRegion(r, () => tipT.LabelCap + " (" + tipT.size.x + "x" + tipT.size.z + ")\n" + (tipT.description ?? "")
                                                  + (reason != null ? "\n\n" + reason : ""), tipT.GetHashCode() ^ 0x5A1);
            }
            Text.Font = GameFont.Small;
            return y + rows * 27f;
        }

        /// <summary>Spartan, comfortable or lavish.</summary>
        private float StyleRow(float y, float w, RoomPlanConfig cfg)
        {
            var styles = new[] { RoomStyle.Spartan, RoomStyle.Comfortable, RoomStyle.Lavish };
            float gap = 3f;
            float bw = (w - gap * 2f) / 3f;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < styles.Length; i++)
            {
                var style = styles[i];
                var r = new Rect(i * (bw + gap), y, bw, 24f);
                bool current = cfg.style == style;
                if (current) Widgets.DrawHighlightSelected(r);
                if (Widgets.ButtonText(r, ("CC_Style_" + style).Translate(), true, true, current ? new Color(1f, 0.9f, 0.55f) : Color.white) && !current)
                {
                    cfg.style = style;
                    cfg.Changed();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                TooltipHandler.TipRegion(r, ("CC_Style_" + style + "_Tip").Translate());
            }
            Text.Font = GameFont.Small;
            return y + 28f;
        }

        /// <summary>
        /// The look the room is built in: as designed, or a theme from a menu. A theme the colony can't use yet is shown
        /// with the reason. The tooltip says what the theme changes in this room.
        /// </summary>
        private float ThemeRow(float y, float w, RoomPlanConfig cfg, Map map)
        {
            var themes = RoomThemeDef.All;
            if (themes.Count == 0) return y;
            var r = new Rect(0f, y, w, 24f);
            Text.Font = GameFont.Tiny;
            string current = cfg.theme != null ? cfg.theme.LabelCap.ToString() : "CC_Theme_None".Translate().CapitalizeFirst().ToString();
            if (Widgets.ButtonText(r, "CC_Theme_Button".Translate(current), true, true, cfg.theme != null ? new Color(1f, 0.9f, 0.55f) : Color.white))
            {
                var opts = new List<FloatMenuOption>();
                var none = new FloatMenuOption("CC_Theme_None".Translate().CapitalizeFirst(), () => PickTheme(cfg, null));
                none.tooltip = new TipSignal("CC_Theme_None_Tip".Translate().ToString());
                opts.Add(none);
                foreach (var th in themes)
                {
                    var local = th;
                    string reason = th.UnavailableReason(map);
                    string label = reason == null ? th.LabelCap.ToString() : th.LabelCap + " (" + reason + ")";
                    System.Action act = reason == null ? () => PickTheme(cfg, local) : (System.Action)null;
                    var icon = th.IconDef;
                    var opt = icon != null ? new FloatMenuOption(label, act, icon) : new FloatMenuOption(label, act);
                    if (!th.description.NullOrEmpty()) opt.tooltip = new TipSignal(th.description);
                    opts.Add(opt);
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            TooltipHandler.TipRegion(r, () => ThemeTip(cfg), 0x7E3A51);
            Text.Font = GameFont.Small;
            return y + 28f;
        }

        private static void PickTheme(RoomPlanConfig cfg, RoomThemeDef theme)
        {
            cfg.SetTheme(theme);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        /// <summary>What themes are, and for the chosen one what it put in this room.</summary>
        private string ThemeTip(RoomPlanConfig cfg)
        {
            var lines = new List<string> { "CC_Theme_Tip".Translate().ToString() };
            var theme = cfg.theme;
            if (theme == null) return lines[0];
            lines.Add("");
            lines.Add(theme.LabelCap + ": " + theme.description);
            var swapped = (des.Furniture ?? new List<ResolvedThing>()).Where(f => f.themed).GroupBy(f => f.def)
                .Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label).ToList();
            lines.Add(swapped.Count > 0 ? "CC_Theme_Swapped".Translate(TextUtil.JoinAnd(swapped)).Resolve() : "CC_Theme_NoSwaps".Translate().Resolve());
            return string.Join("\n", lines);
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

        private static Color? Coverage(BuildableDef def, ThingDef stuff, int units, Map map)
        {
            if (def == null || stuff == null) return null;
            return MaterialPicker.CoverageColor(MaterialPicker.Coverage(def, stuff, units, map));
        }

        /// <summary>A row: optional caption, a button for the thing, an optional button for its material (colored by how well the stock covers it).</summary>
        private static float PickRow(float y, float w, string caption, string thingLabel, string stuffLabel, System.Action onThing, System.Action onStuff = null, ThingDef icon = null, Color? stuffColor = null)
        {
            float x = 0f;
            if (icon != null)
            {
                Widgets.ThingIcon(new Rect(0f, y + 1f, 24f, 24f), icon);
                x = 28f;
            }
            if (caption != null)
            {
                Widgets.Label(new Rect(x, y + 3f, 60f, RowH), caption);
                x += 62f;
            }
            float rest = w - x;
            var color = stuffColor ?? Color.white;
            if (thingLabel != null && stuffLabel != null)
            {
                float a = rest * 0.5f - 2f;
                if (Widgets.ButtonText(new Rect(x, y, a, RowH - 2f), thingLabel.Truncate(a - 6f))) onThing?.Invoke();
                if (Widgets.ButtonText(new Rect(x + a + 4f, y, rest - a - 4f, RowH - 2f), stuffLabel.Truncate(rest - a - 10f), true, true, color)) onStuff?.Invoke();
            }
            else if (thingLabel == null && stuffLabel != null)
            {
                if (Widgets.ButtonText(new Rect(x, y, rest, RowH - 2f), stuffLabel.Truncate(rest - 6f), true, true, color)) (onStuff ?? onThing)?.Invoke();
            }
            else if (Widgets.ButtonText(new Rect(x, y, rest, RowH - 2f), (thingLabel ?? "-").Truncate(rest - 6f)))
            {
                onThing?.Invoke();
            }
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

        /// <summary>"Granite blocks 420 (+85 lying around)": stored and lying around, like the build menu shows it.</summary>
        private static string StuffLabel(ThingDef stuff, Map map) =>
            stuff == null ? "-" : stuff.LabelCap + " " + MaterialCounts.Short(map, stuff);

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
