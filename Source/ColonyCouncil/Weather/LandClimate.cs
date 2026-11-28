using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An extreme of the land's climate as the other parts read it (<see cref="LandClimate.ExtremeNow"/> and its events): a
    /// season far from the usual, a year's own extreme, or once in a lifetime; how far it moves the temperature, the weather it
    /// brings (a def name, or null) and what it is called.
    /// </summary>
    public sealed class ClimateExtreme
    {
        public ExtremeScope scope;
        public SeasonKind seasonKind;
        public YearKind yearKind;
        public RareKind rareKind;
        public int year, quadrum;
        /// <summary>How far it moves the temperature (°C, below 0 colder).</summary>
        public float temp;
        public string weather;
        public string label;

        public override string ToString() => label ?? scope.ToString();
    }

    /// <summary>
    /// The land's climate (docs/design/living-land.md), the one place a season's character and the drift are decided and
    /// applied. Each season gets a character when it begins, drawn from the land, the year and the season (ClimateMath, no
    /// state): most ordinary, some wet, dry, warm, cold, snowy or snowless, a few extreme. It leans the weather the game
    /// chooses (WeatherDynamicsMath.Character, through <see cref="WeatherLean"/>) and moves the temperature for the whole
    /// season (<see cref="Rare"/>, eased in over about half a day). At its start the colonist who reads the weather best
    /// says what kind of season it looks like, and may be wrong. Now and then a year has its own extreme (a year without a
    /// summer, a drought year, a year of floods), and the region's world event to match is started where it can be; very
    /// rarely a season brings weather from outside the land's own climate, with the temperature to match, for days. The
    /// weathers no biome has come with the seasons, as signs and in the world events; the climate drifts slowly over the
    /// years (Weather/ClimateDrift.cs); and the land's record keeps each year for the annals and the elders.
    ///
    /// Other parts read it through <see cref="SeasonNow"/>, <see cref="YearNow"/>, <see cref="DriftNow"/>,
    /// <see cref="ExtremeNow"/> and the events <see cref="SeasonBegan"/>, <see cref="ExtremeBegan"/> and
    /// <see cref="ExtremeEnded"/>. It runs in the weather record's hour (Sched.Weather) and its 250-tick pass
    /// (Sched.RareWeather): nothing per tick but the eased number. Saved with the map's weather record.
    /// </summary>
    public static class LandClimate
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The strength as set: off with the deeper game hooks off.</summary>
        public static ClimateStrength Strength => RimfolkPatches.On ? S?.climateStrength ?? ClimateStrength.Usual : ClimateStrength.Off;

        public static bool On => Strength != ClimateStrength.Off;

        public static ClimateDriftMode DriftMode => On ? S?.climateDrift ?? ClimateDriftMode.Random : ClimateDriftMode.Off;

        /// <summary>
        /// Another mod drives this map's weather and temperature itself: the land's climate then stands down, its seasons and
        /// drift left to that mod. None in the player's list does, so false.
        /// </summary>
        public static bool OtherModDrivesWeather(Map map) => false;

        /// <summary>The climate works on this map: on, a home map with weather of its own, and no other mod driving its weather.</summary>
        public static bool Works(Map map) => On && WeatherDynamics.Applies(map) && !OtherModDrivesWeather(map);

        /// <summary>A season began on a home map (its character as drawn).</summary>
        public static event Action<Map, SeasonCharacter> SeasonBegan;

        /// <summary>An extreme began and was told: a season far from the usual, a year's own extreme, or once in a lifetime.</summary>
        public static event Action<Map, ClimateExtreme> ExtremeBegan;

        /// <summary>An extreme that was told is over (a season's or a year's at its season's end, once in a lifetime when it passes).</summary>
        public static event Action<Map, ClimateExtreme> ExtremeEnded;

        /// <summary>What the climate keeps for one home map (in its WeatherBook).</summary>
        public sealed class State
        {
            // Saved.
            /// <summary>The season last begun (year × 4 + quadrum), -1 before the first look.</summary>
            public int seasonKey = -1;
            /// <summary>The climate's push on the temperature now (°C), eased toward its target.</summary>
            public float climate;
            /// <summary>What the best reader of weather said the season would be, who, and for which season.</summary>
            public string said;
            public Pawn saidBy;
            public int saidKey = -1;
            /// <summary>What was told or brought already, by key (an extreme told, a weather brought, a world event's weather), the newest last.</summary>
            public List<string> done = new List<string>();
            /// <summary>The land's record: one short line a year (ClimateYear).</summary>
            public List<string> years = new List<string>();
            /// <summary>The extremes that came: "year:quadrum:what" ("5502:2:Rare:Snow", "5503:1:Year:Drought", "5503:3:Season:Cold").</summary>
            public List<string> came = new List<string>();
            /// <summary>The trees on the map when the record began, and at the last count.</summary>
            public int treesAtStart = -1, treesNow = -1;

            // Not saved.
            public ClimateLand land;
            public int landDay = -1;
            public int charKey = -1;
            public ClimateStrength charStrength;
            public SeasonCharacter ch;
            public float pollution;
            public int pollutionDay = -1;
            public bool elderDue;
            /// <summary>Dev: once in a lifetime set going by hand, until this tick.</summary>
            public RareKind devRare;
            public int devRareUntil = -1;
            public float devRareTemp;

            public const int MaxDone = 24, MaxCame = 24;

            public void Expose()
            {
                Scribe_Values.Look(ref seasonKey, "climSeason", -1);
                Scribe_Values.Look(ref climate, "climTemp");
                Scribe_Values.Look(ref said, "climSaid");
                Scribe_References.Look(ref saidBy, "climSaidBy");
                Scribe_Values.Look(ref saidKey, "climSaidKey", -1);
                Scribe_Collections.Look(ref done, "climDone", LookMode.Value);
                Scribe_Collections.Look(ref years, "climYears", LookMode.Value);
                Scribe_Collections.Look(ref came, "climCame", LookMode.Value);
                Scribe_Values.Look(ref treesAtStart, "climTreesAtStart", -1);
                Scribe_Values.Look(ref treesNow, "climTreesNow", -1);
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    done ??= new List<string>();
                    years ??= new List<string>();
                    came ??= new List<string>();
                }
            }

            public bool Done(string key) => done.Contains(key);

            public void Mark(string key)
            {
                if (done.Contains(key)) return;
                done.Add(key);
                while (done.Count > MaxDone) done.RemoveAt(0);
            }

            public void Came(int year, int quadrum, string what)
            {
                string k = year + ":" + quadrum + ":" + what;
                if (came.Contains(k)) return;
                came.Add(k);
                while (came.Count > MaxCame) came.RemoveAt(0);
            }
        }

        private static int Now => Find.TickManager.TicksGame;

        private static State StateOf(Map map) => WeatherBook.Of(map)?.clim;

        // ---------------- The land ----------------

        /// <summary>The land as its climate goes, worked out once a day: its weather, each quadrum's usual warmth, its own weathers and the game's.</summary>
        internal static ClimateLand Land(Map map, State st)
        {
            int day = GenDate.DaysPassed;
            if (st.landDay == day) return st.land;
            st.landDay = day;
            var l = new ClimateLand { b = WeatherDynamics.LandOf(map) };
            float[] q = new float[4];
            for (int i = 0; i < 4; i++)
                q[i] = (SeasonUtil.TwelfthTemp(map, (Twelfth)(i * 3)) + SeasonUtil.TwelfthTemp(map, (Twelfth)(i * 3 + 1)) + SeasonUtil.TwelfthTemp(map, (Twelfth)(i * 3 + 2))) / 3f;
            l.q0 = q[0];
            l.q1 = q[1];
            l.q2 = q[2];
            l.q3 = q[3];
            // What the land's own weather brings: a weather in its lists that its temperatures let come (nearly every biome
            // lists the snows, which never fall in a desert or a jungle).
            var own = WeatherDynamics.LandWeathers(map);
            float coldest = l.b.coldest, warmest = l.b.warmest;
            l.hasSnow = coldest < ClimateMath.SnowNeverFrom && own.Any(w => (WeatherDynamics.KindOf(w) == WeatherKind.Snow || w.snowRate > 0.05f) && w.temperatureRange.min <= coldest);
            l.hasBlizzard = coldest < 3f && own.Any(w => w.defName == "Blizzard");
            l.hasDust = own.Any(w => (WeatherDynamics.KindOf(w) == WeatherKind.Dust || w.sandRate > 0f) && w.temperatureRange.max >= warmest - 10f && w.temperatureRange.min <= warmest);
            l.hasDownpour = warmest > 2f && own.Any(w => WeatherDynamics.KindOf(w) == WeatherKind.Downpour);
            l.blizzardDef = Weather("Blizzard") != null;
            l.sandDef = ClimateMath.RareDefs(RareKind.Dust).Any(d => Weather(d) != null);
            l.torrentDef = Weather("TorrentialRain") != null;
            l.hurricaneDef = Weather("VPE_Hurricane") != null;
            foreach (var (kind, def, _, condition) in ClimateMath.SpecialDefs)
            {
                bool loaded = condition ? DefDatabase<GameConditionDef>.GetNamedSilentFail(def) != null : Weather(def) != null;
                if (loaded && !own.Any(w => w.defName == def)) l.specials |= ClimateLand.Bit(kind);
            }
            st.land = l;
            return l;
        }

        private static WeatherDef Weather(string def) => def == null || Array.IndexOf(ClimateMath.Never, def) >= 0 ? null : DefDatabase<WeatherDef>.GetNamedSilentFail(def);

        /// <summary>The game's season in the middle of a quadrum (0 to 3) of the local calendar, on this map's land.</summary>
        private static int SeasonOfQuadrum(Map map, int quadrum)
        {
            return (int)((Quadrum)(((quadrum % 4) + 4) % 4)).GetSeason(Find.WorldGrid.LongLatOf(map.Tile).y);
        }

        private static int Seed(Map map) => map.Tile.tileId;

        /// <summary>The season's character on this map for a year and quadrum, cached for the season now.</summary>
        private static SeasonCharacter Character(Map map, State st, int year, int quadrum)
        {
            var s = Strength;
            int key = year * 4 + quadrum;
            if (st.charKey == key && st.charStrength == s) return st.ch;
            var l = Land(map, st);
            var c = ClimateMath.SeasonOf(Seed(map), year, quadrum, SeasonOfQuadrum(map, quadrum), SeasonOfQuadrum(map, (quadrum + 1) % 4), l, s);
            if (OtherModDrivesWeather(map)) c = new SeasonCharacter { year = year, quadrum = quadrum, season = c.season, role = c.role, snowSeason = c.snowSeason };
            st.charKey = key;
            st.charStrength = s;
            st.ch = c;
            return c;
        }

        private static void Today(Map map, out int year, out int quadrum, out int hour)
        {
            year = GenLocalDate.Year(map);
            int doy = GenLocalDate.DayOfYear(map);
            quadrum = doy / WeatherMath.QuarterDays;
            hour = doy % WeatherMath.QuarterDays * 24 + GenLocalDate.HourOfDay(map);
        }

        // ---------------- What the other parts read ----------------

        /// <summary>The character of the season now on this map (an ordinary one with the climate off, or off a home map).</summary>
        public static SeasonCharacter SeasonNow(Map map)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return default;
            Today(map, out int year, out int q, out _);
            return Character(map, st, year, q);
        }

        /// <summary>The character of a season on this map, by year and quadrum (0 to 3): past or to come, the same every time.</summary>
        public static SeasonCharacter SeasonAt(Map map, int year, int quadrum)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return default;
            if (st.charKey == year * 4 + quadrum && st.charStrength == Strength) return st.ch;
            var l = Land(map, st);
            return ClimateMath.SeasonOf(Seed(map), year, quadrum, SeasonOfQuadrum(map, quadrum), SeasonOfQuadrum(map, (quadrum + 1) % 4), l, Strength);
        }

        /// <summary>This year's character on this map: its lean, wetter or drier and warmer or colder, and its own extreme if it has one.</summary>
        public static YearCharacter YearNow(Map map)
        {
            var st = StateOf(map);
            if (st == null || map?.TileInfo == null) return default;
            int year = GenLocalDate.Year(map);
            var l = Land(map, st);
            var lean = WeatherDynamicsMath.YearOf(Seed(map), year, l.b);
            return new YearCharacter { year = year, wet = lean.wet, temp = lean.temp, kind = Works(map) ? ClimateMath.YearKindOf(Seed(map), year, l, Strength) : YearKind.Ordinary };
        }

        /// <summary>
        /// The climate's drift on this map so far: the trend over the years and what set it going (a volcanic winter, a comet),
        /// with the colony's own part (pollution, the trees lost): °C, and the lean wetter (above 0) or drier.
        /// </summary>
        public static (float temp, float wet) DriftNow(Map map)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return (0f, 0f);
            if (DriftMode == ClimateDriftMode.Off) return (0f, 0f);
            var drift = ClimateDrift.Instance;
            var world = drift != null ? drift.Now(Now, DriftMode, Strength) : (temp: 0f, wet: 0f);
            var local = ClimateMath.Local(Pollution(map, st), TreeLoss(st), Strength);
            return (world.temp + local.temp, world.wet + local.wet);
        }

        /// <summary>The extreme going on now on this map: once in a lifetime first, then a season's or a year's; null if none.</summary>
        public static ClimateExtreme ExtremeNow(Map map)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return null;
            Today(map, out int year, out int q, out int hour);
            var c = Character(map, st, year, q);
            if (st.devRare != RareKind.None && Now < st.devRareUntil)
                return RareExtreme(map, st.devRare, year, q, st.devRareTemp);
            if (RareRunning(st, c, year, q, hour)) return RareExtreme(map, c.rare, year, q, c.rareTemp);
            if (c.yearKind != YearKind.Ordinary && c.extreme) return new ClimateExtreme { scope = ExtremeScope.Year, yearKind = c.yearKind, seasonKind = c.kind, year = year, quadrum = q, temp = c.temp, label = YearLabel(c.yearKind) };
            if (c.extreme) return new ClimateExtreme { scope = ExtremeScope.Season, seasonKind = c.kind, year = year, quadrum = q, temp = c.temp, label = Phrase(c.kind, true, c.role, SeasonLabel(map, c)) };
            return null;
        }

        /// <summary>The climate's push on this map's outdoor temperature now (°C), as eased.</summary>
        public static float TempNow(Map map) => StateOf(map)?.climate ?? 0f;

        /// <summary>The season's lean on the weather now (with the drift's wetness and once in a lifetime's), for the weather's choice (WeatherDynamics.Now).</summary>
        internal static void WeatherLean(Map map, WeatherBook book, ref WeatherNow n)
        {
            var st = book.clim;
            if (!Works(map)) return;
            Today(map, out int year, out int q, out int hour);
            var c = Character(map, st, year, q);
            float wet = c.wet + DriftNow(map).wet, snow = c.snow;
            var rare = RareNowKind(st, c, year, q, hour);
            switch (rare)
            {
                // A great cold or heat comes with clear, still air; dust with dry wind.
                case RareKind.Frost:
                case RareKind.Heat:
                    n.eventClear *= 1.6f;
                    wet -= 0.5f;
                    break;
                case RareKind.Dust:
                    wet -= 0.6f;
                    break;
                case RareKind.Deluge:
                case RareKind.GreatStorm:
                    wet += 1.2f;
                    break;
            }
            n.seasonWet = Mathf.Clamp(wet, ClimateMath.MinWet, ClimateMath.MaxWet);
            n.seasonSnow = Mathf.Clamp(snow, ClimateMath.MinSnow, ClimateMath.MaxSnow);
        }

        // ---------------- The temperature (every 250 ticks) ----------------

        /// <summary>How far the climate's push moves toward its target each look (250 ticks): about half a day to settle.</summary>
        private const float EaseRate = 1f / 110f;

        /// <summary>The climate's push eased toward what the season, once in a lifetime, the drift and the region's world events make it.</summary>
        public static void Rare(Map map, WeatherBook book)
        {
            var st = book.clim;
            float target = Works(map) ? Target(map, st) : 0f;
            st.climate += (target - st.climate) * EaseRate;
            if (target == 0f && Mathf.Abs(st.climate) < 0.02f) st.climate = 0f;
        }

        private static float Target(Map map, State st)
        {
            Today(map, out int year, out int q, out int hour);
            var c = Character(map, st, year, q);
            float t = c.temp;
            var rare = RareNowKind(st, c, year, q, hour);
            if (rare != RareKind.None) t += st.devRare != RareKind.None && Now < st.devRareUntil ? st.devRareTemp : c.rareTemp;
            t += DriftNow(map).temp;
            t += WorldTemp(map, c);
            return Mathf.Clamp(t, -ClimateMath.MaxColder, ClimateMath.MaxWarmer);
        }

        /// <summary>
        /// The region's world events on the temperature, so the land is as their words say: a year without a summer 3 °C
        /// colder, a hard winter 2.5 °C, the region's drought a degree warmer in the warm half of the year; none where the
        /// land's own year or season already is that.
        /// </summary>
        private static float WorldTemp(Map map, SeasonCharacter c)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return 0f;
            float t = 0f;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || (s.ctx?.map != null && s.ctx.map != map)) continue;
                switch (s.def.defName)
                {
                    case "Rimfolk_WS_ColdYear":
                        if (c.yearKind != YearKind.NoSummer) t -= 3f;
                        break;
                    case "Council_WS_HardWinter":
                        if (!(c.kind == SeasonKind.Cold && c.extreme)) t -= 2.5f;
                        break;
                    case "Council_WS_Drought":
                        if (c.yearKind != YearKind.Drought && (c.role == 1 || c.role == 2)) t += 1f;
                        break;
                }
            }
            return t;
        }

        // ---------------- Once an hour ----------------

        /// <summary>The weather record's hour: a season begun, an extreme told, once in a lifetime and the weathers no biome has brought, what set the drift going.</summary>
        public static void Hourly(Map map, WeatherBook book)
        {
            var st = book.clim;
            if (!Works(map)) return;
            int now = Now;
            ClimateDrift.Instance?.Begin(now);
            Today(map, out int year, out int q, out int hour);
            var c = Character(map, st, year, q);
            int key = year * 4 + q;
            if (st.seasonKey != key)
            {
                int before = st.seasonKey;
                st.seasonKey = key;
                if (before >= 0) Began(map, book, st, c, before, now);
                else Count(map, st);
            }
            Extremes(map, book, st, c, year, q, hour, now);
            RareHour(map, st, c, year, q, hour, now);
            Special(map, st, c, year, q, hour);
            World(map, st, c, now);
            Volcanic(map, st, now);
            Comet(map, st, now);
            if (st.elderDue && hour >= 6) Elder(map, st, c);
        }

        /// <summary>A new season: its forecast, the region's world event for a year's extreme, the trees counted, the events.</summary>
        private static void Began(Map map, WeatherBook book, State st, SeasonCharacter c, int before, int now)
        {
            // The season before, if it had an extreme that was told, is over.
            var prev = SeasonAt(map, before / 4, before % 4);
            if (prev.extreme && st.Done("x" + before))
                Ended(map, new ClimateExtreme { scope = prev.yearKind != YearKind.Ordinary ? ExtremeScope.Year : ExtremeScope.Season, seasonKind = prev.kind, yearKind = prev.yearKind, year = prev.year, quadrum = prev.quadrum, temp = prev.temp, label = prev.yearKind != YearKind.Ordinary ? YearLabel(prev.yearKind) : Phrase(prev.kind, true, prev.role, SeasonLabel(map, prev)) });
            Count(map, st);
            st.elderDue = true;
            try
            {
                SeasonBegan?.Invoke(map, c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: a season began (a listener)", e);
            }
            if (WeatherDynamics.ForecastOn) Foretell(map, st, c, c.year * 4 + c.quadrum);
            RegionFollows(map, st, c);
        }

        /// <summary>The trees on the map counted (the first count is the record's start), and its pollution: once a season.</summary>
        private static void Count(Map map, State st)
        {
            int trees = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                if (t?.def?.plant != null && t.def.plant.IsTree) trees++;
            st.treesNow = trees;
            if (st.treesAtStart < 0) st.treesAtStart = trees;
            st.pollutionDay = -1;
        }

        private static float TreeLoss(State st) => st.treesAtStart > 20 && st.treesNow >= 0 ? Mathf.Clamp01(1f - st.treesNow / (float)st.treesAtStart) : 0f;

        private static float Pollution(Map map, State st)
        {
            int day = GenDate.DaysPassed;
            if (st.pollutionDay == day) return st.pollution;
            st.pollutionDay = day;
            st.pollution = ModsConfig.BiotechActive && map.pollutionGrid != null ? map.pollutionGrid.TotalPollutionPercent : 0f;
            return st.pollution;
        }

        /// <summary>
        /// The season's forecast: the colonist who reads the weather best says what kind of season it looks like (right as often
        /// as a season can be read: ClimateMath.SeasonAccuracy), over their head if in view, and in the news.
        /// </summary>
        private static void Foretell(Map map, State st, SeasonCharacter c, int key)
        {
            var by = WeatherDynamics.BestReader(map, out float accuracy);
            if (by == null) return;
            var (kind, extreme) = ClimateMath.Guess(c, ClimateMath.SeasonAccuracy(accuracy), Rand.Value, Rand.Value);
            st.said = kind + (extreme ? "X" : "");
            st.saidBy = by;
            st.saidKey = key;
            string phrase = Phrase(kind, extreme, c.role, SeasonLabel(map, c));
            WeatherTalk.SayBy(by, "Rimfolk_Climate_Pool_SaySeason_" + kind, c.snowSeason ? "snowseason" : "rainseason", extreme ? "extreme" : null);
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Clim_Expects".Translate(by.LabelShort, phrase).Resolve(), by, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// A year's own extreme, or a hard winter, met in the region too: the world event to match (a year without a summer, the
        /// region's drought, its floods, a hard winter) is started where it can be, so the neighbours' news says what the land shows.
        /// </summary>
        private static void RegionFollows(Map map, State st, SeasonCharacter c)
        {
            if (!(S?.worldSituations ?? true) || WorldSituations.Instance == null) return;
            string def = null;
            switch (c.yearKind)
            {
                case YearKind.NoSummer: def = "Rimfolk_WS_ColdYear"; break;
                case YearKind.Drought: def = "Council_WS_Drought"; break;
                case YearKind.Floods: def = "Council_WS_Flood"; break;
            }
            if (def == null && c.extreme && c.kind == SeasonKind.Cold && c.role == 4) def = "Council_WS_HardWinter";
            if (def == null || st.Done("ws" + c.year + def)) return;
            try
            {
                var s = WorldSituations.Instance.TryStart(map, d => d.defName == def, false);
                if (s != null) st.Mark("ws" + c.year + def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: the region's world event", e);
            }
        }

        // ---------------- Extremes told ----------------

        /// <summary>
        /// A season's or a year's extreme told once the land shows it: a warm or cold one a day in, when the temperature has
        /// come most of the way; a wet, dry, snowy or snowless one from its fifth day, when the days so far agree.
        /// </summary>
        private static void Extremes(Map map, WeatherBook book, State st, SeasonCharacter c, int year, int q, int hour, int now)
        {
            int key = year * 4 + q;
            var cur = book.Current;
            if (cur == null || cur.year != year || cur.quadrum != q) return;
            // The year's own extreme, once a year, in the first season it shows in.
            if (c.yearKind != YearKind.Ordinary && !st.Done("y" + year))
            {
                bool shows = c.yearKind == YearKind.NoSummer
                    ? c.role == 2 && hour >= 24 && c.temp < 0f && SeasonMoved(map, st, c) >= 0.6f
                    : hour >= 120 && Agrees(c.yearKind == YearKind.Drought ? SeasonKind.Dry : SeasonKind.Wet, cur, hour);
                if (shows)
                {
                    st.Mark("y" + year);
                    st.Mark("x" + key);
                    string text = c.yearKind == YearKind.NoSummer
                        ? ("CC_Clim_Told_" + c.yearKind).Translate(SeasonLabel(map, c)).Resolve()
                        : ("CC_Clim_Told_" + c.yearKind).Translate(SeasonLabel(map, c), c.yearKind == YearKind.Drought ? cur.dryDays : cur.wetDays, cur.days).Resolve();
                    Told(map, st, year, q, "Year:" + c.yearKind, text, ("CC_Clim_Chron_" + c.yearKind).Translate(SeasonLabel(map, c)).Resolve(), ChronicleKind.Note,
                        new ClimateExtreme { scope = ExtremeScope.Year, yearKind = c.yearKind, seasonKind = c.kind, year = year, quadrum = q, temp = c.temp, label = YearLabel(c.yearKind) });
                    return;
                }
            }
            if (!c.extreme || c.yearKind != YearKind.Ordinary || st.Done("x" + key)) return;
            bool show;
            string told;
            string season = SeasonLabel(map, c);
            switch (c.kind)
            {
                case SeasonKind.Warm:
                case SeasonKind.Cold:
                    show = hour >= 24 && Mathf.Abs(c.temp) > 0.5f && SeasonMoved(map, st, c) >= 0.6f;
                    told = ClimateMath.PhraseKey(c.kind, true, c.role).Replace("CC_Clim_Season_", "CC_Clim_Told_").Translate(season).Resolve();
                    break;
                default:
                    show = hour >= 120 && hour < 300 && Agrees(c.kind, cur, hour);
                    int n = c.kind == SeasonKind.Wet ? cur.wetDays : c.kind == SeasonKind.Dry ? cur.dryDays : cur.snowDays;
                    told = ("CC_Clim_Told_" + c.kind + "X").Translate(season, n, cur.days).Resolve();
                    break;
            }
            if (!show) return;
            st.Mark("x" + key);
            string phrase = Phrase(c.kind, true, c.role, season);
            Told(map, st, year, q, "Season:" + c.kind, told, "CC_Clim_Chron_Season".Translate(phrase.CapitalizeFirst()).Resolve(), ChronicleKind.Note,
                new ClimateExtreme { scope = ExtremeScope.Season, seasonKind = c.kind, year = year, quadrum = q, temp = c.temp, label = phrase });
        }

        /// <summary>How much of the season's own push on the temperature has come so far (0 to 1, past the drift and the region's world events).</summary>
        private static float SeasonMoved(Map map, State st, SeasonCharacter c)
        {
            if (Mathf.Abs(c.temp) < 0.01f) return 0f;
            float moved = st.climate - DriftNow(map).temp - WorldTemp(map, c);
            return moved / c.temp;
        }

        /// <summary>The season's days so far agree with its kind: mostly wet, nothing falling on nearly every day, snow on several, or none yet.</summary>
        private static bool Agrees(SeasonKind kind, WeatherQuarter cur, int hour)
        {
            if (cur.days < 4) return false;
            switch (kind)
            {
                case SeasonKind.Wet: return cur.wetDays >= 3 && cur.wetDays * 2 >= cur.days;
                case SeasonKind.Dry: return cur.dryDays >= cur.days - 1;
                case SeasonKind.Snowy: return cur.snowDays >= 3 && cur.snowDays * 2 >= cur.days;
                case SeasonKind.Snowless: return cur.snowDays == 0 && hour >= 168;
                default: return false;
            }
        }

        private static void Told(Map map, State st, int year, int q, string what, string text, string chronicle, ChronicleKind kind, ClimateExtreme x)
        {
            st.Came(year, q, what);
            Tell.Minor(NewsKind.Land, Importance.Notable, map, text, null, MessageTypeDefOf.NeutralEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(kind, chronicle));
            try
            {
                ExtremeBegan?.Invoke(map, x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: an extreme began (a listener)", e);
            }
        }

        private static void Ended(Map map, ClimateExtreme x)
        {
            try
            {
                ExtremeEnded?.Invoke(map, x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: an extreme ended (a listener)", e);
            }
        }

        // ---------------- Once in a lifetime ----------------

        private static bool RareRunning(State st, SeasonCharacter c, int year, int q, int hour) =>
            c.RareAt(hour) && !st.Done("rx" + (year * 4 + q));

        /// <summary>Once in a lifetime going on now (or one set going by hand), else None.</summary>
        private static RareKind RareNowKind(State st, SeasonCharacter c, int year, int q, int hour)
        {
            if (st.devRare != RareKind.None && Now < st.devRareUntil) return st.devRare;
            return RareRunning(st, c, year, q, hour) ? c.rare : RareKind.None;
        }

        private static ClimateExtreme RareExtreme(Map map, RareKind k, int year, int q, float temp) =>
            new ClimateExtreme { scope = ExtremeScope.Rare, rareKind = k, year = year, quadrum = q, temp = temp, weather = RareWeather(k, year)?.defName, label = ("CC_Clim_RareLabel_" + k).Translate().Resolve() };

        /// <summary>The weather once in a lifetime brings: the first of its kind the game has (a snow gentle or hard by the year).</summary>
        private static WeatherDef RareWeather(RareKind k, int year)
        {
            var defs = ClimateMath.RareDefs(k);
            if (k == RareKind.Snow && year % 3 == 0) defs = defs.Reverse().ToArray();
            foreach (var d in defs)
            {
                var w = Weather(d);
                if (w != null) return w;
            }
            return null;
        }

        /// <summary>
        /// Once in a lifetime's hour: a great storm only within the shared harm budget; its weather brought while it lasts and
        /// the temperature allows; told when it shows (its weather begun, or the cold or heat most of the way in), with the
        /// chronicle, the colony's book (a believer's word on whose sign it is), and the event; and its end.
        /// </summary>
        private static void RareHour(Map map, State st, SeasonCharacter c, int year, int q, int hour, int now)
        {
            bool dev = st.devRare != RareKind.None;
            if (dev && now >= st.devRareUntil)
            {
                Ended(map, RareExtreme(map, st.devRare, year, q, st.devRareTemp));
                st.devRare = RareKind.None;
                return;
            }
            RareKind kind = dev ? st.devRare : c.rare;
            float temp = dev ? st.devRareTemp : c.rareTemp;
            int key = year * 4 + q;
            if (!dev)
            {
                if (c.rare == RareKind.None) return;
                if (hour >= c.rareStart + c.rareHours)
                {
                    if (st.Done("r" + key) && !st.Done("re" + key))
                    {
                        st.Mark("re" + key);
                        Ended(map, RareExtreme(map, kind, year, q, temp));
                    }
                    return;
                }
                if (!c.RareAt(hour) || st.Done("rx" + key)) return;
                if (ClimateMath.RareHarms(kind) && !st.Done("rg" + key))
                {
                    if (!Harm.On || !Harm.SystemOn(HarmKinds.Dangers.system) || !Harm.Roll(HarmKinds.Dangers, 1f))
                    {
                        st.Mark("rx" + key);
                        return;
                    }
                    st.Mark("rg" + key);
                    HarmBook.Instance?.Came(HarmKinds.Dangers, now);
                }
            }
            var w = ClimateMath.RareHasWeather(kind) ? RareWeather(kind, year) : null;
            // Its weather is what the words speak of: without it, nothing is told.
            if (w == null && ClimateMath.RareHasWeather(kind)) return;
            int left = dev ? (st.devRareUntil - now) / GenDate.TicksPerHour : c.rareStart + c.rareHours - hour;
            bool shows;
            if (w != null)
            {
                if (map.weatherManager.curWeather != w) Bring(map, w, Mathf.Max(3, left) * GenDate.TicksPerHour);
                shows = map.weatherManager.curWeather == w;
            }
            // A great cold or heat is told once most of it has come (a frost once the nights freeze).
            else
            {
                float moved = st.climate - (c.temp + DriftNow(map).temp + WorldTemp(map, c));
                shows = Mathf.Abs(temp) > 0.5f && moved / temp >= (kind == RareKind.Frost ? 0.85f : 0.6f);
            }
            string told = dev ? "rdev" + st.devRareUntil : "r" + key;
            if (!shows || st.Done(told)) return;
            st.Mark(told);
            var x = RareExtreme(map, kind, year, q, temp);
            Told(map, st, year, q, "Rare:" + kind, ("CC_Clim_Told_Rare_" + kind).Translate().Resolve(), ("CC_Clim_Chron_Rare_" + kind).Translate().Resolve(), ChronicleKind.World, x);
            SignOfGods(map, kind, x.label, now);
        }

        /// <summary>The god people take once in a lifetime for (a believer's word, the colony's book): their own reading, maybe wrong.</summary>
        private static readonly Dictionary<RareKind, string> RareGod = new Dictionary<RareKind, string>
        {
            [RareKind.Snow] = "Hush", [RareKind.Frost] = "Hush", [RareKind.Blizzard] = "Hush", [RareKind.Heat] = "Helios", [RareKind.Dust] = "Helios",
            [RareKind.Deluge] = "Murmur", [RareKind.GreatStorm] = "Cirrus"
        };

        private static void SignOfGods(Map map, RareKind kind, string label, int now)
        {
            if (!Gods.On || !RareGod.TryGetValue(kind, out string key)) return;
            try
            {
                var lore = GodLore.Instance;
                var god = Gods.ByKey(key);
                if (lore == null || god == null) return;
                int home = Homes.KeyOf(map);
                int lastGod = lore.lastHand.TryGetValue(key, out int lg) ? lg : -1;
                int lastAny = lore.lastHandAt.TryGetValue(home, out int la) ? la : -1;
                if (!GodHandsMath.Remarked(lastGod, lastAny, now, 1f, 0f, GenDate.TicksPerDay)) return;
                GodHands.Show(map, god, HandKind.Sign, label, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: once in a lifetime and the gods", e);
            }
        }

        /// <summary>
        /// A weather brought now for this long (ticks): only where the land can take it (no condition or someone on the map
        /// forcing the weather, the temperature in its range), as the gods' weather answers bring theirs; a forecast is let go.
        /// </summary>
        private static bool Bring(Map map, WeatherDef w, int ticks)
        {
            if (w == null || map.weatherDecider == null || map.weatherDecider.ForcedWeather != null || TutorSystem.TutorialMode || WeatherDynamics.WeatherMakerOn(map)) return false;
            if (!w.temperatureRange.Includes(map.mapTemperature.OutdoorTemp)) return false;
            try
            {
                WeatherDynamics.ForgetForecast(map);
                map.weatherManager.TransitionTo(w);
                WeatherDynamics.SetDuration(map, Mathf.Max(GenDate.TicksPerHour, ticks));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("climate: a weather brought", e);
                return false;
            }
        }

        // ---------------- The weathers no biome has ----------------

        /// <summary>
        /// The season's weather no biome has, at its hour: brought once (fertile rain as its own condition, so the plants grow
        /// faster as its words say), with a word from someone in view; red rain as a sign, which some take for one.
        /// </summary>
        private static void Special(Map map, State st, SeasonCharacter c, int year, int q, int hour)
        {
            int key = year * 4 + q;
            if (!c.SpecialAt(hour) || st.Done("s" + key)) return;
            if (c.special == SpecialKind.FertileRain)
            {
                var cond = DefDatabase<GameConditionDef>.GetNamedSilentFail("VEE_FertileRainsGameCondition");
                if (cond == null || map.gameConditionManager.ConditionIsActive(cond) || map.weatherDecider.ForcedWeather != null) return;
                if (map.mapTemperature.OutdoorTemp < 2f) return;
                st.Mark("s" + key);
                try
                {
                    map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(cond, c.specialHours * GenDate.TicksPerHour));
                    Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Clim_FertileRain".Translate().Resolve(), null, MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("climate: fertile rain", e);
                }
                return;
            }
            var w = SpecialWeather(c.special, year);
            if (w == null || !Bring(map, w, c.specialHours * GenDate.TicksPerHour)) return;
            st.Mark("s" + key);
            if (c.special == SpecialKind.RedRain)
                Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Clim_Sign".Translate().Resolve(), null, MessageTypeDefOf.NeutralEvent);
            WeatherTalk.SayOne(map, "Rimfolk_Climate_Pool_SayOddWeather");
        }

        private static WeatherDef SpecialWeather(SpecialKind k, int year)
        {
            var defs = ClimateMath.SpecialDefs.Where(d => d.kind == k && !d.condition).Select(d => d.def).ToList();
            if (defs.Count > 1 && year % 2 == 1) defs.Reverse();
            foreach (var d in defs)
            {
                var w = Weather(d);
                if (w != null) return w;
            }
            return null;
        }

        /// <summary>
        /// The region's world events on the land's weather, once each, a day or more in: the region's drought brings a day of
        /// dust where it's warm and the land is not wet; a hard winter a whiteout or fog snow where it's cold enough.
        /// </summary>
        private static void World(Map map, State st, SeasonCharacter c, int now)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || (s.ctx?.map != null && s.ctx.map != map) || now - s.startTick < GenDate.TicksPerDay || st.Done("w" + s.id)) continue;
                // A few hours of each day it might come in, by the situation.
                if ((now / GenDate.TicksPerHour + s.id * 7) % 24 != 9) continue;
                WeatherDef w = null;
                float temp = map.mapTemperature.OutdoorTemp;
                switch (s.def.defName)
                {
                    case "Council_WS_Drought":
                        if (temp >= 12f && !Land(map, st).b.Humid) w = SpecialWeather(SpecialKind.Dust, s.id);
                        break;
                    case "Council_WS_HardWinter":
                        if (temp <= -2f) w = SpecialWeather(SpecialKind.Whiteout, s.id) ?? SpecialWeather(SpecialKind.FogSnow, s.id);
                        break;
                    default:
                        continue;
                }
                st.Mark("w" + s.id);
                if (w != null && Bring(map, w, (8 + s.id % 10) * GenDate.TicksPerHour)) WeatherTalk.SayOne(map, "Rimfolk_Climate_Pool_SayOddWeather");
            }
        }

        /// <summary>
        /// A volcanic winter on the map: the climate cooled for a few years (the drift's push), a few hours of ash in the air
        /// (Alpha Biomes), and acid rain from the same ash now and then, only within the shared harm budget, as Alpha Biomes'
        /// own event with its own letter.
        /// </summary>
        private static void Volcanic(Map map, State st, int now)
        {
            var v = Active(map, GameConditionDefOf.VolcanicWinter);
            if (v == null) return;
            string key = "v" + v.uniqueID;
            if (!st.Done(key))
            {
                st.Mark(key);
                ClimateDrift.Instance?.AddPush("volcanic", v.startTick, 1f);
            }
            if (now - v.startTick >= GenDate.TicksPerDay && !st.Done(key + "a"))
            {
                st.Mark(key + "a");
                var ash = Weather("AB_VolcanicAsh");
                if (ash != null) Bring(map, ash, 10 * GenDate.TicksPerHour);
            }
            if (now - v.startTick >= GenDate.TicksPerDay * 3 && !st.Done(key + "r"))
            {
                st.Mark(key + "r");
                var acid = DefDatabase<IncidentDef>.GetNamedSilentFail("AB_AcidRain");
                if (acid == null || !Harm.On || !Harm.SystemOn(HarmKinds.Dangers.system) || !Harm.Roll(HarmKinds.Dangers, 0.5f)) return;
                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(acid.category, map);
                    if (acid.Worker.CanFireNow(parms) && acid.Worker.TryExecute(parms)) HarmBook.Instance?.Came(HarmKinds.Dangers, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("climate: acid rain after the volcanic winter", e);
                }
            }
        }

        /// <summary>A comet over the land (Rimfolk's, Vanilla Gravship Expanded's, or the region's world event): the climate set moving for a few years.</summary>
        private static void Comet(Map map, State st, int now)
        {
            GameCondition comet = CouncilDefOf.Council_Comet != null ? Active(map, CouncilDefOf.Council_Comet) : null;
            comet ??= Active(map, DefDatabase<GameConditionDef>.GetNamedSilentFail("VGE_Comet"));
            int id = comet?.uniqueID ?? -1, start = comet?.startTick ?? -1;
            if (comet == null)
            {
                var s = WorldSituations.Instance?.Running.FirstOrDefault(x => x?.def?.defName == "Council_WS_Comet" && (x.ctx?.map == null || x.ctx.map == map));
                if (s == null) return;
                id = 100000 + s.id;
                start = s.startTick;
            }
            if (st.Done("c" + id)) return;
            st.Mark("c" + id);
            ClimateDrift.Instance?.AddPush("comet", start, start / GenDate.TicksPerHour % 2 == 0 ? 1f : -1f);
        }

        private static GameCondition Active(Map map, GameConditionDef def)
        {
            if (def == null) return null;
            var c = map.gameConditionManager.GetActiveCondition(def);
            if (c != null) return c;
            var w = Find.World?.gameConditionManager?.GetActiveCondition(def);
            return w != null && w.CanApplyOnMap(map) ? w : null;
        }

        // ---------------- The elders remark ----------------

        /// <summary>
        /// Early in a season, the colony's elder (fifty or older, or three years with the colony) says what the record shows of
        /// it over the years, if it shows something: milder winters, hotter summers, a longer growing season, the snow later.
        /// </summary>
        private static void Elder(Map map, State st, SeasonCharacter c)
        {
            st.elderDue = false;
            var trends = ClimateMath.Trends(Years(st)).Where(t => ClimateMath.TrendFits(t.kind, c.role)).ToList();
            if (trends.Count == 0) return;
            var elder = ColonyCalendar.AtHome(map).Where(p => !p.DevelopmentalStage.Baby() && !p.DevelopmentalStage.Child()
                    && (Flavor.HumanAge(p) >= 50f || (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) >= GenDate.TicksPerYear * 3))
                .OrderByDescending(p => p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0).FirstOrDefault();
            if (elder == null) return;
            WeatherTalk.SayBy(elder, "Rimfolk_Climate_Pool_SayTrend", "trend:" + trends[0].kind);
        }

        // ---------------- The land's record ----------------

        private static List<ClimateYear> Years(State st) => st.years.Select(ClimateYear.Unpack).Where(y => y != null).ToList();

        /// <summary>A season of the record ended (WeatherBook.EndQuarter): the last of the year writes the year into the land's record.</summary>
        public static void QuarterEnded(Map map, WeatherBook book, WeatherQuarter q, int firstSnowDoy)
        {
            var st = book.clim;
            if (q == null || q.quadrum != 3) return;
            var y = ClimateYear.Of(book.Quarters, q.year, firstSnowDoy);
            if (y == null) return;
            st.years.RemoveAll(s => ClimateYear.Unpack(s)?.year == y.year);
            st.years.Add(y.Pack());
            while (st.years.Count > ClimateMath.RecordYears) st.years.RemoveAt(0);
        }

        /// <summary>
        /// The year's climate for the annals: its own extreme, its seasons that had a character (and showed it), once in a
        /// lifetime, and what the record shows since the colony came. Null when there's nothing to say.
        /// </summary>
        public static string AnnalsPart(Map map, WeatherBook book, int year)
        {
            var st = book.clim;
            var parts = new List<string>();
            var came = st.came.Select(s => s.Split(':')).Where(p => p.Length >= 4 && int.TryParse(p[0], out int y) && y == year).ToList();
            foreach (var p in came.Where(p => p[2] == "Year").Select(p => p[3]).Distinct())
                parts.Add(("CC_Clim_Annals_" + p).Translate().Resolve());
            // The seasons with a character, as the record saw them; those remembered by a name of their own are told by it.
            var said = new List<string>();
            if (On)
                foreach (var q in book.Quarters.Where(x => x.year == year && x.days >= 10 && x.name == null).OrderBy(x => x.quadrum))
                {
                    var c = SeasonAt(map, year, q.quadrum);
                    if (c.kind == SeasonKind.Ordinary || !Showed(c, q)) continue;
                    said.Add("CC_Clim_Annals_SeasonWas".Translate(SeasonLabel(map, c), ClimateMath.WasKey(c.kind, c.extreme, c.role).Translate().Resolve()).Resolve());
                }
            if (said.Count > 0) parts.Add("CC_Clim_Annals_Seasons".Translate(TextUtil.JoinAnd(said)).Resolve().CapitalizeFirst());
            foreach (var p in came.Where(p => p[2] == "Rare"))
            {
                int qd = int.TryParse(p[1], out int v) ? v : 0;
                parts.Add(("CC_Clim_Annals_Rare_" + p[3]).Translate(SeasonLabel(map, SeasonAt(map, year, qd))).Resolve());
            }
            var trend = ClimateMath.Trends(Years(st).Where(y => y.year <= year).ToList()).FirstOrDefault();
            if (trend.by > 0f) parts.Add(TrendLine(trend.kind, trend.by));
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>A season's character the record saw: warm or cold by a degree or more, wet with rain on a third of its days, dry with little, snowy with snow, snowless without.</summary>
        private static bool Showed(SeasonCharacter c, WeatherQuarter q)
        {
            switch (c.kind)
            {
                case SeasonKind.Warm: return q.Anomaly >= 1f;
                case SeasonKind.Cold: return q.Anomaly <= -1f;
                case SeasonKind.Wet: return q.wetDays * 3 >= q.days;
                case SeasonKind.Dry: return q.dryDays * 3 >= q.days * 2;
                case SeasonKind.Snowy: return q.snowDays >= 4;
                case SeasonKind.Snowless: return q.snowDays <= 1;
                default: return false;
            }
        }

        private static string TrendLine(TrendKind t, float by)
        {
            switch (t)
            {
                case TrendKind.WinterMilder:
                case TrendKind.WinterHarsher:
                case TrendKind.SummerHotter:
                case TrendKind.SummerCooler:
                    return ("CC_Clim_Trend_" + t).Translate(by.ToStringTemperatureOffset("F0")).Resolve();
                case TrendKind.GrowLonger:
                case TrendKind.GrowShorter:
                case TrendKind.SnowEarlier:
                case TrendKind.SnowLater:
                    return ("CC_Clim_Trend_" + t).Translate(Mathf.RoundToInt(by)).Resolve();
                default:
                    return ("CC_Clim_Trend_" + t).Translate().Resolve();
            }
        }

        /// <summary>The Overview's lines: the season's forecast, once in a lifetime going on, and what the record shows since the colony came.</summary>
        public static List<string> OverviewLines(Map map, WeatherBook book)
        {
            var lines = new List<string>();
            var st = book.clim;
            if (!Works(map)) return lines;
            Today(map, out int year, out int q, out int hour);
            var c = Character(map, st, year, q);
            if (st.said != null && st.saidKey == year * 4 + q && st.saidBy != null)
            {
                bool x = st.said.EndsWith("X");
                if (Enum.TryParse(x ? st.said.Substring(0, st.said.Length - 1) : st.said, out SeasonKind kind))
                    lines.Add("CC_Clim_OverviewSaid".Translate(st.saidBy.LabelShort, Phrase(kind, x, c.role, SeasonLabel(map, c))).Resolve());
            }
            var rare = RareNowKind(st, c, year, q, hour);
            if (rare != RareKind.None && (st.Done("r" + (year * 4 + q)) || st.devRare != RareKind.None))
                lines.Add("CC_Clim_OverviewRare".Translate(("CC_Clim_RareLabel_" + rare).Translate()).Resolve().CapitalizeFirst());
            var trend = ClimateMath.Trends(Years(st)).FirstOrDefault();
            if (trend.by > 0f) lines.Add(TrendLine(trend.kind, trend.by));
            return lines;
        }

        // ---------------- Words ----------------

        /// <summary>A season's name as the colony says it: the game's season, or the quadrum where the season is permanent.</summary>
        public static string SeasonLabel(Map map, SeasonCharacter c) =>
            c.season >= 1 && c.season <= 4 ? ((Season)c.season).Label() : ((Quadrum)c.quadrum).Label();

        /// <summary>"a wet summer", "a hard winter", "an ordinary Jugust".</summary>
        public static string Phrase(SeasonKind kind, bool extreme, int role, string season) => ClimateMath.PhraseKey(kind, extreme, role).Translate(season).Resolve();

        private static string YearLabel(YearKind k) => ("CC_Clim_YearLabel_" + k).Translate().Resolve();

        // ---------------- Dev ----------------

        /// <summary>Dev: the next seasons on this map, as drawn (kind, lean, the year's extreme, once in a lifetime, the weather no biome has).</summary>
        internal static string DevSeasons(Map map, int count)
        {
            var st = StateOf(map);
            if (st == null) return "no weather record here.";
            Today(map, out int year, out int q, out _);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int y = year + (q + i) / 4, qq = (q + i) % 4;
                var l = Land(map, st);
                var c = ClimateMath.SeasonOf(Seed(map), y, qq, SeasonOfQuadrum(map, qq), SeasonOfQuadrum(map, (qq + 1) % 4), l, Strength);
                sb.Append(y).Append(' ').Append(((Quadrum)qq).Label()).Append(": ").Append(c.kind).Append(c.extreme ? " (extreme)" : "")
                    .Append(", ").Append(c.temp.ToString("+0.0;-0.0")).Append(" °C, wet ").Append(c.wet.ToString("+0.00;-0.00")).Append(", snow ").Append(c.snow.ToString("+0.00;-0.00"));
                if (c.yearKind != YearKind.Ordinary) sb.Append(", year: ").Append(c.yearKind);
                if (c.rare != RareKind.None) sb.Append(", once in a lifetime: ").Append(c.rare).Append(" from hour ").Append(c.rareStart).Append(" for ").Append(c.rareHours).Append(", ").Append(c.rareTemp.ToString("+0;-0")).Append(" °C");
                if (c.special != SpecialKind.None) sb.Append(c.sign ? ", sign: " : ", weather: ").Append(c.special).Append(" at hour ").Append(c.specialStart);
                sb.AppendLine();
            }
            var d = DriftNow(map);
            sb.Append("Drift now: ").Append(d.temp.ToString("+0.00;-0.00")).Append(" °C, wet ").Append(d.wet.ToString("+0.00;-0.00")).Append("; the climate's push now ").Append(st.climate.ToString("+0.0;-0.0")).Append(" °C.");
            return sb.ToString();
        }

        /// <summary>Dev: once in a lifetime of this kind set going now on this map for a day and a half, with its temperature as this land would have it (or a fair guess).</summary>
        internal static string DevRare(Map map, RareKind kind)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return "the land's climate isn't working here (off, or not a home map with weather).";
            Today(map, out _, out int q, out _);
            var cand = ClimateMath.RareCandidates(Land(map, st), q, ClimateStrength.Dramatic).FirstOrDefault(x => x.kind == kind);
            float temp = cand.kind == kind ? cand.temp : kind == RareKind.Heat ? 12f : kind == RareKind.Snow || kind == RareKind.Frost ? -15f : kind == RareKind.Blizzard ? -8f : kind == RareKind.Dust ? 2f : -2f;
            st.devRare = kind;
            st.devRareTemp = temp;
            st.devRareUntil = Now + GenDate.TicksPerDay * 3 / 2;
            return kind + " for a day and a half, " + temp.ToString("+0;-0") + " °C (eased in over about half a day; " + (cand.kind == kind ? "as this land would have it" : "this land wouldn't have it, so a guess") + ").";
        }

        /// <summary>Dev: the season's forecast said again now.</summary>
        internal static string DevForecast(Map map)
        {
            var st = StateOf(map);
            if (st == null || !Works(map)) return "the land's climate isn't working here.";
            Today(map, out int year, out int q, out _);
            var c = Character(map, st, year, q);
            Foretell(map, st, c, year * 4 + q);
            return st.saidBy == null ? "nobody here reads the weather well enough (Plants 6, or 3 and fifty or older)." : st.saidBy.LabelShort + " said " + st.said + "; it is " + c.kind + (c.extreme ? " (extreme)." : ".");
        }
    }
}
