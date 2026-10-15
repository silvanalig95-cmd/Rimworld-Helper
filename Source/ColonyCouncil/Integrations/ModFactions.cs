using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Which peoples a world event written for a faction mod (or Royalty's Empire) is about, by FactionDef name: the
    /// Empire, Vanilla Factions Expanded's republics, its settlers. The event's own needs (friendly, a town nearby)
    /// still apply. Names that aren't in the game never match, so without the mod the event can't start.
    /// </summary>
    public class ModFactionsExtension : DefModExtension
    {
        /// <summary>The people it's about ({FACTION}): one of these.</summary>
        public List<string> factions;
        /// <summary>The second people ({ENEMY}), when there is one: one of these.</summary>
        public List<string> second;
    }

    /// <summary>
    /// A world event about a particular people (see <see cref="ModFactionsExtension"/>). Its lines can be tagged
    /// faction:DefName and enemy:DefName for the people it picked, so one event can speak of the Empire's high stewards
    /// and a republic's senators in their own words.
    /// </summary>
    public class WorldSituationWorker_ModFaction : WorldSituationWorker
    {
        private ModFactionsExtension Ext => def.GetModExtension<ModFactionsExtension>();

        private static IEnumerable<Faction> Named(IEnumerable<Faction> from, List<string> names) =>
            names.NullOrEmpty() ? from : from.Where(f => f?.def != null && names.Contains(f.def.defName));

        protected override bool CanHappen(Map map)
        {
            var ext = Ext;
            if (ext == null) return true;
            if (def.faction != null && !Named(Candidates(def.faction, null, map), ext.factions).Any()) return false;
            return true;
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            var ext = Ext;
            if (ext == null) return base.Cast(s, map);
            var ctx = s.ctx;
            ctx.map = map;
            if (def.faction != null)
            {
                ctx.faction = Nearest(Named(Candidates(def.faction, null, map), ext.factions), map);
                if (ctx.faction == null) return false;
                ctx.placeName = NearestTown(ctx.faction, map)?.Name ?? ctx.faction.Name;
            }
            if (def.second != null)
            {
                ctx.enemy = Nearest(Named(Candidates(def.second, ctx.faction, map), ext.second), map);
                if (ctx.enemy == null && !def.second.optional) return false;
            }
            var colonists = IncidentWorker_Happening.Colonists(map);
            if (def.colonist != null)
            {
                ctx.a = colonists.Where(p => def.colonist.Allows(p, null))
                    .OrderByDescending(p => def.colonist.bestAt != null ? p.skills?.GetSkill(def.colonist.bestAt)?.Level ?? 0 : Rand.Value).FirstOrDefault();
                if (ctx.a == null) return false;
            }
            else ctx.a = colonists.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>Nearer peoples more often.</summary>
        private static Faction Nearest(IEnumerable<Faction> options, Map map) =>
            options.RandomElementByWeightWithFallback(f => Mathf.Lerp(1.5f, 0.3f, Mathf.Clamp01(Distance(map, NearestTown(f, map)) / 40f)));

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.faction?.def != null) tags.Add("faction:" + s.ctx.faction.def.defName);
            if (s.ctx.enemy?.def != null) tags.Add("enemy:" + s.ctx.enemy.def.defName);
            if (ModsConfig.RoyaltyActive)
            {
                // A colonist who holds the Empire's title: the Empire speaks to the colony differently.
                var titled = s.ctx.map?.mapPawns?.FreeColonists?.Any(p => p.royalty?.MostSeniorTitle != null) ?? false;
                if (titled) tags.Add("titled");
            }
        }
    }
}
