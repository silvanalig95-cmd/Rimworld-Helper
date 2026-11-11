using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The peoples' own camps of round 1 (Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents.xml), each a camp kind with ways of
    // its own (Camps/CampWorker.cs): the pilgrims' hostel on the road (17), the Wardens' lantern post (34), and the
    // scholars measuring an old place (19, the reading's camp). None comes on the camps' own draw (weight 0): a world
    // event, an event or an answer brings each.

    // ===================== 34. The lantern post (the Lantern Wardens) =====================

    public class LanternState : CampWorkState
    {
        public Pawn beast;
        public string kind, name;
        public bool fell, fireVisit, houndLost, hunted, cold;
        public int warnedAt = -999999, goneSince = -1, trailUntil = -1, lanternDay = -1;
        public List<Pawn> trail = new List<Pawn>();

        public override void ExposeData()
        {
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref fell, "fell");
            Scribe_Values.Look(ref fireVisit, "fireVisit");
            Scribe_Values.Look(ref houndLost, "houndLost");
            Scribe_Values.Look(ref hunted, "hunted");
            Scribe_Values.Look(ref cold, "cold");
            Scribe_Values.Look(ref warnedAt, "warnedAt", -999999);
            Scribe_Values.Look(ref goneSince, "goneSince", -1);
            Scribe_Values.Look(ref trailUntil, "trailUntil", -1);
            Scribe_Values.Look(ref lanternDay, "lanternDay", -1);
            Scribe_Collections.Look(ref trail, "trail", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                trail ??= new List<Pawn>();
                trail.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// The Wardens' lantern post: while a great beast (a legend, a nemesis, an alpha: LegendBeasts) is on the colony's land,
    /// three Wardens and two hounds camp at the edge nearest it, light the lantern every night and go out on the trail by
    /// day, watching it from a distance. They warn the colony when it comes near, ask meat for the hounds, and take the
    /// colony's best shot on the trail if asked (the hunt together: their hunters under the game's assault lord, the
    /// colonist on the game's own hunt). It can turn: the beast comes to their fire one night (the game's fight, the
    /// colony told so it may run to help), or a hound is lost on the trail. They stay until the beast falls or ten days;
    /// then the lantern goes out and its page is written.
    /// </summary>
    public class CampWorker_LanternPost : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public static LanternState StateOf(Camp c) => c?.workState as LanternState;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var beast = LegendBeasts.OnLand(map, out _);
            if (beast == null) return usual;
            var edge = LegendBeasts.EdgeToward(map, beast.Position);
            var c = CampLand.TryFindNear(map, edge, 0f, 16f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (c.workState is LanternState) return;
            var beast = LegendBeasts.OnLand(c.map, out string kind);
            c.workState = new LanternState { beast = beast, kind = kind, name = beast?.LabelShort };
        }

        private static bool Hound(Pawn p) => p != null && p.RaceProps != null && p.RaceProps.Animal;

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is LanternState st))
            {
                Started(c);
                st = StateOf(c);
                if (st == null) return;
            }
            var map = c.map;
            var beast = st.beast;
            bool here = LegendBeasts.Here(beast, map);
            // The beast fallen: the page written, and they go.
            if (!st.fell && beast != null && beast.Dead)
            {
                st.fell = true;
                Fell(c, st);
            }
            if (!st.fell && !here)
            {
                if (st.goneSince < 0) st.goneSince = Now;
                else if (Now - st.goneSince > GenDate.TicksPerDay && !st.cold)
                {
                    st.cold = true;
                    c.Note(c.Text("CC_PEv_PostCold"), MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                    c.untilTick = Mathf.Min(c.untilTick, Now + 3 * GenDate.TicksPerHour);
                }
            }
            else if (here) st.goneSince = -1;
            // Their hunters back from the trail once the hunt is over.
            if (st.trail.Count > 0 && (st.fell || !here || Now > st.trailUntil)) Recall(c, st);
            if (!here) return;
            // The lantern lit at dusk.
            if (hour == 18 && st.lanternDay != GenDate.DaysPassed)
            {
                st.lanternDay = GenDate.DaysPassed;
                var lighter = c.Here.Where(p => !Hound(p) && p.Awake()).RandomElementWithFallback();
                if (lighter != null) c.SayMoment(lighter, "lantern");
            }
            // A warning when it comes near the colony.
            var centre = CampSite.ColonyCenter(map);
            if (Now - st.warnedAt > PeopleEventsMath.WarnGapHours * GenDate.TicksPerHour && centre.IsValid && beast.Position.InHorDistOf(centre, PeopleEventsMath.NearCells))
            {
                st.warnedAt = Now;
                var warner = c.Here.FirstOrDefault(p => !Hound(p));
                if (warner != null) c.SayMoment(warner, "warn");
                ColonyCouncil.Tell.Message(NewsKind.Land, Importance.Notable, map, c.Text("CC_PEv_PostWarns"), new LookTargets(beast), MessageTypeDefOf.CautionInput);
            }
            // One night in a stay at most, the beast comes to their fire (the game's own fight).
            if (hour == 23 && !st.fireVisit && st.trail.Count == 0 && !beast.Downed && !beast.InMentalState && Rand.Chance(PeopleEventsMath.FireVisitChance))
            {
                st.fireVisit = true;
                ToTheFire(c, beast);
            }
            // One day in a stay at most, a hound lost on the trail.
            if (hour == 13 && !st.houndLost && Rand.Chance(PeopleEventsMath.HoundLostChance))
            {
                var hound = c.herd.Where(h => h != null && !h.Dead && h.Spawned && h.Map == map).RandomElementWithFallback();
                if (hound != null)
                {
                    st.houndLost = true;
                    LoseHound(c, hound);
                }
            }
        }

        /// <summary>The beast drawn to their fire in the night: it walks in on them, and the Wardens fight it off by their fire.</summary>
        private static void ToTheFire(Camp c, Pawn beast)
        {
            try
            {
                var map = c.map;
                if (!CellFinder.TryFindRandomCellNear(c.FireCell, map, 4, x => x.Standable(map) && beast.CanReach(x, PathEndMode.OnCell, Danger.Deadly), out var cell)) return;
                var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Walk;
                beast.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                ColonyCouncil.Tell.Message(NewsKind.Land, Importance.Important, map, c.Text("CC_PEv_PostFire"), new LookTargets(beast), MessageTypeDefOf.ThreatSmall);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the beast at the Wardens' fire", e);
            }
        }

        private static void LoseHound(Camp c, Pawn hound)
        {
            try
            {
                c.herd.Remove(hound);
                hound.GetLord()?.RemovePawn(hound);
                if (hound.Faction != null) LordMaker.MakeNewLord(hound.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), c.map, new List<Pawn> { hound });
                c.Note(c.Text("CC_PEv_PostHound", null, null, 0, hound.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, hound);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: a hound lost", e);
            }
        }

        /// <summary>The beast down: if they hunted it, one of them skins it where it lies (the hide for their book, the meat the colony's); the lantern goes out.</summary>
        private static void Fell(Camp c, LanternState st)
        {
            var corpse = st.beast?.Corpse;
            if (st.hunted && corpse != null && corpse.Spawned && corpse.Map == c.map && PeoplesD.SkinJob != null)
            {
                Recall(c, st);
                var skinner = c.Here.Where(p => !Hound(p) && !p.Downed).OrderBy(p => p.Position.DistanceToSquared(corpse.Position)).FirstOrDefault();
                if (skinner != null)
                {
                    var job = JobMaker.MakeJob(PeoplesD.SkinJob, corpse);
                    job.count = PeoplesDMath.SkinTicks;
                    skinner.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
            }
            if (st.hunted) FactionMemory.Note(c.faction, "campHelped", st.name);
            c.Note(c.Text(st.hunted ? "CC_PEv_PostFellTogether" : "CC_PEv_PostFell"), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            c.untilTick = Mathf.Min(c.untilTick, Now + 6 * GenDate.TicksPerHour);
        }

        /// <summary>Their hunters back into the camp from the trail.</summary>
        public static void Recall(Camp c, LanternState st)
        {
            foreach (var p in st.trail.Where(p => p != null && !p.Dead && p.Spawned && p.Map == c.map).ToList())
            {
                try
                {
                    if (p.GetLord() == c.lord) continue;
                    p.GetLord()?.RemovePawn(p);
                    if (c.LordAlive) c.lord.AddPawn(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events: the Wardens back from the trail", e);
                }
            }
            st.trail.Clear();
        }

        /// <summary>
        /// The hunt together (the colony's ask): two of them after the beast under the game's assault lord, the colony's best
        /// shot on the game's own hunt beside them, until it falls or a day goes by.
        /// </summary>
        public static bool Trail(Camp c, Pawn colonist)
        {
            var st = StateOf(c);
            var beast = st?.beast;
            if (st == null || !LegendBeasts.Here(beast, c.map) || c.faction == null) return false;
            var hunters = c.Here.Where(p => !Hound(p) && !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent) && !p.DevelopmentalStage.Juvenile()).Take(2).ToList();
            if (hunters.Count == 0) return false;
            try
            {
                foreach (var h in hunters) h.GetLord()?.RemovePawn(h);
                LordMaker.MakeNewLord(c.faction, new LordJob_AssaultThings(c.faction, new List<Thing> { beast }, 1f, false), c.map, hunters);
                st.trail = hunters;
                st.trailUntil = Now + PeopleEventsMath.TrailHours * GenDate.TicksPerHour;
                st.hunted = true;
                c.SayMoment(hunters[0], "trail");
                if (colonist != null && colonist.Spawned && !colonist.Downed && !colonist.Drafted && !colonist.InMentalState)
                {
                    if (c.map.designationManager.DesignationOn(beast, DesignationDefOf.Hunt) == null) c.map.designationManager.AddDesignation(new Designation(beast, DesignationDefOf.Hunt));
                    if (WorkGiver_HunterHunt.HasHuntingWeapon(colonist)) colonist.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Hunt, beast), JobTag.Misc);
                }
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the Wardens' trail", e);
                Recall(c, st);
                return false;
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            if (st == null || Hound(p) || !LegendBeasts.Here(st.beast, c.map) || !Rand.Chance(0.4f)) return null;
            int hour = GenLocalDate.HourOfDay(c.map);
            if (hour < 7 || hour > 17) return null;
            // Out on the trail: a spot some way off the beast, on the camp's side, to watch it from.
            var map = c.map;
            var b = st.beast.Position;
            var v = (c.center - b).ToVector3();
            v.Normalize();
            var toward = (b + new IntVec3(Mathf.RoundToInt(v.x * 18f), 0, Mathf.RoundToInt(v.z * 18f))).ClampInsideMap(map);
            if (!CellFinder.TryFindRandomCellNear(toward, map, 4, x => x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null && p.CanReach(x, PathEndMode.OnCell, Danger.Some),
                    out var cell)) return null;
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_CampTrack");
            if (job == null) return null;
            c.SayMoment(p, "track", 0.25f);
            return JobMaker.MakeJob(job, cell, st.beast);
        }

        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = StateOf(c);
            if (st == null || Hound(p) || (hour < 20 && hour > 4)) return null;
            // The lantern kept by turns: the most rested of them sits up by its light.
            var keeper = c.Here.Where(x => !Hound(x) && !x.Downed && !x.DevelopmentalStage.Juvenile()).OrderByDescending(x => x.needs?.rest?.CurLevelPercentage ?? 1f).FirstOrDefault();
            if (keeper != p || (p.needs?.rest != null && p.needs.rest.CurLevelPercentage < 0.25f)) return null;
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_CampLantern");
            var map = c.map;
            if (job == null || !CellFinder.TryFindRandomCellNear(c.FireCell, map, 3, x => x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null, out var cell)) return null;
            return JobMaker.MakeJob(job, cell, c.FireCell);
        }

        public override void Leaving(Camp c)
        {
            var st = StateOf(c);
            if (st != null) Recall(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.fell) marks.Add(st.hunted ? "together" : "fell");
            else marks.Add("cold");
            if (st.houndLost) marks.Add("hound");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{BEAST}") ? text : text.Replace("{BEAST}", StateOf(c)?.name ?? "the beast");

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.needs != "beastHere") return null;
            var st = StateOf(c);
            if (st == null || !LegendBeasts.Here(st.beast, c.map)) return "CC_PEv_BeastNotHere".Translate().Resolve();
            if (st.trail.Count > 0 || st.hunted) return "CC_CampAsk_Done".Translate().Resolve();
            return null;
        }
    }

    /// <summary>The colony's best shot goes out on the Wardens' trail with two of them (CampWorker_LanternPost.Trail).</summary>
    public class DilemmaEffect_LanternTrail : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_LanternPost);
            var st = CampWorker_LanternPost.StateOf(c);
            if (st == null) return;
            sheet.Play("CC_PEv_Impact_Trail".Translate(ctx.a?.LabelShort ?? "?", st.name ?? "?").Resolve(), "CC_PEv_Impact_TrailBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_LanternPost);
            if (c != null) CampWorker_LanternPost.Trail(c, ctx.a);
        }
    }

    /// <summary>
    /// The Lantern Wardens come to camp at the edge of the colony's land because a great beast is on it ({BEAST}: a legend,
    /// a nemesis, an alpha): the lantern post. Its letter is this event's; the camp is the camps' own from there.
    /// </summary>
    public class HappeningWorker_LanternPost : HappeningWorker_Visitors
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_LanternPost");

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Hunters) != null && LegendBeasts.OnLand(map, out _) != null
            && !CampTracker.VisitorsAt(map).Any(c => c.kind == Kind);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            ctx.pet = LegendBeasts.OnLand(ctx.map, out string kind);
            if (ctx.faction == null || ctx.pet == null) return false;
            ctx.note = kind;
            ctx.beast = ctx.pet.LabelShort;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (tracker == null || kind == null) return false;
            var usual = CampWeather.StayRange(ctx.map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            if (!CampSite.TryFind(ctx.map, out var center, hot)) return false;
            center = CampLand.SiteFor(kind, ctx.map, center, hot);
            if (!center.IsValid) return false;
            var setup = tracker.Make(ctx.map, center, CampSite.ColonyCenter(ctx.map), kind, ctx.faction, hot, null, null);
            if (setup == null) return false;
            setup.land = TravelLand.Read(ctx.map);
            var c = tracker.Start(setup);
            if (c == null)
            {
                CampTracker.Discard(setup);
                return false;
            }
            ctx.stranger = c.leader;
            ctx.spot = c.center;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.pet != null && PackAlphas.IsManhunter(ctx.pet.MentalStateDef)) tags.Add("manhunter");
        }
    }

    // ===================== 17. A hostel on the road (the pilgrim fellowship) =====================

    public class HostelState : CampWorkState
    {
        public List<Pawn> travellers = new List<Pawn>();
        public int nextTraveller = -1, breadDay = -1, quarrelDay = -1, seen;
        public bool bread, asked, quarrelled;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref travellers, "travellers", LookMode.Reference);
            Scribe_Values.Look(ref nextTraveller, "nextTraveller", -1);
            Scribe_Values.Look(ref breadDay, "breadDay", -1);
            Scribe_Values.Look(ref quarrelDay, "quarrelDay", -1);
            Scribe_Values.Look(ref seen, "seen");
            Scribe_Values.Look(ref bread, "bread");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref quarrelled, "quarrelled");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                travellers ??= new List<Pawn>();
                travellers.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// The pilgrims' hostel on the road: in a holy year or a great pilgrimage (the world event's beat brings it), two or three
    /// of the fellowship raise a long hut of bedrolls by the road, the shrine's flame and a bread table, for ten to fifteen
    /// days. Travellers of every people at peace walk in for a night or two and walk on, so the colony sees the region's
    /// faces and hears their news (a place they speak of is marked as heard: World/HeardPlaces.cs). The hostel asks for
    /// bread each morning (granted once, food carried out each morning after) and invites the colony to the evening
    /// blessing. It can turn: the cough goes round the crowded hut (the pilgrims' cough, through the harm budget), or two
    /// travellers of feuding peoples quarrel at the fire (the camp brawl's rate, the game's own social fight). The hut stays
    /// standing as a shelter for the road.
    /// </summary>
    public class CampWorker_Hostel : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public static HostelState StateOf(Camp c) => c?.workState as HostelState;

        /// <summary>Road terrain: what the game lays its roads with.</summary>
        private static bool RoadTerrain(TerrainDef t) =>
            t != null && (t.defName == "PackedDirt" || t.defName == "Gravel" || t.defName == "BrokenAsphalt" || t.defName == "Concrete" || t.defName.StartsWith("Flagstone", StringComparison.Ordinal));

        /// <summary>By the road through the colony's land, where there is one; else the camp's usual spot.</summary>
        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            try
            {
                var roads = (map.TileInfo as SurfaceTile)?.Roads;
                if (roads.NullOrEmpty()) return usual;
                var colony = CampSite.ColonyCenter(map);
                var road = map.AllCells.Where(c => (c.x + c.z) % 7 == 0 && RoadTerrain(c.GetTerrain(map)) && !c.Fogged(map) && (!colony.IsValid || !c.InHorDistOf(colony, 30f)))
                    .OrderBy(c => colony.IsValid ? c.DistanceToSquared(colony) : 0).Take(6).ToList();
                foreach (var r in road)
                {
                    var c = CampLand.TryFindNear(map, r, 4f, 14f, shade);
                    if (c.IsValid) return c;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the hostel by the road", e);
            }
            return usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is HostelState)) c.workState = new HostelState { nextTraveller = Now + GenDate.TicksPerDay / 2 };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is HostelState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            st.travellers.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != map || p.Faction == null || p.HostileTo(Faction.OfPlayer));
            // The bread asked for once, on their first morning.
            if (!st.asked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11)
            {
                var ask = c.AskByKey("bread");
                if (ask != null && c.AskBlocked(ask) == null && ColonyCouncil.Tell.CanAsk && c.Request(ask)) st.asked = true;
            }
            // Bread each morning, carried out to the hostel's table, once granted.
            if (st.bread && hour == PeopleEventsMath.BreadHour && st.breadDay != GenDate.DaysPassed)
            {
                st.breadDay = GenDate.DaysPassed;
                Bread(c, st);
            }
            // Travellers of the region's peoples, walking in for a night or two.
            if (hour >= 8 && hour <= 19 && Now >= st.nextTraveller && st.travellers.Count < PeopleEventsMath.TravellersMax)
            {
                st.nextTraveller = Now + Rand.RangeInclusive(PeopleEventsMath.TravellerGapMin, PeopleEventsMath.TravellerGapMax) * GenDate.TicksPerHour;
                Traveller(c, st);
            }
            // Two of feuding peoples at the evening fire, once a stay: words, and the camp brawl's rate that it comes to blows.
            if (!st.quarrelled && hour >= 19 && hour <= 21 && st.quarrelDay != GenDate.DaysPassed)
            {
                st.quarrelDay = GenDate.DaysPassed;
                Quarrel(c, st);
            }
        }

        /// <summary>The morning's bread: meals if the colony has them, else raw food, carried out to the hostel.</summary>
        private static void Bread(Camp c, HostelState st)
        {
            var map = c.map;
            int n = PeopleEventsMath.Bread(c.Here.Count + st.travellers.Count);
            string what = PlayCarry.Have(map, "meals", null) >= n ? "meals" : "rawfood";
            var carry = new DilemmaEffect_CarryOut
            {
                what = what, count = what == "meals" ? n : n * 10, to = PlaySpot.Camp, who = PlayWho.None, steward = false,
                label = "CC_PEv_BreadLabel".Translate(c.Label.Named("NAME")).Resolve()
            };
            var ctx = new DilemmaContext { map = map, faction = c.faction, stranger = c.leader };
            if (carry.Unavailable(ctx) != null)
            {
                c.AddTension(4f);
                c.Note(c.Text("CC_PEv_HostelNoBread"), MessageTypeDefOf.NegativeEvent, Importance.Flavor);
                return;
            }
            // apart: a colonist carries the morning's bread over to the hostel, where one of the fellowship takes it.
            carry.Apply(ctx);
            c.AddTension(-1f);
        }

        /// <summary>A traveller (or two of one people) walks in to the hostel from the road, with their people's news.</summary>
        private static void Traveller(Camp c, HostelState st)
        {
            var map = c.map;
            var f = Arrivals.Friendly(x => x != c.faction && Arrivals.Usable(x));
            if (f == null) return;
            try
            {
                int n = Rand.Chance(0.3f) ? 2 : 1;
                var list = new List<Pawn>();
                for (int i = 0; i < n; i++)
                {
                    var p = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(18f, 65f));
                    if (p != null) list.Add(p);
                }
                if (list.Count == 0) return;
                float hours = Rand.Range(PeopleEventsMath.TravellerStayMin, PeopleEventsMath.TravellerStayMax);
                if (Arrivals.Visit(list, map, f, c.FireCell, hours) == null)
                {
                    foreach (var p in list) Arrivals.Discard(p);
                    return;
                }
                st.travellers.AddRange(list);
                st.seen += list.Count;
                // Their news of the region: a line of what their people know, and the place it names marked as heard.
                string news = null;
                try
                {
                    var town = WorldSituationWorker.NearestTown(f, map);
                    var facts = ColonyCouncil.Facts.For(FactScene.Event(map, f, town, list[0]));
                    if (facts != null && facts.news.Count > 0)
                    {
                        int i = Rand.Range(0, facts.news.Count);
                        news = facts.news[i];
                        string fact = i < facts.newsFacts.Count ? facts.newsFacts[i] : null;
                        OutsiderTalk.Heard(list[0], c.leader, news, fact != null ? new List<string> { fact } : null);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events: a traveller's news", e);
                }
                string text = c.Text(news.NullOrEmpty() ? "CC_PEv_HostelTraveller" : "CC_PEv_HostelTravellerNews", list[0], null, list.Count, f.Name);
                if (!news.NullOrEmpty()) text += " " + news;
                News.Post(text, new LookTargets(list[0]), NewsKind.World);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: a traveller at the hostel", e);
            }
        }

        /// <summary>Two travellers of feuding peoples at the fire: hard words, and at the camp brawl's rate the game's own social fight.</summary>
        private static void Quarrel(Camp c, HostelState st)
        {
            var map = c.map;
            var at = st.travellers.Where(p => p.Spawned && p.Awake() && !p.Downed && p.Position.InHorDistOf(c.FireCell, 12f)).ToList();
            foreach (var a in at)
                foreach (var b in at)
                {
                    if (a == b || a.Faction == b.Faction || a.Faction == null || b.Faction == null) continue;
                    bool war = a.Faction.HostileTo(b.Faction);
                    int gw = a.Faction.GoodwillWith(b.Faction);
                    if (!PeopleEventsMath.Feuding(gw, war) || !CampBrawls.Fit(a, c.FireCell, 12f) || !CampBrawls.Fit(b, c.FireCell, 12f)) continue;
                    st.quarrelled = true;
                    c.SayMoment(a, "quarrel");
                    if (!Harm.Roll(HarmKinds.CampBrawl, HarmTwoMath.CampBrawlChance(false, 1f)))
                    {
                        c.Note(c.Text("CC_PEv_HostelWords", a, null, 0, b.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Flavor, a);
                        return;
                    }
                    try
                    {
                        a.interactions?.StartSocialFight(b);
                        c.Note(c.Text("CC_PEv_HostelBrawl", a, null, 0, b.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, a);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events: a quarrel at the hostel", e);
                    }
                    return;
                }
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st != null && st.seen >= 3) marks.Add("faces");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{GUESTS}") ? text : text.Replace("{GUESTS}", (StateOf(c)?.seen ?? 0).ToString());

        /// <summary>Granted: the bread carried out each morning from now (the first this morning).</summary>
        public static void Granted(Camp c)
        {
            var st = StateOf(c);
            if (st == null) return;
            st.bread = true;
            st.breadDay = GenDate.DaysPassed;
            Bread(c, st);
        }
    }

    /// <summary>The hostel's bread granted: carried out this morning and each morning after.</summary>
    public class DilemmaEffect_HostelBread : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PEv_Impact_Bread".Translate().Resolve(), "CC_PEv_Impact_BreadBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_Hostel);
            if (c != null) CampWorker_Hostel.Granted(c);
        }
    }

    /// <summary>The cough going round the crowded hut at the blessing: the one who goes may catch it (the pilgrims' cough, through the harm budget), shown.</summary>
    public class DilemmaEffect_HostelCough : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Harm.On || ctx?.a == null) return;
            sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_Cough".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv_Impact_CoughBrief".Translate().Resolve(), "pevcough");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var cold = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_HeadCold");
            if (a == null || a.Dead || cold == null || !AilmentTracker.On) return;
            if (!Harm.Roll(a, HarmKinds.PilgrimCough, PeopleEventsMath.HostelCough, HurtClass.Mild)) return;
            if (Harm.Ail(a, cold, Mathf.Max(0.1f, cold.initialSeverity), HarmKinds.PilgrimCough, HurtClass.Mild) == null) return;
            ColonyCouncil.Tell.Message(NewsKind.Health, Importance.Notable, a.MapHeld, "CC_PEv_HostelCough".Translate(a.Named("PAWN")).Resolve(), a.Spawned ? new LookTargets(a) : LookTargets.Invalid,
                MessageTypeDefOf.NegativeHealthEvent);
        }
    }
}
