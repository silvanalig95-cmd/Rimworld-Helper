using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the colony gathers out of doors (YardMath.cs weighs each cell): the open-air cells of the home ground near the
    /// middle of the colony, weighed by what the colony made of them, read once an hour per map and kept. The good cells
    /// make a few distinct places (a terrace, the yard by the kitchen, the ground by the river); each gathering chooses
    /// one of them by how good it is, holding back the places used lately, so the colony doesn't always meet on the same
    /// patch while it has others. A choice holds for a few hours, so a gathering, its walk and its fire keep their place.
    /// A fire is only laid where nothing that burns stands close.
    /// </summary>
    public static class PlayYard
    {
        private const float Reach = 28f;

        private class Ranked
        {
            public int tick = -999999;
            /// <summary>The good cells, best first, with their scores.</summary>
            public List<IntVec3> best = new List<IntVec3>();
            public List<float> scores = new List<float>();
            /// <summary>The distinct places: indexes into <see cref="best"/> of the cells that start one, best first.</summary>
            public List<int> places = new List<int>();
        }

        /// <summary>What a map's gatherings chose lately: the place chosen now and until when it holds, the places before.</summary>
        private class Chosen
        {
            public IntVec3 cell = IntVec3.Invalid;
            public bool fire;
            public int until = -1;
            public readonly List<IntVec3> recent = new List<IntVec3>();
        }

        private static readonly Dictionary<int, Ranked> cache = new Dictionary<int, Ranked>();
        private static readonly Dictionary<int, Chosen> chosen = new Dictionary<int, Chosen>();

        /// <summary>
        /// An open-air place for this pawn to gather at, or an invalid cell: the place chosen for the gathering under way
        /// while it holds and suits them, else a new choice among the colony's good places (YardMath.Weight).
        /// </summary>
        public static IntVec3 Best(Map map, Pawn pawn, bool fire = false)
        {
            if (map == null) return IntVec3.Invalid;
            var r = Ranking(map);
            if (r.best.Count == 0) return IntVec3.Invalid;
            if (!chosen.TryGetValue(map.uniqueID, out var ch)) chosen[map.uniqueID] = ch = new Chosen();
            int now = Find.TickManager.TicksGame;
            if (ch.cell.IsValid && now < ch.until && now >= ch.until - YardMath.HoldHours * GenDate.TicksPerHour && (!fire || ch.fire || FireSafe(ch.cell, map))
                && PlaySpots.Ok(ch.cell, map, pawn))
                return ch.cell;

            // The places that suit this pawn now, each by its best cell that does.
            var cells = new List<IntVec3>();
            var weights = new List<float>();
            float top = r.scores[r.places[0]];
            for (int k = 0; k < r.places.Count; k++)
            {
                var c = Usable(r, r.places[k], map, pawn, fire);
                if (!c.IsValid) continue;
                cells.Add(c);
                weights.Add(YardMath.Weight(r.scores[r.places[k]], top, UsedAgo(ch, r.best[r.places[k]])));
            }
            int pick = YardMath.Pick(weights, Rand.Value);
            if (pick < 0)
            {
                // No place weighed suits them: the best single cell that does, as before.
                foreach (var c in r.best)
                    if ((!fire || FireSafe(c, map)) && PlaySpots.Ok(c, map, pawn))
                        return c;
                return IntVec3.Invalid;
            }
            var cell = cells[pick];
            ch.cell = cell;
            ch.fire = fire;
            ch.until = now + YardMath.HoldHours * GenDate.TicksPerHour;
            ch.recent.Insert(0, cell);
            if (ch.recent.Count > YardMath.Remembered) ch.recent.RemoveRange(YardMath.Remembered, ch.recent.Count - YardMath.Remembered);
            return cell;
        }

        /// <summary>The place's starting cell, or the next good cell of the same place, that suits the pawn (and a fire).</summary>
        private static IntVec3 Usable(Ranked r, int start, Map map, Pawn pawn, bool fire)
        {
            var origin = r.best[start];
            for (int i = start; i < r.best.Count; i++)
            {
                var c = r.best[i];
                if (i != start && c.DistanceToSquared(origin) >= YardMath.Apart * YardMath.Apart / 4f) continue;
                if (fire && !FireSafe(c, map)) continue;
                if (PlaySpots.Ok(c, map, pawn)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>How many choices ago a place was used (0 the last), or -1 when not lately.</summary>
        private static int UsedAgo(Chosen ch, IntVec3 place)
        {
            for (int i = 0; i < ch.recent.Count; i++)
                if (ch.recent[i].DistanceToSquared(place) < YardMath.Apart * YardMath.Apart)
                    return i;
            return -1;
        }

        private static Ranked Ranking(Map map)
        {
            if (!cache.TryGetValue(map.uniqueID, out var r)) cache[map.uniqueID] = r = new Ranked();
            int now = Find.TickManager.TicksGame;
            if (now - r.tick < GenDate.TicksPerHour && now >= r.tick) return r;
            r.tick = now;
            r.best.Clear();
            r.scores.Clear();
            r.places.Clear();
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
                // Enough of the best cells to find the colony's other good places, not just its one best patch.
                foreach (var x in scored.OrderByDescending(x => x.s).Take(MaxKept))
                {
                    r.best.Add(x.c);
                    r.scores.Add(x.s);
                }
                r.places.AddRange(YardMath.Places(r.best.Select((c, i) => (c.x, c.z, r.scores[i])).ToList()));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering yard", e);
                r.best.Clear();
                r.scores.Clear();
                r.places.Clear();
            }
            return r;
        }

        /// <summary>How many of the best cells are kept to find the places among (an hour's reading, not per frame).</summary>
        private const int MaxKept = 400;

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
