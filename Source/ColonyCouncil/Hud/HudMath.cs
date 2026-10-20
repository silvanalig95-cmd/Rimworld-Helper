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

        /// <summary>The y of a value (0..1) in a graph from <paramref name="top"/> down <paramref name="height"/> pixels.</summary>
        public static float GraphY(float value, float top, float height) => top + height * (1f - Clamp(value, 0f, 1f));
    }
}
