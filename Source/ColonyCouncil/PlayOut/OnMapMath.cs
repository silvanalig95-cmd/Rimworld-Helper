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

        /// <summary>How long those an answer says felled the trees have to fell them: half a day.</summary>
        public const int FellingTicks = 10 * TicksPerHour;

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

        // ---------------- Things kept for a while (OnMapTimed.cs) and plays of one thing (OnMapPlays.cs) ----------------

        public const int TicksPerHour = 2500;

        /// <summary>The dark hours when lanterns burn and a door is bolted: from five in the evening, and until dawn.</summary>
        public const int Dusk = 17, Dawn = 6;

        /// <summary>
        /// A pyre is lit a little later, once it's properly dark, by one of the colony who carries the wood out; with
        /// nobody free to go by ten at night, it's lit all the same.
        /// </summary>
        public const int PyreDusk = 18, PyreLatest = 22;

        /// <summary>The wood carried out to light a pyre, when the stores have it.</summary>
        public const int PyreWood = 20;

        /// <summary>A door is bolted only once the house is abed, from ten at night until five in the morning.</summary>
        public const int BoltFrom = 22, BoltTo = 5;

        /// <summary>Lanterns stand at least this far apart.</summary>
        public const float LanternSpacing = 4.9f;

        /// <summary>At most this many lanterns are set out for one night, whatever the answer asks.</summary>
        public const int MaxLanterns = 16;

        /// <summary>At most this many of the enemy dead are carried out for one answer.</summary>
        public const int MaxDead = 12;

        /// <summary>
        /// Where each of a row of <paramref name="graves"/> graves lies from the first, as steps along the row: side by
        /// side for one trench (<paramref name="apart"/> false), a step of ground between them for graves of their own.
        /// A grave is one cell wide across the row, so the row runs one cell (or two, with the gap) a grave.
        /// </summary>
        public static List<int> GraveRow(int graves, bool apart)
        {
            var steps = new List<int>();
            int step = apart ? 2 : 1;
            for (int i = 0; i < Math.Max(0, Math.Min(graves, MaxDead)); i++) steps.Add(i * step);
            return steps;
        }

        /// <summary>The most people a doctor or a pot of soup goes round to.</summary>
        public const int MaxRounds = 8;

        /// <summary>
        /// Ticks from the hour of the day <paramref name="hourNow"/> (with its fraction) until the next time the clock
        /// shows <paramref name="hour"/>: never zero, so "until dawn" at dawn means the next one.
        /// </summary>
        public static int TicksUntil(float hourNow, int hour)
        {
            float h = (hour - hourNow) % 24f;
            if (h <= 0.01f) h += 24f;
            return (int)Math.Round(h * TicksPerHour);
        }

        /// <summary>Within the hours from <paramref name="from"/> to <paramref name="to"/>, wrapping past midnight.</summary>
        public static bool Between(int hour, int from, int to)
        {
            hour = ((hour % 24) + 24) % 24;
            if (from == to) return true;
            return from < to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>How many lanterns go out: what the answer asks, never more than there are places or the cap, at least one if asked.</summary>
        public static int Lanterns(int wanted, int places) => Math.Max(0, Math.Min(Math.Min(wanted, MaxLanterns), places));

        /// <summary>
        /// How long the doctor or the cook stops with each person on the rounds: the round's time shared out (half of it
        /// walking), never less than a few minutes or more than half an hour.
        /// </summary>
        public static int RoundsStop(int durationTicks, int people) =>
            Math.Max(300, Math.Min(TicksPerHour / 2, durationTicks / Math.Max(1, people) / 2));

        /// <summary>Went round: half of those on the list seen (rounded up), and at least one.</summary>
        public static bool RoundsCount(int seen, int people) => seen > 0 && seen * 2 >= people;

        /// <summary>How long a fire set in someone's room lasts on the wood the answer set aside: as much as it holds, in days of burning.</summary>
        public static float FireDays(float wood, float capacity, float perDay)
        {
            if (wood <= 0f || capacity <= 0f) return 0f;
            float fuel = Math.Min(wood, capacity);
            return perDay > 0f ? fuel / perDay : 0f;
        }

        /// <summary>
        /// The warmer room really is warmer: the one given up is no colder than the other, give or take half a degree (a
        /// swap is only made when it helps the one who's cold).
        /// </summary>
        public static bool Warmer(float given, float kept) => given >= kept - 0.5f;

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
