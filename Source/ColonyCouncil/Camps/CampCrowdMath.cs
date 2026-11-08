using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How many camps the colony's land takes (the setting "How many camps", the player's request of 8 November 2026: "my
    /// current map is quite huge, so i don't mind a fair amount of camps ... maybe i can set a setting slider for different
    /// levels of amount of camps possible"). Usual is how camps always came: one at a time, a fortnight or so apart.
    /// </summary>
    public enum CampCrowd
    {
        Rare,
        Usual,
        Many,
        Crowded
    }

    /// <summary>
    /// The levels' numbers and the rules for camps side by side (Camps/CampCrowds.cs): each level's daily chance, its rest
    /// after a camp leaves and how many may stand at once; the map's size and open ground cap that count (a small map never
    /// takes more than one or two, a huge one the level's full count); how far apart two camps keep; and that two peoples
    /// at war with each other never camp on the same map, unless one came to attack the other. Pure (no game types), so
    /// Tools/Tests checks it.
    /// </summary>
    public static class CampCrowdMath
    {
        // ---------------- The levels ----------------

        /// <summary>
        /// Each day's chance that a camp comes, while one could (CampTimer; a remote home more, TravelLand). With the rests
        /// below and stays of three to seven days, a home with room for them all sees about one camp a year (60 days) at
        /// Rare, one and a half at Usual, four or five at Many, about fourteen at Crowded (about one on the land on average,
        /// up to four at once).
        /// </summary>
        public static float DailyChance(CampCrowd c)
        {
            switch (c)
            {
                case CampCrowd.Rare: return 0.03f;
                case CampCrowd.Many: return 0.12f;
                case CampCrowd.Crowded: return 0.3f;
                default: return 0.05f;
            }
        }

        /// <summary>Days of rest after a camp leaves before the next may come (a hospitable or remote home less).</summary>
        public static float GapDays(CampCrowd c)
        {
            switch (c)
            {
                case CampCrowd.Rare: return 22f;
                case CampCrowd.Many: return 5f;
                case CampCrowd.Crowded: return 1f;
                default: return 15f;
            }
        }

        /// <summary>The most camps visiting at once on a map that has room for them all.</summary>
        public static int MostAtOnce(CampCrowd c)
        {
            switch (c)
            {
                case CampCrowd.Many: return 2;
                case CampCrowd.Crowded: return 4;
                default: return 1;
            }
        }

        // ---------------- The map's room ----------------

        /// <summary>Below this many cells (225 by 225 is 50,625) a map takes one camp at most.</summary>
        public const int SmallCells = 45000;
        /// <summary>Below this (250 by 250 is 62,500) two at most.</summary>
        public const int MediumCells = 57000;
        /// <summary>Below this (275 by 275 is 75,625) three at most; this or more, the level's full count.</summary>
        public const int LargeCells = 70000;

        /// <summary>The most camps a map of this many cells takes, whatever the level.</summary>
        public static int SizeCap(int cells) => cells < SmallCells ? 1 : cells < MediumCells ? 2 : cells < LargeCells ? 3 : 4;

        /// <summary>Below this share of the map open to a camp (ground a fire could stand on, clear of the colony), one camp fewer.</summary>
        public const float SomeRoom = 0.3f;
        /// <summary>Below this share, one camp only.</summary>
        public const float LittleRoom = 0.12f;

        /// <summary>
        /// How many camps may visit this map at once: the level's count, no more than the map's size takes, one fewer on a map
        /// mostly mountain, marsh, water or colony, one only on a map with little open ground left; never fewer than one.
        /// </summary>
        public static int Cap(CampCrowd level, int cells, float openShare)
        {
            int cap = Math.Min(MostAtOnce(level), SizeCap(cells));
            if (openShare < LittleRoom) cap = 1;
            else if (openShare < SomeRoom) cap--;
            return Math.Max(1, cap);
        }

        /// <summary>
        /// What keeps another camp from coming for the camps already here: "here" while the land takes one and one is here,
        /// "full" while it takes more and they are all here; null while there is room for another.
        /// </summary>
        public static string Full(int here, int cap) => here <= 0 || here < cap ? null : cap <= 1 ? "here" : "full";

        // ---------------- Keeping apart ----------------

        /// <summary>
        /// Fewest cells between two camps' fires: their own sites, each its reach round its fire (CampBuildMath.Reach) and
        /// a dozen cells of open ground between, never one camp's beds by another's fire.
        /// </summary>
        public const int Apart = 2 * CampBuildMath.Reach + 12;

        /// <summary>Two camps' fires far enough apart.</summary>
        public static bool FarEnough(int ax, int az, int bx, int bz)
        {
            int dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz >= Apart * Apart;
        }

        /// <summary>
        /// Two peoples may both camp on the colony's land: not while at war with each other, unless the one is here to attack
        /// the other (Camps/CampRaid.cs).
        /// </summary>
        public static bool MayShare(bool atWar, bool attack) => !atWar || attack;
    }
}
