namespace ColonyCouncil
{
    /// <summary>
    /// When Vanilla Factions Expanded - Empire's ballroom stands down (Integrations/BallroomByUse.cs): the room is a temple
    /// (an altar of the colony's faith), a dining room (dining tables), or its only instruments are drums. No game types,
    /// so Tools/Tests checks it.
    /// </summary>
    public static class BallroomMath
    {
        public static bool StandsDown(bool temple, bool diningTables, int instruments, int drums) =>
            temple || diningTables || instruments > 0 && drums >= instruments;
    }
}
