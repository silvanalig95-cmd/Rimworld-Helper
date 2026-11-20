using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's own stories as the game's tales, for its art (docs/content-update-proposals.md, C17; Things/ArtTales.cs):
    /// which moments become one, and how often. No game types, so Tools/Tests reads the same numbers the game uses.
    /// </summary>
    public static class ArtTalesMath
    {
        /// <summary>The tales Rimfolk records (the TaleDefs' names, Defs/TaleDefs/Rimfolk_Tales.xml).</summary>
        public static readonly string[] Kinds =
        {
            "Rimfolk_Tale_GodSeen", "Rimfolk_Tale_SacredBeast", "Rimfolk_Tale_OfferingTaken", "Rimfolk_Tale_FestivalKept", "Rimfolk_Tale_FestivalBeast",
            "Rimfolk_Tale_BeastNamed", "Rimfolk_Tale_HerdLeaderDied", "Rimfolk_Tale_LineHeld"
        };

        /// <summary>
        /// At least this many days between two tales of one kind at a colony: the moments are rare already, and the art should
        /// show more than one of them.
        /// </summary>
        public static int GapDays(string kind)
        {
            switch (kind)
            {
                case "Rimfolk_Tale_GodSeen": return 5;
                case "Rimfolk_Tale_SacredBeast": return 3;
                case "Rimfolk_Tale_OfferingTaken": return 10;
                case "Rimfolk_Tale_LineHeld": return 2;
                default: return 0;
            }
        }

        public static bool Due(string kind, int last, int now, int ticksPerDay = 60000)
        {
            int gap = GapDays(kind);
            return gap <= 0 || last < 0 || now < last || now - last >= gap * ticksPerDay;
        }

        /// <summary>A raid's lessons at a line say it held: they came through the gate, and nobody fell (DefenseLineMath.Lesson.Held, the only lesson).</summary>
        public static bool Held(IList<string> lessons) => lessons != null && lessons.Count == 1 && lessons[0] == "Held";
    }
}
