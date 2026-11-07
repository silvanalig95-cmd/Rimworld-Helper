using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Things by what they are for (DESIGN.md, "Things by what they are for"): where Rimfolk picks a drink for a toast, a
    /// gift, provisions, medicine, a carving or a blade, it asks here, and the answer comes from the live game: every item
    /// of the game and the player's mods judged by what it is (its food type and preferability, its drug and chemical,
    /// what it is made of and the beauty it gives, its comps and trade tags, the research its making needs), by the rules of
    /// <see cref="ThingRolesMath"/>. Never by a mod's name: a mod's wine, dish or statue comes in on its own. Each pick is of
    /// the giver's age (its means) or the colony's, never anything the colony would find wrong (what most of its people's
    /// faith or diet rules out), and falls back to the base game's own thing when nothing fits.
    /// </summary>
    public static class ThingRoles
    {
        private static Dictionary<ThingDef, ThingFacts> facts;
        private static Dictionary<ThingDef, int> made;
        private static Dictionary<ThingDef, List<Func<bool>>> unlocks;
        private static readonly Dictionary<int, List<ThingDef>> candidates = new Dictionary<int, List<ThingDef>>();
        private static HashSet<ThingDef> humanLeathers;
        private static HashSet<ThingDef> ofPersons;

        private static bool OfPerson(ThingDef x) => x != null && (humanLeathers.Contains(x) || x.ingestible?.sourceDef?.race?.Humanlike == true);

        /// <summary>The filter takes something, and only what is of a person (human meat, leather, a humanlike corpse).</summary>
        private static bool OnlyPersons(ThingFilter filter)
        {
            if (filter == null) return false;
            bool any = false;
            foreach (var x in filter.AllowedThingDefs)
            {
                if (!OfPerson(x)) return false;
                any = true;
            }
            return any;
        }
        private static HashSet<string> gameTags, gameWeaponTags;

        /// <summary>What Rimfolk knows of a def for the roles (worked out once: the def database doesn't change in play).</summary>
        public static ThingFacts Facts(ThingDef d)
        {
            if (d == null) return null;
            Build();
            if (facts.TryGetValue(d, out var f)) return f;
            f = Read(d);
            facts[d] = f;
            return f;
        }

        /// <summary>The def is of the role's kind.</summary>
        public static bool Fits(ThingRole role, ThingDef d) => d != null && ThingRolesMath.Fits(role, Facts(d));

        /// <summary>The def is of the role's kind and belongs to the age.</summary>
        public static bool Fits(ThingRole role, ThingDef d, WorldAge age) => d != null && ThingRolesMath.Fits(role, Facts(d), age);

        /// <summary>Every def of the role for an age, in the def database's order.</summary>
        public static List<ThingDef> Candidates(ThingRole role, WorldAge age)
        {
            Build();
            int key = (int)role * 8 + (int)age;
            if (candidates.TryGetValue(key, out var list)) return list;
            list = new List<ThingDef>();
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                try
                {
                    if (ThingRolesMath.Fits(role, Facts(d), age)) list.Add(d);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("things by what they are for: " + d.defName, e);
                }
            }
            candidates[key] = list;
            return list;
        }

        /// <summary>
        /// One thing of the role for an age: by weight (<see cref="ThingRolesMath.Weight"/>), only what <paramref name="also"/>
        /// lets in, nothing most of <paramref name="forColony"/>'s people would mind, the base game's own when nothing fits
        /// (null when that doesn't fit either). A <paramref name="seed"/> other than 0 picks the same each time it's asked
        /// (what an answer's words name and what it then makes).
        /// </summary>
        public static ThingDef Pick(ThingRole role, WorldAge age, bool empire = false, Map forColony = null, int seed = 0, Predicate<ThingDef> also = null)
        {
            try
            {
                var list = new List<ThingDef>();
                var weights = new List<float>();
                foreach (var d in Candidates(role, age))
                {
                    if (also != null && !also(d)) continue;
                    if (forColony != null && ColonyMinds(d, forColony)) continue;
                    list.Add(d);
                    weights.Add(ThingRolesMath.Weight(role, Facts(d), age, empire));
                }
                float roll;
                if (seed != 0)
                {
                    Rand.PushState(seed);
                    roll = Rand.Value;
                    Rand.PopState();
                }
                else roll = Rand.Value;
                int i = ThingRolesMath.PickIndex(weights, roll);
                if (i >= 0) return list[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("things by what they are for: " + role, e);
            }
            var fb = Fallback(role, age);
            return fb != null && (also == null || also(fb)) ? fb : null;
        }

        /// <summary>One thing of the role from a people, by its ways (its age, the Empire's own), for the colony's map.</summary>
        public static ThingDef From(ThingRole role, Faction giver, Map forColony = null, int seed = 0, Predicate<ThingDef> also = null) =>
            Pick(role, Ages.Of(giver), Ages.IsEmpire(giver), forColony, seed, also);

        /// <summary>One thing of the role of the colony's own age (its means), for its map.</summary>
        public static ThingDef Ours(ThingRole role, Map map, int seed = 0, Predicate<ThingDef> also = null) => Pick(role, Ages.Of(map), false, map, seed, also);

        /// <summary>The base game's own thing for the role and age, as Rimfolk had it before (null when the game lacks it).</summary>
        public static ThingDef Fallback(ThingRole role, WorldAge age)
        {
            ThingDef Named(string n) => DefDatabase<ThingDef>.GetNamedSilentFail(n);
            switch (role)
            {
                case ThingRole.Drink: return Named("Beer");
                case ThingRole.SoftDrink: return null;
                case ThingRole.Feast: return ThingDefOf.MealFine;
                case ThingRole.Provisions: return age >= WorldAge.Industrial ? ThingDefOf.MealSurvivalPack ?? ThingDefOf.Pemmican : ThingDefOf.Pemmican;
                case ThingRole.Produce: return ThingDefOf.RawPotatoes;
                case ThingRole.Medicine: return age <= WorldAge.Medieval ? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineIndustrial;
                case ThingRole.Precious:
                case ThingRole.Gift: return age == WorldAge.Tribal ? ThingDefOf.Jade : age == WorldAge.Medieval ? ThingDefOf.Gold : ThingDefOf.Silver;
                case ThingRole.SmallArt:
                case ThingRole.Carving: return Named("SculptureSmall");
                case ThingRole.Blade: return age == WorldAge.Tribal ? Named("MeleeWeapon_Spear") : Named("MeleeWeapon_LongSword");
            }
            return null;
        }

        /// <summary>How many of a thing make up a worth in silver (at least <paramref name="min"/>): the game's balance kept whatever is picked.</summary>
        public static int CountFor(float worth, ThingDef d, int min = 1) => ThingRolesMath.CountFor(worth, d?.BaseMarketValue ?? 0f, min);

        /// <summary>A seed for a pick that must stay the same between an answer's words and what it does: from the things it's about.</summary>
        public static int Seed(params object[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (var p in parts)
                {
                    int x;
                    switch (p)
                    {
                        case null: x = 0; break;
                        case Thing t: x = t.thingIDNumber; break;
                        case Faction f: x = f.loadID * 7919; break;
                        case Def def: x = GenText.StableStringHash(def.defName); break;
                        case string s: x = GenText.StableStringHash(s); break;
                        case Map m: x = m.uniqueID * 104729; break;
                        default: x = p.GetHashCode(); break;
                    }
                    h = h * 31 + x;
                }
                return h == 0 ? 1 : h;
            }
        }

        /// <summary>A seed for a dilemma's or an event's context: its def, the people in it and the home.</summary>
        public static int Seed(DilemmaContext ctx, string what) => ctx == null ? 0 : Seed(what, ctx.def, ctx.a, ctx.b, ctx.stranger, ctx.faction, ctx.map);

        // ---------------- What the colony would mind, and what it can make ----------------

        private static readonly Dictionary<long, bool> minds = new Dictionary<long, bool>();
        private static int mindsHour = -1;

        /// <summary>
        /// Most of the colony's grown people at home would mind having it: their faith or diet rules it out (alcohol for
        /// a dry faith, meat for vegetarians), or it's a drug they may not have (Wishes/Beliefs.cs). Worked out once an hour.
        /// </summary>
        public static bool ColonyMinds(ThingDef d, Map map)
        {
            if (d == null || map == null) return false;
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            if (hour != mindsHour)
            {
                minds.Clear();
                mindsHour = hour;
            }
            long key = (long)map.uniqueID << 32 | (uint)d.shortHash;
            if (minds.TryGetValue(key, out bool m)) return m;
            int all = 0, mind = 0;
            try
            {
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p.DevelopmentalStage.Juvenile()) continue;
                    all++;
                    if (Beliefs.AgainstThing(p, d)) mind++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("things by what they are for: the colony's beliefs", e);
            }
            m = all > 0 && mind * 2 > all;
            minds[key] = m;
            return m;
        }

        /// <summary>The colony could make or grow it now: a recipe, or a crop, whose research is done (or needs none).</summary>
        public static bool ColonyCanMake(ThingDef d)
        {
            if (d == null) return false;
            Build();
            if (!unlocks.TryGetValue(d, out var ways)) return false;
            foreach (var w in ways)
            {
                try
                {
                    if (w()) return true;
                }
                catch
                {
                    // A recipe a mod changed beyond reading: not counted.
                }
            }
            return false;
        }

        // ---------------- Reading the defs ----------------

        private static void Build()
        {
            if (facts != null) return;
            facts = new Dictionary<ThingDef, ThingFacts>();
            made = new Dictionary<ThingDef, int>();
            unlocks = new Dictionary<ThingDef, List<Func<bool>>>();
            humanLeathers = new HashSet<ThingDef>();
            ofPersons = new HashSet<ThingDef>();
            gameTags = new HashSet<string>();
            gameWeaponTags = new HashSet<string>();
            int Tech(ResearchProjectDef r) => r == null ? 0 : ThingRolesMath.IndexOf(ThingRolesMath.Techs, r.techLevel.ToString());
            void Way(ThingDef d, int tech, Func<bool> open)
            {
                if (d == null) return;
                made[d] = made.TryGetValue(d, out int had) ? Math.Min(had, tech) : tech;
                if (!unlocks.TryGetValue(d, out var l)) unlocks[d] = l = new List<Func<bool>>();
                l.Add(open);
            }
            try
            {
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                    if (d.race != null && d.race.Humanlike && d.race.leatherDef != null) humanLeathers.Add(d.race.leatherDef);
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (r?.products == null || r.products.Count == 0) continue;
                    // Made from people (a cannibal platter from human meat): whatever it makes is of a person too.
                    if (r.ingredients != null && r.ingredients.Any(i => OnlyPersons(i?.filter)))
                        foreach (var p in r.products)
                            if (p?.thingDef != null) ofPersons.Add(p.thingDef);
                    int tech = Tech(r.researchPrerequisite);
                    if (r.researchPrerequisites != null)
                        foreach (var x in r.researchPrerequisites)
                            tech = Math.Max(tech, Tech(x));
                    var recipe = r;
                    foreach (var p in r.products) Way(p?.thingDef, tech, () => recipe.AvailableNow);
                }
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (d.modContentPack != null && d.modContentPack.IsOfficialMod)
                    {
                        if (d.tradeTags != null) gameTags.UnionWith(d.tradeTags);
                        if (d.weaponTags != null) gameWeaponTags.UnionWith(d.weaponTags);
                    }
                    var plant = d.plant;
                    if (plant == null || !plant.Sowable || plant.harvestedThingDef == null) continue;
                    int tech = 0;
                    var need = plant.sowResearchPrerequisites;
                    if (need != null)
                        foreach (var x in need)
                            tech = Math.Max(tech, Tech(x));
                    Way(plant.harvestedThingDef, tech, () => need == null || need.All(x => x == null || x.IsFinished));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("things by what they are for: reading the recipes", e);
            }
        }

        private static ThingCategoryDef meals;
        private static ThingCategoryDef Meals => meals ??= DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");

        private static bool Has(List<StuffCategoryDef> cats, StuffCategoryDef c) => cats != null && c != null && cats.Contains(c);

        private static ThingFacts Read(ThingDef d)
        {
            var f = new ThingFacts
            {
                defName = d.defName,
                item = d.category == ThingCategory.Item,
                building = d.category == ThingCategory.Building,
                weapon = d.IsWeapon,
                tech = ThingRolesMath.IndexOf(ThingRolesMath.Techs, d.techLevel.ToString()),
                madeTech = made.TryGetValue(d, out int mt) ? mt : -1,
                value = d.BaseMarketValue,
                stack = d.stackLimit,
                untradeable = d.tradeability == Tradeability.None || d.destroyOnDrop || d.IsCorpse || d.isUnfinishedThing,
                humanlike = humanLeathers.Contains(d) || d.IsCorpse || d.ingestible?.sourceDef?.race?.Humanlike == true
                    || ofPersons.Contains(d) || ThingRolesMath.PersonName(d.defName, d.ingestible != null)
            };
            ChemicalDef chemical = null;
            if (d.comps != null)
                foreach (var c in d.comps)
                {
                    if (c == null) continue;
                    string n = c.GetType().Name, cc = c.compClass?.Name;
                    if (ThingRolesMath.SpecialComp(n) || ThingRolesMath.SpecialComp(cc)) f.special = true;
                    if ((n + cc).IndexOf("Corpse", StringComparison.Ordinal) >= 0) f.holdsDead = true;
                    if (cc == nameof(CompArt) || c is CompProperties_Art) f.art = true;
                    if (c is CompProperties_Rottable rot) f.rotDays = rot.daysToRotStart;
                    if (c is CompProperties_Drug drug)
                    {
                        f.soft = drug.teetotalerCanConsume;
                        chemical = drug.chemical;
                    }
                    if ((n + cc).IndexOf("Bladelink", StringComparison.Ordinal) >= 0 || (n + cc).IndexOf("Biocod", StringComparison.Ordinal) >= 0) f.bonded = true;
                }
            if (d.tradeTags != null)
            {
                if (d.tradeTags.Any(t => ThingRolesMath.ModTag(t, gameTags))) f.special = true;
                if (d.tradeTags.Any(ThingRolesMath.SpecialTags.Contains)) f.exotic = true;
            }
            f.art = f.art && d.IsArt;
            var ing = d.ingestible;
            if (ing != null)
            {
                f.drug = d.IsDrug;
                string cat = ing.drugCategory.ToString();
                f.drugKind = cat == "Medical" ? 1 : cat == "Social" ? 2 : cat == "Hard" ? 3 : 0;
                f.alcohol = ThingRolesMath.Alcoholic(chemical != null && chemical == ChemicalDefOf.Alcohol, chemical == ChemicalDefOf.Alcohol ? null : chemical?.defName,
                    (ing.foodType & FoodTypeFlags.Liquor) != 0, f.soft);
                f.fluid = (ing.foodType & (FoodTypeFlags.Fluid | FoodTypeFlags.Liquor)) != 0;
                f.meal = (ing.foodType & FoodTypeFlags.Meal) != 0;
                f.vegetable = (ing.foodType & FoodTypeFlags.VegetableOrFruit) != 0;
                f.humanEdible = ing.HumanEdible;
                f.withMeals = Meals != null && d.thingCategories != null && d.IsWithinCategory(Meals);
                f.preferability = ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, ing.preferability.ToString());
                f.nutrition = d.statBases?.GetStatValueFromList(StatDefOf.Nutrition, 0f) ?? 0f;
            }
            if (d.IsMedicine) f.potency = d.statBases.GetStatValueFromList(StatDefOf.MedicalPotency, 0f);
            var cats = d.stuffProps?.categories;
            if (cats != null)
            {
                f.metallic = Has(cats, StuffCategoryDefOf.Metallic);
                f.stony = Has(cats, StuffCategoryDefOf.Stony);
                f.otherStuff = cats.Any(c => c != StuffCategoryDefOf.Metallic && c != StuffCategoryDefOf.Stony);
                f.beauty = d.stuffProps.statFactors?.GetStatFactorFromList(StatDefOf.Beauty) ?? 1f;
            }
            f.ofWood = Has(d.stuffCategories, StuffCategoryDefOf.Woody);
            f.ofStone = Has(d.stuffCategories, StuffCategoryDefOf.Stony);
            f.ofMetal = Has(d.stuffCategories, StuffCategoryDefOf.Metallic);
            f.minified = d.Minifiable;
            f.terror = d.statBases != null && d.statBases.Any(s => s?.stat?.defName == "TerrorSource");
            f.work = d.statBases?.GetStatValueFromList(StatDefOf.WorkToMake, 0f) ?? 0f;
            f.melee = d.IsMeleeWeapon;
            f.edged = d.tools != null && d.tools.Any(t => t?.capacities != null && t.capacities.Any(c => c?.defName == "Cut" || c?.defName == "Stab"));
            f.tagged = d.weaponTags != null && d.weaponTags.Any(t => ThingRolesMath.GameWeaponTag(t, gameWeaponTags));
            return f;
        }

        /// <summary>For the dev action and the log: each role's things for each age, and what was left out of the toast and the gifts.</summary>
        public static string Explain(Map map)
        {
            var sb = new System.Text.StringBuilder();
            foreach (ThingRole role in Enum.GetValues(typeof(ThingRole)))
            {
                sb.AppendLine(role + ":");
                foreach (var age in AgesMath.All)
                {
                    var list = Candidates(role, age);
                    var minded = map == null ? new List<ThingDef>() : list.Where(d => ColonyMinds(d, map)).ToList();
                    sb.AppendLine("  " + age + " (" + list.Count + "): " + string.Join(", ", list.Select(d => d.label + (minded.Contains(d) ? " [the colony minds it]" : "")))
                                  + (list.Count == 0 ? "none, so " + (Fallback(role, age)?.label ?? "nothing") : ""));
                }
            }
            return sb.ToString();
        }
    }
}
