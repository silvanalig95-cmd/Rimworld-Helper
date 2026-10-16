using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // ---------------- Shelter ----------------

    public class AdvisorWorker_Beds : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            var noBed = colonists.Where(p => p.ownership?.OwnedBed == null).ToList();
            var bedDef = Avail.BestBuildable(map, Avail.IsSingleBed, "Bed", "Bedroll");
            if (noBed.Count > 0)
            {
                var s = Signal(0.75f + 0.05f * noBed.Count, "CC_Adv_NoBeds_Title".Translate(noBed.Count),
                    "CC_Adv_NoBeds_Detail".Translate(TextUtil.JoinAnd(noBed.Select(p => p.LabelShort).ToList()), Avail.Indefinite(bedDef)));
                s.look = new LookTargets(noBed);
                s.suggestThing = bedDef;
                s.key = "nobed";
                yield return s;
            }
            var barracks = colonists.Where(p => p.ownership?.OwnedBed != null && p.ownership.OwnedRoom == null
                                                && !(p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false)).ToList();
            // With the materials for bedrooms in store, the room growth advice says so instead (AdvisorSignals_Rooms.cs).
            if (barracks.Count > 0 && noBed.Count == 0 && !AdvisorWorker_BarracksToBedrooms.WithinReach(map, barracks.Count, out _, out _, out _))
            {
                var s = Signal(0.18f + 0.05f * barracks.Count, "CC_Adv_Barracks_Title".Translate(barracks.Count),
                    "CC_Adv_Barracks_Detail".Translate(TextUtil.JoinAnd(barracks.Select(p => p.LabelShort).ToList())), 0.4f);
                s.look = new LookTargets(barracks);
                s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Bedroom");
                s.key = "barracks";
                yield return s;
            }
        }
    }

    // ---------------- Food ----------------

    public static class SeasonUtil
    {
        public const float GrowMin = 6f;

        /// <summary>False for vacuum maps (Odyssey space platforms): no seasons, no weather.</summary>
        public static bool HasSeasons(Map map) => map?.Biome != null && !map.Biome.inVacuum;

        public static float TwelfthTemp(Map map, Twelfth t) => GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, t);

        /// <summary>
        /// Days until the growing season ends (outdoor average below 6 °C), and how long the cold spell lasts.
        /// Returns daysUntil = -1 when the map grows all year.
        /// </summary>
        public static void Winter(Map map, out float daysUntil, out float length, out bool inWinterNow)
        {
            var now = GenLocalDate.Twelfth(map);
            int dayInTwelfth = GenLocalDate.DayOfTwelfth(map);
            inWinterNow = TwelfthTemp(map, now) < GrowMin;
            daysUntil = -1f;
            length = 0f;
            int start = -1;
            for (int i = inWinterNow ? 0 : 1; i < 12; i++)
            {
                var t = (Twelfth)(((int)now + i) % 12);
                if (TwelfthTemp(map, t) < GrowMin)
                {
                    start = i;
                    break;
                }
            }
            if (start < 0) return;
            daysUntil = start == 0 ? 0f : start * GenDate.DaysPerTwelfth - dayInTwelfth;
            int count = 0;
            for (int i = start; i < start + 12; i++)
            {
                var t = (Twelfth)(((int)now + i) % 12);
                if (TwelfthTemp(map, t) < GrowMin) count++;
                else break;
            }
            length = count * GenDate.DaysPerTwelfth - (start == 0 ? dayInTwelfth : 0);
        }

        public static float MinTempNextTwelfths(Map map, int n)
        {
            var now = GenLocalDate.Twelfth(map);
            float min = float.MaxValue;
            for (int i = 0; i <= n; i++) min = UnityEngine.Mathf.Min(min, TwelfthTemp(map, (Twelfth)(((int)now + i) % 12)));
            return min;
        }

        public static float MaxTempNextTwelfths(Map map, int n)
        {
            var now = GenLocalDate.Twelfth(map);
            float max = float.MinValue;
            for (int i = 0; i <= n; i++) max = UnityEngine.Mathf.Max(max, TwelfthTemp(map, (Twelfth)(((int)now + i) % 12)));
            return max;
        }
    }

    public class AdvisorWorker_Food : AdvisorSignalWorker
    {
        public static float DaysOfFood(Map map)
        {
            float eaters = map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;
            if (eaters <= 0) return 999f;
            return (map.resourceCounter.TotalHumanEdibleNutrition + Avail.StoredPasteNutrition(map)) / (eaters * 1.6f);
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            float days = MapFacts.Of(map).FoodDays;
            float until = -1f, length = 0f;
            bool inWinter = false;
            if (SeasonUtil.HasSeasons(map)) SeasonUtil.Winter(map, out until, out length, out inWinter);
            bool greenhouse = RoomUtil.HasGreenhouse(map, 20);
            var hydro = Avail.BestBuildable(map, Avail.IsHydroponics, "HydroponicsBasin");

            if (days < 4f)
            {
                var s = Signal(0.95f - days * 0.1f, "CC_Adv_FoodLow_Title".Translate(days.ToString("0.#")), "CC_Adv_FoodLow_Detail".Translate(), 1f);
                s.key = "low";
                yield return s;
                yield break;
            }

            if (until >= 0f && length > 0f && !greenhouse)
            {
                float needed = (inWinter ? length : length + 3f);
                if (days < needed)
                {
                    float gap = (needed - days) / needed;
                    float urgency = inWinter ? 1f : UnityEngine.Mathf.Clamp01(1f - until / 30f);
                    string extra = hydro != null ? "CC_Adv_Winter_Hydro".Translate(hydro.label) : "CC_Adv_Winter_NoHydro".Translate();
                    string title = inWinter ? "CC_Adv_WinterNow_Title".Translate(length.ToString("0")) : "CC_Adv_Winter_Title".Translate(until.ToString("0"));
                    var s = Signal(0.35f + 0.6f * gap, title,
                        "CC_Adv_Winter_Detail".Translate(days.ToString("0"), needed.ToString("0"), length.ToString("0")) + " " + extra, urgency);
                    s.key = "winter";
                    s.template = hydro != null ? DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Greenhouse") : null;
                    yield return s;
                }
            }
        }
    }

    public class AdvisorWorker_Freezer : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (RoomUtil.HasFreezer(map) || !SeasonUtil.HasSeasons(map)) yield break;
            if (SeasonUtil.MaxTempNextTwelfths(map, 1) < 4f) yield break; // the world is already a freezer
            int rotting = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (t.IsForbidden(Faction.OfPlayer) || t.Position.Fogged(map)) continue;
                var rot = t.TryGetComp<CompRottable>();
                if (rot == null || rot.PropsRot.daysToRotStart > 15f) continue;
                if (t.AmbientTemperature > 0f) rotting += t.stackCount;
            }
            if (rotting < 30) yield break;
            var cooler = Avail.BestBuildable(map, d => d.thingClass != null && typeof(Building_Cooler).IsAssignableFrom(d.thingClass), "Cooler");
            if (cooler != null)
            {
                var s = Signal(0.35f + UnityEngine.Mathf.Min(0.3f, rotting / 600f), "CC_Adv_Freezer_Title".Translate(),
                    "CC_Adv_Freezer_Detail".Translate(rotting, Avail.Indefinite(cooler)), 0.7f);
                s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Freezer");
                s.suggestThing = cooler;
                yield return s;
            }
            else
            {
                var pemmican = DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican");
                bool canPreserve = pemmican != null && DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef == pemmican && r.AvailableNow);
                yield return Signal(0.25f, "CC_Adv_Spoil_Title".Translate(),
                    canPreserve ? "CC_Adv_Spoil_Preserve".Translate(rotting, pemmican.label) : "CC_Adv_Spoil_Detail".Translate(rotting), 0.6f);
            }
        }
    }

    // ---------------- Temperature ----------------

    public class AdvisorWorker_Temperature : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!SeasonUtil.HasSeasons(map)) yield break;
            float min = SeasonUtil.MinTempNextTwelfths(map, 2);
            float max = SeasonUtil.MaxTempNextTwelfths(map, 2);
            var colonists = Colonists(map);
            if (colonists.Count == 0) yield break;
            int bedrooms = RoomUtil.ColonyRooms(map).Count(r => r.Role == RoomRoleDefOf.Bedroom || r.Role == RoomRoleDefOf.Barracks);

            if (min < -4f)
            {
                int heaters = Avail.ColonyCount(map, Avail.IsHeater);
                var heater = Avail.BestBuildable(map, Avail.IsHeater, "Heater", "Brazier", "Campfire");
                if (heater != null && heaters < UnityEngine.Mathf.Max(1, bedrooms / 2))
                {
                    var s = Signal(0.3f + UnityEngine.Mathf.Clamp01(-min / 40f) * 0.4f, "CC_Adv_Cold_Title".Translate(min.ToStringTemperature("F0")),
                        "CC_Adv_Cold_Detail".Translate(heaters, Avail.Indefinite(heater)), 0.6f);
                    s.suggestThing = heater;
                    s.key = "heat";
                    yield return s;
                }
                var cold = colonists.Where(p => p.ComfortableTemperatureRange().min > min + 10f).ToList();
                if (cold.Count > 0)
                {
                    var s = Signal(0.25f + 0.05f * cold.Count, "CC_Adv_WarmClothes_Title".Translate(cold.Count),
                        "CC_Adv_WarmClothes_Detail".Translate(TextUtil.JoinAnd(cold.Select(p => p.LabelShort).ToList()), min.ToStringTemperature("F0")), 0.6f);
                    s.look = new LookTargets(cold);
                    s.key = "clothes";
                    yield return s;
                }
            }
            if (max > 36f)
            {
                int coolers = Avail.ColonyCount(map, Avail.IsCooler);
                var cooler = Avail.BestBuildable(map, Avail.IsCooler, "Cooler", "PassiveCooler");
                if (cooler != null && coolers < UnityEngine.Mathf.Max(1, bedrooms / 2))
                {
                    var s = Signal(0.25f + UnityEngine.Mathf.Clamp01((max - 36f) / 20f) * 0.4f, "CC_Adv_Hot_Title".Translate(max.ToStringTemperature("F0")),
                        "CC_Adv_Hot_Detail".Translate(coolers, Avail.Indefinite(cooler)), 0.6f);
                    s.suggestThing = cooler;
                    s.key = "cool";
                    yield return s;
                }
            }
        }
    }

    // ---------------- Defense ----------------

    public class AdvisorWorker_Defense : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var a = DefenseUtil.Assess(map);
            float growth = CouncilMapComponent.For(map)?.WealthGrowth(15) ?? 0f;
            if (a.ratio >= 1.4f && growth < 0.3f) yield break;
            var (def, key, extra) = DefenseUtil.BestDefenseToAdd(map);
            string suggestion = def != null ? "CC_Adv_Defense_Suggest".Translate(extra, def.LabelCap.ToString()) : "CC_Adv_Defense_SuggestTrain".Translate();
            string growthNote = growth >= 0.25f ? " " + "CC_Adv_Defense_Growth".Translate(growth.ToStringPercent()) : "";
            float sev = a.ratio < 1f ? 0.35f + 0.55f * (1f - a.ratio) : growth >= 0.25f ? 0.25f : 0.12f;
            var s = Signal(sev, a.ratio < 0.7f ? "CC_Adv_Defense_TitleBad".Translate() : "CC_Adv_Defense_Title".Translate(),
                (Avail.IsPreIndustrial && a.turrets == 0 ? "CC_Adv_Defense_DetailLow" : "CC_Adv_Defense_Detail").Translate(a.threatPoints.ToString("0"), a.fighters, a.turrets, a.traps, a.cover) + growthNote + " " + suggestion,
                0.5f, 0.5f);
            s.suggestThing = def;
            yield return s;
        }
    }

    // ---------------- Health ----------------

    public class AdvisorWorker_Medical : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map);
            if (colonists.Count == 0) yield break;
            bool anyDoctor = colonists.Any(p => p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && p.workSettings.GetPriority(WorkTypeDefOf.Doctor) > 0);
            if (!anyDoctor)
                yield return Signal(0.6f, "CC_Adv_NoDoctor_Title".Translate(), "CC_Adv_NoDoctor_Detail".Translate());

            int meds = MapFacts.Of(map).Medicine;
            if (meds < colonists.Count * 2)
            {
                var healroot = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Healroot");
                bool canGrow = healroot?.plant != null && (healroot.plant.sowResearchPrerequisites == null || healroot.plant.sowResearchPrerequisites.All(r => r.IsFinished));
                var s = Signal(meds == 0 ? 0.5f : 0.3f, "CC_Adv_Medicine_Title".Translate(meds),
                    canGrow ? "CC_Adv_Medicine_Grow".Translate(healroot.label) : "CC_Adv_Medicine_Detail".Translate(), 0.5f);
                s.key = "meds";
                yield return s;
            }

            bool medicalBed = Avail.ColonyBuildings(map, d => d.IsBed).OfType<Building_Bed>().Any(b => b.Medical);
            if (!medicalBed && colonists.Count >= 3)
            {
                var s = Signal(0.28f, "CC_Adv_Hospital_Title".Translate(), "CC_Adv_Hospital_Detail".Translate(), 0.4f);
                s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Hospital");
                s.key = "hospital";
                yield return s;
            }
        }
    }

    // ---------------- Power ----------------

    public class AdvisorWorker_Power : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Avail.HasElectricity) yield break;
            var nets = map.powerNetManager.AllNetsListForReading;
            bool anyConsumer = false;
            foreach (var net in nets)
            {
                if (net.powerComps == null || net.powerComps.Count == 0) continue;
                bool hasConsumer = net.powerComps.Any(c => !(c is CompPowerPlant));
                if (!hasConsumer) continue;
                anyConsumer = true;
                float gain = net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
                float stored = net.CurrentStoredEnergy();
                var plants = net.powerComps.OfType<CompPowerPlant>().ToList();
                bool solarOnly = plants.Count > 0 && plants.All(p => p is CompPowerPlantSolar);
                var thing = net.powerComps[0].parent;
                if (gain < 0f && stored < -gain * 0.5f)
                {
                    var s = Signal(0.65f, "CC_Adv_PowerShort_Title".Translate(), "CC_Adv_PowerShort_Detail".Translate((-gain).ToString("0")), 1f);
                    s.look = new LookTargets(thing);
                    yield return s;
                }
                else if (solarOnly && (net.batteryComps == null || net.batteryComps.Count == 0))
                {
                    var s = Signal(0.3f, "CC_Adv_SolarOnly_Title".Translate(), "CC_Adv_SolarOnly_Detail".Translate(), 0.4f);
                    s.look = new LookTargets(thing);
                    yield return s;
                }
                else if (plants.Count > 0 && (net.batteryComps == null || net.batteryComps.Count == 0) && ResearchOk("Batteries"))
                {
                    var s = Signal(0.18f, "CC_Adv_NoBattery_Title".Translate(), "CC_Adv_NoBattery_Detail".Translate(), 0.3f);
                    s.look = new LookTargets(thing);
                    yield return s;
                }
            }
            if (!anyConsumer && Avail.ColonyCount(map, Avail.NeedsPower) == 0 && RoomUtil.ColonistCount(map) >= 3)
            {
                var gen = Avail.BestBuildable(map, d => d.GetCompProperties<CompProperties_Power>()?.compClass is System.Type t && typeof(CompPowerPlant).IsAssignableFrom(t),
                    "WoodFiredGenerator", "SolarGenerator", "WindTurbine");
                if (gen != null)
                    yield return Signal(0.15f, "CC_Adv_NoPower_Title".Translate(), "CC_Adv_NoPower_Detail".Translate(Avail.Indefinite(gen)), 0.2f);
            }
        }

        private static bool ResearchOk(string name)
        {
            var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
            return r == null || r.IsFinished;
        }
    }

    // ---------------- Comfort ----------------

    public class AdvisorWorker_Recreation : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map).Where(p => p.needs?.joy != null).ToList();
            if (colonists.Count == 0) yield break;
            int kinds = Avail.JoyKindsOnMap(map);
            int needed = 0;
            try
            {
                needed = ExpectationsUtility.CurrentExpectationFor(map)?.joyKindsNeeded ?? 3;
            }
            catch
            {
                needed = 3;
            }
            float avgJoy = colonists.Average(p => p.needs.joy.CurLevelPercentage);
            if (kinds >= needed && avgJoy > 0.35f) yield break;
            var present = new HashSet<JoyKindDef>(JoyUtility.JoyKindsOnMapTempList(map));
            var giver = Avail.BestBuildable(map, d => Avail.IsJoyGiver(d) && !present.Contains(d.building.joyKind), "HorseshoesPin", "ChessTable", "BilliardsTable");
            string suggestion = giver != null ? "CC_Adv_Rec_Suggest".Translate(Avail.Indefinite(giver), giver.building.joyKind.label) : "";
            var s = Signal(kinds < needed ? 0.3f + 0.05f * (needed - kinds) : 0.25f, "CC_Adv_Rec_Title".Translate(kinds, needed),
                "CC_Adv_Rec_Detail".Translate(avgJoy.ToStringPercent()) + " " + suggestion, 0.4f);
            s.suggestThing = giver;
            s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_RecRoom");
            yield return s;
        }
    }

    public class AdvisorWorker_Dining : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (RoomUtil.ColonistCount(map) < 2 || WishWorker_DiningTable.HasDiningSpot(map)) yield break;
            var table = Avail.BestBuildable(map, Avail.IsEatingTable, "Table2x2c", "Table1x2c");
            if (table == null) yield break;
            var s = Signal(0.3f, "CC_Adv_Dining_Title".Translate(), "CC_Adv_Dining_Detail".Translate(Avail.Indefinite(table)), 0.5f);
            s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_DiningRoom");
            s.suggestThing = table;
            yield return s;
        }
    }

    public class AdvisorWorker_Mood : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var risky = new List<Pawn>();
            var reasons = new List<string>();
            foreach (var p in Colonists(map))
            {
                var mood = p.needs?.mood;
                var breaker = p.mindState?.mentalBreaker;
                if (mood == null || breaker == null) continue;
                if (mood.CurLevel > breaker.BreakThresholdMinor + 0.05f) continue;
                risky.Add(p);
                var thoughts = new List<Thought>();
                mood.thoughts.GetAllMoodThoughts(thoughts);
                var worst = thoughts.Where(t => t.MoodOffset() < 0f).OrderBy(t => t.MoodOffset()).FirstOrDefault();
                reasons.Add(worst != null ? "CC_Adv_Mood_Line".Translate(p.LabelShort, worst.LabelCap, worst.MoodOffset().ToString("0")).Resolve() : p.LabelShort);
            }
            if (risky.Count == 0) yield break;
            var s = Signal(0.5f + 0.1f * risky.Count, "CC_Adv_Mood_Title".Translate(risky.Count), string.Join("\n", reasons), 0.9f);
            s.look = new LookTargets(risky);
            yield return s;
        }
    }

    // ---------------- Work ----------------

    public class AdvisorWorker_WorkCoverage : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map).Where(p => p.workSettings != null && p.workSettings.EverWork).ToList();
            if (colonists.Count == 0) yield break;
            var important = new List<WorkTypeDef> { DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking"), WorkTypeDefOf.Construction, WorkTypeDefOf.Growing, WorkTypeDefOf.Hauling, WorkTypeDefOf.Cleaning };
            if (map.mapPawns.PrisonersOfColonySpawnedCount > 0 && WorkTypeDefOf.Warden != null) important.Add(WorkTypeDefOf.Warden);
            if (RoomUtil.ColonyAnimalCount(map) >= 3) important.Add(WorkTypeDefOf.Handling);
            var missing = new List<string>();
            foreach (var wt in important)
            {
                if (wt == null) continue;
                if (!colonists.Any(p => !p.WorkTypeIsDisabled(wt) && p.workSettings.GetPriority(wt) > 0)) missing.Add(wt.gerundLabel);
            }
            if (missing.Count == 0) yield break;
            yield return Signal(0.3f + 0.1f * missing.Count, "CC_Adv_Work_Title".Translate(missing.Count), "CC_Adv_Work_Detail".Translate(TextUtil.JoinAnd(missing)), 0.8f);
        }
    }

    public class AdvisorWorker_Deterioration : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            int stacks = 0;
            Thing example = null;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.Position.Roofed(map) || t.IsInAnyStorage()) continue;
                if (!map.areaManager.Home[t.Position]) continue;
                if (t.def.category != ThingCategory.Item || !t.def.useHitPoints) continue;
                if (t.GetStatValue(StatDefOf.DeteriorationRate) <= 0f) continue;
                stacks++;
                if (example == null) example = t;
            }
            if (stacks < 8) yield break;
            var shelf = Avail.BestBuildable(map, d => d.thingClass != null && typeof(Building_Storage).IsAssignableFrom(d.thingClass), "Shelf");
            var s = Signal(0.15f + UnityEngine.Mathf.Min(0.2f, stacks / 100f), "CC_Adv_Rot_Title".Translate(stacks),
                shelf != null ? "CC_Adv_Rot_DetailShelf".Translate(shelf.label) : "CC_Adv_Rot_Detail".Translate(), 0.3f);
            s.look = example != null ? new LookTargets(example) : null;
            s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Storeroom");
            yield return s;
        }
    }

    // ---------------- Progress ----------------

    public class AdvisorWorker_Research : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!map.listerBuildings.ColonistsHaveResearchBench()) yield break;
            var current = Find.ResearchManager.GetProject();
            if (current != null) yield break;
            var next = SuggestNext(true);
            if (next == null) yield break;
            // Nothing left in the colony's own era: say so plainly rather than push it into the next one.
            string key = next.techLevel > Avail.EffectiveTech ? "CC_Adv_Research_DetailLaterEra" : "CC_Adv_Research_Detail";
            yield return Signal(0.25f, "CC_Adv_Research_Title".Translate(), key.Translate(next.LabelCap.ToString(), next.CostApparent.ToString("0")), 0.5f);
        }

        /// <summary>
        /// A sensible next project for this colony's era: something it can start now at its own tech level (so a
        /// medieval colony isn't pushed towards spaceships), cheapest first. Only with <paramref name="laterEra"/>
        /// does it fall back to a later era's project when the colony's own era is all done.
        /// </summary>
        public static ResearchProjectDef SuggestNext(bool laterEra = false)
        {
            var tech = Avail.EffectiveTech;
            var available = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(r => !r.IsFinished && !r.IsHidden && r.CanStartNow && r.knowledgeCategory == null).ToList();
            if (available.Count == 0) return null;
            var inEra = available.Where(r => r.techLevel <= tech).ToList();
            var pool = inEra.Count > 0 || !laterEra ? inEra : available;
            return pool.OrderBy(r => r.CostApparent).FirstOrDefault();
        }
    }

    /// <summary>When nothing is on fire: ideas for where the colony could head next.</summary>
    public class AdvisorWorker_Ideas : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            int colonists = RoomUtil.ColonistCount(map);
            if (colonists == 0) yield break;

            if (colonists >= 3 && !RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.RecRoom) && Avail.AnyBuildable(map, Avail.IsJoyGiver))
            {
                var s = Signal(0.1f, "CC_Idea_RecRoom_Title".Translate(), "CC_Idea_RecRoom_Detail".Translate());
                s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_RecRoom");
                s.key = "recroom";
                yield return s;
            }
            var dining = RoomUtil.ColonyRooms(map).Where(r => r.Role == RoomUtil.DiningRoom).ToList();
            if (dining.Count > 0 && dining.Max(r => RoomUtil.Impressiveness(r)) < 40f)
            {
                yield return Signal(0.09f, "CC_Idea_Dining_Title".Translate(), "CC_Idea_Dining_Detail".Translate());
            }
            if (RoomUtil.ColonyAnimalCount(map) >= 3 && !RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.Barn))
            {
                var s = Signal(0.1f, "CC_Idea_Barn_Title".Translate(), "CC_Idea_Barn_Detail".Translate());
                s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Barn");
                yield return s;
            }
            if (Avail.IsPreIndustrial && colonists >= 4)
            {
                // Only a step within the colony's own era: never an idea that pushes it into the next one.
                var next = AdvisorWorker_Research.SuggestNext();
                if (next != null)
                    yield return Signal(0.06f, "CC_Idea_Era_Title".Translate(), "CC_Idea_Era_Detail".Translate(next.LabelCap.ToString()));
            }
            var empire = Find.FactionManager.OfEmpire;
            if (ModsConfig.RoyaltyActive && empire != null && !empire.HostileTo(Faction.OfPlayer) && colonists >= 5
                && !map.mapPawns.FreeColonistsSpawned.Any(p => p.royalty?.MostSeniorTitle != null) && EngagedWithEmpire(map, empire))
            {
                yield return Signal(0.05f, "CC_Idea_Empire_Title".Translate(), "CC_Idea_Empire_Detail".Translate());
            }
        }

        /// <summary>
        /// The player has already taken a step towards the Empire: someone has earned honor, holds a title or has a
        /// psylink. Without that, titles are a late-game system the council doesn't push.
        /// </summary>
        private static bool EngagedWithEmpire(Map map, Faction empire) =>
            map.mapPawns.FreeColonists.Any(p => p.HasPsylink
                                                || (p.royalty != null && (p.royalty.GetFavor(empire) > 0 || p.royalty.HasAnyTitleIn(empire))));
    }
}
