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
    public partial class Camp
    {
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

        /// <summary>In one of their own stores (a storage building of their people's at their camp).</summary>
        public bool InOwnStore(Thing t)
        {
            if (t == null || !t.Spawned || map == null || t.Map != map || faction == null) return false;
            return t.Position.GetSlotGroup(map)?.parent is Building b && b.Faction == faction && b.Position.InHorDistOf(center, CampSite.CoreRadius + 6);
        }

        /// <summary>In the colony's storage or its home area (a store of theirs is not the colony's).</summary>
        public bool InColonyStore(Thing t)
        {
            if (t == null || !t.Spawned || map == null) return false;
            if (StoreUtility.IsInAnyStorage(t) && !InOwnStore(t)) return true;
            var home = map.areaManager.Home;
            return home != null && home[t.Position];
        }

        /// <summary>A cell of one of their stores that takes this and has room for it (the game's own storage rules, their own people's stores only).</summary>
        public IntVec3 StoreCellFor(Thing t, Pawn carrier, List<Building> stores = null)
        {
            stores ??= Stores();
            foreach (var s in stores)
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
            return IntVec3.Invalid;
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
            var stores = Stores();
            if (stores.Count == 0) return null;
            if (Here.Count(o => o != p && o.CurJobDef == JobDefOf.HaulToCell && o.CurJob.haulMode == HaulMode.ToCellStorage) >= 2) return null;
            foreach (var t in Goods().OrderBy(x => x.Position.DistanceToSquared(p.Position)))
            {
                var job = StowJobFor(p, t, stores);
                if (job != null) return job;
            }
            return null;
        }

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
            foreach (var s in Stores())
            {
                var group = (s as ISlotGroupParent)?.GetSlotGroup();
                if (group == null) continue;
                foreach (var c in group.CellsList)
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
