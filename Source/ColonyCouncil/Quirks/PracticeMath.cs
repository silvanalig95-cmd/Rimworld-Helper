using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of practice and of habits that grow from it (Quirks/Practice.cs, Quirks.cs), free of game types so
    /// Tools/Tests can cover it. Each hour at a kind of work adds one to it, and each day everything keeps
    /// <see cref="KeepPerDay"/> of itself: steady work of h hours a day settles at about 20·h, and what stops fades by
    /// half in about two weeks.
    /// </summary>
    public static class PracticeMath
    {
        public const float KeepPerDay = 0.95f;

        /// <summary>A habit's daily chance, for someone at the matching work as much as its full mark, is this many times its usual one.</summary>
        public const float MaxEarned = 10f;

        /// <summary>The share of a habit's full mark that counts as living it (keeping it from fading).</summary>
        public const float LivedShare = 0.4f;

        /// <summary>A habit lived doesn't fade sooner than this many days from the last day it was lived.</summary>
        public const int KeepDays = 60;

        /// <summary>A deep-rooted one, not sooner than this many.</summary>
        public const int DeepKeepDays = 120;

        /// <summary>Lived this many days, a habit is deep-rooted: its knack and its good mood a little stronger.</summary>
        public const int DeepDays = 60;

        /// <summary>A good mood of a deep-rooted habit, this many times its usual.</summary>
        public const float DeepMood = 1.5f;

        /// <summary>Where steady work of this many hours a day settles.</summary>
        public static float Steady(float hoursPerDay) => hoursPerDay / (1f - KeepPerDay);

        /// <summary>
        /// How many times its usual chance a habit has, for someone with these recent hours at its work (its full mark:
        /// <paramref name="full"/>): from once to <see cref="MaxEarned"/> times; for one that comes only from its work,
        /// from nothing to <see cref="MaxEarned"/> times.
        /// </summary>
        public static float Earned(float hours, float full, bool onlyFromPractice = false)
        {
            float share = full <= 0f || hours <= 0f ? 0f : Math.Min(1f, hours / full);
            return onlyFromPractice ? MaxEarned * share : 1f + (MaxEarned - 1f) * share;
        }

        /// <summary>Whether these recent hours count as living the habit.</summary>
        public static bool Lived(float hours, float full) => full > 0f && hours >= full * LivedShare;

        /// <summary>
        /// When a habit lived today fades: no sooner than <see cref="KeepDays"/> from now (<see cref="DeepKeepDays"/> for a
        /// deep-rooted one), and never sooner than it would have anyway. A habit kept for good (0) stays so.
        /// </summary>
        public static int KeptUntil(int expires, int now, int ticksPerDay, bool deep = false) =>
            expires <= 0 ? expires : Math.Max(expires, now + (deep ? DeepKeepDays : KeepDays) * ticksPerDay);

        public static bool Deep(int livedDays, int deepDays = DeepDays) => livedDays >= Math.Max(1, deepDays);

        /// <summary>A good mood of a habit, deepened when it's deep-rooted (bad ones never grow).</summary>
        public static float Mood(float mood, bool deep) => deep && mood > 0f ? (float)Math.Round(mood * DeepMood) : mood;
    }
}
