using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>The kinds the events record counts (Story/EventRecord.cs), in the order its section shows them.</summary>
    public enum EventKind
    {
        /// <summary>The storyteller's raids, sieges, infestations and the like (its big and small threats).</summary>
        Threat,
        /// <summary>Everything else the storyteller sends: traders, visitors, weather, finds, illness, animals.</summary>
        Everyday,
        /// <summary>A quest that plays out on the colony's own map (read from the game's quest list, not kept).</summary>
        QuestHome,
        /// <summary>A quest that sends the colony out on the world map (read from the game's quest list, not kept).</summary>
        QuestAway,
        /// <summary>One of Rimfolk's own events, drawn by the storyteller or on Rimfolk's own timer.</summary>
        Rimfolk,
        /// <summary>A question put to the council.</summary>
        Question,
        /// <summary>A god's card, as the colony saw it.</summary>
        GodCard,
        /// <summary>Something Rimfolk held back, and why (one line a day for each reason).</summary>
        Held
    }

    /// <summary>
    /// The events record's rules (the player, 20 November 2026: "I always wonder, does it even work correctly? And I feel
    /// like I have fewer events than I did in the past"): each line kept as "tick|kind|what", for 30 days and at most
    /// <see cref="MaxLines"/>; what was held back once a day for each reason; each kind's count and its newest few.
    /// Also the pace of Rimfolk's own timer when it runs besides the storyteller's draw. Pure, so Tools/Tests checks it.
    /// </summary>
    public static class EventRecordMath
    {
        public const int TicksPerDay = 60000;

        /// <summary>The record looks back this many days.</summary>
        public const int WindowDays = 30;

        /// <summary>At most this many lines are kept, the oldest let go first (about three a day is a busy month).</summary>
        public const int MaxLines = 200;

        /// <summary>The newest this many of each kind are shown by name and day.</summary>
        public const int LastShown = 3;

        /// <summary>
        /// Why Rimfolk held something back: its own timer waited for a question to be answered, or for a fight to end; the
        /// storyteller drew an everyday event while a question of Rimfolk's waited, so none of Rimfolk's could be drawn then.
        /// </summary>
        public const string HeldWait = "wait", HeldFight = "fight", HeldDrawWait = "drawwait";

        public static readonly string[] HeldReasons = { HeldWait, HeldFight, HeldDrawWait };

        /// <summary>One line of the record: when, what kind, and its name as the colony saw it (no "|" or line breaks in it).</summary>
        public static string Line(int tick, EventKind kind, string what) =>
            tick.ToString(CultureInfo.InvariantCulture) + "|" + ((int)kind).ToString(CultureInfo.InvariantCulture) + "|" + Clean(what);

        private static string Clean(string what)
        {
            if (string.IsNullOrEmpty(what)) return "";
            var s = what.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ').Trim();
            return s.Length > 80 ? s.Substring(0, 80) : s;
        }

        /// <summary>A line read back; false for one that can't be read (it is let go).</summary>
        public static bool TryParse(string line, out int tick, out EventKind kind, out string what)
        {
            tick = 0;
            kind = EventKind.Everyday;
            what = null;
            if (string.IsNullOrEmpty(line)) return false;
            int a = line.IndexOf('|');
            int b = a < 0 ? -1 : line.IndexOf('|', a + 1);
            if (a <= 0 || b < 0) return false;
            if (!int.TryParse(line.Substring(0, a), NumberStyles.Integer, CultureInfo.InvariantCulture, out tick)) return false;
            if (!int.TryParse(line.Substring(a + 1, b - a - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int k)) return false;
            if (k < 0 || k > (int)EventKind.Held) return false;
            kind = (EventKind)k;
            what = line.Substring(b + 1);
            return true;
        }

        /// <summary>Within the record's 30 days of <paramref name="now"/> (and not ahead of it).</summary>
        public static bool InWindow(int tick, int now) => tick <= now && now - tick <= WindowDays * TicksPerDay;

        /// <summary>Lets go of lines older than 30 days or unreadable, then the oldest beyond <see cref="MaxLines"/>.</summary>
        public static void Prune(List<string> lines, int now)
        {
            if (lines == null) return;
            lines.RemoveAll(l => !TryParse(l, out int t, out _, out _) || !InWindow(t, now));
            if (lines.Count > MaxLines) lines.RemoveRange(0, lines.Count - MaxLines);
        }

        /// <summary>The game's day of a tick, counted from 1 (as the rest of Rimfolk says "Day 12").</summary>
        public static int Day(int tick) => tick / TicksPerDay + 1;

        /// <summary>A line about this held reason is already kept for the day of <paramref name="now"/>.</summary>
        public static bool HeldToday(List<string> lines, string reason, int now)
        {
            if (lines == null) return false;
            int day = Day(now);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (!TryParse(lines[i], out int t, out var k, out var w)) continue;
                if (Day(t) != day) return false;
                if (k == EventKind.Held && w == reason) return true;
            }
            return false;
        }

        /// <summary>One kind's month: how many, and the newest few by name and day, newest first.</summary>
        public sealed class KindSum
        {
            public int count;
            public readonly List<(string what, int day)> last = new List<(string, int)>();
        }

        /// <summary>
        /// Each kind's count and newest <paramref name="shown"/> within the window, from lines in the order they came (oldest
        /// first) and any read from elsewhere (the quests). Held lines are counted by reason (<see cref="HeldCounts"/>), not here.
        /// </summary>
        public static Dictionary<EventKind, KindSum> Sum(IEnumerable<(int tick, EventKind kind, string what)> entries, int now, int shown = LastShown)
        {
            var sums = new Dictionary<EventKind, KindSum>();
            var all = new List<(int tick, EventKind kind, string what)>();
            if (entries != null)
                foreach (var e in entries)
                    if (e.kind != EventKind.Held && InWindow(e.tick, now))
                        all.Add(e);
            // Newest first; the same tick keeps the order it came in, the later first.
            var order = new List<int>();
            for (int i = 0; i < all.Count; i++) order.Add(i);
            order.Sort((x, y) => all[y].tick != all[x].tick ? all[y].tick.CompareTo(all[x].tick) : y.CompareTo(x));
            foreach (int i in order)
            {
                var e = all[i];
                if (!sums.TryGetValue(e.kind, out var s)) sums[e.kind] = s = new KindSum();
                s.count++;
                if (s.last.Count < shown) s.last.Add((e.what, Day(e.tick)));
            }
            return sums;
        }

        /// <summary>How many days in the window each held reason was kept for.</summary>
        public static Dictionary<string, int> HeldCounts(IEnumerable<(int tick, EventKind kind, string what)> entries, int now)
        {
            var counts = new Dictionary<string, int>();
            if (entries == null) return counts;
            foreach (var e in entries)
            {
                if (e.kind != EventKind.Held || e.what == null || !InWindow(e.tick, now)) continue;
                counts.TryGetValue(e.what, out int n);
                counts[e.what] = n + 1;
            }
            return counts;
        }

        // ---------------- Rimfolk's own timer besides the storyteller ----------------

        /// <summary>The middle way's pace at 100%: one event about every this many days, the own timer's usual pace.</summary>
        public const float BesidesBaseDays = 4f;

        /// <summary>The setting "... and on Rimfolk's own timer besides": 0 (none, as before) to 1.</summary>
        public const float MaxBesides = 1f;

        /// <summary>Rimfolk's own timer runs besides the storyteller's draw at this share.</summary>
        public static bool BesidesOn(float share) => !float.IsNaN(share) && share > 0.001f;

        /// <summary>Days between the own timer's events besides the storyteller's draw: 4 at 100%, 8 at 50%, 16 at 25%.</summary>
        public static float BesidesDays(float share)
        {
            if (!BesidesOn(share)) return float.PositiveInfinity;
            float s = share > MaxBesides ? MaxBesides : share;
            return BesidesBaseDays / s;
        }
    }
}
