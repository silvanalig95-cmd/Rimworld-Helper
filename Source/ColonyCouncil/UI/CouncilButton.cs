using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council button on the bottom bar: the game's own button that opens its window (floating while Rimfolk's windows
    /// move, UI/FloatingWindows.cs), with the count of what waits on the player on its corner (UI/Inbox.cs: the same number
    /// as This week's tab and the inbox in every window's corner) while the setting "The count on the council button" is
    /// on. The count is worked out at most twice a second.
    /// </summary>
    public class MainButtonWorker_Council : MainButtonWorker_RimfolkWindow
    {
        private static bool broken;

        public override void DoButton(Rect rect)
        {
            base.DoButton(rect);
            if (broken || Event.current.type != EventType.Repaint) return;
            try
            {
                var s = ColonyCouncilMod.Settings;
                if (s == null || !s.inboxCountOnButton || Current.ProgramState != ProgramState.Playing) return;
                int n = Inbox.Count;
                if (n <= 0) return;
                // Inside the button's own corner, clear of its label.
                UiKit.CountBadge(new Rect(rect.xMax - 20f, rect.y + 6f, 16f, 16f), n);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the count on the council button", e);
            }
        }
    }
}
