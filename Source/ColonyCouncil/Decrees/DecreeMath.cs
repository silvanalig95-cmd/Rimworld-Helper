using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The game-free rules behind some decrees, kept apart so the tests can check them (Tools/Tests).</summary>
    public static class DecreeMath
    {
        /// <summary>
        /// "Many are hurt" (the Wounded moment): two colonists or more, and at least a third of them, hurt.
        /// </summary>
        public static bool ManyHurt(int hurt, int colonists) => hurt >= 2 && hurt * 3 >= colonists;

        /// <summary>
        /// A colonist's work during a clean sweep, into <paramref name="into"/>: their essential work first, in their own
        /// order; then every kind of cleaning (<paramref name="cleaning"/>, whether or not they were set to clean); then
        /// the rest of their work as they had it.
        /// </summary>
        public static void SweepOrder<T>(IList<T> own, IList<T> cleaning, Func<T, bool> essential, Func<T, bool> isCleaning, List<T> into)
        {
            into.Clear();
            foreach (var w in own)
                if (essential(w)) into.Add(w);
            foreach (var w in cleaning) into.Add(w);
            foreach (var w in own)
                if (!essential(w) && !isCleaning(w)) into.Add(w);
        }
    }
}
