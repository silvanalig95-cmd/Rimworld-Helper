using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The days of food counted as the meals the stores would make (the player, 14 November 2026: "it should always count how
/// many of my raw foods i can turn into enjoyable meals and use that number", then "always use the nutritional value, not
/// that chickpeas get counted per unit"): a recipe's gain and an ingredient's worth by the arithmetic (Things/MealWorthMath.cs),
/// what counts and what doesn't, the player's own foods by the catalogue of their mods, and the seams: Food Alert reached
/// by name only, its own count kept, the setting, the tooltip's one line, and Rimfolk's own days of food on the same count.
/// </summary>
public static class MealWorthTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "meals the stores would make: " + what);
        }

        bool Near(float a, float b) => Math.Abs(a - b) < 0.0005f;

        // ---------- A recipe's gain ----------
        // The game's simple meal: 0.5 nutrition of raw food, counted by nutrition, makes one meal of 0.9.
        float simple = MealWorthMath.Gain(MealWorthMath.Made(0.9f, 1f), new[] { MealWorthMath.GroupNutrition(0.5f, 1f) });
        C(Near(simple, 1.8f), "a simple meal of 0.9 from 0.5 of raw food: 1.8 times");
        C(Near(MealWorthMath.WorthPerPiece(simple, 0.05f), 0.09f) && Near(MealWorthMath.WorthPerPiece(simple, 0.0125f), 0.0225f),
            "a piece of corn (0.05) makes 0.09 of meal, a chickpea (0.0125) a quarter of that: by nutrition, never by pieces");
        C(Near(MealWorthMath.Gain(MealWorthMath.Made(0.9f, 4f), new[] { MealWorthMath.GroupNutrition(2f, 1f) }), 1.8f), "the bulk bill (four from 2.0) gives the same");
        // The fine meal: a protein group and a plant group, 0.25 each, one meal of 0.9.
        float fine = MealWorthMath.Gain(MealWorthMath.Made(0.9f, 1f), new[] { MealWorthMath.GroupNutrition(0.25f, 1f), MealWorthMath.GroupNutrition(0.25f, 1f) });
        C(Near(fine, 1.8f) && Near(MealWorthMath.WorthPerPiece(fine, 0.05f), 0.09f), "a fine meal's two groups share the meal by what each puts in: 1.8 for meat and for vegetables");
        // The lavish meal: 0.5 and 0.5 for one meal of 1.0.
        C(Near(MealWorthMath.Gain(MealWorthMath.Made(1f, 1f), new[] { 0.5f, 0.5f }), 1f), "a lavish meal gives back what goes in: 1.0 for 1.0");
        // Vanilla Cooking Expanded's simple bake: 1.0 nutrition of flour makes two bakes of 0.7.
        float bake = MealWorthMath.Gain(MealWorthMath.Made(0.7f, 2f), new[] { MealWorthMath.GroupNutrition(1f, 1f) });
        C(Near(bake, 1.4f) && Near(MealWorthMath.WorthPerPiece(bake, 0.05f), 0.07f), "a recipe that makes several meals at once: two bakes of 0.7 from 1.0 of flour, 0.07 a flour");
        // Its fine bake: 0.25 protein, 0.25 plant and 0.5 flour make two fine bakes of 0.8.
        float fineBake = MealWorthMath.Gain(MealWorthMath.Made(0.8f, 2f), new[] { 0.25f, 0.25f, 0.5f });
        C(Near(fineBake, 1.6f), "three groups: two fine bakes of 0.8 from 1.0 in all, 1.6 for each ingredient");
        // A recipe counted by pieces: ten flour of 0.05 each make one simple meal.
        float pieces = MealWorthMath.Gain(MealWorthMath.Made(0.9f, 1f), new[] { MealWorthMath.GroupNutrition(10f, 0.05f) });
        C(Near(MealWorthMath.GroupNutrition(10f, 0.05f), 0.5f) && Near(pieces, 1.8f) && Near(MealWorthMath.WorthPerPiece(pieces, 0.05f), 0.09f),
            "a recipe counted by pieces: ten flour of 0.05 put in 0.5, so a flour makes 0.09, as by nutrition");
        // A gourmet meal's condiment counted by pieces: its little nutrition takes its little share.
        float gourmet = MealWorthMath.Gain(MealWorthMath.Made(0.7f, 1f), new[] { 0.5f, 0.5f, MealWorthMath.GroupNutrition(1f, 0.01f) });
        C(gourmet < 0.7f && gourmet > 0.69f, "a condiment by the piece puts in only its own nutrition");
        // The cap: a recipe of ten meals from 0.5 is counted at three times, the game's best (paste: 0.9 of 0.3).
        C(Near(MealWorthMath.Gain(MealWorthMath.Made(0.9f, 10f), new[] { 0.5f }), MealWorthMath.MaxGain) && MealWorthMath.MaxGain == 3f
          && Near(MealWorthMath.Gain(MealWorthMath.Made(0.9f, 1f), new[] { 0.3f }), 3f), "never more than three times, the game's own nutrient paste");
        C(MealWorthMath.Gain(0.9f, new float[0]) == 0f && MealWorthMath.Gain(0.9f, new[] { 0f, -1f }) == 0f && MealWorthMath.Gain(0f, new[] { 0.5f }) == 0f && MealWorthMath.Gain(0.9f, null) == 0f,
            "a recipe that takes no nutrition, or makes nothing, gives nothing");
        C(MealWorthMath.GroupNutrition(-1f, 1f) == 0f && MealWorthMath.WorthPerPiece(-1f, 0.05f) == 0f && MealWorthMath.Made(0.9f, -2f) == 0f, "nothing below none");

        // ---------- The best of several, and what counts ----------
        C(Near(MealWorthMath.BestGain(new[] { bake, fineBake, 0.9f }), 1.6f) && MealWorthMath.BestGain(new float[0]) == 0f && MealWorthMath.BestGain(new[] { 12f }) == 3f,
            "an ingredient counts by its best recipe (flour: the fine bake over the simple and the lavish), capped too");
        var food = MealWorthMath.Stored.Food;
        C(MealWorthMath.KindOf(true, false, true, false) == MealWorthMath.Stored.Nothing, "animal feed (kibble) counts nothing");
        C(MealWorthMath.PerPiece(MealWorthMath.KindOf(true, false, false, false), 0.05f, 0f, false) == 0f, "what only animals eat and no recipe takes (hay) counts nothing");
        C(MealWorthMath.KindOf(false, false, false, false) == MealWorthMath.Stored.Nothing && MealWorthMath.KindOf(true, true, false, true) == MealWorthMath.Stored.Nothing,
            "what isn't food, a drug or a corpse counts nothing");
        C(Near(MealWorthMath.PerPiece(MealWorthMath.KindOf(true, false, false, true), 0.9f, 3f, true), 0.9f), "a meal in store counts its own nutrition, whatever recipe takes it");
        C(Near(MealWorthMath.PerPiece(food, 0.05f, 1.6f, false), 0.08f), "flour no one eats raw counts the meals it makes");
        C(Near(MealWorthMath.PerPiece(food, 0.05f, 0f, true), 0.05f) && MealWorthMath.PerPiece(food, 0.05f, 0f, false) == 0f,
            "no stove: raw food counts as it is, flour nothing");
        C(Near(MealWorthMath.PerPiece(food, 0.05f, 0.9f, true), 0.05f), "food worth more raw than cooked counts raw");
        C(Near(MealWorthMath.Total(new[] { (10, 0.09f), (2, 0.9f), (100, 0f), (-3, 1f) }), 2.7f) && MealWorthMath.Total(null) == 0f, "the total: pieces times what each counts for");
        C(MealWorthMath.Servings(0.9f * 9f) == 9 && MealWorthMath.Servings(75 * 0.05f) == 4 && MealWorthMath.Servings(0f) == 0 && MealWorthMath.Servings(10 * 0.7f) == 7,
            "meals in servings: nine simple meals are nine, a stack of 75 pemmican four, ten bakes of 0.7 seven");

        // ---------- The player's own foods, by the catalogue of their mods ----------
        var items = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv"));
        var head = items[0].Split('\t').ToList();
        int iName = head.IndexOf("defName"), iActive = head.IndexOf("active"), iPref = head.IndexOf("preferability"), iType = head.IndexOf("foodType"), iNut = head.IndexOf("nutrition");
        var row = items.Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > iNut && c[iActive] == "yes").GroupBy(c => c[iName]).ToDictionary(g => g.Key, g => g.Last());
        float Nut(string d) => row.TryGetValue(d, out var c) && float.TryParse(c[iNut], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : -1f;
        string Pref(string d) => row.TryGetValue(d, out var c) ? c[iPref] : "";
        string Type(string d) => row.TryGetValue(d, out var c) ? c[iType] : "";
        C(Pref("VCE_Flour") == "NeverForNutrition" && Type("VCE_Flour") == "Plant" && Near(Nut("VCE_Flour"), 0.05f),
            "Vanilla Cooking Expanded's flour: plant matter of 0.05, not a person's food, so Food Alert's own count never has it");
        C(Pref("VCE_SimpleBake") == "MealSimple" && Near(Nut("VCE_SimpleBake"), 0.7f) && Pref("VCE_FineBake") == "MealFine" && Near(Nut("VCE_FineBake"), 0.8f),
            "its bakes are enjoyable meals: a simple bake of 0.7, a fine bake of 0.8");
        C(Near(Nut("VCE_RawChickpeas"), 0.0125f) && Pref("VCE_RawChickpeas") == "RawBad" && Near(Nut("RawCorn"), 0.05f), "chickpeas weigh in at 0.0125 a piece, corn at 0.05");
        C(Type("Kibble") == "Kibble" && Pref("Kibble") == "RawBad" && Type("Hay") == "Plant" && Pref("Hay") == "DesperateOnlyForHumanlikes",
            "kibble is feed by its kind, hay what only animals eat");
        C(Pref("VCE_SimpleDessert") == "DesperateOnly", "a dessert is no meal for hunger: not counted");
        // The screenshot's stores (chickpeas 416, corn 77, kibble 105, nine simple meals and a fine one): Food Alert's 23.
        float foodAlert = 416 * Nut("VCE_RawChickpeas") + 77 * Nut("RawCorn") + 105 * Nut("Kibble") + 9 * Nut("MealSimple") + 1 * Nut("MealFine");
        C(Math.Abs(foodAlert - 23.3f) < 0.05f, "Food Alert's own count of the screenshot's stores comes to its 23 (kibble in, flour out)");
        float rimfolk = MealWorthMath.Total(new[]
        {
            (416, MealWorthMath.PerPiece(food, Nut("VCE_RawChickpeas"), simple, true)), (77, MealWorthMath.PerPiece(food, Nut("RawCorn"), simple, true)),
            (105, MealWorthMath.PerPiece(MealWorthMath.KindOf(true, false, true, false), Nut("Kibble"), 0f, true)),
            (9, MealWorthMath.PerPiece(MealWorthMath.Stored.Meal, Nut("MealSimple"), 0f, false)), (1, MealWorthMath.PerPiece(MealWorthMath.Stored.Meal, Nut("MealFine"), 0f, false))
        });
        C(Math.Abs(rimfolk - (9.36f + 6.93f + 9f)) < 0.05f, "Rimfolk's count of the same, before the flour: raw food as simple meals, kibble out");

        // ---------- The seams ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string code = File.ReadAllText(Path.Combine(src, "Integrations", "FoodAlertCount.cs"));
        string worth = File.ReadAllText(Path.Combine(src, "Things", "MealWorth.cs"));
        C(code.Contains("GenTypes.GetTypeInAnyAssembly(CounterType)") && code.Contains("\"FoodAlert.NutritionCounter\"") && code.Contains("\"NutritionFinders\"")
          && code.Contains("is IDictionary<string, Func<Map, float>> finders") && !Regex.IsMatch(File.ReadAllText(Path.Combine(src, "ColonyCouncil.csproj")), "FoodAlert"),
            "Food Alert reached by name only: its list of counters by reflection, no reference to its assembly");
        C(code.Contains("finders.TryGetValue(DefaultKey, out original);") && code.Contains("finders[DefaultKey] = Count;") && code.Contains("return original(map);"),
            "Rimfolk's count takes the \"default\" entry's place and keeps Food Alert's own to answer");
        C(Regex.Matches(code, @"catch \(Exception e\)").Count >= 3 && code.Contains("failed = true;") && code.Contains("CouncilLog.ErrorOnce(\"Food Alert's count"),
            "Rimfolk's count never throws into Food Alert: a failure logged once, Food Alert's own count from then on");
        C(code.Contains("foodAlertMealCount ?? true") && code.Contains("public static bool Counting =>"), "the setting, on by default, switches between the two");
        C(code.Contains("public static string StandsDownWhy()") && code.Contains("[HarmonyPatch]") && code.Contains("AccessTools.Method(type, ReadoutMethod)")
          && code.Contains("\"activeTips\"") && code.Contains("getter.Target is Reword"), "the tooltip's line by a postfix on Food Alert's readout found by name, standing down without it");
        C(code.Contains("\"SettingDescription\".Translate()") && !File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "ColonyCouncil.xml")).Contains("<SettingDescription>"),
            "Food Alert's own words read, never overridden");
        C(worth.Contains("r.AvailableNow") && worth.Contains("SomeoneCanCook") && worth.Contains("bench.AllRecipes") && worth.Contains("getter.ValuePerUnitOf(t)")
          && worth.Contains("r.defaultIngredientFilter") && worth.Contains("map.resourceCounter.AllCountedAmounts"),
            "the recipes read as the game runs: research done, a bench built, a cook who can, by pieces or by nutrition, what a new bill takes, the game's own count of the stores");
        C(worth.Contains("allBuildingsColonist.Count") && worth.Contains("RereadTicks = Sched.HourTicks") && worth.Contains("TotalTicks = 60"),
            "kept per map: read again when the benches change and every hour, the total for a second");
        C(File.ReadAllText(Path.Combine(src, "Advisor", "AdvisorSignals.cs")).Contains("return (MealWorth.Total(map) + Avail.StoredPasteNutrition(map)) / (eaters * 1.6f);"),
            "Rimfolk's own days of food (the advisor, the panels, the letters, the heart) on the same count");
        string trade = File.ReadAllText(Path.Combine(src, "Trade", "TradeAdvice.cs")) + File.ReadAllText(Path.Combine(src, "Trade", "TradeScreen.cs"));
        C(trade.Contains("storeWorth = MealWorth.Total(needs.map);") && trade.Contains("MealWorth.PerPiece(TradeAdvice.foodMap, t.ThingDef)") && !trade.Contains("CookGain"),
            "the trade screen's days of food, now and after the trade, on the same count");
        C(File.ReadAllText(Path.Combine(src, "Integrations", "GuestWill.cs")).Contains("GuestWillMath.DaysOfFood(MealWorth.Total(map), eaters)"), "a guest's room for one more on the same count");
        C(File.ReadAllText(Path.Combine(src, "Building", "ColonyNeeds.cs")).Contains("MealWorth.CookingIngredient(d) ? AheadMath.Raw : AheadMath.Feed"),
            "planning ahead counts flour as raw food for cooking, not animal feed");
        C(File.ReadAllText(Path.Combine(src, "PlayOut", "PlaySpots.cs")).Contains("MealWorth.Servings(Meals(map))")
          && File.ReadAllText(Path.Combine(src, "Advisor", "AdvisorSignals5.cs")).Contains("MealWorth.StoredServings(map,"), "meals in store counted in servings by nutrition");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "ColonyCouncil.xml"));
        foreach (var key in new[] { "CC_Settings_FoodAlertMeals", "CC_Settings_FoodAlertMeals_Tip", "CC_SetNote_FoodAlertMeals", "CC_FoodAlert_Counted", "CC_FoodAlert_CountedRaw" })
        {
            var m = Regex.Match(keyed, "<" + key + ">(.*?)</" + key + ">", RegexOptions.Singleline);
            C(m.Success && m.Groups[1].Value.Length > 10 && !m.Groups[1].Value.Contains("—") && !m.Groups[1].Value.Contains("!"), $"{key}: in plain words, no em dash or exclamation mark");
        }
        C(File.ReadAllText(Path.Combine(root, "Defs", "IntegrationDefs", "Council_Mods_Support.xml")).Contains("<live>foodalert</live>"),
            "Food Alert among the mods Rimfolk works with, its reading checked by the self-check");
        return n;
    }
}
