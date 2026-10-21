using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeps the on-screen panels behind every other window: the game's main tabs and dialogs, Rimfolk's planners and
    /// other mods' windows alike. The panels and most windows share the game's interface layer, where the window opened
    /// or clicked last goes on top, so a panel opened after the room planner (or clicked) would cover it. Once a frame
    /// (from <see cref="HudManager.Update"/>), if a panel sits above any other window in the stack's order, the panels
    /// move to the bottom of the stack, keeping their own order, and the game is asked to apply the order on its next
    /// pass. A panel clicked comes to the front for that one frame; the click still works and it goes back at once.
    ///
    /// The game has no way to send a window to the back, so this reorders the window stack's own list (WindowStack.windows,
    /// with its focused window and its "apply the order later" flag) by reflection. If those fields aren't there, it
    /// stops with one warning and the panels simply keep the game's order.
    /// </summary>
    public static class HudWindowOrder
    {
        private static FieldInfo windowsField, focusField, laterField;
        private static bool looked, broken;
        private static readonly List<Window> panels = new List<Window>();

        private static bool Look()
        {
            if (looked) return !broken;
            looked = true;
            try
            {
                windowsField = AccessTools.Field(typeof(WindowStack), "windows");
                focusField = AccessTools.Field(typeof(WindowStack), "focusedWindow");
                laterField = AccessTools.Field(typeof(WindowStack), "updateInternalWindowsOrderLater");
                if (windowsField == null || windowsField.FieldType != typeof(List<Window>) || laterField == null)
                {
                    broken = true;
                    Log.Warning("[Rimfolk] The on-screen panels can't keep behind other windows in this game version; they keep the game's own window order.");
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("on-screen panels: window order", e);
            }
            return !broken;
        }

        /// <summary>A Rimfolk panel (not its options window or the arranging bar, which are ordinary windows).</summary>
        public static bool IsPanel(Window w) => w is HudPanel;

        /// <summary>Puts the panels behind every other window when one of them has come above another window.</summary>
        public static void KeepBehind(WindowStack stack)
        {
            if (broken || stack == null || !Look()) return;
            try
            {
                if (!(windowsField.GetValue(stack) is List<Window> list) || list.Count < 2) return;
                // Is a panel above some other window? (Walk up: once a non-panel has been passed, any panel above it is wrong.)
                bool otherBelow = false, wrong = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var w = list[i];
                    if (w == null) continue;
                    if (IsPanel(w))
                    {
                        if (otherBelow)
                        {
                            wrong = true;
                            break;
                        }
                    }
                    else otherBelow = true;
                }
                if (!wrong) return;
                panels.Clear();
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && IsPanel(list[i]))
                        panels.Add(list[i]);
                list.RemoveAll(IsPanel);
                // The bottom of the stack: the panels are in the lowest layer, so the list stays sorted by layer.
                int at = 0;
                while (at < list.Count && list[at] != null && list[at].layer < WindowLayer.GameUI) at++;
                list.InsertRange(at, panels);
                panels.Clear();
                // A clicked panel became the focused window, which the game would bring forward again: give the focus
                // back to the topmost ordinary window.
                if (focusField != null && focusField.GetValue(stack) is Window focused && IsPanel(focused) && !((HudPanel)focused).KeepsKeyboard)
                {
                    Window top = null;
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i] != null && !IsPanel(list[i]) && !(list[i] is ImmediateWindow))
                        {
                            top = list[i];
                            break;
                        }
                    focusField.SetValue(stack, top);
                }
                laterField.SetValue(stack, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("on-screen panels: keeping behind other windows (off for this session)", e);
            }
        }
    }
}
