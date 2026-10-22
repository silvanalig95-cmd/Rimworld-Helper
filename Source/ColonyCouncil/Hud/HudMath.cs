using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A rectangle on the screen, in UI pixels (no game types, for the tests).</summary>
    public struct HudBox
    {
        public float x, y, w, h;

        public HudBox(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public float XMax => x + w;
        public float YMax => y + h;
    }

    /// <summary>The panels' names, as kept in the settings file (HudPanelState.id), in the order of the menus.</summary>
    public static class HudIds
    {
        public const string Moods = "moods";
        public const string Trend = "trend";
        public const string Vitals = "vitals";
        public const string Council = "council";
        public const string Alerts = "alerts";
        public const string Health = "health";
        public const string Work = "work";
        public const string Inventory = "inventory";

        public static readonly string[] All = { Moods, Trend, Vitals, Council, Alerts, Health, Work, Inventory };
    }

    /// <summary>
    /// The layout maths of the on-screen panels, free of game types so Tools/Tests can check it: a panel's place kept as
    /// a fraction of the screen from the corner it's nearest to (so it survives a new resolution, and a panel against
    /// the bottom or right edge grows away from it), kept on the screen, snapped to the screen's edges and to the other
    /// panels; which rows show when there's room for only some (the critical ones always do); the trend graph's samples.
    /// </summary>
    public static class HudMath
    {
        /// <summary>Edges this close snap together (UI pixels).</summary>
        public const float SnapDistance = 8f;
        public const float MinScale = 0.75f, MaxScale = 1.5f;
        public const float MinOpacity = 0.25f, MaxOpacity = 1f;
        /// <summary>The thin bar shown over a panel under the mouse (drag handle and buttons), at scale 1.</summary>
        public const float HeaderHeight = 18f;

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

        public static float ClampScale(float s) => float.IsNaN(s) ? 1f : Clamp((float)Math.Round(s * 20f) / 20f, MinScale, MaxScale);

        public static float ClampOpacity(float o) => float.IsNaN(o) ? 1f : Clamp((float)Math.Round(o * 20f) / 20f, MinOpacity, MaxOpacity);

        // ---------------- Places as fractions of the screen ----------------

        /// <summary>
        /// A panel's place as fractions of the screen: the point of its corner nearest the screen's edges (the right edge
        /// when its middle is in the right half, the bottom when in the bottom half), so it stays against that edge at any
        /// resolution and grows away from it.
        /// </summary>
        public static void ToFraction(HudBox b, float sw, float sh, out float fx, out float fy, out bool right, out bool bottom)
        {
            right = b.x + b.w / 2f > sw / 2f;
            bottom = b.y + b.h / 2f > sh / 2f;
            fx = sw <= 0f ? 0f : Clamp((right ? b.XMax : b.x) / sw, 0f, 1f);
            fy = sh <= 0f ? 0f : Clamp((bottom ? b.YMax : b.y) / sh, 0f, 1f);
        }

        /// <summary>The panel's top-left corner from its saved fractions and its size now, kept on the screen.</summary>
        public static HudBox FromFraction(float fx, float fy, bool right, bool bottom, float w, float h, float sw, float sh)
        {
            var b = new HudBox(fx * sw - (right ? w : 0f), fy * sh - (bottom ? h : 0f), w, h);
            return OnScreen(b, sw, sh);
        }

        /// <summary>Kept wholly on the screen; a panel bigger than the screen keeps its top-left corner on it.</summary>
        public static HudBox OnScreen(HudBox b, float sw, float sh)
        {
            b.x = b.w >= sw ? 0f : Clamp(b.x, 0f, sw - b.w);
            b.y = b.h >= sh ? 0f : Clamp(b.y, 0f, sh - b.h);
            return b;
        }

        // ---------------- Snapping ----------------

        /// <summary>
        /// Snaps a panel being moved: each of its edges to the screen's edge within <paramref name="dist"/>, and to the
        /// edges of the other panels it sits beside (alongside, or touching end to end). The nearest snap wins on each
        /// axis; then it's kept on the screen.
        /// </summary>
        public static HudBox Snap(HudBox b, float sw, float sh, IList<HudBox> others, float dist = SnapDistance)
        {
            float bestX = dist + 0.001f, bestY = dist + 0.001f, dx = 0f, dy = 0f;
            void TryX(float edge, float target)
            {
                float d = target - edge;
                if (Math.Abs(d) < Math.Abs(bestX))
                {
                    bestX = d;
                    dx = d;
                }
            }
            void TryY(float edge, float target)
            {
                float d = target - edge;
                if (Math.Abs(d) < Math.Abs(bestY))
                {
                    bestY = d;
                    dy = d;
                }
            }
            TryX(b.x, 0f);
            TryX(b.XMax, sw);
            TryY(b.y, 0f);
            TryY(b.YMax, sh);
            if (others != null)
                foreach (var o in others)
                {
                    // Side by side (they share some height, give or take the snap distance): left and right edges meet or line up.
                    bool besideY = b.y < o.YMax + dist && b.YMax > o.y - dist;
                    if (besideY)
                    {
                        TryX(b.x, o.XMax);
                        TryX(b.XMax, o.x);
                        TryX(b.x, o.x);
                        TryX(b.XMax, o.XMax);
                    }
                    bool besideX = b.x < o.XMax + dist && b.XMax > o.x - dist;
                    if (besideX)
                    {
                        TryY(b.y, o.YMax);
                        TryY(b.YMax, o.y);
                        TryY(b.y, o.y);
                        TryY(b.YMax, o.YMax);
                    }
                }
            if (Math.Abs(bestX) <= dist) b.x += dx;
            if (Math.Abs(bestY) <= dist) b.y += dy;
            return OnScreen(b, sw, sh);
        }

        /// <summary>Whether the bar over a panel goes above it (true), or below it when there's no room above.</summary>
        public static bool HeaderAbove(HudBox body, float header) => body.y >= header;

        /// <summary>Whether two boxes overlap (touching edges don't).</summary>
        public static bool Overlap(HudBox a, HudBox b) => a.x < b.XMax && b.x < a.XMax && a.y < b.YMax && b.y < a.YMax;

        // ---------------- Where each panel starts ----------------

        /// <summary>The gap between the right-hand column (vitals, council, alerts) and the second column (work, health), in UI pixels.</summary>
        public const float SecondColumn = 262f;

        /// <summary>
        /// Where the mood board and the trend start from the left edge, in UI pixels: clear of the inventory panel in the
        /// resource list's place (and of the game's own list, when that shows), with the game's messages beside them.
        /// </summary>
        public const float LeftColumn = 160f;

        /// <summary>
        /// A panel's place as Rimfolk ships it, for a screen of <paramref name="sw"/> by <paramref name="sh"/> UI pixels,
        /// laid out so nothing overlaps at 1080p and nothing sits under the game's own interface. The far left: the
        /// inventory where the game's resource list is. Beside it: the mood board below the colonist bar's height, the
        /// trend above the inspect pane's tabs. The right edge: the vitals and the council, and the alerts in the game's
        /// own alert place above the letters (<paramref name="auto"/>: worked out every frame, see <see cref="AlertsBox"/>).
        /// A second column left of it, clear of the colonist bar: work and idle, then health.
        /// </summary>
        public static void DefaultPlace(string id, float sw, float sh, out float fx, out float fy, out bool right, out bool bottom, out bool auto)
        {
            fx = 0f;
            fy = 0f;
            right = false;
            bottom = false;
            auto = false;
            if (sw <= 0f) sw = 1920f;
            if (sh <= 0f) sh = 1080f;
            float column2 = Clamp((sw - SecondColumn) / sw, 0.5f, 1f);
            float left = Clamp(LeftColumn / sw, 0f, 0.3f);
            switch (id)
            {
                case HudIds.Inventory:
                    break;
                case HudIds.Moods:
                    fx = left;
                    fy = 0.27f;
                    break;
                case HudIds.Trend:
                    fx = left;
                    fy = 0.78f;
                    bottom = true;
                    break;
                case HudIds.Vitals:
                    fx = 1f;
                    fy = 0.2f;
                    right = true;
                    break;
                case HudIds.Council:
                    fx = 1f;
                    fy = 0.326f;
                    right = true;
                    break;
                case HudIds.Alerts:
                    fx = -1f;
                    fy = -1f;
                    right = true;
                    bottom = true;
                    auto = true;
                    break;
                case HudIds.Work:
                    fx = column2;
                    fy = 0.14f;
                    right = true;
                    break;
                case HudIds.Health:
                    fx = column2;
                    fy = 0.413f;
                    right = true;
                    break;
            }
        }

        /// <summary>
        /// The alerts panel in the game's alert place: against the right edge, its bottom just above the letters
        /// (<paramref name="lettersTop"/>, the top of the game's letter stack), or above the game's own alert list while
        /// that still shows (<paramref name="gameList"/> pixels tall). Without a sensible letter line, a quarter up from the bottom.
        /// </summary>
        public static HudBox AlertsBox(float w, float h, float sw, float sh, float lettersTop, float gameList)
        {
            float bottom = lettersTop > sh * 0.2f && lettersTop <= sh ? lettersTop - 4f : sh * 0.76f;
            if (gameList > 0f) bottom -= gameList + 6f;
            return OnScreen(new HudBox(sw - w, bottom - h, w, h), sw, sh);
        }

        // ---------------- The game's own interface over the panels ----------------

        /// <summary>The game's resource list down the left (its icons and counts, and a scroll bar), in UI pixels.</summary>
        public const float ResourceListWidth = 150f;

        /// <summary>
        /// The inspect pane's row of tab buttons, right to left from the pane's right edge above its top
        /// (<paramref name="paneTop"/>); with a tab open the game fills the rest of the row to the left edge.
        /// </summary>
        public static HudBox TabRow(float paneWidth, float paneTop, int tabs, bool anyOpen, float tabWidth, float tabHeight)
        {
            if (tabs <= 0 || paneWidth <= 0f) return new HudBox(0f, paneTop - tabHeight, 0f, 0f);
            float left = anyOpen ? 0f : Math.Max(0f, paneWidth - tabs * tabWidth);
            return new HudBox(left, paneTop - tabHeight, paneWidth - left, tabHeight);
        }

        /// <summary>The space kept clear above the screen's bottom edge: the game's button bar and a small gap.</summary>
        public const float BottomReserve = 39f;

        /// <summary>A panel never gets shorter than this when the screen is short: room for a few rows to scroll through.</summary>
        public const float MinScrollHeight = 120f;

        /// <summary>
        /// Fits a panel's body on the screen: at most down to the button bar, moved up if it would start too low; the
        /// height is the content's (<paramref name="contentH"/>) when it fits, else what's left, and then it scrolls.
        /// </summary>
        public static HudBox FitOnScreen(HudBox b, float contentH, float sh)
        {
            float bottom = sh - BottomReserve;
            float h = Math.Min(contentH, Math.Max(MinScrollHeight, bottom - Math.Max(0f, b.y)));
            h = Math.Min(h, Math.Max(20f, bottom));
            float y = Math.Max(0f, Math.Min(b.y, bottom - h));
            return new HudBox(b.x, y, b.w, h);
        }

        /// <summary>Where scrolling may go: from 0 down to the content's overflow.</summary>
        public static float ClampScroll(float scroll, float contentH, float viewH) => Math.Max(0f, Math.Min(scroll, Math.Max(0f, contentH - viewH)));

        /// <summary>A generous width for one inspect tab: the game's are 72 pixels, other mods' (RimHUD's) up to about 100.</summary>
        public const float AnyTabWidth = 110f;

        /// <summary>
        /// The whole row of inspect tabs over the pane (<paramref name="paneTop"/>), however they're laid out: from the
        /// left edge across the pane, or across as many tabs as there are at their widest, up to <paramref name="maxRight"/>.
        /// </summary>
        public static HudBox TabRowAll(float paneWidth, float paneTop, int tabs, float tabHeight, float maxRight)
        {
            if (tabs <= 0 || paneWidth <= 0f) return new HudBox(0f, paneTop - tabHeight, 0f, 0f);
            float w = Math.Min(Math.Max(paneWidth, tabs * AnyTabWidth), Math.Max(paneWidth, maxRight));
            return new HudBox(0f, paneTop - tabHeight - 2f, w, tabHeight + 2f);
        }

        /// <summary>The letters against the right edge, from the top of the last one, and the date, time and play settings under them.</summary>
        public static HudBox RightColumn(float sw, float sh, float lettersTop, float width)
        {
            float top = lettersTop > 0f && lettersTop < sh ? lettersTop : sh;
            return new HudBox(sw - width, top - 4f, width, sh - top + 4f);
        }

        /// <summary>Whether an alert's row shows the number of things it's about: two or more, and the label doesn't already say a number.</summary>
        public static bool ShowCount(string label, int count)
        {
            if (count < 2) return false;
            if (label == null) return true;
            foreach (char c in label)
                if (char.IsDigit(c))
                    return false;
            return true;
        }

        // ---------------- Rows when space is short ----------------

        /// <summary>
        /// Which rows show when only <paramref name="max"/> fit (0 for no limit): every row scoring at least
        /// <paramref name="always"/> (the critical ones), then the highest of the rest; the rows keep their order.
        /// <paramref name="hidden"/> is how many are left out, for "and 3 more".
        /// </summary>
        public static List<int> Keep(IList<float> scores, int max, float always, out int hidden)
        {
            var keep = new List<int>();
            int n = scores?.Count ?? 0;
            if (max <= 0 || n <= max)
            {
                for (int i = 0; i < n; i++) keep.Add(i);
                hidden = 0;
                return keep;
            }
            var chosen = new bool[n];
            int count = 0;
            for (int i = 0; i < n; i++)
                if (scores[i] >= always)
                {
                    chosen[i] = true;
                    count++;
                }
            // The rest by score, the earlier first on a tie.
            var rest = new List<int>();
            for (int i = 0; i < n; i++)
                if (!chosen[i])
                    rest.Add(i);
            rest.Sort((a, b) =>
            {
                int c = scores[b].CompareTo(scores[a]);
                return c != 0 ? c : a.CompareTo(b);
            });
            foreach (int i in rest)
            {
                if (count >= max) break;
                chosen[i] = true;
                count++;
            }
            for (int i = 0; i < n; i++)
                if (chosen[i])
                    keep.Add(i);
            hidden = n - keep.Count;
            return keep;
        }

        /// <summary>Columns of the compact mood board's grid for this many people, at most <paramref name="maxColumns"/>.</summary>
        public static int GridColumns(int count, int maxColumns)
        {
            if (count <= 0) return 1;
            if (maxColumns < 1) maxColumns = 1;
            // As square as fits, but never more columns than needed.
            int cols = (int)Math.Ceiling(Math.Sqrt(count * 2.0));
            return Math.Max(1, Math.Min(Math.Min(cols, maxColumns), count));
        }

        // ---------------- The trend graph's samples ----------------

        /// <summary>One hourly sample in one number: the hour (since the game began), and the average and lowest mood in percent.</summary>
        public static int Pack(int hour, float average, float lowest)
        {
            int a = Percent(average), l = Percent(lowest);
            return Math.Max(0, hour) * 10000 + a * 100 + l;
        }

        public static void Unpack(int packed, out int hour, out float average, out float lowest)
        {
            if (packed < 0) packed = 0;
            hour = packed / 10000;
            average = packed / 100 % 100 / 100f;
            lowest = packed % 100 / 100f;
        }

        /// <summary>0..99: a full mood is kept as 99%.</summary>
        private static int Percent(float v) => (int)Clamp((float)Math.Round(v * 100f), 0f, 99f);

        /// <summary>The sample nearest the mouse, by hour, among samples whose hours are in <paramref name="hours"/> (oldest first); -1 for none.</summary>
        public static int Nearest(IList<int> hours, float hourAtMouse)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < (hours?.Count ?? 0); i++)
            {
                float d = Math.Abs(hours[i] - hourAtMouse);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Which of three labels on one line fit without touching: one at the left, one centred, one at the right, each
        /// <paramref name="gap"/> apart. The right one (where the line ends: "now") goes first, then the left, then the
        /// middle only if it fits between them. A width of 0 means there's no such label.
        /// </summary>
        public static void FitThree(float width, float left, float middle, float right, float gap, out bool showLeft, out bool showMiddle, out bool showRight)
        {
            showRight = right > 0f && right <= width;
            float rightTaken = showRight ? right + gap : 0f;
            showLeft = left > 0f && left + rightTaken <= width;
            float leftTaken = showLeft ? left + gap : 0f;
            float from = (width - middle) / 2f, to = (width + middle) / 2f;
            showMiddle = middle > 0f && from >= leftTaken && to <= width - rightTaken;
        }

        /// <summary>The y of a value (0..1) in a graph from <paramref name="top"/> down <paramref name="height"/> pixels.</summary>
        public static float GraphY(float value, float top, float height) => top + height * (1f - Clamp(value, 0f, 1f));
    }
}
