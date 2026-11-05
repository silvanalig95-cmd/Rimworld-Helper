using Verse;

namespace ColonyCouncil
{
    // In-laws who write when trouble is coming (docs/design/stage2-diplomacy.md, item 6; Diplomacy/MatchAlliance.cs).
    public static partial class RaidersCamps
    {
        /// <summary>
        /// A people married to the colony writes that a raiders' camp is set against one of its homes: found by their letter,
        /// as a neighbouring town's letter finds one (the game's own bandit camp site on the world map, the letter naming the
        /// danger before any raid comes).
        /// </summary>
        public static void WarnedByKin(Map home, RaidersCamp c, RimWorld.Faction kin, string town, int now)
        {
            if (home == null || c?.people == null || kin == null || c.stage != CampStage.Hidden) return;
            if (Find.WorldObjects.AnyMapParentAt(TileOf(c))) return;
            Found(home, c, null, kin.Name, now, "Letter", town);
        }
    }
}
