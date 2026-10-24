using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Answers that change someone's room: a thing set in its corner (a coffin made ready, a fire kept), and two
    /// colonists trading rooms. A thing goes only where it's out of the way: against a wall, clear of the door and the bed.
    /// </summary>
    public static class OnMapRooms
    {
        /// <summary>
        /// A free place for <paramref name="def"/> in the room, turned <paramref name="rot"/>: every cell of it inside the
        /// room, against a wall, clear of the bed's sides and of the door, where the game would let it be built. The most
        /// tucked away first (a corner before a wall). Invalid when there's none.
        /// </summary>
        public static IntVec3 FreeCorner(Room room, Map map, ThingDef def, Rot4 rot)
        {
            if (room == null || map == null || def == null) return IntVec3.Invalid;
            IntVec3 best = IntVec3.Invalid;
            int bestScore = -1;
            foreach (var c in room.Cells)
            {
                int score = Fits(room, map, def, c, rot);
                if (score > bestScore)
                {
                    best = c;
                    bestScore = score;
                }
            }
            return bestScore >= 0 ? best : IntVec3.Invalid;
        }

        /// <summary>How tucked away the thing would be here (walls touching it), or -1 when it doesn't fit.</summary>
        private static int Fits(Room room, Map map, ThingDef def, IntVec3 c, Rot4 rot)
        {
            var rect = GenAdj.OccupiedRect(c, rot, def.size);
            int walls = 0;
            foreach (var x in rect)
            {
                if (!x.InBounds(map) || x.GetRoom(map) != room || !x.Standable(map) || x.GetEdifice(map) != null || x.GetFirstPawn(map) != null) return -1;
                bool wall = false;
                foreach (var n in GenAdj.CellsAdjacentCardinal(x, Rot4.North, IntVec2.One))
                {
                    if (!n.InBounds(map) || rect.Contains(n)) continue;
                    var e = n.GetEdifice(map);
                    if (e is Building_Door) return -1;
                    if (e is Building_Bed) return -1;
                    if (e != null && e.def.passability == Traversability.Impassable)
                    {
                        wall = true;
                        walls++;
                    }
                }
                if (!wall) return -1;
                foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(x, map)))
                    if (n.InBounds(map) && n.GetDoor(map) != null) return -1;
            }
            if (!GenConstruct.CanPlaceBlueprintAt(def, c, rot, map).Accepted) return -1;
            return walls;
        }

        /// <summary>
        /// Sets a thing made for someone in a corner of their own bedroom (a room nobody else sleeps in): a building stands
        /// there, built (a coffin is theirs, assigned to them as a grave would be); one that can't stand there, or a thing
        /// that's no building, is set down in the room, packed if it's furniture. Null when they have no room of their own
        /// or nowhere in it will do (the caller sets it down beside them instead).
        /// </summary>
        public static Thing PlaceInRoom(Thing thing, Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            var map = room?.Map;
            if (thing == null || map == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            if (room.ContainedBeds.Any(b => b.OwnersForReading.Any(o => o != p && !ArcUtil.Partners(p, o)))) return null;
            var def = thing.def;
            if (def.category == ThingCategory.Building && Avail.CanBuild(def, map))
            {
                foreach (var rot in new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West })
                {
                    if (!def.rotatable && rot != Rot4.North) break;
                    var c = FreeCorner(room, map, def, rot);
                    if (!c.IsValid) continue;
                    thing.SetFaction(Faction.OfPlayer);
                    var built = GenSpawn.Spawn(thing, c, map, rot, WipeMode.VanishOrMoveAside);
                    if (built is Building_Grave grave && p.ownership != null && p.ownership.AssignedGrave == null)
                    {
                        try
                        {
                            p.ownership.ClaimGrave(grave);
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("coffin claimed", e);
                        }
                    }
                    return built;
                }
            }
            if (def.category == ThingCategory.Building && !def.Minifiable) return null;
            var item = def.Minifiable && !(thing is MinifiedThing) ? thing.MakeMinified() : thing;
            var spots = room.Cells.Where(x => x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null)
                .OrderByDescending(x => GenAdj.CellsAdjacentCardinal(x, Rot4.North, IntVec2.One).Count(n => n.InBounds(map) && n.GetEdifice(map)?.def.passability == Traversability.Impassable))
                .Take(1).ToList();
            if (spots.Count == 0) return null;
            return GenPlace.TryPlaceThing(item, spots[0], map, ThingPlaceMode.Near, out var lying) ? lying ?? item : null;
        }
    }

    /// <summary>
    /// B gives A the warmer room: they trade beds, each to the other's room, when each sleeps alone in their bed and B's
    /// room really is the warmer (or no colder). Each sleeps in the other's bed from tonight. Anything else (a shared bed,
    /// a sickbed, a bed one of them may not have): the words alone.
    /// </summary>
    public class DilemmaEffect_SwapRooms : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var b = ctx?.b;
            if (a == null || b == null || a == b || a.Dead || b.Dead) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var bedA = a.ownership?.OwnedBed;
                var bedB = b.ownership?.OwnedBed;
                if (bedA == null || bedB == null || bedA == bedB || !bedA.Spawned || !bedB.Spawned || bedA.Map != bedB.Map) return;
                if (bedA.Medical || bedB.Medical || bedA.OwnersForReading.Count != 1 || bedB.OwnersForReading.Count != 1) return;
                var roomA = bedA.GetRoom();
                var roomB = bedB.GetRoom();
                if (roomA == null || roomB == null || roomA == roomB || !OnMapMath.Warmer(roomB.Temperature, roomA.Temperature)) return;
                var assignA = bedA.CompAssignableToPawn;
                var assignB = bedB.CompAssignableToPawn;
                if (assignA == null || assignB == null || !assignB.CanAssignTo(a).Accepted || !assignA.CanAssignTo(b).Accepted) return;
                a.ownership.UnclaimBed();
                b.ownership.UnclaimBed();
                bool ok = a.ownership.ClaimBedIfNonMedical(bedB) && b.ownership.ClaimBedIfNonMedical(bedA);
                if (!ok)
                {
                    // Put back as it was.
                    a.ownership.UnclaimBed();
                    b.ownership.UnclaimBed();
                    a.ownership.ClaimBedIfNonMedical(bedA);
                    b.ownership.ClaimBedIfNonMedical(bedB);
                    return;
                }
                // apart: the council's ruling on whose bed is whose; the walk over to the new room is the answer's own visit.
                Messages.Message("CC_SwapRooms".Translate(a.Named("PAWN"), b.Named("OTHER")).Resolve(), new LookTargets(bedB), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rooms swapped", e);
            }
        }
    }
}
