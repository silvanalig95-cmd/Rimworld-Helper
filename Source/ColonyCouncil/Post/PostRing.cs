using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The diplomatic range on the region map (UI/RegionMapView.cs), and the hook for any other screen that draws it (the
    /// diplomacy screen): a soft ring round a town far off (amber: by relay) or out of reach (grey), none within range or by
    /// radio; and the reach line in a town's tooltip. Read from Post/PostReach.cs's cache, a couple of new towns a frame.
    /// </summary>
    public static class PostRing
    {
        private static readonly Color FarColor = UiKit.WithAlpha(UiKit.Warn, 0.45f);
        private static readonly Color CutColor = UiKit.WithAlpha(UiKit.Faint, 0.45f);

        /// <summary>The home map at a region map's home tile (or the seat).</summary>
        public static Map HomeAt(PlanetTile tile) => Find.Maps.FirstOrDefault(m => m != null && m.IsPlayerHome && m.Tile == tile) ?? Homes.Seat;

        /// <summary>Where a town lies for the colony's letters from the home at a tile, or null (not a town of another people, the post off).</summary>
        public static ReachInfo Reach(PlanetTile home, Settlement town)
        {
            if (!PostBook.On || town?.Faction == null || town.Faction.IsPlayer) return null;
            var map = HomeAt(home);
            return map == null ? null : PostReach.Of(map, town, true);
        }

        /// <summary>A soft ring for a town far off or out of reach (its colour), false within range or by radio.</summary>
        public static bool Ring(PlanetTile home, Settlement town, out Color color)
        {
            color = Color.clear;
            var info = Reach(home, town);
            if (info == null || info.call || Ages.Ours >= WorldAge.Spacer) return false;
            if (info.band == ReachBand.Far) color = FarColor;
            else if (info.band == ReachBand.Cut) color = CutColor;
            else return false;
            return true;
        }

        /// <summary>The reach line for a town's tooltip on the map.</summary>
        public static List<string> Tip(PlanetTile home, Settlement town)
        {
            if (!PostBook.On || town?.Faction == null || town.Faction.IsPlayer) return new List<string>();
            var map = HomeAt(home);
            var lines = map != null ? PostWords.ReachLines(map, town) : new List<string>();
            return lines.Count == 0 ? lines : new List<string> { lines[0] };
        }
    }
}
