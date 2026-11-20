using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class RaidersCampSignsDefOf
    {
        public static DutyDef Rimfolk_CampScoutWatch;

        static RaidersCampSignsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RaidersCampSignsDefOf));
    }

    /// <summary>
    /// A raiders' camp's signs on the map (docs/content-update-proposals.md, B13; the rules are RaidersCampSignsMath's).
    /// <para>
    /// Its scouts: before each raid two of its people come to the map's edge on the side the raid will come from, walk a few
    /// steps in, stand and watch the colony for an hour or so, then go; the raid follows an hour or two after they were
    /// first seen, in from the edge they watched from, and its letter says so. They never start a fight: hurt, they make for
    /// the edge and defend themselves on the way (the game's own leaving duty).
    /// </para>
    /// <para>
    /// The runaway: once in a camp's life, a captive who ran from it (one of a people at peace with the colony, bruised and
    /// cut from before) reaches the gate from the camp's side (the event Rimfolk_Evt_CampRunaway). Taken in, they lie in the
    /// colony's beds as its guest until they can walk home (World/Region/TakenIn.cs), their people remember it (the deed
    /// "rescued") and the camp's next raid comes sooner, its raiders out looking for them.
    /// </para>
    /// <para>The smoke: at dusk, every few days, someone on the walls says the camp's smoke is up again in its direction.</para>
    /// </summary>
    public static partial class RaidersCamps
    {
        // ================= The scouts =================

        /// <summary>
        /// The camp's raid is coming: its scouts are seen first, at the edge on its side, and the raid is due in an hour or two
        /// from that edge. False when no scout can be made or no edge found (the raid then comes as it always did).
        /// </summary>
        private static bool Scout(Map home, RaidersCamp c, int now)
        {
            if (MapFacts.ThreatOn(home) || now - home.storyState.LastThreatBigTick < Day) return false;
            var from = EdgeToward(home, TileOf(c), out int side, out _);
            if (!from.HasValue || side < 0) return false;
            var watch = WatchSpot(home, from.Value);
            if (!watch.IsValid) return false;
            var scouts = MakeScouts(c, home);
            if (scouts.Count == 0) return false;
            try
            {
                foreach (var p in scouts) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(from.Value, home, 3), home);
                LordMaker.MakeNewLord(c.people, new LordJob_CampScouts(watch, PlaySpots.GateCell(home, null), RaidersCampSignsMath.WatchTicks(Rand.Value)), home, scouts);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its scouts", e);
                foreach (var p in scouts.Where(p => p != null && !p.Destroyed)) p.Destroy();
                return false;
            }
            c.scoutAt = now;
            c.raidDue = now + RaidersCampSignsMath.LeadTicks(Rand.Value);
            c.raidSide = side;
            c.raidCell = from.Value;
            Tell.Message(NewsKind.World, Importance.Important, home,
                TextVariants.Vary("CC_RaidCamp_Scouts").Translate(c.people.Name.Named("PEOPLE"), c.kind.Named("KIND"), SideWord(side).Named("SIDE")).Resolve(),
                new LookTargets(scouts), MessageTypeDefOf.ThreatSmall);
            return true;
        }

        /// <summary>A cell some way in from the edge cell, toward the colony, that they can stand on and walk to.</summary>
        private static IntVec3 WatchSpot(Map home, IntVec3 edge)
        {
            var toward = (home.Center - edge).ToVector3();
            var target = edge + (toward.normalized * RaidersCampSignsMath.WatchCells).ToIntVec3();
            if (!target.InBounds(home)) target = edge;
            return CellFinder.TryFindRandomCellNear(target, home, 5, c => c.Standable(home) && !c.Fogged(home) && !c.Roofed(home)
                                                                      && home.reachability.CanReach(edge, c, PathEndMode.OnCell, TraverseMode.PassDoors), out var cell)
                ? cell
                : IntVec3.Invalid;
        }

        /// <summary>Two of the camp's people, of the plainest fighters their people send (or their ordinary member).</summary>
        private static List<Pawn> MakeScouts(RaidersCamp c, Map home)
        {
            var list = new List<Pawn>();
            var kinds = c.people?.def?.pawnGroupMakers?.Where(m => m != null && m.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true).Distinct().OrderBy(k => k.combatPower).Take(3).ToList() ?? new List<PawnKindDef>();
            if (kinds.Count == 0 && Arrivals.PersonKind(c.people) is PawnKindDef basic) kinds.Add(basic);
            if (kinds.Count == 0) return list;
            for (int i = 0; i < RaidersCampSignsMath.Scouts; i++)
            {
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kinds.RandomElement(), c.people, PawnGenerationContext.NonPlayer, home.Tile,
                        mustBeCapableOfViolence: true));
                    if (p != null) list.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("raiders' camp: a scout made", e);
                }
            }
            return list;
        }

        /// <summary>Its scouts on the map, still up and about.</summary>
        private static List<Pawn> ScoutsOn(Map home, RaidersCamp c) =>
            c?.people == null || home?.mapPawns == null
                ? new List<Pawn>()
                : home.mapPawns.SpawnedPawnsInFaction(c.people).Where(p => p != null && !p.Dead && !p.Downed && !p.IsPrisoner && p.GetLord()?.LordJob is LordJob_CampScouts).ToList();

        /// <summary>
        /// The hour while its raid is due after its scouts: any scout still about is sent off first (the raid waits for them to
        /// be gone), then it comes from their edge; held back by another fight, it waits, and is given up half a day late.
        /// </summary>
        private static void DueRaid(Map home, RaidersCamp c, int now)
        {
            if (c.stage != CampStage.Found || !On || !ThreatsAllowed)
            {
                ClearDue(c);
                return;
            }
            if (!RaidersCampSignsMath.Due(c.raidDue, now)) return;
            var still = ScoutsOn(home, c);
            if (still.Count > 0)
            {
                foreach (var lord in still.Select(p => p.GetLord()).Where(l => l != null).Distinct().ToList()) lord.ReceiveMemo(LordJob_CampScouts.GoMemo);
                if (RaidersCampSignsMath.TooLate(c.raidDue, now)) ClearDue(c);
                return;
            }
            if (TryRaid(home, c, now, c.raidCell, c.raidSide) || RaidersCampSignsMath.TooLate(c.raidDue, now)) ClearDue(c);
        }

        private static void ClearDue(RaidersCamp c)
        {
            c.raidDue = -1;
            c.raidSide = -1;
            c.raidCell = IntVec3.Invalid;
        }

        // ================= The runaway =================

        public const string RunawayEvent = "Rimfolk_Evt_CampRunaway";

        /// <summary>The camp the runaway event is fired for, while it is.</summary>
        private static RaidersCamp firing;

        /// <summary>The camp near this home a runaway may come from now (the one the event is fired for).</summary>
        public static RaidersCamp RunawayFrom(Map home)
        {
            var c = firing ?? CampOf(home);
            return c != null && RaidersCampSignsMath.RunawayMay(c.runaway, c.stage, c.foundAt, c.raidOpen, Now) && c.people != null ? c : null;
        }

        /// <summary>The camp's day: once in its life, a captive who ran from it reaches the gate.</summary>
        private static void Runaway(Map home, RaidersCamp c, int now)
        {
            if (!RaidersCampSignsMath.RunawayMay(c.runaway, c.stage, c.foundAt, c.raidOpen, now) || !Tell.CanAsk || MapFacts.ThreatOn(home)) return;
            if (!Rand.Chance(RaidersCampSignsMath.RunawayDaily)) return;
            FireRunaway(home, c);
        }

        private static bool FireRunaway(Map home, RaidersCamp c)
        {
            firing = c;
            try
            {
                return PeopleEvents2.Fire(RunawayEvent, home, null);
            }
            finally
            {
                firing = null;
            }
        }

        /// <summary>A cell at the map's edge on the camp's side (where the runaway comes in), or null.</summary>
        internal static IntVec3? EdgeOnItsSide(Map home, RaidersCamp c) => EdgeToward(home, TileOf(c), out _, out _);

        /// <summary>The runaway was taken in: the camp's raiders go out looking, and its next raid comes sooner.</summary>
        public static void RunawayTaken(Map home)
        {
            var c = CampOf(home);
            if (c != null) c.sooner = true;
        }

        // ================= The smoke at dusk =================

        /// <summary>At dusk, every few days, half the time: someone on the walls says the camp's smoke is up again, and which way.</summary>
        private static void Smoke(Map home, RaidersCamp c, int now)
        {
            if (c.stage != CampStage.Found || !RaidersCampSignsMath.SmokeDue(GenLocalDate.HourOfDay(home), c.smokeAt, now)) return;
            c.smokeAt = now;
            if (!Rand.Chance(RaidersCampSignsMath.SmokeChance) || MapFacts.ThreatOn(home)) return;
            var who = OnTheWalls(home);
            string line = SmokeLine(home, c);
            if (who == null || line == null) return;
            Speech.Say(who, line);
        }

        /// <summary>The line on the camp's smoke, in its direction from the colony ("There's smoke to the northeast again ..."), or null.</summary>
        private static string SmokeLine(Map home, RaidersCamp c)
        {
            string dir = Direction(home, c);
            return dir.NullOrEmpty() ? null : TextVariants.Vary("CC_RaidCamp_Smoke").Translate(dir.Named("DIR"), c.kind.Named("KIND")).Resolve();
        }

        /// <summary>
        /// Someone of the colony out on its walls at dusk: awake and outdoors, beside a wall, a fence or a gate (the marshal first,
        /// then whoever is drafted, then anyone); else anyone awake out of doors; null for none.
        /// </summary>
        private static Pawn OnTheWalls(Map home)
        {
            var outdoors = home.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Downed && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Baby()
                                                                          && !p.Position.Roofed(home) && !p.InBed()).ToList();
            if (outdoors.Count == 0) return null;
            var marshal = OfficeTracker.For(home)?.HolderOf("Council_Office_Marshal");
            bool ByWall(Pawn p) => GenRadial.RadialCellsAround(p.Position, 2f, true).Any(cell => cell.InBounds(home) && cell.GetEdifice(home) is Building b
                && b.Faction == Faction.OfPlayer && (b.def.passability == Traversability.Impassable || b.def.IsDoor || b.def.IsFence));
            var walls = outdoors.Where(ByWall).ToList();
            return walls.FirstOrDefault(p => p == marshal) ?? walls.FirstOrDefault(p => p.Drafted) ?? walls.RandomElementWithFallback() ?? outdoors.RandomElementWithFallback();
        }

        // ================= Dev mode =================

        [DebugAction("Rimfolk", "Raiders' camp: its scouts now (the raid follows them)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugScouts()
        {
            var home = DebugHome;
            var c = CampOf(home);
            if (c == null || c.stage != CampStage.Found)
            {
                Say("no camp found near this home.");
                return;
            }
            c.raidOpen = false;
            ClearDue(c);
            Say(Scout(home, c, Now)
                ? "its scouts are at the " + SideWord(c.raidSide) + " edge; the raid is due in " + ((c.raidDue - Now) / (float)RaidersCampMath.HourTicks).ToString("0.0") + " hours."
                : "no scouts: a threat is on the map, a big one came within a day, or no edge or scout could be found.");
        }

        [DebugAction("Rimfolk", "Raiders' camp: a runaway at the gate now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRunaway()
        {
            var home = DebugHome;
            var c = CampOf(home);
            if (c == null || c.stage != CampStage.Found)
            {
                Say("no camp found near this home.");
                return;
            }
            c.runaway = false;
            if (c.foundAt > Now - RaidersCampSignsMath.RunawayAfterDays * Day) c.foundAt = Now - RaidersCampSignsMath.RunawayAfterDays * Day;
            if (!FireRunaway(home, c)) Say("no runaway: no people at peace with the colony, a raid is on, or the event couldn't fire.");
        }

        [DebugAction("Rimfolk", "Raiders' camp: the smoke at dusk, said now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSmoke()
        {
            var home = DebugHome;
            var c = CampOf(home);
            if (c == null || c.stage != CampStage.Found)
            {
                Say("no camp found near this home.");
                return;
            }
            var who = OnTheWalls(home);
            string line = SmokeLine(home, c);
            if (who == null || line == null)
            {
                Say(who == null ? "nobody of the colony is awake out of doors." : "the camp has no direction from this home.");
                return;
            }
            Speech.Say(who, line);
        }
    }

    /// <summary>
    /// A raiders' camp's scouts at the edge of the colony's map (RaidersCamps.Scout): they walk to their spot, stand and watch
    /// the colony for a while, then leave by the nearest edge; sent off early by a memo; hurt, they leave at once and defend
    /// themselves as they go. They never start a fight.
    /// </summary>
    public class LordJob_CampScouts : LordJob
    {
        public IntVec3 spot = IntVec3.Invalid, look = IntVec3.Invalid;
        public int watchTicks = 2500;

        public const string GoMemo = "RimfolkScoutsGo";

        public LordJob_CampScouts()
        {
        }

        public LordJob_CampScouts(IntVec3 spot, IntVec3 look, int watchTicks)
        {
            this.spot = spot;
            this.look = look;
            this.watchTicks = Math.Max(1, watchTicks);
        }

        public override bool AddFleeToil => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var watch = new LordToil_CampScoutsWatch(spot, look);
            graph.StartingToil = watch;
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var hurt = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(hurt);
            var done = new Transition(watch, leave);
            done.AddTrigger(new Trigger_TicksPassed(watchTicks));
            done.AddTrigger(new Trigger_Memo(GoMemo));
            graph.AddTransition(done);
            var harmed = new Transition(watch, hurt);
            harmed.AddSource(leave);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            graph.AddTransition(harmed);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref look, "look", IntVec3.Invalid);
            Scribe_Values.Look(ref watchTicks, "watchTicks", 2500);
        }
    }

    /// <summary>Standing near their spot and looking toward the colony (Rimfolk_CampScoutWatch).</summary>
    public class LordToil_CampScoutsWatch : LordToil
    {
        private readonly IntVec3 spot, look;

        public LordToil_CampScoutsWatch(IntVec3 spot, IntVec3 look)
        {
            this.spot = spot;
            this.look = look;
        }

        public override IntVec3 FlagLoc => spot;

        public override bool ShouldFail => base.ShouldFail || lord.ownedPawns.Count == 0;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
            {
                p.mindState.duty = new PawnDuty(RaidersCampSignsDefOf.Rimfolk_CampScoutWatch, spot, look.IsValid ? look : spot, 3f);
            }
        }
    }

    /// <summary>A scout's watch: to the spot if not there, else a while standing still facing the colony (never the waiting job that strikes out at a passer-by).</summary>
    public class JobGiver_CampScoutWatch : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var duty = pawn?.mindState?.duty;
            if (duty == null || !duty.focus.IsValid) return null;
            var spot = duty.focus.Cell;
            if (!pawn.Position.InHorDistOf(spot, 3f))
            {
                if (!pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly)) return null;
                var go = JobMaker.MakeJob(JobDefOf.Goto, spot);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                return go;
            }
            var stand = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            stand.expiryInterval = 600;
            var toward = duty.focusSecond.IsValid ? duty.focusSecond.Cell : spot;
            if (toward != pawn.Position) stand.overrideFacing = Rot4.FromAngleFlat((toward - pawn.Position).AngleFlat);
            return stand;
        }
    }

    /// <summary>
    /// A captive who ran from the raiders' camp near the colony reaches the gate from the camp's side, bruised and cut from
    /// before (RaidersCamps.Runaway, B13). {X} the runaway, {FACTION} their people, {ENEMY} the camp's people, {TALE} the camp's
    /// kind ("brigands' hold"), {PLACE} where it lies ("two days' walk to the north"), {A} who sees them come.
    /// </summary>
    public class HappeningWorker_CampRunaway : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => RaidersCamps.RunawayFrom(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var c = RaidersCamps.RunawayFrom(ctx.map);
            if (c == null) return false;
            ctx.enemy = c.people;
            // Most likely one of a people the camp's own are at war with (whose people it would take), else any at peace with the colony.
            ctx.faction = Arrivals.Friendly(f => f != c.people, f => f.HostileTo(c.people) ? 3f : 1f);
            if (ctx.faction == null) return false;
            var kind = Arrivals.PersonKind(ctx.faction);
            ctx.stranger = kind == null ? null : Arrivals.Make(kind, ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.tale = c.kind;
            ctx.placeName = RaidersCamps.Where(ctx.map, c);
            // Who sees them come: a grown colonist out of doors and awake if there is one.
            var grown = Colonists(ctx.map).Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile()).ToList();
            ctx.a = grown.Where(p => p.Spawned && p.Awake() && !p.Position.Roofed(p.Map)).RandomElementWithFallback() ?? grown.RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var c = RaidersCamps.RunawayFrom(ctx.map);
            var x = ctx.stranger;
            if (c == null || x == null) return false;
            var edge = RaidersCamps.EdgeOnItsSide(ctx.map, c);
            if (!Arrivals.Spawn(x, ctx.map, edge)) return false;
            // Hurt from before: beaten and cut by the camp's people, scratched on the way through the brush.
            TakenIn.Hurt(x, Rand.RangeInclusive(TakenInMath.RunawayInjuriesMin, TakenInMath.RunawayInjuriesMax), false);
            c.runaway = true;
            // To the gate, where they sit and wait for the colony's answer.
            var spot = PeoplesD.GateSpot(ctx.map) ?? IntVec3.Invalid;
            if (!spot.IsValid && !RCellFinder.TryFindRandomSpotJustOutsideColony(x, out spot)) spot = x.Position;
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, spot, Mathf.RoundToInt(ext.stayHours.RandomInRange * GenDate.TicksPerHour)),
                ctx.map, new List<Pawn> { x });
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.faction != null && ctx.enemy != null && ctx.faction.HostileTo(ctx.enemy)) tags.Add("atwar");
        }
    }

    /// <summary>The runaway taken in (B13): put to bed as the colony's guest until they can walk home, and the camp's next raid comes sooner.</summary>
    public class DilemmaEffect_CampRunaway : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (x == null) return;
            sheet.Play("CC_TakenIn_Impact".Translate(x.LabelShort).Resolve(), "CC_TakenIn_ImpactBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, -1, "CC_RaidCamp_Impact_Sooner".Translate(((ctx as HappeningContext)?.tale ?? "").Named("KIND"), x.LabelShort.Named("NAME")).Resolve(),
                "CC_RaidCamp_Impact_SoonerBrief".Translate().Resolve(), "campsooner");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || ctx.map == null) return;
            TakenIn.Add(x, ctx.faction ?? x.Faction, ctx.map, TakenInMath.Runaway, (ctx as HappeningContext)?.tale);
            RaidersCamps.RunawayTaken(ctx.map);
        }
    }
}
