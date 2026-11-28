using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The land's climate (Weather/ClimateMath.cs, LandClimate.cs, ClimateDrift.cs): seasons with a character of their own
/// in the shares designed at each strength (most ordinary, a few extreme), snowy or snowless only where snow falls, a hard
/// winter 4 to 8 °C colder; a year's extremes now and then, never on frozen land; once in a lifetime as rarely as designed,
/// only what the land's own climate never brings, in the season it would come in and within the temperature's reach; the
/// weathers no biome has only where their mod is loaded and the land lacks them, never the ones left to their events, red
/// rain mostly before a hard season; the drift one way, within its cap, set going by what happened; the land's record's
/// trends; the season's forecast as good as its reader. Simulated: a wet season rains about half again as often, a dry one
/// about half as often, and a hundred years with the climate keep each land's rain. And the seams: where the game side
/// calls it, its words, lines, settings and the Almanac.
/// </summary>
public static class LivingClimateTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "living climate: " + what);
        }

        var lands = Lands();
        int allSpecials = 0;
        foreach (SpecialKind k in Enum.GetValues(typeof(SpecialKind)))
            if (k != SpecialKind.None) allSpecials |= ClimateLand.Bit(k);
        var strengths = new[] { ClimateStrength.Gentle, ClimateStrength.Usual, ClimateStrength.Dramatic };

        // ================= The seasons =================

        var shares = new Dictionary<ClimateStrength, (float ordinary, float extreme, Dictionary<SeasonKind, float> kinds)>();
        bool snowRule = true, inBounds = true, offOrdinary = true;
        foreach (var s in strengths.Concat(new[] { ClimateStrength.Off }))
        {
            int total = 0, ordinary = 0, extreme = 0;
            var kinds = new Dictionary<SeasonKind, int>();
            foreach (var (name, l) in lands)
                for (int y = 5500; y < 5800; y++)
                    for (int q = 0; q < 4; q++)
                    {
                        var c = ClimateMath.Core(name.GetHashCode() & 0xFFFF, y, q, SeasonFor(q), l, s);
                        total++;
                        if (c.Ordinary) ordinary++;
                        if (c.extreme) extreme++;
                        kinds[c.kind] = kinds.TryGetValue(c.kind, out int v) ? v + 1 : 1;
                        if (c.snowSeason ? c.kind == SeasonKind.Wet || c.kind == SeasonKind.Dry : c.kind == SeasonKind.Snowy || c.kind == SeasonKind.Snowless) snowRule = false;
                        if (c.wet < ClimateMath.MinWet - 1e-4f || c.wet > ClimateMath.MaxWet + 1e-4f || c.snow < ClimateMath.MinSnow - 1e-4f || c.snow > ClimateMath.MaxSnow + 1e-4f
                            || c.temp < -9f * 1.3f || c.temp > 7f * 1.3f) inBounds = false;
                        if (s == ClimateStrength.Off && (!c.Ordinary || c.rare != RareKind.None || c.temp != 0f || c.wet != 0f)) offOrdinary = false;
                    }
            shares[s] = (ordinary / (float)total, extreme / (float)total, kinds.ToDictionary(kv => kv.Key, kv => kv.Value / (float)total));
        }
        var usual = shares[ClimateStrength.Usual];
        string Show(ClimateStrength s) => $"{shares[s].ordinary:P0} ordinary, {shares[s].extreme:P1} extreme";
        C(usual.ordinary >= 0.44f && usual.ordinary <= 0.58f, $"as designed most seasons are ordinary ({Show(ClimateStrength.Usual)})");
        C(usual.extreme >= 0.07f && usual.extreme <= 0.13f, $"and about one in ten is extreme ({usual.extreme:P1})");
        foreach (var k in new[] { SeasonKind.Wet, SeasonKind.Dry, SeasonKind.Warm, SeasonKind.Cold, SeasonKind.Snowy, SeasonKind.Snowless })
            C(usual.kinds.TryGetValue(k, out float f) && f >= 0.02f && f <= 0.2f, $"some seasons are {k} ({(usual.kinds.TryGetValue(k, out float g) ? g : 0f):P1})");
        C(shares[ClimateStrength.Gentle].ordinary >= 0.62f && shares[ClimateStrength.Gentle].extreme <= 0.05f, $"gentle: mostly ordinary, seldom extreme ({Show(ClimateStrength.Gentle)})");
        C(shares[ClimateStrength.Dramatic].ordinary <= 0.42f && shares[ClimateStrength.Dramatic].extreme >= 0.15f, $"dramatic: fewer ordinary, more extreme ({Show(ClimateStrength.Dramatic)})");
        C(snowRule, "snowy or snowless only where snow falls, wet or dry only where it doesn't");
        C(inBounds, "every season's lean stays within its bounds");
        C(offOrdinary, "off, every season is ordinary");

        // How far each kind goes, at each strength.
        bool hard = true, warm = true, wetLean = true, dryLean = true, deterministic = true, differs = false;
        foreach (var s in strengths)
        {
            float mag = ClimateMath.Params(s).magnitude;
            foreach (var (name, l) in lands)
                for (int y = 5500; y < 5700; y++)
                    for (int q = 0; q < 4; q++)
                    {
                        var c = ClimateMath.Core(7, y, q, SeasonFor(q), l, s);
                        if (c.yearKind != YearKind.Ordinary) continue;
                        if (c.kind == SeasonKind.Cold && c.extreme && (c.temp > -4f * mag + 1e-3f || c.temp < -8f * mag - 1e-3f)) hard = false;
                        if (c.kind == SeasonKind.Warm && !c.extreme && (c.temp < 2f * mag - 1e-3f || c.temp > 3.5f * mag + 1e-3f)) warm = false;
                        if (c.kind == SeasonKind.Wet && !c.extreme && (c.wet < 0.45f * mag - 1e-3f || c.wet > 0.6f * mag + 1e-3f)) wetLean = false;
                        if (c.kind == SeasonKind.Dry && !c.extreme && (c.wet > -0.4f * mag + 1e-3f || c.wet < -0.5f * mag - 1e-3f)) dryLean = false;
                        var again = ClimateMath.Core(7, y, q, SeasonFor(q), l, s);
                        if (again.kind != c.kind || again.temp != c.temp || again.rare != c.rare) deterministic = false;
                        if (ClimateMath.Core(7, y + 1, q, SeasonFor(q), l, s).temp != c.temp) differs = true;
                    }
        }
        C(hard, "a hard winter (an extreme cold one) runs 4 to 8 °C colder as designed, scaled by the strength");
        C(warm && wetLean && dryLean, "a warm season 2 to 3.5 °C warmer, a wet one leans half again as wet, a dry one about half as dry");
        C(deterministic && differs, "a season's character is the same every time it's asked, and differs from year to year");
        var temperate = lands.First(x => x.name == "temperate").l;
        var winterRole = ClimateMath.Role(4, 0, temperate);
        var permanent = lands.First(x => x.name == "tropical").l;
        C(winterRole == 4 && ClimateMath.Role(5, Enumerable.Range(0, 4).First(q => permanent.Rank(q) == 3), permanent) == 2
              && ClimateMath.Role(5, Enumerable.Range(0, 4).First(q => permanent.Rank(q) == 0), permanent) == 4,
            "where the season is permanent, the warmest quadrum plays the summer and the coldest the winter");
        C(Enumerable.Range(0, 4).Select(q => permanent.Rank(q)).OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2, 3 }), "the quadrums rank 0 to 3 by warmth, ties broken");

        // ================= A year's extremes =================

        int years = 0, extremeYears = 0;
        bool frozenNone = true, noSummerRight = true, droughtRight = true, floodsRight = true;
        foreach (var (name, l) in lands)
            for (int y = 3000; y < 6000; y++)
            {
                var yk = ClimateMath.YearKindOf(11, y, l, ClimateStrength.Usual);
                if (l.b.Frozen)
                {
                    if (yk != YearKind.Ordinary) frozenNone = false;
                    continue;
                }
                years++;
                if (yk == YearKind.Ordinary) continue;
                extremeYears++;
                for (int q = 0; q < 4; q++)
                {
                    var c = ClimateMath.Core(11, y, q, SeasonFor(q), l, ClimateStrength.Usual);
                    switch (yk)
                    {
                        case YearKind.NoSummer:
                            if (c.role == 2 && !(c.kind == SeasonKind.Cold && c.extreme && c.temp <= -4f + 1e-3f && c.temp >= -6f - 1e-3f)) noSummerRight = false;
                            if ((c.role == 1 || c.role == 3) && c.kind != SeasonKind.Cold) noSummerRight = false;
                            if (c.role == 4 && c.yearKind != YearKind.Ordinary) noSummerRight = false;
                            break;
                        case YearKind.Drought:
                            if (c.kind != (c.snowSeason ? SeasonKind.Snowless : SeasonKind.Dry)) droughtRight = false;
                            break;
                        case YearKind.Floods:
                            if (c.kind != (c.snowSeason ? SeasonKind.Snowy : SeasonKind.Wet) || (!c.snowSeason && (c.role == 1 || c.role == 3) && !c.extreme)) floodsRight = false;
                            break;
                    }
                }
            }
        float yearShare = extremeYears / (float)years;
        C(yearShare >= 0.065f && yearShare <= 0.095f, $"as designed about one year in twelve has its own extreme ({yearShare:P1}, once in {1f / yearShare:0} years)");
        C(frozenNone, "never on land that is always frozen");
        C(noSummerRight, "a year without a summer: its summer 4 to 6 °C colder, spring and fall cold, the winter its own");
        C(droughtRight && floodsRight, "a dry year makes every season dry (or short of snow); a year of heavy rain every season wet (or snowy), spring and fall very wet");

        // ================= Once in a lifetime =================

        var rareCount = new Dictionary<ClimateStrength, (int years, int rares)>();
        bool onlyOutside = true, rightSeason = true, reach = true, fits = true, noneWithout = true;
        foreach (var s in strengths)
        {
            var p = ClimateMath.Params(s);
            int ys = 0, rs = 0;
            foreach (var (name, l) in lands)
            {
                bool any = Enumerable.Range(0, 4).Any(q => ClimateMath.RareCandidates(l, q, s).Count > 0);
                for (int y = 1000; y < 5000; y++)
                {
                    var r = ClimateMath.RareOfYear(23, y, l, s);
                    if (!any)
                    {
                        if (r.kind != RareKind.None) noneWithout = false;
                        continue;
                    }
                    ys++;
                    if (r.kind == RareKind.None) continue;
                    rs++;
                    var c = ClimateMath.Core(23, y, r.quadrum, SeasonFor(r.quadrum), l, s);
                    if (c.rare != r.kind || c.rareStart < 24 || c.rareStart + c.rareHours > ClimateMath.SeasonHours) fits = false;
                    switch (r.kind)
                    {
                        case RareKind.Snow: if (l.hasSnow || l.b.coldest < ClimateMath.SnowNeverFrom || l.Rank(r.quadrum) != 0) onlyOutside = false; break;
                        case RareKind.Frost: if (l.b.coldest < 12f || l.Rank(r.quadrum) != 0) onlyOutside = false; break;
                        case RareKind.Heat: if (l.b.warmest > 16f || l.Rank(r.quadrum) != 3) onlyOutside = false; break;
                        case RareKind.Blizzard: if (l.hasBlizzard || !l.blizzardDef || l.Rank(r.quadrum) != 0) onlyOutside = false; break;
                        case RareKind.Dust: if (l.hasDust || !l.sandDef || l.Rank(r.quadrum) < 2) onlyOutside = false; break;
                        case RareKind.Deluge: if (!l.b.Arid || l.hasDownpour) onlyOutside = false; break;
                        case RareKind.GreatStorm: if (!l.hurricaneDef || !l.b.water) onlyOutside = false; break;
                    }
                    if (r.temp < -p.maxDrop - 1e-3f || r.temp > p.maxRise + 1e-3f) reach = false;
                    if ((r.kind == RareKind.Snow || r.kind == RareKind.Frost || r.kind == RareKind.Blizzard) && r.temp >= 0f) rightSeason = false;
                    if (r.kind == RareKind.Heat && r.temp < 10f) rightSeason = false;
                }
                // Out of the land's climate only as once in a lifetime: no season of its own ever asks for it otherwise.
                for (int y = 1000; y < 1400; y++)
                    for (int q = 0; q < 4; q++)
                    {
                        var c = ClimateMath.Core(23, y, q, SeasonFor(q), l, s);
                        if (c.rare != RareKind.None && ClimateMath.RareOfYear(23, y, l, s).quadrum != q) onlyOutside = false;
                    }
            }
            rareCount[s] = (ys, rs);
        }
        float Rate(ClimateStrength s) => rareCount[s].rares / (float)Math.Max(1, rareCount[s].years);
        C(Math.Abs(Rate(ClimateStrength.Usual) - 0.05f) <= 0.012f, $"as designed once in a lifetime comes about once in twenty years where it can come ({Rate(ClimateStrength.Usual):P1} of years)");
        C(Rate(ClimateStrength.Gentle) < Rate(ClimateStrength.Usual) && Rate(ClimateStrength.Usual) < Rate(ClimateStrength.Dramatic) && Rate(ClimateStrength.Dramatic) <= 0.15f,
            $"gentler and more often by the strength ({Rate(ClimateStrength.Gentle):P1}, {Rate(ClimateStrength.Dramatic):P1})");
        C(onlyOutside, "only what the land's own climate never brings, in the season it would come in, and only as once in a lifetime");
        C(reach && rightSeason, "within the strength's reach of the temperature, and the right way (snow and frost colder, heat warmer)");
        C(fits && noneWithout, "inside its season, and never on land where nothing could come");
        var desert = lands.First(x => x.name == "desert").l;
        var snowDesert = ClimateMath.RareCandidates(desert, Enumerable.Range(0, 4).First(q => desert.Rank(q) == 0), ClimateStrength.Usual);
        C(snowDesert.Any(x => x.kind == RareKind.Snow && x.temp <= -10f) && snowDesert.All(x => x.kind != RareKind.Heat), "snow in the desert, taken down far enough for it to fall and lie");
        var tundra = lands.First(x => x.name == "tundra").l;
        C(ClimateMath.RareCandidates(tundra, Enumerable.Range(0, 4).First(q => tundra.Rank(q) == 3), ClimateStrength.Usual).Any(x => x.kind == RareKind.Heat), "a great heat on the tundra in its summer");
        var jungle = lands.First(x => x.name == "tropical").l;
        C(ClimateMath.RareCandidates(jungle, Enumerable.Range(0, 4).First(q => jungle.Rank(q) == 0), ClimateStrength.Usual).Any(x => x.kind == RareKind.Frost), "a frost in the jungle in its coolest season");
        C(ClimateMath.RareHarms(RareKind.GreatStorm) && !ClimateMath.RareHarms(RareKind.Snow) && ClimateMath.RareHasWeather(RareKind.Snow) && !ClimateMath.RareHasWeather(RareKind.Heat),
            "a great storm alone can hurt (within the shared harm budget); heat and frost bring no weather of their own");

        // ================= The weathers no biome has =================

        bool masked = true, noneWithoutMods = true, signsHard = true;
        int signs = 0, signsBeforeHard = 0, specials = 0;
        foreach (var (name, l) in lands)
        {
            var bare = l;
            bare.specials = 0;
            for (int y = 2000; y < 2600; y++)
                for (int q = 0; q < 4; q++)
                {
                    var c = ClimateMath.SeasonOf(31, y, q, SeasonFor(q), SeasonFor((q + 1) % 4), l, ClimateStrength.Usual);
                    if (c.special != SpecialKind.None)
                    {
                        specials++;
                        if (!l.Has(c.special)) masked = false;
                        if (c.specialStart < 24 || c.specialStart + c.specialHours > ClimateMath.SeasonHours) masked = false;
                    }
                    if (c.sign)
                    {
                        signs++;
                        var next = ClimateMath.Core(31, q == 3 ? y + 1 : y, (q + 1) % 4, SeasonFor((q + 1) % 4), l, ClimateStrength.Usual);
                        if (next.extreme || next.rare != RareKind.None || (next.yearKind != YearKind.Ordinary && next.yearKind != c.yearKind)) signsBeforeHard++;
                        if (c.special != SpecialKind.RedRain) signsHard = false;
                    }
                    if (ClimateMath.SeasonOf(31, y, q, SeasonFor(q), SeasonFor((q + 1) % 4), bare, ClimateStrength.Usual).special != SpecialKind.None) noneWithoutMods = false;
                }
        }
        C(specials > 100 && masked, $"the weathers no biome has come with the seasons ({specials}), only where their mod is loaded and the land lacks them, inside the season");
        C(noneWithoutMods, "without their mods, none");
        C(signs > 10 && signsHard && signsBeforeHard >= signs * 0.75f, $"red rain is a sign, mostly before a hard season ({signsBeforeHard} of {signs})");
        foreach (var bad in new[] { "BloodRain", "UnnaturalFog", "VEE_PsychicRain", "VEE_LongNight", "VEE_Inferno", "AB_AcidRainWeather", "TM_HailstormWD", "VPEH_Bloodstorm" })
            C(ClimateMath.Never.Contains(bad), $"{bad} is never brought by the climate");
        C(ClimateMath.SpecialDefs.All(d => !ClimateMath.Never.Contains(d.def))
              && ((RareKind[])Enum.GetValues(typeof(RareKind))).All(k => ClimateMath.RareDefs(k).All(d => !ClimateMath.Never.Contains(d))),
            "nothing it brings is on the list it never brings");
        C(((SpecialKind[])Enum.GetValues(typeof(SpecialKind))).Where(k => k != SpecialKind.None).All(k => ClimateMath.SpecialDefs.Any(d => d.kind == k)), "each weather no biome has is named by its def");

        // ================= The drift =================

        bool off = ClimateMath.Trend(ClimateDriftMode.Off, 5, 50f, ClimateStrength.Usual) == (0f, 0f) && ClimateMath.Trend(ClimateDriftMode.Warmer, 5, 50f, ClimateStrength.Off) == (0f, 0f);
        C(off, "no drift with it off, or the climate off");
        bool oneWay = true, capped = true, decade = true;
        for (int seed = 0; seed < 300; seed++)
        {
            float last = 0f;
            foreach (var s in strengths)
            {
                var p = ClimateMath.Params(s);
                last = 0f;
                for (float y = 0f; y <= 150f; y += 2.5f)
                {
                    var (t, w) = ClimateMath.Trend(ClimateDriftMode.Random, seed, y, s);
                    if (Math.Abs(t) + 1e-4f < Math.Abs(last)) oneWay = false;
                    if (Math.Abs(t) > p.driftCap + 1e-4f || Math.Abs(w) > p.wetCap + 1e-4f) capped = false;
                    last = t;
                }
                var (t10, _) = ClimateMath.Trend(ClimateDriftMode.Warmer, seed, 10f, s);
                if (t10 < p.driftLo - 1e-3f || t10 > p.driftHi + 1e-3f) decade = false;
            }
        }
        C(oneWay, "random drift goes one way for the whole game");
        C(capped, "within the strength's cap, however long the game");
        C(decade, "by the setting's rate a decade (1 to 3 °C as designed)");
        C(ClimateMath.Trend(ClimateDriftMode.Colder, 3, 20f, ClimateStrength.Usual).temp < -1.5f && ClimateMath.Trend(ClimateDriftMode.Drier, 3, 20f, ClimateStrength.Usual) is var dr && dr.wet < 0f && dr.temp == 0f
              && ClimateMath.Trend(ClimateDriftMode.Wetter, 3, 20f, ClimateStrength.Usual).wet > 0f, "colder, drier and wetter go their way");
        C(ClimateMath.Trend(ClimateDriftMode.Warmer, 3, 20f, ClimateStrength.Gentle).temp < ClimateMath.Trend(ClimateDriftMode.Warmer, 3, 20f, ClimateStrength.Dramatic).temp,
            "gentle drifts less than dramatic");
        var volcanic = ClimateMath.Push("volcanic", 0.5f, 1f, ClimateStrength.Usual);
        C(Math.Abs(volcanic.temp + 1.5f) < 1e-3f && ClimateMath.Push("volcanic", 4.2f, 1f, ClimateStrength.Usual).temp == 0f && ClimateMath.Push("volcanic", -1f, 1f, ClimateStrength.Usual).temp == 0f,
            "a volcanic winter cools the land 1.5 °C for a year, then fades over three");
        C(ClimateMath.Push("comet", 2f, -1f, ClimateStrength.Usual).temp < 0f && ClimateMath.Push("comet", 2f, 1f, ClimateStrength.Usual).temp > 0f && ClimateMath.Push("comet", 9f, 1f, ClimateStrength.Usual).temp == 0f,
            "a comet sets it moving one way or the other for a few years");
        C(Math.Abs(ClimateMath.Local(1f, 0f, ClimateStrength.Usual).temp - 2f) < 1e-3f && ClimateMath.Local(0f, 0.1f, ClimateStrength.Usual) == (0f, 0f)
              && ClimateMath.Local(0f, 0.5f, ClimateStrength.Usual).temp > 0f && ClimateMath.Local(0f, 0.5f, ClimateStrength.Usual).wet < 0f,
            "pollution warms the land (2 °C at most); losing more than a sixth of the trees makes it warmer and drier");

        // ================= The land's record =================

        var rec = new List<ClimateYear>();
        for (int i = 0; i < 10; i++)
            rec.Add(new ClimateYear { year = 5500 + i, winter = -1f + i * 0.35f, summer = 0.2f * ((i % 3) - 1), wetDays = 18, days = 60, growDays = 40 + i, cover = 10, firstSnow = (57 + i) % 60 });
        var trends = ClimateMath.Trends(rec);
        C(trends.Count > 0 && trends[0].kind == TrendKind.WinterMilder && trends[0].by >= 2f && trends.All(t => t.kind != TrendKind.SummerHotter && t.kind != TrendKind.SummerCooler),
            $"milder winters found, the summers' wobble not ({string.Join(", ", trends.Select(t => t.kind + " " + t.by.ToString("0.0")))})");
        C(trends.Any(t => t.kind == TrendKind.GrowLonger) && trends.Any(t => t.kind == TrendKind.SnowLater && t.by >= 7f && t.by <= 9f),
            "a longer growing season, and the first snow later across the turn of the year");
        C(ClimateMath.Trends(rec.Take(3).ToList()).Count == 0, "three years aren't enough to say");
        var flat = Enumerable.Range(0, 8).Select(i => new ClimateYear { year = i, winter = i % 2 == 0 ? 0.5f : -0.5f, summer = 0f, wetDays = 18 + i % 2, days = 60, growDays = 40, firstSnow = -1 }).ToList();
        C(ClimateMath.Trends(flat).Count == 0, "years that only wobble show nothing");
        var y0 = rec[4];
        var back = ClimateYear.Unpack(y0.Pack());
        C(back != null && back.year == y0.year && Math.Abs(back.winter - y0.winter) < 0.06f && back.firstSnow == y0.firstSnow && back.growDays == y0.growDays && ClimateYear.Unpack("nonsense") == null,
            "a year of the record keeps as a short line");
        var quarters = new List<WeatherQuarter>
        {
            new WeatherQuarter { year = 5501, quadrum = 0, days = 15, meanSum = 15 * -10f, expectedSum = 15 * -6f, wetDays = 2, coldDays = 15, counted = true },
            new WeatherQuarter { year = 5501, quadrum = 1, days = 15, meanSum = 15 * 8f, expectedSum = 15 * 8f, wetDays = 4, coldDays = 3, counted = true },
            new WeatherQuarter { year = 5501, quadrum = 2, days = 15, meanSum = 15 * 22f, expectedSum = 15 * 20f, wetDays = 3, coldDays = 0, counted = true },
            new WeatherQuarter { year = 5501, quadrum = 3, days = 9, meanSum = 9 * 5f, expectedSum = 9 * 5f, wetDays = 1, coldDays = 4, counted = true }
        };
        var made = ClimateYear.Of(quarters, 5501, 44);
        C(made != null && Math.Abs(made.winter + 4f) < 1e-3f && Math.Abs(made.summer - 2f) < 1e-3f && made.wetDays == 9 && made.days == 45 && made.growDays == 27 && made.firstSnow == 44,
            "a year from the record's seasons: the coldest-by-usual its winter, the warmest its summer, short seasons left out");
        C(ClimateMath.TrendFits(TrendKind.WinterMilder, 4) && !ClimateMath.TrendFits(TrendKind.WinterMilder, 2) && ClimateMath.TrendFits(TrendKind.Drier, 1), "the elders speak of winters in winter");

        // ================= The season's forecast =================

        var rng = new System.Random(5);
        int right = 0, tries = 0;
        bool wrongIsWrong = true, wrongFits = true;
        var actual = new SeasonCharacter { kind = SeasonKind.Wet, extreme = true, snowSeason = false };
        for (int i = 0; i < 20000; i++)
        {
            var g = ClimateMath.Guess(actual, 0.75f, (float)rng.NextDouble(), (float)rng.NextDouble());
            tries++;
            if (g.kind == actual.kind)
            {
                right++;
                if (!g.extreme) wrongIsWrong = false;
            }
            else if (!ClimateMath.KindsFor(false).Contains(g.kind)) wrongFits = false;
        }
        C(Math.Abs(right / (float)tries - 0.75f) < 0.02f && wrongIsWrong && wrongFits, $"right as often as the reader is good ({right / (float)tries:P0} at 75%), and wrong with a kind the season could have been");
        C(ClimateMath.SeasonAccuracy(0.9f) < 0.9f && ClimateMath.SeasonAccuracy(0.55f) >= 0.4f, "a season is a little harder to read than tomorrow's weather");
        C(ClimateMath.PhraseKey(SeasonKind.Cold, true, 4) == "CC_Clim_Season_ColdWinterX" && ClimateMath.PhraseKey(SeasonKind.Warm, false, 4) == "CC_Clim_Season_WarmWinter"
              && ClimateMath.PhraseKey(SeasonKind.Ordinary, true, 2) == "CC_Clim_Season_Ordinary" && ClimateMath.WasKey(SeasonKind.Wet, false, 2) == "CC_Clim_Was_Wet",
            "a hard winter, a mild one, an ordinary summer");

        // ================= The weather's lean =================

        var plain = WeatherNow.Calm;
        var wetN = plain;
        wetN.seasonWet = 0.5f;
        var dryN = plain;
        dryN.seasonWet = -0.45f;
        var snowyN = plain;
        snowyN.seasonSnow = 0.6f;
        C(WeatherDynamicsMath.Character(WeatherKind.Rain, wetN) > 1.4f && WeatherDynamicsMath.Character(WeatherKind.Clear, wetN) < 1f
              && WeatherDynamicsMath.Character(WeatherKind.Rain, dryN) < 0.6f && WeatherDynamicsMath.Character(WeatherKind.Dust, dryN) > 1f
              && WeatherDynamicsMath.Character(WeatherKind.Snow, snowyN) > 1.5f && WeatherDynamicsMath.Character(WeatherKind.Special, wetN) == 1f
              && WeatherDynamicsMath.Character(WeatherKind.Rain, plain) == 1f, "a wet season leans to rain, a dry one to dust, a snowy one to snow; specials untouched");
        bool leanBounded = true, leanCaps = true;
        var kindsAll = (WeatherKind[])Enum.GetValues(typeof(WeatherKind));
        foreach (var (lname, land) in WeatherDynamicsTests.Lands().Select(x => (x.name, x)))
            for (int i = 0; i < 3000; i++)
            {
                var nw = WeatherNow.Calm;
                nw.seasonWet = (float)(rng.NextDouble() * (ClimateMath.MaxWet - ClimateMath.MinWet) + ClimateMath.MinWet);
                nw.seasonSnow = (float)(rng.NextDouble() * (ClimateMath.MaxSnow - ClimateMath.MinSnow) + ClimateMath.MinSnow);
                nw.drought = rng.Next(4) == 0;
                var k = kindsAll[rng.Next(kindsAll.Length)];
                float f = WeatherDynamicsMath.Factor(k, nw, land.b);
                if (f < WeatherDynamicsMath.MinFactor - 1e-4f || f > WeatherDynamicsMath.MaxFactor + 1e-4f || float.IsNaN(f)) leanBounded = false;
                if ((k == WeatherKind.Downpour || nw.drought && WeatherDynamicsMath.Wet(k) || k == WeatherKind.Special) && f > 1f + 1e-4f) leanCaps = false;
            }
        C(leanBounded && leanCaps, "with every season's lean, the weather's factors stay in bounds, and still no downpour, no rain in a drought, no special made likelier");

        // ================= Simulated: the seasons on the land =================

        var simLands = WeatherDynamicsTests.Lands();
        var tropicalSim = simLands.First(x => x.name == "tropical rainforest (no Seasonal Weather)");
        var swSim = simLands.First(x => x.name == "temperate forest with Seasonal Weather");
        foreach (var land in new[] { swSim, simLands.First(x => x.name == "temperate forest"), simLands.First(x => x.name == "boreal forest"), simLands.First(x => x.name == "desert"), tropicalSim })
        {
            var baseRun = WeatherDynamicsTests.Simulate(land, true, 41, 200);
            var wetRun = WeatherDynamicsTests.Simulate(land, true, 41, 200, (y, q) => (0.52f, 0f, 0f));
            var dryRun = WeatherDynamicsTests.Simulate(land, true, 41, 200, (y, q) => (-0.45f, 0f, 0f));
            var veryWet = WeatherDynamicsTests.Simulate(land, true, 41, 200, (y, q) => (0.97f, 0f, 0f));
            float wr = wetRun.wetShare / baseRun.wetShare, dr2 = dryRun.wetShare / baseRun.wetShare, vw = veryWet.wetShare / baseRun.wetShare;
            // Where it already rains on a third of the days or more, there is less room: a wet season brings a little less.
            bool rainy = baseRun.wetShare >= 0.3f;
            string what = $"{land.name} (rain on {baseRun.wetShare:P0} of days)";
            C(wr >= (rainy ? 1.12f : 1.3f) && wr <= 1.8f, $"{what}: a wet season rains about half again as often ({wr:0.00} times)");
            C(dr2 >= 0.35f && dr2 <= (rainy ? 0.8f : 0.72f), $"{what}: a dry season about half as often ({dr2:0.00} times)");
            C(vw > wr && vw <= 2.6f, $"{what}: a very wet one more ({vw:0.00} times)");
        }
        // A hundred years with the climate as drawn keep each land's rain, as the weather alone does.
        foreach (var land in simLands)
        {
            var cl = ClimateFor(land);
            var plainRun = WeatherDynamicsTests.Simulate(land, false, 99, 100);
            var withClimate = WeatherDynamicsTests.Simulate(land, true, 99, 100, (y, q) =>
            {
                var c = ClimateMath.Core(77, 5500 + y, q, SeasonFor(q), cl, ClimateStrength.Usual);
                return (c.wet, c.snow, c.temp);
            });
            float ratio = plainRun.wetShare > 0f ? withClimate.wetShare / plainRun.wetShare : 1f;
            C(plainRun.wetShare < 0.02f ? Math.Abs(withClimate.wetShare - plainRun.wetShare) <= 0.008f : ratio > 0.72f && ratio < 1.38f,
                $"{land.name}: about as much rain over a hundred years with the seasons' characters as the game alone ({withClimate.wetShare:P1} of days against {plainRun.wetShare:P1})");
        }
        // Snowy winters bring snow more often than snowless ones.
        var boreal = simLands.First(x => x.name == "boreal forest");
        var snowyRun = WeatherDynamicsTests.Simulate(boreal, true, 5, 150, (y, q) => (0.15f, 0.6f, -1f));
        var snowlessRun = WeatherDynamicsTests.Simulate(boreal, true, 5, 150, (y, q) => (0f, -0.6f, 1f));
        float snowyDays = snowyRun.snowDay.Count(x => x) / (float)snowyRun.snowDay.Length, snowlessDays = snowlessRun.snowDay.Count(x => x) / (float)snowlessRun.snowDay.Length;
        C(snowyDays > snowlessDays * 1.6f, $"boreal forest: a snowy season brings snow far more often than one short of snow ({snowyDays:P1} of days against {snowlessDays:P1})");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string book = Read("Weather/WeatherBook.cs"), dyn = Read("Weather/WeatherDynamics.cs"), lc = Read("Weather/LandClimate.cs"), cm = Read("Weather/ClimateMath.cs");
        C(book.Contains("LandClimate.Hourly(map, this)") && book.Contains("LandClimate.Rare(map, this)") && book.Contains("clim.Expose()") && book.Contains("LandClimate.AnnalsPart(map, this, year)")
              && book.Contains("LandClimate.OverviewLines(map, this)") && book.Contains("LandClimate.QuarterEnded(map, this, q,"),
            "the weather record runs the climate in its hour and its 250-tick pass, saves it, and gives it the annals, the Overview and the year's end");
        C(dyn.Contains("LandClimate.WeatherLean(map, book, ref n)") && dyn.Contains("book.clim.climate") && Read("Weather/WeatherDynamicsMath.cs").Contains("* Character(c, n)"),
            "the season leans the weather's choice and moves the temperature, the fields' forecast too");
        C(Regex.IsMatch(lc, @"public static bool OtherModDrivesWeather\(Map map\) => false;") && lc.Contains("!OtherModDrivesWeather(map)"),
            "the seam where another mod driving the weather would stand the climate down (none yet)");
        C(lc.Contains("Harm.Roll(HarmKinds.Dangers, 1f)") && lc.Contains("Harm.Roll(HarmKinds.Dangers, 0.5f)") && lc.Contains("HarmBook.Instance?.Came(HarmKinds.Dangers, now)"),
            "a great storm and acid rain only within the shared harm budget, counted when they come");
        C(!Regex.IsMatch(lc + cm, @"""(BloodRain|UnnaturalFog|VEE_PsychicRain|VEE_LongNight|VEE_Inferno|AB_AcidRainWeather)""") || cm.Contains("Never ="), "the weathers left to their events are named only to be left alone");
        C(!Regex.IsMatch(Read("Lean/Sched.cs"), @"\bClimate\b"), "no clock of its own: the weather record's hour and pass");

        // The words.
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(f).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (var file in new[] { "Weather/LandClimate.cs", "Weather/WeatherBook.cs", "Weather/ClimateMath.cs" })
            foreach (var k in Regex.Matches(Read(file), @"""(CC_[A-Za-z0-9_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                C(keyed.ContainsKey(k) || k.EndsWith("_"), $"{k} ({file}) is in the words");
        foreach (SeasonKind k in Enum.GetValues(typeof(SeasonKind)))
            foreach (bool x in new[] { false, true })
                foreach (int role in new[] { 1, 2, 3, 4 })
                {
                    C(keyed.ContainsKey(ClimateMath.PhraseKey(k, x, role)) && keyed.ContainsKey(ClimateMath.WasKey(k, x, role)), $"the words for {k}{(x ? " (extreme)" : "")} in role {role}");
                    if (x && k != SeasonKind.Ordinary)
                    {
                        string told = ClimateMath.PhraseKey(k, true, role).Replace("CC_Clim_Season_", "CC_Clim_Told_");
                        C(keyed.ContainsKey(told) || keyed.ContainsKey("CC_Clim_Told_" + k + "X"), $"{told} is told");
                    }
                }
        foreach (RareKind k in Enum.GetValues(typeof(RareKind)))
        {
            if (k == RareKind.None) continue;
            foreach (var prefix in new[] { "CC_Clim_RareLabel_", "CC_Clim_Told_Rare_", "CC_Clim_Chron_Rare_", "CC_Clim_Annals_Rare_" })
                C(keyed.ContainsKey(prefix + k), $"{prefix}{k} is in the words");
        }
        foreach (YearKind k in Enum.GetValues(typeof(YearKind)))
        {
            if (k == YearKind.Ordinary) continue;
            foreach (var prefix in new[] { "CC_Clim_Told_", "CC_Clim_Chron_", "CC_Clim_YearLabel_", "CC_Clim_Annals_" })
                C(keyed.ContainsKey(prefix + k), $"{prefix}{k} is in the words");
        }
        foreach (TrendKind k in Enum.GetValues(typeof(TrendKind))) C(keyed.ContainsKey("CC_Clim_Trend_" + k), $"CC_Clim_Trend_{k} is in the words");
        string climateWords = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Climate.xml"));
        C(!climateWords.Contains('—') && !Regex.IsMatch(climateWords, @"!\s*<"), "plain words: no em dashes or exclamation marks");
        foreach (var o in new[] { "Off", "Gentle", "Usual", "Dramatic" })
            C(keyed.ContainsKey("CC_ClimateStrength_" + o) && keyed.ContainsKey("CC_ClimateStrength_" + o + "_Tip") && keyed.ContainsKey("CC_SetNote_ClimateStrength_" + o), $"the strength {o} has its words");
        foreach (var o in Enum.GetNames(typeof(ClimateDriftMode)))
            C(keyed.ContainsKey("CC_ClimateDrift_" + o) && keyed.ContainsKey("CC_ClimateDrift_" + o + "_Tip") && keyed.ContainsKey("CC_SetNote_ClimateDrift_" + o), $"the drift {o} has its words");

        // The lines.
        var linesFile = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Climate_Lines.xml"));
        var pools = linesFile.Root!.Elements().ToDictionary(e => e.Element("defName")!.Value, e => e.Element("lines")!.Descendants("li").Select(li => li.Value).ToList());
        foreach (SeasonKind k in Enum.GetValues(typeof(SeasonKind))) C(pools.TryGetValue("Rimfolk_Climate_Pool_SaySeason_" + k, out var l) && l.Count >= 6, $"the forecast's words for {k}");
        foreach (var m in Regex.Matches(lc, @"""(Rimfolk_Climate_Pool_[A-Za-z_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(m.EndsWith("_") || pools.ContainsKey(m), $"{m} is a pool");
        foreach (TrendKind k in Enum.GetValues(typeof(TrendKind))) C(pools["Rimfolk_Climate_Pool_SayTrend"].Any(x => x.StartsWith("[trend:" + k + "]")), $"the elders have a word for {k}");
        foreach (var (pool, lines) in pools)
            foreach (var line in lines)
            {
                string body = Regex.Replace(line, @"^\s*\[[^\]]*\]\s*", "").Trim();
                int end = -1;
                for (int i = 12; i < body.Length; i++)
                    if (body[i] == '.' || body[i] == '!' || body[i] == '?') { end = i; break; }
                C((end < 0 || end == body.Length - 1) && body.Length <= 90 && !body.StartsWith("\"") && !body.Contains('!') && !body.Contains('—'), $"{pool}: '{body}' is one plain sentence of 90 characters at most");
            }
        foreach (var w in Regex.Matches(File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Climate_Lines.xml")), @"\[weather:([A-Za-z_]+)\]").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(ClimateMath.SpecialDefs.Any(d => d.def == w) || ClimateMath.RareDefs(RareKind.Dust).Contains(w) || w == "AB_VolcanicAsh", $"the line for {w} is for a weather the climate brings");

        // The settings, and the Almanac.
        var strength = SettingsCatalog.Find("climateStrength");
        var driftRow = SettingsCatalog.Find("climateDrift");
        C(strength != null && strength.page == SettingsPage.WhatsOn && strength.section == "CC_SetSec_Weather" && strength.options.SequenceEqual(Enum.GetNames(typeof(ClimateStrength)))
              && driftRow != null && driftRow.section == "CC_SetSec_Weather" && driftRow.options.SequenceEqual(Enum.GetNames(typeof(ClimateDriftMode))) && driftRow.needs.Contains("climateStrength!=Off"),
            "one setting for the strength and one for the drift, under \"The weather\"");
        var almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council_Weather.xml"));
        foreach (var h in new[] { "Seasons with a character of their own", "A year's extremes, and once in a lifetime", "Weathers that come by no biome", "A climate that drifts", "The land's record" })
            C(almanac.Contains("<heading>" + h.Replace("'", "&apos;") + "</heading>") || almanac.Contains("<heading>" + h + "</heading>"), $"the Almanac's weather page has \"{h}\"");
        var doc = Path.Combine(root, "docs", "design", "living-land.md");
        C(File.Exists(doc) && File.ReadAllText(doc).Contains("LandClimate.SeasonNow") && File.ReadAllText(doc).Contains("ExtremeBegan"), "the design note for the next parts names the API");
        return n;
    }

    /// <summary>The season for a quadrum in these lands: the first the winter, then spring, summer, fall (quadrum 0 is the coldest).</summary>
    private static int SeasonFor(int q) => q == 0 ? 4 : q;

    private static ClimateLand Land(float rainfall, float coldest, float warmest, float wetShare, bool water, bool hasSnow, bool hasBlizzard, bool hasDust, bool hasDownpour, bool defs)
    {
        float mid = (coldest + warmest) / 2f, amp = (warmest - coldest) / 2f;
        var l = new ClimateLand
        {
            b = new BiomeWeather { rainfall = rainfall, coldest = coldest, warmest = warmest, wetShare = wetShare, water = water },
            q0 = mid - amp * 0.85f, q1 = mid + amp * 0.1f, q2 = mid + amp * 0.85f, q3 = mid - amp * 0.05f,
            hasSnow = hasSnow, hasBlizzard = hasBlizzard, hasDust = hasDust, hasDownpour = hasDownpour,
            blizzardDef = defs, sandDef = defs, torrentDef = defs, hurricaneDef = defs
        };
        if (defs)
            foreach (SpecialKind k in Enum.GetValues(typeof(SpecialKind)))
                if (k != SpecialKind.None) l.specials |= ClimateLand.Bit(k);
        return l;
    }

    /// <summary>Lands like the player's, with every mod's weathers loaded, and a few without them.</summary>
    private static List<(string name, ClimateLand l)> Lands() => new List<(string, ClimateLand)>
    {
        ("temperate", Land(1200f, -8f, 24f, 0.2f, false, true, false, false, false, true)),
        ("temperate by water", Land(1600f, -2f, 22f, 0.25f, true, true, false, false, false, true)),
        ("boreal", Land(700f, -25f, 16f, 0.15f, false, true, true, false, false, true)),
        ("tundra", Land(300f, -35f, 8f, 0.1f, false, true, true, false, false, true)),
        ("ice sheet", Land(100f, -50f, -15f, 0f, false, true, true, false, false, true)),
        ("desert", Land(200f, 10f, 40f, 0.05f, false, false, false, true, false, true)),
        ("desert, no dust weather", Land(250f, 8f, 38f, 0.06f, false, false, false, false, false, true)),
        ("extreme desert", Land(50f, 20f, 48f, 0.01f, false, false, false, true, false, true)),
        ("tropical", Land(3500f, 22f, 32f, 0.4f, true, false, false, false, true, true)),
        ("tropical, no mods", Land(3000f, 21f, 31f, 0.4f, false, false, false, false, true, false)),
        ("arid shrubland", Land(500f, 5f, 34f, 0.08f, false, false, false, false, false, true)),
        ("grassland", Land(800f, -4f, 28f, 0.15f, false, true, false, true, false, true)),
        ("swamp", Land(2000f, 2f, 28f, 0.35f, true, true, false, false, true, true)),
        ("temperate, no mods", Land(1100f, -6f, 23f, 0.2f, false, true, false, false, false, false))
    };

    /// <summary>The climate's land for one of the weather tests' lands (WeatherDynamicsTests.Lands).</summary>
    private static ClimateLand ClimateFor(WeatherDynamicsTests.Land land)
    {
        bool snow = land.weathers.Any(w => w.kind == WeatherKind.Snow), dust = land.weathers.Any(w => w.kind == WeatherKind.Dust), down = land.weathers.Any(w => w.kind == WeatherKind.Downpour);
        var b = land.b;
        var l = Land(b.rainfall, b.coldest, b.warmest, b.wetShare, b.water, snow, false, dust, down, true);
        l.b = b;
        return l;
    }
}
