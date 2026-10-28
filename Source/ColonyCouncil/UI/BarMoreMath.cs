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
    }
}
