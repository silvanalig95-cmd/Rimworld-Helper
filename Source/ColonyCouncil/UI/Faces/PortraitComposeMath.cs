using System;

namespace ColonyCouncil
{
    /// <summary>The setting "Backdrop" behind the large portrait: as painted (the player's choice), lightly softened, softened, or none (the portrait's own ground).</summary>
    public enum PortraitBackdropMode
    {
        AsPainted,
        LightlySoftened,
        Softened,
        Plain
    }

    /// <summary>The setting "Line round the person": off (the player's choice to start with), subtle, or stronger.</summary>
    public enum PortraitOutlineMode
    {
        Off,
        Subtle,
        Stronger
    }

    /// <summary>How a backdrop is drawn: its blur (a share of the portrait's side), brightness, colour, and the darkening at its corners.</summary>
    public struct BackdropLook
    {
        public float blur, brightness, colour, corners;

        public BackdropLook(float blur, float brightness, float colour, float corners)
        {
            this.blur = blur;
            this.brightness = brightness;
            this.colour = colour;
            this.corners = corners;
        }
    }

    /// <summary>
    /// How the line round the person is drawn, each from the cut-out's own shape: a soft dark halo (its reach as a share of
    /// the portrait's side, and its strength), a faint pale edge and a thin dark ink line (their widths as shares, at
    /// least a pixel, and their strengths).
    /// </summary>
    public struct OutlineLook
    {
        public float haloReach, halo, paleWidth, pale, inkWidth, ink;

        public OutlineLook(float haloReach, float halo, float paleWidth, float pale, float inkWidth, float ink)
        {
            this.haloReach = haloReach;
            this.halo = halo;
            this.paleWidth = paleWidth;
            this.pale = pale;
            this.inkWidth = inkWidth;
            this.ink = ink;
        }
    }

    /// <summary>
    /// The large portrait's picture made once from the library's cut-out person and a backdrop (docs: DESIGN.md, "Portraits,
    /// phase 4"; <c>docs/portrait-art/FRAMEWORK.md</c>, "How they go together" and "Nothing of this is baked into the
    /// pictures"), without game types (Tools/Tests try them), on the small pictures already cut to the portrait's size.
    /// Pixels are RGBA bytes, four to a pixel; nothing here depends on which way the rows run.
    /// <list type="bullet">
    /// <item>The backdrop (<see cref="Backdrop"/>): as painted, lightly softened (a blur of 0.3% of the portrait, 86%
    /// brightness, 92% colour, a light darkening at the corners) or softened (1.2%, 62%, 75%, darker corners); then the
    /// light of the hour (<see cref="PortraitPictureMath.LightTint"/>).</item>
    /// <item>The person (<see cref="PersonLayer"/>): with the line round them where it is on (a soft dark halo just round
    /// them, a faint pale edge at about a third of the ink's strength, a thin dark ink line, all grown a few pixels from
    /// the cut-out's alpha and blurred a little), tinted a fifth toward the room's light where that is on.</item>
    /// </list>
    /// The two are kept as two pictures, so the person can breathe and sway in front of a backdrop that stays still.
    /// </summary>
    public static class PortraitComposeMath
    {
        public static BackdropLook LookOf(PortraitBackdropMode mode)
        {
            switch (mode)
            {
                case PortraitBackdropMode.LightlySoftened: return new BackdropLook(0.003f, 0.86f, 0.92f, 0.2f);
                case PortraitBackdropMode.Softened: return new BackdropLook(0.012f, 0.62f, 0.75f, 0.4f);
                default: return new BackdropLook(0f, 1f, 1f, 0f);
            }
        }

        /// <summary>The line's look for a setting (all strengths 0 when off).</summary>
        public static OutlineLook LookOf(PortraitOutlineMode mode)
        {
            switch (mode)
            {
                case PortraitOutlineMode.Subtle: return new OutlineLook(0.022f, 0.38f, 0.005f, 0.2f, 0.0028f, 0.6f);
                case PortraitOutlineMode.Stronger: return new OutlineLook(0.028f, 0.55f, 0.006f, 0.28f, 0.004f, 0.85f);
                default: return new OutlineLook(0f, 0f, 0f, 0f, 0f, 0f);
            }
        }

        /// <summary>The share of the way a person is tinted toward the room's light, where the setting is on.</summary>
        public const float PersonTint = 0.2f;

        /// <summary>The colours of the halo, the pale edge and the ink line.</summary>
        public static readonly byte[] HaloColour = { 14, 12, 16 }, PaleColour = { 232, 226, 210 }, InkColour = { 22, 18, 16 };

        // ---------------- The backdrop ----------------

        /// <summary>
        /// A backdrop cut to the portrait's square (<paramref name="side"/> pixels a side), drawn as <paramref name="look"/>
        /// says, then tinted by the light (<paramref name="tr"/>, <paramref name="tg"/>, <paramref name="tb"/>), in place.
        /// </summary>
        public static void Backdrop(byte[] px, int side, BackdropLook look, float tr, float tg, float tb)
        {
            if (px == null || side <= 0) return;
            int radius = (int)Math.Round(look.blur * side);
            if (radius >= 1) BlurRgb(px, side, side, radius);
            float bright = Clamp(look.brightness, 0f, 2f), colour = Clamp(look.colour, 0f, 2f), corners = Clamp(look.corners, 0f, 1f);
            float c = (side - 1) / 2f, far = c * 1.4142f;
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                int i = (y * side + x) * 4;
                float r = px[i], g = px[i + 1], b = px[i + 2];
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                r = lum + (r - lum) * colour;
                g = lum + (g - lum) * colour;
                b = lum + (b - lum) * colour;
                float f = bright;
                if (corners > 0f)
                {
                    float dx = x - c, dy = y - c;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / far;
                    f *= 1f - corners * Smooth(0.45f, 1f, d);
                }
                px[i] = Byte(r * f * tr);
                px[i + 1] = Byte(g * f * tg);
                px[i + 2] = Byte(b * f * tb);
                px[i + 3] = 255;
            }
        }

        /// <summary>The average colour of a picture's pixels, by their alpha (0 to 1 a channel; grey where nothing shows).</summary>
        public static void Average(byte[] px, out float r, out float g, out float b)
        {
            double sr = 0, sg = 0, sb = 0, sa = 0;
            if (px != null)
                for (int i = 0; i + 3 < px.Length; i += 4)
                {
                    double a = px[i + 3];
                    sr += px[i] * a;
                    sg += px[i + 1] * a;
                    sb += px[i + 2] * a;
                    sa += a;
                }
            if (sa <= 0)
            {
                r = g = b = 0.5f;
                return;
            }
            r = (float)(sr / sa / 255.0);
            g = (float)(sg / sa / 255.0);
            b = (float)(sb / sa / 255.0);
        }

        /// <summary>
        /// The tint toward a room's light for a person: their colours a fifth of the way to the light's hue (its colour
        /// at the person's own brightness), so a warm hall warms them and a cold night cools them without washing them out.
        /// </summary>
        public static void TintToward(float lr, float lg, float lb, out float tr, out float tg, out float tb)
        {
            float lum = 0.299f * lr + 0.587f * lg + 0.114f * lb;
            if (lum < 0.02f)
            {
                tr = tg = tb = 1f;
                return;
            }
            tr = 1f + PersonTint * (Clamp(lr / lum, 0.4f, 1.8f) - 1f);
            tg = 1f + PersonTint * (Clamp(lg / lum, 0.4f, 1.8f) - 1f);
            tb = 1f + PersonTint * (Clamp(lb / lum, 0.4f, 1.8f) - 1f);
        }

        // ---------------- The person ----------------

        /// <summary>
        /// The person's picture for the portrait (<paramref name="side"/> by <paramref name="side"/>, clear around them): the
        /// halo, the pale edge and the ink line under them where <paramref name="look"/> has them, then the person tinted by
        /// (<paramref name="tr"/>, <paramref name="tg"/>, <paramref name="tb"/>; 1 for none). A new picture; the person's is left as it was.
        /// </summary>
        public static byte[] PersonLayer(byte[] person, int side, OutlineLook look, float tr = 1f, float tg = 1f, float tb = 1f)
        {
            int n = side * side;
            var outPx = new byte[n * 4];
            var a = PortraitPictureMath.AlphaOf(person);
            bool lines = look.halo > 0f || look.pale > 0f || look.ink > 0f;
            byte[] halo = null, pale = null, ink = null;
            if (lines)
            {
                int inkW = Math.Max(1, (int)Math.Round(look.inkWidth * side));
                int paleW = Math.Max(1, (int)Math.Round(look.paleWidth * side));
                int reach = Math.Max(2, (int)Math.Round(look.haloReach * side));
                var inkShape = Dilate(a, side, side, inkW);
                var paleShape = Dilate(inkShape, side, side, paleW);
                ink = Ring(inkShape, a);
                pale = Ring(paleShape, inkShape);
                BlurAlpha(ink, side, side, 1, 1);
                BlurAlpha(pale, side, side, 1, 1);
                halo = Dilate(a, side, side, Math.Max(1, reach / 2));
                BlurAlpha(halo, side, side, Math.Max(1, reach / 2), 2);
            }
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                float r = 0f, g = 0f, b = 0f, al = 0f;
                if (lines)
                {
                    Over(ref r, ref g, ref b, ref al, HaloColour[0], HaloColour[1], HaloColour[2], halo[i] / 255f * look.halo);
                    Over(ref r, ref g, ref b, ref al, PaleColour[0], PaleColour[1], PaleColour[2], pale[i] / 255f * look.pale);
                    Over(ref r, ref g, ref b, ref al, InkColour[0], InkColour[1], InkColour[2], ink[i] / 255f * look.ink);
                }
                Over(ref r, ref g, ref b, ref al, person[o] * tr, person[o + 1] * tg, person[o + 2] * tb, person[o + 3] / 255f);
                if (al <= 0f)
                {
                    // Clear: the person's own colour kept under it, so the edge never fades to black when drawn smaller.
                    outPx[o] = person[o];
                    outPx[o + 1] = person[o + 1];
                    outPx[o + 2] = person[o + 2];
                    continue;
                }
                outPx[o] = Byte(r);
                outPx[o + 1] = Byte(g);
                outPx[o + 2] = Byte(b);
                outPx[o + 3] = Byte(al * 255f);
            }
            return outPx;
        }

        /// <summary>One colour laid over what is there (straight alpha): <paramref name="a"/> of it on top.</summary>
        public static void Over(ref float r, ref float g, ref float b, ref float al, float sr, float sg, float sb, float a)
        {
            if (a <= 0f) return;
            if (a > 1f) a = 1f;
            float outA = a + al * (1f - a);
            if (outA <= 0f) return;
            float under = al * (1f - a);
            r = (sr * a + r * under) / outA;
            g = (sg * a + g * under) / outA;
            b = (sb * a + b * under) / outA;
            al = outA;
        }

        /// <summary>What one shape covers beyond another: the outer less the inner, never below nothing.</summary>
        public static byte[] Ring(byte[] outer, byte[] inner)
        {
            var r = new byte[outer.Length];
            for (int i = 0; i < r.Length; i++)
            {
                int v = outer[i] - inner[i];
                r[i] = v > 0 ? (byte)v : (byte)0;
            }
            return r;
        }

        /// <summary>A shape grown by <paramref name="radius"/> pixels each way (the most of each pixel's square neighbourhood, across then down).</summary>
        public static byte[] Dilate(byte[] a, int w, int h, int radius)
        {
            if (radius <= 0) return (byte[])a.Clone();
            var tmp = new byte[a.Length];
            var dst = new byte[a.Length];
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    byte m = 0;
                    int x0 = Math.Max(0, x - radius), x1 = Math.Min(w - 1, x + radius);
                    for (int k = x0; k <= x1; k++)
                        if (a[row + k] > m)
                            m = a[row + k];
                    tmp[row + x] = m;
                }
            }
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                byte m = 0;
                int y0 = Math.Max(0, y - radius), y1 = Math.Min(h - 1, y + radius);
                for (int k = y0; k <= y1; k++)
                    if (tmp[k * w + x] > m)
                        m = tmp[k * w + x];
                dst[y * w + x] = m;
            }
            return dst;
        }

        /// <summary>One channel blurred by a box <paramref name="radius"/> pixels each way, <paramref name="passes"/> times (twice or more is near a soft round blur); nothing past the edges.</summary>
        public static void BlurAlpha(byte[] a, int w, int h, int radius, int passes)
        {
            if (radius <= 0 || passes <= 0) return;
            var tmp = new int[Math.Max(w, h)];
            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < h; y++) BoxLine(a, y * w, 1, w, radius, tmp, false);
                for (int x = 0; x < w; x++) BoxLine(a, x, w, h, radius, tmp, false);
            }
        }

        /// <summary>A picture's colours blurred by a box of <paramref name="radius"/> pixels, three times (near a soft round blur), its edges held.</summary>
        public static void BlurRgb(byte[] px, int w, int h, int radius)
        {
            if (radius <= 0) return;
            var ch = new byte[w * h];
            var tmp = new int[Math.Max(w, h)];
            for (int c = 0; c < 3; c++)
            {
                for (int i = 0; i < ch.Length; i++) ch[i] = px[i * 4 + c];
                for (int p = 0; p < 3; p++)
                {
                    for (int y = 0; y < h; y++) BoxLine(ch, y * w, 1, w, radius, tmp, true);
                    for (int x = 0; x < w; x++) BoxLine(ch, x, w, h, radius, tmp, true);
                }
                for (int i = 0; i < ch.Length; i++) px[i * 4 + c] = ch[i];
            }
        }

        /// <summary>
        /// One line of a channel (<paramref name="count"/> values from <paramref name="start"/>, <paramref name="step"/>
        /// apart) blurred by a running box sum; past the ends the edge value (<paramref name="hold"/>) or nothing.
        /// </summary>
        private static void BoxLine(byte[] v, int start, int step, int count, int radius, int[] tmp, bool hold)
        {
            for (int i = 0; i < count; i++) tmp[i] = v[start + i * step];
            int width = radius * 2 + 1, sum = 0;
            int At(int i) => i < 0 ? (hold ? tmp[0] : 0) : i >= count ? (hold ? tmp[count - 1] : 0) : tmp[i];
            for (int i = -radius; i <= radius; i++) sum += At(i);
            for (int i = 0; i < count; i++)
            {
                v[start + i * step] = (byte)((sum + width / 2) / width);
                sum += At(i + radius + 1) - At(i - radius);
            }
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Clamp((x - e0) / (e1 - e0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static byte Byte(float v) => v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)(v + 0.5f);
    }
}
