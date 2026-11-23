using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Portraits, phase 4: the painted library in the game, with backdrops (the player, 22 November 2026: "let's just start
/// with the colonists that I have as a first batch ... the framework should then allow, additions of many, many more images
/// ... It should then find the closest match", "the backgrounds are correctly selected based on age and what it contains
/// and what kind of room it is", "the impact on the gameplay and performance requirements on the device are hopefully not
/// big"; and the rule of the same day: a picture chosen once and kept, a child's next stage the exception). The pure parts:
/// the head measured from a cut-out's alpha, the large portrait's crop by its size, the pictures scaled and edged, the
/// light's steps; the matcher's new rules (pins, the closest, babies and toddlers); the backdrop's choice (the room's kind,
/// the age, what is in it, the land outdoors, the fallbacks, a stable pick); the compose maths on small pictures; and the
/// time the pure parts take on a picture of the library's size.
/// </summary>
public static class PortraitStageTests
{
    /// <summary>A cut-out drawn by code: an oval head from <paramref name="top"/> down <paramref name="headH"/> pixels, a neck, and shoulders wider than the head to the foot.</summary>
    private static byte[] Figure(int w, int h, int top, int headH, int centre, bool longHair = false)
    {
        var a = new byte[w * h];
        int headW = headH * 3 / 4, neckW = headW / 2, neckTop = top + headH, shouldersTop = neckTop + headH / 5;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool on;
            if (y < top) on = false;
            else if (y < neckTop)
            {
                float dx = (x - centre) / (headW / 2f), dy = (y - (top + headH / 2f)) / (headH / 2f);
                on = dx * dx + dy * dy <= 1f;
            }
            else if (y < shouldersTop) on = Math.Abs(x - centre) <= neckW / 2;
            else on = Math.Abs(x - centre) <= headW + (y - shouldersTop) / 2;
            // Long hair down past the shoulders, as wide as the head: no neck shows.
            if (longHair && y >= top + headH / 2 && y < shouldersTop + headH && Math.Abs(x - centre) <= headW / 2) on = true;
            a[y * w + x] = on ? (byte)255 : (byte)0;
        }
        return a;
    }

    private static byte[] Rgba(int w, int h, Func<int, int, (byte r, byte g, byte b, byte a)> f)
    {
        var px = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var c = f(x, y);
            int o = (y * w + x) * 4;
            px[o] = c.r;
            px[o + 1] = c.g;
            px[o + 2] = c.b;
            px[o + 3] = c.a;
        }
        return px;
    }

    private static PortraitColour Hex(string h) => PortraitLibraryMath.TryColour(h, out var c) ? c : throw new Exception("bad colour " + h);

    private static PortraitLook Look(PortraitSex sex, PortraitAge age, string skin = "#e0b898", string hair = "#4a3020", string xenotype = "baseliner", string[]? features = null, string[]? marks = null)
    {
        var l = new PortraitLook { sex = sex, age = age, skin = Hex(skin), hairColour = Hex(hair), xenotype = xenotype, baseliner = xenotype == "baseliner" };

        foreach (var f in features ?? Array.Empty<string>()) l.features.Add(f);
        foreach (var m in marks ?? Array.Empty<string>()) l.marks.Add(PortraitLibraryMath.MarkKey(m));
        return l;
    }

    private static PortraitRecord Rec(string name, PortraitSex sex, PortraitAge age, string skin = "#e0b898", string hair = "#4a3020", string pawn = "", string character = "", string[]? xenotypes = null)
    {
        var r = new PortraitRecord { name = name, sex = sex, age = age, skin = Hex(skin), skinSet = true, hairColour = Hex(hair), hairColourSet = true, pawn = pawn, character = character };
        foreach (var x in xenotypes ?? Array.Empty<string>()) r.xenotypes.Add(x);
        return r;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits, phase 4: " + what);
        }
        bool Near(float a, float b, float eps = 0.002f) => Math.Abs(a - b) <= eps;
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- The head, from a cut-out's alpha ----------------
        {
            int w = 400, h = 500, top = 60, headH = 140, centre = 210;
            var m = PortraitPictureMath.MeasureHead(Figure(w, h, top, headH, centre), w, h);
            C(m.found && Near(m.top, top / (float)h, 0.005f), $"the head's top: the first row of the person ({m})");
            float neckAt = (top + headH) / (float)h;
            C(m.neck >= neckAt - 0.01f && m.neck <= neckAt + headH / 5f / h + 0.01f, $"the neck: the narrowest row under the head, before the shoulders ({m}, the neck from {neckAt:0.000})");
            C(Near(m.centre, (centre + 0.5f) / w, 0.01f), $"the head's middle across ({m})");
            var hair = PortraitPictureMath.MeasureHead(Figure(w, h, top, headH, centre, true), w, h);
            C(hair.found && Near(hair.neck - hair.top, PortraitPictureMath.HeadUsual, 0.01f), $"long hair over the shoulders hides the neck: the head taken as 0.3 of the picture under its top ({hair})");
            var speck = Figure(w, h, top, headH, centre);
            speck[5 * w + 3] = 255;
            C(Near(PortraitPictureMath.MeasureHead(speck, w, h).top, top / (float)h, 0.005f), "a speck above the head is passed over");
            var solid = Enumerable.Repeat((byte)255, w * h).ToArray();
            C(!PortraitPictureMath.MeasureHead(solid, w, h).found && !PortraitPictureMath.MeasureHead(new byte[w * h], w, h).found && !PortraitPictureMath.MeasureHead(null!, w, h).found,
                "a picture painted to its edges, an empty one, none: the head not found (the def's numbers or the usual place)");
            var lay = m.Layout(FaceTurn.Left, 0.8f);
            C(Near(lay.headV, (m.top + m.neck) / 2f) && Near(lay.headH, m.neck - m.top) && lay.faces == FaceTurn.Left && Near(lay.Shape, 0.8f), "the measure as the portraits' layout, with the picture's shape");
        }

        // ---------------- The large portrait's shape (phase 5: the whole picture) ----------------
        {
            C(Near(PortraitPictureMath.TallFor(0.8f), 1.25f) && Near(PortraitPictureMath.TallFor(1f), 1f) && Near(PortraitPictureMath.TallFor(1.5f), 1f)
              && Near(PortraitPictureMath.TallFor(0.3f), PortraitPictureMath.TallMost) && Near(PortraitPictureMath.TallFor(float.NaN), 1f) && Near(PortraitPictureMath.TallFor(0f), 1f),
                "the large portrait takes a 1024 x 1280 picture's shape (5:4 tall); never shorter than square, never too tall; square where the shape isn't known");
            C(Near(PortraitPictureMath.PanelHeight(192f, 1.25f, 3f), 186f * 1.25f + 6f) && Near(PortraitPictureMath.PanelHeight(192f, 1f, 3f), 192f),
                "its height: the inside at the picture's shape, the border round it; the game's drawing square as before");
            var scene = new FaceMath.PaneScene { screenW = 1920f, screenH = 1080f, pane = new FrameRect(0f, 900f, 432f, 180f), paneTop = 900f, tabs = true, gizmoTop = 1080f };
            C(FaceMath.Place(scene, 192f, out var sq) == FaceMath.Placed.OnPane && Near(sq.w, 192f) && Near(sq.h, 192f) && Near(sq.y + sq.h, 870f),
                "the game's drawing: square, standing on the tab buttons");
            C(FaceMath.Place(scene, 192f, out var tl, 1.25f) == FaceMath.Placed.OnPane && Near(tl.w, 192f) && Near(tl.h, 186f * 1.25f + 6f) && Near(tl.y + tl.h, 870f),
                "a painted picture: the same width, taller, its foot on the tab buttons");
            var low = scene;
            low.pane = new FrameRect(0f, 400f, 432f, 680f);
            low.paneTop = 400f;
            C(FaceMath.Place(low, 360f, out var lw, 1.25f) == FaceMath.Placed.OnPane && lw.y >= FaceMath.TopClear - 0.01f && lw.w < 360f,
                $"where the room above is short, narrower so its height fits under the room kept clear ({lw.w} x {lw.h})");
            PortraitPictureMath.BackdropPart(1024f, 1280f, 0.8f, out float bsx, out float bsy, out float box, out float boy);
            PortraitPictureMath.BackdropPart(1024f, 1280f, 1f, out float qsx, out float qsy, out _, out float qoy);
            PortraitPictureMath.BackdropPart(1600f, 1000f, 0.8f, out float wsx, out float wsy, out float wox, out _);
            C(Near(bsx, 1f) && Near(bsy, 1f) && Near(box, 0f) && Near(boy, 0f) && Near(qsx, 1f) && Near(qsy, 0.8f) && Near(qoy, 0.1f) && Near(wsx, 0.5f) && Near(wsy, 1f) && Near(wox, 0.25f),
                "the backdrop fills the portrait's shape from its middle: whole where the shapes agree, a band across or down where they don't");
            // No gap under the body: the half-clear last rows a background remover leaves carried down solid.
            int fw = 6, fh = 100;
            var foot = Rgba(fw, fh, (x, y) => ((byte)(100 + x), (byte)y, 50, (byte)(x < 2 ? 0 : x == 5 ? (y < 50 ? 255 : 0) : y < 98 ? 255 : y == 98 ? 120 : 60)));
            PortraitPictureMath.SolidFoot(foot, fw, fh);
            byte A(int x, int y) => foot[(y * fw + x) * 4 + 3];
            byte Gr(int x, int y) => foot[(y * fw + x) * 4 + 1];
            C(A(2, 98) == 255 && A(2, 99) == 255 && A(4, 99) == 255 && Gr(3, 99) == 97 && Gr(3, 98) == 97 && Gr(3, 96) == 96,
                "the body's last rows made solid down to the foot, in the colour of its lowest solid row");
            C(A(0, 99) == 0 && A(1, 80) == 0 && A(5, 99) == 0 && A(5, 51) == 0, "the ground beside the body left clear (a column clear at the foot, or whose body ends well above it)");
            var src4 = Rgba(4, 3, (x, y) => ((byte)(10 * x), (byte)(10 * y), 7, 255));
            var flip = PortraitPictureMath.FlipRows(src4, 4, 3);
            C(flip[1] == 20 && flip[(2 * 4) * 4 + 1] == 0 && PortraitPictureMath.FlipRows(flip, 4, 3).SequenceEqual(src4), "the rows turned over, and back");
        }

        // ---------------- Scaling and the clear edge ----------------
        {
            C(PortraitPictureMath.LongSideFor(1f) == 900 && PortraitPictureMath.LongSideFor(1.4f) == 1260 && PortraitPictureMath.LongSideFor(2.5f) == 1280 && PortraitPictureMath.LongSideFor(0.5f) == 640
              && PortraitPictureMath.ShrinkSharp(900, 1125, 300, 375) && !PortraitPictureMath.ShrinkSharp(360, 450, 352, 440) && !PortraitPictureMath.ShrinkSharp(300, 375, 400, 500),
                "kept at twice what the largest portrait needs: 900 pixels long at the interface's own scale, at least 640, at most 1280; a part shrunk by more than a twentieth shrunk on the processor, sharp");
            PortraitPictureMath.FitSize(1024, 1280, 450, out int fw, out int fh);
            PortraitPictureMath.FitSize(300, 200, 450, out int sw, out int sh);
            C(fw == 360 && fh == 450 && sw == 300 && sh == 200, $"a 1024 x 1280 picture kept at {fw} x {fh}; a small one never made larger");
            // Half a picture clear (its colour black), half red: scaled down, the red stays red to its edge.
            var half = Rgba(8, 8, (x, y) => x < 4 ? ((byte)0, (byte)0, (byte)0, (byte)0) : ((byte)200, (byte)30, (byte)20, (byte)255));
            var small = PortraitPictureMath.Downscale(half, 8, 8, 3, 3);
            int mid = (1 * 3 + 1) * 4;
            C(small[mid] == 200 && small[mid + 1] == 30 && small[mid + 3] > 60 && small[mid + 3] < 200 && small[(1 * 3 + 0) * 4 + 3] == 0,
                "scaled down by area, the colour weighted by alpha: an edge pixel half clear keeps the person's red, never darkened by the clear black");
            var edge = Rgba(6, 6, (x, y) => x >= 3 ? ((byte)200, (byte)30, (byte)20, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)0));
            PortraitPictureMath.Bleed(edge, 6, 6, 2);
            C(edge[(0 * 6 + 2) * 4] == 200 && edge[(0 * 6 + 1) * 4] == 200 && edge[(0 * 6 + 0) * 4] == 0 && edge[(0 * 6 + 2) * 4 + 3] == 0,
                "the clear edge given the person's colour two pixels deep, its alpha left clear");
        }

        // ---------------- Day and night ----------------
        {
            C(PortraitPictureMath.LightStep(1f) == 0 && PortraitPictureMath.LightStep(0.5f) == 1 && PortraitPictureMath.LightStep(0.3f) == 2 && PortraitPictureMath.LightStep(0.05f) == 3,
                "the light in four steps: daylight, dusk, dim, dark");
            C(PortraitPictureMath.LightStep(0.68f, 0) == 0 && PortraitPictureMath.LightStep(0.64f, 0) == 1 && PortraitPictureMath.LightStep(0.72f, 1) == 1,
                "a step kept until the light is clearly past its edge (no picture made again for a flicker)");
            PortraitPictureMath.LightTint(0, out float r0, out float g0, out float b0);
            PortraitPictureMath.LightTint(3, out float r3, out float g3, out float b3);
            PortraitPictureMath.LightTint(2, out float r2, out _, out _);
            C(r0 == 1f && g0 == 1f && b0 == 1f && r3 < r2 && r2 < r0 && r3 < b3 && g3 < b3, "each step darker, and cooler (red and green down more than blue); daylight untouched");
        }

        // ---------------- The compose maths ----------------
        {
            int side = 40;
            var grey = Rgba(side, side, (x, y) => ((byte)160, (byte)120, (byte)80, (byte)255));
            var painted = (byte[])grey.Clone();
            PortraitComposeMath.Backdrop(painted, side, PortraitComposeMath.LookOf(PortraitBackdropMode.AsPainted), 1f, 1f, 1f);
            C(painted.SequenceEqual(grey), "as painted: the backdrop as it is");
            var light = (byte[])grey.Clone();
            PortraitComposeMath.Backdrop(light, side, PortraitComposeMath.LookOf(PortraitBackdropMode.LightlySoftened), 1f, 1f, 1f);
            var soft = (byte[])grey.Clone();
            PortraitComposeMath.Backdrop(soft, side, PortraitComposeMath.LookOf(PortraitBackdropMode.Softened), 1f, 1f, 1f);
            int centre = (side / 2 * side + side / 2) * 4, corner = 0;
            float lum = 0.299f * 160 + 0.587f * 120 + 0.114f * 80;
            C(Near(light[centre], (lum + (160 - lum) * 0.92f) * 0.86f, 1.5f) && light[corner] < light[centre], $"lightly softened: 86% brightness, 92% colour in the middle ({light[centre]}), a little darker at the corners ({light[corner]})");
            C(soft[centre] < light[centre] && soft[corner] < soft[centre] && soft[centre] - soft[centre + 2] < light[centre] - light[centre + 2], "softened: darker, less colourful, darker corners");
            var lb = PortraitComposeMath.LookOf(PortraitBackdropMode.LightlySoftened);
            var sb = PortraitComposeMath.LookOf(PortraitBackdropMode.Softened);
            C(Near(lb.blur, 0.003f) && Near(lb.brightness, 0.86f) && Near(lb.colour, 0.92f) && Near(sb.blur, 0.012f) && Near(sb.brightness, 0.62f) && Near(sb.colour, 0.75f),
                "the settings' numbers: a blur of 0.3% and 1.2% of the portrait, 86% and 62% brightness, 92% and 75% colour");
            var stripes = Rgba(side, side, (x, y) => x % 2 == 0 ? ((byte)255, (byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)255));
            PortraitComposeMath.BlurRgb(stripes, side, side, 2);
            C(Math.Abs(stripes[centre] - 128) < 20, "the blur evens out fine stripes");
            var night = (byte[])grey.Clone();
            PortraitPictureMath.LightTint(3, out float nr, out float ng, out float nb);
            PortraitComposeMath.Backdrop(night, side, PortraitComposeMath.LookOf(PortraitBackdropMode.AsPainted), nr, ng, nb);
            C(night[centre] < grey[centre] && night[centre + 2] < grey[centre + 2], "at night the backdrop darker");
            // A person: a disc in the middle of the square, clear around.
            var person = Rgba(side, side, (x, y) => (x - 20) * (x - 20) + (y - 20) * (y - 20) <= 100 ? ((byte)200, (byte)150, (byte)120, (byte)255) : ((byte)90, (byte)60, (byte)40, (byte)0));
            var off = PortraitComposeMath.PersonLayer(person, side, PortraitComposeMath.LookOf(PortraitOutlineMode.Off));
            C(off.SequenceEqual(person), "with no line, the person as cut out (their clear edge's colour kept)");
            var subtle = PortraitComposeMath.PersonLayer(person, side, PortraitComposeMath.LookOf(PortraitOutlineMode.Subtle));
            var strong = PortraitComposeMath.PersonLayer(person, side, PortraitComposeMath.LookOf(PortraitOutlineMode.Stronger));
            int At(int x, int y) => (y * side + x) * 4;
            C(subtle[At(20, 20)] == 200 && subtle[At(20, 20) + 3] == 255, "inside the person: unchanged");
            int ink = At(31, 20), far = At(39, 2);
            C(subtle[ink + 3] > 0 && subtle[ink] < 120 && strong[ink + 3] >= subtle[ink + 3], $"just outside the person: the dark line (alpha {subtle[ink + 3]}, firmer with the stronger setting {strong[ink + 3]})");
            C(subtle[far + 3] < 40, "far from the person: clear or nearly");
            // On a portrait of 200 pixels (the halo reaches 2.2% of it): a disc of 50, the halo a few pixels past the line.
            int big = 200;
            var disc = Rgba(big, big, (x, y) => (x - 100) * (x - 100) + (y - 100) * (y - 100) <= 2500 ? ((byte)200, (byte)150, (byte)120, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)0));
            var lined = PortraitComposeMath.PersonLayer(disc, big, PortraitComposeMath.LookOf(PortraitOutlineMode.Subtle));
            int AtB(int x, int y) => (y * big + x) * 4;
            int haloAt = AtB(100, 100 - 50 - 5), farB = AtB(100, 100 - 50 - 14);
            C(lined[haloAt + 3] > 0 && lined[haloAt + 3] < 120 && lined[haloAt] < 40 && lined[farB + 3] == 0,
                $"a soft dark halo a few pixels out (alpha {lined[haloAt + 3]}), gone further out");
            int paleAt = AtB(100, 100 - 50 - 2), inkAt = AtB(100, 100 - 50 - 1);
            C(lined[paleAt] > lined[inkAt] && lined[inkAt + 3] > lined[haloAt + 3], $"the pale edge outside the ink line ({lined[paleAt]} brighter than {lined[inkAt]}), the line firmer than the halo");
            PortraitComposeMath.TintToward(0.9f, 0.6f, 0.3f, out float tr, out float tg, out float tb);
            PortraitComposeMath.TintToward(0.5f, 0.5f, 0.5f, out float gr, out float gg, out float gb);
            C(tr > 1f && tb < 1f && Near(gr, 1f) && Near(gg, 1f) && Near(gb, 1f) && tr < 1.25f, "the room's light: a warm one warms the person a little (a fifth of the way), a grey one not at all");
            var tinted = PortraitComposeMath.PersonLayer(person, side, PortraitComposeMath.LookOf(PortraitOutlineMode.Off), tr, tg, tb);
            C(tinted[At(20, 20)] > 200 && tinted[At(20, 20) + 2] < 120, "the person tinted toward a warm light");
            float r = 0, g = 0, b = 0, a = 0;
            PortraitComposeMath.Over(ref r, ref g, ref b, ref a, 100, 100, 100, 0.5f);
            PortraitComposeMath.Over(ref r, ref g, ref b, ref a, 200, 0, 0, 0.5f);
            C(Near(a, 0.75f) && Near(r, (200 * 0.5f + 100 * 0.25f) / 0.75f, 0.01f), "one colour over another, straight alpha");
            PortraitComposeMath.Average(Rgba(2, 1, (x, y) => x == 0 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)255, (byte)0)), out float ar, out _, out float ab);
            C(Near(ar, 1f) && Near(ab, 0f), "the average colour by alpha");
        }

        // ---------------- The matcher: pins ----------------
        {
            var N = (string? nick, string shown, string? first, string? last, string full) => new PortraitLibraryMath.PinNames(nick, shown, first, last, full);
            // The player's colonists as their save has them (the toddler Luna among them; Wyninn Trorgakoc has no nickname and is shown as Trorgakoc).
            var colonists = new List<KeyValuePair<int, PortraitLibraryMath.PinNames>>
            {
                new(101, N("Avila", "Avila", "Rafael", "Avila", "Rafael 'Avila' Avila")), new(102, N("Soot", "Soot", "Ilsa", "Vey", "Ilsa 'Soot' Vey")),
                new(103, N("Ape", "Ape", "Marga", "Holt", "Marga 'Ape' Holt")), new(104, N(null, "Trorgakoc", "Wyninn", "Trorgakoc", "Wyninn Trorgakoc")),
                new(105, N(null, "Abyssangler", "Lorena", "Abyssangler", "Lorena Abyssangler")), new(106, N(null, "Tocac", "Darunia", "Tocac", "Darunia Tocac")),
                new(107, N("Webb", "Webb", "Tom", "Webb", "Tom 'Webb' Webb")), new(108, N("Bautista", "Bautista", "Juan", "Rios", "Juan 'Bautista' Rios")),
                new(109, N(null, "Dholuccac", "Ettrian", "Dholuccac", "Ettrian Dholuccac")), new(110, N(null, "Gomez", "Guido", "Gomez", "Guido Gomez")),
                new(111, N("Luna", "Luna", "Luna", "Avila", "Luna Avila"))
            };
            foreach (var (pinName, id) in new[]
                     {
                         ("Avila", 101), ("Soot", 102), ("Ape", 103), ("Wyninn Trorgakoc", 104), ("Trorgakoc", 104), ("Lorena Abyssangler", 105), ("Darunia Tocac", 106), ("Webb", 107),
                         ("Bautista", 108), ("Ettrian Dholuccac", 109), ("Guido Gomez", 110), ("Luna", 111), ("luna", 111), (" Guido ", 110)
                     })
                C(PortraitLibraryMath.PinOwner(pinName, colonists) == id, $"the pin '{pinName}' is colonist {id}'s");
            C(PortraitLibraryMath.PinOwner("Avila", colonists) == 101, "a pin naming one colonist's nickname and another's last name (Luna Avila): the nickname first");
            C(PortraitLibraryMath.PinOwner("Nobody", colonists) == -1 && PortraitLibraryMath.PinOwner("", colonists) == -1, "a pin naming no one: no one's (the picture stays unused)");
            var twins = new List<KeyValuePair<int, PortraitLibraryMath.PinNames>> { new(9, N(null, "Ann", "Ann", "Lee", "Ann Lee")), new(4, N(null, "Ann", "Ann", "Ray", "Ann Ray")) };
            C(PortraitLibraryMath.PinOwner("Ann", twins) == 4, "two named alike: the one longer in the game (the lower id)");
            C(PortraitLibraryMath.PinLevel("Trorgakoc", N(null, "Trorgakoc", "Wyninn", "Trorgakoc", "Wyninn Trorgakoc")) == 1, "the name on the colonist bar counts before the first and last name");

            var avila = Rec("Portrait_colonists_Avila_01", PortraitSex.Male, PortraitAge.Young, "#c9a27e", "#1a1a1a", "Avila", "Avila");
            var lunaToddler = Rec("Portrait_colonists_Luna_01", PortraitSex.Female, PortraitAge.Toddler, "#e8c8a8", "#3a2a1a", "Luna", "Luna");
            var stranger = Rec("Portrait_m_0001", PortraitSex.Male, PortraitAge.Adult, "#c9a27e", "#1a1a1a");
            var lib = new List<PortraitRecord> { avila, lunaToddler, stranger };
            var avilaLook = Look(PortraitSex.Male, PortraitAge.Adult, "#c9a27e", "#1a1a1a");
            Func<PortraitRecord, bool> AvilaOwns = r => r.pawn == "Avila", LunaOwns = r => r.pawn == "Luna", NoneOwn = r => false;
            var pin = PortraitLibraryMath.PinFor(avilaLook, lib, AvilaOwns);
            C(pin.Any && lib[pin.index] == avila, "a colonist's pinned picture is theirs, ahead of the matcher (a young picture for one the game counts adult)");
            C(PortraitLibraryMath.PinFit(avila, Look(PortraitSex.Male, PortraitAge.Middle)) == 2 && PortraitLibraryMath.PinFit(avila, Look(PortraitSex.Male, PortraitAge.Old)) == 1
              && PortraitLibraryMath.PinFit(avila, Look(PortraitSex.Male, PortraitAge.Teen)) == 1 && !PortraitLibraryMath.PinFits(avila, Look(PortraitSex.Male, PortraitAge.Child)),
                "a young pin fits young, adult and middle-aged alike; a teen or someone grown old as a stand-in (their own face before a stranger's); never a child");
            var avilaOld = Rec("Portrait_colonists_Avila_old", PortraitSex.Male, PortraitAge.Old, "#c9a27e", "#9a9a9a", "Avila", "Avila");
            var withOld = new List<PortraitRecord> { avila, avilaOld, stranger };
            var oldAvila = Look(PortraitSex.Male, PortraitAge.Elder, "#c9a27e", "#9a9a9a");
            var po = PortraitLibraryMath.PinFor(oldAvila, withOld, AvilaOwns);
            C(po.Any && withOld[po.index] == avilaOld && PortraitLibraryMath.PinFor(oldAvila, lib, AvilaOwns).index == 0,
                "grown old: their old pin where there is one, else their younger one stands in");
            var lookAlike = Look(PortraitSex.Male, PortraitAge.Young, "#c9a27e", "#1a1a1a");
            var youngStranger = Rec("Portrait_m_0002", PortraitSex.Male, PortraitAge.Young, "#c9a27e", "#1a1a1a");
            var alikeLib = new List<PortraitRecord> { avila, youngStranger };
            var other = PortraitLibraryMath.Pick(lookAlike, alikeLib, 5, _ => 0);
            C(other.Any && alikeLib[other.index] == youngStranger && float.IsInfinity(PortraitLibraryMath.Distances(lookAlike, alikeLib)[0]),
                "a pinned picture is never given to anyone else, however alike: the matcher never sees it");
            C(PortraitLibraryMath.PinFor(lookAlike, lib, NoneOwn).index < 0, "someone the pins don't name: no pin");
            var lunaNow = Look(PortraitSex.Female, PortraitAge.Toddler, "#e8c8a8", "#3a2a1a");
            var lunaLater = Look(PortraitSex.Female, PortraitAge.Child, "#e8c8a8", "#3a2a1a");
            C(PortraitLibraryMath.PinFor(lunaNow, lib, LunaOwns).Any && !PortraitLibraryMath.PinFor(lunaLater, lib, LunaOwns).Any,
                "Luna's toddler picture is hers while she is a toddler, not once she is a child");
            var lunaChild = Rec("Portrait_colonists_Luna_child", PortraitSex.Female, PortraitAge.Child, "#e8c8a8", "#3a2a1a", "Luna", "Luna");
            var lib2 = new List<PortraitRecord>(lib) { lunaChild };
            var later = PortraitLibraryMath.PinFor(lunaLater, lib2, LunaOwns);
            C(later.Any && lib2[later.index] == lunaChild && PortraitLibraryMath.PickAgain(true, PortraitStage.Toddler, PortraitStage.Child, "Portrait_colonists_Luna_01", true, 0, false, 1, 1, "w", "w2", 2),
                "grown into a child: picked again, her pin for the new stage");
            C(PortraitLibraryMath.PinFor(Look(PortraitSex.Female, PortraitAge.Baby), lib2, LunaOwns).index < 0, "a baby's stage: no toddler's pin (pins keep to their own stage)");
        }

        // ---------------- How old a person looks ----------------
        {
            PortraitAge Looks(float bio, float born = float.NaN, float life = 80f, float span = 1f, GameStage stage = GameStage.Grown, bool ageless = false) =>
                PortraitLibraryMath.AgeOf(bio, float.IsNaN(born) ? bio : born, life, span, stage, ageless, out var a) ? a : (PortraitAge)255;
            // An ageless elf (DevDesigner_Ageless: its ageing stopped at about 25) by its years since birth, ten times as slow.
            C(Looks(25f, 20f, ageless: true) == PortraitAge.Young && Looks(25f, 200f, ageless: true) == PortraitAge.Young, "an ageless elf of 20 or 200 looks young");
            C(Looks(25f, 300f, ageless: true) == PortraitAge.Adult, "an ageless elf of 300 looks adult");
            C(Looks(25f, 500f, ageless: true) == PortraitAge.Middle && Looks(25f, 740f, ageless: true) == PortraitAge.Old && Looks(25f, 760f, ageless: true) == PortraitAge.Elder,
                "an ageless elf of 500 middle-aged, of 740 old, past 750 an elder (75 years of a human's, ten times over)");
            C(Looks(62f, 100f, ageless: true) == PortraitAge.Old, "an ageless pawn never looks younger than its biological age reads");
            C(PortraitLibraryMath.AgelessLifeScale == 10f, "the ageless scale: ten times their kind's life");
            C(Looks(60f, 120f) == PortraitAge.Old, "a slow-ageing 120-year-old with a biological age of 60 reads by it: old, not an elder");
            C(Looks(150f, 150f, 400f) == PortraitAge.Adult, "a race living 400 years, at 150 biologically: adult, not an elder");
            C(Looks(62f, 40f) == PortraitAge.Old, "a fast-ageing 40-year-old human with a biological age of 62 reads by it: old");
            C(Looks(150f, 150f, 80f, 5f) == PortraitAge.Adult && Looks(70f, 70f, 80f, 0.5f) == PortraitAge.Elder,
                "the life span factor of their genes (a long life five times a human's, a short one half) scales the grown-ups' bands");
            C(Looks(2f, 2f, 400f, 1f, GameStage.Baby) == PortraitAge.Toddler && Looks(30f, 30f, 400f, 1f, GameStage.Child) == PortraitAge.Child
              && Looks(40f, 40f, 400f, 1f, GameStage.Teen) == PortraitAge.Teen, "the young ones by the game's own stages of life, however long their kind lives");
        }

        // ---------------- The matcher: the closest, and the little ones ----------------
        {
            // Far on its hair's colour, its skin in the same tone or the next (a darker skin would not stand for them at all).
            var far = Rec("far", PortraitSex.Male, PortraitAge.Adult, "#e4c699", "#e6e6e2");
            var fair = Look(PortraitSex.Male, PortraitAge.Adult, "#f2ede0", "#594030");
            var p = PortraitLibraryMath.Pick(fair, new List<PortraitRecord> { far }, 1, _ => 0);
            C(p.Any && p.distance > PortraitLibraryMath.CloseEnough, $"the closest: a picture far from their looks still theirs ({p.distance:0.0})");
            C(PortraitLibraryMath.Shown(PortraitPickMode.Closest, false, p.distance) && !PortraitLibraryMath.Shown(PortraitPickMode.OnlyClose, false, p.distance)
              && PortraitLibraryMath.Shown(PortraitPickMode.OnlyClose, true, p.distance), "only close ones: a far one not shown, unless pinned or chosen by hand");
            var woman = Rec("woman", PortraitSex.Female, PortraitAge.Adult);
            C(!PortraitLibraryMath.Pick(fair, new List<PortraitRecord> { woman }, 1, _ => 0).Any, "the closest: the sex must agree");
            C(PortraitLibraryMath.AgeCost(PortraitAge.Young, PortraitAge.Young) == 0f && float.IsInfinity(PortraitLibraryMath.AgeCost(PortraitAge.Young, PortraitAge.Adult))
              && float.IsInfinity(PortraitLibraryMath.AgeCost(PortraitAge.Baby, PortraitAge.Toddler)) && float.IsInfinity(PortraitLibraryMath.AgeCost(PortraitAge.Child, PortraitAge.Teen))
              && float.IsInfinity(PortraitLibraryMath.AgeCost(PortraitAge.Old, PortraitAge.Elder)),
                "the own band only (the player, 23 November 2026): never the next, not even within a stage");
            var old = Rec("old", PortraitSex.Male, PortraitAge.Old);
            var young = Look(PortraitSex.Male, PortraitAge.Young);
            C(!PortraitLibraryMath.Pick(young, new List<PortraitRecord> { old }, 1, _ => 0).Any, "the closest: a young man never gets an old man's picture, even where there is no other");
            var baby = Look(PortraitSex.Female, PortraitAge.Baby);
            var babies = new List<PortraitRecord> { Rec("girl", PortraitSex.Female, PortraitAge.Child), Rec("woman", PortraitSex.Female, PortraitAge.Young), Rec("toddler", PortraitSex.Female, PortraitAge.Toddler) };
            C(!PortraitLibraryMath.Pick(baby, babies, 1, _ => 0).Any, "a baby never gets a toddler's picture: with no baby's picture, the game's own face");
            babies.Add(Rec("baby", PortraitSex.Female, PortraitAge.Baby));
            var pb = PortraitLibraryMath.Pick(baby, babies, 1, _ => 0);
            C(pb.Any && babies[pb.index].name == "baby", "a baby gets a baby's picture");
            var saurid = Look(PortraitSex.Female, PortraitAge.Adult, "#9aa0a0", "#40a0a0", "vresaurids_saurid", new[] { "scales" });
            C(!PortraitLibraryMath.Pick(saurid, new List<PortraitRecord> { woman }, 1, _ => 0).Any, "a saurid with no saurid pictures is never shown as a human");
            var sauridPic = Rec("soot", PortraitSex.Female, PortraitAge.Adult, "#9aa0a0", "#40a0a0", xenotypes: new[] { "vresaurids_saurid" });
            C(PortraitLibraryMath.Pick(saurid, new List<PortraitRecord> { woman, sauridPic }, 1, _ => 0).index == 1, "with saurid pictures, a saurid gets one");
            // A colonist's picture is theirs alone (the player: "once a pawn is part of my colony, their image should no longer
            // be used for anyone else ... replace anyone else who already has that image").
            var owners = PortraitLibraryMath.Owners(new[] { (7, "face_a", 500), (3, "face_a", 900), (9, "face_b", 100), (4, "face_b", 100) },
                new[] { new KeyValuePair<string, int>("Avila", 101), new KeyValuePair<string, int>("Nobody", -1) });
            C(owners["face_a"] == 7 && owners["face_b"] == 4, "of two colonists with the same face, the one who had it first keeps it (then the lower id)");
            C(owners["Avila"] == 101 && owners["Nobody"] == -1, "a pinned face is its colonist's from the start; a pin naming no one is no one else's either");
            var faces = new List<PortraitRecord> { Rec("a", PortraitSex.Male, PortraitAge.Adult, character: "face_a"), Rec("b", PortraitSex.Male, PortraitAge.Adult, "#d8b090", character: "face_b") };
            var stranger2 = Look(PortraitSex.Male, PortraitAge.Adult);
            var free = PortraitLibraryMath.Pick(stranger2, faces, 1, _ => 0, c => owners.TryGetValue(c, out int o) && o != 55);
            C(!free.Any, "a stranger is never given a colonist's face, even the closest; with none left, the game's own drawing");
            var free2 = PortraitLibraryMath.Pick(stranger2, faces, 1, _ => 0, c => c == "face_a");
            C(free2.Any && faces[free2.index].Character == "face_b", "the closest face no colonist holds");
            C(PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitStage.Grown, "a", true, 0, true, 1, 1, "w", "w", 0, true)
              && !PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitStage.Grown, "a", true, 0, true, 1, 1, "w", "w", 0, false),
                "someone holding a face a colonist now owns gets a new pick at once (even one chosen by hand); otherwise kept");
            var alikeUsed = new List<PortraitRecord> { Rec("near", PortraitSex.Male, PortraitAge.Adult), Rec("bit", PortraitSex.Male, PortraitAge.Adult, "#d2a688") };
            var fresh = PortraitLibraryMath.Pick(stranger2, alikeUsed, 3, c => c == "near" ? 1 : 0);
            C(fresh.Any && alikeUsed[fresh.index].name == "bit" && PortraitLibraryMath.Distance(stranger2, alikeUsed[1]) <= PortraitLibraryMath.FewWithin,
                "a face nobody holds yet before a used one among the near-closest (few repeats)");
            var takenChoices = PortraitLibraryMath.Choices(stranger2, faces, r => false, PortraitPickMode.Closest, c => c == "face_a");
            C(takenChoices.Count == 1 && faces[takenChoices[0]].Character == "face_b", "choosing another picture never offers a colonist's face");
            // Choose another picture: their pins first, then the nearest faces, round and round.
            var me = Rec("mine", PortraitSex.Male, PortraitAge.Adult, pawn: "Avila");
            var a1 = Rec("a1", PortraitSex.Male, PortraitAge.Adult);
            var a2 = Rec("a2", PortraitSex.Male, PortraitAge.Adult, "#c49a6c");
            var all = new List<PortraitRecord> { a2, me, a1, far };
            Func<PortraitRecord, bool> mine = r => r.pawn == "Avila";
            var choices = PortraitLibraryMath.Choices(Look(PortraitSex.Male, PortraitAge.Adult), all, mine, PortraitPickMode.Closest);
            C(choices.Select(i => all[i].name).SequenceEqual(new[] { "mine", "a1", "a2", "far" }), $"the choices: their pin, then the faces nearest first ({string.Join(", ", choices.Select(i => all[i].name))})");
            var close = PortraitLibraryMath.Choices(Look(PortraitSex.Male, PortraitAge.Adult), all, mine, PortraitPickMode.OnlyClose);
            C(!close.Select(i => all[i].name).Contains("far"), "with only close ones, no far face among the choices");
            C(all[PortraitLibraryMath.NextChoice(choices, all, "mine")].name == "a1" && all[PortraitLibraryMath.NextChoice(choices, all, "far")].name == "mine"
              && all[PortraitLibraryMath.NextChoice(choices, all, "gone")].name == "mine", "the next choice after the one shown, round to the first");
            C(PortraitLibraryMath.NextChoice(new List<int> { 2 }, all, "a1") == -1 && PortraitLibraryMath.NextChoice(new List<int>(), all, "x") == -1, "only the one shown, or none: no other");
        }

        // ---------------- Backdrops ----------------
        string fixtures = Path.Combine(root, "Tools", "Tests", "PortraitFixtures");
        var backs = new List<BackdropRecord>();
        foreach (var e in XDocument.Load(Path.Combine(fixtures, "BackdropDefs_Sample.xml")).Root!.Elements("ColonyCouncil.BackdropDef"))
        {
            var problems = new List<string>();
            string? F(string name) => e.Element(name)?.Value;
            IList<string>? L(string name) => e.Element(name)?.Elements("li").Select(x => x.Value).ToList();
            var r = BackdropMath.Record(F("defName"), F, L, problems);
            C(problems.Count == 0, $"{r.name}: read without a problem ({string.Join("; ", problems)})");
            backs.Add(r);
        }
        {
            var problems = new List<string>();
            var fields = new Dictionary<string, string> { ["place"] = "attic", ["outdoors"] = "moon", ["era"] = "bronze" };
            var lists = new Dictionary<string, IList<string>> { ["contents"] = new[] { "bed", "piano" } };
            var bad = BackdropMath.Record("Bad", k => fields.TryGetValue(k, out var v) ? v : null, k => lists.TryGetValue(k, out var v) ? v : null, problems);
            foreach (var want in new[] { "texPath", "attic", "moon", "bronze", "piano" }) C(problems.Any(p => p.Contains(want)), $"a backdrop def's problem told: {want}");
            C(bad.contents.SetEquals(new[] { "bed" }), "a backdrop's unknown words left out, the rest kept");
            var roomless = new List<string>();
            BackdropMath.Record("R", k => k == "place" ? "room" : k == "texPath" ? "x" : null, _ => null, roomless);
            C(roomless.Any(p => p.Contains("rooms")), "a room backdrop with no room roles is told");
        }
        string Pick(BackdropWhere w) => BackdropMath.Choose(w, backs) is var c && c.index >= 0 ? backs[c.index].name : "none";
        BackdropWhere Room(string role, PortraitEra era, int seed = 1, params string[] contents) =>
            new BackdropWhere { place = BackdropPlace.Room, role = role, era = era, seed = seed, contents = new HashSet<string>(contents) };
        C(Pick(Room("Bedroom", PortraitEra.Industrial)) == "Backdrop_room_bedroom_industrial_01", "a bedroom in an industrial colony: the industrial bedroom");
        C(Pick(Room("Bedroom", PortraitEra.Spacer)) == "Backdrop_room_bedroom_industrial_01", "a spacer colony with no spacer bedroom: the nearest age's");
        C(Pick(Room("Bedroom", PortraitEra.Tribal, 1, "bed", "hearth")) == "Backdrop_room_bedroom_medieval_01" && Pick(Room("Bedroom", PortraitEra.Medieval, 1, "bed", "table")) == "Backdrop_room_bedroom_medieval_02",
            "of two medieval bedrooms, the one showing more of what is in the room (a hearth, a table)");
        var spread = Enumerable.Range(1, 40).Select(s => Pick(Room("Bedroom", PortraitEra.Medieval, s, "bed"))).Distinct().ToList();
        C(spread.Count == 2 && Enumerable.Range(0, 5).All(_ => Pick(Room("Bedroom", PortraitEra.Medieval, 7, "bed")) == Pick(Room("Bedroom", PortraitEra.Medieval, 7, "bed"))),
            "among equals one by the room's number: the same room always the same backdrop, other rooms spread over both");
        C(Pick(Room("PrisonCell", PortraitEra.Industrial)) == "Backdrop_room_prisoncell_any_01", "a backdrop for any age where it is the only one of its kind");
        C(Pick(Room("GuestRoom", PortraitEra.Medieval, 1, "bed", "hearth")) == "Backdrop_room_bedroom_medieval_01", "a guest room: the bedroom backdrops (they list it)");
        C(Pick(Room("PrisonBarracks", PortraitEra.Medieval)) == "Backdrop_room_prisoncell_any_01", "a prison barracks with no backdrop: like a prison cell");
        C(Pick(Room("VBE_Library", PortraitEra.Medieval)) == "Backdrop_room_study_medieval_01", "a library (Vanilla Books Expanded) with no backdrop: like a laboratory, the study");
        C(Pick(Room("Nursery", PortraitEra.Medieval, 3, "bed")).StartsWith("Backdrop_room_bedroom_medieval"), "a nursery: like a bedroom");
        C(Pick(Room("PrivateBathroom", PortraitEra.Medieval)).StartsWith("Backdrop_plain_"), "a bathroom: a plain ground");
        C(Pick(Room("", PortraitEra.Medieval, 1, "books", "shelves")) == "Backdrop_room_study_medieval_01", "a room of no particular kind: by what is in it (books and shelves, the study)");
        C(Pick(Room("", PortraitEra.Medieval)).StartsWith("Backdrop_plain_"), "a room of no particular kind with nothing a backdrop shows: a plain ground");
        BackdropWhere Out(string g) => new BackdropWhere { place = BackdropPlace.Outdoors, group = g, seed = 2 };
        C(Pick(Out("temperateForest")) == "Backdrop_out_temperateForest_01" && Pick(Out("desert")) == "Backdrop_out_desert_01" && Pick(Out("cave")) == "Backdrop_out_cave_01",
            "outdoors: the land's own group");
        C(Pick(Out("borealForest")) == "Backdrop_out_temperateForest_01" && Pick(Out("shrubland")) == "Backdrop_out_desert_01", "a group with no backdrop: a near group's");
        C(Pick(Out("space")).StartsWith("Backdrop_plain_") && Pick(new BackdropWhere()).StartsWith("Backdrop_plain_"), "space with no backdrop, and no map (a caravan): a plain ground");
        C(BackdropMath.Choose(new BackdropWhere(), backs.Where(b => b.place != BackdropPlace.Plain).ToList()).index == -1 && BackdropMath.Choose(Out("desert"), new List<BackdropRecord>()).index == -1,
            "no plain ground, or no backdrops at all: none (the portrait's own ground)");
        C(new BackdropWhere { place = BackdropPlace.Room, role = "Bedroom", contents = { "bed" } }.Key != new BackdropWhere { place = BackdropPlace.Room, role = "Bedroom", contents = { "bed", "hearth" } }.Key,
            "what decides the backdrop, as a key: chosen again when what is in the room changes");
        // The player's biomes and rooms (docs/player-mods, active rows).
        var biomes = new List<string>();
        var roles = new HashSet<string>();
        string defs = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        if (File.Exists(defs))
        {
            using var gz = new GZipStream(File.OpenRead(defs), CompressionMode.Decompress);
            using var rd = new StreamReader(gz);
            string? line;
            while ((line = rd.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length < 4 || c[3] != "yes") continue;
                if (c[0] == "BiomeDef") biomes.Add(c[1]);
                else if (c[0] == "RoomRoleDef") roles.Add(c[1]);
            }
        }
        C(biomes.Count == 56 && biomes.All(b => BackdropMath.BiomeGroups.ContainsKey(b)), $"every one of the player's {biomes.Count} active biomes has a group of land ({string.Join(", ", biomes.Where(b => !BackdropMath.BiomeGroups.ContainsKey(b)))})");
        C(BackdropMath.BiomeGroups.Keys.All(b => biomes.Contains(b)), $"every biome in the table is one of the player's active ones ({string.Join(", ", BackdropMath.BiomeGroups.Keys.Where(b => !biomes.Contains(b)))})");
        C(BackdropMath.BiomeGroups.Values.All(g => BackdropMath.Groups.Contains(g)) && BackdropMath.Groups.All(g => BackdropMath.GroupFallbacks.ContainsKey(g))
          && BackdropMath.GroupFallbacks.Values.All(v => v.All(g => BackdropMath.Groups.Contains(g))), "the groups and their near groups are the sixteen words");
        C(BackdropMath.BiomeGroups["AB_MycoticJungle"] == "jungle" && BackdropMath.BiomeGroups["RG_AridWasteland"] == "wasteland" && BackdropMath.BiomeGroups["LavaField"] == "volcanic"
          && BackdropMath.BiomeGroups["Space"] == "space" && BackdropMath.BiomeGroups["Orbit"] == "space" && BackdropMath.BiomeGroups["Underground"] == "cave" && BackdropMath.BiomeGroups["Undercave"] == "cave",
            "the brief's examples: a mycotic jungle a jungle, an arid wasteland a wasteland, a lava field volcanic, space and orbit space, underground a cave");
        var named = BackdropMath.RoleFallbacks.Keys.Concat(BackdropMath.RoleFallbacks.Values.SelectMany(v => v)).Distinct().ToList();
        C(roles.Count > 30 && named.All(r => roles.Contains(r) || r == "ThroneRoom"), $"every room role in the table is one of the player's (and Royalty's throne room) ({string.Join(", ", named.Where(r => !roles.Contains(r) && r != "ThroneRoom"))})");
        C(roles.Where(r => r != "None" && r != "Room").All(r => BackdropMath.RoleFallbacks.ContainsKey(r)), $"every one of the player's room roles has its near kinds (or none: a plain ground) ({string.Join(", ", roles.Where(r => r != "None" && r != "Room" && !BackdropMath.RoleFallbacks.ContainsKey(r)))})");
        string G(float t, float rain, float plants, bool extreme = false, bool vacuum = false, bool under = false) => BackdropMath.GroupOf("SomeModBiome", t, rain, plants, extreme, vacuum, under);
        C(G(45f, 100f, 0f, true) == "volcanic" && G(10f, 100f, 0f, true) == "wasteland" && G(-30f, 300f, 0f) == "ice" && G(-5f, 300f, 0.1f) == "tundra" && G(-5f, 600f, 0.6f) == "borealForest"
          && G(30f, 200f, 0.1f) == "desert" && G(12f, 300f, 0.2f) == "shrubland" && G(26f, 2600f, 1f) == "jungle" && G(10f, 2400f, 0.8f) == "swamp" && G(12f, 900f, 0.8f) == "temperateForest"
          && G(12f, 900f, 0.3f) == "grassland" && G(0f, 0f, 0f, false, true) == "space" && G(10f, 600f, 0.5f, false, false, true) == "cave",
            "an unknown biome by its own warmth, rain and plants (and extreme, vacuum, underground)");
        // What is in a room, by what the game says of a thing and its name.
        List<string> W(string name, BackdropMath.ThingIs f = BackdropMath.ThingIs.None) => BackdropMath.ContentWordsOf(name, f);
        C(W("Shelf", BackdropMath.ThingIs.Storage).SequenceEqual(new[] { "shelves" }) && W("LWM_BigShelf", BackdropMath.ThingIs.Storage).Contains("shelves"), "a shelf: shelves");
        C(W("Campfire", BackdropMath.ThingIs.WorkTable | BackdropMath.ThingIs.Fire).SequenceEqual(new[] { "hearth", "fire" }) && W("VFEM2_Hearth").SequenceEqual(new[] { "hearth", "fire" }),
            "a campfire or a hearth: a hearth and a fire (not a workbench)");
        C(W("FueledStove", BackdropMath.ThingIs.WorkTable).SequenceEqual(new[] { "stove" }) && W("ElectricStove", BackdropMath.ThingIs.WorkTable).SequenceEqual(new[] { "stove" }), "a stove: a stove (not a workbench)");
        C(W("Throne", BackdropMath.ThingIs.Throne | BackdropMath.ThingIs.Seat).Contains("throne") && W("Brazier").SequenceEqual(new[] { "fire" }) && W("TorchLamp").SequenceEqual(new[] { "fire" }),
            "a throne; a brazier or a torch, a fire");
        C(W("SimpleResearchBench", BackdropMath.ThingIs.WorkTable | BackdropMath.ThingIs.Research).SequenceEqual(new[] { "books" }) && W("TableMachining", BackdropMath.ThingIs.WorkTable).SequenceEqual(new[] { "workbench" }),
            "a research bench: books; a machining table: a workbench");
        C(W("Bed", BackdropMath.ThingIs.Bed).SequenceEqual(new[] { "bed" }) && W("DiningChair", BackdropMath.ThingIs.Seat).SequenceEqual(new[] { "chairs" }) && W("Table2x2c", BackdropMath.ThingIs.Table).SequenceEqual(new[] { "table" }),
            "a bed, a chair, a table");
        C(W("VitalsMonitor").SequenceEqual(new[] { "medicine" }) && W("MedicineHerbal", BackdropMath.ThingIs.Medicine).SequenceEqual(new[] { "medicine" }) && W("ChessTable", BackdropMath.ThingIs.Joy).Contains("games")
          && W("Column").SequenceEqual(new[] { "pillars" }) && W("ToolCabinet").SequenceEqual(new[] { "tools" }) && W("Altar_Small").SequenceEqual(new[] { "altar" }), "medicine, games, pillars, tools, an altar");
        C(BackdropMath.RoomContents(new[] { W("Bed", BackdropMath.ThingIs.Bed), W("Bed", BackdropMath.ThingIs.Bed), W("Shelf", BackdropMath.ThingIs.Storage) }).SetEquals(new[] { "bed", "beds", "shelves" })
          && !BackdropMath.RoomContents(new[] { W("Bed", BackdropMath.ThingIs.Bed) }).Contains("beds"), "two beds or more: beds");
        C(BackdropMath.ContentWordsOf("Anything", (BackdropMath.ThingIs)2047).All(w => BackdropMath.Contents.Contains(w)), "every word given is one of the content words");

        // ---------------- The library folder ----------------
        {
            var people = new List<FolderPortrait>();
            var backs2 = new List<FolderBackdrop>();
            var problems = new List<string>();
            PortraitFolderMath.Parse(File.ReadAllText(Path.Combine(fixtures, "records_sample.xml")), people, backs2, problems);
            C(people.Count == 3 && backs2.Count == 2, $"records.xml read: its people and its backdrops ({people.Count}, {backs2.Count})");
            var av = people.FirstOrDefault(p => p.record.name == "Portrait_colonists_Avila_01");
            C(av != null && av.record.pawn == "Avila" && av.texPath == "colonists/Avila_style_01_cut_00001_" && av.record.age == PortraitAge.Young && av.record.hair == PortraitHairStyle.Tied
              && float.IsNaN(av.headX), "a person's record: its pin, its path from the folder, its words; the head's place measured (none given)");
            var luna = people.FirstOrDefault(p => p.record.name == "Portrait_colonists_Luna_01");
            C(luna != null && luna.record.age == PortraitAge.Toddler && Near(luna.headY, 0.3f), "a toddler's record; the head's place where a record gives it");
            C(backs2.Any(b => b.record.place == BackdropPlace.Room && b.record.rooms.Contains("Bedroom") && b.texPath.StartsWith("backdrops/")), "a backdrop's record, its path from the folder");
            C(problems.Any(p => p.Contains("Portrait_bad") && p.Contains("wizardhat")) && problems.Any(p => p.Contains("Unknown.Thing")) && people.Any(p => p.record.name == "Portrait_bad"),
                $"an unknown word told with its record's name, the record kept; an element of no known kind told ({string.Join("; ", problems)})");
            bool threw = false;
            try
            {
                PortraitFolderMath.Parse("not xml", people, backs2, problems);
            }
            catch (Exception)
            {
                threw = true;
            }
            C(threw, "a records.xml that is no XML at all: an error the game tells once (the folder's part of the library off)");
            // The lead's batch lists (docs/portrait-art/batches): every record column the pack script writes reads without a problem.
            foreach (var (file, backdrop) in new[] { ("colonists.csv", false), ("backdrops.csv", true) })
            {
                string path = Path.Combine(root, "docs", "portrait-art", "batches", file);
                if (!File.Exists(path)) continue;
                var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
                var head = PortraitFolderMath.Cells(lines[0]);
                int rows = 0, bad = 0;
                var said = new List<string>();
                foreach (var line in lines.Skip(1))
                {
                    var cells = PortraitFolderMath.Cells(line);
                    string? Cell(string col) => head.IndexOf(col) is var at && at >= 0 && at < cells.Count ? cells[at].Trim() : null;
                    IList<string>? Words(string col) => Cell(col)?.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).ToList();
                    var own = new List<string>();
                    if (backdrop)
                        BackdropMath.Record(Cell("name"), f => f == "texPath" ? "x" : Cell(f), Words, own);
                    else
                    {
                        string? kind = Cell("kind");
                        var xenos = string.IsNullOrEmpty(kind) || kind == "Baseliner" ? new List<string>() : new List<string> { kind };
                        var rec = PortraitLibraryMath.Record(Cell("name"), f => f == "texPath" ? "x" : f == "skinColor" ? Cell("skin") : f == "hairColor" ? Cell("hairColour") : Cell(f),
                            f => f == "xenotypes" ? xenos : f == "races" ? Words("race") : Words(f), own);
                        if (!rec.Pinned) own.Add("no pin");
                    }
                    rows++;
                    if (own.Count > 0) bad++;
                    said.AddRange(own.Select(o => Cell("name") + ": " + o));
                }
                C(rows > 0 && bad == 0, $"the batch list {file}: its {rows} rows' records read as Rimfolk's words ({string.Join("; ", said.Take(6))})");
            }
            C(PortraitFolderMath.CleanFolder("  \"C:\\Users\\Me\\Google Drive\\Rimfolk Portraits\"  ") == "C:\\Users\\Me\\Google Drive\\Rimfolk Portraits" && PortraitFolderMath.CleanFolder(null!) == "",
                "a folder pasted from Windows in quotes, spaces and a drive letter kept");
            C(PortraitFolderMath.FileKey("colonists/Avila_01") == "colonists/Avila_01.png" && PortraitFolderMath.FileKey("\\backdrops\\a") == "backdrops/a.png", "a picture's path in the folder");
            // missing.csv: words for the looks, the merge, the text.
            C(PortraitFolderMath.SkinWord(Hex("#f2ede0")) == "very fair" && PortraitFolderMath.SkinWord(Hex("#402917")) == "dark" && PortraitFolderMath.SkinWord(Hex("#73a659")) == "green",
                "a skin in a word: very fair to dark, a xenotype's by its hue");
            C(PortraitFolderMath.HairWord(Hex("#1a1a1a")) == "black" && PortraitFolderMath.HairWord(Hex("#e8cc90")) == "blond" && PortraitFolderMath.HairWord(Hex("#a8481e")) == "red"
              && PortraitFolderMath.HairWord(Hex("#a6a6a6")) == "grey" && PortraitFolderMath.HairWord(Hex("#3359cc")) == "blue" && PortraitFolderMath.HairWord(Hex("#594030")) == "brown",
                "a hair colour in a word");
            PortraitNeed Need(string what, string kind, int count, string date, string why = "no pictures of their kind", string instead = "") =>
                new PortraitNeed { what = what, kind = kind, sex = "female", age = "adult", skin = "fair", hair = "black", era = "medieval", why = why, instead = instead, count = count, lastSeen = date };
            var old = new List<PortraitNeed> { Need("person", "Human/VRESaurids_Saurid", 2, "2026-11-20"), Need("room", "Barracks", 1, "2026-11-21", "no backdrop of this kind of room") };
            var fresh = new List<PortraitNeed> { Need("person", "Human/VRESaurids_Saurid", 1, "2026-11-22", instead: "x"), Need("outdoors", "AB_MycoticJungle", 1, "2026-11-22", "no backdrop for jungle") };
            var merged = PortraitFolderMath.Merge(old, fresh);
            var saurid = merged.First(n => n.kind == "Human/VRESaurids_Saurid");
            C(merged.Count == 3 && saurid.count == 3 && saurid.lastSeen == "2026-11-22" && saurid.instead == "x", "merged with the file: one line per need, the counts added, the newest date kept");
            C(merged[0].what == "person" && merged[1].what == "room" && merged[2].what == "outdoors", "people first, then rooms, then the land");
            string csv = PortraitFolderMath.Csv(merged);
            var back = PortraitFolderMath.Read(csv);
            C(csv.StartsWith("# What Rimfolk needed") && csv.Contains("what,kind,look,role,mod,sex,age,skin,hair,era,why,used instead,how many,last seen") && back.Count == 3
              && back.Select(n => n.Key).SequenceEqual(merged.Select(n => n.Key)) && back.Sum(n => n.count) == 5, "written with a plain header and read back the same");
            var comma = PortraitFolderMath.Read(PortraitFolderMath.Csv(new[] { Need("room", "VFEE_Ballroom", 1, "2026-11-22", "none, at all", "a \"plain\" ground") }));
            C(comma.Count == 1 && comma[0].why == "none, at all" && comma[0].instead == "a \"plain\" ground", "commas and quotes in a cell kept");
        }

        // ---------------- The seams ----------------
        string store = Read("UI/Faces/PortraitStore.cs"), stage = Read("UI/Faces/FaceEngineStage.cs"), inspect = Read("UI/Faces/InspectPortrait.cs"), engine = Read("UI/Faces/FaceEngine.cs");
        string backdrops = Read("UI/Faces/Backdrops.cs"), defSrc = Read("UI/Faces/PortraitDef.cs"), backDef = Read("UI/Faces/BackdropDef.cs"), lib3 = Read("UI/Faces/PortraitLibrary.cs");
        C(store.Contains("File.ReadAllBytes(path)") && store.Contains("ImageConversion.LoadImage(tex, bytes, false)") && store.Contains("ThreadPool.QueueUserWorkItem")
          && store.Contains("heavyDone.TryDequeue(out var heavy)") && store.Contains("PortraitPictureMath.MeasureHead(") && store.Contains("PortraitPictureMath.Downscale(")
          && store.Contains("PortraitPictureMath.LongSideFor(Prefs.UIScale)"), "loaded on demand: read on a worker, decoded by the game at most one a frame, measured and scaled on a worker");
        C(store.Contains("public const int MostPictures = 48;") && store.Contains("UnityEngine.Object.Destroy(f.texture)") && store.Contains("Time.frameCount - f.lastUsed > 1"),
            "a small cache, the least lately shown let go and its texture destroyed, never one shown now");
        C(store.Contains("CouncilLog.WarnOnce(\"Portraits: the picture \" + f.key + \" could not be read") && lib3.Contains("PortraitStore.Failed(pic.file)) return null;"),
            "a file that can't be read: one warning, and the game's own drawing");
        C(defSrc.Contains("modContentPack?.RootDir") && defSrc.Contains("\"Portraits\"") && backDef.Contains("public class BackdropDef : Def") && backDef.Contains("PortraitFiles.For(this, texPath, false)"),
            "a picture's file under its mod's Portraits folder; the backdrops' defs the same way");
        C(engine.Contains("PortraitStore.Pump();") && engine.Contains("if (pic != null && pic.texture == null) return;"), "the store pumped once a frame from the portraits; a picture loading shows the ground");
        C(inspect.Contains("FaceEngine.DrawStaged(inner, p, made, true) && !FaceEngine.DrawGameStaged(inner, p, made, true)") && inspect.Contains("var made = new Vector2(rs, rs * tall);")
          && inspect.Contains("FaceMath.Place(Scene(pane), asked, out var fr, tall)") && inspect.Contains("ev.button == 1") && inspect.Contains("CC_Portrait_ChooseAnother"),
            "the large portrait draws the painted picture whole at its shape in front of its backdrop, else the game's drawing in front of one; a right click offers another picture");
        C(stage.Contains("PortraitGpu.ReadSharp(pic.texture, w, h, Vector2.one, Vector2.zero)") && !stage.Contains("CropFor") && stage.Contains("new Rect(-side / local.width, -below / ph, 1f + 2f * side / local.width, 1f + below / ph)")
          && store.Contains("PortraitPictureMath.SolidFoot(small, nw, nh);"),
            "the whole picture, nothing cropped; its foot row carried on below the portrait's edge, and the body made solid at the picture's foot when it loads");
        C(stage.Contains("StageLookEvery = 2f") && stage.Contains("Backdrops.WhereOf(p, out float glow)") && stage.Contains("PortraitPictureMath.LightStep(glow, -1)")
          && stage.Contains("PortraitStore.Run(") && stage.Contains("staged.Find(s => s.key == key)") && stage.Contains("StagedMost = 4"),
            "the backdrop and the light read when a person is first shown and kept while they are; the picture made once on a worker and kept by its key; each frame only drawn");
        C(backdrops.Contains("map.glowGrid.GroundGlowAt(pos)") && backdrops.Contains("room.PsychologicallyOutdoors") && backdrops.Contains("roof.isNatural") && backdrops.Contains("Avail.EffectiveTech")
          && backdrops.Contains("Peoples.TechOf(owner)") && backdrops.Contains("RoomFor = 30f"), "where a person is: the room, its role and things, the colony's age (or the map's people's), the land, the rock roof, the light");
        string settings = Read("CouncilSettings.cs");
        foreach (var (field, def) in new[] { ("portraitPick", "PortraitPickMode.Closest"), ("portraitBackdrop", "PortraitBackdropMode.AsPainted"), ("portraitOutline", "PortraitOutlineMode.Off"), ("portraitLightTint", "false") })
            C(Regex.IsMatch(settings, $@"public \w+ {field} = {Regex.Escape(def)};") && settings.Contains($"Scribe_Values.Look(ref {field}, \"{field}\", {def});")
              && SettingsCatalog.Find(field)?.section == "CC_SetSec_Portraits", $"the setting {field}, {def} by default, under Portraits");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        foreach (var k in new[]
                 {
                     "CC_SetLabel_PortraitPick", "CC_PortraitPick_Closest", "CC_PortraitPick_OnlyClose", "CC_SetLabel_PortraitBackdrop", "CC_PortraitBackdrop_AsPainted", "CC_PortraitBackdrop_LightlySoftened",
                     "CC_PortraitBackdrop_Softened", "CC_PortraitBackdrop_Plain", "CC_SetLabel_PortraitOutline", "CC_PortraitOutline_Subtle", "CC_PortraitOutline_Stronger", "CC_Settings_PortraitLightTint",
                     "CC_Portrait_ChooseAnother", "CC_Portrait_ChooseDone", "CC_Portrait_ChooseNone", "CC_Portrait_TipLibrary"
                 })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");

        // ---------------- The cost, timed on the pure parts ----------------
        {
            int w = 1024, h = 1280;
            var person = new byte[w * h * 4];
            var fig = Figure(w, h, 90, 340, 520);
            for (int i = 0; i < w * h; i++)
            {
                person[i * 4] = 180;
                person[i * 4 + 1] = 140;
                person[i * 4 + 2] = 110;
                person[i * 4 + 3] = fig[i];
            }
            var sw = Stopwatch.StartNew();
            var head = PortraitPictureMath.MeasureHead(PortraitPictureMath.AlphaOf(person), w, h);
            double measure = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            PortraitPictureMath.FitSize(w, h, 450, out int nw, out int nh);
            var small = PortraitPictureMath.Downscale(person, w, h, nw, nh);
            double scale = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            PortraitPictureMath.Bleed(small, nw, nh, 4);
            double bleed = sw.Elapsed.TotalMilliseconds;
            var times = new List<string>();
            foreach (int side in new[] { 186, 354, 720 })
            {
                // The whole picture at the portrait's shape (5:4 tall), as the game reads it back at that size.
                int pw = side, phh = (int)Math.Round(side * 1.25f);
                var cut = PortraitPictureMath.Downscale(small, nw, nh, Math.Min(pw, nw), Math.Min(phh, nh));
                int cw = Math.Min(pw, nw), ch = Math.Min(phh, nh);
                var back = Rgba(cw, ch, (x, y) => ((byte)(x % 255), (byte)(y % 255), 90, 255));
                sw.Restart();
                PortraitComposeMath.Backdrop(back, cw, ch, PortraitComposeMath.LookOf(PortraitBackdropMode.LightlySoftened), 0.86f, 0.9f, 0.95f);
                var layer = PortraitComposeMath.PersonLayer(cut, cw, ch, PortraitComposeMath.LookOf(PortraitOutlineMode.Subtle));
                times.Add($"{cw} x {ch} px {sw.Elapsed.TotalMilliseconds:0} ms");
                C(layer.Length == cw * ch * 4, $"composed at {cw} x {ch} pixels");
            }
            C(head.found && measure < 2000 && scale < 4000, "the pure parts run on a picture of the library's size");
            Console.WriteLine($"  (portraits, phase 4: on a 1024 x 1280 cut-out here, the head measured in {measure:0} ms, scaled to {nw} x {nh} in {scale:0} ms, its edge coloured in {bleed:0} ms;"
                              + $" a large portrait's picture made (lightly softened, the subtle line) at {string.Join(", ", times)}; all of it on a worker thread in the game)");
        }
        return n;
    }
}
