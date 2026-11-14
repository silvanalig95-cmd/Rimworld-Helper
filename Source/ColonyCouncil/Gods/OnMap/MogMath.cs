using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of Tamiu's own doings (defName and key Mog; Gods/OnMap/MogsWays.cs; docs/gods-next-proposals.md, C 12),
    /// pure so Tools/Tests reads the same: when her riddle comes and how often it is answered wrong, how likely a cuff of
    /// the paw is, when a new grave is kept (by her keepers, else by a cat), and when the wrapped dead stand guard in a raid
    /// and how many.
    /// </summary>
    public static class MogMath
    {
        // ================= The riddle at the threshold =================

        /// <summary>She lies across the way in at most once a stay, from her second day, about one stay in ten days of it.</summary>
        public const float RiddleDayChance = 0.12f;

        /// <summary>The hours of the day she may lie down across the way in (from seven in the morning to seven at night).</summary>
        public const int RiddleFromHour = 7, RiddleToHour = 19;

        /// <summary>
        /// Whether her riddle comes this hour: not asked yet this stay, a day on the land behind her, in the daytime hours,
        /// at the day's chance spread over those hours.
        /// </summary>
        public static bool RiddleDue(float daysHere, bool askedThisStay, int hour, float roll)
        {
            if (askedThisStay || daysHere < 1f || hour < RiddleFromHour || hour >= RiddleToHour) return false;
            return roll < RiddleDayChance / (RiddleToHour - RiddleFromHour);
        }

        /// <summary>How likely a stay of <paramref name="days"/> days sees her riddle (for the tests and the notes).</summary>
        public static float RiddleInAStay(float days) => 1f - (float)Math.Pow(1f - RiddleDayChance, Math.Max(0f, days - 1f));

        /// <summary>The riddles of each age (CC_Mog_RiddleQ_{age}_{i}, CC_Mog_RiddleA_{age}_{i}).</summary>
        public const int RiddlesPerAge = 4;

        /// <summary>Which riddle of an age a seed picks (0 to RiddlesPerAge - 1).</summary>
        public static int Riddle(int seed) => (int)((uint)seed % RiddlesPerAge);

        /// <summary>A wrong answer the answerer guesses: another riddle's answer of the same age, never the right one.</summary>
        public static int WrongGuess(int right, int seed) => (right + 1 + (int)((uint)seed % (RiddlesPerAge - 1))) % RiddlesPerAge;

        /// <summary>
        /// The chance an answer is wrong, by the answerer's skill (their wits, or their tongue): three in four with none, about
        /// one in three at ten, one in ten from fifteen up, never surer than that.
        /// </summary>
        public static float WrongChance(int level) => Clamp(0.75f - 0.045f * Math.Max(0, level), 0.1f, 0.8f);

        /// <summary>The base chance (before the harm setting and its shared budget) that a wrong answer earns a cuff of the paw.</summary>
        public const float CuffChance = 0.6f;

        /// <summary>A cuff of the paw: a scratch on the arm, this deep.</summary>
        public const float CuffLow = 2f, CuffHigh = 5f;

        /// <summary>How long she lies across the way in waiting for an answer, and how long the one sent has to reach her (hours).</summary>
        public const float WaitHours = 10f, WalkHours = 3f;

        /// <summary>The colony's favour (hidden) for an answer: right, wrong, or nobody went.</summary>
        public const float RightFavour = 1.5f, WrongFavour = 0.3f, NobodyFavour = -0.3f;

        // ================= A new grave kept =================

        /// <summary>
        /// The chance a colonist's new grave is kept the first night (by her keepers where a people that holds her can come,
        /// else by a cat): a fifth of graves, more where the colony holds her, more the nearer she is drawn.
        /// </summary>
        public static float KeptChance(bool colonyHolds, float near) => Clamp(0.2f + (colonyHolds ? 0.2f : 0f) + 0.3f * Clamp(near, 0f, 1f), 0f, 0.7f);

        /// <summary>Of the kept graves, how many her keepers come to (when a people that holds her is at peace with the colony).</summary>
        public const float KeeperChance = 0.5f;

        /// <summary>A death older than this when its grave is found is not kept (days): the first night is past.</summary>
        public const float KeepWithinDays = 3f;

        /// <summary>How long the cat keeps the grave, in days.</summary>
        public const float CatDaysMin = 1f, CatDaysMax = 2f;

        /// <summary>The keeper's night at the grave, in hours (from dusk, until about dawn).</summary>
        public const float KeeperHours = 12f;

        // ================= The wrapped dead stand guard =================

        /// <summary>Tombs (graves and sarcophagi holding the dead, Alpha Memes' mummies) a home needs before its dead stand guard.</summary>
        public const int MinTombs = 2;

        /// <summary>How near (GodNear) she must be drawn, when she isn't on the land, before the wrapped dead stand guard at all.</summary>
        public const float RiseNear = 0.5f;

        /// <summary>
        /// The base chance (before the harm setting) that the wrapped dead stand guard in a raid that comes near the tombs: one
        /// raid in two while she is on the land, else from a little to a third as near as she is drawn, never with too few
        /// tombs.
        /// </summary>
        public static float RiseChance(bool onLand, float near, int tombs)
        {
            if (tombs < MinTombs) return 0f;
            if (onLand) return 0.5f;
            if (near < RiseNear) return 0f;
            return Clamp(0.15f + 0.4f * (near - RiseNear), 0f, 0.35f);
        }

        /// <summary>How many rise: two, one more for every three tombs past the first few, four at most.</summary>
        public static int Risen(int tombs) => tombs < MinTombs ? 0 : Math.Max(2, Math.Min(4, 1 + tombs / 3));

        /// <summary>They rise at most once in this many days.</summary>
        public const float RiseGapDays = 6f;

        /// <summary>A raider this near the tombs (cells) wakes them; they keep within the first of the tombs' middle and turn on anything inside the second.</summary>
        public const float EnemyNear = 30f, WanderRadius = 5f, GuardRadius = 9f;

        /// <summary>They lie down and are dust once no raider has been on the map for this long, or after the longest of watches (ticks).</summary>
        public const int QuietTicks = 2500, LongestTicks = 30000;

        /// <summary>Whether the wrapped dead lie down now: the map quiet of raiders an hour, or the longest watch kept.</summary>
        public static bool LieDown(int quietFor, int risenFor) => quietFor >= QuietTicks || risenFor >= LongestTicks;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
