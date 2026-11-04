using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The planning button's window (the button that was "rooms", def Council_RoomsButton): the colony's next steps and
    /// ground plan, the base looked over, what could be made, the room plans, and every room at a glance as the rooms
    /// button showed them. It opens on the page last looked at this session.
    /// </summary>
    public class MainTabWindow_Planning : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.Planning;

        private static string page = PagesMath.FirstOf(RimfolkWindow.Planning);
        private static readonly List<string> Order = PagesMath.PagesIn(RimfolkWindow.Planning);
        private readonly List<WindowTab> tabs = new List<WindowTab>();
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private static float labelsAt = -1f;

        /// <summary>The page the window shows (or will, opened).</summary>
        internal static string Page => page;

        /// <summary>The window is to open on this page (Pages.Open opens it).</summary>
        internal static void Select(string name)
        {
            if (Order.Contains(name)) page = name;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            var comp = CouncilGameComponent.Instance;
            if (Time.realtimeSinceStartup - labelsAt > 0.5f)
            {
                labels.Clear();
                labelsAt = Time.realtimeSinceStartup;
            }
            tabs.Clear();
            foreach (var name in Order)
            {
                string n = name;
                tabs.Add(new WindowTab(Label(n, map), IconOf(n), n == page, () => page = n, Tip(n)));
            }
            var body = WindowTabs.Draw(inRect, tabs, RimfolkWindow.Planning);
            var content = body.ContractedBy(12f);

            // The next and previous page keys step through the planning pages here.
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0)
            {
                int i = Order.IndexOf(page);
                page = Order[(i + step + Order.Count) % Order.Count];
            }

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.Plan ?? Icons.Rooms, "CC_NoGame".Translate());
                return;
            }
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            switch (page)
            {
                case "Plan": NextStepsPanel.Draw(content, comp, map); break;
                case "Base": BasePanel.Draw(content, map); break;
                case "Making": MakingPanel.Draw(content, map); break;
                case "Build": BuildPanel.Draw(content, map); break;
                case "Rooms": RoomsPanel.Draw(content, map); break;
            }
            Nav.Observe(RimfolkWindow.Planning, page, ThingRef.None);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
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
                case "Base": return Icons.Base ?? Icons.Rooms;
                case "Making": return Icons.Making ?? Icons.CatManagement;
                case "Build": return Icons.CatRoomDesign ?? Icons.Rooms;
                default: return Icons.Rooms;
            }
        }
    }
}
