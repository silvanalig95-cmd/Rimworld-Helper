using System;
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
        /// One list mended so the game can read it (Animals/BiomeAnimalsMend.cs): each animal kept once, at its first
        /// record, with the mean of its records; records without an animal left out and counted in
        /// <paramref name="unnamed"/>. Returns each kept record's index in the list and its commonality, in the list's
        /// order. Each animal listed more than once is added to <paramref name="repeated"/> once.
        /// </summary>
        public static List<KeyValuePair<int, float>> KeepOnce<K>(IList<KeyValuePair<K, float>> records, List<K> repeated, out int unnamed) where K : class
        {
            unnamed = 0;
            var order = new List<K>();
            var first = new Dictionary<K, int>();
            var values = new Dictionary<K, List<float>>();
            for (int i = 0; i < (records?.Count ?? 0); i++)
            {
                var k = records[i].Key;
                if (k == null)
                {
                    unnamed++;
                    continue;
                }
                if (values.TryGetValue(k, out var list))
                {
                    if (list.Count == 1) repeated?.Add(k);
                    list.Add(records[i].Value);
                }
                else
                {
                    values[k] = new List<float> { records[i].Value };
                    first[k] = i;
                    order.Add(k);
                }
            }
            return order.Select(k => new KeyValuePair<int, float>(first[k], Mean(values[k]))).ToList();
        }

        /// <summary>A land's own list and the races naming it, mended (<see cref="MendPlain{K,R}"/>).</summary>
        public sealed class MendedLand<K, R>
        {
            /// <summary>The records of the land's own list that stay: index in the list, commonality.</summary>
            public readonly List<KeyValuePair<int, float>> own = new List<KeyValuePair<int, float>>();
            /// <summary>Each race naming the land: the one commonality its single record for the land keeps.</summary>
            public readonly Dictionary<R, float> races = new Dictionary<R, float>();
            /// <summary>Animals of the land's own list listed more than once there, or there and by their race.</summary>
            public readonly List<K> repeatedKinds = new List<K>();
            /// <summary>Races naming the land more than once.</summary>
            public readonly List<R> repeatedRaces = new List<R>();
            public int unnamed;
        }

        /// <summary>
        /// The game's first dictionary of a land's animals, mended so it meets each animal once. The game adds the land's
        /// own list (<paramref name="own"/>, in order) and then, for every pawn kind, its race's records naming the land
        /// (<paramref name="raceRecords"/>, each race with the commonality of each of its records for this land). An
        /// animal in the land's list twice keeps its first record at the mean. An animal in the land's list whose race
        /// names the land too loses its land records: the race's record is the one the game's loop reads for every pawn
        /// kind of that race, so it stays; it takes the mean of all of them when the race has only this one kind
        /// (<paramref name="kindsOf"/>), and keeps its own number otherwise, so the race's other kinds don't change. A
        /// race naming the land more than once keeps one record at the mean. Records without an animal are left out.
        /// </summary>
        public static MendedLand<K, R> MendPlain<K, R>(IList<KeyValuePair<K, float>> own, IEnumerable<KeyValuePair<R, float>> raceRecords,
                                                        Func<K, R> raceOf, Func<R, int> kindsOf) where K : class where R : class
        {
            var land = new MendedLand<K, R>();
            var pools = new Dictionary<R, List<float>>();
            var raceOrder = new List<R>();
            if (raceRecords != null)
                foreach (var rr in raceRecords)
                {
                    if (rr.Key == null) continue;
                    if (pools.TryGetValue(rr.Key, out var pool))
                    {
                        if (pool.Count == 1) land.repeatedRaces.Add(rr.Key);
                        pool.Add(rr.Value);
                    }
                    else
                    {
                        pools[rr.Key] = new List<float> { rr.Value };
                        raceOrder.Add(rr.Key);
                    }
                }
            var order = new List<K>();
            var first = new Dictionary<K, int>();
            var values = new Dictionary<K, List<float>>();
            for (int i = 0; i < (own?.Count ?? 0); i++)
            {
                var k = own[i].Key;
                if (k == null)
                {
                    land.unnamed++;
                    continue;
                }
                if (values.TryGetValue(k, out var list)) list.Add(own[i].Value);
                else
                {
                    values[k] = new List<float> { own[i].Value };
                    first[k] = i;
                    order.Add(k);
                }
            }
            foreach (var k in order)
            {
                var v = values[k];
                var race = raceOf?.Invoke(k);
                if (race != null && pools.TryGetValue(race, out var pool))
                {
                    land.repeatedKinds.Add(k);
                    if ((kindsOf?.Invoke(race) ?? 1) <= 1) pool.AddRange(v);
                    continue;
                }
                if (v.Count > 1) land.repeatedKinds.Add(k);
                land.own.Add(new KeyValuePair<int, float>(first[k], Mean(v)));
            }
            foreach (var race in raceOrder) land.races[race] = Mean(pools[race]);
            return land;
        }

        /// <summary>
        /// The log's line once the lands are mended, or null when nothing was: which animals were listed more than once,
        /// in which lands, and what Rimfolk did about it.
        /// </summary>
        public static string MendLine(IEnumerable<string> animals, IEnumerable<string> lands, int unnamed)
        {
            string a = Names(animals), l = Names(lands);
            if (l.Length == 0 || a.Length == 0 && unnamed <= 0) return null;
            string missing = unnamed == 1 ? "a record naming an animal whose def is missing" : unnamed + " records naming animals whose defs are missing";
            string what = a.Length > 0 && unnamed > 0 ? a + " listed more than once (two mods adding the same animal), and " + missing
                : a.Length > 0 ? a + " listed more than once (two mods adding the same animal)"
                : missing;
            string done = a.Length > 0 && unnamed > 0 ? "Rimfolk kept each repeated animal once, at the mean of its listings, and left the missing ones out"
                : a.Length > 0 ? "Rimfolk kept each one once, at the mean of its listings"
                : "Rimfolk left them out";
            return "Wild animals of " + l + ": " + what + ". " + done + ", so the game can read these lands' wild animals in full.";
        }

        private static float Mean(List<float> values)
        {
            float sum = 0f;
            foreach (float v in values) sum += v;
            return values.Count == 0 ? 0f : sum / values.Count;
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
