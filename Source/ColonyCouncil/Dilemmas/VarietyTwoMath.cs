using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the second round of event variety (Dilemmas/DilemmaWorkers_Variety2.cs,
    /// Happenings/Workers_Variety2.cs): when a weapon in the stores is worth taking up, how long a loss is fresh, how cold
    /// a cold snap must be to ask about, how much blight is worth the council's time, and how big a herd crossing the land
    /// is and how many of it the hunters may take. No game types, for the tests.
    /// </summary>
    public static class VarietyTwoMath
    {
        /// <summary>A weapon in the stores is a real step up when it's worth this much more than what someone carries.</summary>
        public const float UpgradeFactor = 1.3f;

        /// <summary>And at least this much more in silver (a fine knife isn't a step up from a plain one).</summary>
        public const float UpgradeMargin = 40f;

        /// <summary>
        /// Whether a weapon worth <paramref name="newValue"/> is a step up for someone who carries one worth
        /// <paramref name="currentValue"/>: anything is, for someone with none; else only a weapon of the same kind
        /// (a gun for a shooter, a blade for a fighter) worth clearly more.
        /// </summary>
        public static bool Upgrade(float newValue, float currentValue, bool hasWeapon, bool sameKind)
        {
            if (newValue <= 0f) return false;
            if (!hasWeapon) return true;
            if (!sameKind) return false;
            return newValue >= currentValue * UpgradeFactor && newValue - currentValue >= UpgradeMargin;
        }

        /// <summary>The skill that counts for taking up a weapon, at least this good: a weapon goes to someone who'll use it.</summary>
        public const int MinSkill = 4;

        /// <summary>A death is fresh enough for the council to be asked about the animal left behind, in days.</summary>
        public const float LossDays = 12f;

        /// <summary>Whether a death <paramref name="daysSince"/> days ago is still fresh (never one not yet known: below zero).</summary>
        public static bool RecentLoss(float daysSince) => daysSince >= 0f && daysSince <= LossDays;

        /// <summary>A cold snap is asked about only when it freezes outside, at most this many degrees.</summary>
        public const float FreezingAt = 2f;

        public static bool Freezing(float outdoorTemp) => outdoorTemp <= FreezingAt;

        /// <summary>A bedroom this cold is the one a fire is kept in.</summary>
        public const float ColdRoomAt = 12f;

        public static bool ColdRoom(float roomTemp) => roomTemp <= ColdRoomAt;

        /// <summary>Blight the council is asked about: at least this many plants of the colony's fields have it.</summary>
        public const int MinBlighted = 2;

        public static bool BlightWorthAsking(int blighted) => blighted >= MinBlighted;

        /// <summary>A herd crossing the land: at least this many of one kind walking across together.</summary>
        public const int MinHerd = 4;

        public static bool HerdBigEnough(int count) => count >= MinHerd;

        /// <summary>
        /// How many of a herd of <paramref name="herd"/> the hunters mark when the colony takes "a few": a third of the
        /// grown ones, at least two and at most four, never the whole herd.
        /// </summary>
        public static int FewOf(int herd)
        {
            if (herd < 2) return 0;
            int n = Math.Max(2, herd / 3);
            return Math.Min(Math.Min(n, 4), herd - 1);
        }
    }
}
