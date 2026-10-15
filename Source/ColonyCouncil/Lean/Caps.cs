using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How much of its history each part of Rimfolk keeps in the save, and the trimming that keeps it there: the newest
    /// first, and where some entries mean more than others, those longer. DESIGN.md ("Save size") lists every saved list
    /// with its cap or pruning rule. Pure (no game types): Tools/Tests checks the trimming.
    /// </summary>
    public static class Caps
    {
        /// <summary>Chronicle entries; the newest <see cref="ChronicleNewest"/> always stay, older milestones outlast older everyday lines.</summary>
        public const int Chronicle = 300;
        public const int ChronicleNewest = 200;
        /// <summary>Journals of people no longer with the colony, kept by their last line (the living keep theirs).</summary>
        public const int DepartedJournals = 30;
        /// <summary>Years of annals.</summary>
        public const int AnnalsYears = 40;
        /// <summary>Pairs of best friends already noted (other mods' best-friend bonds).</summary>
        public const int BestFriendPairs = 200;

        /// <summary>Drops the oldest entries (the list is oldest first) until it holds <paramref name="max"/>. Returns how many went.</summary>
        public static int TrimOldest<T>(List<T> list, int max)
        {
            if (list == null) return 0;
            if (max < 0) max = 0;
            int over = list.Count - max;
            if (over <= 0) return 0;
            list.RemoveRange(0, over);
            return over;
        }

        /// <summary>
        /// Brings a list (oldest first) down to <paramref name="max"/> entries, keeping the newest and the most meaningful:
        /// the newest <paramref name="keepNewest"/> always stay; of the older ones the ordinary go first, oldest first, and
        /// only then the oldest of the meaningful. The order of what stays doesn't change. Returns how many went.
        /// </summary>
        public static int TrimKeeping<T>(List<T> list, int max, Func<T, bool> meaningful, int keepNewest)
        {
            if (list == null) return 0;
            if (max < 0) max = 0;
            int over = list.Count - max;
            if (over <= 0) return 0;
            int protectFrom = Math.Max(0, list.Count - Math.Max(0, keepNewest));
            var drop = new bool[list.Count];
            int dropped = 0;
            for (int i = 0; i < protectFrom && dropped < over; i++)
            {
                if (meaningful != null && meaningful(list[i])) continue;
                drop[i] = true;
                dropped++;
            }
            for (int i = 0; i < list.Count && dropped < over; i++)
            {
                if (drop[i]) continue;
                drop[i] = true;
                dropped++;
            }
            int w = 0;
            for (int r = 0; r < list.Count; r++)
                if (!drop[r])
                    list[w++] = list[r];
            list.RemoveRange(w, list.Count - w);
            return dropped;
        }

        /// <summary>
        /// Brings a list down to <paramref name="max"/> entries by dropping those with the oldest <paramref name="time"/>
        /// first, among the ones that <paramref name="mayGo"/> (the rest always stay, even over the cap). The order of what
        /// stays doesn't change. Returns how many went.
        /// </summary>
        public static int TrimByAge<T>(List<T> list, int max, Func<T, int> time, Func<T, bool> mayGo)
        {
            if (list == null || time == null) return 0;
            if (max < 0) max = 0;
            int over = list.Count - max;
            if (over <= 0) return 0;
            var candidates = new List<int>();
            for (int i = 0; i < list.Count; i++)
                if (mayGo == null || mayGo(list[i]))
                    candidates.Add(i);
            // Oldest first; equal times by their place in the list.
            candidates.Sort((a, b) =>
            {
                int c = time(list[a]).CompareTo(time(list[b]));
                return c != 0 ? c : a.CompareTo(b);
            });
            var drop = new bool[list.Count];
            int dropped = 0;
            for (int k = 0; k < candidates.Count && dropped < over; k++)
            {
                drop[candidates[k]] = true;
                dropped++;
            }
            int w = 0;
            for (int r = 0; r < list.Count; r++)
                if (!drop[r])
                    list[w++] = list[r];
            list.RemoveRange(w, list.Count - w);
            return dropped;
        }

        /// <summary>
        /// Drops the entries of a dictionary of ticks older than <paramref name="window"/> ticks (and any from after
        /// <paramref name="now"/>, which can only come from an older save). Returns how many went.
        /// </summary>
        public static int DropOlderThan<K>(Dictionary<K, int> dict, int now, int window)
        {
            if (dict == null || dict.Count == 0) return 0;
            List<K> gone = null;
            foreach (var kv in dict)
                if (kv.Value > now || now - kv.Value > window)
                    (gone ??= new List<K>()).Add(kv.Key);
            if (gone == null) return 0;
            foreach (var k in gone) dict.Remove(k);
            return gone.Count;
        }
    }
}
