using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace ColonyCouncil
{
    // Fewer quests out on the world map, and their sites nearer home (the player, 20 November 2026; Story/QuestPlaceMath.cs
    // has the rules, Story/QuestPlaces.cs reads which quest is which). All of them only touch quests the storyteller picks
    // at random or a site's distance while a quest is being made; a quest a story, an ending, a ritual, an item, a book, a
    // trader, a scanner or a mod's chain gives is never held back. All switch off with the deeper game hooks (RimfolkPatches.On).

    /// <summary>The storyteller's quest slot is choosing a quest now (its weights are read for the choice, not for a list).</summary>
    [HarmonyPatch(typeof(NaturalRandomQuestChooser), nameof(NaturalRandomQuestChooser.ChooseNaturalRandomQuest))]
    public static class Patch_QuestSlotChoosing
    {
        [ThreadStatic] internal static int choosing;

        public static void Prefix() => choosing++;

        public static void Finalizer() => choosing = Math.Max(0, choosing - 1);
    }

    /// <summary>
    /// While the quest slot chooses, a quest out on the world map weighs the setting's share of what it would (50% to begin
    /// with): the quests at home keep their weight, so they make up more of what is offered. Its other uses (a mod's list, the
    /// game's debug tables, Rimfolk's ways to play) read the weight as the game has it.
    /// </summary>
    [HarmonyPatch(typeof(NaturalRandomQuestChooser), nameof(NaturalRandomQuestChooser.GetNaturalRandomSelectionWeight))]
    public static class Patch_QuestSlotAwayWeight
    {
        public static void Postfix(QuestScriptDef quest, ref float __result)
        {
            if (__result <= 0f || Patch_QuestSlotChoosing.choosing <= 0 || !RimfolkPatches.On || quest == null) return;
            try
            {
                __result *= QuestPlaces.ChooserFactor(quest);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: a quest's weight in the quest slot", e);
            }
        }
    }

    /// <summary>
    /// A quest of its own in the storyteller's quest slot (an incident of the quest kind that names its quest, as some mods
    /// add): one out on the world map weighs the setting's share, as above. The work sites' incident is left to their own
    /// slot (<see cref="Patch_WorkSitesAway"/>), so it isn't held back twice.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.BaseChanceThisGame), MethodType.Getter)]
    public static class Patch_QuestIncidentAway
    {
        private static HashSet<IncidentDef> workSites;

        public static void Postfix(IncidentWorker __instance, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On) return;
            var def = __instance?.def;
            if (def?.questScriptDef == null || def.category != IncidentCategoryDefOf.GiveQuest || !QuestPlaces.Holding) return;
            try
            {
                workSites ??= WorkSiteIncidents();
                if (!workSites.Contains(def)) __result *= QuestPlaces.ChooserFactor(def.questScriptDef);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: a quest incident's weight", e);
            }
        }

        /// <summary>The incidents every storyteller's work-site slot fires.</summary>
        private static HashSet<IncidentDef> WorkSiteIncidents()
        {
            var set = new HashSet<IncidentDef>();
            foreach (var st in DefDatabase<StorytellerDef>.AllDefsListForReading)
                if (st.comps != null)
                    foreach (var c in st.comps)
                        if (c is StorytellerCompProperties_WorkSite w && w.incident != null)
                            set.Add(w.incident);
            return set;
        }
    }

    /// <summary>
    /// The work sites (a camp of workers on the world map to raid for its goods) come by a slot of their own in the
    /// storyteller, apart from the quest slot: each one it would send comes at the setting's share, so at 50% half as many.
    /// </summary>
    [HarmonyPatch(typeof(StorytellerComp_WorkSite), nameof(StorytellerComp_WorkSite.MakeIntervalIncidents))]
    public static class Patch_WorkSitesAway
    {
        public static IEnumerable<FiringIncident> Postfix(IEnumerable<FiringIncident> __result)
        {
            if (__result == null) yield break;
            foreach (var fi in __result)
            {
                bool keep = true;
                try
                {
                    keep = !RimfolkPatches.On || !QuestPlaces.Holding || Rand.Chance(QuestPlaces.Share);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests: the work sites' slot", e);
                }
                if (keep) yield return fi;
            }
        }
    }

    /// <summary>
    /// A site's tile picked while a quest is being made (the game's QuestNode_GetSiteTile, and every node, the game's or a
    /// mod's, that asks the game for a new site's tile within a distance): the farthest it may be is lowered by the setting
    /// "How far quest sites may be" (Nearer to begin with: 16 tiles of the game's 27). Where no tile that near fits, it is
    /// picked again as the game asked, so a quest is never lost to the setting. Sites made outside a quest are left alone.
    /// Only the outermost call is changed: one overload calling the other, or the second try, keeps the range it is given.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_QuestSitesNearer
    {
        /// <summary>How many of these calls are running on this thread (the outermost one alone is changed).</summary>
        [ThreadStatic] private static int inside;

        public static IEnumerable<MethodBase> TargetMethods() =>
            typeof(TileFinder).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == nameof(TileFinder.TryFindNewSiteTile)
                && m.ReturnType == typeof(bool) && m.GetParameters().Any(p => p.Name == "minDist") && m.GetParameters().Any(p => p.Name == "maxDist"));

        public static void Prefix(ref int minDist, ref int maxDist, out (int min, int max)? __state)
        {
            __state = null;
            bool outermost = inside == 0;
            inside++;
            if (!outermost || !RimfolkPatches.On || QuestGen.quest == null) return;
            try
            {
                var range = ColonyCouncilMod.Settings?.questSiteRange ?? QuestSiteRange.Nearer;
                QuestPlaceMath.SiteRange(minDist, maxDist, range, out int min, out int max);
                if (min == minDist && max == maxDist) return;
                __state = (minDist, maxDist);
                minDist = min;
                maxDist = max;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: a site nearer home", e);
            }
        }

        public static void Postfix(ref bool __result, ref PlanetTile tile, object[] __args, MethodBase __originalMethod, (int min, int max)? __state)
        {
            if (__result || __state == null || __originalMethod == null || __args == null) return;
            // Nothing that near: as the game asked (this call still counts as running, so the second try is left as it is).
            try
            {
                var ps = __originalMethod.GetParameters();
                var args = (object[])__args.Clone();
                int tileAt = -1;
                for (int i = 0; i < ps.Length && i < args.Length; i++)
                {
                    if (ps[i].Name == "minDist") args[i] = __state.Value.min;
                    else if (ps[i].Name == "maxDist") args[i] = __state.Value.max;
                    else if (ps[i].Name == "tile" && ps[i].IsOut) tileAt = i;
                }
                if (tileAt < 0) return;
                if (__originalMethod.Invoke(null, args) is bool ok && ok && args[tileAt] is PlanetTile found)
                {
                    tile = found;
                    __result = true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: a site as far as the game asked", e);
            }
        }

        public static void Finalizer() => inside = Math.Max(0, inside - 1);
    }
}
