using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the third round of variety (Dilemmas/DilemmaWorkers_Variety3.cs, Happenings/Workers_Variety3.cs,
    /// Happenings/Dangers.cs): when a question about the inspired hands, the food that won't keep, the stores in the rain,
    /// the old walls or the new room is worth the council's time, and the dangers on the map: how often each one hurts,
    /// how big the hurt is and how long it waits. No game types, for the tests.
    /// </summary>
    public static class VarietyThreeMath
    {
        // ================= The council's questions =================

        /// <summary>A material is the colony's fine one for a piece of art when it's worth this many times the plain one, a unit.</summary>
        public const float FineFactor = 3f;

        public static bool Fine(float fineValue, float plainValue) => fineValue > 0f && plainValue > 0f && fineValue >= plainValue * FineFactor;

        /// <summary>The food that won't keep: it rots within this many days where it lies.</summary>
        public const float SpoilsWithinDays = 1.5f;

        public static bool SpoilsSoon(float daysLeft) => daysLeft >= 0f && daysLeft <= SpoilsWithinDays;

        /// <summary>Worth asking about from this much food (in the game's nutrition: a simple meal takes half of one).</summary>
        public const float SpoilingWorthNutrition = 5f;

        public static bool SpoilingWorth(float nutrition) => nutrition >= SpoilingWorthNutrition;

        /// <summary>The meals a cooking bill makes of the food: one for each <paramref name="perMeal"/> of it, two at least, twenty at most.</summary>
        public static int Meals(float nutrition, float perMeal)
        {
            if (nutrition <= 0f || perMeal <= 0f) return 0;
            return Math.Max(2, Math.Min(20, (int)Math.Floor(nutrition / perMeal)));
        }

        /// <summary>The stores in the rain: a stockpile with at least this many cells under the open sky...</summary>
        public const int OpenCells = 6;

        /// <summary>...and at least this many stacks lying there that the weather wears away.</summary>
        public const int WornStacks = 3;

        public static bool RainWorth(int openCells, int wornStacks) => openCells >= OpenCells && wornStacks >= WornStacks;

        /// <summary>The old walls: a ruin of at least this many wall pieces near the colony.</summary>
        public const int RuinPieces = 6;

        public static bool RuinWorth(int pieces) => pieces >= RuinPieces;

        /// <summary>A new room is the better one when it's this much more impressive than where they sleep now.</summary>
        public const float RoomMargin = 10f;

        public static bool BetterRoom(float newImpressiveness, float theirs) => newImpressiveness >= theirs + RoomMargin;

        // ================= The dangers on the map =================

        /// <summary>The creak at the dig: the chance the rock comes down on whoever digs on at that face, before the scale.</summary>
        public const float DigOnChance = 0.25f;

        /// <summary>A rockfall's crush, before the body's own toughness: never more than a part has left.</summary>
        public const float RockfallLow = 4f, RockfallHigh = 9f;

        /// <summary>Smoke in the dry grass: the chance whoever stamps it out burns a hand or a foot, before the scale.</summary>
        public const float StampBurnChance = 0.12f;

        public const float StampBurnLow = 2f, StampBurnHigh = 5f;

        /// <summary>Left alone, the smoulder catches within this many hours (a fire of the game's own).</summary>
        public const float FlareLowHours = 1f, FlareHighHours = 3f;

        /// <summary>The leaning tree, left standing: the chance an hour it comes down in a wind, and in still air.</summary>
        public const float TreeFallWindy = 0.08f, TreeFallStill = 0.004f;

        /// <summary>How long the leaning tree is watched, in days: past that it has settled.</summary>
        public const float TreeWatchDays = 6f;

        public static float TreeFallPerHour(bool windy) => windy ? TreeFallWindy : TreeFallStill;

        /// <summary>The share of a wall's hit points a falling tree takes (never all of them: the wall stands, broken).</summary>
        public const float TreeWallShare = 0.6f;

        /// <summary>What a falling tree does to a wall with <paramref name="hp"/> of <paramref name="max"/> hit points: never all of them.</summary>
        public static int TreeWallDamage(int hp, int max)
        {
            if (hp <= 1 || max <= 0) return 0;
            int want = (int)Math.Ceiling(max * TreeWallShare);
            return Math.Max(0, Math.Min(want, hp - 1));
        }

        /// <summary>Whoever is under the falling tree: a crush, before the body's own toughness.</summary>
        public const float TreeCrushLow = 4f, TreeCrushHigh = 8f;

        /// <summary>The old shell set off from a distance: the chance a stone thrown up by the blast cuts whoever set it off.</summary>
        public const float BlastStoneChance = 0.15f;

        public const float BlastStoneLow = 2f, BlastStoneHigh = 5f;

        /// <summary>Nobody this close when the shell goes up, and whoever sets it off stands this far.</summary>
        public const float ShellClear = 7f, ShellStandOff = 9f;

        /// <summary>The twitching scrap: the chance the last of its charge burns whoever breaks it up, or whoever handles it after.</summary>
        public const float ScrapSparkBreak = 0.15f, ScrapSparkLeft = 0.25f;

        public const float ScrapBurnLow = 3f, ScrapBurnHigh = 6f;

        /// <summary>The twitching scrap, left: it stays live this many days, then it's only scrap.</summary>
        public const float ScrapLiveDays = 3f;

        /// <summary>The ground giving: the chance it gives under each of the first few who cross it.</summary>
        public const float GroundGiveChance = 0.3f;

        /// <summary>How many crossings it is tried for before it settles, and how long, in days.</summary>
        public const int GroundCrossings = 3;
        public const float GroundWatchDays = 4f;

        public const float GroundBruiseLow = 3f, GroundBruiseHigh = 6f;

        /// <summary>One time in this many the foot that goes through takes a crack on its bone, not a bruise.</summary>
        public const int GroundCrackOneIn = 4;

        /// <summary>How long a danger's sign waits for the answer before it goes the way of the silence, in hours.</summary>
        public const float SignHours = 8f;

        /// <summary>
        /// The expected Rimfolk-made hurts a year from one danger: how often it comes (a year), the share of the time its risky
        /// answer is chosen, and that answer's chance after the scale. For the docs' and the tests' rate check.
        /// </summary>
        public static float HurtsAYear(float comesAYear, float riskyShare, float chance, float scale) =>
            Math.Max(0f, comesAYear) * Clamp01(riskyShare) * Clamp01(chance * Math.Max(0f, scale));

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>The clutch in the grass: between this many eggs.</summary>
        public const int EggsLow = 3, EggsHigh = 6;

        /// <summary>The ambrosia: worth asking about from this many ripe bushes.</summary>
        public const int AmbrosiaRipe = 3;

        public static bool AmbrosiaWorth(int ripe) => ripe >= AmbrosiaRipe;

        /// <summary>Picked for one night: as many as the colony has people, never more than there are.</summary>
        public static int AmbrosiaForANight(int ripe, int people) => Math.Max(0, Math.Min(ripe, Math.Max(1, people)));
    }
}
