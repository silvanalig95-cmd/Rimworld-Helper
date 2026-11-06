using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Life goals that last (decision 8 of the review, 6 November 2026). The player's save at day 128: of 38 goals chosen,
    /// 25 came after a battle or a brush with death, "bring down ten mechanoids" five times, and Stace changed hers four
    /// times in 25 days (a raid every ten days was a spark each time). Now a fight sparks a fighting goal in one person at
    /// most, the one who did most in it; a spark from a fight pulls less than one from the rest of life (a wedding, a loss,
    /// a child born, a dream come true, a fine meal); a goal someone else at home already holds weighs less, so the colony
    /// doesn't fill with mechanoid hunters; and a goal holds for a while: a change of heart only after weeks with it, only
    /// for something big (a brush with death, a loss), and once per goal. Pure: Tools/Tests checks it.
    /// </summary>
    public static class GoalMath
    {
        /// <summary>Days a goal holds whatever happens (it was ten).</summary>
        public const float HoldsDays = 20f;

        /// <summary>A goal this far along is theirs for good.</summary>
        public const float SettledAt = 0.35f;

        /// <summary>The day's chance of a change of heart, when one may come.</summary>
        public const float ChangeChance = 0.2f;

        /// <summary>The day's chance a spark makes them choose at once (it was 0.6 for any spark), times the spark's pull.</summary>
        public const float SparkedChooseChance = 0.6f;

        /// <summary>A spark big enough to change a goal held: a brush with death, or someone close lost.</summary>
        public static bool Big(string spark) => spark == "downed" || spark == "loss";

        /// <summary>
        /// Whether a goal may give way to another: not one taken up for someone who died, not one already chosen by a change
        /// of heart (once per goal), only after <see cref="HoldsDays"/>, only while it's under <see cref="SettledAt"/> done,
        /// and only for a big spark.
        /// </summary>
        public static bool MayChangeHeart(float daysHeld, float progress, bool inherited, bool turned, string spark) =>
            !inherited && !turned && daysHeld >= HoldsDays && progress < SettledAt && Big(spark);

        /// <summary>
        /// How strongly a spark pulls toward its goals: a fight (and a foe who keeps coming back) less than the rest of life,
        /// so the weight of what sets a goal is spread over a life's kinds, not only its fights.
        /// </summary>
        public static float SparkPull(string spark)
        {
            switch (spark)
            {
                case null: return 1f;
                case "battle": return 0.4f;
                case "nemesis": return 0.6f;
                default: return 1f;
            }
        }

        /// <summary>A goal already held by others at home weighs less: half for one, a third for two.</summary>
        public static float Shared(int heldByOthers) => 1f / (1f + (heldByOthers < 0 ? 0 : heldByOthers));

        /// <summary>
        /// The one who did most in a fight, by the enemies they brought down (index into <paramref name="kills"/>), the first
        /// of equals; -1 when nobody brought anyone down. Only they take a fighting goal from it.
        /// </summary>
        public static int Foremost(IList<int> kills)
        {
            int best = -1;
            for (int i = 0; i < (kills?.Count ?? 0); i++)
                if (kills[i] > 0 && (best < 0 || kills[i] > kills[best]))
                    best = i;
            return best;
        }
    }
}
