using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The fungus meal (Ideology; Defs/Food/Rimfolk_FungusMeal.xml): a simple meal of fungus alone, cooked on its own
    /// bill, so a colonist whose faith asks for fungus can be given only these through a food policy. Finished once at
    /// startup: every raw fungus the game knows (other mods' too) is an ingredient and nothing else is, the meal looks
    /// like a simple meal, and every stove or fire that cooks simple meals cooks this too. A colonist whose faith
    /// requires fungus also gets a ready-made food policy (FungusDiet).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FungusMeals
    {
        public const string MealName = "Rimfolk_MealFungus";
        public const string RecipeName = "Rimfolk_CookFungusMeal";

        static FungusMeals()
        {
            try
            {
                SetUp();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fungus meal", e);
            }
        }

        private static void SetUp()
        {
            var meal = DefDatabase<ThingDef>.GetNamedSilentFail(MealName);
            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(RecipeName);
            if (meal == null || recipe?.ingredients == null || recipe.ingredients.Count == 0) return;
            var simple = ThingDefOf.MealSimple;
            var cookSimple = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
            var raw = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodRaw");
            var fungi = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => IsRawFungus(d, raw)).ToList();

            // Every ingredient is fungus: any raw fungus, and nothing else.
            var fungusPart = recipe.ingredients[0].filter;
            foreach (var f in fungi) fungusPart.SetAllow(f, true);
            recipe.fixedIngredientFilter = Filter(fungi);
            recipe.defaultIngredientFilter = Filter(fungi);

            // It looks like a simple meal.
            if (simple?.graphicData != null)
            {
                meal.graphicData = simple.graphicData;
                if (simple.graphic != null) meal.graphic = simple.graphic;
                if (simple.uiIcon != null)
                {
                    meal.uiIcon = simple.uiIcon;
                    meal.uiIconColor = simple.uiIconColor;
                }
            }

            // Wherever simple meals are cooked: the campfire, and other mods' stoves.
            if (cookSimple != null)
            {
                recipe.recipeUsers ??= new List<ThingDef>();
                var cache = AccessTools.Field(typeof(ThingDef), "allRecipesCached");
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (!d.IsWorkTable || recipe.recipeUsers.Contains(d) || !d.AllRecipes.Contains(cookSimple)) continue;
                    recipe.recipeUsers.Add(d);
                    cache?.SetValue(d, null);
                }
            }
        }

        /// <summary>Raw food that counts as fungus (Ideology's raw fungus, and other mods' mushrooms).</summary>
        private static bool IsRawFungus(ThingDef d, ThingCategoryDef raw)
        {
            if (d == null || d.category != ThingCategory.Item || !d.IsNutritionGivingIngestible || d.IsCorpse) return false;
            if (!d.IsFungus || d.ingestible.IsMeal) return false;
            return raw == null ? d.ingestible.preferability <= FoodPreferability.RawTasty : d.IsWithinCategory(raw);
        }

        private static ThingFilter Filter(List<ThingDef> fungi)
        {
            var filter = new ThingFilter();
            foreach (var f in fungi) filter.SetAllow(f, true);
            filter.RecalculateDisplayRootCategory();
            return filter;
        }

        /// <summary>Raw food that counts as fungus (any mod's), for the fungus diet policy.</summary>
        public static bool IsRawFungus(ThingDef d) => IsRawFungus(d, DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodRaw"));
    }
}
