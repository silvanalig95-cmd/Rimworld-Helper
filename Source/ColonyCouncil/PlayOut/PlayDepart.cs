using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Departures an answer's words put at the end of a scene rather than the moment it's given. Someone who leaves for
    /// good goes from the gate at the end of the walk that sees them off (<see cref="PlayOut.leaving"/> on a walk). A
    /// visitor sent away sets off when the one who walks them back to the road comes to them, and goes with them
    /// (<see cref="PlayOut.leaving"/> on a visit). Animals turned loose or out are led along at the heels of the one
    /// visiting or the walk's lead and let go at the far end (<see cref="PlayOut.led"/>). If the scene never comes
    /// about, whatever it held happens anyway when it falls through, as the answer always had it.
    /// </summary>
    public static class PlayDepart
    {
        /// <summary>Someone leaving for good, seen off by a walk the answer being given set going: they lead it, and go at its end.</summary>
        public static bool HoldLeaving(Pawn p)
        {
            if (!PlayOuts.On || p == null) return false;
            var walk = PlayOuts.Collected().FirstOrDefault(x => x.kind == PlayKind.Procession && x.state == PlayState.Pending && x.leaving == null);
            if (walk == null) return false;
            walk.leaving = p;
            walk.invited.Remove(p);
            walk.invited.Insert(0, p);
            if (!walk.named.Contains(p)) walk.named.Add(p);
            walk.organizer = p;
            return true;
        }

        /// <summary>
        /// A visitor sent away, with a visit to them the answer being given set going: they go when it's over, or with
        /// <paramref name="walked"/> (walked back to the road), set off when it starts, the visitor beside them.
        /// </summary>
        public static bool HoldSendAway(Pawn x, bool walked)
        {
            if (!PlayOuts.On || x == null) return false;
            var visit = PlayOuts.Collected().FirstOrDefault(p => p.kind == PlayKind.Visit && p.state == PlayState.Pending && p.other == x && p.leaving == null);
            if (visit == null) return false;
            visit.leaving = x;
            visit.leavingWalk = walked;
            return true;
        }

        /// <summary>Animals let go, led along by the visit or the walk the answer being given set going (a walk for a herd).</summary>
        public static bool HoldLed(List<Pawn> animals, bool herd)
        {
            if (!PlayOuts.On || animals.NullOrEmpty()) return false;
            var plays = PlayOuts.Collected();
            var p = herd
                ? plays.FirstOrDefault(x => x.kind == PlayKind.Procession && x.state == PlayState.Pending)
                : plays.FirstOrDefault(x => x.kind == PlayKind.Visit && x.state == PlayState.Pending && PlayOuts.OurPerson(x.visitor));
            if (p == null) return false;
            p.led.AddRange(animals.Where(a => a != null && !p.led.Contains(a)));
            p.ledHerd = herd;
            return true;
        }

        /// <summary>It has started: the one sent away sets off with the visitor beside them, the animals fall in behind.</summary>
        public static void Started(PlayOut p)
        {
            if (p == null) return;
            if (p.kind == PlayKind.Visit && p.leavingWalk && p.leaving != null && !p.leavingGone && !p.leaving.Dead && p.leaving.Spawned)
            {
                // together: the visitor is on their way to them, and keeps them company to the edge (the visit's own job).
                DilemmaEffect_SendAway.Send(p.leaving);
                p.leavingGone = true;
            }
            Keep(p);
        }

        /// <summary>Each look while it runs: the animals keep at the heels of the one leading them.</summary>
        public static void Keep(PlayOut p)
        {
            if (p == null || p.led.Count == 0) return;
            var lead = p.kind == PlayKind.Visit ? p.visitor : p.hands.FirstOrDefault(h => !h.left && h.pawn != null && h.pawn.Spawned)?.pawn;
            if (lead == null || !lead.Spawned) return;
            foreach (var a in p.led)
            {
                if (a == null || a.Dead || !a.Spawned || a.Map != lead.Map || a.Downed || a.InMentalState || a.Faction != Faction.OfPlayer) continue;
                if (a.CurJobDef == JobDefOf.FollowClose && a.CurJob.targetA.Thing == lead) continue;
                a.roping?.BreakAllRopes();
                var job = JobMaker.MakeJob(JobDefOf.FollowClose, lead);
                job.followRadius = 3f;
                job.expiryInterval = 1500;
                job.checkOverrideOnExpire = true;
                a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }

        /// <summary>The walk has reached its far end: the one leaving goes from here, and the animals are let go.</summary>
        public static void Arrived(PlayOut p)
        {
            if (p == null) return;
            if (p.leaving != null && !p.leavingGone)
            {
                p.leavingGone = true;
                DilemmaEffect_LeaveForGood.Leave(p.leaving);
            }
            LetGo(p);
        }

        /// <summary>It's over, or never came about: whatever it held back happens now, wherever they are.</summary>
        public static void Over(PlayOut p)
        {
            if (p == null) return;
            try
            {
                if (p.leaving != null && !p.leavingGone)
                {
                    p.leavingGone = true;
                    if (p.kind == PlayKind.Procession) DilemmaEffect_LeaveForGood.Leave(p.leaving);
                    else DilemmaEffect_SendAway.Send(p.leaving);
                }
                LetGo(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out departure", e);
            }
        }

        private static void LetGo(PlayOut p)
        {
            if (p.led.Count == 0) return;
            var led = p.led.Where(a => a != null && !a.Dead && a.Faction == Faction.OfPlayer).ToList();
            p.led.Clear();
            int now = Find.TickManager.TicksGame;
            foreach (var a in led)
            {
                if (a.Spawned) a.jobs?.StopAll();
                a.roping?.BreakAllRopes();
                a.SetFaction(null);
                // They move on off the land within hours, as a beast passing through would (the game's own timed leaving).
                if (a.Spawned && a.mindState != null && !a.RaceProps.Humanlike)
                    a.mindState.exitMapAfterTick = now + (p.ledHerd ? Rand.Range(GenDate.TicksPerHour * 3, GenDate.TicksPerDay) : Rand.Range(GenDate.TicksPerHour, GenDate.TicksPerHour * 4));
            }
            if (p.ledHerd && led.Count > 0)
                Messages.Message("CC_Dlm_TurnedOut".Translate(led.Count).Resolve(), new LookTargets(led.Where(a => a.Spawned)), MessageTypeDefOf.NegativeEvent, false);
        }

        /// <summary>The visit to someone sent away counts once they've set off together and the visitor has gone a little way with them.</summary>
        public static bool WalkedOff(PlayOut p, int stayed) => p.leaving != null && p.leavingWalk && p.leavingGone && stayed >= 250;
    }
}
