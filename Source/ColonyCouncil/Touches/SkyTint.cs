using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A faint color cast on the sky for two of Rimfolk's conditions: a cool night under the comet, warmer daylight in a
    /// warm spell. Only the colors change. The light level (the sky's glow, which the game uses for plants, darkness
    /// and work) is handed back exactly as it already is, so nothing grows, works or sleeps differently.
    /// </summary>
    /// <remarks>
    /// The tint is applied to the sky the map is showing now (<see cref="SkyManager.CurSky"/>), so it follows any weather
    /// or mod without knowing how the game builds its sky. Blended in at half strength each frame, the result settles at
    /// about the multiplier given (1 / (1 - (m - 1)) for a small change), and never runs away (the blend weight times the
    /// multiplier stays well under 1). When any other condition colors the sky (an eclipse, an aurora, fallout), or the
    /// map's brightness is being changed, the tint stands aside entirely.
    /// </remarks>
    public static class SkyTint
    {
        /// <summary>Cool and a little clearer: less red, a touch more blue.</summary>
        private static readonly Color CometNight = new Color(0.96f, 1f, 1.07f);

        /// <summary>Warmer daylight: a little less blue and green.</summary>
        private static readonly Color WarmDay = new Color(1f, 0.985f, 0.94f);

        private const float MaxBlend = 0.5f;

        private static readonly List<GameCondition> conditions = new List<GameCondition>();
        private static int checkedTick = -1;
        private static int checkedMap = -1;
        private static bool crowded;
        private static bool checking;

        // ---------------- The comet ----------------

        public static SkyTarget? Comet(GameCondition c, Map map) => CometLerp(c, map) > 0f ? Tinted(c, map, CometNight, "comet sky") : null;

        /// <summary>Only in the dark: nothing at dusk, all of it once the sun is well down; faded in and out with the comet.</summary>
        public static float CometLerp(GameCondition c, Map map)
        {
            if (!VisualTouches.On || map == null || c == null) return 0f;
            try
            {
                float night = Mathf.InverseLerp(0.2f, 0.05f, GenCelestial.CurCelestialSunGlow(map));
                return Safe(MaxBlend * night * GameConditionUtility.LerpInOutValue(c, 2500f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("comet sky blend", e);
                return 0f;
            }
        }

        // ---------------- The warm spell ----------------

        public static SkyTarget? Warm(GameCondition c, Map map) => WarmLerp(c, map) > 0f ? Tinted(c, map, WarmDay, "warm spell sky") : null;

        /// <summary>Only in full daylight; faded in and out as slowly as the warmth itself.</summary>
        public static float WarmLerp(GameCondition c, Map map)
        {
            if (!VisualTouches.On || map == null || c == null) return 0f;
            try
            {
                float day = Mathf.InverseLerp(0.45f, 0.7f, GenCelestial.CurCelestialSunGlow(map));
                return Safe(MaxBlend * day * GameConditionUtility.LerpInOutValue(c, 12000f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("warm spell sky blend", e);
                return 0f;
            }
        }

        // ---------------- The tint ----------------

        private static SkyTarget? Tinted(GameCondition c, Map map, Color m, string what)
        {
            try
            {
                var sky = map.skyManager;
                if (sky == null || Crowded(map)) return null;
                var cur = sky.CurSky;
                var colors = cur.colors;
                // Not drawn yet (a map just loaded), or a sky some mod keeps black: leave it alone.
                if (Mathf.Max(colors.sky.r, Mathf.Max(colors.sky.g, colors.sky.b)) < 0.05f) return null;
                if (!IsFinite(cur.glow) || cur.glow < 0f) return null;
                colors.sky = new Color(Clamp(colors.sky.r * m.r), Clamp(colors.sky.g * m.g), Clamp(colors.sky.b * m.b), colors.sky.a);
                // The glow and the light sources' shine are what the sky has now: blended with themselves, they don't move.
                return new SkyTarget(cur.glow, colors, cur.lightsourceShineSize, cur.lightsourceShineIntensity);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(what, e);
                return null;
            }
        }

        /// <summary>
        /// Another condition is coloring this map's sky, or two of these at once, or the map's brightness is being
        /// changed: then none of Rimfolk's tints apply. Worked out once per tick and map (the sky asks every frame).
        /// </summary>
        private static bool Crowded(Map map)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now == checkedTick && map.uniqueID == checkedMap) return crowded;
            // Asked again while asking another condition (a mod's sky that looks at the others): just stand aside.
            if (checking) return true;
            checking = true;
            try
            {
                var manager = map.gameConditionManager;
                bool busy = manager == null || Mathf.Abs(manager.MapBrightness - 1f) > 0.001f || manager.BrightnessChanging;
                int skies = 0;
                if (!busy)
                {
                    conditions.Clear();
                    manager.GetAllGameConditionsAffectingMap(map, conditions);
                    foreach (var other in conditions)
                    {
                        if (other is GameCondition_Comet || other is GameCondition_WarmSpell) skies++;
                        else if (other != null && other.SkyTarget(map).HasValue) skies += 2;
                    }
                    conditions.Clear();
                }
                crowded = busy || skies > 1;
                checkedTick = now;
                checkedMap = map.uniqueID;
                return crowded;
            }
            finally
            {
                checking = false;
            }
        }

        private static float Clamp(float v) => IsFinite(v) ? Mathf.Clamp01(v) : 0f;

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>A blend weight that can't be NaN or outside 0..1, whatever the inputs.</summary>
        private static float Safe(float v) => IsFinite(v) ? Mathf.Clamp01(v) : 0f;
    }
}
