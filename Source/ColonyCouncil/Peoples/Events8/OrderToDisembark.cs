using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 77. The order to disembark (the Hollow Fleet) =====================

    /// <summary>
    /// The order to disembark (item 77): Captain Sallow died before giving the order, and nobody since has had the rank to give it. With
    /// Royalty, when one of the colony holds an Empire title of knight or higher, an officer of the Hollow Fleet comes to the gate and asks
    /// them to come to the hull and give the order. Sent, the colonist goes there as a caravan of their own on the game's own roads (round
    /// 2's season away), gives the order in a day or two, and comes home; three to five of the crew come with them and join the colony,
    /// and the rest of the hull's crew found a people of their own near it (release B's arising, its own cause: the disembarked), warm
    /// toward the colony. With the second answer nobody comes home with them, and the new people are the warmer for it. The colonist has
    /// a lasting memory of it, and the Fleet thinks better of the colony (+10). Once a Fleet.
    /// </summary>
    public static class OrderToDisembark
    {
        public const string EventDef = "Rimfolk_Evt_OrderToDisembark";
        public const string AwayKey = "disembark";

        /// <summary>The crew who come home with the colonist, by kind: deckhands, an engineer, one of the ship's guard.</summary>
        private static readonly string[] CrewKinds = { "Rimfolk_HollowFleet_Deckhand", "Rimfolk_HollowFleet_Engineer", "Rimfolk_HollowFleet_Deckhand", "Rimfolk_HollowFleet_Guard", "Rimfolk_HollowFleet_Deckhand" };

        /// <summary>The Fleet's hull nearest the colony, within reach.</summary>
        public static Settlement Hull(Faction f, Map map)
        {
            var town = f != null && map != null ? WorldSituationWorker.NearestTown(f, map) : null;
            return town != null && Find.WorldGrid.ApproxDistanceInTiles(map.Tile, town.Tile) <= PeopleEvents8Math.HullTiles ? town : null;
        }

        /// <summary>The Empire's title a colonist holds, when it is knight or higher.</summary>
        public static RoyalTitleDef Rank(Pawn p)
        {
            if (!ModsConfig.RoyaltyActive || p?.royalty == null || Faction.OfEmpire == null || RoyalTitleDefOf.Knight == null) return null;
            var t = p.royalty.GetCurrentTitle(Faction.OfEmpire);
            return t != null && t.seniority >= RoyalTitleDefOf.Knight.seniority ? t : null;
        }

        /// <summary>The colony's highest-ranked colonist of knight or higher who could go now.</summary>
        public static Pawn Ranked(Map map) =>
            PeoplesD.Able(map).Where(p => Rank(p) != null && PeopleEvents2.MayLeave(p, map) && !SeasonAway.AwayNow(p)).OrderByDescending(p => Rank(p).seniority).FirstOrDefault();

        /// <summary>A Fleet at peace whose order hasn't been given yet.</summary>
        public static Faction Fleet() => Arrivals.Friendly(f => PeoplesD.Theirs(f, PeoplesMath.Crew) && !(PeopleEvents8Book.Instance?.Disembarked(f) ?? false));

        /// <summary>The colonist sent: a caravan of their own to the hull, the Fleet noted as answered.</summary>
        public static void Send(HappeningContext ctx, bool withCrew)
        {
            var map = ctx?.map;
            var f = ctx?.faction;
            var hull = Hull(f, map);
            if (map == null || f == null || ctx.a == null || hull == null) return;
            PeopleEvents8Book.Instance?.NoteDisembarked(f);
            var v = SeasonAway.Send(AwayKey, ctx.incident, ctx.a, null, f, hull.Tile, hull.Label, PeopleEvents8Math.OrderDays, map, withCrew ? ctx.count.ToString() : "0");
            if (v == null)
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv8_OrderCouldNotGo".Translate(ctx.a.Named("PAWN")).Resolve(), new LookTargets(ctx.a), MessageTypeDefOf.NeutralEvent);
            Arrivals.SendAway(PeoplesG.Party(ctx));
        }

        /// <summary>
        /// The crew who disembarked found a people of their own near the hull (release B's arising, its own cause), when new peoples may
        /// arise; null when none arose (the setting off, no ground for a town).
        /// </summary>
        public static Faction Arise(Faction fleet, PlanetTile hull, string hullName, Map home, int joined)
        {
            if (fleet == null || !hull.Valid || !Arising.On) return null;
            try
            {
                var cause = new ArisingCause
                {
                    kind = ArisingMath.Disembarked, key = ArisingMath.Key(ArisingMath.Disembarked, fleet.loadID), age = Ages.Of(fleet), near = hull, minTiles = 2, maxTiles = 6,
                    home = home, tiles = home != null ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, hull) : -1f, parent = fleet, name = hullName, byColony = true,
                    since = Find.TickManager.TicksGame, joined = joined
                };
                return Arising.Arise(cause);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the disembarked arise", e);
                return null;
            }
        }

        /// <summary>The crew who come home with the colonist and join, at their side.</summary>
        public static List<Pawn> CrewHome(Faction f, Pawn with, Map home, int n)
        {
            var list = new List<Pawn>();
            if (f == null || home == null || n <= 0) return list;
            for (int i = 0; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, CrewKinds[i % CrewKinds.Length]), f, new FloatRange(18f, 55f));
                if (p == null) continue;
                IntVec3? near = with != null && with.Spawned && with.Map == home ? with.Position : (IntVec3?)null;
                if (Arrivals.Join(p, home, near)) list.Add(p);
                else Arrivals.Discard(p);
            }
            return list;
        }
    }

    /// <summary>The colonist at the hull: the order given; home, the crew who came with them join and the rest found a people of their own.</summary>
    public class DisembarkAway : AwayKind
    {
        public override string Key => OrderToDisembark.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv8_OrderThere".Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            string place = SeasonAway.PlaceOf(v);
            if (!served)
            {
                Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv8_OrderHomeEarly".Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            int n = int.TryParse(SeasonAway.ExtraOf(v) ?? "0", out int k) ? k : 0;
            var crew = OrderToDisembark.CrewHome(f, p, home, n);
            PeopleEvents8.Thought(p, "Rimfolk_Thought_GaveTheOrder");
            PeopleEvents8.Goodwill(f, PeopleEvents8Math.OrderGoodwill, GoodwillWhy.Helped);
            var arose = OrderToDisembark.Arise(f, SeasonAway.TileOf(v), place, home, crew.Count);
            string key = crew.Count > 0 ? arose != null ? "CC_PEv8_OrderHomeCrewPeople" : "CC_PEv8_OrderHomeCrew" : arose != null ? "CC_PEv8_OrderHomePeople" : "CC_PEv8_OrderHome";
            var look = new List<Pawn> { p }.Concat(crew).Where(x => x != null && x.Spawned).ToList();
            Tell.Letter(NewsKind.World, Importance.Important, home, "CC_PEv8_OrderHomeLabel".Translate(p.LabelShort).Resolve(),
                key.Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION"), crew.Count.Named("COUNT"),
                    TextUtil.JoinAnd(crew.Select(c => c.LabelShort).ToList()).Named("NAMES"), (arose?.Name ?? "").Named("PEOPLE")).Resolve(),
                LetterDefOf.PositiveEvent, look.Count > 0 ? new LookTargets(look) : LookTargets.Invalid, f);
        }
    }

    /// <summary>
    /// An officer of {FACTION} ({X}) at the gate: {A} holds the Empire's title of {TALE}, the rank the Fleet's rule asks for, and the Fleet
    /// asks {A_objective} to come to the hull at {PLACE}, {RELATION} days away, and give the order to disembark; {COUNT} of the crew would come home with them.
    /// </summary>
    public class HappeningWorker_OrderToDisembark : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (!ModsConfig.RoyaltyActive) return false;
            var f = OrderToDisembark.Fleet();
            return f != null && PeoplesD.Friendly(PeoplesMath.Crew) != null && OrderToDisembark.Hull(f, map) != null && OrderToDisembark.Ranked(map) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = OrderToDisembark.Fleet();
            if (ctx.faction == null || !PeoplesD.Theirs(ctx.faction, PeoplesMath.Crew)) return false;
            var hull = OrderToDisembark.Hull(ctx.faction, ctx.map);
            ctx.a = OrderToDisembark.Ranked(ctx.map);
            if (hull == null || ctx.a == null) return false;
            var rank = OrderToDisembark.Rank(ctx.a);
            ctx.tale = rank?.GetLabelFor(ctx.a) ?? rank?.label ?? "?";
            ctx.placeName = hull.Label;
            ctx.count = PeopleEvents8Math.Joiners(Rand.Value);
            ctx.variant = Mathf.Max(1, Mathf.RoundToInt(SeasonAway.RoadDays(ctx.map, hull.Tile)));
            ctx.relation = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Officer"), ctx.faction, ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!Arising.On) tags.Add("noarise");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var a = ctx.a;
            if (a == null || a.Dead || !a.Spawned || a.Map != ctx.map || SeasonAway.AwayNow(a)) return "CC_PEv8_RankedAway".Translate(a?.LabelShort ?? "?").Resolve();
            if (OrderToDisembark.Rank(a) == null) return "CC_PEv8_NoRank".Translate(a.LabelShort).Resolve();
            return OrderToDisembark.Hull(ctx.faction, ctx.map) == null ? "CC_PEv8_HullGone".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "order" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colonist sent to the hull to give the order (OrderToDisembark.Send); with <see cref="withCrew"/>, some of the crew come home with them and join.</summary>
    public class DilemmaEffect_OrderToDisembark : DilemmaEffect
    {
        public bool withCrew = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Play("CC_PEv8_Impact_Order".Translate(hc.a.LabelShort, hc.placeName ?? "?", hc.variant).Resolve(), "CC_PEv8_Impact_OrderBrief".Translate().Resolve());
            if (withCrew) sheet.Add(ImpactKind.People, 1, "CC_PEv8_Impact_OrderCrew".Translate(hc.count).Resolve(), "CC_PEv8_Impact_OrderCrewBrief".Translate().Resolve(), "pev8crew");
            if (Arising.On)
                sheet.Add(ImpactKind.Goodwill, 1, (withCrew ? "CC_PEv8_Impact_OrderPeople" : "CC_PEv8_Impact_OrderPeopleWarm").Translate().Resolve(), "CC_PEv8_Impact_OrderPeopleBrief".Translate().Resolve(),
                    "pev8arise");
            sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_GaveOrder".Translate(hc.a.LabelShort).Resolve(), "CC_PEv8_Impact_GaveOrderBrief".Translate().Resolve(), "pev8order");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.OrderGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => OrderToDisembark.Send(ctx as HappeningContext, withCrew);
    }
}
