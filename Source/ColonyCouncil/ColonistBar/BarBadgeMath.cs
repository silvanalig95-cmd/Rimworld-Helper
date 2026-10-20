using System;

namespace ColonyCouncil
{
    /// <summary>What a badge on the colonist bar is about. The order is also the tie-break between equal priorities.</summary>
    public enum BarBadgeKind
    {
        /// <summary>Close to a mental break (a dot, when the break mark is set to a dot rather than a ring).</summary>
        Break,
        /// <summary>Bitter about the council and leaving the colony in a few days (CouncilDynamics.CheckQuitting).</summary>
        Leaving,
        /// <summary>An open wish that has waited past the set share of its patience.</summary>
        Wish,
        /// <summary>Worn down or near breaking (Heart/).</summary>
        Strain,
        /// <summary>A wavering faith (Faith/, with Ideology).</summary>
        Faith,
        /// <summary>Something the council granted is gone, or was taken back and can still be made right (Wishes/Kept.cs).</summary>
        Promise
    }

    /// <summary>How the colonist bar shows that someone is close to a mental break.</summary>
    public enum BarBreakMark
    {
        /// <summary>A soft ring around the portrait, its colour and strength following how close they are.</summary>
        Ring,
        /// <summary>A dot among the badges, coloured the same way.</summary>
        Dot,
        Off
    }

    /// <summary>The badges' size at the bar's full size (they shrink with the bar).</summary>
    public enum BarBadgeSize
    {
        Small,
        Normal,
        Large
    }

    /// <summary>Which corner of the portrait the badges start from.</summary>
    public enum BarBadgeCorner
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    /// <summary>How close to a mental break: inside the warning margin above the minor line, or under one of the game's three lines.</summary>
    public enum BarBreakLevel
    {
        None,
        Near,
        Minor,
        Major,
        Extreme
    }

    /// <summary>One badge that applies to a colonist: its kind and the kind's own step (see <see cref="BarBadgeMath.Priority"/>).</summary>
    public struct BarBadge
    {
        public BarBadgeKind kind;
        public int level;

        public BarBadge(BarBadgeKind kind, int level)
        {
            this.kind = kind;
            this.level = level;
        }

        public int Priority => BarBadgeMath.Priority(kind, level);

        public override string ToString() => kind + ":" + level;
    }

    /// <summary>
    /// The colonist bar's badges without game types, so the tests can check them: how close to a break someone is and the
    /// ring's colour for it, each kind's step, which badges win when more apply than are shown, and where each one sits on
    /// the portrait. ColonistBar/BarBadges.cs gathers the facts and draws.
    /// </summary>
    public static class BarBadgeMath
    {
        /// <summary>The game's portrait on the colonist bar at full size (ColonistBar.BaseSize).</summary>
        public const float BaseBarSize = 48f;

        /// <summary>
        /// The row the game draws its own icons in, along the bottom of the portrait from the left (sleeping, idle, a
        /// mental state, inspired, burning, an ideoligion role, a slave's mark): up to 20 px and a pixel, at full size.
        /// </summary>
        public const float GameIconRow = 21f;

        /// <summary>The smallest a badge gets on a crowded bar, in pixels.</summary>
        public const float MinSize = 8f;

        /// <summary>From the portrait's edge, in pixels.</summary>
        public const float Pad = 1f;

        /// <summary>The most badges a portrait shows.</summary>
        public const int MaxShown = 3;

        /// <summary>Under this share of the wavering line, wavering faith counts one step higher.</summary>
        public const float FaithLowShare = 0.5f;

        /// <summary>From this share of its patience a wish counts as on its last call, whatever the setting.</summary>
        public const float WishLastCall = 0.85f;

        // ---------------- Close to a break ----------------

        /// <summary>
        /// How close a mood is to a break: under the extreme, major or minor line (the game's own lines for this colonist),
        /// within <paramref name="margin"/> above the minor line, or none. Strictly under, as the game counts it.
        /// </summary>
        public static BarBreakLevel Level(float mood, float minor, float major, float extreme, float margin)
        {
            if (mood < extreme) return BarBreakLevel.Extreme;
            if (mood < major) return BarBreakLevel.Major;
            if (mood < minor) return BarBreakLevel.Minor;
            if (margin > 0f && mood < minor + margin) return BarBreakLevel.Near;
            return BarBreakLevel.None;
        }

        /// <summary>
        /// How close to a break, from 0 (at the warning line, or above it) to 1 (under the extreme line): a third for each
        /// span, the warning margin, minor to major and major to extreme, rising smoothly within each.
        /// </summary>
        public static float Closeness(float mood, float minor, float major, float extreme, float margin)
        {
            switch (Level(mood, minor, major, extreme, margin))
            {
                case BarBreakLevel.Near: return Within(minor + margin - mood, margin) / 3f;
                case BarBreakLevel.Minor: return (1f + Within(minor - mood, minor - major)) / 3f;
                case BarBreakLevel.Major: return (2f + Within(major - mood, major - extreme)) / 3f;
                case BarBreakLevel.Extreme: return 1f;
                default: return 0f;
            }
        }

        private static float Within(float depth, float span) => span <= 0.0001f ? 1f : Clamp01(depth / span);

        /// <summary>
        /// The ring's (and the dot's) colour for a closeness: pale amber at the warning line, orange at the minor line, red at
        /// the major line and deep red under the extreme line, and stronger (less transparent) the closer they are.
        /// </summary>
        public static void RingColor(float closeness, out float r, out float g, out float b, out float a)
        {
            float c = Clamp01(closeness);
            // The stops, a third apart.
            float[] sr = Stops.R, sg = Stops.G, sb = Stops.B;
            float pos = c * 3f;
            int i = Math.Min(2, (int)pos);
            float t = pos - i;
            r = sr[i] + (sr[i + 1] - sr[i]) * t;
            g = sg[i] + (sg[i + 1] - sg[i]) * t;
            b = sb[i] + (sb[i + 1] - sb[i]) * t;
            a = RingAlpha(c);
        }

        /// <summary>How strong the ring is: faint at the warning line (0.3), nearly solid under the extreme line (0.95).</summary>
        public static float RingAlpha(float closeness) => closeness <= 0f ? 0f : 0.3f + 0.65f * Clamp01(closeness);

        private static class Stops
        {
            // Amber, orange, red, deep red.
            public static readonly float[] R = { 1f, 1f, 0.95f, 0.85f };
            public static readonly float[] G = { 0.85f, 0.58f, 0.28f, 0.12f };
            public static readonly float[] B = { 0.4f, 0.18f, 0.22f, 0.12f };
        }

        // ---------------- Each kind's step ----------------

        /// <summary>A wish's step: -1 not yet (under the threshold), 0 waiting long, 1 on its last call (reminded twice, or 85% of its time gone).</summary>
        public static int WishLevel(float elapsed, float threshold, int stage)
        {
            if (stage >= 2 || elapsed >= WishLastCall) return 1;
            return elapsed >= threshold ? 0 : -1;
        }

        /// <summary>Strain's step from its band (0 steady, 1 strained, 2 worn down, 3 near breaking): -1 none, 0 worn down, 1 near breaking.</summary>
        public static int StrainLevel(int band) => band >= 3 ? 1 : band == 2 ? 0 : -1;

        /// <summary>Faith's step: -1 not wavering, 0 wavering, 1 barely holding on (under half the wavering line).</summary>
        public static int FaithLevel(float certainty, float waveringBelow)
        {
            if (certainty >= waveringBelow) return -1;
            return certainty < waveringBelow * FaithLowShare ? 1 : 0;
        }

        // ---------------- What wins ----------------

        /// <summary>
        /// How pressing a badge is; the higher, the sooner it's shown. A break that could come any moment (under the
        /// major line) and someone about to leave come first; then a minor break risk, a wish on its last call, strain near
        /// breaking, a wish that has waited long, a promise taken back or about to be; then the early warnings: just
        /// above the minor line, worn down, and wavering faith last.
        /// </summary>
        public static int Priority(BarBadgeKind kind, int level)
        {
            switch (kind)
            {
                case BarBadgeKind.Break:
                    switch ((BarBreakLevel)level)
                    {
                        case BarBreakLevel.Extreme: return 100;
                        case BarBreakLevel.Major: return 95;
                        case BarBreakLevel.Minor: return 80;
                        case BarBreakLevel.Near: return 40;
                        default: return 0;
                    }
                case BarBadgeKind.Leaving: return 90;
                case BarBadgeKind.Wish: return level >= 1 ? 70 : 60;
                case BarBadgeKind.Strain: return level >= 1 ? 65 : 35;
                case BarBadgeKind.Promise: return level >= 1 ? 55 : 50;
                case BarBadgeKind.Faith: return level >= 1 ? 25 : 20;
                default: return 0;
            }
        }

        /// <summary>
        /// Sorts the first <paramref name="count"/> badges, most pressing first (equal ones in the kinds' order), in place
        /// and without allocating, and returns how many are shown: at most <paramref name="max"/> (1 to 3).
        /// </summary>
        public static int Order(BarBadge[] badges, int count, int max)
        {
            if (badges == null || count <= 0) return 0;
            count = Math.Min(count, badges.Length);
            for (int i = 1; i < count; i++)
            {
                var b = badges[i];
                int j = i - 1;
                while (j >= 0 && Before(b, badges[j]))
                {
                    badges[j + 1] = badges[j];
                    j--;
                }
                badges[j + 1] = b;
            }
            return Math.Min(count, Clamp(max, 1, MaxShown));
        }

        private static bool Before(BarBadge a, BarBadge b)
        {
            int pa = a.Priority, pb = b.Priority;
            return pa != pb ? pa > pb : a.kind < b.kind;
        }

        // ---------------- Where they sit ----------------

        /// <summary>A badge's size in pixels, for the bar's scale (1 at full size), never under <see cref="MinSize"/>.</summary>
        public static float SizeFor(BarBadgeSize size, float scale)
        {
            float px = size == BarBadgeSize.Small ? 11f : size == BarBadgeSize.Large ? 16f : 13f;
            return Math.Max(MinSize, px * scale);
        }

        /// <summary>How many badges fit along the portrait's side without reaching the game's own icons along the bottom (at least one).</summary>
        public static int ColumnFits(float height, float size, float iconRow)
        {
            if (size <= 0f) return 1;
            return Math.Max(1, (int)((height - iconRow - Pad + 0.01f) / size));
        }

        /// <summary>How many badges fit on the portrait at all: down the side, then as many such columns as fit across.</summary>
        public static int Capacity(float width, float height, float size, float iconRow)
        {
            if (size <= 0f) return 1;
            int across = Math.Max(1, (int)((width - Pad + 0.01f) / size));
            return ColumnFits(height, size, iconRow) * across;
        }

        /// <summary>
        /// Where badge <paramref name="index"/> goes on a portrait at (x, y), w by h: from the chosen corner along the side
        /// as far as fits (top corners down from the top, bottom corners up from just above the game's own icons), then the
        /// next column inward. Only the first <see cref="Capacity"/> fit on the portrait.
        /// </summary>
        public static void Slot(int index, BarBadgeCorner corner, float x, float y, float w, float h, float size, float iconRow, out float sx, out float sy)
        {
            bool right = corner == BarBadgeCorner.TopRight || corner == BarBadgeCorner.BottomRight;
            bool top = corner == BarBadgeCorner.TopRight || corner == BarBadgeCorner.TopLeft;
            int column = ColumnFits(h, size, iconRow);
            int down = index % column;
            int across = index / column;
            sx = right ? x + w - Pad - size - across * size : x + Pad + across * size;
            float first = top ? y + Pad : y + h - iconRow - size;
            sy = top ? first + down * size : first - down * size;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
