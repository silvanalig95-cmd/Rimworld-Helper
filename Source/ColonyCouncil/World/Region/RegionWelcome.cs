using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The full welcome of a new town (docs/design/stage1-region.md, item 1; decision 1): a town another mod's builders
    /// finished near a home gets the new-town event's beats (a runner at the gate, a gift hoped for and remembered, timber
    /// wanted, its traders and visitors) on the town that stands, instead of a town made for it
    /// (Defs/WorldSituationDefs/Rimfolk_WS_Region.xml). It never starts by itself: <see cref="RegionFaces"/> starts it
    /// (<see cref="Start"/>) with the town and its founder ({X}) when a world event's slot is free.
    /// </summary>
    public class SituationWorker_NewTownMet : SituationWorker_NewTown
    {
        /// <summary>The town that stands and its founder, given just before it starts; none at any other time.</summary>
        private static Settlement pending;
        private static Pawn pendingFounder;

        public static WorldSituationDef Def => DefDatabase<WorldSituationDef>.GetNamedSilentFail("Rimfolk_WS_NewTownMet");

        /// <summary>Starts the welcome of this town at this home now; null when it can't (no slot free, the town or its people not fit).</summary>
        public static WorldSituation Start(Map home, Settlement town, Pawn founder)
        {
            var def = Def;
            var ws = WorldSituations.Instance;
            if (def == null || ws == null || home == null || town == null || !ws.SlotFree(home)) return null;
            pending = town;
            pendingFounder = founder != null && !founder.Dead && !founder.Discarded ? founder : null;
            try
            {
                return ws.TryStart(home, d => d == def, false);
            }
            finally
            {
                pending = null;
                pendingFounder = null;
            }
        }

        public override bool CanStart(Map map) => pending != null && base.CanStart(map);

        protected override bool CanHappen(Map map)
        {
            var f = pending?.Faction;
            return pending != null && !pending.Destroyed && f != null && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && (!f.HasGoodwill || f.PlayerGoodwill >= -10);
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            var town = pending;
            if (town == null || map == null || !CanHappen(map)) return false;
            s.ctx.map = map;
            s.ctx.faction = town.Faction;
            s.SetNum("town", town.ID);
            s.At(town);
            s.ctx.tale = Where(map, town.Tile);
            s.ctx.stranger = pendingFounder;
            s.ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) s.ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>The town has its faces already (the region gave them when it stood).</summary>
        public override void Begin(WorldSituation s)
        {
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.stranger != null && !s.ctx.stranger.Dead) tags.Add("founder");
        }
    }
}
