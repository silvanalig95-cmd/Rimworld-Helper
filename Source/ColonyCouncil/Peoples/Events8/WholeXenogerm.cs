using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 80. A whole xenogerm (the Helix Works) =====================

    /// <summary>
    /// A whole xenogerm (item 80, a gift): a sequencer of the Helix Works comes to the gate offering one of the colony's grown baseliners a
    /// whole xenogerm of one of the Works' own kinds (the kinds its people are, from Biotech, Odyssey, Alpha Genes and the hussars of
    /// Vanilla Races Expanded, as the player's mods give them; never one with archite genes), for silver and two of the colony's genepacks,
    /// or all in silver. Taken, the sequencer works on them where they stand for three hours; the kind's genes are written in as a xenogerm
    /// (any genes put in before are replaced), and they lie in the game's own xenogermination coma afterwards, with the game's mark of a
    /// xenogerm replicating. A colonist whose faith holds to its own kinds (Biotech's preferred xenotypes) is told so on the card, and the
    /// game's own mood follows. One of the events' visits (kind "pev:xenogerm"): x the sequencer, a the colonist, its text "people|kind".
    /// </summary>
    public static class WholeXenogerm
    {
        public const string Kind = "pev:xenogerm";
        public const string GiftKind = "xenogerm";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The Works' kinds a whole xenogerm is grown of: the round's list, as the Works' own people are, none with archite genes.</summary>
        public static List<XenotypeDef> Kinds(Faction f)
        {
            var set = f?.def?.xenotypeSet;
            return PeopleEvents8Math.Xenotypes.Select(n => DefDatabase<XenotypeDef>.GetNamedSilentFail(n))
                .Where(x => x != null && !x.genes.NullOrEmpty() && x.genes.All(g => g != null && g.biostatArc <= 0) && (set == null || set.Count == 0 || set.Contains(x))).ToList();
        }

        /// <summary>A grown baseliner of the colony with nothing put in, not replicating a xenogerm, no gift of this kind before.</summary>
        public static IEnumerable<Pawn> Takers(Map map) =>
            PeoplesG.Grown(map).Where(p => p.genes != null && p.genes.Xenotype == XenotypeDefOf.Baseliner && p.genes.CustomXenotype == null && p.genes.Xenogenes.Count == 0
                                          && HediffDefOf.XenogermReplicating != null && !p.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating) && PeopleEvents.MayGive(GiftKind, p));

        /// <summary>The colonist's faith holds to its own kinds (Biotech's preferred xenotypes) and this isn't one of them.</summary>
        public static bool Frowned(Pawn p, XenotypeDef x)
        {
            var ideo = ModsConfig.IdeologyActive ? p?.Ideo : null;
            return ideo != null && x != null && ideo.PreferredXenotypes.Count > 0 && !ideo.PreferredXenotypes.Contains(x);
        }

        /// <summary>The answer taken: the sequencer works three hours at the colonist's side; with the play-outs off, at once.</summary>
        public static void Begin(HappeningContext ctx)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var b = ctx?.b;
            if (map == null || t == null || x == null || b == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, b, null, Now + 8 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (ctx.note ?? "");
            if (!PlayOuts.On)
            {
                Implant(v);
                return;
            }
            // Straight to them, or, with a door barred to visitors in the way, they walk out to the sequencer (PeopleEvents8.WorkOn); not
            // able to start now, the visit's hour tries again.
            PeopleEvents8.WorkOn(v, PeopleEvents8.XenogermJob, PeopleEvents8Math.XenogermTicks);
        }

        /// <summary>The visit's hour: the work under way, left; stopped, set going again; the hours out before it could be done, said so (never done unseen).</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            bool working = x != null && x.Spawned && x.CurJobDef == PeopleEvents8.XenogermJob;
            switch (PeopleEvents8Math.Step(PeoplesG.Up(x, v.map), working, now, v.until, v.due))
            {
                case PeopleEvents8Math.MeetStep.Wait:
                    return true;
                case PeopleEvents8Math.MeetStep.Again:
                    v.due++;
                    PeopleEvents8.WorkOn(v, PeopleEvents8.XenogermJob, PeopleEvents8Math.XenogermTicks);
                    return true;
                default:
                    PeopleEvents8.Undone(v);
                    return false;
            }
        }

        public static void Done(Pawn sequencer, Pawn taker)
        {
            var v = PeopleEvents8.Of(Kind, sequencer);
            if (v != null) Implant(v);
        }

        /// <summary>
        /// The xenogerm written in, once: the kind's genes as a xenogerm (whatever was put in before replaced), the game's coma and its mark
        /// of a xenogerm replicating, told; the sequencer goes home.
        /// </summary>
        private static void Implant(Visit v)
        {
            if (v.done) return;
            v.done = true;
            PeopleEvents8.Release(v);
            var x = v.x;
            var b = v.a;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            var parts = (v.text ?? "").Split('|');
            var kind = parts.Length > 1 ? DefDatabase<XenotypeDef>.GetNamedSilentFail(parts[1]) : null;
            if (kind != null && b?.genes != null && !b.Dead)
            {
                try
                {
                    b.genes.ClearXenogenes();
                    foreach (var g in kind.genes)
                        if (g != null && b.genes.GetGene(g) == null)
                            b.genes.AddGene(g, true);
                    b.genes.SetXenotypeDirect(kind);
                    b.genes.xenotypeName = null;
                    b.genes.iconDef = null;
                    if (HediffDefOf.XenogerminationComa != null) b.health.AddHediff(HediffDefOf.XenogerminationComa);
                    GeneUtility.UpdateXenogermReplication(b);
                    PeopleEventsBook.Gave(GiftKind, b);
                    if (b.Spawned) FleckMaker.ThrowMetaIcon(b.Position, b.Map, FleckDefOf.HealingCross);
                    PeoplesG.SayLine(x, v.incident, "done");
                    Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv8_XenogermIn".Translate((x?.LabelShort ?? "?").Named("SEQ"), b.Named("PAWN"), kind.label.Named("KIND")).Resolve(),
                        b.Spawned ? new LookTargets(b) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    PeopleEvents8.Goodwill(f, PeopleEvents8Math.XenogermGoodwill, GoodwillWhy.TheirWork);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 8: the xenogerm written in", e);
                }
            }
            if (x != null) PeoplesG.Home(new List<Pawn> { x });
        }
    }

    /// <summary>
    /// A sequencer of {FACTION} ({X}) at the gate: a whole xenogerm of the {TALE} kind for {B}, three hours' work, for {COUNT} silver and two
    /// genepacks, or all in silver; {B} lies in the xenogermination coma afterwards. {RELATION} is {B}'s faith, where it holds to its own kinds.
    /// </summary>
    public class HappeningWorker_WholeXenogerm : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (!ModsConfig.BiotechActive) return false;
            var f = PeoplesD.Friendly(PeoplesMath.Editors);
            return f != null && WholeXenogerm.Kinds(f).Count > 0 && WholeXenogerm.Takers(map).Any() && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            ctx.b = WholeXenogerm.Takers(ctx.map).RandomElementWithFallback();
            var kind = WholeXenogerm.Kinds(ctx.faction).RandomElementWithFallback();
            if (ctx.b == null || kind == null) return false;
            ctx.note = kind.defName;
            ctx.tale = kind.label;
            ctx.count = PeopleEvents8Math.XenogermPrice(kind.genes.Count, kind.genes.Sum(g => g.biostatCpx));
            ctx.variant = PeopleEvents8Math.SilverOnly(ctx.count);
            ctx.relation = WholeXenogerm.Frowned(ctx.b, kind) ? ctx.b.Ideo?.name ?? "" : "";
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
            if (!ctx.relation.NullOrEmpty()) tags.Add("faith");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var b = ctx.b;
            if (b == null || b.Dead || !b.Spawned || b.Map != ctx.map) return "CC_PeoplesG_TakerGone".Translate().Resolve();
            return !WholeXenogerm.Takers(ctx.map).Contains(b) ? "CC_PEv8_XenogermTaken".Translate(b.LabelShort).Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "work" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The xenogerm written into {B} where they stand (WholeXenogerm.Begin): three hours, then the coma.</summary>
    public class DilemmaEffect_WholeXenogerm : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Play("CC_PEv8_Impact_XenogermWork".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv8_Impact_XenogermWorkBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, 1, "CC_PEv8_Impact_Xenogerm".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv8_Impact_XenogermBrief".Translate().Resolve(), "pev8xenogerm");
            sheet.Add(ImpactKind.Health, -1, "CC_PEv8_Impact_Coma".Translate(hc.b.LabelShort).Resolve(), "CC_PEv8_Impact_ComaBrief".Translate().Resolve(), "pev8coma");
            if (!hc.relation.NullOrEmpty())
                sheet.Add(ImpactKind.Mood, -1, "CC_PEv8_Impact_Faith".Translate(hc.b.LabelShort, hc.relation).Resolve(), "CC_PEv8_Impact_FaithBrief".Translate().Resolve(), "pev8faith");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.XenogermGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => WholeXenogerm.Begin(ctx as HappeningContext);
    }
}
