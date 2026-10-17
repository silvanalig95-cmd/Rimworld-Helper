using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the world beyond the walls feeds the heart's feelings, for the feelings' workers to add (FeelingWorkers.cs):
    /// friends made at a camp (fellowship), the region's troubles for those they touch most (worry), and good news from
    /// the region (hope). Each reads only the colonist and what the camp and the world events already keep, and answers
    /// nothing while its feature is off.
    /// </summary>
    public static class WorldFeeds
    {
        /// <summary>A camp settled on their map, friendly, that they have got to know (its warmth with them).</summary>
        public static void CampFriends(Pawn p, IFeelingSink sink)
        {
            if (p == null || !p.Spawned || !CampTracker.Enabled) return;
            try
            {
                var camp = CampTracker.Instance?.camp;
                if (camp == null || camp.map != p.Map || !camp.Settled || camp.Hostile || camp.warmth == null) return;
                if (!camp.warmth.TryGetValue(p, out int w) || w < 3) return;
                sink.Add(Mathf.Min(4f, 1f + 0.5f * w), "CC_Heart_Cause_CampFriends".Translate(camp.faction?.Name ?? "?"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart: friends at the camp", e);
            }
        }

        /// <summary>
        /// A sickness in the region, for a parent of one of the colony's children and for its healers; hunger or drought
        /// in the region, for those who grow its food.
        /// </summary>
        public static void RegionTroubles(Pawn p, IFeelingSink sink)
        {
            var world = Running();
            if (world == null || p == null) return;
            try
            {
                bool sickness = false, hunger = false;
                foreach (var s in world.Running)
                {
                    if (s?.def?.kind == null) continue;
                    if (s.def.kind == "sickness") sickness = true;
                    else if (s.def.kind == "hunger" || s.def.kind == "drought") hunger = true;
                }
                if (sickness)
                {
                    if (ParentHere(p)) sink.Add(2.5f, "CC_Heart_Cause_SicknessChildren".Translate());
                    else if (Passion(p, SkillDefOf.Medicine)) sink.Add(2f, "CC_Heart_Cause_SicknessHealer".Translate());
                }
                if (hunger && Passion(p, SkillDefOf.Plants)) sink.Add(2f, "CC_Heart_Cause_HungerGrower".Translate());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart: the region's troubles", e);
            }
        }

        /// <summary>Good news from the region: a world event that is neither bad news nor a fight (a fair, a holy year, a new road).</summary>
        public static void GoodNews(Pawn p, IFeelingSink sink)
        {
            var world = Running();
            if (world == null || p == null) return;
            try
            {
                foreach (var s in world.Running)
                {
                    if (s?.def == null || s.def.bad || s.def.battle) continue;
                    sink.Add(1.5f, "CC_Heart_Cause_GoodNews".Translate(s.def.label ?? "?"));
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart: good news", e);
            }
        }

        private static WorldSituations Running() =>
            ColonyCouncilMod.Settings != null && ColonyCouncilMod.Settings.worldEvents ? WorldSituations.Instance : null;

        private static bool Passion(Pawn p, SkillDef skill)
        {
            var r = p.skills?.GetSkill(skill);
            return r != null && !r.TotallyDisabled && People.Passionate(r);
        }

        /// <summary>A parent of a child of the colony who is at home with them.</summary>
        private static bool ParentHere(Pawn p)
        {
            if (p.relations == null || p.MapHeld == null) return false;
            foreach (var r in p.relations.DirectRelations)
            {
                var c = r.otherPawn;
                if (r.def != PawnRelationDefOf.Child || c == null || c.Dead || c.Faction != Faction.OfPlayer || c.MapHeld != p.MapHeld) continue;
                if (c.DevelopmentalStage.Juvenile() || c.DevelopmentalStage.Baby()) return true;
            }
            return false;
        }
    }
}
