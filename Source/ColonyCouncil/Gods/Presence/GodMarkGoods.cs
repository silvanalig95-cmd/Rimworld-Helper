using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a god's moment leaves where it happened, chosen as the moment begins so that its card and the colony's book can
    /// say it (GodPresence.Manifest), and laid down at the sting (GodTrails.Mark).
    /// </summary>
    public sealed class GodMarkPlan
    {
        public GodMark mark;

        /// <summary>The goods, made and waiting to be laid down: a weapon, firewood and meals, metal, components.</summary>
        public readonly List<Thing> things = new List<Thing>();

        /// <summary>The Hunt's animal, made and not yet on the map; it is laid down dead.</summary>
        public Pawn kill;

        /// <summary>The Way's standing stone, made and waiting to be set up.</summary>
        public Thing stone;

        /// <summary>A weapon's ring of scorched ground can be laid round it (open ground, not under a roof).</summary>
        public bool ring = true;

        /// <summary>What it left, in a sentence of its own; null when it leaves nothing.</summary>
        public string words;

        /// <summary>What its goods are worth together, in silver (0 for a mark that only changes the ground).</summary>
        public float worth;
    }

    /// <summary>
    /// A god's mark worth having (DESIGN.md, "A god's mark worth having"; the player, 19 November 2026: "many of them just
    /// generate a single stone [chunk]?? it's a very anticlimatic item and doesn't thematically fit for most of them, let alone
    /// it being a really not interesting or valuable item"): the goods each mark leaves, by the colony's age (Ages.Ours) and
    /// from the player's whole mod list where it has them, with the game's own to fall back on (GodMarkMath has the rules and
    /// the names): the Hunt's fresh kill of the land's game (BiomeAnimals), War's weapon of the colony's age, the Way's
    /// standing stone of the land's rock, the Hearth's firewood and warm meals, the Craft's working metal, the Machine's
    /// components. A mark of goods comes at most once in ten days (GodMarkMath.WorthGapDays); within the wait a moment leaves
    /// the mark's plain form. Each says in words exactly what it left.
    /// </summary>
    public static class GodMarkGoods
    {
        /// <summary>Dev mode: a mark of goods leaves them whenever it comes, the wait between two set aside.</summary>
        internal static bool AnyTime;

        private static ThingDef Named(string n) => n.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(n);

        /// <summary>The colony's age, 0 tribal to 3 spacer (its means: Ages.Ours).</summary>
        public static int Age => GodMarkMath.Age((int)Ages.Ours);

        // ================= The plan =================

        /// <summary>What <paramref name="mark"/> leaves at <paramref name="at"/> now, in the colony's own age.</summary>
        public static GodMarkPlan Plan(GodMark mark, Map map, IntVec3 at) => Plan(mark, map, at, Age, AnyTime);

        /// <summary>
        /// What <paramref name="mark"/> leaves at <paramref name="at"/> in <paramref name="age"/>: its goods made (within the
        /// wait, or where none can be had, its plain form), whether the ground can take it, and its words. <paramref name="anyTime"/>
        /// sets the wait aside.
        /// </summary>
        public static GodMarkPlan Plan(GodMark mark, Map map, IntVec3 at, int age, bool anyTime)
        {
            var plan = new GodMarkPlan { mark = mark };
            if (mark == GodMark.None || map == null || !at.IsValid || !at.InBounds(map))
            {
                plan.mark = GodMark.None;
                return plan;
            }
            var book = GodPresenceComponent.Instance;
            try
            {
                if (!anyTime) plan.mark = GodMarkMath.Allowed(mark, book?.DaysSinceWorth ?? 9999f);
                if (GodMarkMath.HasWorth(plan.mark) && !Goods(plan, map, at, GodMarkMath.Age(age)))
                {
                    Forget(plan);
                    plan.mark = GodMarkMath.Lesser(plan.mark);
                }
                if (!GodMarkMath.HasWorth(plan.mark)) Ground(plan, map, at);
                if (plan.worth > 0f) book?.NoteWorth();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what a mark leaves", e);
                Forget(plan);
                plan = new GodMarkPlan { mark = GodMark.None };
            }
            return plan;
        }

        /// <summary>The goods of a mark made, its worth and words set; false when none could be had (the mark's plain form then).</summary>
        private static bool Goods(GodMarkPlan plan, Map map, IntVec3 at, int age)
        {
            try
            {
                switch (plan.mark)
                {
                    case GodMark.Kill: return Kill(plan, map);
                    case GodMark.Weapon: return Weapon(plan, map, at, age);
                    case GodMark.StandingStone: return Stone(plan, map);
                    case GodMark.Hearth: return Hearth(plan, age);
                    case GodMark.Metal: return Metal(plan, age);
                    case GodMark.Components: return Components(plan, age);
                    default: return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mark's goods (" + plan.mark + ")", e);
                return false;
            }
        }

        /// <summary>A mark that only changes the ground: there when the ground there can take it, with its words.</summary>
        private static void Ground(GodMarkPlan plan, Map map, IntVec3 at)
        {
            plan.worth = 0f;
            if (plan.mark == GodMark.None || !GodTrails.CanMark(plan.mark, map, at))
            {
                plan.mark = GodMark.None;
                plan.words = null;
                return;
            }
            plan.words = Words(plan.mark, false);
        }

        /// <summary>Goods made for a plan that won't be laid down: the animal made for it is let go of properly.</summary>
        internal static void Forget(GodMarkPlan plan)
        {
            if (plan == null) return;
            Let(plan.kill);
            plan.kill = null;
            plan.stone = null;
            plan.things.Clear();
            plan.worth = 0f;
            plan.words = null;
        }

        private static string Words(GodMark m, bool alone, params NamedArgument[] args) => GodMarkMath.WordsKey(m, alone).Translate(args).Resolve();

        /// <summary>"6 fine meals", "85 steel", "an advanced component".</summary>
        private static string Counted(int n, ThingDef def) =>
            n == 1 ? Find.ActiveLanguageWorker.WithIndefiniteArticle(def.label) : n + " " + Couriers.Counted(n, def.label, def);

        /// <summary>A count of a thing in stacks of its own limit.</summary>
        private static IEnumerable<Thing> Stacks(ThingDef def, int count)
        {
            int left = count;
            while (left > 0)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Math.Min(left, Math.Max(1, def.stackLimit));
                left -= t.stackCount;
                yield return t;
            }
        }

        // ================= The Hunt: a fresh kill of the land's game =================

        /// <summary>An animal of the land that is game: flesh, not a person, not an insect, not a Pokémon, not one the colony's faith holds sacred.</summary>
        private static bool Game(PawnKindDef k)
        {
            var r = k?.race?.race;
            if (r == null || !r.Animal || r.Humanlike || !r.IsFlesh || r.meatDef == null || r.FleshType == FleshTypeDefOf.Insectoid) return false;
            return !PokeWorldLink.IsPokemon(k) && !SacredAnimals.SacredToColony(k.race);
        }

        private static bool Kill(GodMarkPlan plan, Map map)
        {
            var biome = map.Biome;
            if (biome == null) return false;
            var land = BiomeAnimals.Of(biome).Where(Game).Distinct().ToList();
            var fit = land.Where(k => GodMarkMath.KillFits(k.RaceProps.baseBodySize)).ToList();
            if (fit.Count == 0) fit = land.Where(k => GodMarkMath.KillFits(k.RaceProps.baseBodySize, true)).ToList();
            // A few of the land's game tried, the commoner first: the first within the worth, else the least worth of them.
            Pawn least = null;
            float leastWorth = float.MaxValue;
            for (int i = 0; i < 4 && fit.Count > 0; i++)
            {
                var kind = fit.RandomElementByWeight(k => Mathf.Max(0.01f, BiomeAnimals.Commonality(biome, k)));
                fit.Remove(kind);
                var p = Animal(kind, map);
                if (p == null) continue;
                float worth = GodMarkMath.KillWorth(Yield(p));
                if (worth <= 0f)
                {
                    Let(p);
                    continue;
                }
                if (worth < leastWorth)
                {
                    if (least != null) Let(least);
                    least = p;
                    leastWorth = worth;
                }
                else Let(p);
                if (GodMarkMath.KillWorthFits(worth)) break;
            }
            if (least == null) return false;
            plan.kill = least;
            plan.worth = leastWorth;
            plan.words = Words(GodMark.Kill, false, least.KindLabel.Named("ANIMAL"));
            return true;
        }

        /// <summary>An animal made for a kill and not taken, let go of properly.</summary>
        private static void Let(Pawn p)
        {
            if (p == null || p.Spawned || p.Discarded) return;
            try
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mark's kill let go", e);
            }
        }

        /// <summary>A grown animal of the kind, not yet on the map.</summary>
        private static Pawn Animal(PawnKindDef kind, Map map)
        {
            try
            {
                var stages = kind.RaceProps.lifeStageAges;
                float grown = stages != null && stages.Count > 0 ? stages[stages.Count - 1].minAge : 1f;
                float age = Mathf.Max(grown + 0.25f, kind.RaceProps.lifeExpectancy * Rand.Range(0.3f, 0.5f));
                return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true,
                    fixedBiologicalAge: age, fixedChronologicalAge: age));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the hunt's kill", e);
                return null;
            }
        }

        /// <summary>What its meat and leather are worth.</summary>
        private static float Yield(Pawn p)
        {
            var r = p.RaceProps;
            float meat = r.meatDef != null ? p.GetStatValue(StatDefOf.MeatAmount) * r.meatDef.BaseMarketValue : 0f;
            float leather = r.leatherDef != null ? p.GetStatValue(StatDefOf.LeatherAmount) * r.leatherDef.BaseMarketValue : 0f;
            return meat + leather;
        }

        /// <summary>The kill laid down where it happened: on open ground near the place, dead and fresh, a little blood beside it outside the home area.</summary>
        internal static void LayKill(GodMarkPlan plan, Map map, IntVec3 at, Color color)
        {
            var p = plan.kill;
            if (p == null || p.Spawned || p.Dead || p.Discarded) return;
            var cell = CellNear(map, at, 6f, c => c.Standable(map) && c.GetFirstPawn(map) == null && !c.Roofed(map))
                       ?? CellNear(map, at, 10f, c => c.Standable(map) && c.GetFirstPawn(map) == null);
            if (cell == null)
            {
                Forget(plan);
                return;
            }
            var blood = p.RaceProps.BloodDef;
            plan.kill = null;
            GenSpawn.Spawn(p, cell.Value, map);
            p.Kill(null);
            p.Corpse?.SetForbidden(false, false);
            if (blood != null && !map.areaManager.Home[cell.Value]) FilthMaker.TryMakeFilth(cell.Value, map, blood, 2);
            Glint(map, cell.Value, color);
        }

        // ================= War: a weapon of the colony's age =================

        private static List<ThingDef> weapons;

        /// <summary>
        /// Every weapon of the player's game a person carries in their hands, every mod's: no grenade, nothing that can't be
        /// dropped or traded, no persona weapon, nothing past the spacer age.
        /// </summary>
        private static List<ThingDef> Weapons() => weapons ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(d =>
                d != null && d.IsWeapon && d.equipmentType == EquipmentType.Primary && !d.weaponTags.NullOrEmpty()
                && !d.weaponTags.Any(t => t != null && t.IndexOf("Grenade", StringComparison.OrdinalIgnoreCase) >= 0) && d.category == ThingCategory.Item
                && !d.destroyOnDrop && d.tradeability != Tradeability.None && d.PlayerAcquirable && !d.IsStuff && d.graphicData != null
                && d.techLevel > TechLevel.Animal && d.techLevel <= TechLevel.Spacer && !d.HasComp(typeof(CompBladelinkWeapon)))
            .ToList();

        /// <summary>What a thing of this kind and material is worth at normal quality; NaN where a mod's def can't say.</summary>
        private static float Worth(ThingDef def, ThingDef stuff)
        {
            try
            {
                return def.GetStatValueAbstract(StatDefOf.MarketValue, stuff);
            }
            catch
            {
                return float.NaN;
            }
        }

        private static TechLevel TechOf(int age) =>
            age <= GodMarkMath.Tribal ? TechLevel.Neolithic : age == GodMarkMath.Medieval ? TechLevel.Medieval : age == GodMarkMath.Industrial ? TechLevel.Industrial : TechLevel.Spacer;

        /// <summary>
        /// A weapon of the colony's age within the age's worth (its material and quality counted), the commoner likelier: of
        /// the age itself first, of an earlier one only when none of its own is; where none is within the worth, the nearest
        /// to it. Its material is one the age has (GenStuff, by the age's tech level).
        /// </summary>
        private static (ThingDef def, ThingDef stuff) PickWeapon(int age)
        {
            var tech = TechOf(age);
            float target = GodMarkMath.Target(age), nearest = float.MaxValue;
            (ThingDef, ThingDef) closest = (null, null);
            for (int tier = 0; tier <= 1; tier++)
            {
                var defs = Weapons().Where(d => GodMarkMath.WeaponTier(PeopleAgesMath.AgeOfTech((int)d.techLevel), age) == tier).ToList();
                for (int i = 0; i < 24 && defs.Count > 0; i++)
                {
                    var d = defs.RandomElementByWeight(x => Mathf.Max(0.1f, x.generateCommonality));
                    defs.Remove(d);
                    ThingDef stuff = null;
                    if (d.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(d, out stuff, tech)) continue;
                    float worth = Worth(d, stuff);
                    if (GodMarkMath.WeaponFits(worth, age)) return (d, stuff);
                    float off = Mathf.Abs(worth - target);
                    if (worth > 0f && worth <= GodMarkMath.WorthMax && off < nearest)
                    {
                        nearest = off;
                        closest = (d, stuff);
                    }
                }
            }
            return closest;
        }

        private static bool Weapon(GodMarkPlan plan, Map map, IntVec3 at, int age)
        {
            var (def, stuff) = PickWeapon(age);
            if (def == null) return false;
            var t = ThingMaker.MakeThing(def, stuff);
            var q = t.TryGetComp<CompQuality>();
            if (q != null)
            {
                q.SetQuality(Rand.Chance(GodMarkMath.WeaponPoorChance) ? QualityCategory.Poor : QualityCategory.Normal, ArtGenerationContext.Outsider);
                if (q.Quality == QualityCategory.Poor && t.MarketValue < GodMarkMath.WorthMin) q.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
            }
            plan.things.Add(t);
            plan.worth = t.MarketValue;
            plan.ring = GodTrails.CanMark(GodMark.Weapon, map, at);
            plan.words = Words(GodMark.Weapon, !plan.ring, Find.ActiveLanguageWorker.WithIndefiniteArticle(GenLabel.ThingLabel(t.def, t.Stuff)).Named("WEAPON"));
            return true;
        }

        // ================= The Way: a standing stone =================

        /// <summary>The stone blocks of the land's own rock (the first rock of its kinds that is dressed into blocks), else granite blocks.</summary>
        private static ThingDef LandBlocks(Map map)
        {
            try
            {
                foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                {
                    var products = rock?.building?.mineableThing?.butcherProducts;
                    if (products == null) continue;
                    foreach (var p in products)
                        if (p?.thingDef != null && p.thingDef.IsStuff && p.thingDef.IsWithinCategory(ThingCategoryDefOf.StoneBlocks))
                            return p.thingDef;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the land's stone", e);
            }
            return Named(GodMarkMath.FallbackBlocks);
        }

        /// <summary>
        /// The standing stone (GodMarkMath.Stones): of the land's own stone, of normal quality, a work of art nobody of the
        /// colony made (the game's way with an outsider's work: no story of the colony's on it, its author unknown).
        /// </summary>
        private static bool Stone(GodMarkPlan plan, Map map)
        {
            var blocks = LandBlocks(map);
            if (blocks == null) return false;
            foreach (var name in GodMarkMath.Stones)
            {
                var def = Named(name);
                if (def == null || def.category != ThingCategory.Building || def.size != IntVec2.One || !def.MadeFromStuff || !GenStuff.AllowedStuffsFor(def).Contains(blocks))
                    continue;
                float worth = Worth(def, blocks);
                if (float.IsNaN(worth) || worth > GodMarkMath.WorthMax) continue;
                var t = ThingMaker.MakeThing(def, blocks);
                t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                var art = t.TryGetComp<CompArt>();
                if (art != null && !art.Active) art.InitializeArt(ArtGenerationContext.Outsider);
                plan.stone = t;
                plan.worth = t.MarketValue;
                plan.words = Words(GodMark.StandingStone, false, Find.ActiveLanguageWorker.WithIndefiniteArticle(def.label).Named("STONE"),
                    (blocks.stuffProps?.stuffAdjective ?? blocks.label).Named("ROCK"));
                return true;
            }
            return false;
        }

        /// <summary>The standing stone set up on open ground near the place (the colony's to keep or move); packed up beside it where no ground is free.</summary>
        internal static void Raise(GodMarkPlan plan, Map map, IntVec3 at, Color color)
        {
            var t = plan.stone;
            if (t == null || t.Spawned || t.Destroyed) return;
            plan.stone = null;
            var cell = CellNear(map, at, 5f, c => !c.Roofed(map) && Free(map, c)) ?? CellNear(map, at, 5f, c => Free(map, c));
            if (cell != null)
            {
                GenSpawn.Spawn(t, cell.Value, map, Rot4.North);
                if (t.def.CanHaveFaction) t.SetFaction(Faction.OfPlayer);
                Glint(map, cell.Value, color);
                return;
            }
            GenPlace.TryPlaceThing(t.MakeMinified(), at, map, ThingPlaceMode.Near);
        }

        /// <summary>Ground a standing stone can go on: nothing built, lying or standing there, no water, no tree, no field.</summary>
        private static bool Free(Map map, IntVec3 c) =>
            c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null && c.GetFirstPawn(map) == null && !(c.GetTerrain(map)?.IsWater ?? true)
            && !(c.GetPlant(map)?.def.plant?.IsTree ?? false) && !(c.GetZone(map) is Zone_Growing);

        // ================= The Hearth: firewood and warm meals =================

        private static bool Hearth(GodMarkPlan plan, int age)
        {
            var meal = GodMarkMath.MealsFor(age).Select(Named).Where(d => d != null && d.IsNutritionGivingIngestible && d.BaseMarketValue > 0f).RandomElementWithFallback();
            var wood = Named(GodMarkMath.Firewood);
            if (meal == null || wood == null) return false;
            int n = GodMarkMath.Meals(age, meal.BaseMarketValue, wood.BaseMarketValue);
            plan.things.AddRange(Stacks(wood, GodMarkMath.FirewoodCount));
            plan.things.AddRange(Stacks(meal, n));
            plan.worth = GodMarkMath.FirewoodCount * wood.BaseMarketValue + n * meal.BaseMarketValue;
            plan.words = Words(GodMark.Hearth, false, Counted(n, meal).Named("MEALS"));
            return true;
        }

        // ================= The Craft: the colony's working metal =================

        private static bool Metal(GodMarkPlan plan, int age)
        {
            var metal = GodMarkMath.MetalFor(age).Select(Named).FirstOrDefault(d => d != null && d.BaseMarketValue > 0f);
            if (metal == null) return false;
            int n = GodMarkMath.MetalCount(age, metal.BaseMarketValue);
            plan.things.AddRange(Stacks(metal, n));
            plan.worth = n * metal.BaseMarketValue;
            plan.words = Words(GodMark.Metal, false, Counted(n, metal).Named("METAL"));
            return true;
        }

        // ================= The Machine: components =================

        private static bool Components(GodMarkPlan plan, int age)
        {
            var plain = Named(GodMarkMath.Component);
            var advanced = Named(GodMarkMath.AdvancedComponent);
            if (plain == null) return false;
            var (a, n) = GodMarkMath.Components(age, plain.BaseMarketValue, advanced?.BaseMarketValue ?? 0f);
            string words = null;
            if (a > 0 && advanced != null)
            {
                plan.things.AddRange(Stacks(advanced, a));
                plan.worth += a * advanced.BaseMarketValue;
                words = Counted(a, advanced);
            }
            if (n > 0)
            {
                plan.things.AddRange(Stacks(plain, n));
                plan.worth += n * plain.BaseMarketValue;
                words = words == null ? Counted(n, plain) : "CC_GodP_Left_ComponentsAnd".Translate(words.Named("ADVANCED"), Counted(n, plain).Named("PARTS")).Resolve();
            }
            if (words == null) return false;
            plan.words = Words(GodMark.Components, false, words.Named("PARTS"));
            return true;
        }

        // ================= Laid down =================

        /// <summary>The goods laid on the ground at the place, each with a glint in the god's colour.</summary>
        internal static void Lay(GodMarkPlan plan, Map map, IntVec3 at, Color color)
        {
            foreach (var t in plan.things.ToList())
            {
                if (t == null || t.Spawned || t.Destroyed) continue;
                if (GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near, out var placed) && placed != null && placed.Spawned) Glint(map, placed.Position, color);
            }
            plan.things.Clear();
        }

        private static void Glint(Map map, IntVec3 c, Color color)
        {
            if (map != Find.CurrentMap) return;
            GodFx.Throw(GodFx.MoteDef, c.ToVector3Shifted(), map, 0.7f, color, 0f, 0.05f, 0f, 0.6f);
        }

        /// <summary>The nearest cell within <paramref name="radius"/> of the place that passes, or null.</summary>
        private static IntVec3? CellNear(Map map, IntVec3 at, float radius, Func<IntVec3, bool> ok)
        {
            foreach (var c in GenRadial.RadialCellsAround(at, radius, true))
                if (c.InBounds(map) && ok(c))
                    return c;
            return null;
        }
    }
}
