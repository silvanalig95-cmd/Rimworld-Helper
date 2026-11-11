using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One view of the region window, or a page another mod added: its address, its tab's name and icon, how it is drawn, and whether it shows now.</summary>
    public sealed class WorldPage
    {
        public string name;
        public string labelKey;
        public Texture2D icon;
        public Action<Rect> draw;
        public Func<bool> shown;
        /// <summary>Its place: the map is 0, the views 20 to 100; a page another mod adds goes under History's More, in this order.</summary>
        public float order;

        private string label;

        public string Label => label ??= labelKey.Translate();

        public bool Shows
        {
            get
            {
                if (shown == null) return true;
                try
                {
                    return shown();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: whether " + name + " shows", e);
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// The region button's window: the Rimfolk map and everything beyond the colony, in four views (docs/design/interface.md,
    /// UI 2): Map (the region map read through six lenses, with a List for each: Land, Stance and ties, Fortunes, Threats,
    /// Trade, Ours; UI/Region/MapView.cs), Peoples (the peoples, or their faces), Here and away (who is here from outside,
    /// what is on its way, letters on the road, who of the colony's is away, missions, houses) and History (the world's
    /// history with its scope, the world's events running as a strip, letters lately). It opens on the map unless something
    /// asked for another view. The ten pages it had before forward to their view, mode or lens (UI/PagesMath.cs); a page
    /// another mod adds with <see cref="Register"/> shows under History's More until it finds its place in a view. While
    /// Rimfolk's windows move and resize, the region button opens the same contents as a floating window in Rimfolk's
    /// frame (UI/Window_Region.cs, <see cref="DrawRegion"/> for both); this tab is the region window with that setting off.
    /// </summary>
    public class MainTabWindow_World : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.World;

        /// <summary>The map's picture and view, kept for the session (the picture itself only while the window is open).</summary>
        private static readonly RegionMapView map = new RegionMapView();

        /// <summary>The window's four views, made the first time they're needed (in the interface, after the textures are loaded).</summary>
        private static List<WorldPage> BuiltIn() => new List<WorldPage>
        {
            Own(RegionView.Map, Icons.World, 0f, r => MapView.Draw(r, map)),
            Own(RegionView.Peoples, Icons.Trust ?? Icons.People, 20f, PeoplesView.Draw),
            Own(RegionView.HereAndAway, Icons.Away ?? Icons.Visitor, 30f, HereAndAwayView.Draw),
            Own(RegionView.Gods, Icons.Journal ?? Icons.Chronicle, 40f, GodLorePage.Draw),
            Own(RegionView.History, Icons.History ?? Icons.Chronicle, 100f, WorldHistoryPage.Draw)
        };

        private static List<WorldPage> views;
        private static readonly List<WorldPage> extra = new List<WorldPage>();

        private static List<WorldPage> Views => views ??= BuiltIn();

        private static WorldPage Own(string name, Texture2D icon, float order, Action<Rect> draw) =>
            new WorldPage { name = name, labelKey = "CC_Tab_" + name, icon = icon, order = order, draw = draw };

        /// <summary>
        /// Adds a page to the region window, for other mods (Rimfolk's own parts add rows, sections, acts and lens marks to
        /// the four views instead: docs/design/interface.md, "Where a new thing goes"): its address (what Pages.Open takes),
        /// its name's text key, its icon, how it draws into its rect, and when it shows (null: always). It is listed under
        /// History's More, in its order, and opens in the window's body with History's tab lit. Call it once, from a static
        /// constructor with [StaticConstructorOnStartup]. A second page by the same name replaces the first; a name that is
        /// already an address (a view, or a page that moved) is refused.
        /// </summary>
        public static WorldPage Register(string name, string labelKey, Texture2D icon, Action<Rect> draw, Func<bool> shown = null, float order = 60f)
        {
            if (name.NullOrEmpty() || draw == null) return null;
            if (PagesMath.IsPage(name))
            {
                Log.Warning("[Rimfolk] A region page can't be called " + name + ": that is another page's name.");
                return null;
            }
            extra.RemoveAll(p => p.name == name);
            var page = new WorldPage { name = name, labelKey = labelKey ?? "CC_Tab_" + name, icon = icon, draw = draw, shown = shown, order = order };
            extra.Add(page);
            extra.Sort((a, b) => a.order.CompareTo(b.order));
            return page;
        }

        /// <summary>Opens the region map with a settlement (or another world object) picked, its card the map's panel (Cards.ShowOnMap).</summary>
        public static void ShowOnMap(RimWorld.Planet.WorldObject o) => Cards.ShowOnMap(o);

        /// <summary>A page another mod added, by its name (any case); null for none.</summary>
        public static WorldPage Extra(string name)
        {
            if (name.NullOrEmpty()) return null;
            for (int i = 0; i < extra.Count; i++)
                if (string.Equals(extra[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return extra[i];
            return null;
        }

        private static readonly List<WorldPage> shownExtra = new List<WorldPage>();

        /// <summary>The pages other mods added that show now, in their order (History's More).</summary>
        public static List<WorldPage> ExtraPages()
        {
            shownExtra.Clear();
            foreach (var p in extra)
                if (p.Shows)
                    shownExtra.Add(p);
            return shownExtra;
        }

        private static string page = RegionView.Map;
        private static bool asked;

        /// <summary>The view the window shows (or will, opened), or another mod's page under History's More.</summary>
        internal static string Page => page;

        /// <summary>
        /// The window is to open at this address (Pages.Open opens it): a view with the mode and lens it names
        /// ("Map/Trade/List"), an old page name forwarding to its view, or another mod's page.
        /// </summary>
        internal static void Select(string name)
        {
            if (PagesMath.TryAddress(name, out var a) && ViewNamed(a.page) != null)
            {
                page = a.page;
                RegionView.Apply(a);
            }
            else
            {
                var p = Extra(name);
                if (p == null) return;
                page = p.name;
            }
            // Asked while closed: it opens there rather than on the map. Asked while open: it just turns the view.
            asked = !Pages.Showing(RimfolkWindow.World);
        }

        public override void PreOpen()
        {
            // The tab and the floating window are one window, never both: the base closes the floating ones.
            base.PreOpen();
            Opening();
        }

        public override void PostClose()
        {
            base.PostClose();
            Closing();
        }

        /// <summary>
        /// The region window opens, as the tab or floating: its button opens it on the map itself (the lens as it was); a
        /// view asked for (a key, a link, a card) opens it there.
        /// </summary>
        internal static void Opening()
        {
            if (!asked)
            {
                page = RegionView.Map;
                RegionView.list = false;
            }
            asked = false;
        }

        /// <summary>The region window closed, as the tab or floating: the map's picture let go.</summary>
        internal static void Closing()
        {
            asked = false;
            map.Release();
        }

        /// <summary>One of the four views by its name, or null.</summary>
        private static WorldPage ViewNamed(string name)
        {
            if (name == null) return null;
            for (int i = 0; i < Views.Count; i++)
                if (Views[i].name == name)
                    return Views[i];
            return null;
        }

        private static readonly List<WindowTab> tabs = new List<WindowTab>();
        /// <summary>The view or page being drawn beside the drawer, and the two drawers made once (nothing made per frame).</summary>
        private static WorldPage drawing;
        private static readonly Action<Rect> drawPage = r => drawing?.draw(r);
        private static readonly Action<Rect> drawLegend = r => map.DrawEmpty(r);
        private static string mapTip;
        private static float mapTipAt = -100f;

        /// <summary>The tab (the region window while Rimfolk's windows don't move), in either look (RimfolkTabWindow frames it in Rimfolk's).</summary>
        protected override void DrawContents(Rect inRect, bool cornerInTitle) => DrawRegion(inRect, cornerInTitle);

        /// <summary>
        /// The region window's contents, the tab's and the floating window's alike: the four views as tabs (with the corner
        /// beside them, or in the title bar above with <paramref name="cornerInTitle"/>), and the view showing with its
        /// drawer in the body under them.
        /// </summary>
        internal static void DrawRegion(Rect inRect, bool cornerInTitle)
        {
            var comp = CouncilGameComponent.Instance;
            var extraPage = ViewNamed(page) != null ? null : Extra(page);
            if (extraPage != null && !extraPage.Shows) extraPage = null;
            if (extraPage == null && ViewNamed(page) == null) page = RegionView.Map;
            // Another mod's page shows with History's tab lit (it is under History's More).
            string lit = extraPage != null ? RegionView.History : page;
            tabs.Clear();
            foreach (var v in Views)
            {
                string n = v.name;
                tabs.Add(new WindowTab(v.Label, v.icon, n == lit, () => page = n, n == RegionView.Map ? MapTip() : null));
            }
            var body = WindowTabs.DrawFor(inRect, tabs, RimfolkWindow.World, cornerInTitle);
            var content = body.ContractedBy(12f);

            // The next and previous page keys step through the four views here.
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0)
            {
                int i = Views.FindIndex(p => p.name == lit);
                page = Views[(i + step + Views.Count) % Views.Count].name;
                extraPage = null;
            }

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.World, "CC_NoGame".Translate());
                return;
            }
            var current = extraPage ?? ViewNamed(page);
            if (current == null) return;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            try
            {
                // A card opens in the drawer at the right; on the map (not its List) the drawer is the map's panel, always there.
                drawing = current;
                bool mapPanel = current.name == RegionView.Map && !RegionView.list;
                Drawer.Around(Drawer.Of(RimfolkWindow.World), content, drawPage, mapPanel ? drawLegend : null);
                Nav.Observe(RimfolkWindow.World, current.name, RegionView.Within(current.name), Drawer.Current(RimfolkWindow.World));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: the view " + current.name, e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The map tab's tooltip: the region map's key, when the player gave it one.</summary>
        private static string MapTip()
        {
            // Looked at again every few seconds, so a key bound meanwhile shows.
            if (mapTip != null && Time.realtimeSinceStartup - mapTipAt < 3f && mapTipAt <= Time.realtimeSinceStartup) return mapTip.Length == 0 ? null : mapTip;
            mapTipAt = Time.realtimeSinceStartup;
            string key = CouncilKeys.RegionMapKey();
            mapTip = key == null ? "" : "CC_Keys_TabTip".Translate(Pages.Name(RegionView.Map), key).Resolve();
            return mapTip.Length == 0 ? null : mapTip;
        }
    }
}
