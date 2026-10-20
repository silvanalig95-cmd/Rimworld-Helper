using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of faith in the colony (Faith/), free of game types so Tools/Tests can cover it: when someone counts
    /// as devout or wavering, how far a word from someone of another faith moves them, and how split a colony is. The
    /// game keeps the certainty itself (0 to 1) and converts someone when it reaches 0; Rimfolk only decides how much a
    /// moment takes off or gives back.
    /// </summary>
    public static class FaithMath
    {
        /// <summary>Below this certainty someone is wavering: open to another faith, and showing it.</summary>
        public const float WaveringBelow = 0.6f;

        /// <summary>From this certainty someone is devout: the one who speaks for their faith.</summary>
        public const float DevoutFrom = 0.85f;

        /// <summary>No single moment takes more than this off someone's certainty (the game's own convert ability takes up to about a fifth).</summary>
        public const float MaxSway = 0.15f;

        public static bool Wavering(float certainty) => certainty < WaveringBelow;

        public static bool Devout(float certainty) => certainty >= DevoutFrom;

        /// <summary>
        /// How much a word from someone of another faith takes off a listener's certainty, before the game's own
        /// certainty-loss factor: the moment's base, the speaker's conversion power (the game's stat, 1 for most), and the
        /// listener's openness (-100 to 100: the open are moved up to half again as much, the closed half as much), pride
        /// (the proud give less ground), opinion of the speaker (-100 to 100: a friend is heard, a rival less) and whether
        /// their own faith frowns on being preached to (half as much). Never more than <see cref="MaxSway"/>.
        /// </summary>
        public static float Sway(float baseAmount, float conversionPower, float openness, float pride, float opinion, bool dislikesPreaching)
        {
            if (baseAmount <= 0f || conversionPower <= 0f) return 0f;
            float open = Clamp(1f + openness / 200f, 0.5f, 1.5f);
            float proud = Clamp(1f - Math.Max(0f, pride) / 250f, 0.6f, 1f);
            float liked = Clamp(1f + opinion / 200f, 0.6f, 1.5f);
            float v = baseAmount * Clamp(conversionPower, 0f, 3f) * open * proud * liked * (dislikesPreaching ? 0.5f : 1f);
            return Clamp(v, 0f, MaxSway);
        }

        /// <summary>How split a colony is: 0 with one faith, 1 when everyone holds a different one.</summary>
        public static float Split(int faiths, int people) => people <= 1 || faiths <= 1 ? 0f : Clamp((faiths - 1f) / (people - 1f), 0f, 1f);

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
