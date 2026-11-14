using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 83. A mechlink (the Lattice) =====================

    /// <summary>
    /// A mech-tender of the Lattice puts the game's mechlink into a colonist where they stand (two hours, sparks and a bar)
    /// and links a light working mech to them to start: a lifter or a constructoid, or with Alpha Mechs a culinarius or a
    /// turbocleaner (PeopleEventsMath.StarterMechs), the mech walking in with the tender. Biotech only. A colony of tribal
    /// means finds it strange to see one of its own given a machine to command.
    /// </summary>
    public static class LatticeMechlink
    {
        public const string GiftKind = "mechlink";

        public static ThingDef LinkDef => DefDatabase<ThingDef>.GetNamedSilentFail("Mechlink");

        /// <summary>A colonist who could take a mechlink: grown, no mechlink yet, able to think and to make.</summary>
        public static bool CanTake(Pawn p) =>
            p?.health?.hediffSet != null && HediffDefOf.MechlinkImplant != null && !p.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant) && p.health.hediffSet.GetBrain() != null
            && !p.WorkTagIsDisabled(WorkTags.Intellectual);

        public static IEnumerable<Pawn> Takers(Map map) => PeopleEvents.Takers(map, GiftKind).Where(CanTake);

        /// <summary>The one the Lattice picks: the colony's best mind and hands among those who could take it.</summary>
        public static Pawn Pick(Map map) =>
            Takers(map).OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0)).FirstOrDefault();

        /// <summary>The light working mech they bring, of the kinds loaded.</summary>
        public static PawnKindDef StarterKind() =>
            PeopleEventsMath.StarterMechs.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k?.race != null && k.RaceProps.IsMechanoid).RandomElementWithFallback();

        public static Pawn MakeMech(Faction f)
        {
            var kind = StarterKind();
            if (kind == null) return null;
            try
            {
                return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the Lattice's mech", e);
                return null;
            }
        }

        /// <summary>The colony lives by tribal means: the sight is strange to its people.</summary>
        public static bool Strange => Avail.EffectiveTech <= TechLevel.Neolithic;

        /// <summary>The mechlink in: the game's implant on the brain, the mech made the colony's with them as its overseer, told.</summary>
        public static void Linked(Pawn tender, Pawn on, Visit v)
        {
            var map = on?.MapHeld ?? v?.map;
            if (on == null || on.Dead || map == null || !CanTake(on)) return;
            Pawn mech = v?.group?.FirstOrDefault(p => p != null && !p.Dead && p.RaceProps.IsMechanoid);
            try
            {
                on.health.AddHediff(HediffDefOf.MechlinkImplant, on.health.hediffSet.GetBrain());
                if (on.Spawned) FleckMaker.ThrowMicroSparks(on.DrawPos, on.Map);
                if (mech != null && mech.Spawned && mech.Map == map)
                {
                    v.group.Remove(mech);
                    Lords.Leave(mech);
                    mech.SetFaction(Faction.OfPlayer);
                    if (on.relations != null && MechanitorUtility.GetOverseer(mech) == null) on.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                }
                else mech = null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the mechlink put in", e);
            }
            PeopleEventsBook.Gave(GiftKind, on);
            PeoplesG.SayLine(tender, v?.incident, "linked");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                (mech != null ? "CC_PEv_Mechlinked" : "CC_PEv_MechlinkedAlone").Translate(on.Named("PAWN"), (tender?.LabelShort ?? "?").Named("TENDER"), (mech?.LabelShort ?? "").Named("MECH")).Resolve(),
                on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv_MechlinkChronicle".Translate(on.Named("PAWN"), (tender?.Faction?.Name ?? "?").Named("FACTION")).Resolve(), on));
            if (!Strange) return;
            var strange = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_MechlinkStrange");
            if (strange == null) return;
            // apart: the colony hears of it, and sees the mech walk behind them from then on; nobody need stand beside them to find it strange.
            using var _cause = Causes.Push("CC_PEv_Cause_Mechlink".Translate(on.LabelShort).Resolve());
            foreach (var p in IncidentWorker_Happening.Colonists(map).Where(p => p != on))
                CouncilGameComponent.GiveThought(p, strange, on);
        }
    }

    /// <summary>
    /// A mech-tender of the Lattice comes with a light working mech ({TALE}) and offers to put a mechlink into {B} where they
    /// stand, two hours with the tender's tools, and link the mech to them to start, for {COUNT} silver or two of the
    /// colony's basic subcores. Biotech only.
    /// </summary>
    public class HappeningWorker_LatticeMechlink : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && LatticeMechlink.Takers(map).Any() && LatticeMechlink.StarterKind() != null
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            ctx.b = LatticeMechlink.Pick(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_MechTender"), ctx.faction, ext.ages);
            var mech = LatticeMechlink.MakeMech(ctx.faction);
            if (ctx.stranger == null || mech == null) return false;
            ctx.group.Add(mech);
            ctx.pet = mech;
            ctx.tale = mech.KindLabel;
            ctx.count = PeopleEventsMath.MechlinkPrice(LatticeMechlink.LinkDef?.BaseMarketValue ?? 600f, mech.MarketValue);
            ctx.a = ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (LatticeMechlink.Strange) tags.Add("strange");
            if (PlayCarry.Have(ctx.map, "SubcoreBasic", null) >= PeopleEventsMath.SubcoresPay) tags.Add("subcores");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_LinkerGone".Translate().Resolve();
            if (!LatticeMechlink.CanTake(ctx.b)) return "CC_PEv_Linked".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "link" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mechlink put in where {B} stands (PeopleWorkOn, Rimfolk_PEv_Mechlink), and the mech linked to them.</summary>
    public class DilemmaEffect_Mechlink : DilemmaEffect
    {
        /// <summary>Paid in silver (given back should the work be given up), not subcores.</summary>
        public bool silver = true;

        public override bool ChanceEach => false;

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Mechlink");

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv_Impact_Mechlink".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv_Impact_MechlinkBrief".Translate().Resolve(), "pevmechlink");
            if (PlayOuts.On) sheet.Play("CC_PEv_Impact_MechlinkWork".Translate(hc.b.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv_Impact_MechlinkWorkBrief".Translate().Resolve());
            var strange = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_MechlinkStrange");
            if (LatticeMechlink.Strange && strange != null)
                sheet.Thought(IncidentWorker_Happening.Colonists(hc.map).Where(p => p != hc.b).ToList(), "CC_PEv_Impact_TheOthers".Translate().Resolve(), strange, p => hc.b);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var x = hc?.stranger;
            if (hc?.map == null || x == null || hc.b == null || Job == null) return;
            PeopleWorkOn.Begin(hc, x, hc.b, Job, LocalTargetInfo.Invalid, null, silver ? hc.count : 0, PeopleEventsMath.MechlinkTicks, 6f);
        }
    }

    public class MechlinkDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => LatticeMechlink.Linked(worker, on, v);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
