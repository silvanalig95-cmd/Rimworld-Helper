using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The land's own wild plants, sorted for the land's passes (Nature/LandRegrowth.cs, LandSickness.cs): the grasses, the
    /// brush and the trees the map's wild plant spawner grows here (the biome's, the tile's features', every mod's that
    /// gives this land a plant), each with the game's own weight for it here; and which wild plants the sicknesses take
    /// (a wilt any wild tree, a rust the grasses, a rot the wild berry bushes). Worked out once a map and kept until the
    /// game loads again; nothing is saved.
    /// </summary>
    public static class LandPlants
    {
        public const int Grass = 0, Brush = 1, Tree = 2;

        private sealed class Lists
        {
            public readonly List<ThingDef>[] defs = { new List<ThingDef>(), new List<ThingDef>(), new List<ThingDef>() };
            public readonly List<float>[] weights = { new List<float>(), new List<float>(), new List<float>() };
        }

        private static readonly Dictionary<int, Lists> byMap = new Dictionary<int, Lists>();
        private static readonly Dictionary<ThingDef, int> classOf = new Dictionary<ThingDef, int>();

        /// <summary>A new game or a load: worked out again.</summary>
        public static void Forget() => byMap.Clear();

        private static bool Named(ThingDef d, params string[] words)
        {
            string n = (d.defName + " " + d.label).ToLowerInvariant();
            return words.Any(w => n.Contains(w));
        }

        /// <summary>A wild plant the land's passes may set or sicken: no cave plant, stump, crop, special tree or one that needs pollution or the dark.</summary>
        public static bool Wildling(ThingDef d) =>
            d?.plant != null && !d.plant.cavePlant && !d.plant.isStump && !d.plant.RequiresPollution && !d.plant.diesToLight && !d.plant.mustBePermanentDarknessToSow
            && !d.plant.neverBlightable;

        /// <summary>Grass (0), brush (1) or a tree (2), or -1 for a wild plant that is none of them (a flower, a crop gone wild, a berry bush).</summary>
        public static int ClassOf(ThingDef d)
        {
            if (d?.plant == null) return -1;
            if (classOf.TryGetValue(d, out int c)) return c;
            if (d.plant.IsTree) c = Tree;
            else if (d.plant.harvestedThingDef == null && Named(d, "grass", "moss", "sedge", "reed")) c = Grass;
            else if (d.plant.harvestedThingDef == null && Named(d, "bush", "bramble", "shrub", "thicket", "fern", "scrub", "thorn")) c = Brush;
            else c = -1;
            classOf[d] = c;
            return c;
        }

        /// <summary>A wild berry bush (or another wild bush whose harvest is fruit): the rot's hosts.</summary>
        public static bool Berry(ThingDef d) =>
            d?.plant != null && !d.plant.IsTree && d.plant.harvestedThingDef?.ingestible != null
            && (d.plant.harvestedThingDef.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0 && Named(d, "berr", "bush", "fruit");

        private static Lists For(Map map)
        {
            if (byMap.TryGetValue(map.uniqueID, out var l)) return l;
            l = new Lists();
            var spawner = map.wildPlantSpawner;
            var all = spawner?.AllWildPlants ?? map.Biome?.AllWildPlants ?? new List<ThingDef>();
            foreach (var d in all.Distinct())
            {
                if (!Wildling(d)) continue;
                int c = ClassOf(d);
                if (c < 0) continue;
                float w = 0f;
                try
                {
                    w = map.Biome?.CommonalityOfPlant(d) ?? 0f;
                }
                catch
                {
                    w = 0f;
                }
                // A plant the land has only through a feature of the tile (a grove, wild crops) has no weight in the biome.
                if (w <= 0f) w = 0.2f;
                l.defs[c].Add(d);
                l.weights[c].Add(w);
            }
            byMap[map.uniqueID] = l;
            return l;
        }

        /// <summary>The land's wild plants of a class here.</summary>
        public static IReadOnlyList<ThingDef> Of(Map map, int cls) => map == null || cls < 0 || cls > 2 ? new List<ThingDef>() : For(map).defs[cls];

        /// <summary>One of the land's wild plants of a class, by the game's weight for it here; not <paramref name="not"/> when another can be had.</summary>
        public static ThingDef Pick(Map map, int cls, ThingDef not = null)
        {
            if (map == null || cls < 0 || cls > 2) return null;
            var l = For(map);
            var defs = l.defs[cls];
            if (defs.Count == 0) return null;
            float total = 0f;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i] != not || defs.Count == 1)
                    total += l.weights[cls][i];
            float at = Rand.Value * total;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] == not && defs.Count > 1) continue;
                at -= l.weights[cls][i];
                if (at <= 0f) return defs[i];
            }
            return defs.LastOrDefault(d => d != not) ?? defs[0];
        }

        /// <summary>The land's own trees include this kind (a tree that grows wild here).</summary>
        public static bool WildHere(Map map, ThingDef d) => d != null && map != null && For(map).defs.Any(x => x.Contains(d));
    }
}
