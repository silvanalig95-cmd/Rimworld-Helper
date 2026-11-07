using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What PokéWorld's Pokémon sound like (the player's request, 6 November 2026: "the constant pokemon screetches" don't fit).</summary>
    public enum PokemonSounds
    {
        /// <summary>The calls of a base-game animal picked by the Pokémon's body, type and size, never more often than its own.</summary>
        Creatures,
        /// <summary>Their own cries, a third as often.</summary>
        Rarer,
        /// <summary>As PokéWorld has them.</summary>
        Own
    }

    /// <summary>A body plan, by family: PokéWorld's own body defs and the game's read the same way.</summary>
    public enum BodyFamily
    {
        Other,
        /// <summary>Winged, with a beak: the birds.</summary>
        Bird,
        /// <summary>Winged, with a jaw: bats, pterosaurs, winged dragons.</summary>
        WingedBeast,
        Insect,
        Snake,
        Fish,
        Tentacle,
        Hooved,
        /// <summary>Four legs with paws: the cats, dogs and small beasts.</summary>
        Paws,
        /// <summary>Two legs.</summary>
        Biped,
        /// <summary>A head, with or without arms, legs or a body: the stones, balls, clouds and sprouts.</summary>
        Head
    }

    /// <summary>
    /// The pure rules behind <see cref="PokeWorldSounds"/>. Each species borrows the voice of an animal picked by its body
    /// plan's family (<see cref="FamilyOf"/>), its types and its size: one of the base game's (<see cref="Voice"/>), or,
    /// where another mod's animal fits it better and is loaded, that one (<see cref="Voices"/>, <see cref="Pick"/>). It
    /// never calls more often than PokéWorld had it (<see cref="Slower"/>). The same species sounds the same every time
    /// the same mods are loaded, and with none of the other mods the voice is the base game's, as before. Where no named
    /// animal is there at all, the nearest one of the same family and size is used instead (<see cref="Distance"/>).
    /// </summary>
    public static class PokeWorldSoundsMath
    {
        /// <summary>With <see cref="PokemonSounds.Rarer"/>, the time between calls is this many times PokéWorld's.</summary>
        public const int RarerFactor = 3;

        /// <summary>Smallest body size counted, so a tiny creature's ratio stays sane.</summary>
        public const float MinSize = 0.05f;

        // Size bands, from PokéWorld's sizes (most Pokémon are 0.1 to 1.4; a muffalo is 2.4).
        public const float Tiny = 0.3f, Small = 0.6f, Medium = 1.0f, Large = 1.6f, Giant = 3.5f;

        /// <summary>The family of a body def, by its name: PokéWorld's (PW_WingedPokemonWithBeak...) and the game's (Bird, BeetleLike...).</summary>
        public static BodyFamily FamilyOf(string body)
        {
            if (string.IsNullOrEmpty(body)) return BodyFamily.Other;
            string b = body.ToLowerInvariant();
            if (b.Contains("insect") || b.Contains("beetle") || b.Contains("spider") || b.Contains("scarab")) return BodyFamily.Insect;
            if (b.Contains("winged") || b.Contains("bird")) return b.Contains("jaw") ? BodyFamily.WingedBeast : BodyFamily.Bird;
            if (b.Contains("snake") || b.Contains("serpent")) return BodyFamily.Snake;
            if (b.Contains("fish")) return BodyFamily.Fish;
            if (b.Contains("tentacle")) return BodyFamily.Tentacle;
            if (b.Contains("quadruped")) return b.Contains("hoove") ? BodyFamily.Hooved : BodyFamily.Paws;
            if (b.Contains("biped") || b.Contains("monkey") || b.Contains("human")) return BodyFamily.Biped;
            if (b.Contains("head") || b.Contains("multiple")) return BodyFamily.Head;
            return BodyFamily.Other;
        }

        private static bool Any(ICollection<string> types, params string[] names) =>
            types != null && names.Any(n => types.Any(t => string.Equals(t, n, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Types of the stones, gases, magnets, spirits, ice and shells: a head of one of these makes little sound (water
        /// and ice since 7 November 2026: the ice blocks, clams and sea horses chattered like monkeys or squeaked).
        /// </summary>
        private static readonly string[] Quiet = { "Rock", "Steel", "Electric", "Ghost", "Poison", "Psychic", "Ground", "Water", "Ice" };

        /// <summary>
        /// The base-game animal (its def name) a Pokémon borrows its voice from: by the family of its body, its types and its
        /// size. Only animals whose grown ones call in the game are named: the cougar, the lynx and the panther have no
        /// call of their own (only an angry snarl), the foxes have no sounds at all, and the turkey, the emu and the
        /// ostrich call only as chicks (the game plays a grown one's call from its own stage, so they are silent grown,
        /// and the chick's cheep would be lent). Insects buzz and click like the game's insects, small birds cluck (ducks for
        /// small water birds) and the bigger ones honk like geese, bats squeak like rats and the bigger winged beasts and
        /// dragons growl and roar, snakes hiss like the cobra and the giant ones rumble, fish and tentacles take the
        /// tortoise's voice (the great whales low like an elephant), hooved ones bleat, call and low like goats, deer,
        /// horses and muffalo, pawed ones squeak, mew, yip, bark, growl and roar by size and temper (the small grass and
        /// water ones grunt like a capybara, the middling grass ones too), two-legged ones chatter like monkeys (the small
        /// water ones quack, the small fire and dragon ones hiss like an iguana) and growl or roar when big, the stones,
        /// balls, clouds, ice and shells take the tortoise's voice, the bird-headed ones cluck or honk, and the other heads
        /// cheep like chicks, squeak, chatter or, the fierce big ones, growl.
        /// </summary>
        public static string Voice(BodyFamily family, float size, ICollection<string> types)
        {
            bool water = Any(types, "Water"), fierce = Any(types, "Fire", "Dark", "Dragon"), tough = Any(types, "Normal", "Fighting", "Ground", "Rock", "Steel");
            bool uncanny = Any(types, "Psychic", "Electric", "Ice", "Fairy", "Ghost"), stony = Any(types, "Rock", "Ground", "Steel");
            switch (family)
            {
                case BodyFamily.Insect:
                    return size < Small ? "Megascarab" : size < Medium ? "Spelopede" : "Megaspider";
                case BodyFamily.Bird:
                    if (size < Small) return water ? "Duck" : "Chicken";
                    return "Goose";
                case BodyFamily.WingedBeast:
                    return size < Small ? "Rat" : size < Large ? "Warg" : "Thrumbo";
                case BodyFamily.Snake:
                    if (size < Large) return "Cobra";
                    return stony ? "Megasloth" : "Thrumbo";
                case BodyFamily.Fish:
                case BodyFamily.Tentacle:
                    return size >= Giant ? "Elephant" : "Tortoise";
                case BodyFamily.Hooved:
                    if (size >= Giant) return "Elephant";
                    if (size >= Small && tough || size >= Large) return "Muffalo";
                    return size < Small ? "Goat" : size < Medium ? "Deer" : "Horse";
                case BodyFamily.Paws:
                    if (size >= Giant) return "Thrumbo";
                    if (size >= Large) return Any(types, "Ice") ? "Bear_Polar" : "Bear_Grizzly";
                    if (size >= Medium) return fierce || uncanny ? "Warg" : "Bear_Grizzly";
                    if (size >= Small) return Any(types, "Grass") && !fierce ? "Capybara" : fierce || tough ? "Wolf_Timber" : "Husky";
                    if (Any(types, "Grass", "Water", "Poison", "Bug") && !fierce && !uncanny) return "Capybara";
                    if (size >= Tiny) return uncanny ? "Cat" : "YorkshireTerrier";
                    return fierce ? "YorkshireTerrier" : "Squirrel";
                case BodyFamily.Biped:
                    if (size >= Large) return fierce || stony ? "Thrumbo" : "Bear_Grizzly";
                    if (size >= Medium) return fierce ? "Warg" : tough ? "Bear_Grizzly" : "Monkey";
                    if (water) return "Duck";
                    if (fierce) return "Iguana";
                    if (size >= Small && Any(types, "Fighting", "Steel")) return "Wolf_Timber";
                    return size < Tiny && !tough ? "Squirrel" : "Monkey";
                default:
                    if (Any(types, Quiet)) return "Tortoise";
                    if (Any(types, "Flying")) return size < Small ? "Chicken" : "Goose";
                    if (size >= Small) return fierce ? "Warg" : "Monkey";
                    return Any(types, "Fire", "Normal") ? "Chicken" : "Squirrel";
            }
        }

        /// <summary>
        /// The base game's animals whose grown ones call (RimWorld 1.6's Core defs, read 7 November 2026): the only ones
        /// <see cref="Voice"/> names. Missing here on purpose: the cougar, the lynx and the panther (an angry snarl only),
        /// the foxes (no sounds), and the turkey, the emu, the cassowary and the ostrich (only their chicks call).
        /// </summary>
        public static readonly string[] GrownCallers =
        {
            "Alpaca", "Alphabeaver", "Bear_Grizzly", "Bear_Polar", "Bison", "Boomalope", "Boomrat", "Capybara", "Caribou", "Cat", "Chicken", "Chinchilla", "Cobra",
            "Cow", "Deer", "Donkey", "Dromedary", "Duck", "Elephant", "Elk", "Gazelle", "Goat", "Goose", "GuineaPig", "Hare", "Horse", "Husky", "Ibex", "Iguana",
            "LabradorRetriever", "Megascarab", "Megasloth", "Megaspider", "Monkey", "Muffalo", "Pig", "Raccoon", "Rat", "Rhinoceros", "Sheep", "Snowhare",
            "Spelopede", "Squirrel", "Thrumbo", "Tortoise", "Warg", "WildBoar", "Wolf_Arctic", "Wolf_Timber", "Yak", "YorkshireTerrier"
        };

        /// <summary>
        /// The voices a species may borrow, best first. Each group holds animals that fit it as well as each other
        /// (<see cref="Pick"/> chooses among the ones loaded by the species' name), and the last group is the base-game
        /// animal <see cref="Voice"/> names, so with none of the other mods the voice is the same as before. Another mod's
        /// animal is named only where it is nearer to what the species is than the base game's, and only one whose grown
        /// ones have a call of their own, not one of the base game's calls again (checked on the player's mod list, 7
        /// November 2026: many modded animals reuse the game's calls, Megafauna's and Dinosauria's among them, and the
        /// demigryphs have no grown call). From Alpha Animals: the bumbledrone, a wasp, for the winged bugs; the meadow
        /// ave, a great flightless bird, for the bigger birds that aren't water birds (those keep the goose) and the bird
        /// heads; the ocular jelly for the tentacled ones; the needlepost, a cactus, for the plant heads and plant
        /// tentacles; the wildpawn, a plant beast, for the big pawed plant ones. From Vanilla Animals Expanded: the zebra
        /// for the electric hooved ones (Blitzle and Zebstrika are zebras), its Endangered pack's quagga after it; the
        /// crocodile for the big two-legged water beasts. From ReGrowth: the rimclaw and the arctic drak for the big
        /// two-legged dragons. And where a modded animal fits as well as the base game's, the species share them for
        /// variety: the cobra and the rattlesnake for the poison and ground snakes, the iguana and the velociraptor for
        /// the small two-legged dragons, the timber wolf and the coyote for the middling fierce or tough pawed ones, the
        /// warg and a lion (Vanilla Animals Expanded's, or Alpha Animals' arctic one) for the big fierce ones, the muffalo
        /// and the wildebeest for the big tough hooved ones. Never one that screams like a person or sounds like a machine.
        /// </summary>
        public static List<string[]> Voices(string body, float size, ICollection<string> types)
        {
            var family = FamilyOf(body);
            var voices = new List<string[]>(Modded(family, body, size, types)) { new[] { Voice(family, size, types) } };
            return voices;
        }

        // The other mods' animals (their def names), each with a call of its own.
        private static readonly string[] Wasp = { "AA_Bumbledrone" }, Ave = { "AA_MeadowAve" }, Jelly = { "AA_OcularJelly" }, Plant = { "AA_Needlepost" },
            PlantBeast = { "AA_Wildpawn" }, Zebra = { "AEXP_Zebra" }, Quagga = { "AEXP_Quagga" }, Crocodile = { "AEXP_Crocodile" };

        private static IEnumerable<string[]> Modded(BodyFamily family, string body, float size, ICollection<string> types)
        {
            bool water = Any(types, "Water"), fierce = Any(types, "Fire", "Dark", "Dragon"), tough = Any(types, "Normal", "Fighting", "Ground", "Rock", "Steel");
            bool uncanny = Any(types, "Psychic", "Electric", "Ice", "Fairy", "Ghost"), grass = Any(types, "Grass"), dragon = Any(types, "Dragon");
            switch (family)
            {
                case BodyFamily.Insect:
                    if (Any(types, "Bug") && (body ?? "").IndexOf("wing", StringComparison.OrdinalIgnoreCase) >= 0) yield return Wasp;
                    break;
                case BodyFamily.Bird:
                    if (size >= Small && !water) yield return Ave;
                    break;
                case BodyFamily.Snake:
                    if (size < Large && Any(types, "Poison", "Ground") && !water && !Any(types, "Bug")) yield return new[] { "Cobra", "AEXP_Rattlesnake" };
                    break;
                case BodyFamily.Tentacle:
                    if (size < Giant) yield return grass ? Plant : Jelly;
                    break;
                case BodyFamily.Hooved:
                    if (size >= Giant) break;
                    if (Any(types, "Electric"))
                    {
                        yield return Zebra;
                        yield return Quagga;
                    }
                    else if (size >= Small && tough || size >= Large) yield return new[] { "Muffalo", "AEXP_Wildebeest" };
                    break;
                case BodyFamily.Paws:
                    if (size >= Giant || size < Small) break;
                    if (size >= Medium && grass && !fierce && !uncanny) yield return PlantBeast;
                    else if (size >= Medium && size < Large && (fierce || uncanny)) yield return new[] { "Warg", "AEXP_Lion", "AA_ArcticLion" };
                    else if (size < Medium && !(grass && !fierce) && (fierce || tough)) yield return new[] { "Wolf_Timber", "AEXP_Coyote" };
                    break;
                case BodyFamily.Biped:
                    if (size >= Large) break;
                    if (size >= Medium && dragon) yield return new[] { "RG_Rimclaw", "RG_ArcticDrak" };
                    else if (size >= Medium && water && !fierce && !tough && !uncanny) yield return Crocodile;
                    else if (size < Medium && dragon && !water) yield return new[] { "Iguana", "RG_Velociraptor" };
                    break;
                case BodyFamily.WingedBeast:
                case BodyFamily.Fish:
                    break;
                default:
                    if (Any(types, "Bug") && Any(types, "Flying")) yield return Wasp;
                    else if (grass) yield return Plant;
                    else if (Any(types, "Flying") && size >= Small && !Any(types, Quiet)) yield return Ave;
                    break;
            }
        }

        /// <summary>
        /// The voice a species takes: from the first group of <paramref name="voices"/> with an animal that <paramref name="calls"/>
        /// (loaded, its grown ones calling), the one its name points to among them, so the same species always takes the same
        /// one while the same mods are loaded. Null when none calls.
        /// </summary>
        public static string Pick(IEnumerable<string[]> voices, Func<string, bool> calls, string species)
        {
            if (voices == null || calls == null) return null;
            foreach (var group in voices)
            {
                var callers = group?.Where(n => !string.IsNullOrEmpty(n) && calls(n)).ToList();
                if (callers == null || callers.Count == 0) continue;
                return callers[(int)(Stable(species) % (uint)callers.Count)];
            }
            return null;
        }

        /// <summary>A number from a name that is the same every time and on every machine (FNV-1a over its characters).</summary>
        public static uint Stable(string name)
        {
            uint h = 2166136261;
            foreach (char c in name ?? "")
            {
                h ^= c;
                h *= 16777619;
            }
            return h;
        }

        /// <summary>Whether a def name is one of the base game's animals the rule names (else another mod's).</summary>
        public static bool IsBase(string name) => Array.IndexOf(GrownCallers, name) >= 0;

        /// <summary>
        /// How far an animal is from a Pokémon, lower being nearer, for when the animal <see cref="Voice"/> names is missing:
        /// another family counts 2, another flesh 1 (an insect for a bug), and the sizes count by how many times bigger one
        /// is (the log of the ratio).
        /// </summary>
        public static float Distance(bool sameFamily, bool sameFlesh, float pokemonSize, float animalSize)
        {
            float a = Math.Max(MinSize, pokemonSize), b = Math.Max(MinSize, animalSize);
            return (sameFamily ? 0f : 2f) + (sameFlesh ? 0f : 1f) + (float)Math.Abs(Math.Log(a / b));
        }

        /// <summary>
        /// Which of the animal's life stages a Pokémon's stage borrows from: first to first and last to last, the ones between
        /// spread over the animal's; with one stage of its own, the animal's grown one. -1 when the animal has none.
        /// </summary>
        public static int StageFor(int index, int count, int animalCount)
        {
            if (animalCount <= 0) return -1;
            if (count <= 1) return animalCount - 1;
            int i = Math.Max(0, Math.Min(count - 1, index));
            return (int)Math.Round(i * (animalCount - 1) / (double)(count - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>The time between calls with <see cref="PokemonSounds.Rarer"/>.</summary>
        public static int Rarer(int ticks) => ticks <= 0 ? ticks : ticks * RarerFactor;

        /// <summary>The slower of two paces between calls (never more often than PokéWorld had it: its 3,000 to 6,000 ticks are longer than most animals').</summary>
        public static (int min, int max) Slower(int ownMin, int ownMax, int otherMin, int otherMax) => (Math.Max(ownMin, otherMin), Math.Max(ownMax, otherMax));

        /// <summary>The slower of two factors for how much sooner an angry or tamed animal calls (a bigger factor is slower).</summary>
        public static float SlowerFactor(float own, float other) => Math.Max(own, other);
    }
}
