using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Opens Rimfolk's pages wherever they now live (UI/PagesMath.cs has the list): a page's name opens its window on
    /// its view and mode, a view's name on the mode last looked at there, an old name where it went (with its filter,
    /// "Dreams" the Chronicle's dreams), "Almanac" the Almanac's own window. Every caller that asks for a page goes through
    /// here, by way of MainTabWindow_Council.OpenTab and its neighbours, so letters, alerts, the panels, the Almanac's
    /// links and the page keys reach the right window. A window opens through its own bottom bar button's def, which
    /// works whether the button shows on the bar or not.
    /// </summary>
    public static class Pages
    {
        /// <summary>The bottom bar button whose window this is.</summary>
        public static MainButtonDef ButtonOf(RimfolkWindow w)
        {
            switch (w)
            {
                case RimfolkWindow.World: return CouncilDefOf.Rimfolk_WorldButton;
                case RimfolkWindow.Defense: return CouncilDefOf.Council_DefenseButton;
                case RimfolkWindow.Planning: return CouncilDefOf.Council_RoomsButton;
                default: return CouncilDefOf.Council_MainButton;
            }
        }

        /// <summary>Which of the four windows a bottom bar button opens, or none.</summary>
        public static RimfolkWindow? WindowOfButton(MainButtonDef b)
        {
            if (b == null) return null;
            foreach (var w in FramedWindowsMath.Floating)
                if (ButtonOf(w) == b)
                    return w;
            return null;
        }

        /// <summary>The window is open as its tab.</summary>
        private static bool TabShowing(RimfolkWindow w)
        {
            var b = ButtonOf(w);
            return b != null && Find.MainTabsRoot?.OpenTab == b;
        }

        /// <summary>The window is open now, floating (UI/FloatingWindows.cs) or as its tab.</summary>
        public static bool Showing(RimfolkWindow w) => FloatingWindows.Showing(w) || TabShowing(w);

        /// <summary>Closes the window if it shows: the floating window, or the tab (as the tab's own button would).</summary>
        public static void Close(RimfolkWindow w)
        {
            if (FloatingWindows.Showing(w))
            {
                FloatingWindows.Shut(w);
                return;
            }
            if (TabShowing(w)) Find.MainTabsRoot.EscapeCurrentTab();
        }

        /// <summary>
        /// Off to the map (a jump to someone or somewhere, a plan put down, a tile picked on the world): the floating windows
        /// close, and the tab open, as the tab closed before the windows floated. The inspect pane stays (a jump picks who it
        /// shows).
        /// </summary>
        public static void LeaveForMap()
        {
            FloatingWindows.ShutAll(false);
            var root = Find.MainTabsRoot;
            if (root?.OpenTab != null && root.OpenTab != MainButtonDefOf.Inspect) root.EscapeCurrentTab(false);
        }

        /// <summary>
        /// One of the game's tabs from a page of Rimfolk's (the research to unlock a thing, the quests): the window gives way
        /// to it, as the tab did before the windows floated.
        /// </summary>
        public static void GoToGameTab(MainButtonDef tab)
        {
            if (tab == null || Find.MainTabsRoot == null) return;
            FloatingWindows.ShutAll(false);
            Find.MainTabsRoot.SetCurrentTab(tab);
        }

        /// <summary>A page of any of the windows (another part of Rimfolk's region pages too, and the Almanac), by its name or an old one.</summary>
        public static bool IsPage(string name) => PagesMath.IsPage(name) || MainTabWindow_World.Extra(name) != null;

        /// <summary>The page a name means now ("World" is "Neighbours"), with the region pages other parts add; null for none.</summary>
        public static string Resolve(string name) => PagesMath.Resolve(name) ?? MainTabWindow_World.Extra(name)?.name;

        public static RimfolkWindow? WindowOf(string name)
        {
            var w = PagesMath.WindowOf(name);
            if (w != null) return w;
            return MainTabWindow_World.Extra(name) != null ? RimfolkWindow.World : (RimfolkWindow?)null;
        }

        /// <summary>The page each window shows now (or would, opened).</summary>
        public static string Current(RimfolkWindow w)
        {
            switch (w)
            {
                case RimfolkWindow.World: return MainTabWindow_World.Page;
                case RimfolkWindow.Defense: return "Defenses";
                case RimfolkWindow.Planning: return MainTabWindow_Planning.Page;
                default: return MainTabWindow_Council.Page;
            }
        }

        /// <summary>The mode last looked at in one of a window's views (its first before any).</summary>
        public static string LastIn(RimfolkWindow w, string view)
        {
            switch (w)
            {
                case RimfolkWindow.Council: return MainTabWindow_Council.LastIn(view);
                case RimfolkWindow.Planning: return MainTabWindow_Planning.LastIn(view);
                default:
                    var modes = PagesMath.ModesOf(w, view);
                    return modes.Count > 0 ? modes[0] : null;
            }
        }

        /// <summary>The page a name opens: the page itself, where an old name went, or a view's mode last looked at.</summary>
        private static string Target(string name)
        {
            string page = Resolve(name);
            if (page != null && !string.Equals(page, name, StringComparison.OrdinalIgnoreCase) && !PagesMath.Moved.ContainsKey(name)
                && PagesMath.IsView(name, out var w, out var view))
                page = LastIn(w, view) ?? page;
            return page;
        }

        /// <summary>Opens the window that has this page, on it (the Almanac in its own window). False when there is no such page.</summary>
        public static bool Open(string name)
        {
            if (PagesMath.IsAlmanac(name))
            {
                Window_Almanac.Open(null);
                return true;
            }
            string page = Target(name);
            var w = WindowOf(page);
            if (page == null || w == null) return false;
            try
            {
                // An old page that is a filter now ("Dreams", "Journals" on the Chronicle).
                string filter = PagesMath.FilterOf(name);
                if (filter != null) Nav.SetFilter(page, filter);
                switch (w.Value)
                {
                    case RimfolkWindow.World:
                        // The whole address: an old page's name or "Map/Trade/List" sets the view's lens and mode too.
                        MainTabWindow_World.Select(name);
                        break;
                    case RimfolkWindow.Planning:
                        MainTabWindow_Planning.Select(page);
                        break;
                    case RimfolkWindow.Council:
                        MainTabWindow_Council.Select(page);
                        break;
                }
                Show(w.Value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("opening the page " + name, e);
            }
            return true;
        }

        /// <summary>A page's key: opens the window on it, or closes the window when that page is already showing (the Almanac's own window likewise).</summary>
        public static void OpenOrClose(string name)
        {
            if (PagesMath.IsAlmanac(name))
            {
                Window_Almanac.Toggle();
                return;
            }
            string page = Target(name);
            var w = WindowOf(page);
            if (page == null || w == null) return;
            if (Showing(w.Value) && Current(w.Value) == page && (PagesMath.FilterOf(name) == null || Nav.FiltersOf(page) == PagesMath.FilterOf(name)))
            {
                Close(w.Value);
                return;
            }
            Open(name);
        }

        /// <summary>
        /// A section's button (MainButtonWorker_Page): the council window on that view's last mode, or another window as
        /// its button opens it, or the Almanac's window; closed when that is already showing.
        /// </summary>
        public static void OpenOrCloseSection(string name)
        {
            if (PagesMath.IsAlmanac(name))
            {
                Window_Almanac.Toggle();
                return;
            }
            if (!PagesMath.TrySection(name, out var w, out var section)) return;
            if (w != RimfolkWindow.Council)
            {
                if (Showing(w)) Close(w);
                else Show(w);
                return;
            }
            if (Showing(RimfolkWindow.Council) && PagesMath.SectionOf(MainTabWindow_Council.Page) == section)
            {
                Close(RimfolkWindow.Council);
                return;
            }
            Open(MainTabWindow_Council.LastIn(section));
        }

        /// <summary>
        /// Opens a window as its own button does (the region window on the map, the others where they were). One of the four
        /// at a time, as the corner turns pages: where Rimfolk's windows move each floats (UI/FloatingWindows.cs), and opening
        /// one closes the other floating three and any of Rimfolk's tabs; otherwise it is the tab, and opening it closes the
        /// floating ones. Open already, a floating window comes to the front.
        /// </summary>
        public static void Show(RimfolkWindow w)
        {
            var root = Find.MainTabsRoot;
            if (root == null) return;
            if (FloatingWindows.Floats)
            {
                // A tab of Rimfolk's open (from before the windows floated, or opened by another mod) gives way.
                var open = root.OpenTab;
                if (open != null && WindowOfButton(open) != null) root.EscapeCurrentTab(false);
                FloatingWindows.Open(w);
                return;
            }
            var b = ButtonOf(w);
            if (b == null || root.OpenTab == b) return;
            FloatingWindows.ShutAll(false);
            root.SetCurrentTab(b, false);
        }

        /// <summary>
        /// A page's plain name, for the buttons that lead there ("Go to Wishes", "Go to People"); a region view's with the
        /// lens or mode its address names ("Map: Trade, as a list").
        /// </summary>
        public static string Name(string name)
        {
            string page = Resolve(name);
            if (page == null) return name;
            var extra = MainTabWindow_World.Extra(page);
            if (extra != null) return extra.Label;
            switch (page)
            {
                case "Wishes": return "CC_Mood_WishesTab".Translate();
                default:
                    string view = ("CC_Tab_" + page).Translate();
                    return WindowOf(page) == RimfolkWindow.World && PagesMath.TryAddress(name, out var a) ? RegionView.Name(a, view) : view;
            }
        }

        /// <summary>A mode's label on its button in the view's header ("Table", "At a glance"); its page's name where it has none of its own.</summary>
        public static string ModeName(string page)
        {
            string key = "CC_Mode_" + page;
            return key.CanTranslate() ? key.Translate().Resolve() : Name(page);
        }

        /// <summary>A view's name on its tab ("People", "Rooms"): its own words where it has them, else its first mode's name.</summary>
        public static string ViewName(RimfolkWindow w, string view)
        {
            string key = "CC_View_" + view;
            if (key.CanTranslate()) return key.Translate().Resolve();
            var modes = PagesMath.ModesOf(w, view);
            return modes.Count > 0 ? Name(modes[0]) : view;
        }

        /// <summary>A window's name, as its button is labelled.</summary>
        public static string WindowName(RimfolkWindow w) => ButtonOf(w)?.LabelCap.ToString() ?? w.ToString();
    }
}
