using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// With Hospitality, visitors given a bed or a roof for the night (VisitEffect_Stay inside, without a room the words
    /// name, as a barn) sleep in the colony's guest beds (Hospitality's Building_GuestBed, found by its class name, so it
    /// does nothing without the mod): they stay in the room with the most free guest beds, if it's warm enough, and each
    /// claims a free bed there as the game claims a bed (it shows their name), lies down in it at night, and gives it back
    /// when they go. Never a bed anyone else has, and no fee: the colony gave them the night. With no free guest bed they
    /// sleep in a corner of the dining room or rec room, as before.
    /// </summary>
    public static class VisitorBeds
    {
        private static readonly Dictionary<int, int> gaveAt = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> noBedUntil = new Dictionary<int, int>();
        private static Game bedGame;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The words leave the bed to the colony (a bed for the night, a roof, the guest corner), rather than naming a room.</summary>
        public static bool For(VisitorTrip t) => t != null && t.stayInside && t.stayRoom.NullOrEmpty();

        /// <summary>The colony's guest beds indoors that these people may have: whole, with a place free, and nobody's but theirs.</summary>
        public static IEnumerable<Building_Bed> Free(Map map, ICollection<Pawn> ours)
        {
            foreach (var b in Guests.Beds(map))
            {
                if (b.IsBurning() || b.Medical || b.ForPrisoners || b.def.building?.bed_humanlike != true) continue;
                if (!b.AnyUnownedSleepingSlot || b.OwnersForReading.Any(o => o != null && !ours.Contains(o))) continue;
                var room = b.GetRoom();
                if (room == null || room.PsychologicallyOutdoors || room.IsPrisonCell) continue;
                yield return b;
            }
        }

        /// <summary>
        /// A spot in the room with the most free guest beds for them, warm enough for the night for every one of them and
        /// in their reach, or invalid when there's none (or Hospitality isn't there).
        /// </summary>
        public static IntVec3 RoomCell(VisitorTrip t)
        {
            var map = t?.map;
            var party = t?.Here;
            if (map == null || party.NullOrEmpty() || !For(t)) return IntVec3.Invalid;
            try
            {
                var rooms = Free(map, t.people).GroupBy(b => b.GetRoom()).OrderByDescending(g => g.Sum(b => b.SleepingSlotsCount - b.OwnersForReading.Count));
                foreach (var g in rooms)
                {
                    var room = g.Key;
                    var middle = room.ExtentsClose.CenterCell;
                    foreach (var c in room.Cells.OrderBy(x => x.DistanceToSquared(middle)).Take(30))
                    {
                        if (!c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null) continue;
                        if (!Visitors.SafeOvernight(party, c, map, 0f)) break;
                        if (party.All(p => p.CanReach(c, PathEndMode.OnCell, Danger.Some))) return c;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors' guest beds", e);
            }
            return IntVec3.Invalid;
        }

        /// <summary>The guest bed this visitor has for the night, if it still stands on their map.</summary>
        public static Building_Bed Owned(Pawn p)
        {
            var b = p?.ownership?.OwnedBed;
            return b != null && b.Spawned && b.Map == p.Map && GuestBeds.IsGuestBed(b) ? b : null;
        }

        /// <summary>A free guest bed each, for the night, claimed as the game claims a bed: those in the room they keep to first.</summary>
        public static void Claim(VisitorTrip t)
        {
            if (!For(t) || t.map == null) return;
            var room = t.spot.IsValid ? t.spot.GetRoom(t.map) : null;
            foreach (var p in t.Standing)
            {
                try
                {
                    if (Owned(p) == null) ClaimOne(p, t, room);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor claims a guest bed", e);
                }
            }
        }

        private static Building_Bed ClaimOne(Pawn p, VisitorTrip t, Room room, bool onlyThere = false)
        {
            if (p.ownership == null || onlyThere && room == null) return null;
            var bed = Free(t.map, t.people)
                .Where(b => (!onlyThere || b.GetRoom() == room) && RestUtility.CanUseBedEver(p, b.def) && p.CanReserveAndReach(b, PathEndMode.OnCell, Danger.Some, b.SleepingSlotsCount, 0))
                .OrderBy(b => room != null && b.GetRoom() == room ? 0 : 1).ThenBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            // Only a place free: the game's claiming would turn someone else out of a full bed.
            if (bed == null || !bed.AnyUnownedSleepingSlot || !p.ownership.ClaimBedIfNonMedical(bed)) return null;
            return bed;
        }

        /// <summary>
        /// Time for bed indoors: lying down in their guest bed (claiming one now if they had none). Null without one, or for
        /// a while after lying down in it didn't work out: then they sleep in their corner as before.
        /// </summary>
        public static Job SleepJob(Pawn p)
        {
            var t = VisitorTracker.Instance?.TripOf(p);
            if (!For(t) || p.Map != t.map) return null;
            if (bedGame != Current.Game)
            {
                bedGame = Current.Game;
                gaveAt.Clear();
                noBedUntil.Clear();
            }
            int id = p.thingIDNumber, now = Now;
            if (noBedUntil.TryGetValue(id, out int until) && now < until) return null;
            // Sent to bed a moment ago and not lying in it: the bed wouldn't have them (another mod's rule), so the floor will do.
            if (gaveAt.TryGetValue(id, out int given) && now - given < 600 && !p.InBed())
            {
                noBedUntil[id] = now + GenDate.TicksPerHour * 8;
                return null;
            }
            // One taken since in the room they keep to: claimed now (never one elsewhere, that the night's warmth wasn't looked at for).
            var bed = Owned(p) ?? ClaimOne(p, t, t.spot.IsValid ? t.spot.GetRoom(t.map) : null, true);
            if (bed == null || !p.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some, bed.SleepingSlotsCount, 0)) return null;
            if (gaveAt.Count > 200) gaveAt.Clear();
            gaveAt[id] = now;
            return JobMaker.MakeJob(JobDefOf.LayDown, bed);
        }

        /// <summary>The guest beds they had go back to the colony (they're leaving, or have joined it).</summary>
        public static void GiveBack(VisitorTrip t)
        {
            if (t == null) return;
            foreach (var p in t.people) GiveBack(p);
        }

        public static void GiveBack(Pawn p)
        {
            try
            {
                var bed = p?.ownership?.OwnedBed;
                if (bed != null && GuestBeds.IsGuestBed(bed)) p.ownership.UnclaimBed();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor gives a guest bed back", e);
            }
        }
    }
}
