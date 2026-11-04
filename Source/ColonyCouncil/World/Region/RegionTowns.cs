using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One home's part of the region as it stands now: its towns in detail, nearest first, and how remote it is.</summary>
    public sealed class HomeReach
    {
        public Map home;
        public int key;
        /// <summary>Its towns in detail (RegionMath.InReach), nearest first, with their distances in tiles.</summary>
        public readonly List<Settlement> towns = new List<Settlement>();
        public readonly List<float> tiles = new List<float>();
        /// <summary>Tiles to the nearest town of a humanlike people at peace with the colony (-1 for none), how many such stand within RegionMath.RemoteFar, and the remoteness from them.</summary>
        public float nearestPeaceful = -1f;
        public int peacefulWithin;
        public float remoteness = 1f;
    }

    /// <summary>
    /// The towns kept in detail (docs/design/stage1-region.md, "Shared rules"): for each home, the nearest
    /// <see cref="RegionMath.MaxTownsPerHome"/> towns of humanlike peoples within the region map's reach
    /// (<c>regionMapRadius</c>), and how remote the home is. Worked out with straight-line distances, at most once a
    /// 250-tick stretch, and only again when the world's towns, the homes or the reach change, or when the region's day
    /// asks; nothing is saved (the ledger keeps the records). Beyond the reach Rimfolk keeps nothing and changes nothing.
    /// </summary>
    public static class RegionTowns
    {
        private static Game game;
        private static int lookedAt = Fresh.Never;
        private static int builtCount = -1, builtNewest = -1, builtReach = -1, builtHomes;
        private static List<HomeReach> reach = new List<HomeReach>();
        private static HashSet<int> detail = new HashSet<int>();
        private static readonly Dictionary<int, float> nearestTiles = new Dictionary<int, float>();

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The region map's reach in tiles (the setting, 20 to 60).</summary>
        public static int ReachTiles => ColonyCouncilMod.Settings?.regionMapRadius ?? (int)RegionMath.DefaultReach;

        /// <summary>A town of a people Rimfolk keeps in detail: standing, of a humanlike people that isn't the player's or hidden.</summary>
        public static bool Fits(Settlement s) =>
            s != null && !s.Destroyed && s.Faction != null && !s.Faction.IsPlayer && !s.Faction.Hidden && s.Faction.def != null && s.Faction.def.humanlikeFaction;

        /// <summary>Each home's part of the region (a shared list: read it, don't change it).</summary>
        public static IReadOnlyList<HomeReach> Reach
        {
            get
            {
                Refresh(false);
                return reach;
            }
        }

        /// <summary>The ids of the towns kept in detail for any home (a shared set: read it, don't change it).</summary>
        public static HashSet<int> InDetail()
        {
            Refresh(false);
            return detail;
        }

        public static bool IsInDetail(int town) => town >= 0 && InDetail().Contains(town);

        /// <summary>A home's part, or null for a map that isn't a home.</summary>
        public static HomeReach Of(Map home)
        {
            if (home == null) return null;
            foreach (var r in Reach)
                if (r.home == home)
                    return r;
            return null;
        }

        /// <summary>How remote a home is, 0 to 1 (RegionMath.Remoteness); 0 for a map that isn't a home.</summary>
        public static float Remoteness(Map home) => Of(home)?.remoteness ?? 0f;

        /// <summary>Tiles from the nearest home on the town's layer to a town in detail, or -1.</summary>
        public static float TilesToNearestHome(int town)
        {
            Refresh(false);
            return nearestTiles.TryGetValue(town, out float t) ? t : -1f;
        }

        /// <summary>The home nearest a tile on its layer, with its distance; null when no home is on that layer.</summary>
        public static Map NearestHome(PlanetTile tile, out float tiles)
        {
            tiles = -1f;
            Map best = null;
            if (!tile.Valid || Find.WorldGrid == null) return null;
            foreach (var m in Homes.All)
            {
                if (m == null || !m.Tile.Valid || m.Tile.Layer != tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(m.Tile, tile);
                if (best != null && d >= tiles) continue;
                best = m;
                tiles = d;
            }
            return best;
        }

        /// <summary>A tile within the region map's reach of a home.</summary>
        public static bool InReach(PlanetTile tile)
        {
            NearestHome(tile, out float tiles);
            return tiles >= 0f && tiles <= ReachTiles;
        }

        /// <summary>Worked out again on the next ask (the region's day asks every day: peace and war change remoteness).</summary>
        public static void Invalidate() => lookedAt = Fresh.Never;

        /// <summary>
        /// The reach as it stands: worked out again when the world's towns (their count or the newest id), the homes or
        /// the reach changed since, or when <paramref name="force"/>; looked at no more than once a 250-tick stretch.
        /// </summary>
        public static void Refresh(bool force)
        {
            if (Current.Game == null || Find.WorldObjects == null)
            {
                reach = new List<HomeReach>();
                detail = new HashSet<int>();
                return;
            }
            if (game != Current.Game)
            {
                game = Current.Game;
                lookedAt = Fresh.Never;
                builtCount = -1;
            }
            int now = Now;
            if (!force && Fresh.Holds(lookedAt, now, Sched.RareTicks)) return;
            lookedAt = now;
            var all = Find.WorldObjects.Settlements;
            int newest = -1;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].ID > newest)
                    newest = all[i].ID;
            var homes = Homes.All;
            int homesKey = 17;
            foreach (var h in homes) homesKey = homesKey * 31 + Homes.KeyOf(h);
            int r = ReachTiles;
            if (!force && builtCount == all.Count && builtNewest == newest && builtReach == r && builtHomes == homesKey) return;
            builtCount = all.Count;
            builtNewest = newest;
            builtReach = r;
            builtHomes = homesKey;
            try
            {
                Build(all, homes, r);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: the towns in reach", e);
            }
        }

        private static void Build(List<Settlement> all, IReadOnlyList<Map> homes, int reachTiles)
        {
            var list = new List<HomeReach>();
            var perHome = new List<IList<int>>();
            nearestTiles.Clear();
            var player = Faction.OfPlayerSilentFail;
            foreach (var home in homes)
            {
                if (home == null || !home.Tile.Valid) continue;
                var hr = new HomeReach { home = home, key = Homes.KeyOf(home) };
                var distances = new List<float>(all.Count);
                var fit = new List<bool>(all.Count);
                float nearest = float.MaxValue;
                int within = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    var s = all[i];
                    bool ok = Fits(s) && s.Tile.Valid && s.Tile.Layer == home.Tile.Layer;
                    float d = ok ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile) : float.MaxValue;
                    distances.Add(d);
                    fit.Add(ok);
                    if (!ok || player == null || s.Faction.HostileTo(player)) continue;
                    if (d < nearest) nearest = d;
                    if (d <= RegionMath.RemoteFar) within++;
                }
                var idx = RegionMath.InReach(distances, fit, reachTiles);
                var ids = new List<int>(idx.Count);
                foreach (int i in idx)
                {
                    hr.towns.Add(all[i]);
                    hr.tiles.Add(distances[i]);
                    ids.Add(all[i].ID);
                    if (!nearestTiles.TryGetValue(all[i].ID, out float was) || distances[i] < was) nearestTiles[all[i].ID] = distances[i];
                }
                hr.nearestPeaceful = nearest == float.MaxValue ? -1f : nearest;
                hr.peacefulWithin = within;
                hr.remoteness = RegionMath.Remoteness(hr.nearestPeaceful, within);
                list.Add(hr);
                perHome.Add(ids);
            }
            reach = list;
            detail = new HashSet<int>(RegionMath.Union(perHome));
        }
    }
}
