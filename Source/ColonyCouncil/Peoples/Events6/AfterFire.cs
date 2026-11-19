using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 54. After the fire (the Mossborn) =====================

    /// <summary>After the fire's week: the burn (its middle and reach, or a blight's), what is left to cut and to plant, and how it turned.</summary>
    public class AfterState : CampWorkState
    {
        public IntVec3 middle = IntVec3.Invalid;
        public float radius = 10f;
        public List<IntVec3> saplings = new List<IntVec3>();
        public Pawn planter;
        public int cut, planted;
        public bool blight, planned, fed, rekindleRolled, rekindled, wronged;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref middle, "middle", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "radius", 10f);
            Scribe_Collections.Look(ref saplings, "saplings", LookMode.Value);
            Scribe_References.Look(ref planter, "planter");
            Scribe_Values.Look(ref cut, "cut");
            Scribe_Values.Look(ref planted, "planted");
            Scribe_Values.Look(ref blight, "blight");
            Scribe_Values.Look(ref planned, "planned");
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref rekindleRolled, "rekindleRolled");
            Scribe_Values.Look(ref rekindled, "rekindled");
            Scribe_Values.Look(ref wronged, "wronged");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) saplings ??= new List<IntVec3>();
        }
    }

    /// <summary>
    /// After the fire (item 54): after a wildfire left ten burned trees or more on the colony's land, or a blight took six of its
    /// plants or more, a grove-warden and seed-carriers of the Mossborn camp a week in the burn. They cut the burned trees (the
    /// logs are the colony's) or the blighted plants (so the blight stops there), plant saplings across the burn (the land's
    /// own trees, sown by hand: the colony's planting in their ledger, Peoples/PeoplesE.cs), and sing at their fire at night.
    /// They ask for food, and that the colony fell nothing within the planting while they are there; the colony's planter may
    /// plant beside them (a lesson). It can turn: the colony fells within the planting (a wrong: they leave and think less of
    /// the colony), or, once a stay in dry weather, the fire starts again in the ash (the game's own fire; the camps' embers'
    /// kind in the shared harm budget), and they beat it out.
    /// </summary>
    public static class AfterFire
    {
        public const string EventDef = "Rimfolk_Evt_AfterFire";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_AfterFire");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static AfterState StateOf(Camp c) => c?.workState as AfterState;

        private static HashSet<ThingDef> burnedDefs;

        /// <summary>What a burned tree becomes: every tree's burned form (the game's burned tree, and any mod's own).</summary>
        public static HashSet<ThingDef> BurnedDefs
        {
            get
            {
                if (burnedDefs != null) return burnedDefs;
                burnedDefs = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.plant?.burnedThingDef != null).Select(d => d.plant.burnedThingDef));
                var game = DefDatabase<ThingDef>.GetNamedSilentFail("BurnedTree");
                if (game != null) burnedDefs.Add(game);
                return burnedDefs;
            }
        }

        /// <summary>The burned trees on the land (within reach of a spot, if it is valid).</summary>
        public static List<Thing> Burned(Map map, IntVec3 at, float radius)
        {
            if (map == null) return new List<Thing>();
            var list = new List<Thing>();
            foreach (var d in BurnedDefs)
                foreach (var t in map.listerThings.ThingsOfDef(d))
                    if (t.Spawned && !t.Position.Fogged(map) && (!at.IsValid || t.Position.InHorDistOf(at, radius)))
                        list.Add(t);
            return list;
        }

        /// <summary>The colony's plants the blight has.</summary>
        public static List<Thing> Blighted(Map map, IntVec3 at, float radius)
        {
            if (map == null) return new List<Thing>();
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .Where(t => t is Plant p && p.Spawned && p.Blighted && (!at.IsValid || t.Position.InHorDistOf(at, radius))).ToList();
        }

        /// <summary>The middle of what they would come for (the burn, else the blight) and how far it reaches.</summary>
        public static IntVec3 Middle(Map map, out float reach, out bool blight)
        {
            reach = 10f;
            blight = false;
            // A big burn first; a blight before a small burn; any burn of a few trees when asked back from the fire (their own
            // coming asks for ten new burned trees: Watch).
            var burned = Burned(map, IntVec3.Invalid, 0f);
            var blighted = Blighted(map, IntVec3.Invalid, 0f);
            blight = blighted.Count >= PeopleEvents6Math.BlightMin && burned.Count < PeopleEvents6Math.BurnedMin || burned.Count < PeopleEvents6Math.BurnedAny;
            var things = blight ? blighted : burned;
            if (blight && things.Count < PeopleEvents6Math.BlightMin) return IntVec3.Invalid;
            int x = 0, z = 0;
            foreach (var t in things)
            {
                x += t.Position.x;
                z += t.Position.z;
            }
            var mid = new IntVec3(x / things.Count, 0, z / things.Count);
            reach = PeopleEvents6Math.PlantingRadius(things.Max(t => t.Position.DistanceTo(mid)));
            return mid;
        }

        /// <summary>
        /// The homes' hour: their camp asked for at the fire comes when it is due and the fire is out; else a burn of ten new
        /// burned trees, or a blight on six plants, the fire out and the Mossborn at peace and not lately here: once each, at a
        /// chance (the storytellers' lean on visitors).
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null || On(map) != null) return;
            bool fireOut = PeopleEvents6.Fires(map, map.Center, 9999f).Count < 3;
            var asked = b.afters.FirstOrDefault(r => r != null && !r.done && r.map == map);
            if (asked != null)
            {
                if (now > asked.due + 3 * GenDate.TicksPerDay || !PeoplesD.Theirs(asked.faction, PeoplesMath.Grovekeepers)) asked.done = true;
                else if (now >= asked.due && fireOut && Tell.CanAsk && CampTracker.RoomFor(map) && !MapFacts.ThreatOn(map))
                {
                    if (PeopleEvents2.Fire(EventDef, map, asked.faction))
                    {
                        asked.done = true;
                        b.lastAfter = now;
                    }
                }
                return;
            }
            if (!fireOut) return;
            var burned = Burned(map, IntVec3.Invalid, 0f);
            int seen = b.BurnSeen(map);
            int fresh = burned.Count(t => t.thingIDNumber > seen);
            int day = GenDate.DaysPassed;
            bool blight = fresh < PeopleEvents6Math.BurnedMin && Blighted(map, IntVec3.Invalid, 0f).Count >= PeopleEvents6Math.BlightMin && !b.BlightRolled(map, day);
            if (fresh < PeopleEvents6Math.BurnedMin && !blight) return;
            if (fresh >= PeopleEvents6Math.BurnedMin) b.NoteBurn(map, burned.Max(t => t.thingIDNumber));
            else b.NoteBlight(map, day);
            var f = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            if (f == null || b.lastAfter >= 0 && now - b.lastAfter < PeopleEvents6Math.AfterGapDays * GenDate.TicksPerDay) return;
            if (!CampTracker.RoomFor(map) || !Tell.CanAsk || MapFacts.ThreatOn(map)) return;
            if (!Rand.Chance(PeopleEvents6Math.AfterChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastAfter = now;
        }

        /// <summary>Dev mode: their camp due here now (a burn or a blight must be on the land).</summary>
        public static string Soon(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            var b = PeopleEvents6Book.Instance;
            if (map == null || f == null || b == null) return "no Mossborn at peace.";
            if (!Middle(map, out _, out _).IsValid) return "no burn (ten burned trees) or blight (six plants) here.";
            b.afters.RemoveAll(r => r != null && r.map == map);
            b.AddAfter(new AfterRecord { map = map, faction = f, due = Now });
            return "the grove-warden comes within the hour (when a question may be asked).";
        }

        /// <summary>The camp made in the burn, a grove-warden and seed-carriers walking in.</summary>
        public static Camp Start(HappeningContext ctx, bool fed)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var mid = Middle(map, out float reach, out bool blight);
            if (!mid.IsValid) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents6Math.AfterDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var center = CampLand.TryFindNear(map, mid, 4f, Mathf.Max(12f, reach + 6f), hot);
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
                c.workState = new AfterState { middle = mid, radius = reach, blight = blight, fed = fed };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the camp after the fire", e);
                return null;
            }
        }

        /// <summary>The planting laid out: the cells across the burn its saplings go in, a few apart, out of the colony's home and fields.</summary>
        public static void Plan(Camp c, AfterState st)
        {
            st.planned = true;
            var map = c.map;
            var home = map.areaManager.Home;
            st.saplings.Clear();
            foreach (var x in GenRadial.RadialCellsAround(st.middle, st.radius, true))
            {
                if (st.saplings.Count >= PeopleEvents6Math.MaxSaplings) break;
                if (!x.InBounds(map) || home != null && home[x] || x.GetZone(map) != null) continue;
                // Where a burned tree or a blighted plant stands now, a sapling once it is cleared.
                bool clearing = x.GetPlant(map) is Plant p && (BurnedDefs.Contains(p.def) || p.Blighted);
                if (!clearing && !PeoplesE.SaplingCell(map, x)) continue;
                if (x.GetTerrain(map) == null || x.GetTerrain(map).fertility < 0.7f || x.Roofed(map)) continue;
                if (st.saplings.Any(o => o.DistanceToSquared(x) < PeopleEvents6Math.SaplingsApartSq)) continue;
                st.saplings.Add(x);
            }
        }

        private static bool Busy(Camp c, Pawn p, JobDef job, LocalTargetInfo t) =>
            c.Here.Any(o => o != p && o.CurJobDef == job && o.CurJob.targetA == t);

        /// <summary>One of theirs by day: the fire first if it has started again, then the burned trees cut, then a sapling planted.</summary>
        public static Job Work(Camp c, AfterState st, Pawn p)
        {
            var map = c.map;
            if (!p.RaceProps.Humanlike || st.wronged) return null;
            if (PeopleEvents6.NearestFire(map, p, st.middle, st.radius + 6f) != null && PeopleEvents6.FirefightJob != null)
            {
                var j = JobMaker.MakeJob(PeopleEvents6.FirefightJob, st.middle);
                j.count = Mathf.RoundToInt(st.radius + 6f);
                return j;
            }
            if (!st.planned) return null;
            var cut = (st.blight ? Blighted(map, st.middle, st.radius) : Burned(map, st.middle, st.radius))
                .Where(t => !Busy(c, p, PeopleEvents6.CutJob, t) && p.CanReach(t, PathEndMode.Touch, Danger.Some)).OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (cut != null) return PeopleEvents6.Make(PeopleEvents6.CutJob, cut, PeopleEvents6Math.CutTicks);
            var cell = st.saplings.Where(x => PeoplesE.SaplingCell(map, x) && !Busy(c, p, PeopleEvents6.SaplingJob, x) && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.DistanceToSquared(p.Position)).FirstOrDefault();
            if (cell.IsValid && st.saplings.Contains(cell)) return PeopleEvents6.Make(PeopleEvents6.SaplingJob, cell, PeopleEvents6Math.SaplingTicks);
            return null;
        }

        private static Camp CampOf(Pawn by) => CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));

        /// <summary>A burned tree cut (its wood the colony's, lying where it stood), or a blighted plant cut out.</summary>
        public static void Cut(Pawn by, Thing t)
        {
            var c = CampOf(by);
            var st = StateOf(c);
            if (st == null || t == null || t.Destroyed || !t.Spawned) return;
            var map = t.Map;
            var at = t.Position;
            bool burned = BurnedDefs.Contains(t.def);
            int n = 0;
            ThingDef wood = null;
            if (burned && t is Plant p)
            {
                wood = p.def.plant?.harvestedThingDef ?? ThingDefOf.WoodLog;
                try
                {
                    n = p.YieldNow();
                }
                catch
                {
                    n = 0;
                }
                if (n <= 0) n = 6;
            }
            t.Destroy();
            st.cut++;
            if (wood != null && n > 0)
            {
                var logs = ThingMaker.MakeThing(wood);
                logs.stackCount = n;
                GenPlace.TryPlaceThing(logs, at, map, ThingPlaceMode.Near);
            }
            if (Rand.Chance(0.2f)) c.SayMoment(by, "cut");
        }

        /// <summary>A sapling set in a cell of the planting (the land's own tree, sown: the colony's planting in the ledger).</summary>
        public static void Planted(Pawn by, IntVec3 cell)
        {
            var c = CampOf(by);
            var st = StateOf(c);
            if (st == null) return;
            if (PeoplesE.Sapling(c.map, cell, PeoplesE.SaplingFor(c.map, cell)) == null) return;
            st.saplings.Remove(cell);
            st.planted++;
            if (Rand.Chance(0.25f)) c.SayMoment(by, "plant");
        }

        /// <summary>A song at their fire.</summary>
        public static void Sang(Pawn by)
        {
            var c = CampOf(by);
            if (c != null && Rand.Chance(0.5f)) c.SayMoment(by, "sing");
        }

        /// <summary>
        /// A tree felled by the colony's people (MossLedger.Notify_Felled): within the planting while their camp stands, it is a
        /// wrong. They leave, think less of the colony and remember it; told.
        /// </summary>
        public static void Felled(Plant plant, Pawn by)
        {
            var map = plant?.MapHeld;
            var c = On(map);
            var st = StateOf(c);
            if (st == null || st.wronged || !c.Settled || c.Hostile || !st.middle.IsValid || !plant.PositionHeld.InHorDistOf(st.middle, st.radius)) return;
            st.wronged = true;
            var f = c.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.FellWrongGoodwill, GoodwillWhy.Ledger);
            if (f != null) FactionMemory.Note(f, "slighted", "CC_PEv6_FelledName".Translate().Resolve());
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "felled");
            c.Leave("felled");
        }

        /// <summary>
        /// Once a stay, after the first day, by day in dry, warm weather: the fire starts again in the ash, out of the colony's
        /// home and clear of anyone (the camps' embers' kind in the shared harm budget; the game's own fire); told, and they
        /// beat it out.
        /// </summary>
        public static void Rekindle(Camp c, AfterState st, int hour)
        {
            var map = c.map;
            if (c.DaysHere < 1f || hour < 10 || hour > 18 || map.weatherManager.RainRate > 0.05f || map.mapTemperature.OutdoorTemp < 15f) return;
            st.rekindleRolled = true;
            if (st.blight || !Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, PeopleEvents6Math.RekindleChance)) return;
            var people = map.mapPawns.AllPawnsSpawned;
            foreach (var x in GenRadial.RadialCellsAround(st.middle, st.radius, true).InRandomOrder())
            {
                if (!x.InBounds(map) || x.Roofed(map) || (map.areaManager.Home?[x] ?? false) || x.GetPlant(map) == null) continue;
                if (people.Any(p => p.Position.InHorDistOf(x, 4f))) continue;
                if (!FireUtility.TryStartFireIn(x, map, 0.3f, null)) continue;
                st.rekindled = true;
                HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
                var warden = c.leader != null && c.leader.Spawned ? c.leader : c.Here.FirstOrDefault();
                if (warden != null) c.SayMoment(warden, "fire");
                c.Note(c.Text("CC_PEv6_AfterRekindled", warden), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
                PeopleEvents6.BeatFires(c.Here.Where(p => p.RaceProps.Humanlike), st.middle, st.radius + 6f);
                return;
            }
        }
    }

    /// <summary>The camp in the burn: the cutting, the planting, the night's song, the term on felling, and the fire that starts again.</summary>
    public class CampWorker_AfterFire : CampWorker
    {
        public override bool FitsMap(Map map) => AfterFire.Middle(map, out _, out _).IsValid;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var mid = AfterFire.Middle(map, out float reach, out _);
            if (!mid.IsValid) return usual;
            var c = CampLand.TryFindNear(map, mid, 4f, Mathf.Max(12f, reach + 6f), shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (c.workState is AfterState) return;
            var mid = AfterFire.Middle(c.map, out float reach, out bool blight);
            c.workState = new AfterState { middle = mid.IsValid ? mid : c.center, radius = reach, blight = blight };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is AfterState st))
            {
                Started(c);
                st = AfterFire.StateOf(c);
            }
            if (!c.Settled || st.wronged) return;
            if (!st.planned) AfterFire.Plan(c, st);
            if (!st.rekindleRolled) AfterFire.Rekindle(c, st, hour);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = AfterFire.StateOf(c);
            return st == null ? null : AfterFire.Work(c, st, p);
        }

        /// <summary>At night they sing by their fire a while, then sleep.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = AfterFire.StateOf(c);
            if (st == null || st.wronged || !p.RaceProps.Humanlike || !PeopleEvents6Math.Sing(hour) || !c.FireCell.IsValid) return null;
            return PeopleEvents6.Make(PeopleEvents6.SingJob, c.FireCell, PeopleEvents6Math.SingTicks);
        }

        public override void Leaving(Camp c)
        {
            var st = AfterFire.StateOf(c);
            if (st == null || st.wronged || c.Hostile || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            if ((st.planted > 0 || st.cut > 0) && c.tension < 50f)
                WorldSituationWorker.Goodwill(c.faction, PeopleEvents6Math.AfterGoodwill + (st.planter != null ? PeopleEvents6Math.PlanterGoodwill : 0), GoodwillWhy.Helped);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = AfterFire.StateOf(c);
            if (st == null) return;
            if (st.planted > 0) marks.Add("planted");
            if (st.planter != null) marks.Add("planter");
            if (st.rekindled) marks.Add("rekindled");
            if (st.blight) marks.Add("blight");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{PLANTED}") ? text : text.Replace("{PLANTED}", (AfterFire.StateOf(c)?.planted ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = AfterFire.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "food":
                    return st.fed ? "CC_PEv6_AfterFed".Translate().Resolve() : null;
                case "plant":
                    return st.planter != null ? "CC_PEv6_AfterPlanterThere".Translate().Resolve() : st.saplings.Count == 0 ? "CC_PEv6_AfterNothingToPlant".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A grove-warden of {FACTION} ({X}) at the gate after a fire (or a blight, {TALE} what it left): they would camp a week in the burn to clear and replant it.</summary>
    public class HappeningWorker_AfterFire : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            AfterFire.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Grovekeepers) != null && AfterFire.On(map) == null
            && AfterFire.Middle(map, out _, out _).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var asked = PeopleEvents6Book.Instance?.afters.FirstOrDefault(r => r != null && !r.done && r.map == ctx.map && PeoplesD.Theirs(r.faction, PeoplesMath.Grovekeepers));
            ctx.faction = asked?.faction ?? PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            var mid = AfterFire.Middle(ctx.map, out _, out bool blight);
            if (ctx.faction == null || !mid.IsValid) return false;
            ctx.variant = blight ? AfterFire.Blighted(ctx.map, IntVec3.Invalid, 0f).Count : AfterFire.Burned(ctx.map, IntVec3.Invalid, 0f).Count;
            ctx.dead = ctx.variant.ToString();
            ctx.note = (blight ? "blight" : "fire") + (asked != null ? "|asked" : "");
            ctx.count = PeopleEvents6Math.AfterMeals;
            ctx.spot = mid;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Mossborn_Tender"), ctx.faction, ext.ages);
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
            tags.Add(ctx.note != null && ctx.note.StartsWith("blight") ? "blight" : "fire");
            if (ctx.note != null && ctx.note.EndsWith("asked")) tags.Add("asked");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (AfterFire.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map))) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Their camp made in the burn (AfterFire.Start); with <see cref="fed"/>, the week's food sent with them.</summary>
    public class DilemmaEffect_AfterFire : DilemmaEffect
    {
        public bool fed;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            bool blight = hc.note != null && hc.note.StartsWith("blight");
            sheet.Add(ImpactKind.People, 1, (blight ? "CC_PEv6_Impact_AfterBlight" : "CC_PEv6_Impact_After").Translate(hc.faction?.Name ?? "?").Resolve(),
                "CC_PEv6_Impact_AfterBrief".Translate().Resolve(), "pev6after");
            sheet.Add(ImpactKind.Other, 0, "CC_PEv6_Impact_AfterTerm".Translate().Resolve(), "CC_PEv6_Impact_AfterTermBrief".Translate().Resolve(), "pev6afterterm");
            if (!blight) sheet.Add(ImpactKind.Other, -1, "CC_PEv6_Impact_AfterTurns".Translate().Resolve(), "CC_PEv6_Impact_AfterTurnsBrief".Translate().Resolve(), "pev6afterturns");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            AfterFire.Start(hc, fed);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The colony's planter at their side in the burn (the ask's lesson is its play-out): remembered for their thanks.</summary>
    public class DilemmaEffect_AfterPlanter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_AfterFire);
            if (c?.faction != null) sheet.Goodwill(c.faction, PeopleEvents6Math.PlanterGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_AfterFire);
            var st = AfterFire.StateOf(c);
            if (st != null && ctx?.a != null) st.planter = ctx.a;
        }
    }

    /// <summary>The food they asked for, carried out: marked so it isn't asked again.</summary>
    public class DilemmaEffect_AfterFed : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
        }

        public override void Apply(DilemmaContext ctx)
        {
            var st = AfterFire.StateOf(CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_AfterFire));
            if (st != null) st.fed = true;
        }
    }
}
