using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// A land's wild-animal records merged the way Rimfolk reads them when the game can't (Animals/BiomeAnimals.cs): one
    /// commonality for each animal, and an animal listed more than once (two mods each adding it to the same land) given
    /// the mean of its records, as the game itself merges a plant that both its land and its own def list. A record
    /// without an animal (its def missing) is left out and counted. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class BiomeAnimalsMath
    {
        /// <summary>
        /// One commonality for each animal in <paramref name="records"/>. Each animal listed more than once is added to
        /// <paramref name="repeated"/> once, in the order its second record comes; <paramref name="unnamed"/> counts the
        /// records without an animal. The game's own dictionary throws on either.
        /// </summary>
        public static Dictionary<K, float> Merge<K>(IEnumerable<KeyValuePair<K, float>> records, List<K> repeated, out int unnamed) where K : class
        {
            unnamed = 0;
            var sums = new Dictionary<K, float>();
            var counts = new Dictionary<K, int>();
            if (records != null)
                foreach (var r in records)
                {
                    if (r.Key == null)
                    {
                        unnamed++;
                        continue;
                    }
                    if (counts.TryGetValue(r.Key, out int n))
                    {
                        if (n == 1) repeated?.Add(r.Key);
                        counts[r.Key] = n + 1;
                        sums[r.Key] += r.Value;
                    }
                    else
                    {
                        counts[r.Key] = 1;
                        sums[r.Key] = r.Value;
                    }
                }
            var merged = new Dictionary<K, float>(sums.Count);
            foreach (var kv in sums) merged[kv.Key] = kv.Value / counts[kv.Key];
            return merged;
        }

        /// <summary>The animals the log names, each once and at most <paramref name="most"/> of them: "RG_Dodo, RG_Thrinaxodon and 2 more".</summary>
        public static string Names(IEnumerable<string> names, int most = 6)
        {
            var list = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            if (list.Count <= most) return string.Join(", ", list);
            return string.Join(", ", list.Take(most)) + " and " + (list.Count - most) + " more";
        }

        /// <summary>
        /// What in a land's lists the game can't read, for the log, or null when nothing: the animals listed more than
        /// once, the records naming an animal whose def is missing, the pawn kinds without a race.
        /// </summary>
        public static string Problem(IEnumerable<string> repeated, int unnamed, int raceless)
        {
            var parts = new List<string>();
            string names = Names(repeated);
            if (names.Length > 0) parts.Add(names + " listed more than once (two mods probably add the same animal)");
            if (unnamed > 0) parts.Add(unnamed == 1 ? "a record naming an animal whose def is missing" : unnamed + " records naming animals whose defs are missing");
            if (raceless > 0) parts.Add(raceless == 1 ? "a pawn kind without a race" : raceless + " pawn kinds without a race");
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }
    }
}
