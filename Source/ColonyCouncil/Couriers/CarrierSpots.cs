using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Where carriers go on the map (CarrierMath has the rules): couriers and porters, the post rider and a go-between under a
    /// white flag all do their business at the colony's mailbag (<c>Rimfolk_Mailbag</c>, the one nearest the gate:
    /// <see cref="Outbox.Bag"/>), else at its gate (<see cref="PlaySpots.GateCell"/>), else where traders leave goods. They
    /// come in by the way nearest to it, stand round it while their business is done (each in their own place, the beasts
    /// further out), and goods are set down on the cells beside it, not on it and not at a trade spot away from the colony.
    /// </summary>
    public static class CarrierSpots
    {
        /// <summary>The spot and what it is: the mailbag, the gate, the trade spot; invalid with none.</summary>
        public static IntVec3 Spot(Map map, out CarrierSpot kind)
        {
            kind = CarrierSpot.None;
            if (map == null) return IntVec3.Invalid;
            var bag = Outbox.Bag(map);
            var gate = bag == null ? PlaySpots.GateCell(map, null) : IntVec3.Invalid;
            var trade = bag == null && !gate.IsValid ? DropCellFinder.TradeDropSpot(map) : IntVec3.Invalid;
            kind = CarrierMath.SpotOf(bag != null, gate.IsValid, trade.IsValid && trade.InBounds(map));
            switch (kind)
            {
                case CarrierSpot.Mailbag: return bag.Position;
                case CarrierSpot.Gate: return gate;
                case CarrierSpot.TradeSpot: return trade;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A cell someone can stand on and wait: open ground, no building in the way, not deep water, not the spot itself.</summary>
        private static bool Standing(IntVec3 c, Map map, IntVec3 spot) =>
            c.InBounds(map) && c != spot && c.Standable(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && !c.Fogged(map);

        /// <summary>
        /// Where the <paramref name="index"/>th of a group stands at the spot (<see cref="CarrierMath.RingOffset"/>, the nearest
        /// free place they can reach), else the spot itself.
        /// </summary>
        public static IntVec3 StandCell(Map map, IntVec3 spot, int index, Pawn pawn)
        {
            if (map == null || !spot.IsValid) return spot;
            for (int k = 0; k < CarrierMath.RingCount; k++)
            {
                CarrierMath.RingOffset(Math.Max(0, index) + k, out int dx, out int dz);
                var c = new IntVec3(spot.x + dx, 0, spot.z + dz);
                if (!Standing(c, map, spot)) continue;
                if (pawn != null && !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                return c;
            }
            return spot;
        }

        /// <summary>
        /// The most direct way in from the map's edge to <paramref name="spot"/> (CarrierMath.BestEntry): walking out from the
        /// spot over the ground a visitor can walk (the game's own flood fill, a step at a time, so a wall or a river counts
        /// the way round it), the edge cell reached in the fewest steps that people can come in by, a road a little preferred
        /// (one within a fifth more steps). False: none (the caller falls back to the game's own random way in).
        /// </summary>
        public static bool EntryCell(Map map, IntVec3 spot, out IntVec3 entry)
        {
            entry = IntVec3.Invalid;
            if (map == null || !spot.IsValid || !spot.InBounds(map)) return false;
            try
            {
                var roads = new HashSet<IntVec3>(map.roadInfo?.roadEdgeTiles ?? new List<IntVec3>());
                var cells = new List<IntVec3>();
                var ways = new List<(float distance, bool road, bool ok)>();
                int first = -1;
                int maxX = map.Size.x - 1, maxZ = map.Size.z - 1;
                bool roofedOk = map.TileInfo.AllowRoofedEdgeWalkIn;
                map.floodFiller.FloodFill(spot, c => c.Walkable(map), (c, steps) =>
                {
                    // Past the nearest by more than a road's allowance: the nearest ones are all in.
                    if (first >= 0 && steps > first * CarrierMath.RoadFactor + 2) return true;
                    if (c.x != 0 && c.z != 0 && c.x != maxX && c.z != maxZ) return false;
                    if (!c.Standable(map) || c.Fogged(map) || (!roofedOk && map.roofGrid.Roofed(c))) return false;
                    if (first < 0) first = steps;
                    cells.Add(c);
                    ways.Add((steps, roads.Contains(c), true));
                    return cells.Count >= 400;
                });
                int best = CarrierMath.BestEntry(ways);
                if (best < 0) return false;
                entry = cells[best];
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: the way in", e);
                return false;
            }
        }

        /// <summary>
        /// Sets <paramref name="count"/> of <paramref name="thing"/> down out of <paramref name="from"/> on the cells beside the
        /// mailbag at <paramref name="bag"/>, the nearest free one first (a pile of stacks goes round it); never on the bag's own
        /// cell. Else near the bag as the game places things. True when set down.
        /// </summary>
        public static bool SetDownBeside(ThingOwner from, Thing thing, int count, Map map, IntVec3 bag, out Thing lying)
        {
            lying = null;
            if (from == null || thing == null || map == null || !bag.IsValid) return false;
            for (int k = 0; k < CarrierMath.RingCount; k++)
            {
                CarrierMath.RingOffset(k, out int dx, out int dz);
                var c = new IntVec3(bag.x + dx, 0, bag.z + dz);
                if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || (c.GetTerrain(map)?.IsWater ?? false)) continue;
                if (!map.reachability.CanReachColony(c)) continue;
                // An empty cell, or one whose stack it tops up: never a pile of different things on one cell.
                var there = c.GetFirstItem(map);
                if (there != null && !there.CanStackWith(thing)) continue;
                int before = thing.stackCount;
                if (from.TryDrop(thing, c, map, ThingPlaceMode.Direct, Math.Min(count, thing.stackCount), out lying)) return true;
                // Part of it topped up the stack there: the rest goes on round.
                count -= before - thing.stackCount;
                if (count <= 0 || thing.Destroyed || !from.Contains(thing)) return true;
            }
            CarrierMath.RingOffset(0, out int x0, out int z0);
            var near = new IntVec3(bag.x + x0, 0, bag.z + z0);
            return from.TryDrop(thing, near.InBounds(map) ? near : bag, map, ThingPlaceMode.Near, Math.Min(count, thing.stackCount), out lying);
        }
    }
}
