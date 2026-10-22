using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure part of things that come to exist on the map because an answer said so (OnMapThings.cs): how many trees a
    /// guest fells for the wood they cut, how the logs are shared out among the stumps, which of a list of things fits
    /// the colony's era, how many saplings the children plant; and the checks that keep a told moment true (a visitor's
    /// dog, an animal in decline, an old injury). No game types, so the tests can run it.
    /// </summary>
    public static class OnMapMath
    {
        /// <summary>At most this many trees are felled for one answer, however much wood it names.</summary>
        public const int MaxTrees = 6;

        /// <summary>At most this many saplings are planted for one answer.</summary>
        public const int MaxSaplings = 6;

        /// <summary>
        /// How many of the nearest trees (their wood, in the order they're nearest) are felled for <paramref name="wanted"/>
        /// logs: the fewest whose wood reaches it, never more than <paramref name="max"/>; none when no wood is wanted or
        /// no tree gives any.
        /// </summary>
        public static int TreesToFell(IList<int> yields, int wanted, int max = MaxTrees)
        {
            if (yields == null || wanted <= 0 || max <= 0) return 0;
            int got = 0, n = 0;
            for (int i = 0; i < yields.Count && n < max; i++)
            {
                if (yields[i] <= 0) continue;
                got += yields[i];
                n = i + 1;
                if (got >= wanted) break;
            }
            return got > 0 ? n : 0;
        }

        /// <summary><paramref name="total"/> logs shared among <paramref name="piles"/> piles as evenly as can be, the first piles taking the odd ones.</summary>
        public static List<int> Shares(int total, int piles)
        {
            var list = new List<int>();
            if (total <= 0) return list;
            if (piles <= 0) piles = 1;
            if (piles > total) piles = total;
            int each = total / piles, odd = total % piles;
            for (int i = 0; i < piles; i++) list.Add(each + (i < odd ? 1 : 0));
            return list;
        }

        /// <summary>
        /// The first of a list of things that exists (tech level -1: not in this game) and belongs to the colony's era (0:
        /// of no era, always fits; otherwise at most <paramref name="colonyTech"/>), or -1 when none does.
        /// </summary>
        public static int FirstFitting(IList<int> techLevels, int colonyTech)
        {
            if (techLevels == null) return -1;
            for (int i = 0; i < techLevels.Count; i++)
            {
                int t = techLevels[i];
                if (t < 0) continue;
                if (t == 0 || t <= colonyTech) return i;
            }
            return -1;
        }

        /// <summary>One sapling for each child of the colony (at least one, for the child who asked), never more than the cap.</summary>
        public static int Saplings(int children, int cap = MaxSaplings) => Math.Max(1, Math.Min(Math.Max(1, cap), children));

        /// <summary>
        /// A dog, by its kind's name or label: the game's husky, labrador retriever and yorkshire terrier, and any other
        /// mod's dog, hound, terrier, retriever, collie, mastiff or shepherd. Not a wolf, a fox or a warg.
        /// </summary>
        public static bool DogKind(string defName, string label)
        {
            string s = ((defName ?? "") + " " + (label ?? "")).ToLowerInvariant();
            if (s.Trim().Length == 0) return false;
            if (s.Contains("wolf") || s.Contains("fox") || s.Contains("warg") || s.Contains("hotdog") || s.Contains("dogfish")) return false;
            foreach (var w in DogWords)
                if (s.Contains(w)) return true;
            return false;
        }

        private static readonly string[] DogWords = { "husky", "labrador", "yorkshire", "terrier", "retriever", "collie", "mastiff", "shepherd", "hound", "dog" };

        /// <summary>
        /// An animal in decline: well on in years for its kind (three quarters of its life expectancy or more), or carrying a
        /// lasting condition that won't get better (a fresh wound or a passing sickness doesn't count).
        /// </summary>
        public static bool Fading(float ageYears, float lifeExpectancy, bool chronic)
        {
            if (chronic) return true;
            return lifeExpectancy > 0f && ageYears >= lifeExpectancy * 0.75f;
        }
    }
}
