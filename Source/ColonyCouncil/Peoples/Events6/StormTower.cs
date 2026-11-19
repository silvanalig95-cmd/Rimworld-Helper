using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 50. The storm tower (the Stormglass Conclave) =====================

    /// <summary>The storm tower's week: its mast and rods, the storms written down, who sat the night watch, and how it turned.</summary>
    public class TowerState : CampWorkState
    {
        public Thing mast;
        public List<Thing> rods = new List<Thing>();
        public Pawn student;
        public int storms, lastStorm = -1;
        public bool raised, inStorm, strikeRolled, struck, chillRolled, chilled, meals;

        public override void ExposeData()
        {
            Scribe_References.Look(ref mast, "mast");
            Scribe_Collections.Look(ref rods, "rods", LookMode.Reference);
            Scribe_References.Look(ref student, "student");
            Scribe_Values.Look(ref storms, "storms");
            Scribe_Values.Look(ref lastStorm, "lastStorm", -1);
            Scribe_Values.Look(ref raised, "raised");
            Scribe_Values.Look(ref inStorm, "inStorm");
            Scribe_Values.Look(ref strikeRolled, "strikeRolled");
            Scribe_Values.Look(ref struck, "struck");
            Scribe_Values.Look(ref chillRolled, "chillRolled");
            Scribe_Values.Look(ref chilled, "chilled");
            Scribe_Values.Look(ref meals, "meals");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rods ??= new List<Thing>();
                rods.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>
    /// The storm tower (item 50): in spring and summer, two readers of the Conclave and an apprentice camp a week on the colony's
    /// high ground (the relay mast's own choice of it) with a glass mast and two of their rods; lightning that falls near them
    /// strikes the rods without fire (the rod's own way, Peoples/PeoplesEPatches.cs), and they write every storm down. They ask
    /// the colony's best at the fields to sit a night watch with them (a lesson; and, taught to read the glass, a season of
    /// better forecasts). When they go they leave a sealed vial and the season's forecast (the Conclave's watch, free for a
    /// season). It can turn, once a stay each: a strike in a storm lands past the rods and starts the game's own fire (the
    /// camps' embers' kind in the shared harm budget; they beat it out), or the apprentice takes a chill out on the high ground
    /// at night (the illness budget; the colony's doctor may see to them).
    /// </summary>
    public static class StormTower
    {
        public const string EventDef = "Rimfolk_Evt_StormTower";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_StormTower");

        public static ThingDef MastDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_StormglassMast");

        public static ThingDef VialDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_StormVial");

        private static int Now => Find.TickManager.TicksGame;

        public static void Forget()
        {
        }

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static TowerState StateOf(Camp c) => c?.workState as TowerState;

        /// <summary>The land has thunderstorms of its own (they come for the storms).</summary>
        public static bool Stormy(Map map) =>
            map?.Biome?.baseWeatherCommonalities?.Any(r => r?.weather != null && r.commonality > 0f && (WeatherDynamics.KindOf(r.weather) == WeatherKind.Storm
                                                                                                     || WeatherDynamics.KindOf(r.weather) == WeatherKind.DryStorm)) ?? false;

        private static bool StormNow(Map map)
        {
            var k = WeatherDynamics.KindOf(map.weatherManager.curWeather);
            return k == WeatherKind.Storm || k == WeatherKind.DryStorm;
        }

        /// <summary>A storm over the camp's map this hour.</summary>
        public static bool StormNowFor(Camp c) => c?.map != null && StormNow(c.map);

        /// <summary>The camp made on the high ground, two readers and an apprentice walking in; with <paramref name="meals"/>, the week's meals sent.</summary>
        public static Camp Start(HappeningContext ctx, bool meals)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                for (int i = 0; i < PeopleEvents6Math.TowerReaders; i++)
                    people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Reader"), ctx.faction, new FloatRange(28f, 60f)));
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Apprentice"), ctx.faction, new FloatRange(16f, 24f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents6Math.TowerDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var high = PeopleEvents6.HighGround(map, out _);
                var center = high.IsValid ? CampLand.TryFindNear(map, high, 3f, 14f, hot) : IntVec3.Invalid;
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
                c.workState = new TowerState { meals = meals };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the storm tower", e);
                return null;
            }
        }

        /// <summary>Their mast and two rods set up by the fire once they have settled; told.</summary>
        public static void Raise(Camp c, TowerState st)
        {
            st.raised = true;
            var map = c.map;
            var cells = PeopleEvents6.RodCells(map, c.FireCell + new IntVec3(0, 0, 3), 3);
            if (cells.Count == 0) return;
            try
            {
                if (MastDef != null)
                {
                    var mast = ThingMaker.MakeThing(MastDef);
                    mast.SetFaction(c.faction);
                    GenSpawn.Spawn(mast, cells[0], map);
                    st.mast = mast;
                    FleckMaker.ThrowMicroSparks(cells[0].ToVector3Shifted(), map);
                }
                foreach (var cell in cells.Skip(1))
                {
                    var rod = PeopleEvents6.Rod(map, cell, c.faction);
                    if (rod != null) st.rods.Add(rod);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the tower's mast", e);
            }
            var reader = c.leader != null && c.leader.Spawned ? c.leader : c.Here.FirstOrDefault();
            if (reader != null) c.SayMoment(reader, "mast");
            c.Note(c.Text("CC_PEv6_TowerRaised", reader), MessageTypeDefOf.NeutralEvent, Importance.Notable, st.mast, true);
        }

        /// <summary>A storm on the land: counted once, a reader writes it down.</summary>
        public static void Storm(Camp c, TowerState st)
        {
            bool now = StormNow(c.map);
            if (now)
            {
                st.lastStorm = Now;
                if (st.inStorm) return;
                st.inStorm = true;
                st.storms++;
                var reader = c.Here.Where(p => p.RaceProps.Humanlike).RandomElementWithFallback();
                if (reader != null) c.SayMoment(reader, "storm");
                c.Note(c.Text("CC_PEv6_TowerStorm", reader, null, st.storms), MessageTypeDefOf.NeutralEvent, Importance.Flavor, st.mast, false);
                return;
            }
            if (st.inStorm && Now - st.lastStorm > PeopleEvents6Math.StormGapHours * GenDate.TicksPerHour) st.inStorm = false;
        }

        /// <summary>
        /// Once a stay, in a storm: a strike lands past the rods (the game's own bolt, at open ground with something to burn, clear of
        /// anyone, out of the colony's home), the camps' embers' kind in the shared harm budget; told, and they beat the fire out.
        /// </summary>
        public static void Strike(Camp c, TowerState st)
        {
            st.strikeRolled = true;
            var map = c.map;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, PeopleEvents6Math.StrikeChance)) return;
            var at = c.center;
            var people = map.mapPawns.AllPawnsSpawned;
            IntVec3 cell = IntVec3.Invalid;
            foreach (var x in GenRadial.RadialCellsAround(at, PeopleEvents6Math.StrikeMax, false).InRandomOrder())
            {
                if (!x.InBounds(map) || x.Fogged(map) || x.Roofed(map) || x.DistanceTo(at) < PeopleEvents6Math.StrikeMin || (map.areaManager.Home?[x] ?? false)) continue;
                if (GlassRod.Near(map, x) != null || x.GetPlant(map) == null || x.GetFirstBuilding(map) != null) continue;
                if (people.Any(p => p.Position.InHorDistOf(x, PeopleEvents6Math.StrikeClear))) continue;
                cell = x;
                break;
            }
            if (!cell.IsValid) return;
            try
            {
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, cell));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the stray strike", e);
                return;
            }
            st.struck = true;
            HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
            var reader = c.Here.Where(p => p.RaceProps.Humanlike).RandomElementWithFallback();
            if (reader != null) c.SayMoment(reader, "strike");
            c.Note(c.Text("CC_PEv6_TowerStrike", reader), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
            PeopleEvents6.BeatFires(c.Here.Where(p => p.RaceProps.Humanlike), cell, 14f);
        }

        /// <summary>
        /// Once a stay, after the second day, at night in the rain or the cold: the apprentice takes a chill out on the high ground
        /// (the game's flu, mild; the camps' fever kind in the shared harm budget and the illness budget); told.
        /// </summary>
        public static void Chill(Camp c, TowerState st, int hour)
        {
            var map = c.map;
            bool wet = map.weatherManager.RainRate > 0.1f;
            if (!PeopleEvents6Math.ChillNight(c.DaysHere, hour, wet, map.mapTemperature.OutdoorTemp)) return;
            st.chillRolled = true;
            var ill = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            var who = Apprentice(c);
            if (ill == null || who == null || !Harm.On || !Harm.SystemOn(HarmKinds.CampFever.system) || !Harm.Roll(HarmKinds.CampFever, PeopleEvents6Math.ChillChance)) return;
            if (!IllnessBudget.Allows(map, ill, who) || Harm.Ail(who, ill, 0.1f, HarmKinds.CampFever) == null) return;
            IllnessBudget.Owe(map, ill, who);
            st.chilled = true;
            Tell.Message(NewsKind.Health, Importance.Notable, map, c.Text("CC_PEv6_TowerChill", c.leader, null, 0, who.LabelShort), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>Their apprentice: the youngest of them.</summary>
        public static Pawn Apprentice(Camp c) => c.Here.Where(p => p.RaceProps.Humanlike).OrderBy(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();

        /// <summary>The reader on the night's watch this hour: by turns.</summary>
        public static Pawn OnWatch(Camp c, int hour)
        {
            var readers = c.Here.Where(p => p.RaceProps.Humanlike && p != Apprentice(c)).OrderBy(p => p.thingIDNumber).ToList();
            return readers.Count == 0 ? null : readers[hour % readers.Count];
        }

        /// <summary>An hour of the night's watch kept at the mast: in a storm, a word.</summary>
        public static void Watched(Pawn by)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            if (c != null && StormNow(c.map) && Rand.Chance(0.3f)) c.SayMoment(by, "watch");
        }

        /// <summary>Going: the mast and rods taken down, the vial and the season's forecast left, the one who sat the watch taught.</summary>
        public static void Leave(Camp c, TowerState st)
        {
            foreach (var r in st.rods) PeopleEvents6.Unrod(r);
            st.rods.Clear();
            if (st.mast != null && !st.mast.Destroyed && st.mast.Spawned) st.mast.Destroy();
            st.mast = null;
            if (c.Hostile || c.faction == null || c.tension >= 50f) return;
            var map = c.map;
            try
            {
                var vial = VialDef;
                if (vial != null) Couriers.Hand(vial, 1, map, PeoplesD.GateSpot(map) ?? c.FireCell, c.faction, c.leader);
                if (GlassWatch.Of(map) == null) GlassWatch.Begin(map, c.faction, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef));
                var s = st.student;
                if (s != null && !s.Dead && HappeningWorker_GlassLesson.Taught != null && !HappeningWorker_GlassLesson.IsTaught(s)) s.health.AddHediff(HappeningWorker_GlassLesson.Taught);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: what the tower leaves", e);
            }
            if (c.faction.HasGoodwill && !c.faction.IsPlayer)
                WorldSituationWorker.Goodwill(c.faction, PeopleEvents6Math.TowerGoodwill + (st.meals ? PeopleEvents6Math.TowerMealsGoodwill : 0), GoodwillWhy.Helped);
        }
    }

    /// <summary>The storm tower on the high ground: its mast and rods, the storms written down, the night's watch, and how it turns.</summary>
    public class CampWorker_StormTower : CampWorker
    {
        public override bool FitsMap(Map map) => StormTower.Stormy(map);

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var high = PeopleEvents6.HighGround(map, out _);
            if (!high.IsValid) return usual;
            var c = CampLand.TryFindNear(map, high, 3f, 14f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is TowerState)) c.workState = new TowerState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is TowerState st))
            {
                Started(c);
                st = StormTower.StateOf(c);
            }
            if (!c.Settled) return;
            if (!st.raised) StormTower.Raise(c, st);
            StormTower.Storm(c, st);
            if (!st.strikeRolled && st.inStorm && StormTower.StormNowFor(c)) StormTower.Strike(c, st);
            if (!st.chillRolled) StormTower.Chill(c, st, hour);
        }

        /// <summary>By night one reader at a time keeps the watch at the mast; the rest sleep.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = StormTower.StateOf(c);
            if (st?.mast == null || !st.mast.Spawned || !(hour >= 20 || hour <= 4) || StormTower.OnWatch(c, hour) != p) return null;
            return PeopleEvents6.Make(PeopleEvents6.TowerWatchJob, st.mast, GenDate.TicksPerHour);
        }

        public override void Leaving(Camp c)
        {
            var st = StormTower.StateOf(c);
            if (st != null) StormTower.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StormTower.StateOf(c);
            if (st == null) return;
            // What they leave (the vial, the season's forecast) when they part on good terms (StormTower.Leave).
            if (!c.Hostile && c.tension < 50f) marks.Add("left");
            if (st.student != null) marks.Add("taught");
            if (st.struck) marks.Add("struck");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{STORMS}") ? text : text.Replace("{STORMS}", (StormTower.StateOf(c)?.storms ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StormTower.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "watch":
                    return st.student != null ? "CC_PEv6_TowerWatched".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A reader of {FACTION} ({X}) at the gate in the stormy months: two readers and an apprentice would camp a week on {PLACE} to write down its storms.</summary>
    public class HappeningWorker_StormTower : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            StormTower.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Seers) != null && StormTower.On(map) == null
            && StormTower.Stormy(map) && PeopleEvents6.HighGround(map, out _).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            if (ctx.faction == null || !PeopleEvents6.HighGround(ctx.map, out bool rocky).IsValid) return false;
            ctx.placeName = PeopleEvents6.HighGroundWords(rocky);
            ctx.variant = rocky ? 1 : 0;
            ctx.count = PeopleEvents6Math.TowerMeals;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Reader"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant == 1 ? "rocky" : "open");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (StormTower.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The tower's camp made on the high ground (StormTower.Start); with <see cref="meals"/>, the week's meals sent with them.</summary>
    public class DilemmaEffect_StormTower : DilemmaEffect
    {
        public bool meals;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv6_Impact_Tower".Translate(hc.faction?.Name ?? "?", hc.placeName ?? "?").Resolve(), "CC_PEv6_Impact_TowerBrief".Translate().Resolve(), "pev6tower");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv6_Impact_TowerLeaves".Translate().Resolve(), "CC_PEv6_Impact_TowerLeavesBrief".Translate().Resolve(), "pev6towerleaves");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv6_Impact_TowerTurns".Translate().Resolve(), "CC_PEv6_Impact_TowerTurnsBrief".Translate().Resolve(), "pev6towerturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.TowerGoodwill + (meals ? PeopleEvents6Math.TowerMealsGoodwill : 0));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            StormTower.Start(hc, meals);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The colony's best at the fields sits the night watch with them (the lesson is the ask's own play-out): remembered, taught when they go.</summary>
    public class DilemmaEffect_TowerWatch : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            sheet.Add(ImpactKind.Other, 1, "CC_PeoplesE_Impact_Taught".Translate(ctx.a.LabelShort).Resolve(), "CC_PeoplesE_Impact_TaughtBrief".Translate().Resolve(), "pev6towertaught");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_StormTower);
            var st = StormTower.StateOf(c);
            if (st != null && ctx?.a != null) st.student = ctx.a;
        }
    }
}
