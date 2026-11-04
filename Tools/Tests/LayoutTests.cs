using ColonyCouncil;

/// <summary>
/// Rimfolk's four windows across screens (docs/design/interface.md, "Screens and interface scale", and decision 6), like
/// TabFitMath's checks for the inspect tabs: on every screen from 768 to 2160 pixels high at every interface scale up to 2,
/// at the standard size and the large one (WindowLayoutMath), the window stays on the screen; its top row has room for
/// five views and the whole corner (back, forward, search, the inbox, following, the Almanac's book and the four windows),
/// each tab at least its icon and, at 1010 wide, its icon and its name; the drawer sits beside the view from a content area
/// of 900 and over it below, always inside the window; the region map keeps a useful size beside the drawer or under it;
/// and the People table keeps its first columns beside a card.
/// </summary>
public static class LayoutTests
{
    /// <summary>The longest view name a tab carries ("Here and away"), about this wide at the Small font.</summary>
    private const float LongestViewName = 90f;

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "layout: " + what);
        }

        // The screens the game runs on, 768 to 2160 pixels high (4:3, 16:9, 16:10, 21:9), and its scales up to 2.
        var screens = new[]
        {
            (1024f, 768f), (1366f, 768f), (1280f, 800f), (1440f, 900f), (1600f, 900f), (1280f, 1024f), (1680f, 1050f), (1920f, 1080f),
            (2560f, 1080f), (1920f, 1200f), (2560f, 1440f), (3440f, 1440f), (2560f, 1600f), (3840f, 2160f)
        };
        var scales = new[] { 1f, 1.25f, 1.5f, 1.75f, 2f };
        float corner = WindowLayoutMath.CornerWidth();
        C(corner >= 300f && corner <= 330f, $"the whole corner is about 310 wide ({corner:0})");
        C(WindowLayoutMath.CornerWidth(3, 4) == 226f, "today's corner (back, forward, the book, the four windows) measures as WindowTabs does");

        var cx = new float[CouncilLayoutMath.PeopleMin.Length];
        var cw = new float[CouncilLayoutMath.PeopleMin.Length];
        var shown = new bool[CouncilLayoutMath.PeopleMin.Length];
        foreach (var (pw, ph) in screens)
            foreach (float scale in scales)
                foreach (bool large in new[] { false, true })
                {
                    float uw = pw / scale, uh = ph / scale;
                    string at = $"{pw}x{ph} at {scale} ({(large ? "large" : "standard")})";
                    WindowLayoutMath.Size(uw, uh, large, out float w, out float h);

                    // ---- The window on the screen ----
                    C(w <= uw - WindowLayoutMath.EdgeX + 0.01f && h <= uh - WindowLayoutMath.BottomBar - WindowLayoutMath.EdgeY + 0.01f,
                        $"{at}: the window stays on the screen ({w:0} by {h:0})");
                    C(w <= (large ? WindowLayoutMath.LargeW : WindowLayoutMath.StandardW) && h <= (large ? WindowLayoutMath.LargeH : WindowLayoutMath.StandardH),
                        $"{at}: never larger than its size");
                    WindowLayoutMath.Size(uw, uh, false, out float sw, out float sh);
                    if (large) C(w >= sw && h >= sh, $"{at}: large is never smaller than standard");
                    bool room = uw >= (large ? WindowLayoutMath.LargeW : WindowLayoutMath.StandardW) + WindowLayoutMath.EdgeX
                                && uh >= (large ? WindowLayoutMath.LargeH : WindowLayoutMath.StandardH) + WindowLayoutMath.EdgeY + WindowLayoutMath.BottomBar;
                    if (room) C(w == (large ? 1280f : 1010f) && h == (large ? 860f : 720f), $"{at}: the full size where the screen has the room");

                    // ---- The top row: five views and the whole corner ----
                    float tabsRoom = WindowLayoutMath.TabsRoom(w, corner);
                    C(tabsRoom >= WindowLayoutMath.TabsMin, $"{at}: the tabs and the corner never meet ({tabsRoom:0} for the tabs)");
                    float tab5 = WindowLayoutMath.TabWidth(tabsRoom, WindowLayoutMath.MostViews);
                    C(WindowLayoutMath.IconFits(tab5), $"{at}: five tabs show at least their icons ({tab5:0} each)");
                    if (w >= WindowLayoutMath.StandardW)
                        C(tab5 >= 135f && WindowLayoutMath.NameFits(tab5, LongestViewName), $"{at}: at 1010 or more five tabs show icon and name ({tab5:0} each)");
                    float tab4 = WindowLayoutMath.TabWidth(tabsRoom, 4);
                    if (w >= 900f) C(WindowLayoutMath.NameFits(tab4, LongestViewName), $"{at}: four views show their names from 900 wide ({tab4:0} each)");

                    // ---- The drawer beside or over the view ----
                    float content = WindowLayoutMath.ContentWidth(w), contentH = WindowLayoutMath.ContentHeight(h);
                    C(content > 0f && contentH > 0f, $"{at}: a content area under the tabs");
                    DrawerMath.Split(0f, content, out _, out float viewW, out float dx, out float dw, out bool over);
                    C(dx >= -0.01f && dx + dw <= content + 0.01f, $"{at}: the drawer stays inside the window ({dw:0} of {content:0})");
                    C(over == !DrawerMath.Beside(content), $"{at}: beside from 900, over the view below");
                    if (!over) C(viewW >= 500f && viewW + DrawerMath.Gap + dw <= content + 0.01f, $"{at}: the view keeps {viewW:0} beside the drawer");
                    else C(content - dw >= Math.Min(DrawerMath.OverMargin, content - Math.Min(DrawerMath.Narrowest, content)) - 0.01f && dw >= Math.Min(DrawerMath.Narrowest, content) - 0.01f,
                        $"{at}: over the view, the drawer leaves its edge in sight ({dw:0} of {content:0})");

                    // ---- The region map with its panel ----
                    WindowLayoutMath.RegionMap(content, contentH, out float mapW, out float mapH, out bool mapOver);
                    C(mapOver == over, $"{at}: the map's panel is the drawer, beside or over as the drawer is");
                    C(mapW >= 400f, $"{at}: the map at least 400 wide ({mapW:0})");
                    C(mapH >= 150f, $"{at}: the map at least 150 tall ({mapH:0})");
                    if (uh >= 540f) C(mapH >= 300f, $"{at}: the map at least 300 tall where the screen is 540 or more ({mapH:0})");
                    if (!over) C(mapW >= 500f, $"{at}: the map at least 500 wide beside the panel ({mapW:0})");

                    // ---- The People table beside a colonist's card ----
                    CouncilLayoutMath.Layout((over ? content : viewW) - 16f, CouncilLayoutMath.PeopleMin, CouncilLayoutMath.PeopleWeight, CouncilLayoutMath.PeoplePriority, cx, cw, shown);
                    C(shown[0] && shown[1] && shown[3], $"{at}: the People table keeps the name, mood and trust beside a card");
                }

        // The numbers in the design: at 1920 by 1080, scale 1, the map 558 wide beside a 380 drawer; large, 828.
        WindowLayoutMath.Size(1920f, 1080f, false, out float w1, out float h1);
        WindowLayoutMath.RegionMap(WindowLayoutMath.ContentWidth(w1), WindowLayoutMath.ContentHeight(h1), out float m1, out float mh1, out bool o1);
        C(w1 == 1010f && h1 == 720f && !o1 && Math.Abs(m1 - 558f) < 0.01f, "1080p standard: the map 558 wide beside the drawer");
        WindowLayoutMath.Size(1920f, 1080f, true, out float w2, out float h2);
        WindowLayoutMath.RegionMap(WindowLayoutMath.ContentWidth(w2), WindowLayoutMath.ContentHeight(h2), out float m2, out float mh2, out bool o2);
        C(w2 == 1280f && h2 == 860f && !o2 && Math.Abs(m2 - 828f) < 0.01f && mh2 > mh1 + 139f, "1080p large: the map 828 wide and 140 taller");
        // At 1080p and scale 1.5 the window is 1010 by 665 and the drawer still beside; large can't grow there.
        WindowLayoutMath.Size(1280f, 720f, false, out float w3, out float h3);
        WindowLayoutMath.Size(1280f, 720f, true, out float w4, out float h4);
        C(w3 == 1010f && h3 == 665f && DrawerMath.Beside(WindowLayoutMath.ContentWidth(w3)), "1080p at 1.5: 1010 by 665, the drawer beside");
        C(w4 == 1260f && h4 == 665f, "1080p at 1.5, large: as wide as the screen allows, no taller");
        // The drawer's own window size is the standard one.
        DrawerMath.WindowSize(1366f, 768f, out float dw1, out float dh1);
        WindowLayoutMath.Size(1366f, 768f, false, out float lw1, out float lh1);
        C(dw1 == lw1 && dh1 == lh1, "the drawer's window size is WindowLayoutMath's standard size");
        return n;
    }
}
