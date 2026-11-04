using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's four windows (Source/ColonyCouncil/UI/PagesMath.cs): every page in one window and view, laid out as the
/// player asked (the council in four views, This week, People, Council, Colony; the planning window's Plan, Base, Making,
/// Rooms), no window over five views and no view over five modes; every old page name (the Roster, Everyone, Dreams,
/// Journals, Priorities, the Almanac, the council window's World, Defenses, Planning and History pages and sections)
/// still leads somewhere, to its view and mode or the Almanac's window; every view's "?" opens an Almanac article; a
/// view's header and the People table beside the drawer; every page name the code opens is a page; the bottom bar
/// buttons are the four windows (the planning button in the rooms button's def), and the rooms and planning buttons'
/// settings merge as they should. The region window's History page: its kinds in words, its filters.
/// </summary>
public static class PagesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "windows: " + what);
        }

        // ---------------- Every page in one place ----------------
        var names = PagesMath.All.Select(p => p.name).ToList();
        C(names.Distinct().Count() == names.Count, "no page is in two places");
        foreach (var p in PagesMath.All)
        {
            C(PagesMath.Resolve(p.name) == p.name, $"{p.name} is its own address");
            C(PagesMath.WindowOf(p.name) == p.window && PagesMath.SectionOf(p.name) == p.section, $"{p.name}: its window and view");
            if (p.window == RimfolkWindow.Council) C(PagesMath.CouncilSections.Contains(p.section), $"{p.name} is in one of the council window's views");
            else if (!PagesMath.HasViews(p.window)) C(p.section == PagesMath.OnlySection(p.window), $"{p.name}: a window with no views of its own");
        }
        foreach (var s in PagesMath.CouncilSections) C(PagesMath.PagesIn(RimfolkWindow.Council, s).Count > 0, $"the council window's {s} view has pages");
        foreach (RimfolkWindow w in Enum.GetValues(typeof(RimfolkWindow)))
        {
            C(PagesMath.PagesIn(w).Count > 0, $"the {w} window has pages");
            C(PagesMath.FirstOf(w) == PagesMath.PagesIn(w)[0], $"the {w} window opens on its first page");
            C(PagesMath.CornerOrder.Contains(w), $"the {w} window is in every window's corner");
        }
        C(PagesMath.CornerOrder.Length == 4, "four windows in the corner");

        // ---------------- At most five views to a window, five modes to a view (docs/design/interface.md) ----------------
        foreach (RimfolkWindow w in Enum.GetValues(typeof(RimfolkWindow)))
        {
            var views = PagesMath.ViewsOf(w);
            // The region window's pages are one section until its own release makes them four views (UI 2); then it
            // joins this check like the others.
            if (w == RimfolkWindow.World && views.Count == 1) continue;
            C(views.Count <= PagesMath.MaxViews, $"the {w} window has at most five views ({views.Count})");
            foreach (var v in views)
                C(PagesMath.ModesOf(w, v).Count <= PagesMath.MaxModes, $"the {w} window's {v} view has at most five modes ({PagesMath.ModesOf(w, v).Count})");
        }
        C(PagesMath.HasViews(RimfolkWindow.Council) && PagesMath.HasViews(RimfolkWindow.Planning) && !PagesMath.HasViews(RimfolkWindow.Defense),
            "the council and planning windows are made of views; the defense window is one");

        // ---------------- The player's layout (4 November 2026, the council in four views) ----------------
        bool Same(IEnumerable<string> a, params string[] b) => a.SequenceEqual(b);
        C(PagesMath.CouncilSections.SequenceEqual(new[] { "Week", "People", "Council", "Colony" }) && Same(PagesMath.ViewsOf(RimfolkWindow.Council), "Week", "People", "Council", "Colony"),
            "the council window: This week, People, Council, Colony, in that order, one row of tabs");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Week"), "Week"), "This week is one view without modes");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "People"), "Table", "Moods", "Bonds", "Faiths", "Houses"), "People: table, moods, bonds, ideoligions, houses");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Council"), "Decrees", "Ambition", "Ways", "Wishes", "Dilemmas"),
            "Council: offices and decrees, ambition, ways, wishes, dilemmas");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Colony"), "Overview", "Calendar", "Chronicle"), "Colony: overview, calendar, chronicle");
        C(Same(PagesMath.PagesIn(RimfolkWindow.World), "Map", "Neighbours", "Peoples", "Letters", "Journeys", "Events", "WorldHistory"),
            "the region window: the map first, then neighbours, peoples, letters and calls, journeys, world events, the world's history");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Defense), "Defenses"), "the defense window has the Defenses page, and only there");
        C(Same(PagesMath.ViewsOf(RimfolkWindow.Planning), "Plan", "Base", "Making", "Rooms"), "the planning window: Plan, Base, Making, Rooms");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Planning, "Plan"), "Plan", "Priorities"), "Plan: the plan, and the advisor's priorities joined to it");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Planning, "Rooms"), "Build", "Rooms"), "Rooms: the list of room plans, every room at a glance");

        // ---------------- Old addresses ----------------
        // Every page before this round, by the names the code, the settings and the keys used: its view and mode now.
        var old = new Dictionary<string, (string page, RimfolkWindow window, string view)>
        {
            ["Week"] = ("Week", RimfolkWindow.Council, "Week"), ["Overview"] = ("Overview", RimfolkWindow.Council, "Colony"),
            ["Priorities"] = ("Priorities", RimfolkWindow.Planning, "Plan"), ["Moods"] = ("Moods", RimfolkWindow.Council, "People"),
            ["World"] = ("Neighbours", RimfolkWindow.World, "World"), ["Defenses"] = ("Defenses", RimfolkWindow.Defense, "Defense"),
            ["Plan"] = ("Plan", RimfolkWindow.Planning, "Plan"), ["Base"] = ("Base", RimfolkWindow.Planning, "Base"),
            ["Making"] = ("Making", RimfolkWindow.Planning, "Making"), ["Build"] = ("Build", RimfolkWindow.Planning, "Rooms"),
            ["Rooms"] = ("Rooms", RimfolkWindow.Planning, "Rooms"), ["Roster"] = ("Table", RimfolkWindow.Council, "People"),
            ["Everyone"] = ("Table", RimfolkWindow.Council, "People"), ["Bonds"] = ("Bonds", RimfolkWindow.Council, "People"),
            ["Faiths"] = ("Faiths", RimfolkWindow.Council, "People"), ["Wishes"] = ("Wishes", RimfolkWindow.Council, "Council"),
            ["Decrees"] = ("Decrees", RimfolkWindow.Council, "Council"), ["Offices"] = ("Decrees", RimfolkWindow.Council, "Council"),
            ["Ambition"] = ("Ambition", RimfolkWindow.Council, "Council"), ["Ways"] = ("Ways", RimfolkWindow.Council, "Council"),
            ["Dilemmas"] = ("Dilemmas", RimfolkWindow.Council, "Council"), ["Chronicle"] = ("Chronicle", RimfolkWindow.Council, "Colony"),
            ["Calendar"] = ("Calendar", RimfolkWindow.Council, "Colony"), ["Dreams"] = ("Chronicle", RimfolkWindow.Council, "Colony"),
            ["Journals"] = ("Chronicle", RimfolkWindow.Council, "Colony"), ["RegionMap"] = ("Map", RimfolkWindow.World, "World"),
            ["History"] = ("Chronicle", RimfolkWindow.Council, "Colony"),
            // A view's name opens it (on the mode last looked at in the game; its first here).
            ["People"] = ("Table", RimfolkWindow.Council, "People"), ["Council"] = ("Decrees", RimfolkWindow.Council, "Council"),
            ["Colony"] = ("Overview", RimfolkWindow.Council, "Colony")
        };
        foreach (var kv in old)
        {
            C(PagesMath.Resolve(kv.Key) == kv.Value.page, $"the old page {kv.Key} is {kv.Value.page} now");
            C(PagesMath.WindowOf(kv.Key) == kv.Value.window, $"the old page {kv.Key} opens the {kv.Value.window} window");
            C(PagesMath.SectionOf(kv.Key) == kv.Value.view, $"the old page {kv.Key} opens its {kv.Value.view} view");
        }
        C(PagesMath.FilterOf("Dreams") == "Dreams" && PagesMath.FilterOf("Journals") == "Journals" && PagesMath.FilterOf("Chronicle") == null && PagesMath.FilterOf("Roster") == null,
            "dreams and journals open the Chronicle on their filter; the rest open as they are");
        // The Almanac: no window's page, a window of its own, and still an address.
        C(PagesMath.IsPage("Almanac") && PagesMath.IsAlmanac("almanac") && PagesMath.Resolve("Almanac") == PagesMath.Almanac && PagesMath.WindowOf("Almanac") == null
            && !names.Contains("Almanac"), "the Almanac is an address of its own window, no window's page");
        C(PagesMath.Resolve("decrees") == "Decrees" && PagesMath.Resolve("WORLD") == "Neighbours" && PagesMath.Resolve("roster") == "Table", "names are read in any case");
        C(!PagesMath.IsPage(null) && !PagesMath.IsPage("") && !PagesMath.IsPage("Nowhere") && PagesMath.WindowOf("Nowhere") == null, "a name that is no page is none");
        C(PagesMath.IsView("People", out var vw, out var vv) && vw == RimfolkWindow.Council && vv == "People" && PagesMath.IsView("Rooms", out var vw2, out _) && vw2 == RimfolkWindow.Planning
            && !PagesMath.IsView("World", out _, out _) && !PagesMath.IsView("Defense", out _, out _) && !PagesMath.IsView("Roster", out _, out _),
            "views by name: the council's and the planning window's, not a window of one view");
        foreach (var kv in PagesMath.Moved)
        {
            C(!names.Contains(kv.Key, StringComparer.OrdinalIgnoreCase), $"the old name {kv.Key} is no page's own");
            C(names.Contains(kv.Value), $"the old name {kv.Key} leads to a page");
        }
        foreach (var kv in PagesMath.MovedFilters) C(PagesMath.Moved.ContainsKey(kv.Key), $"the old name {kv.Key} that is a filter now also leads to its page");

        // ---------------- Every view's "?" leads to an Almanac article ----------------
        var articles = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements())
            {
                string dn = d.Element("defName")?.Value;
                if (dn != null) articles.Add(dn);
            }
        foreach (var w in new[] { RimfolkWindow.Council, RimfolkWindow.Planning })
            foreach (var p in PagesMath.PagesIn(w))
            {
                string a = PagesMath.ArticleOf(p);
                C(a != null && articles.Contains(a), $"the {p} page's \"?\" opens an Almanac article ({a ?? "none"})");
            }
        foreach (var kv in PagesMath.Articles) C(names.Contains(kv.Key), $"the article for {kv.Key} is a page's");
        C(PagesMath.ArticleOf("Roster") == PagesMath.ArticleOf("Table"), "an old name's \"?\" is its page's");

        // ---------------- A view's header: the modes keep their place, the "?" left of the page's own buttons ----------------
        C(ViewHeadMath.Height(false) == 56f && ViewHeadMath.Height(true) == 56f + ViewHeadMath.ModeRow, "a header is as before, a row taller with modes");
        C(ViewHeadMath.Height(true) <= 56f + 38f, "a header with modes is no taller than the old header and its row of sub-tabs");
        var nat = new[] { 70f, 66f, 64f, 98f, 70f };
        var mx = new float[5];
        var mw = new float[5];
        ViewHeadMath.Modes(42f, 900f, nat, 5, mx, mw);
        bool inOrder = true;
        for (int i = 0; i < 5; i++) inOrder &= mw[i] == nat[i] && (i == 0 ? mx[i] == 42f : Math.Abs(mx[i] - (mx[i - 1] + mw[i - 1] + ViewHeadMath.ModeGap)) < 0.01f);
        C(inOrder, "modes that fit sit at their own widths, one after another from where the title begins");
        ViewHeadMath.Modes(42f, 200f, nat, 5, mx, mw);
        C(mx[4] + mw[4] <= 242.01f && mw[0] >= ViewHeadMath.MinMode - 0.01f && Math.Abs(mw[0] - mw[3]) < 0.01f, "modes that don't fit are squeezed evenly within the width");
        ViewHeadMath.Modes(42f, 100f, nat, 5, mx, mw);
        C(mx[4] + mw[4] <= 142.01f && mw.All(x => x >= 0f), "squeezed past their least, the last are cut at the edge");
        ViewHeadMath.Modes(0f, 900f, nat, 3, mx, mw);
        C(mw[2] == nat[2], "only as many modes as asked");
        float hx = ViewHeadMath.HelpX(950f, 330f, 42f, out float statRight);
        C(Math.Abs(hx - (950f - 330f - ViewHeadMath.Help)) < 0.01f && statRight < hx && hx + ViewHeadMath.Help <= 950f - 330f + 0.01f,
            "the \"?\" sits just left of the page's own buttons, the stat left of it");
        hx = ViewHeadMath.HelpX(300f, 400f, 42f, out statRight);
        C(hx >= 42f && statRight >= 42f, "a narrow header keeps the \"?\" right of the icon");

        // ---------------- The drawer in the council window: the People table beside a colonist's card ----------------
        var cx = new float[CouncilLayoutMath.PeopleMin.Length];
        var cw = new float[CouncilLayoutMath.PeopleMin.Length];
        var shown = new bool[CouncilLayoutMath.PeopleMin.Length];
        foreach (var (sw, sh, scale) in new[] { (1920f, 1080f, 1f), (1920f, 1080f, 1.5f), (1366f, 768f, 1f), (2560f, 1440f, 2f), (1280f, 720f, 1f) })
        {
            DrawerMath.WindowSize(sw / scale, sh / scale, out float ww, out _);
            float content = DrawerMath.ContentWidth(ww);
            DrawerMath.Split(0f, content, out _, out float viewW, out _, out _, out bool over);
            // The table scrolls with a bar 16 wide; beside the drawer it has the view, over it the whole content.
            CouncilLayoutMath.Layout((over ? content : viewW) - 16f, CouncilLayoutMath.PeopleMin, CouncilLayoutMath.PeopleWeight, CouncilLayoutMath.PeoplePriority, cx, cw, shown);
            C(shown[0] && shown[1] && shown[3], $"{sw}x{sh} at {scale}: the table keeps the name, mood and trust beside the card ({(over ? "over" : "beside")}, {viewW:0})");
        }

        // Sections by name (a bottom bar button made in XML, MainButtonWorker_Page).
        C(PagesMath.TrySection("People", out var w1, out var s1) && w1 == RimfolkWindow.Council && s1 == "People", "the People view is the council window's");
        C(PagesMath.TrySection("Planning", out var w2, out _) && w2 == RimfolkWindow.Planning, "the old Planning section is the planning window");
        C(PagesMath.TrySection("History", out var w3, out var s3) && w3 == RimfolkWindow.Council && s3 == "Colony", "the old History section opens Colony");
        C(PagesMath.TrySection("Defense", out var w4, out _) && w4 == RimfolkWindow.Defense, "the defense window by name");
        C(PagesMath.TrySection("region", out var w5, out _) && w5 == RimfolkWindow.World, "the region window by its button's name");
        C(!PagesMath.TrySection("Nowhere", out _, out _) && !PagesMath.TrySection(null, out _, out _), "no section by a name that is none");

        // ---------------- Every page the code asks for is a page ----------------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var asks = new[]
        {
            new Regex(@"MainTabWindow_Council\.(?:OpenTab|OpenOrClose)\(""(\w+)""\)"),
            new Regex(@"\bPages\.(?:Open|OpenOrClose)\(""(\w+)""\)"),
            new Regex(@"\.page = ""(\w+)"""),
            new Regex(@"\bpage = ""(\w+)"""),
            new Regex(@"\b(?:Page|ClickPage|CouncilPage)\((?:\w+, )?""(\w+)""\)")
        };
        int asked = 0;
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (var re in asks)
                foreach (Match m in re.Matches(text))
                {
                    asked++;
                    string page = m.Groups[1].Value;
                    C(PagesMath.IsPage(page), $"{Path.GetFileName(file)} asks for the page {page}, which is one");
                }
        }
        C(asked >= 40, $"found the code's asks for pages ({asked})");
        string keys = File.ReadAllText(Path.Combine(src, "UI", "CouncilKeys.cs"));
        foreach (var p in Regex.Match(keys, @"Pages = \{([^}]*)\}").Groups[1].Value.Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0))
            C(PagesMath.IsPage(p), $"the {p} key opens a page");
        C(keys.Contains("OpenOrClose(\"Map\")"), "the region map's key opens the region window on its map");

        // ---------------- The bottom bar's buttons ----------------
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Council_Misc.xml")).Root!;
        var buttons = misc.Elements("MainButtonDef").ToDictionary(e => e.Element("defName")!.Value, e => e);
        string Window(string def) => buttons.TryGetValue(def, out var e) ? e.Element("tabWindowClass")?.Value : null;
        C(Window("Council_MainButton") == "ColonyCouncil.MainTabWindow_Council", "the council button opens the council window");
        C(Window("Rimfolk_WorldButton") == "ColonyCouncil.MainTabWindow_World", "the region button opens the region window");
        C(Window("Council_DefenseButton") == "ColonyCouncil.MainTabWindow_Defenses", "the defense button opens the defense window");
        C(Window("Council_RoomsButton") == "ColonyCouncil.MainTabWindow_Planning", "the planning button keeps the rooms button's def and opens the planning window");
        C(buttons.TryGetValue("Council_RoomsButton", out var plan) && plan.Element("label")?.Value == "planning" && plan.Element("minimized") == null, "the planning button is labelled so and not minimized");
        C(!buttons.ContainsKey(BarMoreMath.OldPlanning), "the small planning button is gone");
        C(buttons.TryGetValue("Rimfolk_WorldButton", out var world) && world.Element("label")?.Value != "world", "the region button isn't called \"world\" like the game's own");
        var order = misc.Elements("MainButtonDef").Select(e => e.Element("defName")!.Value).ToList();
        C(order.IndexOf("Council_MainButton") < order.IndexOf("Rimfolk_WorldButton") && order.IndexOf("Rimfolk_WorldButton") < order.IndexOf("Council_DefenseButton")
            && order.IndexOf("Council_DefenseButton") < order.IndexOf("Council_RoomsButton"), "on the bar: council, region, defense, planning");
        foreach (var cls in new[] { "MainTabWindow_Council", "MainTabWindow_World", "MainTabWindow_Defenses", "MainTabWindow_Planning" })
        {
            string text = File.ReadAllText(Path.Combine(src, "UI", cls + ".cs"));
            C(Regex.IsMatch(text, @"class " + cls + @" : RimfolkTabWindow"), $"{cls} is one of the four windows (the same size and place)");
            C(text.Contains("WindowTabs.Draw("), $"{cls} has the other windows in its corner");
        }

        // The rooms and planning buttons made one, once.
        bool show;
        var merged = BarMoreMath.MergePlanning(new List<string> { "Work", BarMoreMath.Planning }, true, true, out show);
        C(show && !merged.Contains(BarMoreMath.Planning) && merged.Contains("Work"), "rooms put away, planning on the bar: the planning button stays on the bar");
        merged = BarMoreMath.MergePlanning(new List<string> { BarMoreMath.OldPlanning }, true, true, out show);
        C(show && !merged.Contains(BarMoreMath.Planning) && !merged.Contains(BarMoreMath.OldPlanning), "planning put away, rooms on the bar: on the bar, the old name gone");
        merged = BarMoreMath.MergePlanning(new List<string> { BarMoreMath.Planning, BarMoreMath.OldPlanning }, true, true, out show);
        C(show && merged.Contains(BarMoreMath.Planning) && !merged.Contains(BarMoreMath.OldPlanning), "both put away: under More");
        merged = BarMoreMath.MergePlanning(new List<string>(), false, false, out show);
        C(!show && merged.Count == 0, "both switched off: hidden");
        merged = BarMoreMath.MergePlanning(new List<string> { BarMoreMath.OldPlanning }, false, true, out show);
        C(show && merged.Contains(BarMoreMath.Planning), "rooms switched off, planning put away: under More");
        merged = BarMoreMath.MergePlanning(new List<string> { BarMoreMath.Planning }, true, false, out show);
        C(show && merged.Contains(BarMoreMath.Planning), "planning switched off, rooms put away: under More");
        merged = BarMoreMath.MergePlanning(null, true, false, out show);
        C(show && merged.Count == 0, "rooms on the bar, no list: on the bar");
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        C(Regex.IsMatch(settings, @"public bool planningMerged = true;") && settings.Contains("Scribe_Values.Look(ref planningMerged, \"planningMerged\", false);"),
            "a new player's settings start merged; an older file is merged once");

        // ---------------- The region window's History page ----------------
        C(WorldHistoryMath.Words("tie:pact:made") == "pact made", "a tie reads by its kind and what happened");
        C(WorldHistoryMath.Words("ambitionAchieved") == "ambition achieved", "a chronicle kind reads in words");
        C(WorldHistoryMath.Words("world:began") == "world began" && WorldHistoryMath.Words("deed:traded") == "deed traded", "a family and its kind");
        C(WorldHistoryMath.Words(null) == "" && WorldHistoryMath.Words("") == "", "no kind, no words");
        var fams = WorldHistoryMath.Families(new[] { "deed:traded", "world:began", "deed:raided", "loss", "tie:pact:made", "world:ended", "deed:gift", null });
        C(fams.SequenceEqual(new[] { "deed", "world", "loss", "tie" }), $"the filter's kinds: the most common first, then by name ({string.Join(", ", fams)})");
        C(WorldHistoryMath.Families(null).Count == 0, "no history, no kinds");
        var people = Ref.People(7);
        var town = Ref.Town(40);
        var e = new HistoryEntry { kind = "deed:traded", tick = 3 * HistoryMath.DayTicks + 5, first = 3 * HistoryMath.DayTicks + 5 };
        e.actors.Add(new HistoryActor(people, HistoryRole.Did));
        C(WorldHistoryMath.Keeps(e, Ref.None, Ref.None, null), "no filter keeps everything");
        C(WorldHistoryMath.Keeps(e, people, Ref.None, "deed") && !WorldHistoryMath.Keeps(e, people, Ref.None, "world"), "a kind filter keeps its family");
        C(!WorldHistoryMath.Keeps(e, Ref.People(8), Ref.None, null), "a people filter keeps what they took part in");
        C(!WorldHistoryMath.Keeps(e, people, town, null), "a people and a town: both must fit");
        e.place = town;
        C(WorldHistoryMath.Keeps(e, people, town, null) && WorldHistoryMath.Keeps(e, Ref.None, town, null), "an event at the town fits the town");
        var founded = new HistoryEntry { kind = "world:built" };
        founded.actors.Add(new HistoryActor(town, HistoryRole.Did));
        C(WorldHistoryMath.Keeps(founded, Ref.None, town, null), "what the town itself did fits the town");
        C(!WorldHistoryMath.Keeps(null, Ref.None, Ref.None, null), "no event, nothing kept");
        C(WorldHistoryMath.Days(e) == (4, 4), "a day's event is on its day");
        var summary = new HistoryEntry { kind = "deed:traded", first = 10 * HistoryMath.DayTicks, tick = 40 * HistoryMath.DayTicks, count = 6, folded = true };
        C(WorldHistoryMath.Days(summary) == (11, 41), "a summary spans its first to its last day");
        C(WorldHistoryMath.Lead(people, town) == people && WorldHistoryMath.Lead(Ref.None, town) == town, "a people's history asks first, then a town's");
        C(WorldHistoryMath.MaxShown > 0 && WorldHistoryMath.MaxShown <= HistoryMath.MaxEvents, "the page lists no more than the history keeps");
        string pagesFile = File.ReadAllText(Path.Combine(src, "UI", "WorldPages.cs"));
        string cardWords = File.ReadAllText(Path.Combine(src, "UI", "Cards", "CardWords.cs"));
        C(pagesFile.Contains("History.Of(") && pagesFile.Contains("History.At(") && pagesFile.Contains("CardWords.HistoryLine(e, true)") && cardWords.Contains("History.Text(e)"),
            "the History page reads the one history through its own queries and words (with links, UI/Cards/CardWords.cs)");
        string worldWindow = File.ReadAllText(Path.Combine(src, "UI", "MainTabWindow_World.cs"));
        C(Regex.IsMatch(worldWindow, @"public static WorldPage Register\(string name, string labelKey, Texture2D icon, Action<Rect> draw"), "adding a region page is one call");
        return n;
    }
}
