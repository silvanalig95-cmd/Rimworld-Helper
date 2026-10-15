using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A safety net for the map's wind. On a player's 1.7 game the map's wind speed turned into NaN (not a number) a
    /// while after the weather moved on from Windy, on a map that is always windy and during an eclipse. Nothing of
    /// Rimfolk's touches the wind; the cause lies with the game or another mod. But a NaN wind stops every wind
    /// turbine ("NaN W"), breaks mods that divide by it, and wind-driven sounds pass it on to the audio, which then
    /// goes silent until the game is reloaded from the main menu. When the wind isn't a number after the game works it
    /// out, this does what changing the weather in dev mode did for the player: it settles the weather's change (the
    /// last weather becomes the current one, so nothing is half-way between two weathers any more), and the game works
    /// the wind out properly again from the next tick, at full strength on an always-windy map. For the one tick in
    /// between it gives the strongest of the last good wind and what the map's conditions hold it at. It says once in
    /// the log what the weather and conditions were, so the real cause can be found. A wind that is a number is never
    /// touched.
    /// </summary>
    [HarmonyPatch(typeof(WindManager), nameof(WindManager.WindManagerTick))]
    public static class Patch_WindGuard
    {
        private static AccessTools.FieldRef<WindManager, float> speed;
        private static AccessTools.FieldRef<WindManager, Map> mapOf;
        private static bool looked, broken, told;
        private static Game game;
        private static readonly Dictionary<int, float> lastGood = new Dictionary<int, float>();

        /// <summary>How many times this session the wind had to be put right (for "Rimfolk: check myself").</summary>
        public static int Fixed;

        public static void Postfix(WindManager __instance)
        {
            if (broken || !RimfolkPatches.On) return;
            try
            {
                if (!looked)
                {
                    looked = true;
                    speed = AccessTools.FieldRefAccess<WindManager, float>("cachedWindSpeed");
                    mapOf = AccessTools.FieldRefAccess<WindManager, Map>("map");
                }
                if (Current.Game != game)
                {
                    game = Current.Game;
                    lastGood.Clear();
                }
                float v = speed(__instance);
                Map map = mapOf(__instance);
                int id = map?.uniqueID ?? -1;
                if (!float.IsNaN(v) && !float.IsInfinity(v))
                {
                    lastGood[id] = v;
                    return;
                }
                string before = told ? null : Describe(map, v);
                // What fixed it by hand: the weather settled (dev mode's "change weather" leaves last and current the same).
                var w = map?.weatherManager;
                if (w != null && w.curWeather != null && w.lastWeather != w.curWeather) w.lastWeather = w.curWeather;
                float fix = Strongest(map, lastGood.TryGetValue(id, out float good) ? good : -1f);
                speed(__instance) = fix;
                Fixed++;
                if (!told)
                {
                    told = true;
                    Log.Warning(before + "\n  Rimfolk settled the weather and set the wind to " + fix.ToString("0.00") + " for this moment; the game works it out again from the next tick.");
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("wind guard", e);
            }
        }

        /// <summary>
        /// The wind for the one tick until the game works it out again: the last good wind, or what the map's conditions
        /// hold it at if that's stronger (an always-windy map), or a steady breeze if neither is known.
        /// </summary>
        private static float Strongest(Map map, float lastGoodWind)
        {
            float best = lastGoodWind;
            var conditions = map?.gameConditionManager?.ActiveConditions;
            if (conditions != null)
                foreach (var c in conditions)
                {
                    if (c == null) continue;
                    float min = c.MinWindSpeed();
                    if (!float.IsNaN(min) && !float.IsInfinity(min) && min > best) best = min;
                }
            return best >= 0f ? best : 1f;
        }

        /// <summary>Everything the game's wind is worked out from, for the log (read before anything is settled).</summary>
        private static string Describe(Map map, float v)
        {
            var sb = new StringBuilder();
            sb.Append("[Rimfolk] The map's wind speed was ").Append(v)
                .Append(" (not a number), which stops wind turbines and can silence the game's sound. This comes from the game or another mod, not Rimfolk; what the wind is worked out from:");
            try
            {
                var w = map?.weatherManager;
                if (w != null)
                {
                    sb.Append("\n  weather: ").Append(w.curWeather?.defName ?? "none").Append(" (from ").Append(w.lastWeather?.defName ?? "none")
                        .Append("), age ").Append(w.curWeatherAge).Append(" ticks");
                    sb.Append("\n  wind factor ").Append(w.CurWindSpeedFactor).Append(", wind offset ").Append(w.CurWindSpeedOffset);
                    if (w.curWeather != null) sb.Append("\n  ").Append(w.curWeather.defName).Append(": factor ").Append(w.curWeather.windSpeedFactor).Append(", offset ").Append(w.curWeather.windSpeedOffset);
                    if (w.lastWeather != null) sb.Append("\n  ").Append(w.lastWeather.defName).Append(": factor ").Append(w.lastWeather.windSpeedFactor).Append(", offset ").Append(w.lastWeather.windSpeedOffset);
                }
                var conditions = map?.gameConditionManager?.ActiveConditions;
                if (conditions != null)
                    foreach (var c in conditions)
                        if (c?.def != null)
                            sb.Append("\n  condition: ").Append(c.def.defName).Append(" (").Append(c.GetType().FullName).Append("), least wind ").Append(c.MinWindSpeed());
                sb.Append("\n  biome: ").Append(map?.Biome?.defName ?? "?").Append(", tile ").Append(map?.Tile.ToString() ?? "?").Append(", tick ").Append(Find.TickManager?.TicksAbs ?? 0);
            }
            catch (Exception e)
            {
                sb.Append("\n  (couldn't read all of it: ").Append(e.Message).Append(")");
            }
            return sb.ToString();
        }
    }
}
