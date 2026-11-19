using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 84. Linked into the Lattice =====================

    /// <summary>
    /// The Lattice's link on a mechanitor of the colony's (Rimfolk_LatticeLink): the people that linked them (<see cref="lattice"/>) and
    /// whether the whole link (<see cref="whole"/>: two more bandwidth, and now and then another's dream) or a link for work only (one
    /// more, no dreams). Its stages' bandwidth is set at startup (LatticeLinkStats), so the def loads without Biotech.
    /// </summary>
    public class Hediff_LatticeLink : HediffWithComps
    {
        public Faction lattice;
        public bool whole = true;

        public override string LabelInBrackets => lattice?.Name ?? base.LabelInBrackets;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref lattice, "lattice");
            Scribe_Values.Look(ref whole, "whole", true);
        }
    }

    /// <summary>The link's bandwidth (Biotech's stat), put on its two stages once the defs are loaded: work only one, the whole link two.</summary>
    [StaticConstructorOnStartup]
    public static class LatticeLinkStats
    {
        static LatticeLinkStats()
        {
            try
            {
                var def = LatticeLinked.LinkDef;
                if (def?.stages == null || !ModsConfig.BiotechActive || StatDefOf.MechBandwidth == null) return;
                for (int i = 0; i < def.stages.Count; i++)
                {
                    var stage = def.stages[i];
                    stage.statOffsets ??= new List<StatModifier>();
                    if (stage.statOffsets.Any(m => m.stat == StatDefOf.MechBandwidth)) continue;
                    stage.statOffsets.Add(new StatModifier { stat = StatDefOf.MechBandwidth, value = PeopleEvents9Math.Bandwidth(i > 0) });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the link's bandwidth", e);
            }
        }
    }

    /// <summary>
    /// Linked into the Lattice (item 84, a gift): a mech-tender of the Lattice links a mechanitor of the colony's into its own where they
    /// stand (two hours with the array, sparks and a bar: round 1's work on a colonist). From then on they command two more bandwidth for
    /// as long as the Lattice stays at peace with the colony (the link goes quiet, for good, the day it doesn't); and now and then they dream
    /// another's dream, told at dawn as a small mood, good or bad. Or a link for work only: one more bandwidth and no dreams. Biotech only.
    /// </summary>
    public static class LatticeLinked
    {
        public const string GiftKind = "latticeLink";

        public static HediffDef LinkDef => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_LatticeLink");

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Link");

        public static Hediff_LatticeLink LinkOf(Pawn p) => p?.health?.hediffSet?.hediffs?.OfType<Hediff_LatticeLink>().FirstOrDefault();

        /// <summary>A mechanitor of the colony's not linked yet: grown, at home, with a mechlink.</summary>
        public static bool CanTake(Pawn p) => p != null && ModsConfig.BiotechActive && MechanitorUtility.IsMechanitor(p) && LinkOf(p) == null && LinkDef != null;

        public static IEnumerable<Pawn> Takers(Map map) => PeopleEvents.Takers(map, GiftKind).Where(CanTake);

        /// <summary>The link put in: the hediff on them with the Lattice's name and its kind, the sparks, told.</summary>
        public static void Linked(Pawn tender, Pawn on, Visit v)
        {
            var map = on?.MapHeld ?? v?.map;
            var f = tender?.Faction ?? PeopleEvents9.PeopleOf(v);
            if (on == null || on.Dead || map == null || !CanTake(on) || f == null) return;
            bool whole = PeopleWorkOn.Note(v) != "work";
            try
            {
                var h = (Hediff_LatticeLink)HediffMaker.MakeHediff(LinkDef, on, on.health.hediffSet.GetBrain());
                h.lattice = f;
                h.whole = whole;
                h.Severity = whole ? 1f : 0.5f;
                on.health.AddHediff(h);
                if (on.Spawned) FleckMaker.ThrowMicroSparks(on.DrawPos, on.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the link put in", e);
                return;
            }
            PeopleEventsBook.Gave(GiftKind, on);
            PeoplesG.SayLine(tender, v?.incident, "linked");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                (whole ? "CC_PEv9_Linked" : "CC_PEv9_LinkedWork").Translate(on.Named("PAWN"), (tender?.LabelShort ?? "?").Named("TENDER"), f.Name.Named("FACTION"),
                    PeopleEvents9Math.Bandwidth(whole).Named("COUNT")).Resolve(), on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv9_LinkedChronicle".Translate(on.Named("PAWN"), f.Name.Named("FACTION")).Resolve(), on));
            PeopleEvents9.Goodwill(f, whole ? PeopleEvents9Math.LinkGoodwill : PeopleEvents9Math.WorkLinkGoodwill, GoodwillWhy.TheirWork);
        }

        /// <summary>The hour: a link whose people are no longer at peace goes quiet; at dawn a whole link may bring another's dream.</summary>
        public static void Watch(int now)
        {
            if (LinkDef == null) return;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
            {
                var h = LinkOf(p);
                if (h == null) continue;
                var f = h.lattice;
                if (f == null || f.defeated || f.HostileTo(Faction.OfPlayer))
                {
                    Broken(p, h);
                    continue;
                }
                var map = p.MapHeld;
                if (!h.whole || map == null || !PeopleEvents9Math.DreamHour(GenLocalDate.HourOfDay(map)) || p.needs?.mood == null) continue;
                int dream = PeopleEvents9Math.Dream(Rand.Value, Rand.Value);
                if (dream != 0) PeopleEvents9.Thought(p, dream > 0 ? "Rimfolk_Thought_LatticeDreamGood" : "Rimfolk_Thought_LatticeDreamBad");
            }
        }

        /// <summary>The link gone quiet: the hediff off, told.</summary>
        private static void Broken(Pawn p, Hediff_LatticeLink h)
        {
            string people = h.lattice?.Name ?? "CC_PEv9_TheLattice".Translate().Resolve();
            p.health.RemoveHediff(h);
            Tell.Message(NewsKind.People, Importance.Notable, p.MapHeld, "CC_PEv9_LinkQuiet".Translate(p.Named("PAWN"), people.Named("FACTION")).Resolve(),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }
    }

    /// <summary>
    /// A mech-tender of {FACTION} ({X}) at the gate: the Lattice would link {B}, the colony's mechanitor, into its own, two hours where they
    /// stand; two more bandwidth while the Lattice is at peace with the colony, and now and then another's dream. Biotech only.
    /// </summary>
    public class HappeningWorker_LatticeLinked : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && LatticeLinked.Takers(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            ctx.b = LatticeLinked.Takers(ctx.map).OrderByDescending(p => p.mechanitor?.TotalBandwidth ?? 0).FirstOrDefault();
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_MechTender"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.count = PeopleEvents9Math.Bandwidth(true);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv9_MechanitorGone".Translate().Resolve();
            return !LatticeLinked.CanTake(ctx.b) ? "CC_PEv9_AlreadyLinked".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "link" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The link put in where {B} stands (PeopleWorkOn, Rimfolk_PEv9_Link); <see cref="whole"/>: the whole link, else for work only.</summary>
    public class DilemmaEffect_LatticeLink : DilemmaEffect
    {
        public bool whole = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            if (PlayOuts.On) sheet.Play("CC_PEv9_Impact_LinkWork".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_LinkWorkBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv9_Impact_Bandwidth".Translate(hc.b.LabelShort, PeopleEvents9Math.Bandwidth(whole), hc.faction?.Name ?? "?").Resolve(),
                "CC_PEv9_Impact_BandwidthBrief".Translate().Resolve(), "pev9bandwidth");
            if (whole) sheet.Add(ImpactKind.Mood, 0, "CC_PEv9_Impact_Dreams".Translate(hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_DreamsBrief".Translate().Resolve(), "pev9dreams");
            if (hc.faction != null) sheet.Goodwill(hc.faction, whole ? PeopleEvents9Math.LinkGoodwill : PeopleEvents9Math.WorkLinkGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var x = hc?.stranger;
            if (hc?.map == null || x == null || hc.b == null || LatticeLinked.Job == null) return;
            PeopleWorkOn.Begin(hc, x, hc.b, LatticeLinked.Job, LocalTargetInfo.Invalid, whole ? "whole" : "work", 0, PeopleEvents9Math.LinkTicks, 6f);
        }
    }

    /// <summary>The two hours with the array done: the mechanitor linked in (LatticeLinked.Linked).</summary>
    public class LatticeLinkDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => LatticeLinked.Linked(worker, on, v);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
