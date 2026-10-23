using System;

namespace ColonyCouncil
{
    /// <summary>How an evening of faith went: nothing much, a good evening, or a fine one.</summary>
    public enum EveningTier
    {
        Poor,
        Good,
        Fine
    }

    /// <summary>
    /// The council's side of faith in the colony, in numbers and free of game types so Tools/Tests can cover it: who
    /// comes to an evening of faith and how it goes, how far it moves those who came, how often the council can call
    /// one, the wish to learn another faith, the decree that names one faith the colony's, and when faiths clash. Every
    /// move of certainty still goes through <see cref="Faiths"/> (the listener's openness, pride and opinion of the
    /// speaker on top, capped per moment by <see cref="FaithMath.MaxSway"/>).
    /// </summary>
    public static class FaithCouncilMath
    {
        // ---------------- An evening of faith ----------------

        /// <summary>At most one evening of faith in the colony in this many days.</summary>
        public const float EveningGapDays = 5f;

        /// <summary>The same faith holds one at most this often.</summary>
        public const float FaithEveningGapDays = 10f;

        /// <summary>Openness at which someone counts as open-minded (Persona: where a side starts to show).</summary>
        public const float OpenFrom = 30f;

        /// <summary>
        /// The chance that a colonist of another faith comes to hear an evening of faith: a third to start with, more for
        /// the open (openness -100 to 100) and for those who like the speaker (opinion -100 to 100), a good deal more for
        /// the wavering, less for the devout. A faith that frowns on being preached to keeps its people away; believers
        /// of the speaker's own faith always come.
        /// </summary>
        public static float ComeChance(bool sameFaith, bool dislikesPreaching, float openness, float opinion, float certainty)
        {
            if (sameFaith) return 1f;
            if (dislikesPreaching) return 0f;
            float c = 0.35f + openness / 200f + opinion / 200f;
            if (FaithMath.Wavering(certainty)) c += 0.25f;
            else if (FaithMath.Devout(certainty)) c -= 0.15f;
            return Clamp(c, 0.05f, 0.95f);
        }

        /// <summary>
        /// How good an evening was, 0 to 1: the speaker's Social skill (0 to 20) most of all, their warmth (-100 to 100),
        /// how many came to listen (up to six count), and the faith's own altar or ideogram to speak at. Nothing without
        /// the speaker or anyone to hear them; broken off early, half.
        /// </summary>
        public static float EveningQuality(int social, float warmth, int listeners, bool atAltar, bool speakerThere, bool early)
        {
            if (!speakerThere || listeners <= 0) return 0f;
            float q = Clamp(social, 0, 20) / 20f * 0.55f + Clamp(warmth, -100f, 100f) / 100f * 0.15f + Math.Min(listeners, 6) / 6f * 0.2f + (atAltar ? 0.1f : 0f);
            if (early) q *= 0.5f;
            return Clamp(q, 0f, 1f);
        }

        /// <summary>Below this an evening comes to nothing much; from <see cref="FineFrom"/> it was a fine one.</summary>
        public const float GoodFrom = 0.3f, FineFrom = 0.65f;

        public static EveningTier Tier(float quality) => quality >= FineFrom ? EveningTier.Fine : quality >= GoodFrom ? EveningTier.Good : EveningTier.Poor;

        /// <summary>
        /// The base sway for a listener of another faith, before their openness, pride and opinion of the speaker: 6% at a
        /// barely good evening up to 12% at the best; a poor evening moves nobody.
        /// </summary>
        public static float EveningSway(float quality)
        {
            if (Tier(quality) == EveningTier.Poor) return 0f;
            return 0.06f + 0.06f * Clamp((quality - GoodFrom) / (1f - GoodFrom), 0f, 1f);
        }

        /// <summary>Certainty given back to the speaker's own believers who came: 2% to 6% by how good it was; nothing at a poor one.</summary>
        public static float EveningSteady(float quality) => Tier(quality) == EveningTier.Poor ? 0f : 0.02f + 0.04f * Clamp(quality, 0f, 1f);

        /// <summary>Whether the colony may call an evening now: days since the last one anywhere, and since this faith's.</summary>
        public static bool EveningAllowed(float daysSinceAny, float daysSinceFaith) => daysSinceAny >= EveningGapDays && daysSinceFaith >= FaithEveningGapDays;

        // ---------------- A wavering colonist's wish ----------------

        /// <summary>The base sway of an evening spent with a believer of another faith, at the wavering colonist's own asking.</summary>
        public const float WishSway = 0.12f;

        /// <summary>
        /// How much a wavering colonist wants to learn another faith (the wish's weight): more the less sure they are of
        /// their own, more the better they think of the believer (opinion 20 to 100). Nothing unless wavering.
        /// </summary>
        public static float WishWeight(float certainty, float opinion)
        {
            if (!FaithMath.Wavering(certainty) || opinion < 20f) return 0f;
            return 0.6f + (FaithMath.WaveringBelow - Clamp(certainty, 0f, 1f)) * 2f + Clamp(opinion - 20f, 0f, 80f) / 160f;
        }

        // ---------------- One faith for the colony (a decree) ----------------
        // Its pull on everyone of another faith is their leaning toward the named one (LeaningMath.Decree).

        /// <summary>
        /// Trust lost a day by a colonist of another faith while one faith is the colony's: the open-minded a quarter
        /// point, most people half a point, the devout three quarters, and a faith that frowns on being preached to a
        /// whole point.
        /// </summary>
        public static float DecreeTrustPerDay(float openness, float certainty, bool dislikesPreaching)
        {
            if (dislikesPreaching) return -1f;
            if (FaithMath.Devout(certainty)) return -0.75f;
            if (openness >= OpenFrom) return -0.25f;
            return -0.5f;
        }

        /// <summary>Believers of the colony's named faith start talks about it this much more often (read by the faith talks).</summary>
        public const float DecreeTalkFactor = 1.5f;

        // ---------------- When faiths clash (a council question) ----------------

        /// <summary>
        /// The colony is split enough for its faiths to clash: two or more faiths each with two believers or more, or a
        /// split of one half or more (<see cref="FaithMath.Split"/>).
        /// </summary>
        public static bool Clashing(int faithsWithTwo, float split) => faithsWithTwo >= 2 || split >= 0.5f;

        /// <summary>
        /// How likely the question is to come up when the council's schedule has room for it: half the time in a quiet
        /// split colony, always after believers of different faiths have quarrelled lately or while a faith's altar
        /// stands.
        /// </summary>
        public static float ClashChance(bool quarrels, bool altar) => quarrels || altar ? 1f : 0.5f;

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
