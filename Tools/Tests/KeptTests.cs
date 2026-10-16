using ColonyCouncil;

/// <summary>
/// Promises kept (KeptMath): the grace period for each setting, the watch's length, the mood that's larger the sooner a
/// wish is taken back, how much of a stock must stay, and what each check leads to, including whole stories: a bed moved
/// in an afternoon, a guest bed reassigned for good, a room put back after the penalty, and one put back too late.
/// </summary>
public static class KeptTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "promises kept: " + what);
        }

        const int hour = KeptMath.TicksPerHour, day = KeptMath.TicksPerDay;

        // ---------- The grace period: off, half a day, a day (the default), three days ----------
        Check(KeptMath.GraceTicks(KeptGrace.Off) == 0, "no grace when off");
        Check(KeptMath.GraceTicks(KeptGrace.Short) == day / 2, "short grace is half a day");
        Check(KeptMath.GraceTicks(KeptGrace.Normal) == day, "normal grace is a day");
        Check(KeptMath.GraceTicks(KeptGrace.Long) == day * 3, "long grace is three days");
        Check(KeptMath.GraceTicks(KeptGrace.Off) < KeptMath.GraceTicks(KeptGrace.Short) && KeptMath.GraceTicks(KeptGrace.Short) < KeptMath.GraceTicks(KeptGrace.Normal)
              && KeptMath.GraceTicks(KeptGrace.Normal) < KeptMath.GraceTicks(KeptGrace.Long), "each grace longer than the one before");
        Check(KeptMath.GraceTicks(KeptGrace.Short) >= KeptMath.CheckHours * hour * 2, "even the short grace spans several checks");

        // ---------- The watch: about 60 days, longer when the def says so ----------
        Check(KeptMath.WatchTicks(0f) == 60 * day && KeptMath.WatchTicks(-5f) == 60 * day, "the default watch is 60 days");
        Check(KeptMath.WatchTicks(90f) == 90 * day, "a room is watched for 90 days");
        Check(KeptMath.AmendsDays >= 2f && KeptMath.AmendsDays <= 7f, "a few days to make it right");
        Check(KeptMath.MovingDays >= 3f, "a plan being built counts for days, not hours");

        // ---------- Checked every two hours, never per tick ----------
        int due = 0;
        for (int t = 0; t < day; t += 1) if (t % hour == 157 && KeptMath.CheckDue(t)) due++;
        Check(due == 24 / KeptMath.CheckHours, $"checked {24 / KeptMath.CheckHours} times a day (got {due})");
        Check(!KeptMath.CheckDue(-1), "not before the game starts");

        // ---------- The mood: -6 at once, -3 at the end of the watch, never outside that ----------
        int watch = KeptMath.WatchTicks(60f);
        Check(Math.Abs(KeptMath.MoodShare(1000, 1000, watch) - 1f) < 0.001f, "taken back at once: the full mood");
        Check(Math.Abs(KeptMath.MoodShare(1000, 1000 + watch, watch) - 0.5f) < 0.001f, "taken back as the watch ends: half the mood");
        Check(Math.Abs(KeptMath.MoodShare(0, watch / 2, watch) - 0.75f) < 0.001f, "half way: three quarters");
        Check(KeptMath.MoodShare(0, watch * 3, watch) == 0.5f && KeptMath.MoodShare(5000, 0, watch) == 1f, "clamped at both ends");
        Check(KeptMath.MoodShare(0, 100, 0) == KeptMath.MoodLate, "a watch of no length takes the smaller mood");
        float last = 2f;
        bool falling = true;
        for (int d = 0; d <= 60; d++)
        {
            float m = KeptMath.MoodShare(0, d * day, watch);
            if (m > last + 0.0001f) falling = false;
            last = m;
            float mood = 6f * m;
            if (mood < 3f - 0.001f || mood > 6f + 0.001f) falling = false;
        }
        Check(falling, "the mood only shrinks as time passes, from 6 to 3");

        // ---------- Trust: as a broken promise, half back when made right ----------
        Check(KeptMath.TrustLoss == 18f, "the trust of a broken promise");
        Check(KeptMath.TrustBack(18f) == 9f && KeptMath.TrustBack(-4f) == 0f && KeptMath.TrustBack(10f) <= 10f, "half the trust back, never more than was lost");

        // ---------- Stocks: what must stay ----------
        Check(KeptMath.Needed(12f, 0.33f) == 4, $"healroot put by: 4 of 12 must stay (got {KeptMath.Needed(12f, 0.33f)})");
        Check(KeptMath.Needed(2000f, 0.5f) == 1000, "a treasury: half must stay");
        Check(KeptMath.Needed(4f, 0.5f) == 2 && KeptMath.Needed(3f, 0.5f) == 2, "books: half, rounded up");
        Check(KeptMath.Needed(15f, 1f) == 15 && KeptMath.Needed(7.5f, 1f) == 8, "without a share, all of it");
        Check(KeptMath.Needed(1f, 0.1f) == 1, "always at least one");
        Check(KeptMath.Needed(0f, 0.5f) == 0, "nothing asked, nothing needed");

        // ---------- Rooms: a little slack ----------
        Check(KeptMath.RoomHolds(50f, 50f) && KeptMath.RoomHolds(43f, 50f) && !KeptMath.RoomHolds(42f, 50f), "a room may dip to 85% of what was asked");

        // ---------- Plans being built count for a week ----------
        Check(KeptMath.PlanStillCounts(-1, 999999), "no plan yet: nothing to time");
        Check(KeptMath.PlanStillCounts(0, 7 * day) && !KeptMath.PlanStillCounts(0, 7 * day + 1), "a plan counts for seven days");

        // ---------- One check at a time ----------
        int until = 60 * day;
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Holds, day, until, -1, -1) == KeptStep.Nothing, "kept and still there: nothing");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Holds, until, until, -1, -1) == KeptStep.Close, "kept to the end of the watch: lived with");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Gone, day, until, -1, -1) == KeptStep.StartGrace, "gone: the grace starts");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Gone, until + 1, until, -1, -1) == KeptStep.Close, "gone after the watch: just life");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Unclear, day, until, -1, -1) == KeptStep.Nothing, "can't tell: nothing");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Unclear, until, until, -1, -1) == KeptStep.Close, "can't tell to the end: the watch still ends");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Unclear, until - 1, until, day, -1) == KeptStep.Nothing, "missing while away: the grace waits");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Unclear, until + (int)(KeptMath.AmendsDays * day), until, day, -1) == KeptStep.Close,
            "away long past the watch: let go");
        Check(KeptMath.Next(KeptPhase.Kept, KeptCheck.Over, day, until, -1, -1) == KeptStep.Close, "no longer makes sense: the watch ends");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Holds, day, until, day * 2, -1) == KeptStep.BackInTime, "back within the grace");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Gone, day * 2 - 1, until, day * 2, -1) == KeptStep.Nothing, "still in the grace");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Gone, day * 2, until, day * 2, -1) == KeptStep.TakeBack, "the grace ran out");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Gone, until + day, until, until + day / 2, -1) == KeptStep.TakeBack,
            "gone just before the watch ended still counts once the grace runs out");
        Check(KeptMath.Next(KeptPhase.Missing, KeptCheck.Over, day, until, day * 2, -1) == KeptStep.Close, "missing, then no longer makes sense");
        Check(KeptMath.Next(KeptPhase.TakenBack, KeptCheck.Holds, day * 3, until, -1, day) == KeptStep.MadeRight, "back within a few days: made right");
        Check(KeptMath.Next(KeptPhase.TakenBack, KeptCheck.Holds, day + (int)(KeptMath.AmendsDays * day) + 1, until, -1, day) == KeptStep.Close, "back too late: let go");
        Check(KeptMath.Next(KeptPhase.TakenBack, KeptCheck.Gone, day * 2, until, -1, day) == KeptStep.Nothing, "taken back and still gone: waiting");
        Check(KeptMath.Next(KeptPhase.TakenBack, KeptCheck.Gone, day + (int)(KeptMath.AmendsDays * day) + 1, until, -1, day) == KeptStep.Close, "the chance to make it right passed");
        Check(KeptMath.Next(KeptPhase.TakenBack, KeptCheck.Unclear, day + (int)(KeptMath.AmendsDays * day) + 1, until, -1, day) == KeptStep.Close, "away until it was too late");

        // ---------- Whole stories, checked every two hours ----------
        // A guest bed moved to another room: gone for ten hours, then back. Nothing comes of it.
        var moved = Story(KeptGrace.Normal, t => t >= 5 * day && t < 5 * day + 10 * hour);
        Check(moved.takenAt < 0 && moved.end == "lived with", "a bed moved in an afternoon is never taken back");
        // Reassigned for good on day 5: taken back a day later, no sooner.
        var gone = Story(KeptGrace.Normal, t => t >= 5 * day);
        Check(gone.takenAt >= 6 * day && gone.takenAt <= 6 * day + 2 * KeptMath.CheckHours * hour, $"reassigned for good: taken back a day later (day {gone.takenAt / (float)day:0.00})");
        Check(gone.end == "let go" && !gone.madeRight, "never put back: let go after a few days");
        Check(Story(KeptGrace.Short, t => t >= 5 * day).takenAt >= 5 * day + day / 2, "a short grace waits half a day");
        Check(Story(KeptGrace.Long, t => t >= 5 * day).takenAt >= 8 * day, "a long grace waits three days");
        var off = Story(KeptGrace.Off, t => t >= 5 * day);
        Check(off.takenAt >= 5 * day && off.takenAt <= 5 * day + KeptMath.CheckHours * hour, "no grace: at the next check");
        // Put back two days after it was taken: made right, and watched again to the end.
        var right = Story(KeptGrace.Normal, t => t >= 5 * day && t < 8 * day);
        Check(right.madeRight && right.end == "lived with", "put back two days later: made right");
        // Put back a week after: too late, the watch has let it go.
        var late = Story(KeptGrace.Normal, t => t >= 5 * day && t < 13 * day);
        Check(!late.madeRight && late.end == "let go", "put back a week later: too late");
        // Taken away on day 59: still counts, though the watch ends on day 60.
        Check(Story(KeptGrace.Normal, t => t >= 59 * day).takenAt >= 60 * day, "taken on the last day still counts");
        // Taken away on day 61: just life.
        var after = Story(KeptGrace.Normal, t => t >= 61 * day);
        Check(after.takenAt < 0 && after.end == "lived with", "taken after the watch: just life");
        // Taken twice: made right the first time, taken back again later.
        var twice = Story(KeptGrace.Normal, t => (t >= 5 * day && t < 8 * day) || t >= 20 * day);
        Check(twice.madeRight && twice.takenAt >= 21 * day && twice.end == "let go", "made right, then taken again: it counts again");
        // Gone on day 5, and they leave with a caravan six hours later for three days: the grace waits for them.
        var away = Story(KeptGrace.Normal, t => t >= 5 * day, t => t >= 5 * day + 6 * hour && t < 8 * day + 6 * hour);
        Check(away.takenAt >= 9 * day && away.takenAt <= 9 * day + 2 * KeptMath.CheckHours * hour, $"away: the grace's clock stops (day {away.takenAt / (float)day:0.00})");
        // Away for good: the watch still ends.
        var gone2 = Story(KeptGrace.Normal, t => false, t => t >= 10 * day);
        Check(gone2.takenAt < 0 && gone2.end == "lived with", "away to the end of the watch: let go quietly");

        return cases;
    }

    /// <summary>
    /// One watch through 70 days of checks, the thing gone whenever <paramref name="isGone"/> says and the colonist away
    /// (nothing can be told) whenever <paramref name="isAway"/> says. When it was last taken back (-1: never), whether it
    /// was made right, and how the watch ended.
    /// </summary>
    private static (int takenAt, bool madeRight, string end) Story(KeptGrace grace, Func<int, bool> isGone, Func<int, bool> isAway = null)
    {
        const int day = KeptMath.TicksPerDay;
        int until = KeptMath.WatchTicks(60f);
        var phase = KeptPhase.Kept;
        int graceUntil = -1, takenBack = -1, takenAt = -1;
        bool madeRight = false;
        int interval = KeptMath.CheckHours * KeptMath.TicksPerHour;
        for (int t = 157; t < 80 * day; t += KeptMath.TicksPerHour)
        {
            if (!KeptMath.CheckDue(t)) continue;
            var check = isAway != null && isAway(t) ? KeptCheck.Unclear : isGone(t) ? KeptCheck.Gone : KeptCheck.Holds;
            // As the tracker does: while it can't be told, the grace's clock stops.
            if (check == KeptCheck.Unclear && phase == KeptPhase.Missing) graceUntil += interval;
            switch (KeptMath.Next(phase, check, t, until, graceUntil, takenBack))
            {
                case KeptStep.StartGrace:
                    phase = KeptPhase.Missing;
                    graceUntil = t + KeptMath.GraceTicks(grace);
                    if (graceUntil <= t)
                    {
                        phase = KeptPhase.TakenBack;
                        takenBack = takenAt = t;
                    }
                    break;
                case KeptStep.BackInTime:
                    phase = KeptPhase.Kept;
                    break;
                case KeptStep.TakeBack:
                    phase = KeptPhase.TakenBack;
                    takenBack = takenAt = t;
                    break;
                case KeptStep.MadeRight:
                    phase = KeptPhase.Kept;
                    takenBack = -1;
                    madeRight = true;
                    break;
                case KeptStep.Close:
                    return (takenAt, madeRight, phase == KeptPhase.TakenBack ? "let go" : "lived with");
            }
        }
        return (takenAt, madeRight, "running");
    }
}
