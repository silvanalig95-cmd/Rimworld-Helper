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
        private const float LinkSize = WindowLayoutMath.CornerIcon;
        private const float LinkGap = WindowLayoutMath.CornerGap;

        /// <summary>
        /// The corner's width: back and forward, the search, the inbox, following and the Almanac's book, the four windows'
        /// icons, and the space around them (WindowLayoutMath, whose layout test tries it on every screen).
        /// </summary>
        public static float LinksWidth => WindowLayoutMath.CornerWidth(WindowLayoutMath.CornerTools, PagesMath.CornerOrder.Length);

        private static string iconPad;

        /// <summary>
        /// The size every Rimfolk window asks for, so going from one to another keeps the place: 1010 by 720, or with the
        /// setting "Window size" on large up to 1280 by 860, within the screen (WindowLayoutMath).
        /// </summary>
        public static Vector2 Size
        {
            get
            {
                bool large = ColonyCouncilMod.Settings != null && ColonyCouncilMod.Settings.windowSize == WindowSizeChoice.Large;
                WindowLayoutMath.Size(UI.screenWidth, UI.screenHeight, large, out float w, out float h);
                return new Vector2(w, h);
            }
        }

        /// <summary>
        /// The tabs along the top of <paramref name="inRect"/>, the window links in the corner (unless the window's title bar
        /// has them, <paramref name="corner"/> off), and the framed body under them, which is returned.
        /// </summary>
        public static Rect Draw(Rect inRect, List<WindowTab> tabs, RimfolkWindow current, bool corner = true)
        {
            Text.Font = GameFont.Small;
            float tabH = TabDrawer.TabHeight;
            var body = new Rect(inRect.x, inRect.y + tabH, inRect.width, inRect.height - tabH);
            Widgets.DrawMenuSection(body);
            float cornerW = corner ? LinksWidth : 0f;
            var area = new Rect(body.x, body.y, Mathf.Max(WindowLayoutMath.TabsMin, body.width - cornerW), body.height);
            if (tabs.Count > 0) Tabs(area, tabs);
            if (corner) Links(new Rect(body.xMax - cornerW, inRect.y, cornerW, tabH), current);
            return body;
        }

        /// <summary>
        /// A window's tabs in the look it draws in: Rimfolk's own (<see cref="DrawOwn"/>, the corner in the title bar) or the
        /// game's, with the corner beside them unless the title bar has it (<paramref name="cornerInTitle"/>).
        /// </summary>
        public static Rect DrawFor(Rect inRect, List<WindowTab> tabs, RimfolkWindow current, bool cornerInTitle) =>
            UiKit.OwnLook ? DrawOwn(inRect, tabs) : Draw(inRect, tabs, current, !cornerInTitle);

        // ---------------- Rimfolk's own look ----------------

        /// <summary>The own look's row of tabs: its height, the gap between tabs, and the least a tab shrinks to (its icon alone).</summary>
        public const float OwnTabH = 34f, OwnTabGap = 4f, OwnTabLeast = 44f;

        private static float[] ownNatural = new float[0], ownX = new float[0], ownW = new float[0];
        private static readonly Dictionary<string, float> ownWidths = new Dictionary<string, float>();

        /// <summary>
        /// The tabs in Rimfolk's own look (UI/UiKitSkin.cs, OwnTab): a row of tabs that read as tabs along the top of
        /// <paramref name="inRect"/>, the lit one joined to the panel under it, which is drawn and returned. The window's
        /// corner is in its title bar. Each tab as wide as its word where they all fit, shrunk alike where they don't, its
        /// icon alone (the word on hover) at the narrowest.
        /// </summary>
        public static Rect DrawOwn(Rect inRect, List<WindowTab> tabs)
        {
            var body = new Rect(inRect.x, inRect.y + OwnTabH, inRect.width, inRect.height - OwnTabH);
            UiKit.RoundBox(body, UiKit.Panel, UiKit.Edge, SkinMath.RadiusCard);
            int n = tabs.Count;
            if (ownNatural.Length < n)
            {
                ownNatural = new float[n];
                ownX = new float[n];
                ownW = new float[n];
            }
            for (int i = 0; i < n; i++)
            {
                string label = tabs[i].label ?? "";
                if (!ownWidths.TryGetValue(label, out float w))
                {
                    if (ownWidths.Count > 100) ownWidths.Clear();
                    ownWidths[label] = w = SkinMath.TabWidth(UiKit.KeptWidth(label, GameFont.Small), tabs[i].icon != null);
                }
                ownNatural[i] = w;
            }
            float room = inRect.width - 2f * SkinMath.RadiusCard;
            SkinMath.Tabs(ownNatural, n, room, OwnTabGap, OwnTabLeast, ownX, ownW);
            // The lit tab last, so it sits over the panel's edge and joins it.
            int litAt = -1;
            for (int i = 0; i < n; i++)
            {
                if (tabs[i].selected)
                {
                    litAt = i;
                    continue;
                }
                OneOwnTab(inRect, tabs[i], i);
            }
            if (litAt >= 0) OneOwnTab(inRect, tabs[litAt], litAt);
            return body;
        }

        private static void OneOwnTab(Rect inRect, WindowTab t, int i)
        {
            var r = new Rect(inRect.x + SkinMath.RadiusCard + ownX[i], inRect.y + 2f, ownW[i], OwnTabH - 2f);
            bool iconOnly = t.icon != null && UiKit.KeptWidth(t.label ?? "", GameFont.Small) + 20f + 22f > ownW[i];
            if (UiKit.OwnTab(r, t.label, t.icon, t.selected, t.tip, iconOnly)) t.click?.Invoke();
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
                var color = t.selected ? UiKit.Gold : Mouse.IsOver(tr) ? UiKit.Ink : UiKit.WithAlpha(UiKit.Ink, 0.75f);
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
        private static string almanacTip, searchTip, followTip;
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
        public static void Links(Rect row, RimfolkWindow current)
        {
            float top = row.y + (row.height - LinkSize) / 2f;
            float ax = row.xMax - LinksWidth + 4f;
            Nav.Arrows(new Rect(ax, top, LinkSize, LinkSize), new Rect(ax + LinkSize + LinkGap, top, LinkSize, LinkSize));
            ax += 2f * (LinkSize + LinkGap);
            // The search: its field opens under it, and the index is made only then.
            SearchIcon(new Rect(ax, top, LinkSize, LinkSize));
            ax += LinkSize + LinkGap;
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
                if (here) LitBack(r.ExpandedBy(2f));
                else if (over) HoverBack(r.ExpandedBy(2f));
                var tex = IconOf(w);
                var color = here ? UiKit.Gold : over ? UiKit.Ink : UiKit.WithAlpha(UiKit.Ink, 0.6f);
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

        /// <summary>Behind a corner icon that is lit (the window showing, the search open): rounded in Rimfolk's own look.</summary>
        private static void LitBack(Rect r)
        {
            if (UiKit.OwnLook) UiKit.RoundBox(r, UiKit.CardSelected, UiKit.WithAlpha(UiKit.Gold, 0.6f), SkinMath.RadiusMedium);
            else Widgets.DrawBoxSolidWithOutline(r, UiKit.CardSelected, UiKit.WithAlpha(UiKit.Gold, 0.5f));
        }

        /// <summary>Behind a corner icon under the mouse.</summary>
        private static void HoverBack(Rect r)
        {
            if (UiKit.OwnLook) UiKit.RoundBox(r, UiKit.CardHover, UiKit.Edge, SkinMath.RadiusMedium);
            else Widgets.DrawBoxSolid(r, UiKit.CardHover);
        }

        /// <summary>The search: its icon, lit while its field is open under it; a click opens the field (the index made then) or shuts it.</summary>
        private static void SearchIcon(Rect r)
        {
            bool open = Find.WindowStack?.WindowOfType<Window_Search>() != null;
            if (searchTip == null)
            {
                string key = Search.KeyLabel();
                searchTip = "CC_Corner_SearchTip".Translate().Resolve() + (key == null ? "" : " (" + key + ")");
            }
            if (open) LitBack(r.ExpandedBy(2f));
            if (UiKit.IconButton(r, Icons.Search, searchTip, true, open ? UiKit.Gold : (Color?)null)) Search.Toggle(UI.GUIToScreenRect(r));
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
            if (here) LitBack(r.ExpandedBy(2f));
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

        /// <summary>
        /// One of the four as a tab opening turns the page: the floating windows close (a tab and its floating window are one
        /// window, never both open; one of the four at a time).
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();
            FloatingWindows.ShutAll(false);
        }

        // ---------------- The tab in Rimfolk's own look ----------------

        /// <summary>The tab drawn in Rimfolk's own look: its frame, fixed in place as the tab is, the corner in its title bar.</summary>
        private WindowFrame tabFrame;
        private string title;
        private Action<Rect> corner;

        protected override float Margin => WindowFrame.OwnLookOn ? 0f : base.Margin;

        public override void WindowOnGUI()
        {
            doWindowBackground = !WindowFrame.OwnLookOn;
            base.WindowOnGUI();
        }

        /// <summary>
        /// The tab (the window while Rimfolk's windows don't move): in the game's look as it always was, or in Rimfolk's own
        /// with its frame's title bar, fixed at the screen's foot. Both draw <see cref="DrawContents"/>, as the floating
        /// window does.
        /// </summary>
        public sealed override void DoWindowContents(Rect inRect)
        {
            if (!WindowFrame.OwnLookOn)
            {
                DrawContents(inRect, false);
                return;
            }
            bool was = UiKit.BeginOwn();
            try
            {
                if (tabFrame == null)
                {
                    FramedWindowsMath.TryKey(FramedWindowsMath.KeyOf(Which), out var k);
                    tabFrame = new WindowFrame(k.key, k.leastW, k.leastH, k.Bottom);
                }
                title ??= Pages.WindowName(Which);
                corner ??= r => Links(r, Which);
                var body = tabFrame.Draw(this, inRect, title, IconOf(Which), LinksWidth, corner, true, false);
                Widgets.BeginGroup(body);
                try
                {
                    DrawContents(body.AtZero(), true);
                }
                finally
                {
                    Widgets.EndGroup();
                }
            }
            finally
            {
                UiKit.EndOwn(was);
            }
        }

        /// <summary>The window's contents (its views as tabs and the view showing), with the corner beside the tabs or, <paramref name="cornerInTitle"/>, in the title bar.</summary>
        protected abstract void DrawContents(Rect inRect, bool cornerInTitle);

        private static float LinksWidth => WindowTabs.LinksWidth;

        private static Texture2D IconOf(RimfolkWindow w) => WindowTabs.IconOf(w);

        private static void Links(Rect r, RimfolkWindow w) => WindowTabs.Links(r, w);

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
