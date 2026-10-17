using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers behind the colony's animals in Rimfolk's systems, with no game types so the tests can run them: when an
    /// animal counts as old, how big a herd a keeper wants, how many head a hungry colony would cull, what a beloved
    /// animal's day weighs on the calendar, and, with PokéWorld, which level a Pokémon just passed and how far along the
    /// Pokédex is. See Animals/AnimalFacts.cs for what reads the game.
    /// </summary>
    public static class AnimalMath
    {
        // ---------------- Age ----------------

        /// <summary>How far through its life an animal is: its age over its kind's life expectancy (0 when unknown).</summary>
        public static float LifeShare(float ageYears, float lifeExpectancy) =>
            lifeExpectancy <= 0.01f || ageYears <= 0f ? 0f : ageYears / lifeExpectancy;

        /// <summary>Old enough that its person starts to notice: grey at the muzzle, slow in the mornings.</summary>
        public const float OldShare = 0.8f;

        public static bool IsOld(float ageYears, float lifeExpectancy) => LifeShare(ageYears, lifeExpectancy) >= OldShare;

        // ---------------- The herd ----------------

        /// <summary>The herd a keeper sets their heart on: at least <paramref name="least"/> head, and always a few more than now.</summary>
        public static int HerdTarget(int now, int least = 8, int more = 4) => Math.Max(least, Math.Max(0, now) + more);

        /// <summary>
        /// How many head a hungry colony would cull: about a quarter of the herd, at least one and at most four, and never
        /// the last two (a herd that can't breed again isn't worth saving the feed for).
        /// </summary>
        public static int CullCount(int herd)
        {
            if (herd <= 2) return 0;
            int n = Math.Max(1, Math.Min(4, herd / 4));
            return Math.Min(n, herd - 2);
        }

        /// <summary>A colony is going hungry: its food lasts fewer days than this, with a herd eating from the same stores.</summary>
        public const float FamineDays = 6f;

        public static bool Famine(float foodDays, int herd) => herd >= 4 && foodDays >= 0f && foodDays < FamineDays;

        /// <summary>A barn for the herd: animal beds for this many of them (at least two, at most six).</summary>
        public static int BarnBeds(int herd) => Math.Max(2, Math.Min(6, herd));

        // ---------------- Training ----------------

        /// <summary>The share of what it can learn that it has learned (1 when there is nothing it can learn).</summary>
        public static float TrainedShare(int learned, int total) => total <= 0 ? 1f : Math.Max(0f, Math.Min(1f, learned / (float)total));

        // ---------------- The calendar ----------------

        /// <summary>The colony's first foal: a day worth about a new crop's, a little more.</summary>
        public const float FoalWeight = 22f;

        /// <summary>
        /// A beloved animal's day: more for one bonded, for one that fought for its person, and for the years it was with
        /// the colony (up to five).
        /// </summary>
        public static float CompanionWeight(float years, bool bonded, bool hero) =>
            12f + (bonded ? 8f : 0f) + (hero ? 6f : 0f) + 2f * Math.Min(Math.Max(years, 0f), 5f);

        /// <summary>Only an animal with the colony at least this long (in years), or one that fought for its person, is a day on the calendar.</summary>
        public const float CompanionYears = 1f;

        public static bool CompanionDay(float years, bool bonded, bool hero) => bonded && (hero || years >= CompanionYears);

        // ---------------- PokéWorld ----------------

        /// <summary>The levels a trainer marks: the first big step, halfway, the top.</summary>
        public static readonly int[] Milestones = { 25, 50, 75, 100 };

        /// <summary>The highest milestone passed going from <paramref name="before"/> to <paramref name="now"/>, or 0.</summary>
        public static int MilestonePassed(int before, int now)
        {
            int best = 0;
            if (before <= 0 || now <= before) return 0;
            foreach (int m in Milestones)
                if (before < m && now >= m) best = m;
            return best;
        }

        /// <summary>PokéWorld's friendship runs 0 to 255; at this much a trainer and their Pokémon may bond.</summary>
        public const int BondFriendship = 220;

        /// <summary>A share of the Pokédex caught (0 when the Pokédex has no species).</summary>
        public static float DexShare(int caught, int species) => species <= 0 ? 0f : Math.Max(0f, Math.Min(1f, caught / (float)species));

        /// <summary>The Pokédex goal a trainer sets: this many more species, but no more than are left to catch.</summary>
        public static int DexGoal(int caught, int species, int more) => Math.Max(0, Math.Min(Math.Max(0, species), Math.Max(0, caught) + Math.Max(1, more)));

        /// <summary>A rare sighting: PokéWorld's rarity runs 1 (common) to 10 (legendary); these are the rare ones that still roam.</summary>
        public static bool RareSighting(int rarity) => rarity >= 6 && rarity <= 9;

        /// <summary>
        /// An evolution seen between two looks: the same animal (the same birth tick and the same trainer) under a new kind,
        /// and the old one gone without dying. Kinds are compared by name.
        /// </summary>
        public static bool Evolved(string kindBefore, string kindNow, bool oldGoneAlive) =>
            oldGoneAlive && !string.IsNullOrEmpty(kindBefore) && !string.IsNullOrEmpty(kindNow) && kindBefore != kindNow;
    }
}
