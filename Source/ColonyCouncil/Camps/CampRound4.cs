using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content round's fourth part, its camps (docs/content-round-4-camps-events.md, R4-C1 to R4-C6; Defs/CampDefs/
    // Rimfolk_CampKinds_Round4.xml): woodcarvers, fire-watchers, wildfowlers, snow-carvers, cheesemakers and instrument-makers.
    // Each is a camp kind with ways of its own (Camps/CampWorker.cs), its state kept with the camp (a CampWorkState of a few
    // values and single references, no collection); the pure rules are Happenings/Round4CampsEventsMath.cs. What they make,
    // buy, are given or take goes into their store like any camp's, and is shared out among their packs when they leave;
    // what the colony buys of their own making is set out at the edge of their camp nearest the colony, for the colony's
    // haulers to fetch, or carried to the gate by one of them.

    /// <summary>What the round's camps share: their kinds, a piece of their own making, and setting it out for the colony.</summary>
    public static class CampRound4
    {
        public const string Woodcarvers = "Rimfolk_Camp_Woodcarvers", FireWatchers = "Rimfolk_Camp_FireWatchers", Wildfowlers = "Rimfolk_Camp_Wildfowlers",
            SnowCarvers = "Rimfolk_Camp_SnowCarvers", Cheesemakers = "Rimfolk_Camp_Cheesemakers", InstrumentMakers = "Rimfolk_Camp_InstrumentMakers";

        public static readonly string[] Kinds = { Woodcarvers, FireWatchers, Wildfowlers, SnowCarvers, Cheesemakers, InstrumentMakers };

        public static int Now => Find.TickManager.TicksGame;

        /// <summary>The one of them best at <paramref name="skill"/>, up and about, else whoever speaks for them.</summary>
        public static Pawn Best(Camp c, SkillDef skill) =>
            CampRound3.Up(c).Where(p => p != c.trader).OrderByDescending(p => p.skills?.GetSkill(skill)?.Level ?? 0).FirstOrDefault() ?? CampRound3.Up(c).FirstOrDefault() ?? c.leader;

        /// <summary>
        /// A piece of their own making (a carving, an instrument): of <paramref name="stuff"/> where it can be, of a quality by the
        /// roll, its art by <paramref name="maker"/> and titled <paramref name="title"/> when given, packed (minified) to be carried.
        /// </summary>
        public static Thing Made(ThingDef def, ThingDef stuff, Pawn maker, string title, float roll)
        {
            if (def == null) return null;
            ThingDef s = null;
            if (def.MadeFromStuff)
            {
                var allowed = GenStuff.AllowedStuffsFor(def).ToList();
                s = stuff != null && allowed.Contains(stuff) ? stuff : GenStuff.DefaultStuffFor(def);
            }
            var t = ThingMaker.MakeThing(def, s);
            var quality = t.TryGetComp<CompQuality>();
            var art = t.TryGetComp<CompArt>();
            if (quality != null) quality.SetQuality((QualityCategory)Round4CampsEventsMath.CarvingQuality(roll), ArtGenerationContext.Outsider);
            else art?.InitializeArt(ArtGenerationContext.Outsider);
            if (art != null)
            {
                if (maker != null) art.JustCreatedBy(maker);
                if (!title.NullOrEmpty()) art.Title = title;
            }
            return def.Minifiable ? MinifyUtility.MakeMinified(t) : t;
        }

        /// <summary>Set out at the edge of their camp nearest the colony (their share spot), the colony's to fetch.</summary>
        public static Thing SetOut(Camp c, Thing t)
        {
            if (c?.map == null || t == null) return null;
            var at = c.share.IsValid && c.share.InBounds(c.map) ? c.share : c.FireCell;
            if (!GenPlace.TryPlaceThing(t, at, c.map, ThingPlaceMode.Near, out var put)) return null;
            put?.SetForbidden(false, false);
            return put ?? t;
        }

        /// <summary>Open ground near <paramref name="at"/> on their side, standable, with nothing built and nobody there.</summary>
        public static IntVec3 Open(Camp c, IntVec3 at, int radius, Func<IntVec3, bool> also = null)
        {
            var map = c?.map;
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            return CellFinder.TryFindRandomCellNear(at, map, radius, x => x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null && x.GetFirstItem(map) == null
                                                                          && !x.Fogged(map) && c.ClearOfColony(x) && (also == null || also(x)), out var cell)
                ? cell
                : IntVec3.Invalid;
        }
    }

    // ===================== R4-C1. Woodcarvers =====================

    public class R4KCarveState : CampWorkState
    {
        /// <summary>The one of the colony sent to sit for a carving, the fee, until when they wait, since when they sat; carved, sold.</summary>
        public Pawn sitter;
        public int fee, sitUntil = -1, withFrom = -1, carved, sold;

        public override void ExposeData()
        {
            Scribe_References.Look(ref sitter, "sitter");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref sitUntil, "sitUntil", -1);
            Scribe_Values.Look(ref withFrom, "withFrom", -1);
            Scribe_Values.Look(ref carved, "carved");
            Scribe_Values.Look(ref sold, "sold");
        }
    }

    /// <summary>
    /// Woodcarvers (R4-C1): a family of carvers camps by the colony in the warm months where the land has trees, and by day
    /// one or two of them carve at a block by their fire. Someone of the colony may sit for a carving (their ask, a walk to
    /// their fire): once that one has sat with the carver an hour, a small wooden sculpture with their name in its title is
    /// set out at the edge of the camp for the colony to fetch. Or the colony buys one of their carvings, set out the same way.
    /// </summary>
    public class CampWorker_R4KWoodcarvers : CampWorker
    {
        public const string SitAsk = "sit";

        public static R4KCarveState StateOf(Camp c) => c?.workState as R4KCarveState;

        private static R4KCarveState Ensure(Camp c)
        {
            if (!(c.workState is R4KCarveState)) c.workState = new R4KCarveState();
            return (R4KCarveState)c.workState;
        }

        public static ThingDef Sculpture => DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");

        /// <summary>Grown wild trees on the land, out of the colony's home ground and well away from its middle (counted up to <paramref name="most"/>).</summary>
        public static int WildTrees(Map map, int most)
        {
            if (map == null) return 0;
            var mid = CampSite.ColonyCenter(map);
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
            {
                if (!(t is Plant p) || p.def.plant == null || !p.def.plant.IsTree || p.LifeStage != PlantLifeStage.Mature || map.areaManager.Home[p.Position]) continue;
                if (mid.IsValid && p.Position.InHorDistOf(mid, 20f)) continue;
                if (++n >= most) break;
            }
            return n;
        }

        public override bool FitsMap(Map map) => WildTrees(map, Round4CampsEventsMath.CarversTreesFrom) >= Round4CampsEventsMath.CarversTreesFrom;

        public override void Started(Camp c) => Ensure(c);

        public override void Hourly(Camp c, int hour)
        {
            var st = Ensure(c);
            if (st.sitter == null) return;
            if (st.sitter.Dead || CampRound4.Now > st.sitUntil && st.withFrom < 0)
            {
                // Never sat down with them: the fee goes back.
                CampRound3.Refund(c, st.fee, "CC_R4K_Carve_Refund", st.sitter);
                st.sitter = null;
                st.withFrom = -1;
                return;
            }
            bool first = st.withFrom < 0;
            var sitting = CampRound3.Sit(c, st.sitter, ref st.withFrom, Round4CampsEventsMath.SitHours, out var carver);
            if (first && sitting == CampRound3.Sitting.Sitting && carver != null)
            {
                // together: the colonist walked to their fire (the ask's visit) and sits within talking range of the carver (Together.Can).
                c.SayMoment(carver, "sit");
                var at = CampRound3.ByFire(c, carver, 2);
                var job = CampRound2.Work("Rimfolk_R4K_CampCarveWood", at.IsValid ? at : carver.Position, new LocalTargetInfo(st.sitter));
                if (job != null) carver.jobs.StartJob(job, JobCondition.InterruptOptional, null, true, true);
            }
            if (sitting != CampRound3.Sitting.Done) return;
            Carve(c, st, st.sitter, carver);
            st.sitter = null;
            st.withFrom = -1;
        }

        private static void Carve(Camp c, R4KCarveState st, Pawn sitter, Pawn carver)
        {
            Thing piece;
            try
            {
                var made = CampRound4.Made(Sculpture, ThingDefOf.WoodLog, carver, "CC_R4K_Carve_Title".Translate(sitter.LabelShort.Named("PAWN")).Resolve(), Rand.Value);
                piece = CampRound4.SetOut(c, made);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: the carving", e);
                return;
            }
            if (piece == null) return;
            st.carved++;
            if (carver != null) c.SayMoment(carver, "carved");
            c.Warm(sitter, 1);
            CouncilGameComponent.GiveThought(sitter, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R4K_SatForCarving"));
            c.Note(c.Text("CC_R4K_Carve_Done", carver, sitter), MessageTypeDefOf.PositiveEvent, Importance.Notable, piece, true);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // A block by the fire, worked by turns; the one carving for a sitter is kept at it by Hourly.
            if (p == c.trader || !Rand.Chance(0.5f)) return null;
            var at = CampRound3.ByFire(c, p);
            if (!at.IsValid) return null;
            c.SayMoment(p, "carve", 0.12f);
            return CampRound2.Work("Rimfolk_R4K_CampCarveWood", at, new LocalTargetInfo(c.FireCell));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.carved > 0) marks.Add("carved");
            else if (st.sold > 0) marks.Add("sold");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != SitAsk) return null;
            var st = StateOf(c);
            return st?.sitter != null && !st.sitter.Dead && CampRound4.Now <= st.sitUntil ? "CC_R4K_Carve_Busy".Translate().Resolve() : null;
        }

        /// <summary>The one sent over to sit for a carving (the ask's visit takes them there), and the fee paid.</summary>
        public static void Sent(Camp c, Pawn a, int fee)
        {
            var st = StateOf(c);
            if (st == null || a == null) return;
            st.sitter = a;
            st.fee = Mathf.Max(0, fee);
            st.withFrom = -1;
            st.sitUntil = CampRound4.Now + (int)(Round4CampsEventsMath.SitWaitDays * GenDate.TicksPerDay);
        }
    }

    /// <summary>Someone of the colony sent to sit for a carving (the ask's walk over is its visit; its silver the fee).</summary>
    public class DilemmaEffect_R4KSit : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null && CampOnMap.For(ctx)?.kind?.Worker is CampWorker_R4KWoodcarvers)
                sheet.Play("CC_R4K_Carve_Impact".Translate(ctx.a.LabelShort).Resolve(), "CC_R4K_Carve_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_R4KWoodcarvers.Sent(CampOnMap.For(ctx), ctx?.a, ctx?.count ?? 0);
    }

    /// <summary>One of the woodcarvers' carvings bought: made by their best carver and set out at the edge of their camp for the colony.</summary>
    public class DilemmaEffect_R4KBuyCarving : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => CampOnMap.For(ctx) == null || CampWorker_R4KWoodcarvers.Sculpture == null ? "CC_CampAsk_NotNow".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (CampOnMap.For(ctx) == null || CampWorker_R4KWoodcarvers.Sculpture == null) return;
            sheet.Goods(1, "CC_R4K_Carve_Piece".Translate().Resolve(), 1, "r4kcarving");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return;
            var carver = CampRound4.Best(c, SkillDefOf.Artistic);
            Thing piece;
            try
            {
                piece = CampRound4.SetOut(c, CampRound4.Made(CampWorker_R4KWoodcarvers.Sculpture, ThingDefOf.WoodLog, carver, null, Rand.Value));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: a carving bought", e);
                return;
            }
            if (piece == null) return;
            var st = CampWorker_R4KWoodcarvers.StateOf(c);
            if (st != null) st.sold++;
            if (carver != null) c.SayMoment(carver, "sold");
            c.Note(c.Text("CC_R4K_Carve_Sold", carver), MessageTypeDefOf.PositiveEvent, Importance.Notable, piece, true);
        }
    }

    // ===================== R4-C2. Fire-watchers =====================

    public class R4KFireState : CampWorkState
    {
        /// <summary>The firebreak asked for; plants cut for it; fires beaten out; running to one now; the day it was last told.</summary>
        public bool firebreak, running, breakDone;
        public int cut, beaten, toldDay = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref firebreak, "firebreak");
            Scribe_Values.Look(ref running, "running");
            Scribe_Values.Look(ref breakDone, "breakDone");
            Scribe_Values.Look(ref cut, "cut");
            Scribe_Values.Look(ref beaten, "beaten");
            Scribe_Values.Look(ref toldDay, "toldDay", -1);
        }
    }

    /// <summary>
    /// Fire-watchers (R4-C2): in a hot, dry summer three or four camp on the colony's land, and by day one of them stands at the
    /// edge of their camp looking out over the land. When a fire burns in the open within reach of their camp (the game's own
    /// fire), two of them run to it and beat it out (the game's own beating of a fire). Asked, they cut a firebreak round the
    /// colony's biggest field: the dry wild grass and bushes in a strip just outside it, by the working hours (the game's own
    /// cutting), never a tree, a crop or anything in a zone.
    /// </summary>
    public class CampWorker_R4KFireWatchers : CampWorker
    {
        public const string BreakAsk = "firebreak";

        public static R4KFireState StateOf(Camp c) => c?.workState as R4KFireState;

        private static R4KFireState Ensure(Camp c)
        {
            if (!(c.workState is R4KFireState)) c.workState = new R4KFireState();
            return (R4KFireState)c.workState;
        }

        private static readonly string[] WetLands = { "tundra", "swamp", "jungle" };

        public override bool FitsMap(Map map) =>
            map != null && Round4CampsEventsMath.FireWatchFits(map.mapTemperature.OutdoorTemp, (map.weatherManager?.curWeather?.rainRate ?? 0f) > 0.1f, WetLands.Any(Lands.Of(map).Contains));

        public override void Started(Camp c) => Ensure(c);

        /// <summary>Fires burning in the open within their reach, nearest their fire first (not on anyone, not under a roof).</summary>
        public static List<Fire> Fires(Camp c)
        {
            var map = c?.map;
            if (map == null) return new List<Fire>();
            var mid = c.FireCell;
            return map.listerThings.ThingsOfDef(ThingDefOf.Fire).OfType<Fire>()
                .Where(f => f.Spawned && f.parent == null && !f.Position.Roofed(map) && f.Position.InHorDistOf(mid, Round4CampsEventsMath.FireReach))
                .OrderBy(f => f.Position.DistanceToSquared(mid)).ToList();
        }

        /// <summary>The colony's biggest field.</summary>
        public static Zone_Growing Field(Map map) =>
            map?.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count >= 9).OrderByDescending(z => z.Cells.Count).FirstOrDefault();

        /// <summary>A wild plant (no tree, no crop, nothing in a zone) in the strip just outside the field, nearest <paramref name="from"/>.</summary>
        public static Plant NextInBreak(Map map, Zone_Growing field, IntVec3 from, Pawn cutter, HashSet<Plant> not = null)
        {
            if (map == null || field == null) return null;
            var inField = new HashSet<IntVec3>(field.Cells);
            Plant best = null;
            int bestD = int.MaxValue;
            foreach (var c in field.Cells)
            {
                foreach (var x in GenRadial.RadialCellsAround(c, Round4CampsEventsMath.BreakWidth + 0.5f, false))
                {
                    if (inField.Contains(x) || !x.InBounds(map) || x.Fogged(map) || x.Roofed(map) || x.GetZone(map) != null) continue;
                    // Dry grass and bushes only: no tree, nothing that gives a harvest (a crop, a berry bush, wild healroot).
                    if (!(x.GetPlant(map) is Plant p) || p.def.plant == null || p.def.plant.IsTree || p.def.plant.harvestedThingDef != null) continue;
                    if (map.designationManager.DesignationOn(p) != null || x.GetEdifice(map) != null || not != null && not.Contains(p)) continue;
                    int d = x.DistanceToSquared(from);
                    if (d >= bestD || cutter != null && !cutter.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;
                    best = p;
                    bestD = d;
                }
            }
            return best;
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = Ensure(c);
            var map = c.map;
            // Not into a fight: while there is danger on the land they keep to their camp.
            if (MapFacts.ThreatOn(map)) return;
            var fires = Fires(c);
            var up = CampRound3.Up(c).Where(p => p != c.trader).ToList();
            if (fires.Count > 0)
            {
                // Two of them run to the nearest fire and beat it out.
                int sent = up.Count(p => p.CurJobDef == JobDefOf.BeatFire);
                foreach (var p in up.Where(p => p.CurJobDef != JobDefOf.BeatFire).OrderBy(p => p.Position.DistanceToSquared(fires[0].Position)))
                {
                    if (sent >= Round4CampsEventsMath.Beaters) break;
                    var f = fires.FirstOrDefault(x => p.CanReach(x, PathEndMode.Touch, Danger.Some));
                    if (f == null) continue;
                    try
                    {
                        var job = JobMaker.MakeJob(JobDefOf.BeatFire, f);
                        job.locomotionUrgency = LocomotionUrgency.Jog;
                        p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("round 4 camps: the fire-watchers run", e);
                        return;
                    }
                    if (sent == 0) c.SayMoment(p, "fire");
                    sent++;
                }
                if (sent > 0 && !st.running)
                {
                    st.running = true;
                    if (st.toldDay != GenDate.DaysPassed)
                    {
                        st.toldDay = GenDate.DaysPassed;
                        c.Note(c.Text("CC_R4K_Fire_Running", up.FirstOrDefault(), null, 0, StorytellerEvents.Direction(map, fires[0].Position)), MessageTypeDefOf.NeutralEvent, Importance.Notable, fires[0], true);
                    }
                }
                return;
            }
            if (st.running)
            {
                // Out: told when they've put it out.
                st.running = false;
                st.beaten++;
                var p = up.FirstOrDefault();
                if (p != null) c.SayMoment(p, "out");
                c.Note(c.Text("CC_R4K_Fire_Out", p), MessageTypeDefOf.PositiveEvent, Importance.Notable, p, false);
            }
            if (st.firebreak && !st.breakDone) Firebreak(c, st, hour, up);
        }

        /// <summary>The firebreak by the working hours: two of them cutting the wild plants in the strip outside the field, one after another.</summary>
        private static void Firebreak(Camp c, R4KFireState st, int hour, List<Pawn> up)
        {
            if (!Round4CampsEventsMath.BreakHour(hour)) return;
            var map = c.map;
            var field = Field(map);
            int at = up.Count(p => p.CurJobDef == JobDefOf.CutPlant);
            var taken = new HashSet<Plant>();
            foreach (var p in up.Where(p => p.CurJobDef != JobDefOf.CutPlant && p.CurJobDef != JobDefOf.BeatFire && !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)))
            {
                if (at >= 2 || st.cut >= Round4CampsEventsMath.BreakMost) break;
                // A few plants each, one after another along the strip (the game's own cutting, queued).
                var from = p.Position;
                int given = 0;
                for (int i = 0; i < 3 && st.cut < Round4CampsEventsMath.BreakMost; i++)
                {
                    var plant = NextInBreak(map, field, from, p, taken);
                    if (plant == null) break;
                    taken.Add(plant);
                    try
                    {
                        var job = JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                        if (given == 0) p.jobs.StartJob(job, JobCondition.InterruptOptional, null, true, true);
                        else p.jobs.jobQueue.EnqueueLast(job);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("round 4 camps: the firebreak", e);
                        return;
                    }
                    from = plant.Position;
                    given++;
                    st.cut++;
                }
                if (given == 0) break;
                at++;
                c.SayMoment(p, "break", 0.25f);
            }
            if (st.cut >= Round4CampsEventsMath.BreakMost || NextInBreak(map, field, c.FireCell, null, taken) == null)
            {
                st.breakDone = true;
                c.Note(c.Text("CC_R4K_Fire_BreakDone", up.FirstOrDefault(), null, st.cut, field?.label ?? ""), MessageTypeDefOf.PositiveEvent, Importance.Notable, up.FirstOrDefault(), false);
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // One of them at the edge of their camp, looking out over the land.
            if (p != (c.leader ?? p) || !Rand.Chance(0.45f) || c.map == null) return null;
            var mid = CampSite.ColonyCenter(c.map);
            var away = mid.IsValid ? c.center + new IntVec3(Math.Sign(c.center.x - mid.x) * 5, 0, Math.Sign(c.center.z - mid.z) * 5) : c.center;
            var at = CampRound2.Near(c, p, away.ClampInsideMap(c.map), 4);
            if (!at.IsValid) return null;
            c.SayMoment(p, "lookout", 0.2f);
            var look = (at + (at - c.center)).ClampInsideMap(c.map);
            return CampRound2.Work("Rimfolk_R4K_CampLookout", at, new LocalTargetInfo(look));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.beaten > 0) marks.Add("beaten");
            else if (st.cut > 0) marks.Add("break");
        }

        public override string Fill(Camp c, string text) => text?.Replace("{FIELD}", Field(c?.map)?.label ?? "CC_R4K_Fire_TheField".Translate().Resolve());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != BreakAsk) return null;
            var st = StateOf(c);
            if (st != null && st.firebreak) return "CC_R4K_Fire_Busy".Translate().Resolve();
            var field = Field(c?.map);
            return field == null || NextInBreak(c.map, field, c.FireCell, null) == null ? "CC_R4K_Fire_NoField".Translate().Resolve() : null;
        }
    }

    /// <summary>The fire-watchers asked to cut a firebreak round the colony's biggest field (their ask).</summary>
    public class DilemmaEffect_R4KFirebreak : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var field = CampWorker_R4KFireWatchers.Field(ctx?.map);
            if (field == null || !(CampOnMap.For(ctx)?.kind?.Worker is CampWorker_R4KFireWatchers)) return;
            sheet.Play("CC_R4K_Fire_BreakImpact".Translate(field.label, Round4CampsEventsMath.BreakMost).Resolve(), "CC_R4K_Fire_BreakImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var st = CampWorker_R4KFireWatchers.StateOf(CampOnMap.For(ctx));
            if (st != null) st.firebreak = true;
        }
    }

    // ===================== R4-C3. Wildfowlers =====================

    public class R4KFowlState : CampWorkState
    {
        /// <summary>The water by their camp, the kind of bird, the flock brought in; the day of the last bird taken, how many.</summary>
        public IntVec3 water = IntVec3.Invalid;
        public string kind;
        public bool flock;
        public int shotDay = -1, shot;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref water, "water", IntVec3.Invalid);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref flock, "flock");
            Scribe_Values.Look(ref shotDay, "shotDay", -1);
            Scribe_Values.Look(ref shot, "shot");
        }
    }

    /// <summary>
    /// Wildfowlers (R4-C3): in autumn and winter two to four fowlers camp by open water on the colony's land, and a flock of wild
    /// geese or ducks comes in with them and keeps about the water by their camp (Vanilla Animals Expanded's wild goose, else the
    /// game's goose or duck). By day they set out their decoys at the water's edge, and at dawn one of them goes after a bird (the
    /// game's own hunt; their kill, dressed at their camp). The colony may buy a share of their birds or learn their calls.
    /// </summary>
    public class CampWorker_R4KWildfowlers : CampWorker
    {
        public static readonly string[] Fowl = { "AEXP_WildGoose", "Goose", "Duck" };

        public static R4KFowlState StateOf(Camp c) => c?.workState as R4KFowlState;

        private static R4KFowlState Ensure(Camp c)
        {
            if (!(c.workState is R4KFowlState)) c.workState = new R4KFowlState();
            return (R4KFowlState)c.workState;
        }

        public static PawnKindDef Kind(Map map) => map == null ? null : Round3Map.Kind(map, Fowl);

        /// <summary>The map's spot of open water clear of the colony (kept a day: a look over the whole land).</summary>
        private static readonly Dictionary<int, (int tick, IntVec3 cell)> spots = new Dictionary<int, (int, IntVec3)>();
        private static Game spotsGame;

        private static bool Water(Map map, IntVec3 c) => c.InBounds(map) && c.GetTerrain(map) is TerrainDef t && t.IsWater && t.passability != Traversability.Impassable;

        /// <summary>A cell of open water with enough water round it, clear of the colony; invalid when the land has none.</summary>
        public static IntVec3 WaterSpot(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            if (spotsGame != Current.Game)
            {
                spots.Clear();
                spotsGame = Current.Game;
            }
            int now = CampRound4.Now;
            if (spots.TryGetValue(map.uniqueID, out var s) && now - s.tick >= 0 && now - s.tick < GenDate.TicksPerDay) return s.cell;
            var found = IntVec3.Invalid;
            var mid = CampSite.ColonyCenter(map);
            int best = int.MaxValue;
            for (int x = 6; x < map.Size.x - 6; x += 7)
                for (int z = 6; z < map.Size.z - 6; z += 7)
                {
                    var c = new IntVec3(x, 0, z);
                    if (!Water(map, c) || c.Fogged(map) || map.areaManager.Home[c]) continue;
                    if (mid.IsValid && c.InHorDistOf(mid, 30f)) continue;
                    int wet = GenRadial.RadialCellsAround(c, 4f, true).Count(y => Water(map, y));
                    if (wet < Round4CampsEventsMath.WaterCellsFrom) continue;
                    int d = mid.IsValid ? c.DistanceToSquared(mid) : 0;
                    if (d >= best) continue;
                    best = d;
                    found = c;
                }
            if (spots.Count > 30) spots.Clear();
            spots[map.uniqueID] = (now, found);
            return found;
        }

        public override bool FitsMap(Map map) => Kind(map) != null && WaterSpot(map).IsValid;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var w = WaterSpot(map);
            if (!w.IsValid) return usual;
            var c = CampLand.TryFindNear(map, w, 6f, 24f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            var st = Ensure(c);
            if (!st.water.IsValid) st.water = WaterSpot(c.map);
        }

        /// <summary>The flock: wild birds of their kind about the water by the camp.</summary>
        public static List<Pawn> Flock(Camp c, R4KFowlState st)
        {
            var map = c?.map;
            var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(st?.kind ?? "");
            if (map == null || def == null || !st.water.IsValid) return new List<Pawn>();
            return map.mapPawns.AllPawnsSpawned.Where(p => p.kindDef == def && p.Faction == null && !p.Dead && !p.Downed && p.Position.InHorDistOf(st.water, 40f)).ToList();
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = Ensure(c);
            var map = c.map;
            if (!st.water.IsValid) st.water = WaterSpot(map);
            if (!st.water.IsValid || !c.Settled) return;
            if (!st.flock)
            {
                // The flock comes in with them, to the water's edge by their camp.
                st.flock = true;
                var kind = Kind(map);
                if (kind == null) return;
                st.kind = kind.defName;
                int n = Rand.RangeInclusive(Round4CampsEventsMath.FlockMin, Round4CampsEventsMath.FlockMax);
                for (int i = 0; i < n; i++)
                {
                    var at = Round2Map.Near(map, st.water, Round4CampsEventsMath.FlockRange, null);
                    if (at.IsValid) Round3Map.Spawn(kind, at, map);
                }
                var p = CampRound3.Up(c).FirstOrDefault();
                if (p != null) c.SayMoment(p, "decoy");
                return;
            }
            // They keep about the water.
            foreach (var b in Flock(c, st))
            {
                if (b.CurJobDef == JobDefOf.Goto || b.CurJobDef == JobDefOf.GotoWander || b.InMentalState) continue;
                if (!b.Position.InHorDistOf(st.water, Round4CampsEventsMath.FlockRange)) Round2Map.GoTo(b, Round2Map.Near(map, st.water, Round4CampsEventsMath.FlockRange - 2f, b));
                else if (Rand.Chance(0.3f)) Round2Map.Wander(b, st.water, Round4CampsEventsMath.FlockRange - 2f);
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            var map = c.map;
            if (st == null || map == null || !st.water.IsValid) return null;
            int hour = GenLocalDate.HourOfDay(map);
            if (Round4CampsEventsMath.Dawn(hour) && st.shotDay != GenDate.DaysPassed && GamePrey.CanHunt(p))
            {
                // At dawn, one bird of the flock: the game's own hunt.
                var b = Flock(c, st).Where(x => !c.prey.Contains(x) && p.CanReserveAndReach(x, PathEndMode.Touch, Danger.Some) && !c.ColonyInLineOfFire(p.Position, x.Position) && c.ClearOfColony(x.Position))
                    .OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (b != null)
                {
                    c.prey.Add(b);
                    st.shotDay = GenDate.DaysPassed;
                    st.shot++;
                    c.SayMoment(p, "dawn", 0.6f);
                    var job = JobMaker.MakeJob(JobDefOf.Hunt, b);
                    job.ignoreDesignations = true;
                    job.killIncappedTarget = true;
                    return job;
                }
            }
            // By day, the decoys at the water's edge.
            if (hour < 9 || hour > 16 || !Rand.Chance(0.3f)) return null;
            var at = CampRound2.Near(c, p, st.water, 6);
            if (!at.IsValid) return null;
            c.SayMoment(p, "decoy", 0.2f);
            return CampRound2.Work("Rimfolk_R4K_CampDecoys", at, new LocalTargetInfo(st.water));
        }

        public override void Leaving(Camp c)
        {
            // The flock goes on with them.
            var st = StateOf(c);
            if (st == null) return;
            foreach (var b in Flock(c, st)) Round2Map.Leave(b);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.shot ?? 0) > 0) marks.Add("shot");
        }

        public override string Fill(Camp c, string text)
        {
            var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(StateOf(c)?.kind ?? "") ?? Kind(c?.map);
            return text?.Replace("{BIRDS}", def?.GetLabelPlural() ?? "CC_R4K_Fowl_Birds".Translate().Resolve());
        }
    }

    // ===================== R4-C4. Snow-carvers =====================

    public class R4KSnowState : CampWorkState
    {
        /// <summary>Where the next figure is going up and the turns of work on it; figures made; the one of the colony making one with them.</summary>
        public IntVec3 spot = IntVec3.Invalid;
        public int turns, figures, makeUntil = -1, withFrom = -1;
        public Pawn maker;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref turns, "turns");
            Scribe_Values.Look(ref figures, "figures");
            Scribe_Values.Look(ref makeUntil, "makeUntil", -1);
            Scribe_Values.Look(ref withFrom, "withFrom", -1);
            Scribe_References.Look(ref maker, "maker");
        }
    }

    /// <summary>
    /// Snow-carvers (R4-C4): in winter, with deep snow on the land, three to five camp on the snow, and by day two of them work a
    /// heap of snow at the edge of their camp; when one is done it stands there, the game's own snow figure (up to five in a
    /// stay), nobody's, until the thaw takes it. The colony may come for an evening among the figures (a gathering at their
    /// camp), send someone to make a figure with them, or learn how they shape the snow.
    /// </summary>
    public class CampWorker_R4KSnowCarvers : CampWorker
    {
        public const string MakeAsk = "make";

        public static R4KSnowState StateOf(Camp c) => c?.workState as R4KSnowState;

        private static R4KSnowState Ensure(Camp c)
        {
            if (!(c.workState is R4KSnowState)) c.workState = new R4KSnowState();
            return (R4KSnowState)c.workState;
        }

        public static ThingDef Figure => DefDatabase<ThingDef>.GetNamedSilentFail("Snowman");

        /// <summary>The snow lying on open ground near the colony's middle, on average.</summary>
        public static float SnowAround(Map map)
        {
            if (map?.snowGrid == null) return 0f;
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid) mid = map.Center;
            float sum = 0f;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(mid, 16f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map)) continue;
                sum += map.snowGrid.GetDepth(c);
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        public override bool FitsMap(Map map) => map != null && Figure != null && Round4CampsEventsMath.SnowFits(SnowAround(map), map.mapTemperature.OutdoorTemp);

        public override void Started(Camp c) => Ensure(c);

        /// <summary>A figure stands at the spot (or near it): the game's own snow figure, nobody's; a puff of snow as it is done.</summary>
        private static Thing Stand(Camp c, R4KSnowState st, Pawn by)
        {
            var map = c.map;
            var def = Figure;
            if (def == null || map == null) return null;
            var at = st.spot.IsValid && st.spot.Standable(map) && st.spot.GetEdifice(map) == null && st.spot.GetFirstPawn(map) == null ? st.spot : CampRound4.Open(c, c.FireCell, 6, x => !x.InHorDistOf(c.FireCell, 3f));
            if (!at.IsValid) return null;
            Thing t;
            try
            {
                var made = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                var art = made.TryGetComp<CompArt>();
                var q = made.TryGetComp<CompQuality>();
                if (q != null) q.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                else art?.InitializeArt(ArtGenerationContext.Outsider);
                if (art != null && by != null) art.JustCreatedBy(by);
                t = GenSpawn.Spawn(made, at, map);
                FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 1.6f, Color.white);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: a snow figure", e);
                return null;
            }
            st.figures++;
            st.turns = 0;
            st.spot = IntVec3.Invalid;
            return t;
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = Ensure(c);
            if (st.turns >= Round4CampsEventsMath.TurnsPerFigure && st.figures < Round4CampsEventsMath.FiguresMost && st.spot.IsValid
                && !CampRound3.Up(c).Any(p => p.CurJob?.targetB.Cell == st.spot))
            {
                var by = CampRound4.Best(c, SkillDefOf.Artistic);
                var t = Stand(c, st, by);
                if (t != null && by != null) c.SayMoment(by, "figure");
            }
            Together(c, st);
        }

        /// <summary>Someone of the colony making a figure with them: after an hour at it with them, it stands by their camp.</summary>
        private static void Together(Camp c, R4KSnowState st)
        {
            if (st.maker == null) return;
            if (st.maker.Dead || CampRound4.Now > st.makeUntil && st.withFrom < 0)
            {
                st.maker = null;
                st.withFrom = -1;
                return;
            }
            bool first = st.withFrom < 0;
            var sitting = CampRound3.Sit(c, st.maker, ref st.withFrom, Round4CampsEventsMath.ShapeHours, out var them);
            if (first && sitting == CampRound3.Sitting.Sitting && them != null) c.SayMoment(them, "shape");
            if (sitting != CampRound3.Sitting.Done) return;
            // together: the colonist walked to their camp (the ask's visit) and worked within talking range of one of them (Together.Can).
            var maker = st.maker;
            st.maker = null;
            st.withFrom = -1;
            st.spot = IntVec3.Invalid;
            var t = Stand(c, st, maker);
            if (t == null) return;
            if (them != null) c.SayMoment(them, "figure");
            c.Warm(maker, 1);
            CouncilGameComponent.GiveThought(maker, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R4K_SnowFigure"));
            c.Note(c.Text("CC_R4K_Snow_Together", them, maker), MessageTypeDefOf.PositiveEvent, Importance.Notable, t, true);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            if (st == null || p == c.trader || st.figures >= Round4CampsEventsMath.FiguresMost || !Rand.Chance(0.55f)) return null;
            // At most two of them at the heap.
            if (CampRound3.Up(c).Count(x => x.CurJobDef?.defName == "Rimfolk_R4K_CampSnowWork") >= 2) return null;
            if (!st.spot.IsValid || st.spot.GetEdifice(c.map) != null)
            {
                st.spot = CampRound4.Open(c, c.FireCell, 7, x => !x.InHorDistOf(c.FireCell, 3.5f));
                st.turns = 0;
            }
            if (!st.spot.IsValid) return null;
            var at = CampRound2.Near(c, p, st.spot, 1);
            if (!at.IsValid || at == st.spot) return null;
            st.turns++;
            c.SayMoment(p, "shape", 0.15f);
            return CampRound2.Work("Rimfolk_R4K_CampSnowWork", at, new LocalTargetInfo(st.spot));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.figures ?? 0) > 0) marks.Add("figures");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != MakeAsk) return null;
            var st = StateOf(c);
            if (st?.maker != null && !st.maker.Dead && CampRound4.Now <= st.makeUntil) return "CC_R4K_Snow_Busy".Translate().Resolve();
            return Figure == null ? "CC_CampAsk_NotNow".Translate().ToString() : null;
        }

        /// <summary>The one sent over to make a figure with them (the ask's visit takes them there).</summary>
        public static void Sent(Camp c, Pawn a)
        {
            var st = StateOf(c);
            if (st == null || a == null) return;
            st.maker = a;
            st.withFrom = -1;
            st.makeUntil = CampRound4.Now + GenDate.TicksPerDay / 2;
        }
    }

    /// <summary>Someone of the colony sent to make a snow figure with the snow-carvers (the ask's walk over is its visit).</summary>
    public class DilemmaEffect_R4KSnowFigure : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null && CampOnMap.For(ctx)?.kind?.Worker is CampWorker_R4KSnowCarvers)
                sheet.Play("CC_R4K_Snow_Impact".Translate(ctx.a.LabelShort).Resolve(), "CC_R4K_Snow_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_R4KSnowCarvers.Sent(CampOnMap.For(ctx), ctx?.a);
    }

    // ===================== R4-C5. Cheesemakers =====================

    public class R4KCheeseState : CampWorkState
    {
        /// <summary>The day the milk was last set for cheese; the cheese made in their stay.</summary>
        public int lastDay = -1, made;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastDay, "lastDay", -1);
            Scribe_Values.Look(ref made, "made");
        }
    }

    /// <summary>
    /// Cheesemakers (R4-C5): in spring and summer a family with a few goats camps on hilly or mountain land; by day one of them
    /// stirs the curd in a pot by the fire, and once a day, in the afternoon, a little of the milk in their store becomes
    /// cheese (Vanilla Cooking Expanded's; without it the milk stays milk). The colony may buy the goats' milk (the camps' own
    /// milk sale), a wheel of their cheese, or learn the craft.
    /// </summary>
    public class CampWorker_R4KCheesemakers : CampWorker
    {
        public static R4KCheeseState StateOf(Camp c) => c?.workState as R4KCheeseState;

        public static ThingDef Cheese => DefDatabase<ThingDef>.GetNamedSilentFail("VCE_Cheese");

        public override bool FitsMap(Map map)
        {
            var lands = Lands.Of(map);
            return lands.Contains("mountains") || lands.Contains("hills");
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is R4KCheeseState)) c.workState = new R4KCheeseState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is R4KCheeseState st))
            {
                Started(c);
                st = StateOf(c);
            }
            if (hour != 16 || st.lastDay == GenDate.DaysPassed || !c.Settled) return;
            st.lastDay = GenDate.DaysPassed;
            var cheese = Cheese;
            if (cheese == null) return;
            var milkDef = DefDatabase<ThingDef>.GetNamedSilentFail("Milk");
            var milk = c.Goods().Where(t => t.def == milkDef && !t.Destroyed).ToList();
            int have = milk.Sum(t => t.stackCount);
            int n = Round4CampsEventsMath.CheeseOf(have);
            if (milkDef == null || n <= 0) return;
            try
            {
                // Twice the cheese's weight of milk goes into the pot, and the cheese comes out where the milk lay.
                int take = Mathf.Min(have, n * 2);
                var at = milk[0].Position;
                foreach (var t in milk)
                {
                    if (take <= 0) break;
                    int k = Mathf.Min(take, t.stackCount);
                    take -= k;
                    if (k >= t.stackCount) t.Destroy();
                    else t.SplitOff(k).Destroy();
                }
                var made = ThingMaker.MakeThing(cheese);
                made.stackCount = Mathf.Min(n, cheese.stackLimit);
                if (!GenPlace.TryPlaceThing(made, at, c.map, ThingPlaceMode.Near, out var put)) return;
                c.Own(put ?? made);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: the cheese", e);
                return;
            }
            st.made += n;
            var p = CampRound3.Up(c).FirstOrDefault(x => x != c.trader);
            if (p != null) c.SayMoment(p, "cheese");
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            if (p == c.trader || !Rand.Chance(0.4f)) return null;
            var at = CampRound3.ByFire(c, p);
            if (!at.IsValid) return null;
            c.SayMoment(p, "curd", 0.12f);
            return CampRound2.Work("Rimfolk_R4K_CampCurd", at, new LocalTargetInfo(c.FireCell));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.made ?? 0) > 0) marks.Add("cheese");
        }
    }

    // ===================== R4-C6. Instrument-makers =====================

    public class R4KMusicState : CampWorkState
    {
        /// <summary>Their own instrument by the fire (theirs, packed again when they go); set up; how many the colony bought.</summary>
        public Thing instrument;
        public bool placed;
        public int sold;

        public override void ExposeData()
        {
            Scribe_References.Look(ref instrument, "instrument");
            Scribe_Values.Look(ref placed, "placed");
            Scribe_Values.Look(ref sold, "sold");
        }
    }

    /// <summary>
    /// Instrument-makers (R4-C6): two or three makers with a cart camp by the colony from spring to fall, their own instrument
    /// of their age by the fire (Ideology's drum, Royalty's harp, harpsichord or piano, Vanilla Furniture Expanded's upright
    /// piano: Round4CampsEventsMath.Instruments, the nearest age's where theirs isn't there); by day one of them works at a new
    /// one, in the evening one plays theirs (the instrument's own music, as when it is played). The colony may buy one, packed
    /// and set out at the edge of their camp, come for an evening of their music, or learn to play.
    /// </summary>
    public class CampWorker_R4KInstruments : CampWorker
    {
        public static R4KMusicState StateOf(Camp c) => c?.workState as R4KMusicState;

        private static R4KMusicState Ensure(Camp c)
        {
            if (!(c.workState is R4KMusicState)) c.workState = new R4KMusicState();
            return (R4KMusicState)c.workState;
        }

        /// <summary>The first instrument of their age that the game has (a building that can be packed).</summary>
        public static ThingDef DefFor(string ageTag) =>
            Round4CampsEventsMath.Instruments(ageTag).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building && d.Minifiable);

        public static ThingDef DefFor(Camp c) => DefFor(CampRound3.AgeOf(c));

        public override bool FitsMap(Map map) => new[] { "tribal", "medieval", "industrial", "spacer" }.Any(a => DefFor(a) != null);

        public override void Started(Camp c) => Ensure(c);

        /// <summary>Their instrument set up by their fire, theirs.</summary>
        private static void SetUp(Camp c, R4KMusicState st)
        {
            st.placed = true;
            var def = DefFor(c);
            var map = c.map;
            if (def == null || map == null) return;
            var rot = Rot4.South;
            bool Fits(IntVec3 x)
            {
                foreach (var y in GenAdj.OccupiedRect(x, rot, def.size))
                    if (!y.InBounds(map) || !y.Standable(map) || y.GetEdifice(map) != null || y.GetFirstPawn(map) != null || y.GetFirstItem(map) != null || !c.ClearOfColony(y)) return false;
                if (!def.hasInteractionCell) return true;
                var ic = ThingUtility.InteractionCellWhenAt(def, x, rot, map);
                return ic.InBounds(map) && ic.Standable(map) && ic.GetEdifice(map) == null;
            }
            var at = CampRound4.Open(c, c.FireCell, 5, x => !x.InHorDistOf(c.FireCell, 2f) && Fits(x));
            if (!at.IsValid) return;
            try
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                t.SetFaction(c.faction);
                st.instrument = GenSpawn.Spawn(t, at, map, rot);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: the instrument set up", e);
            }
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = Ensure(c);
            if (!st.placed && c.Settled) SetUp(c, st);
        }

        private static bool Here(Camp c, R4KMusicState st) => st?.instrument != null && st.instrument.Spawned && st.instrument.Map == c.map && st.instrument.Faction == c.faction;

        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            // In the evening one of them plays theirs.
            var st = StateOf(c);
            if (!Round4CampsEventsMath.PlayHour(hour) || p == c.trader || !Here(c, st) || !Rand.Chance(0.5f)) return null;
            if (CampRound3.Up(c).Any(x => x.CurJobDef?.defName == "Rimfolk_R4K_CampPlay")) return null;
            var def = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_R4K_CampPlay");
            if (def == null || !p.CanReach(st.instrument, PathEndMode.Touch, Danger.Some)) return null;
            c.SayMoment(p, "play", 0.3f);
            return JobMaker.MakeJob(def, st.instrument);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // By day, a new one fitted and tuned by the fire.
            var st = StateOf(c);
            if (p == c.trader || !Rand.Chance(0.35f)) return null;
            var at = CampRound3.ByFire(c, p);
            if (!at.IsValid) return null;
            c.SayMoment(p, "tune", 0.12f);
            return CampRound2.Work("Rimfolk_R4K_CampTune", at, Here(c, st) ? new LocalTargetInfo(st.instrument) : new LocalTargetInfo(c.FireCell));
        }

        public override void Leaving(Camp c)
        {
            // Their own instrument is packed onto the cart.
            var st = StateOf(c);
            if (!Here(c, st)) return;
            try
            {
                if (st.instrument is Building_MusicalInstrument mi && mi.IsBeingPlayed) mi.StopPlaying();
                st.instrument.Destroy(DestroyMode.Vanish);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: the instrument packed", e);
            }
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.sold ?? 0) > 0) marks.Add("sold");
        }

        public override string Fill(Camp c, string text) => text?.Replace("{INSTRUMENT}", DefFor(c)?.label ?? "CC_R4K_Music_Instrument".Translate().Resolve());
    }

    /// <summary>One of the instrument-makers' instruments bought: a new one of their age, packed and set out at the edge of their camp.</summary>
    public class DilemmaEffect_R4KBuyInstrument : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null || CampWorker_R4KInstruments.DefFor(c) == null ? "CC_CampAsk_NotNow".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var def = c == null ? null : CampWorker_R4KInstruments.DefFor(c);
            if (def != null) sheet.Goods(1, def.label, 1, "r4kinstrument");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var def = c == null ? null : CampWorker_R4KInstruments.DefFor(c);
            if (def == null) return;
            var maker = CampRound4.Best(c, SkillDefOf.Crafting);
            Thing piece;
            try
            {
                piece = CampRound4.SetOut(c, CampRound4.Made(def, null, maker, null, Rand.Value));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 camps: an instrument bought", e);
                return;
            }
            if (piece == null) return;
            var st = CampWorker_R4KInstruments.StateOf(c);
            if (st != null) st.sold++;
            if (maker != null) c.SayMoment(maker, "sold");
            c.Note(c.Text("CC_R4K_Music_Sold", maker, null, 0, def.label), MessageTypeDefOf.PositiveEvent, Importance.Notable, piece, true);
        }
    }

    /// <summary>
    /// One of the instrument-makers playing their own instrument in the evening: to its seat, the instrument's own music while
    /// they play (as when the game's instrument is played), and stopped when they're done.
    /// </summary>
    public class JobDriver_R4KPlay : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private Building_MusicalInstrument Instrument => job.targetA.Thing as Building_MusicalInstrument;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            if (Instrument != null && Instrument.def.hasInteractionCell) yield return Toils_Goto.GotoCell(Instrument.InteractionCell, PathEndMode.OnCell);
            else yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            bool playing = false;
            var play = ToilMaker.MakeToil("R4KPlay");
            play.defaultCompleteMode = ToilCompleteMode.Delay;
            play.defaultDuration = 2000;
            play.handlingFacing = true;
            play.initAction = () =>
            {
                try
                {
                    var mi = Instrument;
                    if (mi != null && !mi.IsBeingPlayed)
                    {
                        mi.StartPlaying(pawn);
                        playing = true;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4 camps: playing", e);
                }
            };
            play.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (!playing && pawn.IsHashIntervalTick(200))
                {
                    var note = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                    if (note != null) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, note);
                }
            };
            play.AddFinishAction(() =>
            {
                try
                {
                    var mi = Instrument;
                    if (playing && mi != null && mi.IsBeingPlayed) mi.StopPlaying();
                    playing = false;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4 camps: playing stopped", e);
                }
            });
            yield return play;
        }
    }

    // ===================== Dev mode =====================

    /// <summary>Dev mode for the round's camps (Debug actions, "Rimfolk"): each new camp brought here now (beside any already here).</summary>
    public static class Round4CampsEventsDebug
    {
        private static Map Map => Find.CurrentMap ?? HappeningTracker.HomeMap();

        private static void Say(string text) => Messages.Message("Round 4: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Round 4: one of the new camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Camp()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (string n in CampRound4.Kinds)
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label + (kind.FitsMap(map) ? "" : " (the land or the season lacks what they come for)"), () =>
                {
                    var tracker = CampTracker.Instance;
                    if (tracker == null || !CampTracker.Enabled)
                    {
                        Say("camps are off.");
                        return;
                    }
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    var why = new List<string>();
                    var c = f == null ? null : tracker.StartKind(map, kind, f, why);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: " + (f == null ? "no people at peace that fits" : string.Join("; ", why)));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Round 4: one of the new map events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Event()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (string n in R4EMap.Events)
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.LabelCap, () =>
                {
                    var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    parms.forced = true;
                    Say(def.Worker.TryExecute(parms) ? def.label + " now." : "couldn't start " + def.label + " now (the land, the season or nobody fits).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
