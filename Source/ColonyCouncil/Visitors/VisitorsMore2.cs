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
    [DefOf]
    public static class VisitorDefOf2
    {
        public static JobDef Rimfolk_VisitRelic;
        public static JobDef Rimfolk_VisitPray;
        public static JobDef Rimfolk_VisitMeditate;
        public static JobDef Rimfolk_VisitGawk;
        public static JobDef Rimfolk_VisitRead;
        public static JobDef Rimfolk_VisitSwap;

        static VisitorDefOf2()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VisitorDefOf2));
        }
    }

    /// <summary>
    /// The second round of what visitors come to do (VisitorWork2Math has the kinds and their hours): pilgrims pray
    /// before the colony's relic in its reliquary, or at the gate facing it when they aren't let in; pilgrims at the
    /// anima tree meditate in a ring round it, and the tree's grass comes up as it does when a colonist meditates there;
    /// gawkers walk round the landed gravship's hull, looking it over; a reader copies a book at the colony's bookcase,
    /// or swaps (or buys) one there. Each is a stint of the visit work (JobDriver_VisitWork) at a real place.
    /// </summary>
    public static class VisitorWork2
    {
        // ---------------- The places ----------------

        /// <summary>The colony's reliquaries with a relic in them.</summary>
        public static List<Thing> Reliquaries(Map map)
        {
            var list = new List<Thing>();
            if (map == null || !ModsConfig.IdeologyActive) return list;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                var c = b.TryGetComp<CompRelicContainer>();
                if (c != null && !c.Empty && CompRelicContainer.IsRelic(c.ContainedThing) && IdeoOf(c.ContainedThing) != null) list.Add(b);
            }
            return list;
        }

        public static Thing RelicIn(Thing reliquary) => reliquary?.TryGetComp<CompRelicContainer>()?.ContainedThing;

        /// <summary>The ideoligion a relic belongs to.</summary>
        public static Ideo IdeoOf(Thing relic) => (relic?.StyleSourcePrecept as Precept_Relic)?.ideo;

        /// <summary>The reliquary they pray at: one holding a relic of their own faith first.</summary>
        public static Thing ReliquaryFor(Map map, Pawn p) =>
            Reliquaries(map).OrderByDescending(b => p?.Ideo != null && IdeoOf(RelicIn(b)) == p.Ideo).ThenBy(b => p == null ? 0 : b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        private static ThingDef animaTree;
        private static bool lookedAnima;

        public static ThingDef AnimaTreeDef
        {
            get
            {
                if (!lookedAnima)
                {
                    lookedAnima = true;
                    animaTree = ModsConfig.RoyaltyActive ? DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeAnima") : null;
                }
                return animaTree;
            }
        }

        /// <summary>The anima tree on the map, standing and not burning.</summary>
        public static Thing AnimaTree(Map map)
        {
            var def = AnimaTreeDef;
            if (map == null || def == null) return null;
            return map.listerThings.ThingsOfDef(def).FirstOrDefault(t => t.Spawned && !t.Destroyed && !t.IsBurning() && !t.Position.Fogged(map));
        }

        /// <summary>The colony's gravship engine on this map (Odyssey).</summary>
        public static Building_GravEngine Engine(Map map)
        {
            if (map == null || !ModsConfig.OdysseyActive) return null;
            try
            {
                return GravshipUtility.GetPlayerGravEngine_NewTemp(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the gravship's engine", e);
                return null;
            }
        }

        /// <summary>The colony's bookcases (with the books on them).</summary>
        public static IEnumerable<Building_Bookcase> Bookcases(Map map) =>
            map == null ? Enumerable.Empty<Building_Bookcase>() : map.listerBuildings.allBuildingsColonist.OfType<Building_Bookcase>().Where(b => b.Spawned);

        /// <summary>
        /// The colony's books that could change hands: on its bookcases, or lying in storage, not forbidden, not in
        /// anyone's hands. Vanilla Books Expanded's books are books of the game's own kind, so they count.
        /// </summary>
        public static List<Thing> Books(Map map)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            foreach (var b in Bookcases(map))
                foreach (var book in b.HeldBooks)
                    if (book != null && !book.Destroyed) list.Add(book);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (t is Book && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()) list.Add(t);
            return list;
        }

        /// <summary>Where a book is to be had: its bookcase, else the book where it lies; null when it's gone or in someone's hands.</summary>
        public static Thing PlaceOf(Thing book)
        {
            if (book == null || book.Destroyed) return null;
            if (book.Spawned) return book;
            return book.ParentHolder is Building_Bookcase b && b.Spawned ? b : null;
        }

        /// <summary>A book's title as the words name it.</summary>
        public static string Title(Thing book) => book is Book b && !b.Title.NullOrEmpty() ? b.Title : book?.LabelNoCount ?? "?";

        // ---------------- The work ----------------

        public static Job JobFor(Pawn p, string work)
        {
            if (p?.Map == null || !VisitorWork2Math.Known(work) || !VisitorWork2Math.InHours(work, GenLocalDate.HourOfDay(p.Map))) return null;
            var map = p.Map;
            var trip = VisitorTracker.Instance?.TripOf(p);
            switch (work)
            {
                case VisitorWork2Math.Relic:
                {
                    var r = ReliquaryFor(map, p);
                    return r != null && Beside(p, r, 2f, true, out var at) ? JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitRelic, at, r) : null;
                }
                case VisitorWork2Math.Pray:
                {
                    // At the gate, facing the relic far inside.
                    var r = ReliquaryFor(map, p);
                    return Ring(p, Focus(p), 3f, out var at) ? JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitPray, at, r != null ? new LocalTargetInfo(r) : new LocalTargetInfo(Focus(p))) : null;
                }
                case VisitorWork2Math.Meditate:
                {
                    var tree = AnimaTree(map);
                    return tree != null && RingAround(p, tree.Position, 2f, 4.5f, out var at) ? JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitMeditate, at, tree) : null;
                }
                case VisitorWork2Math.Gawk:
                {
                    var engine = Engine(map);
                    return engine != null && Hull(p, engine, out var at, out var face) ? JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitGawk, at, face) : null;
                }
                case VisitorWork2Math.Read:
                {
                    var deal = VisitorsMore2Tracker.Instance?.DealOf(trip);
                    var place = PlaceOf(deal?.want) ?? Bookcases(map).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                    return place != null && Beside(p, place, 1.5f, true, out var at) ? JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitRead, at, place) : null;
                }
                case VisitorWork2Math.Swap:
                {
                    var deal = VisitorsMore2Tracker.Instance?.DealOf(trip);
                    if (deal == null || deal.done || deal.by != p || deal.mode.NullOrEmpty()) return null;
                    var place = PlaceOf(deal.want);
                    if (place == null || !p.CanReach(place, PathEndMode.Touch, Danger.Some)) return null;
                    return JobMaker.MakeJob(VisitorDefOf2.Rimfolk_VisitSwap, place, deal.want);
                }
            }
            return null;
        }

        /// <summary>A stint done. True when it was one of these kinds (nothing more to do for the first round's).</summary>
        public static bool Done(Pawn p, string work, IntVec3 at)
        {
            if (!VisitorWork2Math.Known(work)) return false;
            if (p?.Map == null) return true;
            float say = work == VisitorWork2Math.Gawk ? 0.35f : work == VisitorWork2Math.Read ? 0.1f : 0.2f;
            if (Rand.Chance(say)) VisitorWork2.Say(p, work);
            return true;
        }

        /// <summary>Their lines (Rimfolk_VisitorLines2), each tagged with its moment: relic, pray, meditate, gawk, read, swap.</summary>
        public const string Pool = "Rimfolk_VisitorLines2";

        /// <summary>A line from one of them at a moment of their work.</summary>
        public static void Say(Pawn p, string moment)
        {
            var trip = VisitorTracker.Instance?.TripOf(p);
            if (trip != null) VisitorWords.Say(p, trip, moment, Pool);
        }

        /// <summary>Each tick of a stint at the anima tree: what a colonist's meditation there gives its grass (the game's own progress).</summary>
        public static void Meditated(Pawn p, Thing tree, int delta)
        {
            if (tree == null || !tree.Spawned || p == null || p.Position.DistanceTo(tree.Position) > 5f) return;
            var comp = tree.TryGetComp<CompSpawnSubplant>();
            if (comp == null) return;
            for (int i = 0; i < delta; i++) comp.AddProgress(JobDriver_Meditate.AnimaTreeSubplantProgressPerTick);
        }

        // ---------------- Where they stand ----------------

        private static IntVec3 Focus(Pawn p) => p.mindState?.duty != null && p.mindState.duty.focus.IsValid ? p.mindState.duty.focus.Cell : p.Position;

        private static bool Free(Pawn p, IntVec3 c, Map map) =>
            c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some);

        /// <summary>A cell beside a building or a thing (in its room, with <paramref name="sameRoom"/>), to stand at it facing it.</summary>
        private static bool Beside(Pawn p, Thing t, float radius, bool sameRoom, out IntVec3 at)
        {
            var map = p.Map;
            at = IntVec3.Invalid;
            var room = sameRoom ? t.GetRoom() : null;
            var rect = t.OccupiedRect();
            foreach (var c in GenRadial.RadialCellsAround(t.Position, radius + Mathf.Max(rect.Width, rect.Height) / 2f, false).InRandomOrder())
            {
                if (rect.Contains(c) || !Free(p, c, map)) continue;
                if (room != null && c.GetRoom(map) != room) continue;
                at = c;
                return true;
            }
            return false;
        }

        /// <summary>A place in a ring round a spot (the gate), out of doors.</summary>
        private static bool Ring(Pawn p, IntVec3 center, float radius, out IntVec3 at)
        {
            var map = p.Map;
            at = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(center, radius, true).InRandomOrder())
            {
                if (!VisitorWork.OutsideOk(c, map) || !Free(p, c, map)) continue;
                at = c;
                return true;
            }
            return false;
        }

        /// <summary>A place in the ring round the tree, between <paramref name="inner"/> and <paramref name="outer"/> cells out.</summary>
        private static bool RingAround(Pawn p, IntVec3 center, float inner, float outer, out IntVec3 at)
        {
            var map = p.Map;
            at = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(center, outer, false).InRandomOrder())
            {
                if (c.DistanceTo(center) < inner || !Free(p, c, map) || c.GetEdifice(map) != null) continue;
                at = c;
                return true;
            }
            return false;
        }

        /// <summary>A place just off the gravship's hull (beside it, never on it), and the hull cell to look at.</summary>
        private static bool Hull(Pawn p, Building_GravEngine engine, out IntVec3 at, out IntVec3 face)
        {
            var map = p.Map;
            at = face = IntVec3.Invalid;
            HashSet<IntVec3> hull = null;
            try
            {
                hull = engine.AllConnectedSubstructureNoRegen;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the gravship's hull", e);
            }
            if (hull == null || hull.Count == 0)
            {
                if (!Ring(p, engine.Position, 6f, out at)) return false;
                face = engine.Position;
                return true;
            }
            foreach (var h in hull.InRandomOrder().Take(120))
                foreach (var d in GenAdj.CardinalDirections)
                {
                    var c = h + d;
                    if (hull.Contains(c) || c.Roofed(map) || !Free(p, c, map)) continue;
                    at = c;
                    face = h;
                    return true;
                }
            return false;
        }
    }

    /// <summary>A stint of meditation at the anima tree: the visit work, and the tree's grass grows from it as it does from a colonist's.</summary>
    public class JobDriver_VisitMeditate : JobDriver_VisitWork
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var t in base.MakeNewToils())
            {
                if (t.debugName == "VisitWork") t.AddPreTickIntervalAction(delta => VisitorWork2.Meditated(pawn, job.targetB.Thing, delta));
                yield return t;
            }
        }
    }

    /// <summary>
    /// The book swapper at the colony's shelf (or the book where it lies in storage): they take the book the colony
    /// agreed to part with into their pack and leave theirs in its place on the shelf (beside it, if the shelf won't
    /// take it), or the silver for it. Theirs goes home with them.
    /// </summary>
    public class JobDriver_VisitSwap : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var look = ToilMaker.MakeToil("VisitSwapLook");
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = 400;
            look.handlingFacing = true;
            look.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
            yield return look;
            yield return Toils_General.Do(() => VisitorsMore2Tracker.Instance?.Exchange(pawn, job.targetA.Thing, job.targetB.Thing));
        }
    }

    /// <summary>A book swapper's bargain: the colony's book they want, theirs (in their pack), and what the colony said.</summary>
    public class BookDeal : IExposable
    {
        public int tripId = -1;
        public Pawn by;
        public Thing want;
        public Thing theirs;
        public int silver;
        /// <summary>"swap", "sell" or "copy"; empty until the colony answers.</summary>
        public string mode;
        public bool done;
        public int made;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_References.Look(ref by, "by");
            Scribe_References.Look(ref want, "want");
            Scribe_References.Look(ref theirs, "theirs");
            Scribe_Values.Look(ref silver, "silver");
            Scribe_Values.Look(ref mode, "mode");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref made, "made");
        }
    }

    /// <summary>
    /// What the second round of visitors keeps between hours: the book swappers' bargains (each only while their visit
    /// lasts) and the last landing of the colony's gravship near a friendly settlement (GravshipLanding).
    /// </summary>
    public class VisitorsMore2Tracker : GameComponent
    {
        public List<BookDeal> deals = new List<BookDeal>();
        public int landedTick = -1;
        public int landedMap = -1;

        private static VisitorsMore2Tracker cached;
        private static Game cachedGame;

        public static VisitorsMore2Tracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<VisitorsMore2Tracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public VisitorsMore2Tracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public BookDeal DealOf(VisitorTrip t)
        {
            if (t == null) return null;
            Tidy();
            return deals.FirstOrDefault(d => d.tripId == t.id);
        }

        public void Add(BookDeal d)
        {
            Tidy();
            d.made = Find.TickManager.TicksGame;
            deals.Add(d);
        }

        /// <summary>Bargains whose visit is over, or two days old, are let go.</summary>
        private void Tidy()
        {
            var tracker = VisitorTracker.Instance;
            int now = Find.TickManager.TicksGame;
            deals.RemoveAll(d => d == null || now - d.made > VisitorMath.TicksPerDay * 2 || tracker?.Get(d.tripId) is VisitorTrip t && t.state == VisitState.Done);
        }

        /// <summary>
        /// At the shelf: the colony's book into their pack, theirs onto the shelf in its place (or the silver set down
        /// there), and their visit soon over.
        /// </summary>
        public void Exchange(Pawn p, Thing place, Thing want)
        {
            var trip = VisitorTracker.Instance?.TripOf(p);
            var deal = DealOf(trip);
            if (deal == null || deal.done || deal.want != want || p?.Map == null || p.inventory?.innerContainer == null) return;
            var map = p.Map;
            try
            {
                var shelf = want.ParentHolder as Building_Bookcase;
                bool taken;
                if (shelf != null) taken = shelf.GetDirectlyHeldThings().TryTransferToContainer(want, p.inventory.innerContainer, 1) > 0;
                else if (want.Spawned)
                {
                    want.DeSpawn();
                    taken = p.inventory.innerContainer.TryAdd(want);
                    if (!taken) GenPlace.TryPlaceThing(want, p.Position, map, ThingPlaceMode.Near);
                }
                else taken = false;
                if (!taken) return;
                deal.done = true;
                string left;
                if (deal.mode == "sell")
                {
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = Math.Max(1, deal.silver);
                    GenPlace.TryPlaceThing(silver, p.Position, map, ThingPlaceMode.Near);
                    left = "CC_V2_Book_SoldLeft".Translate(deal.silver).Resolve();
                }
                else
                {
                    var theirs = deal.theirs != null && p.inventory.innerContainer.Contains(deal.theirs) ? deal.theirs : null;
                    left = theirs != null ? VisitorWork2.Title(theirs) : null;
                    if (theirs != null)
                    {
                        bool shelved = shelf != null && shelf.Spawned && shelf.Accepts(theirs)
                                       && p.inventory.innerContainer.TryTransferToContainer(theirs, shelf.GetDirectlyHeldThings(), 1) > 0;
                        if (!shelved) p.inventory.innerContainer.TryDrop(theirs, p.Position, map, ThingPlaceMode.Near, out _);
                    }
                }
                VisitorWork2.Say(p, VisitorWork2Math.Swap);
                if (left != null)
                    Tell.Message(NewsKind.World, Importance.Notable, null, "CC_V2_Book_Swapped".Translate(p.Named("PAWN"), VisitorWork2.Title(want).Named("BOOK"), left.Named("LEFT")).Resolve(),
                        new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                // Their business done: they go within the hour.
                if (trip != null && trip.arrived >= 0) trip.hours = Mathf.Min(trip.hours, (Find.TickManager.TicksGame - trip.arrived) / (float)VisitorMath.TicksPerHour + 0.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a book swapped at the shelf", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref deals, "deals", LookMode.Deep);
            Scribe_Values.Look(ref landedTick, "landedTick", -1);
            Scribe_Values.Look(ref landedMap, "landedMap", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (deals == null) deals = new List<BookDeal>();
                deals.RemoveAll(d => d == null);
            }
        }
    }

    /// <summary>
    /// The answer sets those waiting at the gate to what they came for (VisitorWork2Math's kinds) for
    /// <see cref="hours"/> hours of its own hours (answered at night, they sleep by the gate and start in the morning),
    /// then they go. No night given: they stay only for it. With no <see cref="work"/>, they are only kept by the gate
    /// that long by day, for what the answer sets going with them (a word with a colonist, a lesson).
    /// </summary>
    public class VisitEffect_WorkHere : DilemmaEffect
    {
        public string work;
        public int hours = 4;

        private bool Holds => work.NullOrEmpty();

        public override void Apply(DilemmaContext ctx)
        {
            if (!Visitors.On || !Holds && !VisitorWork2Math.Known(work)) return;
            try
            {
                var t = VisitorTracker.Instance?.WaitingFor(ctx);
                if (t == null || t.map == null) return;
                int h = VisitorWork2Math.HoursToCover(Holds ? VisitorWork2Math.Day : work, GenLocalDate.HourOfDay(t.map), hours);
                float before = t.arrived >= 0 ? (Find.TickManager.TicksGame - t.arrived) / (float)VisitorMath.TicksPerHour : 0f;
                // Their stay runs on the clock now, not on the letter (VisitorTracker's passing visits).
                t.kind = VisitKind.Pass;
                t.hours = Mathf.Max(t.hours, before + h);
                if (Holds) return;
                t.work = work;
                if (t.lord?.CurLordToil is LordToil_VisitHere here) here.UpdateAllDuties();
                foreach (var p in t.Standing)
                    if (p.CurJobDef == VisitorDefOf.Rimfolk_VisitSit || p.CurJobDef == JobDefOf.GotoWander || p.CurJobDef == JobDefOf.Wait_Wander)
                        p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors set to their work", e);
            }
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Visitors.On || !Holds && !VisitorWork2Math.Known(work) || VisitorTracker.Instance?.WaitingFor(ctx) == null) return;
            sheet.Play(("CC_Impact_V2Work_" + (Holds ? VisitorWork2Math.Day : work)).Translate(hours).Resolve(), "CC_Impact_V2WorkBrief".Translate().Resolve());
        }
    }
}
