using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Rimfolk's four windows on the bottom bar, one button each.</summary>
    public enum RimfolkWindow
    {
        /// <summary>The colony's own life: this week, its people, the council, the colony.</summary>
        Council,
        /// <summary>The Rimfolk map and everything beyond the colony (the "region" button).</summary>
        World,
        /// <summary>The colony's defenses.</summary>
        Defense,
        /// <summary>Planning the base and its rooms (the button that was "rooms").</summary>
        Planning
    }

    /// <summary>
    /// Where every page of Rimfolk's windows is: which window, which view of it (its section), in what order. Pure, so
    /// Tools/Tests can check it, and the one list the windows build their tabs and modes from (docs/design/interface.md,
    /// "The windows and their views"). A window has at most five views, as its tabs; a view with more than one page
    /// shows them as its modes, at most five, as small buttons in its header. A page's name is its address: code asks
    /// for "Decrees" or "Defenses" (UI/Pages.cs) and the right window opens there, on that view and mode. Old addresses
    /// keep working (<see cref="Moved"/>): the council window's Roster and Everyone are People's table, its Dreams and
    /// Journals the Chronicle with that filter, its Priorities the planning window's Plan, its Almanac the Almanac's own
    /// window; the council window's World page, and the region window's ten pages before its four views, forward to
    /// a region view with its lens and mode ("Map/Stance/List").
    /// </summary>
    public static class PagesMath
    {
        public readonly struct Page
        {
            public readonly string name;
            public readonly RimfolkWindow window;
            public readonly string section;

            public Page(string name, RimfolkWindow window, string section)
            {
                this.name = name;
                this.window = window;
                this.section = section;
            }
        }

        /// <summary>The council window's views, in their order along its top (each a section of <see cref="All"/>).</summary>
        public static readonly string[] CouncilSections = { "Week", "People", "Council", "Colony" };

        /// <summary>The section a window with no views of its own gives its pages.</summary>
        public static string OnlySection(RimfolkWindow w) => w.ToString();

        /// <summary>At most this many views to a window and modes to a view (the design's rule).</summary>
        public const int MaxViews = 5, MaxModes = 5;

        /// <summary>Every page, window by window and view by view, in the order of their tabs and modes.</summary>
        public static readonly Page[] All =
        {
            // The council window: This week; People (table, moods, bonds, ideoligions, renown); Council (offices and
            // decrees, ambition, ways, wishes, dilemmas); Colony (overview, calendar, chronicle).
            new Page("Week", RimfolkWindow.Council, "Week"),
            new Page("Table", RimfolkWindow.Council, "People"),
            new Page("Moods", RimfolkWindow.Council, "People"),
            new Page("Bonds", RimfolkWindow.Council, "People"),
            new Page("Faiths", RimfolkWindow.Council, "People"),
            new Page("Renown", RimfolkWindow.Council, "People"),
            new Page("Decrees", RimfolkWindow.Council, "Council"),
            new Page("Ambition", RimfolkWindow.Council, "Council"),
            new Page("Ways", RimfolkWindow.Council, "Council"),
            new Page("Wishes", RimfolkWindow.Council, "Council"),
            new Page("Dilemmas", RimfolkWindow.Council, "Council"),
            new Page("Overview", RimfolkWindow.Council, "Colony"),
            new Page("Calendar", RimfolkWindow.Council, "Colony"),
            new Page("Chronicle", RimfolkWindow.Council, "Colony"),
            // The region window in four views (UI 2): the map with its lenses, the peoples, who is here and who is away,
            // the history. Each view is one page; its own modes and lenses are parts of its address ("Map/Trade/List",
            // "Peoples/Faces": Modes and Lenses below).
            new Page("Map", RimfolkWindow.World, "Map"),
            new Page("Peoples", RimfolkWindow.World, "Peoples"),
            new Page("HereAndAway", RimfolkWindow.World, "HereAndAway"),
            new Page("WorldHistory", RimfolkWindow.World, "WorldHistory"),
            // The defense window.
            new Page("Defenses", RimfolkWindow.Defense, "Defense"),
            // The planning window: Plan (the plan, the advisor's priorities); Base; Making; Rooms (the room plans as a
            // list, every room at a glance).
            new Page("Plan", RimfolkWindow.Planning, "Plan"),
            new Page("Priorities", RimfolkWindow.Planning, "Plan"),
            new Page("Base", RimfolkWindow.Planning, "Base"),
            new Page("Making", RimfolkWindow.Planning, "Making"),
            new Page("Build", RimfolkWindow.Planning, "Rooms"),
            new Page("Rooms", RimfolkWindow.Planning, "Rooms")
        };

        /// <summary>
        /// Old addresses that are now another page: the council window's World page (later the region window's
        /// Neighbours), the region map's own window, the History section (its first page), the roster and the table of
        /// everyone (People's table, where a colonist's card opens beside the list), dreams and journals (the Chronicle
        /// with their filter, <see cref="MovedFilters"/>), the offices (Offices and decrees), and the region window's ten
        /// pages before its four views, each a view, a mode or a lens of one now (an address after a slash).
        /// </summary>
        public static readonly Dictionary<string, string> Moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["World"] = "Map/Stance/List",
            ["RegionMap"] = "Map",
            ["Region"] = "Map",
            ["History"] = "Chronicle",
            // The region window's pages before it had four views (UI 2): each is a view, a mode or a lens now.
            ["Neighbours"] = "Map/Stance/List",
            ["TradingPosts"] = "Map/Trade/List",
            ["RaidersCamps"] = "Map/Threats/List",
            ["Hamlets"] = "Map/Ours/List",
            ["Letters"] = "HereAndAway",
            ["Journeys"] = "HereAndAway",
            ["Household"] = "HereAndAway",
            ["Diplomacy"] = "Peoples",
            ["Events"] = "WorldHistory",
            // The council window's pages before it had four views (UI 4).
            ["Roster"] = "Table",
            ["Everyone"] = "Table",
            ["Dreams"] = "Chronicle",
            ["Journals"] = "Chronicle",
            ["Offices"] = "Decrees",
            // People → Houses went with the houses (5 November 2026): the colony's renown is where it was.
            ["Houses"] = "Renown"
        };

        /// <summary>An old address that is a page's filter now: the filter it opens the page with (Pages.Open sets it).</summary>
        public static readonly Dictionary<string, string> MovedFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dreams"] = "Dreams",
            ["Journals"] = "Journals"
        };

        /// <summary>The Almanac's address: no page of a window but a window of its own, which any window opens.</summary>
        public const string Almanac = "Almanac";

        public static bool IsAlmanac(string name) => string.Equals(name, Almanac, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Each page's Almanac article, which the "?" in its view's header opens (by defName; a test checks each is in the
        /// Almanac). A page with none has no "?".
        /// </summary>
        public static readonly Dictionary<string, string> Articles = new Dictionary<string, string>
        {
            ["Week"] = "Council_Alm_StartHere",
            ["Table"] = "Council_Alm_C_Trust",
            ["Moods"] = "Council_Alm_C_Moods",
            ["Bonds"] = "Council_Alm_C_Social",
            ["Faiths"] = "Council_Alm_C_FaithCouncil",
            ["Renown"] = "Council_Alm_C_Diplomacy",
            ["Decrees"] = "Council_Alm_C_Decrees",
            ["Ambition"] = "Council_Alm_C_Meetings",
            ["Ways"] = "Council_Alm_C_Ways",
            ["Wishes"] = "Council_Alm_C_Wishes",
            ["Dilemmas"] = "Council_Alm_C_Dilemmas",
            ["Overview"] = "Council_Alm_Basics_ReadingTheCouncil",
            ["Calendar"] = "Council_Alm_C_Calendar",
            ["Chronicle"] = "Council_Alm_C_Names",
            ["Map"] = "Council_Alm_C_Region",
            ["Peoples"] = "Council_Alm_C_Diplomacy",
            ["HereAndAway"] = "Council_Alm_C_Neighbours",
            ["WorldHistory"] = "Council_Alm_C_WorldMemory",
            ["Plan"] = "Council_Alm_C_Ahead",
            ["Priorities"] = "Council_Alm_C_Priorities",
            ["Base"] = "Council_Alm_9_00_BaseGrowth",
            ["Making"] = "Council_Alm_C_Ahead",
            ["Build"] = "Council_Alm_9_00_RoomPlanner",
            ["Rooms"] = "Council_Alm_9_00_RoomPlanner",
            ["Defenses"] = "Rimfolk_AlmDef_Overview"
        };

        /// <summary>A page's Almanac article (its defName), or null.</summary>
        public static string ArticleOf(string page)
        {
            string p = Resolve(page);
            return p != null && Articles.TryGetValue(p, out var a) ? a : null;
        }

        /// <summary>The most lenses the region map has: a seventh comes only by replacing one (docs/design/interface.md).</summary>
        public const int MaxLenses = 6;

        /// <summary>
        /// A region view's own modes (the same things shown another way, where the view is one page), in the order of their
        /// buttons; the first is where the view opens when an address names no mode. A mode's name is part of an address:
        /// "Peoples/Faces". (The council's and planning window's views have their pages as their modes instead.)
        /// </summary>
        public static readonly Dictionary<string, string[]> Modes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // The region window (UI 2).
            ["Map"] = new[] { "Map", "List" },
            ["Peoples"] = new[] { "Peoples", "Faces", "Gods" }
        };

        /// <summary>A view's lenses (the region map's, UI/RegionViewsMath.cs), in the order of the lens bar: "Map/Trade/List".</summary>
        public static readonly Dictionary<string, string[]> Lenses = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Map"] = new[] { "Land", "Stance", "Fortunes", "Threats", "Trade", "Ours" }
        };

        /// <summary>An address: a page, and a mode and a lens of it where it names them (null where it doesn't).</summary>
        public readonly struct Address
        {
            public readonly string page;
            public readonly string mode;
            public readonly string lens;

            public Address(string page, string mode, string lens)
            {
                this.page = page;
                this.mode = mode;
                this.lens = lens;
            }

            /// <summary>The lens and mode as the way back keeps them ("Trade/List"), or null.</summary>
            public string Within => lens == null ? mode : mode == null ? lens : lens + "/" + mode;

            public override string ToString() => page + (Within == null ? "" : "/" + Within);
        }

        /// <summary>A region view's own modes, or none.</summary>
        public static string[] ModesOf(string page) => page != null && Modes.TryGetValue(page, out var m) ? m : Array.Empty<string>();

        /// <summary>The lenses of a view, or none.</summary>
        public static string[] LensesOf(string page) => page != null && Lenses.TryGetValue(page, out var l) ? l : Array.Empty<string>();

        private static string Named(string[] list, string name)
        {
            foreach (var x in list)
                if (string.Equals(x, name, StringComparison.OrdinalIgnoreCase))
                    return x;
            return null;
        }

        private static string PageNamed(string name)
        {
            foreach (var p in All)
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                    return p.name;
            return null;
        }

        /// <summary>A view's first page, by the view's name (of a window made of views), or null.</summary>
        private static string FirstOfView(string name)
        {
            foreach (var p in All)
                if (HasViews(p.window) && string.Equals(p.section, name, StringComparison.OrdinalIgnoreCase))
                    return p.name;
            return null;
        }

        /// <summary>
        /// What a name means as an address: its page, and the mode or lens it names after a slash ("Map/Trade/List",
        /// "Peoples/Faces"). An old page name forwards to where it is now ("TradingPosts" is the map's Trade lens as a
        /// list, "Roster" the People table), and parts after it override what it forwards to; a view's name is its first
        /// page ("People" is the table); the Almanac is itself. False for none. A part that is neither a mode nor a lens of
        /// the page is passed over.
        /// </summary>
        public static bool TryAddress(string name, out Address address)
        {
            address = default;
            if (string.IsNullOrEmpty(name)) return false;
            var parts = new List<string>(name.Split('/'));
            string page = PageNamed(parts[0]);
            if (page == null && Moved.TryGetValue(parts[0], out var to))
            {
                var moved = to.Split('/');
                page = PageNamed(moved[0]);
                parts.RemoveAt(0);
                parts.InsertRange(0, moved);
            }
            if (page == null && IsAlmanac(parts[0]))
            {
                address = new Address(Almanac, null, null);
                return true;
            }
            if (page == null) page = FirstOfView(parts[0]);
            if (page == null) return false;
            string mode = null, lens = null;
            var modes = ModesOf(page);
            var lenses = LensesOf(page);
            for (int i = 1; i < parts.Count; i++)
            {
                string m = Named(modes, parts[i]);
                if (m != null)
                {
                    mode = m;
                    continue;
                }
                string l = Named(lenses, parts[i]);
                if (l != null) lens = l;
            }
            address = new Address(page, mode, lens);
            return true;
        }

        /// <summary>
        /// The page a name means, as its tabs know it ("decrees" is "Decrees", "Roster" the People table, "TradingPosts" the
        /// region's Map); a view's name is its first mode ("People" is the table); the Almanac is itself. Null for none.
        /// </summary>
        public static string Resolve(string name) => TryAddress(name, out var a) ? a.page : null;

        /// <summary>The filter an old address opens its page with ("Dreams": the Chronicle's dreams), or null.</summary>
        public static string FilterOf(string name) => !string.IsNullOrEmpty(name) && MovedFilters.TryGetValue(name, out var f) ? f : null;

        public static bool IsPage(string name) => Resolve(name) != null;

        /// <summary>The page by its name (resolved), or false (the Almanac is no window's page).</summary>
        public static bool TryGet(string name, out Page page)
        {
            string n = Resolve(name);
            foreach (var p in All)
                if (p.name == n)
                {
                    page = p;
                    return true;
                }
            page = default;
            return false;
        }

        /// <summary>The window a page is in; null for no page (and for the Almanac, a window of its own).</summary>
        public static RimfolkWindow? WindowOf(string name) => TryGet(name, out var p) ? p.window : (RimfolkWindow?)null;

        /// <summary>The section a page is in (its view; a window without views' own name); null for no page.</summary>
        public static string SectionOf(string name) => TryGet(name, out var p) ? p.section : null;

        /// <summary>A window's pages in order, or one view's.</summary>
        public static List<string> PagesIn(RimfolkWindow w, string section = null)
        {
            var list = new List<string>();
            foreach (var p in All)
                if (p.window == w && (section == null || p.section == section))
                    list.Add(p.name);
            return list;
        }

        /// <summary>A window's views in the order of its tabs (a window with no views of its own has one, its own name).</summary>
        public static List<string> ViewsOf(RimfolkWindow w)
        {
            var list = new List<string>();
            foreach (var p in All)
                if (p.window == w && !list.Contains(p.section))
                    list.Add(p.section);
            return list;
        }

        // Which windows are made of views (more than one section), worked out once from All.
        private static bool[] viewed;

        /// <summary>
        /// The window is made of views, each a tab with its own pages as modes (more than one section; a window of one
        /// section, as the defense window, is one view, its tabs its pages).
        /// </summary>
        public static bool HasViews(RimfolkWindow w)
        {
            if (viewed == null)
            {
                var v = new bool[Enum.GetValues(typeof(RimfolkWindow)).Length];
                foreach (RimfolkWindow x in Enum.GetValues(typeof(RimfolkWindow))) v[(int)x] = ViewsOf(x).Count > 1;
                viewed = v;
            }
            return viewed[(int)w];
        }

        /// <summary>A view's modes (its pages) in order; a view of one page has no modes.</summary>
        public static List<string> ModesOf(RimfolkWindow w, string view) => PagesIn(w, view);

        /// <summary>A name is one of the windows' views (a section that isn't the window's own name).</summary>
        public static bool IsView(string name, out RimfolkWindow window, out string view)
        {
            window = RimfolkWindow.Council;
            view = null;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var p in All)
                if (HasViews(p.window) && string.Equals(p.section, name, StringComparison.OrdinalIgnoreCase))
                {
                    window = p.window;
                    view = p.section;
                    return true;
                }
            return false;
        }

        /// <summary>A window's first page: where its button opens when nothing else was asked for.</summary>
        public static string FirstOf(RimfolkWindow w)
        {
            foreach (var p in All)
                if (p.window == w)
                    return p.name;
            return null;
        }

        /// <summary>
        /// What a bottom bar button's section name meant (MainButtonWorker_Page): a council view ("People"), or a window by
        /// its name ("Planning", "Defense", "World"), or the old History section (now the Colony view). False for none
        /// (the old Almanac section is the Almanac's window, which Pages opens).
        /// </summary>
        public static bool TrySection(string name, out RimfolkWindow window, out string section)
        {
            window = RimfolkWindow.Council;
            section = null;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var s in CouncilSections)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                {
                    section = s;
                    return true;
                }
            if (string.Equals(name, "History", StringComparison.OrdinalIgnoreCase))
            {
                section = "Colony";
                return true;
            }
            foreach (RimfolkWindow w in Enum.GetValues(typeof(RimfolkWindow)))
                if (string.Equals(w.ToString(), name, StringComparison.OrdinalIgnoreCase) || w == RimfolkWindow.World && string.Equals(name, "Region", StringComparison.OrdinalIgnoreCase))
                {
                    window = w;
                    section = OnlySection(w);
                    return true;
                }
            return false;
        }

        /// <summary>The four windows in the order of their icons in each window's top right corner, as on the bottom bar.</summary>
        public static readonly RimfolkWindow[] CornerOrder = { RimfolkWindow.Council, RimfolkWindow.World, RimfolkWindow.Defense, RimfolkWindow.Planning };
    }
}
