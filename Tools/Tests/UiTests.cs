using ColonyCouncil;

/// <summary>
/// Rimfolk's inspect tabs (Mood, Story, Culture) fit above the inspect pane: on 1080 and 768 pixel screens, at the game's
/// interface scales, under the plain pane and RimHUD's taller ones, the whole tab stays on screen whenever there's any
/// sensible room, and keeps its full height where there is. Rimfolk's column in the game's Social tab stays on screen
/// too, is never drawn narrower than its least, and leaves the game's own width alone when another mod set one.
/// </summary>
public static class UiTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        float[] screens = { 768f, 900f, 1080f, 1440f, 2160f };
        float[] scales = { 1f, 1.25f, 1.5f, 1.75f, 2f };
        // The plain pane, RimHUD's default, and RimHUD with rows added.
        float[] panes = { TabFitMath.VanillaPane, 232f, 320f, 400f, 500f };
        float[] wanted = { 520f, 540f };
        foreach (var px in screens)
        foreach (var scale in scales)
        foreach (var pane in panes)
        foreach (var want in wanted)
        {
            cases++;
            float top = TabFitMath.PaneTopY(px, scale, pane);
            float h = TabFitMath.Height(want, top);
            string at = $"{px}px at {scale}x, pane {pane}, wanting {want}";
            check(h <= want, $"never taller than wanted ({at}: {h})");
            check(h >= Math.Min(want, TabFitMath.Floor), $"never below the floor ({at}: {h})");
            if (TabFitMath.Room(top) >= TabFitMath.Floor)
                check(TabFitMath.TabTop(h, top) >= TabFitMath.TopMargin - 0.01f, $"on screen ({at}: top {TabFitMath.TabTop(h, top)})");
            if (TabFitMath.Room(top) >= want) check(h == want, $"full height where there's room ({at}: {h})");
        }

        // The cases that went wrong before: RimHUD's default pane on a 768 pixel screen, and a 1080 pixel screen at 1.5x.
        float smallHud = TabFitMath.PaneTopY(768f, 1f, 232f);
        check(TabFitMath.Height(540f, smallHud) == 463f, "768p under RimHUD: the Mood tab is 463 tall");
        check(TabFitMath.TabTop(TabFitMath.Height(520f, smallHud), smallHud) == TabFitMath.TopMargin, "768p under RimHUD: the Story tab's top is on screen");
        float scaled = TabFitMath.PaneTopY(1080f, 1.5f, 232f);
        check(TabFitMath.Height(540f, scaled) == 415f, "1080p at 1.5x under RimHUD: 415 tall");
        // And where there's plenty of room nothing changes.
        check(TabFitMath.Height(540f, TabFitMath.PaneTopY(1080f, 1f, TabFitMath.VanillaPane)) == 540f, "1080p, plain pane: full height");
        check(TabFitMath.Height(540f, TabFitMath.PaneTopY(1080f, 1f, 400f)) == 540f, "1080p, RimHUD at 400: full height");

        cases += SocialTabColumn(check);
        return cases;
    }

    /// <summary>The Social tab's width with Rimfolk's column, on common screens at every interface scale.</summary>
    private static int SocialTabColumn(Action<bool, string> check)
    {
        int cases = 0;
        float baseW = TabFitMath.SocialWidth;
        float[] widths = { 1280f, 1366f, 1600f, 1920f, 2560f, 3840f };
        float[] scales = { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f };
        foreach (var px in widths)
        foreach (var scale in scales)
        {
            cases++;
            float screen = px / scale;
            string at = $"{px}px at {scale}x";
            float col = TabFitMath.ColumnWidth(screen, baseW);
            float tab = TabFitMath.WithColumn(baseW, col);
            check(col == 0f || col >= TabFitMath.ColumnMin, $"column never narrower than its least ({at}: {col})");
            check(col <= TabFitMath.ColumnPreferred, $"column never wider than wanted ({at}: {col})");
            check(col == 0f || tab <= screen - TabFitMath.ScreenMargin + 0.01f, $"the wider tab stays on screen ({at}: {tab} of {screen})");
            check(col == 0f ? tab == baseW : TabFitMath.ColumnFits(tab, baseW), $"the tab it asks for has the column's room ({at})");
            if (screen - TabFitMath.ScreenMargin - baseW - TabFitMath.ColumnGap >= TabFitMath.ColumnPreferred)
                check(col == TabFitMath.ColumnPreferred, $"full column where there's room ({at}: {col})");
        }
        // The numbers in TabFitMath's notes.
        check(TabFitMath.WithColumn(baseW, TabFitMath.ColumnWidth(1920f, baseW)) == 912f, "1920 wide: the tab is 912");
        check(TabFitMath.ColumnWidth(1920f / 2f, baseW) == 360f, "1080p at 2x: a full column");
        check(Math.Abs(TabFitMath.ColumnWidth(1366f / 1.5f, baseW) - 350.67f) < 0.1f, "1366 at 1.5x: a 350 column");
        check(TabFitMath.ColumnWidth(1280f / 2f, baseW) == 0f, "1280 at 2x: no column");
        // A window kept at the game's width (another mod's, or the first frame) has no room: nothing is drawn there.
        check(!TabFitMath.ColumnFits(baseW, baseW), "the game's own width has no room for the column");
        check(!TabFitMath.ColumnFits(baseW + 200f, baseW), "a little wider isn't room enough");
        // The tab's own width: found the first time, kept while the tab is at Rimfolk's width, and another mod's taken as it comes.
        check(TabFitMath.BaseWidth(540f, -1f, -1f) == 540f, "base: the game's width the first time");
        check(TabFitMath.BaseWidth(912f, 912f, 540f) == 540f, "base: Rimfolk's own width isn't mistaken for the tab's");
        check(TabFitMath.BaseWidth(540f, 540f, 540f) == 540f, "base: back at the game's width");
        check(TabFitMath.BaseWidth(620f, 912f, 540f) == 620f, "base: another mod's width becomes the base");
        check(TabFitMath.BaseWidth(620f + TabFitMath.ColumnGap + 360f, 620f + TabFitMath.ColumnGap + 360f, 620f) == 620f, "base: and stays it with the column beside it");
        return cases + 12;
    }
}
