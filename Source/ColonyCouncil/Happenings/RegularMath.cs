using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Regulars: visitors who come back (Happenings/Regulars.cs), as plain numbers (Tools/Tests/RegularTests.cs).
    /// A visitor the colony dealt with is remembered; after a few weeks they may come back, the thread between them
    /// and the colonist they know growing a little with each warm visit until it turns: they ask to stay, or ask
    /// their friend to come along.
    /// </summary>
    public static class RegularMath
    {
        /// <summary>How many visitors the colony keeps up with at most.</summary>
        public const int KeepMax = 5;
        /// <summary>No return sooner than this after a visit; forgotten after this long without one.</summary>
        public const float ReturnAfterDays = 20f;
        public const float FadeDays = 160f;
        /// <summary>What a warm answer adds to the bond, and a cold one takes.</summary>
        public const int Warm = 1, Cold = -1;

        /// <summary>Due back: a few weeks since the last visit, and not forgotten yet.</summary>
        public static bool Due(float daysSince) => daysSince >= ReturnAfterDays && daysSince <= FadeDays;

        /// <summary>The thread turns after three visits with the bond grown to four: they ask to stay, or ask their friend along.</summary>
        public static bool Turning(int bond, int visits) => bond >= 4 && visits >= 3;

        /// <summary>How the bond moves with an answer: a cold answer (one that closes the colony a little) takes from it, any other adds.</summary>
        public static int BondChange(float openness) => openness < 0f ? Cold : Warm;

        /// <summary>Which regular comes first when several are due: the closest bond, then the longest away.</summary>
        public static int Compare(int bondA, float daysA, int bondB, float daysB)
        {
            if (bondA != bondB) return bondB.CompareTo(bondA);
            return daysB.CompareTo(daysA);
        }

        /// <summary>Who makes way when there are too many: the faintest bond, then the longest unseen.</summary>
        public static int Weakest(int[] bonds, float[] daysSince)
        {
            int best = -1;
            for (int i = 0; i < bonds.Length; i++)
                if (best < 0 || bonds[i] < bonds[best] || (bonds[i] == bonds[best] && daysSince[i] > daysSince[best])) best = i;
            return best;
        }
    }
}
