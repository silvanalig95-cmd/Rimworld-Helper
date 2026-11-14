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
    // ===================== 10. A beast for a colonist (the herding folk) =====================

    /// <summary>
    /// The beast the herders' elder chose for one of the colony's people ("the grass will tell"): a young beast of the
    /// land that could carry them or their load (a horse, a yak, a dromedary, a muffalo, or any of the mods' beasts that
    /// live here: Alpha Animals, Vanilla Animals Expanded, Megafauna), one they could ride twice as likely with Giddy-Up.
    /// </summary>
    public static class HerdersBeast
    {
        public const string GiftKind = "beast";
        public const string SupperKind = "pev:supper";

        /// <summary>The base game's beasts when the land has none that fit.</summary>
        private static readonly string[] Fallback = { "Horse", "Yak", "Dromedary", "Muffalo", "Alpaca" };

        public static bool Fits(PawnKindDef k)
        {
            var rp = k?.RaceProps;
            if (rp == null || k.race == null || rp.IsMechanoid || rp.Humanlike || rp.trainability == null || rp.trainability == TrainabilityDefOf.None || PokeWorldLink.IsPokemon(k)) return false;
            float wild;
            try
            {
                wild = k.race.GetStatValueAbstract(StatDefOf.Wildness);
            }
            catch
            {
                return false;
            }
            return PeopleEventsMath.BeastFits(rp.Animal, rp.baseBodySize, wild, rp.predator, rp.Insect, rp.Dryad);
        }

        private static bool Warm(Map map, PawnKindDef k) => map?.mapTemperature == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f);

        /// <summary>The kind of beast by the land, or null.</summary>
        public static PawnKindDef KindFor(Map map)
        {
            var biome = map?.Biome;
            var pool = new List<PawnKindDef>();
            if (biome != null) pool.AddRange(BiomeAnimals.Of(biome).Where(k => Fits(k) && Warm(map, k)));
            foreach (string n in Fallback)
            {
                var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
                if (k != null && Fits(k) && Warm(map, k) && !pool.Contains(k)) pool.Add(k);
            }
            bool gu = GiddyUpMounts.On;
            return pool.Distinct().RandomElementByWeightWithFallback(k => PeopleEventsMath.BeastWeight(GiddyUpMounts.Ridable(k.race), gu, biome == null ? 0.05f : BiomeAnimals.Commonality(biome, k)));
        }

        /// <summary>A young beast of this kind, just grown, of the herders' own herd.</summary>
        public static Pawn Make(PawnKindDef kind, Faction f)
        {
            if (kind == null) return null;
            try
            {
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 1f : kind.RaceProps.lifeStageAges.Last().minAge;
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    fixedBiologicalAge: grown + Rand.Range(0.1f, 0.6f)));
                p?.inventory?.DestroyAll();
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the herders' beast", e);
                return null;
            }
        }

        /// <summary>The colony's own young beast that could be traded for theirs: not bonded, young, worth half of theirs or more, the least dear first.</summary>
        public static Pawn OurYoung(Map map, Pawn theirs)
        {
            if (map == null || theirs == null) return null;
            float worth = theirs.MarketValue;
            return map.mapPawns.SpawnedColonyAnimals.Where(a => a != null && !a.Dead && !a.Downed && a.RaceProps.Animal && a.ageTracker != null
                                                                && a.ageTracker.AgeBiologicalYearsFloat < Mathf.Max(1f, a.RaceProps.lifeExpectancy * 0.25f)
                                                                && !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond) && PeopleEventsMath.TradeEnough(a.MarketValue, worth))
                .OrderBy(a => a.MarketValue).FirstOrDefault();
        }

        /// <summary>
        /// The beast is the colony's: trained to its first tasks, bonded to the colonist chosen, who walks out to it and leads
        /// it home (roped to the pen for a beast that roams, else it follows them in).
        /// </summary>
        public static void Handed(HappeningContext ctx)
        {
            var beast = ctx?.pet;
            var b = ctx?.b;
            var map = ctx?.map;
            if (beast == null || beast.Dead || b == null || map == null) return;
            try
            {
                beast.GetLord()?.RemovePawn(beast);
                if (beast.Faction != Faction.OfPlayer) beast.SetFaction(Faction.OfPlayer, b);
                if (beast.training != null)
                    foreach (string n in PeopleEventsMath.FirstTasks)
                    {
                        var td = DefDatabase<TrainableDef>.GetNamedSilentFail(n);
                        if (td != null && beast.training.CanAssignToTrain(td).Accepted)
                        {
                            beast.training.SetWantedRecursive(td, true);
                            beast.training.Train(td, b, true);
                        }
                    }
                if (!b.Dead && b.relations != null && !b.relations.DirectRelationExists(PawnRelationDefOf.Bond, beast)) b.relations.AddDirectRelation(PawnRelationDefOf.Bond, beast);
                if (beast.playerSettings != null && !b.Dead) beast.playerSettings.Master = b;
                PeopleEventsBook.Gave(GiftKind, b);
                if (PlayOuts.On && b.Spawned && beast.Spawned && !b.Downed && !b.Drafted && !b.InMentalState) LeadHome(b, beast);
                Tell.Message(NewsKind.People, Importance.Notable, map,
                    "CC_PEv_BeastHanded".Translate(b.Named("PAWN"), beast.LabelShort.Named("BEAST"), (ctx.stranger?.LabelShort ?? "?").Named("RIDER")).Resolve(),
                    new LookTargets(new Thing[] { b, beast }.Where(t => t.Spawned)), MessageTypeDefOf.PositiveEvent);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World,
                    "CC_PEv_BeastChronicle".Translate(b.Named("PAWN"), beast.LabelShort.Named("BEAST"), (ctx.faction?.Name ?? "?").Named("FACTION")).Resolve(), b));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the beast handed over", e);
            }
            Supper(ctx);
        }

        /// <summary>The colonist walks out to the beast and leads it home: roped to the pen (a beast that roams), else it follows them.</summary>
        private static void LeadHome(Pawn b, Pawn beast)
        {
            try
            {
                if (beast.RaceProps.Roamer && JobDefOf.RopeToPen != null && AnimalPenUtility.GetCurrentPenOf(beast, false) == null
                    && AnimalPenUtility.ClosestSuitablePen(beast, false) != null)
                {
                    var rope = JobMaker.MakeJob(JobDefOf.RopeToPen, beast);
                    if (b.jobs.TryTakeOrderedJob(rope, JobTag.Misc)) return;
                }
                var go = JobMaker.MakeJob(JobDefOf.Goto, beast.Position);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                b.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                var follow = JobMaker.MakeJob(JobDefOf.FollowClose, b);
                follow.followRadius = 3f;
                follow.expiryInterval = GenDate.TicksPerHour;
                follow.checkOverrideOnExpire = true;
                beast.jobs?.StartJob(follow, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the beast led home", e);
            }
        }

        /// <summary>The rider stays the night: supper at the colony's table, then home in the morning (one of the events' visits).</summary>
        public static void Supper(HappeningContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map;
            if (!PeoplesG.Up(x, map) || x.Faction == null) return;
            int hour = GenLocalDate.HourOfDay(map);
            // Until seven the next morning, past this evening's supper (a handover in the small hours waits for tonight's).
            int hours = (24 - hour + 7) % 24;
            if (hours < 6) hours += 24;
            int untilMorning = hours * GenDate.TicksPerHour;
            try
            {
                x.GetLord()?.RemovePawn(x);
                var spot = PeoplesD.GateSpot(map) ?? x.Position;
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, spot, untilMorning), map, new List<Pawn> { x });
                CouncilGameComponent.Instance?.happenings?.AddVisit(SupperKind, ctx.incident, map, x, ctx.b, null, Find.TickManager.TicksGame + untilMorning);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the rider's supper", e);
            }
        }

        /// <summary>The rider's evening: a meal of the colony's at its table between five and nine, once.</summary>
        public static bool SupperHour(Visit v, int now)
        {
            var x = v.x;
            if (v.done || now > v.until || !PeoplesG.Up(x, v.map)) return false;
            int hour = GenLocalDate.HourOfDay(v.map);
            if (hour < 17 || hour > 21 || x.InMentalState) return true;
            var meal = Hosting.Meal(x, false) ?? Hosting.Raw(x);
            // What the colony has lies where visitors don't go (a cold store): one of the colony brings the rider's supper
            // out to them, and they eat it when it's handed over (KeepOut).
            if (meal == null && Hosting.Brought(x, false, true, out var brought))
            {
                if (brought == null) return true;
                v.done = true;
                PeoplesG.SayLine(x, v.incident, "supper");
                return false;
            }
            if (meal == null || !Hosting.Eat(x, meal, null)) return true;
            v.done = true;
            PeoplesG.SayLine(x, v.incident, "supper");
            return false;
        }
    }

    /// <summary>
    /// A rider of the herding folk leads in a young beast their elder chose for one of the colony's people ({B}: "the grass
    /// will tell"), {TALE} by the land, for a fair price ({COUNT} silver), or one of the colony's own young beasts in trade.
    /// Once a colonist, and never two gifts of the peoples close together.
    /// </summary>
    public class HappeningWorker_HerdersBeast : HappeningWorker_Visitors
    {
        private static IEnumerable<Pawn> Takers(Map map) =>
            PeopleEvents.Takers(map, HerdersBeast.GiftKind).Where(p => p.skills?.GetSkill(SkillDefOf.Animals)?.TotallyDisabled == false);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Herders) != null && Takers(map).Any() && HerdersBeast.KindFor(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Herders);
            if (ctx.faction == null) return false;
            // The elder's choice: anyone who could keep a beast, the ones with a way with them likelier.
            ctx.b = Takers(ctx.map).RandomElementByWeightWithFallback(p =>
            {
                var s = p.skills.GetSkill(SkillDefOf.Animals);
                return (1f + s.Level) * (s.passion != Passion.None ? 2f : 1f);
            });
            var kind = HerdersBeast.KindFor(ctx.map);
            if (ctx.b == null || kind == null) return false;
            ctx.pet = HerdersBeast.Make(kind, ctx.faction);
            if (ctx.pet == null) return false;
            ctx.tale = kind.label;
            ctx.beast = ctx.pet.LabelShort;
            ctx.count = PeopleEventsMath.BeastPrice(ctx.pet.MarketValue);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Drover"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger, ctx.pet }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.pet != null && !ctx.pet.Spawned && !ctx.pet.Discarded) Find.WorldPawns.PassToWorld(ctx.pet, PawnDiscardDecideMode.Discard);
            ctx.pet = null;
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.pet != null && GiddyUpMounts.On && GiddyUpMounts.Ridable(ctx.pet.def) && ctx.pet.RaceProps.baseBodySize >= OwnRiders.MinMountSize) tags.Add("ride");
            if (ctx.pet != null && ctx.pet.RaceProps.packAnimal) tags.Add("pack");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_ChosenGone".Translate().Resolve();
            if (ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned) return "CC_PEv_BeastGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "handed" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The beast handed over to {B} where it stands: theirs now, its first tasks, the game's bond; led home; the rider stays to supper.</summary>
    public class DilemmaEffect_HerdersBeast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null || hc.pet == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv_Impact_Beast".Translate(hc.b.LabelShort, hc.pet.LabelShort).Resolve(), "CC_PEv_Impact_BeastBrief".Translate().Resolve(), "pevbeast");
            if (PlayOuts.On) sheet.Play("CC_PEv_Impact_BeastHome".Translate(hc.b.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv_Impact_BeastHomeBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => HerdersBeast.Handed(ctx as HappeningContext);
    }

    /// <summary>One of the colony's own young beasts goes back with the rider, in trade: theirs now, walking home in their party.</summary>
    public class DilemmaEffect_HerdersTrade : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            HerdersBeast.OurYoung(ctx?.map, ctx?.pet) == null ? "CC_PEv_NoYoungBeast".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var ours = HerdersBeast.OurYoung(ctx?.map, ctx?.pet);
            if (ours == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_PEv_Impact_Trade".Translate(ours.LabelShort, ctx.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv_Impact_TradeBrief".Translate().Resolve(), "pevtrade");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            var ours = HerdersBeast.OurYoung(map, ctx?.pet);
            if (ours == null || x == null || x.Faction == null) return;
            try
            {
                ours.relations?.ClearAllRelations();
                ours.SetFaction(x.Faction);
                var lord = x.GetLord();
                if (lord != null && ours.Spawned && ours.Map == x.Map) lord.AddPawn(ours);
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv_BeastTraded".Translate(ours.LabelShort.Named("BEAST"), x.LabelShort.Named("RIDER")).Resolve(),
                    ours.Spawned ? new LookTargets(ours) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: a young beast traded", e);
            }
        }
    }
}
