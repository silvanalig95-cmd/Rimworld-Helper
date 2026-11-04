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
    /// the icon, with the name on hover, where even the name doesn't fit), and in the top right corner back and forward,
    /// the search, This week's inbox with the count of what waits on the player, what is followed, the Almanac's book,
    /// and a small icon for each of the four windows (the council, the region, defense, planning) to go from one to
    /// another. The one showing is lit; the others open their window as its button would, whether or not that button
    /// is on the bar.
    /// </summary>
    public static class WindowTabs
    {
        private const float TabIconSize = 18f;
        private const float LinkSize = 24f;
        private const float LinkGap = 6f;
        /// <summary>The search field's width where the window is wide enough; below, the search is an icon like the rest.</summary>
        private const float SearchWide = 82f, SearchFrom = 860f;

        private static float SearchWidth(float width) => width >= SearchFrom ? SearchWide : LinkSize;

        /// <summary>
        /// The corner's width in a window of this width: back and forward, the search, the inbox, following and the
        /// Almanac's book, the four windows' icons, and the space around them.
        /// </summary>
        public static float CornerWidth(float width) =>
            2f * (LinkSize + LinkGap) + SearchWidth(width) + LinkGap + 3f * (LinkSize + LinkGap) + 8f + PagesMath.CornerOrder.Length * (LinkSize + LinkGap) + 8f;

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
            float corner = CornerWidth(body.width);
            var area = new Rect(body.x, body.y, Mathf.Max(120f, body.width - corner), body.height);
            if (tabs.Count > 0) Tabs(area, tabs);
            Links(new Rect(body.xMax - corner, inRect.y, corner, tabH), current, body.width);
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
        private static string almanacTip, searchTip, searchWord, followTip;
        private static int inboxTipFor = -1;
        private static string inboxTip;

        private static string Tip(RimfolkWindow w)
        {
            if (tips.TryGetValue(w, out var t)) return t;
            t = "CC_Win_Tip".Translate(Pages.WindowName(w).Named("WINDOW"), ("CC_Win_What_" + w).Translate().Named("WHAT")).Resolve();
            tips[w] = t;
            return t;
        }

        /// <summary>
        /// Back and forward (UI/Cards/Nav.cs), the search (UI/Search.cs), This week's inbox with its count (UI/Inbox.cs),
        /// what is followed, the Almanac's book (its own window, lit while open), then the four windows' icons,
        /// right-aligned in <paramref name="row"/>; the window showing is lit and does nothing.
        /// </summary>
        public static void Links(Rect row, RimfolkWindow current, float width)
        {
            float top = row.y + (row.height - LinkSize) / 2f;
            float ax = row.xMax - CornerWidth(width) + 4f;
            Nav.Arrows(new Rect(ax, top, LinkSize, LinkSize), new Rect(ax + LinkSize + LinkGap, top, LinkSize, LinkSize));
            ax += 2f * (LinkSize + LinkGap);
            // The search: a field where there is room, else its icon; the index is made only when it opens.
            float sw = SearchWidth(width);
            SearchField(new Rect(ax, top, sw, LinkSize));
            ax += sw + LinkGap;
            // This week, the one inbox, with the count of what waits on the player.
            InboxIcon(new Rect(ax, top, LinkSize, LinkSize));
            ax += LinkSize + LinkGap;
            // What is followed: a list of it, each opening its card.
            FollowingIcon(new Rect(ax, top, LinkSize, LinkSize));
            ax += LinkSize + LinkGap;
            // The Almanac, in its own window beside this one.
            var book = new Rect(ax, top, LinkSize, LinkSize);
            almanacTip ??= "CC_Win_AlmanacTip".Translate().Resolve();
            if (UiKit.IconButton(book, Icons.Almanac, almanacTip, true, Window_Almanac.Showing ? UiKit.Gold : (Color?)null)) Window_Almanac.Toggle();
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

        /// <summary>The search: a field with its icon and word where there is room, its icon alone where not; a click opens it under the field.</summary>
        private static void SearchField(Rect r)
        {
            bool over = Mouse.IsOver(r);
            bool open = Find.WindowStack?.WindowOfType<Window_Search>() != null;
            if (searchTip == null)
            {
                string key = Search.KeyLabel();
                searchTip = "CC_Corner_SearchTip".Translate().Resolve() + (key == null ? "" : " (" + key + ")");
                searchWord = "CC_Corner_Search".Translate().Resolve();
            }
            if (r.width > LinkSize + 1f)
            {
                Widgets.DrawBoxSolidWithOutline(r, over || open ? UiKit.CardHover : UiKit.CardFill, open ? UiKit.WithAlpha(UiKit.Gold, 0.5f) : UiKit.CardBorder, 1);
                UiKit.Icon(new Rect(r.x + 5f, r.y + 5f, 14f, 14f), Icons.Search, open ? UiKit.Gold : over ? Color.white : UiKit.Faint);
                UiKit.LabelFit(new Rect(r.x + 23f, r.y, r.width - 26f, r.height), searchWord, over ? Color.white : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(r, searchTip);
                if (!Widgets.ButtonInvisible(r, false)) return;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else if (!UiKit.IconButton(r, Icons.Search, searchTip, true, open ? UiKit.Gold : (Color?)null)) return;
            Search.Toggle(UI.GUIToScreenRect(r));
        }

        /// <summary>This week, the one inbox: lit while it shows, with the count of what waits on the player (the same as its tab and the council button).</summary>
        private static void InboxIcon(Rect r)
        {
            int n = Inbox.Count;
            bool here = Pages.Showing(RimfolkWindow.Council) && MainTabWindow_Council.Page == "Week";
            if (inboxTipFor != n || inboxTip == null)
            {
                inboxTipFor = n;
                inboxTip = (n > 0 ? "CC_Corner_InboxTip".Translate(n.Named("COUNT")) : "CC_Corner_InboxTipCalm".Translate()).Resolve();
            }
            if (here) Widgets.DrawBoxSolidWithOutline(r.ExpandedBy(2f), UiKit.CardSelected, UiKit.WithAlpha(UiKit.Gold, 0.5f));
            if (UiKit.IconButton(r, Icons.Inbox ?? Icons.Week, inboxTip, true, here ? UiKit.Gold : (Color?)null) && !here) Pages.Open("Week");
            if (n > 0) UiKit.CountBadge(r, n);
        }

        /// <summary>What is followed: a list of it with each one's newest change, each opening its card, and the panel on the screen.</summary>
        private static void FollowingIcon(Rect r)
        {
            var memory = WeekMemory.Instance;
            int n = memory?.following.Count ?? 0;
            followTip ??= "CC_Corner_FollowTip".Translate().Resolve();
            if (!UiKit.IconButton(r, Icons.Pin ?? Icons.Promise, followTip, memory != null, n > 0 ? UiKit.Gold : (Color?)null)) return;
            var options = new List<FloatMenuOption>();
            foreach (var l in FollowWords.Lines())
            {
                var thing = l.thing;
                string label = LinkTextMath.Plain(l.name) + (l.plainNews.NullOrEmpty() ? "" : ": " + l.plainNews);
                options.Add(new FloatMenuOption(label, () => Cards.Open(thing)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("CC_Corner_FollowNone".Translate(), null));
            bool panel = HudLayout.State(HudLayout.Following).on && ColonyCouncilMod.Settings.hudShown;
            options.Add(new FloatMenuOption((panel ? "CC_Corner_FollowPanelOff" : "CC_Corner_FollowPanelOn").Translate(),
                () => HudManager.SetPanel(HudLayout.Following, !panel)));
            Find.WindowStack.Add(new FloatMenu(options));
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

        /// <summary>Escape closes a card's drawer first, the window second.</summary>
        public override void OnCancelKeyPressed()
        {
            if (Drawer.Cancel(Which))
            {
                Event.current.Use();
                return;
            }
            base.OnCancelKeyPressed();
        }

        public override void PostClose()
        {
            base.PostClose();
            Previews.Hide();
        }
    }
}
