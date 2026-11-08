using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release G's four peoples at the colony's gate (docs/design/peoples.md, "Release G"; docs/design/peoples-third.md, step
    // 3; the defs in Defs/IncidentDefs/Rimfolk_Events_PeoplesG.xml), each only from a people of its calling at peace with
    // the colony, and every one played out on the map. Here: the Helix Works' sample from a colonist with rare genes, its
    // gene work, its vat-keeper's case and its batch gone wrong; the Lattice's repairs to the colony's mechs, its lost mech,
    // its part-trader and its engineers' turret. The Choir of the Spire and the Sunken Vault are in PeoplesG2.cs. Nothing
    // here keeps a clock or saves anything of its own: what is under way waits on the job that does it (a job loaded
    // halfway starts its target again, or the visitors go home on their hours), and the Sunken Vault's supply is a memory
    // of the peoples' memory (FactionMemory), read by the age rule (Peoples/PeopleAges.cs).

    /// <summary>What release G's touchpoints share: their jobs, the work done at their end, holding someone still for it.</summary>
    public static class PeoplesG
    {
        public static JobDef DrawJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Draw");

        public static JobDef GeneJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_GeneWork");

        public static JobDef RepairJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Repair");

        public static JobDef RelinkJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Relink");

        public static JobDef TurretJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_RaiseTurret");

        public static JobDef ListenJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Listen");

        public static JobDef OrdinaryJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Ordinary");

        public static JobDef TeachJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Teach");

        public static JobDef SkyJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesG_Sky");

        /// <summary>The Helix Works' batch gone wrong: a gene fault in their vat-grown (Defs/Misc/Rimfolk_PeoplesG_Misc.xml).</summary>
        public static HediffDef FaultDef => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GeneFault");

        public static int Now => Find.TickManager.TicksGame;

        public static bool Up(Pawn p, Map map) => PeoplesE.Up(p, map);

        /// <summary>A job of release G's own done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target, bool last)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == DrawJob) HelixSample.Drawn(pawn, target.Pawn);
                else if (job == GeneJob) HelixGenes.Written(pawn, target.Pawn);
                else if (job == RepairJob) LatticeRepair.Mended(pawn, target.Pawn, last);
                else if (job == RelinkJob) LatticeLost.Relinked(pawn, target.Pawn);
                else if (job == TurretJob) LatticeTurret.Raised(pawn, target.Cell);
                else if (job == ListenJob) ChoirListening.Listened(pawn);
                else if (job == OrdinaryJob) HappeningWorker_MissionChoirStay.Followed(pawn, target.Pawn);
                else if (job == TeachJob) VaultCraft.Taught(pawn, target.Thing);
                else if (job == SkyJob) VaultChild.SawTheSky(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples G: work done", e);
            }
        }

        /// <summary>
        /// Someone kept still where they stand for a while (a forced wait): the colony's colonist for the Works' draw or
        /// their gene work, a mech while it is mended or relinked. False: they couldn't be stopped (downed, drafted, in a
        /// mental state, not here).
        /// </summary>
        public static bool Hold(Pawn p, int ticks)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.Drafted || p.InMentalState || p.jobs == null) return false;
            var job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = Mathf.Max(60, ticks);
            job.checkOverrideOnExpire = true;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJobDef == JobDefOf.Wait;
        }

        /// <summary>
        /// One of theirs on the map sent to work at a target (or a queue of them), kept there by a lord of their own at the
        /// work's spot so their visit's duties don't call them off; their pace an errand's. False: it couldn't start.
        /// </summary>
        public static bool Errand(Pawn x, Map map, LocalTargetInfo first, List<LocalTargetInfo> rest, JobDef job, int ticks, float hours)
        {
            if (!Up(x, map) || job == null || !first.IsValid || !x.CanReach(first, PathEndMode.Touch, Danger.Some)) return false;
            x.GetLord()?.RemovePawn(x);
            LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, first.Cell, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map, new List<Pawn> { x });
            return PeoplesE.Start(x, job, first, rest, ticks);
        }

        /// <summary>A line of the event's own with these tags, said over their head.</summary>
        public static void Say(Pawn x, HappeningExtension ext, HappeningContext ctx, params string[] tags) => PeoplesD.Say(x, ext, ctx, tags);

        /// <summary>A line of the event's own with this tag (the first of its kind), said by someone of theirs outside an answer.</summary>
        public static void SayLine(Pawn x, IncidentDef incident, string tag) => PeoplesF.SayLine(x, incident, tag);

        /// <summary>Sends one of theirs, or a group of them, home: people walk off as visitors do, a mech with them.</summary>
        public static void Home(IEnumerable<Pawn> pawns)
        {
            var list = pawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Faction != null && !p.Faction.IsPlayer).Distinct().ToList();
            if (list.Count == 0) return;
            foreach (var p in list) p.GetLord()?.RemovePawn(p);
            foreach (var g in list.GroupBy(p => new { p.Map, p.Faction }))
                LordMaker.MakeNewLord(g.Key.Faction, new LordJob_ExitMapBest(Pace.Errand, false, true), g.Key.Map, g.ToList());
        }

        /// <summary>Who came with an event's visitor: the visitor and the rest of their party.</summary>
        public static List<Pawn> Party(HappeningContext ctx) => ctx == null ? new List<Pawn>() : ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();

        /// <summary>The colony's grown people who could keep still for the Works: up, about and not a child.</summary>
        public static IEnumerable<Pawn> Grown(Map map) => PeoplesD.Able(map).Where(p => !p.DevelopmentalStage.Juvenile());
    }

    /// <summary>
    /// Release G's own work at a cell, a thing or a person, or a queue of them: there, a while at it (a cross, sparks, the
    /// hum, a bar), then what it was for (PeoplesG.WorkDone); the next in the queue, if any. Someone of the colony's or a
    /// mech it works on is held still while it works. Its place in the queue isn't kept with the game: a job loaded halfway
    /// starts its current target again.
    /// </summary>
    public class JobDriver_PeoplesG : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PeoplesGNext");
            next.initAction = () =>
            {
                if (!job.targetA.IsValid)
                {
                    if (!job.targetQueueA.NullOrEmpty())
                    {
                        job.targetA = job.targetQueueA[0];
                        job.targetQueueA.RemoveAt(0);
                    }
                    else
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                }
                // The one worked on keeps still while the work is done: the colony's own for the draw or the gene work, a mech while it is mended or relinked.
                var on = job.targetA.Pawn;
                if (on != null && on != pawn && (job.def == PeoplesG.DrawJob || job.def == PeoplesG.GeneJob || job.def == PeoplesG.RepairJob || job.def == PeoplesG.RelinkJob))
                    PeoplesG.Hold(on, job.count + 1500);
            };
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return next;
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PeoplesGWork");
            work.tickIntervalAction = delta =>
            {
                var t = job.targetA;
                if (!t.IsValid) return;
                pawn.rotationTracker.FaceCell(t.Cell);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                if (job.def == PeoplesG.DrawJob || job.def == PeoplesG.GeneJob) FleckMaker.ThrowMetaIcon(t.Cell, map, FleckDefOf.HealingCross);
                else if (job.def == PeoplesG.ListenJob) FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating);
                else if (job.def == PeoplesG.RepairJob || job.def == PeoplesG.RelinkJob || job.def == PeoplesG.TurretJob) FleckMaker.ThrowMicroSparks(t.Cell.ToVector3Shifted(), map);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("PeoplesGDone");
            done.initAction = () =>
            {
                bool last = job.targetQueueA.NullOrEmpty();
                PeoplesG.WorkDone(pawn, job.def, job.targetA, last);
                job.targetA = LocalTargetInfo.Invalid;
                if (!last) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The Helix Works: the sample =====================

    /// <summary>The sample: whose genes are rare enough to the Works, and the draw itself when it is done.</summary>
    public static class HelixSample
    {
        private sealed class Draw
        {
            public Faction f;
            public int price;
            public IncidentDef incident;
        }

        /// <summary>The draws under way this session, by the buyer's id (a draw loaded halfway is paid for nothing; they go home on their hours).</summary>
        private static readonly Dictionary<int, Draw> draws = new Dictionary<int, Draw>();

        /// <summary>A gene the game marks as archite (no one makes those any more).</summary>
        public static bool Archite(GeneDef g) => g != null && g.biostatArc > 0;

        /// <summary>How rare someone's genes are to the Works (PeoplesGMath.Rarity): archite genes, a xenotype's own genes, nothing for a plain baseliner.</summary>
        public static int RarityOf(Pawn p)
        {
            if (!ModsConfig.BiotechActive || p?.genes == null) return 0;
            var genes = p.genes.GenesListForReading;
            int arc = genes.Count(g => Archite(g.def));
            int own = genes.Count(g => !Archite(g.def) && (g.def.biostatCpx != 0 || g.def.biostatMet != 0));
            bool baseliner = p.genes.Xenotype == XenotypeDefOf.Baseliner && p.genes.CustomXenotype == null && p.genes.Xenogenes.Count == 0;
            return PeoplesGMath.Rarity(arc, own, baseliner);
        }

        /// <summary>The colony's grown person whose genes are rarest, if any is worth a sample.</summary>
        public static Pawn Rarest(Map map, out int rarity)
        {
            rarity = 0;
            Pawn best = null;
            if (map == null) return null;
            foreach (var p in PeoplesG.Grown(map))
            {
                int r = RarityOf(p);
                if (r <= rarity) continue;
                rarity = r;
                best = p;
            }
            if (!PeoplesGMath.WorthASample(rarity)) best = null;
            return best;
        }

        public static void Pending(Pawn buyer, HappeningContext ctx)
        {
            if (buyer == null || ctx == null) return;
            draws[buyer.thingIDNumber] = new Draw { f = ctx.faction, price = ctx.count, incident = ctx.incident };
        }

        /// <summary>
        /// The draw done: a little blood from the colonist's arm (below the game's first stage of blood loss, gone in a few
        /// hours; their answer, so not a harm the budget rolls), the sample-buyer's silver paid, told; then their party goes.
        /// </summary>
        public static void Drawn(Pawn buyer, Pawn donor)
        {
            if (buyer == null) return;
            var map = buyer.MapHeld ?? donor?.MapHeld;
            draws.TryGetValue(buyer.thingIDNumber, out var d);
            draws.Remove(buyer.thingIDNumber);
            if (donor != null && !donor.Dead && donor.health != null)
            {
                HealthUtility.AdjustSeverity(donor, HediffDefOf.BloodLoss, PeoplesGMath.DrawSeverity);
                if (donor.Spawned) FleckMaker.ThrowMetaIcon(donor.Position, donor.Map, FleckDefOf.HealingCross);
            }
            if (d != null && map != null && d.price > 0)
                Couriers.PaySilver(map, donor != null && donor.Spawned ? donor.Position : buyer.Spawned ? buyer.Position : IntVec3.Invalid, d.price, d.f, buyer);
            if (d != null) PeoplesG.SayLine(buyer, d.incident, "done");
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PeoplesG_Drawn".Translate((buyer.LabelShort).Named("BUYER"), (donor?.LabelShort ?? "?").Named("PAWN"),
                (d?.price ?? 0).Named("COUNT")).Resolve(), donor != null && donor.Spawned ? new LookTargets(donor) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            var lord = buyer.GetLord();
            PeoplesG.Home(lord?.ownedPawns?.ToList() ?? new List<Pawn> { buyer });
        }
    }

    /// <summary>
    /// The Helix Works' sample-buyer and a vat-keeper come to the gate: the Works have heard that one of the colony's people
    /// carries rare genes ({B}: an archite gene, or a xenotype's own), and they would pay {COUNT} silver for a sample, a
    /// small blood draw from the arm done where {B} stands. Or with the colony's doctor {A} at the buyer's side, learning
    /// how it's done. {TALE} is what is rare in {B}.
    /// </summary>
    public class HappeningWorker_HelixSample : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Editors) != null && HelixSample.Rarest(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            ctx.b = HelixSample.Rarest(ctx.map, out int rarity);
            if (ctx.b == null) return false;
            ctx.variant = rarity;
            ctx.count = PeoplesGMath.SamplePrice(rarity);
            var genes = ctx.b.genes.GenesListForReading;
            var arc = genes.Where(g => HelixSample.Archite(g.def)).Select(g => g.def.label).Distinct().ToList();
            ctx.note = arc.Count > 0 ? "archite" : null;
            ctx.tale = arc.Count > 0
                ? "CC_PeoplesG_RareArchite".Translate(TextUtil.JoinAnd(arc)).Resolve()
                : "CC_PeoplesG_RareXeno".Translate(ctx.b.genes.XenotypeLabel).Resolve();
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => p != ctx.b && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) ?? ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "archite") tags.Add("archite");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) ? "CC_PeoplesG_DonorGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sample" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The sample taken: {B} keeps still where they are, the sample-buyer walks over and draws it (HelixSample.Drawn); with the play-outs off, at once.</summary>
    public class DilemmaEffect_HelixSample : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.b == null) return;
            sheet.Play("CC_PeoplesG_Impact_Sample".Translate(ctx.b.LabelShort).Resolve(), "CC_PeoplesG_Impact_SampleBrief".Translate().Resolve());
            sheet.Silver(ctx.count);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            var x = ctx?.stranger;
            var donor = ctx?.b;
            if (hc == null || map == null || x == null) return;
            HelixSample.Pending(x, hc);
            if (PlayOuts.On && PeoplesG.Up(x, map) && PeoplesG.Up(donor, map)
                && PeoplesG.Errand(x, map, donor, null, PeoplesG.DrawJob, PeoplesGMath.DrawTicks, 4f)) return;
            HelixSample.Drawn(x, donor);
        }
    }

    // ===================== The Helix Works: gene work =====================

    /// <summary>The Works' gene work: which of their genes a colonist could take, and the gene written in when the work is done.</summary>
    public static class HelixGenes
    {
        private static readonly Dictionary<int, GeneDef> pending = new Dictionary<int, GeneDef>();

        /// <summary>Their genes this colonist could take: one they lack, conflicting with nothing they carry.</summary>
        public static List<GeneDef> Fits(Pawn p)
        {
            var list = new List<GeneDef>();
            if (!ModsConfig.BiotechActive || p?.genes == null) return list;
            foreach (string name in PeoplesGMath.Offered)
            {
                var def = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                if (def == null || p.genes.GetGene(def) != null) continue;
                if (p.genes.GenesListForReading.Any(g => g.def.ConflictsWith(def))) continue;
                list.Add(def);
            }
            return list;
        }

        /// <summary>The colony's grown people who could take a gene now: none still replicating a xenogerm.</summary>
        public static IEnumerable<Pawn> Takers(Map map) =>
            PeoplesG.Grown(map).Where(p => p.genes != null && HediffDefOf.XenogermReplicating != null && !p.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating) && Fits(p).Count > 0);

        public static void Pending(Pawn sequencer, GeneDef gene)
        {
            if (sequencer != null && gene != null) pending[sequencer.thingIDNumber] = gene;
        }

        /// <summary>
        /// The gene work done: the gene written into them as a xenogene, the game's own mark of a xenogerm replicating on
        /// them (so nobody puts another in too soon), told; the sequencer goes home.
        /// </summary>
        public static void Written(Pawn sequencer, Pawn taker)
        {
            if (sequencer == null) return;
            pending.TryGetValue(sequencer.thingIDNumber, out var gene);
            pending.Remove(sequencer.thingIDNumber);
            var map = sequencer.MapHeld ?? taker?.MapHeld;
            if (gene != null && taker != null && !taker.Dead && taker.genes != null && taker.genes.GetGene(gene) == null)
            {
                taker.genes.AddGene(gene, true);
                if (HediffDefOf.XenogermReplicating != null && !taker.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                    taker.health.AddHediff(HediffDefOf.XenogermReplicating);
                if (taker.Spawned) FleckMaker.ThrowMetaIcon(taker.Position, taker.Map, FleckDefOf.HealingCross);
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PeoplesG_GeneWritten".Translate(sequencer.LabelShort.Named("SEQ"), taker.LabelShort.Named("PAWN"),
                    gene.label.Named("GENE")).Resolve(), taker.Spawned ? new LookTargets(taker) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(new List<Pawn> { sequencer });
        }
    }

    /// <summary>
    /// A sequencer of the Helix Works comes to the gate offering gene work: one gene of the Works' own lab ({TALE}) written
    /// into {B} where they stand, two hours with the Works' instruments, for {COUNT} silver or one of the colony's genepacks.
    /// </summary>
    public class HappeningWorker_HelixGeneWork : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Editors) != null && HelixGenes.Takers(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            ctx.b = HelixGenes.Takers(ctx.map).RandomElementWithFallback();
            if (ctx.b == null) return false;
            var gene = HelixGenes.Fits(ctx.b).RandomElementWithFallback();
            if (gene == null) return false;
            ctx.note = gene.defName;
            ctx.tale = gene.label;
            ctx.count = PeoplesGMath.GenePrice(gene.biostatMet, gene.biostatCpx);
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
            if (PlayCarry.Have(ctx.map, "Genepack", null) > 0) tags.Add("genepack");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PeoplesG_TakerGone".Translate().Resolve();
            var gene = DefDatabase<GeneDef>.GetNamedSilentFail(ctx.note ?? "");
            return gene == null || !HelixGenes.Fits(ctx.b).Contains(gene) ? "CC_PeoplesG_GeneTaken".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "gene" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The gene work done on {B}: they keep still, the sequencer works on them two hours (HelixGenes.Written); with the play-outs off, at once.</summary>
    public class DilemmaEffect_HelixGene : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Add(ImpactKind.Health, 1, "CC_PeoplesG_Impact_Gene".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PeoplesG_Impact_GeneBrief".Translate().Resolve(), "genework");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (hc == null || map == null || x == null) return;
            var gene = DefDatabase<GeneDef>.GetNamedSilentFail(hc.note ?? "");
            HelixGenes.Pending(x, gene);
            if (PlayOuts.On && PeoplesG.Up(x, map) && PeoplesG.Up(hc.b, map)
                && PeoplesG.Errand(x, map, hc.b, null, PeoplesG.GeneJob, PeoplesGMath.GeneWorkTicks, 5f)) return;
            HelixGenes.Written(x, hc.b);
        }
    }

    // ===================== The Helix Works: the vat-keeper's case =====================

    /// <summary>
    /// A vat-keeper of the Helix Works comes with a case of the Works' medicine and drugs (the Works' trade without Biotech
    /// too): medicine, a little glitterworld medicine and penoxycyline ({TALE}) for {COUNT} silver, carried out to them and
    /// the case set down at the gate. Or the colony's doctor {A} looks through it with them and learns.
    /// </summary>
    public class HappeningWorker_HelixCase : HappeningWorker_Visitors
    {
        public static List<(ThingDef def, int count)> Case(Map map)
        {
            float wf = LivingEvents.WealthFactor(map);
            var list = new List<(ThingDef, int)> { (ThingDefOf.MedicineIndustrial, PeoplesGMath.CaseMedicine(wf)) };
            var ultra = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineUltratech");
            if (ultra != null) list.Add((ultra, PeoplesGMath.CaseUltratech(wf)));
            var peno = DefDatabase<ThingDef>.GetNamedSilentFail("Penoxycyline");
            if (peno != null) list.Add((peno, PeoplesGMath.CasePenoxycyline(wf)));
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Editors) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            var crate = Case(ctx.map);
            ctx.note = HappeningWorker_UnionGoods.Write(crate);
            ctx.count = PeoplesGMath.CasePrice(crate.Sum(c => c.def.BaseMarketValue * c.count));
            ctx.tale = TextUtil.JoinAnd(crate.Select(c => Couriers.Amount(c.count, null, c.def)).ToList());
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (!ModsConfig.BiotechActive) tags.Add("nogenes");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sold" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>A crate of an event's (its note, HappeningWorker_UnionGoods.Write) set down at the gate by the visitor: the vat-keeper's case, the vault's last spacer goods.</summary>
    public class DilemmaEffect_PeoplesGCrate : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            foreach (var (def, n) in HappeningWorker_UnionGoods.Read(hc.note)) sheet.Goods(1, def, n);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null) return;
            var goods = HappeningWorker_UnionGoods.Read(hc.note).SelectMany(c => Couriers.Make(c.def, c.count)).ToList();
            if (goods.Count == 0) return;
            var x = ctx.stranger;
            Couriers.Loot(goods, map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x);
        }
    }

    // ===================== The Helix Works: a batch gone wrong =====================

    /// <summary>
    /// A vat-keeper of the Helix Works comes to the gate with a batch gone wrong: {COUNT} of the Works' vat-grown, young and
    /// sick with a gene fault that wants tending. The colony's doctor {A} may see to them where they stand, with a medicine
    /// each from the stores; or medicine is carried out to the vat-keeper ({TALE}); or the gate stays shut. The Works think
    /// the better of a colony that helped.
    /// </summary>
    public class HappeningWorker_HelixBatch : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesG.FaultDef != null && PeoplesD.Friendly(PeoplesMath.Editors) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            var fault = PeoplesG.FaultDef;
            if (ctx.faction == null || fault == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = PeoplesGMath.BatchSize(Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var v = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_Vatborn"), ctx.faction, new FloatRange(18f, 21f));
                if (v == null) continue;
                var h = HediffMaker.MakeHediff(fault, v);
                h.Severity = PeoplesGMath.FaultStart;
                v.health.AddHediff(h);
                ctx.group.Add(v);
            }
            if (ctx.group.Count == 0) return false;
            ctx.count = ctx.group.Count;
            ctx.variant = ctx.group.Count * PeoplesGMath.BatchMedicineEach;
            ctx.tale = ctx.variant.ToString();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !p.WorkTagIsDisabled(WorkTags.Caring))
                    ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.group.Count == 1) tags.Add("one");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "tended" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's doctor {A} sees to each of the batch where they stand, a medicine from the stores for each (OutsiderTend); else the medicine goes out as before.</summary>
    public class DilemmaEffect_HelixBatch : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Play("CC_PeoplesG_Impact_Batch".Translate(hc.a.LabelShort, hc.group.Count).Resolve(), "CC_PeoplesG_Impact_BatchBrief".Translate().Resolve());
            sheet.Goods(-1, "CC_Dilemma_Medicine".Translate().Resolve(), Mathf.Max(1, hc.group.Count), "medicine");
        }

        public override string Unavailable(DilemmaContext ctx) =>
            new DilemmaEffect_Consume { what = "medicine", count = Mathf.Max(1, (ctx as HappeningContext)?.group.Count ?? 1) }.Unavailable(ctx);

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null) return;
            var patients = hc.group.Where(p => PeoplesG.Up(p, map)).ToList();
            if (patients.Count > 0 && hc.a != null && OutsiderTend.Send(hc.a, patients)) return;
            PlayCarry.Take(map, "medicine", null, Mathf.Max(1, hc.group.Count));
        }
    }

    // ===================== The Lattice: repairs =====================

    /// <summary>The colony's hurt mechs, and the mending done when the mech-tender has worked on one.</summary>
    public static class LatticeRepair
    {
        /// <summary>The colony's mechs here that want mending (the game's own test), the worst first, at most a handful.</summary>
        public static List<Pawn> Hurt(Map map)
        {
            if (!ModsConfig.BiotechActive || map == null) return new List<Pawn>();
            return map.mapPawns.SpawnedColonyMechs.Where(m => m != null && !m.Dead && !m.Downed && !m.Position.Fogged(map) && MechRepairUtility.CanRepair(m))
                .OrderBy(m => m.health.summaryHealth.SummaryHealthPercent).Take(PeoplesGMath.RepairMax).ToList();
        }

        public static int Wounds(IEnumerable<Pawn> mechs) => mechs.Sum(m => m.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart));

        /// <summary>
        /// A mech mended: the game's own repair run until it wants none (its wounds closed, missing parts grown back, as a
        /// mechanitor's repair does it); at the last, told, and the mech-tender goes home.
        /// </summary>
        public static void Mended(Pawn tender, Pawn mech, bool last)
        {
            if (mech != null && !mech.Dead && ModsConfig.BiotechActive)
            {
                for (int i = 0; i < 30000 && MechRepairUtility.CanRepair(mech); i++) MechRepairUtility.RepairTick(mech);
                if (mech.Spawned) FleckMaker.ThrowMicroSparks(mech.DrawPos, mech.Map);
            }
            if (!last || tender == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, tender.MapHeld, "CC_PeoplesG_Mended".Translate(tender.LabelShort.Named("PAWN")).Resolve(),
                mech != null && mech.Spawned ? new LookTargets(mech) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            PeoplesG.Home(new List<Pawn> { tender });
        }
    }

    /// <summary>
    /// A mech-tender of the Lattice comes to the gate: the colony's mechs are hurt ({TALE}), and the Lattice would mend
    /// them, a while at each, for {COUNT} silver or one of the colony's basic subcores.
    /// </summary>
    public class HappeningWorker_LatticeRepair : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && LatticeRepair.Hurt(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            if (ctx.faction == null) return false;
            var hurt = LatticeRepair.Hurt(ctx.map);
            if (hurt.Count == 0) return false;
            int wounds = LatticeRepair.Wounds(hurt);
            ctx.variant = hurt.Count;
            ctx.count = PeoplesGMath.RepairPrice(hurt.Count, wounds);
            ctx.tale = "CC_PeoplesG_HurtMechs".Translate(hurt.Count, wounds).Resolve();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_MechTender"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Able(ctx.map).FirstOrDefault(MechanitorUtility.IsMechanitor) ?? PeoplesD.Best(ctx.map, SkillDefOf.Crafting);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.variant == 1) tags.Add("one");
            if (PlayCarry.Have(ctx.map, "SubcoreBasic", null) > 0) tags.Add("subcore");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && LatticeRepair.Hurt(ctx.map).Count == 0 ? "CC_PeoplesG_NothingHurt".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "mend" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mech-tender goes from mech to mech and mends each (LatticeRepair.Mended), each held still at its turn; with the play-outs off, at once.</summary>
    public class DilemmaEffect_LatticeRepair : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesG_Impact_Repair".Translate((ctx as HappeningContext)?.variant ?? 0).Resolve(), "CC_PeoplesG_Impact_RepairBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (map == null || x == null) return;
            var hurt = LatticeRepair.Hurt(map);
            if (hurt.Count == 0) return;
            var queue = hurt.Select(m => (LocalTargetInfo)m).ToList();
            if (PlayOuts.On && PeoplesG.Errand(x, map, queue[0], queue.Skip(1).ToList(), PeoplesG.RepairJob, PeoplesGMath.RepairTicks, 2f + hurt.Count)) return;
            for (int i = 0; i < hurt.Count; i++) LatticeRepair.Mended(x, hurt[i], i == hurt.Count - 1);
        }
    }

    // ===================== The Lattice: the lost mech =====================

    /// <summary>The Lattice's lost mech: the light mechs that wander off, kept by the colony, relinked and walked home, or let go.</summary>
    public static class LatticeLost
    {
        /// <summary>The light working mechs of theirs that stray: a lifter, a constructoid, a cleansweeper, a paramedic.</summary>
        public static readonly string[] Strays = { "Mech_Lifter", "Mech_Constructoid", "Mech_Cleansweeper", "Mech_Paramedic" };

        public static PawnKindDef Kind() => Strays.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k != null).RandomElementWithFallback();

        /// <summary>The colony's mechanitor who could take the mech on now (the game's own test: bandwidth and all), or null.</summary>
        public static Pawn Keeper(Map map, Pawn mech)
        {
            if (!ModsConfig.BiotechActive || map == null || mech == null) return null;
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => MechanitorUtility.IsMechanitor(p) && MechanitorUtility.CanControlMech(p, mech).Accepted);
        }

        private static readonly Dictionary<int, IncidentDef> wardens = new Dictionary<int, IncidentDef>();

        public static void Pending(Pawn warden, IncidentDef incident)
        {
            if (warden != null) wardens[warden.thingIDNumber] = incident;
        }

        /// <summary>The mech relinked by the link-warden: the two walk home together, and the Lattice thinks the better of the colony.</summary>
        public static void Relinked(Pawn warden, Pawn mech)
        {
            if (warden == null) return;
            var f = warden.Faction;
            wardens.TryGetValue(warden.thingIDNumber, out var incident);
            wardens.Remove(warden.thingIDNumber);
            PeoplesG.SayLine(warden, incident, "relinked");
            if (mech != null && mech.Spawned) FleckMaker.ThrowMicroSparks(mech.DrawPos, mech.Map);
            if (f != null && !f.IsPlayer && f.HasGoodwill) WorldSituationWorker.Goodwill(f, PeoplesGMath.ReturnedGoodwill);
            Tell.Message(NewsKind.World, Importance.Notable, warden.MapHeld, "CC_PeoplesG_Relinked".Translate(warden.LabelShort.Named("PAWN"), (mech?.LabelShort ?? "?").Named("MECH")).Resolve(),
                new LookTargets(warden), MessageTypeDefOf.PositiveEvent);
            PeoplesG.Home(new List<Pawn> { warden, mech });
        }

        /// <summary>Kept: the mech is the colony's, its mechanitor its overseer; the Lattice thinks the worse of the colony, told.</summary>
        public static bool Keep(Pawn mech, Pawn keeper, Faction f)
        {
            if (mech == null || mech.Dead || keeper == null) return false;
            mech.GetLord()?.RemovePawn(mech);
            mech.SetFaction(Faction.OfPlayer);
            if (keeper.relations != null && MechanitorUtility.GetOverseer(mech) == null) keeper.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            if (f != null && !f.IsPlayer && f.HasGoodwill) WorldSituationWorker.Goodwill(f, PeoplesGMath.KeptGoodwill);
            Tell.Message(NewsKind.World, Importance.Notable, mech.MapHeld, "CC_PeoplesG_Kept".Translate(mech.LabelShort.Named("MECH"), keeper.LabelShort.Named("PAWN"),
                (f?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(mech), MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    /// <summary>
    /// A mech of the Lattice ({THING}) has wandered onto the colony's map, its link to them broken: it drifts near where it
    /// came in. The colony may send word, and a link-warden comes to relink it and walk it home; or keep it, the colony's
    /// mechanitor {A} its overseer, which the Lattice won't forgive soon; or let it find its own way home.
    /// </summary>
    public class HappeningWorker_LatticeLostMech : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && LatticeLost.Kind() != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            var kind = LatticeLost.Kind();
            if (ctx.faction == null || kind == null) return false;
            ctx.stranger = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, ctx.faction, PawnGenerationContext.NonPlayer, ctx.map.Tile));
            if (ctx.stranger == null) return false;
            ctx.thing = ctx.stranger.def;
            ctx.a = ctx.map.mapPawns.FreeColonistsSpawned.FirstOrDefault(MechanitorUtility.IsMechanitor) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(ctx.map.Center, ctx.map, out ctx.spot)) ctx.spot = IntVec3.Invalid;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, ctx.spot.IsValid ? ctx.spot : (IntVec3?)null, PeoplesGMath.LostHours) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(LatticeLost.Keeper(ctx.map, ctx.stranger) != null ? "mechanitor" : "nomechanitor");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var mech = ctx.stranger;
            if (mech == null || mech.Dead || !mech.Spawned || mech.Map != ctx.map || mech.Faction == Faction.OfPlayer) return option == 2 ? null : "CC_PeoplesG_MechGone".Translate().Resolve();
            return option == 1 && LatticeLost.Keeper(ctx.map, mech) == null ? "CC_PeoplesG_NoKeeper".Translate().Resolve() : null;
        }

    }

    /// <summary>Word sent: a link-warden of the Lattice walks in from the edge to the mech, relinks it and walks it home (LatticeLost.Relinked); with the play-outs off, at once.</summary>
    public class DilemmaEffect_LatticeReturn : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Play("CC_PeoplesG_Impact_Return".Translate(ctx?.thing?.label ?? "?").Resolve(), "CC_PeoplesG_Impact_ReturnBrief".Translate().Resolve());
            if (ctx?.faction != null && ctx.faction.HasGoodwill) sheet.Goodwill(ctx.faction, PeoplesGMath.ReturnedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var mech = ctx?.stranger;
            var f = ctx?.faction;
            if (map == null || mech == null || f == null || mech.Dead) return;
            var warden = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Lattice_LinkWarden"), f, new FloatRange(25f, 55f));
            LatticeLost.Pending(warden, (ctx as HappeningContext)?.incident);
            if (warden != null && PlayOuts.On && mech.Spawned && mech.Map == map && Arrivals.Spawn(warden, map)
                && PeoplesG.Errand(warden, map, mech, null, PeoplesG.RelinkJob, PeoplesGMath.RelinkTicks, 6f)) return;
            if (warden != null && !warden.Spawned) Arrivals.Discard(warden);
            if (f.HasGoodwill) WorldSituationWorker.Goodwill(f, PeoplesGMath.ReturnedGoodwill);
            PeoplesG.Home(new List<Pawn> { mech });
        }
    }

    /// <summary>Kept: the mech is the colony's, {A} its overseer, at a cost to the Lattice's goodwill (LatticeLost.Keep).</summary>
    public class DilemmaEffect_LatticeKeep : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var keeper = LatticeLost.Keeper(ctx?.map, ctx?.stranger);
            sheet.Add(ImpactKind.People, 1, "CC_PeoplesG_Impact_Keep".Translate(ctx?.thing?.label ?? "?", keeper?.LabelShort ?? "?").Resolve(), "CC_PeoplesG_Impact_KeepBrief".Translate().Resolve(), "keepmech");
            if (ctx?.faction != null && ctx.faction.HasGoodwill) sheet.Goodwill(ctx.faction, PeoplesGMath.KeptGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => LatticeLost.Keep(ctx?.stranger, LatticeLost.Keeper(ctx?.map, ctx?.stranger), ctx?.faction);
    }

    /// <summary>Let go: the mech finds its own way home, walking off the map.</summary>
    public class DilemmaEffect_LatticeMechHome : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesG_Impact_MechHome".Translate(ctx?.thing?.label ?? "?").Resolve(), "CC_PeoplesG_Impact_MechHomeBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx) => PeoplesG.Home(new List<Pawn> { ctx?.stranger });
    }

    // ===================== The Lattice: the part-trader =====================

    /// <summary>
    /// The colony has subcores and mech chips in its stores (PlayCarry.MechParts), and the Lattice's part-trader comes for
    /// them: carried out and paid {COUNT} silver, a fifth over their worth ({TALE}). The colony's crafter {A} may look at
    /// the Lattice's tools with them.
    /// </summary>
    public class HappeningWorker_LatticeParts : HappeningWorker_Visitors
    {
        public static float Worth(Map map, out int items)
        {
            var stock = PlayCarry.Stock(map, PlayCarry.MechParts, null).ToList();
            items = stock.Sum(t => t.stackCount);
            return stock.Sum(t => t.MarketValue * t.stackCount);
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && Worth(map, out _) >= PeoplesGMath.PartsMinWorth && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            if (ctx.faction == null) return false;
            float worth = Worth(ctx.map, out int items);
            if (worth < PeoplesGMath.PartsMinWorth || items <= 0) return false;
            ctx.count = PeoplesGMath.PartsPrice(worth);
            ctx.variant = items;
            ctx.tale = PlayCarry.Label(PlayCarry.MechParts, null) + " (" + items + ")";
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Crafting) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && Worth(ctx.map, out _) < PeoplesGMath.PartsMinWorth ? "CC_PeoplesG_PartsGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "parts" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The subcores and chips carried out to the part-trader (Couriers.TakeFor), the silver paid at the gate (the worker's Resolved).</summary>
    public class DilemmaEffect_LatticeParts : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = ctx?.map;
            if (map == null) return;
            HappeningWorker_LatticeParts.Worth(map, out int items);
            if (items > 0) sheet.Goods(-1, PlayCarry.Label(PlayCarry.MechParts, null), items, PlayCarry.MechParts);
            sheet.Silver(ctx.count);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            int items = PlayCarry.Have(map, PlayCarry.MechParts, null);
            if (items > 0 && !Couriers.TakeFor(ctx, PlayCarry.MechParts, null, items)) PlayCarry.Take(map, PlayCarry.MechParts, null, items);
        }
    }

    // ===================== The Lattice: the engineers' turret =====================

    /// <summary>The Lattice's engineers' turret (their trade without Biotech too): where it stands, and raising it.</summary>
    public static class LatticeTurret
    {
        public static ThingDef Def => DefDatabase<ThingDef>.GetNamedSilentFail("Turret_MiniTurret");

        /// <summary>A cell by the gate the turret can go on: clear, standable ground the game would let it be built on, a few cells in from the gate.</summary>
        public static IntVec3 Site(Map map)
        {
            var def = Def;
            var gate = PeoplesD.GateSpot(map);
            if (map == null || def == null || gate == null) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(gate.Value, PeoplesGMath.TurretMaxCells, true).Skip(9))
            {
                if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstBuilding(map) != null || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null) continue;
                if (c.GetPlant(map) is Plant p && p.def.plant.IsTree) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(def, c, Rot4.North, map, false, null, null, null, false, false, false).Accepted) continue;
                if (!map.reachability.CanReach(gate.Value, c, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The turret raised at the cell, the colony's from now on, told (it wants power to fire); the engineer goes home.</summary>
        public static void Raised(Pawn by, IntVec3 c)
        {
            var map = by?.Map;
            var def = Def;
            if (map == null || def == null) return;
            if (!c.IsValid || c.GetFirstBuilding(map) != null) c = Site(map);
            if (!c.IsValid) return;
            var turret = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            turret.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(turret, c, map);
            FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(), map);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesG_TurretRaised".Translate(by.LabelShort.Named("PAWN")).Resolve(), new LookTargets(turret),
                MessageTypeDefOf.PositiveEvent);
            if (by.Faction != null && !by.Faction.IsPlayer) PeoplesG.Home(new List<Pawn> { by });
        }
    }

    /// <summary>
    /// An engineer of the Lattice comes to the gate with a turret in pieces on a cart: they would raise it by the gate for
    /// {COUNT} silver (it wants the colony's power to fire). Or with the colony's builder {A} beside them, learning.
    /// </summary>
    public class HappeningWorker_LatticeTurret : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && (int)Ages.Ours >= 2 && LatticeTurret.Def != null && LatticeTurret.Site(map).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            var def = LatticeTurret.Def;
            if (ctx.faction == null || def == null) return false;
            ctx.spot = LatticeTurret.Site(ctx.map);
            if (!ctx.spot.IsValid) return false;
            ctx.thing = def;
            ctx.count = PeoplesGMath.TurretPrice(def.BaseMarketValue);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_Engineer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Construction) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (!ModsConfig.BiotechActive) tags.Add("nomechs");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !LatticeTurret.Site(ctx.map).IsValid ? "CC_PeoplesG_NoTurretSite".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "turret" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The engineer raises the turret by the gate (LatticeTurret.Raised); with the play-outs off, at once.</summary>
    public class DilemmaEffect_LatticeTurret : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing == null) return;
            sheet.Play("CC_PeoplesG_Impact_Turret".Translate(ctx.thing.label).Resolve(), "CC_PeoplesG_Impact_TurretBrief".Translate().Resolve());
            sheet.Goods(1, ctx.thing, 1);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (hc == null || map == null) return;
            var site = hc.spot.IsValid && hc.spot.GetFirstBuilding(map) == null ? hc.spot : LatticeTurret.Site(map);
            if (!site.IsValid) return;
            if (PlayOuts.On && PeoplesG.Errand(x, map, site, null, PeoplesG.TurretJob, PeoplesGMath.TurretHours * GenDate.TicksPerHour, PeoplesGMath.TurretHours + 3)) return;
            if (x != null && x.Spawned) LatticeTurret.Raised(x, site);
            else
            {
                var def = LatticeTurret.Def;
                if (def == null) return;
                var turret = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                turret.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(turret, site, map);
            }
        }
    }
}
