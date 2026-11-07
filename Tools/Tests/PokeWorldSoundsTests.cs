using System;
using System.IO;
using ColonyCouncil;

/// <summary>
/// What PokéWorld's Pokémon sound like (the player's request, 6 November 2026): a base-game animal's voice by body family,
/// types and size, checked on PokéWorld's own species, life stages matched first to first and last to last, never more often
/// than PokéWorld had it, the rarer pace, and the setting wired in.
/// </summary>
public static class PokeWorldSoundsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pokemon sounds: " + what);
        }

        // The nearest animal, when the named one is missing: the body family counts most, then the flesh, then how many times bigger.
        float same = PokeWorldSoundsMath.Distance(true, true, 1f, 1f);
        C(same == 0f, "the same body, flesh and size is no distance");
        C(PokeWorldSoundsMath.Distance(true, true, 0.3f, 0.3f * (float)Math.E) > 0.99f && PokeWorldSoundsMath.Distance(true, true, 0.3f, 0.3f * (float)Math.E) < 1.01f,
            "a size e times bigger counts one");
        C(PokeWorldSoundsMath.Distance(true, true, 1f, 4f) < PokeWorldSoundsMath.Distance(false, true, 1f, 1f), "the same family four times the size beats another family the same size");
        C(PokeWorldSoundsMath.Distance(true, false, 1f, 1f) < PokeWorldSoundsMath.Distance(false, true, 1f, 1f), "another flesh counts less than another family");
        C(PokeWorldSoundsMath.Distance(true, true, 1f, 2f) == PokeWorldSoundsMath.Distance(true, true, 2f, 1f), "bigger or smaller, the same distance");
        C(!float.IsInfinity(PokeWorldSoundsMath.Distance(true, true, 0f, 1f)) && !float.IsNaN(PokeWorldSoundsMath.Distance(true, true, 0f, 0f)), "a size of nothing stays sane");

        // The body families, PokéWorld's and the game's.
        C(PokeWorldSoundsMath.FamilyOf("PW_WingedPokemonWithBeak") == BodyFamily.Bird && PokeWorldSoundsMath.FamilyOf("Bird") == BodyFamily.Bird, "birds");
        C(PokeWorldSoundsMath.FamilyOf("PW_WingedPokemonWithJaw") == BodyFamily.WingedBeast, "winged ones with a jaw");
        C(PokeWorldSoundsMath.FamilyOf("PW_InsectPokemonWithWingsAndLegs") == BodyFamily.Insect && PokeWorldSoundsMath.FamilyOf("BeetleLike") == BodyFamily.Insect, "insects, winged or not");
        C(PokeWorldSoundsMath.FamilyOf("PW_QuadrupedPokemonWithHoovesAndTail") == BodyFamily.Hooved && PokeWorldSoundsMath.FamilyOf("QuadrupedAnimalWithHooves") == BodyFamily.Hooved, "hooved");
        C(PokeWorldSoundsMath.FamilyOf("PW_QuadrupedPokemonWithPaws") == BodyFamily.Paws && PokeWorldSoundsMath.FamilyOf("QuadrupedAnimalWithPawsAndTail") == BodyFamily.Paws, "pawed");
        C(PokeWorldSoundsMath.FamilyOf("PW_BipedPokemonWithTail") == BodyFamily.Biped && PokeWorldSoundsMath.FamilyOf("Monkey") == BodyFamily.Biped, "two-legged");
        C(PokeWorldSoundsMath.FamilyOf("PW_HeadPokemonWithArms") == BodyFamily.Head && PokeWorldSoundsMath.FamilyOf("PW_MultipleBodiedPokemon3Head") == BodyFamily.Head, "heads");
        C(PokeWorldSoundsMath.FamilyOf("PW_SnakePokemon") == BodyFamily.Snake && PokeWorldSoundsMath.FamilyOf("PW_FishPokemonWithPectoralCaudalDorsalFins") == BodyFamily.Fish
          && PokeWorldSoundsMath.FamilyOf("PW_TentaclePokemonWithShell") == BodyFamily.Tentacle && PokeWorldSoundsMath.FamilyOf(null) == BodyFamily.Other, "snakes, fish, tentacles");

        // The voice, on PokéWorld's own species (body, size and types from its files).
        string V(string body, float size, params string[] types) => PokeWorldSoundsMath.Voice(PokeWorldSoundsMath.FamilyOf(body), size, types);
        C(V("PW_QuadrupedPokemonWithPaws", 0.25f, "Electric") == "Squirrel", "Pikachu squeaks like a squirrel");
        C(V("PW_BipedPokemonWithTail", 0.37f, "Fire") == "Iguana", "Charmander hisses like an iguana");
        C(V("PW_BipedPokemonWithTail", 1.06f, "Fire", "Flying") == "Warg", "Charizard growls like a warg");
        C(V("PW_WingedPokemonWithBeak", 0.19f, "Normal", "Flying") == "Chicken", "Pidgey clucks");
        C(V("PW_WingedPokemonWithJaw", 0.5f, "Poison", "Flying") == "Rat", "Zubat squeaks like a rat");
        C(V("PW_HeadPokemonWithArms", 0.25f, "Rock", "Ground") == "Tortoise" && V("PW_HeadPokemon", 0.31f, "Electric") == "Tortoise", "Geodude and Voltorb take the tortoise's voice");
        C(V("PW_SnakePokemon", 5.5f, "Rock", "Ground") == "Megasloth" && V("PW_SnakePokemon", 4.38f, "Dragon", "Flying") == "Thrumbo" && V("PW_SnakePokemon", 0.62f, "Poison") == "Cobra",
            "Onix rumbles, Rayquaza roars, Ekans hisses");
        C(V("PW_FishPokemonWithPectoralCaudalDorsalFins", 0.56f, "Water") == "Tortoise" && V("PW_FishPokemonWithPectoralCaudalDorsalFins", 9.06f, "Water") == "Elephant",
            "Magikarp takes the tortoise's voice, Wailord lows like an elephant");
        C(V("PW_QuadrupedPokemonWithHoovesAndTail", 0.87f, "Normal") == "Muffalo" && V("PW_QuadrupedPokemonWithHoovesAndTail", 0.62f, "Fire") == "Deer", "Tauros lows, Ponyta calls like a deer");
        // 7 November 2026: the game's foxes have no sounds and its cougar and lynx no call, so Growlithe yips like a small
        // dog (it was a fox), Mewtwo takes the monkey's voice the game already gave it (it was a cougar), and Ivysaur
        // grunts like its Bulbasaur (it was a lynx).
        C(V("PW_QuadrupedPokemonWithPaws", 1.19f, "Fire") == "Warg" && V("PW_QuadrupedPokemonWithPaws", 0.44f, "Fire") == "YorkshireTerrier", "Arcanine growls, Growlithe yips like a small dog");
        C(V("PW_BipedPokemon", 1.31f, "Normal") == "Bear_Grizzly" && V("PW_BipedPokemonWithTail", 1.25f, "Psychic") == "Monkey", "Snorlax growls like a bear, Mewtwo takes the monkey's voice");
        C(V("PW_QuadrupedPokemonWithPaws", 0.62f, "Grass", "Poison") == "Capybara" && V("PW_QuadrupedPokemonWithPaws", 1.25f, "Grass", "Poison") == "Bear_Grizzly",
            "Ivysaur grunts like a capybara, Venusaur growls like a bear");
        C(V("PW_QuadrupedPokemonWithPawsAndTail", 0.87f, "Electric") == "Husky" && V("PW_QuadrupedPokemonWithPaws", 1.19f, "Electric") == "Warg", "Luxray barks, Raikou growls");
        C(V("PW_WingedPokemonWithJaw", 0.69f, "Ground", "Flying") == "Warg", "Gligar growls like its Gliscor (the cougar's place)");
        // The bigger birds honk like geese: the game's turkey, emu and ostrich call only as chicks.
        C(V("PW_WingedPokemonWithBeak", 0.94f, "Normal", "Flying") == "Goose" && V("PW_WingedPokemonWithBeak", 2.37f, "Fire", "Flying") == "Goose"
          && V("PW_WingedPokemonWithBeak", 0.37f, "Water", "Flying") == "Duck", "Pidgeot and Ho-Oh honk, Wingull quacks");
        // The heads of ice and water take the tortoise's voice with the stones; bird heads cluck or honk; a fierce big one growls.
        C(V("PW_HeadPokemon", 0.25f, "Ice") == "Tortoise" && V("PW_HeadPokemonWithBody", 0.75f, "Water") == "Tortoise", "Vanillite and Seadra take the tortoise's voice");
        C(V("PW_HeadPokemonWithBody", 1.12f, "Normal", "Flying") == "Goose" && V("PW_MultipleBodiedPokemon3Head", 1.12f, "Dark", "Dragon") == "Warg", "Dodrio honks, Hydreigon growls");
        C(V("PW_InsectPokemonWith6Legs", 0.19f, "Bug") == "Megascarab" && V("PW_InsectPokemonWithWingsAndLegs", 0.94f, "Bug", "Flying") == "Spelopede", "Caterpie and Scyther click like insects");
        C(V("PW_BipedPokemonWithTail", 0.5f, "Water") == "Duck" && V("PW_QuadrupedPokemonWithPaws", 0.44f, "Grass", "Poison") == "Capybara", "Psyduck quacks, Bulbasaur grunts");
        C(V("PW_HeadPokemonWithLegs", 0.25f, "Fire") == "Chicken", "Torchic cheeps");
        foreach (BodyFamily f in Enum.GetValues(typeof(BodyFamily)))
            foreach (var size in new[] { 0.1f, 0.4f, 0.8f, 1.2f, 2f, 5f })
                C(!string.IsNullOrEmpty(PokeWorldSoundsMath.Voice(f, size, new string[0])), $"{f} at {size}: always some animal");

        // Every voice the rule can name is a base-game animal whose grown ones call: every family, size band and pair of types.
        string[] allTypes = { "Normal", "Fire", "Water", "Grass", "Electric", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel", "Fairy" };
        float[] sizes = { 0.05f, 0.2f, 0.3f, 0.45f, 0.6f, 0.8f, 1f, 1.3f, 1.6f, 2.5f, 3.5f, 9f };
        var callers = new System.Collections.Generic.HashSet<string>(PokeWorldSoundsMath.GrownCallers);
        var silent = new System.Collections.Generic.HashSet<string>();
        foreach (BodyFamily f in Enum.GetValues(typeof(BodyFamily)))
            foreach (var size in sizes)
                for (int a = 0; a < allTypes.Length; a++)
                    for (int b = a; b < allTypes.Length; b++)
                    {
                        string v = PokeWorldSoundsMath.Voice(f, size, a == b ? new[] { allTypes[a] } : new[] { allTypes[a], allTypes[b] });
                        if (!callers.Contains(v)) silent.Add(v);
                    }
        C(silent.Count == 0, "the rule only names animals whose grown ones call" + (silent.Count > 0 ? " (not " + string.Join(", ", silent) + ")" : ""));
        foreach (var quiet in new[] { "Cougar", "Lynx", "Panther", "Fox_Red", "Fox_Fennec", "Fox_Arctic", "Turkey", "Emu", "Cassowary", "Ostrich" })
            C(!callers.Contains(quiet), quiet + " isn't among the animals that call grown");

        // Never more often than PokéWorld had it.
        C(PokeWorldSoundsMath.Slower(3000, 6000, 2000, 4000) == (3000, 6000) && PokeWorldSoundsMath.Slower(3000, 6000, 4000, 8000) == (4000, 8000),
            "the slower pace of the two");
        C(PokeWorldSoundsMath.SlowerFactor(0.25f, 1f) == 1f, "and the slower of the angry and tamed factors");

        // Life stages: first to first, last to last, one stage takes the grown one.
        C(PokeWorldSoundsMath.StageFor(0, 1, 3) == 2, "one stage of its own borrows the animal's grown one");
        C(PokeWorldSoundsMath.StageFor(0, 3, 3) == 0 && PokeWorldSoundsMath.StageFor(2, 3, 3) == 2 && PokeWorldSoundsMath.StageFor(1, 3, 3) == 1, "stage for stage when both have three");
        C(PokeWorldSoundsMath.StageFor(0, 2, 3) == 0 && PokeWorldSoundsMath.StageFor(1, 2, 3) == 2, "two stages: young to young, grown to grown");
        C(PokeWorldSoundsMath.StageFor(1, 3, 1) == 0 && PokeWorldSoundsMath.StageFor(0, 3, 0) == -1, "an animal with one stage lends it to all; one with none lends nothing");
        C(PokeWorldSoundsMath.StageFor(9, 3, 3) == 2, "a stage past the end is the last");

        // The rarer pace.
        C(PokeWorldSoundsMath.Rarer(2000) == 6000 && PokeWorldSoundsMath.Rarer(0) == 0, "rarer is three times as long between calls");

        // The setting, the row and the hooks.
        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string settings = src("CouncilSettings.cs"), catalog = src("UI/SettingsCatalog.cs"), presets = src("UI/SettingsPresets.cs"), mod = src("ColonyCouncilMod.cs");
        string code = src("Integrations/PokeWorldSounds.cs");
        C(settings.Contains("public PokemonSounds pokemonSounds = PokemonSounds.Creatures;") && settings.Contains("Scribe_Values.Look(ref pokemonSounds, \"pokemonSounds\", PokemonSounds.Creatures);"),
            "the setting is saved, the animals' voices by default");
        C(catalog.Contains("Choice(\"pokemonSounds\"") && catalog.Contains("new[] { \"Creatures\", \"Rarer\", \"Own\" }), PokeWorldId)"), "its row is on the other mods' page, for PokéWorld");
        C(Enum.GetNames(typeof(PokemonSounds)).Length == 3 && Enum.IsDefined(typeof(PokemonSounds), "Creatures") && Enum.IsDefined(typeof(PokemonSounds), "Rarer")
          && Enum.IsDefined(typeof(PokemonSounds), "Own"), "the row's choices are the setting's values");
        C(presets.Contains("id == SettingsCatalog.PokeWorldId ? PokeWorldLink.Active") && presets.Contains("if (ids.Contains(\"pokemonSounds\")) PokeWorldSounds.Apply();"),
            "PokéWorld is found by its Pokémon, and a change is applied at once");
        C(mod.Contains("PokeWorldSounds.Apply();"), "applied again when the settings window closes");
        C(code.Contains("r.Dryad") && code.Contains("r.IsMechanoid") && code.Contains("PokeWorldLink.IsPokemon(d)"), "never a mechanoid, a dryad or another Pokémon's voice");
        C(code.Contains("r.lifeStageAges[r.lifeStageAges.Count - 1]?.soundCall == null) continue;"), "only an animal whose grown one calls lends its voice (never a chick's cheep)");
        C(code.Contains("PokeWorldSoundsMath.Voice(PokeWorldSoundsMath.FamilyOf(r.body?.defName), r.baseBodySize, PokeWorldLink.TypesOf(pokemon))")
          && code.Contains("PokeWorldSoundsMath.Slower(was.interval.min"), "the game uses the same rule, and the slower pace");
        C(code.Contains("r.lifeStageAges = kv.Value.stages;") && code.Contains("(LifeStageAge)Clone.Invoke(own[i], null)"),
            "each species gets copies of its life stages, and the originals come back");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Integrations_PokeWorld.xml"));
        foreach (var o in Enum.GetNames(typeof(PokemonSounds)))
            C(keys.Contains("<CC_PokemonSounds_" + o + ">") && keys.Contains("<CC_PokemonSounds_" + o + "_Tip>") && keys.Contains("<CC_SetNote_PokemonSounds_" + o + ">"),
                o + ": its words");
        return n;
    }
}
