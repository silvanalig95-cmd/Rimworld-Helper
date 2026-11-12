using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Raids with a reason (Neighbours, What You Sow, Rise and Fall's war, Crossroads' rivals; the story direction over any
    /// storyteller): when the storyteller sends a raid without saying from whom, the way's share of them
    /// (StoryWaysMath.RaidReasons) comes from the hostile people with the most reason to come, the wrongs the colony did
    /// them first (StoryWaysMath.Grievance), if that people could have come anyway. The raid's own letter then names the
    /// old wrong, as it always does when there is one (FactionMemory.RaidNote). With no people that has reason enough,
    /// the game chooses as ever. The number of raids is the storyteller's: this only says who comes.
    /// </summary>
    public static class StoryReasons
    {
        /// <summary>The hostile people with reason to raid this home, by how much reason, that the raid could come from; null for none.</summary>
        public static Faction Pick(Map home, IncidentParms parms, IncidentWorker_RaidEnemy worker)
        {
            var mem = FactionMemory.Instance;
            if (home == null || parms == null) return null;
            var may = Find.FactionManager.AllFactionsListForReading.Where(f => Raids.CanRaid(f, home)).ToList();
            if (may.Count == 0) return null;
            return may.Select(f => (f, why: Reason(mem, f)))
                .Where(x => x.why >= StoryWaysMath.GrievanceFloor && parms.points >= x.f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, null)
                                                                  && (worker == null || worker.FactionCanBeGroupSource(x.f, parms, false)))
                .RandomElementByWeightWithFallback(x => x.why).f;
        }

        /// <summary>How much reason a people has to raid the colony (StoryWaysMath.Grievance), from its memory of the colony and its goodwill.</summary>
        public static float Reason(FactionMemory mem, Faction f)
        {
            if (f == null) return 0f;
            bool remembers = mem != null && FactionMemory.On && FactionMemory.Remembers(f);
            return StoryWaysMath.Grievance(remembers ? mem.Sum(f, DeedSide.Wrong) : 0f, remembers ? mem.Sum(f, DeedSide.TheirWrong) : 0f, f.PlayerGoodwill);
        }
    }

    /// <summary>
    /// The storyteller's raid sent without saying from whom: by the active way's share, it comes from the people with the
    /// most reason (StoryReasons). Not a quest's raid, nor one whose plan or way in was already chosen, nor one on a map
    /// that isn't a home; nothing with the deeper game hooks off.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
    public static class Patch_Story_RaidReasons
    {
        public static void Prefix(IncidentWorker_RaidEnemy __instance, IncidentParms parms)
        {
            if (!RimfolkPatches.On || parms == null || parms.faction != null || parms.quest != null || parms.raidStrategy != null || parms.raidArrivalMode != null) return;
            if (!(parms.target is Map map) || !map.IsPlayerHome || parms.points <= 0f) return;
            try
            {
                float share = StoryDirection.Factor(StoryWaysMath.RaidReasons);
                if (share <= 0f || !Rand.Chance(share)) return;
                var f = StoryReasons.Pick(map, parms, __instance);
                if (f != null) parms.faction = f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ways to play: a raid with a reason", e);
            }
        }
    }
}
