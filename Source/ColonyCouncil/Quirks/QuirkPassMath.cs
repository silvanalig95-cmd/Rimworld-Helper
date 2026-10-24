namespace ColonyCouncil
{
    /// <summary>
    /// How likely a habit passes from one colonist to someone close to them (QuirkPassing.cs), each day: the closer they
    /// are, the likelier, and a habit lived long enough to take root passes a little more readily. A parent's habit
    /// reaches their child in a season or two; a good friend's takes a year or more.
    /// </summary>
    public static class QuirkPassMath
    {
        /// <summary>Each day, for one habit of someone they're close to (before closeness).</summary>
        public const float BaseChance = 0.0015f;

        /// <summary>Once they've picked one up from someone, this many days before the next.</summary>
        public const int RestDays = 30;

        /// <summary>A habit that comes only from its work passes to someone at that work this much of its full hours.</summary>
        public const float PracticeShare = 0.25f;

        /// <summary>How close they are (0: not close enough to pick anything up).</summary>
        public enum Bond
        {
            None,
            Friend,
            Family,
            Lover,
            Mentor,
            Parent
        }

        public static float Weight(Bond b)
        {
            switch (b)
            {
                case Bond.Parent: return 3f;
                case Bond.Mentor: return 2.5f;
                case Bond.Lover: return 2f;
                case Bond.Family: return 1.5f;
                case Bond.Friend: return 1f;
                default: return 0f;
            }
        }

        /// <summary>The least they must think of the one they'd pick it up from: a friend more than family.</summary>
        public static int MinOpinion(Bond b) => b == Bond.Friend ? 20 : 0;

        public static float Chance(Bond b, bool deep) => BaseChance * Weight(b) * (deep ? 1.5f : 1f);

        /// <summary>Whether someone has done enough of a habit's work to pick up one that comes only from it.</summary>
        public static bool AtTheWork(float hours, float fullHours) => hours >= fullHours * PracticeShare;
    }
}
