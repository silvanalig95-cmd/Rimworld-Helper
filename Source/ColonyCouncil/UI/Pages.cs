using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Opens Rimfolk's pages wherever they now live (UI/PagesMath.cs has the list): a page's name opens its window on it.
    /// Every caller that asks for a page goes through here, by way of MainTabWindow_Council.OpenTab and its neighbours,
    /// so letters, alerts, the panels, the Almanac's links and the page keys reach the right window. A window opens
    /// through its own bottom bar button's def, which works whether the button shows on the bar or not.
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

        /// <summary>The window is open now.</summary>
        public static bool Showing(RimfolkWindow w)
        {
            var b = ButtonOf(w);
            return b != null && Find.MainTabsRoot?.OpenTab == b;
        }

        /// <summary>A page of any of the windows (another part of Rimfolk's region pages too), by its name or an old one.</summary>
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

        /// <summary>Opens the window that has this page, on it. False when there is no such page.</summary>
        public static bool Open(string name)
        {
            string page = Resolve(name);
            var w = WindowOf(page);
            if (page == null || w == null) return false;
            try
            {
                switch (w.Value)
                {
                    case RimfolkWindow.World:
                        MainTabWindow_World.Select(page);
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

        /// <summary>A page's key: opens the window on it, or closes the window when that page is already showing.</summary>
        public static void OpenOrClose(string name)
        {
            string page = Resolve(name);
            var w = WindowOf(page);
            if (page == null || w == null) return;
            if (Showing(w.Value) && Current(w.Value) == page)
            {
                Find.MainTabsRoot.EscapeCurrentTab();
                return;
            }
            Open(page);
        }

        /// <summary>
        /// A section's button (MainButtonWorker_Page): the council window on that section's last page, or another window as
        /// its button opens it; closed when that section is already showing.
        /// </summary>
        public static void OpenOrCloseSection(string name)
        {
            if (!PagesMath.TrySection(name, out var w, out var section)) return;
            if (w != RimfolkWindow.Council)
            {
                if (Showing(w)) Find.MainTabsRoot.EscapeCurrentTab();
                else Show(w);
                return;
            }
            if (Showing(RimfolkWindow.Council) && PagesMath.SectionOf(MainTabWindow_Council.Page) == section)
            {
                Find.MainTabsRoot.EscapeCurrentTab();
                return;
            }
            Open(MainTabWindow_Council.LastIn(section));
        }

        /// <summary>Opens a window as its own button does (the region window on the map, the others where they were).</summary>
        public static void Show(RimfolkWindow w)
        {
            var b = ButtonOf(w);
            var root = Find.MainTabsRoot;
            if (b == null || root == null || root.OpenTab == b) return;
            root.SetCurrentTab(b, false);
        }

        /// <summary>A page's plain name, for the buttons that lead there ("Go to Wishes").</summary>
        public static string Name(string name)
        {
            string page = Resolve(name);
            if (page == null) return name;
            var extra = MainTabWindow_World.Extra(page);
            if (extra != null) return extra.Label;
            switch (page)
            {
                case "Wishes": return "CC_Mood_WishesTab".Translate();
                default: return ("CC_Tab_" + page).Translate();
            }
        }

        /// <summary>A window's name, as its button is labelled.</summary>
        public static string WindowName(RimfolkWindow w) => ButtonOf(w)?.LabelCap.ToString() ?? w.ToString();
    }
}
