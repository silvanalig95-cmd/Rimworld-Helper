using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The plain arithmetic of the camps that come for something on the land (CampKindsLand.cs): whether the colony's
    /// growers are behind with the harvest, the reapers' sheaf in ten, how much a day of fishing or stripping comes to,
    /// half a haul, a beast's price, whether the land has woods enough for woodcutters. No game types: tested in
    /// Tools/Tests (CampLandTests, VarietyTwoTests).
    /// </summary>
    public static class CampLandMath
    {
        /// <summary>Hours a grower gives to bringing the harvest in on a working day, among sowing and everything else.</summary>
        public const float GrowerHarvestHours = 1.5f;

        /// <summary>The walk between plants, on top of the harvest work itself.</summary>
        public const float WalkFactor = 1.5f;

        /// <summary>Only this close to the season's end does a field left standing look like a loss.</summary>
        public const float MaxDaysLeft = 15f;

        /// <summary>Fewer ripe plants than this is no work for a crew.</summary>
        public const int MinPlants = 40;

        /// <summary>The reapers' share in kind: one sheaf in ten.</summary>
        public const float Sheaf = 0.1f;

        /// <summary>Fishing trips a grown fisher makes in a day.</summary>
        public const int TripsEach = 2;

        /// <summary>Hours of harvest work standing ripe (or ripe before the season ends), from the plants' work in ticks at an even pace.</summary>
        public static float RipeHours(float workTicks) => Math.Max(0f, workTicks) * WalkFactor / 2500f;

        /// <summary>
        /// More crop than the colony's growers can bring in before the season ends: the hours it takes against what the
        /// growers (their plant work speeds added up) can spare a day, over the days left. Never with the season's end far
        /// off, already past, or no crop to speak of.
        /// </summary>
        public static bool HarvestBehind(float ripeHours, int ripePlants, float growerSpeed, float daysLeft)
        {
            if (ripePlants < MinPlants || daysLeft <= 0f || daysLeft > MaxDaysLeft) return false;
            float spare = Math.Max(0f, growerSpeed) * GrowerHarvestHours * daysLeft;
            return ripeHours > spare;
        }

        /// <summary>
        /// The reapers' share of one plant's yield at <paramref name="part"/> (a tenth), the fraction carried over to the
        /// next plant so a small yield still adds up to whole sheaves: never more than the yield.
        /// </summary>
        public static int Share(int yield, float part, ref float carry)
        {
            if (yield <= 0 || part <= 0f) return 0;
            float owed = yield * part + carry;
            int whole = Math.Min(yield, (int)Math.Floor(owed + 1e-4f));
            carry = Math.Max(0f, owed - whole);
            return whole;
        }

        /// <summary>Fishing trips the whole camp makes in a day.</summary>
        public static int FishTrips(int adults) => Math.Max(0, adults) * TripsEach;

        /// <summary>Pieces of the ruins a crew of so many strips in a day.</summary>
        public static int StripsADay(int adults) => adults <= 0 ? 0 : 2 + (adults + 1) / 2;

        /// <summary>Half of what they have (rounded down), the colony's share of the haul; nothing of a stack too small to split.</summary>
        public static int Half(int count) => count < 2 ? 0 : count / 2;

        /// <summary>What they pay for one of the colony's beasts: seven tenths of its worth, in tens.</summary>
        public static int BeastPrice(float worth) => worth <= 0f ? 0 : Math.Max(10, (int)Math.Round(worth * 0.7f / 10f, MidpointRounding.AwayFromZero) * 10);

        /// <summary>Woodcutters come only to land with at least this many grown wild trees clear of the colony.</summary>
        public const int MinWoods = 30;

        /// <summary>A tree this far grown is worth felling.</summary>
        public const float TreeGrown = 0.6f;

        /// <summary>A stand of trees: a tree with at least this many more within a dozen cells.</summary>
        public const int StandAround = 5;

        public static bool WoodsEnough(int wildTrees) => wildTrees >= MinWoods;
    }
}
