using ColonyCouncil;

/// <summary>
/// The heart's needs (HeartNeedMath): where a need counts as missed and well met, how much likelier the need lacked most
/// makes its wishes and life goals, how a low need deepens its feeling, and the count of days badly lacking that, after
/// about three, adds a little strain: whole stories of a lonely week, a good hour in the middle of it, and recovery.
/// </summary>
public static class HeartNeedTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "heart's needs: " + what);
        }

        bool Near(float a, float b, float eps = 1e-4f) => Math.Abs(a - b) <= eps;

        // ---------- Levels ----------
        Check(HeartNeedMath.LevelOf(0f) == NeedLevel.Lacking && HeartNeedMath.LevelOf(24.9f) == NeedLevel.Lacking, "below 25 is badly lacking");
        Check(HeartNeedMath.LevelOf(25f) == NeedLevel.Missed && HeartNeedMath.LevelOf(44.9f) == NeedLevel.Missed, "25 to 44 is missed");
        Check(HeartNeedMath.LevelOf(45f) == NeedLevel.Fair && HeartNeedMath.LevelOf(59.9f) == NeedLevel.Fair, "45 to 59 is fair");
        Check(HeartNeedMath.LevelOf(60f) == NeedLevel.Met && HeartNeedMath.LevelOf(100f) == NeedLevel.Met, "60 and up is well met");
        Check(HeartNeedMath.LackingBelow < HeartNeedMath.FeelingBelow && HeartNeedMath.FeelingBelow < HeartNeedMath.MissedBelow
              && HeartNeedMath.MissedBelow < HeartNeedMath.WellMet, "thresholds in order: 25 < 35 < 45 < 60");

        // ---------- Wishes and life goals ----------
        Check(HeartNeedMath.WishFactor(45f) == 1f && HeartNeedMath.WishFactor(100f) == 1f, "no nudge from 45 up");
        Check(HeartNeedMath.WishFactor(44.9f) > 1f, "a nudge just below 45");
        Check(Near(HeartNeedMath.WishFactor(0f), 1.35f), $"wishes 35% likelier at 0, got {HeartNeedMath.WishFactor(0f)}");
        Check(Near(HeartNeedMath.WishFactor(22.5f), 1.175f), "half way, half the nudge");
        float last = 2f;
        bool falling = true;
        for (float v = 0f; v <= 50f; v += 2.5f)
        {
            float f = HeartNeedMath.WishFactor(v);
            if (f > last + 1e-6f) falling = false;
            last = f;
        }
        Check(falling, "the lower the need, the likelier its wishes");
        Check(HeartNeedMath.GoalFactor(45f) == 1f, "no goal nudge from 45 up");
        Check(Near(HeartNeedMath.GoalFactor(0f), HeartNeedMath.MaxFactor), "goals at most half again as likely");
        for (float v = 0f; v < 45f; v += 5f)
            Check(HeartNeedMath.GoalFactor(v) >= HeartNeedMath.WishFactor(v) && HeartNeedMath.GoalFactor(v) <= HeartNeedMath.MaxFactor,
                $"goals a little more than wishes at {v}");
        Check(HeartNeedMath.AllWellMet(60f, 60f, 60f, 60f) && !HeartNeedMath.AllWellMet(60f, 59.9f, 90f, 90f), "all four well met from 60");
        Check(Near(HeartNeedMath.AllMetRelief, 1.2f), "all four met: strain drains 20% faster");

        // ---------- Feelings ----------
        Check(HeartNeedMath.FeelingAmount(35f, 3f) == 0f && HeartNeedMath.FeelingAmount(80f, 3f) == 0f, "no feeling from 35 up");
        Check(Near(HeartNeedMath.FeelingAmount(0f, 3f), 3f) && Near(HeartNeedMath.FeelingAmount(17.5f, 3f), 1.5f), "deepens by up to its amount at 0");
        Check(HeartNeedMath.FeelingAmount(10f, 0f) == 0f && HeartNeedMath.FeelingAmount(10f, -2f) == 0f, "no amount, no feeling");
        Check(HeartNeedMath.FeelingAmount(0f, 3f) < 5f, "a low need alone never makes a feeling felt (they're felt from about 5)");

        // ---------- Counting the days badly lacking ----------
        Check(HeartNeedMath.LackHours(0f, 20f, 1f) == 1f, "an hour lacking counts");
        Check(HeartNeedMath.LackHours(10f, 30f, 1f) == 10f - HeartNeedMath.RecoverySpeed, "a better hour takes two off");
        Check(HeartNeedMath.LackHours(1f, 30f, 3f) == 0f, "never below nothing");
        Check(HeartNeedMath.LackHours(HeartNeedMath.MaxLackHours, 0f, 5f) == HeartNeedMath.MaxLackHours, "the count stops at a week");
        Check(HeartNeedMath.LackHours(7f, 10f, 0f) == 7f, "no time, no change");
        Check(HeartNeedMath.LackHours(float.NaN, 10f, 1f) == 0f && HeartNeedMath.LackHours(-5f, 10f, 1f) == 1f, "a broken count starts again");

        // A lonely spell: belonging at 15 hour after hour.
        float hours = 0f;
        int firstWearing = -1;
        for (int h = 1; h <= 24 * 5; h++)
        {
            hours = HeartNeedMath.LackHours(hours, 15f, 1f);
            if (firstWearing < 0 && HeartNeedMath.Wearing(hours, 15f)) firstWearing = h;
        }
        Check(firstWearing == 72, $"it starts to wear after three days, got hour {firstWearing}");
        Check(!HeartNeedMath.Wearing(71f, 15f) && HeartNeedMath.Wearing(72f, 15f), "not before");
        Check(!HeartNeedMath.Wearing(100f, 25f), "not once it's back at 25");
        // A good hour in the middle doesn't undo it; the next low hour wears again.
        float spell = HeartNeedMath.LackHours(80f, 40f, 1f);
        Check(HeartNeedMath.StrainPerHour(spell, 40f) == 0f && HeartNeedMath.Wearing(HeartNeedMath.LackHours(spell, 15f, 1f), 15f), "one good hour is a pause, not a cure");
        // A few good days make it good.
        float mended = HeartNeedMath.MaxLackHours;
        int toMend = 0;
        while (mended > 0f && toMend < 1000)
        {
            mended = HeartNeedMath.LackHours(mended, 50f, 1f);
            toMend++;
        }
        Check(toMend <= 96, $"a week's lack is made good in about three and a half days, took {toMend} hours");
        // Back to lacking after mending: three days again before it wears.
        Check(!HeartNeedMath.Wearing(HeartNeedMath.LackHours(0f, 10f, 1f), 10f), "after mending, it starts over");

        // ---------- How much strain ----------
        Check(HeartNeedMath.StrainPerHour(50f, 10f) == 0f, "nothing before three days");
        Check(Near(HeartNeedMath.StrainPerHour(100f, 0f), HeartNeedMath.StrainPerHourMost), "most at 0");
        Check(HeartNeedMath.StrainPerHour(100f, 24.99f) >= HeartNeedMath.StrainPerHourLeast - 1e-4f, "least just under 25");
        float perDayMost = HeartNeedMath.StrainPerHourMost * 24f;
        Check(perDayMost <= 2f && HeartNeedMath.StrainPerHourLeast * 24f >= 0.9f, $"about 1 to 2 a day for one need, got up to {perDayMost}");
        float cap = HeartMath.SourceCap(StrainSource.Unmet);
        Check(cap <= 3f && cap >= perDayMost, "its own cap: 3 a day, more than one need alone gives");
        Check(cap < HeartMath.DailyCap / 5f, "a small part of the day's strain at most");
        // Four needs badly lacking for days, all at 0, at 100% strength: the cap holds.
        {
            float today = 0f, total = 0f, added = 0f;
            for (int h = 0; h < 24; h++) added += HeartMath.Add(4f * HeartNeedMath.StrainPerHourMost, 1f, 1f, ref today, cap, ref total, HeartMath.DailyCap);
            Check(Near(added, cap), $"everything missing adds no more than the cap, got {added}");
        }
        Check(HeartMath.TraitFactor(StrainSource.Unmet, StrainTraits.IronWilled) < 1f && HeartMath.TraitFactor(StrainSource.Unmet, StrainTraits.Psychopath) == 1f,
            "nerves change it like any strain; nothing special for psychopaths");
        return cases;
    }
}
