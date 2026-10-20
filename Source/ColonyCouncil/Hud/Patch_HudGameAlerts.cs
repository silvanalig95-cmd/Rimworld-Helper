using System;
using HarmonyLib;
using RimWorld;

namespace ColonyCouncil
{
    /// <summary>
    /// Hides the game's own alert list (the column at the right) while Rimfolk's alerts panel shows it instead, as the
    /// player asked ("instead"; a setting, on by default). Only the drawing is skipped: AlertsReadoutUpdate, which keeps
    /// every alert up to date and which the game's logic and the panel both rely on, runs as before. Applied on its own
    /// through the patch bootstrap (Patches/RimfolkPatches.cs): if the game's method changes it's skipped with one
    /// warning and the game's list simply stays. Any error here lets the game draw its list.
    /// </summary>
    [HarmonyPatch(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI))]
    public static class Patch_HudGameAlerts
    {
        private static bool broken;

        public static bool Prefix()
        {
            if (broken) return true;
            try
            {
                return !HudAlerts.HidesGameList();
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("alerts panel: hiding the game's list", e);
                return true;
            }
        }
    }
}
