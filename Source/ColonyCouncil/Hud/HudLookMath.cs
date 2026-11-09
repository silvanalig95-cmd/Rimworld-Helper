using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The on-screen panels in Rimfolk's own look (docs/design/rimfolk-look.md, stage 3, point 3; Hud/Look/), free of game
    /// types so Tools/Tests can check them: the card's fill for each of the panels' looks and how see-through it is, the
    /// sizes of a card's title row and of each panel's rows (so the panels still start where they did without touching one
    /// another), where each part of a colonist's row sits at any scale, the bars' shares for values with a range (days of
    /// food against the warning line, medicine against the advisor's, trust either side of nothing), and the clock that
    /// times the drawing of either look.
    /// </summary>
    public static class HudLookMath
    {
        // ---------------- Sizes at scale 1 (each is rounded at the panel's scale) ----------------

        /// <summary>A card's title row: the panel's icon and name, its buttons while the mouse is on it.</summary>
        public const float TitleHeight = 16f;

        /// <summary>The card's padding round its rows.</summary>
        public const float Pad = 4f;

        /// <summary>
        /// A line of the vitals, the council and Following: two less than the classic's, so the cards, their title rows
        /// included, still start clear of one another where Rimfolk places them.
        /// </summary>
        public const float LineRow = 18f;

        /// <summary>An alert's row, full and compact.</summary>
        public const float AlertRow = 18f, AlertRowCompact = 17f;

        /// <summary>A colonist's row on the mood board: their name and marks, the mood bar, the heart's line under it.</summary>
        public const float MoodRow = 36f;

        /// <summary>A colonist's row of the work panel, and one of its colony lines above them.</summary>
        public const float WorkRow = 18f, WorkLine = 18f;

        /// <summary>The "and N more" line under the rows.</summary>
        public const float MoreLine = 16f;

        /// <summary>The coloured stripe down an urgent row's left edge.</summary>
        public const float Stripe = 3f;

        /// <summary>An icon on a row, and one of the marks at a colonist's row's end.</summary>
        public const float RowIcon = 13f, Mark = 12f;

        /// <summary>The mood bar on a colonist's row, the heart's line under it (strain, then the good side), and the gap between.</summary>
        public const float MoodBarHeight = 6f, HeartLine = 5f, HeartGap = 2f;

        /// <summary>A health row's bar for their summary health, under the name.</summary>
        public const float HealthBar = 3f;

        /// <summary>A value at a scale, rounded to the pixel as the panels round it (Unity rounds halves to even, as here).</summary>
        public static float At(float v, float s) => (float)Math.Round(v * s);

        // ---------------- The card ----------------

        /// <summary>
        /// The card's colour for the panels' look: the look's panel, or its darker window ground for the darker look and the
        /// clear one (the clear one less solid, so the darker colour keeps its words readable).
        /// </summary>
        public static uint CardColour(HudTheme theme) => theme == HudTheme.Dark ? SkinMath.Panel : SkinMath.WindowGround;

        /// <summary>
        /// How solid the card is for the look at full opacity: nearly solid for the dark and darker looks, so every word holds
        /// 4.5:1 over snow; the clear one as see-through as holds it over the game's soil.
        /// </summary>
        public static float CardAlpha(HudTheme theme) => theme == HudTheme.Darker ? 0.97f : theme == HudTheme.Clear ? 0.72f : 0.95f;

        /// <summary>
        /// How solid the card is: the look's, times the panel's own opacity, and fuller while the mouse is on it (as the
        /// panels' background always was).
        /// </summary>
        public static float CardAlpha(HudTheme theme, float opacity, bool hovered)
        {
            float b = CardAlpha(theme);
            float a = b * Clamp01(opacity);
            if (hovered) a = Math.Max(a, Math.Min(0.97f, b + 0.15f));
            return a;
        }

        /// <summary>A colour laid at <paramref name="alpha"/> over a ground: what the eye sees of a see-through card over the map.</summary>
        public static uint Over(uint colour, float alpha, uint ground)
        {
            alpha = Clamp01(alpha);
            uint Mix(int shift)
            {
                float a = (colour >> shift) & 0xff, b = (ground >> shift) & 0xff;
                return (uint)Math.Round(a * alpha + b * (1f - alpha)) & 0xff;
            }
            return (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
        }

        /// <summary>
        /// The map grounds the contrast is held against: bright snow under the dark and darker looks, the game's soil under
        /// the clear one (mostly see-through by choice, it can't hold 4.5:1 over snow).
        /// </summary>
        public const uint Snow = 0xe6e6e6, Soil = 0x6e6e64;

        // ---------------- The panels' sizes ----------------

        /// <summary>A panel of lines (the vitals, the council, Following): its rows (two to a row compact), "and N more", the padding.</summary>
        public static float LinesHeight(int lines, bool more, bool compact, float s)
        {
            float pad = At(Pad, s);
            if (lines <= 0) return pad * 2f + At(LineRow, s);
            int rows = compact ? (lines + 1) / 2 : lines;
            return pad * 2f + rows * At(LineRow, s) + (more ? At(MoreLine, s) : 0f);
        }

        /// <summary>The alerts: a row each, "and N more", the padding.</summary>
        public static float AlertsHeight(int rows, bool more, bool compact, float s)
        {
            float pad = At(Pad, s);
            float rh = At(compact ? AlertRowCompact : AlertRow, s);
            if (rows <= 0) return pad * 2f + rh;
            return pad * 2f + rows * rh + (more ? At(MoreLine, s) : 0f);
        }

        /// <summary>The mood board in full: a row each, "and N more", the padding.</summary>
        public static float MoodsHeight(int rows, bool more, float s)
        {
            float pad = At(Pad, s);
            if (rows <= 0) return At(26f, s);
            return pad * 2f + rows * At(MoodRow, s) + (more ? At(MoreLine, s) : 0f);
        }

        /// <summary>Work and idle: the colony's lines with the divider under them, a row for each colonist, "and N more", the padding.</summary>
        public static float WorkHeight(int colonyLines, int rows, bool more, float s)
        {
            float pad = At(Pad, s);
            float top = colonyLines > 0 ? colonyLines * At(WorkLine, s) + At(4f, s) : 0f;
            if (rows <= 0) return pad * 2f + top + At(20f, s);
            return pad * 2f + top + rows * At(WorkRow, s) + (more ? At(MoreLine, s) : 0f);
        }

        /// <summary>
        /// A health row with its summary under the name: the name's line, the bar of their summary health, the lines of
        /// chips it took (one or two); one line when the details are off.
        /// </summary>
        public static float HealthRow(bool twoLines, int chipLines, float s)
        {
            if (!twoLines) return At(20f, s);
            return At(18f, s) + At(HealthBar + 2f, s) + Math.Max(1, chipLines) * At(14f, s);
        }

        /// <summary>A card in full: the title row on top of what the panel measures.</summary>
        public static float WithTitle(float body, float s) => body + At(TitleHeight, s);

        // ---------------- A colonist's row on the mood board ----------------

        /// <summary>Where each part of a colonist's row on the mood board sits, in the row's own coordinates.</summary>
        public struct MoodRowLayout
        {
            /// <summary>The portrait (0 wide without one), the name's line and its marks, the mood bar and its number, the heart's line, what drags them down.</summary>
            public float portraitX, portraitY, portraitSize;
            public float nameX, nameY, nameH, nameRight;
            public float barX, barY, barW, barH;
            public float numberX, numberW;
            public float heartY, heartH;
            public float dragX, dragW;
        }

        /// <summary>
        /// A colonist's row: the portrait at the left over both lines; the name on the first line with room kept at its end
        /// for <paramref name="marks"/> marks; under it the mood bar (wide, or as wide as the row when nothing is said after
        /// it), its number small at its end, the heart's line under the bar, and what drags them down after the number.
        /// </summary>
        public static MoodRowLayout LayMoodRow(float width, float s, bool portrait, int marks, bool drag, float numberWidth)
        {
            var l = new MoodRowLayout();
            float pad = At(3f, s);
            // Clear of the stripe down an urgent row's edge.
            float x = At(Stripe + 3f, s);
            if (portrait)
            {
                l.portraitSize = At(26f, s);
                l.portraitX = x;
                l.portraitY = (float)Math.Round((At(MoodRow, s) - l.portraitSize) / 2f);
                x += l.portraitSize + At(6f, s);
            }
            float right = width - pad;
            l.nameX = x;
            l.nameY = At(2f, s);
            l.nameH = At(17f, s);
            float markRoom = marks > 0 ? marks * (At(Mark, s) + 1f) + 2f : 0f;
            l.nameRight = Math.Max(x, right - markRoom);
            l.barX = x;
            l.barH = Math.Max(4f, At(MoodBarHeight, s));
            l.barY = l.nameY + l.nameH + At(3f, s);
            l.numberW = numberWidth;
            float room = right - x;
            l.barW = drag ? Math.Max(At(40f, s), Math.Min(At(96f, s), room - numberWidth - At(4f, s))) : Math.Max(0f, room - numberWidth - At(4f, s));
            l.numberX = l.barX + l.barW + At(4f, s);
            l.heartY = l.barY + l.barH + At(HeartGap, s);
            // At the smallest scales the heart's line gives up a pixel rather than leave the row.
            l.heartH = Math.Max(1f, Math.Min(At(HeartLine, s), At(MoodRow, s) - 1f - l.heartY));
            l.dragX = l.numberX + numberWidth + At(5f, s);
            l.dragW = Math.Max(0f, right - l.dragX);
            return l;
        }

        // ---------------- Bars for values with a range ----------------

        /// <summary>
        /// Days of food as a bar: the warning line (the settings', the advisor's own at first) at its middle, so the bar is
        /// full at twice it and half full where it starts to warn.
        /// </summary>
        public static float FoodFill(float days, float warn) => warn <= 0f ? 1f : Clamp01(days / (2f * warn));

        /// <summary>Where a number of days sits on the food bar (the warning line at the middle, the bad one before it).</summary>
        public static float FoodLine(float days, float warn) => FoodFill(days, warn);

        /// <summary>Medicine as a bar: the advisor's line (two for each colonist) at its middle.</summary>
        public static float StockFill(int count, int line) => line <= 0 ? (count > 0 ? 1f : 0f) : Clamp01(count / (2f * line));

        /// <summary>
        /// Trust (from -100 to 100) as a bar either side of nothing: from the middle to where it stands, so distrust fills
        /// leftwards and trust rightwards.
        /// </summary>
        public static void TrustBar(float trust, out float from, out float to)
        {
            float at = Clamp01((trust + 100f) / 200f);
            from = Math.Min(0.5f, at);
            to = Math.Max(0.5f, at);
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        // ---------------- The clock ----------------

        /// <summary>
        /// The panels' drawing timed in either look: the stopwatch's ticks spent painting, the frames painted and the panels
        /// in them, so the two can be set side by side (the debug action "On-screen panels: time the classic and refined
        /// drawing").
        /// </summary>
        public sealed class Clock
        {
            public long ticks;
            public int frames, panels;

            public void Add(long elapsed, int count)
            {
                ticks += elapsed;
                panels += count;
            }

            public void Frame() => frames++;

            public void Clear()
            {
                ticks = 0;
                frames = 0;
                panels = 0;
            }

            /// <summary>Microseconds a frame, for a stopwatch of <paramref name="frequency"/> ticks a second.</summary>
            public double PerFrame(long frequency) => frames <= 0 || frequency <= 0 ? 0.0 : ticks * 1e6 / frequency / frames;

            /// <summary>Microseconds a panel.</summary>
            public double PerPanel(long frequency) => panels <= 0 || frequency <= 0 ? 0.0 : ticks * 1e6 / frequency / panels;
        }

        /// <summary>The two halves of a timing run: so many painted frames in the refined look, then as many in the classic.</summary>
        public const int MeasureFrames = 300;

        /// <summary>Which look a timing run draws in after <paramref name="framesDone"/> frames: refined first, then classic; null when it's over.</summary>
        public static bool? MeasureLook(int framesDone) => framesDone < MeasureFrames ? true : framesDone < MeasureFrames * 2 ? false : (bool?)null;
    }
}
