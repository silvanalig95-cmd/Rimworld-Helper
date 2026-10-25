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

        // ---------------- Needed and wanted: a decree again sooner ----------------

        /// <summary>A moment that holds counts this much toward a decree being needed, and this much when it holds strongly.</summary>
        public const int Holds = 1, HoldsStrongly = 2;

        /// <summary>The fewest days between a decree's end and declaring it again, however much it's needed.</summary>
        public const float MinCooldownDays = 1f;

        /// <summary>At most this share of a decree's rest is let off when it's needed and wanted, and of the resentment of a repeat.</summary>
        public const float MostCooldownLetOff = 0.65f, MostBacklashLetOff = 0.6f;

        /// <summary>
        /// How much the colony needs a decree now, 0 to 1, from its moments that hold (each <see cref="Holds"/>, or
        /// <see cref="HoldsStrongly"/>): three's worth is full need (a filthy home and many hurt after fight on fight).
        /// </summary>
        public static float Need(int strength) => Clamp01(strength / 3f);

        /// <summary>
        /// How much the colony wants it, 0 to 1: those who love it count most and those who hate it against it, those who
        /// don't mind it count for nothing; someone asking the council for it adds.
        /// </summary>
        public static float Want(int love, int like, int neutral, int dislike, int hate, bool asked)
        {
            int all = love + like + neutral + dislike + hate;
            float w = all > 0 ? (love + 0.75f * like - 0.5f * dislike - 0.75f * hate) / all : 0f;
            return Clamp01(w + (asked ? 0.3f : 0f));
        }

        /// <summary>
        /// The colony's understanding for a decree declared sooner and again, 0 to 1: none unless it's needed, more the
        /// more it's wanted. Patience wears thin: once it has been declared twice in the last month, it's halved.
        /// </summary>
        public static float Understanding(float need, float want, int timesThisMonth)
        {
            if (need <= 0f) return 0f;
            float u = Clamp01(need) * (0.4f + 0.6f * Clamp01(want));
            return timesThisMonth >= 2 ? u * 0.5f : u;
        }

        /// <summary>A decree's rest after it ends (ticks), with the colony's understanding: never shorter than a day, nor longer than its own.</summary>
        public static int CooldownTicks(float cooldownDays, float understanding, int ticksPerDay)
        {
            float full = cooldownDays * ticksPerDay;
            float let = full * (1f - MostCooldownLetOff * Clamp01(understanding));
            return (int)Math.Round(Math.Min(full, Math.Max(MinCooldownDays * ticksPerDay, let)));
        }

        /// <summary>How much of a repeat's resentment (lost trust, the mood against it, the chance someone snaps) still comes, with the colony's understanding.</summary>
        public static float BacklashFactor(float understanding) => 1f - MostBacklashLetOff * Clamp01(understanding);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

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
