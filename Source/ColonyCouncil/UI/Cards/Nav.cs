using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Back and forward through Rimfolk's windows, like a browser's (docs/design/interface.md, "Back, forward, search,
    /// following"): every window, view, filter that matters and card opened is a step on one trail (NavTrailMath), kept
    /// for the session, at most 50 steps, whichever way the player got there (a tab, a key, a link, a card). Each window
    /// tells where it is once a frame (<see cref="Observe"/>); a change is a new step, scrolling and folding aren't. The
    /// arrows are in every window's corner (UI/WindowTabs.cs); two keys can be bound (Options, Keyboard, Rimfolk); the
    /// mouse's side buttons work where the game reports them. A step whose thing is gone is dropped on the way.
    /// </summary>
    public static class Nav
    {
        private static readonly NavTrail trail = new NavTrail();
        private static readonly Func<NavStep, bool> alive = Alive;
        /// <summary>A step back or forward was just taken: the next place a window tells replaces it rather than adding one.</summary>
        private static bool settle;
        private static int sideFrame = -1;
        private static KeyBindingDef back, forward;
        private static bool looked;

        private static readonly Dictionary<string, (Func<string> get, Action<string> set)> filters = new Dictionary<string, (Func<string>, Action<string>)>();

        /// <summary>
        /// A page whose filters matter to the way back (the world's history: a people, a town, a kind): how to read them as
        /// a short text and how to put them back.
        /// </summary>
        public static void Filters(string page, Func<string> get, Action<string> set)
        {
            if (!page.NullOrEmpty() && get != null && set != null) filters[page] = (get, set);
        }

        /// <summary>A page's filters as they stand, or null.</summary>
        public static string FiltersOf(string page)
        {
            if (page == null || !filters.TryGetValue(page, out var f)) return null;
            try
            {
                return f.get();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Puts a page's filters as the text says (an old address that is a filter now: "Dreams" on the Chronicle).</summary>
        public static void SetFilter(string page, string value)
        {
            if (page == null || !filters.TryGetValue(page, out var f)) return;
            try
            {
                f.set(value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a page's filter " + page, e);
            }
        }

        private static bool Alive(NavStep s) => s.card.IsNone || ThingRefs.Exists(s.card);

        /// <summary>From each window once a frame while it draws: where it is. A new place is a step.</summary>
        public static void Observe(RimfolkWindow w, string page, ThingRef card)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || page == null) return;
            string f = FiltersOf(page);
            if (trail.HasCurrent && trail.Current.Same(w, page, null, f, card))
            {
                settle = false;
                return;
            }
            var step = new NavStep(w, page, null, f, card);
            if (settle) trail.Replace(step);
            else trail.Push(step);
            settle = false;
        }

        public static bool CanBack => trail.CanBack(alive);

        public static bool CanForward => trail.CanForward(alive);

        public static void Back()
        {
            if (trail.Back(alive, out var s)) Go(s);
        }

        public static void Forward()
        {
            if (trail.Forward(alive, out var s)) Go(s);
        }

        private static void Go(NavStep s)
        {
            settle = true;
            Previews.Hide();
            try
            {
                if (s.filters != null || filters.ContainsKey(s.page ?? ""))
                    if (filters.TryGetValue(s.page, out var f))
                        f.set(s.filters);
                Drawer.FromNav(s.window, s.card);
                Pages.Open(s.page);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("going back or forward", e);
            }
        }

        /// <summary>Where a step leads, in words, for the arrows' tooltips: the page, and the card open there.</summary>
        private static string Words(NavStep s)
        {
            string page = Pages.Name(s.page);
            return s.card.IsNone ? page : "CC_Nav_PageCard".Translate(page.Named("PAGE"), ThingRefs.Name(s.card).Named("CARD")).Resolve();
        }

        private static string Tip(bool forwards)
        {
            int i = trail.At + (forwards ? 1 : -1);
            string key = forwards ? "CC_Nav_ForwardTip" : "CC_Nav_BackTip";
            string where = i >= 0 && i < trail.Count ? Words(trail.Steps[i]) : "";
            string bound = forwards ? KeyLabel(forward) : KeyLabel(back);
            string tip = key.Translate(where.Named("WHERE")).Resolve();
            return bound == null ? tip : tip + " (" + bound + ")";
        }

        private static string KeyLabel(KeyBindingDef k)
        {
            Look();
            try
            {
                return k == null || k.MainKey == KeyCode.None ? null : k.MainKeyLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string backTip, forwardTip;
        private static int tipsAt = -1;

        /// <summary>The two arrows, in a corner's rect (two squares side by side).</summary>
        public static void Arrows(Rect a, Rect b)
        {
            if (tipsAt != trail.At + trail.Count * 1000)
            {
                tipsAt = trail.At + trail.Count * 1000;
                backTip = Tip(false);
                forwardTip = Tip(true);
            }
            if (UiKit.IconButton(a, Icons.Back ?? TexUI.ArrowTexLeft, backTip, CanBack)) Back();
            if (UiKit.IconButton(b, Icons.Forward ?? TexUI.ArrowTexRight, forwardTip, CanForward)) Forward();
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            back = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_Back");
            forward = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_Forward");
        }

        private static bool AnyShowing() =>
            Pages.Showing(RimfolkWindow.Council) || Pages.Showing(RimfolkWindow.World) || Pages.Showing(RimfolkWindow.Defense) || Pages.Showing(RimfolkWindow.Planning);

        /// <summary>From the game's OnGUI (UI/WeekMemory.cs): the keys and the mouse's side buttons, while one of the windows is open.</summary>
        public static void OnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var e = Event.current;
            if (e == null) return;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && GUIUtility.keyboardControl == 0)
            {
                Look();
                if (back != null && back.KeyDownEvent && AnyShowing())
                {
                    Back();
                    e.Use();
                    return;
                }
                if (forward != null && forward.KeyDownEvent && AnyShowing())
                {
                    Forward();
                    e.Use();
                    return;
                }
            }
            // The side buttons, once a frame (Unity's legacy input reports them as buttons 3 and 4 where the system does).
            if (sideFrame == Time.frameCount) return;
            sideFrame = Time.frameCount;
            bool b3, b4;
            try
            {
                b3 = Input.GetMouseButtonDown(3);
                b4 = Input.GetMouseButtonDown(4);
            }
            catch (Exception)
            {
                return;
            }
            if (!b3 && !b4 || !AnyShowing()) return;
            if (b3) Back();
            else Forward();
        }

        /// <summary>A new game: the trail starts empty.</summary>
        public static void Reset()
        {
            trail.Clear();
            settle = false;
        }
    }
}
