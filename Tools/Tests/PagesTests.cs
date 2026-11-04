using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's four windows (Source/ColonyCouncil/UI/PagesMath.cs): every page in one window and section, laid out as the
/// player asked (the council's own life in one window, the region's in another, defense, planning); the old page names
/// (the council window's World, Defenses, Planning and History pages and sections) still lead somewhere, and to the
/// right place; every page name the code opens is a page; the bottom bar buttons are the four windows (the planning
/// button in the rooms button's def), and the rooms and planning buttons' settings merge as they should. The region
/// window's History page: its kinds in words, its filters.
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
            C(PagesMath.WindowOf(p.name) == p.window && PagesMath.SectionOf(p.name) == p.section, $"{p.name}: its window and section");
            if (p.window == RimfolkWindow.Council) C(PagesMath.CouncilSections.Contains(p.section), $"{p.name} is in one of the council window's sections");
            else C(p.section == PagesMath.OnlySection(p.window), $"{p.name}: a window with no sections of its own");
        }
        foreach (var s in PagesMath.CouncilSections) C(PagesMath.PagesIn(RimfolkWindow.Council, s).Count > 0, $"the council window's {s} section has pages");
        foreach (RimfolkWindow w in Enum.GetValues(typeof(RimfolkWindow)))
        {
            C(PagesMath.PagesIn(w).Count > 0, $"the {w} window has pages");
            C(PagesMath.FirstOf(w) == PagesMath.PagesIn(w)[0], $"the {w} window opens on its first page");
            C(PagesMath.CornerOrder.Contains(w), $"the {w} window is in every window's corner");
        }
        C(PagesMath.CornerOrder.Length == 4, "four windows in the corner");

        // ---------------- The player's layout (4 November 2026) ----------------
        bool Same(IEnumerable<string> a, params string[] b) => a.SequenceEqual(b);
        C(PagesMath.CouncilSections.SequenceEqual(new[] { "Council", "People", "Colony", "Almanac" }), "the council window: council, people, colony and the Almanac, in that order");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Council"), "Decrees", "Ambition", "Ways", "Dilemmas", "Wishes"),
            "Council: decrees (offices and elections), ambition (meetings), ways, dilemmas, wishes (and petitions)");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "People"), "Roster", "Everyone", "Bonds", "Faiths", "Moods", "Dreams", "Journals"),
            "People: roster, everyone, bonds, faiths, moods, dreams, journals");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Colony"), "Week", "Overview", "Priorities", "Calendar", "Chronicle"),
            "Colony: this week, overview, priorities, calendar, the chronicle");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Council, "Almanac"), "Almanac"), "the Almanac");
        C(Same(PagesMath.PagesIn(RimfolkWindow.World), "Map", "Neighbours", "Peoples", "Letters", "Journeys", "Events", "WorldHistory"),
            "the region window: the map first, then neighbours, peoples, letters and calls, journeys, world events, the world's history");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Defense), "Defenses"), "the defense window has the Defenses page, and only there");
        C(Same(PagesMath.PagesIn(RimfolkWindow.Planning), "Plan", "Base", "Making", "Build", "Rooms"), "the planning window: plan, base, making, build, rooms");

        // ---------------- Old addresses ----------------
        // The council window's pages before this round, by the names the code, the settings and the keys used.
        var old = new Dictionary<string, (string page, RimfolkWindow window)>
        {
            ["Week"] = ("Week", RimfolkWindow.Council), ["Overview"] = ("Overview", RimfolkWindow.Council), ["Priorities"] = ("Priorities", RimfolkWindow.Council),
            ["Moods"] = ("Moods", RimfolkWindow.Council), ["World"] = ("Neighbours", RimfolkWindow.World), ["Defenses"] = ("Defenses", RimfolkWindow.Defense),
            ["Plan"] = ("Plan", RimfolkWindow.Planning), ["Base"] = ("Base", RimfolkWindow.Planning), ["Making"] = ("Making", RimfolkWindow.Planning),
            ["Build"] = ("Build", RimfolkWindow.Planning), ["Roster"] = ("Roster", RimfolkWindow.Council), ["Everyone"] = ("Everyone", RimfolkWindow.Council),
            ["Bonds"] = ("Bonds", RimfolkWindow.Council), ["Faiths"] = ("Faiths", RimfolkWindow.Council), ["Wishes"] = ("Wishes", RimfolkWindow.Council),
            ["Decrees"] = ("Decrees", RimfolkWindow.Council), ["Ambition"] = ("Ambition", RimfolkWindow.Council), ["Ways"] = ("Ways", RimfolkWindow.Council),
            ["Dilemmas"] = ("Dilemmas", RimfolkWindow.Council), ["Chronicle"] = ("Chronicle", RimfolkWindow.Council), ["Calendar"] = ("Calendar", RimfolkWindow.Council),
            ["Dreams"] = ("Dreams", RimfolkWindow.Council), ["Journals"] = ("Journals", RimfolkWindow.Council), ["Almanac"] = ("Almanac", RimfolkWindow.Council),
            ["RegionMap"] = ("Map", RimfolkWindow.World), ["History"] = ("Chronicle", RimfolkWindow.Council)
        };
        foreach (var kv in old)
        {
            C(PagesMath.Resolve(kv.Key) == kv.Value.page, $"the old page {kv.Key} is {kv.Value.page} now");
            C(PagesMath.WindowOf(kv.Key) == kv.Value.window, $"the old page {kv.Key} opens the {kv.Value.window} window");
        }
        C(PagesMath.SectionOf("Moods") == "People" && PagesMath.SectionOf("Dreams") == "People" && PagesMath.SectionOf("Journals") == "People",
            "moods, dreams and journals moved to People");
        C(PagesMath.SectionOf("Calendar") == "Colony" && PagesMath.SectionOf("Chronicle") == "Colony", "the calendar and the chronicle moved to Colony");
        C(PagesMath.SectionOf("Wishes") == "Council", "wishes moved to Council");
        C(PagesMath.Resolve("decrees") == "Decrees" && PagesMath.Resolve("WORLD") == "Neighbours", "names are read in any case");
        C(!PagesMath.IsPage(null) && !PagesMath.IsPage("") && !PagesMath.IsPage("Nowhere") && PagesMath.WindowOf("Nowhere") == null, "a name that is no page is none");
        foreach (var kv in PagesMath.Moved)
        {
            C(!names.Contains(kv.Key, StringComparer.OrdinalIgnoreCase), $"the old name {kv.Key} is no page's own");
            C(names.Contains(kv.Value), $"the old name {kv.Key} leads to a page");
        }
        // Sections by name (a bottom bar button made in XML, MainButtonWorker_Page).
        C(PagesMath.TrySection("People", out var w1, out var s1) && w1 == RimfolkWindow.Council && s1 == "People", "the People section is the council window's");
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
