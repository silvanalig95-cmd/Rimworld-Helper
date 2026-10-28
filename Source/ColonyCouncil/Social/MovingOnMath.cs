using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The price of keeping someone who asked to move on (Social/MovingOn.cs), free of game types so Tools/Tests can cover
    /// it. How much they wanted to go (0 to 1) sets the price: the trust lost when the council asks them to stay, and the
    /// longing they carry while kept, which grows over the first weeks and lasts until what they wanted comes to them
    /// (their family here, their partner with them, a room of their own) or, after a season, they make their peace with
    /// it. A promised room is a promise: kept in time it is remembered, broken it costs again. Unhappy, or wanting it
    /// badly, they ask once more after a few weeks, and keeping them then costs more.
    /// </summary>
    public static class MovingOnMath
    {
        /// <summary>Days kept before they may ask a second time (a partner's next visit counts from <see cref="AgainLoveDays"/>).</summary>
        public const int AgainDays = 20, AgainLoveDays = 10;

        /// <summary>They ask a second time when their mood is under this, or they wanted it this badly.</summary>
        public const float AgainMood = 0.45f, AgainStrength = 0.7f;

        /// <summary>Days a promised room has to be theirs.</summary>
        public const int PromiseDays = 15;

        /// <summary>Trust a broken promise of a room costs, and a kept one gives back.</summary>
        public const float BrokenPromiseTrust = 10f, KeptPromiseTrust = 3f;

        /// <summary>After this many days kept they make their peace with it.</summary>
        public const int PeaceDays = 60;

        /// <summary>Days of longing until it is at its full weight (it starts at half).</summary>
        public const float RampDays = 20f;

        /// <summary>A parent, brother or sister grown old, or a child of theirs: by how much they think of them, more for a child.</summary>
        public static float Family(float opinion, bool child) => Clamp01(0.3f + Clamp01((opinion - 20f) / 80f) * 0.6f + (child ? 0.2f : 0f));

        /// <summary>A partner from outside: a spouse most, then the betrothed, then a lover, a little more the more they love them.</summary>
        public static float Love(bool married, bool engaged, float opinion) => Clamp01((married ? 0.85f : engaged ? 0.7f : 0.55f) + Clamp01(opinion / 100f) * 0.15f);

        /// <summary>No room: by how unhappy they are (mood under 35% is the threshold to ask at all).</summary>
        public static float Room(float mood) => Clamp01(0.3f + Clamp01((0.35f - mood) / 0.35f) * 0.7f);

        /// <summary>Trust lost asking them to stay: the answer's base loss, from half for a mild wish to half again for a strong one.</summary>
        public static float TrustLoss(float baseLoss, float strength) => baseLoss * (0.5f + Clamp01(strength));

        /// <summary>How heavy their longing is now: their wish, from half to whole over <see cref="RampDays"/>.</summary>
        public static float Longing(float strength, float days) => Clamp01(strength) * Math.Min(1f, 0.5f + Math.Max(0f, days) / RampDays * 0.5f);

        /// <summary>Which of the longing's three stages they feel (a promised room not yet due only the first).</summary>
        public static int Stage(float strength, float days, bool promiseOpen)
        {
            if (promiseOpen) return 0;
            float l = Longing(strength, days);
            return l < 0.35f ? 0 : l < 0.7f ? 1 : 2;
        }

        /// <summary>They come to the council once more.</summary>
        public static bool AskAgain(float days, bool love, float mood, float strength, bool askedAgain, bool promiseOpen) =>
            !askedAgain && !promiseOpen && days >= (love ? AgainLoveDays : AgainDays) && (mood < AgainMood || strength >= AgainStrength);

        public static bool PromiseOpen(bool promised, float days) => promised && days < PromiseDays;

        public static bool PromiseBroken(bool promised, float days) => promised && days >= PromiseDays;

        public static bool MadePeace(float days) => days >= PeaceDays;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
