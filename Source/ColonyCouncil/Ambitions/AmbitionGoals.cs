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

        /// <summary>
        /// Hourly while the ambition is on (the current stage of a staged one): a goal that keeps its own count returns the
        /// baseline it should have now. A streak (days without a breakdown) moves its start forward when the streak breaks.
        /// </summary>
        public virtual float Update(Map map, float baseline) => baseline;

        public abstract float Progress(Map map, float baseline);

        public bool Met(Map map, float baseline) => Progress(map, baseline) >= 0.999f;

        public abstract string Describe(Map map, float baseline);

        protected static float Ratio(float have, float need) => need <= 0f ? 1f : UnityEngine.Mathf.Clamp01(have / need);
    }

    public class Goal_FoodDays : AmbitionGoal
    {
        public float days = 30f;

        public override float Progress(Map map, float baseline) => Ratio(MapFacts.Of(map).FoodDays, days);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_FoodDays".Translate(days.ToString("0"), MapFacts.Of(map).FoodDays.ToString("0"));
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
        /// <summary>Only while a colonist's royal title asks for a throne room (no noble, no throne room ambition).</summary>
        public bool forTitle;

        private RoomRoleDef Role
        {
            get
            {
                var type = GenTypes.GetTypeInAnyAssembly("RimWorld.RoomRoleWorker_" + role);
                return type == null ? null : RoomUtil.RoleByWorker(type);
            }
        }

        /// <summary>Someone in the colony holds a title that wants a throne room (Royalty).</summary>
        public static bool TitleWantsThroneRoom(Map map) =>
            ModsConfig.RoyaltyActive && map.mapPawns.FreeColonists.Any(p => p.royalty != null && p.royalty.CanRequireThroneroom()
                                                                            && p.royalty.HighestTitleWithThroneRoomRequirements() != null);

        public override bool Possible(Map map)
        {
            if (Role == null) return false;
            if (forTitle && !TitleWantsThroneRoom(map)) return false;
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
        /// <summary>Only while the colony has a child or a baby (school desks for nobody is no ambition).</summary>
        public bool forChildren;

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
            if (forChildren && !map.mapPawns.FreeColonists.Any(c => c.DevelopmentalStage.Juvenile())) return false;
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

    /// <summary>
    /// Have N of an item category in stock: medicine, meals, fine meals, silver, alcohol (any brewed drink),
    /// firewood (logs and modded raw wood), or a thing by defName.
    /// </summary>
    public class Goal_Stock : AmbitionGoal
    {
        public string kind = "medicine";
        public int count = 20;
        /// <summary>For named things: only possible if the colony can make it right now (or already has some).</summary>
        public bool needsRecipe;
        /// <summary>This many per colonist, when that comes to more than <see cref="count"/> (a feast for everyone).</summary>
        public float perColonist;

        /// <summary>How many are wanted for this colony.</summary>
        public int Wanted(Map map) => perColonist > 0f ? UnityEngine.Mathf.Max(count, UnityEngine.Mathf.CeilToInt(perColonist * RoomUtil.ColonistCount(map))) : count;

        private static HashSet<ThingDef> alcohol;

        /// <summary>
        /// Beer and anything else brewed: vanilla's beer, Medieval Overhaul's ales and wines, Vanilla Brewing's
        /// spirits. Recognized as liquor or by the alcohol chemical, so modded drinks count without being named.
        /// </summary>
        public static HashSet<ThingDef> Alcohol => alcohol ?? (alcohol = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.category == ThingCategory.Item && d.ingestible != null
                        && ((d.ingestible.foodType & FoodTypeFlags.Liquor) != 0
                            || (d.IsDrug && d.GetCompProperties<CompProperties_Drug>()?.chemical == ChemicalDefOf.Alcohol)))));

        /// <summary>Can the colony brew something: a barrel it has or can build, or a researched recipe at a bench it has or can build.</summary>
        public static bool CanBrew(Map map) =>
            Avail.ColonyHas(map, Avail.IsFermenter) || Avail.AnyBuildable(map, Avail.IsFermenter)
            || DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef != null && Alcohol.Contains(r.ProducedThingDef) && r.AvailableNow
                                                                     && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)));

        public static int Count(Map map, string kind)
        {
            switch (kind)
            {
                case "alcohol":
                {
                    int n = 0;
                    foreach (var d in Alcohol) n += map.resourceCounter.GetCount(d);
                    return n;
                }
                case "firewood":
                    return Goal_Woodpile.Firewood(map);
                case "medicine":
                    return MapFacts.Of(map).Medicine;
                case "meals":
                    return WishWorker_BetterFood.MealStock(map, FoodPreferability.MealSimple);
                case "finemeals":
                    return WishWorker_BetterFood.MealStock(map, FoodPreferability.MealFine);
                case "lavishmeals":
                    return WishWorker_BetterFood.MealStock(map, FoodPreferability.MealLavish);
                case "herbal":
                    return ThingDefOf.MedicineHerbal != null ? map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal) : 0;
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
            if (kind == "lavishmeals") return WishWorker_BetterFood.CanCook(map, FoodPreferability.MealLavish);
            // Healroot that can be grown here: sown with the research it needs.
            if (kind == "herbal")
                return ThingDefOf.MedicineHerbal != null && DefDatabase<ThingDef>.AllDefsListForReading.Any(d => d.plant != null && d.plant.harvestedThingDef == ThingDefOf.MedicineHerbal
                    && d.plant.Sowable && (d.plant.sowResearchPrerequisites == null || d.plant.sowResearchPrerequisites.All(r => r.IsFinished)));
            if (kind == "meals") return WishWorker_BetterFood.CanCook(map, FoodPreferability.MealSimple);
            if (kind == "medicine" || kind == "silver" || kind == "firewood") return true;
            if (kind == "alcohol") return Alcohol.Count > 0 && (Count(map, kind) > 0 || CanBrew(map));
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(kind);
            if (def == null) return false;
            if (!needsRecipe) return true;
            return map.resourceCounter.GetCount(def) > 0 || DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef == def && r.AvailableNow);
        }

        public override float Progress(Map map, float baseline) => Ratio(Count(map, kind), Wanted(map));

        public override string Describe(Map map, float baseline)
        {
            string label = ("CC_Stock_" + kind).CanTranslate() ? ("CC_Stock_" + kind).Translate().Resolve() : DefDatabase<ThingDef>.GetNamedSilentFail(kind)?.label ?? kind;
            return "CC_Goal_Stock".Translate(Wanted(map), label, Count(map, kind));
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

        // A Pokémon (PokéWorld) can't be tamed by hand, only caught with a Poké Ball the colony has or can make.
        public override bool Possible(Map map)
        {
            bool? canCatch = null;
            return map.mapPawns.AllPawnsSpawned.Any(a => a.RaceProps.Animal && a.Faction == null && a.GetStatValue(StatDefOf.Wildness) < 0.85f
                                                         && (!PokeWorldLink.IsPokemon(a) || (canCatch ??= PokeWorldLink.CanCatch(map))));
        }

        public override float Baseline(Map map) => PokeWorldLink.OwnedAnimalCount(map);

        public override float Progress(Map map, float baseline) => Ratio(PokeWorldLink.OwnedAnimalCount(map) - baseline, count);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_Animals".Translate(count, UnityEngine.Mathf.Max(0, PokeWorldLink.OwnedAnimalCount(map) - (int)baseline));
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

namespace ColonyCouncil
{
    /// <summary>Every kitchen clean, and no butchering next to the stove.</summary>
    public class Goal_CleanKitchens : AmbitionGoal
    {
        public float minCleanliness = 0f;

        public override bool Possible(Map map) => RoomUtil.RoomsWithRole(map, RoomUtil.Kitchen).Any();

        public override float Progress(Map map, float baseline)
        {
            var kitchens = RoomUtil.RoomsWithRole(map, RoomUtil.Kitchen).ToList();
            if (kitchens.Count == 0) return 0f;
            int good = kitchens.Count(r => r.GetStat(RoomStatDefOf.Cleanliness) >= minCleanliness
                                           && !RoomUtil.RoomContains(r, AdvisorWorker_Kitchen.IsButcherTable));
            return good / (float)kitchens.Count;
        }

        public override string Describe(Map map, float baseline) => "CC_Goal_CleanKitchens".Translate(minCleanliness.ToString("0.0"));
    }

    /// <summary>Most of the colony's walls made of something that doesn't burn.</summary>
    public class Goal_SafeWalls : AmbitionGoal
    {
        public float share = 0.8f;

        private static float Share(Map map)
        {
            var walls = map.listerBuildings.allBuildingsColonist.Where(b => b.def.IsWall && !b.def.IsFrame).ToList();
            if (walls.Count == 0) return 0f;
            return walls.Count(b => b.GetStatValue(StatDefOf.Flammability) <= 0.2f) / (float)walls.Count;
        }

        public override bool Possible(Map map) =>
            map.listerBuildings.allBuildingsColonist.Count(b => b.def.IsWall && !b.def.IsFrame) >= 20
            && DefDatabase<ThingDef>.AllDefsListForReading.Any(d => d.IsStuff && d.stuffProps?.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Stony) && map.resourceCounter.GetCount(d) > 0);

        public override float Progress(Map map, float baseline) => Ratio(Share(map), share);

        public override string Describe(Map map, float baseline) => "CC_Goal_SafeWalls".Translate(share.ToStringPercent(), Share(map).ToStringPercent());
    }

    /// <summary>No room people use is left in the dark.</summary>
    public class Goal_AllLit : AmbitionGoal
    {
        private static int Dark(Map map)
        {
            var worker = new AdvisorWorker_Darkness();
            var signal = worker.Evaluate(map).FirstOrDefault();
            return signal == null ? 0 : 1;
        }

        public override bool Possible(Map map) => Avail.AnyBuildable(map, Avail.IsLight);

        public override float Progress(Map map, float baseline) => Dark(map) == 0 ? 1f : 0.3f;

        public override string Describe(Map map, float baseline) => "CC_Goal_AllLit".Translate();
    }

    /// <summary>Someone in the colony masters a skill.</summary>
    public class Goal_SkillLevel : AmbitionGoal
    {
        public SkillDef skill;
        public int level = 10;

        private int Best(Map map) => map.mapPawns.FreeColonistsSpawned.Select(p => p.skills?.GetSkill(skill)).Where(s => s != null && !s.TotallyDisabled).Select(s => s.Level).DefaultIfEmpty(0).Max();

        public override bool Possible(Map map) => skill != null && map.mapPawns.FreeColonistsSpawned.Any(p => p.skills?.GetSkill(skill) is SkillRecord s && !s.TotallyDisabled && People.Passionate(s) && s.Level < level);

        public override float Progress(Map map, float baseline) => skill == null ? 0f : Ratio(Best(map), level);

        public override string Describe(Map map, float baseline) => "CC_Goal_SkillLevel".Translate(skill?.label ?? "?", level, Best(map));
    }

    /// <summary>A number of factions abroad who like the colony this much: trade partners, allies, someone to call when it goes wrong.</summary>
    public class Goal_Goodwill : AmbitionGoal
    {
        public int goodwill = 75;
        public int count = 1;

        private static IEnumerable<Faction> Candidates() =>
            Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.deactivated && !f.temporary && f.HasGoodwill && !f.def.PermanentlyHostileTo(Faction.OfPlayer.def));

        private int Friends() => Candidates().Count(f => f.PlayerGoodwill >= goodwill);

        public override bool Possible(Map map) => Candidates().Count() >= count;

        public override float Progress(Map map, float baseline) => Ratio(Friends(), count);

        public override string Describe(Map map, float baseline) => "CC_Goal_Goodwill".Translate(count, goodwill, Friends());
    }

    /// <summary>Make N new things of at least this quality: something the colony can point at and say "we made that".</summary>
    public class Goal_Quality : AmbitionGoal
    {
        public QualityCategory quality = QualityCategory.Masterwork;
        public int count = 1;
        /// <summary>Someone needs at least this level in Crafting, Artistic or Construction for it to be a fair ask.</summary>
        public int minSkill = 10;

        private int cachedTick = -99999;
        private Map cachedMap;
        private int cached;

        // Read when needed: goals are made while the defs are still loading, before SkillDefOf is filled in.
        private static SkillDef[] MakerSkills => new[] { SkillDefOf.Crafting, SkillDefOf.Artistic, SkillDefOf.Construction };

        private int Count(Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (map == cachedMap && now - cachedTick < 250 && now >= cachedTick) return cached;
            cachedTick = now;
            cachedMap = map;
            var seen = new HashSet<Thing>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)) Consider(t, seen);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (t.Faction == Faction.OfPlayer) Consider(t, seen);
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.apparel != null) foreach (var a in p.apparel.WornApparel) Consider(a, seen);
                if (p.equipment != null) foreach (var e in p.equipment.AllEquipmentListForReading) Consider(e, seen);
            }
            cached = seen.Count;
            return cached;
        }

        private void Consider(Thing t, HashSet<Thing> seen)
        {
            var inner = t.GetInnerIfMinified();
            if (inner.TryGetQuality(out var q) && q >= quality) seen.Add(inner);
        }

        public override bool Possible(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Any(p => MakerSkills.Any(s => p.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled && r.Level >= minSkill));

        public override float Baseline(Map map) => Count(map);

        public override float Progress(Map map, float baseline) => Ratio(Count(map) - baseline, count);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_Quality".Translate(count, quality.GetLabel(), UnityEngine.Mathf.Max(0, Count(map) - (int)baseline));
    }

    /// <summary>A winter woodpile: only asked for when the colony actually burns wood and real cold is on its way.</summary>
    public class Goal_Woodpile : Goal_Stock
    {
        public Goal_Woodpile()
        {
            kind = "firewood";
            count = 300;
        }

        public override bool Possible(Map map) => ColdComing(map) && BurnsWood(map);

        /// <summary>Logs, and Medieval Overhaul's raw wood (looked up by name, so it's simply left out without the mod).</summary>
        public static IEnumerable<ThingDef> FirewoodDefs
        {
            get
            {
                yield return ThingDefOf.WoodLog;
                var raw = DefDatabase<ThingDef>.GetNamedSilentFail("DankPyon_RawWood");
                if (raw != null) yield return raw;
            }
        }

        public static int Firewood(Map map)
        {
            int n = 0;
            foreach (var d in FirewoodDefs) n += map.resourceCounter.GetCount(d);
            return n;
        }

        /// <summary>Real cold within the next few months (and a map with seasons at all).</summary>
        public static bool ColdComing(Map map) => SeasonUtil.HasSeasons(map) && SeasonUtil.MinTempNextTwelfths(map, 4) < 0f;

        /// <summary>The colony has something that burns wood: a hearth, a campfire, a wood-fired stove or heater.</summary>
        public static bool BurnsWood(Map map) => map.listerBuildings.allBuildingsColonist.Any(BurnsWood);

        private static bool BurnsWood(Building b) =>
            b.TryGetComp<CompRefuelable>() is CompRefuelable r && r.Props.fuelFilter != null && FirewoodDefs.Any(r.Props.fuelFilter.Allows);
    }

    /// <summary>No two colonists who loathe each other (both ways at or below the threshold).</summary>
    public class Goal_NoRivals : AmbitionGoal
    {
        public int threshold = -20;

        private int Pairs(Map map)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => p.relations != null && !p.IsQuestLodger()).ToList();
            int n = 0;
            for (int i = 0; i < pawns.Count; i++)
                for (int j = i + 1; j < pawns.Count; j++)
                    if (pawns[i].relations.OpinionOf(pawns[j]) <= threshold || pawns[j].relations.OpinionOf(pawns[i]) <= threshold) n++;
            return n;
        }

        public override bool Possible(Map map) => Pairs(map) > 0;

        public override float Progress(Map map, float baseline) => Pairs(map) == 0 ? 1f : 0.3f;

        public override string Describe(Map map, float baseline) => "CC_Goal_NoRivals".Translate(Pairs(map));
    }

    /// <summary>The colony's average trust in the council at or above a level.</summary>
    public class Goal_ColonyTrust : AmbitionGoal
    {
        public float trust = 30f;

        private static float Now() => CouncilGameComponent.Instance?.standings.ColonyTrust() ?? 0f;

        public override bool Possible(Map map) => ColonyCouncilMod.Settings.enableWishes && Now() < trust - 5f;

        public override float Progress(Map map, float baseline) => Ratio(Now() + 100f, trust + 100f);

        public override string Describe(Map map, float baseline) => "CC_Goal_ColonyTrust".Translate(trust.ToString("0"), Now().ToString("0"));
    }

    /// <summary>Nobody in the colony addicted to anything.</summary>
    public class Goal_NoAddictions : AmbitionGoal
    {
        private static int Addicts(Map map) => map.mapPawns.FreeColonistsSpawned.Count(p => p.health?.hediffSet?.hediffs.Any(h => h is Hediff_Addiction) ?? false);

        public override bool Possible(Map map) => Addicts(map) > 0;

        public override float Progress(Map map, float baseline) => Addicts(map) == 0 ? 1f : 0.3f;

        public override string Describe(Map map, float baseline) => "CC_Goal_NoAddictions".Translate(Addicts(map));
    }

    /// <summary>Burn a hostile settlement to the ground. Only pitched while the colony is grieving or fresh from a raid.</summary>
    public class Goal_RaidSettlement : AmbitionGoal
    {
        public float maxTiles = 25f;
        public int minFighters = 4;

        private static int Destroyed() => Find.WorldObjects.AllWorldObjects.Count(o => o.def == WorldObjectDefOf.DestroyedSettlement);

        private RimWorld.Planet.Settlement Nearest(Map map) => Find.WorldObjects.Settlements
            .Where(s => s.Faction != null && !s.Faction.IsPlayer && s.Faction.HostileTo(Faction.OfPlayer) && s.Attackable)
            .Select(s => (s, d: Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile)))
            .Where(x => x.d <= maxTiles)
            .OrderBy(x => x.d)
            .Select(x => x.s)
            .FirstOrDefault();

        public override bool Possible(Map map)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned;
            if (pawns.Count(p => !p.WorkTagIsDisabled(WorkTags.Violent) && !p.DevelopmentalStage.Juvenile()) < minFighters) return false;
            int lastThreat = map.storyState.LastThreatBigTick;
            bool angry = pawns.Any(Flavor.IsGrieving) || (lastThreat > 0 && Find.TickManager.TicksGame - lastThreat < GenDate.TicksPerDay * 5);
            return angry && Nearest(map) != null;
        }

        public override float Baseline(Map map) => Destroyed();

        public override float Progress(Map map, float baseline) => Destroyed() > baseline ? 1f : 0f;

        public override string Describe(Map map, float baseline)
        {
            var s = Nearest(map);
            return s == null ? "CC_Goal_RaidSettlementAny".Translate() : "CC_Goal_RaidSettlement".Translate(s.LabelCap, s.Faction.Name, Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile).ToString("0"));
        }
    }

    /// <summary>Complete quests abroad (from now).</summary>
    public class Goal_Quests : AmbitionGoal
    {
        public int count = 2;

        private static int Done() => Find.QuestManager.QuestsListForReading.Count(q => q.State == QuestState.EndedSuccess);

        public override float Baseline(Map map) => Done();

        public override float Progress(Map map, float baseline) => Ratio(Done() - baseline, count);

        public override string Describe(Map map, float baseline) => "CC_Goal_Quests".Translate(count, UnityEngine.Mathf.Max(0, Done() - (int)baseline));
    }
}
