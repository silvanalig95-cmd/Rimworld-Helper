using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's suggestions (Building/Suggest/Suggestions.cs) as plain rules: the order they come in (the soonest
    /// short first, then by level), how long a "Not this" holds, when a step that was done is checked and what the
    /// check makes of it, and how much is kept. Pure, so Tools/Tests can check them.
    /// </summary>
    public static class SuggestMath
    {
        public const int TicksPerDay = 60000;
        /// <summary>A suggestion that isn't about time running out.</summary>
        public const float NoRush = 999f;

        /// <summary>What became of a step that was done: still to be checked, it worked, it didn't, it was undone, or nothing to check.</summary>
        public const int Waiting = 0, Worked = 1, Failed = 2, Undone = 3, NoCheck = 4;

        /// <summary>Steps kept in the log, and suggestions set aside remembered.</summary>
        public const int DoneKept = 20, DeclinedKept = 80;
        /// <summary>A step that hasn't worked at its check is looked at this many times in all before it is called stuck.</summary>
        public const int Tries = 2;

        /// <summary>
        /// The order suggestions come in: the soonest something runs short first; those not about time by their level
        /// (4 critical to 0 an idea), highest first; then by key, so the order holds from hour to hour.
        /// </summary>
        public static int Compare(float daysA, int levelA, string keyA, float daysB, int levelB, string keyB)
        {
            int c = Math.Min(daysA, NoRush).CompareTo(Math.Min(daysB, NoRush));
            if (c != 0) return c;
            c = levelB.CompareTo(levelA);
            return c != 0 ? c : string.CompareOrdinal(keyA ?? "", keyB ?? "");
        }

        /// <summary>A level for a number of days until short (for colours and for ordering among other kinds): 4 within half a day, 0 beyond a fortnight.</summary>
        public static int LevelOf(float days) => days <= 0.5f ? 4 : days <= 2f ? 3 : days <= 5f ? 2 : days <= 15f ? 1 : 0;

        /// <summary>The tick a "Not this" holds until: -1 for good (until brought back), else so many days on.</summary>
        public static int DeclineUntil(int now, float days) => days <= 0f ? -1 : now + (int)(days * TicksPerDay);

        /// <summary>A "Not this" still holds: for good, or its days not yet passed.</summary>
        public static bool StillDeclined(int until, int now) => until < 0 || now < until;

        /// <summary>A done step is due its next look: its wait after the doing, once for each look already taken.</summary>
        public static bool CheckDue(int doneTick, int waitTicks, int tries, int now) => now >= doneTick + Math.Max(1, waitTicks) * (tries + 1);

        /// <summary>What a look makes of a step: it worked; not yet (another look later); or, after the last look, it didn't.</summary>
        public static int AfterLook(bool worked, int triesSoFar) => worked ? Worked : triesSoFar + 1 >= Tries ? Failed : Waiting;

        /// <summary>Lets the oldest go past a cap; how many went.</summary>
        public static int TrimOldest<T>(List<T> list, int cap)
        {
            if (list == null) return 0;
            int n = Math.Max(0, list.Count - Math.Max(0, cap));
            if (n > 0) list.RemoveRange(0, n);
            return n;
        }
    }
}
