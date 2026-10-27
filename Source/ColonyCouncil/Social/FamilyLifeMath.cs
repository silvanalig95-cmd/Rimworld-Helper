using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>How someone close takes a new couple or an engagement (Social/FamilyLife.cs).</summary>
    public enum MatchTake
    {
        None,
        /// <summary>Glad for them.</summary>
        Glad,
        /// <summary>Family who can't stand the one chosen.</summary>
        Doubt
    }

    /// <summary>A relative whose first name a baby might be given (Social/FamilyLife.cs).</summary>
    public struct NameSource
    {
        public string name;
        /// <summary>How close: 0 a parent, 1 a grandparent, 2 an aunt or uncle, 3 a great-grandparent, 4 further.</summary>
        public int rank;
        public bool dead;
        /// <summary>Of the baby's own gender (or the baby has none).</summary>
        public bool fits;
    }

    /// <summary>
    /// The numbers and choices behind the family's life together (Social/FamilyLife.cs): who is glad of a new couple and
    /// who doubts it, whether a child on the way is wanted, which family names a baby is offered, whether a birthday is
    /// one the family marks, and what a child was named after. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class FamilyLifeMath
    {
        /// <summary>Opinion of one of the couple that makes a friend glad for them.</summary>
        public const int FriendOpinion = 30;
        /// <summary>Opinion of the one chosen at or under which family doubt the match.</summary>
        public const int DoubtOpinion = -20;
        /// <summary>At most this many of those close to a couple react to it.</summary>
        public const int MaxReacting = 4;
        /// <summary>At most this many family names are offered for a baby.</summary>
        public const int MaxSuggested = 6;
        /// <summary>The birthday a child comes of age.</summary>
        public const int AdultAge = 18;

        /// <summary>
        /// How someone takes it: family doubt a match with someone they can't stand, and are otherwise glad; a friend is
        /// glad when they think well of the one they're close to and don't dislike the other.
        /// </summary>
        public static MatchTake Take(bool family, int opinionOfTheirs, int opinionOfPartner)
        {
            if (family) return opinionOfPartner <= DoubtOpinion ? MatchTake.Doubt : MatchTake.Glad;
            return opinionOfTheirs >= FriendOpinion && opinionOfPartner >= 0 ? MatchTake.Glad : MatchTake.None;
        }

        /// <summary>A child on the way is wanted unless the mother is unhappy about it or the couple were set against one.</summary>
        public static bool Wanted(bool motherUnhappy, bool avoiding) => !motherUnhappy && !avoiding;

        /// <summary>A birthday the family marks with a toast: one of the game's growth moments, or coming of age.</summary>
        public static bool Marked(int age, bool growthMoment) => growthMoment || age == AdultAge;

        /// <summary>
        /// The family's first names to offer for a baby: of the baby's own gender, the dead remembered first, then the
        /// closest; each once, none that a living brother or sister already has, at most <see cref="MaxSuggested"/>.
        /// </summary>
        public static List<string> Suggestions(IEnumerable<NameSource> family, IEnumerable<string> taken)
        {
            var no = new HashSet<string>((taken ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var s in (family ?? Enumerable.Empty<NameSource>()).Where(s => s.fits && !string.IsNullOrEmpty(s.name))
                         .OrderByDescending(s => s.dead).ThenBy(s => s.rank))
            {
                if (no.Contains(s.name) || !seen.Add(s.name)) continue;
                list.Add(s.name);
                if (list.Count >= MaxSuggested) break;
            }
            return list;
        }

        /// <summary>
        /// Who a child seems named after: the closest older relative (born before them) with the same first name, or -1.
        /// <paramref name="older"/> lists each relative's first name and how close they are (as <see cref="NameSource.rank"/>).
        /// </summary>
        public static int NamedAfter(string first, IList<(string name, int rank, bool older)> relatives)
        {
            if (string.IsNullOrEmpty(first) || relatives == null) return -1;
            int best = -1;
            for (int i = 0; i < relatives.Count; i++)
            {
                var r = relatives[i];
                if (!r.older || !string.Equals(r.name, first, StringComparison.OrdinalIgnoreCase)) continue;
                if (best < 0 || r.rank < relatives[best].rank) best = i;
            }
            return best;
        }
    }
}
