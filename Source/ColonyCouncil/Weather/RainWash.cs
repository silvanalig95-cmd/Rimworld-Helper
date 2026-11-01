using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rain washing beyond the game's own: the harder it rains, the more often filth that the game already lets rain wash
    /// (each filth def's rainWashes: blood, dirt, vomit and the like) thins where it lies under the open sky, so a day of
    /// steady rain leaves the yard clean of most of it. Rubble and whatever else rain doesn't wash stays, and nothing under
    /// a roof is touched. Hourly from the weather book; the game's own washing goes on as before.
    /// </summary>
    public static class RainWash
    {
        private static readonly List<Thing> buffer = new List<Thing>();

        public static bool On => ColonyCouncilMod.Settings?.rainWashesMore ?? true;

        /// <summary>The chance in an hour that one layer of a filth washes off, at this rain rate (none in a drizzle).</summary>
        public static float ThinChance(float rainRate) => rainRate < 0.2f ? 0f : Math.Min(0.25f, 0.15f * rainRate);

        public static void Hourly(Map map)
        {
            if (!On || map?.weatherManager == null) return;
            float chance = ThinChance(map.weatherManager.RainRate);
            if (chance <= 0f) return;
            buffer.Clear();
            buffer.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Filth));
            try
            {
                foreach (var t in buffer)
                {
                    if (!(t is Filth f) || !f.Spawned || f.def.filth == null || !f.def.filth.rainWashes) continue;
                    if (f.Position.Roofed(map) || !Rand.Chance(chance)) continue;
                    f.ThinFilth();
                }
            }
            finally
            {
                buffer.Clear();
            }
        }
    }
}
