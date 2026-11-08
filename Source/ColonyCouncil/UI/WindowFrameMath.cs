using System;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>Which edges of a window a drag sizes from (a corner is two).</summary>
    [Flags]
    public enum FrameEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8
    }

    /// <summary>A window's place on the screen, in the interface's units (x and y from the top left).</summary>
    public struct FrameRect
    {
        public float x, y, w, h;

        public FrameRect(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public float XMax => x + w;
        public float YMax => y + h;

        public bool Near(FrameRect o, float by = 1f) => Math.Abs(x - o.x) <= by && Math.Abs(y - o.y) <= by && Math.Abs(w - o.w) <= by && Math.Abs(h - o.h) <= by;

        public override string ToString() => $"{x:0},{y:0} {w:0}x{h:0}";
    }

    /// <summary>
    /// Where a Rimfolk window is remembered: its place, whether it is large, and the place it goes back to from large.
    /// Kept in the mod's settings by the window's key (UI/WindowFrame.cs), so it lasts between openings and games.
    /// </summary>
    public struct FramePlace
    {
        public FrameRect rect;
        public bool large;
        /// <summary>Where the window was before it was made large (its place when it is made small again).</summary>
        public FrameRect back;
    }

    /// <summary>
    /// The rules of Rimfolk's window frame (docs/design/rimfolk-look.md, stage 1): the title bar's height and the grips
    /// at the edges, where a window first opens, how it is moved and sized and kept on the screen (and above the bottom
    /// bar where it asks), its least size, the large size (about 92% of the screen) and back, and the remembered place
    /// written as text in the settings and read back, checked, and fitted to the screen it opens on (another resolution
    /// or interface scale). All in the game's interface units; free of game types, so Tools/Tests try every screen.
    /// </summary>
    public static class WindowFrameMath
    {
        /// <summary>The title bar: its height, its buttons and their gap.</summary>
        public const float TitleH = 36f, TitleButton = 24f, TitleGap = 6f;

        /// <summary>How far in from an edge a press sizes the window, and how large a corner's grip is.</summary>
        public const float Grip = 6f, CornerGrip = 16f;

        /// <summary>The large size's share of the screen.</summary>
        public const float LargeShare = 0.92f;

        /// <summary>The game's bottom bar, kept clear by a window that asks for it (the region window; a dialog may cover it).</summary>
        public const float BottomBar = 35f;

        /// <summary>The smallest a window can be made anywhere (a window gives its own least, never below this): a name asked for in a field and its OK.</summary>
        public const float LeastW = 360f, LeastH = 160f;

        /// <summary>
        /// The region window's least size (its tabs and the corner in the title bar, a view under the map's panel laid over
        /// it) and the trade window's (the foot's five buttons and the silver, the table's name and count beside the side).
        /// </summary>
        public const float RegionLeastW = 720f, RegionLeastH = 480f, TradeLeastW = 980f, TradeLeastH = 560f;

        /// <summary>The body's inset from the window's sides and foot: Rimfolk's own look, and the game's (its window margin).</summary>
        public const float BodyInsetOwn = 10f, BodyInsetGame = 18f;

        /// <summary>The body's width in a window this wide, under the title bar.</summary>
        public static float BodyWidth(float windowW, bool ownLook) => windowW - 2f * (ownLook ? BodyInsetOwn : BodyInsetGame);

        /// <summary>The body's height in a window this tall: under the title bar (and a little gap in the game's look), above the inset.</summary>
        public static float BodyHeight(float windowH, bool ownLook) => windowH - TitleH - (ownLook ? BodyInsetOwn : BodyInsetGame + 4f);

        /// <summary>The room a window has on a screen: the whole width, the height less the bottom bar where it keeps it clear.</summary>
        public static void Room(float screenW, float screenH, float bottom, out float roomW, out float roomH)
        {
            roomW = Math.Max(1f, screenW);
            roomH = Math.Max(1f, screenH - Math.Max(0f, bottom));
        }

        /// <summary>A window's least size on this screen: its own least, never below <see cref="LeastW"/> by <see cref="LeastH"/>, never more than the room.</summary>
        public static void Least(float minW, float minH, float screenW, float screenH, float bottom, out float lw, out float lh)
        {
            Room(screenW, screenH, bottom, out float roomW, out float roomH);
            lw = Math.Min(Math.Max(minW, LeastW), roomW);
            lh = Math.Min(Math.Max(minH, LeastH), roomH);
        }

        /// <summary>
        /// The rect kept on the screen: no smaller than the least size, no larger than the room, and wholly inside it (moved
        /// in from an edge it went past, the top left kept first).
        /// </summary>
        public static FrameRect Clamp(FrameRect r, float screenW, float screenH, float minW, float minH, float bottom)
        {
            Room(screenW, screenH, bottom, out float roomW, out float roomH);
            Least(minW, minH, screenW, screenH, bottom, out float lw, out float lh);
            if (!Finite(r)) r = new FrameRect(0f, 0f, lw, lh);
            r.w = Math.Min(Math.Max(r.w, lw), roomW);
            r.h = Math.Min(Math.Max(r.h, lh), roomH);
            r.x = Math.Max(0f, Math.Min(r.x, roomW - r.w));
            r.y = Math.Max(0f, Math.Min(r.y, roomH - r.h));
            return Round(r);
        }

        /// <summary>Where a window first opens: the size it asks for, within the room, in the middle of it.</summary>
        public static FrameRect Centred(float wantW, float wantH, float screenW, float screenH, float minW, float minH, float bottom)
        {
            Room(screenW, screenH, bottom, out float roomW, out float roomH);
            Least(minW, minH, screenW, screenH, bottom, out float lw, out float lh);
            // The size first (within the least and the room), then the middle for that size.
            float w = Math.Min(Math.Max(wantW, lw), roomW), h = Math.Min(Math.Max(wantH, lh), roomH);
            return Clamp(new FrameRect((roomW - w) / 2f, (roomH - h) / 2f, w, h), screenW, screenH, minW, minH, bottom);
        }

        /// <summary>The large size: about 92% of the room each way, in the middle of it.</summary>
        public static FrameRect Large(float screenW, float screenH, float minW, float minH, float bottom)
        {
            Room(screenW, screenH, bottom, out float roomW, out float roomH);
            return Centred(roomW * LargeShare, roomH * LargeShare, screenW, screenH, minW, minH, bottom);
        }

        /// <summary>The window moved by the mouse's way since the press, kept on the screen.</summary>
        public static FrameRect Move(FrameRect start, float dx, float dy, float screenW, float screenH, float minW, float minH, float bottom)
        {
            start.x += dx;
            start.y += dy;
            return Clamp(start, screenW, screenH, minW, minH, bottom);
        }

        /// <summary>
        /// The window sized from <paramref name="edges"/> by the mouse's way since the press: the opposite edge stays put,
        /// the size kept between the least and the screen's edge.
        /// </summary>
        public static FrameRect Resize(FrameRect start, FrameEdge edges, float dx, float dy, float screenW, float screenH, float minW, float minH, float bottom)
        {
            Room(screenW, screenH, bottom, out float roomW, out float roomH);
            Least(minW, minH, screenW, screenH, bottom, out float lw, out float lh);
            var r = start;
            if ((edges & FrameEdge.Right) != 0) r.w = Math.Max(lw, Math.Min(start.w + dx, roomW - start.x));
            if ((edges & FrameEdge.Bottom) != 0) r.h = Math.Max(lh, Math.Min(start.h + dy, roomH - start.y));
            if ((edges & FrameEdge.Left) != 0)
            {
                float right = start.XMax;
                float x = Math.Max(0f, Math.Min(start.x + dx, right - lw));
                r.x = x;
                r.w = right - x;
            }
            if ((edges & FrameEdge.Top) != 0)
            {
                float bottomY = start.YMax;
                float y = Math.Max(0f, Math.Min(start.y + dy, bottomY - lh));
                r.y = y;
                r.h = bottomY - y;
            }
            return Clamp(r, screenW, screenH, minW, minH, bottom);
        }

        /// <summary>
        /// The edges a press at (<paramref name="lx"/>, <paramref name="ly"/>) inside a window <paramref name="w"/> by
        /// <paramref name="h"/> sizes from: within <see cref="Grip"/> of an edge, or within <see cref="CornerGrip"/> of a
        /// corner along either of its edges (both edges then); none inside or outside.
        /// </summary>
        public static FrameEdge EdgesAt(float lx, float ly, float w, float h)
        {
            if (lx < 0f || ly < 0f || lx > w || ly > h) return FrameEdge.None;
            bool nearL = lx <= Grip, nearR = lx >= w - Grip, nearT = ly <= Grip, nearB = ly >= h - Grip;
            bool cornerL = lx <= CornerGrip, cornerR = lx >= w - CornerGrip, cornerT = ly <= CornerGrip, cornerB = ly >= h - CornerGrip;
            var e = FrameEdge.None;
            if (nearL || (cornerL && (nearT || nearB))) e |= FrameEdge.Left;
            if (nearR || (cornerR && (nearT || nearB))) e |= FrameEdge.Right;
            if (nearT || (cornerT && (nearL || nearR))) e |= FrameEdge.Top;
            if (nearB || (cornerB && (nearL || nearR))) e |= FrameEdge.Bottom;
            // A window too narrow for both sides' grips takes the nearer.
            if ((e & FrameEdge.Left) != 0 && (e & FrameEdge.Right) != 0) e &= lx < w / 2f ? ~FrameEdge.Right : ~FrameEdge.Left;
            if ((e & FrameEdge.Top) != 0 && (e & FrameEdge.Bottom) != 0) e &= ly < h / 2f ? ~FrameEdge.Bottom : ~FrameEdge.Top;
            return e;
        }

        /// <summary>Large or back: the place after the button or a double-click on the title.</summary>
        public static FramePlace Toggle(FramePlace p, float screenW, float screenH, float minW, float minH, float bottom)
        {
            if (p.large)
            {
                p.large = false;
                p.rect = Clamp(Finite(p.back) && p.back.w > 0f ? p.back : p.rect, screenW, screenH, minW, minH, bottom);
                return p;
            }
            p.back = p.rect;
            p.large = true;
            p.rect = Large(screenW, screenH, minW, minH, bottom);
            return p;
        }

        /// <summary>
        /// Where a remembered window opens on this screen: large again at this screen's large size, or its place fitted to
        /// the screen (moved in and shrunk where the screen is smaller than the one it was left on).
        /// </summary>
        public static FrameRect Opening(FramePlace p, float screenW, float screenH, float minW, float minH, float bottom) =>
            p.large ? Large(screenW, screenH, minW, minH, bottom) : Clamp(p.rect, screenW, screenH, minW, minH, bottom);

        // ---------------- Remembered as text ----------------

        /// <summary>The place as text for the settings: "x;y;w;h" and, made large, ";L;" with where it goes back to.</summary>
        public static string Write(FramePlace p)
        {
            var c = CultureInfo.InvariantCulture;
            string s = string.Format(c, "{0:0};{1:0};{2:0};{3:0}", p.rect.x, p.rect.y, p.rect.w, p.rect.h);
            if (p.large) s += string.Format(c, ";L;{0:0};{1:0};{2:0};{3:0}", p.back.x, p.back.y, p.back.w, p.back.h);
            return s;
        }

        /// <summary>
        /// A place read back from the settings; false for text that is not one (hand-edited, from another version, sizes
        /// of nothing or of the impossible), so the window opens where it would at first.
        /// </summary>
        public static bool TryRead(string text, out FramePlace p)
        {
            p = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(';');
            if (parts.Length != 4 && parts.Length != 9) return false;
            if (!TryRect(parts, 0, out p.rect)) return false;
            if (parts.Length == 9)
            {
                if (parts[4] != "L" || !TryRect(parts, 5, out p.back)) return false;
                p.large = true;
            }
            return Valid(p.rect) && (!p.large || Valid(p.back));
        }

        private static bool TryRect(string[] parts, int at, out FrameRect r)
        {
            r = default;
            var c = CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[at], NumberStyles.Float, c, out r.x) || !float.TryParse(parts[at + 1], NumberStyles.Float, c, out r.y)
                || !float.TryParse(parts[at + 2], NumberStyles.Float, c, out r.w) || !float.TryParse(parts[at + 3], NumberStyles.Float, c, out r.h))
                return false;
            return true;
        }

        /// <summary>A rect a window can be at: finite, of some size, not absurdly far or large.</summary>
        public static bool Valid(FrameRect r) => Finite(r) && r.w >= 1f && r.h >= 1f && r.w <= 100000f && r.h <= 100000f && Math.Abs(r.x) <= 100000f && Math.Abs(r.y) <= 100000f;

        private static bool Finite(FrameRect r) => IsFinite(r.x) && IsFinite(r.y) && IsFinite(r.w) && IsFinite(r.h);

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        private static FrameRect Round(FrameRect r) => new FrameRect((float)Math.Round(r.x), (float)Math.Round(r.y), (float)Math.Floor(r.w), (float)Math.Floor(r.h));

        // ---------------- The remembered places, one line each ----------------

        /// <summary>A settings line: "key=place".</summary>
        public static string Line(string key, FramePlace p) => key + "=" + Write(p);

        /// <summary>A settings line read: its key and place; false for a line that is not one.</summary>
        public static bool TryLine(string line, out string key, out FramePlace p)
        {
            key = null;
            p = default;
            int eq = line?.IndexOf('=') ?? -1;
            if (eq <= 0) return false;
            key = line.Substring(0, eq);
            return TryRead(line.Substring(eq + 1), out p);
        }
    }
}
