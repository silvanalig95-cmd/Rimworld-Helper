using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a seasonal custom needs beyond its season, on a dilemma def with <see cref="DilemmaWorker_Custom"/>: the
    /// weather outside (a swim wants a warm day, a frost soup the first cold one), water on the map, the colony's own
    /// graves, or enough raw food for a feast.
    /// </summary>
    public class CustomConditions : DefModExtension
    {
        /// <summary>The outdoor temperature at the home map, in °C (leave out for any).</summary>
        public float minTemp = -999f;
        public float maxTemp = 999f;
        /// <summary>A river, a coast or some open water on the map.</summary>
        public bool water;
        /// <summary>At least one grave with one of the colony's own dead in it.</summary>
        public bool graves;
        /// <summary>Raw food in storage, at least this many times the dilemma's thingCount (0 = no need).</summary>
        public float foodFactor;
    }

    /// <summary>A seasonal custom: the usual season and roles, plus whatever its <see cref="CustomConditions"/> ask.</summary>
    public class DilemmaWorker_Custom : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            var c = def.GetModExtension<CustomConditions>();
            if (c == null) return true;
            float temp = map.mapTemperature?.OutdoorTemp ?? 15f;
            if (temp < c.minTemp || temp > c.maxTemp) return false;
            if (c.water && !HasWater(map)) return false;
            if (c.graves && !HasOurGraves(map)) return false;
            return true;
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = def.GetModExtension<CustomConditions>();
            return c == null || c.foodFactor <= 0f || DilemmaEffect_Consume.RawFood(ctx.map) >= ctx.count * c.foodFactor;
        }

        /// <summary>A river or coast on the world tile, or open water somewhere on the map (sampled, so it stays cheap).</summary>
        public static bool HasWater(Map map)
        {
            try
            {
                if (map.TileInfo is RimWorld.Planet.SurfaceTile tile && !tile.Rivers.NullOrEmpty()) return true;
            }
            catch
            {
                // Odd tiles (space, other layers) have no rivers to ask about.
            }
            var grid = map.terrainGrid;
            int step = 7;
            for (int x = step / 2; x < map.Size.x; x += step)
                for (int z = step / 2; z < map.Size.z; z += step)
                {
                    var t = grid.TerrainAt(new IntVec3(x, 0, z));
                    if (t != null && t.IsWater) return true;
                }
            return false;
        }

        /// <summary>A grave holding one of the colony's own dead.</summary>
        public static bool HasOurGraves(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>()
                .Any(g => g.HasCorpse && g.Corpse?.InnerPawn != null && g.Corpse.InnerPawn.RaceProps.Humanlike && g.Corpse.InnerPawn.Faction == Faction.OfPlayer);
    }
}
