using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A walk together that goes round from stop to stop rather than out to one place and back
    /// (<see cref="DilemmaEffect_Procession.route"/>): the bounds walked by day, from one turn of the colony's edge to
    /// the next (the night watch's look-outs, PlayVigils.Route), a stone set at each turn the first time and found again
    /// after; or the round of the colony's beehives, each one visited in turn to be told the news. The walk keeps its
    /// stops in <see cref="PlayOut.plots"/>, the one it's at in <see cref="PlayOut.amount"/>, the stones it set in
    /// <see cref="PlayOut.tally"/> and those it found in <see cref="PlayOut.marked"/>; it stays a short while at each
    /// stop and walks back from the last.
    /// </summary>
    public static class PlayRoutes
    {
        /// <summary>The bounds walked, the bounds walked setting stones, the round of the hives.</summary>
        public const string Bounds = "bounds", Stones = "bounds-stones", Hives = "hives";

        public static bool Known(string mode) => mode == Bounds || mode == Stones || mode == Hives;

        private static bool OfBounds(PlayOut p) => p.mode == Bounds || p.mode == Stones;

        /// <summary>A walk that goes round its stops.</summary>
        public static bool IsRoute(PlayOut p) => p != null && p.kind == PlayKind.Procession && Known(p.mode) && p.plots.Count > 0;

        /// <summary>How long a route walk stays at each stop: a few minutes, to look about, set a stone or speak to the bees.</summary>
        public static int StopTicks => PlayOutMath.TicksPerHour / 5;

        /// <summary>How long the walk stays where it has got to: at a stop of a route, or at a walk's far end.</summary>
        public static int Stay(PlayOut p) => IsRoute(p) ? StopTicks : PlayOutMath.WalkStay(p.durationTicks);

        /// <summary>The colony's beehives (Alpha Bees' beehouses, or any building called a beehive or an apiary).</summary>
        public static List<Thing> HivesOf(Map map)
        {
            if (map == null) return new List<Thing>();
            return map.listerBuildings.allBuildingsColonist.Where(t => t.Spawned && !(t is Frame) && IsHive(t.def) && !t.Position.Fogged(map)).Cast<Thing>().ToList();
        }

        public static bool IsHive(ThingDef d)
        {
            if (d == null) return false;
            string n = (d.defName + " " + d.label).ToLowerInvariant();
            return n.Contains("beehouse") || n.Contains("beehive") || n.Contains("apiary") || n.Contains("bee house");
        }

        /// <summary>The stops, in the order the walk takes them (nearest next, from where it gathers). Empty when there are none to reach.</summary>
        public static List<IntVec3> Stops(PlayOut p, Pawn lead)
        {
            var stops = new List<IntVec3>();
            var map = p?.map;
            if (map == null || lead == null) return stops;
            if (OfBounds(p)) return PlayVigils.Route(map, lead, 0, 1);
            if (p.mode != Hives) return stops;
            var at = lead.Position;
            var hives = HivesOf(map);
            while (hives.Count > 0 && stops.Count < 8)
            {
                var h = hives.OrderBy(x => x.Position.DistanceToSquared(at)).First();
                hives.Remove(h);
                var c = PlaySpots.FreeCellNear(h.InteractionCell.IsValid && h.def.hasInteractionCell ? h.InteractionCell : h.Position, map, lead);
                if (!c.IsValid || stops.Any(s => s.InHorDistOf(c, 2f))) continue;
                stops.Add(c);
                at = c;
            }
            return stops;
        }

        /// <summary>Starts the round at its first stop (StartWalk): false, with why, when there's nowhere to go.</summary>
        public static bool Start(PlayOut p, Pawn lead, out string where)
        {
            where = null;
            p.plots = Stops(p, lead);
            if (p.plots.Count == 0)
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return false;
            }
            p.amount = 0;
            p.tally = 0;
            p.marked = 0;
            p.cell = p.plots[0];
            p.place = p.mode == Hives ? HiveAt(p.map, p.cell) : null;
            where = (p.mode == Hives ? "CC_Play_Where_Hives" : "CC_Play_Where_Bounds").Translate();
            return true;
        }

        private static Thing HiveAt(Map map, IntVec3 c) => HivesOf(map).OrderBy(h => h.Position.DistanceToSquared(c)).FirstOrDefault();

        /// <summary>Got to a stop: a stone set there (or found where one was set before), on a bounds walk that sets them.</summary>
        public static void AtStop(PlayOut p)
        {
            if (!IsRoute(p) || p.mode != Stones) return;
            try
            {
                var map = p.map;
                var stone = PlayMarkers.StoneFor(map);
                if (stone == null) return;
                var found = GenRadial.RadialDistinctThingsAround(p.cell, map, 3.5f, true).FirstOrDefault(t => t.def == stone && t.IsForbidden(Faction.OfPlayer));
                if (found != null)
                {
                    p.marked++;
                    return;
                }
                var set = PlayMarkers.SetStone(map, p.cell, stone);
                if (set == null) return;
                // Left where it stands: the colony's own hauling doesn't carry a boundary stone off to the stockpile.
                set.SetForbidden(true, false);
                p.tally++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bounds stone", e);
            }
            finally
            {
                p.note = "CC_Play_Note_Bounds".Translate(p.tally, p.marked).Resolve();
            }
        }

        /// <summary>
        /// Leaving a stop: on to the next one if there is one (true, with the walk back out to it), else back home (false).
        /// </summary>
        public static bool Next(PlayOut p)
        {
            if (!IsRoute(p) || p.amount + 1 >= p.plots.Count) return false;
            p.amount++;
            p.cell = p.plots[p.amount];
            if (p.mode == Hives) p.place = HiveAt(p.map, p.cell);
            return true;
        }
    }
}
