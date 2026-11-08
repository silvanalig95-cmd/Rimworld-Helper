using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>One building a camp means to put up: what, of what, where, which way round.</summary>
    public class CampPlan : IExposable
    {
        public string role;
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rot = Rot4.North;
        public bool placed;
        /// <summary>Given up on (only for what may never be touched: the colony's, an ore, water; or nobody built it in time).</summary>
        public bool dropped;
        /// <summary>The hut or tent it belongs to (its walls, door and beds), 0 for none; a save from before rooms had one hut, all its walls in it.</summary>
        public int room;
        /// <summary>Its blueprint waits for its ground to be cleared first: plain rock mined, a tree no building goes over felled.</summary>
        public bool waiting;

        public CellRect Rect => GenAdj.OccupiedRect(cell, rot, def?.size ?? IntVec2.One);

        /// <summary>The finished building, if it stands (and is still theirs).</summary>
        public Building Built(Map map, Faction f)
        {
            if (map == null || def == null || f == null || !cell.InBounds(map)) return null;
            foreach (var t in cell.GetThingList(map))
                if (t is Building b && b.def == def && b.Faction == f) return b;
            return null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref role, "role");
            // Some of these come from a DLC (a meditation spot): one removed since loads as null, without errors.
            CouncilScribe.LookDef(ref def, "def");
            CouncilScribe.LookDef(ref stuff, "stuff");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rot, "rot", Rot4.North);
            Scribe_Values.Look(ref placed, "placed");
            Scribe_Values.Look(ref dropped, "dropped");
            Scribe_Values.Look(ref room, "room");
            Scribe_Values.Look(ref waiting, "waiting");
        }
    }

    /// <summary>
    /// Where a camp can go: reachable ground well away from the colony (no home area, zones or buildings of the colony
    /// nearby), close enough that the two share the land, open or wooded (trees, bushes, chunks and plain rock are cleared,
    /// as a player's builders would). And how the camp is laid out round its fire (CampBuildMath.Plan) and what it builds it
    /// with (CampKit).
    /// </summary>
    public static class CampSite
    {
        /// <summary>How far the camp itself reaches from its fire.</summary>
        public const int CoreRadius = CampBuildMath.Reach;
        /// <summary>Nearest a camp may be to the home area or any of the colony's buildings.</summary>
        public const int MinFromColony = 28;
        public const int MaxFromColony = 80;
        /// <summary>How far they go for wood and game.</summary>
        public const float WorkRadius = 26f;
        /// <summary>Nothing they take may lie this close to the home area.</summary>
        public const int HomeMargin = 8;

        /// <summary>Cells of the colony: its buildings and (thinned out) its home area.</summary>
        public static List<IntVec3> ColonyPoints(Map map)
        {
            var list = new List<IntVec3>();
            foreach (var b in map.listerBuildings.allBuildingsColonist) list.Add(b.Position);
            var home = map.areaManager.Home;
            if (home != null)
                foreach (var c in home.ActiveCells)
                    if ((c.x + c.z) % 3 == 0) list.Add(c);
            return list;
        }

        /// <summary>The middle of the colony: the average of its buildings, or of its home area.</summary>
        public static IntVec3 ColonyCenter(Map map)
        {
            var pts = map.listerBuildings.allBuildingsColonist.Select(b => b.Position).ToList();
            if (pts.Count == 0 && map.areaManager.Home != null) pts = map.areaManager.Home.ActiveCells.ToList();
            if (pts.Count == 0) return map.Center;
            return new IntVec3(Mathf.RoundToInt((float)pts.Average(p => p.x)), 0, Mathf.RoundToInt((float)pts.Average(p => p.z)));
        }

        private static int MinDistSquared(IntVec3 c, List<IntVec3> pts)
        {
            int best = int.MaxValue;
            foreach (var p in pts)
            {
                int dx = p.x - c.x, dz = p.z - c.z;
                int d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Open ground: standable, unroofed, outdoors, dry, no zone, no building, no home area (a spot to set something down on now).</summary>
        public static bool OpenGround(IntVec3 c, Map map)
        {
            if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.Roofed(map)) return false;
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.IsWater || terrain.passability == Traversability.Impassable) return false;
            if (map.zoneManager.ZoneAt(c) != null) return false;
            if (map.areaManager.Home != null && map.areaManager.Home[c]) return false;
            if (c.GetFirstBuilding(map) != null) return false;
            return !Marked(c, map);
        }

        /// <summary>The colony has plans for this spot: a plan drawn on it, or work marked on the ground.</summary>
        public static bool Marked(IntVec3 c, Map map) => map.planManager?.PlanAt(c) != null || map.designationManager.HasMapDesignationAt(c);

        // ---------------- What a cell holds ----------------

        /// <summary>Overhead rock within this of a rock they would mine: never, so no roof can come down (the game's roofs need a holder within about seven cells).</summary>
        public const float RoofSafe = 7.9f;

        /// <summary>What a cell holds, as a camp's builders see it (CampBuildMath.Fate); <paramref name="camp"/>: their own goods are theirs to move aside.</summary>
        public static CampCell CellAt(Map map, IntVec3 c, Camp camp = null)
        {
            var cell = new CampCell();
            if (!c.InBounds(map) || c.Fogged(map))
            {
                cell.outside = true;
                return cell;
            }
            var terrain = c.GetTerrain(map);
            if (terrain == null || terrain.IsWater || terrain.passability == Traversability.Impassable || terrain.affordances == null || !terrain.affordances.Contains(TerrainAffordanceDefOf.Light))
                cell.badGround = true;
            var home = map.areaManager.Home;
            if ((home != null && home[c]) || map.zoneManager.ZoneAt(c) != null || Marked(c, map)) cell.colony = true;
            var roof = c.GetRoof(map);
            if (roof != null && roof.isNatural) cell.roofed = true;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t.Faction == Faction.OfPlayer)
                {
                    cell.colony = true;
                    continue;
                }
                switch (t.def.category)
                {
                    case ThingCategory.Building:
                        if (t.def.mineable)
                        {
                            if (CampTake.PlainRock(t.def) && !RoofNear(map, c)) cell.rock = true;
                            else cell.ore = true;
                        }
                        else cell.building = true;
                        break;
                    case ThingCategory.Plant:
                        if (!(t is Plant p) || p.sown || p.def.plant == null || p.def.plant.treeCategory == TreeCategory.Super || map.designationManager.DesignationOn(t) != null) cell.sown = true;
                        else if (p.def.plant.IsTree || p.def.passability == Traversability.Impassable) cell.tree = true;
                        else if (p.def.plant.harvestWork > ThingDefOf.Plant_Dandelion.plant.harvestWork) cell.plant = true;
                        break;
                    case ThingCategory.Item:
                        var f = CampTake.Facts(t, camp);
                        if (f.theirs && !f.stored) cell.chunk = true;
                        else if (f.stored || f.forbidden || f.home || f.zoned) cell.colony = true;
                        else if (CampTakeMath.May(f)) cell.chunk = true;
                        else cell.valuable = true;
                        break;
                }
            }
            return cell;
        }

        /// <summary>Any natural roof within <see cref="RoofSafe"/> of the cell.</summary>
        private static bool RoofNear(Map map, IntVec3 c)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, RoofSafe, true))
                if (x.InBounds(map) && x.Roofed(map)) return true;
            return false;
        }

        public static CellFate FateAt(Map map, IntVec3 c, Camp camp = null) => CampBuildMath.Fate(CellAt(map, c, camp));

        /// <summary>The camp could build here, clearing it first if need be.</summary>
        public static bool Buildable(IntVec3 c, Map map) => FateAt(map, c) != CellFate.Never;

        // ---------------- Where ----------------

        /// <summary>
        /// The best spot for a camp: open or wooded ground clear of the colony, its reach nearly all ground they can clear and
        /// build on. <paramref name="shade"/>: hot weather, when trees around count for more (they pitch in their shade).
        /// </summary>
        public static bool TryFind(Map map, out IntVec3 center, bool shade = false)
        {
            center = IntVec3.Invalid;
            if (map == null) return false;
            int margin = CoreRadius + 6;
            if (map.Size.x < margin * 2 + 20 || map.Size.z < margin * 2 + 20) return false;
            // The ground first, so a map that is mostly mountain, fog or water still finds the little it has; then, on a
            // tight map, a spot a little nearer the colony and with a little less room.
            var open = OpenCells(map, margin);
            if (open.Count == 0) return false;
            var pts = ColonyPoints(map);
            return TryFindIn(map, open, pts, shade, MinFromColony, 0.8f, out center)
                || TryFindIn(map, open, pts, shade, MinFromColonyTight, 0.65f, out center);
        }

        /// <summary>On a tight map (mountain, marsh, a crowded valley): how near the colony a camp may come, as a last resort.</summary>
        public const int MinFromColonyTight = 20;

        /// <summary>How many spots are weighed for a camp each time.</summary>
        private const int Tries = 400;

        /// <summary>Every cell a camp's fire could stand on (ground they can walk and clear: open, grass, bushes, trees, a chunk), every third cell each way.</summary>
        private static List<IntVec3> OpenCells(Map map, int margin)
        {
            var list = new List<IntVec3>();
            for (int x = margin; x < map.Size.x - margin; x += 3)
                for (int z = margin; z < map.Size.z - margin; z += 3)
                {
                    var c = new IntVec3(x, 0, z);
                    var fate = FateAt(map, c);
                    if (fate != CellFate.Never && fate != CellFate.Mine) list.Add(c);
                }
            return list;
        }

        private static bool TryFindIn(Map map, List<IntVec3> open, List<IntVec3> pts, bool shade, int minFrom, float roomShare, out IntVec3 center)
        {
            center = IntVec3.Invalid;
            var traverse = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            float bestScore = float.MinValue;
            int minSq = minFrom * minFrom, maxSq = MaxFromColony * MaxFromColony;
            int tries = Mathf.Min(Tries, open.Count);
            var h = CampTracker.HoldingAt(map);
            for (int i = 0; i < tries; i++)
            {
                var c = open.Count <= Tries ? open[i] : open.RandomElement();
                // Well clear of neighbours settled for good (Holding.cs).
                if (h != null && h.map == map && h.center.IsValid && c.InHorDistOf(h.center, CoreRadius * 2 + 10)) continue;
                int d = pts.Count > 0 ? MinDistSquared(c, pts) : int.MaxValue;
                if (d < minSq || (pts.Count > 0 && d > maxSq)) continue;
                // Room for the camp: nearly all of its reach ground they can clear and build on, nothing of the colony's in it.
                int room = 0, total = 0, clearing = 0;
                bool colony = false;
                foreach (var x in GenRadial.RadialCellsAround(c, CoreRadius - 1, true))
                {
                    // Every other cell is enough to judge the ground by.
                    if ((x.x + x.z) % 2 != 0) continue;
                    total++;
                    var cell = CellAt(map, x);
                    if (cell.colony)
                    {
                        colony = true;
                        break;
                    }
                    var fate = CampBuildMath.Fate(cell);
                    if (fate == CellFate.Never) continue;
                    room++;
                    clearing += CampBuildMath.Cost(fate);
                }
                if (colony || room < total * roomShare) continue;
                if (!map.reachability.CanReachMapEdge(c, traverse) || !map.reachability.CanReachColony(c)) continue;
                // Close enough to share the land, with trees about (more in the heat, for the shade), little to clear; a
                // little chance for variety.
                float dist = pts.Count > 0 ? Mathf.Sqrt(d) : 50f;
                int trees = 0;
                foreach (var x in GenRadial.RadialCellsAround(c, 14f, false))
                    if ((x.x + x.z) % 2 == 0 && x.InBounds(map) && x.GetPlant(map) is Plant p && p.def.plant.IsTree) trees++;
                float score = -Mathf.Abs(dist - 42f) * 0.6f + Mathf.Min(trees, 15) * (shade ? 1.8f : 0.6f) - clearing * 0.05f + Rand.Range(0f, 6f);
                if (score > bestScore)
                {
                    bestScore = score;
                    center = c;
                }
            }
            return center.IsValid;
        }

        /// <summary>Trees close round the spot, enough to sit and sleep in their shade (the camp never fells those, CampWork.Tree).</summary>
        public static bool Shaded(Map map, IntVec3 center)
        {
            int trees = 0;
            foreach (var x in GenRadial.RadialCellsAround(center, ShadeRadius, false))
                if (x.InBounds(map) && x.GetPlant(map) is Plant p && p.def.plant.IsTree && ++trees >= ShadeTrees) return true;
            return false;
        }

        private const float ShadeRadius = CoreRadius + 1;
        private const int ShadeTrees = 6;

        private static readonly Dictionary<int, (int day, bool ok)> cache = new Dictionary<int, (int, bool)>();
        private static Game cacheGame;

        /// <summary>Whether a camp could go anywhere on this map (worked out once a day).</summary>
        public static bool AnyRoom(Map map)
        {
            // Map ids and days start over in every game.
            if (cacheGame != Current.Game)
            {
                cacheGame = Current.Game;
                cache.Clear();
            }
            int day = GenDate.DaysPassed;
            if (cache.TryGetValue(map.uniqueID, out var c) && c.day == day) return c.ok;
            bool ok = TryFind(map, out _);
            cache[map.uniqueID] = (day, ok);
            return ok;
        }

        /// <summary>A unit step from the colony toward the camp (which way is "away").</summary>
        public static IntVec3 Away(IntVec3 center, IntVec3 colony)
        {
            var v = new Vector3(center.x - colony.x, 0f, center.z - colony.z);
            if (v.sqrMagnitude < 0.01f) return IntVec3.North;
            v.Normalize();
            return Mathf.Abs(v.x) > Mathf.Abs(v.z) ? new IntVec3(v.x > 0 ? 1 : -1, 0, 0) : new IntVec3(0, 0, v.z > 0 ? 1 : -1);
        }

        /// <summary>The quarter turn that sends the layout's +z (away from the colony) the way <paramref name="away"/> points.</summary>
        public static int TurnFor(IntVec3 away) => away.x > 0 ? 1 : away.z < 0 ? 2 : away.x < 0 ? 3 : 0;

        /// <summary>Which way the camp lies from the colony, in words.</summary>
        public static string Direction(IntVec3 center, IntVec3 colony)
        {
            float dx = center.x - colony.x, dz = center.z - colony.z;
            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            string[] keys = { "CC_Camp_Dir_E", "CC_Camp_Dir_NE", "CC_Camp_Dir_N", "CC_Camp_Dir_NW", "CC_Camp_Dir_W", "CC_Camp_Dir_SW", "CC_Camp_Dir_S", "CC_Camp_Dir_SE" };
            int i = Mathf.RoundToInt(angle / 45f) % 8;
            return keys[i].Translate();
        }

        // ---------------- Layout ----------------

        /// <summary>Beds this close to the fire are drawn in round it, for cold nights.</summary>
        public const float CloseToFire = 3.5f;

        /// <summary>Most of the camp's beds round the fire are drawn in close to it (they sleep round it, not in a hut or tents).</summary>
        public static bool BedsClose(List<CampPlan> plans)
        {
            var fire = plans.FirstOrDefault(p => p.role == "fire");
            var beds = plans.Where(p => p.role == "bed").ToList();
            return fire != null && beds.Count > 0 && beds.Count(b => b.cell.InHorDistOf(fire.cell, CloseToFire + 1.6f)) * 2 >= beds.Count;
        }

        /// <summary>How a kind of camp sleeps, with what this people has (a mod's tent that is a bed makes tents of the beds themselves).</summary>
        public static CampLayoutSpec SpecFor(CampKindDef kind, int people, float days, bool cold, Faction f, out Dictionary<string, CampStructure> pieces)
        {
            pieces = new Dictionary<string, CampStructure>();
            foreach (var role in CampKit.Roles)
            {
                var p = CampKit.Piece(kind, role, f);
                if (p?.Def != null) pieces[role] = p;
            }
            // A kind's own spot (a shrine, a stage), if it has one.
            var spot = CampKit.Piece(kind, "spot", f);
            if (spot?.Def != null) pieces["spot"] = spot;
            var spec = new CampLayoutSpec { sleepers = Mathf.Max(1, people), sleep = kind.sleep };
            var bed = pieces.TryGetValue("bed", out var b) ? b.Def : null;
            var tent = pieces.TryGetValue("tent", out var t) ? t.Def : null;
            if (spec.sleep == CampSleepForm.Tents && tent != null && tent.IsBed)
            {
                spec.tentBeds = true;
                bed = tent;
            }
            if (spec.sleep != CampSleepForm.Ring && (!pieces.ContainsKey(spec.sleep == CampSleepForm.Hut ? "hut" : "tentwall") || !pieces.ContainsKey(spec.sleep == CampSleepForm.Hut ? "door" : "tentdoor"))
                && !spec.tentBeds)
                spec.sleep = CampSleepForm.Ring;
            if (bed != null)
            {
                spec.bedW = bed.size.x;
                spec.bedD = bed.size.z;
            }
            spec.windbreak = cold && spec.sleep == CampSleepForm.Ring && pieces.ContainsKey("wall");
            var table = pieces.TryGetValue("table", out var tb) ? tb.Def : null;
            spec.tables = table == null ? 0 : CampBuildMath.TablesFor(people, days, kind.dines);
            if (table != null)
            {
                spec.tableW = table.size.x;
                spec.tableD = table.size.z;
            }
            spec.seats = pieces.ContainsKey("seat");
            bool hunts = kind.huntsPerDay > 0f || (kind.works?.Contains(CampLabour.Hunt) ?? false);
            spec.butcher = pieces.ContainsKey("butcher") && (hunts || kind.structures.Any(s => s.role == "butcher"));
            // A crafting spot of the kit's, unless their own spot is a work table already (the smith's).
            spec.craft = pieces.ContainsKey("craft") && (CampBuildMath.CraftFor(days) || (kind.works?.Contains(CampLabour.Craft) ?? false)) && !(spot?.Def?.IsWorkTable ?? false);
            spec.stall = kind.structures.Any(s => s.role == "stall") && pieces.ContainsKey("stall");
            spec.spot = kind.structures.Any(s => s.role == "spot") && pieces.ContainsKey("spot");
            spec.picket = pieces.ContainsKey("picket") && (kind.animals?.Any(a => a != null && (a.pack || a.kinds?.Count > 0)) ?? false);
            spec.stash = pieces.ContainsKey("stash") && days >= 3f;
            spec.lights = CampBuildMath.LightsFor(people, pieces.ContainsKey("light"));
            return spec;
        }

        /// <summary>
        /// The camp around its fire (CampBuildMath.Plan, its +z away from the colony): tried at the spot and a few cells
        /// round it, every way round, the layout with everything in it and least to clear taken; where nothing fits it all,
        /// the one with most of it; a shelter that won't go anywhere, and they sleep round the fire instead. Null when not even
        /// the fire and the beds fit. <paramref name="center"/> may move a few cells.
        /// </summary>
        public static List<CampPlan> Layout(Map map, ref IntVec3 center, CampKindDef kind, int people, IntVec3 colony, out IntVec3 stash, out IntVec3 share, bool cold, Faction f, float days)
        {
            stash = share = IntVec3.Invalid;
            var spec = SpecFor(kind, people, days, cold, f, out var pieces);
            var cells = new Dictionary<IntVec3, int>();
            int Cost(IntVec3 c)
            {
                if (!cells.TryGetValue(c, out int v)) cells[c] = v = CampBuildMath.Cost(FateAt(map, c));
                return v;
            }
            int baseTurn = TurnFor(Away(center, colony));
            CampLayout best = null;
            IntVec3 bestAt = center;
            int bestTurn = baseTurn;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 2 && (best == null || !best.Livable); attempt++)
            {
                if (attempt == 1)
                {
                    // No room for the shelter anywhere here: they sleep round the fire.
                    if (spec.sleep == CampSleepForm.Ring) break;
                    spec.sleep = CampSleepForm.Ring;
                    spec.tentBeds = false;
                    if (pieces.TryGetValue("bed", out var b2) && b2.Def != null)
                    {
                        spec.bedW = b2.Def.size.x;
                        spec.bedD = b2.Def.size.z;
                    }
                }
                foreach (var shift in new[] { IntVec3.Zero, new IntVec3(3, 0, 0), new IntVec3(-3, 0, 0), new IntVec3(0, 0, 3), new IntVec3(0, 0, -3), new IntVec3(6, 0, 0), new IntVec3(-6, 0, 0), new IntVec3(0, 0, 6), new IntVec3(0, 0, -6) })
                {
                    var at = center + shift;
                    if (!at.InBounds(map)) continue;
                    var fate = FateAt(map, at);
                    if (fate == CellFate.Never || fate == CellFate.Mine) continue;
                    foreach (int k in new[] { baseTurn, baseTurn + 1, baseTurn + 3, baseTurn + 2 })
                    {
                        int turn = k % 4;
                        var layout = CampBuildMath.Plan(spec, (x, z) =>
                        {
                            var (wx, wz) = CampBuildMath.Turn(x, z, turn);
                            return Cost(new IntVec3(at.x + wx, 0, at.z + wz));
                        });
                        if (!layout.pieces.Any(p => p.role == "fire")) continue;
                        float score = (layout.Livable ? 10000f : 0f) - layout.missing.Count * 500f - layout.cost - (turn == baseTurn ? 0f : 5f) - shift.LengthHorizontal * 2f;
                        if (score <= bestScore) continue;
                        bestScore = score;
                        best = layout;
                        bestAt = at;
                        bestTurn = turn;
                    }
                    // Everything fits here: no need to look further.
                    if (best != null && best.Complete) break;
                }
            }
            if (best == null || !best.Livable) return null;
            center = bestAt;
            var plans = new List<CampPlan>();
            foreach (var p in best.pieces)
            {
                string role = p.role == "bed" && spec.tentBeds ? "tent" : p.role == "hutbed" ? "bed"
                    : p.role == "hut" ? (spec.sleep == CampSleepForm.Hut ? "hut" : "tentwall")
                    : p.role == "door" ? (spec.sleep == CampSleepForm.Hut ? "door" : "tentdoor") : p.role;
                if (!pieces.TryGetValue(role, out var s) || s.Def == null) continue;
                var plan = PlanFor(s, p, center, bestTurn);
                if (plan == null) continue;
                // The plan's role is the layout's (a hut's bed is a "hutbed", a tent that is a bed a "bed").
                plan.role = p.role;
                plans.Add(plan);
            }
            var (sx, sz) = CampBuildMath.Turn(best.stash.x, best.stash.z, bestTurn);
            stash = new IntVec3(center.x + sx, 0, center.z + sz);
            var (hx, hz) = CampBuildMath.Turn(0, -8, bestTurn);
            share = new IntVec3(center.x + hx, 0, center.z + hz);
            if (!share.InBounds(map) || !share.Standable(map)) share = CellFinder.RandomClosewalkCellNear(center + (share - center) * 2 / 3, map, 3);
            return plans;
        }

        /// <summary>The piece set down on the map: where its rectangle goes once turned with the layout, and the cell and rotation the game needs for it.</summary>
        private static CampPlan PlanFor(CampStructure s, CampPiece p, IntVec3 center, int turn)
        {
            var def = s.Def;
            var (ax, az) = CampBuildMath.Turn(p.minX, p.minZ, turn);
            var (bx, bz) = CampBuildMath.Turn(p.maxX, p.maxZ, turn);
            var rect = CellRect.FromLimits(center.x + Mathf.Min(ax, bx), center.z + Mathf.Min(az, bz), center.x + Mathf.Max(ax, bx), center.z + Mathf.Max(az, bz));
            var facing = new Rot4((p.facing + turn) % 4);
            var rots = def.rotatable ? new[] { facing, facing.Opposite, facing.Rotated(RotationDirection.Clockwise), facing.Rotated(RotationDirection.Counterclockwise) } : new[] { Rot4.North };
            foreach (var rot in rots)
                foreach (var c in rect)
                    if (GenAdj.OccupiedRect(c, rot, def.size).Equals(rect))
                        return new CampPlan { role = s.role, def = def, stuff = s.StuffFor(def), cell = c, rot = rot, room = p.room };
            // A piece smaller than its place (a mod's round table where a 1x2 was planned): at the place's first cell.
            var small = GenAdj.OccupiedRect(rect.minX == rect.maxX && rect.minZ == rect.maxZ ? rect.CenterCell : new IntVec3(rect.minX, 0, rect.minZ), Rot4.North, def.size);
            if (small.Cells.All(rect.Contains)) return new CampPlan { role = s.role, def = def, stuff = s.StuffFor(def), cell = small.CenterCell, rot = Rot4.North, room = p.room };
            return null;
        }

        /// <summary>The cells under each hut's or tent's roof (inside its walls), room by room; a save from before rooms, one hut.</summary>
        public static IEnumerable<IntVec3> HutInside(List<CampPlan> plans, int room = -1)
        {
            foreach (var r in plans.Where(p => p.role == "hut" || p.role == "door").Select(p => p.room).Distinct())
            {
                if (room >= 0 && r != room) continue;
                var walls = plans.Where(p => (p.role == "hut" || p.role == "door") && p.room == r).Select(p => p.cell).ToList();
                if (walls.Count < 8) continue;
                var solid = new HashSet<IntVec3>(walls);
                int minX = walls.Min(c => c.x), maxX = walls.Max(c => c.x), minZ = walls.Min(c => c.z), maxZ = walls.Max(c => c.z);
                for (int x = minX + 1; x < maxX; x++)
                    for (int z = minZ + 1; z < maxZ; z++)
                    {
                        var c = new IntVec3(x, 0, z);
                        if (!solid.Contains(c)) yield return c;
                    }
            }
        }

        /// <summary>The rooms of the camp (its huts and tents), by number.</summary>
        public static IEnumerable<int> Rooms(List<CampPlan> plans) => plans.Where(p => p.role == "hut" || p.role == "door").Select(p => p.room).Distinct();
    }
}
