using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of the newer wishes and life moments (Wishes/WishWorkers_LifeNew.cs, Lives/LifeNew.cs), free of
    /// game types so the tests can cover it (Tools/Tests/LifeNewTests.cs): which crop someone misses from home, the
    /// way between a bed and the table and its open steps, kept in a wish as text, two cups in the same hour, books
    /// read once each, and a slip back to the drug.
    /// </summary>
    public static class LifeNewMath
    {
        /// <summary>Two cups drunk within this many ticks of each other count as the same hour (an in-game hour).</summary>
        public const int HourTicks = 2500;

        /// <summary>At most this many books remembered as read, all colonists together (the oldest go first).</summary>
        public const int MaxRead = 600;

        /// <summary>A way's open steps worth a roof: at least this many, and at most so many (a long way is a building project, not a wish).</summary>
        public const int MinOpen = 3, MaxOpen = 40;

        /// <summary>Cells a way's search may look at before it gives up (a quarter of a large map).</summary>
        public const int MaxSearch = 16000;

        /// <summary>
        /// Which of these someone favours: the same one each time for the same person while the choice stays the same
        /// (a crop from home), by a hash of their seed and each key. -1 when there's nothing to choose.
        /// </summary>
        public static int Favourite(int seed, IReadOnlyList<int> keys)
        {
            if (keys == null || keys.Count == 0) return -1;
            int best = -1;
            uint bestHash = uint.MaxValue;
            for (int i = 0; i < keys.Count; i++)
            {
                uint h = Mix((uint)seed * 2654435761u ^ (uint)keys[i]);
                if (best < 0 || h < bestHash)
                {
                    best = i;
                    bestHash = h;
                }
            }
            return best;
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        /// <summary>Two cups in the same hour: drunk within an hour of each other, either way round.</summary>
        public static bool SameHour(int tickA, int tickB) => Math.Abs(tickA - tickB) <= HourTicks;

        /// <summary>
        /// A drink's name says coffee or tea (or a kind of either): whole words only, so "steak" isn't tea and
        /// "psychite tea" is (the caller rules out what a teetotaler can't have).
        /// </summary>
        public static bool IsCupWord(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            foreach (var raw in label.ToLowerInvariant().Split(' ', '-', '_', '(', ')', ','))
            {
                switch (raw)
                {
                    case "coffee":
                    case "tea":
                    case "teas":
                    case "coffees":
                    case "chai":
                    case "espresso":
                    case "matcha":
                    case "latte":
                    case "cappuccino":
                    case "yerba":
                        return true;
                }
            }
            return false;
        }

        /// <summary>A slip: still addicted, and no longer in withdrawal, so they have had the drug again.</summary>
        public static bool Relapsed(bool addicted, bool inWithdrawal) => addicted && !inWithdrawal;

        /// <summary>
        /// A book read: remembered as "who|title", true the first time. Kept to <paramref name="cap"/> entries, the
        /// oldest dropped first.
        /// </summary>
        public static bool AddRead(List<string> read, string who, string title, int cap = MaxRead)
        {
            if (read == null || string.IsNullOrEmpty(who) || string.IsNullOrEmpty(title)) return false;
            string key = who + "|" + title;
            if (read.Contains(key)) return false;
            read.Add(key);
            while (read.Count > Math.Max(1, cap)) read.RemoveAt(0);
            return true;
        }

        /// <summary>Different books this one has read, of those remembered.</summary>
        public static int ReadCount(List<string> read, string who)
        {
            if (read == null || string.IsNullOrEmpty(who)) return 0;
            string prefix = who + "|";
            int n = 0;
            foreach (var k in read)
                if (k.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        // ---------------- A way between two places ----------------

        /// <summary>
        /// The shortest walk from <paramref name="start"/> to <paramref name="goal"/> on a grid of cells numbered
        /// z * width + x, stepping to any of the eight neighbours that can be walked on, diagonally only past two
        /// walkable sides (as people walk). The cells in order, both ends included; empty when the goal can't be
        /// reached within <paramref name="maxSearch"/> cells.
        /// </summary>
        public static List<int> Way(int width, int height, Func<int, bool> walkable, int start, int goal, int maxSearch = MaxSearch)
        {
            var path = new List<int>();
            int n = width * height;
            if (width <= 0 || height <= 0 || walkable == null || start < 0 || goal < 0 || start >= n || goal >= n) return path;
            if (start == goal)
            {
                path.Add(start);
                return path;
            }
            var from = new Dictionary<int, int> { [start] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            int seen = 0;
            bool found = false;
            while (queue.Count > 0 && seen++ < maxSearch)
            {
                int c = queue.Dequeue();
                int cx = c % width, cz = c / width;
                for (int dz = -1; dz <= 1 && !found; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= width || z >= height) continue;
                        int next = z * width + x;
                        if (from.ContainsKey(next)) continue;
                        // The goal may be a table or a bed: reached by standing next to it.
                        if (next != goal && !walkable(next)) continue;
                        if (dx != 0 && dz != 0 && (!walkable(cz * width + x) || !walkable(z * width + cx))) continue;
                        from[next] = c;
                        if (next == goal)
                        {
                            found = true;
                            break;
                        }
                        queue.Enqueue(next);
                    }
                if (found) break;
            }
            if (!found) return path;
            for (int c = goal; c != -1; c = from[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>A way's open steps are worth a wish: a few, not a long road.</summary>
        public static bool WorthARoof(int open) => open >= MinOpen && open <= MaxOpen;

        /// <summary>Cells as text for a wish to keep ("12.40 13.40"), and back.</summary>
        public static string Encode(IEnumerable<(int x, int z)> cells)
        {
            var sb = new StringBuilder();
            if (cells == null) return "";
            foreach (var (x, z) in cells)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(x).Append('.').Append(z);
            }
            return sb.ToString();
        }

        public static List<(int x, int z)> Decode(string text)
        {
            var cells = new List<(int, int)>();
            if (string.IsNullOrEmpty(text)) return cells;
            foreach (var part in text.Split(' '))
            {
                int dot = part.IndexOf('.');
                if (dot <= 0) continue;
                if (int.TryParse(part.Substring(0, dot), out int x) && int.TryParse(part.Substring(dot + 1), out int z)) cells.Add((x, z));
            }
            return cells;
        }
    }
}
