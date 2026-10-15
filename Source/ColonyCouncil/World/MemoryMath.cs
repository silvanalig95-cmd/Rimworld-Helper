using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of what the world's peoples remember of the colony (<see cref="FactionMemory"/>): how a deed fades
    /// over the years (the bigger it was, the slower), how a second deed of the same kind adds to the first, which deed
    /// makes way when a people's list is full, and how long ago something was, in words. Pure (no game types):
    /// Tools/Tests checks it.
    /// </summary>
    public static class MemoryMath
    {
        public const int TicksPerDay = 60000;
        /// <summary>The game's year: four quadrums of fifteen days.</summary>
        public const int TicksPerYear = TicksPerDay * 60;

        /// <summary>Deeds kept for each people; past that, the weakest makes way.</summary>
        public const int PerFaction = 12;

        /// <summary>The strongest a single deed can be (a town burned is 45).</summary>
        public const int MaxWeight = 60;

        /// <summary>A deed this faint is forgotten.</summary>
        public const float Forgotten = 1f;

        /// <summary>
        /// Years for a deed to fade to half: three quarters of a year, and a year more for every 12 points of weight. A small
        /// gift (6) is half forgotten in a year and a quarter; a prisoner set free (12) in a year and three quarters; a town
        /// burned (45) in four and a half years.
        /// </summary>
        public static float HalfLifeYears(int weight) => 0.75f + Math.Abs(weight) / 12f;

        /// <summary>How strongly a deed of this weight is remembered after <paramref name="ageTicks"/>.</summary>
        public static float Strength(int weight, int ageTicks)
        {
            if (weight == 0) return 0f;
            float years = Math.Max(0, ageTicks) / (float)TicksPerYear;
            return (float)(weight * Math.Pow(0.5, years / HalfLifeYears(weight)));
        }

        /// <summary>Years until a deed of this weight is forgotten (fades below <see cref="Forgotten"/>).</summary>
        public static float YearsRemembered(int weight)
        {
            int w = Math.Abs(weight);
            if (w <= Forgotten) return 0f;
            return (float)(HalfLifeYears(w) * Math.Log(w / Forgotten, 2.0));
        }

        /// <summary>
        /// The same kind of deed again soon after (another trade, more raiders fallen): what is left of the first, plus the
        /// new one, at most <paramref name="max"/> (and never past <see cref="MaxWeight"/>). The deed counts from now.
        /// </summary>
        public static int Merged(int oldWeight, int oldAgeTicks, int add, int max = MaxWeight)
        {
            float left = Strength(Math.Abs(oldWeight), oldAgeTicks);
            int cap = Math.Min(Math.Max(1, max), MaxWeight);
            return Math.Max(1, Math.Min(cap, (int)Math.Round(left + Math.Abs(add))));
        }

        /// <summary>
        /// Which deed makes way when a people's list is full: a settled one before any still owed, then the faintest. Returns
        /// its index, or -1 for an empty list.
        /// </summary>
        public static int Weakest(IList<float> strengths, IList<bool> settled)
        {
            int best = -1;
            for (int i = 0; i < strengths.Count; i++)
            {
                if (best < 0) best = i;
                else
                {
                    bool s = settled != null && i < settled.Count && settled[i];
                    bool bs = settled != null && best < settled.Count && settled[best];
                    if (s && !bs || s == bs && Math.Abs(strengths[i]) < Math.Abs(strengths[best])) best = i;
                }
            }
            return best;
        }

        /// <summary>How long ago, in the words the World page uses.</summary>
        public enum Ago
        {
            /// <summary>Within the last fifteen days (this quadrum or so).</summary>
            Lately,
            /// <summary>Days ago (the number is days).</summary>
            Days,
            /// <summary>About a year ago.</summary>
            LastYear,
            /// <summary>Years ago (the number is whole years).</summary>
            Years
        }

        public static Ago AgoOf(int ageTicks, out int n)
        {
            int days = Math.Max(0, ageTicks) / TicksPerDay;
            n = days;
            if (days < 15) return Ago.Lately;
            if (days < 60) return Ago.Days;
            int years = days / 60;
            n = years;
            return years <= 1 ? Ago.LastYear : Ago.Years;
        }

        /// <summary>
        /// How a people stands with the colony, from what it remembers: how grateful it is (the colony's good deeds), how
        /// much it holds against the colony, and how much the colony owes it. 0: nothing to speak of; 1: well disposed;
        /// 2: in the colony's debt; 3: wary; 4: holds a grudge; 5: both grateful and aggrieved; 6: the colony owes them.
        /// </summary>
        public static int Tone(float grateful, float grudge, float owed)
        {
            const float some = 4f, much = 15f;
            if (grateful >= much && grudge >= much) return 5;
            if (grudge >= much && grudge > grateful) return 4;
            if (grateful >= much) return 2;
            if (owed >= much && owed > grudge) return 6;
            if (grudge >= some && grudge > grateful) return 3;
            if (grateful >= some) return 1;
            if (owed >= some) return 6;
            return 0;
        }
    }
}
