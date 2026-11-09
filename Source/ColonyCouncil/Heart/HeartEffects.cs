using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Strain as a mood: a situational thought that reads the colonist's band each time the game asks, so nothing is
    /// stored in their memories. Strained -4, worn -8, breaking -14 (the setting "How strongly the bands are felt": -3, -5
    /// and -8 mild, -5, -10 and -18 strong); nothing while steady, with the feature off, or on "Advisor only" stakes (no mood
    /// effects at all).
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

    /// <summary>The strain mood, saying what built it up lately; its size by the band and how strongly the bands are felt.</summary>
    public class Thought_HeartStrain : Thought_Situational, IExplainedThought
    {
        /// <summary>The stage's size for the strength chosen (the stages in XML are the default, "felt").</summary>
        protected override float BaseMoodOffset => HeartCountMath.StrainMood((StrainBand)(CurStageIndex + 1), ColonyCouncilMod.Settings?.heartBands ?? BandStrength.Felt);

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
    /// Work speed: strained colonists work 3% slower, worn 7%, breaking 12% (with the heart's effects beyond mood; 5% and 10%
    /// from worn without them), and the days of rest decree slows everyone it covers. Patched into the game's global work
    /// speed stat (Patches/Council_Heart.xml); contributes nothing while the feature is off. Carries no save data.
    /// </summary>
    public class StatPart_HeartStrain : StatPart
    {
        // Work speed is asked for every tick a colonist works, so this stays a dictionary lookup: only those with a heart
        // (the colony's own people, never animals, mechs, visitors or quest lodgers) are touched at all.
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!Heart.On || !(req.Thing is Pawn p)) return;
            var h = Heart.Of(p);
            if (h == null) return;
            val *= HeartEffects.Work(h);
            if (!p.IsFreeColonist) return;
            float decree = Heart.DecreeWorkFactor(p, out _);
            if (decree != 1f) val *= decree;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!Heart.On || !(req.Thing is Pawn p)) return null;
            var h = Heart.Of(p);
            if (h == null) return null;
            string text = null;
            float f = HeartEffects.Work(h);
            if (f != 1f) text = "CC_Heart_StatLine".Translate(HeartView.BandLabel(h.Band), f.ToStringPercent()).Resolve();
            DecreeDef which = null;
            float decree = p.IsFreeColonist ? Heart.DecreeWorkFactor(p, out which) : 1f;
            if (decree != 1f && which != null)
            {
                string line = "CC_Heart_StatDecree".Translate(which.label, decree.ToStringPercent()).Resolve();
                text = text == null ? line : text + "\n" + line;
            }
            return text;
        }
    }

    /// <summary>
    /// The heart's effects beyond mood on one stat (Patches/Council_Heart.xml): learning, the need for sleep, healing and
    /// immunity, by strain's band and the good side's (HeartCountMath.Factor: never more than a tenth either way, immunity
    /// never more than 5% slower). Nothing with the heart or the setting "Effects beyond mood" off, or for anyone without a
    /// heart. Its lines in the stat's explanation say which side gave what.
    /// </summary>
    public class StatPart_HeartFelt : StatPart
    {
        public HeartEffect effect = HeartEffect.Learning;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!HeartEffects.On || !(req.Thing is Pawn p)) return;
            var h = Heart.Of(p);
            if (h == null) return;
            float f = HeartCountMath.Factor(effect, h.Band, GoodHeart.Band(h), true);
            if (f != 1f) val *= f;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!HeartEffects.On || !(req.Thing is Pawn p)) return null;
            var h = Heart.Of(p);
            return h == null ? null : HeartEffects.StatLines(h, effect);
        }
    }

    /// <summary>The heart's effects beyond mood, for the stat parts, the heart's page and the Mood tab's summary.</summary>
    public static class HeartEffects
    {
        /// <summary>The heart and its effects beyond mood are on.</summary>
        public static bool On => Heart.On && (ColonyCouncilMod.Settings?.heartEffects ?? true);

        /// <summary>The good side's mood at a band by how strongly the bands are felt (the setting).</summary>
        public static float GoodMoodNow(GoodBand band) => HeartCountMath.GoodMood(band, ColonyCouncilMod.Settings?.heartBands ?? BandStrength.Felt);

        /// <summary>Work speed from strain's band (the release 1 numbers with the effects beyond mood off).</summary>
        public static float Work(PawnHeart h) => h == null ? 1f : HeartCountMath.Work(h.Band, ColonyCouncilMod.Settings?.heartEffects ?? true);

        /// <summary>The stat's explanation: "Strain, worn down: x94%" and "Good side, in good heart: x104%", each that moves it.</summary>
        public static string StatLines(PawnHeart h, HeartEffect e)
        {
            string text = null;
            var good = GoodHeart.Band(h);
            float strain = HeartCountMath.StrainSide(e, h.Band);
            float goodSide = HeartCountMath.GoodSide(e, good);
            float whole = HeartCountMath.Factor(e, h.Band, good, true);
            if (strain != 1f) text = "CC_Heart_StatLine".Translate(HeartView.BandLabel(h.Band), strain.ToStringPercent()).Resolve();
            if (goodSide != 1f)
            {
                string line = "CC_Count_StatGood".Translate(GoodHeart.BandLabel(good), goodSide.ToStringPercent()).Resolve();
                text = text == null ? line : text + "\n" + line;
            }
            // Kept within bounds: say so when the two sides together would have gone further.
            if (text != null && Mathf.Abs(whole - strain * goodSide) > 0.0005f) text += "\n" + "CC_Count_StatBound".Translate(whole.ToStringPercent()).Resolve();
            return text;
        }

        /// <summary>
        /// What the bands are doing to them now, in a few words: "-8 mood, work -7%, learns 6% slower, needs 6% more sleep,
        /// heals 5% slower, fights off illness 3% slower, nightmares 1.6 times as likely". Strain's side, or the good side's
        /// ("+6 mood, learns 4% faster"). Null for a side with nothing.
        /// </summary>
        public static string Doing(PawnHeart h, bool goodSide) => h == null ? null : goodSide ? DoingGood(GoodHeart.Band(h)) : DoingStrain(h.Band);

        /// <summary>What strain's band does, by the settings as they are (the band's own words on the heart's page say it too).</summary>
        public static string DoingStrain(StrainBand band)
        {
            if (band == StrainBand.Steady) return null;
            var parts = new List<string>(7);
            var s = ColonyCouncilMod.Settings;
            bool beyond = On;
            if (s != null && s.MoodEffects) parts.Add("CC_Count_DoingMood".Translate(MoodView.Number(HeartCountMath.StrainMood(band, s.heartBands))).Resolve());
            float work = HeartCountMath.Work(band, beyond);
            if (work != 1f) parts.Add("CC_Count_DoingWork".Translate(Pct(work)).Resolve());
            if (beyond)
            {
                Part(parts, HeartEffect.Learning, HeartCountMath.StrainSide(HeartEffect.Learning, band));
                Part(parts, HeartEffect.Rest, HeartCountMath.StrainSide(HeartEffect.Rest, band));
                Part(parts, HeartEffect.Healing, HeartCountMath.StrainSide(HeartEffect.Healing, band));
                Part(parts, HeartEffect.Immunity, HeartCountMath.StrainSide(HeartEffect.Immunity, band));
            }
            float nightmare = HeartMath.NightmareFactor(band);
            if (nightmare > 1f) parts.Add("CC_Count_DoingNightmares".Translate(nightmare.ToString("0.#")).Resolve());
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>What the good side's band does, by the settings as they are.</summary>
        public static string DoingGood(GoodBand good)
        {
            if (good == GoodBand.None || !GoodHeart.On) return null;
            var parts = new List<string>(5);
            var s = ColonyCouncilMod.Settings;
            if (s != null && s.MoodEffects) parts.Add("CC_Count_DoingMood".Translate(MoodView.Number(HeartCountMath.GoodMood(good, s.heartBands))).Resolve());
            if (On)
            {
                Part(parts, HeartEffect.Learning, HeartCountMath.GoodSide(HeartEffect.Learning, good));
                Part(parts, HeartEffect.Healing, HeartCountMath.GoodSide(HeartEffect.Healing, good));
                Part(parts, HeartEffect.Immunity, HeartCountMath.GoodSide(HeartEffect.Immunity, good));
            }
            if (good >= GoodBand.GoodHeart) parts.Add("CC_Count_DoingDreams".Translate().Resolve());
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        private static void Part(List<string> parts, HeartEffect e, float f)
        {
            if (Mathf.Abs(f - 1f) < 0.0005f) return;
            string pct = Mathf.RoundToInt(Mathf.Abs(f - 1f) * 100f) + "%";
            string key = "CC_Count_Doing" + e + (f > 1f ? "_More" : "_Less");
            parts.Add(key.Translate(pct).Resolve());
        }

        /// <summary>A factor as a signed percentage: 0.93 is "-7%".</summary>
        public static string Pct(float f)
        {
            int n = Mathf.RoundToInt((f - 1f) * 100f);
            return (n > 0 ? "+" : "") + n + "%";
        }
    }

    /// <summary>A dilemma's answer that lightens (negative) or adds to (positive) someone's strain.</summary>
    public class DilemmaEffect_Strain : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Heart.On || amount == 0f) return;
            var who = Reached(ctx).Where(p => Heart.Eligible(p)).ToList();
            if (who.Count > 0) DilemmaImpact.Strain(sheet, WhoOf(ctx, who), KeyOf(who), amount, who.Count > 1);
        }

        public float amount;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Heart.On || amount == 0f) return;
            foreach (var p in Targets(ctx).ToList())
                if (Heart.Eligible(p)) HeartLogic.Change(p, amount);
        }
    }
}
