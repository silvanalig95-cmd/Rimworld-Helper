using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using RimWorld.Planet;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's report (7 November 2026): "why do 4-5 people visitor groups stay in my colony if i only have one
    /// reserved visitor bed?" Hospitality sizes a guest group from its own settings and the years played
    /// (IncidentWorker_VisitorGroup.GetGroupSize), never from the guest beds; its only bed rule is to refuse guests until
    /// there is a guest bed at all. With the setting "Guest groups fit your guest beds" (on), a group of its random size
    /// is cut to the free guest bed places on the map, now and then with one or two more ("Extra guests a group may bring",
    /// 2 by default; GuestGroupBedsMath.Size and Extra), as the player asked: "i don't mind if sometimes one or two too
    /// many visitors arrive, but not like four". The ones left out walk past instead ("otherwise they should just walk by
    /// basically"): they come in where the group would have, cross the map like the game's own travellers and leave by
    /// the far side, with one message saying so (no letter). With every guest bed taken and no extra this time, the whole
    /// group walks past, and Hospitality's own spawn finds nobody left to send. A size asked for outright (a quest's, a
    /// debug action's) is left alone.
    /// </summary>
    public static class GuestGroupBeds
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.guestGroupsFitBeds ?? true) && People.ModActive(Guests.Mod);

        /// <summary>The guest beds' places on the map, and those nobody has claimed.</summary>
        public static (int places, int free) Places(Map map)
        {
            int places = 0, free = 0;
            if (map?.listerBuildings == null) return (0, 0);
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_Bed bed) || !GuestBeds.IsGuestBed(bed) || bed.Destroyed) continue;
                int slots = bed.SleepingSlotsCount;
                places += slots;
                int owned = bed.OwnersForReading?.Count(o => o != null && !o.Dead) ?? 0;
                free += Math.Max(0, slots - owned);
            }
            return (places, free);
        }

        /// <summary>
        /// The guests left out walk past: they come in where the group would have and cross the map to its far side, as
        /// the game's own travellers do (IncidentWorker_TravelerGroup), with one message. Only pawns waiting in the world
        /// (the group's own, not yet sent); never throws on one that can't come.
        /// </summary>
        public static void WalkPast(List<Pawn> passers, IncidentParms parms, Map map)
        {
            var faction = parms?.faction;
            if (passers == null || passers.Count == 0 || map == null || faction == null || faction == Faction.OfPlayer || faction.HostileTo(Faction.OfPlayer)) return;
            if (!parms.spawnCenter.IsValid || !parms.spawnCenter.InBounds(map)) return;
            if (!RCellFinder.TryFindTravelDestFrom(parms.spawnCenter, map, out var dest)) return;
            var walking = new List<Pawn>();
            foreach (var p in passers)
            {
                if (p == null || p.Dead || p.Destroyed || p.Spawned || p.RaceProps?.IsMechanoid == true || p.Faction != faction) continue;
                if (Find.WorldPawns.GetSituation(p) != WorldPawnSituation.Free) continue;
                try
                {
                    GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(parms.spawnCenter, map, 5), map);
                    walking.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a guest walking past", e);
                    if (p.Spawned)
                    {
                        p.DeSpawn();
                        p.DestroyOrPassToWorld();
                    }
                }
            }
            if (walking.Count == 0) return;
            LordMaker.MakeNewLord(faction, new LordJob_TravelAndExit(dest), map, walking);
            string text = walking.Count == 1
                ? "CC_GuestsWalkPastOne".Translate(walking[0].Named("PAWN"), faction.Name.Named("FACTION")).Resolve()
                : "CC_GuestsWalkPast".Translate(walking.Count, faction.Name.Named("FACTION")).Resolve();
            Notify.Message(text, new LookTargets(walking[0]), MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }
    }

    /// <summary>Hospitality's guests to send, cut to the free guest beds (Integrations/GuestGroupBeds.cs).</summary>
    [HarmonyPatch]
    public static class Patch_GuestGroupFitsBeds
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() =>
            GenTypes.GetTypeInAnyAssembly("Hospitality.IncidentWorker_VisitorGroup") is Type t ? AccessTools.Method(t, "GetPawnsToSpawn", new[] { typeof(IncidentParms) }) : null;

        public static void Postfix(IncidentParms __0, ref IEnumerable<Pawn> __result)
        {
            if (!GuestGroupBeds.On || __0 == null || __0.pawnCount > 0 || !(__0.target is Map map) || __result == null) return;
            try
            {
                var group = __result.ToList();
                var (places, free) = GuestGroupBeds.Places(map);
                int extra = GuestGroupBedsMath.Extra(Rand.Value, ColonyCouncilMod.Settings?.guestBedsExtra ?? 2);
                int size = GuestGroupBedsMath.Size(group.Count, places, free, extra);
                if (size >= group.Count)
                {
                    __result = group;
                    return;
                }
                if (Prefs.DevMode)
                    Log.Message("[Rimfolk] Hospitality's guest group of " + group.Count + " cut to " + size + " (" + free + " of " + places + " guest bed places free, "
                                + extra + " more allowed this time); the rest walk past.");
                __result = group.Take(size).ToList();
                GuestGroupBeds.WalkPast(group.Skip(size).ToList(), __0, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest group fitting the guest beds", e);
            }
        }
    }
}
