using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a weather is, for how one follows another (from its rain, snow, storm, fog, wind and dust).</summary>
    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Windy,
        Fog,
        Rain,
        /// <summary>Rain so heavy it floods (Odyssey's torrential rain): never made likelier.</summary>
        Downpour,
        Snow,
        /// <summary>A storm with rain or snow in it.</summary>
        Storm,
        /// <summary>A storm with nothing falling: dry lightning, a duststorm.</summary>
        DryStorm,
        Dust,
        /// <summary>Weather some event or other mod brings for its own reasons (a pall, blood rain, acid fog, a double rainbow): left alone.</summary>
        Special
    }

    /// <summary>What a weather does, read from its def by the game side (Weather/WeatherDynamics.cs).</summary>
    public struct WeatherTraits
    {
        public float rainRate, snowRate;
        public bool storm, fog, windy, cloudy, dust, special;
    }

    /// <summary>
    /// The land, for its weather: the tile's rain a year, the coldest and warmest months' means, how much of the biome's
    /// weather is rain at all, whether Seasonal Weather already gives it seasons, and whether there's much open water.
    /// </summary>
    public struct BiomeWeather
    {
        public float rainfall, coldest, warmest, wetShare;
        public bool seasonsShaped, water;

        public bool Arid => rainfall < 400f || wetShare < 0.08f;
        public bool Humid => rainfall > 1800f && !Arid;
        public bool Frozen => warmest < 0f;
        public bool Tropical => coldest >= 18f;
        /// <summary>Hot summers and cold winters: weather fronts are strong here.</summary>
        public bool Continental => warmest - coldest >= 30f;
    }

    /// <summary>The weather as it stands, when the next one is chosen.</summary>
    public struct WeatherNow
    {
        public WeatherKind current;
        /// <summary>Days running of rain, of nothing falling, of snow (today counted once it qualifies).</summary>
        public int wetRun, dryRun, snowRun;
        public int hour, quadrum;
        /// <summary>The game's season: 1 spring, 2 summer, 3 fall, 4 winter, 5 permanent summer, 6 permanent winter.</summary>
        public int season;
        public float temp;
        /// <summary>How much colder today is than yesterday (°C; a cold front when large).</summary>
        public float tempFall;
        /// <summary>A drought of any kind is on (another mod's, the game's, or a regional one): nothing makes rain likelier.</summary>
        public bool drought;
        /// <summary>This year's lean, wetter (above 0) or drier (WeatherDynamicsMath.YearOf).</summary>
        public float yearWet;
        /// <summary>What events and conditions ask of the weather now: factors on the rain, the clear skies, storms, snow and fog (1 = nothing).</summary>
        public float eventRain, eventClear, eventStorm, eventSnow, eventFog;
        /// <summary>The tile's own number, for when its wet season falls.</summary>
        public int seed;

        public static WeatherNow Calm => new WeatherNow { current = WeatherKind.Clear, hour = 12, season = 2, temp = 15f, eventRain = 1f, eventClear = 1f, eventStorm = 1f, eventSnow = 1f, eventFog = 1f };
    }

    /// <summary>
    /// Weather that changes like weather (Weather/WeatherDynamics.cs): the game still chooses the next weather when one
    /// ends, from the biome's own weathers and weights (and Seasonal Weather's by season); this only weighs them a
    /// little differently, as the land and the last days make likely. Spells hold and then break (a long dry spell with
    /// a storm), one weather follows another as it would (no storm out of a clear sky, fog after a cold clear night or
    /// rain, snow behind a cold front), events and conditions move the weather (the long rains bring rain, a heat wave
    /// clear skies, a drought never more rain), each year leans a little wetter or drier, and a tropical land without
    /// Seasonal Weather has a wet and a dry season. The weather changes the temperature a few degrees (cooler under rain
    /// on a hot day, colder on a clear night, milder under cloud), a morning fog burns off by midday, and a forecast is
    /// as good as the one who makes it. Every factor stays within <see cref="MinFactor"/> to <see cref="MaxFactor"/>,
    /// leaves the special weathers alone and never makes a flooding downpour likelier. No game types, so Tools/Tests
    /// checks it, over every kind of weather and land.
    /// </summary>
    public static class WeatherDynamicsMath
    {
        public const float MinFactor = 0.3f, MaxFactor = 2.5f;

        /// <summary>The most the weather moves the temperature, down and up (°C).</summary>
        public const float MaxCooling = 5f, MaxWarming = 3f;

        public const int TicksPerHour = 2500;

        public static WeatherKind KindOf(WeatherTraits t)
        {
            if (t.special) return WeatherKind.Special;
            if (t.storm) return t.rainRate > 0.1f || t.snowRate > 0.05f ? WeatherKind.Storm : WeatherKind.DryStorm;
            if (t.snowRate > 0.05f) return WeatherKind.Snow;
            if (t.rainRate >= 1.5f) return WeatherKind.Downpour;
            if (t.rainRate > 0.1f) return WeatherKind.Rain;
            if (t.fog) return WeatherKind.Fog;
            if (t.dust) return WeatherKind.Dust;
            if (t.windy) return WeatherKind.Windy;
            if (t.cloudy) return WeatherKind.Cloudy;
            return WeatherKind.Clear;
        }

        /// <summary>Rain falls in it (rain, a downpour, a storm with rain).</summary>
        public static bool Wet(WeatherKind k) => k == WeatherKind.Rain || k == WeatherKind.Downpour || k == WeatherKind.Storm;

        /// <summary>Open sky: clear, cloudy or windy.</summary>
        public static bool Open(WeatherKind k) => k == WeatherKind.Clear || k == WeatherKind.Cloudy || k == WeatherKind.Windy;

        /// <summary>Days of rain running after which a wet spell tends to break, and days of nothing falling after which a dry spell breaks with a storm.</summary>
        public static int WetSpellEnds(BiomeWeather b) => b.Arid ? 3 : b.Humid ? 9 : 6;

        public static int DrySpellBreaks(BiomeWeather b) => b.Arid ? 12 : b.Humid ? 5 : 8;

        /// <summary>
        /// The factor on a candidate weather's weight, for the weather as it stands on this land: the product of the
        /// spells, what follows on, events and conditions, the year and the season, within the bounds.
        /// </summary>
        public static float Factor(WeatherKind c, WeatherNow n, BiomeWeather b)
        {
            if (c == WeatherKind.Special) return 1f;
            float f = Spell(c, n, b) * FollowOn(c, n, b) * Events(c, n) * Year(c, n, b) * Season(c, n, b);
            // A drought means no more rain than the land would have anyway, and a downpour is never made likelier.
            if ((n.drought && Wet(c)) || c == WeatherKind.Downpour) f = Math.Min(f, 1f);
            return Clamp(f, MinFactor, MaxFactor);
        }

        /// <summary>
        /// The factors for every weather the game could choose now (kinds and the game's own weights), so that together
        /// the ordinary weathers keep the chance they had: the dynamics only share it out differently among them, and
        /// the special weathers (and any weather the land's list doesn't have, which another mod adds) keep exactly theirs.
        /// </summary>
        public static void Shared(WeatherKind[] kinds, float[] weights, WeatherNow n, BiomeWeather b, float[] factors)
        {
            float before = 0f, after = 0f;
            for (int i = 0; i < kinds.Length; i++)
            {
                factors[i] = Factor(kinds[i], n, b);
                if (kinds[i] == WeatherKind.Special || weights[i] <= 0f) continue;
                before += weights[i];
                after += weights[i] * factors[i];
            }
            float norm = after > 0f && before > 0f ? before / after : 1f;
            for (int i = 0; i < kinds.Length; i++)
                if (kinds[i] != WeatherKind.Special)
                {
                    factors[i] = Clamp(factors[i] * norm, MinFactor, MaxFactor);
                    if ((n.drought && Wet(kinds[i])) || kinds[i] == WeatherKind.Downpour) factors[i] = Math.Min(factors[i], 1f);
                }
        }

        /// <summary>Spells that hold, and break: rain after rain, dry after dry, snow after snow; a long dry spell breaks with a storm.</summary>
        public static float Spell(WeatherKind c, WeatherNow n, BiomeWeather b)
        {
            float f = 1f;
            if (n.wetRun >= 1)
            {
                float p = Math.Min(n.wetRun, 4) * 0.2f * (b.Arid ? 0.8f : 1f);
                bool ending = n.wetRun >= WetSpellEnds(b);
                if (Wet(c)) f *= ending ? 0.6f : 1f + p;
                else if (Open(c)) f *= ending ? 1.3f : 1f - p * 0.5f;
            }
            int holds = b.Arid ? 3 : 2;
            if (n.dryRun >= holds)
            {
                float d = Math.Min(n.dryRun - holds + 1, 6) * (b.Humid ? 0.12f : 0.08f);
                // When it breaks, it breaks with rain, and in the warm with a storm; in a dry land, where long dry
                // spells are the usual, only now and then with a storm.
                bool breaks = n.dryRun >= DrySpellBreaks(b) && !n.drought;
                if (c == WeatherKind.Storm) f *= breaks && n.temp >= 10f ? (b.Arid ? 1.3f : 1.8f) : 1f - d;
                else if (Wet(c)) f *= breaks ? (b.Arid ? 1f : 1.2f) : 1f - d;
                else if (Open(c)) f *= breaks ? 1f : 1f + d * 0.5f;
            }
            if (n.snowRun >= 2)
            {
                if (c == WeatherKind.Snow) f *= n.snowRun >= 5 ? 0.8f : 1.2f;
                else if (n.snowRun >= 5 && c == WeatherKind.Clear) f *= 1.3f;
            }
            return f;
        }

        /// <summary>
        /// One weather following another as it would: clouds build into rain, rain settles or clears, a storm seldom comes
        /// out of a clear sky (except on a hot afternoon), fog comes at night or in the morning after a clear night or rain
        /// and not in the wind, wind blows dust about in a dry land, and a cold front brings snow, or a storm where it's warm.
        /// </summary>
        public static float FollowOn(WeatherKind c, WeatherNow n, BiomeWeather b)
        {
            bool morning = n.hour < 9, afternoon = n.hour >= 12 && n.hour < 20;
            bool hot = n.temp >= 24f;
            float f = 1f;
            switch (n.current)
            {
                case WeatherKind.Clear:
                    if (c == WeatherKind.Storm) f *= afternoon && hot ? 1.2f : n.tempFall >= 5f ? 1f : 0.5f;
                    else if (c == WeatherKind.Fog) f *= morning ? 1.4f : 0.6f;
                    else if (c == WeatherKind.Cloudy) f *= 1.2f;
                    break;
                case WeatherKind.Cloudy:
                    if (c == WeatherKind.Rain) f *= 1.3f;
                    else if (c == WeatherKind.Storm) f *= 1.1f;
                    break;
                case WeatherKind.Windy:
                    if (c == WeatherKind.Fog) f *= 0.3f;
                    else if (c == WeatherKind.Dust) f *= b.Arid ? 1.4f : 1f;
                    else if (c == WeatherKind.Clear) f *= 1.2f;
                    break;
                case WeatherKind.Fog:
                    if (c == WeatherKind.Clear) f *= morning ? 1f : 1.3f;
                    else if (c == WeatherKind.Rain) f *= 1.1f;
                    else if (c == WeatherKind.Storm) f *= 0.7f;
                    break;
                case WeatherKind.Rain:
                case WeatherKind.Downpour:
                    if (c == WeatherKind.Fog) f *= morning ? 1.4f : 1f;
                    else if (c == WeatherKind.Storm) f *= afternoon && n.temp >= 22f ? 1.3f : 1f;
                    else if (c == WeatherKind.Clear) f *= 1.1f;
                    break;
                case WeatherKind.Storm:
                    if (c == WeatherKind.Rain) f *= 1.4f;
                    else if (c == WeatherKind.Clear) f *= 1.2f;
                    else if (c == WeatherKind.Storm) f *= 0.6f;
                    else if (c == WeatherKind.Snow) f *= 1.3f;
                    break;
                case WeatherKind.DryStorm:
                    if (c == WeatherKind.Storm) f *= n.drought ? 1f : 1.3f;
                    else if (c == WeatherKind.DryStorm) f *= 0.7f;
                    break;
                case WeatherKind.Snow:
                    if (c == WeatherKind.Snow) f *= 1.2f;
                    else if (c == WeatherKind.Clear) f *= 1.1f;
                    break;
                case WeatherKind.Dust:
                    if (Open(c)) f *= 1.2f;
                    else if (c == WeatherKind.Dust) f *= 0.8f;
                    break;
            }
            // Fog over water in the calm of the night or morning.
            if (c == WeatherKind.Fog && b.water && morning) f *= 1.2f;
            // A cold front: snow behind it, or where it's still warm a storm along it; then clearer, colder air.
            if (n.tempFall >= 5f)
            {
                float front = b.Continental ? 1.6f : 1.4f;
                if (c == WeatherKind.Snow) f *= front;
                else if (c == WeatherKind.Storm) f *= n.temp > 5f ? front * 0.85f : 1f;
                else if (c == WeatherKind.Fog) f *= 0.7f;
                else if (c == WeatherKind.Clear) f *= 1.15f;
            }
            return f;
        }

        /// <summary>What events and conditions ask of the weather now (the long rains, a heat wave, a hard winter...).</summary>
        public static float Events(WeatherKind c, WeatherNow n)
        {
            switch (c)
            {
                case WeatherKind.Rain:
                case WeatherKind.Downpour: return Clamp(n.eventRain, 0.3f, 3f);
                case WeatherKind.Storm: return Clamp(n.eventRain * n.eventStorm, 0.3f, 3f);
                case WeatherKind.DryStorm: return Clamp(n.eventStorm, 0.3f, 3f);
                case WeatherKind.Snow: return Clamp(n.eventSnow, 0.3f, 3f);
                case WeatherKind.Fog: return Clamp(n.eventFog, 0.3f, 3f);
                case WeatherKind.Clear:
                case WeatherKind.Cloudy:
                case WeatherKind.Windy: return Clamp(n.eventClear, 0.3f, 3f);
                default: return 1f;
            }
        }

        /// <summary>This year's lean: a wetter year has more rain and fewer clear days, a drier one the other way.</summary>
        public static float Year(WeatherKind c, WeatherNow n, BiomeWeather b)
        {
            if (Wet(c)) return 1f + n.yearWet;
            if (Open(c)) return 1f - n.yearWet * 0.5f;
            if (c == WeatherKind.Snow) return 1f + n.yearWet * 0.5f;
            return 1f;
        }

        /// <summary>
        /// Seasons where Seasonal Weather doesn't already give them: a tropical land has a wet season (two quadrums
        /// running, which ones by the tile) and a dry one; a land with seasons has summer storms, few in winter, and a
        /// little more rain in spring and fall. A frozen land, and one Seasonal Weather shapes, are left as they are.
        /// </summary>
        public static float Season(WeatherKind c, WeatherNow n, BiomeWeather b)
        {
            if (b.seasonsShaped || b.Frozen) return 1f;
            if (b.Tropical)
            {
                if (!Wet(c) && !Open(c)) return 1f;
                bool wetSeason = WetQuadrum(n.quadrum, n.seed);
                if (Wet(c)) return wetSeason ? 1.6f : 0.5f;
                return wetSeason ? 0.75f : 1.25f;
            }
            // Over the year about as much rain as before: storms in summer and seldom in winter, a little more rain in
            // spring and fall and a little less in summer.
            if (c == WeatherKind.Storm) return n.season == 2 ? 1.3f : n.season == 3 ? 0.9f : n.season == 4 ? 0.6f : 1f;
            if (Wet(c)) return n.season == 1 || n.season == 3 ? 1.1f : n.season == 2 ? 0.9f : 1f;
            return 1f;
        }

        /// <summary>Whether a quadrum (0 to 3) falls in a tropical land's wet season: two running, starting at one the tile picks.</summary>
        public static bool WetQuadrum(int quadrum, int seed)
        {
            int start = ((seed % 4) + 4) % 4;
            int q = ((quadrum - start) % 4 + 4) % 4;
            return q < 2;
        }

        /// <summary>
        /// How long a fog that starts now lasts, in ticks: a fog in the night or morning burns off by late morning (by
        /// eleven, at least three hours), one by day passes in a few hours. Null when the game's own length stands
        /// (a land that's always foggy, or a fog in the rain).
        /// </summary>
        public static int? FogTicks(int hour, int gameTicks, BiomeWeather b)
        {
            if (b.Frozen) return null;
            if (hour < 9 || hour >= 20)
            {
                int untilEleven = ((11 - hour) % 24 + 24) % 24;
                return Math.Min(gameTicks, Math.Max(3, untilEleven) * TicksPerHour);
            }
            return Math.Min(gameTicks, 4 * TicksPerHour);
        }

        /// <summary>
        /// How much the weather moves the temperature now (°C), before it's eased in: cooler under rain on a warm day
        /// (more in a storm), colder on a clear night (much colder in a dry land, less in a humid one), milder under cloud,
        /// fog, rain or snow at night, a little warmer in clear sunshine (more in a dry land), colder over snow cover on
        /// land that isn't always frozen, and this year's own lean.
        /// </summary>
        public static float TempTarget(WeatherKind k, float rainRate, bool night, float sun, BiomeWeather b, float snowCover, float temp, float yearTemp)
        {
            float t = yearTemp;
            if (k == WeatherKind.Special) return Clamp(t, -MaxCooling, MaxWarming);
            if (!night && temp >= 12f && Wet(k))
                t -= (1f + 2f * Math.Min(Math.Max(rainRate, 0f), 1f)) * (b.Humid ? 0.8f : 1f);
            if (!night && temp >= 15f && k == WeatherKind.Storm) t -= 1f;
            if (night && (k == WeatherKind.Clear || k == WeatherKind.Windy))
                t -= 1.5f * (b.Arid ? 2f : b.Humid ? 0.6f : 1f) * (b.Frozen || snowCover >= 0.25f ? 1.3f : 1f);
            if (night && (k == WeatherKind.Cloudy || k == WeatherKind.Fog || Wet(k) || k == WeatherKind.Snow))
                t += b.Humid ? 0.5f : 1f;
            if (!night && sun >= 0.7f && (k == WeatherKind.Clear || k == WeatherKind.Windy)) t += b.Arid ? 1.5f : 0.5f;
            if (!b.Frozen && snowCover >= 0.25f) t -= night ? 1f : 0.5f;
            return Clamp(t, -MaxCooling, MaxWarming);
        }

        /// <summary>The temperature's way toward its target, a tenth of the way each look (every 250 ticks): it settles in about two hours.</summary>
        public static float Ease(float current, float target) => current + (target - current) * 0.1f;

        /// <summary>
        /// How often a forecast comes true: from a little better than chance for someone who knows the fields a bit, to
        /// most times for an old hand at them; a weather station makes it nearly certain.
        /// </summary>
        public static float ForecastAccuracy(int plants, float age, bool station)
        {
            if (station) return 0.95f;
            float a = 0.55f + Math.Min(plants, 12) * 0.025f + (age >= 50f ? 0.05f : 0f);
            return Clamp(a, 0.5f, 0.9f);
        }

        /// <summary>Someone who can read the weather: a fair hand at the fields, or older with some of it, or anyone with a weather station.</summary>
        public static bool ReadsWeather(int plants, float age, bool station) => station || plants >= 6 || (age >= 50f && plants >= 3);

        /// <summary>
        /// A year's character on a tile: how much wetter or drier (a dry land varies more from year to year) and how
        /// much warmer or colder (°C) it leans. The same tile and year always give the same.
        /// </summary>
        public static (float wet, float temp) YearOf(int seed, int year, BiomeWeather b)
        {
            uint h = Hash((uint)seed * 2654435761u ^ (uint)year * 40503u);
            float u1 = (h & 0xFFFF) / 65535f * 2f - 1f;
            float u2 = ((h >> 16) & 0xFFFF) / 65535f * 2f - 1f;
            float wet = u1 * 0.22f * (b.Arid ? 1.5f : 1f);
            return (wet, u2 * 1.2f);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        /// <summary>
        /// What a year was, measured: "wet" or "dry" against the years before it (a quarter more or fewer wet days, and
        /// at least five), "warm" or "cold" against the land's usual (a degree or more). Null for an ordinary year.
        /// </summary>
        public static (string wet, string temp) YearWords(int wetDays, float earlierWetDays, int earlierYears, float anomaly)
        {
            string w = null, t = null;
            if (earlierYears > 0)
            {
                if (wetDays >= earlierWetDays * 1.25f && wetDays - earlierWetDays >= 5f) w = "wet";
                else if (wetDays <= earlierWetDays * 0.75f && earlierWetDays - wetDays >= 5f) w = "dry";
            }
            if (anomaly >= 1f) t = "warm";
            else if (anomaly <= -1f) t = "cold";
            return (w, t);
        }

        /// <summary>
        /// The year's words for its seasons as the record kept them (WeatherMath's quadrums, three years at most): only
        /// a year with three seasons or more counted, against the earlier years kept that had as many, per season.
        /// </summary>
        public static (string wet, string temp) YearWordsFor(IList<WeatherQuarter> kept, int year)
        {
            var mine = kept.Where(q => q.year == year && q.days >= 10).ToList();
            if (mine.Count < 3) return (null, null);
            int wetDays = mine.Sum(q => q.wetDays);
            float anomaly = mine.Average(q => q.Anomaly);
            var earlier = kept.Where(q => q.year < year && q.days >= 10).GroupBy(q => q.year).Where(g => g.Count() >= 3).ToList();
            // Earlier years' wet days, as many seasons' worth as this year has.
            float before = earlier.Count > 0 ? earlier.Average(g => g.Sum(q => q.wetDays) / (float)g.Count()) * mine.Count : 0f;
            return YearWords(wetDays, before, earlier.Count, anomaly);
        }

        // ---------------- Events' pulls on the weather ----------------

        /// <summary>The most pulls on the weather kept at once (the oldest go first).</summary>
        public const int MaxPulls = 6;

        /// <summary>
        /// An event's pull on the weather as its def gives it, "kind:factor:days" ("rain:1.8:5"): rain, storm, clear,
        /// snow or fog, made likelier (or less likely, below 1) for that many days. False when it isn't one.
        /// </summary>
        public static bool TryParsePull(string s, out string kind, out float factor, out float days)
        {
            kind = null;
            factor = 1f;
            days = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Split(':');
            if (parts.Length != 3) return false;
            kind = parts[0].Trim().ToLowerInvariant();
            if (kind != "rain" && kind != "storm" && kind != "clear" && kind != "snow" && kind != "fog") return false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out factor) || factor <= 0f || factor > 5f) return false;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out days) || days <= 0f || days > 30f) return false;
            return true;
        }

        /// <summary>A pull added to what events and conditions ask of the weather now.</summary>
        public static void AddPull(ref WeatherNow n, string kind, float factor)
        {
            switch (kind)
            {
                case "rain": n.eventRain *= factor; break;
                case "storm": n.eventStorm *= factor; break;
                case "clear": n.eventClear *= factor; break;
                case "snow": n.eventSnow *= factor; break;
                case "fog": n.eventFog *= factor; break;
            }
        }

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
