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
    /// Weather as a god's voice (brainstorm 9; docs/design/gods.md, "On the map"): after the colony's deeds and offerings
    /// (the gods' acts of favour, GodBook's), one god answers in its domain with the game's own weather and conditions:
    /// rain after the harvest's or the waters' god is pleased, thunder on cue for a god of storms, an eclipse by day for a
    /// dark god's anger, an aurora on a god's night. Only as the land allows it (a weather the biome has and the season
    /// suits, the game's own condition rules), at most once in GodsOnMapMath.WeatherGapDays at a home, by a chance by how
    /// loudly the god answers (GodsOnMapMath.VoiceChance). A colonist out of doors always names it in a bubble. In
    /// Believed nothing is sent: the weather the game brings of itself soon after is what the colony reads as the god's
    /// answer. The rival gods' quarrel in the weather is the world's part, not this.
    /// </summary>
    public static class GodWeather
    {
        public const string SayPool = "Rimfolk_Pool_GodWeatherSay";

        private static int Now => Find.TickManager.TicksGame;

        // Believed: a change of favour waiting to be read into the weather that comes (not saved: a day's memory).
        private static readonly List<(int god, int home, float amount, int until, WeatherVoice was)> reading = new List<(int, int, float, int, WeatherVoice)>();
        private static Game game;

        private static void Fresh()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            reading.Clear();
        }

        // ================= After the colony's deeds =================

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            Fresh();
            var book = GodBook.Instance;
            if (book == null) return;
            if (b.actsSeen < 0)
            {
                b.actsSeen = now;
                return;
            }
            int seen = b.actsSeen;
            foreach (var act in book.acts.ToList())
            {
                if (act == null || act.tick <= seen || act.kind != GodActKind.Favour || act.other >= 0) continue;
                if (act.tick > b.actsSeen) b.actsSeen = act.tick;
                var god = Gods.ById(act.god);
                var map = Homes.MapOf(act.home);
                // Only a god whose shape speaks in the weather, and one the colony knows (it can't hear a stranger's voice).
                if (god == null || map == null || !Homes.IsHome(map) || !god.known || !GodShapes.Weather(god)) continue;
                if (Gods.Realness == GodRealness.Believed)
                {
                    reading.Add((god.id, act.home, act.favour, now + GodsOnMapMath.BelievedWithinHours * GenDate.TicksPerHour, NowShowing(map)));
                    continue;
                }
                if (Rand.Chance(GodsOnMapMath.VoiceChance(Gods.Loudness(god), act.favour, GodShapes.Bodiless(god)))) Answer(god, map, act.favour);
            }
            if (reading.Count > 0) Read(b, now);
        }

        /// <summary>
        /// <paramref name="god"/> answers the colony at <paramref name="map"/> in its weather, for a change of favour of
        /// <paramref name="amount"/> (pleased or angered), if its domain has a voice for it, the land allows it and its last
        /// answer there is long enough ago (<paramref name="anyway"/>: the gap only, not the size). For the worship's
        /// festival nights and a stranger's blessing. True when it answered.
        /// </summary>
        public static bool Answer(God god, Map map, float amount, bool anyway = false)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || god == null || map == null || !Gods.On || Gods.Realness == GodRealness.Believed || !WeatherDynamics.Applies(map) || !GodShapes.Weather(god)) return false;
            int home = Gods.HomeKey(map);
            if (Now - b.LastWeather(home) < GodsOnMapMath.WeatherGap(GodShapes.Bodiless(god)) * GenDate.TicksPerDay) return false;
            bool night = GenCelestial.CurCelestialSunGlow(map) < 0.4f;
            float size = anyway ? Math.Sign(amount) * Math.Max(Math.Abs(amount), GodsOnMapMath.WeatherFrom) : amount;
            var voice = GodsOnMapMath.Voice(god.domain, god.second, size, night, GodShapes.Voices(god));
            if (voice == WeatherVoice.None || !Bring(map, voice, amount < 0f)) return false;
            b.NoteWeather(home, Now);
            Told(god, map, voice, amount > 0f, false);
            return true;
        }

        // ================= The weather itself =================

        /// <summary>Brings the weather or condition of a voice, as the land allows it now. False when it can't come.</summary>
        private static bool Bring(Map map, WeatherVoice v, bool angered)
        {
            try
            {
                switch (v)
                {
                    case WeatherVoice.Rain:
                        return Rain(map);
                    case WeatherVoice.Thunder:
                        // Thunder on cue, then the land's own storm when it has one now (dry thunder for an angered god).
                        for (int i = 0; i < 3; i++) map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
                        Storm(map, angered);
                        return true;
                    case WeatherVoice.Eclipse:
                        if (!Condition(map, GameConditionDefOf.Eclipse, "Eclipse", 0.5f, false)) return false;
                        Watch(map);
                        return true;
                    case WeatherVoice.Aurora:
                        return Condition(map, GameConditionDefOf.Aurora, "Aurora", 0.3f, true);
                    case WeatherVoice.Cold:
                        return Condition(map, GameConditionDefOf.ColdSnap, "ColdSnap", 1.5f, false, true);
                    case WeatherVoice.Heat:
                        return Condition(map, GameConditionDefOf.HeatWave, "HeatWave", 1.5f, false, true);
                    case WeatherVoice.Clear:
                        return Clear(map);
                    case WeatherVoice.Sleepless:
                        return Sleepless(map);
                    case WeatherVoice.Snow:
                        return Snow(map);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an answer in the weather", e);
            }
            return false;
        }

        private static readonly AccessTools.FieldRef<WeatherDecider, int> Duration = AccessTools.FieldRefAccess<WeatherDecider, int>("curWeatherDuration");

        /// <summary>The land's weathers of a kind that can come now (the biome has them, the season's warmth suits them).</summary>
        private static List<WeatherDef> LandOf(Map map, Func<WeatherKind, bool> kind)
        {
            float temp = map.mapTemperature.OutdoorTemp;
            return map.Biome.baseWeatherCommonalities.Where(r => r?.weather != null && r.commonality > 0f && kind(WeatherDynamics.KindOf(r.weather))
                                                              && r.weather.temperatureRange.Includes(temp)).Select(r => r.weather).ToList();
        }

        private static void Start(Map map, WeatherDef w)
        {
            map.weatherManager.TransitionTo(w);
            try
            {
                Duration(map.weatherDecider) = w.durationRange.RandomInRange;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the weather's length", e);
            }
        }

        /// <summary>The land's rain, if it has one now and it isn't raining already, and a rainbow after it where a mod has one.</summary>
        private static bool Rain(Map map)
        {
            var now = WeatherDynamics.KindOf(map.weatherManager.curWeather);
            if (now == WeatherKind.Rain || now == WeatherKind.Storm || now == WeatherKind.Downpour) return false;
            if (map.gameConditionManager.ActiveConditions.Any(c => c.def.preventRain)) return false;
            var rain = LandOf(map, k => k == WeatherKind.Rain).RandomElementWithFallback();
            if (rain == null) return false;
            Start(map, rain);
            return true;
        }

        /// <summary>Everyone of the colony out of doors stops a while to watch (an eclipse: something stepping between).</summary>
        private static void Watch(Map map)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed && !p.Drafted && !p.Position.Roofed(map)
                                                                         && (p.CurJob == null || p.CurJob.def.casualInterruptible)).Take(8).ToList())
            {
                try
                {
                    var wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                    wait.expiryInterval = Rand.Range(400, 800);
                    p.jobs.StartJob(wait, Verse.AI.JobCondition.InterruptForced, null, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: watching the eclipse", e);
                }
            }
        }

        /// <summary>Clear skies: the land's clear weather, if it isn't clear already, held a long while.</summary>
        private static bool Clear(Map map)
        {
            if (WeatherDynamics.KindOf(map.weatherManager.curWeather) == WeatherKind.Clear) return false;
            var clear = LandOf(map, k => k == WeatherKind.Clear).RandomElementWithFallback();
            if (clear == null) return false;
            Start(map, clear);
            return true;
        }

        public const string NightmareThought = "Rimfolk_Thought_MoonNightmare";

        /// <summary>
        /// A night of bad sleep: everyone of the colony asleep now wakes from a nightmare that follows them into the day
        /// (a bad memory, a day long), and says so. False when nobody is asleep.
        /// </summary>
        private static bool Sleepless(Map map)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(NightmareThought);
            if (def == null) return false;
            var asleep = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Awake() && !p.Downed && p.needs?.mood != null && People.HasInnerLife(p)).ToList();
            if (asleep.Count == 0) return false;
            foreach (var p in asleep)
            {
                p.needs.mood.thoughts.memories.TryGainMemory(def);
                try
                {
                    // Woken by it: up out of bed for a while.
                    RestUtility.WakeUp(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a nightmare", e);
                }
            }
            return true;
        }

        /// <summary>The land's snow, if it has a snow it can have now and it isn't snowing already.</summary>
        private static bool Snow(Map map)
        {
            var now = WeatherDynamics.KindOf(map.weatherManager.curWeather);
            if (now == WeatherKind.Snow) return false;
            var snow = LandOf(map, k => k == WeatherKind.Snow).RandomElementWithFallback();
            if (snow == null) return false;
            Start(map, snow);
            return true;
        }

        /// <summary>The land's thunderstorm (a dry one when <paramref name="dry"/>), if it has one now. False when not.</summary>
        public static bool Storm(Map map, bool dry)
        {
            if (map == null || !WeatherDynamics.Applies(map)) return false;
            var now = WeatherDynamics.KindOf(map.weatherManager.curWeather);
            if (now == WeatherKind.Storm || now == WeatherKind.DryStorm) return false;
            var storms = LandOf(map, k => dry ? k == WeatherKind.DryStorm : k == WeatherKind.Storm);
            var storm = storms.FirstOrDefault(w => w.defName.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0) ?? storms.RandomElementWithFallback();
            if (storm == null) return false;
            Start(map, storm);
            return true;
        }

        /// <summary>
        /// The game's own condition (an eclipse, an aurora) for its own length (its event's days, else <paramref name="days"/>),
        /// when none is on, the map isn't always dark, and (an aurora) it is night and will stay night a while.
        /// </summary>
        private static bool Condition(Map map, GameConditionDef def, string incident, float days, bool atNight, bool anyHour = false)
        {
            if (def == null || map.gameConditionManager.ConditionIsActive(def) || map.gameConditionManager.IsAlwaysDarkOutside) return false;
            if (!anyHour && atNight && (GenCelestial.CurCelestialSunGlow(map) > 0.5f || GenCelestial.CelestialSunGlow(map, Find.TickManager.TicksAbs + 5000) > 0.5f)) return false;
            if (!anyHour && !atNight && GenCelestial.CurCelestialSunGlow(map) < 0.5f) return false;
            // The game's own rules for the condition (a cold snap only where it can be cold, its own season).
            var inc0 = DefDatabase<IncidentDef>.GetNamedSilentFail(incident);
            if (anyHour && inc0 != null)
            {
                var parms = StorytellerUtility.DefaultParmsNow(inc0.category, map);
                if (!inc0.Worker.CanFireNow(parms)) return false;
            }
            var inc = DefDatabase<IncidentDef>.GetNamedSilentFail(incident);
            float d = inc != null && inc.durationDays.max > 0f ? inc.durationDays.RandomInRange : days;
            var cond = GameConditionMaker.MakeCondition(def, Mathf.RoundToInt(d * GenDate.TicksPerDay));
            map.gameConditionManager.RegisterCondition(cond);
            return true;
        }

        private static WeatherVoice NowShowing(Map map)
        {
            var k = WeatherDynamics.KindOf(map.weatherManager.curWeather);
            if (map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse)) return WeatherVoice.Eclipse;
            if (GameConditionDefOf.Aurora != null && map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Aurora)) return WeatherVoice.Aurora;
            if (k == WeatherKind.Storm || k == WeatherKind.DryStorm) return WeatherVoice.Thunder;
            if (k == WeatherKind.Rain || k == WeatherKind.Downpour) return WeatherVoice.Rain;
            if (map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap)) return WeatherVoice.Cold;
            if (map.gameConditionManager.ConditionIsActive(GameConditionDefOf.HeatWave)) return WeatherVoice.Heat;
            if (k == WeatherKind.Snow) return WeatherVoice.Snow;
            return WeatherVoice.None;
        }

        // ================= Believed: the weather read as an answer =================

        /// <summary>Believed: the weather the game brought of itself after a change of favour, if it is the god's kind of answer, read as one.</summary>
        private static void Read(GodsOnMapBook b, int now)
        {
            for (int i = reading.Count - 1; i >= 0; i--)
            {
                var r = reading[i];
                var god = Gods.ById(r.god);
                var map = Homes.MapOf(r.home);
                if (god == null || map == null || now > r.until)
                {
                    reading.RemoveAt(i);
                    continue;
                }
                var cur = NowShowing(map);
                if (cur == WeatherVoice.None || cur == r.was) continue;
                bool night = GenCelestial.CurCelestialSunGlow(map) < 0.4f;
                if (GodsOnMapMath.Voice(god.domain, god.second, r.amount, night, GodShapes.Voices(god)) != cur) continue;
                reading.RemoveAt(i);
                if (now - b.LastWeather(r.home) < GodsOnMapMath.WeatherGap(GodShapes.Bodiless(god)) * GenDate.TicksPerDay) continue;
                b.NoteWeather(r.home, now);
                Told(god, map, cur, r.amount > 0f, true);
            }
        }

        // ================= Said and remembered =================

        /// <summary>A colonist out of doors names it in a bubble (what they make of it), and the god's act is remembered; nothing else is told.</summary>
        private static void Told(God god, Map map, WeatherVoice v, bool pleased, bool readInto)
        {
            string tag = v.ToString().ToLowerInvariant();
            var speaker = map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed && !p.InMentalState && !p.Position.Roofed(map) && People.HasInnerLife(p))
                .RandomElementWithFallback() ?? map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed).RandomElementWithFallback();
            if (speaker != null)
            {
                string line = Flavor.FromPool(SayPool, speaker, null, null, tag, pleased ? "pleased" : "angered", "named", GodShapes.LineTag(god),
                    god.domain.ToString().ToLowerInvariant(), readInto ? "believed" : null);
                string said = line.NullOrEmpty() ? null : line.Formatted(GodsOnMapWords.Args(god, false).Concat(new[] { speaker.Named("PAWN") }).ToArray()).Resolve();
                if (said != null) Speech.Say(speaker, said);
            }
            // No line in the news: the weather is seen, and someone says what they make of it. The colony's book keeps it in
            // their words: what they saw, and that they took it for a sign (which may be wrong).
            GodsOnMapWords.Saw(god, map, speaker?.Position ?? IntVec3.Invalid, GodActKind.Act, speaker, "CC_GodM_Saw_Weather_" + v);
        }
    }
}
