using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 52. Fire in the canopy (the Mossborn) =====================

    /// <summary>
    /// Fire in the canopy (item 52): when a wildfire burns in the colony's woods (six fires or more on its trees or among them:
    /// Seasonal Weather's wildfire, a dry storm's lightning, anything), three to five thorn-guards of the Mossborn come at a run
    /// from the edge nearest it and beat it out beside the colony's people, fire by fire (the game's own beating, its burns the
    /// game's own). Their letter comes once they are already at it: the colony may ask them back to replant the burn once the
    /// fire is out (the camp after the fire, item 54), or thank them and send them home now. When no fire is left within their
    /// reach they say their word and go.
    /// </summary>
    public static class CanopyFire
    {
        public const string Kind = "pev:canopy";
        public const string EventDef = "Rimfolk_Evt_CanopyFire";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The fires burning on the colony's trees or among them.</summary>
        public static List<Fire> Canopy(Map map)
        {
            var list = new List<Fire>();
            if (map == null) return list;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Fire))
            {
                if (!(t is Fire f) || !f.Spawned || f.Position.Fogged(map) || f.Position.Roofed(map)) continue;
                bool tree = f.parent is Plant p && p.def.plant?.IsTree == true || f.Position.GetPlant(map) is Plant q && q.def.plant?.IsTree == true;
                if (tree) list.Add(f);
            }
            return list;
        }

        /// <summary>The middle of the wildfire.</summary>
        public static IntVec3 Middle(List<Fire> fires)
        {
            if (fires.NullOrEmpty()) return IntVec3.Invalid;
            int x = 0, z = 0;
            foreach (var f in fires)
            {
                x += f.Position.x;
                z += f.Position.z;
            }
            var mid = new IntVec3(x / fires.Count, 0, z / fires.Count);
            return fires.OrderBy(f => f.Position.DistanceToSquared(mid)).First().Position;
        }

        /// <summary>
        /// The homes' hour: a wildfire in the colony's woods, the Mossborn at peace and not lately here: once a fire (a day), at a
        /// chance (the storytellers' lean on visitors), they come within the hour.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var fires = Canopy(map);
            if (fires.Count < PeopleEvents6Math.CanopyFires) return;
            var b = PeopleEvents6Book.Instance;
            int day = GenDate.DaysPassed;
            if (b == null || b.CanopyRolled(map, day) || b.CanopyRolled(map, day - 1)) return;
            var f = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            if (f == null || b.lastCanopy >= 0 && now - b.lastCanopy < PeopleEvents6Math.CanopyGapDays * GenDate.TicksPerDay || !Tell.CanAsk) return;
            b.NoteCanopy(map, day);
            if (!Rand.Chance(PeopleEvents6Math.CanopyChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastCanopy = now;
        }

        /// <summary>Dev mode: a fire started among the trees nearest the colony, and the thorn-guards come to it.</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            if (map == null || f == null) return "no Mossborn at peace.";
            if (Canopy(map).Count < PeopleEvents6Math.CanopyFires)
            {
                var mid = CampSite.ColonyCenter(map);
                var trees = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Where(t => t is Plant p && p.def.plant.IsTree && !t.Position.Roofed(map) && !(map.areaManager.Home?[t.Position] ?? false))
                    .OrderBy(t => t.Position.DistanceToSquared(mid)).Take(PeopleEvents6Math.CanopyFires + 2).ToList();
                foreach (var t in trees) t.TryAttachFire(0.6f, null);
            }
            var b = PeopleEvents6Book.Instance;
            if (b != null) b.lastCanopy = -1;
            return PeopleEvents2.Fire(EventDef, map, f) ? "the thorn-guards are on their way to the fire." : "no wildfire in the trees here (six fires on trees or more).";
        }

        private static List<Pawn> Guards(Visit v) => v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();

        private static Visit VisitAt(Map map) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.map == map);

        /// <summary>Their run in: at the edge nearest the fire, straight to the nearest fires; the visit kept for the hours after.</summary>
        public static bool Run(HappeningContext ctx)
        {
            var map = ctx?.map;
            var party = PeoplesG.Party(ctx);
            if (map == null || party.Count == 0 || ctx.faction == null || !ctx.spot.IsValid) return false;
            var edge = LegendBeasts.EdgeToward(map, ctx.spot);
            if (!edge.IsValid) return false;
            foreach (var p in party)
                if (!Arrivals.Spawn(p, map, edge)) return false;
            try
            {
                LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, ctx.spot, PeopleEvents6Math.CanopyHours * GenDate.TicksPerHour), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the thorn-guards' run", e);
                return false;
            }
            var v = CouncilGameComponent.Instance?.happenings?.AddVisit(Kind, ctx.incident, map, party[0], null, party.Skip(1).ToList(), Now + PeopleEvents6Math.CanopyHours * GenDate.TicksPerHour);
            if (v == null) return false;
            v.cell = ctx.spot;
            v.text = "fight";
            PeopleEvents6.BeatFires(party, ctx.spot, PeopleEvents6Math.CanopyRadius);
            return true;
        }

        /// <summary>The colony's answer: "fight" (they beat it out), "replant" (and come back to replant the burn), "away" (they go now).</summary>
        public static void Terms(HappeningContext ctx, string how)
        {
            var v = VisitAt(ctx?.map);
            if (v == null) return;
            v.text = how;
            if (how == "replant")
            {
                PeopleEvents6Book.Instance?.AddAfter(new AfterRecord { map = ctx.map, faction = ctx.faction, due = Now + GenDate.TicksPerDay / 2 });
                return;
            }
            if (how != "away") return;
            v.done = true;
            PeoplesG.Home(Guards(v));
        }

        /// <summary>The visit's hour: while fires burn within their reach, each of them not at it sent to the nearest; with none left, their word and they go.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var guards = Guards(v);
            if (guards.Count == 0 || now > v.until)
            {
                Finish(v, guards);
                return false;
            }
            var fires = PeopleEvents6.Fires(v.map, v.cell, PeopleEvents6Math.CanopyRadius);
            if (PeopleEvents6Math.Out(fires.Count))
            {
                Finish(v, guards);
                return false;
            }
            PeopleEvents6.BeatFires(guards, v.cell, PeopleEvents6Math.CanopyRadius);
            if (Rand.Chance(0.25f)) PeoplesG.SayLine(guards.RandomElement(), v.incident, "beat");
            return true;
        }

        private static void Finish(Visit v, List<Pawn> guards)
        {
            v.done = true;
            var f = PeopleEvents5.PeopleOf(v);
            bool out_ = PeopleEvents6Math.Out(PeopleEvents6.Fires(v.map, v.cell, PeopleEvents6Math.CanopyRadius).Count);
            if (out_ && guards.Count > 0 && v.text != "away")
            {
                if (PeopleEvents6.Thanks(f)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.CanopyGoodwill, GoodwillWhy.Helped);
                // together: the thorn-guards' word in the ash beside the colony's people who fought it with them.
                PeoplesG.SayLine(guards[0], v.incident, v.text == "replant" ? "back" : "out");
                int burned = AfterFire.Burned(v.map, v.cell, PeopleEvents6Math.CanopyRadius).Count;
                Tell.Message(NewsKind.Land, Importance.Notable, v.map,
                    (v.text == "replant" ? "CC_PEv6_CanopyOutBack" : "CC_PEv6_CanopyOut").Translate((f?.Name ?? "?").Named("FACTION"), burned.Named("COUNT")).Resolve(),
                    new LookTargets(guards), MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(guards);
        }
    }

    /// <summary>
    /// Thorn-guards of {FACTION} ({X} and {COUNT} in all) came at a run when a wildfire took the colony's woods ({DEAD} fires), and
    /// are already beating it out; their letter asks what follows.
    /// </summary>
    public class HappeningWorker_CanopyFire : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Grovekeepers) != null && CanopyFire.Canopy(map).Count >= PeopleEvents6Math.CanopyFires && PeopleEvents6.FirefightJob != null
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            var fires = CanopyFire.Canopy(ctx.map);
            if (ctx.faction == null || fires.Count < PeopleEvents6Math.CanopyFires) return false;
            ctx.spot = CanopyFire.Middle(fires);
            int n = Rand.RangeInclusive(PeopleEvents6Math.GuardsMin, PeopleEvents6Math.GuardsMax);
            for (int i = 0; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Mossborn_Thorn"), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.count = PeoplesG.Party(ctx).Count;
            ctx.variant = fires.Count;
            ctx.dead = fires.Count.ToString();
            ctx.a = PeoplesD.Able(ctx.map).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>Their run in from the edge nearest the fire, already beating it out as the letter comes.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => CanopyFire.Run(ctx);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant >= 20 ? "big" : "small");
        }

        protected override LookTargets Look(HappeningContext ctx) => PeoplesG.Party(ctx).Any(p => p.Spawned) ? new LookTargets(PeoplesG.Party(ctx).Where(p => p.Spawned)) : base.Look(ctx);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "fight" : option == 1 ? "replant" : "away");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>What follows the fire (CanopyFire.Terms): <see cref="how"/> "fight", "replant", or "away".</summary>
    public class DilemmaEffect_CanopyFire : DilemmaEffect
    {
        public string how = "fight";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            switch (how)
            {
                case "fight":
                case "replant":
                    sheet.Play("CC_PEv6_Impact_Canopy".Translate().Resolve(), "CC_PEv6_Impact_CanopyBrief".Translate().Resolve());
                    if (how == "replant")
                        sheet.Add(ImpactKind.People, 1, "CC_PEv6_Impact_CanopyBack".Translate(f?.Name ?? "?").Resolve(), "CC_PEv6_Impact_CanopyBackBrief".Translate().Resolve(), "pev6back");
                    if (f != null) sheet.Goodwill(f, PeopleEvents6Math.CanopyGoodwill);
                    break;
                default:
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv6_Impact_CanopyAway".Translate(f?.Name ?? "?").Resolve(), "CC_PEv6_Impact_CanopyAwayBrief".Translate().Resolve(), "pev6canopyaway");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => CanopyFire.Terms(ctx as HappeningContext, how);
    }
}
