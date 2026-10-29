using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One piece of furniture in a room being rearranged: the building, its part in the layout rules, and what becomes of it.</summary>
    public class RearrangePiece
    {
        public Building thing;
        public RoomArrangeMath.Item item;
        /// <summary>Why it stays where it is (the end of a CC_Rearr_Stay_ key), or null when it may move.</summary>
        public string stay;
        /// <summary>Where it goes, in map cells.</summary>
        public IntVec3 to;
        public Rot4 toRot;
        /// <summary>Needs power and has no conduit within reach of its new place.</summary>
        public bool losesPower;

        public bool Moves => stay == null && item != null && !item.stranded && item.moved;
        public bool Kept => stay == null && item != null && !item.stranded && !item.moved;
        public bool Stays => !Moves && !Kept;

        /// <summary>Why it goes where it goes, or why it stays, for the player.</summary>
        public string Reason =>
            stay != null ? ("CC_Rearr_Stay_" + stay).Translate().Resolve()
            : item.stranded ? "CC_Rearr_Stay_Stranded".Translate().Resolve()
            : ("CC_Rearr_Why_" + (item.why ?? "fits")).Translate().Resolve();
    }

    /// <summary>A room looked over for rearranging: its floor as the layout rules see it, and every piece in it.</summary>
    public class RearrangePlan
    {
        public Room room;
        public Map map;
        public CellRect rect;
        public List<IntVec3> cells = new List<IntVec3>();
        public RoomArrangeMath.Grid grid;
        public List<RearrangePiece> pieces = new List<RearrangePiece>();
        public List<string> notes = new List<string>();
        /// <summary>Shelves and stores with goods in them are moved too (the goods are set down and hauled back).</summary>
        public bool moveStocked;
        public string roomLabel;

        public int Moves => pieces.Count(p => p.Moves);
        public int KeptCount => pieces.Count(p => p.Kept);
        /// <summary>What stays put; blueprints and frames only hold their cells and aren't counted.</summary>
        public int StayCount => pieces.Count(p => p.Stays && p.stay != "Blueprint");
        public IEnumerable<RearrangePiece> Moving => pieces.Where(p => p.Moves);
        public IEnumerable<RearrangePiece> Staying => pieces.Where(p => p.Stays && p.stay != "Blueprint");
        public IEnumerable<RearrangePiece> Unpowered => pieces.Where(p => p.Moves && p.losesPower);
    }

    /// <summary>
    /// "Rearrange a room": reads a built room, has the layout rules (<see cref="RoomArrangeMath"/>) place its furniture
    /// anew, and carries the moves out with the game's own reinstall orders, a few at a time as the cells free up
    /// (<see cref="RearrangeQueue"/>). Only what can be picked up moves (any mod that makes more things minifiable
    /// widens that); walls, doors, pipes and conduits, plumbed fixtures, growing plants, filled graves and, unless
    /// asked, stocked shelves stay where they are and are worked around.
    /// </summary>
    public static class RoomRearranger
    {
        /// <summary>Bigger rooms than this are for the player's own hands: the rules place every piece against every cell.</summary>
        public const int MaxCells = 400;

        /// <summary>The room can be rearranged: enclosed, not too big, with a door.</summary>
        public static AcceptanceReport CanDo(Room room)
        {
            if (room == null || room.Map == null || room.PsychologicallyOutdoors || room.TouchesMapEdge || !room.ProperRoom || room.IsDoorway)
                return "CC_Rearr_NotARoom".Translate();
            if (room.CellCount > MaxCells) return "CC_Rearr_TooBig".Translate(MaxCells);
            if (DoorsInside(room, new HashSet<IntVec3>(room.Cells)).Count == 0) return "CC_Rearr_NoDoor".Translate();
            return true;
        }

        /// <summary>The floor cells just inside each of the room's doors.</summary>
        private static List<IntVec3> DoorsInside(Room room, HashSet<IntVec3> cells)
        {
            var map = room.Map;
            var result = new List<IntVec3>();
            foreach (var c in cells)
            {
                for (int i = 0; i < 4; i++)
                {
                    var n = c + GenAdj.CardinalDirections[i];
                    if (cells.Contains(n) || !n.InBounds(map)) continue;
                    var ed = n.GetEdifice(map);
                    if (ed != null && ed.def.IsDoor && !result.Contains(c))
                    {
                        result.Add(c);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>Looks the room over and works out where everything goes. Null if the room can't be done.</summary>
        public static RearrangePlan Survey(Room room, bool moveStocked)
        {
            if (!CanDo(room).Accepted) return null;
            var map = room.Map;
            var plan = new RearrangePlan { room = room, map = map, rect = room.ExtentsClose, moveStocked = moveStocked, roomLabel = room.GetRoomRoleLabel() };
            var cells = new HashSet<IntVec3>(room.Cells);
            plan.cells.AddRange(cells);
            var rect = plan.rect;
            var g = new RoomArrangeMath.Grid { w = rect.Width, h = rect.Height };
            g.floor = new bool[g.w, g.h];
            g.heavy = new bool[g.w, g.h];
            foreach (var c in cells)
            {
                int x = c.x - rect.minX, z = c.z - rect.minZ;
                if (x < 0 || z < 0 || x >= g.w || z >= g.h) continue;
                var terrain = c.GetTerrain(map);
                if (terrain == null || terrain.passability == Traversability.Impassable) continue;
                g.floor[x, z] = true;
                var aff = terrain.affordances;
                g.heavy[x, z] = aff != null && aff.Contains(TerrainAffordanceDefOf.Medium) && aff.Contains(TerrainAffordanceDefOf.Heavy);
            }
            foreach (var d in DoorsInside(room, cells)) g.doorInside.Add((d.x - rect.minX, d.z - rect.minZ));
            plan.grid = g;

            // Every piece on the room's floor: what may move as an item for the rules, the rest as a fixed block.
            var seen = new HashSet<Thing>();
            var player = Faction.OfPlayerSilentFail;
            foreach (var thing in room.ContainedAndAdjacentThings)
            {
                if (thing == null || !seen.Add(thing)) continue;
                var occ = thing.OccupiedRect();
                bool inside = true;
                foreach (var c in occ)
                    if (!cells.Contains(c)) inside = false;
                if (!inside) continue;
                if (thing is Blueprint || thing is Frame)
                {
                    // Something still to be built: its cells are spoken for.
                    if (thing.def.entityDefToBuild is ThingDef td && td.IsEdifice())
                        plan.pieces.Add(new RearrangePiece { thing = thing as Building, item = BlockFor(thing, rect), stay = "Blueprint" });
                    continue;
                }
                if (!(thing is Building b) || !EdificeUtility.IsEdifice(b.def)) continue;
                var piece = new RearrangePiece { thing = b, item = ItemFor(b, rect) };
                piece.stay = StayReason(b, map, player, moveStocked);
                if (piece.stay != null) piece.item.fixedPlace = true;
                plan.pieces.Add(piece);
            }

            LinkFacilities(plan.pieces);

            // Arrange; a target the ground won't take, or a place worker refuses, pins that piece and the rest is placed again.
            var items = plan.pieces.Select(p => p.item).ToList();
            for (int round = 0; round < 4; round++)
            {
                plan.notes = RoomArrangeMath.Arrange(g, items);
                bool again = false;
                foreach (var p in plan.pieces)
                {
                    if (!p.Moves) continue;
                    p.to = new IntVec3(rect.minX + p.item.x, 0, rect.minZ + p.item.z);
                    p.toRot = new Rot4(p.item.rot);
                    if (SpotTakesIt(p.thing, p.to, p.toRot, map)) continue;
                    p.stay = "NoSpot";
                    p.item.fixedPlace = true;
                    again = true;
                }
                if (!again) break;
            }
            foreach (var p in plan.pieces)
            {
                if (!p.Moves) continue;
                p.losesPower = NeedsPower(p.thing) && PowerConnectionMaker.BestTransmitterForConnector(p.to, map) == null;
            }
            return plan;
        }

        /// <summary>
        /// A facility (a tool cabinet, a dresser, a multi-analyzer) learns how far it reaches and which pieces here it
        /// serves, so the rules put it within reach of as many as they can. Those that must stand by a bed's head are
        /// bedside pieces already.
        /// </summary>
        private static void LinkFacilities(List<RearrangePiece> pieces)
        {
            foreach (var f in pieces)
            {
                var fac = f.thing?.def.GetCompProperties<CompProperties_Facility>();
                if (fac == null || fac.mustBePlacedAdjacent || fac.mustBePlacedAdjacentCardinalToBedHead || fac.mustBePlacedAdjacentCardinalToAndFacingBedHead || fac.maxDistance <= 0f) continue;
                var links = new HashSet<int>();
                foreach (var t in pieces)
                {
                    if (t == f || t.thing == null) continue;
                    var aff = t.thing.def.GetCompProperties<CompProperties_AffectedByFacilities>();
                    if (aff?.linkableFacilities != null && aff.linkableFacilities.Contains(f.thing.def)) links.Add(t.item.id);
                }
                if (links.Count == 0) continue;
                f.item.reach = fac.maxDistance;
                f.item.links = links;
            }
        }

        /// <summary>The ground takes the piece there and nothing about the spot itself forbids it (other furniture may still stand there for now).</summary>
        private static bool SpotTakesIt(Thing t, IntVec3 cell, Rot4 rot, Map map)
        {
            var def = t.def;
            try
            {
                foreach (var c in GenAdj.OccupiedRect(cell, rot, def.size))
                    if (!c.InBounds(map) || c.Fogged(map) || !GenConstruct.CanBuildOnTerrain(def, c, map, rot, t, t.Stuff)) return false;
                var workers = def.PlaceWorkers;
                if (workers != null)
                    foreach (var pw in workers)
                        if (!pw.AllowsPlacing(def, cell, rot, map, t, t).Accepted) return false;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("rearrange spot " + def.defName, e);
                return false;
            }
            return true;
        }

        private static bool NeedsPower(Thing t)
        {
            var p = t.TryGetComp<CompPowerTrader>();
            return p != null && p.Props != null && !p.Props.transmitsPower && p.Props.PowerConsumption > 0f;
        }

        /// <summary>Why a piece stays where it is, or null when it may move.</summary>
        private static string StayReason(Building b, Map map, Faction player, bool moveStocked)
        {
            var def = b.def;
            if (player == null || b.Faction != player) return "NotOurs";
            if (!def.Minifiable || def.installBlueprintDef == null) return "Fixed";
            if (IsPiped(b)) return "Plumbed";
            if (IsRitual(def)) return "Ritual";
            if (b is Building_PlantGrower pg && !Avail.IsPlantPot(def) && pg.PlantsOnMe.Any()) return "Plants";
            if (b is Building_Grave grave && grave.HasCorpse) return "Grave";
            if (!moveStocked && b is Building_Storage st && st.slotGroup != null && st.slotGroup.HeldThings.Any()) return "Stocked";
            var dm = map.designationManager;
            if (dm.DesignationOn(b, DesignationDefOf.Deconstruct) != null || dm.DesignationOn(b, DesignationDefOf.Uninstall) != null
                || InstallBlueprintUtility.ExistingBlueprintFor(b) != null) return "Orders";
            return null;
        }

        /// <summary>Joined to a pipe network: Dubs Bad Hygiene's plumbing, or a Vanilla Expanded resource pipe (gas, chemfuel).</summary>
        private static bool IsPiped(Building b)
        {
            var comps = b.AllComps;
            if (comps == null) return false;
            foreach (var c in comps)
            {
                var type = c?.GetType();
                if (type == null) continue;
                if (HomeLife.ClassIs(type, "DubsBadHygiene", "CompPipe") || HomeLife.ClassIs(type, "PipeSystem", "CompResource")) return true;
            }
            return false;
        }

        /// <summary>Thrones, altars, lecterns, pews, meditation spots: placed with care for a ceremony, left to the player.</summary>
        private static bool IsRitual(ThingDef def)
        {
            foreach (var role in new[] { "throne", "altar", "lectern", "pew", "meditation", "holding", "inhibitor" })
            {
                try
                {
                    var pred = RoomTemplateDef.RolePredicate(role);
                    if (pred != null && pred(def)) return true;
                }
                catch
                {
                    // A modded def a role check can't read: not a ritual piece.
                }
            }
            return def.thingClass != null && typeof(Building_Throne).IsAssignableFrom(def.thingClass);
        }

        /// <summary>A built piece as the layout rules see it.</summary>
        private static RoomArrangeMath.Item ItemFor(Building b, CellRect rect)
        {
            var def = b.def;
            var it = new RoomArrangeMath.Item
            {
                id = b.thingIDNumber,
                sizeX = def.size.x,
                sizeZ = def.size.z,
                rotatable = def.rotatable,
                passable = RoomTemplateDef.IsPassable(def),
                curX = b.Position.x - rect.minX,
                curZ = b.Position.z - rect.minZ,
                curRot = b.Rotation.AsInt
            };
            if (def.hasInteractionCell) it.spots.Add((def.interactionCellOffset.x, def.interactionCellOffset.z));
            if (def.multipleInteractionCellOffsets != null)
                foreach (var o in def.multipleInteractionCellOffsets)
                    if (!it.spots.Contains((o.x, o.z))) it.spots.Add((o.x, o.z));
            var need = def.GetTerrainAffordanceNeed(b.Stuff);
            it.heavy = need != null && need != TerrainAffordanceDefOf.Light;
            it.kind = KindOf(def);
            return it;
        }

        /// <summary>A blueprint or frame: a fixed block over the cells it will take.</summary>
        private static RoomArrangeMath.Item BlockFor(Thing t, CellRect rect)
        {
            var occ = t.OccupiedRect();
            return new RoomArrangeMath.Item
            {
                id = t.thingIDNumber,
                sizeX = occ.Width,
                sizeZ = occ.Height,
                rotatable = false,
                curX = occ.minX + (occ.Width - 1) / 2 - rect.minX,
                curZ = occ.minZ + (occ.Height - 1) / 2 - rect.minZ,
                fixedPlace = true
            };
        }

        /// <summary>What a piece is for, as far as its place goes (<see cref="RoomArrangeMath"/>'s kinds).</summary>
        public static int KindOf(ThingDef def)
        {
            if (def.thingClass != null && typeof(Building_Art).IsAssignableFrom(def.thingClass)) return RoomArrangeMath.Decor;
            // A sun lamp lights the whole room from its middle; a plain lamp stands in a corner.
            if (Avail.IsLight(def) && WallLights.GlowRadius(def) >= 10f) return RoomArrangeMath.Centre;
            string role = CustomRooms.RoleOf(def);
            switch (role)
            {
                case "medical":
                case "crib":
                case "bed":
                case "doublebed":
                case "animalbed":
                    return RoomArrangeMath.Bed;
                case "endtable":
                case "monitor":
                    return RoomArrangeMath.BedSide;
                case "table":
                    return RoomArrangeMath.Table;
                case "chair":
                    return RoomArrangeMath.Chair;
                case "research":
                case "cook":
                case "butcher":
                case "stonecutter":
                case "fabrication":
                case "tailor":
                case "drugs":
                case "craft":
                case "art":
                case "crematorium":
                case "school":
                    return RoomArrangeMath.Bench;
                case "storage":
                case "bookcase":
                case "genebank":
                    return RoomArrangeMath.Storage;
                case "plantpot":
                case "light":
                case "pedestal":
                case "column":
                    return RoomArrangeMath.Decor;
                case "joy":
                    return RoomArrangeMath.Centre;
                case null:
                    break;
                default:
                    return RoomArrangeMath.WallHugger;
            }
            if (def.hasInteractionCell || !def.multipleInteractionCellOffsets.NullOrEmpty()) return RoomArrangeMath.Bench;
            if (def.size.x == 1 && def.size.z == 1 && RoomTemplateDef.IsPassable(def)) return RoomArrangeMath.Decor;
            return RoomArrangeMath.WallHugger;
        }

        /// <summary>Gives the orders: each move to the queue, in an order that frees cells as it goes.</summary>
        public static void Apply(RearrangePlan plan)
        {
            if (plan == null || plan.map == null) return;
            var items = plan.pieces.Select(p => p.item).ToList();
            var order = RoomArrangeMath.MoveOrder(items, out var setAside);
            var queue = plan.map.GetComponent<RearrangeQueue>();
            if (queue == null) return;
            int n = 0;
            foreach (int i in order)
            {
                var p = plan.pieces[i];
                if (!p.Moves || p.thing == null) continue;
                queue.Add(p.thing, p.to, p.toRot, setAside.Contains(i), plan.roomLabel);
                n++;
            }
            if (n == 0) return;
            queue.Work();
            Messages.Message("CC_Rearr_Started".Translate(plan.roomLabel, n), new LookTargets(plan.rect.CenterCell, plan.map), MessageTypeDefOf.TaskCompletion, false);
        }
    }

    /// <summary>One move still to be carried out: a piece, where it goes, and how far it has got.</summary>
    public class RearrangeOrder : IExposable
    {
        public Thing thing;
        public IntVec3 cell;
        public Rot4 rot;
        /// <summary>Picked up first, so that its cells free up for the others (its own new place is still taken).</summary>
        public bool aside;
        public int tick;
        /// <summary>Its reinstall order has been placed once: if it's gone, the player cancelled it.</summary>
        public bool placed;
        /// <summary>An uninstall order was given by the council, to be taken back if the move is called off.</summary>
        public bool asked;
        public string room;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref aside, "aside");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref placed, "placed");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref room, "room");
        }
    }

    /// <summary>
    /// Carries a room's moves out with the game's own orders: a reinstall order for each piece as soon as its new
    /// cells are clear, an uninstall first for a piece that has to get out of the others' way, and nothing at all
    /// for a piece the player has since given other orders. Runs every 250 ticks while anything waits.
    /// </summary>
    public class RearrangeQueue : MapComponent
    {
        public List<RearrangeOrder> orders = new List<RearrangeOrder>();

        private const int MaxAgeTicks = GenDate.TicksPerDay * 15;
        /// <summary>A move blocked by something that isn't the room's own furniture is given up after this long.</summary>
        private const int BlockedTicks = GenDate.TicksPerDay * 2;

        public RearrangeQueue(Map map) : base(map) { }

        public int Count => orders.Count;

        public void Add(Thing thing, IntVec3 cell, Rot4 rot, bool aside, string room)
        {
            orders.Add(new RearrangeOrder { thing = thing, cell = cell, rot = rot, aside = aside, tick = Find.TickManager.TicksGame, room = room });
        }

        /// <summary>Moves still under way for pieces inside <paramref name="rect"/>.</summary>
        public int CountIn(CellRect rect) => orders.Count(o => rect.Contains(o.cell));

        /// <summary>Calls the moves inside <paramref name="rect"/> off: their orders are taken back, what has moved stays moved.</summary>
        public void Cancel(CellRect rect)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                var o = orders[i];
                if (!rect.Contains(o.cell)) continue;
                try
                {
                    if (o.thing != null && !o.thing.Destroyed)
                    {
                        if (o.placed) InstallBlueprintUtility.CancelBlueprintsFor(o.thing);
                        if (o.asked && o.thing.Spawned)
                        {
                            var d = map.designationManager.DesignationOn(o.thing, DesignationDefOf.Uninstall);
                            if (d != null) map.designationManager.RemoveDesignation(d);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("calling a move off", e);
                }
                orders.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                orders ??= new List<RearrangeOrder>();
                orders.RemoveAll(o => o == null || o.thing == null);
            }
        }

        public override void MapComponentTick()
        {
            if (orders.Count == 0 || !Sched.Every(Find.TickManager.TicksGame, Sched.RareTicks, Sched.RareRearrange)) return;
            Work();
        }

        /// <summary>One pass over the moves: places what can be placed, picks up what is in the way, gives up what can't be done.</summary>
        public void Work()
        {
            try
            {
                int now = Find.TickManager.TicksGame;
                var dm = map.designationManager;
                var finished = new HashSet<string>();
                for (int i = orders.Count - 1; i >= 0; i--)
                {
                    var o = orders[i];
                    var t = o.thing;
                    if (t == null || t.Destroyed || now - o.tick > MaxAgeTicks)
                    {
                        Drop(i, finished);
                        continue;
                    }
                    bool here = t.Spawned && t.Map == map;
                    if (here && t.Position == o.cell && t.Rotation == o.rot)
                    {
                        Drop(i, finished);
                        continue;
                    }
                    var bp = InstallBlueprintUtility.ExistingBlueprintFor(t);
                    if (bp != null)
                    {
                        // Under way; unless the player has sent it somewhere else, which is their call.
                        if (bp.Position != o.cell || bp.Rotation != o.rot) Drop(i, finished);
                        continue;
                    }
                    if (o.placed)
                    {
                        // Its order is gone and it isn't there: the player cancelled it.
                        Drop(i, finished);
                        continue;
                    }
                    var mini = t.holdingOwner?.Owner as MinifiedThing;
                    if (!here && mini == null)
                    {
                        Drop(i, finished);
                        continue;
                    }
                    if (GenConstruct.CanPlaceBlueprintAt(t.def, o.cell, o.rot, map, false, t, t, t.Stuff).Accepted)
                    {
                        if (here)
                        {
                            // The reinstall order does the picking up itself.
                            var d = dm.DesignationOn(t, DesignationDefOf.Uninstall);
                            if (d != null) dm.RemoveDesignation(d);
                            GenConstruct.PlaceBlueprintForReinstall((Building)t, o.cell, map, o.rot, Faction.OfPlayer);
                        }
                        else GenConstruct.PlaceBlueprintForInstall(mini, o.cell, map, o.rot, Faction.OfPlayer);
                        o.placed = true;
                        continue;
                    }
                    // Blocked. By the room's own furniture still standing there?
                    Building blocker = null;
                    bool busy = false;
                    foreach (var c in GenAdj.OccupiedRect(o.cell, o.rot, t.def.size))
                    {
                        if (!c.InBounds(map)) continue;
                        foreach (var th in c.GetThingList(map))
                        {
                            if (th == t || !(th is Building b) || !orders.Any(x => x.thing == b)) continue;
                            blocker ??= b;
                            if (InstallBlueprintUtility.ExistingBlueprintFor(b) != null || dm.DesignationOn(b, DesignationDefOf.Uninstall) != null) busy = true;
                        }
                    }
                    if (blocker != null)
                    {
                        // Pieces standing on each other's new places: pick one up so the others can go.
                        if (!busy && (o.aside || now - o.tick > Sched.HourTicks)) AskUninstall(here && o.aside ? (Building)t : blocker);
                        continue;
                    }
                    if (o.aside && here && !o.asked)
                    {
                        AskUninstall((Building)t);
                        continue;
                    }
                    if (now - o.tick > BlockedTicks)
                    {
                        var why = GenConstruct.CanPlaceBlueprintAt(t.def, o.cell, o.rot, map, false, t, t, t.Stuff);
                        Messages.Message("CC_Rearr_GaveUp".Translate(t.LabelCap, why.Reason ?? ""), new LookTargets(o.cell, map), MessageTypeDefOf.NeutralEvent, false);
                        Drop(i, finished);
                    }
                }
                foreach (var room in finished)
                    if (!orders.Any(o => o.room == room))
                        Messages.Message("CC_Rearr_Finished".Translate(room), MessageTypeDefOf.TaskCompletion, false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("rearranging", e);
            }
        }

        private void Drop(int i, HashSet<string> finished)
        {
            if (!orders[i].room.NullOrEmpty()) finished.Add(orders[i].room);
            orders.RemoveAt(i);
        }

        private void AskUninstall(Building b)
        {
            if (b == null || !b.Spawned || b.Faction != Faction.OfPlayer) return;
            var dm = map.designationManager;
            if (dm.DesignationOn(b, DesignationDefOf.Uninstall) != null || dm.DesignationOn(b, DesignationDefOf.Deconstruct) != null) return;
            dm.AddDesignation(new Designation(b, DesignationDefOf.Uninstall));
            foreach (var o in orders)
                if (o.thing == b) o.asked = true;
        }
    }
}
