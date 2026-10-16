using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The sizes of This week (cards in one or two columns) and of the council's tables (People → Everyone, Council →
    /// Dilemmas): pure arithmetic with no game types, so Tools/Tests can check every width from a small window at a
    /// big interface scale to a wide screen.
    /// </summary>
    public static class CouncilLayoutMath
    {
        // ---------------- This week ----------------

        /// <summary>Below this width the cards stack in one column.</summary>
        public const float TwoColumnsFrom = 700f;
        public const float ColumnGap = 12f;
        public const float CardHeader = 30f;
        public const float CardPad = 6f;
        public const float RowHeight = 26f;
        /// <summary>The most rows a card shows; the rest are one "and N more" row that opens the page.</summary>
        public const int MaxRows = 6;

        public static int Columns(float width) => width >= TwoColumnsFrom ? 2 : 1;

        public static float ColumnWidth(float width, int columns) =>
            columns <= 1 ? Math.Max(0f, width) : Math.Max(0f, (width - ColumnGap * (columns - 1)) / columns);

        /// <summary>How many of a card's rows show (the rest fold into one "more" row).</summary>
        public static int ShownRows(int rows, int max = MaxRows)
        {
            if (rows <= 0) return 0;
            if (max < 1) max = 1;
            // A single row over the cap shows itself rather than "and 1 more".
            return rows <= max + 1 ? rows : max;
        }

        /// <summary>A card's height: its header, the rows that show, a "more" row when some don't, or one line saying it's empty.</summary>
        public static float CardHeight(int rows, int max = MaxRows, float emptyLine = RowHeight)
        {
            int shown = ShownRows(rows, max);
            float body = rows == 0 ? emptyLine : shown * RowHeight + (rows > shown ? RowHeight : 0f);
            return CardHeader + body + CardPad;
        }

        /// <summary>Which column each card goes into, in order: each into the shorter column so far (left on a tie).</summary>
        public static int[] Place(float[] heights, int columns, float gap = ColumnGap)
        {
            var into = new int[heights?.Length ?? 0];
            if (heights == null || heights.Length == 0) return into;
            columns = Math.Max(1, columns);
            var used = new float[columns];
            for (int i = 0; i < heights.Length; i++)
            {
                int best = 0;
                for (int c = 1; c < columns; c++)
                    if (used[c] < used[best] - 0.01f)
                        best = c;
                into[i] = best;
                used[best] += heights[i] + gap;
            }
            return into;
        }

        /// <summary>The share of a mood balance bar that is good: Rimfolk's good moods against its bad ones (both as positive sizes).</summary>
        public static float GoodShare(float good, float bad)
        {
            good = Math.Max(0f, good);
            bad = Math.Max(0f, Math.Abs(bad));
            return good + bad <= 0.001f ? 0.5f : good / (good + bad);
        }

        // ---------------- Tables ----------------

        public const float CellGap = 6f;

        // People → Everyone, in the order of PeopleTable.Col: name, mood, Rimfolk, trust, strain, setbacks, wish, story,
        // health, quirks. All fit the council window's usual width; on a narrower one quirks go first, then the story.
        public static readonly float[] PeopleMin = { 124f, 80f, 50f, 72f, 94f, 98f, 100f, 92f, 88f, 76f };
        public static readonly float[] PeopleWeight = { 1.2f, 0.6f, 0.2f, 0.5f, 0.8f, 1f, 1f, 0.9f, 0.8f, 0.6f };
        public static readonly int[] PeoplePriority = { 0, 0, 4, 1, 1, 3, 2, 5, 3, 6 };

        // Council → Dilemmas: day, question, answer, who it was about, how it went.
        public static readonly float[] DilemmaMin = { 60f, 140f, 140f, 90f, 160f };
        public static readonly float[] DilemmaWeight = { 0f, 1.1f, 1.2f, 0.4f, 1.4f };
        public static readonly int[] DilemmaPriority = { 0, 0, 0, 1, 0 };

        /// <summary>The width a page gets in the council window at its usual size (1010 wide, less the margins and a scroll bar).</summary>
        public const float UsualTableWidth = 1010f - 36f - 24f - 16f;

        /// <summary>
        /// Lays out a table's columns across a width. Each column has a smallest width, a share of the room left over, and a
        /// priority (0: always shown; higher numbers give way first when the width runs out). Returns false when even the
        /// columns that must stay don't fit (they're then squeezed below their smallest width, never past the edge).
        /// </summary>
        public static bool Layout(float width, float[] min, float[] weight, int[] priority, float[] x, float[] w, bool[] shown, float gap = CellGap)
        {
            int n = min.Length;
            for (int i = 0; i < n; i++) shown[i] = true;
            float Need()
            {
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!shown[i]) continue;
                    sum += min[i];
                    count++;
                }
                return sum + gap * Math.Max(0, count - 1);
            }
            // Hide the columns that give way first until the rest fit.
            while (Need() > width)
            {
                int drop = -1;
                for (int i = 0; i < n; i++)
                {
                    if (!shown[i] || priority[i] <= 0) continue;
                    if (drop < 0 || priority[i] > priority[drop] || (priority[i] == priority[drop] && i > drop)) drop = i;
                }
                if (drop < 0) break;
                shown[drop] = false;
            }
            float need = Need();
            bool fits = need <= width + 0.01f;
            float extra = Math.Max(0f, width - need);
            float weights = 0f;
            for (int i = 0; i < n; i++)
                if (shown[i])
                    weights += Math.Max(0f, weight[i]);
            // Squeezed: every shown column shrinks by the same share so the table still ends at the edge.
            float squeeze = fits || need <= 0f ? 1f : Math.Max(0f, (width - gap * Math.Max(0, Count(shown) - 1)) / Math.Max(1f, need - gap * Math.Max(0, Count(shown) - 1)));
            float cx = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!shown[i])
                {
                    x[i] = cx;
                    w[i] = 0f;
                    continue;
                }
                float cw = min[i] * squeeze + (weights > 0f ? extra * Math.Max(0f, weight[i]) / weights : 0f);
                x[i] = cx;
                w[i] = cw;
                cx += cw + gap;
            }
            return fits;
        }

        private static int Count(bool[] shown)
        {
            int c = 0;
            foreach (var s in shown)
                if (s)
                    c++;
            return c;
        }
    }
}
