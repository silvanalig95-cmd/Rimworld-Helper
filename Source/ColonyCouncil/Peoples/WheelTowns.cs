using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The Wheel's towns move (docs/design/peoples.md, "Release D"): a town on wheels and runners never stays long. On the
    /// region's day (World/Region/RegionBook.cs), each town of a people of the Wheel's ways comes up on its own day of
    /// every 30 (PeoplesDMath.MoveDay, by its id) and moves on two to five tiles, to land a town can stand on, clear of the
    /// game's nearness rule round the colony's homes and of other peoples' land (Arising.TryTownTile, as a new people's
    /// town is placed). Never while the colony has a map of it, has a caravan or a shuttle bound for it, or a quest about
    /// it is open. The colony hears of it in the news, with the town on the world map. Nothing is saved: the day is the
    /// town's own, read from its id.
    /// </summary>
    public static class WheelTowns
    {
        public static void Daily(int now)
        {
            int day = now / GenDate.TicksPerDay;
            var towns = Find.WorldObjects?.Settlements?.Where(s => s != null && !s.Destroyed && Peoples.CallingOf(s.Faction) == PeoplesMath.Salvagers && !s.Faction.defeated).ToList();
            if (towns == null) return;
            foreach (var s in towns)
            {
                if (!PeoplesDMath.MoveDay(day, s.ID) || !MayMove(s)) continue;
                Move(s);
            }
        }

        /// <summary>A town may move: no map of it, no caravan of the colony's bound for it or on it, no open quest about it.</summary>
        public static bool MayMove(Settlement s)
        {
            if (s == null || s.HasMap || !s.Tile.Valid || s.Tile.Layer == null || !s.Tile.Layer.IsRootSurface) return false;
            if (Find.WorldObjects.Caravans.Any(c => c != null && c.IsPlayerControlled && (c.Tile == s.Tile || c.pather?.Destination == s.Tile))) return false;
            try
            {
                if (Find.QuestManager.QuestsListForReading.Any(q => q != null && q.State == QuestState.Ongoing && q.QuestLookTargets.Any(t => t.WorldObject == s))) return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>The town moves on, when land a few tiles off will take it; the news with the town on the world map.</summary>
        public static bool Move(Settlement s)
        {
            try
            {
                var from = s.Tile;
                if (!Arising.TryTownTile(from, PeoplesDMath.MoveMin, PeoplesDMath.MoveMax, new HashSet<Faction> { s.Faction }, null, out var to) || to == from) return false;
                s.Tile = to;
                RegionTowns.Refresh(true);
                Tell.Message(NewsKind.World, Importance.Flavor, null, "CC_PeoplesD_WheelMoved".Translate(s.Label, s.Faction.Name).Resolve(), new LookTargets(s), MessageTypeDefOf.NeutralEvent);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: a Wheel town moving on", e);
                return false;
            }
        }
    }
}
