using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Cooped up indoors for too long (mountain bases, long winters): they want to get outside.</summary>
    public class WishWorker_Outside : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var need = pawn.needs?.outdoors;
            if (need == null || HomeMap(pawn) == null || HasTrait(pawn, DefDatabase<TraitDef>.GetNamedSilentFail("Undergrounder"))) yield break;
            if (need.CurLevel > 0.3f) yield break;
            var c = Candidate(need.CurLevel < 0.15f ? 1.6f : 1f, "CC_Reason_Outside".Translate(pawn.Named("PAWN")));
            c.subjectKey = "outside";
            c.targetValue = 0.7f;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => (wish.Beneficiary.needs?.outdoors?.CurLevel ?? 1f) >= wish.targetValue;

        public override float Progress(Wish wish) => UnityEngine.Mathf.Clamp01((wish.Beneficiary.needs?.outdoors?.CurLevel ?? 1f) / wish.targetValue);

        public override string ProgressText(Wish wish) => "CC_Progress_Outside".Translate(wish.Beneficiary.Named("PAWN"));
    }

    /// <summary>An addict who isn't trying to quit wants the drug they're hooked on in stock.</summary>
    public class WishWorker_Fix : WishWorker
    {
        public const int Wanted = 5;

        public static Hediff_Addiction Addiction(Pawn p) =>
            p.health?.hediffSet?.hediffs.OfType<Hediff_Addiction>().FirstOrDefault(a => a.Chemical != null);

        public static List<ThingDef> DrugsFor(ChemicalDef chem) => DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.IsDrug && d.GetCompProperties<CompProperties_Drug>()?.chemical == chem).ToList();

        public static int Stock(Map map, ChemicalDef chem) => map == null || chem == null ? 0 : DrugsFor(chem).Sum(d => map.resourceCounter.GetCount(d));

        /// <summary>
        /// Can the colony come by any of these: some in stock, a researched recipe at a bench it has or can build,
        /// a crop it can sow, a brewing barrel for drink, or a trader on the map who sells it.
        /// </summary>
        public static bool Obtainable(Map map, ICollection<ThingDef> things)
        {
            if (map == null || things == null || things.Count == 0) return false;
            if (things.Any(d => map.resourceCounter.GetCount(d) > 0)) return true;
            if (CanMake(map, things) || CanGrow(things)) return true;
            if (things.Any(Goal_Stock.Alcohol.Contains) && (Avail.ColonyHas(map, Avail.IsFermenter) || Avail.AnyBuildable(map, Avail.IsFermenter))) return true;
            return TraderSells(map, things);
        }

        /// <summary>A researched recipe for one of these, at a bench the colony has or can build.</summary>
        public static bool CanMake(Map map, ICollection<ThingDef> things) =>
            DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef != null && things.Contains(r.ProducedThingDef) && r.AvailableNow
                                                                  && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)));

        /// <summary>A sowable crop that yields one of these, with its research done.</summary>
        public static bool CanGrow(ICollection<ThingDef> things) =>
            DefDatabase<ThingDef>.AllDefsListForReading.Any(p => p.plant?.harvestedThingDef != null && things.Contains(p.plant.harvestedThingDef) && p.plant.Sowable
                                                                 && (p.plant.sowResearchPrerequisites == null || p.plant.sowResearchPrerequisites.All(rp => rp.IsFinished)));

        /// <summary>A trader on the map has one of these for sale.</summary>
        public static bool TraderSells(Map map, ICollection<ThingDef> things) =>
            Strangers.On(map, StrangerKind.Trader).Any(t => t.trader?.Goods?.Any(g => g != null && things.Contains(g.def)) ?? false);

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var addiction = Addiction(pawn);
            if (map == null || addiction == null || TraitDegree(pawn, TraitDefOf.DrugDesire) < 0) yield break;
            var drugs = DrugsFor(addiction.Chemical);
            if (drugs.Count == 0 || Stock(map, addiction.Chemical) >= Wanted) yield break;
            // Only what the colony can actually get: no asking for a drug it can't make, grow or buy.
            if (!Obtainable(map, drugs)) yield break;
            var c = Candidate(1.5f, "CC_Reason_Fix".Translate(pawn.Named("PAWN"), addiction.Chemical.label.Named("DRUG")));
            c.thingDef = drugs.OrderBy(d => d.BaseMarketValue).First();
            c.subjectKey = addiction.Chemical.defName;
            c.subjectLabel = addiction.Chemical.label;
            c.targetValue = Wanted;
            yield return c;
        }

        private static ChemicalDef Chem(Wish wish) => DefDatabase<ChemicalDef>.GetNamedSilentFail(wish.subjectKey);

        public override bool IsFulfilled(Wish wish) => Stock(wish.Beneficiary.MapHeld, Chem(wish)) >= wish.targetValue;

        /// <summary>Clean now, or the craving is gone: nothing to ask for.</summary>
        public override bool StillValid(Wish wish)
        {
            var chem = Chem(wish);
            return chem != null && Addiction(wish.Beneficiary)?.Chemical == chem;
        }

        /// <summary>The drug can't be made, grown or bought any more (the trader left, its mod was removed): no blame.</summary>
        public override bool StillObtainable(Wish wish)
        {
            var chem = Chem(wish);
            if (!base.StillObtainable(wish) || chem == null) return false;
            var map = wish.Beneficiary.MapHeld;
            return map == null || Obtainable(map, DrugsFor(chem));
        }

        public override string ProgressText(Wish wish) => "CC_Progress_Fix".Translate(Stock(wish.Beneficiary.MapHeld, Chem(wish)), wish.targetValue.ToString("0"), wish.subjectLabel ?? "");
    }

    /// <summary>An addict who wants to get clean: the colony has to stop supplying them until the addiction passes.</summary>
    public class WishWorker_Quit : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var addiction = WishWorker_Fix.Addiction(pawn);
            if (addiction == null || HomeMap(pawn) == null || TraitDegree(pawn, TraitDefOf.DrugDesire) > 0) yield break;
            // Some addictions never fade (luciferium): quitting those isn't something the colony can grant.
            if (!(addiction.def.comps?.OfType<HediffCompProperties_SeverityPerDay>().Any(c => c.severityPerDay < 0f) ?? false)) yield break;
            var c = Candidate(TraitDegree(pawn, TraitDefOf.DrugDesire) < 0 ? 2f : 1f, "CC_Reason_Quit".Translate(pawn.Named("PAWN"), addiction.Chemical.label.Named("DRUG")));
            c.subjectKey = addiction.Chemical.defName;
            c.subjectLabel = addiction.Chemical.label;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => WishWorker_Fix.Addiction(wish.Beneficiary) == null
                                                       || WishWorker_Fix.Addiction(wish.Beneficiary).Chemical.defName != wish.subjectKey;

        public override string ProgressText(Wish wish) => "CC_Progress_Quit".Translate(wish.Beneficiary.Named("PAWN"), wish.subjectLabel ?? "");
    }

    /// <summary>Sharing a room with someone they can't stand. They want to sleep somewhere else, or the other one to.</summary>
    public class WishWorker_Separate : WishWorker
    {
        public static Room BedRoom(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            return bed != null && bed.Spawned ? bed.GetRoom() : null;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = BedRoom(pawn);
            if (room == null || room.PsychologicallyOutdoors || pawn.relations == null) yield break;
            foreach (var other in pawn.MapHeld.mapPawns.FreeColonistsSpawned)
            {
                if (other == pawn || BedRoom(other) != room) continue;
                if (LovePartnerRelationUtility.LovePartnerRelationExists(pawn, other)) continue;
                int opinion = pawn.relations.OpinionOf(other);
                if (opinion > -20) continue;
                var c = Candidate(opinion <= -50 ? 2f : 1.2f, "CC_Reason_Separate".Translate(pawn.Named("PAWN"), other.Named("OTHER")));
                c.other = other;
                c.subjectKey = other.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var mine = BedRoom(wish.Beneficiary);
            return wish.other == null || (mine != null && BedRoom(wish.other) != mine);
        }

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead && wish.other.IsColonist;

        public override string ProgressText(Wish wish) => "CC_Progress_Separate".Translate(wish.Beneficiary.Named("PAWN"), wish.other.Named("OTHER"));

        /// <summary>
        /// Promises kept: they still sleep apart. Gone, or on good terms again, there's nothing left to keep; without a bed
        /// of their own just now, it can't be told.
        /// </summary>
        public override KeptCheck Holds(Wish wish)
        {
            var o = wish.other;
            var me = wish.Beneficiary;
            if (o == null || o.Dead || !o.IsColonist || me.relations == null || me.relations.OpinionOf(o) > 0) return KeptCheck.Over;
            var mine = BedRoom(me);
            if (mine == null || mine.PsychologicallyOutdoors) return KeptCheck.Unclear;
            return BedRoom(o) != mine ? KeptCheck.Holds : KeptCheck.Gone;
        }

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "assignment";

        public override LookTargets LookAt(Wish wish) => wish.Beneficiary.ownership?.OwnedBed is Building_Bed b ? new LookTargets(b) : base.LookAt(wish);
    }

    /// <summary>A doctor wants the infirmary stocked: enough medicine for everyone, twice over.</summary>
    public class WishWorker_Infirmary : WishWorker
    {
        public static int Medicine(Map map) => Avail.MedicineCount(map);

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var med = pawn.skills?.GetSkill(SkillDefOf.Medicine);
            if (map == null || med == null || med.TotallyDisabled || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) yield break;
            if (!People.Passionate(med) && med.Level < 8) yield break;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            int have = Medicine(map);
            if (have >= colonists * 2) yield break;
            int recentThreat = map.storyState.LastThreatBigTick;
            bool afterFight = recentThreat > 0 && Find.TickManager.TicksGame - recentThreat < GenDate.TicksPerDay * 3;
            var c = Candidate(afterFight ? 1.6f : 0.8f, "CC_Reason_Infirmary".Translate(pawn.Named("PAWN"), have.Named("COUNT")));
            c.subjectKey = "colony";
            c.targetValue = colonists * 3;
            yield return c;
        }

        private static int MedicineAt(Wish wish) => wish.Beneficiary.MapHeld is Map m ? Medicine(m) : 0;

        public override bool IsFulfilled(Wish wish) => wish.Beneficiary.MapHeld != null && MedicineAt(wish) >= wish.targetValue;

        public override float Progress(Wish wish) => UnityEngine.Mathf.Clamp01(MedicineAt(wish) / UnityEngine.Mathf.Max(1f, wish.targetValue));

        public override string ProgressText(Wish wish) => "CC_Progress_Infirmary".Translate(MedicineAt(wish), wish.targetValue.ToString("0"));

        public override InspirationDef InspirationFor(Wish wish) => DefDatabase<InspirationDef>.GetNamedSilentFail("Inspired_Surgery");

        /// <summary>Promises kept: enough medicine stays in store; a share of it, since medicine is there to be used (keepShare).</summary>
        public override KeptCheck Holds(Wish wish) =>
            MedicineAt(wish) >= KeptMath.Needed(wish.targetValue, def.keepShare) ? KeptCheck.Holds : KeptCheck.Gone;

        public override string KeptKind(Wish wish) => "stock";
    }
}
