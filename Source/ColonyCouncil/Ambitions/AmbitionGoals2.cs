using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A seat at a table indoors for everyone who eats (see <see cref="WishWorker_SeatsForAll"/>).</summary>
    public class Goal_SeatsForAll : AmbitionGoal
    {
        public override bool Possible(Map map) =>
            WishWorker_SeatsForAll.Diners(map) >= 2 && WishWorker_SeatsForAll.Seats(map) < WishWorker_SeatsForAll.Diners(map)
            && Avail.AnyBuildable(map, Avail.IsChair) && (WishWorker_DiningTable.HasDiningSpot(map) || Avail.AnyBuildable(map, Avail.IsEatingTable));

        public override float Progress(Map map, float baseline) => Ratio(WishWorker_SeatsForAll.Seats(map), WishWorker_SeatsForAll.Diners(map));

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_SeatsForAll".Translate(WishWorker_SeatsForAll.Seats(map), WishWorker_SeatsForAll.Diners(map));
    }

    /// <summary>Everyone sleeps in a real bed (one that took something to build), not a sleeping spot or the floor.</summary>
    public class Goal_BedsForAll : AmbitionGoal
    {
        /// <summary>A bed that took wood or cloth to make: not a sleeping spot marked on the ground.</summary>
        public static bool RealBed(ThingDef d) => d != null && d.IsBed && (d.MadeFromStuff || !d.CostList.NullOrEmpty());

        private static int Count(Map map, out int total)
        {
            var sleepers = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby() && p.needs?.rest != null && !p.IsQuestLodger()).ToList();
            total = sleepers.Count;
            return sleepers.Count(p => p.ownership?.OwnedBed is Building_Bed b && RealBed(b.def));
        }

        public override bool Possible(Map map)
        {
            int have = Count(map, out int total);
            return total > 0 && have < total && Avail.BestBuildable(map, Avail.IsHumanBed, "Bed") != null;
        }

        public override float Progress(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return total == 0 ? 0f : Ratio(have, total);
        }

        public override string Describe(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return "CC_Goal_BedsForAll".Translate(have, total);
        }
    }

    /// <summary>
    /// So many days in a row without anyone in the colony breaking down (a mental break of any kind; the heart's short
    /// episodes don't count). The baseline is when the streak began; a break moves it to now.
    /// </summary>
    public class Goal_CalmDays : AmbitionGoal
    {
        public float days = 15f;
        public int minColonists = 3;
        /// <summary>Only pitched when it's needed: someone is close to breaking, or the colony is worn down.</summary>
        public bool whenStrained = true;
        /// <summary>Only once the colony has lasted this many days.</summary>
        public float minColonyDays;

        public static bool AnyBreak(Map map)
        {
            var list = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.InMentalState && p.MentalStateDef != Episodes.Def && !p.IsQuestLodger()) return true;
            }
            return false;
        }

        private static bool Strained(Map map)
        {
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.mood != null && !p.IsQuestLodger()).ToList();
            if (people.Any(p => p.mindState?.mentalBreaker != null && p.needs.mood.CurLevel < p.mindState.mentalBreaker.BreakThresholdMinor + 0.08f)) return true;
            return Heart.On && Heart.ColonyStrain(people) >= 40f;
        }

        public override bool Possible(Map map) =>
            RoomUtil.ColonistCount(map) >= minColonists && Find.TickManager.TicksGame >= minColonyDays * GenDate.TicksPerDay && !AnyBreak(map)
            && (!whenStrained || Strained(map));

        public override float Baseline(Map map) => Find.TickManager.TicksGame;

        public override float Update(Map map, float baseline) => AnyBreak(map) ? Find.TickManager.TicksGame : baseline;

        private float Days(float baseline) => Mathf.Max(0f, (Find.TickManager.TicksGame - baseline) / GenDate.TicksPerDay);

        public override float Progress(Map map, float baseline) => Ratio(Days(baseline), days);

        public override string Describe(Map map, float baseline) => "CC_Goal_CalmDays".Translate(days.ToString("0"), Days(baseline).ToString("0.#"));
    }

    /// <summary>So many days in a row with nobody in the colony sick (an ailment, or one of the game's illnesses).</summary>
    public class Goal_HealthyDays : AmbitionGoal
    {
        public float days = 15f;

        public static int Sick(Map map) => map.mapPawns.FreeColonistsSpawned.Count(p => !p.IsQuestLodger() && WishWorker_Sickbed.Ailing(p));

        /// <summary>Pitched while there's something going round.</summary>
        public override bool Possible(Map map) => RoomUtil.ColonistCount(map) >= 2 && Sick(map) > 0;

        public override float Baseline(Map map) => Find.TickManager.TicksGame;

        public override float Update(Map map, float baseline) => Sick(map) > 0 ? Find.TickManager.TicksGame : baseline;

        private float Days(float baseline) => Mathf.Max(0f, (Find.TickManager.TicksGame - baseline) / GenDate.TicksPerDay);

        public override float Progress(Map map, float baseline) => Ratio(Days(baseline), days);

        public override string Describe(Map map, float baseline) => "CC_Goal_HealthyDays".Translate(days.ToString("0"), Days(baseline).ToString("0.#"), Sick(map));
    }

    /// <summary>Several colonists who've sat with the campers often enough to know them (the camp keeps count of visits).</summary>
    public class Goal_CampFriends : AmbitionGoal
    {
        public int count = 3;
        public int visits = 3;

        private static Camp CampOn(Map map)
        {
            var camp = CampTracker.Instance?.camp;
            return camp != null && camp.map == map && camp.Settled && camp.CanTalk ? camp : null;
        }

        private int Friends(Map map)
        {
            var camp = CampOn(map);
            if (camp?.warmth == null) return 0;
            int n = 0;
            foreach (var kv in camp.warmth)
                if (kv.Key != null && !kv.Key.Dead && kv.Key.IsColonist && kv.Value >= visits) n++;
            return n;
        }

        /// <summary>A friendly camp that will be here a few days yet, and enough people to go over.</summary>
        public override bool Possible(Map map)
        {
            var camp = CampOn(map);
            if (camp == null || camp.Here.Count == 0 || RoomUtil.ColonistCount(map) < count + 1) return false;
            if (camp.untilTick != int.MaxValue && camp.untilTick - Find.TickManager.TicksGame < GenDate.TicksPerDay * 4) return false;
            return Friends(map) < count;
        }

        public override float Progress(Map map, float baseline) => Ratio(Friends(map), count);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_CampFriends".Translate(count, CampOn(map)?.Label ?? "", Friends(map));
    }

    /// <summary>Everyone dressed for the cold: comfortable down to this temperature in what they wear now.</summary>
    public class Goal_WarmClothes : AmbitionGoal
    {
        public float temperature = -10f;

        private int Count(Map map, out int total)
        {
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby() && p.apparel != null && !p.IsQuestLodger()).ToList();
            total = people.Count;
            return people.Count(p => p.GetStatValue(StatDefOf.ComfyTemperatureMin) <= temperature);
        }

        /// <summary>Something warm the colony can make now: a coat, a parka, a cloak.</summary>
        private static bool CanMakeWarm(Map map) =>
            DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef?.apparel != null && r.AvailableNow
                && r.ProducedThingDef.GetStatValueAbstract(StatDefOf.Insulation_Cold, r.ProducedThingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(r.ProducedThingDef) : null) >= 20f
                && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)));

        /// <summary>Real cold on its way, someone not dressed for it, and the colony can make something warm.</summary>
        public override bool Possible(Map map)
        {
            if (!SeasonUtil.HasSeasons(map) || SeasonUtil.MinTempNextTwelfths(map, 4) > temperature + 5f) return false;
            int have = Count(map, out int total);
            return total > 0 && have < total && CanMakeWarm(map);
        }

        public override float Progress(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return total == 0 ? 0f : Ratio(have, total);
        }

        public override string Describe(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return "CC_Goal_WarmClothes".Translate(temperature.ToStringTemperature("F0"), have, total);
        }
    }

    /// <summary>At least this many people who can do a job reasonably well, so nobody is the only one who can.</summary>
    public class Goal_SkillDepth : AmbitionGoal
    {
        public SkillDef skill;
        public int count = 2;
        public int level = 6;

        private IEnumerable<SkillRecord> Records(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && !p.IsQuestLodger())
                .Select(p => p.skills?.GetSkill(skill)).Where(r => r != null && !r.TotallyDisabled);

        private int Have(Map map) => skill == null ? 0 : Records(map).Count(r => r.Level >= level);

        /// <summary>Not yet, and there are enough people who could learn it.</summary>
        public override bool Possible(Map map) => skill != null && Have(map) < count && Records(map).Count() >= count;

        public override float Progress(Map map, float baseline) => Ratio(Have(map), count);

        public override string Describe(Map map, float baseline) => "CC_Goal_SkillDepth".Translate(count, skill?.label ?? "?", level, Have(map));
    }

    /// <summary>
    /// Nobody in the colony missing a limb or an eye the colony could replace: something to install is in stock or can be
    /// made (see <see cref="WishWorker_Prosthetic"/>). Missing parts nothing can replace don't count.
    /// </summary>
    public class Goal_Whole : AmbitionGoal
    {
        private static int Missing(Map map)
        {
            int n = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsQuestLodger() || p.health?.hediffSet == null || (p.story?.traits?.HasTrait(TraitDefOf.BodyPurist) ?? false)) continue;
                foreach (var missing in p.health.hediffSet.GetMissingPartsCommonAncestors())
                    if (missing.Part?.def != null && WishWorker_Prosthetic.ObtainableInstall(map, missing.Part.def) != null) n++;
            }
            return n;
        }

        public override bool Possible(Map map) => Missing(map) > 0;

        public override float Baseline(Map map) => Missing(map);

        public override float Progress(Map map, float baseline)
        {
            int now = Missing(map);
            if (now == 0) return 1f;
            return Mathf.Clamp01(1f - now / Mathf.Max(1f, baseline)) * 0.9f;
        }

        public override string Describe(Map map, float baseline) => "CC_Goal_Whole".Translate(Missing(map));
    }

    /// <summary>A room with this role whose stat (cleanliness, say) is at least this.</summary>
    public class Goal_RoomStat : AmbitionGoal
    {
        public string role = "Hospital";
        public string stat = "Cleanliness";
        public float min = 0.5f;

        private RoomRoleDef Role
        {
            get
            {
                var type = GenTypes.GetTypeInAnyAssembly("RimWorld.RoomRoleWorker_" + role);
                return type == null ? null : RoomUtil.RoleByWorker(type);
            }
        }

        private RoomStatDef Stat => DefDatabase<RoomStatDef>.GetNamedSilentFail(stat);

        private float Best(Map map)
        {
            var roleDef = Role;
            var st = Stat;
            if (roleDef == null || st == null) return float.MinValue;
            float best = float.MinValue;
            foreach (var room in RoomUtil.RoomsWithRole(map, roleDef)) best = Mathf.Max(best, room.GetStat(st));
            return best;
        }

        public override bool Possible(Map map) => Role != null && Stat != null;

        public override float Progress(Map map, float baseline)
        {
            float b = Best(map);
            if (b == float.MinValue) return 0f;
            if (b >= min) return 1f;
            // Below zero counts as nothing yet; above it, part of the way.
            return min <= 0f ? 0.3f : Mathf.Clamp01(b / min) * 0.9f;
        }

        public override string Describe(Map map, float baseline)
        {
            float b = Best(map);
            return "CC_Goal_RoomStat".Translate(Role?.label ?? role, Stat?.label ?? stat, min.ToString("0.##"), b == float.MinValue ? "-" : b.ToString("0.##"));
        }
    }
}
