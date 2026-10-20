using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One part of a colonist's character page: a heading and its lines (each line may have a tooltip).</summary>
    public class CharacterSection
    {
        public string title;
        public List<string> lines = new List<string>();
        /// <summary>A tooltip per line (same order; null or shorter for lines without one).</summary>
        public List<string> tips = new List<string>();
        public int order;
        /// <summary>The key it was registered under ("faith", "groups"...), so a page can pick the parts it shows.</summary>
        public string key;

        public bool Empty => lines == null || lines.Count == 0;

        public CharacterSection Line(string text, string tip = null)
        {
            if (text.NullOrEmpty()) return this;
            lines.Add(text);
            while (tips.Count < lines.Count - 1) tips.Add(null);
            tips.Add(tip);
            return this;
        }
    }

    /// <summary>
    /// The colonist's character page is made of sections that the systems add themselves (their nature and what
    /// shaped them, the views they hold, their groups, what people say about them), so each system keeps its own part
    /// and the page draws them all. Register once at startup with <see cref="Add"/>; a section that throws is left out.
    /// </summary>
    public static class CharacterPage
    {
        private static readonly List<(string key, int order, Func<Pawn, CharacterSection> make)> makers = new List<(string, int, Func<Pawn, CharacterSection>)>();

        /// <summary>Adds (or replaces, by key) a section maker; lower <paramref name="order"/> comes first.</summary>
        public static void Add(string key, int order, Func<Pawn, CharacterSection> make)
        {
            if (key == null || make == null) return;
            makers.RemoveAll(m => m.key == key);
            makers.Add((key, order, make));
            makers.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.key, b.key));
        }

        public static IEnumerable<string> Keys => makers.Select(m => m.key);

        /// <summary>The sections for a colonist, in order, without the empty ones.</summary>
        public static List<CharacterSection> For(Pawn p)
        {
            var list = new List<CharacterSection>();
            if (p == null) return list;
            foreach (var (key, order, make) in makers)
            {
                try
                {
                    var s = make(p);
                    if (s == null || s.Empty) continue;
                    s.order = order;
                    s.key = key;
                    list.Add(s);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("character page: " + key, e);
                }
            }
            return list;
        }

        /// <summary>One section by its key, or null when it's empty, missing or throws (the Social tab's column, the Culture tab).</summary>
        public static CharacterSection Make(string key, Pawn p)
        {
            if (p == null || key == null) return null;
            foreach (var (k, order, make) in makers)
            {
                if (k != key) continue;
                try
                {
                    var s = make(p);
                    if (s == null || s.Empty) return null;
                    s.order = order;
                    s.key = key;
                    return s;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("character page: " + key, e);
                    return null;
                }
            }
            return null;
        }
    }
}
