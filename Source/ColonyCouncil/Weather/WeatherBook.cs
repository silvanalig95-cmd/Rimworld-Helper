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
    /// first snow against last year's; the recent weather Rimfolk's events ask for ("rain:5/7"), so an event never
    /// speaks of rain that didn't fall; last winter as the advisor reads it (how long the cold lasted, the coldest night,
    /// how long the snow lay: LastWinter); and the disasters, each with its work party after (Weather/AfterStorm.cs) and
    /// counted for the annals. A month of days and three years of seasons are kept, as short lines.
    /// </summary>
    public class WeatherBook : MapComponent
    {
        // Kept in the save as short lines (WeatherMath.Pack), worked on unpacked.
        private List<string> days = new List<string>();
        private List<string> quarters = new List<string>();
        private string today, quarter, records;
        private int lastSnowDay = -9999, firstSnowYear = -1, firstSnowDoy = -1;
        private int lastRecordNews = -9999999;
        /// <summary>
        /// The disasters going on at the last look (AfterStorm.OnNow, comma between), the work parties waiting for their time
        /// (AfterStormMath: "kind@tick;..."), and how many of each came this year and the year before ("year:kind=n,...|...").
        /// </summary>
        private string stormsOn = "", disasters = "";
        internal string afterWaiting = "";
        /// <summary>Kept before the disasters were: Seasonal Weather's earthquake and a tornado going on, and the year's quakes (read from an older save only).</summary>
        private bool quakeOn, tornadoOn;
        private int quakeYear = -1, quakeCount;

        [Unsaved] private List<WeatherDay> dayList = new List<WeatherDay>();
        [Unsaved] private List<WeatherQuarter> quarterList = new List<WeatherQuarter>();
        [Unsaved] private WeatherDay now;
        [Unsaved] private WeatherQuarter cur;
        [Unsaved] private WeatherRecords rec = new WeatherRecords();

        /// <summary>The weather dynamics' part (Weather/WeatherDynamics.cs): the forecast and the events' pulls are saved with the record.</summary>
        [Unsaved] internal readonly WeatherDynamics.State dyn = new WeatherDynamics.State();

        /// <summary>The land's climate's part (Weather/LandClimate.cs): the season begun, the climate's push, what was told, the land's record.</summary>
        [Unsaved] internal readonly LandClimate.State clim = new LandClimate.State();

        /// <summary>A day without snow for this long, then snow: the first snow of the winter (as WeatherTalk has it).</summary>
        private const int SnowGapDays = 10;

        /// <summary>The land usually has its rain (mm a year): only then is a long dry spell worth a name.</summary>
        private const float WetLand = 600f;

        public WeatherBook(Map map) : base(map)
        {
        }

        private static Map lastMap;
        private static WeatherBook lastBook;
        private static Game lastGame;

        /// <summary>The map's weather record (the last one asked for is kept at hand: the temperature asks for it often).</summary>
        public static WeatherBook Of(Map map)
        {
            if (map == null) return null;
            if (ReferenceEquals(map, lastMap) && lastBook != null && ReferenceEquals(lastGame, Verse.Current.Game)) return lastBook;
            var book = map.GetComponent<WeatherBook>();
            lastMap = map;
            lastBook = book;
            lastGame = Verse.Current.Game;
            return book;
        }

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
            if (Sched.Every(tick, Sched.RareTicks, Sched.RareWeather) && (dyn.offset != 0f || WeatherDynamics.TemperatureOn || clim.climate != 0f || LandClimate.On))
            {
                using var rare = PassClock.Time(Pass.RareWeather);
                try
                {
                    WeatherDynamics.Rare(map, this);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("weather temperature", e);
                }
                try
                {
                    LandClimate.Rare(map, this);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("climate temperature", e);
                }
            }
            if (!Sched.Hourly(tick, Sched.Weather) || !map.IsPlayerHome || map.weatherManager == null) return;
            using var clock = PassClock.Time(Pass.Weather);
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
            try
            {
                LandClimate.Hourly(map, this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land's climate", e);
            }
            try
            {
                RainWash.Hourly(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rain washing", e);
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
                    cur = new WeatherQuarter { year = now.year, quadrum = now.quadrum, season = (int)GenLocalDate.Season(map), counted = true };
                }
            }
            var wm = map.weatherManager;
            bool snow = wm.SnowRate >= 0.05f;
            // The game's snows count as rain too (they put out fires): rain is rain without snow.
            bool rain = !snow && wm.RainRate >= 0.1f;
            bool storm = Flavor.IsStorm(wm.curWeather);
            bool fog = wm.curWeather?.defName != null && wm.curWeather.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0;
            float temp = map.mapTemperature.OutdoorTemp;
            WeatherMath.AddHour(now, rain, snow, storm, fog, temp, SnowLies(map));
            var broken = WeatherMath.TempRecord(rec, temp, day);
            if (broken != WeatherRecordKind.None) RecordBroken(broken, temp, tick);
            // A storm keeps a trade caravan a little longer (Weather/WeatherVisitors.cs).
            WeatherVisitors.Hourly(map);
            // The disasters (an earthquake, a tornado, a flashstorm, hail, a flood): counted for the annals, and a work
            // party after each (Weather/AfterStorm.cs).
            AfterStorm.Hourly(map, this, year);
        }

        /// <summary>
        /// Snow lies on most of the open land: of the open ground looked at on a coarse grid over the map (every tenth
        /// cell each way, no roof, no water), half or more under a layer of snow.
        /// </summary>
        private static bool SnowLies(Map map)
        {
            var grid = map.snowGrid;
            if (grid == null) return false;
            int open = 0, covered = 0;
            var size = map.Size;
            for (int x = 5; x < size.x; x += 10)
                for (int z = 5; z < size.z; z += 10)
                {
                    var c = new IntVec3(x, 0, z);
                    if (c.Roofed(map) || c.GetTerrain(map)?.IsWater == true) continue;
                    open++;
                    if (grid.GetDepth(c) >= SnowLayer) covered++;
                }
            return open > 0 && covered * 2 >= open;
        }

        /// <summary>Snow this deep on a cell (of the game's 0 to 1) is a layer that lies, not a dusting.</summary>
        private const float SnowLayer = 0.1f;

        // ---------------- The disasters (Weather/AfterStorm.cs) ----------------

        /// <summary>The disasters going on at the last look.</summary>
        internal HashSet<string> StormsOn
        {
            get => new HashSet<string>((stormsOn ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(AfterStormMath.IsKind));
            set => stormsOn = value == null ? "" : string.Join(",", AfterStormMath.Kinds.Where(value.Contains));
        }

        /// <summary>One more of a kind this year, for the annals.</summary>
        internal void CountDisaster(int year, string kind) => disasters = AfterStormMath.Count(disasters, year, kind);

        // ---------------- Last winter (the advisor remembers it) ----------------

        /// <summary>The last winter the record kept on this map (the last year's seasons, WeatherMath.WinterBefore), or null.</summary>
        public LastWinter LastWinter
        {
            get
            {
                int year = GenLocalDate.Year(map), quadrum = GenLocalDate.DayOfYear(map) / WeatherMath.QuarterDays;
                return WeatherMath.WinterBefore(quarterList, year, quadrum);
            }
        }

        /// <summary>Dev: a last winter made up for this map (four seasons of the year before, one a winter), so the advisor's notes can be tried at once.</summary>
        internal void DevLastWinter()
        {
            int year = GenLocalDate.Year(map), quadrum = GenLocalDate.DayOfYear(map) / WeatherMath.QuarterDays;
            int at = year * 4 + quadrum;
            quarterList.RemoveAll(q => q.year * 4 + q.quadrum >= at - 4 && q.year * 4 + q.quadrum < at);
            for (int i = 4; i >= 1; i--)
            {
                int n = at - i;
                bool winter = i == 2;
                var q = new WeatherQuarter
                {
                    year = n / 4, quadrum = n % 4, season = winter ? 4 : 2, days = 15, counted = true, coldest = winter ? -24f : 3f, hottest = winter ? 4f : 24f,
                    meanSum = (winter ? -9f : 12f) * 15, expectedSum = (winter ? -8f : 12f) * 15,
                    coldDays = winter ? 15 : i == 1 || i == 3 ? 4 : 0, frozenDays = winter ? 11 : 0, coverDays = winter ? 9 : 0
                };
                quarterList.Add(q);
            }
            quarterList.Sort((a, b) => (a.year * 4 + a.quadrum).CompareTo(b.year * 4 + b.quadrum));
            WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
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
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, (earlier > 0 ? "CC_Weather_SnowEarlier" : "CC_Weather_SnowLater").Translate(Math.Abs(earlier)).Resolve(), null, MessageTypeDefOf.NeutralEvent);
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
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, text, null, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A season ended: set against the year before, and named if it was far from usual here.</summary>
        private void EndQuarter(WeatherQuarter q, int tick)
        {
            if (q.days < 10)
            {
                quarterList.Add(q);
                WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
                ClimateYearEnded(q);
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
                Tell.Minor(NewsKind.Land, Importance.Notable, map, text, null, MessageTypeDefOf.NeutralEvent);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Weather_NamedChronicle".Translate(named.CapitalizeFirst()).Resolve()));
                // The believers say whose doing they take it for (Gods/Among/SeasonGods.cs).
                try
                {
                    SeasonGods.Named(map, q, named);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("weather: a named season and the gods", e);
                }
            }
            ClimateYearEnded(q);
            var change = WeatherMath.Compare(q, before, out float by);
            if (change == WeatherChange.Same) return;
            string line = Change(change, season, q, before, by);
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, line, null, MessageTypeDefOf.NeutralEvent);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, line));
            // Someone who has lived through both writes it down: the longest with the colony.
            var writer = ColonyCalendar.AtHome(map).Where(p => !p.DevelopmentalStage.Baby())
                .OrderByDescending(p => p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0).FirstOrDefault();
            if (writer != null) comp?.journals.WriteTagged(writer, "Council_Journal_SeasonCompare", new[] { change.ToString().ToLowerInvariant(), WeatherMath.SeasonWord(q.season) }, null, season.Named("SEASON"));
        }

        /// <summary>The year's last season ended: the year into the land's record (Weather/LandClimate.cs).</summary>
        private void ClimateYearEnded(WeatherQuarter q)
        {
            try
            {
                LandClimate.QuarterEnded(map, this, q, firstSnowYear == q.year ? firstSnowDoy : -1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land's record", e);
            }
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
            string quakes = DisastersLine(year);
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
            // The land's climate: the year's own extreme, its seasons' characters, once in a lifetime, and what the record shows.
            string climate = null;
            try
            {
                climate = LandClimate.AnnalsPart(map, this, year);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land's climate in the annals", e);
            }
            if (climate != null) line += " " + climate;
            return quakes != null ? line + " " + quakes : line;
        }

        /// <summary>The year's disasters for the annals: "The earth shook twice, and hail fell on the fields once." Null when none came.</summary>
        private string DisastersLine(int year)
        {
            var parts = AfterStormMath.Annals(disasters, year)
                .Select(x => (x.count == 1 ? "CC_Annals_After_" + x.kind + "_Once" : "CC_Annals_After_" + x.kind).Translate(x.count).Resolve()).ToList();
            return parts.Count == 0 ? null : "CC_Annals_After".Translate(TextUtil.JoinAnd(parts)).Resolve().CapitalizeFirst();
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
            try
            {
                lines.AddRange(LandClimate.OverviewLines(map, this));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land's climate on the Overview", e);
            }
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
            Scribe_Values.Look(ref stormsOn, "stormsOn", "");
            Scribe_Values.Look(ref afterWaiting, "afterWaiting", "");
            Scribe_Values.Look(ref disasters, "disasters", "");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // An older save's earthquake and tornado going on, and its year's quakes, carried over once.
                Scribe_Values.Look(ref quakeOn, "quakeOn");
                Scribe_Values.Look(ref tornadoOn, "tornadoOn");
                Scribe_Values.Look(ref quakeYear, "quakeYear", -1);
                Scribe_Values.Look(ref quakeCount, "quakeCount");
            }
            dyn.Expose();
            clim.Expose();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dayList = (days ?? new List<string>()).Select(WeatherMath.UnpackDay).Where(d => d != null).ToList();
                quarterList = (quarters ?? new List<string>()).Select(WeatherMath.UnpackQuarter).Where(q => q != null).ToList();
                now = today != null ? WeatherMath.UnpackDay(today) : null;
                cur = quarter != null ? WeatherMath.UnpackQuarter(quarter) : null;
                rec = (records != null ? WeatherMath.UnpackRecords(records) : null) ?? new WeatherRecords();
                WeatherMath.Trim(dayList, WeatherMath.MaxDays);
                WeatherMath.Trim(quarterList, WeatherMath.MaxQuarters);
                stormsOn ??= "";
                afterWaiting ??= "";
                disasters ??= "";
                if (quakeOn || tornadoOn)
                {
                    var on = StormsOn;
                    if (quakeOn) on.Add(AfterStormMath.Quake);
                    if (tornadoOn) on.Add(AfterStormMath.Tornado);
                    StormsOn = on;
                    quakeOn = tornadoOn = false;
                }
                if (quakeYear >= 0 && quakeCount > 0 && disasters.Length == 0)
                    for (int i = 0; i < quakeCount; i++)
                        disasters = AfterStormMath.Count(disasters, quakeYear, AfterStormMath.Quake);
                quakeYear = -1;
                quakeCount = 0;
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
