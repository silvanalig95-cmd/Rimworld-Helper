using System;
using HarmonyLib;
using RimWorld;

namespace ColonyCouncil
{
    /// <summary>
    /// Hides the game's own resource list (down the left) while Rimfolk's inventory panel shows the stock instead, as the
    /// player asked (a setting, on by default). Only the drawing is skipped: the game's resource counter, which the panel
    /// reads and the game's logic uses, counts as before. It also tells the panels' early pass that the game drew this
    /// part of its interface (<see cref="HudPaint.SawGameInterface"/>), which must come after the panels. Applied on its
    /// own through the patch bootstrap (Patches/RimfolkPatches.cs): if the game's method changes it's skipped with one
    /// warning and the game's list simply stays. Any error here lets the game draw its list.
    /// </summary>
    [HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
    public static class Patch_HudGameResources
    {
        private static bool broken;

        public static bool Prefix()
        {
            HudPaint.SawGameInterface();
            if (broken) return true;
            try
            {
                return !HidesGameList();
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("inventory panel: hiding the game's resource list", e);
                return true;
            }
        }

        /// <summary>
        /// Whether the game's resource list is hidden now: the setting is on, the game hooks are on, this patch is in, and
        /// the inventory panel is open and reading the game's counts.
        /// </summary>
        public static bool HidesGameList()
        {
            var s = ColonyCouncilMod.Settings;
            if (broken || s == null || !s.hudHideGameResources || !s.hudShown || !RimfolkPatches.On) return false;
            if (!RimfolkPatches.Applied.Contains(nameof(Patch_HudGameResources)) || HudManager.Failed(HudLayout.Inventory)) return false;
            return HudManager.Window(HudLayout.Inventory) is HudInventory w && w.IsOpen && w.ReadOk;
        }
    }
}
