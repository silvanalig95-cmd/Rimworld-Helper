using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 66. The line crew (the Signal Line) =====================

    /// <summary>The line crew's week: their mast (or tower), the news heard at their fire, the colony's keeping of it, and how it turned.</summary>
    public class LineState : CampWorkState
    {
        public Thing mast;
        public bool raised, tower, keep, fed, fallRolled, fell, struckRolled, struck;
        public int newsDay = -1, news, raiseAfter = -1;

        public override void ExposeData()
        {
            Scribe_References.Look(ref mast, "mast");
            Scribe_Values.Look(ref raised, "raised");
            Scribe_Values.Look(ref tower, "tower");
            Scribe_Values.Look(ref keep, "keep");
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref fallRolled, "fallRolled");
            Scribe_Values.Look(ref fell, "fell");
            Scribe_Values.Look(ref struckRolled, "struckRolled");
            Scribe_Values.Look(ref struck, "struck");
            Scribe_Values.Look(ref newsDay, "newsDay", -1);
            Scribe_Values.Look(ref news, "news");
            Scribe_Values.Look(ref raiseAfter, "raiseAfter", -1);
        }
    }

    /// <summary>
    /// The line crew (item 66): three or four linesmen of the Signal Line camp a week on the colony's high ground (the relay mast's own
    /// choice of it) and raise a working mast there, the Line's, and string wire by day. While it stands the colony's letters go out by
    /// their wire (half the days on the road, World/NeighbourLetters.cs through PeopleEvents7.Relays), and once a night the radio at their
    /// fire plays the region's news, each place it names heard of (World/HeardPlaces.cs). They ask for components and food. The colony may
    /// keep the mast as its own relay mast when they go, for a fee (only while it has none). A Line of the tribal or medieval age raises a
    /// flag-and-lamp tower instead, its watcher reading the next tower's flags at dusk, and takes it with them. It can turn, once a stay
    /// each, in a thunderstorm: a lineman up the mast falls (the dangers on the map, the shared harm budget), or the lightning takes the
    /// mast (the game's own bolt, clear of anyone; the camps' embers' kind for the fire it may start) and they raise it again next day.
    /// </summary>
    public static class LineCrew
    {
        public const string EventDef = "Rimfolk_Evt_LineCrew";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_LineCrew");

        public static ThingDef TowerDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_SignalTower");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static LineState StateOf(Camp c) => c?.workState as LineState;

        /// <summary>The Line raises a flag-and-lamp tower before the industrial age, a radio mast from it on.</summary>
        public static bool Tower(Faction line) => PeopleEvents.AgeOf(line) < 2;

        /// <summary>A crew camped at this home with its mast up: the colony's letters go out by their wire.</summary>
        public static bool Relays(Map home)
        {
            var c = On(home);
            var st = StateOf(c);
            return c != null && c.Settled && !c.Hostile && st?.mast != null && st.mast.Spawned && !st.mast.Destroyed;
        }

        /// <summary>The camp made on the high ground, the crew walking in; <paramref name="fed"/>: the week's food went with them from the gate.</summary>
        public static Camp Start(HappeningContext ctx, bool fed = false)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                int n = Rand.RangeInclusive(PeopleEvents7Math.CrewMin, PeopleEvents7Math.CrewMax);
                for (int i = 0; i < n; i++)
                    people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, i == 0 ? "Rimfolk_SignalLine_RelayKeeper" : i == 1 ? "Rimfolk_SignalLine_Operator" : "Rimfolk_SignalLine_Lineman"),
                        ctx.faction, new FloatRange(20f, 50f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents7Math.CrewDays);
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
                c.workState = new LineState { tower = Tower(ctx.faction), fed = fed };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the line crew", e);
                return null;
            }
        }

        /// <summary>Where the mast goes: by their fire on the high ground, on ground that bears it.</summary>
        private static IntVec3 MastCell(Camp c)
        {
            var map = c.map;
            foreach (var x in GenRadial.RadialCellsAround(c.FireCell + new IntVec3(0, 0, 3), 6f, true))
                if (RelayMast.Fits(map, x) && !GenAdj.CellsAdjacent8Way(new TargetInfo(x, map)).Any(n => n.InBounds(map) && n.GetEdifice(map) != null)) return x;
            return IntVec3.Invalid;
        }

        /// <summary>Once settled (and the day after the lightning took it), the keeper raises the mast by their fire: two hours' work, then it stands.</summary>
        public static void Raise(Camp c, LineState st)
        {
            if (st.raiseAfter >= 0 && Now < st.raiseAfter) return;
            var keeper = c.leader != null && c.leader.Spawned ? c.leader : c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
            var cell = MastCell(c);
            if (keeper == null || !cell.IsValid) return;
            st.raised = true;
            st.raiseAfter = -1;
            if (keeper.CurJobDef == PeopleEvents7.StringJob) return;
            if (!PeopleEvents7.Send(keeper, PeopleEvents7.StringJob, cell, PeopleEvents7Math.RaiseTicks)) Stood(c, st, cell);
            else c.SayMoment(keeper, "raise");
        }

        /// <summary>The mast raised but not standing, and nobody of theirs at it (the keeper called off the work): it stands now.</summary>
        public static void Stand(Camp c, LineState st)
        {
            if (!st.raised || st.mast != null && st.mast.Spawned || st.raiseAfter >= 0 || c.Here.Any(p => p.CurJobDef == PeopleEvents7.StringJob)) return;
            var cell = MastCell(c);
            if (cell.IsValid) Stood(c, st, cell);
        }

        /// <summary>The mast (or the tower) stands at the cell, theirs; told.</summary>
        private static void Stood(Camp c, LineState st, IntVec3 cell)
        {
            var def = st.tower ? TowerDef : PeoplesF.MastDef;
            var map = c.map;
            if (def == null || st.mast != null && st.mast.Spawned) return;
            if (!RelayMast.Fits(map, cell))
            {
                cell = MastCell(c);
                if (!cell.IsValid) return;
            }
            try
            {
                var mast = ThingMaker.MakeThing(def);
                mast.SetFaction(c.faction);
                GenSpawn.Spawn(mast, cell, map);
                st.mast = mast;
                FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the crew's mast", e);
                return;
            }
            c.Note(c.Text(st.tower ? "CC_PEv7_CrewTowerUp" : "CC_PEv7_CrewMastUp", c.leader), MessageTypeDefOf.NeutralEvent, Importance.Notable, st.mast, true);
        }

        /// <summary>A job of theirs on the wire done: at the mast's cell before it stands, the mast raised; else a length of wire strung.</summary>
        public static void Strung(Pawn by, LocalTargetInfo target)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null) return;
            if (st.raised && (st.mast == null || !st.mast.Spawned) && !target.HasThing) Stood(c, st, target.Cell);
            else if (Rand.Chance(0.2f)) c.SayMoment(by, "wire");
        }

        /// <summary>A storm on the high ground (thunder and lightning).</summary>
        public static bool StormNow(Map map) => PeoplesF.Storm(map);

        /// <summary>
        /// Once a stay, by day in a thunderstorm: a lineman goes up the mast to make it fast; at the turn's chance (the dangers on the map,
        /// the shared harm budget) they fall when the climb is done (a cracked or bruised leg); told.
        /// </summary>
        public static void Climb(Camp c, LineState st)
        {
            st.fallRolled = true;
            var climber = c.Here.Where(p => p.RaceProps.Humanlike && p != c.leader).RandomElementWithFallback() ?? c.leader;
            if (climber == null || st.mast == null || !st.mast.Spawned) return;
            if (PeopleEvents7.Send(climber, PeopleEvents7.ClimbJob, st.mast, GenDate.TicksPerHour)) c.SayMoment(climber, "climb");
        }

        /// <summary>A climb in the storm done: the mast made fast, and the fall at its chance.</summary>
        public static void Climbed(Pawn by, Thing mast)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null || st.fell || !StormNow(c.map)) return;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.Dangers.system) || !Harm.Roll(by, HarmKinds.Dangers, PeopleEvents7Math.FallChance)) return;
            var wound = Harm.Injure(by, Rand.Bool ? HurtKind.Crack : HurtKind.Bruise, HurtWhere.Leg, PeopleEvents7Math.FallLow, PeopleEvents7Math.FallHigh, HarmKinds.Dangers);
            if (wound == null) return;
            st.fell = true;
            c.AddTension(5f);
            Tell.Message(NewsKind.World, Importance.Notable, c.map, c.Text("CC_PEv7_CrewFell", by, null, 0, wound.Part?.Label ?? ""), new LookTargets(by), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>
        /// Once a stay, in a thunderstorm with nobody near the mast: the game's own bolt comes down on it, the camps' embers' kind in the
        /// shared harm budget for the fire it may start; the mast is broken, and they raise another the next day; told, the fire beaten out.
        /// </summary>
        public static void Strike(Camp c, LineState st)
        {
            var mast = st.mast;
            var map = c.map;
            if (mast == null || !mast.Spawned) return;
            if (map.mapPawns.AllPawnsSpawned.Any(p => p.Position.InHorDistOf(mast.Position, 4f))) return;
            st.struckRolled = true;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, PeopleEvents7Math.MastStruckChance)) return;
            var cell = mast.Position;
            try
            {
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, cell));
                if (!mast.Destroyed) mast.Destroy(DestroyMode.KillFinalize);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the lightning on the mast", e);
                return;
            }
            st.struck = true;
            st.mast = null;
            st.raised = false;
            st.raiseAfter = Now + GenDate.TicksPerDay / 2;
            HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
            var keeper = c.leader != null && c.leader.Spawned ? c.leader : c.Here.FirstOrDefault();
            if (keeper != null) c.SayMoment(keeper, "struck");
            c.Note(c.Text(st.tower ? "CC_PEv7_CrewTowerStruck" : "CC_PEv7_CrewMastStruck", keeper), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
            PeopleEvents6.BeatFires(c.Here.Where(p => p.RaceProps.Humanlike), cell, 12f);
        }

        /// <summary>
        /// An hour at the radio by their fire at night done: once a night the region's news, a line of it told with the place it names
        /// marked as heard of (from the Line, through its radio).
        /// </summary>
        public static void Listened(Pawn by)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            int day = GenDate.DaysPassed;
            if (st == null || st.newsDay == day) return;
            st.newsDay = day;
            try
            {
                var town = WorldSituationWorker.NearestTown(c.faction, c.map);
                var scene = new FactScene { colony = c.map, people = c.faction, place = town, listener = by, asks = FactsMath.Letter(true) };
                var facts = Facts.For(scene);
                if (facts == null || facts.news.Count == 0) return;
                int i = Rand.Range(0, facts.news.Count);
                string line = facts.news[i];
                string fact = i < facts.newsFacts.Count ? facts.newsFacts[i] : null;
                if (fact != null)
                    HeardPlaces.Heard(new[] { fact }, line, (c.faction?.Name ?? "?"), by.LabelShort);
                st.news++;
                c.SayMoment(by, st.tower ? "flags" : "radio");
                c.Note(c.Text(st.tower ? "CC_PEv7_CrewFlagsNews" : "CC_PEv7_CrewRadioNews", by, null, 0, line), MessageTypeDefOf.NeutralEvent, Importance.Flavor, by, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the news at the fire", e);
            }
        }

        /// <summary>Going: the mast the colony's when it kept it (and has none), else taken down and carried off; told.</summary>
        public static void Leave(Camp c, LineState st)
        {
            var mast = st.mast;
            if (mast == null || mast.Destroyed || !mast.Spawned) return;
            if (st.keep && !st.tower && !c.Hostile && RelayMast.Of(c.map) == null)
            {
                mast.SetFaction(Faction.OfPlayer);
                Tell.Message(NewsKind.Land, Importance.Notable, c.map, c.Text("CC_PEv7_CrewMastKept", c.leader), new LookTargets(mast), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                FleckMaker.ThrowDustPuff(mast.Position, c.map, 1f);
                mast.Destroy();
            }
            st.mast = null;
            if (!c.Hostile && c.tension < 50f) PeopleEvents7.Goodwill(c.faction, PeopleEvents7Math.CrewGoodwill, GoodwillWhy.CampWell);
        }

        /// <summary>Dev mode: a storm's turn now (the climb and the fall at its chance, or the lightning on the mast).</summary>
        public static string Turn(Camp c, bool strike)
        {
            var st = StateOf(c);
            if (st == null) return "no line crew here.";
            if (strike)
            {
                st.struckRolled = false;
                Strike(c, st);
                return st.struck ? "the lightning took the mast." : "no strike (someone near the mast, the shared harm budget said no, or no mast).";
            }
            st.fallRolled = false;
            Climb(c, st);
            return "a lineman goes up the mast; the fall is rolled when the climb is done, in a storm.";
        }
    }

    /// <summary>The line crew on the high ground: the mast raised, the wire by day, the radio at night, and the storm's turns.</summary>
    public class CampWorker_LineCrew : CampWorker
    {
        public override bool FitsMap(Map map) => PeopleEvents6.HighGround(map, out _).IsValid;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var high = PeopleEvents6.HighGround(map, out _);
            if (!high.IsValid) return usual;
            var c = CampLand.TryFindNear(map, high, 3f, 14f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is LineState)) c.workState = new LineState { tower = LineCrew.Tower(c.faction) };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is LineState st))
            {
                Started(c);
                st = LineCrew.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            if (!st.raised) LineCrew.Raise(c, st);
            else LineCrew.Stand(c, st);
            bool storm = LineCrew.StormNow(c.map);
            if (storm && st.mast != null && st.mast.Spawned && hour >= 9 && hour <= 16)
            {
                if (!st.fallRolled) LineCrew.Climb(c, st);
                else if (!st.struckRolled) LineCrew.Strike(c, st);
            }
        }

        /// <summary>By day they string wire out from the mast toward the colony.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = LineCrew.StateOf(c);
            if (st?.mast == null || !st.mast.Spawned || !p.RaceProps.Humanlike || p == c.leader || !PeopleEvents7Math.WireHour(GenLocalDate.HourOfDay(c.map))) return null;
            var mid = CampSite.ColonyCenter(c.map);
            var from = st.mast.Position;
            float t = Rand.Range(0.15f, 0.5f);
            var at = mid.IsValid ? new IntVec3(Mathf.RoundToInt(from.x + (mid.x - from.x) * t), 0, Mathf.RoundToInt(from.z + (mid.z - from.z) * t)) : from;
            var cell = CellFinder.StandableCellNear(at, c.map, 3f);
            return cell.IsValid ? PeopleEvents7.Make(PeopleEvents7.StringJob, cell, PeopleEvents7Math.StringTicks) : null;
        }

        /// <summary>In the evening one of them sits at the radio (or watches the next tower's lamps) by their fire.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = LineCrew.StateOf(c);
            if (st?.mast == null || !PeopleEvents7Math.RadioHour(hour) || st.newsDay == GenDate.DaysPassed) return null;
            var listener = c.Here.Where(x => x.RaceProps.Humanlike).OrderBy(x => x.thingIDNumber).Skip(GenDate.DaysPassed % Math.Max(1, c.Here.Count)).FirstOrDefault();
            if (listener != p) return null;
            var cell = CellFinder.StandableCellNear(c.FireCell, c.map, 2f);
            return cell.IsValid ? PeopleEvents7.Make(PeopleEvents7.ListenJob, cell, GenDate.TicksPerHour) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = LineCrew.StateOf(c);
            if (st != null) LineCrew.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = LineCrew.StateOf(c);
            if (st == null) return;
            if (st.keep && !st.tower) marks.Add("kept");
            if (st.fell) marks.Add("fell");
            if (st.struck) marks.Add("struck");
            if (st.tower) marks.Add("tower");
        }

        public override string Fill(Camp c, string text)
        {
            if (text == null) return null;
            var st = LineCrew.StateOf(c);
            if (text.Contains("{MAST}")) text = text.Replace("{MAST}", (st?.tower ?? LineCrew.Tower(c.faction) ? "CC_PEv7_Tower" : "CC_PEv7_Mast").Translate().Resolve());
            if (text.Contains("{NEWS}")) text = text.Replace("{NEWS}", (st?.news ?? 0).ToString());
            return text;
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = LineCrew.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "food":
                    return st.fed ? "CC_PEv7_CrewFed".Translate().Resolve() : null;
                case "keep":
                    if (st.tower) return "CC_PEv7_KeepTower".Translate().Resolve();
                    if (st.keep) return "CC_PEv7_KeepAlready".Translate().Resolve();
                    if (RelayMast.Of(c.map) != null) return "CC_PEv7_KeepHave".Translate().Resolve();
                    return st.mast == null || !st.mast.Spawned ? "CC_PEv7_KeepNoMast".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A lineman of {FACTION} ({X}) at the gate: a crew would camp a week on {PLACE} and raise a {TALE} there, the colony's letters going out by their wire.</summary>
    public class HappeningWorker_LineCrew : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            LineCrew.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Linesmen) != null && LineCrew.On(map) == null
            && PeopleEvents6.HighGround(map, out _).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            if (ctx.faction == null || !PeopleEvents6.HighGround(ctx.map, out bool rocky).IsValid) return false;
            ctx.placeName = PeopleEvents6.HighGroundWords(rocky);
            ctx.variant = rocky ? 1 : 0;
            ctx.tale = (LineCrew.Tower(ctx.faction) ? "CC_PEv7_Tower" : "CC_PEv7_Mast").Translate().Resolve();
            ctx.count = PeopleEvents7Math.KeepFee(LivingEvents.WealthFactor(ctx.map));
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SignalLine_Lineman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(LineCrew.Tower(ctx.faction) ? "tower" : "mast");
            if (RelayMast.Of(ctx.map) == null) tags.Add("nomast");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (LineCrew.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The crew's camp made on the high ground (LineCrew.Start); with <see cref="fed"/>, the week's food went with them.</summary>
    public class DilemmaEffect_LineCrew : DilemmaEffect
    {
        public bool fed;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv7_Impact_Crew".Translate(hc.faction?.Name ?? "?", hc.placeName ?? "?", hc.tale ?? "?").Resolve(), "CC_PEv7_Impact_CrewBrief".Translate().Resolve(), "pev7crew");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv7_Impact_CrewLetters".Translate().Resolve(), "CC_PEv7_Impact_CrewLettersBrief".Translate().Resolve(), "pev7crewletters");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv7_Impact_CrewTurns".Translate().Resolve(), "CC_PEv7_Impact_CrewTurnsBrief".Translate().Resolve(), "pev7crewturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.CrewGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            LineCrew.Start(hc, fed);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The crew's mast kept as the colony's own relay mast when they go (the fee is the ask's own).</summary>
    public class DilemmaEffect_KeepMast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv7_Impact_KeepMast".Translate().Resolve(), "CC_PEv7_Impact_KeepMastBrief".Translate().Resolve(), "pev7keepmast");

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_LineCrew);
            var st = LineCrew.StateOf(c);
            if (st != null) st.keep = true;
        }
    }
}
