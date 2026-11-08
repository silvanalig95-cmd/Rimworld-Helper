using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The region window as a floating window (docs/design/rimfolk-look.md, stage 1), opened by the region's button while
    /// "Rimfolk's windows move and resize" is on: the same four views and drawer as the tab (MainTabWindow_World draws
    /// them for both), in Rimfolk's frame, dragged by its title bar, sized from its edges, large and back, where it was
    /// left. It is a window of the game's interface layer, not a main tab: a click on the map, a colonist picked, the
    /// inspect pane or another of the game's tabs leave it open, so the player can play with it beside the map. It is
    /// still one of Rimfolk's four windows (UI/FloatingWindows.cs keeps it with the other three since stage 2): the corner
    /// and the other three buttons turn the page to another (it closes), its button and its keys open and close it, Escape
    /// closes the drawer first, and every address (Pages.Open) and other mods' region pages land in it as in the tab.
    /// </summary>
    public class Window_Region : RimfolkFloatingWindow
    {
        /// <summary>The least size: room for the tabs and the corner, a view beside the map's panel laid over it.</summary>
        public const float LeastW = WindowFrameMath.RegionLeastW, LeastH = WindowFrameMath.RegionLeastH;

        public Window_Region() : base(RimfolkWindow.World)
        {
        }

        public override RimfolkWindow Which => RimfolkWindow.World;

        /// <summary>The region window floats (its button opens this window rather than the tab).</summary>
        public static bool Floats => FloatingWindows.Floats;

        /// <summary>The floating region window is open now.</summary>
        public static bool Showing => FloatingWindows.Showing(RimfolkWindow.World);

        /// <summary>Opens the floating region window (on the map, unless an address asked for a view).</summary>
        public static void Open() => FloatingWindows.Open(RimfolkWindow.World);

        /// <summary>Closes it, if open.</summary>
        public static void Shut(bool sound = true) => FloatingWindows.Shut(RimfolkWindow.World, sound);

        public override void PreOpen()
        {
            base.PreOpen();
            MainTabWindow_World.Opening();
        }

        public override void PostClose()
        {
            base.PostClose();
            MainTabWindow_World.Closing();
        }

        protected override void DoFramedContents(Rect inRect) => MainTabWindow_World.DrawRegion(inRect, true);
    }

    /// <summary>
    /// The region button on the bottom bar: the floating region window while Rimfolk's windows move, else the tab as
    /// before; clicked again while it shows, it closes it (the worker of all four buttons, by its old name).
    /// </summary>
    public class MainButtonWorker_Region : MainButtonWorker_RimfolkWindow
    {
    }
}
