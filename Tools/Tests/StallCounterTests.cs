using ColonyCouncil;

/// <summary>
/// The arithmetic of Rimfolk's sales table for RimMarket's colony stalls (Source/ColonyCouncil/Integrations/
/// StallCounterMath.cs): prices kept as RimMarket keeps them, counts within what there is, the quick amounts, how well a
/// price sells by RimMarket's own chances, and what a line fetches.
/// </summary>
public static class StallCounterTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "market stall: " + what);
        }

        // ---------- Prices as RimMarket keeps them ----------
        bool snapped = true;
        for (float p = 0f; p <= 4f; p += 0.013f)
        {
            float s = StallCounterMath.SnapPrice(p);
            // RimMarket's own: Mathf.Clamp(Mathf.Round(price / 0.05f) * 0.05f, 0.5f, 3f).
            float theirs = Math.Clamp((float)Math.Round(p / 0.05f) * 0.05f, 0.5f, 3f);
            if (Math.Abs(s - theirs) > 1e-4f || s < StallCounterMath.MinPrice || s > StallCounterMath.MaxPrice) snapped = false;
        }
        Check(snapped, "a price is snapped to 5% and kept between 50% and 300%, exactly as RimMarket does it");
        Check(Math.Abs(StallCounterMath.SnapPrice(1.1f) - 1.1f) < 1e-4f && Math.Abs(StallCounterMath.SnapPrice(1.12f) - 1.1f) < 1e-4f, "110% stays 110%");
        Check(StallCounterMath.Bargain < StallCounterMath.Fair && StallCounterMath.Fair < StallCounterMath.Dear
              && Math.Abs(StallCounterMath.Fair - StallCounterMath.DefaultPrice) < 1e-4f, "a bargain, the usual (RimMarket's default) and dear, in that order");

        // ---------- Counts ----------
        Check(StallCounterMath.ClampCount(0, 52) == 1 && StallCounterMath.ClampCount(80, 52) == 52 && StallCounterMath.ClampCount(7, 52) == 7, "a line sells at least one and no more than there is");
        Check(StallCounterMath.ClampCount(5, 0) == 1, "with none left in the colony, a line still holds one");
        var q52 = StallCounterMath.QuickAmounts(52);
        Check(q52.SequenceEqual(new[] { 52, 26, 10, 1 }), "all, half, ten and one: " + string.Join(",", q52));
        Check(StallCounterMath.QuickAmounts(1).SequenceEqual(new[] { 1 }) && StallCounterMath.QuickAmounts(3).SequenceEqual(new[] { 3, 1 })
              && StallCounterMath.QuickAmounts(20).SequenceEqual(new[] { 20, 10, 1 }) && StallCounterMath.QuickAmounts(0).Count == 0,
            "no repeats, nothing above what there is, nothing for none");

        // ---------- How well a price sells, by RimMarket's own chances ----------
        // RimMarket's ColonyStalls.ChanceFor: 90% up to 85%, 70% at 110%, 40% at 140%, 10% from 200%.
        float Chance(float pf) => pf <= 0.85f ? 0.9f : pf <= 1.1f ? Lerp(0.9f, 0.7f, (pf - 0.85f) / 0.25f) : pf <= 1.4f ? Lerp(0.7f, 0.4f, (pf - 1.1f) / 0.3f)
            : pf <= 2f ? Lerp(0.4f, 0.1f, (pf - 1.4f) / 0.6f) : 0.1f;
        Check(StallCounterMath.Demand(Chance(StallCounterMath.Bargain)) == 3 && StallCounterMath.Demand(Chance(StallCounterMath.Fair)) == 3
              && StallCounterMath.Demand(Chance(StallCounterMath.Dear)) == 1 && StallCounterMath.Demand(Chance(1.25f)) == 2 && StallCounterMath.Demand(Chance(3f)) == 0,
            "a bargain and the usual sell well, a little dearer sells, dear sells slowly, the dearest hardly");
        bool falls = true;
        int last = 3;
        for (float p = 0.5f; p <= 3f; p += 0.05f)
        {
            int d = StallCounterMath.Demand(Chance(p));
            if (d > last) falls = false;
            last = d;
        }
        Check(falls, "the dearer the price, never the better it sells");
        Check(StallCounterMath.LineWorth(12, 8) == 96 && StallCounterMath.LineWorth(-3, 8) == 0, "a line fetches its price each times its count");
        return cases;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
