using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the colony gathers out of doors (YardMath.cs weighs each cell): the open-air cells of the home ground near the
    /// middle of the colony, weighed by what the colony made of them, best first. Read once an hour per map and kept, so
    /// a gathering asking for a place costs nothing; each asker takes the best one that suits them (PlaySpots.Ok). A fire
    /// is only laid where nothing that burns stands close.
    /// </summary>
    public static class PlayYard
    {
        private const float Reach = 28f;

        private class Ranked
        {
            public int tick = -999999;
            public List<IntVec3> best = new List<IntVec3>();
        }

        private static readonly Dictionary<int, Ranked> cache = new Dictionary<int, Ranked>();

        /// <summary>The best open-air place for this pawn to gather at, or an invalid cell.</summary>
        public static IntVec3 Best(Map map, Pawn pawn, bool fire = false)
        {
            if (map == null) return IntVec3.Invalid;
            foreach (var c in Ranking(map))
            {
                if (fire && !FireSafe(c, map)) continue;
                if (PlaySpots.Ok(c, map, pawn)) return c;
            }
            return IntVec3.Invalid;
        }

        private static List<IntVec3> Ranking(Map map)
        {
            if (!cache.TryGetValue(map.uniqueID, out var r)) cache[map.uniqueID] = r = new Ranked();
            int now = Find.TickManager.TicksGame;
            if (now - r.tick < GenDate.TicksPerHour && now >= r.tick) return r.best;
            r.tick = now;
            r.best.Clear();
            try
            {
                var center = TemplatePlanner.ColonyCenter(map);
                var home = map.areaManager?.Home;
                var scored = new List<(IntVec3 c, float s)>();
                foreach (var c in GenRadial.RadialCellsAround(center, Reach, true))
                {
                    if (!Open(c, map) || home != null && home.TrueCount > 0 && !home[c]) continue;
                    var room = c.GetRoom(map);
                    if (room != null && !room.PsychologicallyOutdoors) continue;
                    float s = YardMath.Score(Read(c, map, center));
                    if (s > YardMath.Unfit) scored.Add((c, s));
                }
                r.best.AddRange(scored.OrderByDescending(x => x.s).Take(40).Select(x => x.c));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering yard", e);
            }
            return r.best;
        }

        private static bool Open(IntVec3 c, Map map) =>
            c.InBounds(map) && !c.Roofed(map) && c.Standable(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && !c.Fogged(map);

        private static YardMath.Cell Read(IntVec3 c, Map map, IntVec3 center)
        {
            var cell = new YardMath.Cell { distance = c.DistanceTo(center) };
            foreach (var x in GenRadial.RadialCellsAround(c, 2.9f, true))
                if (Open(x, map)) cell.room++;
            if (cell.room < YardMath.MinRoom) return cell;
            cell.beauty = BeautyUtility.AverageBeautyPerceptible(c, map);
            cell.floor = map.terrainGrid.CanRemoveTopLayerAt(c);
            var zone = map.zoneManager.ZoneAt(c);
            cell.stockpile = zone is Zone_Stockpile;
            cell.field = zone is Zone_Growing;
            foreach (var x in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                if (x.InBounds(map) && x.GetDoor(map) != null) cell.byDoor = true;
            foreach (var t in GenRadial.RadialDistinctThingsAround(c, map, 6f, true))
            {
                if (t?.def?.building == null || t.Faction != Faction.OfPlayer) continue;
                bool near = t.Position.DistanceTo(c) <= 4.9f;
                if (map.gatherSpotLister.activeSpots.Any(g => g.parent == t) || t.def == ThingDefOf.Campfire) cell.gatherNear = true;
                else if (near && !t.Position.Roofed(map) && (t.def.building.isSittable || t.def.building.joyKind != null || t.def.surfaceType == SurfaceType.Eat)) cell.seats++;
                else if (near && Works(t)) cell.works++;
            }
            return cell;
        }

        /// <summary>Machinery and defenses nobody gathers beside: power, batteries, climate units, turrets, fuel and the like.</summary>
        private static bool Works(Thing t)
        {
            var tw = t as ThingWithComps;
            if (tw != null && (tw.GetComp<CompPowerPlant>() != null || tw.GetComp<CompPowerBattery>() != null || tw.GetComp<CompTempControl>() != null)) return true;
            if (t.def.building.IsTurret || t.def.building.isTrap) return true;
            string cat = t.def.designationCategory?.defName;
            return cat == "Power" || cat == "Security" || cat == "Temperature";
        }

        /// <summary>Nothing that burns stands within a few steps (a fire laid here won't take the colony with it).</summary>
        private static bool FireSafe(IntVec3 c, Map map)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, 2.9f, true))
            {
                if (!x.InBounds(map)) continue;
                var b = x.GetEdifice(map);
                if (b != null && b.FlammableNow) return false;
            }
            return true;
        }
    }
}
