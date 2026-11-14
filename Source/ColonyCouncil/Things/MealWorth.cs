using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The food in a map's stores counted as the meals the colony's own kitchen would make of it (DESIGN.md, "The days of
    /// food as the kitchen would make them"; the arithmetic in <see cref="MealWorthMath"/>), always in nutrition: meals at
    /// their own; each raw ingredient (flour and every other one the recipes take, eaten raw or not) at the meals of its
    /// best recipe; food a person eats raw that no recipe takes, or that is worth more raw, at its own; animal feed (kibble,
    /// hay, anything only animals eat) left out. The recipes are the game's as it runs, every mod's: one counts when it makes
    /// a meal a person enjoys (simple or better: not paste or an awful meal, not a drug), its research is done, a bench of
    /// the colony's on the map has it, someone at home has the skill it asks, and (for a meal of several ingredients) the
    /// stores hold something for each. With no such bench (no stove of any kind) raw food counts at its raw worth. The
    /// ingredients a recipe takes are those a new bill of it allows (its fixed and default filters, and their special
    /// filters, as the game reads them). What each kind of thing counts for is kept per map, read again when the colony's
    /// benches change and every in-game hour (research, cooks, what is in store); the total is kept for a second of game
    /// time, so Food Alert's recount every 100 ticks costs one pass over the game's own count of the stores.
    /// </summary>
    public static class MealWorth
    {
        /// <summary>The benches' recipes are read again this often (research done and the cooks' skill), and at once when the benches change.</summary>
        public const int RereadTicks = Sched.HourTicks;
        /// <summary>The stores' total holds this long (a second of game time).</summary>
        public const int TotalTicks = 60;

        /// <summary>A recipe as the count reads it: what it gives back for the nutrition put in, and the ingredients a new bill of it takes, all and by group.</summary>
        private sealed class Shape
        {
            public float gain;
            public readonly HashSet<ThingDef> takes = new HashSet<ThingDef>();
            public readonly List<HashSet<ThingDef>> groups = new List<HashSet<ThingDef>>();
        }

        private static readonly Shape NoMeal = new Shape();

        /// <summary>One map's reading.</summary>
        private sealed class Book
        {
            public Map map;
            public int readAt = Fresh.Never, buildings = -1;
            public HashSet<ThingDef> benches = new HashSet<ThingDef>();
            /// <summary>Each ingredient's best gain among the recipes the colony can cook here now.</summary>
            public readonly Dictionary<ThingDef, float> gains = new Dictionary<ThingDef, float>();
            /// <summary>What one piece of each kind of thing counts for.</summary>
            public readonly Dictionary<ThingDef, float> perPiece = new Dictionary<ThingDef, float>();
            public bool cooks;
            public int totalAt = Fresh.Never;
            public float total;
        }

        private static readonly Dictionary<int, Book> books = new Dictionary<int, Book>();
        private static readonly Dictionary<RecipeDef, Shape> shapes = new Dictionary<RecipeDef, Shape>();
        private static readonly Dictionary<ThingDef, float> nutritions = new Dictionary<ThingDef, float>();
        private static HashSet<ThingDef> ingredients;
        private static Game game;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- What is asked ----------------

        /// <summary>The nutrition the map's stores (the game's own count of what is stored) come to as the colony's kitchen would make them.</summary>
        public static float Total(Map map)
        {
            if (map?.resourceCounter == null || map.listerBuildings == null) return 0f;
            var b = BookOf(map);
            int now = Now;
            if (Fresh.Holds(b.totalAt, now, TotalTicks)) return b.total;
            b.total = MealWorthMath.Total(Stores(b, map.resourceCounter.AllCountedAmounts));
            b.totalAt = now;
            return b.total;
        }

        /// <summary>What one piece of a kind of thing counts for in the map's stores (0 for animal feed and what isn't food).</summary>
        public static float PerPiece(Map map, ThingDef d)
        {
            if (map?.listerBuildings == null || d == null) return 0f;
            return PerPieceOf(BookOf(map), d);
        }

        /// <summary>Whether the colony can cook an enjoyable meal on the map now (a bench for it, its research, a cook for it).</summary>
        public static bool Cooks(Map map) => map?.listerBuildings != null && BookOf(map).cooks;

        /// <summary>
        /// Whether any of the game's recipes that make an enjoyable meal takes this thing, wherever it is cooked: flour
        /// that no one eats raw is cooking stock, not animal feed (the plan ahead's kinds, Building/ColonyNeeds.cs).
        /// </summary>
        public static bool CookingIngredient(ThingDef d)
        {
            if (d == null || IsFeed(d)) return false;
            if (ingredients == null)
            {
                var all = new HashSet<ThingDef>();
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    var s = ShapeOf(r);
                    if (s.gain > 0f) all.UnionWith(s.takes);
                }
                ingredients = all;
            }
            return ingredients.Contains(d);
        }

        /// <summary>A thing's nutrition, the game's stat (kept: a def's doesn't change while the game runs).</summary>
        public static float Nutrition(ThingDef d)
        {
            if (d == null) return 0f;
            if (nutritions.TryGetValue(d, out float n)) return n;
            try
            {
                n = d.IsIngestible ? d.GetStatValueAbstract(StatDefOf.Nutrition) : 0f;
            }
            catch
            {
                n = d.ingestible?.CachedNutrition ?? 0f;
            }
            nutritions[d] = n;
            return n;
        }

        /// <summary>Animal feed: kibble and the like (raw, of the kibble kind or named so). Hay and the rest only animals eat count nothing through having no recipe that takes them.</summary>
        public static bool IsFeed(ThingDef d)
        {
            var ing = d?.ingestible;
            if (ing == null) return false;
            if (ing.preferability <= FoodPreferability.RawTasty && (ing.foodType & FoodTypeFlags.Kibble) != 0) return true;
            return Making.KindOf(d) == "feed";
        }

        /// <summary>
        /// Cooked meals in the stores counted in servings: their nutrition in simple meals' worth, so a stack of pemmican
        /// is the few meals it makes and not a meal a piece. <paramref name="isMeal"/> picks the kinds counted.
        /// </summary>
        public static int StoredServings(Map map, Func<ThingDef, bool> isMeal)
        {
            if (map?.resourceCounter == null || isMeal == null) return 0;
            float n = 0f;
            foreach (var kv in map.resourceCounter.AllCountedAmounts)
                if (kv.Value > 0 && kv.Key != null && isMeal(kv.Key))
                    n += kv.Value * Nutrition(kv.Key);
            return MealWorthMath.Servings(n);
        }

        /// <summary>The servings in these meals (see <see cref="StoredServings"/>).</summary>
        public static int Servings(IEnumerable<Thing> meals)
        {
            float n = 0f;
            if (meals != null)
                foreach (var t in meals)
                    if (t?.def != null)
                        n += t.stackCount * Nutrition(t.def);
            return MealWorthMath.Servings(n);
        }

        // ---------------- The map's reading ----------------

        private static IEnumerable<(int pieces, float perPiece)> Stores(Book b, Dictionary<ThingDef, int> counted)
        {
            foreach (var kv in counted)
                if (kv.Value > 0 && kv.Key != null)
                    yield return (kv.Value, PerPieceOf(b, kv.Key));
        }

        private static Book BookOf(Map map)
        {
            if (Current.Game != game)
            {
                game = Current.Game;
                books.Clear();
            }
            if (!books.TryGetValue(map.uniqueID, out var b) || b.map != map)
            {
                // Maps come and go (a site, a raid on an outpost): those that are gone are let go.
                if (books.Count > 8) books.Clear();
                b = new Book { map = map };
                books[map.uniqueID] = b;
            }
            int now = Now;
            int count = map.listerBuildings.allBuildingsColonist.Count;
            bool due = !Fresh.Holds(b.readAt, now, RereadTicks);
            if (due || count != b.buildings)
            {
                b.buildings = count;
                var benches = Benches(map);
                if (due || !benches.SetEquals(b.benches))
                {
                    b.benches = benches;
                    b.readAt = now;
                    try
                    {
                        Read(b);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("meal worth", e);
                        b.gains.Clear();
                        b.cooks = false;
                    }
                    b.perPiece.Clear();
                    b.totalAt = Fresh.Never;
                }
            }
            return b;
        }

        /// <summary>The kinds of work bench the colony has built on the map (not frames).</summary>
        private static HashSet<ThingDef> Benches(Map map)
        {
            var set = new HashSet<ThingDef>();
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i]?.def;
                if (d != null && !d.IsFrame && d.IsWorkTable) set.Add(d);
            }
            return set;
        }

        /// <summary>Each ingredient's best gain among the enjoyable meals the colony can cook here now.</summary>
        private static void Read(Book b)
        {
            b.gains.Clear();
            b.cooks = false;
            var cooks = b.map.mapPawns?.FreeColonistsSpawned;
            var stores = b.map.resourceCounter?.AllCountedAmounts;
            var seen = new HashSet<RecipeDef>();
            foreach (var bench in b.benches)
            {
                var recipes = bench.AllRecipes;
                if (recipes == null) continue;
                for (int i = 0; i < recipes.Count; i++)
                {
                    var r = recipes[i];
                    if (r == null || !seen.Add(r)) continue;
                    var s = ShapeOf(r);
                    if (s.gain <= 0f || !r.AvailableNow || !SomeoneCanCook(r, cooks)) continue;
                    b.cooks = true;
                    // A meal of several ingredients is cooked only while the stores hold something for each of them.
                    if (!Stocked(s, stores)) continue;
                    foreach (var t in s.takes)
                        if (!b.gains.TryGetValue(t, out float had) || s.gain > had)
                            b.gains[t] = s.gain;
                }
            }
        }

        /// <summary>The stores hold something for each of the recipe's ingredient groups (one group: whatever is counted is its own).</summary>
        private static bool Stocked(Shape s, Dictionary<ThingDef, int> stores)
        {
            if (s.groups.Count <= 1 || stores == null) return true;
            foreach (var g in s.groups)
            {
                bool any = false;
                foreach (var t in g)
                    if (stores.TryGetValue(t, out int n) && n > 0)
                    {
                        any = true;
                        break;
                    }
                if (!any) return false;
            }
            return true;
        }

        /// <summary>The recipe's skill is met by someone at home (with nobody at home, nobody is asked).</summary>
        private static bool SomeoneCanCook(RecipeDef r, List<Pawn> cooks)
        {
            if (r.skillRequirements == null || r.skillRequirements.Count == 0 || cooks == null || cooks.Count == 0) return true;
            for (int i = 0; i < cooks.Count; i++)
                if (cooks[i] != null && r.PawnSatisfiesSkillRequirements(cooks[i]))
                    return true;
            return false;
        }

        private static float PerPieceOf(Book b, ThingDef d)
        {
            if (b.perPiece.TryGetValue(d, out float v)) return v;
            var ing = d.ingestible;
            bool human = ing != null && ing.HumanEdible;
            bool meal = human && ing.preferability >= FoodPreferability.MealTerrible;
            bool asIs = human && (ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty);
            var kind = MealWorthMath.KindOf(d.IsNutritionGivingIngestible, d.IsDrug || d.IsCorpse, IsFeed(d), meal);
            b.gains.TryGetValue(d, out float gain);
            v = MealWorthMath.PerPiece(kind, Nutrition(d), gain, asIs);
            b.perPiece[d] = v;
            return v;
        }

        // ---------------- A recipe ----------------

        private static Shape ShapeOf(RecipeDef r)
        {
            if (r == null) return NoMeal;
            if (shapes.TryGetValue(r, out var s)) return s;
            try
            {
                s = ReadShape(r);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("meal worth: recipe " + r.defName, e);
                s = NoMeal;
            }
            shapes[r] = s;
            return s;
        }

        /// <summary>A meal a person enjoys: eaten as it is, simple or better, not a drug.</summary>
        private static bool Enjoyable(ThingDef d) =>
            d != null && d.IsNutritionGivingIngestible && d.ingestible.HumanEdible && !d.IsDrug && d.ingestible.preferability >= FoodPreferability.MealSimple;

        private static Shape ReadShape(RecipeDef r)
        {
            if (r.products == null || r.products.Count == 0 || r.ingredients == null || r.ingredients.Count == 0) return NoMeal;
            float made = 0f;
            foreach (var p in r.products)
                if (p?.thingDef != null && Enjoyable(p.thingDef))
                    made += MealWorthMath.Made(Nutrition(p.thingDef), p.count);
            if (made <= 0f) return NoMeal;
            var getter = r.IngredientValueGetter;
            if (getter == null) return NoMeal;
            var barredFixed = Barred(r.fixedIngredientFilter);
            var barredDefault = Barred(r.defaultIngredientFilter);
            var shape = new Shape();
            var groups = new List<float>(r.ingredients.Count);
            foreach (var ing in r.ingredients)
            {
                float perUnit = 0f;
                int n = 0;
                var group = new HashSet<ThingDef>();
                if (ing?.filter != null)
                    foreach (var t in ing.filter.AllowedThingDefs)
                    {
                        if (!Takes(r, t, barredFixed, barredDefault)) continue;
                        float value = ValueOf(getter, t);
                        if (value <= 0f) continue;
                        group.Add(t);
                        shape.takes.Add(t);
                        perUnit += Nutrition(t) / value;
                        n++;
                    }
                shape.groups.Add(group);
                // Counted by nutrition, a unit is one nutrition; by pieces, the nutrition of a piece it takes.
                groups.Add(MealWorthMath.GroupNutrition(ing?.GetBaseCount() ?? 0f, n > 0 ? perUnit / n : 0f));
            }
            shape.gain = MealWorthMath.Gain(made, groups);
            return shape.gain > 0f ? shape : NoMeal;
        }

        /// <summary>The recipe's value of one piece (its nutrition, or 1 for a piece); a mod's getter that can't answer for a thing leaves that thing out.</summary>
        private static float ValueOf(IngredientValueGetter getter, ThingDef t)
        {
            try
            {
                return getter.ValuePerUnitOf(t);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>A new bill of the recipe would take it: its fixed and default filters allow it, special filters as the game reads them.</summary>
        private static bool Takes(RecipeDef r, ThingDef t, List<SpecialThingFilterDef> barredFixed, List<SpecialThingFilterDef> barredDefault)
        {
            if (t == null) return false;
            if (r.fixedIngredientFilter != null && (!r.fixedIngredientFilter.Allows(t) || BarredBy(barredFixed, t))) return false;
            if (r.defaultIngredientFilter != null && (!r.defaultIngredientFilter.Allows(t) || BarredBy(barredDefault, t))) return false;
            return true;
        }

        /// <summary>The special filters a filter turns down (hay-like plant food for the game's meals).</summary>
        private static List<SpecialThingFilterDef> Barred(ThingFilter f)
        {
            var list = new List<SpecialThingFilterDef>();
            if (f == null) return list;
            foreach (var sf in DefDatabase<SpecialThingFilterDef>.AllDefsListForReading)
                if (sf != null && !f.Allows(sf))
                    list.Add(sf);
            return list;
        }

        /// <summary>A special filter turned down always matches the thing, within its category: the game turns down every such piece.</summary>
        private static bool BarredBy(List<SpecialThingFilterDef> barred, ThingDef t)
        {
            for (int i = 0; i < barred.Count; i++)
            {
                var sf = barred[i];
                try
                {
                    if (sf.Worker != null && sf.Worker.AlwaysMatches(t) && (sf.parentCategory == null || t.IsWithinCategory(sf.parentCategory))) return true;
                }
                catch
                {
                    // A mod's special filter that can't be asked about a def: left to the game.
                }
            }
            return false;
        }
    }
}
