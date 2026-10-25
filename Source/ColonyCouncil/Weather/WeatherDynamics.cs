using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Weather that changes like weather, in the game (the arithmetic is in WeatherDynamicsMath, checked over every land
    /// and combination in Tools/Tests). The game still chooses each next weather when one ends, from the biome's own
    /// weathers and weights (Seasonal Weather's by season, other mods' too); this only weighs them as the land and the
    /// last days make likely (<see cref="Patch_WeatherChoice"/>): spells that hold and break, one weather following
    /// another, Rimfolk's events and the conditions on the map, the year's lean, and seasons where nothing else gives the
    /// land any. Together the ordinary weathers keep the chance they had, so the special ones (a double rainbow, blood
    /// rain, acid smog) and anything an event forces are left as they are.
    ///
    /// The weather also moves the outdoor temperature a few degrees (<see cref="Patch_WeatherTemperature"/>, eased in
    /// every 250 ticks), a morning fog burns off by late morning (<see cref="Patch_WeatherStarted"/>), and someone who
    /// reads the weather says what's coming a few hours before it does; what comes is then held to unless it can no
    /// longer come (<see cref="Patch_WeatherForecastHeld"/>), and the forecast is right as often as they're good at it.
    ///
    /// Only on home maps with weather of their own (not a pocket map or open space). Nothing new is put on the map: the
    /// state is in the map's <see cref="WeatherBook"/>, and only the forecast and the events' pulls are saved.
    /// </summary>
    public static class WeatherDynamics
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool ChoiceOn => RimfolkPatches.On && (S?.dynamicWeather ?? true);
        public static bool TemperatureOn => RimfolkPatches.On && (S?.weatherTemperature ?? true);
        public static bool ForecastOn => RimfolkPatches.On && (S?.weatherForecast ?? true);

        /// <summary>Weather stations from other mods: with one, a forecast is nearly always right (Stormproof, Weather Control).</summary>
        private static readonly string[] Stations = { "Stormproof_WeatherForecaster", "NCWC_WeatherStation", "NCWC_WindSock" };

        /// <summary>A forecast is made this many hours before the weather is due to change, at most and at least.</summary>
        private const int ForecastFromHours = 8, ForecastToHours = 2;

        private static readonly AccessTools.FieldRef<WeatherDecider, Map> MapOf = AccessTools.FieldRefAccess<WeatherDecider, Map>("map");
        private static readonly AccessTools.FieldRef<WeatherDecider, int> DurationOf = AccessTools.FieldRefAccess<WeatherDecider, int>("curWeatherDuration");
        private static readonly MethodInfo ChooseMethod = AccessTools.Method(typeof(WeatherDecider), "ChooseNextWeather");
        private static readonly MethodInfo CommonalityMethod = AccessTools.Method(typeof(WeatherDecider), "CurrentWeatherCommonality");

        /// <summary>What the weather dynamics keep for one home map (in its WeatherBook).</summary>
        public sealed class State
        {
            // Saved: the forecast, and the events' pulls on the weather ("rain:1.8:tick until").
            public List<string> pulls = new List<string>();
            /// <summary>The weather that comes next (held to), what was said would, who said it, the weather it was said in and when that began.</summary>
            public string pin, said, during;
            public Pawn by;
            public int forecastStart = -1;

            // Not saved: the land, the factors for the weather being chosen now, the temperature's lean, a day of hours.
            public BiomeWeather land;
            public int landDay = -1;
            public int factorsTick = -1;
            public readonly Dictionary<WeatherDef, float> factors = new Dictionary<WeatherDef, float>();
            public float offset;
            public readonly float[] hourTemps = new float[24];
            public int hoursSeen;
            /// <summary>Who got the last forecast wrong, to own up to it if in view.</summary>
            public Pawn missedBy;

            public void Expose()
            {
                Scribe_Collections.Look(ref pulls, "weatherPulls", LookMode.Value);
                Scribe_Values.Look(ref pin, "forecastPin");
                Scribe_Values.Look(ref said, "forecastSaid");
                Scribe_Values.Look(ref during, "forecastDuring");
                Scribe_References.Look(ref by, "forecastBy");
                Scribe_Values.Look(ref forecastStart, "forecastStart", -1);
                if (Scribe.mode == LoadSaveMode.PostLoadInit) pulls = pulls ?? new List<string>();
            }
        }

        /// <summary>The maps it works on: home maps with weather of their own.</summary>
        public static bool Applies(Map map) =>
            map != null && !map.Disposed && map.IsPlayerHome && !map.IsPocketMap && !(map.Biome?.inVacuum ?? false)
            && map.weatherManager != null && map.weatherDecider != null && map.TileInfo != null;

        // ---------------- What a weather is ----------------

        private static readonly Dictionary<WeatherDef, WeatherKind> kinds = new Dictionary<WeatherDef, WeatherKind>();

        public static WeatherKind KindOf(WeatherDef w)
        {
            if (w == null) return WeatherKind.Clear;
            if (kinds.TryGetValue(w, out var k)) return k;
            k = WeatherDynamicsMath.KindOf(TraitsOf(w));
            kinds[w] = k;
            return k;
        }

        /// <summary>
        /// A weather's traits from its def: lightning makes a storm, a fog overlay fog, sand dust; a double rainbow (the
        /// only very good weather), one that poisons, one a monolith brings or one with a mood of its own is special.
        /// </summary>
        private static WeatherTraits TraitsOf(WeatherDef w)
        {
            string n = w.defName.ToLowerInvariant();
            bool lightning = w.eventMakers?.Any(e => e?.eventClass != null && e.eventClass.Name.IndexOf("Lightning", StringComparison.OrdinalIgnoreCase) >= 0) ?? false;
            bool fogOverlay = w.overlayClasses?.Any(t => t != null && t.Name.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0) ?? false;
            return new WeatherTraits
            {
                rainRate = w.rainRate,
                snowRate = w.snowRate,
                storm = lightning || Flavor.IsStorm(w),
                fog = fogOverlay || n.Contains("fog"),
                windy = n.Contains("wind") || (w.windSpeedFactor >= 2.5f && w.rainRate < 0.1f && w.snowRate < 0.05f),
                cloudy = n.Contains("cloud") || n.Contains("overcast"),
                dust = w.sandRate > 0f || n.Contains("dust") || n.Contains("sand"),
                special = w.favorability == Favorability.VeryGood || w.doToxicBuildup || w.minMonolithLevel > 0 || w.weatherThought != null
            };
        }

        // ---------------- The land ----------------

        /// <summary>The land as its weather goes, worked out once a day: the tile's rain and months, the biome's weathers, open water.</summary>
        private static BiomeWeather Land(Map map, State st)
        {
            int day = GenDate.DaysPassed;
            if (st.landDay == day) return st.land;
            st.landDay = day;
            var b = new BiomeWeather { rainfall = map.TileInfo.rainfall };
            float lo = 999f, hi = -999f;
            for (int t = 0; t < 12; t++)
            {
                float v = SeasonUtil.TwelfthTemp(map, (Twelfth)t);
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            b.coldest = lo;
            b.warmest = hi;
            // Seasonal Weather swaps the biome's weathers by season: its four lists together are the land's weather.
            var ext = map.Biome.modExtensions?.FirstOrDefault(e => e != null && e.GetType().FullName == "SeasonalWeather.SeasonalWeatherExtension");
            b.seasonsShaped = ext != null;
            var lists = new List<List<WeatherCommonalityRecord>>();
            if (ext != null)
                foreach (string f in new[] { "spring", "summer", "fall", "winter" })
                    if (AccessTools.Field(ext.GetType(), f)?.GetValue(ext) is List<WeatherCommonalityRecord> l) lists.Add(l);
            if (lists.Count == 0 && map.Biome.baseWeatherCommonalities != null) lists.Add(map.Biome.baseWeatherCommonalities);
            float wet = 0f, all = 0f;
            foreach (var list in lists)
                foreach (var r in list)
                {
                    if (r?.weather == null) continue;
                    all += r.commonality;
                    if (WeatherDynamicsMath.Wet(KindOf(r.weather))) wet += r.commonality;
                }
            b.wetShare = all > 0f ? wet / all : 0f;
            // Open water: a look at every fourth cell each way.
            int water = 0, seen = 0;
            for (int x = 0; x < map.Size.x; x += 4)
                for (int z = 0; z < map.Size.z; z += 4)
                {
                    seen++;
                    if (map.terrainGrid.TerrainAt(new IntVec3(x, 0, z))?.IsWater ?? false) water++;
                }
            b.water = seen > 0 && water >= seen * 0.08f;
            st.land = b;
            return b;
        }

        private static int Seed(Map map) => map.Tile.tileId;

        // ---------------- The weather as it stands ----------------

        private static WeatherNow Now(Map map, WeatherBook book, BiomeWeather b)
        {
            var st = book.dyn;
            var n = WeatherNow.Calm;
            n.current = KindOf(map.weatherManager.curWeather);
            WeatherMath.RunsWithToday(book.DaysBefore, book.Today, out n.wetRun, out n.dryRun, out n.snowRun);
            n.hour = GenLocalDate.HourOfDay(map);
            n.quadrum = GenLocalDate.DayOfYear(map) / WeatherMath.QuarterDays;
            n.season = (int)GenLocalDate.Season(map);
            n.temp = map.mapTemperature.OutdoorTemp - st.offset;
            n.tempFall = TempFall(st, n.temp);
            n.seed = Seed(map);
            n.yearWet = WeatherDynamicsMath.YearOf(n.seed, GenLocalDate.Year(map), b).wet;
            Conditions(map, ref n);
            int now = Find.TickManager.TicksGame;
            foreach (var p in st.pulls)
                if (Pull(p, out string kind, out float factor, out int until) && until > now) WeatherDynamicsMath.AddPull(ref n, kind, factor);
            return n;
        }

        /// <summary>
        /// How much colder it is than about this time yesterday (a cold front), once a day of hours has been seen: the
        /// next hour's slot, written 23 hours ago (this hour's may already hold today's).
        /// </summary>
        private static float TempFall(State st, float temp)
        {
            if (st.hoursSeen < 24) return 0f;
            float then = st.hourTemps[(Find.TickManager.TicksGame / GenDate.TicksPerHour + 1) % 24];
            return Mathf.Max(0f, then - temp);
        }

        /// <summary>
        /// What the conditions on the map ask of the weather: a heat wave or Stormproof's heat dome clear skies and less
        /// rain, a cold snap or its polar front clear skies, Rimfolk's warm spell the same; any drought (the game's,
        /// Vanilla Events Expanded's, or the region's) less rain and never more; the region's hard winter more snow, its
        /// floods more rain.
        /// </summary>
        private static void Conditions(Map map, ref WeatherNow n)
        {
            foreach (var c in map.gameConditionManager.ActiveConditions) Fold(c.def, ref n);
            var world = Find.World?.gameConditionManager?.ActiveConditions;
            if (world != null)
                foreach (var c in world)
                    if (c.CanApplyOnMap(map)) Fold(c.def, ref n);
            var ws = WorldSituations.Instance;
            if (ws == null) return;
            foreach (var s in ws.Running)
                switch (s.def?.defName)
                {
                    case "Council_WS_Drought":
                        n.drought = true;
                        n.eventRain *= 0.6f;
                        break;
                    case "Council_WS_HardWinter":
                        n.eventSnow *= 1.3f;
                        break;
                    case "Council_WS_Flood":
                        n.eventRain *= 1.3f;
                        break;
                }
        }

        private static void Fold(GameConditionDef d, ref WeatherNow n)
        {
            if (d == null) return;
            if (d == GameConditionDefOf.HeatWave || WeatherMods.HeatLike(d))
            {
                n.eventClear *= 1.3f;
                n.eventRain *= 0.8f;
            }
            else if (d == GameConditionDefOf.ColdSnap || WeatherMods.ColdLike(d)) n.eventClear *= 1.2f;
            else if (d.defName == "Council_WarmSpell")
            {
                n.eventClear *= 1.3f;
                n.eventRain *= 0.9f;
            }
            if (d.defName.IndexOf("Drought", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                n.drought = true;
                n.eventRain *= 0.7f;
            }
        }

        // ---------------- The next weather ----------------

        /// <summary>The factor on a weather's weight as the game chooses the next one on this map (1 off home maps).</summary>
        public static float FactorFor(WeatherDecider wd, WeatherDef w) => FactorFor(MapOf(wd), w);

        public static float FactorFor(Map map, WeatherDef w)
        {
            if (w == null || !Applies(map)) return 1f;
            var book = WeatherBook.Of(map);
            if (book == null) return 1f;
            var st = book.dyn;
            int tick = Find.TickManager.TicksGame;
            if (st.factorsTick != tick) Share(map, book, tick);
            return st.factors.TryGetValue(w, out float f) ? f : 1f;
        }

        /// <summary>The factors for every weather the biome has, worked out once for the choice being made.</summary>
        private static void Share(Map map, WeatherBook book, int tick)
        {
            var st = book.dyn;
            st.factorsTick = tick;
            st.factors.Clear();
            var list = map.Biome.baseWeatherCommonalities;
            if (list == null || list.Count == 0) return;
            var b = Land(map, st);
            var n = Now(map, book, b);
            var cur = map.weatherManager.curWeather;
            float temp = map.mapTemperature.OutdoorTemp;
            var ks = new WeatherKind[list.Count];
            var ws = new float[list.Count];
            var fs = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i]?.weather;
                if (w == null) continue;
                ks[i] = KindOf(w);
                bool could = w.temperatureRange.Includes(temp) && !(w == cur && !w.repeatable);
                ws[i] = could ? list[i].commonality * (w.commonalityRainfallFactor?.Evaluate(map.TileInfo.rainfall) ?? 1f) : 0f;
            }
            WeatherDynamicsMath.Shared(ks, ws, n, b, fs);
            for (int i = 0; i < list.Count; i++)
                if (list[i]?.weather != null) st.factors[list[i].weather] = fs[i];
        }

        // ---------------- The temperature ----------------

        /// <summary>The weather's push on this map's outdoor temperature now (°C).</summary>
        public static float OffsetOf(Map map)
        {
            if (!TemperatureOn) return 0f;
            return WeatherBook.Of(map)?.dyn.offset ?? 0f;
        }

        /// <summary>Every 250 ticks: the temperature eased toward what the weather makes it.</summary>
        public static void Rare(Map map, WeatherBook book)
        {
            var st = book.dyn;
            float target = 0f;
            if (TemperatureOn && Applies(map))
            {
                var b = Land(map, st);
                var wm = map.weatherManager;
                float glow = GenCelestial.CurCelestialSunGlow(map);
                float snow = map.snowGrid.TotalDepth / Mathf.Max(1, map.Area);
                float raw = map.mapTemperature.OutdoorTemp - st.offset;
                float year = WeatherDynamicsMath.YearOf(Seed(map), GenLocalDate.Year(map), b).temp;
                target = WeatherDynamicsMath.TempTarget(KindOf(wm.curWeather), wm.RainRate, glow < 0.3f, glow, b, snow, raw, year);
            }
            st.offset = WeatherDynamicsMath.Ease(st.offset, target);
            if (target == 0f && Mathf.Abs(st.offset) < 0.02f) st.offset = 0f;
        }

        // ---------------- Once an hour: the day's hours, the forecast ----------------

        public static void Hourly(Map map, WeatherBook book)
        {
            var st = book.dyn;
            int now = Find.TickManager.TicksGame;
            st.hourTemps[now / GenDate.TicksPerHour % 24] = map.mapTemperature.OutdoorTemp - st.offset;
            st.hoursSeen++;
            st.pulls.RemoveAll(p => !Pull(p, out _, out _, out int until) || until <= now);
            // Whoever got the last forecast wrong owns up to it now and then.
            if (st.missedBy != null)
            {
                var p = st.missedBy;
                st.missedBy = null;
                if (p.Spawned && p.Map == map && Rand.Chance(0.5f)) WeatherTalk.SayBy(p, "Council_Pool_SayForecastWrong");
            }
            if (ForecastOn && Applies(map)) Forecast(map, book, now);
        }

        private static void Forecast(Map map, WeatherBook book, int now)
        {
            var st = book.dyn;
            var wd = map.weatherDecider;
            var wm = map.weatherManager;
            if (st.pin != null || wm.curWeather == null || wd.ForcedWeather != null || TutorSystem.TutorialMode) return;
            int start = now - wm.curWeatherAge;
            if (st.forecastStart == start) return;
            int left = DurationOf(wd) - wm.curWeatherAge;
            if (left > ForecastFromHours * GenDate.TicksPerHour || left < ForecastToHours * GenDate.TicksPerHour) return;
            st.forecastStart = start;
            bool station = Station(map);
            Pawn by = null;
            int plants = 0;
            float age = 0f;
            foreach (var p in CalendarTracker.AtHome(map))
            {
                if (p.Downed || p.DevelopmentalStage.Baby() || p.skills == null) continue;
                var skill = p.skills.GetSkill(SkillDefOf.Plants);
                int level = skill == null || skill.TotallyDisabled ? 0 : skill.Level;
                float a = Flavor.HumanAge(p);
                if (!WeatherDynamicsMath.ReadsWeather(level, a, station)) continue;
                if (by == null || level > plants || (level == plants && a > age))
                {
                    by = p;
                    plants = level;
                    age = a;
                }
            }
            if (by == null || ChooseMethod == null) return;
            // What comes next is rolled now, by the game's own choice, and held to; the forecast is right or not.
            var next = ChooseMethod.Invoke(wd, null) as WeatherDef;
            if (next == null || KindOf(next) == WeatherKind.Special) return;
            var said = next;
            if (!Rand.Chance(WeatherDynamicsMath.ForecastAccuracy(plants, age, station)))
            {
                var other = map.Biome.baseWeatherCommonalities?
                    .Where(r => r?.weather != null && Group(KindOf(r.weather)) != Group(KindOf(next)) && KindOf(r.weather) != WeatherKind.Special && Commonality(wd, r.weather) > 0f)
                    .ToList();
                if (!other.NullOrEmpty()) said = other.RandomElementByWeight(r => r.commonality).weather;
            }
            st.pin = next.defName;
            st.said = Group(KindOf(said));
            st.during = wm.curWeather.defName;
            st.by = by;
            WeatherTalk.SayBy(by, PoolFor(st.said));
        }

        private static string PoolFor(string said)
        {
            switch (said)
            {
                case "Rain": return "Council_Pool_SayForecastRain";
                case "Storm": return "Council_Pool_SayForecastStorm";
                case "Snow": return "Council_Pool_SayForecastSnow";
                case "Fog": return "Council_Pool_SayForecastFog";
                case "Cloud": return "Council_Pool_SayForecastCloud";
                case "Wind": return "Council_Pool_SayForecastWind";
                case "Dust": return "Council_Pool_SayForecastDust";
                default: return "Council_Pool_SayForecastClear";
            }
        }

        private static float Commonality(WeatherDecider wd, WeatherDef w)
        {
            if (CommonalityMethod == null) return 0f;
            return CommonalityMethod.Invoke(wd, new object[] { w }) is float f ? f : 0f;
        }

        /// <summary>What a forecast says, by kind: Rain, Storm, Snow, Fog, Clear, Cloud, Wind or Dust.</summary>
        public static string Group(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Rain:
                case WeatherKind.Downpour: return "Rain";
                case WeatherKind.Storm:
                case WeatherKind.DryStorm: return "Storm";
                case WeatherKind.Snow: return "Snow";
                case WeatherKind.Fog: return "Fog";
                case WeatherKind.Cloudy: return "Cloud";
                case WeatherKind.Windy: return "Wind";
                case WeatherKind.Dust: return "Dust";
                default: return "Clear";
            }
        }

        private static bool Station(Map map)
        {
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (Array.IndexOf(Stations, b.def.defName) >= 0 && (b.TryGetComp<CompPowerTrader>()?.PowerOn ?? true)) return true;
            return false;
        }

        /// <summary>The weather that was foretold, as the game is about to choose the next one: null when there's none, or it can't come now.</summary>
        public static WeatherDef Held(WeatherDecider wd)
        {
            var map = MapOf(wd);
            var book = WeatherBook.Of(map);
            if (book == null || book.dyn.pin == null) return null;
            var st = book.dyn;
            string pin = st.pin;
            st.pin = null;
            if (!Applies(map) || wd.ForcedWeather != null || TutorSystem.TutorialMode || map.weatherManager.curWeather?.defName != st.during) return null;
            var w = DefDatabase<WeatherDef>.GetNamedSilentFail(pin);
            return w != null && Commonality(wd, w) > 0f ? w : null;
        }

        /// <summary>The next weather has begun: a forecast came true or not, and a fog in the night or morning lifts by late morning.</summary>
        public static void Started(WeatherDecider wd)
        {
            var map = MapOf(wd);
            var book = WeatherBook.Of(map);
            if (book == null) return;
            var st = book.dyn;
            var cur = map.weatherManager?.curWeather;
            if (st.said != null)
            {
                if (cur != null && Group(KindOf(cur)) != st.said && st.by != null) st.missedBy = st.by;
                st.said = null;
                st.by = null;
                st.pin = null;
            }
            if (!ChoiceOn || cur == null || !Applies(map) || wd.ForcedWeather != null || KindOf(cur) != WeatherKind.Fog) return;
            int? ticks = WeatherDynamicsMath.FogTicks(GenLocalDate.HourOfDay(map), DurationOf(wd), Land(map, st));
            if (ticks.HasValue) DurationOf(wd) = Mathf.Max(GenDate.TicksPerHour, ticks.Value);
        }

        /// <summary>The Overview's line on the forecast ("Hector expects rain."), or null.</summary>
        public static string ForecastLine(WeatherBook book)
        {
            var st = book.dyn;
            if (!ForecastOn || st.pin == null || st.said == null || st.by == null) return null;
            return "CC_Overview_WeatherForecast".Translate(st.by.LabelShort, ("CC_Forecast_" + st.said).Translate().Resolve()).Resolve();
        }

        // ---------------- Events ----------------

        /// <summary>An event's pulls on the weather ("rain:1.8:5", its def's weatherAfter), from now for their days.</summary>
        public static void PullFor(Map map, List<string> pulls)
        {
            if (pulls.NullOrEmpty()) return;
            var book = WeatherBook.Of(map);
            if (book == null) return;
            int now = Find.TickManager.TicksGame;
            foreach (string s in pulls)
            {
                if (!WeatherDynamicsMath.TryParsePull(s, out string kind, out float factor, out float days)) continue;
                book.dyn.pulls.Add(kind + ":" + factor.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ":" + (now + Mathf.RoundToInt(days * GenDate.TicksPerDay)));
            }
            while (book.dyn.pulls.Count > WeatherDynamicsMath.MaxPulls) book.dyn.pulls.RemoveAt(0);
        }

        /// <summary>A kept pull: its kind, factor and the tick it lasts until.</summary>
        private static bool Pull(string s, out string kind, out float factor, out int until)
        {
            kind = null;
            factor = 1f;
            until = 0;
            var parts = s?.Split(':');
            if (parts == null || parts.Length != 3) return false;
            kind = parts[0];
            return float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out factor)
                   && int.TryParse(parts[2], out until);
        }
    }

    /// <summary>The game weighs the weathers it could choose next (Weather/WeatherDynamics.cs).</summary>
    [HarmonyPatch(typeof(WeatherDecider), "CurrentWeatherCommonality")]
    public static class Patch_WeatherChoice
    {
        public static void Postfix(WeatherDecider __instance, WeatherDef weather, ref float __result)
        {
            if (__result <= 0f || !WeatherDynamics.ChoiceOn) return;
            try
            {
                __result *= WeatherDynamics.FactorFor(__instance, weather);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather choice", e);
            }
        }
    }

    /// <summary>The weather foretold comes, if it still can (Weather/WeatherDynamics.cs).</summary>
    [HarmonyPatch(typeof(WeatherDecider), "ChooseNextWeather")]
    public static class Patch_WeatherForecastHeld
    {
        public static bool Prefix(WeatherDecider __instance, ref WeatherDef __result)
        {
            if (!WeatherDynamics.ForecastOn) return true;
            try
            {
                var w = WeatherDynamics.Held(__instance);
                if (w == null) return true;
                __result = w;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather forecast", e);
                return true;
            }
        }
    }

    /// <summary>A new weather has begun (Weather/WeatherDynamics.cs).</summary>
    [HarmonyPatch(typeof(WeatherDecider), nameof(WeatherDecider.StartNextWeather))]
    public static class Patch_WeatherStarted
    {
        public static void Postfix(WeatherDecider __instance)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                WeatherDynamics.Started(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather started", e);
            }
        }
    }

    /// <summary>The weather's push on a map's outdoor temperature (Weather/WeatherDynamics.cs).</summary>
    [HarmonyPatch(typeof(GameConditionManager), "AggregateTemperatureOffset")]
    public static class Patch_WeatherTemperature
    {
        public static void Postfix(GameConditionManager __instance, ref float __result)
        {
            var map = __instance.ownerMap;
            if (map == null || !WeatherDynamics.TemperatureOn) return;
            try
            {
                __result += WeatherDynamics.OffsetOf(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather temperature", e);
            }
        }
    }
}
