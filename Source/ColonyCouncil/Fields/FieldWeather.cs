using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Fields by the weather, the player's request: sowing that pays by the forecast, and a frost harvest offered with
    /// its numbers. The forecast comes from the game's own numbers for each hour ahead (the land's reckoning, which holds
    /// its seasons, its days and nights and its run of warm and cold days; each condition on the map while it lasts;
    /// Rimfolk's weather push easing out), shared by every field of the map and worked out again every other hour. The
    /// arithmetic is <see cref="FieldWeatherMath"/>.
    ///
    /// A growing zone the player sets to "By the weather" (a switch on the zone, off on each) sows only while a crop sown
    /// now is expected to bring <see cref="FieldWeatherMath.WorthShare"/> of a full harvest: growth slowed by cold and by
    /// the dark, plants lost by their own cold limits, plants ripe enough when a killing frost comes counted as harvested
    /// early at their yield then. Below it, sowing there pauses (<see cref="Patch_FieldWeatherSow"/>, which only ever
    /// holds a sowing back) and the zone says when it is expected to pay again. Every outdoor field, set or not, gets a
    /// frost harvest suggestion when the next <see cref="FieldWeatherMath.FrostHours"/> hours would kill plants ripe
    /// enough to harvest: one click marks exactly those, Undo takes the marks off, and after the frost a check says
    /// whether it came and how many were saved. It goes through the shared suggestion core (source "fields"), shows on
    /// the Making page's Ahead section, on the field itself and in the alerts, with a message when it first appears.
    /// </summary>
    public class FieldWeather : MapComponent
    {
        public const string Source = "fields";
        private const int RefreshTicks = 2 * GenDate.TicksPerHour;
        /// <summary>No plant's limit is near this warm: a forecast that stays above it needs no plant looked at.</summary>
        private const float NoFrostAbove = 15f;

        /// <summary>The growing zones set to sow by the weather, by zone ID (saved).</summary>
        private List<int> byWeather = new List<int>();
        /// <summary>How much colder than the land's own reckoning each of the last days got at its coldest (saved, <see cref="FieldWeatherMath.StrayDays"/> at most).</summary>
        private List<float> strays = new List<float>();

        [Unsaved] private float dayStray;
        [Unsaved] private int strayDay = -1;
        [Unsaved] private int refreshedTick = -999999;
        [Unsaved] private bool dirty = true, started, refreshSoon;
        [Unsaved] private Hours last;
        [Unsaved] private readonly Dictionary<int, Outlook> outlooks = new Dictionary<int, Outlook>();
        [Unsaved] private List<FrostStep> frost = new List<FrostStep>();
        [Unsaved] private readonly Dictionary<int, string> youngOnly = new Dictionary<int, string>();
        [Unsaved] private readonly HashSet<string> told = new HashSet<string>();
        [Unsaved] private readonly Dictionary<int, (int tick, bool outdoor)> outdoorSeen = new Dictionary<int, (int, bool)>();
        [Unsaved] private List<FrostStep> openKept;
        [Unsaved] private int openVersion = -1, openTick = -1;
        [Unsaved] private int keptAbs;
        /// <summary>
        /// The forecast can be wrong (harm, part 5, 4.4.2; hurts nobody, outside the budget): a field set to sow by the
        /// weather, by zone ID, the last tick its outlook said a crop sown now would pay; and the frosts that took such a
        /// field all the same, colder than the forecast had it (the tick and how cold), for the field's note a few days.
        /// </summary>
        [Unsaved] private readonly Dictionary<int, int> promised = new Dictionary<int, int>();
        [Unsaved] private readonly Dictionary<int, (int tick, float temp)> missed = new Dictionary<int, (int, float)>();
        [Unsaved] private float[] keptBase, keptGlow;
        [Unsaved] private bool[] keptRest;

        /// <summary>The zones whose sowing is paused now, on every map: the sowing hook's one look.</summary>
        private static readonly HashSet<Zone> paused = new HashSet<Zone>();
        private static Game pausedGame;
        private static bool gapTold;

        /// <summary>A frost harvest step waits for its check somewhere: harvested plants are looked for.</summary>
        public static bool Watching { get; private set; }

        public FieldWeather(Map map) : base(map)
        {
        }

        public static FieldWeather Of(Map map) => map?.GetComponent<FieldWeather>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The "By the weather" switch on the fields and the sowing it pauses (a deeper game hook).</summary>
        public static bool SowOn => RimfolkPatches.On && (S?.fieldsByWeather ?? true);

        /// <summary>The frost harvest suggestion.</summary>
        public static bool FrostOn => S?.frostHarvestSuggest ?? true;
        /// <summary>The frost harvest done by itself rather than suggested (the player's request; Undo leaves that frost's field to them).</summary>
        public static bool FrostAuto => FrostOn && (S?.frostHarvestAuto ?? true);

        /// <summary>A field set to sow by the weather, and what its sowing is now (not saved).</summary>
        public sealed class Outlook
        {
            public ThingDef crop;
            public FieldOutlook math;
            public string line;
        }

        /// <summary>A frost harvest for one crop of one field: the plants, the pick, and the step.</summary>
        public sealed class FrostStep
        {
            public Zone_Growing zone;
            public ThingDef crop;
            public List<Plant> plants = new List<Plant>();
            public FrostPick pick;
            public Suggestion step;
        }

        /// <summary>The forecast for one map, shared by its fields.</summary>
        public sealed class Hours
        {
            public float[] temp, glow, sowLow;
            public bool[] rest;
            public float sowMargin, frostMargin, gap, stray;
            public int hourNow;
            public long localHours;
            public readonly Dictionary<ThingDef, float[]> rates = new Dictionary<ThingDef, float[]>();
            public readonly Dictionary<ThingDef, (float min, float max)> offsets = new Dictionary<ThingDef, (float, float)>();
        }

        // ---------------- The game's clock ----------------

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (!started) Start();
            if (refreshSoon) MapComponentUpdate();
            if (!Sched.Hourly(tick, Sched.Fields)) return;
            if (!map.IsPlayerHome)
            {
                if (frost.Count > 0 || outlooks.Count > 0) Clear();
                return;
            }
            using var clock = PassClock.Time(Pass.Fields);
            try
            {
                Hour(tick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fields by the weather", e);
            }
        }

        private void Hour(int tick)
        {
            NoteStray();
            Missed(tick);
            FeedChecks();
            bool sow = SowOn && byWeather.Count > 0, frostOn = FrostOn;
            if (!sow && !frostOn)
            {
                if (frost.Count > 0 || outlooks.Count > 0) Clear();
                return;
            }
            // A field whose crop changed, or that has none worked out, is worked out again now; while a frost harvest
            // stands, every hour, so its hours stay right.
            if (sow)
                foreach (int id in byWeather)
                    if (ZoneOf(id) is Zone_Growing z && z.GetPlantDefToGrow() is ThingDef crop && (!outlooks.TryGetValue(id, out var o) || o.crop != crop))
                        dirty = true;
            if (dirty || frost.Count > 0 || tick - refreshedTick >= RefreshTicks) Refresh();
        }

        /// <summary>How much colder than the land's reckoning it is this hour, kept as the day's coldest; a finished day goes to the record.</summary>
        private void NoteStray()
        {
            float dev = Find.World.tileTemperatures.OutdoorTemperatureAt(map.Tile, Find.TickManager.TicksAbs) - map.mapTemperature.OutdoorTemp;
            int day = GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);
            if (day == strayDay)
            {
                dayStray = Mathf.Max(dayStray, dev);
                return;
            }
            if (strayDay >= 0)
            {
                strays.Add(dayStray);
                SuggestMath.TrimOldest(strays, FieldWeatherMath.StrayDays);
            }
            strayDay = day;
            dayStray = dev;
        }

        /// <summary>
        /// The hour's look for a forecast that missed: a field sown by the weather that its outlook said would pay (within
        /// a season) whose crop is frosted now, the air below the plants' own cold limit where the last forecast for this
        /// hour had it at the limit and the frost margin above. The game's own frost takes the plants, as it always did;
        /// the field's note says the forecast missed.
        /// </summary>
        private void Missed(int tick)
        {
            if (!SowOn || last?.temp == null || promised.Count == 0) return;
            float actual = map.mapTemperature.OutdoorTemp;
            if (actual > NoFrostAbove) return;
            int h = (tick - refreshedTick) / GenDate.TicksPerHour;
            if (h < 0 || h >= last.temp.Length) return;
            float forecast = last.temp[h];
            foreach (var kv in promised.ToList())
            {
                if (tick - kv.Value > HarmFiveMath.PromiseKeepDays * GenDate.TicksPerDay || tick < kv.Value)
                {
                    promised.Remove(kv.Key);
                    continue;
                }
                if (missed.TryGetValue(kv.Key, out var m) && tick - m.tick < HarmFiveMath.MissedNoteDays * GenDate.TicksPerDay) continue;
                if (!(ZoneOf(kv.Key) is Zone_Growing z) || !IsByWeather(z) || !(z.GetPlantDefToGrow() is ThingDef crop)) continue;
                foreach (var c in z.Cells)
                {
                    var plant = c.GetPlant(map);
                    if (plant?.def != crop || c.Roofed(map) || !HarmFiveMath.ForecastMissed(forecast, actual, LimitOf(plant), last.frostMargin)) continue;
                    missed[kv.Key] = (tick, actual);
                    break;
                }
            }
        }

        /// <summary>Dev mode: the selected field's note says the forecast missed, as if a frost colder than reckoned came now.</summary>
        public string DebugMissed(Zone_Growing z)
        {
            if (z == null) return "no field.";
            missed[z.ID] = (Find.TickManager.TicksGame, map.mapTemperature.OutdoorTemp);
            return z.label + "'s note now says the forecast missed (for five days).";
        }

        /// <summary>The frost checks waiting on this map hear the hour's temperature.</summary>
        private void FeedChecks()
        {
            var t = SuggestTracker.Instance;
            if (t == null)
            {
                Watching = false;
                return;
            }
            bool any = false;
            foreach (var d in t.done)
            {
                if (d.state != SuggestMath.Waiting || !(d.check is FrostHarvestCheck c)) continue;
                any = true;
                if (d.map == map) c.lowest = Mathf.Min(c.lowest, map.mapTemperature.OutdoorTemp);
            }
            Watching = any;
        }

        private void Clear()
        {
            outlooks.Clear();
            frost = new List<FrostStep>();
            youngOnly.Clear();
            openKept = null;
            paused.RemoveWhere(z => z == null || z.Map == null || z.Map == map);
        }

        // ---------------- Working it out ----------------

        private Zone ZoneOf(int id) => map.zoneManager.AllZones.FirstOrDefault(z => z.ID == id);

        /// <summary>Whether a field has open ground under the sky (only those are reckoned by the weather).</summary>
        public bool Outdoor(Zone_Growing z)
        {
            if (z == null) return false;
            int now = Find.TickManager.TicksGame;
            if (outdoorSeen.TryGetValue(z.ID, out var seen) && now - seen.tick < Sched.RareTicks && now >= seen.tick) return seen.outdoor;
            bool outdoor = z.Cells.Any(c => !c.Roofed(map) && c.UsesOutdoorTemperature(map));
            outdoorSeen[z.ID] = (now, outdoor);
            return outdoor;
        }

        /// <summary>Everything worked out again: the forecast, each field set to sow by the weather, the frost harvest.</summary>
        public void Refresh()
        {
            refreshedTick = Find.TickManager.TicksGame;
            dirty = false;
            openKept = null;
            var zones = map.zoneManager.AllZones.OfType<Zone_Growing>().ToList();
            byWeather.RemoveAll(id => !zones.Any(z => z.ID == id));
            var weather = SowOn ? zones.Where(z => byWeather.Contains(z.ID) && z.GetPlantDefToGrow()?.plant != null && Outdoor(z)).ToList() : new List<Zone_Growing>();
            bool frostOn = FrostOn;
            outlooks.Clear();
            youngOnly.Clear();
            paused.RemoveWhere(z => z == null || z.Map == null || z.Map == map);
            if (weather.Count == 0 && !frostOn)
            {
                frost = new List<FrostStep>();
                return;
            }
            int hours = weather.Count > 0 ? FieldWeatherMath.ForecastDays * FieldWeatherMath.DayHours : FieldWeatherMath.FrostHours + 2;
            var ahead = Forecast(hours);
            last = ahead;
            foreach (var z in weather)
            {
                var o = OutlookFor(z, ahead);
                if (o == null) continue;
                outlooks[z.ID] = o;
                if (!o.math.sow) paused.Add(z);
                else promised[z.ID] = refreshedTick;
            }
            frost = frostOn ? FrostSteps(zones, ahead) : new List<FrostStep>();
            Tell();
        }

        /// <summary>The forecast for each hour ahead from the game's own numbers, with the margins.</summary>
        private Hours Forecast(int hours)
        {
            var f = new Hours();
            int abs = Find.TickManager.TicksAbs;
            var tile = map.Tile;
            var temps = Find.World.tileTemperatures;
            float lon = Find.WorldGrid.LongLatOf(tile).x;
            // The land's reckoning, the sun and the plants' rest for a tick never change: the hours worked out at the last
            // look are kept, and only the new ones are worked out (the hourly looks fall on the same tick of the hour).
            var baseTemp = new float[hours];
            f.glow = new float[hours];
            f.rest = new bool[hours];
            int shift = keptBase != null && abs >= keptAbs && (abs - keptAbs) % GenDate.TicksPerHour == 0 ? (abs - keptAbs) / GenDate.TicksPerHour : -1;
            for (int h = 0; h < hours; h++)
            {
                int was = shift < 0 ? -1 : h + shift;
                if (was >= 0 && was < keptBase.Length)
                {
                    baseTemp[h] = keptBase[was];
                    f.glow[h] = keptGlow[was];
                    f.rest[h] = keptRest[was];
                    continue;
                }
                int t = abs + h * GenDate.TicksPerHour;
                baseTemp[h] = temps.OutdoorTemperatureAt(tile, t);
                f.glow[h] = GenCelestial.CelestialSunGlow(map, t);
                f.rest[h] = FieldWeatherMath.Resting(GenDate.DayPercent(t, lon));
            }
            if (keptBase == null || hours >= keptBase.Length || shift < 0)
            {
                keptAbs = abs;
                keptBase = baseTemp;
                keptGlow = f.glow;
                keptRest = f.rest;
            }
            // Each condition on the map (and the world's) at its offset now, for as long as it lasts.
            var offsets = new List<float>();
            var left = new List<int>();
            void Add(GameConditionManager m)
            {
                if (m == null) return;
                foreach (var c in m.ActiveConditions)
                {
                    float off = c.TemperatureOffset();
                    if (Mathf.Abs(off) < 0.01f) continue;
                    offsets.Add(off);
                    left.Add(c.Permanent ? int.MaxValue : Mathf.Max(0, c.TicksLeft) / GenDate.TicksPerHour);
                }
            }
            Add(map.gameConditionManager);
            Add(map.gameConditionManager.Parent);
            // What the game's queue holds for the map, from the hour it comes: the Stormglass Conclave's held cold or heat (Peoples/PeoplesE.cs).
            var from = offsets.Select(_ => 0).ToList();
            foreach (var (off, at, hrs) in GlassWatch.Ahead(map, hours))
            {
                offsets.Add(off);
                left.Add(hrs);
                from.Add(at);
            }
            var raw = FieldWeatherMath.Forecast(baseTemp, offsets, left, null, from);
            var push = WeatherDynamics.PushAhead(map, raw, f.glow);
            f.temp = FieldWeatherMath.Forecast(baseTemp, offsets, left, push, from);
            float actual = map.mapTemperature.OutdoorTemp;
            f.gap = FieldWeatherMath.Gap(f.temp[0], actual);
            if (f.gap > 0f && Prefs.DevMode && !gapTold)
            {
                gapTold = true;
                Log.Message("[Rimfolk] Fields by the weather: the forecast for this hour (" + f.temp[0].ToString("0.0") + " °C) is " + f.gap.ToString("0.0")
                            + " °C off the map's own (" + actual.ToString("0.0") + " °C); the margins widen by that much.");
            }
            f.stray = FieldWeatherMath.Stray(strays);
            f.sowMargin = FieldWeatherMath.SowMargin + f.stray + f.gap;
            f.frostMargin = FieldWeatherMath.FrostMargin + f.gap;
            f.sowLow = FieldWeatherMath.Lows(f.temp, f.sowMargin);
            f.hourNow = GenDate.HourOfDay(abs, lon);
            f.localHours = (abs + GenDate.LocalTicksOffsetFromLongitude(lon)) / GenDate.TicksPerHour;
            return f;
        }

        /// <summary>How fast a crop grows each hour of the forecast at full soil: warmth times light, nothing while it rests.</summary>
        private static float[] RateFor(ThingDef crop, Hours f)
        {
            if (f.rates.TryGetValue(crop, out var r)) return r;
            r = new float[f.temp.Length];
            for (int h = 0; h < r.Length; h++)
                r[h] = f.rest[h] ? 0f : PlantUtility.GrowthRateFactorFor_Temperature(crop, f.temp[h]) * PlantUtility.GrowthRateFactorFor_Light(crop, f.glow[h]);
            f.rates[crop] = r;
            return r;
        }

        private static readonly Func<Plant, float> ThreshOf = MakeThresh();

        private static Func<Plant, float> MakeThresh()
        {
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(Plant), "LeaflessTemperatureThresh");
                return getter == null ? null : AccessTools.MethodDelegate<Func<Plant, float>>(getter);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A plant's own cold limit (the game's Plant.LeaflessTemperatureThresh); the warmest of its crop's range if it can't be read.</summary>
        public static float LimitOf(Plant p)
        {
            if (ThreshOf != null)
                try
                {
                    return ThreshOf(p);
                }
                catch
                {
                    // Read as below.
                }
            return (p?.def?.plant?.minGrowthTemperature ?? 0f) + FieldWeatherMath.MaxLimitOffset;
        }

        /// <summary>The range of a crop's cold limits below its least growing temperature, from its plants on the map once enough are standing.</summary>
        private (float min, float max) OffsetsFor(ThingDef crop, Hours f)
        {
            if (f.offsets.TryGetValue(crop, out var o)) return o;
            var seen = new List<float>();
            if (ThreshOf != null)
                foreach (var t in map.listerThings.ThingsOfDef(crop))
                {
                    if (t is Plant p && p.Spawned) seen.Add(LimitOf(p) - crop.plant.minGrowthTemperature);
                    if (seen.Count >= 200) break;
                }
            o = FieldWeatherMath.Offsets(seen);
            f.offsets[crop] = o;
            return o;
        }

        private Outlook OutlookFor(Zone_Growing z, Hours f)
        {
            var crop = z.GetPlantDefToGrow();
            var pp = crop?.plant;
            if (pp == null) return null;
            float fert = 0f;
            int n = 0;
            foreach (var c in z.Cells)
            {
                if (c.Roofed(map) || !c.UsesOutdoorTemperature(map)) continue;
                fert += PlantUtility.GrowthRateFactorFor_Fertility(crop, map.fertilityGrid.FertilityAt(c));
                n++;
            }
            var (lo, hi) = OffsetsFor(crop, f);
            var run = new CropRun
            {
                growDays = pp.growDays, harvestMinGrowth = pp.harvestMinGrowth, dieIfLeafless = pp.dieIfLeafless, fertility = n > 0 ? fert / n : 1f,
                lifeHours = pp.LimitedLifespan ? pp.LifespanTicks / GenDate.TicksPerHour : 0, limits = FieldWeatherMath.Limits(pp.minGrowthTemperature, lo, hi),
                rate = RateFor(crop, f), low = f.sowLow
            };
            var o = new Outlook { crop = crop, math = FieldWeatherMath.Decide(run) };
            o.line = OutlookLine(o, crop);
            return o;
        }

        // ---------------- Words ----------------

        private static string Pct(float share) => FieldWeatherMath.Percent(share) + "%";

        /// <summary>How far ahead in words: "within a day", "in about a day", "in about 13 days".</summary>
        private static string In(int hours)
        {
            if (hours < FieldWeatherMath.DayHours) return "CC_Fields_InADay".Translate().Resolve();
            int days = FieldWeatherMath.Days(hours);
            return days == 1 ? "CC_Fields_InOneDay".Translate().Resolve() : "CC_Fields_InDays".Translate(days).Resolve();
        }

        private static string Temp(float c) => c.ToStringTemperature("F0");

        /// <summary>The field's outlook in a line for its inspect pane.</summary>
        private string OutlookLine(Outlook o, ThingDef crop)
        {
            var m = o.math;
            var now = m.now;
            if (m.sow)
            {
                string line = now.readyHour > 0
                    ? "CC_Fields_Inspect_Sow".Translate(Pct(now.share).Named("SHARE"), In(now.readyHour).Named("IN")).Resolve()
                    : "CC_Fields_Inspect_SowLate".Translate(Pct(now.share).Named("SHARE")).Resolve();
                if (!PlantUtility.GrowthSeasonNow(map, crop)) line += " " + "CC_Fields_Inspect_GameSeason".Translate(Plural(crop, 2)).Resolve();
                return line;
            }
            string why = WhyLine(now);
            string again = m.againHours > 0
                ? "CC_Fields_Inspect_Again".Translate(In(m.againHours).Named("IN"), Pct(m.againShare).Named("SHARE")).Resolve()
                : "CC_Fields_Inspect_NotAgain".Translate(FieldWeatherMath.SearchDays).Resolve();
            return "CC_Fields_Inspect_Paused".Translate(why.Named("WHY"), again.Named("AGAIN")).Resolve();
        }

        /// <summary>What costs a crop sown now the most, in words.</summary>
        private static string WhyLine(CropOutcome o)
        {
            switch (o.loss)
            {
                case FieldWeatherMath.LossFrost:
                {
                    string most = ("CC_Fields_Most_" + (o.killed >= 0.99f ? "All" : o.killed >= 0.5f ? "Most" : "Some")).Translate().Resolve();
                    return "CC_Fields_Why_Frost".Translate(In(o.frostHour).Named("IN"), Temp(o.frostLow).Named("TEMP"), most.Named("MOST"),
                        Pct(o.share).Named("SHARE")).Resolve();
                }
                case FieldWeatherMath.LossEarly:
                    return "CC_Fields_Why_Early".Translate(In(o.frostHour).Named("IN"), Temp(o.frostLow).Named("TEMP"),
                        Pct(o.earlyYield).Named("YIELD"), Pct(o.share).Named("SHARE")).Resolve();
                default:
                    return "CC_Fields_Why_Slow".Translate(Pct(o.share).Named("SHARE")).Resolve();
            }
        }

        /// <summary>An hour of the day in words: "3 at night", "7 in the morning", "noon".</summary>
        public static string HourWords(int hour)
        {
            hour = (hour % 24 + 24) % 24;
            if (hour == 0) return "CC_Fields_Hour_Midnight".Translate().Resolve();
            if (hour == 12) return "CC_Fields_Hour_Noon".Translate().Resolve();
            string part = hour < 6 || hour >= 22 ? "Night" : hour < 12 ? "Morning" : hour < 18 ? "Afternoon" : "Evening";
            return ("CC_Fields_Hour_" + part).Translate(hour > 12 ? hour - 12 : hour).Resolve();
        }

        private static string Plural(ThingDef crop, int n) => n == 1 ? crop.label : Find.ActiveLanguageWorker.Pluralize(crop.label, n);

        // ---------------- The frost harvest ----------------

        private List<FrostStep> FrostSteps(List<Zone_Growing> zones, Hours f)
        {
            var list = new List<FrostStep>();
            int n = Mathf.Min(f.temp.Length, FieldWeatherMath.FrostHours + 1);
            float lowAhead = float.MaxValue;
            for (int h = 0; h < n; h++) lowAhead = Mathf.Min(lowAhead, f.temp[h]);
            if (lowAhead >= NoFrostAbove) return list;
            var dm = map.designationManager;
            foreach (var z in zones)
            {
                var byCrop = new Dictionary<ThingDef, List<Plant>>();
                foreach (var c in z.Cells)
                {
                    if (c.Roofed(map) || !c.UsesOutdoorTemperature(map)) continue;
                    var p = c.GetPlant(map);
                    var pp = p?.def?.plant;
                    if (pp == null || !pp.Sowable || !pp.dieIfLeafless || p.Blighted) continue;
                    // Already marked to harvest or cut: it goes anyway.
                    if (dm.DesignationOn(p, DesignationDefOf.HarvestPlant) != null || dm.DesignationOn(p, DesignationDefOf.CutPlant) != null) continue;
                    if (!byCrop.TryGetValue(p.def, out var l)) byCrop[p.def] = l = new List<Plant>();
                    l.Add(p);
                }
                foreach (var kv in byCrop)
                {
                    var crop = kv.Key;
                    var plants = kv.Value;
                    var stand = plants.Select(p => new FrostPlant { limit = LimitOf(p), growth = p.Growth, harvestable = p.HarvestableNow }).ToList();
                    var pick = FieldWeatherMath.PickFrost(stand, f.temp, crop.plant.harvestMinGrowth, FieldWeatherMath.FrostHours, f.frostMargin);
                    if (pick.harvest.Count == 0)
                    {
                        if (pick.young > 0 && !youngOnly.ContainsKey(z.ID))
                            youngOnly[z.ID] = "CC_Fields_Inspect_YoungOnly".Translate(WhenWords(pick.lowHour, f).Named("WHEN"), Temp(pick.low).Named("TEMP"),
                                pick.young.Named("N"), Plural(crop, pick.young).Named("CROPS")).Resolve();
                        continue;
                    }
                    var fs = new FrostStep { zone = z, crop = crop, pick = pick, plants = pick.harvest.Select(i => plants[i]).ToList() };
                    fs.step = StepOf(fs, f);
                    list.Add(fs);
                }
            }
            return list;
        }

        private static string WhenWords(int lowHour, Hours f) => ("CC_Fields_When_" + FieldWeatherMath.FrostWhen(lowHour, f.hourNow)).Translate().Resolve();

        private Suggestion StepOf(FrostStep fs, Hours f)
        {
            var pick = fs.pick;
            var crop = fs.crop;
            var zone = fs.zone;
            int n = pick.harvest.Count;
            string when = WhenWords(pick.lowHour, f);
            string zoneName = zone.label;
            var s = new Suggestion
            {
                key = Source + ":frost:" + zone.ID + ":" + crop.defName + ":" + FieldWeatherMath.NightIndex(f.localHours + pick.lowHour),
                source = Source, area = "fields", map = map, days = Mathf.Max(0.01f, Mathf.Max(0, pick.deadlineHour) / (float)FieldWeatherMath.DayHours)
            };
            s.title = "CC_Fields_Frost_Title".Translate(when.Named("WHEN"), Temp(pick.low).Named("TEMP"), HourWords(f.hourNow + pick.lowHour).Named("HOUR"),
                n.Named("N"), pick.all.Named("ALL"), Plural(crop, pick.all).Named("CROPS"), zoneName.Named("ZONE"), Pct(pick.yieldShare).Named("YIELD")).Resolve();
            var pp = crop.plant;
            var (lo, hi) = OffsetsFor(crop, f);
            var parts = new List<string>
            {
                "CC_Fields_Frost_Why".Translate(Plural(crop, 2).Named("CROPS"), Temp(pp.minGrowthTemperature + hi).Named("WARM"), Temp(pp.minGrowthTemperature + lo).Named("COLD"),
                    Temp(pp.minGrowthTemperature).Named("MIN"), f.frostMargin.ToStringTemperatureOffset("F0").Named("MARGIN")).Resolve()
            };
            if (pick.young > 0) parts.Add("CC_Fields_Frost_Young".Translate(pick.young).Resolve());
            if (pick.safe > 0) parts.Add("CC_Fields_Frost_Safe".Translate(pick.safe).Resolve());
            s.reason = string.Join(" ", parts);
            s.pill = pick.deadlineHour <= 0 ? "CC_Fields_Pill_Now".Translate().Resolve() : "CC_Fields_Pill_Hours".Translate(pick.deadlineHour).Resolve();
            s.doLabel = "CC_Fields_Frost_Do".Translate(n).Resolve();
            s.doTip = "CC_Fields_Frost_DoTip".Translate(n).Resolve();
            var plants = fs.plants;
            s.look = new LookTargets(plants.Cast<Thing>());
            s.cells = () => plants.Where(p => p.Spawned).Select(p => p.Position).ToList();
            float expected = pick.low;
            int waitHours = Mathf.Max(2, pick.lowHour + 4);
            string done = "CC_Fields_Frost_Done".Translate(n.Named("N"), Plural(crop, n).Named("CROPS"), zoneName.Named("ZONE")).Resolve();
            var m = map;
            s.apply = () =>
            {
                var undo = new UndoOrders();
                var dm = m.designationManager;
                float warmest = float.MinValue;
                foreach (var p in plants)
                {
                    if (p == null || !p.Spawned || p.Map != m || !p.HarvestableNow) continue;
                    if (dm.DesignationOn(p, DesignationDefOf.HarvestPlant) != null || dm.DesignationOn(p, DesignationDefOf.CutPlant) != null) continue;
                    dm.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
                    undo.marked.Add(p);
                    undo.markedDefs.Add(DesignationDefOf.HarvestPlant.defName);
                    warmest = Mathf.Max(warmest, LimitOf(p));
                }
                if (undo.Count == 0) return null;
                Watching = true;
                return new SuggestDone
                {
                    undo = undo, text = done,
                    check = new FrostHarvestCheck { waitTicks = waitHours * GenDate.TicksPerHour, expected = expected, warmest = warmest, marked = undo.marked.Count }
                };
            };
            return s;
        }

        /// <summary>
        /// A frost harvest is done by itself when that's on: the plants marked at once and the message says so (unless the
        /// player undid it for that frost, which leaves it to them). Otherwise, or when nothing could be marked, it is told
        /// once in a message as a suggestion (the alert and the field keep it in view).
        /// </summary>
        private void Tell()
        {
            var t = SuggestTracker.Instance;
            bool auto = FrostAuto;
            foreach (var fs in frost)
            {
                string key = fs.step.key;
                if (t == null || !t.Offered(key, map)) continue;
                int n = fs.pick.harvest.Count;
                if (auto && !t.UndoneLast(key, map) && Markable(fs) && t.Apply(fs.step, quiet: true) != null)
                {
                    told.Add(key);
                    Notify.Message("CC_Fields_Frost_Auto".Translate(WhenWords(fs.pick.lowHour, last).Named("WHEN"), Temp(fs.pick.low).Named("TEMP"), n.Named("N"),
                        Plural(fs.crop, n).Named("CROPS"), fs.zone.label.Named("ZONE")).Resolve(), fs.step.look, MessageTypeDefOf.TaskCompletion, Importance.Important);
                    continue;
                }
                if (!told.Add(key)) continue;
                Notify.Message("CC_Fields_Frost_Message".Translate(WhenWords(fs.pick.lowHour, last).Named("WHEN"), Temp(fs.pick.low).Named("TEMP"), n.Named("N"),
                    Plural(fs.crop, n).Named("CROPS"), fs.zone.label.Named("ZONE")).Resolve(), fs.step.look, MessageTypeDefOf.NegativeEvent, Importance.Important);
            }
            if (told.Count > 200) told.Clear();
        }

        /// <summary>At least one of the step's plants can still be marked (there, harvestable, not marked to harvest or cut).</summary>
        private bool Markable(FrostStep fs)
        {
            var dm = map.designationManager;
            return fs.plants.Any(p => p != null && p.Spawned && p.Map == map && p.HarvestableNow
                                      && dm.DesignationOn(p, DesignationDefOf.HarvestPlant) == null && dm.DesignationOn(p, DesignationDefOf.CutPlant) == null);
        }

        // ---------------- What the pages, the field and the hooks ask ----------------

        public bool IsByWeather(Zone_Growing z) => z != null && byWeather.Contains(z.ID);

        /// <summary>Sets a field to sow by the weather or not; its outlook is worked out in the next frame.</summary>
        public void Set(Zone_Growing z, bool on)
        {
            if (z == null) return;
            byWeather.Remove(z.ID);
            if (on) byWeather.Add(z.ID);
            dirty = true;
            refreshSoon = true;
        }

        /// <summary>Once after a load (or a new map): the fields set to sow by the weather are worked out at once, so none sows in the meantime.</summary>
        private void Start()
        {
            started = true;
            if (map.IsPlayerHome && SowOn && byWeather.Count > 0) refreshSoon = true;
        }

        /// <summary>Each frame: fields just switched (or loaded) are worked out together, once, so their pane and sowing follow at once.</summary>
        public override void MapComponentUpdate()
        {
            if (!started) Start();
            if (!refreshSoon) return;
            refreshSoon = false;
            try
            {
                Refresh();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fields by the weather: a field set", e);
            }
        }

        /// <summary>Sowing on this cell is paused now: it lies in a field set to sow by the weather whose crop wouldn't pay. One look; nothing is worked out here.</summary>
        public static bool Paused(Pawn pawn, IntVec3 c)
        {
            if (paused.Count == 0) return false;
            var m = pawn?.Map;
            return m != null && m.zoneManager.ZoneAt(c) is Zone_Growing z && paused.Contains(z);
        }

        /// <summary>Every frost harvest step on a map now, before "Not this" and the waiting are taken out (for the Ahead section).</summary>
        public static List<Suggestion> Steps(Map map)
        {
            var fw = FrostOn ? Of(map) : null;
            return fw == null ? new List<Suggestion>() : fw.frost.Select(f => f.step).ToList();
        }

        /// <summary>The frost harvests still open on this map, the soonest first (kept until the tracker or the forecast changes).</summary>
        public List<FrostStep> Open()
        {
            var t = SuggestTracker.Instance;
            if (!FrostOn || t == null) return new List<FrostStep>();
            if (openKept != null && openVersion == t.Version && openTick == refreshedTick) return openKept;
            var open = new HashSet<Suggestion>(t.Open(frost.Select(f => f.step)));
            openKept = frost.Where(f => open.Contains(f.step)).OrderBy(f => f.step.days).ToList();
            openVersion = t.Version;
            openTick = refreshedTick;
            return openKept;
        }

        /// <summary>The field's line in its inspect pane: its sowing by the weather, a frost harvest open for it.</summary>
        public string Inspect(Zone_Growing z, string text)
        {
            if (z == null) return text;
            var lines = new List<string>();
            if (SowOn && IsByWeather(z) && Outdoor(z))
                lines.Add(outlooks.TryGetValue(z.ID, out var o) && o.crop == z.GetPlantDefToGrow() ? o.line : "CC_Fields_Inspect_Working".Translate().Resolve());
            if (FrostOn)
            {
                var fs = Open().FirstOrDefault(f => f.zone == z);
                if (fs != null)
                    lines.Add("CC_Fields_Inspect_Frost".Translate(WhenWords(fs.pick.lowHour, last).Named("WHEN"), fs.pick.harvest.Count.Named("N"),
                        Plural(fs.crop, fs.pick.harvest.Count).Named("CROPS")).Resolve());
                else if (youngOnly.TryGetValue(z.ID, out var young)) lines.Add(young);
            }
            // The forecast that missed (harm, part 5): said for a few days, never a number of how sure it was.
            if (missed.TryGetValue(z.ID, out var miss))
            {
                int ago = Find.TickManager.TicksGame - miss.tick;
                if (ago >= 0 && ago < HarmFiveMath.MissedNoteDays * GenDate.TicksPerDay)
                    lines.Add("CC_HarmFive_ForecastMissed".Translate(GenDate.DateShortStringAt(Find.TickManager.TicksAbs - ago, Find.WorldGrid.LongLatOf(map.Tile)).Named("DATE"),
                        Temp(miss.temp).Named("TEMP")).Resolve());
            }
            if (lines.Count == 0) return text;
            string add = string.Join("\n", lines);
            return text.NullOrEmpty() ? add : text + "\n" + add;
        }

        /// <summary>The field's buttons: the "By the weather" switch, and the frost harvest when one is open for it.</summary>
        public List<Gizmo> GizmosFor(Zone_Growing z)
        {
            var list = new List<Gizmo>();
            if (z == null) return list;
            if (SowOn && Outdoor(z))
            {
                string desc = ToggleDesc;
                if (IsByWeather(z) && outlooks.TryGetValue(z.ID, out var o)) desc += "\n\n" + o.line;
                if (SmartFarmingHere) desc += "\n\n" + "CC_Fields_ToggleSmartFarming".Translate().Resolve();
                var zone = z;
                list.Add(new Command_Toggle
                {
                    defaultLabel = "CC_Fields_Toggle".Translate(), defaultDesc = desc, icon = FieldIcons.Weather,
                    isActive = () => IsByWeather(zone), toggleAction = () => Set(zone, !IsByWeather(zone))
                });
            }
            if (FrostOn)
            {
                var fs = Open().FirstOrDefault(f => f.zone == z);
                if (fs != null)
                {
                    var step = fs.step;
                    list.Add(new Command_Action
                    {
                        defaultLabel = step.doLabel, defaultDesc = step.title + "\n\n" + step.reason + "\n\n" + AheadPanel.DoTip(step), icon = FieldIcons.Harvest,
                        action = () => AheadPanel.DoStep(step)
                    });
                }
            }
            return list;
        }

        private static string toggleDesc;
        private static TemperatureDisplayMode toggleDescMode;

        /// <summary>The switch's own words (the temperatures in the player's unit), made once.</summary>
        private static string ToggleDesc
        {
            get
            {
                if (toggleDesc == null || toggleDescMode != Prefs.TemperatureMode)
                {
                    toggleDescMode = Prefs.TemperatureMode;
                    toggleDesc = "CC_Fields_ToggleDesc".Translate(Pct(FieldWeatherMath.WorthShare), (-FieldWeatherMath.MaxLimitOffset).ToStringTemperatureOffset("F0"),
                        (-FieldWeatherMath.MinLimitOffset).ToStringTemperatureOffset("F0"), Temp(FieldWeatherMath.MaxLimitOffset), Temp(FieldWeatherMath.MinLimitOffset)).Resolve();
                }
                return toggleDesc;
            }
        }

        [Unsaved] private bool? smartFarming;

        /// <summary>Smart Farming (Owlchemist's, or ReGrowth's copy) is running on this map (looked at once).</summary>
        private bool SmartFarmingHere => smartFarming ??= map.components.Any(c => c != null && c.GetType().Name == "MapComponent_SmartFarming");

        /// <summary>A plant marked by a frost harvest step was harvested (the game's Plant.PlantCollected): counted for the step's check.</summary>
        public static void Collected(Plant plant)
        {
            var t = SuggestTracker.Instance;
            if (plant == null || t == null) return;
            foreach (var d in t.done)
                if (d.state == SuggestMath.Waiting && d.check is FrostHarvestCheck c && d.undo is UndoOrders u && u.marked.Contains(plant))
                {
                    c.collected++;
                    return;
                }
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) byWeather.RemoveAll(id => ZoneOf(id) == null);
            Scribe_Collections.Look(ref byWeather, "byWeather", LookMode.Value);
            Scribe_Collections.Look(ref strays, "strays", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                byWeather ??= new List<int>();
                strays ??= new List<float>();
                SuggestMath.TrimOldest(strays, FieldWeatherMath.StrayDays);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (pausedGame != Current.Game)
            {
                paused.Clear();
                pausedGame = Current.Game;
                Watching = false;
            }
            dirty = true;
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            Clear();
        }
    }

    /// <summary>
    /// Whether a frost harvest worked, looked at a few hours after the coldest hour forecast: whether the frost came (the
    /// coldest hour seen since reached the warmest limit among the plants marked) and how many of them were harvested
    /// in time. The coldest hour is fed hourly by <see cref="FieldWeather"/>, the harvests by <see cref="Patch_FieldWeatherCollected"/>.
    /// </summary>
    public class FrostHarvestCheck : SuggestCheck
    {
        public float expected, warmest, lowest = 999f;
        public int marked, collected;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var map = done.map;
            if (map != null) lowest = Mathf.Min(lowest, map.mapTemperature.OutdoorTemp);
            string low = lowest < 900f ? lowest.ToStringTemperature("F0") : "?";
            if (!(lowest < warmest))
            {
                done.found = "CC_Fields_Check_NoFrost".Translate(low.Named("LOW"), expected.ToStringTemperature("F0").Named("EXPECTED"), collected.Named("SAVED"),
                    marked.Named("N")).Resolve();
                return true;
            }
            if (collected * 2 >= marked)
            {
                done.found = "CC_Fields_Check_Saved".Translate(low.Named("LOW"), collected.Named("SAVED"), marked.Named("N")).Resolve();
                return true;
            }
            whyNot = "CC_Fields_Check_Late".Translate(low.Named("LOW"), collected.Named("SAVED"), marked.Named("N")).Resolve();
            if (map != null && !map.mapPawns.FreeColonistsSpawned.Any(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) && (p.workSettings?.WorkIsActive(WorkTypeDefOf.Growing) ?? false)))
                whyNot += " " + "CC_Fields_Check_NoGrowers".Translate().Resolve();
            // After the frost there is nothing more to wait for: this look is the last.
            done.tries = Math.Max(done.tries, SuggestMath.Tries - 1);
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expected, "expected");
            Scribe_Values.Look(ref warmest, "warmest");
            Scribe_Values.Look(ref lowest, "lowest", 999f);
            Scribe_Values.Look(ref marked, "marked");
            Scribe_Values.Look(ref collected, "collected");
        }
    }

    /// <summary>The fields' buttons' icons: the weather switch, and the game's own harvest mark.</summary>
    [StaticConstructorOnStartup]
    public static class FieldIcons
    {
        public static readonly Texture2D Weather = ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/Temperature", false) ?? Icons.CatFood;
        public static readonly Texture2D Harvest = ContentFinder<Texture2D>.Get("UI/Designators/Harvest", false) ?? Icons.CatFood;
    }

    /// <summary>"Frost: harvest 9 wheat plants" in the alerts while a frost harvest is open on the map in view; a click selects the field, where its button marks them.</summary>
    public class Alert_FrostHarvest : Alert
    {
        private FieldWeather.FrostStep top;
        private FieldWeather.FrostStep labelFor;
        private string label;
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_FrostHarvest()
        {
            defaultPriority = AlertPriority.High;
        }

        public override string GetLabel()
        {
            if (top == null) return "";
            if (label == null || labelFor != top)
            {
                int n = top.pick.harvest.Count;
                label = "CC_Fields_Alert".Translate(n.Named("N"), (n == 1 ? top.crop.label : Find.ActiveLanguageWorker.Pluralize(top.crop.label, n)).Named("CROPS")).Resolve();
                // At another home than the one in view: its name, so the alert says where.
                var at = top.zone?.Map;
                if (at != null && at != Find.CurrentMap && ColonyState.Several) label = "CC_Many_FrostAt".Translate(label.Named("ALERT"), Homes.Name(at).Named("PLACE")).Resolve();
                labelFor = top;
            }
            return label;
        }

        public override TaggedString GetExplanation()
        {
            if (top?.step == null) return "";
            return top.step.title + "\n\n" + top.step.reason + "\n\n" + "CC_Fields_AlertClick".Translate().Resolve();
        }

        public override AlertReport GetReport()
        {
            top = null;
            if (!FieldWeather.FrostOn) return false;
            // Every home's fields, the map in view first (ManyColoniesMath.FrostAt); with one colony, that one.
            List<FieldWeather.FrostStep> open = null;
            var inView = Find.CurrentMap;
            if (inView != null && inView.IsPlayerHome) open = FieldWeather.Of(inView)?.Open();
            if (open == null || open.Count == 0)
            {
                open = null;
                var homes = Homes.All;
                if (homes.Count > 1)
                    foreach (var home in homes)
                    {
                        if (home == inView) continue;
                        var o = FieldWeather.Of(home)?.Open();
                        if (o == null || o.Count == 0) continue;
                        open = o;
                        break;
                    }
            }
            if (open == null || open.Count == 0) return false;
            top = open[0];
            culprits.Clear();
            foreach (var p in top.plants)
                if (p != null && p.Spawned) culprits.Add(p);
            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Active;
        }

        protected override void OnClick()
        {
            var zone = top?.zone;
            if (zone?.Map == null || zone.Cells.Count == 0)
            {
                base.OnClick();
                return;
            }
            if (Find.CurrentMap != zone.Map) Current.Game.CurrentMap = zone.Map;
            Find.Selector.ClearSelection();
            Find.Selector.Select(zone);
            CameraJumper.TryJump(zone.Cells[zone.Cells.Count / 2], zone.Map);
        }
    }
}
