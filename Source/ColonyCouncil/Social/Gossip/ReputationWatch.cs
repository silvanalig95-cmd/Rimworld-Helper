using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The reputation's own hour (<see cref="Sched.Reputation"/>), looking at what can't be caught in one moment: during a
    /// fight, who is carrying the wounded out (a rescue under fire) and who fights beside someone known as brave (a little
    /// strain eased, through the heart's own daily cap); the rest of the time, who stands about while the others work,
    /// and who has been at the wounded's side all day. Its first hour of the day turns those into deeds: a day idled away
    /// in sight of the busy, and days of tending. What it counts is not saved: a load only starts the count again.
    /// </summary>
    public static class ReputationWatch
    {
        private static readonly Dictionary<int, int> idleHours = new Dictionary<int, int>();
        private static readonly Dictionary<int, HashSet<Pawn>> idleSeenBy = new Dictionary<int, HashSet<Pawn>>();
        private static readonly Dictionary<int, float> tendBase = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> tendScore = new Dictionary<int, float>();
        private static readonly Dictionary<int, HashSet<Pawn>> tendedBy = new Dictionary<int, HashSet<Pawn>>();

        /// <summary>A new game or a load: nothing counted carries over.</summary>
        public static void Reset()
        {
            idleHours.Clear();
            idleSeenBy.Clear();
            tendBase.Clear();
            tendScore.Clear();
            tendedBy.Clear();
            Deeds.Reset();
        }

        public static void Hourly(ReputationTracker tr)
        {
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToList())
            {
                var people = map.mapPawns.FreeColonistsSpawned.Where(Reputations.Public).ToList();
                if (people.Count == 0) continue;
                if (Deeds.FightOn(map))
                {
                    Interplay.Safe("reputation: the fight", () => Fight(people));
                    continue;
                }
                Interplay.Safe("reputation: work", () => Work(people));
                Interplay.Safe("reputation: the wounded", () => Patients(people));
            }
        }

        // ---------------- In a fight ----------------

        /// <summary>In the fight: drafted, or has gone for an enemy within the last two hours.</summary>
        private static bool InFight(Pawn p)
        {
            if (p == null || p.Downed || p.InMentalState) return false;
            if (p.Drafted) return true;
            var ms = p.mindState;
            return ms != null && Find.TickManager.TicksGame - ms.lastAttackTargetTick < GenDate.TicksPerHour * 2;
        }

        private static void Fight(List<Pawn> people)
        {
            // Carrying one of ours out while the fight is on: a rescue under fire, even if the game's own thanks never comes.
            var rescue = Deeds.Named("Rimfolk_Deed_Rescue");
            foreach (var p in people)
                if (p.CurJobDef == JobDefOf.Rescue && p.CurJob?.targetA.Thing is Pawn hurt && Reputations.Public(hurt))
                    Deeds.Record(rescue, p, hurt);
            if (!Interplay.On || !Heart.On) return;
            // together: side by side in the same fight, in sight of each other (Deeds.Sees).
            foreach (var brave in people)
            {
                float courage = Reputations.Standing(brave, RepFacet.Courage);
                float relief = RepMath.BraveRelief(courage);
                if (relief <= 0f || !InFight(brave)) continue;
                foreach (var o in people)
                {
                    if (o == brave || !InFight(o) || !Heart.Eligible(o) || !Deeds.Sees(o, brave, 12f)) continue;
                    // Once a day each: the brave are followed, not leaned on.
                    if (!Interplay.Ready("braveside|" + o.thingIDNumber, 1f)) continue;
                    Interplay.Ease(o, relief, StrainRelief.Friends);
                }
            }
        }

        // ---------------- Idling while others work ----------------

        private static bool Idle(Pawn p)
        {
            var job = p.CurJob;
            if (job == null) return p.mindState?.IsIdle ?? false;
            if (job.workGiverDef != null || job.def.joyKind != null || job.def == JobDefOf.Ingest) return false;
            return job.def == JobDefOf.Wait_Wander || job.def == JobDefOf.GotoWander || job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture;
        }

        /// <summary>
        /// Someone who could be working: grown, awake, up and about, not in bed or a break, able to work at all, and not
        /// part of a gathering, a ritual or a council meeting (waiting about is what those are for).
        /// </summary>
        private static bool CouldWork(Pawn p) =>
            !p.DevelopmentalStage.Juvenile() && p.Awake() && !p.Downed && !p.Drafted && !p.InMentalState && !p.InBed()
            && (p.workSettings?.EverWork ?? false) && p.GetLord() == null && p.timetable?.CurrentAssignment != TimeAssignmentDefOf.Joy
            && p.timetable?.CurrentAssignment != TimeAssignmentDefOf.Sleep;

        private static void Work(List<Pawn> people)
        {
            var able = people.Where(CouldWork).ToList();
            if (able.Count < 3) return;
            int working = able.Count(p => p.CurJob?.workGiverDef != null);
            foreach (var p in able)
            {
                if (!Idle(p)) continue;
                // Standing about only says something when the others are busy (most of them, not counting this one).
                if (working < (able.Count - 1) * 0.5f) continue;
                idleHours[p.thingIDNumber] = (idleHours.TryGetValue(p.thingIDNumber, out int h) ? h : 0) + 1;
                if (!idleSeenBy.TryGetValue(p.thingIDNumber, out var seen)) idleSeenBy[p.thingIDNumber] = seen = new HashSet<Pawn>();
                foreach (var w in able)
                    if (w != p && w.CurJob?.workGiverDef != null && Deeds.Sees(w, p, 13f))
                        seen.Add(w);
            }
        }

        // ---------------- Tending ----------------

        private static void Patients(List<Pawn> people)
        {
            foreach (var p in people)
            {
                if (p.CurJobDef != JobDefOf.TendPatient || !(p.CurJob?.targetA.Thing is Pawn patient) || !Reputations.Public(patient)) continue;
                if (!tendedBy.TryGetValue(p.thingIDNumber, out var set)) tendedBy[p.thingIDNumber] = set = new HashSet<Pawn>();
                set.Add(patient);
            }
        }

        // ---------------- The day ----------------

        public static void Daily(ReputationTracker tr)
        {
            var idle = Deeds.Named("Rimfolk_Deed_Idle");
            foreach (var kv in idleHours.ToList())
            {
                var p = tr.PawnOf(kv.Key);
                if (p == null || kv.Value < 5) continue;
                idleSeenBy.TryGetValue(kv.Key, out var seen);
                // Seen by nobody busy: nobody minds.
                if (seen == null || seen.Count == 0) continue;
                Deeds.Record(idle, p, null, RepMath.Clamp(kv.Value / 6f, 0.5f, 1.5f), null, null, seen, false);
            }
            idleHours.Clear();
            idleSeenBy.Clear();
            var tended = Deeds.Named("Rimfolk_Deed_Tended");
            foreach (var kv in tr.Colony.ToList())
            {
                var p = kv.Value;
                if (p.records == null) continue;
                float now = p.records.GetValue(RecordDefOf.TimesTendedOther);
                if (!tendBase.TryGetValue(kv.Key, out float before))
                {
                    tendBase[kv.Key] = now;
                    continue;
                }
                tendBase[kv.Key] = now;
                float score = (tendScore.TryGetValue(kv.Key, out float s) ? s * 0.5f : 0f) + Math.Max(0f, now - before);
                // A day or two at the wounded's side (a dozen tends or so in two days), not one patched knee.
                if (score >= 9f)
                {
                    tendedBy.TryGetValue(kv.Key, out var patients);
                    Deeds.Record(tended, p, null, RepMath.Clamp(score / 9f, 1f, 2f), null, null, patients, false);
                    score = 0f;
                    tendedBy.Remove(kv.Key);
                }
                tendScore[kv.Key] = score;
                // The stretch is over: the patients seen in it are forgotten with it.
                if (score < 1f) tendedBy.Remove(kv.Key);
            }
            foreach (var id in tendBase.Keys.Where(id => !tr.Colony.ContainsKey(id)).ToList())
            {
                tendBase.Remove(id);
                tendScore.Remove(id);
                tendedBy.Remove(id);
            }
        }
    }
}
