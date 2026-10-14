using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What kind of land a map is, in words lines and encounters can ask for: desert, tundra, boreal, jungle, swamp,
    /// forest, grassland, mountains, hills, coast, lake, river. Worked out from the biome's name (so other mods' biomes
    /// fit too: an "AB_MiasmicMangrove" is a swamp) and the tile (hills, a coast, a river). As tags they read
    /// "land:desert"; a map can be several at once (a tropical swamp on the coast).
    /// </summary>
    public static class Lands
    {
        private static readonly (string land, string[] words)[] BiomeWords =
        {
            ("desert", new[] { "desert", "arid", "dune", "sand", "badland", "salt", "xeric" }),
            ("tundra", new[] { "tundra", "ice", "glaci", "polar", "frozen", "arctic", "permafrost", "snow" }),
            ("boreal", new[] { "boreal", "taiga" }),
            ("jungle", new[] { "jungle", "rainforest", "tropical", "cloudforest" }),
            ("swamp", new[] { "swamp", "bog", "marsh", "wetland", "mangrove", "fen", "mire" }),
            ("forest", new[] { "forest", "wood", "grove" }),
            ("grassland", new[] { "grass", "steppe", "savanna", "prairie", "meadow", "plain", "shrub" })
        };

        private static readonly Dictionary<int, (int tick, HashSet<string> lands)> cache = new Dictionary<int, (int, HashSet<string>)>();
        private const int CacheTicks = GenDate.TicksPerDay;

        /// <summary>The lands of a map, as bare words ("desert", "coast"). Never null.</summary>
        public static HashSet<string> Of(Map map)
        {
            if (map == null) return new HashSet<string>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(map.uniqueID, out var c) && now >= c.tick && now - c.tick < CacheTicks) return c.lands;
            if (cache.Count > 30) cache.Clear();
            var lands = new HashSet<string>();
            try
            {
                Build(map, lands);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("lands", e);
            }
            cache[map.uniqueID] = (now, lands);
            return lands;
        }

        private static void Build(Map map, HashSet<string> lands)
        {
            string biome = map.Biome?.defName?.ToLowerInvariant() ?? "";
            foreach (var (land, words) in BiomeWords)
                if (words.Any(biome.Contains)) lands.Add(land);
            var tile = map.TileInfo;
            if (tile == null) return;
            if (tile.hilliness >= Hilliness.Mountainous) lands.Add("mountains");
            else if (tile.hilliness == Hilliness.LargeHills) lands.Add("hills");
            if (tile.IsCoastal) lands.Add("coast");
            if (tile is SurfaceTile surface && !surface.Rivers.NullOrEmpty()) lands.Add("river");
            var mutators = tile.Mutators;
            if (mutators == null) return;
            foreach (var m in mutators)
            {
                string n = m?.defName?.ToLowerInvariant();
                if (n == null) continue;
                if (n.Contains("mountain")) lands.Add("mountains");
                if (n.Contains("coast") || n.Contains("fjord") || n.Contains("bay") || n.Contains("peninsula") || n.Contains("archipelago")) lands.Add("coast");
                if (n.Contains("lake")) lands.Add("lake");
                if (n.Contains("river")) lands.Add("river");
            }
        }

        /// <summary>Whether the map is any of these lands (empty: any land at all).</summary>
        public static bool Fits(Map map, List<string> any)
        {
            if (any.NullOrEmpty()) return true;
            var lands = Of(map);
            foreach (var l in any)
                if (!l.NullOrEmpty() && lands.Contains(l.Trim())) return true;
            return false;
        }

        /// <summary>Adds the map's lands to a line's tags, as "land:desert".</summary>
        public static void AddTags(Map map, HashSet<string> tags)
        {
            if (map == null || tags == null) return;
            foreach (var l in Of(map)) tags.Add("land:" + l);
        }
    }
}
