using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Optional keyboard shortcuts for the council's pages: the game's own key bindings (Defs/Misc/Council_KeyBindings.xml,
    /// under "Rimfolk" in Options → Keyboard), none of them bound until the player picks a key. A page's key opens the
    /// council window on that page (or closes it when that page is already showing); the next and previous page keys
    /// work while the window is open. Nothing happens while a text field has the keyboard.
    /// </summary>
    public static class CouncilKeys
    {
        /// <summary>The pages with a key of their own, by the council window's page names; each key is "Council_Key_" + the name.</summary>
        public static readonly string[] Pages = { "Week", "Everyone", "Wishes", "Moods", "World", "Calendar", "Dilemmas", "Almanac" };

        private static KeyBindingDef[] keys;
        private static KeyBindingDef next, previous;

        private static void Look()
        {
            if (keys != null) return;
            keys = new KeyBindingDef[Pages.Length];
            for (int i = 0; i < Pages.Length; i++) keys[i] = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_" + Pages[i]);
            next = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_NextPage");
            previous = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_PreviousPage");
        }

        /// <summary>A key went down that no text field took.</summary>
        private static bool FreeKeyDown()
        {
            var e = Event.current;
            return e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None && GUIUtility.keyboardControl == 0;
        }

        /// <summary>From the game's OnGUI (WeekMemory): a page's key opens the window there.</summary>
        public static void OnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing || !FreeKeyDown()) return;
            Look();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null || !keys[i].KeyDownEvent) continue;
                MainTabWindow_Council.OpenOrClose(Pages[i]);
                Event.current.Use();
                return;
            }
        }

        /// <summary>From the council window: +1 for the next page, -1 for the previous one, 0 for neither.</summary>
        public static int PageStep()
        {
            if (!FreeKeyDown()) return 0;
            Look();
            int step = next != null && next.KeyDownEvent ? 1 : previous != null && previous.KeyDownEvent ? -1 : 0;
            if (step != 0) Event.current.Use();
            return step;
        }

        /// <summary>The key bound to a page ("F5"), or null when none is.</summary>
        public static string KeyFor(string page)
        {
            Look();
            for (int i = 0; i < Pages.Length; i++)
            {
                if (Pages[i] != page || keys[i] == null) continue;
                try
                {
                    return keys[i].MainKey == KeyCode.None ? null : keys[i].MainKeyLabel;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
