using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The measures of a view's header (docs/design/interface.md, "The look", components 1 and 2): the icon, the title
    /// with the stat and the "?" on the right of its first line, the one line saying what it is under it, and, for a view
    /// with modes, a row of small buttons under that, one lit. The modes keep their place whatever the title, so the
    /// buttons don't move under the mouse from one mode to the next. Pure: Tools/Tests checks it.
    /// </summary>
    public static class ViewHeadMath
    {
        /// <summary>The header without modes, with the space below it (UiKit.HeaderHeight).</summary>
        public const float Base = 56f;
        /// <summary>The row of modes adds this much.</summary>
        public const float ModeRow = 26f;
        public const float ModeH = 22f;
        /// <summary>The icon and the space after it, where the title, its line and the modes begin.</summary>
        public const float IconSpace = 42f;
        /// <summary>The "?" button, and the gap before what lies right of it.</summary>
        public const float Help = 22f, HelpGap = 8f;
        public const float ModeGap = 4f;
        /// <summary>Room around a mode's label, and the least a squeezed mode keeps.</summary>
        public const float ModePad = 20f, MinMode = 36f;
        /// <summary>Where the row of modes sits under the header's top.</summary>
        public const float ModesY = 52f;

        /// <summary>The header's height, with the space below it.</summary>
        public static float Height(bool modes) => modes ? Base + ModeRow : Base;

        /// <summary>
        /// The "?" button's left edge in a header <paramref name="width"/> wide whose last <paramref name="reserveRight"/>
        /// pixels hold the page's own controls; the right edge left for the stat is <paramref name="right"/>.
        /// </summary>
        public static float HelpX(float width, float reserveRight, float iconSpace, out float right)
        {
            float edge = Math.Max(iconSpace, width - reserveRight);
            float x = Math.Max(iconSpace, edge - Help);
            right = Math.Max(iconSpace, x - HelpGap);
            return x;
        }

        /// <summary>
        /// The modes' places from <paramref name="x"/> in <paramref name="width"/>: each at its own width (its label and
        /// room around it) one after another, or all squeezed evenly when they don't fit (each at least
        /// <see cref="MinMode"/>, the last cut at the edge). Fills <paramref name="xs"/> and <paramref name="ws"/>.
        /// </summary>
        public static void Modes(float x, float width, float[] natural, int count, float[] xs, float[] ws)
        {
            int n = natural == null ? 0 : Math.Min(count, natural.Length);
            if (n == 0) return;
            float total = 0f;
            for (int i = 0; i < n; i++) total += natural[i];
            total += ModeGap * (n - 1);
            float even = Math.Max(MinMode, (width - ModeGap * (n - 1)) / n);
            float cx = x;
            for (int i = 0; i < n; i++)
            {
                float w = total <= width ? natural[i] : even;
                if (cx > x + width) cx = x + width;
                if (cx + w > x + width) w = Math.Max(0f, x + width - cx);
                xs[i] = cx;
                ws[i] = w;
                cx += w + ModeGap;
            }
        }

        /// <summary>A mode's own width: its label's and the room around it.</summary>
        public static float ModeWidth(float labelWidth) => labelWidth + ModePad;
    }
}
