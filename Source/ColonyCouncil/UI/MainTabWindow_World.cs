using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One page of the region window: its address, its tab's name and icon, how it is drawn, and whether it shows now.</summary>
    public sealed class WorldPage
    {
        public string name;
        public string labelKey;
        public Texture2D icon;
        public Action<Rect> draw;
        public Func<bool> shown;
        /// <summary>Its place among the tabs: the map is 0, the built-in pages 10 to 50, the world's history 100; other pages go between, 60 by default.</summary>
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
    /// The region button's window: the Rimfolk map and everything beyond the colony. It opens on the region map (unless
    /// something asked for another page), and its other pages are the neighbours with what's coming and who's out there,
    /// the peoples (stance, wars, what each remembers), letters and calls, journeys, world events and the world's
    /// history. Other parts of Rimfolk add their pages with one line, <see cref="Register"/>, and their sections on the
    /// map's panel through World/NeighbourPanel.cs.
    /// </summary>
    public class MainTabWindow_World : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.World;

        /// <summary>The map's picture and view, kept for the session (the picture itself only while the window is open).</summary>
        private static readonly RegionMapView map = new RegionMapView();

        /// <summary>The window's own pages, made the first time they're needed (in the interface, after the textures are loaded).</summary>
        private static List<WorldPage> BuiltIn() => new List<WorldPage>
        {
            Own("Map", Icons.World, 0f, r => map.Draw(r)),
            Own("Neighbours", Icons.Visitor, 10f, r => WorldPanel.Draw(r, CouncilGameComponent.Instance)),
            Own("Peoples", Icons.Trust ?? Icons.People, 20f, PeoplesPage.Draw),
            Own("Letters", Icons.Journal, 30f, LettersPage.Draw, () => NotableBook.On),
            Own("Journeys", Icons.Trader, 40f, JourneysPage.Draw),
            Own("Events", Icons.WorldEvent, 50f, EventsPage.Draw),
            Own("WorldHistory", Icons.History ?? Icons.Chronicle, 100f, WorldHistoryPage.Draw)
        };

        private static List<WorldPage> builtIn;
        private static readonly List<WorldPage> extra = new List<WorldPage>();

        private static WorldPage Own(string name, Texture2D icon, float order, Action<Rect> draw, Func<bool> shown = null) =>
            new WorldPage { name = name, labelKey = "CC_Tab_" + name, icon = icon, order = order, draw = draw, shown = shown };

        /// <summary>
        /// Adds a page to the region window, for the region's later pages (trading posts, raiders' camps, hamlets, ties and
        /// diplomacy): its address (what Pages.Open and MainTabWindow_Council.OpenTab take), its tab's text key, its icon,
        /// how it draws into the page's rect, and when it shows (null: always). Call it once, from a static constructor
        /// with [StaticConstructorOnStartup]. A second page by the same name replaces the first.
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
            all = null;
            return page;
        }

        /// <summary>Opens the region map with a settlement (or another world object) picked, its card the map's panel (Cards.ShowOnMap).</summary>
        public static void ShowOnMap(RimWorld.Planet.WorldObject o) => Cards.ShowOnMap(o);

        /// <summary>A page another part of Rimfolk added, by its name (any case); null for none.</summary>
        public static WorldPage Extra(string name)
        {
            if (name.NullOrEmpty()) return null;
            for (int i = 0; i < extra.Count; i++)
                if (string.Equals(extra[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return extra[i];
            return null;
        }

        private static List<WorldPage> all;

        /// <summary>Every page in its order, built-in and added.</summary>
        private static List<WorldPage> All => all ??= (builtIn ??= BuiltIn()).Concat(extra).OrderBy(p => p.order).ToList();

        private static string page = "Map";
        private static bool asked;

        /// <summary>The page the window shows (or will, opened).</summary>
        internal static string Page => page;

        /// <summary>The window is to open on this page (Pages.Open opens it).</summary>
        internal static void Select(string name)
        {
            var p = All.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
            if (p == null) return;
            page = p.name;
            // Asked while closed: it opens there rather than on the map. Asked while open: it just turns the page.
            asked = !Pages.Showing(RimfolkWindow.World);
        }

        public override void PreOpen()
        {
            base.PreOpen();
            // Its button opens it on the map; a page asked for (a key, a link, a card) opens it there.
            if (!asked) page = "Map";
            asked = false;
        }

        public override void PostClose()
        {
            base.PostClose();
            asked = false;
            map.Release();
        }

        private readonly List<WindowTab> tabs = new List<WindowTab>();
        private readonly List<WorldPage> shown = new List<WorldPage>();
        /// <summary>The page being drawn beside the drawer, and the two drawers made once (nothing made per frame).</summary>
        private static WorldPage drawing;
        private static readonly Action<Rect> drawPage = r => drawing?.draw(r);
        private static readonly Action<Rect> drawLegend = r => map.DrawEmpty(r);
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private static float labelsAt = -1f;

        public override void DoWindowContents(Rect inRect)
        {
            var comp = CouncilGameComponent.Instance;
            if (Time.realtimeSinceStartup - labelsAt > 0.5f)
            {
                labels.Clear();
                labelsAt = Time.realtimeSinceStartup;
            }
            shown.Clear();
            foreach (var p in All)
                if (p.Shows) shown.Add(p);
            if (shown.Count > 0 && !shown.Any(p => p.name == page)) page = shown[0].name;
            tabs.Clear();
            foreach (var p in shown)
            {
                string n = p.name;
                tabs.Add(new WindowTab(Label(p, comp), p.icon, n == page, () => page = n, Tip(n)));
            }
            var body = WindowTabs.Draw(inRect, tabs, RimfolkWindow.World);
            var content = body.ContractedBy(12f);

            // The next and previous page keys step through the region's pages here.
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0 && shown.Count > 0)
            {
                int i = shown.FindIndex(p => p.name == page);
                page = shown[(i + step + shown.Count) % shown.Count].name;
            }

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.World, "CC_NoGame".Translate());
                return;
            }
            WorldPage current = null;
            for (int i = 0; i < shown.Count; i++)
                if (shown[i].name == page)
                    current = shown[i];
            if (current == null) return;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            try
            {
                // A card opens in the drawer at the right; on the map the drawer is the map's panel, always there.
                drawing = current;
                Drawer.Around(RimfolkWindow.World, content, drawPage, current.name == "Map" ? drawLegend : null);
                Nav.Observe(RimfolkWindow.World, current.name, Drawer.Current(RimfolkWindow.World));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: the page " + current.name, e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>A tab's name, with a count where something there is under way (fights coming, letters on their way).</summary>
        private static string Label(WorldPage p, CouncilGameComponent comp)
        {
            if (labels.TryGetValue(p.name, out var s)) return s;
            s = p.Label;
            if (comp != null)
            {
                if (p.name == "Neighbours" && comp.happenings.raids.Count > 0) s = "CC_Tab_NeighboursComing".Translate(comp.happenings.raids.Count).Resolve();
                else if (p.name == "Letters" && NeighbourLetters.OpenCount > 0) s = "CC_Tab_LettersOpen".Translate(NeighbourLetters.OpenCount).Resolve();
            }
            labels[p.name] = s;
            return s;
        }

        /// <summary>A page's tooltip: its shortcut, when the player gave it one.</summary>
        private static string Tip(string name)
        {
            string k = "tip:" + name;
            if (labels.TryGetValue(k, out var s)) return s;
            string key = name == "Map" ? CouncilKeys.RegionMapKey() : name == "Neighbours" ? CouncilKeys.KeyFor("World") : null;
            s = key == null ? null : "CC_Keys_TabTip".Translate(Pages.Name(name), key).Resolve();
            labels[k] = s;
            return s;
        }
    }
}
