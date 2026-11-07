using System;
using System.IO;
using ColonyCouncil;

/// <summary>
/// PokéWorld reviewed against the player's version, PokéWorld Updated for 1.6 (themoonitself.pokeworld, 898 species of
/// eight generations), 7 November 2026: which species roam, the spawn slider's share, the names Rimfolk reads from it (each
/// checked against its decompiled code), what is said in the log when one can't be read, the shiny shown, and the
/// Almanac's eight generations.
/// </summary>
public static class PokeWorldReviewTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pokeworld review: " + what);
        }

        // Which species roam: PokéWorld's spawner picks from the land's own wild animals, where it lists no legendary,
        // fossil or baby, nor Porygon, Unown, Spiritomb and the few others it keeps out of the wild.
        C(AnimalMath.PokemonRoams(false, true), "a species in some land's wild animals roams");
        C(!AnimalMath.PokemonRoams(false, false), "a baby or one kept out of the wild (in no land's list) doesn't");
        C(!AnimalMath.PokemonRoams(true, true) && !AnimalMath.PokemonRoams(true, false), "a legendary or a fossil never does");

        // The slider: its spawner picks a Pokémon when a roll between the ends is at most the setting (0 to 8 in steps).
        C(AnimalMath.SliderShare(0f, 0f, 8f) == 0f && AnimalMath.SliderShare(8f, 0f, 8f) == 1f, "no Pokémon at the minimum, only Pokémon at the maximum");
        C(AnimalMath.SliderShare(2f, 0f, 8f) == 0.25f && AnimalMath.SliderShare(4f, 0f, 8f) == 0.5f, "each step an eighth");
        C(AnimalMath.SliderShare(9f, 0f, 8f) == 1f && AnimalMath.SliderShare(-1f, 0f, 8f) == 0f, "past the ends, held at the ends");
        C(AnimalMath.SliderShare(3f, 8f, 8f) < 0f && AnimalMath.SliderShare(3f, 8f, 0f) < 0f, "ends that make no range can't be read");

        // The rarity scale: 1 to 10, 10 the legendaries (and Type: Null and Silvally, which do roam); rare sightings 6 to 9.
        C(!AnimalMath.RareSighting(5) && AnimalMath.RareSighting(6) && AnimalMath.RareSighting(9) && !AnimalMath.RareSighting(10) && !AnimalMath.RareSighting(0),
            "a rare sighting is rarity 6 to 9, never the top or an unread one");

        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string link = src("Integrations/PokeWorld.cs"), life = src("Integrations/PokeWorldLife.cs"), camp = src("Integrations/PokeWorldCamp.cs");

        // Every name read from PokéWorld, as its 1.6 code has it (PokeWorld.CompPokemon, CompProperties_Pokemon,
        // PokeWorldSettings, StorageSystem, PokedexManager, CompProperties_Pokeball, the trackers).
        foreach (var name in new[]
                 {
                     "\"CompPokemon\"", "\"generation\"", "\"rarity\"", "\"types\"", "\"attributes\"", "\"Legendary\"", "\"Fossil\"", "\"shinyChance\"",
                     "\"PokeWorld.PokeWorldSettings\"", "\"selectedPokemonFrequency\"", "\"minFrequency\"", "\"maxFrequency\"", "\"allowGen\"", "\"GenerationAllowed\"",
                     "\"PokeWorld.StorageSystem\"", "\"PokeWorld.PokedexManager\"", "\"IsPokemonCaught\"", "\"CompProperties_Pokeball\""
                 })
            C(link.Contains(name), "PokeWorld.cs reads " + name);
        foreach (var name in new[] { "\"levelTracker\", \"level\"", "\"friendshipTracker\", \"friendship\"", "\"shinyTracker\", \"isShiny\"", "\"TotalCaught\"" })
            C(life.Contains(name), "PokeWorldLife.cs reads " + name);

        // Generations 5 to 8 count: its own GenerationAllowed first, the switches up to the highest generation its species have.
        C(link.Contains("for (int gen = 1; gen <= Math.Max(9, highestGeneration); gen++)") && link.Contains("new bool[Math.Max(10, highestGeneration + 1)]"),
            "every generation's switch is read, eight in PokéWorld Updated");
        C(link.Contains("return (bool)generationAllowed.Invoke(null, new object[] { gen });"), "its own generation check is asked first, as its spawner asks it");

        // Roaming and the animal events: only species of some land's wild animals.
        C(link.Contains("BiomeAnimals.Of(biome)") && link.Contains("s.wild = true"), "the wild ones are the ones some land lists");
        C(link.Contains("AnimalMath.PokemonRoams(s.rare, s.wild)") && link.Contains("else if (Roams(s, allowed)) pokemon.Add(k);") && link.Contains("if (Roams(kv.Value, allowed)) yield return kv.Key;"),
            "the events and the roaming kinds use the same rule");
        C(camp.Contains("PokeWorldLink.Roaming().Where(PokeWorldLink.CanShine)"), "a shiny rumour names a species that roams and can be shiny");

        // The Pokédex: PokéWorld Updated keeps it by number, so the caught kinds are asked of it, not read from its dictionary.
        C(link.Contains("GetMethod(\"IsPokemonCaught\", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(PawnKindDef) }, null)"), "the caught species are asked of the Pokédex");
        C(life.Contains("GetProperty(\"caught\""), "a Pokédex of entries is counted by their caught flag when it has no total");

        // A name that can't be read is said once, with what Rimfolk does without it.
        foreach (var said in new[]
                 {
                     "couldn't read its generation switches", "found no switch for generation", "couldn't find its PC storage", "couldn't find its Pokédex",
                     "couldn't read its Pokémon's generations", "couldn't read its Pokémon's rarity", "couldn't read its Pokémon's types",
                     "couldn't read its Pokémon's attributes", "none of its Pokémon live wild", "couldn't read which species its Pokédex has caught",
                     "no Poké Ball belts were recognized", "couldn't read its wild Pokémon frequency setting"
                 })
            C(link.Contains(said), "the log says when it " + said.Replace("couldn't", "can't"));
        C(life.Contains("couldn't count its Pokédex") && life.Contains("CouncilLog.WarnOnce(\"PokéWorld: couldn't find \" + what"), "and when the Pokédex or a tracker can't be read");

        // The shiny the rumour brings looks shiny: PokéWorld picks its colours when the graphics are made.
        C(life.Contains("if (p.Spawned) p.Drawer?.renderer?.SetAllGraphicsDirty();"), "a Pokémon made shiny on the map is drawn again");

        // The Almanac counts PokéWorld Updated's eight generations.
        string alm = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Mods_PokeWorld.xml"));
        C(alm.Contains("eight switches, one for each generation (Gen. 1 to 8") && !alm.Contains("four switches") && alm.Contains("switch off all eight"), "the Almanac's generation switches are eight");
        C(alm.Contains("898 of eight generations"), "and its species 898");
        return n;
    }
}
