using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Which wild animals live in this land, and how common is this one here?", without throwing. Everything in
    /// Rimfolk that asks goes through here rather than to <c>BiomeDef.AllWildAnimals</c> and
    /// <c>BiomeDef.CommonalityOfAnimal</c>.
    ///
    /// The game builds a dictionary of a land's animals on first use (the land's own list and the lands each race names,
    /// then its pollution and its coastal list, each their own) and throws when an animal is in a list twice: two mods
    /// adding the same animal to the same land (seen with ReGrowth's RG_Dodo and RG_Thrinaxodon). It throws too on a record
    /// whose animal's def is missing and on a pawn kind without a race. After that first throw its dictionary stays half
    /// filled, so later answers come back short without a word. So each land's lists are read here once: where nothing in
    /// them would make the game throw, every answer is the game's own, as before; where something would, or the game's
    /// answer throws anyway, the answers come from the lists read here, the repeats merged
    /// (<see cref="BiomeAnimalsMath.Merge"/>), with one line in the log naming the land and what is wrong in it.
    /// </summary>
    public static class BiomeAnimals
    {
        private sealed class Land
        {
            /// <summary>The land's own list and the races naming it: what <c>CommonalityOfAnimal</c> reads.</summary>
            public Dictionary<PawnKindDef, float> plain = new Dictionary<PawnKindDef, float>();
            public Dictionary<PawnKindDef, float> pollution = new Dictionary<PawnKindDef, float>();
            public Dictionary<PawnKindDef, float> coastal = new Dictionary<PawnKindDef, float>();
            /// <summary>The game's commonality can't be had: something in the land's own list or the races' makes it throw.</summary>
            public bool plainOurs;
            /// <summary>The game's list of the land's animals can't be had: something in any of the three makes it throw.</summary>
            public bool listOurs;
            /// <summary>The animals as read here, in the game's order (the kinds' own); made when first asked.</summary>
            public List<PawnKindDef> kinds;
        }

        private static readonly Dictionary<BiomeDef, Land> lands = new Dictionary<BiomeDef, Land>();

        private static readonly AccessTools.FieldRef<BiomeDef, List<BiomeAnimalRecord>> WildRecords = Field("wildAnimals");
        private static readonly AccessTools.FieldRef<BiomeDef, List<BiomeAnimalRecord>> PollutionRecords = Field("pollutionWildAnimals");
        private static readonly AccessTools.FieldRef<BiomeDef, List<BiomeAnimalRecord>> CoastalRecords = Field("coastalWildAnimals");

        /// <summary>The wild animals of this land, as <c>BiomeDef.AllWildAnimals</c> lists them; none for no land.</summary>
        public static IEnumerable<PawnKindDef> Of(BiomeDef biome)
        {
            if (biome == null) return Enumerable.Empty<PawnKindDef>();
            var land = LandOf(biome);
            if (!land.listOurs)
            {
                try
                {
                    return biome.AllWildAnimals.ToList();
                }
                catch (Exception e)
                {
                    Broke(biome, land, e);
                }
            }
            return land.kinds ??= Kinds(land);
        }

        /// <summary>This animal's commonality in this land, as <c>BiomeDef.CommonalityOfAnimal</c> gives it (0 where it doesn't live).</summary>
        public static float Commonality(BiomeDef biome, PawnKindDef kind)
        {
            if (biome == null || kind == null) return 0f;
            var land = LandOf(biome);
            if (!land.plainOurs)
            {
                try
                {
                    return biome.CommonalityOfAnimal(kind);
                }
                catch (Exception e)
                {
                    Broke(biome, land, e);
                }
            }
            return land.plain.TryGetValue(kind, out float c) ? c : 0f;
        }

        /// <summary>The animal lives wild in this land: a commonality above nothing.</summary>
        public static bool LivesIn(BiomeDef biome, PawnKindDef kind) => Commonality(biome, kind) > 0f;

        /// <summary>A land's lists, read once; where something in them makes the game throw, its answers are read here from then on.</summary>
        private static Land LandOf(BiomeDef biome)
        {
            if (lands.TryGetValue(biome, out var land)) return land;
            land = new Land();
            lands[biome] = land;
            var repeated = new List<PawnKindDef>();
            string problem;
            try
            {
                int raceless = 0;
                land.plain = BiomeAnimalsMath.Merge(PlainRecords(biome, ref raceless), repeated, out int unnamed);
                land.plainOurs = repeated.Count > 0 || unnamed > 0 || raceless > 0;
                int unnamedPollution = 0, unnamedCoastal = 0;
                if (ModsConfig.BiotechActive) land.pollution = BiomeAnimalsMath.Merge(Records(biome, PollutionRecords), repeated, out unnamedPollution);
                if (ModsConfig.OdysseyActive) land.coastal = BiomeAnimalsMath.Merge(Records(biome, CoastalRecords), repeated, out unnamedCoastal);
                land.listOurs = land.plainOurs || repeated.Count > 0 || unnamedPollution + unnamedCoastal > 0;
                problem = BiomeAnimalsMath.Problem(repeated.Select(k => k.defName), unnamed + unnamedPollution + unnamedCoastal, raceless);
            }
            catch (Exception e)
            {
                // The lists couldn't be read: the game answers, and if it throws, what was read here stands.
                land.plainOurs = land.listOurs = false;
                CouncilLog.ErrorOnce("wild animals: reading the lists of " + biome.defName, e);
                return land;
            }
            if (land.listOurs)
                CouncilLog.WarnOnce("the game can't read the wild animals of " + biome.defName + ": " + problem
                                    + ". Rimfolk reads this land's animals from its lists itself, each repeat merged into one and the rest left out.");
            return land;
        }

        /// <summary>The game's answer threw though nothing in the lists should make it: the lists read here answer from now on.</summary>
        private static void Broke(BiomeDef biome, Land land, Exception e)
        {
            land.plainOurs = land.listOurs = true;
            CouncilLog.WarnOnce("the game's reading of the wild animals of " + biome.defName + " failed (" + e.GetType().Name + ": " + e.Message
                                + "). Rimfolk reads this land's animals from its lists itself.");
        }

        /// <summary>
        /// What the game puts in its first dictionary, in its order: the land's own list, then every kind whose race names
        /// this land. A kind without a race is counted in <paramref name="raceless"/> (the game's loop throws on it).
        /// </summary>
        private static List<KeyValuePair<PawnKindDef, float>> PlainRecords(BiomeDef biome, ref int raceless)
        {
            var list = Records(biome, WildRecords).ToList();
            foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (k == null) continue;
                var race = k.race?.race;
                if (race == null)
                {
                    raceless++;
                    continue;
                }
                if (race.wildBiomes == null) continue;
                foreach (var w in race.wildBiomes)
                    if (w != null && w.biome == biome)
                        list.Add(new KeyValuePair<PawnKindDef, float>(k, w.commonality));
            }
            return list;
        }

        private static IEnumerable<KeyValuePair<PawnKindDef, float>> Records(BiomeDef biome, AccessTools.FieldRef<BiomeDef, List<BiomeAnimalRecord>> field)
        {
            var list = field == null ? null : field(biome);
            if (list == null) yield break;
            foreach (var r in list)
                if (r != null)
                    yield return new KeyValuePair<PawnKindDef, float>(r.animal, r.commonality);
        }

        /// <summary>The land's animals as the game lists them, in the kinds' own order: those with a commonality in any of the three.</summary>
        private static List<PawnKindDef> Kinds(Land land)
        {
            var list = new List<PawnKindDef>();
            foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
                if (k != null && (Above(land.plain, k) || Above(land.pollution, k) || Above(land.coastal, k)))
                    list.Add(k);
            return list;
        }

        private static bool Above(Dictionary<PawnKindDef, float> d, PawnKindDef k) => d.TryGetValue(k, out float c) && c > 0f;

        private static AccessTools.FieldRef<BiomeDef, List<BiomeAnimalRecord>> Field(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<BiomeDef, List<BiomeAnimalRecord>>(name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wild animals: the game's field BiomeDef." + name, e);
                return null;
            }
        }
    }
}
