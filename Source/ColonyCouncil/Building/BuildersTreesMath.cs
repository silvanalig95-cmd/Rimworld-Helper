namespace ColonyCouncil
{
    /// <summary>
    /// When a builder leaves a tree in the way of their build to the plant cutters (the player, 12 November 2026: "if a pawn
    /// that wants to do construction on something where they need to cut trees for it, then they should rather do another
    /// construction job first so the plant cutters can get the trees first"). Pure, for the tests.
    /// </summary>
    public static class BuildersTreesMath
    {
        /// <summary>A tree marked for the cutters waits this long (half a day) before a builder cuts it after all.</summary>
        public const int WaitTicks = 30000;

        /// <summary>
        /// Whether the builder leaves it: a tree, in the way of a build they took on themselves (not one the player ordered),
        /// while another build is free for them, someone of the home cuts plants, and it hasn't waited marked half a day.
        /// </summary>
        public static bool Leave(bool forced, bool tree, bool otherBuildFree, bool cutterThere, int markedForTicks) =>
            !forced && tree && otherBuildFree && cutterThere && markedForTicks < WaitTicks;
    }
}
