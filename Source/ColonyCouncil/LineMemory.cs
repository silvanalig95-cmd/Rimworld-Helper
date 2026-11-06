using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The lines each pool gave out lately, so a line doesn't come round again soon while another one fits the moment.
    /// A pool keeps out its last few picks: a third of its lines, at least one, at most <see cref="MaxWindow"/>. Kept in
    /// memory only, so after a load every pool's lines are fresh again.
    /// <para>
    /// And each speaker's own: what they said lately (their last <see cref="SpeakerWindow"/> picks of any pool, in memory),
    /// and what they wrote in their journal, read back from the journal itself (<see cref="Wrote"/>: a pool's line with its
    /// names left open, against the words as written), so a person doesn't write a line they already wrote while their
    /// journal still holds it, however many loads lie between. The journal is saved anyway: nothing new is. In the
    /// player's save (6 November 2026) 24 of 630 journal lines were a writer's own earlier line, half within ten days.
    /// </para>
    /// Pure (no game types), so the tests run it.
    /// </summary>
    public sealed class LineMemory
    {
        public const int MaxWindow = 12;

        /// <summary>How many of a speaker's latest picks are kept out for them, whatever the pool.</summary>
        public const int SpeakerWindow = 20;

        /// <summary>A pool's line must have at least this many letters of its own words to be told apart in a journal.</summary>
        public const int MinWords = 12;

        private readonly Dictionary<object, Queue<string>> picks = new Dictionary<object, Queue<string>>();
        private readonly Dictionary<object, HashSet<string>> sets = new Dictionary<object, HashSet<string>>();
        private readonly Dictionary<int, Queue<string>> said = new Dictionary<int, Queue<string>>();
        private readonly Dictionary<int, HashSet<string>> saidSets = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<(object pool, int speaker), (int stamp, HashSet<string> lines)> written =
            new Dictionary<(object, int), (int, HashSet<string>)>();

        /// <summary>The journal look-ups kept at most; past it they are worked out afresh.</summary>
        public const int MaxWritten = 4000;

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

        // ---------------- Each speaker's own ----------------

        /// <summary>The lines this speaker was given lately, from any pool (as picked, tags removed), or null.</summary>
        public HashSet<string> SaidBy(int speaker) => saidSets.TryGetValue(speaker, out var s) && s.Count > 0 ? s : null;

        /// <summary>Notes a line picked for this speaker; the oldest of their last <see cref="SpeakerWindow"/> drops out.</summary>
        public void RememberSaid(int speaker, string line)
        {
            if (line == null) return;
            if (!said.TryGetValue(speaker, out var q))
            {
                q = new Queue<string>();
                said[speaker] = q;
                saidSets[speaker] = new HashSet<string>();
            }
            q.Enqueue(line);
            while (q.Count > SpeakerWindow) q.Dequeue();
            var set = saidSets[speaker];
            set.Clear();
            foreach (var l in q) set.Add(l);
        }

        /// <summary>
        /// The lines of this pool (as picked, tags removed) the speaker has already written in their journal
        /// (<paramref name="journalNow"/>, the entries as written). Worked out again only when <paramref name="stamp"/> (the
        /// journal's length and last line's time, say) changes.
        /// </summary>
        public HashSet<string> WrittenBy(object pool, int speaker, int stamp, Func<IEnumerable<string>> lines, Func<IList<string>> journalNow)
        {
            if (pool == null || lines == null || journalNow == null) return null;
            if (written.TryGetValue((pool, speaker), out var had) && had.stamp == stamp) return had.lines;
            var journal = journalNow();
            HashSet<string> found = null;
            if (journal != null && journal.Count > 0)
            {
                // Each entry's words made plain once, each line's shape once (Shape.Of keeps it), then line against entry.
                var plain = new List<string>(journal.Count);
                foreach (var w in journal)
                    if (!string.IsNullOrEmpty(w)) plain.Add(Plain(w).Trim());
                foreach (var line in lines())
                {
                    if (line == null || (found != null && found.Contains(line))) continue;
                    var shape = Shape.Of(line);
                    if (!shape.usable) continue;
                    for (int i = plain.Count - 1; i >= 0; i--)
                    {
                        if (!shape.Fits(plain[i])) continue;
                        (found ??= new HashSet<string>()).Add(line);
                        break;
                    }
                }
            }
            if (written.Count >= MaxWritten) written.Clear();
            written[(pool, speaker)] = (stamp, found);
            return found;
        }

        /// <summary>
        /// Whether <paramref name="written"/> is <paramref name="line"/> as it came out: the line's own words, in order, with
        /// anything at all where it names someone or something ({PAWN_nameDef}, {0}, [grammar]); its first words begin the
        /// text unless it begins with a name, and its last words end it unless it ends with one. Rich text in the written
        /// words (a name's colour) is passed over, and case is ignored. A line with fewer than <see cref="MinWords"/>
        /// letters of its own is never matched (nearly anything would fit it).
        /// </summary>
        public static bool Wrote(string line, string written)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(written)) return false;
            var shape = Shape.Of(line);
            return shape.usable && shape.Fits(Plain(written).Trim());
        }

        /// <summary>A line taken apart once for <see cref="Wrote"/>: its own words and whether a name opens or closes it.</summary>
        private sealed class Shape
        {
            public List<string> parts;
            public bool openStart, openEnd, usable;

            private static readonly Dictionary<string, Shape> known = new Dictionary<string, Shape>(StringComparer.Ordinal);

            public static Shape Of(string line)
            {
                if (known.TryGetValue(line, out var s)) return s;
                s = new Shape();
                s.parts = Literals(line.Trim(), out s.openStart, out s.openEnd);
                int letters = 0;
                foreach (var p in s.parts)
                    foreach (char c in p)
                        if (char.IsLetter(c)) letters++;
                s.usable = letters >= MinWords;
                if (known.Count >= 20000) known.Clear();
                known[line] = s;
                return s;
            }

            public bool Fits(string text)
            {
                if (parts.Count == 1 && !openStart && !openEnd) return string.Equals(text, parts[0], StringComparison.OrdinalIgnoreCase);
                int at = 0;
                for (int i = 0; i < parts.Count; i++)
                {
                    string part = parts[i];
                    if (i == 0 && !openStart)
                    {
                        if (!text.StartsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
                        at = part.Length;
                        continue;
                    }
                    if (i == parts.Count - 1 && !openEnd)
                    {
                        int end = text.Length - part.Length;
                        return end >= at && text.EndsWith(part, StringComparison.OrdinalIgnoreCase);
                    }
                    int found = text.IndexOf(part, at, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) return false;
                    at = found + part.Length;
                }
                return true;
            }
        }

        /// <summary>
        /// A line's own words between its placeholders ({...} and [...]), in order and never empty; whether it begins
        /// (<paramref name="openStart"/>) or ends (<paramref name="openEnd"/>) with a placeholder.
        /// </summary>
        public static List<string> Literals(string line, out bool openStart, out bool openEnd)
        {
            var parts = new List<string>();
            openStart = openEnd = false;
            if (string.IsNullOrEmpty(line)) return parts;
            var cur = new System.Text.StringBuilder();
            int i = 0;
            bool lastWasHole = false;
            while (i < line.Length)
            {
                char c = line[i];
                char close = c == '{' ? '}' : c == '[' ? ']' : '\0';
                int shut = close == '\0' ? -1 : line.IndexOf(close, i + 1);
                if (shut > i)
                {
                    if (cur.Length > 0) parts.Add(cur.ToString());
                    else if (parts.Count == 0) openStart = true;
                    cur.Clear();
                    i = shut + 1;
                    lastWasHole = true;
                    continue;
                }
                cur.Append(c);
                lastWasHole = false;
                i++;
            }
            if (cur.Length > 0) parts.Add(cur.ToString());
            openEnd = lastWasHole;
            if (parts.Count == 0) openStart = true;
            return parts;
        }

        /// <summary>Written words without rich text (a name's &lt;color&gt;, &lt;b&gt;).</summary>
        public static string Plain(string written)
        {
            if (written == null || written.IndexOf('<') < 0) return written ?? "";
            var sb = new System.Text.StringBuilder(written.Length);
            int i = 0;
            while (i < written.Length)
            {
                if (written[i] == '<')
                {
                    int shut = written.IndexOf('>', i + 1);
                    string tag = shut > i ? written.Substring(i + 1, shut - i - 1) : "";
                    if (shut > i && (tag.StartsWith("color", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("/color", StringComparison.OrdinalIgnoreCase)
                                     || tag == "b" || tag == "/b" || tag == "i" || tag == "/i"))
                    {
                        i = shut + 1;
                        continue;
                    }
                }
                sb.Append(written[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Whether these words are already in the journal (<paramref name="written"/>, its entries): a person doesn't write
        /// the very same line twice while their journal still holds the first.
        /// </summary>
        public static bool Repeats(IEnumerable<string> written, string line)
        {
            if (written == null || string.IsNullOrEmpty(line)) return false;
            foreach (var w in written)
                if (string.Equals(w, line, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// A speaker's tone draws from its own lines alone only with at least this many; fewer are drawn together with the
        /// plain lines, so a one-line tone doesn't give every cheerful colonist the same words for every cold.
        /// </summary>
        public const int MinOwnTone = 3;

        public static bool ToneDrawsAlone(int toneLines) => toneLines >= MinOwnTone;

        /// <summary>Forgets every speaker's part (another game's people); the pools' own memory stays.</summary>
        public void ForgetSpeakers()
        {
            said.Clear();
            saidSets.Clear();
            written.Clear();
        }

        public void Clear()
        {
            picks.Clear();
            sets.Clear();
            said.Clear();
            saidSets.Clear();
            written.Clear();
        }
    }
}
