using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Five wishes that start from what the colony's days really hold (Defs/WishDefs/Rimfolk_Wishes_LifeNew.xml): a grave
    // for a pet whose body lies at home, a crop from home the colony could sow this spring and doesn't, a sun lamp over
    // an indoor field before the winter, a lamp at a bench someone worked in the dark, and a roof over the way between
    // a bed and the table for someone who came in soaked.

    /// <summary>
    /// A named or bonded animal has died and its body lies whole at home: its person wants it buried, not butchered.
    /// Promised (or, out of patience, by their own hand), they dig a grave near the colony and carry it in
    /// (PlayOut/PlayPetGrave.cs). Fulfilled once the body rests in a grave, whoever laid it there.
    /// </summary>
    public class WishWorker_PetGrave : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome) yield break;
            foreach (var corpse in PetGrave.Bodies(map))
            {
                var pet = corpse.InnerPawn;
                if (PetTracker.PersonOf(pet) != pawn || !PetGrave.CanBury(map, corpse)) continue;
                bool bonded = pet.relations != null && pet.relations.DirectRelationExists(PawnRelationDefOf.Bond, pawn);
                var c = Candidate(bonded ? 4f : 2.5f, (bonded ? "CC_Reason_PetGraveBonded" : "CC_Reason_PetGrave").Translate(pawn.Named("PAWN"), pet.Named("PET")));
                c.other = pet;
                c.subjectKey = pet.ThingID;
                yield return c;
            }
        }

        /// <summary>The body is gone (butchered, burned, rotted away): nothing left to bury.</summary>
        public override bool StillValid(Wish wish) => PetGrave.CorpseOf(wish.other) != null;

        public override bool IsFulfilled(Wish wish)
        {
            var corpse = PetGrave.CorpseOf(wish.other);
            if (corpse == null) return false;
            if (corpse.ParentHolder is Building_Grave) return true;
            // Promised and it fell through (danger, nobody free all day): set going again while the body still lies there.
            if (wish.promised) PetGrave.Arrange(wish, false);
            return false;
        }

        public override LookTargets LookAt(Wish wish)
        {
            var corpse = PetGrave.CorpseOf(wish.other);
            return corpse != null && corpse.Spawned ? new LookTargets(corpse) : base.LookAt(wish);
        }

        public override string OnPromised(Wish wish) => PetGrave.Arrange(wish, true) ? "CC_Promise_PetGrave" : null;

        public override string TrySelfHelp(Wish wish) => PetGrave.Arrange(wish, true) ? "CC_SelfHelp_PetGrave" : null;
    }

    /// <summary>
    /// The pet is buried: its person writes about it, and the colony's chronicle keeps a line (Rimfolk_Pool_PetBuried).
    /// The lines that say the animal was buried are said here, when it was, and no longer when it died.
    /// </summary>
    public class WishEffect_PetBuried : WishEffect
    {
        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var pet = w.other;
            var comp = CouncilGameComponent.Instance;
            if (pet == null || comp == null || p != w.Beneficiary) return;
            bool bonded = pet.relations != null && pet.relations.DirectRelationExists(PawnRelationDefOf.Bond, p);
            string size = pet.RaceProps.baseBodySize >= 1.5f ? "big" : pet.RaceProps.baseBodySize <= 0.5f ? "small" : "midsize";
            string line = Flavor.FromPool("Rimfolk_Pool_PetBuried", p, null, null, size, bonded ? "bonded" : "master");
            if (!line.NullOrEmpty()) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, line.Formatted(p.Named("PAWN"), pet.Named("PET")).Resolve(), p));
            comp.journals.WriteTagged(p, "Rimfolk_Journal_PetBuried", new[] { size }, null, pet.Named("PET"));
        }
    }

    /// <summary>
    /// Spring, and someone misses a crop from home: a fruit, a vegetable or a flower (modded crops too) the colony could
    /// sow now (researched, in season, within its growers' skill) and doesn't grow anywhere. The same person misses the
    /// same crop while the choice stays the same. Promised, a small field of it is laid out beside the others; fulfilled
    /// once a few plants of it are sown.
    /// </summary>
    public class WishWorker_HomeCrop : WishWorker
    {
        /// <summary>Plants of it sown for the wish to count as done.</summary>
        public const int SownEnough = 6;

        private const int FieldCells = 12;

        private static readonly Dictionary<Map, List<ThingDef>> crops = new Dictionary<Map, List<ThingDef>>();
        private static int cropsHour = -1;

        /// <summary>A crop from home: sown in the ground, not a tree, food people eat raw or cooked, or a flower.</summary>
        public static bool IsHomeCrop(ThingDef d)
        {
            var pp = d?.plant;
            if (pp == null || !pp.Sowable || pp.IsTree || pp.cavePlant || pp.sowTags == null || !pp.sowTags.Contains("Ground")) return false;
            if (pp.purpose == PlantPurpose.Beauty) return true;
            var food = pp.harvestedThingDef?.ingestible;
            return pp.purpose == PlantPurpose.Food && food != null && food.HumanEdible && (food.foodType & FoodTypeFlags.VegetableOrFruit) != 0;
        }

        /// <summary>What the colony grows already: its growing zones' crops and its growers'.</summary>
        private static HashSet<ThingDef> Grown(Map map)
        {
            var set = new HashSet<ThingDef>();
            foreach (var z in map.zoneManager.AllZones)
                if (z is Zone_Growing g && g.GetPlantDefToGrow() != null) set.Add(g.GetPlantDefToGrow());
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b is Building_PlantGrower pg && pg.GetPlantDefToGrow() != null) set.Add(pg.GetPlantDefToGrow());
            return set;
        }

        /// <summary>The crops from home the colony could sow here now and grows nowhere (worked out once an hour).</summary>
        public static List<ThingDef> Missing(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (hour != cropsHour)
            {
                crops.Clear();
                cropsHour = hour;
            }
            if (crops.TryGetValue(map, out var list)) return list;
            list = new List<ThingDef>();
            var grown = Grown(map);
            int bestGrower = map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                .Select(p => p.skills.GetSkill(SkillDefOf.Plants)?.Level ?? 0).DefaultIfEmpty(-1).Max();
            if (bestGrower >= 0)
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (!IsHomeCrop(d) || grown.Contains(d) || d.plant.sowMinSkill > bestGrower) continue;
                    if (d.plant.sowResearchPrerequisites != null && d.plant.sowResearchPrerequisites.Any(r => r != null && !r.IsFinished)) continue;
                    if (!PlantUtility.GrowthSeasonNow(map, d)) continue;
                    list.Add(d);
                }
            crops[map] = list;
            return list;
        }

        /// <summary>What they'd call it: the harvest for food ("potatoes"), the flowers for a flower ("roses").</summary>
        public static string CropName(ThingDef d)
        {
            if (d?.plant == null) return "";
            if (d.plant.purpose == PlantPurpose.Beauty) return Find.ActiveLanguageWorker.Pluralize(d.label, -1);
            string name = d.plant.harvestedThingDef?.label ?? d.label;
            return name.StartsWith("raw ", StringComparison.OrdinalIgnoreCase) ? name.Substring(4) : name;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || pawn.DevelopmentalStage.Baby() || GenLocalDate.Season(map) != Season.Spring) yield break;
            var list = Missing(map);
            int i = LifeNewMath.Favourite(pawn.thingIDNumber, list.Select(d => (int)d.shortHash).ToList());
            if (i < 0) yield break;
            var crop = list[i];
            var plants = pawn.skills?.GetSkill(SkillDefOf.Plants);
            var c = Candidate(plants != null && People.Passionate(plants) ? 1.2f : 0.6f,
                (crop.plant.purpose == PlantPurpose.Beauty ? "CC_Reason_HomeFlower" : "CC_Reason_HomeCrop").Translate(pawn.Named("PAWN"), CropName(crop).Named("CROP")));
            c.thingDef = crop;
            c.subjectKey = crop.defName;
            c.subjectLabel = CropName(crop);
            yield return c;
        }

        /// <summary>Plants of it standing in the colony's growing zones and growers.</summary>
        public static int Sown(Map map, ThingDef crop)
        {
            if (map == null || crop == null) return 0;
            int n = 0;
            foreach (var t in map.listerThings.ThingsOfDef(crop))
            {
                if (!(t is Plant pl) || !pl.Spawned) continue;
                if (pl.Position.GetZone(map) is Zone_Growing z && z.GetPlantDefToGrow() == crop) n++;
                else if (pl.Position.GetEdifice(map) is Building_PlantGrower g && g.GetPlantDefToGrow() == crop) n++;
            }
            return n;
        }

        public override bool IsFulfilled(Wish wish) => Sown(wish.Beneficiary?.MapHeld, wish.thingDef) >= SownEnough;

        public override float Progress(Wish wish) => Math.Min(1f, Sown(wish.Beneficiary?.MapHeld, wish.thingDef) / (float)SownEnough);

        public override string ProgressText(Wish wish) => "CC_Wish_HomeCrop_Progress".Translate(Sown(wish.Beneficiary?.MapHeld, wish.thingDef), SownEnough).Resolve();

        /// <summary>Its research undone, or it can't be sown by anyone here any more.</summary>
        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && IsHomeCrop(wish.thingDef);

        /// <summary>Promised: a small field of it laid out beside the colony's fields, on ground it grows on.</summary>
        public override string OnPromised(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null || wish.thingDef == null || Sown(map, wish.thingDef) > 0) return null;
            if (Sowing.NewGround(map, wish.thingDef, FieldCells).Count < SownEnough) return null;
            return Sowing.Sow(map, wish.thingDef, FieldCells) != null ? "CC_Promise_HomeCrop" : null;
        }

        public override LookTargets LookAt(Wish wish)
        {
            var zone = Sowing.ZoneFor(wish.Beneficiary?.MapHeld, wish.thingDef);
            return zone != null && zone.Cells.Count > 0 ? new LookTargets(new TargetInfo(zone.Cells[0], zone.Map)) : base.LookAt(wish);
        }
    }

    /// <summary>
    /// Fall or winter in a land with a real winter, with power and sun lamps researched: a grower, or someone who has
    /// gone without fresh food, wants a sun lamp over an indoor field. Fulfilled when one is lit over six or more
    /// cells of an indoor growing zone or hydroponics. Promised, a sun lamp is planned in an indoor field that has none.
    /// </summary>
    public class WishWorker_WinterGreen : WishWorker
    {
        private const int FieldCells = 6;

        /// <summary>The colony makes power right now.</summary>
        private static bool Powered(Map map) => map.powerNetManager.AllNetsListForReading.Any(n => n.CurrentEnergyGainRate() > 0f);

        /// <summary>A sun lamp lit over at least six roofed cells of a growing zone or a grower.</summary>
        public static bool LitField(Map map)
        {
            if (map == null) return false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!Avail.IsSunLamp(b.def)) continue;
                var glower = b.TryGetComp<CompGlower>();
                if (glower == null || !glower.Glows) continue;
                if (FieldUnder(map, b.Position, glower.Props.overlightRadius) >= FieldCells) return true;
            }
            return false;
        }

        /// <summary>Roofed cells of growing ground (a growing zone, a grower) within this far of a cell.</summary>
        private static int FieldUnder(Map map, IntVec3 at, float radius)
        {
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(at, Math.Max(1f, radius), true))
            {
                if (!c.InBounds(map) || !c.Roofed(map)) continue;
                if (c.GetZone(map) is Zone_Growing || c.GetEdifice(map) is Building_PlantGrower) n++;
            }
            return n;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || !SeasonUtil.HasSeasons(map)) yield break;
            SeasonUtil.Winter(map, out float until, out float length, out bool now);
            if (until < 0f || length < 10f || (!now && until > 15f)) yield break;
            if (!Powered(map) || LitField(map)) yield break;
            var lamp = Avail.BestBuildable(map, Avail.IsSunLamp, "SunLamp");
            if (lamp == null) yield break;
            // A passionate grower's greenhouse already asked for (WishWorker_PassionSpace) is the same thing.
            var comp = CouncilGameComponent.Instance;
            if (comp != null && comp.OpenWishes.Any(w => w.def.Worker is WishWorker_PassionSpace && w.subjectKey == "Plants")) yield break;
            var plants = pawn.skills?.GetSkill(SkillDefOf.Plants);
            bool grower = plants != null && !plants.TotallyDisabled && plants.Level >= 6 && (pawn.workSettings?.WorkIsActive(WorkTypeDefOf.Growing) ?? false);
            var tracker = AilmentTracker.Instance;
            bool noFresh = (AilmentTracker.DietKnown && tracker != null && pawn.needs?.food != null && !tracker.AteFreshWithin(pawn, 6f))
                           || pawn.health?.hediffSet?.hediffs.Any(h => h.def.defName == "Council_Ail_Scurvy") == true;
            if (!grower && !noFresh) yield break;
            var c = Candidate(noFresh ? 1.4f : 0.8f, (noFresh ? "CC_Reason_WinterGreenFresh" : "CC_Reason_WinterGreenGrower").Translate(pawn.Named("PAWN")));
            c.thingDef = lamp;
            c.subjectKey = noFresh ? "fresh" : "grower";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => LitField(wish.Beneficiary?.MapHeld);

        /// <summary>Promised: a sun lamp planned in the middle of an indoor field with none lit over it.</summary>
        public override string OnPromised(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var lamp = wish.thingDef;
            if (map == null || lamp == null || !Avail.CanBuild(lamp, map)) return null;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.Cells.Count))
            {
                var inside = z.Cells.Where(c => c.Roofed(map) && c.GetRoom(map) is Room r && !r.PsychologicallyOutdoors).ToList();
                if (inside.Count < FieldCells) continue;
                var mid = new IntVec3((int)inside.Average(c => c.x), 0, (int)inside.Average(c => c.z));
                foreach (var c in inside.OrderBy(c => c.DistanceToSquared(mid)))
                {
                    if (c.GetEdifice(map) != null || c.GetThingList(map).Any(t => t is Blueprint || t is Frame)) continue;
                    var stuff = lamp.MadeFromStuff ? Avail.PlainStuff(lamp, map) ?? Avail.BestStuff(lamp, map) : null;
                    if (!GenConstruct.CanPlaceBlueprintAt(lamp, c, Rot4.North, map, false, null, null, stuff).Accepted) continue;
                    GenConstruct.PlaceBlueprintForBuild(lamp, c, map, Rot4.North, Faction.OfPlayer, stuff);
                    return "CC_Promise_WinterGreen";
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Someone was seen working a bench (or researching) in the dark in the last few days (Lives/LifeNew.cs, the same
    /// hours eye strain counts): they want a lamp there. Fulfilled when the bench's work spot is lit by a lamp, day or
    /// night. Promised, or by their own hand out of patience, a standing lamp is planned beside it.
    /// </summary>
    public class WishWorker_BenchLamp : WishWorker
    {
        /// <summary>A lamp to stand by a bench: one on the floor, not a sun lamp, not darklight.</summary>
        public static bool IsBenchLamp(ThingDef d) => WallLights.IsFloorLight(d) && !Avail.IsSunLamp(d) && !WishWorker_Light.IsDarklight(d);

        public static Building BenchOf(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null || !int.TryParse(wish.subjectKey, out int id)) return null;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b.thingIDNumber == id) return b.Spawned ? b : null;
            return null;
        }

        /// <summary>The bench's work spot has a lamp's light on it (the sky left out).</summary>
        public static bool Lit(Building bench) =>
            bench != null && bench.Spawned && GlowGrid.PsychGlowAtGlow(bench.Map.glowGrid.GroundGlowAt(bench.InteractionCell, false, true)) != PsychGlow.Dark;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var bench = LifeNewTracker.DarkBench(pawn, 3f);
            if (map == null || bench == null || bench.Map != map || bench.Faction != Faction.OfPlayer || Lit(bench)) yield break;
            var lamp = Avail.BestBuildable(map, IsBenchLamp, "StandingLamp", "TorchLamp");
            if (lamp == null) yield break;
            bool research = bench is Building_ResearchBench;
            var c = Candidate(1f, (research ? "CC_Reason_BenchLampResearch" : "CC_Reason_BenchLamp").Translate(pawn.Named("PAWN"), bench.def.label.Named("BENCH")));
            c.thingDef = lamp;
            c.subjectKey = bench.thingIDNumber.ToString();
            c.subjectLabel = bench.def.label;
            yield return c;
        }

        public override bool StillValid(Wish wish) => BenchOf(wish) != null;

        public override bool IsFulfilled(Wish wish) => Lit(BenchOf(wish));

        public override LookTargets LookAt(Wish wish) => BenchOf(wish) is Building b ? new LookTargets(b) : base.LookAt(wish);

        public override string OnPromised(Wish wish) => Plan(wish, false) ? "CC_Promise_BenchLamp" : null;

        public override string TrySelfHelp(Wish wish) => Plan(wish, true) ? "CC_SelfHelp_BenchLamp" : null;

        /// <summary>
        /// A lamp planned a step or two from the bench's work spot, in the same room, where its light reaches the spot:
        /// never on a work spot, a doorway's side or in someone's way. By their own hand, plain materials only.
        /// </summary>
        private static bool Plan(Wish wish, bool ownAccord)
        {
            var bench = BenchOf(wish);
            var lamp = wish.thingDef;
            var map = bench?.Map;
            if (map == null || lamp == null || !Avail.CanBuild(lamp, map) || Lit(bench)) return false;
            if (ownAccord && !Avail.PlainWithinReach(lamp, map, out _)) return false;
            var stuff = lamp.MadeFromStuff ? Avail.PlainStuff(lamp, map) ?? (ownAccord ? null : Avail.BestStuff(lamp, map)) : null;
            if (lamp.MadeFromStuff && stuff == null) return false;
            var spot = bench.InteractionCell;
            var room = spot.GetRoom(map);
            float reach = Math.Max(1.5f, WallLights.GlowRadius(lamp) - 2f);
            var workSpots = new HashSet<IntVec3>(map.listerBuildings.allBuildingsColonist.Where(b => b.def.hasInteractionCell && b.Spawned).Select(b => b.InteractionCell));
            foreach (var c in GenRadial.RadialCellsAround(spot, Math.Min(3.9f, reach), false).OrderBy(c => c.DistanceToSquared(spot)))
            {
                if (!c.InBounds(map) || workSpots.Contains(c) || c.GetRoom(map) != room || !c.Standable(map) || c.GetEdifice(map) != null) continue;
                if (c.GetThingList(map).Any(t => t is Blueprint || t is Frame)) continue;
                bool byDoor = false;
                for (int i = 0; i < 4; i++)
                {
                    var n = c + GenAdj.CardinalDirections[i];
                    if (n.InBounds(map) && n.GetDoor(map) != null) byDoor = true;
                }
                if (byDoor) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(lamp, c, Rot4.North, map, false, null, null, stuff).Accepted) continue;
                GenConstruct.PlaceBlueprintForBuild(lamp, c, map, Rot4.North, Faction.OfPlayer, stuff);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// In a wet land, someone came in soaked to the table or their bed (the game's own soaked memory, Lives/LifeNew.cs):
    /// they want the way between their bed and the table roofed. The way is worked out once, when the wish is made (the
    /// shortest walk from the bed to the nearest dining table), and its open steps are kept with the wish; it comes up
    /// only when there are a few of them, all near enough to a wall for a roof to hold. Promised, or by their own hand,
    /// the open steps are marked for the game's own roof building. Fulfilled when every one of them is under a roof.
    /// </summary>
    public class WishWorker_RoofedWay : WishWorker
    {
        private static readonly Dictionary<Pawn, (int day, string cells)> ways = new Dictionary<Pawn, (int, string)>();

        /// <summary>A land where it rains a lot: a wet biome, or plenty of rain in the year.</summary>
        public static bool WetLand(Map map) => map != null && (AilmentChecks.WetBiome(map) || (map.TileInfo?.rainfall ?? 0f) >= 1400f);

        /// <summary>The table they'd eat at: the nearest eating table in a dining room, else the nearest anywhere.</summary>
        private static Building Table(Map map, IntVec3 from) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && Avail.IsEatingTable(b.def))
                .OrderByDescending(b => b.GetRoom()?.Role == RoomUtil.DiningRoom).ThenBy(b => b.Position.DistanceToSquared(from)).FirstOrDefault();

        /// <summary>The open steps (unroofed) of the walk from their bed to the table, as text; empty when there's none to roof.</summary>
        public static string OpenWay(Pawn p)
        {
            var map = p?.MapHeld;
            var bed = p?.ownership?.OwnedBed;
            if (map == null || bed == null || !bed.Spawned || bed.Map != map) return "";
            var table = Table(map, bed.Position);
            if (table == null) return "";
            int w = map.Size.x, h = map.Size.z;
            var path = LifeNewMath.Way(w, h, i => map.cellIndices.IndexToCell(i).Walkable(map), map.cellIndices.CellToIndex(bed.Position), map.cellIndices.CellToIndex(table.Position));
            var open = new List<(int, int)>();
            foreach (var i in path)
            {
                var c = map.cellIndices.IndexToCell(i);
                if (c == bed.Position || c == table.Position || c.Roofed(map)) continue;
                // A roof only holds near a wall, and never where roofing is ruled out.
                if (map.areaManager.NoRoof[c] || !RoofCollapseUtility.WithinRangeOfRoofHolder(c, map, false)) return "";
                open.Add((c.x, c.z));
            }
            return LifeNewMath.WorthARoof(open.Count) ? LifeNewMath.Encode(open) : "";
        }

        private static List<IntVec3> Cells(Wish wish) => LifeNewMath.Decode(wish.subjectKey).Select(c => new IntVec3(c.x, 0, c.z)).ToList();

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || !WetLand(map) || !LifeNewTracker.CameInSoaked(pawn, 3f)) yield break;
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (!ways.TryGetValue(pawn, out var known) || known.day != day)
            {
                known = (day, OpenWay(pawn));
                ways[pawn] = known;
                if (ways.Count > 60) ways.Clear();
            }
            if (known.cells.NullOrEmpty()) yield break;
            int steps = LifeNewMath.Decode(known.cells).Count;
            var c = Candidate(1.2f, "CC_Reason_RoofedWay".Translate(pawn.Named("PAWN"), steps.Named("COUNT")));
            c.subjectKey = known.cells;
            c.subjectLabel = "CC_Wish_RoofedWay_Subject".Translate().Resolve();
            c.targetValue = steps;
            yield return c;
        }

        private static int Roofed(Wish wish, out int all)
        {
            var map = wish.Beneficiary?.MapHeld;
            var cells = Cells(wish);
            all = cells.Count;
            return map == null ? 0 : cells.Count(c => c.InBounds(map) && c.Roofed(map));
        }

        public override bool StillValid(Wish wish) => Cells(wish).Count > 0;

        public override bool IsFulfilled(Wish wish) => Roofed(wish, out int all) >= all && all > 0;

        public override float Progress(Wish wish)
        {
            int done = Roofed(wish, out int all);
            return all == 0 ? -1f : done / (float)all;
        }

        public override string ProgressText(Wish wish) => "CC_Wish_RoofedWay_Progress".Translate(Roofed(wish, out int all), all).Resolve();

        public override LookTargets LookAt(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var first = Cells(wish).FirstOrDefault(c => map != null && c.InBounds(map) && !c.Roofed(map));
            return map != null && first.IsValid && first.InBounds(map) ? new LookTargets(new TargetInfo(first, map)) : base.LookAt(wish);
        }

        public override string OnPromised(Wish wish) => Mark(wish) ? "CC_Promise_RoofedWay" : null;

        public override string TrySelfHelp(Wish wish) => Mark(wish) ? "CC_SelfHelp_RoofedWay" : null;

        /// <summary>The way's open steps marked for the game's own roof building (the build roof area).</summary>
        private static bool Mark(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null) return false;
            int marked = 0;
            foreach (var c in Cells(wish))
            {
                if (!c.InBounds(map) || c.Roofed(map) || map.areaManager.NoRoof[c]) continue;
                if (!map.areaManager.BuildRoof[c]) map.areaManager.BuildRoof[c] = true;
                marked++;
            }
            return marked > 0;
        }
    }
}
