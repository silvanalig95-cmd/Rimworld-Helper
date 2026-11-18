namespace ColonyCouncil
{
    /// <summary>
    /// When the one a piece was made for goes to put it on (Integrations/MadeForWear.cs; the player, 18 November 2026: "they
    /// just wear it once they are kind of close by again or back into the base"). No game types, linked in the tests.
    /// </summary>
    public static class MadeForWearMath
    {
        /// <summary>A short walk: the piece this close counts as near, wherever it lies.</summary>
        public const float NearCells = 40f;

        /// <summary>A piece nobody went for in this many days is let go of (it stays in storage like any other).</summary>
        public const float KeepDays = 15f;

        /// <summary>
        /// They go now: free to (not drafted, downed, in a mental state, starving, nor a threat on the map), back in the home
        /// area, and the piece in the home area too or within a short walk.
        /// </summary>
        public static bool GoNow(bool free, bool inHome, bool pieceInHome, float cells) => free && inHome && (pieceInHome || cells <= NearCells);

        /// <summary>The wait has run past its days.</summary>
        public static bool Stale(int since, int now) => now - since > KeepDays * 60000f;
    }
}
