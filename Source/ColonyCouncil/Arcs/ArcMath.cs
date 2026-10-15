using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of personal stories (Arcs/), free of game types so the tests can cover it (Tools/Tests): how many
    /// stories a colony sees in a year at each setting, how many may run at once, the chance a new one begins on a given
    /// day, how a held condition counts up, and how candidates are weighed.
    /// </summary>
    public static class ArcMath
    {
        /// <summary>Days in a RimWorld year (four quadrums of fifteen days).</summary>
        public const float DaysPerYear = 60f;

        /// <summary>Settings as numbers: 0 off, 1 rare, 2 normal, 3 frequent.</summary>
        public const int Off = 0, Rare = 1, Normal = 2, Frequent = 3;

        /// <summary>
        /// Stories a colony sees in a year at this setting: two, four or seven for a colony of about six, three quarters
        /// of that for one to three people, up to half again as many from twelve.
        /// </summary>
        public static float PerYear(int frequency, int colonists)
        {
            if (frequency <= Off || colonists <= 0) return 0f;
            float basis = frequency == Rare ? 2f : frequency == Normal ? 4f : 7f;
            float size = Clamp01((colonists - 3) / 9f);
            return basis * (0.75f + 0.75f * size);
        }

        /// <summary>
        /// Stories under way at once: one (rare), two (normal) or three (frequent), one more from nine colonists, and
        /// never more than one for every two people.
        /// </summary>
        public static int MaxActive(int frequency, int colonists)
        {
            if (frequency <= Off || colonists <= 0) return 0;
            int cap = frequency == Rare ? 1 : frequency == Normal ? 2 : 3;
            if (colonists >= 9) cap++;
            return Math.Max(1, Math.Min(cap, colonists / 2));
        }

        /// <summary>The fewest days between two new stories: two fifths of the average wait.</summary>
        public static float MinGapDays(float perYear) => perYear <= 0f ? float.MaxValue : DaysPerYear / perYear * 0.4f;

        /// <summary>No more than this many stories may begin within any one year (the budget).</summary>
        public static int Budget(float perYear) => perYear <= 0f ? 0 : (int)Math.Ceiling(perYear);

        /// <summary>
        /// The chance a new story begins today. None within the gap after the last one, while as many as the budget
        /// allows began in the last year, or while the most that may run at once are running. After the gap the daily
        /// chance is set so the average wait comes out at a year divided by the stories it should hold, and doubles
        /// once a story is well overdue.
        /// </summary>
        public static float DailyChance(float perYear, int startedLastYear, float daysSinceLast, int active, int maxActive)
        {
            if (perYear <= 0f || active >= maxActive || startedLastYear >= Budget(perYear)) return 0f;
            float gap = MinGapDays(perYear);
            if (daysSinceLast < gap) return 0f;
            float mean = DaysPerYear / perYear;
            float rate = 1f / Math.Max(1f, mean - gap);
            if (daysSinceLast > mean * 1.5f) rate *= 2f;
            return Math.Min(0.9f, rate);
        }

        /// <summary>
        /// Hours counted toward a gate that waits for a condition to hold: one for each hour it holds, half an hour taken
        /// back for each hour it doesn't, never below nothing. A bad evening costs a little; a bad week costs the lot.
        /// </summary>
        public static float Held(float held, bool holds) => holds ? held + 1f : Math.Max(0f, held - 0.5f);

        /// <summary>
        /// How likely this colonist and this story are to be picked among the day's candidates: half again as likely for
        /// someone who has never had a story, a third as likely for a story the colony saw lately.
        /// </summary>
        public static float CandidateWeight(float weight, bool hadStoryBefore, bool storySeenLately)
        {
            if (weight <= 0f) return 0f;
            float w = weight;
            if (!hadStoryBefore) w *= 1.5f;
            if (storySeenLately) w *= 0.35f;
            return w;
        }

        /// <summary>A stage that has gone on longer than its gate allows.</summary>
        public static bool TimedOut(float daysInStage, float timeoutDays) => timeoutDays > 0f && daysInStage >= timeoutDays;

        /// <summary>A share of the way to a target, from a start value, for the Story tab (0 to 1).</summary>
        public static float Progress(float now, float start, float target)
        {
            if (target <= start) return now >= target ? 1f : 0f;
            return Clamp01((now - start) / (target - start));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
