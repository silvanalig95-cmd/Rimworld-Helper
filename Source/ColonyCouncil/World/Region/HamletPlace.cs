using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Where a hamlet may stand (docs/design/stage1-region.md, item 6, by item 3's rules): 6 to 10 tiles from the home over
    // passable land, valid for a town with nothing on it, clear of every one of the colony's settlements by the game's own
    // goodwill reach, 3 tiles or more from any other town, not beside a town being built nor in another people's territory;
    // by the land's settlement weight, a river twice and a road half again. The same rules check a place the colony chooses.
    // Straight-line distances only, inside the flood fill too (a walking distance there would hang the game).
    public static partial class Hamlets
    {
        private static float Nearest(WorldGrid grid, List<PlanetTile> tiles, PlanetTile t)
        {
            float best = -1f;
            foreach (var x in tiles)
            {
                float d = grid.ApproxDistanceInTiles(x, t);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        /// <summary>The colony's settlements on a layer, and the other towns within <paramref name="near"/> tiles of <paramref name="root"/>.</summary>
        private static void Neighbours(PlanetTile root, float near, out List<PlanetTile> ours, out List<PlanetTile> others, out List<PlanetTile> sites)
        {
            var grid = Find.WorldGrid;
            var layer = root.Layer;
            ours = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && s.Faction != null && s.Faction.IsPlayer && s.Tile.Layer == layer).Select(s => s.Tile).ToList();
            others = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && (s.Faction == null || !s.Faction.IsPlayer) && s.Tile.Layer == layer
                                                              && grid.ApproxDistanceInTiles(root, s.Tile) <= near).Select(s => s.Tile).ToList();
            sites = WorldMechanics.SettlementsBeingBuilt().Where(b => b != null && b.tile.Valid && b.tile.Layer == layer).Select(b => b.tile).ToList();
        }

        /// <summary>
        /// Why a tile can't hold the hamlet, in words, or null when it can: the same rules as the search (the distance from
        /// the home, the colony's other settlements, other towns, a town being built, another people's territory, land that
        /// holds a town, a tile the game holds valid with nothing on it).
        /// </summary>
        public static string WhyNotHere(Map home, Faction people, PlanetTile t)
        {
            if (home == null || !t.Valid) return "CC_Hamlet_Place_Invalid".Translate().Resolve();
            if (t.Layer != home.Tile.Layer) return "CC_Hamlet_Place_Layer".Translate().Resolve();
            var grid = Find.WorldGrid;
            float d = grid.ApproxDistanceInTiles(home.Tile, t);
            if (d < HamletMath.MinTiles || d > HamletMath.MaxTiles)
                return "CC_Hamlet_Place_Far".Translate(((int)HamletMath.MinTiles).Named("MIN"), ((int)HamletMath.MaxTiles).Named("MAX")).Resolve();
            if (Find.World.Impassable(t) || Find.WorldObjects.AnyWorldObjectAt(t) || !TileFinder.IsValidTileForNewSettlement(t)) return "CC_Hamlet_Place_Invalid".Translate().Resolve();
            Neighbours(home.Tile, HamletMath.MaxTiles + HamletMath.TownGap + 1f, out var ours, out var others, out var sites);
            float w = RegionFortunes.BiomeWeight(t);
            if (w <= 0f) return "CC_Hamlet_Place_Land".Translate().Resolve();
            if (!RiseFallMath.ClearOfHomes(Nearest(grid, ours, t), RiseFall.GoodwillTiles)) return "CC_Hamlet_Place_Ours".Translate().Resolve();
            float town = Nearest(grid, others, t);
            if (town >= 0f && town < HamletMath.TownGap) return "CC_Hamlet_Place_Town".Translate().Resolve();
            if (sites.Any(s => grid.ApproxDistanceInTiles(s, t) <= 1.5f)) return "CC_Hamlet_Place_Town".Translate().Resolve();
            var owner = WorldMechanics.TerritoryOwner(t);
            if (owner != null && owner != people) return "CC_Hamlet_Place_Territory".Translate(owner.Name.Named("PEOPLE")).Resolve();
            return null;
        }

        /// <summary>
        /// Where the hamlet stands: a tile 6 to 10 tiles from the home by the rules above, chosen by the land's settlement
        /// weight, a river twice and a road half again. Invalid for none.
        /// </summary>
        public static PlanetTile FindTile(Map home, Faction people)
        {
            if (home == null || !home.Tile.Valid) return PlanetTile.Invalid;
            try
            {
                var root = home.Tile;
                var grid = Find.WorldGrid;
                int goodwillTiles = RiseFall.GoodwillTiles;
                Neighbours(root, HamletMath.MaxTiles + HamletMath.TownGap + 1f, out var ours, out var others, out var sites);
                var tiles = new List<PlanetTile>();
                var weights = new List<float>();
                Action<PlanetTile, int> each = (t, _) =>
                {
                    float d = grid.ApproxDistanceInTiles(root, t);
                    if (d < HamletMath.MinTiles || Find.WorldObjects.AnyWorldObjectAt(t)) return;
                    float w = RegionFortunes.BiomeWeight(t);
                    if (!HamletMath.PlaceFits(true, d, Nearest(grid, ours, t), goodwillTiles, Nearest(grid, others, t), w)) return;
                    if (sites.Any(s => grid.ApproxDistanceInTiles(s, t) <= 1.5f)) return;
                    if (!TileFinder.IsValidTileForNewSettlement(t)) return;
                    var owner = WorldMechanics.TerritoryOwner(t);
                    if (owner != null && owner != people) return;
                    bool road = false, river = false;
                    if (grid[t] is SurfaceTile st)
                    {
                        road = !st.Roads.NullOrEmpty();
                        river = !st.Rivers.NullOrEmpty();
                    }
                    tiles.Add(t);
                    weights.Add(HamletMath.PlaceWeight(road, river, w));
                };
                root.Layer.Filler.FloodFill(root, t => !Find.World.Impassable(t) && grid.ApproxDistanceInTiles(root, t) <= HamletMath.MaxTiles, each);
                int i = RiseFallMath.PickTile(weights, Rand.Value);
                return i < 0 ? PlanetTile.Invalid : tiles[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a place for one", e);
                return PlanetTile.Invalid;
            }
        }

        /// <summary>Why the hamlet's tile was chosen, as the ledger keeps a town's reason: the settlers fled a town given up, else a river, a road, or the land.</summary>
        internal static string WhyHere(HamletHome h, PlanetTile t)
        {
            if (h.way == HamletWay.Refugees) return "refugees";
            if (t.Valid && Find.WorldGrid[t] is SurfaceTile st)
            {
                if (!st.Rivers.NullOrEmpty()) return "river";
                if (!st.Roads.NullOrEmpty()) return "road";
            }
            return RegionMath.AnyLand;
        }

        /// <summary>The place as words from the home: "a day east".</summary>
        public static string Where(Map home, HamletHome h)
        {
            var t = TileOf(h);
            if (home == null || !t.Valid || t.Layer != home.Tile.Layer) return "CC_Region_Where_Near".Translate().Resolve();
            return RegionWords.Where(home, t, Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t));
        }

        /// <summary>
        /// The colony chooses another place on the world map (the ask letter's "choose another place"): the world's targeter,
        /// a place accepted only by the same rules, then the letter again with the new place.
        /// </summary>
        internal static void ChoosePlace(Map home, HamletHome h, Letter letter)
        {
            if (home == null || h == null) return;
            try
            {
                CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(TileOf(h).Valid ? TileOf(h) : home.Tile)));
                Find.WorldSelector.ClearSelection();
                Find.WorldTargeter.BeginTargeting(target =>
                    {
                        string why = WhyNotHere(home, h.people, target.Tile);
                        if (why != null)
                        {
                            // direct: the answer to the player's own click on the world map.
                            Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                            return false;
                        }
                        if (h.stage != HamletStage.Asked) return true;
                        h.tile = Refs.Of(target.Tile);
                        HamletAsk.Retell(home, h, letter);
                        return true;
                    }, true, null, true, null,
                    target => target.Tile.Valid ? WhyNotHere(home, h.people, target.Tile) ?? "CC_Hamlet_Place_Fits".Translate(Where(home, h, target.Tile).Named("WHERE")).Resolve() : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: another place chosen", e);
            }
        }

        private static string Where(Map home, HamletHome h, PlanetTile t) =>
            home == null || !t.Valid || t.Layer != home.Tile.Layer ? "CC_Region_Where_Near".Translate().Resolve() : RegionWords.Where(home, t, Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t));
    }
}
