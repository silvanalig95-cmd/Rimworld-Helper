using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>On every Rimfolk ailment: hears when it ends (recovered, cured, fatal) and tells the tracker.</summary>
    public class HediffComp_Ailment : HediffComp
    {
        public HediffCompProperties_Ailment Props => (HediffCompProperties_Ailment)props;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            // Handled on the next tick: adding the after-effect now would change the hediff list the game is walking.
            AilmentTracker.QueueEnded(Pawn, parent);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (culprit != parent) return;
            try
            {
                AilmentTracker.Instance?.Notify_Died(Pawn, parent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailment death", e);
            }
        }
    }

    /// <summary>
    /// The game's immunity race, with what real illnesses need on top: a quiet incubation before the first symptoms
    /// (no severity, no immunity yet), a milder course in children, a cough that worsens in the cold, and for long
    /// illnesses (tuberculosis) a treatment course: immunity builds faster while it's tended and slips back when
    /// treatment stops early.
    /// </summary>
    public class HediffCompProperties_AilmentImmunizable : HediffCompProperties_Immunizable
    {
        /// <summary>Days with no symptoms before it shows (0: none).</summary>
        public FloatRange incubationDays = FloatRange.Zero;
        /// <summary>Factor on how fast it worsens in children.</summary>
        public float childFactor = 1f;
        /// <summary>Factor on how fast it worsens while they're somewhere colder than 10 °C.</summary>
        public float coldFactor = 1f;
        /// <summary>Extra immunity per day while tended, times tend quality and the medicine's factor.</summary>
        public float treatedImmunityPerDay;
        /// <summary>Immunity lost per day while untended once treatment has begun.</summary>
        public float untendedImmunityLossPerDay;

        public HediffCompProperties_AilmentImmunizable()
        {
            compClass = typeof(HediffComp_AilmentImmunizable);
        }
    }

    public class HediffComp_AilmentImmunizable : HediffComp_Immunizable
    {
        private int incubateUntil = -1;
        private bool everTended;
        [Unsaved] private HediffComp_TendDuration tend;
        [Unsaved] private HediffComp_AilmentMedicine medicine;
        [Unsaved] private bool looked;

        private HediffCompProperties_AilmentImmunizable AProps => (HediffCompProperties_AilmentImmunizable)props;

        public bool Incubating => incubateUntil > 0 && Find.TickManager.TicksGame < incubateUntil;

        /// <summary>Symptoms from the start (a case that arrives by an incident).</summary>
        public void EndIncubation() => incubateUntil = -1;

        private void Look()
        {
            if (looked) return;
            looked = true;
            tend = parent.TryGetComp<HediffComp_TendDuration>();
            medicine = parent.TryGetComp<HediffComp_AilmentMedicine>();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var range = AProps.incubationDays;
            if (range.max > 0f) incubateUntil = Find.TickManager.TicksGame + Mathf.Max(1, (int)(range.RandomInRange * GenDate.TicksPerDay));
        }

        public override float SeverityChangePerDay()
        {
            if (Incubating) return 0f;
            float v = base.SeverityChangePerDay();
            if (v <= 0f) return v;
            var p = Pawn;
            if (AProps.childFactor != 1f && p.DevelopmentalStage.Juvenile()) v *= AProps.childFactor;
            if (AProps.coldFactor != 1f)
            {
                float temp;
                try
                {
                    temp = p.AmbientTemperature;
                }
                catch
                {
                    temp = 20f;
                }
                if (temp < 10f) v *= AProps.coldFactor;
            }
            return v;
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!Pawn.IsHashIntervalTick(200, delta)) return;
            bool incubating = Incubating;
            var ap = AProps;
            if (!incubating && ap.treatedImmunityPerDay <= 0f && ap.untendedImmunityLossPerDay <= 0f) return;
            var record = Pawn.health?.immunity?.GetImmunityRecord(Def);
            if (record == null) return;
            if (incubating)
            {
                // The body hasn't noticed it yet.
                record.immunity = 0f;
                return;
            }
            Look();
            float perDay = 0f;
            if (tend != null && tend.IsTended)
            {
                everTended = true;
                perDay = ap.treatedImmunityPerDay * tend.tendQuality * (medicine?.Factor ?? 1f);
            }
            else if (everTended) perDay = -ap.untendedImmunityLossPerDay;
            if (perDay != 0f && record.immunity < 1f) record.immunity = Mathf.Clamp01(record.immunity + perDay * 200f / GenDate.TicksPerDay);
        }

        public override string CompTipStringExtra
        {
            get
            {
                string b = base.CompTipStringExtra;
                // Only a long course that slips back when it stops (tuberculosis) needs this said.
                if (AProps.untendedImmunityLossPerDay <= 0f) return b;
                Look();
                string course = tend != null && tend.IsTended ? "CC_Ail_Tip_Treating".Translate() : everTended ? "CC_Ail_Tip_Lapsed".Translate() : "CC_Ail_Tip_Untreated".Translate();
                return b.NullOrEmpty() ? course : b + "\n" + course;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref incubateUntil, "incubateUntil", -1);
            Scribe_Values.Look(ref everTended, "everTended");
        }
    }

    /// <summary>
    /// What tending does for an ailment, with the medicine counted the way the real illness would: for most everyday
    /// ailments any care helps about as well, while for the serious ones that need modern drugs (antibiotics, an
    /// antitoxin) industrial or glitterworld medicine works far better than herbs. Tend timing is the game's own
    /// TendDuration (whose own severity effect is left at 0); the kind of medicine is read from the treatment's best
    /// possible quality.
    /// </summary>
    public class HediffCompProperties_AilmentMedicine : HediffCompProperties
    {
        /// <summary>Severity per day while tended, at quality 100% (negative).</summary>
        public float severityPerDayTended = -0.1f;
        /// <summary>Factors for herbal medicine or none, industrial, and glitterworld medicine.</summary>
        public float basic = 1f;
        public float industrial = 1f;
        public float ultra = 1f;

        public HediffCompProperties_AilmentMedicine()
        {
            compClass = typeof(HediffComp_AilmentMedicine);
        }

        public bool MedicineMatters => Math.Abs(basic - industrial) > 0.01f || Math.Abs(industrial - ultra) > 0.01f;
    }

    public class HediffComp_AilmentMedicine : HediffComp_SeverityModifierBase
    {
        /// <summary>The medicine of the last treatment (see <see cref="AilmentMath.TierOf"/>); -1 before the first.</summary>
        public int tier = -1;
        [Unsaved] private HediffComp_TendDuration tend;
        [Unsaved] private bool looked;

        public HediffCompProperties_AilmentMedicine Props => (HediffCompProperties_AilmentMedicine)props;

        public float Factor => AilmentMath.TierFactor(Math.Max(0, tier), Props.basic, Props.industrial, Props.ultra);

        private HediffComp_TendDuration Tend
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    tend = parent.TryGetComp<HediffComp_TendDuration>();
                }
                return tend;
            }
        }

        public override float SeverityChangePerDay()
        {
            var t = Tend;
            if (t == null || !t.IsTended) return 0f;
            return Props.severityPerDayTended * t.tendQuality * Factor;
        }

        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.CompTended(quality, maxQuality, batchPosition);
            tier = AilmentMath.TierOf(maxQuality);
            try
            {
                AilmentTracker.Instance?.Notify_Tended(Pawn, parent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailment tending", e);
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                if (tier < 0 || !Props.MedicineMatters) return null;
                string key = tier >= 2 ? "CC_Ail_Tip_Medicine2" : tier == 1 ? "CC_Ail_Tip_Medicine1" : "CC_Ail_Tip_Medicine0";
                return key.Translate(Factor.ToStringPercent());
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref tier, "tier", -1);
        }
    }

    /// <summary>
    /// A steady recovery, for ailments the body simply gets over (a strained back, sunburn, blisters): faster in bed
    /// where rest is what helps, slower outdoors where the cause is outdoors (pollen).
    /// </summary>
    public class HediffCompProperties_AilmentRecovery : HediffCompProperties
    {
        public float severityPerDay = -0.1f;
        public float bedFactor = 1f;
        public float outdoorsFactor = 1f;

        public HediffCompProperties_AilmentRecovery()
        {
            compClass = typeof(HediffComp_AilmentRecovery);
        }
    }

    public class HediffComp_AilmentRecovery : HediffComp_SeverityModifierBase
    {
        public HediffCompProperties_AilmentRecovery Props => (HediffCompProperties_AilmentRecovery)props;

        public override float SeverityChangePerDay()
        {
            var pr = Props;
            float v = pr.severityPerDay;
            if (v >= 0f) return v;
            var p = Pawn;
            if (pr.bedFactor != 1f && p.InBed()) v *= pr.bedFactor;
            if (pr.outdoorsFactor != 1f && p.Spawned)
            {
                var room = p.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) v *= pr.outdoorsFactor;
            }
            return v;
        }
    }

    /// <summary>
    /// An illness of the diet (scurvy): it worsens while no fruit or vegetables have been eaten lately and clears
    /// while they are. Without Rimfolk's meal hook the diet isn't known, and it only clears.
    /// </summary>
    public class HediffCompProperties_AilmentDiet : HediffCompProperties
    {
        public float growPerDay = 0.018f;
        public float recoverPerDay = -0.08f;
        /// <summary>Fruit or vegetables within this many days count as eating them.</summary>
        public float freshDays = 2f;

        public HediffCompProperties_AilmentDiet()
        {
            compClass = typeof(HediffComp_AilmentDiet);
        }
    }

    public class HediffComp_AilmentDiet : HediffComp_SeverityModifierBase
    {
        public HediffCompProperties_AilmentDiet Props => (HediffCompProperties_AilmentDiet)props;

        public bool EatingWell => AilmentTracker.Instance?.AteFreshWithin(Pawn, Props.freshDays) ?? true;

        public override float SeverityChangePerDay() => EatingWell ? Props.recoverPerDay : Props.growPerDay;

        public override string CompTipStringExtra => (EatingWell ? "CC_Ail_Tip_DietGood" : "CC_Ail_Tip_DietBad").Translate();
    }

    /// <summary>
    /// The mood of an ailment, by its stage: like the game's own hediff thought, but silent while the illness is still
    /// hidden (incubating), with stages counted from the first one that shows.
    /// </summary>
    public class ThoughtWorker_Ailment : ThoughtWorker_Hediff
    {
        private static readonly Dictionary<HediffDef, int> firstVisible = new Dictionary<HediffDef, int>();

        private static int FirstVisible(HediffDef d)
        {
            if (firstVisible.TryGetValue(d, out int i)) return i;
            i = 0;
            if (d.stages != null)
                for (int s = 0; s < d.stages.Count; s++)
                    if (d.stages[s] != null && d.stages[s].becomeVisible)
                    {
                        i = s;
                        break;
                    }
            firstVisible[d] = i;
            return i;
        }

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (def.hediff == null || def.stages.NullOrEmpty()) return ThoughtState.Inactive;
            var h = p.health?.hediffSet?.GetFirstHediffOfDef(def.hediff);
            if (h == null || !h.Visible) return ThoughtState.Inactive;
            int index = h.CurStageIndex - FirstVisible(def.hediff);
            return ThoughtState.ActiveAtStage(Mathf.Clamp(index, 0, def.stages.Count - 1));
        }
    }
}
