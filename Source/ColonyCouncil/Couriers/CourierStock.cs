using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a trip takes, wherever it lies: its particular things (a neighbour's cargo), the goods of a mission's order
    /// (Missions.Matching: meals, art, blocks...), or the stores by an answer's words (PlayCarry: silver, raw food,
    /// medicine, meals, wood, a defName). And the old way of taking it, all at once.
    /// </summary>
    public static class CourierStock
    {
        public static bool Particular(CourierTrip t) => t.things.Count > 0;

        public static IEnumerable<Thing> Of(CourierTrip t, Map map)
        {
            if (map == null) return Enumerable.Empty<Thing>();
            if (Particular(t)) return t.things.Where(x => x != null && !x.Destroyed && x.Spawned && x.Map == map);
            if (t.source == "mission") return Missions.Matching(map, t.what);
            return PlayCarry.Stock(map, t.what, t.def);
        }

        /// <summary>How much of it the colony still has.</summary>
        public static int Have(CourierTrip t) => t.MapThere ? Of(t, t.map).Sum(x => x.stackCount) : 0;

        /// <summary>The old way: so many taken from the stores at once (the particular things: all that's left of them). How many.</summary>
        public static int TakeNow(CourierTrip t, int n)
        {
            if (!t.MapThere || n <= 0) return 0;
            if (Particular(t))
            {
                int got = 0;
                foreach (var thing in t.things.Where(x => x != null && !x.Destroyed && x.Spawned).ToList())
                {
                    got += thing.stackCount;
                    thing.Destroy();
                }
                t.things.Clear();
                return got;
            }
            if (t.source == "mission") return Missions.Take(t.map, t.what, n);
            return PlayCarry.Take(t.map, t.what, t.def, n);
        }
    }
}
