using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>One item on the inventory panel, without game types (for the tests): its count, change, group and order.</summary>
    public struct StockEntry
    {
        public string key, label;
        public int count;
        /// <summary>The change since about the same hour yesterday; only meaningful with <see cref="hasChange"/>.</summary>
        public int change;
        public bool hasChange, pinned;
        /// <summary>The game's resource category (its place among the resource list's roots) and the item's place in the game's list.</summary>
        public int group, order;
    }

    /// <summary>
    /// The inventory panel's rules, free of game types so Tools/Tests can check them: the order of the items (pinned
    /// first, then by the game's categories, by name, by count, or by the biggest change), which snapshot of the counts
    /// counts as "yesterday", the change since then, the snapshots packed into one line each for the save, and the icon
    /// grid's layout.
    /// </summary>
    public static class HudInventoryMath
    {
        /// <summary>A snapshot of the counts is taken this often (in-game hours).</summary>
        public const int SnapshotHours = 6;

        /// <summary>"Yesterday" is the newest snapshot at least a day old, and not older than this (hours).</summary>
        public const int YesterdayMaxHours = 24 + SnapshotHours;

        /// <summary>
        /// The items in the panel's order: pinned ones first, then by the game's categories and its own order within
        /// them (<see cref="HudSort.Category"/>), by name, the most first, or the biggest change first (gains before losses
        /// of the same size; unknown changes last). Ties keep the game's order.
        /// </summary>
        public static void Sort(List<StockEntry> items, HudSort sort)
        {
            if (items == null) return;
            items.Sort((a, b) =>
            {
                if (a.pinned != b.pinned) return a.pinned ? -1 : 1;
                int c = 0;
                switch (sort)
                {
                    case HudSort.Name:
                        c = string.Compare(a.label ?? "", b.label ?? "", StringComparison.OrdinalIgnoreCase);
                        break;
                    case HudSort.Count:
                        c = b.count.CompareTo(a.count);
                        break;
                    case HudSort.Change:
                        if (a.hasChange != b.hasChange) return a.hasChange ? -1 : 1;
                        c = Math.Abs(b.change).CompareTo(Math.Abs(a.change));
                        if (c == 0) c = b.change.CompareTo(a.change);
                        break;
                    default:
                        c = a.group.CompareTo(b.group);
                        break;
                }
                if (c != 0) return c;
                c = a.group.CompareTo(b.group);
                if (c != 0 && sort != HudSort.Category) return c;
                return a.order.CompareTo(b.order);
            });
        }

        /// <summary>
        /// Which snapshot is "yesterday" for <paramref name="nowHour"/>: the newest at least 24 hours old and at most
        /// <see cref="YesterdayMaxHours"/> old; -1 when there's none (a new colony, or the game skipped ahead).
        /// </summary>
        public static int Yesterday(IList<int> snapshotHours, int nowHour)
        {
            int best = -1, bestHour = int.MinValue;
            for (int i = 0; i < (snapshotHours?.Count ?? 0); i++)
            {
                int age = nowHour - snapshotHours[i];
                if (age < 24 || age > YesterdayMaxHours) continue;
                if (snapshotHours[i] > bestHour)
                {
                    bestHour = snapshotHours[i];
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Whether a snapshot is due: none yet, or <see cref="SnapshotHours"/> since the last (or the clock went back).</summary>
        public static bool SnapshotDue(int lastHour, int nowHour) => lastHour < 0 || nowHour - lastHour >= SnapshotHours || nowHour < lastHour;

        /// <summary>The change in one item since a snapshot: what there is now less what there was (nothing there counts as 0).</summary>
        public static int Change(int now, IDictionary<string, int> then, string key)
        {
            int was = 0;
            if (then != null && key != null) then.TryGetValue(key, out was);
            return now - was;
        }

        /// <summary>A count short enough for a small tile: "950", "4.2k", "12k", "1.3M".</summary>
        public static string ShortCount(int n)
        {
            var ci = CultureInfo.InvariantCulture;
            if (n < 0) return "−" + ShortCount(-n);
            if (n < 1000) return n.ToString(ci);
            if (n < 10000) return (Math.Floor(n / 100.0) / 10.0).ToString("0.#", ci) + "k";
            if (n < 1000000) return (n / 1000).ToString(ci) + "k";
            return (Math.Floor(n / 100000.0) / 10.0).ToString("0.#", ci) + "M";
        }

        /// <summary>"+12", "−5", or "" for no change.</summary>
        public static string Signed(int change)
        {
            if (change == 0) return "";
            return change > 0 ? "+" + change.ToString(CultureInfo.InvariantCulture) : "−" + (-change).ToString(CultureInfo.InvariantCulture);
        }

        // ---------------- Snapshots in the save ----------------

        /// <summary>
        /// One snapshot as one line: "map|hour|defName:count,defName:count" (only what there's some of), so the whole of it
        /// is one value in the save.
        /// </summary>
        public static string Pack(int map, int hour, IEnumerable<KeyValuePair<string, int>> counts)
        {
            var sb = new StringBuilder();
            sb.Append(map.ToString(CultureInfo.InvariantCulture)).Append('|').Append(hour.ToString(CultureInfo.InvariantCulture)).Append('|');
            bool first = true;
            if (counts != null)
                foreach (var kv in counts)
                {
                    if (kv.Value <= 0 || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ',', ':', '|' }) >= 0) continue;
                    if (!first) sb.Append(',');
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                    first = false;
                }
            return sb.ToString();
        }

        /// <summary>A packed snapshot read back; false when the line isn't one.</summary>
        public static bool Unpack(string line, out int map, out int hour, Dictionary<string, int> counts)
        {
            map = -1;
            hour = -1;
            counts?.Clear();
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Split(new[] { '|' }, 3);
            if (parts.Length < 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out map) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)) return false;
            if (counts == null || parts[2].Length == 0) return true;
            foreach (var pair in parts[2].Split(','))
            {
                int colon = pair.LastIndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(pair.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) counts[pair.Substring(0, colon)] = n;
            }
            return true;
        }

        // ---------------- The icon grid ----------------

        /// <summary>The icon grid's rows for <paramref name="count"/> tiles, <paramref name="columns"/> to a row (at least one).</summary>
        public static int GridRows(int count, int columns) => count <= 0 ? 0 : (count + Math.Max(1, columns) - 1) / Math.Max(1, columns);

        /// <summary>Where tile <paramref name="index"/> sits (left to right, then down), from the grid's top-left corner.</summary>
        public static void GridCell(int index, int columns, float tileW, float tileH, float gap, out float x, out float y)
        {
            int cols = Math.Max(1, columns);
            x = index % cols * (tileW + gap);
            y = index / cols * (tileH + gap);
        }

        /// <summary>The grid's width and height for <paramref name="count"/> tiles (no more columns than tiles).</summary>
        public static void GridSize(int count, int columns, float tileW, float tileH, float gap, out float w, out float h)
        {
            int cols = Math.Max(1, Math.Min(columns, Math.Max(1, count)));
            int rows = GridRows(count, cols);
            w = cols * tileW + (cols - 1) * gap;
            h = rows <= 0 ? 0f : rows * tileH + (rows - 1) * gap;
        }
    }
}
