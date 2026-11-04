using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A struggling town given up, as a world event on that town (docs/design/stage1-region.md, item 2, "Abandonment";
    /// Defs/WorldSituationDefs/Rimfolk_WS_RiseFall.xml): the town-abandoned event's beats (a family of theirs at the gate,
    /// what the town leaves behind) on the town whose fortune failed, told by what struck it ({PLACE}; {ITEM} the town its
    /// families go to; flags sickness, hunger, winter, flood, fire, strife or hard, and nowhere when they have no other
    /// town to go to). It never starts by itself: the region starts it (<see cref="Start"/>) when a world event's slot is
    /// free at the home nearest the town, else the town is given up quietly (World/Region/RiseFall.cs). At its end the
    /// town is a ruin, unless the colony has business there (a caravan on its way, a quest): then a few families hold on.
    /// </summary>
    public class SituationWorker_GivenUp : SituationWorker_Abandoned
    {
        private static Settlement pending;

        public static WorldSituationDef Def => DefDatabase<WorldSituationDef>.GetNamedSilentFail("Rimfolk_WS_GivenUp");

        /// <summary>Starts the giving up of this town at this home now; null when it can't (no slot free, the town or its people not fit).</summary>
        public static WorldSituation Start(Map home, Settlement town)
        {
            var def = Def;
            var ws = WorldSituations.Instance;
            if (def == null || ws == null || home == null || town == null || !ws.SlotFree(home)) return null;
            pending = town;
            try
            {
                return ws.TryStart(home, d => d == def, false);
            }
            finally
            {
                pending = null;
            }
        }

        public override bool CanStart(Map map) => pending != null && base.CanStart(map);

        protected override bool CanHappen(Map map)
        {
            var f = pending?.Faction;
            return pending != null && !pending.Destroyed && pending.HasName && f != null && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && Safe(pending);
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            var town = pending;
            if (town == null || map == null || !CanHappen(map)) return false;
            s.ctx.map = map;
            s.ctx.faction = town.Faction;
            s.At(town);
            s.ctx.tale = SituationWorker_NewTown.Where(map, town.Tile);
            var next = RiseFall.NextTown(town);
            s.ctx.item = next?.Name ?? town.Faction.Name;
            if (next == null) s.flags.Add("nowhere");
            s.SetNum("town", town.ID);
            s.flags.Add(Cause(town.Faction));
            s.ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) s.ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>
        /// What struck the town, as its texts are tagged: the latest bad world event of its people's that cost their towns'
        /// fortunes (sickness, hunger, winter, flood, fire, strife), else hard (years of hard luck).
        /// </summary>
        public static string Cause(Faction f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || f == null) return "hard";
            WorldSituationDef worst = null;
            int at = int.MinValue;
            foreach (var s in ws.Running)
                if (s?.def != null && s.def.prosperity < 0f && s.ctx?.faction == f && s.startTick > at)
                {
                    worst = s.def;
                    at = s.startTick;
                }
            foreach (var r in ws.history)
            {
                if (r == null || r.faction != f || r.startTick <= at) continue;
                var d = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "");
                if (d == null || d.prosperity >= 0f) continue;
                worst = d;
                at = r.startTick;
            }
            switch (worst?.kind)
            {
                case "sickness": return "sickness";
                case "hunger":
                case "drought": return "hunger";
                case "weather": return "winter";
                case "water": return "flood";
                case "fire": return "fire";
                case "creed":
                case "feud":
                case "roads":
                case "court": return "strife";
                default: return "hard";
            }
        }

        private static Settlement TownOf(WorldSituation s)
        {
            int id = s.Num("town");
            return id == 0 ? null : NotableBook.TownOf(id);
        }

        /// <summary>The end: the town a ruin (the ledger keeps it, the history has it; the event's own letter tells it), or a few families holding on.</summary>
        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            var town = TownOf(s);
            if (town == null || town.Destroyed) return;
            if (ending == null || ending.key == "holdouts" || !Safe(town) || !RiseFallMath.KeepsEnough(Find.WorldObjects.Settlements.Count(x => x.Faction == town.Faction && !x.Destroyed)))
            {
                RiseFall.HeldOn(town, false);
                return;
            }
            RiseFall.MakeRuin(town, WorldLedger.TownRecord(town.ID), false);
        }
    }
}
