using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Runs once after all defs are loaded: places the council button and the architect "rooms" category.</summary>
    [StaticConstructorOnStartup]
    public static class CouncilStartup
    {
        static CouncilStartup()
        {
            try
            {
                ApplyButtonPosition(false);
                PlaceArchitectCategory();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("startup", e);
            }
        }

        /// <summary>
        /// Gives the council button an order that puts it where the player wants it, computed from the buttons
        /// that actually exist (DLCs and other mods add their own). Ties sort by load order, and this mod loads
        /// after the game, so "same order as X" means "right after X".
        /// </summary>
        public static void ApplyButtonPosition(bool rebuildBar)
        {
            var ours = CouncilDefOf.Council_MainButton;
            if (ours == null) return;
            var others = DefDatabase<MainButtonDef>.AllDefsListForReading.Where(b => b != ours).OrderBy(b => b.order).ToList();
            if (others.Count == 0) return;
            MainButtonDef after = null;
            switch (ColonyCouncilMod.Settings.buttonPosition)
            {
                case ButtonPosition.AfterResearch: after = Named("Research"); break;
                case ButtonPosition.AfterHistory: after = Named("History"); break;
                case ButtonPosition.AfterWork: after = Named("Work"); break;
            }
            if (after == null)
            {
                // Before the menu: right after whatever comes last before it.
                var menu = Named("Menu");
                int menuIndex = menu != null ? others.IndexOf(menu) : others.Count;
                after = menuIndex > 0 ? others[menuIndex - 1] : others.Last();
            }
            ours.order = after.order;
            if (rebuildBar && Current.ProgramState == ProgramState.Playing && Find.UIRoot is UIRoot_Play play)
            {
                play.mainButtonsRoot = new MainButtonsRoot();
            }
        }

        private static MainButtonDef Named(string defName) => DefDatabase<MainButtonDef>.GetNamedSilentFail(defName);

        /// <summary>Slots the "rooms" architect category in right after Structure.</summary>
        private static void PlaceArchitectCategory()
        {
            var ours = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("Council_Rooms");
            var structure = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("Structure");
            if (ours == null || structure == null) return;
            var sorted = DefDatabase<DesignationCategoryDef>.AllDefsListForReading.Where(d => d != ours).OrderBy(d => d.order).ToList();
            int i = sorted.IndexOf(structure);
            // Halfway between Structure and its neighbor on either side works whichever way the menu sorts.
            var next = i + 1 < sorted.Count ? sorted[i + 1] : null;
            ours.order = next != null && next.order - structure.order >= 2 ? (structure.order + next.order) / 2 : structure.order;
        }
    }
}
