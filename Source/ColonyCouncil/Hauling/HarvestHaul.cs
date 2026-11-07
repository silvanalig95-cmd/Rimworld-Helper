using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harvest and haul, folded in from the player's own Harvest and Haul (1.2.0): pawns put what their own work drops
    /// into their inventory while they work (harvesting, chopping and cutting plants, also to clear a cell for sowing or
    /// building; mining; deep drilling; shearing and milking; deconstructing and removing floors; fishing), keep at the
    /// same kind of work while there is room, and take it to storage themselves: when something no longer fits (and the
    /// load is worth the trip), when they turn to other work, eat, sleep, relax or idle, or after the hours the setting
    /// allows. What else a deconstruction leaves is hauled by the same pawn right after. Delivering is part of the work,
    /// so a pawn who can't haul still brings in what it produced. Drafting, mental states, fights, emergencies, medical
    /// rest, urgent needs, rituals and caravans and anything the player ordered come first. While the standalone mod is
    /// loaded all of this stands down (its hooks aren't applied), and Rimfolk's book holds no harvest of its own.
    /// </summary>
    public static class HarvestHaul
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static readonly List<Thing> tmpCandidates = new List<Thing>();

        /// <summary>
        /// Collecting is on: the hooks are on, the setting is, the standalone mod isn't loaded, and the choice of the next
        /// job is hooked (without it pawns would hoard, so collecting is off then).
        /// </summary>
        public static bool Works => RimfolkPatches.On && S != null && S.haulOwnWork && !HaulLinks.Standalone && ChoiceHooked;

        public static bool ChoiceHooked => RimfolkPatches.Applied.Contains(nameof(Patch_HaulChoosing)) && RimfolkPatches.Applied.Contains(nameof(Patch_HaulNextJob));

        /// <summary>"These kinds were just dropped round here by this pawn's work; pick them up."</summary>
        public sealed class Request
        {
            public Pawn pawn;
            public Map map;
            public CellRect area;
            /// <summary>The product, when there is one kind.</summary>
            public ThingDef def;
            /// <summary>The products, when there are several.</summary>
            public HashSet<ThingDef> defs;
            /// <summary>Anything a deep drill can bring up.</summary>
            public bool anyDeepResource;
            /// <summary>The skill of the work, when the pawn's job doesn't say which work type it is.</summary>
            public SkillDef fallbackSkill;
            /// <summary>For work that keeps producing (deep drilling, fishing): this job ends once the pawn is full.</summary>
            public Job jobToEndWhenFull;

            public bool Matches(ThingDef thingDef)
            {
                if (thingDef == null) return false;
                if (thingDef != def && (defs == null || !defs.Contains(thingDef)) && !(anyDeepResource && thingDef.deepCommonality > 0f)) return false;
                return Collectable(thingDef);
            }
        }

        public static bool Collectable(ThingDef def)
        {
            if (def.category != ThingCategory.Item || !def.EverStorable(false)) return false;
            if (def.IsCorpse || def == ThingDefOf.MinifiedThing) return false;
            return S.haulChunks || !IsChunk(def);
        }

        public static bool IsChunk(ThingDef def)
        {
            var c = def.thingCategories;
            return c != null && (c.Contains(ThingCategoryDefOf.Chunks) || c.Contains(ThingCategoryDefOf.StoneChunks));
        }

        private static float MinTrip => S?.haulMinTrip ?? 5f;
        public static bool WorthATrip(HaulLoad load) => HaulMath.WorthATrip(load.Mass, MinTrip);

        // ================= What work dropped (from the hooks) =================

        /// <summary>A plant was harvested or cut, for any reason (harvesting, chopping, clearing a cell for sowing or building).</summary>
        public static void PlantCollected(Pawn pawn, Plant plant)
        {
            ThingDef def = plant.def?.plant?.harvestedThingDef;
            if (!S.haulPlants || def == null) return;
            Submit(new Request { pawn = pawn, map = pawn.Map, area = CellRect.CenteredOn(pawn.Position, HaulMath.SearchMargin), def = def, fallbackSkill = SkillDefOf.Plants });
        }

        /// <summary>A rock, an ore or another mineable was mined out.</summary>
        public static void Mined(Pawn pawn, Map map, CellRect rect, ThingDef product)
        {
            if (!S.haulMining || product == null || pawn.Map != map) return;
            Submit(new Request { pawn = pawn, map = map, area = rect.ExpandedBy(HaulMath.SearchMargin), def = product, fallbackSkill = SkillDefOf.Mining });
        }

        /// <summary>A deep drill brought up a portion.</summary>
        public static void DeepDrillPortion(Pawn driller, Thing drill)
        {
            if (!S.haulDeepDrill || drill == null || !drill.Spawned || driller.Map != drill.Map) return;
            Submit(new Request
            {
                pawn = driller, map = drill.Map, area = drill.OccupiedRect().ExpandedBy(HaulMath.SearchMargin + 1), anyDeepResource = true,
                fallbackSkill = SkillDefOf.Mining, jobToEndWhenFull = driller.CurJob
            });
        }

        /// <summary>Wool, milk or another body resource was gathered from an animal.</summary>
        public static void Gathered(Pawn doer, ThingDef product)
        {
            if (!S.haulAnimals || product == null) return;
            Submit(new Request { pawn = doer, map = doer.Map, area = CellRect.CenteredOn(doer.Position, HaulMath.SearchMargin), def = product, fallbackSkill = SkillDefOf.Animals });
        }

        /// <summary>A building or floor was taken apart and gave back some of its materials.</summary>
        public static void Deconstructed(Pawn pawn, Map map, CellRect rect, HashSet<ThingDef> refunds)
        {
            if (!S.haulDeconstruct || refunds == null || refunds.Count == 0 || pawn.Map != map) return;
            Submit(new Request { pawn = pawn, map = map, area = rect.ExpandedBy(HaulMath.SearchMargin), defs = refunds, fallbackSkill = SkillDefOf.Construction });
        }

        /// <summary>The ids of the things lying round a building before it is taken apart (to tell its leftovers after).</summary>
        public static HashSet<int> SnapshotLeftovers(Map map, CellRect rect)
        {
            if (!S.haulLeftoversNow) return null;
            var ids = new HashSet<int>();
            foreach (IntVec3 c in rect.ExpandedBy(HaulMath.SearchMargin).ClipInsideMap(map))
            {
                var things = map.thingGrid.ThingsListAtFast(c);
                for (int i = 0; i < things.Count; i++)
                    if (things[i].def.category == ThingCategory.Item)
                        ids.Add(things[i].thingIDNumber);
            }
            return ids;
        }

        /// <summary>
        /// What a deconstruction left lying round that didn't go into the inventory (chunks, fuel, its contents) is hauled
        /// by the same pawn right after its job.
        /// </summary>
        public static void DeconstructionLeftovers(Pawn pawn, Map map, CellRect rect, HashSet<int> before)
        {
            if (!Works || !S.haulLeftoversNow || !Carrying.CanCarry(pawn) || pawn.Map != map) return;
            var book = HaulBook.Get;
            if (book == null) return;
            HaulLoad load = null;
            foreach (IntVec3 c in rect.ExpandedBy(HaulMath.SearchMargin).ClipInsideMap(map))
            {
                var things = map.thingGrid.ThingsListAtFast(c);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t.def.category != ThingCategory.Item || !t.def.EverHaulable || before.Contains(t.thingIDNumber)) continue;
                    if (t.IsForbidden(pawn) || t.IsInValidStorage()) continue;
                    load ??= book.LoadFor(pawn);
                    if (!load.haulNow.Contains(t)) load.haulNow.Add(t);
                }
            }
            if (load != null)
            {
                load.haulNowTick = Find.TickManager.TicksGame;
                Carrying.Debug($"{pawn.LabelShort} will haul {load.haulNow.Count} thing(s) the deconstruction left, right away.");
            }
        }

        /// <summary>Fish were caught (Odyssey). They are placed after this, so only the next tick's look finds them.</summary>
        public static void FishCaught(Pawn pawn, IntVec3 spot, List<Thing> catches)
        {
            if (!S.haulFishing || catches == null || catches.Count == 0) return;
            var defs = new HashSet<ThingDef>();
            for (int i = 0; i < catches.Count; i++)
                if (catches[i]?.def != null)
                    defs.Add(catches[i].def);
            CellRect area = CellRect.CenteredOn(pawn.Position, HaulMath.SearchMargin);
            if (spot.IsValid) area = area.Encapsulate(CellRect.CenteredOn(spot, 1));
            Submit(new Request { pawn = pawn, map = pawn.Map, area = area, defs = defs, fallbackSkill = SkillDefOf.Animals, jobToEndWhenFull = pawn.CurJob }, false);
        }

        private static void Submit(Request r, bool collectNow = true)
        {
            if (!Works || !Carrying.CanCarry(r.pawn) || r.map == null) return;
            var book = HaulBook.Get;
            if (book == null) return;
            if (collectNow) Collect(r);
            book.QueueSweep(r);
        }

        /// <summary>A tick after the drop: what turned up late, and the end of work that keeps producing once the pawn is full.</summary>
        public static void Sweep(Request r)
        {
            Pawn pawn = r.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map != r.map) return;
            Collect(r);
            Job job = r.jobToEndWhenFull;
            if (job == null || pawn.CurJob != job) return;
            var load = HaulBook.Get?.LoadOf(pawn);
            if (load != null && load.full && WorthATrip(load))
            {
                Carrying.Debug($"{pawn.LabelShort} is full, stopping {job.def.defName} to deliver.");
                pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        // ================= Collecting =================

        /// <summary>Moves the matching loose things in the area into the pawn's inventory, nearest first, as far as it has room.</summary>
        public static void Collect(Request r)
        {
            Pawn pawn = r.pawn;
            Map map = r.map;
            if (!Works || !Carrying.CanCarry(pawn) || pawn.Map != map) return;
            var book = HaulBook.Get;
            if (book == null) return;
            tmpCandidates.Clear();
            foreach (IntVec3 c in r.area.ClipInsideMap(map))
            {
                var things = map.thingGrid.ThingsListAtFast(c);
                for (int j = 0; j < things.Count; j++)
                    if (r.Matches(things[j].def) && CanCollect(pawn, things[j]))
                        tmpCandidates.Add(things[j]);
            }
            if (tmpCandidates.Count == 0) return;
            IntVec3 at = pawn.Position;
            tmpCandidates.SortBy(t => t.Position.DistanceToSquared(at));

            HaulLoad load = book.LoadOf(pawn);
            int picked = 0;
            float smallest = float.MaxValue;
            Thing leftover = null;
            for (int i = 0; i < tmpCandidates.Count; i++)
            {
                Thing t = tmpCandidates[i];
                if (t.Destroyed || !t.Spawned) continue;
                float each = t.GetStatValue(StatDefOf.Mass);
                int count = Carrying.CountThatFits(pawn, t);
                // Whatever stays behind of the first stack that doesn't fit whole.
                if (count < t.stackCount && leftover == null) leftover = t;
                if (count <= 0) continue;
                ThingDef def = t.def;
                int taken = Carrying.TakeToInventory(pawn, t, count, false);
                if (taken <= 0) continue;
                load ??= book.LoadFor(pawn);
                load.Add(def, taken);
                picked += taken;
                smallest = Math.Min(smallest, each);
                Carrying.Debug($"{pawn.LabelShort} collected {taken}x {def.label} (free {Carrying.FreeMass(pawn):0.##} kg).");
            }
            tmpCandidates.Clear();
            if (load == null || load.Empty) return;
            if (picked > 0) AddWorkSkills(pawn, load, r.fallbackSkill);
            // Full once something didn't fit, or there's no room left for one more.
            if (leftover != null || (picked > 0 && Carrying.FreeMass(pawn) < smallest)) MarkFull(pawn, load, leftover);
        }

        private static bool CanCollect(Pawn pawn, Thing t)
        {
            if (!t.Spawned || t.Destroyed || t.stackCount <= 0) return false;
            if (t.IsForbidden(pawn) || t.IsBurning()) return false;
            if (t.IsInValidStorage()) return false;
            // Someone else is already on the way to haul it.
            return pawn.CanReserve(t);
        }

        private static void AddWorkSkills(Pawn pawn, HaulLoad load, SkillDef fallback)
        {
            var skills = pawn.CurJob?.workGiverDef?.workType?.relevantSkills;
            if (skills != null && skills.Count > 0)
                for (int i = 0; i < skills.Count; i++)
                    load.AddSkill(skills[i]);
            else load.AddSkill(fallback);
        }

        private static void MarkFull(Pawn pawn, HaulLoad load, Thing leftover)
        {
            if (leftover != null) load.leftover = leftover;
            if (!load.full) Carrying.Debug($"{pawn.LabelShort} is full ({load.Mass:0.#} kg collected).");
            load.full = true;
            if (!WorthATrip(load)) return;
            // Finish the plant in hand, then end the job by dropping the plants still queued: it succeeds the game's way.
            if (pawn.jobs?.curDriver is JobDriver_PlantWork) pawn.CurJob?.targetQueueA?.Clear();
        }

        // ================= When to deliver =================

        /// <summary>
        /// The pawn has just chosen its next job (its last one ended). The job becomes a trip to storage first when the
        /// load should go now; the things a deconstruction left go first of all.
        /// </summary>
        public static void TryReplaceNextJob(Pawn pawn, ref ThinkResult result)
        {
            var load = HaulBook.Get?.LoadOf(pawn);
            if (load == null) return;
            if (TryHaulLeftBehind(pawn, load, ref result)) return;
            if (load.Empty || pawn.inventory == null) return;
            load.Reconcile(pawn);
            if (load.Empty) return;
            if (!ShouldDeliverInsteadOf(pawn, load, result)) return;
            Job job = Carrying.DeliveryJob(pawn, load);
            if (job == null) return;
            load.lastDeliveryJobTick = Find.TickManager.TicksGame;
            Carrying.Debug($"{pawn.LabelShort} takes its load to storage instead of {(result.IsValid ? result.Job.def.defName : "idling")}.");
            result = new ThinkResult(job, result.IsValid ? result.SourceNode : null, JobTag.MiscWork, false);
        }

        /// <summary>The next thing the pawn's work left behind, hauled the game's way, before anything that can wait.</summary>
        private static bool TryHaulLeftBehind(Pawn pawn, HaulLoad load, ref ThinkResult result)
        {
            var list = load.haulNow;
            if (list.Count == 0) return false;
            if (Find.TickManager.TicksGame - load.haulNowTick > HaulMath.HaulNowWindowTicks || pawn.Map == null)
            {
                list.Clear();
                return false;
            }
            if (!Carrying.CanDeliverNow(pawn, load)) return false;
            if (result.IsValid && (result.FromQueue || result.Job.playerForced || !Postponable(result.SourceNode))) return false;
            while (list.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < list.Count; i++)
                    if (list[i] != null && (list[best] == null || list[i].PositionHeld.DistanceToSquared(pawn.Position) < list[best].PositionHeld.DistanceToSquared(pawn.Position)))
                        best = i;
                Thing thing = list[best];
                list.RemoveAt(best);
                if (thing == null || !thing.Spawned || thing.Map != pawn.Map || thing.IsForbidden(pawn) || thing.IsInValidStorage()) continue;
                if (!thing.Position.InHorDistOf(pawn.Position, HaulMath.HaulNowMaxDistance) || !HaulAIUtility.PawnCanAutomaticallyHaul(pawn, thing, false)) continue;
                Job haul = HaulAIUtility.HaulToStorageJob(pawn, thing, false);
                if (haul == null) continue; // no storage takes it; it waits for the haulers
                load.lastDeliveryJobTick = Find.TickManager.TicksGame;
                Carrying.Debug($"{pawn.LabelShort} hauls {thing.LabelCap} right away instead of {(result.IsValid ? result.Job.def.defName : "idling")}.");
                result = new ThinkResult(haul, result.IsValid ? result.SourceNode : null, JobTag.MiscWork, false);
                return true;
            }
            return false;
        }

        private static bool ShouldDeliverInsteadOf(Pawn pawn, HaulLoad load, ThinkResult next)
        {
            if (!Carrying.CanDeliverNow(pawn, load) || Carrying.DeliveryUnderWay(pawn)) return false;
            if (!next.IsValid) return true;
            Job job = next.Job;
            // Never in place of what the player ordered.
            if (next.FromQueue || job.playerForced) return false;
            if (job.def == HaulDefOf.Rimfolk_CarryToStorage || HaulLinks.IsPuahUnload(job)) return false;
            bool held = HaulMath.HoldExpired(load.startedTick, Find.TickManager.TicksGame, S?.haulMaxHours ?? 8f);
            // Still on a material run and building on: what must go to storage waits for the run's end, one trip back.
            if (Carrying.StillBuilding(pawn, load, job)) return (load.full && WorthATrip(load)) || held;
            // Keep at the same kind of work while there is room; deliver when full (and it's worth it), held too long, or told to go now.
            if (!load.now && SameKindOfWork(job, load)) return (load.full && WorthATrip(load)) || held;
            return Postponable(next.SourceNode);
        }

        /// <summary>
        /// The next job uses a skill of the work that made the load: harvesting then sowing, one cell mined after another,
        /// one cow milked after another, deconstructing then building. Mods that split the work types are covered too.
        /// </summary>
        public static bool SameKindOfWork(Job job, HaulLoad load)
        {
            var skills = job.workGiverDef?.workType?.relevantSkills;
            if (skills != null)
                for (int i = 0; i < skills.Count; i++)
                    if (load.skills.Contains(skills[i]))
                        return true;
            // Jobs not given by a work giver.
            Type driver = job.def.driverClass;
            if (driver == null) return false;
            if (load.skills.Contains(SkillDefOf.Plants) && (typeof(JobDriver_PlantWork).IsAssignableFrom(driver) || typeof(JobDriver_PlantSow).IsAssignableFrom(driver))) return true;
            if (load.skills.Contains(SkillDefOf.Mining) && (typeof(JobDriver_Mine).IsAssignableFrom(driver) || typeof(JobDriver_OperateDeepDrill).IsAssignableFrom(driver))) return true;
            return false;
        }

        /// <summary>
        /// Jobs that may wait for the delivery: ordinary work, idling, and with the setting eating, sleeping, recreation and
        /// meditation. Anything else (emergencies, medical care, fleeing, rituals, other mods' jobs) is left alone.
        /// </summary>
        private static bool Postponable(ThinkNode node)
        {
            switch (node)
            {
                case null:
                    return false;
                case JobGiver_Work work:
                    return !work.emergency;
                case JobGiver_Wander _:
                case JobGiver_Idle _:
                case JobGiver_SelfShutdown _:
                    return true;
            }
            if (S == null || !S.haulBeforeNeeds) return false;
            return node is JobGiver_GetFood || node is JobGiver_GetRest || node is JobGiver_GetJoy || node is JobGiver_Meditate;
        }
    }
}
