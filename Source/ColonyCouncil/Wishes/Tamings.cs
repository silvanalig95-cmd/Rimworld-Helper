using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How many animals the colony has tamed, ever (CouncilGameComponent.animalsTamed): each colonist's own record of
    /// animals tamed, read once an hour, its rises added up (PokéWorld's catches count there too). For the wish and the
    /// ambition that ask for a new animal tamed: a chick hatched or a dog bought is no taming, and a hen slaughtered
    /// takes none back. Whoever joins brings no count of their own: only rises seen while they're the colony's count.
    /// </summary>
    public static class Tamings
    {
        private static readonly Dictionary<int, int> seen = new Dictionary<int, int>();
        private static Game game;

        /// <summary>The colony's tamings so far (0 before any, and from the version that began counting them).</summary>
        public static int Total => CouncilGameComponent.Instance?.animalsTamed ?? 0;

        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null) return;
            if (game != Current.Game)
            {
                game = Current.Game;
                seen.Clear();
            }
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                if (p?.records == null) continue;
                int n = p.records.GetAsInt(RecordDefOf.AnimalsTamed);
                if (seen.TryGetValue(p.thingIDNumber, out int was) && n > was) comp.animalsTamed += n - was;
                seen[p.thingIDNumber] = n;
            }
        }
    }
}
