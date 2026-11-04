using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// Names that lead somewhere, in text Rimfolk draws in its own windows: the marker a link is written with, reading it
    /// back into runs (plain text, or a link with the thing it names), the text without markers, marking the names a line
    /// mentions, and laying the runs out for a width. Only Rimfolk's LinkText (UI/Cards/LinkText.cs) reads the markers;
    /// everything that goes to the game (letters, messages, bubbles, the play log) is filled plain, so no marker reaches the
    /// game's windows, and <see cref="Plain"/> takes any stray one out at the doors to the game (Tell, Speech). The marker is
    /// three characters from Unicode's private use area, which no font draws and no text of the game or Rimfolk uses:
    /// open, the thing's ref in its text form ("t55"), a separator, the words shown, close. Pure: Tools/Tests checks it.
    /// </summary>
    public static class LinkTextMath
    {
        public const char Open = '';
        public const char Mid = '';
        public const char Close = '';

        /// <summary>One piece of a text: plain words, or a link (its thing's ref in text form) with the words it shows.</summary>
        public struct Run
        {
            public string text;
            /// <summary>The thing it names in its text form ("p12"), or null for plain words.</summary>
            public string thing;

            public bool IsLink => thing != null;

            public Run(string text, string thing)
            {
                this.text = text;
                this.thing = thing;
            }
        }

        /// <summary>The text has at least one marker in it.</summary>
        public static bool HasMarks(string s) => s != null && s.IndexOf(Open) >= 0;

        /// <summary>A marker character anywhere in the text (an open, a separator or a close).</summary>
        public static bool AnyMarker(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (c == Open || c == Mid || c == Close)
                    return true;
            return false;
        }

        /// <summary>
        /// The words shown as a link to the thing (its text form, "t55"); the words as they are when there is no thing or no
        /// words. Markers already in the words are taken out, so links never nest.
        /// </summary>
        public static string Wrap(string label, string thing)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(thing) || AnyMarker(thing)) return label;
            return Open + thing + Mid + Plain(label) + Close;
        }

        /// <summary>The text with every marker taken out, its links' words kept: what the game gets. The same string when there is none.</summary>
        public static string Plain(string s)
        {
            if (!AnyMarker(s)) return s;
            var sb = new StringBuilder(s.Length);
            bool inRef = false;
            foreach (char c in s)
            {
                if (c == Open)
                {
                    inRef = true;
                    continue;
                }
                if (c == Mid)
                {
                    inRef = false;
                    continue;
                }
                if (c == Close)
                {
                    inRef = false;
                    continue;
                }
                if (!inRef) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The text read into runs, in order; neighbouring plain words are one run. A marker that isn't whole (no separator,
        /// no close) reads as plain words without its marker characters, so nothing broken is ever drawn as a link.
        /// </summary>
        public static List<Run> Parse(string s)
        {
            var runs = new List<Run>();
            if (string.IsNullOrEmpty(s)) return runs;
            var plain = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == Open)
                {
                    int mid = s.IndexOf(Mid, i + 1);
                    int close = mid < 0 ? -1 : s.IndexOf(Close, mid + 1);
                    int nextOpen = s.IndexOf(Open, i + 1);
                    if (mid > i + 1 && close > mid && (nextOpen < 0 || nextOpen > close))
                    {
                        string thing = s.Substring(i + 1, mid - i - 1);
                        string label = s.Substring(mid + 1, close - mid - 1);
                        if (plain.Length > 0)
                        {
                            runs.Add(new Run(plain.ToString(), null));
                            plain.Clear();
                        }
                        if (label.Length > 0) runs.Add(new Run(label, thing));
                        i = close + 1;
                        continue;
                    }
                    // A broken marker: its characters dropped, the words kept.
                    i++;
                    continue;
                }
                if (c != Mid && c != Close) plain.Append(c);
                i++;
            }
            if (plain.Length > 0) runs.Add(new Run(plain.ToString(), null));
            return runs;
        }

        private static bool WordChar(char c) => char.IsLetterOrDigit(c) || c == '\'';

        /// <summary>
        /// The text with the first mention of each name (a whole word or words, matched as written) marked as a link to its
        /// thing; the longest names first, never inside a link already there, never two links over the same words. Names
        /// shorter than two letters are left alone. The same string when nothing is marked.
        /// </summary>
        public static string MarkNames(string text, IList<KeyValuePair<string, string>> names)
        {
            if (string.IsNullOrEmpty(text) || names == null || names.Count == 0) return text;
            var order = new List<KeyValuePair<string, string>>();
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n.Key) && n.Key.Length >= 2 && !string.IsNullOrEmpty(n.Value) && !AnyMarker(n.Key) && !AnyMarker(n.Value))
                    order.Add(n);
            if (order.Count == 0) return text;
            order.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            // Where links already are, and where new ones go: start, length, thing.
            var taken = new List<(int start, int end)>();
            if (HasMarks(text))
            {
                int at = 0;
                while (true)
                {
                    int o = text.IndexOf(Open, at);
                    if (o < 0) break;
                    int c = text.IndexOf(Close, o + 1);
                    if (c < 0) break;
                    taken.Add((o, c + 1));
                    at = c + 1;
                }
            }
            var marks = new List<(int start, int len, string thing)>();
            var done = new HashSet<string>();
            foreach (var n in order)
            {
                if (!done.Add(n.Key)) continue;
                int from = 0;
                while (from < text.Length)
                {
                    int i = text.IndexOf(n.Key, from, StringComparison.Ordinal);
                    if (i < 0) break;
                    int end = i + n.Key.Length;
                    bool whole = (i == 0 || !WordChar(text[i - 1])) && (end >= text.Length || !WordChar(text[end]));
                    bool free = true;
                    foreach (var t in taken)
                        if (i < t.end && end > t.start)
                        {
                            free = false;
                            break;
                        }
                    if (whole && free)
                    {
                        marks.Add((i, n.Key.Length, n.Value));
                        taken.Add((i, end));
                        break;
                    }
                    from = i + 1;
                }
            }
            if (marks.Count == 0) return text;
            marks.Sort((a, b) => a.start.CompareTo(b.start));
            var sb = new StringBuilder(text.Length + marks.Count * 8);
            int copied = 0;
            foreach (var m in marks)
            {
                sb.Append(text, copied, m.start - copied);
                sb.Append(Open).Append(m.thing).Append(Mid).Append(text, m.start, m.len).Append(Close);
                copied = m.start + m.len;
            }
            sb.Append(text, copied, text.Length - copied);
            return sb.ToString();
        }

        // ---------------- Laying out ----------------

        /// <summary>A piece of laid-out text: words of one run on one line, where they start and how wide they are.</summary>
        public sealed class Piece
        {
            public string text;
            public float x, w;
            public int line;
            /// <summary>The link it belongs to (an index into <see cref="Layout.links"/>), or -1 for plain words.</summary>
            public int link = -1;
        }

        /// <summary>Runs laid out for one width: the pieces in reading order, the links they belong to, and how many lines.</summary>
        public sealed class Layout
        {
            public float width;
            public readonly List<Piece> pieces = new List<Piece>();
            /// <summary>Each link's thing (its text form), in the order they come.</summary>
            public readonly List<string> links = new List<string>();
            /// <summary>Each link's thing read back (none for a text form that names nothing).</summary>
            public readonly List<ThingRef> things = new List<ThingRef>();
            public int lines;
        }

        private struct Token
        {
            public string word;
            public int run;
            public bool spaceBefore;
            public bool newLine;
        }

        /// <summary>
        /// The runs laid out for a width, as the game wraps text: words go on a line while they fit and a line breaks only at
        /// a space or a line break (a word glued to a link, "Tallbrook." or "Edda's", stays with it); a word longer than the
        /// width stands alone on its line. <paramref name="measure"/> gives a word's width, <paramref name="space"/> the width
        /// of a space. Neighbouring words of one run on one line are one piece, so a line is drawn in a few calls.
        /// </summary>
        public static Layout Lay(List<Run> runs, float width, Func<string, float> measure, float space)
        {
            var layout = new Layout { width = width };
            if (runs == null || runs.Count == 0 || measure == null) return layout;
            // The words, with whether a space comes before each (across runs) and where the text breaks a line.
            var tokens = new List<Token>();
            var runLink = new int[runs.Count];
            bool pendingSpace = false, pendingBreak = false;
            for (int r = 0; r < runs.Count; r++)
            {
                runLink[r] = -1;
                if (runs[r].IsLink)
                {
                    runLink[r] = layout.links.Count;
                    layout.links.Add(runs[r].thing);
                    layout.things.Add(ThingRef.Parse(runs[r].thing));
                }
                string text = runs[r].text ?? "";
                int i = 0;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (c == '\n')
                    {
                        if (pendingBreak || tokens.Count == 0) tokens.Add(new Token { word = "", run = r, newLine = true });
                        pendingBreak = true;
                        pendingSpace = false;
                        i++;
                        continue;
                    }
                    if (c == ' ' || c == '\t' || c == '\r')
                    {
                        pendingSpace = true;
                        i++;
                        continue;
                    }
                    int j = i;
                    while (j < text.Length && text[j] != ' ' && text[j] != '\n' && text[j] != '\t' && text[j] != '\r') j++;
                    tokens.Add(new Token { word = text.Substring(i, j - i), run = r, spaceBefore = pendingSpace && !pendingBreak, newLine = pendingBreak });
                    pendingSpace = false;
                    pendingBreak = false;
                    i = j;
                }
            }
            if (tokens.Count == 0) return layout;
            int line = 0;
            float x = 0f;
            bool lineEmpty = true;
            Piece piece = null;
            int t = 0;
            while (t < tokens.Count)
            {
                var first = tokens[t];
                if (first.newLine && t > 0)
                {
                    line++;
                    x = 0f;
                    lineEmpty = true;
                    piece = null;
                }
                if (first.word.Length == 0)
                {
                    t++;
                    continue;
                }
                // A group: this word and the words glued to it (no space between, no line break).
                int g = t + 1;
                float gw = measure(first.word);
                while (g < tokens.Count && !tokens[g].spaceBefore && !tokens[g].newLine && tokens[g].word.Length > 0)
                {
                    gw += measure(tokens[g].word);
                    g++;
                }
                float lead = lineEmpty || !first.spaceBefore ? 0f : space;
                if (!lineEmpty && x + lead + gw > width + 0.01f)
                {
                    line++;
                    x = 0f;
                    lineEmpty = true;
                    piece = null;
                    lead = 0f;
                }
                for (int k = t; k < g; k++)
                {
                    var tok = tokens[k];
                    float w = measure(tok.word);
                    float gap = k == t ? lead : 0f;
                    int link = runLink[tok.run];
                    if (piece != null && piece.link == link && piece.line == line)
                    {
                        if (gap > 0f) piece.text += " ";
                        piece.text += tok.word;
                        piece.w += gap + w;
                    }
                    else
                    {
                        piece = new Piece { text = tok.word, x = x + gap, w = w, line = line, link = link };
                        layout.pieces.Add(piece);
                    }
                    x += gap + w;
                    lineEmpty = false;
                }
                t = g;
            }
            layout.lines = line + 1;
            return layout;
        }
    }
}
