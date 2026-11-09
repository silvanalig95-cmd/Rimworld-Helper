using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The kinds Rimfolk's item card shows: food, drink, drugs and medicine (step 1), materials and art (step 2), then step 3's
    /// books, body parts, techprints, trainers, genes, usable items, shells, furniture in a crate and any other item, read the
    /// same way (<see cref="ItemSort"/>). None opens the game's card (weapons and apparel have their own, and the few things
    /// ItemCardLive.KindOf leaves to the game).
    /// </summary>
    public enum ItemKind
    {
        None,
        Food,
        Drink,
        Drug,
        Medicine,
        Material,
        Art,
        Book,
        BodyPart,
        Techprint,
        Trainer,
        Genes,
        Usable,
        Shell,
        Furniture,
        Other
    }

    /// <summary>
    /// What the item card knows of a thing: its own numbers (read from the game by Items/ItemCardLive.cs, by hand in the
    /// tests), Rimfolk's roles for it, and the colony's side (the map the card is opened on; nothing without one). Plain
    /// data: the rules in <see cref="ItemCardMath"/> turn it into tags and a line in plain words.
    /// </summary>
    public sealed class ItemFacts
    {
        /// <summary>The kind's label as the game has it ("fine meal", "herbal medicine").</summary>
        public string Label = "";
        public ItemKind Kind;
        /// <summary>A thing in hand (a stack on the map, in a pack, on the trade screen), not a kind.</summary>
        public bool InHand;
        public int Count = 1;
        /// <summary>A piece's market value in silver and its mass in kilograms.</summary>
        public float Value, Mass;
        public bool Tradeable = true;

        // ---------------- Food ----------------

        /// <summary>Nutrition a piece.</summary>
        public float Nutrition;
        /// <summary>How much people like to eat it (<see cref="ThingRolesMath.Preferabilities"/>).</summary>
        public int Preferability;
        public bool Meal, HumanEdible, Fluid;
        /// <summary>Days before it starts to rot above 10 °C; -1 when it never rots.</summary>
        public float RotDays = -1f;
        /// <summary>A thing: days left before it starts to rot where it lies now (the game's own reckoning at its temperature); -1 unknown or never.</summary>
        public float DaysLeftHere = -1f;
        /// <summary>A thing: it doesn't rot where it lies now (at or below 0 °C, or in a fridge).</summary>
        public bool FrozenHere;
        /// <summary>A thing already rotting: whoever eats it gets food poisoning.</summary>
        public bool Rotting;
        /// <summary>The game's thought from eating it as it is (its taste, or "ate raw food"), its mood and how long it lasts.</summary>
        public string TasteThought = "";
        public float TasteMood, TasteDays;
        /// <summary>A thing's own chance of food poisoning (its cook, its kitchen), 0 to 1.</summary>
        public float PoisonChance;
        /// <summary>Its kind's own chance of food poisoning for people (the game's dangerous kinds of food), 0 to 1.</summary>
        public float PoisonFixed;
        /// <summary>The storyteller's factor on food poisoning.</summary>
        public float PoisonFactor = 1f;
        /// <summary>Human meat or made with it; insect meat or made with it.</summary>
        public bool OfPerson, Insect;
        /// <summary>Raw food a recipe of the colony's cooks into a meal, and where (a bench's label, empty when unknown).</summary>
        public bool Cookable;
        public string CookAt = "";

        // ---------------- Drink and drugs ----------------

        public bool IsDrug;
        /// <summary>0 none, 1 medical, 2 social, 3 hard (the game's drug categories).</summary>
        public int DrugKind;
        public bool Alcohol;
        /// <summary>A teetotaler may have it (tea, coffee).</summary>
        public bool Soft;
        /// <summary>Each dose may addict: its chance, and the tolerance it needs first (0 none).</summary>
        public bool Addictive;
        public float Addictiveness, MinTolerance;
        /// <summary>What a dose adds toward an overdose (a range, 0 to 1 of a deadly one), and the chance of a large one.</summary>
        public float OverdoseMin, OverdoseMax, LargeOverdose;
        /// <summary>Recreation a dose gives, 0 to 1.</summary>
        public float Joy;
        /// <summary>The best mood its effect gives while it lasts (the game's thought on its high), and how long one dose lasts in days.</summary>
        public float HighMood, HighDays;

        // ---------------- Medicine ----------------

        /// <summary>Its medical potency and the best tending it allows, against the game's herbal and industrial medicine.</summary>
        public float Potency, QualityMax;
        public float HerbalPotency = 0.6f, IndustrialPotency = 1f;
        /// <summary>Rimfolk's medicine by what is treated is on (Health/MedicineChoice.cs).</summary>
        public bool MedicineByNeed;

        // ---------------- Rimfolk's roles (Things/ThingRoles.cs, Integrations/ToastDrinks.cs) ----------------

        public bool FeastDish, ToastDrink, Gift, Provisions;
        /// <summary>The journey planner's order for the road: 2 never rots, 1 keeps, 0 not provisions.</summary>
        public int TravelRank;

        // ---------------- The colony ----------------

        /// <summary>There is a colony to speak of (the card is opened on a home map).</summary>
        public bool HasColony;
        /// <summary>Its grown people at home, and those whose faith, diet or nature rules this out, by name and why.</summary>
        public int Grown;
        public int ForbiddenFor;
        public string ForbiddenWho = "";
        /// <summary>A drug: colonists whose drug policy lets them take it, of how many.</summary>
        public int PolicyAllows, PolicyOf;
        /// <summary>Food: days the colony eats from its stores; medicine: how much of every kind it holds; its people.</summary>
        public float FoodDays = -1f;
        public int MedicineHeld = -1, People;
        /// <summary>The colony keeps food cold somewhere (a freezer, a fridge, a cold world).</summary>
        public bool ColdStore = true;

        // ---------------- Materials and art (step 2: Items/ItemCardMaterialMath.cs) ----------------

        /// <summary>A material's numbers, uses, stock and sources; null for other kinds.</summary>
        public MaterialFacts Material;
        /// <summary>A work of art's beauty, story and room; null for other kinds.</summary>
        public ArtFacts ArtPiece;

        // ---------------- Step 3 (Items/ItemCardUseMath.cs) ----------------

        /// <summary>A book's, body part's, techprint's, trainer's, gene holder's, usable item's, shell's, crated furniture's or any other item's facts and who at home it is for; null for other kinds.</summary>
        public UseFacts Use;
    }

    /// <summary>
    /// The item card's rules, pure (no game types): which kind a thing is, its tags (the shared six categories of
    /// <see cref="TagCategory"/>, each from the thing's own numbers or Rimfolk's own rules, with why), the line in plain
    /// words, and the small sums the card shows (colonist-days of food, days before it rots, tending quality, whether the
    /// colony is short). Tools/Tests checks them on the base game's food, drink, drugs and medicine. Materials' and art's
    /// rules (step 2) are in Items/ItemCardMaterialMath.cs, their tags in this one vocabulary.
    /// </summary>
    public static partial class ItemCardMath
    {
        /// <summary>Nutrition a grown colonist eats a day (the game's human at "fed"; Rimfolk's days of food count the same).</summary>
        public const float NeedADay = 1.6f;

        /// <summary>A meal's nutrition: a supper.</summary>
        public const float MealNutrition = 0.9f;

        /// <summary>Food that starts to rot within this many days spoils fast (meals and meat do).</summary>
        public const float SpoilsFastDays = 5f;

        /// <summary>A trade good is worth this much silver a kilogram or more, and keeps.</summary>
        public const float TradeValuePerKg = 50f;

        /// <summary>A chance of food poisoning worth a tag (half a percent).</summary>
        public const float PoisonWorthSaying = 0.005f;

        /// <summary>Tags shown at most.</summary>
        public const int MaxTags = 8;

        // ---------------- The kind ----------------

        /// <summary>
        /// The card's kind of a thing: medicine; a drug that is drunk is a drink (beer, tea), any other drug a drug; food
        /// that is drunk (a juice) a drink; anything else that feeds, food. None for the rest (the game's card opens).
        /// </summary>
        public static ItemKind KindOf(bool medicine, bool drug, bool fluid, bool feeds)
        {
            if (medicine) return ItemKind.Medicine;
            if (drug) return fluid ? ItemKind.Drink : ItemKind.Drug;
            if (feeds) return fluid ? ItemKind.Drink : ItemKind.Food;
            return ItemKind.None;
        }

        /// <summary>The card's food parts (nutrition, keeping, who eats it) apply: food, and a drink that isn't a drug.</summary>
        public static bool ShowsFood(ItemFacts f) => f != null && (f.Kind == ItemKind.Food || f.Kind == ItemKind.Drink && !f.IsDrug);

        /// <summary>The card's drug parts (effects, addiction, overdose, policies) apply.</summary>
        public static bool ShowsDrug(ItemFacts f) => f != null && f.IsDrug && f.Kind != ItemKind.Medicine;

        public static bool Raw(ItemFacts f) => f != null && (f.Preferability == ThingRolesMath.RawBad || f.Preferability == ThingRolesMath.RawTasty);

        // ---------------- Small sums ----------------

        /// <summary>Colonist-days of food in a number of pieces.</summary>
        public static float ColonistDays(float nutrition, int count) => Math.Max(0f, nutrition) * Math.Max(0, count) / NeedADay;

        /// <summary>Pieces that make a meal's worth (raw potatoes: 18), at least one.</summary>
        public static int ToAMeal(float nutrition) => nutrition <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(MealNutrition / nutrition - 0.0001f));

        /// <summary>Suppers in a stack of meals: one a meal of a meal's worth or more, else by nutrition.</summary>
        public static int Suppers(float nutrition, int count) =>
            nutrition >= MealNutrition - 0.0001f ? Math.Max(0, count) : (int)Math.Floor(Math.Max(0f, nutrition) * Math.Max(0, count) / MealNutrition + 0.0001f);

        /// <summary>
        /// The game's tending quality with a medicine (TendUtility.CalculateBaseTendQuality, no bed): the doctor's tend
        /// quality times the medicine's potency, held between none and the medicine's own best.
        /// </summary>
        public static float TendQuality(float doctorQuality, float potency, float qualityMax) => Math.Max(0f, Math.Min(Math.Max(0f, qualityMax), doctorQuality * potency));

        /// <summary>The game's tending without medicine (its own potency and best for none).</summary>
        public const float NoMedicinePotency = 0.3f, NoMedicineQualityMax = 0.7f;

        /// <summary>The lowest medical care that allows it (MedicineChoiceMath: herbal, normal, best).</summary>
        public static int CareTier(ItemFacts f) => MedicineChoiceMath.NeedsCare(f.Potency, f.HerbalPotency, f.IndustrialPotency);

        /// <summary>The colony's days of food are short of what the trade advice keeps as the least (ten days).</summary>
        public static bool FoodShort(float days) => days >= 0f && days < TradeAdviceMath.FoodDaysAtLeast;

        /// <summary>More food than twice the least the trade advice keeps.</summary>
        public static bool FoodSpare(float days) => days >= 0f && TradeAdviceMath.FoodSurplusDays(days, TradeAdviceMath.FoodDaysAtLeast) > 0f;

        /// <summary>The medicine the colony needs (the trade advice's floor for its people) and keeps (twice that).</summary>
        public static int MedicineNeed(int people) => TradeAdviceMath.Need(TradeGood.Medicine, people, 0f);

        public static int MedicineKeep(int people) => TradeAdviceMath.Keep(TradeGood.Medicine, people, 0f, 0);

        public static bool MedicineShort(int held, int people) => held >= 0 && people > 0 && held < MedicineNeed(people);

        public static bool MedicineSpare(int held, int people) => held >= 0 && people > 0 && held > MedicineKeep(people);

        /// <summary>The chance of food poisoning eating it: a rotting one for sure, else its own or its kind's, the larger, by the storyteller's factor.</summary>
        public static float PoisonOdds(ItemFacts f)
        {
            if (f == null) return 0f;
            if (f.Rotting && f.PoisonFactor > 0f) return 1f;
            return Math.Max(0f, Math.Min(1f, Math.Max(f.PoisonChance, f.PoisonFixed) * Math.Max(0f, f.PoisonFactor)));
        }

        /// <summary>Silver a kilogram (0 with no mass).</summary>
        public static float ValuePerKg(ItemFacts f) => f == null || f.Mass <= 0.0001f ? 0f : f.Value / f.Mass;

        // ---------------- Words ----------------

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A share as a percentage: one decimal under 10%, whole above ("3.4%", "35%").</summary>
        public static string Pct(float x)
        {
            float p = x * 100f;
            return (Math.Abs(p) < 9.95f && Math.Abs(p - (float)Math.Round(p)) > 0.049f ? p.ToString("0.#", Inv) : Math.Round(p).ToString("0", Inv)) + "%";
        }

        /// <summary>A mood with its sign ("+5", "-7").</summary>
        public static string Mood(float m) => m > 0f ? "+" + m.ToString("0.#", Inv) : m.ToString("0.#", Inv);

        /// <summary>A number with up to two decimals ("0.9", "0.05").</summary>
        public static string N(float v) => v.ToString("0.##", Inv);

        /// <summary>A stretch of time in days, or hours under a day ("4 days", "1 day", "12 hours").</summary>
        public static string Days(float d)
        {
            if (d < 1f)
            {
                int h = Math.Max(1, (int)Math.Round(d * 24f));
                return h == 1 ? "1 hour" : h + " hours";
            }
            float v = d < 10f ? (float)Math.Round(d, 1) : (float)Math.Round(d);
            return Math.Abs(v - 1f) < 0.001f ? "1 day" : v.ToString("0.#", Inv) + " days";
        }

        /// <summary>A thing with "a" or "an" before it ("a campfire", "an electric stove").</summary>
        public static string A(string s) => string.IsNullOrEmpty(s) ? "" : ("aeiouAEIOU".IndexOf(s[0]) >= 0 ? "an " : "a ") + s;

        /// <summary>A label with its first letter capital.</summary>
        public static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ---------------- The tags ----------------

        private static TagDef D(string id, string label, TagCategory c, int importance, string rule) =>
            new TagDef { Id = id, Label = label, Category = c, Importance = importance, Rule = rule };

        private const TagCategory S = TagCategory.Strength, W = TagCategory.Weakness, R = TagCategory.Risk, X = TagCategory.Special,
            O = TagCategory.Role, Nd = TagCategory.Needs;

        /// <summary>The item card's one vocabulary: every tag, its category, its importance and its rule in words (step 1's, then step 2's for materials and art, then step 3's for the rest).</summary>
        public static readonly TagDef[] Vocabulary = StepOneTags().Concat(StepTwoTags()).Concat(StepThreeTags()).ToArray();

        private static IEnumerable<TagDef> StepOneTags() => new[]
        {
            D("of-person", "Of a person", R, 99, "human meat or leather, or made with it: people who aren't cannibals are horrified to eat it or unhappy to wear it"),
            D("overdose", "Overdose risk", R, 97, "each dose adds toward an overdose, which kills at 100%"),
            D("addictive", "Addictive", R, 95, "each dose may addict, by the game's own chance"),
            D("food-poisoning", "Food poisoning", R, 93, "a chance of food poisoning: the stack's own (its cook, its kitchen), its kind's, or certain once it rots"),
            D("forbidden", "Forbidden to some", R, 91, "some of the colony's grown people won't have it: their faith, their diet or their nature rules it out"),
            D("needed", "Needed now", O, 90, "the colony is short of it: under ten days of food, less medicine than the trade advice's floor, or less of a material than its blueprints, bills or need; someone at home is missing the part it replaces or has no bed; a project still needs the techprint"),
            D("mood-lift", "Mood lift", S, 88, "eating or taking it gives a good mood (the game's own thought)"),
            D("potent", "Potent", S, 87, "medicine at least as potent as industrial medicine"),
            D("for-serious", "For serious care", O, 86, "with medicine by what is treated, kept for heavy bleeding, deep wounds, a vital part, illness and infection"),
            D("for-minor", "For minor wounds", O, 86, "with medicine by what is treated, what cuts, bites, small burns and everyday ailments get"),
            D("weak-surgery", "Risky for surgery", W, 84, "medicine less potent than industrial medicine: surgery with it fails more often, its tending stops lower"),
            D("spoils-fast", "Spoils fast", W, 82, "it starts to rot within 5 days above 10 °C"),
            D("nutritious", "Nutritious", S, 80, "a whole meal's worth (0.9 nutrition) or more in one piece"),
            D("keeps-long", "Keeps long", S, 78, "it never rots, or keeps 20 days or more"),
            D("provisions", "Provisions", O, 76, "a meal that keeps 20 days or more: the journey planner packs it first"),
            D("raw", "Raw", W, 74, "raw food that sours the mood eaten as it is"),
            D("bad-taste", "Bad taste", W, 72, "eating it gives a bad mood (the game's own thought)"),
            D("insect", "Insect meat", R, 70, "insect meat, or made with it: most people are disgusted to eat it"),
            D("feast-dish", "Feast's dish", X, 68, "a fine or lavish meal a recipe cooks: what a feast sets on the table"),
            D("toast-drink", "Toast's drink", X, 68, "a social drink with alcohol, or one a teetotaler may have: poured at Rimfolk's toasts"),
            D("gift", "Gift for a people", X, 64, "a fine drink worth 15 silver or more, a precious material or small art: a gift between peoples"),
            D("drug-policy", "Drug policy", Nd, 62, "colonists take it only as their drug policy allows"),
            D("recreation", "Recreation", S, 60, "a dose, a read or a piece of furniture gives recreation"),
            D("cooking", "Cooking", Nd, 58, "raw food a recipe of the colony's cooks into a meal"),
            D("freezer", "Freezer", Nd, 56, "it spoils fast and the colony keeps nothing cold"),
            D("spare", "Spare to sell", O, 54, "the colony holds more than twice what it needs (the trade advice's reserve), a work of art no room of the colony's would rise with, or a techprint its project needs no more"),
            D("trade-good", "Trade good", O, 50, "it keeps, and is worth 50 silver a kilogram or more: much worth in a pack"),
        };

        private static readonly Dictionary<string, TagDef> byId = Vocabulary.ToDictionary(t => t.Id);

        public static TagDef Def(string id) => byId.TryGetValue(id, out var d) ? d : null;

        /// <summary>
        /// Every tag the thing earns, each with why in its own numbers, most important first; at most <see cref="MaxTags"/>,
        /// keeping its first strength and first weakness when it has them (as the weapon and apparel tags do).
        /// </summary>
        public static List<ThingTag> Tags(ItemFacts f)
        {
            var all = AllTags(f);
            var sorted = all.OrderByDescending(t => t.Importance).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
            if (sorted.Count <= MaxTags) return sorted;
            var keep = sorted.Take(MaxTags).ToList();
            foreach (var c in new[] { TagCategory.Strength, TagCategory.Weakness })
            {
                if (keep.Any(t => t.Category == c)) continue;
                var first = sorted.FirstOrDefault(t => t.Category == c);
                if (first == null) continue;
                // The least important tag that isn't the only one of its kind gives way.
                var drop = keep.LastOrDefault(t => keep.Count(k => k.Category == t.Category) > 1);
                if (drop == null) continue;
                keep.Remove(drop);
                keep.Add(first);
            }
            return keep.OrderByDescending(t => t.Importance).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
        }

        private static ThingTag T(string id, string why, string label = null)
        {
            var d = byId[id];
            return new ThingTag { Id = id, Label = label ?? d.Label, Category = d.Category, Why = why, Importance = d.Importance };
        }

        /// <summary>Every tag the rules give, unordered and uncut.</summary>
        public static List<ThingTag> AllTags(ItemFacts f)
        {
            var tags = new List<ThingTag>();
            if (f == null || f.Kind == ItemKind.None) return tags;
            if (f.Kind == ItemKind.Material)
            {
                MaterialTags(f, tags);
                return tags;
            }
            if (f.Kind == ItemKind.Art)
            {
                ArtTags(f, tags);
                return tags;
            }
            if (StepThree(f.Kind))
            {
                UseTags(f, tags);
                return tags;
            }
            bool food = ShowsFood(f);

            // What it is made of.
            if (f.OfPerson) tags.Add(T("of-person", "human meat, or made with it: people who aren't cannibals are horrified to eat it, and those who are glad of it are few"));
            if (f.Insect && !f.OfPerson) tags.Add(T("insect", "insect meat, or made with it: most people are disgusted to eat it (the game's own thought)"));

            // Its mood: a taste, raw food, a drug's high.
            if (food && f.TasteMood > 0f)
                tags.Add(T("mood-lift", "eating it gives \"" + f.TasteThought + "\": " + Mood(f.TasteMood) + " mood for " + Days(f.TasteDays)));
            else if (ShowsDrug(f) && f.HighMood > 0f)
                tags.Add(T("mood-lift", "its effect gives " + Mood(f.HighMood) + " mood while it lasts" + (f.HighDays > 0f ? " (" + Days(f.HighDays) + " a dose)" : "")));
            if (food && f.TasteMood < 0f)
            {
                if (RawToCook(f)) tags.Add(T("raw", "eaten as it is it gives \"" + f.TasteThought + "\" (" + Mood(f.TasteMood) + " mood); cooked into a meal it doesn't"));
                else tags.Add(T("bad-taste", "eating it gives \"" + f.TasteThought + "\": " + Mood(f.TasteMood) + " mood for " + Days(f.TasteDays)));
            }
            if (ShowsDrug(f) && f.Joy > 0f) tags.Add(T("recreation", "a dose gives " + Pct(f.Joy) + " recreation"));

            // Food: what it gives and how it keeps.
            if (food)
            {
                if (f.HumanEdible && f.Nutrition >= MealNutrition - 0.0001f)
                    tags.Add(T("nutritious", N(f.Nutrition) + " nutrition a piece, a whole meal's worth (a colonist eats about " + N(NeedADay) + " a day)"));
                if (f.RotDays < 0f) tags.Add(T("keeps-long", "it never rots"));
                else if (f.RotDays >= ThingRolesMath.KeepsDays) tags.Add(T("keeps-long", "it keeps " + Days(f.RotDays) + " before it starts to rot, and for good below 0 °C"));
                else if (f.RotDays > 0f && f.RotDays <= SpoilsFastDays)
                {
                    tags.Add(T("spoils-fast", "it starts to rot after " + Days(f.RotDays) + " above 10 °C, slower as it gets colder, never at or below 0 °C"));
                    if (f.HasColony && !f.ColdStore) tags.Add(T("freezer", "it rots in " + Days(f.RotDays) + " and the colony keeps nothing cold: a room at or below 0 °C keeps it for good"));
                }
                if (RawToCook(f) && f.Preferability == ThingRolesMath.RawBad)
                    tags.Add(T("cooking", f.CookAt.Length > 0 ? "raw: cooked into a meal at " + A(f.CookAt) : "raw: best cooked into a meal"));
            }

            // Food poisoning.
            float odds = PoisonOdds(f);
            if (food || f.Kind == ItemKind.Drink)
            {
                if (f.Rotting && odds >= 1f) tags.Add(T("food-poisoning", "it is rotting: whoever eats it gets food poisoning"));
                else if (odds >= PoisonWorthSaying)
                    tags.Add(T("food-poisoning", f.PoisonChance >= f.PoisonFixed && f.InHand
                        ? "this stack gives food poisoning " + Pct(odds) + " of the time (its cook or its kitchen)"
                        : "its kind gives people food poisoning " + Pct(odds) + " of the time"));
            }

            // Drugs: addiction, overdose, the policies.
            if (ShowsDrug(f))
            {
                if (f.Addictive && f.Addictiveness > 0f)
                    tags.Add(T("addictive", f.MinTolerance > 0.0001f
                        ? "each dose may addict (" + Pct(f.Addictiveness) + ") once its tolerance is " + Pct(f.MinTolerance) + " or more"
                        : "each dose may addict (" + Pct(f.Addictiveness) + ")"));
                if (f.OverdoseMax > 0f || f.LargeOverdose > 0f)
                {
                    string why = f.OverdoseMax > 0f
                        ? "each dose adds " + (Math.Abs(f.OverdoseMax - f.OverdoseMin) < 0.005f ? Pct(f.OverdoseMax) : Pct(f.OverdoseMin).TrimEnd('%') + " to " + Pct(f.OverdoseMax))
                          + " toward an overdose, which kills at 100%"
                        : "a dose may bring on an overdose, which kills at 100%";
                    if (f.LargeOverdose > 0f) why += "; " + Pct(f.LargeOverdose) + " of doses are a large one";
                    tags.Add(T("overdose", why));
                }
                if (f.HasColony && f.PolicyOf > 0)
                    tags.Add(T("drug-policy", "colonists take it only as their drug policy allows: " + f.PolicyAllows + " of " + f.PolicyOf + " allow it"));
            }

            // The colony's people who won't have it.
            if (f.HasColony && f.ForbiddenFor > 0 && f.Kind != ItemKind.Medicine)
                tags.Add(T("forbidden", f.ForbiddenFor + " of the colony's " + f.Grown + " grown people won't have it" + (f.ForbiddenWho.Length > 0 ? ": " + f.ForbiddenWho : ""),
                    f.ForbiddenFor * 2 > f.Grown ? "Forbidden to most" : null));

            // Medicine.
            if (f.Kind == ItemKind.Medicine)
            {
                if (f.Potency >= f.IndustrialPotency - 0.0001f)
                    tags.Add(T("potent", "medical potency " + Pct(f.Potency) + ", at least industrial medicine's " + Pct(f.IndustrialPotency) + "; its tending reaches " + Pct(f.QualityMax) + " at best"));
                else
                    tags.Add(T("weak-surgery", "medical potency " + Pct(f.Potency) + ", under industrial medicine's " + Pct(f.IndustrialPotency) + ": surgery with it fails more often, and its tending stops at " + Pct(f.QualityMax)));
                if (f.MedicineByNeed)
                {
                    if (CareTier(f) <= MedicineChoiceMath.Herbal)
                        tags.Add(T("for-minor", "with medicine by what is treated, it is what cuts, bites, small burns and everyday ailments get; the serious get better where the care allows"));
                    else
                        tags.Add(T("for-serious", "with medicine by what is treated, it is kept for the serious: heavy bleeding, deep wounds, a vital part, illness and infection; scratches get herbal or none"));
                }
                if (f.HasColony && MedicineShort(f.MedicineHeld, f.People))
                    tags.Add(T("needed", "the colony holds " + f.MedicineHeld + " medicine of every kind, under the " + MedicineNeed(f.People) + " its " + f.People + " people need"));
                else if (f.HasColony && MedicineSpare(f.MedicineHeld, f.People))
                    tags.Add(T("spare", "the colony holds " + f.MedicineHeld + " medicine of every kind, more than the " + MedicineKeep(f.People) + " the trade advice keeps"));
            }

            // Food in the colony.
            if (food && f.HumanEdible && f.HasColony)
            {
                if (FoodShort(f.FoodDays))
                    tags.Add(T("needed", "the colony has " + Days(f.FoodDays) + " of food, under the " + N(TradeAdviceMath.FoodDaysAtLeast) + " the trade advice keeps at least"));
                else if (FoodSpare(f.FoodDays))
                    tags.Add(T("spare", "the colony has " + Days(f.FoodDays) + " of food, more than twice the " + N(TradeAdviceMath.FoodDaysAtLeast) + " it keeps"));
            }

            // Rimfolk's roles.
            if (f.FeastDish) tags.Add(T("feast-dish", "a fine or lavish meal a recipe cooks: what a feast sets on the table (the Empire's guests expect a fine meal)"));
            if (f.ToastDrink)
                tags.Add(T("toast-drink", f.Alcohol
                    ? "a social drink with alcohol: poured at Rimfolk's toasts"
                    : "a social drink a teetotaler may have: poured at Rimfolk's toasts for those who don't drink"));
            if (f.Gift) tags.Add(T("gift", "a fine drink worth " + N(f.Value) + " silver: a gift between peoples (more often in the middle ages)"));
            if (f.Provisions)
                tags.Add(T("provisions", f.TravelRank >= 2
                    ? "a meal that never rots: the journey planner packs it first"
                    : "a meal that keeps " + Days(f.RotDays) + ": the journey planner packs it before what rots sooner"));
            float perKg = ValuePerKg(f);
            if (f.Tradeable && perKg >= TradeValuePerKg && (f.RotDays < 0f || f.RotDays >= ThingRolesMath.KeepsDays))
                tags.Add(T("trade-good", "it keeps, and is worth " + Math.Round(perKg).ToString("0", Inv) + " silver a kilogram: much worth in a pack"));
            return tags;
        }

        // ---------------- The line in plain words ----------------

        /// <summary>
        /// What it is good for in this colony, in one line: "Fine meal: lifts the mood (+5), keeps 4 days, enough for 2
        /// colonists' supper." "Herbal medicine: fine for a cut, risky for surgery." At most four parts, the most telling first.
        /// </summary>
        public static string Plain(ItemFacts f)
        {
            if (f == null || f.Kind == ItemKind.None) return "";
            if (f.Kind == ItemKind.Material) return MaterialPlain(f);
            if (f.Kind == ItemKind.Art) return ArtPlain(f);
            if (StepThree(f.Kind)) return UsePlain(f);
            var parts = new List<string>();
            switch (f.Kind)
            {
                case ItemKind.Medicine:
                {
                    int tier = CareTier(f);
                    if (tier <= MedicineChoiceMath.Herbal)
                    {
                        parts.Add("fine for a cut or a cold");
                        if (f.Potency < f.IndustrialPotency - 0.0001f) parts.Add("risky for surgery");
                    }
                    else if (tier == MedicineChoiceMath.Normal)
                    {
                        parts.Add("right for serious wounds and illness");
                        parts.Add("safe for surgery");
                    }
                    else
                    {
                        parts.Add("the best for surgery and the gravest wounds");
                        parts.Add("wasted on a scratch");
                    }
                    if (f.HasColony && MedicineShort(f.MedicineHeld, f.People)) parts.Add("the colony is short of medicine");
                    break;
                }
                case ItemKind.Drug:
                case ItemKind.Drink when f.IsDrug:
                {
                    if (f.ToastDrink) parts.Add(f.Alcohol ? "a toast's drink" : "a toast's drink for those who don't drink");
                    else if (f.DrugKind == 1) parts.Add("a medical drug");
                    else if (f.DrugKind == 3) parts.Add("a hard drug");
                    if (f.HighMood > 0f) parts.Add("lifts the mood (" + Mood(f.HighMood) + ")" + (f.HighDays > 0f ? " for " + Days(f.HighDays) : ""));
                    else if (f.Joy > 0f) parts.Add("recreation");
                    if (f.OverdoseMax > 0f || f.LargeOverdose > 0f) parts.Add("can overdose");
                    if (f.Addictive && f.Addictiveness > 0f) parts.Add(f.Addictiveness <= 0.011f ? "rarely habit-forming" : "addictive");
                    else parts.Add("not habit-forming");
                    break;
                }
                default:
                    FoodParts(f, parts);
                    break;
            }
            if (parts.Count > 4) parts = parts.Take(4).ToList();
            return Cap(f.Label) + ": " + string.Join(", ", parts) + ".";
        }

        private static void FoodParts(ItemFacts f, List<string> parts)
        {
            if (f.OfPerson) parts.Add("of a person");
            else if (f.Insect) parts.Add("insect meat");
            if (!f.HumanEdible)
            {
                parts.Add("animal feed");
                parts.Add(Keeps(f));
                return;
            }
            if (f.TasteMood > 0f) parts.Add("lifts the mood (" + Mood(f.TasteMood) + ")");
            else if (f.TasteMood < 0f) parts.Add(RawToCook(f) ? "best cooked (" + Mood(f.TasteMood) + " eaten raw)" : "sours the mood (" + Mood(f.TasteMood) + ")");
            parts.Add(Keeps(f));
            if (f.Meal && f.Nutrition >= MealNutrition - 0.0001f)
            {
                // A meal is a supper: one a colonist, a stack in hand as many as it has.
                int n = Suppers(f.Nutrition, f.Count);
                parts.Add(f.InHand && n > 1 ? "enough for " + n + " colonists' supper" : "a supper for one colonist");
            }
            else if (f.InHand && f.Count > 1 && ColonistDays(f.Nutrition, f.Count) >= 0.995f)
                parts.Add("feeds a colonist " + Days(ColonistDays(f.Nutrition, f.Count)));
            else if (f.Nutrition > 0f) parts.Add(ToAMeal(f.Nutrition) + " to a meal's worth");
            if (f.Provisions) parts.Add("packed for journeys");
        }

        /// <summary>Raw food that is better cooked: the game's "ate raw food" mood eaten as it is, and a recipe of the colony's makes a meal of it.</summary>
        public static bool RawToCook(ItemFacts f) => Raw(f) && f.Cookable;

        /// <summary>How it keeps, in a few words: "never spoils", "keeps 4 days", "keeps 2.5 more days", "kept frozen", "already rotting".</summary>
        public static string Keeps(ItemFacts f)
        {
            if (f.Rotting) return "already rotting";
            if (f.RotDays < 0f) return "never spoils";
            if (f.InHand && f.FrozenHere) return "kept frozen";
            if (f.InHand && f.DaysLeftHere >= 0f) return "keeps " + Days(f.DaysLeftHere) + " more";
            return "keeps " + Days(f.RotDays);
        }

        /// <summary>The words of Rimfolk's tags and line: no em dashes, no exclamation marks (the tests read every one).</summary>
        public static bool CleanWords(string s) => s != null && s.IndexOf('\u2014') < 0 && s.IndexOf('!') < 0;
    }
}
