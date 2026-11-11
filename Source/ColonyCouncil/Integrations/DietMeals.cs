using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vegetarian and carnivore simple meals (Defs/Food/Rimfolk_DietSimpleMeals.xml; the rules are DietMealsMath.cs):
    /// simple meals in every way, made from plant food only or from meat only, each on its own bills, so food policies,
    /// shelves and stockpiles can tell them apart. Finished once at startup: each bill takes exactly the ingredients of the
    /// game's own vegetarian or carnivore fine meal bill of the same size (so other mods' foods count as the game counts
    /// them), every stove or fire with the game's simple meal bill of that size gets it too, and the meal takes the simple
    /// meal's look in its own tint. In a save from before them, every food policy, shelf and stockpile that takes simple
    /// meals takes these as well (<see cref="AllowWhereSimple"/>), so nothing is left without a place or a taker.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DietMeals
    {
        static DietMeals()
        {
            try
            {
                foreach (var v in DietMealsMath.Variants) SetUp(v);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("vegetarian and carnivore simple meals", e);
            }
        }

        private static void SetUp(DietMealsMath.Variant v)
        {
            var meal = DefDatabase<ThingDef>.GetNamedSilentFail(v.meal);
            if (meal == null) return;
            Finish(DefDatabase<RecipeDef>.GetNamedSilentFail(v.recipe), DefDatabase<RecipeDef>.GetNamedSilentFail(v.likeRecipe), DefDatabase<RecipeDef>.GetNamedSilentFail(v.simpleRecipe));
            Finish(DefDatabase<RecipeDef>.GetNamedSilentFail(v.bulk), DefDatabase<RecipeDef>.GetNamedSilentFail(v.likeBulk), DefDatabase<RecipeDef>.GetNamedSilentFail(v.simpleBulk));
            Look(meal, ThingDefOf.MealSimple, new Color(v.r, v.g, v.b));
        }

        /// <summary>
        /// A bill finished like the game's: its ingredients those of <paramref name="like"/> (the vegetarian or carnivore fine
        /// meal bill), its fixed and default filters the same, and every bench with <paramref name="simple"/> cooks it.
        /// </summary>
        private static void Finish(RecipeDef recipe, RecipeDef like, RecipeDef simple)
        {
            if (recipe?.ingredients == null || recipe.ingredients.Count == 0) return;
            if (like?.ingredients != null && like.ingredients.Count > 0)
            {
                var part = new ThingFilter();
                foreach (var ing in like.ingredients)
                    if (ing?.filter != null)
                        foreach (var d in ing.filter.AllowedThingDefs) part.SetAllow(d, true);
                part.RecalculateDisplayRootCategory();
                recipe.ingredients[0].filter = part;
                if (like.fixedIngredientFilter != null) recipe.fixedIngredientFilter = Copy(like.fixedIngredientFilter);
                if (like.defaultIngredientFilter != null) recipe.defaultIngredientFilter = Copy(like.defaultIngredientFilter);
            }
            if (simple == null) return;
            recipe.recipeUsers ??= new List<ThingDef>();
            var cache = AccessTools.Field(typeof(ThingDef), "allRecipesCached");
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!d.IsWorkTable || recipe.recipeUsers.Contains(d) || !d.AllRecipes.Contains(simple)) continue;
                recipe.recipeUsers.Add(d);
                cache?.SetValue(d, null);
            }
        }

        private static ThingFilter Copy(ThingFilter from)
        {
            var filter = new ThingFilter();
            filter.CopyAllowancesFrom(from);
            filter.RecalculateDisplayRootCategory();
            return filter;
        }

        /// <summary>The simple meal's picture in the meal's own tint, on the map and in lists.</summary>
        private static void Look(ThingDef meal, ThingDef simple, Color tint)
        {
            if (simple?.graphicData == null) return;
            var data = new GraphicData();
            data.CopyFrom(simple.graphicData);
            data.color = tint;
            meal.graphicData = data;
            meal.graphic = data.Graphic;
            if (simple.uiIcon != null)
            {
                meal.uiIcon = simple.uiIcon;
                meal.uiIconColor = tint;
            }
        }

        /// <summary>
        /// Once, in a save from before these meals: every food policy, shelf and stockpile that takes simple meals takes
        /// them too (a filter saved before a def existed leaves it out). The player can then take either off where it
        /// doesn't belong.
        /// </summary>
        public static void AllowWhereSimple()
        {
            var simple = ThingDefOf.MealSimple;
            var meals = DietMealsMath.Variants.Select(v => DefDatabase<ThingDef>.GetNamedSilentFail(v.meal)).Where(d => d != null).ToList();
            if (simple == null || meals.Count == 0) return;
            void Allow(ThingFilter f)
            {
                if (f == null || !f.Allows(simple)) return;
                foreach (var m in meals) f.SetAllow(m, true);
            }
            var policies = Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions;
            if (policies != null)
                foreach (var p in policies) Allow(p?.filter);
            if (Find.Maps == null) return;
            foreach (var map in Find.Maps)
            {
                var places = map?.haulDestinationManager?.AllHaulDestinationsListForReading;
                if (places == null) continue;
                foreach (var place in places.ToList()) Allow(place?.GetStoreSettings()?.filter);
            }
        }
    }
}
