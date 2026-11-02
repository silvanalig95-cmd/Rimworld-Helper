using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure part of the second round of visitors' work (VisitorsMore2.cs): which kinds there are, the hours each is
    /// done in, and how long a visit must stay for a few hours of its work. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class VisitorWork2Math
    {
        /// <summary>
        /// Praying before the colony's relic, praying at the gate facing it, meditating at the anima tree, walking round
        /// the gravship's hull, reading at the colony's bookcase, swapping a book at it.
        /// </summary>
        public const string Relic = "relic", Pray = "pray", Meditate = "meditate", Gawk = "gawk", Read = "read", Swap = "swap";

        public static readonly string[] Kinds = { Relic, Pray, Meditate, Gawk, Read, Swap };

        /// <summary>Not a work: kept by the gate for something by day (a word with a colonist, a lesson), in the hours people are about.</summary>
        public const string Day = "day";

        public static bool Known(string work) => work != null && Array.IndexOf(Kinds, work) >= 0;

        /// <summary>The hours [from, to) a kind of work is done in: prayer and meditation from dawn to late evening, a look round the hull by daylight.</summary>
        public static bool Hours(string work, out int from, out int to)
        {
            switch (work)
            {
                case Relic:
                case Pray:
                    from = 6;
                    to = 22;
                    return true;
                case Meditate:
                    from = 5;
                    to = 23;
                    return true;
                case Gawk:
                    from = 7;
                    to = 19;
                    return true;
                case Read:
                case Swap:
                    from = 7;
                    to = 21;
                    return true;
                case Day:
                    from = 7;
                    to = 20;
                    return true;
                default:
                    from = to = 0;
                    return false;
            }
        }

        public static bool InHours(string work, int hour)
        {
            if (!Hours(work, out int from, out int to)) return false;
            hour = ((hour % 24) + 24) % 24;
            return hour >= from && hour < to;
        }

        /// <summary>The longest a visit is held for its work, whatever the hours.</summary>
        public const int MaxHours = 30;

        /// <summary>
        /// Hours from <paramref name="hour"/> until <paramref name="need"/> hours of the work's own hours have gone by
        /// (answered at night, they wait for the morning and work then), and one more for the walk; at most
        /// <see cref="MaxHours"/>. Nothing to do: none.
        /// </summary>
        public static int HoursToCover(string work, int hour, int need)
        {
            if (need <= 0 || !Hours(work, out _, out _)) return 0;
            int done = 0;
            for (int h = 0; h < MaxHours; h++)
            {
                if (InHours(work, hour + h)) done++;
                if (done >= need) return Math.Min(MaxHours, h + 2);
            }
            return MaxHours;
        }
    }

    /// <summary>The turns of a regular's own life (Happenings/Workers_Visitors2.cs), as plain numbers.</summary>
    public static class RegularLifeMath
    {
        /// <summary>Known well enough for their life to be the colony's news: three visits at least, and a warm bond.</summary>
        public static bool Warm(int visits, int bond) => visits >= 3 && bond >= 3;

        /// <summary>Old enough to hand their trade to an apprentice.</summary>
        public static bool Elder(float age) => age >= 50f;

        /// <summary>Old enough that a death on the road comes of age alone.</summary>
        public static bool Old(float age) => age >= 72f;
    }
}
