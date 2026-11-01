using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// A small rolling record of what the colony used and made over the last few days (Building/ColonyUse.cs fills it
    /// from the game's own events), as plain rules: an amount for each key and day, the days before the window let go,
    /// and rates a day over the time the record covers. Keys are short strings, their parts split by '|' (what was
    /// counted, then where and what). Pure, so Tools/Tests can check it.
    /// </summary>
    public class UseLog
    {
        public const int TicksPerDay = 60000;
        /// <summary>The window: today and this many days before it.</summary>
        public const int WindowDays = 3;
        /// <summary>The record is gone by only once it covers this many days.</summary>
        public const float MinDays = 1f;

        /// <summary>Each entry's day, key and amount, oldest first.</summary>
        public List<int> days = new List<int>();
        public List<string> keys = new List<string>();
        public List<float> amounts = new List<float>();
        /// <summary>The tick the record began (-1: nothing recorded yet).</summary>
        public int since = -1;

        private int prunedDay = int.MinValue;

        public static string Key(string kind, object a, object b) => kind + "|" + a + "|" + b;

        /// <summary>A key's parts: what was counted, then where, then what.</summary>
        public static string[] Parts(string key) => key?.Split('|') ?? new string[0];

        /// <summary>The first day the window keeps.</summary>
        public static int FirstDay(int now) => now / TicksPerDay - WindowDays;

        /// <summary>Counts so much more under the key, today.</summary>
        public void Add(int now, string key, float amount)
        {
            if (string.IsNullOrEmpty(key) || !(amount > 0f) || float.IsInfinity(amount)) return;
            if (since < 0 || since > now) since = now;
            int day = now / TicksPerDay;
            if (day != prunedDay) Prune(now);
            // Today's entries are the last ones.
            for (int i = keys.Count - 1; i >= 0 && days[i] >= day; i--)
            {
                if (days[i] != day || keys[i] != key) continue;
                amounts[i] += amount;
                return;
            }
            days.Add(day);
            keys.Add(key);
            amounts.Add(amount);
        }

        /// <summary>Lets go of the days before the window.</summary>
        public void Prune(int now)
        {
            prunedDay = now / TicksPerDay;
            int first = FirstDay(now), cut = 0;
            while (cut < days.Count && days[cut] < first) cut++;
            if (cut == 0) return;
            days.RemoveRange(0, cut);
            keys.RemoveRange(0, cut);
            amounts.RemoveRange(0, cut);
        }

        /// <summary>The days the record covers within the window: from its start, or the window's, to now.</summary>
        public float Covered(int now)
        {
            if (since < 0 || now <= since) return 0f;
            return (now - Math.Max(since, FirstDay(now) * TicksPerDay)) / (float)TicksPerDay;
        }

        /// <summary>The record covers enough time to go by.</summary>
        public bool Enough(int now) => Covered(now) >= MinDays;

        /// <summary>Every key with its total over the window.</summary>
        public Dictionary<string, float> Totals(int now)
        {
            var totals = new Dictionary<string, float>();
            int first = FirstDay(now);
            for (int i = 0; i < keys.Count; i++)
            {
                if (days[i] < first || days[i] > now / TicksPerDay) continue;
                totals.TryGetValue(keys[i], out float had);
                totals[keys[i]] = had + amounts[i];
            }
            return totals;
        }

        /// <summary>The key's amount a day over the time covered (0 with no time covered).</summary>
        public float ADay(string key, int now)
        {
            float covered = Covered(now);
            if (covered <= 0f) return 0f;
            return Totals(now).TryGetValue(key, out float t) ? t / covered : 0f;
        }

        /// <summary>Whether the saved lists still go together; if not they are emptied.</summary>
        public void Repair()
        {
            days ??= new List<int>();
            keys ??= new List<string>();
            amounts ??= new List<float>();
            if (days.Count == keys.Count && keys.Count == amounts.Count) return;
            days.Clear();
            keys.Clear();
            amounts.Clear();
            since = -1;
        }
    }
}
