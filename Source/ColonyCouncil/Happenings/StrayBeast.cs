using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // A neighbors' stray beast (Council_Evt_StrayBeast, a HappeningSetting with stray kinds): the letter says a branded
    // draft animal of theirs is at the colony, so one is. It comes with the letter, theirs, standing in the colony's barn
    // or pen (or about the colony near A when there's neither), and stays close to that spot while the colony decides.
    // Given back, their drover comes for it on the map (Couriers.Fetch) and leads it off with whatever the answer sends
    // with them; kept, it becomes the colony's. Once nothing waits on it any more (the letter answered and nobody coming,
    // or their people turned on the colony), it walks home by itself.

    [DefOf]
    public static class StrayDefOf
    {
        public static DutyDef Rimfolk_StrayHold;

        static StrayDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(StrayDefOf));
    }

    public static class StrayBeast
    {
        /// <summary>Where it stands: in a barn, in a pen, or about the colony (the letter's tags [barn], [pen], [yard]).</summary>
        public const string Barn = "barn", Pen = "pen", Yard = "yard";

        /// <summary>One of these kinds (PawnKindDef names) that exists and suits the weather: the land's own pack animals first.</summary>
        public static PawnKindDef Kind(Map map, List<string> kinds)
        {
            if (map == null || kinds.NullOrEmpty()) return null;
            var ok = kinds.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)).ToList();
            if (ok.Count == 0) return null;
            var biome = map.Biome;
            var local = biome == null ? ok : ok.Where(k => biome.IsPackAnimalAllowed(k.race)).ToList();
            return (local.Count > 0 ? local : ok).RandomElement();
        }

        /// <summary>
        /// The beast, theirs, at the colony: in a barn first, then a pen, then near A, wherever it could still walk off the
        /// map from; held there (<see cref="LordJob_StrayBeast"/>). Null when it can't be.
        /// </summary>
        public static Pawn Spawn(Map map, Faction faction, Pawn a, List<string> kinds, out string where)
        {
            where = null;
            var kind = Kind(map, kinds);
            if (kind == null || faction == null) return null;
            var x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, fixedBiologicalAge: kind.RaceProps.lifeExpectancy * Rand.Range(0.25f, 0.45f)));
            if (x == null) return null;
            foreach (var (cell, place) in Spots(map, a))
            {
                GenSpawn.Spawn(x, cell, map, Rot4.Random);
                if (ReachabilityUtility.CanReachMapEdge(x))
                {
                    LordMaker.MakeNewLord(faction, new LordJob_StrayBeast(cell), map, new List<Pawn> { x });
                    where = place;
                    return x;
                }
                x.DeSpawn();
            }
            Arrivals.Discard(x);
            return null;
        }

        /// <summary>A free cell in the barn nearest A, in the pen nearest A, about the colony near A, by the gate.</summary>
        private static IEnumerable<(IntVec3, string)> Spots(Map map, Pawn a)
        {
            var near = a != null && a.Spawned && a.Map == map ? a.Position : TemplatePlanner.ColonyCenter(map);
            foreach (var room in HerdHome.Barns(map).Where(r => !r.Fogged).OrderBy(r => r.Cells.FirstOrDefault().DistanceToSquared(near)).Take(2).ToList())
            {
                var cells = room.Cells.Where(c => Free(c, map)).ToList();
                if (cells.Count > 0) yield return (cells.RandomElement(), Barn);
            }
            foreach (var pen in HerdHome.Pens(map).Where(p => p.PenState != null && p.PenState.Enclosed)
                         .OrderBy(p => p.parent.Position.DistanceToSquared(near)).Take(2).ToList())
            {
                var cells = pen.PenState.ConnectedRegions.SelectMany(r => r.Cells).Where(c => Free(c, map) && !Indoors(c, map)).ToList();
                if (cells.Count > 0) yield return (cells.RandomElement(), Pen);
            }
            if (CellFinder.TryFindRandomCellNear(near, map, 8, c => Free(c, map) && !Indoors(c, map), out var yard)) yield return (yard, Yard);
            var gate = PlaySpots.GateCell(map, null);
            if (gate.IsValid && Free(gate, map)) yield return (gate, Yard);
        }

        private static bool Free(IntVec3 c, Map map) =>
            c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null && c.GetDoor(map) == null && !(c.GetTerrain(map)?.IsWater ?? false);

        private static bool Indoors(IntVec3 c, Map map)
        {
            var room = c.GetRoom(map);
            return room != null && !room.PsychologicallyOutdoors;
        }

        /// <summary>The event's stray (its X), still held where it was found: alive, here, theirs.</summary>
        public static Pawn Here(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Map != ctx.map || x.RaceProps == null || !x.RaceProps.Animal) return null;
            if (x.Faction == null || x.Faction.IsPlayer) return null;
            return x.GetLord()?.CurLordToil is LordToil_StrayHold ? x : null;
        }

        /// <summary>Someone still waits on it: the letter about it, unanswered, or their drover on the way for it.</summary>
        public static bool Waited(Pawn x)
        {
            if (x?.Faction == null || x.Faction.IsPlayer || x.Faction.HostileTo(Faction.OfPlayer)) return false;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.stranger == x)
                    return true;
            return Coming(x) != null;
        }

        /// <summary>Their drover's trip for it, while they haven't got it yet.</summary>
        public static CourierTrip Coming(Pawn x)
        {
            var trips = CourierTracker.Instance?.trips;
            if (x == null || trips == null) return null;
            foreach (var t in trips)
                if (t.fetch == x && t.FetchPending && !t.settled)
                    return t;
            return null;
        }

        /// <summary>It goes home by itself: off the map at a walk, or, shut in where it can't get out, gone without a trace.</summary>
        public static void GoHome(Pawn x)
        {
            if (x == null || x.Dead || !x.Spawned) return;
            var lord = x.GetLord();
            if (lord?.LordJob is LordJob_StrayBeast && ReachabilityUtility.CanReachMapEdge(x))
            {
                lord.ReceiveMemo(LordJob_StrayBeast.MemoHome);
                return;
            }
            lord?.RemovePawn(x);
            x.DeSpawn();
            if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
        }

        /// <summary>
        /// What the answer tells. Given back with their drover coming: told when they've led it off (the letter waits on
        /// their trip), so nothing is said now. Gone before anyone came for it, or nobody could come and it walks home on
        /// its own: that, instead of the answer's words.
        /// </summary>
        public static string Outcome(HappeningContext ctx, bool byDefault, string outcome)
        {
            var x = ctx?.stranger;
            if (x == null || x.Faction == Faction.OfPlayer) return outcome;
            var trip = Coming(x);
            if (trip != null)
            {
                string told = ctx.Format(outcome);
                if (!told.NullOrEmpty())
                {
                    trip.letterLabel = ctx.incident?.LabelCap.ToString() ?? "";
                    trip.letterText = byDefault ? "CC_Evt_NoAnswer".Translate() + " " + told : told;
                    trip.letterDef = LetterDefOf.NeutralEvent;
                }
                return null;
            }
            string people = ctx.faction?.Name ?? x.Faction?.Name ?? "";
            if (x.Dead || !x.Spawned || x.Map != ctx.map) return "CC_Stray_GoneOutcome".Translate(people).Resolve();
            if (x.GetLord()?.CurLordToil is LordToil_StrayHome) return "CC_Stray_WalkedOutcome".Translate(people).Resolve();
            return outcome;
        }
    }

    /// <summary>
    /// A stray held where it was found while the colony decides (<see cref="StrayBeast"/>): it eats, rests and wanders a
    /// few steps about its spot, keeping to the barn it stands in. Once nothing waits on it any more, it walks home.
    /// </summary>
    public class LordJob_StrayBeast : LordJob
    {
        public IntVec3 spot;

        public const string MemoHome = "StrayHome";

        public LordJob_StrayBeast()
        {
        }

        public LordJob_StrayBeast(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var hold = new LordToil_StrayHold(spot);
            graph.StartingToil = hold;
            var home = new LordToil_StrayHome();
            graph.AddToil(home);
            var goes = new Transition(hold, home);
            goes.AddTrigger(new Trigger_Memo(MemoHome));
            graph.AddTransition(goes);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spot, "spot");
        }
    }

    /// <summary>Kept close to its spot (Rimfolk_StrayHold); once an hour, asked whether anyone still waits on it.</summary>
    public class LordToil_StrayHold : LordToil
    {
        private readonly IntVec3 spot;

        public LordToil_StrayHold(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override IntVec3 FlagLoc => spot;

        /// <summary>Taken by their drover, or made the colony's: nothing left to hold, and the group ends.</summary>
        public override bool ShouldFail => base.ShouldFail || lord.ownedPawns.Count == 0;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns) p.mindState.duty = new PawnDuty(StrayDefOf.Rimfolk_StrayHold, spot, 3f);
        }

        public override void LordToilTick()
        {
            if (lord.ticksInToil % 2500 != 1300) return;
            try
            {
                foreach (var p in lord.ownedPawns.ToList())
                    if (!StrayBeast.Waited(p))
                        StrayBeast.GoHome(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("stray beast hold", e);
            }
        }
    }

    /// <summary>Off home at a walk, by the nearest edge.</summary>
    public class LordToil_StrayHome : LordToil_ExitMap
    {
        public LordToil_StrayHome() : base(LocomotionUrgency.Walk, false, true)
        {
        }

        public override bool ShouldFail => base.ShouldFail || lord.ownedPawns.Count == 0;
    }

    /// <summary>A held stray wanders only where it stands: in the barn, it keeps to the barn.</summary>
    public class JobGiver_StrayWander : JobGiver_WanderNearDutyLocation
    {
        public JobGiver_StrayWander()
        {
            wanderDestValidator = (pawn, loc, root) =>
            {
                var focus = pawn.mindState?.duty?.focus.Cell ?? IntVec3.Invalid;
                var map = pawn.Map;
                return map == null || !focus.IsValid || !focus.InBounds(map) || loc.GetRoom(map) == focus.GetRoom(map);
            };
        }
    }

    /// <summary>
    /// The stray given back (the event's X, <see cref="StrayBeast"/>): their drover comes for it, walks to it and leads it
    /// off with their party (Couriers.Fetch), bringing whatever else the same answer sends them with, a gift or a fee, in
    /// one visit (so this comes before those effects). Nobody can come: it walks home by itself. Gone: the answer can't be
    /// given (unless it's the default, which tells that it was gone).
    /// </summary>
    public class DilemmaEffect_StrayHome : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => StrayBeast.Here(ctx) == null ? "CC_Stray_NotHere".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = StrayBeast.Here(ctx);
            if (x == null) return;
            string kind = x.KindLabel, people = x.Faction?.Name ?? "";
            // As Couriers.Fetch decides it: their own drover, or nobody.
            if (Couriers.CanSend(ctx.map, x.Faction) && Couriers.Choose(x.Faction, ctx.map) == x.Faction)
                sheet.Play("CC_Impact_StrayFetched".Translate(people, kind).Resolve(), "CC_Impact_StrayFetchedBrief".Translate(kind).Resolve());
            else sheet.Play("CC_Impact_StrayWalks".Translate(people, kind).Resolve(), "CC_Impact_StrayWalksBrief".Translate(kind).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = StrayBeast.Here(ctx);
            if (x == null) return;
            string label = "CC_Courier_The".Translate(x.KindLabel).Resolve();
            if (Couriers.Fetch(ctx.map, x, x.Faction, null, x.Position, false, label)) return;
            StrayBeast.GoHome(x);
        }
    }

    /// <summary>The stray kept (the event's X, <see cref="StrayBeast"/>): it becomes the colony's where it stands. Gone: the answer can't be given.</summary>
    public class DilemmaEffect_KeepStray : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => StrayBeast.Here(ctx) == null ? "CC_Stray_NotHere".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = StrayBeast.Here(ctx);
            if (x == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_Impact_StrayKept".Translate(x.Faction?.Name ?? "", x.KindLabel).Resolve(),
                "CC_Impact_StrayKeptBrief".Translate(x.KindLabel).Resolve(), "animal");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = StrayBeast.Here(ctx);
            if (x == null) return;
            x.GetLord()?.RemovePawn(x);
            x.SetFaction(Faction.OfPlayer);
        }
    }
}
