using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Families across the years (Social/FamilyYears.cs): how many years someone spent in cryptosleep (their calendar age
    /// beyond the years they lived), how many generations a relative is above or below them by the game's relation, and
    /// whether their ages run the wrong way round (a mother younger than her son, a grandson older than his grandmother).
    /// Only the direct line counts: an uncle younger than his nephew is just a late brother. No game types, so
    /// Tools/Tests checks it.
    /// </summary>
    public static class FamilyYearsMath
    {
        /// <summary>Years asleep worth a word: two or more (a few months in a pod are nobody's story).</summary>
        public const int SleptAtLeast = 2;

        /// <summary>Two people of the same family may come back to it this many days after the last time at the soonest.</summary>
        public const float RemarkDays = 10f;

        /// <summary>The chance a day that a pair whose time has come starts looking for the moment.</summary>
        public const float RemarkChance = 0.2f;

        /// <summary>Years spent in cryptosleep: the calendar years beyond those lived (never less than none).</summary>
        public static int Slept(int biological, int chronological) => Math.Max(0, chronological - biological);

        public static bool HasSlept(int biological, int chronological) => Slept(biological, chronological) >= SleptAtLeast;

        /// <summary>
        /// How many generations above (positive) or below (negative) someone a relative of theirs is, by the game's
        /// relation from their side (Parent: their mother or father, one up). 0 for anyone not in their direct line:
        /// siblings, cousins, uncles and aunts, nephews and nieces, in-laws, partners.
        /// </summary>
        public static int Generations(string relation)
        {
            switch (relation)
            {
                case "Parent": return 1;
                case "Grandparent": return 2;
                case "GreatGrandparent": return 3;
                case "Child": return -1;
                case "Grandchild": return -2;
                case "GreatGrandchild": return -3;
                default: return 0;
            }
        }

        /// <summary>
        /// The ages run the wrong way round, by a year or more: someone above in the line is younger in years lived, or
        /// someone below is older. The one above must have slept for it (they were grown when the one below was born).
        /// </summary>
        public static bool UpsideDown(int generations, int mine, int theirs) =>
            generations > 0 ? mine - theirs >= 1 : generations < 0 && theirs - mine >= 1;

        /// <summary>Which of the two speaks as what: "elder" for the one above in the line, "younger" for the one below.</summary>
        public static string Side(int generationsOfListener) => generationsOfListener < 0 ? "elder" : "younger";

        /// <summary>Their time has come round again: never spoken of, or <see cref="RemarkDays"/> or more since.</summary>
        public static bool RemarkDue(int lastTick, int now, int ticksPerDay) => lastTick < 0 || now < lastTick || now - lastTick >= RemarkDays * ticksPerDay;

        /// <summary>The same key for a pair whichever way round they're asked about.</summary>
        public static string PairKey(int a, int b) => a < b ? a + "|" + b : b + "|" + a;
    }
}
