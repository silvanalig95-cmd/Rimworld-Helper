using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How wet the season has been on a map, for the land's still water (Nature/LandWater.cs): one method, the seam the
    /// water reads through. Today it reads the colony's weather record (Weather/WeatherBook.cs: the wet, snowy and dry days
    /// of the last two weeks against the land's usual rain) and any drought or flood going on (the game's, Vanilla Events
    /// Expanded's, the region's). When the climate's own season character (LandClimate, being built in Weather/) is there,
    /// this is the one place to read it instead.
    /// </summary>
    public static class LandWet
    {
        /// <summary>Days of the record read: the last two weeks.</summary>
        public const int Days = 14;

        /// <summary>How wet the season has been, 0 (a drought) to 1 (very wet), 0.5 as usual for this land (LandMath.Wetness).</summary>
        public static float SeasonWet(Map map)
        {
            if (map == null) return 0.5f;
            var book = WeatherBook.Of(map);
            var days = book?.Days;
            int n = 0, wet = 0, snow = 0, run = 0, longest = 0;
            if (days != null)
                foreach (var d in days.Skip(Math.Max(0, days.Count - Days)))
                {
                    if (!d.Counted) continue;
                    n++;
                    if (d.Wet) wet++;
                    if (d.Snowy) snow++;
                    run = d.Dry ? run + 1 : 0;
                    longest = Math.Max(longest, run);
                }
            return LandMath.Wetness(n, wet, snow, longest, map.TileInfo?.rainfall ?? 600f, Drought(map), Flood(map));
        }

        /// <summary>A drought going on: the game's (Odyssey's), Vanilla Events Expanded's, or the region's (a world event).</summary>
        public static bool Drought(Map map)
        {
            if (map?.gameConditionManager == null) return false;
            foreach (var c in map.gameConditionManager.ActiveConditions)
            {
                string n = c?.def?.defName;
                if (n == "Drought" || n == "VEE_Drought") return true;
            }
            return Running("Council_WS_Drought");
        }

        /// <summary>The region's floods going on (a world event).</summary>
        public static bool Flood(Map map) => map != null && Running("Council_WS_Flood");

        /// <summary>A world event of this name is running.</summary>
        public static bool Running(string defName)
        {
            var ws = WorldSituations.Instance;
            return ws != null && ws.Running.Any(s => s.def?.defName == defName);
        }
    }
}
