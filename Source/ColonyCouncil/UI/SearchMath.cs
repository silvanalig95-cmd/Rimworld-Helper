using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>What a search finds, one group each, in this order when two groups match as well.</summary>
    public enum SearchKind : byte
    {
        Colonist,
        Person,
        Place,
        People,
        House,
        /// <summary>A view or a mode of one of Rimfolk's windows ("Bonds", "Map: Trade").</summary>
        View,
        /// <summary>An Almanac article.</summary>
        Article,
        /// <summary>Whatever another part registers (Search.Register).</summary>
        Other
    }

    /// <summary>One thing the search can find: its name, other words it is found by, what it opens, and a quieter line.</summary>
    public sealed class SearchItem
    {
        public SearchKind kind;
        public string name;
        /// <summary>Other words it is found by, less strongly than its name (a title, its town, the window).</summary>
        public string also;
        /// <summary>What it opens: a thing's text form ("p12", "t55"), a page's address ("Bonds", "Map/Trade"), an article's defName.</summary>
        public string key;
        public string sub;
        /// <summary>What a click or Enter does, for what another part registers; Rimfolk's own kinds open by their key.</summary>
        public Action open;
        /// <summary>Folded once when the index is made (<see cref="SearchMath.Prepare"/>).</summary>
        public string foldedName, foldedAlso;

        public SearchItem(SearchKind kind, string name, string key, string sub = null, string also = null)
        {
            this.kind = kind;
            this.name = name;
            this.key = key;
            this.sub = sub;
            this.also = also;
        }

        public override string ToString() => kind + ":" + name;
    }

    /// <summary>The found items of one kind, best first.</summary>
    public sealed class SearchGroup
    {
        public SearchKind kind;
        public readonly List<SearchItem> items = new List<SearchItem>();
        public int best;
        /// <summary>How many of this kind matched in all (more than show).</summary>
        public int total;
    }

    /// <summary>
    /// The search in every window's corner (docs/design/interface.md, "Back, forward, search, following"): names folded
    /// for matching (any case, without accents or apostrophes, link markers left out), a match scored (the whole name,
    /// its start, a word's start, every word of the query starting a word, inside a longer word; other words weaker),
    /// grouped by kind with the best group first, a few per group, and Enter takes the first. Pure, so Tools/Tests checks
    /// the matching and grouping; the index is made by the game's side when the field is clicked (UI/Search.cs).
    /// </summary>
    public static class SearchMath
    {
        /// <summary>At most this many of one kind show.</summary>
        public const int PerGroup = 6;

        public const int Exact = 100, Start = 80, WordStart = 60, AllWords = 55, Inside = 40, AlsoWord = 30, AlsoInside = 20;

        /// <summary>A text as the search compares it: lower case, no accents, no apostrophes, other marks as spaces, link markers gone, spaces single.</summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // A link's words, without its markers and the thing it names.
            if (LinkTextMath.HasMarks(s)) s = LinkTextMath.Plain(s);
            string d;
            try
            {
                d = s.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException)
            {
                d = s;
            }
            var sb = new StringBuilder(d.Length);
            bool space = true;
            foreach (char c0 in d)
            {
                // Link markers and other private use characters, and the accents a letter was split from.
                if (c0 >= '' && c0 <= '') continue;
                var cat = CharUnicodeInfo.GetUnicodeCategory(c0);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (c0 == '\'' || c0 == '’') continue;
                if (char.IsLetterOrDigit(c0))
                {
                    sb.Append(char.ToLowerInvariant(c0));
                    space = false;
                }
                else if (!space)
                {
                    sb.Append(' ');
                    space = true;
                }
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            return sb.ToString();
        }

        /// <summary>Folds every item's name and other words once, when the index is made.</summary>
        public static void Prepare(IList<SearchItem> items)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                if (it == null) continue;
                it.foldedName = Fold(it.name);
                it.foldedAlso = Fold(it.also);
            }
        }

        private static bool WordStartAt(string s, int i) => i == 0 || s[i - 1] == ' ';

        /// <summary>A word of <paramref name="s"/> starts with <paramref name="q"/>.</summary>
        private static bool StartsAWord(string s, string q)
        {
            if (q.Length == 0 || s.Length < q.Length) return false;
            int i = s.IndexOf(q, StringComparison.Ordinal);
            while (i >= 0)
            {
                if (WordStartAt(s, i)) return true;
                i = s.IndexOf(q, i + 1, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>How well a folded query matches a folded name (and its other words); 0 for no match.</summary>
        public static int Score(string query, string name, string also = null)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(name) && string.IsNullOrEmpty(also)) return 0;
            name ??= "";
            if (name == query) return Exact;
            if (name.StartsWith(query, StringComparison.Ordinal)) return Start;
            if (StartsAWord(name, query)) return WordStart;
            var words = query.Split(' ');
            if (words.Length > 1)
            {
                bool all = true;
                foreach (var w in words)
                    if (w.Length > 0 && !StartsAWord(name, w))
                    {
                        all = false;
                        break;
                    }
                if (all) return AllWords;
            }
            if (query.Length >= 3 && name.IndexOf(query, StringComparison.Ordinal) >= 0) return Inside;
            if (!string.IsNullOrEmpty(also))
            {
                if (StartsAWord(also, query)) return AlsoWord;
                if (query.Length >= 3 && also.IndexOf(query, StringComparison.Ordinal) >= 0) return AlsoInside;
            }
            return 0;
        }

        /// <summary>
        /// What the query finds, grouped by kind: each group best first (then the shorter name, then by name), at most
        /// <paramref name="perGroup"/> of it; the groups best first, kinds in their order on a tie. Nothing for an empty query.
        /// </summary>
        public static List<SearchGroup> Find(string query, IList<SearchItem> items, int perGroup = PerGroup)
        {
            var groups = new List<SearchGroup>();
            string q = Fold(query);
            if (q.Length == 0 || items == null) return groups;
            var byKind = new Dictionary<SearchKind, List<(SearchItem item, int score)>>();
            foreach (var it in items)
            {
                if (it == null) continue;
                int s = Score(q, it.foldedName ?? Fold(it.name), it.foldedAlso ?? Fold(it.also));
                if (s <= 0) continue;
                if (!byKind.TryGetValue(it.kind, out var list)) byKind[it.kind] = list = new List<(SearchItem, int)>();
                list.Add((it, s));
            }
            foreach (var kv in byKind)
            {
                var list = kv.Value;
                list.Sort((a, b) =>
                {
                    if (a.score != b.score) return b.score.CompareTo(a.score);
                    int la = a.item.name?.Length ?? 0, lb = b.item.name?.Length ?? 0;
                    if (la != lb) return la.CompareTo(lb);
                    return string.CompareOrdinal(a.item.name, b.item.name);
                });
                var g = new SearchGroup { kind = kv.Key, best = list[0].score, total = list.Count };
                for (int i = 0; i < list.Count && i < Math.Max(1, perGroup); i++) g.items.Add(list[i].item);
                groups.Add(g);
            }
            groups.Sort((a, b) => a.best != b.best ? b.best.CompareTo(a.best) : a.kind.CompareTo(b.kind));
            return groups;
        }

        /// <summary>What Enter takes: the first item of the first group, or null.</summary>
        public static SearchItem First(List<SearchGroup> groups) => groups != null && groups.Count > 0 && groups[0].items.Count > 0 ? groups[0].items[0] : null;
    }
}
