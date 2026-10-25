using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's weather record for one home map (the arithmetic is in WeatherMath): once an hour it notes whether it
    /// rains, snows, storms or is foggy and how cold or warm it is outside; the hours make days and the days make the
    /// season. What it gives: the coldest night, the hottest day, the longest rain and the longest dry spell since the
    /// colony came, remarked on when broken; each season set against the same season a year before; a season far from
    /// this land's usual, remembered by a name ("the winter of the long cold") in the chronicle and the annals; the
    /// first snow against last year's; and the recent weather Rimfolk's events ask for ("rain:5/7"), so an event never
    /// speaks of rain that didn't fall. A month of days and three years of seasons are kept, as short lines.
    /// </summary>
    public class WeatherBook : MapComponent
    {
        // Kept in the save as short lines (WeatherMath.Pack), worked on unpacked.
        private List<string> days = new List<string>();
        private List<string> quarters = new List<string>();
        private string today, quarter, records;
        private int lastSnowDay = -9999, firstSnowYear = -1, firstSnowDoy = -1;
        private int lastRecordNews = -9999999;
        /// <summary>Seasonal Weather's earthquake was going on at the last look; the year of the last quakes, and how many that year.</summary>
        private bool quakeOn, tornadoOn;
        private int quakeYear = -1, quakeCount;

        [Unsaved] private List<WeatherDay> dayList = new List<WeatherDay>();
        [Unsaved] private List<WeatherQuarter> quarterList = new List<WeatherQuarter>();
        [Unsaved] private WeatherDay now;
        [Unsaved] private WeatherQuarter cur;
        [Unsaved] private WeatherRecords rec = new WeatherRecords();

        /// <summary>The weather dynamics' part (Weather/WeatherDynamics.cs): the forecast and the events' pulls are saved with the record.</summary>
        [Unsaved] internal readonly WeatherDynamics.State dyn = new WeatherDynamics.State();

        /// <summary>A day without snow for this long, then snow: the first snow of the winter (as WeatherTalk has it).</summary>
        private const int SnowGapDays = 10;

        /// <summary>The land usually has its rain (mm a year): only then is a long dry spell worth a name.</summary>
        private const float WetLand = 600f;

        public WeatherBook(Map map) : base(map)
        {
        }

        public static WeatherBook Of(Map map) => map?.GetComponent<WeatherBook>();

        /// <summary>The days kept, today last (counted so far).</summary>
        public List<WeatherDay> Days
        {
            get
            {
                var list = new List<WeatherDay>(dayList);
                if (now != null && now.Counted) list.Add(now);
                return list;
            }
        }

        /// <summary>The days kept before today, and today so far (null before the first look).</summary>
        public List<WeatherDay> DaysBefore => dayList;

        public WeatherDay Today => now;

        public List<WeatherQuarter> Quarters => quarterList;

        public WeatherQuarter Current => cur;

        public WeatherRecords Records => rec;

        /// <summary>
        /// The recent weather fits all of an event's requirements (WeatherMath.Fits). Off maps that aren't home, or before
        /// the record has seen enough days, it doesn't fit.
        /// </summary>
        public bool Fits(List<string> requirements)
        {
            if (requirements.NullOrEmpty()) return true;
            var list = Days;
            string season = WeatherMath.SeasonWord((int)GenLocalDate.Season(map));
            foreach (var r in requirements)
                if (!WeatherMath.Fits(r, list, season)) return false;
            return true;
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (Sched.Every(tick, Sched.RareTicks, Sched.RareWeather) && (dyn.offset != 0f || WeatherDynamics.TemperatureOn))
            {
                try
                {
                    WeatherDynamics.Rare(map, this);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("weather temperature", e);
                }
            }
            if (!Sched.Hourly(tick, Sched.Weather) || !map.IsPlayerHome || map.weatherManager == null) return;
            try
            {
                Hour(tick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather record", e);
            }
            try
            {
                WeatherDynamics.Hourly(map, this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather forecast", e);
            }
        }

        private void Hour(int tick)
        {
            int year = GenLocalDate.Year(map), doy = GenLocalDate.DayOfYear(map);
            int day = year * WeatherMath.YearDays + doy;
            if (now == null || now.day != day)
            {
                if (now != null) EndDay(now, tick);
                now = new WeatherDay
                {
                    day = day, year = year, quadrum = doy / WeatherMath.QuarterDays, dayOfYear = doy,
                    expected = SeasonUtil.TwelfthTemp(map, GenLocalDate.Twelfth(map))
                };
                if (cur == null || cur.year != now.year || cur.quadrum != now.quadrum)
                {
                    if (cur != null) EndQuarter(cur, tick);
                    cur = new WeatherQuarter { year = now.year, quadrum = now.quadrum, season = (int)GenLocalDate.Season(map) };
                }
            }
            var wm = map.weatherManager;
            bool snow = wm.SnowRate >= 0.05f;
            // The game's snows count as rain too (they put out fires): rain is rain without snow.
            bool rain = !snow && wm.RainRate >= 0.1f;
            bool storm = Flavor.IsStorm(wm.curWeather);
            bool fog = wm.curWeather?.defName != null && wm.curWeather.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0;
            float temp = map.mapTemperature.OutdoorTemp;
            WeatherMath.AddHour(now, rain, snow, storm, fog, temp);
            var broken = WeatherMath.TempRecord(rec, temp, day);
            if (broken != WeatherRecordKind.None) RecordBroken(broken, temp, tick);
            // A storm keeps a trade caravan a little longer (Weather/WeatherVisitors.cs).
            WeatherVisitors.Hourly(map);
            // Seasonal Weather's earthquake: counted for the annals, and gone round after (Weather/WeatherDisasters.cs).
            bool quake = WeatherDisasters.Active && map.gameConditionManager.ActiveConditions.Any(c => c.def.defName == "Earthquake");
            if (quake && !quakeOn)
            {
                if (quakeYear != year)
                {
                    quakeYear = year;
                    quakeCount = 0;
                }
                quakeCount++;
            }
            if (!quake && quakeOn) WeatherDisasters.QuakeOver(map);
            quakeOn = quake;
            // A tornado crossing the land (Disasters: Tornado, or the game's own): the chronicle, and a journal after.
            bool tornado = WeatherMods.TornadoOn(map);
            if (tornado && !tornadoOn) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Weather_TornadoChronicle".Translate().Resolve()));
            if (!tornado && tornadoOn)
            {
                var home = CalendarTracker.AtHome(map);
                if (home.Count > 0) CouncilGameComponent.Instance?.journals.WriteTagged(home.RandomElement(), "Council_Journal_Tornado", new string[0]);
            }
            tornadoOn = tornado;
        }

        private void EndDay(WeatherDay d, int tick)
        {
            if (!d.Counted) return;
            dayList.Add(d);
            WeatherMath.Trim(dayList, WeatherMath.MaxDays);
            if (cur != null && cur.year == d.year && cur.quadrum == d.quadrum) WeatherMath.AddDay(cur, d, dayList);
            var spell = WeatherMath.SpellRecord(rec, dayList);
            if (spell != WeatherRecordKind.None) RecordBroken(spell, 0f, tick);
            if (d.Snowy) FirstSnow(d);
        }

        /// <summary>
        /// The first snow of the year (snow after ten days without), set against last year's by the day of the year. A
        /// first snow the record wasn't watching long enough before to be sure of isn't compared with, the next year.
        /// </summary>
        private void FirstSnow(WeatherDay d)
        {
            bool first = d.day - lastSnowDay > SnowGapDays;
            lastSnowDay = d.day;
            if (!first || firstSnowYear == d.year) return;
            int prevYear = firstSnowYear, prevDoy = firstSnowDoy;
            bool watched = rec.since >= 0 && d.day - rec.since > SnowGapDays;
            firstSnowYear = d.year;
            firstSnowDoy = watched ? d.dayOfYear : -1;
            if (!watched || prevYear != d.year - 1 || prevDoy < 0) return;
            int earlier = WeatherMath.SnowEarlier(d.dayOfYear, prevDoy);
            if (Math.Abs(earlier) < 5) return;
            News.Post((earlier > 0 ? "CC_Weather_SnowEarlier" : "CC_Weather_SnowLater").Translate(Math.Abs(earlier)).Resolve(), LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
        }

        /// <summary>A record broken: a word from someone in view (with the touches), and the news (no more than one every two days).</summary>
        private void RecordBroken(WeatherRecordKind kind, float temp, int tick)
        {
            if (tick - lastRecordNews < GenDate.TicksPerDay * 2 && tick >= lastRecordNews) return;
            lastRecordNews = tick;
            string text, pool;
            switch (kind)
            {
                case WeatherRecordKind.Coldest:
                    text = "CC_Weather_RecordColdest".Translate(temp.ToStringTemperature("F0")).Resolve();
                    pool = "Council_Pool_SayRecordCold";
                    break;
                case WeatherRecordKind.Hottest:
                    text = "CC_Weather_RecordHottest".Translate(temp.ToStringTemperature("F0")).Resolve();
                    pool = "Council_Pool_SayRecordHot";
                    break;
                case WeatherRecordKind.LongestWet:
                    text = "CC_Weather_RecordWet".Translate(rec.longestWet).Resolve();
                    pool = "Council_Pool_SayRecordWet";
                    break;
                default:
                    text = "CC_Weather_RecordDry".Translate(rec.longestDry).Resolve();
                    pool = "Council_Pool_SayRecordDry";
                    break;
            }
            WeatherTalk.SayOne(map, pool);
            News.Post(text, LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
        }

        /// <summary>A season ended: set against the year before, and named if it was far from usual here.</summary>
        private void EndQuarter(WeatherQuarter q, int tick)
        {
            if (q.days < 10)
            {
                quarterList.Add(q);
                WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
                return;
            }
            q.name = WeatherMath.NameOf(q, map.TileInfo != null && map.TileInfo.rainfall >= WetLand);
            var before = WeatherMath.YearBefore(quarterList, q);
            quarterList.Add(q);
            WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
            var comp = CouncilGameComponent.Instance;
            string season = SeasonLabel(q);
            if (q.name != null)
            {
                string named = NameText(q);
                string text = "CC_Weather_Named".Translate(named, Why(q)).Resolve();
                News.Post(text, LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Weather_NamedChronicle".Translate(named.CapitalizeFirst()).Resolve()));
            }
            var change = WeatherMath.Compare(q, before, out float by);
            if (change == WeatherChange.Same) return;
            string line = Change(change, season, q, before, by);
            News.Post(line, LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, line));
            // Someone who has lived through both writes it down: the longest with the colony.
            var writer = CalendarTracker.AtHome(map).Where(p => !p.DevelopmentalStage.Baby())
                .OrderByDescending(p => p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0).FirstOrDefault();
            if (writer != null) comp?.journals.WriteTagged(writer, "Council_Journal_SeasonCompare", new[] { change.ToString().ToLowerInvariant(), WeatherMath.SeasonWord(q.season) }, null, season.Named("SEASON"));
        }

        private static string SeasonLabel(WeatherQuarter q) => ((Season)q.season).Label();

        /// <summary>The name a season is remembered by: "the winter of the long cold", "the warm winter", "the dry summer".</summary>
        public static string NameText(WeatherQuarter q)
        {
            string season = SeasonLabel(q);
            if (q.name == "heat" && WeatherMath.SeasonWord(q.season) == "winter") return "CC_Weather_Name_WarmWinter".Translate().Resolve();
            return ("CC_Weather_Name_" + q.name).Translate(season).Resolve();
        }

        private static string Why(WeatherQuarter q)
        {
            switch (q.name)
            {
                case "cold": return "CC_Weather_Why_cold".Translate((-q.Anomaly).ToStringTemperatureOffset("F0")).Resolve();
                case "heat": return "CC_Weather_Why_heat".Translate(q.Anomaly.ToStringTemperatureOffset("F0")).Resolve();
                case "storms": return "CC_Weather_Why_storms".Translate(q.stormHours).Resolve();
                case "wet": return "CC_Weather_Why_wet".Translate(q.wetDays, q.days).Resolve();
                default: return "CC_Weather_Why_dry".Translate(q.longestDry).Resolve();
            }
        }

        private static string Change(WeatherChange change, string season, WeatherQuarter q, WeatherQuarter before, float by)
        {
            switch (change)
            {
                case WeatherChange.Colder: return "CC_Weather_Colder".Translate(season, by.ToStringTemperatureOffset("F0")).Resolve();
                case WeatherChange.Warmer: return "CC_Weather_Warmer".Translate(season, by.ToStringTemperatureOffset("F0")).Resolve();
                case WeatherChange.Wetter: return "CC_Weather_Wetter".Translate(season, q.wetDays, before.wetDays).Resolve();
                case WeatherChange.Drier: return "CC_Weather_Drier".Translate(season, q.wetDays, before.wetDays).Resolve();
                default: return "CC_Weather_Stormier".Translate(season, q.stormHours, before.stormHours).Resolve();
            }
        }

        // ---------------- What the pages show ----------------

        /// <summary>The year's weather for the annals: the seasons it is remembered by, and how cold and hot it got. Null if there's nothing to say.</summary>
        public string AnnalsLine(int year)
        {
            string quakes = quakeYear == year && quakeCount > 0 ? "CC_Annals_WeatherQuakes".Translate(quakeCount).Resolve() : null;
            var seasons = quarterList.Where(q => q.year == year && q.days >= 10).ToList();
            if (seasons.Count < 2) return quakes;
            var named = seasons.Where(q => q.name != null).Select(NameText).ToList();
            float coldest = seasons.Min(q => q.coldest), hottest = seasons.Max(q => q.hottest);
            string range = "CC_Annals_WeatherRange".Translate(coldest.ToStringTemperature("F0"), hottest.ToStringTemperature("F0")).Resolve();
            string line = named.Count > 0 ? "CC_Annals_WeatherNamed".Translate(TextUtil.JoinAnd(named), range).Resolve() : range;
            // What the year was, measured against the years before it and the land's usual: "It was a wet and warm year."
            var (wet, temp) = WeatherDynamicsMath.YearWordsFor(quarterList, year);
            var words = new List<string>();
            if (wet != null) words.Add(("CC_Annals_Year_" + wet).Translate().Resolve());
            if (temp != null) words.Add(("CC_Annals_Year_" + temp).Translate().Resolve());
            if (words.Count > 0) line = "CC_Annals_WeatherYear".Translate(TextUtil.JoinAnd(words)).Resolve() + " " + line;
            return quakes != null ? line + " " + quakes : line;
        }

        /// <summary>A few lines for the Overview: the forecast, this season so far against last year's, the records, the last season with a name.</summary>
        public List<string> OverviewLines()
        {
            var lines = new List<string>();
            string forecast = WeatherDynamics.ForecastLine(this);
            if (forecast != null) lines.Add(forecast);
            if (cur != null && cur.days >= 3)
            {
                var before = WeatherMath.YearBefore(quarterList, cur);
                string season = SeasonLabel(cur);
                if (before != null && before.days >= 10)
                {
                    float diff = cur.Mean - before.Mean;
                    lines.Add("CC_Overview_WeatherSoFar".Translate(season.CapitalizeFirst(), (diff >= 0f ? "CC_Overview_WeatherWarmer" : "CC_Overview_WeatherColder").Translate(Math.Abs(diff).ToStringTemperatureOffset("F0")).Resolve(),
                        cur.wetDays, cur.days, before.wetDays, before.days).Resolve());
                }
                else lines.Add("CC_Overview_WeatherSoFarFirst".Translate(season.CapitalizeFirst(), cur.wetDays, cur.days).Resolve());
            }
            if (rec.coldest < 900f && rec.since >= 0 && (now?.day ?? 0) - rec.since >= WeatherMath.RecordAfterDays)
                lines.Add("CC_Overview_WeatherRecords".Translate(rec.coldest.ToStringTemperature("F0"), rec.hottest.ToStringTemperature("F0"), rec.longestWet, rec.longestDry).Resolve());
            var last = quarterList.LastOrDefault(q => q.name != null);
            if (last != null) lines.Add("CC_Overview_WeatherNamed".Translate(NameText(last).CapitalizeFirst(), last.year).Resolve());
            return lines;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                days = dayList.Select(WeatherMath.Pack).ToList();
                quarters = quarterList.Select(WeatherMath.Pack).ToList();
                today = now != null ? WeatherMath.Pack(now) : null;
                quarter = cur != null ? WeatherMath.Pack(cur) : null;
                records = WeatherMath.Pack(rec);
            }
            Scribe_Collections.Look(ref days, "days", LookMode.Value);
            Scribe_Collections.Look(ref quarters, "quarters", LookMode.Value);
            Scribe_Values.Look(ref today, "today");
            Scribe_Values.Look(ref quarter, "quarter");
            Scribe_Values.Look(ref records, "records");
            Scribe_Values.Look(ref lastSnowDay, "lastSnowDay", -9999);
            Scribe_Values.Look(ref firstSnowYear, "firstSnowYear", -1);
            Scribe_Values.Look(ref firstSnowDoy, "firstSnowDoy", -1);
            Scribe_Values.Look(ref lastRecordNews, "lastRecordNews", -9999999);
            Scribe_Values.Look(ref quakeOn, "quakeOn");
            Scribe_Values.Look(ref tornadoOn, "tornadoOn");
            Scribe_Values.Look(ref quakeYear, "quakeYear", -1);
            Scribe_Values.Look(ref quakeCount, "quakeCount");
            dyn.Expose();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dayList = (days ?? new List<string>()).Select(WeatherMath.UnpackDay).Where(d => d != null).ToList();
                quarterList = (quarters ?? new List<string>()).Select(WeatherMath.UnpackQuarter).Where(q => q != null).ToList();
                now = today != null ? WeatherMath.UnpackDay(today) : null;
                cur = quarter != null ? WeatherMath.UnpackQuarter(quarter) : null;
                rec = (records != null ? WeatherMath.UnpackRecords(records) : null) ?? new WeatherRecords();
                WeatherMath.Trim(dayList, WeatherMath.MaxDays);
                WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
            }
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Only the lists in memory are kept between saves.
                days = new List<string>();
                quarters = new List<string>();
            }
        }
    }
}
