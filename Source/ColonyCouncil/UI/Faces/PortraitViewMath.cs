using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the head is in a whole picture of a person that a portrait source gives (a picture of the library, or any
    /// later source): its middle across and down and its height, each as a share of the picture (from the top left),
    /// the way the person faces in it, and the picture's shape (its width for each unit of height: 0.8 for the library's
    /// 1024 x 1280 pictures, 1 or 0 for a square one).
    /// </summary>
    public struct PictureLayout
    {
        /// <summary>The middle of the head with its hair, across and down; the head's height with its hair.</summary>
        public float headU, headV, headH;
        /// <summary>The way the person in the picture faces: the front, or turned to the picture's left or right.</summary>
        public FaceTurn faces;
        /// <summary>The picture's width for each unit of its height (0 or less: square).</summary>
        public float shape;

        public PictureLayout(float headU, float headV, float headH, FaceTurn faces, float shape = 1f)
        {
            this.headU = headU;
            this.headV = headV;
            this.headH = headH;
            this.faces = faces;
            this.shape = shape;
        }

        /// <summary>The picture's shape, square where it isn't known.</summary>
        public float Shape => shape > 0.05f && shape < 20f ? shape : 1f;
    }

    /// <summary>
    /// The part of a picture a portrait shows: from (<see cref="x"/>, <see cref="y"/>) across and down, <see cref="w"/>
    /// by <see cref="h"/>, all shares of the picture; and where the head lands in it (for the light behind it).
    /// </summary>
    public struct PictureView
    {
        public float x, y, w, h;
        public float headU, headV, headShare;

        public override string ToString() => $"({x:0.000}, {y:0.000}, {w:0.000} x {h:0.000}; head {headU:0.00}, {headV:0.00}, {headShare:0.00})";
    }

    /// <summary>
    /// How a whole picture of a person is framed for each portrait (Tools/Tests try it; docs: DESIGN.md, "Portraits, phase
    /// 3"): the same framing for the same place whatever the source, counted in head heights as the face engine counts
    /// the game's own drawing, so a picture's face sits where the game's face sat in that place.
    /// <list type="bullet">
    /// <item>the round portraits (<see cref="FaceFrame.Face"/>): the head with its hair a little over half the circle's
    /// height, a little room above it, the shoulders' top under it;</item>
    /// <item>the large portrait (<see cref="FaceFrame.Bust"/>): the head about half the square's height, the face two
    /// fifths down, the shoulders and chest under it;</item>
    /// <item>the colonist card's tall tile (<see cref="FaceFrame.Figure"/>): as much of the picture as fits the tile
    /// (the player, 22 November 2026: "I'd rather, you know, see as much as I can"), its whole height where the tile is
    /// narrower than the picture, the head at the top, the sides cut to the tile.</item>
    /// </list>
    /// The view never goes past the picture (a small margin kept, so a pose's small move or tilt never shows its edge),
    /// and keeps the head in the middle across. A picture is always shown as painted, never mirrored (the player, the
    /// same day: "They should, you know, always look the same direction if they're represented on different parts of the
    /// UI"); the large portrait at the inspect pane shows the whole picture (<see cref="PortraitPictureMath.TallFor"/>).
    /// </summary>
    public static class PortraitViewMath
    {
        /// <summary>
        /// Where the head is in the library's pictures as the lead lines them up (<c>docs/portrait-art/README.md</c>): square,
        /// the person in the middle, the eyes 40% down, the head with its hair from about 13% to 58% down, the shoulders
        /// reaching the foot. A picture's def may say otherwise.
        /// </summary>
        public const float LibraryHeadX = 0.5f, LibraryHeadY = 0.355f, LibraryHeadSize = 0.45f;

        public static readonly PictureLayout Library = new PictureLayout(LibraryHeadX, LibraryHeadY, LibraryHeadSize, FaceTurn.Front);

        /// <summary>The way a picture faces, as the engine turns it: a little or three-quarter to a side is that side.</summary>
        public static FaceTurn FacesOf(PortraitFacing f) =>
            f == PortraitFacing.Left || f == PortraitFacing.ThreeQuarterLeft ? FaceTurn.Left : f == PortraitFacing.Right || f == PortraitFacing.ThreeQuarterRight ? FaceTurn.Right : FaceTurn.Front;

        /// <summary>The share of the picture kept unshown at each edge.</summary>
        public const float Margin = 0.02f;

        /// <summary>The head's share of the view's height for each frame, and the room above the head's top as a share of the view.</summary>
        public static float HeadShareOf(FaceFrame f) => f == FaceFrame.Bust ? 0.5f : f == FaceFrame.Figure ? 0.42f : 0.6f;

        public static float RoomAboveOf(FaceFrame f) => f == FaceFrame.Bust ? 0.08f : f == FaceFrame.Figure ? 0.05f : 0.18f;

        private static bool Ok(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static float Clamp(float v, float lo, float hi) => hi < lo ? (lo + hi) / 2f : v < lo ? lo : v > hi ? hi : v;

        /// <summary>
        /// The view of a picture for a frame in a tile <paramref name="aspect"/> wide for each unit high, the head moved
        /// <paramref name="liftHeads"/> head heights up (a pose: the picture's content rises, so the view goes down).
        /// </summary>
        public static PictureView View(PictureLayout l, FaceFrame frame, float aspect, float liftHeads = 0f, bool open = false)
        {
            if (!Ok(l.headU) || !Ok(l.headV) || !Ok(l.headH) || l.headH < 0.05f || l.headH > 1f) l = new PictureLayout(LibraryHeadX, LibraryHeadY, LibraryHeadSize, l.faces, l.shape);
            if (!Ok(aspect) || aspect <= 0.05f || aspect > 20f) aspect = 1f;
            if (!Ok(liftHeads)) liftHeads = 0f;
            // The view's width in shares of the picture's width: a tile as wide as it is high takes 1.25 times the share of
            // a 1024 x 1280 picture's width that it takes of its height.
            aspect /= l.Shape;
            float most = 1f - 2f * Margin;
            // The tall tile: as much of the picture as fits, from its top; the others framed on the head.
            float h = frame == FaceFrame.Figure ? most : Math.Min(l.headH / HeadShareOf(frame), most);
            float w = h * aspect;
            if (w > most)
            {
                w = most;
                h = w / aspect;
            }
            float top = l.headV - l.headH / 2f;
            float y = top - RoomAboveOf(frame) * h + liftHeads * l.headH;
            // A cut-out (open: clear round the person) may be shown past its edges, so a small face is centred even where the
            // head is near the picture's top or side (the player, 23 November 2026: the smaller previews "should really focus
            // on the face and have that centered"); a picture with its own ground keeps to it.
            y = open && frame == FaceFrame.Face ? Clamp(y, -0.5f, 1f - Margin - h) : Clamp(y, Margin, 1f - Margin - h);
            float x = open && frame == FaceFrame.Face ? l.headU - w / 2f : Clamp(l.headU - w / 2f, Margin, 1f - Margin - w);
            return new PictureView
            {
                x = x, y = y, w = w, h = h,
                headU = (l.headU - x) / w,
                headV = (l.headV - y) / h,
                headShare = l.headH / h
            };
        }

        /// <summary>
        /// A pose on a whole picture: the head's lift (the picture's content moved up by these head heights, the proud up,
        /// the sad and prisoners down), and its turn in degrees about the foot of the tile (the lean, and half the head's
        /// tilt, since the shoulders turn with it). Round portraits take only the lift.
        /// </summary>
        public static void Pose(HeadShift s, out float liftHeads, out float turnDegrees)
        {
            liftHeads = s.dy;
            turnDegrees = s.lean + 0.5f * s.tilt;
        }

        /// <summary>The view's texture coordinates as the game's drawing takes them (from the foot of the picture up): x, y, width, height.</summary>
        public static void TexCoords(PictureView v, out float u, out float vv, out float w, out float h)
        {
            u = v.x;
            w = v.w;
            vv = 1f - v.y - v.h;
            h = v.h;
        }
    }
}
