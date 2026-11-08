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
    // Release F's four peoples at the colony's gate (docs/design/peoples.md, "Release F"; docs/design/peoples-third.md, step
    // 2; the defs in Defs/IncidentDefs/Rimfolk_Events_PeoplesF.xml), each only from a people of its calling at peace with
    // the colony (the late-woken sleeper from any Fleet still standing), and every one played out on the map. Here: the
    // Signal Line's relay mast, its wire-trader and its lineman in the storm, and the Deepwell Union's drill for a share,
    // its goods and its strike. The Last Republic and the Hollow Fleet are in PeoplesF2.cs; the game hooks (the mast's
    // warning of a raid, the strike that stalls the union's trade, a citizen's terms) in PeoplesFPatches.cs. Nothing here
    // keeps a clock or saves anything of its own: the strike, the papers and the conscript's season are visits of the
    // events' tracker (HappeningTracker.visits), looked at on its hour; the mast is a building on the map; the region's
    // day (RegionBook.Daily) calls PeoplesF.Daily for the strikes and the Republic's radio.

    /// <summary>What release F's touchpoints share: their jobs, the work done at their end, the visits they ride on.</summary>
    public static class PeoplesF
    {
        public static JobDef RaiseJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_RaiseMast");

        public static JobDef ClimbJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_ClimbMast");

        public static JobDef DrillJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_Drill");

        public static JobDef CountJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_Count");

        public static JobDef CutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_CutChunk");

        public static JobDef OutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesF_ConscriptOut");

        /// <summary>The Signal Line's relay mast (Defs/Misc/Rimfolk_PeoplesF_Misc.xml).</summary>
        public static ThingDef MastDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_RelayMast");

        public static int Now => Find.TickManager.TicksGame;

        public static List<Visit> Visits => CouncilGameComponent.Instance?.happenings?.visits;

        /// <summary>The running visits of this kind (not done, within their days).</summary>
        public static IEnumerable<Visit> Running(string kind)
        {
            var all = Visits;
            if (all == null) return Enumerable.Empty<Visit>();
            int now = Now;
            return all.Where(v => v != null && v.kind == kind && !v.done && now <= v.until);
        }

        /// <summary>The people a visit was kept for (its text is the people's load id), or null.</summary>
        public static Faction PeopleOf(Visit v) =>
            v?.text == null ? null : Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == v.text.Split('|')[0]);

        /// <summary>A visitor of theirs on the map and up.</summary>
        public static bool Up(Pawn p, Map map) => PeoplesE.Up(p, map);

        /// <summary>A job of release F's own done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target, bool last)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == RaiseJob) RelayMast.Raise(pawn, target.Cell);
                else if (job == ClimbJob) RelayMast.Climbed(pawn, target.Thing);
                else if (job == DrillJob) UnionDrill.Drilled(pawn, target.Cell);
                else if (job == CountJob) Census.Counted(pawn, target.Cell, last);
                else if (job == CutJob) FleetSalvage.Cut(pawn, target.Thing, last);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: work done", e);
            }
        }

        /// <summary>The region's day (RegionBook.Daily, with the deeper game hooks): the union's strikes and the Republic's radio.</summary>
        public static void Daily(int now)
        {
            try
            {
                UnionStrike.Daily(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the union's strikes", e);
            }
            try
            {
                RepublicRadio.Daily(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the Republic's radio", e);
            }
        }

        /// <summary>A line of the event's own with these tags, said over their head.</summary>
        public static void Say(Pawn x, HappeningExtension ext, HappeningContext ctx, params string[] tags) => PeoplesD.Say(x, ext, ctx, tags);

        /// <summary>A line of the event's own with this tag (the first of its kind), said by someone of theirs outside an answer.</summary>
        public static void SayLine(Pawn x, IncidentDef incident, string tag)
        {
            var lines = incident?.GetModExtension<HappeningExtension>()?.lines;
            if (x == null || !x.Spawned || lines == null) return;
            var mine = lines.Where(l => l.StartsWith("[" + tag + "]")).Select(l => l.Substring(l.IndexOf(']') + 1).TrimStart()).ToList();
            string line = Flavor.PickText(mine);
            if (!line.NullOrEmpty()) Speech.Say(x, line);
        }

        /// <summary>A thunderstorm over the map now: weather that sends lightning.</summary>
        public static bool Storm(Map map) =>
            map?.weatherManager?.curWeather?.eventMakers != null && map.weatherManager.curWeather.eventMakers.Any(e => e?.eventClass == typeof(WeatherEvent_LightningStrike));
    }

    /// <summary>
    /// Release F's own work at a cell or a thing, or a queue of them: there, a while at it (sparks or dust, a bar), then
    /// what it was for (PeoplesF.WorkDone); the next in the queue, if any. Its place in the queue isn't kept with the game:
    /// a job loaded halfway starts its current target again.
    /// </summary>
    public class JobDriver_PeoplesF : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("PeoplesFNext");
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
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PeoplesFWork");
            work.tickIntervalAction = delta =>
            {
                var t = job.targetA;
                if (!t.IsValid) return;
                pawn.rotationTracker.FaceCell(t.Cell);
                if (!pawn.IsHashIntervalTick(200, delta)) return;
                if (job.def == PeoplesF.CountJob) return;
                if (job.def == PeoplesF.DrillJob) FleckMaker.ThrowDustPuff(t.Cell, pawn.Map, 0.6f);
                else FleckMaker.ThrowMicroSparks(t.Cell.ToVector3Shifted(), pawn.Map);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("PeoplesFDone");
            done.initAction = () =>
            {
                bool last = job.targetQueueA.NullOrEmpty();
                PeoplesF.WorkDone(pawn, job.def, job.targetA, last);
                job.targetA = LocalTargetInfo.Invalid;
                if (!last) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The Signal Line: the relay mast =====================

    /// <summary>The relay mast's own part (Defs/Misc/Rimfolk_PeoplesF_Misc.xml): what a thunderstorm does to it.</summary>
    public class CompProperties_RelayMast : CompProperties
    {
        public CompProperties_RelayMast()
        {
            compClass = typeof(CompRelayMast);
        }
    }

    /// <summary>
    /// The colony's relay mast in the weather: a thunderstorm wears it a hit point each quarter hour of it
    /// (PeoplesFMath.StormWear); gone under half, it is quiet until it is mended, told once as it goes. Its inspect line
    /// says whether it sings.
    /// </summary>
    public class CompRelayMast : ThingComp
    {
        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!RimfolkPatches.On) return;
            try
            {
                RelayMast.Weather(parent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the mast in the weather", e);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (parent?.Faction == null || !parent.Faction.IsPlayer) return null;
            return (RelayMast.Sings(parent) ? "CC_PeoplesF_MastSings" : "CC_PeoplesF_MastQuiet").Translate().Resolve();
        }
    }

    /// <summary>
    /// The Signal Line's relay mast on the colony's highest ground: where it goes, raising it, a storm's wear and a
    /// lineman's climb, and whether it sings (the colony's own, above half its hit points, a Line at peace to carry its
    /// messages). While it sings the colony's letters reach their readers in half the days (World/NeighbourLetters.cs,
    /// PeoplesFMath.Relayed) and a raid the storyteller sends comes a little after the line's warning
    /// (Patch_LineMastWarns).
    /// </summary>
    public static class RelayMast
    {
        private static readonly Dictionary<int, int> quietTold = new Dictionary<int, int>();

        /// <summary>The colony's mast on this map, or null.</summary>
        public static Thing Of(Map map)
        {
            var def = PeoplesF.MastDef;
            if (map == null || def == null) return null;
            return map.listerThings.ThingsOfDef(def).FirstOrDefault(t => t.Spawned && t.Faction != null && t.Faction.IsPlayer);
        }

        /// <summary>The mast sings: the colony's, above half its hit points, with a Signal Line at peace to carry it.</summary>
        public static bool Sings(Thing mast) =>
            mast != null && mast.Spawned && mast.Faction != null && mast.Faction.IsPlayer && !PeoplesFMath.Quiet(mast.HitPoints, mast.MaxHitPoints)
            && PeoplesD.Friendly(PeoplesMath.Linesmen) != null;

        /// <summary>A home with a singing mast (null map: none).</summary>
        public static bool Working(Map map) => map != null && map.IsPlayerHome && Sings(Of(map));

        /// <summary>A cell the mast can stand on: open ground, under the sky, standable and clear, ground that bears a heavy building.</summary>
        public static bool Fits(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map)) return false;
            if (c.GetFirstBuilding(map) != null || c.GetFirstItem(map) != null || c.GetZone(map) != null || c.GetPlant(map) is Plant p && p.def.plant.IsTree) return false;
            var terrain = c.GetTerrain(map);
            return terrain != null && !terrain.IsWater && terrain.affordances != null && terrain.affordances.Contains(TerrainAffordanceDefOf.Heavy);
        }

        /// <summary>
        /// The colony's highest ground for a mast: between eight and forty cells from its middle, the hillside first (natural
        /// rock near), rough ground next, the nearer the better (PeoplesFMath.SiteScore). Invalid when there is none.
        /// </summary>
        public static IntVec3 Site(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid) return IntVec3.Invalid;
            IntVec3 best = IntVec3.Invalid;
            float score = float.MinValue;
            foreach (var c in GenRadial.RadialCellsAround(mid, PeoplesFMath.MastMaxCells, true))
            {
                float d = c.DistanceTo(mid);
                if (d < PeoplesFMath.MastMinCells || !Fits(map, c)) continue;
                int rock = 0;
                foreach (var n in GenRadial.RadialCellsAround(c, 4f, false))
                    if (n.InBounds(map) && n.GetEdifice(map) is Building b && b.def.building != null && b.def.building.isNaturalRock && ++rock >= 4) break;
                bool rough = (c.GetTerrain(map)?.defName ?? "").Contains("Rough") || (c.GetTerrain(map)?.defName ?? "").Contains("Gravel");
                float s = PeoplesFMath.SiteScore(rock, rough, d);
                if (s <= score) continue;
                score = s;
                best = c;
            }
            return best;
        }

        /// <summary>The mast raised at the cell, the colony's from now on, and the lineman on the road.</summary>
        public static void Raise(Pawn by, IntVec3 c, Map map = null)
        {
            map ??= by?.Map;
            var def = PeoplesF.MastDef;
            if (map == null || def == null || Of(map) != null) return;
            if (!Fits(map, c))
            {
                c = Site(map);
                if (!c.IsValid) return;
            }
            var mast = ThingMaker.MakeThing(def);
            mast.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(mast, c, map);
            FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(), map);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesF_MastRaised".Translate((by?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(mast),
                MessageTypeDefOf.PositiveEvent);
            if (by != null && by.Spawned && by.Faction != null && !by.Faction.IsPlayer) Arrivals.SendAway(new List<Pawn> { by });
        }

        /// <summary>A thunderstorm wears the colony's mast; going quiet, it is told once.</summary>
        public static void Weather(Thing mast)
        {
            if (mast == null || !mast.Spawned || mast.Faction == null || !mast.Faction.IsPlayer || !PeoplesF.Storm(mast.Map)) return;
            bool was = PeoplesFMath.Quiet(mast.HitPoints, mast.MaxHitPoints);
            if (mast.HitPoints > 1) mast.HitPoints = Mathf.Max(1, mast.HitPoints - PeoplesFMath.StormWear);
            if (was || !PeoplesFMath.Quiet(mast.HitPoints, mast.MaxHitPoints)) return;
            int now = PeoplesF.Now;
            if (quietTold.TryGetValue(mast.thingIDNumber, out int at) && now - at < GenDate.TicksPerDay) return;
            quietTold[mast.thingIDNumber] = now;
            Tell.Message(NewsKind.Land, Importance.Notable, mast.Map, "CC_PeoplesF_MastWentQuiet".Translate().Resolve(), new LookTargets(mast), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A lineman's climb in the storm done: the mast made fast and mended to its full strength, singing again.</summary>
        public static void Climbed(Pawn by, Thing mast)
        {
            if (mast == null || mast.Destroyed || !mast.Spawned) return;
            mast.HitPoints = mast.MaxHitPoints;
            FleckMaker.ThrowMicroSparks(mast.DrawPos, mast.Map);
            Tell.Message(NewsKind.Land, Importance.Notable, mast.Map, "CC_PeoplesF_MastKept".Translate((by?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(mast),
                MessageTypeDefOf.PositiveEvent);
            if (by != null && by.Spawned && by.Faction != null && !by.Faction.IsPlayer) Arrivals.SendAway(new List<Pawn> { by });
        }

        /// <summary>
        /// The storyteller sends a raid to a home whose mast sings (Patch_LineMastWarns): it goes into the game's own queue
        /// a couple of hours on (PeoplesFMath.WarnHours), and the line's warning is told now, the mast throwing sparks as it
        /// carries it. True: held (the storyteller doesn't fire it now).
        /// </summary>
        public static bool Warn(FiringIncident fi)
        {
            var map = fi?.parms?.target as Map;
            if (fi?.def?.Worker == null || map == null || !Working(map)) return false;
            bool raid = fi.def.Worker is IncidentWorker_Raid;
            bool threat = fi.def.category == IncidentCategoryDefOf.ThreatBig || fi.def.category == IncidentCategoryDefOf.ThreatSmall;
            if (!PeoplesFMath.Warns(raid, threat, fi.sourceQuestPart != null || fi.parms.quest != null, false)) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms)) return false;
            int at = PeoplesF.Now + PeoplesFMath.WarnHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, PeoplesFMath.WarnRetryHours * GenDate.TicksPerHour))) return false;
            var mast = Of(map);
            if (mast != null) FleckMaker.ThrowMicroSparks(mast.DrawPos, map);
            var line = PeoplesD.Friendly(PeoplesMath.Linesmen);
            string who = fi.parms.faction != null ? fi.parms.faction.Name : "CC_PeoplesF_RaidWarnSomeone".Translate().Resolve();
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PeoplesF_RaidWarnLabel".Translate().Resolve(),
                "CC_PeoplesF_RaidWarn".Translate(who.Named("WHO"), (line?.Name ?? "?").Named("LINE")).Resolve(), LetterDefOf.ThreatSmall,
                mast != null ? new LookTargets(mast) : LookTargets.Invalid, line);
            return true;
        }
    }

    /// <summary>
    /// A lineman of the Signal Line comes to the gate with a mast on a mule: the Line would raise a relay mast on the
    /// colony's highest ground (RelayMast.Site), and from then the colony's letters reach their readers in half the days
    /// and the line warns of a raid a little before it comes. Raised, it is the colony's. Or the colony's builder may stand
    /// by to learn how it is guyed. {TALE} says where it would stand.
    /// </summary>
    public class HappeningWorker_LineMast : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Linesmen) != null && PeoplesF.MastDef != null && RelayMast.Of(map) == null && RelayMast.Site(map).IsValid
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            if (ctx.faction == null) return false;
            ctx.spot = RelayMast.Site(ctx.map);
            if (!ctx.spot.IsValid) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SignalLine_Lineman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            bool hill = GenRadial.RadialCellsAround(ctx.spot, 4f, false).Any(c => c.InBounds(ctx.map) && c.GetEdifice(ctx.map) is Building b && b.def.building?.isNaturalRock == true);
            ctx.tale = (hill ? "CC_PeoplesF_MastHill" : "CC_PeoplesF_MastRise").Translate().Resolve();
            ctx.note = hill ? "hill" : null;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Construction) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "hill") tags.Add("hill");
            if (PeoplesF.Storm(ctx.map)) tags.Add("storm");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "mast" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mast raised where the event found the ground (HappeningContext.spot): the lineman carries it there and raises it; with the play-outs off, at once.</summary>
    public class DilemmaEffect_LineMast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && RelayMast.Of(ctx.map) != null ? "CC_PeoplesF_MastThere".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            sheet.Play("CC_PeoplesF_Impact_Mast".Translate(hc?.tale ?? "?").Resolve(), "CC_PeoplesF_Impact_MastBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null) return;
            var site = hc.spot.IsValid && RelayMast.Fits(map, hc.spot) ? hc.spot : RelayMast.Site(map);
            if (!site.IsValid) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesF.Up(x, map) && x.CanReach(site, PathEndMode.Touch, Danger.Some))
            {
                x.GetLord()?.RemovePawn(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, site, 5 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeoplesE.Start(x, PeoplesF.RaiseJob, site, null, 2 * GenDate.TicksPerHour)) return;
            }
            RelayMast.Raise(x, site, map);
        }
    }

    // ===================== The Signal Line: the wire-trader =====================

    /// <summary>
    /// The colony has components in its stores, and the Line's wire-trader comes to buy them: carried out to them at the
    /// gate and paid for in silver ({COUNT}, a fifth over their worth; {TALE} how many). Or the colony buys the line's
    /// news of a caravan on the road for a little silver, and the caravan comes (the game's own trader caravan, whoever's
    /// it is that the line heard).
    /// </summary>
    public class HappeningWorker_LineWire : HappeningWorker_Visitors
    {
        public static int Components(Map map) => map == null ? 0 : PlayCarry.Have(map, ThingDefOf.ComponentIndustrial.defName, ThingDefOf.ComponentIndustrial);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Linesmen) != null && Components(map) >= PeoplesFMath.WireMin && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            if (ctx.faction == null) return false;
            int have = Components(ctx.map);
            if (have < PeoplesFMath.WireMin) return false;
            ctx.variant = Math.Min(have, PeoplesFMath.WireMax);
            ctx.thing = ThingDefOf.ComponentIndustrial;
            ctx.count = PeoplesFMath.WirePrice(ctx.variant * ThingDefOf.ComponentIndustrial.BaseMarketValue);
            var list = new List<Pawn>
            {
                Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages), Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages)
            };
            ctx.stranger = list[0];
            ctx.group = list.Skip(1).Where(p => p != null).ToList();
            ctx.a = PeoplesD.Able(ctx.map).OrderBy(p => p.Position.DistanceToSquared(PeoplesD.GateSpot(ctx.map) ?? p.Position)).FirstOrDefault();
            ctx.tale = "CC_PeoplesF_WireCount".Translate(ctx.variant).Resolve();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && Components(ctx.map) < ctx.variant ? "CC_PeoplesF_WireGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Sold: the wire-trader pays at the gate; the components go out by the answer's errand.
            if (option == 0 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option == 0 ? "wire" : option == 1 ? "news" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Signal Line: the lineman in the storm =====================

    /// <summary>
    /// A thunderstorm is wearing the colony's mast, and a lineman of the Line comes up the road through it to climb the
    /// mast and keep it singing: the climb (sparks and a bar at the mast's foot), then the mast made fast and mended to
    /// its full strength. Or with the colony's builder holding the line at its foot, learning. {TALE} is how the mast
    /// stands.
    /// </summary>
    public class HappeningWorker_LineStorm : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var mast = RelayMast.Of(map);
            return PeoplesD.Friendly(PeoplesMath.Linesmen) != null && mast != null && mast.HitPoints < mast.MaxHitPoints && PeoplesF.Storm(map) && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            ctx.place = RelayMast.Of(ctx.map);
            if (ctx.faction == null || ctx.place == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SignalLine_Lineman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            bool quiet = PeoplesFMath.Quiet(ctx.place.HitPoints, ctx.place.MaxHitPoints);
            ctx.note = quiet ? "quiet" : null;
            ctx.tale = (quiet ? "CC_PeoplesF_MastQuietNow" : "CC_PeoplesF_MastWearing").Translate().Resolve();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Construction) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "quiet") tags.Add("quiet");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "CC_PeoplesF_MastGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "climb" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The lineman climbs the colony's mast in the storm and makes it fast (RelayMast.Climbed); with the play-outs off, at once.</summary>
    public class DilemmaEffect_LineClimb : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesF_Impact_Climb".Translate().Resolve(), "CC_PeoplesF_Impact_ClimbBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var mast = (ctx as HappeningContext)?.place;
            if (map == null || mast == null || mast.Destroyed || !mast.Spawned) return;
            var x = ctx.stranger;
            if (PlayOuts.On && PeoplesF.Up(x, map) && x.CanReach(mast, PathEndMode.Touch, Danger.Some))
            {
                x.GetLord()?.RemovePawn(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, mast.Position, 4 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeoplesE.Start(x, PeoplesF.ClimbJob, mast, null, 1500)) return;
            }
            RelayMast.Climbed(x, mast);
        }
    }

    // ===================== The Deepwell Union: the drill for a share =====================

    /// <summary>
    /// The colony's ground holds a deep seam (the game's own deep resources), and a drill crew of the Deepwell Union comes
    /// to drill it for a share: the driller works the seam for six hours where it lies (a bar and dust) while the crew
    /// stands by, then what came up ({COUNT} of {THING}) is split, the union's two in five into their packs and the
    /// colony's ({TALE}) left at the drill, and the seam is the less by it. The colony's hand may work the drill beside
    /// them and learn, and one shift in twenty a mishap hurts them, through the harm setting and the shared budget
    /// (HarmKinds.UnionDrill), told when it lands and said in the answer.
    /// </summary>
    public class HappeningWorker_UnionDrill : HappeningWorker_Visitors
    {
        /// <summary>The deep seam nearest the colony's middle, within reach and in the open: its cell, its ore and how much lies there round it.</summary>
        public static IntVec3 Seam(Map map, out ThingDef ore, out int deep)
        {
            ore = null;
            deep = 0;
            var grid = map?.deepResourceGrid;
            if (grid == null) return IntVec3.Invalid;
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid) return IntVec3.Invalid;
            IntVec3 best = IntVec3.Invalid;
            float bestD = float.MaxValue;
            foreach (var c in map.AllCells)
            {
                if (grid.ThingDefAt(c) == null || grid.CountAt(c) <= 0 || c.Fogged(map) || !c.Standable(map) || c.Roofed(map)) continue;
                float d = c.DistanceToSquared(mid);
                if (d >= bestD) continue;
                bestD = d;
                best = c;
            }
            if (!best.IsValid) return best;
            ore = grid.ThingDefAt(best);
            foreach (var c in Cluster(map, best, ore)) deep += grid.CountAt(c);
            return best;
        }

        /// <summary>The seam's cells round its spot: the same ore within three cells.</summary>
        public static List<IntVec3> Cluster(Map map, IntVec3 at, ThingDef ore) =>
            GenRadial.RadialCellsAround(at, 3f, true).Where(c => c.InBounds(map) && map.deepResourceGrid.ThingDefAt(c) == ore && map.deepResourceGrid.CountAt(c) > 0).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Drillers) != null && !UnionStrike.Striking(PeoplesD.Friendly(PeoplesMath.Drillers)) && Seam(map, out _, out _).IsValid
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (ctx.faction == null || UnionStrike.Striking(ctx.faction)) return false;
            ctx.spot = Seam(ctx.map, out var ore, out int deep);
            if (!ctx.spot.IsValid || ore == null) return false;
            ctx.thing = ore;
            ctx.count = PeoplesFMath.Yield(deep, ore.BaseMarketValue, LivingEvents.WealthFactor(ctx.map));
            ctx.variant = PeoplesFMath.ColonyShare(ctx.count);
            ctx.tale = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Captain"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = Rand.RangeInclusive(1, 2);
            for (int i = 0; i < n; i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Miner"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Mining, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining)) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map.deepResourceGrid.AnyActiveDeepScannersOnMap()) tags.Add("scanned");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "drill" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The drill for a share taken: the crew gathers at the seam, the driller works it six hours (with <see cref="together"/>,
    /// the colony's hand works it beside them), and what came up is split there (UnionDrill.Drilled). With the play-outs off,
    /// at once.
    /// </summary>
    public class DilemmaEffect_UnionDrill : DilemmaEffect
    {
        public bool together;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.thing == null) return;
            sheet.Play("CC_PeoplesF_Impact_Drill".Translate(hc.count, hc.thing.label, hc.variant).Resolve(), "CC_PeoplesF_Impact_DrillBrief".Translate().Resolve());
            if (together && hc.a != null && Harm.On)
                sheet.Add(ImpactKind.Health, -1, "CC_PeoplesF_Impact_DrillRisk".Translate(hc.a.LabelShort).Resolve(), "CC_PeoplesF_Impact_DrillRiskBrief".Translate().Resolve(), "drillrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null || !hc.spot.IsValid || hc.thing == null) return;
            var x = ctx.stranger;
            UnionDrill.Pending(x, hc, together);
            if (PlayOuts.On && PeoplesF.Up(x, map) && x.CanReach(hc.spot, PathEndMode.Touch, Danger.Some))
            {
                var crew = hc.group.Where(p => PeoplesF.Up(p, map)).ToList();
                PeoplesD.Gather(crew.Prepend(x).ToList(), x.Faction, hc.spot, PeoplesFMath.DrillHours + 2);
                if (PeoplesE.Start(x, PeoplesF.DrillJob, hc.spot, null, PeoplesFMath.DrillHours * GenDate.TicksPerHour))
                {
                    var a = hc.a;
                    if (together && a != null && a.Spawned && a.Map == map && !a.Downed && !a.Drafted && !a.InMentalState)
                        PeoplesE.Start(a, PeoplesF.DrillJob, hc.spot, null, PeoplesFMath.DrillHours * GenDate.TicksPerHour);
                    return;
                }
            }
            UnionDrill.Drilled(x, hc.spot);
        }
    }

    /// <summary>The union's drill: what is owed when the shift ends, the split, and the mishap for the colony's hand at the drill.</summary>
    public static class UnionDrill
    {
        private sealed class Shift
        {
            public Map map;
            public ThingDef ore;
            public int total;
            public Pawn hand;
            public IncidentDef incident;
        }

        /// <summary>The drills under way this session, by the driller's id (a drill loaded halfway brings nothing up; the crew goes home on its hours).</summary>
        private static readonly Dictionary<int, Shift> shifts = new Dictionary<int, Shift>();

        public static void Pending(Pawn driller, HappeningContext ctx, bool together)
        {
            if (driller == null || ctx?.thing == null) return;
            shifts[driller.thingIDNumber] = new Shift { map = ctx.map, ore = ctx.thing, total = ctx.count, hand = together ? ctx.a : null, incident = ctx.incident };
        }

        /// <summary>
        /// A shift at the drill done. The union's driller: what came up split at the seam (the colony's share in stacks there,
        /// the union's into the driller's pack), the seam the less by it, told; then the crew goes home. The colony's hand:
        /// one shift in twenty a mishap (HarmKinds.UnionDrill), told.
        /// </summary>
        public static void Drilled(Pawn pawn, IntVec3 at)
        {
            if (pawn == null) return;
            var map = pawn.MapHeld;
            if (pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                Hand(pawn);
                return;
            }
            if (!shifts.TryGetValue(pawn.thingIDNumber, out var s) || map == null || s.ore == null) return;
            shifts.Remove(pawn.thingIDNumber);
            // The seam the less by what came up, cell by cell.
            int left = s.total;
            foreach (var c in HappeningWorker_UnionDrill.Cluster(map, at, s.ore))
            {
                if (left <= 0) break;
                int here = map.deepResourceGrid.CountAt(c);
                int take = Mathf.Min(here, left);
                map.deepResourceGrid.SetAt(c, here - take > 0 ? s.ore : null, here - take);
                left -= take;
            }
            int got = s.total - left;
            int ours = PeoplesFMath.ColonyShare(got), theirs = got - ours;
            foreach (var t in Couriers.Make(s.ore, ours)) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            if (theirs > 0)
                foreach (var t in Couriers.Make(s.ore, theirs))
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(t))
                        t.Destroy();
            FleckMaker.ThrowDustPuff(at, map, 1.2f);
            PeoplesF.SayLine(pawn, s.incident, "done");
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesF_Drilled".Translate(got, s.ore.label, ours, (pawn.Faction?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(at, map), MessageTypeDefOf.PositiveEvent);
            var crew = pawn.GetLord()?.ownedPawns?.ToList() ?? new List<Pawn> { pawn };
            Arrivals.SendAway(crew);
        }

        /// <summary>The colony's hand after their shift at the drill (what they learned is the answer's own): the chance of a mishap.</summary>
        private static void Hand(Pawn a)
        {
            if (!Harm.Roll(a, HarmKinds.UnionDrill, PeoplesFMath.AccidentChance)) return;
            var wound = Harm.Injure(a, Rand.Bool ? HurtKind.Crush : HurtKind.Bruise, HurtWhere.Foot, PeoplesFMath.AccidentLow, PeoplesFMath.AccidentHigh, HarmKinds.UnionDrill);
            if (wound == null) return;
            Tell.Message(NewsKind.People, Importance.Important, a.Map, "CC_PeoplesF_DrillMishap".Translate(a.Named("PAWN"), (wound.Part?.Label ?? "").Named("PART")).Resolve(),
                new LookTargets(a), MessageTypeDefOf.NegativeHealthEvent);
        }
    }

    // ===================== The Deepwell Union: the assayer's goods =====================

    /// <summary>
    /// The union's assayer comes to the gate with a cart: a crate of steel, components and blasting goods (high-explosive
    /// shells, and Settlers' dynamite where it is loaded) for {COUNT} silver, carried out to them and the crate set down at
    /// the gate. Or the assayer walks the colony's ground with its best miner and says what lies under it. {TALE} is what
    /// is in the crate.
    /// </summary>
    public class HappeningWorker_UnionGoods : HappeningWorker_Visitors
    {
        /// <summary>The crate by the colony's wealth: steel, components and three shells, a stick of dynamite with Settlers.</summary>
        public static List<(ThingDef def, int count)> Crate(Map map)
        {
            float wf = LivingEvents.WealthFactor(map);
            var list = new List<(ThingDef, int)> { (ThingDefOf.Steel, PeoplesFMath.CrateSteel(wf)), (ThingDefOf.ComponentIndustrial, PeoplesFMath.CrateComponents(wf)) };
            var shell = DefDatabase<ThingDef>.GetNamedSilentFail("Shell_HighExplosive");
            if (shell != null) list.Add((shell, 3));
            var dynamite = DefDatabase<ThingDef>.GetNamedSilentFail("VFES_Weapon_GrenadeDynamite");
            if (dynamite != null) list.Add((dynamite, 1));
            return list;
        }

        /// <summary>The crate as the event keeps it (its note: "Steel:60;ComponentIndustrial:4"), and read back.</summary>
        public static string Write(List<(ThingDef def, int count)> crate) => string.Join(";", crate.Select(c => c.def.defName + ":" + c.count));

        public static List<(ThingDef def, int count)> Read(string note)
        {
            var list = new List<(ThingDef, int)>();
            foreach (var part in (note ?? "").Split(';'))
            {
                var bits = part.Split(':');
                if (bits.Length != 2 || !int.TryParse(bits[1], out int n) || n <= 0) continue;
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(bits[0]);
                if (def != null) list.Add((def, n));
            }
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            return f != null && !UnionStrike.Striking(f) && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (ctx.faction == null || UnionStrike.Striking(ctx.faction)) return false;
            var crate = Crate(ctx.map);
            ctx.note = Write(crate);
            ctx.count = PeoplesFMath.GoodsPrice(crate.Sum(c => c.def.BaseMarketValue * c.count));
            ctx.tale = TextUtil.JoinAnd(crate.Select(c => Couriers.Amount(c.count, null, c.def)).ToList());
            var list = new List<Pawn>
            {
                Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages), Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Miner"), ctx.faction, ext.ages)
            };
            ctx.stranger = list[0];
            ctx.group = list.Skip(1).Where(p => p != null).ToList();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Mining) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            HappeningWorker_UnionDrill.Seam(ctx.map, out var ore, out _);
            if (ore != null) tags.Add("seam");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option == 0 ? "sold" : option == 1 ? "ground" : "thanks");
            if (option == 1 && !byDefault)
            {
                HappeningWorker_UnionDrill.Seam(ctx.map, out var ore, out int deep);
                if (ore != null && ctx.a != null)
                    Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_PeoplesF_GroundRead".Translate(ctx.a.Named("PAWN"), ore.label.Named("ORE"), deep.Named("COUNT")).Resolve(),
                        new LookTargets(ctx.a), MessageTypeDefOf.NeutralEvent);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The assayer's crate handed over at the gate ({TALE}): the goods set down by the assayer.</summary>
    public class DilemmaEffect_UnionGoods : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            foreach (var (def, n) in HappeningWorker_UnionGoods.Read(hc.note)) sheet.Goods(1, def, n);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null) return;
            var goods = HappeningWorker_UnionGoods.Read(hc.note).SelectMany(c => Couriers.Make(c.def, c.count)).ToList();
            if (goods.Count == 0) return;
            var x = ctx.stranger;
            Couriers.Loot(goods, map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x);
        }
    }

    // ===================== The Deepwell Union: the strike =====================

    /// <summary>
    /// A strike of the Deepwell Union, called by vote: a visit of the events' tracker (kind "unionStrike") for its days,
    /// its text the union's load id, done when it is settled. While it runs their trade stalls: no caravan of theirs comes
    /// (Patch_UnionStrikeCaravans), their visitors bring nothing to sell (Patch_UnionStrikeVisitors) and their pits don't
    /// trade (Patch_UnionStrikeTown). It ends by their own vote after a month, or sooner when the colony helps settle it at
    /// the gate (Rimfolk_Evt_UnionStrike).
    /// </summary>
    public static class UnionStrike
    {
        public const string Kind = "unionStrike";

        public static Visit Of(Faction f)
        {
            if (f == null) return null;
            string id = f.GetUniqueLoadID();
            return PeoplesF.Running(Kind).FirstOrDefault(v => v.text == id);
        }

        public static bool Striking(Faction f) => f != null && Peoples.CallingOf(f) == PeoplesMath.Drillers && Of(f) != null;

        /// <summary>The region's day: each Union people looked at on its day of the season; one look in seven calls a strike.</summary>
        public static void Daily(int now)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var home = HappeningTracker.HomeMap();
            if (t == null || home == null) return;
            int day = now / GenDate.TicksPerDay;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.defeated && !f.IsPlayer && Peoples.CallingOf(f) == PeoplesMath.Drillers).ToList())
            {
                if (!PeoplesFMath.StrikeDay(day, f.loadID) || Striking(f) || !Rand.Chance(PeoplesFMath.StrikeChance)) continue;
                Call(f, home, now);
            }
        }

        /// <summary>The strike called at the union's pit nearest the colony: the visit, and the news.</summary>
        public static void Call(Faction f, Map home, int now)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || f == null || home == null || Striking(f)) return;
            var v = t.AddVisit(Kind, null, home, null, null, null, now + PeoplesFMath.StrikeDays * GenDate.TicksPerDay);
            v.text = f.GetUniqueLoadID();
            v.due = now;
            var town = WorldSituationWorker.NearestTown(f, home);
            Tell.Letter(NewsKind.World, Importance.Notable, home, "CC_PeoplesF_StrikeCalledLabel".Translate(f.Name).Resolve(),
                "CC_PeoplesF_StrikeCalled".Translate(f.Name.Named("FACTION"), (town?.Name ?? f.Name).Named("TOWN")).Resolve(), LetterDefOf.NeutralEvent,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
        }

        /// <summary>The strike's hour: it ends by the union's own vote when its days are out, told; gone with its people.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            var f = PeoplesF.PeopleOf(v);
            if (f == null || f.defeated) return false;
            if (now <= v.until) return true;
            Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PeoplesF_StrikeEnded".Translate(f.Name).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            return false;
        }

        /// <summary>The strike settled with the colony's help: done, the union thinks the better of the colony, told.</summary>
        public static void Settle(Faction f, Map map, Pawn by)
        {
            var v = Of(f);
            if (v == null) return;
            v.done = true;
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.SettledGoodwill);
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Evt_StrikeSettled");
            if (thought != null && by != null && by.Faction != null && by.Faction.IsPlayer) CouncilGameComponent.GiveThought(by, thought);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_StrikeSettled".Translate(f.Name).Resolve(), by != null ? new LookTargets(by) : LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A shift-captain of the union on strike comes to the gate for help: food for the picket line ({COUNT}), carried out
    /// to them, or the colony's best talker sitting with them a while to talk it through; either settles it (the union
    /// votes to go back once it can hold out no longer, or once it has a deal). Or the gate stays shut, and the strike goes on.
    /// </summary>
    public class HappeningWorker_UnionStrike : HappeningWorker_Visitors
    {
        private static Faction Striker() =>
            Arrivals.Friendly(f => PeoplesD.Theirs(f, PeoplesMath.Drillers) && UnionStrike.Of(f) is Visit v
                                   && PeoplesF.Now - v.due >= PeoplesFMath.StrikeAskAfterDays * GenDate.TicksPerDay);

        protected override bool CanHappen(Map map, HappeningExtension ext) => Striker() != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Striker();
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Captain"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int pickets = Rand.RangeInclusive(3, 8);
            ctx.count = PeoplesFMath.PicketFood(pickets);
            ctx.variant = pickets * 10;
            var town = WorldSituationWorker.NearestTown(ctx.faction, ctx.map);
            ctx.placeName = town?.Name ?? ctx.faction.Name;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "settled" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The strike settled with the colony's help (UnionStrike.Settle): the trade moves again.</summary>
    public class DilemmaEffect_UnionSettle : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            if (f == null) return;
            sheet.Play("CC_PeoplesF_Impact_Settle".Translate(f.Name).Resolve(), "CC_PeoplesF_Impact_SettleBrief".Translate().Resolve());
            if (f.HasGoodwill && !f.IsPlayer) sheet.Goodwill(f, PeoplesFMath.SettledGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.faction == null) return;
            UnionStrike.Settle(ctx.faction, ctx.map, ctx.a);
        }
    }
}
