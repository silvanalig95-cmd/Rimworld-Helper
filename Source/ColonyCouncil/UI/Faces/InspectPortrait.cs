using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The large portrait at the inspect pane (the player's go, 20 November 2026: "the larger image of the pawn can be in
    /// the bottom left corner, attached to the pawn default screen when you select it ... perhaps you can rescale this
    /// window with drag and drop also"). With one person selected on the map (a colonist, a slave, a prisoner, a visitor,
    /// a raider: anyone humanlike), their portrait from the face engine (the bust) stands on the inspect pane's tab
    /// buttons at its left, with their name and the name the colony knows them by under the face and the mark of their
    /// people in its corner.
    /// <list type="bullet">
    /// <item>The pane is read as it is on the screen (its window and the top the game gives its tabs), so a mod that
    /// makes it taller or wider (RimHUD) moves the portrait with it.</item>
    /// <item>An open tab (Bio, Gear, Health...) is read from the game's own tab (its rect as the game draws it): the
    /// portrait stands right of it, its foot level with the tab's and above the gizmos; with no room there, above the
    /// tab; with no room at all, it waits until the tab is closed (<see cref="FaceMath.Place"/>).</item>
    /// <item>Dragging its free corner (the top right) sizes it, 96 to 360, kept in the settings between games; while
    /// dragging, the picture made at the start is shown larger or smaller, and it is drawn sharp again at the new size
    /// when the mouse is let go, so no picture is made at every step.</item>
    /// <item>A window of its own the size of the portrait (the game's way for the inspect tabs), so it takes clicks only
    /// on itself: a click opens their card.</item>
    /// </list>
    /// A deeper game hook (a postfix on the inspect pane's drawing, every frame it is open); the setting "A large portrait
    /// at the inspect pane" switches it off.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class InspectPortrait
    {
        private const int WindowId = 0x52465054;

        /// <summary>The setting, with the deeper game hooks.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.inspectPortrait ?? true);

        // What the window draws this frame; one draw call made once, so nothing is made per frame.
        private static Pawn pawn;
        private static Rect rect;
        private static float render;
        private static int shownFrame = -100;
        private static readonly Action DrawAction = DrawWindow;

        private static bool dragging;
        private static float dragStart, dragSize, dragX, dragY;

        private static Func<InspectTabBase, Rect> tabRect;
        private static bool tabRectBroken;

        private static Pawn named;
        private static float namedUntil;
        private static string name, known, tip, gripTip;

        /// <summary>Where it was drawn, while it shows (this frame or the last): the hover card's corner keeps above it.</summary>
        public static bool Showing(out Rect r)
        {
            r = rect;
            return Time.frameCount - shownFrame <= 1;
        }

        /// <summary>Every event the inspect pane is open: the drag, then the portrait's place and its window.</summary>
        public static void OnGUI(MainTabWindow_Inspect pane)
        {
            if (dragging && !Input.GetMouseButton(0)) EndDrag();
            if (!On || pane == null) return;
            var p = Subject();
            if (p == null) return;
            if (dragging)
            {
                var m = UI.MousePositionOnUIInverted;
                dragSize = FaceMath.Dragged(dragStart, m.x - dragX, m.y - dragY);
            }
            float asked = dragging ? dragSize : FaceMath.Snap(ColonyCouncilMod.Settings?.inspectPortraitSize ?? FaceMath.SizeFirst);
            if (FaceMath.Place(Scene(pane), asked, out var fr) == FaceMath.Placed.None) return;
            pawn = p;
            rect = new Rect(fr.x, fr.y, fr.w, fr.h);
            if (!dragging) render = rect.width;
            shownFrame = Time.frameCount;
            Find.WindowStack.ImmediateWindow(WindowId, rect, WindowLayer.GameUI, DrawAction, false, false, 0f);
        }

        /// <summary>The one humanlike person selected on the map, or null.</summary>
        private static Pawn Subject()
        {
            var sel = Find.Selector;
            if (sel == null || sel.NumSelected != 1) return null;
            if (!(sel.SingleSelectedThing is Pawn p) || p.Destroyed || !p.Spawned) return null;
            return p.RaceProps != null && p.RaceProps.Humanlike ? p : null;
        }

        /// <summary>The inspect pane, its tab buttons, an open tab and the gizmos, as they are on the screen now.</summary>
        private static FaceMath.PaneScene Scene(MainTabWindow_Inspect pane)
        {
            var w = pane.windowRect;
            var s = new FaceMath.PaneScene
            {
                screenW = UI.screenWidth,
                screenH = UI.screenHeight,
                pane = new FrameRect(w.x, w.y, w.width, w.height),
                paneTop = w.y,
                gizmoTop = UI.screenHeight
            };
            try
            {
                s.paneTop = Mathf.Min(w.y, pane.PaneTopY);
                float g = GizmoGridDrawer.HeightDrawnRecently;
                if (g > 0f) s.gizmoTop = UI.screenHeight - WindowFrameMath.BottomBar - g;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the portrait at the inspect pane: the pane's top and the gizmos", e);
            }
            var type = pane.OpenTabType;
            InspectTabBase open = null;
            bool any = false;
            foreach (var t in pane.CurTabs ?? Enumerable.Empty<InspectTabBase>())
            {
                if (t == null || !t.IsVisible || t.Hidden) continue;
                any = true;
                if (type != null && t.GetType() == type) open = t;
            }
            s.tabs = any;
            if (open != null)
            {
                s.tabOpen = true;
                // A tab whose place can't be read is taken as filling the screen above the pane: the portrait waits.
                s.tab = TabRect(open, out var tr)
                    ? new FrameRect(tr.x, tr.y, tr.width, tr.height)
                    : new FrameRect(0f, 0f, UI.screenWidth, Mathf.Max(0f, s.paneTop - FaceMath.TabRowH));
            }
            return s;
        }

        /// <summary>The open tab's rect as the game draws it (InspectTabBase.TabRect, which other mods may move).</summary>
        private static bool TabRect(InspectTabBase tab, out Rect r)
        {
            r = default;
            if (tabRectBroken) return false;
            try
            {
                tabRect ??= AccessTools.MethodDelegate<Func<InspectTabBase, Rect>>(AccessTools.PropertyGetter(typeof(InspectTabBase), "TabRect"));
                r = tabRect(tab);
                return true;
            }
            catch (Exception e)
            {
                tabRectBroken = true;
                CouncilLog.ErrorOnce("the portrait at the inspect pane: an open tab's place (it waits while a tab is open)", e);
                return false;
            }
        }

        // ---------------- The window ----------------

        private static void DrawWindow()
        {
            var p = pawn;
            if (p == null) return;
            bool ownOn = WindowFrame.OwnLookOn;
            bool was = ownOn && UiKit.BeginOwnColours();
            try
            {
                Body(p, new Rect(0f, 0f, rect.width, rect.height));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the portrait at the inspect pane", e);
            }
            finally
            {
                if (ownOn) UiKit.EndOwnColours(was);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private static void Body(Pawn p, Rect r)
        {
            var ev = Event.current;
            var grip = new Rect(r.xMax - FaceMath.Handle, 0f, FaceMath.Handle, FaceMath.Handle);
            if (ev.type == EventType.MouseDown && ev.button == 0 && Mouse.IsOver(grip))
            {
                StartDrag();
                ev.Use();
            }
            Names(p);
            var inner = r.ContractedBy(FaceMath.Border);
            if (ev.type == EventType.Repaint)
            {
                if (UiKit.OwnLook) UiKit.RoundBox(r, UiKit.PortraitBack, UiKit.Edge, SkinMath.RadiusCard);
                else Widgets.DrawBoxSolidWithOutline(r, UiKit.PortraitBack, UiKit.CardBorder, 1);
                float rs = Mathf.Max(1f, render - 2f * FaceMath.Border);
                FaceEngine.Draw(inner, p, FaceFrame.Bust, new Vector2(rs, rs));
                if (r.width >= FaceMath.NamedFrom) NameBand(inner);
                if (dragging || Mouse.IsOver(r)) Grip(grip);
            }
            float ms = Mathf.Clamp(Mathf.Round(r.width * 0.14f), 18f, 32f);
            var mark = new Rect(inner.x + 4f, inner.y + 4f, ms, ms);
            FaceEngine.DrawMark(mark, p);
            if (!dragging)
            {
                // One tooltip at a time: the mark's people, the corner's sizing, else the portrait's own.
                var people = FaceEngine.PeopleOf(p);
                if (Mouse.IsOver(mark) && FaceEngine.MarkOf(people) != null && !people.Name.NullOrEmpty()) TooltipHandler.TipRegion(mark, people.Name);
                else if (Mouse.IsOver(grip)) TooltipHandler.TipRegion(grip, gripTip);
                else TooltipHandler.TipRegion(r, tip);
                // A left click opens their card; any other click on it is taken here, so it never reaches the map below.
                if (Widgets.ButtonInvisible(r, false) && ev.button == 0)
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Cards.Open(p);
                }
            }
            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseUp) && Mouse.IsOver(r)) ev.Use();
        }

        /// <summary>Their name and the name the colony knows them by, on a dark band at the foot of the picture.</summary>
        private static void NameBand(Rect inner)
        {
            bool big = inner.width >= 250f;
            float nameH = big ? 30f : 22f, knownH = known != null ? 18f : 0f;
            float h = nameH + knownH + 6f;
            var band = new Rect(inner.x, inner.yMax - h, inner.width, h);
            Widgets.DrawBoxSolid(band, UiKit.WithAlpha(UiKit.WindowGround, FaceMath.BandAlpha));
            UiKit.LabelFit(new Rect(band.x + 6f, band.y + 3f, band.width - 12f, nameH), name, UiKit.Ink, big ? GameFont.Medium : GameFont.Small, TextAnchor.MiddleCenter);
            if (known != null)
                UiKit.LabelFit(new Rect(band.x + 6f, band.y + 3f + nameH, band.width - 12f, knownH), known, UiKit.Heading, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        /// <summary>The game's grip for a window's bottom right corner, turned to the top right where this one sizes.</summary>
        private static void Grip(Rect g)
        {
            var tex = TexUI.WinExpandWidget;
            if (tex == null) return;
            GUI.color = dragging || Mouse.IsOver(g) ? UiKit.Plain : UiKit.WithAlpha(UiKit.Plain, 0.55f);
            GUI.DrawTextureWithTexCoords(g.ContractedBy(3f), tex, new Rect(0f, 1f, 1f, -1f));
            GUI.color = Color.white;
        }

        /// <summary>The words for the person shown, made again when the person changes and every two seconds.</summary>
        private static void Names(Pawn p)
        {
            float now = Time.realtimeSinceStartup;
            if (p == named && now < namedUntil) return;
            named = p;
            namedUntil = now + 2f;
            name = p.LabelShortCap;
            known = null;
            try
            {
                string k = KnownAs.Short(p);
                if (!k.NullOrEmpty()) known = k;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the portrait at the inspect pane: the name the colony knows them by", e);
            }
            tip = "CC_Portrait_Tip".Translate(p.Named("PAWN")).Resolve();
            gripTip ??= "CC_Portrait_GripTip".Translate().Resolve();
        }

        // ---------------- Sizing by its corner ----------------

        private static void StartDrag()
        {
            dragging = true;
            dragStart = rect.width;
            dragSize = dragStart;
            var m = UI.MousePositionOnUIInverted;
            dragX = m.x;
            dragY = m.y;
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
        }

        /// <summary>The mouse let go: the size it was shown at is kept in the settings (the slider shows it), and it is drawn sharp at it.</summary>
        private static void EndDrag()
        {
            dragging = false;
            var s = ColonyCouncilMod.Settings;
            float now = Time.frameCount - shownFrame <= 1 ? rect.width : dragSize;
            if (s == null || Mathf.Abs(s.inspectPortraitSize - now) < 1f) return;
            s.inspectPortraitSize = FaceMath.Snap(now);
            s.Write();
        }
    }

    /// <summary>
    /// The large portrait at the inspect pane (UI/Faces/InspectPortrait.cs), after the pane's own drawing outside its window
    /// (its tabs and gizmos), every event while the pane is open. Applied on its own.
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.ExtraOnGUI))]
    public static class Patch_InspectPortrait
    {
        public static void Postfix(MainTabWindow_Inspect __instance)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                InspectPortrait.OnGUI(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the portrait at the inspect pane", e);
            }
        }
    }
}
