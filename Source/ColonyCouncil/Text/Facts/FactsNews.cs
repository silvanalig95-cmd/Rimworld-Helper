using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The provider of the world's news near a people's town (a letter's news when asked for some).
    public static partial class Facts
    {
        /// <summary>
        /// Real things near a people's town, as they'd tell of them, into the facts' news: world events in the region or
        /// on them, places near the town, a quest of theirs, their wars.
        /// </summary>
        private static void NewsNear(FactSet facts, Faction f, Settlement town)
        {
            var items = facts.news;
            if (f == null) return;
            try
            {
                foreach (var s in WorldSituations.Instance?.Running ?? Enumerable.Empty<WorldSituation>())
                    if (s.ctx?.faction == null || s.ctx.faction == f || s.ctx.enemy == f)
                        items.Add((s.ctx?.placeName).NullOrEmpty() ? "CC_News_Situation".Translate(s.Label.Named("WHAT")).Resolve()
                            : "CC_News_SituationAt".Translate(s.Label.Named("WHAT"), s.ctx.placeName.Named("PLACE")).Resolve());
                if (town != null)
                    foreach (var site in Find.WorldObjects.Sites.Where(x => x != null && x.Tile.Layer == town.Tile.Layer).OrderBy(x => Find.WorldGrid.ApproxDistanceInTiles(x.Tile, town.Tile)).Take(2))
                        if (Find.WorldGrid.ApproxDistanceInTiles(site.Tile, town.Tile) <= 15f)
                            items.Add("CC_News_Site".Translate(site.LabelCap.Named("SITE"), NeighbourLetters.Direction(town.Tile, site.Tile).Named("DIR"), town.Name.Named("TOWN")).Resolve());
                foreach (var q in Find.QuestManager.QuestsListForReading)
                {
                    if (q == null || q.hidden || q.State != QuestState.NotYetAccepted || q.name.NullOrEmpty()) continue;
                    if (!q.InvolvedFactions.Contains(f)) continue;
                    items.Add("CC_News_Quest".Translate(q.name.Named("QUEST")).Resolve());
                    break;
                }
                foreach (var g in Find.FactionManager.AllFactionsListForReading)
                {
                    if (g == null || g == f || g.IsPlayer || g.Hidden || g.defeated || !g.def.humanlikeFaction || !f.HostileTo(g) || g.def.permanentEnemy) continue;
                    items.Add("CC_News_War".Translate(g.Name.Named("FACTION")).Resolve());
                    break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter news", e);
            }
        }
    }
}
