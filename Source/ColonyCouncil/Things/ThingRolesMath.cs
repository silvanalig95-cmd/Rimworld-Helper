using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What a thing is for, where Rimfolk picks one (DESIGN.md, "Things by what they are for"): the game's and the player's
    /// mods' items answer to a role by what they are, never by name, so a mod's wines, dishes, carvings and blades come in on
    /// their own, each in the ages that have the means for it.
    /// </summary>
    public enum ThingRole
    {
        /// <summary>A drink raised in company: a social drink with alcohol in it (beer, a mod's wine, mead, cider or spirits).</summary>
        Drink,
        /// <summary>A drink for one who may not have alcohol: a social drink a teetotaler may have (a mod's tea, coffee or soda).</summary>
        SoftDrink,
        /// <summary>A dish for a feast: a fine or lavish meal, cooked from a recipe, fresh.</summary>
        Feast,
        /// <summary>Food that keeps on the road: a simple meal or better that lasts twenty days or more (pemmican, a survival meal, a mod's cheese).</summary>
        Provisions,
        /// <summary>A farm's crop: raw vegetables, grain or fruit that is sown.</summary>
        Produce,
        /// <summary>Medicine.</summary>
        Medicine,
        /// <summary>A precious material: metal or stone that makes things beautiful (silver, gold, jade, a mod's obsidian).</summary>
        Precious,
        /// <summary>A small work of art made at a bench, of any material (a small sculpture, a mod's small statue or pottery).</summary>
        SmallArt,
        /// <summary>A small work of art that may be carved of wood (what a traveller carries in a pack).</summary>
        Carving,
        /// <summary>A gift of one people to another, counted by worth: a precious material, or a fine drink.</summary>
        Gift,
        /// <summary>A melee weapon a smith forges of metal (a spear, a sword, a mod's blades).</summary>
        Blade
    }

    /// <summary>
    /// What Rimfolk knows of a thing to judge it for a role (ThingRoles.Facts reads it from the game's def; the tests read
    /// the same from the player's catalogue, docs/player-mods/). Tech levels as numbers: 0 undefined, 1 animal, 2 neolithic,
    /// 3 medieval, 4 industrial, 5 spacer, 6 ultra, 7 archotech.
    /// </summary>
    public sealed class ThingFacts
    {
        public string defName;
        /// <summary>An item (not a building, a plant or a pawn).</summary>
        public bool item;
        /// <summary>A building (works of art are buildings, carried minified).</summary>
        public bool building;
        /// <summary>A weapon.</summary>
        public bool weapon;
        /// <summary>Its own tech level.</summary>
        public int tech;
        /// <summary>The tech level of the research the easiest way of making or sowing it needs (0 none needed); -1 when nothing makes it.</summary>
        public int madeTech = -1;
        public float value;
        public int stack = 1;
        /// <summary>Never traded (a quest's item, a mod's special one).</summary>
        public bool untradeable;
        /// <summary>Something to use rather than have (a comp that is used, targeted, bonded or hatches), or marked by a trade tag of its own (exotic, an artifact, a mod's tag).</summary>
        public bool special;
        /// <summary>Of a person: human meat, a person's leather, a corpse.</summary>
        public bool humanlike;
        /// <summary>A drug, and its kind: 0 none, 1 medical, 2 social, 3 hard.</summary>
        public bool drug;
        public int drugKind;
        /// <summary>Alcohol is in it (<see cref="ThingRolesMath.Alcoholic"/>).</summary>
        public bool alcohol;
        /// <summary>A teetotaler may have it (a drug's own flag).</summary>
        public bool soft;
        /// <summary>Its food type: a drink, a meal, a vegetable or fruit; people may eat it; kept with the meals (the game's meals category: a mod's juice).</summary>
        public bool fluid, meal, vegetable, humanEdible, withMeals;
        /// <summary>How much people like to eat it (the game's order, <see cref="ThingRolesMath.Preferabilities"/>).</summary>
        public int preferability;
        public float nutrition;
        /// <summary>Days before it starts to rot; -1 when it never rots.</summary>
        public float rotDays = -1f;
        public float potency;
        /// <summary>As a material: metal, stone, something else besides (wax, cloth), and how much beauty it gives.</summary>
        public bool metallic, stony, otherStuff;
        public float beauty;
        /// <summary>What it may be made of: wood, stone, metal.</summary>
        public bool ofWood, ofStone, ofMetal;
        /// <summary>A work of art, kept packed (minified).</summary>
        public bool art, minified;
        /// <summary>Art that does more than please: frightens (a terror sculpture), holds the dead (a sarcophagus, a mummy).</summary>
        public bool terror, holdsDead;
        public float work;
        /// <summary>
        /// A weapon's facts: a melee weapon with an edge or a point (it cuts or stabs), given to pawns by the game's own
        /// weapon tags (not only a mod's people's own), bonded to one person.
        /// </summary>
        public bool melee, edged, tagged, bonded;
    }

    /// <summary>The rules of <see cref="ThingRole"/>, pure (no game types): Tools/Tests checks them on clear cases and against the player's own catalogue.</summary>
    public static class ThingRolesMath
    {
        /// <summary>The game's tech levels in order (TechLevel).</summary>
        public static readonly string[] Techs = { "Undefined", "Animal", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech" };

        /// <summary>The game's food preferabilities in order (FoodPreferability).</summary>
        public static readonly string[] Preferabilities =
            {
                "Undefined", "NeverForNutrition", "DesperateOnly", "DesperateOnlyForHumanlikes", "RawBad", "RawTasty", "MealTerrible", "MealAwful", "MealSimple",
                "MealFine", "MealLavish"
            };

        public const int RawBad = 4, RawTasty = 5, MealSimple = 8, MealFine = 9, MealLavish = 10;

        /// <summary>The food types people eat (FoodTypeFlags.OmnivoreHuman).</summary>
        public static readonly HashSet<string> HumanFoodTypes = new HashSet<string>
            { "VegetableOrFruit", "Meat", "Fluid", "Corpse", "Seed", "AnimalProduct", "Meal", "Processed", "Liquor", "Kibble", "Fungus" };

        /// <summary>Food that keeps this long counts as provisions.</summary>
        public const float KeepsDays = 20f;

        /// <summary>A small work of art is a day or so of an artist's work (the game's small sculpture is 18,000).</summary>
        public const float SmallArtWork = 20000f;

        /// <summary>A drink dearer than this is a treasure, not a round (Vanilla Brewing's ambrandy).</summary>
        public const float DrinkValueCap = 100f;

        /// <summary>A drink as a gift: worth this much a bottle at least.</summary>
        public const float FineDrinkValue = 15f;

        /// <summary>
        /// The game's trade tags that mark a thing as more than an ordinary good: exotic, an artifact, a serum, an implant.
        /// A tag the game itself never uses is a mod's own mark and counts the same (ThingRoles.Facts).
        /// </summary>
        public static readonly HashSet<string> SpecialTags = new HashSet<string>
        {
            "Exotic", "ExoticMisc", "ExoticBuilding", "Artifact", "Artifacts", "Serum", "TechHediff", "Bionic", "BodyPartOrImplant",
            "ImplantEmpireCommon", "ImplantEmpireRoyal", "PsylinkNeuroformer", "UtilitySpecial", "NonContractable", "GravshipUpgrade", "AnimalPart"
        };

        /// <summary>Comps that make a thing one to use rather than to have (by the class name's words: used, targeted, bonded, hatching, exploding).</summary>
        public static readonly string[] SpecialComps = { "Usable", "UseEffect", "Targetable", "Biocod", "Bladelink", "Hatcher", "Explosive", "Spawner", "Studiable", "Relic" };

        /// <summary>
        /// Alcohol is in a drink: its drug's chemical is alcohol, or it is liquor by its food type with no other chemical
        /// and not one a teetotaler may have (a mod's coffee is "liquor" by its food type, with caffeine in it).
        /// </summary>
        public static bool Alcoholic(bool chemicalIsAlcohol, string otherChemical, bool liquor, bool soft) =>
            chemicalIsAlcohol || liquor && string.IsNullOrEmpty(otherChemical) && !soft;

        /// <summary>A weapon tag of the game's own: one the base game's weapons carry (a mod's people's own tag isn't).</summary>
        public static bool GameWeaponTag(string tag, ICollection<string> gameTags) => !string.IsNullOrEmpty(tag) && gameTags != null && gameTags.Contains(tag);

        /// <summary>A comp of one of the kinds that make a thing special.</summary>
        public static bool SpecialComp(string compName)
        {
            if (string.IsNullOrEmpty(compName)) return false;
            foreach (var w in SpecialComps)
                if (compName.IndexOf(w, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>A trade tag that marks a thing as special: one of the game's own (<see cref="SpecialTags"/>), or one the game never uses.</summary>
        public static bool SpecialTag(string tag, ICollection<string> gameTags) =>
            !string.IsNullOrEmpty(tag) && (SpecialTags.Contains(tag) || gameTags != null && !gameTags.Contains(tag));

        /// <summary>Index of a name in the game's order (tech levels, preferabilities), 0 when unknown.</summary>
        public static int IndexOf(string[] names, string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int i = Array.IndexOf(names, name.Trim());
            return i < 0 ? 0 : i;
        }

        /// <summary>The age a thing belongs to: its own tech level or the research its making needs, whichever is later; -1 when neither is known.</summary>
        public static int AgeOf(ThingFacts f)
        {
            if (f == null) return -1;
            if (f.tech <= 0 && f.madeTech < 0) return -1;
            return (int)LedgerMath.AgeOf(Math.Max(f.tech, f.madeTech));
        }

        /// <summary>
        /// The thing belongs to this age or an earlier one. A thing whose age isn't known goes with every age, but for
        /// provisions (packaged or canned food of a mod's factory or press) only with industry and after.
        /// </summary>
        public static bool InAge(ThingRole role, ThingFacts f, WorldAge age)
        {
            int a = AgeOf(f);
            if (a < 0) return role != ThingRole.Provisions || age >= WorldAge.Industrial;
            return a <= (int)age;
        }

        /// <summary>An ordinary good anyone may have and give: traded, not special, not of a person, worth something.</summary>
        public static bool Ordinary(ThingFacts f) => f != null && !f.untradeable && !f.special && !f.humanlike;

        /// <summary>The thing is of the role's kind (whatever its age).</summary>
        public static bool Fits(ThingRole role, ThingFacts f)
        {
            if (!Ordinary(f)) return false;
            switch (role)
            {
                case ThingRole.Drink:
                case ThingRole.SoftDrink:
                {
                    if (!f.item || !f.drug || f.drugKind != 2 || !f.fluid || f.value <= 0f || f.value > DrinkValueCap) return false;
                    return role == ThingRole.Drink ? f.alcohol : f.soft && !f.alcohol;
                }
                case ThingRole.Feast:
                    return f.item && !f.drug && f.humanEdible && f.meal && (f.preferability == MealFine || f.preferability == MealLavish) && f.madeTech >= 0
                           && f.rotDays >= 0f && f.nutrition > 0f;
                case ThingRole.Provisions:
                    return f.item && !f.drug && f.humanEdible && f.meal && f.preferability >= MealSimple && f.preferability <= MealLavish
                           && (f.rotDays < 0f || f.rotDays >= KeepsDays) && f.nutrition > 0f;
                case ThingRole.Produce:
                    return f.item && !f.drug && f.humanEdible && f.vegetable && !f.meal && !f.fluid && !f.withMeals && (f.preferability == RawBad || f.preferability == RawTasty)
                           && f.madeTech >= 0 && f.nutrition > 0f;
                case ThingRole.Medicine:
                    return f.item && f.potency > 0f && f.value > 0f;
                case ThingRole.Precious:
                    return f.item && (f.metallic || f.stony) && !f.otherStuff && f.beauty >= 2f && f.stack >= 10 && f.value > 0f;
                case ThingRole.SmallArt:
                case ThingRole.Carving:
                    return f.building && f.art && f.minified && f.madeTech >= 0 && !f.terror && !f.holdsDead && f.work > 0f && f.work <= SmallArtWork
                           && (role == ThingRole.Carving ? f.ofWood : f.ofWood || f.ofStone || f.ofMetal);
                case ThingRole.Gift:
                    return Fits(ThingRole.Precious, f) || Fits(ThingRole.Drink, f) && f.value >= FineDrinkValue;
                case ThingRole.Blade:
                    return f.weapon && f.melee && f.edged && f.ofMetal && f.tagged && !f.bonded && f.madeTech >= 0;
            }
            return false;
        }

        /// <summary>Of the role and of the age.</summary>
        public static bool Fits(ThingRole role, ThingFacts f, WorldAge age) => Fits(role, f) && InAge(role, f, age);

        /// <summary>
        /// How likely a pick is among those that fit: a thing of the age's own twice as likely as an older one (a tribe toasts
        /// with what a tribe brews, a spacer colony with anything up to its own). By the giver's ways for a gift: a tribe's
        /// stone (jade), the middle ages' and the Empire's finest metal (gold, by the beauty it gives), the industrial and
        /// spacer ages' metal (silver); a fine drink now and then, more often in the middle ages. Medicine of the age's own
        /// three times as likely (a tribe's healing herbs).
        /// </summary>
        public static float Weight(ThingRole role, ThingFacts f, WorldAge age, bool empire = false)
        {
            if (f == null) return 0f;
            int a = AgeOf(f);
            float w = a == (int)age ? 2f : 1f;
            switch (role)
            {
                case ThingRole.Medicine:
                    if (a == (int)age) w *= 1.5f;
                    break;
                case ThingRole.Gift:
                case ThingRole.Precious:
                {
                    w = 1f;
                    if (Fits(ThingRole.Drink, f)) return age == WorldAge.Medieval && !empire ? 1.5f : 0.7f;
                    if (empire || age == WorldAge.Medieval) w = Math.Max(1f, f.beauty);
                    else if (age == WorldAge.Tribal) w = f.stony ? 3f : 1f;
                    else w = f.metallic ? 2f : 1f;
                    break;
                }
            }
            return w;
        }

        /// <summary>How many of a thing make up a worth (at least <paramref name="min"/>).</summary>
        public static int CountFor(float worth, float unitValue, int min = 1)
        {
            if (unitValue <= 0f || float.IsNaN(worth) || float.IsNaN(unitValue)) return min;
            return Math.Max(min, (int)Math.Round(worth / unitValue));
        }

        /// <summary>How good a food is for the road (the journey planner's order): one that never rots best, then one that keeps, then the rest.</summary>
        public static int TravelRank(ThingFacts f)
        {
            if (f == null || !Fits(ThingRole.Provisions, f)) return 0;
            return f.rotDays < 0f ? 2 : 1;
        }

        /// <summary>
        /// A pick by weight from a roll in [0, 1): the candidates laid end to end by their weights. -1 when there is
        /// nothing to pick (none, or no weight at all).
        /// </summary>
        public static int PickIndex(IList<float> weights, float roll)
        {
            if (weights == null || weights.Count == 0 || float.IsNaN(roll)) return -1;
            float total = 0f;
            foreach (var w in weights) total += w > 0f && !float.IsNaN(w) ? w : 0f;
            if (total <= 0f) return -1;
            float at = Math.Max(0f, Math.Min(0.999999f, roll)) * total;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = weights[i] > 0f && !float.IsNaN(weights[i]) ? weights[i] : 0f;
                if (w <= 0f) continue;
                if (at < w) return i;
                at -= w;
            }
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0f) return i;
            return -1;
        }

        /// <summary>A role's name in a def's text: "@Drink", "@Carving" (case ignored); false for a def's own name.</summary>
        public static bool TryParse(string token, out ThingRole role)
        {
            role = ThingRole.Drink;
            if (string.IsNullOrEmpty(token)) return false;
            token = token.Trim();
            if (!token.StartsWith("@", StringComparison.Ordinal)) return false;
            return Enum.TryParse(token.Substring(1), true, out role) && Enum.IsDefined(typeof(ThingRole), role);
        }
    }
}
