using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Checks built on the good-practice research: kitchens, fire, insects, the dead, crops, expectations,
    // prisoners, darkness and whatever the sky is doing right now.

    /// <summary>Food poisoning starts in the kitchen: blood from butchering, dirt, no proper floor.</summary>
    public class AdvisorWorker_Kitchen : AdvisorSignalWorker
    {
        public static bool IsButcherTable(ThingDef d) =>
            d?.AllRecipes != null && d.AllRecipes.Any(r => r.defName.StartsWith("ButcherCorpse")) && d.building != null && !d.building.isNaturalRock;

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (!RoomUtil.RoomContains(room, Avail.IsCookingStation)) continue;
                var butcher = room.ContainedAndAdjacentThings.FirstOrDefault(t => t.Faction == Faction.OfPlayer && IsButcherTable(t.def) && room.ContainsCell(t.Position));
                if (butcher != null)
                {
                    var s = Signal(0.45f, "CC_Adv_ButcherInKitchen_Title".Translate(), "CC_Adv_ButcherInKitchen_Detail".Translate(butcher.LabelShort), 0.6f);
                    s.look = new LookTargets(butcher);
                    s.key = "butcher";
                    yield return s;
                    yield break;
                }
                float clean = room.GetStat(RoomStatDefOf.Cleanliness);
                // Below -2, dirt adds to the chance of food poisoning; just above, it's close.
                if (clean < -1.5f)
                {
                    var s = Signal(clean < -2f ? 0.55f : 0.25f, "CC_Adv_DirtyKitchen_Title".Translate(), "CC_Adv_DirtyKitchen_Detail".Translate(clean.ToString("0.0")), 0.6f);
                    s.look = new LookTargets(room.Cells.First(), map);
                    s.key = "dirty";
                    yield return s;
                    yield break;
                }
            }
        }
    }

    /// <summary>Somebody has to clean, and the hospital most of all.</summary>
    public class AdvisorWorker_Hygiene : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map);
            if (colonists.Count == 0) yield break;
            var cleaning = WorkTypeDefOf.Cleaning;
            if (cleaning != null && colonists.All(p => p.WorkTypeIsDisabled(cleaning) || p.workSettings == null || p.workSettings.GetPriority(cleaning) == 0))
            {
                var s = Signal(0.4f, "CC_Adv_NoCleaner_Title".Translate(), "CC_Adv_NoCleaner_Detail".Translate(), 0.5f);
                s.key = "cleaner";
                yield return s;
            }
            foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.Hospital))
            {
                float clean = room.GetStat(RoomStatDefOf.Cleanliness);
                if (clean >= 0f) continue;
                var s = Signal(0.35f, "CC_Adv_DirtyHospital_Title".Translate(), "CC_Adv_DirtyHospital_Detail".Translate(clean.ToString("0.0")), 0.5f);
                s.look = new LookTargets(room.Cells.First(), map);
                s.key = "hospital";
                yield return s;
                yield break;
            }
        }
    }

    /// <summary>Wooden walls around a fire, and no stone where it matters.</summary>
    public class AdvisorWorker_Fire : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var walls = map.listerBuildings.allBuildingsColonist.Where(b => b.def.IsWall || (b.def.building != null && b.def.building.isPlaceOverableWall)).ToList();
            if (walls.Count < 30) yield break;
            int flammable = walls.Count(b => b.Stuff != null && b.GetStatValue(StatDefOf.Flammability) > 0.5f);
            float share = flammable / (float)walls.Count;
            // Rooms where something burns on purpose, inside flammable walls.
            int risky = 0;
            Thing example = null;
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                var fire = room.ContainedAndAdjacentThings.FirstOrDefault(t => t.Faction == Faction.OfPlayer && t.TryGetComp<CompFireOverlay>() != null && room.ContainsCell(t.Position));
                if (fire == null) continue;
                bool woodenRoom = room.BorderCells.Take(40).Count(c => c.InBounds(map) && c.GetEdifice(map) is Building b && b.Stuff != null && b.GetStatValue(StatDefOf.Flammability) > 0.5f) > 6;
                if (woodenRoom)
                {
                    risky++;
                    example = example ?? fire;
                }
            }
            if (risky == 0 && share < 0.7f) yield break;
            var stone = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff && d.stuffProps?.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Stony))
                .OrderByDescending(d => map.resourceCounter.GetCount(d)).FirstOrDefault();
            string material = stone != null && map.resourceCounter.GetCount(stone) > 50 ? stone.label : "CC_StoneGeneric".Translate().ToString();
            var s = Signal(risky > 0 ? 0.4f : 0.25f, "CC_Adv_Fire_Title".Translate(),
                (risky > 0 ? "CC_Adv_Fire_Rooms".Translate(risky) : "CC_Adv_Fire_Walls".Translate(share.ToStringPercent())) + " " + "CC_Adv_Fire_Detail".Translate(material), 0.4f);
            if (example != null) s.look = new LookTargets(example);
            yield return s;
        }
    }

    /// <summary>Overhead mountain over a warm, lived-in base: insects are coming eventually.</summary>
    public class AdvisorWorker_Infestation : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (Find.Storyteller?.difficulty != null && !Find.Storyteller.difficulty.allowBigThreats) yield break;
            int thick = 0;
            IntVec3 example = IntVec3.Invalid;
            var home = map.areaManager.Home;
            foreach (var c in home.ActiveCells)
            {
                var roof = c.GetRoof(map);
                if (roof == null || !roof.isThickRoof || c.GetEdifice(map) != null) continue;
                thick++;
                if (!example.IsValid) example = c;
            }
            if (thick < 40) yield break;
            if (map.mapTemperature.OutdoorTemp < -17f) yield break;
            var turret = Avail.BestBuildable(map, Avail.IsAutoTurret);
            string detail = "CC_Adv_Infestation_Detail".Translate(thick) + " " + (turret != null ? "CC_Adv_Infestation_Turret".Translate(turret.label) : "CC_Adv_Infestation_LowTech".Translate());
            var s = Signal(0.25f + Mathf.Min(0.2f, thick / 2000f), "CC_Adv_Infestation_Title".Translate(), detail, 0.3f, 0.7f);
            if (example.IsValid) s.look = new LookTargets(example, map);
            yield return s;
        }
    }

    /// <summary>The dead, lying where everyone can see them.</summary>
    public class AdvisorWorker_Corpses : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var home = map.areaManager.Home;
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                .Where(c => c.Spawned && c.InnerPawn != null && c.InnerPawn.RaceProps.Humanlike && home[c.Position] && !(c.ParentHolder is Building_Grave))
                .ToList();
            if (corpses.Count == 0) yield break;
            bool colonist = corpses.Any(c => c.InnerPawn.Faction == Faction.OfPlayer);
            bool rotting = corpses.Any(c => c.GetRotStage() != RotStage.Fresh);
            var grave = Avail.BestBuildable(map, Avail.IsGrave, "Grave");
            var crematorium = Avail.BestBuildable(map, d => d.AllRecipes != null && d.AllRecipes.Any(r => r.defName.Contains("Cremat")));
            string how = grave != null && crematorium != null ? "CC_Adv_Corpses_GraveOrBurn".Translate(grave.label, crematorium.label)
                : grave != null ? "CC_Adv_Corpses_Grave".Translate(grave.label) : "CC_Adv_Corpses_Dump".Translate();
            var s = Signal((colonist ? 0.45f : 0.3f) + (rotting ? 0.15f : 0f), "CC_Adv_Corpses_Title".Translate(corpses.Count),
                "CC_Adv_Corpses_Detail".Translate() + " " + how, rotting ? 0.9f : 0.6f);
            s.look = new LookTargets(corpses.Take(5).Cast<Thing>());
            s.suggestThing = grave;
            yield return s;
        }
    }

    /// <summary>Blight in the fields, and ripe crops with frost on the way.</summary>
    public class AdvisorWorker_Crops : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var zones = map.zoneManager.AllZones.OfType<Zone_Growing>().ToList();
            if (zones.Count == 0) yield break;
            var blighted = new List<Thing>();
            var ripe = new List<Thing>();
            var dm = map.designationManager;
            foreach (var z in zones)
            {
                foreach (var c in z.Cells)
                {
                    var plant = c.GetPlant(map);
                    if (plant == null) continue;
                    if (plant.Blighted) blighted.Add(plant);
                    else if (plant.HarvestableNow && plant.def.plant.harvestedThingDef != null && dm.DesignationOn(plant) == null) ripe.Add(plant);
                }
            }
            if (blighted.Count > 0)
            {
                var s = Signal(0.75f, "CC_Adv_Blight_Title".Translate(blighted.Count), "CC_Adv_Blight_Detail".Translate(), 1f);
                s.look = new LookTargets(blighted.Take(10));
                s.key = "blight";
                yield return s;
            }
            if (!SeasonUtil.HasSeasons(map) || ripe.Count < 10) yield break;
            float now = map.mapTemperature.OutdoorTemp;
            float next = SeasonUtil.MinTempNextTwelfths(map, 1);
            if (now < 4f && next < 2f)
            {
                var s = Signal(0.55f, "CC_Adv_HarvestFrost_Title".Translate(), "CC_Adv_HarvestFrost_Detail".Translate(ripe.Count, now.ToStringTemperature("F0")), 1f);
                s.look = new LookTargets(ripe.Take(10));
                s.key = "frost";
                yield return s;
            }
        }
    }

    /// <summary>Getting richer quietly raises everyone's expectations.</summary>
    public class AdvisorWorker_Expectations : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var current = ExpectationsUtility.CurrentExpectationFor(map);
            if (current == null) yield break;
            var next = DefDatabase<ExpectationDef>.AllDefsListForReading.Where(e => !e.forRoles && e.order == current.order + 1).FirstOrDefault();
            if (next == null || current.maxMapWealth <= 0f || current.maxMapWealth > 1e8f) yield break;
            float wealth = map.wealthWatcher.WealthTotal;
            float left = current.maxMapWealth - wealth;
            if (left < 0f || left > current.maxMapWealth * 0.08f) yield break;
            yield return Signal(0.2f, "CC_Adv_Expectations_Title".Translate(next.label),
                "CC_Adv_Expectations_Detail".Translate(left.ToString("N0"), next.label, next.joyKindsNeeded), 0.5f, 0.8f);
        }
    }

    /// <summary>Prisoners need a warden, a bed and food, or they'll be trouble.</summary>
    public class AdvisorWorker_Prisoners : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var prisoners = map.mapPawns.PrisonersOfColonySpawned.ToList();
            if (prisoners.Count == 0) yield break;
            var colonists = Colonists(map);
            var warden = WorkTypeDefOf.Warden;
            if (warden != null && colonists.All(p => p.WorkTypeIsDisabled(warden) || p.workSettings == null || p.workSettings.GetPriority(warden) == 0))
            {
                var s = Signal(0.6f, "CC_Adv_NoWarden_Title".Translate(), "CC_Adv_NoWarden_Detail".Translate(prisoners.Count), 0.9f);
                s.look = new LookTargets(prisoners.Cast<Thing>());
                s.key = "warden";
                yield return s;
            }
            var hungry = prisoners.Where(p => p.needs?.food != null && p.needs.food.CurLevelPercentage < 0.2f).ToList();
            if (hungry.Count > 0)
            {
                var s = Signal(0.5f, "CC_Adv_HungryPrisoner_Title".Translate(), "CC_Adv_HungryPrisoner_Detail".Translate(hungry[0].LabelShort), 0.9f);
                s.look = new LookTargets(hungry.Cast<Thing>());
                s.key = "hungry";
                yield return s;
            }
            var noBed = prisoners.Where(p => p.ownership?.OwnedBed == null && !p.InBed()).ToList();
            if (noBed.Count > 0)
            {
                var s = Signal(0.3f, "CC_Adv_PrisonerBed_Title".Translate(), "CC_Adv_PrisonerBed_Detail".Translate(), 0.5f);
                s.look = new LookTargets(noBed.Cast<Thing>());
                s.key = "bed";
                yield return s;
            }
        }
    }

    /// <summary>Work rooms and bedrooms without a single light: slow work and gloomy moods.</summary>
    public class AdvisorWorker_Darkness : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var dark = new List<Room>();
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (room.PsychologicallyOutdoors || room.CellCount < 4) continue;
                var role = room.Role;
                bool matters = role == RoomUtil.Workshop || role == RoomUtil.Kitchen || role == RoomUtil.Laboratory || role == RoomUtil.Hospital
                               || role == RoomUtil.DiningRoom || role == RoomUtil.RecRoom || role == RoomRoleDefOf.Bedroom || role == RoomRoleDefOf.Barracks;
                if (!matters || RoomUtil.RoomContains(room, Avail.IsLight)) continue;
                // Some open sky lets daylight in; that's someone else's problem at night, but not a dark room.
                if (room.OpenRoofCount > 0) continue;
                dark.Add(room);
            }
            if (dark.Count == 0) yield break;
            // Wall lamps when most of the dark rooms aren't large and the colony can build one; else a standing lamp or torch.
            var light = WallLights.ForRooms(map, dark);
            if (light == null) yield break;
            var s = Signal(0.25f + Mathf.Min(0.2f, dark.Count * 0.05f), "CC_Adv_Dark_Title".Translate(dark.Count),
                (WallLights.IsAttached(light) ? "CC_Adv_Dark_DetailWall" : "CC_Adv_Dark_Detail").Translate(Avail.Indefinite(light)), 0.4f);
            s.look = new LookTargets(dark.Take(5).Select(r => new TargetInfo(r.Cells.First(), map)));
            s.suggestThing = light;
            yield return s;
        }
    }

    /// <summary>What to do about whatever the sky (or something worse) is doing right now.</summary>
    public class AdvisorWorker_Conditions : AdvisorSignalWorker
    {
        private static readonly Dictionary<string, float> Severity = new Dictionary<string, float>
        {
            { "ToxicFallout", 0.7f },
            { "VolcanicWinter", 0.45f },
            { "SolarFlare", 0.5f },
            { "HeatWave", 0.5f },
            { "ColdSnap", 0.5f },
            { "Flashstorm", 0.35f },
            { "PsychicDrone", 0.45f },
            { "NoxiousHaze", 0.5f },
            { "Drought", 0.4f },
            { "Eclipse", 0.25f },
            { "Aurora", 0.05f },
            // Vanilla Events Expanded
            { "VEE_Drought", 0.45f },
            { "VEE_Whiteout", 0.6f },
            { "VEE_Scorch", 0.6f },
            { "VEE_LongNight", 0.6f },
            // Alpha Biomes
            { "AB_AcidRainCondition", 0.45f },
            { "AB_VolcanicHeatWave", 0.5f },
            // Seasonal Weather
            { "Earthquake", 0.6f },
            { "Wildfire", 0.7f }
        };

        private static readonly Dictionary<string, string> ConditionArticles = new Dictionary<string, string>
        {
            { "ToxicFallout", "Council_Alm_3_16_ToxicFallout" },
            { "SolarFlare", "Council_Alm_3_17_SolarFlare" },
            { "HeatWave", "Council_Alm_3_18_HeatWaveColdSnap" },
            { "ColdSnap", "Council_Alm_3_18_HeatWaveColdSnap" },
            { "Eclipse", "Council_Alm_3_19_EclipseVolcanicWinter" },
            { "VolcanicWinter", "Council_Alm_3_19_EclipseVolcanicWinter" },
            { "VEE_Drought", "Council_AlmMod_B_VEE" },
            { "VEE_Whiteout", "Council_AlmMod_B_VEE" },
            { "VEE_Scorch", "Council_AlmMod_B_VEE" },
            { "VEE_LongNight", "Council_AlmMod_B_VEE" },
            { "AB_AcidRainCondition", "Council_AlmMod_D_AlphaBiomes" },
            { "AB_VolcanicHeatWave", "Council_AlmMod_D_AlphaBiomes" },
            { "Earthquake", "Council_AlmMod_D_SeasonalWeather" },
            { "Wildfire", "Council_AlmMod_D_SeasonalWeather" }
        };

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            foreach (var cond in map.gameConditionManager.ActiveConditions)
            {
                string key = cond.def.defName;
                if (!Severity.TryGetValue(key, out float sev)) continue;
                // A solar flare means nothing to a colony without power.
                if (key == "SolarFlare" && !Avail.HasElectricity) continue;
                string titleKey = "CC_Adv_Cond_" + key + "_Title";
                string detailKey = "CC_Adv_Cond_" + key + "_Detail";
                if (!Avail.HasElectricity && (detailKey + "Low").CanTranslate()) detailKey += "Low";
                if (!titleKey.CanTranslate() || !detailKey.CanTranslate()) continue;
                int left = cond.TicksLeft;
                string time = cond.Permanent || left <= 0 ? "CC_Adv_Cond_Unknown".Translate().ToString() : TextUtil.DaysString(left);
                var s = Signal(sev, titleKey.Translate(), detailKey.Translate(time), 1f);
                s.key = key;
                if (ConditionArticles.TryGetValue(key, out string article)) s.article = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(article);
                yield return s;
            }
        }
    }
}
