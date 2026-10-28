using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of leanings (Faith/FaithLeanings.cs), free of game types so Tools/Tests can cover it. Beside the
    /// ideoligion they hold and its certainty (the game's), each colonist leans, 0 to 1, toward the other ideoligions
    /// around them. Each day a leaning fades a little and is fed by that ideoligion's believers in the colony (more by
    /// friends, lovers and family), and by the council's decree when it names that ideoligion; the strongest leaning
    /// wears on their certainty, against the game's own daily recovery for the content. When a leaning passes their
    /// certainty, they take that ideoligion up.
    ///
    /// The pace, for a content colonist (the game gives back about three points of certainty a day): four friends of
    /// another ideoligion, about 70 days, and with the decree a little sooner, about two months; a lone believer of a
    /// minority never pulls the rest over. The closed-minded hold out far longer, the unhappy come round sooner (about
    /// a month, and by the decree alone).
    ///
    /// The decree is no shortcut on its own. It carries a leaning only so far (<see cref="DecreeCeiling"/>): past that it
    /// takes believers close to them, or moments of faith, so a content colonist with nobody of that ideoligion close
    /// never comes over by decree alone. And those it can't sway, the set in their ways, the proud and those whose
    /// ideoligion frowns on being preached to, dig in while it holds (<see cref="Defiance"/>): the more it presses, the
    /// less anyone of that ideoligion draws them and the firmer they hold, and it wears off only slowly after. Whoever
    /// comes over while it holds stays drawn back to the ideoligion they left.
    /// </summary>
    public static class LeaningMath
    {
        /// <summary>Each day a leaning keeps this share of itself before it is fed.</summary>
        public const float KeepPerDay = 0.97f;

        /// <summary>The most an ideoligion's believers add to a leaning in a day (a crowd of close friends).</summary>
        public const float BelieversMax = 0.04f;

        /// <summary>How much company (summed closeness) gives half of <see cref="BelieversMax"/>.</summary>
        public const float CompanyHalf = 2f;

        /// <summary>What the council's decree naming an ideoligion adds to a leaning toward it in a day.</summary>
        public const float Decree = 0.04f;

        /// <summary>The decree adds nothing to a leaning this strong: the rest is up to the believers around them.</summary>
        public const float DecreeCeiling = 0.3f;

        /// <summary>Defiance a day of the decree adds to someone it can't sway (half again for one whose ideoligion frowns on preaching).</summary>
        public const float DefianceRise = 0.06f;

        /// <summary>Each day without the decree, defiance keeps this share of itself (half gone in about three weeks).</summary>
        public const float DefianceKeep = 0.97f;

        /// <summary>Certainty full defiance gives back in a day: they hold to their own the harder.</summary>
        public const float DefianceSteady = 0.015f;

        /// <summary>Below this defiance is forgotten; passing <see cref="DefianceTellAt"/>, it is told once.</summary>
        public const float DefianceForget = 0.02f, DefianceTellAt = 0.5f;

        /// <summary>
        /// Someone the decree won't sway, only harden: set in their ways (openness under -20), proud (40 or more), or of an
        /// ideoligion that frowns on being preached to.
        /// </summary>
        public static bool Resists(float openness, float pride, bool dislikesPreaching) => dislikesPreaching || openness < -20f || pride >= 40f;

        /// <summary>One day of defiance: it grows while the decree presses them, and fades slowly once it doesn't.</summary>
        public static float Defiance(float defiance, bool pressed, bool dislikesPreaching)
        {
            float d = Clamp(defiance, 0f, 1f);
            if (!pressed) return d * DefianceKeep < DefianceForget ? 0f : d * DefianceKeep;
            return Clamp(d + DefianceRise * (dislikesPreaching ? 1.5f : 1f) * (1f - d), 0f, 1f);
        }

        /// <summary>What defiance gives back to their certainty in a day.</summary>
        public static float Steady(float defiance) => DefianceSteady * Clamp(defiance, 0f, 1f);

        /// <summary>The share of the strongest leaning taken off certainty each day.</summary>
        public const float Erode = 0.12f;

        /// <summary>
        /// The devout's certainty is worn this share of what others' is. (Their leanings grow as anyone's: the game keeps
        /// content colonists at full certainty, so nearly everyone counts as devout until a pull starts to tell.)
        /// </summary>
        public const float DevoutWear = 0.7f;

        /// <summary>A leaning has to pass certainty by this much, and be at least <see cref="MinSwitch"/>, to take over.</summary>
        public const float Margin = 0.05f, MinSwitch = 0.3f;

        /// <summary>Below this a leaning is forgotten.</summary>
        public const float Forget = 0.02f;

        /// <summary>At most this many leanings are kept for anyone, the strongest.</summary>
        public const int MaxLeanings = 6;

        /// <summary>Passing this, a leaning is told once (their pull toward it is plain to see).</summary>
        public const float TellAt = 0.5f;

        /// <summary>
        /// After taking an ideoligion up, someone settles into it for this many days (a year): no leaning wears on them
        /// or takes over meanwhile, so nobody flips back and forth.
        /// </summary>
        public const int SettleDays = 60;

        /// <summary>The ideoligion they left stays with them as a leaning of this share of their certainty in it, at most <see cref="ReturnMax"/>.</summary>
        public const float ReturnShare = 0.25f, ReturnMax = 0.1f;

        /// <summary>The same for someone who came over while the decree held: more of the old stays with them.</summary>
        public const float PressedReturnShare = 0.6f, PressedReturnMax = 0.3f;

        /// <summary>How strongly they're still drawn to the ideoligion they just left (more if they came over under the decree).</summary>
        public static float ReturnPull(float leftCertainty, bool underDecree = false) =>
            Clamp(underDecree ? Math.Min(PressedReturnMax, leftCertainty * PressedReturnShare) : Math.Min(ReturnMax, leftCertainty * ReturnShare), 0f, 1f);

        /// <summary>Whether they've settled since their last switch (-1: never switched).</summary>
        public static bool Settled(int lastSwitch, int now, int dayTicks) => lastSwitch < 0 || now - lastSwitch >= SettleDays * dayTicks;

        /// <summary>
        /// How much one believer's faith pulls on someone: by their opinion of them (-100 to 100: a rival nothing, a
        /// stranger half, a friend one, the dearest one and a half), half again for a lover or family.
        /// </summary>
        public static float Closeness(float opinion, bool loverOrKin) => Clamp(0.5f + opinion / 100f, 0f, 1.5f) * (loverOrKin ? 1.5f : 1f);

        /// <summary>What an ideoligion's believers add in a day, by their summed closeness: more company, less added by each.</summary>
        public static float Company(float closeness) => closeness <= 0f ? 0f : BelieversMax * closeness / (closeness + CompanyHalf);

        /// <summary>How readily someone leans: by openness (-100 to 100: the closed 0.4, the open up to 1.6).</summary>
        public static float Readiness(float openness) => Clamp(1f + openness / 150f, 0.4f, 1.6f);

        /// <summary>
        /// What feeds a leaning in a day: the believers' company, and the decree when it names that ideoligion (half for
        /// someone whose own ideoligion frowns on being preached to) while the leaning is under <see cref="DecreeCeiling"/>,
        /// by how readily they lean; less by their defiance of that ideoligion.
        /// </summary>
        public static float Feed(float closeness, bool decree, float readiness, bool dislikesPreaching, float pull = 0f, float defiance = 0f)
        {
            float byDecree = decree && pull < DecreeCeiling ? Decree * (dislikesPreaching ? 0.5f : 1f) : 0f;
            return Math.Max(0f, (Company(closeness) + byDecree) * readiness * (1f - Clamp(defiance, 0f, 1f)));
        }

        /// <summary>One day of a leaning: it fades, then is fed (less the closer it is to whole).</summary>
        public static float Day(float pull, float feed)
        {
            float kept = Clamp(pull, 0f, 1f) * KeepPerDay;
            return Clamp(kept + Math.Max(0f, feed) * (1f - kept), 0f, 1f);
        }

        /// <summary>What the strongest leaning takes off certainty in a day (less from the devout).</summary>
        public static float Erosion(float strongest, bool devout) => Erode * Clamp(strongest, 0f, 1f) * (devout ? DevoutWear : 1f);

        /// <summary>Whether a leaning has overtaken their certainty, and they take that ideoligion up: past it, and not fading.</summary>
        public static bool Overtakes(float pull, float certainty, int trend = 1) => pull >= MinSwitch && pull > certainty + Margin && trend >= 0;

        /// <summary>Which way a leaning went today: 1 growing, -1 fading, 0 about the same.</summary>
        public static int Trend(float before, float after) => after - before > 0.002f ? 1 : before - after > 0.002f ? -1 : 0;

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
