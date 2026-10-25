using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Weather that changes like weather (Weather/WeatherDynamicsMath.cs), over every kind of weather and land: every
/// factor stays in bounds whatever the combination (land, season, hour, temperature, spells, fronts, droughts, events,
/// the year), the special weathers and flooding downpours are never made likelier, a drought never brings more rain,
/// Seasonal Weather's lands keep their own seasons, the temperature moves only a few degrees and the right way, a
/// morning fog burns off, and a hundred simulated years on eight lands keep each land's own character: no land drowns
/// or dries out, spells come and end, and a desert stays a desert.
/// </summary>
public static class WeatherDynamicsTests
{
    private struct Land
    {
        public string name;
        public BiomeWeather b;
        public (string def, WeatherKind kind, float weight, float minT, float maxT, bool storm)[] weathers;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "weather dynamics: " + what);
        }

        // ---------- What a weather is ----------
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { rainRate = 1f }) == WeatherKind.Rain, "rain is rain");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { rainRate = 1f, snowRate = 1f }) == WeatherKind.Snow, "the game's snows count rain too, and are snow");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { rainRate = 2f }) == WeatherKind.Downpour, "a flooding rain is a downpour");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { storm = true, rainRate = 1f }) == WeatherKind.Storm
              && WeatherDynamicsMath.KindOf(new WeatherTraits { storm = true, snowRate = 1.2f, rainRate = 1f }) == WeatherKind.Storm, "a thunderstorm, and a snowy one (Thunder Snow), are storms");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { storm = true }) == WeatherKind.DryStorm, "dry lightning and a duststorm are dry storms");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { fog = true, rainRate = 1f }) == WeatherKind.Rain && WeatherDynamicsMath.KindOf(new WeatherTraits { fog = true }) == WeatherKind.Fog,
            "foggy rain is rain, fog is fog");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { windy = true }) == WeatherKind.Windy && WeatherDynamicsMath.KindOf(new WeatherTraits { cloudy = true }) == WeatherKind.Cloudy
              && WeatherDynamicsMath.KindOf(new WeatherTraits { dust = true }) == WeatherKind.Dust && WeatherDynamicsMath.KindOf(new WeatherTraits()) == WeatherKind.Clear,
            "Seasonal Weather's windy, cloudy and dust, and plain clear skies");
        Check(WeatherDynamicsMath.KindOf(new WeatherTraits { special = true, rainRate = 1f, storm = true }) == WeatherKind.Special, "a special weather stays special whatever it does");

        // ---------- The lands ----------
        var lands = Lands();
        var kinds = (WeatherKind[])Enum.GetValues(typeof(WeatherKind));

        // ---------- Every combination, in bounds ----------
        var rnd = new System.Random(1234);
        bool bounded = true, special = true, downpour = true, drought = true, shaped = true, noFogInWind = true, stormsNeedHeat = true;
        int combos = 0;
        foreach (var land in lands)
            for (int i = 0; i < 25000; i++)
            {
                var n = RandomNow(rnd, kinds);
                var c = kinds[rnd.Next(kinds.Length)];
                float f = WeatherDynamicsMath.Factor(c, n, land.b);
                combos++;
                if (f < WeatherDynamicsMath.MinFactor - 1e-4f || f > WeatherDynamicsMath.MaxFactor + 1e-4f || float.IsNaN(f)) bounded = false;
                if (c == WeatherKind.Special && Math.Abs(f - 1f) > 1e-4f) special = false;
                if (c == WeatherKind.Downpour && f > 1f + 1e-4f) downpour = false;
                if (n.drought && WeatherDynamicsMath.Wet(c) && f > 1f + 1e-4f) drought = false;
                if ((land.b.seasonsShaped || land.b.Frozen) && Math.Abs(WeatherDynamicsMath.Season(c, n, land.b) - 1f) > 1e-4f) shaped = false;
                if (n.current == WeatherKind.Windy && c == WeatherKind.Fog && WeatherDynamicsMath.FollowOn(c, n, land.b) >= 1f && n.tempFall < 5f) noFogInWind = false;
                if (n.current == WeatherKind.Clear && c == WeatherKind.Storm && n.tempFall < 5f && (n.hour < 12 || n.temp < 24f) && WeatherDynamicsMath.FollowOn(c, n, land.b) >= 1f) stormsNeedHeat = false;
            }
        Check(bounded, $"every factor stays within {WeatherDynamicsMath.MinFactor} and {WeatherDynamicsMath.MaxFactor} ({combos} combinations of land, weather, season, hour, temperature, spells, fronts, droughts, events and year)");
        Check(special, "the special weathers (a pall, blood rain, acid fog, a double rainbow) are left exactly as they are");
        Check(downpour, "a flooding downpour is never made likelier");
        Check(drought, "in a drought nothing makes rain likelier");
        Check(shaped, "a land Seasonal Weather gives seasons to, and a frozen one, keeps its seasons as they are");
        Check(noFogInWind, "no fog follows wind");
        Check(stormsNeedHeat, "a storm out of a clear sky only on a hot afternoon (or along a cold front)");

        // ---------- Shared out among the ordinary weathers ----------
        bool sharedBounded = true, sharedSpecial = true, sharedCaps = true, sharedKept = true;
        foreach (var land in lands)
        {
            var ks = land.weathers.Select(x => x.kind).ToArray();
            var fs = new float[ks.Length];
            for (int i = 0; i < 4000; i++)
            {
                var n = RandomNow(rnd, kinds);
                var ws = land.weathers.Select(x => rnd.Next(0, 5) == 0 ? 0f : x.weight).ToArray();
                WeatherDynamicsMath.Shared(ks, ws, n, land.b, fs);
                float before = 0f, after = 0f, special0 = 0f;
                bool clamped = false;
                for (int j = 0; j < ks.Length; j++)
                {
                    float f = fs[j];
                    if (f < WeatherDynamicsMath.MinFactor - 1e-4f || f > WeatherDynamicsMath.MaxFactor + 1e-4f || float.IsNaN(f)) sharedBounded = false;
                    if (ks[j] == WeatherKind.Special) { if (Math.Abs(f - 1f) > 1e-4f) sharedSpecial = false; special0 += ws[j]; continue; }
                    if ((ks[j] == WeatherKind.Downpour || (n.drought && WeatherDynamicsMath.Wet(ks[j]))) && f > 1f + 1e-4f) sharedCaps = false;
                    if (f <= WeatherDynamicsMath.MinFactor + 1e-4f || f >= WeatherDynamicsMath.MaxFactor - 1e-4f || f >= 1f - 1e-4f && (ks[j] == WeatherKind.Downpour || n.drought && WeatherDynamicsMath.Wet(ks[j]))) clamped = true;
                    before += ws[j];
                    after += ws[j] * f;
                }
                if (!clamped && before > 0f && special0 > 0f && Math.Abs(after / before - 1f) > 0.02f) sharedKept = false;
            }
        }
        Check(sharedBounded, "shared out among the weathers that could come, every factor stays in bounds");
        Check(sharedSpecial && sharedKept, "the ordinary weathers keep the chance they had together, so the special ones keep exactly theirs");
        Check(sharedCaps, "and still no downpour, and no rain in a drought, is made likelier");

        // ---------- Spells ----------
        var temperate = lands.First(l => l.name == "temperate forest").b;
        var desert = lands.First(l => l.name == "desert").b;
        var wetNow = WeatherNow.Calm;
        wetNow.wetRun = 3;
        Check(WeatherDynamicsMath.Spell(WeatherKind.Rain, wetNow, temperate) > 1f && WeatherDynamicsMath.Spell(WeatherKind.Clear, wetNow, temperate) < 1f, "after three wet days, rain is likelier and clear skies less so");
        wetNow.wetRun = WeatherDynamicsMath.WetSpellEnds(temperate);
        Check(WeatherDynamicsMath.Spell(WeatherKind.Rain, wetNow, temperate) < 1f && WeatherDynamicsMath.Spell(WeatherKind.Clear, wetNow, temperate) > 1f, "a long wet spell breaks");
        Check(WeatherDynamicsMath.WetSpellEnds(desert) < WeatherDynamicsMath.WetSpellEnds(temperate) && WeatherDynamicsMath.DrySpellBreaks(desert) > WeatherDynamicsMath.DrySpellBreaks(temperate),
            "a desert's wet spells end sooner and its dry spells run longer");
        var dryNow = WeatherNow.Calm;
        dryNow.dryRun = 5;
        Check(WeatherDynamicsMath.Spell(WeatherKind.Rain, dryNow, temperate) < 1f, "a dry spell holds");
        dryNow.dryRun = WeatherDynamicsMath.DrySpellBreaks(temperate);
        dryNow.temp = 20f;
        Check(WeatherDynamicsMath.Spell(WeatherKind.Storm, dryNow, temperate) > 1.5f, "a long dry spell breaks with a storm");
        dryNow.temp = 0f;
        Check(WeatherDynamicsMath.Spell(WeatherKind.Storm, dryNow, temperate) < 1f, "but not in the cold");
        dryNow.temp = 20f;
        dryNow.drought = true;
        Check(WeatherDynamicsMath.Factor(WeatherKind.Storm, dryNow, temperate) <= 1f, "and not in a drought");

        // ---------- What follows on ----------
        var n2 = WeatherNow.Calm;
        n2.current = WeatherKind.Storm;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Rain, n2, temperate) > 1f && WeatherDynamicsMath.FollowOn(WeatherKind.Storm, n2, temperate) < 1f, "a storm settles into rain, and seldom straight into another");
        n2.current = WeatherKind.Clear;
        n2.hour = 6;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Fog, n2, temperate) > 1f, "fog after a clear night");
        n2.hour = 15;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Fog, n2, temperate) < 1f, "but seldom by day");
        n2.current = WeatherKind.Rain;
        n2.hour = 5;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Fog, n2, temperate) > 1f, "and fog after rain in the morning");
        var front = WeatherNow.Calm;
        front.tempFall = 8f;
        front.temp = -3f;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Snow, front, temperate) > 1.3f, "snow behind a cold front");
        front.temp = 12f;
        Check(WeatherDynamicsMath.FollowOn(WeatherKind.Storm, front, temperate) > 1f, "a storm along a front where it's still warm");

        // ---------- Events, the year, the seasons ----------
        var rains = WeatherNow.Calm;
        rains.eventRain = 2f;
        rains.eventClear = 0.6f;
        Check(WeatherDynamicsMath.Factor(WeatherKind.Rain, rains, temperate) > 1.5f && WeatherDynamicsMath.Factor(WeatherKind.Clear, rains, temperate) < 0.8f, "the long rains bring rain");
        var heat = WeatherNow.Calm;
        heat.eventClear = 1.3f;
        heat.eventRain = 0.8f;
        Check(WeatherDynamicsMath.Factor(WeatherKind.Clear, heat, temperate) > 1f && WeatherDynamicsMath.Factor(WeatherKind.Rain, heat, temperate) < 1f, "a heat wave brings clear skies");
        var wetYear = WeatherNow.Calm;
        wetYear.yearWet = 0.2f;
        Check(WeatherDynamicsMath.Year(WeatherKind.Rain, wetYear, temperate) > 1f && WeatherDynamicsMath.Year(WeatherKind.Clear, wetYear, temperate) < 1f, "a wet year has more rain");
        var tropical = lands.First(l => l.name == "tropical rainforest (no Seasonal Weather)").b;
        int wetQuads = Enumerable.Range(0, 4).Count(q => WeatherDynamicsMath.WetQuadrum(q, 7));
        Check(wetQuads == 2 && Enumerable.Range(-5, 20).All(s => Enumerable.Range(0, 4).Count(q => WeatherDynamicsMath.WetQuadrum(q, s)) == 2), "a tropical land has a wet season of two quadrums, whatever the tile");
        var wetSeason = WeatherNow.Calm;
        wetSeason.seed = 7;
        wetSeason.quadrum = Enumerable.Range(0, 4).First(q => WeatherDynamicsMath.WetQuadrum(q, 7));
        var drySeason = wetSeason;
        drySeason.quadrum = Enumerable.Range(0, 4).First(q => !WeatherDynamicsMath.WetQuadrum(q, 7));
        Check(WeatherDynamicsMath.Season(WeatherKind.Rain, wetSeason, tropical) > 1f && WeatherDynamicsMath.Season(WeatherKind.Rain, drySeason, tropical) < 1f, "rain in the wet season, little in the dry");
        var shapedLand = tropical;
        shapedLand.seasonsShaped = true;
        Check(WeatherDynamicsMath.Season(WeatherKind.Rain, wetSeason, shapedLand) == 1f, "Seasonal Weather's rainforest keeps its own seasons");

        // ---------- Temperature ----------
        bool tempBounded = true;
        foreach (var land in lands)
            foreach (var k in kinds)
                foreach (bool night in new[] { false, true })
                    foreach (float temp in new[] { -30f, 0f, 14f, 30f })
                        foreach (float snow in new[] { 0f, 0.5f })
                            foreach (float year in new[] { -1.2f, 0f, 1.2f })
                            {
                                float t = WeatherDynamicsMath.TempTarget(k, k == WeatherKind.Rain ? 1f : 0f, night, night ? 0f : 1f, land.b, snow, temp, year);
                                if (t < -WeatherDynamicsMath.MaxCooling - 1e-4f || t > WeatherDynamicsMath.MaxWarming + 1e-4f) tempBounded = false;
                            }
        Check(tempBounded, $"the weather moves the temperature at most {WeatherDynamicsMath.MaxCooling} °C down and {WeatherDynamicsMath.MaxWarming} °C up, on every land");
        Check(WeatherDynamicsMath.TempTarget(WeatherKind.Rain, 1f, false, 0.4f, temperate, 0f, 25f, 0f) < -2f, "rain cools a warm day");
        Check(WeatherDynamicsMath.TempTarget(WeatherKind.Clear, 0f, true, 0f, desert, 0f, 10f, 0f) < WeatherDynamicsMath.TempTarget(WeatherKind.Clear, 0f, true, 0f, temperate, 0f, 10f, 0f)
              && WeatherDynamicsMath.TempTarget(WeatherKind.Clear, 0f, true, 0f, temperate, 0f, 10f, 0f) < 0f, "a clear night is cold, and colder in the desert");
        Check(WeatherDynamicsMath.TempTarget(WeatherKind.Cloudy, 0f, true, 0f, temperate, 0f, 10f, 0f) > 0f, "a cloudy night is milder");
        Check(WeatherDynamicsMath.TempTarget(WeatherKind.Special, 1f, true, 0f, desert, 0.5f, 10f, 0.7f) == 0.7f, "a special weather moves nothing but the year's own lean");
        float eased = 0f;
        for (int i = 0; i < 30; i++) eased = WeatherDynamicsMath.Ease(eased, -3f);
        Check(eased < -2.7f && eased > -3f && WeatherDynamicsMath.Ease(0f, -3f) > -0.5f, "the temperature eases toward it over a couple of hours, not at once");

        // ---------- Fog ----------
        int hour = WeatherDynamicsMath.TicksPerHour;
        Check(WeatherDynamicsMath.FogTicks(6, 100 * hour, temperate) == 5 * hour, "a fog at six burns off by eleven");
        Check(WeatherDynamicsMath.FogTicks(10, 100 * hour, temperate) == 4 * hour, "a fog by day passes in a few hours");
        Check(WeatherDynamicsMath.FogTicks(23, 100 * hour, temperate) == 12 * hour && WeatherDynamicsMath.FogTicks(23, 5 * hour, temperate) == 5 * hour, "a fog at night lasts till late morning, never longer than the game had it");
        Check(WeatherDynamicsMath.FogTicks(6, 100 * hour, lands.First(l => l.b.Frozen).b) == null, "on frozen land, fog lasts as the game has it");

        // ---------- Forecast, the year's words ----------
        Check(WeatherDynamicsMath.ForecastAccuracy(0, 20f, false) >= 0.5f && WeatherDynamicsMath.ForecastAccuracy(12, 60f, false) <= 0.9f
              && WeatherDynamicsMath.ForecastAccuracy(10, 30f, false) > WeatherDynamicsMath.ForecastAccuracy(6, 30f, false) && WeatherDynamicsMath.ForecastAccuracy(0, 20f, true) >= 0.95f,
            "a forecast is as good as the one who makes it, and a weather station better");
        Check(!WeatherDynamicsMath.ReadsWeather(2, 30f, false) && WeatherDynamicsMath.ReadsWeather(6, 30f, false) && WeatherDynamicsMath.ReadsWeather(3, 60f, false) && WeatherDynamicsMath.ReadsWeather(0, 20f, true),
            "who can read the weather");
        var y1 = WeatherDynamicsMath.YearOf(55, 5502, temperate);
        Check(y1 == WeatherDynamicsMath.YearOf(55, 5502, temperate) && y1 != WeatherDynamicsMath.YearOf(55, 5503, temperate), "a year's character is the same every time it's asked, and differs from year to year");
        bool yearsBounded = Enumerable.Range(0, 400).All(y => Math.Abs(WeatherDynamicsMath.YearOf(9, y, temperate).wet) <= 0.22f + 1e-4f && Math.Abs(WeatherDynamicsMath.YearOf(9, y, temperate).temp) <= 1.2f + 1e-4f);
        float desertSpread = Enumerable.Range(0, 400).Max(y => Math.Abs(WeatherDynamicsMath.YearOf(9, y, desert).wet));
        Check(yearsBounded && desertSpread > 0.22f && desertSpread <= 0.33f + 1e-4f, "years lean a little (a dry land's more), never far");
        Check(WeatherDynamicsMath.YearWords(30, 20f, 2, 0f).wet == "wet" && WeatherDynamicsMath.YearWords(12, 20f, 2, 0f).wet == "dry" && WeatherDynamicsMath.YearWords(22, 20f, 2, 0.5f) == (null, null)
              && WeatherDynamicsMath.YearWords(30, 0f, 0, -1.5f) == (null, "cold"), "a year is called wet, dry, warm or cold only when it was, against the years before and the land's usual");

        // ---------- Events' pulls, the year measured ----------
        Check(WeatherDynamicsMath.TryParsePull("rain:1.8:5", out string pk, out float pf, out float pd) && pk == "rain" && Math.Abs(pf - 1.8f) < 1e-4f && Math.Abs(pd - 5f) < 1e-4f
              && WeatherDynamicsMath.TryParsePull("Clear:0.6:2.5", out pk, out _, out pd) && pk == "clear" && Math.Abs(pd - 2.5f) < 1e-4f,
            "an event's pull on the weather reads as its def gives it");
        Check(new[] { "", "rain", "rain:2", "hail:2:3", "rain:x:3", "rain:0:3", "rain:2:0", "rain:9:3", "rain:2:90", "rain:2:3:4" }.All(x => !WeatherDynamicsMath.TryParsePull(x, out _, out _, out _)),
            "and nothing else does");
        // Every event's weatherAfter in the defs reads.
        int pullsSeen = 0;
        foreach (var file in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Defs"), "*.xml", System.IO.SearchOption.AllDirectories))
        {
            var doc = System.Xml.Linq.XDocument.Load(file);
            foreach (var el in doc.Descendants("weatherAfter"))
                foreach (var li in el.Elements("li"))
                {
                    pullsSeen++;
                    Check(WeatherDynamicsMath.TryParsePull(li.Value, out _, out _, out _), $"{System.IO.Path.GetFileName(file)}: weatherAfter \"{li.Value}\" reads");
                }
        }
        Check(pullsSeen >= 10, $"the events' pulls on the weather are found ({pullsSeen})");
        var pulled = WeatherNow.Calm;
        WeatherDynamicsMath.AddPull(ref pulled, "rain", 1.8f);
        WeatherDynamicsMath.AddPull(ref pulled, "rain", 1.5f);
        WeatherDynamicsMath.AddPull(ref pulled, "clear", 0.6f);
        Check(Math.Abs(pulled.eventRain - 2.7f) < 1e-4f && Math.Abs(pulled.eventClear - 0.6f) < 1e-4f && WeatherDynamicsMath.Events(WeatherKind.Rain, pulled) <= 3f
              && WeatherDynamicsMath.Factor(WeatherKind.Rain, pulled, temperate) <= WeatherDynamicsMath.MaxFactor, "pulls add up, within bounds");
        var kept = new List<WeatherQuarter>();
        for (int y = 5500; y < 5503; y++)
            for (int q = 0; q < 4; q++)
                kept.Add(new WeatherQuarter { year = y, quadrum = q, days = 15, wetDays = y == 5502 ? 6 : 3, meanSum = 15 * (y == 5502 ? 12f : 10f), expectedSum = 150f });
        Check(WeatherDynamicsMath.YearWordsFor(kept, 5502) == ("wet", "warm") && WeatherDynamicsMath.YearWordsFor(kept, 5501) == (null, null),
            "a year measured against the years kept before it");
        Check(WeatherDynamicsMath.YearWordsFor(kept.Where(q => q.year == 5502 && q.quadrum < 2).ToList(), 5502) == (null, null), "half a year isn't judged");

        // ---------- Runs with today in ----------
        var past = new List<WeatherDay>
        {
            new WeatherDay { day = 1, hours = 24, rain = 10 }, new WeatherDay { day = 2, hours = 24, rain = 8 }
        };
        WeatherMath.RunsWithToday(past, new WeatherDay { day = 3, hours = 5, rain = 5 }, out int rw, out int rd, out _);
        Check(rw == 3 && rd == 0, "a wet morning adds to the wet run");
        WeatherMath.RunsWithToday(past, new WeatherDay { day = 3, hours = 6 }, out rw, out rd, out _);
        Check(rw == 2 && rd == 0, "an early dry morning breaks nothing yet");
        WeatherMath.RunsWithToday(past, new WeatherDay { day = 3, hours = 14 }, out rw, out rd, out _);
        Check(rw == 0 && rd == 1, "half a day with nothing falling ends the wet run and starts a dry one");
        WeatherMath.RunsWithToday(past, null, out rw, out _, out _);
        Check(rw == 2, "before today is looked at, the days before stand");

        // ---------- A hundred years on each land ----------
        foreach (var land in lands)
        {
            var plain = Simulate(land, false, 99, 100);
            var dyn = Simulate(land, true, 99, 100);
            string what = land.name;
            float ratio = plain.wetShare > 0f ? dyn.wetShare / plain.wetShare : 1f;
            // Where it hardly ever rains a few days either way is chance, not a change of climate.
            Check(plain.wetShare < 0.02f ? Math.Abs(dyn.wetShare - plain.wetShare) <= 0.006f : ratio > 0.75f && ratio < 1.33f,
                $"{what}: about as much rain over a hundred years as the game alone gives ({dyn.wetShare:P1} of days against {plain.wetShare:P1})");
            Check(dyn.maxDry <= Math.Max(plain.maxDry * 2 + 6, 20) && dyn.maxWet <= Math.Max(plain.maxWet * 2 + 3, 8), $"{what}: spells end (longest dry {dyn.maxDry} days against {plain.maxDry}, wet {dyn.maxWet} against {plain.maxWet})");
            // A few in a hundred years: as often as before means within what chance gives.
            Check(Math.Abs(dyn.special - plain.special) <= 2 * Math.Sqrt(dyn.special + plain.special) + 1, $"{what}: the special weathers come as often as before ({dyn.special} against {plain.special})");
        }
        var arid = Simulate(lands.First(l => l.name == "extreme desert"), true, 5);
        Check(arid.wetShare < 0.05f, $"an extreme desert stays one ({arid.wetShare:P1} wet days)");
        var dyTemperate = Simulate(lands.First(l => l.name == "temperate forest"), true, 7, 100);
        var plTemperate = Simulate(lands.First(l => l.name == "temperate forest"), false, 7, 100);
        Check(dyTemperate.meanWetRun > plTemperate.meanWetRun, $"spells hold: wet spells last longer than the game's own coin tosses ({dyTemperate.meanWetRun:0.00} days against {plTemperate.meanWetRun:0.00})");
        var dyTrop = Simulate(lands.First(l => l.name == "tropical rainforest (no Seasonal Weather)"), true, 11, 100);
        Check(dyTrop.wetSeasonShare > dyTrop.drySeasonShare * 1.5f, $"a tropical land without Seasonal Weather has a wet season ({dyTrop.wetSeasonShare:P0} wet days against {dyTrop.drySeasonShare:P0} in the dry)");
        return cases;
    }

    private static WeatherNow RandomNow(System.Random r, WeatherKind[] kinds)
    {
        float[] evs = { 0.3f, 0.6f, 1f, 1.5f, 3f };
        return new WeatherNow
        {
            current = kinds[r.Next(kinds.Length)],
            wetRun = r.Next(0, 14),
            dryRun = r.Next(0, 20),
            snowRun = r.Next(0, 8),
            hour = r.Next(0, 24),
            quadrum = r.Next(0, 4),
            season = r.Next(1, 7),
            temp = r.Next(-40, 45),
            tempFall = r.Next(0, 3) == 0 ? r.Next(5, 15) : 0f,
            drought = r.Next(0, 4) == 0,
            yearWet = (float)(r.NextDouble() * 0.66 - 0.33),
            eventRain = evs[r.Next(evs.Length)],
            eventClear = evs[r.Next(evs.Length)],
            eventStorm = evs[r.Next(evs.Length)],
            eventSnow = evs[r.Next(evs.Length)],
            eventFog = evs[r.Next(evs.Length)],
            seed = r.Next()
        };
    }

    private static Land L(string name, float rainfall, float coldest, float warmest, bool shaped, bool water, params (string, WeatherKind, float, float, float, bool)[] w)
    {
        float wet = w.Where(x => WeatherDynamicsMath.Wet(x.Item2)).Sum(x => x.Item3) / Math.Max(0.01f, w.Sum(x => x.Item3));
        return new Land { name = name, b = new BiomeWeather { rainfall = rainfall, coldest = coldest, warmest = warmest, wetShare = wet, seasonsShaped = shaped, water = water }, weathers = w };
    }

    /// <summary>Lands like the game's (and Seasonal Weather's and Odyssey's weathers in them), their weights about the game's.</summary>
    private static List<Land> Lands()
    {
        const float Hot = 999f, Any = -999f;
        var clear = ("Clear", WeatherKind.Clear, 18f, Any, Hot, false);
        var fog = ("Fog", WeatherKind.Fog, 1f, Any, Hot, false);
        var rain = ("Rain", WeatherKind.Rain, 2f, 0f, Hot, false);
        var foggyRain = ("FoggyRain", WeatherKind.Rain, 1f, 0f, Hot, false);
        var storm = ("RainyThunderstorm", WeatherKind.Storm, 1f, 0f, Hot, true);
        var dry = ("DryThunderstorm", WeatherKind.DryStorm, 0.2f, Any, Hot, true);
        var snow = ("SnowGentle", WeatherKind.Snow, 4f, Any, -0.5f, false);
        var snowHard = ("SnowHard", WeatherKind.Snow, 4f, Any, -0.5f, false);
        var windy = ("Windy", WeatherKind.Windy, 4f, Any, Hot, false);
        var cloudy = ("Cloudy", WeatherKind.Cloudy, 6f, Any, Hot, false);
        var rainbow = ("DoubleRainbow", WeatherKind.Special, 0.1f, 0f, Hot, false);
        var torrent = ("RainTorrential", WeatherKind.Downpour, 0.3f, 0f, Hot, false);
        return new List<Land>
        {
            L("temperate forest", 1200f, -8f, 24f, false, false, clear, fog, rain, foggyRain, storm, dry, snow, snowHard),
            L("temperate forest with Seasonal Weather", 1200f, -8f, 24f, true, true, clear, fog, rain, foggyRain, storm, dry, snow, snowHard, windy, cloudy, rainbow),
            L("boreal forest", 700f, -25f, 16f, false, false, clear, fog, (rain.Item1, rain.Item2, 1.5f, rain.Item4, rain.Item5, rain.Item6), foggyRain, (storm.Item1, storm.Item2, 0.4f, storm.Item4, storm.Item5, true), snow, snowHard),
            L("desert", 200f, 8f, 40f, false, false, clear, (fog.Item1, fog.Item2, 0.1f, fog.Item4, fog.Item5, false), (rain.Item1, rain.Item2, 0.5f, rain.Item4, rain.Item5, false), (storm.Item1, storm.Item2, 0.3f, storm.Item4, storm.Item5, true), (dry.Item1, dry.Item2, 1f, dry.Item4, dry.Item5, true), ("Dust", WeatherKind.Dust, 1f, Any, Hot, false)),
            L("extreme desert", 50f, 12f, 48f, false, false, (clear.Item1, clear.Item2, 20f, clear.Item4, clear.Item5, false), (rain.Item1, rain.Item2, 0.15f, rain.Item4, rain.Item5, false), (dry.Item1, dry.Item2, 1f, dry.Item4, dry.Item5, true)),
            L("tropical rainforest (no Seasonal Weather)", 3500f, 22f, 32f, false, true, (clear.Item1, clear.Item2, 13f, clear.Item4, clear.Item5, false), (fog.Item1, fog.Item2, 2f, fog.Item4, fog.Item5, false), (rain.Item1, rain.Item2, 6f, rain.Item4, rain.Item5, false), (foggyRain.Item1, foggyRain.Item2, 3f, foggyRain.Item4, foggyRain.Item5, false), (storm.Item1, storm.Item2, 3f, storm.Item4, storm.Item5, true), torrent),
            L("tundra", 300f, -35f, 2f, false, false, clear, fog, (rain.Item1, rain.Item2, 1f, rain.Item4, rain.Item5, false), (snow.Item1, snow.Item2, 6f, snow.Item4, snow.Item5, false), snowHard),
            L("ice sheet", 100f, -50f, -15f, false, false, clear, fog, (snow.Item1, snow.Item2, 6f, snow.Item4, snow.Item5, false), snowHard, rainbow),
        };
    }

    private struct SimResult
    {
        public float wetShare, meanWetRun, wetSeasonShare, drySeasonShare;
        public int maxDry, maxWet, special;
    }

    /// <summary>
    /// Years of weather on a land, hour by hour, as the game chooses it (weights, the temperature ranges, not the
    /// same weather twice running, lengths of a quarter of a day to nearly three days, storms shorter), with or
    /// without Rimfolk's factors.
    /// </summary>
    private static SimResult Simulate(Land land, bool dynamic, int seed, int years = 10)
    {
        var r = new System.Random(seed);
        var b = land.b;
        int days = years * 60;
        int cur = 0, left = 0, rainHours = 0, snowHours = 0, special = 0;
        var wet = new List<bool>();
        var dryDays = new List<bool>();
        var snowy = new List<bool>();
        var quads = new List<int>();
        float yesterdayMean = 0f, todayMin = 999f, todayMax = -999f;
        int tropicalSeed = 7;
        var factors = new float[land.weathers.Length];
        for (int day = 0; day < days; day++)
        {
            int quadrum = day % 60 / 15;
            float season = (float)Math.Sin((day % 60) / 60.0 * Math.PI * 2 - Math.PI / 2);
            float mean = (b.coldest + b.warmest) / 2f + season * (b.warmest - b.coldest) / 2f;
            float noise = (float)(r.NextDouble() * 6 - 3);
            todayMin = 999f;
            todayMax = -999f;
            rainHours = snowHours = 0;
            for (int h = 0; h < 24; h++)
            {
                float temp = mean + noise + (float)Math.Sin((h - 9) / 24.0 * Math.PI * 2) * 5f;
                todayMin = Math.Min(todayMin, temp);
                todayMax = Math.Max(todayMax, temp);
                if (left <= 0)
                {
                    var now = new WeatherNow
                    {
                        current = land.weathers[cur].kind,
                        wetRun = Run(wet), dryRun = Run(dryDays), snowRun = Run(snowy),
                        hour = h, quadrum = quadrum, season = 1 + quadrum, temp = temp,
                        tempFall = Math.Max(0f, yesterdayMean - (todayMin + todayMax) / 2f),
                        yearWet = WeatherDynamicsMath.YearOf(3, day / 60, b).wet,
                        eventRain = 1f, eventClear = 1f, eventStorm = 1f, eventSnow = 1f, eventFog = 1f, seed = tropicalSeed
                    };
                    float total = 0f;
                    var weights = new float[land.weathers.Length];
                    for (int i = 0; i < land.weathers.Length; i++)
                    {
                        var w = land.weathers[i];
                        if (i == cur && w.kind != WeatherKind.Clear) continue;
                        if (temp < w.minT || temp > w.maxT) continue;
                        weights[i] = w.weight;
                    }
                    if (dynamic)
                    {
                        WeatherDynamicsMath.Shared(land.weathers.Select(x => x.kind).ToArray(), weights, now, b, factors);
                        for (int i = 0; i < weights.Length; i++) weights[i] *= factors[i];
                    }
                    for (int i = 0; i < weights.Length; i++) total += weights[i];
                    if (total <= 0f) cur = 0;
                    else
                    {
                        double pick = r.NextDouble() * total;
                        for (int i = 0; i < weights.Length; i++)
                        {
                            pick -= weights[i];
                            if (pick <= 0) { cur = i; break; }
                        }
                    }
                    var chosen = land.weathers[cur];
                    left = chosen.storm ? r.Next(6, 17) : r.Next(6, 65);
                    if (dynamic && chosen.kind == WeatherKind.Fog)
                    {
                        int? fog = WeatherDynamicsMath.FogTicks(h, left * WeatherDynamicsMath.TicksPerHour, b);
                        if (fog.HasValue) left = Math.Max(1, fog.Value / WeatherDynamicsMath.TicksPerHour);
                    }
                    if (chosen.kind == WeatherKind.Special) special++;
                }
                var k = land.weathers[cur].kind;
                // The game's temperature ranges: rain turns to nothing below freezing, snow stops above it.
                if (WeatherDynamicsMath.Wet(k) && temp >= 0f) rainHours++;
                if (k == WeatherKind.Snow && temp <= 0f) snowHours++;
                left--;
            }
            wet.Add(rainHours >= 4);
            dryDays.Add(rainHours == 0 && snowHours == 0);
            snowy.Add(snowHours >= 2);
            quads.Add(quadrum);
            yesterdayMean = (todayMin + todayMax) / 2f;
        }
        int maxDry = 0, maxWet = 0, runs = 0, runDays = 0, curDry = 0, curWet = 0;
        for (int i = 0; i < days; i++)
        {
            curDry = dryDays[i] ? curDry + 1 : 0;
            if (wet[i]) curWet++;
            else
            {
                if (curWet > 0) { runs++; runDays += curWet; }
                curWet = 0;
            }
            maxDry = Math.Max(maxDry, curDry);
            maxWet = Math.Max(maxWet, curWet);
        }
        int wetSeasonDays = 0, wetSeasonWet = 0, drySeasonDays = 0, drySeasonWet = 0;
        for (int i = 0; i < days; i++)
        {
            if (WeatherDynamicsMath.WetQuadrum(quads[i], tropicalSeed)) { wetSeasonDays++; if (wet[i]) wetSeasonWet++; }
            else { drySeasonDays++; if (wet[i]) drySeasonWet++; }
        }
        return new SimResult
        {
            wetShare = wet.Count(x => x) / (float)days,
            meanWetRun = runs > 0 ? runDays / (float)runs : 0f,
            maxDry = maxDry,
            maxWet = maxWet,
            special = special,
            wetSeasonShare = wetSeasonDays > 0 ? wetSeasonWet / (float)wetSeasonDays : 0f,
            drySeasonShare = drySeasonDays > 0 ? drySeasonWet / (float)drySeasonDays : 0f
        };
    }

    /// <summary>Days running, counted back from the newest.</summary>
    private static int Run(List<bool> days)
    {
        int n = 0;
        for (int i = days.Count - 1; i >= 0 && days[i]; i--) n++;
        return n;
    }
}
