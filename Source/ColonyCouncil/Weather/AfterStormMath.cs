using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the work party after a disaster (Weather/AfterStorm.cs, C19 of docs/content-update-proposals.md): which
    /// disasters there are, how long after one ends the party sets out, how long it may wait for the fires to go out, and the
    /// short lines the weather record keeps them in (the parties waiting, the year's count for the annals). No game types, so
    /// Tools/Tests checks it.
    /// </summary>
    public static class AfterStormMath
    {
        /// <summary>
        /// The disasters, in the annals' order: an earthquake (Seasonal Weather's or Vanilla Events Expanded's), a tornado, a
        /// flashstorm, hail, a flood (Odyssey's seasonal flood, Vanilla Landmarks Expanded's tidal flooding).
        /// </summary>
        public const string Quake = "quake", Tornado = "tornado", Flashstorm = "flashstorm", Hail = "hail", Flood = "flood";

        public static readonly string[] Kinds = { Quake, Tornado, Flashstorm, Hail, Flood };

        public static bool IsKind(string kind) => Array.IndexOf(Kinds, kind) >= 0;

        /// <summary>Hours after it ends before the party sets out: the dust settled, the ground walked over, the water down.</summary>
        public static int DelayHours(string kind)
        {
            switch (kind)
            {
                case Flashstorm: return 2;
                case Flood: return 3;
                default: return 1;
            }
        }

        /// <summary>A party still waiting this long after it was due (fires still burning, raiders on the land) is let go.</summary>
        public const int GiveUpHours = 48;

        /// <summary>At most this many parties wait at once on a map (the oldest let go first).</summary>
        public const int MaxWaiting = 4;

        /// <summary>The colony's people a party takes: the one who leads it and a few others.</summary>
        public const int Hands = 4;

        /// <summary>Whether a party waiting since <paramref name="due"/> may set out now, wait more, or be let go.</summary>
        public static bool Due(int due, int now) => now >= due;

        public static bool TooLate(int due, int now, int ticksPerHour = 2500) => now - due > GiveUpHours * ticksPerHour;

        // ---------------- The parties waiting: "kind@tick;kind@tick" ----------------

        public static List<(string kind, int due)> Unpack(string s)
        {
            var list = new List<(string, int)>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(';'))
            {
                int at = part.IndexOf('@');
                if (at <= 0) continue;
                string kind = part.Substring(0, at);
                if (!IsKind(kind) || !int.TryParse(part.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int due)) continue;
                list.Add((kind, due));
            }
            return list;
        }

        public static string Pack(IEnumerable<(string kind, int due)> list) =>
            string.Join(";", list.Select(x => x.kind + "@" + x.due.ToString(CultureInfo.InvariantCulture)));

        /// <summary>A party added to wait: one of a kind at a time (a second tornado before the first is cleared up joins it), the newest <see cref="MaxWaiting"/>.</summary>
        public static string Add(string packed, string kind, int due)
        {
            var list = Unpack(packed).Where(x => x.kind != kind).ToList();
            list.Add((kind, due));
            while (list.Count > MaxWaiting) list.RemoveAt(0);
            return Pack(list);
        }

        public static string Remove(string packed, string kind) => Pack(Unpack(packed).Where(x => x.kind != kind));

        // ---------------- The year's count for the annals: "year:kind=n,kind=n|year:..." ----------------

        /// <summary>How many of each came in a year, from the kept line.</summary>
        public static Dictionary<string, int> CountsFor(string packed, int year)
        {
            var d = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(packed)) return d;
            foreach (var y in packed.Split('|'))
            {
                int colon = y.IndexOf(':');
                if (colon <= 0 || !int.TryParse(y.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int yy) || yy != year) continue;
                foreach (var kv in y.Substring(colon + 1).Split(','))
                {
                    int eq = kv.IndexOf('=');
                    if (eq <= 0) continue;
                    string kind = kv.Substring(0, eq);
                    if (IsKind(kind) && int.TryParse(kv.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0) d[kind] = n;
                }
            }
            return d;
        }

        /// <summary>One more of a kind in a year; only this year and the one before are kept (the annals read the year most of it fell in).</summary>
        public static string Count(string packed, int year, string kind)
        {
            if (!IsKind(kind)) return packed ?? "";
            var years = new SortedDictionary<int, Dictionary<string, int>>();
            foreach (int y in new[] { year - 1, year })
            {
                var c = CountsFor(packed, y);
                if (c.Count > 0) years[y] = c;
            }
            if (!years.TryGetValue(year, out var now)) years[year] = now = new Dictionary<string, int>();
            now[kind] = (now.TryGetValue(kind, out int n) ? n : 0) + 1;
            return string.Join("|", years.Select(y => y.Key.ToString(CultureInfo.InvariantCulture) + ":" +
                                                      string.Join(",", Kinds.Where(y.Value.ContainsKey).Select(k => k + "=" + y.Value[k].ToString(CultureInfo.InvariantCulture)))));
        }

        /// <summary>The year's disasters in the annals' order, each with how many.</summary>
        public static List<(string kind, int count)> Annals(string packed, int year)
        {
            var c = CountsFor(packed, year);
            return Kinds.Where(c.ContainsKey).Select(k => (k, c[k])).ToList();
        }
    }
}
