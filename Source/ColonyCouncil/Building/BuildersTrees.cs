using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Builders leave the trees in the way to the plant cutters (BuildersTreesMath): where the game would hand a builder a
    /// cut-plant job of their own for a tree on their blueprint or frame (GenConstruct.HandleBlockingThingJob), the tree is
    /// marked for cutting instead (the game's own mark, so it shows) and the builder goes on to another build. They cut it
    /// themselves when nothing else is free to build, when nobody of the home has plant cutting on, when the player ordered
    /// the build, or once it has waited marked half a day. Nothing saved: when each tree was marked is kept for the session.
    /// </summary>
    public static class BuildersTrees
    {
        private static readonly Dictionary<int, int> markedAt = new Dictionary<int, int>();
        private static readonly Dictionary<int, (int tick, bool free, bool cutter)> mapState = new Dictionary<int, (int, bool, bool)>();

        public static bool On => RimfolkPatches.On;

        /// <summary>True when the builder leaves <paramref name="plant"/> to the cutters (and it is marked for them).</summary>
        public static bool LeaveToCutters(Thing constructible, Pawn worker, Plant plant, bool forced)
        {
            var map = worker?.Map;
            if (map == null || plant == null || !plant.Spawned || plant.def?.plant == null) return false;
            if (!(constructible is Blueprint_Build) && !(constructible is Frame)) return false;
            int now = Find.TickManager.TicksGame;
            var (free, cutter) = State(map, now);
            var dm = map.designationManager;
            bool markedNow = dm.DesignationOn(plant, DesignationDefOf.CutPlant) != null || dm.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null;
            int since = markedAt.TryGetValue(plant.thingIDNumber, out int t) ? t : now;
            if (!BuildersTreesMath.Leave(forced, plant.def.plant.IsTree, free, cutter, now - since)) return false;
            if (!markedNow) dm.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
            if (!markedAt.ContainsKey(plant.thingIDNumber)) markedAt[plant.thingIDNumber] = now;
            if (markedAt.Count > 400) Prune(now);
            return true;
        }

        /// <summary>Whether any build of the colony's on the map is free of anything in its way, and whether anyone cuts plants: looked at most every 250 ticks.</summary>
        private static (bool free, bool cutter) State(Map map, int now)
        {
            if (mapState.TryGetValue(map.uniqueID, out var s) && now - s.tick < 250) return (s.free, s.cutter);
            bool free = false;
            foreach (var group in new[] { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame })
            {
                foreach (var c in map.listerThings.ThingsInGroup(group))
                {
                    if (c?.Faction != Faction.OfPlayer || c.IsForbidden(Faction.OfPlayer) || !(c is Blueprint_Build || c is Frame)) continue;
                    if (GenConstruct.FirstBlockingThing(c, null) == null)
                    {
                        free = true;
                        break;
                    }
                }
                if (free) break;
            }
            bool cutter = map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)
                                                                     && p.workSettings.WorkIsActive(WorkTypeDefOf.PlantCutting));
            mapState[map.uniqueID] = (now, free, cutter);
            return (free, cutter);
        }

        private static void Prune(int now)
        {
            foreach (var id in markedAt.Where(kv => now - kv.Value > BuildersTreesMath.WaitTicks * 4).Select(kv => kv.Key).ToList()) markedAt.Remove(id);
        }
    }

    /// <summary>A tree in the way of a build: marked for the plant cutters, and the builder goes on to another build (BuildersTrees).</summary>
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob))]
    public static class Patch_BuildersLeaveTrees
    {
        private static bool broken;

        public static void Postfix(Thing constructible, Pawn worker, bool forced, ref Job __result)
        {
            if (broken || __result == null || forced || __result.def != JobDefOf.CutPlant || !BuildersTrees.On) return;
            try
            {
                if (BuildersTrees.LeaveToCutters(constructible, worker, __result.targetA.Thing as Plant, forced)) __result = null;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("builders leaving trees to the plant cutters", e);
            }
        }
    }
}
