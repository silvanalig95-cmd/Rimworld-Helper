using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampWinterDefOf
    {
        public static JobDef Rimfolk_CampIceFish;

        static CampWinterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampWinterDefOf));
        }
    }

    /// <summary>
    /// What the winter camps need from the land and the sky (the player's request of 10 November 2026: "i'd like a handful
    /// of camp events that can also come in the coldest winter"): snow falling, for a caravan come in out of it; frozen
    /// water with fish under it, for ice fishers (Odyssey's thin ice, the game's own freezing of shallow water below -7 °C);
    /// and the hole they cut in the ice and keep open while they fish, which freezes over again by the game's own rule
    /// once they're done.
    /// </summary>
    public static partial class CampLand
    {
        /// <summary>The ice fishers' work (CampKindDef.work) and what their land must have (CampKindDef.needs).</summary>
        public const string IceWork = "icefish", IceNeed = "ice";

        /// <summary>Odyssey's thin ice over frozen water; null without it.</summary>
        public static TerrainDef Ice => ModsConfig.OdysseyActive ? TerrainDefOf.ThinIce : null;

        /// <summary>Snow is falling now.</summary>
        public static bool SnowNow(Map map) => (map?.weatherManager?.curWeather?.snowRate ?? 0f) > 0.01f;

        /// <summary>Hard snow, a blizzard (the game's hard snow and anything heavier).</summary>
        public static bool Blizzard(Map map) => (map?.weatherManager?.curWeather?.snowRate ?? 0f) >= 1f;

        /// <summary>A cell of ice over water that holds fish (the water's own fish stock, under the ice as it is in the open).</summary>
        public static bool UnderIce(Map map, IntVec3 c)
        {
            var ice = Ice;
            if (ice == null || map?.waterBodyTracker == null || !c.InBounds(map)) return false;
            return c.GetTerrain(map) == ice && map.waterBodyTracker.AnyFishPopulationAt(c);
        }

        /// <summary>Frozen water with fish under it, well clear of the colony, sampled across the map: where ice fishers could pitch.</summary>
        public static List<IntVec3> IceTargets(Map map)
        {
            var list = new List<IntVec3>();
            if (map == null || Ice == null) return list;
            var pts = CampSite.ColonyPoints(map);
            int minSq = 30 * 30;
            for (int x = 4; x < map.Size.x - 4; x += 4)
                for (int z = 4; z < map.Size.z - 4; z += 4)
                {
                    var c = new IntVec3(x, 0, z);
                    if (c.Fogged(map) || !UnderIce(map, c)) continue;
                    if (pts.Count > 0 && MinDistSquared(c, pts) < minSq) continue;
                    list.Add(c);
                }
            return list.OrderByDescending(c => FishAt(map, c)).ThenBy(c => Rand.Value).ToList();
        }

        private static Map iceMap;
        private static int iceHour = -1;
        private static bool iceOn;

        /// <summary>Some water on the map is frozen over (for the camps' words, [ice]), read once an hour.</summary>
        public static bool IceOn(Map map)
        {
            var ice = Ice;
            if (map == null || ice == null) return false;
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (map == iceMap && hour == iceHour) return iceOn;
            iceMap = map;
            iceHour = hour;
            iceOn = false;
            try
            {
                for (int x = 2; x < map.Size.x - 2 && !iceOn; x += 3)
                    for (int z = 2; z < map.Size.z - 2; z += 3)
                        if (new IntVec3(x, 0, z).GetTerrain(map) == ice)
                        {
                            iceOn = true;
                            break;
                        }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp ice on the map", e);
            }
            return iceOn;
        }

        /// <summary>
        /// While a camper works (JobDriver_CampTask, every second): an ice fisher keeps their hole open, cutting the ice
        /// away again where it has frozen over.
        /// </summary>
        public static void WorkTick(Pawn p, Job job)
        {
            if (job?.def != CampWinterDefOf.Rimfolk_CampIceFish || p?.Map == null || !job.targetB.IsValid) return;
            try
            {
                var map = p.Map;
                var hole = job.targetB.Cell;
                if (Ice != null && hole.InBounds(map) && hole.GetTerrain(map) == Ice && hole.GetFirstPawn(map) == null) map.terrainGrid.RemoveTempTerrain(hole);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp ice hole", e);
            }
        }
    }

    // The ice fishers' day: a hole cut in the ice near their camp, fished through by day.
    public partial class Camp
    {
        [Unsaved] private List<(IntVec3 stand, IntVec3 hole)> iceSpots;
        [Unsaved] private int iceSpotsTick = -999999;

        /// <summary>
        /// Where to fish through the ice within reach of the camp: a cell of ice to kneel on, and beside it the hole (ice
        /// over fishing water, or a hole cut earlier that hasn't frozen over yet, first), clear of the colony. Worked out
        /// every two hours.
        /// </summary>
        private List<(IntVec3 stand, IntVec3 hole)> IceSpots()
        {
            if (iceSpots != null && Now - iceSpotsTick < GenDate.TicksPerHour * 2) return iceSpots;
            var open = new List<(IntVec3, IntVec3)>();
            var list = new List<(IntVec3, IntVec3)>();
            var ice = CampLand.Ice;
            if (ice != null && map?.waterBodyTracker != null)
            {
                var seen = new HashSet<IntVec3>();
                foreach (var h in GenRadial.RadialCellsAround(center, CampLand.FishRadius, true))
                {
                    if (!h.InBounds(map) || seen.Contains(h) || map.zoneManager.ZoneAt(h) != null || !ClearOfColony(h)) continue;
                    var t = h.GetTerrain(map);
                    bool cut = t != null && t.IsWater && map.waterBodyTracker.AnyFishPopulationAt(h) && GenAdj.CardinalDirections.All(d => !(h + d).InBounds(map) || (h + d).GetTerrain(map) == ice);
                    if (!cut && !CampLand.UnderIce(map, h)) continue;
                    foreach (var s in GenAdj.CardinalDirections.Select(d => h + d))
                    {
                        if (!s.InBounds(map) || seen.Contains(s) || s.GetTerrain(map) != ice || !s.Standable(map) || s.GetEdifice(map) != null || !ClearOfColony(s)) continue;
                        seen.Add(s);
                        seen.Add(h);
                        (cut ? open : list).Add((s, h));
                        break;
                    }
                    if (open.Count + list.Count >= 60) break;
                }
            }
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some, false);
            iceSpots = open.Concat(list).Where(x => map.reachability.CanReach(center, x.Item1, PathEndMode.OnCell, tp)).Take(20).ToList();
            iceSpotsTick = Now;
            return iceSpots;
        }

        /// <summary>A trip out onto the ice to fish through a hole (their day's own work); open water if there is no ice left.</summary>
        private Job IceFishJob(Pawn p)
        {
            if (workToday >= CampLandMath.FishTrips(Adults) || !Rand.Chance(0.6f) || Now < landNextSearch) return null;
            var spots = IceSpots().Where(x => x.stand.GetFirstPawn(map) == null && x.hole.GetFirstPawn(map) == null
                                              && !Here.Any(o => o != p && o.CurJob?.def == CampWinterDefOf.Rimfolk_CampIceFish && (o.CurJob.targetA.Cell == x.stand || o.CurJob.targetB.Cell == x.hole))
                                              && p.CanReach(x.stand, PathEndMode.OnCell, Danger.Some)).ToList();
            if (spots.Count == 0) return FishJob(p);
            // The open holes first, so the ice isn't cut up all over.
            var spot = spots.Take(Math.Min(spots.Count, 4)).RandomElement();
            SayMoment(p, "ice", 0.3f);
            return JobMaker.MakeJob(CampWinterDefOf.Rimfolk_CampIceFish, spot.stand, spot.hole);
        }

        /// <summary>A kind that asks for the colony's roof on a stormy evening (an ask "shelter" of their own): the watchers, a caravan out of the snow.</summary>
        public bool Shelters => kind?.asks != null && kind.asks.Any(a => a != null && a.key == "shelter" && a.theyAsk);
    }
}
