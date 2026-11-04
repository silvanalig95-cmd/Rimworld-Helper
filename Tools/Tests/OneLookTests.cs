using ColonyCouncil;

/// <summary>
/// The interface's fifth release, one look (docs/design/interface.md, UI 5): the second level of previews (PreviewStack:
/// a link inside a preview shows one more, two levels at most, the first kept while the second shows, both gone when the
/// mouse leaves, none while busy, a new first takes the second away) and where they go (PreviewMath: beside the first,
/// never over it, always on the screen); and the mod's side: the defense window draws its view with the drawer and the
/// view header, its sections with the design's components, names as links; the inspect tabs and the Social column draw
/// names as links and keep the game's own text plain; the window size setting.
/// </summary>
public static class OneLookTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "one look: " + what);
        }

        // ---------------- Two levels of previews ----------------
        var a = ThingRef.From(Ref.Person(12));
        var b = ThingRef.From(Ref.Town(55));
        var c = ThingRef.From(Ref.People(3));
        var d = ThingRef.From(Ref.Person(40));
        var s = new PreviewStack();
        float t = 0f;
        int f = 0;
        float mx = 0f, my = 0f;
        void Frame(int from, ThingRef r, ScreenBox at, bool busy = false)
        {
            f++;
            t += 0.05f;
            if (!r.IsNone) s.Hover(from, r, at, t, f);
            s.Tick(t, f, busy, 0.35f, mx, my, 1920f, 1080f, _ => 150f);
        }
        void Frames(int count, int from, ThingRef r, ScreenBox at, bool busy = false)
        {
            for (int i = 0; i < count; i++) Frame(from, r, at, busy);
        }

        var linkA = new ScreenBox(100f, 100f, 60f, 18f);
        mx = 110f;
        my = 105f;
        Frames(4, 0, a, linkA);
        C(s.Shown(1).IsNone, "no preview before the pause");
        Frames(4, 0, a, linkA);
        C(s.Shown(1) == a && s.Shown(2).IsNone, "the first preview after the pause");
        var r1 = s.RectOf(1);
        C(r1.y >= linkA.YMax && r1.x <= linkA.x, "the first under its link");
        // Into the first, onto a link there.
        var linkB = new ScreenBox(r1.x + 30f, r1.y + 80f, 50f, 18f);
        mx = linkB.x + 5f;
        my = linkB.y + 5f;
        Frames(10, 1, b, linkB);
        C(s.Shown(1) == a && s.Shown(2) == b, "a link inside the first shows a second beside it, the first kept");
        var r2 = s.RectOf(2);
        C(!r1.Overlaps(r2) && r2.x >= r1.XMax, "the second right of the first, never over it");
        C(s.Hover(2, c, new ScreenBox(r2.x + 10f, r2.y + 60f, 40f, 18f), t, f) == 0, "a link inside the second shows no third");
        // Into the second, no link: both stay.
        mx = r2.x + 100f;
        my = r2.y + 60f;
        Frames(20, 0, ThingRef.None, default);
        C(s.Shown(1) == a && s.Shown(2) == b, "the mouse in the second keeps both");
        // Back into the first, off its links: the second goes, the first stays.
        mx = r1.x + 200f;
        my = r1.y + 20f;
        Frames(12, 0, ThingRef.None, default);
        C(s.Shown(1) == a && s.Shown(2).IsNone, "back in the first, the second goes half a second later");
        // Away from both: the first goes too.
        mx = 1500f;
        my = 900f;
        Frames(12, 0, ThingRef.None, default);
        C(!s.Any && s.Shown(2).IsNone, "the mouse away from both: both gone");
        // A new first takes the second away.
        mx = 110f;
        my = 105f;
        Frames(10, 0, a, linkA);
        r1 = s.RectOf(1);
        linkB = new ScreenBox(r1.x + 30f, r1.y + 80f, 50f, 18f);
        mx = linkB.x + 5f;
        my = linkB.y + 5f;
        Frames(10, 1, b, linkB);
        C(s.Shown(2) == b, "the second again");
        var linkD = new ScreenBox(700f, 600f, 50f, 18f);
        mx = 705f;
        my = 605f;
        Frames(14, 0, d, linkD);
        C(s.Shown(1) == d && s.Shown(2).IsNone, "a new first takes the second with it");
        // Busy (dragging, scrolling): nothing new shows.
        s.Hide();
        Frames(3, 0, ThingRef.None, default);
        mx = 110f;
        my = 105f;
        Frames(12, 0, a, linkA, true);
        C(!s.Any, "none while dragging or scrolling");
        // A card opened from a name: quiet until the mouse leaves the name.
        Frames(10, 0, a, linkA);
        C(s.Shown(1) == a, "shown once not busy");
        s.Hide();
        Frames(12, 0, a, linkA);
        C(!s.Any, "no preview again for the name whose card just opened");
        Frames(3, 0, ThingRef.None, default);
        Frames(10, 0, a, linkA);
        C(s.Shown(1) == a, "after the mouse left the name and came back, its preview again");
        // The second needs a first.
        var fresh = new PreviewStack();
        C(fresh.Hover(1, b, linkB, 1f, 1) == 0, "no second without a first");
        C(s.Shown(1) == a && s.Hover(1, a, linkA, t, f) == 0, "a name inside the first that is its own thing shows no second of it");

        // ---------------- Where previews go ----------------
        var parent = new ScreenBox(1500f, 400f, PreviewMath.Width, 160f);
        var left = PreviewMath.Beside(parent, new ScreenBox(1530f, 470f, 40f, 18f), 150f, 1920f, 1080f);
        C(left.XMax <= parent.x && !left.Overlaps(parent), "no room on the right: the second on the left");
        var narrow = new ScreenBox(200f, 300f, PreviewMath.Width, 160f);
        var under = PreviewMath.Beside(narrow, new ScreenBox(230f, 370f, 40f, 18f), 150f, 560f, 700f);
        C(under.y >= 370f + 18f && under.x >= PreviewMath.Edge && under.XMax <= 560f - PreviewMath.Edge + 0.01f, "no room either side: under its link, on the screen");
        foreach (var (sw, sh) in new[] { (1024f, 768f), (1280f, 720f), (1920f, 1080f), (683f, 384f) })
            foreach (var (ax, ay) in new[] { (5f, 5f), (sw - 60f, 5f), (5f, sh - 30f), (sw - 60f, sh - 30f), (sw / 2f, sh / 2f) })
            {
                var p = PreviewMath.Place(new ScreenBox(ax, ay, 50f, 18f), 200f, sw, sh);
                C(p.x >= PreviewMath.Edge - 0.01f && p.XMax <= sw - PreviewMath.Edge + 0.01f && p.y >= PreviewMath.Edge - 0.01f && p.YMax <= sh - PreviewMath.Edge + 0.01f,
                    $"{sw}x{sh}: a preview for a link at {ax:0},{ay:0} stays on the screen");
                var q = PreviewMath.Beside(p, new ScreenBox(p.x + 20f, p.y + 60f, 50f, 18f), 180f, sw, sh);
                C(q.x >= PreviewMath.Edge - 0.01f && q.XMax <= sw - PreviewMath.Edge + 0.01f && q.y >= PreviewMath.Edge - 0.01f && q.YMax <= sh - PreviewMath.Edge + 0.01f,
                    $"{sw}x{sh}: its second stays on the screen");
            }
        var tall = PreviewMath.Fit(new ScreenBox(10f, 900f, PreviewMath.Width, 100f), 200f, 1080f);
        C(tall.YMax <= 1080f - PreviewMath.Edge + 0.01f, "a preview grown taller is kept on the screen");

        // ---------------- The mod's side ----------------
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string previews = Code("UI/Cards/Previews.cs");
        C(previews.Contains("PreviewStack") && previews.Contains("WindowId + level") && previews.Contains("GenUI.AbsorbClicksInRect"),
            "the previews draw both levels as their own windows and keep their clicks");
        string defense = Code("UI/MainTabWindow_Defenses.cs");
        C(defense.Contains("Drawer.Around(") && defense.Contains("ViewHead.Draw(") && defense.Contains("Nav.Observe(RimfolkWindow.Defense, \"Defenses\", Drawer.Current(RimfolkWindow.Defense))"),
            "the defense window draws its view with the drawer and the view header, and the way back knows its card");
        C(PagesMath.ArticleOf("Defenses") == "Rimfolk_AlmDef_Overview", "the defense view's \"?\" opens its Almanac page");
        string panel = Code("UI/DefensePanel.cs");
        C(panel.Contains("UiKit.SectionHeading(") && panel.Contains("UiKit.ActButton(") && panel.Contains("UiKit.EmptyState(") && panel.Contains("PaneLinks.Mark("),
            "the defense page's sections fold with their counts, its acts say why not, names are links");
        C(!panel.Contains("Messages.Message(reason"), "an act that can't be done is greyed with why, not refused after the click");
        foreach (var tab in new[] { "UI/SocialColumn.cs", "UI/ITab_Pawn_Story.cs", "UI/ITab_Pawn_Culture.cs" })
            C(Code(tab).Contains("PaneLinks.") || Code(tab).Contains("ThingRefs.Link("), $"{tab}: names drawn as links");
        string pane = Code("UI/PawnPane.cs");
        C(pane.Contains("LinkText.Draw(") && pane.Contains("LinkText.Height("), "a pane's rows draw their links");
        C(Code("UI/SocialColumn.cs").Contains("e.ToGameStringFromPOV(p)") && !Code("UI/SocialColumn.cs").Contains("PaneLinks.Mark(e.ToGameStringFromPOV"),
            "the game's own play log lines stay plain");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public WindowSizeChoice windowSize = WindowSizeChoice.Standard;") && Code("UI/SettingsCatalog.cs").Contains("Choice(\"windowSize\""),
            "the window size: standard at first, in the settings");
        C(Code("UI/WindowTabs.cs").Contains("WindowLayoutMath.Size(UI.screenWidth, UI.screenHeight, large"), "the windows ask for the size the setting gives");
        return n;
    }
}
