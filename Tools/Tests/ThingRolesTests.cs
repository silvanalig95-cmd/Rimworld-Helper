using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Things by what they are for (Things/ThingRolesMath.cs, ThingRoles.cs; DESIGN.md "Things by what they are for"): the
/// rules of each role on clear cases, the ages and the giver's ways, the pick by weight and the count by worth; then the
/// same rules on the player's own catalogue (docs/player-mods/, items.tsv and weapons.tsv, their active rows), so that each
/// role has several things of the game and the player's mods in every age, the ones the colony would find wrong left out;
/// and the seams: every place that picked a base-game thing for one of these asks the roles now.
/// </summary>
public static class ThingRolesTests
{
    private static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    private static string[] Split(string s) => (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "things by role: " + what);
        }

        // ================= The rules on clear cases =================

        ThingFacts Drink(float value = 12f, int tech = 2) => new ThingFacts
        {
            defName = "beer", item = true, drug = true, drugKind = 2, alcohol = true, fluid = true, humanEdible = true, value = value, tech = tech,
            preferability = 1, nutrition = 0.08f, stack = 25
        };
        var beer = Drink();
        var tea = Drink(8f);
        tea.alcohol = false;
        tea.soft = true;
        var smoke = Drink(11f);
        smoke.fluid = false;
        smoke.alcohol = false;
        C(ThingRolesMath.Fits(ThingRole.Drink, beer) && !ThingRolesMath.Fits(ThingRole.SoftDrink, beer), "beer is a drink for a toast, not one for the dry");
        C(ThingRolesMath.Fits(ThingRole.SoftDrink, tea) && !ThingRolesMath.Fits(ThingRole.Drink, tea), "tea a teetotaler may have is the dry's drink");
        C(!ThingRolesMath.Fits(ThingRole.Drink, smoke) && !ThingRolesMath.Fits(ThingRole.SoftDrink, smoke), "a smoke is no drink");
        var hard = Drink();
        hard.drugKind = 3;
        C(!ThingRolesMath.Fits(ThingRole.Drink, hard), "a hard drug is never raised in a toast");
        C(!ThingRolesMath.Fits(ThingRole.Drink, Drink(600f)), "a treasure of a drink (ambrandy) is no round");

        ThingFacts Meal(int pref, float rot = 4f, int made = 0) => new ThingFacts
        {
            defName = "meal", item = true, meal = true, humanEdible = true, withMeals = true, preferability = pref, rotDays = rot, madeTech = made,
            nutrition = 0.9f, value = 20f, stack = 10
        };
        C(ThingRolesMath.Fits(ThingRole.Feast, Meal(ThingRolesMath.MealFine)) && ThingRolesMath.Fits(ThingRole.Feast, Meal(ThingRolesMath.MealLavish)),
            "fine and lavish meals are for a feast");
        C(!ThingRolesMath.Fits(ThingRole.Feast, Meal(ThingRolesMath.MealSimple)) && !ThingRolesMath.Fits(ThingRole.Feast, Meal(ThingRolesMath.MealFine, 4f, -1)),
            "a simple meal isn't, nor a fine one no recipe cooks (a ruined grill)");
        C(!ThingRolesMath.Fits(ThingRole.Feast, Meal(ThingRolesMath.MealFine, -1f)), "a feast is fresh: a meal that never rots is road food");
        C(ThingRolesMath.Fits(ThingRole.Provisions, Meal(ThingRolesMath.MealSimple, 70f)) && ThingRolesMath.Fits(ThingRole.Provisions, Meal(ThingRolesMath.MealSimple, -1f))
          && !ThingRolesMath.Fits(ThingRole.Provisions, Meal(ThingRolesMath.MealSimple, 4f)) && !ThingRolesMath.Fits(ThingRole.Provisions, Meal(ThingRolesMath.MealSimple - 1, 70f)),
            "provisions keep twenty days or more and are a simple meal or better (pemmican, a survival meal; never paste)");
        C(ThingRolesMath.TravelRank(Meal(ThingRolesMath.MealSimple, -1f)) == 2 && ThingRolesMath.TravelRank(Meal(ThingRolesMath.MealSimple, 70f)) == 1
          && ThingRolesMath.TravelRank(Meal(ThingRolesMath.MealFine, 4f)) == 0, "for the road: what never rots, then what keeps, then the rest");
        var potato = new ThingFacts { item = true, vegetable = true, humanEdible = true, preferability = ThingRolesMath.RawBad, madeTech = 0, nutrition = 0.05f, value = 1.1f };
        var juice = new ThingFacts { item = true, vegetable = true, humanEdible = true, withMeals = true, preferability = ThingRolesMath.RawTasty, madeTech = 4, nutrition = 0.05f, value = 1.2f };
        var wildBerry = new ThingFacts { item = true, vegetable = true, humanEdible = true, preferability = ThingRolesMath.RawTasty, madeTech = -1, nutrition = 0.05f, value = 1.2f };
        C(ThingRolesMath.Fits(ThingRole.Produce, potato) && !ThingRolesMath.Fits(ThingRole.Produce, juice) && !ThingRolesMath.Fits(ThingRole.Produce, wildBerry),
            "produce is a sown crop, raw (not a juice kept with the meals, nor something only found wild)");
        var person = Meal(ThingRolesMath.MealFine);
        person.humanlike = true;
        C(!ThingRolesMath.Fits(ThingRole.Feast, person) && !ThingRolesMath.Ordinary(person), "nothing of a person is ever picked");
        var quest = Drink();
        quest.untradeable = true;
        var used = Drink();
        used.special = true;
        C(!ThingRolesMath.Fits(ThingRole.Drink, quest) && !ThingRolesMath.Fits(ThingRole.Drink, used), "a quest's or a mod's special thing never is");
        C(ThingRolesMath.SpecialComp("CompProperties_UseEffectArtifact") && ThingRolesMath.SpecialComp("CompBiocodable") && ThingRolesMath.SpecialComp("CompProperties_Hatcher")
          && !ThingRolesMath.SpecialComp("CompProperties_Drug") && !ThingRolesMath.SpecialComp("CompProperties_Rottable") && !ThingRolesMath.SpecialComp(null),
            "a comp that makes a thing one to use is special, a drug's or rot's isn't");
        var game = new HashSet<string> { "Drugs", "Art", "ExoticMisc" };
        C(ThingRolesMath.SpecialTag("ExoticMisc", game) && ThingRolesMath.SpecialTag("RawArcane", game) && !ThingRolesMath.SpecialTag("Drugs", game) && !ThingRolesMath.SpecialTag("", game),
            "a trade tag the game marks exotic, or one the game never uses (a mod's own), is special");

        ThingFacts Stuff(bool metal, bool stone, float beauty, float value = 5f) => new ThingFacts
            { item = true, metallic = metal, stony = stone, beauty = beauty, value = value, stack = 75 };
        var gold = Stuff(true, false, 4f, 10f);
        var jade = Stuff(false, true, 2.5f);
        var silver = Stuff(true, false, 2f, 1f);
        var steel = Stuff(true, false, 1f, 1.9f);
        var wax = Stuff(true, false, 2f);
        wax.otherStuff = true;
        C(ThingRolesMath.Fits(ThingRole.Precious, gold) && ThingRolesMath.Fits(ThingRole.Precious, jade) && ThingRolesMath.Fits(ThingRole.Precious, silver),
            "silver, gold and jade are precious by the beauty they give");
        C(!ThingRolesMath.Fits(ThingRole.Precious, steel) && !ThingRolesMath.Fits(ThingRole.Precious, wax), "steel isn't, nor a waxy stuff of a mod");
        C(ThingRolesMath.Weight(ThingRole.Gift, jade, WorldAge.Tribal) > ThingRolesMath.Weight(ThingRole.Gift, gold, WorldAge.Tribal)
          && ThingRolesMath.Weight(ThingRole.Gift, gold, WorldAge.Medieval) > ThingRolesMath.Weight(ThingRole.Gift, jade, WorldAge.Medieval)
          && ThingRolesMath.Weight(ThingRole.Gift, gold, WorldAge.Spacer, true) > ThingRolesMath.Weight(ThingRole.Gift, silver, WorldAge.Spacer, true)
          && ThingRolesMath.Weight(ThingRole.Gift, silver, WorldAge.Industrial) > ThingRolesMath.Weight(ThingRole.Gift, jade, WorldAge.Industrial),
            "a gift by the giver's ways: a tribe's jade, the middle ages' and the Empire's gold, the later ages' silver");
        var wine = Drink(20f);
        C(ThingRolesMath.Fits(ThingRole.Gift, wine) && !ThingRolesMath.Fits(ThingRole.Gift, Drink(12f))
          && ThingRolesMath.Weight(ThingRole.Gift, wine, WorldAge.Medieval) > ThingRolesMath.Weight(ThingRole.Gift, wine, WorldAge.Industrial),
            "a fine drink is a gift too (more so in the middle ages), a plain beer isn't");

        ThingFacts Art(float work, bool wood = true) => new ThingFacts
            { building = true, art = true, minified = true, madeTech = 0, work = work, ofWood = wood, ofStone = true, ofMetal = true };
        var small = Art(18000f);
        var grand = Art(105000f);
        var stoneOnly = Art(18000f, false);
        var terror = Art(18000f);
        terror.terror = true;
        var mummy = Art(5000f);
        mummy.holdsDead = true;
        var found = Art(18000f);
        found.madeTech = -1;
        C(ThingRolesMath.Fits(ThingRole.SmallArt, small) && ThingRolesMath.Fits(ThingRole.Carving, small) && !ThingRolesMath.Fits(ThingRole.SmallArt, grand),
            "small art is a day's work at a bench, not a grand sculpture");
        C(ThingRolesMath.Fits(ThingRole.SmallArt, stoneOnly) && !ThingRolesMath.Fits(ThingRole.Carving, stoneOnly), "a carving may be of wood");
        C(!ThingRolesMath.Fits(ThingRole.SmallArt, terror) && !ThingRolesMath.Fits(ThingRole.SmallArt, mummy) && !ThingRolesMath.Fits(ThingRole.SmallArt, found),
            "never a terror sculpture, nothing that holds the dead, nothing no bench makes");

        var spear = new ThingFacts { weapon = true, melee = true, edged = true, ofMetal = true, tagged = true, madeTech = 2, tech = 2, value = 40f };
        var persona = new ThingFacts { weapon = true, melee = true, edged = true, ofMetal = true, tagged = true, bonded = true, madeTech = 5, tech = 5, value = 900f };
        var mace = new ThingFacts { weapon = true, melee = true, ofMetal = true, ofWood = true, tagged = true, madeTech = 3, tech = 3, value = 40f };
        var elven = new ThingFacts { weapon = true, melee = true, edged = true, ofMetal = true, madeTech = 3, tech = 3, value = 200f };
        C(ThingRolesMath.Fits(ThingRole.Blade, spear) && !ThingRolesMath.Fits(ThingRole.Blade, persona) && !ThingRolesMath.Fits(ThingRole.Blade, mace)
          && !ThingRolesMath.Fits(ThingRole.Blade, elven), "a blade is forged of metal and cuts or stabs; never one bonded to a person, a mace, or one only a mod's people carry");
        C(ThingRolesMath.Alcoholic(true, null, false, false) && ThingRolesMath.Alcoholic(false, null, true, false) && !ThingRolesMath.Alcoholic(false, "VBE_Caffeine", true, true)
          && !ThingRolesMath.Alcoholic(false, null, true, true) && !ThingRolesMath.Alcoholic(false, null, false, false), "alcohol: its chemical, or liquor with no other in it (a coffee isn't)");

        var medHerbal = new ThingFacts { item = true, potency = 0.6f, tech = 2, madeTech = 0, value = 10f };
        var medIndustrial = new ThingFacts { item = true, potency = 1f, tech = 4, madeTech = 4, value = 18f };
        var medGlitter = new ThingFacts { item = true, potency = 1.6f, tech = 6, value = 50f };
        C(ThingRolesMath.Fits(ThingRole.Medicine, medHerbal, WorldAge.Tribal) && !ThingRolesMath.Fits(ThingRole.Medicine, medIndustrial, WorldAge.Medieval)
          && ThingRolesMath.Fits(ThingRole.Medicine, medIndustrial, WorldAge.Industrial) && !ThingRolesMath.Fits(ThingRole.Medicine, medGlitter, WorldAge.Industrial)
          && ThingRolesMath.Fits(ThingRole.Medicine, medGlitter, WorldAge.Spacer), "medicine by age: herbs for a tribe, medicine with industry, glitterworld's in the spacer age");
        C(ThingRolesMath.Weight(ThingRole.Medicine, medHerbal, WorldAge.Tribal) > ThingRolesMath.Weight(ThingRole.Medicine, medHerbal, WorldAge.Industrial)
          && ThingRolesMath.Weight(ThingRole.Medicine, medIndustrial, WorldAge.Industrial) > ThingRolesMath.Weight(ThingRole.Medicine, medHerbal, WorldAge.Industrial),
            "each age reaches first for its own medicine");

        // Ages.
        C(ThingRolesMath.AgeOf(beer) == (int)WorldAge.Tribal && ThingRolesMath.AgeOf(medIndustrial) == (int)WorldAge.Industrial && ThingRolesMath.AgeOf(medGlitter) == (int)WorldAge.Spacer
          && ThingRolesMath.AgeOf(new ThingFacts()) == -1 && ThingRolesMath.AgeOf(new ThingFacts { madeTech = 3 }) == (int)WorldAge.Medieval,
            "a thing's age: its own tech level or its making's research, whichever is later; unknown when neither is");
        var unknown = Meal(ThingRolesMath.MealSimple, -1f, -1);
        C(!ThingRolesMath.InAge(ThingRole.Provisions, unknown, WorldAge.Medieval) && ThingRolesMath.InAge(ThingRole.Provisions, unknown, WorldAge.Industrial)
          && ThingRolesMath.InAge(ThingRole.Drink, new ThingFacts(), WorldAge.Tribal), "packaged food of no known age is the industrial age's; a drink of no known age anyone's");
        C(ThingRolesMath.Weight(ThingRole.Drink, Drink(12f, 4), WorldAge.Industrial) > ThingRolesMath.Weight(ThingRole.Drink, beer, WorldAge.Industrial),
            "the age's own things twice as likely as older ones");

        // The pick and the count.
        C(ThingRolesMath.PickIndex(new List<float> { 1f, 3f }, 0f) == 0 && ThingRolesMath.PickIndex(new List<float> { 1f, 3f }, 0.3f) == 1
          && ThingRolesMath.PickIndex(new List<float> { 1f, 3f }, 0.9999f) == 1 && ThingRolesMath.PickIndex(new List<float> { 0f, 2f }, 0f) == 1,
            "a pick by weight, the weights laid end to end");
        C(ThingRolesMath.PickIndex(new List<float>(), 0.5f) == -1 && ThingRolesMath.PickIndex(new List<float> { 0f }, 0.5f) == -1 && ThingRolesMath.PickIndex(null, 0.5f) == -1
          && ThingRolesMath.PickIndex(new List<float> { 1f }, float.NaN) == -1 && ThingRolesMath.PickIndex(new List<float> { float.NaN, 1f }, 0.1f) == 1, "nothing to pick picks nothing");
        C(ThingRolesMath.CountFor(200f, 10f) == 20 && ThingRolesMath.CountFor(200f, 1f) == 200 && ThingRolesMath.CountFor(3f, 10f, 2) == 2 && ThingRolesMath.CountFor(100f, 0f, 3) == 3,
            "the count by worth, so the game's balance holds whatever is picked");
        C(ThingRolesMath.TryParse("@Carving", out var tr) && tr == ThingRole.Carving && ThingRolesMath.TryParse("@drink", out tr) && tr == ThingRole.Drink
          && !ThingRolesMath.TryParse("SculptureSmall", out _) && !ThingRolesMath.TryParse("@Nothing", out _) && !ThingRolesMath.TryParse(null, out _),
            "a role named in a def's text: @ and its name");
        C(ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, "MealFine") == ThingRolesMath.MealFine && ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, "MealSimple") == ThingRolesMath.MealSimple
          && ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, "RawTasty") == ThingRolesMath.RawTasty && ThingRolesMath.IndexOf(ThingRolesMath.Techs, "Industrial") == 4
          && ThingRolesMath.IndexOf(ThingRolesMath.Techs, "nonsense") == 0, "the game's orders read by name (1.6's preferabilities, with the terrible meal)");

        // ================= The player's own catalogue =================

        string cat = Path.Combine(root, "docs", "player-mods");
        List<Dictionary<string, string>> Table(string file)
        {
            var lines = File.ReadAllLines(Path.Combine(cat, file));
            var head = lines[0].Split('\t');
            return lines.Skip(1).Where(l => l.Length > 0).Select(l =>
            {
                var c = l.Split('\t');
                var row = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
                return row;
            }).ToList();
        }
        var itemsAll = Table("items.tsv");
        var weaponsAll = Table("weapons.tsv");
        C(itemsAll.Count > 0 && itemsAll[0].ContainsKey("madeTech") && itemsAll[0].ContainsKey("preferability") && itemsAll[0].ContainsKey("beauty")
          && weaponsAll[0].ContainsKey("madeTech") && weaponsAll[0].ContainsKey("comps"), "the catalogue carries what the roles read (Tools/mod_pool.py's columns)");
        var humanRaces = Table("races.tsv").Where(r => r["kind"] == "humanlike").Select(r => r["defName"]).ToHashSet();
        var gameTags = itemsAll.Concat(weaponsAll).Where(r => r["mod"].StartsWith("ludeon.")).SelectMany(r => Split(r["tradeTags"])).ToHashSet();
        var gameWeaponTags = weaponsAll.Where(r => r["mod"].StartsWith("ludeon.")).SelectMany(r => Split(r["weaponTags"])).ToHashSet();
        bool Special(Dictionary<string, string> r) =>
            Split(r["comps"]).Any(ThingRolesMath.SpecialComp) || Split(r["tradeTags"]).Any(t => ThingRolesMath.SpecialTag(t, gameTags));
        int Made(string s) => string.IsNullOrEmpty(s) ? -1 : ThingRolesMath.IndexOf(ThingRolesMath.Techs, s);
        ThingFacts ItemFacts(Dictionary<string, string> r)
        {
            var food = Split(r["foodType"].Replace(' ', ','));
            var stuff = Split(r["stuffProps"]);
            var madeOf = Split(r["madeOf"]);
            var comps = Split(r["comps"]);
            string drug = r["drugCategory"];
            return new ThingFacts
            {
                defName = r["defName"],
                item = r["category"] == "Item",
                building = r["category"] == "Building",
                tech = ThingRolesMath.IndexOf(ThingRolesMath.Techs, r["techLevel"]),
                madeTech = Made(r["madeTech"]),
                value = F(r["marketValue"]),
                stack = r["stackLimit"] == "" ? 1 : (int)F(r["stackLimit"]),
                untradeable = r["tradeability"] == "None",
                special = Special(r),
                humanlike = humanRaces.Contains(r["sourceDef"]),
                drug = drug != "" && drug != "None",
                drugKind = drug == "Medical" ? 1 : drug == "Social" ? 2 : drug == "Hard" ? 3 : 0,
                alcohol = ThingRolesMath.Alcoholic(r["chemical"] == "Alcohol", r["chemical"] == "Alcohol" ? "" : r["chemical"], food.Contains("Liquor"), r["teetotaler"] == "true"),
                soft = r["teetotaler"] == "true",
                fluid = food.Contains("Fluid") || food.Contains("Liquor"),
                meal = food.Contains("Meal"),
                vegetable = food.Contains("VegetableOrFruit"),
                humanEdible = food.Any(ThingRolesMath.HumanFoodTypes.Contains),
                withMeals = Split(r["thingCategories"]).Contains("FoodMeals"),
                preferability = ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, r["preferability"]),
                nutrition = F(r["nutrition"]),
                rotDays = r["rotDays"] == "" ? -1f : F(r["rotDays"]),
                potency = F(r["potency"]),
                metallic = stuff.Contains("Metallic"),
                stony = stuff.Contains("Stony"),
                otherStuff = stuff.Any(s => s != "Metallic" && s != "Stony"),
                beauty = stuff.Length == 0 ? 0f : r["beauty"] == "" ? 1f : F(r["beauty"]),
                ofWood = madeOf.Contains("Woody"),
                ofStone = madeOf.Contains("Stony"),
                ofMetal = madeOf.Contains("Metallic"),
                art = comps.Contains("CompProperties_Art") && Split(r["thingCategories"]).Contains("BuildingsArt"),
                minified = r["minified"] != "",
                terror = r["terror"] != "",
                holdsDead = comps.Any(c => c.Contains("Corpse")),
                work = F(r["work"])
            };
        }
        ThingFacts WeaponFacts(Dictionary<string, string> r)
        {
            var comps = Split(r["comps"]);
            var stuff = Split(r["stuff"]);
            return new ThingFacts
            {
                defName = r["defName"],
                weapon = true,
                tech = ThingRolesMath.IndexOf(ThingRolesMath.Techs, r["techLevel"]),
                madeTech = Made(r["madeTech"]),
                value = F(r["marketValue"]),
                untradeable = r["tradeability"] == "None",
                special = Split(r["tradeTags"]).Any(t => ThingRolesMath.SpecialTag(t, gameTags)),
                melee = r["kind"] == "melee",
                edged = Split(r["capacities"]).Any(c => c == "Cut" || c == "Stab"),
                ofWood = stuff.Contains("Woody"),
                ofStone = stuff.Contains("Stony"),
                ofMetal = stuff.Contains("Metallic"),
                tagged = Split(r["weaponTags"]).Any(t => ThingRolesMath.GameWeaponTag(t, gameWeaponTags)),
                bonded = comps.Any(c => c.Contains("Bladelink") || c.Contains("Biocod"))
            };
        }
        var active = itemsAll.Where(r => r["active"] == "yes").Select(r => (mod: r["mod"], facts: ItemFacts(r)))
            .Concat(weaponsAll.Where(r => r["active"] == "yes").Select(r => (mod: r["mod"], facts: WeaponFacts(r)))).ToList();
        C(active.Count > 4000, $"the player's active items and weapons are read ({active.Count})");

        // Each role, each age: several things, the player's mods among them (Vanilla Brewing's spirits for a toast, Vanilla
        // Cooking's dishes for a feast, the mods' statues, blades and crops).
        var least = new Dictionary<ThingRole, int>
        {
            { ThingRole.Drink, 10 }, { ThingRole.SoftDrink, 2 }, { ThingRole.Feast, 10 }, { ThingRole.Provisions, 2 }, { ThingRole.Produce, 20 },
            { ThingRole.Medicine, 2 }, { ThingRole.Precious, 4 }, { ThingRole.SmallArt, 3 }, { ThingRole.Carving, 3 }, { ThingRole.Gift, 8 },
            { ThingRole.Blade, 2 }
        };
        var report = new List<string>();
        foreach (var role in least.Keys)
            foreach (var age in AgesMath.All)
            {
                var fit = active.Where(x => ThingRolesMath.Fits(role, x.facts, age)).ToList();
                int mods = fit.Count(x => !x.mod.StartsWith("ludeon."));
                report.Add($"{role} {age}: {fit.Count} ({mods} of mods): " + string.Join(", ", fit.Select(x => x.facts.defName)));
                C(fit.Count >= least[role], $"{role} for the {age} age: {fit.Count} things of the game and the player's mods (at least {least[role]})");
                C(mods >= 1, $"{role} for the {age} age: the player's mods among them ({mods})");
            }
        try
        {
            string outDir = Environment.GetEnvironmentVariable("RIMFOLK_ROLES_REPORT");
            if (!string.IsNullOrEmpty(outDir)) File.WriteAllLines(outDir, report);
        }
        catch
        {
            // Only a report for the developer.
        }
        bool In(ThingRole role, string defName, WorldAge age = WorldAge.Spacer) => active.Any(x => x.facts.defName == defName && ThingRolesMath.Fits(role, x.facts, age));
        bool Known(string defName) => active.Any(x => x.facts.defName == defName);
        C(In(ThingRole.Drink, "Beer", WorldAge.Tribal) && In(ThingRole.Feast, "MealFine", WorldAge.Tribal) && In(ThingRole.Provisions, "Pemmican", WorldAge.Tribal)
          && In(ThingRole.Medicine, "MedicineHerbal", WorldAge.Tribal) && In(ThingRole.Precious, "Gold", WorldAge.Tribal) && In(ThingRole.Carving, "SculptureSmall", WorldAge.Tribal)
          && In(ThingRole.Produce, "RawPotatoes", WorldAge.Tribal) && In(ThingRole.Provisions, "MealSurvivalPack", WorldAge.Industrial)
          && !In(ThingRole.Provisions, "MealSurvivalPack", WorldAge.Medieval), "the base game's own things fit their roles and ages (the fallbacks)");
        if (Known("VBE_Whiskey")) C(In(ThingRole.Drink, "VBE_Whiskey", WorldAge.Tribal), "Vanilla Brewing's whiskey is raised in any age's toast");
        if (Known("VFEM2_Wine")) C(In(ThingRole.Drink, "VFEM2_Wine", WorldAge.Medieval) && In(ThingRole.Gift, "VFEM2_Wine", WorldAge.Medieval), "Medieval 2's wine is a toast and a gift");
        if (Known("VBE_Tea")) C(In(ThingRole.SoftDrink, "VBE_Tea", WorldAge.Tribal) && !In(ThingRole.Drink, "VBE_Tea"), "Coffees and Teas' tea is the dry's toast");
        if (Known("VBE_Ambrandy")) C(!In(ThingRole.Drink, "VBE_Ambrandy"), "ambrandy is a treasure, not a round");
        if (Known("VCE_MealGourmet")) C(In(ThingRole.Feast, "VCE_MealGourmet", WorldAge.Tribal), "Vanilla Cooking's gourmet meal is a feast's dish");
        if (Known("VCE_RuinedFineGrill")) C(!In(ThingRole.Feast, "VCE_RuinedFineGrill"), "never a ruined grill");
        if (Known("VFE_StatueSmall")) C(In(ThingRole.Carving, "VFE_StatueSmall", WorldAge.Medieval) && !In(ThingRole.Carving, "VFE_StatueSmall", WorldAge.Tribal),
            "Vanilla Furniture Expanded - Art's small statue is a carving from the middle ages on (its statues' research)");
        if (Known("VFE_Pottery")) C(In(ThingRole.Carving, "VFE_Pottery", WorldAge.Tribal), "and its pottery a tribe's");
        if (Known("VFEE_RoyalSculptureSmall")) C(In(ThingRole.SmallArt, "VFEE_RoyalSculptureSmall", WorldAge.Medieval) && !In(ThingRole.SmallArt, "VFEE_RoyalSculptureSmall", WorldAge.Tribal),
            "the Empire mod's small sculpture from the middle ages on (its research)");
        foreach (var name in new[] { "SculptureTerror", "AM_MummyMale", "AP_FakeSculptureSmallA", "SculptureGrand" })
            if (Known(name)) C(!In(ThingRole.SmallArt, name), $"{name} is no small piece to give");
        if (Known("TM_Arcalleum")) C(!In(ThingRole.Precious, "TM_Arcalleum"), "A RimWorld of Magic's arcane metal is no purse of gold (its own trade tags)");
        if (Known("RB_BlackBeeswax")) C(!In(ThingRole.Precious, "RB_BlackBeeswax"), "Alpha Bees' black wax is no precious metal");
        if (Known("Obsidian")) C(In(ThingRole.Precious, "Obsidian", WorldAge.Tribal), "Odyssey's obsidian is precious stone");
        if (Known("VBE_Juice")) C(!In(ThingRole.Produce, "VBE_Juice"), "juice isn't a crop");
        foreach (var name in new[] { "Meat_Human", "MealNutrientPaste", "Chocolate", "Kibble", "Hay", "GoJuice", "Yayo", "SmokeleafJoint" })
            if (Known(name)) C(!new[] { ThingRole.Drink, ThingRole.Feast, ThingRole.Provisions, ThingRole.Produce, ThingRole.Gift }.Any(r => In(r, name)),
                $"{name} is never a toast, a feast, provisions, produce or a gift");
        C(active.Where(x => ThingRolesMath.Fits(ThingRole.Blade, x.facts, WorldAge.Tribal)).All(x => x.facts.tech <= 2 && x.facts.madeTech <= 2),
            "a tribe's smith forges only what a tribe knows");
        C(!active.Any(x => ThingRolesMath.Fits(ThingRole.Blade, x.facts) && x.facts.bonded), "no blade is bonded to anyone");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var seams = new (string file, string role, string gone)[]
        {
            ("Diplomacy/Matches.cs", "ThingRole.Gift", "GetNamedSilentFail(\"Jade\")"),
            ("Camps/CampLife.cs", "ThingRole.Precious", "GetNamedSilentFail(\"Jade\")"),
            ("Camps/CampRequests.cs", "ThingRole.Precious", "GetNamedSilentFail(\"Jade\")"),
            ("Diplomacy/Ransoms.cs", "ThingRole.Provisions", "GetNamedSilentFail(\"Pemmican\")"),
            ("Diplomacy/FavourCalls.cs", "ThingRole.Medicine", "GetNamedSilentFail(\"Pemmican\")"),
            ("Diplomacy/EnvoysAbroad.cs", "ThingRole.Gift", "? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineIndustrial"),
            ("World/NeighbourLetters.cs", "ThingRole.Provisions", "GetNamedSilentFail(\"Pemmican\")"),
            ("World/Workers_Remembered.cs", "ThingRole.Provisions", "GetNamedSilentFail(\"MealSurvivalPack\")"),
            ("Missions/Hosting.cs", "ThingRole.Provisions", "var def = ThingDefOf.Pemmican"),
            ("Happenings/Workers_Callers.cs", "\"@Produce\"", "RawPotatoes|RawRice"),
            ("Happenings/Workers_Storyteller.cs", "ThingRole.Carving", "GetNamedSilentFail(\"SculptureSmall\")"),
            ("Happenings/Workers_Visitors2.cs", "ThingRole.Carving", "GetNamedSilentFail(\"SculptureSmall\")"),
            ("Strangers/EncounterGifts.cs", "ThingRole.Carving", "GetNamedSilentFail(\"SculptureSmall\")"),
            ("Happenings/Workers_Storyteller_Land.cs", "ThingRole.Blade", "MeleeWeapon_Gladius"),
            ("Outsiders/OutsiderOutcomes.cs", "ThingRole.Drink", "Add(\"Beer\", 0)"),
            ("Journeys/JourneyPack.cs", "ThingRolesMath.TravelRank", "d == ThingDefOf.MealSurvivalPack ? 2"),
            ("Colonies/Foundings.cs", "ThingRole.Medicine", "ThingDefOf.MedicineUltratech }"),
            ("Missions/Workers_Missions.cs", "\"drinks\"", "Add(m, \"Beer\""),
        };
        foreach (var (file, role, gone) in seams)
        {
            string code = Code(file);
            C(code.Contains(role) && !code.Contains(gone), $"{file} picks by role ({role}), not by its old fixed name");
        }
        string made = Code("PlayOut/OnMapThings.cs");
        C(made.Contains("ThingRolesMath.TryParse") && made.Contains("ThingRoles."), "an answer's things may name a role (@Carving)");
        string roles = Code("Things/ThingRoles.cs");
        C(roles.Contains("ColonyMinds") && roles.Contains("Beliefs.AgainstThing") && roles.Contains("Fallback("), "the colony's beliefs and the base game's fallback are in the one picker");
        C(!System.Text.RegularExpressions.Regex.IsMatch(Code("Things/ThingRolesMath.cs") + roles, "\"(VBE|VCE|VFE|AC|RB|AM|TM|AA|AB)_"), "no mod's def is named in the picker");
        var tests = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj"));
        C(tests.Contains("Things/ThingRolesMath.cs"), "the pure rules are the mod's own, linked into the tests");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
