using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 9. Grazing across the land (the herding folk) =====================

    /// <summary>
    /// Grazing across the land (item 9): a drove of twenty to forty head asks to cross the colony's land and graze there for
    /// two or three days. The herd walks in from one edge and grazes its way across, while the drovers' fire burns at the far
    /// edge, where they sleep. Allowed: they leave a young beast of the drove (or hides and milk) at their fire as they go,
    /// and the colony's best hand at beasts rides with the drove for a day (a lesson). Unfenced fields by the herd's ground
    /// lose some crops (the game's own fences keep it out), told on the card and when it happens; rarely, a spooked beast
    /// kicks one of the colony who comes close (the drovers' bull, through the harm budget). Or the colony charges a toll
    /// (they pay, and think less of it), or turns them away (they go round, and think less still). On foot in every age;
    /// with Giddy-Up the drovers ride. The great herds' walking groups made a drove: a group of the herders' own
    /// (<see cref="LordJob_Drove"/>) whose ground moves across the map a little each hour.
    /// </summary>
    public static class Drove
    {
        public const string Kind = "pev:drove";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The drove's beasts by the land: the kinds that stand the season here, the land's own likelier.</summary>
        public static PawnKindDef KindFor(Map map)
        {
            if (map == null) return null;
            var land = GreatHerds.LandAnimals(map);
            var pool = PeopleEvents3Math.DroveKinds.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)).ToList();
            return pool.RandomElementByWeightWithFallback(k => land.Contains(k) ? 3f : 1f);
        }

        /// <summary>Where the herd comes in and where the drovers' fire burns: an edge, and the farthest reachable cell of the edge across from it.</summary>
        public static bool Edges(Map map, out IntVec3 from, out IntVec3 to)
        {
            to = IntVec3.Invalid;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out from, map, CellFinder.EdgeRoadChance_Animal)) return false;
            var start = from;
            for (int i = 0; i < 10; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReach(start, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.NoPassClosedDoors).WithFenceblocked(true)),
                        map, CellFinder.EdgeRoadChance_Ignore, out var c2)) break;
                if (!to.IsValid || c2.DistanceToSquared(start) > to.DistanceToSquared(start)) to = c2;
            }
            return to.IsValid && to.DistanceToSquared(from) > 60 * 60;
        }

        /// <summary>The herd's ground at a share of its days: along the line from where it came in to the drovers' fire.</summary>
        public static IntVec3 Ground(Map map, IntVec3 from, IntVec3 to, float share)
        {
            float s = PeopleEvents3Math.DroveShare(share);
            var at = new IntVec3(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, s)), 0, Mathf.RoundToInt(Mathf.Lerp(from.z, to.z, s))).ClampInsideMap(map);
            return CellFinder.TryFindRandomCellNear(at, map, 8, c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map), out var near) ? near : at;
        }

        private static bool Fenced(Map map, IntVec3 from, IntVec3 field) =>
            !map.reachability.CanReach(from, field, PathEndMode.Touch, TraverseParms.For(TraverseMode.NoPassClosedDoors).WithFenceblocked(true));

        /// <summary>The crops of unfenced fields within the herd's reach of a spot (the game's fences keep a herd out).</summary>
        public static List<Plant> CropsNear(Map map, IntVec3 at)
        {
            var list = new List<Plant>();
            if (map == null || !at.IsValid) return list;
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                if (zone.cells.Count == 0 || !zone.Cells.Any(c => c.InHorDistOf(at, PeopleEvents3Math.GrazeReach))) continue;
                var edge = zone.Cells.Where(c => c.InHorDistOf(at, PeopleEvents3Math.GrazeReach)).OrderBy(c => c.DistanceToSquared(at)).First();
                if (Fenced(map, at, edge)) continue;
                foreach (var c in zone.Cells)
                    if (c.InHorDistOf(at, PeopleEvents3Math.GrazeReach) && c.GetPlant(map) is Plant p && p.def.plant.Harvestable && p.Growth > 0.2f)
                        list.Add(p);
            }
            return list;
        }

        /// <summary>The crops at risk along the drove's way (for the card): the unfenced fields within reach of its line.</summary>
        public static int CropsAtRisk(Map map, IntVec3 from, IntVec3 to)
        {
            if (map == null || !from.IsValid || !to.IsValid) return 0;
            var seen = new HashSet<Plant>();
            for (int i = 1; i <= 7; i++)
            {
                float s = PeopleEvents3Math.DroveShare(i / 8f);
                var at = new IntVec3(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, s)), 0, Mathf.RoundToInt(Mathf.Lerp(from.z, to.z, s))).ClampInsideMap(map);
                foreach (var p in CropsNear(map, at)) seen.Add(p);
            }
            return seen.Count;
        }

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        private static IntVec3 Cell(string s)
        {
            var b = (s ?? "").Split(',');
            return b.Length == 2 && int.TryParse(b[0], out int x) && int.TryParse(b[1], out int z) ? new IntVec3(x, 0, z) : IntVec3.Invalid;
        }

        private static string Cell(IntVec3 c) => c.x + "," + c.z;

        /// <summary>
        /// The drove comes: the herd (a young one among it) and the drovers walk in at one edge, their fire is laid at the far
        /// one, and they graze across the land for their days. <paramref name="free"/>: let cross as neighbours (the gift and the
        /// ride); else for the toll.
        /// </summary>
        public static Visit Begin(HappeningContext ctx, bool free)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var f = ctx?.faction;
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ctx?.dead ?? "") ?? KindFor(map);
            if (map == null || t == null || f == null || kind == null || !Edges(map, out var from, out var to)) return null;
            var all = new List<Pawn>();
            try
            {
                // The drover who spoke at the gate walks back to the herd; the drovers bring it in at the edge.
                Arrivals.SendAway(PeoplesG.Party(ctx));
                var drovers = new List<Pawn>();
                for (int i = 0; i < 3; i++)
                {
                    var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Herders_Drover"), f, new FloatRange(16f, 60f));
                    if (p != null) drovers.Add(p);
                }
                int head = Mathf.Clamp(ctx.count, PeopleEvents3Math.DroveMin, PeopleEvents3Math.DroveMax);
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 1f : kind.RaceProps.lifeStageAges.Last().minAge;
                var rot = Rot4.FromAngleFlat((map.Center - from).AngleFlat);
                foreach (var d in drovers)
                {
                    Hosting.Provision(d, PeopleEvents3Math.DroveMaxDays + 1f);
                    GenSpawn.Spawn(d, CellFinder.RandomClosewalkCellNear(from, map, 6), map, rot);
                    all.Add(d);
                }
                for (int i = 0; i < head; i++)
                {
                    bool young = i < 2;
                    var a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        fixedBiologicalAge: young ? Mathf.Max(0.1f, grown * Rand.Range(0.5f, 0.85f)) : grown + Rand.Range(0.3f, kind.RaceProps.lifeExpectancy * 0.4f)));
                    a.inventory?.DestroyAll();
                    GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(from, map, 10), map, rot);
                    all.Add(a);
                }
                // With Giddy-Up the drovers ride, on horses of their own.
                var horse = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
                if (GiddyUpMounts.On && horse != null && GiddyUpMounts.Ridable(horse.race))
                    foreach (var d in drovers.Take(2))
                    {
                        var h = PawnGenerator.GeneratePawn(new PawnGenerationRequest(horse, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                            fixedBiologicalAge: horse.RaceProps.lifeExpectancy * 0.3f));
                        GenSpawn.Spawn(h, CellFinder.RandomClosewalkCellNear(d.Position, map, 2), map);
                        all.Add(h);
                        GiddyUpMounts.MountNow(d, h);
                    }
                // The drovers' fire at the far edge.
                Thing fire = null;
                var fireDef = DefDatabase<ThingDef>.GetNamedSilentFail("Campfire");
                if (fireDef != null && CellFinder.TryFindRandomCellNear(to, map, 10, c => c.Standable(map) && c.GetEdifice(map) == null && !c.Roofed(map) && !c.Fogged(map)
                                                                                         && c.GetFirstItem(map) == null && c.DistanceToEdge(map) >= 4, out var fc))
                {
                    fire = ThingMaker.MakeThing(fireDef);
                    fire.SetFaction(f);
                    fire = GenSpawn.Spawn(fire, fc, map);
                    fire.TryGetComp<CompRefuelable>()?.Refuel(20f);
                }
                var lord = LordMaker.MakeNewLord(f, new LordJob_Drove(from, fire?.Position ?? to), map, all);
                float days = Rand.Range(PeopleEvents3Math.DroveMinDays, PeopleEvents3Math.DroveMaxDays);
                var v = t.AddVisit(Kind, ctx.incident, map, drovers.FirstOrDefault(), free ? ctx.a : null, all, Now + Mathf.RoundToInt((days + 1f) * GenDate.TicksPerDay));
                v.place = fire;
                v.due = Now + Mathf.RoundToInt(days * GenDate.TicksPerDay);
                v.stake = 0;
                v.text = f.GetUniqueLoadID() + "|" + Cell(from) + "|" + Cell(to) + "|" + Now + "|" + (free ? "free" : "toll") + "|-";
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv3_DroveIn".Translate(f.Name.Named("FACTION"), head.Named("COUNT"), kind.GetLabelPlural().Named("KIND")).Resolve(),
                    new LookTargets(all.FirstOrDefault(p => !p.RaceProps.Humanlike) ?? all.FirstOrDefault()), MessageTypeDefOf.NeutralEvent);
                return v;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the drove", e);
                return null;
            }
        }

        private static string Flag(Visit v) => Parts(v).Length > 5 ? Parts(v)[5] : "-";

        private static bool Has(Visit v, char c) => Flag(v).IndexOf(c) >= 0;

        private static void Mark(Visit v, char c)
        {
            var p = Parts(v);
            if (p.Length < 6 || p[5].IndexOf(c) >= 0) return;
            p[5] = p[5] + c;
            v.text = string.Join("|", p);
        }

        /// <summary>The drove's hour: its ground moved on, the night by the fire, a field grazed, a kick, the ride, and its going.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var map = v.map;
            var p = Parts(v);
            var f = PeoplesF.PeopleOf(v);
            var lord = v.group.Select(x => x?.GetLord()).FirstOrDefault(l => l?.LordJob is LordJob_Drove);
            var here = v.group.Where(x => x != null && !x.Dead && x.Spawned && x.Map == map).ToList();
            if (map == null || p.Length < 6 || here.Count == 0 || lord == null)
            {
                v.done = true;
                Gone(v);
                return false;
            }
            var from = Cell(p[1]);
            var to = Cell(p[2]);
            int.TryParse(p[3], out int start);
            bool free = p[4] == "free";
            var job = (LordJob_Drove)lord.LordJob;
            int hour = GenLocalDate.HourOfDay(map);
            if (v.stake == 0)
            {
                float share = v.due > start ? Mathf.Clamp01((now - start) / (float)(v.due - start)) : 1f;
                job.point = Ground(map, from, to, share);
                job.night = hour >= 20 || hour < 6;
                (lord.CurLordToil as LordToil_Drove)?.UpdateAllDuties();
                if (v.place is Building fire && fire.Spawned) fire.TryGetComp<CompRefuelable>()?.Refuel(2f);
                if (hour == 12 && !Has(v, 'g')) Graze(v, map, job.point, f);
                Kick(v, map, here);
                if (free) Ride(v, map, job.point, hour, now, start);
                if (now >= v.due)
                {
                    if (free) Gift(v, map, f, here);
                    v.stake = 1;
                    lord.ReceiveMemo(LordJob_Drove.MemoOn);
                    if (v.x != null && v.x.Spawned) PeoplesG.SayLine(v.x, v.incident, "on");
                }
                return true;
            }
            return true;
        }

        /// <summary>The herd at the colony's unfenced crops near its ground: a fifth of those within reach grazed down, once a day at most, told.</summary>
        private static void Graze(Visit v, Map map, IntVec3 at, Faction f)
        {
            var crops = CropsNear(map, at);
            if (crops.Count == 0) return;
            int n = PeopleEvents3Math.Grazed(crops.Count);
            foreach (var plant in crops.InRandomOrder().Take(n))
            {
                plant.Growth = Mathf.Min(plant.Growth, 0.05f);
                plant.DirtyMapMesh(map);
            }
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv3_DroveGrazed".Translate((f?.Name ?? "?").Named("FACTION"), n.Named("COUNT")).Resolve(),
                new LookTargets(new TargetInfo(crops[0].Position, map)), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Once a drove at most: one of the colony too close to a big beast of it, and it kicks (the drovers' bull, through the harm budget), shown.</summary>
        private static void Kick(Visit v, Map map, List<Pawn> here)
        {
            if (Has(v, 'k') || !Harm.On || !Harm.SystemOn(HarmKinds.DroversBull.system)) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var beast = here.FirstOrDefault(a => a.RaceProps.Animal && !a.Downed && a.BodySize >= 1f && a.Position.InHorDistOf(p.Position, 3f));
                if (beast == null || !Together.Able(p)) continue;
                Mark(v, 'k');
                var cls = p.DevelopmentalStage.Juvenile() ? HurtClass.Mild : HurtClass.Injury;
                if (!Harm.Roll(p, HarmKinds.DroversBull, PeopleEvents3Math.KickChance, cls)) return;
                var h = Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Leg : HurtWhere.Torso, cls == HurtClass.Mild ? 1f : 2f, cls == HurtClass.Mild ? 3f : 6f, HarmKinds.DroversBull, cls, beast);
                if (h == null) return;
                FleckMaker.ThrowDustPuff(beast.Position, map, 1.2f);
                Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv3_DroveKick".Translate(p.Named("PAWN"), beast.Named("ANIMAL"), (h.Part?.Label ?? "").Named("PART")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
                return;
            }
        }

        /// <summary>The colony's best hand at beasts rides with the drove for a day (its second): out with it from morning to evening, a lesson at the end.</summary>
        private static void Ride(Visit v, Map map, IntVec3 at, int hour, int now, int start)
        {
            var a = v.a;
            if (a == null || a.Dead || Has(v, 'r') || now - start < GenDate.TicksPerDay * 0.6f) return;
            if (hour == 18 && Has(v, 's'))
            {
                Mark(v, 'r');
                try
                {
                    a.skills?.Learn(SkillDefOf.Animals, PeopleEvents3Math.RideXp, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: the ride's lesson", e);
                }
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv3_RideDone".Translate(a.Named("PAWN"), (PeoplesF.PeopleOf(v)?.Name ?? "?").Named("FACTION")).Resolve(),
                    a.Spawned ? new LookTargets(a) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (hour < 7 || hour > 17 || !a.Spawned || a.Map != map || a.Downed || a.Drafted || a.InMentalState || a.CurJobDef == JobDefOf.Ingest) return;
            if (!Has(v, 's'))
            {
                Mark(v, 's');
                var mount = GiddyUpMounts.On ? OwnRiders.MountFor(map, a) : null;
                if (mount != null) GiddyUpMounts.MountNow(a, mount);
                if (v.x != null && v.x.Spawned) PeoplesG.SayLine(v.x, v.incident, "ride");
            }
            // With the herd all day: out on its ground, walking its edge.
            if (!CellFinder.TryFindRandomCellNear(at, map, 10, c => c.Standable(map) && a.CanReach(c, PathEndMode.OnCell, Danger.Some), out var spot)) return;
            var job = JobMaker.MakeJob(JobDefOf.GotoWander, spot);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Their thanks as they go: a young beast of the drove handed over at their fire, else hides and milk left there.</summary>
        private static void Gift(Visit v, Map map, Faction f, List<Pawn> here)
        {
            var fire = v.place != null && v.place.Spawned ? v.place.Position : here.FirstOrDefault()?.Position ?? IntVec3.Invalid;
            if (!fire.IsValid) return;
            var young = here.FirstOrDefault(a => a.RaceProps.Animal && a.ageTracker != null && !a.ageTracker.Adult && !a.Downed);
            try
            {
                if (young != null && Rand.Chance(PeopleEvents3Math.YoungBeastChance))
                {
                    young.GetLord()?.RemovePawn(young);
                    v.group.Remove(young);
                    young.SetFaction(Faction.OfPlayer);
                    if (v.a != null && !v.a.Dead && young.playerSettings != null) young.playerSettings.Master = v.a;
                    Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv3_DroveYoung".Translate((f?.Name ?? "?").Named("FACTION"), young.Named("YOUNG")).Resolve(), new LookTargets(young),
                        MessageTypeDefOf.PositiveEvent);
                    return;
                }
                var kind = here.FirstOrDefault(a => a.RaceProps.Animal)?.def;
                var leather = kind?.race?.leatherDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain");
                var milk = kind?.GetCompProperties<CompProperties_Milkable>()?.milkDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("Milk");
                int hides = Rand.RangeInclusive(PeopleEvents3Math.HidesMin, PeopleEvents3Math.HidesMax), pails = Rand.RangeInclusive(PeopleEvents3Math.MilkMin, PeopleEvents3Math.MilkMax);
                foreach (var t in Couriers.Make(leather, hides).Concat(Couriers.Make(milk, pails)))
                    if (GenPlace.TryPlaceThing(t, fire, map, ThingPlaceMode.Near, out var placed)) placed?.SetForbidden(false, false);
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv3_DroveHides".Translate((f?.Name ?? "?").Named("FACTION"), hides.Named("HIDES"), (leather?.label ?? "").Named("LEATHER"),
                    pails.Named("MILK")).Resolve(), new LookTargets(new TargetInfo(fire, map)), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the drove's thanks", e);
            }
        }

        /// <summary>All gone off the map: the fire raked out, and their people's word on the crossing.</summary>
        private static void Gone(Visit v)
        {
            var map = v.map;
            if (v.place != null && v.place.Spawned) v.place.Destroy();
            var f = PeoplesF.PeopleOf(v);
            bool free = Parts(v).Length > 4 && Parts(v)[4] == "free";
            if (free && f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.CrossedGoodwill, false);
            if (map != null) News.Post("CC_PEv3_DroveGone".Translate((f?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid, NewsKind.Land);
        }

        /// <summary>Dev mode: the drove's days are over now.</summary>
        public static bool OnNow()
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.stake == 0);
            if (v == null) return false;
            v.due = Now;
            return true;
        }
    }

    /// <summary>A drove crossing the colony's land: its herd and drovers about a ground that moves a little each hour, by their fire at night, then off at the far edge.</summary>
    public class LordJob_Drove : LordJob
    {
        public const string MemoOn = "DroveOn";

        public IntVec3 point, fire;
        public bool night;

        public LordJob_Drove()
        {
        }

        public LordJob_Drove(IntVec3 point, IntVec3 fire)
        {
            this.point = point;
            this.fire = fire;
        }

        public override bool AddFleeToil => true;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var graze = new LordToil_Drove();
            graph.StartingToil = graze;
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var on = new Transition(graze, leave);
            on.AddTrigger(new Trigger_Memo(MemoOn));
            on.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay * 5));
            graph.AddTransition(on);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref point, "point");
            Scribe_Values.Look(ref fire, "fire");
            Scribe_Values.Look(ref night, "night");
        }
    }

    /// <summary>The herd grazing about the drove's ground; the drovers with it by day and at their fire by night.</summary>
    public class LordToil_Drove : LordToil
    {
        private LordJob_Drove Job => lord?.LordJob as LordJob_Drove;

        public override IntVec3 FlagLoc => Job?.point ?? IntVec3.Invalid;

        public override bool AllowSatisfyLongNeeds => true;

        public override bool AllowRestingInBed => false;

        public override void UpdateAllDuties()
        {
            var j = Job;
            if (j == null) return;
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = p.RaceProps.Humanlike
                    ? new PawnDuty(DutyDefOf.WanderClose, j.night && j.fire.IsValid ? j.fire : j.point, j.night ? 5f : 10f)
                    : new PawnDuty(CampDefOf.Council_CampBeasts, j.point, 9f);
        }
    }

    /// <summary>
    /// A drover of {FACTION} at the gate: a drove of {COUNT} head of {TALE} would cross the colony's land and graze there two
    /// or three days, on to their fire at the far edge. {A} is the colony's best hand at beasts; the toll is {BEAST} silver
    /// (the event has no beast of its own to name).
    /// </summary>
    public class HappeningWorker_DroveCrossing : HappeningWorker_Visitors
    {
        private static bool Running(Map map) => CouncilGameComponent.Instance?.happenings?.visits?.Any(v => v != null && v.kind == Drove.Kind && !v.done && v.map == map) ?? false;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Herders) != null && Drove.KindFor(map) != null && !Running(map)
            && GenLocalDate.Season(map) != Season.Winter && GenLocalDate.Season(map) != Season.PermanentWinter && Drove.Edges(map, out _, out _) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Herders);
            var kind = Drove.KindFor(ctx.map);
            if (ctx.faction == null || kind == null || !Drove.Edges(ctx.map, out var from, out var to)) return false;
            ctx.count = Rand.RangeInclusive(PeopleEvents3Math.DroveMin, PeopleEvents3Math.DroveMax);
            ctx.tale = kind.GetLabelPlural();
            // The kind kept for the answer (never shown: the event has no dead of its own), the toll in variant and named by {BEAST}.
            ctx.dead = kind.defName;
            ctx.variant = PeopleEvents3Math.DroveToll(ctx.count, LivingEvents.WealthFactor(ctx.map));
            ctx.beast = ctx.variant.ToString(CultureInfo.InvariantCulture);
            ctx.note = Drove.CropsAtRisk(ctx.map, from, to).ToString(CultureInfo.InvariantCulture);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Drover"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Animals) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (GiddyUpMounts.On) tags.Add("ride");
            if (int.TryParse(ctx.note ?? "0", out int crops) && crops > 0) tags.Add("fields");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !Drove.Edges(ctx.map, out _, out _) ? "CC_PEv3_NoWayAcross".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "cross" : "round");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The drove crosses (Drove.Begin): as neighbours (their thanks and the ride), or for the toll (paid at the gate; they think less of it).</summary>
    public class DilemmaEffect_Drove : DilemmaEffect
    {
        public bool toll;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv3_Impact_Drove".Translate(hc.count, hc.tale ?? "?").Resolve(), "CC_PEv3_Impact_DroveBrief".Translate().Resolve());
            int crops = int.TryParse(hc.note ?? "0", out int n) ? n : 0;
            sheet.Add(ImpactKind.Goods, crops > 0 ? -1 : 0, crops > 0 ? "CC_PEv3_Impact_Crops".Translate(PeopleEvents3Math.Grazed(crops), crops).Resolve() : "CC_PEv3_Impact_NoCrops".Translate().Resolve(),
                "CC_PEv3_Impact_CropsBrief".Translate().Resolve(), "pev3crops");
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_Kick".Translate().Resolve(), "CC_PEv3_Impact_KickBrief".Translate().Resolve(), "pev3kick");
            if (toll)
            {
                sheet.Silver(hc.variant);
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.DroveTollGoodwill);
                return;
            }
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv3_Impact_DroveThanks".Translate().Resolve(), "CC_PEv3_Impact_DroveThanksBrief".Translate().Resolve(), "pev3drovethanks");
            if (hc.a != null)
            {
                sheet.Play("CC_PEv3_Impact_Ride".Translate(hc.a.LabelShort).Resolve(), "CC_PEv3_Impact_RideBrief".Translate().Resolve());
                DilemmaImpact.Xp(sheet, hc.a.LabelShort, hc.a.ThingID, SkillDefOf.Animals, PeopleEvents3Math.RideXp, false);
            }
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.CrossedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (toll)
            {
                var x = hc.stranger;
                Couriers.PaySilver(hc.map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, hc.variant, hc.faction, PeoplesG.Up(x, hc.map) ? x : null);
                if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents3Math.DroveTollGoodwill, false);
            }
            Drove.Begin(hc, !toll);
        }
    }

    /// <summary>Turned away: they take the drove round the colony's land, and think less of it.</summary>
    public class DilemmaEffect_DroveTurned : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents3Math.DroveTurnedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.faction != null && ctx.faction.HasGoodwill && !ctx.faction.IsPlayer) WorldSituationWorker.Goodwill(ctx.faction, PeopleEvents3Math.DroveTurnedGoodwill, false);
        }
    }
}
