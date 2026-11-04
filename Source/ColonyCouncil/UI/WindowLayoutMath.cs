using System;

namespace ColonyCouncil
{
    /// <summary>How large Rimfolk's four windows are (the setting "Window size", docs/design/interface.md, decision 6).</summary>
    public enum WindowSizeChoice
    {
        /// <summary>1010 by 720, or the screen less its edges.</summary>
        Standard,
        /// <summary>Up to 1280 by 860 where the screen has the room.</summary>
        Large
    }

    /// <summary>
    /// How large Rimfolk's four windows are and how their parts lay out in them (docs/design/interface.md, "Screens and
    /// interface scale"): the window's size, standard (1010 by 720) or large (up to 1280 by 860, the setting "Window
    /// size"), always within the screen; the row at the top with the views as tabs and the corner beside them; the content
    /// area under it, where the drawer sits beside the view or over it (DrawerMath); and the region map's room in the Map
    /// view. Everything is in the game's interface units (the screen's pixels divided by the interface scale). Free of game
    /// types, so Tools/Tests can try every screen from 768 to 2160 pixels high at every scale up to 2.
    /// </summary>
    public static class WindowLayoutMath
    {
        /// <summary>The standard size, and the most the large size grows to.</summary>
        public const float StandardW = 1010f, StandardH = 720f, LargeW = 1280f, LargeH = 860f;

        /// <summary>Kept free beside the window on the screen, and above it with the bottom bar under it.</summary>
        public const float EdgeX = 20f, EdgeY = 20f, BottomBar = 35f;

        /// <summary>The game's margin inside a window (Window.Margin), and the padding inside the body under the tabs.</summary>
        public const float WindowMargin = 18f, BodyPad = 12f;

        /// <summary>The game's tabs (TabDrawer): their height, how much two overlap, and the widest one.</summary>
        public const float TabHeight = 32f, TabOverlap = 10f, TabMax = 200f;

        /// <summary>The least the row of tabs keeps beside the corner (WindowTabs).</summary>
        public const float TabsMin = 120f;

        /// <summary>
        /// A tab's icon (18), the room the game leaves around its label (24), and the space before the label that makes room
        /// for the icon (about 23 at the Small font): a tab shows its icon and its name when its label fits in what is left.
        /// </summary>
        public const float TabIcon = 18f, TabLabelPad = 24f, TabIconPad = 23f;

        /// <summary>One button in the corner and the gap after it, and the space around the two groups.</summary>
        public const float CornerIcon = 24f, CornerGap = 6f, CornerPad = 8f;

        /// <summary>
        /// The corner's tools before the windows' icons once the interface is whole: back, forward, search, the inbox,
        /// following and the Almanac's book.
        /// </summary>
        public const int CornerTools = 6;

        /// <summary>The four windows' icons at the corner's right.</summary>
        public const int CornerWindows = 4;

        /// <summary>The most views a window has (PagesMath.MaxViews).</summary>
        public const int MostViews = 5;

        /// <summary>
        /// The window's size on a screen <paramref name="uiWidth"/> by <paramref name="uiHeight"/> units: standard (1010 by
        /// 720) or, with <paramref name="large"/>, up to 1280 by 860; never more than the screen less its edges and the bottom
        /// bar, and the large size never smaller than the standard one.
        /// </summary>
        public static void Size(float uiWidth, float uiHeight, bool large, out float w, out float h)
        {
            float roomW = uiWidth - EdgeX, roomH = uiHeight - BottomBar - EdgeY;
            w = Math.Min(large ? LargeW : StandardW, roomW);
            h = Math.Min(large ? LargeH : StandardH, roomH);
        }

        /// <summary>Inside the window's margins: the width the tabs and the corner share.</summary>
        public static float InnerWidth(float windowW) => windowW - 2f * WindowMargin;

        /// <summary>The content area's width: inside the margins and the body's padding (where views and the drawer go).</summary>
        public static float ContentWidth(float windowW) => windowW - 2f * WindowMargin - 2f * BodyPad;

        /// <summary>The content area's height: inside the margins, under the tabs, inside the body's padding.</summary>
        public static float ContentHeight(float windowH) => windowH - 2f * WindowMargin - TabHeight - 2f * BodyPad;

        /// <summary>The corner's width with this many tools before the windows' icons (WindowTabs.LinksWidth's measure).</summary>
        public static float CornerWidth(int tools = CornerTools, int windows = CornerWindows) =>
            tools * (CornerIcon + CornerGap) + CornerPad + windows * (CornerIcon + CornerGap) + CornerPad;

        /// <summary>The row of tabs beside the corner, before WindowTabs keeps its least (so below 0 means they would meet).</summary>
        public static float TabsRoom(float windowW, float corner) => InnerWidth(windowW) - corner;

        /// <summary>One tab's width when <paramref name="count"/> tabs share <paramref name="tabsWidth"/> (as WindowTabs lays them).</summary>
        public static float TabWidth(float tabsWidth, int count)
        {
            if (count <= 0) return 0f;
            return Math.Min(TabMax, (Math.Max(TabsMin, tabsWidth) + (count - 1) * TabOverlap) / count);
        }

        /// <summary>A tab shows its icon and its name when a label this wide fits beside the icon (WindowTabs' first way).</summary>
        public static bool NameFits(float tabW, float labelW) => TabIconPad + labelW <= tabW - TabLabelPad;

        /// <summary>A tab has room for its icon at least (with the name on hover).</summary>
        public static bool IconFits(float tabW) => tabW >= TabIcon + 8f;

        // ---------------- The region map in the Map view ----------------

        /// <summary>Over the map: the lens bar with Map or List (UiKit.ModesHeight and its gap), then the map's own line (RegionMapView).</summary>
        public const float LensBar = 26f + 6f, MapLine = 26f + 4f;

        /// <summary>
        /// The region map's own rect in the Map view, with the drawer (the map's panel, always there on the map) beside the
        /// view or over it: as wide as the view beside the drawer, or the whole content under it, and as tall as the content
        /// less the lens bar and the map's line.
        /// </summary>
        public static void RegionMap(float contentW, float contentH, out float mapW, out float mapH, out bool drawerOver)
        {
            DrawerMath.Split(0f, contentW, out _, out float viewW, out _, out _, out drawerOver);
            mapW = viewW;
            mapH = contentH - LensBar - MapLine;
        }
    }
}
