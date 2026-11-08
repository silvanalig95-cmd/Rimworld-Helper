using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own look, stage 1 (docs/design/rimfolk-look.md; Source/ColonyCouncil/UI/WindowFrameMath.cs, SkinMath.cs,
/// WindowFrame.cs, UiKitSkin.cs, Window_Region.cs): the frame's rules on every screen from 768 to 2160 pixels high at every
/// interface scale up to 2 (where a window opens, moved and sized and kept on the screen, its least, the large size and
/// back, the remembered place written and read and fitted to another screen), the palette's contrast for text on every
/// ground, the kit's rounded shapes and 9-slices, the tabs' room, the layouts from the least window to a large one, and the
/// seams in the game (the two settings, the region button, the floating window as one of the four, the trade window in
/// the frame, nothing made per frame).
/// </summary>
public static class RimfolkLookTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "Rimfolk's own look: " + what);
        }

        Frame(C);
        Remembered(C);
        Palette(C);
        Shapes(C);
        Layouts(C);
        Seams(root, C);
        return n;
    }

    private static bool Near(float a, float b, float by = 0.51f) => Math.Abs(a - b) <= by;

    private static bool OnScreen(FrameRect r, float sw, float sh, float bottom) =>
        r.x >= -0.01f && r.y >= -0.01f && r.XMax <= sw + 0.51f && r.YMax <= sh - bottom + 0.51f && r.w > 0f && r.h > 0f;

    // ---------------- The frame on every screen ----------------

    private static void Frame(Action<bool, string> C)
    {
        var screens = new[]
        {
            (1024f, 768f), (1366f, 768f), (1280f, 800f), (1440f, 900f), (1600f, 900f), (1280f, 1024f), (1680f, 1050f), (1920f, 1080f),
            (2560f, 1080f), (1920f, 1200f), (2560f, 1440f), (3440f, 1440f), (2560f, 1600f), (3840f, 2160f)
        };
        var scales = new[] { 1f, 1.25f, 1.5f, 1.75f, 2f };
        var windows = new[]
        {
            ("the region window", WindowFrameMath.RegionLeastW, WindowFrameMath.RegionLeastH, WindowFrameMath.BottomBar, 1010f, 720f),
            ("the trade window", WindowFrameMath.TradeLeastW, WindowFrameMath.TradeLeastH, 0f, 1800f, 4000f)
        };
        int screensTried = 0;
        foreach (var (pw, ph) in screens)
        foreach (var scale in scales)
        {
            float sw = (float)Math.Floor(pw / scale), sh = (float)Math.Floor(ph / scale);
            screensTried++;
            string at = $"{pw}x{ph} at {scale}";
            foreach (var (name, lw, lh, bottom, wantW, wantH) in windows)
            {
                var first = WindowFrameMath.Centred(Math.Min(wantW, sw - 24f), Math.Min(wantH, sh), sw, sh, lw, lh, bottom);
                C(OnScreen(first, sw, sh, bottom), $"{at}: {name} first opens on the screen ({first})");
                WindowFrameMath.Room(sw, sh, bottom, out float roomW, out float roomH);
                C(Near(first.x + first.w / 2f, roomW / 2f, 1f) && Near(first.y + first.h / 2f, roomH / 2f, 1f), $"{at}: {name} first opens in the middle");
                C(first.w >= Math.Min(lw, roomW) - 0.5f && first.h >= Math.Min(lh, roomH) - 0.5f, $"{at}: {name} no smaller than its least where the screen has room");
                var large = WindowFrameMath.Large(sw, sh, lw, lh, bottom);
                C(OnScreen(large, sw, sh, bottom), $"{at}: {name} large stays on the screen ({large})");
                C(large.w >= Math.Min(roomW, Math.Max(lw, roomW * WindowFrameMath.LargeShare)) - 1.01f && large.w <= roomW + 0.01f
                  && large.h >= Math.Min(roomH, Math.Max(lh, roomH * WindowFrameMath.LargeShare)) - 1.01f, $"{at}: {name} large is about 92% of the screen ({large.w:0} of {roomW:0})");
                C(Near(large.x + large.w / 2f, roomW / 2f, 1f), $"{at}: {name} large sits in the middle");
                // Dragged far past every edge: still on the screen, its size kept.
                foreach (var (dx, dy) in new[] { (-5000f, 0f), (5000f, 0f), (0f, -5000f), (0f, 5000f), (3000f, 3000f), (-3000f, -3000f) })
                {
                    var moved = WindowFrameMath.Move(first, dx, dy, sw, sh, lw, lh, bottom);
                    C(OnScreen(moved, sw, sh, bottom) && Near(moved.w, first.w) && Near(moved.h, first.h), $"{at}: {name} dragged ({dx},{dy}) stays on the screen at its size ({moved})");
                }
                // Sized from every edge and corner, grown and shrunk past the screen and the least.
                foreach (var e in new[] { FrameEdge.Left, FrameEdge.Right, FrameEdge.Top, FrameEdge.Bottom, FrameEdge.Left | FrameEdge.Top, FrameEdge.Right | FrameEdge.Bottom, FrameEdge.Left | FrameEdge.Bottom, FrameEdge.Right | FrameEdge.Top })
                foreach (float d in new[] { -4000f, -60f, 60f, 4000f })
                {
                    var r = WindowFrameMath.Resize(first, e, d, d, sw, sh, lw, lh, bottom);
                    WindowFrameMath.Least(lw, lh, sw, sh, bottom, out float lw2, out float lh2);
                    C(OnScreen(r, sw, sh, bottom), $"{at}: {name} sized from {e} by {d} stays on the screen ({r})");
                    C(r.w >= lw2 - 0.5f && r.h >= lh2 - 0.5f, $"{at}: {name} sized from {e} by {d} keeps its least ({r})");
                    if ((e & FrameEdge.Left) != 0 && (e & FrameEdge.Right) == 0) C(Near(r.XMax, first.XMax, 1f) || r.w >= roomW - 0.5f, $"{at}: {name} sized from the left keeps its right edge");
                    if ((e & FrameEdge.Right) != 0) C(Near(r.x, first.x, 1f), $"{at}: {name} sized from the right keeps its left edge");
                    if ((e & FrameEdge.Top) != 0 && (e & FrameEdge.Bottom) == 0) C(Near(r.YMax, first.YMax, 1f) || r.h >= roomH - 0.5f, $"{at}: {name} sized from the top keeps its foot");
                    if ((e & FrameEdge.Bottom) != 0) C(Near(r.y, first.y, 1f), $"{at}: {name} sized from the bottom keeps its top");
                }
                // Large and back again.
                var place = new FramePlace { rect = first };
                var big = WindowFrameMath.Toggle(place, sw, sh, lw, lh, bottom);
                var back = WindowFrameMath.Toggle(big, sw, sh, lw, lh, bottom);
                C(big.large && big.rect.Near(large) && !back.large && back.rect.Near(first, 1f), $"{at}: {name} large and back where it was");
            }
        }
        C(screensTried == 70, $"every screen at every scale ({screensTried})");
        // The region window keeps the game's bottom bar clear; the trade window, a dialog, may cover it.
        var region = WindowFrameMath.Large(1920f, 1080f, WindowFrameMath.RegionLeastW, WindowFrameMath.RegionLeastH, WindowFrameMath.BottomBar);
        C(region.YMax <= 1080f - WindowFrameMath.BottomBar, "the region window large stops above the bottom bar");
        C(Near(region.w, (float)Math.Floor(1920f * 0.92f), 1f), $"at 1920 the large size is 92% of the width ({region.w:0})");
        // The edges' grips: inside, near an edge, in a corner.
        C(WindowFrameMath.EdgesAt(500f, 300f, 1000f, 700f) == FrameEdge.None, "the middle of a window sizes nothing");
        C(WindowFrameMath.EdgesAt(2f, 300f, 1000f, 700f) == FrameEdge.Left && WindowFrameMath.EdgesAt(998f, 300f, 1000f, 700f) == FrameEdge.Right
          && WindowFrameMath.EdgesAt(500f, 1f, 1000f, 700f) == FrameEdge.Top && WindowFrameMath.EdgesAt(500f, 697f, 1000f, 700f) == FrameEdge.Bottom, "each edge sizes from itself");
        C(WindowFrameMath.EdgesAt(996f, 690f, 1000f, 700f) == (FrameEdge.Right | FrameEdge.Bottom) && WindowFrameMath.EdgesAt(3f, 10f, 1000f, 700f) == (FrameEdge.Left | FrameEdge.Top)
          && WindowFrameMath.EdgesAt(12f, 697f, 1000f, 700f) == (FrameEdge.Left | FrameEdge.Bottom), "a corner sizes from both its edges, along a short grip");
        C(WindowFrameMath.EdgesAt(-1f, 300f, 1000f, 700f) == FrameEdge.None && WindowFrameMath.EdgesAt(500f, 701f, 1000f, 700f) == FrameEdge.None, "outside the window nothing");
        C(WindowFrameMath.EdgesAt(200f, 20f, 1000f, 700f) == FrameEdge.None, "the title bar below the top grip moves, it doesn't size");
        C(WindowFrameMath.Grip < WindowFrameMath.TitleH / 4f && WindowFrameMath.Grip >= 4f, "the grip is thin beside the title bar, thick enough to find");
        // A narrow window takes the nearer edge, never both.
        var both = WindowFrameMath.EdgesAt(4f, 300f, 10f, 700f);
        C(both == FrameEdge.Left, $"a window narrower than two grips sizes from the nearer side ({both})");
        // A window asked for larger than the screen is the screen; the least never more than the screen.
        var tooBig = WindowFrameMath.Centred(5000f, 5000f, 800f, 600f, 720f, 480f, 35f);
        C(tooBig.w <= 800f && tooBig.h <= 565f && OnScreen(tooBig, 800f, 600f, 35f), "asked larger than the screen: the screen");
        WindowFrameMath.Least(720f, 480f, 500f, 400f, 35f, out float leastW, out float leastH);
        C(leastW == 500f && leastH == 365f, "the least is never more than the screen");
        WindowFrameMath.Least(10f, 10f, 1920f, 1080f, 0f, out leastW, out leastH);
        C(leastW == WindowFrameMath.LeastW && leastH == WindowFrameMath.LeastH, "a window's least is never below the frame's");
    }

    // ---------------- Remembered between openings and games ----------------

    private static void Remembered(Action<bool, string> C)
    {
        var p = new FramePlace { rect = new FrameRect(120f, 80f, 1100f, 760f) };
        string text = WindowFrameMath.Write(p);
        C(text == "120;80;1100;760", $"a place written in plain numbers ({text})");
        C(WindowFrameMath.TryRead(text, out var back) && back.rect.Near(p.rect) && !back.large, "and read back the same");
        var large = new FramePlace { rect = new FrameRect(77f, 43f, 1766f, 958f), large = true, back = new FrameRect(300f, 200f, 1010f, 720f) };
        string lt = WindowFrameMath.Write(large);
        C(lt == "77;43;1766;958;L;300;200;1010;720", $"a large one with where it goes back to ({lt})");
        C(WindowFrameMath.TryRead(lt, out var lb) && lb.large && lb.back.Near(large.back) && lb.rect.Near(large.rect), "and read back large, with its way back");
        foreach (var bad in new[] { null, "", "1;2;3", "a;b;c;d", "1;2;0;4", "1;2;3;-4", "NaN;2;300;400", "1;2;300;400;X;1;2;3;4", "1;2;300;400;L;1;2", "1;2;9999999;400", "1;2;300;400;5" })
            C(!WindowFrameMath.TryRead(bad, out _), $"\"{bad}\" is not a place: the window opens where it first would");
        C(WindowFrameMath.TryLine("Region=10;20;800;600", out var key, out var lp) && key == "Region" && lp.rect.Near(new FrameRect(10f, 20f, 800f, 600f)),
            "a settings line names its window");
        C(!WindowFrameMath.TryLine("=1;2;3;4", out _, out _) && !WindowFrameMath.TryLine("Region", out _, out _) && !WindowFrameMath.TryLine(null, out _, out _), "a line without a key or a place is none");
        C(WindowFrameMath.Line("Trade", p) == "Trade=120;80;1100;760", "and is written so");
        // Left on a 4K screen at scale 1, opened on 1366 by 768: fitted onto it, shrunk where it must be.
        var wide = new FramePlace { rect = new FrameRect(2400f, 1200f, 1400f, 900f) };
        var small = WindowFrameMath.Opening(wide, 1366f, 768f, WindowFrameMath.RegionLeastW, WindowFrameMath.RegionLeastH, WindowFrameMath.BottomBar);
        C(OnScreen(small, 1366f, 768f, WindowFrameMath.BottomBar), $"wholly on it ({small})");
        var bigAgain = WindowFrameMath.Opening(new FramePlace { rect = new FrameRect(0f, 0f, 3000f, 2000f), large = true }, 1600f, 900f, 720f, 480f, 35f);
        C(bigAgain.Near(WindowFrameMath.Large(1600f, 900f, 720f, 480f, 35f)), "a window left large opens large on this screen's large size");
        C(!WindowFrameMath.Valid(new FrameRect(float.NaN, 0f, 100f, 100f)) && !WindowFrameMath.Valid(new FrameRect(0f, 0f, 0f, 100f)) && WindowFrameMath.Valid(new FrameRect(-20f, 5f, 800f, 600f)),
            "a rect is valid when finite and of some size (off the screen is fitted, not refused)");
    }

    // ---------------- The palette: contrast for text ----------------

    private static void Palette(Action<bool, string> C)
    {
        foreach (var (name, rgb) in SkinMath.TextColours)
        foreach (var (ground, g) in SkinMath.Grounds)
        {
            double c = SkinMath.Contrast(rgb, g);
            C(c >= SkinMath.TextContrast, $"{name} on {ground}: contrast {c:0.00} of at least 4.5");
        }
        C(SkinMath.Contrast(SkinMath.AccentInk, SkinMath.Accent) >= SkinMath.TextContrast && SkinMath.Contrast(SkinMath.AccentInk, SkinMath.AccentHover) >= SkinMath.TextContrast
          && SkinMath.Contrast(SkinMath.AccentInk, SkinMath.AccentPressed) >= SkinMath.TextContrast, "a primary button's word on the accent, under the mouse and held");
        C(Math.Abs(SkinMath.Contrast(0xffffff, 0x000000) - 21.0) < 0.01 && Math.Abs(SkinMath.Contrast(0x777777, 0x777777) - 1.0) < 0.001, "the contrast as WCAG measures it");
        // The mockup's own values.
        C(SkinMath.WindowGround == 0x17191b && SkinMath.Panel == 0x212427 && SkinMath.Raised == 0x2a2e32 && SkinMath.Edge == 0x3a3f44 && SkinMath.Ink == 0xece8df
          && SkinMath.Faint == 0xb0aa9c && SkinMath.Accent == 0xe0a845 && SkinMath.Ally == 0x6aa6dc && SkinMath.Foe == 0xe08a45, "the mockup's palette, as the player saw it");
        // The grounds step up from the window to a card, so a card reads on a panel on the window.
        C(SkinMath.Luminance(SkinMath.WindowGround) < SkinMath.Luminance(SkinMath.Panel) && SkinMath.Luminance(SkinMath.Panel) < SkinMath.Luminance(SkinMath.Raised)
          && SkinMath.Luminance(SkinMath.Raised) < SkinMath.Luminance(SkinMath.RaisedHover) && SkinMath.Luminance(SkinMath.Pressed) < SkinMath.Luminance(SkinMath.Raised),
            "the window, a panel, a card and a card under the mouse step up; held, it steps down");
        // Allied and hostile can be told apart by more than hue (blue and orange), and never without their word.
        C(SkinMath.Ally != SkinMath.Foe && Math.Abs(SkinMath.B(SkinMath.Ally) - SkinMath.B(SkinMath.Foe)) > 0.3f, "allied blue and hostile orange apart");
    }

    // ---------------- The kit's shapes ----------------

    private static void Shapes(Action<bool, string> C)
    {
        foreach (int radius in SkinMath.Radii)
        {
            int k = SkinMath.Supersample, s = SkinMath.TextureSide(radius);
            float r = radius * k;
            C(s % 2 == 0 && s >= 2 * r + 4, $"radius {radius}: a texture {s} wide with its flat middle");
            C(SkinMath.Coverage(s / 2, s / 2, s, r) == 1f, $"radius {radius}: the middle is filled");
            C(SkinMath.Coverage(0, 0, s, r) < 0.05f && SkinMath.Coverage(s - 1, s - 1, s, r) < 0.05f, $"radius {radius}: the corners are cut");
            C(SkinMath.Coverage(s / 2, 0, s, r) > 0.45f && SkinMath.Coverage(0, s / 2, s, r) > 0.45f, $"radius {radius}: the edges' middles are filled");
            bool symmetric = true;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                if (Math.Abs(SkinMath.Coverage(x, y, s, r) - SkinMath.Coverage(s - 1 - x, y, s, r)) > 0.001f || Math.Abs(SkinMath.Coverage(x, y, s, r) - SkinMath.Coverage(x, s - 1 - y, s, r)) > 0.001f)
                    symmetric = false;
            C(symmetric, $"radius {radius}: the same at every corner");
            C(SkinMath.Ring(s / 2, s / 2, s, r, k) == 0f && SkinMath.Ring(s / 2, 0, s, r, k) > 0.4f, $"radius {radius}: the edge is a ring, empty in the middle");
            C(SkinMath.Coverage(0, s - 1, s, r, 0f, true) > 0.95f && SkinMath.Coverage(0, 0, s, r, 0f, true) < 0.05f, $"radius {radius}: a tab is rounded at its top only");
            C(SkinMath.Ring(s / 2, s - 1, s, r, k, true) == 0f && SkinMath.Ring(0, s - 1, s, r, k, true) > 0.4f && SkinMath.Ring(s / 2, 0, s, r, k, true) > 0.4f,
                $"radius {radius}: a tab's edge runs along its top and sides, open at its foot where it joins the panel");
            // Smooth: no texel jumps from empty to full along the corner's diagonal.
            float last = 0f;
            bool smooth = true;
            for (int i = 0; i < s / 2; i++)
            {
                float c = SkinMath.Coverage(i, i, s, r);
                if (c < last - 0.001f) smooth = false;
                last = c;
            }
            C(smooth, $"radius {radius}: the corner fills smoothly toward the middle");
        }
        int side = (SkinMath.ShadowReach + SkinMath.RadiusCard) * 2 + 4;
        C(SkinMath.Shadow(side / 2, side / 2, side, SkinMath.ShadowReach, SkinMath.RadiusCard) == 1f && SkinMath.Shadow(0, 0, side, SkinMath.ShadowReach, SkinMath.RadiusCard) < 0.05f
          && SkinMath.Shadow(0, side / 2, side, SkinMath.ShadowReach, SkinMath.RadiusCard) < 0.2f, "the shadow is full under the shape and fades out to its edge");
        // 9-slice: the pieces tile the rect exactly, whatever its size, and read the texture's matching parts.
        foreach (var (w, h) in new[] { (300f, 120f), (40f, 20f), (18f, 18f), (8f, 30f), (1000f, 6f) })
        {
            float area = 0f;
            bool inside = true, uv = true;
            for (int i = 0; i < 9; i++)
            {
                if (!SkinMath.Slice(i, 10f, 20f, w, h, 9f, 0.4f, out float x, out float y, out float pw, out float ph, out float u, out float v, out float uw, out float vh)) continue;
                area += pw * ph;
                if (x < 10f - 0.01f || y < 20f - 0.01f || x + pw > 10f + w + 0.01f || y + ph > 20f + h + 0.01f) inside = false;
                if (u < -0.001f || v < -0.001f || u + uw > 1.001f || v + vh > 1.001f || uw <= 0f || vh <= 0f) uv = false;
            }
            C(Math.Abs(area - w * h) < 0.5f && inside, $"a {w}x{h} rect: its nine pieces cover it exactly");
            C(uv, $"a {w}x{h} rect: each piece reads inside the texture");
            C(SkinMath.SliceMargin(w, h, 9f) <= Math.Min(w, h) / 2f + 0.001f, $"a {w}x{h} rect: its corners never meet past its middle (as round as it is tall)");
        }
        SkinMath.Slice(0, 0f, 0f, 100f, 100f, 8f, 0.3f, out _, out _, out float cw, out float ch, out float cu, out float cv, out float cuw, out float cvh);
        C(cw == 8f && ch == 8f && cu == 0f && Math.Abs(cv - 0.7f) < 0.001f && Math.Abs(cuw - 0.3f) < 0.001f && Math.Abs(cvh - 0.3f) < 0.001f, "the top left piece is the texture's top left corner");
        SkinMath.Slice(8, 0f, 0f, 100f, 100f, 8f, 0.3f, out float bx, out float by, out _, out _, out float bu, out float bv, out _, out _);
        C(bx == 92f && by == 92f && Math.Abs(bu - 0.7f) < 0.001f && bv == 0f, "the bottom right piece is the texture's bottom right corner");
        // Tabs share their row: as wide as they like where they fit, shrunk alike where they don't, never below the least.
        var natural = new[] { 90f, 120f, 150f, 110f };
        var tx = new float[4];
        var tw = new float[4];
        float used = SkinMath.Tabs(natural, 4, 1000f, 4f, 44f, tx, tw);
        C(tw.SequenceEqual(natural) && Near(used, 482f) && tx[0] == 0f && Near(tx[3], 372f), "tabs that fit keep their widths, side by side");
        used = SkinMath.Tabs(natural, 4, 300f, 4f, 44f, tx, tw);
        C(used <= 300.5f && tw.All(x => x >= 44f) && Math.Abs(tw[1] / tw[0] - 120f / 90f) < 0.01f, $"tabs that don't fit shrink alike into the room ({used:0})");
        used = SkinMath.Tabs(natural, 4, 100f, 4f, 44f, tx, tw);
        C(tw.All(x => Near(x, 44f)), "never below the least (the icon alone)");
        C(SkinMath.TabWidth(0f, false) >= 64f && SkinMath.TabWidth(2000f, true) <= 240f && SkinMath.TabWidth(80f, true) > SkinMath.TabWidth(80f, false), "a tab's width within reason, more with its icon");
    }

    // ---------------- The layouts from the least window to a large one ----------------

    private static void Layouts(Action<bool, string> C)
    {
        // The region window's body in Rimfolk's look: the window less its insets, the tabs' row, the panel's padding.
        foreach (var (w, h, label) in new[] { (WindowFrameMath.RegionLeastW, WindowFrameMath.RegionLeastH, "the least"), (1010f, 720f, "the standard"), (1280f, 860f, "the old large"),
                     (1766f, 958f, "1920 made large"), (3532f, 1955f, "4K made large") })
        {
            float content = WindowFrameMath.BodyWidth(w, true) - 2f * UiKitPadding;
            float contentH = WindowFrameMath.BodyHeight(h, true) - TabRowOwn - 2f * UiKitPadding;
            C(content > 0f && contentH > 0f, $"{label} region window ({w:0}x{h:0}): a content area {content:0} by {contentH:0}");
            DrawerMath.Split(0f, content, out _, out float viewW, out _, out float drawerW, out bool over);
            C(drawerW >= DrawerMath.Narrowest - 0.5f && drawerW <= DrawerMath.Width + 0.5f, $"{label}: the drawer {drawerW:0} wide");
            C(over == (content < DrawerMath.BesideFrom), $"{label}: the drawer {(over ? "over" : "beside")} the view");
            C(viewW >= 300f, $"{label}: the view keeps room beside the drawer ({viewW:0})");
            float peoplesView = content;
            float list = PeoplesViewMath.ListWidth(peoplesView), page = PeoplesViewMath.PageWidth(peoplesView);
            C(page >= 330f, $"{label}: Peoples' page keeps room for its header and acts ({page:0})");
            C(list >= PeoplesViewMath.ListCompact && list <= PeoplesViewMath.ListWider, $"{label}: the list {list:0} wide");
            if (w >= 1766f) C(list == PeoplesViewMath.ListWider && PeoplesViewMath.ActColumns(page) >= 2, $"{label}: a wider list and the acts in columns");
            if (w >= 3000f) C(PeoplesViewMath.ActColumns(page) == 3, $"{label}: the acts in three columns");
            int cols = RegionViewsMath.Columns(content - 16f);
            if (w <= 1010f) C(cols == 1, $"{label}: Here and away in one column");
            if (w >= 1766f) C(cols == 2, $"{label}: Here and away and the faces in two columns side by side");
            C(RegionViewsMath.ColumnWidth(content - 16f) >= 330f, $"{label}: each column keeps room for its rows");
            // The title bar: the icon and title, the corner, large and close, in the least window too.
            float bar = 12f + 26f + 120f + 8f + WindowLayoutMath.CornerWidth() + 8f + 2f * (WindowFrameMath.TitleButton + WindowFrameMath.TitleGap) + 12f;
            C(bar <= w, $"{label}: the title bar holds the title, the corner and its buttons ({bar:0} of {w:0})");
        }
        C(RegionViewsMath.Columns(RegionViewsMath.TwoColumnsFrom) == 2 && RegionViewsMath.Columns(RegionViewsMath.TwoColumnsFrom - 1f) == 1, "two columns from their width");
        C(PeoplesViewMath.ActColumns(1180f) == 3 && PeoplesViewMath.ActColumns(1179f) == 2 && Near(PeoplesViewMath.ActColumnWidth(1200f), (1200f - 24f) / 3f)
          && PeoplesViewMath.ActColumnWidth(500f) == 500f, "the acts in three columns on a large page, their widths sharing it");
        // The trade window at its least: the table and the side, the foot's buttons and the silver.
        float inner = WindowFrameMath.BodyWidth(WindowFrameMath.TradeLeastW, true);
        float side = inner >= 1500f ? 300f : 240f, table = inner - side - 14f;
        C(table >= 600f, $"the trade window at its least keeps a table {table:0} wide");
        float mid = inner / 2f, reset = mid - 80f - 170f, fillW = Math.Min(140f, (reset - 20f) / 2f), silver = inner - (mid + 80f + 10f + 160f) - 16f;
        C(fillW >= 80f && silver >= 120f, $"the trade window at its least: Fill and Clear ({fillW:0}) and the silver ({silver:0}) beside Reset, Accept and Cancel");
        C(WindowFrameMath.BodyWidth(1000f, false) == 1000f - 36f && WindowFrameMath.BodyHeight(700f, false) == 700f - WindowFrameMath.TitleH - 22f, "in the game's look the body keeps the game's margin");
    }

    /// <summary>UiKit.Padding (the content inside a panel) and the own look's row of tabs (WindowTabs.OwnTabH), read from their files below.</summary>
    private const float UiKitPadding = 12f, TabRowOwn = 34f;

    // ---------------- The seams in the game ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool ownLook = true;") && settings.Contains("Scribe_Values.Look(ref ownLook, \"ownLook\", true);"), "Rimfolk's own look: on at first, kept");
        C(settings.Contains("public bool windowsMove = true;") && settings.Contains("Scribe_Values.Look(ref windowsMove, \"windowsMove\", true);"), "the windows move and resize: on at first, kept");
        C(settings.Contains("Scribe_Collections.Look(ref windowPlaces, \"windowPlaces\", LookMode.Value);") && settings.Contains("windowPlaces ??= "), "the places kept in the settings, never null");
        C(catalog.Contains("Toggle(\"ownLook\"") && catalog.Contains("Toggle(\"windowsMove\"") && catalog.Contains("places.covers = new[] { \"windowPlaces\" };"), "both in the settings, and a way to put the windows back");
        C(Code("UI/UiKit.cs").Contains("public const float Padding = 12f;") && Code("UI/WindowTabs.cs").Contains("OwnTabH = 34f"), "the layout test's measures are the kit's");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_OwnLook.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var k in new[] { "CC_Settings_OwnLook", "CC_Settings_OwnLook_Tip", "CC_SetNote_OwnLook", "CC_Settings_WindowsMove", "CC_Settings_WindowsMove_Tip", "CC_SetNote_WindowsMove",
                     "CC_Settings_WindowsPlaceReset", "CC_SetNote_WindowsPlaceReset", "CC_Frame_CloseTip", "CC_Frame_LargeTip", "CC_Frame_BackTip" })
            C(keyed.ContainsKey(k) && !keyed[k].Contains('!') && !keyed[k].Contains('—'), $"{k} has its words, plainly");
        string frame = Code("UI/WindowFrame.cs");
        C(frame.Contains("public override void WindowOnGUI()") && frame.Contains("frame.Follow(this);") && frame.Contains("Input.GetMouseButton(0)") && frame.Contains("UI.MousePositionOnUIInverted"),
            "the window follows the mouse from outside the game's window function, until the button is let go");
        C(frame.Contains("e.clickCount >= 2") && frame.Contains("ToggleLarge(w)") && frame.Contains("WindowFrameMath.EdgesAt("), "a double-click on the title makes it large; the edges size it");
        C(frame.Contains("CouncilSettings.SaveNow()") && frame.Contains("WindowFrameMath.Opening(") && frame.Contains("public override void Notify_ResolutionChanged()"),
            "its place written to the settings, fitted on opening and when the screen changes");
        C(frame.Contains("UiKit.BeginOwn()") && frame.Contains("UiKit.EndOwn(was)") && Regex.IsMatch(frame, @"finally\s*\{\s*if \(ownLook\) UiKit\.EndOwn\(was\);"), "the look begins and always ends with the window's contents");
        // Since stage 2 the region window is one of four floating windows (UI/FloatingWindows.cs) sharing one base and one worker.
        string region = Code("UI/Window_Region.cs"), floating = Code("UI/FloatingWindows.cs");
        C(region.Contains("class Window_Region : RimfolkFloatingWindow") && floating.Contains("layer = WindowLayer.GameUI") && floating.Contains("closeOnClickedOutside = false")
          && region.Contains("MainTabWindow_World.DrawRegion(inRect, true)") && floating.Contains("Drawer.Cancel(Which)"),
            "the floating region window: the game's interface layer, left open by a click on the map, the same views, Escape for the drawer first");
        C(region.Contains("class MainButtonWorker_Region : MainButtonWorker_RimfolkWindow") && floating.Contains("class MainButtonWorker_RimfolkWindow : MainButtonWorker_ToggleTab")
          && floating.Contains("base.Activate();"), "its button floats it, or opens the tab as before");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Council_Misc.xml")).Root!.Elements("MainButtonDef").First(e => e.Element("defName")!.Value == "Rimfolk_WorldButton");
        C(misc.Element("workerClass")?.Value == "ColonyCouncil.MainButtonWorker_Region" && misc.Element("tabWindowClass")?.Value == "ColonyCouncil.MainTabWindow_World",
            "the region button keeps its tab and gains its worker");
        string pages = Code("UI/Pages.cs");
        C(pages.Contains("FloatingWindows.Showing(w) || TabShowing(w)") && pages.Contains("FloatingWindows.Open(w);") && pages.Contains("FloatingWindows.ShutAll(false);")
          && pages.Contains("Close(w.Value);"), "Pages knows the floating windows: showing, opened, closed, one of the four at a time");
        C(Code("UI/WindowTabs.cs").Contains("FloatingWindows.ShutAll(false);"), "a tab of the four opening turns the page");
        C(Code("UI/Cards/Cards.cs").Contains("Pages.LeaveForMap();") && !Code("UI/Cards/Cards.cs").Contains("EscapeCurrentTab"), "a jump to the map leaves the floating window as it left the tab");
        string world = Code("UI/MainTabWindow_World.cs");
        C(world.Contains("internal static void DrawRegion(Rect inRect, bool cornerInTitle)") && world.Contains("WindowTabs.DrawFor(inRect, tabs, RimfolkWindow.World, cornerInTitle)")
          && Code("UI/WindowTabs.cs").Contains("UiKit.OwnLook ? DrawOwn(inRect, tabs)"), "the tab and the floating window draw the same contents, never both open");
        string trade = Code("Trade/Window_RimfolkTrade.cs");
        C(trade.Contains("class Window_RimfolkTrade : FramedWindow") && trade.Contains("base(\"Trade\", LeastW, LeastH, 0f)") && trade.Contains("protected override void DoFramedContents(Rect inRect)"),
            "the trade window in the frame, remembered as Trade");
        C(trade.Contains("UiKit.Button(accept, \"AcceptButton\".Translate(), ButtonKind.Primary)") && trade.Contains("UiKit.SearchField(") && trade.Contains("UiKit.Toggle(inDeal"),
            "Accept the primary act; the search and the switch in the kit");
        C(trade.Contains("UiKit.SoftShadow(r, 0.7f)") && trade.Contains("private static Color Short =>"), "the silver box a card of its own, its warning in words and colour");
        C(!Regex.IsMatch(trade, @"labels = cats\.Select\(") && trade.Contains("chipLabels.Add(l);"), "the chips' words made once a trade, not each frame");
        // Nothing made per frame where the kit draws.
        string skin = Code("UI/UiKitSkin.cs"), math = Code("UI/SkinMath.cs");
        string draw9 = Regex.Match(skin, @"private static void Draw9\(.*?\n        \}", RegexOptions.Singleline).Value;
        C(draw9.Length > 0 && !Regex.IsMatch(draw9, @"new (?!Rect\()"), "a rounded rect draws without making anything");
        string slice = Regex.Match(math, @"public static bool Slice\(.*?\n        \}", RegexOptions.Singleline).Value;
        C(slice.Length > 0 && !slice.Contains("new "), "the 9-slice's pieces are worked out without making anything");
        string scroll = Regex.Match(skin, @"private static void Scrollbars\(bool on\).*?\n        \}", RegexOptions.Singleline).Value;
        C(scroll.Length > 0 && !Regex.IsMatch(scroll, @"new (GUIStyle|Texture2D|\w+\[)"), "the scrollbars' styles swap without making anything");
        C(skin.Contains("private static readonly Slice9[] fills = MakeSet(false, false);") && skin.Contains("tex.Apply(false, true);"), "the textures made once at load, kept on the card");
        C(!Regex.IsMatch(math + Code("UI/WindowFrameMath.cs"), @"using (Verse|RimWorld|UnityEngine)"), "the rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("UI/WindowFrameMath.cs") && File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("UI/SkinMath.cs"),
            "and are linked into the tests");
        // Every control draws the game's look when Rimfolk's is off.
        foreach (var part in new[] { "public static bool Button(", "public static string TextField(", "public static bool Toggle(", "public static void RowLit(", "public static void RowHover(", "public static bool IconPlate(" })
        {
            string body = Regex.Match(skin, Regex.Escape(part) + @".*?\n        \}", RegexOptions.Singleline).Value;
            C(body.Contains("if (!own") , $"{part.TrimEnd('(')}: the game's look when Rimfolk's is off");
        }
    }
}
