using System;
using System.IO;
using ColonyCouncil;

/// <summary>
/// What PokéWorld's Pokémon sound like (the player's request, 6 November 2026): the nearest animal's voice by body plan,
/// flesh and size, life stages matched first to first and last to last, the rarer pace, and the setting wired in.
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

        // The nearest animal: the body plan counts most, then the flesh, then how many times bigger.
        float same = PokeWorldSoundsMath.Distance(true, true, 1f, 1f);
        C(same == 0f, "the same body, flesh and size is no distance");
        C(PokeWorldSoundsMath.Distance(true, true, 0.3f, 0.3f * (float)Math.E) > 0.99f && PokeWorldSoundsMath.Distance(true, true, 0.3f, 0.3f * (float)Math.E) < 1.01f,
            "a size e times bigger counts one");
        C(PokeWorldSoundsMath.Distance(true, true, 1f, 4f) < PokeWorldSoundsMath.Distance(false, true, 1f, 1f), "the same body four times the size beats another body the same size");
        C(PokeWorldSoundsMath.Distance(true, false, 1f, 1f) < PokeWorldSoundsMath.Distance(false, true, 1f, 1f), "another flesh counts less than another body");
        C(PokeWorldSoundsMath.Distance(true, true, 1f, 2f) == PokeWorldSoundsMath.Distance(true, true, 2f, 1f), "bigger or smaller, the same distance");
        C(!float.IsInfinity(PokeWorldSoundsMath.Distance(true, true, 0f, 1f)) && !float.IsNaN(PokeWorldSoundsMath.Distance(true, true, 0f, 0f)), "a size of nothing stays sane");

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
        C(code.Contains("r.lifeStageAges = kv.Value.stages;") && code.Contains("(LifeStageAge)Clone.Invoke(own[i], null)"),
            "each species gets copies of its life stages, and the originals come back");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Integrations_PokeWorld.xml"));
        foreach (var o in Enum.GetNames(typeof(PokemonSounds)))
            C(keys.Contains("<CC_PokemonSounds_" + o + ">") && keys.Contains("<CC_PokemonSounds_" + o + "_Tip>") && keys.Contains("<CC_SetNote_PokemonSounds_" + o + ">"),
                o + ": its words");
        return n;
    }
}
