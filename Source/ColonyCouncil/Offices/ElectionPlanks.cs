using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something a candidate for an office runs on (OfficeDef.planks): a key for their lines (plank:KEY), when it fits
    /// (<see cref="PlankWhen"/>), and the announcement letter's sentence about it ({PAWN} the candidate).
    /// </summary>
    public class OfficePlank
    {
        public string key;
        public PlankWhen when = PlankWhen.Always;
        [MustTranslate] public string text;
    }

    /// <summary>The colony's state as candidates read it, and the planks they run on.</summary>
    public static class ElectionPlanks
    {
        /// <summary>A plank's moment is now, on this map.</summary>
        public static bool Holds(PlankWhen when, Map map, CouncilGameComponent comp)
        {
            if (when == PlankWhen.Always) return true;
            if (map == null) return false;
            try
            {
                switch (when)
                {
                    case PlankWhen.RecentRaid:
                    case PlankWhen.Quiet:
                        int last = map.storyState?.LastThreatBigTick ?? -1;
                        bool recent = last > 0 && Find.TickManager.TicksGame - last < GenDate.TicksPerDay * 15;
                        return when == PlankWhen.RecentRaid ? recent : !recent;
                    case PlankWhen.MealsShort:
                        return Meals(map) < Eaters(map) * 2 || MapFacts.Of(map).FoodDays < 5f;
                    case PlankWhen.FoodPlenty:
                        return MapFacts.Of(map).FoodDays >= 15f;
                    case PlankWhen.Sick:
                        return map.mapPawns.FreeColonistsSpawned.Any(p => !p.Dead && (HealthAIUtility.ShouldSeekMedicalRest(p) || (p.health?.HasHediffsNeedingTend() ?? false)));
                    case PlankWhen.MedicineLow:
                        return MapFacts.Of(map).Medicine < map.mapPawns.FreeColonistsSpawnedCount;
                    case PlankWhen.LowMood:
                        var moods = map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.mood != null).Select(p => p.needs.mood.CurLevelPercentage).ToList();
                        return moods.Count > 0 && moods.Average() < 0.45f;
                    case PlankWhen.LowTrust:
                        var people = map.mapPawns.FreeColonistsSpawned;
                        return comp != null && people.Count > 0 && people.Average(p => comp.standings.TrustOf(p)) < 5f;
                    case PlankWhen.Rivalries:
                        return comp?.social != null && comp.social.ties.Any(t => t.kind == TieKind.Rivals && t.announced);
                    case PlankWhen.Growing:
                    case PlankWhen.Cold:
                        var season = GenLocalDate.Season(map);
                        bool growing = season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer;
                        return when == PlankWhen.Growing ? growing : !growing;
                    case PlankWhen.Herd:
                        return map.mapPawns.SpawnedColonyAnimals.Count >= 3;
                    case PlankWhen.Traditions:
                        return ColonyCalendar.For(map)?.Traditions.Any() ?? false;
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("election plank " + when, e);
            }
            return false;
        }

        private static int Eaters(Map map) => map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;

        /// <summary>Cooked meals in the colony's count.</summary>
        private static int Meals(Map map)
        {
            int n = 0;
            foreach (var kv in map.resourceCounter.AllCountedAmounts)
                if (kv.Value > 0 && kv.Key?.ingestible != null && kv.Key.ingestible.IsMeal && kv.Key.ingestible.HumanEdible && !kv.Key.IsDrug) n += kv.Value;
            return n;
        }

        /// <summary>
        /// A plank for each candidate of a race, the frontrunner first: what is pressing now goes first (planks whose
        /// moment has come before the ones that always fit), and each candidate takes a different one while there are
        /// enough. The office's own planks only; a seat with none has none.
        /// </summary>
        public static List<OfficePlank> Deal(OfficeDef office, int count, Map map, CouncilGameComponent comp)
        {
            var dealt = new List<OfficePlank>();
            var fitting = (office?.planks ?? new List<OfficePlank>()).Where(p => p != null && !p.key.NullOrEmpty() && Holds(p.when, map, comp)).ToList();
            if (fitting.Count == 0) return dealt;
            var pressing = fitting.Where(p => p.when != PlankWhen.Always).InRandomOrder().ToList();
            var always = fitting.Where(p => p.when == PlankWhen.Always).InRandomOrder().ToList();
            var order = pressing.Concat(always).ToList();
            for (int i = 0; i < count; i++) dealt.Add(order[i % order.Count]);
            return dealt;
        }
    }
}
