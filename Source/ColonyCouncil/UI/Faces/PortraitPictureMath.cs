using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Where the head is in a cut-out picture, measured from its alpha: its top, the neck (the narrowest row under it) and its middle across, as shares of the picture.</summary>
    public struct HeadMeasure
    {
        public float top, neck, centre;
        public bool found;

        /// <summary>As the portraits' layout counts it: the head with its hair from its top to the neck.</summary>
        public PictureLayout Layout(FaceTurn faces, float shape) =>
            new PictureLayout(centre, (top + neck) / 2f, Math.Max(0.05f, neck - top), faces, shape);

        public override string ToString() => found ? $"top {top:0.000}, neck {neck:0.000}, centre {centre:0.000}" : "not found";
    }

    /// <summary>
    /// The painted library's pictures as the game loads them (docs: DESIGN.md, "Portraits, phase 4: the painted library in
    /// the game, with backdrops"), without game types (Tools/Tests try them). Pixels are RGBA bytes, four to a pixel, row
    /// by row from the top:
    /// <list type="bullet">
    /// <item>the head measured once from a cut-out's alpha (<see cref="MeasureHead"/>);</item>
    /// <item>the large portrait's shape for a picture (<see cref="TallFor"/>): the whole picture, its own shape, the
    /// size setting its width (the player, 22 November 2026: "I'd rather, you know, see as much as I can");</item>
    /// <item>a picture scaled down once to what the largest portrait needs (<see cref="Downscale"/>), the body made solid
    /// where it meets the picture's foot (<see cref="SolidFoot"/>), its clear edge given the colour beside it
    /// (<see cref="Bleed"/>) so no dark fringe shows when it is drawn smaller;</item>
    /// <item>the light on the map in a few steps (<see cref="LightStep"/>), each a darker, cooler tint (<see cref="LightTint"/>).</item>
    /// </list>
    /// </summary>
    public static class PortraitPictureMath
    {
        // ---------------- The head ----------------

        /// <summary>A pixel counts as the person from this alpha (of 255).</summary>
        public const byte Solid = 128;

        /// <summary>The head's top to the neck, as a share of the picture's height, at least and at most; where no neck shows, this much under the top.</summary>
        public const float HeadLeast = 0.1f, HeadMost = 0.5f, HeadUsual = 0.3f;

        /// <summary>
        /// Where the head is in a cut-out (<paramref name="alpha"/>: one byte a pixel, <paramref name="w"/> by
        /// <paramref name="h"/>, rows from the top): the top is the first row with any of the person in it (a speck of a few
        /// pixels aside); the neck the narrowest row (from the person's leftmost to rightmost pixel) under the head's widest
        /// row, before the shoulders, else 0.3 of the picture under the top (long hair over the shoulders hides the neck);
        /// the middle across the centre of the person's pixels from the top to the neck. Not found for a picture with no
        /// clear background (an old picture painted to its edges) or nothing in it.
        /// </summary>
        public static HeadMeasure MeasureHead(byte[] alpha, int w, int h)
        {
            var m = new HeadMeasure { top = 0.13f, neck = 0.58f, centre = 0.5f };
            if (alpha == null || w < 8 || h < 8 || alpha.Length < w * h) return m;
            int least = Math.Max(2, w / 100);
            var left = new int[h];
            var right = new int[h];
            var count = new int[h];
            long clear = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w, l = -1, r = -1, c = 0;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] < Solid)
                    {
                        clear++;
                        continue;
                    }
                    if (l < 0) l = x;
                    r = x;
                    c++;
                }
                left[y] = l;
                right[y] = r;
                count[y] = c;
            }
            // A picture with hardly any clear pixels is no cut-out: the head can't be found from its shape.
            if (clear < (long)w * h / 20) return m;
            int top = -1;
            for (int y = 0; y < h && top < 0; y++)
                if (count[y] >= least)
                    top = y;
            if (top < 0) return m;
            int Width(int y) => count[y] < least ? 0 : right[y] - left[y] + 1;
            int searchEnd = Math.Min(h - 1, top + (int)(HeadMost * h));
            // The head's widest row in its upper part, then the narrowest row under it before the width grows again (the shoulders).
            int widest = top, wide = Width(top);
            int headEnd = Math.Min(searchEnd, top + (int)(0.25f * h));
            for (int y = top; y <= headEnd; y++)
                if (Width(y) > wide)
                {
                    wide = Width(y);
                    widest = y;
                }
            int neck = -1, narrow = int.MaxValue;
            for (int y = widest + 1; y <= searchEnd; y++)
            {
                int wy = Width(y);
                if (wy <= 0) continue;
                if (wy < narrow)
                {
                    narrow = wy;
                    neck = y;
                }
                // Past the neck the shoulders widen fast: stop once a row is much wider than the narrowest seen.
                if (neck >= 0 && wy > narrow * 1.6f && wy > wide) break;
            }
            int least0 = top + (int)(HeadLeast * h), most0 = top + (int)(HeadMost * h);
            bool neckShows = neck >= 0 && narrow < 0.88f * wide && neck >= least0 && neck <= most0;
            if (!neckShows) neck = Math.Min(h - 1, top + (int)(HeadUsual * h));
            long sum = 0, n = 0;
            for (int y = top; y <= neck; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] < Solid) continue;
                    sum += x;
                    n++;
                }
            }
            m.top = (float)top / h;
            m.neck = (float)(neck + 1) / h;
            m.centre = n > 0 ? (sum / (float)n + 0.5f) / w : 0.5f;
            m.found = true;
            return m;
        }

        // ---------------- The large portrait's shape ----------------

        /// <summary>The large portrait's largest size (its width).</summary>
        public const float LargePanel = 360f;

        /// <summary>The library's pictures' shape (1024 x 1280: 0.8 wide for each unit high), taken while a picture loads.</summary>
        public const float LibraryShape = 0.8f;

        /// <summary>The tallest the large portrait grows for a picture, for each unit of its width.</summary>
        public const float TallMost = 1.6f;

        /// <summary>
        /// The large portrait's height for each unit of its width with a painted picture (the player, 22 November 2026: "I'd
        /// rather, you know, see as much as I can ... It can show as much as the image, so to say, generates"): the whole
        /// picture at its own shape (<paramref name="shape"/>, its width for each unit of height: 1.25 for the library's 1024
        /// x 1280), never shorter than square nor taller than <see cref="TallMost"/>; square where the shape isn't known.
        /// </summary>
        public static float TallFor(float shape)
        {
            if (float.IsNaN(shape) || shape <= 0.05f) return 1f;
            float t = 1f / shape;
            return t < 1f ? 1f : t > TallMost ? TallMost : t;
        }

        /// <summary>The panel's outer height for its width: the inside (within its border) of the picture's shape, the border round it.</summary>
        public static float PanelHeight(float width, float tall, float border) => (width - 2f * border) * (tall < 1f ? 1f : tall) + 2f * border;

        /// <summary>
        /// The part of a backdrop (<paramref name="bw"/> by <paramref name="bh"/>) that fills a panel <paramref name="aspect"/>
        /// wide for each unit high: its middle, as much of it as has that shape, as the scale and offset the graphics card
        /// takes (shares of the backdrop, from its foot).
        /// </summary>
        public static void BackdropPart(float bw, float bh, float aspect, out float sx, out float sy, out float ox, out float oy)
        {
            if (!(bw > 0f) || !(bh > 0f) || !(aspect > 0f) || float.IsInfinity(aspect))
            {
                sx = sy = 1f;
                ox = oy = 0f;
                return;
            }
            float own = bw / bh;
            if (own > aspect)
            {
                sx = aspect / own;
                sy = 1f;
            }
            else
            {
                sx = 1f;
                sy = own / aspect;
            }
            ox = (1f - sx) / 2f;
            oy = (1f - sy) / 2f;
        }

        // ---------------- The foot of the picture ----------------

        /// <summary>The foot of a picture made solid this far up at most, as a share of its height.</summary>
        public const float FootBand = 0.03f;

        /// <summary>
        /// The person made solid where they meet the picture's foot (the player, 22 November 2026: "there's a really minor,
        /// small offset between the bottom of its body and where the UI continues below"): a background remover leaves the
        /// last rows of a body that runs off the picture half clear. In each column whose last row has any of the person in
        /// it, the lowest solid pixel within <see cref="FootBand"/> of the foot is carried down to the foot, so the body runs
        /// to the edge; columns clear at the foot (the ground beside an arm) are left as they are. In place.
        /// </summary>
        public static void SolidFoot(byte[] px, int w, int h)
        {
            if (px == null || w <= 0 || h <= 1 || px.Length < w * h * 4) return;
            int band = Math.Max(2, (int)Math.Round(FootBand * h));
            int last = h - 1;
            for (int x = 0; x < w; x++)
            {
                if (px[(last * w + x) * 4 + 3] <= 8) continue;
                int solid = -1;
                for (int y = last; y >= Math.Max(0, h - band); y--)
                    if (px[(y * w + x) * 4 + 3] >= Solid)
                    {
                        solid = y;
                        break;
                    }
                if (solid < 0 || solid == last) continue;
                int from = (solid * w + x) * 4;
                for (int y = solid + 1; y <= last; y++)
                {
                    int i = (y * w + x) * 4;
                    px[i] = px[from];
                    px[i + 1] = px[from + 1];
                    px[i + 2] = px[from + 2];
                    px[i + 3] = 255;
                }
                px[from + 3] = 255;
            }
        }

        // ---------------- Scaling and the clear edge ----------------

        /// <summary>
        /// The longest side a picture is kept at: twice what the largest portrait (360 at the interface's scale, 5:4) shows,
        /// at least 640 and at most 1280 pixels, so every size of the portrait is made by shrinking it, sharp (the player,
        /// 23 November 2026: "there seems to be some threshold below which it kind of makes the image a bit more blurry ...
        /// It should ideally be sharp at all stages ... we can just load it at high resolution throughout").
        /// </summary>
        public static int LongSideFor(float uiScale)
        {
            if (float.IsNaN(uiScale) || uiScale <= 0f) uiScale = 1f;
            int side = (int)Math.Ceiling(LargePanel * uiScale * 2.5f);
            return side < 640 ? 640 : side > 1280 ? 1280 : side;
        }

        /// <summary>
        /// Whether a part of a picture <paramref name="sw"/> by <paramref name="sh"/> pixels, made <paramref name="w"/> by
        /// <paramref name="h"/>, is shrunk enough that the graphics card's own shrinking would soften it: then it is read at
        /// its own size and shrunk by <see cref="Downscale"/>, each pixel the average of all it covers.
        /// </summary>
        public static bool ShrinkSharp(int sw, int sh, int w, int h) => w > 0 && h > 0 && (sw > w * 1.05f || sh > h * 1.05f);

        /// <summary>The size a <paramref name="w"/> by <paramref name="h"/> picture is kept at: its long side at most <paramref name="longSide"/>, never larger than it is.</summary>
        public static void FitSize(int w, int h, int longSide, out int nw, out int nh)
        {
            int l = Math.Max(w, h);
            if (l <= longSide || l <= 0)
            {
                nw = Math.Max(1, w);
                nh = Math.Max(1, h);
                return;
            }
            double k = (double)longSide / l;
            nw = Math.Max(1, (int)Math.Round(w * k));
            nh = Math.Max(1, (int)Math.Round(h * k));
        }

        /// <summary>The smallest copy of a picture kept, on its long side.</summary>
        public const int SmallestCopy = 24;

        /// <summary>
        /// The sizes of a picture's smaller copies, largest first: each half the one before (rounded down), while its long
        /// side stays at least <see cref="SmallestCopy"/>. Each is its own texture without smaller levels (the player, 23
        /// November 2026: the small portraits still showed a second face behind the picture; 1.7's graphics engine draws the
        /// smaller levels of a texture this size shifted, so they are never used).
        /// </summary>
        public static List<(int w, int h)> CopySizes(int w, int h)
        {
            var sizes = new List<(int w, int h)>();
            while (Math.Max(w, h) / 2 >= SmallestCopy)
            {
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
                sizes.Add((w, h));
            }
            return sizes;
        }

        /// <summary>
        /// Which copy of a picture to draw where its whole width shows <paramref name="drawn"/> pixels wide on the screen:
        /// the smallest at least that wide, as an index into <paramref name="widths"/> (largest first), or -1 for the picture
        /// itself. Drawn from a copy at most twice its size on the screen, the picture stays sharp and never grainy.
        /// </summary>
        public static int CopyFor(IList<int> widths, float drawn)
        {
            if (widths == null || float.IsNaN(drawn) || float.IsInfinity(drawn)) return -1;
            int best = -1;
            for (int i = 0; i < widths.Count; i++)
            {
                if (widths[i] < drawn) break;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// A picture scaled down to <paramref name="nw"/> by <paramref name="nh"/>, each new pixel the average of the old
        /// ones it covers (by area), its colour weighted by alpha so a clear background's colour never darkens the edge.
        /// </summary>
        public static byte[] Downscale(byte[] src, int w, int h, int nw, int nh)
        {
            if (nw == w && nh == h) return (byte[])src.Clone();
            var dst = new byte[nw * nh * 4];
            double sx = (double)w / nw, sy = (double)h / nh;
            var acc = new double[nw * 4];
            var wsum = new double[nw];
            for (int y = 0; y < nh; y++)
            {
                Array.Clear(acc, 0, acc.Length);
                Array.Clear(wsum, 0, wsum.Length);
                double y0 = y * sy, y1 = y0 + sy;
                for (int oy = (int)y0; oy < Math.Min(h, (int)Math.Ceiling(y1)); oy++)
                {
                    double fy = Math.Min(y1, oy + 1) - Math.Max(y0, oy);
                    if (fy <= 0) continue;
                    int row = oy * w * 4;
                    for (int x = 0; x < nw; x++)
                    {
                        double x0 = x * sx, x1 = x0 + sx;
                        int xe = Math.Min(w, (int)Math.Ceiling(x1));
                        for (int ox = (int)x0; ox < xe; ox++)
                        {
                            double f = fy * (Math.Min(x1, ox + 1) - Math.Max(x0, ox));
                            if (f <= 0) continue;
                            int i = row + ox * 4;
                            double a = src[i + 3] * f;
                            acc[x * 4] += src[i] * a;
                            acc[x * 4 + 1] += src[i + 1] * a;
                            acc[x * 4 + 2] += src[i + 2] * a;
                            acc[x * 4 + 3] += a;
                            wsum[x] += f;
                        }
                    }
                }
                for (int x = 0; x < nw; x++)
                {
                    int o = (y * nw + x) * 4;
                    double a = acc[x * 4 + 3];
                    if (a > 0)
                    {
                        dst[o] = Byte(acc[x * 4] / a);
                        dst[o + 1] = Byte(acc[x * 4 + 1] / a);
                        dst[o + 2] = Byte(acc[x * 4 + 2] / a);
                    }
                    dst[o + 3] = wsum[x] > 0 ? Byte(a / wsum[x]) : (byte)0;
                }
            }
            return dst;
        }

        private static byte Byte(double v) => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);

        /// <summary>
        /// The clear pixels next to the person given the colour of the person's pixels beside them, <paramref name="passes"/>
        /// pixels deep (their alpha stays 0), so when the picture is drawn smaller or its mipmaps are made the edge keeps
        /// the person's colour instead of fading to black.
        /// </summary>
        public static void Bleed(byte[] px, int w, int h, int passes)
        {
            if (px == null || passes <= 0) return;
            var done = new bool[w * h];
            for (int i = 0; i < w * h; i++) done[i] = px[i * 4 + 3] > 0;
            var next = new System.Collections.Generic.List<int>();
            for (int pass = 0; pass < passes; pass++)
            {
                next.Clear();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (done[i]) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (!done[j]) continue;
                        r += px[j * 4];
                        g += px[j * 4 + 1];
                        b += px[j * 4 + 2];
                        n++;
                    }
                    if (n == 0) continue;
                    px[i * 4] = (byte)(r / n);
                    px[i * 4 + 1] = (byte)(g / n);
                    px[i * 4 + 2] = (byte)(b / n);
                    next.Add(i);
                }
                if (next.Count == 0) break;
                foreach (int i in next) done[i] = true;
            }
        }

        /// <summary>The rows turned over (the game's textures run from their foot up; these pixels from the top down).</summary>
        public static byte[] FlipRows(byte[] src, int w, int h)
        {
            var dst = new byte[src.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++) Buffer.BlockCopy(src, y * stride, dst, (h - 1 - y) * stride, stride);
            return dst;
        }

        /// <summary>The alpha of each pixel, one byte a pixel.</summary>
        public static byte[] AlphaOf(byte[] px)
        {
            var a = new byte[px.Length / 4];
            for (int i = 0; i < a.Length; i++) a[i] = px[i * 4 + 3];
            return a;
        }

        // ---------------- Day and night ----------------

        /// <summary>The light where the person stands (0 dark to 1 full daylight) at which each step begins: full light, dusk, dim, dark.</summary>
        public static readonly float[] StepFrom = { 0.7f, 0.45f, 0.22f, 0f };

        /// <summary>How far past a step's edge the light must go before the step changes (so a picture isn't made again for a flicker).</summary>
        public const float StepSlack = 0.04f;

        /// <summary>The step of light: 0 full light, 1 dusk, 2 dim, 3 dark; the step it was kept unless the light is clearly past its edge.</summary>
        public static int LightStep(float glow, int was = -1)
        {
            if (float.IsNaN(glow)) glow = 1f;
            int step = StepFrom.Length - 1;
            for (int i = 0; i < StepFrom.Length; i++)
                if (glow >= StepFrom[i])
                {
                    step = i;
                    break;
                }
            if (was < 0 || was >= StepFrom.Length || was == step) return step;
            // Kept while the light is within the slack of the kept step's own band.
            float hi = was == 0 ? 2f : StepFrom[was - 1], lo = StepFrom[was];
            return glow >= lo - StepSlack && glow < hi + StepSlack ? was : step;
        }

        /// <summary>The tint for a step of light: each step darker and a little cooler (red and green down more than blue).</summary>
        public static void LightTint(int step, out float r, out float g, out float b)
        {
            step = step < 0 ? 0 : step > 3 ? 3 : step;
            float dark = new[] { 1f, 0.86f, 0.7f, 0.52f }[step], cool = new[] { 0f, 0.05f, 0.1f, 0.15f }[step];
            r = dark * (1f - cool);
            g = dark * (1f - cool * 0.5f);
            b = dark;
        }
    }
}
