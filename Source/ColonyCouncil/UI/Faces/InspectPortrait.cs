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
    /// <item>Shown only while no tab is open (the player, 21 November 2026: "it only needs to be shown when you click the
    /// pawn, without having one of these tabs open ... Once you click one of these tabs, the image of the pawn can,
    /// disappear in favor of the tab"): with Bio, Gear, Health or any other tab open it gives way to the tab
    /// (<see cref="FaceMath.Place"/>).</item>
    /// <item>Always front-on (the player, the same day: "just show them from the front angle when I select them"): no
    /// turn to a side in its movement and no shy half-turn (<see cref="FaceEngine.Draw"/>).</item>
    /// <item>Dragging its free corner (the top right) sizes it, 96 to 360 wide, kept in the settings between games; while
    /// dragging, the picture made at the start is shown larger or smaller, and it is drawn sharp again at the new size
    /// when the mouse is let go, so no picture is made at every step.</item>
    /// <item>With a painted picture it shows the whole picture at its own shape, a little taller than wide (phase 5; the
    /// player, 22 November 2026: "I'd rather, you know, see as much as I can"); with the game's own drawing it is square,
    /// in front of the backdrop of where they are where "Backdrop behind the game's own drawing too" is on.</item>
    /// <item>A window of its own the size of the portrait (the game's way for the inspect tabs), so it takes clicks only
    /// on itself: a click opens their card.</item>
    /// </list>
    /// An animal, an insect, a mechanoid or another creature shows here too where the library folder has a painted picture
    /// of its kind (<see cref="CreatureLibrary"/>; the player, 23 November 2026: "also please create lists for animals and
    /// creatures and implement image display for them too"), the same way in front of the backdrop of where it is; a
    /// click opens the game's information on it, a right click chooses another picture or deletes this one for good.
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
        /// <summary>Its inside's height for each unit of its width: the painted picture's own shape, else 1 (square).</summary>
        private static float tall = 1f;
        private static int shownFrame = -100;
        private static readonly Action DrawAction = DrawWindow;

        private static bool dragging;
        private static float dragStart, dragSize, dragX, dragY;

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
            if (p != pawn || Event.current.type == EventType.Layout) tall = FaceEngine.StageTall(p);
            if (FaceMath.Place(Scene(pane), asked, out var fr, tall) == FaceMath.Placed.None) return;
            pawn = p;
            rect = new Rect(fr.x, fr.y, fr.w, fr.h);
            if (!dragging) render = rect.width;
            shownFrame = Time.frameCount;
            Find.WindowStack.ImmediateWindow(WindowId, rect, WindowLayer.GameUI, DrawAction, false, false, 0f);
        }

        /// <summary>The one humanlike person selected on the map, or the one animal or creature with a painted picture; else null.</summary>
        private static Pawn Subject()
        {
            var sel = Find.Selector;
            if (sel == null || sel.NumSelected != 1) return null;
            if (!(sel.SingleSelectedThing is Pawn p) || p.Destroyed || !p.Spawned || p.RaceProps == null) return null;
            if (p.RaceProps.Humanlike) return p;
            return PortraitSources.ForStage(p) != null ? p : null;
        }

        /// <summary>The inspect pane, its tab buttons, whether a tab is open, and the gizmos, as they are on the screen now.</summary>
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
            bool any = false;
            foreach (var t in pane.CurTabs ?? Enumerable.Empty<InspectTabBase>())
            {
                if (t == null || !t.IsVisible || t.Hidden) continue;
                any = true;
                // The tab the pane has open, when it is one it shows for this person: the portrait gives way to it.
                if (type != null && t.GetType() == type) s.tabOpen = true;
            }
            s.tabs = any;
            return s;
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
                var made = new Vector2(rs, rs * tall);
                // Phase 6: where the mouse is over it, for the depth that follows it (not while it is being sized).
                PortraitLifeMath.Pointer(ev.mousePosition.x, ev.mousePosition.y, inner.x, inner.y, inner.width, inner.height, out float mx, out float my);
                FaceEngine.Hover(!dragging && Mouse.IsOver(r), mx, my);
                // A painted picture from the library, whole, in front of the backdrop of where they are; else the game's
                // drawing in front of it (phase 5), or on the portrait's own colour as before.
                if (!FaceEngine.DrawStaged(inner, p, made, true) && !FaceEngine.DrawGameStaged(inner, p, made, true)) FaceEngine.Draw(inner, p, FaceFrame.Bust, made, 1f, true);
                // What they go through, laid over the picture (UI/Faces/PortraitEffects.cs).
                PortraitEffects.Draw(inner, p, true, false);
                if (r.width >= FaceMath.NamedFrom) NameBand(inner);
                if (dragging || Mouse.IsOver(r)) Grip(grip);
            }
            bool creature = CreatureLibrary.IsCreature(p);
            float ms = Mathf.Clamp(Mathf.Round(r.width * 0.14f), 18f, 32f);
            var mark = new Rect(inner.x + 4f, inner.y + 4f, ms, ms);
            // The mark of their people: a person's only.
            if (!creature) FaceEngine.DrawMark(mark, p);
            // A right click: its menu (another painted picture for them, keeping the picture for training, or deleting the picture or backdrop shown for good).
            if (!dragging && ev.type == EventType.MouseDown && ev.button == 1 && Mouse.IsOver(r))
            {
                if (creature) CreatureMenu(p);
                else Menu(p);
                ev.Use();
            }
            if (!dragging)
            {
                // One tooltip at a time: the mark's people, the corner's sizing, else the portrait's own.
                var people = creature ? null : FaceEngine.PeopleOf(p);
                if (people != null && Mouse.IsOver(mark) && FaceEngine.MarkOf(people) != null && !people.Name.NullOrEmpty()) TooltipHandler.TipRegion(mark, people.Name);
                else if (Mouse.IsOver(grip)) TooltipHandler.TipRegion(grip, gripTip);
                else TooltipHandler.TipRegion(r, tip);
                // A left click opens their card; any other click on it is taken here, so it never reaches the map below.
                if (Widgets.ButtonInvisible(r, false) && ev.button == 0)
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    // An animal's or creature's: the game's own information on it.
                    if (creature) Find.WindowStack.Add(new Dialog_InfoCard(p));
                    else Cards.Open(p);
                }
            }
            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseUp) && Mouse.IsOver(r)) ev.Use();
        }

        /// <summary>
        /// The portrait's menu: "Choose another picture" (the next painted picture that may stand for them: their own pinned
        /// pictures, then the nearest faces), kept for them from then on; greyed out with the reason where there is none.
        /// Then "About this picture..." (<see cref="Window_PortraitAbout"/>: its record, why it is theirs, its versions and
        /// corrections), "Keep for training" for the painted picture shown (<see cref="PortraitTraining"/>), and "Delete this picture
        /// for good..." and "Delete this backdrop for good..." for the painted picture and the backdrop shown, where they come
        /// from the library folder (<see cref="PortraitDelete"/>).
        /// </summary>
        private static void Menu(Pawn p)
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            var lib = PortraitLibrary.Source;
            string label = "CC_Portrait_ChooseAnother".Translate().Resolve();
            if (lib.Count == 0) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_ChooseEmpty".Translate().Resolve() + ")", null));
            else if (!(ColonyCouncilMod.Settings?.portraitLibrary ?? true)) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_ChooseOff".Translate().Resolve() + ")", null));
            else if (!lib.CanChoose(p)) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_ChooseNoneShort".Translate().Resolve() + ")", null));
            else
                options.Add(new FloatMenuOption(label, () =>
                {
                    string said = lib.ChooseAnother(p);
                    Messages.Message(said, MessageTypeDefOf.SilentInput, false);
                }));
            // About the painted picture shown: its record, why it is theirs, its versions, corrections.
            string about = "CC_PortraitAbout_Menu".Translate().Resolve();
            if (PortraitSources.For(p)?.file != null && lib.KeptFor(p, out _, out _)) options.Add(new FloatMenuOption(about, () => Window_PortraitAbout.Open(p)));
            else options.Add(new FloatMenuOption(about + " (" + "CC_Portrait_TrainNoPicture".Translate().Resolve() + ")", null));
            PortraitTraining.AddOption(p, options);
            PortraitDelete.AddOptions(p, options);
            options.Add(new FloatMenuOption("CC_Portrait_RimfolkSettings".Translate().Resolve(), () => SettingsWindow.Open("CC_SetSec_Portraits")));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// An animal's or creature's picture's menu: "Choose another picture" (the next of its kind, age and sex, kept for it
        /// from then on), "Delete this picture for good..." and the backdrop's, and Rimfolk's settings.
        /// </summary>
        private static void CreatureMenu(Pawn p)
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            var lib = CreatureLibrary.Source;
            string label = "CC_Portrait_ChooseAnother".Translate().Resolve();
            if (!lib.CanChoose(p)) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_CreatureOnlyOne".Translate().Resolve() + ")", null));
            else
                options.Add(new FloatMenuOption(label, () =>
                {
                    string said = lib.ChooseAnother(p);
                    Messages.Message(said, MessageTypeDefOf.SilentInput, false);
                }));
            PortraitDelete.AddOptions(p, options);
            options.Add(new FloatMenuOption("CC_Portrait_RimfolkSettings".Translate().Resolve(), () => SettingsWindow.Open("CC_SetSec_Portraits")));
            Find.WindowStack.Add(new FloatMenu(options));
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
            if (CreatureLibrary.IsCreature(p))
            {
                tip = "CC_Portrait_CreatureTip".Translate(name.Named("NAME")).Resolve();
                gripTip ??= "CC_Portrait_GripTip".Translate().Resolve();
                return;
            }
            try
            {
                string k = KnownAs.Short(p);
                if (!k.NullOrEmpty()) known = k;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the portrait at the inspect pane: the name the colony knows them by", e);
            }
            tip = (PortraitLibrary.Source.Count > 0 ? "CC_Portrait_TipLibrary" : "CC_Portrait_Tip").Translate(p.Named("PAWN")).Resolve();
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
