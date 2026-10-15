using ColonyCouncil;

/// <summary>
/// Answers that play out on the map (PlayOutMath): the hours a gathering may start in (also past midnight), how long a
/// plan waits before it leaves only its moods, who counts as having been there, what a feast can offer with the meals in
/// the stores, and when there are enough people for a gathering.
/// </summary>
public static class PlayOutTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "play out: " + what);
        }

        // Hours, with and without the turn of midnight; equal ends mean any hour.
        Check(PlayOutMath.InHours(6, 6, 22) && PlayOutMath.InHours(21, 6, 22) && !PlayOutMath.InHours(22, 6, 22) && !PlayOutMath.InHours(3, 6, 22), "the day's hours");
        Check(PlayOutMath.InHours(23, 20, 3) && PlayOutMath.InHours(1, 20, 3) && !PlayOutMath.InHours(3, 20, 3) && !PlayOutMath.InHours(12, 20, 3), "past midnight");
        for (int h = 0; h < 24; h++) Check(PlayOutMath.InHours(h, 0, 0) && PlayOutMath.InHours(h, 5, 5), $"any hour ({h})");
        Check(PlayOutMath.InHours(-1, 22, 2) && PlayOutMath.InHours(25, 0, 2), "hours wrap");

        // Hours until the window opens.
        Check(PlayOutMath.HoursUntil(10, 6, 22) == 0, "open now");
        Check(PlayOutMath.HoursUntil(23, 6, 22) == 7 && PlayOutMath.HoursUntil(3, 6, 22) == 3, "until the morning");
        Check(PlayOutMath.HoursUntil(10, 17, 23) == 7 && PlayOutMath.HoursUntil(4, 20, 3) == 16, "until the evening");
        for (int h = 0; h < 24; h++)
            for (int from = 0; from < 24; from += 5)
            {
                int to = (from + 7) % 24;
                int wait = PlayOutMath.HoursUntil(h, from, to);
                Check(wait >= 0 && wait < 24 && PlayOutMath.InHours(h + wait, from, to), $"waiting {wait} hours from {h} opens {from}~{to}");
            }

        // Deadlines: the hours, then the wait, never more than the cap, never less than an hour.
        Check(PlayOutMath.Deadline(1000, 10, 6, 22, 6f) == 1000 + 6 * PlayOutMath.TicksPerHour, "open now: just the wait");
        Check(PlayOutMath.Deadline(0, 23, 6, 22, 6f) == 13 * PlayOutMath.TicksPerHour, "night: the morning, then the wait");
        Check(PlayOutMath.Deadline(0, 10, 17, 23, 100f) == (int)(PlayOutMath.MaxWaitHours * PlayOutMath.TicksPerHour), "capped");
        Check(PlayOutMath.Deadline(0, 10, 6, 22, 0f) == PlayOutMath.TicksPerHour, "at least an hour");

        // Who was there.
        int gathering = PlayOutMath.TicksPerHour * 3;
        Check(!PlayOutMath.Attended(0, gathering) && !PlayOutMath.Attended(1000, gathering), "a glance in isn't being there");
        Check(PlayOutMath.Attended(PlayOutMath.TicksPerHour / 2, gathering), "half an hour of a long one is");
        Check(PlayOutMath.Attended(PlayOutMath.TicksPerHour / 4, PlayOutMath.TicksPerHour), "a quarter of a short one is");
        Check(!PlayOutMath.Attended(200, 400) && PlayOutMath.Attended(250, 400), "never under a few minutes");
        Check(PlayOutMath.VisitCounts(1300, 2500) && !PlayOutMath.VisitCounts(1000, 2500) && !PlayOutMath.VisitCounts(200, 300), "a visit counts at half");

        // Feasts.
        Check(PlayOutMath.PlanFeast(0, 5) == FeastPlan.Party, "no meals: a party");
        Check(PlayOutMath.PlanFeast(3, 5) == FeastPlan.Partial, "some meals: some eat");
        Check(PlayOutMath.PlanFeast(5, 5) == FeastPlan.Feast && PlayOutMath.PlanFeast(40, 5) == FeastPlan.Feast, "enough: a feast");

        // Length and company.
        Check(PlayOutMath.DurationTicks(2f) == 2 * PlayOutMath.TicksPerHour && PlayOutMath.DurationTicks(0f) == PlayOutMath.TicksPerHour / 4, "lengths");
        Check(PlayOutMath.EnoughToGather(2, 8) && !PlayOutMath.EnoughToGather(1, 8) && PlayOutMath.EnoughToGather(1, 1) && !PlayOutMath.EnoughToGather(0, 1),
            "two make a gathering, or everyone invited");
        return cases;
    }
}
