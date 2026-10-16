using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How long a worked-out answer holds. Rimfolk keeps some answers (a map's facts for the hour, a colonist's mood
    /// state) instead of working them out every time they're asked; these rules say when such an answer is still good.
    /// An answer is kept for one "bucket" of game time (the in-game hour, a 250-tick stretch), so everyone asking within
    /// it gets the same answer and the first to ask in the next one works it out again; and it's dropped whenever what
    /// it was worked out from has changed (a version number its source counts up), or the clock went backwards (an
    /// older save loaded). Pure (no game types): Tools/Tests checks the rules.
    /// </summary>
    public static class Fresh
    {
        /// <summary>Never worked out.</summary>
        public const int Never = int.MinValue;

        /// <summary>Which stretch of <paramref name="period"/> ticks a tick falls in (rounding down, below zero too).</summary>
        public static int Bucket(int tick, int period)
        {
            if (period <= 1) return tick;
            int b = tick / period;
            if (tick < 0 && tick % period != 0) b--;
            return b;
        }

        /// <summary>An answer worked out at <paramref name="at"/> still holds at <paramref name="now"/>: the same stretch of time.</summary>
        public static bool Holds(int at, int now, int period) => at != Never && Bucket(at, period) == Bucket(now, period);

        /// <summary>
        /// An answer worked out at <paramref name="at"/> from version <paramref name="versionAt"/> of its source still holds
        /// at <paramref name="now"/>, with the source at <paramref name="version"/>: the same stretch, the source unchanged.
        /// </summary>
        public static bool Holds(int at, int versionAt, int now, int version, int period) => Holds(at, now, period) && versionAt == version;
    }

    /// <summary>
    /// Answers kept per key (a colonist's ID) for one stretch of game time, or until their source changes (see
    /// <see cref="Fresh"/>). Holds at most <see cref="MaxKeys"/> keys, then starts again, so it never grows without end.
    /// Pure (no game types).
    /// </summary>
    public sealed class KeptAnswers<TValue>
    {
        private struct Entry
        {
            public int at, version;
            public TValue value;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly int period;

        public const int MaxKeys = 2000;

        /// <param name="period">How many ticks an answer holds (within one stretch of this many ticks).</param>
        public KeptAnswers(int period)
        {
            this.period = period;
        }

        public int Count => entries.Count;

        public int Period => period;

        /// <summary>The kept answer for this key, if it still holds now with the source at this version.</summary>
        public bool TryGet(int key, int now, int version, out TValue value)
        {
            if (entries.TryGetValue(key, out var e) && Fresh.Holds(e.at, e.version, now, version, period))
            {
                value = e.value;
                return true;
            }
            value = default;
            return false;
        }

        public void Set(int key, int now, int version, TValue value)
        {
            if (entries.Count >= MaxKeys && !entries.ContainsKey(key)) entries.Clear();
            entries[key] = new Entry { at = now, version = version, value = value };
        }

        public void Clear() => entries.Clear();
    }
}
