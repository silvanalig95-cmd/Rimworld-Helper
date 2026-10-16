using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist is doing this hour, as far as ailments care: one look at their job, where they stand and the
    /// room they're in. Nothing here walks the map; the one loop (a small room's things, for a fire) stops after a few
    /// hundred things and only runs for rooms of 60 cells or fewer.
    /// </summary>
    public static class AilmentChecks
    {
        private const int MaxThingsLooked = 300;

        private static readonly string[] WetBiomes = { "swamp", "jungle", "rainforest", "bog", "marsh", "wetland", "mangrove" };
        private static readonly string[] AshBiomes = { "volcan", "lava", "ash", "pyroclast", "cinder" };

        private static readonly Dictionary<BiomeDef, bool> wetBiome = new Dictionary<BiomeDef, bool>();
        private static readonly Dictionary<BiomeDef, bool> ashBiome = new Dictionary<BiomeDef, bool>();

        private static NeedDef hygiene;
        private static TraitDef nightOwl;
        private static GameConditionDef noxiousHaze;
        private static TerrainDef ice;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            // Dubs Bad Hygiene's need, when it's there.
            hygiene = DefDatabase<NeedDef>.GetNamedSilentFail("Hygiene");
            nightOwl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            // Biotech's acid haze over a polluted world.
            noxiousHaze = DefDatabase<GameConditionDef>.GetNamedSilentFail("NoxiousHaze");
            ice = DefDatabase<TerrainDef>.GetNamedSilentFail("Ice");
        }

        public static void Reset()
        {
            wetBiome.Clear();
            ashBiome.Clear();
            looked = false;
            hygiene = null;
            nightOwl = null;
            noxiousHaze = null;
            ice = null;
        }

        private static bool BiomeIs(BiomeDef b, string[] words, Dictionary<BiomeDef, bool> cache)
        {
            if (b == null) return false;
            if (cache.TryGetValue(b, out bool v)) return v;
            string n = b.defName.ToLowerInvariant();
            v = false;
            foreach (var w in words)
                if (n.Contains(w))
                {
                    v = true;
                    break;
                }
            cache[b] = v;
            return v;
        }

        public static bool WetBiome(Map map) => BiomeIs(map?.Biome, WetBiomes, wetBiome);

        public static bool AshBiome(Map map) => BiomeIs(map?.Biome, AshBiomes, ashBiome);

        private static bool Has(int mask, AilmentTrigger t) => (mask & (1 << (int)t)) != 0;

        private static int Bit(AilmentTrigger t) => 1 << (int)t;

        /// <summary>The counted triggers that fit this colonist right now, as bits (1 &lt;&lt; trigger).</summary>
        public static int Sample(Pawn p, bool awake)
        {
            int mask = 0;
            var caravan = p.GetCaravan();
            if (caravan != null)
            {
                if (awake && caravan.pather != null && caravan.pather.MovingNow) mask |= Bit(AilmentTrigger.Walking) | Bit(AilmentTrigger.CaravanTravel);
                // A caravan camps rough.
                if (!awake) mask |= Bit(AilmentTrigger.HardBed);
                if (awake) mask |= Inner(p);
                return mask;
            }
            if (!p.Spawned || p.Map == null) return 0;
            // Knocked out on the ground isn't a night's sleep, and a downed colonist isn't working.
            if (p.Downed && !p.InBed()) return 0;
            var map = p.Map;
            var room = p.GetRoom();
            bool outdoors = room == null || room.PsychologicallyOutdoors;
            if (Smoky(room)) mask |= Bit(AilmentTrigger.SmokyRoom);
            float temp = p.AmbientTemperature;
            if (temp >= 32f || (temp >= 27f && WetBiome(map))) mask |= Bit(AilmentTrigger.Sweltering);
            if (outdoors && temp >= 18f && WetBiome(map)) mask |= Bit(AilmentTrigger.Insects);
            if (!awake)
            {
                var bed = RestUtility.CurrentBed(p);
                if (HardBed(p, bed)) mask |= Bit(AilmentTrigger.HardBed);
                var bedRoom = bed != null ? bed.GetRoom() : room;
                if (bedRoom != null && !bedRoom.PsychologicallyOutdoors)
                {
                    if (Damp(bedRoom, bed?.Position ?? p.Position, map)) mask |= Bit(AilmentTrigger.DampSleep);
                    if (CrowdedDirty(p, bedRoom)) mask |= Bit(AilmentTrigger.CrowdedDirty);
                }
                return mask;
            }
            mask |= Inner(p);
            if (SmallHours(p, map)) mask |= Bit(AilmentTrigger.NightWork);
            if (InSpace(map)) mask |= Bit(AilmentTrigger.InSpace);
            var job = p.CurJob;
            string work = job?.workGiverDef?.workType?.defName;
            bool working = work != null;
            bool heavy = working && (work.IndexOf("Haul", StringComparison.Ordinal) >= 0 || work == "Mining" || work == "Construction");
            if (!heavy && job != null && (job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer || job.def == JobDefOf.Mine)) heavy = true;
            if (heavy) mask |= Bit(AilmentTrigger.HeavyWork);
            bool hands = working && (work == "Crafting" || work == "Smithing" || work == "Tailoring" || work == "Art");
            if (hands) mask |= Bit(AilmentTrigger.HandWork);
            if ((hands || work == "Research") && map.glowGrid.PsychGlowAt(p.Position) == PsychGlow.Dark) mask |= Bit(AilmentTrigger.DarkWork);
            bool moving = p.pather != null && p.pather.Moving;
            // Long walks: out on a map away from home, or out hunting. Errands around the home don't wear out feet.
            if (moving && (!map.IsPlayerHome || work == "Hunting")) mask |= Bit(AilmentTrigger.Walking);
            if (job != null && Chopping(job)) mask |= Bit(AilmentTrigger.Chopping);
            if (moving && WetGround(p.Position, map)) mask |= Bit(AilmentTrigger.WetFeet);
            if (outdoors)
            {
                if (working && temp >= -10f && temp <= 6f) mask |= Bit(AilmentTrigger.ColdWork);
                if (temp < -10f) mask |= Bit(AilmentTrigger.BitterCold);
                if (InTheSun(p, map, temp)) mask |= Bit(AilmentTrigger.SunWork);
                if (moving && RoughGround(p.Position, map)) mask |= Bit(AilmentTrigger.RoughGround);
                if (AshBiome(map) || ConditionOn(map, GameConditionDefOf.VolcanicWinter)) mask |= Bit(AilmentTrigger.Ashfall);
                if (temp >= 10f && (work == "Growing" || work == "PlantCutting")) mask |= Bit(AilmentTrigger.PlantWork);
                if (Polluted(p, map)) mask |= Bit(AilmentTrigger.Polluted);
                if (SnowGlare(p, map)) mask |= Bit(AilmentTrigger.SnowGlare);
            }
            if (job != null && (job.def == JobDefOf.BeatFire || job.def == JobDefOf.ExtinguishFiresNearby || work == "Firefighter"))
                mask |= Bit(AilmentTrigger.Firefighting);
            if (job != null && Ancient(job)) mask |= Bit(AilmentTrigger.AncientWork);
            return mask;
        }

        /// <summary>
        /// Biotech: polluted ground underfoot, or the acid haze of a polluted world overhead, for someone without a gas
        /// mask (or a gene, or anything else, that makes them half proof against a toxic world).
        /// </summary>
        private static bool Polluted(Pawn p, Map map)
        {
            if (!ModsConfig.BiotechActive) return false;
            Look();
            bool polluted;
            try
            {
                polluted = (map.pollutionGrid != null && map.pollutionGrid.IsPolluted(p.Position)) || ConditionOn(map, noxiousHaze);
                if (polluted && StatDefOf.ToxicEnvironmentResistance != null && p.GetStatValue(StatDefOf.ToxicEnvironmentResistance) >= 0.5f) return false;
            }
            catch
            {
                return false;
            }
            return polluted;
        }

        /// <summary>
        /// The glare off snow and ice: around midday, under a bright sky with nothing falling, on deep snow or bare ice,
        /// with nothing over the eyes.
        /// </summary>
        private static bool SnowGlare(Pawn p, Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 9 || hour > 16) return false;
            if (map.skyManager == null || map.skyManager.CurSkyGlow < 0.7f) return false;
            var weather = map.weatherManager?.curWeather;
            if (weather != null && (weather.rainRate > 0.1f || weather.snowRate > 0.1f)) return false;
            Look();
            bool white = (map.snowGrid != null && map.snowGrid.GetDepth(p.Position) >= 0.4f) || (ice != null && p.Position.GetTerrain(map) == ice);
            return white && !CoveredEyes(p);
        }

        /// <summary>Goggles, a visor, a full helmet: anything worn over the eyes.</summary>
        public static bool CoveredEyes(Pawn p)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return false;
            foreach (var a in worn)
            {
                var groups = a.def.apparel?.bodyPartGroups;
                if (groups != null && (groups.Contains(BodyPartGroupDefOf.Eyes) || groups.Contains(BodyPartGroupDefOf.FullHead))) return true;
            }
            return false;
        }

        /// <summary>Odyssey: a map in orbit, where the light never changes and the air comes out of the scrubbers.</summary>
        private static bool InSpace(Map map) => ModsConfig.OdysseyActive && map?.Biome != null && map.Biome.inVacuum;

        public static bool Counted(AilmentTrigger t) => t >= AilmentTrigger.HeavyWork && t < AilmentTrigger.Wound;

        /// <summary>What's going on inside them, wherever they are: hunger, and worry (low mood or strain).</summary>
        private static int Inner(Pawn p)
        {
            int mask = 0;
            var food = p.needs?.food;
            if (food != null && food.CurCategory >= HungerCategory.Hungry) mask |= Bit(AilmentTrigger.Hungry);
            var mood = p.needs?.mood;
            if (mood != null && (mood.CurLevelPercentage < 0.3f || Heart.StrainOf(p) >= 30f)) mask |= Bit(AilmentTrigger.Worry);
            return mask;
        }

        /// <summary>Up between midnight and five in the morning, when they're not made for it.</summary>
        private static bool SmallHours(Pawn p, Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (hour >= 5) return false;
            Look();
            return nightOwl == null || p.story?.traits == null || !p.story.traits.HasTrait(nightOwl);
        }

        /// <summary>Felling or harvesting a tree.</summary>
        private static bool Chopping(Verse.AI.Job job)
        {
            if (!(job.targetA.Thing is Plant plant) || plant.def.plant == null || !plant.def.plant.IsTree) return false;
            return job.def == JobDefOf.CutPlant || job.def == JobDefOf.CutPlantDesignated || job.def == JobDefOf.Harvest || job.def == JobDefOf.HarvestDesignated;
        }

        /// <summary>Water, marsh or mud underfoot (not sand or snow, which are only hard going).</summary>
        private static bool WetGround(IntVec3 c, Map map)
        {
            var terrain = c.GetTerrain(map);
            if (terrain == null) return false;
            if (terrain.IsWater) return true;
            string n = terrain.defName;
            return n.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ConditionOn(Map map, GameConditionDef def) => def != null && (map.gameConditionManager?.ConditionIsActive(def) ?? false);

        /// <summary>Midday sun on bare heads in the heat: no roof, no hat, no clouds.</summary>
        private static bool InTheSun(Pawn p, Map map, float temp)
        {
            if (temp < 30f && !ConditionOn(map, GameConditionDefOf.HeatWave)) return false;
            if (p.Position.Roofed(map)) return false;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 10 || hour > 16) return false;
            if (map.skyManager != null && map.skyManager.CurSkyGlow < 0.6f) return false;
            var weather = map.weatherManager?.curWeather;
            if (weather != null && (weather.rainRate > 0.1f || weather.snowRate > 0.1f)) return false;
            return !CoveredHead(p);
        }

        public static bool CoveredHead(Pawn p)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return false;
            foreach (var a in worn)
            {
                var groups = a.def.apparel?.bodyPartGroups;
                if (groups != null && (groups.Contains(BodyPartGroupDefOf.UpperHead) || groups.Contains(BodyPartGroupDefOf.FullHead))) return true;
            }
            return false;
        }

        private static bool RoughGround(IntVec3 c, Map map)
        {
            var terrain = c.GetTerrain(map);
            if (terrain != null && (terrain.pathCost >= 6 || terrain.IsWater)) return true;
            return map.snowGrid != null && map.snowGrid.GetDepth(c) >= 0.4f;
        }

        /// <summary>Ancient walls, caskets and junk, or the ownerless buildings of a ruin being taken apart.</summary>
        private static bool Ancient(Verse.AI.Job job)
        {
            var t = job.targetA.Thing;
            if (t == null) return false;
            if (t.def.defName.IndexOf("Ancient", StringComparison.Ordinal) >= 0) return true;
            return job.def == JobDefOf.Deconstruct && t.Faction == null && t.def.building != null && !t.def.building.isNaturalRock;
        }

        /// <summary>Asleep on the ground, a sleeping spot, a bedroll or a bed with little comfort.</summary>
        private static bool HardBed(Pawn p, Building_Bed bed)
        {
            if (bed == null) return true;
            string n = bed.def.defName;
            if (n.IndexOf("Bedroll", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("SleepingSpot", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // Medical beds are for patients; a stiff neck there would be unkind.
            if (bed.Medical) return false;
            try
            {
                return bed.GetStatValue(StatDefOf.Comfort) < 0.55f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>A wood fire (a campfire, a brazier, a hearth) burning in a small closed room.</summary>
        public static bool Smoky(Room room)
        {
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors || room.CellCount > 60) return false;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
            {
                var t = things[i];
                if (t.def.category != ThingCategory.Building) continue;
                var heat = t.def.GetCompProperties<CompProperties_HeatPusher>();
                if (heat == null || heat.heatPerSecond < 10f || t.def.GetCompProperties<CompProperties_Glower>() == null) continue;
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel != null && fuel.HasFuel && t.GetRoom() == room) return true;
            }
            return false;
        }

        private static float Cleanliness(Room room)
        {
            try
            {
                return room.GetStat(RoomStatDefOf.Cleanliness);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>Damp: dirty and under solid rock, open to the rain, or dirty in a swamp or jungle.</summary>
        private static bool Damp(Room room, IntVec3 bedCell, Map map)
        {
            float clean = Cleanliness(room);
            var roof = map.roofGrid.RoofAt(bedCell);
            if (roof != null && roof.isThickRoof && clean < -0.3f) return true;
            var weather = map.weatherManager?.curWeather;
            if (room.OpenRoofCount > 0 && weather != null && weather.rainRate > 0.1f) return true;
            return WetBiome(map) && clean < 0f;
        }

        public static int SleepingSlots(Room room)
        {
            int n = 0;
            foreach (var b in room.ContainedBeds)
                if (!b.Medical) n += b.SleepingSlotsCount;
            return n;
        }

        /// <summary>Unwashed, by Dubs Bad Hygiene's need (false without the mod).</summary>
        public static bool Unwashed(Pawn p)
        {
            Look();
            if (hygiene == null || p.needs == null) return false;
            var need = p.needs.TryGetNeed(hygiene);
            return need != null && need.CurLevelPercentage < 0.2f;
        }

        public static bool Dirty(Room room) => Cleanliness(room) < -0.5f;

        /// <summary>A room of four or more beds that is dirty, or shared while unwashed: where lice go round.</summary>
        private static bool CrowdedDirty(Pawn p, Room room)
        {
            int slots = SleepingSlots(room);
            if (slots >= 4 && Dirty(room)) return true;
            return slots >= 2 && Unwashed(p);
        }
    }
}
