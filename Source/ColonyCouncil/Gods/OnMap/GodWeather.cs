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
                        return Condition(map, GameConditionDefOf.Eclipse, "Eclipse", 0.5f, false);
                    case WeatherVoice.Aurora:
                        return Condition(map, GameConditionDefOf.Aurora, "Aurora", 0.3f, true);
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
        private static bool Condition(Map map, GameConditionDef def, string incident, float days, bool atNight)
        {
            if (def == null || map.gameConditionManager.ConditionIsActive(def) || map.gameConditionManager.IsAlwaysDarkOutside) return false;
            if (atNight && (GenCelestial.CurCelestialSunGlow(map) > 0.5f || GenCelestial.CelestialSunGlow(map, Find.TickManager.TicksAbs + 5000) > 0.5f)) return false;
            if (!atNight && GenCelestial.CurCelestialSunGlow(map) < 0.5f) return false;
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

        /// <summary>A colonist out of doors names it in a bubble, a line in the news in the voice of how real the gods are, and the god's act remembered.</summary>
        private static void Told(God god, Map map, WeatherVoice v, bool pleased, bool readInto)
        {
            string tag = v.ToString().ToLowerInvariant();
            var speaker = map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed && !p.InMentalState && !p.Position.Roofed(map) && People.HasInnerLife(p))
                .RandomElementWithFallback() ?? map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed).RandomElementWithFallback();
            if (speaker != null)
            {
                string line = Flavor.FromPool(SayPool, speaker, null, null, tag, pleased ? "pleased" : "angered", "named", GodShapes.LineTag(god),
                    god.domain.ToString().ToLowerInvariant(), readInto ? "believed" : null);
                if (!line.NullOrEmpty()) Speech.Say(speaker, line.Formatted(GodsOnMapWords.Args(god, false).Concat(new[] { speaker.Named("PAWN") }).ToArray()).Resolve());
            }
            string what = ("CC_GodM_Weather_" + v).Translate(GodsOnMapWords.Args(god, false)).Resolve();
            string text = GodsOnMapWords.Voice(what) + ".";
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, speaker != null ? new LookTargets(speaker) : null, MessageTypeDefOf.NeutralEvent);
            Gods.Remember(new GodAct { god = god.id, home = Gods.HomeKey(map), tick = Now, kind = GodActKind.Act, text = GodsOnMapWords.Veil(what, Gods.Realness != GodRealness.Walking) });
        }
    }
}
