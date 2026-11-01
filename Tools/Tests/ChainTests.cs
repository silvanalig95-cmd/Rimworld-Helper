using System;
using ColonyCouncil;

/// <summary>Production chains followed to the end (Building/ChainMath.cs): batches a day, days left, what a want asks of a part, bill counts, choosing.</summary>
public static class ChainTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "chains: " + what);
        }
        C(Math.Abs(ChainMath.BatchesADay(2500f, 1f) - 8f) < 0.001f, "an hour's batch at plain speed: eight a day");
        C(Math.Abs(ChainMath.BatchesADay(2500f, 2f) - 16f) < 0.001f && ChainMath.BatchesADay(0f, 1f) == 0f && ChainMath.BatchesADay(2500f, 0f) == 0f, "twice the speed, twice the batches; no work or no speed, none");
        C(Math.Abs(ChainMath.HoursABatch(5000f, 1f) - 2f) < 0.001f && ChainMath.HoursABatch(5000f, 0f) == ChainMath.Far, "a two-hour batch; nobody to make it, never");
        C(Math.Abs(ChainMath.DaysLeft(30, 10f, 0f) - 3f) < 0.001f, "thirty at ten a day last three days");
        C(ChainMath.DaysLeft(30, 10f, 10f) == ChainMath.Far && ChainMath.DaysLeft(30, 10f, 12f) == ChainMath.Far, "a make that keeps up never runs the stock down");
        C(Math.Abs(ChainMath.DaysLeft(30, 10f, 5f) - 6f) < 0.001f, "half made up: twice as long");
        C(ChainMath.Need(100, 10f, 20f, 0) == 200, "a hundred wanted at ten a batch takes ten batches of twenty parts");
        C(ChainMath.Need(100, 10f, 20f, 150) == 50 && ChainMath.Need(100, 10f, 20f, 300) == 0, "less what is in stock");
        C(ChainMath.Need(0, 10f, 20f, 0) == 0 && ChainMath.Need(10, 0f, 20f, 0) == 0, "nothing wanted, or nothing a batch gives: nothing needed");
        C(ChainMath.Need(5, 10f, 20f, 0) == 20, "a want under a batch still takes a whole batch");
        C(ChainMath.Target(0, 40, 10f, 20) == 85, "the bill covers the want and three days of use (with the fifth's margin, in fives)");
        C(ChainMath.Target(100, 0, 0f, 20) == 20, "nothing short and nothing used: the usual");
        C(Math.Abs(ChainMath.MealsADay(5, 0.9f) - 8.889f) < 0.01f && ChainMath.MealsADay(0, 0.9f) == 0f, "five people eat about nine simple meals a day");
        C(ChainMath.Efficiency(10f, 2500f) > ChainMath.Efficiency(4f, 2500f) && ChainMath.Efficiency(4f, 500f) > ChainMath.Efficiency(10f, 2500f), "more product for the hour is the better recipe");
        C(ChainMath.BenchScore(10, 0f) > ChainMath.BenchScore(10, 40f) && ChainMath.BenchScore(12, 40f) == ChainMath.BenchScore(10, 0f), "a bench by the store, or a better hand two levels up, score the same");
        C(!ChainMath.TooDeep(3) && ChainMath.TooDeep(4), "chains stop four links down");
        C(Math.Abs(ChainMath.LeadDays(new[] { 12f, 12f, 24f }) - 2f) < 0.001f && ChainMath.LeadDays(null) == 0f, "the lead time is the path's hours as days");
        C(Math.Abs(ChainMath.YieldADay(10f, 5f) - 1.2f) < 0.001f && ChainMath.YieldADay(10f, 0f) == 0f, "a plant yields its harvest over its growing days, at the land's share");
        C(ChainMath.CellsToSow(100, 10f) == 10 && ChainMath.CellsToSow(10, 10f) == 4 && ChainMath.CellsToSow(10000, 10f) == 60 && ChainMath.CellsToSow(10, 0f) == 0, "cells to sow: the want over the yield, four at least, sixty at most");

        // Per bench: the hand's speed times the bench's, and a hand's day shared among the benches they keep busy.
        C(Math.Abs(ChainMath.BenchSpeed(1.2f, 0.5f) - 0.6f) < 0.001f && ChainMath.BenchSpeed(1.2f, 0f) == 0f && ChainMath.BenchSpeed(-1f, 1f) == 0f,
            "a quick hand at a half-speed bench works at a little over half; a bench that can't be worked, not at all");
        C(ChainMath.HandHours(1) == ChainMath.WorkHoursADay && Math.Abs(ChainMath.HandHours(2) - ChainMath.WorkHoursADay / 2f) < 0.001f && ChainMath.HandHours(0) == ChainMath.WorkHoursADay,
            "one hand at two benches gives each half the day");
        C(ChainMath.BatchesADay(2500f, ChainMath.BenchSpeed(1f, 0.5f), ChainMath.HandHours(2)) == 2f, "half speed for half the hours: a quarter of the batches");
        C(Math.Abs(ChainMath.BenchScore(10, 0f, 0.5f) - ChainMath.BenchScore(8, 0f)) < 0.001f, "a half-speed bench scores like a hand two levels worse");
        C(ChainMath.BenchScore(10, 0f, 0f) < ChainMath.BenchScore(0, 100f), "a bench that can't be worked now scores below any that can");
        C(ChainMath.BenchScore(0, 60f) + ChainMath.DoingBonus > ChainMath.BenchScore(20, 0f, 1.5f), "the bench a bill already stands on is the one that does it");

        // The rolling record of use: amounts by key and day, the window, the time covered, rates a day.
        const int Day = UseLog.TicksPerDay;
        var log = new UseLog();
        C(log.Covered(5 * Day) == 0f && !log.Enough(5 * Day) && log.ADay("u|1|Meal", 5 * Day) == 0f, "an empty record covers nothing and says nothing");
        log.Add(10 * Day, "u|1|Meal", 4f);
        log.Add(10 * Day + Day / 2, "u|1|Meal", 2f);
        log.Add(10 * Day + Day / 2, "u|1|Herb", 1f);
        log.Add(10 * Day + Day / 2, "u|1|Meal", 0f);
        log.Add(10 * Day + Day / 2, "", 3f);
        C(log.keys.Count == 2 && log.since == 10 * Day, $"the same thing on the same day is one entry; nothing and no key aren't counted ({log.keys.Count} entries)");
        C(!log.Enough(10 * Day + Day / 2) && Math.Abs(log.Covered(10 * Day + Day / 2) - 0.5f) < 0.001f, "half a day in, the record isn't gone by yet");
        log.Add(11 * Day + Day / 2, "u|1|Meal", 3f);
        C(log.Enough(11 * Day + Day / 2) && Math.Abs(log.ADay("u|1|Meal", 11 * Day + Day / 2) - 6f) < 0.001f, "nine meals over a day and a half: six a day");
        log.Add(20 * Day, "u|1|Meal", 2f);
        C(log.keys.Count == 1 && log.days[0] == 20, "days before the window are let go");
        C(Math.Abs(log.Covered(20 * Day + Day / 4) - (UseLog.WindowDays + 0.25f)) < 0.001f, "a long record covers the window: three days and today so far");
        C(Math.Abs(log.ADay("u|1|Meal", 20 * Day + Day / 4) - 2f / (UseLog.WindowDays + 0.25f)) < 0.001f, "the rate is the window's total over the time it covers");
        var totals = log.Totals(20 * Day + Day / 4);
        C(totals.Count == 1 && totals["u|1|Meal"] == 2f && UseLog.Parts("w|12|34")[2] == "34" && UseLog.Key("p", 12, "Steel") == "p|12|Steel", "totals by key, and keys made and read in parts");
        var broken = new UseLog { since = 5 };
        broken.days.Add(1);
        broken.Repair();
        C(broken.days.Count == 0 && broken.keys.Count == 0 && broken.since == -1, "lists that don't go together are emptied on load");
        return n;
    }
}
