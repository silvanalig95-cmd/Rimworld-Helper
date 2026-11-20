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
    // ===================== 43. Mend it tonight (the Wheel) =====================

    /// <summary>
    /// Mend it tonight (item 43): the night after a raid, a crew of the Wheel comes with their lamps and goes round the
    /// colony's damaged buildings (walls, doors, turrets, furniture) mending each back to whole, sparks and a bar, while their
    /// runner gathers the raiders' broken gear lying about (forbidden, worn past its best). They are paid in that gear and a
    /// little silver, or in silver alone and the colony keeps the gear, and they are gone by first light, whatever is left.
    /// "Mend it tonight, we leave tomorrow" is one of their sayings.
    /// </summary>
    public static class MendTonight
    {
        public const string Kind = "pev:mend";
        public const string EventDef = "Rimfolk_Evt_MendTonight";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's damaged buildings, the nearest its middle first.</summary>
        public static List<Building> Damaged(Map map)
        {
            if (map == null) return new List<Building>();
            var mid = CampSite.ColonyCenter(map);
            return map.listerBuildings.allBuildingsColonist.Where(b => b != null && b.Spawned && !b.Destroyed && b.def.useHitPoints && b.MaxHitPoints > 0 && b.HitPoints < b.MaxHitPoints
                                                                      && !(b is Frame) && b.def.building != null && !b.def.building.isNaturalRock && !b.Position.Fogged(map))
                .OrderBy(b => b.Position.DistanceToSquared(mid)).Take(PeopleEvents5Math.MaxMended).ToList();
        }

        public static int Missing(List<Building> list) => list.Sum(b => Math.Max(0, b.MaxHitPoints - b.HitPoints));

        /// <summary>The raiders' broken gear lying about: weapons and clothes left forbidden where they fell, worn past their best, outside the stores.</summary>
        public static List<Thing> Gear(Map map)
        {
            if (map == null) return new List<Thing>();
            var list = new List<Thing>();
            foreach (var group in new[] { ThingRequestGroup.Weapon, ThingRequestGroup.Apparel })
                foreach (var t in map.listerThings.ThingsInGroup(group))
                    if (t.Spawned && !t.Position.Fogged(map) && t.IsForbidden(Faction.OfPlayer) && !t.IsInAnyStorage() && t.def.useHitPoints
                        && PeopleEvents5Math.Broken(t.HitPoints, t.MaxHitPoints, t is Apparel a && a.WornByCorpse))
                        list.Add(t);
            return list.Take(PeopleEvents5Math.MaxGear).ToList();
        }

        /// <summary>
        /// The homes' hour: after dark on the night after a raid (and the fight over), the Wheel at peace and not lately here,
        /// enough of the colony's buildings damaged: once a raid, at a chance (the storytellers' lean on visitors).
        /// </summary>
        public static void Watch(Map map, int now)
        {
            int raid = map.storyState.LastThreatBigTick;
            if (!PeopleEvents5Math.AfterRaid(raid, now) || !PeopleEvents5Math.MendHour(GenLocalDate.HourOfDay(map)) || MapFacts.ThreatOn(map)) return;
            var b = PeopleEvents5Book.Instance;
            if (b == null || b.MendRolled(map, raid)) return;
            var f = PeoplesD.Friendly(PeoplesMath.Salvagers);
            if (f == null || b.lastMend >= 0 && now - b.lastMend < PeopleEvents5Math.MendGapDays * GenDate.TicksPerDay) return;
            if (Damaged(map).Count < PeopleEvents5Math.MinDamaged || !Tell.CanAsk) return;
            b.NoteMendRaid(map, raid);
            if (!Rand.Chance(PeopleEvents5Math.MendChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastMend = now;
        }

        /// <summary>Dev mode: the crew comes now, as if tonight were the night after a raid.</summary>
        public static bool Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Salvagers);
            if (map == null || f == null) return false;
            var b = PeopleEvents5Book.Instance;
            if (b != null) b.lastMend = -1;
            return PeopleEvents2.Fire(EventDef, map, f);
        }

        private static List<Pawn> Crew(Visit v) => v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();

        private static Visit VisitOf(Pawn p) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && (v.x == p || v.group.Contains(p)));

        /// <summary>The night's work begins: the crew kept on the colony's ground until first light, the visit counting what they mend and gather.</summary>
        public static void Begin(HappeningContext ctx, bool gear)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var crew = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            if (map == null || t == null || crew.Count == 0) return;
            int hours = Mathf.Max(3, PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(map), 6));
            var v = t.AddVisit(Kind, ctx.incident, map, crew[0], null, crew.Skip(1).ToList(), Now + hours * GenDate.TicksPerHour);
            v.text = gear ? "gear" : "silver";
            v.stake = 0;
            v.due = 0;
            Lords.Leave(crew);
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, CampSite.ColonyCenter(map), hours * GenDate.TicksPerHour), map, crew);
            Hour(v, Now);
        }

        private static bool Busy(Pawn p) => p?.CurJobDef != null && (p.CurJobDef == PeopleEvents5.MendJob || p.CurJobDef == PeopleEvents5.GatherJob);

        /// <summary>
        /// The visit's hour: each of the crew not at work given the next damaged buildings near them (three at a time), the
        /// runner the raiders' gear when it is theirs; at first light, or with nothing left, their word and they go.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var crew = Crew(v);
            int hour = GenLocalDate.HourOfDay(v.map);
            if (crew.Count == 0 || now > v.until || hour >= 6 && hour < 12)
            {
                Finish(v, crew);
                return false;
            }
            if (v.map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            var taken = new HashSet<Thing>(crew.Where(Busy).SelectMany(p => (p.CurJob.targetQueueA ?? new List<LocalTargetInfo>()).Select(q => q.Thing).Prepend(p.CurJob.targetA.Thing)).Where(x => x != null));
            // Only what the crew can get to: a building in a room visitors keep out of (a bedroom, a cold store) is left alone, so it
            // never holds a mender up all night, and with nothing else left they go.
            var damaged = Damaged(v.map).Where(b => !taken.Contains(b) && crew[0].CanReach(b, PathEndMode.Touch, Danger.Some)).ToList();
            var gear = v.text == "gear" ? Gear(v.map).Where(g => !taken.Contains(g)).ToList() : new List<Thing>();
            var runner = gear.Count > 0 && crew.Count > 1 ? crew[crew.Count - 1] : null;
            if (damaged.Count == 0 && gear.Count == 0 && !crew.Any(Busy))
            {
                Finish(v, crew);
                return false;
            }
            foreach (var p in crew.Where(p => !Busy(p)))
            {
                if (p == runner)
                {
                    var some = gear.OrderBy(g => g.Position.DistanceToSquared(p.Position)).Take(4).ToList();
                    if (some.Count > 0 && PeopleEvents5.Send(p, PeopleEvents5.GatherJob, some[0], 240, some.Skip(1).Select(g => new LocalTargetInfo(g)).ToList()))
                        foreach (var g in some) gear.Remove(g);
                    continue;
                }
                var next = damaged.OrderBy(b => b.Position.DistanceToSquared(p.Position)).Take(3).ToList();
                if (next.Count == 0) continue;
                int ticks = PeopleEvents5Math.MendTicks(next.Max(b => b.MaxHitPoints - b.HitPoints));
                if (PeopleEvents5.Send(p, PeopleEvents5.MendJob, next[0], ticks, next.Skip(1).Select(b => new LocalTargetInfo(b)).ToList()))
                    foreach (var b in next) damaged.Remove(b);
            }
            return true;
        }

        /// <summary>A building mended back to whole.</summary>
        public static void Mended(Pawn by, Thing t)
        {
            if (t == null || t.Destroyed || !t.def.useHitPoints) return;
            t.HitPoints = t.MaxHitPoints;
            if (t is Building b && b.Spawned) b.Map.listerBuildingsRepairable?.Notify_BuildingRepaired(b);
            var v = VisitOf(by);
            if (v != null) v.stake++;
            if (Rand.Chance(0.12f)) PeoplesG.SayLine(by, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "mend");
        }

        /// <summary>A piece of the raiders' gear picked up into the runner's pack.</summary>
        public static void Gathered(Pawn by, Thing t)
        {
            if (by == null || t == null || t.Destroyed || !t.Spawned) return;
            t.DeSpawn();
            if (by.inventory?.innerContainer == null || !by.inventory.innerContainer.TryAdd(t)) t.Destroy();
            var v = VisitOf(by);
            if (v != null) v.due++;
        }

        private static void Finish(Visit v, List<Pawn> crew)
        {
            v.done = true;
            var f = PeopleEvents5.PeopleOf(v);
            if (v.stake > 0 || v.due > 0)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.MendGoodwill, GoodwillWhy.Helped);
                if (crew.Count > 0) PeoplesG.SayLine(crew[0], v.incident, "done");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map,
                    (v.due > 0 ? "CC_PEv5_MendDoneGear" : "CC_PEv5_MendDone").Translate((f?.Name ?? "?").Named("FACTION"), v.stake.Named("COUNT"), v.due.Named("GEAR")).Resolve(),
                    crew.Count > 0 ? new LookTargets(crew) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(crew);
        }
    }

    /// <summary>
    /// A crew of {FACTION} ({X} and {COUNT_crew}) at the gate the night after a raid: they would mend the colony's damaged
    /// buildings ({TALE}) by morning, paid in the raiders' broken gear and {COUNT} silver, or {DEAD} silver alone.
    /// </summary>
    public class HappeningWorker_MendTonight : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Salvagers) != null && !MapFacts.ThreatOn(map) && MendTonight.Damaged(map).Count >= PeopleEvents5Math.MinDamaged && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Salvagers);
            var damaged = MendTonight.Damaged(ctx.map);
            if (ctx.faction == null || damaged.Count < PeopleEvents5Math.MinDamaged) return false;
            int missing = MendTonight.Missing(damaged);
            var gear = MendTonight.Gear(ctx.map);
            ctx.count = PeopleEvents5Math.MendSilver(missing);
            ctx.variant = PeopleEvents5Math.MendSilverOnly(missing, gear.Sum(g => g.MarketValue));
            ctx.dead = ctx.variant.ToString();
            ctx.tale = TextUtil.JoinAnd(damaged.GroupBy(b => b.def).OrderByDescending(g => g.Count()).Take(3).Select(g => VarietyUtil.Bundle(g.Key, g.Count())).ToList());
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 1; i < PeopleEvents5Math.MendCrew; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Wheel_Hand"), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Construction, new IntRange(10, 16));
            ctx.a = PeoplesD.Able(ctx.map).OrderBy(p => p.Position.DistanceToSquared(PeoplesD.GateSpot(ctx.map) ?? p.Position)).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            ctx.note = gear.Count.ToString();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "0" ? "nogear" : "gear");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "tonight" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The night's mending (MendTonight.Begin), <see cref="gear"/> the raiders' broken gear part of their pay.</summary>
    public class DilemmaEffect_MendTonight : DilemmaEffect
    {
        public bool gear = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            sheet.Play("CC_PEv5_Impact_Mend".Translate(hc.tale ?? "?").Resolve(), "CC_PEv5_Impact_MendBrief".Translate().Resolve());
            if (gear && hc.note != "0") sheet.Goods(-1, "CC_PEv5_RaidersGear".Translate().Resolve(), int.TryParse(hc.note, out int n) ? n : 0, "pev5gear");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents5Math.MendGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => MendTonight.Begin(ctx as HappeningContext, gear);
    }
}
