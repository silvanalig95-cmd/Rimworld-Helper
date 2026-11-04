using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Runs once after all defs are loaded: places Rimfolk's buttons on the bottom bar, merges flavor extensions.</summary>
    [StaticConstructorOnStartup]
    public static class CouncilStartup
    {
        static CouncilStartup()
        {
            try
            {
                ApplyButtonPosition(false);
                FlavorExtensions.Apply();
                LetterVariants.Apply();
                AddStoryTab();
                // Feelings nudge which moments colonists start (an angry one picks at people, a lonely one seeks them out).
                InteractionWorker_Moment.WeightHook = Heart.MomentWeightFactor;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("startup", e);
            }
            try
            {
                HappeningTracker.ApplyEventMode();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("event pools", e);
            }
            try
            {
                // Rimfolk's tellings of the game's moods take the originals' nullifiers (Moods/LikeThought.cs).
                LikeThought.Apply();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("like thoughts", e);
            }
        }

        /// <summary>
        /// Adds the Mood and Story tabs to every humanlike race (vanilla humans and modded races alike), in code rather than by
        /// an XML patch, so it survives other mods rewriting a race's tabs and covers races added by mods. The Culture tab
        /// goes right after the game's Social tab in the list, so it's drawn just left of it (the pane draws its tabs from
        /// right to left); a race without a Social tab gets it at the end.
        /// </summary>
        private static void AddStoryTab()
        {
            foreach (var tabType in new[] { typeof(ITab_Pawn_Mood), typeof(ITab_Pawn_Story) })
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def.race == null || !def.race.Humanlike) continue;
                    if (def.inspectorTabs == null) def.inspectorTabs = new List<System.Type>();
                    if (!def.inspectorTabs.Contains(tabType)) def.inspectorTabs.Add(tabType);
                    if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
                    if (!def.inspectorTabsResolved.Any(t => t.GetType() == tabType))
                        def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
                }
            }
            var culture = typeof(ITab_Pawn_Culture);
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race == null || !def.race.Humanlike) continue;
                try
                {
                    if (def.inspectorTabs == null) def.inspectorTabs = new List<System.Type>();
                    if (!def.inspectorTabs.Contains(culture))
                    {
                        int at = def.inspectorTabs.FindIndex(t => t != null && typeof(ITab_Pawn_Social).IsAssignableFrom(t));
                        def.inspectorTabs.Insert(at >= 0 ? at + 1 : def.inspectorTabs.Count, culture);
                    }
                    if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
                    if (!def.inspectorTabsResolved.Any(t => t != null && t.GetType() == culture))
                    {
                        int at = def.inspectorTabsResolved.FindIndex(t => t is ITab_Pawn_Social);
                        def.inspectorTabsResolved.Insert(at >= 0 ? at + 1 : def.inspectorTabsResolved.Count, InspectTabManager.GetSharedInstance(culture));
                    }
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("culture tab on " + def.defName, e);
                }
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
            var more = CouncilDefOf.Rimfolk_MoreButton;
            var rimfolk = new[] { ours, more, CouncilDefOf.Rimfolk_WorldButton, CouncilDefOf.Council_DefenseButton, CouncilDefOf.Council_RoomsButton };
            var others = DefDatabase<MainButtonDef>.AllDefsListForReading.Where(b => !rimfolk.Contains(b)).OrderBy(b => b.order).ToList();
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
            // The region, defense and planning buttons sit right after the council button (same order, defined after it,
            // in that order: Defs/Misc/Council_Misc.xml).
            var world = CouncilDefOf.Rimfolk_WorldButton;
            if (world != null)
            {
                world.order = ours.order;
                world.buttonVisible = ColonyCouncilMod.Settings.worldButton;
            }
            var defense = CouncilDefOf.Council_DefenseButton;
            if (defense != null)
            {
                defense.order = ours.order;
                defense.buttonVisible = ColonyCouncilMod.Settings.defenseButton;
            }
            var planning = CouncilDefOf.Council_RoomsButton;
            if (planning != null)
            {
                planning.order = ours.order;
                planning.buttonVisible = ColonyCouncilMod.Settings.planButton;
            }
            // More (the bottom bar's buttons put away) goes just before the game's menu, after the council's buttons.
            BarMore.Place();
            if (rebuildBar && Current.ProgramState == ProgramState.Playing && Find.UIRoot is UIRoot_Play play)
            {
                play.mainButtonsRoot = new MainButtonsRoot();
            }
        }

        private static MainButtonDef Named(string defName) => DefDatabase<MainButtonDef>.GetNamedSilentFail(defName);
    }
}
