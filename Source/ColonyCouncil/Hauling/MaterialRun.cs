using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The long material run (the player's request, 7 November 2026): a pawn that fetches building material from far
    /// away (the setting's distance, 30 cells by default, straight from the material to the frame or blueprint) takes
    /// enough for every unfinished frame and blueprint of the build area round it that uses that material, not only the
    /// one it is building: its hands take what the game gives them, its inventory the rest, as far as its carrying weight
    /// allows (the fill limit, and Combat Extended's with that mod). Its next deliveries come out of its inventory, with
    /// the game's own delivery job, before anyone walks back to the stockpile: while its stock holds all the build area
    /// still needs of a material, the other builders leave that material to it. When the build area needs none of it any
    /// more, or the run went unused for two hours, what is left goes to storage (Pick Up And Haul's unloading with that
    /// mod). Picking up happens where the material lies: the stack the pawn stands at and the stacks within its reach.
    /// </summary>
    public static class MaterialRun
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => RimfolkPatches.On && S != null && S.materialRun;

        private static float Radius => HaulMath.RunRadius(S?.materialRunDistance ?? HaulMath.RunDistanceDefault);

        /// <summary>A frame or blueprint that takes delivered material (an install of a minified building doesn't).</summary>
        private static bool Builds(Thing t) => t is IConstructible && !(t is Blueprint_Install) && t.Spawned;

        /// <summary>
        /// How much of a material a frame or blueprint still has room for, past what is on its way to it (the game's own
        /// count): what the others bring, and what this pawn brings too unless it is the one left out.
        /// </summary>
        private static int Room(Thing t, ThingDef def, Pawn leftOut) =>
            t is IHaulEnroute e ? e.GetSpaceRemainingWithEnroute(def, leftOut) : t is IConstructible c ? c.ThingCountNeeded(def) : 0;

        /// <summary>A frame or blueprint of the pawn's people the pawn may build (the game's own test for "nearby" ones).</summary>
        private static bool Ours(Thing t, Pawn pawn) =>
            t.Faction == pawn.Faction && !t.IsForbidden(pawn) && GenConstruct.CanConstruct(t, pawn, false, false, JobDefOf.HaulToContainer);

        /// <summary>
        /// What the frames and blueprints of the build area round a cell still need of a material, not counting what the
        /// pawn itself is bringing (its job's share is the hands'); <paramref name="beyondAll"/> is what they need past
        /// everything on its way, the pawn's job too (what its stock must hold to cover them).
        /// </summary>
        public static int NeededAround(Pawn pawn, IntVec3 center, Map map, ThingDef def, float radius, out int beyondAll)
        {
            beyondAll = 0;
            if (map == null || def == null || !center.IsValid) return 0;
            int total = 0;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(center, map, radius, true))
            {
                if (!Builds(t)) continue;
                int n = Room(t, def, pawn);
                if (n <= 0 || !Ours(t, pawn)) continue;
                total += n;
                beyondAll += Room(t, def, null);
            }
            return total;
        }

        // ================= At the stack =================

        /// <summary>
        /// The game's delivery job is about to put material in the pawn's hands (Toils_Haul.StartCarryThing, hooked): from
        /// a stack far from the frame or blueprint, the inventory first takes what the build area needs past the hands'
        /// share, from that stack (never what the hands are to take) and from the stacks of it within the pawn's reach.
        /// From the pawn's own inventory (a delivery out of its stock), the run goes on round the new frame.
        /// </summary>
        public static void AtPickup(Pawn pawn)
        {
            if (!On || pawn?.jobs == null) return;
            Job job = pawn.CurJob;
            if (job == null || job.def != JobDefOf.HaulToContainer || pawn.jobs.curDriver == null || pawn.jobs.curDriver.GetType() != typeof(JobDriver_HaulToContainer)) return;
            Thing target = job.targetC.Thing ?? job.targetB.Thing;
            if (target == null || !Builds(target)) return;
            Thing stack = job.targetA.Thing;
            if (stack == null) return;
            var book = HaulBook.Get;
            if (book == null) return;
            int now = Find.TickManager.TicksGame;

            // Out of its own stock: the run is in use, round this frame now.
            if (!stack.Spawned)
            {
                var mine = book.LoadOf(pawn);
                if (mine != null && mine.RunCount(stack.def) > 0 && pawn.inventory.innerContainer.Contains(stack))
                {
                    mine.runTick = now;
                    mine.runCenter = target.Position;
                }
                return;
            }
            if (stack.Map != pawn.Map || !HaulMath.Far(stack.Position.DistanceTo(target.Position), S.materialRunDistance)) return;
            if (!Carrying.CanCarry(pawn)) return;

            ThingDef def = stack.def;
            var load = book.LoadOf(pawn);
            load?.Reconcile(pawn);
            int need = NeededAround(pawn, target.Position, pawn.Map, def, Radius, out int beyond);
            Thing carried = pawn.carryTracker.CarriedThing;
            int inHands = carried != null && carried.def == def ? carried.stackCount : 0;
            // The hands take the job's count (what is still to come into them), as much as they hold.
            int handsToCome = Math.Max(0, Math.Min(job.count, pawn.carryTracker.AvailableStackSpace(def)));
            int extra = HaulMath.Extra(need, inHands + handsToCome, load?.RunCount(def) ?? 0);
            if (extra <= 0) return;

            int took = 0;
            int spare = stack.stackCount - Math.Min(handsToCome, stack.stackCount);
            if (spare > 0) took += Carrying.TakeToInventory(pawn, stack, Math.Min(extra, spare));
            if (took < extra)
            {
                var queued = job.targetQueueA;
                foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(pawn).Concat(new[] { pawn.Position }))
                {
                    if (took >= extra) break;
                    if (!c.InBounds(pawn.Map)) continue;
                    var here = c.GetThingList(pawn.Map);
                    for (int i = here.Count - 1; i >= 0 && took < extra; i--)
                    {
                        Thing t = here[i];
                        if (t == stack || t.def != def || !t.Spawned || t.IsForbidden(pawn) || t.IsBurning()) continue;
                        if (queued != null && queued.Any(q => q.Thing == t)) continue;
                        if (!pawn.CanReserve(t)) continue;
                        int n = Carrying.TakeToInventory(pawn, t, extra - took);
                        if (n <= 0) break;
                        took += n;
                    }
                }
            }
            if (took <= 0) return;
            load = book.LoadFor(pawn);
            load.AddRun(def, took, target.Position, now);
            load.areaNeed[def] = beyond;
            Carrying.Debug($"{pawn.LabelShort} takes {took}x {def.label} more for the build round {target.LabelShort} (the area needs {need}).");
        }

        // ================= Deliveries out of the stock =================

        /// <summary>
        /// The game's delivery job for a frame or blueprint, with the material from the pawn's run stock: one stack of its
        /// inventory, for this one and the game's nearby ones (eight cells), as much as its hands hold. Null when the pawn
        /// has none of what this one needs, or already carries something (the game then uses what is in its hands).
        /// </summary>
        public static Job FromStock(Pawn pawn, IConstructible c, bool forced)
        {
            if (!On || c == null || c is Blueprint_Install || pawn?.carryTracker == null || pawn.carryTracker.CarriedThing != null) return null;
            var load = HaulBook.Get?.LoadOf(pawn);
            if (load == null || load.RunEmpty) return null;
            Thing ct = (Thing)c;
            if (!ct.Spawned || ct.Map != pawn.Map) return null;
            foreach (var need in c.TotalMaterialCost())
            {
                ThingDef def = need.thingDef;
                int stock = load.RunCount(def);
                if (stock <= 0) continue;
                Thing inv = Carrying.FirstInInventory(pawn, def);
                if (inv == null) continue;
                int num = forced || !(c is IHaulEnroute e) ? c.ThingCountNeeded(def) : e.GetSpaceRemainingWithEnroute(def, pawn);
                if (num <= 0) continue;
                int cap = Math.Min(Math.Min(stock, inv.stackCount), pawn.carryTracker.MaxStackSpaceEver(def));
                int total = num;
                var others = new List<Thing>();
                foreach (Thing t in GenRadial.RadialDistinctThingsAround(ct.Position, ct.Map, HaulMath.NearbyNeeders, true))
                {
                    if (total >= cap) break;
                    if (t == ct || !Builds(t)) continue;
                    int n = Room(t, def, pawn);
                    if (n <= 0 || !Ours(t, pawn)) continue;
                    others.Add(t);
                    total += n;
                }
                int count = Math.Min(total, cap);
                if (count <= 0) continue;
                // The first stop is the nearest to the pawn, as the game picks the nearest to the material.
                var stops = new List<Thing>(others) { ct };
                Thing first = stops.MinBy(t => IntVec3Utility.ManhattanDistanceFlat(pawn.Position, t.Position));
                stops.Remove(first);
                Job job = JobMaker.MakeJob(JobDefOf.HaulToContainer);
                job.targetA = inv;
                job.targetB = first;
                job.targetC = ct;
                if (stops.Count > 0) job.targetQueueB = stops.Select(t => new LocalTargetInfo(t)).ToList();
                job.count = count;
                job.haulMode = HaulMode.ToContainer;
                return job;
            }
            return null;
        }

        // ================= The other builders =================

        /// <summary>
        /// Every material this frame or blueprint still needs is covered by another pawn's run: its stock holds all the
        /// build area still needs of it, it used the run within the hour, and it can carry on (not downed, drafted or in a
        /// mental state). The other builders then leave it be rather than walk to the stockpile. Never for an order.
        /// </summary>
        public static bool CoveredForOthers(Pawn pawn, IConstructible c, bool forced)
        {
            if (!On || forced || c == null || c is Blueprint_Install) return false;
            var book = HaulBook.Get;
            if (book == null || !book.AnyLoads) return false;
            Thing ct = (Thing)c;
            if (!ct.Spawned) return false;
            bool any = false;
            int now = Find.TickManager.TicksGame;
            foreach (var need in c.TotalMaterialCost())
            {
                ThingDef def = need.thingDef;
                if (Room(ct, def, pawn) <= 0) continue;
                any = true;
                if (!CoveredBy(book, pawn, ct, def, now)) return false;
            }
            return any;
        }

        private static bool CoveredBy(HaulBook book, Pawn pawn, Thing ct, ThingDef def, int now)
        {
            float r = Radius;
            foreach (var kv in book.Runners)
            {
                Pawn runner = kv.Key;
                var load = kv.Value;
                if (runner == pawn || runner == null || !runner.Spawned || runner.Map != ct.Map || runner.Downed || runner.Drafted || runner.InMentalState) continue;
                if (!load.runCenter.IsValid || now - load.runTick > HaulMath.CoverTicks || !load.runCenter.InHorDistOf(ct.Position, r)) continue;
                if (!load.areaNeed.TryGetValue(def, out int areaNeed)) continue;
                if (HaulMath.Covered(load.RunCount(def), areaNeed)) return true;
            }
            return false;
        }

        // ================= The book's look =================

        /// <summary>
        /// Every 250 ticks for a pawn on a run: what its build area still needs of each material it carries (the other
        /// builders' cover), and the end of the run for a material the area needs none of any more (or after two hours
        /// unused): what is left goes to storage.
        /// </summary>
        public static void Look(Pawn pawn, HaulLoad load, int now)
        {
            if (!load.runCenter.IsValid || pawn.Map == null)
            {
                foreach (var def in load.run.Keys.ToList()) load.RunToStorage(def);
                return;
            }
            // Not while it is about to put some in a frame.
            Job cur = pawn.CurJob;
            bool delivering = cur != null && cur.def == JobDefOf.HaulToContainer;
            foreach (var def in load.run.Keys.ToList())
            {
                int need = NeededAround(pawn, load.runCenter, pawn.Map, def, Radius, out int beyond);
                load.areaNeed[def] = beyond;
                if (delivering && cur.targetA.Thing?.def == def) continue;
                if (!On || HaulMath.RunOver(need, load.runTick, now))
                {
                    load.RunToStorage(def);
                    Carrying.Debug($"{pawn.LabelShort}'s run of {def.label} is over (the area needs {need}); what is left goes to storage.");
                }
            }
        }
    }
}
