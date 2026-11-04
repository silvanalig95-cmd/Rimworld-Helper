using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// One step of the way back: where the player was in Rimfolk's windows. Its window, the page (view) there, its mode or
    /// lens where it has one, the filters that matter (in a page's own short text), and the card open in the window's
    /// drawer. Two steps are the same place when all of these are.
    /// </summary>
    public readonly struct NavStep : IEquatable<NavStep>
    {
        public readonly RimfolkWindow window;
        public readonly string page;
        public readonly string mode;
        public readonly string filters;
        public readonly ThingRef card;

        public NavStep(RimfolkWindow window, string page, string mode = null, string filters = null, ThingRef card = default)
        {
            this.window = window;
            this.page = page;
            this.mode = string.IsNullOrEmpty(mode) ? null : mode;
            this.filters = string.IsNullOrEmpty(filters) ? null : filters;
            this.card = card;
        }

        /// <summary>The same step with another card open (or none).</summary>
        public NavStep WithCard(ThingRef c) => new NavStep(window, page, mode, filters, c);

        public bool Same(RimfolkWindow w, string p, string m, string f, ThingRef c) =>
            window == w && string.Equals(page, p, StringComparison.Ordinal) && string.Equals(mode, string.IsNullOrEmpty(m) ? null : m, StringComparison.Ordinal)
            && string.Equals(filters, string.IsNullOrEmpty(f) ? null : f, StringComparison.Ordinal) && card == c;

        public bool Equals(NavStep o) => Same(o.window, o.page, o.mode, o.filters, o.card);

        public override bool Equals(object obj) => obj is NavStep s && Equals(s);

        public override int GetHashCode() => unchecked(((int)window * 31 + (page?.GetHashCode() ?? 0)) * 31 + card.GetHashCode());

        public override string ToString() => window + "/" + page + (mode != null ? "/" + mode : "") + (filters != null ? "?" + filters : "") + (card.IsNone ? "" : "#" + card);
    }

    /// <summary>
    /// The way back and forward through Rimfolk's windows, like a browser's: each window, view, mode, filter that matters
    /// and card opened is a step; going somewhere new after going back drops the steps ahead; at most <see cref="Max"/>
    /// steps, the oldest going first. A step whose thing is gone (a town razed, a person the game let go of) is dropped
    /// when the way passes it, rather than leading nowhere. Kept for the session (UI/Cards/Nav.cs holds one). Pure:
    /// Tools/Tests checks it.
    /// </summary>
    public sealed class NavTrail
    {
        public const int Max = 50;

        private readonly List<NavStep> steps = new List<NavStep>();
        private int at = -1;

        public int Count => steps.Count;

        /// <summary>Where the trail stands (its index), -1 when empty.</summary>
        public int At => at;

        public IReadOnlyList<NavStep> Steps => steps;

        public bool HasCurrent => at >= 0 && at < steps.Count;

        public NavStep Current => HasCurrent ? steps[at] : default;

        /// <summary>
        /// A new place: added after the current step (the steps ahead dropped), unless it is where the trail stands already.
        /// Returns whether a step was added.
        /// </summary>
        public bool Push(NavStep s)
        {
            if (HasCurrent && steps[at].Equals(s)) return false;
            if (at < steps.Count - 1) steps.RemoveRange(at + 1, steps.Count - at - 1);
            steps.Add(s);
            while (steps.Count > Max) steps.RemoveAt(0);
            at = steps.Count - 1;
            return true;
        }

        /// <summary>The current step becomes this one without a new step (the place settled after a step back led there).</summary>
        public void Replace(NavStep s)
        {
            if (!HasCurrent)
            {
                Push(s);
                return;
            }
            steps[at] = s;
            // Two neighbours the same: one is enough.
            if (at > 0 && steps[at - 1].Equals(s))
            {
                steps.RemoveAt(at);
                at--;
            }
            else if (at < steps.Count - 1 && steps[at + 1].Equals(s)) steps.RemoveAt(at + 1);
        }

        /// <summary>There is a step behind that still leads somewhere.</summary>
        public bool CanBack(Func<NavStep, bool> alive)
        {
            for (int i = at - 1; i >= 0; i--)
                if (alive == null || alive(steps[i]))
                    return true;
            return false;
        }

        public bool CanForward(Func<NavStep, bool> alive)
        {
            for (int i = at + 1; i < steps.Count; i++)
                if (alive == null || alive(steps[i]))
                    return true;
            return false;
        }

        /// <summary>One step back: the steps passed whose thing is gone are dropped. False (and nothing changed) when none is left.</summary>
        public bool Back(Func<NavStep, bool> alive, out NavStep step)
        {
            step = default;
            if (!CanBack(alive)) return false;
            int i = at - 1;
            while (i >= 0 && alive != null && !alive(steps[i]))
            {
                steps.RemoveAt(i);
                at--;
                i--;
            }
            at = i;
            step = steps[at];
            return true;
        }

        /// <summary>One step forward, dropping the gone ones passed. False when none is left.</summary>
        public bool Forward(Func<NavStep, bool> alive, out NavStep step)
        {
            step = default;
            if (!CanForward(alive)) return false;
            int i = at + 1;
            while (i < steps.Count && alive != null && !alive(steps[i])) steps.RemoveAt(i);
            at = i;
            step = steps[at];
            return true;
        }

        /// <summary>Every step whose thing is gone, dropped (where the trail stands moves with them).</summary>
        public void Prune(Func<NavStep, bool> alive)
        {
            if (alive == null) return;
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                if (alive(steps[i])) continue;
                steps.RemoveAt(i);
                if (i <= at) at--;
            }
            if (at < 0 && steps.Count > 0) at = 0;
            if (at >= steps.Count) at = steps.Count - 1;
        }

        public void Clear()
        {
            steps.Clear();
            at = -1;
        }
    }

    /// <summary>
    /// Where a card's drawer goes in a window (docs/design/interface.md, "Where a card opens"): 380 wide at the right of
    /// the content area, beside the view when the content area is 900 or more, over it with a shadow when narrower (and
    /// never wider than the content leaves room for). The size of Rimfolk's windows on a screen at an interface scale
    /// is the one <c>WindowTabs.Size</c> asks for. Pure: Tools/Tests checks it at the screens and scales the game offers.
    /// </summary>
    public static class DrawerMath
    {
        public const float Width = 380f;
        public const float Narrowest = 340f;
        public const float Gap = 12f;
        public const float BesideFrom = 900f;
        /// <summary>The view keeps at least this much when the drawer is over it, so the edge of what is under it shows.</summary>
        public const float OverMargin = 40f;

        /// <summary>The window's size in the interface's units: 1010 by 720, or the screen less its edges.</summary>
        public static void WindowSize(float uiWidth, float uiHeight, out float w, out float h)
        {
            w = Math.Min(1010f, uiWidth - 20f);
            h = Math.Min(720f, uiHeight - 35f - 20f);
        }

        /// <summary>The content area of a window that wide: less the window's margins (18 each side) and the body's padding (12 each side).</summary>
        public static float ContentWidth(float windowWidth) => windowWidth - 2f * 18f - 2f * 12f;

        public static bool Beside(float contentWidth) => contentWidth >= BesideFrom;

        /// <summary>
        /// The drawer in a content area from <paramref name="x"/>, <paramref name="width"/> wide: where the view goes and how
        /// wide it is, where the drawer goes and how wide, and whether it lies over the view.
        /// </summary>
        public static void Split(float x, float width, out float viewX, out float viewW, out float drawerX, out float drawerW, out bool over)
        {
            viewX = x;
            if (Beside(width))
            {
                drawerW = Width;
                over = false;
                viewW = width - Width - Gap;
                drawerX = x + viewW + Gap;
                return;
            }
            over = true;
            viewW = width;
            drawerW = Math.Max(Math.Min(Width, width - OverMargin), Math.Min(Narrowest, width));
            drawerX = x + width - drawerW;
        }
    }
}
