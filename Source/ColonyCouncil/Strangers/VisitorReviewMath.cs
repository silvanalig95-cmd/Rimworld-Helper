using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The plain rules the review of the visitors and strangers settled (docs/review-visitors.md), kept free of game types so
    /// the tests can check them: how a pet's farewell is told by how it died, what a bad night or a worn thing costs in a
    /// nuisance, and how near "beside" is.
    /// </summary>
    public static class VisitorReviewMath
    {
        // ---------------- A pet's farewell ----------------

        /// <summary>
        /// The farewell's tag by how the animal died: "hero" when an enemy's blow killed it (it went down in the fighting),
        /// "quiet" when it died with no fresh wound on it (in its sleep, of age or illness), none when it died some other way
        /// (a wild beast, a fall, the colony's own knife), which only the plain lines fit.
        /// </summary>
        public static string Manner(bool enemyBlow, bool freshWound) => enemyBlow ? "hero" : freshWound ? null : "quiet";

        /// <summary>The lines of a death of old age only for an old animal that died quietly.</summary>
        public static bool OldAge(bool old, string manner) => old && manner == "quiet";

        // ---------------- A nuisance's own effect ----------------

        /// <summary>The rest bar after a bad night: <paramref name="lost"/> of it gone, never below a tenth (nobody drops asleep for it).</summary>
        public static float RestAfter(float level, float lost)
        {
            if (lost <= 0f) return level;
            if (level <= 0.1f) return level;
            return Math.Max(0.1f, level - lost);
        }

        /// <summary>Hit points a worn thing loses: <paramref name="share"/> of its most, at least one, and never the last one (it wears, it doesn't fall apart).</summary>
        public static int WearLoss(int hitPoints, int maxHitPoints, float share)
        {
            if (share <= 0f || maxHitPoints <= 0 || hitPoints <= 1) return 0;
            int loss = Math.Max(1, (int)Math.Round(maxHitPoints * share));
            return Math.Min(loss, hitPoints - 1);
        }

        // ---------------- Beside someone ----------------

        /// <summary>Close enough to do it there and then: kneeling beside them.</summary>
        public const float BesideCells = 2.5f;

        /// <summary>"A few steps from" someone: the fallen raider's plea is only asked of one this near (Council_DlmS_FallenPlea).</summary>
        public const float FewSteps = 12f;
    }
}
