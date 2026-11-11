using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Vegetarian and carnivore simple meals (the player, 11 November 2026: "can i please have dedicated recipes for
/// vegetarian &amp; meat simple meals? so i actually have a dedicated slot for it and can better manage different diets"):
/// the defs are simple meals in every way, each on two bills, and the seams finish them from the game's own fine meal bills
/// and simple meal benches, and let them in once wherever an older save takes simple meals.
/// </summary>
public static class DietMealsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "vegetarian and carnivore simple meals: " + what);
        }

        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Food", "Rimfolk_DietSimpleMeals.xml"));
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string code = File.ReadAllText(Path.Combine(src, "Integrations", "DietMeals.cs"));
        string comp = File.ReadAllText(Path.Combine(src, "CouncilGameComponent.cs"));
        string review = File.ReadAllText(Path.Combine(src, "Building", "StorageReview.cs"));

        string Block(string defName)
        {
            var m = Regex.Match(defs, @"<(ThingDef|RecipeDef)[^>]*>\s*<defName>" + Regex.Escape(defName) + @"</defName>.*?</\1>", RegexOptions.Singleline);
            return m.Success ? m.Value : "";
        }

        C(DietMealsMath.Variants.Length == 2 && DietMealsMath.Variants.Select(v => v.meal).Distinct().Count() == 2, "two meals, vegetarian and carnivore");
        foreach (var v in DietMealsMath.Variants)
        {
            string meal = Block(v.meal), one = Block(v.recipe), four = Block(v.bulk);
            C(meal.Contains("ParentName=\"MealCooked\"") && meal.Contains("<preferability>MealSimple</preferability>") && meal.Contains("<Nutrition>0.9</Nutrition>")
              && meal.Contains("<MarketValue>15</MarketValue>") && meal.Contains("<WorkToMake>300</WorkToMake>"), $"{v.meal} is a simple meal in every way (its place among the meals, nutrition, worth, work)");
            C(!meal.Contains("MayRequire"), $"{v.meal} needs no DLC: the game's own vegetarian and carnivore meals are base game");
            C(one.Contains($"<{v.meal}>1</{v.meal}>") && one.Contains("<count>0.5</count>") && !one.Contains("<workAmount>"), $"{v.recipe} makes one meal from 0.5 nutrition, as the simple meal bill");
            C(four.Contains($"<{v.meal}>4</{v.meal}>") && four.Contains("<count>2.0</count>") && four.Contains("<workAmount>1200</workAmount>"), $"{v.bulk} makes four at once from 2 nutrition");
            C(one.Contains("<workSkill>Cooking</workSkill>") && four.Contains("<workSkill>Cooking</workSkill>") && one.Contains("ParentName=\"CookMealBase\""), $"{v.meal}'s bills are cooking, like the game's");
            C(v.likeRecipe.StartsWith("CookMealFine_") && v.likeBulk.StartsWith("CookMealFineBulk_") && v.simpleRecipe == "CookMealSimple" && v.simpleBulk == "CookMealSimpleBulk",
                $"{v.meal} takes the ingredients of the game's {v.likeRecipe} and the benches of its simple meal bills");
            C(v.r is > 0f and <= 1f && v.g is > 0f and <= 1f && v.b is > 0f and <= 1f, $"{v.meal}'s tint is a colour");
        }
        var veg = DietMealsMath.Variants.First(v => v.meal == DietMealsMath.VegMeal);
        var meat = DietMealsMath.Variants.First(v => v.meal == DietMealsMath.MeatMeal);
        C(Block(veg.meal).Contains("<ingestEffect>EatVegetarian</ingestEffect>") && Block(meat.meal).Contains("<ingestEffect>EatMeat</ingestEffect>"), "each eaten as what it is");
        C(veg.likeRecipe.EndsWith("_Veg") && meat.likeRecipe.EndsWith("_Meat") && veg.recipe.EndsWith("_Veg") && meat.recipe.EndsWith("_Meat"), "the vegetarian meal takes the vegetarian bill's foods, the carnivore meal the carnivore's");
        C(veg.g > veg.r && veg.g > veg.b && meat.r > meat.g && meat.r > meat.b, "told apart at a glance: green for the vegetarian, red for the carnivore");
        C(DietMealsMath.IsSimpleMeal("MealSimple") && DietMealsMath.IsSimpleMeal(veg.meal) && DietMealsMath.IsSimpleMeal(meat.meal) && !DietMealsMath.IsSimpleMeal("MealFine"),
            "all three count as simple meals by name");

        // ---------- The seams ----------
        C(code.Contains("[StaticConstructorOnStartup]") && code.Contains("foreach (var v in DietMealsMath.Variants) SetUp(v);") && code.Contains("CouncilLog.ErrorOnce(\"vegetarian and carnivore simple meals\""),
            "finished once at startup, a failure logged once");
        C(code.Contains("foreach (var d in ing.filter.AllowedThingDefs) part.SetAllow(d, true);") && code.Contains("recipe.fixedIngredientFilter = Copy(like.fixedIngredientFilter);")
          && code.Contains("recipe.defaultIngredientFilter = Copy(like.defaultIngredientFilter);"), "the bills take exactly the foods and default ticks of the game's fine meal bills");
        C(code.Contains("if (!d.IsWorkTable || recipe.recipeUsers.Contains(d) || !d.AllRecipes.Contains(simple)) continue;") && code.Contains("cache?.SetValue(d, null);"),
            "every stove or fire that cooks simple meals (other mods' too) gets the bills");
        C(code.Contains("data.CopyFrom(simple.graphicData);") && code.Contains("data.color = tint;") && code.Contains("meal.uiIconColor = tint;"), "the simple meal's picture in the meal's tint, on the map and in lists");
        C(code.Contains("if (f == null || !f.Allows(simple)) return;") && code.Contains("foodRestrictionDatabase?.AllFoodRestrictions") && code.Contains("AllHaulDestinationsListForReading")
          && code.Contains("GetStoreSettings()?.filter"), "an older save: every food policy, shelf and stockpile that takes simple meals takes these too");
        C(comp.Contains("if (dietMealsVersion < 1)") && comp.Contains("DietMeals.AllowWhereSimple();") && comp.Contains("Scribe_Values.Look(ref dietMealsVersion, \"dietMealsVersion\");"),
            "once per save, remembered");
        C(review.Contains("(\"meals\", new[] { \"MealSimple\", DietMealsMath.VegMeal, DietMealsMath.MeatMeal, \"MealFine\" })"), "the storage review counts them with the meals");
        return n;
    }
}
