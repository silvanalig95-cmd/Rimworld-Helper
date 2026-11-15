using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How many lines the world events corner gives (UI/WorldEventsCorner.cs): one for each world event running now, three at
    /// most; past that, the last of the three says "and N more". No game types, so Tools/Tests checks it.
    /// </summary>
    public static class WorldEventsCornerMath
    {
        /// <summary>The most lines the corner takes, "and N more" among them.</summary>
        public const int MostLines = 3;

        /// <summary>How many of the running world events get a line of their own.</summary>
        public static int OwnLines(int running, int most = MostLines)
        {
            if (running <= 0 || most <= 0) return 0;
            return running <= most ? running : most - 1;
        }

        /// <summary>How many are left for the "and N more" line (0: no such line).</summary>
        public static int More(int running, int most = MostLines) => Math.Max(0, running - OwnLines(running, most));
    }
}
