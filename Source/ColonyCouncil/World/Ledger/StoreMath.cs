using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules the stores that moved into the world ledger keep (stage 0 step 3b): when a tie that lapses "after
    /// so many days" lapses, and the dead a people's grudge keeps. Pure (no game types): Tools/Tests checks them.
    /// </summary>
    public static class StoreMath
    {
        /// <summary>
        /// The first tick at which <c>now - since &gt; days</c> holds (the old stores' "more than so many days ago"), as a
        /// tie's until: a tie ended at it by the ledger ends exactly when the old store let it go.
        /// </summary>
        public static int After(int since, float days, int dayTicks = TieRules.DayTicks)
        {
            double span = Math.Floor((double)days * dayTicks);
            double t = since + span + 1;
            return t >= int.MaxValue ? int.MaxValue - 1 : (int)t;
        }

        /// <summary>The old stores' test: more than <paramref name="days"/> days since <paramref name="since"/>.</summary>
        public static bool Past(int now, int since, float days, int dayTicks = TieRules.DayTicks) => now - since > days * dayTicks;
    }

    /// <summary>One of a people's dead held against the colony: in the order the deaths came (over all peoples), when, settled yet, and who.</summary>
    public struct GrudgeDead
    {
        public int order;
        public int tick;
        public bool settled;
        public string name;

        public GrudgeDead(int order, int tick, bool settled, string name)
        {
            this.order = order;
            this.tick = tick;
            this.settled = settled;
            this.name = name ?? "";
        }
    }

    /// <summary>
    /// The dead a people's grudge tie keeps (prisoners who died in the colony's hands): one line each in the tie's key,
    /// "order|tick|settled|name", the count of those not yet settled in its count. Ten at most over all peoples (the
    /// oldest go first, settled or not), as the event tracker kept them; settled ones go at the next day's tidy, and
    /// any after two years. Pure: Tools/Tests checks it.
    /// </summary>
    public static class GrudgeTally
    {
        /// <summary>The dead kept over all peoples together.</summary>
        public const int Max = 10;
        /// <summary>How long a grudge is held: two years.</summary>
        public const int KeepDays = 120;
        /// <summary>A grudge acted on only once it's this old.</summary>
        public const int ActDays = 8;

        public static List<GrudgeDead> Read(string key)
        {
            var list = new List<GrudgeDead>();
            if (string.IsNullOrEmpty(key)) return list;
            foreach (var line in key.Split('\n'))
            {
                var parts = line.Split(new[] { '|' }, 4);
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int order)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tick)) continue;
                list.Add(new GrudgeDead(order, tick, parts[2] == "1", parts[3]));
            }
            return list;
        }

        public static string Write(List<GrudgeDead> dead)
        {
            if (dead == null || dead.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var d in dead)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(d.order.ToString(CultureInfo.InvariantCulture)).Append('|').Append(d.tick.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(d.settled ? '1' : '0').Append('|').Append(Clean(d.name));
            }
            return sb.ToString();
        }

        /// <summary>A name as one line.</summary>
        public static string Clean(string name) => (name ?? "").Replace('\n', ' ').Replace('\r', ' ');

        /// <summary>How many are not settled yet.</summary>
        public static int Open(List<GrudgeDead> dead)
        {
            int n = 0;
            if (dead != null)
                foreach (var d in dead)
                    if (!d.settled)
                        n++;
            return n;
        }

        /// <summary>The order for the next death: after every one kept.</summary>
        public static int NextOrder(IEnumerable<List<GrudgeDead>> all)
        {
            int max = 0;
            if (all != null)
                foreach (var list in all)
                    if (list != null)
                        foreach (var d in list)
                            max = Math.Max(max, d.order);
            return max + 1;
        }

        /// <summary>
        /// The orders that go to keep <see cref="Max"/> over all peoples: the oldest first, settled or not (the event
        /// tracker dropped its first entries the same way).
        /// </summary>
        public static HashSet<int> OverMax(IEnumerable<List<GrudgeDead>> all, int max = Max)
        {
            var orders = new List<int>();
            if (all != null)
                foreach (var list in all)
                    if (list != null)
                        foreach (var d in list)
                            orders.Add(d.order);
            orders.Sort();
            var drop = new HashSet<int>();
            for (int i = 0; i < orders.Count - max; i++) drop.Add(orders[i]);
            return drop;
        }

        /// <summary>Gone at the day's tidy: settled, or held longer than <see cref="KeepDays"/>.</summary>
        public static bool Forgotten(GrudgeDead d, int now, int dayTicks = TieRules.DayTicks) => d.settled || now - d.tick > (long)KeepDays * dayTicks;

        /// <summary>Settles the dead of this name; returns how many.</summary>
        public static int Settle(List<GrudgeDead> dead, string name)
        {
            int n = 0;
            if (dead == null) return 0;
            for (int i = 0; i < dead.Count; i++)
            {
                var d = dead[i];
                if (d.name != Clean(name)) continue;
                d.settled = true;
                dead[i] = d;
                n++;
            }
            return n;
        }
    }
}
