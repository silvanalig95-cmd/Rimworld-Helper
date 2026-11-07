using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Loose things that belong in storage, picked up on a long walk that ends at storage: the walk to storage with a
    /// load, and the walk back to a far stockpile for more building material (they are put in storage there before the
    /// material is taken). At most six cells out of the way, six things, and only what fits; only for a pawn whose
    /// hauling work is on, and only things the game's own haulers would carry. The walk from the stockpile to the build
    /// takes none: they would ride away from storage and take the room the material needs.
    /// </summary>
    public static class OnTheWay
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static readonly List<(Thing thing, float dist)> tmp = new List<(Thing, float)>();
        private static readonly List<Thing> none = new List<Thing>();

        // The last look for a walk to a far stockpile, by pawn and tick: the game asks for the same delivery job many times in one look for work.
        private static Pawn lastPawn;
        private static int lastTick = -1;
        private static IntVec3 lastTo = IntVec3.Invalid;
        private static List<Thing> lastFound = none;

        public static bool On => RimfolkPatches.On && S != null && S.haulOnTheWay;

        /// <summary>The pawn's hauling work is on (the player's work tab is respected for things that aren't its own).</summary>
        public static bool Hauls(Pawn pawn) =>
            pawn.workSettings != null && pawn.workSettings.EverWork && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling);

        /// <summary>Things on the way from the pawn to where its load goes first.</summary>
        public static List<Thing> ForDelivery(Pawn pawn, HaulLoad load)
        {
            if (!On || load == null || !Carrying.CanCarry(pawn) || !Hauls(pawn)) return new List<Thing>();
            int now = Find.TickManager.TicksGame;
            if (now - load.onTheWayFailTick < HaulMath.OnTheWayCooldownTicks) return new List<Thing>();
            IntVec3 to = IntVec3.Invalid;
            foreach (var kv in load.owed)
            {
                Thing t = Carrying.FirstInInventory(pawn, kv.Key);
                if (t != null && StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out IntVec3 cell, false))
                {
                    to = cell;
                    break;
                }
            }
            var found = to.IsValid ? Near(pawn, pawn.Position, to) : new List<Thing>();
            if (found.Count == 0) load.onTheWayFailTick = now;
            return found;
        }

        /// <summary>
        /// Before a long walk to a far stockpile for building material: the things on the way, to be put in storage first,
        /// or none. Asked many times in one look for work, so the answer is kept for the tick.
        /// </summary>
        public static List<Thing> BeforeFetch(Pawn pawn, Thing stack, Thing target)
        {
            if (!On || stack == null || target == null || !stack.Spawned || stack.Map != pawn.Map) return none;
            float walk = pawn.Position.DistanceTo(stack.Position);
            if (!HaulMath.Far(walk, S.materialRunDistance) || !HaulMath.Far(stack.Position.DistanceTo(target.Position), S.materialRunDistance)) return none;
            int now = Find.TickManager.TicksGame;
            if (lastPawn == pawn && lastTick == now && lastTo == stack.Position) return lastFound;
            lastPawn = pawn;
            lastTick = now;
            lastTo = stack.Position;
            lastFound = none;
            var load = HaulBook.Get?.LoadOf(pawn);
            if (load != null && now - load.onTheWayFailTick < HaulMath.OnTheWayCooldownTicks) return none;
            if (!Carrying.CanCarry(pawn) || !Hauls(pawn)) return none;
            var found = Near(pawn, pawn.Position, stack.Position);
            if (found.Count > 0) lastFound = found;
            return lastFound;
        }

        /// <summary>
        /// Loose things near the straight walk from one cell to another, nearest first: no more than six cells out of the
        /// way there and back, six at most, what fits under the fill limit; each one the game's own haulers would take
        /// (not forbidden, not reserved, reachable, not in storage already, with storage that takes it).
        /// </summary>
        public static List<Thing> Near(Pawn pawn, IntVec3 from, IntVec3 to)
        {
            var result = new List<Thing>();
            Map map = pawn.Map;
            float straight = from.DistanceTo(to);
            if (map == null || straight < HaulMath.MinWalk) return result;
            tmp.Clear();
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                if (t == null || !t.Spawned || t.def.category != ThingCategory.Item) continue;
                float a = from.DistanceTo(t.Position);
                if (a > straight + HaulMath.MaxDetour) continue;
                if (!HaulMath.OnTheWay(a, t.Position.DistanceTo(to), straight)) continue;
                tmp.Add((t, a));
            }
            if (tmp.Count == 0) return result;
            tmp.Sort((x, y) => x.dist.CompareTo(y.dist));
            float room = Carrying.FreeMass(pawn);
            for (int i = 0; i < tmp.Count && result.Count < HaulMath.MaxOnTheWay && room > 0f; i++)
            {
                Thing t = tmp[i].thing;
                try
                {
                    if (t is Corpse || t.def == ThingDefOf.MinifiedThing || !t.def.EverStorable(false) || t.IsInValidStorage()) continue;
                    if (!HaulAIUtility.PawnCanAutomaticallyHaul(pawn, t, false)) continue;
                    float each = t.GetStatValue(StatDefOf.Mass);
                    int fits = HaulMath.CountThatFits(t.stackCount, each, room);
                    if (fits <= 0) continue;
                    if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, map, StoreUtility.CurrentStoragePriorityOf(t), pawn.Faction, out _, false)) continue;
                    result.Add(t);
                    room -= fits * each;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("things on the way", e);
                }
            }
            tmp.Clear();
            if (result.Count > 0) Carrying.Debug($"{pawn.LabelShort} picks up {result.Count} thing(s) on the way to {to}.");
            return result;
        }
    }
}
