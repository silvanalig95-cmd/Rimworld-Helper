using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A mission the colony was offered or took on (filled in with the missions themselves).</summary>
    public class Mission : IExposable
    {
        public int id;
        public string kind;
        public Pawn x;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref x, "x");
        }
    }

    public static class Missions
    {
        public static bool Holds(Pawn p) => false;

        public static void Tick(StoriesComponent comp, int now)
        {
        }

        public static void Hourly(StoriesComponent comp, int now)
        {
        }
    }
}
