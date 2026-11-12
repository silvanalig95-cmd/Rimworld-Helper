using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 6. Nobody on a sickbed is anyone's enemy (the hospitaller order) =====================

    /// <summary>
    /// Nobody on a sickbed is anyone's enemy (item 6): after a raid, with raiders still lying downed (never of a people that is
    /// everyone's enemy for good), two or three hospitallers come with litters (a stretcher cart from the industrial age) to
    /// carry them to the order's sickhouse. Let go, each is carried off at the map's edge, and their people think better of
    /// the colony by how many were carried, as the Bell's goodwill for the dead works (PeoplesDMath.GoodwillFor), and the
    /// order gives +5. Kept as prisoners, the order says nothing but writes it down (PeopleEvents3Book.kept). The watch after
    /// a raid comes from the events' hour (PeopleEvents3.Hourly), at a small chance, an hour or so after the fight.
    /// </summary>
    public static class Litters
    {
        public const string Kind = "pev:litters";
        public const string EventDef = "Rimfolk_Evt_SickbedLitters";

        private static int Now => Find.TickManager.TicksGame;

        public static JobDef CarryJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_LitterCarry");

        /// <summary>A raider lying downed after the fight: a person of a people at war with the colony (not everyone's enemy for good), not taken, not carried, not in a bed.</summary>
        public static bool Lying(Pawn p, Map map)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map != map || !p.Downed || p.RaceProps?.Humanlike != true || p.Position.Fogged(map)) return false;
            var f = p.Faction;
            if (f == null || f.IsPlayer || f.def == null || !f.def.humanlikeFaction || f.def.permanentEnemy || !f.HostileTo(Faction.OfPlayer)) return false;
            return !p.IsPrisoner && p.CarriedBy == null && !p.InBed();
        }

        public static List<Pawn> Downed(Map map) =>
            map == null ? new List<Pawn>() : map.mapPawns.AllPawnsSpawned.Where(p => Lying(p, map)).Take(PeopleEvents3Math.LittersMax).ToList();

        /// <summary>The worst hurt first: bleeding fastest, then nearest death.</summary>
        public static List<Pawn> Worst(List<Pawn> downed) =>
            downed.OrderByDescending(p => p.health?.hediffSet?.BleedRateTotal ?? 0f).ThenBy(p => p.health?.summaryHealth?.SummaryHealthPercent ?? 1f).ToList();

        // ---------------- The watch after a raid ----------------

        private static readonly Dictionary<int, (bool rolled, int at, int tries)> pending = new Dictionary<int, (bool, int, int)>();

        public static void Forget() => pending.Clear();

        /// <summary>
        /// The events' hour at a home: raiders lying downed and the fight over, the order at peace, not soon after the last
        /// time: once a raid, a roll at a small chance, and if it comes the hospitallers come an hour or a few later.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            int key = map.uniqueID;
            if (Downed(map).Count == 0)
            {
                pending.Remove(key);
                return;
            }
            if (MapFacts.ThreatOn(map)) return;
            var b = PeopleEvents3Book.Instance;
            if (b == null || PeoplesD.Friendly(PeoplesMath.Healers) == null) return;
            if (b.lastLitters >= 0 && now - b.lastLitters < PeopleEvents3Math.LittersGapDays * GenDate.TicksPerDay) return;
            if (!pending.TryGetValue(key, out var p))
            {
                bool comes = Rand.Chance(PeopleEvents3Math.LittersChance * StoryDirection.Factor(StoryWaysMath.Visitors));
                p = (true, comes ? now + Rand.RangeInclusive(PeopleEvents3Math.LittersMinHours, PeopleEvents3Math.LittersMaxHours) * GenDate.TicksPerHour : -1, 0);
                pending[key] = p;
            }
            if (p.at < 0 || now < p.at) return;
            if (PeopleEvents2.Fire(EventDef, map, PeoplesD.Friendly(PeoplesMath.Healers)))
            {
                b.lastLitters = now;
                pending[key] = (true, -1, 0);
            }
            else pending[key] = p.tries >= 6 ? (true, -1, 0) : (true, p.at, p.tries + 1);
        }

        /// <summary>Dev mode: the hospitallers come within the hour for the raiders lying here.</summary>
        public static void Soon(Map map)
        {
            if (map == null) return;
            pending[map.uniqueID] = (true, Now, 0);
            var b = PeopleEvents3Book.Instance;
            if (b != null) b.lastLitters = -1;
        }

        // ---------------- The carrying ----------------

        /// <summary>The litters set going: the hospitallers carry <paramref name="who"/> off at the map's edge one by one.</summary>
        public static Visit Begin(HappeningContext ctx, List<Pawn> who)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var party = PeoplesG.Party(ctx).Where(p => p.Spawned && !p.Dead && !p.Downed).ToList();
            if (t == null || party.Count == 0 || who.Count == 0) return null;
            var v = t.AddVisit(Kind, ctx.incident, ctx.map, party[0], null, party.Skip(1).ToList(), Now + GenDate.TicksPerDay);
            v.touched.AddRange(who);
            v.text = ctx.faction.GetUniqueLoadID() + "|";
            foreach (var p in party) Next(v, p);
            return v;
        }

        private static List<Pawn> Bearers(Visit v) => v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();

        private static bool Carrying(Pawn p) => p?.CurJobDef != null && p.CurJobDef == CarryJob;

        /// <summary>
        /// The next one to carry for this bearer, nearest first, none another bearer has: their litter job, or false. With
        /// <paramref name="queued"/>, it waits for the job they are finishing (set from that job's last step).
        /// </summary>
        private static bool Next(Visit v, Pawn bearer, bool queued = false)
        {
            var job = CarryJob;
            if (job == null || bearer == null || (!queued && Carrying(bearer))) return false;
            var taken = new HashSet<Thing>(Bearers(v).Where(p => p != bearer && Carrying(p)).Select(p => p.CurJob.targetA.Thing));
            var raider = v.touched.Where(r => Lying(r, v.map) && !taken.Contains(r) && bearer.CanReserveAndReach(r, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(r => r.Position.DistanceToSquared(bearer.Position)).FirstOrDefault();
            if (raider == null) return false;
            if (!RCellFinder.TryFindBestExitSpot(bearer, out var edge, TraverseMode.ByPawn)) return false;
            try
            {
                var j = JobMaker.MakeJob(job, raider, edge);
                j.count = 1;
                j.locomotionUrgency = LocomotionUrgency.Walk;
                if (queued)
                {
                    bearer.jobs.jobQueue.EnqueueFirst(j);
                    return true;
                }
                bearer.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return Carrying(bearer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: a litter", e);
                return false;
            }
        }

        private static Visit VisitOf(Pawn bearer) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && (v.x == bearer || v.group.Contains(bearer)));

        /// <summary>At the map's edge with one on the litter: laid in the cart (or carried on), off the map, counted; then the next.</summary>
        public static void AtEdge(Pawn bearer)
        {
            // The body of one who died in the field sickhouse, carried home by the Bell or the sisters.
            if (bearer?.carryTracker?.CarriedThing is Corpse)
            {
                FieldSickhouse.BodyAway(bearer);
                return;
            }
            var v = VisitOf(bearer);
            if (bearer?.carryTracker?.CarriedThing is Pawn raider && bearer.Spawned)
            {
                var f = raider.Faction;
                if (bearer.carryTracker.TryDropCarriedThing(bearer.Position, ThingPlaceMode.Near, out var dropped) && dropped is Pawn laid && laid.Spawned && !laid.Dead)
                {
                    try
                    {
                        laid.ExitMap(false, CellRect.WholeMap(laid.Map).GetClosestEdge(laid.Position));
                        if (v != null && f != null) v.text += f.GetUniqueLoadID() + ",";
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 3: off on the litter", e);
                    }
                }
            }
            if (v == null) return;
            v.touched.RemoveAll(r => r == null || r.Dead || !r.Spawned);
            // The next one as soon as this one is laid down, while there are any.
            if (v.touched.Count > 0) Next(v, bearer, true);
        }

        /// <summary>The litters' hour: anyone idle takes the next; when none are left (or the day is up), the order's thanks and their people's, and they go.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            v.touched.RemoveAll(r => r == null || !Lying(r, v.map));
            var bearers = Bearers(v);
            foreach (var p in bearers) Next(v, p);
            bool busy = bearers.Any(Carrying);
            if (!busy && (v.touched.Count == 0 || bearers.Count == 0 || now > v.until)) Done(v);
            return !v.done;
        }

        private static void Done(Visit v)
        {
            v.done = true;
            var parts = (v.text ?? "").Split('|');
            var order = PeoplesF.PeopleOf(v);
            var counts = new Dictionary<Faction, int>();
            if (parts.Length > 1)
                foreach (string id in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.GetUniqueLoadID() == id);
                    if (f != null) counts[f] = (counts.TryGetValue(f, out int n) ? n : 0) + 1;
                }
            int all = counts.Values.Sum();
            var b = PeopleEvents3Book.Instance;
            if (b != null) b.carried += all;
            foreach (var kv in counts)
                if (kv.Key.HasGoodwill && !kv.Key.defeated) WorldSituationWorker.Goodwill(kv.Key, PeoplesDMath.GoodwillFor(kv.Value), false);
            if (all > 0 && order != null && order.HasGoodwill && !order.IsPlayer) WorldSituationWorker.Goodwill(order, PeopleEvents3Math.LittersGoodwill, false);
            var party = new List<Pawn> { v.x };
            party.AddRange(v.group);
            if (v.x != null && v.x.Spawned) PeoplesG.SayLine(v.x, v.incident, "gone");
            Arrivals.SendAway(party.Where(p => p != null));
            if (all > 0)
            {
                string peoples = TextUtil.JoinAnd(counts.Keys.Select(f => f.Name).ToList());
                Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PEv3_LittersDone".Translate((order?.Name ?? "?").Named("FACTION"), all.Named("COUNT"), peoples.Named("PEOPLES")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    /// <summary>
    /// Hospitallers of {FACTION} at the gate with litters after the fight: {COUNT} raiders of {ENEMY} (and others) still lie
    /// downed, and the order would carry them to its sickhouse. {A} is who they speak to.
    /// </summary>
    public class HappeningWorker_SickbedLitters : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Healers) != null && Litters.Downed(map).Count > 0 && !MapFacts.ThreatOn(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Healers);
            var downed = Litters.Downed(ctx.map);
            if (ctx.faction == null || downed.Count == 0) return false;
            ctx.count = downed.Count;
            ctx.enemy = downed.GroupBy(p => p.Faction).OrderByDescending(g => g.Count()).First().Key;
            ctx.variant = PeopleEvents3Math.WorstHurt(downed.Count);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Healer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = PeopleEvents3Math.Litters(downed.Count);
            for (int i = 1; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, i == 1 ? "Rimfolk_Order_Brother" : "Rimfolk_Order_Lay"), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Best(ctx.map, SkillDefOf.Medicine) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEvents.AgeOf(ctx.faction) >= 2 ? "cart" : "litters");
            if (ctx.count == 1) tags.Add("one");
            if (Litters.Downed(ctx.map).Select(p => p.Faction).Distinct().Count() > 1) tags.Add("many");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && Litters.Downed(ctx.map).Count == 0 ? "CC_PEv3_NoneLying".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "carry" : "kept");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The hospitallers carry them off (all, or with <see cref="worst"/> only the worst hurt; the rest are the colony's to keep).</summary>
    public class DilemmaEffect_Litters : DilemmaEffect
    {
        public bool worst;

        public override bool ChanceEach => false;

        private List<Pawn> Who(Map map, out int kept)
        {
            var downed = Litters.Downed(map);
            var who = worst ? Litters.Worst(downed).Take(PeopleEvents3Math.WorstHurt(downed.Count)).ToList() : downed;
            kept = downed.Count - who.Count;
            return who;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = ctx?.map;
            if (map == null) return;
            var who = Who(map, out int kept);
            if (who.Count == 0) return;
            sheet.Play("CC_PEv3_Impact_Litters".Translate(who.Count).Resolve(), "CC_PEv3_Impact_LittersBrief".Translate().Resolve());
            foreach (var g in who.GroupBy(p => p.Faction).Where(g => g.Key != null && g.Key.HasGoodwill))
                sheet.Goodwill(g.Key, PeoplesDMath.GoodwillFor(g.Count()));
            if (ctx.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents3Math.LittersGoodwill);
            if (kept > 0) sheet.Add(ImpactKind.People, 0, "CC_PEv3_Impact_LittersKeep".Translate(kept).Resolve(), "CC_PEv3_Impact_LittersKeepBrief".Translate().Resolve(), "pev3keep");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            var who = Who(hc.map, out int kept);
            if (Litters.Begin(hc, who) == null) return;
            var b = PeopleEvents3Book.Instance;
            if (b != null) b.kept += kept;
        }
    }

    /// <summary>The colony keeps them all: the order says nothing, but writes it down.</summary>
    public class DilemmaEffect_LittersKept : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = Litters.Downed(ctx?.map).Count;
            if (n > 0) sheet.Add(ImpactKind.Other, 0, "CC_PEv3_Impact_Kept".Translate(n).Resolve(), "CC_PEv3_Impact_KeptBrief".Translate().Resolve(), "pev3kept");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var b = PeopleEvents3Book.Instance;
            if (b != null) b.kept += Litters.Downed(ctx?.map).Count;
        }
    }

    /// <summary>
    /// A hospitaller carries one downed raider to the map's edge on a litter (target A the raider, B the edge), where the
    /// cart or the litter takes them on (Litters.AtEdge), and goes for the next.
    /// </summary>
    public class JobDriver_LitterCarry : JobDriver
    {
        /// <summary>A raider lying downed, or the body of one who died in the field sickhouse (FieldSickhouse.cs).</summary>
        private Thing Load => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => Load is Pawn p && (p.Dead || (!p.Downed && pawn.carryTracker.CarriedThing != p)));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var away = ToilMaker.MakeToil("LitterAway");
            away.initAction = () => Litters.AtEdge(pawn);
            away.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return away;
        }
    }
}
