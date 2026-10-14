using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A dilemma brought by the group camped on the colony's land (Camps/): X is their leader (or whoever of them is
    /// about), {FACTION} their people. Only while they're settled and at peace with the colony.
    /// </summary>
    public class DilemmaWorker_Camp : DilemmaWorker
    {
        public static Camp CampOn(Map map)
        {
            if (map == null || !CampTracker.Enabled) return null;
            var c = CampTracker.Instance?.camp;
            return c != null && c.map == map && c.Settled && c.CanTalk && c.faction != null ? c : null;
        }

        public override bool CanHappenNow(Map map) => CampOn(map) != null && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = CampOn(ctx.map);
            if (c == null) return false;
            var here = c.Here;
            var x = c.leader != null && here.Contains(c.leader) ? c.leader : here.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (x == null) return false;
            ctx.stranger = x;
            ctx.faction = c.faction;
            return true;
        }
    }

    /// <summary>The camp's mood toward the colony: negative eases tension (a kindness), positive raises it (a refusal).</summary>
    public class DilemmaEffect_CampTension : DilemmaEffect
    {
        public float amount;
        /// <summary>Counts as a gift in the camp's books (what they remember when they leave).</summary>
        public bool gift;

        public override void Apply(DilemmaContext ctx)
        {
            var c = DilemmaWorker_Camp.CampOn(ctx.map);
            if (c == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            if (amount != 0f) c.AddTension(amount);
            if (gift) c.gifts++;
        }
    }

    /// <summary>
    /// Eases the target's illness (see <see cref="LifeChecks.Illness"/>): less severe, and for an illness the body fights,
    /// a step closer to immunity. Never cures it outright and never makes it worse.
    /// </summary>
    public class DilemmaEffect_EaseIllness : DilemmaEffect
    {
        public float severity = 0.1f;
        public float immunity;

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
            {
                var h = LifeChecks.Illness(p);
                if (h == null) continue;
                if (severity > 0f) h.Severity = Mathf.Max(Mathf.Min(h.Severity, 0.02f), h.Severity - severity);
                if (immunity > 0f && h is HediffWithComps hc && hc.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    var rec = p.health?.immunity?.GetImmunityRecord(h.def);
                    if (rec != null) rec.immunity = Mathf.Min(0.99f, rec.immunity + immunity);
                }
            }
        }
    }

    /// <summary>
    /// A takes B on as a student (a mentorship in the social web), or B takes A with <see cref="reverse"/>: in
    /// <see cref="skill"/>, or else whatever the teacher is best at and the student could still learn. With
    /// <see cref="xp"/>, the student gets a first lesson's worth of practice whether or not the mentorship takes.
    /// </summary>
    public class DilemmaEffect_Teach : DilemmaEffect
    {
        public SkillDef skill;
        public bool reverse;
        public float xp;

        public override void Apply(DilemmaContext ctx)
        {
            var social = CouncilGameComponent.Instance?.social;
            var teacher = reverse ? ctx.b : ctx.a;
            var student = reverse ? ctx.a : ctx.b;
            if (social == null || teacher == null || student == null || teacher == student) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var sk = skill ?? teacher.skills?.skills
                .Where(s => !s.TotallyDisabled && student.skills?.GetSkill(s.def) is SkillRecord r && !r.TotallyDisabled && r.Level + 4 <= s.Level)
                .OrderByDescending(s => s.Level).FirstOrDefault()?.def;
            if (sk == null) return;
            social.StartMentorship(teacher, student, sk);
            var rec = student.skills?.GetSkill(sk);
            if (xp > 0f && rec != null && !rec.TotallyDisabled) rec.Learn(xp, true);
        }
    }
}
