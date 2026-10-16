using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Moments that wait until two people are really together (Social/Together.cs): each under a key, so the same
    /// moment never waits twice, with the tick it gives up at and, for the ones worth a walk, the tick from which one
    /// of the two may go and find the other. A small list (a few dozen at most), looked through once an hour. No game
    /// types, so Tools/Tests can check it.
    /// </summary>
    public sealed class PendingQueue<T>
    {
        public sealed class Entry
        {
            public int id;
            public string key;
            public int created;
            /// <summary>When it gives up: from this tick on it no longer happens.</summary>
            public int expires;
            /// <summary>From when one may go and find the other (a walk and a word); -1: they only meet by chance.</summary>
            public int seekFrom = -1;
            public T item;

            public bool Seeks => seekFrom >= 0;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private int nextId = 1;

        /// <summary>At most this many wait at once; a new one past it pushes out the one closest to giving up.</summary>
        public int Max { get; }

        public PendingQueue(int max = 40)
        {
            Max = Math.Max(1, max);
        }

        public int Count => entries.Count;

        /// <summary>The ones waiting, oldest first. Copy it before removing while looping.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        public bool Contains(string key)
        {
            if (key == null) return false;
            foreach (var e in entries)
                if (e.key == key)
                    return true;
            return false;
        }

        public Entry Find(int id)
        {
            foreach (var e in entries)
                if (e.id == id)
                    return e;
            return null;
        }

        /// <summary>
        /// Waits <paramref name="item"/> under <paramref name="key"/> until <paramref name="expires"/>. A key already
        /// waiting keeps its place (null back: the first one stands). Something already out of time isn't added. With
        /// the list full, the one closest to giving up makes way.
        /// </summary>
        public Entry Add(string key, T item, int now, int expires, int seekFrom = -1)
        {
            if (key == null || expires <= now || Contains(key)) return null;
            if (entries.Count >= Max)
            {
                int soonest = 0;
                for (int i = 1; i < entries.Count; i++)
                    if (entries[i].expires < entries[soonest].expires)
                        soonest = i;
                entries.RemoveAt(soonest);
            }
            var e = new Entry { id = nextId++, key = key, created = now, expires = expires, seekFrom = seekFrom, item = item };
            entries.Add(e);
            return e;
        }

        public bool Remove(Entry e) => e != null && entries.Remove(e);

        public bool RemoveKey(string key)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].key == key)
                {
                    entries.RemoveAt(i);
                    return true;
                }
            return false;
        }

        /// <summary>Takes out every moment whose time ran out (it simply doesn't happen), oldest first.</summary>
        public List<Entry> Expire(int now)
        {
            var gone = new List<Entry>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].expires <= now)
                    gone.Add(entries[i]);
            foreach (var e in gone) entries.Remove(e);
            return gone;
        }

        public void Clear() => entries.Clear();
    }

    /// <summary>The numbers behind moments that wait for people to be together.</summary>
    public static class TogetherMath
    {
        public const int TicksPerHour = 2500;

        /// <summary>The shortest and longest a moment waits for the two to be together.</summary>
        public const float MinWaitHours = 1f;
        public const float MaxWaitHours = 48f;

        /// <summary>A walk over isn't started this close to giving up: there'd be no time for the word itself.</summary>
        public const int SeekMarginTicks = TicksPerHour / 2;

        /// <summary>The longest a walk over to someone may take before it's given up (they kept moving, or it's far).</summary>
        public const int WalkTicks = TicksPerHour * 3 / 2;

        /// <summary>How long the word itself lasts once they're together, in ticks (a few seconds at normal speed).</summary>
        public const int TalkTicks = 300;

        /// <summary>When a moment waiting <paramref name="hours"/> gives up (between one hour and two days).</summary>
        public static int Expires(int now, float hours)
        {
            float h = Math.Min(MaxWaitHours, Math.Max(MinWaitHours, hours));
            return now + (int)Math.Round(h * TicksPerHour);
        }

        /// <summary>From when one may go and find the other: after <paramref name="afterHours"/> of waiting; -1 (never) when negative.</summary>
        public static int SeekFrom(int now, float afterHours) => afterHours < 0f ? -1 : now + (int)Math.Round(afterHours * TicksPerHour);

        /// <summary>A walk over may start now: the wait for a chance meeting is over, and there's still time for it.</summary>
        public static bool MaySeek(int now, int seekFrom, int expires) => seekFrom >= 0 && now >= seekFrom && now < expires - SeekMarginTicks;
    }
}
