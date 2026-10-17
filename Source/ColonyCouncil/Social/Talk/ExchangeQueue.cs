using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The lines of conversations under way (Social/Talk/): each exchange is two people and a few lines, said one after
    /// another with a short gap, the speakers taking turns. Not saved: after a load the talk is simply over (what it
    /// changed between them was settled when it began). An exchange can be dropped at any time (the two parted), and
    /// someone already talking isn't given a second one. No game types, so Tools/Tests can check it.
    /// </summary>
    public sealed class ExchangeQueue<T> where T : class
    {
        public sealed class Line
        {
            public T speaker, listener;
            public string text;
            public int due;
        }

        public sealed class Exchange
        {
            public int id;
            public T a, b;
            public readonly List<Line> lines = new List<Line>();
            public int Next => lines.Count > 0 ? lines[0].due : int.MaxValue;
        }

        private readonly List<Exchange> exchanges = new List<Exchange>();
        private int nextId = 1;

        /// <summary>At most this many exchanges at once; a new one past it pushes out the oldest.</summary>
        public int Max { get; }

        public ExchangeQueue(int max = 12)
        {
            Max = Math.Max(1, max);
        }

        public int Count => exchanges.Count;

        public IReadOnlyList<Exchange> Exchanges => exchanges;

        /// <summary>Someone is in an exchange still under way.</summary>
        public bool Busy(T p)
        {
            if (p == null) return false;
            foreach (var e in exchanges)
                if (e.a == p || e.b == p)
                    return true;
            return false;
        }

        /// <summary>
        /// A new exchange between <paramref name="a"/> and <paramref name="b"/>: the lines in order, the first said by
        /// <paramref name="a"/> after <paramref name="firstGap"/> ticks and each after that <paramref name="gap"/> ticks
        /// later, the speakers taking turns. Empty lines are skipped. Returns its id, or 0 if there was nothing to say
        /// or one of them is already talking.
        /// </summary>
        public int Add(T a, T b, IList<string> texts, int now, int firstGap, int gap)
        {
            if (a == null || b == null || a == b || texts == null || texts.Count == 0) return 0;
            if (Busy(a) || Busy(b)) return 0;
            var e = new Exchange { id = nextId++, a = a, b = b };
            int due = now + Math.Max(0, firstGap);
            for (int i = 0; i < texts.Count; i++)
            {
                if (string.IsNullOrEmpty(texts[i])) continue;
                bool first = i % 2 == 0;
                e.lines.Add(new Line { speaker = first ? a : b, listener = first ? b : a, text = texts[i], due = due });
                due += Math.Max(1, gap);
            }
            if (e.lines.Count == 0) return 0;
            exchanges.Add(e);
            while (exchanges.Count > Max) exchanges.RemoveAt(0);
            return e.id;
        }

        /// <summary>The lines due by <paramref name="now"/>, in order, taken off the queue (an exchange with nothing left is done).</summary>
        public List<(int id, Line line)> Due(int now)
        {
            var due = new List<(int, Line)>();
            for (int i = 0; i < exchanges.Count; i++)
            {
                var e = exchanges[i];
                while (e.lines.Count > 0 && e.lines[0].due <= now)
                {
                    due.Add((e.id, e.lines[0]));
                    e.lines.RemoveAt(0);
                }
            }
            exchanges.RemoveAll(e => e.lines.Count == 0);
            return due;
        }

        /// <summary>The rest of an exchange is dropped (the two parted, one was called away).</summary>
        public void Drop(int id) => exchanges.RemoveAll(e => e.id == id);

        /// <summary>Everything someone is in is dropped.</summary>
        public void DropWith(T p)
        {
            if (p != null) exchanges.RemoveAll(e => e.a == p || e.b == p);
        }

        public Exchange Find(int id)
        {
            foreach (var e in exchanges)
                if (e.id == id)
                    return e;
            return null;
        }

        /// <summary>The earliest line still to come, or int.MaxValue.</summary>
        public int NextDue
        {
            get
            {
                int next = int.MaxValue;
                foreach (var e in exchanges)
                    if (e.Next < next)
                        next = e.Next;
                return next;
            }
        }

        public void Clear() => exchanges.Clear();
    }
}
