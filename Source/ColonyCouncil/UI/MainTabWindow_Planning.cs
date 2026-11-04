using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The planning button's window (the button that was "rooms", def Council_RoomsButton), in four views: Plan (the
    /// colony's next steps and ground plan, and the advisor's priorities as its second mode, the two sharing "Not this"),
    /// Base (the base looked over), Making (what could be made) and Rooms (the room plans as a list, and every room at a
    /// glance as the rooms button showed them). Every view's header carries its modes and a "?" to its Almanac article; a
    /// card opened from a name here opens in this window's drawer. It opens on the page last looked at this session.
    /// </summary>
    public class MainTabWindow_Planning : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.Planning;

        private static readonly List<string> Views = PagesMath.ViewsOf(RimfolkWindow.Planning);
        private static readonly List<string> Order = PagesMath.PagesIn(RimfolkWindow.Planning);
        private static string page = PagesMath.FirstOf(RimfolkWindow.Planning);
        private static readonly Dictionary<string, string> lastInView = new Dictionary<string, string>();
        private readonly List<WindowTab> tabs = new List<WindowTab>();
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private static float labelsAt = -1f;
        private static readonly Dictionary<string, ViewHeadModel> heads = new Dictionary<string, ViewHeadModel>();
        private static readonly Dictionary<string, float> headsAt = new Dictionary<string, float>();
        private static readonly System.Action<Rect> drawView = DrawView;
        private static readonly System.Action<Rect> drawPage = DrawPage;
        private static CouncilGameComponent comp;
        private static Map map;

        /// <summary>The page the window shows (or will, opened).</summary>
        internal static string Page => page;

        private static string ViewOf(string p) => PagesMath.SectionOf(p) ?? Views[0];

        /// <summary>The window is to open on this page (Pages.Open opens it).</summary>
        internal static void Select(string name)
        {
            if (Order.Contains(name)) SetPage(name);
        }

        /// <summary>The mode last looked at in one of the window's views (its first before any).</summary>
        internal static string LastIn(string view)
        {
            if (view == null || !Views.Contains(view)) return null;
            return lastInView.TryGetValue(view, out var p) ? p : PagesMath.ModesOf(RimfolkWindow.Planning, view)[0];
        }

        private static void SetPage(string p)
        {
            page = p;
            lastInView[ViewOf(p)] = p;
        }

        public override void DoWindowContents(Rect inRect)
        {
            map = Find.CurrentMap;
            comp = CouncilGameComponent.Instance;
            if (Time.realtimeSinceStartup - labelsAt > 0.5f)
            {
                labels.Clear();
                labelsAt = Time.realtimeSinceStartup;
            }
            string current = ViewOf(page);
            tabs.Clear();
            foreach (var view in Views)
            {
                string v = view;
                tabs.Add(new WindowTab(ViewLabel(v), IconOf(PagesMath.ModesOf(RimfolkWindow.Planning, v)[0]), v == current, () => SetPage(LastIn(v)), ViewTip(v)));
            }
            var body = WindowTabs.Draw(inRect, tabs, RimfolkWindow.Planning);
            var content = body.ContractedBy(12f);

            // The next and previous page keys step through the planning pages here.
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0)
            {
                int i = Order.IndexOf(page);
                SetPage(Order[(i + step + Order.Count) % Order.Count]);
            }

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.Plan ?? Icons.Rooms, "CC_NoGame".Translate());
                return;
            }
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Drawer.Around(RimfolkWindow.Planning, content, drawView);
            Nav.Observe(RimfolkWindow.Planning, page, Drawer.Current(RimfolkWindow.Planning));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawView(Rect rect) => ViewHead.Draw(rect, page, Pages.ModeName(page), IconOf(page), Head(ViewOf(page)), drawPage);

        private static void DrawPage(Rect content)
        {
            switch (page)
            {
                case "Plan": NextStepsPanel.Draw(content, comp, map); break;
                case "Priorities": PrioritiesPanel.Draw(content, map); break;
                case "Base": BasePanel.Draw(content, map); break;
                case "Making": MakingPanel.Draw(content, map); break;
                case "Build": BuildPanel.Draw(content, map); break;
                case "Rooms": RoomsPanel.Draw(content, map); break;
            }
        }

        /// <summary>A view's header extras: its modes, the one lit, and the "?" to the page's article.</summary>
        private static ViewHeadModel Head(string view)
        {
            if (!heads.TryGetValue(view, out var h))
            {
                var v = view;
                heads[view] = h = new ViewHeadModel { pick = i => PickMode(v, i) };
            }
            var modes = PagesMath.ModesOf(RimfolkWindow.Planning, view);
            if (h.labels.Count != modes.Count || !headsAt.TryGetValue(view, out float at) || at != labelsAt)
            {
                headsAt[view] = labelsAt;
                h.labels.Clear();
                h.tips.Clear();
                foreach (var m in modes)
                {
                    h.labels.Add(Pages.ModeName(m));
                    h.tips.Add(Tip(m));
                }
            }
            h.lit = modes.IndexOf(page);
            h.article = PagesMath.ArticleOf(page);
            return h;
        }

        private static void PickMode(string view, int i)
        {
            var modes = PagesMath.ModesOf(RimfolkWindow.Planning, view);
            if (i >= 0 && i < modes.Count) SetPage(modes[i]);
        }

        private static string ViewLabel(string view)
        {
            string k = "view:" + view;
            if (labels.TryGetValue(k, out var s)) return s;
            s = view == "Plan" ? Label("Plan", map) : Pages.ViewName(RimfolkWindow.Planning, view);
            labels[k] = s;
            return s;
        }

        private static string ViewTip(string view)
        {
            string k = "viewtip:" + view;
            if (labels.TryGetValue(k, out var s)) return s;
            string key = "CC_View_Tip_" + view;
            s = key.CanTranslate() ? key.Translate().Resolve() : null;
            foreach (var p in PagesMath.ModesOf(RimfolkWindow.Planning, view))
            {
                string bound = CouncilKeys.KeyFor(p);
                if (bound != null) s = (s == null ? "" : s + "\n\n") + "CC_Keys_TabTip".Translate(Pages.Name(p), bound).Resolve();
            }
            labels[k] = s;
            return s;
        }

        private static string Label(string name, Map map)
        {
            if (labels.TryGetValue(name, out var s)) return s;
            if (name == "Plan")
            {
                int steps = map != null ? NextSteps.PlannedCount(map) : 0;
                s = steps > 0 ? "CC_Tab_PlanCount".Translate(steps).ToString() : "CC_Tab_Plan".Translate().ToString();
            }
            else s = ("CC_Tab_" + name).Translate();
            labels[name] = s;
            return s;
        }

        /// <summary>A page's tooltip: its shortcut, when the player gave it one.</summary>
        private static string Tip(string name)
        {
            string k = "tip:" + name;
            if (labels.TryGetValue(k, out var s)) return s;
            string key = CouncilKeys.KeyFor(name);
            s = key == null ? null : "CC_Keys_TabTip".Translate(Pages.Name(name), key).Resolve();
            labels[k] = s;
            return s;
        }

        private static Texture2D IconOf(string name)
        {
            switch (name)
            {
                case "Plan": return Icons.Plan ?? Icons.Week;
                case "Priorities": return Icons.Priorities;
                case "Base": return Icons.Base ?? Icons.Rooms;
                case "Making": return Icons.Making ?? Icons.CatManagement;
                case "Build": return Icons.CatRoomDesign ?? Icons.Rooms;
                default: return Icons.Rooms;
            }
        }
    }
}
