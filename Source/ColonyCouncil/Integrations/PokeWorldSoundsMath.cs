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
    /// The pure rules behind <see cref="PokeWorldSounds"/>. Each species borrows the voice of one of the base game's animals,
    /// picked by its body plan's family (<see cref="FamilyOf"/>), its types and its size (<see cref="Voice"/>). It never
    /// calls more often than PokéWorld had it (<see cref="Slower"/>). The game's own animals are always there, so the
    /// same species always sounds the same, and the full list can be made from PokéWorld's files alone. Where a named
    /// animal is missing, the nearest one of the same family and size is used instead (<see cref="Distance"/>).
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

        /// <summary>Types of the stones, gases, magnets and spirits: a head of one of these makes little sound.</summary>
        private static readonly string[] Quiet = { "Rock", "Steel", "Electric", "Ghost", "Poison", "Psychic", "Ground" };

        /// <summary>
        /// The base-game animal (its def name) a Pokémon borrows its voice from: by the family of its body, its types and its
        /// size. Insects buzz and click like the game's insects, birds cluck, honk or boom by size (ducks and geese for
        /// water birds), bats squeak like rats and winged dragons roar, snakes hiss like the cobra and the giant ones rumble,
        /// fish and tentacles take the tortoise's voice (the great whales low like an elephant), hooved ones bleat, call
        /// and low like goats, deer, horses and muffalo, pawed ones squeak, mew, yip, growl and roar by size and temper (the small grass and water ones grunt like a capybara),
        /// two-legged ones chatter like monkeys (the small water ones quack, the small fire and dragon ones hiss like an iguana)
        /// and growl or roar when big, the stones, balls and clouds take the tortoise's voice, and the other small heads with legs cheep like chicks or squeak.
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
                    if (water) return size < Small ? "Duck" : "Goose";
                    return size < Small ? "Chicken" : size < Medium ? "Turkey" : size < Large ? "Emu" : "Ostrich";
                case BodyFamily.WingedBeast:
                    return size < Small ? "Rat" : size < Medium ? "Cougar" : size < Large ? "Warg" : "Thrumbo";
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
                    if (size >= Medium) return fierce ? "Warg" : uncanny || Any(types, "Grass") ? "Cougar" : "Bear_Grizzly";
                    if (size >= Small) return uncanny || Any(types, "Grass") ? "Lynx" : fierce || tough ? "Wolf_Timber" : "Husky";
                    if (Any(types, "Grass", "Water", "Poison", "Bug") && !fierce && !uncanny) return "Capybara";
                    if (size >= Tiny) return uncanny ? "Cat" : fierce ? "Fox_Red" : "YorkshireTerrier";
                    return fierce ? "Fox_Red" : "Squirrel";
                case BodyFamily.Biped:
                    if (size >= Large) return fierce || stony ? "Thrumbo" : "Bear_Grizzly";
                    if (size >= Medium) return fierce ? "Warg" : tough ? "Bear_Grizzly" : uncanny ? "Cougar" : "Monkey";
                    if (water) return "Duck";
                    if (fierce) return "Iguana";
                    if (size >= Small && Any(types, "Fighting", "Steel")) return "Wolf_Timber";
                    return size < Tiny && !tough ? "Squirrel" : "Monkey";
                default:
                    if (Any(types, Quiet)) return "Tortoise";
                    if (size >= Small) return "Monkey";
                    return Any(types, "Fire", "Normal", "Flying") ? "Chicken" : "Squirrel";
            }
        }

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
