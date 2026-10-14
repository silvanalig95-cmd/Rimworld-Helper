using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The storyteller batch, part two: the land and the seasons (a rockslide that closes a way through the hills, a warm
    // spring after a tremor, a lightning-struck tree, wild bees, a fallen star, high water, a sign at dawn for the
    // old-fashioned), and the colony's own small finds (an old map, a child's treasure, a pet's present, a sealed crate).

    // ===================== Rockslide =====================

    /// <summary>
    /// In hilly country, a slope comes down out on the land: a heap of broken rock closes a stretch of ground until it's
    /// mined away, and the fresh rock face shows a seam of ore. Never in the home area, never on anything built.
    /// </summary>
    public class HappeningWorker_Rockslide : IncidentWorker_Happening
    {
        private static bool NearRock(IntVec3 c, Map map)
        {
            foreach (var n in GenRadial.RadialCellsAround(c, 3f, false))
                if (StorytellerEvents.NaturalRock(n, map)) return true;
            return false;
        }

        private static bool Site(Map map, out IntVec3 cell) =>
            StorytellerEvents.TryOpenSpot(map, 20f, 999f, out cell, c => NearRock(c, map));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            map.TileInfo != null && map.TileInfo.hilliness >= Hilliness.SmallHills && StorytellerEvents.Adults(map).Any() && Site(map, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = StorytellerEvents.Adults(ctx.map).Where(p => !p.Position.Roofed(ctx.map)).RandomElementWithFallback() ?? StorytellerEvents.Adults(ctx.map).RandomElementWithFallback();
            var ore = StorytellerEvents.Ore(false);
            if (ctx.a == null || ore == null) return false;
            ctx.thing = ore;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            if (!Site(map, out var center)) return false;
            var ore = ctx.thing;
            var cells = GenRadial.RadialCellsAround(center, Rand.Range(2.4f, 3.6f), true).Where(c => StorytellerEvents.OpenGround(c, map)).ToList();
            if (cells.Count < 6) return false;
            // The ore shows on the side that faces the hill.
            var seam = cells.OrderByDescending(c => GenRadial.RadialCellsAround(c, 3f, false).Count(n => StorytellerEvents.NaturalRock(n, map))).Take(Rand.RangeInclusive(3, 5)).ToList();
            Thing first = null;
            foreach (var c in cells)
            {
                var t = GenSpawn.Spawn(seam.Contains(c) ? ore : ThingDefOf.CollapsedRocks, c, map, WipeMode.Vanish);
                if (seam.Contains(c) && first == null) first = t;
            }
            foreach (var c in GenRadial.RadialCellsAround(center, 5f, true))
                if (c.InBounds(map) && c.Standable(map) && Rand.Chance(0.3f)) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleRock);
            ctx.place = first;
            ctx.spot = center;
            ctx.count = cells.Count;
            ctx.placeName = StorytellerEvents.Direction(map, center);
            // {THING} is the ore by the name the map shows it under: "compacted steel", "silver", "jade".
            return true;
        }
    }

    // ===================== A warm spring =====================

    /// <summary>
    /// After a tremor in the night, steam rises from a new vent out on the land: a warm spring to soak in before
    /// electricity, a geyser for a geothermal generator after. Always on open ground, never on anything built.
    /// </summary>
    public class HappeningWorker_WarmVent : IncidentWorker_Happening
    {
        private static bool Fits(IntVec3 c, Map map)
        {
            var def = ThingDefOf.SteamGeyser;
            foreach (var cell in GenAdj.OccupiedRect(c, Rot4.North, def.size))
                if (!StorytellerEvents.OpenGround(cell, map)) return false;
            // Not crowding an old vent.
            return !map.listerThings.ThingsOfDef(def).Any(g => g.Position.DistanceTo(c) < 12f);
        }

        private static bool Spot(Map map, out IntVec3 cell) => StorytellerEvents.TryOpenSpot(map, 12f, 60f, out cell, c => Fits(c, map));

        protected override bool CanHappen(Map map, HappeningExtension ext) => ThingDefOf.SteamGeyser != null && StorytellerEvents.Adults(map).Any() && Spot(map, out _);

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Spot(ctx.map, out var cell)) return false;
            ctx.place = GenSpawn.Spawn(ThingDefOf.SteamGeyser, cell, ctx.map, WipeMode.Vanish);
            ctx.placeName = StorytellerEvents.Direction(ctx.map, cell);
            return ctx.place != null;
        }
    }

    // ===================== A lightning-struck tree =====================

    /// <summary>
    /// Lightning split an old tree near the colony, and the heartwood has dried hard and dark. The colony's best artist
    /// wants to carve from it (an inspiration), it can be cut for seasoned timber, or left standing as a landmark.
    /// </summary>
    public class HappeningWorker_StruckTree : IncidentWorker_Happening
    {
        private static IEnumerable<Plant> Trees(Map map)
        {
            var center = StorytellerEvents.Center(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(p => p.Spawned && p.def.plant != null && p.def.plant.IsTree && p.def.plant.treeCategory == TreeCategory.Full && p.Growth >= 0.9f
                            && p.def.plant.harvestedThingDef == ThingDefOf.WoodLog && p.def.plant.harvestYield >= 10f && !p.Position.Fogged(map)
                            && p.Position.DistanceTo(center) <= 70f);
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Any() && Trees(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var tree = Trees(ctx.map).RandomElementWithFallback();
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Artistic);
            if (tree == null || ctx.a == null) return false;
            ctx.place = tree;
            ctx.count = Mathf.RoundToInt(tree.def.plant.harvestYield * Rand.Range(1.2f, 1.5f));
            ctx.beast = tree.def.label;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            foreach (var c in GenRadial.RadialCellsAround(ctx.place.Position, 1.5f, true))
                if (c.InBounds(ctx.map) && c.Standable(ctx.map)) FilthMaker.TryMakeFilth(c, ctx.map, ThingDefOf.Filth_Ash);
            return true;
        }

        private static bool Standing(HappeningContext ctx) => ctx.place != null && ctx.place.Spawned && !ctx.place.Destroyed;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !Standing(ctx) ? StorytellerEvents.Gone(Find.ActiveLanguageWorker.WithDefiniteArticle(ctx.beast ?? "?")) : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && Standing(ctx))
            {
                var at = ctx.place.Position;
                var map = ctx.map;
                ctx.place.Destroy();
                // The heartwood goes to the carver; the rest is timber.
                if (option == 0) ctx.count = Mathf.RoundToInt(ctx.count * 0.6f);
                StorytellerEvents.Hand(ThingDefOf.WoodLog, ctx.count, map, at);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== Wild bees =====================

    /// <summary>
    /// A wild swarm settles in a hollow tree near the fields. The colony's best hand with animals can smoke them out for
    /// the honey (a gamble: the swarm may chase them off), or the colony leaves them be and they work its open fields,
    /// which come on a few days early. With a mod that has honey, the honey is real.
    /// </summary>
    public class HappeningWorker_WildBees : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = StorytellerEvents.Adults(ctx.map).Where(p => p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Animals).Level).FirstOrDefault() ?? StorytellerEvents.Adults(ctx.map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.thing = StorytellerEvents.Honey();
            ctx.count = Rand.RangeInclusive(12, 25);
            ctx.variant = GameCondition_Hail.Crops(ctx.map).Count;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant >= 10 ? "fields" : "nofields");

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var more = StorytellerEvents.More(def);
            if (option == 0 && !risk)
            {
                if (ctx.thing != null) StorytellerEvents.Hand(ctx.thing, ctx.count, ctx.map, VarietyUtil.Near(ctx.a, ctx.map));
                outcome = VarietyUtil.Line(more?.outcomes, ctx, new[] { ctx.thing != null ? "honey" : "comb" }) ?? outcome;
            }
            else if (option == 1 && ctx.map != null)
            {
                // The bees work the open fields all season: crops come on a few days early.
                int n = 0;
                foreach (var p in GameCondition_Hail.Crops(ctx.map).Where(p => p.Growth < 0.95f && p.def.plant != null && p.def.plant.growDays > 0.5f))
                {
                    p.Growth = Mathf.Min(1f, p.Growth + Rand.Range(1.5f, 3f) / p.def.plant.growDays);
                    ctx.map.mapDrawer.MapMeshDirty(p.Position, MapMeshFlagDefOf.Things);
                    n++;
                }
                outcome = VarietyUtil.Line(more?.outcomes, ctx, new[] { n >= 10 ? "fields" : "nofields" }) ?? outcome;
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A fallen star =====================

    /// <summary>
    /// A small star fell in the night and lies cooling out on the land: iron. The colony's best crafter can forge a blade
    /// from it (better than their usual work, if the metal holds), it can be broken up for steel, or kept as it fell.
    /// </summary>
    public class HappeningWorker_StarIron : IncidentWorker_Happening
    {
        /// <summary>What a smith of the colony's era would make of star iron: a spear before metalworking, a sword after.</summary>
        private static ThingDef Blade()
        {
            var steel = ThingDefOf.Steel?.stuffProps;
            if (steel == null) return null;
            string[] names = Avail.EffectiveTech <= TechLevel.Neolithic
                ? new[] { "MeleeWeapon_Spear", "MeleeWeapon_Knife" }
                : Rand.Bool ? new[] { "MeleeWeapon_LongSword", "MeleeWeapon_Gladius", "MeleeWeapon_Knife" } : new[] { "MeleeWeapon_Gladius", "MeleeWeapon_LongSword", "MeleeWeapon_Knife" };
            return names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.MadeFromStuff && steel.CanMake(d));
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            StorytellerEvents.Adults(map).Any() && Blade() != null && StorytellerEvents.TryOpenSpot(map, 15f, 50f, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Crafting);
            ctx.thing = Blade();
            if (ctx.a == null || ctx.thing == null) return false;
            ctx.tale = ctx.thing.label;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!StorytellerEvents.TryOpenSpot(ctx.map, 15f, 50f, out var cell)) return false;
            ctx.place = GenSpawn.Spawn(ThingDefOf.ChunkSlagSteel, cell, ctx.map);
            ctx.placeName = StorytellerEvents.Direction(ctx.map, cell);
            foreach (var c in GenRadial.RadialCellsAround(cell, 2.5f, false))
                if (c.InBounds(ctx.map) && c.Standable(ctx.map) && Rand.Chance(0.6f)) FilthMaker.TryMakeFilth(c, ctx.map, ThingDefOf.Filth_Ash);
            return ctx.place != null;
        }

        private static bool Lying(HappeningContext ctx) => ctx.place != null && ctx.place.Spawned && !ctx.place.Destroyed && ctx.place.Map == ctx.map;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !Lying(ctx) ? StorytellerEvents.Gone("CC_St_TheStar".Translate()) : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var more = StorytellerEvents.More(def);
            if (option <= 1 && Lying(ctx))
            {
                var at = ctx.place.Position;
                ctx.place.Destroy();
                var near = option == 0 && LivingEvents.AtHome(ctx.a, ctx.map) ? ctx.a.Position : at;
                if (option == 0 && !risk && ctx.thing != null)
                {
                    // Star iron and a smith's best: one step above what they'd make from ordinary steel.
                    var q = ctx.a?.skills != null ? QualityUtility.GenerateQualityCreatedByPawn(ctx.a, SkillDefOf.Crafting, false) : QualityCategory.Good;
                    if (q < QualityCategory.Legendary) q += 1;
                    var blade = ThingMaker.MakeThing(ctx.thing, ThingDefOf.Steel);
                    blade.TryGetComp<CompQuality>()?.SetQuality(q, ArtGenerationContext.Colony);
                    GenPlace.TryPlaceThing(blade, near, ctx.map, ThingPlaceMode.Near);
                    CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.StarBlade, null, null, ctx.thing.defName, ctx.thing.label, false, ctx.a);
                    outcome = VarietyUtil.Line(more?.outcomes, ctx, null, QualityUtility.GetLabel(q)) ?? outcome;
                }
                else StorytellerEvents.Hand(ThingDefOf.Steel, option == 0 ? Rand.RangeInclusive(18, 28) : Rand.RangeInclusive(35, 55), ctx.map, near);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== High water =====================

    /// <summary>
    /// Where a river runs through the land: high water in the spring melt or the autumn rains leaves driftwood all along
    /// the banks, and now and then something else caught in it.
    /// </summary>
    public class HappeningWorker_HighWater : IncidentWorker_Happening
    {
        private static Map cachedMap;
        private static int cachedDay = -1;
        private static List<IntVec3> cachedBanks;

        /// <summary>Dry, open ground right by the river, all along it (worked out once a day).</summary>
        private static List<IntVec3> Banks(Map map)
        {
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (cachedMap == map && cachedDay == day && cachedBanks != null) return cachedBanks;
            var banks = new HashSet<IntVec3>();
            foreach (var cell in map.AllCells)
            {
                if (!cell.GetTerrain(map).IsRiver) continue;
                foreach (var d in GenAdj.AdjacentCells)
                {
                    var n = cell + d;
                    if (n.InBounds(map) && !n.GetTerrain(map).IsWater && StorytellerEvents.OpenGround(n, map)) banks.Add(n);
                }
            }
            cachedMap = map;
            cachedDay = day;
            cachedBanks = banks.ToList();
            return cachedBanks;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Any() && Banks(map).Count >= 20;

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var spots = new List<IntVec3>();
            int want = Rand.RangeInclusive(5, 8);
            foreach (var c in Banks(map).InRandomOrder())
            {
                if (spots.Count >= want) break;
                if (StorytellerEvents.OpenGround(c, map) && spots.All(s => s.DistanceTo(c) >= 7f)) spots.Add(c);
            }
            if (spots.Count < 3) return false;
            int total = 0;
            foreach (var s in spots)
            {
                int n = Rand.RangeInclusive(8, 20);
                var t = StorytellerEvents.Hand(ThingDefOf.WoodLog, n, map, s);
                if (ctx.place == null) ctx.place = t;
                total += n;
            }
            ctx.count = total;
            // Sometimes something else came down with the wood.
            if (Rand.Chance(0.45f))
            {
                var things = Loot.Make(Rand.Range(100f, 260f) * LivingEvents.WealthFactor(map));
                if (things.Count > 0)
                {
                    Loot.Deliver(things, map, spots.Last(), false);
                    ctx.tale = Loot.Describe(things);
                    ctx.variant = 1;
                }
            }
            ctx.a = StorytellerEvents.Adults(map).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "snag" : "wood");
    }

    // ===================== A sign at dawn =====================

    /// <summary>
    /// Tribal and medieval colonies: at dawn a wild animal (a stag, an elk, whatever grazes these lands) stands at the
    /// edge of the fields, looking at the colony, and doesn't run. Follow it (the best hand with animals may be led to
    /// something), leave it an offering, hunt it, or let it go.
    /// </summary>
    public class HappeningWorker_OmenBeast : IncidentWorker_Happening
    {
        private static readonly string[] Favored = { "Deer", "Elk", "Caribou", "Ibex", "Gazelle", "Muffalo", "Alpaca" };

        private static IEnumerable<PawnKindDef> Kinds(Map map) =>
            PokeWorldLink.WildKinds(map.Biome.AllWildAnimals.Where(k => k?.race?.race != null && k.RaceProps.Animal && !k.RaceProps.predator && !k.RaceProps.Insect && !k.RaceProps.Dryad
                                                && k.RaceProps.baseBodySize >= 0.6f && k.RaceProps.baseBodySize <= 3f && k.race.GetStatValueAbstract(StatDefOf.Wildness) >= 0.4f
                                                && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Avail.IsPreIndustrial && StorytellerEvents.Adults(map).Any() && Kinds(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var kind = Kinds(ctx.map).RandomElementByWeightWithFallback(k => Favored.Any(f => k.defName.Contains(f)) ? 4f : 1f);
            ctx.a = StorytellerEvents.Adults(ctx.map).Where(p => p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Animals).Level).FirstOrDefault() ?? StorytellerEvents.Adults(ctx.map).RandomElementWithFallback();
            if (kind == null || ctx.a == null) return false;
            float age = kind.RaceProps.lifeExpectancy * Rand.Range(0.4f, 0.6f);
            var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: age, fixedChronologicalAge: age));
            if (animal == null) return false;
            ctx.stranger = animal;
            ctx.beast = kind.label;
            ctx.count = Rand.RangeInclusive(10, 20);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var center = StorytellerEvents.Center(map);
            if (!CellFinder.TryFindRandomCellNear(center, map, 30, c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && !map.areaManager.Home[c] && c.DistanceTo(center) >= 12f
                                                                       && c.GetFirstPawn(map) == null, out var cell)
                && !RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal)) return false;
            GenSpawn.Spawn(ctx.stranger, cell, map);
            ctx.stranger.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && !x.Spawned && !x.Discarded) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        private static bool Here(HappeningContext ctx) => ctx.stranger != null && !ctx.stranger.Dead && ctx.stranger.Spawned && ctx.stranger.Map == ctx.map;

        public override string Unavailable(HappeningContext ctx, int option) =>
            (option == 0 || option == 2) && !Here(ctx) ? StorytellerEvents.Gone(Find.ActiveLanguageWorker.WithDefiniteArticle(ctx.beast ?? "?")) : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            if (option == 0 && !risk && LivingEvents.AtHome(ctx.a, ctx.map))
            {
                // Where it led: a stand of healing herbs, or an old offering place.
                bool herbs = ThingDefOf.MedicineHerbal != null && Rand.Bool;
                if (herbs) StorytellerEvents.Hand(ThingDefOf.MedicineHerbal, Rand.RangeInclusive(5, 10), ctx.map, ctx.a.Position);
                else
                {
                    var things = Loot.Make(Rand.Range(150f, 320f) * LivingEvents.WealthFactor(ctx.map));
                    Loot.Deliver(things, ctx.map, ctx.a.Position, false);
                    ctx.tale = Loot.Describe(things);
                }
                outcome = VarietyUtil.Line(StorytellerEvents.More(def)?.outcomes, ctx, new[] { herbs ? "herbs" : "offering" }) ?? outcome;
            }
            // It goes its way soon, unless it's being hunted.
            if (option != 2 && x != null && x.Spawned && x.Faction == null && x.mindState != null)
                x.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerHour * 2;
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>A wild animal of the land: nothing to let go of.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    // ===================== An old map =====================

    /// <summary>
    /// A colonist turns up an old map among their things, from the life they had before (an outlaw's stash, an old
    /// unit's supply cache, a trader's hidden goods). Follow it up (a site on the world map, the game's own item stash
    /// quest), or keep it as a keepsake.
    /// </summary>
    public class HappeningWorker_OldMap : IncidentWorker_Happening
    {
        private static QuestScriptDef Quest => DefDatabase<QuestScriptDef>.GetNamedSilentFail("OpportunitySite_ItemStash");

        private static bool QuestPossible(Map map)
        {
            var q = Quest;
            if (q == null) return false;
            try
            {
                return q.CanRun(StorytellerUtility.DefaultSiteThreatPointsNow(), (IIncidentTarget)map ?? Find.World);
            }
            catch
            {
                return false;
            }
        }

        private static readonly Past[] Told = { Past.Outlaw, Past.Soldier, Past.Trader, Past.Noble, Past.Scholar };

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Any() && QuestPossible(map);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = adults.Where(p => Told.Any(k => Pasts.Has(p, k))).RandomElementWithFallback() ?? adults.RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.tale = StorytellerEvents.WhoTheyWere(ctx.a);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            foreach (var k in Told)
                if (Pasts.Has(ctx.a, k))
                {
                    tags.Add(Pasts.Tag(k));
                    return;
                }
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault)
            {
                Quest quest = null;
                try
                {
                    if (QuestPossible(ctx.map)) quest = QuestUtility.GenerateQuestAndMakeAvailable(Quest, StorytellerUtility.DefaultSiteThreatPointsNow());
                    if (quest != null) QuestUtility.SendLetterQuestAvailable(quest);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("old map quest", e);
                    quest = null;
                }
                // Too old: the place it shows isn't there any more.
                if (quest == null) outcome = VarietyUtil.Line(StorytellerEvents.More(def)?.outcomes, ctx, new[] { "gone" }) ?? outcome;
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A child's find =====================

    /// <summary>
    /// A child of the colony brings home something they found (a fossil, an old coin, a bird's skull, a geode, a scrap of
    /// old machinery). The colony's cleverest can study it with them, the child can keep it, or it can be traded.
    /// </summary>
    public class HappeningWorker_ChildFind : IncidentWorker_Happening
    {
        private static readonly string[] Finds = { "fossil", "coin", "skull", "geode", "machine" };

        private static IEnumerable<Pawn> Children(Map map) =>
            Colonists(map).Where(p => p.DevelopmentalStage.Child() && p.Spawned && !p.Downed && !p.InMentalState);

        protected override bool CanHappen(Map map, HappeningExtension ext) => Children(map).Any() && VarietyUtil.Best(map, SkillDefOf.Intellectual) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Children(ctx.map).RandomElementWithFallback();
            ctx.b = StorytellerEvents.Adults(ctx.map).Where(p => p.skills?.GetSkill(SkillDefOf.Intellectual) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level).FirstOrDefault();
            if (ctx.a == null || ctx.b == null) return false;
            ctx.variant = Rand.RangeInclusive(0, Avail.IsPreIndustrial ? 3 : 4);
            ctx.count = Rand.RangeInclusive(3, 9) * 5;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(Finds[Mathf.Clamp(ctx.variant, 0, Finds.Length - 1)]);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 2 && !byDefault) VarietyUtil.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A pet's present =====================

    /// <summary>
    /// A colony animal comes home with something in its mouth and drops it at its person's feet: a hare, a mouthful of
    /// healing herbs, a lost purse, a chewed component. Its person is very proud of it.
    /// </summary>
    public class HappeningWorker_PetFind : IncidentWorker_Happening
    {
        private static IEnumerable<Pawn> Pets(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed && a.RaceProps.Animal && !a.RaceProps.Insect && !a.RaceProps.Dryad
                                                         && a.RaceProps.baseBodySize <= 1.3f && (a.RaceProps.predator || a.RaceProps.petness >= 0.3f)
                                                         && LivingEvents.AtHome(PetTracker.PersonOf(a), map));

        /// <summary>Small game of the land a hunting pet could bring down.</summary>
        private static PawnKindDef Game(Map map) =>
            PokeWorldLink.WildKinds(map.Biome.AllWildAnimals.Where(k => k?.race?.race != null && k.RaceProps.Animal && !k.RaceProps.Insect && k.RaceProps.baseBodySize <= 0.35f
                                                && k.RaceProps.meatDef != null && !k.RaceProps.predator)).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pets(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.pet = Pets(ctx.map).RandomElementWithFallback();
            ctx.a = PetTracker.PersonOf(ctx.pet);
            if (ctx.pet == null || ctx.a == null) return false;
            // What it found: 0 game, 1 herbs, 2 a purse, 3 a component, 4 nothing of use.
            var options = new List<int> { 2, 4 };
            if (ctx.pet.RaceProps.predator && Game(ctx.map) != null) options.Add(0);
            if (ThingDefOf.MedicineHerbal != null) options.Add(1);
            if (!Avail.IsPreIndustrial) options.Add(3);
            ctx.variant = options.RandomElement();
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var at = ctx.pet.Position;
            switch (ctx.variant)
            {
                case 0:
                {
                    var game = Game(ctx.map);
                    var meat = game?.RaceProps.meatDef;
                    if (meat == null) return false;
                    ctx.beast = Find.ActiveLanguageWorker.WithIndefiniteArticle(game.label);
                    ctx.place = StorytellerEvents.Hand(meat, Rand.RangeInclusive(6, 12), ctx.map, at);
                    break;
                }
                case 1:
                    ctx.place = StorytellerEvents.Hand(ThingDefOf.MedicineHerbal, Rand.RangeInclusive(2, 4), ctx.map, at);
                    break;
                case 2:
                    ctx.count = Rand.RangeInclusive(3, 8) * 5;
                    ctx.place = StorytellerEvents.Hand(ThingDefOf.Silver, ctx.count, ctx.map, at);
                    break;
                case 3:
                    ctx.place = StorytellerEvents.Hand(ThingDefOf.ComponentIndustrial, 1, ctx.map, at);
                    break;
            }
            StorytellerEvents.Give(ctx.a, "Council_Evt_PetPresent", def);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            string[] what = { "game", "herbs", "purse", "component", "nothing" };
            tags.Add(what[Mathf.Clamp(ctx.variant, 0, what.Length - 1)]);
        }
    }

    // ===================== A sealed crate =====================

    /// <summary>
    /// Someone working outside digs up an old sealed crate. The colony's best crafter can work the lock open (likely to
    /// find it whole, if they're good), it can be broken open (quick, and half of it may break too), or sold sealed to a
    /// neighbor's rider for a fair price.
    /// </summary>
    public class HappeningWorker_SealedCrate : HappeningWorker_Unearthed
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Arrivals.Friendly() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.faction = Arrivals.Friendly();
            ctx.b = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Crafting);
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            return ctx.faction != null && ctx.b != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add(ctx.b == ctx.a ? "same" : "other");
        }

        /// <summary>Opening it carefully goes wrong less often the better the crafter.</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance)
        {
            if (option != 0) return base.RiskChance(ctx, option, chance);
            var r = ctx.b?.skills?.GetSkill(SkillDefOf.Crafting);
            if (r != null && !r.TotallyDisabled) chance -= r.Level * 0.035f;
            return Mathf.Clamp(chance, 0.05f, 0.95f);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && ctx.map != null)
            {
                float worth = option == 0 ? (risk ? Rand.Range(60f, 140f) : Rand.Range(350f, 700f)) : (risk ? Rand.Range(100f, 200f) : Rand.Range(300f, 600f));
                var things = Loot.Make(worth * LivingEvents.WealthFactor(ctx.map), option == 0 && !risk);
                var near = VarietyUtil.Near(option == 0 ? ctx.b : ctx.a, ctx.map);
                Loot.Deliver(things, ctx.map, near, false);
                outcome = VarietyUtil.Line(StorytellerEvents.More(def)?.outcomes, ctx, new[] { option == 0 ? "careful" : "broken", risk ? "risk" : "safe" }, Loot.Describe(things)) ?? outcome;
            }
            else if (option == 2 && !byDefault) VarietyUtil.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
