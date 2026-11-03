using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Fields by the weather and the frost harvest (Fields/FieldWeatherMath): growth hour by hour (resting at night, slowed
/// by cold), the plants' own cold limits, the yield of a plant harvested early, the 60% rule, when sowing pays again,
/// the frost harvest's pick, the forecast's parts and margins, and the player's desert, where winter nights stay above
/// the 1.6 cold limits.
/// </summary>
public static class FieldWeatherTests
{
    // ---------- A forecast to grow on, with the game's default growing temperatures (0, 6, 42, 58) ----------

    private static float TempFactor(float t) => t < 0f ? 0f : t < 6f ? t / 6f : t <= 42f ? 1f : t < 58f ? (58f - t) / 16f : 0f;

    /// <summary>Hour h of the forecast is hour h of the day counted from midnight.</summary>
    private static bool Rest(int h) => FieldWeatherMath.Resting(h % 24 / 24f);

    private static CropRun Crop(float[] temps, float growDays = 5.8f, float hmg = 0.65f, bool dies = true, int lifeHours = 0, float minGrowth = 0f, float margin = 0f)
    {
        var rate = new float[temps.Length];
        for (int h = 0; h < temps.Length; h++) rate[h] = Rest(h) ? 0f : TempFactor(temps[h]);
        return new CropRun
        {
            growDays = growDays, harvestMinGrowth = hmg, dieIfLeafless = dies, lifeHours = lifeHours, limits = FieldWeatherMath.Limits(minGrowth), rate = rate,
            low = FieldWeatherMath.Lows(temps, margin)
        };
    }

    private static float[] Flat(int days, float t) => Enumerable.Repeat(t, days * 24).ToArray();

    /// <summary>A warm forecast with one night at <paramref name="cold"/> on the given day, at 3 at night.</summary>
    private static float[] WithFrost(int days, int frostDay, float cold, float warm = 20f)
    {
        var a = Flat(days, warm);
        a[frostDay * 24 + 3] = cold;
        return a;
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "fields by the weather: " + what);
        }

        // ---------- Growth by the hour ----------
        C(FieldWeatherMath.Resting(0.1f) && !FieldWeatherMath.Resting(0.5f) && FieldWeatherMath.Resting(0.85f), "plants rest early in the morning and at night, as the game's do");
        C(Math.Abs(FieldWeatherMath.PerHour(5.8f) * 24f * 5.8f - 1f) < 0.0001f, "a grow day spread over 24 hours");
        C(FieldWeatherMath.EaseHour(5f, 0f) > 1.6f && FieldWeatherMath.EaseHour(5f, 0f) < 1.9f, "the weather's push keeps about a third of its gap after an hour (ten tenths of the way)");
        {
            float p = 4f;
            for (int i = 0; i < 24; i++) p = FieldWeatherMath.EaseHour(p, -1f);
            C(Math.Abs(p + 1f) < 0.01f, "after a day the push is what the weather makes it");
        }

        // ---------- The plants' own cold limits ----------
        var limits = FieldWeatherMath.Limits(0f);
        C(limits.Length == FieldWeatherMath.LimitBins, "eight shares of plants yet to be sown");
        C(limits.All(l => l > -18f && l < -10f), "1.6: a crop growing from 0 °C dies between -10 and -18 °C");
        C(Math.Abs(limits.Average() + 14f) < 0.01f, "evenly spread: the middle at -14 °C");
        C(FieldWeatherMath.Limits(6f).All(l => l > -12f && l < -4f), "a crop that needs more warmth dies sooner");
        C(FieldWeatherMath.Offsets(new List<float> { -12f, -15f }) == (FieldWeatherMath.MinLimitOffset, FieldWeatherMath.MaxLimitOffset), "a few plants seen: the game's range");
        var seen = Enumerable.Range(0, 10).Select(i => -11f - i * 0.5f).ToList();
        C(FieldWeatherMath.Offsets(seen) == (-15.5f, -11f), "enough plants seen: their own range");
        C(FieldWeatherMath.Offsets(Enumerable.Repeat(-60f, 10).ToList()).min >= -40f, "nothing past -40 below its growing temperature");

        // ---------- The yield of a plant harvested early ----------
        C(FieldWeatherMath.YieldShare(0.5f, 0.65f) == 0f, "below the least growth to harvest: nothing");
        C(Math.Abs(FieldWeatherMath.YieldShare(0.65f, 0.65f) - 0.5f) < 0.001f, "at the least growth to harvest: half the yield");
        C(FieldWeatherMath.YieldShare(1f, 0.65f) == 1f, "ripe: the full yield");
        C(Math.Abs(FieldWeatherMath.YieldShare(0.825f, 0.65f) - 0.75f) < 0.001f, "half way from the least growth to ripe: three quarters");
        {
            bool up = true;
            float prev = -1f;
            for (float g = 0f; g <= 1.0001f; g += 0.01f)
            {
                float y = FieldWeatherMath.YieldShare(g, 0.65f);
                if (y < prev) up = false;
                prev = y;
            }
            C(up, "the yield never falls as a plant grows");
        }

        // ---------- A crop sown at an hour ----------
        {
            var o = FieldWeatherMath.Simulate(Crop(Flat(60, 20f)), 1);
            C(Math.Abs(o.share - 1f) < 0.0001f && o.loss == FieldWeatherMath.LossNone, "no frost: the full harvest");
            float days = o.readyHour / 24f;
            C(days > 5.8f * 1.6f && days < 5.8f * 2f, $"resting at night, a 5.8-day crop takes about 1.8 times as long ({days:0.0} days)");
            var cool = FieldWeatherMath.Simulate(Crop(Flat(60, 3f)), 1);
            C(cool.readyHour > o.readyHour * 1.9f && cool.share > 0.99f, "at 3 °C it grows at half speed and still ripens");
        }
        {
            // A killing frost on day 3, long before the crop can be harvested.
            var o = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, -20f)), 1);
            C(o.share == 0f && o.killed == 1f && o.loss == FieldWeatherMath.LossFrost, "a frost below every limit before the least growth: all lost");
            C(o.frostHour == 3 * 24 + 3 - 1 && o.frostLow == -20f, "the frost's hour and cold are told");
        }
        {
            // A frost that only reaches the warmer limits: those plants die, the rest ripen.
            var o = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, -13f)), 1);
            C(Math.Abs(o.killed - 3f / 8f) < 0.001f && Math.Abs(o.share - 5f / 8f) < 0.001f, $"a frost of -13 °C kills the three shares with limits above it ({o.share:0.00})");
        }
        {
            // A frost after the least growth to harvest: harvested early, at its yield then.
            var ripeDay = FieldWeatherMath.Simulate(Crop(Flat(60, 20f)), 1).readyHour / 24;
            int frostDay = (int)(ripeDay * 0.85f);
            var o = FieldWeatherMath.Simulate(Crop(WithFrost(60, frostDay, -20f)), 1);
            C(o.early == 1f && o.killed == 0f && o.loss == FieldWeatherMath.LossEarly, "a frost when the crop can be harvested: harvested early, nothing killed");
            C(o.share > 0.5f && o.share < 1f && Math.Abs(o.share - o.earlyYield) < 0.0001f, $"at the yield it has then ({o.share:0.00})");
        }
        {
            // Colder frosts never bring more; a later frost never brings less (for young plants).
            bool colder = true, later = true;
            float prev = 2f;
            for (float t = -9f; t >= -20f; t -= 0.5f)
            {
                float s = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, t)), 1).share;
                if (s > prev + 0.0001f) colder = false;
                prev = s;
            }
            prev = -1f;
            for (int d = 1; d < 30; d++)
            {
                float s = FieldWeatherMath.Simulate(Crop(WithFrost(60, d, -20f)), 1).share;
                if (s < prev - 0.0001f) later = false;
                prev = s;
            }
            C(colder, "a colder frost never brings more");
            C(later, "a later frost never brings less");
        }
        {
            // The margin: a frost a degree above every limit counts with a margin of two.
            var plain = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, -9f)), 1);
            var careful = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, -9f), margin: 2f), 1);
            C(plain.share == 1f && careful.share < 1f, "the sowing margin makes a near frost count");
        }
        {
            // Too cold to ripen in its life: what it gives when it dies of age, at most.
            var o = FieldWeatherMath.Simulate(Crop(Flat(60, 1f), lifeHours: 20 * 24), 1);
            C(o.share < 0.6f && o.loss == FieldWeatherMath.LossSlow && o.readyHour < 0, $"too slow in the cold to ripen in its life ({o.share:0.00})");
            var shortForecast = FieldWeatherMath.Simulate(Crop(Flat(3, 20f)), 1);
            C(shortForecast.share == 0f && shortForecast.late == 1f, "past the forecast's end nothing is counted that isn't ripe enough to harvest");
        }
        {
            // A plant cold only makes leafless: never killed, only later.
            var o = FieldWeatherMath.Simulate(Crop(WithFrost(60, 3, -20f), dies: false), 1);
            var warm = FieldWeatherMath.Simulate(Crop(Flat(60, 20f), dies: false), 1);
            C(o.killed == 0f && Math.Abs(o.share - 1f) < 0.0001f, "a plant that only goes leafless comes through");
            C(o.readyHour >= warm.readyHour + 20, $"but it stops growing for a day ({o.readyHour - warm.readyHour} hours later)");
        }
        C(FieldWeatherMath.Simulate(new CropRun(), 0).share == 0f, "nothing to grow on: nothing expected");

        // ---------- The 60% rule, and when sowing pays again ----------
        C(FieldWeatherMath.WorthShare == 0.6f && FieldWeatherMath.Worth(0.6f) && !FieldWeatherMath.Worth(0.59f), "sow at 60% of a full harvest or more");
        {
            var f = FieldWeatherMath.Decide(Crop(Flat(120, 20f)));
            C(f.sow && f.againHours < 0, "a warm forecast: sow now");
        }
        {
            // A killing frost on day 3 at 3 at night: sowing now (at 1) loses it all, as does sowing at 1 on day 3; on day 4 nothing stands in the way.
            var f = FieldWeatherMath.Decide(Crop(WithFrost(120, 3, -20f)));
            C(!f.sow && f.now.share == 0f, "a frost before the crop can be harvested: sowing paused");
            C(f.againHours == 4 * 24 && f.againShare >= 0.99f, $"worth sowing again once the frost is past (in {f.againHours / 24} days)");
        }
        {
            // Frost every night for good: never worth it within the search.
            var f = FieldWeatherMath.Decide(Crop(Flat(120, -25f)));
            C(!f.sow && f.againHours < 0, "a land frozen for good: not within the search");
        }
        {
            // The search finds the first day that pays, not a later one.
            var temps = Flat(120, 20f);
            for (int d = 0; d < 10; d++) temps[d * 24 + 3] = -20f;
            var f = FieldWeatherMath.Decide(Crop(temps));
            C(f.againHours == 10 * 24, $"frosts for ten nights: worth sowing again once the last is past ({f.againHours / 24} days)");
        }

        // ---------- The player's desert: winter nights near the warmest 1.6 limits ----------
        {
            // Yearly mean 15.7 °C, winter mean about 2 °C, nights about 10 degrees under the day's mean and two colder
            // every third night: in midwinter from -8 to -10 °C. The runtime's sowing margin (1 °C and the record's
            // default 1.5) is under it.
            var temps = new float[120 * 24];
            for (int h = 0; h < temps.Length; h++)
            {
                int day = h / 24, hour = h % 24;
                double mean = 15.7 + 13.7 * Math.Cos(2 * Math.PI * (day - 30) / 60.0);
                temps[h] = (float)(mean + 10.0 * Math.Cos(2 * Math.PI * (hour - 15) / 24.0) - (day % 3 == 0 ? 2.0 : 0.0));
            }
            float coldest = temps.Min(), margin = FieldWeatherMath.SowMargin + FieldWeatherMath.StrayDefault;
            C(coldest >= -10.5f && coldest <= -7f, $"the desert's winter nights ({coldest:0.0} °C at the coldest)");
            var plain = FieldWeatherMath.Simulate(Crop(temps, lifeHours: 46 * 24), 0);
            C(plain.killed == 0f, "by the forecast itself no 1.6 crop dies of the desert's winter nights (Smart Farming's -3 °C line would cut every one)");
            var f = FieldWeatherMath.Decide(Crop(temps, lifeHours: 46 * 24, margin: margin), 0);
            C(f.sow && f.now.share < 1f, $"with the margin a few plants count as lost, and midwinter sowing still pays ({FieldWeatherMath.Percent(f.now.share)}%)");
            var cold = (float[])temps.Clone();
            for (int h = 0; h < cold.Length; h++) cold[h] -= 8f;
            var g = FieldWeatherMath.Decide(Crop(cold, lifeHours: 46 * 24, margin: margin), 0);
            C(!g.sow && g.now.loss == FieldWeatherMath.LossFrost && g.againHours > 0 && g.againHours < 30 * 24,
                $"eight degrees colder, midwinter sowing pauses until the nights warm ({g.againHours / 24} days, {FieldWeatherMath.Percent(g.againShare)}%)");
        }

        // ---------- The frost harvest ----------
        {
            var plants = new List<FrostPlant>();
            foreach (float l in FieldWeatherMath.Limits(0f))
            {
                plants.Add(new FrostPlant { limit = l, growth = 0.8f, harvestable = true });
                plants.Add(new FrostPlant { limit = l, growth = 0.3f, harvestable = false });
            }
            var temps = Flat(2, 5f);
            temps[20] = -12f;
            var pick = FieldWeatherMath.PickFrost(plants, temps, 0.65f);
            // At risk: limit + 1 above -12, so the limits -10.5 and -11.5 (and -12.5, with the margin).
            C(pick.harvest.Count == 3 && pick.young == 3 && pick.safe == 10 && pick.all == 16, $"each plant by its own limit, a degree to spare ({pick.harvest.Count} ripe, {pick.young} young, {pick.safe} safe)");
            C(pick.harvest.All(i => plants[i].harvestable), "only plants ripe enough to harvest are picked");
            C(pick.lowHour == 20 && pick.low == -12f && pick.deadlineHour == 20, "the coldest hour and the first hour a picked plant is at risk");
            C(Math.Abs(pick.yieldShare - FieldWeatherMath.YieldShare(0.8f, 0.65f)) < 0.001f, "their yield now, as a share of a full one");
            var roofed = plants.Select(p => new FrostPlant { limit = p.limit, growth = p.growth, harvestable = p.harvestable, roofed = true }).ToList();
            var none = FieldWeatherMath.PickFrost(roofed, temps, 0.65f);
            C(none.harvest.Count == 0 && none.all == 0, "never a plant under a roof");
            var mild = FieldWeatherMath.PickFrost(plants, Flat(2, 5f), 0.65f);
            C(mild.harvest.Count == 0 && mild.young == 0 && mild.safe == plants.Count, "no frost: nothing to harvest");
            var late = Flat(3, 5f);
            late[40] = -20f;
            C(FieldWeatherMath.PickFrost(plants, late, 0.65f).harvest.Count == 0, "a frost past the next 30 hours waits for its turn");
            var edge = new List<FrostPlant> { new FrostPlant { limit = -12.9f, growth = 1f, harvestable = true }, new FrostPlant { limit = -13.1f, growth = 1f, harvestable = true } };
            var e = Flat(1, 5f);
            e[5] = -12f;
            C(FieldWeatherMath.PickFrost(edge, e, 0.65f).harvest.SequenceEqual(new[] { 0 }), "the margin is one degree: -12.9 at risk at -12 °C, -13.1 not");
        }
        C(FieldWeatherMath.FrostWhen(8, 20) == FieldWeatherMath.Tonight, "at 20, a frost at 4: tonight");
        C(FieldWeatherMath.FrostWhen(18, 10) == FieldWeatherMath.Tonight, "at 10, a frost at 4 the next morning: tonight");
        C(FieldWeatherMath.FrostWhen(3, 2) == FieldWeatherMath.Tonight, "at 2 at night, a frost at 5: tonight");
        C(FieldWeatherMath.FrostWhen(28, 22) == FieldWeatherMath.TomorrowNight, "at 22, a frost at 2 the night after: tomorrow night");
        C(FieldWeatherMath.FrostWhen(4, 10) == FieldWeatherMath.Today && FieldWeatherMath.FrostWhen(24, 10) == FieldWeatherMath.Tomorrow, "a frost by day: today or tomorrow");
        C(FieldWeatherMath.NightIndex(5 * 24 + 20) == FieldWeatherMath.NightIndex(6 * 24 + 3), "one night from 20 to 3: one key");
        C(FieldWeatherMath.NightIndex(6 * 24 + 12) != FieldWeatherMath.NightIndex(6 * 24 + 3), "the next day's noon is another night's");

        // ---------- The forecast's parts and margins ----------
        {
            var t = FieldWeatherMath.Forecast(new[] { 1f, 2f, 3f, 4f }, new[] { -5f, 2f }, new[] { 2, int.MaxValue }, new[] { 0.5f, 0.5f, 0f, 0f });
            C(t.SequenceEqual(new[] { -1.5f, -0.5f, 5f, 6f }), "the land's reckoning, each condition while it lasts, the weather's push");
            C(FieldWeatherMath.Lows(new[] { 3f, -1f }, 1.5f).SequenceEqual(new[] { 1.5f, -2.5f }), "the cautious temperature: the margin under the forecast");
        }
        C(FieldWeatherMath.Stray(new List<float> { 3f }) == FieldWeatherMath.StrayDefault, "a day or two kept: the default margin");
        C(FieldWeatherMath.Stray(new List<float> { -2f, -1f, 0f, -3f }) == 0f, "days warmer than the reckoning add nothing");
        C(Math.Abs(FieldWeatherMath.Stray(Enumerable.Range(0, 10).Select(i => i * 0.5f).ToList()) - 3.5f) < 0.001f, "the colder of most days: the eighth of ten");
        C(FieldWeatherMath.Stray(Enumerable.Repeat(20f, 10).ToList()) == FieldWeatherMath.StrayMax, "never more than six degrees");
        C(FieldWeatherMath.Gap(5f, 5.9f) == 0f && Math.Abs(FieldWeatherMath.Gap(5f, 2f) - 3f) < 0.001f, "the forecast's hour off the map's own by more than a degree widens the margins");

        // ---------- Words ----------
        C(FieldWeatherMath.Percent(0.83f) == 85 && FieldWeatherMath.Percent(0.6f) == 60 && FieldWeatherMath.Percent(1.3f) == 100, "shares in whole fives of percent");
        C(FieldWeatherMath.Days(30) == 1 && FieldWeatherMath.Days(3) == 1 && FieldWeatherMath.Days(13 * 24) == 13, "days in words, at least one");
        C(FieldWeatherMath.ForecastDays * 24 >= (FieldWeatherMath.SearchDays + 46) * 24, "the forecast runs past the search and a potato's life after it");
        return n;
    }
}
