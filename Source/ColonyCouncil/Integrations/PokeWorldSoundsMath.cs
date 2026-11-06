using System;

namespace ColonyCouncil
{
    /// <summary>What PokéWorld's Pokémon sound like (the player's request, 6 November 2026: "the constant pokemon screetches" don't fit).</summary>
    public enum PokemonSounds
    {
        /// <summary>The calls of the nearest animal of the game or another mod: the same body plan and flesh, the nearest size, and its pace.</summary>
        Creatures,
        /// <summary>Their own cries, a third as often.</summary>
        Rarer,
        /// <summary>As PokéWorld has them.</summary>
        Own
    }

    /// <summary>The pure rules behind <see cref="PokeWorldSounds"/>: which animal a Pokémon borrows its voice from, and the rarer pace.</summary>
    public static class PokeWorldSoundsMath
    {
        /// <summary>With <see cref="PokemonSounds.Rarer"/>, the time between calls is this many times PokéWorld's.</summary>
        public const int RarerFactor = 3;

        /// <summary>Smallest body size counted, so a tiny creature's ratio stays sane.</summary>
        public const float MinSize = 0.05f;

        /// <summary>
        /// How far an animal is from a Pokémon, lower being nearer: another body plan counts 2, another flesh 1 (an insect for
        /// a bug, a bird's body for a bird), and the sizes count by how many times bigger one is (the log of the ratio).
        /// </summary>
        public static float Distance(bool sameBody, bool sameFlesh, float pokemonSize, float animalSize)
        {
            float a = Math.Max(MinSize, pokemonSize), b = Math.Max(MinSize, animalSize);
            return (sameBody ? 0f : 2f) + (sameFlesh ? 0f : 1f) + (float)Math.Abs(Math.Log(a / b));
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
    }
}
