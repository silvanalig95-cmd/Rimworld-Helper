using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>One tab along the top of a Rimfolk window.</summary>
    public struct WindowTab
    {
        public string label;
        public Texture2D icon;
        public bool selected;
        public Action click;
        public string tip;

        public WindowTab(string label, Texture2D icon, bool selected, Action click, string tip = null)
        {
            this.label = label;
            this.icon = icon;
            this.selected = selected;
            this.click = click;
            this.tip = tip;
        }
    }

    /// <summary>
    /// What Rimfolk's four windows share at their top: the game's own tabs, each with its icon where it has room (only
    /// the icon, with the name on hover, where even the name doesn't fit), and in the top right corner a small icon for
    /// each of the four windows (the council, the region, defense, planning) to go from one to another. The one showing
    /// is lit; the others open their window as its button would, whether or not that button is on the bar.
    /// </summary>
    public static class WindowTabs
    {
        private const float TabIconSize = 18f;
        private const float LinkSize = 24f;
        private const float LinkGap = 6f;

        /// <summary>The corner's width: four icons and the space around them.</summary>
        public static float LinksWidth => PagesMath.CornerOrder.Length * (LinkSize + LinkGap) + 8f;

        private static string iconPad;

        /// <summary>The size every Rimfolk window asks for (the council window's), so going from one to another keeps the place.</summary>
        public static Vector2 Size => new Vector2(Mathf.Min(1010f, UI.screenWidth - 20f), Mathf.Min(720f, UI.screenHeight - 35f - 20f));

        /// <summary>
        /// The tabs along the top of <paramref name="inRect"/>, the window links in the corner, and the framed body under
        /// them, which is returned.
        /// </summary>
        public static Rect Draw(Rect inRect, List<WindowTab> tabs, RimfolkWindow current)
        {
            Text.Font = GameFont.Small;
            float tabH = TabDrawer.TabHeight;
            var body = new Rect(inRect.x, inRect.y + tabH, inRect.width, inRect.height - tabH);
            Widgets.DrawMenuSection(body);
            var area = new Rect(body.x, body.y, Mathf.Max(120f, body.width - LinksWidth), body.height);
            if (tabs.Count > 0) Tabs(area, tabs);
            Links(new Rect(body.xMax - LinksWidth, inRect.y, LinksWidth, tabH), current);
            return body;
        }

        private static readonly List<TabRecord> records = new List<TabRecord>();
        private static readonly List<byte> modes = new List<byte>();

        private static void Tabs(Rect area, List<WindowTab> tabs)
        {
            float overlap = TabDrawer.TabHoriztonalOverlap;
            float tabW = Mathf.Min(200f, (area.width + (tabs.Count - 1) * overlap) / tabs.Count);
            records.Clear();
            modes.Clear();
            for (int i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                // 1: the icon and the name; 2: the icon alone (the name on hover); 0: the name alone.
                byte mode = t.icon == null ? (byte)0
                    : Text.CalcSize(IconPad + t.label).x <= tabW - 24f ? (byte)1
                    : Text.CalcSize(t.label).x > tabW - 16f ? (byte)2 : (byte)0;
                modes.Add(mode);
                string label = mode == 1 ? IconPad + t.label : mode == 2 ? "" : t.label;
                var click = t.click;
                records.Add(new TabRecord(label, () => click?.Invoke(), t.selected));
            }
            TabDrawer.DrawTabs(area, records, 200f);
            for (int i = 0; i < tabs.Count; i++)
            {
                // Where TabDrawer puts each tab, and where it centers the label in it.
                var tr = new Rect(area.x + i * (tabW - overlap), area.y - TabDrawer.TabHeight + 1f, tabW, TabDrawer.TabHeight);
                var t = tabs[i];
                string tip = modes[i] == 2 ? t.label + (t.tip.NullOrEmpty() ? "" : "\n\n" + t.tip) : t.tip;
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(tr, tip);
                if (modes[i] == 0) continue;
                var color = t.selected ? UiKit.Gold : Mouse.IsOver(tr) ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                float left = modes[i] == 2 ? tr.center.x - TabIconSize / 2f - 1f : tr.center.x - Text.CalcSize(records[i].label).x / 2f;
                UiKit.Icon(new Rect(left + 1f, tr.center.y - TabIconSize / 2f, TabIconSize, TabIconSize), t.icon, color);
            }
        }

        /// <summary>Blank space as wide as a tab icon, measured between two letters (a string of spaces alone may measure as nothing).</summary>
        public static string IconPad
        {
            get
            {
                if (iconPad != null) return iconPad;
                var old = Text.Font;
                Text.Font = GameFont.Small;
                float bare = Text.CalcSize("xx").x;
                string pad = " ";
                while (pad.Length < 12 && Text.CalcSize("x" + pad + "x").x - bare < TabIconSize + 5f) pad += " ";
                Text.Font = old;
                return iconPad = pad;
            }
        }

        // ---------------- The corner ----------------

        public static Texture2D IconOf(RimfolkWindow w)
        {
            switch (w)
            {
                case RimfolkWindow.World: return Icons.World;
                case RimfolkWindow.Defense: return Icons.Defenses ?? Icons.CatDefense;
                case RimfolkWindow.Planning: return Icons.Plan ?? Icons.Rooms;
                default: return Icons.Council;
            }
        }

        private static readonly Dictionary<RimfolkWindow, string> tips = new Dictionary<RimfolkWindow, string>();

        private static string Tip(RimfolkWindow w)
        {
            if (tips.TryGetValue(w, out var t)) return t;
            t = "CC_Win_Tip".Translate(Pages.WindowName(w).Named("WINDOW"), ("CC_Win_What_" + w).Translate().Named("WHAT")).Resolve();
            tips[w] = t;
            return t;
        }

        /// <summary>The four windows' icons, right-aligned in <paramref name="row"/>; the one showing is lit and does nothing.</summary>
        public static void Links(Rect row, RimfolkWindow current)
        {
            float x = row.xMax - 4f;
            var order = PagesMath.CornerOrder;
            for (int i = order.Length - 1; i >= 0; i--)
            {
                var w = order[i];
                x -= LinkSize;
                var r = new Rect(x, row.y + (row.height - LinkSize) / 2f, LinkSize, LinkSize);
                x -= LinkGap;
                bool here = w == current;
                bool over = Mouse.IsOver(r);
                if (here) Widgets.DrawBoxSolidWithOutline(r.ExpandedBy(2f), UiKit.CardSelected, UiKit.WithAlpha(UiKit.Gold, 0.5f));
                else if (over) Widgets.DrawBoxSolid(r.ExpandedBy(2f), UiKit.CardHover);
                var tex = IconOf(w);
                var color = here ? UiKit.Gold : over ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (tex != null) UiKit.Icon(r.ContractedBy(2f), tex, color);
                else UiKit.LabelFit(r, Pages.WindowName(w).Substring(0, 1), color, GameFont.Small, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(r, Tip(w));
                if (!here && Widgets.ButtonInvisible(r))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Pages.Show(w);
                }
            }
        }
    }

    /// <summary>
    /// What Rimfolk's four windows have in common: the same size and place, so going from one to another through the
    /// corner's icons feels like turning a page rather than opening something new.
    /// </summary>
    public abstract class RimfolkTabWindow : MainTabWindow
    {
        public abstract RimfolkWindow Which { get; }

        public override Vector2 RequestedTabSize => WindowTabs.Size;
    }
}
