namespace ColonyCouncil
{
    /// <summary>
    /// When a lord (the game's group that gives pawns their duties) is over, for Lean/Lords.cs. Pure (no game types):
    /// Tools/Tests checks it.
    /// </summary>
    public static class LordsMath
    {
        /// <summary>Rimfolk's own lord jobs are in this namespace: only these are ended when found empty in a save.</summary>
        public const string OwnNamespace = "ColonyCouncil";

        /// <summary>
        /// The game's own test (Lord.ShouldExist): a lord goes on while it has pawns, or while its job can go on without
        /// them, or while it holds buildings and its job is kept for them.
        /// </summary>
        public static bool ShouldExist(int pawns, int buildings, bool withoutPawns, bool keptForBuildings) =>
            pawns > 0 || withoutPawns || buildings > 0 && keptForBuildings;

        /// <summary>
        /// A lord found in a save (on loading, or once a day) that is ended: one of Rimfolk's own jobs, with nobody in it,
        /// no building of its own and a job that can't go on without pawns. The game's and other mods' lords are left as
        /// they are.
        /// </summary>
        public static bool Swept(string jobNamespace, int pawns, int buildings, bool withoutPawns) =>
            jobNamespace == OwnNamespace && pawns == 0 && buildings == 0 && !withoutPawns;
    }
}
