using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The fungus meal (Ideology; Defs/Food/Rimfolk_FungusMeal.xml): a simple meal with fungus in it, cooked on its own
    /// bills (one, or four at once), so a colonist whose faith asks for fungus can be given only these through a food
    /// policy. Alpha Memes' fungus diet is content with a meal that has any fungus in it, so part fungus stretches the
    /// fungus. Finished once at startup: every raw fungus the game knows (other mods' too) counts as the fungus part, the
    /// meal looks like a simple meal, the bills take the simple meal's ingredient filters, and every stove or fire with
    /// the game's simple meal bills cooks these too. Every other meal bill starts with fungus unticked, so the fungus is
    /// kept for these. A colonist whose faith requires fungus also gets a ready-made food policy (FungusDiet).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FungusMeals
    {
        public const string MealName = "Rimfolk_MealFungus";
        public const string RecipeName = "Rimfolk_CookFungusMeal";
        public const string BulkName = "Rimfolk_CookFungusMealBulk";

        /// <summary>Every raw fungus the game knows, and the meal bills that start with it unticked.</summary>
        private static List<ThingDef> rawFungi = new List<ThingDef>();
        private static readonly HashSet<RecipeDef> fungusUnticked = new HashSet<RecipeDef>();

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
            if (meal == null) return;
            var simple = ThingDefOf.MealSimple;
            var raw = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodRaw");
            var fungi = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => IsRawFungus(d, raw)).ToList();

            // One meal, and four at once, each like the game's own simple meal bill of the same size.
            Finish(DefDatabase<RecipeDef>.GetNamedSilentFail(RecipeName), DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple"), fungi);
            Finish(DefDatabase<RecipeDef>.GetNamedSilentFail(BulkName), DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimpleBulk"), fungi);

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

            // Every other meal bill starts with fungus unticked (after Finish, which copies the simple meal's filter).
            rawFungi = fungi;
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                if (UntickFungus(r, fungi)) fungusUnticked.Add(r);
        }

        /// <summary>
        /// A fungus meal bill finished like the game's <paramref name="like"/>: its first ingredient takes any raw
        /// fungus, the simple meal's ingredient rules (no hay, no human meat by default) with every fungus allowed, and
        /// every stove or fire that has the game's bill gets this one too.
        /// </summary>
        private static void Finish(RecipeDef recipe, RecipeDef like, List<ThingDef> fungi)
        {
            if (recipe?.ingredients == null || recipe.ingredients.Count == 0) return;
            var fungusPart = recipe.ingredients[0].filter;
            foreach (var f in fungi) fungusPart.SetAllow(f, true);
            recipe.fixedIngredientFilter = Filter(like?.fixedIngredientFilter ?? recipe.fixedIngredientFilter, fungi);
            recipe.defaultIngredientFilter = Filter(like?.defaultIngredientFilter ?? recipe.fixedIngredientFilter, fungi);
            if (like == null) return;
            recipe.recipeUsers ??= new List<ThingDef>();
            var cache = AccessTools.Field(typeof(ThingDef), "allRecipesCached");
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!d.IsWorkTable || recipe.recipeUsers.Contains(d) || !d.AllRecipes.Contains(like)) continue;
                recipe.recipeUsers.Add(d);
                cache?.SetValue(d, null);
            }
        }

        /// <summary>
        /// A meal bill other than the fungus meal's starts with fungus unticked; the player can tick it back on any bill.
        /// Left alone: a bill that takes no fungus anyway, and one with a part only fungus can fill (another mod's
        /// mushroom dish), which could never be cooked without it.
        /// </summary>
        private static bool UntickFungus(RecipeDef r, List<ThingDef> fungi)
        {
            if (r == null || r.defName == RecipeName || r.defName == BulkName || r.defaultIngredientFilter == null) return false;
            if (r.products == null || !r.products.Any(p => p?.thingDef?.ingestible != null && p.thingDef.ingestible.IsMeal)) return false;
            if (!fungi.Any(f => r.defaultIngredientFilter.Allows(f))) return false;
            if (r.ingredients != null)
                foreach (var part in r.ingredients)
                {
                    if (part?.filter == null) continue;
                    if (!part.filter.AllowedThingDefs.Any(d => !fungi.Contains(d) && (part.IsFixedIngredient || r.fixedIngredientFilter == null || r.fixedIngredientFilter.Allows(d)))) return false;
                }
            var filter = new ThingFilter();
            filter.CopyAllowancesFrom(r.defaultIngredientFilter);
            foreach (var f in fungi) filter.SetAllow(f, false);
            r.defaultIngredientFilter = filter;
            return true;
        }

        /// <summary>
        /// Once, in a save from before other meal bills started with fungus unticked: the meal bills already queued on the
        /// colony's maps get fungus unticked too. A bill ticked back afterwards keeps it.
        /// </summary>
        public static void UntickQueued()
        {
            if (fungusUnticked.Count == 0 || Find.Maps == null) return;
            foreach (var map in Find.Maps)
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver))
                {
                    var bills = (t as IBillGiver)?.BillStack?.Bills;
                    if (bills == null) continue;
                    foreach (var bill in bills)
                    {
                        if (bill?.ingredientFilter == null || !fungusUnticked.Contains(bill.recipe)) continue;
                        foreach (var f in rawFungi) bill.ingredientFilter.SetAllow(f, false);
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

        private static ThingFilter Filter(ThingFilter from, List<ThingDef> fungi)
        {
            var filter = new ThingFilter();
            if (from != null) filter.CopyAllowancesFrom(from);
            foreach (var f in fungi) filter.SetAllow(f, true);
            filter.RecalculateDisplayRootCategory();
            return filter;
        }

        /// <summary>Raw food that counts as fungus (any mod's), for the fungus diet policy.</summary>
        public static bool IsRawFungus(ThingDef d) => IsRawFungus(d, DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodRaw"));
    }
}
