using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The weather in the colony's dealings with others. A trade caravan whose stay is nearly over when a storm or a
    /// heavy snow sets in waits it out: half a day longer at its spot, once a visit (the game's own timer for their
    /// stay, set back). People coming to the colony for a mission (a dignitary, refugees, a guest) take half again as
    /// long when the land is snowbound. A camp dresses and pitches for its weather, and packs up only when it turns
    /// dangerous (Camps/CampWeather.cs).
    /// </summary>
    public static class WeatherVisitors
    {
        /// <summary>Hours left of a trade caravan's stay under which a storm keeps it, and how long longer it stays.</summary>
        private const int NearlyOverHours = 8, LongerHours = 12;

        // The caravans already kept once (not saved: after a load, one may be kept once more).
        private static readonly HashSet<int> kept = new HashSet<int>();
        private static Game game;

        /// <summary>A storm, or snow coming down hard.</summary>
        public static bool Rough(Map map)
        {
            var w = map?.weatherManager;
            return w != null && (Flavor.IsStorm(w.curWeather) || w.SnowRate >= 0.8f);
        }

        /// <summary>Snow lies deep on the land, or it has snowed on most of the last days (the weather record).</summary>
        public static bool Snowbound(Map map)
        {
            if (map?.snowGrid == null || map.Area <= 0) return false;
            if (map.snowGrid.TotalDepth / map.Area >= 0.25f) return true;
            var days = WeatherBook.Of(map)?.Days;
            return days != null && days.Count >= 3 && WeatherMath.Count(days, 3, d => d.Snowy) >= 2;
        }

        /// <summary>Once an hour on a home map (the weather record's hour).</summary>
        public static void Hourly(Map map)
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                kept.Clear();
            }
            if (!Rough(map) || map.lordManager == null) return;
            foreach (var lord in map.lordManager.lords)
            {
                if (!(lord?.LordJob is LordJob_TradeWithColony) || !AtTheirSpot(lord) || kept.Contains(lord.loadID)) continue;
                if (KeepLonger(lord))
                {
                    kept.Add(lord.loadID);
                    var faction = lord.faction;
                    News.Post("CC_Weather_TraderStays".Translate(faction?.Name ?? "?").Resolve(), new LookTargets(lord.ownedPawns), NewsKind.World, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                }
            }
        }

        /// <summary>The caravan is standing at its spot, trading (the game's toil for it is internal, so known by name).</summary>
        private static bool AtTheirSpot(Lord lord) => lord.CurLordToil?.GetType().Name == "LordToil_DefendTraderCaravan";

        /// <summary>The caravan's timer for its stay at its spot, nearly run out: set back half a day.</summary>
        private static bool KeepLonger(Lord lord)
        {
            var graph = lord.Graph;
            if (graph?.transitions == null) return false;
            foreach (var t in graph.transitions)
            {
                if (t?.sources == null || !t.sources.Contains(lord.CurLordToil) || !(t.target is LordToil_ExitMapAndEscortCarriers) || t.triggers == null) continue;
                foreach (var trigger in t.triggers)
                {
                    if (!(trigger is Trigger_TicksPassed timer) || !(trigger.data is TriggerData_TicksPassed d)) continue;
                    if (timer.TicksLeft > NearlyOverHours * GenDate.TicksPerHour) return false;
                    d.ticksPassed = Mathf.Max(0, d.ticksPassed - LongerHours * GenDate.TicksPerHour);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A mission just taken on whose people are still to come: half again as long on the way when the land is
        /// snowbound, and its time to finish longer by as much. A word says so.
        /// </summary>
        public static void Notify_MissionStarted(Mission m, Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (m == null || map == null || m.due <= now || !Snowbound(map)) return;
            int extra = (m.due - now) / 2;
            if (extra < GenDate.TicksPerHour) return;
            m.due += extra;
            m.days += extra / (float)GenDate.TicksPerDay;
            News.Post("CC_Weather_MissionSnow".Translate(m.faction?.Name ?? "?").Resolve(), LookTargets.Invalid, NewsKind.World, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
        }
    }
}
