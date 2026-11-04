using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// One way to fill a text's placeholders, for every system that writes from what is true (DESIGN.md, "Facts"): the
    /// conversations with outsiders, the letters to the neighbours, events', camps' and groups' lines. A placeholder is a
    /// name in braces, {TOWN}, read as the def validator reads it (the innermost pair of braces). Values that are names
    /// keep their case; a few come with "the" ({SITUATION}: "the gray cough", <see cref="WithThe"/>), and a line said
    /// aloud that starts with one is capitalized (<see cref="FillLine"/>). Pure: Tools/Tests checks it against the fills
    /// it replaced (the talks' own, and the chains of Replace).
    /// </summary>
    public static class Texts
    {
        /// <summary>
        /// The text with each {KEY} that has a value filled with it (a null value as nothing); any other placeholder is
        /// left as written, for the game's own grammar or a context's Format after ({PAWN_nameDef}).
        /// </summary>
        public static string Fill(string text, IReadOnlyDictionary<string, string> values) => Scan(text, values, null, null, false);

        /// <summary>
        /// The same, for text drawn in Rimfolk's own windows: with <paramref name="links"/>, each value whose placeholder
        /// names a thing (<paramref name="things"/>: the key to the thing's text form, FactSet.things) is filled as a link to
        /// it (LinkTextMath.Wrap), which only Rimfolk's LinkText reads. Without links it is exactly <see cref="Fill(string,
        /// IReadOnlyDictionary{string, string})"/>: everything that goes to the game (letters, messages, bubbles, the play
        /// log) is filled that way, so no marker reaches the game's windows.
        /// </summary>
        public static string Fill(string text, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> things, bool links) =>
            !links || things == null || things.Count == 0 ? Scan(text, values, null, null, false) : Scan(text, values, null, null, false, things);

        /// <summary>The same with the values given in place: Texts.Fill(line, ("ITEM", item), ("DAYS", days)).</summary>
        public static string Fill(string text, params (string key, string value)[] values) => Scan(text, null, null, values, false);

        /// <summary>
        /// A line to be said, by the conversations' rule: every placeholder filled (<paramref name="first"/>'s value
        /// before <paramref name="values"/>', one with none left empty, which the talks never let happen: a line is only
        /// said when each has a value), and a line starting with a value in lower case ("the gray cough is...")
        /// capitalized.
        /// </summary>
        public static string FillLine(string text, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> first = null) =>
            Capitalize(Scan(text, first, values, null, true));

        /// <summary>The placeholders in a text, once each, in the order they come ("{TOWN} and {WAR} and {TOWN}": TOWN, WAR).</summary>
        public static string[] Holes(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return Array.Empty<string>();
            var list = new List<string>();
            int i = 0;
            while (Next(text, i, out int a, out int b))
            {
                string h = text.Substring(a + 1, b - a - 1);
                if (h.Length > 0 && !list.Contains(h)) list.Add(h);
                i = b + 1;
            }
            return list.ToArray();
        }

        /// <summary>The first letter in upper case when it is a lower-case one.</summary>
        public static string Capitalize(string s) => string.IsNullOrEmpty(s) || !char.IsLower(s[0]) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>
        /// A name for a placeholder that comes with "the": its own <paramref name="name"/> when it has one ("the Long
        /// Thaw"), else what it is with "the" ("the plague"), unless that already starts with "the", "a" or "an". Null
        /// with neither.
        /// </summary>
        public static string WithThe(string name, string label)
        {
            if (!string.IsNullOrEmpty(name)) return name;
            if (string.IsNullOrEmpty(label)) return null;
            string l = label.ToLowerInvariant();
            return l.StartsWith("the ") || l.StartsWith("a ") || l.StartsWith("an ") ? label : "the " + label;
        }

        /// <summary>The next placeholder from <paramref name="from"/>: its braces at a and b (the innermost pair), or false.</summary>
        private static bool Next(string text, int from, out int a, out int b)
        {
            a = b = -1;
            if (from >= text.Length) return false;
            int open = text.IndexOf('{', from);
            if (open < 0) return false;
            int close = text.IndexOf('}', open + 1);
            if (close < 0) return false;
            // "{A {B}": the placeholder is {B}.
            a = text.LastIndexOf('{', close - 1, close - open);
            b = close;
            return true;
        }

        private static bool Look(string key, IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> then, (string key, string value)[] pairs,
            out string value)
        {
            if (first != null && first.TryGetValue(key, out value)) return true;
            if (then != null && then.TryGetValue(key, out value)) return true;
            if (pairs != null)
                for (int i = 0; i < pairs.Length; i++)
                    if (string.Equals(pairs[i].key, key, StringComparison.Ordinal))
                    {
                        value = pairs[i].value;
                        return true;
                    }
            value = null;
            return false;
        }

        /// <summary>
        /// Each placeholder with a value filled; with <paramref name="all"/>, one without a value left empty, else left as
        /// written. The text itself when nothing is filled.
        /// </summary>
        private static string Scan(string text, IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> then, (string key, string value)[] pairs,
            bool all, IReadOnlyDictionary<string, string> things = null)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            StringBuilder sb = null;
            int i = 0, copied = 0;
            while (Next(text, i, out int a, out int b))
            {
                string key = text.Substring(a + 1, b - a - 1);
                if (Look(key, first, then, pairs, out string value) || all)
                {
                    if (things != null && value != null && things.TryGetValue(key, out string thing)) value = LinkTextMath.Wrap(value, thing);
                    sb ??= new StringBuilder(text.Length + 32);
                    sb.Append(text, copied, a - copied).Append(value ?? "");
                    copied = b + 1;
                }
                i = b + 1;
            }
            if (sb == null) return text;
            sb.Append(text, copied, text.Length - copied);
            return sb.ToString();
        }
    }
}
