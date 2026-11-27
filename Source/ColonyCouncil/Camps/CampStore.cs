using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Their goods into their own storage (CampDayMath.Stows): what lies loose at their camp is carried onto their shelf, into
    // their basket or onto their firewood rack by one of them, the game's own hauling into storage, and kept there. Only
    // their own goods, only into their own stores: the game lets a hauler store only in storage of its own people, so the
    // colony's stockpiles are never theirs and their shelf is never the colony's.
    // Kept out of the weather (21 November 2026, the player: "this camp that's currently set up has all their stuff just out
    // in the open instead of on a shelf or somewhere where it doesn't deteriorate"): the shelf they put up is one the game
    // keeps things on without deteriorating (CampKit.Keeps); a people who know no such shelf (a tribe) put up a store tent of
    // their tent walls and door, roofed once it stands, and the game keeps what lies in a closed room under a roof; what they
    // brought to build with and didn't use goes back in their packs once their camp stands (CampDayMath.Repacks).
    public partial class Camp
    {
        /// <summary>The roles of their store tent's walls and door (CampBuildMath.StoreTent).</summary>
        public static bool StoreTentRole(string role) => role == "storewall" || role == "storedoor";

        /// <summary>The stacks they brought to build with and set down at their stash, by thing id: back in their packs once nothing needs them.</summary>
        public List<int> brought = new List<int>();

        [Unsaved] private List<IntVec3> tentCells;
        [Unsaved] private int tentCellsTick = -1;

        private void ExposeStore()
        {
            Scribe_Collections.Look(ref brought, "brought", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) brought ??= new List<int>();
        }

        /// <summary>They set this down at their stash from their packs, to build with.</summary>
        private void Brought(Thing t)
        {
            if (t == null) return;
            Own(t);
            if (!brought.Contains(t.thingIDNumber)) brought.Add(t.thingIDNumber);
        }

        /// <summary>Their store tent's walls and door, as planned (none for a camp with a shelf).</summary>
        private List<CampPlan> StoreTentPlans() => plans.Where(p => StoreTentRole(p.role) && !p.dropped && p.def != null).ToList();

        /// <summary>The floor of their store tent, when its walls and door all stand: what its roof covers (none otherwise).</summary>
        public List<IntVec3> StoreTentInside()
        {
            var tent = StoreTentPlans();
            if (tent.Count < 8 || map == null || !stash.IsValid || tent.Any(p => p.Built(map, faction) == null)) return new List<IntVec3>();
            return CampBuildMath.StoreInside(tent.SelectMany(p => p.Rect.Cells).Select(c => (c.x, c.z)), (stash.x, stash.z))
                .Select(c => new IntVec3(c.x, 0, c.z)).Where(c => c.InBounds(map)).ToList();
        }

        /// <summary>The store tent gets its roof once its walls and door stand (never over the mountain's own).</summary>
        private void RoofStoreTent()
        {
            foreach (var c in StoreTentInside())
                if (!c.Roofed(map)) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            tentCells = null;
        }

        /// <summary>The store tent's roof taken off, before its first wall or its door is taken down.</summary>
        private void UnroofStoreTent()
        {
            foreach (var c in StoreTentInside())
                if (map.roofGrid.RoofAt(c) == RoofDefOf.RoofConstructed) map.roofGrid.SetRoof(c, null);
            tentCells = null;
        }

        /// <summary>The floor of their store tent where it stands under its roof: where their goods keep (looked at again every few seconds).</summary>
        private List<IntVec3> TentCells()
        {
            if (tentCells != null && Now - tentCellsTick < 250) return tentCells;
            tentCellsTick = Now;
            tentCells = StoreTentInside().Where(c => c.Roofed(map)).ToList();
            return tentCells;
        }

        /// <summary>A cell of their store tent's floor that takes this: empty, or a stack of the same it can join.</summary>
        private IntVec3 TentCellFor(Thing t, Pawn carrier)
        {
            foreach (var c in TentCells().OrderBy(c => carrier != null ? c.DistanceToSquared(carrier.Position) : 0))
            {
                if (!c.Standable(map)) continue;
                if (carrier != null && !carrier.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                var here = c.GetThingList(map).Where(x => x.def.category == ThingCategory.Item).ToList();
                if (here.Count == 0) return c;
                if (here.Count == 1 && here[0] != t && here[0].CanStackWith(t) && here[0].stackCount < here[0].def.stackLimit && Owns(here[0])) return c;
            }
            return IntVec3.Invalid;
        }
        /// <summary>The roles of their buildings that store goods: the stash's shelf, and what they add over the stay (CampGrow.cs).</summary>
        public static readonly string[] StoreRoles = { "stash", "store", "basket", "woodpile" };

        /// <summary>Their stores standing on the map: buildings of theirs that hold goods.</summary>
        public List<Building> Stores()
        {
            var list = new List<Building>();
            if (map == null || faction == null) return list;
            foreach (var p in plans)
            {
                if (p.dropped || p.def == null || !StoreRoles.Contains(p.role)) continue;
                var b = p.Built(map, faction);
                if (b is ISlotGroupParent && b.Spawned && !list.Contains(b)) list.Add(b);
            }
            return list;
        }

        /// <summary>In one of their own stores (a storage building of their people's at their camp, or their store tent).</summary>
        public bool InOwnStore(Thing t)
        {
            if (t == null || !t.Spawned || map == null || t.Map != map || faction == null) return false;
            if (t.Position.GetSlotGroup(map)?.parent is Building b && b.Faction == faction && b.Position.InHorDistOf(center, CampSite.CoreRadius + 6)) return true;
            return TentCells().Contains(t.Position);
        }

        /// <summary>In the colony's storage or its home area (a store of theirs is not the colony's).</summary>
        public bool InColonyStore(Thing t)
        {
            if (t == null || !t.Spawned || map == null) return false;
            if (StoreUtility.IsInAnyStorage(t) && !(t.Position.GetSlotGroup(map)?.parent is Building b && b.Faction == faction)) return true;
            var home = map.areaManager.Home;
            return home != null && home[t.Position];
        }

        /// <summary>
        /// A cell of one of their stores that takes this and has room for it (the game's own storage rules, their own people's
        /// stores only): a shelf that keeps the weather off first, then a store of their own for it (a firewood rack, a food
        /// basket), then the floor of their store tent.
        /// </summary>
        public IntVec3 StoreCellFor(Thing t, Pawn carrier, List<Building> stores = null)
        {
            stores ??= Stores();
            foreach (var s in stores.OrderBy(b => CampKit.Keeps(b.def) ? 0 : 1))
            {
                var group = (s as ISlotGroupParent)?.GetSlotGroup();
                if (group == null) continue;
                try
                {
                    if (StoreUtility.TryFindBestBetterStoreCellForIn(t, carrier, map, StoragePriority.Unstored, faction, group, out var cell)) return cell;
                }
                catch (Exception e)
                {
                    CouncilLog.WarnOnce("camp store cell for " + t.def.defName + ": " + e.Message);
                }
            }
            return TentCells().Count > 0 && !(t is Corpse) ? TentCellFor(t, carrier) : IntVec3.Invalid;
        }

        /// <summary>The facts about one of their goods, for whether it goes into their storage (CampDayMath.Stows); storeTakes is filled by the caller.</summary>
        private StowFacts StowFactsOf(Thing t, Pawn carrier) => new StowFacts
        {
            theirs = Owns(t), atCamp = t.Spawned && t.Map == map && t.Position.InHorDistOf(center, CampSite.CoreRadius + 6), corpse = t is Corpse, putOut = IsProp(t) || IsWare(t),
            inStore = InOwnStore(t), inColony = InColonyStore(t),
            busy = carrier != null && (!carrier.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some) || Here.Any(o => o != carrier && o.CurJob != null && o.CurJob.targetA.Thing == t))
        };

        /// <summary>
        /// One of their goods lying loose at their camp carried into their storage: the nearest first, at most two of them at it
        /// at once. The game's own hauling into storage, without picking up anything else on the way (nothing of the colony's
        /// is ever in their hands).
        /// </summary>
        public Job StowJob(Pawn p)
        {
            if (p == null || p.WorkTagIsDisabled(WorkTags.Hauling) || p.DevelopmentalStage.Juvenile() || packingFor != null) return null;
            // Nothing found to put away a little while ago: not looked for again until a quarter of an hour has passed.
            if (Now < nextStowSearch) return null;
            var stores = Stores();
            if (stores.Count == 0 && TentCells().Count == 0) return null;
            if (Here.Count(o => o != p && o.CurJobDef == JobDefOf.HaulToCell) >= 2) return null;
            foreach (var t in Goods().OrderBy(x => x.Position.DistanceToSquared(p.Position)))
            {
                var job = StowJobFor(p, t, stores);
                if (job != null) return job;
            }
            nextStowSearch = Now + GenDate.TicksPerHour / 4;
            return null;
        }

        [Unsaved] private int nextStowSearch = -1;

        /// <summary>This one of their goods carried into their storage, if it goes there and a store of theirs has room; else null.</summary>
        public Job StowJobFor(Pawn p, Thing t, List<Building> stores = null)
        {
            if (t == null || p == null || p.WorkTagIsDisabled(WorkTags.Hauling)) return null;
            var facts = StowFactsOf(t, p);
            facts.storeTakes = true;
            if (!CampDayMath.Stows(facts)) return null;
            var cell = StoreCellFor(t, p, stores);
            if (!cell.IsValid) return null;
            try
            {
                if (TentCells().Contains(cell))
                {
                    // The floor of their store tent: carried in and set down, as the game sets a thing down on any cell.
                    var into = JobMaker.MakeJob(JobDefOf.HaulToCell, t, cell);
                    into.haulMode = HaulMode.ToCellNonStorage;
                    into.haulOpportunisticDuplicates = false;
                    var there = cell.GetFirstItem(map);
                    int room = there == null ? t.def.stackLimit : there.def.stackLimit - there.stackCount;
                    into.count = CampDayMath.StowCount(t.stackCount, room, p.carryTracker?.MaxStackSpaceEver(t.def) ?? t.stackCount);
                    return into;
                }
                var job = HaulAIUtility.HaulToCellStorageJob(p, t, cell, false);
                // Only this stack: never a stack of the same lying nearby, which may be the colony's.
                job.haulOpportunisticDuplicates = false;
                job.count = CampDayMath.StowCount(t.stackCount, job.count, p.carryTracker?.MaxStackSpaceEver(t.def) ?? t.stackCount);
                return job;
            }
            catch (Exception e)
            {
                CouncilLog.WarnOnce("camp goods stowed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// What lies in their stores is theirs: a part of a stack carried in on its own (the game makes a new stack of a part)
        /// is taken into their goods, forbidden to the colony like the rest. Only what the colony forbade too (their goods
        /// always are); nothing else ever lands on their shelf (the colony can't store in a people's storage that isn't its own).
        /// </summary>
        private void KeepStores()
        {
            if (map == null || owned == null) return;
            var cells = new List<IntVec3>(TentCells());
            foreach (var s in Stores())
            {
                var group = (s as ISlotGroupParent)?.GetSlotGroup();
                if (group != null) cells.AddRange(group.CellsList);
            }
            foreach (var c in cells)
            {
                if (!c.InBounds(map)) continue;
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    var t = things[i];
                    if (t.def.category != ThingCategory.Item || Owns(t) || !t.IsForbidden(Faction.OfPlayer)) continue;
                    Own(t);
                }
            }
        }

        // ---------------- What they brought to build with, back in their packs ----------------

        /// <summary>The facts about a stack they brought, for whether it goes back in their packs now (CampDayMath.Repacks).</summary>
        private RepackFacts RepackFactsOf(Thing t, Pawn packer, HashSet<ThingDef> needed) => new RepackFacts
        {
            brought = t != null && brought.Contains(t.thingIDNumber), theirs = Owns(t),
            atCamp = t != null && t.Spawned && t.Map == map && t.Position.InHorDistOf(center, CampSite.CoreRadius + 6),
            putOut = t != null && (IsProp(t) || IsWare(t)), inColony = InColonyStore(t),
            busy = packer != null && t != null && (!packer.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some) || Here.Any(o => o != packer && o.CurJob != null && o.CurJob.targetA.Thing == t)),
            settled = phase == CampPhase.Living && packingFor == null,
            underWay = growing != null || Unfinished().Count > 0,
            needed = t != null && needed != null && needed.Contains(t.def),
            inStore = t != null && InOwnStore(t),
            storeTakes = t != null && StoreCellFor(t, packer, Stores()).IsValid
        };

        /// <summary>
        /// One of them puts a stack they brought to build with and didn't use back in their pack: once their camp stands and
        /// nothing of theirs is under way, one stack at a time, by a grown hand who isn't their trader (whose pack is their stock).
        /// </summary>
        public Job RepackJob(Pawn p)
        {
            if (p == null || brought.Count == 0 || p == trader || p.inventory == null || p.DevelopmentalStage.Juvenile() || p.WorkTagIsDisabled(WorkTags.Hauling)) return null;
            if (phase != CampPhase.Living || packingFor != null || Now < nextRepackSearch) return null;
            if (Here.Any(o => o != p && o.CurJobDef == CampDayDefOf.Rimfolk_CampRepack)) return null;
            var needed = new HashSet<ThingDef>(BuildNeeds().Select(n => n.def));
            foreach (var t in Goods().Where(x => brought.Contains(x.thingIDNumber)).OrderBy(x => x.Position.DistanceToSquared(p.Position)))
                if (CampDayMath.Repacks(RepackFactsOf(t, p, needed))) return JobMaker.MakeJob(CampDayDefOf.Rimfolk_CampRepack, t);
            nextRepackSearch = Now + GenDate.TicksPerHour / 2;
            return null;
        }

        [Unsaved] private int nextRepackSearch = -1;

        /// <summary>The stack is in their pack: no longer lying at their camp, not for sale, theirs to take home.</summary>
        public void Repacked(Pawn p, Thing t)
        {
            if (p?.inventory == null || t == null || t.Destroyed || !t.Spawned || !Owns(t)) return;
            brought.Remove(t.thingIDNumber);
            Disown(t);
            t.DeSpawn();
            p.inventory.TryAddItemNotForSale(t);
            if (!t.Destroyed && !p.inventory.innerContainer.Contains(t) && !t.Spawned)
            {
                // No room in their pack: back at the stash, theirs as before.
                if (GenPlace.TryPlaceThing(t, stash, map, ThingPlaceMode.Near, out var back, null, AtStash) && back != null) Own(back);
            }
        }

        /// <summary>What they brought is tracked while it lies at their camp: gone into a building, eaten or carried off, it's no longer counted.</summary>
        private void KeepBrought()
        {
            if (brought.Count == 0) return;
            brought.RemoveAll(id => !owned.Contains(id));
        }

        /// <summary>How many of their goods lie about with nowhere to go (no store of theirs takes them or has room), and whether any is food: for a second store (CampGrow.cs).</summary>
        public (int count, bool food) LooseWithoutRoom()
        {
            int n = 0;
            bool food = false;
            var stores = Stores();
            foreach (var t in Goods())
            {
                var f = StowFactsOf(t, null);
                f.storeTakes = true;
                if (!CampDayMath.Stows(f)) continue;
                if (stores.Count > 0 && StoreCellFor(t, null, stores).IsValid) continue;
                n++;
                if (t.def.IsNutritionGivingIngestible && !(t is Corpse)) food = true;
            }
            return (n, food);
        }
    }
}
