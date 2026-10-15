using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The settings window's sizes, free of game types so Tools/Tests can check them on small screens and big interface
    /// scales: how big the game's mod settings dialog may be on the screen, and how the space inside it is shared between
    /// the search box, the page list with its presets, and the page itself. Every part scrolls when it doesn't fit.
    /// </summary>
    public static class SettingsLayoutMath
    {
        /// <summary>The game's mod settings dialog: its usual size, its margin, the title row above our part and the close button below.</summary>
        public const float DialogWidth = 900f;
        public const float DialogHeight = 700f;
        public const float WindowMargin = 18f;
        public const float TitleArea = 40f;
        public const float CloseArea = 40f;
        /// <summary>Space kept free around the dialog when it has to shrink to fit the screen.</summary>
        public const float ScreenEdge = 6f;

        public const float TopBar = 32f;
        public const float Gap = 8f;
        public const float PageRow = 30f;
        public const float PresetRow = 28f;
        public const float PresetHeader = 24f;
        public const float LeftMin = 150f;
        public const float LeftMax = 220f;
        public const float ScrollBar = 16f;
        /// <summary>From this width a slider sits beside its label instead of under it.</summary>
        public const float SliderBesideWidth = 420f;

        /// <summary>The dialog's size on a screen of this many interface pixels: the usual size, shrunk to fit if the screen is smaller.</summary>
        public static void DialogSize(float screenW, float screenH, out float w, out float h)
        {
            w = Math.Min(DialogWidth, Math.Max(200f, screenW - 2f * ScreenEdge));
            h = Math.Min(DialogHeight, Math.Max(200f, screenH - 2f * ScreenEdge));
        }

        /// <summary>The rect the game gives the mod to draw in, for a dialog of this size.</summary>
        public static void ContentSize(float dialogW, float dialogH, out float w, out float h)
        {
            w = dialogW - 2f * WindowMargin;
            h = dialogH - 2f * WindowMargin - TitleArea - CloseArea;
        }

        /// <summary>The page list's width: a quarter of the space, within limits.</summary>
        public static float LeftWidth(float contentW) => Clamp(contentW * 0.25f, LeftMin, LeftMax);

        /// <summary>The height under the search box, shared by the page list and the page.</summary>
        public static float BodyHeight(float contentH) => Math.Max(0f, contentH - TopBar - Gap);

        /// <summary>The page's width, beside the page list.</summary>
        public static float PageWidth(float contentW) => Math.Max(0f, contentW - LeftWidth(contentW) - Gap);

        /// <summary>The width rows are drawn at inside the page's scroll view (inner padding and the scroll bar taken off).</summary>
        public static float RowWidth(float contentW) => Math.Max(0f, PageWidth(contentW) - 2f * Gap - ScrollBar);

        /// <summary>How tall the page list with its presets is.</summary>
        public static float LeftColumnHeight(int pages, int presets) => pages * PageRow + Gap * 2f + PresetHeader + presets * (PresetRow + 4f);

        /// <summary>Whether the page list has to scroll.</summary>
        public static bool LeftScrolls(float contentH, int pages, int presets) => LeftColumnHeight(pages, presets) > BodyHeight(contentH);

        public static bool SliderBeside(float rowWidth) => rowWidth >= SliderBesideWidth;

        /// <summary>The width of each of n buttons sharing a row, with a gap between them.</summary>
        public static float ButtonWidth(float rowWidth, int n, float gap = 6f) => n <= 0 ? 0f : Math.Max(0f, (rowWidth - gap * (n - 1)) / n);

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
