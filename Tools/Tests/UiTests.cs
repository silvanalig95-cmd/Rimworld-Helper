using ColonyCouncil;

/// <summary>
/// Rimfolk's inspect tabs (Mood, Story) fit above the inspect pane: on 1080 and 768 pixel screens, at the game's
/// interface scales, under the plain pane and RimHUD's taller ones, the whole tab stays on screen whenever there's any
/// sensible room, and keeps its full height where there is.
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
        return cases;
    }
}
