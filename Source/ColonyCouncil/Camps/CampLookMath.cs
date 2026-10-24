using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers behind a camp that looks lived-in (CampLook.cs): how many animals a camp may bring, when they're fed,
    /// when a porter carries a load out or back, how long a farewell walk and a smith's repair may take, and which of
    /// the colony's damaged buildings a smith mends first. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampLookMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>A handful at most: pack beasts, a herd and its dog together.</summary>
        public const int MaxAnimals = 5;

        /// <summary>How many of a group come, given how many animals are already coming: never past the cap, never fewer than none.</summary>
        public static int AnimalsFor(int wanted, int already) => Math.Max(0, Math.Min(wanted, MaxAnimals - Math.Max(0, already)));

        /// <summary>Their animals are fed from the camp's own stores when this hungry (before the game would send them after the colony's crops)...</summary>
        public const float FeedBelow = 0.5f;
        /// <summary>...up to this.</summary>
        public const float FeedTo = 0.8f;
        /// <summary>A grazing beast crops the grass near its pasture now and then while it's below this.</summary>
        public const float GrazeBelow = 0.85f;

        /// <summary>The fed level after the camp's feeding: unchanged unless below <see cref="FeedBelow"/>.</summary>
        public static float Fed(float level) => level < FeedBelow ? Math.Max(level, FeedTo) : level;

        /// <summary>A porter's load lying at its home spot (within this) goes out to the other end; anywhere else it comes home.</summary>
        public const float AtSpot = 3.5f;

        public static bool CarryOut(float distFromHome) => distFromHome <= AtSpot;

        /// <summary>A farewell walk may take this long before they go anyway.</summary>
        public const int WalkTicks = TicksPerHour * 5;

        /// <summary>The walk is over: done, past its time, or with nobody left to walk it.</summary>
        public static bool WalkOver(bool done, bool walkerOk, int now, int until) => done || !walkerOk || (until >= 0 && now > until);

        /// <summary>A smith sets out to mend one of the colony's buildings only when it's below this share of its hit points.</summary>
        public const float RepairBelow = 0.9f;
        /// <summary>...and gives it up if the work isn't done within this.</summary>
        public const int RepairTicks = TicksPerDay;
        /// <summary>How far from the camp a smith will go to mend something.</summary>
        public const float RepairRange = 110f;

        /// <summary>
        /// Which damaged building a smith mends first (lower is better): the nearest to the camp, with doors and gates
        /// counted at half their distance (the hinge on the gate), and nothing out of range.
        /// </summary>
        public static float RepairScore(float dist, bool door) => dist > RepairRange ? float.MaxValue : door ? dist * 0.5f : dist;

        /// <summary>Saplings in a ring round the old fire: at least this far out, and less than <see cref="RingOuter"/>.</summary>
        public const float RingInner = 1.9f, RingOuter = 3f;

        public static bool InRing(int dx, int dz)
        {
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            return d >= RingInner && d < RingOuter;
        }
    }
}
