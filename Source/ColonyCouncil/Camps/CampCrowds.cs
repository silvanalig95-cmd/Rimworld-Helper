using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Camps side by side (the setting "How many camps", CampCrowdMath): the level the player chose, how many camps a home's
    /// map takes at once by its size and open ground, how far apart they keep, and which peoples may camp beside each other.
    /// </summary>
    public static class CampCrowds
    {
        /// <summary>The level the player chose (Usual by default: one camp at a time, as camps always came).</summary>
        public static CampCrowd Level => ColonyCouncilMod.Settings?.campCrowd ?? CampCrowd.Usual;

        /// <summary>Each day's chance at this level, before a remote home's more (TravelLand.CampChance).</summary>
        public static float DailyChance => CampCrowdMath.DailyChance(Level);

        /// <summary>How many camps may visit this map at once: the level's count by the map's size and open ground (worked out once a day).</summary>
        public static int CapAt(Map map)
        {
            if (map == null) return 1;
            return CampCrowdMath.Cap(Level, map.Size.x * map.Size.z, CampSite.OpenShare(map));
        }

        /// <summary>The visiting camps on this map now (coming, settled, leaving: all that still stand on it).</summary>
        public static int HereAt(Map map) => CampTracker.VisitorsAt(map).Count;

        /// <summary>Room on this map for another camp now (by the count only: the site finder still has to find it a spot).</summary>
        public static bool Room(Map map) => map != null && CampCrowdMath.Full(HereAt(map), CapAt(map)) == null;

        /// <summary>Every camp's fire on this map (visiting and settled for good), for keeping a new one apart.</summary>
        public static List<IntVec3> Fires(Map map, Camp except = null)
        {
            var list = new List<IntVec3>();
            var t = CampTracker.Instance;
            if (t == null || map == null) return list;
            foreach (var c in t.All)
                if (c != null && c != except && c.map == map && c.Active && c.center.IsValid) list.Add(c.FireCell);
            return list;
        }

        /// <summary>A spot far enough from every camp on the map and every old site that another might come back to.</summary>
        public static bool Apart(Map map, IntVec3 c, List<IntVec3> fires, IntVec3? except = null)
        {
            if (fires != null)
                foreach (var f in fires)
                    if (!CampCrowdMath.FarEnough(c.x, c.z, f.x, f.z)) return false;
            foreach (var g in CampGrounds.On(map))
                if ((!except.HasValue || g.center != except.Value) && !CampCrowdMath.FarEnough(c.x, c.z, g.center.x, g.center.z)) return false;
            return true;
        }

        /// <summary>
        /// This people may camp beside those camped here now: at war with none of them (two peoples at war never camp on the
        /// same map, unless one came to attack the other: CampRaid.cs).
        /// </summary>
        public static bool AtPeaceHere(Map map, Faction f, Camp except = null)
        {
            if (map == null || f == null) return true;
            var t = CampTracker.Instance;
            if (t == null) return true;
            foreach (var c in t.All)
                if (c != null && c != except && c.map == map && c.Active && c.faction != null && c.faction != f && !CampCrowdMath.MayShare(c.faction.HostileTo(f), false)) return false;
            return true;
        }

        /// <summary>A line for the World page: how many camps this land takes at the level, and how many are here.</summary>
        public static string RoomLine(Map map)
        {
            int cap = CapAt(map), here = HereAt(map);
            string level = ("CC_CampCrowd_" + Level).Translate();
            if (here >= cap) return "CC_CampCrowd_RoomFull".Translate(level.Named("LEVEL"), cap.Named("CAP")).Resolve();
            return "CC_CampCrowd_RoomLine".Translate(level.Named("LEVEL"), cap.Named("CAP"), (cap - here).Named("LEFT")).Resolve();
        }
    }
}
