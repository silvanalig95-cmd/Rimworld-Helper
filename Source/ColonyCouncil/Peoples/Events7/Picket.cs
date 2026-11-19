using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 70. The picket (the Deepwell Union) =====================

    /// <summary>The picket by the road: the strike it keeps, how it ended, and how it turned.</summary>
    public class PicketState : CampWorkState
    {
        public int strikeFrom = -1, strikeUntil = -1, singDay = -1, fedAt = -1;
        public string ended;
        public bool breakersRolled, breakers, fed;
        public Faction enemy;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref strikeFrom, "strikeFrom", -1);
            Scribe_Values.Look(ref strikeUntil, "strikeUntil", -1);
            Scribe_Values.Look(ref singDay, "singDay", -1);
            Scribe_Values.Look(ref fedAt, "fedAt", -1);
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref breakersRolled, "breakersRolled");
            Scribe_Values.Look(ref breakers, "breakers");
            Scribe_Values.Look(ref fed, "fed");
            Scribe_References.Look(ref enemy, "enemy");
        }
    }

    /// <summary>
    /// The picket (item 70): when the Deepwell Union calls a strike by vote (round F's strike, Peoples/PeoplesF.cs), strikers may ask to
    /// keep a picket camp by the colony's road for as long as it lasts: a fire, and songs in the evening. Its shift-captain is the one
    /// who settles it with the colony: they ask for food for the picket line (carried out to the camp, which settles the strike, as at
    /// round F's gate; while the picket stands that gate visit doesn't come), and invite the colony's best miner to their fire of an
    /// evening. It can turn, once a stay: strikebreakers of a people at war with the union come against the camp (a band against the camp,
    /// the camps' own, the colony asked what to do), or the strike ends by the union's own vote when its days are out and they march home
    /// singing.
    /// </summary>
    public static class Picket
    {
        public const string EventDef = "Rimfolk_Evt_Picket";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_Picket");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static PicketState StateOf(Camp c) => c?.workState as PicketState;

        /// <summary>A picket of this union camps at a home now (round F's gate visit for the strike waits on it).</summary>
        public static bool Here(Faction f) => f != null && Homes.All.Any(m => On(m)?.faction == f);

        /// <summary>The strikebreakers: a people at war with the union that fights, one at war with the colony too first.</summary>
        public static Faction Breakers(Faction union)
        {
            if (union == null) return null;
            var all = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f != union && !f.IsPlayer && !f.defeated && !f.Hidden && f.def.humanlikeFaction
                                                                            && f.HostileTo(union) && CallsToArms.Fields(f)).ToList();
            var both = all.Where(f => f.HostileTo(Faction.OfPlayer)).ToList();
            return (both.Count > 0 ? both : all).RandomElementWithFallback();
        }

        /// <summary>
        /// The events' hour: a strike called by a union at peace with the colony is rolled for once, within its first three days, at a chance
        /// (the storytellers' lean on visitors): its strikers come to ask for a picket at the home.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents7Book.Instance;
            var home = HappeningTracker.HomeMap();
            if (b == null || home == null || Kind == null) return;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && PeoplesD.Theirs(f, PeoplesMath.Drillers)).ToList())
            {
                var strike = UnionStrike.Of(f);
                if (strike == null || b.PicketRolled(f, strike.due) || now - strike.due > 3 * GenDate.TicksPerDay) continue;
                if (!Tell.CanAsk || MapFacts.ThreatOn(home) || !CampTracker.RoomFor(home) || On(home) != null) continue;
                b.NotePicket(f, strike.due);
                if (!Rand.Chance(PeopleEvents7Math.PicketChance * StoryDirection.Factor(StoryWaysMath.Visitors))) continue;
                if (PeopleEvents2.Fire(EventDef, home, f)) b.lastPicket = now;
            }
        }

        /// <summary>Beside the colony's road, where it comes in nearest the colony; by the edge toward it where no road comes.</summary>
        public static IntVec3 Site(Map map, bool hot)
        {
            var mid = CampSite.ColonyCenter(map);
            var edges = map.roadInfo?.roadEdgeTiles?.Where(c => c.InBounds(map)).ToList() ?? new List<IntVec3>();
            var edge = edges.Count > 0 ? edges.OrderBy(c => mid.IsValid ? c.DistanceToSquared(mid) : 0).First() : LegendBeasts.EdgeToward(map, mid);
            if (!edge.IsValid) return IntVec3.Invalid;
            var roads = mid.IsValid ? GenRadial.RadialCellsAround(edge, 25f, true).Where(c => c.InBounds(map) && c.GetTerrain(map).IsRoad && c.DistanceTo(edge) >= 8f).ToList() : new List<IntVec3>();
            var at = roads.Count > 0 ? roads.OrderBy(c => c.DistanceToSquared(mid)).First() : edge;
            return CampLand.TryFindNear(map, at, 3f, 12f, hot);
        }

        /// <summary>The camp made by the road, the strikers walking in.</summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            var strike = UnionStrike.Of(ctx?.faction);
            if (map == null || tracker == null || kind == null || ctx.faction == null || strike == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                int n = kind.people.RandomInRange;
                for (int i = 0; i < n; i++)
                    people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, i == 0 ? "Rimfolk_Deepwell_Captain" : i % 3 == 0 ? "Rimfolk_Deepwell_ShotFirer" : "Rimfolk_Deepwell_Miner"),
                        ctx.faction, new FloatRange(18f, 58f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var center = Site(map, hot);
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
                c.workState = new PicketState { strikeFrom = strike.due, strikeUntil = strike.until, enemy = Breakers(ctx.faction) };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the picket", e);
                return null;
            }
        }

        /// <summary>
        /// Their hour: the strike over, they go (settled: as the colony fed the line; by vote: its days run out, marching home singing);
        /// the strikebreakers once a stay.
        /// </summary>
        public static void Hour(Camp c, PicketState st, int hour)
        {
            // Fed by the colony: settled once the meals have had time to reach the line.
            if (st.fed && st.fedAt >= 0 && Now - st.fedAt >= 3 * GenDate.TicksPerHour && UnionStrike.Striking(c.faction))
            {
                st.fedAt = -1;
                UnionStrike.Settle(c.faction, c.map, null);
            }
            if (!UnionStrike.Striking(c.faction))
            {
                st.ended = Now > st.strikeUntil ? "vote" : "settled";
                if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, st.ended);
                // Their own farewell, by how it ended (the camps' "time" ending tells the kind's farewell lines by their marks).
                c.Leave("time");
                return;
            }
            if (!st.breakersRolled && c.DaysHere >= 1f && hour >= 9 && hour <= 17 && !MapFacts.ThreatOn(c.map)) Breakers(c, st);
        }

        /// <summary>Once a stay, by day after their first: strikebreakers of a people at war with the union come against the camp, only while Rimfolk's world events may bring a fight.</summary>
        public static void Breakers(Camp c, PicketState st)
        {
            st.breakersRolled = true;
            var enemy = st.enemy != null && !st.enemy.defeated && st.enemy.HostileTo(c.faction) ? st.enemy : Breakers(c.faction);
            if (enemy == null || !IncidentWorker_Happening.BattlesAllowed(c.map) || !Rand.Chance(PeopleEvents7Math.BreakersChance)) return;
            if (CampRaids.Send(c, enemy, false, true) == null) return;
            st.breakers = true;
            st.enemy = enemy;
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "breakers");
        }

        /// <summary>An evening's singing at their fire done.</summary>
        public static void Sang(Pawn by)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null || st.singDay == GenDate.DaysPassed) return;
            st.singDay = GenDate.DaysPassed;
            c.SayMoment(by, "song");
        }

        /// <summary>
        /// The colony fed the picket line (their ask): the strike settled with the colony's help, as at round F's gate, once the meals
        /// have had a few hours to reach the line; the one who went down to them remembers it.
        /// </summary>
        public static void Fed(Camp c, Pawn by)
        {
            var st = StateOf(c);
            if (st == null || c.faction == null || st.fed) return;
            st.fed = true;
            st.fedAt = Now;
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Evt_StrikeSettled");
            if (thought != null && by != null && by.Faction != null && by.Faction.IsPlayer) CouncilGameComponent.GiveThought(by, thought);
        }

        /// <summary>Going: the union's thanks when they leave on good terms.</summary>
        public static void Leave(Camp c, PicketState st)
        {
            if (!c.Hostile && c.tension < 50f) PeopleEvents7.Goodwill(c.faction, PeopleEvents7Math.PicketGoodwill, GoodwillWhy.CampWell);
        }

        /// <summary>Dev mode: a strike called for the first union at peace (if none runs), and its strikers at the gate now.</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            var b = PeopleEvents7Book.Instance;
            if (map == null || f == null || b == null) return "no Deepwell Union at peace.";
            if (UnionStrike.Of(f) == null) UnionStrike.Call(f, map, Now);
            var strike = UnionStrike.Of(f);
            if (strike == null) return "no strike could be called.";
            b.NotePicket(f, strike.due);
            return PeopleEvents2.Fire(EventDef, map, f) ? "the strikers of " + f.Name + " come to the gate." : "they could not come now (no room for a camp, or a question waits).";
        }
    }

    /// <summary>The picket by the road: gone when the strike ends, the strikebreakers, songs at night.</summary>
    public class CampWorker_Picket : CampWorker
    {
        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var c = Picket.Site(map, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is PicketState))
            {
                var strike = UnionStrike.Of(c.faction);
                c.workState = new PicketState { strikeFrom = strike?.due ?? -1, strikeUntil = strike?.until ?? -1, enemy = Picket.Breakers(c.faction) };
            }
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is PicketState st))
            {
                Started(c);
                st = Picket.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            Picket.Hour(c, st, hour);
        }

        /// <summary>In the evening they sing by their fire.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = Picket.StateOf(c);
            if (st == null || !PeopleEvents7Math.Sing(hour) || !p.RaceProps.Humanlike) return null;
            var cell = CellFinder.StandableCellNear(c.FireCell, c.map, 3f);
            return cell.IsValid ? PeopleEvents7.Make(PeopleEvents7.SingJob, cell, GenDate.TicksPerHour) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = Picket.StateOf(c);
            if (st != null) Picket.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = Picket.StateOf(c);
            if (st == null) return;
            if (st.ended == "settled") marks.Add("settled");
            if (st.ended == "vote") marks.Add("vote");
            if (st.breakers) marks.Add("breakers");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = Picket.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "food":
                    return st.fed || !UnionStrike.Striking(c.faction) ? "CC_PEv7_PicketSettled".Translate().Resolve() : null;
                case "evening":
                    return PeoplesD.Best(c.map, SkillDefOf.Mining) == null ? "CC_PEv7_PicketNoMiner".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A shift-captain of {FACTION} on strike ({X}) at the gate: strikers would keep a picket by the colony's road while the strike lasts.</summary>
    public class HappeningWorker_Picket : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Picket.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && Picket.On(map) == null && Striker() != null && Colonists(map).Count > 0;

        private static Faction Striker() => Arrivals.Friendly(f => PeoplesD.Theirs(f, PeoplesMath.Drillers) && UnionStrike.Of(f) != null);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Striker();
            if (ctx.faction == null) return false;
            var town = WorldSituationWorker.NearestTown(ctx.faction, ctx.map);
            ctx.placeName = town?.Label ?? ctx.faction.Name;
            ctx.count = PeopleEvents7Math.PicketMeals;
            ctx.variant = 30;
            ctx.tale = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Captain"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (Picket.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The picket made by the road (Picket.Start).</summary>
    public class DilemmaEffect_Picket : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv7_Impact_Picket".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv7_Impact_PicketBrief".Translate().Resolve(), "pev7picket");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv7_Impact_PicketTurns".Translate().Resolve(), "CC_PEv7_Impact_PicketTurnsBrief".Translate().Resolve(), "pev7picketturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.PicketGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            Picket.Start(hc);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>Food carried out to the picket line (the ask's own carry-out): the strike settled with the colony's help.</summary>
    public class DilemmaEffect_PicketFed : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_Picket);
            if (c?.faction == null) return;
            sheet.Play("CC_PeoplesF_Impact_Settle".Translate(c.faction.Name).Resolve(), "CC_PeoplesF_Impact_SettleBrief".Translate().Resolve());
            if (c.faction.HasGoodwill && !c.faction.IsPlayer) sheet.Goodwill(c.faction, PeoplesFMath.SettledGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_Picket);
            if (c != null) Picket.Fed(c, ctx?.a);
        }
    }
}
