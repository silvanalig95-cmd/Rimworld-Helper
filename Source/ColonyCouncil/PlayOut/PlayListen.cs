using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Words said to a gathering (a lesson, a story, a letter read out): the speaker stands at the head of the table the
    /// gathering is at, and the listeners take the chairs round it, facing the speaker; those without a chair stand close
    /// by. With no table near, they stand in a ring round the speaker. The game's party toil underneath still counts who
    /// was there, and their recreation.
    /// </summary>
    public class LordToil_PlayOutListen : LordToil_Party
    {
        private readonly IntVec3 spot;
        private bool placed;
        private CellRect stage;
        private IntVec3 head = IntVec3.Invalid;

        public LordToil_PlayOutListen(IntVec3 spot, GatheringDef gatheringDef) : base(spot, gatheringDef, 1.5E-05f)
        {
            this.spot = spot;
        }

        /// <summary>Where the speaker stands: the head of the table, or the gathering's spot.</summary>
        public IntVec3 Head
        {
            get
            {
                Place();
                return head;
            }
        }

        /// <summary>The table (or the speaker's cell): what the listeners sit or stand round.</summary>
        public CellRect Stage
        {
            get
            {
                Place();
                return stage;
            }
        }

        private void Place()
        {
            if (placed || Map == null) return;
            placed = true;
            stage = CellRect.SingleCell(spot);
            head = spot;
            var table = PlayListen.TableNear(spot, Map);
            if (table == null) return;
            var h = PlayListen.HeadOf(table, Map, spot);
            if (!h.IsValid) return;
            stage = table.OccupiedRect();
            head = h;
        }

        public override void UpdateAllDuties()
        {
            Place();
            var speaker = (lord.LordJob as LordJob_PlayOut)?.ColonistSpeaker;
            foreach (var p in lord.ownedPawns)
            {
                if (p == speaker)
                    p.mindState.duty = new PawnDuty(PlayOutDefOf.Council_PlayOutSpeakDuty, head) { focusSecond = stage.CenterCell };
                else
                    p.mindState.duty = new PawnDuty(PlayOutDefOf.Council_PlayOutListenDuty, head) { focusSecond = stage.CenterCell, spectateRect = stage };
            }
        }
    }

    /// <summary>Seats, standing room and the head of the table for <see cref="LordToil_PlayOutListen"/>.</summary>
    public static class PlayListen
    {
        /// <summary>How far from the spot a table may be and still be the one the gathering is at.</summary>
        private const float TableRadius = 6.9f;

        /// <summary>One of the colony's tables near the spot, in the same room when the spot is indoors.</summary>
        public static Thing TableNear(IntVec3 spot, Map map)
        {
            var room = spot.GetRoom(map);
            bool indoors = room != null && !room.PsychologicallyOutdoors;
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (var t in GenRadial.RadialDistinctThingsAround(spot, map, TableRadius, true))
            {
                if (t.def?.surfaceType != SurfaceType.Eat || t.Faction != Faction.OfPlayer || t is Frame) continue;
                if (indoors && t.GetRoom() != room) continue;
                float d = t.Position.DistanceToSquared(spot);
                if (d < bestDist)
                {
                    best = t;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Where the speaker stands at a table: beside one of its short ends if a cell there is free (no chair, no wall),
        /// else beside any side, the one nearest the gathering's spot first. Invalid if the table is hemmed in.
        /// </summary>
        public static IntVec3 HeadOf(Thing table, Map map, IntVec3 spot)
        {
            var r = table.OccupiedRect();
            var ends = new List<IntVec3>();
            var sides = new List<IntVec3>();
            bool wide = r.Width >= r.Height;
            foreach (var c in r.ExpandedBy(1).EdgeCells)
            {
                if (r.Contains(c)) continue;
                bool corner = (c.x < r.minX || c.x > r.maxX) && (c.z < r.minZ || c.z > r.maxZ);
                if (corner) continue;
                bool atEnd = wide ? c.x < r.minX || c.x > r.maxX : c.z < r.minZ || c.z > r.maxZ;
                (atEnd ? ends : sides).Add(c);
            }
            foreach (var group in new[] { ends, sides })
                foreach (var c in group.OrderBy(x => x.DistanceToSquared(spot)))
                    if (FreeToStand(c, map)) return c;
            return IntVec3.Invalid;
        }

        /// <summary>A cell someone can stand in for a while: standable, no chair or bed, not a door.</summary>
        private static bool FreeToStand(IntVec3 c, Map map)
        {
            if (!c.InBounds(map) || !c.Standable(map)) return false;
            var e = c.GetEdifice(map);
            return e == null || !(e.def.building?.isSittable ?? false) && !(e is Building_Door) && !(e is Building_Bed);
        }

        /// <summary>A free chair round the stage for a listener: one beside the table first, then any within a few cells.</summary>
        public static Thing SeatFor(Pawn pawn, CellRect stage, IntVec3 head, Map map)
        {
            Thing best = null;
            int bestScore = int.MaxValue;
            var near = stage.ExpandedBy(3);
            foreach (var c in near)
            {
                if (!c.InBounds(map) || stage.Contains(c) || c == head) continue;
                var e = c.GetEdifice(map);
                if (e == null || !(e.def.building?.isSittable ?? false) || e.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveSittableOrSpot(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                // The seat they already have first, so nobody changes places each time the gathering asks again.
                int score = c == pawn.Position ? -1 : DistanceTo(stage, c) * 100 + c.DistanceToSquared(head);
                if (score < bestScore)
                {
                    best = e;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>A free cell close to the stage to stand in, facing it: next to it if there's room, else a step or two back.</summary>
        public static IntVec3 StandFor(Pawn pawn, CellRect stage, IntVec3 head, Map map)
        {
            var best = IntVec3.Invalid;
            float bestScore = float.MaxValue;
            foreach (var c in stage.ExpandedBy(3))
            {
                if (stage.Contains(c) || c == head || !FreeToStand(c, map)) continue;
                int d = DistanceTo(stage, c);
                if (d < 1) continue;
                if (!pawn.CanReserve(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float score = c == pawn.Position ? -1f : d * 100 + c.DistanceToSquared(head) + Rand.Value;
                if (score < bestScore)
                {
                    best = c;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>Steps from the rect to the cell (0 inside it).</summary>
        private static int DistanceTo(CellRect r, IntVec3 c)
        {
            int dx = c.x < r.minX ? r.minX - c.x : c.x > r.maxX ? c.x - r.maxX : 0;
            int dz = c.z < r.minZ ? r.minZ - c.z : c.z > r.maxZ ? c.z - r.maxZ : 0;
            return Mathf.Max(dx, dz);
        }

        /// <summary>The speaker's job: to the head of the table, and stand there facing the listeners.</summary>
        public static Job SpeakJob(IntVec3 head, IntVec3 face)
        {
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutSpeak, head, face);
            job.count = 1250;
            return job;
        }
    }

    /// <summary>A listener's duty: a chair round the table, or a place to stand by it, facing the speaker.</summary>
    public class JobGiver_PlayOutListen : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var duty = pawn.mindState?.duty;
            var map = pawn.Map;
            if (duty == null || map == null) return null;
            var stage = duty.spectateRect;
            var head = duty.focus.Cell;
            var face = head.IsValid && !stage.Contains(head) ? head : stage.CenterCell;
            var seat = PlayListen.SeatFor(pawn, stage, head, map);
            Job job;
            if (seat != null) job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutListen, seat.Position, face, seat);
            else
            {
                var cell = PlayListen.StandFor(pawn, stage, head, map);
                if (!cell.IsValid) return null;
                job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutListen, cell, face);
            }
            job.count = 1250;
            return job;
        }
    }

    /// <summary>The speaker's duty (a colonist): stand at the head of the table.</summary>
    public class JobGiver_PlayOutSpeak : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var duty = pawn.mindState?.duty;
            if (duty == null || !duty.focus.IsValid) return null;
            return PlayListen.SpeakJob(duty.focus.Cell, duty.focusSecond.IsValid ? duty.focusSecond.Cell : duty.focus.Cell);
        }
    }

    /// <summary>Sitting (or standing) to listen: to the seat or cell (A), facing B, a while; the gathering gives it again.</summary>
    public class JobDriver_PlayOutListen : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job.targetC.HasThing) return pawn.ReserveSittableOrSpot(job.targetA.Cell, job, errorOnFailed);
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport() => "CC_Play_Report_Listen".Translate();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var listen = ToilMaker.MakeToil("PlayOutListen");
            listen.defaultCompleteMode = ToilCompleteMode.Delay;
            listen.defaultDuration = Mathf.Max(300, job.count);
            listen.socialMode = RandomSocialMode.Quiet;
            listen.handlingFacing = true;
            listen.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetB.Cell);
            yield return listen;
        }
    }

    /// <summary>Standing at the head of the table (A) to speak, facing the listeners (B), a while; given again while it lasts.</summary>
    public class JobDriver_PlayOutSpeak : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Play_Report_Speak".Translate();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var speak = ToilMaker.MakeToil("PlayOutSpeak");
            speak.defaultCompleteMode = ToilCompleteMode.Delay;
            speak.defaultDuration = Mathf.Max(300, job.count);
            speak.socialMode = RandomSocialMode.Off;
            speak.handlingFacing = true;
            speak.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetB.Cell);
            yield return speak;
        }
    }
}
