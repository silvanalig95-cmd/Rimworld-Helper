using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The colony's weather record (Weather/WeatherMath.cs, Weather/WeatherBook.cs): hours make days and days make seasons,
/// records are broken only after the colony has seen enough weather and by a clear margin, a season is set against the
/// year before and named only when it was far from usual, the first snow is set against last year's across the turn of
/// the year, an event's weather requirement fits only weather that really came, and everything packs into the save and
/// back. Every event that asks for weather asks in words the record can read.
/// </summary>
public static class WeatherTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "weather: " + what);
        }

        WeatherDay Day(int day, int rainH, int snowH = 0, int stormH = 0, float min = 5f, float max = 15f, float expected = 10f)
        {
            var d = new WeatherDay { day = day, year = day / 60, dayOfYear = day % 60, quadrum = day % 60 / 15, expected = expected };
            for (int h = 0; h < 24; h++)
                WeatherMath.AddHour(d, h < rainH, h < snowH, h < stormH, false, h < 12 ? min : max);
            return d;
        }

        // ---------- Days ----------
        var wet = Day(0, 6);
        var dry = Day(1, 0);
        var snowy = Day(2, 0, 3, 0, -8f, -2f);
        Check(wet.Wet && !wet.Dry && dry.Dry && !dry.Wet, "a day with hours of rain is wet; one with nothing falling is dry");
        Check(snowy.Snowy && snowy.Frozen && !wet.Frozen, "snow for a while makes a snowy day; below freezing all day is a frozen one");
        Check(Day(3, 3).Wet == false && !Day(3, 3).Dry, "a passing shower is neither wet nor dry");
        Check(Math.Abs(wet.Mean - 10f) < 0.01f && wet.hours == 24, "a day keeps its hours, coldest and warmest");
        var run = new List<WeatherDay> { Day(10, 0), Day(11, 6), Day(12, 6), Day(13, 6) };
        Check(WeatherMath.Run(run, d => d.Wet) == 3 && WeatherMath.Count(run, 4, d => d.Wet) == 3 && WeatherMath.Count(run, 2, d => d.Dry) == 0, "rain three days running, counted back from today");

        // ---------- Records ----------
        var rec = new WeatherRecords();
        Check(WeatherMath.TempRecord(rec, -10f, 100) == WeatherRecordKind.None && WeatherMath.TempRecord(rec, -30f, 105) == WeatherRecordKind.None,
            "nothing counts as a record in the first days: every day would be one");
        Check(Math.Abs(rec.coldest + 30f) < 0.01f, "the first days still set the mark to beat");
        Check(WeatherMath.TempRecord(rec, -30.5f, 125) == WeatherRecordKind.None, "half a degree past the mark is not a record");
        Check(WeatherMath.TempRecord(rec, -31.5f, 126) == WeatherRecordKind.Coldest, "a degree past it is the coldest since the colony came");
        Check(WeatherMath.TempRecord(rec, -31.8f, 126) == WeatherRecordKind.None, "a night that keeps getting colder breaks it once");
        Check(WeatherMath.TempRecord(rec, 40f, 127) == WeatherRecordKind.Hottest || rec.hottest >= 40f, "the hottest day is kept the same way");
        var spell = new WeatherRecords { since = 0 };
        var days = new List<WeatherDay>();
        var told = new List<WeatherRecordKind>();
        void Add(WeatherDay d)
        {
            days.Add(d);
            told.Add(WeatherMath.SpellRecord(spell, days));
        }
        for (int d = 30; d < 36; d++) Add(Day(d, 6));
        Check(told.All(k => k == WeatherRecordKind.None) && spell.longestWet == 6, "a first long rain only sets the mark: it beats nothing earlier");
        Add(Day(36, 0));
        told.Clear();
        for (int d = 37; d < 45; d++) Add(Day(d, 6));
        Check(told.Count(k => k == WeatherRecordKind.LongestWet) == 1 && told[6] == WeatherRecordKind.LongestWet, "a later rain is the longest yet the day it passes the first, and is told once");
        Check(spell.longestWet == 8 && spell.wetHeld, "it holds the record while it goes on");
        var back2 = WeatherMath.UnpackRecords(WeatherMath.Pack(spell));
        Check(back2 != null && back2.wetHeld && back2.longestWet == 8 && WeatherMath.UnpackRecords("1,2,3,4,5,6,7") != null, "the spell holding the record is saved, and older saves still load");

        // ---------- Seasons ----------
        WeatherQuarter Season(int year, int q, int season, int wetDays, float mean, float expected, int storm = 0, int dryRun = 0)
        {
            var s = new WeatherQuarter { year = year, quadrum = q, season = season, days = 15, wetDays = wetDays, meanSum = mean * 15, expectedSum = expected * 15, stormHours = storm, longestDry = dryRun };
            return s;
        }
        var lastYear = Season(5500, 3, 4, 3, -5f, -5f);
        var colder = Season(5501, 3, 4, 3, -9f, -5f);
        Check(WeatherMath.Compare(colder, lastYear, out float by) == WeatherChange.Colder && Math.Abs(by - 4f) < 0.01f, "a winter four degrees colder than last year's is colder");
        Check(WeatherMath.Compare(Season(5501, 3, 4, 4, -6f, -5f), lastYear, out _) == WeatherChange.Same, "a degree and a wet day apart is the same");
        Check(WeatherMath.Compare(Season(5501, 1, 2, 9, 20f, 20f), Season(5500, 1, 2, 4, 20f, 20f), out float wetBy) == WeatherChange.Wetter && Math.Abs(wetBy - 5f) < 0.01f, "nine wet days against four is wetter");
        Check(WeatherMath.Compare(Season(5501, 1, 2, 4, 20f, 20f, 60), Season(5500, 1, 2, 4, 20f, 20f, 10), out _) == WeatherChange.Stormier, "two days of storms against a few hours is stormier");
        Check(WeatherMath.Compare(colder, new WeatherQuarter { days = 4 }, out _) == WeatherChange.Same, "a season hardly seen isn't compared");
        Check(WeatherMath.NameOf(colder, true) == null, "four degrees colder than usual has no name yet");
        Check(WeatherMath.NameOf(Season(5501, 3, 4, 3, -12f, -5f), true) == "cold", "seven degrees colder than usual here is the long cold");
        Check(WeatherMath.NameOf(Season(5501, 3, 4, 3, 2f, -5f), true) == "heat", "seven warmer is the great heat (the warm winter)");
        Check(WeatherMath.NameOf(Season(5501, 1, 2, 12, 20f, 20f), true) == "wet", "rain on most days is the wet season");
        Check(WeatherMath.NameOf(Season(5501, 1, 2, 0, 20f, 20f, 0, 13), true) == "dry" && WeatherMath.NameOf(Season(5501, 1, 2, 0, 20f, 20f, 0, 13), false) == null,
            "a long dry spell is the dry season, but not on land that is always dry");
        Check(WeatherMath.NameOf(Season(5501, 1, 2, 3, 20f, 20f, 50), true) == "storms", "two days of storms is the season of storms");
        Check(WeatherMath.NameOf(new WeatherQuarter { days = 5, meanSum = -100f }, true) == null, "a season hardly seen gets no name");
        var kept = new List<WeatherQuarter> { Season(5499, 3, 4, 0, 0, 0), lastYear, Season(5501, 0, 1, 0, 0, 0) };
        Check(WeatherMath.YearBefore(kept, colder) == lastYear, "the same season a year before is found among the kept ones");

        // ---------- The first snow ----------
        Check(WeatherMath.SnowEarlier(40, 50) == 10 && WeatherMath.SnowEarlier(50, 40) == -10, "ten days earlier, ten days later");
        Check(WeatherMath.SnowEarlier(2, 58) == -4 && WeatherMath.SnowEarlier(58, 2) == 4, "across the turn of the year");

        // ---------- What events ask ----------
        var week = new List<WeatherDay>();
        for (int d = 0; d < 7; d++) week.Add(Day(200 + d, d % 2 == 0 ? 6 : 0, 0, d == 6 ? 2 : 0));
        Check(WeatherMath.Fits("rain:4/7", week, "summer") && !WeatherMath.Fits("rain:5/7", week, "summer"), "rain on four of the last seven days, and not five");
        Check(WeatherMath.Fits("storm:1", week, "summer") && !WeatherMath.Fits("dry:2", week, "summer"), "a storm today; it hasn't been dry");
        Check(!WeatherMath.Fits("rain:1/10", week, "summer"), "not before the record has seen that many days");
        Check(WeatherMath.Fits("[fall]rain:7/7", week, "summer") && !WeatherMath.Fits("[fall]rain:7/7", week, "fall"), "a requirement for one season asks nothing in the others");
        Check(!WeatherMath.Fits("drizzle:2/3", week, "summer") && !WeatherMath.Fits("rain:x/3", week, "summer"), "a requirement it can't read doesn't fit");
        var frost = new List<WeatherDay>();
        for (int d = 0; d < 7; d++) frost.Add(Day(300 + d, 0, 0, 0, -9f, d == 0 ? 1f : -3f));
        Check(WeatherMath.Fits("frost:5/7", frost, "winter") && WeatherMath.Fits("dry:7", frost, "winter"), "below freezing all day on most days, and nothing fell");
        Check(WeatherMath.SeasonWord(1) == "spring" && WeatherMath.SeasonWord(4) == "winter" && WeatherMath.SeasonWord(6) == "winter" && WeatherMath.SeasonWord(5) == "summer", "the game's seasons in words");

        // ---------- Into the save and back ----------
        var back = WeatherMath.UnpackDay(WeatherMath.Pack(snowy));
        Check(back != null && back.day == snowy.day && back.snow == snowy.snow && Math.Abs(back.min - snowy.min) < 0.1f && back.Frozen, "a day packs and unpacks");
        var named = Season(5501, 3, 4, 3, -12f, -5f);
        named.name = "cold";
        var qback = WeatherMath.UnpackQuarter(WeatherMath.Pack(named));
        Check(qback != null && qback.name == "cold" && Math.Abs(qback.Anomaly - named.Anomaly) < 0.1f && WeatherMath.UnpackQuarter(WeatherMath.Pack(lastYear)).name == null, "a season packs with its name, or none");
        var rback = WeatherMath.UnpackRecords(WeatherMath.Pack(rec));
        Check(rback != null && Math.Abs(rback.coldest - rec.coldest) < 0.1f && rback.since == rec.since, "the records pack and unpack");
        Check(WeatherMath.UnpackDay("1,2,x") == null && WeatherMath.UnpackDay(null) == null && WeatherMath.UnpackQuarter("a,b") == null, "a damaged line is dropped, not an error");
        var trim = Enumerable.Range(0, 40).ToList();
        WeatherMath.Trim(trim, WeatherMath.MaxDays);
        Check(trim.Count == WeatherMath.MaxDays && trim[0] == 10, "only the newest days are kept");

        // ---------- The events ----------
        var form = new Regex(@"^(\[(spring|summer|fall|winter)(\|(spring|summer|fall|winter))*\])?((rain|snow|frost):\d+/\d+|(dry|storm):\d+)$");
        int asked = 0;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(f);
            foreach (var w in doc.Descendants("weather"))
                foreach (var li in w.Elements("li"))
                {
                    asked++;
                    Check(form.IsMatch(li.Value.Trim()), "the record can read " + Path.GetFileName(f) + "'s requirement \"" + li.Value + "\"");
                }
        }
        Check(asked >= 9, "the events that speak of weather ask for it (the long rains, high water, a struck tree...)");
        string gates = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Happenings/IncidentWorker_Happening.cs"));
        Check(gates.Contains("WeatherBook.Of(map)?.Fits(ext.weather)"), "every one of Rimfolk's events checks it before it can come");
        string hail = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Happenings/Workers_Nature.cs"));
        Check(hail.Contains("temperatureRange.Includes") && hail.Contains("StartNextWeather"), "hail rains only while it's warm enough to rain, and its rain doesn't run on after it");
        return cases;
    }
}
