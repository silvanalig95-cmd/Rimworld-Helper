using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Who an effect lands on.</summary>
    public enum EffectTarget
    {
        Beneficiary,
        Requester,
        Supporters,
        /// <summary>Everyone at home.</summary>
        Colony,
        /// <summary>Colonists who like the beneficiary (opinion 20+).</summary>
        Friends
    }

    /// <summary>
    /// Something that happens when a wish comes true or is let go. Listed per wish in XML
    /// (<c>grantEffects</c> / <c>ignoreEffects</c>). Effects marked <see cref="negative"/> only happen
    /// with Balanced or Demanding stakes and scale with the penalty slider.
    /// </summary>
    public abstract class WishEffect
    {
        public EffectTarget target = EffectTarget.Beneficiary;
        public float chance = 1f;
        public bool negative;

        public void Run(Wish w)
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.MoodEffects) return;
            float scale = negative ? s.PenaltyScale : s.rewardFactor;
            if (scale <= 0f) return;
            foreach (var p in Targets(w).Distinct().ToList())
            {
                if (p == null || p.Dead) continue;
                if (!Rand.Chance(UnityEngine.Mathf.Clamp01(chance * (negative ? scale : 1f)))) continue;
                try
                {
                    Apply(w, p, scale);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("wish effect " + GetType().Name, e);
                }
            }
        }

        protected IEnumerable<Pawn> Targets(Wish w)
        {
            switch (target)
            {
                case EffectTarget.Requester:
                    yield return w.pawn;
                    break;
                case EffectTarget.Supporters:
                    foreach (var p in w.Supporters) yield return p;
                    if (!w.Supporters.Contains(w.Beneficiary)) yield return w.Beneficiary;
                    break;
                case EffectTarget.Colony:
                    var map = w.Beneficiary?.MapHeld;
                    if (map == null) yield break;
                    foreach (var p in map.mapPawns.FreeColonistsSpawned) yield return p;
                    break;
                case EffectTarget.Friends:
                    var who = w.Beneficiary;
                    if (who?.MapHeld == null) yield break;
                    foreach (var p in who.MapHeld.mapPawns.FreeColonistsSpawned)
                    {
                        if (p != who && p.relations != null && p.relations.OpinionOf(who) >= 20) yield return p;
                    }
                    break;
                default:
                    yield return w.Beneficiary;
                    break;
            }
        }

        protected abstract void Apply(Wish w, Pawn p, float scale);
    }

    /// <summary>Adds a hediff, e.g. motivated (works faster) or disgruntled (works slower).</summary>
    public class WishEffect_Hediff : WishEffect
    {
        public HediffDef hediff;
        public float severity = 0.5f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            if (hediff == null || p.health == null) return;
            var existing = p.health.hediffSet.GetFirstHediffOfDef(hediff);
            float sev = UnityEngine.Mathf.Clamp(severity * (negative ? scale : 1f), 0.05f, 1f);
            if (existing != null)
            {
                existing.Severity = UnityEngine.Mathf.Max(existing.Severity, sev);
                var disappears = existing.TryGetComp<HediffComp_Disappears>();
                if (disappears != null) disappears.ticksToDisappear = UnityEngine.Mathf.Max(disappears.ticksToDisappear, disappears.Props.disappearsAfterTicks.RandomInRange);
                return;
            }
            var h = HediffMaker.MakeHediff(hediff, p);
            h.Severity = sev;
            p.health.AddHediff(h);
        }
    }

    public class WishEffect_Thought : WishEffect
    {
        public ThoughtDef thought;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var other = p == w.Beneficiary ? w.pawn : w.Beneficiary;
            CouncilGameComponent.GiveThought(p, thought, thought?.IsSocial == true ? other : null, negative ? scale : 1f);
        }
    }

    /// <summary>
    /// Experience in a skill. With <see cref="wishSkill"/>, the skill the wish is about (a passion space).
    /// Colonists throw themselves into the thing they asked for.
    /// </summary>
    public class WishEffect_SkillXp : WishEffect
    {
        public SkillDef skill;
        public bool wishSkill;
        public float xp = 1500f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var sk = skill;
            if (wishSkill) sk = DefDatabase<SkillDef>.GetNamedSilentFail(w.subjectKey) ?? sk;
            var rec = sk == null ? null : p.skills?.GetSkill(sk);
            if (rec == null || rec.TotallyDisabled) return;
            rec.Learn(xp * scale, true);
        }
    }

    /// <summary>Tops up a need, e.g. recreation.</summary>
    public class WishEffect_Need : WishEffect
    {
        public NeedDef need;
        public float amount = 0.3f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var n = need == null ? null : p.needs?.TryGetNeed(need);
            if (n != null) n.CurLevel += amount;
        }
    }

    public class WishEffect_Trust : WishEffect
    {
        public float amount = 5f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            CouncilGameComponent.Instance?.standings.Change(p, amount * (negative ? scale : 1f), w.Label);
        }
    }

    /// <summary>A proper burial lets people start healing: grief memories age faster.</summary>
    public class WishEffect_ShortenGrief : WishEffect
    {
        public float fraction = 0.4f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var mem = p.needs?.mood?.thoughts?.memories;
            if (mem == null) return;
            foreach (var m in mem.Memories)
            {
                if (!Flavor.GriefThoughts.Contains(m.def.defName)) continue;
                int duration = m.DurationTicks;
                if (duration <= 0) continue;
                m.age = UnityEngine.Mathf.Min(duration - 1, m.age + (int)(duration * fraction));
            }
        }
    }

    /// <summary>The supporters of a petition grow closer: they fought for something together.</summary>
    public class WishEffect_Bond : WishEffect
    {
        public ThoughtDef thought;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            if (thought == null) return;
            foreach (var other in w.Supporters)
            {
                if (other != p) CouncilGameComponent.GiveThought(p, thought, other);
            }
        }
    }

    /// <summary>A mental state (only when already unhappy enough). For harsh consequences.</summary>
    public class WishEffect_MentalState : WishEffect
    {
        public MentalStateDef state;
        public float maxMood = 0.35f;

        protected override void Apply(Wish w, Pawn p, float scale)
        {
            if (state == null || p.InMentalState || p.Downed || !p.Spawned) return;
            if ((p.needs?.mood?.CurLevelPercentage ?? 1f) > maxMood) return;
            using (WorldReactions.OwnBreak()) p.mindState?.mentalStateHandler?.TryStartMentalState(state, "CC_MentalReason".Translate(w.Label.Named("WISH")).Resolve(), false, false, true);
        }
    }

    /// <summary>What can make a colonist voice a wish right now (after a raid, a death, a cold night...).</summary>
    public class WishTrigger
    {
        /// <summary>Name for flavour tags: lines tagged [trigger:key] are used for wishes it starts.</summary>
        public string key;
        /// <summary>Fresh memories (within <see cref="freshHours"/>) or current situational thoughts.</summary>
        public List<string> thoughts;
        public List<string> hediffs;
        /// <summary>Active game conditions on the map (cold snap, heat wave...).</summary>
        public List<string> conditions;
        /// <summary>Hours since the last big threat (raid, siege, manhunters...) during which this matches. 0 = off.</summary>
        public int afterThreatHours;
        public float boost = 4f;
        public int freshHours = 8;

        public bool Matches(Pawn p, List<Thought> current)
        {
            if (thoughts != null && current != null)
            {
                int fresh = freshHours * 2500;
                foreach (var t in current)
                {
                    if (!thoughts.Contains(t.def.defName)) continue;
                    if (t is Thought_Memory m && m.age > fresh) continue;
                    return true;
                }
            }
            if (hediffs != null && p.health?.hediffSet != null)
            {
                foreach (var h in p.health.hediffSet.hediffs)
                {
                    if (hediffs.Contains(h.def.defName)) return true;
                }
            }
            if (afterThreatHours > 0 && p.MapHeld != null)
            {
                int last = p.MapHeld.storyState.LastThreatBigTick;
                if (last > 0 && Find.TickManager.TicksGame - last < afterThreatHours * 2500) return true;
            }
            if (conditions != null && p.MapHeld != null)
            {
                foreach (var c in p.MapHeld.gameConditionManager.ActiveConditions)
                {
                    if (conditions.Contains(c.def.defName)) return true;
                }
            }
            return false;
        }
    }
}
