using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Something a colonist's beliefs rule out: their ideoligion's precepts (the game's, Vanilla Ideology Expanded's
    /// memes, Alpha Memes), and a few traits that say the same (a nudist, a teetotaler, a body purist). Wishes, life
    /// goals and council projects that clash with one are never raised; a feast or a toast never serves what clashes
    /// with one (see <see cref="Beliefs"/>).
    /// </summary>
    public enum Belief
    {
        /// <summary>Lovin, and so sharing a bed, only within marriage: any lovin precept but the free ones.</summary>
        LovinInMarriage,
        /// <summary>No lovin at all, married or not.</summary>
        NoLovin,
        /// <summary>No meat (vegetarian precepts).</summary>
        NoMeat,
        /// <summary>Meat only: no plants (carnivore precepts).</summary>
        MeatOnly,
        /// <summary>No animal products at all, not even eggs, milk or honey (strict vegans).</summary>
        NoAnimalProducts,
        /// <summary>Fungus despised.</summary>
        NoFungus,
        /// <summary>Fungus preferred, and plants that aren't fungus despised.</summary>
        FungusOnly,
        /// <summary>Insect meat despised or sacrilegious.</summary>
        NoInsectMeat,
        /// <summary>
        /// Alpha Memes' fungus and insect meat both required: anything else (but drugs and water) makes a colonist
        /// sick, so only fungus, insect meat, insect jelly or meals with some of either.
        /// </summary>
        FungusOrInsectOnly,
        /// <summary>Nutrient paste disgusting, or forbidden (Alpha Memes makes them sick).</summary>
        NoPaste,
        /// <summary>Good food expected: a simple meal or raw food is a disgrace (Alpha Memes).</summary>
        FineMealsOnly,
        /// <summary>No alcohol (More Precepts, Vanilla Ideology Expanded's mild abstinence, teetotalers).</summary>
        NoAlcohol,
        /// <summary>No social or recreational drugs: beer, smokeleaf (medical use only, or none at all).</summary>
        NoSocialDrugs,
        /// <summary>No hard drugs.</summary>
        NoHardDrugs,
        /// <summary>No drugs at all, not even medical ones.</summary>
        NoDrugs,
        /// <summary>Darkness preferred: no lamps.</summary>
        Darkness,
        /// <summary>Darklight preferred: a lamp, but a darklight one.</summary>
        Darklight,
        /// <summary>Nudity preferred or required: no clothes asked for.</summary>
        Nudity,
        /// <summary>No armour (Alpha Memes).</summary>
        NoArmor,
        /// <summary>No prosthetics or bionics.</summary>
        NoProsthetics,
        /// <summary>Violence abhorrent: no weapons, hunts or executions asked for.</summary>
        NoViolence,
        /// <summary>Killing innocent animals disapproved: no hunts.</summary>
        NoHunting,
        /// <summary>Executions disapproved.</summary>
        NoExecution,
        /// <summary>Recreation frowned on or abhorrent.</summary>
        NoRecreation,
        /// <summary>Travel despised.</summary>
        NoTravel,
        /// <summary>The surface is no place for them: indoors required (Alpha Memes).</summary>
        Indoors,
        /// <summary>Fire despised.</summary>
        NoFire,
        /// <summary>Fishing forbidden.</summary>
        NoFishing,
        /// <summary>Reading disliked.</summary>
        NoBooks,
        /// <summary>Bonding with animals abhorrent: no pets.</summary>
        NoPets,
        /// <summary>Barracks preferred: a private room is a frivolity.</summary>
        Barracks,
        /// <summary>Small rooms preferred.</summary>
        SmallRooms,
        /// <summary>Discomfort preferred.</summary>
        Discomfort,
        /// <summary>Religion despised or disliked: no shrine.</summary>
        NoReligion,
        /// <summary>Slab beds preferred.</summary>
        SlabBeds,
        /// <summary>Ranching disliked.</summary>
        NoRanching,
        /// <summary>Leather clothes disliked.</summary>
        NoLeather
    }

    /// <summary>What's known about one thing to eat or drink, for <see cref="BeliefMath.WhyNotEat"/>.</summary>
    public struct FoodFacts
    {
        /// <summary>Eating it would give a bad "ate ..." memory (the game's own list of what eating it does).</summary>
        public bool badMemory;
        /// <summary>Fungus, or a meal with some fungus in it (as Alpha Memes reads it).</summary>
        public bool fungus;
        /// <summary>Insect meat, an insect's body, or a meal with insect meat in it.</summary>
        public bool insect;
        public bool insectJelly;
        public bool drug;
        /// <summary>Plain water (Dubs Bad Hygiene's bottle), which Alpha Memes lets anyone drink.</summary>
        public bool water;
        public bool paste;
        public bool alcohol;
        public bool meat;
        /// <summary>A plant, or with plants in it (fungus counts).</summary>
        public bool plant;
        /// <summary>Eggs, milk, honey and the like, or with them in it.</summary>
        public bool animalProduct;
        public bool simpleMeal;
        public bool rawBad;
        /// <summary>A meal whose ingredients aren't known (a kind of meal, not one on a shelf): judged by kind alone.</summary>
        public bool unknownIngredients;
    }

    /// <summary>
    /// The pure part of <see cref="Beliefs"/>: which precepts mean what, and what a colonist with those beliefs won't
    /// eat, ask for or share. No game types, so it can be tested on its own.
    /// </summary>
    public static class BeliefMath
    {
        public static ulong Bit(Belief b) => 1UL << (int)b;

        public static bool Has(ulong mask, Belief b) => (mask & Bit(b)) != 0;

        /// <summary>Every belief in <paramref name="beliefs"/> (none for an empty list).</summary>
        public static ulong Mask(IEnumerable<Belief> beliefs)
        {
            ulong m = 0;
            if (beliefs == null) return m;
            foreach (var b in beliefs) m |= Bit(b);
            return m;
        }

        /// <summary>Holds any of the beliefs in <paramref name="any"/>.</summary>
        public static bool HasAny(ulong mask, ulong any) => (mask & any) != 0;

        public const string FungusRequired = "AM_FungusEating_Required";
        public const string InsectMeatRequired = "AM_InsectMeatEating_Required";
        public const string PasteForbidden = "AM_NutrientPasteEating_Forbidden";
        public const string MealsQualityDesired = "AM_Meals_QualityDesired";
        public const string LovinIssue = "Lovin";

        /// <summary>
        /// Precepts by name, and what each rules out: the game's Ideology, Vanilla Ideology Expanded - Memes, Alpha Memes
        /// and More Precepts. A precept that isn't loaded is simply never found on anyone's ideoligion.
        /// </summary>
        private static readonly Dictionary<string, Belief[]> ByPrecept = new Dictionary<string, Belief[]>(StringComparer.Ordinal)
        {
            // Lovin (the game's): anything but free lovin keeps it, and a shared bed, to the married.
            { "Lovin_SpouseOnly_Mild", new[] { Belief.LovinInMarriage } },
            { "Lovin_SpouseOnly_Moderate", new[] { Belief.LovinInMarriage } },
            { "Lovin_SpouseOnly_Strict", new[] { Belief.LovinInMarriage } },
            { "Lovin_Prohibited", new[] { Belief.NoLovin } },
            { "Lovin_Horrible", new[] { Belief.NoLovin } },
            // Diet.
            { "MeatEating_Disapproved", new[] { Belief.NoMeat } },
            { "MeatEating_Horrible", new[] { Belief.NoMeat } },
            { "MeatEating_Abhorrent", new[] { Belief.NoMeat } },
            { "VME_MeatEating_Abhorrent_Strict", new[] { Belief.NoMeat, Belief.NoAnimalProducts } },
            { "MeatEating_NonMeat_Disapproved", new[] { Belief.MeatOnly } },
            { "MeatEating_NonMeat_Horrible", new[] { Belief.MeatOnly } },
            { "MeatEating_NonMeat_Abhorrent", new[] { Belief.MeatOnly } },
            { "FungusEating_Despised", new[] { Belief.NoFungus } },
            { "FungusEating_Preferred", new[] { Belief.FungusOnly } },
            { "InsectMeatEating_Despised_Classic", new[] { Belief.NoInsectMeat } },
            { "VME_InsectMeatEating_Sacrilegious", new[] { Belief.NoInsectMeat } },
            { "NutrientPasteEating_Disgusting", new[] { Belief.NoPaste } },
            { PasteForbidden, new[] { Belief.NoPaste } },
            { MealsQualityDesired, new[] { Belief.FineMealsOnly } },
            // Drink and drugs.
            { "DrugUse_Prohibited", new[] { Belief.NoDrugs } },
            { "DrugUse_MedicalOnly", new[] { Belief.NoSocialDrugs } },
            { "DrugUse_MedicalOrSocial", new[] { Belief.NoHardDrugs } },
            { "Alcohol_Prohibited", new[] { Belief.NoAlcohol } },
            { "Alcohol_Disapproved", new[] { Belief.NoAlcohol } },
            { "VME_Alcohol_MildAbstinence", new[] { Belief.NoAlcohol } },
            // Light.
            { "Darklight_Preferred", new[] { Belief.Darklight } },
            // Clothes and the body.
            { "Nudity_Male_Mandatory", new[] { Belief.Nudity } },
            { "Nudity_Female_Mandatory", new[] { Belief.Nudity } },
            { "Nudity_Male_CoveringAnythingButGroinDisapproved", new[] { Belief.Nudity } },
            { "Nudity_Female_CoveringAnythingButGroinDisapproved", new[] { Belief.Nudity } },
            { "AM_Nudity_Male_Strict", new[] { Belief.Nudity } },
            { "AM_Nudity_Female_Strict", new[] { Belief.Nudity } },
            { "AM_Armour_Forbidden", new[] { Belief.NoArmor } },
            { "BodyMod_Disapproved", new[] { Belief.NoProsthetics } },
            { "BodyMod_Abhorrent", new[] { Belief.NoProsthetics } },
            { "VME_BodyMod_OnlyBiological", new[] { Belief.NoProsthetics } },
            { "VME_LeatherApparel_Abhorrent", new[] { Belief.NoLeather } },
            { "VME_LeatherApparel_Disliked", new[] { Belief.NoLeather } },
            // Violence, hunting and executions.
            { "VME_Violence_Abhorrent", new[] { Belief.NoViolence } },
            { "VME_Violence_Disapproved", new[] { Belief.NoViolence } },
            { "AM_Violence_Abhorrent_Strict", new[] { Belief.NoViolence } },
            { "KillingInnocentAnimals_Disapproved", new[] { Belief.NoHunting } },
            { "KillingInnocentAnimals_Horrible", new[] { Belief.NoHunting } },
            { "KillingInnocentAnimals_Abhorrent", new[] { Belief.NoHunting } },
            { "Execution_Abhorrent", new[] { Belief.NoExecution } },
            { "Execution_Horrible", new[] { Belief.NoExecution } },
            // Everyday life.
            { "AM_Recreation_Abhorrent", new[] { Belief.NoRecreation } },
            { "VME_Recreation_Disapproved", new[] { Belief.NoRecreation } },
            { "VME_Travel_Despised", new[] { Belief.NoTravel } },
            { "AM_Indoors_Required", new[] { Belief.Indoors } },
            { "VME_Fire_Despised", new[] { Belief.NoFire } },
            { "VME_Fishing_Forbidden", new[] { Belief.NoFishing } },
            { "VME_BookReading_Disliked", new[] { Belief.NoBooks } },
            { "AM_Bonding_Abhorrent", new[] { Belief.NoPets } },
            { "AM_Barracks_Preferred", new[] { Belief.Barracks } },
            { "AM_Barracks_PreferredTrue", new[] { Belief.Barracks } },
            { "AM_RoomSize_Small", new[] { Belief.SmallRooms } },
            { "AM_Comfort_DiscomfortPreferred", new[] { Belief.Discomfort } },
            { "AM_Religion_Despised", new[] { Belief.NoReligion } },
            { "AM_Religion_Disliked", new[] { Belief.NoReligion } },
            { "SlabBed_Preferred", new[] { Belief.SlabBeds } },
            { "VME_Ranching_Disliked", new[] { Belief.NoRanching } },
        };

        /// <summary>What one precept rules out on its own (empty for one that rules nothing out).</summary>
        public static IReadOnlyList<Belief> Of(string preceptDefName) =>
            preceptDefName != null && ByPrecept.TryGetValue(preceptDefName, out var b) ? b : Array.Empty<Belief>();

        /// <summary>The precepts (by name) that rule this out on their own.</summary>
        public static List<string> PreceptsFor(Belief belief)
        {
            var list = new List<string>();
            foreach (var kv in ByPrecept)
                if (Array.IndexOf(kv.Value, belief) >= 0) list.Add(kv.Key);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>All the precept names known here.</summary>
        public static IEnumerable<string> KnownPrecepts => ByPrecept.Keys;

        /// <summary>The free lovin precepts (the game's "free" and "free, approved"): the only ones that don't ask for marriage.</summary>
        public static bool FreeLovin(string preceptDefName) => preceptDefName != null && preceptDefName.StartsWith("Lovin_Free", StringComparison.Ordinal);

        /// <summary>
        /// What an ideoligion's precepts (each by name, with its issue's name) rule out together: each precept's own
        /// list, any lovin precept that isn't free lovin (a mod's too), and Alpha Memes' two "required" diets together.
        /// </summary>
        public static ulong FromPrecepts(IEnumerable<KeyValuePair<string, string>> precepts)
        {
            ulong m = 0;
            if (precepts == null) return m;
            bool fungus = false, insect = false;
            foreach (var kv in precepts)
            {
                string name = kv.Key;
                if (name == null) continue;
                foreach (var b in Of(name)) m |= Bit(b);
                if (kv.Value == LovinIssue && !FreeLovin(name) && !Has(m, Belief.NoLovin)) m |= Bit(Belief.LovinInMarriage);
                if (name == FungusRequired) fungus = true;
                else if (name == InsectMeatRequired) insect = true;
            }
            if (fungus && insect) m |= Bit(Belief.FungusOrInsectOnly);
            return Close(m);
        }

        /// <summary>What follows from what: no lovin rules out lovin outside marriage too, no drugs rules out beer, and so on.</summary>
        public static ulong Close(ulong m)
        {
            if (Has(m, Belief.NoLovin)) m |= Bit(Belief.LovinInMarriage);
            if (Has(m, Belief.NoDrugs)) m |= Bit(Belief.NoSocialDrugs);
            if (Has(m, Belief.NoSocialDrugs)) m |= Bit(Belief.NoAlcohol) | Bit(Belief.NoHardDrugs);
            if (Has(m, Belief.NoAnimalProducts)) m |= Bit(Belief.NoMeat);
            if (Has(m, Belief.NoViolence)) m |= Bit(Belief.NoHunting) | Bit(Belief.NoExecution);
            return m;
        }

        /// <summary>The beliefs about food and drink (who has none of these eats whatever the game lets them).</summary>
        public static readonly ulong Diet = Mask(new[]
        {
            Belief.NoMeat, Belief.MeatOnly, Belief.NoAnimalProducts, Belief.NoFungus, Belief.FungusOnly, Belief.NoInsectMeat, Belief.FungusOrInsectOnly,
            Belief.NoPaste, Belief.FineMealsOnly, Belief.NoAlcohol
        });

        /// <summary>
        /// Two partners sharing a bed goes against one of their faiths: either keeps lovin to the married and they aren't,
        /// or either forbids lovin altogether (a shared bed is never only for sleeping, in the game).
        /// </summary>
        public static bool SharingBedClashes(bool married, ulong a, ulong b)
        {
            if (Has(a, Belief.NoLovin) || Has(b, Belief.NoLovin)) return true;
            return !married && (Has(a, Belief.LovinInMarriage) || Has(b, Belief.LovinInMarriage));
        }

        /// <summary>
        /// Why someone with these beliefs won't eat or drink this, or null if they may: a bad memory from eating it, a
        /// diet their faith keeps, Alpha Memes' rule that makes them sick (fungus and insect required, paste forbidden), a
        /// plain meal to someone who expects better, or alcohol to someone who abstains. Drugs' own rules (the drug
        /// policy, drug precepts) are checked apart.
        /// </summary>
        public static string WhyNotEat(ulong beliefs, FoodFacts f)
        {
            if (f.badMemory) return "memory";
            // Alpha Memes: everything but fungus, insect meat, insect jelly, drugs and water makes them sick.
            if (Has(beliefs, Belief.FungusOrInsectOnly) && !f.drug && !f.water && !f.unknownIngredients && !(f.fungus || f.insect || f.insectJelly))
                return "fungusOrInsect";
            if (Has(beliefs, Belief.NoPaste) && f.paste) return "paste";
            if (Has(beliefs, Belief.FineMealsOnly) && (f.simpleMeal || f.rawBad)) return "plain";
            if (Has(beliefs, Belief.NoAlcohol) && f.alcohol) return "alcohol";
            if (f.drug) return null;
            if (Has(beliefs, Belief.NoAnimalProducts) && (f.animalProduct || f.meat)) return "animalProduct";
            if (Has(beliefs, Belief.NoMeat) && f.meat) return "meat";
            if (Has(beliefs, Belief.MeatOnly) && f.plant) return "plant";
            if (Has(beliefs, Belief.NoFungus) && f.fungus) return "fungus";
            if (Has(beliefs, Belief.FungusOnly) && f.plant && !f.fungus) return "notFungus";
            if (Has(beliefs, Belief.NoInsectMeat) && f.insect) return "insect";
            return null;
        }

        /// <summary>Fit to eat for someone with these beliefs (see <see cref="WhyNotEat"/>).</summary>
        public static bool FitToEat(ulong beliefs, FoodFacts f) => WhyNotEat(beliefs, f) == null;
    }
}
