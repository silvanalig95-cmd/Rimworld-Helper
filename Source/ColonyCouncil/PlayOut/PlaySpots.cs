using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Where answers play out: the dining table, the rec room, a fire (laid for a bonfire if the colony has none and can
    /// spare the wood), the open air, a grave, the camp; and the meals a feast is served from.
    /// </summary>
    public static class PlaySpots
    {
        // ---------------- A gathering's spot ----------------

        /// <summary>
        /// The first of the plan's spots that works now, with the words for it ("around the dining table"). A bonfire
        /// with no fire to gather at may lay one (<paramref name="laid"/>, the wood it took in <paramref name="wood"/>).
        /// </summary>
        public static bool TryFind(PlayOut p, Pawn organizer, out IntVec3 spot, out string where, out Thing laid, out int wood)
        {
            spot = IntVec3.Invalid;
            where = null;
            laid = null;
            wood = 0;
            var map = p.map;
            if (map == null || organizer == null) return false;
            var list = p.spots.NullOrEmpty() ? new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors } : p.spots;
            foreach (var s in list)
            {
                switch (s)
                {
                    case PlaySpot.Dining:
                        foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).OrderByDescending(r => r.CellCount))
                        {
                            var table = room.ContainedAndAdjacentThings.FirstOrDefault(t => t.def.surfaceType == SurfaceType.Eat && t.Faction == Faction.OfPlayer);
                            if (table == null) continue;
                            var c = FreeCellNear(table.Position, map, organizer);
                            if (!c.IsValid) continue;
                            spot = c;
                            where = "CC_Venue_Dining".Translate();
                            return true;
                        }
                        break;
                    case PlaySpot.Rec:
                        foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom).OrderByDescending(r => r.CellCount))
                        {
                            var middle = room.ExtentsClose.CenterCell;
                            var c = First(room.Cells.Where(x => x.GetEdifice(map) == null).OrderBy(x => x.DistanceToSquared(middle)).Take(40), x => Ok(x, map, organizer));
                            if (!c.IsValid) continue;
                            spot = c;
                            where = "CC_Venue_RecRoom".Translate();
                            return true;
                        }
                        break;
                    case PlaySpot.Fire:
                    {
                        var fire = ColonyFire(map, organizer);
                        if (fire != null)
                        {
                            var c = FreeCellNear(fire.Position, map, organizer);
                            if (c.IsValid)
                            {
                                spot = c;
                                where = "CC_Venue_Campfire".Translate();
                                return true;
                            }
                        }
                        if (p.buildFire && Outside(map, p) && TryLayFire(map, organizer, !p.firePaid, out laid, out wood))
                        {
                            spot = FreeCellNear(laid.Position, map, organizer);
                            if (spot.IsValid)
                            {
                                where = "CC_Venue_Campfire".Translate();
                                return true;
                            }
                            // Nowhere to stand round it after all: the wood goes back.
                            UnlayFire(laid, wood);
                            laid = null;
                            wood = 0;
                        }
                        break;
                    }
                    case PlaySpot.Gather:
                    {
                        var gather = map.gatherSpotLister.activeSpots.Select(g => g.parent).Where(t => t != null && t.Spawned && t.Faction == Faction.OfPlayer)
                            .OrderBy(t => t.Position.DistanceToSquared(organizer.Position)).FirstOrDefault();
                        if (gather == null) break;
                        var c = FreeCellNear(gather.Position, map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Venue_GatherSpot".Translate(gather.LabelShort);
                        return true;
                    }
                    case PlaySpot.Outdoors:
                    {
                        if (!Outside(map, p)) break;
                        var c = OpenCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Venue_Outside".Translate();
                        return true;
                    }
                    case PlaySpot.Water:
                    {
                        if (!Outside(map, p)) break;
                        var c = WaterCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Water".Translate();
                        return true;
                    }
                    case PlaySpot.Fields:
                    {
                        if (!Outside(map, p)) break;
                        var c = FieldCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Fields".Translate();
                        return true;
                    }
                    case PlaySpot.Grave:
                    {
                        var grave = GraveFor(p);
                        if (grave == null) break;
                        var c = FreeCellNear(grave.Position, map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = GraveWords(grave);
                        return true;
                    }
                    case PlaySpot.Camp:
                    {
                        var c = CampCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Camp".Translate();
                        return true;
                    }
                    case PlaySpot.Stranger:
                    {
                        var x = p.other;
                        if (x == null || x.Dead || !x.Spawned || x.Map != map) break;
                        var c = FreeCellNear(x.Position, map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Stranger".Translate(x.Named("OTHER")).Resolve();
                        return true;
                    }
                    case PlaySpot.Gate:
                    {
                        var c = GateCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Gate".Translate();
                        return true;
                    }
                    case PlaySpot.Place:
                    {
                        var t = p.place;
                        if (t == null || !t.Spawned || t.Map != map) break;
                        if (t is Building_Grave)
                        {
                            var g = FreeCellNear(t.Position, map, organizer);
                            if (!g.IsValid) break;
                            spot = g;
                            where = GraveWords(t);
                            return true;
                        }
                        var c = FreeCellNear(t.Position, map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Place".Translate(t.LabelShort);
                        return true;
                    }
                    case PlaySpot.Walk:
                    {
                        var c = WalkCell(map, organizer);
                        if (!c.IsValid) break;
                        spot = c;
                        where = "CC_Play_Where_Walk".Translate();
                        return true;
                    }
                }
            }
            return false;
        }

        // ---------------- Work parties, walks, vigils, searches ----------------

        /// <summary>
        /// Where a walk goes, or a vigil is kept: the first of <paramref name="spot"/>'s places that works (a grave, the
        /// gate, the fields, the water, the camp, a fire, the open air, the event's own place, a long walk out), with the
        /// words for it. Never lays a fire.
        /// </summary>
        public static IntVec3 FocusCell(PlayOut p, PlaySpot spot, Pawn pawn, out string where)
        {
            where = null;
            if (p?.map == null || pawn == null) return IntVec3.Invalid;
            var saved = p.spots;
            bool fire = p.buildFire;
            p.spots = new List<PlaySpot> { spot };
            p.buildFire = false;
            try
            {
                return TryFind(p, pawn, out var c, out where, out _, out _) ? c : IntVec3.Invalid;
            }
            finally
            {
                p.spots = saved;
                p.buildFire = fire;
            }
        }

        /// <summary>
        /// The colony's gate: going out from the middle of the colony toward the nearest edge of the map, the last open
        /// cell still on the home ground (a dozen steps out where there's none), that can be reached safely. The other
        /// ways out are tried if that one is shut.
        /// </summary>
        public static IntVec3 GateCell(Map map, Pawn pawn)
        {
            if (map == null) return IntVec3.Invalid;
            var center = TemplatePlanner.ColonyCenter(map);
            var home = map.areaManager?.Home;
            var ways = new List<(int dx, int dz, int dist)>
            {
                (-1, 0, center.x), (1, 0, map.Size.x - 1 - center.x), (0, -1, center.z), (0, 1, map.Size.z - 1 - center.z)
            };
            foreach (var w in ways.OrderBy(w => w.dist))
            {
                var open = new List<IntVec3>();
                for (int step = 3; step <= 70; step++)
                {
                    var c = new IntVec3(center.x + w.dx * step, 0, center.z + w.dz * step);
                    if (!c.InBounds(map)) break;
                    bool ours = home != null && home[c];
                    if (!ours && step > 12) break;
                    if (!c.Roofed(map) && c.Standable(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && !c.Fogged(map)) open.Add(c);
                }
                // The farthest that can be reached safely.
                for (int i = open.Count - 1; i >= 0 && i >= open.Count - 12; i--)
                    if (Ok(open[i], map, pawn)) return open[i];
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// The <paramref name="i"/>th free place to stand round <paramref name="center"/> (see <see cref="PlayOutMath.RingOffset"/>),
        /// skipping cells that are blocked or taken; a free cell near it if the ring is full.
        /// </summary>
        public static IntVec3 RingCell(Map map, IntVec3 center, int i, Pawn pawn, ICollection<IntVec3> taken)
        {
            if (map == null || !center.IsValid) return IntVec3.Invalid;
            for (int k = 0; k < PlayOutMath.RingCount; k++)
            {
                PlayOutMath.RingOffset(i + k, out int dx, out int dz);
                var c = new IntVec3(center.x + dx, 0, center.z + dz);
                if (!c.InBounds(map) || c.GetEdifice(map) != null || (c.GetTerrain(map)?.IsWater ?? false)) continue;
                if (taken != null && taken.Contains(c)) continue;
                if (Ok(c, map, pawn)) return c;
            }
            return FreeCellNear(center, map, pawn);
        }

        /// <summary>
        /// A searcher's places, out from the middle of the colony (or from the fields, or the water) along their own arc of
        /// the compass: open ground they can reach without danger, never in water or the fog.
        /// </summary>
        public static List<IntVec3> SearchRoute(PlayOut p, Pawn pawn, int searcher, int searchers)
        {
            var route = new List<IntVec3>();
            var map = p?.map;
            if (map == null || pawn == null) return route;
            IntVec3 from;
            float near, far;
            switch (p.to)
            {
                case PlaySpot.Fields:
                    from = FieldCell(map, pawn);
                    near = 3f;
                    far = 12f;
                    break;
                case PlaySpot.Water:
                    from = WaterCell(map, pawn);
                    near = 4f;
                    far = 14f;
                    break;
                default:
                    from = IntVec3.Invalid;
                    near = 16f;
                    far = 34f;
                    break;
            }
            if (!from.IsValid)
            {
                from = TemplatePlanner.ColonyCenter(map);
                near = 16f;
                far = 34f;
            }
            int stops = PlayOutMath.SearchStops(searchers);
            for (int s = 0; s < stops; s++)
            {
                float bearing = PlayOutMath.SearchBearing(searcher, searchers, s, stops, p.seed) * Mathf.Deg2Rad;
                float radius = PlayOutMath.SearchRadius(s, stops, near, far);
                var aim = new IntVec3(from.x + Mathf.RoundToInt(Mathf.Cos(bearing) * radius), 0, from.z + Mathf.RoundToInt(Mathf.Sin(bearing) * radius));
                aim = aim.ClampInsideMap(map);
                if (CellFinder.TryFindRandomCellNear(aim, map, 6, c => c.Standable(map) && !c.Fogged(map) && !(c.GetTerrain(map)?.IsWater ?? false)
                                                                     && c.GetEdifice(map) == null && Ok(c, map, pawn), out var cell))
                    route.Add(cell);
            }
            return route;
        }

        /// <summary>Where a walk together gathers and comes back to: a gather spot, else the open air near the middle of the colony.</summary>
        public static IntVec3 MeetCell(PlayOut p, Pawn pawn)
        {
            var c = FocusCell(p, PlaySpot.Gather, pawn, out _);
            if (c.IsValid) return c;
            c = OpenCell(p.map, pawn);
            return c.IsValid ? c : FreeCellNear(TemplatePlanner.ColonyCenter(p.map), p.map, pawn);
        }

        /// <summary>Somewhere to stand: on the map, walkable, not a prison cell, not forbidden, and reachable without danger.</summary>
        public static bool Ok(IntVec3 c, Map map, Pawn pawn)
        {
            if (!c.IsValid || !c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) return false;
            var room = c.GetRoom(map);
            if (room != null && room.IsPrisonCell) return false;
            if (pawn != null && (c.IsForbidden(pawn) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.None))) return false;
            return true;
        }

        /// <summary>The first cell that passes, or an invalid cell (never the map's corner by default).</summary>
        private static IntVec3 First(IEnumerable<IntVec3> cells, System.Predicate<IntVec3> ok)
        {
            foreach (var c in cells)
                if (ok(c)) return c;
            return IntVec3.Invalid;
        }

        public static IntVec3 FreeCellNear(IntVec3 c, Map map, Pawn pawn)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, 3.9f, true))
                if (x.InBounds(map) && x.GetEdifice(map) == null && Ok(x, map, pawn)) return x;
            return IntVec3.Invalid;
        }

        /// <summary>Good weather for being out (a fire is lit whatever the weather: that's half the point of it).</summary>
        private static bool Outside(Map map, PlayOut p) => p.kind == PlayKind.Bonfire || JoyUtility.EnjoyableOutsideNow(map);

        /// <summary>An open, unroofed cell near the middle of the colony.</summary>
        public static IntVec3 OpenCell(Map map, Pawn pawn)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 16f, true))
                if (c.InBounds(map) && !c.Roofed(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && Ok(c, map, pawn)) return c;
            return IntVec3.Invalid;
        }

        /// <summary>Shallow water near the middle of the colony that someone can wade into (a river's edge, a pond, the shore).</summary>
        public static IntVec3 WaterCell(Map map, Pawn pawn)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 45f, true))
            {
                if (!c.InBounds(map) || !(c.GetTerrain(map)?.IsWater ?? false) || !c.Standable(map) || c.Roofed(map)) continue;
                if (Ok(c, map, pawn)) return c;
            }
            if (CellFinder.TryFindRandomCellNear(center, map, 90, x => (x.GetTerrain(map)?.IsWater ?? false) && x.Standable(map) && !x.Roofed(map) && Ok(x, map, pawn),
                    out var far))
                return far;
            return IntVec3.Invalid;
        }

        /// <summary>A walkable cell in the colony's biggest growing zone.</summary>
        public static IntVec3 FieldCell(Map map, Pawn pawn)
        {
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.CellCount).Take(3))
            {
                var cells = zone.Cells;
                if (cells == null || cells.Count == 0) continue;
                var middle = cells[cells.Count / 2];
                var c = First(cells.OrderBy(x => x.DistanceToSquared(middle)).Take(30), x => !x.Roofed(map) && Ok(x, map, pawn));
                if (c.IsValid) return c;
            }
            return IntVec3.Invalid;
        }

        // ---------------- Fires ----------------

        /// <summary>A lit fire of the colony's that people gather round: a campfire, a bonfire, a fire pit.</summary>
        public static Thing ColonyFire(Map map, Pawn near)
        {
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (var t in map.listerBuildings.allBuildingsColonist)
            {
                // A campfire still being built is a frame, not a fire.
                if (t == null || !t.Spawned || t is Frame) continue;
                string n = t.def.defName;
                if (n.IndexOf("Campfire", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Bonfire", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("FirePit", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel != null && !fuel.HasFuel) continue;
                float d = near != null ? t.Position.DistanceToSquared(near.Position) : 0f;
                if (d < bestDist)
                {
                    best = t;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>
        /// A campfire laid for the evening in the open near the middle of the colony, if the colony could build one at all:
        /// from its own wood (the cost of building one, in <paramref name="wood"/>), or, with <paramref name="take"/> off,
        /// from the wood the answer already set aside for it. It burns down when the gathering is over.
        /// </summary>
        public static bool TryLayFire(Map map, Pawn organizer, bool take, out Thing fire, out int wood)
        {
            fire = null;
            wood = 0;
            var def = ThingDefOf.Campfire;
            var woodDef = ThingDefOf.WoodLog;
            if (def == null || woodDef == null || !def.IsResearchFinished || !def.BuildableByPlayer) return false;
            int cost = def.costList?.FirstOrDefault(c => c.thingDef == woodDef)?.count ?? 20;
            if (take && map.resourceCounter.GetCount(woodDef) < cost) return false;
            var center = TemplatePlanner.ColonyCenter(map);
            IntVec3 cell = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(center, 18f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetZone(map) != null) continue;
                if (c.GetTerrain(map)?.IsWater ?? false) continue;
                if (!Ok(c, map, organizer) || !GenConstruct.CanPlaceBlueprintAt(def, c, Rot4.North, map).Accepted) continue;
                // Room to stand round it.
                if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Count(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null) < 6) continue;
                cell = c;
                break;
            }
            if (!cell.IsValid) return false;
            if (take && !Take(map, woodDef, cost)) return false;
            fire = ThingMaker.MakeThing(def);
            fire.SetFaction(Faction.OfPlayer);
            fire = GenSpawn.Spawn(fire, cell, map);
            var refuel = fire.TryGetComp<CompRefuelable>();
            refuel?.Refuel(refuel.Props.fuelCapacity);
            wood = take ? cost : 0;
            return true;
        }

        /// <summary>A fire that was laid and not needed after all: taken away, its wood put back.</summary>
        public static void UnlayFire(Thing fire, int wood)
        {
            if (fire == null || !fire.Spawned) return;
            var map = fire.Map;
            var at = fire.Position;
            fire.Destroy(DestroyMode.Vanish);
            if (wood <= 0 || ThingDefOf.WoodLog == null) return;
            var logs = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            logs.stackCount = wood;
            GenPlace.TryPlaceThing(logs, at, map, ThingPlaceMode.Near);
        }

        /// <summary>The gathering is over: the fire laid for it burns down to ash.</summary>
        public static void BurnDown(Thing fire)
        {
            if (fire == null || !fire.Spawned) return;
            var map = fire.Map;
            var at = fire.Position;
            fire.Destroy(DestroyMode.Vanish);
            if (ThingDefOf.Filth_Ash != null) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 2);
        }

        /// <summary>Takes so many of a thing from the stores (anything spawned and not forbidden). False if there wasn't enough.</summary>
        private static bool Take(Map map, ThingDef def, int count)
        {
            var stacks = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).OrderByDescending(t => t.IsInAnyStorage()).ToList();
            if (stacks.Sum(t => t.stackCount) < count) return false;
            int left = count;
            foreach (var t in stacks)
            {
                if (left <= 0) break;
                int n = System.Math.Min(left, t.stackCount);
                t.SplitOff(n).Destroy();
                left -= n;
            }
            return true;
        }

        // ---------------- Graves and the camp ----------------

        /// <summary>The event's own grave, else a grave of one of the colony's dead (one of the plan's people's kin first).</summary>
        public static Thing GraveFor(PlayOut p)
        {
            var map = p.map;
            if (map == null) return null;
            if (p.place is Building_Grave own && own.Spawned && own.Map == map) return own;
            Building_Grave best = null;
            int bestScore = int.MinValue;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Grave))
            {
                if (!(t is Building_Grave g) || !g.Spawned || !g.HasCorpse || g.Corpse?.InnerPawn == null) continue;
                var dead = g.Corpse.InnerPawn;
                int score = 0;
                if (dead.Faction == Faction.OfPlayer || dead.IsColonist) score += 10;
                foreach (var who in new[] { p.visitor, p.organizer, p.other })
                    if (who?.relations != null && who.relations.FamilyByBlood.Contains(dead)) score += 20;
                if (score > bestScore)
                {
                    best = g;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>The grave of someone in particular (a calendar day's dead), by ThingID.</summary>
        public static Thing GraveOf(Map map, string pawnId)
        {
            if (map == null || pawnId.NullOrEmpty()) return null;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Grave))
                if (t is Building_Grave g && g.Spawned && g.HasCorpse && g.Corpse?.InnerPawn?.ThingID == pawnId) return g;
            return null;
        }

        public static string GraveWords(Thing grave)
        {
            var dead = (grave as Building_Grave)?.Corpse?.InnerPawn;
            return dead != null ? "CC_Play_Where_GraveOf".Translate(dead.Named("DEAD")).Resolve() : "CC_Play_Where_Graves".Translate().Resolve();
        }

        /// <summary>A free cell by the camp's fire, while the camp is settled and at peace.</summary>
        public static IntVec3 CampCell(Map map, Pawn pawn)
        {
            var c = CampTracker.Instance?.camp;
            if (c == null || c.map != map || c.phase != CampPhase.Living || c.Hostile) return IntVec3.Invalid;
            var fire = c.FireCell;
            if (!fire.IsValid) return IntVec3.Invalid;
            if (CellFinder.TryFindRandomCellNear(fire, map, 3, x => x != fire && Ok(x, map, pawn) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null, out var cell))
                return cell;
            return IntVec3.Invalid;
        }

        /// <summary>Somewhere out past the buildings for a long walk: open ground some way off, reachable safely.</summary>
        public static IntVec3 WalkCell(Map map, Pawn pawn)
        {
            if (!JoyUtility.EnjoyableOutsideNow(pawn)) return IntVec3.Invalid;
            var center = TemplatePlanner.ColonyCenter(map);
            if (CellFinder.TryFindRandomCellNear(center, map, 30,
                    x => !x.Roofed(map) && x.DistanceTo(center) >= 14f && !(x.GetTerrain(map)?.IsWater ?? false) && Ok(x, map, pawn), out var cell))
                return cell;
            return IntVec3.Invalid;
        }

        // ---------------- Meals ----------------

        /// <summary>A cooked meal a colonist would sit down to: simple or better, never paste, kibble, drugs, baby food or a spoiled grill.</summary>
        public static bool IsFeastMeal(Thing t)
        {
            var ing = t?.def?.ingestible;
            if (ing == null || !t.def.IsNutritionGivingIngestible || t.def.IsDrug || t.def.IsCorpse || !ing.HumanEdible) return false;
            if (ing.preferability < FoodPreferability.MealSimple) return false;
            // Vanilla Cooking Expanded's grills spoiled by the cold keep their grade on paper, but nobody serves them at a feast.
            return t.def.defName != "BabyFood" && !Cuisine.IsSpoiledMeal(t.def);
        }

        /// <summary>The colony's cooked meals: in storage or lying about at home, not forbidden.</summary>
        public static IEnumerable<Thing> Meals(Map map)
        {
            if (map == null) yield break;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (!IsFeastMeal(t) || !t.Spawned || t.IsForbidden(Faction.OfPlayer)) continue;
                if (!t.IsInAnyStorage() && !map.areaManager.Home[t.Position]) continue;
                yield return t;
            }
        }

        public static int CountMeals(Map map)
        {
            int n = 0;
            foreach (var t in Meals(map)) n += t.stackCount;
            return n;
        }

        /// <summary>
        /// The best meal this colonist may have: the finest kind first, then the nearest to the feast; one they'd eat,
        /// their food policy allows, nobody else has claimed and they can get to.
        /// </summary>
        public static Thing BestMeal(Pawn p, Map map, IntVec3 spot) => BestMeal(p, map, spot, out _);

        /// <summary>
        /// The best meal this colonist may have (see above), and only one their beliefs let them eat (Wishes/Beliefs.cs):
        /// nothing that would leave them a bad "ate ..." memory (meat for a vegetarian, paste for one who finds it
        /// disgusting), nothing Alpha Memes would make them sick for, nothing their food policy rules out.
        /// <paramref name="noneFit"/>: there are meals, but none of them is fit for them, so they come without eating.
        /// </summary>
        public static Thing BestMeal(Pawn p, Map map, IntVec3 spot, out bool noneFit)
        {
            noneFit = false;
            if (p == null || map == null) return null;
            bool any = false;
            var fit = new List<Thing>();
            foreach (var t in Meals(map))
            {
                any = true;
                if (!t.IsForbidden(p) && !Beliefs.MindsEating(p, t)) fit.Add(t);
            }
            if (any && fit.Count == 0)
            {
                noneFit = true;
                return null;
            }
            return fit
                .OrderByDescending(t => (int)t.def.ingestible.preferability)
                .ThenBy(t => t.Position.DistanceToSquared(spot))
                .FirstOrDefault(t => p.CanReserve(t, 10, 1) && p.CanReach(t, PathEndMode.ClosestTouch, Danger.Some));
        }
    }
}
