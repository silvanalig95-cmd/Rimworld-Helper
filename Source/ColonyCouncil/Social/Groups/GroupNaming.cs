using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a friendship group is named for (a GroupShareDef): what most of them share, a habit, or their friendship.</summary>
    public enum ShareIdea
    {
        /// <summary>The hours they keep: the night.</summary>
        Hours,
        /// <summary>The work most of them do.</summary>
        Work,
        /// <summary>A side of their character most of them have.</summary>
        Likeness,
        /// <summary>A habit of theirs, and the place they keep it: the fire, the table, the rec room.</summary>
        Habit,
        /// <summary>Only their friendship.</summary>
        Friends
    }

    /// <summary>One thing a group might be named for, and how many of its people it fits.</summary>
    public struct ShareFit
    {
        /// <summary>The share's name (a GroupShareDef's defName).</summary>
        public string key;
        public ShareIdea idea;
        /// <summary>The share of its people it fits (0-1): those who keep the night hours, do that work, have that side.</summary>
        public float fit;
        /// <summary>A habit's place in the group's own list (its first habit first); for the rest, the share's own order.</summary>
        public int order;
    }

    /// <summary>
    /// How a friendship group gets its name, with no game types, so the tests can check it: by what most of its people
    /// share (the night hours, their work, a side of their character), else by a habit of theirs and its place (the fire,
    /// the table), else by their friendship alone; never after one of them. The name is picked once and kept. Older saves
    /// named friendship groups after their anchor ("Stace's friends"), and any group whose kind's own names were all
    /// taken ("Hector's hunters"): <see cref="AfterSomeone"/> finds those, so they get a name of this kind once.
    /// </summary>
    public static class GroupNaming
    {
        /// <summary>
        /// How many of the group a share must fit, so that "most of them" is true: more than half keep the night hours or
        /// do the work; two in three have the side (2 of 3, 4 of 6).
        /// </summary>
        public const float HoursAbove = 0.5f, WorkAbove = 0.5f, LikenessAt = 0.66f;

        /// <summary>Whether a share holds for the group at all (a habit of theirs, or their friendship, always does).</summary>
        public static bool Holds(ShareFit s)
        {
            switch (s.idea)
            {
                case ShareIdea.Hours: return s.fit > HoursAbove;
                case ShareIdea.Work: return s.fit > WorkAbove;
                case ShareIdea.Likeness: return s.fit >= LikenessAt;
                default: return true;
            }
        }

        /// <summary>The share of <paramref name="of"/> people that <paramref name="count"/> of them are (0 with nobody).</summary>
        public static float Fit(int count, int of) => of <= 0 ? 0f : count / (float)of;

        private static int Rank(ShareIdea idea) => idea == ShareIdea.Friends ? 2 : idea == ShareIdea.Habit ? 1 : 0;

        /// <summary>
        /// The shares that hold, the one to name the group for first: what most of them share (the more of them it fits,
        /// the sooner; the hours before work before character on a tie), then their habits in the group's own order, then
        /// their friendship alone.
        /// </summary>
        public static List<ShareFit> Ranked(IEnumerable<ShareFit> fits) =>
            (fits ?? Enumerable.Empty<ShareFit>()).Where(Holds)
            .OrderBy(s => Rank(s.idea))
            .ThenByDescending(s => Rank(s.idea) == 0 ? s.fit : 0f)
            .ThenBy(s => (int)s.idea)
            .ThenBy(s => s.order)
            .ThenBy(s => s.key ?? "", StringComparer.Ordinal)
            .ToList();

        /// <summary>
        /// One of <paramref name="options"/> that no group has (<paramref name="taken"/>, without regard to case), the same
        /// one for the same <paramref name="seed"/>; null when every one is taken.
        /// </summary>
        public static string Pick(IList<string> options, ICollection<string> taken, int seed)
        {
            if (options == null) return null;
            var free = new List<string>();
            foreach (var o in options)
            {
                if (string.IsNullOrEmpty(o) || free.Contains(o)) continue;
                bool used = false;
                if (taken != null)
                    foreach (var t in taken)
                        if (string.Equals(t, o, StringComparison.OrdinalIgnoreCase))
                        {
                            used = true;
                            break;
                        }
                if (!used) free.Add(o);
            }
            if (free.Count == 0) return null;
            int i = seed % free.Count;
            return free[i < 0 ? i + free.Count : i];
        }

        /// <summary>The token a name pattern puts someone's name in ("{ANCHOR}'s lot").</summary>
        public const string Someone = "{ANCHOR}";

        /// <summary>
        /// A keyed name after someone as a pattern: their placeholder (<paramref name="who"/>: "ANCHOR" in
        /// "{ANCHOR_nameDef}'s {KIND}", "PAWN" in "{PAWN_nameDef}'s friends"), in any form, becomes <see cref="Someone"/>,
        /// and {KIND} the kind's label.
        /// </summary>
        public static string AsPattern(string text, string who, string kind = null)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(who)) return text;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                int close = text.IndexOf('}', open);
                if (close < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                sb.Append(text, i, open - i);
                string token = text.Substring(open + 1, close - open - 1);
                if (token == who || token.StartsWith(who + "_", StringComparison.Ordinal)) sb.Append(Someone);
                else if (token == "KIND" && kind != null) sb.Append(kind);
                else sb.Append(text, open, close - open + 1);
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Whether <paramref name="name"/> was given after one person: one of <paramref name="patterns"/> ("{ANCHOR}'s
        /// lot", "{ANCHOR}'s hunters") with a name where <see cref="Someone"/> is, and perhaps a number after it ("Hector's
        /// lot (2)"). A pattern without the token never matches.
        /// </summary>
        public static bool AfterSomeone(string name, IEnumerable<string> patterns, IEnumerable<string> notAfter = null)
        {
            if (string.IsNullOrEmpty(name) || patterns == null) return false;
            string n = WithoutNumber(name.Trim());
            // A name a kind or a share gives (the medieval hunters' "the huntsmen's table") is nobody's, whatever it looks like.
            if (notAfter != null)
                foreach (var own in notAfter)
                    if (own != null && string.Equals(own.Trim(), n, StringComparison.OrdinalIgnoreCase))
                        return false;
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                int at = pattern.IndexOf(Someone, StringComparison.Ordinal);
                if (at < 0) continue;
                string before = pattern.Substring(0, at);
                string after = pattern.Substring(at + Someone.Length);
                if (n.Length <= before.Length + after.Length) continue;
                if (!n.StartsWith(before, StringComparison.OrdinalIgnoreCase) || !n.EndsWith(after, StringComparison.OrdinalIgnoreCase)) continue;
                string who = n.Substring(before.Length, n.Length - before.Length - after.Length);
                if (who.Trim().Length > 0) return true;
            }
            return false;
        }

        /// <summary>A name with the number a taken name was given taken off: "the hunters (2)" is "the hunters".</summary>
        public static string WithoutNumber(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.EndsWith(")")) return name;
            int open = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (open <= 0) return name;
            string digits = name.Substring(open + 2, name.Length - open - 3);
            return digits.Length > 0 && digits.All(char.IsDigit) ? name.Substring(0, open) : name;
        }
    }
}
