using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 74. A ration post (the Last Republic) =====================

    /// <summary>The ration post's week: the rations left, the hungry at its table, how many were fed, and how it turned.</summary>
    public class RationState : CampWorkState
    {
        public List<Pawn> hungry = new List<Pawn>();
        public Pawn denounced;
        /// <summary>The people of the one the guards turned away, for the words ({PEOPLE}).</summary>
        public string turnedPeople;
        public int rations, served, nextHungry = -1, denouncedAt = -1;
        public bool joinAsked, riotRolled, rioted, turnedRolled, turned;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref hungry, "hungry", LookMode.Reference);
            Scribe_References.Look(ref denounced, "denounced");
            Scribe_Values.Look(ref turnedPeople, "turnedPeople");
            Scribe_Values.Look(ref rations, "rations");
            Scribe_Values.Look(ref served, "served");
            Scribe_Values.Look(ref nextHungry, "nextHungry", -1);
            Scribe_Values.Look(ref denouncedAt, "denouncedAt", -1);
            Scribe_Values.Look(ref joinAsked, "joinAsked");
            Scribe_Values.Look(ref riotRolled, "riotRolled");
            Scribe_Values.Look(ref rioted, "rioted");
            Scribe_Values.Look(ref turnedRolled, "turnedRolled");
            Scribe_Values.Look(ref turned, "turned");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                hungry ??= new List<Pawn>();
                hungry.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// A ration post (item 74): in a famine in the region (a world event of hunger: famine, blight, locusts), a clerk and two guards of
    /// the Last Republic keep a ration post by the colony's road for a week, its rations stacked by their fire. Hungry travellers of the
    /// region's peoples walk in from the road now and then by day, take a ration at their fire and walk on. The post buys food from the
    /// colony at the Republic's fixed price, and a colony holding the Republic's papers may draw ration cards there; now and then one of
    /// the hungry asks the colony to take them in. It can turn, once a stay each: the rations run out with people still waiting and the
    /// queue comes to blows (the camp brawl's rate, the game's own social fight), or one of a people the Republic is at war with comes
    /// for a ration and the guards turn them away.
    /// </summary>
    public static class RationPost
    {
        public const string EventDef = "Rimfolk_Evt_RationPost";
        public const string JoinDef = "Rimfolk_Evt_RationJoin";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_RationPost");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static RationState StateOf(Camp c) => c?.workState as RationState;

        /// <summary>One of the hungry at the post asking to join (set only while their letter is being made).</summary>
        public static Pawn asking;

        /// <summary>A hunger in the region now (a world event of the hunger kind), with its key and its start.</summary>
        public static bool Famine(out string key, out int since, out string label)
        {
            key = null;
            label = null;
            since = -1;
            if (ColonyCouncilMod.Settings == null || !ColonyCouncilMod.Settings.worldEvents) return false;
            var s = WorldSituations.Instance?.Running.FirstOrDefault(x => x?.def != null && x.def.kind == "hunger");
            if (s == null) return false;
            key = s.def.defName + ":" + s.startTick;
            since = s.startTick;
            label = s.def.label;
            return true;
        }

        /// <summary>
        /// The events' hour: a famine in the region is rolled for once, within its first days, at a chance (the storytellers' lean on
        /// visitors): a clerk of a Republic at peace comes to ask for a ration post by the colony's road.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents8Book.Instance;
            var home = HappeningTracker.HomeMap();
            if (b == null || home == null || Kind == null || !Famine(out string key, out int since, out _)) return;
            if (b.FamineRolled(key)) return;
            if (now - since > PeopleEvents8Math.RationWithinDays * GenDate.TicksPerDay)
            {
                b.NoteFamine(key);
                return;
            }
            if (!Tell.CanAsk || MapFacts.ThreatOn(home) || !CampTracker.RoomFor(home) || On(home) != null) return;
            b.NoteFamine(key);
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (f == null || !Rand.Chance(PeopleEvents8Math.RationChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, home, f)) b.lastRation = now;
        }

        /// <summary>The post made by the colony's road, the clerk and the guards walking in; <paramref name="extra"/> meals the colony sent with them.</summary>
        public static Camp Start(HappeningContext ctx, int extra)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, new FloatRange(28f, 60f)));
                for (int i = 1; i < PeopleEvents8Math.PostPeople; i++) people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, new FloatRange(20f, 45f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents8Math.PostDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var center = Picket.Site(map, hot);
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
                c.workState = new RationState { rations = PeopleEvents8Math.PostRations + Mathf.Max(0, extra), nextHungry = Now + 6 * GenDate.TicksPerHour };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the ration post", e);
                return null;
            }
        }

        private static Pawn Guard(Camp c) => c.Here.Where(p => p != c.leader && p.RaceProps.Humanlike).RandomElementWithFallback() ?? c.leader;

        /// <summary>Their hour: the hungry sent to the table, more of them walking in, and the one the guards turn away.</summary>
        public static void Hour(Camp c, RationState st, int hour)
        {
            var map = c.map;
            st.hungry.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != map || p.Faction == Faction.OfPlayer || p.Downed);
            foreach (var p in st.hungry.ToList())
            {
                if (p.CurJobDef == PeopleEvents8.RationJob || p.InMentalState) continue;
                var cell = CellFinder.StandableCellNear(c.FireCell, map, 3f);
                if (cell.IsValid) PeopleEvents8.Send(p, PeopleEvents8.RationJob, cell, PeopleEvents8Math.RationTicks);
            }
            if (st.denounced != null)
            {
                var d = st.denounced;
                if (d.Dead || !d.Spawned || d.Map != map) st.denounced = null;
                else if (d.Position.InHorDistOf(c.FireCell, 14f) || Now - st.denouncedAt > 2 * GenDate.TicksPerHour) TurnAway(c, st);
            }
            if (PeopleEvents8Math.PostHour(hour) && Now >= st.nextHungry && st.hungry.Count < PeopleEvents8Math.HungryMax && !MapFacts.ThreatOn(map))
            {
                st.nextHungry = Now + Rand.RangeInclusive(PeopleEvents8Math.HungryGapMin, PeopleEvents8Math.HungryGapMax) * GenDate.TicksPerHour;
                Hungry(c, st);
            }
        }

        /// <summary>
        /// A hungry traveller (or two of one people) walks in from the road for a ration: of a people at peace with the colony and the
        /// Republic; once a stay, at a chance, one of a people the Republic is at war with instead, whom the guards turn away.
        /// </summary>
        public static void Hungry(Camp c, RationState st)
        {
            var map = c.map;
            Faction f;
            bool denounced = false;
            if (!st.turnedRolled && st.denounced == null && Rand.Chance(PeopleEvents8Math.DenouncedChance)
                && (f = Arrivals.Friendly(x => x != c.faction && Arrivals.Usable(x) && x.HostileTo(c.faction))) != null)
            {
                st.turnedRolled = true;
                denounced = true;
            }
            else f = Arrivals.Friendly(x => x != c.faction && Arrivals.Usable(x) && !x.HostileTo(c.faction));
            if (f == null) return;
            try
            {
                int n = !denounced && Rand.Chance(0.3f) ? 2 : 1;
                var list = new List<Pawn>();
                for (int i = 0; i < n; i++)
                {
                    var p = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(16f, 65f));
                    if (p == null) continue;
                    if (p.needs?.food != null) p.needs.food.CurLevelPercentage = Rand.Range(0.05f, 0.25f);
                    list.Add(p);
                }
                if (list.Count == 0) return;
                if (Arrivals.Visit(list, map, f, c.FireCell, Rand.Range(4f, 6f)) == null)
                {
                    foreach (var p in list) Arrivals.Discard(p);
                    return;
                }
                if (denounced)
                {
                    st.denounced = list[0];
                    st.denouncedAt = Now;
                    return;
                }
                st.hungry.AddRange(list);
                c.Note(c.Text(list.Count > 1 ? "CC_PEv8_RationHungryTwo" : "CC_PEv8_RationHungry", list[0], null, list.Count, f.Name), MessageTypeDefOf.NeutralEvent, Importance.Flavor, list[0]);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the hungry at the post", e);
            }
        }

        private static bool Ask(Pawn p, Map map)
        {
            asking = p;
            try
            {
                return PeopleEvents2.Fire(JoinDef, map, p.Faction);
            }
            finally
            {
                asking = null;
            }
        }

        /// <summary>A ration taken at their fire: one from the stack and the hungry one walks on; with none left, they go hungry, and the queue may come to blows.</summary>
        public static void Taken(Pawn p)
        {
            var c = CampTracker.VisitorsAt(p?.Map).FirstOrDefault(x => x.kind == Kind && StateOf(x)?.hungry.Contains(p) == true);
            var st = StateOf(c);
            if (st == null) return;
            st.hungry.Remove(p);
            if (st.rations > 0)
            {
                st.rations--;
                st.served++;
                if (p.needs?.food != null) p.needs.food.CurLevelPercentage = Mathf.Max(p.needs.food.CurLevelPercentage, 0.8f);
                if (Rand.Chance(0.3f)) c.SayMoment(Guard(c), "ration");
            }
            else
            {
                c.SayMoment(c.leader, "short");
                if (!st.riotRolled) Riot(c, st, p);
            }
            // Once a stay, one of the hungry fed at the post asks the colony to take them in, and waits at the post for the answer.
            if (st.served > 0 && !st.joinAsked && p.Spawned && !p.InMentalState && Rand.Chance(PeopleEvents8Math.HungryJoinChance) && Tell.CanAsk && Ask(p, p.Map))
            {
                st.joinAsked = true;
                return;
            }
            Arrivals.SendAway(new List<Pawn> { p });
        }

        /// <summary>
        /// Once a stay, the rations run out with someone waiting: words at the fire, and at the camp brawl's rate (the shared harm budget)
        /// the one turned away and a guard come to blows (the game's own social fight); told.
        /// </summary>
        public static void Riot(Camp c, RationState st, Pawn waiting)
        {
            st.riotRolled = true;
            var guard = Guard(c);
            if (waiting == null || guard == null || !waiting.Spawned) return;
            bool fight = Harm.On && Harm.SystemOn(HarmKinds.CampBrawl.system) && CampBrawls.Fit(waiting, c.FireCell, 14f) && CampBrawls.Fit(guard, c.FireCell, 14f)
                         && Harm.Roll(HarmKinds.CampBrawl, HarmTwoMath.CampBrawlChance(false, 1f));
            if (!fight)
            {
                c.Note(c.Text("CC_PEv8_RationShort", c.leader, null, 0, waiting.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, waiting);
                return;
            }
            try
            {
                waiting.interactions?.StartSocialFight(guard);
                st.rioted = true;
                c.Note(c.Text("CC_PEv8_RationRiot", guard, null, 0, waiting.LabelShort), MessageTypeDefOf.NegativeEvent, Importance.Notable, waiting, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the queue at the post", e);
            }
        }

        /// <summary>The one of a people the Republic is at war with turned away at the fire by a guard: told, and they walk on hungry.</summary>
        public static void TurnAway(Camp c, RationState st)
        {
            var d = st.denounced;
            st.denounced = null;
            if (d == null || !d.Spawned) return;
            st.turned = true;
            st.turnedPeople = d.Faction?.Name;
            var guard = Guard(c);
            if (guard != null) c.SayMoment(guard, "turned");
            c.Note(c.Text("CC_PEv8_RationTurned", guard, null, 0, d.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, d);
            Arrivals.SendAway(new List<Pawn> { d });
        }

        /// <summary>The colony sold the post food (carried out by the ask's own carry-out): the meals added to the rations, the Republic's price paid.</summary>
        public static void Sold(Camp c, int meals)
        {
            var st = StateOf(c);
            if (st == null || c.faction == null) return;
            st.rations += Mathf.Max(0, meals);
            var at = c.FireCell.IsValid ? c.FireCell : PeoplesD.GateSpot(c.map) ?? IntVec3.Invalid;
            Couriers.PaySilver(c.map, at, PeopleEvents8Math.RationPay(meals), c.faction, c.leader);
        }

        /// <summary>The ration cards honoured: a ration for each of the colony's people from the post's stack, set down by their fire for the colony.</summary>
        public static int Cards(Camp c)
        {
            var st = StateOf(c);
            var map = c?.map;
            if (st == null || map == null) return 0;
            int n = Mathf.Min(st.rations, PeopleEvents8Math.Cards(IncidentWorker_Happening.Colonists(map).Count));
            if (n <= 0) return 0;
            st.rations -= n;
            var pack = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
            pack.stackCount = n;
            var at = CellFinder.StandableCellNear(c.FireCell, map, 4f);
            if (!at.IsValid || !GenPlace.TryPlaceThing(pack, at, map, ThingPlaceMode.Near)) return 0;
            return n;
        }

        public static void Leave(Camp c, RationState st)
        {
            foreach (var p in st.hungry.Where(p => p != null && p.Spawned).ToList()) Arrivals.SendAway(new List<Pawn> { p });
            if (!c.Hostile && c.tension < 50f) PeopleEvents8.Goodwill(c.faction, PeopleEvents8Math.PostGoodwill, GoodwillWhy.CampWell);
        }

        /// <summary>Dev mode: the clerk of a ration post at the gate now (whatever the famine).</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (map == null || f == null) return "no Last Republic at peace.";
            if (On(map) != null || !CampTracker.RoomFor(map)) return "no room for a camp here.";
            if (Famine(out string key, out _, out _)) PeopleEvents8Book.Instance?.NoteFamine(key);
            else return "no famine in the region (start one with the world events' tools first: famine, blight or locusts).";
            return PeopleEvents2.Fire(EventDef, map, f) ? "a clerk of " + f.Name + " comes to ask for a ration post." : "the clerk could not come now (a question waits).";
        }
    }

    /// <summary>The ration post by the road: the hungry fed at its fire, the queue, the one turned away, the rations counted in its farewell.</summary>
    public class CampWorker_RationPost : CampWorker
    {
        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var c = Picket.Site(map, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is RationState)) c.workState = new RationState { rations = PeopleEvents8Math.PostRations, nextHungry = Find.TickManager.TicksGame + 6 * GenDate.TicksPerHour };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is RationState st))
            {
                Started(c);
                st = RationPost.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            RationPost.Hour(c, st, hour);
        }

        public override void Leaving(Camp c)
        {
            var st = RationPost.StateOf(c);
            if (st != null) RationPost.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = RationPost.StateOf(c);
            if (st == null) return;
            if (st.served >= 4) marks.Add("fed");
            if (st.rioted) marks.Add("riot");
            if (st.turned) marks.Add("turned");
        }

        public override string Fill(Camp c, string text)
        {
            if (text == null) return null;
            var st = RationPost.StateOf(c);
            if (text.Contains("{FED}")) text = text.Replace("{FED}", (st?.served ?? 0).ToString());
            if (text.Contains("{PEOPLE}")) text = text.Replace("{PEOPLE}", st?.turnedPeople ?? "?");
            return text;
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = RationPost.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "food":
                    return null;
                case "cards":
                    if (!Papers.Holds(c.faction)) return "CC_PEv8_NoPapers".Translate(c.faction?.Name ?? "?").Resolve();
                    return st.rations <= 0 ? "CC_PEv8_PostEmpty".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A clerk of {FACTION} ({X}) at the gate in a famine ({TALE}): the Republic would keep a ration post by the colony's road for a week.</summary>
    public class HappeningWorker_RationPost : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            RationPost.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && RationPost.On(map) == null && PeoplesD.Friendly(PeoplesMath.Citizens) != null
            && RationPost.Famine(out _, out _, out _) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (ctx.faction == null || !RationPost.Famine(out _, out _, out string label)) return false;
            ctx.tale = label;
            ctx.count = PeopleEvents8Math.RationPrice;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, ext.ages);
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
            if (Papers.Holds(ctx.faction)) tags.Add("papers");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (RationPost.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The ration post made by the road (RationPost.Start); <see cref="sack"/> meals of the colony's went with them.</summary>
    public class DilemmaEffect_RationPost : DilemmaEffect
    {
        public int sack;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv8_Impact_Post".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv8_Impact_PostBrief".Translate().Resolve(), "pev8post");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv8_Impact_PostTurns".Translate().Resolve(), "CC_PEv8_Impact_PostTurnsBrief".Translate().Resolve(), "pev8postturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.PostGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            RationPost.Start(hc, sack);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The post buys the colony's meals at the Republic's price (carried out to them by the ask's own carry-out).</summary>
    public class DilemmaEffect_RationSold : DilemmaEffect
    {
        public int meals = PeopleEvents8Math.RationBuy;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) => sheet.Silver(PeopleEvents8Math.RationPay(meals));

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RationPost);
            if (c != null) RationPost.Sold(c, meals);
        }
    }

    /// <summary>The colony's ration cards honoured at the post: rations set down by their fire for the colony.</summary>
    public class DilemmaEffect_RationCards : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RationPost);
            var st = RationPost.StateOf(c);
            if (st == null || ctx?.map == null) return;
            int n = Mathf.Min(st.rations, PeopleEvents8Math.Cards(IncidentWorker_Happening.Colonists(ctx.map).Count));
            if (n > 0) sheet.Goods(1, ThingDefOf.MealSurvivalPack, n);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_RationPost);
            if (c == null) return;
            int n = RationPost.Cards(c);
            if (n > 0) c.Note(c.Text("CC_PEv8_CardsDrawn", c.leader, ctx.a, n), MessageTypeDefOf.PositiveEvent, Importance.Notable);
        }
    }

    /// <summary>One of the hungry at the ration post ({X}, of {FACTION}) asks the colony to take them in.</summary>
    public class HappeningWorker_RationJoin : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            RationPost.asking != null && RationPost.asking.Spawned && RationPost.asking.Map == map && RationPost.On(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var p = RationPost.asking;
            var c = RationPost.On(ctx.map);
            if (p == null || c == null) return false;
            ctx.stranger = p;
            ctx.faction = p.Faction;
            ctx.placeName = c.place;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, RationPost.On(ctx.map)?.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var x = ctx.stranger;
            return option <= 1 && (x == null || x.Dead || !x.Spawned || x.Map != ctx.map) ? "CC_Stranger_Gone".Translate(x?.LabelShort ?? "?").Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "taken" : option == 1 ? "meal" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
