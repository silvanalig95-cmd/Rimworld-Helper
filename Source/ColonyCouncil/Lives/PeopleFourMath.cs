using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's own people, the fourth round (the player's request, 6 November 2026: "do another big content update to
    /// existing mechanics"): the pure rules behind the new life goals, wishes and quirks, without game types. Tools/Tests
    /// checks them (PeopleFourTests). The defs are in the People4 files of Defs/AspirationDefs, WishDefs, QuirkDefs,
    /// EpithetDefs and DeedDefs.
    /// </summary>
    public static class PeopleFourMath
    {
        // ---------------- Life goals ----------------

        /// <summary>Sure on their feet: a month (two seasons of a 60-day year) without another hurt from everyday life, or being downed.</summary>
        public const float SureFootedDays = 30f;

        /// <summary>Only someone hurt in the last ten days sets their heart on it.</summary>
        public const float RecentHurtDays = 10f;

        /// <summary>
        /// Days since the goal began, or since their last hurt or fall after it began, whichever is latest: the clock starts
        /// again at each one.
        /// </summary>
        public static float CleanDays(int now, int started, int lastHurt, int lastDowned, int ticksPerDay)
        {
            int from = started;
            if (lastHurt > from) from = lastHurt;
            if (lastDowned > from) from = lastDowned;
            if (ticksPerDay <= 0 || now <= from) return 0f;
            return (now - from) / (float)ticksPerDay;
        }

        /// <summary>How far along a stretch without a hurt is, 0 to 1.</summary>
        public static float Share(float days, float target) => target <= 0f ? 1f : Math.Max(0f, Math.Min(1f, days / target));

        /// <summary>A new colony's people, to see it grow: two more at least, and never fewer than the goal's own number.</summary>
        public const int HomeGrowsBy = 2;

        public static int HomeTarget(int peopleNow, int amount) => Math.Max(amount, peopleNow + HomeGrowsBy);

        /// <summary>
        /// The journey to the other colony is made: they stand on one of the colony's homes (<paramref name="hereKey"/>) that
        /// isn't the one they belonged to when they set their heart on it.
        /// </summary>
        public static bool Visited(int homeKey, int hereKey, int none) => homeKey != none && hereKey != none && hereKey != homeKey;

        /// <summary>Someone they're this fond of, living at another of the colony's homes, is who they'd go to see.</summary>
        public const int CloseOpinion = 30;

        // ---------------- Names ----------------

        /// <summary>
        /// The deeds the colony talks about that count for a name, and the record each counts one more each time it's done,
        /// merged with the last or not (Defs/Misc/Rimfolk_Records_People4.xml; the names in
        /// Defs/EpithetDefs/Rimfolk_Epithets_People4.xml). Null for a deed that counts for none.
        /// </summary>
        public static string DeedRecord(string deed)
        {
            switch (deed)
            {
                case "Rimfolk_Deed_Rescue": return "Council_Rescues";
                case "Rimfolk_Deed_NightWatch": return "Council_NightWatches";
                case "Rimfolk_Deed_ContestWon": return "Council_ContestsWon";
                case "Rimfolk_Deed_Masterwork": return "Council_Masterworks";
                case "Rimfolk_Deed_Rounds": return "Council_RoundsMade";
                case "Rimfolk_Deed_StrangerKindness": return "Council_KindToStrangers";
                default: return null;
            }
        }

        // ---------------- Wishes ----------------

        /// <summary>Grown trees against the walls worth the council's word (a tree beside a wall or a door, in the home area).</summary>
        public const int TreesWorth = 3;

        /// <summary>A tree this far grown is a tree that can come down on a roof.</summary>
        public const float GrownTree = 0.6f;

        public static bool TreesByWallsWorth(int trees) => trees >= TreesWorth;

        /// <summary>Snow this deep (the game's 0 to 1) on the ways by the doors makes slow, cold going.</summary>
        public const float DeepSnow = 0.4f;

        /// <summary>Snow this shallow is cleared.</summary>
        public const float ClearedSnow = 0.15f;

        /// <summary>Deep snow on this many cells by the doors, with almost none of the snow-clearing area marked yet.</summary>
        public const int SnowCellsWorth = 12;

        public const int SnowAreaMarked = 6;

        public static bool SnowWorth(int deepCells, int markedCells) => deepCells >= SnowCellsWorth && markedCells < SnowAreaMarked;

        /// <summary>The ways are clear once nine in ten of their cells are clear.</summary>
        public static bool SnowCleared(int stillDeep, int all) => all > 0 && stillDeep * 10 <= all;
    }
}
