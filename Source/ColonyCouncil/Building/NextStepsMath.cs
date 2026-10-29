using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's next steps (Building/NextSteps.cs) as plain rules: what the council proposes from what it watches,
    /// what the player took into the plan, and when a step is done or let go. Each step has a key (its source: an
    /// advisor signal, a piece of defense advice, the research bench, or the player's own), a score (how much it
    /// matters now), and a state.
    /// </summary>
    public static class NextStepsMath
    {
        /// <summary>Proposed by the council, taken into the plan, done, or let go by the player.</summary>
        public const int Proposed = 0, Planned = 1, Done = 2, Dropped = 3;

        /// <summary>A step whose source has stopped asking for it this many looks in a row (one an hour) is done.</summary>
        public const int DoneAfterMissing = 3;
        /// <summary>Days a let-go step stays let go before the council may propose it again, and days a done step stays in the list.</summary>
        public const int DroppedDays = 15, DoneKeptDays = 5;
        /// <summary>The council proposes at most this many at once, the top scored; the rest wait their turn.</summary>
        public const int MaxProposed = 8;

        /// <summary>Only what is worth a step: a signal that gives the player something to do.</summary>
        public static bool WorthProposing(float score, bool hasSomethingToDo, float floor = 0.08f) => hasSomethingToDo && score >= floor;

        /// <summary>
        /// The proposals to show, in score order, at most <see cref="MaxProposed"/>: current sources not already in the
        /// plan, not done lately, and not let go within <see cref="DroppedDays"/>.
        /// </summary>
        public static List<int> Propose(IList<float> scores, IList<bool> inPlan, IList<bool> doneLately, IList<bool> droppedLately)
        {
            var picks = new List<int>();
            for (int i = 0; i < scores.Count; i++)
                if (!inPlan[i] && !doneLately[i] && !droppedLately[i]) picks.Add(i);
            picks.Sort((a, b) => scores[b].CompareTo(scores[a]));
            if (picks.Count > MaxProposed) picks.RemoveRange(MaxProposed, picks.Count - MaxProposed);
            return picks;
        }

        /// <summary>A planned step with a source: done once the source has been missing <see cref="DoneAfterMissing"/> looks in a row.</summary>
        public static bool DoneNow(bool hasSource, int missingLooks) => hasSource && missingLooks >= DoneAfterMissing;

        /// <summary>Whether a let-go key is still let go, or a done step still shown, at <paramref name="now"/>.</summary>
        public static bool StillDropped(int droppedTick, int now, int ticksPerDay) => now - droppedTick < DroppedDays * ticksPerDay;

        public static bool StillShownDone(int doneTick, int now, int ticksPerDay) => now - doneTick < DoneKeptDays * ticksPerDay;

        /// <summary>
        /// Where a newly planned step goes in the plan: an urgent one (score at or over <paramref name="urgentFloor"/>)
        /// ahead of the first step less urgent than it; anything else at the end. The player's own order stays otherwise.
        /// </summary>
        public static int InsertAt(IList<float> plannedScores, float score, float urgentFloor = 0.45f)
        {
            if (score < urgentFloor) return plannedScores.Count;
            for (int i = 0; i < plannedScores.Count; i++)
                if (plannedScores[i] < score) return i;
            return plannedScores.Count;
        }

        /// <summary>The plan sorted by urgency, the player's own steps (no score) keeping their place among those of equal score.</summary>
        public static List<int> ByUrgency(IList<float> plannedScores)
        {
            var order = new List<int>();
            for (int i = 0; i < plannedScores.Count; i++) order.Add(i);
            order.Sort((a, b) => plannedScores[a] != plannedScores[b] ? plannedScores[b].CompareTo(plannedScores[a]) : a.CompareTo(b));
            return order;
        }

        /// <summary>A score as a level: 0 idea, 1 low, 2 medium, 3 high, 4 critical (the order of AdviceLevel).</summary>
        public static int LevelOf(float score) => score >= 0.7f ? 4 : score >= 0.45f ? 3 : score >= 0.22f ? 2 : score >= 0.08f ? 1 : 0;

        /// <summary>Moving a step up or down one place: the new index, or the same when it can't move.</summary>
        public static int Moved(int index, int count, int dir)
        {
            int to = index + dir;
            return to < 0 || to >= count ? index : to;
        }
    }
}
