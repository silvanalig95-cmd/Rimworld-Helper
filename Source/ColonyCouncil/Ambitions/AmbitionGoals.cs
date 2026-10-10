using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One checkable goal of an ambition. Goals that count "more of something" capture a baseline when
    /// the ambition is chosen, so progress means real new work.
    /// </summary>
    public abstract class AmbitionGoal
    {
        /// <summary>Can this colony achieve it with what it knows and has? Keeps ambitions era-appropriate.</summary>
        public virtual bool Possible(Map map) => true;

        public virtual float Baseline(Map map) => 0f;

        public abstract float Progress(Map map, float baseline);

        public bool Met(Map map, float baseline) => Progress(map, baseline) >= 0.999f;

        public abstract string Describe(Map map, float baseline);

        protected static float Ratio(float have, float need) => need <= 0f ? 1f : UnityEngine.Mathf.Clamp01(have / need);
    }

    public class Goal_FoodDays : AmbitionGoal
    {
        public float days = 30f;

        public override float Progress(Map map, float baseline) => Ratio(AdvisorWorker_Food.DaysOfFood(map), days);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_FoodDays".Translate(days.ToString("0"), AdvisorWorker_Food.DaysOfFood(map).ToString("0"));
    }

    public class Goal_AllOwnRooms : AmbitionGoal
    {
        public override bool Possible(Map map) => Avail.BestBuildable(map, Avail.IsSingleBed, "Bed") != null;

        private static int Count(Map map, out int total)
        {
            var list = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby() && !p.IsQuestLodger()).ToList();
            total = list.Count;
            return list.Count(p => p.ownership?.OwnedRoom != null || (p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false));
        }

        public override float Progress(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return Ratio(have, total);
        }

        public override string Describe(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return "CC_Goal_AllOwnRooms".Translate(have, total);
        }
    }

    /// <summary>A room with a given role (by worker class name), optionally holding certain furniture and looking good.</summary>
    public class Goal_Room : AmbitionGoal
    {
        public string role = "Hospital";
        public string thingRole;
        public int thingCount = 0;
        public float minImpressiveness = 0f;

        private RoomRoleDef Role
        {
            get
            {
                var type = GenTypes.GetTypeInAnyAssembly("RimWorld.RoomRoleWorker_" + role);
                return type == null ? null : RoomUtil.RoleByWorker(type);
            }
        }

        public override bool Possible(Map map)
        {
            if (Role == null) return false;
            if (thingRole.NullOrEmpty()) return true;
            var pred = RoomTemplateDef.RolePredicate(thingRole);
            return pred != null && (Avail.AnyBuildable(map, pred) || Avail.ColonyHas(map, pred));
        }

        private float Best(Map map)
        {
            var roleDef = Role;
            var pred = thingRole.NullOrEmpty() ? null : RoomTemplateDef.RolePredicate(thingRole);
            float best = 0f;
            foreach (var room in RoomUtil.RoomsWithRole(map, roleDef))
            {
                float things = pred == null || thingCount <= 0 ? 1f : Ratio(RoomUtil.RoomCount(room, pred), thingCount);
                float imp = minImpressiveness <= 0f ? 1f : Ratio(RoomUtil.Impressiveness(room), minImpressiveness);
                best = UnityEngine.Mathf.Max(best, 0.15f + 0.85f * things * imp);
            }
            return best;
        }

        public override float Progress(Map map, float baseline) => Best(map);

        public override string Describe(Map map, float baseline)
        {
            string roleLabel = Role?.label ?? role;
            string s = "CC_Goal_Room".Translate(roleLabel);
            if (thingCount > 0 && !thingRole.NullOrEmpty()) s += " " + "CC_Goal_RoomThings".Translate(thingCount, ("CC_Role_" + thingRole).Translate());
            if (minImpressiveness > 0f) s += " " + "CC_Goal_RoomImpressive".Translate(minImpressiveness.ToString("0"));
            return s;
        }
    }

    /// <summary>Build N more of something, identified by purpose ("turret", "trap", "light", "joy", ...).</summary>
    public class Goal_Build : AmbitionGoal
    {
        public string thingRole = "cover";
        public int count = 4;
        public bool relative = true;

        private System.Func<ThingDef, bool> Pred
        {
            get
            {
                switch (thingRole)
                {
                    case "turret": return Avail.IsAutoTurret;
                    case "trap": return Avail.IsTrap;
                    case "cover": return Avail.IsCover;
                    case "fermenter": return Avail.IsFermenter;
                    case "battery": return d => d.GetCompProperties<CompProperties_Battery>() != null;
                    case "art": return d => d.IsArt;
                    default: return RoomTemplateDef.RolePredicate(thingRole);
                }
            }
        }

        public override bool Possible(Map map)
        {
            var p = Pred;
            if (p == null) return false;
            if (thingRole == "art") return Avail.ColonyHas(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic)) || Avail.AnyBuildable(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic));
            return Avail.AnyBuildable(map, p);
        }

        public override float Baseline(Map map) => relative ? Avail.ColonyCount(map, Pred) : 0f;

        public override float Progress(Map map, float baseline) => Ratio(Avail.ColonyCount(map, Pred) - baseline, count);

        public override string Describe(Map map, float baseline)
        {
            int have = Avail.ColonyCount(map, Pred) - (int)baseline;
            return "CC_Goal_Build".Translate(count, ("CC_Role_" + thingRole).Translate(), UnityEngine.Mathf.Max(0, have));
        }
    }

    public class Goal_Freezer : AmbitionGoal
    {
        public override bool Possible(Map map) =>
            Avail.AnyBuildable(map, d => d.thingClass != null && typeof(Building_Cooler).IsAssignableFrom(d.thingClass));

        public override float Progress(Map map, float baseline) => RoomUtil.HasFreezer(map) ? 1f : 0f;

        public override string Describe(Map map, float baseline) => "CC_Goal_Freezer".Translate();
    }

    public class Goal_Greenhouse : AmbitionGoal
    {
        /// <summary>Only where winter actually stops the fields, and only with hydroponics or sun lamps.</summary>
        public override bool Possible(Map map)
        {
            if (!SeasonUtil.HasSeasons(map)) return false;
            SeasonUtil.Winter(map, out float daysUntil, out float length, out _);
            if (daysUntil < 0f || length < 10f) return false;
            return Avail.AnyBuildable(map, Avail.IsHydroponics) || Avail.AnyBuildable(map, Avail.IsSunLamp);
        }

        public override float Progress(Map map, float baseline) => RoomUtil.HasGreenhouse(map, 20) ? 1f : 0f;

        public override string Describe(Map map, float baseline) => "CC_Goal_Greenhouse".Translate();
    }

    public class Goal_JoyKinds : AmbitionGoal
    {
        public int count = 5;

        public override bool Possible(Map map) => Avail.AnyBuildable(map, Avail.IsJoyGiver);

        public override float Progress(Map map, float baseline) => Ratio(Avail.JoyKindsOnMap(map), count);

        public override string Describe(Map map, float baseline) => "CC_Goal_JoyKinds".Translate(count, Avail.JoyKindsOnMap(map));
    }

    /// <summary>Have N of an item category in stock: medicine, meals, components, silver.</summary>
    public class Goal_Stock : AmbitionGoal
    {
        public string kind = "medicine";
        public int count = 20;

        public static int Count(Map map, string kind)
        {
            switch (kind)
            {
                case "medicine":
                {
                    int n = 0;
                    foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine))
                        if (!t.IsForbidden(Faction.OfPlayer)) n += t.stackCount;
                    return n;
                }
                case "meals":
                    return WishWorker_BetterFood.MealStock(map, FoodPreferability.MealSimple);
                case "finemeals":
                    return WishWorker_BetterFood.MealStock(map, FoodPreferability.MealFine);
                case "silver":
                    return map.resourceCounter.Silver;
                default:
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(kind);
                    return def == null ? 0 : map.resourceCounter.GetCount(def);
                }
            }
        }

        public override bool Possible(Map map)
        {
            if (kind == "finemeals") return WishWorker_BetterFood.CanCook(map, FoodPreferability.MealFine);
            if (kind == "meals") return WishWorker_BetterFood.CanCook(map, FoodPreferability.MealSimple);
            if (kind == "medicine" || kind == "silver") return true;
            return DefDatabase<ThingDef>.GetNamedSilentFail(kind) != null;
        }

        public override float Progress(Map map, float baseline) => Ratio(Count(map, kind), count);

        public override string Describe(Map map, float baseline)
        {
            string label = ("CC_Stock_" + kind).CanTranslate() ? ("CC_Stock_" + kind).Translate().Resolve() : DefDatabase<ThingDef>.GetNamedSilentFail(kind)?.label ?? kind;
            return "CC_Goal_Stock".Translate(count, label, Count(map, kind));
        }
    }

    /// <summary>Finish N research projects (from now).</summary>
    public class Goal_Research : AmbitionGoal
    {
        public int count = 2;

        private static int Finished() => DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(r => r.IsFinished);

        public override bool Possible(Map map) =>
            map.listerBuildings.ColonistsHaveResearchBench() && DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(r => !r.IsFinished && r.CanStartNow) >= count;

        public override float Baseline(Map map) => Finished();

        public override float Progress(Map map, float baseline) => Ratio(Finished() - baseline, count);

        public override string Describe(Map map, float baseline) => "CC_Goal_Research".Translate(count, UnityEngine.Mathf.Max(0, Finished() - (int)baseline));
    }

    public class Goal_AverageMood : AmbitionGoal
    {
        public float mood = 0.6f;

        private static float Avg(Map map)
        {
            var list = map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.mood != null).ToList();
            return list.Count == 0 ? 0f : list.Average(p => p.needs.mood.CurLevelPercentage);
        }

        public override float Progress(Map map, float baseline) => Ratio(Avg(map), mood);

        public override string Describe(Map map, float baseline) => "CC_Goal_Mood".Translate(mood.ToStringPercent(), Avg(map).ToStringPercent());
    }

    public class Goal_Animals : AmbitionGoal
    {
        public int count = 2;

        public override bool Possible(Map map) =>
            map.mapPawns.AllPawnsSpawned.Any(a => a.RaceProps.Animal && a.Faction == null && a.GetStatValue(StatDefOf.Wildness) < 0.85f);

        public override float Baseline(Map map) => RoomUtil.ColonyAnimalCount(map);

        public override float Progress(Map map, float baseline) => Ratio(RoomUtil.ColonyAnimalCount(map) - baseline, count);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_Animals".Translate(count, UnityEngine.Mathf.Max(0, RoomUtil.ColonyAnimalCount(map) - (int)baseline));
    }

    public class Goal_SharedArt : AmbitionGoal
    {
        public int count = 3;

        public override bool Possible(Map map) =>
            Avail.ColonyHas(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic)) || Avail.AnyBuildable(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic));

        public override float Progress(Map map, float baseline) => Ratio(WishWorker_Artwork.SharedArtCount(map), count);

        public override string Describe(Map map, float baseline) => "CC_Goal_SharedArt".Translate(count, WishWorker_Artwork.SharedArtCount(map));
    }

    public class Goal_Colonists : AmbitionGoal
    {
        public int count = 1;

        public override float Baseline(Map map) => RoomUtil.ColonistCount(map);

        public override float Progress(Map map, float baseline) => Ratio(RoomUtil.ColonistCount(map) - baseline, count);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_Colonists".Translate(count, UnityEngine.Mathf.Max(0, RoomUtil.ColonistCount(map) - (int)baseline));
    }

    public class Goal_DefenseRatio : AmbitionGoal
    {
        public float ratio = 1.2f;

        public override float Progress(Map map, float baseline) => Ratio(DefenseUtil.Assess(map).ratio, ratio);

        public override string Describe(Map map, float baseline) => "CC_Goal_Defense".Translate((DefenseUtil.Assess(map).ratio / ratio).ToStringPercent());
    }

    public class Goal_Power : AmbitionGoal
    {
        public float storedWd = 1000f;

        public override bool Possible(Map map) => Avail.HasElectricity && Avail.AnyBuildable(map, d => d.GetCompProperties<CompProperties_Battery>() != null);

        private static float Stored(Map map) => map.powerNetManager.AllNetsListForReading.Sum(n => n.CurrentStoredEnergy());

        public override float Progress(Map map, float baseline) => Ratio(Stored(map), storedWd);

        public override string Describe(Map map, float baseline) => "CC_Goal_Power".Translate(storedWd.ToString("0"), Stored(map).ToString("0"));
    }

    public class Goal_WarmBedrooms : AmbitionGoal
    {
        public override bool Possible(Map map) =>
            SeasonUtil.HasSeasons(map) && Avail.AnyBuildable(map, Avail.IsHeater) && SeasonUtil.MinTempNextTwelfths(map, 4) < 0f;

        private static int Count(Map map, out int total)
        {
            var rooms = RoomUtil.ColonyRooms(map).Where(r => r.Role == RoomRoleDefOf.Bedroom || r.Role == RoomRoleDefOf.Barracks).ToList();
            total = rooms.Count;
            return rooms.Count(r => RoomUtil.RoomContains(r, Avail.IsHeater));
        }

        public override float Progress(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return total == 0 ? 0f : Ratio(have, total);
        }

        public override string Describe(Map map, float baseline)
        {
            int have = Count(map, out int total);
            return "CC_Goal_WarmBedrooms".Translate(have, total);
        }
    }
}
