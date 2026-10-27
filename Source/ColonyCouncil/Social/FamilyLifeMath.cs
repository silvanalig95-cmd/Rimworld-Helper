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
    /// one the family marks, what a child was named after, and how much family weighs in who inherits. No game types,
    /// so Tools/Tests checks it.
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
        /// <summary>The eighteenth birthday, which the family marks as well.</summary>
        public const int AdultAge = 18;
        /// <summary>The birthday a child comes of age (stops being a child: Lives/Legacy.cs marks it).</summary>
        public const int ComingOfAgeAge = 13;

        /// <summary>The least <see cref="HeirBonus"/> that counts as family for a dead colonist's room (family by marriage).</summary>
        public const float HeirFamily = 40f;

        /// <summary>
        /// How much a relation weighs in who inherits what the dead leave (Social/FamilyInheritance.cs, Lives/Legacy.cs),
        /// on top of how the two felt about each other: a parent, child, brother or sister most; then a partner, or a
        /// parent or child by adoption; other family by blood; family by marriage; anything else between them (an old
        /// flame) a little; no relation, nothing.
        /// </summary>
        public static float HeirBonus(string relation, bool byBlood)
        {
            if (string.IsNullOrEmpty(relation)) return 0f;
            switch (relation)
            {
                case "Parent":
                case "Child":
                case "Sibling":
                    return 80f;
                case "Spouse":
                case "Fiance":
                case "Lover":
                    return 70f;
            }
            if (FamilyMath.Adoptive(relation)) return 70f;
            if (byBlood) return 60f;
            if (FamilyMath.ByMarriage(relation)) return HeirFamily;
            return 20f;
        }

        /// <summary>
        /// How someone takes it: family doubt a match with someone they can't stand, and are otherwise glad; a friend is
        /// glad when they think well of the one they're close to and don't dislike the other.
        /// </summary>
        public static MatchTake Take(bool family, int opinionOfTheirs, int opinionOfPartner)
        {
            if (family) return opinionOfPartner <= DoubtOpinion ? MatchTake.Doubt : MatchTake.Glad;
            return opinionOfTheirs >= FriendOpinion && opinionOfPartner >= 0 ? MatchTake.Glad : MatchTake.None;
        }

        /// <summary>
        /// How a widowed parent's child takes their new partner (Social/FamilyLife.cs): glad for the parent unless they
        /// dislike the one who seems to take their late parent's place at all.
        /// </summary>
        public static MatchTake NewPartnerTake(int opinionOfPartner) => opinionOfPartner < 0 ? MatchTake.Doubt : MatchTake.Glad;

        /// <summary>A child on the way is wanted unless the mother is unhappy about it or the couple were set against one.</summary>
        public static bool Wanted(bool motherUnhappy, bool avoiding) => !motherUnhappy && !avoiding;

        /// <summary>A birthday the family marks with a toast: one of the game's growth moments, or the eighteenth.</summary>
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
