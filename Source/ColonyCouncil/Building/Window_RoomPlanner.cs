using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The panel shown while a room plan is selected, wherever it was picked from (council tab or architect
    /// menu): turning, mirroring, door position, materials, furniture variants, clearing options, cost.
    /// </summary>
    public class Window_RoomPlanner : Window
    {
        private readonly Designator_PlanRoom des;
        private Vector2 scroll;
        private float contentHeight = 400f;
        private const float Width = 330f;
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
            if (Find.DesignatorManager.SelectedDesignator == des) des.HandleKeys();
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
            Widgets.Label(new Rect(34f, y, w - 34f, 32f), t.LabelCap);
            Text.Font = GameFont.Small;
            y += 30f;
            GUI.color = Faint;
            Widgets.Label(new Rect(0f, y, w, 22f), "CC_Planner_Size".Translate(t.size.x, t.size.z));
            GUI.color = Color.white;
            y += 22f;

            // Orientation
            float half = (w - 4f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, half, RowH), "CC_Plan_RotateLeft".Translate())) des.Rotate(-1);
            if (Widgets.ButtonText(new Rect(half + 4f, y, half, RowH), "CC_Plan_RotateRight".Translate())) des.Rotate(1);
            y += RowH + 3f;
            float third = (w - 8f) / 3f;
            if (Widgets.ButtonText(new Rect(0f, y, third, RowH), cfg.mirror ? "CC_Planner_Mirrored".Translate() : "CC_Planner_Mirror".Translate()))
            {
                cfg.mirror = !cfg.mirror;
                cfg.Changed();
            }
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
            y += RowH + 6f;

            // Materials
            y = Section(y, w, "CC_Planner_Materials".Translate());
            var walls = cfg.WallStuff(t, map);
            y = PickRow(y, w, "CC_Planner_Walls".Translate(), null, StuffLabel(walls, map), () => StuffMenu(ThingDefOf.Wall, walls, map, x =>
            {
                cfg.wallStuff = x;
                cfg.Changed();
            }));
            var doorDef = cfg.DoorDef(map);
            var doorStuff = cfg.DoorStuff(doorDef, walls, map);
            y = PickRow(y, w, "CC_Planner_Door".Translate(), doorDef.LabelCap, doorStuff != null ? StuffLabel(doorStuff, map) : null, () =>
            {
                var opts = RoomPlanConfig.DoorOptions(map).Select(d => new FloatMenuOption(d.LabelCap, () =>
                {
                    cfg.doorDef = d;
                    cfg.Changed();
                })).ToList();
                if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
            }, () => StuffMenu(doorDef, doorStuff, map, x =>
            {
                cfg.doorStuff = x;
                cfg.Changed();
            }));
            var floor = cfg.Floor(t, map);
            y = PickRow(y, w, "CC_Planner_Floor".Translate(), floor != null ? floor.LabelCap.ToString() : "CC_Planner_NoFloor".Translate().ToString(), null,
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
                        var opts = t.VariantsFor(tt, map).Select(d => new FloatMenuOption(d.LabelCap, () =>
                        {
                            cfg.variant[groupKey] = d;
                            cfg.Changed();
                        }, d)).ToList();
                        if (opts.Count > 1) Find.WindowStack.Add(new FloatMenu(opts));
                        else Messages.Message("CC_Planner_NoVariants".Translate(), MessageTypeDefOf.RejectInput, false);
                    }, first.stuff == null ? (System.Action)null : () => StuffMenu(first.def, first.stuff, map, x =>
                    {
                        cfg.stuff[groupKey] = x;
                        cfg.Changed();
                    }), first.def);
                }
            }

            // Clearing
            y = Section(y + 4f, w, "CC_Planner_Clearing".Translate());
            y = Toggle(y, w, "CC_Planner_CutPlants", ref s.planCutPlants);
            y = Toggle(y, w, "CC_Planner_MineRock", ref s.planMineRock);
            y = Toggle(y, w, "CC_Planner_ReplaceOwn", ref s.planReplaceOwn);
            y = Toggle(y, w, "CC_Planner_Smooth", ref s.planSmoothStone);

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
                    int have = map.resourceCounter.GetCount(kv.Key);
                    GUI.color = have >= kv.Value ? Color.white : Short;
                    y += Label(y, w, "CC_Planner_CostLine".Translate(kv.Key.LabelCap, kv.Value, have));
                }
                GUI.color = Warn;
                foreach (var warning in plan.warnings) y += Label(y, w, warning);
                if (!plan.Valid)
                {
                    GUI.color = Short;
                    y += Label(y, w, plan.FailReason ?? "");
                }
                GUI.color = Color.white;
            }

            // A word of advice
            if (t.tips != null && t.tips.Count > 0)
            {
                int i = (int)(Time.realtimeSinceStartup / 12f) % t.tips.Count;
                GUI.color = Faint;
                y += 4f + Label(y + 4f, w, t.tips[i]);
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

        /// <summary>A row: optional caption, a button for the thing, an optional button for its material.</summary>
        private static float PickRow(float y, float w, string caption, string thingLabel, string stuffLabel, System.Action onThing, System.Action onStuff = null, ThingDef icon = null)
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
            if (thingLabel != null && stuffLabel != null)
            {
                float a = rest * 0.5f - 2f;
                if (Widgets.ButtonText(new Rect(x, y, a, RowH - 2f), thingLabel)) onThing?.Invoke();
                if (Widgets.ButtonText(new Rect(x + a + 4f, y, rest - a - 4f, RowH - 2f), stuffLabel)) onStuff?.Invoke();
            }
            else if (Widgets.ButtonText(new Rect(x, y, rest, RowH - 2f), thingLabel ?? stuffLabel ?? "-"))
            {
                if (thingLabel != null) onThing?.Invoke();
                else (onStuff ?? onThing)?.Invoke();
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

        private static string StuffLabel(ThingDef stuff, Map map) =>
            stuff == null ? "-" : stuff.LabelCap + " (" + map.resourceCounter.GetCount(stuff) + ")";

        private static void StuffMenu(BuildableDef def, ThingDef current, Map map, System.Action<ThingDef> set)
        {
            if (def == null || !def.MadeFromStuff) return;
            var opts = RoomPlanConfig.StuffOptions(def, map, current)
                .Select(x => new FloatMenuOption(x.LabelCap + " (" + map.resourceCounter.GetCount(x) + ")", () => set(x), x))
                .ToList();
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Plan_NoMaterials".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
