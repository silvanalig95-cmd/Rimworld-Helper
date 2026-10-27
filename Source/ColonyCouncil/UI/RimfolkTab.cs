using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of Rimfolk's inspect tabs (Mood, Story). It is as tall as it would like where the screen has room, and
    /// shorter above a tall inspect pane (RimHUD with extra rows, or a big interface scale on a small screen), but never
    /// off the top of the screen (TabFitMath has the numbers). Its content scrolls, so nothing is lost when it's short,
    /// and its long sections fold away with a click on their heading.
    /// </summary>
    public abstract class RimfolkTab : ITab
    {
        /// <summary>The height the tab would like; it gets less where there isn't room.</summary>
        protected abstract float PreferredHeight { get; }

        /// <summary>The top of the inspect pane when a Rimfolk tab was last drawn (for "Rimfolk: check myself"), or -1.</summary>
        public static float LastPaneTopY = -1f;

        /// <summary>The height a Rimfolk tab last got (for "Rimfolk: check myself"), or -1.</summary>
        public static float LastHeight = -1f;

        /// <summary>Asked every time the tab is placed, so it follows the pane as RimHUD or the interface scale changes it.</summary>
        protected override void UpdateSize()
        {
            base.UpdateSize();
            float top;
            try
            {
                // RimHUD moves the pane's top through this same property, so it's always where the tab opens from.
                top = PaneTopY;
            }
            catch
            {
                top = TabFitMath.PaneTopY(Screen.height, Prefs.UIScale, TabFitMath.VanillaPane);
            }
            size.y = TabFitMath.Height(PreferredHeight, top);
            LastPaneTopY = top;
            LastHeight = size.y;
        }
    }

    /// <summary>
    /// The sections of one page that are folded away, by a fixed key per section. A page can start some of them folded
    /// (the Story tab's longer lists). Kept for the session, not saved: a new session starts as the page does.
    /// </summary>
    public class Folds
    {
        private readonly HashSet<string> shutAtFirst;
        /// <summary>The sections the player has opened or shut the other way from how the page starts them.</summary>
        private readonly HashSet<string> flipped = new HashSet<string>();

        public Folds(params string[] shutAtFirst)
        {
            this.shutAtFirst = new HashSet<string>(shutAtFirst ?? new string[0]);
        }

        public bool IsOpen(string key) => key == null || shutAtFirst.Contains(key) == flipped.Contains(key);

        public void Toggle(string key)
        {
            if (key == null) return;
            if (!flipped.Remove(key)) flipped.Add(key);
        }
    }
}
