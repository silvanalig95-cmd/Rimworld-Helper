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
        /// <summary>
        /// Kept at the bottom whatever the order, below everything but the pinned: 0 for the game's counted stock, then the
        /// weapons in storage (<see cref="HudInventoryMath.TailWeapons"/>), then the apparel (<see cref="HudInventoryMath.TailApparel"/>).
        /// </summary>
        public int tail;
        /// <summary>A weapon's or apparel's kind for the filters: ranged or melee; armour, utility or clothes.</summary>
        public string kind;
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
        /// of the same size; unknown changes last), the weapons and then the apparel (<see cref="StockEntry.tail"/>) below
        /// the rest, each in the same order. Ties keep the game's order.
        /// </summary>
        public static void Sort(List<StockEntry> items, HudSort sort)
        {
            if (items == null) return;
            items.Sort((a, b) =>
            {
                if (a.pinned != b.pinned) return a.pinned ? -1 : 1;
                if (a.tail != b.tail) return a.tail.CompareTo(b.tail);
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

        /// <summary>
        /// A key in a snapshot that says it counted the weapons in storage too (they came later): without it, a weapon's
        /// change since then is unknown rather than all of it new. Never a def's name.
        /// </summary>
        public const string WeaponsMark = "~weapons";

        /// <summary>The same for the apparel in storage.</summary>
        public const string ApparelMark = "~apparel";

        /// <summary>The snapshot's mark for a tail's things.</summary>
        public static string Mark(int tail) => tail == TailApparel ? ApparelMark : WeaponsMark;

        // ---------------- The weapons and apparel in storage, and the filters ----------------

        /// <summary>The list's parts from the top: the game's counted stock, the weapons in storage, the apparel in storage.</summary>
        public const int TailStock = 0, TailWeapons = 1, TailApparel = 2;

        public const string FilterAll = "", FilterStock = "stock", FilterWeapons = "weapons", FilterRanged = "ranged", FilterMelee = "melee",
            FilterApparel = "apparel", FilterArmour = "armour", FilterUtility = "utility", FilterClothes = "clothes";

        /// <summary>The filters in their menu's order, each family followed by its kinds (the player: "perhaps i can then have filters for those as well").</summary>
        public static readonly string[] Filters =
            { FilterAll, FilterStock, FilterWeapons, FilterRanged, FilterMelee, FilterApparel, FilterArmour, FilterUtility, FilterClothes };

        /// <summary>A kind's family (ranged and melee are weapons; armour, utility and clothes are apparel), else the filter itself.</summary>
        public static string Family(string filter)
        {
            switch (filter)
            {
                case FilterRanged:
                case FilterMelee:
                    return FilterWeapons;
                case FilterArmour:
                case FilterUtility:
                case FilterClothes:
                    return FilterApparel;
                default:
                    return Array.IndexOf(Filters, filter) >= 0 ? filter : FilterAll;
            }
        }

        /// <summary>
        /// Whether an item shows under a filter: everything shows the stock and, where their part is on, the weapons and
        /// apparel; a family shows only its own; a kind only its own kind. A filter chosen shows its things even with their
        /// part off (the part is about what "everything" holds).
        /// </summary>
        public static bool FilterShows(string filter, int tail, string kind, bool partOn)
        {
            switch (filter)
            {
                case FilterStock: return tail == TailStock;
                case FilterWeapons: return tail == TailWeapons;
                case FilterApparel: return tail == TailApparel;
                case FilterRanged:
                case FilterMelee:
                    return tail == TailWeapons && kind == filter;
                case FilterArmour:
                case FilterUtility:
                case FilterClothes:
                    return tail == TailApparel && kind == filter;
                default:
                    return tail == TailStock || partOn;
            }
        }

        /// <summary>
        /// An apparel kind's filter: by the apparel rating's role where it has one (body armour, a helmet or a shield is
        /// armour, utility its own), else by the game's categories (armour, armoured headgear, utility); clothes otherwise.
        /// </summary>
        public static string ApparelKind(string role, bool armourCategory, bool utilityCategory)
        {
            if (role == "body armour" || role == "helmet" || role == "off-hand shield") return FilterArmour;
            if (role == "utility") return FilterUtility;
            if (!string.IsNullOrEmpty(role)) return FilterClothes;
            return armourCategory ? FilterArmour : utilityCategory ? FilterUtility : FilterClothes;
        }

        /// <summary>The order inside a tail: ranged before melee, armour before utility before clothes.</summary>
        public static int KindRank(string kind) =>
            kind == FilterRanged || kind == FilterArmour ? 0 : kind == FilterUtility ? 1 : 2;

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
