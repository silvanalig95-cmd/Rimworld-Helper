using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== Wild bounty =====================

    /// <summary>A good year for wild berries (or healroot, or whatever grows wild here): patches spring up ready to pick.</summary>
    public class HappeningWorker_WildBounty : IncidentWorker_Happening
    {
        private static IEnumerable<ThingDef> Species(Map map) =>
            map.Biome.AllWildPlants.Where(p => p?.plant != null && !p.plant.IsTree && !p.plant.cavePlant && p.plant.Harvestable && p.plant.harvestedThingDef is ThingDef h
                                               && ((h.IsNutritionGivingIngestible && h.ingestible.HumanEdible && h.ingestible.preferability >= FoodPreferability.RawBad) || h.IsMedicine));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Species(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.thing = Species(ctx.map).RandomElementByWeightWithFallback(p => p.plant.harvestedThingDef.IsMedicine ? 0.6f : 1f);
            if (ctx.thing == null) return false;
            // What's there to pick: "berries", not "berry bush"; medicine plants go by their own name.
            var crop = ctx.thing.plant.harvestedThingDef;
            ctx.tale = crop.IsMedicine ? ctx.thing.label : crop.label;
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.a = pawns.Where(p => p.skills?.GetSkill(SkillDefOf.Plants) is SkillRecord r && People.Passionate(r)).RandomElementWithFallback() ?? pawns.RandomElementWithFallback();
            return ctx.a != null;
        }

        private static bool Free(IntVec3 c, Map map, ThingDef plant)
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.Roofed(map) || c.GetFirstBuilding(map) != null || c.GetZone(map) != null) return false;
            if (map.areaManager.Home[c] || map.fertilityGrid.FertilityAt(c) < plant.plant.fertilityMin) return false;
            var existing = c.GetPlant(map);
            return existing == null || (!existing.def.plant.IsTree && !existing.sown && existing.def != plant);
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var plant = ctx.thing;
            int spawned = 0;
            int patches = ext.count.RandomInRange;
            for (int i = 0; i < patches; i++)
            {
                if (!CellFinderLoose.TryGetRandomCellWith(c => Free(c, map, plant), map, 400, out var center)) continue;
                int want = Rand.RangeInclusive(5, 9);
                foreach (var c in GenRadial.RadialCellsAround(center, 3f, true))
                {
                    if (want <= 0) break;
                    if (!Free(c, map, plant) || !Rand.Chance(0.75f)) continue;
                    c.GetPlant(map)?.Destroy();
                    if (GenSpawn.Spawn(plant, c, map) is Plant p)
                    {
                        p.Growth = 1f;
                        if (ctx.place == null) ctx.place = p;
                        spawned++;
                        want--;
                    }
                }
            }
            ctx.count = spawned;
            return spawned > 0;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.thing?.plant?.harvestedThingDef?.IsMedicine == true ? "medicine" : "food");
        }
    }

    // ===================== Sky and weather =====================

    /// <summary>Starts a game condition (a comet, fireflies, a hailstorm) for a few days.</summary>
    public class HappeningWorker_Condition : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ext.condition != null && !map.gameConditionManager.ConditionIsActive(ext.condition) && Colonists(map).Count > 0;

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var cond = GameConditionMaker.MakeCondition(ext.condition, Mathf.RoundToInt(ext.days.RandomInRange * GenDate.TicksPerDay));
            ctx.map.gameConditionManager.RegisterCondition(cond);
            return true;
        }
    }

    /// <summary>A hailstorm: only worth telling about when there are crops out in the open.</summary>
    public class HappeningWorker_Hail : HappeningWorker_Condition
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && GameCondition_Hail.Crops(map).Count >= 12;
    }

    /// <summary>A comet in the sky for days: stargazing is better, and people read it as an omen, each in their own way.</summary>
    public class GameCondition_Comet : GameCondition
    {
        public override float SkyGazeChanceFactor(Map map) => 2.5f;

        public override float SkyGazeJoyGainFactor(Map map) => 1.6f;

        // A faint cool cast on the night sky (colors only; see SkyTint).
        public override SkyTarget? SkyTarget(Map map) => SkyTint.Comet(this, map);

        public override float SkyTargetLerpFactor(Map map) => SkyTint.CometLerp(this, map);
    }

    /// <summary>Warm nights full of fireflies: anyone outside after dark gets to see them.</summary>
    public class GameCondition_Fireflies : GameCondition
    {
        public override float SkyGazeJoyGainFactor(Map map) => 1.3f;

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Find.TickManager.TicksGame % 600 != 0) return;
            foreach (var map in AffectedMaps)
            {
                int hour = GenLocalDate.HourOfDay(map);
                if (hour >= 4 && hour < 20) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!p.Awake() || p.Position.Roofed(map) || p.InMentalState) continue;
                    if (p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(CouncilDefOf.Council_Evt_Fireflies) != null) continue;
                    CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_Fireflies);
                }
            }
        }
    }

    /// <summary>
    /// Hail for a few hours: crops out in the open get knocked back (some young ones flattened), and anyone caught
    /// outside grumbles. Nobody is hurt. Roofed fields and greenhouses are safe. The colony sees it coming: for the first
    /// two hours the sky only warns of it (the one who knows the fields best says so, and a work party brings the ripe
    /// crops in, from the event's effects), then it comes. It rains with the hail while it's warm enough to rain, and
    /// when the hail ends a rain it brought hands back to the weather's own choice.
    /// </summary>
    public class GameCondition_Hail : GameCondition
    {
        private int hits;
        /// <summary>It was raining already when the hail began: that rain is the weather's own and carries on.</summary>
        private bool wasRaining;
        private bool warned;

        /// <summary>The time the sky warns of it before the hail comes.</summary>
        public const int LeadTicks = GenDate.TicksPerHour * 2;

        /// <summary>Still only coming: the sky warns of it.</summary>
        public bool Coming => TicksPassed < LeadTicks;

        /// <summary>Hail is falling on this map now (not just coming).</summary>
        public static bool Falling(Map map)
        {
            var c = map?.gameConditionManager?.GetActiveCondition<GameCondition_Hail>();
            return c != null && !c.Coming;
        }

        /// <summary>Crops in open fields (growing zones under the sky).</summary>
        public static List<Plant> Crops(Map map)
        {
            var list = new List<Plant>();
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in zone.Cells)
                {
                    if (c.Roofed(map)) continue;
                    var p = c.GetPlant(map);
                    if (p != null && p.sown && !p.def.plant.IsTree) list.Add(p);
                }
            return list;
        }

        private static WeatherDef rain;
        private static bool rainLooked;

        private static WeatherDef Rain
        {
            get
            {
                if (!rainLooked)
                {
                    rain = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
                    rainLooked = true;
                }
                return rain;
            }
        }

        public override void Init()
        {
            base.Init();
            var now = SingleMap?.weatherManager?.curWeather;
            wasRaining = now != null && now.rainRate > 0.1f;
        }

        /// <summary>The game's rain while the hail falls, as long as it's warm enough outside to rain (the game's own range for rain).</summary>
        public override WeatherDef ForcedWeather()
        {
            if (Coming) return null;
            var r = Rain;
            var map = SingleMap;
            if (r == null || map == null) return r;
            return r.temperatureRange.Includes(map.mapTemperature.OutdoorTemp) ? r : null;
        }

        /// <summary>
        /// The one who knows the fields best (growing, then years) reads the sky and says so, if they're in view: the
        /// warning that comes before the hail.
        /// </summary>
        private void Warn(Map map)
        {
            warned = true;
            var reader = map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Drafted && !p.DevelopmentalStage.Baby() && p.skills != null)
                .OrderByDescending(p => (p.skills.GetSkill(SkillDefOf.Plants)?.Level ?? 0) * 2f + p.ageTracker.AgeBiologicalYearsFloat / 10f).FirstOrDefault();
            if (reader != null) WeatherTalk.SayBy(reader, "Council_Pool_SayHailComing");
        }

        /// <summary>A rain the hail brought doesn't run on for a day or two after it: the weather chooses again (easing over an hour or so).</summary>
        public override void End()
        {
            base.End();
            var map = SingleMap;
            if (wasRaining || Rain == null || map?.weatherDecider == null || map.weatherManager?.curWeather != Rain) return;
            map.weatherDecider.StartNextWeather();
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (Coming)
            {
                if (!warned && AffectedMaps.Count > 0) Warn(AffectedMaps[0]);
                return;
            }
            foreach (var map in AffectedMaps)
            {
                var crops = Crops(map);
                // About a quarter of the open fields over the storm, a few plants at a time.
                if (crops.Count > 0 && hits < Mathf.Max(6, crops.Count / 4))
                {
                    foreach (var p in crops.InRandomOrder().Take(Rand.RangeInclusive(1, 3)))
                    {
                        hits++;
                        if (p.Growth < 0.25f && Rand.Chance(0.35f)) p.Kill(null, null);
                        else
                        {
                            p.Growth = Mathf.Max(0.05f, p.Growth - Rand.Range(0.08f, 0.2f));
                            map.mapDrawer.MapMeshDirty(p.Position, MapMeshFlagDefOf.Things);
                        }
                        VisualTouches.HailHit(map, p.Position);
                    }
                }
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.Position.Roofed(map) || !pawn.Awake()) continue;
                    if (pawn.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(CouncilDefOf.Council_Evt_Hail) != null) continue;
                    CouncilGameComponent.GiveThought(pawn, CouncilDefOf.Council_Evt_Hail);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hits, "hits");
            Scribe_Values.Look(ref wasRaining, "wasRaining");
            Scribe_Values.Look(ref warned, "warned");
        }
    }

    /// <summary>How someone reads the comet: a good omen (the cheerful), a bad one (the gloomy and nervous), or just a wonder.</summary>
    public class ThoughtWorker_Comet : ThoughtWorker
    {
        private static TraitDef naturalMood;
        private static TraitDef nerves;
        private static bool looked;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var map = p.MapHeld;
            if (map == null || CouncilDefOf.Council_Comet == null || !map.gameConditionManager.ConditionIsActive(CouncilDefOf.Council_Comet)) return ThoughtState.Inactive;
            if (!ColonyCouncilMod.Settings.MoodEffects || !p.RaceProps.Humanlike) return ThoughtState.Inactive;
            if (!looked)
            {
                naturalMood = DefDatabase<TraitDef>.GetNamedSilentFail("NaturalMood");
                nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                looked = true;
            }
            var traits = p.story?.traits;
            if (traits == null) return ThoughtState.ActiveAtStage(2);
            int mood = naturalMood != null && traits.HasTrait(naturalMood) ? traits.DegreeOfTrait(naturalMood) : 0;
            int nerve = nerves != null && traits.HasTrait(nerves) ? traits.DegreeOfTrait(nerves) : 0;
            if (mood > 0) return ThoughtState.ActiveAtStage(0);
            if (mood < 0 || nerve < 0) return ThoughtState.ActiveAtStage(1);
            return ThoughtState.ActiveAtStage(2);
        }
    }

    // ===================== Vermin =====================

    /// <summary>
    /// Rats (or whatever passes for them here) get into the stores. Food kept cold is safe; a colony cat or dog
    /// may catch them first. Otherwise some of the food is spoiled.
    /// </summary>
    public class HappeningWorker_Pests : IncidentWorker_Happening
    {
        private static List<Thing> AtRisk(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && !(t is Pawn) && !t.def.IsCorpse && !t.def.IsDrug && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible
                            && t.IsInAnyStorage() && t.AmbientTemperature > 4f).ToList();

        private static Pawn Mouser(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Downed && !a.Dead && a.RaceProps.predator && a.RaceProps.baseBodySize <= 1.1f).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => AtRisk(map).Count >= 3;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var food = AtRisk(ctx.map);
            var room = food.GroupBy(t => t.GetRoom()).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (room == null) return false;
            ctx.place = room.OrderByDescending(t => t.stackCount).First();
            ctx.pet = Mouser(ctx.map);
            ctx.variant = ctx.pet != null ? 1 : 0;
            ctx.a = ctx.pet != null ? PetTracker.PersonOf(ctx.pet) : null;
            ctx.a = ctx.a ?? Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (ctx.a == null) return false;
            if (ctx.variant == 0)
            {
                int lost = 0;
                foreach (var t in room.ToList())
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(t.stackCount * Rand.Range(0.1f, 0.25f)));
                    n = Mathf.Min(n, t.stackCount);
                    lost += n;
                    if (n >= t.stackCount)
                    {
                        if (t == ctx.place) ctx.place = null;
                        t.Destroy();
                    }
                    else t.SplitOff(n).Destroy();
                }
                ctx.count = lost;
            }
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // apart: an animal's good turn, credited to its person.
            // Only the animal's own person takes the credit.
            if (ctx.variant == 1 && ctx.a != null && PetTracker.PersonOf(ctx.pet) == ctx.a) CouncilGameComponent.GiveThought(ctx.a, CouncilDefOf.Council_Evt_PetCaughtVermin, ctx.pet);
            return true;
        }

        protected override LetterDef LetterFor(HappeningContext ctx, HappeningExtension ext) => ctx.variant == 1 ? LetterDefOf.PositiveEvent : base.LetterFor(ctx, ext);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "mouser" : "loss");

        protected override string Extra(HappeningContext ctx) => ctx.variant == 0 ? "CC_Evt_PestsTip".Translate().Resolve() : null;
    }

    // ===================== A find =====================

    /// <summary>Someone working outside turns up something buried long ago: of the colony's own era.</summary>
    public class HappeningWorker_BuriedCache : IncidentWorker_Happening
    {
        private static IEnumerable<Pawn> Outside(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Baby() && p.Awake() && !p.Drafted && !p.Position.Roofed(map));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Outside(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Outside(ctx.map).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var things = Loot.Make(ext.value.RandomInRange * Mathf.Clamp(Mathf.Sqrt(ctx.map.wealthWatcher.WealthTotal / 40000f), 0.6f, 2f), Rand.Chance(0.25f));
            if (things.Count == 0) return false;
            foreach (var t in things) GenPlace.TryPlaceThing(t, ctx.a.Position, ctx.map, ThingPlaceMode.Near);
            ctx.place = things.FirstOrDefault(t => t.Spawned);
            ctx.tale = Loot.Describe(things);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var job = ctx.a?.CurJobDef;
            if (job == JobDefOf.Mine) tags.Add("mining");
            else if (job == JobDefOf.Sow || job == JobDefOf.Harvest || job == JobDefOf.CutPlant) tags.Add("farming");
            if (ctx.a?.DevelopmentalStage.Child() == true) tags.Add("child");
        }

        protected override string Extra(HappeningContext ctx) => "CC_Evt_Found".Translate(ctx.tale ?? "").Resolve();
    }
}
