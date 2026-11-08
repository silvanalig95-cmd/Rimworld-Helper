using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The region window as a floating window (docs/design/rimfolk-look.md, stage 1), opened by the region's button while
    /// "Rimfolk's windows move and resize" is on: the same four views and drawer as the tab (MainTabWindow_World draws
    /// them for both), in Rimfolk's frame, dragged by its title bar, sized from its edges, large and back, where it was
    /// left. It is a window of the game's interface layer, not a main tab: a click on the map, a colonist picked, the
    /// inspect pane or another of the game's tabs leave it open, so the player can play with it beside the map. It is
    /// still one of Rimfolk's four windows: the corner and the other three buttons turn the page to another (it closes),
    /// its button and its keys open and close it, Escape closes the drawer first, and every address (Pages.Open) and other
    /// mods' region pages land in it as in the tab.
    /// </summary>
    public class Window_Region : FramedWindow
    {
        /// <summary>The least size: room for the tabs and the corner, a view beside the map's panel laid over it.</summary>
        public const float LeastW = WindowFrameMath.RegionLeastW, LeastH = WindowFrameMath.RegionLeastH;

        private static Window_Region instance;

        public Window_Region() : base("Region", LeastW, LeastH, WindowFrameMath.BottomBar)
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

        /// <summary>The region window floats (its button opens this window rather than the tab).</summary>
        public static bool Floats => WindowFrame.MovesOn;

        /// <summary>The floating region window is open now.</summary>
        public static bool Showing => instance != null && Find.WindowStack != null && Find.WindowStack.IsOpen(instance);

        /// <summary>The floating window's place on the screen, when open (the on-screen panels step aside from a big one).</summary>
        public static Rect? Shown => Showing ? instance.windowRect : (Rect?)null;

        /// <summary>Opens the floating region window (on the map, unless an address asked for a view).</summary>
        public static void Open()
        {
            if (Showing || Find.WindowStack == null) return;
            instance ??= new Window_Region();
            Find.WindowStack.Add(instance);
        }

        /// <summary>Closes it, if open.</summary>
        public static void Shut(bool sound = true)
        {
            if (Showing) instance.Close(sound);
        }

        public override Vector2 InitialSize => WindowTabs.Size;

        protected override string FrameTitle => title ??= Pages.WindowName(RimfolkWindow.World);
        private static string title;

        protected override Texture2D FrameIcon => Icons.World;

        // The corner's tools in the title bar: back and forward, the search, the inbox, following, the Almanac, the four windows.
        protected override float FrameToolsWidth => WindowTabs.LinksWidth;

        protected override void DrawFrameTools(Rect r) => WindowTabs.Links(r, RimfolkWindow.World);

        public override void PreOpen()
        {
            base.PreOpen();
            MainTabWindow_World.Opening();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public override void PostClose()
        {
            base.PostClose();
            MainTabWindow_World.Closing();
            Previews.Hide();
        }

        /// <summary>Escape closes a card's drawer first, the window second.</summary>
        public override void OnCancelKeyPressed()
        {
            if (Drawer.Cancel(RimfolkWindow.World))
            {
                Event.current.Use();
                return;
            }
            base.OnCancelKeyPressed();
        }

        protected override void DoFramedContents(Rect inRect) => MainTabWindow_World.DrawRegion(inRect, true);
    }

    /// <summary>
    /// The region button on the bottom bar: the floating region window while Rimfolk's windows move, else the tab as
    /// before; clicked again while it shows, it closes it.
    /// </summary>
    public class MainButtonWorker_Region : MainButtonWorker_ToggleTab
    {
        public override void Activate()
        {
            if (Window_Region.Showing)
            {
                Window_Region.Shut();
                return;
            }
            if (Window_Region.Floats)
            {
                Pages.Show(RimfolkWindow.World);
                return;
            }
            base.Activate();
        }
    }
}
