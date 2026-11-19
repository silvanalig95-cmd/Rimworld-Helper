using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 46. The Wheel stops by (the Wheel) =====================

    /// <summary>The yard's days at the edge: the mending had, an argument over a price, the days it stayed on.</summary>
    public class YardState : CampWorkState
    {
        public bool mended, sour, quarrelRolled, stayRolled, stayed;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref mended, "mended");
            Scribe_Values.Look(ref sour, "sour");
            Scribe_Values.Look(ref quarrelRolled, "quarrelRolled");
            Scribe_Values.Look(ref stayRolled, "stayRolled");
            Scribe_Values.Look(ref stayed, "stayed");
        }
    }

    /// <summary>
    /// The Wheel stops by (item 46): when a Wheel town's move (Peoples/WheelTowns.cs) brings it within a few tiles of one of the
    /// colony's homes, its yard camps three days at that map's edge: carts and pack beasts, a mending tent and a scrap market
    /// (the game's own trading with their trader). The colony buys, sells its scrap, and has its worn clothes and weapons
    /// mended (the fixer's own mending, Happenings/VisitorServices.cs). It can turn, once a stay each: a fixer and a colonist
    /// argue over a price at the stall (the camp's unease up, and the rest of the stay's mending a tenth dearer), or the yard
    /// decides to stay two days longer.
    /// </summary>
    public static class WheelStopsBy
    {
        public const string EventDef = "Rimfolk_Evt_WheelStopsBy";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_WheelYard");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static YardState StateOf(Camp c) => c?.workState as YardState;

        /// <summary>A Wheel town has moved (WheelTowns.Move): a home within a few tiles of where it stands now has its yard due within the day.</summary>
        public static void Moved(Settlement s)
        {
            var b = PeopleEvents5Book.Instance;
            var f = s?.Faction;
            if (b == null || f == null || !PeoplesD.Theirs(f, PeoplesMath.Salvagers)) return;
            foreach (var map in Homes.All)
            {
                if (map == null || !PeopleEvents5Math.Near(Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile))) continue;
                b.AddYard(new YardRecord
                {
                    map = map, faction = f, town = s.Label,
                    due = Now + Rand.RangeInclusive(PeopleEvents5Math.YardMinHours, PeopleEvents5Math.YardMaxHours) * GenDate.TicksPerHour
                });
            }
        }

        /// <summary>The yard due at this home now.</summary>
        public static YardRecord Due(Map map) =>
            PeopleEvents5Book.Instance?.yards.FirstOrDefault(r => r != null && !r.done && r.map == map && Find.TickManager.TicksGame >= r.due && PeoplesD.Theirs(r.faction, PeoplesMath.Salvagers));

        /// <summary>The events' hour: a yard due comes once the colony can be asked; one waiting two days is let go.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null) return;
            foreach (var r in b.yards.Where(r => r != null && !r.done))
            {
                if (r.map == null || !Homes.IsHome(r.map) || now > r.due + 2 * GenDate.TicksPerDay || !PeoplesD.Theirs(r.faction, PeoplesMath.Salvagers))
                {
                    r.done = true;
                    continue;
                }
                if (now < r.due || MapFacts.ThreatOn(r.map) || !Tell.CanAsk || !CampTracker.RoomFor(r.map) || On(r.map) != null) continue;
                if (PeopleEvents2.Fire(EventDef, r.map, r.faction)) r.done = true;
            }
        }

        /// <summary>Dev mode: a yard of the Wheel's nearest town is due here within the hour, as if it had just moved near.</summary>
        public static bool Soon(Map map)
        {
            var b = PeopleEvents5Book.Instance;
            var f = PeoplesD.Friendly(PeoplesMath.Salvagers);
            if (b == null || map == null || f == null) return false;
            b.yards.RemoveAll(r => r != null && r.map == map);
            string town = Find.WorldObjects.Settlements.Where(s => s.Faction == f).OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile)).FirstOrDefault()?.Label ?? f.Name;
            b.AddYard(new YardRecord { map = map, faction = f, town = town, due = Now });
            return true;
        }

        /// <summary>The colony's worn clothes and weapons, in its stores and on its people at home, worst first (as the tinker sees them).</summary>
        public static List<Thing> Worn(Map map)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            bool Bad(Thing t) => t != null && !t.Destroyed && t.def.useHitPoints && t.def.stackLimit == 1 && (t.def.IsApparel || t.def.IsWeapon) && t.MaxHitPoints > 0 && t.HitPoints < t.MaxHitPoints * 0.9f;
            foreach (var group in new[] { ThingRequestGroup.Apparel, ThingRequestGroup.Weapon })
                foreach (var t in map.listerThings.ThingsInGroup(group))
                    if (t.Spawned && Bad(t) && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()) list.Add(t);
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.apparel != null) list.AddRange(p.apparel.WornApparel.Where(Bad));
                if (p.equipment != null) list.AddRange(p.equipment.AllEquipmentListForReading.Where(Bad));
            }
            return list.Distinct().OrderBy(t => (float)t.HitPoints / t.MaxHitPoints).Take(20).ToList();
        }

        /// <summary>What the mending costs: a third of what the damage takes off their worth (a tenth more after an argument).</summary>
        public static int MendPrice(List<Thing> things, bool sour) =>
            PeopleEvents5Math.YardMendPrice(things.Sum(t => (1f - (float)t.HitPoints / t.MaxHitPoints) * t.GetStatValue(StatDefOf.MarketValueIgnoreHp)), sour);

        /// <summary>The yard's fixer: their trader, else one of them.</summary>
        public static Pawn Fixer(Camp c) => c.trader != null && PeoplesG.Up(c.trader, c.map) ? c.trader : c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);

        /// <summary>The camp made at the map's edge nearest the colony, the yard rolling in.</summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 3);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var edge = LegendBeasts.EdgeToward(map, CampSite.ColonyCenter(map));
                var center = edge.IsValid ? CampLand.TryFindNear(map, edge, 4f, 16f, hot) : IntVec3.Invalid;
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new YardState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the Wheel's yard", e);
                return null;
            }
        }

        /// <summary>
        /// Once a stay, when one of the colony is at the yard by its fixer: they argue over a price, at a chance. The camp's
        /// unease up, the rest of the stay's mending a tenth dearer; told.
        /// </summary>
        public static void Quarrel(Camp c, YardState st)
        {
            var fixer = Fixer(c);
            if (fixer == null) return;
            var colonist = c.map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Downed && !p.Drafted && p.Position.InHorDistOf(fixer.Position, 8f) && Together.Can(p, fixer));
            if (colonist == null) return;
            st.quarrelRolled = true;
            if (!Rand.Chance(PeopleEvents5Math.QuarrelChance)) return;
            st.sour = true;
            c.AddTension(PeopleEvents5Math.QuarrelTension);
            // together: the colonist is at the yard's stall beside the fixer (Together.Can above).
            c.SayMoment(fixer, "haggle");
            Speech.Say(colonist, TextVariants.Vary("CC_PEv5_YardHaggleBack").Translate().Resolve());
            c.Note(c.Text("CC_PEv5_YardQuarrel", fixer, colonist), MessageTypeDefOf.NeutralEvent, Importance.Notable, fixer, true);
        }

        /// <summary>Once a stay, on the second day: the yard decides to stay two days longer; told.</summary>
        public static void StayOn(Camp c, YardState st)
        {
            st.stayRolled = true;
            if (c.untilTick == int.MaxValue || !Rand.Chance(PeopleEvents5Math.StayLongerChance)) return;
            st.stayed = true;
            c.untilTick += PeopleEvents5Math.StayLongerDays * GenDate.TicksPerDay;
            c.AddTension(PeopleEvents5Math.StayLongerTension);
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "stay");
            c.Note(c.Text("CC_PEv5_YardStays", c.leader, null, PeopleEvents5Math.StayLongerDays), MessageTypeDefOf.NeutralEvent, Importance.Notable, c.leader, true);
        }
    }

    /// <summary>The Wheel's yard at the edge: its fixer's mending, the scrap market (the camps' trader), the argument, the days it stays on.</summary>
    public class CampWorker_WheelYard : CampWorker
    {
        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var edge = LegendBeasts.EdgeToward(map, CampSite.ColonyCenter(map));
            if (!edge.IsValid) return usual;
            var c = CampLand.TryFindNear(map, edge, 4f, 16f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is YardState)) c.workState = new YardState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is YardState st))
            {
                Started(c);
                st = WheelStopsBy.StateOf(c);
            }
            if (!c.Settled) return;
            if (!st.quarrelRolled && hour >= 9 && hour <= 18) WheelStopsBy.Quarrel(c, st);
            if (!st.stayRolled && c.DaysHere >= 2f && hour == 12) WheelStopsBy.StayOn(c, st);
        }

        public override void Leaving(Camp c)
        {
            var st = WheelStopsBy.StateOf(c);
            if (st == null || c.Hostile || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            if ((st.mended || c.tradeDays > 0) && c.tension < 50f) WorldSituationWorker.Goodwill(c.faction, PeopleEvents5Math.YardGoodwill, GoodwillWhy.Helped);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = WheelStopsBy.StateOf(c);
            if (st == null) return;
            if (st.sour) marks.Add("sour");
            if (st.stayed) marks.Add("stayed");
            if (st.mended) marks.Add("mended");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = WheelStopsBy.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "mend":
                    if (WheelStopsBy.Fixer(c) == null) return "CC_PEv5_YardNoFixer".Translate().Resolve();
                    return WheelStopsBy.Worn(c.map).Count == 0 ? "CC_PEv5_YardNothingWorn".Translate().Resolve() : null;
                case "scrap":
                    return HappeningWorker_WheelScrap.Scrap(c.map, out _, out _) == 0 ? "CC_PEv5_YardNoScrap".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A fixer of {FACTION} ({X}) at the gate: their town, {PLACE}, has moved near, and its yard would camp three days at the edge of the colony's land.</summary>
    public class HappeningWorker_WheelStopsBy : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            WheelStopsBy.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && WheelStopsBy.Due(map) != null && WheelStopsBy.On(map) == null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = WheelStopsBy.Due(ctx.map);
            ctx.faction = r?.faction;
            if (r == null || ctx.faction == null) return false;
            ctx.placeName = r.town ?? ctx.faction.Name;
            int n = HappeningWorker_WheelScrap.Scrap(ctx.map, out int slag, out float worth);
            ctx.variant = Math.Min(n, PeoplesDMath.ScrapMax);
            float share = n > 0 ? ctx.variant / (float)n : 1f;
            ctx.count = ctx.variant > 0 ? PeoplesDMath.ScrapPrice(Mathf.RoundToInt(slag * share), worth * share) : 0;
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant > 0 ? "scrap" : "noscrap");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (WheelStopsBy.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            if (option == 1 && ctx.variant <= 0) return "CC_PeoplesD_ScrapGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Their scrap bought: paid at the gate (Couriers/: the silver carried in); the scrap goes out by the answer's errand.
            if (option == 1 && !byDefault && ctx.count > 0) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "yard" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The yard rolls in and camps at the edge (WheelStopsBy.Start).</summary>
    public class DilemmaEffect_WheelYard : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv5_Impact_Yard".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv5_Impact_YardBrief".Translate().Resolve(), "pev5yard");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            WheelStopsBy.Start(hc);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The yard's fixer mends the colony's worn things one by one where they are (the tinker's own way), its price carried out to the camp.</summary>
    public class DilemmaEffect_YardMend : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Camp Yard(DilemmaContext ctx) => CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_WheelYard);

        private static DilemmaEffect_CarryOut Pay(Camp c) =>
            new DilemmaEffect_CarryOut { what = "silver", count = WheelStopsBy.MendPrice(WheelStopsBy.Worn(c.map), WheelStopsBy.StateOf(c)?.sour ?? false), to = PlaySpot.Camp, steward = false };

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = Yard(ctx);
            return c == null ? null : Pay(c).Unavailable(ctx);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = Yard(ctx);
            if (c == null) return;
            Pay(c).Describe(ctx, sheet);
            sheet.Play("CC_PEv5_Impact_YardMend".Translate(WheelStopsBy.Worn(c.map).Count).Resolve(), "CC_PEv5_Impact_YardMendBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = Yard(ctx);
            var st = WheelStopsBy.StateOf(c);
            var fixer = c != null ? WheelStopsBy.Fixer(c) : null;
            if (c == null || st == null) return;
            var pile = WheelStopsBy.Worn(c.map);
            Pay(c).Apply(ctx);
            st.mended = true;
            if (fixer == null || !VisitorServices.Mend(c.map, fixer, pile, c.Text("CC_PEv5_YardMended", fixer, null, pile.Count)))
                foreach (var t in pile) t.HitPoints = t.MaxHitPoints;
        }
    }

    /// <summary>The colony's scrap carried out to the yard's market and paid for there (as the scrap-runners pay).</summary>
    public class DilemmaEffect_YardScrap : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static int Price(Map map, out int n)
        {
            int all = HappeningWorker_WheelScrap.Scrap(map, out int slag, out float worth);
            n = Math.Min(all, PeoplesDMath.ScrapMax);
            float share = all > 0 ? n / (float)all : 1f;
            return n > 0 ? PeoplesDMath.ScrapPrice(Mathf.RoundToInt(slag * share), worth * share) : 0;
        }

        public override string Unavailable(DilemmaContext ctx) => Price(ctx?.map, out int n) <= 0 || n <= 0 ? "CC_PEv5_YardNoScrap".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int price = Price(ctx?.map, out int n);
            if (n <= 0) return;
            new DilemmaEffect_CarryOut { what = PlayCarry.Scrap, count = n, to = PlaySpot.Camp, steward = false }.Describe(ctx, sheet);
            sheet.Silver(price);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_WheelYard);
            int price = Price(ctx?.map, out int n);
            if (c == null || n <= 0) return;
            new DilemmaEffect_CarryOut { what = PlayCarry.Scrap, count = n, to = PlaySpot.Camp, steward = false }.Apply(ctx);
            Couriers.PaySilver(c.map, c.FireCell, price, c.faction, WheelStopsBy.Fixer(c));
        }
    }
}
