using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rimfolk's item card, step 1 (the player, 9 November 2026: "also like item card for everything"; docs/design/rimfolk-look.md,
/// stage 3, point 5): food, drink, drugs and medicine. The rules (Items/ItemCardMath.cs) on the base game's own: the kind of
/// a thing, the tags in the shared six categories with why, the line in plain words, the small sums (colonist-days, suppers,
/// a meal's worth, tending, the colony short or with spare); and the seams (the window hook and its pass-through, the button
/// on the game's card, the setting, the frame, the keys, the model refreshed rarely).
/// </summary>
public static class ItemCardTests
{
    private static ItemFacts FineMeal() => new ItemFacts
    {
        Label = "fine meal", Kind = ItemKind.Food, Nutrition = 0.9f, Preferability = ThingRolesMath.MealFine, Meal = true, HumanEdible = true, RotDays = 4f,
        TasteThought = "Ate fine meal", TasteMood = 5f, TasteDays = 1f, FeastDish = true, Value = 28f, Mass = 0.44f
    };

    private static ItemFacts SimpleMeal() => new ItemFacts
    {
        Label = "simple meal", Kind = ItemKind.Food, Nutrition = 0.9f, Preferability = ThingRolesMath.MealSimple, Meal = true, HumanEdible = true, RotDays = 4f,
        Value = 15f, Mass = 0.44f
    };

    private static ItemFacts SurvivalMeal() => new ItemFacts
    {
        Label = "packaged survival meal", Kind = ItemKind.Food, Nutrition = 0.9f, Preferability = ThingRolesMath.MealSimple, Meal = true, HumanEdible = true,
        RotDays = -1f, Provisions = true, TravelRank = 2, Value = 24f, Mass = 0.3f
    };

    private static ItemFacts Pemmican() => new ItemFacts
    {
        Label = "pemmican", Kind = ItemKind.Food, Nutrition = 0.05f, Preferability = ThingRolesMath.MealSimple, Meal = true, HumanEdible = true, RotDays = 70f,
        Provisions = true, TravelRank = 1, Value = 1.4f, Mass = 0.018f
    };

    private static ItemFacts Potatoes() => new ItemFacts
    {
        Label = "potatoes", Kind = ItemKind.Food, Nutrition = 0.05f, Preferability = ThingRolesMath.RawBad, HumanEdible = true, RotDays = 30f,
        TasteThought = "Ate raw food", TasteMood = -7f, TasteDays = 1f, Cookable = true, CookAt = "electric stove", Value = 1.1f, Mass = 0.03f
    };

    private static ItemFacts Meat() => new ItemFacts
    {
        Label = "muffalo meat", Kind = ItemKind.Food, Nutrition = 0.05f, Preferability = ThingRolesMath.RawBad, HumanEdible = true, RotDays = 2f,
        TasteThought = "Ate raw food", TasteMood = -7f, TasteDays = 1f, Cookable = true, PoisonFixed = 0.02f, Value = 2f, Mass = 0.03f
    };

    private static ItemFacts Beer() => new ItemFacts
    {
        Label = "beer", Kind = ItemKind.Drink, IsDrug = true, DrugKind = 2, Alcohol = true, Fluid = true, Addictive = true, Addictiveness = 0.01f, MinTolerance = 0.25f,
        Joy = 0.17f, ToastDrink = true, Nutrition = 0.08f, HumanEdible = true, Value = 12f, Mass = 0.3f, RotDays = -1f
    };

    private static ItemFacts Yayo() => new ItemFacts
    {
        Label = "yayo", Kind = ItemKind.Drug, IsDrug = true, DrugKind = 3, Addictive = true, Addictiveness = 0.04f, Joy = 0.35f, OverdoseMin = 0.18f, OverdoseMax = 0.35f,
        LargeOverdose = 0.01f, HighMood = 35f, HighDays = 0.75f, Value = 21f, Mass = 0.05f, RotDays = -1f
    };

    private static ItemFacts Medicine(string label, float potency, float max) => new ItemFacts
    {
        Label = label, Kind = ItemKind.Medicine, Potency = potency, QualityMax = max, MedicineByNeed = true, Value = potency * 18f, Mass = 0.5f, RotDays = -1f
    };

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "item card: " + what);
        }
        bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps;
        bool Has(ItemFacts f, string id) => ItemCardMath.Tags(f).Any(t => t.Id == id);
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The kind ----------
        C(ItemCardMath.KindOf(true, false, false, false) == ItemKind.Medicine, "medicine is medicine");
        C(ItemCardMath.KindOf(false, true, true, true) == ItemKind.Drink, "a drug that is drunk (beer) is a drink");
        C(ItemCardMath.KindOf(false, true, false, false) == ItemKind.Drug, "a drug that isn't drunk (yayo) is a drug");
        C(ItemCardMath.KindOf(false, false, true, true) == ItemKind.Drink, "food that is drunk (a juice) is a drink");
        C(ItemCardMath.KindOf(false, false, false, true) == ItemKind.Food, "anything else that feeds is food");
        C(ItemCardMath.KindOf(false, false, false, false) == ItemKind.None, "anything else opens the game's card");
        C(ItemCardMath.ShowsFood(Beer()) == false && ItemCardMath.ShowsDrug(Beer()), "beer shows as a drug, not as food");
        C(ItemCardMath.ShowsFood(new ItemFacts { Kind = ItemKind.Drink }) && !ItemCardMath.ShowsDrug(new ItemFacts { Kind = ItemKind.Drink }), "a juice shows as food");

        // ---------- The small sums ----------
        C(Near(ItemCardMath.ColonistDays(0.9f, 2), 1.125f), "two meals are 1.1 colonist-days (a colonist eats 1.6 a day)");
        C(ItemCardMath.ToAMeal(0.05f) == 18 && ItemCardMath.ToAMeal(0.9f) == 1 && ItemCardMath.ToAMeal(0f) == 0, "18 potatoes to a meal's worth");
        C(ItemCardMath.Suppers(0.9f, 2) == 2 && ItemCardMath.Suppers(1f, 3) == 3 && ItemCardMath.Suppers(0.05f, 75) == 4, "suppers: a meal each, else by nutrition");
        C(Near(ItemCardMath.TendQuality(1.2f, 0.6f, 0.7f), 0.7f), "herbal medicine caps a good doctor's tending at 70%");
        C(Near(ItemCardMath.TendQuality(0.8f, 1f, 1f), 0.8f) && Near(ItemCardMath.TendQuality(0.5f, ItemCardMath.NoMedicinePotency, ItemCardMath.NoMedicineQualityMax), 0.15f),
            "tending is the doctor's quality times the potency, held at the medicine's best");
        C(ItemCardMath.CareTier(Medicine("herbal medicine", 0.6f, 0.7f)) == MedicineChoiceMath.Herbal && ItemCardMath.CareTier(Medicine("medicine", 1f, 1f)) == MedicineChoiceMath.Normal
          && ItemCardMath.CareTier(Medicine("glitterworld medicine", 1.6f, 1.3f)) == MedicineChoiceMath.Best, "the care each medicine needs, as medicine by what is treated reads it");
        C(ItemCardMath.FoodShort(7f) && !ItemCardMath.FoodShort(12f) && !ItemCardMath.FoodShort(-1f), "food is short under the trade advice's ten days");
        C(ItemCardMath.FoodSpare(25f) && !ItemCardMath.FoodSpare(15f), "food is spare beyond twice the ten days");
        C(ItemCardMath.MedicineNeed(6) == 18 && ItemCardMath.MedicineNeed(1) == 6 && ItemCardMath.MedicineKeep(6) == 36, "medicine: the trade advice's floor and reserve");
        C(ItemCardMath.MedicineShort(5, 6) && !ItemCardMath.MedicineShort(20, 6) && ItemCardMath.MedicineSpare(40, 6) && !ItemCardMath.MedicineSpare(30, 6), "medicine short or spare");
        var rotten = Meat();
        rotten.Rotting = true;
        C(Near(ItemCardMath.PoisonOdds(rotten), 1f), "rotting food poisons for sure");
        C(Near(ItemCardMath.PoisonOdds(new ItemFacts { PoisonChance = 0.034f, PoisonFixed = 0.02f }), 0.034f)
          && Near(ItemCardMath.PoisonOdds(new ItemFacts { PoisonChance = 0.034f, PoisonFactor = 0.5f }), 0.017f), "food poisoning: the larger chance, by the storyteller's factor");
        C(ItemCardMath.Pct(0.034f) == "3.4%" && ItemCardMath.Pct(0.02f) == "2%" && ItemCardMath.Pct(0.35f) == "35%" && ItemCardMath.Pct(1f) == "100%", "shares in words");
        C(ItemCardMath.Days(4f) == "4 days" && ItemCardMath.Days(1f) == "1 day" && ItemCardMath.Days(0.5f) == "12 hours" && ItemCardMath.Days(0.75f) == "18 hours"
          && ItemCardMath.Days(3.24f) == "3.2 days" && ItemCardMath.Days(30f) == "30 days", "days and hours in words");
        C(ItemCardMath.Mood(5f) == "+5" && ItemCardMath.Mood(-7f) == "-7", "a mood with its sign");
        C(ItemCardMath.A("campfire") == "a campfire" && ItemCardMath.A("electric stove") == "an electric stove", "a bench with its article");

        // ---------- Food: the tags and the line ----------
        var fine = FineMeal();
        C(Has(fine, "mood-lift") && Has(fine, "spoils-fast") && Has(fine, "nutritious") && Has(fine, "feast-dish") && !Has(fine, "keeps-long") && !Has(fine, "trade-good"),
            "a fine meal lifts the mood, spoils fast, is nutritious and a feast's dish; it doesn't keep, so it is no trade good");
        C(ItemCardMath.Tags(fine).First(t => t.Id == "mood-lift").Why == "eating it gives \"Ate fine meal\": +5 mood for 1 day", "the mood lift's reason is the game's thought");
        C(ItemCardMath.Plain(fine) == "Fine meal: lifts the mood (+5), keeps 4 days, a supper for one colonist.", "a fine meal in plain words: " + ItemCardMath.Plain(fine));
        var two = FineMeal();
        two.InHand = true;
        two.Count = 2;
        two.DaysLeftHere = 3.24f;
        C(ItemCardMath.Plain(two) == "Fine meal: lifts the mood (+5), keeps 3.2 days more, enough for 2 colonists' supper.", "two fine meals in hand: " + ItemCardMath.Plain(two));
        two.FrozenHere = true;
        C(ItemCardMath.Plain(two).Contains("kept frozen"), "a stack in the freezer is kept frozen");
        C(ItemCardMath.Plain(SimpleMeal()) == "Simple meal: keeps 4 days, a supper for one colonist.", "a simple meal in plain words");
        var survival = SurvivalMeal();
        C(Has(survival, "keeps-long") && Has(survival, "provisions") && Has(survival, "trade-good") && Has(survival, "nutritious") && !Has(survival, "spoils-fast"),
            "a survival meal keeps, is provisions and a trade good (80 silver a kilo)");
        C(ItemCardMath.Tags(survival).First(t => t.Id == "provisions").Why.Contains("packs it first"), "the journey planner packs what never rots first");
        C(ItemCardMath.Plain(survival) == "Packaged survival meal: never spoils, a supper for one colonist, packed for journeys.", "a survival meal in plain words");
        var pemmican = Pemmican();
        C(Has(pemmican, "keeps-long") && Has(pemmican, "provisions") && !Has(pemmican, "nutritious") && Has(pemmican, "trade-good"), "pemmican keeps, is provisions, isn't a meal's worth a piece");
        C(ItemCardMath.Plain(pemmican) == "Pemmican: keeps 70 days, 18 to a meal's worth, packed for journeys.", "pemmican in plain words: " + ItemCardMath.Plain(pemmican));
        var potatoes = Potatoes();
        C(Has(potatoes, "raw") && Has(potatoes, "keeps-long") && Has(potatoes, "cooking") && !Has(potatoes, "bad-taste") && !Has(potatoes, "provisions"),
            "potatoes are raw, keep and want cooking");
        C(ItemCardMath.Tags(potatoes).First(t => t.Id == "cooking").Why == "raw: cooked into a meal at an electric stove", "the cooking tag names the colony's bench");
        C(ItemCardMath.Plain(potatoes) == "Potatoes: best cooked (-7 eaten raw), keeps 30 days, 18 to a meal's worth.", "potatoes in plain words: " + ItemCardMath.Plain(potatoes));
        var meat = Meat();
        C(Has(meat, "food-poisoning") && Has(meat, "spoils-fast") && Has(meat, "raw"), "raw meat may poison, spoils fast and is raw");
        C(ItemCardMath.Tags(meat).First(t => t.Id == "food-poisoning").Why == "its kind gives people food poisoning 2% of the time", "the poisoning's reason is its kind's own chance");
        var stack = Meat();
        stack.InHand = true;
        stack.Count = 75;
        stack.Rotting = true;
        C(ItemCardMath.Tags(stack).First(t => t.Id == "food-poisoning").Why.StartsWith("it is rotting"), "a rotting stack poisons whoever eats it");
        C(ItemCardMath.Plain(stack) == "Muffalo meat: best cooked (-7 eaten raw), already rotting, feeds a colonist 2.3 days.", "a rotting stack of meat in plain words: " + ItemCardMath.Plain(stack));
        var cooked = Meat();
        cooked.InHand = true;
        cooked.PoisonChance = 0.034f;
        C(ItemCardMath.Tags(cooked).First(t => t.Id == "food-poisoning").Why.StartsWith("this stack gives food poisoning 3.4%"), "a stack's own chance, from its cook, is named");
        var kibble = new ItemFacts
        {
            Label = "kibble", Kind = ItemKind.Food, Nutrition = 0.05f, Preferability = ThingRolesMath.RawBad, HumanEdible = true, TasteThought = "Ate kibble", TasteMood = -12f,
            TasteDays = 1f, RotDays = -1f
        };
        C(Has(kibble, "bad-taste") && !Has(kibble, "raw") && !Has(kibble, "cooking") && ItemCardMath.Plain(kibble).Contains("sours the mood (-12)"),
            "kibble, which nothing cooks, tastes bad rather than being raw");
        var hay = new ItemFacts { Label = "hay", Kind = ItemKind.Food, Nutrition = 0.05f, HumanEdible = false, RotDays = 60f };
        C(ItemCardMath.Plain(hay) == "Hay: animal feed, keeps 60 days." && !Has(hay, "nutritious"), "hay is animal feed");
        var human = Meat();
        human.Label = "human meat";
        human.OfPerson = true;
        C(ItemCardMath.Tags(human)[0].Id == "of-person" && ItemCardMath.Plain(human).StartsWith("Human meat: of a person"), "human meat says so first");
        var insect = Meat();
        insect.Insect = true;
        C(Has(insect, "insect") && ItemCardMath.Plain(insect).Contains("insect meat"), "insect meat says so");

        // ---------- Drinks and drugs ----------
        var beer = Beer();
        C(Has(beer, "addictive") && Has(beer, "toast-drink") && Has(beer, "recreation") && !Has(beer, "overdose") && !Has(beer, "gift") && !Has(beer, "nutritious"),
            "beer is a toast's drink, recreation, mildly addictive, no overdose, too cheap for a gift");
        C(ItemCardMath.Tags(beer).First(t => t.Id == "addictive").Why == "each dose may addict (1%) once its tolerance is 25% or more", "beer's addiction by its own numbers");
        C(ItemCardMath.Plain(beer) == "Beer: a toast's drink, recreation, rarely habit-forming.", "beer in plain words: " + ItemCardMath.Plain(beer));
        var wine = Beer();
        wine.Label = "wine";
        wine.Value = 22f;
        wine.Gift = true;
        C(Has(wine, "gift") && ItemCardMath.Tags(wine).First(t => t.Id == "gift").Why.Contains("22 silver"), "a fine wine is a gift between peoples");
        var tea = Beer();
        tea.Alcohol = false;
        tea.Soft = true;
        tea.Addictive = false;
        C(ItemCardMath.Tags(tea).First(t => t.Id == "toast-drink").Why.Contains("teetotaler") && ItemCardMath.Plain(tea).Contains("for those who don't drink")
          && ItemCardMath.Plain(tea).Contains("not habit-forming"), "tea is the toast's drink for those who don't drink");
        var yayo = Yayo();
        C(Has(yayo, "overdose") && Has(yayo, "addictive") && Has(yayo, "mood-lift"), "yayo can overdose, addicts and lifts the mood");
        C(ItemCardMath.Tags(yayo).First(t => t.Id == "overdose").Why == "each dose adds 18 to 35% toward an overdose, which kills at 100%; 1% of doses are a large one", "yayo's overdose by its own numbers");
        C(ItemCardMath.Tags(yayo).First(t => t.Id == "mood-lift").Why == "its effect gives +35 mood while it lasts (18 hours a dose)", "yayo's high by the game's thought");
        C(ItemCardMath.Plain(yayo) == "Yayo: a hard drug, lifts the mood (+35) for 18 hours, can overdose, addictive.", "yayo in plain words: " + ItemCardMath.Plain(yayo));
        C(ItemCardMath.Tags(yayo).Take(2).Select(t => t.Id).SequenceEqual(new[] { "overdose", "addictive" }), "a drug's gravest risks come first");
        var policy = Yayo();
        policy.HasColony = true;
        policy.PolicyOf = 5;
        policy.PolicyAllows = 1;
        C(ItemCardMath.Tags(policy).First(t => t.Id == "drug-policy").Why.EndsWith("1 of 5 allow it"), "the drug policies at home, counted");
        C(!Has(Yayo(), "drug-policy"), "no policies to count without a colony");

        // ---------- Medicine ----------
        var herbal = Medicine("herbal medicine", 0.6f, 0.7f);
        C(Has(herbal, "weak-surgery") && Has(herbal, "for-minor") && !Has(herbal, "potent") && !Has(herbal, "for-serious"), "herbal medicine: for minor wounds, risky for surgery");
        C(ItemCardMath.Plain(herbal) == "Herbal medicine: fine for a cut or a cold, risky for surgery.", "herbal medicine in plain words: " + ItemCardMath.Plain(herbal));
        var industrial = Medicine("medicine", 1f, 1f);
        C(Has(industrial, "potent") && Has(industrial, "for-serious") && !Has(industrial, "weak-surgery"), "industrial medicine: potent, kept for the serious");
        C(ItemCardMath.Plain(industrial) == "Medicine: right for serious wounds and illness, safe for surgery.", "industrial medicine in plain words");
        var glitter = Medicine("glitterworld medicine", 1.6f, 1.3f);
        C(ItemCardMath.Plain(glitter) == "Glitterworld medicine: the best for surgery and the gravest wounds, wasted on a scratch.", "glitterworld medicine in plain words");
        var off = Medicine("medicine", 1f, 1f);
        off.MedicineByNeed = false;
        C(!Has(off, "for-serious") && !Has(off, "for-minor"), "with medicine by what is treated off, no tag says where Rimfolk gives it");
        var shortMed = Medicine("medicine", 1f, 1f);
        shortMed.HasColony = true;
        shortMed.People = 6;
        shortMed.MedicineHeld = 5;
        C(Has(shortMed, "needed") && ItemCardMath.Tags(shortMed).First(t => t.Id == "needed").Why.Contains("under the 18"), "the colony short of medicine: needed now");
        C(ItemCardMath.Plain(shortMed).Contains("the colony is short of medicine"), "the line says the colony is short");
        shortMed.MedicineHeld = 40;
        C(Has(shortMed, "spare") && !Has(shortMed, "needed"), "more than the reserve: spare to sell");

        // ---------- The colony ----------
        var hungry = SimpleMeal();
        hungry.HasColony = true;
        hungry.FoodDays = 7f;
        C(Has(hungry, "needed") && !Has(hungry, "spare"), "a colony with 7 days of food needs food");
        hungry.FoodDays = 25f;
        C(Has(hungry, "spare") && !Has(hungry, "needed"), "a colony with 25 days of food has spare");
        var minds = SimpleMeal();
        minds.HasColony = true;
        minds.Grown = 5;
        minds.ForbiddenFor = 2;
        minds.ForbiddenWho = "Bo (no meat), Cy (no meat)";
        var tag = ItemCardMath.Tags(minds).First(t => t.Id == "forbidden");
        C(tag.Label == "Forbidden to some" && tag.Why == "2 of the colony's 5 grown people won't have it: Bo (no meat), Cy (no meat)", "who won't have it, by name and why");
        minds.ForbiddenFor = 3;
        C(ItemCardMath.Tags(minds).First(t => t.Id == "forbidden").Label == "Forbidden to most", "most of the colony won't have it");
        var warm = FineMeal();
        warm.HasColony = true;
        warm.ColdStore = false;
        C(Has(warm, "freezer"), "a meal that spoils fast in a colony with nothing cold needs a freezer");
        warm.ColdStore = true;
        C(!Has(warm, "freezer"), "a colony with a freezer needs none");

        // ---------- The vocabulary ----------
        C(ItemCardMath.Vocabulary.Select(t => t.Id).Distinct().Count() == ItemCardMath.Vocabulary.Length, "every tag of the vocabulary is its own");
        foreach (TagCategory cat in Enum.GetValues(typeof(TagCategory)))
            C(ItemCardMath.Vocabulary.Any(t => t.Category == cat), $"the vocabulary has tags of the category {cat}");
        foreach (var id in new[] { "nutritious", "keeps-long", "mood-lift" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Strength, $"{id} is a strength");
        foreach (var id in new[] { "spoils-fast", "raw" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Weakness, $"{id} is a weakness");
        foreach (var id in new[] { "food-poisoning", "addictive", "overdose", "forbidden" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Risk, $"{id} is a risk");
        foreach (var id in new[] { "feast-dish", "toast-drink", "gift" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Special, $"{id} is special");
        foreach (var id in new[] { "provisions", "trade-good" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Role, $"{id} is a role");
        foreach (var id in new[] { "cooking", "drug-policy" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Needs, $"{id} is a need");
        var everything = new List<ItemFacts> { FineMeal(), SimpleMeal(), SurvivalMeal(), Pemmican(), Potatoes(), Meat(), stack, cooked, kibble, hay, human, insect, beer, wine, tea, yayo, policy, herbal, industrial, glitter, shortMed, hungry, minds, warm };
        foreach (var f in everything)
        {
            var tags = ItemCardMath.Tags(f);
            C(tags.Count <= ItemCardMath.MaxTags, $"{f.Label}: at most {ItemCardMath.MaxTags} tags");
            C(tags.All(t => ItemCardMath.Def(t.Id) != null && !string.IsNullOrEmpty(t.Why)), $"{f.Label}: every tag is of the vocabulary and says why");
            C(tags.Select(t => t.Importance).SequenceEqual(tags.Select(t => t.Importance).OrderByDescending(i => i)), $"{f.Label}: the tags in order of importance");
            string plain = ItemCardMath.Plain(f);
            C(plain.EndsWith(".") && !plain.Contains(". ") && ItemCardMath.CleanWords(plain), $"{f.Label}: the line is one sentence with no em dash or exclamation mark");
            C(tags.All(t => ItemCardMath.CleanWords(t.Why) && ItemCardMath.CleanWords(t.Label)), $"{f.Label}: the tags' words are clean");
        }
        C(ItemCardMath.Vocabulary.All(t => ItemCardMath.CleanWords(t.Rule) && ItemCardMath.CleanWords(t.Label)), "the vocabulary's rules are clean words");
        // Many tags at once: the cut keeps the first strength and weakness.
        var crowded = Yayo();
        crowded.HasColony = true;
        crowded.PolicyOf = 4;
        crowded.Grown = 4;
        crowded.ForbiddenFor = 1;
        crowded.ForbiddenWho = "Bo";
        crowded.Kind = ItemKind.Drink;
        crowded.ToastDrink = true;
        crowded.Gift = true;
        crowded.Tradeable = true;
        crowded.Value = 30f;
        crowded.OfPerson = true;
        crowded.HighMood = 0f;
        crowded.PoisonFixed = 0.02f;
        var cut = ItemCardMath.Tags(crowded);
        C(ItemCardMath.AllTags(crowded).Count > ItemCardMath.MaxTags && cut.Count == ItemCardMath.MaxTags && cut.Any(t => t.Id == "recreation") && !cut.Any(t => t.Id == "gift"),
            "a crowded thing keeps its one strength among its eight tags, the least of a doubled category giving way");

        // ---------- The seams ----------
        string math = Read("Source/ColonyCouncil/Items/ItemCardMath.cs");
        string live = Read("Source/ColonyCouncil/Items/ItemCardLive.cs");
        string window = Read("Source/ColonyCouncil/Items/ThingCardWindow.cs");
        string first = Read("Source/ColonyCouncil/Items/ItemCardFirst.cs");
        string hook = Read("Source/ColonyCouncil/Weapons/WeaponCardFirst.cs");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_ItemCard.xml");
        C(!math.Contains("using Verse") && !math.Contains("using RimWorld") && !math.Contains("using UnityEngine") && Read("Tools/Tests/Tests.csproj").Contains("Items/ItemCardMath.cs"),
            "the rules are pure and tested here");
        C(hook.Contains("if (ItemCardFirst.Swapped(card)) return false;") && hook.Contains("return !WeaponCardFirst.Swapped(card) && !ApparelCardFirst.Swapped(card);")
          && hook.Contains("if (broken || !(window is Dialog_InfoCard card)) return true;"), "the one hook on the window stack opens the item card first too, every other window untouched");
        C(live.Contains("d.IsWeapon || d.IsApparel"), "the item card never takes a weapon or apparel from the other cards");
        C(first.Contains("WeaponCardFirst.insideGameCard") && first.Contains("letThrough") && window.Contains("ItemCardFirst.OpenGameCard(t, d, s)"),
            "links inside the game's card stay in it; \"All the game's stats\" is let through");
        C(first.Contains("ThingCardWindow.On && (ColonyCouncilMod.Settings?.itemCardFirst ?? true)") && first.Contains("if (On) card.Close(false);"),
            "only while the setting is on; going back from the game's card closes it");
        C(first.Contains("[HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]") && first.Contains("class Patch_ItemCardButton"),
            "the game's card keeps a button back to Rimfolk's");
        C(settings.Contains("public bool itemCardFirst = true;") && settings.Contains("Scribe_Values.Look(ref itemCardFirst, \"itemCardFirst\", true);"), "itemCardFirst: a setting, on at first, saved");
        C(catalog.Contains("Toggle(\"itemCardFirst\", \"CC_Settings_ItemCardFirst\", \"CC_SetNote_ItemCardFirst\", \"deepHooks\");") && catalog.Contains("Section(\"CC_SetSec_Items\");")
          && catalog.IndexOf("CC_SetSec_Items", StringComparison.Ordinal) > catalog.IndexOf("CC_SetSec_Apparel", StringComparison.Ordinal)
          && catalog.IndexOf("CC_SetSec_Items", StringComparison.Ordinal) < catalog.IndexOf("CC_SetSec_ColonistBar", StringComparison.Ordinal),
            "the Items group under How you're told, after Apparel");
        C(keys.Contains("<CC_Settings_ItemCardFirst>Open Rimfolk's item card first</CC_Settings_ItemCardFirst>") && keys.Contains("<CC_Settings_ItemCardFirst_Tip>")
          && keys.Contains("<CC_SetNote_ItemCardFirst>") && keys.Contains("<CC_SetSec_Items>"), "the setting's label, tip and note");
        C(window.Contains(": ItemCardWindow") && window.Contains("base(\"ItemCard\")") && FramedWindowsMath.TryKey("ItemCard", out var k) && k.leastW >= 420f && k.leastH >= 420f,
            "Rimfolk's item card is a framed window on the item card's base, with its frame key");
        C(window.Contains("NoBadge = true") && Read("Source/ColonyCouncil/UI/ItemCardWindow.cs").Contains("if (!h.NoBadge)") && Read("Source/ColonyCouncil/UI/ItemCardWindow.cs").Contains("UiKit.ItemPortrait(pic, thing, def, stuff, true)"),
            "the head with the thing's own picture, without a rating's badge");
        // Speed: built when opened, refreshed rarely, nothing between.
        C(live.Contains("LiveEvery = 1f, ColonyEvery = 10f") && window.Contains("ItemCardLive.Build(thing, def, stuff, Find.CurrentMap)") && window.Contains("ItemCardLive.Refresh(model)"),
            "the model is built when the card opens and refreshed at most once a second, the colony's every ten seconds");
        int bodyAt = window.IndexOf("protected override float Body", StringComparison.Ordinal);
        string body = bodyAt >= 0 ? window.Substring(bodyAt) : "";
        C(bodyAt > 0 && !body.Contains("ItemCardLive.") && !body.Contains("DefDatabase") && !body.Contains("listerThings") && !body.Contains("mapPawns"),
            "the card draws only from its model: nothing is read from the game while drawing");
        // Every key the code names exists, and every reason a colonist may not eat it.
        var codeKeys = new HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Items"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"(CC_[A-Za-z0-9_]+)\""))
                codeKeys.Add(m.Groups[1].Value);
        foreach (var key in codeKeys.Where(x => !x.EndsWith("_")))
            C(keys.Contains("<" + key + ">"), $"the key {key} is in Rimfolk_ItemCard.xml");
        var reasons = Regex.Matches(Read("Source/ColonyCouncil/Wishes/BeliefMath.cs").Split("public static string WhyNotEat(")[1].Split("public static bool FitToEat")[0], "return \"([A-Za-z]+)\"")
            .Select(m => m.Groups[1].Value).Concat(new[] { "wont", "policy", "inedible", "drug" }).Distinct().ToList();
        C(reasons.Count >= 12, "the beliefs' reasons are read from BeliefMath");
        foreach (var why in reasons)
            C(keys.Contains("<CC_ItemCard_Why_" + why + ">") && keys.Contains("<CC_ItemCard_WhyShort_" + why + ">"), $"the reason {why} in words, long and short");
        C(!keys.Contains("—") && !Regex.IsMatch(keys, @"<CC_[A-Za-z_]+>[^<]*!"), "no em dashes or exclamation marks in the item card's keys");
        return n;
    }
}
