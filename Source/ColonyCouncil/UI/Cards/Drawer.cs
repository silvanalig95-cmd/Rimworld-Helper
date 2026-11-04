using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The drawer (docs/design/interface.md, "Where a card opens"): a panel at the right of a Rimfolk window, 380 wide,
    /// beside the view where the content area is 900 or more and over it, with a shadow, where it isn't (DrawerMath). Its
    /// head shows the trail of cards opened in it ("Tallbrook › Edda Marsh"), each a link back, and a way to close it;
    /// under it the card (CardView). Escape closes it before the window. On the region map the drawer is the map's
    /// panel: it is always there, picking a place opens its card, and with nothing picked it shows what the map's
    /// colours mean. Kept for the session per window: what was open comes back when the window opens again.
    /// </summary>
    public static class Drawer
    {
        private const int MaxCrumbs = 8;
        private const float TrailH = 28f;

        private sealed class State
        {
            public readonly List<ThingRef> crumbs = new List<ThingRef>();
            public int at = -1;
            public readonly CardState card = new CardState();

            public ThingRef Current => at >= 0 && at < crumbs.Count ? crumbs[at] : ThingRef.None;
        }

        private static readonly State[] states = { new State(), new State(), new State(), new State() };

        private static State Of(RimfolkWindow w) => states[(int)w];

        /// <summary>The card open in a window's drawer, or none.</summary>
        public static ThingRef Current(RimfolkWindow w) => Of(w).Current;

        public static bool IsOpen(RimfolkWindow w) => !Of(w).Current.IsNone;

        /// <summary>
        /// Opens a card in a window's drawer. A card already on its trail is gone back to; another follows the one showing
        /// (the trail after it dropped); <paramref name="newTrail"/> starts the trail anew with it (a place picked on the map).
        /// </summary>
        public static void Open(RimfolkWindow w, ThingRef r, bool newTrail = false)
        {
            if (r.IsNone) return;
            var s = Of(w);
            if (newTrail)
            {
                s.crumbs.Clear();
                s.crumbs.Add(r);
                s.at = 0;
                return;
            }
            int i = s.crumbs.IndexOf(r);
            if (i >= 0)
            {
                s.at = i;
                return;
            }
            if (s.at < s.crumbs.Count - 1) s.crumbs.RemoveRange(s.at + 1, s.crumbs.Count - s.at - 1);
            s.crumbs.Add(r);
            while (s.crumbs.Count > MaxCrumbs) s.crumbs.RemoveAt(0);
            s.at = s.crumbs.Count - 1;
        }

        public static void Close(RimfolkWindow w)
        {
            var s = Of(w);
            s.crumbs.Clear();
            s.at = -1;
        }

        /// <summary>Every drawer closed (a game loaded: its things are another game's).</summary>
        public static void Reset()
        {
            foreach (var s in states)
            {
                s.crumbs.Clear();
                s.at = -1;
            }
        }

        /// <summary>A step of the way back (UI/Cards/Nav.cs) puts the drawer as it was: the card open then, or none.</summary>
        public static void FromNav(RimfolkWindow w, ThingRef card)
        {
            if (card.IsNone)
            {
                Close(w);
                return;
            }
            Open(w, card);
        }

        /// <summary>
        /// Escape: a drawer open closes first. True when it took the key (the window stays open). On the region map, the
        /// panel unpicks what was picked.
        /// </summary>
        public static bool Cancel(RimfolkWindow w)
        {
            if (!IsOpen(w)) return false;
            Close(w);
            SoundDefOf.TabClose.PlayOneShotOnCamera();
            return true;
        }

        /// <summary>
        /// Where the view and the drawer go in a window's content area: with the drawer showing (<paramref name="shows"/>),
        /// beside the view when there is room, over it when there isn't.
        /// </summary>
        public static void Layout(Rect content, bool shows, out Rect view, out Rect drawer, out bool over)
        {
            if (!shows)
            {
                view = content;
                drawer = Rect.zero;
                over = false;
                return;
            }
            DrawerMath.Split(content.x, content.width, out float vx, out float vw, out float dx, out float dw, out over);
            view = new Rect(vx, content.y, vw, content.height);
            drawer = new Rect(dx, content.y, dw, content.height);
        }

        /// <summary>
        /// Draws a view with the drawer beside or over it: the view first (where the drawer lies over it, the view doesn't
        /// see the mouse there), then the drawer. <paramref name="empty"/> draws the drawer when no card is open (the map's
        /// legend); without it, no card means no drawer.
        /// </summary>
        public static void Around(RimfolkWindow w, Rect content, Action<Rect> view, Action<Rect> empty = null)
        {
            bool shows = IsOpen(w) || empty != null;
            Layout(content, shows, out var viewRect, out var drawerRect, out bool over);
            var e = Event.current;
            bool hide = shows && over && drawerRect.Contains(e.mousePosition) && e.type != EventType.Layout;
            var saved = e.mousePosition;
            if (hide) e.mousePosition = new Vector2(-10000f, -10000f);
            try
            {
                view(viewRect);
            }
            finally
            {
                if (hide) e.mousePosition = saved;
            }
            if (!shows) return;
            if (over && e.type == EventType.Repaint)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                Widgets.DrawShadowAround(drawerRect);
                GUI.color = Color.white;
            }
            Draw(w, drawerRect, empty);
        }

        /// <summary>The drawer in its rect: the trail, the card, or what it shows with none.</summary>
        public static void Draw(RimfolkWindow w, Rect rect, Action<Rect> empty)
        {
            var s = Of(w);
            Widgets.DrawBoxSolidWithOutline(rect, UiKit.DrawerFill, UiKit.CardBorder);
            var inner = rect.ContractedBy(10f);
            // A card whose thing went (a town razed while it showed) stays with what was kept of it; a gone one on the
            // trail is passed over when going back.
            if (s.Current.IsNone)
            {
                empty?.Invoke(inner);
                return;
            }
            Trail(w, s, new Rect(rect.x, rect.y, rect.width, TrailH), empty == null);
            var body = new Rect(inner.x, rect.y + TrailH + 8f, inner.width, rect.yMax - TrailH - 8f - 10f - rect.y);
            CardView.Draw(body, s.Current, s.card);
        }

        private static readonly List<float> crumbWidths = new List<float>();

        /// <summary>The trail of cards: a link back to each, the one showing in plain ink, and the close button.</summary>
        private static void Trail(RimfolkWindow w, State s, Rect r, bool closable)
        {
            Widgets.DrawLineHorizontal(r.x, r.yMax, r.width, UiKit.CardBorder);
            float x = r.x + 10f;
            float right = r.xMax - 8f;
            if (closable || !s.Current.IsNone)
            {
                var close = new Rect(right - 20f, r.y + 4f, 20f, 20f);
                if (UiKit.IconButton(close, TexButton.CloseXSmall, "CC_Card_CloseTip".Translate())) Close(w);
                right = close.x - 6f;
            }
            Text.Font = GameFont.Tiny;
            string sep = " › ";
            float sepW = Text.CalcSize(sep).x;
            // From the one showing back, as many as fit; the rest are a "..." that leads to the first.
            int first = s.at;
            float used = 0f;
            crumbWidths.Clear();
            for (int i = s.at; i >= 0; i--)
            {
                float cw = Mathf.Min(Text.CalcSize(ThingRefs.Name(s.crumbs[i])).x, 140f);
                if (used + cw + (i < s.at ? sepW : 0f) > right - x - 20f && i < s.at) break;
                used += cw + (i < s.at ? sepW : 0f);
                first = i;
            }
            if (first > 0)
            {
                var dots = new Rect(x, r.y, 14f, r.height);
                UiKit.LabelFit(dots, "...", UiKit.Faint, GameFont.Tiny);
                if (Widgets.ButtonInvisible(dots)) s.at = 0;
                x += 14f;
                UiKit.LabelFit(new Rect(x, r.y, sepW, r.height), sep, UiKit.Dim, GameFont.Tiny);
                x += sepW;
            }
            for (int i = first; i < s.crumbs.Count && i <= s.at; i++)
            {
                string name = ThingRefs.Name(s.crumbs[i]);
                Text.Font = GameFont.Tiny;
                float cw = Mathf.Min(Mathf.Min(Text.CalcSize(name).x, 140f), right - x);
                if (cw <= 8f) break;
                var cr = new Rect(x, r.y, cw, r.height);
                if (i == s.at) UiKit.LabelFit(cr, name, Color.white, GameFont.Tiny);
                else
                {
                    bool over = Mouse.IsOver(cr);
                    UiKit.LabelFit(cr, name, over ? UiKit.LinkHot : UiKit.Link, GameFont.Tiny);
                    LinkText.Underline(cr.x, r.y + r.height / 2f + 7f, cw, over);
                    if (Widgets.ButtonInvisible(cr, false))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        s.at = i;
                    }
                }
                x += cw;
                if (i < s.at)
                {
                    UiKit.LabelFit(new Rect(x, r.y, sepW, r.height), sep, UiKit.Dim, GameFont.Tiny);
                    x += sepW;
                }
            }
            Text.Font = GameFont.Small;
        }
    }
}
