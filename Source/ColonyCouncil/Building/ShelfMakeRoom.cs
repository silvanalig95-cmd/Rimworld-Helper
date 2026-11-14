using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    [DefOf]
    public static class ShelfDefOf
    {
        public static JobDef Rimfolk_MakeRoomOnShelf;

        static ShelfDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ShelfDefOf));
        }
    }

    /// <summary>
    /// Making room on a full shelf for a reserved kind (the player's report, 10 November 2026: a slot kept for meals held
    /// by something else, and a meal that couldn't be hauled for want of a free spot). Two ways, both behind the shelf
    /// reserves setting and the deeper game hooks (ShelfReserves.On):
    /// - On their own: a stack the reserves mark to move out that no other storage takes is set down on a free cell beside
    ///   the shelf (the game's own haul aside), while the shelf is short of room for a kind waiting for it
    ///   (Patch_ShelfMakeRoomAuto). It isn't put back while it would take the slot kept for that kind
    ///   (ShelfReserves.MayStore), so the usual hauling then brings the kind in.
    /// - Ordered: a colonist's menu on a stack of a reserved kind the game finds no storage for offers "Make room on the
    ///   shelf and haul it there" (FloatMenuOptionProvider_RimfolkShelfRoom), in place of the game's "Cannot haul" line
    ///   (Patch_ShelfMakeRoomMenu). Taken, the colonist carries a stack off the shelf to its best other storage, else sets
    ///   it down beside the shelf, then hauls the stack into the freed cell, kept for them from the order on. The shelf
    ///   may be of the same priority as where the stack lies (a reserved kind is fetched from there too), never lower.
    /// </summary>
    public static class ShelfMakeRoom
    {
        /// <summary>What a colonist does to make room: the shelf, the stack to take off it, the job doing that, and the freed cell the stack comes into.</summary>
        public class Plan
        {
            public Building_Storage shelf;
            public Thing occupant, thing;
            public IntVec3 freed;
            public Job first;
        }

        // ---------------- On their own ----------------

        /// <summary>
        /// For a stack the game found no storage for: a job setting it down beside its shelf when the reserves mark it to
        /// move out and the shelf is short of room for a kind that would come (ShelfReserves.Coming), counting the stacks
        /// already on their way out; else null.
        /// </summary>
        internal static Job SetDownJob(Pawn p, Thing t)
        {
            var map = p?.Map;
            if (map == null || t == null || !t.Spawned || t.Map != map || !ShelfReserves.On) return null;
            var shelf = map.haulDestinationManager.SlotGroupAt(t.Position)?.parent as Building_Storage;
            if (shelf == null || !ShelfReserves.Reserved.Contains(shelf)) return null;
            var reserves = ShelfReserves.Of(map);
            var st = reserves?.StateOf(shelf);
            if (st == null || st.waiting <= 0 || !st.moveOut.Contains(t)) return null;
            int leaving = 0;
            foreach (var o in st.moveOut)
                if (o != t && o.Spawned && map.reservationManager.IsReservedByAnyoneOf(o, Faction.OfPlayer)) leaving++;
            // Every slot waiting first (it costs nothing), then only those the usual hauling would fill.
            if (ShelfReserveMath.ShortOfRoom(st.capacity, st.held, st.waiting, leaving) <= 0) return null;
            if (ShelfReserveMath.ShortOfRoom(st.capacity, st.held, reserves.Coming(st, shelf), leaving) <= 0) return null;
            return HaulAIUtility.CanHaulAside(p, t, out var cell) ? MoveJob(t, cell, shelf, false) : null;
        }

        /// <summary>The job taking a stack off a shelf to a cell: whole, to storage or set down, saying it makes room.</summary>
        private static Job MoveJob(Thing t, IntVec3 cell, Building_Storage shelf, bool toStorage)
        {
            var job = JobMaker.MakeJob(ShelfDefOf.Rimfolk_MakeRoomOnShelf, t, cell, shelf);
            job.count = t.stackCount;
            job.haulMode = toStorage ? HaulMode.ToCellStorage : HaulMode.ToCellNonStorage;
            job.haulOpportunisticDuplicates = false;
            job.ignoreDesignations = true;
            return job;
        }

        // ---------------- Ordered ----------------

        /// <summary>
        /// For a stack the game's own haul finds no storage for: the reserved shelf with an empty slot it would fill (the
        /// best storage first, then the nearest), the stack to take off it and where that goes; null when no room can be made.
        /// </summary>
        public static Plan PlanFor(Pawn pawn, Thing thing)
        {
            var map = pawn?.Map;
            if (map == null || thing == null || !thing.Spawned || thing.Map != map || !ShelfReserves.On || ShelfReserves.Reserved.Count == 0) return null;
            if (thing.def.category != ThingCategory.Item || !thing.def.EverHaulable || thing is Corpse) return null;
            if (thing.IsForbidden(pawn) || thing.IsBurning() || !thing.IsSociallyProper(pawn, false, true)) return null;
            // The shelves that could take it once a stack is off them, looked at first: they cost little.
            var reserves = ShelfReserves.Of(map);
            var shelves = ShelfReserves.Reserved
                .Where(s => s != null && s.Spawned && s.Map == map && s.Faction == pawn.Faction && s.GetStoreSettings() != null)
                .Where(s => reserves?.StateOf(s) is ShelfReserves.State st && st.held >= st.capacity && st.FillsMissing(thing.def))
                .OrderByDescending(s => (int)s.GetStoreSettings().Priority).ThenBy(s => s.Position.DistanceToSquared(thing.Position)).ToList();
            if (shelves.Count == 0 || !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) return null;
            var current = StoreUtility.CurrentStoragePriorityOf(thing, true);
            if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, current, pawn.Faction, out _, out _)) return null;
            foreach (var shelf in shelves)
            {
                var plan = PlanOn(pawn, thing, shelf, current);
                if (plan != null) return plan;
            }
            return null;
        }

        private static Plan PlanOn(Pawn pawn, Thing thing, Building_Storage shelf, StoragePriority current)
        {
            var map = pawn.Map;
            var st = ShelfReserves.Of(map)?.StateOf(shelf);
            if (st == null || st.entries.Count == 0 || st.things == null || st.stacks == null || st.things.Count != st.stacks.Count) return null;
            if (shelf.OccupiedRect().Contains(thing.Position) || !shelf.HaulDestinationEnabled) return null;
            var settings = shelf.GetStoreSettings();
            bool allowed = settings.AllowedToAccept(thing) && !st.Barred(thing.def);
            // As good as where it lies or better: a reserved kind is fetched from storage of the shelf's own priority too.
            bool better = (int)settings.Priority >= (int)current;
            if (!ShelfReserveMath.MayMakeRoom(st.capacity, st.held, st.FillsMissing(thing.def), allowed, better)) return null;
            // What goes: what the reserves mark to move out, counting this kind as waiting if it isn't yet (it lies
            // outside the home area, say), else the stack of least worth of no reserved kind.
            var marked = new HashSet<Thing>(st.moveOut);
            if (!st.Waits(thing.def))
                foreach (int i in ShelfReserveMath.MoveOut(st.capacity, st.stacks, st.kinds.Count, st.waiting + 1, st.caps))
                    marked.Add(st.things[i]);
            var rect = shelf.OccupiedRect();
            var movable = st.things.Select(t => t.Spawned && t.Map == map && rect.Contains(t.Position) && !t.IsForbidden(pawn) && !t.IsBurning()
                && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly)).ToList();
            var worth = st.things.Select(t => t.MarketValue * t.stackCount).ToList();
            int pick = ShelfReserveMath.RoomFrom(st.stacks, st.things.Select(marked.Contains).ToList(), worth, movable);
            if (pick < 0) return null;
            var occupant = st.things[pick];
            var freed = occupant.Position;
            if (!pawn.CanReserve(freed) || !pawn.CanReach(freed, PathEndMode.ClosestTouch, Danger.Deadly)) return null;
            var first = FirstJob(pawn, occupant, shelf);
            return first == null ? null : new Plan { shelf = shelf, occupant = occupant, thing = thing, freed = freed, first = first };
        }

        /// <summary>The stack off the shelf: to its best other storage (the game's search), else set down on a free cell beside the shelf.</summary>
        private static Job FirstJob(Pawn pawn, Thing occupant, Building_Storage shelf)
        {
            if (StoreUtility.TryFindBestBetterStorageFor(occupant, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out var cell, out var dest) && dest != shelf)
            {
                if (dest is ISlotGroupParent && cell.IsValid) return MoveJob(occupant, cell, shelf, true);
                if (dest is Thing container && container.TryGetInnerInteractableThingOwner() != null) return HaulAIUtility.HaulToContainerJob(pawn, occupant, container);
            }
            return HaulAIUtility.CanHaulAside(pawn, occupant, out var aside) ? MoveJob(occupant, aside, shelf, false) : null;
        }

        /// <summary>
        /// The order taken: the stack off the shelf first, then the game's own haul of the thing into the freed cell, queued
        /// next and reserved at once (the thing and the cell), so nobody else fills the cell in between.
        /// </summary>
        public static void Take(Pawn pawn, Thing thing)
        {
            // The menu may have stood open a while: looked at again.
            var plan = PlanFor(pawn, thing);
            if (plan == null)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }
            if (!pawn.jobs.TryTakeOrderedJob(plan.first, JobTag.Misc)) return;
            var haul = JobMaker.MakeJob(JobDefOf.HaulToCell, plan.thing, plan.freed);
            int count = Math.Min(plan.thing.stackCount, plan.thing.def.stackLimit);
            int most = ShelfReserves.MostInto(plan.freed, pawn.Map, plan.thing);
            haul.count = most > 0 ? Math.Min(count, most) : count;
            haul.haulMode = HaulMode.ToCellStorage;
            haul.haulOpportunisticDuplicates = false;
            haul.playerForced = true;
            if (!haul.TryMakePreToilReservations(pawn, false)) return;
            // Straight after the first job, which is under way, or queued behind it (an order added to the queue).
            if (pawn.CurJob == plan.first) pawn.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
            else pawn.jobs.jobQueue.EnqueueLast(haul, JobTag.Misc);
        }

        // ---------------- Words ----------------

        /// <summary>The menu's line: "Make room on the shelf and haul simple meal x4 there".</summary>
        public static string Label(Plan plan) =>
            "CC_Shelf_MakeRoomHaul".Translate(plan.shelf.LabelNoCount.Named("SHELF"), plan.thing.Label.Named("THING")).Resolve();

        /// <summary>
        /// What the job is: "making room on the shelf for the simple meal." The kind is the stack queued to come into the
        /// shelf (an order), else the first kind waiting for room on it; null leaves the game's own words.
        /// </summary>
        internal static string Report(Pawn pawn, Job job)
        {
            if (!(job?.targetC.Thing is Building_Storage shelf) || !shelf.Spawned) return null;
            string kind = null;
            var queue = pawn?.jobs?.jobQueue;
            if (queue != null)
                foreach (var q in queue)
                {
                    var j = q?.job;
                    if (j == null || j.def != JobDefOf.HaulToCell || j.targetA.Thing == null || !shelf.OccupiedRect().Contains(j.targetB.Cell)) continue;
                    kind = "CC_Shelf_TheKind".Translate(j.targetA.Thing.LabelNoCount.Named("KIND")).Resolve();
                    break;
                }
            if (kind == null)
            {
                var k = ShelfReserves.Of(shelf.Map)?.StateOf(shelf)?.FirstWaiting();
                if (k == null) return null;
                kind = k.def != null ? "CC_Shelf_TheKind".Translate(k.def.label.Named("KIND")).Resolve() : k.cat?.label;
            }
            return kind.NullOrEmpty() ? null : "CC_Shelf_MakingRoomFor".Translate(shelf.LabelNoCount.Named("SHELF"), kind.Named("KIND")).Resolve();
        }

        /// <summary>
        /// The game's "Cannot haul X: No empty, accessible spot configured to store it" for a stack the menu also offers
        /// to make room for: taken out, so the line that works stands in its place.
        /// </summary>
        internal static void InPlaceOfCannotHaul(List<FloatMenuOption> options)
        {
            string verb = DefDatabase<WorkGiverDef>.GetNamedSilentFail("HaulGeneral")?.verb;
            string reason = HaulAIUtility.NoEmptyPlaceLowerTrans;
            if (verb.NullOrEmpty() || reason.NullOrEmpty()) return;
            var ours = options.OfType<ShelfRoomOption>().Where(o => !o.Disabled && o.thing != null).ToList();
            foreach (var o in ours)
            {
                string head = "CannotGenericWork".Translate(verb, o.thing.Label);
                string tail = ": " + reason.CapitalizeFirst();
                options.RemoveAll(x => x != o && x.Disabled && (x.iconThing == null || x.iconThing == o.thing)
                    && x.Label != null && x.Label.StartsWith(head) && x.Label.EndsWith(tail));
            }
        }
    }

    /// <summary>The menu's "Make room on the shelf and haul X there", knowing its stack (Patch_ShelfMakeRoomMenu).</summary>
    public class ShelfRoomOption : FloatMenuOption
    {
        public readonly Thing thing;

        public ShelfRoomOption(string label, Action action, Thing thing) : base(label, action)
        {
            this.thing = thing;
        }
    }

    /// <summary>
    /// Right-clicking a stack of a reserved kind with a colonist selected, when the game finds no storage for it and a
    /// reserved shelf could take it once a stack is off it: "Make room on the shelf and haul X there". The game finds
    /// this provider by itself (every FloatMenuOptionProvider in a loaded assembly).
    /// </summary>
    public class FloatMenuOptionProvider_RimfolkShelfRoom : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context) =>
            ShelfReserves.On && ShelfReserves.Reserved.Count > 0 && Current.ProgramState == ProgramState.Playing;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            var pawn = context.FirstSelectedPawn;
            if (pawn == null || clickedThing == null || clickedThing is Pawn || !pawn.IsColonistPlayerControlled || pawn.workSettings == null) return null;
            // Someone who doesn't haul keeps the game's own line, which says why.
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)) return null;
            try
            {
                var plan = ShelfMakeRoom.PlanFor(pawn, clickedThing);
                if (plan == null) return null;
                var thing = clickedThing;
                var option = new ShelfRoomOption(ShelfMakeRoom.Label(plan), () => ShelfMakeRoom.Take(pawn, thing), thing);
                return FloatMenuUtility.DecoratePrioritizedTask(option, pawn, thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reserved shelf slots: the menu", e);
                return null;
            }
        }
    }

    /// <summary>The job taking a stack off a shelf to make room (the game's haul to a cell), saying so.</summary>
    public class JobDriver_MakeRoomOnShelf : JobDriver_HaulToCell
    {
        public override string GetReport()
        {
            try
            {
                return ShelfMakeRoom.Report(pawn, job) ?? base.GetReport();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reserved shelf slots: the job's words", e);
                return base.GetReport();
            }
        }
    }

    /// <summary>
    /// A stack the reserves mark to move out that no other storage takes is set down beside the shelf while a reserved kind
    /// waits for its room (ShelfMakeRoom.SetDownJob), so it never blocks the slot for good; and a stack in storage of a
    /// reserved shelf's own priority that the shelf wants, which the game's search finds no better storage for, is
    /// fetched to it (ShelfReserves.FetchJob).
    /// </summary>
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob))]
    public static class Patch_ShelfMakeRoomAuto
    {
        private static bool broken;

        public static void Postfix(Pawn p, Thing t, ref Job __result)
        {
            if (__result != null || broken || ShelfReserves.Reserved.Count == 0 || t == null || p == null) return;
            try
            {
                var job = ShelfMakeRoom.SetDownJob(p, t) ?? ShelfReserves.FetchJob(p, t);
                if (job == null) return;
                JobFailReason.Clear();
                __result = job;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: making room", e);
            }
        }
    }

    /// <summary>"Make room on the shelf and haul X there" stands in place of the game's "Cannot haul X" line for the same stack (ShelfMakeRoom.InPlaceOfCannotHaul).</summary>
    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
    public static class Patch_ShelfMakeRoomMenu
    {
        private static bool broken;

        public static void Postfix(List<FloatMenuOption> __result)
        {
            if (broken || __result == null || __result.Count < 2 || ShelfReserves.Reserved.Count == 0 || !ShelfReserves.On) return;
            try
            {
                ShelfMakeRoom.InPlaceOfCannotHaul(__result);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: the haul line", e);
            }
        }
    }
}
