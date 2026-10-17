using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Word of the colony's hospital has got about (its name, HealthTracker.HospitalLevel): someone who is unwell comes to
    /// ask for its doctor. From the camp on the map when there is one (they've watched the colony's doctor at work), else
    /// a traveller of a friendly people who heard of it on the road. They come with something small and catching or an
    /// everyday hurt, never anything that could kill them, and they leave when their stay is up whatever the answer.
    /// </summary>
    public class HappeningWorker_HospitalPatient : HappeningWorker_Visitors
    {
        /// <summary>What they come with: Rimfolk's everyday ailments, the first ones that are in the game.</summary>
        private static readonly string[] Ills =
        {
            "Council_Ail_ChestCold", "Council_Ail_HeadCold", "Council_Ail_StomachBug", "Council_Ail_Pinkeye", "Council_Ail_SprainedAnkle",
            "Council_Ail_Toothache", "Council_Ail_StrainedBack"
        };

        public override float BaseChanceThisGame => base.BaseChanceThisGame * HealthMath.PatientWeight(HealthTracker.Instance?.HospitalLevel ?? 0);

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (!HealthTracker.ReputationOn || (HealthTracker.Instance?.HospitalLevel ?? 0) < 1 || Doctor(map) == null) return false;
            return CampPatient(map) != null || base.CanHappen(map, ext);
        }

        /// <summary>The colony's best doctor at home, able to work.</summary>
        private static Pawn Doctor(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();

        /// <summary>One of the camp's grown-ups, not its speaker, when a camp friendly to the colony is settled on the map.</summary>
        private static Pawn CampPatient(Map map)
        {
            var camp = CampTracker.Enabled ? CampTracker.Instance?.camp : null;
            if (camp == null || camp.map != map || !camp.Settled || !camp.CanTalk) return null;
            return camp.members.Where(p => p != null && p.Spawned && p.Map == map && !p.Dead && !p.Downed && p != camp.leader && p.RaceProps.Humanlike
                                            && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Doctor(ctx.map);
            if (ctx.a == null) return false;
            var fromCamp = CampPatient(ctx.map);
            if (fromCamp != null && (Arrivals.Friendly() == null || Rand.Chance(0.6f)))
            {
                ctx.stranger = fromCamp;
                ctx.faction = fromCamp.Faction;
                ctx.note = "camp";
            }
            else
            {
                ctx.faction = Arrivals.Friendly();
                if (ctx.faction == null) return false;
                MakeGroup(ctx, ext, 1, ext.ages);
                ctx.note = null;
            }
            if (ctx.stranger == null) return false;
            ctx.tale = Ail(ctx.stranger);
            return true;
        }

        /// <summary>Gives them something small to come about, and names it.</summary>
        private static string Ail(Pawn p)
        {
            var options = Ills.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(d => d != null && !p.health.hediffSet.HasHediff(d)).ToList();
            var def = options.RandomElementWithFallback();
            if (def == null) return "CC_Health_Patient_Unwell".Translate();
            try
            {
                var h = HediffMaker.MakeHediff(def, p);
                var info = AilmentTracker.InfoOf(def);
                if (h.TryGetComp<HediffComp_Immunizable>() == null) h.Severity = info?.startSeverity.RandomInRange ?? 0.4f;
                else h.Severity = 0.3f;
                p.health.AddHediff(h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hospital patient's ailment", e);
            }
            return def.label;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            ctx.note == "camp" || Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.note == "camp") tags.Add("camp");
            tags.Add((HealthTracker.Instance?.HospitalLevel ?? 0) >= 2 ? "renowned" : "known");
        }
    }

    /// <summary>The colony's doctor (A) sees the patient who came for the hospital (X): tended, counted, remembered.</summary>
    public class DilemmaEffect_HospitalPatient : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (chance < 1f && !Rand.Chance(chance)) return;
            // apart: the answer is the doctor's word; the walk over to the patient plays out on the map (DilemmaEffect_Visit).
            HealthTracker.Instance?.Notify_OutsidePatient(ctx.a, ctx.stranger);
        }
    }
}
