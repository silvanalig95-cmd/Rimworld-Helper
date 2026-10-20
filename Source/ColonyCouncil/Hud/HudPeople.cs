using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Whom the health and the work panels list: the colony's people (free colonists and slaves, not quest lodgers or
    /// anyone in cryptosleep) at home, everywhere (other maps, caravans, shuttles), or with the colony's prisoners too;
    /// and the colonist bar's order for those panels' second order.
    /// </summary>
    public static class HudPeople
    {
        public static List<Pawn> Who(HudWho who)
        {
            var list = new List<Pawn>();
            void Add(Pawn p)
            {
                if (p == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike || p.IsQuestLodger() || list.Contains(p)) return;
                if (p.ParentHolder is Building_CryptosleepCasket) return;
                list.Add(p);
            }
            if (who == HudWho.Home)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome) continue;
                    foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList()) Add(p);
                    foreach (var p in map.mapPawns.SlavesOfColonySpawned.ToList()) Add(p);
                }
                return list;
            }
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep.ToList()) Add(p);
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_SlavesOfColony.ToList()) Add(p);
            if (who == HudWho.WithPrisoners)
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony.ToList())
                    Add(p);
            return list;
        }

        /// <summary>"in a caravan", "away" or null when they're on a map.</summary>
        public static string Away(Pawn p)
        {
            if (p == null || p.Spawned || p.CarriedBy != null) return null;
            try
            {
                if (p.GetCaravan() != null) return HudKit.T("CC_Pnl_InCaravan");
            }
            catch (Exception)
            {
            }
            return HudKit.T("CC_Pnl_Away");
        }

        /// <summary>Selects them and moves the camera there (their caravan on the world map when they're travelling).</summary>
        public static void Go(Pawn p)
        {
            if (p == null) return;
            try
            {
                CameraJumper.TryJumpAndSelect(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels: jump", e);
            }
        }
    }
}
