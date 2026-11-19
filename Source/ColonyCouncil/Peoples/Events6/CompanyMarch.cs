using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 58. A company on the march (the Ashen Banner) =====================

    /// <summary>A company's night or two at the edge: whom they march against, the soldiers who stayed, and how it turned.</summary>
    public class MarchState : CampWorkState
    {
        public Faction enemy;
        public int joinedSeen, drillDay = -1;
        public bool caughtRolled, caught, betRolled, brawled, enlisted;

        public override void ExposeData()
        {
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_Values.Look(ref joinedSeen, "joinedSeen");
            Scribe_Values.Look(ref drillDay, "drillDay", -1);
            Scribe_Values.Look(ref caughtRolled, "caughtRolled");
            Scribe_Values.Look(ref caught, "caught");
            Scribe_Values.Look(ref betRolled, "betRolled");
            Scribe_Values.Look(ref brawled, "brawled");
            Scribe_Values.Look(ref enlisted, "enlisted");
        }
    }

    /// <summary>
    /// A company on the march (item 58): eight to twelve soldiers of the Ashen Banner on their way to a contract against a people
    /// at war with the Banner camp a night or two at the colony's edge, their bedrolls round their fire, and drill at dawn. They
    /// buy food in quantity (at the colony's market, and twenty meals at the gate with the second answer). The sergeant drills
    /// the colony's fighters if asked (a lesson), and asks whether one of them would enlist with the company for a season (a
    /// caravan of one to the Banner's town, round 2's season away: home with pay and the drill ground's lessons; the roads are
    /// the game's own). A soldier may ask to stay (the camps' own joining), which the Banner holds against the colony. It can
    /// turn, once a stay each: the people they march against catches them here (a band against the camp, the camps' own; the
    /// colony may stand with them), or a soldier and a colonist at their fire come to blows over a bet (the camps' brawl in the
    /// shared harm budget, the game's own social fight).
    /// </summary>
    public static class CompanyMarch
    {
        public const string EventDef = "Rimfolk_Evt_CompanyMarch";
        public const string AwayKey = "enlist";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_CompanyMarch");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static MarchState StateOf(Camp c) => c?.workState as MarchState;

        /// <summary>The people they march against: at war with the Banner, with fighters; one at war with the colony too first.</summary>
        public static Faction Against(Faction banner)
        {
            if (banner == null) return null;
            var all = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f != banner && !f.IsPlayer && !f.defeated && !f.Hidden && f.def.humanlikeFaction
                                                                            && f.HostileTo(banner) && CallsToArms.Fields(f)).ToList();
            var both = all.Where(f => f.HostileTo(Faction.OfPlayer)).ToList();
            return (both.Count > 0 ? both : all).RandomElementWithFallback();
        }

        /// <summary>The camp made at the edge nearest the colony, the company marching in.</summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                int n = Mathf.Max(kind.people.min, ctx.count);
                for (int i = 0; i < n; i++)
                    people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, i == 0 ? "Rimfolk_AshenBanner_Sergeant" : i % 3 == 0 ? "Rimfolk_AshenBanner_Shot" : "Rimfolk_AshenBanner_Soldier"),
                        ctx.faction, new FloatRange(19f, 48f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 2);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var edge = LegendBeasts.EdgeToward(map, CampSite.ColonyCenter(map));
                var center = edge.IsValid ? CampLand.TryFindNear(map, edge, 4f, 18f, hot) : IntVec3.Invalid;
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot))
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
                c.workState = new MarchState { enemy = ctx.enemy };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the company on the march", e);
                return null;
            }
        }

        /// <summary>An hour's drill done at dawn.</summary>
        public static void Drilled(Pawn by)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            if (c != null && by == c.leader && Rand.Chance(0.6f)) c.SayMoment(by, "drill");
        }

        /// <summary>A soldier of theirs taken in by the colony (the camps' own joining): the Banner holds it against the colony; told once each.</summary>
        public static void Stayed(Camp c, MarchState st)
        {
            if (c.joined.Count <= st.joinedSeen) return;
            var names = c.joined.Skip(st.joinedSeen).Where(p => p != null).Select(p => p.LabelShort).ToList();
            st.joinedSeen = c.joined.Count;
            if (names.Count == 0 || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            WorldSituationWorker.Goodwill(c.faction, PeopleEvents6Math.StayedGoodwill * names.Count, GoodwillWhy.Deserter);
            c.Note(c.Text("CC_PEv6_MarchStayed", c.leader, null, names.Count, TextUtil.JoinAnd(names)), MessageTypeDefOf.NegativeEvent, Importance.Notable, c.leader, true);
        }

        /// <summary>
        /// Once a stay, by day after their first hours: the people they march against catches them here, at a chance, only while
        /// Rimfolk's world events may bring a fight: a band against the camp (the camps' own, the colony asked what to do).
        /// </summary>
        public static void Caught(Camp c, MarchState st)
        {
            st.caughtRolled = true;
            var enemy = st.enemy;
            if (enemy == null || enemy.defeated || !enemy.HostileTo(c.faction) || !IncidentWorker_Happening.BattlesAllowed(c.map)) return;
            if (!Rand.Chance(PeopleEvents6Math.CaughtChance)) return;
            if (CampRaids.Send(c, enemy, false, true) == null) return;
            st.caught = true;
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "caught");
        }

        /// <summary>
        /// Once a stay, at their fire in the evening with one of the colony there: a bet, and at the camps' brawl's rate (the
        /// shared harm budget) the two come to blows (the game's own social fight, a brawl their people won't count as an attack);
        /// told, the camp's unease up.
        /// </summary>
        public static void Bet(Camp c, MarchState st)
        {
            var fire = c.FireCell;
            var colonist = c.map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => CampBrawls.Fit(p, fire, 8f));
            if (colonist == null) return;
            var soldier = c.Here.Where(p => p.RaceProps.Humanlike && CampBrawls.Fit(p, fire, 8f) && Together.Can(p, colonist)).RandomElementWithFallback();
            if (soldier == null) return;
            st.betRolled = true;
            // together: the soldier and the colonist at the company's fire, within talking range of each other (Together.Can).
            c.SayMoment(soldier, "bet");
            Speech.Say(colonist, TextVariants.Vary("CC_PEv6_BetBack").Translate().Resolve());
            if (!CampBrawls.ColonySide || !Harm.Roll(HarmKinds.CampBrawl, PeopleEvents6Math.BetChance)) return;
            var bout = Outsiders.QuietBrawl(colonist, soldier);
            if (bout == null) return;
            if (!Harm.Fight(colonist, soldier, HarmKinds.CampBrawl))
            {
                Outsiders.ForgetBrawl(bout);
                return;
            }
            st.brawled = true;
            c.AddTension(10f);
            c.Note(c.Text("CC_PEv6_MarchBrawl", soldier, colonist), MessageTypeDefOf.NegativeEvent, Importance.Notable, soldier, true);
        }
    }

    /// <summary>The company at the edge: the dawn's drill, the soldiers who stay, the band that catches them, the bet at the fire.</summary>
    public class CampWorker_CompanyMarch : CampWorker
    {
        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var edge = LegendBeasts.EdgeToward(map, CampSite.ColonyCenter(map));
            if (!edge.IsValid) return usual;
            var c = CampLand.TryFindNear(map, edge, 4f, 18f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is MarchState)) c.workState = new MarchState { enemy = CompanyMarch.Against(c.faction) };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is MarchState st))
            {
                Started(c);
                st = CompanyMarch.StateOf(c);
            }
            if (!c.Settled) return;
            CompanyMarch.Stayed(c, st);
            if (!st.caughtRolled && c.DaysHere >= 0.4f && hour >= 9 && hour <= 17 && !MapFacts.ThreatOn(c.map)) CompanyMarch.Caught(c, st);
            if (!st.betRolled && hour >= 18 && hour <= 22) CompanyMarch.Bet(c, st);
        }

        /// <summary>At dawn they drill where they camp.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = CompanyMarch.StateOf(c);
            if (st == null || !p.RaceProps.Humanlike || !PeopleEvents6Math.DrillHour(GenLocalDate.HourOfDay(c.map))) return null;
            var spot = CellFinder.RandomClosewalkCellNear(c.FireCell, c.map, 5, x => x.Standable(c.map) && x.GetFirstBuilding(c.map) == null);
            return spot.IsValid ? PeopleEvents6.Make(PeopleEvents6.DrillJob, spot, PeopleEvents6Math.DrillTicks) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = CompanyMarch.StateOf(c);
            if (st == null || c.Hostile || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            if (c.tension < 50f && c.joined.Count == 0) WorldSituationWorker.Goodwill(c.faction, PeopleEvents6Math.MarchGoodwill, GoodwillWhy.CampWell);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = CompanyMarch.StateOf(c);
            if (st == null) return;
            if (st.caught) marks.Add("caught");
            if (st.brawled) marks.Add("brawl");
            if (st.enlisted) marks.Add("enlisted");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = CompanyMarch.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "enlist":
                    if (st.enlisted) return "CC_PEv6_MarchEnlisted".Translate().Resolve();
                    return LeagueGuard.Fighters(c.map).Count == 0 ? "CC_PEv6_NoneCanLeave".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>
    /// A sergeant of {FACTION} ({X}) at the gate: a company of {COUNT} on its way to a contract against {ENEMY} asks to camp a
    /// night or two at the edge of the colony's land; they would buy twenty meals for {DEAD} silver.
    /// </summary>
    public class HappeningWorker_CompanyMarch : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            return f != null && CompanyMarch.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && CompanyMarch.On(map) == null && CompanyMarch.Against(f) != null
                   && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            ctx.enemy = CompanyMarch.Against(ctx.faction);
            if (ctx.faction == null || ctx.enemy == null) return false;
            ctx.count = Rand.RangeInclusive(CompanyMarch.Kind?.people.min ?? 8, CompanyMarch.Kind?.people.max ?? 12);
            ctx.variant = PeopleEvents6Math.FoodPay(PeopleEvents6Math.FoodMeals * (ThingDefOf.MealSimple?.BaseMarketValue ?? 15f));
            ctx.dead = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Sergeant"), ctx.faction, ext.ages);
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
            if (ctx.enemy != null && ctx.enemy.HostileTo(Faction.OfPlayer)) tags.Add("ourfoe");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (CompanyMarch.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The company's camp made at the edge (CompanyMarch.Start); with <see cref="sell"/>, the twenty meals they buy carried out and paid for.</summary>
    public class DilemmaEffect_CompanyMarch : DilemmaEffect
    {
        public bool sell;

        public override bool ChanceEach => false;

        private static DilemmaEffect_CarryOut Carry() =>
            new DilemmaEffect_CarryOut { what = "meals", count = PeopleEvents6Math.FoodMeals, steward = false, label = "CC_PEv6_MealsSold".Translate().Resolve() };

        public override string Unavailable(DilemmaContext ctx) => sell ? Carry().Unavailable(ctx) : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            if (sell)
            {
                Carry().Describe(ctx, sheet);
                sheet.Silver(hc.variant);
            }
            sheet.Add(ImpactKind.People, 1, "CC_PEv6_Impact_March".Translate(hc.faction?.Name ?? "?", hc.count).Resolve(), "CC_PEv6_Impact_MarchBrief".Translate().Resolve(), "pev6march");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv6_Impact_MarchTurns".Translate(hc.enemy?.Name ?? "?").Resolve(), "CC_PEv6_Impact_MarchTurnsBrief".Translate().Resolve(), "pev6marchturns");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (sell)
            {
                Carry().Apply(ctx);
                Couriers.PaySilver(hc.map, PeoplesD.GateSpot(hc.map) ?? CampSite.ColonyCenter(hc.map), hc.variant, hc.faction, hc.stranger);
            }
            CompanyMarch.Start(hc);
            if (!sell) Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>One of the colony's fighters enlisted with the company for a season: they set out for the Banner's town as a caravan of one.</summary>
    public class DilemmaEffect_CompanyEnlist : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Pawn Who(DilemmaContext ctx)
        {
            var a = ctx?.a;
            return a != null && PeopleEvents2.MayLeave(a, ctx.map) && !a.WorkTagIsDisabled(WorkTags.Violent) ? a : LeagueGuard.BestFighter(LeagueGuard.Fighters(ctx?.map));
        }

        public override string Unavailable(DilemmaContext ctx) => Who(ctx) == null ? "CC_PEv6_NoneCanLeave".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var who = Who(ctx);
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_CompanyMarch);
            if (who == null || c?.faction == null) return;
            var town = OldSoldier.Town(c.faction, ctx.map);
            sheet.Play("CC_PEv6_Impact_Enlist".Translate(who.LabelShort, town?.Label ?? c.faction.Name, PeopleEvents6Math.EnlistDays).Resolve(), "CC_PEv6_Impact_EnlistBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv6_Impact_EnlistPay".Translate(who.LabelShort, PeopleEvents6Math.EnlistPay).Resolve(), "CC_PEv6_Impact_EnlistPayBrief".Translate().Resolve(), "pev6enlistpay");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_CompanyMarch);
            var st = CompanyMarch.StateOf(c);
            var who = Who(ctx);
            var town = c != null ? OldSoldier.Town(c.faction, ctx.map) : null;
            if (st == null || who == null || town == null) return;
            if (SeasonAway.Send(CompanyMarch.AwayKey, DefDatabase<IncidentDef>.GetNamedSilentFail(CompanyMarch.EventDef), who, null, c.faction, town.Tile, town.Label,
                    PeopleEvents6Math.EnlistDays, ctx.map) != null)
                st.enlisted = true;
        }
    }

    /// <summary>A season with the company: the letter as they join it, and their pay and the drill ground's lessons at home.</summary>
    public class EnlistAway : AwayKind
    {
        public override string Key => CompanyMarch.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv6_EnlistThere".Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            if (served)
            {
                if (p.Spawned)
                    foreach (var t in Couriers.Make(ThingDefOf.Silver, PeopleEvents6Math.EnlistPay))
                        GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
                try
                {
                    p.skills?.Learn(SkillDefOf.Melee, PeopleEvents6Math.EnlistXp, true);
                    p.skills?.Learn(SkillDefOf.Shooting, PeopleEvents6Math.EnlistXp, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: home from the season with the company", e);
                }
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.MarchGoodwill, GoodwillWhy.DutyDone);
            }
            Tell.Message(NewsKind.World, Importance.Notable, home,
                (served ? "CC_PEv6_EnlistHome" : "CC_PEv6_EnlistHomeEarly").Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION"),
                    PeopleEvents6Math.EnlistPay.Named("PAY")).Resolve(), p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }
}
