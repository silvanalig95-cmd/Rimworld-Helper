using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 82. A field lab (the Helix Works) =====================

    /// <summary>One piece of the field lab's work the colony asked for: a gene written into a colonist, or a sample drawn from one.</summary>
    public class LabTask : IExposable
    {
        public string kind;
        public Pawn pawn;
        public string gene;
        public int price;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref gene, "gene");
            Scribe_Values.Look(ref price, "price");
        }
    }

    /// <summary>The field lab's week: the work the colony asked for, the samples they took, their beast, and how it turned.</summary>
    public class LabState : CampWorkState
    {
        public List<LabTask> tasks = new List<LabTask>();
        public Pawn beast;
        public int sampled;
        public bool looseRolled, loose, sickRolled, sick;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref tasks, "tasks", LookMode.Deep);
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref sampled, "sampled");
            Scribe_Values.Look(ref looseRolled, "looseRolled");
            Scribe_Values.Look(ref loose, "loose");
            Scribe_Values.Look(ref sickRolled, "sickRolled");
            Scribe_Values.Look(ref sick, "sick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tasks ??= new List<LabTask>();
                tasks.RemoveAll(t => t?.pawn == null);
            }
        }
    }

    /// <summary>
    /// A field lab (item 82): two sequencers and a vat-keeper of the Helix Works camp a week by the colony, sampling the land's beasts and
    /// plants by day. All week their gene work and their sample are open on the camp's page (round G's gene work and sample, done at the
    /// colonist's side by a sequencer of the camp, with the same hold and instruments), and they buy genepacks. With Vanilla Genetics
    /// Expanded a young hybrid of theirs comes with them. It can turn, once a stay each: the hybrid gets loose and turns on whoever is
    /// near (the game's own manhunter, the camps' beasts' kind in the shared harm budget), or a vat-grown of theirs falls ill with a gene
    /// fault (the camps' fever kind, the shared harm and illness budgets), and the colony's doctor may see to them.
    /// </summary>
    public static class FieldLab
    {
        public const string EventDef = "Rimfolk_Evt_FieldLab";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_FieldLab");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static LabState StateOf(Camp c) => c?.workState as LabState;

        private static bool Sequencer(Pawn p) => p?.kindDef?.defName == "Rimfolk_HelixWorks_Sequencer";

        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_Sequencer"), ctx.faction, new FloatRange(28f, 60f)));
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_Sequencer"), ctx.faction, new FloatRange(24f, 50f)));
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, new FloatRange(24f, 55f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents8Math.LabDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                var st = new LabState();
                c.workState = st;
                Beast(c, st);
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the field lab", e);
                return null;
            }
        }

        /// <summary>With Vanilla Genetics Expanded, a young hybrid of theirs walks in with them, in their group.</summary>
        private static void Beast(Camp c, LabState st)
        {
            var map = c.map;
            var kind = HybridBeast.Kinds(map).RandomElementWithFallback();
            var with = c.members.FirstOrDefault(p => p != null && p.Spawned);
            if (kind == null || with == null || c.lord == null) return;
            try
            {
                var beast = HybridBeast.Young(kind, c.faction, map.Tile);
                if (beast == null) return;
                GenSpawn.Spawn(beast, CellFinder.RandomClosewalkCellNear(with.Position, map, 3), map);
                c.herd.Add(beast);
                c.lord.AddPawn(beast);
                st.beast = beast;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the lab's beast", e);
            }
        }

        /// <summary>Their hour: the beast loose, a vat-grown of theirs ill, once a stay each, by day after their first.</summary>
        public static void Hour(Camp c, LabState st, int hour)
        {
            st.tasks.RemoveAll(t => t?.pawn == null || t.pawn.Dead || t.pawn.Faction != Faction.OfPlayer);
            if (c.DaysHere < 1f || hour < 9 || hour > 17 || MapFacts.ThreatOn(c.map)) return;
            if (!st.looseRolled && st.beast != null) Loose(c, st);
            else if (!st.sickRolled) Sick(c, st);
        }

        /// <summary>
        /// Once a stay: their hybrid gets loose and turns on whoever is near (the game's own manhunter), at the camps' beasts' rate in the
        /// shared harm budget (the drovers' bull's kind); told as a threat.
        /// </summary>
        public static void Loose(Camp c, LabState st)
        {
            st.looseRolled = true;
            var b = st.beast;
            if (b == null || b.Dead || !b.Spawned || b.Downed || !Harm.On || !Harm.SystemOn(HarmKinds.DroversBull.system) || !Harm.Roll(HarmKinds.DroversBull, PeopleEvents8Math.LooseChance)) return;
            try
            {
                Lords.Leave(b);
                c.herd.Remove(b);
                b.SetFaction(null);
                if (b.mindState?.mentalStateHandler == null || !b.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, null, true, false, false, null, false, false, false)) return;
                st.loose = true;
                HarmBook.Instance?.Came(HarmKinds.DroversBull, Now);
                if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "loose");
                Tell.Letter(NewsKind.Land, Importance.Important, c.map, "CC_PEv8_LabLooseLabel".Translate(b.LabelShort).Resolve(),
                    c.Text("CC_PEv8_LabLoose", c.leader, null, 0, b.LabelShort), LetterDefOf.ThreatSmall, new LookTargets(b), c.faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the lab's beast loose", e);
            }
        }

        /// <summary>
        /// Once a stay: a vat-grown of theirs falls ill with a gene fault (round G's batch gone wrong; the camps' fever kind in the shared
        /// harm budget and the illness budget); told, and the colony's doctor may see to them.
        /// </summary>
        public static void Sick(Camp c, LabState st)
        {
            st.sickRolled = true;
            var map = c.map;
            var ill = PeoplesG.FaultDef ?? DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            var who = c.Here.Where(p => p.RaceProps.Humanlike && !Sequencer(p)).RandomElementWithFallback() ?? c.Here.Where(p => p.RaceProps.Humanlike && p != c.leader).RandomElementWithFallback();
            if (ill == null || who == null || !Harm.On || !Harm.SystemOn(HarmKinds.CampFever.system) || !Harm.Roll(HarmKinds.CampFever, PeopleEvents8Math.VatSickChance)) return;
            if (!IllnessBudget.Allows(map, ill, who) || Harm.Ail(who, ill, Mathf.Max(0.1f, ill.initialSeverity), HarmKinds.CampFever) == null) return;
            IllnessBudget.Owe(map, ill, who);
            st.sick = true;
            Tell.Message(NewsKind.Health, Importance.Notable, map, c.Text("CC_PEv8_LabSick", c.leader, null, 0, who.LabelShort), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>Something of the land to sample by day: a wild beast or a wild plant within a walk of their camp, out of the fog.</summary>
        public static LocalTargetInfo SampleTarget(Camp c, Pawn p)
        {
            var map = c.map;
            if (Rand.Bool)
            {
                var beast = map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps.Animal && a.Faction == null && !a.Downed && a.MentalStateDef == null && a.Position.InHorDistOf(c.center, 40f)
                                                                     && !a.Position.Fogged(map) && p.CanReach(a, PathEndMode.Touch, Danger.Some)).RandomElementWithFallback();
                if (beast != null) return beast;
            }
            for (int i = 0; i < 10; i++)
            {
                var cell = c.center + IntVec3.FromVector3(new Vector3(Rand.Range(-30f, 30f), 0f, Rand.Range(-30f, 30f)));
                if (!cell.InBounds(map) || cell.Fogged(map)) continue;
                var plant = cell.GetPlant(map);
                // Wild growth only: never the colony's own crops.
                if (plant == null || plant.def.plant == null || map.zoneManager.ZoneAt(cell) is Zone_Growing) continue;
                if (p.CanReach(plant, PathEndMode.Touch, Danger.Some)) return plant;
            }
            return LocalTargetInfo.Invalid;
        }

        /// <summary>A beast or plant sampled: now and then a word, the count kept for their farewell.</summary>
        public static void Sampled(Pawn by, LocalTargetInfo target)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null) return;
            st.sampled++;
            c.SayMoment(by, "sample", 0.3f);
        }

        /// <summary>The colony's ask: a gene of the Works' written into a colonist who could take one (the asker if they could, else another).</summary>
        public static Pawn GeneTaker(Map map, Pawn asked) =>
            asked != null && HelixGenes.Takers(map).Contains(asked) ? asked : HelixGenes.Takers(map).FirstOrDefault();

        public static bool Queue(Camp c, LabTask t)
        {
            var st = StateOf(c);
            if (st == null || t?.pawn == null || st.tasks.Count >= PeopleEvents8Math.MaxLabTasks) return false;
            st.tasks.Add(t);
            return true;
        }

        /// <summary>A sequencer of theirs given the next piece of the colony's work, when the one it is for is at home and in reach (any waking hour).</summary>
        public static Job TaskJob(Camp c, Pawn p, int hour)
        {
            var st = StateOf(c);
            if (st == null || st.tasks.Count == 0 || !Sequencer(p) || hour < 7 || hour > 20) return null;
            var t = st.tasks[0];
            if (t.pawn == null || !t.pawn.Spawned || t.pawn.Map != c.map || t.pawn.Downed || !p.CanReach(t.pawn, PathEndMode.Touch, Danger.Some)) return null;
            if (c.Here.Any(o => o != p && (o.CurJobDef == PeopleEvents8.LabGeneJob || o.CurJobDef == PeopleEvents8.LabSampleJob))) return null;
            return t.kind == "gene"
                ? PeopleEvents8.Make(PeopleEvents8.LabGeneJob, t.pawn, PeoplesGMath.GeneWorkTicks)
                : PeopleEvents8.Make(PeopleEvents8.LabSampleJob, t.pawn, PeoplesGMath.DrawTicks);
        }

        private static (Camp c, LabTask t) TaskOf(Pawn seq, Pawn on, string kind)
        {
            var c = CampTracker.VisitorsAt(seq?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(seq));
            var t = StateOf(c)?.tasks.FirstOrDefault(x => x.pawn == on && x.kind == kind);
            return (c, t);
        }

        /// <summary>The gene work done at the colonist's side: the gene written in as a xenogene, the game's mark of a xenogerm replicating, told.</summary>
        public static void GeneDone(Pawn seq, Pawn taker)
        {
            var (c, t) = TaskOf(seq, taker, "gene");
            if (c == null || t == null) return;
            StateOf(c).tasks.Remove(t);
            var gene = DefDatabase<GeneDef>.GetNamedSilentFail(t.gene ?? "");
            if (gene == null || taker?.genes == null || taker.Dead || taker.genes.GetGene(gene) != null || !HelixGenes.Fits(taker).Contains(gene)) return;
            taker.genes.AddGene(gene, true);
            if (HediffDefOf.XenogermReplicating != null && !taker.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating)) taker.health.AddHediff(HediffDefOf.XenogermReplicating);
            if (taker.Spawned) FleckMaker.ThrowMetaIcon(taker.Position, taker.Map, FleckDefOf.HealingCross);
            c.SayMoment(seq, "gene");
            Tell.Message(NewsKind.People, Importance.Notable, c.map, "CC_PeoplesG_GeneWritten".Translate(seq.LabelShort.Named("SEQ"), taker.LabelShort.Named("PAWN"), gene.label.Named("GENE")).Resolve(),
                taker.Spawned ? new LookTargets(taker) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The sample drawn at the colonist's side: a little blood from the arm (round G's draw), the Works' price paid, told.</summary>
        public static void SampleDone(Pawn seq, Pawn donor)
        {
            var (c, t) = TaskOf(seq, donor, "sample");
            if (c == null || t == null) return;
            StateOf(c).tasks.Remove(t);
            if (donor == null || donor.Dead) return;
            HealthUtility.AdjustSeverity(donor, HediffDefOf.BloodLoss, PeoplesGMath.DrawSeverity);
            if (donor.Spawned) FleckMaker.ThrowMetaIcon(donor.Position, donor.Map, FleckDefOf.HealingCross);
            if (t.price > 0 && c.faction != null) Couriers.PaySilver(c.map, donor.Spawned ? donor.Position : seq.Position, t.price, c.faction, seq);
            c.SayMoment(seq, "drawn");
            Tell.Message(NewsKind.People, Importance.Notable, c.map, "CC_PeoplesG_Drawn".Translate(seq.LabelShort.Named("BUYER"), donor.LabelShort.Named("PAWN"), t.price.Named("COUNT")).Resolve(),
                donor.Spawned ? new LookTargets(donor) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public static void Leave(Camp c, LabState st)
        {
            if (!c.Hostile && c.tension < 50f) PeopleEvents8.Goodwill(c.faction, PeopleEvents8Math.LabGoodwill, GoodwillWhy.CampWell);
        }
    }

    /// <summary>The field lab's ways: sampling the land by day, the colony's gene work and samples done at its people's side, the beast, the sick vat-grown.</summary>
    public class CampWorker_FieldLab : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is LabState)) c.workState = new LabState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is LabState st))
            {
                Started(c);
                st = FieldLab.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            FieldLab.Hour(c, st, hour);
        }

        /// <summary>The colony's gene work or sample waiting: a sequencer goes to the colonist (any waking hour, before the camp's own day).</summary>
        public override Job NightJob(Camp c, Pawn p, int hour) => FieldLab.TaskJob(c, p, hour);

        /// <summary>By day, sampling the land: a wild beast or plant within a walk, half an hour at it.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            if (!PeopleEvents8Math.SampleHour(GenLocalDate.HourOfDay(c.map)) || !p.RaceProps.Humanlike || !Rand.Chance(0.5f)) return null;
            var t = FieldLab.SampleTarget(c, p);
            return t.IsValid ? PeopleEvents8.Make(PeopleEvents8.FieldSampleJob, t, PeopleEvents8Math.SampleTicks) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = FieldLab.StateOf(c);
            if (st != null) FieldLab.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = FieldLab.StateOf(c);
            if (st == null) return;
            if (st.sampled >= 5) marks.Add("sampled");
            if (st.loose) marks.Add("loose");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{SAMPLED}") ? text : text.Replace("{SAMPLED}", (FieldLab.StateOf(c)?.sampled ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = FieldLab.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "gene":
                    if (!ModsConfig.BiotechActive) return "CC_PEv8_NoBiotech".Translate().Resolve();
                    if (st.tasks.Count >= PeopleEvents8Math.MaxLabTasks) return "CC_PEv8_LabBusy".Translate().Resolve();
                    return FieldLab.GeneTaker(c.map, null) == null ? "CC_PEv8_NoGeneTaker".Translate().Resolve() : null;
                case "sample":
                    if (!ModsConfig.BiotechActive) return "CC_PEv8_NoBiotech".Translate().Resolve();
                    if (st.tasks.Count >= PeopleEvents8Math.MaxLabTasks) return "CC_PEv8_LabBusy".Translate().Resolve();
                    return HelixSample.Rarest(c.map, out _) == null || st.tasks.Any(t => t.kind == "sample") ? "CC_PEv8_NoRareBlood".Translate().Resolve() : null;
                case "genepack":
                    return !ModsConfig.BiotechActive ? "CC_PEv8_NoBiotech".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A vat-keeper of {FACTION} ({X}) at the gate: two sequencers and a vat-keeper would camp a week by the colony and sample the land.</summary>
    public class HappeningWorker_FieldLab : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            FieldLab.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && FieldLab.On(map) == null && PeoplesD.Friendly(PeoplesMath.Editors) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ModsConfig.BiotechActive) tags.Add("genes");
            if (HybridBeast.Kinds(ctx.map).Count > 0) tags.Add("hybrid");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (FieldLab.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The field lab made (FieldLab.Start).</summary>
    public class DilemmaEffect_FieldLab : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv8_Impact_Lab".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv8_Impact_LabBrief".Translate().Resolve(), "pev8lab");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv8_Impact_LabTurns".Translate().Resolve(), "CC_PEv8_Impact_LabTurnsBrief".Translate().Resolve(), "pev8labturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.LabGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            FieldLab.Start(hc);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The camp's gene work asked for (its silver paid by the ask): one of the Works' genes queued for a colonist who could take it.</summary>
    public class DilemmaEffect_LabGene : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx?.map == null || FieldLab.GeneTaker(ctx.map, ctx.a) == null ? "CC_PEv8_NoGeneTaker".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var taker = ctx?.map == null ? null : FieldLab.GeneTaker(ctx.map, ctx.a);
            if (taker == null) return;
            sheet.Add(ImpactKind.Health, 1, "CC_PEv8_Impact_LabGene".Translate(taker.LabelShort).Resolve(), "CC_PeoplesG_Impact_GeneBrief".Translate().Resolve(), "pev8labgene");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_FieldLab);
            var taker = ctx?.map == null ? null : FieldLab.GeneTaker(ctx.map, ctx.a);
            var gene = taker == null ? null : HelixGenes.Fits(taker).RandomElementWithFallback();
            if (c == null || gene == null) return;
            if (FieldLab.Queue(c, new LabTask { kind = "gene", pawn = taker, gene = gene.defName }))
                c.Note(c.Text("CC_PEv8_LabGeneQueued", null, taker, 0, gene.label), MessageTypeDefOf.NeutralEvent, Importance.Notable, taker, true);
        }
    }

    /// <summary>The camp's sample asked for: a draw from the colony's rarest blood queued, the Works' price paid when it is drawn.</summary>
    public class DilemmaEffect_LabSample : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx?.map == null || HelixSample.Rarest(ctx.map, out _) == null ? "CC_PEv8_NoRareBlood".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int r = 0;
            var donor = ctx?.map == null ? null : HelixSample.Rarest(ctx.map, out r);
            if (donor == null) return;
            sheet.Play("CC_PeoplesG_Impact_Sample".Translate(donor.LabelShort).Resolve(), "CC_PeoplesG_Impact_SampleBrief".Translate().Resolve());
            sheet.Silver(PeoplesGMath.SamplePrice(r));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_FieldLab);
            int r = 0;
            var donor = ctx?.map == null ? null : HelixSample.Rarest(ctx.map, out r);
            if (c == null || donor == null) return;
            if (FieldLab.Queue(c, new LabTask { kind = "sample", pawn = donor, price = PeoplesGMath.SamplePrice(r) }))
                c.Note(c.Text("CC_PEv8_LabSampleQueued", null, donor), MessageTypeDefOf.NeutralEvent, Importance.Notable, donor, true);
        }
    }

    /// <summary>A genepack of the colony's bought by the lab (carried out to them by the ask's own carry-out): their price paid.</summary>
    public class DilemmaEffect_LabBuys : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) => sheet.Silver(PeopleEvents8Math.GenepackPrice);

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_FieldLab);
            if (c?.faction != null) Couriers.PaySilver(c.map, c.FireCell, PeopleEvents8Math.GenepackPrice, c.faction, c.leader);
        }
    }
}
