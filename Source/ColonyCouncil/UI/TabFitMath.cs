using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How tall one of Rimfolk's inspect tabs may be (RimfolkTab), and how wide the game's Social tab gets with Rimfolk's
    /// column beside it (Patches/Patch_SocialTab.cs), free of game types so the tests can try screen sizes, interface
    /// scales and inspect panes (Tools/Tests). Everything is in the game's interface units:
    /// UI.screenHeight is the screen's height in pixels divided by Prefs.UIScale, so a 1080-pixel screen at 1.5 scale
    /// is 720 units tall.
    /// </summary>
    /// <remarks>
    /// The game opens a tab above the inspect pane: its top edge is at PaneTopY - 30 - height, the 30 being the row of
    /// tab buttons. PaneTopY is the screen height less the pane (165, or RimHUD's own, 232 by default and up to 800)
    /// and the bottom bar (35). Some numbers, for a tab that would like 540:
    /// <list type="bullet">
    /// <item>1080 pixels, scale 1, plain pane: room for 842, so 540.</item>
    /// <item>1080 pixels, scale 1, RimHUD at 400: room for 607, so 540.</item>
    /// <item>1080 pixels, scale 1.5, RimHUD default: room for 415.</item>
    /// <item>768 pixels, scale 1, plain pane: room for 530.</item>
    /// <item>768 pixels, scale 1, RimHUD default: room for 463.</item>
    /// <item>768 pixels, scale 1, RimHUD at 400: room for 295.</item>
    /// </list>
    /// Before, the Story tab stayed 520 tall whatever the room and the Mood tab allowed itself 20 units too many (it
    /// forgot the tab buttons), so above a tall pane both went off the top of the screen.
    /// </remarks>
    public static class TabFitMath
    {
        /// <summary>The row of tab buttons between the inspect pane and an open tab.</summary>
        public const float TabButtons = 30f;

        /// <summary>Kept free at the top of the screen, so the tab's edge and close button are never cut.</summary>
        public const float TopMargin = 8f;

        /// <summary>The bottom bar under the inspect pane (MainButtonDef.ButtonHeight).</summary>
        public const float BottomBar = 35f;

        /// <summary>The game's own inspect pane (InspectPaneUtility).</summary>
        public const float VanillaPane = 165f;

        /// <summary>
        /// A tab is never squeezed below this, even if that leaves part of it off screen: on a screen this small the
        /// tab's scroll view still shows something useful, a sliver wouldn't.
        /// </summary>
        public const float Floor = 160f;

        /// <summary>The room above an inspect pane whose top is at <paramref name="paneTopY"/>.</summary>
        public static float Room(float paneTopY) => paneTopY - TabButtons - TopMargin;

        /// <summary>The height for a tab that would like <paramref name="wanted"/>: that, or the room there is.</summary>
        public static float Height(float wanted, float paneTopY) => Math.Min(wanted, Math.Max(Floor, Room(paneTopY)));

        /// <summary>The top of the inspect pane on a screen <paramref name="pixels"/> tall at an interface scale, with a pane this tall.</summary>
        public static float PaneTopY(float pixels, float uiScale, float paneHeight) => pixels / Math.Max(0.5f, uiScale) - paneHeight - BottomBar;

        /// <summary>Where the top edge of a tab this tall ends up (0 is the top of the screen).</summary>
        public static float TabTop(float height, float paneTopY) => paneTopY - TabButtons - height;

        // ---------------- Rimfolk's column in the game's Social tab ----------------
        // The Social tab opens at the left edge of the screen, 540 wide (ITab_Pawn_Social.Width). With the column it
        // grows to the right by the column and a gap, as far as the screen allows: 912 on a 1920 screen, still 912 at
        // 1080 pixels and scale 2 (960 units), 902 at 1366 pixels and scale 1.5 (910 units, a 350 column), and no column
        // at all where under 300 would fit (1280 pixels at scale 2).

        /// <summary>The game's Social tab, as wide as the game makes it.</summary>
        public const float SocialWidth = 540f;

        /// <summary>Between the game's card and the column: a thin line with room either side.</summary>
        public const float ColumnGap = 12f;

        /// <summary>The column's width where there's room, and the least it's drawn at.</summary>
        public const float ColumnPreferred = 360f, ColumnMin = 300f;

        /// <summary>Kept free at the right edge of the screen.</summary>
        public const float ScreenMargin = 8f;

        /// <summary>
        /// The column's width beside a Social tab <paramref name="baseWidth"/> wide on a screen <paramref name="screenWidth"/>
        /// units wide: the preferred width, less on a narrow screen, 0 (no column) where not even the least would fit.
        /// </summary>
        public static float ColumnWidth(float screenWidth, float baseWidth)
        {
            float room = screenWidth - ScreenMargin - baseWidth - ColumnGap;
            return room < ColumnMin ? 0f : Math.Min(ColumnPreferred, room);
        }

        /// <summary>The Social tab's width with a column this wide (just the base without one).</summary>
        public static float WithColumn(float baseWidth, float column) => column <= 0f ? baseWidth : baseWidth + ColumnGap + column;

        /// <summary>
        /// Whether a Social tab window this wide has room for the column. When another mod keeps the tab at its own width,
        /// it doesn't, and the column isn't drawn.
        /// </summary>
        public static bool ColumnFits(float windowWidth, float baseWidth) => windowWidth >= baseWidth + ColumnGap + ColumnMin - 0.5f;

        /// <summary>
        /// The Social tab's own width: what it is now, unless that is the width Rimfolk gave it last time (then the one
        /// found before that). So a tab another mod made wider keeps that as its base, and Rimfolk's column goes beside it.
        /// </summary>
        public static float BaseWidth(float now, float lastSet, float knownBase) =>
            knownBase > 0f && lastSet > 0f && Math.Abs(now - lastSet) < 0.5f ? knownBase : now;
    }
}
