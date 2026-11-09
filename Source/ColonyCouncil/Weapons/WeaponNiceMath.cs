using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the suggestions sit in Nice Inventory Tab's equipment box (Weapons/WeaponNiceTab.cs), without game types
    /// (Tools/Tests try it): its widths, as Nice Inventory Tab lays its tab out (read from its decompiled build: the tab
    /// 560 to 960 pixels wide by its setting, 760 at first; the layout's margins 6 a side; two columns of equal stretch with 6
    /// between, the equipment box in one; the box's scrolling list 4 in from each side, its scroll bar 14 when its rows are
    /// taller than the box); the rows' heights, so the box makes room for them; where the row goes among the box's own rows
    /// (under the last weapon or other thing held, above the inventory's cells); and which buttons fit a row's width.
    /// </summary>
    public static class WeaponNiceMath
    {
        /// <summary>Nice Inventory Tab's tab width: at first, and its setting's least and most.</summary>
        public const float TabDefault = 760f, TabMin = 560f, TabMax = 960f;

        /// <summary>The width of a row in its equipment box, the same in its two layouts (the equipment box fills its column).</summary>
        public static float RowWidth(float tabWidth, bool scrolling) => (tabWidth - 12f - 6f) / 2f - 8f - (scrolling ? 14f : 0f);

        /// <summary>A suggestion's row: its height (its warning line adds one), the space between rows (Nice Inventory Tab's list's), and the quiet line's.</summary>
        public const float RowH = 70f, WarnH = 16f, Gap = 3f, QuietH = 26f;

        /// <summary>Inside a row: its margin, the picture's tile (as wide as Nice Inventory Tab's own rows' pictures, 1.2 its height), the gap after it.</summary>
        public const float Margin = 6f, TileShape = 1.2f, TileGap = 6f;

        public static float RowHeight(bool warning) => RowH + (warning ? WarnH : 0f);

        /// <summary>
        /// The room the box gives the suggestions: their rows and the gaps between them, the quiet line alone, or none; with
        /// rows, <paramref name="gapLine"/> adds the quiet line under them (an empty slot of the kit nothing fills).
        /// </summary>
        public static float Height(IList<bool> warnings, bool quiet, bool gapLine = false)
        {
            if (warnings != null && warnings.Count > 0)
            {
                float h = 0f;
                for (int i = 0; i < warnings.Count; i++) h += RowHeight(warnings[i]) + (i > 0 ? Gap : 0f);
                return h + (gapLine ? Gap + QuietH : 0f);
            }
            return quiet ? QuietH : 0f;
        }

        /// <summary>What a row of the box is, for <see cref="Place"/>: one of its weapons or other things held, Rimfolk's own row, anything else.</summary>
        public enum Child
        {
            Other,
            Held,
            Ours
        }

        /// <summary>
        /// Where Rimfolk's row should be among the box's rows: right under the last thing held (its weapons, sidearms, belts),
        /// or first when there is none. Returns the index it should have once it is in its place (counted with it in).
        /// </summary>
        public static int Place(IList<Child> rows)
        {
            int last = -1, n = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == Child.Ours) continue;
                if (rows[i] == Child.Held) last = n;
                n++;
            }
            return last + 1;
        }

        /// <summary>Whether Rimfolk's row is where <see cref="Place"/> puts it (else it is moved, and the box laid out again).</summary>
        public static bool InPlace(IList<Child> rows)
        {
            int at = -1;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] == Child.Ours)
                    at = i;
            return at >= 0 && at == Place(rows);
        }

        /// <summary>The picture's tile in a row: as high as a row without a warning line inside its margins, 1.2 times as wide.</summary>
        public static float TileH => RowH - 2f * Margin;

        public static float TileW => (float)Math.Round(TileH * TileShape);

        /// <summary>The text's width in a row: the row less its margins, the picture's tile and the gap after it.</summary>
        public static float TextWidth(float rowWidth) => rowWidth - 2f * Margin - TileW - TileGap;

        /// <summary>
        /// Whether "Not now" is a word on a row or only its cross: the word when the row's acts and it fit the text's width
        /// (4 between buttons), the cross (20) otherwise.
        /// </summary>
        public static bool NotNowAsWord(float textWidth, float actsWidth, float notNowWidth) => actsWidth + 4f + notNowWidth <= textWidth;

        /// <summary>The cross standing for "Not now" on a narrow row.</summary>
        public const float CrossW = 20f;
    }
}
