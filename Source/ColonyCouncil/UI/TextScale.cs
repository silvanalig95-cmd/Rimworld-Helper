using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Text larger or smaller than the game's three fonts, for a stretch of drawing: <c>using (TextScale.Push(1.15f)) { ... }</c>.
    /// The game has only Tiny, Small and Medium, each one shared style, so this sets the size of all three styles and puts
    /// it back when the scope ends, also when the drawing throws. Everything inside measures and draws at the new size
    /// (Text.CalcHeight, Text.CalcSize, Widgets.Label, buttons), so heights must be measured, not taken from
    /// Text.LineHeight, which the game works out once. Tooltips are drawn later, at the usual size.
    /// </summary>
    /// <remarks>
    /// Scopes don't nest: an inner one does nothing. A font the game can't resize (not a dynamic font) is left alone.
    /// UiKit.Fit remembers its cuts by font, not size, so text inside a scope isn't cut with it.
    /// </remarks>
    public readonly struct TextScale : IDisposable
    {
        private static readonly int[] saved = new int[3];
        private static bool active;
        private readonly bool owns;

        private TextScale(bool owns)
        {
            this.owns = owns;
        }

        /// <summary>Is a scope drawing at another size right now?</summary>
        public static bool Active => active;

        public static TextScale Push(float scale)
        {
            if (active || Mathf.Abs(scale - 1f) < 0.01f) return default;
            var styles = Text.fontStyles;
            if (styles == null || styles.Length < saved.Length) return default;
            for (int i = 0; i < saved.Length; i++) saved[i] = styles[i]?.fontSize ?? 0;
            active = true;
            try
            {
                for (int i = 0; i < saved.Length; i++)
                {
                    int size = BaseSize(styles[i]);
                    if (size > 0) styles[i].fontSize = AlmanacLayoutMath.FontSize(size, scale);
                }
            }
            catch (Exception e)
            {
                Restore();
                CouncilLog.ErrorOnce("text size", e);
                return default;
            }
            return new TextScale(true);
        }

        public void Dispose()
        {
            if (owns) Restore();
        }

        private static void Restore()
        {
            var styles = Text.fontStyles;
            for (int i = 0; i < saved.Length && i < styles.Length; i++)
                if (styles[i] != null) styles[i].fontSize = saved[i];
            active = false;
        }

        /// <summary>The size a style draws at: its own, or its font's; 0 when it can't be resized.</summary>
        private static int BaseSize(GUIStyle style)
        {
            if (style?.font == null || !style.font.dynamic) return 0;
            return style.fontSize > 0 ? style.fontSize : style.font.fontSize;
        }
    }
}
