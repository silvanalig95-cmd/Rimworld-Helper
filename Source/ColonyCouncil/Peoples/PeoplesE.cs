using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release E's four peoples at the colony's gate (docs/design/peoples.md, "Release E"; docs/design/peoples-next.md; the
    // defs in Defs/IncidentDefs/Rimfolk_Events_PeoplesE.xml), each only from a people of its calling at peace with the
    // colony, and every one played out on the map. Here: the Stormglass Conclave's watch on the sky, its rod and its
    // lesson, and the Mossborn's saplings, their tree over a grave, their tending of the colony's Gauranlen tree and their
    // ledger of the colony's planting and felling. The Ashen Banner and the Kiln Guild are in PeoplesE2.cs; the game hooks
    // (the held conditions, the rod's lightning, the felled trees) in PeoplesEPatches.cs. Nothing here keeps a clock or
    // saves anything of its own: the watch and the runners are the events' visits (HappeningTracker.visits), the ledger's
    // counts are kept in the peoples' book (ArisingBook), the held conditions in the game's own incident queue.

    /// <summary>What release E's touchpoints share: their jobs, the work done at their end, a sapling planted.</summary>
    public static class PeoplesE
    {
        public static JobDef RodJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_SetRod");

        public static JobDef PlantJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_Plant");

        public static JobDef TendJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_TendTree");

        public static JobDef BenchJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_Bench");

        public static JobDef GraveTreeJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_GraveTree");

        public static JobDef SkyJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesE_Skywatch");

        /// <summary>The Conclave's rod (Defs/Misc/Rimfolk_PeoplesE_Misc.xml).</summary>
        public static ThingDef RodDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_StormglassRod");

        /// <summary>A visitor of theirs on the map and up.</summary>
        public static bool Up(Pawn p, Map map) => p != null && !p.Dead && !p.Downed && p.Spawned && p.Map == map;

        /// <summary>A job of release E's own done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == RodJob) GlassRod.Set(pawn, target.Cell);
                else if (job == PlantJob) MossPlanting.Planted(pawn, target.Cell, null, pawn.CurJob?.targetQueueA.NullOrEmpty() != false);
                else if (job == GraveTreeJob) MossPlanting.GraveTree(pawn, target.Cell);
                else if (job == TendJob) MossTend.Tended(pawn, target.Thing);
                else if (job == BenchJob) WanderYear.Worked(pawn, target.Thing);
                else if (job == SkyJob) HappeningWorker_MissionGlassApprentice.Watched(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: work done", e);
            }
        }

        /// <summary>A job of theirs at one target or a queue of them, a while at each (Job.count ticks).</summary>
        public static bool Start(Pawn pawn, JobDef def, LocalTargetInfo first, List<LocalTargetInfo> rest, int ticks)
        {
            if (pawn == null || def == null || !pawn.Spawned || pawn.Downed || !first.IsValid) return false;
            var job = JobMaker.MakeJob(def, first);
            job.count = Mathf.Max(60, ticks);
            if (!rest.NullOrEmpty()) job.targetQueueA = rest.ToList();
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return pawn.CurJobDef == def;
        }

        /// <summary>A tree that grows wild here for a sapling (the land's own first, else an oak or a birch), or null.</summary>
        public static ThingDef SaplingFor(Map map, IntVec3 c)
        {
            if (map == null) return null;
            var wild = map.Biome?.AllWildPlants?.Where(d => d?.plant != null && d.plant.IsTree && d.plant.harvestedThingDef == ThingDefOf.WoodLog
                                                            && c.GetTerrain(map).fertility >= d.plant.fertilityMin).ToList();
            if (!wild.NullOrEmpty()) return wild.RandomElementByWeight(d => Mathf.Max(0.05f, map.Biome.CommonalityOfPlant(d)));
            foreach (var n in new[] { "Plant_TreeOak", "Plant_TreeBirch", "Plant_TreePoplar" })
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d?.plant != null && c.GetTerrain(map).fertility >= d.plant.fertilityMin) return d;
            }
            return null;
        }

        /// <summary>A cell a sapling can take: soil, nothing on it, under the sky, out of the colony's buildings and fields.</summary>
        public static bool SaplingCell(Map map, IntVec3 c) =>
            c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetFirstBuilding(map) == null && c.GetPlant(map) == null && c.GetFirstItem(map) == null
            && !c.Roofed(map) && c.GetZone(map) == null && c.GetTerrain(map).fertility >= 0.7f && !c.GetTerrain(map).IsWater;

        /// <summary>A sapling set in the ground at a cell, planted by hand (so it counts as the colony's planting).</summary>
        public static Plant Sapling(Map map, IntVec3 c, ThingDef def)
        {
            if (map == null || def == null || !SaplingCell(map, c)) return null;
            if (!(GenSpawn.Spawn(def, c, map) is Plant p)) return null;
            p.Growth = Rand.Range(PeoplesEMath.SaplingGrowthMin, PeoplesEMath.SaplingGrowthMax);
            p.sown = true;
            FleckMaker.ThrowDustPuff(c, map, 0.6f);
            return p;
        }

        /// <summary>A line of the event's own with these tags, said over their head.</summary>
        public static void Say(Pawn x, HappeningExtension ext, HappeningContext ctx, params string[] tags) => PeoplesD.Say(x, ext, ctx, tags);
    }

    /// <summary>
    /// Release E's own work at a cell or a thing, or a queue of them: there, a while at it (dust, sparks or leaves), then
    /// what it was for (PeoplesE.WorkDone); the next in the queue, if any. The sky watched (PeoplesE.SkyJob) is lying on
    /// the ground face up a while, and stops under a roof. Its place in the queue isn't kept with the game: a job loaded
    /// halfway starts its current target again.
    /// </summary>
    public class JobDriver_PeoplesE : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PeoplesENext");
            next.initAction = () =>
            {
                if (job.targetA.IsValid) return;
                if (!job.targetQueueA.NullOrEmpty())
                {
                    job.targetA = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                }
                else EndJobWith(JobCondition.Succeeded);
            };
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return next;
            bool sky = job.def == PeoplesE.SkyJob;
            if (sky) yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            else yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PeoplesEWork");
            if (sky)
            {
                work.initAction = () => pawn.jobs.posture = PawnPosture.LayingOnGroundFaceUp;
                work.FailOn(() => pawn.Position.Roofed(pawn.Map));
            }
            else
            {
                work.tickIntervalAction = delta =>
                {
                    var t = job.targetA;
                    if (!t.IsValid) return;
                    pawn.rotationTracker.FaceCell(t.Cell);
                    if (!pawn.IsHashIntervalTick(200, delta)) return;
                    if (job.def == PeoplesE.RodJob || job.def == PeoplesE.BenchJob) FleckMaker.ThrowMicroSparks(t.Cell.ToVector3Shifted(), pawn.Map);
                    else FleckMaker.ThrowDustPuff(t.Cell, pawn.Map, 0.4f);
                };
                work.WithProgressBarToilDelay(TargetIndex.A);
                work.handlingFacing = true;
            }
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("PeoplesEDone");
            done.initAction = () =>
            {
                PeoplesE.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The Stormglass Conclave: a season's watch on the sky =====================

    /// <summary>
    /// A reader of the Stormglass Conclave comes to the gate: for a fee, the Conclave will keep watch on the colony's sky for
    /// a season. While the watch runs, every condition of the sky the storyteller sends to the colony (a cold snap, a heat
    /// wave, toxic fallout, a volcanic winter, a flashstorm, an eclipse, the noxious haze, a drought: PeoplesEMath.SkyIncidents)
    /// is held back a day in the game's own incident queue, and a vial-runner of theirs comes to the gate with the warning
    /// first; the colony's fields by the weather reckon the held cold or heat from that hour (Fields/FieldWeather.cs). Paid,
    /// the reader walks out to the colony's fields to read them before going. Or a reading of tonight's sky alone, free: the
    /// reader stands at the gate until dark. {COUNT} is the fee.
    /// </summary>
    public class HappeningWorker_GlassReading : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && GlassWatch.Of(map) == null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            ctx.count = PeoplesEMath.WatchFee(ctx.map.wealthWatcher.WealthTotal);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Plants) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (GlassWatch.Fields(ctx.map) > 0) tags.Add("fields");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 0 ? "watch" : option == 1 ? "sky" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The watch begun: a season of the Conclave's warnings, and the reader walks out to the colony's fields to read them.</summary>
    public class DilemmaEffect_GlassWatch : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesE_Impact_Watch".Translate(PeoplesEMath.WatchDays).Resolve(), "CC_PeoplesE_Impact_WatchBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (map == null || ctx.faction == null) return;
            GlassWatch.Begin(map, ctx.faction, hc?.incident);
            var x = ctx.stranger;
            if (!PeoplesE.Up(x, map)) return;
            // The reader walks out to the fields (the biggest under the sky, else the middle of the colony) and reads them a while.
            var field = map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count > 0).OrderByDescending(z => z.Cells.Count).FirstOrDefault();
            var spot = field?.Cells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(field.Position)).FirstOrDefault() ?? IntVec3.Invalid;
            if (!spot.IsValid) spot = CampSite.ColonyCenter(map);
            if (spot.IsValid) PeoplesD.Gather(new List<Pawn> { x }, ctx.faction, spot, 3f);
        }
    }

    /// <summary>
    /// The Conclave's watch on one home's sky (a visit of the events' tracker, until the season is out): the held condition
    /// (its def's name in the visit's text, the tick it is due in its due), the runner sent with it, and what the fields
    /// reckon of what the game's queue holds for the map.
    /// </summary>
    public static class GlassWatch
    {
        public const string Kind = "glassWatch", RunnerKind = "glassRunner";

        private static int Now => Find.TickManager.TicksGame;

        private static List<Visit> Visits => CouncilGameComponent.Instance?.happenings?.visits;

        /// <summary>The watch running on this map, or null.</summary>
        public static Visit Of(Map map)
        {
            var all = Visits;
            if (map == null || all == null) return null;
            int now = Now;
            return all.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.map == map && now <= v.until);
        }

        /// <summary>The colony's fields set to sow by the weather (which reckon a held cold or heat).</summary>
        public static int Fields(Map map) =>
            map == null ? 0 : map.zoneManager.AllZones.OfType<Zone_Growing>().Count(z => FieldWeather.Of(map)?.IsByWeather(z) == true);

        public static void Begin(Map map, Faction f, IncidentDef incident)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || map == null || Of(map) != null) return;
            var v = t.AddVisit(Kind, incident, map, null, null, null, Now + PeoplesEMath.WatchDays * GenDate.TicksPerDay);
            v.due = -1;
            v.text = f?.GetUniqueLoadID();
        }

        /// <summary>The watch's hour: it ends with its season; a held condition past its hour and its retries is let go.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done || v.map == null || !Find.Maps.Contains(v.map) || now > v.until) return false;
            if (v.due > 0 && now > v.due + PeoplesEMath.RetryHours * GenDate.TicksPerHour) v.due = -1;
            return true;
        }

        /// <summary>The Conclave's people the watch was bought from (the first of their calling at peace, if that one is gone).</summary>
        private static Faction PeopleOf(Visit v) =>
            Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == v.text && PeoplesD.Theirs(f, PeoplesMath.Seers))
            ?? PeoplesD.Friendly(PeoplesMath.Seers);

        /// <summary>
        /// The storyteller sends a condition of the sky (Patch_GlassHold): while a watch runs on its home and nothing is held
        /// already, it goes into the game's own queue a day on, the runner sets off with the warning, and the fields are
        /// reckoned again. True: held (the storyteller doesn't fire it now).
        /// </summary>
        public static bool Hold(FiringIncident fi)
        {
            if (fi?.def == null || fi.parms == null || !PeoplesEMath.Foreseen(fi.def.defName) || fi.sourceQuestPart != null || fi.parms.quest != null) return false;
            var map = fi.parms.target as Map;
            var v = Of(map);
            if (v == null || v.due > 0 || !map.IsPlayerHome) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms)) return false;
            int now = Now;
            int at = now + PeoplesEMath.HoldHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, PeoplesEMath.RetryHours * GenDate.TicksPerHour))) return false;
            v.due = at;
            Runner(map, v, fi.def);
            try
            {
                FieldWeather.Of(map)?.Refresh();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the fields reckon a held condition", e);
            }
            return true;
        }

        /// <summary>The vial-runner with the warning: the news at once, and the runner at the gate to say it.</summary>
        private static void Runner(Map map, Visit watch, IncidentDef def)
        {
            var f = PeopleOf(watch);
            string what = (def.gameCondition?.label ?? def.label).ToString();
            Pawn runner = null;
            try
            {
                var kind = f == null ? null : Arrivals.PersonKind(f);
                runner = kind == null ? null : Arrivals.Make(kind, f, new FloatRange(16f, 30f));
                if (runner != null && Arrivals.Visit(new List<Pawn> { runner }, map, f, PeoplesD.GateSpot(map), 4f) == null)
                {
                    Arrivals.Discard(runner);
                    runner = null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the vial-runner", e);
                runner = null;
            }
            if (runner != null)
            {
                var r = CouncilGameComponent.Instance?.happenings?.AddVisit(RunnerKind, watch.incident, map, runner, null, null, Now + 10 * GenDate.TicksPerHour);
                if (r != null) r.text = what;
            }
            string word = Peoples.Courier(f, false) ?? "CC_PeoplesE_GlassRunnerNoun".Translate().Resolve();
            string text = runner != null
                ? "CC_PeoplesE_GlassHeld".Translate(runner.LabelShort.Named("RUNNER"), word.Named("WORD"), (f?.Name ?? "?").Named("FACTION"), what.Named("CONDITION")).Resolve()
                : "CC_PeoplesE_GlassHeldWord".Translate((f?.Name ?? "?").Named("FACTION"), what.Named("CONDITION")).Resolve();
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PeoplesE_GlassHeldLabel".Translate(what.CapitalizeFirst()).Resolve(),
                text + (Fields(map) > 0 ? "\n\n" + "CC_PeoplesE_GlassHeldFields".Translate().Resolve() : ""),
                LetterDefOf.NeutralEvent, runner != null ? new LookTargets(runner) : LookTargets.Invalid, f);
        }

        /// <summary>The runner's hour: at the gate, the warning said over their head, and they go.</summary>
        public static bool RunnerHour(Visit v, CouncilGameComponent comp, int now)
        {
            var x = v.x;
            if (v.done || now > v.until || x == null || x.Dead || !x.Spawned || x.Map != v.map) return false;
            var gate = PeoplesD.GateSpot(v.map);
            if (gate.HasValue && !x.Position.InHorDistOf(gate.Value, 10f)) return true;
            Speech.Say(x, "CC_PeoplesE_GlassRunnerSays".Translate((v.text ?? "?").CapitalizeFirst()).Resolve());
            return false;
        }

        /// <summary>
        /// What the game's incident queue holds for this map of the sky's cold and heat (a held cold snap, a heat wave, a
        /// volcanic winter), as the fields' forecast reads it: the offset, from which hour of the forecast, for how many.
        /// </summary>
        public static List<(float offset, int from, int hours)> Ahead(Map map, int forecastHours)
        {
            var list = new List<(float, int, int)>();
            var queue = Find.Storyteller?.incidentQueue;
            if (map == null || queue == null) return list;
            int now = Now;
            foreach (var o in queue)
            {
                if (!(o is QueuedIncident q) || q.FiringIncident?.def == null || q.FiringIncident.parms?.target != map) continue;
                var def = q.FiringIncident.def;
                float off = PeoplesEMath.Offset(def.gameCondition?.defName ?? def.defName);
                if (Mathf.Abs(off) < 0.01f) continue;
                PeoplesEMath.Span(q.FireTick, now, def.durationDays.Average, forecastHours, out int from, out int hours);
                if (hours > 0) list.Add((off, from, hours));
            }
            return list;
        }
    }

    // ===================== The Stormglass Conclave: a rod on the roof =====================

    /// <summary>
    /// A storm-walker of the Conclave asks to set one of their rods on the colony's highest roof: a brass rod with a sealed
    /// stormglass at its foot, for the Conclave's record of the storms. Set, it draws the lightning that would strike the
    /// open ground near it (PeoplesEMath.RodRadius) down onto itself, harmlessly, and the colony's forecasts are as right
    /// as with a weather station (Weather/WeatherDynamics.cs). The storm-walker carries it in and sets it up where they
    /// stand ({PLACE} is the room it goes over).
    /// </summary>
    public class HappeningWorker_GlassRod : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && PeoplesE.RodDef != null && map.listerThings.ThingsOfDef(PeoplesE.RodDef).Count == 0
            && GlassRod.Roof(map, out _).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            if (ctx.faction == null) return false;
            ctx.spot = GlassRod.Roof(ctx.map, out var room);
            if (!ctx.spot.IsValid) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Walker"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.tale = room?.GetRoomRoleLabel() ?? "CC_PeoplesE_RodRoom".Translate().Resolve();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Construction) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map.weatherManager.curWeather?.isBad == true || ctx.map.weatherManager.curWeather?.defName?.Contains("Thunder") == true) tags.Add("storm");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "rod" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The rod set where the event found the roof (HappeningContext.spot): the storm-walker carries it there and sets it up; with the play-outs off, at once.</summary>
    public class DilemmaEffect_GlassRod : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null || !hc.spot.IsValid || !GlassRod.Fits(ctx.map, hc.spot)) return "CC_PeoplesE_RodNoRoof".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            sheet.Play("CC_PeoplesE_Impact_Rod".Translate(hc?.tale ?? "?", (int)PeoplesEMath.RodRadius).Resolve(), "CC_PeoplesE_Impact_RodBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null || !hc.spot.IsValid) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesE.Up(x, map) && x.CanReach(hc.spot, PathEndMode.Touch, Danger.Some))
            {
                x.GetLord()?.RemovePawn(x);
                Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitColony(x.Faction, hc.spot, 4 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeoplesE.Start(x, PeoplesE.RodJob, hc.spot, null, 1200)) return;
            }
            GlassRod.Set(x, hc.spot, map);
        }
    }

    /// <summary>The Conclave's rods on the colony's roofs: where one goes, setting it, and the lightning it draws (Patch_GlassRodStrike).</summary>
    public static class GlassRod
    {
        /// <summary>A lightning strike on a rod under way (no fire where it lands: Patch_GlassRodExplosion).</summary>
        public static IntVec3 Striking = IntVec3.Invalid;

        public static Map StrikingMap;

        private static readonly Dictionary<int, int> toldAt = new Dictionary<int, int>();

        /// <summary>A cell a rod can stand on: under a roof of the colony's, standable, nothing built there.</summary>
        public static bool Fits(Map map, IntVec3 c) =>
            map != null && c.InBounds(map) && c.Roofed(map) && !map.roofGrid.RoofAt(c).isThickRoof && c.Standable(map) && c.GetEdifice(map) == null
            && c.GetFirstBuilding(map) == null && c.GetFirstItem(map) == null;

        /// <summary>The colony's highest roof, as far as a roof goes: the middle of its biggest roofed room (no cell, no sickroom, no mountain).</summary>
        public static IntVec3 Roof(Map map, out Room room)
        {
            room = null;
            if (map == null) return IntVec3.Invalid;
            foreach (var r in RoomUtil.ColonyRooms(map).Where(r => r != null && !r.PsychologicallyOutdoors && !r.IsPrisonCell && r.CellCount >= 9).OrderByDescending(r => r.CellCount))
            {
                var mid = r.Cells.Aggregate(IntVec3.Zero, (a, c) => a + c);
                var centre = new IntVec3(mid.x / r.CellCount, 0, mid.z / r.CellCount);
                var cell = r.Cells.Where(c => Fits(map, c)).OrderBy(c => c.DistanceToSquared(centre)).FirstOrDefault();
                if (cell.IsValid && Fits(map, cell))
                {
                    room = r;
                    return cell;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>The rod set at the cell, the colony's from now on.</summary>
        public static void Set(Pawn by, IntVec3 c, Map map = null)
        {
            map ??= by?.Map;
            var def = PeoplesE.RodDef;
            if (map == null || def == null || !Fits(map, c) || map.listerThings.ThingsOfDef(def).Count > 0) return;
            var rod = ThingMaker.MakeThing(def);
            rod.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(rod, c, map);
            FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(), map);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesE_RodSet".Translate((by?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(rod),
                MessageTypeDefOf.PositiveEvent);
            if (by != null && by.Spawned && by.Faction != null && !by.Faction.IsPlayer) Arrivals.SendAway(new List<Pawn> { by });
        }

        /// <summary>The rod that draws a strike at this cell (the open ground within its reach), or null.</summary>
        public static Thing Near(Map map, IntVec3 loc)
        {
            var def = PeoplesE.RodDef;
            if (map == null || def == null || !loc.IsValid) return null;
            var rods = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < rods.Count; i++)
            {
                var r = rods[i];
                if (r.Spawned && PeoplesEMath.Draws(r.Position.x - loc.x, r.Position.z - loc.z)) return r;
            }
            return null;
        }

        /// <summary>Lightning taken by the rod: said in the news once a storm (six hours between).</summary>
        public static void Struck(Map map, Thing rod)
        {
            int now = Find.TickManager.TicksGame;
            if (toldAt.TryGetValue(map.uniqueID, out int last) && now - last < 6 * GenDate.TicksPerHour && now >= last) return;
            toldAt[map.uniqueID] = now;
            Tell.Message(NewsKind.Land, Importance.Flavor, map, "CC_PeoplesE_RodStruck".Translate().Resolve(), new LookTargets(rod), MessageTypeDefOf.NeutralEvent);
        }
    }

    // ===================== The Stormglass Conclave: reading the glass =====================

    /// <summary>
    /// Towards evening a reader of the Conclave offers to teach the colony's weather-reader (its best hand at the fields,
    /// who already says what the weather will do) to read the glass: a lesson at dusk, and for a season after their
    /// forecasts are better (PeoplesEMath.Taught; a weather-reader the colony didn't have before now reads it). Or an hour on
    /// the wall together, looking at tonight's sky.
    /// </summary>
    public class HappeningWorker_GlassLesson : HappeningWorker_Visitors
    {
        public static HediffDef Taught => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GlassTaught");

        public static bool IsTaught(Pawn p) => Taught != null && p?.health?.hediffSet?.HasHediff(Taught) == true;

        /// <summary>The colony's weather-reader who isn't taught yet: its best at the fields.</summary>
        public static Pawn Reader(Map map) =>
            PeoplesD.Best(map, SkillDefOf.Plants, p => !IsTaught(p) && p.skills?.GetSkill(SkillDefOf.Plants)?.TotallyDisabled == false);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && Taught != null && PeoplesMath.ShelterHour(GenLocalDate.HourOfDay(map)) && Reader(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Plants, new IntRange(10, 16));
            ctx.a = Reader(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(ctx.map), 6)) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            int plants = ctx.a?.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0;
            if (!WeatherDynamicsMath.ReadsWeather(plants, ctx.a != null ? Flavor.HumanAge(ctx.a) : 0f, false)) tags.Add("newreader");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "glass" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>A taught to read the glass: for a season their forecasts are the Conclave's (Rimfolk_GlassTaught).</summary>
    public class DilemmaEffect_GlassTaught : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.a == null || ctx.a.Dead ? "CC_PeoplesE_NoReader".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            sheet.Add(ImpactKind.Other, 1, "CC_PeoplesE_Impact_Taught".Translate(ctx.a.LabelShort).Resolve(), "CC_PeoplesE_Impact_TaughtBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = ctx?.a;
            var def = HappeningWorker_GlassLesson.Taught;
            if (p == null || p.Dead || def == null || HappeningWorker_GlassLesson.IsTaught(p)) return;
            p.health.AddHediff(def);
        }
    }

    // ===================== The Mossborn: saplings for what was cleared =====================

    /// <summary>
    /// The colony has felled many trees this season (the Mossborn's ledger: PeoplesEMath.SaplingsAfter), and a seed-carrier
    /// of theirs comes with a sack of saplings: every tree felled owes the world a seedling, and they are here to collect.
    /// Bidden, they plant them on open soil round the colony (out of its buildings and fields), one after another, the
    /// colony's planter beside them learning if the colony likes; the saplings are the colony's planting in the ledger.
    /// {COUNT} is how many saplings.
    /// </summary>
    public class HappeningWorker_MossSaplings : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Grovekeepers) != null && MossLedger.Felled >= PeoplesEMath.SaplingsAfter && Colonists(map).Count > 0
            && MossPlanting.Cells(map, PeoplesEMath.Saplings(MossLedger.Felled)).Count >= 4;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Mossborn_SeedCarrier"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.variant = MossLedger.Felled;
            ctx.tale = ctx.variant.ToString();
            ctx.count = MossPlanting.Cells(ctx.map, PeoplesEMath.Saplings(ctx.variant)).Count;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Plants) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null && ctx.count > 0;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.variant >= 40) tags.Add("cleared");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "plant" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The seed-carrier plants the saplings ({COUNT}) on open soil round the colony; with the play-outs off, they are there at once.</summary>
    public class DilemmaEffect_MossPlant : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            MossPlanting.Cells(ctx?.map, 1).Count == 0 ? "CC_PeoplesE_NoSoil".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesE_Impact_Saplings".Translate(Mathf.Max(1, ctx?.count ?? 1)).Resolve(), "CC_PeoplesE_Impact_SaplingsBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            var cells = MossPlanting.Cells(map, Mathf.Max(1, ctx.count));
            if (cells.Count == 0) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesE.Up(x, map))
            {
                // Their visit stretched while they plant, and the work itself: one sapling after another.
                x.GetLord()?.RemovePawn(x);
                Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitColony(x.Faction, cells[0], 8 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeoplesE.Start(x, PeoplesE.PlantJob, cells[0], cells.Skip(1).Select(c => (LocalTargetInfo)c).ToList(), PeoplesEMath.PlantTicks)) return;
            }
            for (int i = 0; i < cells.Count; i++) MossPlanting.Planted(x, cells[i], map, i == cells.Count - 1);
        }
    }

    /// <summary>Where the Mossborn's saplings go, and one planted.</summary>
    public static class MossPlanting
    {
        /// <summary>Up to so many cells of open soil round the colony, two apart, the nearest the colony's middle first, out of the home area where there is room.</summary>
        public static List<IntVec3> Cells(Map map, int n)
        {
            var list = new List<IntVec3>();
            if (map == null || n <= 0) return list;
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            var home = map.areaManager.Home;
            foreach (var c in GenRadial.RadialCellsAround(centre, 40f, false))
            {
                if (list.Count >= n) break;
                if (!PeoplesE.SaplingCell(map, c) || home != null && home[c] || list.Any(o => o.DistanceToSquared(c) < 5)) continue;
                list.Add(c);
            }
            return list;
        }

        /// <summary>A sapling planted at the cell (the land's own tree), the colony's planting; the last of them told.</summary>
        public static void Planted(Pawn by, IntVec3 c, Map map, bool last)
        {
            map ??= by?.Map;
            var def = PeoplesE.SaplingFor(map, c);
            PeoplesE.Sapling(map, c, def);
            if (!last || map == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesE_SaplingsPlanted".Translate((by?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(c, map),
                MessageTypeDefOf.PositiveEvent);
            if (by != null && by.Spawned && by.Faction != null && !by.Faction.IsPlayer) Arrivals.SendAway(new List<Pawn> { by });
        }

        /// <summary>The sapling beside a grave: the land's own tree, told.</summary>
        public static void GraveTree(Pawn by, IntVec3 c, Map map = null)
        {
            map ??= by?.Map;
            var p = PeoplesE.Sapling(map, c, PeoplesE.SaplingFor(map, c));
            if (p == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesE_GraveTreePlanted".Translate((by?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(p),
                MessageTypeDefOf.PositiveEvent);
        }
    }

    // ===================== The Mossborn: a tree for the dead =====================

    /// <summary>
    /// One of the colony died within the last days and lies in a grave, and a grove-warden of the Mossborn comes with a
    /// sapling: their dead are buried under saplings so the person becomes the grove, and they would plant one beside this
    /// grave too, with the colony gathered there if it likes. {DEAD} is the dead.
    /// </summary>
    public class HappeningWorker_MossGraveTree : HappeningWorker_Visitors
    {
        /// <summary>A grave of one of the colony who died within the grove's days, with soil beside it for a sapling.</summary>
        public static Building_Grave Grave(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null) return null;
            foreach (var g in HappeningWorker_Setting.Graves(map).Where(g => g.Corpse != null && PeoplesEMath.GroveFresh(g.Corpse.Age)).OrderBy(g => g.Corpse.Age))
            {
                var near = GenAdj.CellsAdjacent8Way(g).Where(c => PeoplesE.SaplingCell(map, c)).ToList();
                if (near.Count == 0) continue;
                cell = near.RandomElement();
                return g;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Grovekeepers) != null && Grave(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            var grave = Grave(ctx.map, out var cell);
            if (ctx.faction == null || grave == null) return false;
            ctx.place = grave;
            ctx.spot = cell;
            var dead = grave.Corpse.InnerPawn;
            ctx.dead = dead?.LabelShort ?? "?";
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Able(ctx.map).OrderByDescending(p => dead != null && p.relations != null ? p.relations.OpinionOf(dead) : 0).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, ctx.place?.Position, ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !PeoplesE.SaplingCell(ctx.map, ctx.spot)) ? "CC_PeoplesE_GraveTreeGone".Translate(ctx.dead ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "grove" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The sapling planted beside the grave (HappeningContext.spot) by the grove-warden; with the play-outs off, at once.</summary>
    public class DilemmaEffect_MossGraveTree : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesE_Impact_GraveTree".Translate((ctx as HappeningContext)?.dead ?? "?").Resolve(), "CC_PeoplesE_Impact_GraveTreeBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null || !hc.spot.IsValid) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesE.Up(x, map) && PeoplesE.Start(x, PeoplesE.GraveTreeJob, hc.spot, null, PeoplesEMath.PlantTicks * 2)) return;
            MossPlanting.GraveTree(x, hc.spot, map);
        }
    }

    // ===================== The Mossborn: the colony's Gauranlen tree tended =====================

    /// <summary>
    /// With Ideology: the colony has a Gauranlen tree connected to one of its people, and a tree-connector of the Mossborn
    /// comes to tend it: they prune it a while, the colony's own connector at their side if the colony likes, and its
    /// connection is the stronger for it (PeoplesEMath.TendStrength). {A} is the colony's connector.
    /// </summary>
    public class HappeningWorker_MossTreeTend : HappeningWorker_Visitors
    {
        /// <summary>The colony's Gauranlen tree with a connection to one of its people, or null.</summary>
        public static Thing Tree(Map map, out Pawn connector)
        {
            connector = null;
            if (!ModsConfig.IdeologyActive || map == null) return null;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeGauranlen");
            if (def == null) return null;
            foreach (var t in map.listerThings.ThingsOfDef(def))
            {
                var comp = t.TryGetComp<CompTreeConnection>();
                if (comp == null || !comp.Connected || comp.ConnectedPawn?.Faction != Faction.OfPlayer || comp.ConnectionStrength >= 0.95f) continue;
                connector = comp.ConnectedPawn;
                return t;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Grovekeepers) != null && Tree(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            ctx.place = Tree(ctx.map, out var connector);
            if (ctx.faction == null || ctx.place == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Plants, new IntRange(10, 16));
            ctx.a = connector != null && connector.Spawned && connector.Map == ctx.map ? connector : PeoplesD.Best(ctx.map, SkillDefOf.Plants);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "CC_PeoplesE_TreeGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "tend" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The tree-connector prunes the colony's Gauranlen tree a while (the event's place); with the play-outs off, at once.</summary>
    public class DilemmaEffect_MossTend : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesE_Impact_Tend".Translate((int)(PeoplesEMath.TendStrength * 100f)).Resolve(), "CC_PeoplesE_Impact_TendBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var tree = (ctx as HappeningContext)?.place;
            var map = ctx?.map;
            if (tree == null || map == null || tree.Destroyed) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesE.Up(x, map) && PeoplesE.Start(x, PeoplesE.TendJob, tree, null, 1500)) return;
            MossTend.Tended(x, tree);
        }
    }

    public static class MossTend
    {
        /// <summary>The tree tended: its connection the stronger, and the colony told.</summary>
        public static void Tended(Pawn by, Thing tree)
        {
            var comp = tree?.TryGetComp<CompTreeConnection>();
            if (comp == null || !comp.Connected) return;
            comp.ConnectionStrength = PeoplesEMath.Tended(comp.ConnectionStrength);
            if (tree.Spawned) FleckMaker.ThrowDustPuff(tree.Position, tree.Map, 0.8f);
            Tell.Message(NewsKind.Land, Importance.Notable, tree.MapHeld,
                "CC_PeoplesE_TreeTended".Translate((by?.LabelShort ?? "?").Named("PAWN"), (comp.ConnectedPawn?.LabelShort ?? "?").Named("CONNECTOR")).Resolve(), new LookTargets(tree),
                MessageTypeDefOf.PositiveEvent);
        }
    }

    // ===================== The Mossborn's ledger: the colony's planting and felling =====================

    /// <summary>
    /// The Mossborn's goodwill follows the colony's trees, a season at a time: the trees its people felled (counted as each
    /// is cut: Patch_MossFelled) against the trees they planted (counted from the trees sown by hand that came up since
    /// the last look: Plant.sown), reckoned every fifteen days from the region's day (PeoplesEMath.Shift). Each Mossborn
    /// people hears of it, warmer or cooler, never an enemy by it (PeoplesEMath.Kept), and the news says what they heard.
    /// The counts are kept in the peoples' book (ArisingBook).
    /// </summary>
    public static class MossLedger
    {
        private static ArisingBook Book => ArisingBook.Instance;

        public static int Felled => Book?.groveFelled ?? 0;

        public static int Planted => Book?.grovePlanted ?? 0;

        /// <summary>A tree cut by one of the colony's people (Patch_MossFelled).</summary>
        public static void Notify_Felled(Plant plant, Pawn by)
        {
            var b = Book;
            if (b == null || plant?.def?.plant == null || !plant.def.plant.IsTree || by?.Faction == null || !by.Faction.IsPlayer) return;
            if (plant.MapHeld == null || !plant.MapHeld.IsPlayerHome) return;
            b.groveFelled++;
        }

        /// <summary>The region's day: the trees sown since the last look counted, and every season the reckoning.</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            int high = b.groveSownId;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                {
                    if (!(t is Plant p) || !p.sown || p.def?.plant == null || !p.def.plant.IsTree || p.thingIDNumber <= b.groveSownId) continue;
                    b.grovePlanted++;
                    high = Mathf.Max(high, p.thingIDNumber);
                }
            b.groveSownId = high;
            if (b.groveSince < 0) b.groveSince = now;
            if (now - b.groveSince < PeoplesEMath.ReckonDays * GenDate.TicksPerDay) return;
            Reckon(b.groveFelled, b.grovePlanted);
            b.groveFelled = 0;
            b.grovePlanted = 0;
            b.groveSince = now;
        }

        /// <summary>A season's reckoning told to each Mossborn people (and their goodwill moved by it).</summary>
        public static void Reckon(int felled, int planted)
        {
            int shift = PeoplesEMath.Shift(felled, planted);
            if (shift == 0) return;
            var map = Find.AnyPlayerHomeMap;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Grovekeepers && !f.defeated && f.HasGoodwill))
            {
                int kept = PeoplesEMath.Kept(f.PlayerGoodwill, shift);
                if (kept == 0) continue;
                WorldSituationWorker.Goodwill(f, kept);
                Tell.Message(NewsKind.World, Importance.Notable, map,
                    (kept > 0 ? "CC_PeoplesE_LedgerWarm" : "CC_PeoplesE_LedgerCool").Translate(f.Name.Named("FACTION"), felled.Named("FELLED"), planted.Named("PLANTED")).Resolve(),
                    LookTargets.Invalid, kept > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
