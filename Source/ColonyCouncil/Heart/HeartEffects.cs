using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Strain as a mood: a situational thought that reads the colonist's band each time the game asks, so nothing is
    /// stored in their memories. Strained -3, worn -5, breaking -8; nothing while steady, with the feature off, or on
    /// "Advisor only" stakes (no mood effects at all).
    /// </summary>
    public class ThoughtWorker_HeartStrain : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!ColonyCouncilMod.Settings.MoodEffects) return ThoughtState.Inactive;
            var h = Heart.Of(p);
            if (h == null || !Heart.Eligible(p)) return ThoughtState.Inactive;
            switch (h.Band)
            {
                case StrainBand.Strained: return ThoughtState.ActiveAtStage(0);
                case StrainBand.Worn: return ThoughtState.ActiveAtStage(1);
                case StrainBand.Breaking: return ThoughtState.ActiveAtStage(2);
                default: return ThoughtState.Inactive;
            }
        }
    }

    /// <summary>The strain mood, saying what built it up lately.</summary>
    public class Thought_HeartStrain : Thought_Situational, IExplainedThought
    {
        public string Why
        {
            get
            {
                var h = Heart.Of(pawn);
                if (h == null) return null;
                string sources = HeartView.SourcesText(h, 3);
                return sources.NullOrEmpty() ? null : "CC_Heart_ThoughtWhy".Translate(sources).Resolve();
            }
        }

        public override string Description => Causes.Append(base.Description, Why);
    }

    /// <summary>
    /// Work speed: worn colonists work 5% slower, breaking ones 10%, and the days of rest decree slows everyone it covers.
    /// Patched into the game's global work speed stat (Patches/Council_Heart.xml); contributes nothing while the
    /// feature is off. Carries no save data.
    /// </summary>
    public class StatPart_HeartStrain : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!Heart.On || !(req.Thing is Pawn p)) return;
            var h = Heart.Of(p);
            if (h != null) val *= HeartMath.WorkFactor(h.Band);
            float decree = Heart.DecreeWorkFactor(out _);
            if (decree != 1f && p.IsFreeColonist && !p.IsQuestLodger()) val *= decree;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!Heart.On || !(req.Thing is Pawn p)) return null;
            string text = null;
            var h = Heart.Of(p);
            if (h != null)
            {
                float f = HeartMath.WorkFactor(h.Band);
                if (f != 1f) text = "CC_Heart_StatLine".Translate(HeartView.BandLabel(h.Band), f.ToStringPercent()).Resolve();
            }
            float decree = Heart.DecreeWorkFactor(out var which);
            if (decree != 1f && which != null && p.IsFreeColonist && !p.IsQuestLodger())
            {
                string line = "CC_Heart_StatDecree".Translate(which.label, decree.ToStringPercent()).Resolve();
                text = text == null ? line : text + "\n" + line;
            }
            return text;
        }
    }

    /// <summary>A dilemma's answer that lightens (negative) or adds to (positive) someone's strain.</summary>
    public class DilemmaEffect_Strain : DilemmaEffect
    {
        public float amount;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Heart.On || amount == 0f) return;
            foreach (var p in Targets(ctx).ToList())
                if (Heart.Eligible(p)) HeartLogic.Change(p, amount);
        }
    }
}
