using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The floating windows of Rimfolk's four (docs/design/rimfolk-look.md, stage 2): the council, the region, defense and
    /// planning, each a plain window in Rimfolk's frame while "Rimfolk's windows move and resize" is on, made once and kept,
    /// by <see cref="RimfolkWindow"/>. Pages asks here whether one shows, opens and closes it, and off to the map closes them
    /// all. One of the four at a time, as the corner turns pages: opening one closes the other floating three
    /// (<see cref="FramedWindowsMath.ToClose"/>); a window opening for the first time takes the place of the one it turns the
    /// page from.
    /// </summary>
    public static class FloatingWindows
    {
        private static readonly RimfolkFloatingWindow[] made = new RimfolkFloatingWindow[4];

        /// <summary>Rimfolk's four windows float (their buttons open the floating windows rather than the tabs).</summary>
        public static bool Floats => WindowFrame.MovesOn;

        private static RimfolkFloatingWindow Made(RimfolkWindow w)
        {
            int i = (int)w;
            return made[i] ??= Make(w);
        }

        private static RimfolkFloatingWindow Make(RimfolkWindow w)
        {
            switch (w)
            {
                case RimfolkWindow.World: return new Window_Region();
                case RimfolkWindow.Planning: return new Window_Planning();
                case RimfolkWindow.Defense: return new Window_Defenses();
                default: return new Window_Council();
            }
        }

        /// <summary>The floating window is open now.</summary>
        public static bool Showing(RimfolkWindow w)
        {
            var f = made[(int)w];
            return f != null && Find.WindowStack != null && Find.WindowStack.IsOpen(f);
        }

        /// <summary>Any of the four floats open now.</summary>
        public static bool AnyShowing
        {
            get
            {
                foreach (var w in FramedWindowsMath.Floating)
                    if (Showing(w))
                        return true;
                return false;
            }
        }

        private static readonly List<RimfolkWindow> openNow = new List<RimfolkWindow>();

        /// <summary>The floating windows open now, in the corner's order.</summary>
        private static List<RimfolkWindow> OpenNow()
        {
            openNow.Clear();
            foreach (var w in FramedWindowsMath.Floating)
                if (Showing(w))
                    openNow.Add(w);
            return openNow;
        }

        /// <summary>The open floating window's place on the screen (the on-screen panels step aside from a big one), or none.</summary>
        public static Rect? Shown
        {
            get
            {
                foreach (var w in FramedWindowsMath.Floating)
                    if (Showing(w))
                        return made[(int)w].windowRect;
                return null;
            }
        }

        /// <summary>
        /// Opens a floating window (or brings it to the front, open), closing the other floating three: one of the four at a
        /// time. Opened for the first time, it takes the place of the one it turns the page from.
        /// </summary>
        public static void Open(RimfolkWindow w)
        {
            var stack = Find.WindowStack;
            if (stack == null) return;
            var win = Made(w);
            if (Showing(w))
            {
                stack.Notify_ClickedInsideWindow(win);
                return;
            }
            Rect? from = null;
            foreach (var other in FramedWindowsMath.ToClose(w, OpenNow()))
            {
                from ??= made[(int)other].windowRect;
                Shut(other, false);
            }
            if (from.HasValue && !win.FrameKnown) win.handedPlace = from;
            stack.Add(win);
        }

        /// <summary>Closes a floating window, if open.</summary>
        public static void Shut(RimfolkWindow w, bool sound = true)
        {
            if (Showing(w)) made[(int)w].Close(sound);
        }

        /// <summary>Closes every floating window (a tab of Rimfolk's opening, off to the map).</summary>
        public static void ShutAll(bool sound = false)
        {
            foreach (var w in FramedWindowsMath.Floating) Shut(w, sound);
        }
    }

    /// <summary>
    /// One of Rimfolk's four windows floating (UI/FloatingWindows.cs): a plain window of the game's interface layer in
    /// Rimfolk's frame, not closed by a click on the map or a colonist picked, letting the camera move, so the player can
    /// play with it beside the map. Its title bar has the window's icon and name and the corner (back and forward, the
    /// search, the inbox, following, the Almanac, the four windows); Escape closes a card's drawer first, the window second.
    /// Each draws the same contents as its tab (the tab is what opens with "Rimfolk's windows move and resize" off).
    /// </summary>
    public abstract class RimfolkFloatingWindow : FramedWindow
    {
        public abstract RimfolkWindow Which { get; }

        protected RimfolkFloatingWindow(RimfolkWindow w) : base(FramedWindowsMath.KeyOf(w))
        {
            layer = WindowLayer.GameUI;
            doCloseX = false;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            closeOnAccept = false;
            soundAppear = null;
            soundClose = SoundDefOf.TabClose;
        }

        /// <summary>The window has a place of its own already (it was left somewhere before).</summary>
        internal bool FrameKnown => frame.Known;

        /// <summary>The size every one of the four first asks for (the setting "Window size"), as the tabs do.</summary>
        public override Vector2 InitialSize => WindowTabs.Size;

        private string title;

        protected override string FrameTitle => title ??= Pages.WindowName(Which);

        protected override Texture2D FrameIcon => WindowTabs.IconOf(Which);

        // The corner's tools in the title bar.
        protected override float FrameToolsWidth => WindowTabs.LinksWidth;

        protected override void DrawFrameTools(Rect r) => WindowTabs.Links(r, Which);

        public override void PostClose()
        {
            base.PostClose();
            Previews.Hide();
        }

        /// <summary>Escape closes a card's drawer first, the window second.</summary>
        public override void OnCancelKeyPressed()
        {
            if (Drawer.Cancel(Which))
            {
                Event.current.Use();
                return;
            }
            base.OnCancelKeyPressed();
        }
    }

    /// <summary>The council's window floating: This week, People, Council and Colony, as the council's tab draws them.</summary>
    public class Window_Council : RimfolkFloatingWindow
    {
        public Window_Council() : base(RimfolkWindow.Council)
        {
        }

        public override RimfolkWindow Which => RimfolkWindow.Council;

        public override void PreOpen()
        {
            base.PreOpen();
            MainTabWindow_Council.Opening();
        }

        public override void PostClose()
        {
            base.PostClose();
            MainTabWindow_Council.Closing();
        }

        protected override void DoFramedContents(Rect inRect) => MainTabWindow_Council.DrawCouncil(inRect, true);
    }

    /// <summary>The planning window floating: Plan, Base, Making and Rooms, as the planning tab draws them.</summary>
    public class Window_Planning : RimfolkFloatingWindow
    {
        public Window_Planning() : base(RimfolkWindow.Planning)
        {
        }

        public override RimfolkWindow Which => RimfolkWindow.Planning;

        public override void PreOpen()
        {
            base.PreOpen();
            MainTabWindow_Planning.Opening();
        }

        protected override void DoFramedContents(Rect inRect) => MainTabWindow_Planning.DrawPlanning(inRect, true);
    }

    /// <summary>The defense window floating: the Defenses page, as the defense tab draws it.</summary>
    public class Window_Defenses : RimfolkFloatingWindow
    {
        public Window_Defenses() : base(RimfolkWindow.Defense)
        {
        }

        public override RimfolkWindow Which => RimfolkWindow.Defense;

        protected override void DoFramedContents(Rect inRect) => MainTabWindow_Defenses.DrawDefenses(inRect, true);
    }

    /// <summary>
    /// The bottom bar button of one of Rimfolk's four windows: the floating window while Rimfolk's windows move, else the
    /// tab as before; clicked again while it shows, it closes it. Its key (the button's hotkey) does the same.
    /// </summary>
    public class MainButtonWorker_RimfolkWindow : MainButtonWorker_ToggleTab
    {
        public override void Activate()
        {
            var w = Pages.WindowOfButton(def);
            if (w.HasValue)
            {
                if (FloatingWindows.Showing(w.Value))
                {
                    FloatingWindows.Shut(w.Value);
                    return;
                }
                if (FloatingWindows.Floats)
                {
                    // As the game's tab button sounds; opened from the corner or by an address it opens quietly, as the tab did.
                    Pages.Show(w.Value);
                    SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    return;
                }
            }
            base.Activate();
        }
    }
}
