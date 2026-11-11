using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The sun standing still a moment too long (Helios's sign; GodOmens, OmenKind.StillSun): for an hour and a half of the
    /// afternoon the shadows hold where they were when it began (the game's own shadow override for a weather event, as a
    /// lightning flash has). Nothing is saved: a load lets the shadows go on.
    /// </summary>
    public class WeatherEvent_StillSun : WeatherEvent
    {
        private readonly Vector2 held;
        private readonly int until;

        private WeatherEvent_StillSun(Map map, Vector2 held, int ticks) : base(map)
        {
            this.held = held;
            until = Find.TickManager.TicksGame + ticks;
        }

        /// <summary>The sun stands still over <paramref name="map"/> for <paramref name="ticks"/>; only on the map being looked at (the shadows are read off the screen). True when it does.</summary>
        public static bool Begin(Map map, int ticks)
        {
            if (map == null || Find.CurrentMap != map || Active(map)) return false;
            try
            {
                var v = Shader.GetGlobalVector(ShaderPropertyIDs.MapSunLightDirection);
                if (Math.Abs(v.x) + Math.Abs(v.z) < 0.001f) return false;
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_StillSun(map, new Vector2(v.x, v.z), ticks));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sun standing still", e);
                return false;
            }
        }

        /// <summary>The sun is standing still over <paramref name="map"/> now.</summary>
        public static bool Active(Map map)
        {
            var events = map?.weatherManager?.eventHandler?.LiveEventsListForReading;
            if (events == null) return false;
            foreach (var e in events)
                if (e is WeatherEvent_StillSun)
                    return true;
            return false;
        }

        public override bool Expired => Find.TickManager.TicksGame > until;

        public override Vector2? OverrideShadowVector => held;

        public override void FireEvent()
        {
        }

        public override void WeatherEventTick()
        {
        }
    }
}
