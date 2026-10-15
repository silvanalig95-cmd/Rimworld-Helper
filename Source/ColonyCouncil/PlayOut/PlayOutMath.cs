using System;

namespace ColonyCouncil
{
    /// <summary>How a feast turns out with the meals in the stores (see <see cref="PlayOutMath.PlanFeast"/>).</summary>
    public enum FeastPlan
    {
        /// <summary>A cooked meal for everyone who comes.</summary>
        Feast,
        /// <summary>Meals for some: the first to the table eat, the rest make do with talk.</summary>
        Partial,
        /// <summary>No cooked meals at all: it's a party instead.</summary>
        Party
    }

    /// <summary>
    /// The numbers behind answers that play out on the map (PlayOut/): when a gathering may start, how long it may wait,
    /// who counts as having been there, and what a feast can offer. No game types, so Tools/Tests can check them.
    /// </summary>
    public static class PlayOutMath
    {
        public const int TicksPerHour = 2500;

        /// <summary>A gathering's usual hours to start in (from six in the morning until ten at night).</summary>
        public const int DayFrom = 6;
        public const int DayTo = 22;

        /// <summary>How long a plan waits by default for danger to pass and people to be free, after its hours open.</summary>
        public const float DefaultWaitHours = 6f;

        /// <summary>The longest any plan waits, whatever its hours and wait say (so nothing hangs about for days).</summary>
        public const float MaxWaitHours = 30f;

        public static int WrapHour(int h) => ((h % 24) + 24) % 24;

        /// <summary>Within the hours from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive), wrapping past midnight (20 to 2). Equal ends: any hour.</summary>
        public static bool InHours(int hour, int from, int to)
        {
            hour = WrapHour(hour);
            from = WrapHour(from);
            to = WrapHour(to);
            if (from == to) return true;
            return from < to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>Whole hours until the window opens: 0 when it's open now.</summary>
        public static int HoursUntil(int hour, int from, int to)
        {
            if (InHours(hour, from, to)) return 0;
            return WrapHour(WrapHour(from) - WrapHour(hour));
        }

        /// <summary>When a plan gives up and leaves only its moods: its hours, then the wait it allows (never more than <see cref="MaxWaitHours"/>).</summary>
        public static int Deadline(int now, int hour, int from, int to, float waitHours)
        {
            float wait = Math.Max(1f, waitHours);
            float total = Math.Min(MaxWaitHours, HoursUntil(hour, from, to) + wait);
            return now + (int)Math.Round(total * TicksPerHour);
        }

        /// <summary>
        /// There long enough to count as having taken part: a quarter of the gathering, but never more than half an hour
        /// and never less than a few minutes.
        /// </summary>
        public static bool Attended(int presentTicks, int durationTicks)
        {
            int need = Math.Max(250, Math.Min(TicksPerHour / 2, durationTicks / 4));
            return presentTicks >= need;
        }

        /// <summary>A visit or a lesson counts once half of it has passed (cut short later, it still happened).</summary>
        public static bool VisitCounts(int stayedTicks, int durationTicks) => stayedTicks >= Math.Max(250, durationTicks / 2);

        /// <summary>What the stores allow: a meal each, a meal for some, or none at all.</summary>
        public static FeastPlan PlanFeast(int meals, int guests)
        {
            if (meals <= 0) return FeastPlan.Party;
            return meals >= guests ? FeastPlan.Feast : FeastPlan.Partial;
        }

        /// <summary>A gathering's length in ticks from hours, never under a quarter of an hour.</summary>
        public static int DurationTicks(float hours) => Math.Max(TicksPerHour / 4, (int)Math.Round(hours * TicksPerHour));

        /// <summary>Enough people for it to be a gathering: two, or everyone invited when fewer were.</summary>
        public static bool EnoughToGather(int able, int invited) => able >= Math.Min(2, Math.Max(1, invited));
    }
}
