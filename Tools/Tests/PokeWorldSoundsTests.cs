using System;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// What PokéWorld's Pokémon sound like (the player's request, 6 November 2026): an animal's voice by body family, types and
/// size, another mod's where it fits better (7 November 2026, checked on the player's mod list) and the base game's
/// otherwise, checked on PokéWorld's own species, life stages matched first to first and last to last, never more often
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

        // Other mods' animals first where they fit better, the base game's last (7 November 2026, the player's mod list).
        // docs/player-animals.tsv: the animals of the player's active mods whose grown ones have a call of their own.
        var player = new System.Collections.Generic.Dictionary<string, string[]>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "docs", "player-animals.tsv")))
        {
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("defName\t")) continue;
            var f = line.Split('\t');
            if (f.Length >= 8) player[f[0]] = f;
        }
        C(player.Count >= 40 && player.ContainsKey("AA_MeadowAve") && player.ContainsKey("AEXP_Zebra"), "the player's modded animals are read (" + player.Count + ")");
        string[] bodies =
        {
            "PW_InsectPokemonWith6Legs", "PW_InsectPokemonWithWingsAndLegs", "PW_WingedPokemonWithBeak", "PW_WingedPokemonWithJaw", "PW_SnakePokemon",
            "PW_FishPokemonWithPectoralCaudalDorsalFins", "PW_TentaclePokemon", "PW_QuadrupedPokemonWithHoovesAndTail", "PW_QuadrupedPokemonWithPaws",
            "PW_BipedPokemonWithTail", "PW_HeadPokemon", "PW_MultipleBodiedPokemon3Head", null
        };
        string[] names = { "PW_Bulbasaur", "PW_Pikachu", "PW_Eevee", "PW_Mew", "PW_Zarude" };
        var named = new System.Collections.Generic.HashSet<string>();
        string notBaseLast = null, changedWithout = null, notCalling = null;
        foreach (var body in bodies)
            foreach (var size in sizes)
                for (int a = 0; a < allTypes.Length; a++)
                    for (int b = a; b < allTypes.Length; b++)
                    {
                        var types = a == b ? new[] { allTypes[a] } : new[] { allTypes[a], allTypes[b] };
                        string plain = PokeWorldSoundsMath.Voice(PokeWorldSoundsMath.FamilyOf(body), size, types);
                        var voices = PokeWorldSoundsMath.Voices(body, size, types);
                        var last = voices[voices.Count - 1];
                        if (last.Length != 1 || last[0] != plain || !callers.Contains(last[0])) notBaseLast = body + " " + size + " " + string.Join("/", types);
                        foreach (var g in voices)
                            foreach (var v in g)
                            {
                                named.Add(v);
                                if (!PokeWorldSoundsMath.IsBase(v) && !player.ContainsKey(v)) notCalling = v;
                            }
                        foreach (var s in names)
                            if (PokeWorldSoundsMath.Pick(voices, callers.Contains, s) != plain) changedWithout = s + " as " + body + " " + size + " " + string.Join("/", types);
                    }
        C(notBaseLast == null, "the last choice is always the base-game animal the rule names, one whose grown ones call" + (notBaseLast != null ? " (not for " + notBaseLast + ")" : ""));
        C(changedWithout == null, "with none of the other mods, every species sounds as before" + (changedWithout != null ? " (not " + changedWithout + ")" : ""));
        C(notCalling == null, "every other mod's animal named is one of the player's whose grown ones have a call of their own" + (notCalling != null ? " (not " + notCalling + ")" : ""));
        var modded = named.Where(v => !PokeWorldSoundsMath.IsBase(v)).ToList();
        C(modded.Count >= 10 && modded.Select(v => player[v][3]).Distinct().Count() >= 3, "and they come from several of the player's mods (" + string.Join(", ", modded) + ")");
        foreach (var never in new[] { "AEXP_TasmanianDevil", "AEXP_Hyena", "GR_Hurseman", "GR_ParagonHumanoid", "AA_Atispec", "AA_UnblinkingEye", "TM_DemonR", "TM_SkeletonR",
                     "VPE_SteelConstruct", "VQEA_Splicefiend", "pphhyy_Demigryph", "TyrannosaurusRex", "WoollyMammoth" })
            C(!named.Contains(never), never + " is never named (it screams like a person, sounds like a machine or is undead, or its grown ones have no call of their own)");

        // With the player's mods: samples, the same species the same each time, and variety among equals.
        var all = new System.Collections.Generic.HashSet<string>(PokeWorldSoundsMath.GrownCallers);
        all.UnionWith(player.Keys);
        string W(string species, string body, float size, params string[] types) => PokeWorldSoundsMath.Pick(PokeWorldSoundsMath.Voices(body, size, types), all.Contains, species);
        C(W("PW_Pidgeot", "PW_WingedPokemonWithBeak", 0.94f, "Normal", "Flying") == "AA_MeadowAve" && W("PW_Dodrio", "PW_MultipleBodiedPokemon3Head", 1.12f, "Normal", "Flying") == "AA_MeadowAve"
          && W("PW_Swanna", "PW_WingedPokemonWithBeak", 0.81f, "Water", "Flying") == "Goose", "Pidgeot and Dodrio cry like a meadow ave, Swanna still honks like a goose");
        C(W("PW_Beedrill", "PW_InsectPokemonWithWingsAndLegs", 0.62f, "Bug", "Poison") == "AA_Bumbledrone" && W("PW_Combee", "PW_HeadPokemonWithBody", 0.19f, "Bug", "Flying") == "AA_Bumbledrone"
          && W("PW_Crobat", "PW_InsectPokemonWithWingsAndLegs", 1.12f, "Poison", "Flying") == "Megaspider" && W("PW_Caterpie", "PW_InsectPokemonWith6Legs", 0.19f, "Bug") == "Megascarab",
            "Beedrill and Combee buzz like a bumbledrone; Crobat (no bug) and Caterpie (no wings) keep the game's insects");
        C(W("PW_Tentacruel", "PW_TentaclePokemon", 1f, "Water", "Poison") == "AA_OcularJelly" && W("PW_Carnivine", "PW_TentaclePokemon", 0.87f, "Grass") == "AA_Needlepost"
          && W("PW_Sunkern", "PW_HeadPokemon", 0.19f, "Grass") == "AA_Needlepost" && W("PW_Venusaur", "PW_QuadrupedPokemonWithPaws", 1.25f, "Grass", "Poison") == "AA_Wildpawn",
            "Tentacruel takes the ocular jelly's voice, Carnivine and Sunkern the needlepost's, Venusaur the wildpawn's");
        C(W("PW_Blastoise", "PW_BipedPokemonWithTail", 1f, "Water") == "AEXP_Crocodile" && W("PW_Zebstrika", "PW_QuadrupedPokemonWithHoovesAndTail", 1f, "Electric") == "AEXP_Zebra",
            "Blastoise growls like a crocodile, Zebstrika calls like a zebra");
        var noVae = new System.Collections.Generic.HashSet<string>(all);
        noVae.Remove("AEXP_Zebra");
        C(PokeWorldSoundsMath.Pick(PokeWorldSoundsMath.Voices("PW_QuadrupedPokemonWithHoovesAndTail", 1f, new[] { "Electric" }), noVae.Contains, "PW_Zebstrika") == "AEXP_Quagga"
          && PokeWorldSoundsMath.Pick(PokeWorldSoundsMath.Voices("PW_QuadrupedPokemonWithHoovesAndTail", 1f, new[] { "Electric" }), callers.Contains, "PW_Zebstrika") == "Horse",
            "without Vanilla Animals Expanded the Endangered pack's quagga, without either the game's horse");
        C(new[] { "RG_Rimclaw", "RG_ArcticDrak" }.Contains(W("PW_Garchomp", "PW_BipedPokemonWithTail", 1.19f, "Dragon", "Ground")), "Garchomp cries like a rimclaw or an arctic drak");
        C(W("PW_Pikachu", "PW_QuadrupedPokemonWithPaws", 0.25f, "Electric") == "Squirrel" && W("PW_Geodude", "PW_HeadPokemonWithArms", 0.25f, "Rock", "Ground") == "Tortoise"
          && W("PW_Magikarp", "PW_FishPokemonWithPectoralCaudalDorsalFins", 0.56f, "Water") == "Tortoise" && W("PW_Charizard", "PW_BipedPokemonWithTail", 1.06f, "Fire", "Flying") == "Warg",
            "where no modded animal fits better the base game's stays: Pikachu, Geodude, Magikarp, Charizard");
        C(PokeWorldSoundsMath.Stable("a") == 3826002220u && PokeWorldSoundsMath.Stable("PW_Pikachu") == 1373377016u && PokeWorldSoundsMath.Stable(null) == 2166136261u,
            "the number from a name is the same on every machine (FNV-1a)");
        var lions = new System.Collections.Generic.List<string[]> { new[] { "Warg", "AEXP_Lion", "AA_ArcticLion" }, new[] { "Warg" } };
        var shared = new System.Collections.Generic.HashSet<string>();
        bool steady = true;
        for (int i = 0; i < 60; i++)
        {
            string s = "PW_Species" + i, v = PokeWorldSoundsMath.Pick(lions, all.Contains, s);
            shared.Add(v);
            steady &= v == PokeWorldSoundsMath.Pick(lions, all.Contains, s);
        }
        C(shared.SetEquals(new[] { "Warg", "AEXP_Lion", "AA_ArcticLion" }), "equals are shared out among the species");
        C(steady, "and the same species always takes the same one");
        var noLion = new System.Collections.Generic.HashSet<string>(all);
        noLion.Remove("AEXP_Lion");
        C(Enumerable.Range(0, 60).All(i => PokeWorldSoundsMath.Pick(lions, noLion.Contains, "PW_Species" + i) != "AEXP_Lion"), "an animal that isn't loaded is never taken");
        C(PokeWorldSoundsMath.Pick(lions, n => false, "PW_Entei") == null && PokeWorldSoundsMath.Pick(null, all.Contains, "PW_Entei") == null, "with none loaded, none (the nearest animal stands in)");

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
        C(code.Contains("PokeWorldSoundsMath.Voices(r.body?.defName, r.baseBodySize, PokeWorldLink.TypesOf(pokemon))")
          && code.Contains("PokeWorldSoundsMath.Pick(voices, byName.ContainsKey, pokemon.defName)") && code.Contains("PokeWorldSoundsMath.Slower(was.interval.min"),
            "the game uses the same rule, among the animals that call grown, and the slower pace");
        C(code.Contains("if (Prefs.DevMode && !moddedLogged)") && code.Contains("!a.modContentPack.IsOfficialMod"), "the voices from other mods in use are said once in dev mode");
        C(code.Contains("r.lifeStageAges = kv.Value.stages;") && code.Contains("(LifeStageAge)Clone.Invoke(own[i], null)"),
            "each species gets copies of its life stages, and the originals come back");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Integrations_PokeWorld.xml"));
        foreach (var o in Enum.GetNames(typeof(PokemonSounds)))
            C(keys.Contains("<CC_PokemonSounds_" + o + ">") && keys.Contains("<CC_PokemonSounds_" + o + "_Tip>") && keys.Contains("<CC_SetNote_PokemonSounds_" + o + ">"),
                o + ": its words");
        return n;
    }
}
