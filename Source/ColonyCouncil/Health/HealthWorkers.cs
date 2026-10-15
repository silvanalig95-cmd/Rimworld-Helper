using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== Moods =====================

    /// <summary>
    /// The plain mood of convalescing ("getting my strength back"). With convalescence on, the impatient feel the other
    /// one instead (<see cref="ThoughtWorker_ConvalescentFrustrated"/>), so nobody has both.
    /// </summary>
    public class ThoughtWorker_Convalescent : ThoughtWorker_Ailment
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // The hediff first: asked for every colonist, and only the convalescing need their traits read.
            var state = base.CurrentStateInternal(p);
            if (!state.Active) return state;
            if (HealthTracker.ConvalescenceOn && HealthTracker.Impatient(p)) return ThoughtState.Inactive;
            return state;
        }
    }

    /// <summary>Weak and frustrated: the industrious and the hard workers, while they're convalescing.</summary>
    public class ThoughtWorker_ConvalescentFrustrated : ThoughtWorker_Ailment
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!HealthTracker.ConvalescenceOn) return ThoughtState.Inactive;
            var state = base.CurrentStateInternal(p);
            if (!state.Active || !HealthTracker.Impatient(p)) return ThoughtState.Inactive;
            return state;
        }
    }

    // ===================== A wish =====================

    /// <summary>
    /// Laid up in bed or convalescing, someone wants a visit from a particular friend who hasn't been by. Comes true when
    /// that friend sits with them (the game's own sickbed visit, or a moment of visiting the sick). Promising it sends the
    /// friend over, if they're free to go.
    /// </summary>
    public class WishWorker_SickbedVisit : WishWorker
    {
        private static bool LaidUp(Pawn p)
        {
            if (p == null || !p.Spawned) return false;
            if (HealthTracker.ConvalescenceOf(p) != null) return true;
            try
            {
                return p.InBed() && HealthAIUtility.ShouldSeekMedicalRest(p);
            }
            catch
            {
                return false;
            }
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!HealthTracker.ConvalescenceOn || !LaidUp(pawn) || pawn.relations == null || pawn.DevelopmentalStage.Baby()) yield break;
            var tracker = HealthTracker.Instance;
            if (tracker == null) yield break;
            int now = Find.TickManager.TicksGame;
            Pawn best = null;
            int bestOpinion = 29;
            foreach (var o in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == pawn || !HealthTracker.Ours(o) || o.Downed || o.InBed() || o.InMentalState || o.DevelopmentalStage.Baby()) continue;
                int last = tracker.LastVisit(o, pawn);
                if (last >= 0 && last <= now && now - last < GenDate.TicksPerDay * 3) continue;
                int opinion = pawn.relations.OpinionOf(o);
                if (opinion > bestOpinion)
                {
                    bestOpinion = opinion;
                    best = o;
                }
            }
            if (best == null) yield break;
            var c = Candidate(1f, "CC_Reason_SickbedVisit".Translate(pawn.Named("PAWN"), best.Named("OTHER")));
            c.other = best;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var patient = wish.Beneficiary;
            var friend = wish.other;
            if (patient == null || friend == null) return false;
            int last = HealthTracker.Instance?.LastVisit(friend, patient) ?? -1;
            if (last >= wish.createdTick) return true;
            var memories = patient.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return false;
            int since = Find.TickManager.TicksGame - wish.createdTick;
            foreach (var m in memories)
                if (m.def == HealthDefOf.Council_Moment_VisitedWhileIll && m.otherPawn == friend && m.age <= since)
                    return true;
            return false;
        }

        public override bool StillValid(Wish wish)
        {
            var friend = wish.other;
            return friend != null && !friend.Dead && friend.Faction == Faction.OfPlayer && friend.MapHeld == wish.Beneficiary?.MapHeld && LaidUp(wish.Beneficiary);
        }

        public override LookTargets LookAt(Wish wish) => wish.other != null ? new LookTargets(new[] { wish.Beneficiary, wish.other }) : base.LookAt(wish);

        /// <summary>The friend goes to sit with them now, if they're free to.</summary>
        public override string OnPromised(Wish wish)
        {
            var friend = wish.other;
            var patient = wish.Beneficiary;
            if (friend == null || patient == null || !friend.Spawned || !patient.Spawned || friend.Map != patient.Map) return null;
            if (friend.Downed || friend.Drafted || friend.InMentalState || !patient.InBed() || !RestUtility.Awake(patient)) return null;
            if (!friend.CanReserveAndReach(patient, PathEndMode.Touch, Danger.Some)) return null;
            var chair = SickPawnVisitUtility.FindChair(friend, patient);
            var job = JobMaker.MakeJob(JobDefOf.VisitSickPawn, patient, chair);
            return friend.jobs != null && friend.jobs.TryTakeOrderedJob(job, JobTag.Misc) ? "CC_Promise_SickbedVisit" : null;
        }
    }

    // ===================== Life goals =====================

    /// <summary>
    /// A record goal for healers: patients pulled through, babies delivered. Only while doctors' reputations are on, and
    /// the births one only with Biotech (where births happen).
    /// </summary>
    public class AspirationWorker_HealerRecord : AspirationWorker_Record
    {
        public override bool CanHave(Pawn p)
        {
            if (!HealthTracker.ReputationOn) return false;
            if (def.record == "Council_BirthsAttended" && !ModsConfig.BiotechActive) return false;
            return base.CanHave(p);
        }
    }

    // ===================== Dilemmas =====================

    /// <summary>
    /// A doctor who lost a patient in the last few days (roleA thoughts say which moods): X is the one they lost.
    /// </summary>
    public class DilemmaWorker_DoctorLoss : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => HealthTracker.ReputationOn && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var rec = HealthTracker.Instance?.RecordOf(ctx.a);
            if (rec?.lastLost == null || rec.lastLostTick < 0) return false;
            if (Find.TickManager.TicksGame - rec.lastLostTick > GenDate.TicksPerDay * 4) return false;
            ctx.stranger = rec.lastLost;
            return true;
        }
    }

    /// <summary>Someone still convalescing (and still weak, or near it) who wants to be back at work.</summary>
    public class DilemmaWorker_Convalescent : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => HealthTracker.ConvalescenceOn && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var h = HealthTracker.ConvalescenceOf(ctx.a);
            return h != null && h.Severity >= 0.3f;
        }
    }

    /// <summary>
    /// A major operation is planned for A (a bill in their medical tab), and B, family or a partner, wants a say in it.
    /// </summary>
    public class DilemmaWorker_FamilySurgery : DilemmaWorker
    {
        public static IEnumerable<Bill_Medical> PlannedMajor(Pawn p)
        {
            var bills = p?.BillStack?.Bills;
            if (bills == null) yield break;
            foreach (var b in bills)
                if (b is Bill_Medical m && !m.suspended && m.recipe != null && HealthTracker.MajorSurgery(m.recipe, m.Part))
                    yield return m;
        }

        public override bool CanHappenNow(Map map) => (HealthTracker.ConvalescenceOn || HealthTracker.ReputationOn) && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || !PlannedMajor(ctx.a).Any()) return false;
            var a = ctx.a;
            ctx.b = ctx.map.mapPawns.FreeColonistsSpawned
                .Where(o => o != a && HealthTracker.Ours(o) && !o.Downed && !o.InMentalState && !o.DevelopmentalStage.Baby() && Close(o, a))
                .OrderByDescending(o => o.relations?.OpinionOf(a) ?? 0)
                .FirstOrDefault();
            return ctx.b != null;
        }

        /// <summary>Family or a partner who cares about them (not an ex, not someone who can't stand them).</summary>
        private static bool Close(Pawn o, Pawn a)
        {
            var rel = PawnRelationUtility.GetMostImportantRelation(o, a);
            if (rel == null || rel == PawnRelationDefOf.ExLover || rel == PawnRelationDefOf.ExSpouse) return false;
            return (o.relations?.OpinionOf(a) ?? 0) >= 10;
        }
    }

    /// <summary>
    /// A midwife who has delivered a few babies (Biotech), and B, someone who could learn it from them: Medicine still
    /// well below theirs.
    /// </summary>
    public class DilemmaWorker_MidwifeApprentice : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => HealthTracker.ReputationOn && ModsConfig.BiotechActive && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var a = ctx.a;
            if ((HealthTracker.Instance?.RecordOf(a)?.births ?? 0) < 2) return false;
            var mine = a.skills?.GetSkill(SkillDefOf.Medicine);
            if (mine == null || mine.TotallyDisabled) return false;
            ctx.b = ctx.map.mapPawns.FreeColonistsSpawned
                .Where(o => o != a && HealthTracker.Ours(o) && !o.Downed && !o.InMentalState && !o.DevelopmentalStage.Baby()
                            && o.skills?.GetSkill(SkillDefOf.Medicine) is SkillRecord s && !s.TotallyDisabled && s.Level + 4 <= mine.Level
                            && !o.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                .OrderByDescending(o => People.Passionate(o.skills.GetSkill(SkillDefOf.Medicine)) ? 1 : 0)
                .ThenByDescending(o => o.relations?.OpinionOf(a) ?? 0)
                .FirstOrDefault();
            ctx.skill = SkillDefOf.Medicine;
            return ctx.b != null;
        }
    }

    /// <summary>
    /// The target's convalescence runs longer (<see cref="days"/> positive: back to work too soon) or shorter (negative:
    /// kept in bed). Only ever the time it takes; never past its longest, never below a day.
    /// </summary>
    public class DilemmaEffect_Convalescence : DilemmaEffect
    {
        public float days;

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
            {
                var h = HealthTracker.ConvalescenceOf(p);
                if (h == null) continue;
                float change = days * HealthMath.RecoveryPerDay;
                h.Severity = Mathf.Clamp(h.Severity + change, Mathf.Min(h.Severity, HealthMath.RecoveryPerDay), HealthMath.MaxSeverity);
            }
        }
    }

    /// <summary>A's planned major operations are put off (their bills suspended) until the player resumes them.</summary>
    public class DilemmaEffect_PutOffSurgery : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
                foreach (var b in DilemmaWorker_FamilySurgery.PlannedMajor(p).ToList())
                    b.suspended = true;
        }
    }
}
