using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of good times turning ordinary (GoodTimes.cs, SmallJoys.cs), free of game types so the tests can
    /// cover it (Tools/Tests): how much sooner a good memory fades when life is already good, how much of the game's own
    /// comforts a colonist with high standards still feels, and how good moods count when several are on someone's mind.
    /// </summary>
    public static class GoodTimesMath
    {
        // ---------------- Getting used to good times ----------------

        /// <summary>Up to this mood a new good memory lasts its full time.</summary>
        public const float FadeFrom = 0.75f;

        /// <summary>From this mood on it lasts <see cref="FadeMost"/> of its time.</summary>
        public const float FadeFull = 0.9f;

        /// <summary>The shortest a good memory gets, as a share of its full time.</summary>
        public const float FadeMost = 0.5f;

        /// <summary>
        /// How long a new good memory lasts at this mood (0 to 1), as a share of its full time: all of it up to 75%,
        /// then smoothly less, down to half from 90% on.
        /// </summary>
        public static float DurationFactor(float mood)
        {
            if (float.IsNaN(mood) || mood <= FadeFrom) return 1f;
            if (mood >= FadeFull) return FadeMost;
            float t = (mood - FadeFrom) / (FadeFull - FadeFrom);
            float smooth = t * t * (3f - 2f * t);
            return 1f - (1f - FadeMost) * smooth;
        }

        /// <summary>A memory's time in ticks at this factor, never less than one tick.</summary>
        public static int ShortenedTicks(int ticks, float factor)
        {
            if (ticks <= 0 || factor >= 1f) return ticks;
            return Math.Max(1, (int)Math.Round(ticks * Math.Max(0f, factor)));
        }

        // ---------------- Standards and the game's comforts ----------------

        /// <summary>
        /// Only this much of any one comfort counts for less; whatever is above it counts in full, so the big moments of
        /// a life (a wedding, a catharsis) still land.
        /// </summary>
        public const float ComfortScaledUpTo = 10f;

        /// <summary>How much of the game's comforts a colonist feels at each standards level (0 modest to 3 spoiled).</summary>
        public static float ComfortFactor(int level) => level <= 0 ? 1f : level == 1 ? 0.9f : level == 2 ? 0.8f : 0.7f;

        /// <summary>A good mood from the game or another mod, as felt at this factor: its first 10 points count for less.</summary>
        public static float Comfort(float offset, float factor)
        {
            if (offset <= 0f || factor >= 1f) return offset;
            float scaled = Math.Min(offset, ComfortScaledUpTo);
            return scaled * Math.Max(0f, factor) + (offset - scaled);
        }

        // ---------------- Good moods add up slowly ----------------

        /// <summary>
        /// How much a good mood counts by its place among the colonist's good moods, biggest first (0 is the biggest):
        /// the first two in full, each one after that half as much as the one before.
        /// </summary>
        public static float RankFactor(int rank)
        {
            if (rank < 2) return 1f;
            if (rank > 40) return 0f;
            return (float)Math.Pow(0.5, rank - 1);
        }
    }
}
