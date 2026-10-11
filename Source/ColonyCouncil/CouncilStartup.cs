using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Runs once after all defs are loaded: places the council and rooms buttons, merges flavor extensions.</summary>
    [StaticConstructorOnStartup]
    public static class CouncilStartup
    {
        static CouncilStartup()
        {
            try
            {
                ApplyButtonPosition(false);
                FlavorExtensions.Apply();
                AddStoryTab();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("startup", e);
            }
        }

        /// <summary>
        /// Adds the Story tab to every humanlike race (vanilla humans and modded races alike), in code rather than by
        /// an XML patch, so it survives other mods rewriting a race's tabs and covers races added by mods.
        /// </summary>
        private static void AddStoryTab()
        {
            var tabType = typeof(ITab_Pawn_Story);
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race == null || !def.race.Humanlike) continue;
                if (def.inspectorTabs == null) def.inspectorTabs = new List<System.Type>();
                if (!def.inspectorTabs.Contains(tabType)) def.inspectorTabs.Add(tabType);
                if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
                if (!def.inspectorTabsResolved.Any(t => t is ITab_Pawn_Story))
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
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
            // The rooms button sits right after the council button (same order, defined after it).
            var rooms = CouncilDefOf.Council_RoomsButton;
            if (rooms != null)
            {
                rooms.order = ours.order;
                rooms.buttonVisible = ColonyCouncilMod.Settings.roomsButton;
            }
            if (rebuildBar && Current.ProgramState == ProgramState.Playing && Find.UIRoot is UIRoot_Play play)
            {
                play.mainButtonsRoot = new MainButtonsRoot();
            }
        }

        private static MainButtonDef Named(string defName) => DefDatabase<MainButtonDef>.GetNamedSilentFail(defName);
    }
}
