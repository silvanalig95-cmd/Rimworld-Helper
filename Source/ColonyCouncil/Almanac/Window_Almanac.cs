using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The Almanac in a window of its own (docs/design/interface.md, decision 4): movable and resizable, so it can sit
    /// beside what it explains. The "?" on every view's header opens it on that view's article, the book in every
    /// window's corner opens it where it was, and so do the old addresses ("Almanac", OpenAlmanac). One at a time; it
    /// never pauses the game and leaves the camera free. Since stage 2 of Rimfolk's own look it is in Rimfolk's frame (key
    /// "Almanac"): moved by its title bar, sized from its edges, large and back, where it was left between games, in the
    /// look while that is on, its pages at their own text sizes. With "Rimfolk's windows move and resize" off it is the
    /// game's movable window as before, where it stood kept for the session. Where each page was read to is kept for the
    /// session; the page last read is kept in the settings for the next one.
    /// </summary>
    public class Window_Almanac : FramedWindow
    {
        private static Rect? last;
        private static bool restored;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(900f, UI.screenWidth - 40f), Mathf.Min(760f, UI.screenHeight - 100f));

        public Window_Almanac() : base("Almanac")
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            onlyOneOfTypeAllowed = true;
            layer = WindowLayer.GameUI;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = SoundDefOf.TabClose;
        }

        /// <summary>The Almanac's window is open.</summary>
        public static bool Showing => Find.WindowStack?.WindowOfType<Window_Almanac>() != null;

        /// <summary>Opens the Almanac's window on an article (null: where it was), or turns the open one to it.</summary>
        public static void Open(AlmanacArticleDef article)
        {
            try
            {
                Restore();
                if (article != null) AlmanacPanel.Show(article);
                var stack = Find.WindowStack;
                if (stack == null) return;
                var open = stack.WindowOfType<Window_Almanac>();
                if (open != null)
                {
                    // Already open: in front, turned to the article.
                    stack.Notify_ClickedInsideWindow(open);
                    return;
                }
                stack.Add(new Window_Almanac());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("opening the Almanac", e);
            }
        }

        /// <summary>The Almanac key or button: opens the window, or closes it when open.</summary>
        public static void Toggle()
        {
            var open = Find.WindowStack?.WindowOfType<Window_Almanac>();
            if (open != null) open.Close();
            else Open(null);
        }

        /// <summary>The page last read in an earlier session, once a session, when nothing else was asked for.</summary>
        private static void Restore()
        {
            if (restored) return;
            restored = true;
            var s = ColonyCouncilMod.Settings;
            if (AlmanacPanel.Selected != null || s == null || s.lastAlmanacPage.NullOrEmpty()) return;
            var page = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(s.lastAlmanacPage);
            if (page != null) AlmanacPanel.Show(page);
        }

        private static string title;

        protected override string FrameTitle => title ??= "CC_Tab_Almanac".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.Almanac;

        /// <summary>The first time beside Rimfolk's windows (or where it stood this session, its frame not moving).</summary>
        protected override bool OwnFirstPlace => true;

        protected override void PlaceUnframed()
        {
            var size = InitialSize;
            Rect r;
            if (last.HasValue) r = last.Value;
            else
            {
                // Beside Rimfolk's windows when the screen has room for both (they open at the bottom left), else in the middle.
                float besideX = WindowTabs.Size.x + 30f;
                r = UI.screenWidth - size.x - 20f >= besideX
                    ? new Rect(UI.screenWidth - size.x - 20f, 40f, size.x, size.y)
                    : new Rect((UI.screenWidth - size.x) / 2f, (UI.screenHeight - size.y) / 2f, size.x, size.y);
            }
            r.width = Mathf.Clamp(r.width, 520f, UI.screenWidth);
            r.height = Mathf.Clamp(r.height, 360f, UI.screenHeight);
            r.x = Mathf.Clamp(r.x, 0f, UI.screenWidth - r.width);
            r.y = Mathf.Clamp(r.y, 0f, UI.screenHeight - r.height);
            windowRect = r.Rounded();
        }

        protected override void DoFramedContents(Rect inRect)
        {
            try
            {
                // The game's close button sits in the top right corner: the header's own buttons stay clear of it (the
                // frame's is in its title bar).
                AlmanacPanel.Draw(inRect, TitleInBar ? 0f : 30f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the Almanac's window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UiKit.Ink;
            }
        }

        /// <summary>
        /// Enter (the game's accept key) while typing in its search stays in the Almanac; otherwise it closes it, as the
        /// game's windows do.
        /// </summary>
        public override void OnAcceptKeyPressed()
        {
            if (GUIUtility.keyboardControl != 0)
            {
                Event.current.Use();
                return;
            }
            base.OnAcceptKeyPressed();
        }

        public override void PreClose()
        {
            base.PreClose();
            last = windowRect;
        }

        public override void PostClose()
        {
            base.PostClose();
            Previews.Hide();
            Remember();
        }

        /// <summary>Keeps the page last read in the settings for next time; the file is written only when it changed.</summary>
        private static void Remember()
        {
            try
            {
                var s = ColonyCouncilMod.Settings;
                if (s == null) return;
                string almanac = AlmanacPanel.Selected?.defName ?? "";
                if (s.lastAlmanacPage == almanac) return;
                s.lastAlmanacPage = almanac;
                s.Write();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saving the Almanac's page", e);
            }
        }
    }
}
