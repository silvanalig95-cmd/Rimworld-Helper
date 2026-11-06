using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The seats' names by the colony's age (decision 10 of the review, 6 November 2026: a spacer colony doesn't elect a
    /// reeve): the age's name of a seat, and a shipped line that names a seat by its old name (the medieval one, the
    /// office's own label) said with the age's name instead, its article put right ("a reeve" to "an agronomist"). Pure
    /// (no game types): Tools/Tests checks it; the game's side is Offices/OfficeWords.cs.
    /// </summary>
    public static class OfficeNamesMath
    {
        /// <summary>The ages in the order an office's names are given: tribal, medieval, industrial, spacer.</summary>
        public static int Index(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return 0;
                case WorldAge.Medieval: return 1;
                case WorldAge.Industrial: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// A seat's name in an age: the age's own when it has one, else the nearest age's going down then up (an industrial
        /// name for a spacer seat with none), else <paramref name="label"/> (the office's own).
        /// </summary>
        public static string NameIn(IList<string> names, WorldAge age, string label)
        {
            int i = Index(age);
            if (names != null)
            {
                for (int d = 0; d < 4; d++)
                {
                    foreach (int k in new[] { i - d, i + d })
                        if (k >= 0 && k < names.Count && !string.IsNullOrEmpty(names[k]))
                            return names[k];
                }
            }
            return label ?? "";
        }

        /// <summary>A plain English plural: "elders", "aldermen", "chiefs", "secretaries".</summary>
        public static string Plural(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            // The last word takes the plural ("captain of the guard" is one name: its first word).
            int of = name.IndexOf(" of ", StringComparison.Ordinal);
            if (of > 0) return Plural(name.Substring(0, of)) + name.Substring(of);
            if (name.EndsWith("man", StringComparison.Ordinal)) return name.Substring(0, name.Length - 3) + "men";
            if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1 && "aeiou".IndexOf(name[name.Length - 2]) < 0)
                return name.Substring(0, name.Length - 1) + "ies";
            if (name.EndsWith("s", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal) || name.EndsWith("ch", StringComparison.Ordinal))
                return name + "es";
            return name + "s";
        }

        /// <summary>"an" before a vowel sound, "a" before the rest (by the first letter: the seats' names have no silent h).</summary>
        public static string Article(string word) => !string.IsNullOrEmpty(word) && "aeiouAEIOU".IndexOf(word[0]) >= 0 ? "an" : "a";

        /// <summary>
        /// A line with each seat's old name (whole words, any case, the plural too) said with its new one: the first letter's
        /// case kept, and an "a" or "an" just before it put right. <paramref name="names"/>: (old, new) pairs; pairs whose
        /// two names are the same change nothing. Longer old names go first, so "keeper of days" is never read as "keeper".
        /// </summary>
        public static string Rename(string text, IList<(string from, string to)> names)
        {
            if (string.IsNullOrEmpty(text) || names == null || names.Count == 0) return text;
            var pairs = new List<(string from, string to)>();
            foreach (var (from, to) in names)
            {
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) continue;
                pairs.Add((Plural(from), Plural(to)));
                pairs.Add((from, to));
            }
            if (pairs.Count == 0) return text;
            pairs.Sort((a, b) => b.from.Length.CompareTo(a.from.Length));
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                bool matched = false;
                if (WordStart(text, i))
                {
                    foreach (var (from, to) in pairs)
                    {
                        if (i + from.Length > text.Length || string.Compare(text, i, from, 0, from.Length, StringComparison.OrdinalIgnoreCase) != 0) continue;
                        if (!WordEnd(text, i + from.Length)) continue;
                        string word = char.IsUpper(text[i]) ? char.ToUpperInvariant(to[0]) + to.Substring(1) : to;
                        FixArticle(sb, word);
                        sb.Append(word);
                        i += from.Length;
                        matched = true;
                        break;
                    }
                }
                if (matched) continue;
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        private static bool Letter(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

        private static bool WordStart(string s, int i) => i == 0 || !Letter(s[i - 1]) && s[i - 1] != '{';

        private static bool WordEnd(string s, int i) => i >= s.Length || !Letter(s[i]) && s[i] != '}';

        /// <summary>An "a" or "an" just written before the new word, made to fit it.</summary>
        private static void FixArticle(StringBuilder sb, string word)
        {
            int n = sb.Length;
            if (n < 2 || sb[n - 1] != ' ') return;
            int end = n - 1, start = end;
            while (start > 0 && char.IsLetter(sb[start - 1])) start--;
            if (start > 0 && Letter(sb[start - 1])) return;
            string art = sb.ToString(start, end - start);
            if (art != "a" && art != "an" && art != "A" && art != "An") return;
            string want = Article(word);
            if (char.IsUpper(art[0])) want = char.ToUpperInvariant(want[0]) + want.Substring(1);
            sb.Remove(start, end - start);
            sb.Insert(start, want);
        }
    }
}
