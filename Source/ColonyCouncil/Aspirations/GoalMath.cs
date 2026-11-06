using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Life goals that last (decision 8 of the review, 6 November 2026). The player's save at day 128: of 38 goals chosen,
    /// 25 came after a battle or a brush with death, "bring down ten mechanoids" five times, and Stace changed hers four
    /// times in 25 days (a raid every ten days was a spark each time). Then a fight sparked only the one who brought down
    /// the most, and no goal came from fights at all; the player's word the same day: "some lifegoals should be sparked by
    /// fights, no? not 0." Now a fight sparks those who fought hardest in it (<see cref="Hardest"/>: the few who brought
    /// down the most, anyone downed in it, whoever carried someone out); a spark from a fight pulls a little less than one
    /// from the rest of life (a wedding, a loss, a child born, a dream come true, a fine meal); a goal someone else at home
    /// already holds weighs less, and a fight never sparks a goal someone else at home took up after a fight and is still
    /// at (<see cref="FightMaySpark"/>), so the colony doesn't fill with mechanoid hunters; and a goal holds for a while: a
    /// change of heart only after weeks with it, only for something big (a brush with death, a loss), and once per goal.
    /// Pure: Tools/Tests checks it.
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

        /// <summary>How many of those who brought down the most in a fight it sparks.</summary>
        public const int FightTop = 3;

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
        /// How strongly a spark pulls toward its goals: a fight (0.7; it was 0.4) and a foe who keeps coming back a little
        /// less than the rest of life, so the weight of what sets a goal is spread over a life's kinds, not only its fights.
        /// </summary>
        public static float SparkPull(string spark)
        {
            switch (spark)
            {
                case null: return 1f;
                case "battle": return 0.7f;
                case "nemesis": return 0.6f;
                // A war among the neighbours (6 November 2026): every colonist hears of it at once, so it pulls like a foe.
                case "war": return 0.6f;
                default: return 1f;
            }
        }

        /// <summary>A goal already held by others at home weighs less: half for one, a third for two.</summary>
        public static float Shared(int heldByOthers) => 1f / (1f + (heldByOthers < 0 ? 0 : heldByOthers));

        /// <summary>
        /// A spark of fighting: a fight the colony fought, or a war among the neighbours running now (one spark for every
        /// colonist at once, held to the same rule so a war doesn't set several on one fighting goal).
        /// </summary>
        public static bool FightSpark(string spark) => spark == "battle" || spark == "war";

        /// <summary>
        /// Whether a spark may set them on a goal: a fight's (or a war's) never on a goal someone else at home took up after
        /// a fight or a war and is still at, so one fighting goal is never chosen by several at once; any other spark always
        /// may.
        /// </summary>
        public static bool FightMaySpark(string spark, int othersAfterAFight) => !FightSpark(spark) || othersAfterAFight <= 0;

        /// <summary>
        /// Those who fought hardest in a fight and may take a fighting goal from it (indexes into the lists, in their order):
        /// the <see cref="FightTop"/> who brought down the most enemies (more first, the first noted of equals; nobody for
        /// bringing down none), anyone downed in it, and whoever carried someone of ours out of it.
        /// </summary>
        public static List<int> Hardest(IList<int> kills, IList<bool> downed, IList<bool> carried)
        {
            int n = kills?.Count ?? 0;
            var order = new List<int>();
            for (int i = 0; i < n; i++)
                if (kills[i] > 0)
                    order.Add(i);
            order.Sort((a, b) => kills[a] != kills[b] ? kills[b].CompareTo(kills[a]) : a.CompareTo(b));
            var chosen = new HashSet<int>();
            for (int k = 0; k < order.Count && k < FightTop; k++) chosen.Add(order[k]);
            for (int i = 0; i < n; i++)
                if (i < (downed?.Count ?? 0) && downed[i] || i < (carried?.Count ?? 0) && carried[i])
                    chosen.Add(i);
            var result = new List<int>();
            for (int i = 0; i < n; i++)
                if (chosen.Contains(i))
                    result.Add(i);
            return result;
        }
    }
}
