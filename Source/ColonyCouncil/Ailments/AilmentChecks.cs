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
        // Deserts and dry scrub: the game's Desert, ExtremeDesert and AridShrubland, and other mods' by name.
        private static readonly string[] AridBiomes = { "desert", "arid", "dune", "badland", "steppe", "savanna" };

        private static readonly Dictionary<BiomeDef, bool> wetBiome = new Dictionary<BiomeDef, bool>();
        private static readonly Dictionary<BiomeDef, bool> ashBiome = new Dictionary<BiomeDef, bool>();
        private static readonly Dictionary<BiomeDef, bool> aridBiome = new Dictionary<BiomeDef, bool>();

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
            aridBiome.Clear();
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

        private static long Bit(AilmentTrigger t) => 1L << (int)t;

        /// <summary>The counted triggers that fit this colonist right now, as bits (1 &lt;&lt; trigger).</summary>
        public static long Sample(Pawn p, bool awake)
        {
            long mask = 0;
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
            // The heat and cold lines move with their own comfortable range: genes, traits, race (Health/BodyMath.cs).
            BodyFit.TempShift(p, out float cold, out float heat);
            if (BodyMath.Sweltering(temp, WetBiome(map), heat)) mask |= Bit(AilmentTrigger.Sweltering);
            if (outdoors && temp >= 18f && WetBiome(map)) mask |= Bit(AilmentTrigger.Insects);
            bool arid = AridBiome(map);
            if (awake && AilmentCauseMath.Sandflies(arid, outdoors, temp, GenLocalDate.HourOfDay(map))) mask |= Bit(AilmentTrigger.Sandflies);
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
            // Eyes that see in the dark don't strain in it (Health/BodyFit.cs).
            if ((hands || work == "Research") && map.glowGrid.PsychGlowAt(p.Position) == PsychGlow.Dark && !BodyFit.SeesInDark(p)) mask |= Bit(AilmentTrigger.DarkWork);
            bool moving = p.pather != null && p.pather.Moving;
            // Long walks: out on a map away from home, or out hunting. Errands around the home don't wear out feet.
            if (moving && (!map.IsPlayerHome || work == "Hunting")) mask |= Bit(AilmentTrigger.Walking);
            if (job != null && Chopping(job)) mask |= Bit(AilmentTrigger.Chopping);
            if (job != null && StoneDust(p, job, map)) mask |= Bit(AilmentTrigger.StoneDust);
            if (job != null && (job.def == JobDefOf.Clean || job.def == JobDefOf.Sow)) mask |= Bit(AilmentTrigger.Kneeling);
            if (work == "Handling") mask |= Bit(AilmentTrigger.Handling);
            if (job != null && (BeeThing(job.targetA.Thing) || BeeThing(job.targetB.Thing))) mask |= Bit(AilmentTrigger.Beekeeping);
            bool wetGround = WetGround(p.Position, map);
            if (moving && wetGround) mask |= Bit(AilmentTrigger.WetFeet);
            if (AilmentCauseMath.ColdWet(outdoors, wetGround, temp, cold)) mask |= Bit(AilmentTrigger.ColdWetFeet);
            if (job != null && job.def == JobDefOf.GoSwimming && StillWater(p.Position, map)) mask |= Bit(AilmentTrigger.StillSwim);
            if (job != null && job.def == JobDefOf.DoBill) mask |= Bench(p, job, work, room);
            if (job != null && job.def == JobDefOf.Clean && !outdoors && Birds(room)) mask |= Bit(AilmentTrigger.BirdBarn);
            if ((moving || heavy || hands) && OldLimb(p)) mask |= Bit(AilmentTrigger.OldLimb);
            if (arid && outdoors && AilmentCauseMath.DesertDust(true, true, Digging(job, work, map), Raining(map))) mask |= Bit(AilmentTrigger.DesertDust);
            if (outdoors)
            {
                if (working && BodyMath.ColdWork(temp, cold)) mask |= Bit(AilmentTrigger.ColdWork);
                if (BodyMath.BitterCold(temp, cold)) mask |= Bit(AilmentTrigger.BitterCold);
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
        private static long Inner(Pawn p)
        {
            long mask = 0;
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

        /// <summary>
        /// Stone dust: mining rock with a mountain overhead (a thick rock roof over the cell being dug), or cutting stone
        /// blocks at a bench in a closed room of 60 cells or fewer. A big or open workshop lets the dust settle.
        /// </summary>
        private static bool StoneDust(Pawn p, Verse.AI.Job job, Map map)
        {
            if (job.def == JobDefOf.Mine)
            {
                var c = job.targetA.Cell;
                if (!c.IsValid || !c.InBounds(map)) return false;
                var roof = map.roofGrid.RoofAt(c);
                return roof != null && roof.isThickRoof;
            }
            if (job.def != JobDefOf.DoBill) return false;
            ThingDef made;
            try
            {
                made = job.bill?.recipe?.ProducedThingDef;
            }
            catch
            {
                return false;
            }
            if (made == null || !made.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)) return false;
            var room = p.GetRoom();
            return room != null && room.ProperRoom && !room.PsychologicallyOutdoors && room.CellCount <= 60;
        }

        /// <summary>An Alpha Bees beehouse or bee chamber, by name (RB_Beehouse and its kinds, the brood and hybridization chambers).</summary>
        private static bool BeeThing(Thing t)
        {
            string n = t?.def?.defName;
            if (n == null || !n.StartsWith("RB_", StringComparison.Ordinal)) return false;
            return n.IndexOf("Beehouse", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("BroodChamber", StringComparison.Ordinal) >= 0
                   || n.IndexOf("HybridizationChamber", StringComparison.Ordinal) >= 0;
        }

        // ---------------- The land, the work and the age (6 November 2026) ----------------

        public static bool AridBiome(Map map) => BiomeIs(map?.Biome, AridBiomes, aridBiome);

        /// <summary>A lake, a pond or a marsh underfoot: still water, never a river or the sea.</summary>
        private static bool StillWater(IntVec3 c, Map map)
        {
            var t = c.GetTerrain(map);
            if (t == null) return false;
            bool marsh = t.defName.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0;
            return AilmentCauseMath.StillWater(t.IsWater, t.IsRiver, t.IsOcean, marsh);
        }

        private static bool Raining(Map map)
        {
            var weather = map.weatherManager?.curWeather;
            return weather != null && weather.rainRate > 0.1f;
        }

        /// <summary>Digging the ground: building, mining with open sky overhead, sowing or clearing plants.</summary>
        private static bool Digging(Verse.AI.Job job, string work, Map map)
        {
            if (job == null) return false;
            if (work == "Construction" || work == "Growing" || work == "PlantCutting") return true;
            if (job.def != JobDefOf.Mine) return false;
            var c = job.targetA.Cell;
            return c.IsValid && c.InBounds(map) && !(map.roofGrid.RoofAt(c)?.isThickRoof ?? false);
        }

        /// <summary>
        /// A bench's bill: butchering small wild game or a bad carcass (a corpse on the bill), or smithing and smelting in a
        /// small closed room.
        /// </summary>
        private static long Bench(Pawn p, Verse.AI.Job job, string work, Room room)
        {
            long mask = 0;
            RecipeDef recipe;
            try
            {
                recipe = job.bill?.recipe;
            }
            catch
            {
                return 0;
            }
            if (recipe == null) return 0;
            bool butcher = recipe.specialProducts != null && recipe.specialProducts.Contains(SpecialProductType.Butchery);
            if (butcher)
            {
                var corpse = BillCorpse(job);
                var inner = corpse?.InnerPawn;
                if (inner?.RaceProps != null)
                {
                    bool animal = inner.RaceProps.Animal && inner.RaceProps.IsFlesh;
                    if (AilmentCauseMath.SmallGame(animal, inner.Faction == null, inner.RaceProps.baseBodySize)) mask |= Bit(AilmentTrigger.SmallGame);
                    bool rotting = corpse.GetRotStage() == RotStage.Rotting;
                    if (AilmentCauseMath.BadCarcass(animal, rotting, DiedSick(inner))) mask |= Bit(AilmentTrigger.BadCarcass);
                }
            }
            bool forge = work == "Smithing" || (job.targetA.Thing?.def?.defName?.IndexOf("Smelter", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            if (forge && room != null && AilmentCauseMath.FumeRoom(room.ProperRoom, room.PsychologicallyOutdoors, room.CellCount)) mask |= Bit(AilmentTrigger.MetalFumes);
            return mask;
        }

        /// <summary>The corpse a bill is working on: the one carried, the next in the queue, or one already on the bench.</summary>
        private static Corpse BillCorpse(Verse.AI.Job job)
        {
            if (job.targetB.Thing is Corpse b) return b;
            var queue = job.targetQueueB;
            if (queue != null)
                for (int i = 0; i < queue.Count; i++)
                    if (queue[i].Thing is Corpse q)
                        return q;
            var placed = job.placedThings;
            if (placed != null)
                for (int i = 0; i < placed.Count; i++)
                    if (placed[i]?.thing is Corpse c)
                        return c;
            return null;
        }

        /// <summary>The animal had an illness when it died: the animal flu or plague, or anything else that makes one sick.</summary>
        private static bool DiedSick(Pawn inner)
        {
            var hs = inner.health?.hediffSet?.hediffs;
            if (hs == null) return false;
            for (int i = 0; i < hs.Count; i++)
            {
                var d = hs[i]?.def;
                if (d == null || hs[i] is Hediff_Injury) continue;
                if (d.makesSickThought || d.defName.StartsWith("Animal_", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Birds kept in the room (chickens, ducks, geese, turkeys and their kind, by their body), among its first few hundred things.</summary>
        private static bool Birds(Room room)
        {
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return false;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
                if (things[i] is Pawn a && a.RaceProps != null && a.RaceProps.Animal && a.RaceProps.body?.defName == "Bird" && a.GetRoom() == room)
                    return true;
            return false;
        }

        /// <summary>A limb of the old kinds on a leg, a foot, an arm or a hand: a peg leg, a wooden foot or hand, a simple prosthetic.</summary>
        public static bool OldLimb(Pawn p)
        {
            var hs = p.health?.hediffSet?.hediffs;
            if (hs == null) return false;
            for (int i = 0; i < hs.Count; i++)
            {
                if (!(hs[i] is Hediff_AddedPart part) || part.Part == null || part.def.addedPartProps == null) continue;
                string n = part.Part.def.defName;
                if (n != "Leg" && n != "Foot" && n != "Arm" && n != "Hand" && n != "Shoulder") continue;
                var props = part.def.addedPartProps;
                if (AilmentCauseMath.LowTechLimb(props.partEfficiency, props.betterThanNatural, TechOf(part.def))) return true;
            }
            return false;
        }

        /// <summary>The tech level of what a part or implant is made of (the thing a surgeon takes out again), as a number.</summary>
        public static int TechOf(HediffDef d)
        {
            var thing = d?.spawnThingOnRemoved;
            return thing != null ? (int)thing.techLevel : 0;
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
