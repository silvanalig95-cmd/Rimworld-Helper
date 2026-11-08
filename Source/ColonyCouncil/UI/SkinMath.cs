using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own look in numbers (docs/design/rimfolk-look.md; the setting "Rimfolk's own look"): the palette the
    /// player saw in the diplomacy screen's mockup as plain sRGB values, the contrast of text on its grounds, the coverage
    /// of a rounded corner (how the kit's textures are drawn once at load), and how a 9-slice texture is cut and laid
    /// into a rect of any size. Free of game types, so Tools/Tests can check every text colour against every ground and
    /// every slice against every size. UiKit's tokens are made from these values (UI/UiKit.cs).
    /// </summary>
    public static class SkinMath
    {
        // ---------------- The palette (0xRRGGBB) ----------------

        /// <summary>The window's ground.</summary>
        public const uint WindowGround = 0x17191b;
        /// <summary>A panel inside the window: the body under the tabs, a tab that is lit.</summary>
        public const uint Panel = 0x212427;
        /// <summary>A card, a button, a field: raised on the panel.</summary>
        public const uint Raised = 0x2a2e32;
        /// <summary>A button or a row under the mouse.</summary>
        public const uint RaisedHover = 0x33383d;
        /// <summary>A button held down.</summary>
        public const uint Pressed = 0x1b1d20;
        /// <summary>A faint edge around a card, a field, the window.</summary>
        public const uint Edge = 0x3a3f44;
        /// <summary>An edge under the mouse.</summary>
        public const uint EdgeStrong = 0x4f565c;

        /// <summary>Text.</summary>
        public const uint Ink = 0xece8df;
        /// <summary>Secondary text.</summary>
        public const uint Faint = 0xb0aa9c;
        /// <summary>Quiet text: what is out of reach, past or empty.</summary>
        public const uint Dim = 0x9c968a;
        /// <summary>A section's heading.</summary>
        public const uint Heading = 0xe8d5a6;
        /// <summary>The one accent: ours, chosen, the primary act.</summary>
        public const uint Accent = 0xe0a845;
        public const uint AccentHover = 0xeab65a;
        public const uint AccentPressed = 0xc8923a;
        /// <summary>Text on the accent (a primary button's word).</summary>
        public const uint AccentInk = 0x17191b;
        /// <summary>A name that leads to its card.</summary>
        public const uint Link = 0xeed8a4;
        public const uint LinkHot = 0xfff1cf;
        /// <summary>Allied, friendly, for, rising: the mockup's blue.</summary>
        public const uint Ally = 0x6aa6dc;
        /// <summary>Neutral or wary; what is under way.</summary>
        public const uint Cool = 0xa9b1ba;
        /// <summary>Hostile, bad, against, falling: the mockup's orange.</summary>
        public const uint Foe = 0xe08a45;
        /// <summary>Needs the player soon.</summary>
        public const uint Soon = 0xe6c35c;
        /// <summary>Harm now (always with the warning icon).</summary>
        public const uint Danger = 0xf27466;
        /// <summary>Finished, kept, answered.</summary>
        public const uint Done = 0x9db89f;
        /// <summary>World events and news from afar.</summary>
        public const uint World = 0xb4a7e2;

        /// <summary>The colours text is drawn in, by name, so a test can try each on each ground.</summary>
        public static readonly (string name, uint rgb)[] TextColours =
        {
            ("Ink", Ink), ("Faint", Faint), ("Dim", Dim), ("Heading", Heading), ("Gold", Accent), ("Link", Link), ("LinkHot", LinkHot), ("Warm", Ally),
            ("Cool", Cool), ("Sour", Foe), ("Soon", Soon), ("Danger", Danger), ("Done", Done), ("World", World)
        };

        /// <summary>The grounds text sits on: the window, a panel, a card.</summary>
        public static readonly (string name, uint rgb)[] Grounds = { ("the window", WindowGround), ("a panel", Panel), ("a card", Raised) };

        /// <summary>WCAG's least contrast for text.</summary>
        public const float TextContrast = 4.5f;

        public static float R(uint rgb) => ((rgb >> 16) & 0xff) / 255f;
        public static float G(uint rgb) => ((rgb >> 8) & 0xff) / 255f;
        public static float B(uint rgb) => (rgb & 0xff) / 255f;

        private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>A colour's relative luminance (WCAG 2).</summary>
        public static double Luminance(uint rgb) => 0.2126 * Linear(R(rgb)) + 0.7152 * Linear(G(rgb)) + 0.0722 * Linear(B(rgb));

        /// <summary>The contrast of two colours, 1 to 21 (WCAG 2).</summary>
        public static double Contrast(uint a, uint b)
        {
            double la = Luminance(a), lb = Luminance(b);
            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        // ---------------- Radii and the textures made at load ----------------

        /// <summary>The corner radii the kit draws, in the interface's units: chips and fields, buttons and rows, cards, the window.</summary>
        public const int RadiusSmall = 4, RadiusMedium = 6, RadiusCard = 8, RadiusWindow = 10;

        /// <summary>The radii a texture is made for, smallest first.</summary>
        public static readonly int[] Radii = { RadiusSmall, RadiusMedium, RadiusCard, RadiusWindow };

        /// <summary>Texels per interface unit in the kit's textures (drawn smaller, so they stay smooth at an interface scale of 2).</summary>
        public const int Supersample = 2;

        /// <summary>The soft shadow under a card or the window: how far it reaches, in the interface's units.</summary>
        public const int ShadowReach = 10;

        /// <summary>
        /// A texture's side for a radius: the corner on each side and a little flat run in the middle that stretches.
        /// </summary>
        public static int TextureSide(int radius) => (radius * 2 + 4) * Supersample;

        /// <summary>
        /// How much of the texel at (<paramref name="px"/>, <paramref name="py"/>) a rounded square of side
        /// <paramref name="side"/> covers, with corners of radius <paramref name="radius"/> (all in texels, rows counted from
        /// the top of the picture), smoothed over one texel; with <paramref name="inset"/>, the same shape drawn that far
        /// inside. <paramref name="topOnly"/> rounds the top corners only (a tab).
        /// </summary>
        public static float Coverage(int px, int py, int side, float radius, float inset = 0f, bool topOnly = false)
        {
            float x = px + 0.5f, y = py + 0.5f;
            float centre = side / 2f, half = side / 2f - inset;
            if (half <= 0f) return 0f;
            float r = Math.Max(0f, Math.Min(radius - inset, half));
            // A tab's lower half is open at the foot (it joins the panel under it): only its sides bound it.
            if (topOnly && y > centre) return Clamp01(0.5f - (Math.Abs(x - centre) - half));
            return Clamp01(0.5f - Distance(x - centre, y - centre, half, r));
        }

        /// <summary>A point's distance outside a rounded square centred on the origin (negative inside): its half side and corner radius.</summary>
        private static float Distance(float dx, float dy, float half, float r)
        {
            float qx = Math.Abs(dx) - (half - r), qy = Math.Abs(dy) - (half - r);
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        /// <summary>The coverage of a ring <paramref name="width"/> wide along the shape's edge (a card's or a field's border).</summary>
        public static float Ring(int px, int py, int side, float radius, float width, bool topOnly = false) =>
            Clamp01(Coverage(px, py, side, radius, 0f, topOnly) - Coverage(px, py, side, radius, width, topOnly));

        /// <summary>
        /// A soft shadow's strength at a texel of a square <paramref name="side"/> texels wide: full inside the shape that is
        /// <paramref name="reach"/> in from each side with corners of <paramref name="radius"/>, falling smoothly to nothing
        /// at the texture's edge.
        /// </summary>
        public static float Shadow(int px, int py, int side, float reach, float radius)
        {
            float centre = side / 2f, half = side / 2f - reach;
            if (half <= 0f || reach <= 0f) return 0f;
            float d = Distance(px + 0.5f - centre, py + 0.5f - centre, half, Math.Min(radius, half));
            if (d <= 0f) return 1f;
            float t = Clamp01(1f - d / reach);
            // Smooth, a little heavier near the shape.
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ---------------- 9-slice ----------------

        /// <summary>
        /// The margin a 9-slice draws at in a rect <paramref name="w"/> by <paramref name="h"/>: the corner's
        /// <paramref name="margin"/>, or half the rect's shorter side where it is smaller (a chip as round as it is tall).
        /// </summary>
        public static float SliceMargin(float w, float h, float margin) => Math.Max(0f, Math.Min(margin, Math.Min(w, h) / 2f));

        /// <summary>
        /// The nine pieces of a 9-slice: for piece <paramref name="i"/> (0 to 8, left to right and top to bottom) its rect
        /// in the target (x, y, w, h) and its rect in the texture (u, v, uw, vh, in 0 to 1, v from the texture's bottom as
        /// Unity reads it). <paramref name="margin"/> is the corner in the target's units, <paramref name="texMargin"/> the
        /// corner's share of the texture (0 to 0.5). False for a piece with no room (a rect too small for it).
        /// </summary>
        public static bool Slice(int i, float x, float y, float w, float h, float margin, float texMargin, out float rx, out float ry, out float rw, out float rh,
            out float u, out float v, out float uw, out float vh)
        {
            float m = SliceMargin(w, h, margin);
            float tm = Math.Max(0f, Math.Min(0.5f, texMargin));
            int col = i % 3, row = i / 3;
            rx = col == 0 ? x : col == 1 ? x + m : x + w - m;
            rw = col == 1 ? w - 2f * m : m;
            ry = row == 0 ? y : row == 1 ? y + m : y + h - m;
            rh = row == 1 ? h - 2f * m : m;
            u = col == 0 ? 0f : col == 1 ? tm : 1f - tm;
            uw = col == 1 ? 1f - 2f * tm : tm;
            // Unity counts a texture's rows from the bottom: the target's top row is the texture's top.
            v = row == 0 ? 1f - tm : row == 1 ? tm : 0f;
            vh = row == 1 ? 1f - 2f * tm : tm;
            return rw > 0.01f && rh > 0.01f;
        }

        // ---------------- Tabs and segments ----------------

        /// <summary>A tab's width for a label: its word, its icon and the room around them, within reason.</summary>
        public static float TabWidth(float labelW, bool icon) => Math.Max(64f, Math.Min(240f, labelW + 28f + (icon ? 24f : 0f)));

        /// <summary>
        /// Tabs in a row <paramref name="room"/> wide: each as wide as it would like where they all fit; else shrunk alike
        /// to share the room; never under <paramref name="least"/> (the label then shows as its icon). Fills
        /// <paramref name="x"/> and <paramref name="w"/> from 0 and returns the room they take.
        /// </summary>
        public static float Tabs(float[] natural, int n, float room, float gap, float least, float[] x, float[] w)
        {
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += natural[i];
            float gaps = Math.Max(0, n - 1) * gap;
            float scale = sum + gaps > room && sum > 0f ? Math.Max(0f, room - gaps) / sum : 1f;
            float at = 0f;
            for (int i = 0; i < n; i++)
            {
                w[i] = Math.Max(least, natural[i] * scale);
                x[i] = at;
                at += w[i] + gap;
            }
            return n == 0 ? 0f : at - gap;
        }
    }
}
