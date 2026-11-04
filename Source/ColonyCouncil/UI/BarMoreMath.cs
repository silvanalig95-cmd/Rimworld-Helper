using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The bottom bar's buttons put away under More (UI/BarMore.cs), by def name, as the settings keep them
    /// (CouncilSettings.barPutAway). The game's menu, its inspect pane and More itself always stay on the bar. Names of
    /// buttons from mods no longer loaded are kept, so the button is still put away if that mod comes back.
    /// </summary>
    public static class BarMoreMath
    {
        public const string More = "Rimfolk_MoreButton";

        /// <summary>The buttons that always stay: the game's menu (Esc), the inspect pane, and More.</summary>
        public static readonly string[] Never = { "Menu", "Inspect", More };

        public static bool CanPutAway(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (var n in Never)
                if (n == defName) return false;
            return true;
        }

        /// <summary>Puts the button away (once). True when the list changed.</summary>
        public static bool PutAway(List<string> away, string defName)
        {
            if (away == null || !CanPutAway(defName) || away.Contains(defName)) return false;
            away.Add(defName);
            return true;
        }

        /// <summary>Back on the bar. True when the list changed.</summary>
        public static bool PinBack(List<string> away, string defName)
        {
            if (away == null || defName == null) return false;
            return away.RemoveAll(n => n == defName) > 0;
        }

        /// <summary>A loaded list set right: no empty names, none twice, none of the buttons that always stay.</summary>
        public static List<string> Clean(List<string> away)
        {
            var clean = new List<string>();
            if (away == null) return clean;
            foreach (var n in away)
                if (CanPutAway(n) && !clean.Contains(n)) clean.Add(n);
            return clean;
        }

        /// <summary>More's place on the bar: just before the game's menu (its order less one), or at the end with no menu.</summary>
        public static int OrderFor(int? menuOrder, int lastOrder) => menuOrder.HasValue ? menuOrder.Value - 1 : lastOrder + 1;

        /// <summary>The planning button: the rooms button's def, which it replaced.</summary>
        public const string Planning = "Council_RoomsButton";

        /// <summary>The small planning button there was before the rooms and planning buttons became one.</summary>
        public const string OldPlanning = "Rimfolk_PlanButton";

        /// <summary>
        /// The rooms and planning buttons made one (once, from settings saved before): the planning button is on the bar
        /// if either of the two was (shown by its setting and not put away), put away under More if one was shown but
        /// both were put away, and hidden only if both were switched off. Returns the list put away, with the old small
        /// button's name gone.
        /// </summary>
        public static List<string> MergePlanning(List<string> away, bool roomsShown, bool planShown, out bool show)
        {
            var list = Clean(away);
            bool roomsAway = list.Contains(Planning), planAway = list.Contains(OldPlanning);
            bool onBar = roomsShown && !roomsAway || planShown && !planAway;
            show = roomsShown || planShown;
            list.RemoveAll(n => n == OldPlanning);
            if (onBar) list.RemoveAll(n => n == Planning);
            else if (show && !list.Contains(Planning)) list.Add(Planning);
            return list;
        }
    }
}
