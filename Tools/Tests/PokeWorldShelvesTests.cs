using System.IO.Compression;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// PokéWorld's items on the shelves that carry everything else (the player, 20 November 2026: "there's a bunch of items
/// from the Poke World mod, for example, the Dawn Stone ... that I cannot put into large storage shelves"): each kind of
/// item placed beside the game's own category of the same kind, read from its fields and checked on PokéWorld's own items
/// in the player's catalogue (docs/player-mods, active rows); a storage filter takes them where it takes the whole of that
/// category; a fridge only what spoils and eggs; the shelves already built once a save; the setting and the seams.
/// </summary>
public static class PokeWorldShelvesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "PokéWorld's items on the shelves: " + what);
        }

        // ---------- Each kind of item to the game's own category ----------
        string K(ShelfItemFacts f) => PokeWorldShelvesMath.KindOf(f);
        C(K(new ShelfItemFacts()) == "ItemsMisc", "an item that is nothing else (a stone, a fossil, a ball, a TM, a potion) goes with the miscellaneous items");
        C(K(new ShelfItemFacts { apparel = true, beltLayer = true }) == "ApparelUtility", "a belt goes with the utility apparel, beside the shield belt");
        C(K(new ShelfItemFacts { apparel = true, onHead = true }) == "Headgear", "a hat goes with the headgear");
        C(K(new ShelfItemFacts { apparel = true }) == "ApparelMisc", "other apparel goes with the other apparel");
        C(K(new ShelfItemFacts { apparel = true, weapon = true, beltLayer = true }) == "ApparelUtility", "worn first: a belt with a verb is still apparel");
        C(K(new ShelfItemFacts { weapon = true, ranged = true }) == "WeaponsRanged" && K(new ShelfItemFacts { weapon = true }) == "WeaponsMelee", "a weapon with the ranged or melee weapons");
        C(K(new ShelfItemFacts { tool = true }) == "Weapons", "a tool held like a weapon goes with the weapons");
        C(K(new ShelfItemFacts { hatches = true, food = true, animalProduct = true }) == "EggsFertilized", "an egg that hatches goes with the fertilized eggs, though it can be eaten");
        C(K(new ShelfItemFacts { medicine = true }) == "Medicine" && K(new ShelfItemFacts { drug = true, food = true }) == "Drugs", "medicine and drugs with theirs");
        C(K(new ShelfItemFacts { food = true, meal = true, meat = true }) == "FoodMeals", "a meal with the meals");
        C(K(new ShelfItemFacts { food = true, meat = true }) == "MeatRaw" && K(new ShelfItemFacts { food = true, plantFood = true }) == "PlantFoodRaw"
          && K(new ShelfItemFacts { food = true, animalProduct = true }) == "AnimalProductRaw" && K(new ShelfItemFacts { food = true }) == "FoodRaw", "raw food by its kind");
        C(K(new ShelfItemFacts { leathery = true, otherStuff = true }) == "Leathers" && K(new ShelfItemFacts { fabric = true, otherStuff = true }) == "Textiles"
          && K(new ShelfItemFacts { otherStuff = true }) == "ResourcesRaw", "a material: leathers, textiles, or the raw resources");
        C(PokeWorldShelvesMath.ToPlace(true, true, false, false), "a storable item in PokéWorld's lists only is placed");
        C(!PokeWorldShelvesMath.ToPlace(false, true, false, false) && !PokeWorldShelvesMath.ToPlace(true, false, false, false)
          && !PokeWorldShelvesMath.ToPlace(true, true, true, false) && !PokeWorldShelvesMath.ToPlace(true, true, false, true),
            "not a building or a Pokémon, nothing that can't be stored, no unfinished thing, nothing already in the game's lists");

        // Every category it can name is the game's own, each falling back to the one above it.
        var ludeonCats = new HashSet<string>(Catalogue(root).Where(c => c.type == "ThingCategoryDef" && c.mod.StartsWith("ludeon.")).Select(c => c.def));
        foreach (var kv in PokeWorldShelvesMath.Parents)
        {
            C(ludeonCats.Contains(kv.Key), $"{kv.Key} is one of the game's own categories");
            var chain = PokeWorldShelvesMath.WithParents(kv.Key).ToList();
            C(chain[0] == kv.Key && chain.Count <= 4 && PokeWorldShelvesMath.Parents[chain[^1]] == null, $"{kv.Key} falls back up to a top category ({string.Join(" > ", chain)})");
        }

        // ---------- PokéWorld's own items, from the player's catalogue ----------
        const string Pw = "themoonitself.pokeworld";
        var itemsHead = File.ReadLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).First().Split('\t').ToList();
        int Col(string name) => itemsHead.IndexOf(name);
        var items = File.ReadLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Where(c => c.Length > 25 && c[1] == Pw && c[2] == "yes").ToList();
        var pwCats = new HashSet<string>(Catalogue(root).Where(c => c.type == "ThingCategoryDef" && c.mod == Pw).Select(c => c.def));
        var kindOf = new Dictionary<string, string>();
        int skipped = 0;
        foreach (var r in items)
        {
            string cats = r[Col("thingCategories")], comps = r[Col("comps")], foodType = r[Col("foodType")], pref = r[Col("preferability")];
            bool item = r[Col("category")] == "Item";
            bool others = cats.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(c => !pwCats.Contains(c));
            if (!PokeWorldShelvesMath.ToPlace(item, cats.Length > 0, r[0].StartsWith("PW_Unfinished"), others))
            {
                skipped++;
                continue;
            }
            float.TryParse(r[Col("nutrition")], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float nutrition);
            string drug = r[Col("drugCategory")];
            var f = new ShelfItemFacts
            {
                hatches = comps.Split(',').Any(PokeWorldShelvesMath.IsHatcherType),
                medicine = r[Col("potency")].Length > 0,
                drug = drug.Length > 0 && drug != "None",
                food = foodType.Length > 0 && nutrition > 0f,
                meal = pref.StartsWith("Meal"),
                meat = foodType.Contains("Meat"),
                plantFood = foodType.Contains("VegetableOrFruit") || foodType.Contains("Seed"),
                animalProduct = foodType.Contains("AnimalProduct"),
                leathery = r[Col("stuffProps")].Split(',').Contains("Leathery"),
                fabric = r[Col("stuffProps")].Split(',').Contains("Fabric"),
                otherStuff = r[Col("stuffProps")].Length > 0
            };
            kindOf[r[0]] = K(f);
        }
        var apparel = File.ReadLines(Path.Combine(root, "docs", "player-mods", "apparel.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 7 && c[1] == Pw && c[2] == "yes").ToList();
        foreach (var a in apparel)
            kindOf[a[0]] = K(new ShelfItemFacts { apparel = true, beltLayer = a[6].Split(',').Contains("Belt"), onHead = a[7].Split(',').Any(g => g == "UpperHead" || g == "FullHead") });

        C(items.Count > 400 && kindOf.Count > 400, $"PokéWorld's items read from the catalogue ({items.Count} items, {apparel.Count} apparel, {kindOf.Count} placed)");
        C(kindOf.TryGetValue("PW_DawnStone", out var dawn) && dawn == "ItemsMisc", "the Dawn Stone the player named goes with the miscellaneous items");
        string[] stones = { "PW_DawnStone", "PW_FireStone", "PW_IceStone", "PW_LeafStone", "PW_MoonStone", "PW_ShinyStone", "PW_ThunderStone", "PW_WaterStone", "PW_KingsRock" };
        C(stones.All(s => kindOf.TryGetValue(s, out var k) && k == "ItemsMisc"), "every evolution stone with the miscellaneous items");
        foreach (var (def, kind, what) in new[]
                 {
                     ("PW_HelixFossil", "ItemsMisc", "a fossil"), ("PW_OldAmber", "ItemsMisc", "old amber"), ("PW_CryptosleepPokeBall", "ItemsMisc", "a Poké Ball"),
                     ("PW_TechnicalMachine", "ItemsMisc", "a TM"), ("PW_Potion", "ItemsMisc", "a potion"), ("PW_RareCandy", "ItemsMisc", "rare candy"),
                     ("PW_Upgrade", "ItemsMisc", "an evolution item"), ("PW_OddKeystone", "ItemsMisc", "the odd keystone"), ("PW_EggAbra", "EggsFertilized", "a Pokémon egg"),
                     ("PW_Leather_Legendary", "Leathers", "the legendary leather"), ("PW_PokeBallBelt", "ApparelUtility", "a ball belt")
                 })
            C(kindOf.TryGetValue(def, out var k) && k == kind, $"{what} ({def}) goes to {kind}");
        int eggs = items.Count(r => r[Col("thingCategories")].Contains("PW_PokemonEggsFertilized"));
        C(eggs > 300 && kindOf.Count(kv => kv.Value == "EggsFertilized") == eggs, $"every Pokémon egg with the fertilized eggs ({eggs})");
        C(apparel.Count >= 5 && apparel.All(a => kindOf[a[0]] == "ApparelUtility"), "every ball belt with the utility apparel");
        C(!kindOf.ContainsKey("PW_UnfinishedHelixFossil") && !kindOf.ContainsKey("PW_PortableComputer") && skipped >= 18,
            "the unfinished fossils (already the game's unfinished things) and PokéWorld's building are left alone");
        C(kindOf.Values.All(PokeWorldShelvesMath.Parents.ContainsKey), "every PokéWorld item goes to one of the game's own categories");
        C(PokeWorldShelvesMath.Summary(new[] { "ItemsMisc", "EggsFertilized", "ItemsMisc" }) == "ItemsMisc 2, EggsFertilized 1", "the count for the log reads plainly");

        // ---------- The shelf rule: the whole of a category, or not ----------
        C(PokeWorldShelvesMath.TakesWhole(true, false, 0, 0), "a shelf whose own list names the category (or one above it) takes it whole");
        C(PokeWorldShelvesMath.TakesWhole(true, false, 47, 46), "named in its list, one thing set apart by name doesn't count against it");
        C(!PokeWorldShelvesMath.TakesWhole(true, true, 47, 46), "named, but a part of it left out by its list: not whole");
        C(PokeWorldShelvesMath.TakesWhole(false, false, 47, 47), "counted: every thing of the category taken is the whole");
        C(!PokeWorldShelvesMath.TakesWhole(false, false, 47, 46) && !PokeWorldShelvesMath.TakesWhole(false, false, 47, 1), "counted: one thing short is not the whole (a shelf that takes only a few kinds, a weapon rack's)");
        C(!PokeWorldShelvesMath.TakesWhole(false, false, 0, 0), "an empty category is never taken whole by count");
        C(PokeWorldShelvesMath.Allow(true, false, false, "ItemsMisc") && !PokeWorldShelvesMath.Allow(false, false, false, "ItemsMisc"), "a shelf takes the items of a category only where it takes the whole of it");
        C(!PokeWorldShelvesMath.Allow(true, true, false, "ItemsMisc") && PokeWorldShelvesMath.Allow(true, true, true, "ItemsMisc") && PokeWorldShelvesMath.Allow(true, true, false, "EggsFertilized"),
            "a fridge takes only what spoils, and eggs");

        // The fridges and the shelves of the player's storage mods, by their class (buildings.tsv, active rows).
        var buildings = File.ReadLines(Path.Combine(root, "docs", "player-mods", "buildings.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 14 && c[2] == "yes").ToList();
        var fridges = buildings.Where(b => b[1] == "rimfridge.kv.rw").ToList();
        C(fridges.Count >= 5 && fridges.All(b => PokeWorldShelvesMath.IsFridgeType(b[14])), $"RimFridge's {fridges.Count} fridges are fridges by their class");
        var shelves = buildings.Where(b => b[1] == "lwm.deepstorage" || b[1] == "mlie.deepstorageplus" || b[1] == "ludeon.rimworld" && b[5].Contains("storage")).ToList();
        C(shelves.Count >= 18 && shelves.All(b => !PokeWorldShelvesMath.IsFridgeType(b[14])), $"Deep Storage's, Deep Storage Plus's and the game's {shelves.Count} storage buildings are not fridges by their class");
        C(PokeWorldShelvesMath.IsHatcherType("CompProperties_Hatcher") && PokeWorldShelvesMath.IsHatcherType("CompProperties_PokemonEggHatcher") && !PokeWorldShelvesMath.IsHatcherType("CompProperties_Rottable"),
            "a hatcher comp by its type, the game's and PokéWorld's");

        // ---------- The seams ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string code = File.ReadAllText(Path.Combine(src, "Integrations", "PokeWorldShelves.cs"));
        string comp = File.ReadAllText(Path.Combine(src, "CouncilGameComponent.cs"));
        string mod = File.ReadAllText(Path.Combine(src, "ColonyCouncilMod.cs"));
        string catalog = File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs"));
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        string selfCheck = File.ReadAllText(Path.Combine(src, "UI", "SelfCheck.cs"));
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Integrations_PokeWorld.xml"));
        C(code.Contains("[StaticConstructorOnStartup]") && code.Contains("LongEventHandler.ExecuteWhenFinished(Apply);"), "placed once every mod's startup has run");
        C(code.Contains("if (!PokeWorldLink.Active || failed != null) return;") && code.Contains("\"PokéWorld isn't loaded\""), "nothing without PokéWorld, and it says so (StandsDownWhy)");
        C(code.Contains("d?.modContentPack != pack") && code.Contains("d.category == ThingCategory.Item && d.race == null && !d.IsCorpse"), "only PokéWorld's own items, never its Pokémon, corpses or buildings");
        C(code.Contains("b.modContentPack == pack) continue;"), "PokéWorld's own buildings are left alone");
        C(code.Contains("item.thingCategories.Add(kind);") && code.Contains("kind.childThingDefs.Add(item);") && code.Contains("c.ResolveReferences();"),
            "placed in the game's category both ways, its cached lists worked out again (and those above it)");
        C(code.Contains("own?.RecalculateDisplayRootCategory();") && code.Contains("defaults?.RecalculateDisplayRootCategory();"), "the filters' display roots worked out again, so the filter window lists them");
        C(code.Contains("bool fixedTakes = own == null || PokeWorldShelvesMath.TakesWhole(") && code.Contains("bool defaultTakes = defaults != null"),
            "the fixed filter where it takes the whole category, the default settings where they take the whole of it too");
        C(code.Contains("PokeWorldShelvesMath.Allow(true, fridge, Rots(item), g.Key.defName)") && code.Contains("IsFridge(b.def)"), "fridges by the fridge rule, at startup and on the shelves already built");
        C(code.Contains("kindsOf[kind] = kind.DescendantThingDefs.Where(k => k != null && k.modContentPack != pack && k.EverStorable(false))"),
            "the whole of a category is the game's own things in it, counted before PokéWorld's join");
        C(code.Contains("var within = parent == null ? kindsOf[g.Key] : kindsOf[g.Key].Where(parent.Allows).ToList();") && code.Contains("(parent != null && !parent.Allows(item)) || own.filter.Allows(item)"),
            "a shelf already built takes them only where its own settings take the whole of what it can hold of that category");
        C(code.Contains("private static void Undo()") && code.Contains("filter.SetAllow(item, false);") && code.Contains("kind.childThingDefs.Remove(item);"), "switched off, everything placed is undone");
        C(comp.Contains("public int pokeShelvesVersion;") && comp.Contains("Scribe_Values.Look(ref pokeShelvesVersion, \"pokeShelvesVersion\");")
          && comp.Contains("if (pokeShelvesVersion >= 1 || !PokeWorldShelves.Applied) return;") && comp.Contains("PokeShelvesOnce();"),
            "the shelves already built take them once a save, at load");
        C(mod.Contains("PokeWorldShelves.Apply();") && mod.Contains("CouncilGameComponent.Instance?.PokeShelvesOnce();"), "the settings window applies it at once, and a running game's shelves take them");
        C(settings.Contains("public bool pokemonShelves = true;") && settings.Contains("Scribe_Values.Look(ref pokemonShelves, \"pokemonShelves\", true);")
          && code.Contains("ColonyCouncilMod.Settings?.pokemonShelves ?? true"), "the setting, on by default");
        var row = SettingsCatalog.Find("pokemonShelves");
        C(row != null && row.page == SettingsPage.OtherMods && row.section == "CC_SetSec_Mod_PokeWorld" && row.mods != null && row.mods.Contains(SettingsCatalog.PokeWorldId) && row.needs == null,
            "on Your other mods under PokéWorld, shown only with it, not a game hook");
        C(catalog.Contains("Mod(Toggle(\"pokemonShelves\", \"CC_Settings_PokemonShelves\", \"CC_SetNote_PokemonShelves\"), PokeWorldId);"), "its row in the catalogue");
        foreach (var key in new[] { "CC_Settings_PokemonShelves", "CC_Settings_PokemonShelves_Tip", "CC_SetNote_PokemonShelves" })
        {
            var m = Regex.Match(keyed, "<" + key + ">(.*?)</" + key + ">", RegexOptions.Singleline);
            C(m.Success && m.Groups[1].Value.Length > 10 && !m.Groups[1].Value.Contains("—") && !m.Groups[1].Value.Contains('!'), $"{key}: plain text, no em dash or exclamation mark");
        }
        C(Regex.Match(keyed, "<CC_Settings_PokemonShelves>(.*?)</CC_Settings_PokemonShelves>").Groups[1].Value == "PokéWorld's items on the shelves", "the setting's name as the player reads it");
        C(selfCheck.Contains("PokeWorldShelves.CheckLine()"), "the self-check says how many were placed and which storage takes them");
        return n;
    }

    private static List<(string type, string def, string mod, bool active)> catalogue;

    /// <summary>The player's mods' every def (docs/player-mods/defs.tsv.gz): type, name, package, active.</summary>
    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }
}
