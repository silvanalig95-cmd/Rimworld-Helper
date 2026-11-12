using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 40. A leg for a lame beast (the Circuit of Tamers) =====================

    /// <summary>
    /// The beast-doctor's leg (item 40): for an animal of the colony's that lost a leg, the Circuit's beast-doctor comes. A
    /// Circuit of the industrial or spacer age fits Alpha Implants' animal prosthetic leg by the game's own surgery and its
    /// chance (PeopleWorkOn, the beast held where it stands); in the tribal and medieval ages (or without Alpha Implants) she
    /// takes the beast to their stables to live out its days, leading it away as a stray is led home, and leaves the young
    /// one of its kind she brought.
    /// </summary>
    public static class LameBeast
    {
        public const string GiftKind = "beastleg";

        private static bool Below(BodyPartRecord part, BodyPartRecord top)
        {
            for (var p = part; p != null; p = p.parent)
                if (p == top) return true;
            return false;
        }

        /// <summary>A leg this beast lost (the part missing), or null.</summary>
        public static BodyPartRecord Lost(Pawn beast)
        {
            if (beast?.health?.hediffSet == null || beast.RaceProps?.Animal != true) return null;
            try
            {
                return beast.health.hediffSet.GetMissingPartsCommonAncestors().Select(m => m?.Part).FirstOrDefault(p => p != null && PeopleEvents2Math.IsLeg(p.def?.defName));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Alpha Implants' leg in the Circuit's age and the part it goes on, or false (no leg of their age, or no Alpha Implants).</summary>
        public static bool Fitting(Pawn beast, int age, out RecipeDef recipe, out BodyPartRecord part)
        {
            recipe = null;
            part = null;
            var lost = Lost(beast);
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(PeopleEvents2Math.BeastLeg(age) ?? "");
            if (lost == null || r?.Worker == null || r.addsHediff == null) return false;
            try
            {
                var on = r.Worker.GetPartsToApplyOn(beast, r).FirstOrDefault(x => x != null && Below(lost, x));
                if (on == null) return false;
                recipe = r;
                part = on;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The colony's beasts that lost a leg, never one the Circuit has seen to.</summary>
        public static IEnumerable<Pawn> Lame(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(a => a != null && !a.Dead && !a.Downed && a.RaceProps.Animal && !PeopleEventsBook.Given(GiftKind, a) && Lost(a) != null);

        /// <summary>A young one of the beast's kind, just past its first stage, of the Circuit's own.</summary>
        public static Pawn Young(Pawn beast, Faction f)
        {
            var kind = beast?.kindDef;
            if (kind == null) return null;
            try
            {
                var stages = kind.RaceProps.lifeStageAges;
                float age = stages != null && stages.Count > 1 ? Mathf.Max(0.05f, stages[1].minAge * 0.5f + 0.01f) : 0.3f;
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: age,
                    fixedChronologicalAge: age));
                p?.inventory?.DestroyAll();
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the young one", e);
                return null;
            }
        }
    }

    /// <summary>
    /// The Circuit's beast-doctor at the gate for {PET}, which lost its {RELATION}: Alpha Implants' leg ({TALE}) fitted for
    /// {COUNT} silver in an industrial or spacer age, or the beast taken to their stables, a young one of its kind left.
    /// </summary>
    public class HappeningWorker_LameBeast : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Tamers) != null && LameBeast.Lame(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            ctx.pet = LameBeast.Lame(ctx.map).RandomElementWithFallback();
            if (ctx.faction == null || ctx.pet == null) return false;
            var lost = LameBeast.Lost(ctx.pet);
            ctx.relation = lost?.Label ?? "leg";
            ctx.beast = ctx.pet.LabelShort;
            int age = PeopleEvents.AgeOf(ctx.faction);
            if (LameBeast.Fitting(ctx.pet, age, out var recipe, out _))
            {
                ctx.note = recipe.defName;
                ctx.tale = recipe.addsHediff.label;
                ctx.count = PeopleEvents2Math.LegPrice(recipe.addsHediff.spawnThingOnRemoved?.BaseMarketValue ?? 400f);
            }
            ctx.a = ctx.pet.playerSettings?.Master ?? PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Tamer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(10, 15));
            // The young one of its kind she brings, for the stables' answer.
            var young = LameBeast.Young(ctx.pet, ctx.faction);
            if (young != null) ctx.group.Add(young);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note.NullOrEmpty() ? "stables" : "leg");
            if (ctx.group.Count == 0) tags.Add("noyoung");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned || ctx.pet.Map != ctx.map) return "CC_PEv2_BeastGone".Translate().Resolve();
            if (option == 0)
            {
                if (ctx.note.NullOrEmpty())
                    return (PeopleEvents.AgeOf(ctx.faction) >= 2 ? "CC_PEv2_NoBeastLegMod" : "CC_PEv2_NoBeastLegAge").Translate(ctx.faction?.Name ?? "?").Resolve();
                if (!LameBeast.Fitting(ctx.pet, PeopleEvents.AgeOf(ctx.faction), out var r, out _) || r.defName != ctx.note) return "CC_PEv_LimbFitted".Translate().Resolve();
            }
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "fit" : option == 1 ? "stables" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var y in ctx.group.Where(p => p != null && p.RaceProps.Animal && !p.Spawned && !p.Discarded).ToList()) Find.WorldPawns.PassToWorld(y, PawnDiscardDecideMode.Discard);
            ctx.group.RemoveAll(p => p != null && p.RaceProps.Animal);
            base.Abort(ctx);
        }
    }

    /// <summary>The beast held where it stands and the beast-doctor at work (Rimfolk_PEv2_BeastLeg); then the game's own surgery (BeastLegDone).</summary>
    public class DilemmaEffect_BeastLeg : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv2_BeastLeg");

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(hc?.note ?? "");
            if (hc?.pet == null || r == null) return;
            sheet.Add(ImpactKind.Health, 1, "CC_PEv2_Impact_BeastLeg".Translate(hc.pet.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv2_Impact_BeastLegBrief".Translate().Resolve(), "pev2beastleg");
            int chance = LameBeast.Fitting(hc.pet, PeopleEvents.AgeOf(hc.faction), out _, out var part) ? OrderLimb.Chance(r, hc.stranger, hc.pet, part) : 0;
            sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_LimbRisk".Translate(hc.pet.LabelShort, (100 - chance).Named("FAIL"), chance.Named("CHANCE")).Resolve(), "CC_PEv_Impact_LimbRiskBrief".Translate().Resolve(), "pev2beastlegrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null || hc.pet == null || hc.stranger == null || Job == null) return;
            PeopleWorkOn.Begin(hc, hc.stranger, hc.pet, Job, LocalTargetInfo.Invalid, hc.note, hc.count, PeopleEvents2Math.LegTicks, 8f);
        }
    }

    /// <summary>The fitting done: the game's own surgery on the beast, its chance its own.</summary>
    public class BeastLegDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on)
        {
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(PeopleWorkOn.Note(v) ?? "");
            var f = worker?.Faction;
            var map = on?.MapHeld ?? v.map;
            if (r == null || on == null || on.Dead || !LameBeast.Fitting(on, PeopleEvents.AgeOf(f), out var now, out var part) || now != r) return;
            bool fitted = false;
            try
            {
                r.Worker.ApplyOnPawn(on, part, worker, new List<Thing>(), null);
                fitted = !on.Dead && on.health.hediffSet.hediffs.Any(h => h.def == r.addsHediff && h.Part == part);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the beast-doctor's surgery", e);
            }
            PeopleEventsBook.Gave(LameBeast.GiftKind, on);
            if (fitted)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.LegGoodwill, false);
                PeoplesG.SayLine(worker, v.incident, "walks");
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv2_BeastLegDone".Translate(worker.LabelShort.Named("DOCTOR"), on.LabelShort.Named("BEAST"), r.addsHediff.label.Named("LEG")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else if (!on.Dead)
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv2_BeastLegFailed".Translate(worker.LabelShort.Named("DOCTOR"), on.LabelShort.Named("BEAST")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.NegativeHealthEvent);
        }

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>
    /// To the Circuit's stables: the lame beast handed over to the beast-doctor's people and led away at her heels as a stray
    /// is led, and the young one of its kind she brought handed to the colony at the gate.
    /// </summary>
    public class DilemmaEffect_BeastStables : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.pet == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_PEv2_Impact_Stables".Translate(hc.pet.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_StablesBrief".Translate().Resolve(), "pev2stables");
            var young = hc.group.FirstOrDefault(p => p != null && p.RaceProps.Animal);
            if (young != null) sheet.Add(ImpactKind.People, 1, "CC_PEv2_Impact_Young".Translate(young.LabelShort).Resolve(), "CC_PEv2_Impact_YoungBrief".Translate().Resolve(), "pev2young");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            var x = hc?.stranger;
            var beast = hc?.pet;
            if (map == null || x == null || beast == null || beast.Dead) return;
            try
            {
                // The young one first: the colony's, roped or led to the pen as the herders' beast is.
                var young = hc.group.FirstOrDefault(p => p != null && p.RaceProps.Animal && p.Spawned && !p.Dead);
                if (young != null)
                {
                    young.GetLord()?.RemovePawn(young);
                    young.SetFaction(Faction.OfPlayer);
                    if (hc.a != null && !hc.a.Dead && young.playerSettings != null) young.playerSettings.Master = hc.a;
                }
                // The lame beast to the Circuit, at her heels as she goes.
                PeopleEventsBook.Gave(LameBeast.GiftKind, beast);
                beast.relations?.ClearAllRelations();
                beast.SetFaction(x.Faction);
                var lord = x.GetLord();
                if (lord != null && beast.Spawned && beast.Map == x.Map) lord.AddPawn(beast);
                if (beast.Spawned && beast.jobs != null)
                {
                    var follow = JobMaker.MakeJob(JobDefOf.FollowClose, x);
                    follow.followRadius = 3f;
                    follow.expiryInterval = 4 * GenDate.TicksPerHour;
                    beast.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                }
                if (x.Faction != null && x.Faction.HasGoodwill && !x.Faction.IsPlayer) WorldSituationWorker.Goodwill(x.Faction, PeopleEvents2Math.StablesGoodwill, false);
                Tell.Message(NewsKind.Land, Importance.Notable, map,
                    (young != null ? "CC_PEv2_StablesYoung" : "CC_PEv2_Stables").Translate(beast.LabelShort.Named("BEAST"), x.LabelShort.Named("DOCTOR"), (young?.LabelShort ?? "").Named("YOUNG")).Resolve(),
                    new LookTargets(new Thing[] { beast, young }.Where(t => t != null && t.Spawned)), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: to the stables", e);
            }
        }
    }
}
