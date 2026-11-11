namespace ColonyCouncil
{
    /// <summary>
    /// Vegetarian and carnivore simple meals (Defs/Food/Rimfolk_DietSimpleMeals.xml; the player, 11 November 2026: "can i
    /// please have dedicated recipes for vegetarian & meat simple meals? so i actually have a dedicated slot for it and can
    /// better manage different diets"): which meal each is, its two bills, the game's own fine meal bills whose ingredients
    /// it takes, the simple meal bills whose stoves and fires cook it, and the tint that tells it apart from the simple meal.
    /// </summary>
    public static class DietMealsMath
    {
        public const string VegMeal = "Rimfolk_MealSimple_Veg", MeatMeal = "Rimfolk_MealSimple_Meat";

        public sealed class Variant
        {
            public string meal, recipe, bulk;

            /// <summary>The game's vegetarian or carnivore fine meal bills: their ingredients are this meal's.</summary>
            public string likeRecipe, likeBulk;

            /// <summary>The game's simple meal bills: every bench that has them cooks this meal too.</summary>
            public string simpleRecipe = "CookMealSimple", simpleBulk = "CookMealSimpleBulk";

            /// <summary>The simple meal's picture is drawn in this colour (red, green, blue, 0 to 1).</summary>
            public float r, g, b;
        }

        public static readonly Variant[] Variants =
        {
            new Variant
            {
                meal = VegMeal, recipe = "Rimfolk_CookMealSimple_Veg", bulk = "Rimfolk_CookMealSimpleBulk_Veg",
                likeRecipe = "CookMealFine_Veg", likeBulk = "CookMealFineBulk_Veg", r = 0.70f, g = 0.95f, b = 0.55f
            },
            new Variant
            {
                meal = MeatMeal, recipe = "Rimfolk_CookMealSimple_Meat", bulk = "Rimfolk_CookMealSimpleBulk_Meat",
                likeRecipe = "CookMealFine_Meat", likeBulk = "CookMealFineBulk_Meat", r = 1.00f, g = 0.66f, b = 0.58f
            }
        };

        /// <summary>The game's simple meal or one of these: a simple meal for whatever counts simple meals by name.</summary>
        public static bool IsSimpleMeal(string defName) => defName == "MealSimple" || defName == VegMeal || defName == MeatMeal;
    }
}
