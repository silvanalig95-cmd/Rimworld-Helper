using System;

namespace ColonyCouncil
{
    /// <summary>How much of a person a portrait shows, always framed by their own head.</summary>
    public enum FaceFrame
    {
        /// <summary>The face with a little of the shoulders: the round portraits in lists, letters and cards.</summary>
        Face,
        /// <summary>Head and shoulders down to the chest: the large portrait at the inspect pane.</summary>
        Bust,
        /// <summary>The whole figure, framed by the head so a child fills the tile as a grown-up does: the colonist card's header.</summary>
        Figure
    }

    /// <summary>
    /// The portrait camera for one picture: its offset from the pawn's middle in world units (x to the right, z up the
    /// screen) and its zoom as the game's portrait cache takes them, and where the head lands in the picture.
    /// </summary>
    public struct FaceCamera
    {
        public float x, z, zoom;
        /// <summary>The middle of the head in the picture, as shares of its width and height from its top left.</summary>
        public float headU, headV;
        /// <summary>The head's size (its mesh, hair room included) as a share of the picture's height.</summary>
        public float headShare;
    }

    /// <summary>
    /// The face engine's rules without game types (Tools/Tests try them): framing a picture by the pawn's own head, the
    /// backdrop in their colour, the large portrait's size and where it goes at the inspect pane.
    /// <para>
    /// The game's portrait camera is orthographic: at zoom z it sees 2 / z world units from the picture's top to its
    /// foot. A grown-up's head is drawn on a mesh 1.5 units square whose middle sits about 0.34 above the pawn's middle;
    /// a child's head is smaller and lower, a big race's larger and higher. So each framing is given in head sizes (how
    /// many across the picture's narrower side, how far the picture's top is above the head's middle), and the same
    /// numbers put a child, a grown-up, a big xenotype and another mod's race in the picture alike.
    /// </para>
    /// </summary>
    public static class FaceMath
    {
        /// <summary>The game's humanlike head mesh, in world units (MeshPool's head set).</summary>
        public const float HeadMesh = 1.5f;

        /// <summary>A grown-up's head above the pawn's middle in the game's front view (the average body type's head offset).</summary>
        public const float GrownHeadZ = 0.34f;

        /// <summary>The game's portrait cache renders this much larger than it is shown (its supersampling).</summary>
        public const float Supersample = 1.25f;

        // Each framing: the picture's narrower side in head sizes, and the picture's top above the head's middle in head sizes.
        // The face: as the round portraits were before the engine for a grown-up (zoom about 1.8, the camera 0.28 above
        // the middle), the hair's top left to the circle. The bust (the player, 21 November 2026: "a lot of the square is
        // still, so to say, empty space ... The pawn could be centered further, and be a bit bigger"): closer than in
        // 0.289.0 (0.8 head sizes across, not 1.05) and with less room above the hair (0.32, not 0.44), so the head and hair
        // take about half the square's height, the face two fifths down, the shoulders and the chest under it. The figure:
        // the whole of a grown-up from the hair to the feet in a tile 100 by 140.
        public const float FaceSpan = 0.74f, FaceTop = 0.33f;
        public const float BustSpan = 0.8f, BustTop = 0.32f;
        public const float FigureSpan = 0.8f, FigureTop = 0.42f;

        /// <summary>An animal or a mechanoid has no head of its own to frame by: its whole drawing, this share of it across the narrower side.</summary>
        public const float BodySpan = 0.85f;

        public static float Span(FaceFrame f) => f == FaceFrame.Bust ? BustSpan : f == FaceFrame.Figure ? FigureSpan : FaceSpan;
        public static float Top(FaceFrame f) => f == FaceFrame.Bust ? BustTop : f == FaceFrame.Figure ? FigureTop : FaceTop;

        /// <summary>
        /// The camera for a picture <paramref name="w"/> by <paramref name="h"/> framed by a head whose middle is at
        /// (<paramref name="headX"/>, <paramref name="headZ"/>) from the pawn's middle and whose mesh is
        /// <paramref name="headSize"/> across.
        /// </summary>
        public static FaceCamera Frame(FaceFrame frame, float headX, float headZ, float headSize, float w, float h)
        {
            if (!Plausible(headX, headZ, headSize))
            {
                headX = 0f;
                headZ = GrownHeadZ;
                headSize = HeadMesh;
            }
            w = Math.Max(1f, w);
            h = Math.Max(1f, h);
            // World units per pixel: the narrower side holds the framing's span of heads.
            float perPx = Span(frame) * headSize / Math.Min(w, h);
            float viewH = perPx * h;
            float top = headZ + Top(frame) * headSize;
            var c = new FaceCamera
            {
                x = Round(headX),
                z = Round(top - viewH / 2f),
                zoom = Round(2f / viewH),
                headU = 0.5f,
                headV = (top - headZ) / viewH,
                headShare = headSize / viewH
            };
            return c;
        }

        /// <summary>The camera for a pawn with no head to frame by: its whole drawing (<paramref name="drawW"/> by <paramref name="drawH"/>) in the middle.</summary>
        public static FaceCamera FrameBody(float drawW, float drawH, float w, float h)
        {
            float size = Math.Max(drawW, drawH);
            if (!(size > 0.1f && size < 20f)) size = HeadMesh;
            w = Math.Max(1f, w);
            h = Math.Max(1f, h);
            float perPx = BodySpan * size / Math.Min(w, h);
            float viewH = perPx * h;
            return new FaceCamera { x = 0f, z = 0f, zoom = Round(2f / viewH), headU = 0.5f, headV = 0.5f, headShare = size / viewH };
        }

        /// <summary>
        /// Whether a head read from the game's drawing is one to frame by: a size between a fifth of a unit and eight, no
        /// further than three units to the side, between two below and six above the pawn's middle. Anything else (a
        /// drawing the engine can't read) is framed as a grown-up's head.
        /// </summary>
        public static bool Plausible(float headX, float headZ, float headSize) =>
            Finite(headX) && Finite(headZ) && Finite(headSize) && headSize >= 0.2f && headSize <= 8f && Math.Abs(headX) <= 3f && headZ >= -2f && headZ <= 6f;

        /// <summary>The head's size when the drawing can't be read: the game's head mesh times the life stage's head size (a child's is smaller).</summary>
        public static float FallbackHeadSize(float headSizeFactor) => HeadMesh * (headSizeFactor > 0.05f && headSizeFactor < 5f ? headSizeFactor : 1f);

        /// <summary>The pixels the game's portrait cache renders for a picture this many interface units across.</summary>
        public static int RenderPixels(float side, float uiScale) => (int)(Math.Max(1f, side) * Supersample * Math.Max(0.5f, uiScale));

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static float Round(float v) => (float)Math.Round(v, 4);

        // ---------------- Their colour behind them ----------------

        /// <summary>The darkest the backdrop's light behind the head may be kept to, and the backdrop itself, as luminance.</summary>
        public const double BackdropMost = 0.06, GlowMost = 0.2;

        /// <summary>One colour mixed into another: <paramref name="t"/> of <paramref name="b"/> into <paramref name="a"/>.</summary>
        public static uint Mix(uint a, uint b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            uint Ch(int shift)
            {
                float ca = (a >> shift) & 0xff, cb = (b >> shift) & 0xff;
                return (uint)Math.Round(ca + (cb - ca) * t) & 0xff;
            }
            return (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
        }

        /// <summary>
        /// The backdrop in a person's colour on a dark ground: as much of their colour as keeps it no brighter than
        /// <see cref="BackdropMost"/> (a pale yellow gets less than a deep blue), so the face reads on it as on the
        /// game's dark panels and two people's colours still tell apart.
        /// </summary>
        public static uint Backdrop(uint colour, uint ground) => Mix(ground, colour, BackdropShare(colour, ground));

        /// <summary>The soft light behind the head: more of their colour, no brighter than <see cref="GlowMost"/>.</summary>
        public static uint Glow(uint colour, uint ground) => Mix(ground, colour, GlowShare(colour, ground));

        /// <summary>How much of their colour goes into the backdrop (0 to 0.45), the game's look mixing it with the ground the same way.</summary>
        public static float BackdropShare(uint colour, uint ground) => Share(ground, colour, 0.45f, BackdropMost);

        /// <summary>How much of their colour goes into the light behind the head (0 to 0.75).</summary>
        public static float GlowShare(uint colour, uint ground) => Share(ground, colour, 0.75f, GlowMost);

        private static float Share(uint ground, uint colour, float most, double cap)
        {
            for (float t = most; t > 0.02f; t -= 0.03f)
                if (SkinMath.Luminance(Mix(ground, colour, t)) <= cap) return t;
            return 0f;
        }

        // ---------------- The large portrait at the inspect pane ----------------

        /// <summary>Its size: the least, the most, the first, and the step it keeps to (dragging and the slider).</summary>
        public const float SizeLeast = 96f, SizeMost = 360f, SizeFirst = 192f, SizeStep = 8f;

        /// <summary>The row of the inspect pane's tab buttons above it (the game's InspectPaneUtility.TabHeight).</summary>
        public const float TabRowH = 30f;

        /// <summary>The gap to the gizmos, and the room kept clear at the top of the screen (the colonist bar).</summary>
        public const float Gap = 6f, TopClear = 96f;

        /// <summary>The frame around the picture, the corner that sizes it, and the band with the name from this size up.</summary>
        public const float Border = 3f, Handle = 18f, NamedFrom = 128f;

        /// <summary>The name band's ground over the picture: dark enough that the name and the earned name read over any clothes.</summary>
        public const float BandAlpha = 0.78f;

        /// <summary>A size kept to the steps, between the least and the most.</summary>
        public static float Snap(float size)
        {
            if (!Finite(size)) size = SizeFirst;
            float s = (float)Math.Round(size / SizeStep) * SizeStep;
            return Math.Max(SizeLeast, Math.Min(SizeMost, s));
        }

        /// <summary>The size that fits the room there is (whole steps), or 0 when not even the least fits.</summary>
        public static float Fit(float size, float room)
        {
            float s = Snap(size);
            if (!Finite(room) || room < SizeLeast) return 0f;
            float most = (float)Math.Floor(room / SizeStep) * SizeStep;
            return Math.Max(SizeLeast, Math.Min(s, most));
        }

        /// <summary>
        /// The size while its free corner (the top right; the foot and left side stay put) is dragged: the start size grown
        /// by the farther of the mouse's way right and up, kept to the steps.
        /// </summary>
        public static float Dragged(float start, float dx, float dy) => Snap(start + Math.Max(dx, -dy));

        /// <summary>What the large portrait stands on: the screen, the inspect pane, its tab buttons, whether a tab is open, and the gizmos.</summary>
        public struct PaneScene
        {
            public float screenW, screenH;
            /// <summary>The inspect pane's window as it is on the screen now.</summary>
            public FrameRect pane;
            /// <summary>The pane's top: the higher of its window's top and the top the game gives its tabs (another mod may make it taller).</summary>
            public float paneTop;
            /// <summary>The pane has tab buttons on a row above its top.</summary>
            public bool tabs;
            /// <summary>One of the pane's tabs (Bio, Gear, Health, Log...) is open.</summary>
            public bool tabOpen;
            /// <summary>The top of the gizmos right of the pane (the screen's height when none show).</summary>
            public float gizmoTop;
        }

        /// <summary>Where the large portrait goes.</summary>
        public enum Placed
        {
            None,
            /// <summary>On the tab buttons at the pane's left, no tab open.</summary>
            OnPane
        }

        /// <summary>
        /// The large portrait's place: standing on the pane's tab buttons at its left, lifted above the gizmos where it
        /// reaches past the pane's right side over them; never past the screen's sides or into the room kept clear at its
        /// top, smaller than asked where the room is less. Not shown while a tab is open (the player, 21 November 2026: "it
        /// only needs to be shown when you click the pawn, without having one of these tabs open ... Once you click one of
        /// these tabs, the image of the pawn can, disappear in favor of the tab"), so it never stands beside or over a tab.
        /// </summary>
        public static Placed Place(PaneScene s, float size, out FrameRect r)
        {
            r = default;
            if (s.tabOpen) return Placed.None;
            float baseY = Math.Min(s.paneTop, s.pane.y) - (s.tabs ? TabRowH : 0f);
            float x = s.pane.x;
            float foot = baseY;
            float fit = Fit(size, Math.Min(foot - TopClear, s.screenW - x));
            if (fit <= 0f) return Placed.None;
            // Reaching right past the pane, over the gizmos: lift it above them.
            if (x + fit > s.pane.XMax && foot > s.gizmoTop - Gap)
            {
                foot = s.gizmoTop - Gap;
                fit = Fit(size, Math.Min(foot - TopClear, s.screenW - x));
                if (fit <= 0f) return Placed.None;
            }
            r = new FrameRect(x, foot - fit, fit, fit);
            return Placed.OnPane;
        }

        /// <summary>Whether two rects on the screen overlap (touching edges don't).</summary>
        public static bool Overlap(FrameRect a, FrameRect b) => a.x < b.XMax && b.x < a.XMax && a.y < b.YMax && b.y < a.YMax;
    }
}
