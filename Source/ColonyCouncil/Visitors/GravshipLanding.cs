using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's gravship has come down (Odyssey): when that's a few tiles from a friendly settlement on the same
    /// layer, the landing is noted (VisitorsMore2Tracker) and the settlement's gawkers are put in the game's own
    /// incident queue for a few hours later (Rimfolk_Evt_GravshipGawkers, which still checks everything when it comes).
    /// </summary>
    public static class GravshipLanding
    {
        /// <summary>How far a settlement's folk walk out to see a gravship.</summary>
        public const float Tiles = 8f;

        public const string EventName = "Rimfolk_Evt_GravshipGawkers";

        /// <summary>The nearest settlement of a people at peace with the colony within <see cref="Tiles"/> tiles, on the map's own layer.</summary>
        public static Settlement NearSettlement(Map map)
        {
            if (map == null || !map.Tile.Valid) return null;
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s?.Faction == null || s.Faction.IsPlayer || !Visitors.Usable(s.Faction) || !s.Tile.Valid || s.Tile.Layer != map.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile);
                if (d <= Tiles && d < bestD)
                {
                    best = s;
                    bestD = d;
                }
            }
            return best;
        }

        public static void Landed(Map map)
        {
            if (!ModsConfig.OdysseyActive || map == null || !map.IsPlayerHome || MapFacts.InVacuum(map) || NearSettlement(map) == null) return;
            var tr = VisitorsMore2Tracker.Instance;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(EventName);
            if (tr == null || def == null) return;
            int now = Find.TickManager.TicksGame;
            tr.landedTick = now;
            tr.landedMap = map.uniqueID;
            if (!(ColonyCouncilMod.Settings?.worldEvents ?? false)) return;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            Find.Storyteller.incidentQueue.Add(def, now + Rand.RangeInclusive(4, 10) * GenDate.TicksPerHour, parms, GenDate.TicksPerDay * 3 / 2);
        }
    }

    /// <summary>The game tells the scenario a gravship has landed (Scenario.PostGravshipLanded): Rimfolk notes it for the gawkers.</summary>
    [HarmonyPatch(typeof(Scenario), "PostGravshipLanded")]
    public static class Patch_GravshipLanded
    {
        public static void Postfix(Map map)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                GravshipLanding.Landed(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a gravship's landing", e);
            }
        }
    }
}
