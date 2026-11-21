namespace ColonyCouncil
{
    /// <summary>
    /// Which way a walking pawn faces its next step (Lean/WalkFacing.cs), as the game's rotation tracker turns anyone who
    /// walks (Pawn_RotationTracker.FaceAdjacentCell): east or west whenever the step goes sideways at all, else north or
    /// south. Pure (no game types): Tools/Tests checks it.
    /// </summary>
    public static class WalkFacingMath
    {
        /// <summary>The game's Rot4 numbers.</summary>
        public const int North = 0, East = 1, South = 2, West = 3, None = -1;

        /// <summary>The facing for a step of <paramref name="dx"/>, <paramref name="dz"/> cells; None for no step.</summary>
        public static int Toward(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return None;
            if (dx > 0) return East;
            if (dx < 0) return West;
            return dz > 0 ? North : South;
        }
    }
}
