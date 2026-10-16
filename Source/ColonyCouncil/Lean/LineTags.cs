using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// A tagged line ("[winter,night|evening] The cold's in my teeth."), taken apart once and kept. Every line Rimfolk
    /// picks goes through <see cref="Flavor"/>, which used to split each line of a pool, its tags and their
    /// alternatives again on every pick (strings, arrays and LINQ for every line of the pool). The lines come from the
    /// defs and never change, so each is taken apart the first time it is seen and the parts are kept here; picking then
    /// only compares. The rules are exactly those of <see cref="Flavor.Strip(string, out string[])"/>,
    /// <see cref="Flavor.Match"/> and <see cref="Flavor.EraTagsFit"/> (Tools/Tests checks them against the old code on
    /// every line of the mod). Pure (no game types).
    /// </summary>
    public static class LineTags
    {
        /// <summary>One alternative of a tag: its text as written (trimmed) and, for matching, without a leading '!'.</summary>
        public readonly struct Alt
        {
            /// <summary>As written, trimmed ("!child").</summary>
            public readonly string raw;
            /// <summary>Without the first '!' ("child"), what the moment's tags are looked up by.</summary>
            public readonly string name;
            public readonly bool neg;

            public Alt(string raw)
            {
                this.raw = raw;
                neg = raw.StartsWith("!", StringComparison.Ordinal);
                name = neg ? raw.Substring(1) : raw;
            }
        }

        public sealed class Parsed
        {
            /// <summary>The line without its tags (what Strip returns).</summary>
            public string text;
            /// <summary>The tags, trimmed, empty ones left out; null when the line has none (as Strip gives them).</summary>
            public string[] tags;
            /// <summary>Per tag, its | alternatives.</summary>
            public Alt[][] alts;
            /// <summary>Per tag: it only says something that holds for most of a game (the era, wealth, size, the land).</summary>
            public bool[] standing;
            /// <summary>The tags made only of era names (tribal, medieval, industrial, spacer, lowtech), with their alternatives.</summary>
            public Alt[][] era;
            /// <summary>Whether the text talks about guns or power (worked out by Flavor the first time it's asked): -1 not yet, 0 no, 1 yes.</summary>
            public sbyte highTech = -1;
        }

        /// <summary>The tags that hold for most of a game (the same set as <see cref="Flavor.StandingTags"/>).</summary>
        public static readonly HashSet<string> Standing = new HashSet<string>
        {
            "tribal", "medieval", "industrial", "spacer", "lowtech", "poor", "rich", "small", "large"
        };

        public static readonly HashSet<string> EraNames = new HashSet<string> { "tribal", "medieval", "industrial", "spacer", "lowtech" };

        /// <summary>How many lines are kept before the store starts again (the mod's own lines are about twenty thousand).</summary>
        public const int MaxKept = 60000;

        private static readonly Dictionary<string, Parsed> kept = new Dictionary<string, Parsed>(StringComparer.Ordinal);
        private static readonly Parsed nothing = new Parsed();

        /// <summary>How many lines are kept now (for the tests and the self-check).</summary>
        public static int Count => kept.Count;

        public static void Clear() => kept.Clear();

        /// <summary>The line taken apart (the same object each time it's asked). Null for a null line.</summary>
        public static Parsed Of(string line)
        {
            if (line == null) return null;
            if (kept.TryGetValue(line, out var p)) return p;
            if (kept.Count >= MaxKept) kept.Clear();
            p = Parse(line);
            kept[line] = p;
            return p;
        }

        /// <summary>Takes a line apart (not kept): the rules of the old Strip, Match and EraTagsFit, worked out once.</summary>
        public static Parsed Parse(string line)
        {
            var p = new Parsed { text = line };
            if (line == null) return p;
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("[")) return p;
            int end = trimmed.IndexOf(']');
            if (end < 0) return p;
            var parts = trimmed.Substring(1, end - 1).Split(',');
            var tags = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                string t = part.Trim();
                if (t.Length > 0) tags.Add(t);
            }
            p.tags = tags.ToArray();
            p.text = trimmed.Substring(end + 1).TrimStart();
            p.alts = new Alt[p.tags.Length][];
            p.standing = new bool[p.tags.Length];
            List<Alt[]> era = null;
            for (int i = 0; i < p.tags.Length; i++)
            {
                var raw = p.tags[i].Split('|');
                var alts = new Alt[raw.Length];
                bool allEra = true, standing = true;
                for (int k = 0; k < raw.Length; k++)
                {
                    string a = raw[k].Trim();
                    alts[k] = new Alt(a);
                    if (!EraNames.Contains(a.TrimStart('!'))) allEra = false;
                    if (!Standing.Contains(a.TrimStart('!')) && !a.TrimStart('!').StartsWith("land:")) standing = false;
                }
                p.alts[i] = alts;
                p.standing[i] = standing;
                if (allEra) (era ??= new List<Alt[]>()).Add(alts);
            }
            p.era = era?.ToArray();
            return p;
        }

        /// <summary>
        /// -1 when the line doesn't fit the moment, else how many of its tags speak to it (standing tags don't count):
        /// every tag must fit by one of its alternatives (an alternative "!x" fits when x isn't in the moment).
        /// </summary>
        public static int Match(Parsed p, HashSet<string> context)
        {
            if (p?.tags == null || p.tags.Length == 0) return 0;
            int matched = 0;
            for (int i = 0; i < p.alts.Length; i++)
            {
                bool ok = false;
                var alts = p.alts[i];
                for (int k = 0; k < alts.Length; k++)
                {
                    bool has = context != null && context.Contains(alts[k].name);
                    if (has != alts[k].neg)
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) return -1;
                if (!p.standing[i]) matched++;
            }
            return matched;
        }

        /// <summary>False when one of the line's era tags rules out the colony's era (<paramref name="era"/>: its era tags).</summary>
        public static bool EraFits(Parsed p, HashSet<string> era)
        {
            if (p?.era == null) return true;
            foreach (var alts in p.era)
            {
                bool any = false;
                foreach (var a in alts)
                {
                    if (a.neg ? !era.Contains(a.name) : era.Contains(a.raw))
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false;
            }
            return true;
        }
    }
}
