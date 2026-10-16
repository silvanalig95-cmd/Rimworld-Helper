using ColonyCouncil;

/// <summary>
/// Beliefs and diets (BeliefMath): which precepts rule out what, lovin outside marriage and a shared bed, Alpha Memes'
/// fungus-and-insect rule and paste, and the choice of what a colonist may be served, including the colony from the
/// report: two Alpha Memes diets together, fungus preferred, insect meat despised, paste disgusting, lovin for spouses.
/// </summary>
public static class BeliefTests
{
    private static KeyValuePair<string, string> P(string name, string issue = null) => new KeyValuePair<string, string>(name, issue);

    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "beliefs: " + what);
        }

        // ---------- Every belief fits in the mask, and every known precept means something ----------
        var all = Enum.GetValues(typeof(Belief)).Cast<Belief>().ToList();
        Check(all.All(b => (int)b >= 0 && (int)b < 64), "every belief fits in 64 bits");
        Check(all.Select(BeliefMath.Bit).Distinct().Count() == all.Count, "every belief has its own bit");
        foreach (var name in BeliefMath.KnownPrecepts)
        {
            Check(BeliefMath.Of(name).Count > 0, name + " rules something out");
            Check(!name.Any(char.IsWhiteSpace), name + " has no spaces in its name");
        }
        Check(BeliefMath.Of("NoSuchPrecept").Count == 0 && BeliefMath.Of(null).Count == 0, "an unknown precept rules nothing out");
        Check(BeliefMath.FromPrecepts(null) == 0 && BeliefMath.FromPrecepts(new List<KeyValuePair<string, string>>()) == 0, "no faith, no beliefs");

        // ---------- Lovin: anything but free lovin keeps it, and a shared bed, to the married ----------
        foreach (var name in new[] { "Lovin_SpouseOnly_Mild", "Lovin_SpouseOnly_Moderate", "Lovin_SpouseOnly_Strict" })
        {
            ulong m = BeliefMath.FromPrecepts(new[] { P(name, "Lovin") });
            Check(BeliefMath.Has(m, Belief.LovinInMarriage), name + " keeps lovin to the married");
            Check(!BeliefMath.Has(m, Belief.NoLovin), name + " doesn't forbid lovin altogether");
            Check(BeliefMath.PreceptsFor(Belief.LovinInMarriage).Contains(name), name + " is listed for lovin in marriage");
        }
        foreach (var name in new[] { "Lovin_Free", "Lovin_FreeApproved" })
            Check(BeliefMath.FromPrecepts(new[] { P(name, "Lovin") }) == 0, name + " rules nothing out");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("SomeMod_Lovin_Chaste", "Lovin") }), Belief.LovinInMarriage), "a mod's own lovin precept that isn't free counts too");
        Check(BeliefMath.FromPrecepts(new[] { P("AM_LovinFrequency_Increased", "AM_LovinFrequency") }) == 0, "how often isn't whom: Alpha Memes' lovin frequency rules nothing out");
        ulong prohibited = BeliefMath.FromPrecepts(new[] { P("Lovin_Prohibited", "Lovin") });
        Check(BeliefMath.Has(prohibited, Belief.NoLovin) && BeliefMath.Has(prohibited, Belief.LovinInMarriage), "no lovin at all rules out lovin outside marriage too");

        ulong mild = BeliefMath.FromPrecepts(new[] { P("Lovin_SpouseOnly_Mild", "Lovin") });
        Check(BeliefMath.SharingBedClashes(false, mild, 0), "lovers not yet married don't share a bed under a spouse-only faith (the report)");
        Check(BeliefMath.SharingBedClashes(false, 0, mild), "the partner's faith counts as much as their own");
        Check(!BeliefMath.SharingBedClashes(true, mild, mild), "the married share a bed");
        Check(!BeliefMath.SharingBedClashes(false, 0, 0), "free lovin: lovers share a bed");
        Check(BeliefMath.SharingBedClashes(true, prohibited, 0), "a faith that forbids lovin keeps even the married apart");

        // ---------- Drink and drugs ----------
        ulong noDrugs = BeliefMath.FromPrecepts(new[] { P("DrugUse_Prohibited", "DrugUse") });
        Check(BeliefMath.Has(noDrugs, Belief.NoDrugs) && BeliefMath.Has(noDrugs, Belief.NoSocialDrugs) && BeliefMath.Has(noDrugs, Belief.NoAlcohol)
              && BeliefMath.Has(noDrugs, Belief.NoHardDrugs), "drugs prohibited rules out beer, smokeleaf and the hard drugs");
        ulong medical = BeliefMath.FromPrecepts(new[] { P("DrugUse_MedicalOnly", "DrugUse") });
        Check(BeliefMath.Has(medical, Belief.NoAlcohol) && !BeliefMath.Has(medical, Belief.NoDrugs), "medical use only: no beer, but medicine is fine");
        ulong social = BeliefMath.FromPrecepts(new[] { P("DrugUse_MedicalOrSocial", "DrugUse") });
        Check(BeliefMath.Has(social, Belief.NoHardDrugs) && !BeliefMath.Has(social, Belief.NoAlcohol), "medical or social: beer is fine, hard drugs aren't");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("VME_Alcohol_MildAbstinence", "VME_Alcohol") }), Belief.NoAlcohol), "mild abstinence: no beer at a toast");

        // ---------- Diets ----------
        ulong vegan = BeliefMath.FromPrecepts(new[] { P("VME_MeatEating_Abhorrent_Strict", "MeatEating") });
        Check(BeliefMath.Has(vegan, Belief.NoMeat) && BeliefMath.Has(vegan, Belief.NoAnimalProducts), "strict vegans eat neither meat nor eggs, milk or honey");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("MeatEating_Horrible") }), Belief.NoMeat), "vegetarian precepts rule out meat");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("MeatEating_NonMeat_Abhorrent") }), Belief.MeatOnly), "carnivore precepts rule out plants");
        Check(!BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P(BeliefMath.FungusRequired) }), Belief.FungusOrInsectOnly), "Alpha Memes' fungus required alone doesn't make anyone sick");
        Check(!BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P(BeliefMath.InsectMeatRequired) }), Belief.FungusOrInsectOnly), "nor does insect meat required alone");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P(BeliefMath.InsectMeatRequired), P(BeliefMath.FungusRequired) }), Belief.FungusOrInsectOnly),
            "both together: only fungus or insect");
        Check(BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("Biosculpting_Despised", "Biosculpting") }), Belief.NoBiosculpter)
              && BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("SomeMod_Biosculpting_Despised", "Biosculpting") }), Belief.NoBiosculpter)
              && !BeliefMath.Has(BeliefMath.FromPrecepts(new[] { P("Biosculpting_Accelerated", "Biosculpting") }), Belief.NoBiosculpter),
            "biosculpting despised (by any name in its issue) rules out a pod; accelerated doesn't");
        ulong violence = BeliefMath.FromPrecepts(new[] { P("VME_Violence_Abhorrent") });
        Check(BeliefMath.Has(violence, Belief.NoHunting) && BeliefMath.Has(violence, Belief.NoExecution), "violence abhorrent: no hunts, no executions");
        Check((BeliefMath.Diet & BeliefMath.Bit(Belief.NoMeat)) != 0 && (BeliefMath.Diet & BeliefMath.Bit(Belief.FungusOrInsectOnly)) != 0
              && (BeliefMath.Diet & BeliefMath.Bit(Belief.NoLovin)) == 0, "the diet beliefs are the food ones");

        // ---------- The colony from the report: three faiths ----------
        // Hers: Alpha Memes' two diets together, paste disgusting, lovin for spouses, slab beds and sleep accelerators.
        ulong hers = BeliefMath.FromPrecepts(new[]
        {
            P(BeliefMath.FungusRequired, "FungusEating"), P(BeliefMath.InsectMeatRequired, "InsectMeat"), P("NutrientPasteEating_Disgusting", "NutrientPasteEating"),
            P("Lovin_SpouseOnly_Mild", "Lovin"), P("SlabBed_Preferred", "SlabBed"), P("SleepAccelerator_Preferred", "SleepAccelerator")
        });
        // Another: fungus despised, insect meat despised, lovin for spouses.
        ulong theirs = BeliefMath.FromPrecepts(new[]
        {
            P("FungusEating_Despised", "FungusEating"), P("InsectMeatEating_Despised_Classic", "InsectMeat"), P("Lovin_SpouseOnly_Moderate", "Lovin")
        });
        // A third: fungus preferred.
        ulong third = BeliefMath.FromPrecepts(new[] { P("FungusEating_Preferred", "FungusEating") });
        Check(BeliefMath.Has(hers, Belief.LovinInMarriage) && BeliefMath.Has(theirs, Belief.LovinInMarriage), "report: no shared bed before the wedding, in either faith");
        Check(BeliefMath.SharingBedClashes(false, hers, theirs) && !BeliefMath.SharingBedClashes(true, hers, theirs), "report: lovers of the two faiths wait for the wedding");
        Check(BeliefMath.Has(hers, Belief.SlabBeds), "report: slab beds preferred");
        Check(BeliefMath.Has(hers, Belief.FungusOrInsectOnly) && BeliefMath.Has(hers, Belief.NoPaste), "report: only fungus or insect for her, and no paste");
        Check(!BeliefMath.Has(hers, Belief.NoMeat) && !BeliefMath.Has(theirs, Belief.FungusOrInsectOnly), "report: meat isn't ruled out as such, and the rule is hers alone");
        Check(BeliefMath.Has(theirs, Belief.NoFungus) && BeliefMath.Has(theirs, Belief.NoInsectMeat), "report: the other faith despises fungus and insect meat");

        // ---------- What may be served ----------
        ulong am = BeliefMath.FromPrecepts(new[] { P(BeliefMath.FungusRequired), P(BeliefMath.InsectMeatRequired) });
        var simple = new FoodFacts { simpleMeal = true, plant = true };
        var simpleWithFungus = new FoodFacts { simpleMeal = true, plant = true, fungus = true };
        var insectMeal = new FoodFacts { meat = true, insect = true };
        var jelly = new FoodFacts { insectJelly = true, animalProduct = true };
        var beer = new FoodFacts { drug = true, alcohol = true };
        var water = new FoodFacts { water = true };
        var paste = new FoodFacts { paste = true, plant = true, meat = true };
        var fineMeat = new FoodFacts { meat = true, plant = true };
        var mealKind = new FoodFacts { simpleMeal = true, unknownIngredients = true };
        Check(BeliefMath.WhyNotEat(am, simple) == "fungusOrInsect", "Alpha Memes' rule: a simple meal of potatoes would make them sick (the report)");
        Check(BeliefMath.WhyNotEat(am, fineMeat) == "fungusOrInsect", "nor a fine meal of cow and corn");
        Check(BeliefMath.FitToEat(am, simpleWithFungus), "a meal with fungus in it is fine");
        Check(BeliefMath.FitToEat(am, insectMeal), "insect meat is fine");
        Check(BeliefMath.FitToEat(am, jelly), "insect jelly is fine");
        Check(BeliefMath.FitToEat(am, beer) && BeliefMath.FitToEat(am, water), "drugs and water never make them sick");
        Check(BeliefMath.FitToEat(am, mealKind), "a kind of meal, its ingredients unknown, isn't ruled out yet (a stock wish judges each meal)");
        Check(BeliefMath.WhyNotEat(BeliefMath.Bit(Belief.NoPaste), paste) == "paste", "no paste for one who finds it disgusting");
        Check(BeliefMath.FitToEat(0, paste), "paste for one who doesn't mind it");
        Check(BeliefMath.WhyNotEat(0, new FoodFacts { badMemory = true }) == "memory", "never what would leave a bad memory");
        ulong fine = BeliefMath.FromPrecepts(new[] { P(BeliefMath.MealsQualityDesired) });
        Check(BeliefMath.WhyNotEat(fine, simple) == "plain" && BeliefMath.WhyNotEat(fine, new FoodFacts { rawBad = true }) == "plain", "good food expected: no simple meal, no raw food");
        Check(BeliefMath.FitToEat(fine, fineMeat), "good food expected: a fine meal is fine");
        Check(BeliefMath.WhyNotEat(BeliefMath.Bit(Belief.NoAlcohol), beer) == "alcohol", "no beer for one who abstains");
        Check(BeliefMath.FitToEat(BeliefMath.Bit(Belief.NoAlcohol), new FoodFacts { drug = true }), "a cup of tea for one who abstains");
        ulong veg = BeliefMath.FromPrecepts(new[] { P("MeatEating_Abhorrent") });
        Check(BeliefMath.WhyNotEat(veg, fineMeat) == "meat" && BeliefMath.WhyNotEat(veg, insectMeal) == "meat", "no meat for a vegetarian, insect meat included");
        Check(BeliefMath.FitToEat(veg, simple) && BeliefMath.FitToEat(veg, jelly), "greens and jelly for a vegetarian");
        Check(BeliefMath.WhyNotEat(vegan, jelly) == "animalProduct", "no jelly for a strict vegan");
        ulong carnivore = BeliefMath.FromPrecepts(new[] { P("MeatEating_NonMeat_Disapproved") });
        Check(BeliefMath.WhyNotEat(carnivore, simple) == "plant" && BeliefMath.WhyNotEat(carnivore, fineMeat) == "plant", "a carnivore eats nothing with plants in it");
        Check(BeliefMath.FitToEat(carnivore, insectMeal), "meat for a carnivore");
        ulong fungusFan = BeliefMath.FromPrecepts(new[] { P("FungusEating_Preferred") });
        Check(BeliefMath.WhyNotEat(fungusFan, simple) == "notFungus" && BeliefMath.FitToEat(fungusFan, simpleWithFungus), "fungus preferred: fungus, not potatoes");
        ulong fungusHater = BeliefMath.FromPrecepts(new[] { P("FungusEating_Despised") });
        Check(BeliefMath.WhyNotEat(fungusHater, simpleWithFungus) == "fungus" && BeliefMath.FitToEat(fungusHater, simple), "fungus despised: potatoes, not fungus");
        // A feast for the colony in the report: each is served only what fits their own faith.
        var stores = new List<FoodFacts> { simple, fineMeat, simpleWithFungus, paste, insectMeal };
        var forHer = stores.Where(f => BeliefMath.FitToEat(hers, f)).ToList();
        Check(forHer.Count == 2 && forHer.All(f => f.fungus || f.insect), $"report: for her, only the fungus meal and the insect meat (got {forHer.Count})");
        var forThem = stores.Where(f => BeliefMath.FitToEat(theirs, f)).ToList();
        Check(forThem.Count == 3 && forThem.All(f => !f.fungus && !f.insect), $"report: for the other faith, nothing with fungus or insect in it (got {forThem.Count})");
        var forThird = stores.Where(f => BeliefMath.FitToEat(third, f)).ToList();
        Check(forThird.Count == 2 && forThird.Contains(simpleWithFungus), $"report: fungus preferred, the fungus meal or meat (got {forThird.Count})");
        // With nothing of her kind in the stores, she comes without eating.
        Check(!new List<FoodFacts> { simple, fineMeat, paste }.Any(f => BeliefMath.FitToEat(hers, f)), "nothing fit: she comes without eating");

        return cases;
    }
}
