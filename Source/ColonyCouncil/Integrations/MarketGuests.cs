using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// With RimMarket and Hospitality, guests always may go to the market's stalls to shop, wherever their guest area
    /// is. Hospitality makes anything outside a guest's area unreachable for them, so a stall outside it never gets
    /// their custom; with the setting "Guests always shop at the market" on, a place at a stall (within a few cells of
    /// one) stays reachable for an arrived guest as it is for anyone, and once they've shopped Hospitality walks them
    /// back into their area as it always does. Nothing else about their area changes. All of it is found by name.
    /// </summary>
    public static class MarketGuests
    {
        public const string HospitalityId = "Orion.Hospitality";

        /// <summary>How far from a stall's own cell its customers stand (its counter, the cells in front to browse from).</summary>
        private const float Reach = 4.5f;

        private static bool looked, ready;
        private static Type stallComp, guestComp;
        private static FieldInfo guestArea;

        public static bool Active => ModsConfig.IsActive(RimMarketStall.PackageId) && ModsConfig.IsActive(HospitalityId);

        public static bool On
        {
            get
            {
                if (!RimfolkPatches.On || !(ColonyCouncilMod.Settings?.marketGuestsShop ?? true)) return false;
                Look();
                return ready;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                if (!Active) return;
                stallComp = AccessTools.TypeByName("RimMarket.Buildings.CompMarketStall");
                guestComp = AccessTools.TypeByName("Hospitality.CompGuest");
                guestArea = guestComp != null ? AccessTools.Field(guestComp, "guestArea_int") : null;
                ready = stallComp != null && guestComp != null && guestArea != null && guestArea.FieldType == typeof(Area);
                if (!ready) Log.Warning("[Rimfolk] RimMarket's stalls or Hospitality's guests aren't the ones Rimfolk knows, so guests keep to their area for the market too.");
            }
            catch (Exception e)
            {
                ready = false;
                CouncilLog.ErrorOnce("market guests", e);
            }
        }

        private static readonly Dictionary<int, (int tick, List<IntVec3> cells)> stalls = new Dictionary<int, (int, List<IntVec3>)>();

        public static void Reset() => stalls.Clear();

        /// <summary>Where the market's stalls stand on this map (looked at again every few seconds).</summary>
        private static List<IntVec3> Stalls(Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (stalls.TryGetValue(map.uniqueID, out var c) && now - c.tick < 250 && now >= c.tick) return c.cells;
            var list = new List<IntVec3>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b is ThingWithComps t && t.AllComps.Any(x => stallComp.IsInstanceOfType(x))) list.Add(b.Position);
            stalls[map.uniqueID] = (now, list);
            return list;
        }

        /// <summary>A place at one of the market's stalls: its counter, or where its customers stand.</summary>
        public static bool AtStall(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid) return false;
            foreach (var s in Stalls(map))
                if (s.InHorDistOf(cell, Reach)) return true;
            return false;
        }

        /// <summary>The guest's own Hospitality record, or null for anyone who isn't one.</summary>
        public static ThingComp GuestOf(Pawn p)
        {
            if (p == null || p.Faction == null || p.Faction.IsPlayer || !(p is ThingWithComps t)) return null;
            foreach (var x in t.AllComps)
                if (guestComp.IsInstanceOfType(x)) return x;
            return null;
        }

        [ThreadStatic] private static bool asking;

        /// <summary>
        /// Hospitality said no: is that only their area? Asked again with their area set aside a moment, so everything
        /// else (walls, locked doors, danger) answers as it always does.
        /// </summary>
        public static bool ReachableWithoutArea(Reachability r, IntVec3 start, LocalTargetInfo dest, PathEndMode mode, TraverseParms parms, ThingComp guest)
        {
            var area = guestArea.GetValue(guest) as Area;
            if (area == null || asking) return false;
            asking = true;
            try
            {
                guestArea.SetValue(guest, null);
                return r.CanReach(start, dest, mode, parms);
            }
            finally
            {
                guestArea.SetValue(guest, area);
                asking = false;
            }
        }
    }

    /// <summary>A guest may reach the market's stalls whatever their Hospitality guest area (MarketGuests).</summary>
    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
    public static class Patch_MarketGuestsReach
    {
        public static bool Prepare() => MarketGuests.Active;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Reachability __instance, ref bool __result, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams)
        {
            if (__result || !MarketGuests.On) return;
            var p = traverseParams.pawn;
            if (p == null || !p.Spawned || !dest.IsValid || !MarketGuests.AtStall(p.Map, dest.Cell)) return;
            try
            {
                var guest = MarketGuests.GuestOf(p);
                if (guest != null && MarketGuests.ReachableWithoutArea(__instance, start, dest, peMode, traverseParams, guest)) __result = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("market guests reach", e);
            }
        }
    }
}
