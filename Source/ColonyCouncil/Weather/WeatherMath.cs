using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One day of weather on a map, as the colony's weather record keeps it.</summary>
    public class WeatherDay
    {
        /// <summary>The day it was (days since the game began), and the local year, quadrum (0 to 3) and day of the year (0 to 59).</summary>
        public int day = -1, year, quadrum, dayOfYear;
        /// <summary>Hours of it the record looked at, and of those, hours of rain, of snow, of storm and of fog.</summary>
        public int hours, rain, snow, storm, fog;
        /// <summary>The lowest and highest outdoor temperature looked at (°C), and the tile's usual mean for the time of year.</summary>
        public float min = 999f, max = -999f, expected;

        public bool Counted => hours > 0;

        /// <summary>It stayed below freezing all day.</summary>
        public bool Frozen => Counted && max < 0f;

        /// <summary>It rained a good part of the day.</summary>
        public bool Wet => rain >= WeatherMath.WetHours;

        /// <summary>Snow fell for a while.</summary>
        public bool Snowy => snow >= WeatherMath.SnowyHours;

        /// <summary>Nothing fell at all.</summary>
        public bool Dry => rain == 0 && snow == 0;

        public float Mean => Counted ? (min + max) / 2f : 0f;
    }

    /// <summary>One quadrum of weather on a map (a season, or a quarter of the year where seasons don't turn).</summary>
    public class WeatherQuarter
    {
        public int year, quadrum, season;
        public int days, wetDays, snowDays, dryDays, stormHours, fogHours;
        public float coldest = 999f, hottest = -999f, meanSum;
        /// <summary>The tile's usual means for its days added up (°C), from the game's climate.</summary>
        public float expectedSum;
        /// <summary>The longest run of wet days, and of days with nothing falling.</summary>
        public int longestWet, longestDry;
        /// <summary>The name it was given when it ended ("the winter of the long cold"), as a kind (see <see cref="WeatherMath.NameOf"/>), or null.</summary>
        public string name;

        public float Mean => days > 0 ? meanSum / days : 0f;

        public float Expected => days > 0 ? expectedSum / days : 0f;

        /// <summary>How much warmer (or colder, below 0) than usual for here it was.</summary>
        public float Anomaly => Mean - Expected;
    }

    /// <summary>The coldest, the hottest and the longest spells the record has seen on a map.</summary>
    public class WeatherRecords
    {
        public float coldest = 999f, hottest = -999f;
        public int coldestDay = -1, hottestDay = -1;
        public int longestWet, longestDry;
        /// <summary>The first day the record looked at.</summary>
        public int since = -1;
        /// <summary>The wet (or dry) spell going on now is the one holding the record: it's told once, when it passes an earlier one.</summary>
        public bool wetHeld, dryHeld;
    }

    /// <summary>What was broken: the coldest night, the hottest day, the longest rain or the longest dry spell since the colony came.</summary>
    public enum WeatherRecordKind
    {
        None,
        Coldest,
        Hottest,
        LongestWet,
        LongestDry
    }

    /// <summary>How a season ended against the same season a year before.</summary>
    public enum WeatherChange
    {
        Same,
        Colder,
        Warmer,
        Wetter,
        Drier,
        Stormier
    }

    /// <summary>
    /// The arithmetic of the colony's weather record (Weather/WeatherBook.cs): hours added up into days, days into
    /// quadrums, the records a day breaks, how a season compares with the year before, and when a season was unusual
    /// enough to be remembered by a name. Pure, with no game types, so Tools/Tests checks it.
    /// </summary>
    public static class WeatherMath
    {
        /// <summary>Hours of rain that make a day a wet one, and of snow a snowy one.</summary>
        public const int WetHours = 4, SnowyHours = 2;

        /// <summary>Days the record keeps day by day; quadrums it keeps (three years).</summary>
        public const int MaxDays = 30, MaxQuarters = 12;

        /// <summary>A record only counts once the colony has seen this many days of weather (else every day breaks one).</summary>
        public const int RecordAfterDays = 20;

        /// <summary>By how much a new coldest or hottest has to beat the old one to be remarked on (°C).</summary>
        public const float RecordMargin = 1f;

        /// <summary>A season this much warmer or colder than usual for here is remembered by a name (°C).</summary>
        public const float NamedAnomaly = 5f;

        /// <summary>Days in the game's year, and in a quadrum.</summary>
        public const int YearDays = 60, QuarterDays = 15;

        /// <summary>One hour looked at.</summary>
        public static void AddHour(WeatherDay d, bool rain, bool snow, bool storm, bool fog, float temp)
        {
            d.hours++;
            if (rain) d.rain++;
            if (snow) d.snow++;
            if (storm) d.storm++;
            if (fog) d.fog++;
            if (temp < d.min) d.min = temp;
            if (temp > d.max) d.max = temp;
        }

        /// <summary>How many of the last days (newest last) in a row have something, counting back from the newest.</summary>
        public static int Run(IList<WeatherDay> days, Func<WeatherDay, bool> has)
        {
            int n = 0;
            for (int i = days.Count - 1; i >= 0; i--)
            {
                if (!days[i].Counted) continue;
                if (!has(days[i])) break;
                n++;
            }
            return n;
        }

        /// <summary>How many of the last <paramref name="last"/> counted days (newest last) have something.</summary>
        public static int Count(IList<WeatherDay> days, int last, Func<WeatherDay, bool> has)
        {
            int n = 0, seen = 0;
            for (int i = days.Count - 1; i >= 0 && seen < last; i--)
            {
                if (!days[i].Counted) continue;
                seen++;
                if (has(days[i])) n++;
            }
            return n;
        }

        /// <summary>A finished day goes into its quadrum; <paramref name="days"/> already ends with it.</summary>
        public static void AddDay(WeatherQuarter q, WeatherDay d, IList<WeatherDay> days)
        {
            if (!d.Counted) return;
            q.days++;
            if (d.Wet) q.wetDays++;
            if (d.Snowy) q.snowDays++;
            if (d.Dry) q.dryDays++;
            q.stormHours += d.storm;
            q.fogHours += d.fog;
            if (d.min < q.coldest) q.coldest = d.min;
            if (d.max > q.hottest) q.hottest = d.max;
            q.meanSum += d.Mean;
            q.expectedSum += d.expected;
            q.longestWet = Math.Max(q.longestWet, Math.Min(Run(days, x => x.Wet), q.days));
            q.longestDry = Math.Max(q.longestDry, Math.Min(Run(days, x => x.Dry), q.days));
        }

        /// <summary>
        /// A temperature looked at now against the coldest and hottest the colony has seen, updating them: the record it
        /// breaks, only once the colony has seen <see cref="RecordAfterDays"/> days and by a clear margin (a cold
        /// night that keeps getting colder breaks it once, then again only a degree further down).
        /// </summary>
        public static WeatherRecordKind TempRecord(WeatherRecords r, float temp, int day)
        {
            if (r.since < 0) r.since = day;
            bool counts = day - r.since >= RecordAfterDays;
            var broken = WeatherRecordKind.None;
            if (temp < r.coldest)
            {
                if (counts && r.coldest < 900f && temp <= r.coldest - RecordMargin) broken = WeatherRecordKind.Coldest;
                if (broken == WeatherRecordKind.Coldest || !counts || r.coldest > 900f || r.coldestDay != day || temp <= r.coldest - RecordMargin)
                {
                    r.coldest = temp;
                    r.coldestDay = day;
                }
            }
            if (temp > r.hottest)
            {
                if (counts && r.hottest > -900f && temp >= r.hottest + RecordMargin) broken = WeatherRecordKind.Hottest;
                if (broken == WeatherRecordKind.Hottest || !counts || r.hottest < -900f || r.hottestDay != day || temp >= r.hottest + RecordMargin)
                {
                    r.hottest = temp;
                    r.hottestDay = day;
                }
            }
            return broken;
        }

        /// <summary>
        /// The spell a finished day makes the longest yet (<paramref name="days"/> already ends with it): rain on more
        /// days running than any earlier wet spell (of four days or more), or nothing falling for longer than any
        /// earlier dry spell (of a week or more). Told once, the day the spell passes the earlier one; a spell that only
        /// beats itself isn't. Only once the colony has seen <see cref="RecordAfterDays"/> days.
        /// </summary>
        public static WeatherRecordKind SpellRecord(WeatherRecords r, IList<WeatherDay> days)
        {
            if (days.Count == 0) return WeatherRecordKind.None;
            int day = days[days.Count - 1].day;
            if (r.since < 0) r.since = day;
            bool counts = day - r.since >= RecordAfterDays;
            var broken = WeatherRecordKind.None;
            int wet = Run(days, x => x.Wet), dry = Run(days, x => x.Dry);
            if (wet == 0) r.wetHeld = false;
            if (dry == 0) r.dryHeld = false;
            if (wet > r.longestWet)
            {
                if (!r.wetHeld)
                {
                    if (counts && r.longestWet >= 4) broken = WeatherRecordKind.LongestWet;
                    r.wetHeld = true;
                }
                r.longestWet = wet;
            }
            if (dry > r.longestDry)
            {
                if (!r.dryHeld)
                {
                    if (counts && broken == WeatherRecordKind.None && r.longestDry >= 7) broken = WeatherRecordKind.LongestDry;
                    r.dryHeld = true;
                }
                r.longestDry = dry;
            }
            return broken;
        }

        /// <summary>
        /// How a finished season compares with the same one a year before: colder or warmer by 3 °C on average, wetter
        /// or drier by at least three wet days and half again, or stormier by a day's storms and twice as many. The
        /// clearest difference wins; Same when neither was looked at for most of the season.
        /// </summary>
        public static WeatherChange Compare(WeatherQuarter now, WeatherQuarter before, out float by)
        {
            by = 0f;
            if (now == null || before == null || now.days < 10 || before.days < 10) return WeatherChange.Same;
            float temp = now.Mean - before.Mean;
            int wet = now.wetDays - before.wetDays;
            float tempScore = Math.Abs(temp) >= 3f ? Math.Abs(temp) / 3f : 0f;
            float wetScore = Math.Abs(wet) >= 3 && Ratio(now.wetDays, before.wetDays) >= 1.5f ? Math.Abs(wet) / 3f : 0f;
            float stormScore = now.stormHours - before.stormHours >= 24 && now.stormHours >= before.stormHours * 2 ? (now.stormHours - before.stormHours) / 24f : 0f;
            if (tempScore <= 0f && wetScore <= 0f && stormScore <= 0f) return WeatherChange.Same;
            if (tempScore >= wetScore && tempScore >= stormScore)
            {
                by = Math.Abs(temp);
                return temp < 0f ? WeatherChange.Colder : WeatherChange.Warmer;
            }
            if (wetScore >= stormScore)
            {
                by = Math.Abs(wet);
                return wet > 0 ? WeatherChange.Wetter : WeatherChange.Drier;
            }
            by = now.stormHours - before.stormHours;
            return WeatherChange.Stormier;
        }

        private static float Ratio(int a, int b)
        {
            int hi = Math.Max(a, b), lo = Math.Min(a, b);
            return lo <= 0 ? (hi > 0 ? 99f : 1f) : hi / (float)lo;
        }

        /// <summary>
        /// How many days earlier this first snow came than last year's (later below 0), by the day of the year: within
        /// half a year either way, as the snow comes round once a year.
        /// </summary>
        public static int SnowEarlier(int thisDay, int lastDay)
        {
            int d = ((lastDay - thisDay) % YearDays + YearDays) % YearDays;
            return d > YearDays / 2 ? d - YearDays : d;
        }

        /// <summary>
        /// The name a finished season is remembered by, as a kind, or null for an ordinary one: "cold" or "heat" for
        /// one <see cref="NamedAnomaly"/> °C colder or warmer than usual for here; "storms" for one with two days or
        /// more of storms; "wet" for one where most days were wet; "dry" for one where nothing fell for twelve days
        /// running on land that usually has its rain (<paramref name="usuallyWet"/>). The strongest wins. Only for a
        /// season looked at for most of its days.
        /// </summary>
        public static string NameOf(WeatherQuarter q, bool usuallyWet)
        {
            if (q == null || q.days < 10) return null;
            float a = q.Anomaly;
            float coldScore = a <= -NamedAnomaly ? -a / NamedAnomaly : 0f;
            float heatScore = a >= NamedAnomaly ? a / NamedAnomaly : 0f;
            float stormScore = q.stormHours >= 48 ? q.stormHours / 48f : 0f;
            float wetScore = q.wetDays >= q.days * 0.7f ? q.wetDays / (q.days * 0.7f) : 0f;
            float dryScore = usuallyWet && q.longestDry >= 12 ? q.longestDry / 12f : 0f;
            float best = 0f;
            string name = null;
            void Try(float score, string kind)
            {
                if (score > best)
                {
                    best = score;
                    name = kind;
                }
            }
            Try(coldScore, "cold");
            Try(heatScore, "heat");
            Try(stormScore, "storms");
            Try(wetScore, "wet");
            Try(dryScore, "dry");
            return name;
        }

        /// <summary>
        /// Whether the recent weather fits a requirement of an event's (all of its list must fit): "rain:N/M" at least N
        /// wet days in the last M, "snow:N/M" snowy days, "frost:N/M" days that stayed below freezing, "dry:M" nothing
        /// fell in the last M days, "storm:M" a storm in the last M days. A leading "[spring|fall]" limits it to those
        /// seasons (spring, summer, fall, winter): in others it fits. <paramref name="days"/> ends with today, counted
        /// so far. False when the record hasn't seen M days yet, or the requirement can't be read.
        /// </summary>
        public static bool Fits(string req, IList<WeatherDay> days, string season)
        {
            if (string.IsNullOrEmpty(req)) return true;
            string r = req.Trim();
            if (r.StartsWith("["))
            {
                int close = r.IndexOf(']');
                if (close < 0) return false;
                var seasons = r.Substring(1, close - 1).Split('|');
                if (Array.IndexOf(seasons, season) < 0) return true;
                r = r.Substring(close + 1).Trim();
            }
            int colon = r.IndexOf(':');
            if (colon <= 0) return false;
            string kind = r.Substring(0, colon);
            string[] args = r.Substring(colon + 1).Split('/');
            if (!int.TryParse(args[args.Length - 1], out int last) || last <= 0) return false;
            int need = 1;
            if (args.Length == 2 && !int.TryParse(args[0], out need)) return false;
            int counted = 0;
            foreach (var d in days)
                if (d.Counted) counted++;
            if (counted < last) return false;
            switch (kind)
            {
                case "rain": return Count(days, last, x => x.Wet) >= need;
                case "snow": return Count(days, last, x => x.Snowy) >= need;
                case "frost": return Count(days, last, x => x.Frozen) >= need;
                case "dry": return Count(days, last, x => !x.Dry) == 0;
                case "storm": return Count(days, last, x => x.storm > 0) >= 1;
                default: return false;
            }
        }

        /// <summary>The season's word for requirements and lines: spring, summer, fall or winter (a season that never turns counts as summer or winter).</summary>
        public static string SeasonWord(int season)
        {
            // The game's Season: Undefined 0, Spring 1, Summer 2, Fall 3, Winter 4, PermanentSummer 5, PermanentWinter 6.
            switch (season)
            {
                case 1: return "spring";
                case 3: return "fall";
                case 4:
                case 6: return "winter";
                default: return "summer";
            }
        }

        // ---------------- Kept in the save as short lines ----------------

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        private static string F(float v) => v.ToString("0.#", Inv);

        private static float PF(string s) => float.Parse(s, Inv);

        private static int PI(string s) => int.Parse(s, Inv);

        public static string Pack(WeatherDay d) =>
            string.Join(",", d.day, d.year, d.quadrum, d.dayOfYear, d.hours, d.rain, d.snow, d.storm, d.fog, F(d.min), F(d.max), F(d.expected));

        public static WeatherDay UnpackDay(string s)
        {
            try
            {
                var p = s?.Split(',');
                if (p == null || p.Length < 12) return null;
                return new WeatherDay
                {
                    day = PI(p[0]), year = PI(p[1]), quadrum = PI(p[2]), dayOfYear = PI(p[3]), hours = PI(p[4]), rain = PI(p[5]), snow = PI(p[6]),
                    storm = PI(p[7]), fog = PI(p[8]), min = PF(p[9]), max = PF(p[10]), expected = PF(p[11])
                };
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        public static string Pack(WeatherQuarter q) =>
            string.Join(",", q.year, q.quadrum, q.season, q.days, q.wetDays, q.snowDays, q.dryDays, q.stormHours, q.fogHours, F(q.coldest), F(q.hottest),
                F(q.meanSum), F(q.expectedSum), q.longestWet, q.longestDry, q.name ?? "");

        public static WeatherQuarter UnpackQuarter(string s)
        {
            try
            {
                var p = s?.Split(',');
                if (p == null || p.Length < 16) return null;
                return new WeatherQuarter
                {
                    year = PI(p[0]), quadrum = PI(p[1]), season = PI(p[2]), days = PI(p[3]), wetDays = PI(p[4]), snowDays = PI(p[5]), dryDays = PI(p[6]),
                    stormHours = PI(p[7]), fogHours = PI(p[8]), coldest = PF(p[9]), hottest = PF(p[10]), meanSum = PF(p[11]), expectedSum = PF(p[12]),
                    longestWet = PI(p[13]), longestDry = PI(p[14]), name = p[15].Length > 0 ? p[15] : null
                };
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        public static string Pack(WeatherRecords r) => string.Join(",", F(r.coldest), F(r.hottest), r.coldestDay, r.hottestDay, r.longestWet, r.longestDry, r.since, r.wetHeld ? 1 : 0, r.dryHeld ? 1 : 0);

        public static WeatherRecords UnpackRecords(string s)
        {
            try
            {
                var p = s?.Split(',');
                if (p == null || p.Length < 7) return null;
                return new WeatherRecords
                {
                    coldest = PF(p[0]), hottest = PF(p[1]), coldestDay = PI(p[2]), hottestDay = PI(p[3]), longestWet = PI(p[4]), longestDry = PI(p[5]), since = PI(p[6]),
                    wetHeld = p.Length > 7 && p[7] == "1", dryHeld = p.Length > 8 && p[8] == "1"
                };
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        /// <summary>The same quadrum a year earlier, among the kept ones, or null.</summary>
        public static WeatherQuarter YearBefore(IList<WeatherQuarter> kept, WeatherQuarter q)
        {
            if (kept == null || q == null) return null;
            for (int i = kept.Count - 1; i >= 0; i--)
                if (kept[i].quadrum == q.quadrum && kept[i].year == q.year - 1) return kept[i];
            return null;
        }

        /// <summary>Keeps the newest <paramref name="max"/> of a list (newest last).</summary>
        public static void Trim<T>(List<T> list, int max)
        {
            if (list.Count > max) list.RemoveRange(0, list.Count - max);
        }
    }
}
