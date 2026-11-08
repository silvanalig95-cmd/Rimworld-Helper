using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the colony and a camp hand things over (the player's report, 8 November 2026: "i decided to pay them and my
    /// pawn started walking to the other side of the map ... my pawn should pay them once they are closer. these thoughts
    /// must be applied to all such kinds of events"). Whatever the colony carries to a camp (silver paid, meals, a gift,
    /// provisions) goes to the camp's fire only while the camp is near; a camp further off sends one of its people to the
    /// colony's gate to meet it, and the colonist walks only that far. The camp's own goods for the colony already come
    /// to the gate (a load of wood bought, a share of the harvest); a camper who brings them takes the payment there.
    /// </summary>
    public static class CampMeet
    {
        /// <summary>The camper on the way to the gate, by camp, and when they were sent (for this session only).</summary>
        private static readonly Dictionary<int, (Pawn pawn, int tick)> sent = new Dictionary<int, (Pawn, int)>();

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>
        /// For a carry to the camp: true when they meet at the gate (the camp is far). <paramref name="met"/> is a camper at
        /// the gate to take it, or null while one is on the way (the errand waits). False: the camp is near, or nobody of it
        /// can come, and the carry goes to its fire as before.
        /// </summary>
        public static bool AtGate(Map map, Pawn carrier, Pawn other, out Pawn met)
        {
            met = null;
            try
            {
                var c = CampTracker.PlayCamp(map, other);
                if (c == null || carrier == null) return false;
                var gate = PlaySpots.GateCell(map, carrier);
                var fire = c.FireCell;
                if (!gate.IsValid || !fire.IsValid || !CampMeetMath.MeetAtGate(fire.DistanceTo(gate))) return false;
                var here = c.Here.Where(x => !x.Downed && !x.InMentalState && x.inventory != null && !x.DevelopmentalStage.Juvenile()).ToList();
                if (here.Count == 0) return false;
                // One of them at the gate already (sent, or bringing the colony's load of wood): into their hands.
                met = here.Where(x => x.Position.InHorDistOf(gate, CampMeetMath.AtGate) && carrier.CanReach(x, PathEndMode.Touch, Danger.Some))
                    .OrderBy(x => x.Position.DistanceToSquared(gate)).FirstOrDefault();
                if (met != null) return true;
                // One on the way: wait for them.
                if (sent.TryGetValue(c.id, out var s) && s.pawn != null && !s.pawn.Dead && s.pawn.Spawned && s.pawn.Map == map
                    && Now - s.tick < CampMeetMath.WaitTicks * 2) return true;
                // Send the one nearest the gate.
                var who = here.Where(x => x.CanReach(gate, PathEndMode.ClosestTouch, Danger.Some)).OrderBy(x => x.Position.DistanceToSquared(gate)).FirstOrDefault();
                if (who == null) return false;
                var spot = CellFinder.RandomClosewalkCellNear(gate, map, 3);
                var go = JobMaker.MakeJob(JobDefOf.Goto, spot);
                go.locomotionUrgency = Pace.Errand;
                var wait = JobMaker.MakeJob(JobDefOf.Wait, spot);
                wait.expiryInterval = CampMeetMath.WaitTicks;
                who.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                who.jobs.jobQueue.EnqueueLast(wait);
                sent[c.id] = (who, Now);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp meets at the gate", e);
                met = null;
                return false;
            }
        }

        /// <summary>The camp is far enough that the colony meets it at the gate (for the answers' words).</summary>
        public static bool Far(Map map, Pawn other = null)
        {
            var c = CampTracker.PlayCamp(map, other);
            if (c == null || map == null) return false;
            var gate = PlaySpots.GateCell(map, null);
            return gate.IsValid && c.FireCell.IsValid && CampMeetMath.MeetAtGate(c.FireCell.DistanceTo(gate));
        }
    }
}
