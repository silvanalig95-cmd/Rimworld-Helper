using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 79. A gene taken out (the Helix Works) =====================

    /// <summary>
    /// A gene taken out (item 79, a gift): a sequencer of the Helix Works comes to the gate offering to take a harmful gene out of one of
    /// the colony (one the body gains metabolism by, not archite, that no other gene of theirs needs), where they stand, in two hours, for
    /// silver or one of the colony's genepacks: round G's gene work in reverse, the same hold and the same instruments. The colonist loses
    /// the metabolism that gene gave them (they get a little hungrier, as the game counts it) and gets the game's own mark of a xenogerm
    /// replicating, so no more gene work is done on them too soon. One of the events' visits (kind "pev:geneout"): x the sequencer, a the
    /// colonist, its text "people|gene".
    /// </summary>
    public static class GeneTakenOut
    {
        public const string Kind = "pev:geneout";
        public const string GiftKind = "geneOut";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The harmful genes a colonist carries that could be taken out: one the body gains by, not archite, that no other gene of theirs needs.</summary>
        public static List<Gene> Harmful(Pawn p)
        {
            var list = new List<Gene>();
            if (!ModsConfig.BiotechActive || p?.genes == null) return list;
            var all = p.genes.GenesListForReading;
            foreach (var g in all)
            {
                if (g?.def == null || !PeopleEvents8Math.Harmful(g.def.biostatMet, g.def.biostatArc)) continue;
                if (all.Any(o => o != g && o.def.prerequisite == g.def)) continue;
                list.Add(g);
            }
            return list;
        }

        /// <summary>The colony's grown people a gene could be taken out of now: none still replicating a xenogerm, no gift of this kind before.</summary>
        public static IEnumerable<Pawn> Takers(Map map) =>
            PeoplesG.Grown(map).Where(p => p.genes != null && HediffDefOf.XenogermReplicating != null && !p.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating)
                                          && PeopleEvents.MayGive(GiftKind, p) && Harmful(p).Count > 0);

        /// <summary>
        /// The answer taken: the sequencer walks over to them and works on them two hours; when a door barred to visitors is in the way
        /// (they are in a bedroom or a cold store), they walk out to the sequencer instead and it is done there (PeopleEvents8.WorkOn).
        /// With the play-outs off, at once.
        /// </summary>
        public static void Begin(HappeningContext ctx)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var b = ctx?.b;
            if (map == null || t == null || x == null || b == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, b, null, Now + 6 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (ctx.note ?? "");
            if (!PlayOuts.On)
            {
                Out(v);
                return;
            }
            // Not able to start now (they are drafted, or in a break): the visit's hour tries again.
            PeopleEvents8.WorkOn(v, PeopleEvents8.GeneOutJob, PeopleEvents8Math.OutTicks);
        }

        /// <summary>The visit's hour: the work under way, left; stopped, set going again; the hours out before it could be done, said so (never done unseen).</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            bool working = x != null && x.Spawned && x.CurJobDef == PeopleEvents8.GeneOutJob;
            switch (PeopleEvents8Math.Step(PeoplesG.Up(x, v.map), working, now, v.until, v.due))
            {
                case PeopleEvents8Math.MeetStep.Wait:
                    return true;
                case PeopleEvents8Math.MeetStep.Again:
                    v.due++;
                    PeopleEvents8.WorkOn(v, PeopleEvents8.GeneOutJob, PeopleEvents8Math.OutTicks);
                    return true;
                default:
                    PeopleEvents8.Undone(v);
                    return false;
            }
        }

        public static void Done(Pawn sequencer, Pawn taker)
        {
            var v = PeopleEvents8.Of(Kind, sequencer);
            if (v != null) Out(v);
        }

        /// <summary>The gene taken out, once: gone from them, the game's mark of a xenogerm replicating put on them, told; the sequencer goes home.</summary>
        private static void Out(Visit v)
        {
            if (v.done) return;
            v.done = true;
            PeopleEvents8.Release(v);
            var x = v.x;
            var b = v.a;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            var parts = (v.text ?? "").Split('|');
            var def = parts.Length > 1 ? DefDatabase<GeneDef>.GetNamedSilentFail(parts[1]) : null;
            var gene = def != null && b?.genes != null && !b.Dead ? b.genes.GenesListForReading.FirstOrDefault(g => g.def == def) : null;
            if (gene != null)
            {
                try
                {
                    b.genes.RemoveGene(gene);
                    if (HediffDefOf.XenogermReplicating != null && !b.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating)) b.health.AddHediff(HediffDefOf.XenogermReplicating);
                    PeopleEventsBook.Gave(GiftKind, b);
                    if (b.Spawned) FleckMaker.ThrowMetaIcon(b.Position, b.Map, FleckDefOf.HealingCross);
                    PeoplesG.SayLine(x, v.incident, "done");
                    Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv8_GeneOut".Translate((x?.LabelShort ?? "?").Named("SEQ"), b.Named("PAWN"), def.label.Named("GENE")).Resolve(),
                        b.Spawned ? new LookTargets(b) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    PeopleEvents8.Goodwill(f, PeopleEvents8Math.OutGoodwill, GoodwillWhy.TheirWork);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 8: the gene taken out", e);
                }
            }
            if (x != null) PeoplesG.Home(new List<Pawn> { x });
        }
    }

    /// <summary>
    /// A sequencer of {FACTION} ({X}) at the gate: they would take the harmful gene {TALE} out of {B} where {B_pronoun} stands, two hours'
    /// work, for {COUNT} silver or one of the colony's genepacks.
    /// </summary>
    public class HappeningWorker_GeneTakenOut : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Editors) != null && GeneTakenOut.Takers(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            ctx.b = GeneTakenOut.Takers(ctx.map).RandomElementWithFallback();
            if (ctx.b == null) return false;
            var gene = GeneTakenOut.Harmful(ctx.b).OrderByDescending(g => g.def.biostatMet).FirstOrDefault();
            if (gene == null) return false;
            ctx.note = gene.def.defName;
            ctx.tale = gene.def.label;
            ctx.variant = gene.def.biostatMet;
            ctx.count = PeopleEvents8Math.OutPrice(gene.def.biostatMet);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_Sequencer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PlayCarry.Have(ctx.map, "Genepack", null) > 0) tags.Add("genepack");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PeoplesG_TakerGone".Translate().Resolve();
            return !GeneTakenOut.Harmful(ctx.b).Any(g => g.def.defName == ctx.note) ? "CC_PEv8_GeneGone".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "work" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The gene taken out of {B} where they stand (GeneTakenOut.Begin): two hours, the healing cross, then the gene gone.</summary>
    public class DilemmaEffect_GeneOut : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Play("CC_PEv8_Impact_GeneOutWork".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv8_Impact_GeneOutWorkBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, 1, "CC_PEv8_Impact_GeneOut".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv8_Impact_GeneOutBrief".Translate().Resolve(), "pev8geneout");
            sheet.Add(ImpactKind.Needs, -1, "CC_PEv8_Impact_Hungrier".Translate(hc.b.LabelShort).Resolve(), "CC_PEv8_Impact_HungrierBrief".Translate().Resolve(), "pev8hungrier");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.OutGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => GeneTakenOut.Begin(ctx as HappeningContext);
    }
}
