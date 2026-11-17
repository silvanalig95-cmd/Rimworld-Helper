using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The lands' wild-animal lists mended once at load, for the whole game (the player's startup log, 17 November 2026).
    /// When two mods add the same animal to the same land (ReGrowth's RG_Dodo, RG_Thrinaxodon and RG_Synthetion in five of
    /// the player's lands), the game's dictionary of that land's animals throws at the repeat and stays half filled: the
    /// game's own wild-animal spawns there, Giddy-Up's mounts and everything else that asks miss every animal after it.
    /// Here, before any mod's startup class asks (queued from the mod's constructor, which runs before them), each land's
    /// own list, the races naming it, and its pollution and coastal lists are mended so the game meets each animal once
    /// (<see cref="BiomeAnimalsMath.MendPlain{K,R}"/>, <see cref="BiomeAnimalsMath.KeepOnce{K}"/>): a repeat is kept once at
    /// the mean of its listings, a record whose animal's def is missing is dropped, and the game's cached dictionaries of
    /// a mended land are cleared so they are built again from the mended lists. A pawn kind without a race can't be
    /// mended this way; Animals/BiomeAnimals.cs still reads around it. No Harmony: only the defs' own lists change.
    /// </summary>
    public static class BiomeAnimalsMend
    {
        private static bool done;

        private static readonly string[] Caches = { "cachedAnimalCommonalities", "cachedPollutionAnimalCommonalities", "cachedCoastalAnimalCommonalities" };

        /// <summary>Mends every land's lists, once; later calls do nothing. Never throws.</summary>
        public static void Run()
        {
            if (done) return;
            done = true;
            try
            {
                Mend();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wild animals: mending the lands' lists", e);
            }
        }

        private static void Mend()
        {
            // How many pawn kinds each race serves, and the races that name lands.
            var kindsOfRace = new Dictionary<ThingDef, int>();
            foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                var race = k?.race;
                if (race == null) continue;
                kindsOfRace[race] = (kindsOfRace.TryGetValue(race, out int n) ? n : 0) + 1;
            }
            var wildRaces = kindsOfRace.Keys.Where(r => r.race?.wildBiomes != null).ToList();
            var caches = Caches.Select(name => AccessTools.Field(typeof(BiomeDef), name)).Where(f => f != null).ToList();

            var lands = new List<string>();
            var animals = new List<string>();
            int unnamed = 0;
            foreach (var biome in DefDatabase<BiomeDef>.AllDefsListForReading)
            {
                if (biome == null) continue;
                bool changed = false;
                var names = new List<string>();

                // The land's own list and the races naming it: the game's first dictionary.
                var own = BiomeAnimals.WildRecords?.Invoke(biome);
                var raceRecords = new List<KeyValuePair<ThingDef, float>>();
                foreach (var race in wildRaces)
                    foreach (var w in race.race.wildBiomes)
                        if (w != null && w.biome == biome)
                            raceRecords.Add(new KeyValuePair<ThingDef, float>(race, w.commonality));
                var mended = BiomeAnimalsMath.MendPlain(Pairs(own), raceRecords, k => k.race, r => kindsOfRace.TryGetValue(r, out int c) ? c : 1);
                if (own != null) changed |= Apply(own, mended.own);
                foreach (var kv in mended.races) changed |= ApplyRace(kv.Key, biome, kv.Value);
                unnamed += mended.unnamed;
                names.AddRange(mended.repeatedKinds.Select(k => k.defName));
                names.AddRange(mended.repeatedRaces.Select(r => r.defName));

                // The pollution and coastal lists, each its own dictionary, read only with their expansion.
                if (ModsConfig.BiotechActive) changed |= MendList(BiomeAnimals.PollutionRecords?.Invoke(biome), names, ref unnamed);
                if (ModsConfig.OdysseyActive) changed |= MendList(BiomeAnimals.CoastalRecords?.Invoke(biome), names, ref unnamed);

                if (!changed) continue;
                foreach (var f in caches) f.SetValue(biome, null);
                lands.Add(biome.defName);
                animals.AddRange(names);
            }
            string line = BiomeAnimalsMath.MendLine(animals, lands, unnamed);
            if (line != null) Log.Message("[Rimfolk] " + line);
        }

        private static List<KeyValuePair<PawnKindDef, float>> Pairs(List<BiomeAnimalRecord> list) =>
            list == null
                ? new List<KeyValuePair<PawnKindDef, float>>()
                : list.Select(r => new KeyValuePair<PawnKindDef, float>(r?.animal, r?.commonality ?? 0f)).ToList();

        /// <summary>Keeps only the records <paramref name="keep"/> names, each at its commonality; true when anything changed.</summary>
        private static bool Apply(List<BiomeAnimalRecord> list, List<KeyValuePair<int, float>> keep)
        {
            bool changed = keep.Count != list.Count;
            var kept = new List<BiomeAnimalRecord>(keep.Count);
            foreach (var kv in keep)
            {
                var r = list[kv.Key];
                if (r.commonality != kv.Value)
                {
                    r.commonality = kv.Value;
                    changed = true;
                }
                kept.Add(r);
            }
            if (!changed) return false;
            list.Clear();
            list.AddRange(kept);
            return true;
        }

        /// <summary>The race keeps one record naming the land, at <paramref name="commonality"/>; true when anything changed.</summary>
        private static bool ApplyRace(ThingDef race, BiomeDef biome, float commonality)
        {
            var records = race.race.wildBiomes;
            var mine = records.Where(w => w != null && w.biome == biome).ToList();
            if (mine.Count == 0) return false;
            bool changed = mine.Count > 1 || mine[0].commonality != commonality;
            if (!changed) return false;
            mine[0].commonality = commonality;
            for (int i = 1; i < mine.Count; i++) records.Remove(mine[i]);
            return true;
        }

        private static bool MendList(List<BiomeAnimalRecord> list, List<string> names, ref int unnamed)
        {
            if (list == null) return false;
            var repeated = new List<PawnKindDef>();
            var keep = BiomeAnimalsMath.KeepOnce(Pairs(list), repeated, out int missing);
            unnamed += missing;
            names.AddRange(repeated.Select(k => k.defName));
            return Apply(list, keep);
        }
    }
}
