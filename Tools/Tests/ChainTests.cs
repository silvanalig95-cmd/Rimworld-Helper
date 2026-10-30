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
        return n;
    }
}
