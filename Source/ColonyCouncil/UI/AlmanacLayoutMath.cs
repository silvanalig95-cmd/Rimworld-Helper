using System;

namespace ColonyCouncil
{
    /// <summary>How large the Almanac's pages are written: a step either side of the game's own size, and two up.</summary>
    public enum AlmanacTextSize
    {
        Small,
        Normal,
        Large,
        Larger
    }

    /// <summary>How a table on an Almanac page is drawn.</summary>
    public enum TableMode
    {
        /// <summary>Every column as wide as its longest cell: nothing wraps.</summary>
        Natural,
        /// <summary>Columns share the page: each keeps room for its longest word, the rest goes where the text is.</summary>
        Shared,
        /// <summary>Too narrow for columns: each row becomes a small card, its first cell as the title.</summary>
        Cards
    }

    /// <summary>
    /// The Almanac's layout, free of game types so Tools/Tests can try it on small screens, big interface scales and
    /// every text size: how wide the categories, the page list and the page are, and how a table's columns share the
    /// page's width. Widths are in the game's interface units; text widths are measured by the caller (Text.CalcSize)
    /// in the font size the page is drawn in.
    /// </summary>
    /// <remarks>
    /// The council window is at most 1010 wide (MainTabWindow_Council), and its tabs get that less 60 for the window's
    /// margins: 950 on a 1080 or 768 pixel screen at scale 1, 880 on a 1080 pixel screen at scale 2, and 603 on a 768
    /// pixel screen at scale 2. Before, the page list kept 290 of that and the page got about 440, so on the ailments page
    /// a four-column table cut its last column to a few letters and broke its words.
    /// </remarks>
    public static class AlmanacLayoutMath
    {
        // ---------------- Text size ----------------

        /// <summary>How much larger than the game's fonts each step draws.</summary>
        public static float Scale(AlmanacTextSize size)
        {
            switch (size)
            {
                case AlmanacTextSize.Small: return 0.88f;
                case AlmanacTextSize.Large: return 1.15f;
                case AlmanacTextSize.Larger: return 1.32f;
                default: return 1f;
            }
        }

        /// <summary>A font of <paramref name="baseSize"/> points at this scale, never below 6.</summary>
        public static int FontSize(int baseSize, float scale) => Math.Max(6, (int)Math.Round(baseSize * scale, MidpointRounding.AwayFromZero));

        // ---------------- The three columns ----------------

        /// <summary>The space between the categories and the page list, and between the list and the page.</summary>
        public const float CategoryGap = 8f;
        public const float PageGap = 10f;

        /// <summary>The thin strip the categories and the page list fold into, with buttons to unfold and to turn pages.</summary>
        public const float Strip = 30f;

        public const float CategoryMin = 130f, CategoryMax = 180f, CategoryShare = 0.18f;
        public const float ListMin = 160f, ListMax = 240f, ListShare = 0.23f;

        /// <summary>
        /// The widths of the categories, the page list and the page in a tab <paramref name="width"/> wide. Folded, the
        /// categories and the list are one thin strip (<paramref name="category"/> is its width, <paramref name="list"/> 0).
        /// </summary>
        public static void Columns(float width, bool folded, out float category, out float list, out float page)
        {
            if (folded)
            {
                category = Strip;
                list = 0f;
                page = Math.Max(0f, width - Strip - PageGap);
                return;
            }
            category = Clamp(width * CategoryShare, CategoryMin, CategoryMax);
            list = Clamp(width * ListShare, ListMin, ListMax);
            page = Math.Max(0f, width - category - CategoryGap - list - PageGap);
        }

        /// <summary>The council window's tab width on a screen this many interface units wide (its 1010 less the margins).</summary>
        public static float TabWidth(float screenWidth) => Math.Min(1010f, screenWidth - 20f) - 60f;

        /// <summary>Inside the page: a little room on the left, the scroll bar, and room between the text and the scroll bar.</summary>
        public const float ArticleLeft = 4f, ScrollBar = 16f, ArticleRight = 8f;

        /// <summary>How wide the text of a page this wide is.</summary>
        public static float TextWidth(float page) => Math.Max(0f, page - ArticleLeft - ScrollBar - ArticleRight);

        // ---------------- Tables ----------------

        /// <summary>The space inside a cell, left and right together.</summary>
        public const float CellPad = 10f;

        /// <summary>
        /// A little more than the longest word, for rounding: the game nudges labels onto whole pixels at some interface
        /// scales, and a word that only just fits would break.
        /// </summary>
        public const float WordSlack = 2f;

        /// <summary>
        /// Each column gets at least this many characters' width (unless all of its text is shorter), so no column ends up
        /// a narrow strip of one or two words a line.
        /// </summary>
        public const float ComfortChars = 14f;

        /// <summary>
        /// A table reads better as cards when one of its columns is narrow (under <see cref="WideChars"/> characters) and
        /// would still wrap its typical cell into more lines than this: a strip of two or three words a line. A wide column
        /// of long text (a name, then a paragraph) stays a table, however many lines it takes.
        /// </summary>
        public const float MaxTypicalLines = 3f;
        public const float WideChars = 30f;

        /// <summary>
        /// The column widths of a table on a page <paramref name="available"/> wide.
        /// <paramref name="word"/> is each column's longest word, <paramref name="full"/> its longest cell on one line and
        /// <paramref name="typical"/> its average cell on one line, all text widths without the cell's padding;
        /// <paramref name="charWidth"/> is an average letter's width in the font used.
        /// </summary>
        /// <returns>
        /// <see cref="TableMode.Natural"/> when every column fits on one line; <see cref="TableMode.Shared"/> when each
        /// column keeps room for its longest word and a comfortable minimum, and shares the rest in proportion to how much
        /// more its typical cell needs (never wider than its longest cell); <see cref="TableMode.Cards"/> when even the
        /// minimum doesn't fit, or a column stays a narrow strip (see <see cref="Squeezed"/>). <paramref name="widths"/> is
        /// filled for the first two.
        /// </returns>
        public static TableMode Widths(float[] word, float[] full, float[] typical, float charWidth, float available, float[] widths)
        {
            int n = full.Length;
            float natural = 0f;
            for (int c = 0; c < n; c++) natural += Natural(word, full, c);
            if (natural <= available)
            {
                for (int c = 0; c < n; c++) widths[c] = Natural(word, full, c);
                return TableMode.Natural;
            }
            float floor = 0f;
            var weight = new float[n];
            for (int c = 0; c < n; c++)
            {
                widths[c] = Floor(word, full, c, charWidth);
                floor += widths[c];
                // What the typical cell needs beyond the minimum: a column whose cells mostly fit already asks for little.
                weight[c] = Math.Max(1f, typical[c] + CellPad - widths[c]);
            }
            if (floor > available) return TableMode.Cards;

            // What's left goes to the columns by those weights, none beyond its longest cell; a column that is full passes
            // its share on to the others.
            for (int round = 0; round < n + 1; round++)
            {
                float left = available;
                float weights = 0f;
                for (int c = 0; c < n; c++)
                {
                    left -= widths[c];
                    if (widths[c] < Natural(word, full, c) - 0.01f) weights += weight[c];
                }
                if (left < 0.5f || weights <= 0f) break;
                for (int c = 0; c < n; c++)
                {
                    float nat = Natural(word, full, c);
                    if (widths[c] >= nat - 0.01f) continue;
                    widths[c] = Math.Min(nat, widths[c] + left * weight[c] / weights);
                }
            }
            for (int c = 0; c < n; c++)
                if (Squeezed(typical[c], widths[c], charWidth)) return TableMode.Cards;
            return TableMode.Shared;
        }

        /// <summary>A column narrower than <see cref="WideChars"/> that still wraps its typical cell into more than <see cref="MaxTypicalLines"/> lines.</summary>
        public static bool Squeezed(float typical, float width, float charWidth) =>
            width - CellPad < WideChars * charWidth && Lines(typical, width) > MaxTypicalLines;

        /// <summary>A column on one line: its longest cell, and never narrower than its longest word.</summary>
        public static float Natural(float[] word, float[] full, int c) => Math.Max(full[c], word[c] + WordSlack) + CellPad;

        /// <summary>The narrowest a column may be: its longest word, and a comfortable few words unless its text is shorter.</summary>
        public static float Floor(float[] word, float[] full, int c, float charWidth) =>
            Math.Min(Natural(word, full, c), Math.Max(word[c] + WordSlack, Math.Min(full[c], ComfortChars * charWidth)) + CellPad);

        /// <summary>About how many lines a text this wide takes in a column this wide.</summary>
        public static float Lines(float textWidth, float columnWidth) => textWidth <= 0f ? 1f : textWidth / Math.Max(1f, columnWidth - CellPad);

        // ---------------- Cards ----------------

        /// <summary>The space inside a card, left and right together, and the gap between a line's label and its value.</summary>
        public const float CardPad = 16f;
        public const float LabelGap = 10f;

        /// <summary>A card's labels stand in their own column when they take no more than this share of the card.</summary>
        public const float LabelShare = 0.38f;

        /// <summary>
        /// Whether a card's lines put their labels in a column of their own (widest label <paramref name="label"/>) with
        /// the values beside them, rather than "Label: value" on one wrapped line: only when the labels are narrow enough
        /// and the values' longest word still fits beside them.
        /// </summary>
        public static bool LabelsBeside(float cardWidth, float label, float valueWord)
        {
            float inside = cardWidth - CardPad;
            float column = label + LabelGap;
            return column <= inside * LabelShare && inside - column >= valueWord + WordSlack;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
