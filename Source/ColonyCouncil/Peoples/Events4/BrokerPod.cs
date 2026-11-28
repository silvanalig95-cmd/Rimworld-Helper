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
    // ===================== 24. Stranded with something to sell (the spacer enclave) =====================

    /// <summary>
    /// Stranded with something to sell (item 24): a broker's pod comes down hard on the colony's land (the game's pod crash),
    /// the broker hurt among the cargo's crates. Rescued and tended, the enclave's lander comes for them two or three days
    /// later: its marshals carry the broker off (or walk with them), lift the cargo aboard, and pay a fifth of its worth
    /// (+10). Robbing the pods is the colony's choice, and earns the enclave's lasting grudge (the lander comes for the
    /// broker alone). Left for their own lander, it comes the next morning for both, and nothing is owed either way. A crate
    /// gone when the lander comes is a crate taken.
    /// </summary>
    public static class BrokerPod
    {
        public const string LanderKind = "pev:lander";

        private static int Now => Find.TickManager.TicksGame;

        public static PodRecord Record(int id) => PeopleEvents4Book.Instance?.pods.FirstOrDefault(r => r != null && r.id == id);

        /// <summary>The pod record of the broker this event is about.</summary>
        public static PodRecord Of(HappeningContext ctx) => Record(int.TryParse(ctx?.note ?? "", out int id) ? id : -1);

        /// <summary>The cargo still where it came down (or anywhere on the map).</summary>
        public static List<Thing> Present(PodRecord r) => r?.cargo.Where(t => t != null && !t.Destroyed && t.Spawned && t.Map == r.map).ToList() ?? new List<Thing>();

        /// <summary>The cargo: two or three crates of spacer goods worth <paramref name="worth"/> in all, the game's own.</summary>
        public static List<Thing> Cargo(int worth, int crates)
        {
            var defs = PeopleEvents4Math.CargoDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && d.BaseMarketValue > 0f).ToList();
            var list = new List<Thing>();
            if (defs.Count == 0) return list;
            foreach (var def in defs.InRandomOrder().Take(crates))
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(worth / (float)crates / def.BaseMarketValue), 1, def.stackLimit);
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.stackCount = n;
                list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// The crash: the broker hurt and down (the game's pod crash: hurt until downed, a pod with slag), the cargo's crates
        /// near, sealed (forbidden) for the enclave. The record waits for the colony's answer.
        /// </summary>
        public static PodRecord Crash(HappeningContext ctx)
        {
            var map = ctx?.map;
            var broker = ctx?.stranger;
            var b = PeopleEvents4Book.Instance;
            if (map == null || broker == null || b == null) return null;
            if (!CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && map.reachability.CanReachColony(c) && !c.InHorDistOf(CampSite.ColonyCenter(map), 15f),
                    map, 400, out var spot))
                spot = DropCellFinder.RandomDropSpot(map);
            if (!spot.IsValid) return null;
            int worth = PeopleEvents4Math.CargoWorth(LivingEvents.WealthFactor(map));
            var cargo = Cargo(worth, PeopleEvents4Math.CargoCrates(Rand.Value));
            if (cargo.Count == 0) return null;
            try
            {
                HealthUtility.DamageUntilDowned(broker, true);
                if (broker.Dead) return null;
                DropPodUtility.DropThingsNear(spot, map, new List<Thing> { broker }, 180, false, true, false, false);
                DropPodUtility.DropThingsNear(spot, map, cargo, 200, false, true, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the broker's pod", e);
                return null;
            }
            ctx.spot = spot;
            return b.AddPod(new PodRecord { faction = ctx.faction, map = map, broker = broker, cargo = cargo, crates = cargo.Count, worth = worth, since = Now });
        }

        /// <summary>The pods' hour: a lander due comes down; a record whose letter went unanswered goes as left for their own.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null) return;
            foreach (var r in b.pods.Where(r => r != null && !r.done).ToList())
            {
                if (r.map == null || !Find.Maps.Contains(r.map) || r.faction == null || r.faction.defeated)
                {
                    r.done = true;
                    continue;
                }
                if (r.state == "waiting" || r.due < 0 || now < r.due) continue;
                if (r.faction.HostileTo(Faction.OfPlayer))
                {
                    r.done = true;
                    continue;
                }
                if (MapFacts.ThreatOn(r.map)) continue;
                Lander(r);
            }
        }

        /// <summary>The answer given: the lander's day set (two or three days after a rescue, the next morning otherwise).</summary>
        public static void Answered(PodRecord r, string state)
        {
            if (r == null) return;
            r.state = state;
            int hours = state == "rescue" ? Rand.RangeInclusive(PeopleEvents4Math.LanderMinHours, PeopleEvents4Math.LanderMaxHours) : PeopleEvents4Math.LanderLeftHours;
            r.due = Now + hours * GenDate.TicksPerHour;
        }

        /// <summary>Their lander: two marshals by pod beside the cargo (or where the broker lies), to take what is theirs.</summary>
        public static void Lander(PodRecord r)
        {
            var map = r.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var f = r.faction;
            if (t == null || map == null) return;
            r.due = -1;
            var marshals = new List<Pawn>();
            for (int i = 0; i < 2; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Enclave_Marshal"), f, new FloatRange(25f, 50f));
                if (p != null) marshals.Add(p);
            }
            if (marshals.Count == 0)
            {
                r.done = true;
                return;
            }
            var near = Present(r).FirstOrDefault()?.Position ?? (r.broker != null && r.broker.Spawned && r.broker.Map == map ? r.broker.Position : DropCellFinder.TradeDropSpot(map));
            try
            {
                DropPodUtility.DropThingsNear(near, map, marshals.Cast<Thing>(), 110, false, false, false, false);
                LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, near, GenDate.TicksPerDay), map, marshals);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the lander", e);
                r.done = true;
                return;
            }
            var v = t.AddVisit(LanderKind, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_BrokerPod"), map, marshals[0], null, marshals.Skip(1).ToList(), Now + GenDate.TicksPerDay);
            v.stake = r.id;
            v.due = Now + GenDate.TicksPerHour;
            // A crate the colony took (robbed, or gone from where it was left for them): told and remembered when they count it.
            int present = Present(r).Count;
            if (r.state == "rescue" && PeopleEvents4Math.Robbed(r.crates, present)) Taken(r, r.crates - present);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv4_LanderDown".Translate((f?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(marshals), MessageTypeDefOf.NeutralEvent);
        }

        private static List<Pawn> Marshals(Visit v) => v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();

        /// <summary>
        /// The marshals' hour: the broker first (carried off on the order's litter job, or walking with them), then each crate
        /// lifted aboard; when all is done (or the day is up), the pay and their thanks, and they go.
        /// </summary>
        public static bool LanderHour(Visit v, int now)
        {
            if (v.done) return false;
            var r = Record(v.stake);
            var marshals = Marshals(v);
            if (r == null || marshals.Count == 0 || now > v.until)
            {
                Finish(v, r);
                return false;
            }
            if (now < v.due) return true;
            var broker = r.broker;
            bool brokerHere = broker != null && !broker.Dead && broker.Spawned && broker.Map == v.map && broker.Faction == r.faction;
            var cargo = Present(r);
            foreach (var m in marshals)
            {
                if (m.CurJobDef == PeopleEvents4.LiftJob || m.CurJobDef == Litters.CarryJob) continue;
                if (brokerHere && (broker.Downed || broker.InBed()) && broker.CarriedBy == null && !marshals.Any(o => o != m && o.CurJobDef == Litters.CarryJob)
                    && RCellFinder.TryFindBestExitSpot(m, out var edge, TraverseMode.ByPawn) && Litters.CarryJob != null && m.CanReserveAndReach(broker, PathEndMode.ClosestTouch, Danger.Some))
                {
                    var j = JobMaker.MakeJob(Litters.CarryJob, broker, edge);
                    j.count = 1;
                    j.locomotionUrgency = LocomotionUrgency.Walk;
                    m.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                    continue;
                }
                var crate = cargo.Where(c => !marshals.Any(o => o != m && o.CurJobDef == PeopleEvents4.LiftJob && o.CurJob.targetA.Thing == c)).OrderBy(c => c.Position.DistanceToSquared(m.Position)).FirstOrDefault();
                if (crate != null) PeopleEvents4.Send(m, PeopleEvents4.LiftJob, crate, 400);
            }
            // The broker up and about walks home with them.
            if (brokerHere && !broker.Downed && !broker.InBed() && broker.GetLord() != marshals[0].GetLord())
            {
                Lords.Leave(broker);
                marshals[0].GetLord()?.AddPawn(broker);
            }
            bool busy = marshals.Any(m => m.CurJobDef == PeopleEvents4.LiftJob || m.CurJobDef == Litters.CarryJob);
            bool brokerWaiting = brokerHere && (broker.Downed || broker.InBed());
            if (!busy && Present(r).Count == 0 && !brokerWaiting) Finish(v, r);
            return !v.done;
        }

        /// <summary>A crate lifted aboard (the marshal's job done): gone up with the lander.</summary>
        public static void Lifted(Pawn marshal, Thing crate)
        {
            if (crate == null || crate.Destroyed || !crate.Spawned) return;
            var map = crate.Map;
            FleckMaker.ThrowDustPuff(crate.Position, map, 1.2f);
            crate.Destroy();
        }

        /// <summary>The colony took crates the enclave left in its care: their lasting grudge.</summary>
        private static void Taken(PodRecord r, int crates)
        {
            if (r.state == "robbed") return;
            r.state = "robbed";
            var f = r.faction;
            if (f == null) return;
            FactionMemory.Note(f, "robbed", "CC_PEv4_PodName".Translate().Resolve(), PeopleEvents4Math.RobbedWeight);
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.RobbedGoodwill, GoodwillWhy.DealBroken);
            Tell.Message(NewsKind.World, Importance.Important, r.map, "CC_PEv4_PodTaken".Translate(f.Name.Named("FACTION"), crates.Named("COUNT")).Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Done: a rescue paid (a fifth of the cargo, their thanks, the colony's care remembered), and the marshals go.</summary>
        private static void Finish(Visit v, PodRecord r)
        {
            v.done = true;
            var marshals = Marshals(v);
            if (r != null && !r.done)
            {
                r.done = true;
                var f = r.faction;
                if (r.state == "rescue" && f != null && !f.HostileTo(Faction.OfPlayer))
                {
                    int pay = PeopleEvents4Math.Share(r.worth);
                    var by = marshals.FirstOrDefault();
                    Couriers.PaySilver(v.map, by != null ? by.Position : IntVec3.Invalid, pay, f, by);
                    if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.RescueGoodwill, GoodwillWhy.Helped);
                    if (r.broker != null) FactionMemory.Note(f, "tended", r.broker.LabelShort);
                    if (by != null) PeoplesG.SayLine(by, v.incident, "paid");
                    Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PEv4_PodPaid".Translate(f.Name.Named("FACTION"), pay.Named("COUNT"), (r.broker?.LabelShort ?? "?").Named("BROKER")).Resolve(),
                        by != null ? new LookTargets(by) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                }
            }
            // The broker up and about goes home with them.
            var broker = r?.broker;
            if (broker != null && broker.Spawned && !broker.Dead && !broker.Downed && broker.Map == v.map && broker.Faction == r.faction) marshals.Add(broker);
            PeoplesG.Home(marshals);
        }

        /// <summary>Dev mode: the lander for any pod waiting comes now.</summary>
        public static bool LanderNow()
        {
            var r = PeopleEvents4Book.Instance?.pods.FirstOrDefault(x => x != null && !x.done && x.state != "waiting");
            if (r == null) return false;
            r.due = Now;
            return true;
        }
    }

    /// <summary>
    /// A broker of the enclave ({X}) down hard on the colony's land in a pod, hurt among the cargo ({TALE}, {COUNT} silver's
    /// worth of it); {A} is the colony's doctor.
    /// </summary>
    public class HappeningWorker_BrokerPod : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Brokers) != null && !MapFacts.InVacuum(map) && !MapFacts.ThreatOn(map) && Colonists(map).Count > 0
            && !(PeopleEvents4Book.Instance?.pods.Any(r => r != null && !r.done && r.map == map) ?? false);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Brokers);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Factor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>The crash itself, before the letter: the broker down, the crates by them.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var r = BrokerPod.Crash(ctx);
            if (r == null) return false;
            ctx.note = r.id.ToString();
            ctx.count = r.worth;
            ctx.tale = TextUtil.JoinAnd(r.cargo.Select(t => t.LabelNoCount).ToList());
            ctx.variant = PeopleEvents4Math.Share(r.worth);
            // {BEAST} names their pay for a rescue.
            ctx.beast = ctx.variant.ToString();
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PeopleEvents2.ColonyAge <= 1) tags.Add("lowcolony");
        }

        protected override LookTargets Look(HappeningContext ctx) =>
            ctx.spot.IsValid ? new LookTargets(new TargetInfo(ctx.spot, ctx.map)) : base.Look(ctx);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var r = BrokerPod.Of(ctx);
            if (option == 0 && (r?.broker == null || r.broker.Dead)) return "CC_PEv4_BrokerDead".Translate().Resolve();
            return null;
        }

    }

    /// <summary>
    /// The colony's answer to the pod: <see cref="how"/> "rescue" (the doctor carries the broker to a bed, the game's own
    /// rescue, and tends them; where there is no bed, tends them where they lie), "robbed" (the crates the colony's, the
    /// enclave's grudge) or "left" (left for their own lander).
    /// </summary>
    public class DilemmaEffect_BrokerPod : DilemmaEffect
    {
        public string how = "left";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            switch (how)
            {
                case "rescue":
                    if (PlayOuts.On) sheet.Play("CC_PEv4_Impact_Rescue".Translate(hc.a?.LabelShort ?? "?", hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv4_Impact_RescueBrief".Translate().Resolve());
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv4_Impact_Share".Translate(hc.variant).Resolve(), "CC_PEv4_Impact_ShareBrief".Translate().Resolve(), "pev4share");
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.RescueGoodwill);
                    break;
                case "robbed":
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv4_Impact_Rob".Translate(hc.tale ?? "?", hc.count).Resolve(), "CC_PEv4_Impact_RobBrief".Translate().Resolve(), "pev4rob");
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.RobbedGoodwill);
                    sheet.Add(ImpactKind.Other, -1, "CC_PEv4_Impact_Grudge".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv4_Impact_GrudgeBrief".Translate().Resolve(), "pev4grudge");
                    break;
                default:
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv4_Impact_Left".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv4_Impact_LeftBrief".Translate().Resolve(), "pev4left");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var r = BrokerPod.Of(hc);
            if (r == null) return;
            BrokerPod.Answered(r, how);
            if (how == "robbed")
            {
                foreach (var t in BrokerPod.Present(r)) t.SetForbidden(false, false);
                r.state = "robbed";
                var f = r.faction;
                if (f != null)
                {
                    FactionMemory.Note(f, "robbed", "CC_PEv4_PodName".Translate().Resolve(), PeopleEvents4Math.RobbedWeight);
                    if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.RobbedGoodwill, GoodwillWhy.DealBroken);
                }
                return;
            }
            if (how != "rescue") return;
            var broker = r.broker;
            var doctor = hc.a;
            if (broker == null || broker.Dead || !broker.Spawned) return;
            // The one sent: the colonist named, else (drafted since, in a break, down or on the player's own order) the nearest
            // other of the colony up to it.
            if (doctor == null || !doctor.Spawned || doctor.Map != broker.Map || PeoplesReview.Held(doctor))
                doctor = PeoplesD.Able(broker.Map).Where(p => !PeoplesReview.Held(p) && !p.WorkTagIsDisabled(WorkTags.Caring))
                    .OrderBy(p => p.Position.DistanceToSquared(broker.Position)).FirstOrDefault();
            if (doctor == null) return;
            try
            {
                var bed = RestUtility.FindBedFor(broker, doctor, false, false, GuestStatus.Guest);
                if (bed != null && PlayOuts.On && doctor.CanReserveAndReach(broker, PathEndMode.OnCell, Danger.Some))
                {
                    var j = JobMaker.MakeJob(JobDefOf.Rescue, broker, bed);
                    j.count = 1;
                    doctor.jobs.TryTakeOrderedJob(j, JobTag.Misc);
                }
                else OutsiderTend.Send(doctor, new[] { broker });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the broker rescued", e);
            }
        }
    }
}
