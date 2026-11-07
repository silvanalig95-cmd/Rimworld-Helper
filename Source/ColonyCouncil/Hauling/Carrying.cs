using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    [DefOf]
    public static class HaulDefOf
    {
        public static JobDef Rimfolk_CarryToStorage;

        static HaulDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HaulDefOf));
        }
    }

    /// <summary>
    /// What harvest and haul, the material run and things on the way share: who takes part, how much fits, what a pawn
    /// keeps, when it may go to storage, the job that takes it there (Pick Up And Haul's own unloading for what is wholly
    /// Rimfolk's, when that mod is loaded), the book's pass every 250 ticks, and the take-over of the standalone mod's loads.
    /// </summary>
    public static class Carrying
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static void Debug(string message)
        {
            if (S != null && S.haulLog) Log.Message("[Rimfolk] Carrying: " + message);
        }

        // ================= Who and how much =================

        /// <summary>
        /// The colony's own people, and its mechanoids that can carry when the setting allows; spawned and able, with an
        /// inventory and room in it, and never one whose inventory holds a caravan's packs.
        /// </summary>
        public static bool CanCarry(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null) return false;
            if (pawn.Faction == null || !pawn.Faction.IsPlayer || pawn.IsQuestLodger()) return false;
            if (pawn.inventory == null || pawn.carryTracker == null) return false;
            if (pawn.RaceProps.IsMechanoid)
            {
                if (S == null || !S.haulMechs) return false;
            }
            else if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.inventory.HasAnyUnpackedCaravanItems) return false;
            return MassUtility.CanEverCarryAnything(pawn) && MassUtility.Capacity(pawn) > 0f;
        }

        public static float FreeMass(Pawn pawn) => HaulMath.FreeMass(MassUtility.Capacity(pawn), S?.haulFill ?? 1f, MassUtility.GearAndInventoryMass(pawn));

        /// <summary>How many of a stack fit in the pawn's inventory under the fill limit (and Combat Extended's, with that mod).</summary>
        public static int CountThatFits(Pawn pawn, Thing thing)
        {
            if (thing == null) return 0;
            int n = HaulMath.CountThatFits(thing.stackCount, thing.GetStatValue(StatDefOf.Mass), FreeMass(pawn));
            return n <= 0 ? 0 : HaulLinks.CeLimit(pawn, thing, n);
        }

        /// <summary>
        /// Moves part of a stack into the pawn's inventory, as many as fit and no more than asked; how many went in. The
        /// game's pickup sound plays, but not for what work drops at the pawn's feet (the work has its own sounds).
        /// </summary>
        public static int TakeToInventory(Pawn pawn, Thing thing, int want, bool sound = true)
        {
            if (thing == null || !thing.Spawned || want <= 0) return 0;
            int count = Math.Min(want, CountThatFits(pawn, thing));
            if (count <= 0) return 0;
            IntVec3 pos = thing.Position;
            Map map = thing.Map;
            ThingDef def = thing.def;
            Thing taken = thing.SplitOff(count);
            int n = taken.stackCount;
            if (!pawn.inventory.innerContainer.TryAdd(taken, true))
            {
                if (!taken.Destroyed && !taken.Spawned) GenPlace.TryPlaceThing(taken, pos, map, ThingPlaceMode.Near);
                return 0;
            }
            if (sound) def.soundPickup?.PlayOneShot(new TargetInfo(pawn.Position, map));
            return n;
        }

        public static int InInventory(Pawn pawn, ThingDef def) => pawn?.inventory == null || def == null ? 0 : pawn.inventory.innerContainer.TotalStackCountOfDef(def);

        public static Thing FirstInInventory(Pawn pawn, ThingDef def)
        {
            var inv = pawn?.inventory?.innerContainer;
            if (inv == null) return null;
            for (int i = 0; i < inv.Count; i++)
                if (inv[i].def == def)
                    return inv[i];
            return null;
        }

        /// <summary>
        /// What the pawn keeps of a kind, as the game's own unloading keeps it: what its drug policy carries, its
        /// inventory stock, and the packed food it wants.
        /// </summary>
        public static int Kept(Pawn pawn, ThingDef def)
        {
            if (pawn == null || def == null) return 0;
            int keep = 0;
            var policy = pawn.drugs?.CurrentPolicy;
            if (policy != null)
                for (int i = 0; i < policy.Count; i++)
                    if (policy[i].drug == def)
                        keep += Math.Max(0, policy[i].takeToInventory);
            var stock = pawn.inventoryStock?.stockEntries;
            if (stock != null)
                foreach (var entry in stock.Values)
                    if (entry != null && entry.thingDef == def)
                        keep += Math.Max(0, entry.count);
            if (pawn.IsColonist && def.IsNutritionGivingIngestible && !def.IsDrug && pawn.needs?.food != null)
            {
                Thing sample = FirstInInventory(pawn, def);
                if (sample != null && JobGiver_PackFood.IsGoodPackableFoodFor(sample, pawn, false))
                {
                    int have = InInventory(pawn, def);
                    float each = sample.GetStatValue(StatDefOf.Nutrition);
                    float other = JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn) - each * have;
                    keep += HaulMath.FoodKept(have, each, other, pawn.needs.food.MaxLevel);
                }
            }
            return keep;
        }

        // ================= When it may go to storage =================

        /// <summary>
        /// Not now: drafted, downed, in a mental state, burning, in a lord (a ritual, a caravan forming), with enemies close,
        /// lying in bed, urgently hungry or very tired, carrying something in the hands, or a delivery failed lately. Being ill
        /// alone doesn't stop it (the player's game, 7 November 2026: a grower with tuberculosis kept harvesting with a full
        /// pack he was never let take to storage, so every harvest after fell to the ground): the delivery only ever takes the
        /// place of work, never of rest, so one who is up and working stores the load before working on.
        /// </summary>
        public static bool CanDeliverNow(Pawn pawn, HaulLoad load)
        {
            if (!pawn.Spawned || pawn.Map == null || pawn.Downed || pawn.Drafted || pawn.InMentalState) return false;
            if (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null) return false;
            if (pawn.IsBurning() || pawn.GetLord() != null) return false;
            if (pawn.mindState != null && pawn.mindState.anyCloseHostilesRecently) return false;
            if (pawn.InBed()) return false;
            var food = pawn.needs?.food;
            if (food != null && food.CurCategory >= HungerCategory.UrgentlyHungry) return false;
            var rest = pawn.needs?.rest;
            if (rest != null && rest.CurCategory >= RestCategory.VeryTired) return false;
            int now = Find.TickManager.TicksGame;
            if (load.lastDeliveryJobTick == now) return false;
            if (now - load.lastDeliveryFailTick < HaulMath.DeliveryFailCooldownTicks) return false;
            return true;
        }

        public static void Notify_DeliveryFailed(Pawn pawn)
        {
            var load = HaulBook.Get?.LoadOf(pawn);
            if (load != null) load.lastDeliveryFailTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Building work: the game's delivery to a frame or blueprint, or a job of the construction work type. While a pawn
        /// still has a run's material and builds on, what must go to storage waits for the end of the run, so a run of two
        /// materials makes one trip back, not two.
        /// </summary>
        public static bool Builds(Job job) =>
            job != null && (job.def == JobDefOf.HaulToContainer && job.GetTarget(TargetIndex.C).Thing is IConstructible
                            || job.workGiverDef?.workType == WorkTypeDefOf.Construction || job.def == JobDefOf.FinishFrame);

        public static bool StillBuilding(Pawn pawn, HaulLoad load, Job job) => !load.RunEmpty && Builds(job);

        /// <summary>A delivery of Rimfolk's or Pick Up And Haul's is the pawn's job now or waits in its queue.</summary>
        public static bool DeliveryUnderWay(Pawn pawn)
        {
            if (IsDelivery(pawn.CurJob)) return true;
            var q = pawn.jobs?.jobQueue;
            if (q == null) return false;
            for (int i = 0; i < q.Count; i++)
                if (IsDelivery(q[i].job))
                    return true;
            return false;
        }

        private static bool IsDelivery(Job job) => job != null && (job.def == HaulDefOf.Rimfolk_CarryToStorage || HaulLinks.IsPuahUnload(job));

        // ================= The job that takes it there =================

        /// <summary>
        /// The job that takes the load to storage, or null when there is nothing: with Pick Up And Haul, every stack that
        /// is wholly Rimfolk's (none of it kept, none of it the pawn's own) is handed to that mod's unloading, so it all
        /// goes as one; the rest, the stack carried in the hands and anything to pick up on the way go by Rimfolk's own.
        /// </summary>
        public static Job DeliveryJob(Pawn pawn, HaulLoad load)
        {
            if (pawn == null) return null;
            List<Thing> onTheWay = OnTheWay.ForDelivery(pawn, load);
            bool handed = false;
            if (load != null && load.leftover == null && HaulLinks.PuahFor(pawn)) handed = HandOver(pawn, load);
            bool ours = load != null && (!load.Empty || load.leftover != null) || onTheWay.Count > 0;
            if (!ours) return handed || HaulLinks.PuahCount(pawn) > 0 ? HaulLinks.PuahUnloadJob(pawn) : null;
            Job job = JobMaker.MakeJob(HaulDefOf.Rimfolk_CarryToStorage);
            if (onTheWay.Count > 0) job.targetQueueA = onTheWay.Select(t => new LocalTargetInfo(t)).ToList();
            return job;
        }

        /// <summary>
        /// Hands what is wholly Rimfolk's to Pick Up And Haul's unloading; true if anything went. A kind a bill sent to a
        /// stockpile the player picked stays with Rimfolk's job, which takes it there (that mod's unloading takes all to the best).
        /// </summary>
        public static bool HandOver(Pawn pawn, HaulLoad load)
        {
            bool any = false;
            foreach (var def in load.owed.Keys.ToList())
            {
                if (load.IntoOf(def).IsValid) continue;
                int have = InInventory(pawn, def);
                if (!HaulMath.WhollyOurs(load.OwedCount(def), have, Kept(pawn, def))) continue;
                var inv = pawn.inventory.innerContainer;
                bool all = true;
                for (int i = 0; i < inv.Count; i++)
                    if (inv[i].def == def)
                        all &= HaulLinks.HandToPuah(pawn, inv[i]);
                if (!all) continue;
                load.Remove(def, have);
                any = true;
                Debug($"{pawn.LabelShort} hands {have}x {def.label} to Pick Up And Haul's unloading.");
            }
            return any;
        }

        /// <summary>
        /// The storage cell a stack from the inventory goes to: in the stockpile a bill picked for that kind while it still
        /// takes it, else the best storage the game's own search finds (where the game's haulers would take it anyway).
        /// </summary>
        public static bool StoreCellFor(Pawn pawn, Thing thing, HaulLoad load, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            Map map = pawn.Map;
            if (map == null || thing == null) return false;
            IntVec3 into = load?.IntoOf(thing.def) ?? IntVec3.Invalid;
            if (into.IsValid && into.InBounds(map))
            {
                SlotGroup group = into.GetSlotGroup(map);
                ISlotGroup picked = (ISlotGroup)group?.StorageGroup ?? group;
                if (picked != null && StoreUtility.TryFindBestBetterStoreCellForIn(thing, pawn, map, StoragePriority.Unstored, pawn.Faction, picked, out cell)) return true;
            }
            return StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, map, StoragePriority.Unstored, pawn.Faction, out cell);
        }

        /// <summary>Queues the delivery after the pawn's present job, when nothing else waits in its queue and it may go now.</summary>
        public static bool TryQueueDelivery(Pawn pawn, HaulLoad load)
        {
            if (!CanDeliverNow(pawn, load) || DeliveryUnderWay(pawn)) return false;
            if (pawn.jobs?.jobQueue == null || pawn.jobs.jobQueue.Count > 0) return false;
            Job job = DeliveryJob(pawn, load);
            if (job == null) return false;
            load.lastDeliveryJobTick = Find.TickManager.TicksGame;
            pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.MiscWork);
            Debug($"{pawn.LabelShort} will take its load to storage next ({job.def.defName}).");
            return true;
        }

        // ================= The book's pass =================

        /// <summary>
        /// Every 250 ticks for each pawn carrying something for Rimfolk: never more owed than it has; a run whose build
        /// area needs no more (or that went unused for two hours) ends and its leftovers go to storage; meals go now while
        /// someone is hungry with no meal in reach; a load that must go now, or that the job choice can't send (harvest
        /// and haul off or standing down), is queued for storage.
        /// </summary>
        public static void Pass(Pawn pawn, HaulLoad load, int now)
        {
            if (!pawn.Spawned)
            {
                // Away in a caravan or a shuttle: the run's area is on a map it has left. What it owes stays owed.
                load.DropRun();
                load.Reconcile(pawn);
                load.haulNow.Clear();
                return;
            }
            load.Reconcile(pawn);
            if (load.haulNow.Count > 0 && now - load.haulNowTick > HaulMath.HaulNowWindowTicks) load.haulNow.Clear();
            if (!load.RunEmpty) MaterialRun.Look(pawn, load, now);
            // Meals carried while someone is hungry and the meals in reach don't feed them all go to storage now.
            if (!load.now && WorkProducts.MealsWanted(pawn, load))
            {
                load.now = true;
                Debug($"{pawn.LabelShort} carries meals while someone is hungry: to storage now.");
            }
            bool stuck = HaulMath.Stuck(load.startedTick, now, ColonyCouncilMod.Settings?.haulMaxHours ?? 8f);
            if (!load.Empty && (load.now || stuck || !HarvestHaul.Works) && !StillBuilding(pawn, load, pawn.CurJob)) TryQueueDelivery(pawn, load);
        }

        // ================= The standalone mod's loads =================

        /// <summary>The loads the standalone mod's pawns carried in this save, taken over: owed as before, and to storage at the next chance.</summary>
        public static int TakeOver(List<HaulMath.StandaloneLoad> from, HaulBook book)
        {
            if (from == null || from.Count == 0 || book == null) return 0;
            var byId = new Dictionary<string, Pawn>();
            foreach (var p in PawnsFinder.AllMaps_Spawned)
            {
                string id = p.GetUniqueLoadID();
                if (!byId.ContainsKey(id)) byId[id] = p;
            }
            int taken = 0;
            foreach (var l in from)
            {
                if (l?.pawnId == null || !byId.TryGetValue(l.pawnId, out var pawn) || pawn.Faction == null || !pawn.Faction.IsPlayer) continue;
                var load = book.LoadFor(pawn);
                foreach (var kv in l.owed)
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                    if (def != null) load.Add(def, kv.Value);
                }
                foreach (var s in l.skills)
                {
                    var skill = DefDatabase<SkillDef>.GetNamedSilentFail(s);
                    if (skill != null) load.AddSkill(skill);
                }
                load.Reconcile(pawn);
                if (load.Empty) continue;
                if (l.startedTick >= 0) load.startedTick = l.startedTick;
                load.full = l.full;
                load.now = true;
                taken++;
            }
            return taken;
        }
    }
}
