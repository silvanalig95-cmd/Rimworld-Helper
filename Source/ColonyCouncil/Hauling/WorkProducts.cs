using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Everything else a pawn's work makes goes into its inventory while it fits, and to storage with the rest of its
    /// load by harvest and haul's rules (the player's request, 7 November 2026: "they should always take any 'produced'
    /// item or resource into their inventory when they have space, to later store it, for any possible item creating or
    /// harvesting interaction in the game and mods"). Two ways in:
    /// <list type="bullet">
    /// <item>A bill's products (<see cref="PocketBill"/>): in the game's own last toil of a bill, after the products are
    /// made and before the game places them, those of a bill that takes them to a stockpile go into the inventory (a
    /// stockpile the player picked is remembered for the delivery). What doesn't fit is placed or carried the game's way.</item>
    /// <item>What any other work job makes beside the pawn (the one generic catch): a thing made while the job runs (not
    /// split off a stack) or taken out of a building's container is noted for that job. When the job is about to carry
    /// it to a storage cell in its hands (a fermenting barrel, an egg box, a processor, a beehouse, a harvester), it goes
    /// into the inventory instead and the job ends there; what the job left lying beside the pawn is taken when it ends.</item>
    /// </list>
    /// Never what the colony would rather have where it is: meals while someone hungry has no other meal in reach,
    /// medicine, weapons (Simple Sidearms reads a weapon in the inventory as a sidearm), what dissolves (wastepacks), what
    /// the weather ruins or hatches (fertilized eggs, wort), what the player uses where it lies (xenogerms, genepacks,
    /// mechlinks), corpses and minified things, a bill's product meant to stay at the bench, a long project's masterpiece.
    /// Never what a pawn drops on purpose (from its hands or inventory), what a hauler hauls, a bill's ingredients, or
    /// anything the player ordered.
    /// </summary>
    public static class WorkProducts
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The pawn whose job runs a toil or a tick just now (the job driver's hooks), on the main thread only.</summary>
        [ThreadStatic] public static Pawn actor;
        /// <summary>The pawn in the game's own last toil of a bill, while its products are placed.</summary>
        [ThreadStatic] public static Pawn finishing;

        /// <summary>What one work job made so far, and the family its products belong to.</summary>
        private sealed class Made
        {
            public Job job;
            public HaulMath.MadeFamily family;
            public readonly List<Thing> things = new List<Thing>();
        }

        private static readonly Dictionary<Pawn, Made> made = new Dictionary<Pawn, Made>();
        private static readonly List<Thing> tmpOne = new List<Thing>(1);

        /// <summary>Some job has noted something it made (the hooks' fast way out otherwise).</summary>
        public static bool Any => made.Count > 0;

        /// <summary>A new game: nothing noted.</summary>
        public static void Reset() => made.Clear();

        // ================= What may go into an inventory =================

        /// <summary>
        /// A kind that may go into a working pawn's inventory: an item that goes to storage (not a corpse or a minified
        /// thing, an extracted tree among them; chunks only with that setting), never medicine (the doctors' to reach), never a weapon (Simple Sidearms
        /// would read it as a sidearm), never what dissolves out of storage (a wastepack), never what the weather ruins or
        /// what hatches (a fertilized egg, wort): those go straight to storage in the hands, as the game takes them. Nor
        /// what the player uses where it lies (Biotech's xenogerms, genepacks, embryos, ova and mechlinks).
        /// </summary>
        public static bool PocketableDef(ThingDef def)
        {
            if (def == null || !HarvestHaul.Collectable(def)) return false;
            if (def.IsMedicine || def.IsWeapon) return false;
            Type cls = def.thingClass;
            if (cls != null && (typeof(MinifiedThing).IsAssignableFrom(cls) || typeof(Xenogerm).IsAssignableFrom(cls) || typeof(Genepack).IsAssignableFrom(cls)
                                || typeof(HumanEmbryo).IsAssignableFrom(cls) || typeof(HumanOvum).IsAssignableFrom(cls) || typeof(Mechlink).IsAssignableFrom(cls))) return false;
            return !def.HasComp<CompDissolution>() && !def.HasComp<CompTemperatureRuinable>() && !def.HasComp<CompHatcher>();
        }

        /// <summary>
        /// What pawns on the bill's map carry for Rimfolk of the bill's product, counted as the bill counts it (its quality,
        /// hit points and stuff), so a "do until" bill doesn't make more while the products ride to storage in a pocket.
        /// With "count equipped" the game counts inventories itself; with a stockpile to count in, only what goes there.
        /// </summary>
        public static int CarriedFor(RecipeWorkerCounter counter, Bill_Production bill)
        {
            var book = HaulBook.Get;
            Map map = bill?.Map;
            if (book == null || !book.AnyLoads || map == null || bill.includeEquipped) return 0;
            var products = bill.recipe?.products;
            if (products == null || products.Count == 0 || products[0]?.thingDef == null) return 0;
            ThingDef def = products[0].thingDef;
            ISlotGroup include = bill.GetIncludeSlotGroup();
            int n = 0;
            foreach (var kv in book.All)
            {
                Pawn p = kv.Key;
                HaulLoad load = kv.Value;
                if (p == null || !p.Spawned || p.Map != map || p.inventory == null || load == null) continue;
                int owed = load.OwedCount(def);
                if (owed <= 0 || include != null && !InGroup(load.IntoOf(def), map, include)) continue;
                int valid = 0;
                var inv = p.inventory.innerContainer;
                for (int i = 0; i < inv.Count; i++)
                    if (counter.CountValidThing(inv[i], bill, def))
                        valid += inv[i].stackCount;
                n += HaulMath.Carried(owed, valid);
            }
            return n;
        }

        /// <summary>What pawns on the map carry for Rimfolk of a category (the game's stone block and butchering counts go by category).</summary>
        public static int CarriedOf(Map map, ThingCategoryDef category)
        {
            var book = HaulBook.Get;
            if (book == null || !book.AnyLoads || map == null || category?.childThingDefs == null) return 0;
            int n = 0;
            foreach (var kv in book.All)
            {
                Pawn p = kv.Key;
                if (p == null || !p.Spawned || p.Map != map || kv.Value == null) continue;
                foreach (var owed in kv.Value.owed)
                    if (category.childThingDefs.Contains(owed.Key))
                        n += HaulMath.Carried(owed.Value, Carrying.InInventory(p, owed.Key));
            }
            return n;
        }

        private static bool InGroup(IntVec3 cell, Map map, ISlotGroup group)
        {
            if (!cell.IsValid || !cell.InBounds(map)) return false;
            SlotGroup at = cell.GetSlotGroup(map);
            return at != null && (at == group || at.StorageGroup == group);
        }

        public static bool IsMeal(ThingDef def) => def?.ingestible != null && def.ingestible.IsMeal;

        /// <summary>How many of a thing the rules let into the inventory, before the room: a meal only past what the hungry need.</summary>
        private static int Allowed(Pawn pawn, Thing thing, bool spawned)
        {
            if (!IsMeal(thing.def) || pawn.Map == null) return thing.stackCount;
            int inReach = MealsInReach(pawn.Map) - (spawned ? thing.stackCount : 0);
            return HaulMath.MealsToPocket(thing.stackCount, Hungry(pawn.Map), inReach);
        }

        /// <summary>The colony's free people on the map who are hungry or worse.</summary>
        public static int Hungry(Map map)
        {
            int n = 0;
            var people = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < people.Count; i++)
            {
                var food = people[i].needs?.food;
                if (food != null && food.CurCategory >= HungerCategory.Hungry) n++;
            }
            return n;
        }

        /// <summary>The meals lying on the map that anyone may eat (a working paste dispenser feeds them all).</summary>
        public static int MealsInReach(Map map)
        {
            foreach (var dispenser in map.listerBuildings.AllBuildingsColonistOfClass<Building_NutrientPasteDispenser>())
                if (dispenser.CanDispenseNow)
                    return 9999;
            int n = 0;
            var food = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            for (int i = 0; i < food.Count && n < 9999; i++)
            {
                Thing t = food[i];
                if (t.Spawned && IsMeal(t.def) && !t.IsForbidden(Faction.OfPlayer)) n += t.stackCount;
            }
            return n;
        }

        /// <summary>The load holds meals.</summary>
        public static bool HasMeals(HaulLoad load)
        {
            if (load == null) return false;
            foreach (var def in load.owed.Keys)
                if (IsMeal(def))
                    return true;
            return false;
        }

        /// <summary>The pawn carries meals while someone hungry has none in reach: they go to storage now.</summary>
        public static bool MealsWanted(Pawn pawn, HaulLoad load)
        {
            Map map = pawn?.Map;
            if (map == null || !HasMeals(load)) return false;
            return HaulMath.MealsWanted(Hungry(map), MealsInReach(map));
        }

        // ================= A bill's products =================

        public static HaulMath.StoreMode ModeOf(BillStoreModeDef mode)
        {
            if (mode == BillStoreModeDefOf.DropOnFloor) return HaulMath.StoreMode.Floor;
            if (mode == BillStoreModeDefOf.BestStockpile) return HaulMath.StoreMode.Best;
            if (mode == BillStoreModeDefOf.SpecificStockpile) return HaulMath.StoreMode.Specific;
            return HaulMath.StoreMode.Other;
        }

        /// <summary>
        /// The game has made a bill's products (RecordsUtility.Notify_BillDone, in its own last toil of a bill) and is
        /// about to place them. Those the rules let in and some storage takes (the picked stockpile for that store mode)
        /// go into the inventory, as many as fit, and come off the product list; the game then places or carries the rest
        /// its own way, with the bill's store mode. The quests that count what is made are told of what went in.
        /// </summary>
        public static void PocketBill(Pawn pawn, List<Thing> products)
        {
            if (S == null || !S.haulBills || products == null || products.Count == 0 || !HarvestHaul.Works) return;
            Job job = pawn.CurJob;
            Bill bill = job?.bill;
            if (bill == null || !(pawn.jobs.curDriver is JobDriver_DoBill) || !Carrying.CanCarry(pawn)) return;
            var mode = ModeOf(bill.GetStoreMode());
            float work = job.RecipeDef != null ? job.RecipeDef.WorkAmountTotal(job.GetTarget(TargetIndex.B).Thing) : 0f;
            if (!HaulMath.BillPockets(mode, job.playerForced, bill is Bill_Medical || bill is Bill_Autonomous, work)) return;
            ISlotGroup group = mode == HaulMath.StoreMode.Specific ? bill.GetSlotGroup() : null;
            if (mode == HaulMath.StoreMode.Specific && group == null) return;
            var book = HaulBook.Get;
            Map map = pawn.Map;
            if (book == null || map == null) return;

            HaulLoad load = null;
            bool full = false;
            int pieces = 0;
            for (int i = 0; i < products.Count; i++)
            {
                Thing p = products[i];
                if (p == null || p.Destroyed || p.Spawned || p.holdingOwner != null || !PocketableDef(p.def)) continue;
                IntVec3 cell;
                bool stored = group != null
                    ? StoreUtility.TryFindBestBetterStoreCellForIn(p, pawn, map, StoragePriority.Unstored, pawn.Faction, group, out cell, false)
                    : StoreUtility.TryFindBestBetterStoreCellFor(p, pawn, map, StoragePriority.Unstored, pawn.Faction, out cell, false);
                if (!stored) continue;
                int want = Math.Min(p.stackCount, Allowed(pawn, p, false));
                int fits = Math.Min(want, Carrying.CountThatFits(pawn, p));
                if (fits < want) full = true;
                if (fits <= 0) continue;
                Thing piece = fits >= p.stackCount ? p : p.SplitOff(fits);
                ThingDef def = piece.def;
                int n = piece.stackCount;
                // Told before it goes in: merged into a stack already carried, the piece itself is gone.
                tmpOne.Clear();
                tmpOne.Add(piece);
                Find.QuestManager?.Notify_ThingsProduced(pawn, tmpOne);
                tmpOne.Clear();
                if (!pawn.inventory.innerContainer.TryAdd(piece, true))
                {
                    if (piece != p && !piece.Destroyed) p.TryAbsorbStack(piece, false);
                    continue;
                }
                if (piece == p)
                {
                    products.RemoveAt(i);
                    i--;
                }
                load ??= book.LoadFor(pawn);
                load.Add(def, n);
                if (group != null) load.into[def] = cell;
                pieces++;
                Carrying.Debug($"{pawn.LabelShort} pockets {n}x {def.label} from the bill {bill.LabelCap} ({(group != null ? "for the stockpile the bill picked" : "for the best storage")}).");
            }
            if (load == null || pieces == 0) return;
            HarvestHaul.AddWorkSkills(pawn, load, job.RecipeDef?.workSkill);
            if (full) HarvestHaul.MarkFull(pawn, load, null);
        }

        // ================= What other work makes =================

        private static bool FamilyOn(HaulMath.MadeFamily family)
        {
            if (S == null) return false;
            switch (family)
            {
                case HaulMath.MadeFamily.Buildings: return S.haulBuildings;
                case HaulMath.MadeFamily.Fishing: return S.haulFishing;
                case HaulMath.MadeFamily.OtherWork: return S.haulOtherWork;
                default: return false;
            }
        }

        /// <summary>The job's family: a work job of the colony's carrier, not ordered, not a bill, not one a source of its own covers.</summary>
        private static HaulMath.MadeFamily FamilyOf(Pawn pawn, Job job)
        {
            if (job.playerForced || job.workGiverDef == null || !HarvestHaul.Works || !Carrying.CanCarry(pawn)) return HaulMath.MadeFamily.None;
            Type driver = job.def?.driverClass;
            Thing target = job.targetA.Thing;
            return HaulMath.FamilyOf(true, job.bill != null, HandledElsewhere(job, driver), job.workGiverDef.workType == WorkTypeDefOf.Construction,
                HaulMath.FishingName(driver?.Name, job.def?.defName), target is Building && !(target is Frame));
        }

        /// <summary>A bill, Rimfolk's own delivery or Pick Up And Haul's unloading, or a job one of the sources of their own covers.</summary>
        private static bool HandledElsewhere(Job job, Type driver)
        {
            if (job.def == HaulDefOf.Rimfolk_CarryToStorage || HaulLinks.IsPuahUnload(job)) return true;
            if (driver == null) return false;
            return typeof(JobDriver_DoBill).IsAssignableFrom(driver) || typeof(JobDriver_PlantWork).IsAssignableFrom(driver) || typeof(JobDriver_PlantSow).IsAssignableFrom(driver)
                   || typeof(JobDriver_Mine).IsAssignableFrom(driver) || typeof(JobDriver_OperateDeepDrill).IsAssignableFrom(driver)
                   || typeof(JobDriver_RemoveBuilding).IsAssignableFrom(driver) || typeof(JobDriver_AffectFloor).IsAssignableFrom(driver)
                   || typeof(JobDriver_GatherAnimalBodyResources).IsAssignableFrom(driver) || typeof(JobDriver_Fish).IsAssignableFrom(driver);
        }

        /// <summary>The note for the pawn's present job (made at the first thing it makes), or null when its products aren't taken here.</summary>
        private static Made RecordOf(Pawn pawn)
        {
            Job job = pawn.CurJob;
            if (job == null) return null;
            if (!made.TryGetValue(pawn, out var rec) || rec.job != job)
            {
                rec = new Made { job = job, family = FamilyOf(pawn, job) };
                made[pawn] = rec;
            }
            return rec.family == HaulMath.MadeFamily.None ? null : rec;
        }

        private static void Note(Pawn pawn, Thing thing)
        {
            var rec = RecordOf(pawn);
            if (rec == null || rec.things.Count >= HaulMath.MaxMadePerJob || rec.things.Contains(thing)) return;
            rec.things.Add(thing);
        }

        /// <summary>A thing was made while the actor's job ran (ThingMaker.MakeThing, hooked).</summary>
        public static void NoteMade(Thing thing)
        {
            Pawn pawn = actor;
            if (pawn == null || thing?.def == null || thing.def.category != ThingCategory.Item) return;
            Note(pawn, thing);
        }

        /// <summary>A stack split in two while the actor's job ran: the new part is the old stack's, never something made.</summary>
        public static void Forget(Thing thing)
        {
            Pawn pawn = actor;
            if (pawn == null || thing == null || made.Count == 0) return;
            if (made.TryGetValue(pawn, out var rec)) rec.things.Remove(thing);
        }

        /// <summary>
        /// Something was dropped out of a container while the actor's job ran (GenDrop.TryDropSpawn, hooked). Out of a
        /// building's (an egg box's, a processor's), it was taken out by the work and is noted; out of a pawn's hands or
        /// inventory, a corpse or anything else, it was dropped on purpose and is never taken.
        /// </summary>
        public static void Dropped(IThingHolder from, Thing thing, Thing resulting, bool placed)
        {
            Pawn pawn = actor;
            if (pawn == null || from == null) return;
            Thing holder = from as Thing ?? (from as ThingComp)?.parent;
            if (holder is Building && !(holder is Frame))
            {
                if (placed && resulting != null && resulting.Spawned && resulting.def.category == ThingCategory.Item) Note(pawn, resulting);
                return;
            }
            Forget(thing);
            if (resulting != null && resulting != thing) Forget(resulting);
        }

        /// <summary>Not forbidden, not burning, of a kind that may go in, and not in storage already.</summary>
        private static bool CanTake(Pawn pawn, Thing thing) =>
            !thing.IsForbidden(pawn) && !thing.IsBurning() && PocketableDef(thing.def) && !thing.IsInValidStorage();

        /// <summary>The job carries its thing to a storage cell (one of its targets is a cell of a stockpile or a shelf).</summary>
        private static bool CarriesToStorage(Job job, Map map)
        {
            for (int i = 0; i < 3; i++)
            {
                LocalTargetInfo t = job.GetTarget(i == 0 ? TargetIndex.A : i == 1 ? TargetIndex.B : TargetIndex.C);
                if (!t.HasThing && t.IsValid && t.Cell.InBounds(map) && t.Cell.GetSlotGroup(map) != null) return true;
            }
            return false;
        }

        /// <summary>Moves what the work made into the inventory and owes it to storage; how many went in.</summary>
        private static int Pocket(Pawn pawn, Thing thing, int count, string why)
        {
            var book = HaulBook.Get;
            if (book == null || count <= 0) return 0;
            ThingDef def = thing.def;
            int taken = Carrying.TakeToInventory(pawn, thing, count);
            if (taken <= 0) return 0;
            var load = book.LoadFor(pawn);
            load.Add(def, taken);
            HarvestHaul.AddWorkSkills(pawn, load, null);
            Carrying.Debug($"{pawn.LabelShort} pockets {taken}x {def.label} {why} (free {Carrying.FreeMass(pawn):0.##} kg).");
            return taken;
        }

        /// <summary>
        /// The game's carry step is about to put a thing in the pawn's hands (Toils_Haul.StartCarryThing, hooked). When it
        /// is something this work job made or took out of a building, on its way to a storage cell, and all of it fits:
        /// into the inventory instead, and the job ends there (done). What doesn't fit whole goes the game's way.
        /// </summary>
        public static void AtCarry(Pawn pawn, TargetIndex ind)
        {
            if (pawn == null || made.Count == 0 || !made.TryGetValue(pawn, out var rec) || rec.family == HaulMath.MadeFamily.None) return;
            try
            {
                Job job = pawn.CurJob;
                if (job == null || job != rec.job || !FamilyOn(rec.family) || !HarvestHaul.Works) return;
                Thing thing = job.GetTarget(ind).Thing;
                if (thing == null || !thing.Spawned || thing.Map != pawn.Map || !rec.things.Contains(thing)) return;
                if (pawn.carryTracker?.CarriedThing != null || !CarriesToStorage(job, pawn.Map)) return;
                if (!Carrying.CanCarry(pawn) || !CanTake(pawn, thing)) return;
                int want = Math.Min(thing.stackCount, Allowed(pawn, thing, true));
                if (want < thing.stackCount || Carrying.CountThatFits(pawn, thing) < want) return;
                if (Pocket(pawn, thing, want, "on its way to storage") <= 0) return;
                rec.things.Remove(thing);
                pawn.jobs.curDriver?.EndJobWith(JobCondition.Succeeded);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carrying and hauling, a product on its way to storage", e);
            }
        }

        /// <summary>
        /// The pawn's job is ending (Pawn_JobTracker.CleanupCurrentJob, hooked, before the game drops what the hands hold):
        /// what it made or took out that still lies loose beside the pawn, that some storage takes, goes into the inventory
        /// as far as it fits; the first thing that doesn't fit is carried in the hands on the delivery. Not when the pawn
        /// is drafted, downed or in a mental state, nor for a job that errored.
        /// </summary>
        public static void JobEnding(Pawn pawn, Job job, JobCondition condition)
        {
            if (pawn == null || !made.TryGetValue(pawn, out var rec)) return;
            made.Remove(pawn);
            if (rec.job != job || rec.family == HaulMath.MadeFamily.None || rec.things.Count == 0) return;
            if (condition == JobCondition.Errored || condition == JobCondition.ErroredPather || !FamilyOn(rec.family) || !HarvestHaul.Works) return;
            if (!pawn.Spawned || pawn.Map == null || pawn.Drafted || pawn.Downed || pawn.InMentalState || !Carrying.CanCarry(pawn)) return;
            Map map = pawn.Map;
            Thing leftover = null;
            bool full = false;
            // A part split off as it goes in is no new thing of the job's: nothing is noted meanwhile.
            Pawn wasActor = actor;
            actor = null;
            try
            {
                for (int i = 0; i < rec.things.Count; i++)
                {
                    Thing t = rec.things[i];
                    if (t == null || t.Destroyed || !t.Spawned || t.Map != map || !t.Position.InHorDistOf(pawn.Position, HaulMath.MadeReach)) continue;
                    if (!CanTake(pawn, t) || !pawn.CanReserve(t)) continue;
                    if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, map, StoreUtility.CurrentStoragePriorityOf(t), pawn.Faction, out _, false)) continue;
                    int want = Math.Min(t.stackCount, Allowed(pawn, t, true));
                    int fits = Math.Min(want, Carrying.CountThatFits(pawn, t));
                    if (fits < want)
                    {
                        full = true;
                        leftover ??= t;
                    }
                    if (fits > 0) Pocket(pawn, t, fits, "that its work left");
                }
            }
            finally
            {
                actor = wasActor;
            }
            var load = HaulBook.Get?.LoadOf(pawn);
            if (full && load != null && !load.Empty) HarvestHaul.MarkFull(pawn, load, leftover);
        }

        /// <summary>The note is dropped without taking anything (the deeper hooks were switched off).</summary>
        public static void Drop(Pawn pawn)
        {
            if (pawn != null) made.Remove(pawn);
        }
    }
}
