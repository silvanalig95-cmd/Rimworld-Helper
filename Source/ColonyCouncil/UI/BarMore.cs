using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The bottom bar's buttons the player doesn't use, put away under one More button (three dots, just before the
    /// game's menu). Right-click a button on the bar to put it away; More lists the ones put away (click to open, the
    /// pin to put one back on the bar) and "Arrange the bottom bar", a window with every button. Which are put away is
    /// a setting (CouncilSettings.barPutAway, by def name), the same in every save. A button put away still answers to
    /// its key. Needs the deeper game hooks: the buttons are hidden by asking each button whether it shows
    /// (Patch_BarPutAway), so a mod's own switch for its button still works as before.
    /// </summary>
    public static class BarMore
    {
        /// <summary>Set while Rimfolk asks a button whether it would show if it weren't put away.</summary>
        internal static bool asking;

        private static List<string> cachedList;
        private static int cachedCount = -1;
        private static readonly HashSet<string> away = new HashSet<string>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static MainButtonDef MoreDef => CouncilDefOf.Rimfolk_MoreButton;

        /// <summary>More is on the bar, and buttons can be put away under it.</summary>
        public static bool On => RimfolkPatches.On && (S?.barMore ?? false) && MoreDef != null;

        private static HashSet<string> Away
        {
            get
            {
                var list = S?.barPutAway;
                if (list != cachedList || (list?.Count ?? -1) != cachedCount)
                {
                    away.Clear();
                    if (list != null) away.UnionWith(list);
                    cachedList = list;
                    cachedCount = list?.Count ?? -1;
                }
                return away;
            }
        }

        public static bool IsAway(MainButtonDef def) => def != null && Away.Contains(def.defName);

        /// <summary>The bar asks whether a button shows (Patch_BarPutAway): not if it's put away.</summary>
        internal static void Hide(MainButtonWorker worker, ref bool result)
        {
            if (!result || asking || hideBroken) return;
            try
            {
                if (On && IsAway(worker?.def)) result = false;
            }
            catch (Exception e)
            {
                hideBroken = true;
                CouncilLog.ErrorOnce("bottom bar: buttons put away", e);
            }
        }

        private static bool hideBroken;

        private static void Changed()
        {
            cachedCount = -2;
            S.Write();
        }

        /// <summary>Whether the button would be on the bar now if it weren't put away (its own rules, its mod's switch).</summary>
        public static bool WouldShow(MainButtonDef def)
        {
            if (def == null) return false;
            if (Current.ProgramState != ProgramState.Playing) return def.buttonVisible;
            asking = true;
            try
            {
                return def.Worker.Visible;
            }
            catch
            {
                return def.buttonVisible;
            }
            finally
            {
                asking = false;
            }
        }

        public static void PutAway(MainButtonDef def)
        {
            if (def == null || S == null) return;
            S.barPutAway ??= new List<string>();
            if (BarMoreMath.PutAway(S.barPutAway, def.defName)) Changed();
        }

        public static void PinBack(MainButtonDef def)
        {
            if (def == null || S?.barPutAway == null) return;
            if (BarMoreMath.PinBack(S.barPutAway, def.defName)) Changed();
        }

        public static void PinAllBack()
        {
            if (S?.barPutAway == null || S.barPutAway.Count == 0) return;
            S.barPutAway.Clear();
            Changed();
        }

        /// <summary>Every button, in the bar's order (the game's: by order, then by load order).</summary>
        public static List<MainButtonDef> InBarOrder() => DefDatabase<MainButtonDef>.AllDefs.OrderBy(d => d.order).ToList();

        /// <summary>The buttons that can be arranged: those the bar shows (or would, put away), but never the menu or More.</summary>
        public static List<MainButtonDef> Arrangeable() =>
            InBarOrder().Where(d => BarMoreMath.CanPutAway(d.defName) && (IsAway(d) || WouldShow(d))).ToList();

        /// <summary>More's place: just before the game's menu (CouncilStartup.ApplyButtonPosition).</summary>
        public static void Place()
        {
            var more = MoreDef;
            if (more == null) return;
            var menu = DefDatabase<MainButtonDef>.GetNamedSilentFail("Menu");
            int last = DefDatabase<MainButtonDef>.AllDefs.Where(d => d != more).Select(d => d.order).DefaultIfEmpty(0).Max();
            more.order = BarMoreMath.OrderFor(menu?.order, last);
            if (S != null) S.barPutAway = BarMoreMath.Clean(S.barPutAway);
        }

        /// <summary>More's list: the buttons put away that would show now, each with a pin to put it back; then arranging.</summary>
        public static List<FloatMenuOption> MoreMenu()
        {
            var list = new List<FloatMenuOption>();
            var shown = InBarOrder().Where(d => IsAway(d) && WouldShow(d)).ToList();
            bool anyIcon = shown.Any(d => d.Icon != null);
            foreach (var def in shown)
            {
                var d = def;
                bool disabled;
                try
                {
                    disabled = d.Worker.Disabled;
                }
                catch
                {
                    disabled = false;
                }
                Action open = disabled ? null : () => d.Worker.InterfaceTryActivate();
                Func<Rect, bool> pin = r => PinButton(r, d);
                var opt = anyIcon
                    ? new FloatMenuOption(d.LabelCap, open, d.Icon ?? BaseContent.ClearTex, Color.white, extraPartWidth: 30f, extraPartOnGUI: pin)
                    : new FloatMenuOption(d.LabelCap, open, extraPartWidth: 30f, extraPartOnGUI: pin);
                opt.extraPartRightJustified = true;
                if (!d.description.NullOrEmpty()) opt.tooltip = new TipSignal(d.description);
                list.Add(opt);
            }
            if (shown.Count == 0) list.Add(new FloatMenuOption("CC_Bar_NoneAway".Translate(), null));
            list.Add(new FloatMenuOption("CC_Bar_Arrange".Translate(), () => Find.WindowStack.Add(new Dialog_ArrangeBar())));
            return list;
        }

        /// <summary>Right-click on a button of the bar: put it away under More, or arrange the bar.</summary>
        public static List<FloatMenuOption> ButtonMenu(MainButtonDef def) => new List<FloatMenuOption>
        {
            new FloatMenuOption("CC_Bar_PutAway".Translate(def.LabelCap.Named("BUTTON")), () => PutAway(def)),
            new FloatMenuOption("CC_Bar_Arrange".Translate(), () => Find.WindowStack.Add(new Dialog_ArrangeBar()))
        };

        private static bool PinButton(Rect r, MainButtonDef def)
        {
            var tex = Icons.Pin;
            var box = new Rect(r.x + (r.width - 22f) / 2f, r.y + (r.height - 22f) / 2f, 22f, 22f);
            TooltipHandler.TipRegion(box, "CC_Bar_PinTip".Translate());
            if (tex == null ? Widgets.ButtonText(box, "^") : Widgets.ButtonImage(box, tex, Color.white, GenUI.MouseoverColor))
            {
                PinBack(def);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                return true;
            }
            return false;
        }
    }

    /// <summary>More on the bottom bar: a click (either button) lists the buttons put away (BarMore.MoreMenu).</summary>
    public class MainButtonWorker_More : MainButtonWorker
    {
        public override bool Visible => BarMore.On;

        public override void Activate() => Find.WindowStack.Add(new FloatMenu(BarMore.MoreMenu()));
    }

    /// <summary>
    /// A button put away under More doesn't show on the bar: the game asks each button's worker whether it shows, and
    /// the answer is no for those put away. This is the game's own answer, which most buttons use;
    /// Patch_BarPutAwayOwn covers the workers that give their own.
    /// </summary>
    [HarmonyPatch(typeof(MainButtonWorker), nameof(MainButtonWorker.Visible), MethodType.Getter)]
    public static class Patch_BarPutAway
    {
        public static void Postfix(MainButtonWorker __instance, ref bool __result) => BarMore.Hide(__instance, ref __result);
    }

    /// <summary>
    /// The same for every worker class that gives its own answer (the mechs tab's, and other mods'), found among the
    /// loaded assemblies when the hooks start. On its own, so a mod's worker that can't be patched leaves the rest working.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_BarPutAwayOwn
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var seen = new HashSet<MethodBase>();
            foreach (var t in GenTypes.AllSubclasses(typeof(MainButtonWorker)))
            {
                if (t == typeof(MainButtonWorker_More) || t.IsGenericTypeDefinition) continue;
                MethodInfo getter = null;
                try
                {
                    getter = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(p => p.Name == nameof(MainButtonWorker.Visible) && p.PropertyType == typeof(bool))?.GetGetMethod(true);
                }
                catch
                {
                    // A type that can't be read (a mod's missing reference): its buttons go by the game's answer.
                }
                if (getter != null && !getter.IsAbstract && getter.DeclaringType == t && seen.Add(getter)) yield return getter;
            }
        }

        public static void Postfix(MainButtonWorker __instance, ref bool __result) => BarMore.Hide(__instance, ref __result);
    }

    /// <summary>A right-click on a button of the bottom bar offers to put it away under More (BarMore.ButtonMenu).</summary>
    [HarmonyPatch(typeof(MainButtonWorker), nameof(MainButtonWorker.DoButton))]
    public static class Patch_BarRightClick
    {
        private static bool broken;

        public static void Prefix(MainButtonWorker __instance, Rect rect)
        {
            if (broken) return;
            try
            {
                var e = Event.current;
                if (e == null || e.type != EventType.MouseDown || e.button != 1 || !BarMore.On) return;
                var def = __instance?.def;
                if (def == null || !BarMoreMath.CanPutAway(def.defName) || !Mouse.IsOver(rect)) return;
                e.Use();
                Find.WindowStack.Add(new FloatMenu(BarMore.ButtonMenu(def)));
            }
            catch (Exception ex)
            {
                broken = true;
                CouncilLog.ErrorOnce("bottom bar: right-click", ex);
            }
        }
    }

    /// <summary>Every button of the bottom bar, ticked to stay on the bar or not ticked to be put away under More.</summary>
    public class Dialog_ArrangeBar : Window
    {
        private Vector2 scroll;
        private float height = 400f;

        public Dialog_ArrangeBar()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(520f, UI.screenWidth - 40f), Mathf.Min(680f, UI.screenHeight - 40f));

        public override void DoWindowContents(Rect inRect)
        {
            var buttons = BarMore.Arrangeable();
            int putAway = buttons.Count(BarMore.IsAway);
            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.More, "CC_Bar_ArrangeTitle".Translate(),
                "CC_Bar_ArrangeSub".Translate(), putAway > 0 ? "CC_Bar_ArrangeStat".Translate(putAway).Resolve() : null, UiKit.Faint);
            float y = inRect.y + top;
            if (!BarMore.On)
            {
                y += UiKit.Paragraph(inRect.x, y, inRect.width, (RimfolkPatches.On ? "CC_Bar_Off" : "CC_Bar_NeedsHooks").Translate(), UiKit.Warn) + 6f;
            }
            const float bottom = 44f, rowH = 32f;
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - bottom - 40f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float ry = 0f;
            for (int i = 0; i < buttons.Count; i++)
            {
                var def = buttons[i];
                var row = new Rect(0f, ry, view.width, rowH);
                UiKit.Zebra(row, i);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                float x = 6f;
                if (def.Icon != null)
                {
                    GUI.DrawTexture(new Rect(x, ry + 4f, 24f, 24f), def.Icon);
                }
                x += 32f;
                bool onBar = !BarMore.IsAway(def);
                bool was = onBar;
                UiKit.LabelFit(new Rect(x, ry, view.width - x - 150f, rowH), def.LabelCap, onBar ? Color.white : UiKit.Faint);
                var tick = new Rect(view.width - 144f, ry, 138f, rowH);
                Widgets.CheckboxLabeled(tick, "CC_Bar_OnTheBar".Translate(), ref onBar);
                if (!def.description.NullOrEmpty()) TooltipHandler.TipRegion(row, def.description);
                if (onBar != was)
                {
                    if (onBar) BarMore.PinBack(def);
                    else BarMore.PutAway(def);
                }
                ry += rowH;
            }
            height = Mathf.Max(ry, 40f);
            Widgets.EndScrollView();
            var all = new Rect(inRect.x, inRect.yMax - bottom - 36f, 260f, 32f);
            if (putAway > 0 && Widgets.ButtonText(all, UiKit.Fit("CC_Bar_PinAll".Translate(), all.width - 12f))) BarMore.PinAllBack();
        }
    }
}
