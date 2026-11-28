using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>How strongly the land's climate shows (the setting "The land's climate"): off, gentle and realistic, as designed, or dramatic.</summary>
    public enum ClimateStrength
    {
        Off,
        Gentle,
        Usual,
        Dramatic
    }

    /// <summary>Which way the climate drifts over the years (the setting "Climate drift"): not at all, a way of its own, or a way the player chose.</summary>
    public enum ClimateDriftMode
    {
        Off,
        Random,
        Warmer,
        Colder,
        Drier,
        Wetter
    }

    /// <summary>A season's character: most are ordinary; a wet or dry one, a warm or cold one, and where snow falls a snowy one or one with little snow.</summary>
    public enum SeasonKind
    {
        Ordinary,
        Wet,
        Dry,
        Warm,
        Cold,
        Snowy,
        Snowless
    }

    /// <summary>A year's own extreme, now and then: a year without a summer, a drought year, a year of floods.</summary>
    public enum YearKind
    {
        Ordinary,
        NoSummer,
        Drought,
        Floods
    }

    /// <summary>
    /// Once in a lifetime: weather from outside the land's own climate, for a few days, with the temperature to match. Snow
    /// where it never snows, a hard frost on warm land, a great heat on cold land, a blizzard where blizzards don't come, a
    /// dust storm where there is no dust weather, heavy rain on dry land, a great storm off the water.
    /// </summary>
    public enum RareKind
    {
        None,
        Snow,
        Frost,
        Heat,
        Blizzard,
        Dust,
        Deluge,
        GreatStorm
    }

    /// <summary>
    /// The weathers no biome has (they come only by an event or a psycast), given a place by the seasons: fertile rain and a
    /// drizzle in a wet season, dust in a dry one, fog snow and a whiteout in a snowy winter, swelter in a hot summer, a petal
    /// storm or a double rainbow in spring, a fog storm in a wet fall, and red rain as a sign before a hard season.
    /// </summary>
    public enum SpecialKind
    {
        None,
        FertileRain,
        Drizzle,
        Dust,
        FogSnow,
        Whiteout,
        Swelter,
        Petals,
        Rainbow,
        FogStorm,
        RedRain
    }

    /// <summary>What an extreme is: a season far from the usual, a year's own extreme, or once in a lifetime.</summary>
    public enum ExtremeScope
    {
        Season,
        Year,
        Rare
    }

    /// <summary>What the record shows over the years (the land's record, E3).</summary>
    public enum TrendKind
    {
        WinterMilder,
        WinterHarsher,
        SummerHotter,
        SummerCooler,
        Wetter,
        Drier,
        GrowLonger,
        GrowShorter,
        SnowEarlier,
        SnowLater
    }

    /// <summary>
    /// The land, for its climate: its weather (BiomeWeather), each quadrum's usual mean temperature, which kinds of weather its
    /// own lists hold, and which of the weathers that can come from outside it the game has (by def name, from any mod).
    /// </summary>
    public struct ClimateLand
    {
        public BiomeWeather b;
        /// <summary>The usual mean temperature of each quadrum (0 to 3, the local calendar's), °C.</summary>
        public float q0, q1, q2, q3;
        /// <summary>
        /// The land's own weather brings snow, a blizzard, dust or sand, a flooding downpour: one is in its lists and its
        /// temperatures let it come (nearly every biome lists the snows; they never fall where the coldest month is 7 °C or more).
        /// </summary>
        public bool hasSnow, hasBlizzard, hasDust, hasDownpour;
        /// <summary>The game has these weathers at all (Odyssey's blizzard, sandstorm and torrential rain; Vanilla Psycasts Expanded's hurricane).</summary>
        public bool blizzardDef, sandDef, torrentDef, hurricaneDef;
        /// <summary>The weathers no biome has that are loaded here and not already in the land's lists, a bit per <see cref="SpecialKind"/>.</summary>
        public int specials;

        public float Quad(int q)
        {
            switch (((q % 4) + 4) % 4)
            {
                case 0: return q0;
                case 1: return q1;
                case 2: return q2;
                default: return q3;
            }
        }

        /// <summary>Where a quadrum stands among the four by warmth: 0 the coldest, 3 the warmest (ties by order).</summary>
        public int Rank(int q)
        {
            float m = Quad(q);
            int rank = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i == q) continue;
                float o = Quad(i);
                if (o < m || (o == m && i < q)) rank++;
            }
            return rank;
        }

        public bool Has(SpecialKind k) => k != SpecialKind.None && (specials & (1 << (int)k)) != 0;

        public static int Bit(SpecialKind k) => 1 << (int)k;
    }

    /// <summary>A season's character, drawn for the land, the year and the quadrum (ClimateMath.SeasonOf): the same every time it's asked.</summary>
    public struct SeasonCharacter
    {
        public int year, quadrum;
        /// <summary>The game's season (1 spring, 2 summer, 3 fall, 4 winter, 5 permanent summer, 6 permanent winter).</summary>
        public int season;
        /// <summary>The part it plays in the year (1 spring, 2 summer, 3 fall, 4 winter): the game's season, or by warmth where the season is permanent.</summary>
        public int role;
        /// <summary>Snow is what falls in it here (its usual mean at 3 °C or below): snowy or snowless, not wet or dry.</summary>
        public bool snowSeason;
        public SeasonKind kind;
        public bool extreme;
        /// <summary>How much warmer (or colder) it runs (°C), how much wetter (or drier) it leans, how much more (or less) snow.</summary>
        public float temp, wet, snow;
        /// <summary>The year's own extreme, when this season is part of it.</summary>
        public YearKind yearKind;
        /// <summary>Once in a lifetime in this season: what, from which hour of the season, for how long, and how far it moves the temperature (°C).</summary>
        public RareKind rare;
        public int rareStart, rareHours;
        public float rareTemp;
        /// <summary>A weather no biome has, given a place in this season: which, from which hour, for how long.</summary>
        public SpecialKind special;
        public int specialStart, specialHours;
        /// <summary>The special is a sign before a hard season, year or once in a lifetime (red rain); a few signs come before nothing.</summary>
        public bool sign;

        public bool Ordinary => kind == SeasonKind.Ordinary && !extreme && yearKind == YearKind.Ordinary;

        /// <summary>Once in a lifetime is going on at this hour of the season.</summary>
        public bool RareAt(int hour) => rare != RareKind.None && hour >= rareStart && hour < rareStart + rareHours;

        public bool SpecialAt(int hour) => special != SpecialKind.None && hour >= specialStart && hour < specialStart + specialHours;
    }

    /// <summary>A year's character: its lean (WeatherDynamicsMath.YearOf) and its own extreme, if it has one.</summary>
    public struct YearCharacter
    {
        public int year;
        public float wet, temp;
        public YearKind kind;
    }

    /// <summary>The numbers a strength setting stands for.</summary>
    public struct ClimateParams
    {
        /// <summary>The share of seasons with a character, and of those the share that are extreme.</summary>
        public float nonOrdinary, extremeShare;
        /// <summary>How far a season moves the temperature and the rain (1 as designed).</summary>
        public float magnitude;
        /// <summary>The chance a year is a year without a summer, a drought year or a year of floods (together), and a year has once in a lifetime (in a season it can come in).</summary>
        public float yearRate, rareRate;
        /// <summary>The most once in a lifetime takes the temperature down or up (°C).</summary>
        public float maxDrop, maxRise;
        /// <summary>The drift: °C a decade (low, high) and its cap; wetness a decade (low, high) and its cap.</summary>
        public float driftLo, driftHi, driftCap, wetLo, wetHi, wetCap;
        /// <summary>The factor on the weathers no biome has coming with the seasons.</summary>
        public float specialFactor;
    }

    /// <summary>
    /// The land's climate (Weather/LandClimate.cs), as pure rules: each season gets a character when it begins (most are
    /// ordinary, some wet, dry, warm, cold, snowy or snowless, a few extreme), drawn from the land, the year and the season by a
    /// hash, so it needs nothing saved and is the same every time it's asked; now and then a year has its own extreme (a year
    /// without a summer, a drought year, a year of floods); very rarely a season brings weather from outside the land's own
    /// climate, with the temperature to match; the weathers no biome has come with the seasons and as signs; and the climate
    /// drifts slowly over the years. What the record shows over the years is measured here too. No game types, so
    /// Tools/Tests checks it over many lands and a hundred years.
    /// </summary>
    public static class ClimateMath
    {
        /// <summary>Hours in a season (15 days).</summary>
        public const int SeasonHours = 15 * 24;

        /// <summary>A quadrum whose usual mean is at or below this is a snow season (°C).</summary>
        public const float SnowSeasonBelow = 3f;

        /// <summary>Snow never falls on land whose coldest month is this warm or warmer (°C): its nights stay above freezing.</summary>
        public const float SnowNeverFrom = 7f;

        /// <summary>The climate never moves the temperature further than this, all together (°C).</summary>
        public const float MaxColder = 28f, MaxWarmer = 24f;

        /// <summary>The land's record keeps this many years.</summary>
        public const int RecordYears = 40;

        /// <summary>A season's lean stays within these.</summary>
        public const float MinWet = -0.85f, MaxWet = 1.4f, MinSnow = -0.95f, MaxSnow = 1.5f;

        public static ClimateParams Params(ClimateStrength s)
        {
            switch (s)
            {
                case ClimateStrength.Gentle:
                    return new ClimateParams
                    {
                        nonOrdinary = 0.30f, extremeShare = 0.10f, magnitude = 0.7f, yearRate = 0.04f, rareRate = 0.02f, maxDrop = 16f, maxRise = 13f,
                        driftLo = 0.8f, driftHi = 1.5f, driftCap = 2.5f, wetLo = 0.05f, wetHi = 0.1f, wetCap = 0.2f, specialFactor = 0.6f
                    };
                case ClimateStrength.Usual:
                    return new ClimateParams
                    {
                        nonOrdinary = 0.48f, extremeShare = 0.18f, magnitude = 1f, yearRate = 0.08f, rareRate = 0.05f, maxDrop = 22f, maxRise = 18f,
                        driftLo = 1f, driftHi = 3f, driftCap = 4f, wetLo = 0.08f, wetHi = 0.18f, wetCap = 0.3f, specialFactor = 1f
                    };
                case ClimateStrength.Dramatic:
                    return new ClimateParams
                    {
                        nonOrdinary = 0.65f, extremeShare = 0.30f, magnitude = 1.3f, yearRate = 0.16f, rareRate = 0.12f, maxDrop = 26f, maxRise = 22f,
                        driftLo = 2f, driftHi = 4f, driftCap = 6f, wetLo = 0.15f, wetHi = 0.25f, wetCap = 0.45f, specialFactor = 1.5f
                    };
                default:
                    return default;
            }
        }

        // ---------------- The hash ----------------

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        /// <summary>A key for the land, a year, a quadrum and a purpose: the same inputs always give the same key.</summary>
        public static uint Key(int seed, int year, int quadrum, uint salt) =>
            Mix(Mix((uint)seed * 2654435761u) ^ Mix((uint)year * 40503u + salt * 0x27d4eb2fu) ^ (uint)(quadrum + 1) * 0x9E3779B9u);

        /// <summary>The key's i-th number, between 0 and 1 (never 1).</summary>
        public static float U(uint key, int i) => Mix(key + (uint)i * 0x632BE5ABu) / 4294967296f;

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private const uint SaltSeason = 11, SaltYear = 23, SaltRare = 37, SaltSpecial = 41, SaltSign = 53, SaltDrift = 67;

        // ---------------- The season's part in the year ----------------

        /// <summary>
        /// The part a quadrum plays in the year: the game's season where it has seasons; where the season is permanent, by
        /// warmth (the warmest the summer, the coldest the winter, the one after the coldest the spring).
        /// </summary>
        public static int Role(int season, int quadrum, ClimateLand l)
        {
            if (season >= 1 && season <= 4) return season;
            int rank = l.Rank(quadrum);
            if (rank == 3) return 2;
            if (rank == 0) return 4;
            int coldest = Enumerable.Range(0, 4).First(i => l.Rank(i) == 0);
            return ((quadrum - coldest + 4) % 4) == 1 ? 1 : 3;
        }

        public static bool SnowSeason(ClimateLand l, int quadrum) => l.Quad(quadrum) <= SnowSeasonBelow;

        // ---------------- The year ----------------

        /// <summary>
        /// A year's own extreme, now and then: a year without a summer, a drought year or a year of floods; none on land that
        /// is always frozen. The same tile and year always give the same.
        /// </summary>
        public static YearKind YearKindOf(int seed, int year, ClimateLand l, ClimateStrength s)
        {
            var p = Params(s);
            if (p.yearRate <= 0f || l.b.Frozen) return YearKind.Ordinary;
            float u = U(Key(seed, year, -1, SaltYear), 0);
            float noSummer = p.yearRate * 0.25f, drought = p.yearRate * 0.375f;
            if (u < noSummer) return YearKind.NoSummer;
            if (u < noSummer + drought) return YearKind.Drought;
            if (u < p.yearRate) return YearKind.Floods;
            return YearKind.Ordinary;
        }

        // ---------------- The season ----------------

        /// <summary>
        /// A season's character on this land in this year and quadrum, at this strength: its kind and lean, the year's extreme
        /// where it is part of one, once in a lifetime if it comes, and the weather no biome has that it brings, or the sign
        /// before a hard one. <paramref name="season"/> is the game's season then (Season as a number).
        /// </summary>
        public static SeasonCharacter SeasonOf(int seed, int year, int quadrum, int season, int nextSeason, ClimateLand l, ClimateStrength s)
        {
            var c = Core(seed, year, quadrum, season, l, s);
            Specials(ref c, seed, l, s);
            int ny = quadrum == 3 ? year + 1 : year, nq = (quadrum + 1) % 4;
            var next = Core(seed, ny, nq, nextSeason, l, s);
            Sign(ref c, next, seed, l, s);
            return c;
        }

        /// <summary>The character without its specials and sign: the kind, the lean, the year's extreme and once in a lifetime.</summary>
        public static SeasonCharacter Core(int seed, int year, int quadrum, int season, ClimateLand l, ClimateStrength s)
        {
            var p = Params(s);
            var c = new SeasonCharacter { year = year, quadrum = quadrum, season = season, role = Role(season, quadrum, l), snowSeason = SnowSeason(l, quadrum) };
            if (s == ClimateStrength.Off) return c;
            uint key = Key(seed, year, quadrum, SaltSeason);
            float mag = p.magnitude;
            bool summer = c.role == 2, winter = c.role == 4, spring = c.role == 1;
            if (U(key, 0) < p.nonOrdinary)
            {
                float u = U(key, 1);
                c.extreme = U(key, 2) < p.extremeShare;
                float m = U(key, 3);
                if (c.snowSeason)
                    c.kind = u < 0.27f ? SeasonKind.Snowy : u < 0.50f ? SeasonKind.Snowless : u < 0.75f ? SeasonKind.Warm : SeasonKind.Cold;
                else
                    c.kind = u < 0.27f ? SeasonKind.Wet : u < 0.54f ? SeasonKind.Dry : u < 0.77f ? SeasonKind.Warm : SeasonKind.Cold;
                Lean(ref c, c.kind, c.extreme, m, mag, summer, spring);
            }
            // The year's own extreme takes the seasons it belongs to.
            var yk = YearKindOf(seed, year, l, s);
            if (yk != YearKind.Ordinary)
            {
                float m = U(key, 4);
                switch (yk)
                {
                    case YearKind.NoSummer:
                        if (!winter)
                        {
                            c.yearKind = yk;
                            c.kind = SeasonKind.Cold;
                            c.extreme = summer;
                            c.temp = -(summer ? Lerp(4f, 6f, m) : Lerp(2.5f, 3.5f, m)) * mag;
                            c.wet = 0.15f;
                            c.snow = 0f;
                        }
                        break;
                    case YearKind.Drought:
                        c.yearKind = yk;
                        c.kind = c.snowSeason ? SeasonKind.Snowless : SeasonKind.Dry;
                        c.extreme = summer && !c.snowSeason;
                        Lean(ref c, c.kind, c.extreme, m, mag, summer, spring);
                        break;
                    case YearKind.Floods:
                        c.yearKind = yk;
                        c.kind = c.snowSeason ? SeasonKind.Snowy : SeasonKind.Wet;
                        c.extreme = !c.snowSeason && (spring || c.role == 3);
                        Lean(ref c, c.kind, c.extreme, m, mag, summer, spring);
                        break;
                }
            }
            Rare(ref c, seed, l, s);
            return c;
        }

        /// <summary>How far a kind of season leans, at a point <paramref name="m"/> (0 to 1) of its range and the strength's magnitude.</summary>
        private static void Lean(ref SeasonCharacter c, SeasonKind kind, bool x, float m, float mag, bool summer, bool spring)
        {
            c.temp = c.wet = c.snow = 0f;
            switch (kind)
            {
                case SeasonKind.Wet:
                    c.wet = (x ? Lerp(0.85f, 1.1f, m) : Lerp(0.45f, 0.6f, m)) * mag;
                    if (summer) c.temp = (x ? -1f : -0.5f) * mag;
                    break;
                case SeasonKind.Dry:
                    c.wet = -(x ? Lerp(0.65f, 0.8f, m) : Lerp(0.4f, 0.5f, m)) * mag;
                    if (summer || spring) c.temp = (x ? Lerp(1f, 2f, m) : Lerp(0.5f, 1f, m)) * mag;
                    break;
                case SeasonKind.Warm:
                    c.temp = (x ? Lerp(4f, 6.5f, m) : Lerp(2f, 3.5f, m)) * mag;
                    if (summer) c.wet = -0.1f * mag;
                    break;
                case SeasonKind.Cold:
                    c.temp = -(x ? Lerp(4f, 8f, m) : Lerp(2f, 3.5f, m)) * mag;
                    break;
                case SeasonKind.Snowy:
                    c.snow = (x ? Lerp(0.9f, 1.2f, m) : Lerp(0.5f, 0.7f, m)) * mag;
                    c.temp = (x ? -2.5f : -1f) * mag;
                    c.wet = (x ? 0.25f : 0.15f) * mag;
                    break;
                case SeasonKind.Snowless:
                    c.snow = -(x ? Lerp(0.85f, 0.95f, m) : Lerp(0.55f, 0.65f, m)) * mag;
                    c.temp = (x ? 2.5f : 1f) * mag;
                    c.wet = x ? -0.15f * mag : 0f;
                    break;
            }
            c.wet = Clamp(c.wet, MinWet, MaxWet);
            c.snow = Clamp(c.snow, MinSnow, MaxSnow);
        }

        // ---------------- Once in a lifetime ----------------

        /// <summary>
        /// The kinds of once in a lifetime that could come to this land in this quadrum: only what its own climate never
        /// brings, in the season it would come in, and within the strength's reach of the temperature. With the temperature
        /// each moves (°C) and how long it lasts (hours, low and high).
        /// </summary>
        public static List<(RareKind kind, float temp, int lo, int hi)> RareCandidates(ClimateLand l, int quadrum, ClimateStrength s)
        {
            var p = Params(s);
            var list = new List<(RareKind, float, int, int)>();
            if (s == ClimateStrength.Off) return list;
            var b = l.b;
            float m = l.Quad(quadrum);
            int rank = l.Rank(quadrum);
            // Snow where it never snows, in the coldest season, cold enough for it to fall and lie (a mean of -6 °C).
            if (!l.hasSnow && b.coldest >= SnowNeverFrom && rank == 0 && m + 6f <= p.maxDrop) list.Add((RareKind.Snow, -(m + 6f), 48, 96));
            // A hard frost on warm land: a mean of 2 °C, frost at night.
            if (b.coldest >= 12f && rank == 0 && m - 2f <= p.maxDrop && m - 2f >= 10f) list.Add((RareKind.Frost, -(m - 2f), 72, 120));
            // A great heat on cold land, in its warmest season: toward a mean of 30 °C.
            if (b.warmest <= 16f && rank == 3)
            {
                float rise = Math.Min(30f - m, p.maxRise);
                if (rise >= 10f) list.Add((RareKind.Heat, rise, 72, 144));
            }
            // A blizzard where blizzards don't come, on land with cold winters.
            if (l.blizzardDef && !l.hasBlizzard && b.coldest <= 3f && b.coldest >= -15f && rank == 0)
            {
                float drop = Clamp(m + 8f, 4f, 12f);
                if (drop <= p.maxDrop) list.Add((RareKind.Blizzard, -drop, 36, 72));
            }
            // A dust storm where the land has no dust weather: warm, not wet.
            if (l.sandDef && !l.hasDust && b.rainfall < 1400f && !b.Frozen && m >= 14f && rank >= 2) list.Add((RareKind.Dust, 2f, 18, 40));
            // Heavy rain on dry land.
            if (b.Arid && !l.hasDownpour && m >= 6f) list.Add((RareKind.Deluge, -2f, 18, 40));
            // A great storm off the water, on wet, warm land by open water.
            if (l.hurricaneDef && b.water && b.rainfall >= 900f && m >= 16f) list.Add((RareKind.GreatStorm, -1f, 18, 30));
            return list;
        }

        /// <summary>
        /// The year's once in a lifetime, if it has one: drawn once a year (so a land where it could come in only one season
        /// sees it as often as one where it could come in any), in one of the seasons it could come in, at an hour of that
        /// season and for a while. Quadrum -1 when the year has none.
        /// </summary>
        public static (int quadrum, RareKind kind, float temp, int start, int hours) RareOfYear(int seed, int year, ClimateLand l, ClimateStrength s)
        {
            var p = Params(s);
            var none = (-1, RareKind.None, 0f, 0, 0);
            if (s == ClimateStrength.Off) return none;
            uint key = Key(seed, year, -1, SaltRare);
            if (U(key, 0) >= p.rareRate) return none;
            var cands = new List<(int q, RareKind kind, float temp, int lo, int hi)>();
            for (int q = 0; q < 4; q++)
                foreach (var c in RareCandidates(l, q, s))
                    cands.Add((q, c.kind, c.temp, c.lo, c.hi));
            if (cands.Count == 0) return none;
            var pick = cands[Math.Min(cands.Count - 1, (int)(U(key, 1) * cands.Count))];
            int hours = pick.lo + (int)(U(key, 2) * (pick.hi - pick.lo + 1));
            int latest = SeasonHours - hours - 12;
            int start = 36 + (int)(U(key, 3) * Math.Max(1, latest - 36));
            return (pick.q, pick.kind, pick.temp, Math.Max(24, Math.Min(start, latest)), hours);
        }

        private static void Rare(ref SeasonCharacter c, int seed, ClimateLand l, ClimateStrength s)
        {
            var r = RareOfYear(seed, c.year, l, s);
            if (r.quadrum != c.quadrum || r.kind == RareKind.None) return;
            c.rare = r.kind;
            c.rareTemp = r.temp;
            c.rareHours = r.hours;
            c.rareStart = r.start;
        }

        /// <summary>Once in a lifetime that brings weather of its own (the rest move only the temperature, with clear skies).</summary>
        public static bool RareHasWeather(RareKind k) => k == RareKind.Snow || k == RareKind.Blizzard || k == RareKind.Dust || k == RareKind.Deluge || k == RareKind.GreatStorm;

        /// <summary>Once in a lifetime that can hurt (a great storm's winds and lightning): it comes only within the shared harm budget.</summary>
        public static bool RareHarms(RareKind k) => k == RareKind.GreatStorm;

        // ---------------- The weathers no biome has ----------------

        /// <summary>
        /// Each weather no biome has, given a place in the seasons: its def, the mod it comes from, whether it is brought as a
        /// game condition (so it does what its words say: fertile rain's growth), whether it can hurt. Only these are ever
        /// brought; the rest of the game's event weathers are left to their events (<see cref="Never"/>).
        /// </summary>
        public static readonly (SpecialKind kind, string def, string mod, bool condition)[] SpecialDefs =
        {
            (SpecialKind.FertileRain, "VEE_FertileRainsGameCondition", "Vanilla Landmarks Expanded", true),
            (SpecialKind.Drizzle, "RG_Drizzle", "ReGrowth 2", false),
            (SpecialKind.Dust, "Dust", "Seasonal Weather", false),
            (SpecialKind.Dust, "VEE_DustStorm", "Vanilla Landmarks Expanded", false),
            (SpecialKind.FogSnow, "FogSnow", "Seasonal Weather", false),
            (SpecialKind.Whiteout, "VEE_Whiteout", "Vanilla Events Expanded", false),
            (SpecialKind.Swelter, "VEE_Swelter", "Vanilla Events Expanded", false),
            (SpecialKind.Petals, "AB_PetalStorms", "Alpha Biomes", false),
            (SpecialKind.Rainbow, "DoubleRainbow", "Seasonal Weather", false),
            (SpecialKind.FogStorm, "RG_FogStorm", "ReGrowth 2", false),
            (SpecialKind.RedRain, "AB_ReddishRain", "Alpha Biomes", false)
        };

        /// <summary>
        /// The game's and the mods' event weathers the climate never brings, for what they do: blood rain drives people
        /// berserk, the death pall raises the dead, the unnatural darkness and the long night are a story's own, psychic rain
        /// ages whoever stands in it, the inferno sets fires, the bloodstorm and the psychic storm are psycasts', radioactive
        /// fog a reactor's, the dust clouds are space's, the magic hail strikes, healing rain is a mage's, and acid rain only
        /// with a volcanic winter, within the shared harm budget.
        /// </summary>
        public static readonly string[] Never =
        {
            "BloodRain", "UnnaturalFog", "UnnaturalDarkness_Stage1", "UnnaturalDarkness_Stage2", "VEE_LongNight", "VEE_PsychicRain", "VEE_Inferno",
            "VPEH_Bloodstorm", "VREA_PsychicStorm", "VPE_RadioactiveFog", "VGE_DustCloud", "VGE_ToxicDustCloud", "TM_HailstormWD", "TM_HealingRainWD",
            "AB_AcidRainWeather", "AB_ForsakenNight_Alternate", "AB_ForsakenRainyNight_Alternate", "AB_ForsakenThunderstorm_Alternate"
        };

        /// <summary>The weathers once in a lifetime may bring, by kind (the first the game has is taken; a snow is gentle or hard by the hash).</summary>
        public static string[] RareDefs(RareKind k)
        {
            switch (k)
            {
                case RareKind.Snow: return new[] { "SnowGentle", "SnowHard" };
                case RareKind.Blizzard: return new[] { "Blizzard" };
                case RareKind.Dust: return new[] { "Sandstorm", "VEE_DustStorm", "DryDuststorm" };
                case RareKind.Deluge: return new[] { "TorrentialRain", "RainyThunderstorm" };
                case RareKind.GreatStorm: return new[] { "VPE_Hurricane" };
                default: return new string[0];
            }
        }

        /// <summary>The chance a season brings a weather no biome has, by the season and the land (before the strength's factor).</summary>
        public static List<(SpecialKind kind, float chance)> SpecialChances(SeasonCharacter c, ClimateLand l)
        {
            var list = new List<(SpecialKind, float)>();
            float m = l.Quad(c.quadrum);
            bool spring = c.role == 1, summer = c.role == 2, fall = c.role == 3;
            bool wet = c.kind == SeasonKind.Wet, dry = c.kind == SeasonKind.Dry;
            if (wet && (spring || summer) && m >= 8f) list.Add((SpecialKind.FertileRain, 0.2f));
            if (wet && m >= 3f) list.Add((SpecialKind.Drizzle, 0.25f));
            if (dry && m >= 12f && !l.b.Humid) list.Add((SpecialKind.Dust, 0.25f));
            if (c.kind == SeasonKind.Snowy && m <= 0f) list.Add((SpecialKind.FogSnow, 0.25f));
            if (c.extreme && (c.kind == SeasonKind.Snowy || c.kind == SeasonKind.Cold) && c.snowSeason && m <= -2f) list.Add((SpecialKind.Whiteout, 0.3f));
            if (c.extreme && c.kind == SeasonKind.Warm && summer && m >= 18f) list.Add((SpecialKind.Swelter, 0.3f));
            if (spring && (c.kind == SeasonKind.Ordinary || c.kind == SeasonKind.Warm || wet) && m >= 8f) list.Add((SpecialKind.Petals, 0.04f));
            if ((spring || summer) && (c.kind == SeasonKind.Ordinary || wet) && m >= 4f) list.Add((SpecialKind.Rainbow, wet ? 0.1f : 0.05f));
            if (fall && (wet || c.kind == SeasonKind.Cold) && m >= 0f && m <= 15f) list.Add((SpecialKind.FogStorm, 0.12f));
            return list;
        }

        private static void Specials(ref SeasonCharacter c, int seed, ClimateLand l, ClimateStrength s)
        {
            var p = Params(s);
            if (s == ClimateStrength.Off) return;
            uint key = Key(seed, c.year, c.quadrum, SaltSpecial);
            float u = U(key, 0), sum = 0f;
            foreach (var (kind, chance) in SpecialChances(c, l))
            {
                if (!l.Has(kind)) continue;
                sum += chance * p.specialFactor;
                if (u >= sum) continue;
                c.special = kind;
                c.specialHours = kind == SpecialKind.FertileRain ? 12 + (int)(U(key, 1) * 19) : 4 + (int)(U(key, 1) * 9);
                c.specialStart = 24 + (int)(U(key, 2) * (SeasonHours - 72 - c.specialHours));
                return;
            }
        }

        /// <summary>
        /// A sign before a hard season, a year's extreme or once in a lifetime: red rain in this season's last days, now and
        /// then, where the land is warm enough for rain; a few come before nothing at all.
        /// </summary>
        private static void Sign(ref SeasonCharacter c, SeasonCharacter next, int seed, ClimateLand l, ClimateStrength s)
        {
            var p = Params(s);
            if (s == ClimateStrength.Off || !l.Has(SpecialKind.RedRain) || l.Quad(c.quadrum) < 2f) return;
            uint key = Key(seed, c.year, c.quadrum, SaltSign);
            bool hard = next.extreme || next.rare != RareKind.None || (next.yearKind != YearKind.Ordinary && next.yearKind != c.yearKind);
            float chance = hard ? 0.4f : 0.015f;
            if (U(key, 0) >= chance * p.specialFactor) return;
            c.special = SpecialKind.RedRain;
            c.sign = true;
            c.specialHours = 3 + (int)(U(key, 1) * 6);
            c.specialStart = SeasonHours - 96 + (int)(U(key, 2) * 72);
        }

        // ---------------- The season said ----------------

        /// <summary>The kinds a season of this sort can be (what a forecast can say).</summary>
        public static SeasonKind[] KindsFor(bool snowSeason) => snowSeason
            ? new[] { SeasonKind.Ordinary, SeasonKind.Snowy, SeasonKind.Snowless, SeasonKind.Warm, SeasonKind.Cold }
            : new[] { SeasonKind.Ordinary, SeasonKind.Wet, SeasonKind.Dry, SeasonKind.Warm, SeasonKind.Cold };

        /// <summary>How often a season's forecast comes true: a little less than a day's (a season is harder to read).</summary>
        public static float SeasonAccuracy(float dayAccuracy) => Clamp(dayAccuracy * 0.9f, 0.4f, 0.9f);

        /// <summary>
        /// What the best reader of weather says a season looks like at its start: right as often as <paramref name="accuracy"/>
        /// (an extreme said as one), else wrong: half the time an unusual season taken for an ordinary one, else another kind.
        /// </summary>
        public static (SeasonKind kind, bool extreme) Guess(SeasonCharacter actual, float accuracy, float u1, float u2)
        {
            if (u1 < accuracy) return (actual.kind, actual.extreme);
            if (actual.kind != SeasonKind.Ordinary && u2 < 0.5f) return (SeasonKind.Ordinary, false);
            var others = KindsFor(actual.snowSeason).Where(k => k != actual.kind).ToArray();
            float v = actual.kind != SeasonKind.Ordinary ? (u2 - 0.5f) * 2f : u2;
            return (others[Math.Min(others.Length - 1, (int)(v * others.Length))], false);
        }

        /// <summary>The words key for a season of this kind ("a wet {0}", "a hard {0}"): a winter's warm is mild, its cold extreme hard.</summary>
        public static string PhraseKey(SeasonKind kind, bool extreme, int role)
        {
            string k = kind.ToString();
            if (role == 4 && (kind == SeasonKind.Warm || kind == SeasonKind.Cold)) k += "Winter";
            return "CC_Clim_Season_" + k + (extreme && kind != SeasonKind.Ordinary ? "X" : "");
        }

        /// <summary>The words key for how a season was, in the annals ("wet", "hard").</summary>
        public static string WasKey(SeasonKind kind, bool extreme, int role) => PhraseKey(kind, extreme, role).Replace("CC_Clim_Season_", "CC_Clim_Was_");

        // ---------------- The drift ----------------

        /// <summary>
        /// The drift's trend after <paramref name="years"/>: one way for the whole game (warmer or colder, and drier, wetter
        /// or neither, by the world in Random), by 1 to 3 °C a decade as designed (a rate of its own each decade), within the
        /// strength's cap. Chosen ways move only the temperature (warmer, colder) or only the rain (drier, wetter).
        /// </summary>
        public static (float temp, float wet) Trend(ClimateDriftMode mode, int worldSeed, float years, ClimateStrength s)
        {
            if (mode == ClimateDriftMode.Off || s == ClimateStrength.Off || years <= 0f) return (0f, 0f);
            var p = Params(s);
            uint key = Key(worldSeed, 0, 0, SaltDrift);
            float tSign = 0f, wSign = 0f;
            switch (mode)
            {
                case ClimateDriftMode.Random:
                    tSign = U(key, 0) < 0.5f ? -1f : 1f;
                    float uw = U(key, 1);
                    wSign = uw < 0.35f ? -1f : uw < 0.7f ? 1f : 0f;
                    break;
                case ClimateDriftMode.Warmer: tSign = 1f; break;
                case ClimateDriftMode.Colder: tSign = -1f; break;
                case ClimateDriftMode.Drier: wSign = -1f; break;
                case ClimateDriftMode.Wetter: wSign = 1f; break;
            }
            float t = 0f, w = 0f;
            for (int d = 0; d < 64 && d * 10f < years; d++)
            {
                float span = Math.Min(10f, years - d * 10f) / 10f;
                t += tSign * Lerp(p.driftLo, p.driftHi, U(key, 10 + d)) * span;
                w += wSign * Lerp(p.wetLo, p.wetHi, U(key, 80 + d)) * span;
            }
            return (Clamp(t, -p.driftCap, p.driftCap), Clamp(w, -p.wetCap, p.wetCap));
        }

        /// <summary>A push on the climate that rises, holds and fades (years since it began): 0 before and after, 1 while it holds.</summary>
        public static float Envelope(float t, float rise, float hold, float fade)
        {
            if (t < 0f) return 0f;
            if (t < rise) return rise > 0f ? t / rise : 1f;
            t -= rise;
            if (t < hold) return 1f;
            t -= hold;
            return t < fade ? 1f - t / fade : 0f;
        }

        /// <summary>The push something that happened sets going (°C and wetness), <paramref name="years"/> after it began.</summary>
        public static (float temp, float wet) Push(string kind, float years, float sign, ClimateStrength s)
        {
            float mag = Params(s).magnitude;
            switch (kind)
            {
                // Ash in the high air: a cooler few years.
                case "volcanic":
                {
                    float e = Envelope(years, 0.1f, 1f, 3f);
                    return (-1.5f * mag * e, 0.05f * mag * e);
                }
                // A comet: the climate set moving one way or the other for a few years.
                case "comet":
                {
                    float e = Envelope(years, 1f, 3f, 4f);
                    return (0.8f * sign * mag * e, 0.1f * sign * mag * e);
                }
                default:
                    return (0f, 0f);
            }
        }

        /// <summary>
        /// What the colony itself does to its land's climate: the map's pollution warms it (up to 2 °C), and the trees lost
        /// since the record began (a share, 0 to 1) make it warmer and drier once more than a sixth are gone.
        /// </summary>
        public static (float temp, float wet) Local(float pollution, float treeLoss, ClimateStrength s)
        {
            if (s == ClimateStrength.Off) return (0f, 0f);
            float mag = Params(s).magnitude;
            float t = Math.Min(2f, 2.5f * Clamp(pollution, 0f, 1f)) * mag;
            float w = 0f;
            float loss = Clamp(treeLoss, 0f, 1f);
            if (loss >= 0.15f)
            {
                t += 0.6f * loss * mag;
                w -= 0.25f * loss * mag;
            }
            return (t, w);
        }

        // ---------------- The land's record ----------------

        /// <summary>
        /// The trends the land's record shows: its first years against its last (up to three of each, four years at least):
        /// winters milder or harsher and summers hotter or cooler by 1.2 °C or more, a quarter more or fewer wet days, the
        /// growing season six days longer or shorter, the first snow a week earlier or later. Strongest first.
        /// </summary>
        public static List<(TrendKind kind, float by)> Trends(IList<ClimateYear> years)
        {
            var list = new List<(TrendKind, float, float)>();
            var ys = years.Where(y => y != null && y.days >= 30).OrderBy(y => y.year).ToList();
            if (ys.Count < 4) return new List<(TrendKind, float)>();
            int k = Math.Min(3, ys.Count / 2);
            var first = ys.Take(k).ToList();
            var last = ys.Skip(ys.Count - k).ToList();
            float winter = last.Average(y => y.winter) - first.Average(y => y.winter);
            if (Math.Abs(winter) >= 1.2f) list.Add((winter > 0f ? TrendKind.WinterMilder : TrendKind.WinterHarsher, Math.Abs(winter), Math.Abs(winter) / 1.2f));
            float summer = last.Average(y => y.summer) - first.Average(y => y.summer);
            if (Math.Abs(summer) >= 1.2f) list.Add((summer > 0f ? TrendKind.SummerHotter : TrendKind.SummerCooler, Math.Abs(summer), Math.Abs(summer) / 1.2f));
            float wetFirst = first.Sum(y => y.wetDays) / (float)Math.Max(1, first.Sum(y => y.days));
            float wetLast = last.Sum(y => y.wetDays) / (float)Math.Max(1, last.Sum(y => y.days));
            if (wetFirst > 0f && wetLast >= wetFirst * 1.25f && wetLast - wetFirst >= 0.05f) list.Add((TrendKind.Wetter, wetLast / wetFirst, wetLast / wetFirst / 1.25f));
            else if (wetFirst > 0f && wetLast <= wetFirst * 0.8f && wetFirst - wetLast >= 0.05f) list.Add((TrendKind.Drier, wetFirst / Math.Max(0.001f, wetLast), wetFirst / Math.Max(0.001f, wetLast) / 1.25f));
            if (first.All(y => y.growDays < y.days) || last.All(y => y.growDays < y.days))
            {
                float grow = (float)(last.Average(y => y.growDays * 60.0 / y.days) - first.Average(y => y.growDays * 60.0 / y.days));
                if (Math.Abs(grow) >= 6f) list.Add((grow > 0f ? TrendKind.GrowLonger : TrendKind.GrowShorter, Math.Abs(grow), Math.Abs(grow) / 6f));
            }
            var snowFirst = first.Where(y => y.firstSnow >= 0).ToList();
            var snowLast = last.Where(y => y.firstSnow >= 0).ToList();
            if (snowFirst.Count > 0 && snowLast.Count > 0)
            {
                int anchor = snowFirst[0].firstSnow;
                float a = snowFirst.Average(y => (float)Around(y.firstSnow, anchor)), b = snowLast.Average(y => (float)Around(y.firstSnow, anchor));
                float later = b - a;
                if (Math.Abs(later) >= 7f) list.Add((later > 0f ? TrendKind.SnowLater : TrendKind.SnowEarlier, Math.Abs(later), Math.Abs(later) / 7f));
            }
            return list.OrderByDescending(x => x.Item3).Select(x => (x.Item1, x.Item2)).ToList();
        }

        /// <summary>A day of the year as days from an anchor day, the short way round (-30 to 29).</summary>
        private static int Around(int doy, int anchor)
        {
            int d = ((doy - anchor) % WeatherMath.YearDays + WeatherMath.YearDays) % WeatherMath.YearDays;
            return d >= WeatherMath.YearDays / 2 ? d - WeatherMath.YearDays : d;
        }

        /// <summary>The trends that belong to a season's part in the year, for the elders to remark on at its start.</summary>
        public static bool TrendFits(TrendKind t, int role)
        {
            switch (t)
            {
                case TrendKind.WinterMilder:
                case TrendKind.WinterHarsher: return role == 4;
                case TrendKind.SummerHotter:
                case TrendKind.SummerCooler: return role == 2;
                case TrendKind.GrowLonger:
                case TrendKind.GrowShorter: return role == 1;
                case TrendKind.SnowEarlier:
                case TrendKind.SnowLater: return role == 3;
                default: return true;
            }
        }
    }

    /// <summary>
    /// One year of the land's record (LandClimate, E3): how far from the land's usual its winter and its summer ran (°C), its
    /// wet days of the days kept, its growing days (warm enough to grow), its days with snow lying, and the day of the year
    /// the first snow came (-1 if none). Kept as a short line.
    /// </summary>
    public sealed class ClimateYear
    {
        public int year, wetDays, days, growDays, cover, firstSnow = -1;
        public float winter, summer;

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        public string Pack() => string.Join(";", year, winter.ToString("0.0", Inv), summer.ToString("0.0", Inv), wetDays, days, growDays, cover, firstSnow);

        public static ClimateYear Unpack(string s)
        {
            var p = s?.Split(';');
            if (p == null || p.Length < 8) return null;
            var y = new ClimateYear();
            if (!int.TryParse(p[0], out y.year) || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, Inv, out y.winter)
                || !float.TryParse(p[2], System.Globalization.NumberStyles.Float, Inv, out y.summer) || !int.TryParse(p[3], out y.wetDays)
                || !int.TryParse(p[4], out y.days) || !int.TryParse(p[5], out y.growDays) || !int.TryParse(p[6], out y.cover) || !int.TryParse(p[7], out y.firstSnow))
                return null;
            return y;
        }

        /// <summary>
        /// A year from the record's seasons (WeatherBook's quarters): three counted seasons of ten days or more at least; the
        /// winter is the season usually coldest, the summer the one usually warmest.
        /// </summary>
        public static ClimateYear Of(IList<WeatherQuarter> quarters, int year, int firstSnowDoy)
        {
            var mine = quarters.Where(q => q.year == year && q.days >= 10).ToList();
            if (mine.Count < 3) return null;
            var winter = mine.OrderBy(q => q.Expected).First();
            var summer = mine.OrderByDescending(q => q.Expected).First();
            return new ClimateYear
            {
                year = year, winter = winter.Anomaly, summer = summer.Anomaly, wetDays = mine.Sum(q => q.wetDays), days = mine.Sum(q => q.days),
                growDays = mine.Where(q => q.counted).Sum(q => q.days - q.coldDays) + mine.Where(q => !q.counted).Sum(q => q.days),
                cover = mine.Sum(q => q.coverDays), firstSnow = firstSnowDoy
            };
        }
    }
}
