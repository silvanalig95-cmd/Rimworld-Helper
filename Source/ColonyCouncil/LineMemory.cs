using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The lines each pool gave out lately, so a line doesn't come round again soon while another one fits the moment.
    /// A pool keeps out its last few picks: a third of its lines, at least one, at most <see cref="MaxWindow"/>. Kept in
    /// memory only, so after a load every line is fresh again. Pure (no game types), so the tests run it.
    /// </summary>
    public sealed class LineMemory
    {
        public const int MaxWindow = 12;

        private readonly Dictionary<object, Queue<string>> picks = new Dictionary<object, Queue<string>>();
        private readonly Dictionary<object, HashSet<string>> sets = new Dictionary<object, HashSet<string>>();

        /// <summary>How many of its latest picks a pool of this many lines keeps out (0 for a pool of one line).</summary>
        public static int Window(int poolSize)
        {
            if (poolSize <= 1) return 0;
            int w = poolSize / 3;
            if (w < 1) w = 1;
            return w > MaxWindow ? MaxWindow : w;
        }

        /// <summary>The lines this pool gave out lately (as shown, tags removed), or null.</summary>
        public HashSet<string> Recent(object pool)
        {
            if (pool == null) return null;
            return sets.TryGetValue(pool, out var s) && s.Count > 0 ? s : null;
        }

        public bool IsRecent(object pool, string line) => line != null && Recent(pool)?.Contains(line) == true;

        /// <summary>Notes a pick from a pool of <paramref name="poolSize"/> lines; the oldest pick drops out of the window.</summary>
        public void Remember(object pool, string line, int poolSize)
        {
            if (pool == null || line == null) return;
            int window = Window(poolSize);
            if (window == 0) return;
            if (!picks.TryGetValue(pool, out var q))
            {
                q = new Queue<string>();
                picks[pool] = q;
                sets[pool] = new HashSet<string>();
            }
            var set = sets[pool];
            q.Enqueue(line);
            while (q.Count > window) q.Dequeue();
            set.Clear();
            foreach (var l in q) set.Add(l);
        }

        public void Clear()
        {
            picks.Clear();
            sets.Clear();
        }
    }
}
