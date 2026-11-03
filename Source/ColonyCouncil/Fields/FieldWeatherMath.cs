using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// One crop as the field forecast grows it (<see cref="FieldWeatherMath.Simulate"/>): how it grows, how its plants
    /// die, and the forecast hour by hour from the forecast's first hour.
    /// </summary>
    public sealed class CropRun
    {
        /// <summary>The crop's grow days and the least growth it can be harvested at (the game's PlantProperties).</summary>
        public float growDays = 5.8f, harvestMinGrowth = 0.65f;
        /// <summary>The soil's factor on its growth (the game's GrowthRateFactorFor_Fertility, averaged over the field).</summary>
        public float fertility = 1f;
        /// <summary>A plant that goes leafless from cold dies (most crops); otherwise it only stops growing a day.</summary>
        public bool dieIfLeafless = true;
        /// <summary>Hours a plant lives before it dies of age (0: no limit).</summary>
        public int lifeHours;
        /// <summary>The plants' cold limits (°C): one a plant, or one for each even share of plants yet to be sown.</summary>
        public float[] limits;
        /// <summary>Each hour of the forecast: how fast it grows (warmth x light, 0 while resting), and the cautious temperature (the forecast less its margin).</summary>
        public float[] rate, low;
    }

    /// <summary>What a crop sown at one hour is expected to bring, and what costs it.</summary>
    public struct CropOutcome
    {
        /// <summary>The share of a full harvest expected (0 to 1).</summary>
        public float share;
        /// <summary>Shares of the plants that a frost kills young, that are harvested early before a frost, that ripen, and that don't ripen in their life or within the forecast.</summary>
        public float killed, early, ripe, late;
        /// <summary>Of the plants harvested early, or harvested unripe at the end, the share of a full yield each gives on average.</summary>
        public float earlyYield, lateYield;
        /// <summary>Hours from sowing until it is ripe (-1: not within its life or the forecast).</summary>
        public int readyHour;
        /// <summary>Hours from sowing to the first frost that costs plants, and how cold it is then (-1: none).</summary>
        public int frostHour;
        public float frostLow;
        /// <summary>What costs the most: <see cref="FieldWeatherMath.LossNone"/>, Frost, Early or Slow.</summary>
        public int loss;
    }

    /// <summary>A field's sowing by the weather: sow now or not, what sowing now brings, and when it would pay again.</summary>
    public sealed class FieldOutlook
    {
        public bool sow;
        public CropOutcome now;
        /// <summary>Hours from now until sowing is expected to bring enough again (-1: not within the search).</summary>
        public int againHours = -1;
        public float againShare;
    }

    /// <summary>One plant standing in a field, for the frost harvest.</summary>
    public struct FrostPlant
    {
        public float limit, growth;
        public bool harvestable, roofed;
    }

    /// <summary>Which plants to harvest before a frost: those ripe enough whose own limit the forecast reaches.</summary>
    public sealed class FrostPick
    {
        /// <summary>The plants to harvest now (indices into what was given).</summary>
        public List<int> harvest = new List<int>();
        /// <summary>Plants the frost would kill that are too young to harvest, those it spares, and all looked at (roofed ones aside).</summary>
        public int young, safe, all;
        /// <summary>The share of a full yield those harvested now give, on average.</summary>
        public float yieldShare;
        /// <summary>The first hour the forecast reaches a picked plant's limit (with the margin), and the coldest hour and its temperature.</summary>
        public int deadlineHour = -1, lowHour = -1;
        public float low = float.MaxValue;
    }

    /// <summary>
    /// Fields by the weather (Fields/FieldWeather.cs) as plain arithmetic, so Tools/Tests can check it. A crop is grown
    /// hour by hour over a forecast made from the game's own numbers: it rests at night as the game's plants do, grows by
    /// the warmth, the light and the soil at each hour, and each of its plants dies at the first hour colder than its own
    /// cold limit (in 1.6 a plant's least growing temperature plus an offset between -18 and -10 °C of its own), unless it
    /// is ripe enough to harvest then: such a plant counts as harvested early at its yield then, as a frost harvest would
    /// take it. A plant still unripe when it dies of age, or when the forecast ends, counts at what it would give then.
    /// A field set to sow by the weather sows while a crop sown now is expected to bring at least
    /// <see cref="WorthShare"/> of a full harvest; otherwise sowing there pauses and the days ahead are searched for
    /// when it would pay again. The frost harvest looks <see cref="FrostHours"/> ahead plant by plant.
    /// </summary>
    public static class FieldWeatherMath
    {
        public const int DayHours = 24;
        /// <summary>A field sows while a crop sown now is expected to bring at least this share of a full harvest.</summary>
        public const float WorthShare = 0.6f;
        /// <summary>The game's cold limits (1.6): a plant dies, or goes leafless, below its least growing temperature plus an offset of its own in this range.</summary>
        public const float MinLimitOffset = -18f, MaxLimitOffset = -10f;
        /// <summary>Plants yet to be sown are reckoned as this many even shares over the range of limits.</summary>
        public const int LimitBins = 8;
        /// <summary>At least this many plants of a crop on the map before their own limits stand in for the game's range.</summary>
        public const int LimitsSeen = 8;
        /// <summary>A newly sown plant's growth (the game's Plant.BaseSownGrowthPercent).</summary>
        public const float SownGrowth = 0.0001f;
        /// <summary>A plant that went leafless from cold doesn't grow for a day (the game's LeaflessMinRecoveryTicks).</summary>
        public const int LeaflessHours = 24;
        /// <summary>A plant rests (doesn't grow) before this share of the day and after that one (the game's Plant.Resting).</summary>
        public const float RestBefore = 0.25f, RestAfter = 0.8f;
        /// <summary>The frost harvest looks this many hours ahead, and takes a plant as at risk this much above its own limit.</summary>
        public const int FrostHours = 30;
        public const float FrostMargin = 1f;
        /// <summary>The sowing reckoning's margin under the forecast, before what the record of real nights adds.</summary>
        public const float SowMargin = 1f;
        /// <summary>When sowing would pay again is looked for this many days ahead, a day at a time.</summary>
        public const int SearchDays = 60;
        /// <summary>The forecast runs this many days: the search and a long crop's life after it.</summary>
        public const int ForecastDays = 120;
        /// <summary>
        /// How much colder than the land's own reckoning real days got at their coldest, kept for this many days; the
        /// sowing margin adds the colder of each <see cref="StrayShare"/> of them, at most <see cref="StrayMax"/>, and
        /// <see cref="StrayDefault"/> until <see cref="StrayLeast"/> days are kept.
        /// </summary>
        public const int StrayDays = 15, StrayLeast = 3;
        public const float StrayShare = 0.8f, StrayMax = 6f, StrayDefault = 1.5f;
        /// <summary>The forecast's first hour off the map's own temperature by more than this: the margins widen by the gap.</summary>
        public const float GapAllowed = 1f;

        /// <summary>What costs a harvest the most.</summary>
        public const int LossNone = 0, LossFrost = 1, LossEarly = 2, LossSlow = 3;

        /// <summary>When a frost comes, in words: tonight, tomorrow night, later today, tomorrow.</summary>
        public const int Tonight = 0, TomorrowNight = 1, Today = 2, Tomorrow = 3;

        /// <summary>The weather's push on the temperature eases a tenth of the way every 250 ticks: this much of the gap is left after an hour.</summary>
        public static readonly float EaseLeftAnHour = (float)Math.Pow(0.9, 10);

        // ---------------- Growth ----------------

        /// <summary>A plant rests at this share of the day (the game's plants don't grow early in the morning and at night).</summary>
        public static bool Resting(float dayPercent) => dayPercent < RestBefore || dayPercent > RestAfter;

        /// <summary>Growth an hour at the full rate: the game spreads a grow day over all the hours, the resting ones giving none.</summary>
        public static float PerHour(float growDays) => growDays <= 0f ? 1f : 1f / (DayHours * growDays);

        /// <summary>
        /// The share of a full harvest a plant gives when harvested at <paramref name="growth"/>: none below the least
        /// growth to harvest, half at it, all of it ripe (the game's Plant.YieldNow, before hit points and difficulty).
        /// </summary>
        public static float YieldShare(float growth, float harvestMinGrowth)
        {
            if (growth >= 1f) return 1f;
            if (growth < harvestMinGrowth) return 0f;
            float t = harvestMinGrowth >= 1f ? 1f : (growth - harvestMinGrowth) / (1f - harvestMinGrowth);
            return 0.5f + 0.5f * Math.Min(1f, Math.Max(0f, t));
        }

        /// <summary>The cold limits of plants yet to be sown: the least growing temperature plus offsets spread evenly over the range, the middle of each share.</summary>
        public static float[] Limits(float minGrowth, float minOffset = MinLimitOffset, float maxOffset = MaxLimitOffset, int bins = LimitBins)
        {
            bins = Math.Max(1, bins);
            if (maxOffset < minOffset) (minOffset, maxOffset) = (maxOffset, minOffset);
            var limits = new float[bins];
            float step = (maxOffset - minOffset) / bins;
            for (int i = 0; i < bins; i++) limits[i] = minGrowth + minOffset + step * (i + 0.5f);
            return limits;
        }

        /// <summary>
        /// The range of offsets the plants of a crop have, from their own limits on the map (limit less the least growing
        /// temperature) once <see cref="LimitsSeen"/> of them are known, else the game's range; nothing outside -40 to 0.
        /// </summary>
        public static (float min, float max) Offsets(IList<float> seen)
        {
            if (seen == null || seen.Count < LimitsSeen) return (MinLimitOffset, MaxLimitOffset);
            float lo = Math.Max(-40f, seen.Min()), hi = Math.Min(0f, seen.Max());
            return hi < lo ? (MinLimitOffset, MaxLimitOffset) : (lo, hi);
        }

        // ---------------- The forecast ----------------

        /// <summary>The weather's push an hour on: eased from what it is toward what the weather makes it.</summary>
        public static float EaseHour(float current, float target) => target + (current - target) * EaseLeftAnHour;

        /// <summary>
        /// The forecast for each hour: the land's own reckoning, each condition on the map at its offset for as long as it
        /// lasts (hours; int.MaxValue for one that doesn't end), and the weather's push.
        /// </summary>
        public static float[] Forecast(float[] baseTemp, IList<float> offsets, IList<int> hoursLeft, float[] push)
        {
            int n = baseTemp?.Length ?? 0;
            var temp = new float[n];
            for (int h = 0; h < n; h++)
            {
                float t = baseTemp[h];
                if (offsets != null)
                    for (int i = 0; i < offsets.Count; i++)
                        if (hoursLeft == null || i >= hoursLeft.Count || h < hoursLeft[i]) t += offsets[i];
                if (push != null && h < push.Length) t += push[h];
                temp[h] = t;
            }
            return temp;
        }

        /// <summary>The cautious temperature each hour: the forecast less the margin.</summary>
        public static float[] Lows(float[] temp, float margin)
        {
            var low = new float[temp?.Length ?? 0];
            for (int h = 0; h < low.Length; h++) low[h] = temp[h] - Math.Max(0f, margin);
            return low;
        }

        /// <summary>
        /// How much colder than the land's reckoning real days tend to get at their coldest, from the days kept: the colder
        /// of each <see cref="StrayShare"/> of them (a day warmer than the reckoning counts as none), at most
        /// <see cref="StrayMax"/>; <see cref="StrayDefault"/> until enough days are kept.
        /// </summary>
        public static float Stray(IList<float> days)
        {
            if (days == null || days.Count < StrayLeast) return StrayDefault;
            var list = days.Select(d => Math.Max(0f, d)).OrderBy(d => d).ToList();
            int i = Math.Min(list.Count - 1, Math.Max(0, (int)Math.Ceiling(StrayShare * list.Count) - 1));
            return Math.Min(StrayMax, list[i]);
        }

        /// <summary>The forecast's first hour against the map's own temperature now: the gap the margins widen by (0 within <see cref="GapAllowed"/>).</summary>
        public static float Gap(float forecastNow, float actualNow)
        {
            float d = Math.Abs(actualNow - forecastNow);
            return d > GapAllowed ? d : 0f;
        }

        // ---------------- A crop sown at an hour ----------------

        /// <summary>
        /// A crop sown at hour <paramref name="start"/> of the forecast, grown hour by hour to the end of its life or the
        /// forecast: what share of a full harvest it is expected to bring, when it is ripe, and what costs it.
        /// </summary>
        public static CropOutcome Simulate(CropRun run, int start)
        {
            var o = new CropOutcome { readyHour = -1, frostHour = -1 };
            int bins = run?.limits?.Length ?? 0;
            if (bins == 0 || run.rate == null || run.low == null) return o;
            int end = Math.Min(run.rate.Length, run.low.Length);
            if (start < 0 || start >= end) return o;
            if (run.lifeHours > 0 && start + run.lifeHours < end) end = start + run.lifeHours;
            float per = PerHour(run.growDays) * Math.Max(0f, run.fertility);
            float hmg = run.harvestMinGrowth;
            float killed = 0f, early = 0f, earlyYield = 0f, ripe = 0f, late = 0f, lateYield = 0f;
            if (run.dieIfLeafless)
            {
                // All the plants grow alike; each dies at the first hour colder than its own limit, the warmest first.
                var limits = (float[])run.limits.Clone();
                Array.Sort(limits);
                Array.Reverse(limits);
                int next = 0;
                float g = SownGrowth;
                for (int h = start; h < end && next < bins; h++)
                {
                    float low = run.low[h];
                    while (next < bins && low < limits[next])
                    {
                        if (o.frostHour < 0)
                        {
                            o.frostHour = h - start;
                            o.frostLow = low;
                        }
                        if (g >= hmg)
                        {
                            early++;
                            earlyYield += YieldShare(g, hmg);
                        }
                        else killed++;
                        next++;
                    }
                    if (next >= bins) break;
                    g += run.rate[h] * per;
                    if (g >= 1f)
                    {
                        ripe = bins - next;
                        o.readyHour = h + 1 - start;
                        next = bins;
                    }
                }
                int left = bins - next;
                if (left > 0) Late(left, g, hmg, ref late, ref lateYield);
            }
            else
            {
                // A plant that only goes leafless: each on its own, a cold hour stops its growth for a day.
                var ready = new List<int>();
                foreach (float limit in run.limits)
                {
                    float g = SownGrowth;
                    int stillTill = -1;
                    bool done = false;
                    for (int h = start; h < end; h++)
                    {
                        if (run.low[h] < limit)
                        {
                            stillTill = h + LeaflessHours;
                            if (o.frostHour < 0)
                            {
                                o.frostHour = h - start;
                                o.frostLow = run.low[h];
                            }
                        }
                        if (h >= stillTill) g += run.rate[h] * per;
                        if (g >= 1f)
                        {
                            ripe++;
                            ready.Add(h + 1 - start);
                            done = true;
                            break;
                        }
                    }
                    if (!done) Late(1, g, hmg, ref late, ref lateYield);
                }
                if (ready.Count > 0)
                {
                    ready.Sort();
                    o.readyHour = ready[ready.Count / 2];
                }
            }
            o.killed = killed / bins;
            o.early = early / bins;
            o.ripe = ripe / bins;
            o.late = late / bins;
            o.earlyYield = early > 0f ? earlyYield / early : 0f;
            o.lateYield = late > 0f ? lateYield / late : 0f;
            o.share = (ripe + earlyYield + lateYield) / bins;
            o.loss = LossOf(killed, early - earlyYield, late - lateYield);
            return o;
        }

        /// <summary>Plants unripe at the end of their life or of the forecast: harvested at what they give then if they can be, else nothing.</summary>
        private static void Late(int n, float g, float hmg, ref float late, ref float lateYield)
        {
            late += n;
            if (g >= hmg) lateYield += n * YieldShare(g, hmg);
        }

        /// <summary>What costs the most of a harvest: plants killed young, plants harvested early, or plants that didn't ripen.</summary>
        public static int LossOf(float killed, float earlyLost, float lateLost)
        {
            const float none = 0.0001f;
            if (killed <= none && earlyLost <= none && lateLost <= none) return LossNone;
            if (killed >= earlyLost && killed >= lateLost) return LossFrost;
            return earlyLost >= lateLost ? LossEarly : LossSlow;
        }

        /// <summary>A crop is worth sowing when it is expected to bring at least <see cref="WorthShare"/> of a full harvest.</summary>
        public static bool Worth(float share) => share >= WorthShare - 0.0001f;

        /// <summary>
        /// A field's outlook: the crop sown at <paramref name="start"/>; if that isn't worth it, the days after searched,
        /// a day at a time for <see cref="SearchDays"/>, for the first sowing that would be.
        /// </summary>
        public static FieldOutlook Decide(CropRun run, int start = 1, int searchDays = SearchDays)
        {
            var f = new FieldOutlook { now = Simulate(run, start) };
            f.sow = Worth(f.now.share);
            if (f.sow) return f;
            int n = Math.Min(run?.rate?.Length ?? 0, run?.low?.Length ?? 0);
            for (int d = 1; d <= searchDays && start + d * DayHours < n; d++)
            {
                var o = Simulate(run, start + d * DayHours);
                if (!Worth(o.share)) continue;
                f.againHours = d * DayHours;
                f.againShare = o.share;
                break;
            }
            return f;
        }

        // ---------------- The frost harvest ----------------

        /// <summary>
        /// The plants to harvest before a frost: of those not under a roof, each whose own limit (with the margin) the
        /// forecast for the next <paramref name="hours"/> reaches and that is ripe enough to harvest; the young ones it
        /// would kill and those it spares counted.
        /// </summary>
        public static FrostPick PickFrost(IList<FrostPlant> plants, float[] temp, float harvestMinGrowth, int hours = FrostHours, float margin = FrostMargin)
        {
            var p = new FrostPick();
            int n = Math.Min(temp?.Length ?? 0, hours + 1);
            for (int h = 0; h < n; h++)
                if (temp[h] < p.low)
                {
                    p.low = temp[h];
                    p.lowHour = h;
                }
            if (plants == null || n == 0) return p;
            float sum = 0f;
            for (int i = 0; i < plants.Count; i++)
            {
                var x = plants[i];
                if (x.roofed) continue;
                p.all++;
                float line = x.limit + margin;
                if (!(p.low < line))
                {
                    p.safe++;
                    continue;
                }
                if (!x.harvestable)
                {
                    p.young++;
                    continue;
                }
                p.harvest.Add(i);
                sum += YieldShare(x.growth, harvestMinGrowth);
                for (int h = 0; h < n; h++)
                    if (temp[h] < line)
                    {
                        if (p.deadlineHour < 0 || h < p.deadlineHour) p.deadlineHour = h;
                        break;
                    }
            }
            p.yieldShare = p.harvest.Count > 0 ? sum / p.harvest.Count : 0f;
            return p;
        }

        /// <summary>When a frost <paramref name="hoursAhead"/> comes, at <paramref name="hourNow"/> o'clock now: tonight (a night runs from 18 to 9), tomorrow night, later today or tomorrow.</summary>
        public static int FrostWhen(int hoursAhead, int hourNow)
        {
            int at = Math.Max(0, hourNow) + Math.Max(0, hoursAhead);
            int day = at / DayHours, hour = at % DayHours;
            if (hour >= 18 || hour < 9)
            {
                int night = hour >= 18 ? day : day - 1;
                return night <= 0 ? Tonight : TomorrowNight;
            }
            return day == 0 ? Today : Tomorrow;
        }

        /// <summary>Which night an hour belongs to, counting from the land's hours since the start (a night runs from 18 to 9): the same frost keeps its key.</summary>
        public static long NightIndex(long localHours) => (long)Math.Floor((localHours + 15) / (double)DayHours);

        /// <summary>A share in whole fives of percent, for words: 0.83 → 85.</summary>
        public static int Percent(float share) => (int)Math.Round(Math.Min(1f, Math.Max(0f, share)) * 20f) * 5;

        /// <summary>Whole days for words, at least one: 30 hours → 1.</summary>
        public static int Days(int hours) => Math.Max(1, (int)Math.Round(hours / (double)DayHours));
    }
}
