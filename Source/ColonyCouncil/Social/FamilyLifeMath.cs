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

    /// <summary>How two grown brothers or sisters get on (Social/FamilyBonds.cs).</summary>
    public enum SiblingTake
    {
        None,
        /// <summary>Close: each thinks well of the other.</summary>
        Close,
        /// <summary>Rivals: one of them can't stand the other.</summary>
        Rival
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
    /// one the family marks, what a child was named after, how much family weighs in who inherits, what a family is
    /// called and what its name stands for, who sits down to a family supper, how grown brothers and sisters get on, and
    /// how often a grown child looks in on an ageing parent. No game types, so Tools/Tests checks it.
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

        // ---------------- Family names ----------------

        /// <summary>"Boyds", "Joneses": the family known by its last name (said as "the Boyds"). Null for no name.</summary>
        public static string FamilyPlural(string last)
        {
            if (string.IsNullOrWhiteSpace(last)) return null;
            last = last.Trim();
            string lower = last.ToLowerInvariant();
            bool es = lower.EndsWith("s") || lower.EndsWith("x") || lower.EndsWith("z") || lower.EndsWith("ch") || lower.EndsWith("sh");
            return last + (es ? "es" : "s");
        }

        /// <summary>Two last names that are the same family name (case aside). Two blanks are not.</summary>
        public static bool SameName(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Those linked to each other, directly or through others: each group's members (indexes below
        /// <paramref name="count"/>), smallest index first, the groups in the order of their first member.
        /// </summary>
        public static List<List<int>> Clusters(int count, IEnumerable<(int a, int b)> links)
        {
            var root = new int[Math.Max(0, count)];
            for (int i = 0; i < root.Length; i++) root[i] = i;
            int Find(int i)
            {
                while (root[i] != i)
                {
                    root[i] = root[root[i]];
                    i = root[i];
                }
                return i;
            }
            foreach (var (a, b) in links ?? Enumerable.Empty<(int, int)>())
            {
                if (a < 0 || b < 0 || a >= count || b >= count) continue;
                int ra = Find(a), rb = Find(b);
                if (ra != rb) root[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
            var groups = new Dictionary<int, List<int>>();
            var order = new List<int>();
            for (int i = 0; i < root.Length; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out var g))
                {
                    groups[r] = g = new List<int>();
                    order.Add(r);
                }
                g.Add(i);
            }
            return order.Select(r => groups[r]).ToList();
        }

        /// <summary>A family's name is talked of once it has this many of its own here.</summary>
        public const int FamilyAtLeast = 2;

        /// <summary>
        /// What a family's name stands for: each side of its members' names (RepMath's facets), averaged over them all.
        /// Null with nobody.
        /// </summary>
        public static float[] FamilyStanding(IList<float[]> members)
        {
            if (members == null || members.Count == 0) return null;
            var sum = new float[RepMath.Facets];
            int n = 0;
            foreach (var m in members)
            {
                if (m == null) continue;
                n++;
                for (int f = 0; f < RepMath.Facets && f < m.Length; f++) sum[f] += m[f];
            }
            if (n == 0) return null;
            for (int f = 0; f < RepMath.Facets; f++) sum[f] /= n;
            return sum;
        }

        /// <summary>
        /// The sides a family's name shows (<see cref="RepMath.Shows"/> or more either way), strongest first, at most
        /// <paramref name="max"/>: each facet and which way it leans.
        /// </summary>
        public static List<(RepFacet facet, bool plus)> FamilySides(float[] standing, int max = 3)
        {
            var list = new List<(RepFacet, bool)>();
            if (standing == null) return list;
            foreach (int f in Enumerable.Range(0, Math.Min(RepMath.Facets, standing.Length)).OrderByDescending(f => Math.Abs(standing[f])))
            {
                if (list.Count >= max || RepMath.Degree(standing[f]) == 0) break;
                list.Add(((RepFacet)f, standing[f] > 0f));
            }
            return list;
        }

        /// <summary>Whether a side of a name speaks well of them (a temper speaks against): as Reputations.Good.</summary>
        public static bool GoodSide(RepFacet f, bool plus) => (plus ? 1f : -1f) * RepMath.Worth[(int)f] > 0f;

        /// <summary>A share of the stories told about one of a known family that bring the family's name into it.</summary>
        public const float FamilyGossipChance = 0.3f;

        // ---------------- A family supper ----------------

        /// <summary>A supper is for this many of the family at home, or more.</summary>
        public const int SupperAtLeast = 3;
        /// <summary>The chance of a supper on a day a family could have one, and the days between one family's suppers.</summary>
        public const float SupperChance = 0.2f;
        public const int SupperGapDays = 6;

        /// <summary>A family supper can be had: enough of them at home, and a meal in the stores for each.</summary>
        public static bool Supper(int atHome, int meals) => atHome >= SupperAtLeast && meals >= atHome;

        // ---------------- Grown brothers and sisters ----------------

        /// <summary>Opinions at which grown siblings are close (both this or more) or rivals (either this or less).</summary>
        public const int SiblingClose = 20, SiblingRival = -10;
        /// <summary>The chance a day that two grown siblings at home have a moment, and the days between their moments.</summary>
        public const float SiblingChance = 0.15f;
        public const int SiblingGapDays = 5;

        /// <summary>How two grown siblings get on: rivals if either can't stand the other, close if both think well of the other.</summary>
        public static SiblingTake Siblings(int opinionAB, int opinionBA)
        {
            if (opinionAB <= SiblingRival || opinionBA <= SiblingRival) return SiblingTake.Rival;
            return opinionAB >= SiblingClose && opinionBA >= SiblingClose ? SiblingTake.Close : SiblingTake.None;
        }

        // ---------------- An ageing parent ----------------

        /// <summary>The age (in human years) from which a parent is looked in on by their grown children.</summary>
        public const float ElderAge = 60f;

        /// <summary>
        /// The chance a day that a grown child looks in on their parent: none before <see cref="ElderAge"/>; more past
        /// 75; most when the parent is ill or laid up.
        /// </summary>
        public static float LookInChance(float age, bool ill)
        {
            if (age < ElderAge) return 0f;
            if (ill) return 0.6f;
            return age >= 75f ? 0.2f : 0.12f;
        }

        /// <summary>The days between one child's visits to their parent: a day when the parent is ill, else four.</summary>
        public static int LookInGapDays(bool ill) => ill ? 1 : 4;
    }
}
