using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Rimfolk's four windows on the bottom bar, one button each.</summary>
    public enum RimfolkWindow
    {
        /// <summary>The colony's own life: the council, its people, the colony, the Almanac.</summary>
        Council,
        /// <summary>The Rimfolk map and everything beyond the colony (the "region" button).</summary>
        World,
        /// <summary>The colony's defenses.</summary>
        Defense,
        /// <summary>Planning the base and its rooms (the button that was "rooms").</summary>
        Planning
    }

    /// <summary>
    /// Where every page of Rimfolk's windows is: which window, which section of it, in what order. Pure, so Tools/Tests
    /// can check it, and the one list the windows build their tabs from. A page's name is its address: code asks for
    /// "Decrees" or "Defenses" (UI/Pages.cs) and the right window opens there. Old addresses keep working: the council
    /// window's World page is now the region window's Neighbours page, its History section's pages went to People and
    /// Colony, its Defenses page to the defense window and its Planning section to the planning window.
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

        /// <summary>The council window's sections, in their order along its top.</summary>
        public static readonly string[] CouncilSections = { "Council", "People", "Colony", "Almanac" };

        /// <summary>The section a window with no sections of its own gives its pages.</summary>
        public static string OnlySection(RimfolkWindow w) => w.ToString();

        /// <summary>Every page, window by window and section by section, in the order of their tabs.</summary>
        public static readonly Page[] All =
        {
            // The council window.
            new Page("Decrees", RimfolkWindow.Council, "Council"),
            new Page("Ambition", RimfolkWindow.Council, "Council"),
            new Page("Ways", RimfolkWindow.Council, "Council"),
            new Page("Dilemmas", RimfolkWindow.Council, "Council"),
            new Page("Wishes", RimfolkWindow.Council, "Council"),
            new Page("Roster", RimfolkWindow.Council, "People"),
            new Page("Everyone", RimfolkWindow.Council, "People"),
            new Page("Bonds", RimfolkWindow.Council, "People"),
            new Page("Faiths", RimfolkWindow.Council, "People"),
            new Page("Moods", RimfolkWindow.Council, "People"),
            new Page("Dreams", RimfolkWindow.Council, "People"),
            new Page("Journals", RimfolkWindow.Council, "People"),
            new Page("Week", RimfolkWindow.Council, "Colony"),
            new Page("Overview", RimfolkWindow.Council, "Colony"),
            new Page("Priorities", RimfolkWindow.Council, "Colony"),
            new Page("Calendar", RimfolkWindow.Council, "Colony"),
            new Page("Chronicle", RimfolkWindow.Council, "Colony"),
            new Page("Almanac", RimfolkWindow.Council, "Almanac"),
            // The region window: the map first, then what lies beyond the colony.
            new Page("Map", RimfolkWindow.World, "World"),
            new Page("Neighbours", RimfolkWindow.World, "World"),
            new Page("Peoples", RimfolkWindow.World, "World"),
            new Page("Letters", RimfolkWindow.World, "World"),
            new Page("Journeys", RimfolkWindow.World, "World"),
            new Page("Events", RimfolkWindow.World, "World"),
            new Page("WorldHistory", RimfolkWindow.World, "World"),
            // The defense window.
            new Page("Defenses", RimfolkWindow.Defense, "Defense"),
            // The planning window.
            new Page("Plan", RimfolkWindow.Planning, "Planning"),
            new Page("Base", RimfolkWindow.Planning, "Planning"),
            new Page("Making", RimfolkWindow.Planning, "Planning"),
            new Page("Build", RimfolkWindow.Planning, "Planning"),
            new Page("Rooms", RimfolkWindow.Planning, "Planning")
        };

        /// <summary>
        /// Old addresses that are now another page: the council window's World page (now the region window's
        /// Neighbours, which kept what it showed), the region map's own window, and the History section (its first page).
        /// </summary>
        public static readonly Dictionary<string, string> Moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["World"] = "Neighbours",
            ["RegionMap"] = "Map",
            ["Region"] = "Map",
            ["History"] = "Chronicle"
        };

        /// <summary>The page a name means, as its tabs know it ("decrees" is "Decrees", "World" is "Neighbours"); null for none.</summary>
        public static string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var p in All)
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                    return p.name;
            return Moved.TryGetValue(name, out var to) ? to : null;
        }

        public static bool IsPage(string name) => Resolve(name) != null;

        /// <summary>The page by its name (resolved), or false.</summary>
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

        /// <summary>The window a page is in; null for no page.</summary>
        public static RimfolkWindow? WindowOf(string name) => TryGet(name, out var p) ? p.window : (RimfolkWindow?)null;

        /// <summary>The section a page is in (the council window's sections; another window's own name); null for no page.</summary>
        public static string SectionOf(string name) => TryGet(name, out var p) ? p.section : null;

        /// <summary>A window's pages in order, or one section's.</summary>
        public static List<string> PagesIn(RimfolkWindow w, string section = null)
        {
            var list = new List<string>();
            foreach (var p in All)
                if (p.window == w && (section == null || p.section == section))
                    list.Add(p.name);
            return list;
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
        /// What a bottom bar button's section name meant (MainButtonWorker_Page): a council section ("People"), or a
        /// window by its name ("Planning", "Defense", "World"), or the old History section (now in Colony). Null for none.
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
