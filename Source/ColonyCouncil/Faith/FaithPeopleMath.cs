using System;

namespace ColonyCouncil
{
    /// <summary>What a talk about faith does to the two who had it (Faith/FaithTalk.cs).</summary>
    public enum FaithTalkMove
    {
        /// <summary>Nothing for their faith.</summary>
        None,
        /// <summary>One of them, devout, comes out of it well: the other is swayed a little toward their faith.</summary>
        Sway,
        /// <summary>A heated argument across faiths: nobody is swayed, and both hold on a little tighter.</summary>
        DigIn,
        /// <summary>Two of one faith who saw it the same way, or argued it out kindly: both a little steadier.</summary>
        Steady
    }

    /// <summary>
    /// The arithmetic of faith among the colony's people (Faith/: the faith talks, the crises, the faiths page), free of
    /// game types so Tools/Tests can cover it: which talk does what and how much, who comes out of a friendly debate
    /// well, how often a pair or a listener can be moved, how a view about faith reads beside certainty, how hard a crisis
    /// shakes, and when a faith's demands or a brush with death count as one. How far a word moves someone is
    /// <see cref="FaithMath.Sway"/>.
    /// </summary>
    public static class FaithPeopleMath
    {
        public const int DayTicks = 60000;

        // ---------------- Talks ----------------

        /// <summary>What a talk agreed on gives the devout speaker's word, before the listener's character and the cap.</summary>
        public const float AgreeSway = 0.035f;
        /// <summary>What a friendly debate the devout speaker came out of well gives their word: livelier than agreeing.</summary>
        public const float DebateSway = 0.045f;
        /// <summary>A heated argument across faiths: each holds on this much tighter.</summary>
        public const float DigIn = 0.01f;
        /// <summary>Two of one faith who talked it over: each this much steadier.</summary>
        public const float SameFaithSteady = 0.01f;
        /// <summary>One faith move between the same two in this long (either way round).</summary>
        public const int PairGapTicks = 2 * DayTicks;
        /// <summary>Someone is swayed by a talk at most once a day, whoever it's with.</summary>
        public const int ListenerGapTicks = DayTicks;
        /// <summary>Someone is steadied (or digs in) by a talk at most once a day.</summary>
        public const int SteadyGapTicks = DayTicks;
        /// <summary>A listener expected to give at least this much ground says so in the talk; less, and they keep it to themselves.</summary>
        public const float VisiblyMovedAt = 0.03f;
        /// <summary>A friendly debate: one of them likes the other at least this much (or they are friends or lovers).</summary>
        public const int FriendlyOpinion = 15;

        /// <summary>What a talk about faith does, by how it went, whether the two share a faith and whether a debate was friendly.</summary>
        public static FaithTalkMove Move(TalkOutcome how, bool sameFaith, bool friendly)
        {
            if (sameFaith) return how == TalkOutcome.Agree || how == TalkOutcome.Debate ? FaithTalkMove.Steady : FaithTalkMove.None;
            switch (how)
            {
                case TalkOutcome.Agree: return FaithTalkMove.Sway;
                case TalkOutcome.Debate: return friendly ? FaithTalkMove.Sway : FaithTalkMove.None;
                case TalkOutcome.Argue: return FaithTalkMove.DigIn;
                default: return FaithTalkMove.None;
            }
        }

        /// <summary>The base of a sway for how the talk went (0 for anything but agreeing or a debate).</summary>
        public static float SwayBase(TalkOutcome how) => how == TalkOutcome.Debate ? DebateSway : how == TalkOutcome.Agree ? AgreeSway : 0f;

        /// <summary>
        /// How persuasive someone is in a talk about faith: the game's conversion power (1 for most; the Social skill
        /// raises it), twice as much from the devout, and how well the other likes them (-100..100).
        /// </summary>
        public static float Persuasion(float conversionPower, bool devout, float likedBy)
        {
            float liked = Clamp(1f + likedBy / 200f, 0.5f, 1.5f);
            return Math.Max(0f, conversionPower) * (devout ? 1f : 0.5f) * liked;
        }

        /// <summary>The chance the first of two comes out of a debate well, by their persuasion.</summary>
        public static float FirstWins(float first, float second)
        {
            first = Math.Max(0f, first);
            second = Math.Max(0f, second);
            return first + second <= 0f ? 0.5f : first / (first + second);
        }

        /// <summary>Whether enough time has passed since <paramref name="last"/> (-1: never) for it to happen again.</summary>
        public static bool Ready(int last, int now, int gap) => last < 0 || now < last || now - last >= gap;

        // ---------------- A view about faith, beside certainty ----------------

        /// <summary>A certainty move shows in their view about faith twice over (4% less certain, 0.08 less "holds to the faith").</summary>
        public const float ViewPerCertainty = 2f;

        /// <summary>
        /// Their view about faith (-1..1, as the character page reads it) kept in step with how firmly they hold theirs:
        /// someone wavering can't read as holding firmly to the faith, and someone devout can't read as doubting it.
        /// In between, the view is theirs.
        /// </summary>
        public static float ViewWithCertainty(float stance, float certainty)
        {
            float edge = TalkMath.SideAt - 0.01f;
            if (FaithMath.Wavering(certainty)) return Math.Min(stance, edge);
            if (FaithMath.Devout(certainty)) return Math.Max(stance, -edge);
            return stance;
        }

        // ---------------- A crisis of faith ----------------

        /// <summary>A crisis takes at least this much certainty, and at most this much, whatever their nature.</summary>
        public const float MinShake = 0.1f, MaxShake = 0.3f;
        /// <summary>No two crises of faith for one person within this many days.</summary>
        public const float CrisisGapDays = 20f;
        /// <summary>The same thing their faith asks, weighing on them at the daily look this many days running, is a crisis.</summary>
        public const int BurdenDays = 5;
        /// <summary>...when it takes at least this much off their mood.</summary>
        public const float BurdenMood = -4f;
        /// <summary>Near death: blood loss at least this severe (the game's "severe").</summary>
        public const float NearDeathBloodLoss = 0.45f;
        /// <summary>Near death: an illness or wound this far toward what kills.</summary>
        public const float NearDeathLethalShare = 0.75f;
        /// <summary>Near death: bleeding out within this many hours.</summary>
        public const float NearDeathBleedHours = 8f;
        /// <summary>A crisis waiting to be felt (the days after a funeral, back on their feet) is let go after this long.</summary>
        public const int WaitingDays = 6;

        /// <summary>How much certainty a crisis takes: its base, scaled by their nature (the mark's lean), never under or over the bounds.</summary>
        public static float CrisisShake(float baseShake, float natureScale)
        {
            if (baseShake <= 0f) return 0f;
            return Clamp(baseShake * Math.Max(0f, natureScale), MinShake, MaxShake);
        }

        /// <summary>Whether a new crisis may come: none yet (-1), or the last one at least <see cref="CrisisGapDays"/> ago.</summary>
        public static bool CrisisReady(int lastCrisis, int now) => lastCrisis < 0 || now < lastCrisis || now - lastCrisis >= CrisisGapDays * DayTicks;

        /// <summary>
        /// The days the same demand of their faith has weighed on them: one more when it still does, starting again at one
        /// when another has taken its place, none when nothing does.
        /// </summary>
        public static int BurdenStep(int days, bool any, bool same) => !any ? 0 : same ? days + 1 : 1;

        /// <summary>A faith's demand held long and hard enough to shake them.</summary>
        public static bool BurdenDue(int days, float mood) => days >= BurdenDays && mood <= BurdenMood;

        /// <summary>
        /// Close to death: blood loss severe, an illness or wound most of the way to what kills (its share of the lethal
        /// severity), or bleeding out within hours (negative: not bleeding out).
        /// </summary>
        public static bool NearDeath(float bloodLoss, float worstLethalShare, float hoursToBleedOut) =>
            bloodLoss >= NearDeathBloodLoss || worstLethalShare >= NearDeathLethalShare || (hoursToBleedOut >= 0f && hoursToBleedOut < NearDeathBleedHours);

        /// <summary>When a crisis felt later comes due: some days after (a loss: the days after the funeral), with a roll in 0..1.</summary>
        public static int DueAfter(float minDays, float maxDays, float roll) => (int)((minDays + (maxDays - minDays) * Clamp(roll, 0f, 1f)) * DayTicks);

        // ---------------- The faiths page ----------------

        /// <summary>A believer's faith in words for the page: "devout", "wavering" or "steady".</summary>
        public static string State(float certainty) => FaithMath.Devout(certainty) ? "devout" : FaithMath.Wavering(certainty) ? "wavering" : "steady";

        /// <summary>A talk worth showing as "could sway": at least this much off the listener's certainty from one good talk.</summary>
        public const float ShowSwayFrom = 0.025f;

        /// <summary>The newest faith moments kept per person (a sway, a steadying, a crisis), for their page.</summary>
        public const int MaxNotes = 5;
        /// <summary>A faith moment is kept this many days.</summary>
        public const int NoteDays = 120;

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
